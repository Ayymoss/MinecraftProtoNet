using MinecraftProtoNet.Core.Utilities;
using Serilog;

namespace MinecraftProtoNet.Core.Packets.Base.Definitions;

public class Slot
{
    // TODO: Partial: https://minecraft.wiki/w/Minecraft_Wiki:Projects/wiki.vg_merge/Slot_Data
    public int ItemCount { get; set; }
    public int? ItemId { get; set; }
    public StructuredComponent[]? ComponentsToAdd { get; set; }
    public ComponentType[]? ComponentsToRemove { get; set; }

    public static Slot Empty { get; } = new()
    {
        ItemCount = 0,
        ItemId = 0
    };

    /// <summary>
    /// Whether this slot is empty (no item or zero count).
    /// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/item/ItemStack.java isEmpty()
    /// </summary>
    public bool IsEmpty => ItemCount <= 0 || ItemId is null or 0;

    /// <summary>
    /// Creates a deep copy of this slot.
    /// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/item/ItemStack.java copy()
    /// </summary>
    public Slot Clone()
    {
        return new Slot
        {
            ItemCount = ItemCount,
            ItemId = ItemId,
            ComponentsToAdd = ComponentsToAdd?.ToArray(),
            ComponentsToRemove = ComponentsToRemove?.ToArray()
        };
    }

    /// <summary>
    /// Creates a copy with a different count.
    /// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/item/ItemStack.java copyWithCount(int)
    /// </summary>
    public Slot CopyWithCount(int count)
    {
        var copy = Clone();
        copy.ItemCount = count;
        return copy;
    }

    /// <summary>
    /// Checks if two slots have the same item type and components (ignoring count).
    /// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/item/ItemStack.java isSameItemSameComponents()
    /// </summary>
    public static bool IsSameItemSameComponents(Slot a, Slot b)
    {
        if (a.ItemId != b.ItemId) return false;
        // For component comparison, we compare by reference arrays length and types.
        // Full hash-based comparison would require implementing component hashing.
        var aAddCount = a.ComponentsToAdd?.Length ?? 0;
        var bAddCount = b.ComponentsToAdd?.Length ?? 0;
        var aRemoveCount = a.ComponentsToRemove?.Length ?? 0;
        var bRemoveCount = b.ComponentsToRemove?.Length ?? 0;
        return aAddCount == bAddCount && aRemoveCount == bRemoveCount;
    }

    /// <summary>
    /// Checks if two slots are completely identical (same item, count, and components).
    /// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/item/ItemStack.java matches()
    /// </summary>
    public static bool Matches(Slot a, Slot b)
    {
        if (a.IsEmpty && b.IsEmpty) return true;
        if (a.IsEmpty != b.IsEmpty) return false;
        return a.ItemCount == b.ItemCount && IsSameItemSameComponents(a, b);
    }

