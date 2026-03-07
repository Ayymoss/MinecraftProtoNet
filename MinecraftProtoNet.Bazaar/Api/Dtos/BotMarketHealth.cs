namespace MinecraftProtoNet.Bazaar.Api.Dtos;

/// <summary>
/// Mirrors BazaarCompanion /api/bot/market/health response shape.
/// </summary>
public sealed record BotMarketHealth(
    double HealthScore,
    double AverageSpread,
    double ManipulationIndex,
    int ActiveProductsCount,
    double TotalMarketCap,
    double Volume24h,
    double Volume7d,
    string Recommendation,
    string RecommendationReason
);
