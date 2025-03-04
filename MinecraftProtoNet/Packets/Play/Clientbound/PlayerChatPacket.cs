using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.NBT.Tags;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

[Packet(0x3B, ProtocolState.Play)]
public class PlayerChatPacket : IClientPacket
{
    public HeaderPayload Header { get; set; }
    public BodyPayload Body { get; set; }
    public MessageValidationPayload[] Validations { get; set; }
    public OtherPayload Other { get; set; }
    public FormattingPayload Formatting { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        // Header
        Header = new HeaderPayload
        {
            Uuid = buffer.ReadUUID(),
            Index = buffer.ReadVarInt(),
            MessageSignature = buffer.ReadBoolean() ? buffer.ReadBuffer(256).ToArray() : null
        };

        // Body
        Body = new BodyPayload
        {
            Message = buffer.ReadString(),
            Timestamp = buffer.ReadSignedLong(),
            Salt = buffer.ReadSignedLong()
        };

        // Validations
        var count = buffer.ReadVarInt();
        Validations = new MessageValidationPayload[count];
        for (var i = 0; i < count; i++)
        {
            Validations[i] = new MessageValidationPayload
            {
                MessageId = buffer.ReadVarInt(),
                Signature = buffer.ReadBuffer(256).ToArray()
            };
        }

        // Other
        var unsignedContent = buffer.ReadBoolean() ? buffer.ReadNbtTag() : null;
        var filterType = (FilterType)buffer.ReadVarInt();
        var filterTypeBits = filterType == FilterType.PartiallyFiltered ? buffer.ReadBitSet() : null;

        Other = new OtherPayload
        {
            UnsignedContent = unsignedContent,
            FilterType = filterType,
            FilterTypeBits = filterTypeBits
        };

        // Formatting
        Formatting = new FormattingPayload
        {
            Type = buffer.ReadVarInt(),
            SenderName = buffer.ReadNbtTag(),
            TargetName = buffer.ReadBoolean() ? buffer.ReadNbtTag() : null
        };
    }

    public class HeaderPayload
    {
        public required Guid Uuid { get; set; }
        public required int Index { get; set; }
        public byte[]? MessageSignature { get; set; }
    }

    public class BodyPayload
    {
        public required string Message { get; set; } = string.Empty;
        public required long Timestamp { get; set; }
        public required long Salt { get; set; }
    }

    public class MessageValidationPayload
    {
        public required int MessageId { get; set; }
        public required byte[] Signature { get; set; }
    }

    public class OtherPayload
    {
        public NbtTag? UnsignedContent { get; set; }
        public required FilterType FilterType { get; set; }
        public long[]? FilterTypeBits { get; set; }
    }

    public class FormattingPayload
    {
        public required int Type { get; set; }
        public required NbtTag SenderName { get; set; }
        public NbtTag? TargetName { get; set; }
    }

    public enum FilterType
    {
        PassThrough = 0,
        FullyFiltered = 1,
        PartiallyFiltered = 2
    }
}
