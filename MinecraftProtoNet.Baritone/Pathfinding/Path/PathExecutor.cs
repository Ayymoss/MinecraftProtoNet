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
using MinecraftProtoNet.Core.Models.Core;
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

    public bool OnTick()
    {
        var context = _behavior.SecretInternalGetCalculationContext() ?? new CalculationContext(Baritone);
        bool canCancelResult = true;

        int iterations = 0;
        while (iterations++ < 100)
        {
            if (_pathPosition == _path.Length() - 1) _pathPosition++;
            if (_pathPosition >= _path.Length()) { ClearKeys(); return true; }

            var movement = (MovementType)_path.Movements()[_pathPosition];
            var whereAmI = _ctx.PlayerFeet();
            if (whereAmI == null) return false;

            if (!movement.GetValidPositions().Contains(whereAmI))
            {
                bool foundLagPos = false;
                for (int i = _pathPosition - 1; i >= 0; i--)
                {
                    if (((MovementType)_path.Movements()[i]).GetValidPositions().Contains(whereAmI))
                    {
                        int previousPos = _pathPosition;
                        _pathPosition = i;
                        for (int j = _pathPosition; j <= previousPos; j++) _path.Movements()[j].Reset();
                        OnChangeInPathPosition();
                        Baritone.GetGameEventHandler().LogDirect($"Lag rewind: {previousPos} -> {i}");
                        foundLagPos = true;
                        break;
                    }
                }
                if (foundLagPos) continue;

                bool foundSkipPos = false;
                for (int i = _pathPosition + 3; i < _path.Length() - 1; i++)
                {
                    if (((MovementType)_path.Movements()[i]).GetValidPositions().Contains(whereAmI))
                    {
                        _pathPosition = i - 1;
                        OnChangeInPathPosition();
                        foundSkipPos = true;
                        break;
                    }
                }
                if (foundSkipPos) continue;
            }

            var status = ClosestPathPos(_path);
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
                if (i < 0 || i >= _path.Movements().Count) continue;
                var m = (MovementType)_path.Movements()[i];
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
                for (int i = _pathPosition; i < _path.Movements().Count; i++)
                {
                    var m = (MovementType)_path.Movements()[i];
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
                for (int i = 1; i < BaritoneSettings.Settings().CostVerificationLookahead.Value && _pathPosition + i < _path.Length() - 1; i++)
                {
                    var futureMovement = (MovementType)_path.Movements()[_pathPosition + i];
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
                _pathPosition++; OnChangeInPathPosition(); continue;
            }
            else
            {
                var skipResult = CheckSkip(context);
                if (skipResult == SkipResult.Skipped) continue;
                _sprintNextTick = (skipResult == SkipResult.Sprint);
                if (!_sprintNextTick)
                {
                    var player = _ctx.Player() as Entity;
                    if (player != null) player.StopSprinting();
                }
                _ticksOnCurrent++;
                if (_currentMovementOriginalCostEstimate.HasValue && _ticksOnCurrent > _currentMovementOriginalCostEstimate.Value + BaritoneSettings.Settings().MovementTimeoutTicks.Value)
                {
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
        bool requested = _behavior.Baritone.GetInputOverrideHandler().IsInputForcedDown(Api.Utils.Input.Input.Sprint);
        _behavior.Baritone.GetInputOverrideHandler().SetInputForceState(Api.Utils.Input.Input.Sprint, false);
        if (!context.CanSprint) return SkipResult.NoSprint;

        var current = _path.Movements()[_pathPosition];
        if (current is MovementTraverse traverse && _pathPosition < _path.Length() - 3)
        {
            var next = _path.Movements()[_pathPosition + 1];
            if (next is MovementAscend ascend && SprintableAscend(traverse, ascend, _path.Movements()[_pathPosition + 2], context))
            {
                if (SkipNow(current, context))
                {
                    _pathPosition++; OnChangeInPathPosition();
                    _behavior.Baritone.GetInputOverrideHandler().SetInputForceState(Api.Utils.Input.Input.Jump, true);
                    return SkipResult.Skipped;
                }
            }
        }

        if (requested) return SkipResult.Sprint;

        if (current is MovementDescend descend)
        {
            if (_pathPosition < _path.Length() - 2)
            {
                var next = _path.Movements()[_pathPosition + 1];
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

            if (_pathPosition < _path.Length() - 2)
            {
                var next = _path.Movements()[_pathPosition + 1];
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
            var prev = _path.Movements()[_pathPosition - 1];
            if (prev is MovementDescend && prev.GetDirection().X == current.GetDirection().X && prev.GetDirection().Z == current.GetDirection().Z)
            {
                var center = current.GetSrc().Above();
                if (((Entity)_ctx.Player()!).Position.Y >= center.Y - 0.07)
                {
                    _behavior.Baritone.GetInputOverrideHandler().SetInputForceState(Api.Utils.Input.Input.Jump, false);
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
                    _pathPosition = _path.Positions().ToList().IndexOf(data.Value.Pos);
                    OnChangeInPathPosition(); return SkipResult.Skipped;
                }
                ClearKeys();
                var playerHead = _ctx.PlayerHead(); var playerRot = _ctx.PlayerRotations();
                if (playerHead != null && playerRot != null)
                {
                    Baritone.GetLookBehavior().UpdateTarget(RotationUtils.CalcRotationFromVec3d(playerHead, data.Value.Vec, playerRot), false);
                }
                _behavior.Baritone.GetInputOverrideHandler().SetInputForceState(Api.Utils.Input.Input.MoveForward, true);
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
        for (i = _pathPosition + 1; i < _path.Length() - 1 && i < _pathPosition + 3; i++)
        {
            var next = _path.Movements()[i];
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
    private void Cancel() { ClearKeys(); _pathPosition = _path.Length() + 3; _failed = true; }

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
        var current = _behavior.GetInProgress();
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
            if (_pathPosition < _path.Movements().Count && _path.Movements()[_pathPosition] is MovementFall) return false;
            return true;
        }
        return false;
    }

    public bool Snipsnapifpossible()
    {
        var playerFeet = _ctx.PlayerFeet();
        if (playerFeet == null) return false;
        int index = _path.Positions().ToList().FindIndex(p => p.Equals(playerFeet));
        if (index == -1) return false;
        _pathPosition = index; ClearKeys(); return true;
    }

    public int GetPosition() => _pathPosition;
    public IPathExecutor? TrySplice(IPathExecutor? next) => this;
    public IPath GetPath() => _path;
    public bool Failed() => _failed;
    public bool Finished() => _pathPosition >= _path.Length();
    public IReadOnlySet<(int X, int Y, int Z)> ToBreak() => _toBreak;
    public IReadOnlySet<(int X, int Y, int Z)> ToPlace() => _toPlace;
    public IReadOnlySet<(int X, int Y, int Z)> ToWalkInto() => _toWalkInto;
    public bool IsSprinting() => _sprintNextTick;
}
