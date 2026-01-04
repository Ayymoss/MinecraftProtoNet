using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Serverbound;

[Packet(0x06, ProtocolState.Play)]
public class ChatCommandPacket(string command) : IServerboundPacket
{
    public string Command { get; set; } = command;

    public void Serialize(ref PacketBufferWriter buffer)
    {
        buffer.WriteString(Command);
    }
}
