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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/Movement.java
 */

using MinecraftProtoNet.Baritone.Api;
using MinecraftProtoNet.Baritone.Api.Pathing.Movement;
using MinecraftProtoNet.Baritone.Api.Utils;
using MinecraftProtoNet.Baritone.Api.Utils.Input;
using MinecraftProtoNet.Baritone.Behaviors;
using MinecraftProtoNet.Baritone.Utils;
using MinecraftProtoNet.Core.Enums;
using MinecraftProtoNet.Core.State;

namespace MinecraftProtoNet.Baritone.Pathfinding.Movement;

/// <summary>
/// Base class for all movements.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/Movement.java
/// </summary>
public abstract class Movement : IMovement
{
    public static readonly BlockFace[] HorizontalsButAlsoDown = 
    {
        BlockFace.North, BlockFace.South, BlockFace.East, BlockFace.West, BlockFace.Bottom
    };

    private static int _movementTickCounter = 0;

    protected readonly IBaritone Baritone;
    protected readonly IPlayerContext Ctx;

    private MovementState _currentState = new MovementState().SetStatus(MovementStatus.Prepping);

    protected readonly BetterBlockPos Src;
    protected readonly BetterBlockPos Dest;

    /// <summary>
    /// The positions that need to be broken before this movement can ensue.
    /// </summary>
    protected readonly BetterBlockPos[] PositionsToBreak;

    /// <summary>
    /// The position where we need to place a block before this movement can ensue.
    /// </summary>
    protected readonly BetterBlockPos? PositionToPlace;

    private double? _cost;

    public List<BetterBlockPos>? ToBreakCached = null;
    public List<BetterBlockPos>? ToPlaceCached = null;
    public List<BetterBlockPos>? ToWalkIntoCached = null;

    private HashSet<BetterBlockPos>? _validPositionsCached = null;

    private bool? _calculatedWhileLoaded;

    protected Movement(IBaritone baritone, BetterBlockPos src, BetterBlockPos dest, BetterBlockPos[] toBreak, BetterBlockPos? toPlace)
    {
        Baritone = baritone;
        Ctx = baritone.GetPlayerContext();
        Src = src;
        Dest = dest;
        PositionsToBreak = toBreak;
        PositionToPlace = toPlace;
    }

    protected Movement(IBaritone baritone, BetterBlockPos src, BetterBlockPos dest, BetterBlockPos[] toBreak)
        : this(baritone, src, dest, toBreak, null)
    {
    }

    public double GetCost()
    {
        if (_cost == null)
        {
            throw new InvalidOperationException("Cost has not been calculated yet. Call GetCost(CalculationContext) first.");
        }
        return _cost.Value;
    }

    public double GetCost(CalculationContext context)
    {
        if (_cost == null)
        {
            _cost = CalculateCost(context);
        }
        return _cost.Value;
    }

    public abstract double CalculateCost(CalculationContext context);

    public double RecalculateCost(CalculationContext context)
    {
        _cost = null;
        return GetCost(context);
    }

    public void Override(double cost)
    {
        _cost = cost;
    }

    protected abstract HashSet<BetterBlockPos> CalculateValidPositions();

    public HashSet<BetterBlockPos> GetValidPositions()
    {
        if (_validPositionsCached == null)
        {
            _validPositionsCached = CalculateValidPositions();
            if (_validPositionsCached == null)
            {
                throw new InvalidOperationException("CalculateValidPositions returned null");
            }
        }
        return _validPositionsCached;
    }

    protected bool PlayerInValidPosition()
    {
        var pathingBehavior = (PathingBehavior)Baritone.GetPathingBehavior();
        var playerFeet = Ctx.PlayerFeet();
        if (playerFeet == null)
        {
            return false;
        }
        return GetValidPositions().Contains(playerFeet);
    }

    /// <summary>
    /// Handles the execution of the latest Movement State, and offers a Status to the calling class.
    /// </summary>
    public MovementStatus Update()
    {
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/Movement.java:147
        // Disable flying ability
        var player = Ctx.Player() as Entity;
        // TODO: Disable flying when abilities system is available
        _currentState = UpdateState(_currentState);
        
        // If in liquid and below destination, jump
        if (MovementHelper.IsLiquid(Ctx, Ctx.PlayerFeet() ?? Src) && (Ctx.Player() as Entity)?.Position.Y < Dest.Y + 0.6)
        {
            _currentState.SetInput(Input.Jump, true);
        }
        
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/Movement.java:157-161
        // If player is in wall, break it
        var playerEntity = Ctx.Player() as Entity;
        if (playerEntity != null)
        {
            var feet = Ctx.PlayerFeet();
            if (feet != null)
            {
                var blockState = BlockStateInterface.Get(Ctx, feet);
                if (blockState != null && !MovementHelper.CanWalkThrough(Ctx, feet))
                {
                    MovementHelper.SwitchToBestToolFor(Ctx, blockState);
                    _currentState.SetInput(Input.ClickLeft, true);
                }
            }
        }

        // If the movement target has to force the new rotations, or we aren't using silent move, then force the rotations
        var rotation = _currentState.GetTarget().GetRotation();
        if (rotation != null)
        {
            Baritone.GetLookBehavior().UpdateTarget(
                rotation,
                _currentState.GetTarget().HasToForceRotations());
        }
        
        Baritone.GetInputOverrideHandler().ClearAllKeys();
        var inputStates = _currentState.GetInputStates().ToList();
        foreach (var kvp in inputStates)
        {
            Baritone.GetInputOverrideHandler().SetInputForceState(kvp.Key, kvp.Value);
        }
        _currentState.GetInputStates().Clear();
        
        // Reduced logging - removed verbose movement logging

        // If the current status indicates a completed movement
        if (_currentState.GetStatus().IsComplete())
        {
            Baritone.GetInputOverrideHandler().ClearAllKeys();
        }

        return _currentState.GetStatus();
    }

