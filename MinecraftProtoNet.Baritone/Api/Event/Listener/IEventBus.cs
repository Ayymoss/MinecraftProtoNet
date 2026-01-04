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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/event/listener/IEventBus.java
 */

namespace MinecraftProtoNet.Baritone.Api.Event.Listener;

/// <summary>
/// A type of IGameEventListener that can have additional listeners registered.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/event/listener/IEventBus.java
/// </summary>
public interface IEventBus : IGameEventListener
{
    /// <summary>
    /// Registers the specified listener to this event bus.
    /// </summary>
    void RegisterEventListener(IGameEventListener listener);

    /// <summary>
    /// Logs a direct message.
    /// </summary>
    void LogDirect(string message);

    /// <summary>
    /// Logs a notification message.
    /// </summary>
    void LogNotification(string message, bool logToChat);
}

