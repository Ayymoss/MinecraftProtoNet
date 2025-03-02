using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Handlers;
using MinecraftProtoNet.Handlers.Base;
using MinecraftProtoNet.Models.Core;
using MinecraftProtoNet.Packets.Play.Serverbound;
using MinecraftProtoNet.Services;

namespace Bot;

public static class Program
{
    public static async Task Main()
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        var serviceProvider = services.BuildServiceProvider();

        var client = serviceProvider.GetRequiredService<IMinecraftClient>();

        await client.ConnectAsync("10.10.1.20", 25555);


        while (true)
        {
            var coords = Console.ReadLine();
            if (coords?.Split(' ').Length != 3) continue;
            var coordsArray = coords.Split(' ').Select(float.Parse).ToArray();
            var targetPosition = new Vector3F(coordsArray[0], coordsArray[1], coordsArray[2]);
            await InterpolateToCoordinates(client, targetPosition);

            //var direction = Console.ReadKey();
            //await Move(client, direction.Key);
        }


        //Console.WriteLine("Press Enter to disconnect...");
        Console.ReadKey();

        await client.DisconnectAsync();
    }

    // TODO: It should have an internal service collection
    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<Connection>();
        services.AddSingleton<MinecraftClientState>();
        services.AddSingleton<IPacketService, PacketService>();
        services.AddSingleton<IMinecraftClient, MinecraftClient>();

        services.AddSingleton<IPacketHandler, StatusHandler>();
        services.AddSingleton<IPacketHandler, LoginHandler>();
        services.AddSingleton<IPacketHandler, ConfigurationHandler>();
        services.AddSingleton<IPacketHandler, PlayHandler>();
    }

    private static async Task InterpolateToCoordinates(IMinecraftClient client, Vector3F targetPosition)
    {
        const float baseSpeed = 0.4f;
        const float stoppingDistance = 0.5f;

        while (true)
        {
            var currentPosition = client.ClientState.Position;
            var direction = new Vector3F(
                targetPosition.X - currentPosition.X,
                targetPosition.Y - currentPosition.Y,
                targetPosition.Z - currentPosition.Z);

            var distance = direction.Length();
            if (distance <= stoppingDistance) break;

            direction.X /= distance;
            direction.Y /= distance;
            direction.Z /= distance;

            var newPosition = currentPosition + direction * baseSpeed;
            var targetYaw = CalculateYawToTarget(currentPosition, targetPosition);
            client.ClientState.Rotation.X = NormalizeYaw(targetYaw);
            var pitchDegrees = client.ClientState.Rotation.Y;

            var result = new MovePlayerPositionRotationPacket
            {
                Payload = new MovePlayerPositionRotationPacket.MovePlayerPositionRotation
                {
                    X = newPosition.X,
                    Y = newPosition.Y,
                    Z = newPosition.Z,
                    Yaw = client.ClientState.Rotation.X,
                    Pitch = pitchDegrees,
                    MovementFlags = MovePlayerPositionRotationPacket.MovementFlags.None
                }
            };

            client.ClientState.Position.X = (float)result.Payload.X;
            client.ClientState.Position.Y = (float)result.Payload.Y;
            client.ClientState.Position.Z = (float)result.Payload.Z;

            await client.SendPacketAsync(result);
            await Task.Delay(20);
        }
    }

    private static async Task Move(IMinecraftClient client, ConsoleKey directionKey)
    {
        const float baseSpeed = 0.1f;
        const float rotationIncrementDegrees = 5.0f;

        var moveVector = new Vector3F(0, 0, 0);
        var yawDegrees = client.ClientState.Rotation.X;
        var pitchDegrees = client.ClientState.Rotation.Y;

        var yawRadians = DegreesToRadians(yawDegrees);
        var sinYaw = (float)Math.Sin(yawRadians);
        var cosYaw = (float)Math.Cos(yawRadians);

        var forwardVector = new Vector3F(-sinYaw, 0, cosYaw);
        var leftVector = new Vector3F(cosYaw, 0, sinYaw);

        forwardVector = NormalizeXZ(forwardVector);
        leftVector = NormalizeXZ(leftVector);

        switch (directionKey)
        {
            case ConsoleKey.W:
                moveVector += forwardVector;
                break;
            case ConsoleKey.S:
                moveVector -= forwardVector;
                break;
            case ConsoleKey.A:
                moveVector += leftVector;
                break;
            case ConsoleKey.D:
                moveVector -= leftVector;
                break;
            case ConsoleKey.Q:
                moveVector.Y += baseSpeed * 10;
                break;
            case ConsoleKey.E:
                moveVector.Y -= baseSpeed * 10;
                break;
            case ConsoleKey.Z:
                yawDegrees -= rotationIncrementDegrees;
                break;
            case ConsoleKey.X:
                yawDegrees += rotationIncrementDegrees;
                break;
        }

        yawDegrees = NormalizeYaw(yawDegrees);

        var horizontalMove = new Vector3F(moveVector.X, 0, moveVector.Z);
        if (horizontalMove.Length() > 1)
        {
            horizontalMove = NormalizeXZ(horizontalMove);
            moveVector.X = horizontalMove.X;
            moveVector.Z = horizontalMove.Z;
        }

        var newPosition = client.ClientState.Position + moveVector * baseSpeed;
        client.ClientState.Rotation.X = yawDegrees;

        var result = new MovePlayerPositionRotationPacket
        {
            Payload = new MovePlayerPositionRotationPacket.MovePlayerPositionRotation
            {
                X = newPosition.X,
                Y = newPosition.Y,
                Z = newPosition.Z,
                Yaw = yawDegrees,
                Pitch = pitchDegrees,
                MovementFlags = MovePlayerPositionRotationPacket.MovementFlags.None
            }
        };

        client.ClientState.Position.X = (float)result.Payload.X;
        client.ClientState.Position.Y = (float)result.Payload.Y;
        client.ClientState.Position.Z = (float)result.Payload.Z;

        await client.SendPacketAsync(result);
    }

    private static float DegreesToRadians(float degrees)
    {
        return degrees * (float)Math.PI / 180f;
    }

    private static Vector3F NormalizeXZ(Vector3F vector)
    {
        var lengthXZ = (float)Math.Sqrt(vector.X * vector.X + vector.Z * vector.Z);
        return lengthXZ > 1e-5f ? new Vector3F(vector.X / lengthXZ, vector.Y, vector.Z / lengthXZ) : vector;
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

    private static float CalculateYawToTarget(Vector3F currentPosition, Vector3F targetPosition)
    {
        double deltaX = targetPosition.X - currentPosition.X;
        double deltaZ = targetPosition.Z - currentPosition.Z;

        var yaw = (float)(Math.Atan2(-deltaX, deltaZ) * (180 / Math.PI)); // Note the -deltaX for Minecraft's yaw convention
        return NormalizeYaw(yaw);
    }
}
