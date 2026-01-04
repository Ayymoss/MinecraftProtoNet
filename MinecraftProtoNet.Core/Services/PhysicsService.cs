using Microsoft.Extensions.Logging;
using MinecraftProtoNet.Core.Core.Abstractions;
using MinecraftProtoNet.Core.Enums;
using MinecraftProtoNet.Core.Models.Core;
using MinecraftProtoNet.Core.Packets.Play.Serverbound;
using MinecraftProtoNet.Core.Physics;
using MinecraftProtoNet.Core.Physics.Shapes;
using MinecraftProtoNet.Core.State;

namespace MinecraftProtoNet.Core.Services;

/// <summary>
/// Service for handling entity physics simulation.
/// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/entity/LivingEntity.java
/// </summary>
public class PhysicsService(ILogger<PhysicsService> logger) : IPhysicsService
{
    private readonly ILogger<PhysicsService> _logger = logger;
    
    // Terminal velocity in blocks/tick (Minecraft's max fall speed)
    private const double TerminalVelocity = -3.92;
    private const double PositionUpdateThreshold = 2.0E-4;
    private const int PositionReminderInterval = 20;

    public async Task PhysicsTickAsync(
        Entity entity,
        Level level,
        IPacketSender packetSender,
        Action<Entity>? prePhysicsCallback = null)
    {
        // Invoke pre-physics callback (e.g., for pathfinding input)
        prePhysicsCallback?.Invoke(entity);

        // Update fluid state
        UpdateFluidState(entity, level);

        // Calculate movement based on travel method (updates velocity)
        var input = ConvertInputToVector3(entity);
        Travel(entity, level, input);

        // Apply movement with collision
        Move(entity, level, entity.Velocity);

        // Send position updates to server
        await SendPositionAsync(entity, packetSender);

        await Task.CompletedTask;
    }

