using MinecraftProtoNet.Bazaar.Api.Dtos;
using MinecraftProtoNet.Bazaar.Configuration;
using MinecraftProtoNet.Bazaar.Engine;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MinecraftProtoNet.Bazaar.Safety;

/// <summary>
/// Circuit breakers for the trading engine: max loss, market health, consecutive failures.
/// </summary>
public sealed class TradingSafetyGuard(
    IOptions<BazaarTradingConfig> config,
    ILogger<TradingSafetyGuard> logger)
{
    private readonly BazaarTradingConfig _config = config.Value;

    /// <summary>
    /// Checks all safety conditions. Returns null if safe, or a halt reason string if not.
    /// </summary>
    public string? CheckSafety(TradingState tradingState, BotMarketHealth? marketHealth)
    {
        // Max loss check
        if (tradingState.RealizedPnL < -_config.MaxLossBeforeHalt)
        {
            var reason = $"Max loss exceeded: {tradingState.RealizedPnL:F0} < -{_config.MaxLossBeforeHalt:F0}";
            logger.LogError("SAFETY HALT: {Reason}", reason);
            return reason;
        }

        // Consecutive failures
        if (tradingState.ConsecutiveFailures >= _config.MaxConsecutiveFailures)
        {
            var reason = $"Consecutive failures: {tradingState.ConsecutiveFailures} >= {_config.MaxConsecutiveFailures}";
            logger.LogError("SAFETY HALT: {Reason}", reason);
            return reason;
        }

        // Market health
        if (marketHealth is not null && marketHealth.HealthScore < _config.MinMarketHealthScore)
        {
            var reason = $"Market health too low: {marketHealth.HealthScore:F0} < {_config.MinMarketHealthScore:F0} ({marketHealth.Recommendation})";
            logger.LogWarning("SAFETY HALT: {Reason}", reason);
            return reason;
        }

        return null;
    }

    /// <summary>
    /// Checks if a specific flip opportunity is safe to trade.
    /// </summary>
    public bool IsOpportunitySafe(FlipOpportunity opportunity)
    {
        if (opportunity.IsManipulated)
        {
            logger.LogDebug("Skipping {Product}: manipulated", opportunity.ProductKey);
            return false;
        }

        if (opportunity.ManipulationIntensity > _config.ManipulationIntensityThreshold)
        {
            logger.LogDebug("Skipping {Product}: manipulation intensity {Intensity:F2} > {Threshold:F2}",
                opportunity.ProductKey, opportunity.ManipulationIntensity, _config.ManipulationIntensityThreshold);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Validates that a trade at the given price is profitable after tax.
    /// </summary>
    public bool IsProfitable(double buyPrice, double sellPrice)
    {
        var profitPerUnit = (sellPrice * (1 - _config.TaxRate)) - buyPrice;
        if (profitPerUnit < _config.MinProfitPerUnit)
            return false;

        var profitPercent = buyPrice > 0 ? profitPerUnit / buyPrice * 100 : 0;
        return profitPercent >= _config.MinProfitPercent;
    }
}
