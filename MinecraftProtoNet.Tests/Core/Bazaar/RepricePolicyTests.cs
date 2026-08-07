using FluentAssertions;
using MinecraftProtoNet.Bazaar.Trading;

namespace MinecraftProtoNet.Tests.Core.Bazaar;

/// <summary>
/// The repricing rules, which decide by themselves whether to chase a price.
///
/// The case that matters most is the ladder walk: an attacker posts a tick better than the top, waits for the
/// incumbent to reflexively re-post a tick better than that, and repeats until the incumbent's price is far
/// enough from fair value to fill against the attacker's real order at a loss. It only works on a victim who
/// reacts instantly and unconditionally, so these tests pin the three things that stop it — confirmation,
/// a profit floor measured against entry, and a bounded walk budget — as well as the patience that stops the
/// protections turning into an order that never fills.
/// </summary>
public sealed class RepricePolicyTests
{
    private static RepriceContext SellLeg(
        double ourPrice = 1583.3,
        double bestAsk = 1583.3,
        double costPerUnit = 1300.3,
        int competingDepth = 500,
        int pollsBeaten = 0,
        int stepsTaken = 0,
        TimeSpan? age = null,
        TimeSpan? dataAge = null,
        double entryPrice = 1583.3,
        double bestBid = 1300.5) =>
        new(
            IsBuyLeg: false,
            OurPrice: ourPrice,
            CostPerUnit: costPerUnit,
            BestOnOurSide: bestAsk,
            BestOnOtherSide: bestBid,
            CompetingDepth: competingDepth,
            EntryMarginPerUnit: 265.5,
            EntryPrice: entryPrice,
            PollsBeaten: pollsBeaten,
            StepsTaken: stepsTaken,
            Age: age ?? TimeSpan.FromMinutes(2),
            DataAge: dataAge ?? TimeSpan.FromSeconds(20));

    [Fact]
    public void HoldsWhenStillBestOnOurSide()
    {
        var decision = RepricePolicy.Decide(SellLeg());
        decision.Action.Should().Be(RepriceAction.Hold);
        decision.Reason.Should().Contain("still best");
    }

    [Fact]
    public void DoesNotReactToASingleUndercut()
    {
        // One poll is a flicker — and a bait order pulled straight away never survives to a second look.
        var decision = RepricePolicy.Decide(SellLeg(bestAsk: 1583.2, pollsBeaten: 1));
        decision.Action.Should().Be(RepriceAction.Hold);
        decision.Reason.Should().Contain("waiting for");
    }

    [Fact]
    public void StepsOneTickWhenTheUndercutPersists()
    {
        var decision = RepricePolicy.Decide(SellLeg(bestAsk: 1583.2, pollsBeaten: 2));
        decision.Action.Should().Be(RepriceAction.Step);
        decision.NewPrice.Should().BeApproximately(1583.1, 0.001);
    }

    [Fact]
    public void RefusesToChaseAThinLevelFarAwayAfterSeveralSteps()
    {
        // The ladder signature: a handful of units, well below us, and we have already moved twice for it.
        var decision = RepricePolicy.Decide(SellLeg(
            ourPrice: 1582.0,
            bestAsk: 1580.0,
            competingDepth: 4,
            pollsBeaten: 3,
            stepsTaken: 2));

        decision.Action.Should().Be(RepriceAction.Hold);
        decision.Reason.Should().Contain("ladder");
    }

    [Fact]
    public void DisengagesOnceItHasBecomeAPriceWar()
    {
        var decision = RepricePolicy.Decide(SellLeg(
            ourPrice: 1580.0,
            bestAsk: 1579.9,
            competingDepth: 5000,
            pollsBeaten: 2,
            stepsTaken: RepricePolicy.WarStepThreshold));

        decision.Action.Should().Be(RepriceAction.Hold);
        decision.Reason.Should().Contain("price war");
    }

    [Fact]
    public void NeverStepsBelowTheMarginFloorForItsStage()
    {
        // Ask has collapsed to barely above cost. Early on, that is far under the floor and we sit tight
        // rather than crystallise a bad flip.
        var decision = RepricePolicy.Decide(SellLeg(
            ourPrice: 1583.3,
            bestAsk: 1320.0,
            pollsBeaten: 5,
            age: TimeSpan.FromMinutes(3)));

        decision.Action.Should().Be(RepriceAction.Hold);
        decision.Reason.Should().Contain("floor");
    }

    [Fact]
    public void MarginFloorRelaxesAsThePositionAges()
    {
        // Capital tied up in an unsold position costs more than the last few coins of margin, so the same
        // book that was refused at three minutes is accepted at two hours.
        var early = RepricePolicy.MarginFloorFraction(TimeSpan.FromMinutes(3));
        var working = RepricePolicy.MarginFloorFraction(TimeSpan.FromMinutes(30));
        var clearing = RepricePolicy.MarginFloorFraction(TimeSpan.FromHours(2));

        early.Should().BeGreaterThan(working);
        working.Should().BeGreaterThan(clearing);
        clearing.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void StopsWalkingOncePriceHasDriftedPastTheBudget()
    {
        var decision = RepricePolicy.Decide(SellLeg(
            entryPrice: 1583.3,
            ourPrice: 1500.0,
            bestAsk: 1480.0,
            pollsBeaten: 3,
            age: TimeSpan.FromMinutes(2)));

        decision.Action.Should().Be(RepriceAction.Hold);
        decision.Reason.Should().Contain("walk budget");
    }

    [Fact]
    public void CrossesTheSpreadToClearGoodsHeldTooLong()
    {
        var decision = RepricePolicy.Decide(SellLeg(
            bestAsk: 1583.2,
            pollsBeaten: 4,
            age: TimeSpan.FromHours(7)));

        decision.Action.Should().Be(RepriceAction.CrossSpread);
        decision.NewPrice.Should().BeApproximately(1300.5, 0.001);
    }

    [Fact]
    public void AbandonsAnUnfilledBuyRatherThanCrossing()
    {
        // Nothing was bought, so there is nothing to salvage — the coins are better spent on another flip.
        var decision = RepricePolicy.Decide(SellLeg(age: TimeSpan.FromHours(7), bestAsk: 1583.2, pollsBeaten: 4)
            with { IsBuyLeg = true, BestOnOurSide = 1300.6, OurPrice = 1300.3, EntryPrice = 1300.3 });

        decision.Action.Should().Be(RepriceAction.Abandon);
    }

    [Fact]
    public void WillNotPriceAgainstAStaleBook()
    {
        var decision = RepricePolicy.Decide(SellLeg(
            bestAsk: 1583.2,
            pollsBeaten: 5,
            dataAge: TimeSpan.FromMinutes(5)));

        decision.Action.Should().Be(RepriceAction.Hold);
        decision.Reason.Should().Contain("stale");
    }

    [Fact]
    public void BuyLegStepsUpwardsNotDownwards()
    {
        var decision = RepricePolicy.Decide(SellLeg() with
        {
            IsBuyLeg = true,
            OurPrice = 1300.3,
            EntryPrice = 1300.3,
            BestOnOurSide = 1300.5,
            PollsBeaten = 2,
            CompetingDepth = 900
        });

        decision.Action.Should().Be(RepriceAction.Step);
        decision.NewPrice.Should().BeApproximately(1300.6, 0.001);
    }
}
