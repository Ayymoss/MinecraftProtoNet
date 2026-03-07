using Microsoft.Extensions.Logging;

namespace MinecraftProtoNet.Bazaar.Services;

/// <summary>
/// Tracks the bot's coin balance. Initially uses manual balance + chat-inferred adjustments.
/// Will be enhanced with scoreboard parsing in Phase 6.
/// </summary>
public sealed class CoinTracker(ILogger<CoinTracker> logger)
{
    private double _balance;
    private readonly object _lock = new();

    /// <summary>Current estimated coin balance.</summary>
    public double Balance
    {
        get { lock (_lock) return _balance; }
    }

    /// <summary>Sets the balance manually (e.g., from initial configuration or user command).</summary>
    public void SetBalance(double balance)
    {
        lock (_lock)
        {
            _balance = balance;
            logger.LogInformation("Coin balance set to {Balance:N0}", balance);
        }
    }

    /// <summary>Adjusts balance by a delta (positive = coins gained, negative = coins spent).</summary>
    public void Adjust(double delta, string reason)
    {
        lock (_lock)
        {
            var oldBalance = _balance;
            _balance += delta;
            logger.LogDebug("Coin balance: {Old:N0} -> {New:N0} ({Delta:+#,##0;-#,##0}) [{Reason}]",
                oldBalance, _balance, delta, reason);
        }
    }
}
