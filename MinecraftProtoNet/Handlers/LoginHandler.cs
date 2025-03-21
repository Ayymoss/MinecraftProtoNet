﻿using MinecraftProtoNet.Core;
using MinecraftProtoNet.Handlers.Base;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Packets.Login.Clientbound;
using MinecraftProtoNet.Packets.Login.Serverbound;
using Spectre.Console;

namespace MinecraftProtoNet.Handlers;

public class LoginHandler : IPacketHandler
{
    public IEnumerable<(ProtocolState State, int PacketId)> RegisteredPackets =>
    [
        (ProtocolState.Login, 0x00), //Disconnect
        (ProtocolState.Login, 0x02) //Login Success
    ];

    public async Task HandleAsync(IClientPacket packet, IMinecraftClient client)
    {
        switch (packet)
        {
            case LoginSuccessPacket loginSuccess:
                await client.SendPacketAsync(new LoginAcknowledgedPacket());
                client.ProtocolState = ProtocolState.Configuration;
                AnsiConsole.MarkupLine($"[grey][[DEBUG]][/] [fuchsia]SWITCHING PROTOCOL STATE:[/] [cyan]{client.ProtocolState.ToString()}[/]");
                break;
        }
    }
}
