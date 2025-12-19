using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

[Packet(0x67, ProtocolState.Play)]
public class SetHealthPacket : IClientboundPacket
{
    public float Health { get; set; }
    public int Food { get; set; }
    public float FoodSaturation { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        Health = buffer.ReadFloat();
        Food = buffer.ReadVarInt();
        FoodSaturation = buffer.ReadFloat();
    }
}
