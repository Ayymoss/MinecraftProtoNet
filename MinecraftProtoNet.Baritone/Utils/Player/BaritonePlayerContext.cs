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
    private readonly IPlayerController _playerController;

    public BaritonePlayerContext(IBaritone baritone, IMinecraftClient mc)
    {
        _baritone = baritone;
        _mc = mc;
        _playerController = new BaritonePlayerController(baritone, mc);
    }

    public object Minecraft() => _mc;
    public object? Player() => _mc.State.LocalPlayer?.Entity;
    public IPlayerController PlayerController() => _playerController;
    public object? World() => _mc.State.Level;
    public IWorldData? WorldData() => _baritone.GetWorldProvider().GetCurrentWorld();
    public object? ObjectMouseOver()
    {
        var player = _mc.State.LocalPlayer?.Entity;
        var level = _mc.State.Level;
        if (player == null || level == null) return null;
        
        return player.GetLookingAtBlock(level, _playerController.GetBlockReachDistance());
    }

    public BetterBlockPos? PlayerFeet()
    {
        var player = _mc.State.LocalPlayer?.Entity;
        if (player == null) return null;
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/utils/IPlayerContext.java:63-82
        // Java adds +0.1251 to Y to handle soul sand and block boundary cases
        var feet = new BetterBlockPos(player.Position.X, player.Position.Y + 0.1251, player.Position.Z);

        // Slab check: if the block at feet is a slab, return feet.above()
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/utils/IPlayerContext.java:76-78
        try
        {
            var state = BlockStateInterface.Get(this, feet);
            if (state.Name.Contains("slab", StringComparison.OrdinalIgnoreCase))
            {
                return feet.Above();
            }
        }
        catch
        {
            // Ignore exceptions from null world or cross-thread access
        }

        return feet;
    }

    public Vector3<double>? PlayerHead()
    {
        var player = _mc.State.LocalPlayer?.Entity;
        if (player == null) return null;
        // Return eye position (head), not feet position
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/utils/IPlayerContext.java:47
        return player.EyePosition;
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

    public BetterBlockPos? GetSelectedBlock()
    {
        var player = _mc.State.LocalPlayer?.Entity;
        var level = _mc.State.Level;
        if (player == null || level == null) return null;

        var hit = player.GetLookingAtBlock(level, _playerController.GetBlockReachDistance());
        if (hit == null) return null;

        return new BetterBlockPos(hit.BlockPosition.X, hit.BlockPosition.Y, hit.BlockPosition.Z);
    }

    public bool IsLookingAt(BetterBlockPos pos)
    {
        var selected = GetSelectedBlock();
        return selected != null && selected.X == pos.X && selected.Y == pos.Y && selected.Z == pos.Z;
    }
}

