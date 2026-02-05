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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/event/events/TickEvent.java
 */

using MinecraftProtoNet.Baritone.Api.Event.Events.Type;

namespace MinecraftProtoNet.Baritone.Api.Event.Events;

/// <summary>
/// Tick event.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/event/events/TickEvent.java
/// </summary>
public class TickEvent
{
    private static int _overallTickCount;

    private readonly EventState _state;
    private readonly TickEventType _type;
    private readonly int _count;

    public TickEvent(EventState state, TickEventType type, int count)
    {
        _state = state;
        _type = type;
        _count = count;
    }

    public int GetCount() => _count;

    public new TickEventType GetType() => _type;

    public EventState GetState() => _state;

    public static Func<EventState, TickEventType, TickEvent> CreateNextProvider()
    {
        var count = Interlocked.Increment(ref _overallTickCount);
        return (state, type) => new TickEvent(state, type, count);
    }

    public enum TickEventType
    {
        /// <summary>
        /// When guarantees can be made about the game state and in-game variables.
        /// </summary>
        In,

        /// <summary>
        /// No guarantees can be made about the game state. This probably means we are at the main menu.
        /// </summary>
        Out
    }
}

