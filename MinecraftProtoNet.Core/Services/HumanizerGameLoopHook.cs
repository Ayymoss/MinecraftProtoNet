using Microsoft.Extensions.Logging;
using MinecraftProtoNet.Core.Abstractions;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Core.Abstractions;

namespace MinecraftProtoNet.Core.Services;

/// <summary>
/// Hooks into the game loop PostTick to perform idle behavior (looking at nearby entities)
/// when the bot has no active task. Skips idle actions when Baritone or other systems are active.
/// </summary>
public sealed class HumanizerGameLoopHook
{
    private readonly IHumanizer _humanizer;
    private readonly ILogger<HumanizerGameLoopHook> _logger;

    public HumanizerGameLoopHook(
        IGameLoop gameLoop,
        IHumanizer humanizer,
        ILogger<HumanizerGameLoopHook> logger)
    {
        _humanizer = humanizer;
        _logger = logger;

        gameLoop.PostTick += OnPostTick;
        logger.LogInformation("HumanizerGameLoopHook: Hooked idle behavior to game loop");
    }

    /// <summary>
    /// Where the current idle glance is heading, and how far through it we are. A real glance is spread over
    /// many ticks; see the note in OnPostTick.
    /// </summary>
    private float _targetYaw;
    private float _targetPitch;
    private int _slewTicksRemaining;

    private void OnPostTick(IMinecraftClient client)
    {
        if (!_humanizer.IsEnabled) return;

        var entity = client.State.LocalPlayer?.Entity;
        if (entity is null) return;

        // Skip idle behavior if the entity is actively moving (Baritone, player input, etc.)
        var input = entity.InputState.Current;
        if (input.Forward || input.Backward || input.Left || input.Right)
        {
            _slewTicksRemaining = 0;
            return;
        }

        // A glance already in progress takes precedence over starting a new one.
        if (_slewTicksRemaining > 0)
        {
            StepTowardsTarget(entity);
            return;
        }

        if (!_humanizer.ShouldPerformIdleAction()) return;

        var target = _humanizer.GetIdleLookTarget(
            entity.Position,
            entity.YawPitch,
            client.State.WorldEntities);

        if (target is null) return;

        // Turn over MANY ticks rather than assigning the destination outright.
        //
        // The old code set the full target in one tick, so a ±10° glance produced exactly one Rot/PosRot
        // packet carrying a 10° delta and then silence. A real client turns in MouseHandler.turnPlayer per
        // FRAME, and sendPosition fires on every tick where deltaYRot != 0
        // (Reference: minecraft-26.2-REFERENCE-ONLY/net/minecraft/client/player/LocalPlayer.java:263), so the
        // same head movement emits ~10-20 small-delta packets. That ratio is the right order of magnitude for
        // the measured rotation-packet rate of 0.09x vanilla, and the per-packet delta distribution is the
        // real giveaway: no mouse produces a single 10° step followed by nothing.
        _targetYaw = target.Value.yaw;
        _targetPitch = target.Value.pitch;
        _slewTicksRemaining = _humanizer.GetIdleLookSlewTicks();

        _logger.LogDebug("Idle look: yaw={Yaw:F1} pitch={Pitch:F1} over {Ticks} ticks",
            _targetYaw, _targetPitch, _slewTicksRemaining);

        StepTowardsTarget(entity);
    }

    /// <summary>
    /// Advances one tick's worth of the current glance. Ease-out (a constant fraction of the REMAINING
    /// error each tick) rather than a linear ramp: a real turn decelerates into its target rather than
    /// stopping dead, and a constant per-tick delta is as artificial as a single step.
    /// </summary>
    private void StepTowardsTarget(State.Entity entity)
    {
        var current = entity.YawPitch;

        // Shortest way round, so a turn across the ±180 seam does not go the long way.
        // Reference: minecraft-26.2-REFERENCE-ONLY/net/minecraft/util/Mth.java wrapDegrees
        var yawError = WrapDegrees(_targetYaw - current.X);
        var pitchError = _targetPitch - current.Y;

        _slewTicksRemaining--;

        float newYaw, newPitch;
        if (_slewTicksRemaining <= 0)
        {
            newYaw = _targetYaw;
            newPitch = _targetPitch;
        }
        else
        {
            const float stepFraction = 0.35f;
            newYaw = current.X + yawError * stepFraction;
            newPitch = current.Y + pitchError * stepFraction;
        }

        // PhysicsService picks this up on the next tick and sends the rotation packet naturally.
        entity.YawPitch = new Models.Core.Vector2<float>(newYaw, newPitch);
    }

    private static float WrapDegrees(float degrees)
    {
        var wrapped = degrees % 360.0f;
        if (wrapped >= 180.0f) wrapped -= 360.0f;
        if (wrapped < -180.0f) wrapped += 360.0f;
        return wrapped;
    }
}
