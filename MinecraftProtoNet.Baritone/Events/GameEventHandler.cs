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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/event/GameEventHandler.java
 */

using Microsoft.Extensions.Logging;
using MinecraftProtoNet.Baritone.Api;
using MinecraftProtoNet.Baritone.Api.Event.Events;
using MinecraftProtoNet.Baritone.Api.Event.Events.Type;
using MinecraftProtoNet.Baritone.Api.Event.Listener;
using MinecraftProtoNet.Baritone.Utils;
using MinecraftProtoNet.Core.Core;

namespace MinecraftProtoNet.Baritone.Events;

/// <summary>
/// Game event handler implementation.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/event/GameEventHandler.java
/// </summary>
public class GameEventHandler : IEventBus
{
    private readonly IBaritone _baritone;
    private readonly List<IGameEventListener> _listeners = new();

    public GameEventHandler(IBaritone baritone)
    {
        _baritone = baritone;
    }

    public void RegisterEventListener(IGameEventListener listener)
    {
        _listeners.Add(listener);
    }

    public void OnTick(TickEvent evt)
    {
        if (evt.GetType() == TickEvent.TickEventType.In)
        {
            try
            {
                var baritoneImpl = (Core.Baritone)_baritone;
                baritoneImpl.Bsi = new BlockStateInterface(_baritone.GetPlayerContext());
            }
            catch (Exception)
            {
                var baritoneImpl = (Core.Baritone)_baritone;
                baritoneImpl.Bsi = null;
            }
        }
        else
        {
            var baritoneImpl = (Core.Baritone)_baritone;
            baritoneImpl.Bsi = null;
        }

        foreach (var listener in _listeners)
        {
            listener.OnTick(evt);
        }
    }

    public void OnPostTick(TickEvent evt)
    {
        foreach (var listener in _listeners)
        {
            listener.OnPostTick(evt);
        }
    }

    public void OnPlayerUpdate(PlayerUpdateEvent evt)
    {
        foreach (var listener in _listeners)
        {
            try
            {
                listener.OnPlayerUpdate(evt);
            }
            catch (Exception ex)
            {
                LogDirect($"Error in listener.OnPlayerUpdate: {ex.Message}");
            }
        }
    }

    public void OnSendChatMessage(ChatEvent evt)
    {
        foreach (var listener in _listeners)
        {
            listener.OnSendChatMessage(evt);
        }
    }

    public void OnPreTabComplete(TabCompleteEvent evt)
    {
        foreach (var listener in _listeners)
        {
            listener.OnPreTabComplete(evt);
        }
    }

    public void OnChunkEvent(ChunkEvent evt)
    {
        var state = evt.GetState();
        var type = evt.GetType();

        // Note: World access will be implemented when integrated with Core
        // For now, we'll queue chunks for packing when appropriate
        if (evt.IsPostPopulate() || (state == EventState.Pre && type == ChunkEvent.Type.Unload))
        {
            _baritone.GetWorldProvider().IfWorldLoaded(worldData =>
            {
                // Queue chunk for packing - will be implemented when CachedWorld is ported
                // worldData.GetCachedWorld().QueueForPacking(chunk);
            });
        }

        foreach (var listener in _listeners)
        {
            listener.OnChunkEvent(evt);
        }
    }

    public void OnBlockChange(BlockChangeEvent evt)
    {
        if (MinecraftProtoNet.Baritone.Core.Baritone.Settings().RepackOnAnyBlockChange.Value)
        {
            // Check if any of the changed blocks are ones we track
            // This will be fully implemented when CachedChunk is ported
            // For now, we'll queue the chunk for repacking
            _baritone.GetWorldProvider().IfWorldLoaded(worldData =>
            {
                var chunkPos = evt.GetChunkPos();
                // worldData.GetCachedWorld().QueueForPacking(world.GetChunk(chunkPos.ChunkX, chunkPos.ChunkZ));
            });
        }

        foreach (var listener in _listeners)
        {
            listener.OnBlockChange(evt);
        }
    }

    public void OnRenderPass(RenderEvent evt)
    {
        foreach (var listener in _listeners)
        {
            listener.OnRenderPass(evt);
        }
    }

    public void OnWorldEvent(WorldEvent evt)
    {
        var cache = _baritone.GetWorldProvider();

        if (evt.GetState() == EventState.Post)
        {
            // Close current world and init new one
            // This will be implemented when WorldProvider is fully ported
            // cache.CloseWorld();
            // if (evt.GetWorld() != null)
            // {
            //     cache.InitWorld(evt.GetWorld());
            // }
        }

        foreach (var listener in _listeners)
        {
            listener.OnWorldEvent(evt);
        }
    }

    public void OnSendPacket(PacketEvent evt)
    {
        foreach (var listener in _listeners)
        {
            listener.OnSendPacket(evt);
        }
    }

    public void OnReceivePacket(PacketEvent evt)
    {
        foreach (var listener in _listeners)
        {
            listener.OnReceivePacket(evt);
        }
    }

    public void OnPlayerRotationMove(RotationMoveEvent evt)
    {
        foreach (var listener in _listeners)
        {
            listener.OnPlayerRotationMove(evt);
        }
    }

    public void OnPlayerSprintState(SprintStateEvent evt)
    {
        foreach (var listener in _listeners)
        {
            listener.OnPlayerSprintState(evt);
        }
    }

    public void OnBlockInteract(BlockInteractEvent evt)
    {
        foreach (var listener in _listeners)
        {
            listener.OnBlockInteract(evt);
        }
    }

    public void OnPlayerDeath()
    {
        foreach (var listener in _listeners)
        {
            listener.OnPlayerDeath();
        }
    }

    public void OnPathEvent(PathEvent evt)
    {
        foreach (var listener in _listeners)
        {
            listener.OnPathEvent(evt);
        }
    }

    public void LogDirect(string message)
    {
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/utils/Helper.java:235-237
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/utils/GameEventHandler.java:235
        // Use the logger setting configured in Settings, which should be hooked to ILogger
        try
        {
            BaritoneAPI.GetSettings().Logger.Value(message);
        }
        catch (Exception ex)
        {
            // Fallback to console if logger setting fails
            Console.WriteLine($"[Baritone] {message}");
            // Also try to log the error using the logging infrastructure
            try
            {
                var logger = LoggingConfiguration.CreateLogger<GameEventHandler>();
                logger.LogError(ex, "Failed to log Baritone message via Settings.Logger: {Message}", message);
            }
            catch
            {
                // If logging infrastructure is not available, just use console
            }
        }
    }

    public void LogNotification(string message, bool logToChat)
    {
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/utils/GameEventHandler.java:241-247
        // Log notification, optionally to chat
        Console.WriteLine($"[Baritone] {message}");
        if (logToChat)
        {
            // TODO: When chat system is available, send message to chat
            // In Java, this uses Helper.HELPER.logNotification(message, true) which sends to chat
            // For now, just log to console
        }
    }
}

