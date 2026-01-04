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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/process/ElytraProcess.java
 */

using MinecraftProtoNet.Baritone.Api;
using MinecraftProtoNet.Baritone.Api.Pathing.Goals;
using MinecraftProtoNet.Baritone.Api.Process;
using MinecraftProtoNet.Baritone.Api.Utils;
using MinecraftProtoNet.Baritone.Api.Utils.Input;
using MinecraftProtoNet.Baritone.Pathfinding.Goals;
using MinecraftProtoNet.Baritone.Pathfinding.Movement;
using MinecraftProtoNet.Baritone.Pathfinding.Movement.Movements;
using MinecraftProtoNet.Baritone.Process.Elytra;
using MinecraftProtoNet.Baritone.Utils;
using MinecraftProtoNet.Core.State;
using BaritoneInput = MinecraftProtoNet.Baritone.Api.Utils.Input.Input;
using BaritoneSettings = MinecraftProtoNet.Baritone.Core.Baritone;

namespace MinecraftProtoNet.Baritone.Process;

/// <summary>
/// Elytra process implementation.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/process/ElytraProcess.java
/// 
/// DISABLED: This functionality requires the external native library 'dev.babbaj.pathfinder' (NetherPathfinder).
/// See NetherPathfinderContext.IsSupported() for details on why this is disabled.
/// </summary>
public class ElytraProcess : BaritoneProcessHelper, IElytraProcess
{
    private State _state = State.StartFlying;
#pragma warning disable CS0414 // Field is assigned but never used - reserved for future landing spot logic
    private bool _goingToLandingSpot;
    private BetterBlockPos? _landingSpot;
    private bool _reachedGoal;
#pragma warning restore CS0414
    private Goal? _goal;
    private BetterBlockPos? _destination;
    private bool _predictingTerrain;
    private readonly HashSet<BetterBlockPos> _badLandingSpots = new();
    private ElytraBehavior? _behavior;

    private const int LANDING_COLUMN_HEIGHT = 15;
    private const int CHESTPLATE_SLOT = 38; // EquipmentSlot.Chestplate maps to slot 38

    public ElytraProcess(IBaritone baritone) : base(baritone)
    {
    }

    public override bool IsActive()
    {
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/process/ElytraProcess.java:96-98
        // Active when we have a destination
        return _destination != null;
    }

