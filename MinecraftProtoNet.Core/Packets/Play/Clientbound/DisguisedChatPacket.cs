using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.NBT.Tags;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Clientbound;

/// <summary>
/// A chat packet for messages that are "disguised" (e.g., from a command or a specific chat type).
/// Mirrors ClientboundDisguisedChatPacket.java
/// </summary>
[Packet(0x21, ProtocolState.Play)]
public class DisguisedChatPacket : IClientboundPacket
{
    public required NbtTag Message { get; set; }
    public required ChatTypeBound ChatType { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        Message = buffer.ReadNbtTag();
        
        ChatType = new ChatTypeBound
        {
            ChatTypeId = buffer.ReadVarInt(),
            Name = buffer.ReadNbtTag(),
            TargetName = buffer.ReadBoolean() ? buffer.ReadNbtTag() : null
        };
    }

    public class ChatTypeBound
    {
        public required int ChatTypeId { get; set; }
        public required NbtTag Name { get; set; }
        public required NbtTag? TargetName { get; set; }

        public override string ToString()
        {
            return $"{{ChatTypeId={ChatTypeId}, Name={Name}, TargetName={TargetName}}}";
        }
    }

    public override string ToString()
    {
        return $"DisguisedChatPacket{{Message={Message}, ChatType={ChatType}}}";
    }
}
