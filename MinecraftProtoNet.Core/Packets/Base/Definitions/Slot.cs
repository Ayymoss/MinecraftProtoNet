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
        ComponentType lastType = 0;
        for (var i = 0; i < componentsToAddCount; i++)
        {
            var typeId = reader.ReadVarInt();
            var type = (ComponentType)typeId;
            try
            {
                var data = ReadComponentData(ref reader, type, typeId);
                slot.ComponentsToAdd[i] = new StructuredComponent
                {
                    Type = type,
                    Data = data
                };
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed reading component {type} (id={typeId}) at index {i}/{componentsToAddCount} " +
                    $"for ItemId={slot.ItemId}, ItemCount={slot.ItemCount}. " +
                    $"Previous component: {(i > 0 ? lastType.ToString() : "none")}. " +
                    $"Components to remove: {componentsToRemoveCount}", ex);
            }
            lastType = type;
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

            // === AdventureModePredicate types (list of BlockPredicates) ===
            // Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/world/item/AdventureModePredicate.java
            ComponentType.CanPlaceOn => ReadAdventureModePredicate(ref reader),
            ComponentType.CanBreak => ReadAdventureModePredicate(ref reader),

            // === ItemAttributeModifiers (list of Entry) ===
            // Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/world/item/component/ItemAttributeModifiers.java
            ComponentType.AttributeModifiers => ReadAttributeModifiers(ref reader),

            // === Consumable: Float + VarInt(animation) + Holder<SoundEvent> + Bool + list<ConsumeEffect> ===
            // Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/world/item/component/Consumable.java
            ComponentType.Consumable => ReadConsumable(ref reader),

            // === UseRemainder: recursive Slot (ItemStack) ===
            // Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/world/item/component/UseRemainder.java
            ComponentType.UseRemainder => Slot.Read(ref reader),

            // === UseCooldown: Float + Optional<String> ===
            // Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/world/item/component/UseCooldown.java
            ComponentType.UseCooldown => ReadUseCooldown(ref reader),

            // === Tool: list<Rule> + Float + VarInt + Bool ===
            // Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/world/item/component/Tool.java
            ComponentType.Tool => ReadTool(ref reader),

            // === Equippable: VarInt + Holder<SoundEvent> + Optional<String> + Optional<String> + Optional<HolderSet> + 5×Bool + Holder<SoundEvent> ===
            // Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/world/item/equipment/Equippable.java
            ComponentType.Equippable => ReadEquippable(ref reader),

            // === Repairable: HolderSet<Item> ===
            // Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/world/item/component/Repairable.java (holderSet)
            ComponentType.Repairable => ReadRepairable(ref reader),

            // === DeathProtection: list<ConsumeEffect> ===
            // Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/world/item/component/DeathProtection.java
            ComponentType.DeathProtection => ReadDeathProtection(ref reader),

            // === BlocksAttacks: complex composite ===
            // Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/world/item/component/BlocksAttacks.java
            ComponentType.BlocksAttacks => ReadBlocksAttacks(ref reader),

            // === PiercingWeapon: Bool + Bool + Optional<Holder<SoundEvent>> + Optional<Holder<SoundEvent>> ===
            // Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/world/item/component/PiercingWeapon.java
            ComponentType.PiercingWeapon => ReadPiercingWeapon(ref reader),

            // === KineticWeapon: VarInt + VarInt + 3×Optional<Condition> + Float + Float + 2×Optional<Holder<SoundEvent>> ===
            // Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/world/item/component/KineticWeapon.java
            ComponentType.KineticWeapon => ReadKineticWeapon(ref reader),

            // === ChargedProjectiles: list<Slot> ===
            // Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/world/item/component/ChargedProjectiles.java
            ComponentType.ChargedProjectiles => ReadSlotList(ref reader),

            // === BundleContents: list<Slot> ===
            // Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/world/item/component/BundleContents.java
            ComponentType.BundleContents => ReadSlotList(ref reader),

            // === PotionContents: Optional<VarInt> + Optional<Int> + list<MobEffectInstance> + Optional<String> ===
            // Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/world/item/alchemy/PotionContents.java
            ComponentType.PotionContents => ReadPotionContents(ref reader),

            // === SuspiciousStewEffects: list<Entry(VarInt + VarInt)> ===
            // Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/world/item/component/SuspiciousStewEffects.java
            ComponentType.SuspiciousStewEffects => ReadSuspiciousStewEffects(ref reader),

            // === WritableBookContent: list<Filterable<String>> ===
            // Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/world/item/component/WritableBookContent.java
            ComponentType.WritableBookContent => ReadWritableBookContent(ref reader),

            // === WrittenBookContent: Filterable<String> + String + VarInt + list<Filterable<NbtTag>> + Bool ===
            // Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/world/item/component/WrittenBookContent.java
            ComponentType.WrittenBookContent => ReadWrittenBookContent(ref reader),

            // === Trim: VarInt(material holder) + VarInt(pattern holder) ===
            // Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/world/item/armortrim/ArmorTrim.java
            ComponentType.Trim => ReadTrim(ref reader),

            // === EntityData: NbtTag (CustomData.STREAM_CODEC) ===
            // Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/core/component/DataComponents.java
            ComponentType.EntityData => reader.ReadNbtTag(),

            // === BlockEntityData: NbtTag (CustomData.STREAM_CODEC) ===
            // Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/core/component/DataComponents.java
            ComponentType.BlockEntityData => reader.ReadNbtTag(),

            // === Instrument: Holder<Instrument> via ByteBufCodecs.holder() ===
            // Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/world/item/component/InstrumentComponent.java
            ComponentType.Instrument => ReadInstrument(ref reader),

            // === JukeboxPlayable: Holder<JukeboxSong> (VarInt registry ref) ===
            // Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/world/item/JukeboxPlayable.java
            ComponentType.JukeboxPlayable => ReadJukeboxPlayable(ref reader),

            // === LodestoneTracker: Optional<GlobalPos> + Bool ===
            // Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/world/item/component/LodestoneTracker.java
            ComponentType.LodestoneTracker => ReadLodestoneTracker(ref reader),

            // === FireworkExplosion: VarInt + IntList + IntList + Bool + Bool ===
            // Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/world/item/component/FireworkExplosion.java
            ComponentType.FireworkExplosion => ReadFireworkExplosion(ref reader),

            // === Fireworks: VarInt + list<FireworkExplosion> ===
            // Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/world/item/component/Fireworks.java
            ComponentType.Fireworks => ReadFireworks(ref reader),

            // === Profile: Either<GameProfile, Partial> + SkinPatch ===
            // Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/world/item/component/ResolvableProfile.java
            ComponentType.Profile => ReadProfile(ref reader),

            // === BannerPatterns: list<(VarInt + VarInt)> ===
            // Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/world/level/block/entity/BannerPatternLayers.java
            ComponentType.BannerPatterns => ReadBannerPatterns(ref reader),

            // === PotDecorations: list<Optional<VarInt>> (exactly 4) ===
            // Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/world/level/block/entity/DecoratedPotBlockEntity.java
            ComponentType.PotDecorations => ReadPotDecorations(ref reader),

            // === Container: list<Slot> ===
            // Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/world/item/component/ItemContainerContents.java
            ComponentType.Container => ReadSlotList(ref reader),

            // === Bees: list<(NbtTag + VarInt + VarInt)> ===
            // Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/world/level/block/entity/BeehiveBlockEntity.java
            ComponentType.Bees => ReadBees(ref reader),

            // === BreakSound: Holder<SoundEvent> ===
            // Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/core/component/DataComponents.java
            ComponentType.BreakSound => ReadBreakSound(ref reader),

            // All remaining types use binary StreamCodec.composite formats.
            // They are NOT NBT-encoded. If encountered, we cannot skip them without
            // knowing their exact format. The fallback will log a warning.
            _ => ReadUnknownComponent(ref reader, typeId)
        };
    }

    #region Component Data Readers

    // ===========================================================================
    // Helper methods for reading common sub-structures
    // ===========================================================================

    /// <summary>
    /// Reads a HolderSet encoded via ByteBufCodecs.holderSet().
    /// Wire format: VarInt value; if 0 => tag key (String), else (value-1) entries of VarInt IDs.
    /// Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/network/codec/ByteBufCodecs.java holderSet()
    /// </summary>
    private static void ReadHolderSet(ref PacketBufferReader reader)
    {
        var value = reader.ReadVarInt();
        if (value == 0)
        {
            reader.ReadString(); // tag key
        }
        else
        {
            var count = value - 1;
            for (var i = 0; i < count; i++)
                reader.ReadVarInt(); // holder registry IDs
        }
    }

    /// <summary>
    /// Reads a Holder&lt;SoundEvent&gt; — inline (VarInt(0) + ResourceLocation + Optional&lt;Float&gt;) or reference (VarInt(id+1)).
    /// Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/sounds/SoundEvent.java STREAM_CODEC
    /// </summary>
    private static void ReadHolderSoundEvent(ref PacketBufferReader reader)
    {
        var value = reader.ReadVarInt();
        if (value == 0)
        {
            reader.ReadString(); // ResourceLocation
            if (reader.ReadBoolean()) reader.ReadFloat(); // Optional fixed range
        }
        // else: value-1 is registry ID, nothing more to read
    }

    /// <summary>
    /// Reads a ConsumeEffect dispatched by registry type ID.
    /// Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/world/item/consume_effects/ConsumeEffect.java
    /// Types: 0=ApplyEffects, 1=RemoveEffects, 2=ClearAllEffects, 3=TeleportRandomly, 4=PlaySound
    /// </summary>
    private static void ReadConsumeEffect(ref PacketBufferReader reader)
    {
        var typeId = reader.ReadVarInt();
        switch (typeId)
        {
            case 0: // ApplyStatusEffectsConsumeEffect: list<MobEffectInstance> + Float(probability)
                var count = reader.ReadVarInt();
                for (var i = 0; i < count; i++) ReadMobEffectInstance(ref reader);
                reader.ReadFloat(); // probability
                break;
            case 1: // RemoveStatusEffectsConsumeEffect: HolderSet<MobEffect>
                ReadHolderSet(ref reader);
                break;
            case 2: // ClearAllStatusEffectsConsumeEffect: unit (no data)
                break;
            case 3: // TeleportRandomlyConsumeEffect: Float(diameter)
                reader.ReadFloat();
                break;
            case 4: // PlaySoundConsumeEffect: Holder<SoundEvent>
                ReadHolderSoundEvent(ref reader);
                break;
        }
    }

    /// <summary>
    /// Reads a MobEffectInstance: Holder&lt;MobEffect&gt; + Details.
    /// Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/world/effect/MobEffectInstance.java
    /// </summary>
    private static void ReadMobEffectInstance(ref PacketBufferReader reader)
    {
        reader.ReadVarInt(); // effect holder
        ReadMobEffectDetails(ref reader);
    }

    /// <summary>
    /// Reads MobEffectInstance.Details: VarInt(amplifier) + VarInt(duration) + Bool(ambient) + Bool(showParticles) + Bool(showIcon) + Optional&lt;Details&gt;(recursive).
    /// Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/world/effect/MobEffectInstance.java Details record
    /// </summary>
    private static void ReadMobEffectDetails(ref PacketBufferReader reader)
    {
        reader.ReadVarInt(); // amplifier
        reader.ReadVarInt(); // duration
        reader.ReadBoolean(); // ambient
        reader.ReadBoolean(); // showParticles
        reader.ReadBoolean(); // showIcon
        if (reader.ReadBoolean()) // has hidden effect
            ReadMobEffectDetails(ref reader); // recursive
    }

    /// <summary>
    /// Reads a FireworkExplosion: VarInt(shape) + IntList(colors) + IntList(fadeColors) + Bool(trail) + Bool(twinkle).
    /// Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/world/item/component/FireworkExplosion.java
    /// </summary>
    private static object ReadFireworkExplosion(ref PacketBufferReader reader)
    {
        var shape = reader.ReadVarInt();
        var colorCount = reader.ReadVarInt();
        for (var i = 0; i < colorCount; i++) reader.ReadSignedInt();
        var fadeCount = reader.ReadVarInt();
        for (var i = 0; i < fadeCount; i++) reader.ReadSignedInt();
        var trail = reader.ReadBoolean();
        var twinkle = reader.ReadBoolean();
        return (shape, trail, twinkle);
    }

    /// <summary>
    /// Reads a PropertyMap (GameProfile properties): VarInt(count), each: String(name) + String(value) + nullable String(signature).
    /// Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/network/codec/ByteBufCodecs.java GAME_PROFILE_PROPERTIES
    /// </summary>
    private static void ReadPropertyMap(ref PacketBufferReader reader)
    {
        var count = reader.ReadVarInt();
        for (var i = 0; i < count; i++)
        {
            reader.ReadString(); // property name
            reader.ReadString(); // property value
            if (reader.ReadBoolean()) reader.ReadString(); // optional signature
        }
    }

    /// <summary>
    /// Reads a Filterable&lt;String&gt;: String(raw) + Optional&lt;String&gt;(filtered: Bool + String if true).
    /// Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/server/network/Filterable.java streamCodec()
    /// </summary>
    private static void ReadFilterableString(ref PacketBufferReader reader)
    {
        reader.ReadString(); // raw
        if (reader.ReadBoolean()) reader.ReadString(); // optional filtered
    }

    /// <summary>
    /// Reads a Filterable&lt;Component&gt;: NbtTag(raw) + Optional&lt;NbtTag&gt;(filtered).
    /// Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/server/network/Filterable.java streamCodec()
    /// </summary>
    private static void ReadFilterableComponent(ref PacketBufferReader reader)
    {
        reader.ReadNbtTag(); // raw component
        if (reader.ReadBoolean()) reader.ReadNbtTag(); // optional filtered
    }

    /// <summary>
    /// Reads a BlockPredicate: Optional&lt;HolderSet&gt; + Optional&lt;StatePropertiesPredicate&gt; + Optional&lt;NbtPredicate&gt; + DataComponentMatchers.
    /// Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/advancements/criterion/BlockPredicate.java
    /// </summary>
    private static void ReadBlockPredicate(ref PacketBufferReader reader)
    {
        // Optional<HolderSet<Block>>
        if (reader.ReadBoolean()) ReadHolderSet(ref reader);
        // Optional<StatePropertiesPredicate> — list of PropertyMatcher
        if (reader.ReadBoolean()) ReadStatePropertiesPredicate(ref reader);
        // Optional<NbtPredicate> — CompoundTag
        if (reader.ReadBoolean()) reader.ReadNbtTag();
        // DataComponentMatchers — DataComponentExactPredicate + DataComponentPredicate
        ReadDataComponentMatchers(ref reader);
    }

    /// <summary>
    /// Reads StatePropertiesPredicate: list of PropertyMatcher(String + ValueMatcher).
    /// Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/advancements/criterion/StatePropertiesPredicate.java
    /// </summary>
    private static void ReadStatePropertiesPredicate(ref PacketBufferReader reader)
    {
        var count = reader.ReadVarInt();
        for (var i = 0; i < count; i++)
        {
            reader.ReadString(); // property name
            // ValueMatcher: Either<ExactMatcher, RangedMatcher> via Bool dispatch
            if (reader.ReadBoolean())
            {
                // Left = ExactMatcher: String
                reader.ReadString();
            }
            else
            {
                // Right = RangedMatcher: Optional<String> + Optional<String>
                if (reader.ReadBoolean()) reader.ReadString(); // min
                if (reader.ReadBoolean()) reader.ReadString(); // max
            }
        }
    }

    /// <summary>
    /// Reads DataComponentMatchers: DataComponentExactPredicate + DataComponentPredicate map.
    /// Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/advancements/criterion/DataComponentMatchers.java
    /// </summary>
    private static void ReadDataComponentMatchers(ref PacketBufferReader reader)
    {
        // DataComponentExactPredicate: list<TypedDataComponent> — each is VarInt(typeId) + component data
        var exactCount = reader.ReadVarInt();
        for (var i = 0; i < exactCount; i++)
        {
            var typeId = reader.ReadVarInt();
            var type = (ComponentType)typeId;
            ReadComponentData(ref reader, type, typeId); // recursive component read
        }
        // DataComponentPredicate: list<Single> — each is VarInt(typeId) + predicate data (dispatched)
        var predicateCount = reader.ReadVarInt();
        for (var i = 0; i < predicateCount; i++)
        {
            var typeId = reader.ReadVarInt();
            // Predicate data is dispatched by type. For now, read as NBT since most predicates use NBT encoding.
            // This may not be perfect for all types but avoids buffer corruption for the common case.
            reader.ReadNbtTag();
        }
    }

    /// <summary>
    /// Reads a PlayerSkin.Patch: 3×Optional&lt;ResourceTexture&gt;(String) + Optional&lt;PlayerModelType&gt;(Bool→Bool).
    /// Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/world/entity/player/PlayerSkin.java Patch
    /// </summary>
    private static void ReadSkinPatch(ref PacketBufferReader reader)
    {
        // Optional<ResourceTexture> body — ResourceTexture.STREAM_CODEC = Identifier.STREAM_CODEC = String
        if (reader.ReadBoolean()) reader.ReadString();
        // Optional<ResourceTexture> cape
        if (reader.ReadBoolean()) reader.ReadString();
        // Optional<ResourceTexture> elytra
        if (reader.ReadBoolean()) reader.ReadString();
        // Optional<PlayerModelType> model — PlayerModelType.STREAM_CODEC = Bool.map(SLIM/WIDE)
        if (reader.ReadBoolean()) reader.ReadBoolean();
    }

    /// <summary>
    /// Reads a KineticWeapon.Condition: VarInt(maxDurationTicks) + Float(minSpeed) + Float(minRelativeSpeed).
    /// Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/world/item/component/KineticWeapon.java Condition
    /// </summary>
    private static void ReadKineticWeaponCondition(ref PacketBufferReader reader)
    {
        reader.ReadVarInt(); // maxDurationTicks
        reader.ReadFloat(); // minSpeed
        reader.ReadFloat(); // minRelativeSpeed
    }

    /// <summary>
    /// Reads a Holder&lt;T&gt; using ByteBufCodecs.holder(): VarInt id. If 0, read inline data; else id-1 is registry ref.
    /// This variant reads the VarInt only (for simple holder references like JukeboxSong).
    /// Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/network/codec/ByteBufCodecs.java holder()
    /// </summary>
    private static void ReadHolder(ref PacketBufferReader reader)
    {
        reader.ReadVarInt(); // holder ID (registry reference)
    }

    // ===========================================================================
    // Component-specific readers
    // ===========================================================================

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

    // ===========================================================================
    // Missing component type readers
    // ===========================================================================

    /// <summary>
    /// Reads AdventureModePredicate: list&lt;BlockPredicate&gt;.
    /// Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/world/item/AdventureModePredicate.java
    /// </summary>
    private static object? ReadAdventureModePredicate(ref PacketBufferReader reader)
    {
        var count = reader.ReadVarInt();
        for (var i = 0; i < count; i++)
            ReadBlockPredicate(ref reader);
        return null;
    }

    /// <summary>
    /// Reads ItemAttributeModifiers: list&lt;Entry&gt;.
    /// Entry = Holder&lt;Attribute&gt;(VarInt) + AttributeModifier(String + Double + VarInt(operation)) + EquipmentSlotGroup(VarInt) + Display(dispatched).
    /// Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/world/item/component/ItemAttributeModifiers.java
    /// </summary>
    private static object? ReadAttributeModifiers(ref PacketBufferReader reader)
    {
        var count = reader.ReadVarInt();
        for (var i = 0; i < count; i++)
        {
            reader.ReadVarInt(); // Holder<Attribute>
            // AttributeModifier: Identifier(String) + Double(amount) + Operation(VarInt)
            reader.ReadString(); // modifier id
            reader.ReadDouble(); // amount
            reader.ReadVarInt(); // operation
            // EquipmentSlotGroup: VarInt
            reader.ReadVarInt();
            // Display: dispatched by Type VarInt
            var displayType = reader.ReadVarInt();
            switch (displayType)
            {
                case 0: // Default — no additional data (unit)
                    break;
                case 1: // Hidden — no additional data (unit)
                    break;
                case 2: // OverrideText — Component (NbtTag)
                    reader.ReadNbtTag();
                    break;
            }
        }
        return null;
    }

    /// <summary>
    /// Reads Consumable: Float + VarInt(animation) + Holder&lt;SoundEvent&gt; + Bool + list&lt;ConsumeEffect&gt;.
    /// Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/world/item/component/Consumable.java
    /// </summary>
    private static object? ReadConsumable(ref PacketBufferReader reader)
    {
        reader.ReadFloat(); // consumeSeconds
        reader.ReadVarInt(); // animation (ItemUseAnimation)
        ReadHolderSoundEvent(ref reader); // sound
        reader.ReadBoolean(); // hasConsumeParticles
        var effectCount = reader.ReadVarInt();
        for (var i = 0; i < effectCount; i++)
            ReadConsumeEffect(ref reader);
        return null;
    }

    /// <summary>
    /// Reads UseCooldown: Float(seconds) + Optional&lt;Identifier&gt;(cooldownGroup).
    /// Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/world/item/component/UseCooldown.java
    /// </summary>
    private static object? ReadUseCooldown(ref PacketBufferReader reader)
    {
        reader.ReadFloat(); // seconds
        if (reader.ReadBoolean()) reader.ReadString(); // optional cooldown group (Identifier)
        return null;
    }

    /// <summary>
    /// Reads Tool: list&lt;Rule&gt; + Float + VarInt + Bool.
    /// Rule = HolderSet&lt;Block&gt; + Optional&lt;Float&gt;(speed) + Optional&lt;Bool&gt;(correctForDrops).
    /// Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/world/item/component/Tool.java
    /// </summary>
    private static object? ReadTool(ref PacketBufferReader reader)
    {
        var ruleCount = reader.ReadVarInt();
        for (var i = 0; i < ruleCount; i++)
        {
            ReadHolderSet(ref reader); // blocks
            if (reader.ReadBoolean()) reader.ReadFloat(); // optional speed
            if (reader.ReadBoolean()) reader.ReadBoolean(); // optional correctForDrops
        }
        reader.ReadFloat(); // defaultMiningSpeed
        reader.ReadVarInt(); // damagePerBlock
        reader.ReadBoolean(); // canDestroyBlocksInCreative
        return null;
    }

    /// <summary>
    /// Reads Equippable: VarInt(slot) + Holder&lt;SoundEvent&gt; + Optional&lt;ResourceKey&gt; + Optional&lt;Identifier&gt; + Optional&lt;HolderSet&gt; + 5×Bool + Holder&lt;SoundEvent&gt;.
    /// Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/world/item/equipment/Equippable.java
    /// </summary>
    private static object? ReadEquippable(ref PacketBufferReader reader)
    {
        reader.ReadVarInt(); // EquipmentSlot
        ReadHolderSoundEvent(ref reader); // equipSound
        if (reader.ReadBoolean()) reader.ReadString(); // optional assetId (ResourceKey = String)
        if (reader.ReadBoolean()) reader.ReadString(); // optional cameraOverlay (Identifier)
        if (reader.ReadBoolean()) ReadHolderSet(ref reader); // optional allowedEntities
        reader.ReadBoolean(); // dispensable
        reader.ReadBoolean(); // swappable
        reader.ReadBoolean(); // damageOnHurt
        reader.ReadBoolean(); // equipOnInteract
        reader.ReadBoolean(); // canBeSheared
        ReadHolderSoundEvent(ref reader); // shearingSound
        return null;
    }

    /// <summary>
    /// Reads Repairable: HolderSet&lt;Item&gt;.
    /// Reference: minecraft-26.1.1-REFERENCE-ONLY holderSet encoding
    /// </summary>
    private static object? ReadRepairable(ref PacketBufferReader reader)
    {
        ReadHolderSet(ref reader);
        return null;
    }

    /// <summary>
    /// Reads DeathProtection: list&lt;ConsumeEffect&gt;.
    /// Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/world/item/component/DeathProtection.java
    /// </summary>
    private static object? ReadDeathProtection(ref PacketBufferReader reader)
    {
        var count = reader.ReadVarInt();
        for (var i = 0; i < count; i++)
            ReadConsumeEffect(ref reader);
        return null;
    }

    /// <summary>
    /// Reads BlocksAttacks: Float + Float + list&lt;DamageReduction&gt; + ItemDamageFunction + Optional&lt;HolderSet&gt; + Optional&lt;Holder&lt;SoundEvent&gt;&gt; + Optional&lt;Holder&lt;SoundEvent&gt;&gt;.
    /// Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/world/item/component/BlocksAttacks.java
    /// </summary>
    private static object? ReadBlocksAttacks(ref PacketBufferReader reader)
    {
        reader.ReadFloat(); // blockDelaySeconds
        reader.ReadFloat(); // disableCooldownScale
        // list<DamageReduction>: each = Float(angle) + Optional<HolderSet<DamageType>> + Float(base) + Float(factor)
        var reductionCount = reader.ReadVarInt();
        for (var i = 0; i < reductionCount; i++)
        {
            reader.ReadFloat(); // horizontalBlockingAngle
            if (reader.ReadBoolean()) ReadHolderSet(ref reader); // optional type
            reader.ReadFloat(); // base
            reader.ReadFloat(); // factor
        }
        // ItemDamageFunction: Float(threshold) + Float(base) + Float(factor)
        reader.ReadFloat(); // threshold
        reader.ReadFloat(); // base
        reader.ReadFloat(); // factor
        // Optional<HolderSet<DamageType>> bypassedBy
        if (reader.ReadBoolean()) ReadHolderSet(ref reader);
        // Optional<Holder<SoundEvent>> blockSound
        if (reader.ReadBoolean()) ReadHolderSoundEvent(ref reader);
        // Optional<Holder<SoundEvent>> disableSound
        if (reader.ReadBoolean()) ReadHolderSoundEvent(ref reader);
        return null;
    }

    /// <summary>
    /// Reads PiercingWeapon: Bool + Bool + Optional&lt;Holder&lt;SoundEvent&gt;&gt; + Optional&lt;Holder&lt;SoundEvent&gt;&gt;.
    /// Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/world/item/component/PiercingWeapon.java
    /// </summary>
    private static object? ReadPiercingWeapon(ref PacketBufferReader reader)
    {
        reader.ReadBoolean(); // dealsKnockback
        reader.ReadBoolean(); // dismounts
        if (reader.ReadBoolean()) ReadHolderSoundEvent(ref reader); // optional sound
        if (reader.ReadBoolean()) ReadHolderSoundEvent(ref reader); // optional hitSound
        return null;
    }

    /// <summary>
    /// Reads KineticWeapon: VarInt + VarInt + 3×Optional&lt;Condition&gt; + Float + Float + 2×Optional&lt;Holder&lt;SoundEvent&gt;&gt;.
    /// Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/world/item/component/KineticWeapon.java
    /// </summary>
    private static object? ReadKineticWeapon(ref PacketBufferReader reader)
    {
        reader.ReadVarInt(); // contactCooldownTicks
        reader.ReadVarInt(); // delayTicks
        // 3× Optional<Condition>
        if (reader.ReadBoolean()) ReadKineticWeaponCondition(ref reader); // dismountConditions
        if (reader.ReadBoolean()) ReadKineticWeaponCondition(ref reader); // knockbackConditions
        if (reader.ReadBoolean()) ReadKineticWeaponCondition(ref reader); // damageConditions
        reader.ReadFloat(); // forwardMovement
        reader.ReadFloat(); // damageMultiplier
        if (reader.ReadBoolean()) ReadHolderSoundEvent(ref reader); // optional sound
        if (reader.ReadBoolean()) ReadHolderSoundEvent(ref reader); // optional hitSound
        return null;
    }

    /// <summary>
    /// Reads a list of Slots (ItemStacks) — used by ChargedProjectiles, BundleContents, Container.
    /// </summary>
    private static object? ReadSlotList(ref PacketBufferReader reader)
    {
        var count = reader.ReadVarInt();
        var slots = new Slot[count];
        for (var i = 0; i < count; i++)
            slots[i] = Slot.Read(ref reader);
        return slots;
    }

    /// <summary>
    /// Reads PotionContents: Optional&lt;Holder&lt;Potion&gt;&gt;(VarInt) + Optional&lt;Int&gt;(customColor) + list&lt;MobEffectInstance&gt; + Optional&lt;String&gt;.
    /// Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/world/item/alchemy/PotionContents.java
    /// </summary>
    private static object? ReadPotionContents(ref PacketBufferReader reader)
    {
        // Optional<Holder<Potion>> — Potion.STREAM_CODEC = holderRegistry VarInt, wrapped in optional
        if (reader.ReadBoolean()) reader.ReadVarInt(); // potion holder
        // Optional<Integer> customColor
        if (reader.ReadBoolean()) reader.ReadSignedInt();
        // list<MobEffectInstance>
        var effectCount = reader.ReadVarInt();
        for (var i = 0; i < effectCount; i++)
            ReadMobEffectInstance(ref reader);
        // Optional<String> customName
        if (reader.ReadBoolean()) reader.ReadString();
        return null;
    }

    /// <summary>
    /// Reads SuspiciousStewEffects: list&lt;Entry(Holder&lt;MobEffect&gt; + VarInt(duration))&gt;.
    /// Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/world/item/component/SuspiciousStewEffects.java
    /// </summary>
    private static object? ReadSuspiciousStewEffects(ref PacketBufferReader reader)
    {
        var count = reader.ReadVarInt();
        for (var i = 0; i < count; i++)
        {
            reader.ReadVarInt(); // effect holder
            reader.ReadVarInt(); // duration
        }
        return null;
    }

    /// <summary>
    /// Reads WritableBookContent: list&lt;Filterable&lt;String&gt;&gt;.
    /// Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/world/item/component/WritableBookContent.java
    /// </summary>
    private static object? ReadWritableBookContent(ref PacketBufferReader reader)
    {
        var pageCount = reader.ReadVarInt();
        for (var i = 0; i < pageCount; i++)
            ReadFilterableString(ref reader);
        return null;
    }

    /// <summary>
    /// Reads WrittenBookContent: Filterable&lt;String&gt;(title) + String(author) + VarInt(generation) + list&lt;Filterable&lt;Component&gt;&gt;(pages) + Bool(resolved).
    /// Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/world/item/component/WrittenBookContent.java
    /// </summary>
    private static object? ReadWrittenBookContent(ref PacketBufferReader reader)
    {
        ReadFilterableString(ref reader); // title
        reader.ReadString(); // author
        reader.ReadVarInt(); // generation
        var pageCount = reader.ReadVarInt();
        for (var i = 0; i < pageCount; i++)
            ReadFilterableComponent(ref reader); // pages (Filterable<Component> = NbtTag + Optional<NbtTag>)
        reader.ReadBoolean(); // resolved
        return null;
    }

    /// <summary>
    /// Reads Trim: VarInt(material holder) + VarInt(pattern holder).
    /// Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/world/item/armortrim/ArmorTrim.java
    /// </summary>
    private static object ReadTrim(ref PacketBufferReader reader)
    {
        var material = reader.ReadVarInt();
        var pattern = reader.ReadVarInt();
        return (material, pattern);
    }

    /// <summary>
    /// Reads Instrument: Holder&lt;Instrument&gt; via ByteBufCodecs.holder() — VarInt(0) + inline OR VarInt(id+1).
    /// Inline = Holder&lt;SoundEvent&gt; + Float(useDuration) + Float(range) + Component(NbtTag)(description).
    /// Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/world/item/Instrument.java
    /// </summary>
    private static object? ReadInstrument(ref PacketBufferReader reader)
    {
        var value = reader.ReadVarInt();
        if (value == 0)
        {
            // Inline Instrument
            ReadHolderSoundEvent(ref reader); // soundEvent
            reader.ReadFloat(); // useDuration
            reader.ReadFloat(); // range
            reader.ReadNbtTag(); // description (Component via ComponentSerialization)
        }
        // else: value-1 is registry ID, nothing more to read
        return null;
    }

    /// <summary>
    /// Reads JukeboxPlayable: Holder&lt;JukeboxSong&gt; — uses holderRegistry so just VarInt.
    /// Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/world/item/JukeboxPlayable.java
    /// </summary>
    private static object? ReadJukeboxPlayable(ref PacketBufferReader reader)
    {
        reader.ReadVarInt(); // song holder
        return null;
    }

    /// <summary>
    /// Reads LodestoneTracker: Optional&lt;GlobalPos&gt; + Bool.
    /// GlobalPos = ResourceKey&lt;Level&gt;(String) + BlockPos(Long packed).
    /// Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/world/item/component/LodestoneTracker.java
    /// </summary>
    private static object? ReadLodestoneTracker(ref PacketBufferReader reader)
    {
        // Optional<GlobalPos>
        if (reader.ReadBoolean())
        {
            reader.ReadString(); // dimension (ResourceKey = String)
            reader.ReadSignedLong(); // BlockPos packed as Long
        }
        reader.ReadBoolean(); // tracked
        return null;
    }

    /// <summary>
    /// Reads Fireworks: VarInt(flightDuration) + list&lt;FireworkExplosion&gt;.
    /// Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/world/item/component/Fireworks.java
    /// </summary>
    private static object? ReadFireworks(ref PacketBufferReader reader)
    {
        reader.ReadVarInt(); // flightDuration
        var explosionCount = reader.ReadVarInt();
        for (var i = 0; i < explosionCount; i++)
            ReadFireworkExplosion(ref reader);
        return null;
    }

    /// <summary>
    /// Reads Profile (ResolvableProfile): Either&lt;GameProfile, Partial&gt; + SkinPatch.
    /// Either is: Bool(true) → GameProfile | Bool(false) → Partial.
    /// GameProfile = UUID(readLong+readLong) + String(name, max 16) + PropertyMap.
    /// Partial = Optional&lt;String&gt;(name) + Optional&lt;UUID&gt; + PropertyMap.
    /// Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/world/item/component/ResolvableProfile.java
    /// </summary>
    private static object? ReadProfile(ref PacketBufferReader reader)
    {
        var isLeft = reader.ReadBoolean(); // Either dispatch: true=GameProfile, false=Partial
        if (isLeft)
        {
            // GameProfile: UUID + String(name, max 16) + PropertyMap
            reader.ReadUuid(); // UUID (16 bytes: readLong + readLong)
            reader.ReadString(); // name
            ReadPropertyMap(ref reader);
        }
        else
        {
            // Partial: Optional<String>(name) + Optional<UUID>(id) + PropertyMap
            if (reader.ReadBoolean()) reader.ReadString(); // optional name
            if (reader.ReadBoolean()) reader.ReadUuid(); // optional UUID
            ReadPropertyMap(ref reader);
        }
        // SkinPatch: 3×Optional<ResourceTexture>(String) + Optional<PlayerModelType>(Bool→Bool)
        ReadSkinPatch(ref reader);
        return null;
    }

    /// <summary>
    /// Reads BannerPatterns: list&lt;Layer&gt;, each = VarInt(patternHolder) + VarInt(dyeColor).
    /// Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/world/level/block/entity/BannerPatternLayers.java
    /// </summary>
    private static object? ReadBannerPatterns(ref PacketBufferReader reader)
    {
        var count = reader.ReadVarInt();
        for (var i = 0; i < count; i++)
        {
            reader.ReadVarInt(); // pattern holder
            reader.ReadVarInt(); // dye color
        }
        return null;
    }

    /// <summary>
    /// Reads PotDecorations: VarInt(count, always 4) + count × Optional&lt;VarInt&gt;(item holder).
    /// Reference: minecraft-26.1.1-REFERENCE-ONLY PotDecorations — holderRegistry.optional.list(4)
    /// </summary>
    private static object? ReadPotDecorations(ref PacketBufferReader reader)
    {
        var count = reader.ReadVarInt(); // should be 4
        for (var i = 0; i < count; i++)
        {
            if (reader.ReadBoolean()) reader.ReadVarInt(); // optional item holder
        }
        return null;
    }

    /// <summary>
    /// Reads Bees: list&lt;Occupant&gt;, each = NbtTag(entityData) + VarInt(ticksInHive) + VarInt(minTicksInHive).
    /// Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/world/level/block/entity/BeehiveBlockEntity.java
    /// </summary>
    private static object? ReadBees(ref PacketBufferReader reader)
    {
        var count = reader.ReadVarInt();
        for (var i = 0; i < count; i++)
        {
            reader.ReadNbtTag(); // entityData (CustomData)
            reader.ReadVarInt(); // ticksInHive
            reader.ReadVarInt(); // minTicksInHive
        }
        return null;
    }

    /// <summary>
    /// Reads BreakSound: Holder&lt;SoundEvent&gt;.
    /// Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/core/component/DataComponents.java
    /// </summary>
    private static object? ReadBreakSound(ref PacketBufferReader reader)
    {
        ReadHolderSoundEvent(ref reader);
        return null;
    }

    #endregion

    /// <summary>
    /// Fallback for unknown component types. Throws immediately to trigger the
    /// per-slot error handler — silent buffer corruption is worse than a clean skip.
    /// </summary>
    private static object? ReadUnknownComponent(ref PacketBufferReader reader, int typeId)
    {
        throw new NotSupportedException(
            $"Unknown component type {typeId} (0x{typeId:X2}). " +
            "Cannot determine wire format — add explicit handling in ReadComponentData.");
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
