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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/utils/player/BaritonePlayerContext.java
 */

using MinecraftProtoNet.Baritone.Api;
using MinecraftProtoNet.Baritone.Api.Cache;
using MinecraftProtoNet.Baritone.Api.Utils;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Models.Core;

namespace MinecraftProtoNet.Baritone.Utils.Player;

/// <summary>
/// Baritone player context implementation.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/utils/player/BaritonePlayerContext.java
/// </summary>
public class BaritonePlayerContext : IPlayerContext
{
    private readonly IBaritone _baritone;
    private readonly IMinecraftClient _mc;

    public BaritonePlayerContext(IBaritone baritone, IMinecraftClient mc)
    {
        _baritone = baritone;
        _mc = mc;
    }

    public object Minecraft() => _mc;
    public object? Player() => _mc.State.LocalPlayer?.Entity;
    public IPlayerController PlayerController() => throw new NotImplementedException(); // Will be implemented
    public object? World() => _mc.State.Level;
    public IWorldData? WorldData() => _baritone.GetWorldProvider().GetCurrentWorld();
    public object? ObjectMouseOver() => null;
    public BetterBlockPos? PlayerFeet()
    {
        var player = _mc.State.LocalPlayer?.Entity;
        if (player == null) return null;
        return new BetterBlockPos(
            (int)Math.Floor(player.Position.X),
            (int)Math.Floor(player.Position.Y),
            (int)Math.Floor(player.Position.Z)
        );
    }

    public Vector3<double>? PlayerHead()
    {
        var player = _mc.State.LocalPlayer?.Entity;
        return player?.Position;
    }

    public Vector3<double>? PlayerMotion()
    {
        var player = _mc.State.LocalPlayer?.Entity;
        return player?.Velocity;
    }

    public BetterBlockPos? ViewerPos() => PlayerFeet();
    public Rotation? PlayerRotations()
    {
        var player = _mc.State.LocalPlayer?.Entity;
        if (player == null) return null;
        return new Rotation(player.YawPitch.X, player.YawPitch.Y);
    }

    public BetterBlockPos? GetSelectedBlock() => null;
    public bool IsLookingAt(BetterBlockPos pos) => false;
}

