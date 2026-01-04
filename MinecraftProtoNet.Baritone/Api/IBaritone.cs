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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/IBaritone.java
 */

using MinecraftProtoNet.Baritone.Api.Behavior;
using MinecraftProtoNet.Baritone.Api.Cache;
using MinecraftProtoNet.Baritone.Api.Command.Manager;
using MinecraftProtoNet.Baritone.Api.Event.Listener;
using MinecraftProtoNet.Baritone.Api.Pathing.Calc;
using MinecraftProtoNet.Baritone.Api.Process;
using MinecraftProtoNet.Baritone.Api.Selection;
using MinecraftProtoNet.Baritone.Api.Utils;
using MinecraftProtoNet.Core.Services;

namespace MinecraftProtoNet.Baritone.Api;

/// <summary>
/// Main Baritone interface.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/IBaritone.java
/// </summary>
public interface IBaritone
{
    /// <summary>
    /// Gets the pathing behavior instance.
    /// </summary>
    IPathingBehavior GetPathingBehavior();

    /// <summary>
    /// Gets the look behavior instance.
    /// </summary>
    ILookBehavior GetLookBehavior();

    /// <summary>
    /// Gets the follow process instance.
    /// </summary>
    IFollowProcess GetFollowProcess();

    /// <summary>
    /// Gets the mine process instance.
    /// </summary>
    IMineProcess GetMineProcess();

    /// <summary>
    /// Gets the builder process instance.
    /// </summary>
    IBuilderProcess GetBuilderProcess();

    /// <summary>
    /// Gets the explore process instance.
    /// </summary>
    IExploreProcess GetExploreProcess();

    /// <summary>
    /// Gets the farm process instance.
    /// </summary>
    IFarmProcess GetFarmProcess();

    /// <summary>
    /// Gets the custom goal process instance.
    /// </summary>
    ICustomGoalProcess GetCustomGoalProcess();

    /// <summary>
    /// Gets the get-to-block process instance.
    /// </summary>
    IGetToBlockProcess GetGetToBlockProcess();

    /// <summary>
    /// Gets the elytra process instance.
    /// </summary>
    IElytraProcess GetElytraProcess();

    /// <summary>
    /// Gets the world provider instance.
    /// </summary>
    IWorldProvider GetWorldProvider();

    /// <summary>
    /// Gets the pathing control manager instance.
    /// </summary>
    IPathingControlManager GetPathingControlManager();

    /// <summary>
    /// Gets the input override handler instance.
    /// </summary>
    IInputOverrideHandler GetInputOverrideHandler();

    /// <summary>
    /// Gets the inventory behavior instance.
    /// </summary>
    IInventoryBehavior GetInventoryBehavior();

    /// <summary>
    /// Gets the player context instance.
    /// </summary>
    IPlayerContext GetPlayerContext();

    /// <summary>
    /// Gets the game event handler (event bus) instance.
    /// </summary>
    IEventBus GetGameEventHandler();

    /// <summary>
    /// Gets the selection manager instance.
    /// </summary>
    ISelectionManager GetSelectionManager();

    /// <summary>
    /// Gets the command manager instance.
    /// </summary>
    ICommandManager GetCommandManager();

    /// <summary>
    /// Opens the click GUI (not implemented for headless client).
    /// </summary>
    void OpenClick();

    /// <summary>
    /// Gets the item registry service for item name lookups.
    /// </summary>
    IItemRegistryService GetItemRegistryService();
}

