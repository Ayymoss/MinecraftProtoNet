using MinecraftProtoNet.Enums;
using MinecraftProtoNet.Models.Core;
using MinecraftProtoNet.Packets.Play.Serverbound;

namespace MinecraftProtoNet.Core;

public partial class MinecraftClient
{
    // TODO: Clean this method up. 
    public async Task PhysicsTickAsync()
    {
        if (!State.LocalPlayer.HasEntity) return;

        const double gravity = -0.08;
        const double dragMultiplier = 0.98;
        const double groundFriction = 0.6;
        const double terminalVelocity = -3.92;
        const double jumpVelocity = 0.42;
        const double playerCollisionPushStrength = 0.06;
        const double knockbackBaseStrength = 0.4;
        const double knockbackVerticalBoost = 0.1;
        const double playerBoundingBoxWidth = 0.6;
        const double playerBoundingBoxHeight = 1.8;
        const double playerCollisionRange = playerBoundingBoxWidth * 2.0;
        const double halfWidth = playerBoundingBoxWidth / 2.0;

        var entity = State.LocalPlayer.Entity;
        var level = State.Level;

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

            var dy = entity.Position.Y - otherEntity.Position.Y;
            if (Math.Abs(dy) > 1.0) continue;

            var dx = entity.Position.X - otherEntity.Position.X;
            var dz = entity.Position.Z - otherEntity.Position.Z;
            var distanceSquared = dx * dx + dz * dz;
            if (!(distanceSquared < playerCollisionRange * playerCollisionRange)) continue;

            var pushDistance = Math.Sqrt(distanceSquared);
            if (pushDistance < 0.01)
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
            var lookingYaw = entity.YawPitch.X + 90;
            var hurtFromYaw = entity.IsHurtFromYaw.Value;

            var lookingRadians = lookingYaw * (Math.PI / 180);
            var hurtRadians = hurtFromYaw * (Math.PI / 180);
            var attackAngle = lookingRadians + hurtRadians;

            entity.Velocity.X += -Math.Sin(attackAngle) * knockbackBaseStrength;
            entity.Velocity.Z += Math.Cos(attackAngle) * knockbackBaseStrength;
            entity.Velocity.Y += knockbackVerticalBoost;

            entity.IsHurt = false;
            entity.IsHurtFromYaw = null;
        }

        var targetX = entity.Position.X + entity.Velocity.X;
        var targetY = entity.Position.Y + entity.Velocity.Y;
        var targetZ = entity.Position.Z + entity.Velocity.Z;

        var collidedX = false;
        var collidedZ = false;

        if (Math.Abs(entity.Velocity.X) > 0.0001)
        {
            var dirX = entity.Velocity.X > 0 ? 1 : -1;
            var checkDistanceX = Math.Abs(entity.Velocity.X) + 0.05;
            var xCollision = CheckHorizontalCollision(entity.Position, new Vector3<double>(dirX, 0, 0), checkDistanceX,
                halfWidth);

            if (xCollision != null)
            {
                var xDist = Math.Abs(xCollision.Value - entity.Position.X) - halfWidth - 0.01;
                if (xDist < Math.Abs(entity.Velocity.X))
                {
                    targetX = entity.Position.X + (xDist * dirX);
                    entity.Velocity.X = 0;
                    collidedX = true;
                }
            }
        }

        if (Math.Abs(entity.Velocity.Z) > 0.0001)
        {
            var dirZ = entity.Velocity.Z > 0 ? 1 : -1;
            var checkDistanceZ = Math.Abs(entity.Velocity.Z) + 0.05;
            var zCollision = CheckHorizontalCollision(entity.Position, new Vector3<double>(0, 0, dirZ), checkDistanceZ,
                halfWidth);

            if (zCollision is not null)
            {
                var zDist = Math.Abs(zCollision.Value - entity.Position.Z) - halfWidth - 0.01;
                if (zDist < Math.Abs(entity.Velocity.Z))
                {
                    targetZ = entity.Position.Z + (zDist * dirZ);
                    entity.Velocity.Z = 0;
                    collidedZ = true;
                }
            }
        }

