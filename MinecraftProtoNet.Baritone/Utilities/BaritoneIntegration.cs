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
using MinecraftProtoNet.Baritone.Settings;
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

    // Track whether we've subscribed to Level.BlockChanged to avoid duplicate subscriptions
    private static volatile bool _blockChangeHooked;

    // The host whose profile is currently loaded, so the settings are only rewritten when it actually changes.
    private static string? _profiledHost;

    /// <summary>
    /// Keeps Baritone's build permissions in step with the server it is connected to.
    ///
    /// Done here, on the tick, because permissions are a property of the SERVER and not of whichever code path
    /// happened to call Connect — five call sites exist today and any new one would otherwise inherit vanilla
    /// permissions on a server that forbids building. On Hypixel that meant Baritone costing routes through
    /// glass panes it can never break, then swinging at them until the movement timed out.
    /// </summary>
    private static void ApplyServerProfile(string? host, ILogger? logger)
    {
        if (string.Equals(host, _profiledHost, StringComparison.OrdinalIgnoreCase)) return;

        _profiledHost = host;
        var profile = ServerProfiles.For(host);
        ServerProfiles.Apply(profile);

        logger?.LogInformation(
            "Baritone server profile \"{Profile}\" applied for {Host}: allowBreak={Break}, allowPlace={Place}",
            profile.Name, host ?? "(disconnected)", profile.AllowBreak, profile.AllowPlace);
    }

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
                ApplyServerProfile(client.State.ConnectedServerHost, logger);

                // Subscribe to Level.BlockChanged once to forward to Baritone's event system
                if (!_blockChangeHooked)
                {
                    _blockChangeHooked = true;
                    client.State.Level.BlockChanged += (x, y, z, blockStateId) =>
                    {
                        try
                        {
                            var chunkX = x >> 4;
                            var chunkZ = z >> 4;
                            var pos = new Api.Utils.BetterBlockPos(x, y, z);
                            var blocks = new List<(Api.Utils.BetterBlockPos Pos, object BlockState)>
                            {
                                (pos, blockStateId)
                            };
                            var evt = new BlockChangeEvent((chunkX, chunkZ), blocks);

                            var provider = BaritoneAPI.GetProvider();
                            foreach (var b in provider.GetAllBaritones())
                            {
                                b.GetGameEventHandler().OnBlockChange(evt);
                            }
                        }
                        catch (Exception ex)
                        {
                            logger?.LogTrace(ex, "Error dispatching Baritone block change event");
                        }
                    };
                }

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
                            
                            // Java's mixin order: the bot tick computes the aim target first, then the player
                            // tick applies it (PRE) and restores it afterwards (POST).
                            // Reference: baritone-1.21.11-REFERENCE-ONLY/src/launch/java/baritone/launch/mixins/MixinClientPlayerEntity.java:73
                            //
                            // PRE previously ran BEFORE OnTick, so _target was always null and PRE applied
                            // nothing (measured: 582/582 rotation writes came from the eager write in
                            // UpdateTarget, 0 from PRE, 0 restores from POST). The eager write was added to
                            // compensate, but it makes the aim permanent — which is what produced the paired
                            // 180deg yaw flips. An earlier attempt at this reorder (AIM4) regressed pillaring
                            // because same-tick place checks read the entity rotation; that is now fixed
                            // properly, the Java way, via LookBehavior.GetEffectiveRotation() being consumed by
                            // BaritonePlayerContext.PlayerRotations().
                            baritone.GetGameEventHandler().OnTick(tickProvider(EventState.Pre, tickType));

                            if (tickType == TickEvent.TickEventType.In)
                            {
                                baritone.GetGameEventHandler().OnPlayerUpdate(
                                    new Api.Event.Events.PlayerUpdateEvent(EventState.Pre));
                            }
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

