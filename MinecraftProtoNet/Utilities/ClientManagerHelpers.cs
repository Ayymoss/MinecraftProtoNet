using MinecraftProtoNet.Core;
using MinecraftProtoNet.Enums;
using MinecraftProtoNet.Models.Core;
using MinecraftProtoNet.Packets.Play.Serverbound;

namespace MinecraftProtoNet.Utilities;

public static class ClientManagerHelpers
{
    public static void InterpolateToCoordinates(IMinecraftClient client, Vector3<double> targetPosition, float speed = 0.25f)
    {
        var stoppingDistance = float.Max(0.25f, speed);

        _ = Task.Run(async () =>
        {
            while (true)
            {
                if (client.State.LocalPlayer.Entity is not { } entity) return;
                var currentPosition = entity.Position;
                var direction = new Vector3<double>(
                    targetPosition.X - currentPosition.X,
                    targetPosition.Y - currentPosition.Y,
                    targetPosition.Z - currentPosition.Z);

                var distance = direction.Length();
                if (distance <= stoppingDistance) break;

                direction.X /= distance;
                direction.Y /= distance;
                direction.Z /= distance;

                var newPosition = currentPosition + direction * speed;
                var targetYaw = CalculateYawToTarget(currentPosition, targetPosition);
                entity.YawPitch.X = NormalizeYaw(targetYaw);
                var pitchDegrees = entity.YawPitch.Y;

                var result = new MovePlayerPositionRotationPacket
                {
                    X = newPosition.X,
                    Y = newPosition.Y,
                    Z = newPosition.Z,
                    Yaw = (float)entity.YawPitch.X,
                    Pitch = (float)pitchDegrees,
                    Flags = MovementFlags.None
                };

                entity.Position.X = result.X;
                entity.Position.Y = result.Y;
                entity.Position.Z = result.Z;

                await client.SendPacketAsync(result);
                await Task.Delay(20);
            }
        });
    }

    private static float NormalizeYaw(float yaw)
    {
        yaw %= 360;
        switch (yaw)
        {
            case > 180:
                yaw -= 360;
                break;
            case <= -180:
                yaw += 360;
                break;
        }

        return yaw;
    }

    private static float CalculateYawToTarget(Vector3<double> currentPosition, Vector3<double> targetPosition)
    {
        var deltaX = targetPosition.X - currentPosition.X;
        var deltaZ = targetPosition.Z - currentPosition.Z;

        var yaw = (float)(Math.Atan2(-deltaX, deltaZ) * (180 / Math.PI));
        return NormalizeYaw(yaw);
    }
}
