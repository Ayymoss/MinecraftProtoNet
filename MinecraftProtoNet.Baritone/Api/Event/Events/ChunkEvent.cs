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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/event/events/ChunkEvent.java
 */

using MinecraftProtoNet.Baritone.Api.Event.Events.Type;

namespace MinecraftProtoNet.Baritone.Api.Event.Events;

/// <summary>
/// Chunk event.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/event/events/ChunkEvent.java
/// </summary>
public class ChunkEvent
{
    private readonly EventState _state;
    private readonly Type _type;
    private readonly int _x;
    private readonly int _z;

    public ChunkEvent(EventState state, Type type, int x, int z)
    {
        _state = state;
        _type = type;
        _x = x;
        _z = z;
    }

    public EventState GetState() => _state;
    public new Type GetType() => _type;
    public int GetX() => _x;
    public int GetZ() => _z;

    public bool IsPostPopulate() => _state == EventState.Post && _type.IsPopulate();

    public enum Type
    {
        Load,
        Unload,
        PopulateFull,
        PopulatePartial
    }
}

/// <summary>
/// Extension methods for ChunkEvent.Type.
/// </summary>
public static class ChunkEventTypeExtensions
{
    public static bool IsPopulate(this ChunkEvent.Type type)
    {
        return type == ChunkEvent.Type.PopulateFull || type == ChunkEvent.Type.PopulatePartial;
    }
}

