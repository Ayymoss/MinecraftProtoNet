using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

[Packet(0x63, ProtocolState.Play)]
public class SetHeldSlotPacket : IClientPacket
{
    public int HandHeldSlot { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        HandHeldSlot = buffer.ReadVarInt();
    }
}
