using System.ComponentModel.DataAnnotations;
using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Login.Serverbound;

[Packet(0x00, ProtocolState.Login)]
public class HelloPacket : IServerboundPacket
{
    [Length(3, 16)] public required string Username { get; set; } = string.Empty;
    public Guid? Uuid { get; set; }

    public void Serialize(ref PacketBufferWriter buffer)
    {
        

        buffer.WriteString(Username);

        if (Uuid.HasValue)
        {
            buffer.WriteUUID(Uuid.Value);
        }
        else
        {
            // Protocol 775+ stricter requirement: always send UUID. 
            // Ideally AuthResult provides this, but fallback to Empty if offline/missing.
            buffer.WriteUUID(Guid.Empty);
        }
    }
}
