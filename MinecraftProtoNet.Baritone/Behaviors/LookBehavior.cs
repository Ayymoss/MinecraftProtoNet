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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/behavior/LookBehavior.java
 */

using MinecraftProtoNet.Baritone.Api;
using MinecraftProtoNet.Baritone.Api.Behavior;
using MinecraftProtoNet.Baritone.Api.Behavior.Look;
using MinecraftProtoNet.Baritone.Api.Event.Events;
using MinecraftProtoNet.Baritone.Api.Utils;
using MinecraftProtoNet.Baritone.Behaviors.Look;
using MinecraftProtoNet.Baritone.Core;
using MinecraftProtoNet.Core.Models.Core;
using MinecraftProtoNet.Core.State;

namespace MinecraftProtoNet.Baritone.Behaviors;

/// <summary>
/// Look behavior implementation.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/behavior/LookBehavior.java
/// </summary>
public class LookBehavior : Behavior, ILookBehavior
{
    private Target? _target;
    private Rotation? _serverRotation;
    private Rotation? _prevRotation;
    private readonly AimProcessor _processor;
    private readonly Queue<float> _smoothYawBuffer;
    private readonly Queue<float> _smoothPitchBuffer;

    public LookBehavior(IBaritone baritone) : base(baritone)
    {
        _processor = new AimProcessor(Ctx);
        _smoothYawBuffer = new Queue<float>();
        _smoothPitchBuffer = new Queue<float>();
    }

    public void UpdateTarget(Rotation rotation, bool blockInteract)
    {
        _target = new Target(rotation, Target.Resolve(Ctx, blockInteract));
    }

    public IAimProcessor GetAimProcessor() => _processor;

    public override void OnTick(Api.Event.Events.TickEvent evt)
    {
        if (evt.GetType() == TickEvent.TickEventType.In)
        {
            _processor.Tick();
        }
    }

    public override void OnPlayerUpdate(PlayerUpdateEvent evt)
    {
        if (_target == null)
        {
            return;
        }

        switch (evt.GetState())
        {
            case Api.Event.Events.Type.EventState.Pre:
                if (_target.Mode == Target.TargetMode.None)
                {
                    return;
                }

                var player = Ctx.Player() as Entity;
                if (player != null)
                {
                    _prevRotation = new Rotation(player.YawPitch.X, player.YawPitch.Y);
                    var actual = _processor.PeekRotation(_target.Rotation);
                    player.YawPitch = new Vector2<float>(actual.GetYaw(), actual.GetPitch());
                }
                break;

            case Api.Event.Events.Type.EventState.Post:
                var playerPost = Ctx.Player() as Entity;
                if (_prevRotation != null && playerPost != null)
                {
                    _smoothYawBuffer.Enqueue(_target.Rotation.GetYaw());
                    while (_smoothYawBuffer.Count > Core.Baritone.Settings().SmoothLookTicks.Value)
                    {
                        _smoothYawBuffer.Dequeue();
                    }
                    _smoothPitchBuffer.Enqueue(_target.Rotation.GetPitch());
                    while (_smoothPitchBuffer.Count > Core.Baritone.Settings().SmoothLookTicks.Value)
                    {
                        _smoothPitchBuffer.Dequeue();
                    }

                    if (_target.Mode == Target.TargetMode.Server)
                    {
                        playerPost.YawPitch = new Vector2<float>(_prevRotation.GetYaw(), _prevRotation.GetPitch());
                    }
                    else
                    {
                        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/behavior/LookBehavior.java:111-118
                        // Check for elytra, smooth look settings
                        bool hasElytra = false; // TODO: Check for elytra when equipment system is available
                        if (hasElytra && Core.Baritone.Settings().ElytraLookBehavior.Value)
                        {
                            // Elytra look behavior - don't smooth
                            playerPost.YawPitch = new Vector2<float>(_target.Rotation.GetYaw(), _target.Rotation.GetPitch());
                        }
                        else if (Core.Baritone.Settings().SmoothLook.Value)
                        {
                            float avgYaw = _smoothYawBuffer.Count > 0 ? (float)_smoothYawBuffer.Average() : _prevRotation.GetYaw();
                            float avgPitch = _smoothPitchBuffer.Count > 0 ? (float)_smoothPitchBuffer.Average() : _prevRotation.GetPitch();
                            playerPost.YawPitch = new Vector2<float>(avgYaw, avgPitch);
                        }
                    }
                    _prevRotation = null;
                }
                _target = null;
                break;
        }
    }

    public override void OnSendPacket(PacketEvent evt)
    {
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/behavior/LookBehavior.java:127-131
        // Update serverRotation from packet
        // This would require packet type checking
        // TODO: Implement when packet system is available
        // For now, extract rotation from player entity
        var player = Ctx.Player() as Entity;
        if (player != null)
        {
            _serverRotation = new Rotation(player.YawPitch.X, player.YawPitch.Y);
        }
    }

    public override void OnWorldEvent(WorldEvent evt)
    {
        _serverRotation = null;
    }

    private class Target
    {
        public Rotation Rotation { get; }
        public TargetMode Mode { get; }

        public Target(Rotation rotation, TargetMode mode)
        {
            Rotation = rotation;
            Mode = mode;
        }

        public enum TargetMode
        {
            None,
            Client,
            Server
        }

        public static TargetMode Resolve(IPlayerContext ctx, bool blockInteract)
        {
            // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/behavior/LookBehavior.java:156-161
            // Complex logic to determine mode
            var settings = Core.Baritone.Settings();
            if (blockInteract)
            {
                return settings.RotateToBreakBlocks.Value ? TargetMode.Server : TargetMode.Client;
            }
            return settings.RotateToPlaceBlocks.Value ? TargetMode.Server : TargetMode.Client;
        }
    }
}

