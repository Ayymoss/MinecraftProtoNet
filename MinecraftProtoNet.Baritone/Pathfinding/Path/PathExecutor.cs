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

using Microsoft.Extensions.Logging;
using MinecraftProtoNet.Baritone.Api;
using MinecraftProtoNet.Baritone.Api.Pathing.Calc;
using MinecraftProtoNet.Baritone.Api.Pathing.Movement;
using MinecraftProtoNet.Baritone.Api.Pathing.Path;
using MinecraftProtoNet.Baritone.Api.Utils;
using MinecraftProtoNet.Baritone.Behaviors;
using MinecraftProtoNet.Baritone.Pathfinding.Movement;
using MinecraftProtoNet.Baritone.Pathfinding.Movement.Movements;
using MinecraftProtoNet.Baritone.Utils;
using MinecraftProtoNet.Core.Models.Core;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.State;
using MovementType = MinecraftProtoNet.Baritone.Pathfinding.Movement.Movement;
using BaritoneSettings = MinecraftProtoNet.Baritone.Core.Baritone;

namespace MinecraftProtoNet.Baritone.Pathfinding.Path;

/// <summary>
/// Behavior to execute a precomputed path.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/path/PathExecutor.java
/// </summary>
public class PathExecutor(PathingBehavior behavior, IPath path) : IPathExecutor
{
    private static readonly ILogger _logger = LoggingConfiguration.CreateLogger("MinecraftProtoNet.Baritone.PathExecutor");

    private const double MaxMaxDistFromPath = 3;
    private const double MaxDistFromPath = 2;
    private const double MaxTicksAway = 200;

    private int _pathPosition = 0;
    private int _ticksAway;
    private int _ticksOnCurrent;
    private double? _currentMovementOriginalCostEstimate;
    private int? _costEstimateIndex;
    private bool _failed;
    private bool _recalcBP = true;
    private HashSet<(int X, int Y, int Z)> _toBreak = new();
    private HashSet<(int X, int Y, int Z)> _toPlace = new();
    private HashSet<(int X, int Y, int Z)> _toWalkInto = new();

    private readonly IPlayerContext _ctx = behavior.Ctx;
    private IBaritone Baritone => behavior.Baritone;

    private bool _sprintNextTick;

    /// <summary>
    /// Checks if the player's feet position is contained in a movement's valid positions,
    /// accounting for bottom slab offset. When standing on a bottom slab at block Y,
    /// the player's Y is Y+0.5 which floors to Y, but the path expects Y+1.
    /// </summary>
    private bool IsInValidPositions(HashSet<BetterBlockPos> validPositions, BetterBlockPos feet)
    {
        if (validPositions.Contains(feet)) return true;
        // Check slab-offset: player at (X, Y, Z) on a bottom slab should match (X, Y+1, Z)
        var blockAtFeet = BlockStateInterface.Get(_ctx, feet);
        if (blockAtFeet != null && MovementHelper.IsBottomSlab(blockAtFeet))
        {
            var slabAdjusted = new BetterBlockPos(feet.X, feet.Y + 1, feet.Z);
            return validPositions.Contains(slabAdjusted);
        }
        return false;
    }

