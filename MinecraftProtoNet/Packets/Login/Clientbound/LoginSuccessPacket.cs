using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Models.Player;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Login.Clientbound;

[Packet(0x02, ProtocolState.Login)]
public class LoginSuccessPacket : IClientboundPacket
{
    public Guid UUID { get; set; }
    public string Username { get; set; } = string.Empty;
    public Property[] Properties { get; set; }

    // TODO: This packet needs to be revised for latest protocol (1.21.4)
    public void Deserialize(ref PacketBufferReader buffer)
    {
        UUID = buffer.ReadUuid();
        Username = buffer.ReadString();
        var count = buffer.ReadVarInt();
        Properties = new Property[count];
        for (int i = 0; i < count; i++)
        {
            Properties[i] = new Property
            {
                Name = buffer.ReadString(),
                Value = buffer.ReadString(),
                Signature = buffer.ReadString()
            };
        }
    }
}
