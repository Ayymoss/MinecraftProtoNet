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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/utils/player/BaritonePlayerContext.java (inner class implementation)
 */

using MinecraftProtoNet.Baritone.Api;
using MinecraftProtoNet.Baritone.Api.Utils;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.State;

namespace MinecraftProtoNet.Baritone.Utils.Player;

/// <summary>
/// Baritone player controller implementation for headless client.
/// Provides default reach distance and basic block interaction stubs.
/// </summary>
public class BaritonePlayerController : IPlayerController
{
    private readonly IBaritone _baritone;
    private readonly IMinecraftClient _mc;

    public BaritonePlayerController(IBaritone baritone, IMinecraftClient mc)
    {
        _baritone = baritone;
        _mc = mc;
    }

    public void SyncHeldItem()
    {
        // No-op for headless
    }

    public bool HasBrokenBlock() => false;

    public bool OnPlayerDamageBlock(BetterBlockPos pos, int side) => false;

    public void ResetBlockRemoving()
    {
        // No-op for headless
    }

    public void WindowClick(int windowId, int slotId, int mouseButton, int type, object player)
    {
        // TODO: Implement window click packets when needed
    }

    public int GetGameType()
    {
        // Default to Survival (0)
        return (int)(_mc.State.LocalPlayer?.GameMode ?? 0);
    }

    public int ProcessRightClickBlock(object player, object world, int hand, object result) => 0;

    public int ProcessRightClick(object player, object world, int hand) => 0;

    public bool ClickBlock(BetterBlockPos loc, int face) => false;

    public void SetHittingBlock(bool hittingBlock)
    {
        // No-op for headless
    }

    public double GetBlockReachDistance()
    {
        // Standard Minecraft survival reach is 4.5 blocks, creative is 5.0
        // We'll return 4.5 as a safe default
        return 4.5;
    }
}
