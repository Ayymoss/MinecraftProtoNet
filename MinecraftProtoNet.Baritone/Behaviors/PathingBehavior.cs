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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/behavior/PathingBehavior.java
 */

using System.Collections.Concurrent;
using MinecraftProtoNet.Baritone.Api;
using MinecraftProtoNet.Baritone.Api.Behavior;
using MinecraftProtoNet.Baritone.Api.Event.Events;
using MinecraftProtoNet.Baritone.Api.Pathing.Calc;
using MinecraftProtoNet.Baritone.Api.Pathing.Goals;
using MinecraftProtoNet.Baritone.Api.Pathing.Movement;
using MinecraftProtoNet.Baritone.Api.Pathing.Path;
using MinecraftProtoNet.Baritone.Api.Process;
using MinecraftProtoNet.Baritone.Api.Utils;
using MinecraftProtoNet.Baritone.Core;
using MinecraftProtoNet.Baritone.Pathfinding.Calc;
using MinecraftProtoNet.Baritone.Pathfinding.Goals;
using MinecraftProtoNet.Baritone.Pathfinding.Movement;
using MinecraftProtoNet.Baritone.Pathfinding.Path;
using MinecraftProtoNet.Baritone.Utils;
using MinecraftProtoNet.Baritone.Utils.Pathing;
using MinecraftProtoNet.Core.State;
using BaritonePath = MinecraftProtoNet.Baritone.Pathfinding.Calc.Path;

namespace MinecraftProtoNet.Baritone.Behaviors;

/// <summary>
/// Pathing behavior implementation.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/behavior/PathingBehavior.java
/// </summary>
public sealed class PathingBehavior : Behavior, IPathingBehavior
{
    private PathExecutor? _current;
    private PathExecutor? _next;

    private Goal? _goal;
    private CalculationContext? _context;

    /*eta*/
    private int _ticksElapsedSoFar;
    private BetterBlockPos? _startPosition;

    private bool _safeToCancel;
    private bool _pauseRequestedLastTick;
    private bool _unpausedLastTick;
    private bool _pausedThisTick;
    private bool _cancelRequested;
    private bool _calcFailedLastTick;

    private volatile AbstractNodeCostSearch? _inProgress;
    private readonly object _pathCalcLock = new();
    private readonly object _pathPlanLock = new();

    private BetterBlockPos? _expectedSegmentStart;

    private readonly ConcurrentQueue<PathEvent> _toDispatch = new();

    public PathingBehavior(IBaritone baritone) : base(baritone)
    {
    }

    private void QueuePathEvent(PathEvent evt)
    {
        _toDispatch.Enqueue(evt);
    }

    private void DispatchEvents()
    {
        var curr = new List<PathEvent>();
        while (_toDispatch.TryDequeue(out var evt))
        {
            curr.Add(evt);
        }
        _calcFailedLastTick = curr.Contains(PathEvent.CalcFailed);
        foreach (var evt in curr)
        {
            Baritone.GetGameEventHandler().OnPathEvent(evt);
        }
    }

    public override void OnTick(TickEvent evt)
    {
        DispatchEvents();
        if (evt.GetType() == TickEvent.TickEventType.Out)
        {
            SecretInternalSegmentCancel();
            if (Baritone.GetPathingControlManager() is Utils.PathingControlManager pcmOut)
            {
                pcmOut.CancelEverything();
            }
            return;
        }

        _expectedSegmentStart = PathStart();
        if (Baritone.GetPathingControlManager() is Utils.PathingControlManager pcmIn)
        {
            pcmIn.PreTick();
        }
        TickPath();
        _ticksElapsedSoFar++;
        DispatchEvents();
    }

    public override void OnPlayerSprintState(SprintStateEvent evt)
    {
        if (IsPathing())
        {
            evt.SetState(_current?.IsSprinting() ?? false);
        }
    }