    public bool OnTick()
    {
        var context = behavior.SecretInternalGetCalculationContext() ?? new CalculationContext(Baritone);
        bool canCancelResult = true;

        int iterations = 0;
        while (iterations++ < 100)
        {
            if (_pathPosition == path.Length() - 1) _pathPosition++;
            if (_pathPosition >= path.Length()) { ClearKeys(); return true; }

            var movement = (MovementType)path.Movements()[_pathPosition];
            var whereAmI = _ctx.PlayerFeet();
            if (whereAmI == null) return false;

            if (!IsInValidPositions(movement.GetValidPositions(), whereAmI))
            {
                _logger.LogDebug(
                    "[PathExec] NOT in valid pos! Feet=({FeetX},{FeetY},{FeetZ}) PathPos={PathPos} Move={Type} ({SrcX},{SrcY},{SrcZ})->({DestX},{DestY},{DestZ}) ValidPos=[{Valid}]",
                    whereAmI.X, whereAmI.Y, whereAmI.Z, _pathPosition, movement.GetType().Name,
                    movement.GetSrc().X, movement.GetSrc().Y, movement.GetSrc().Z,
                    movement.GetDest().X, movement.GetDest().Y, movement.GetDest().Z,
                    string.Join("; ", movement.GetValidPositions().Select(p => $"({p.X},{p.Y},{p.Z})")));

                bool foundLagPos = false;
                for (int i = _pathPosition - 1; i >= 0; i--)
                {
                    if (IsInValidPositions(((MovementType)path.Movements()[i]).GetValidPositions(), whereAmI))
                    {
                        int previousPos = _pathPosition;
                        _pathPosition = i;
                        for (int j = _pathPosition; j <= previousPos; j++) path.Movements()[j].Reset();
                        OnChangeInPathPosition();
                        _logger.LogDebug("[PathExec] Lag rewind: {PreviousPos} -> {NewPos}", previousPos, i);
                        Baritone.GetGameEventHandler().LogDirect($"Lag rewind: {previousPos} -> {i}");
                        foundLagPos = true;
                        break;
                    }
                }
                if (foundLagPos) continue;

                bool foundSkipPos = false;
                for (int i = _pathPosition + 3; i < path.Length() - 1; i++)
                {
                    if (IsInValidPositions(((MovementType)path.Movements()[i]).GetValidPositions(), whereAmI))
                    {
                        _logger.LogDebug("[PathExec] Skip forward: {OldPos} -> {NewPos}", _pathPosition, i - 1);
                        _pathPosition = i - 1;
                        OnChangeInPathPosition();
                        foundSkipPos = true;
                        break;
                    }
                }
                if (foundSkipPos) continue;

                _logger.LogDebug("[PathExec] No valid position found, executing movement anyway");
            }

            var status = ClosestPathPos(path);
            if (PossiblyOffPath(status, MaxDistFromPath))
            {
                _ticksAway++;
                if (_ticksAway > MaxTicksAway) { Cancel(); return false; }
            }
            else _ticksAway = 0;
            
            if (PossiblyOffPath(status, MaxMaxDistFromPath)) { Cancel(); return false; }

            var bsi = context.Bsi;
            for (int i = _pathPosition - 10; i < _pathPosition + 10; i++)
            {
                if (i < 0 || i >= path.Movements().Count) continue;
                var m = (MovementType)path.Movements()[i];
                var prevBreak = m.ToBreak(bsi);
                var prevPlace = m.ToPlace(bsi, context);
                var prevWalkInto = m.ToWalkInto(bsi);
                m.ResetBlockCache();
                if (!prevBreak.SequenceEqual(m.ToBreak(bsi)) || !prevPlace.SequenceEqual(m.ToPlace(bsi, context)) || !prevWalkInto.SequenceEqual(m.ToWalkInto(bsi)))
                {
                    _recalcBP = true;
                }
            }
            if (_recalcBP)
            {
                var newBreak = new HashSet<(int X, int Y, int Z)>();
                var newPlace = new HashSet<(int X, int Y, int Z)>();
                var newWalkInto = new HashSet<(int X, int Y, int Z)>();
                for (int i = _pathPosition; i < path.Movements().Count; i++)
                {
                    var m = (MovementType)path.Movements()[i];
                    foreach (var pos in m.ToBreak(bsi)) newBreak.Add((pos.X, pos.Y, pos.Z));
                    foreach (var pos in m.ToPlace(bsi, context)) newPlace.Add((pos.X, pos.Y, pos.Z));
                    foreach (var pos in m.ToWalkInto(bsi)) newWalkInto.Add((pos.X, pos.Y, pos.Z));
                }
                _toBreak = newBreak; _toPlace = newPlace; _toWalkInto = newWalkInto; _recalcBP = false;
            }

            bool canCancel = movement.SafeToCancel();
            if (_costEstimateIndex == null || _costEstimateIndex != _pathPosition)
            {
                _costEstimateIndex = _pathPosition;
                _currentMovementOriginalCostEstimate = movement.GetCost();
                for (int i = 1; i < BaritoneSettings.Settings().CostVerificationLookahead.Value && _pathPosition + i < path.Length() - 1; i++)
                {
                    var futureMovement = (MovementType)path.Movements()[_pathPosition + i];
                    if (futureMovement.CalculateCost(context) >= ActionCosts.CostInf && canCancel) { Cancel(); return true; }
                }
            }

            double currentCost = movement.RecalculateCost(context);
            if (currentCost >= ActionCosts.CostInf && canCancel) { Cancel(); return true; }
            if (ShouldPause()) { ClearKeys(); return true; }

            var movementStatus = movement.Update();
            if (movementStatus == MovementStatus.Unreachable || movementStatus == MovementStatus.Failed) { Cancel(); return true; }
            if (movementStatus == MovementStatus.Success)
            {
                _logger.LogDebug("[PathExec] Movement SUCCESS at pathPos={PathPos}, advancing", _pathPosition);
                _pathPosition++; OnChangeInPathPosition(); continue;
            }
            else
            {
                var skipResult = CheckSkip(context);
                if (skipResult == SkipResult.Skipped)
                {
                    _logger.LogDebug("[PathExec] CheckSkip=Skipped at pathPos={PathPos}", _pathPosition);
                    continue;
                }
                _sprintNextTick = (skipResult == SkipResult.Sprint);
                // Re-set sprint on InputOverrideHandler. CheckSkip always clears sprint (line 196)
                // then returns Sprint/NoSprint. We must propagate this back so
                // InputOverrideHandler.OnTick() applies the correct state to Entity.
                // This replaces Java's SprintStateEvent mechanism (MixinClientPlayerEntity.redirectSprintInput).
                behavior.Baritone.GetInputOverrideHandler().SetInputForceState(
                    Api.Utils.Input.Input.Sprint, _sprintNextTick);
                _ticksOnCurrent++;
                if (_currentMovementOriginalCostEstimate.HasValue && _ticksOnCurrent > _currentMovementOriginalCostEstimate.Value + BaritoneSettings.Settings().MovementTimeoutTicks.Value)
                {
                    _logger.LogWarning("[PathExec] Movement TIMEOUT at pathPos={PathPos} ticksOnCurrent={Ticks}", _pathPosition, _ticksOnCurrent);
                    Cancel(); return true;
                }
            }
            canCancelResult = canCancel; break;
        }
        return canCancelResult;
    }

