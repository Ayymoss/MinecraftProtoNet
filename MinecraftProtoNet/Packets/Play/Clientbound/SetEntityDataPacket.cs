using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Enums;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;
using Spectre.Console;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

// TODO: Partially implemented.
[Packet(0x5D, ProtocolState.Play, true)]
public class SetEntityDataPacket : IClientPacket
{
    public int EntityId { get; set; }
    public Metadata[] MetadataPayload { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        EntityId = buffer.ReadVarInt();
        List<Metadata> metadata = [];
        do
        {
            var index = buffer.ReadUnsignedByte();
            if (index is 0xFF) break;

            var type = (MetadataType)buffer.ReadVarInt();
            var value = GetValue(ref buffer, type);
            metadata.Add(new Metadata
            {
                Index = index,
                Type = type,
                Value = value
            });
        } while (true);

        MetadataPayload = metadata.ToArray();
    }

    public class Metadata
    {
        public required byte Index { get; set; }
        public MetadataType? Type { get; set; }
        public object? Value { get; set; }

        public override string ToString()
        {
            return $"[{Index}] {Type} = {Value}";
        }
    }

    public enum MetadataType
    {
        Byte = 0,
        VarInt = 1,
        VarLong = 2,
        Float = 3,
        String = 4,
        NbtText = 5,
        OptionalNbt = 6,
        Slot = 7,
        Boolean = 8,
        Rotations = 9,
        Position = 10,
        OptionalPosition = 11,
        Direction = 12,
        OptionalUuid = 13,
        BlockState = 14,
        OptionalBlockState = 15,
        NbtTag = 16,
        Particle = 17,
        Particles = 18,
        VillagerData = 19,
        OptionalVarInt = 20,
        Pose = 21,
        CatVariant = 22,
        WolfVariant = 23,
        FrogVariant = 24,
        OptionalGlobalPos = 25,
        PaintingVariant = 26,
        SnifferState = 27,
        ArmadilloState = 28,
        Vector3 = 29,
        Quaternion = 30,
    }

    private object? GetValue(ref PacketBufferReader buffer, MetadataType type)
    {
        object? value = null;
        switch (type)
        {
            case MetadataType.Byte:
                value = buffer.ReadUnsignedByte();
                break;
            case MetadataType.VarInt:
                value = buffer.ReadVarInt();
                break;
            case MetadataType.VarLong:
                value = buffer.ReadVarLong();
                break;
            case MetadataType.Float:
                value = buffer.ReadFloat();
                break;
            case MetadataType.String:
                value = buffer.ReadString();
                break;
            case MetadataType.NbtText:
                value = buffer.ReadNbtTag();
                break;
            case MetadataType.OptionalNbt:
                value = buffer.ReadOptionalNbtTag();
                break;
            case MetadataType.Pose:
                value = (Pose)buffer.ReadVarInt();
                break;
            default:
                AnsiConsole.MarkupLine($"[yellow]Warning:[/] [white]Non-implemented metadata type:[/] {type} ({(int)type})");
                break;
        }

        return value;
    }
}
