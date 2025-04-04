﻿using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

// TODO: Partially implemented.
[Packet(0x1F, ProtocolState.Play, true)]
public class EntityEventPacket : IClientPacket
{
    public int EntityId { get; set; }
    public sbyte EntityStatus { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        EntityId = buffer.ReadSignedInt();
        EntityStatus = buffer.ReadSignedByte();
    }
}