    private enum SkipResult { NoSprint, Sprint, Skipped }

    private SkipResult CheckSkip(CalculationContext context)
    {
        bool requested = behavior.Baritone.GetInputOverrideHandler().IsInputForcedDown(Api.Utils.Input.Input.Sprint);
        behavior.Baritone.GetInputOverrideHandler().SetInputForceState(Api.Utils.Input.Input.Sprint, false);
        if (!context.CanSprint) return SkipResult.NoSprint;

        var current = path.Movements()[_pathPosition];
        if (current is MovementTraverse traverse && _pathPosition < path.Length() - 3)
        {
            var next = path.Movements()[_pathPosition + 1];
            if (next is MovementAscend ascend && SprintableAscend(traverse, ascend, path.Movements()[_pathPosition + 2], context))
            {
                if (SkipNow(current, context))
                {
                    _pathPosition++; OnChangeInPathPosition();
                    behavior.Baritone.GetInputOverrideHandler().SetInputForceState(Api.Utils.Input.Input.Jump, true);
                    // Sprint must also be active for the sprint-jump boost (0.2 horizontal velocity)
                    // Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/entity/LivingEntity.java:2229-2241
                    behavior.Baritone.GetInputOverrideHandler().SetInputForceState(Api.Utils.Input.Input.Sprint, true);
                    return SkipResult.Skipped;
                }
            }
        }

        if (requested) return SkipResult.Sprint;

        if (current is MovementDescend descend)
        {
            if (_pathPosition < path.Length() - 2)
            {
                var next = path.Movements()[_pathPosition + 1];
                if (MovementHelper.CanUseFrostWalker(context, next.GetDest().Below()))
                {
                    if (next is MovementTraverse || next is MovementParkour)
                    {
                        bool sameFlatDirection = current.GetDirection().X + next.GetDirection().X != 0 || current.GetDirection().Z + next.GetDirection().Z != 0;
                        if (sameFlatDirection && !context.HasThrowaway) descend.ForceSafeMode();
                    }
                }
            }
            if (descend.SafeMode() && !descend.SkipToAscend()) return SkipResult.NoSprint;

            if (_pathPosition < path.Length() - 2)
            {
                var next = path.Movements()[_pathPosition + 1];
                if (next is MovementAscend && current.GetDirection().X == next.GetDirection().X && current.GetDirection().Z == next.GetDirection().Z)
                {
                    _pathPosition++; OnChangeInPathPosition(); return SkipResult.Skipped;
                }
                if (CanSprintFromDescendInto(current, next, context))
                {
                    if (_ctx.PlayerFeet()?.Equals(current.GetDest()) == true)
                    {
                        _pathPosition++; OnChangeInPathPosition(); return SkipResult.Skipped;
                    }
                    return SkipResult.Sprint;
                }
            }
        }

        if (current is MovementAscend && _pathPosition != 0)
        {
            var prev = path.Movements()[_pathPosition - 1];
            if (prev is MovementDescend && prev.GetDirection().X == current.GetDirection().X && prev.GetDirection().Z == current.GetDirection().Z)
            {
                var center = current.GetSrc().Above();
                if (((Entity)_ctx.Player()!).Position.Y >= center.Y - 0.07)
                {
                    behavior.Baritone.GetInputOverrideHandler().SetInputForceState(Api.Utils.Input.Input.Jump, false);
                    return SkipResult.Sprint;
                }
            }
        }
        
        if (current is MovementFall fall)
        {
            var data = OverrideFall(fall, context);
            if (data != null)
            {
                if (_ctx.PlayerFeet()?.Equals(data.Value.Pos) == true)
                {
                    _pathPosition = path.Positions().ToList().IndexOf(data.Value.Pos);
                    OnChangeInPathPosition(); return SkipResult.Skipped;
                }
                ClearKeys();
                var playerHead = _ctx.PlayerHead(); var playerRot = _ctx.PlayerRotations();
                if (playerHead != null && playerRot != null)
                {
                    Baritone.GetLookBehavior().UpdateTarget(RotationUtils.CalcRotationFromVec3d(playerHead, data.Value.Vec, playerRot), false);
                }
                behavior.Baritone.GetInputOverrideHandler().SetInputForceState(Api.Utils.Input.Input.MoveForward, true);
                return SkipResult.Sprint;
            }
        }

        return SkipResult.NoSprint;
    }

