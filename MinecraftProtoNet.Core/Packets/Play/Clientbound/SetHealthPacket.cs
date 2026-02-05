using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Clientbound;

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
