using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MinecraftProtoNet.Bazaar.Api.Dtos;
using MinecraftProtoNet.Bazaar.Api.Enums;
using MinecraftProtoNet.Bazaar.Configuration;
using MinecraftProtoNet.Bazaar.Engine;
using MinecraftProtoNet.Bazaar.Safety;
using Moq;

namespace MinecraftProtoNet.Tests.Bazaar;

public class TradingSafetyGuardTests
{
    private readonly TradingSafetyGuard _guard;
    private readonly BazaarTradingConfig _config;

    public TradingSafetyGuardTests()
    {
        _config = new BazaarTradingConfig
        {
            MaxLossBeforeHalt = 500_000,
            MaxConsecutiveFailures = 5,
            MinMarketHealthScore = 25,
            ManipulationIntensityThreshold = 0.7,
            MinProfitPerUnit = 50,
            MinProfitPercent = 1.0,
            TaxRate = 0.01125
        };

        _guard = new TradingSafetyGuard(
            Options.Create(_config),
            Mock.Of<ILogger<TradingSafetyGuard>>());
    }

    [Fact]
    public void CheckSafety_HealthyState_ReturnsNull()
    {
        var state = new TradingState { CoinBalance = 1_000_000 };
        var health = new BotMarketHealth(75, 100, 0.2, 500, 1e9, 1e6, 5e6, "Normal", "OK");

        _guard.CheckSafety(state, health).Should().BeNull();
    }

    [Fact]
    public void CheckSafety_MaxLossExceeded_ReturnsReason()
    {
        var state = new TradingState { CoinBalance = 100 };
        // Simulate losses by recording negative trades
        state.RecordCompletedTrade(new TradeRecord("TEST", "Test", 1, 600_000, 50_000, 0.01125, DateTime.UtcNow));

        _guard.CheckSafety(state, null).Should().Contain("Max loss exceeded");
    }

    [Fact]
    public void CheckSafety_ConsecutiveFailures_ReturnsReason()
    {
        var state = new TradingState { ConsecutiveFailures = 5 };

        _guard.CheckSafety(state, null).Should().Contain("Consecutive failures");
    }

    [Fact]
    public void CheckSafety_LowMarketHealth_ReturnsReason()
    {
        var state = new TradingState();
        var health = new BotMarketHealth(10, 50, 0.8, 100, 1e8, 1e5, 5e5, "HaltTrading", "Bad");

        _guard.CheckSafety(state, health).Should().Contain("Market health too low");
    }

    [Fact]
    public void IsOpportunitySafe_ManipulatedProduct_ReturnsFalse()
    {
        var opp = CreateOpportunity(isManipulated: true);
        _guard.IsOpportunitySafe(opp).Should().BeFalse();
    }

    [Fact]
    public void IsOpportunitySafe_HighManipulationIntensity_ReturnsFalse()
    {
        var opp = CreateOpportunity(manipulationIntensity: 0.9);
        _guard.IsOpportunitySafe(opp).Should().BeFalse();
    }

    [Fact]
    public void IsOpportunitySafe_CleanProduct_ReturnsTrue()
    {
        var opp = CreateOpportunity();
        _guard.IsOpportunitySafe(opp).Should().BeTrue();
    }

    [Fact]
    public void IsProfitable_GoodSpread_ReturnsTrue()
    {
        // Buy at 1000, sell at 1200 → profit = 1200 * 0.98875 - 1000 = 186.5
        _guard.IsProfitable(1000, 1200).Should().BeTrue();
    }

    [Fact]
    public void IsProfitable_TightSpread_ReturnsFalse()
    {
        // Buy at 1000, sell at 1010 → profit = 1010 * 0.98875 - 1000 = -1.37
        _guard.IsProfitable(1000, 1010).Should().BeFalse();
    }

    [Fact]
    public void IsProfitable_ExactlyAtMinimum_ReturnsTrue()
    {
        // Need profit >= 50 per unit and >= 1%
        // 50 / (1 - 0.01125) = 50.57 spread needed at any price
        // At buy=5000: sell must be >= (5000 + 50) / (1 - 0.01125) = 5107.4
        _guard.IsProfitable(5000, 5110).Should().BeTrue();
    }

    private static FlipOpportunity CreateOpportunity(
        bool isManipulated = false,
        double manipulationIntensity = 0.1) =>
        new("TEST", "Test Item", ItemTier.Common, false,
            1000, 10, 100, 50000, 1200, 10, 100, 50000,
            200, 20, 1.2, 5.0, 180, isManipulated, manipulationIntensity, 5);
}
