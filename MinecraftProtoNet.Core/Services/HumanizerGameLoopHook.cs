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

    private void OnPostTick(IMinecraftClient client)
    {
        if (!_humanizer.IsEnabled) return;
        if (!_humanizer.ShouldPerformIdleAction()) return;

        var entity = client.State.LocalPlayer?.Entity;
        if (entity is null) return;

        // Skip idle behavior if the entity is actively moving (Baritone, player input, etc.)
        var input = entity.InputState.Current;
        if (input.Forward || input.Backward || input.Left || input.Right)
            return;

        var target = _humanizer.GetIdleLookTarget(
            entity.Position,
            entity.YawPitch,
            client.State.WorldEntities);

        if (target is null) return;

        // Set yaw/pitch on entity — PhysicsService will pick this up on the next tick
        // and send the rotation packet naturally (with its own rotation jitter added)
        entity.YawPitch = new Models.Core.Vector2<float>(target.Value.yaw, target.Value.pitch);

        _logger.LogDebug("Idle look: yaw={Yaw:F1} pitch={Pitch:F1}", target.Value.yaw, target.Value.pitch);
    }
}
