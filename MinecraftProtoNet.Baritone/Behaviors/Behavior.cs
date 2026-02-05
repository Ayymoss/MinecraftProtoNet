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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/behavior/Behavior.java
 */

using MinecraftProtoNet.Baritone.Api;
using MinecraftProtoNet.Baritone.Api.Behavior;
using MinecraftProtoNet.Baritone.Api.Event.Listener;
using MinecraftProtoNet.Baritone.Api.Utils;

namespace MinecraftProtoNet.Baritone.Behaviors;

/// <summary>
/// A type of game event listener that is given Baritone instance context.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/behavior/Behavior.java
/// </summary>
public class Behavior : IBehavior, IGameEventListener
{
    public readonly IBaritone Baritone;
    public readonly IPlayerContext Ctx;

    protected Behavior(IBaritone baritone)
    {
        Baritone = baritone;
        Ctx = baritone.GetPlayerContext();
    }

    public IBaritone GetBaritone() => Baritone;

    // IGameEventListener implementation - default empty implementations
    // Subclasses override the methods they need
    public virtual void OnTick(Api.Event.Events.TickEvent evt) { }
    public virtual void OnPostTick(Api.Event.Events.TickEvent evt) { }
    public virtual void OnPlayerUpdate(Api.Event.Events.PlayerUpdateEvent evt) { }
    public virtual void OnSendChatMessage(Api.Event.Events.ChatEvent evt) { }
    public virtual void OnPreTabComplete(Api.Event.Events.TabCompleteEvent evt) { }
    public virtual void OnChunkEvent(Api.Event.Events.ChunkEvent evt) { }
    public virtual void OnBlockChange(Api.Event.Events.BlockChangeEvent evt) { }
    public virtual void OnRenderPass(Api.Event.Events.RenderEvent evt) { }
    public virtual void OnWorldEvent(Api.Event.Events.WorldEvent evt) { }
    public virtual void OnSendPacket(Api.Event.Events.PacketEvent evt) { }
    public virtual void OnReceivePacket(Api.Event.Events.PacketEvent evt) { }
    public virtual void OnPlayerRotationMove(Api.Event.Events.RotationMoveEvent evt) { }
    public virtual void OnPlayerSprintState(Api.Event.Events.SprintStateEvent evt) { }
    public virtual void OnBlockInteract(Api.Event.Events.BlockInteractEvent evt) { }
    public virtual void OnPlayerDeath() { }
    public virtual void OnPathEvent(Api.Event.Events.PathEvent evt) { }
}

