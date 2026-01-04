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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/path/PathExecutor.java
 */

using MinecraftProtoNet.Baritone.Api;
using MinecraftProtoNet.Baritone.Api.Pathing.Calc;
using MinecraftProtoNet.Baritone.Api.Pathing.Movement;
using MinecraftProtoNet.Baritone.Api.Pathing.Path;
using MinecraftProtoNet.Baritone.Api.Utils;
using MinecraftProtoNet.Baritone.Behaviors;
using MinecraftProtoNet.Baritone.Pathfinding.Goals;
using MinecraftProtoNet.Baritone.Pathfinding.Movement;
using MinecraftProtoNet.Baritone.Pathfinding.Movement.Movements;
using MinecraftProtoNet.Baritone.Utils;
using MinecraftProtoNet.Core.State;
using MovementType = MinecraftProtoNet.Baritone.Pathfinding.Movement.Movement;
using BaritoneSettings = MinecraftProtoNet.Baritone.Core.Baritone;

namespace MinecraftProtoNet.Baritone.Pathfinding.Path;

/// <summary>
/// Behavior to execute a precomputed path.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/path/PathExecutor.java
/// </summary>
public class PathExecutor : IPathExecutor
{
    private const double MaxMaxDistFromPath = 3;
    private const double MaxDistFromPath = 2;

    /// <summary>
    /// Default value is equal to 10 seconds. It's fine to decrease it, but it must be at least 5.5s (110 ticks).
    /// </summary>
    private const double MaxTicksAway = 200;

    private readonly IPath _path;
    private int _pathPosition;
    private int _ticksAway;
    private int _ticksOnCurrent;
    private double? _currentMovementOriginalCostEstimate;
    private int? _costEstimateIndex;
    private bool _failed;
    private bool _recalcBP = true;
    private HashSet<(int X, int Y, int Z)> _toBreak = new();
    private HashSet<(int X, int Y, int Z)> _toPlace = new();
    private HashSet<(int X, int Y, int Z)> _toWalkInto = new();

    private readonly PathingBehavior _behavior;
    private readonly IPlayerContext _ctx;
    private IBaritone Baritone => _behavior.Baritone;

    private bool _sprintNextTick;

    public PathExecutor(PathingBehavior behavior, IPath path)
    {
        _behavior = behavior;
        _ctx = behavior.Ctx;
        _path = path;
        _pathPosition = 0;
    }