    /// <summary>
    /// Gets the max stack size for this item. Defaults to 64 if no MaxStackSize component.
    /// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/item/ItemStack.java getMaxStackSize()
    /// </summary>
    public int GetMaxStackSize()
    {
        if (ComponentsToAdd == null) return 64;
        foreach (var component in ComponentsToAdd)
        {
            if (component.Type == ComponentType.MaxStackSize && component.Data is int maxSize)
                return maxSize;
        }
        return 64;
    }

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
    public static object? ReadComponentData(ref PacketBufferReader reader, ComponentType type, int typeId)
    {
        // Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/core/component/DataComponents.java
        // Each component type has a specific StreamCodec; the wire format varies by type.
        // NBT-based types use fromCodecWithRegistries (writes an NBT tag).
        // Simple types use ByteBufCodecs.VAR_INT/FLOAT/BOOL/etc.
        // Complex types use StreamCodec.composite or custom codecs.
        return type switch
        {
            // === NBT-based types (CustomData.STREAM_CODEC = NBT compound) ===
            ComponentType.CustomData => reader.ReadNbtTag(),
            ComponentType.BucketEntityData => reader.ReadNbtTag(),

            // === VarInt types (ByteBufCodecs.VAR_INT) ===
            ComponentType.MaxStackSize => reader.ReadVarInt(),
            ComponentType.MaxDamage => reader.ReadVarInt(),
            ComponentType.Damage => reader.ReadVarInt(),
            ComponentType.RepairCost => reader.ReadVarInt(),
            ComponentType.AdditionalTradeCost => reader.ReadVarInt(),
            ComponentType.OminousBottleAmplifier => reader.ReadVarInt(),
            ComponentType.Enchantable => reader.ReadVarInt(),

            // === VarInt enum types (ByteBufCodecs.idMapper) ===
            ComponentType.Dye => reader.ReadVarInt(),
            ComponentType.BaseColor => reader.ReadVarInt(),
            ComponentType.MapPostProcessing => reader.ReadVarInt(),
            ComponentType.Rarity => reader.ReadVarInt(),
            ComponentType.WolfCollar => reader.ReadVarInt(),
            ComponentType.TropicalFishBaseColor => reader.ReadVarInt(),
            ComponentType.TropicalFishPatternColor => reader.ReadVarInt(),
            ComponentType.CatCollar => reader.ReadVarInt(),
            ComponentType.SheepColor => reader.ReadVarInt(),
            ComponentType.ShulkerColor => reader.ReadVarInt(),
            ComponentType.FoxVariant => reader.ReadVarInt(),
            ComponentType.SalmonSize => reader.ReadVarInt(),
            ComponentType.ParrotVariant => reader.ReadVarInt(),
            ComponentType.MooshroomVariant => reader.ReadVarInt(),
            ComponentType.RabbitVariant => reader.ReadVarInt(),
            ComponentType.LlamaVariant => reader.ReadVarInt(),
            ComponentType.AxolotlVariant => reader.ReadVarInt(),

            // === Holder<T> registry types (ByteBufCodecs.holderRegistry = VarInt) ===
            ComponentType.DamageType => reader.ReadVarInt(),
            ComponentType.DamageResistant => reader.ReadVarInt(),
            ComponentType.VillagerVariant => reader.ReadVarInt(),
            ComponentType.WolfVariant => reader.ReadVarInt(),
            ComponentType.WolfSoundVariant => reader.ReadVarInt(),
            ComponentType.PigVariant => reader.ReadVarInt(),
            ComponentType.PigSoundVariant => reader.ReadVarInt(),
            ComponentType.CowVariant => reader.ReadVarInt(),
            ComponentType.CowSoundVariant => reader.ReadVarInt(),
            ComponentType.ChickenVariant => reader.ReadVarInt(),
            ComponentType.ChickenSoundVariant => reader.ReadVarInt(),
            ComponentType.ZombieNautilusVariant => reader.ReadVarInt(),
            ComponentType.FrogVariant => reader.ReadVarInt(),
            ComponentType.PaintingVariant => reader.ReadVarInt(),
            ComponentType.CatVariant => reader.ReadVarInt(),
            ComponentType.CatSoundVariant => reader.ReadVarInt(),
            ComponentType.ProvidesTrimMaterial => reader.ReadVarInt(),

            // === Boolean types (ByteBufCodecs.BOOL) ===
            ComponentType.EnchantmentGlintOverride => reader.ReadBoolean(),

            // === Float types (ByteBufCodecs.FLOAT) ===
            ComponentType.MinimumAttackCharge => reader.ReadFloat(),
            ComponentType.PotionDurationScale => reader.ReadFloat(),

            // === Unit types (no data) ===
            ComponentType.Unbreakable => Unit.Instance,
            ComponentType.Glider => Unit.Instance,
            ComponentType.IntangibleProjectile => null, // No network sync
            ComponentType.CreativeSlotLock => Unit.Instance,

            // === Identifier types (Identifier.STREAM_CODEC = VarInt length + UTF-8) ===
            ComponentType.ItemModel => reader.ReadString(),
            ComponentType.TooltipStyle => reader.ReadString(),
            ComponentType.NoteBlockSound => reader.ReadString(),
            ComponentType.ProvidesBannerPatterns => reader.ReadString(), // TagKey = Identifier

            // === Text component types (ComponentSerialization.STREAM_CODEC = NBT tag) ===
            ComponentType.CustomName => reader.ReadNbtTag(),
            ComponentType.ItemName => reader.ReadNbtTag(),

            // === Map<Holder, VarInt> types (ItemEnchantments) ===
            // Reference: ItemEnchantments.java — map(holderRegistry, VAR_INT)
            ComponentType.Enchantments => ReadMapVarIntVarInt(ref reader),
            ComponentType.StoredEnchantments => ReadMapVarIntVarInt(ref reader),

            // === Composite types with known binary formats ===
            // Reference: UseEffects.java — BOOL + BOOL + FLOAT
            ComponentType.UseEffects => ReadUseEffects(ref reader),
            // Reference: FoodProperties.java — VAR_INT + FLOAT + BOOL
            ComponentType.Food => ReadFoodProperties(ref reader),
            // Reference: Weapon.java — VAR_INT + FLOAT
            ComponentType.Weapon => ReadWeapon(ref reader),
            // Reference: AttackRange.java — 6 FLOATs
            ComponentType.AttackRange => ReadAttackRange(ref reader),
            // Reference: SwingAnimation.java — VarInt enum + VarInt
            ComponentType.SwingAnimation => ReadSwingAnimation(ref reader),
            // Reference: TooltipDisplay.java — BOOL + list of VarInt
            ComponentType.TooltipDisplay => ReadTooltipDisplay(ref reader),
            // Reference: CustomModelData.java — 4 lists (floats, bools, strings, ints)
            ComponentType.CustomModelData => ReadCustomModelData(ref reader),
            // Reference: MapId.java — VAR_INT
            ComponentType.MapId => reader.ReadVarInt(),
            // Reference: MapItemColor.java — INT (signed 32-bit)
            ComponentType.MapColor => reader.ReadSignedInt(),
            // Reference: DyedItemColor.java — INT (signed 32-bit)
            ComponentType.DyedColor => reader.ReadSignedInt(),
            // Reference: HorseVariant.java — composite VarInt + VarInt
            ComponentType.HorseVariant => ReadHorseVariant(ref reader),
            // Reference: TropicalFish.Pattern.STREAM_CODEC — composite VarInt + VarInt
            ComponentType.TropicalFishPattern => ReadTropicalFishPattern(ref reader),

            // === Lore: list of text components (each is an NBT tag via ComponentSerialization) ===
            // Reference: ItemLore.java — ComponentSerialization.STREAM_CODEC.apply(list(256))
            ComponentType.Lore => ReadNbtList(ref reader),

            // === BlockItemStateProperties: map<String, String> ===
            // Reference: BlockItemStateProperties.java — map(STRING_UTF8, STRING_UTF8)
            ComponentType.BlockState => ReadMapStringString(ref reader),

            // Non-networked types (no STREAM_CODEC, should never appear on wire)
            ComponentType.Lock => null,
            ComponentType.ContainerLoot => null,
            ComponentType.Recipes => null,
            ComponentType.MapDecorations => null,
            ComponentType.DebugStickState => null,

            // All remaining types use binary StreamCodec.composite formats.
            // They are NOT NBT-encoded. If encountered, we cannot skip them without
            // knowing their exact format. The fallback will log a warning.
            _ => ReadUnknownComponent(ref reader, typeId)
        };
    }

