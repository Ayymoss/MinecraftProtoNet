using MinecraftProtoNet.Bazaar.Api.Enums;

namespace MinecraftProtoNet.Bazaar.Api.Dtos;

/// <summary>
/// Mirrors BazaarCompanion /api/bot/products/{key} response shape.
/// </summary>
public sealed record BotProductDetail(
    string ProductKey,
    string Name,
    ItemTier Tier,
    bool Unstackable,
    double BidPrice,
    double AskPrice,
    double Spread,
    double BidWeekVolume,
    double AskWeekVolume,
    double TotalWeekVolume,
    int BidOrders,
    int AskOrders,
    int BidVolume,
    int AskVolume,
    double OpportunityScore,
    double ProfitMultiplier,
    bool IsManipulated,
    double ManipulationIntensity,
    double PriceDeviationPercent,
    List<OrderBookEntry> BidBook,
    List<OrderBookEntry> AskBook,
    List<PriceHistoryEntry> PriceHistory
);

/// <summary>
/// Single order book entry (bid or ask).
/// </summary>
public sealed record OrderBookEntry(
    double PricePerUnit,
    int Amount,
    int Orders
);

/// <summary>
/// Daily price snapshot from BazaarCompanion.
/// </summary>
public sealed record PriceHistoryEntry(
    DateTime Date,
    double Bid,
    double Ask
);
