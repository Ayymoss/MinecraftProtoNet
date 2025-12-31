using MinecraftProtoNet.Utilities;
using Serilog;

namespace MinecraftProtoNet.Packets.Base.Definitions;

public class Slot
{
    // TODO: Partial: https://minecraft.wiki/w/Minecraft_Wiki:Projects/wiki.vg_merge/Slot_Data
    public int ItemCount { get; set; }
    public int? ItemId { get; set; }
    public StructuredComponent[]? ComponentsToAdd { get; set; }
    public ComponentType[]? ComponentsToRemove { get; set; }

    public static Slot Empty { get; set; } = new()
    {
        ItemCount = 0,
        ItemId = 0
    };

    public static Slot Read(ref PacketBufferReader reader)
    {
        var slot = new Slot
        {
            ItemCount = reader.ReadVarInt()
        };

        if (slot.ItemCount is 0) return slot;

        slot.ItemId = reader.ReadVarInt();
        var componentsToAddCount = reader.ReadVarInt();
        var componentsToRemoveCount = reader.ReadVarInt();

        slot.ComponentsToAdd = new StructuredComponent[componentsToAddCount];
        for (var i = 0; i < componentsToAddCount; i++)
        {
            var typeId = reader.ReadVarInt();
            var type = (ComponentType)typeId;
            var data = ReadComponentData(ref reader, type, typeId);
            slot.ComponentsToAdd[i] = new StructuredComponent
            {
                Type = type,
                Data = data
            };
        }

        slot.ComponentsToRemove = new ComponentType[componentsToRemoveCount];
        for (var i = 0; i < componentsToRemoveCount; i++)
        {
            slot.ComponentsToRemove[i] = (ComponentType)reader.ReadVarInt();
        }

        return slot;
    }

    public void Write(ref PacketBufferWriter writer)
    {
        writer.WriteVarInt(ItemCount);
        if (ItemCount is 0) return;

        writer.WriteVarInt(ItemId ?? 0);
        writer.WriteVarInt(ComponentsToAdd?.Length ?? 0);
        writer.WriteVarInt(ComponentsToRemove?.Length ?? 0);

        if (ComponentsToAdd != null)
        {
            foreach (var component in ComponentsToAdd)
            {
                writer.WriteVarInt((int)component.Type);
                // TODO: Implement component data writing if needed
                // For now, we usually send Slot.Empty or simple items where components are minimal
            }
        }

        if (ComponentsToRemove != null)
        {
            foreach (var type in ComponentsToRemove)
            {
                writer.WriteVarInt((int)type);
            }
        }
    }

    /// <summary>
    /// Writes the slot as a HashedStack (used in ClickContainerPacket).
    /// Format: Boolean(Present) -> ItemId -> Count -> HashedPatchMap
    /// </summary>
    public void WriteHashed(ref PacketBufferWriter writer)
    {
        if (ItemCount <= 0 || ItemId == null)
        {
            writer.WriteBoolean(false);
            return;
        }

        writer.WriteBoolean(true);
        writer.WriteVarInt(ItemId.Value);
        writer.WriteVarInt(ItemCount);

        // HashedPatchMap
        // For now, we assume no component changes/hashes are needed for basic swaps.
        // If we need to support component-heavy items, we'll need to compute hashes.
        // Added Components: Map<DataComponentType, Integer>
        writer.WriteVarInt(0); // Count of added
        
        // Removed Components: Set<DataComponentType>
        writer.WriteVarInt(0); // Count of removed
    }

    /// <summary>
    /// Reads component data based on type. Component types are defined in DataComponents.java.
    /// Each type has a specific stream codec that must be matched.
    /// </summary>
    private static object? ReadComponentData(ref PacketBufferReader reader, ComponentType type, int typeId)
    {
        // Based on DataComponents.java from Minecraft 1.21
        // Types use ByteBufCodecs.VAR_INT, FLOAT, BOOL, or complex stream codecs
        return type switch
        {
            // NBT-based types
            ComponentType.CustomData => reader.ReadNbtTag(),
            ComponentType.BucketEntityData => reader.ReadNbtTag(),
            
            // VarInt types
            ComponentType.MaxStackSize => reader.ReadVarInt(),
            ComponentType.MaxDamage => reader.ReadVarInt(),
            ComponentType.Damage => reader.ReadVarInt(),
            ComponentType.RepairCost => reader.ReadVarInt(),
            ComponentType.AdditionalTradeCost => reader.ReadVarInt(),
            ComponentType.OminousBottleAmplifier => reader.ReadVarInt(),
            
            // Boolean types
            ComponentType.EnchantmentGlintOverride => reader.ReadBoolean(),
            
            // Float types  
            ComponentType.MinimumAttackCharge => reader.ReadFloat(),
            ComponentType.PotionDurationScale => reader.ReadFloat(),
            
            // Unit types (no data - just presence)
            ComponentType.Unbreakable => Unit.Instance,
            ComponentType.Glider => Unit.Instance,
            ComponentType.IntangibleProjectile => null, // No network sync
            ComponentType.CreativeSlotLock => Unit.Instance,
            
            // Identifier types (string - namespace:path format)
            ComponentType.ItemModel => reader.ReadString(),
            ComponentType.TooltipStyle => reader.ReadString(),
            ComponentType.NoteBlockSound => reader.ReadString(),
            
            // Complex types - read as NBT fallback (may not work for all)
            // These require proper stream codec implementations
            _ => ReadUnknownComponent(ref reader, typeId)
        };
    }