    public override PathingCommand OnTick(bool calcFailed, bool isSafeToCancel)
    {
        // DISABLED: Elytra pathfinding requires native library support (dev.babbaj.pathfinder)
        // Without the native library, real-time elytra pathfinding cannot function due to performance requirements.
        if (!IsLoaded())
        {
            OnLostControl();
            return new PathingCommand(null, PathingCommandType.CancelAndSetGoal);
        }

        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/process/ElytraProcess.java:113-286
        if (calcFailed)
        {
            OnLostControl();
            LogDirect("Failed to compute a walking path to a spot to jump off from. Consider starting from a higher location, near an overhang.");
            return new PathingCommand(null, PathingCommandType.CancelAndSetGoal);
        }

        var player = Ctx.Player() as Entity;
        if (player == null)
        {
            return new PathingCommand(null, PathingCommandType.RequestPause);
        }

        // Check if player is flying with elytra (falling with negative Y velocity and not on ground)
        bool isFlying = !player.IsOnGround && player.Velocity.Y < -0.1;

        if (isFlying)
        {
            // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/process/ElytraProcess.java:192-197
            // Player is flying - handle elytra flight
            if (_behavior != null)
            {
                _behavior.Tick();
            }
            Baritone.GetInputOverrideHandler().ClearAllKeys();
            return new PathingCommand(null, PathingCommandType.CancelAndSetGoal);
        }
        else if (_state == State.Landing)
        {
            // Check if landed
            if (player.IsOnGround)
            {
                var horizontalVel = Math.Sqrt(player.Velocity.X * player.Velocity.X + player.Velocity.Z * player.Velocity.Z);
                if (horizontalVel > 0.001)
                {
                    LogDirect("Landed, but still moving, waiting for velocity to die down...");
                    Baritone.GetInputOverrideHandler().SetInputForceState(BaritoneInput.Sneak, true);
                    return new PathingCommand(null, PathingCommandType.RequestPause);
                }
                LogDirect("Done :)");
                Baritone.GetInputOverrideHandler().ClearAllKeys();
                OnLostControl();
                return new PathingCommand(null, PathingCommandType.RequestPause);
            }
        }

        if (_state == State.Flying || _state == State.StartFlying)
        {
            // Transition to locate jump if on ground and auto jump enabled
            if (player.IsOnGround && BaritoneSettings.Settings().ElytraAutoJump.Value)
            {
                _state = State.LocateJump;
            }
            else
            {
                _state = State.StartFlying;
            }
        }

        if (_state == State.LocateJump)
        {
            if (ShouldLandForSafety())
            {
                LogDirect("Not taking off, because elytra durability or fireworks are so low that I would immediately emergency land anyway.");
                OnLostControl();
                return new PathingCommand(null, PathingCommandType.CancelAndSetGoal);
            }

            if (_goal == null)
            {
                _goal = new GoalYLevel(31);
            }

            var executor = Baritone.GetPathingBehavior().GetCurrent();
            if (executor != null && Equals(executor.GetPath().GetGoal(), _goal))
            {
                var movements = executor.GetPath().Movements();
                var fallMovement = movements.FirstOrDefault(m => m is MovementFall);

                if (fallMovement != null)
                {
                    // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/process/ElytraProcess.java:232-244
                    // Found a fall movement - calculate path to destination
                    var from = new BetterBlockPos(
                        (fallMovement.GetSrc().X + fallMovement.GetDest().X) / 2,
                        (fallMovement.GetSrc().Y + fallMovement.GetDest().Y) / 2,
                        (fallMovement.GetSrc().Z + fallMovement.GetDest().Z) / 2
                    );
                    
                    // TODO: When PathManager is fully implemented, use: behavior.pathManager.pathToDestination(from).whenComplete(...)
                    // For now, transition to GET_TO_JUMP state
                    _state = State.GetToJump;
                }
                else
                {
                    OnLostControl();
                    LogDirect("Failed to compute a walking path to a spot to jump off from.");
                    return new PathingCommand(null, PathingCommandType.CancelAndSetGoal);
                }
            }
            else
            {
                // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/process/ElytraProcess.java:251
                // Use WalkOffCalculationContext for pathfinding (allows falling into lava)
                var walkOffContext = new WalkOffCalculationContext(Baritone);
                return new PathingCommandContext(_goal, PathingCommandType.SetGoalAndPath, walkOffContext);
            }
        }

        if (_state == State.Pause)
        {
            return new PathingCommand(null, PathingCommandType.RequestPause);
        }

        if (_state == State.GetToJump)
        {
            var executor = Baritone.GetPathingBehavior().GetCurrent();
            // Check if player is falling fast enough to start flying
            bool canStartFlying = player.Velocity.Y < -0.377
                && !isSafeToCancel
                && executor != null
                && executor.GetPath().Movements().ElementAtOrDefault(executor.GetPosition()) is MovementFall;

            if (canStartFlying)
            {
                _state = State.StartFlying;
            }
            else
            {
                return new PathingCommand(null, PathingCommandType.SetGoalAndPath);
            }
        }

        if (_state == State.StartFlying)
        {
            if (!isSafeToCancel)
            {
                Baritone.GetPathingBehavior().SecretInternalSegmentCancel();
            }
            Baritone.GetInputOverrideHandler().ClearAllKeys();
            if (player.Velocity.Y < -0.377)
            {
                Baritone.GetInputOverrideHandler().SetInputForceState(BaritoneInput.Jump, true);
            }
        }

        return new PathingCommand(null, PathingCommandType.CancelAndSetGoal);
    }

    public override void OnLostControl()
    {
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/process/ElytraProcess.java:75-82
        _state = State.StartFlying;
        _goingToLandingSpot = false;
        _landingSpot = null;
        _reachedGoal = false;
        _goal = null;
        _destination = null;
        DestroyBehaviorAsync();
    }

    private void DestroyBehaviorAsync()
    {
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/process/ElytraProcess.java:295-301
        var behavior = _behavior;
        if (behavior != null)
        {
            _behavior = null;
            // TODO: When executor service is available, use: Baritone.getExecutor().execute(behavior::destroy);
            // For now, destroy synchronously
            behavior.Destroy();
        }
    }

    public override string DisplayName() => $"Elytra - {_state.Description()}";

    public override double Priority() => 0; // Higher priority than CustomGoalProcess

    public void Engage()
    {
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/process/ElytraProcess.java
        // Engage elytra flight
        if (_destination != null)
        {
            _state = State.LocateJump;
        }
    }

    public void Disengage()
    {
        OnLostControl();
    }

    public void SetGoal(Goal? goal)
    {
        _goal = goal;
    }

    public Goal? GetGoal() => _goal;

    public void RepackChunks()
    {
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/process/ElytraProcess.java:314-318
        if (_behavior != null)
        {
            _behavior.RepackChunks();
        }
    }

    public BetterBlockPos? CurrentDestination() => _destination;

    public void PathTo(BetterBlockPos destination)
    {
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/process/ElytraProcess.java:326-341
        PathTo0(destination, false);
    }

