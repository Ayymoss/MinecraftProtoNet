using MinecraftProtoNet.Enums;
using MinecraftProtoNet.Models.Core;
using MinecraftProtoNet.Packets.Play.Serverbound;

namespace MinecraftProtoNet.Core;

public partial class MinecraftClient
{
    // TODO: Clean this method up. 
    // TODO: Ground detection doesn't work when center is over the edge of a block.
    public async Task PhysicsTickAsync()
    {
        if (!State.LocalPlayer.HasEntity) return;

        var entity = State.LocalPlayer.Entity;
        var level = State.Level;

        const double gravity = -0.08;
        const double dragMultiplier = 0.98;
        const double groundFriction = 0.6;
        const double terminalVelocity = -3.92;
        const double jumpVelocity = 0.42;

        const double playerCollisionPushStrength = 0.06;
        const double knockbackBaseStrength = 0.4;
        const double knockbackVerticalBoost = 0.1;
        const double playerBoundingBoxSize = 0.6;
        const double playerCollisionRange = playerBoundingBoxSize * 2.0;

        entity.IsOnGround = entity.CheckIsOnGround(level);

        if (entity is { IsJumping: true, IsOnGround: true })
        {
            entity.Velocity.Y = jumpVelocity;
            entity.IsJumping = false;
            entity.IsOnGround = false;
        }
        else if (!entity.IsOnGround)
        {
            entity.Velocity.X *= dragMultiplier;
            entity.Velocity.Z *= dragMultiplier;

            entity.Velocity.Y += gravity;

            if (entity.Velocity.Y < terminalVelocity)
            {
                entity.Velocity.Y = terminalVelocity;
            }
        }
        else
        {
            if (entity.Velocity.Y < 0)
            {
                entity.Velocity.Y = 0;
            }

            entity.Velocity.X *= groundFriction;
            entity.Velocity.Z *= groundFriction;
        }

        var allEntityIds = level.GetAllEntityIds();
        foreach (var otherId in allEntityIds)
        {
            if (otherId == entity.EntityId) continue;

            var otherEntity = level.GetEntityOfId(otherId);
            if (otherEntity == null) continue;

            var dx = entity.Position.X - otherEntity.Position.X;
            var dz = entity.Position.Z - otherEntity.Position.Z;
            var distanceSquared = dx * dx + dz * dz;

            if (!(distanceSquared < playerCollisionRange * playerCollisionRange)) continue;

            var pushDistance = Math.Sqrt(distanceSquared);
            if (pushDistance < 0.01) // Avoid division by almost-zero
            {
                var randomAngle = new Random().NextDouble() * 2 * Math.PI;
                dx = Math.Cos(randomAngle);
                dz = Math.Sin(randomAngle);
                pushDistance = 0.01;
            }

            var pushDirectionX = dx / pushDistance;
            var pushDirectionZ = dz / pushDistance;

            var pushStrength = playerCollisionPushStrength * (playerCollisionRange - pushDistance) / playerCollisionRange;

            entity.Velocity.X += pushDirectionX * pushStrength;
            entity.Velocity.Z += pushDirectionZ * pushStrength;
        }

        if (entity.IsHurt)
        {
            var knockbackYaw = entity.IsHurtFromYaw.Value; // Reverse the direction
            var knockbackYawRad = Math.PI / 180 * knockbackYaw;
            var knockbackX = -Math.Sin(knockbackYawRad) * knockbackBaseStrength;
            var knockbackZ = Math.Cos(knockbackYawRad) * knockbackBaseStrength;

            entity.Velocity.X += knockbackX;
            entity.Velocity.Z += knockbackZ;
            entity.Velocity.Y += knockbackVerticalBoost;

            entity.IsHurt = false;
            entity.IsHurtFromYaw = null;
        }

        var targetX = entity.Position.X + entity.Velocity.X;
        var targetY = entity.Position.Y + entity.Velocity.Y;
        var targetZ = entity.Position.Z + entity.Velocity.Z;

        if (entity.Velocity.Y < 0)
        {
            var checkDistance = Math.Abs(entity.Velocity.Y) + 0.05;
            var rayResult = level.RayCast(
                entity.Position,
                new Vector3<double>(0, -1, 0),
                checkDistance
            );

            if (rayResult != null && !rayResult.Block.IsAir && !rayResult.Block.IsLiquid)
            {
                var blockY = rayResult.BlockPosition.Y;
                var floorY = blockY + 1.0;

                if (targetY < floorY)
                {
                    targetY = floorY;
                    entity.Velocity.Y = 0;
                    entity.IsOnGround = true;
                }
            }
        }

        entity.Position.X = targetX;
        entity.Position.Y = targetY;
        entity.Position.Z = targetZ;

        if (entity.Velocity.Y != 0)
        {
            entity.IsOnGround = entity.CheckIsOnGround(level);
        }

        // Determine movement flags
        var flags = MovementFlags.None;
        if (entity.IsOnGround)
        {
            flags = MovementFlags.OnGround;
        }
        // Check if pushing against wall
        else if (Math.Abs(entity.Velocity.X) < 0.003 && Math.Abs(entity.Velocity.Z) < 0.003 &&
                 (Math.Abs(entity.Velocity.X) > 0.0001 || Math.Abs(entity.Velocity.Z) > 0.0001))
        {
            flags = MovementFlags.PushingAgainstWall;
        }

        await SendPacketAsync(new MovePlayerPositionRotationPacket
        {
            X = entity.Position.X,
            Y = entity.Position.Y,
            Z = entity.Position.Z,
            Yaw = entity.YawPitch.X,
            Pitch = entity.YawPitch.Y,
            Flags = flags
        });
    }
}
