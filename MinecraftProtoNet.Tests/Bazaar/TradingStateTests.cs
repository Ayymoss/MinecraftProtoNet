using FluentAssertions;
using MinecraftProtoNet.Bazaar.Engine;

namespace MinecraftProtoNet.Tests.Bazaar;

public class TradingStateTests
{
    [Fact]
    public void RecordInvestment_UpdatesBalanceAndInvested()
    {
        var state = new TradingState { CoinBalance = 1_000_000 };
        state.RecordInvestment(100_000);

        state.CoinBalance.Should().Be(900_000);
        state.InvestedCapital.Should().Be(100_000);
    }

    [Fact]
    public void RecordRefund_ReturnsCapital()
    {
        var state = new TradingState { CoinBalance = 900_000 };
        state.RecordInvestment(100_000);
        state.RecordRefund(100_000);

        state.CoinBalance.Should().Be(900_000);
        state.InvestedCapital.Should().Be(0);
    }

    [Fact]
    public void RecordCompletedTrade_UpdatesPnL()
    {
        var state = new TradingState { CoinBalance = 0 };
        state.RecordInvestment(64_000); // Buy 64 @ 1000

        var trade = new TradeRecord("TEST", "Test", 64, 1000, 1200, 0.01125, DateTime.UtcNow);
        state.RecordCompletedTrade(trade);

        state.RealizedPnL.Should().Be(trade.Profit);
        state.CompletedTradeCount.Should().Be(1);
        state.ConsecutiveFailures.Should().Be(0);
    }

    [Fact]
    public void TradeRecord_ProfitCalculation()
    {
        var trade = new TradeRecord("TEST", "Test", 64, 1000, 1200, 0.01125, DateTime.UtcNow);

        trade.BuyCost.Should().Be(64_000);
        trade.SellRevenue.Should().BeApproximately(1200 * 64 * (1 - 0.01125), 0.01);
        trade.Profit.Should().BeApproximately(trade.SellRevenue - trade.BuyCost, 0.01);
        trade.Profit.Should().BeGreaterThan(0);
    }

    [Fact]
    public void TradeRecord_LosingTrade_NegativeProfit()
    {
        // Bought at 1200, sold at 1000 (bad trade)
        var trade = new TradeRecord("TEST", "Test", 10, 1200, 1000, 0.01125, DateTime.UtcNow);

        trade.Profit.Should().BeLessThan(0);
    }
}