    protected virtual bool Prepared(MovementState state)
    {
        if (state.GetStatus() == MovementStatus.Waiting)
        {
            return true;
        }
        
        bool somethingInTheWay = false;
        foreach (var blockPos in PositionsToBreak)
        {
            // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/Movement.java:198-203
            // Check for falling blocks
            // TODO: Check for falling block entities when entity system is available
            var settings = Core.Baritone.Settings();
            if (settings.PauseMiningForFallingBlocks.Value)
            {
                // For now, skip falling block check
            }
            
            if (!MovementHelper.CanWalkThrough(Ctx, blockPos))
            {
                somethingInTheWay = true;
                var blockState = BlockStateInterface.Get(Ctx, blockPos);
                MovementHelper.SwitchToBestToolFor(Ctx, blockState);
                
                // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/Movement.java:211-222
                // RotationUtils.reachable
                var reachable = Utils.RotationUtils.Reachable(Ctx, blockPos, Ctx.PlayerController().GetBlockReachDistance());
                if (reachable != null)
                {
                    state.SetTarget(new MovementState.MovementTarget(reachable, true));
                    var playerRot = Ctx.PlayerRotations();
                    if (playerRot != null && (Ctx.IsLookingAt(blockPos) || playerRot.IsReallyCloseTo(reachable)))
                    {
                        state.SetInput(Input.ClickLeft, true);
                    }
                    return false;
                }
                
                // Fallback: try to break anyway
                // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/Movement.java:225-229
                // RotationUtils.calcRotationFromVec3d
                var playerHead = Ctx.PlayerHead();
                var playerRotations = Ctx.PlayerRotations();
                if (playerHead == null || playerRotations == null) return false;
                var rotation = Utils.RotationUtils.CalcRotationFromVec3d(
                    playerHead,
                    Utils.VecUtils.GetBlockPosCenter(blockPos),
                    playerRotations
                );
                state.SetTarget(new MovementState.MovementTarget(rotation, true));
                state.SetInput(Input.ClickLeft, true);
                return false;
            }
        }
        
        if (somethingInTheWay)
        {
            // There's a block or blocks that we can't walk through, but we have no target rotation to reach any
            state.SetStatus(MovementStatus.Unreachable);
            return true;
        }
        
        return true;
    }

    public bool SafeToCancel()
    {
        return SafeToCancel(_currentState);
    }

    protected virtual bool SafeToCancel(MovementState currentState)
    {
        return true;
    }

    public BetterBlockPos GetSrc() => Src;

    public BetterBlockPos GetDest() => Dest;

    public virtual void Reset()
    {
        _currentState = new MovementState().SetStatus(MovementStatus.Prepping);
    }

    /// <summary>
    /// Calculate latest movement state. Gets called once a tick.
    /// </summary>
    protected virtual MovementState UpdateState(MovementState state)
    {
        if (!Prepared(state))
        {
            return state.SetStatus(MovementStatus.Prepping);
        }
        else if (state.GetStatus() == MovementStatus.Prepping)
        {
            state.SetStatus(MovementStatus.Waiting);
        }

        if (state.GetStatus() == MovementStatus.Waiting)
        {
            state.SetStatus(MovementStatus.Running);
        }

        return state;
    }

    public (int X, int Y, int Z) GetDirection()
    {
        return (Dest.X - Src.X, Dest.Y - Src.Y, Dest.Z - Src.Z);
    }

    public void CheckLoadedChunk(CalculationContext context)
    {
        _calculatedWhileLoaded = context.Bsi.WorldContainsLoadedChunk(Dest.X, Dest.Z);
    }

    public bool CalculatedWhileLoaded()
    {
        return _calculatedWhileLoaded ?? false;
    }

    public void ResetBlockCache()
    {
        ToBreakCached = null;
        ToPlaceCached = null;
        ToWalkIntoCached = null;
    }

    public List<BetterBlockPos> ToBreak(BlockStateInterface bsi)
    {
        if (ToBreakCached != null)
        {
            return ToBreakCached;
        }
        var result = new List<BetterBlockPos>();
        foreach (var positionToBreak in PositionsToBreak)
        {
            if (!MovementHelper.CanWalkThrough(bsi, positionToBreak.X, positionToBreak.Y, positionToBreak.Z))
            {
                result.Add(positionToBreak);
            }
        }
        ToBreakCached = result;
        return result;
    }

    public List<BetterBlockPos> ToPlace(BlockStateInterface bsi)
    {
        if (ToPlaceCached != null)
        {
            return ToPlaceCached;
        }
        var result = new List<BetterBlockPos>();
        if (PositionToPlace != null && !MovementHelper.CanWalkOn(bsi, PositionToPlace.X, PositionToPlace.Y, PositionToPlace.Z))
        {
            result.Add(PositionToPlace);
        }
        ToPlaceCached = result;
        return result;
    }

    public virtual List<BetterBlockPos> ToWalkInto(BlockStateInterface bsi)
    {
        if (ToWalkIntoCached == null)
        {
            ToWalkIntoCached = new List<BetterBlockPos>();
        }
        return ToWalkIntoCached;
    }

    public BetterBlockPos[] ToBreakAll() => PositionsToBreak;
}

