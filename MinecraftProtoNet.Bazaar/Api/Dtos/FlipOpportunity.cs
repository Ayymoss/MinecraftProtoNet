using MinecraftProtoNet.Bazaar.Api.Enums;

namespace MinecraftProtoNet.Bazaar.Api.Dtos;

/// <summary>
/// Mirrors BazaarCompanion /api/bot/flips response shape.
/// </summary>
public sealed record FlipOpportunity(
    string ProductKey,
    string Name,
    ItemTier Tier,
    bool Unstackable,
    double BestBidPrice,
    int BidOrders,
    int BidVolume,
    double BidWeekVolume,
    double BestAskPrice,
    int AskOrders,
    int AskVolume,
    double AskWeekVolume,
    double Spread,
    double SpreadPercent,
    double ProfitMultiplier,
    double OpportunityScore,
    double EstimatedProfitPerUnit,
    bool IsManipulated,
    double ManipulationIntensity,
    double PriceDeviationPercent
);
