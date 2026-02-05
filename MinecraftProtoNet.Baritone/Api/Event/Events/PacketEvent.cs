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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/event/events/PacketEvent.java
 */

using MinecraftProtoNet.Baritone.Api.Event.Events.Type;

namespace MinecraftProtoNet.Baritone.Api.Event.Events;

/// <summary>
/// Packet event.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/event/events/PacketEvent.java
/// </summary>
public class PacketEvent
{
    private readonly object _networkManager; // Will be typed when integrated
    private readonly EventState _state;
    private readonly object _packet; // Will be typed when integrated

    public PacketEvent(object networkManager, EventState state, object packet)
    {
        _networkManager = networkManager;
        _state = state;
        _packet = packet;
    }

    public object GetNetworkManager() => _networkManager;
    public EventState GetState() => _state;
    public object GetPacket() => _packet;

    public T Cast<T>() => (T)_packet;
}

