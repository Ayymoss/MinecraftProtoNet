using FluentAssertions;
using MinecraftProtoNet.Bazaar.Trading;
using Xunit;

namespace MinecraftProtoNet.Tests.Bazaar;

/// <summary>
/// The scorecard decides where real capital goes, and its whole purpose is to stop trading something that
/// USED to be good. Both halves need proving: that a proven earner stays favoured while it keeps earning, and
/// that its record stops counting once it goes quiet.
/// </summary>
public class ProductScorecardTests
{
    private static readonly DateTime T0 = new(2026, 8, 7, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void UntradedProduct_ScoresNeutral_SoNewThingsGetTried()
    {
        var card = new ProductScorecard();

        card.ScoreOf("NEVER_TRADED", T0).Should().Be(0);
        card.IsBenched("NEVER_TRADED", T0).Should().BeFalse();
    }

    [Fact]
    public void AProfitableFlip_ProducesAProfitPerHourScore()
    {
        var card = new ProductScorecard();

        // 84,000 coins earned over two hours.
        card.RecordOutcome("SHARD_GROUNDHOG", "Groundhog Shard", 84_000, TimeSpan.FromHours(2), T0);

        card.ScoreOf("SHARD_GROUNDHOG", T0).Should().BeApproximately(42_000, 1);
    }

    /// <summary>Capital is the scarce resource: the same profit earned faster is worth more.</summary>
    [Fact]
    public void FasterFlips_ScoreHigherForTheSameProfit()
    {
        var card = new ProductScorecard();
        card.RecordOutcome("FAST", "Fast", 500, TimeSpan.FromMinutes(10), T0);
        card.RecordOutcome("SLOW", "Slow", 500, TimeSpan.FromHours(1), T0);

        card.ScoreOf("FAST", T0).Should().BeGreaterThan(card.ScoreOf("SLOW", T0));
    }

    /// <summary>
    /// The behaviour the whole class exists for: a product that was excellent and then went quiet must stop
    /// being favoured, without anyone having to notice or intervene.
    /// </summary>
    [Fact]
    public void AGoodRecord_FadesWithTime()
    {
        var card = new ProductScorecard();
        card.RecordOutcome("SHARD_GROUNDHOG", "Groundhog Shard", 60_000, TimeSpan.FromHours(1), T0);

        var fresh = card.ScoreOf("SHARD_GROUNDHOG", T0);
        var oneHalfLife = card.ScoreOf("SHARD_GROUNDHOG", T0.AddHours(ProductScorecard.HalfLifeHours));
        var aDayLater = card.ScoreOf("SHARD_GROUNDHOG", T0.AddHours(24));

        oneHalfLife.Should().BeApproximately(fresh / 2, fresh * 0.01);
        aDayLater.Should().BeLessThan(fresh * 0.07);
    }

    /// <summary>
    /// "Great on day one, two coins a trade on day two" — the case described. The recent bad result has to
    /// dominate the old good one, not be averaged with it.
    /// </summary>
    [Fact]
    public void ARecentCollapse_OutweighsAnOldSuccess()
    {
        var card = new ProductScorecard();
        card.RecordOutcome("FADED", "Faded Thing", 100_000, TimeSpan.FromHours(1), T0);

        var dayTwo = T0.AddHours(24);
        card.RecordOutcome("FADED", "Faded Thing", 2, TimeSpan.FromHours(1), dayTwo);

        // The claim that matters is that yesterday's number stops driving the decision — better than a 99%
        // reduction — not that it reaches any particular figure.
        card.ScoreOf("FADED", dayTwo).Should().BeLessThan(1_000,
            "a day-old prior must be nearly worthless next to what the product earns today");
    }

    /// <summary>One poor flip is noise. Benching on a single sample would discard merely unlucky products.</summary>
    [Fact]
    public void ASingleBadFlip_DoesNotBenchAProduct()
    {
        var card = new ProductScorecard();
        card.RecordOutcome("UNLUCKY", "Unlucky", 5, TimeSpan.FromHours(1), T0);

        card.IsBenched("UNLUCKY", T0).Should().BeFalse();
    }

    [Fact]
    public void RepeatedlyPoorProducts_AreBenched()
    {
        var card = new ProductScorecard();
        card.RecordOutcome("DUD", "Dud", 3, TimeSpan.FromHours(1), T0);
        card.RecordOutcome("DUD", "Dud", 2, TimeSpan.FromHours(1), T0.AddMinutes(30));

        card.IsBenched("DUD", T0.AddMinutes(30)).Should().BeTrue();
    }

    /// <summary>
    /// A bench must expire on its own. Bazaar markets are cyclical, and a score that has decayed to nothing
    /// is still below the bench floor — so without an expiry a product condemned on one bad afternoon could
    /// never be picked again, and the bot could never discover it had recovered.
    /// </summary>
    [Fact]
    public void ABenchHeals_SoACyclicalMarketGetsAnotherChance()
    {
        var card = new ProductScorecard();
        card.RecordOutcome("DUD", "Dud", 3, TimeSpan.FromHours(1), T0);
        card.RecordOutcome("DUD", "Dud", 2, TimeSpan.FromHours(1), T0.AddMinutes(30));

        var benchedAt = T0.AddMinutes(30);
        card.IsBenched("DUD", benchedAt).Should().BeTrue();

        // Still benched a few hours later — the verdict is recent, so it stands.
        card.IsBenched("DUD", benchedAt.AddHours(6)).Should().BeTrue();

        // Once the evidence is a day old it no longer justifies exclusion, with no new trade required.
        card.IsBenched("DUD", benchedAt.AddHours(ProductScorecard.BenchHealHours)).Should().BeFalse();
    }

    /// <summary>A healed product returns as UNKNOWN, not as bad — otherwise it would never out-rank anything.</summary>
    [Fact]
    public void AHealedProduct_ReturnsOnEqualFootingWithSomethingNeverTried()
    {
        var card = new ProductScorecard();
        card.RecordOutcome("DUD", "Dud", 3, TimeSpan.FromHours(1), T0);
        card.RecordOutcome("DUD", "Dud", 2, TimeSpan.FromHours(1), T0.AddMinutes(30));

        var later = T0.AddHours(72);

        card.IsBenched("DUD", later).Should().BeFalse();
        card.ScoreOf("DUD", later).Should().BeApproximately(0, 1,
            "a decayed record is indistinguishable from no record, which is what lets it be tried again");
    }

    /// <summary>A fresh good result should also lift a bench immediately, without waiting for the expiry.</summary>
    [Fact]
    public void AFreshGoodResult_LiftsABenchImmediately()
    {
        var card = new ProductScorecard();
        card.RecordOutcome("DUD", "Dud", 3, TimeSpan.FromHours(1), T0);
        card.RecordOutcome("DUD", "Dud", 2, TimeSpan.FromHours(1), T0.AddMinutes(30));

        card.RecordOutcome("DUD", "Dud", 40_000, TimeSpan.FromHours(1), T0.AddHours(48));

        card.IsBenched("DUD", T0.AddHours(48)).Should().BeFalse();
    }

    /// <summary>A loss must register as a loss, not be clamped away.</summary>
    [Fact]
    public void ALosingFlip_ScoresNegative()
    {
        var card = new ProductScorecard();
        card.RecordOutcome("LOSER", "Loser", -5_000, TimeSpan.FromHours(1), T0);

        card.ScoreOf("LOSER", T0).Should().BeLessThan(0);
    }

    [Fact]
    public void Top_RanksByCurrentDecayedScore()
    {
        var card = new ProductScorecard();
        card.RecordOutcome("OLD_GOOD", "Old Good", 100_000, TimeSpan.FromHours(1), T0);
        card.RecordOutcome("NEW_OK", "New OK", 20_000, TimeSpan.FromHours(1), T0.AddHours(23));

        var top = card.Top(T0.AddHours(24), 2).ToList();

        top[0].Name.Should().Be("New OK", "yesterday's winner has decayed below today's steady earner");
    }
}