    private (Vector3<double> Vec, BetterBlockPos Pos)? OverrideFall(MovementFall movement, CalculationContext context)
    {
        var dir = movement.GetDirection();
        if (dir.Y < -3) return null;
        if (movement.ToBreakAll().Any(p => !MovementHelper.CanWalkThrough(context, p.X, p.Y, p.Z))) return null;

        var flatDirX = dir.X; var flatDirZ = dir.Z; int i;
        for (i = _pathPosition + 1; i < path.Length() - 1 && i < _pathPosition + 3; i++)
        {
            var next = path.Movements()[i];
            if (next is not MovementTraverse || next.GetDirection().X != flatDirX || next.GetDirection().Z != flatDirZ) break;
            bool allPassable = true;
            for (int y = next.GetDest().Y; y <= movement.GetSrc().Y + 1; y++)
            {
                if (!MovementHelper.FullyPassable(context, next.GetDest().X, y, next.GetDest().Z)) { allPassable = false; break; }
            }
            if (!allPassable || !MovementHelper.CanWalkOn(context, next.GetDest().Below().X, next.GetDest().Below().Y, next.GetDest().Below().Z)) break;
        }
        i--; if (i == _pathPosition) return null;
        double len = i - _pathPosition - 0.4;
        var dest = movement.GetDest();
        return (new Vector3<double>(flatDirX * len + dest.X + 0.5, dest.Y, flatDirZ * len + dest.Z + 0.5),
                new BetterBlockPos(dest.X + (int)(flatDirX * (i - _pathPosition)), dest.Y, dest.Z + (int)(flatDirZ * (i - _pathPosition))));
    }

    private bool SkipNow(IMovement current, CalculationContext context)
    {
        var dir = current.GetDirection();
        var playerPos = ((Entity)_ctx.Player()!).Position;
        var src = current.GetSrc();
        double offTarget = Math.Abs(dir.X * (src.Z + 0.5 - playerPos.Z)) + Math.Abs(dir.Z * (src.X + 0.5 - playerPos.X));
        if (offTarget > 0.1) return false;
        var headBonk = new BetterBlockPos(src.X - dir.X, src.Y + 2, src.Z - dir.Z); 
        if (MovementHelper.FullyPassable(context, headBonk.X, headBonk.Y, headBonk.Z)) return true;
        double flatDist = Math.Abs(dir.X * (headBonk.X + 0.5 - playerPos.X)) + Math.Abs(dir.Z * (headBonk.Z + 0.5 - playerPos.Z));
        return flatDist > 0.8;
    }

