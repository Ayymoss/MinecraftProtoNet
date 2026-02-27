using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Models.Player;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Login.Clientbound;

// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/network/protocol/login/ClientboundLoginFinishedPacket.java
// Codec: ByteBufCodecs.GAME_PROFILE -> UUID, player name (max 16), properties (max 16 entries)
[Packet(0x02, ProtocolState.Login)]
public class LoginFinishedPacket : IClientboundPacket
{
    public Guid UUID { get; set; }
    public string Username { get; set; } = string.Empty;
    public required Property[] Properties { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        UUID = buffer.ReadUuid();
        Username = buffer.ReadString();
        var count = buffer.ReadVarInt();
        Properties = new Property[count];
        for (var i = 0; i < count; i++)
        {
            Properties[i] = new Property
            {
                Name = buffer.ReadString(),
                Value = buffer.ReadString(),
                Signature = buffer.ReadBoolean() ? buffer.ReadString() : null
            };
        }
    }
}