    /// <summary>
    /// Updates fluid state (IsInWater, IsInLava, FluidHeight) from block states.
    /// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/entity/Entity.java:1567-1594
    /// </summary>
    private void UpdateFluidState(Entity entity, Level level)
    {
        var blockPos = entity.BlockPosition();
        var aabb = entity.GetBoundingBox();
        
        entity.IsInWater = false;
        entity.IsInLava = false;
        entity.FluidHeight = 0.0;
        
        var minX = (int)Math.Floor(aabb.MinX);
        var maxX = (int)Math.Floor(aabb.MaxX);
        var minY = (int)Math.Floor(aabb.MinY);
        var maxY = (int)Math.Floor(aabb.MinY + 0.001);
        var minZ = (int)Math.Floor(aabb.MinZ);
        var maxZ = (int)Math.Floor(aabb.MaxZ);
        
        double maxFluidHeight = double.NegativeInfinity;
        
        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    var blockState = level.GetBlockAt(x, y, z);
                    if (blockState == null) continue;
                    
                    if (blockState.Name.Contains("water", StringComparison.OrdinalIgnoreCase))
                    {
                        entity.IsInWater = true;
                        // Calculate fluid height from level property (0-8, where 8 is full)
                        double fluidHeight = 1.0;
                        if (blockState.Properties.TryGetValue("level", out var levelStr) && 
                            int.TryParse(levelStr, out var levelInt))
                        {
                            fluidHeight = levelInt == 0 ? 1.0 : (levelInt / 8.0);
                        }
                        double blockFluidHeight = y + fluidHeight;
                        maxFluidHeight = Math.Max(maxFluidHeight, blockFluidHeight);
                    }
                    else if (blockState.Name.Contains("lava", StringComparison.OrdinalIgnoreCase))
                    {
                        entity.IsInLava = true;
                        double fluidHeight = 1.0;
                        if (blockState.Properties.TryGetValue("level", out var levelStr) && 
                            int.TryParse(levelStr, out var levelInt))
                        {
                            fluidHeight = levelInt == 0 ? 1.0 : (levelInt / 8.0);
                        }
                        double blockFluidHeight = y + fluidHeight;
                        maxFluidHeight = Math.Max(maxFluidHeight, blockFluidHeight);
                    }
                }
            }
        }
        
        if (maxFluidHeight > double.NegativeInfinity)
        {
            entity.FluidHeight = Math.Max(0.0, maxFluidHeight - aabb.MinY);
        }
    }

    /// <summary>
    /// Routes to appropriate travel method based on fluid state.
    /// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/entity/LivingEntity.java:2268-2277
    /// </summary>
    private void Travel(Entity entity, Level level, Vector3<double> input)
    {
        if (entity.IsInWater || entity.IsInLava)
        {
            TravelInFluid(entity, level, input);
        }
        else
        {
            TravelInAir(entity, level, input);
        }
    }

    /// <summary>
    /// Travel in air with gravity, friction, and block friction.
    /// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/entity/LivingEntity.java:2304-2330
    /// </summary>
    private void TravelInAir(Entity entity, Level level, Vector3<double> input)
    {
        var blockPosBelow = GetBlockPosBelowThatAffectsMyMovement(entity, level);
        var blockStateBelow = level.GetBlockAt(blockPosBelow.X, blockPosBelow.Y, blockPosBelow.Z);
        float blockFriction = entity.IsOnGround && blockStateBelow != null 
            ? blockStateBelow.Friction 
            : 1.0f;
        float friction = blockFriction * PhysicsConstants.AirDrag;
        
        // Handle input-based movement with friction
        var movement = HandleRelativeFrictionAndCalculateMovement(entity, level, input, blockFriction);
        double movementY = movement.Y;
        
        // Apply gravity
        movementY -= PhysicsConstants.DefaultGravity;
        
        // Apply friction
        float verticalFriction = 0.98f; // Not FlyingAnimal, so use 0.98
        entity.Velocity = new Vector3<double>(
            movement.X * friction,
            movementY * verticalFriction,
            movement.Z * friction);
        
        // Clamp to terminal velocity
        if (entity.Velocity.Y < TerminalVelocity)
        {
            entity.Velocity = new Vector3<double>(
                entity.Velocity.X,
                TerminalVelocity,
                entity.Velocity.Z);
        }
    }

    /// <summary>
    /// Travel in fluid (water or lava).
    /// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/entity/LivingEntity.java:2332-2390
    /// </summary>
    private void TravelInFluid(Entity entity, Level level, Vector3<double> input)
    {
        bool isFalling = entity.Velocity.Y <= 0.0;
        double oldY = entity.Position.Y;
        double baseGravity = PhysicsConstants.DefaultGravity;
        
        if (entity.IsInWater)
        {
            TravelInWater(entity, level, input, baseGravity, isFalling, oldY);
        }
        else if (entity.IsInLava)
        {
            TravelInLava(entity, level, input, baseGravity, isFalling, oldY);
        }
    }

    /// <summary>
    /// Travel in water.
    /// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/entity/LivingEntity.java:2345-2372
    /// </summary>
    private void TravelInWater(Entity entity, Level level, Vector3<double> input, double baseGravity, bool isFalling, double oldY)
    {
        float slowDown = entity.IsSprinting ? 0.9f : PhysicsConstants.WaterSlowdown;
        float speed = PhysicsConstants.WaterAcceleration;
        
        // Move relative to input
        MoveRelative(entity, speed, input);
        
        // Apply water slowdown (velocity will be applied with collision in Move() later)
        var ladderMovement = entity.Velocity;
        // TODO: Handle ladder collisions when onClimbable is implemented
        ladderMovement = new Vector3<double>(
            ladderMovement.X * slowDown,
            ladderMovement.Y * 0.8,
            ladderMovement.Z * slowDown);
        
        // Apply fluid falling adjustment
        var adjustedMovement = GetFluidFallingAdjustedMovement(baseGravity, isFalling, ladderMovement);
        entity.Velocity = adjustedMovement;
    }

    /// <summary>
    /// Travel in lava.
    /// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/entity/LivingEntity.java:2374-2390
    /// </summary>
    private void TravelInLava(Entity entity, Level level, Vector3<double> input, double baseGravity, bool isFalling, double oldY)
    {
        MoveRelative(entity, PhysicsConstants.WaterAcceleration, input);
        
        if (entity.FluidHeight <= 0.4) // fluidJumpThreshold
        {
            entity.Velocity = new Vector3<double>(
                entity.Velocity.X * 0.5,
                entity.Velocity.Y * 0.8,
                entity.Velocity.Z * 0.5);
            var movement = GetFluidFallingAdjustedMovement(baseGravity, isFalling, entity.Velocity);
            entity.Velocity = movement;
        }
        else
        {
            entity.Velocity = new Vector3<double>(
                entity.Velocity.X * 0.5,
                entity.Velocity.Y * 0.5,
                entity.Velocity.Z * 0.5);
        }
        
        if (baseGravity != 0.0)
        {
            entity.Velocity = new Vector3<double>(
                entity.Velocity.X,
                entity.Velocity.Y - baseGravity / 4.0,
                entity.Velocity.Z);
        }
    }

    /// <summary>
    /// Handles input-based movement with friction calculation.
    /// Returns the movement vector after applying input and friction.
    /// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/entity/LivingEntity.java:2505-2515
    /// Note: In vanilla, this calls move() internally, but we apply movement separately.
    /// </summary>
    private Vector3<double> HandleRelativeFrictionAndCalculateMovement(Entity entity, Level level, Vector3<double> input, float blockFriction)
    {
        float speed = GetFrictionInfluencedSpeed(entity, blockFriction);
        MoveRelative(entity, speed, input);
        
        // TODO: Handle climbable (ladder) logic when implemented
        // entity.Velocity = HandleOnClimbable(entity.Velocity);
        
        var movement = entity.Velocity;
        // TODO: Handle ladder collision boost when onClimbable is implemented
        
        return movement;
    }

    /// <summary>
    /// Gets friction-influenced speed based on block friction.
    /// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/entity/LivingEntity.java:2549-2551
    /// </summary>
    private float GetFrictionInfluencedSpeed(Entity entity, float blockFriction)
    {
        if (entity.IsOnGround)
        {
            // Base movement speed (0.1) with friction formula: 0.216 / (friction^3)
            return (float)(PhysicsConstants.BaseMovementSpeed * (0.21600002f / (blockFriction * blockFriction * blockFriction)));
        }
        else
        {
            // Flying speed (0.02 for players)
            return PhysicsConstants.DefaultFlyingSpeed;
        }
    }

    /// <summary>
    /// Moves entity relative to input direction based on yaw.
    /// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/entity/Entity.java:1691-1706
    /// </summary>
    private void MoveRelative(Entity entity, float speed, Vector3<double> input)
    {
        var delta = GetInputVector(input, speed, entity.YawPitch.X);
        entity.Velocity = new Vector3<double>(
            entity.Velocity.X + delta.X,
            entity.Velocity.Y + delta.Y,
            entity.Velocity.Z + delta.Z);
    }

    /// <summary>
    /// Converts input vector to movement delta based on yaw rotation.
    /// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/entity/Entity.java:1696-1706
    /// </summary>
    private Vector3<double> GetInputVector(Vector3<double> input, float speed, float yaw)
    {
        double lengthSqr = input.LengthSquared();
        if (lengthSqr < 1.0E-7)
        {
            return Vector3<double>.Zero;
        }
        
        var movement = lengthSqr > 1.0 ? input.Normalized() * speed : input * speed;
        
        float yawRad = yaw * (MathF.PI / 180.0f);
        float sin = MathF.Sin(yawRad);
        float cos = MathF.Cos(yawRad);
        
        return new Vector3<double>(
            movement.X * cos - movement.Z * sin,
            movement.Y,
            movement.Z * cos + movement.X * sin);
    }

    /// <summary>
    /// Converts Entity.Input to Vector3 movement vector.
    /// </summary>
    private Vector3<double> ConvertInputToVector3(Entity entity)
    {
        double x = 0.0;
        double z = 0.0;
        
        if (entity.Input.Forward) z -= 1.0;
        if (entity.Input.Backward) z += 1.0;
        if (entity.Input.Left) x -= 1.0;
        if (entity.Input.Right) x += 1.0;
        
        // Normalize diagonal movement
        if (x != 0.0 && z != 0.0)
        {
            x *= 0.7071067811865476; // 1/sqrt(2)
            z *= 0.7071067811865476;
        }
        
        return new Vector3<double>(x, 0.0, z);
    }

    /// <summary>
    /// Gets block position below entity that affects movement (for friction).
    /// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/entity/Entity.java:1004-1006
    /// </summary>
    private Vector3<int> GetBlockPosBelowThatAffectsMyMovement(Entity entity, Level level)
    {
        var blockPos = entity.BlockPosition();
        return new Vector3<int>(blockPos.X, blockPos.Y - 1, blockPos.Z);
    }

    /// <summary>
    /// Gets fluid falling adjusted movement.
    /// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/entity/LivingEntity.java:2517-2530
    /// </summary>
    private Vector3<double> GetFluidFallingAdjustedMovement(double baseGravity, bool isFalling, Vector3<double> movement)
    {
        if (baseGravity != 0.0) // TODO: Check if sprinting
        {
            double yd;
            if (isFalling && Math.Abs(movement.Y - 0.005) >= 0.003 && Math.Abs(movement.Y - baseGravity / 16.0) < 0.003)
            {
                yd = -0.003;
            }
            else
            {
                yd = movement.Y - baseGravity / 16.0;
            }
            return new Vector3<double>(movement.X, yd, movement.Z);
        }
        return movement;
    }

    /// <summary>
    /// Checks if entity can move to position without collision.
    /// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/entity/Entity.java:681-687
    /// </summary>
    private bool IsFree(Entity entity, Level level, Vector3<double> movement)
    {
        var aabb = entity.GetBoundingBox().Move(movement);
        return !level.GetCollidingBlockAABBs(aabb).Any();
    }

    /// <summary>
    /// Applies movement with collision detection and step-up logic.
    /// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/entity/Entity.java:737-832
    /// </summary>
    private void Move(Entity entity, Level level, Vector3<double> delta)
    {
        if (delta.LengthSquared() < 1.0E-7)
        {
            return;
        }
        
        // Collide movement
        var movement = Collide(entity, level, delta);
        var movementLengthSqr = movement.LengthSquared();
        
        if (movementLengthSqr > 1.0E-7)
        {
            // Update position
            entity.Position = new Vector3<double>(
                entity.Position.X + movement.X,
                entity.Position.Y + movement.Y,
                entity.Position.Z + movement.Z);
        }
        
        // Update collision flags and ground detection
        bool xCollision = Math.Abs(delta.X - movement.X) > 1.0E-7;
        bool yCollision = Math.Abs(delta.Y - movement.Y) > 1.0E-7;
        bool zCollision = Math.Abs(delta.Z - movement.Z) > 1.0E-7;
        
        entity.HorizontalCollision = xCollision || zCollision;
        bool verticalCollisionBelow = yCollision && delta.Y < 0.0;
        entity.IsOnGround = verticalCollisionBelow;
        
        // Zero horizontal velocity on collision
        if (entity.HorizontalCollision)
        {
            entity.Velocity = new Vector3<double>(
                xCollision ? 0.0 : entity.Velocity.X,
                entity.Velocity.Y,
                zCollision ? 0.0 : entity.Velocity.Z);
        }
        
        // Apply block speed factor (affects velocity for next tick)
        var blockPos = entity.BlockPosition();
        var blockState = level.GetBlockAt(blockPos.X, blockPos.Y, blockPos.Z);
        float blockSpeedFactor = blockState?.SpeedFactor ?? 1.0f;
        entity.Velocity = new Vector3<double>(
            entity.Velocity.X * blockSpeedFactor,
            entity.Velocity.Y,
            entity.Velocity.Z * blockSpeedFactor);
    }

    /// <summary>
    /// Collides movement with blocks, returns actual movement delta.
    /// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/entity/Entity.java:1089-1118
    /// </summary>
    private Vector3<double> Collide(Entity entity, Level level, Vector3<double> movement)
    {
        var aabb = entity.GetBoundingBox();
        
        // Get colliding shapes
        var collidingShapes = level.GetCollidingShapes(aabb.ExpandTowards(movement)).ToList();
        
        // Collide with shapes (X, Y, Z order)
        var resolvedMovement = CollideWithShapes(movement, aabb, collidingShapes);
        
        // Check for collisions
        bool xCollision = Math.Abs(movement.X - resolvedMovement.X) > 1.0E-7;
        bool yCollision = Math.Abs(movement.Y - resolvedMovement.Y) > 1.0E-7;
        bool zCollision = Math.Abs(movement.Z - resolvedMovement.Z) > 1.0E-7;
        bool onGroundAfterCollision = yCollision && movement.Y < 0.0;
        
        // Step-up logic
        double stepHeight = PhysicsConstants.DefaultStepHeight;
        if (stepHeight > 0.0 && (onGroundAfterCollision || entity.IsOnGround) && (xCollision || zCollision))
        {
            var groundedAABB = onGroundAfterCollision ? aabb.Move(0.0, resolvedMovement.Y, 0.0) : aabb;
            var stepUpAABB = groundedAABB.ExpandTowards(movement.X, stepHeight, movement.Z);
            if (!onGroundAfterCollision)
            {
                stepUpAABB = stepUpAABB.ExpandTowards(0.0, -9.999999747378752E-6, 0.0);
            }
            
            var stepUpShapes = level.GetCollidingShapes(stepUpAABB).ToList();
            var candidateHeights = CollectCandidateStepUpHeights(groundedAABB, stepUpShapes, (float)stepHeight, (float)resolvedMovement.Y);
            
            foreach (var candidateHeight in candidateHeights)
            {
                var stepMovement = new Vector3<double>(movement.X, candidateHeight, movement.Z);
                var stepFromGround = CollideWithShapes(stepMovement, groundedAABB, stepUpShapes);
                double horizontalDistSqr = stepFromGround.X * stepFromGround.X + stepFromGround.Z * stepFromGround.Z;
                double resolvedDistSqr = resolvedMovement.X * resolvedMovement.X + resolvedMovement.Z * resolvedMovement.Z;
                
                if (horizontalDistSqr > resolvedDistSqr)
                {
                    double distanceToGround = aabb.MinY - groundedAABB.MinY;
                    return new Vector3<double>(
                        stepFromGround.X,
                        stepFromGround.Y - distanceToGround,
                        stepFromGround.Z);
                }
            }
        }
        
        return resolvedMovement;
    }

    /// <summary>
    /// Collides movement with a list of VoxelShapes.
    /// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/entity/Entity.java:1171-1189
    /// </summary>
    private Vector3<double> CollideWithShapes(Vector3<double> movement, AABB boundingBox, List<VoxelShape> shapes)
    {
        if (shapes.Count == 0)
        {
            return movement;
        }
        
        var resolvedMovement = Vector3<double>.Zero;
        
        // Resolve collisions in X, Y, Z order (vanilla uses axisStepOrder which optimizes, but X-Y-Z works)
        var axes = new[] { Axis.X, Axis.Y, Axis.Z };
        foreach (var axis in axes)
        {
            double axisMovement = axis switch
            {
                Axis.X => movement.X,
                Axis.Y => movement.Y,
                Axis.Z => movement.Z,
                _ => 0.0
            };
            
            if (Math.Abs(axisMovement) > 1.0E-7)
            {
                var movedBox = boundingBox.Move(resolvedMovement.X, resolvedMovement.Y, resolvedMovement.Z);
                double collision = Shapes.Collide(axis, movedBox, shapes, axisMovement);
                
                resolvedMovement = axis switch
                {
                    Axis.X => new Vector3<double>(collision, resolvedMovement.Y, resolvedMovement.Z),
                    Axis.Y => new Vector3<double>(resolvedMovement.X, collision, resolvedMovement.Z),
                    Axis.Z => new Vector3<double>(resolvedMovement.X, resolvedMovement.Y, collision),
                    _ => resolvedMovement
                };
            }
        }
        
        return resolvedMovement;
    }

    /// <summary>
    /// Collects candidate step-up heights from colliding shapes.
    /// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/entity/Entity.java:1120-1143
    /// </summary>
    private float[] CollectCandidateStepUpHeights(AABB boundingBox, List<VoxelShape> colliders, float maxStepHeight, float stepHeightToSkip)
    {
        var candidates = new HashSet<float>();
        
        foreach (var collider in colliders)
        {
            var coords = collider.GetCoords(Axis.Y);
            for (int i = 0; i < coords.Count; i++)
            {
                double coord = coords.GetDouble(i);
                float relativeCoord = (float)(coord - boundingBox.MinY);
                
                if (relativeCoord >= 0.0f && relativeCoord != stepHeightToSkip && relativeCoord <= maxStepHeight)
                {
                    candidates.Add(relativeCoord);
                }
            }
        }
        
        var sorted = candidates.ToArray();
        Array.Sort(sorted);
        return sorted;
    }

    /// <summary>
    /// Sends position updates to server based on changes.
    /// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/client/player/LocalPlayer.java:248-286
    /// </summary>
    private async Task SendPositionAsync(Entity entity, IPacketSender packetSender)
    {
        double deltaX = entity.Position.X - entity.LastSentPosition.X;
        double deltaY = entity.Position.Y - entity.LastSentPosition.Y;
        double deltaZ = entity.Position.Z - entity.LastSentPosition.Z;
        double deltaYRot = entity.YawPitch.X - entity.LastSentYawPitch.X;
        double deltaXRot = entity.YawPitch.Y - entity.LastSentYawPitch.Y;
        
        entity.PositionReminder++;
        bool move = Math.Sqrt(deltaX * deltaX + deltaY * deltaY + deltaZ * deltaZ) > PositionUpdateThreshold || 
                    entity.PositionReminder >= PositionReminderInterval;
        bool rot = Math.Abs(deltaYRot) > 1.0E-7 || Math.Abs(deltaXRot) > 1.0E-7;
        
        var flags = MovementFlags.None;
        if (entity.IsOnGround) flags |= MovementFlags.OnGround;
        if (entity.HorizontalCollision) flags |= MovementFlags.HorizontalCollision;
        
        if (move && rot)
        {
            await packetSender.SendPacketAsync(new MovePlayerPositionRotationPacket
            {
                X = entity.Position.X,
                Y = entity.Position.Y,
                Z = entity.Position.Z,
                Yaw = entity.YawPitch.X,
                Pitch = entity.YawPitch.Y,
                Flags = flags
            });
        }
        else if (move)
        {
            await packetSender.SendPacketAsync(new MovePlayerPositionPacket
            {
                X = entity.Position.X,
                Y = entity.Position.Y,
                Z = entity.Position.Z,
                Flags = flags
            });
        }
        else if (rot)
        {
            await packetSender.SendPacketAsync(new MovePlayerRotationPacket
            {
                Yaw = entity.YawPitch.X,
                Pitch = entity.YawPitch.Y,
                Flags = flags
            });
        }
        else if (entity.IsOnGround != entity.LastSentOnGround || 
                 entity.HorizontalCollision != entity.LastSentHorizontalCollision)
        {
            // Status-only packet (no position/rotation change)
            // Note: Minecraft sends a status-only packet, but we don't have that packet type.
            // For now, we'll send a position packet with same position but updated flags.
            await packetSender.SendPacketAsync(new MovePlayerPositionPacket
            {
                X = entity.LastSentPosition.X,
                Y = entity.LastSentPosition.Y,
                Z = entity.LastSentPosition.Z,
                Flags = flags
            });
        }
        
        // Update last sent values
        if (move)
        {
            entity.LastSentPosition = entity.Position;
            entity.PositionReminder = 0;
        }
        
        if (rot)
        {
            entity.LastSentYawPitch = entity.YawPitch;
        }
        
        entity.LastSentOnGround = entity.IsOnGround;
        entity.LastSentHorizontalCollision = entity.HorizontalCollision;
    }
}
