using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Clientbound;

[Packet(0x68, ProtocolState.Play)]
public class SetHeldSlotPacket : IClientboundPacket
{
    public short HeldSlot { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        HeldSlot = (short)buffer.ReadVarInt();
    }
}
