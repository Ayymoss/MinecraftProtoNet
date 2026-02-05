using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Models.Player;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Login.Clientbound;

[Packet(0x02, ProtocolState.Login)]
public class LoginSuccessPacket : IClientboundPacket
{
    public Guid UUID { get; set; }
    public string Username { get; set; } = string.Empty;
    public required Property[] Properties { get; set; }

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