    private void TickPath()
    {
        _pausedThisTick = false;
        if (_pauseRequestedLastTick && _safeToCancel)
        {
            _pauseRequestedLastTick = false;
            if (_unpausedLastTick)
            {
                Baritone.GetInputOverrideHandler().ClearAllKeys();
                // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/behavior/PathingBehavior.java:131
                // Stop breaking block
                Baritone.GetInputOverrideHandler().ClearAllKeys();
            }
            _unpausedLastTick = false;
            _pausedThisTick = true;
            return;
        }
        _unpausedLastTick = true;
        if (_cancelRequested)
        {
            _cancelRequested = false;
            Baritone.GetInputOverrideHandler().ClearAllKeys();
        }
        lock (_pathPlanLock)
        {
            lock (_pathCalcLock)
            {
                if (_inProgress != null)
                {
                    var calcFrom = _inProgress.GetStart();
                    var currentBest = _inProgress.BestPathSoFar();
                    var playerFeet = Ctx.PlayerFeet();
                    if ((_current == null || !_current.GetPath().GetDest().Equals(calcFrom))
                        && !calcFrom.Equals(playerFeet) && !calcFrom.Equals(_expectedSegmentStart)
                        && (currentBest == null || (!currentBest.Positions().Contains(playerFeet ?? new BetterBlockPos(0, 0, 0)) && !currentBest.Positions().Contains(_expectedSegmentStart ?? new BetterBlockPos(0, 0, 0)))))
                    {
                        _inProgress.Cancel();
                    }
                }
            }
            if (_current == null)
            {
                return;
            }
            _safeToCancel = _current.OnTick();
            if (_current.Failed() || _current.Finished())
            {
                // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/behavior/PathingBehavior.java:172-186
                // Clear input overrides when path finishes to stop movement
                Baritone.GetInputOverrideHandler().ClearAllKeys();
                _current = null;
                if (_goal == null || (_goal.IsInGoal(Ctx.PlayerFeet() ?? new BetterBlockPos(0, 0, 0))))
                {
                    // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/behavior/PathingBehavior.java:170
                    // Debug logging
                    if (Core.Baritone.Settings().DebugPathCompletion.Value)
                    {
                        Baritone.GetGameEventHandler().LogDirect($"All done. At {_goal}");
                    }
                    QueuePathEvent(PathEvent.AtGoal);
                    _next = null;
                    return;
                }
                if (_next != null && !_next.GetPath().Positions().Contains(Ctx.PlayerFeet() ?? new BetterBlockPos(0, 0, 0)) && !_next.GetPath().Positions().Contains(_expectedSegmentStart ?? new BetterBlockPos(0, 0, 0)))
                {
                    // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/behavior/PathingBehavior.java:177
                    if (Core.Baritone.Settings().DebugPathCompletion.Value)
                    {
                        Baritone.GetGameEventHandler().LogDirect("Discarding next path as it does not contain current position");
                    }
                    QueuePathEvent(PathEvent.DiscardNext);
                    _next = null;
                }
                if (_next != null)
                {
                    // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/behavior/PathingBehavior.java:183
                    if (Core.Baritone.Settings().DebugPathCompletion.Value)
                    {
                        Baritone.GetGameEventHandler().LogDirect("Continuing on to planned next path");
                    }
                    QueuePathEvent(PathEvent.ContinuingOntoPlannedNext);
                    _current = _next;
                    _next = null;
                    _current.OnTick();
                    return;
                }
                lock (_pathCalcLock)
                {
                    if (_inProgress != null)
                    {
                        QueuePathEvent(PathEvent.PathFinishedNextStillCalculating);
                        return;
                    }
                    QueuePathEvent(PathEvent.CalcStarted);
                    FindPathInNewThread(_expectedSegmentStart ?? new BetterBlockPos(0, 0, 0), true, _context!);
                }
                return;
            }
            if (_safeToCancel && _next != null && _next.Snipsnapifpossible())
            {
                // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/behavior/PathingBehavior.java:204
                if (Core.Baritone.Settings().DebugPathCompletion.Value)
                {
                    Baritone.GetGameEventHandler().LogDirect("Splicing into planned next path early...");
                }
                QueuePathEvent(PathEvent.SplicingOntoNextEarly);
                _current = _next;
                _next = null;
                _current.OnTick();
                return;
            }
            if (Core.Baritone.Settings().SplicePath.Value)
            {
                _current = (PathExecutor?)_current?.TrySplice(_next);
            }
            if (_next != null && _current?.GetPath().GetDest().Equals(_next.GetPath().GetDest()) == true)
            {
                _next = null;
            }
            lock (_pathCalcLock)
            {
                if (_inProgress != null)
                {
                    return;
                }
                if (_next != null)
                {
                    return;
                }
                if (_goal == null || _goal.IsInGoal(_current?.GetPath().GetDest() ?? new BetterBlockPos(0, 0, 0)))
                {
                    return;
                }
                if (TicksRemainingInSegment(false) < Core.Baritone.Settings().PlanningTickLookahead.Value)
                {
                    // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/behavior/PathingBehavior.java:235
                    if (Core.Baritone.Settings().DebugPathCompletion.Value)
                    {
                        Baritone.GetGameEventHandler().LogDirect("Path almost over. Planning ahead...");
                    }
                    QueuePathEvent(PathEvent.NextSegmentCalcStarted);
                    FindPathInNewThread(_current!.GetPath().GetDest(), false, _context!);
                }
            }
        }
    }

