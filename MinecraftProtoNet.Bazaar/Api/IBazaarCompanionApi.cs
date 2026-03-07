using MinecraftProtoNet.Bazaar.Api.Dtos;
using Refit;

namespace MinecraftProtoNet.Bazaar.Api;

/// <summary>
/// Refit interface for BazaarCompanion bot API endpoints.
/// </summary>
public interface IBazaarCompanionApi
{
    /// <summary>
    /// Get top flip opportunities sorted by opportunity score.
    /// </summary>
    [Get("/api/bot/flips")]
    Task<List<FlipOpportunity>> GetFlipOpportunitiesAsync(
        [AliasAs("minPrice")] double? minPrice = null,
        [AliasAs("maxPrice")] double? maxPrice = null,
        [AliasAs("minVolume")] double? minVolume = null,
        [AliasAs("minAskVolume")] double? minAskVolume = null,
        [AliasAs("excludeManipulated")] bool? excludeManipulated = true,
        [AliasAs("minScore")] double? minScore = null,
        [AliasAs("maxResults")] int? maxResults = null,
        CancellationToken ct = default);

    /// <summary>
    /// Get full product detail with order books and price history.
    /// </summary>
    [Get("/api/bot/products/{productKey}")]
    Task<BotProductDetail> GetProductDetailAsync(
        string productKey,
        CancellationToken ct = default);

    /// <summary>
    /// Batch product lookup (lightweight, no order books/history).
    /// </summary>
    [Get("/api/bot/products/batch")]
    Task<List<BotProductSummary>> GetProductsBatchAsync(
        [AliasAs("keys")] string keys,
        CancellationToken ct = default);

    /// <summary>
    /// Get order book analysis for a product.
    /// </summary>
    [Get("/api/bot/products/{productKey}/orderbook")]
    Task<ApiResponse<object>> GetOrderBookAnalysisAsync(
        string productKey,
        CancellationToken ct = default);

    /// <summary>
    /// Get OHLC candle history for a product.
    /// </summary>
    [Get("/api/bot/products/{productKey}/candles")]
    Task<List<CandleData>> GetCandlesAsync(
        string productKey,
        [AliasAs("interval")] int? interval = null,
        [AliasAs("limit")] int? limit = null,
        [AliasAs("before")] long? before = null,
        CancellationToken ct = default);

    /// <summary>
    /// Get market health score with trading recommendation.
    /// </summary>
    [Get("/api/bot/market/health")]
    Task<BotMarketHealth> GetMarketHealthAsync(
        CancellationToken ct = default);
}
