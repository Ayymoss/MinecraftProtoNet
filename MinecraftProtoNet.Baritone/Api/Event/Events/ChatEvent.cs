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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/event/events/ChatEvent.java
 */

namespace MinecraftProtoNet.Baritone.Api.Event.Events;

/// <summary>
/// Chat event.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/event/events/ChatEvent.java
/// </summary>
public class ChatEvent : Cancellable
{
    private readonly string _message;

    public ChatEvent(string message)
    {
        _message = message;
    }

    public string GetMessage() => _message;
}

