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

    /// <summary>
    /// Reference: LookBehavior.java:67-69 - store the target ONLY. The rotation is applied once in
    /// OnPlayerUpdate(PRE) and restored in POST. There is deliberately no eager write here: writing the aim
    /// onto the entity permanently makes placement aims (pitch ~88deg, where yaw is degenerate) stick as the
    /// player's heading, so the next tick's target is computed relative to the flipped yaw and has to flip
    /// back - the paired 180deg snaps. Same-tick place checks stay correct because
    /// BaritonePlayerContext.PlayerRotations() reads GetEffectiveRotation() (the rotation actually sent),
    /// which is what Java does.
    /// </summary>
    public void UpdateTarget(Rotation rotation, bool blockInteract)
    {
        _target = new Target(rotation, Target.Resolve(Ctx, blockInteract));
    }

    /// <summary>
    /// Reference: LookBehavior.java:161-167.
    /// </summary>
    public Rotation? GetEffectiveRotation()
    {
        if (Core.Baritone.Settings().FreeLook.Value)
        {
            return _serverRotation;
        }
        // If freeLook isn't on, just defer to the player's actual rotations
        return null;
    }

    // TEMP diagnostic: trace the per-tick rotation write sequence (gated by MCPROTO_LOOK_DEBUG=1; off by default).
    internal static bool LookDebug = Environment.GetEnvironmentVariable("MCPROTO_LOOK_DEBUG") == "1";

    public IAimProcessor GetAimProcessor() => _processor;

    public override void OnTick(TickEvent evt)
    {
        if (evt.GetType() == TickEvent.TickEventType.In)
        {
            Utils.AimDiag.BeginTick((Ctx.World() as Level)?.ClientTickCounter ?? 0);
            _processor.Tick();
        }
    }

    public override void OnPlayerUpdate(PlayerUpdateEvent evt)
    {
        if (_target == null)
        {
            if (LookDebug) Console.WriteLine($"[LOOK] OnPlayerUpdate {evt.GetState()} _target=NULL (skip)");
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
                    float before = player.YawPitch.X;
                    float beforePitch = player.YawPitch.Y;
                    _prevRotation = new Rotation(player.YawPitch.X, player.YawPitch.Y);
                    var actual = _processor.PeekRotation(_target.Rotation);
                    player.YawPitch = new Vector2<float>(actual.GetYaw(), actual.GetPitch());
                    // This is the rotation the outgoing movement packet will carry this tick (PRE runs after
                    // the bot tick and before physics/packet send), i.e. "the rotation known to the server".
                    // Java captures it in onSendPacket off the real ServerboundMovePlayerPacket; our Core has
                    // no send-packet event wired (GameEventHandler.OnSendPacket is never fired), so capture it
                    // here instead - equivalent given the PRE -> packet -> POST ordering.
                    _serverRotation = actual;
                    Utils.AimDiag.Write("pre", _target.Mode.ToString(),
                        _target.Rotation.GetYaw(), _target.Rotation.GetPitch(),
                        before, beforePitch, actual.GetYaw(), actual.GetPitch());
                    if (LookDebug) Console.WriteLine($"[LOOK] PRE mode={_target.Mode} tgtYaw={_target.Rotation.GetYaw():F1} peekYaw={actual.GetYaw():F1} player {before:F1}->{player.YawPitch.X:F1}");
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
                        Utils.AimDiag.Write("post-restore", _target.Mode.ToString(),
                            _target.Rotation.GetYaw(), _target.Rotation.GetPitch(),
                            playerPost.YawPitch.X, playerPost.YawPitch.Y,
                            _prevRotation.GetYaw(), _prevRotation.GetPitch());
                        if (LookDebug) Console.WriteLine($"[LOOK] POST Server-restore player {playerPost.YawPitch.X:F1}->{_prevRotation.GetYaw():F1}");
                        playerPost.YawPitch = new Vector2<float>(_prevRotation.GetYaw(), _prevRotation.GetPitch());
                    }
                    // Reference: LookBehavior.java:117-122 - non-fall-flying: smooth YAW only and KEEP the
                    // aimed pitch (Java does not set pitch here, so it stays at the PRE-peeked value). Averaging
                    // pitch too would smear placement aim off the block face.
                    // NOTE: the fall-flying branch (elytraSmoothLook + pitch averaging) is omitted because the
                    // fall-flying state isn't exposed on the player entity yet (see AIM7-ELYTRA).
                    else if (Core.Baritone.Settings().SmoothLook.Value)
                    {
                        float avgYaw = _smoothYawBuffer.Count > 0 ? (float)_smoothYawBuffer.Average() : _prevRotation.GetYaw();
                        playerPost.YawPitch = new Vector2<float>(avgYaw, playerPost.YawPitch.Y);
                    }
                    _prevRotation = null;
                }
                _target = null;
                break;
        }
    }

    public override void OnSendPacket(PacketEvent evt)
    {
        var player = Ctx.Player() as Entity;
        if (player != null)
        {
            _serverRotation = new Rotation(player.YawPitch.X, player.YawPitch.Y);
        }
    }

    public override void OnWorldEvent(WorldEvent evt)
    {
        _serverRotation = null;
        _target = null;
    }

    public override void OnPlayerRotationMove(RotationMoveEvent evt)
    {
        if (_target != null)
        {
            var actual = _processor.PeekRotation(_target.Rotation);
            if (LookDebug) Console.WriteLine($"[LOOK] RotationMove tgtYaw={_target.Rotation.GetYaw():F1} peekYaw={actual.GetYaw():F1} (livePrev={Ctx.PlayerRotations()?.GetYaw():F1})");
            evt.SetYaw(actual.GetYaw());
            evt.SetPitch(actual.GetPitch());
        }
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

        /// <summary>
        /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/behavior/LookBehavior.java:336-356
        /// NOTE: this previously used RotateToBreakBlocks/RotateToPlaceBlocks, which are not what Java consults
        /// here, and returned SERVER for block interaction - the opposite of Java. SERVER mode restores the
        /// client rotation at POST, so placement aims were reverted every tick and the entity rotation froze at
        /// a stale value (physics is yaw-relative, so the bot then walked the wrong way and never placed).
        /// Java deliberately uses CLIENT for block interaction: the aim must actually stick, otherwise
        /// objectMouseOver() reflects wherever the player is visually looking and Baritone halts.
        /// </summary>
        public static TargetMode Resolve(IPlayerContext ctx, bool blockInteract)
        {
            var settings = Core.Baritone.Settings();
            bool antiCheat = settings.AntiCheatCompatibility.Value;
            bool blockFreeLook = settings.BlockFreeLook.Value;

            // Java checks ctx.player().isFallFlying() first; elytra state is not exposed on our player entity
            // yet (see AIM7-ELYTRA), so the fall-flying branch is omitted and we fall through to freeLook.
            if (settings.FreeLook.Value)
            {
                // Regardless of antiCheatCompatibility, a blockInteract needs the player rotation set somehow,
                // otherwise Baritone halts since objectMouseOver() is whatever the player is mousing over.
                if (blockInteract)
                {
                    return blockFreeLook ? TargetMode.Server : TargetMode.Client;
                }
                return antiCheat ? TargetMode.Server : TargetMode.None;
            }

            // all freeLook settings are disabled so set the angles
            return TargetMode.Client;
        }
    }
}
