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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/utils/Rotation.java
 */

namespace MinecraftProtoNet.Baritone.Api.Utils;

/// <summary>
/// Represents a rotation (yaw and pitch).
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/utils/Rotation.java
/// </summary>
public class Rotation
{
    /// <summary>
    /// The yaw angle of this rotation
    /// </summary>
    private readonly float _yaw;

    /// <summary>
    /// The pitch angle of this rotation
    /// </summary>
    private readonly float _pitch;

    public Rotation(float yaw, float pitch)
    {
        _yaw = yaw;
        _pitch = pitch;
        if (float.IsInfinity(yaw) || float.IsNaN(yaw) || float.IsInfinity(pitch) || float.IsNaN(pitch))
        {
            throw new InvalidOperationException($"{yaw} {pitch}");
        }
    }

    /// <summary>
    /// Gets the yaw of this rotation
    /// </summary>
    public float GetYaw() => _yaw;

    /// <summary>
    /// Gets the pitch of this rotation
    /// </summary>
    public float GetPitch() => _pitch;

    /// <summary>
    /// Adds the yaw/pitch of the specified rotation to this rotation's yaw/pitch.
    /// </summary>
    public Rotation Add(Rotation other)
    {
        return new Rotation(_yaw + other._yaw, _pitch + other._pitch);
    }

    /// <summary>
    /// Subtracts the yaw/pitch of the specified rotation from this rotation's yaw/pitch.
    /// </summary>
    public Rotation Subtract(Rotation other)
    {
        return new Rotation(_yaw - other._yaw, _pitch - other._pitch);
    }

    /// <summary>
    /// Returns a copy of this rotation with the pitch clamped.
    /// </summary>
    public Rotation Clamp()
    {
        return new Rotation(_yaw, ClampPitch(_pitch));
    }

    /// <summary>
    /// Returns a copy of this rotation with the yaw normalized.
    /// </summary>
    public Rotation Normalize()
    {
        return new Rotation(NormalizeYaw(_yaw), _pitch);
    }

    /// <summary>
    /// Returns a copy of this rotation with the pitch clamped and the yaw normalized.
    /// </summary>
    public Rotation NormalizeAndClamp()
    {
        return new Rotation(NormalizeYaw(_yaw), ClampPitch(_pitch));
    }

    /// <summary>
    /// Returns a copy of this rotation with a different pitch.
    /// </summary>
    public Rotation WithPitch(float pitch)
    {
        return new Rotation(_yaw, pitch);
    }

    /// <summary>
    /// Checks if this rotation is really close to another rotation.
    /// </summary>
    public bool IsReallyCloseTo(Rotation other)
    {
        return YawIsReallyClose(other) && Math.Abs(_pitch - other._pitch) < 0.01f;
    }

    /// <summary>
    /// Checks if the yaw is really close to another rotation's yaw.
    /// </summary>
    public bool YawIsReallyClose(Rotation other)
    {
        float yawDiff = Math.Abs(NormalizeYaw(_yaw) - NormalizeYaw(other._yaw));
        return yawDiff < 0.01f || yawDiff > 359.99f;
    }

    /// <summary>
    /// Clamps the specified pitch value between -90 and 90.
    /// </summary>
    public static float ClampPitch(float pitch)
    {
        return Math.Max(-90f, Math.Min(90f, pitch));
    }

    /// <summary>
    /// Normalizes the specified yaw value between -180 and 180.
    /// </summary>
    public static float NormalizeYaw(float yaw)
    {
        float newYaw = yaw % 360f;
        if (newYaw < -180f)
        {
            newYaw += 360f;
        }
        if (newYaw > 180f)
        {
            newYaw -= 360f;
        }
        return newYaw;
    }

    public override string ToString()
    {
        return $"Yaw: {_yaw}, Pitch: {_pitch}";
    }
}

