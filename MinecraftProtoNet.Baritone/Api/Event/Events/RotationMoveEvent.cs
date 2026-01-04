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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/event/events/RotationMoveEvent.java
 */

using MinecraftProtoNet.Baritone.Api.Utils;

namespace MinecraftProtoNet.Baritone.Api.Event.Events;

/// <summary>
/// Rotation move event.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/event/events/RotationMoveEvent.java
/// </summary>
public class RotationMoveEvent
{
    private readonly Type _type;
    private readonly Rotation _original;
    private float _yaw;
    private float _pitch;

    public RotationMoveEvent(Type type, float yaw, float pitch)
    {
        _type = type;
        _original = new Rotation(yaw, pitch);
        _yaw = yaw;
        _pitch = pitch;
    }

    public Rotation GetOriginal() => _original;
    public new Type GetType() => _type;

    public void SetYaw(float yaw) => _yaw = yaw;
    public float GetYaw() => _yaw;

    public void SetPitch(float pitch) => _pitch = pitch;
    public float GetPitch() => _pitch;

    public enum Type
    {
        MotionUpdate,
        Jump
    }
}