    public void SecretInternalSetGoal(Goal? goal)
    {
        _goal = goal;
    }

    public bool SecretInternalSetGoalAndPath(PathingCommand command)
    {
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/behavior/PathingBehavior.java:262-268
        SecretInternalSetGoal(command.Goal!); // Null-forgiving: command.Goal can be null but method handles it
        if (command is PathingCommandContext commandContext)
        {
            _context = commandContext.DesiredCalcContext;
        }
        else
        {
            _context = new CalculationContext(Baritone, true);
        }
        if (_goal == null)
        {
            Baritone.GetGameEventHandler().LogDirect("SecretInternalSetGoalAndPath: Goal is null, cannot start pathfinding");
            return false;
        }
        var playerFeet = Ctx.PlayerFeet() ?? new BetterBlockPos(0, 0, 0);
        if (_goal.IsInGoal(playerFeet))
        {
            Baritone.GetGameEventHandler().LogDirect($"SecretInternalSetGoalAndPath: Already at goal {_goal}");
            return false;
        }
        lock (_pathPlanLock)
        {
            // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/behavior/PathingBehavior.java:262-335
            // If there's a current path, check if we need to cancel it
            if (_current != null)
            {
                var currentPath = _current.GetPath();
                var currentGoal = currentPath.GetGoal();
                // If the goal is different, cancel the current path
                if (currentGoal == null || !_goal.Equals(currentGoal) || !_goal.IsInGoal(currentPath.GetDest()))
                {
                    if (IsSafeToCancel())
                    {
                        Baritone.GetGameEventHandler().LogDirect($"SecretInternalSetGoalAndPath: Cancelling current path (goal changed from {currentGoal} to {_goal})");
                        // Cancel inline to avoid deadlock (we're already holding _pathPlanLock)
                        QueuePathEvent(PathEvent.Canceled);
                        if (_inProgress != null)
                        {
                            _inProgress.Cancel();
                        }
                        _current = null;
                        _next = null;
                        Baritone.GetInputOverrideHandler().ClearAllKeys();
                    }
                    else
                    {
                        Baritone.GetGameEventHandler().LogDirect("SecretInternalSetGoalAndPath: Already executing a path and cannot cancel safely");
                        return false;
                    }
                }
                else
                {
                    // Same goal, no need to start a new path
                    Baritone.GetGameEventHandler().LogDirect("SecretInternalSetGoalAndPath: Already pathing to the same goal");
                    return false;
                }
            }
            lock (_pathCalcLock)
            {
                if (_inProgress != null)
                {
                    Baritone.GetGameEventHandler().LogDirect("SecretInternalSetGoalAndPath: Path calculation already in progress");
                    return false;
                }
                var start = _expectedSegmentStart ?? playerFeet;
                Baritone.GetGameEventHandler().LogDirect($"SecretInternalSetGoalAndPath: Starting pathfinding from {start} to {_goal}");
                QueuePathEvent(PathEvent.CalcStarted);
                FindPathInNewThread(start, true, _context);
                return true;
            }
        }
    }

    public Goal? GetGoal() => _goal;

    public bool IsPathing() => HasPath() && !_pausedThisTick;

    public IPathExecutor? GetCurrent() => _current;

    public IPathExecutor? GetNext() => _next;

    public IPathFinder? GetInProgress()
    {
        lock (_pathCalcLock)
        {
            return _inProgress;
        }
    }

    public bool IsSafeToCancel()
    {
        if (_current == null)
        {
            // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/behavior/PathingBehavior.java:322
            // Check if elytra process is active - if so, don't cancel
            var elytraProcess = Baritone.GetElytraProcess();
            if (elytraProcess != null && elytraProcess.IsActive())
            {
                return false; // Don't cancel if elytra is active
            }
            return true;
        }
        return _safeToCancel;
    }

    public void RequestPause()
    {
        _pauseRequestedLastTick = true;
    }

