﻿using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

[Packet(0x57, ProtocolState.Play)]
public class SetChunkCacheCenterPacket : IClientboundPacket
{
    public int ChunkX { get; set; }
    public int ChunkY { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        ChunkX = buffer.ReadVarInt();
        ChunkY = buffer.ReadVarInt();
    }
}
