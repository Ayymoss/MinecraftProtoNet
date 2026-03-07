namespace MinecraftProtoNet.Bazaar.Api.Dtos;

/// <summary>
/// Mirrors BazaarCompanion /api/bot/products/batch response shape.
/// </summary>
public sealed record BotProductSummary(
    string ProductKey,
    string Name,
    double BidPrice,
    double AskPrice,
    double Spread,
    double OpportunityScore,
    bool IsManipulated,
    double BidWeekVolume,
    double AskWeekVolume
);