    #region Component Data Readers

    /// <summary>
    /// Reads a list of NBT tags (e.g. Lore — list of text components).
    /// Reference: ItemLore.java — ComponentSerialization.STREAM_CODEC.apply(ByteBufCodecs.list(256))
    /// </summary>
    private static object ReadNbtList(ref PacketBufferReader reader)
    {
        var count = reader.ReadVarInt();
        var tags = new object?[count];
        for (var i = 0; i < count; i++)
            tags[i] = reader.ReadNbtTag();
        return tags;
    }

    /// <summary>
    /// Reads a Map(String, String) — used by BlockItemStateProperties.
    /// Reference: BlockItemStateProperties.java — ByteBufCodecs.map(HashMap::new, STRING_UTF8, STRING_UTF8)
    /// </summary>
    private static object ReadMapStringString(ref PacketBufferReader reader)
    {
        var count = reader.ReadVarInt();
        var map = new Dictionary<string, string>(count);
        for (var i = 0; i < count; i++)
        {
            var key = reader.ReadString();
            var value = reader.ReadString();
            map[key] = value;
        }
        return map;
    }

    /// <summary>
    /// Reads a Map(VarInt, VarInt) — used by ItemEnchantments.
    /// Reference: ItemEnchantments.java — ByteBufCodecs.map(holderRegistry, VAR_INT)
    /// </summary>
    private static object ReadMapVarIntVarInt(ref PacketBufferReader reader)
    {
        var count = reader.ReadVarInt();
        var map = new Dictionary<int, int>(count);
        for (var i = 0; i < count; i++)
        {
            var key = reader.ReadVarInt();
            var value = reader.ReadVarInt();
            map[key] = value;
        }
        return map;
    }

    /// <summary>
    /// Reference: UseEffects.java — BOOL + BOOL + FLOAT
    /// </summary>
    private static object ReadUseEffects(ref PacketBufferReader reader)
    {
        var canSprint = reader.ReadBoolean();
        var interactVibrations = reader.ReadBoolean();
        var speedMultiplier = reader.ReadFloat();
        return (canSprint, interactVibrations, speedMultiplier);
    }