    public bool CancelSegmentIfSafe()
    {
        if (IsSafeToCancel())
        {
            SecretInternalSegmentCancel();
            return true;
        }
        return false;
    }

    public bool CancelEverything()
    {
        bool doIt = IsSafeToCancel();
        if (doIt)
        {
            SecretInternalSegmentCancel();
        }
        Baritone.GetPathingControlManager().CancelEverything();
        return doIt;
    }

    public bool CalcFailedLastTick() => _calcFailedLastTick;

    public void SoftCancelIfSafe()
    {
        lock (_pathPlanLock)
        {
            if (_inProgress != null)
            {
                _inProgress.Cancel();
            }
            if (!IsSafeToCancel())
            {
                return;
            }
            _current = null;
            _next = null;
        }
        _cancelRequested = true;
    }

    public void SecretInternalSegmentCancel()
    {
        QueuePathEvent(PathEvent.Canceled);
        lock (_pathPlanLock)
        {
            if (_inProgress != null)
            {
                _inProgress.Cancel();
            }
            if (_current != null)
            {
                _current = null;
                _next = null;
                Baritone.GetInputOverrideHandler().ClearAllKeys();
            }
        }
    }

    public void ForceCancel()
    {
        CancelEverything();
        SecretInternalSegmentCancel();
        lock (_pathCalcLock)
        {
            _inProgress = null;
        }
    }

    public void ForceRevalidateGoalAndPath()
    {
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/utils/PathingControlManager.java:138-144
        if (_goal == null || ForceRevalidate(_goal) || RevalidateGoal(_goal))
        {
            SoftCancelIfSafe();
        }
        if (_goal != null)
        {
            var command = new PathingCommand(_goal, PathingCommandType.SetGoalAndPath);
            SecretInternalSetGoalAndPath(command);
        }
    }

    public void RevalidateGoalAndPath()
    {
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/utils/PathingControlManager.java:145-150
        if (Core.Baritone.Settings().CancelOnGoalInvalidation.Value && (_goal == null || RevalidateGoal(_goal)))
        {
            SoftCancelIfSafe();
        }
        if (_goal != null)
        {
            var command = new PathingCommand(_goal, PathingCommandType.SetGoalAndPath);
            SecretInternalSetGoalAndPath(command);
        }
    }

    private bool ForceRevalidate(Goal newGoal)
    {
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/utils/PathingControlManager.java:155-164
        var current = GetCurrent();
        if (current != null)
        {
            var path = current.GetPath();
            if (path != null && newGoal.IsInGoal(path.GetDest()))
            {
                return false;
            }
            return !newGoal.Equals(path?.GetGoal());
        }
        return false;
    }

    private bool RevalidateGoal(Goal newGoal)
    {
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/utils/PathingControlManager.java:166-175
        var current = GetCurrent();
        if (current != null)
        {
            var path = current.GetPath();
            if (path != null && newGoal.IsInGoal(path.GetDest()))
            {
                return false;
            }
            var currentGoal = path?.GetGoal();
            if (currentGoal != null && !newGoal.Equals(currentGoal))
            {
                return true;
            }
        }
        return false;
    }

    public CalculationContext? SecretInternalGetCalculationContext() => _context;

    public double? EstimatedTicksToGoal()
    {
        var currentPos = Ctx.PlayerFeet();
        if (_goal == null || currentPos == null || _startPosition == null)
        {
            return null;
        }
        if (_goal.IsInGoal(currentPos))
        {
            ResetEstimatedTicksToGoal();
            return 0.0;
        }
        if (_ticksElapsedSoFar == 0)
        {
            return null;
        }
        double current = _goal.Heuristic(currentPos.X, currentPos.Y, currentPos.Z);
        double start = _goal.Heuristic(_startPosition.X, _startPosition.Y, _startPosition.Z);
        if (current == start)
        {
            return null;
        }
        double eta = Math.Abs(current - _goal.Heuristic()) * _ticksElapsedSoFar / Math.Abs(start - current);
        return eta;
    }

    private void ResetEstimatedTicksToGoal()
    {
        ResetEstimatedTicksToGoal(_expectedSegmentStart ?? new BetterBlockPos(0, 0, 0));
    }

    private void ResetEstimatedTicksToGoal(BetterBlockPos start)
    {
        _ticksElapsedSoFar = 0;
        _startPosition = start;
    }

