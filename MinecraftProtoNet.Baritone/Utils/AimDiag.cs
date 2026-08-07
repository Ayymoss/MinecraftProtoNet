namespace MinecraftProtoNet.Baritone.Utils;

/// <summary>
/// Per-tick aim tracing: records every write to the player's rotation, tagged with the call site that
/// caused it. Exists to answer "why is the view floaty" — a rotation that is applied more than once per
/// tick, or a target that is recomputed relative to the live rotation, compounds into a drift the player
/// sees as wandering. Append-only in memory (no per-line I/O) so it does not perturb game-loop timing;
/// the harness flushes <see cref="MovementDiag.Lines"/> after the run. Gated by <see cref="MovementDiag.Enabled"/>.
/// </summary>
public static class AimDiag
{
    private static long _tick = -1;
    private static int _writesThisTick;

    /// <summary>Writes applied to the player rotation during the tick that just ended.</summary>
    public static int LastTickWriteCount { get; private set; }

    /// <summary>Called once per game tick so per-tick write counts can be attributed.</summary>
    public static void BeginTick(long tick)
    {
        if (!MovementDiag.Enabled) return;
        if (_tick >= 0 && _writesThisTick > 1)
        {
            MovementDiag.Log($"AIM-MULTIWRITE tick={_tick} writes={_writesThisTick}");
        }
        LastTickWriteCount = _writesThisTick;
        _tick = tick;
        _writesThisTick = 0;
    }

    /// <summary>
    /// Records one application of an aim target to the player rotation.
    /// </summary>
    /// <param name="site">Call site tag: eager / pre / post / rotationMove.</param>
    /// <param name="mode">The resolved target mode (Client/Server/None).</param>
    /// <param name="targetYaw">Yaw the movement asked for.</param>
    /// <param name="targetPitch">Pitch the movement asked for.</param>
    /// <param name="prevYaw">Player yaw before this write.</param>
    /// <param name="prevPitch">Player pitch before this write.</param>
    /// <param name="appliedYaw">Player yaw after this write.</param>
    /// <param name="appliedPitch">Player pitch after this write.</param>
    public static void Write(string site, string mode,
        float targetYaw, float targetPitch,
        float prevYaw, float prevPitch,
        float appliedYaw, float appliedPitch)
    {
        if (!MovementDiag.Enabled) return;
        _writesThisTick++;
        float dYaw = ((appliedYaw - prevYaw + 180f) % 360f + 360f) % 360f - 180f;
        float dPitch = appliedPitch - prevPitch;
        // nudge = the target asked for the pitch we already had, so AimProcessor.NudgeToLevel moved it
        // 1 degree toward level instead (quantized to the mouse grid = 1.05).
        bool nudged = Math.Abs(targetPitch - prevPitch) < 1e-4f && Math.Abs(dPitch) > 1e-4f;
        MovementDiag.Log($"AIM tick={_tick} n={_writesThisTick} site={site} mode={mode} " +
            $"tgt=y{targetYaw:F2}/p{targetPitch:F2} prev=y{prevYaw:F2}/p{prevPitch:F2} " +
            $"applied=y{appliedYaw:F2}/p{appliedPitch:F2} d=y{dYaw:+0.00;-0.00}/p{dPitch:+0.00;-0.00}" +
            (nudged ? " NUDGE-TO-LEVEL" : ""));
    }
}
