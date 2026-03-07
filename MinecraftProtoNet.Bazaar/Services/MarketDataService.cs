using MinecraftProtoNet.Bazaar.Api;
using MinecraftProtoNet.Bazaar.Api.Dtos;
using MinecraftProtoNet.Bazaar.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MinecraftProtoNet.Bazaar.Services;

/// <summary>
/// Polls BazaarCompanion API for flip opportunities and market health.
/// Caches results to avoid excessive API calls.
/// </summary>
public sealed class MarketDataService(
    IBazaarCompanionApi api,
    IOptions<BazaarTradingConfig> config,
    ILogger<MarketDataService> logger)
{
    private readonly BazaarTradingConfig _config = config.Value;

    private List<FlipOpportunity>? _cachedOpportunities;
    private DateTime _opportunitiesCachedAt;

    private BotMarketHealth? _cachedHealth;
    private DateTime _healthCachedAt;

    /// <summary>
    /// Gets flip opportunities, using cache if fresh enough.
    /// </summary>
    public async Task<List<FlipOpportunity>> GetOpportunitiesAsync(CancellationToken ct = default)
    {
        if (_cachedOpportunities is not null &&
            DateTime.UtcNow - _opportunitiesCachedAt < _config.FlipPollInterval)
        {
            return _cachedOpportunities;
        }

        try
        {
            _cachedOpportunities = await api.GetFlipOpportunitiesAsync(
                minPrice: _config.MinBidPrice,
                maxPrice: _config.MaxBidPrice,
                minAskVolume: _config.MinWeeklyAskVolume,
                excludeManipulated: true,
                minScore: _config.MinOpportunityScore,
                maxResults: 20,
                ct: ct);
            _opportunitiesCachedAt = DateTime.UtcNow;

            logger.LogDebug("Fetched {Count} flip opportunities", _cachedOpportunities.Count);
            return _cachedOpportunities;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch flip opportunities");
            return _cachedOpportunities ?? [];
        }
    }

    /// <summary>
    /// Gets market health, using cache if fresh enough.
    /// </summary>
    public async Task<BotMarketHealth?> GetMarketHealthAsync(CancellationToken ct = default)
    {
        if (_cachedHealth is not null &&
            DateTime.UtcNow - _healthCachedAt < _config.MarketHealthPollInterval)
        {
            return _cachedHealth;
        }

        try
        {
            _cachedHealth = await api.GetMarketHealthAsync(ct);
            _healthCachedAt = DateTime.UtcNow;

            logger.LogDebug("Market health: {Score:F0} ({Recommendation})",
                _cachedHealth.HealthScore, _cachedHealth.Recommendation);
            return _cachedHealth;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch market health");
            return _cachedHealth;
        }
    }

    /// <summary>
    /// Gets current product detail (bypasses cache for real-time price checks).
    /// </summary>
    public async Task<BotProductDetail?> GetProductDetailAsync(string productKey, CancellationToken ct = default)
    {
        try
        {
            return await api.GetProductDetailAsync(productKey, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch product detail for {ProductKey}", productKey);
            return null;
        }
    }

    /// <summary>Invalidates cached opportunities, forcing a fresh fetch on next call.</summary>
    public void InvalidateOpportunities() => _opportunitiesCachedAt = DateTime.MinValue;

    /// <summary>Invalidates cached health, forcing a fresh fetch on next call.</summary>
    public void InvalidateHealth() => _healthCachedAt = DateTime.MinValue;
}