    /// <summary>
    /// Tick this executor.
    /// </summary>
    /// <returns>True if a movement just finished (and the player is therefore in a "stable" state), false otherwise.</returns>
    public bool OnTick()
    {
        if (_pathPosition == _path.Length() - 1)
        {
            _pathPosition++;
        }
        if (_pathPosition >= _path.Length())
        {
            return true; // stop bugging me, I'm done
        }
        var movement = (MovementType)_path.Movements()[_pathPosition];
        var whereAmI = _ctx.PlayerFeet();
        if (whereAmI == null)
        {
            return false;
        }

        if (!movement.GetValidPositions().Contains(whereAmI))
        {
            // Check if we're at a previous position (lag teleport)
            for (int i = 0; i < _pathPosition && i < _path.Length(); i++)
            {
                if (((MovementType)_path.Movements()[i]).GetValidPositions().Contains(whereAmI))
                {
                    int previousPos = _pathPosition;
                    _pathPosition = i;
                    for (int j = _pathPosition; j <= previousPos; j++)
                    {
                        _path.Movements()[j].Reset();
                    }
                    OnChangeInPathPosition();
                    OnTick();
                    return false;
                }
            }
            // Check if we're ahead (skip forward)
            for (int i = _pathPosition + 3; i < _path.Length() - 1; i++)
            {
                if (((MovementType)_path.Movements()[i]).GetValidPositions().Contains(whereAmI))
                {
                    if (i - _pathPosition > 2)
                    {
                        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathfinding/path/PathExecutor.java:116
                        if (Core.Baritone.Settings().DebugPathCompletion.Value)
                        {
                            Baritone.GetGameEventHandler().LogDirect($"Skipping forward {i - _pathPosition} steps, to {i}");
                        }
                    }
                    _pathPosition = i - 1;
                    OnChangeInPathPosition();
                    OnTick();
                    return false;
                }
            }
        }

        var status = ClosestPathPos(_path);
        if (PossiblyOffPath(status, MaxDistFromPath))
        {
            _ticksAway++;
            if (_ticksAway > MaxTicksAway)
            {
                // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathfinding/path/PathExecutor.java:132
                if (Core.Baritone.Settings().DebugPathCompletion.Value)
                {
                    Baritone.GetGameEventHandler().LogDirect("Too far away from path for too long, cancelling path");
                }
                Cancel();
                return false;
            }
        }
        else
        {
            _ticksAway = 0;
        }
        if (PossiblyOffPath(status, MaxMaxDistFromPath))
        {
            // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathfinding/path/PathExecutor.java:143
            if (Core.Baritone.Settings().DebugPathCompletion.Value)
            {
                Baritone.GetGameEventHandler().LogDirect("too far from path");
            }
            Cancel();
            return false;
        }

        // Recalculate break/place blocks
        var bsi = new BlockStateInterface(_ctx);
        for (int i = _pathPosition - 10; i < _pathPosition + 10; i++)
        {
            if (i < 0 || i >= _path.Movements().Count)
            {
                continue;
            }
            var m = (MovementType)_path.Movements()[i];
            var prevBreak = m.ToBreak(bsi);
            var prevPlace = m.ToPlace(bsi);
            var prevWalkInto = m.ToWalkInto(bsi);
            m.ResetBlockCache();
            if (!prevBreak.SequenceEqual(m.ToBreak(bsi)))
            {
                _recalcBP = true;
            }
            if (!prevPlace.SequenceEqual(m.ToPlace(bsi)))
            {
                _recalcBP = true;
            }
            if (!prevWalkInto.SequenceEqual(m.ToWalkInto(bsi)))
            {
                _recalcBP = true;
            }
        }
        if (_recalcBP)
        {
            var newBreak = new HashSet<(int X, int Y, int Z)>();
            var newPlace = new HashSet<(int X, int Y, int Z)>();
            var newWalkInto = new HashSet<(int X, int Y, int Z)>();
            for (int i = _pathPosition; i < _path.Movements().Count; i++)
            {
                var m = (MovementType)_path.Movements()[i];
                foreach (var pos in m.ToBreak(bsi))
                {
                    newBreak.Add((pos.X, pos.Y, pos.Z));
                }
                foreach (var pos in m.ToPlace(bsi))
                {
                    newPlace.Add((pos.X, pos.Y, pos.Z));
                }
                foreach (var pos in m.ToWalkInto(bsi))
                {
                    newWalkInto.Add((pos.X, pos.Y, pos.Z));
                }
            }
            _toBreak = newBreak;
            _toPlace = newPlace;
            _toWalkInto = newWalkInto;
            _recalcBP = false;
        }

        // Check if next movement is in loaded chunks
        if (_pathPosition < _path.Movements().Count - 1)
        {
            var next = _path.Movements()[_pathPosition + 1];
            if (!((Core.Baritone)_behavior.Baritone).Bsi!.WorldContainsLoadedChunk(next.GetDest().X, next.GetDest().Z))
            {
                // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathfinding/path/PathExecutor.java:207
                if (Core.Baritone.Settings().DebugPathCompletion.Value)
                {
                    Baritone.GetGameEventHandler().LogDirect("Pausing since destination is at edge of loaded chunks");
                }
                ClearKeys();
                return true;
            }
        }

        bool canCancel = movement.SafeToCancel();
        if (_costEstimateIndex == null || _costEstimateIndex != _pathPosition)
        {
            _costEstimateIndex = _pathPosition;
            _currentMovementOriginalCostEstimate = movement.GetCost();
            var context = _behavior.SecretInternalGetCalculationContext();
            for (int i = 1; i < BaritoneSettings.Settings().CostVerificationLookahead.Value && _pathPosition + i < _path.Length() - 1; i++)
            {
                var futureMovement = (MovementType)_path.Movements()[_pathPosition + i];
                if (context != null && futureMovement.CalculateCost(context) >= ActionCosts.CostInf && canCancel)
                {
                    // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathfinding/path/PathExecutor.java:224
                    if (Core.Baritone.Settings().DebugPathCompletion.Value)
                    {
                        Baritone.GetGameEventHandler().LogDirect("Something has changed in the world and a future movement has become impossible. Cancelling.");
                    }
                    Cancel();
                    return true;
                }
            }
        }

        var context2 = _behavior.SecretInternalGetCalculationContext();
        double currentCost = context2 != null ? movement.RecalculateCost(context2) : ActionCosts.CostInf;
        if (currentCost >= ActionCosts.CostInf && canCancel)
        {
            // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathfinding/path/PathExecutor.java:235
            if (Core.Baritone.Settings().DebugPathCompletion.Value)
            {
                Baritone.GetGameEventHandler().LogDirect("Something has changed in the world and this movement has become impossible. Cancelling.");
            }
            Cancel();
            return true;
        }
        if (!movement.CalculatedWhileLoaded() && _currentMovementOriginalCostEstimate.HasValue &&
            currentCost - _currentMovementOriginalCostEstimate.Value > BaritoneSettings.Settings().MaxCostIncrease.Value && canCancel)
        {
            // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathfinding/path/PathExecutor.java:242
            if (Core.Baritone.Settings().DebugPathCompletion.Value)
            {
                Baritone.GetGameEventHandler().LogDirect($"Original cost {_currentMovementOriginalCostEstimate} current cost {currentCost}. Cancelling.");
            }
            Cancel();
            return true;
        }
        if (ShouldPause())
        {
            // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathfinding/path/PathExecutor.java:248
            if (Core.Baritone.Settings().DebugPathCompletion.Value)
            {
                Baritone.GetGameEventHandler().LogDirect("Pausing since current best path is a backtrack");
            }
            ClearKeys();
            return true;
        }

        var movementStatus = movement.Update();
        if (movementStatus == MovementStatus.Unreachable || movementStatus == MovementStatus.Failed)
        {
            // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathfinding/path/PathExecutor.java:256
            if (Core.Baritone.Settings().DebugPathCompletion.Value)
            {
                Baritone.GetGameEventHandler().LogDirect($"Movement returns status {movementStatus}");
            }
            Cancel();
            return true;
        }
        if (movementStatus == MovementStatus.Success)
        {
            _pathPosition++;
            OnChangeInPathPosition();
            OnTick();
            return true;
        }
        else
        {
            _sprintNextTick = ShouldSprintNextTick();
            if (!_sprintNextTick)
            {
                // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathfinding/path/PathExecutor.java:272
                // Stop sprinting
                var player = _ctx.Player() as Entity;
                if (player != null)
                {
                    player.StopSprinting();
                }
            }
            _ticksOnCurrent++;
            if (_currentMovementOriginalCostEstimate.HasValue &&
                _ticksOnCurrent > _currentMovementOriginalCostEstimate.Value + BaritoneSettings.Settings().MovementTimeoutTicks.Value)
            {
                // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathfinding/path/PathExecutor.java:278
                if (Core.Baritone.Settings().DebugPathCompletion.Value)
                {
                    Baritone.GetGameEventHandler().LogDirect($"This movement has taken too long ({_ticksOnCurrent} ticks, expected {_currentMovementOriginalCostEstimate}). Cancelling.");
                }
                Cancel();
                return true;
            }
        }
        return canCancel;
    }

