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
        // Idle look: small random offsets from current yaw/pitch to simulate natural head drift.
        // Instead of snapping to a random entity (which causes jarring instant rotations),
        // we jitter relative to the current looking direction, like a real player glancing around.
        var yawOffset = (float)(Random.Shared.NextDouble() * 20.0 - 10.0);   // ±10° yaw
        var pitchOffset = (float)(Random.Shared.NextDouble() * 8.0 - 4.0);   // ±4° pitch

        var yaw = currentYawPitch.X + yawOffset;
        var pitch = Math.Clamp(currentYawPitch.Y + pitchOffset, -90f, 90f);   // Keep pitch in valid range

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
