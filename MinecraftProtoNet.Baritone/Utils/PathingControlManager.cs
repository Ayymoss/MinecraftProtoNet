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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/utils/PathingControlManager.java
 */

using MinecraftProtoNet.Baritone.Api;
using MinecraftProtoNet.Baritone.Api.Event.Events;
using MinecraftProtoNet.Baritone.Api.Event.Listener;
using MinecraftProtoNet.Baritone.Api.Pathing.Calc;
using MinecraftProtoNet.Baritone.Api.Process;

namespace MinecraftProtoNet.Baritone.Utils;

/// <summary>
/// Pathing control manager implementation.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/utils/PathingControlManager.java
/// </summary>
public class PathingControlManager : IPathingControlManager
{
    private readonly IBaritone _baritone;
    private readonly HashSet<IBaritoneProcess> _processes = new();
    private readonly List<IBaritoneProcess> _active = new();
    private IBaritoneProcess? _inControlLastTick;
    private IBaritoneProcess? _inControlThisTick;
    private PathingCommand? _command;

    public PathingControlManager(IBaritone baritone)
    {
        _baritone = baritone;
        
        // Register tick listener to call PostTick() after tick processing
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/utils/PathingControlManager.java:43-54
        baritone.GetGameEventHandler().RegisterEventListener(new PostTickListener(this));
    }
    
    /// <summary>
    /// Simple event listener that calls PostTick() after tick processing.
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/utils/PathingControlManager.java:47-54
    /// Note: In Java, this is called in onTick() but after all behavior ticks, effectively making it post-processing.
    /// In C#, we call it from OnPostTick() to match the semantics.
    /// </summary>
    private class PostTickListener : IGameEventListener
    {
        private readonly PathingControlManager _manager;
        
        public PostTickListener(PathingControlManager manager)
        {
            _manager = manager;
        }
        
        public void OnTick(TickEvent evt) { }
        
        public void OnPostTick(TickEvent evt)
        {
            if (evt.GetType() == TickEvent.TickEventType.In)
            {
                _manager.PostTick();
            }
        }
        
        // Empty implementations for other events
        public void OnPlayerUpdate(PlayerUpdateEvent evt) { }
        public void OnSendChatMessage(ChatEvent evt) { }
        public void OnPreTabComplete(TabCompleteEvent evt) { }
        public void OnChunkEvent(ChunkEvent evt) { }
        public void OnBlockChange(BlockChangeEvent evt) { }
        public void OnRenderPass(RenderEvent evt) { }
        public void OnWorldEvent(WorldEvent evt) { }
        public void OnSendPacket(PacketEvent evt) { }
        public void OnReceivePacket(PacketEvent evt) { }
        public void OnPlayerRotationMove(RotationMoveEvent evt) { }
        public void OnPlayerSprintState(SprintStateEvent evt) { }
        public void OnBlockInteract(BlockInteractEvent evt) { }
        public void OnPlayerDeath() { }
        public void OnPathEvent(PathEvent evt) { }
    }

    public void RegisterProcess(IBaritoneProcess process)
    {
        process.OnLostControl(); // make sure it's reset
        _processes.Add(process);
    }

    public IBaritoneProcess? MostRecentInControl() => _inControlThisTick;

    public PathingCommand? MostRecentCommand() => _command;

    /// <summary>
    /// Cancels everything. Called by PathingBehavior on TickEvent Type OUT.
    /// </summary>
    public void CancelEverything()
    {
        _inControlLastTick = null;
        _inControlThisTick = null;
        _command = null;
        _active.Clear();
        foreach (var proc in _processes)
        {
            proc.OnLostControl();
            if (proc.IsActive() && !proc.IsTemporary())
            {
                throw new InvalidOperationException($"{proc.DisplayName()} stayed active after being cancelled");
            }
        }
    }