    private (double Distance, BetterBlockPos? Pos) ClosestPathPos(IPath path)
    {
        double best = -1;
        BetterBlockPos? bestPos = null;
        foreach (var movement in path.Movements())
        {
            foreach (var pos in ((MovementType)movement).GetValidPositions())
            {
                // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathfinding/path/PathExecutor.java:294
                // Calculate distance from player to position
                var player = _ctx.Player() as Entity;
                double dist = player != null ? VecUtils.EntityDistanceToCenter(player, pos) : pos.DistanceTo(_ctx.PlayerFeet() ?? new BetterBlockPos(0, 0, 0));
                if (dist < best || best == -1)
                {
                    best = dist;
                    bestPos = pos;
                }
            }
        }
        return (best, bestPos);
    }

    private bool ShouldPause()
    {
        var current = _behavior.GetInProgress();
        if (current == null)
        {
            return false;
        }
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathfinding/path/PathExecutor.java:313
        // Additional checks for onGround, canWalkOn, etc.
        var player = _ctx.Player() as Entity;
        if (player != null && !player.IsOnGround)
        {
            return false; // Must be on ground to splice
        }
        var currentBest = current.BestPathSoFar();
        if (currentBest == null)
        {
            return false;
        }
        var positions = currentBest.Positions();
        if (positions.Count < 3)
        {
            return false;
        }
        // Skip first position (overlap)
        var playerFeet = _ctx.PlayerFeet();
        if (playerFeet == null) return false;
        return positions.Skip(1).Any(p => p.Equals(playerFeet));
    }

