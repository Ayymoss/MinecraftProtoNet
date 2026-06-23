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
    private readonly ForkableRandom _rand;
    private double _randomYawOffset;
    private double _randomPitchOffset;
    private Rotation _currentRotation;
    private readonly bool _isFork;
    private Rotation _chainedPrev;

    public AimProcessor(IPlayerContext ctx)
    {
        _ctx = ctx;
        _rand = new ForkableRandom();
        _currentRotation = ctx.PlayerRotations() ?? new Rotation(0, 0);
        _chainedPrev = _currentRotation;
    }

    // Fork ctor (Reference: LookBehavior.java:203-208,261-276): shares the source's forked RNG + current
    // random offsets, and chains getPrevRotation from the source instead of reading the live rotation.
    private AimProcessor(IPlayerContext ctx, ForkableRandom rand, double yawOffset, double pitchOffset, Rotation chainedPrev)
    {
        _ctx = ctx;
        _rand = rand;
        _randomYawOffset = yawOffset;
        _randomPitchOffset = pitchOffset;
        _isFork = true;
        _chainedPrev = chainedPrev;
        _currentRotation = chainedPrev;
    }

    public Rotation PeekRotation(Rotation desired)
    {
        var prev = GetPrevRotation() ?? new Rotation(0, 0);

        float desiredYaw = desired.GetYaw();
        float desiredPitch = desired.GetPitch();

        // Reference: LookBehavior.java:219 - exact equality. The target only "doesn't care" about pitch
        // when it passes playerRotations().getPitch() verbatim; RotationUtils.Reachable adds a +0.0001
        // sentinel to a real desired pitch so it will NOT equal prev here. A tolerance (e.g. < 0.001)
        // would swallow that sentinel and wrongly nudge the placement pitch off the block face.
        if (desiredPitch == prev.GetPitch())
        {
            desiredPitch = NudgeToLevel(desiredPitch);
        }

        desiredYaw += (float)_randomYawOffset;
        desiredPitch += (float)_randomPitchOffset;

        // CalculateMouseMove now quantizes the delta to mouse-pixel steps (AIM1), matching Java. The
        // trailing NormalizeAndClamp is a C# safety net (Java leaves it unnormalized here): keeps yaw in
        // [-180,180] and pitch in [-90,90] regardless of accumulated live yaw.
        return new Rotation(
            CalculateMouseMove(prev.GetYaw(), desiredYaw),
            CalculateMouseMove(prev.GetPitch(), desiredPitch)
        ).NormalizeAndClamp();
    }

    public ITickableAimProcessor Fork()
    {
        // Reference: LookBehavior.java:261-276 - a fork shares a forked RNG + the current offsets and
        // chains its own prev rotation (for multi-step predictive lookahead).
        return new AimProcessor(_ctx, _rand.Fork(), _randomYawOffset, _randomPitchOffset, GetPrevRotation());
    }

    public void Tick()
    {
        // Reference: LookBehavior.java:233-244 - randomLooking + randomLooking113 aim jitter
        var settings = Api.BaritoneAPI.GetSettings();
        _randomYawOffset = (_rand.NextDouble() - 0.5) * settings.RandomLooking.Value;
        _randomPitchOffset = (_rand.NextDouble() - 0.5) * settings.RandomLooking.Value;

        double random = _rand.NextDouble() - 0.5;
        if (Math.Abs(random) < 0.1)
        {
            random *= 4;
        }
        _randomYawOffset += random * settings.RandomLooking113.Value;

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
        if (_isFork)
        {
            _chainedPrev = result; // Reference: LookBehavior.java:267-269 - fork chains prev across calls
        }
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
        // Reference: LookBehavior.java:187/272 - base processor uses the player's LIVE rotation (a stale
        // cache would diverge from what's sent and break the AIM2 sentinel); a fork chains its own prev.
        if (_isFork)
        {
            return _chainedPrev;
        }
        return _ctx.PlayerRotations() ?? new Rotation(0, 0);
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

    // Reference: LookBehavior.java:292-296 - quantize the angle change to whole mouse-pixel steps so the sent
    // rotation lands on the same discrete grid a real mouse produces.
    private float CalculateMouseMove(float current, float target)
    {
        float delta = target - current;
        double deltaPx = AngleToMouse(delta);
        return current + MouseToAngle(deltaPx);
    }

    // Reference: LookBehavior.java:298-301
    private double AngleToMouse(float angleDelta)
    {
        float minAngleChange = MouseToAngle(1);
        return Math.Round(angleDelta / minAngleChange);
    }

    // Reference: LookBehavior.java:303-307 - sensitivity substituted by the MouseSensitivity setting (no client options).
    private float MouseToAngle(double mouseDelta)
    {
        double f = Api.BaritoneAPI.GetSettings().MouseSensitivity.Value * 0.6 + 0.2;
        return (float)(mouseDelta * f * f * f * 8.0) * 0.15f;
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

