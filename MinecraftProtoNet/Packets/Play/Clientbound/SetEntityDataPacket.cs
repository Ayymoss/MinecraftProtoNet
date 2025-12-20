using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Enums;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Packets.Base.Definitions;
using MinecraftProtoNet.Utilities;
using Spectre.Console;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

// TODO: Partially implemented.
[Packet(0x62, ProtocolState.Play, true)]
public class SetEntityDataPacket : IClientboundPacket
{
    public int EntityId { get; set; }
    public Metadata[] MetadataPayload { get; set; } = [];

    public void Deserialize(ref PacketBufferReader buffer)
    {
        EntityId = buffer.ReadVarInt();
        List<Metadata> metadata = [];

        while (buffer.ReadableBytes > 0)
        {
            var index = buffer.ReadUnsignedByte();
            if (index == 0xFF) break;

            var typeId = buffer.ReadVarInt();
            
            // Safety: if type is out of known range, consume rest of buffer to avoid corruption
            if (typeId is < 0 or > (int)MetadataType.HumanoidArm)
            {
                AnsiConsole.MarkupLine($"[yellow]Warning:[/] [white]Unknown metadata type ID:[/] {typeId}");
                _ = buffer.ReadRestBuffer();
                break;
            }

            var type = (MetadataType)typeId;
            var value = GetValue(ref buffer, type);
            metadata.Add(new Metadata
            {
                Index = index,
                Type = type,
                Value = value
            });
        }

        MetadataPayload = metadata.ToArray();
    }

    public class Metadata
    {
        public required byte Index { get; set; }
        public MetadataType? Type { get; set; }
        public object? Value { get; set; }

        public override string ToString()
        {
            return $"[{Index}] {Type?.ToString() ?? "<NULL>"} = {Value?.GetType().ToString() ?? "<NULL>"}";
        }
    }

    // Metadata types as registered in EntityDataSerializers (MC 26.1)
    // Order matches registerSerializer() calls in EntityDataSerializers.java
    public enum MetadataType
    {
        Byte = 0,
        Int = 1,              // VarInt
        Long = 2,             // VarLong
        Float = 3,
        String = 4,
        Component = 5,        // Chat component (NBT)
        OptionalComponent = 6,
        ItemStack = 7,        // Slot
        Boolean = 8,
        Rotations = 9,        // 3 floats
        BlockPos = 10,        // Position (long)
        OptionalBlockPos = 11,
        Direction = 12,       // VarInt enum
        OptionalLivingEntityReference = 13, // Optional UUID -> VarInt entity ID
        BlockState = 14,      // VarInt
        OptionalBlockState = 15,
        Particle = 16,        // Complex particle data
        Particles = 17,       // List of particles
        VillagerData = 18,    // 3 VarInts (type, profession, level)
        OptionalUnsignedInt = 19, // OptionalVarInt
        Pose = 20,            // VarInt enum
        CatVariant = 21,      // VarInt holder ID
        CowVariant = 22,
        WolfVariant = 23,
        WolfSoundVariant = 24,
        FrogVariant = 25,
        PigVariant = 26,
        ChickenVariant = 27,
        ZombieNautilusVariant = 28,
        OptionalGlobalPos = 29,
        PaintingVariant = 30,
        SnifferState = 31,
        ArmadilloState = 32,
        CopperGolemState = 33,
        WeatheringCopperState = 34,
        Vector3 = 35,         // 3 floats
        Quaternion = 36,      // 4 floats
        ResolvableProfile = 37, // Optional game profile
        HumanoidArm = 38,     // VarInt enum (0=left, 1=right)
    }