        if (entity.Velocity.Y < 0)
        {
            var checkDistance = Math.Abs(entity.Velocity.Y) + 0.05;
            var foundGround = false;

            Vector3<double>[] cornerOffsets =
            [
                new(-halfWidth, 0, -halfWidth), // Back left
                new(-halfWidth, 0, halfWidth), // Front left
                new(halfWidth, 0, -halfWidth), // Back right
                new(halfWidth, 0, halfWidth) // Front right
            ];

            var highestFloorY = double.MinValue;

            foreach (var offset in cornerOffsets)
            {
                var checkPos = new Vector3<double>(
                    entity.Position.X + offset.X,
                    entity.Position.Y,
                    entity.Position.Z + offset.Z
                );

                var rayResult = level.RayCast(
                    checkPos,
                    new Vector3<double>(0, -1, 0),
                    checkDistance
                );

                if (rayResult == null || rayResult.Block.IsAir || rayResult.Block.IsLiquid) continue;

                var blockY = rayResult.BlockPosition.Y;
                var floorY = blockY + 1.0;

                if (floorY > highestFloorY)
                {
                    highestFloorY = floorY;
                }

                foundGround = true;
            }

            if (foundGround && targetY < highestFloorY)
            {
                targetY = highestFloorY;
                entity.Velocity.Y = 0;
                entity.IsOnGround = true;
            }
        }
        else if (entity.Velocity.Y > 0)
        {
            var checkDistance = Math.Abs(entity.Velocity.Y) + 0.05;
            Vector3<double>[] ceilingCheckOffsets =
            [
                new(-halfWidth, playerBoundingBoxHeight, -halfWidth),
                new(-halfWidth, playerBoundingBoxHeight, halfWidth),
                new(halfWidth, playerBoundingBoxHeight, -halfWidth),
                new(halfWidth, playerBoundingBoxHeight, halfWidth)
            ];

            var lowestCeilingY = double.MaxValue;
            var foundCeiling = false;

            foreach (var offset in ceilingCheckOffsets)
            {
                var checkPos = new Vector3<double>(
                    entity.Position.X + offset.X,
                    entity.Position.Y + offset.Y,
                    entity.Position.Z + offset.Z
                );

                var rayResult = level.RayCast(
                    checkPos,
                    new Vector3<double>(0, 1, 0),
                    checkDistance
                );

                if (rayResult == null || rayResult.Block.IsAir || rayResult.Block.IsLiquid) continue;

                var blockY = rayResult.BlockPosition.Y;
                if (blockY < lowestCeilingY)
                {
                    lowestCeilingY = blockY;
                }

                foundCeiling = true;
            }

            if (foundCeiling && targetY + playerBoundingBoxHeight > lowestCeilingY)
            {
                targetY = lowestCeilingY - playerBoundingBoxHeight - 0.01;
                entity.Velocity.Y = 0;
            }
        }

        // We haven't moved.
        if (Math.Abs(entity.Position.X - targetX) < 0.0001 && Math.Abs(entity.Position.Y - targetY) < 0.0001 &&
            Math.Abs(entity.Position.Z - targetZ) < 0.0001)
        {
            return;
        }

        entity.Position.X = targetX;
        entity.Position.Y = targetY;
        entity.Position.Z = targetZ;

        if (entity.Velocity.Y != 0)
        {
            entity.IsOnGround = entity.CheckIsOnGround(level);
        }

        var flags = MovementFlags.None;
        if (entity.IsOnGround)
        {
            flags = MovementFlags.OnGround;
        }
        else if ((collidedX || collidedZ) && (Math.Abs(entity.Velocity.X) > 0.0001 || Math.Abs(entity.Velocity.Z) > 0.0001))
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

    private double? CheckHorizontalCollision(Vector3<double> position, Vector3<double> direction, double checkDistance, double halfWidth)
    {
        double[] heightOffsets = [0.1, 0.5, 1.0, 1.7]; // Bottom, knees, torso, head
        Vector3<double>[] cornerOffsets;

        if (direction.X > 0)
        {
            cornerOffsets =
            [
                new Vector3<double>(halfWidth, 0, -halfWidth),
                new Vector3<double>(halfWidth, 0, halfWidth)
            ];
        }
        else if (direction.X < 0)
        {
            cornerOffsets =
            [
                new Vector3<double>(-halfWidth, 0, -halfWidth),
                new Vector3<double>(-halfWidth, 0, halfWidth)
            ];
        }
        else if (direction.Z > 0)
        {
            cornerOffsets =
            [
                new Vector3<double>(-halfWidth, 0, halfWidth),
                new Vector3<double>(halfWidth, 0, halfWidth)
            ];
        }
        else if (direction.Z < 0)
        {
            cornerOffsets =
            [
                new Vector3<double>(-halfWidth, 0, -halfWidth),
                new Vector3<double>(halfWidth, 0, -halfWidth)
            ];
        }
        else
        {
            return null;
        }

        double? nearestCollision = null;

        foreach (var offset in cornerOffsets)
        {
            foreach (var heightOffset in heightOffsets)
            {
                var checkPos = new Vector3<double>(
                    position.X + offset.X,
                    position.Y + heightOffset,
                    position.Z + offset.Z
                );

                var rayResult = State.Level.RayCast(checkPos, direction, checkDistance);
                if (rayResult == null || rayResult.Block.IsAir || rayResult.Block.IsLiquid) continue;

                double collisionPoint;
                if (direction.X != 0)
                {
                    collisionPoint = rayResult.BlockPosition.X + (direction.X > 0 ? 0 : 1);
                    if (nearestCollision == null ||
                        Math.Abs(collisionPoint - position.X) < Math.Abs(nearestCollision.Value - position.X))
                    {
                        nearestCollision = collisionPoint;
                    }
                }
                else
                {
                    collisionPoint = rayResult.BlockPosition.Z + (direction.Z > 0 ? 0 : 1);
                    if (nearestCollision == null ||
                        Math.Abs(collisionPoint - position.Z) < Math.Abs(nearestCollision.Value - position.Z))
                    {
                        nearestCollision = collisionPoint;
                    }
                }
            }
        }

        return nearestCollision;
    }
}
