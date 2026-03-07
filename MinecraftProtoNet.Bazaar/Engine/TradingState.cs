namespace MinecraftProtoNet.Bazaar.Engine;

/// <summary>
/// Tracks the financial state of the trading engine: capital, P&L, trade history.
/// </summary>
public sealed class TradingState
{
    private readonly object _lock = new();
    private readonly List<TradeRecord> _completedTrades = [];

    /// <summary>Current coin balance (manually set initially, updated from claims).</summary>
    public double CoinBalance { get; set; }

    /// <summary>Total coins invested in active orders.</summary>
    public double InvestedCapital { get; private set; }

    /// <summary>Running total realized profit (can be negative).</summary>
    public double RealizedPnL { get; private set; }

    /// <summary>Number of completed round-trip trades.</summary>
    public int CompletedTradeCount => _completedTrades.Count;

    /// <summary>Number of consecutive failures (resets on success).</summary>
    public int ConsecutiveFailures { get; set; }

    /// <summary>Completed trade history (most recent first).</summary>
    public IReadOnlyList<TradeRecord> CompletedTrades
    {
        get
        {
            lock (_lock) return _completedTrades.ToList();
        }
    }

    /// <summary>Records capital invested when placing a buy order.</summary>
    public void RecordInvestment(double amount)
    {
        lock (_lock)
        {
            InvestedCapital += amount;
            CoinBalance -= amount;
        }
    }

    /// <summary>Records capital returned when cancelling an order.</summary>
    public void RecordRefund(double amount)
    {
        lock (_lock)
        {
            InvestedCapital -= amount;
            CoinBalance += amount;
        }
    }

    /// <summary>Records a completed round-trip trade (buy + sell).</summary>
    public void RecordCompletedTrade(TradeRecord trade)
    {
        lock (_lock)
        {
            _completedTrades.Insert(0, trade);
            RealizedPnL += trade.Profit;
            InvestedCapital -= trade.BuyCost;
            CoinBalance += trade.SellRevenue;
            ConsecutiveFailures = 0;
        }
    }

    /// <summary>Records coins claimed from a sell offer fill.</summary>
    public void RecordCoinsClaimed(double amount)
    {
        lock (_lock)
        {
            CoinBalance += amount;
        }
    }
}

/// <summary>
/// Record of a completed round-trip trade (buy then sell).
/// </summary>
public sealed record TradeRecord(
    string ProductKey,
    string ProductName,
    int Quantity,
    double BuyPricePerUnit,
    double SellPricePerUnit,
    double TaxRate,
    DateTime CompletedAt)
{
    public double BuyCost => BuyPricePerUnit * Quantity;
    public double SellRevenue => SellPricePerUnit * Quantity * (1 - TaxRate);
    public double Profit => SellRevenue - BuyCost;
    public double ProfitPercent => BuyCost > 0 ? Profit / BuyCost * 100 : 0;
}
