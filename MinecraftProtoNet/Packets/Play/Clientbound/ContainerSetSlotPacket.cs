﻿using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Packets.Base.Definitions;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

[Packet(0x15, ProtocolState.Play)]
public class ContainerSetSlotPacket : IClientPacket
{
    public int WindowId { get; set; }
    public int StateId { get; set; }
    public short SlotToUpdate { get; set; }
    public Slot Slot { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        WindowId = buffer.ReadVarInt();
        StateId = buffer.ReadVarInt();
        SlotToUpdate = buffer.ReadSignedShort();

        Slot = Slot.Read(ref buffer);
    }
}
