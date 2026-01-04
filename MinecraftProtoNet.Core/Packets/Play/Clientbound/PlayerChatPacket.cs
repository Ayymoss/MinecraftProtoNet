using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.NBT.Tags;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

[Packet(0x40, ProtocolState.Play)]
public class PlayerChatPacket : IClientboundPacket
{
    public required HeaderPayload Header { get; set; }
    public required BodyPayload Body { get; set; }
    public required MessageValidationPayload[] Validations { get; set; }
    public required OtherPayload Other { get; set; }
    public required FormattingPayload Formatting { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        // Header
        Header = new HeaderPayload
        {
            GlobalIndex = buffer.ReadVarInt(),
            Uuid = buffer.ReadUuid(),
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
            var messageId = buffer.ReadVarInt();
            var signature = messageId == 0 ? buffer.ReadBuffer(256).ToArray() : null;

            Validations[i] = new MessageValidationPayload
            {
                MessageId = messageId,
                Signature = signature
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
        public required byte[]? MessageSignature { get; set; }
        public required int GlobalIndex { get; set; }

        public override string ToString()
        {
            return $"{{GlobalIndex={GlobalIndex}, Uuid={Uuid}, Index={Index}, MessageSignature={MessageSignature}}}";
        }
    }

    public class BodyPayload
    {
        public required string Message { get; set; } = string.Empty;
        public required long Timestamp { get; set; }
        public required long Salt { get; set; }

        public override string ToString()
        {
            return $"{{Message={Message}, Timestamp={Timestamp}, Salt={Salt}}}";
        }
    }

    public class MessageValidationPayload
    {
        public required int MessageId { get; set; }
        public required byte[]? Signature { get; set; }

        public override string ToString()
        {
            return $"{{MessageId={MessageId}, Signature={Signature}}}";
        }
    }

    public class OtherPayload
    {
        public required NbtTag? UnsignedContent { get; set; }
        public required FilterType FilterType { get; set; }
        public required long[]? FilterTypeBits { get; set; }

        public override string ToString()
        {
            return $"{{UnsignedContent={UnsignedContent}, FilterType={FilterType}, FilterTypeBits={FilterTypeBits}}}";
        }
    }

    public class FormattingPayload
    {
        public required int Type { get; set; }
        public required NbtTag SenderName { get; set; }
        public required NbtTag? TargetName { get; set; }

        public override string ToString()
        {
            return $"{{Type={Type}, SenderName={SenderName}, TargetName={TargetName}}}";
        }
    }

    public enum FilterType
    {
        PassThrough = 0,
        FullyFiltered = 1,
        PartiallyFiltered = 2
    }
}
