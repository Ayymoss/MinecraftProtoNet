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
 */

using System.Threading;
using Microsoft.Extensions.Logging;
using MinecraftProtoNet.Baritone.Api;
using MinecraftProtoNet.Baritone.Api.Event.Events;
using MinecraftProtoNet.Baritone.Api.Event.Events.Type;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Core.Core.Abstractions;

namespace MinecraftProtoNet.Baritone.Utilities;

/// <summary>
/// Integration helper for connecting Baritone to the game loop.
/// This avoids circular dependencies by keeping Core independent of Baritone.
/// </summary>
public static class BaritoneIntegration
{
    // Thread-local storage for tick provider to share between PRE and POST tick events
    // Reference: baritone-1.21.11-REFERENCE-ONLY/src/launch/java/baritone/launch/mixins/MixinMinecraft.java:54-110
    private static readonly ThreadLocal<Func<EventState, TickEvent.TickEventType, TickEvent>?> TickProviderStorage = new();

    /// <summary>
    /// Hooks Baritone tick events to the game loop.
    /// Call this from the application layer (e.g., Bot.Webcore) after creating the GameLoop.
    /// </summary>
    /// <param name="gameLoop">The game loop instance</param>
    /// <param name="logger">Optional logger for error reporting</param>
    public static void HookToGameLoop(IGameLoop gameLoop, ILogger? logger = null)
    {
        logger?.LogWarning("BaritoneIntegration.HookToGameLoop: Setting up tick event handlers");
        // Hook PRE tick events
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/launch/java/baritone/launch/mixins/MixinMinecraft.java:65-91
        gameLoop.PreTick += client =>
        {
            try
            {
                var baritoneProvider = BaritoneAPI.GetProvider();
                var allBaritones = baritoneProvider.GetAllBaritones();
                
                if (allBaritones.Count > 0)
                {
                    var tickProvider = TickEvent.CreateNextProvider();
                    TickProviderStorage.Value = tickProvider;
                    
                    foreach (var baritone in allBaritones)
                    {
                        try
                        {
                            var ctx = baritone.GetPlayerContext();
                            var tickType = ctx.Player() != null && ctx.World() != null
                                ? TickEvent.TickEventType.In
                                : TickEvent.TickEventType.Out;
                            
                            // Fire PlayerUpdateEvent PRE before tick (allows LookBehavior to set rotations)
                            // Reference: baritone-1.21.11-REFERENCE-ONLY/src/launch/java/baritone/launch/mixins/MixinClientPlayerEntity.java:73
                            if (tickType == TickEvent.TickEventType.In)
                            {
                                baritone.GetGameEventHandler().OnPlayerUpdate(
                                    new Api.Event.Events.PlayerUpdateEvent(EventState.Pre));
                            }
                            
                            baritone.GetGameEventHandler().OnTick(tickProvider(EventState.Pre, tickType));
                        }
                        catch (Exception ex)
                        {
                            logger?.LogWarning(ex, "Error dispatching Baritone PRE tick event");
                        }
                    }
                }
                else
                {
                    TickProviderStorage.Value = null;
                }
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Error initializing Baritone tick events");
                TickProviderStorage.Value = null;
            }
        };
        
        // Hook POST tick events
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/launch/java/baritone/launch/mixins/MixinMinecraft.java:93-110
        gameLoop.PostTick += client =>
        {
            var tickProvider = TickProviderStorage.Value;
            if (tickProvider == null)
            {
                return;
            }

            try
            {
                var baritoneProvider = BaritoneAPI.GetProvider();
                var allBaritones = baritoneProvider.GetAllBaritones();
                
                foreach (var baritone in allBaritones)
                {
                    try
                    {
                        var ctx = baritone.GetPlayerContext();
                        var tickType = ctx.Player() != null && ctx.World() != null
                            ? TickEvent.TickEventType.In
                            : TickEvent.TickEventType.Out;
                        
                        baritone.GetGameEventHandler().OnPostTick(tickProvider(EventState.Post, tickType));
                        
                        // Fire PlayerUpdateEvent POST after tick (allows LookBehavior to restore rotations if needed)
                        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/launch/java/baritone/launch/mixins/MixinMinecraft.java:125
                        if (tickType == TickEvent.TickEventType.In)
                        {
                            baritone.GetGameEventHandler().OnPlayerUpdate(
                                new Api.Event.Events.PlayerUpdateEvent(EventState.Post));
                        }
                    }
                    catch (Exception ex)
                    {
                        logger?.LogWarning(ex, "Error dispatching Baritone POST tick event");
                    }
                }
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Error dispatching Baritone POST tick events");
            }
            finally
            {
                TickProviderStorage.Value = null;
            }
        };
    }
}