    private void PathTo0(BetterBlockPos destination, bool appendDestination)
    {
        // DISABLED: Elytra pathfinding requires native library support (dev.babbaj.pathfinder)
        if (!IsLoaded())
        {
            LogDirect("Elytra pathfinding is disabled: requires native library 'dev.babbaj.pathfinder' (NetherPathfinder). See NetherPathfinderContext for details.");
            return;
        }

        var player = Ctx.Player() as Entity;
        if (player == null)
        {
            return;
        }

        var world = Ctx.World() as Level;
        if (world == null || world.DimensionType.Name != "minecraft:the_nether")
        {
            LogDirect("Elytra pathfinding only works in the Nether");
            return;
        }

        OnLostControl();
        _predictingTerrain = BaritoneSettings.Settings().ElytraPredictTerrain.Value;
        _destination = destination;
        
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/process/ElytraProcess.java:336
        _behavior = new ElytraBehavior(Baritone, this, destination, appendDestination);
        
        if (world != null)
        {
            _behavior.RepackChunks();
        }
        
        // TODO: When PathManager is fully implemented, use: this.behavior.pathTo()
        // For now, just set the destination and transition to locate jump
        _state = State.LocateJump;
    }

    public void PathTo(Goal destination)
    {
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/process/ElytraProcess.java:344-365
        int x, y, z;
        if (destination is GoalXZ goalXZ)
        {
            x = goalXZ.GetX();
            y = 64;
            z = goalXZ.GetZ();
        }
        else if (destination is GoalBlock goalBlock)
        {
            x = goalBlock.X;
            y = goalBlock.Y;
            z = goalBlock.Z;
        }
        else
        {
            throw new ArgumentException("The goal must be a GoalXZ or GoalBlock");
        }

        if (y <= 0 || y >= 128)
        {
            throw new ArgumentException("The y of the goal is not between 0 and 128");
        }

        PathTo(new BetterBlockPos(x, y, z));
    }

    public void ResetState()
    {
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/process/ElytraProcess.java:101-108
        var destination = CurrentDestination();
        OnLostControl();
        if (destination != null)
        {
            PathTo(destination);
            RepackChunks();
        }
    }

    private bool ShouldLandForSafety()
    {
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/process/ElytraProcess.java:367-385
        var player = Ctx.Player() as Entity;
        if (player == null)
        {
            return true;
        }

        // Check elytra durability
        var chestSlot = player.Inventory.GetSlot(CHESTPLATE_SLOT);
        if (chestSlot.ItemId == null || chestSlot.ItemId == 0)
        {
            return true; // No chestplate
        }

        var itemRegistry = Baritone.GetItemRegistryService();
        var itemName = itemRegistry.GetItemName(chestSlot.ItemId.Value);
        if (itemName == null || !itemName.Contains("elytra", StringComparison.OrdinalIgnoreCase))
        {
            return true; // Not elytra
        }

        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/process/ElytraProcess.java:369-371
        // Check elytra durability
        int maxDamage = chestSlot.GetMaxDamage();
        if (maxDamage > 0)
        {
            int remainingDurability = chestSlot.GetRemainingDurability();
            if (remainingDurability < BaritoneSettings.Settings().ElytraMinimumDurability.Value)
            {
                return true; // Elytra durability too low
            }
        }

        // Check fireworks quantity
        int fireworkQty = 0;
        foreach (var slot in player.Inventory.Items.Values)
        {
            if (slot.ItemId != null && slot.ItemId != 0)
            {
                var slotItemName = itemRegistry.GetItemName(slot.ItemId.Value);
                if (slotItemName != null && slotItemName.Contains("firework", StringComparison.OrdinalIgnoreCase))
                {
                    fireworkQty += slot.ItemCount;
                }
            }
        }

        if (fireworkQty <= BaritoneSettings.Settings().ElytraMinFireworksBeforeLanding.Value)
        {
            return true;
        }

        return false;
    }

    public bool IsLoaded()
    {
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/process/ElytraProcess.java:388-390
        // In Java, this checks if NetherPathfinderContext.isSupported()
        return NetherPathfinderContext.IsSupported();
    }

    public bool IsSafeToCancel()
    {
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/process/ElytraProcess.java:393-395
        return !IsActive() || !(_state == State.Flying || _state == State.StartFlying);
    }

    public enum State
    {
        LocateJump,
        Pause,
        GetToJump,
        StartFlying,
        Flying,
        Landing
    }

}

internal static class ElytraProcessStateExtensions
{
    private static readonly Dictionary<ElytraProcess.State, string> Descriptions = new()
    {
        { ElytraProcess.State.LocateJump, "Finding spot to jump off" },
        { ElytraProcess.State.Pause, "Waiting for elytra path" },
        { ElytraProcess.State.GetToJump, "Walking to takeoff" },
        { ElytraProcess.State.StartFlying, "Begin flying" },
        { ElytraProcess.State.Flying, "Flying" },
        { ElytraProcess.State.Landing, "Landing" }
    };

    public static string Description(this ElytraProcess.State state)
    {
        return Descriptions.TryGetValue(state, out var desc) ? desc : state.ToString();
    }
}
