﻿using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Enums;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Services;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Serverbound;

[Packet(0x1E, ProtocolState.Play)]
public class MovePlayerRotationPacket : IServerPacket
{
    public required float Yaw { get; set; }
    public required float Pitch { get; set; }
    public required MovementFlags Flags { get; set; }
    public void Serialize(ref PacketBufferWriter buffer)
    {
        buffer.WriteVarInt(this.GetPacketAttributeValue(p => p.PacketId));
        buffer.WriteFloat(Yaw);
        buffer.WriteFloat(Pitch);
        buffer.WriteUnsignedByte((byte)Flags);
    }
}
