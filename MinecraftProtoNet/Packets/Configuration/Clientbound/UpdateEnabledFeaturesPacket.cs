using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Configuration.Clientbound;

public class UpdateEnabledFeaturesPacket : Packet
{
    public override int PacketId => 0x0C;
    public override PacketDirection Direction => PacketDirection.Clientbound;

    public string[] FeatureFlags { get; set; }

    public override void Deserialize(ref PacketBufferReader buffer)
    {
        var count = buffer.ReadVarInt();
        FeatureFlags = new string[count];
        for (var i = 0; i < count; i++)
        {
            FeatureFlags[i] = buffer.ReadString();
        }
    }
}
