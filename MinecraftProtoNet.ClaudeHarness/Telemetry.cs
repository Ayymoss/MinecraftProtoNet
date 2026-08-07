using System.Text.Json;
using MinecraftProtoNet.Baritone.Api;
using MinecraftProtoNet.Baritone.Api.Event.Events;
using MinecraftProtoNet.Baritone.Api.Event.Listener;
using MinecraftProtoNet.Core.Core;

namespace MinecraftProtoNet.ClaudeHarness;

public enum RunOutcome
{
    Success,
    Fall,
    CalcFail,
    Death,
    Stuck,
    Timeout,
    Error
}

/// <summary>One game-tick snapshot. Serialized as a line of run.jsonl.</summary>
public sealed record TelemetrySample(
    long Tick,
    string Phase,
    double X, double Y, double Z,
    int FeetX, int FeetY, int FeetZ,
    double VelX, double VelY, double VelZ,
    bool OnGround,
    bool Forward, bool Backward, bool Left, bool Right, bool Jump, bool Shift, bool Sprint, bool ClickLeft, bool ClickRight,
    double Yaw, double Pitch,
    string? MovementType, int PathIndex, int PathLength,
    int? MovementSrcX, int? MovementSrcY, int? MovementSrcZ,
    int? MovementDestX, int? MovementDestY, int? MovementDestZ,
    double DistToGoal);

