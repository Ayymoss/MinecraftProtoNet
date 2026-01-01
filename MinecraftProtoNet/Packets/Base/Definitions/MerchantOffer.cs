using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Base.Definitions;

/// <summary>
/// Represents a villager/merchant trade offer.
/// Based on MerchantOffer.java from Minecraft source.
/// </summary>
public class MerchantOffer
{
    /// <summary>Primary input item cost.</summary>
    public required ItemCost BaseCostA { get; set; }
    
    /// <summary>Optional secondary input item cost.</summary>
    public ItemCost? CostB { get; set; }
    
    /// <summary>Result item from the trade.</summary>
    public required Slot Result { get; set; }
    
    /// <summary>Number of times this trade has been used.</summary>
    public int Uses { get; set; }
    
    /// <summary>Maximum uses before trade is exhausted.</summary>
    public int MaxUses { get; set; }
    
    /// <summary>XP reward for the player when trading.</summary>
    public int Xp { get; set; }
    
    /// <summary>Special price modifier (from reputation, hero of village, etc.).</summary>
    public int SpecialPriceDiff { get; set; }
    
    /// <summary>Price multiplier for demand-based pricing.</summary>
    public float PriceMultiplier { get; set; }
    
    /// <summary>Current demand level affecting price.</summary>
    public int Demand { get; set; }

    /// <summary>Whether this trade is exhausted (uses >= maxUses).</summary>
    public bool IsOutOfStock => Uses >= MaxUses;

    /// <summary>
    /// Calculates the actual cost count considering demand and special pricing.
    /// </summary>
    public int GetModifiedCostCount()
    {
        var basePrice = BaseCostA.Count;
        var demandDiff = Math.Max(0, (int)Math.Floor(basePrice * Demand * PriceMultiplier));
        return Math.Clamp(basePrice + demandDiff + SpecialPriceDiff, 1, 64);
    }

    /// <summary>
    /// Reads a MerchantOffer from the packet buffer.
    /// </summary>
    public static MerchantOffer Read(ref PacketBufferReader reader)
    {
        var baseCostA = ItemCost.Read(ref reader);
        var result = Slot.Read(ref reader);
        var costB = ItemCost.ReadOptional(ref reader);
        var isOutOfStock = reader.ReadBoolean();
        var uses = reader.ReadSignedInt();
        var maxUses = reader.ReadSignedInt();
        var xp = reader.ReadSignedInt();
        var specialPriceDiff = reader.ReadSignedInt();
        var priceMultiplier = reader.ReadFloat();
        var demand = reader.ReadSignedInt();


        var offer = new MerchantOffer
        {
            BaseCostA = baseCostA,
            Result = result,
            CostB = costB,
            Uses = uses,
            MaxUses = maxUses,
            Xp = xp,
            SpecialPriceDiff = specialPriceDiff,
            PriceMultiplier = priceMultiplier,
            Demand = demand
        };

        // If marked as out of stock, set uses to max
        if (isOutOfStock)
        {
            offer.Uses = offer.MaxUses;
        }

        return offer;
    }

    /// <summary>
    /// Reads a list of MerchantOffers from the packet buffer.
    /// </summary>
    public static List<MerchantOffer> ReadList(ref PacketBufferReader reader)
    {
        var count = reader.ReadVarInt();
        var offers = new List<MerchantOffer>(count);
        for (var i = 0; i < count; i++)
        {
            offers.Add(Read(ref reader));
        }
        return offers;
    }

    public override string ToString()
    {
        var costStr = CostB != null 
            ? $"{BaseCostA.Count}x#{BaseCostA.ItemId} + {CostB.Count}x#{CostB.ItemId}"
            : $"{BaseCostA.Count}x#{BaseCostA.ItemId}";
        return $"[Trade: {costStr} -> {Result.ItemCount}x#{Result.ItemId} ({Uses}/{MaxUses})]";
    }
}
