using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

public class SetHealthPacket : Packet
{
    public override int PacketId => 0x62;
    public override PacketDirection Direction => PacketDirection.Clientbound;

    public float Health { get; set; }
    public int Food { get; set; }
    public float FoodSaturation { get; set; }

    public override void Deserialize(ref PacketBufferReader buffer)
    {
        Health = buffer.ReadFloat();
        Food = buffer.ReadVarInt();
        FoodSaturation = buffer.ReadFloat();
    }
}