    private bool PossiblyOffPath((double Distance, BetterBlockPos? Pos) status, double leniency)
    {
        double distanceFromPath = status.Distance;
        if (distanceFromPath > leniency)
        {
            // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathfinding/path/PathExecutor.java:336
            // When we're midair in the middle of a fall, we're very far from both the beginning and the end, but we aren't actually off path
            // Check for MovementFall type
            if (_pathPosition < _path.Movements().Count)
            {
                var movement = _path.Movements()[_pathPosition];
                if (movement is MovementFall)
                {
                    return false; // In a fall, being far from path is expected
                }
            }
            return true;
        }
        return false;
    }

    /// <summary>
    /// Regardless of current path position, snap to the current player feet if possible.
    /// </summary>
    public bool Snipsnapifpossible()
    {
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathfinding/path/PathExecutor.java:347
        // Additional safety checks
        var playerFeet = _ctx.PlayerFeet();
        if (playerFeet == null) return false;

        int index = _path.Positions().ToList().FindIndex(p => p.Equals(playerFeet));
        if (index == -1)
        {
            return false;
        }
        _pathPosition = index;
        ClearKeys();
        return true;
    }

    private bool ShouldSprintNextTick()
    {
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathfinding/path/PathExecutor.java:363
        // Complex sprint logic from Java implementation
        if (_pathPosition >= _path.Movements().Count)
        {
            return false;
        }
        var movement = _path.Movements()[_pathPosition];
        var context = new CalculationContext(Baritone);
        if (!context.CanSprint)
        {
            return false;
        }
        // Check if movement type benefits from sprinting
        // Most movements benefit from sprinting when not in water
        return true;
    }

    private void OnChangeInPathPosition()
    {
        ClearKeys();
        _ticksOnCurrent = 0;
    }

    private void ClearKeys()
    {
        _behavior.Baritone.GetInputOverrideHandler().ClearAllKeys();
    }

    private void Cancel()
    {
        ClearKeys();
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathfinding/path/PathExecutor.java:382
        // Stop breaking block
        Baritone.GetInputOverrideHandler().ClearAllKeys();
        _pathPosition = _path.Length() + 3;
        _failed = true;
    }

    public int GetPosition() => _pathPosition;

    public IPathExecutor? TrySplice(IPathExecutor? next)
    {
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathfinding/path/PathExecutor.java:391
        // Splicing logic - combine current path with next path
        // This is handled by PathingBehavior when paths are calculated
        return this;
    }

    public IPath GetPath() => _path;

    public bool Failed() => _failed;

    public bool Finished() => _pathPosition >= _path.Length();

    public IReadOnlySet<(int X, int Y, int Z)> ToBreak() => _toBreak;

    public IReadOnlySet<(int X, int Y, int Z)> ToPlace() => _toPlace;

    public IReadOnlySet<(int X, int Y, int Z)> ToWalkInto() => _toWalkInto;

    public bool IsSprinting() => _sprintNextTick;
}

