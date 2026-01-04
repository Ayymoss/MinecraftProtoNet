using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Configuration.Clientbound;

[Packet(0x0C, ProtocolState.Configuration)]
public class UpdateEnabledFeaturesPacket : IClientboundPacket
{
    public string[] FeatureFlags { get; set; } = [];

    public void Deserialize(ref PacketBufferReader buffer)
    {
        var count = buffer.ReadVarInt();
        FeatureFlags = new string[count];
        for (var i = 0; i < count; i++)
        {
            FeatureFlags[i] = buffer.ReadString();
        }
    }
}