    private bool SprintableAscend(MovementTraverse current, MovementAscend next, IMovement nextnext, CalculationContext context)
    {
        if (!BaritoneSettings.Settings().SprintAscends.Value) return false;
        if (current.GetDirection().X != next.GetDirection().X || current.GetDirection().Z != next.GetDirection().Z) return false;
        if (nextnext.GetDirection().X != next.GetDirection().X || nextnext.GetDirection().Z != next.GetDirection().Z) return false;
        if (!MovementHelper.CanWalkOn(context, current.GetDest().Below().X, current.GetDest().Below().Y, current.GetDest().Below().Z) || 
            !MovementHelper.CanWalkOn(context, next.GetDest().Below().X, next.GetDest().Below().Y, next.GetDest().Below().Z)) return false;
        if (next.ToBreakAll().Any(p => !MovementHelper.CanWalkThrough(context, p.X, p.Y, p.Z))) return false;
        for (int x = 0; x < 2; x++)
        {
            for (int y = 0; y < 3; y++)
            {
                var chk = current.GetSrc().Above(y);
                if (x == 1) chk = new BetterBlockPos(chk.X + current.GetDirection().X, chk.Y, chk.Z + current.GetDirection().Z);
                if (!MovementHelper.FullyPassable(context, chk.X, chk.Y, chk.Z)) return false;
            }
        }
        return !MovementHelper.AvoidWalkingInto(BlockStateInterface.Get(_ctx, current.GetSrc().Above(3))) &&
               !MovementHelper.AvoidWalkingInto(BlockStateInterface.Get(_ctx, next.GetDest().Above(2)));
    }

    private bool CanSprintFromDescendInto(IMovement current, IMovement next, CalculationContext context)
    {
        if (next is MovementDescend && next.GetDirection().Equals(current.GetDirection())) return true;
        if (!MovementHelper.CanWalkOn(context, current.GetDest().X + current.GetDirection().X, current.GetDest().Y, current.GetDest().Z + current.GetDirection().Z)) return false;
        if (next is MovementTraverse && next.GetDirection().Equals(current.GetDirection())) return true;
        return next is MovementDiagonal && BaritoneSettings.Settings().AllowOvershootDiagonalDescend.Value;
    }

    private void OnChangeInPathPosition() { ClearKeys(); _ticksOnCurrent = 0; }
    private void ClearKeys() { Baritone.GetInputOverrideHandler().ClearAllKeys(); }
    private void Cancel() { ClearKeys(); _pathPosition = path.Length() + 3; _failed = true; }

    private (double Distance, BetterBlockPos? Pos) ClosestPathPos(IPath path)
    {
        double best = -1; BetterBlockPos? bestPos = null;
        foreach (var movement in path.Movements())
        {
            foreach (var pos in ((MovementType)movement).GetValidPositions())
            {
                var player = _ctx.Player() as Entity;
                double dist = player != null ? VecUtils.EntityDistanceToCenter(player, pos) : pos.DistanceTo(_ctx.PlayerFeet() ?? new BetterBlockPos(0, 0, 0));
                if (dist < best || best == -1) { best = dist; bestPos = pos; }
            }
        }
        return (best, bestPos);
    }

    private bool ShouldPause()
    {
        var current = behavior.GetInProgress();
        if (current == null) return false;
        var player = _ctx.Player() as Entity;
        if (player != null && !player.IsOnGround) return false;
        var currentBest = current.BestPathSoFar();
        if (currentBest == null) return false;
        var positions = currentBest.Positions();
        if (positions.Count < 3) return false;
        var playerFeet = _ctx.PlayerFeet();
        if (playerFeet == null) return false;
        return positions.Skip(1).Any(p => p.Equals(playerFeet));
    }

    private bool PossiblyOffPath((double Distance, BetterBlockPos? Pos) status, double leniency)
    {
        if (status.Distance > leniency)
        {
            if (_pathPosition < path.Movements().Count && path.Movements()[_pathPosition] is MovementFall) return false;
            return true;
        }
        return false;
    }

    public bool Snipsnapifpossible()
    {
        var playerFeet = _ctx.PlayerFeet();
        if (playerFeet == null) return false;
        int index = path.Positions().ToList().FindIndex(p => p.Equals(playerFeet));
        if (index == -1) return false;
        _pathPosition = index; ClearKeys(); return true;
    }

    public int GetPosition() => _pathPosition;
    public IPathExecutor? TrySplice(IPathExecutor? next) => this;
    public IPath GetPath() => path;
    public bool Failed() => _failed;
    public bool Finished() => _pathPosition >= path.Length();
    public IReadOnlySet<(int X, int Y, int Z)> ToBreak() => _toBreak;
    public IReadOnlySet<(int X, int Y, int Z)> ToPlace() => _toPlace;
    public IReadOnlySet<(int X, int Y, int Z)> ToWalkInto() => _toWalkInto;
    public bool IsSprinting() => _sprintNextTick;
}