    /// <summary>
    /// Called before each tick to execute processes and determine pathing commands.
    /// </summary>
    public void PreTick()
    {
        _inControlLastTick = _inControlThisTick;
        _inControlThisTick = null;
        var pathingBehavior = (Behaviors.PathingBehavior)_baritone.GetPathingBehavior();
        _command = ExecuteProcesses();
        
        if (_command == null)
        {
            pathingBehavior.CancelSegmentIfSafe();
            pathingBehavior.SecretInternalSetGoal(null!); // Explicitly allow null
            return;
        }
        
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/utils/PathingControlManager.java:90-92
        // Handle command types and process control changes
        switch (_command.CommandType)
        {
            case PathingCommandType.CancelAndSetGoal:
                pathingBehavior.SecretInternalSetGoal(_command.Goal!); // Goal can be null, method handles it
                pathingBehavior.CancelSegmentIfSafe();
                break;
            case PathingCommandType.SetGoalAndPath:
                pathingBehavior.SecretInternalSetGoal(_command.Goal!); // Goal can be null, method handles it
                break;
            case PathingCommandType.ForceRevalidateGoalAndPath:
                pathingBehavior.SecretInternalSetGoal(_command.Goal!); // Goal can be null, method handles it
                pathingBehavior.ForceRevalidateGoalAndPath();
                break;
            case PathingCommandType.RevalidateGoalAndPath:
                pathingBehavior.SecretInternalSetGoal(_command.Goal!); // Goal can be null, method handles it
                pathingBehavior.RevalidateGoalAndPath();
                break;
            case PathingCommandType.RequestPause:
                pathingBehavior.RequestPause();
                break;
            case PathingCommandType.Defer:
                // Defer means wait for next tick
                break;
        }
    }

    private PathingCommand? ExecuteProcesses()
    {
        foreach (var process in _processes)
        {
            if (process.IsActive())
            {
                if (!_active.Contains(process))
                {
                    _active.Insert(0, process);
                }
            }
            else
            {
                _active.Remove(process);
            }
        }
        
        _active.Sort((a, b) => b.Priority().CompareTo(a.Priority()));
        
        foreach (var proc in _active)
        {
            var pathingBehavior = (Behaviors.PathingBehavior)_baritone.GetPathingBehavior();
            var exec = proc.OnTick(
                proc == _inControlLastTick && pathingBehavior.CalcFailedLastTick(),
                pathingBehavior.IsSafeToCancel());
            
            if (exec == null)
            {
                if (proc.IsActive())
                {
                    throw new InvalidOperationException($"{proc.DisplayName()} actively returned null PathingCommand");
                }
            }
            else if (exec.CommandType != PathingCommandType.Defer)
            {
                _inControlThisTick = proc;
                if (!proc.IsTemporary())
                {
                    foreach (var remaining in _active.Skip(_active.IndexOf(proc) + 1))
                    {
                        remaining.OnLostControl();
                    }
                }
                return exec;
            }
        }
        return null;
    }

    /// <summary>
    /// Called after each tick to handle goal revalidation commands.
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/utils/PathingControlManager.java:128-153
    /// </summary>
    private void PostTick()
    {
        // if we did this in pretick, it would suck
        // we use the time between ticks as calculation time
        // therefore, we only cancel and recalculate after the tick for the current path has executed
        // "it would suck" means it would actually execute a path every other tick
        if (_command == null)
        {
            return;
        }
        
        var pathingBehavior = (Behaviors.PathingBehavior)_baritone.GetPathingBehavior();
        switch (_command.CommandType)
        {
            case PathingCommandType.ForceRevalidateGoalAndPath:
                if (_command.Goal == null || ForceRevalidate(_command.Goal) || RevalidateGoal(_command.Goal))
                {
                    // pwnage
                    pathingBehavior.SoftCancelIfSafe();
                }
                pathingBehavior.SecretInternalSetGoalAndPath(_command);
                break;
            case PathingCommandType.RevalidateGoalAndPath:
                if (Core.Baritone.Settings().CancelOnGoalInvalidation.Value && (_command.Goal == null || RevalidateGoal(_command.Goal)))
                {
                    pathingBehavior.SoftCancelIfSafe();
                }
                pathingBehavior.SecretInternalSetGoalAndPath(_command);
                break;
            default:
                break;
        }
    }

    private bool ForceRevalidate(Api.Pathing.Goals.Goal newGoal)
    {
        var current = _baritone.GetPathingBehavior().GetCurrent();
        if (current != null)
        {
            if (newGoal.IsInGoal(current.GetPath().GetDest()))
            {
                return false;
            }
            return !newGoal.Equals(current.GetPath().GetGoal());
        }
        return false;
    }

    private bool RevalidateGoal(Api.Pathing.Goals.Goal newGoal)
    {
        var current = _baritone.GetPathingBehavior().GetCurrent();
        if (current != null)
        {
            var intended = current.GetPath().GetGoal();
            var end = current.GetPath().GetDest();
            if (intended.IsInGoal(end) && !newGoal.IsInGoal(end))
            {
                // this path used to end in the goal
                // but the goal has changed, so there's no reason to continue...
                return true;
            }
        }
        return false;
    }
}