    /// <summary>
    /// Attempts to read unknown component types. Since we can't know the format,
    /// we try reading as NBT first. If that fails, we log and return null.
    /// This is a workaround until all component types are properly implemented.
    /// </summary>
    private static object? ReadUnknownComponent(ref PacketBufferReader reader, int typeId)
    {
        // Most complex types are NBT-encoded, try that first
        try
        {
            return reader.ReadNbtTag();
        }
        catch
        {
            Log.Warning("[Slot] Unknown component type {TypeId} - unable to parse", typeId);
            return null;
        }
    }

    public override string ToString()
    {
        return
            $"{ItemCount} {ItemId?.ToString() ?? "<NULL>"} {ComponentsToAdd?.Length.ToString() ?? "<NULL>"} {ComponentsToRemove?.Length.ToString() ?? "<NULL>"}";
    }
}

public class StructuredComponent
{
    public ComponentType Type { get; set; }
    public object? Data { get; set; }

    public override string ToString()
    {
        return $"{Type} {Data?.GetType().ToString() ?? "<NULL>"}";
    }
}

/// <summary>
/// Unit type representing presence without data (like void/empty).
/// Used for flags like Unbreakable, Glider, etc.
/// </summary>
public class Unit
{
    public static readonly Unit Instance = new();
    private Unit() { }
}

/// <summary>
/// Component types from DataComponents.java (Minecraft 1.21).
/// Order matches the registry order.
/// </summary>
public enum ComponentType
{
    CustomData = 0,
    MaxStackSize = 1,
    MaxDamage = 2,
    Damage = 3,
    Unbreakable = 4,
    UseEffects = 5,
    CustomName = 6,
    MinimumAttackCharge = 7,
    DamageType = 8,
    ItemName = 9,
    ItemModel = 10,
    Lore = 11,
    Rarity = 12,
    Enchantments = 13,
    CanPlaceOn = 14,
    CanBreak = 15,
    AttributeModifiers = 16,
    CustomModelData = 17,
    TooltipDisplay = 18,
    RepairCost = 19,
    CreativeSlotLock = 20,
    EnchantmentGlintOverride = 21,
    IntangibleProjectile = 22,
    Food = 23,
    Consumable = 24,
    UseRemainder = 25,
    UseCooldown = 26,
    DamageResistant = 27,
    Tool = 28,
    Weapon = 29,
    AttackRange = 30,
    Enchantable = 31,
    Equippable = 32,
    Repairable = 33,
    Glider = 34,
    TooltipStyle = 35,
    DeathProtection = 36,
    BlocksAttacks = 37,
    PiercingWeapon = 38,
    KineticWeapon = 39,
    SwingAnimation = 40,
    AdditionalTradeCost = 41,
    StoredEnchantments = 42,
    DyedColor = 43,
    MapColor = 44,
    MapId = 45,
    MapDecorations = 46,
    MapPostProcessing = 47,
    ChargedProjectiles = 48,
    BundleContents = 49,
    PotionContents = 50,
    PotionDurationScale = 51,
    SuspiciousStewEffects = 52,
    WritableBookContent = 53,
    WrittenBookContent = 54,
    Trim = 55,
    DebugStickState = 56,
    EntityData = 57,
    BucketEntityData = 58,
    BlockEntityData = 59,
    Instrument = 60,
    ProvidesTrimMaterial = 61,
    OminousBottleAmplifier = 62,
    JukeboxPlayable = 63,
    ProvidesBannerPatterns = 64,
    Recipes = 65,
    LodestoneTracker = 66,
    FireworkExplosion = 67,
    Fireworks = 68,
    Profile = 69,
    NoteBlockSound = 70,
    BannerPatterns = 71,
    BaseColor = 72,
    PotDecorations = 73,
    Container = 74,
    BlockState = 75,
    Bees = 76,
    Lock = 77,
    ContainerLoot = 78,
    BreakSound = 79,
    // Entity variants continue from here...
}
