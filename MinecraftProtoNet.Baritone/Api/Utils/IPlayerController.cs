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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/utils/IPlayerController.java
 */

namespace MinecraftProtoNet.Baritone.Api.Utils;

/// <summary>
/// Interface for player controller.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/utils/IPlayerController.java
/// </summary>
public interface IPlayerController
{
    /// <summary>
    /// Syncs the held item.
    /// </summary>
    void SyncHeldItem();

    /// <summary>
    /// Returns whether a block has been broken.
    /// </summary>
    bool HasBrokenBlock();

    /// <summary>
    /// Called when player damages a block.
    /// </summary>
    bool OnPlayerDamageBlock(BetterBlockPos pos, int side);

    /// <summary>
    /// Resets block removing state.
    /// </summary>
    void ResetBlockRemoving();

    /// <summary>
    /// Performs a window click.
    /// </summary>
    void WindowClick(int windowId, int slotId, int mouseButton, int type, object player);

    /// <summary>
    /// Gets the game type.
    /// </summary>
    int GetGameType();

    /// <summary>
    /// Processes right click on block.
    /// </summary>
    int ProcessRightClickBlock(object player, object world, int hand, object result);

    /// <summary>
    /// Processes right click.
    /// </summary>
    int ProcessRightClick(object player, object world, int hand);

    /// <summary>
    /// Clicks a block.
    /// </summary>
    bool ClickBlock(BetterBlockPos loc, int face);

    /// <summary>
    /// Sets whether hitting a block.
    /// </summary>
    void SetHittingBlock(bool hittingBlock);

    /// <summary>
    /// Gets the block reach distance.
    /// </summary>
    double GetBlockReachDistance();
}

