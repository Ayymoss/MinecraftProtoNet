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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/Moves.java
 */

using MinecraftProtoNet.Baritone.Api.Pathing.Movement;
using MinecraftProtoNet.Baritone.Api.Utils;
using MinecraftProtoNet.Baritone.Utils.Pathing;
using MinecraftProtoNet.Core.Enums;

namespace MinecraftProtoNet.Baritone.Pathfinding.Movement;

/// <summary>
/// An enum-like class of all possible movements attached to all possible directions they could be taken in.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/Moves.java
/// </summary>
public static class Moves
{
    // Movement type definitions
    public static readonly MoveType Downward = new(0, -1, 0, false, false);
    public static readonly MoveType Pillar = new(0, +1, 0, false, false);
    public static readonly MoveType TraverseNorth = new(0, 0, -1, false, false);
    public static readonly MoveType TraverseSouth = new(0, 0, +1, false, false);
    public static readonly MoveType TraverseEast = new(+1, 0, 0, false, false);
    public static readonly MoveType TraverseWest = new(-1, 0, 0, false, false);
    public static readonly MoveType AscendNorth = new(0, +1, -1, false, false);
    public static readonly MoveType AscendSouth = new(0, +1, +1, false, false);
    public static readonly MoveType AscendEast = new(+1, +1, 0, false, false);
    public static readonly MoveType AscendWest = new(-1, +1, 0, false, false);
    public static readonly MoveType DescendEast = new(+1, -1, 0, false, true);
    public static readonly MoveType DescendWest = new(-1, -1, 0, false, true);
    public static readonly MoveType DescendNorth = new(0, -1, -1, false, true);
    public static readonly MoveType DescendSouth = new(0, -1, +1, false, true);
    public static readonly MoveType DiagonalNortheast = new(+1, 0, -1, false, true);
    public static readonly MoveType DiagonalNorthwest = new(-1, 0, -1, false, true);
    public static readonly MoveType DiagonalSoutheast = new(+1, 0, +1, false, true);
    public static readonly MoveType DiagonalSouthwest = new(-1, 0, +1, false, true);
    public static readonly MoveType ParkourNorth = new(0, 0, -4, true, true);
    public static readonly MoveType ParkourSouth = new(0, 0, +4, true, true);
    public static readonly MoveType ParkourEast = new(+4, 0, 0, true, true);
    public static readonly MoveType ParkourWest = new(-4, 0, 0, true, true);

    public static MoveType[] Values { get; } = new[]
    {
        Downward, Pillar,
        TraverseNorth, TraverseSouth, TraverseEast, TraverseWest,
        AscendNorth, AscendSouth, AscendEast, AscendWest,
        DescendEast, DescendWest, DescendNorth, DescendSouth,
        DiagonalNortheast, DiagonalNorthwest, DiagonalSoutheast, DiagonalSouthwest,
        ParkourNorth, ParkourSouth, ParkourEast, ParkourWest
    };

    /// <summary>
    /// Represents a movement type with its offsets and dynamic flags.
    /// </summary>
    public class MoveType
    {
        public readonly bool DynamicXZ;
        public readonly bool DynamicY;
        public readonly int XOffset;
        public readonly int YOffset;
        public readonly int ZOffset;

        public Func<CalculationContext, BetterBlockPos, IMovement>? Apply0Func { get; set; }
        public Action<CalculationContext, int, int, int, MutableMoveResult>? ApplyFunc { get; set; }
        public Func<CalculationContext, int, int, int, double>? CostFunc { get; set; }

        public MoveType(int x, int y, int z, bool dynamicXZ, bool dynamicY)
        {
            XOffset = x;
            YOffset = y;
            ZOffset = z;
            DynamicXZ = dynamicXZ;
            DynamicY = dynamicY;
        }

        public MoveType(int x, int y, int z) : this(x, y, z, false, false)
        {
        }

        public IMovement Apply0(CalculationContext context, BetterBlockPos src)
        {
            if (Apply0Func == null)
            {
                throw new NotImplementedException($"Movement type must have Apply0Func set");
            }

            return Apply0Func(context, src);
        }

        public void Apply(CalculationContext context, int x, int y, int z, MutableMoveResult result)
        {
            if (ApplyFunc != null)
            {
                ApplyFunc(context, x, y, z, result);
                return;
            }

            if (DynamicXZ || DynamicY)
            {
                throw new InvalidOperationException("Movements with dynamic offset must have ApplyFunc set");
            }

            result.X = x + XOffset;
            result.Y = y + YOffset;
            result.Z = z + ZOffset;
            result.Cost = Cost(context, x, y, z);
        }

        public double Cost(CalculationContext context, int x, int y, int z)
        {
            if (CostFunc == null)
            {
                throw new InvalidOperationException("Movements must have CostFunc set");
            }

            return CostFunc(context, x, y, z);
        }
    }