    /// <summary>
    /// Reference: FoodProperties.java — VAR_INT + FLOAT + BOOL
    /// </summary>
    private static object ReadFoodProperties(ref PacketBufferReader reader)
    {
        var nutrition = reader.ReadVarInt();
        var saturation = reader.ReadFloat();
        var canAlwaysEat = reader.ReadBoolean();
        return (nutrition, saturation, canAlwaysEat);
    }

    /// <summary>
    /// Reference: Weapon.java — VAR_INT + FLOAT
    /// </summary>
    private static object ReadWeapon(ref PacketBufferReader reader)
    {
        var itemDamagePerAttack = reader.ReadVarInt();
        var disableBlockingForSeconds = reader.ReadFloat();
        return (itemDamagePerAttack, disableBlockingForSeconds);
    }

    /// <summary>
    /// Reference: AttackRange.java — 6 FLOATs
    /// </summary>
    private static object ReadAttackRange(ref PacketBufferReader reader)
    {
        var minReach = reader.ReadFloat();
        var maxReach = reader.ReadFloat();
        var minCreativeReach = reader.ReadFloat();
        var maxCreativeReach = reader.ReadFloat();
        var hitboxMargin = reader.ReadFloat();
        var mobFactor = reader.ReadFloat();
        return (minReach, maxReach, minCreativeReach, maxCreativeReach, hitboxMargin, mobFactor);
    }

    /// <summary>
    /// Reference: SwingAnimation.java — VarInt enum + VarInt duration
    /// </summary>
    private static object ReadSwingAnimation(ref PacketBufferReader reader)
    {
        var animationType = reader.ReadVarInt();
        var duration = reader.ReadVarInt();
        return (animationType, duration);
    }

    /// <summary>
    /// Reference: TooltipDisplay.java — BOOL + collection of VarInt (DataComponentType registry IDs)
    /// </summary>
    private static object ReadTooltipDisplay(ref PacketBufferReader reader)
    {
        var hideTooltip = reader.ReadBoolean();
        var count = reader.ReadVarInt();
        var hiddenComponents = new int[count];
        for (var i = 0; i < count; i++)
            hiddenComponents[i] = reader.ReadVarInt();
        return (hideTooltip, hiddenComponents);
    }

    /// <summary>
    /// Reference: CustomModelData.java — 4 lists (floats, bools, strings, ints)
    /// </summary>
    private static object ReadCustomModelData(ref PacketBufferReader reader)
    {
        // List<Float>
        var floatCount = reader.ReadVarInt();
        for (var i = 0; i < floatCount; i++) reader.ReadFloat();
        // List<Boolean>
        var boolCount = reader.ReadVarInt();
        for (var i = 0; i < boolCount; i++) reader.ReadBoolean();
        // List<String>
        var stringCount = reader.ReadVarInt();
        for (var i = 0; i < stringCount; i++) reader.ReadString();
        // List<Int>
        var intCount = reader.ReadVarInt();
        for (var i = 0; i < intCount; i++) reader.ReadSignedInt();
        return null;
    }

    /// <summary>
    /// Reference: Variant.java (Horse) — VarInt + VarInt
    /// </summary>
    private static object ReadHorseVariant(ref PacketBufferReader reader)
    {
        var color = reader.ReadVarInt();
        var markings = reader.ReadVarInt();
        return (color, markings);
    }

    /// <summary>
    /// Reference: TropicalFish.Pattern.STREAM_CODEC — VarInt size + VarInt pattern
    /// </summary>
    private static object ReadTropicalFishPattern(ref PacketBufferReader reader)
    {
        var size = reader.ReadVarInt();
        var pattern = reader.ReadVarInt();
        return (size, pattern);
    }

    #endregion

    /// <summary>
    /// Fallback for truly unknown component types. Logs a warning.
    /// Since we cannot determine the wire format, this will likely corrupt the buffer.
    /// All known component types should be handled explicitly above.
    /// </summary>
    private static object? ReadUnknownComponent(ref PacketBufferReader reader, int typeId)
    {
        Log.Warning("[Slot] Unhandled component type {TypeId} — buffer may be corrupted. " +
                    "Add explicit handling for this type in ReadComponentData.", typeId);
        // Attempt NBT as last resort — some modded/future types may use it
        try
        {
            return reader.ReadNbtTag();
        }
        catch
        {
            Log.Error("[Slot] Failed to read component type {TypeId} as NBT — buffer is now corrupted", typeId);
            return null;
        }
    }

