using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

[Packet(0x7C, ProtocolState.Play)]
public class UpdateAttributesPacket : IClientPacket
{
    public int EntityId { get; set; }
    public Property[] Properties { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        EntityId = buffer.ReadVarInt();

        var propertiesCount = buffer.ReadVarInt();
        Properties = new Property[propertiesCount];
        for (var i = 0; i < propertiesCount; i++)
        {
            var id = buffer.ReadVarInt();
            var value = buffer.ReadDouble();

            var modifiersCount = buffer.ReadVarInt();
            var modifiers = new Modifier[modifiersCount];
            for (var j = 0; j < modifiersCount; j++)
            {
                var modifier = new Modifier
                {
                    Identifier = buffer.ReadString(),
                    Amount = buffer.ReadDouble(),
                    Operation = buffer.ReadUnsignedByte()
                };
                modifiers[j] = modifier;
            }

            Properties[i] = new Property
            {
                Id = id,
                Value = value,
                Modifiers = modifiers,
            };
        }
    }

    public class Property
    {
        public required int Id { get; set; }
        public required double Value { get; set; }
        public required Modifier[] Modifiers { get; set; }

        public override string ToString()
        {
            return $"{Id} {Value} {Modifiers.Length}";
        }
    }

    public class Modifier
    {
        public required string Identifier { get; set; }
        public required double Amount { get; set; }
        public required byte Operation { get; set; }

        public override string ToString()
        {
            return $"{Identifier} {Amount} {Operation}";
        }
    }
}
