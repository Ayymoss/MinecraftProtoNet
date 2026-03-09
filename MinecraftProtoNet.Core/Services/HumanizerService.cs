using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MinecraftProtoNet.Core.Abstractions;
using MinecraftProtoNet.Core.Configuration;
using MinecraftProtoNet.Core.Models.Core;
using MinecraftProtoNet.Core.State;
using MinecraftProtoNet.Core.State.Base;

namespace MinecraftProtoNet.Core.Services;

/// <summary>
/// Provides human-like timing variance to avoid anti-cheat detection.
/// Uses Random.Shared for thread-safety across game loop and async contexts.
/// </summary>
public sealed class HumanizerService : IHumanizer
{
    private readonly HumanizerConfig _config;
    private readonly ClientState _state;
    private readonly ILogger<HumanizerService> _logger;

    private int _ticksSinceLastIdleAction;
    private int _nextIdleActionAt;

    public HumanizerService(
        IOptions<HumanizerConfig> config,
        ClientState state,
        ILogger<HumanizerService> logger)
    {
        _config = config.Value;
        _state = state;
        _logger = logger;

        // Randomize first idle action timing
        _nextIdleActionAt = RandomRange(_config.IdleMinIntervalTicks, _config.IdleMaxIntervalTicks);

        logger.LogInformation("Humanizer initialized (Enabled={Enabled}, ForceOnRemote={ForceOnRemote})",
            _config.Enabled, _config.ForceOnRemote);
    }

    public bool IsEnabled
    {
        get
        {
            if (_config.Enabled) return true;
            return _config.ForceOnRemote && IsRemoteServer;
        }
    }

    public bool IsRemoteServer
    {
        get
        {
            var host = _state.ConnectedServerHost;
            if (string.IsNullOrEmpty(host)) return false;

            foreach (var local in _config.LocalNetworks)
            {
                if (host.StartsWith(local, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            return true;
        }
    }

    public int GetTickJitterMs()
    {
        if (!IsEnabled) return 0;
        return RandomRange(_config.TickJitterMinMs, _config.TickJitterMaxMs);
    }

    public (float yawOffset, float pitchOffset) GetRotationJitter()
    {
        if (!IsEnabled) return (0, 0);

        var maxDeg = _config.RotationJitterMaxDegrees;
        var yaw = (float)(Random.Shared.NextDouble() * 2 * maxDeg - maxDeg);
        var pitch = (float)(Random.Shared.NextDouble() * 2 * maxDeg - maxDeg);
        return (yaw, pitch);
    }

    public int GetGuiClickDelayMs()
    {
        if (!IsEnabled) return 0;
        return RandomRange(_config.GuiClickMinMs, _config.GuiClickMaxMs);
    }

    public int GetGuiNavigationDelayMs()
    {
        if (!IsEnabled) return 0;
        return RandomRange(_config.GuiNavigationMinMs, _config.GuiNavigationMaxMs);
    }

    public int GetChatCommandDelayMs()
    {
        if (!IsEnabled) return 0;
        return RandomRange(_config.ChatCommandMinMs, _config.ChatCommandMaxMs);
    }

    public int GetPostActionDelayMs()
    {
        if (!IsEnabled) return 0;
        return RandomRange(_config.PostActionMinMs, _config.PostActionMaxMs);
    }

    public (float yaw, float pitch)? GetIdleLookTarget(
        Vector3<double> playerPosition,
        Vector2<float> currentYawPitch,
        WorldEntityRegistry worldEntities)
    {
        var entities = worldEntities.GetAllEntities();
        if (entities.Count == 0) return null;

        var maxDist = _config.IdleLookMaxDistance;
        var maxDistSq = maxDist * maxDist;

        // Collect nearby entities
        var nearby = new List<WorldEntity>();
        foreach (var entity in entities)
        {
            var dx = entity.Position.X - playerPosition.X;
            var dy = entity.Position.Y - playerPosition.Y;
            var dz = entity.Position.Z - playerPosition.Z;
            var distSq = dx * dx + dy * dy + dz * dz;
            if (distSq > 1.0 && distSq < maxDistSq) // At least 1 block away
                nearby.Add(entity);
        }

        if (nearby.Count == 0) return null;

        // Pick a random entity
        var target = nearby[Random.Shared.Next(nearby.Count)];

        // Calculate yaw/pitch from player to target (eye height ~1.62)
        var dirX = target.Position.X - playerPosition.X;
        var dirY = (target.Position.Y + 1.0) - (playerPosition.Y + 1.62); // Target center, player eyes
        var dirZ = target.Position.Z - playerPosition.Z;

        var horizontalDist = Math.Sqrt(dirX * dirX + dirZ * dirZ);
        var yaw = (float)(Math.Atan2(-dirX, dirZ) * (180.0 / Math.PI));
        var pitch = (float)(Math.Atan2(-dirY, horizontalDist) * (180.0 / Math.PI));

        // Add a tiny bit of imprecision (humans don't look dead-center at things)
        yaw += (float)(Random.Shared.NextDouble() * 3.0 - 1.5);
        pitch += (float)(Random.Shared.NextDouble() * 2.0 - 1.0);

        return (yaw, pitch);
    }

    public bool ShouldPerformIdleAction()
    {
        if (!IsEnabled) return false;

        _ticksSinceLastIdleAction++;
        if (_ticksSinceLastIdleAction < _nextIdleActionAt) return false;

        // Reset for next idle action
        _ticksSinceLastIdleAction = 0;
        _nextIdleActionAt = RandomRange(_config.IdleMinIntervalTicks, _config.IdleMaxIntervalTicks);
        return true;
    }

    private static int RandomRange(int min, int max)
    {
        if (min >= max) return min;
        return Random.Shared.Next(min, max + 1);
    }
}