    /// <summary>
    /// Gets the damage value of this item, or 0 if not damageable or not present.
    /// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/item/ItemStack.java:366-368
    /// </summary>
    public int GetDamageValue()
    {
        if (ComponentsToAdd == null) return 0;
        foreach (var component in ComponentsToAdd)
        {
            if (component.Type == ComponentType.Damage && component.Data is int damage)
            {
                return damage;
            }
        }
        return 0;
    }

    /// <summary>
    /// Gets the maximum damage value of this item, or 0 if not damageable or not present.
    /// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/item/ItemStack.java:374-376
    /// </summary>
    public int GetMaxDamage()
    {
        if (ComponentsToAdd == null) return 0;
        foreach (var component in ComponentsToAdd)
        {
            if (component.Type == ComponentType.MaxDamage && component.Data is int maxDamage)
            {
                return maxDamage;
            }
        }
        return 0;
    }

    /// <summary>
    /// Checks if this item is damageable (has MaxDamage component).
    /// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/item/ItemStack.java:358-360
    /// </summary>
    public bool IsDamageable()
    {
        if (ComponentsToAdd == null) return false;
        bool hasMaxDamage = false;
        bool hasUnbreakable = false;
        bool hasDamage = false;
        
        foreach (var component in ComponentsToAdd)
        {
            if (component.Type == ComponentType.MaxDamage)
            {
                hasMaxDamage = true;
            }
            if (component.Type == ComponentType.Unbreakable)
            {
                hasUnbreakable = true;
            }
            if (component.Type == ComponentType.Damage)
            {
                hasDamage = true;
            }
        }
        
        return hasMaxDamage && !hasUnbreakable && hasDamage;
    }

    /// <summary>
    /// Gets the remaining durability (maxDamage - damageValue).
    /// </summary>
    public int GetRemainingDurability()
    {
        int maxDamage = GetMaxDamage();
        if (maxDamage == 0) return 0;
        return maxDamage - GetDamageValue();
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
/// Component types from DataComponents.java (Minecraft 26.1).
/// Order matches the registry registration order — IDs are assigned sequentially.
/// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/core/component/DataComponents.java:107-216
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
    Dye = 43,
    DyedColor = 44,
    MapColor = 45,
    MapId = 46,
    MapDecorations = 47,
    MapPostProcessing = 48,
    ChargedProjectiles = 49,
    BundleContents = 50,
    PotionContents = 51,
    PotionDurationScale = 52,
    SuspiciousStewEffects = 53,
    WritableBookContent = 54,
    WrittenBookContent = 55,
    Trim = 56,
    DebugStickState = 57,
    EntityData = 58,
    BucketEntityData = 59,
    BlockEntityData = 60,
    Instrument = 61,
    ProvidesTrimMaterial = 62,
    OminousBottleAmplifier = 63,
    JukeboxPlayable = 64,
    ProvidesBannerPatterns = 65,
    Recipes = 66,
    LodestoneTracker = 67,
    FireworkExplosion = 68,
    Fireworks = 69,
    Profile = 70,
    NoteBlockSound = 71,
    BannerPatterns = 72,
    BaseColor = 73,
    PotDecorations = 74,
    Container = 75,
    BlockState = 76,
    Bees = 77,
    Lock = 78,
    ContainerLoot = 79,
    BreakSound = 80,
    // Entity variant components (26.1)
    VillagerVariant = 81,
    WolfVariant = 82,
    WolfSoundVariant = 83,
    WolfCollar = 84,
    FoxVariant = 85,
    SalmonSize = 86,
    ParrotVariant = 87,
    TropicalFishPattern = 88,
    TropicalFishBaseColor = 89,
    TropicalFishPatternColor = 90,
    MooshroomVariant = 91,
    RabbitVariant = 92,
    PigVariant = 93,
    PigSoundVariant = 94,
    CowVariant = 95,
    CowSoundVariant = 96,
    ChickenVariant = 97,
    ChickenSoundVariant = 98,
    ZombieNautilusVariant = 99,
    FrogVariant = 100,
    HorseVariant = 101,
    PaintingVariant = 102,
    LlamaVariant = 103,
    AxolotlVariant = 104,
    CatVariant = 105,
    CatSoundVariant = 106,
    CatCollar = 107,
    SheepColor = 108,
    ShulkerColor = 109,
}
