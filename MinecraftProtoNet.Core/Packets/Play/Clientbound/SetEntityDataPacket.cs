using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Enums;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Packets.Base.Definitions;
using MinecraftProtoNet.Core.Utilities;
using Spectre.Console;

namespace MinecraftProtoNet.Core.Packets.Play.Clientbound;

// TODO: Partially implemented.
[Packet(0x63, ProtocolState.Play, true)]
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

    /// <summary>
    /// Metadata types, numbered by their registration order in EntityDataSerializers.
    /// Reference: minecraft-26.2-REFERENCE-ONLY/net/minecraft/network/syncher/EntityDataSerializers.java:191-233
    ///
    /// These ids are positional, so an omission does not just lose one type — it renumbers every type after it,
    /// and since entity data is a flat field stream a wrong type desynchronises the rest of the packet. 26.2
    /// added the four *_SOUND_VARIANT serializers below; without them everything from CowVariant onwards was
    /// off by up to four, which made Vector3/Quaternion/ResolvableProfile unreadable.
    /// </summary>
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
        CatSoundVariant = 22,
        CowVariant = 23,
        CowSoundVariant = 24,
        WolfVariant = 25,
        WolfSoundVariant = 26,
        FrogVariant = 27,
        PigVariant = 28,
        PigSoundVariant = 29,
        ChickenVariant = 30,
        ChickenSoundVariant = 31,
        ZombieNautilusVariant = 32,
        OptionalGlobalPos = 33,
        PaintingVariant = 34,
        SnifferState = 35,
        ArmadilloState = 36,
        CopperGolemState = 37,
        WeatheringCopperState = 38,
        Vector3 = 39,         // 3 floats
        Quaternion = 40,      // 4 floats
        ResolvableProfile = 41,
        HumanoidArm = 42,     // VarInt enum (0=left, 1=right)
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
            case MetadataType.CatSoundVariant:
            case MetadataType.CowVariant:
            case MetadataType.CowSoundVariant:
            case MetadataType.WolfVariant:
            case MetadataType.WolfSoundVariant:
            case MetadataType.FrogVariant:
            case MetadataType.PigVariant:
            case MetadataType.PigSoundVariant:
            case MetadataType.ChickenVariant:
            case MetadataType.ChickenSoundVariant:
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
                // Backs minecraft:mannequin NPCs (and player heads), so the value is kept rather than skipped.
                // The layout changed in 26.2 — either(GameProfile, Partial) followed by a PlayerSkin.Patch —
                // and the previous read was the older name/uuid/properties one. Since entity data is a flat
                // field stream, that mismatch corrupted every field after it in the same packet.
                value = ResolvableProfileData.Read(ref buffer);
                break;
            case MetadataType.Particles:
            {
                // PARTICLES = ParticleTypes.STREAM_CODEC.apply(list()) — a VarInt count then that many
                // particles. Reference: EntityDataSerializers.java:150
                //
                // This is LivingEntity.DATA_EFFECT_PARTICLES (index 10), so it is present on essentially every
                // living entity, and for an entity with no potion effects the list is empty — one VarInt.
                // Consuming the rest of the buffer on sight (what this used to do) therefore discarded every
                // field after index 10 on every mob, player and armour stand that sent one. Servers that emit
                // it in the spawn bundle lost the entire tail of that packet.
                //
                // A non-empty list still needs per-particle payloads, which vary by type and are not modelled
                // yet, so that case keeps the old bail-out rather than desynchronising the stream.
                var particleCount = buffer.ReadVarInt();
                if (particleCount == 0)
                {
                    value = Array.Empty<object>();
                    break;
                }

                AnsiConsole.MarkupLine($"[yellow]Warning:[/] [white]{particleCount} particle(s) in entity data; payloads not implemented, consuming rest of buffer[/]");
                _ = buffer.ReadRestBuffer();
                break;
            }
            case MetadataType.Particle:
                // Single particle: type id plus a type-specific payload that is not modelled yet.
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

