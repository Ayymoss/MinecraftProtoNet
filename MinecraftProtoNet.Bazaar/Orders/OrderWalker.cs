using MinecraftProtoNet.Bazaar.Api;
using MinecraftProtoNet.Bazaar.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MinecraftProtoNet.Bazaar.Orders;

/// <summary>
/// Evaluates whether active orders should be "walked" (price adjusted)
/// to stay competitive, while ensuring profitability is maintained.
/// </summary>
public sealed class OrderWalker(
    IBazaarCompanionApi api,
    IOptions<BazaarTradingConfig> config,
    ILogger<OrderWalker> logger)
{
    private readonly BazaarTradingConfig _config = config.Value;

    /// <summary>
    /// Evaluates whether an order should be walked to a new price.
    /// </summary>
    public async Task<WalkDecision> EvaluateAsync(OrderRecord order, CancellationToken ct = default)
    {
        try
        {
            var product = await api.GetProductDetailAsync(order.ProductKey, ct);

            if (order.Side == OrderSide.Buy)
                return EvaluateBuyWalk(order, product.BidPrice, product.AskPrice);

            return EvaluateSellWalk(order, product.BidPrice, product.AskPrice);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to evaluate walk for order {OrderId}", order.Id);
            return WalkDecision.NoWalk("Failed to fetch market data");
        }
    }

    private WalkDecision EvaluateBuyWalk(OrderRecord order, double currentBestBid, double currentBestAsk)
    {
        // If our price is still the best bid, no walk needed
        if (order.PricePerUnit >= currentBestBid)
            return WalkDecision.NoWalk("Still best bid");

        // Someone bid higher — calculate new price (penny above current best)
        var newPrice = currentBestBid + 0.1;

        // Check profitability at new price
        var profit = (currentBestAsk * (1 - _config.TaxRate)) - newPrice;
        if (profit < _config.MinProfitPerUnit)
            return WalkDecision.CancelOrder($"Walk price {newPrice:F1} not profitable (profit {profit:F1} < min {_config.MinProfitPerUnit})");

        // Check walk count
        if (order.WalkCount >= _config.MaxWalksPerOrder)
            return WalkDecision.CancelOrder($"Max walks ({_config.MaxWalksPerOrder}) reached");

        // Check slippage
        var slippage = Math.Abs(newPrice - order.OriginalPrice) / order.OriginalPrice * 100;
        if (slippage > _config.MaxWalkSlippagePercent)
            return WalkDecision.CancelOrder($"Walk slippage {slippage:F1}% exceeds max {_config.MaxWalkSlippagePercent}%");

        return WalkDecision.Walk(newPrice,
            $"Undercut detected (best bid {currentBestBid:F1}), walking to {newPrice:F1}");
    }

    private WalkDecision EvaluateSellWalk(OrderRecord order, double currentBestBid, double currentBestAsk)
    {
        // If our price is still the best ask, no walk needed
        if (order.PricePerUnit <= currentBestAsk)
            return WalkDecision.NoWalk("Still best ask");

        // Someone undercut — calculate new price (penny below current best)
        var newPrice = currentBestAsk - 0.1;

        // Check profitability at new price (we already bought, so check against our buy cost)
        var revenue = newPrice * (1 - _config.TaxRate);
        // We don't know the original buy price here — the engine should validate separately

        // Check walk count
        if (order.WalkCount >= _config.MaxWalksPerOrder)
            return WalkDecision.CancelOrder($"Max walks ({_config.MaxWalksPerOrder}) reached");

        // Check slippage
        var slippage = Math.Abs(newPrice - order.OriginalPrice) / order.OriginalPrice * 100;
        if (slippage > _config.MaxWalkSlippagePercent)
            return WalkDecision.CancelOrder($"Walk slippage {slippage:F1}% exceeds max {_config.MaxWalkSlippagePercent}%");

        return WalkDecision.Walk(newPrice,
            $"Undercut detected (best ask {currentBestAsk:F1}), walking to {newPrice:F1}");
    }
}

/// <summary>
/// Result of evaluating whether to walk an order.
/// </summary>
public sealed record WalkDecision(
    WalkAction Action,
    double? NewPrice,
    string Reason)
{
    public static WalkDecision NoWalk(string reason) => new(WalkAction.None, null, reason);
    public static WalkDecision Walk(double newPrice, string reason) => new(WalkAction.Walk, newPrice, reason);
    public static WalkDecision CancelOrder(string reason) => new(WalkAction.Cancel, null, reason);
}

public enum WalkAction
{
    None,
    Walk,
    Cancel
}
