using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

[Packet(0x68, ProtocolState.Play)]
public class SetHeldSlotPacket : IClientboundPacket
{
    public short HeldSlot { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        HeldSlot = (short)buffer.ReadVarInt();
    }
}
