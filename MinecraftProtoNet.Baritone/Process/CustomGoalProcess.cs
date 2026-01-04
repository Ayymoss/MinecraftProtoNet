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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/process/CustomGoalProcess.java
 */

using MinecraftProtoNet.Baritone.Api;
using MinecraftProtoNet.Baritone.Api.Pathing.Goals;
using MinecraftProtoNet.Baritone.Api.Process;
using MinecraftProtoNet.Baritone.Utils;

namespace MinecraftProtoNet.Baritone.Process;

/// <summary>
/// Custom goal process implementation.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/process/CustomGoalProcess.java
/// </summary>
public class CustomGoalProcess : BaritoneProcessHelper, ICustomGoalProcess
{
    private Goal? _goal;
    private Goal? _mostRecentGoal;
    private State _state = State.None;

    public CustomGoalProcess(IBaritone baritone) : base(baritone)
    {
    }

    public void SetGoal(Goal? goal)
    {
        _goal = goal;
        _mostRecentGoal = goal;
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/process/CustomGoalProcess.java:47
        if (goal != null && Baritone.GetElytraProcess().IsActive())
        {
            // If elytra is active, use elytra process to path to goal
            // TODO: When elytra pathTo method is available, use: Baritone.GetElytraProcess().PathTo(goal);
            // For now, elytra process is a null implementation for headless clients
        }
        if (_state == State.None)
        {
            _state = State.GoalSet;
        }
        if (_state == State.Executing)
        {
            _state = State.PathRequested;
        }
    }

    public void Path()
    {
        _state = State.PathRequested;
    }

    public Goal? GetGoal() => _goal;

    public Goal? MostRecentGoal() => _mostRecentGoal;

    public override bool IsActive() => _state != State.None;

    public override PathingCommand OnTick(bool calcFailed, bool isSafeToCancel)
    {
        switch (_state)
        {
            case State.GoalSet:
                return new PathingCommand(_goal, PathingCommandType.CancelAndSetGoal);
            
            case State.PathRequested:
                var ret = new PathingCommand(_goal, PathingCommandType.ForceRevalidateGoalAndPath);
                _state = State.Executing;
                return ret;
            
            case State.Executing:
                if (calcFailed)
                {
                    OnLostControl();
                    return new PathingCommand(_goal, PathingCommandType.CancelAndSetGoal);
                }
                var feet = Ctx.PlayerFeet();
                var pathingBehavior = Baritone.GetPathingBehavior();
                if (_goal == null || (feet != null && _goal.IsInGoal(feet) && _goal.IsInGoal(pathingBehavior.PathStart() ?? feet)))
                {
                    // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/process/CustomGoalProcess.java:93-95
                    // Check disconnectOnArrival, notificationOnPathComplete
                    var settings = Core.Baritone.Settings();
                    if (settings.DisconnectOnArrival.Value)
                    {
                        // TODO: Implement disconnect when client disconnect functionality is available
                        LogDirect("Goal reached, disconnecting...");
                    }
                    if (settings.NotificationOnPathComplete.Value)
                    {
                        LogNotification("Path complete!", true);
                    }
                    OnLostControl();
                    return new PathingCommand(_goal, PathingCommandType.CancelAndSetGoal);
                }
                return new PathingCommand(_goal, PathingCommandType.SetGoalAndPath);
            
            default:
                throw new InvalidOperationException($"Unexpected state {_state}");
        }
    }

    public override void OnLostControl()
    {
        _state = State.None;
        _goal = null;
    }

    public override string DisplayName() => $"Custom Goal {_goal}";

    public void SetGoalAndPath(Goal goal)
    {
        SetGoal(goal);
        Path();
    }

    private enum State
    {
        None,
        GoalSet,
        PathRequested,
        Executing
    }
}

