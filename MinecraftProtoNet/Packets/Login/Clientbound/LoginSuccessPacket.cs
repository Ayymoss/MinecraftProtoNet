using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Login.Clientbound;

public class LoginSuccessPacket : Packet
{
    public override int PacketId => 0x02;
    public override PacketDirection Direction => PacketDirection.Clientbound;
    public Guid UUID { get; set; }
    public string Username { get; set; } = string.Empty;
    public Property[] Properties { get; set; }

    // TODO: This packet needs to be revised for latest protocol (1.21.4)
    public override void Deserialize(ref PacketBufferReader buffer)
    {
        UUID = buffer.ReadUUID();
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
    
    public class Property
    {
        public string? Name { get; set; }
        public string? Value { get; set; }
        public string? Signature { get; set; }
    }
}