    private object? GetValue(ref PacketBufferReader buffer, MetadataType type)
    {
        object? value = null;
        switch (type)
        {
            case MetadataType.Byte:
                value = buffer.ReadUnsignedByte();
                break;
            case MetadataType.Int:
                value = buffer.ReadVarInt();
                break;
            case MetadataType.Long:
                value = buffer.ReadVarLong();
                break;
            case MetadataType.Float:
                value = buffer.ReadFloat();
                break;
            case MetadataType.String:
                value = buffer.ReadString();
                break;
            case MetadataType.Component:
                value = buffer.ReadNbtTag();
                break;
            case MetadataType.OptionalComponent:
                value = buffer.ReadOptionalNbtTag();
                break;
            case MetadataType.ItemStack:
                value = Slot.Read(ref buffer);
                break;
            case MetadataType.Boolean:
                value = buffer.ReadBoolean();
                break;
            case MetadataType.Rotations:
                value = (buffer.ReadFloat(), buffer.ReadFloat(), buffer.ReadFloat());
                break;
            case MetadataType.BlockPos:
                value = buffer.ReadCoordinatePosition();
                break;
            case MetadataType.OptionalBlockPos:
                if (buffer.ReadBoolean())
                    value = buffer.ReadCoordinatePosition();
                break;
            case MetadataType.Direction:
                value = buffer.ReadVarInt();
                break;
            case MetadataType.OptionalLivingEntityReference:
                if (buffer.ReadBoolean())
                    value = buffer.ReadVarInt(); // Entity ID
                break;
            case MetadataType.BlockState:
            case MetadataType.OptionalBlockState:
                value = buffer.ReadVarInt();
                break;
            case MetadataType.Pose:
                value = (Pose)buffer.ReadVarInt();
                break;
            case MetadataType.VillagerData:
                value = (buffer.ReadVarInt(), buffer.ReadVarInt(), buffer.ReadVarInt());
                break;
            case MetadataType.OptionalUnsignedInt:
                value = buffer.ReadVarInt(); // 0 = empty, otherwise value - 1
                break;
            case MetadataType.Vector3:
                value = (buffer.ReadFloat(), buffer.ReadFloat(), buffer.ReadFloat());
                break;
            case MetadataType.Quaternion:
                value = (buffer.ReadFloat(), buffer.ReadFloat(), buffer.ReadFloat(), buffer.ReadFloat());
                break;
            case MetadataType.HumanoidArm:
                value = buffer.ReadVarInt(); // 0=left, 1=right
                break;
            // Variant types are VarInt holder IDs
            case MetadataType.CatVariant:
            case MetadataType.CowVariant:
            case MetadataType.WolfVariant:
            case MetadataType.WolfSoundVariant:
            case MetadataType.FrogVariant:
            case MetadataType.PigVariant:
            case MetadataType.ChickenVariant:
            case MetadataType.ZombieNautilusVariant:
            case MetadataType.PaintingVariant:
            case MetadataType.SnifferState:
            case MetadataType.ArmadilloState:
            case MetadataType.CopperGolemState:
            case MetadataType.WeatheringCopperState:
                value = buffer.ReadVarInt();
                break;
            case MetadataType.OptionalGlobalPos:
                if (buffer.ReadBoolean())
                {
                    buffer.ReadString(); // dimension identifier
                    buffer.ReadCoordinatePosition(); // block pos
                }
                break;
            case MetadataType.ResolvableProfile:
                // Complex: name (optional string), uuid (optional), properties
                var hasName = buffer.ReadBoolean();
                if (hasName) buffer.ReadString();
                var hasUuid = buffer.ReadBoolean();
                if (hasUuid) buffer.ReadUuid();
                var propCount = buffer.ReadVarInt();
                for (var i = 0; i < propCount; i++)
                {
                    buffer.ReadString(); // name
                    buffer.ReadString(); // value
                    if (buffer.ReadBoolean()) buffer.ReadString(); // signature
                }
                break;
            case MetadataType.Particle:
            case MetadataType.Particles:
                // Complex particle data - skip remaining for safety
                AnsiConsole.MarkupLine($"[yellow]Warning:[/] [white]Particle metadata not fully implemented, consuming rest of buffer[/]");
                _ = buffer.ReadRestBuffer();
                break;
            default:
                AnsiConsole.MarkupLine($"[yellow]Warning:[/] [white]Non-implemented metadata type:[/] {type} ({(int)type})");
                break;
        }

        return value;
    }
}

