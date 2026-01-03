using MinecraftProtoNet.Pathfinding.Calc;
using MinecraftProtoNet.State;
using MinecraftProtoNet.Baritone.Pathfinding.Calc;

namespace MinecraftProtoNet.Baritone.Pathfinding.Movement;

/// <summary>
/// Abstract base class for all movement implementations.
/// Based on Baritone's Movement.java.
/// </summary>
public abstract class MovementBase
{
    /// <summary>
    /// Source position (where the movement starts).
    /// </summary>
    public (int X, int Y, int Z) Source { get; }

    /// <summary>
    /// Destination position (where the movement ends).
    /// </summary>
    public (int X, int Y, int Z) Destination { get; }

    /// <summary>
    /// The movement direction.
    /// </summary>
    public MoveDirection Direction { get; }

    /// <summary>
    /// Calculated cost of this movement (in ticks).
    /// </summary>
    public double Cost { get; protected set; }

    /// <summary>
    /// Current movement state.
    /// </summary>
    protected readonly MovementState State = new();

    protected MovementBase(int srcX, int srcY, int srcZ, int destX, int destY, int destZ, MoveDirection direction)
    {
        Source = (srcX, srcY, srcZ);
        Destination = (destX, destY, destZ);
        Direction = direction;
    }

    /// <summary>
    /// Calculates the cost of this movement given the world context.
    /// </summary>
    /// <param name="context">The calculation context with world state</param>
    /// <returns>The cost in ticks, or ActionCosts.CostInf if impossible</returns>
    public abstract double CalculateCost(CalculationContext context);

    /// <summary>
    /// Updates the movement state for one tick during execution.
    /// </summary>
    /// <param name="entity">The entity performing the movement</param>
    /// <param name="level">The level/world</param>
    /// <returns>The updated movement state</returns>
    public abstract MovementState UpdateState(Entity entity, Level level);

    /// <summary>
    /// Returns whether this movement is safe to cancel at the current state.
    /// </summary>
    public virtual bool SafeToCancel()
    {
        return State.Status != MovementStatus.Running;
    }

    /// <summary>
    /// Returns the list of blocks that need to be broken for this movement.
    /// Used for proactive breaking (Horizon).
    /// </summary>
    public virtual IEnumerable<(int X, int Y, int Z)> GetBlocksToBreak(CalculationContext context)
    {
        return Enumerable.Empty<(int X, int Y, int Z)>();
    }

    /// <summary>
    /// Returns the list of blocks that need to be placed for this movement.
    /// Used for proactive placing (Horizon).
    /// </summary>
    public virtual IEnumerable<(int X, int Y, int Z)> GetBlocksToPlace(CalculationContext context)
    {
        return Enumerable.Empty<(int X, int Y, int Z)>();
    }

    /// <summary>
    /// Returns the list of blocks that the player walks into during this movement.
    /// Used for tracking intermediate blocks (e.g., corner blocks in diagonal movement).
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/Movement.java:286-291
    /// </summary>
    public virtual IEnumerable<(int X, int Y, int Z)> GetBlocksToWalkInto(CalculationContext context)
    {
        return Enumerable.Empty<(int X, int Y, int Z)>();
    }

    /// <summary>
    /// Returns the set of valid positions for this movement.
    /// Used for off-path distance calculation and path recovery.
    /// Based on Baritone's Movement.getValidPositions() (lines 104-110)
    /// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/movement/Movement.java
    /// </summary>
    public virtual HashSet<(int X, int Y, int Z)> GetValidPositions()
    {
        // Default implementation returns Source and Destination
        // Subclasses override for movements with intermediate positions (e.g. diagonal)
        return [Source, Destination];
    }

    /// <summary>
    /// Returns whether the given position is valid for this movement.
    /// Used for path recovery after server teleport.
    /// Based on Baritone's Movement.getValidPositions()
    /// </summary>
    public bool IsValidPosition(int x, int y, int z)
    {
        return GetValidPositions().Contains((x, y, z));
    }

    /// <summary>
    /// Resets the movement state for reuse.
    /// </summary>
    public virtual void Reset()
    {
        State.ClearInputs();
        State.Status = MovementStatus.Waiting;
    }

    /// <summary>
    /// Returns whether the entity has reached the destination.
    /// </summary>
    protected bool HasReachedDestination(Entity entity)
    {
        var dx = entity.Position.X - (Destination.X + 0.5);
        var dy = entity.Position.Y - Destination.Y;
        var dz = entity.Position.Z - (Destination.Z + 0.5);

        // Within 0.5 blocks horizontally and close vertically
        return Math.Abs(dx) < 0.5 && Math.Abs(dz) < 0.5 && Math.Abs(dy) < 0.5;
    }

    /// <summary>
    /// Gets the block position the entity is standing on.
    /// </summary>
    protected static (int X, int Y, int Z) GetFeetPosition(Entity entity)
    {
        return ((int)Math.Floor(entity.Position.X),
                (int)Math.Floor(entity.Position.Y),
                (int)Math.Floor(entity.Position.Z));
    }

    /// <summary>
    /// Sets movement input to walk towards the destination.
    /// </summary>
    protected void MoveTowards(Entity entity)
    {
        var targetX = Destination.X + 0.5;
        var targetZ = Destination.Z + 0.5;

        // Calculate yaw to face destination
        var yaw = MovementHelper.CalculateYaw(entity.Position.X, entity.Position.Z, targetX, targetZ);
        State.SetTarget(yaw, 0);

        // Set forward movement
        State.MoveForward = true;
    }

    public override string ToString()
    {
        return $"{GetType().Name}({Source} -> {Destination}, cost={Cost:F2})";
    }
}
