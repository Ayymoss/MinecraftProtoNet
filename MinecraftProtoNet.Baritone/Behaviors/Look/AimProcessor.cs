/*
 * This file is part of Baritone.
 *
 * Baritone is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Lesser General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * Baritone is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with Baritone.  If not, see <https://www.gnu.org/licenses/>.
 *
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/behavior/LookBehavior.java (AimProcessor inner class)
 */

using MinecraftProtoNet.Baritone.Api.Behavior.Look;
using MinecraftProtoNet.Baritone.Api.Utils;

namespace MinecraftProtoNet.Baritone.Behaviors.Look;

/// <summary>
/// Aim processor implementation.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/behavior/LookBehavior.java (AimProcessor)
/// </summary>
public class AimProcessor : ITickableAimProcessor
{
    private readonly IPlayerContext _ctx;
    private readonly Random _rand;
    private double _randomYawOffset;
    private double _randomPitchOffset;
    private Rotation _currentRotation;

    public AimProcessor(IPlayerContext ctx)
    {
        _ctx = ctx;
        _rand = new Random();
        _currentRotation = ctx.PlayerRotations() ?? new Rotation(0, 0);
    }

    public Rotation PeekRotation(Rotation desired)
    {
        var prev = GetPrevRotation() ?? new Rotation(0, 0);

        float desiredYaw = desired.GetYaw();
        float desiredPitch = desired.GetPitch();

        // If pitch hasn't changed, nudge it to a normal level
        if (Math.Abs(desiredPitch - prev.GetPitch()) < 0.001f)
        {
            desiredPitch = NudgeToLevel(desiredPitch);
        }

        desiredYaw += (float)_randomYawOffset;
        desiredPitch += (float)_randomPitchOffset;

        return new Rotation(
            CalculateMouseMove(prev.GetYaw(), desiredYaw),
            CalculateMouseMove(prev.GetPitch(), desiredPitch)
        ).Clamp();
    }

    public ITickableAimProcessor Fork()
    {
        var fork = new AimProcessor(_ctx);
        fork._randomYawOffset = _randomYawOffset;
        fork._randomPitchOffset = _randomPitchOffset;
        return fork;
    }

    public void Tick()
    {
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/behavior/LookBehavior.java:77-80
        var settings = Api.BaritoneAPI.GetSettings();
        // randomLooking and randomLooking113 settings
        // TODO: Implement random looking when settings are available
        _randomYawOffset = 0;
        _randomPitchOffset = 0;
        _currentRotation = _ctx.PlayerRotations() ?? new Rotation(0, 0);
    }

    public Rotation GetCurrentRotation() => _currentRotation;

    // Properties for convenience
    public float Yaw => _currentRotation.GetYaw();
    public float Pitch => _currentRotation.GetPitch();

    public Rotation NextRotation(Rotation rotation)
    {
        var result = PeekRotation(rotation);
        Tick();
        return result;
    }

    public void Advance(int ticks)
    {
        for (int i = 0; i < ticks; i++)
        {
            Tick();
        }
    }

    private Rotation GetPrevRotation()
    {
        return _currentRotation ?? new Rotation(0, 0);
    }

    private static float NudgeToLevel(float pitch)
    {
        // Nudge pitch towards a regular level (between -20 and 10)
        if (pitch < -20)
        {
            return pitch + 1;
        }
        else if (pitch > 10)
        {
            return pitch - 1;
        }
        return pitch;
    }

    private float CalculateMouseMove(float current, float target)
    {
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/behavior/LookBehavior.java:292-296
        // Simplified implementation - the full Java version uses Minecraft sensitivity which requires access to IMinecraftClient
        // For now, return target directly (will be properly implemented when IMinecraftClient options are available)
        return target;
    }

    private static float NormalizeAngle(float angle)
    {
        while (angle > 180)
        {
            angle -= 360;
        }
        while (angle < -180)
        {
            angle += 360;
        }
        return angle;
    }
}

