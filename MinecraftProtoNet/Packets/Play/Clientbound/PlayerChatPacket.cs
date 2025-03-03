using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.NBT.Tags;
using MinecraftProtoNet.NBT.Tags.Abstract;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

[Packet(0x3B, ProtocolState.Play)]
public class PlayerChatPacket : IClientPacket
{
    public void Deserialize(ref PacketBufferReader buffer)
    {
        
    }

    public class Header
    {
        
    }

    public class Body
    {
        
    }

    public class MessageValidation
    {
        
    }

    public class Other
    {
    }

    public class Formatting
    {
        public int Type { get; set; }
        public NbtTag SenderName { get; set; } = new NbtEnd();
        public NbtTag? TargetName { get; set; }
    }
}
