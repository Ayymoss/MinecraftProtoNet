﻿using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

[Packet(0x21, ProtocolState.Play, true)]
public class ForgetLevelChunkPacket : IClientboundPacket
{
    public int ChunkX { get; set; }
    public int ChunkZ { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        ChunkX = buffer.ReadSignedInt();
        ChunkZ = buffer.ReadSignedInt();
    }
}
