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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/event/listener/IGameEventListener.java
 */

using MinecraftProtoNet.Baritone.Api.Event.Events;

namespace MinecraftProtoNet.Baritone.Api.Event.Listener;

/// <summary>
/// Interface for game event listener.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/event/listener/IGameEventListener.java
/// </summary>
public interface IGameEventListener
{
    /// <summary>
    /// Run once per game tick before screen input is handled.
    /// </summary>
    void OnTick(TickEvent evt);

    /// <summary>
    /// Run once per game tick after the tick is completed.
    /// </summary>
    void OnPostTick(TickEvent evt);

    /// <summary>
    /// Run once per game tick from before and after the player rotation is sent to the server.
    /// </summary>
    void OnPlayerUpdate(PlayerUpdateEvent evt);

    /// <summary>
    /// Runs whenever the client player sends a message to the server.
    /// </summary>
    void OnSendChatMessage(ChatEvent evt);

    /// <summary>
    /// Runs whenever the client player tries to tab complete in chat.
    /// </summary>
    void OnPreTabComplete(TabCompleteEvent evt);

    /// <summary>
    /// Runs before and after whenever a chunk is either loaded, unloaded, or populated.
    /// </summary>
    void OnChunkEvent(ChunkEvent evt);

    /// <summary>
    /// Runs after a single or multi block change packet is received and processed.
    /// </summary>
    void OnBlockChange(BlockChangeEvent evt);

    /// <summary>
    /// Runs once per world render pass.
    /// </summary>
    void OnRenderPass(RenderEvent evt);

    /// <summary>
    /// Runs before and after whenever a new world is loaded.
    /// </summary>
    void OnWorldEvent(WorldEvent evt);

    /// <summary>
    /// Runs before an outbound packet is sent.
    /// </summary>
    void OnSendPacket(PacketEvent evt);

    /// <summary>
    /// Runs before an inbound packet is processed.
    /// </summary>
    void OnReceivePacket(PacketEvent evt);

    /// <summary>
    /// Run once per game tick from before and after the player's moveRelative method is called and before and after the player jumps.
    /// </summary>
    void OnPlayerRotationMove(RotationMoveEvent evt);

    /// <summary>
    /// Called whenever the sprint keybind state is checked.
    /// </summary>
    void OnPlayerSprintState(SprintStateEvent evt);

    /// <summary>
    /// Called when the local player interacts with a block, whether it is breaking or opening/placing.
    /// </summary>
    void OnBlockInteract(BlockInteractEvent evt);

    /// <summary>
    /// Called when the local player dies.
    /// </summary>
    void OnPlayerDeath();

    /// <summary>
    /// When the pathfinder's state changes.
    /// </summary>
    void OnPathEvent(PathEvent evt);
}

