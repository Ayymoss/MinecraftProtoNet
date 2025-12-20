using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Services;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Serverbound;

[Packet(0x06, ProtocolState.Play)]
public class ChatCommandPacket(string command) : IServerboundPacket
{
    public string Command { get; set; } = command;

    public void Serialize(ref PacketBufferWriter buffer)
    {
        buffer.WriteString(Command);
    }
}
