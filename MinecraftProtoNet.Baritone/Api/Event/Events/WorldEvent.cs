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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/event/events/WorldEvent.java
 */

using MinecraftProtoNet.Baritone.Api.Event.Events.Type;

namespace MinecraftProtoNet.Baritone.Api.Event.Events;

/// <summary>
/// World event.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/event/events/WorldEvent.java
/// </summary>
public class WorldEvent
{
    private readonly object? _world; // Will be typed as Level when integrated
    private readonly EventState _state;

    public WorldEvent(object? world, EventState state)
    {
        _world = world;
        _state = state;
    }

    public object? GetWorld() => _world;
    public EventState GetState() => _state;
}

