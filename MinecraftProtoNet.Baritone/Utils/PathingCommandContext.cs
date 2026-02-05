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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/utils/PathingCommandContext.java
 */

using MinecraftProtoNet.Baritone.Api.Pathing.Goals;
using MinecraftProtoNet.Baritone.Api.Process;
using MinecraftProtoNet.Baritone.Pathfinding.Movement;

namespace MinecraftProtoNet.Baritone.Utils;

/// <summary>
/// Pathing command with custom calculation context.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/utils/PathingCommandContext.java
/// </summary>
public class PathingCommandContext : PathingCommand
{
    /// <summary>
    /// The desired calculation context for this pathfinding command.
    /// </summary>
    public readonly CalculationContext DesiredCalcContext;

    public PathingCommandContext(Goal? goal, PathingCommandType commandType, CalculationContext context) 
        : base(goal, commandType)
    {
        DesiredCalcContext = context;
    }
}