    public BetterBlockPos PathStart()
    {
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/behavior/PathingBehavior.java:427-465
        var playerFeet = Ctx.PlayerFeet();
        if (playerFeet == null) return new BetterBlockPos(0, 0, 0);

        var player = Ctx.Player() as Entity;
        if (player == null) return playerFeet;

        // See issue #209 - handle cases where player is not standing on a block
        if (!MovementHelper.CanWalkOn(Ctx, playerFeet.Below()))
        {
            if (player.IsOnGround)
            {
                // Player might be sneaking off the edge of a block
                double playerX = player.Position.X;
                double playerZ = player.Position.Z;
                var closest = new List<BetterBlockPos>();
                
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        closest.Add(new BetterBlockPos(playerFeet.X + dx, playerFeet.Y, playerFeet.Z + dz));
                    }
                }
                
                // Sort by distance to player
                closest.Sort((a, b) =>
                {
                    double distA = Math.Pow((a.X + 0.5) - playerX, 2) + Math.Pow((a.Z + 0.5) - playerZ, 2);
                    double distB = Math.Pow((b.X + 0.5) - playerX, 2) + Math.Pow((b.Z + 0.5) - playerZ, 2);
                    return distA.CompareTo(distB);
                });
                
                for (int i = 0; i < Math.Min(4, closest.Count); i++)
                {
                    var possibleSupport = closest[i];
                    double xDist = Math.Abs((possibleSupport.X + 0.5) - playerX);
                    double zDist = Math.Abs((possibleSupport.Z + 0.5) - playerZ);
                    if (xDist > 0.8 && zDist > 0.8)
                    {
                        // Can't possibly be sneaking off of this one, we're too far away
                        continue;
                    }
                    if (MovementHelper.CanWalkOn(Ctx, possibleSupport.Below()) &&
                        MovementHelper.CanWalkThrough(Ctx, possibleSupport) &&
                        MovementHelper.CanWalkThrough(Ctx, possibleSupport.Above()))
                    {
                        // This is plausible
                        return possibleSupport;
                    }
                }
            }
            else
            {
                // !onGround - we're in the middle of a jump
                if (MovementHelper.CanWalkOn(Ctx, playerFeet.Below().Below()))
                {
                    return playerFeet.Below();
                }
            }
        }
        return playerFeet;
    }

    private void FindPathInNewThread(BetterBlockPos start, bool talkAboutIt, CalculationContext context)
    {
        lock (_pathCalcLock)
        {
            if (_inProgress != null)
            {
                throw new InvalidOperationException("Already doing it");
            }
            if (!context.SafeForThreadedUse)
            {
                throw new InvalidOperationException("Improper context thread safety level");
            }
            var goal = _goal;
            if (goal == null)
            {
                return;
            }
            long primaryTimeout;
            long failureTimeout;
            if (_current == null)
            {
                primaryTimeout = Core.Baritone.Settings().PrimaryTimeoutMs.Value;
                failureTimeout = Core.Baritone.Settings().FailureTimeoutMs.Value;
            }
            else
            {
                primaryTimeout = Core.Baritone.Settings().PlanAheadPrimaryTimeoutMs.Value;
                failureTimeout = Core.Baritone.Settings().PlanAheadFailureTimeoutMs.Value;
            }
            var pathfinder = CreatePathfinder(start, goal, _current?.GetPath(), context);
            _inProgress = pathfinder;
            Task.Run(() =>
            {
                if (talkAboutIt)
                {
                    // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/behavior/PathingBehavior.java:463
                    if (Core.Baritone.Settings().DebugPathCompletion.Value)
                    {
                        Baritone.GetGameEventHandler().LogDirect($"Starting to search for path from {start} to {_goal}");
                    }
                }

                var calcResult = pathfinder.Calculate(primaryTimeout, failureTimeout);
                lock (_pathPlanLock)
                {
                    var executor = calcResult.GetPath() != null ? new PathExecutor(this, calcResult.GetPath()!) : null;
                    if (_current == null)
                    {
                        if (executor != null)
                        {
                            if (executor.GetPath().Positions().Contains(_expectedSegmentStart ?? new BetterBlockPos(0, 0, 0)))
                            {
                                QueuePathEvent(PathEvent.CalcFinishedNowExecuting);
                                _current = executor;
                                ResetEstimatedTicksToGoal(start);
                            }
                            else
                            {
                                // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/behavior/PathingBehavior.java:482
                                if (Core.Baritone.Settings().DebugPathCompletion.Value)
                                {
                                    Baritone.GetGameEventHandler().LogDirect("Warning: discarding orphan path segment with incorrect start");
                                }
                            }
                        }
                        else
                        {
                            if (calcResult.GetType() != PathCalculationResult.PathCalculationResultType.Cancellation && calcResult.GetType() != PathCalculationResult.PathCalculationResultType.Exception)
                            {
                                QueuePathEvent(PathEvent.CalcFailed);
                            }
                        }
                    }
                    else
                    {
                        if (_next == null)
                        {
                            if (executor != null)
                            {
                                if (executor.GetPath().GetSrc().Equals(_current.GetPath().GetDest()))
                                {
                                    QueuePathEvent(PathEvent.NextSegmentCalcFinished);
                                    _next = executor;
                                }
                                else
                                {
                                    // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/behavior/PathingBehavior.java:506
                                    if (Core.Baritone.Settings().DebugPathCompletion.Value)
                                    {
                                        Baritone.GetGameEventHandler().LogDirect("Warning: discarding orphan next segment with incorrect start");
                                    }
                                }
                            }
                            else
                            {
                                QueuePathEvent(PathEvent.NextCalcFailed);
                            }
                        }
                    }
                    lock (_pathCalcLock)
                    {
                        _inProgress = null;
                    }
                }
            });
        }
    }

    private AbstractNodeCostSearch CreatePathfinder(BetterBlockPos start, Goal goal, IPath? previous, CalculationContext context)
    {
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/behavior/PathingBehavior.java:560-577
        Goal transformed = goal;
        if (Core.Baritone.Settings().SimplifyUnloadedYCoord.Value)
        {
            // Check if goal has a specific position and simplify if unloaded
            // In Java, this checks for IGoalRenderPos interface
            // For C#, we'll check if goal is GoalBlock or GoalGetToBlock which have positions
            if (goal is GoalBlock goalBlock)
            {
                var pos = goalBlock.GetGoalPos();
                if (!context.IsLoaded(pos.X, pos.Z))
                {
                    // Simplify to GoalXZ if chunk is not loaded
                    transformed = new GoalXZ(pos.X, pos.Z);
                }
            }
            else if (goal is GoalGetToBlock goalGetToBlock)
            {
                var pos = goalGetToBlock.GetGoalPos();
                if (!context.IsLoaded(pos.X, pos.Z))
                {
                    // Simplify to GoalXZ if chunk is not loaded
                    transformed = new GoalXZ(pos.X, pos.Z);
                }
            }
        }
        var favoring = new Favoring(context.GetBaritone().GetPlayerContext(), previous, context);
        var feet = Ctx.PlayerFeet();
        var realStart = start;
        if (feet != null && feet.Y == realStart.Y && Math.Abs(feet.X - realStart.X) <= 1 && Math.Abs(feet.Z - realStart.Z) <= 1)
        {
            realStart = feet;
        }
        return new AStarPathFinder(realStart, start.X, start.Y, start.Z, transformed, favoring, context);
    }

    public double? TicksRemainingInSegment(bool includeCurrentMovement = true)
    {
        // Reference: baritone-1.21.11-REFERENCE-ONLY - Calculate remaining ticks in current path segment
        if (_current == null)
        {
            return null;
        }
        
        var path = _current.GetPath();
        if (path == null)
        {
            return null;
        }
        
        // Use path's TicksRemainingFrom method if available
        int currentPosition = _current.GetPosition();
        if (path is BaritonePath pathImpl)
        {
            int pathStartIndex = includeCurrentMovement ? currentPosition : currentPosition + 1;
            if (pathStartIndex < path.Movements().Count)
            {
                return pathImpl.TicksRemainingFrom(pathStartIndex);
            }
        }
        
        // Fallback: calculate manually
        double remainingTicks = 0.0;
        var movements = path.Movements();
        int fallbackStartIndex = includeCurrentMovement ? currentPosition : currentPosition + 1;
        
        for (int i = fallbackStartIndex; i < movements.Count; i++)
        {
            var movement = movements[i];
            double cost = movement.GetCost();
            if (cost < Api.Pathing.Movement.ActionCosts.CostInf)
            {
                remainingTicks += cost;
            }
        }
        
        return remainingTicks > 0 ? remainingTicks : null;
    }

    public bool HasPath() => _current != null;

    public IPath? GetPath() => _current?.GetPath();
}