/// <summary>
/// Records per-tick telemetry (via GameLoop.PostTick) plus path/death events (via IGameEventListener),
/// and decides the terminal outcome of a run (success / fall / stuck / calc-fail / death).
/// </summary>
public sealed class TelemetryRecorder(IBaritone baritone, Scenario scenario, string runDir) : IGameEventListener, IDisposable
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };
    private const int RingCapacity = 80;

    private readonly StreamWriter _runWriter = new(Path.Combine(runDir, "run.jsonl")) { AutoFlush = true };
    private readonly StreamWriter _eventWriter = new(Path.Combine(runDir, "events.jsonl")) { AutoFlush = true };
    private readonly Lock _lock = new();
    private readonly TaskCompletionSource<RunOutcome> _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Queue<TelemetrySample> _ring = new();

    private bool _capturing;
    private bool _terminal;
    private double _highestGroundY = double.MinValue;
    private double _bestDist = double.MaxValue;
    private long _lastProgressTick;
    private List<string>? _intendedPath; // Baritone's first computed plan (intent), for compare-vs-actual

    public Task<RunOutcome> Completion => _tcs.Task;
    public RunOutcome? Outcome { get; private set; }
    public string? TerminalDetail { get; private set; }
    public TelemetrySample? Latest { get; private set; }
    public TelemetrySample? TerminalSample { get; private set; }
    public double HighestGroundY => _highestGroundY;

    /// <summary>Baritone's first computed path (its "intent"), formatted "[i] Type (src)->(dest)".</summary>
    public IReadOnlyList<string>? IntendedPath
    {
        get { lock (_lock) return _intendedPath; }
    }

    public IReadOnlyList<TelemetrySample> Recent()
    {
        lock (_lock) return _ring.ToArray();
    }

    // Caller must hold _lock. Reads the executor's current path movements.
    private List<string>? SnapshotCurrentPath()
    {
        try
        {
            var exec = baritone.GetPathingBehavior().GetCurrent();
            if (exec == null) return null;
            var moves = exec.GetPath().Movements();
            var list = new List<string>(moves.Count);
            for (int i = 0; i < moves.Count; i++)
            {
                var m = moves[i];
                var s = m.GetSrc();
                var d = m.GetDest();
                list.Add($"[{i}] {m.GetType().Name} ({s.X},{s.Y},{s.Z})->({d.X},{d.Y},{d.Z})");
            }
            return list;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Switch from the "arrange" phase to the "act" phase: start judging terminal conditions.</summary>
    public void BeginCapture(long currentTick)
    {
        lock (_lock)
        {
            _capturing = true;
            _lastProgressTick = currentTick;
            _highestGroundY = double.MinValue;
            _bestDist = double.MaxValue;
            WriteEvent(currentTick, "CAPTURE_BEGIN");
        }
    }

    /// <summary>Controller-driven terminal (e.g. wall-clock Timeout).</summary>
    public void ForceTerminal(RunOutcome outcome, string detail)
    {
        lock (_lock) SetTerminal(outcome, detail, Latest);
    }

    /// <summary>GameLoop.PostTick handler. Records a sample and evaluates terminal conditions.</summary>
    public void OnGameLoopTick(IMinecraftClient client)
    {
        if (!client.State.LocalPlayer.HasEntity) return;
        var sample = BuildSample(client);
        lock (_lock)
        {
            WriteSample(sample);
            Latest = sample;
            _ring.Enqueue(sample);
            while (_ring.Count > RingCapacity) _ring.Dequeue();

            if (!_capturing || _terminal) return;

            if (sample.OnGround && sample.Y > _highestGroundY) _highestGroundY = sample.Y;
            if (sample.DistToGoal < _bestDist - 0.4)
            {
                _bestDist = sample.DistToGoal;
                _lastProgressTick = sample.Tick;
            }

            if (GoalReached(sample))
            {
                SetTerminal(RunOutcome.Success, $"feet at goal ({sample.FeetX},{sample.FeetY},{sample.FeetZ})", sample);
            }
            else if (sample.Y < scenario.FallFloorY)
            {
                SetTerminal(RunOutcome.Fall, $"Y {sample.Y:F2} fell below floor {scenario.FallFloorY} (off the course)", sample);
            }
            else if (sample.Tick - _lastProgressTick > scenario.StuckTicks)
            {
                SetTerminal(RunOutcome.Stuck, $"no progress for {scenario.StuckTicks} ticks (bestDist {_bestDist:F2})", sample);
            }
        }
    }

    // GoalYTolerance defaults to 1 (a bot that ends a block low on a course has still done the course). Tests
    // that hinge on the exact final block - e.g. "stand ON TOP of the ladder", where one block low means
    // still clinging to the top rung - set it to 0, and RequireOnGround so clinging cannot count as standing.
    private bool GoalReached(TelemetrySample s) =>
        scenario.GoalDrivenTermination
        && s.FeetX == scenario.End.X && s.FeetZ == scenario.End.Z
        && Math.Abs(s.FeetY - scenario.End.Y) <= scenario.GoalYTolerance
        && (!scenario.RequireOnGround || s.OnGround);

    private TelemetrySample BuildSample(IMinecraftClient client)
    {
        var e = client.State.LocalPlayer.Entity;
        var p = e.Position;
        var v = e.Velocity;
        var yp = e.YawPitch;
        var inp = e.InputState.Current;

        string? mvType = null;
        int idx = -1, len = -1;
        int? sx = null, sy = null, sz = null;
        int? dx = null, dy = null, dz = null;
        try
        {
            var exec = baritone.GetPathingBehavior().GetCurrent();
            if (exec != null)
            {
                idx = exec.GetPosition();
                var path = exec.GetPath();
                len = path.Length();
                var moves = path.Movements();
                if (idx >= 0 && idx < moves.Count)
                {
                    var mv = moves[idx];
                    mvType = mv.GetType().Name;
                    var srcPos = mv.GetSrc();
                    var d = mv.GetDest();
                    sx = srcPos.X; sy = srcPos.Y; sz = srcPos.Z;
                    dx = d.X; dy = d.Y; dz = d.Z;
                }
            }
        }
        catch { /* pathing state mid-mutation; leave movement info null */ }

        double dist = Math.Sqrt(
            Math.Pow(p.X - scenario.End.X, 2) +
            Math.Pow(p.Y - scenario.End.Y, 2) +
            Math.Pow(p.Z - scenario.End.Z, 2));

        return new TelemetrySample(
            client.State.Level.ClientTickCounter,
            _capturing ? "act" : "arrange",
            R(p.X), R(p.Y), R(p.Z),
            (int)Math.Floor(p.X), (int)Math.Floor(p.Y), (int)Math.Floor(p.Z),
            R(v.X), R(v.Y), R(v.Z), e.IsOnGround,
            inp.Forward, inp.Backward, inp.Left, inp.Right, inp.Jump, inp.Shift, inp.Sprint, inp.ClickLeft, inp.ClickRight,
            R(yp.X), R(yp.Y),
            mvType, idx, len, sx, sy, sz, dx, dy, dz, R(dist));
    }

    private static double R(double v) => Math.Round(v, 3);

    // Caller must hold _lock.
    private void SetTerminal(RunOutcome outcome, string detail, TelemetrySample? sample)
    {
        if (_terminal) return;
        _terminal = true;
        Outcome = outcome;
        TerminalDetail = detail;
        TerminalSample = sample ?? Latest;
        WriteEvent(Latest?.Tick ?? 0, $"TERMINAL {outcome}: {detail}");
        _tcs.TrySetResult(outcome);
    }

    private void WriteSample(TelemetrySample s) => _runWriter.WriteLine(JsonSerializer.Serialize(s, Json));

    // Caller must hold _lock.
    private void WriteEvent(long tick, string message) =>
        _eventWriter.WriteLine(JsonSerializer.Serialize(new { tick, message }, Json));

    // ===== IGameEventListener: only path + death are meaningful here =====

    public void OnPathEvent(PathEvent evt)
    {
        lock (_lock)
        {
            WriteEvent(Latest?.Tick ?? 0, $"PATH {evt}");
            if (evt == PathEvent.CalcFinishedNowExecuting && _intendedPath == null)
            {
                _intendedPath = SnapshotCurrentPath();
            }
            if (!_capturing || _terminal) return;
            if (evt == PathEvent.AtGoal)
            {
                SetTerminal(RunOutcome.Success, "PathEvent.AtGoal", Latest);
            }
            else if (evt == PathEvent.CalcFailed)
            {
                SetTerminal(RunOutcome.CalcFail, "PathEvent.CalcFailed (no path found)", Latest);
            }
        }
    }

    public void OnPlayerDeath()
    {
        lock (_lock)
        {
            WriteEvent(Latest?.Tick ?? 0, "PLAYER_DEATH");
            if (_capturing) SetTerminal(RunOutcome.Death, "player died", Latest);
        }
    }

    public void OnTick(TickEvent evt) { }
    public void OnPostTick(TickEvent evt) { }
    public void OnPlayerUpdate(PlayerUpdateEvent evt) { }
    public void OnSendChatMessage(ChatEvent evt) { }
    public void OnPreTabComplete(TabCompleteEvent evt) { }
    public void OnChunkEvent(ChunkEvent evt) { }
    public void OnBlockChange(BlockChangeEvent evt) { }
    public void OnRenderPass(RenderEvent evt) { }
    public void OnWorldEvent(WorldEvent evt) { }
    public void OnSendPacket(PacketEvent evt) { }
    public void OnReceivePacket(PacketEvent evt) { }
    public void OnPlayerRotationMove(RotationMoveEvent evt) { }
    public void OnPlayerSprintState(SprintStateEvent evt) { }
    public void OnBlockInteract(BlockInteractEvent evt) { }

    public void Dispose()
    {
        lock (_lock)
        {
            _runWriter.Flush();
            _eventWriter.Flush();
            _runWriter.Dispose();
            _eventWriter.Dispose();
        }
    }
}