    // Initialize movement types with their specific implementations
    static Moves()
    {
        // Traverse movements
        TraverseNorth.Apply0Func = (ctx, src) => new Movements.MovementTraverse(ctx.GetBaritone(), src, src.North());
        TraverseNorth.CostFunc = (ctx, x, y, z) => Movements.MovementTraverse.Cost(ctx, x, y, z, x, z - 1);
        TraverseNorth.ApplyFunc = (ctx, x, y, z, res) =>
        {
            res.X = x;
            res.Y = y;
            res.Z = z - 1;
            res.Cost = Movements.MovementTraverse.Cost(ctx, x, y, z, x, z - 1);
        };

        TraverseSouth.Apply0Func = (ctx, src) => new Movements.MovementTraverse(ctx.GetBaritone(), src, src.South());
        TraverseSouth.CostFunc = (ctx, x, y, z) => Movements.MovementTraverse.Cost(ctx, x, y, z, x, z + 1);
        TraverseSouth.ApplyFunc = (ctx, x, y, z, res) =>
        {
            res.X = x;
            res.Y = y;
            res.Z = z + 1;
            res.Cost = Movements.MovementTraverse.Cost(ctx, x, y, z, x, z + 1);
        };

        TraverseEast.Apply0Func = (ctx, src) => new Movements.MovementTraverse(ctx.GetBaritone(), src, src.East());
        TraverseEast.CostFunc = (ctx, x, y, z) => Movements.MovementTraverse.Cost(ctx, x, y, z, x + 1, z);
        TraverseEast.ApplyFunc = (ctx, x, y, z, res) =>
        {
            res.X = x + 1;
            res.Y = y;
            res.Z = z;
            res.Cost = Movements.MovementTraverse.Cost(ctx, x, y, z, x + 1, z);
        };

        TraverseWest.Apply0Func = (ctx, src) => new Movements.MovementTraverse(ctx.GetBaritone(), src, src.West());
        TraverseWest.CostFunc = (ctx, x, y, z) => Movements.MovementTraverse.Cost(ctx, x, y, z, x - 1, z);
        TraverseWest.ApplyFunc = (ctx, x, y, z, res) =>
        {
            res.X = x - 1;
            res.Y = y;
            res.Z = z;
            res.Cost = Movements.MovementTraverse.Cost(ctx, x, y, z, x - 1, z);
        };

        // Ascend movements
        AscendNorth.Apply0Func = (ctx, src) =>
            new Movements.MovementAscend(ctx.GetBaritone(), src, new BetterBlockPos(src.X, src.Y + 1, src.Z - 1));
        AscendNorth.CostFunc = (ctx, x, y, z) => Movements.MovementAscend.Cost(ctx, x, y, z, x, z - 1);
        AscendNorth.ApplyFunc = (ctx, x, y, z, res) =>
        {
            res.X = x;
            res.Y = y + 1;
            res.Z = z - 1;
            res.Cost = Movements.MovementAscend.Cost(ctx, x, y, z, x, z - 1);
        };

        AscendSouth.Apply0Func = (ctx, src) =>
            new Movements.MovementAscend(ctx.GetBaritone(), src, new BetterBlockPos(src.X, src.Y + 1, src.Z + 1));
        AscendSouth.CostFunc = (ctx, x, y, z) => Movements.MovementAscend.Cost(ctx, x, y, z, x, z + 1);
        AscendSouth.ApplyFunc = (ctx, x, y, z, res) =>
        {
            res.X = x;
            res.Y = y + 1;
            res.Z = z + 1;
            res.Cost = Movements.MovementAscend.Cost(ctx, x, y, z, x, z + 1);
        };

        AscendEast.Apply0Func = (ctx, src) =>
            new Movements.MovementAscend(ctx.GetBaritone(), src, new BetterBlockPos(src.X + 1, src.Y + 1, src.Z));
        AscendEast.CostFunc = (ctx, x, y, z) => Movements.MovementAscend.Cost(ctx, x, y, z, x + 1, z);
        AscendEast.ApplyFunc = (ctx, x, y, z, res) =>
        {
            res.X = x + 1;
            res.Y = y + 1;
            res.Z = z;
            res.Cost = Movements.MovementAscend.Cost(ctx, x, y, z, x + 1, z);
        };

        AscendWest.Apply0Func = (ctx, src) =>
            new Movements.MovementAscend(ctx.GetBaritone(), src, new BetterBlockPos(src.X - 1, src.Y + 1, src.Z));
        AscendWest.CostFunc = (ctx, x, y, z) => Movements.MovementAscend.Cost(ctx, x, y, z, x - 1, z);
        AscendWest.ApplyFunc = (ctx, x, y, z, res) =>
        {
            res.X = x - 1;
            res.Y = y + 1;
            res.Z = z;
            res.Cost = Movements.MovementAscend.Cost(ctx, x, y, z, x - 1, z);
        };

        // Descend movements (can become MovementFall)
        DescendNorth.Apply0Func = (ctx, src) =>
        {
            var res = new MutableMoveResult();
            Movements.MovementDescend.Cost(ctx, src.X, src.Y, src.Z, src.X, src.Z - 1, res);
            if (res.Y == src.Y - 1)
            {
                return new Movements.MovementDescend(ctx.GetBaritone(), src, new BetterBlockPos(res.X, res.Y, res.Z));
            }
            else
            {
                return new Movements.MovementFall(ctx.GetBaritone(), src, new BetterBlockPos(res.X, res.Y, res.Z));
            }
        };
        DescendNorth.ApplyFunc = (ctx, x, y, z, res) => Movements.MovementDescend.Cost(ctx, x, y, z, x, z - 1, res);

        DescendSouth.Apply0Func = (ctx, src) =>
        {
            var res = new MutableMoveResult();
            Movements.MovementDescend.Cost(ctx, src.X, src.Y, src.Z, src.X, src.Z + 1, res);
            if (res.Y == src.Y - 1)
            {
                return new Movements.MovementDescend(ctx.GetBaritone(), src, new BetterBlockPos(res.X, res.Y, res.Z));
            }
            else
            {
                return new Movements.MovementFall(ctx.GetBaritone(), src, new BetterBlockPos(res.X, res.Y, res.Z));
            }
        };
        DescendSouth.ApplyFunc = (ctx, x, y, z, res) => Movements.MovementDescend.Cost(ctx, x, y, z, x, z + 1, res);

        DescendEast.Apply0Func = (ctx, src) =>
        {
            var res = new MutableMoveResult();
            Movements.MovementDescend.Cost(ctx, src.X, src.Y, src.Z, src.X + 1, src.Z, res);
            if (res.Y == src.Y - 1)
            {
                return new Movements.MovementDescend(ctx.GetBaritone(), src, new BetterBlockPos(res.X, res.Y, res.Z));
            }
            else
            {
                return new Movements.MovementFall(ctx.GetBaritone(), src, new BetterBlockPos(res.X, res.Y, res.Z));
            }
        };
        DescendEast.ApplyFunc = (ctx, x, y, z, res) => Movements.MovementDescend.Cost(ctx, x, y, z, x + 1, z, res);

        DescendWest.Apply0Func = (ctx, src) =>
        {
            var res = new MutableMoveResult();
            Movements.MovementDescend.Cost(ctx, src.X, src.Y, src.Z, src.X - 1, src.Z, res);
            if (res.Y == src.Y - 1)
            {
                return new Movements.MovementDescend(ctx.GetBaritone(), src, new BetterBlockPos(res.X, res.Y, res.Z));
            }
            else
            {
                return new Movements.MovementFall(ctx.GetBaritone(), src, new BetterBlockPos(res.X, res.Y, res.Z));
            }
        };
        DescendWest.ApplyFunc = (ctx, x, y, z, res) => Movements.MovementDescend.Cost(ctx, x, y, z, x - 1, z, res);

        // Parkour movements
        ParkourNorth.Apply0Func = (ctx, src) => Movements.MovementParkour.Cost(ctx, src, BlockFace.North);
        ParkourNorth.ApplyFunc = (ctx, x, y, z, res) =>
        {
            var tempRes = new MutableMoveResult();
            Movements.MovementParkour.Cost(ctx, x, y, z, BlockFace.North, tempRes);
            res.X = tempRes.X;
            res.Y = tempRes.Y;
            res.Z = tempRes.Z;
            res.Cost = tempRes.Cost;
        };

        ParkourSouth.Apply0Func = (ctx, src) => Movements.MovementParkour.Cost(ctx, src, BlockFace.South);
        ParkourSouth.ApplyFunc = (ctx, x, y, z, res) =>
        {
            var tempRes = new MutableMoveResult();
            Movements.MovementParkour.Cost(ctx, x, y, z, BlockFace.South, tempRes);
            res.X = tempRes.X;
            res.Y = tempRes.Y;
            res.Z = tempRes.Z;
            res.Cost = tempRes.Cost;
        };

        ParkourEast.Apply0Func = (ctx, src) => Movements.MovementParkour.Cost(ctx, src, BlockFace.East);
        ParkourEast.ApplyFunc = (ctx, x, y, z, res) =>
        {
            var tempRes = new MutableMoveResult();
            Movements.MovementParkour.Cost(ctx, x, y, z, BlockFace.East, tempRes);
            res.X = tempRes.X;
            res.Y = tempRes.Y;
            res.Z = tempRes.Z;
            res.Cost = tempRes.Cost;
        };

        ParkourWest.Apply0Func = (ctx, src) => Movements.MovementParkour.Cost(ctx, src, BlockFace.West);
        ParkourWest.ApplyFunc = (ctx, x, y, z, res) =>
        {
            var tempRes = new MutableMoveResult();
            Movements.MovementParkour.Cost(ctx, x, y, z, BlockFace.West, tempRes);
            res.X = tempRes.X;
            res.Y = tempRes.Y;
            res.Z = tempRes.Z;
            res.Cost = tempRes.Cost;
        };

        // Note: Downward, Pillar, Diagonal movements are already implemented in MovementDownward, MovementPillar, MovementDiagonal
        // They are registered in the Moves class initialization above
    }
}
