namespace MinecraftProtoNet.Bazaar.Api.Dtos;

/// <summary>
/// Mirrors BazaarCompanion /api/bot/products/{key}/candles response shape.
/// </summary>
public sealed record CandleData(
    long Timestamp,
    double Open,
    double High,
    double Low,
    double Close,
    double Volume,
    double Spread,
    double AskClose
);
