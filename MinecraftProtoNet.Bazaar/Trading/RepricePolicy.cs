namespace MinecraftProtoNet.Bazaar.Trading;

/// <summary>What to do with a live order this poll.</summary>
public enum RepriceAction
{
    /// <summary>Leave it where it is.</summary>
    Hold,

    /// <summary>Cancel and re-post one tick better than whoever beat us.</summary>
    Step,

    /// <summary>Stop working the order: cancel it and take the position off the table.</summary>
    Abandon,

    /// <summary>Take whatever the other side is offering right now, to clear inventory.</summary>
    CrossSpread
}

public sealed record RepriceDecision(RepriceAction Action, double NewPrice, string Reason);

/// <summary>
/// The state a repricing decision is made from. Everything here is measured, not guessed.
/// </summary>
public sealed record RepriceContext(
    bool IsBuyLeg,
    /// <summary>The price our live order sits at.</summary>
    double OurPrice,
    /// <summary>What we paid per unit (sell leg), or 0 while buying.</summary>
    double CostPerUnit,
    /// <summary>Best price on our own side of the book — includes our order when we are top.</summary>
    double BestOnOurSide,
    /// <summary>Best price on the other side, i.e. what we could cross to right now.</summary>
    double BestOnOtherSide,
    /// <summary>Units queued at the price that beat us. A trap is usually thin.</summary>
    int CompetingDepth,
    /// <summary>The margin per unit this flip was entered for — the yardstick everything is measured against.</summary>
    double EntryMarginPerUnit,
    /// <summary>The price this leg was first posted at, for bounding total drift.</summary>
    double EntryPrice,
    /// <summary>Consecutive polls we have been beaten for. One poll is a flicker; several is a market.</summary>
    int PollsBeaten,
    /// <summary>Steps already taken on this leg — a rising count is the signature of a price war.</summary>
    int StepsTaken,
    TimeSpan Age,
    /// <summary>How stale the book data is. Acting on a minutes-old book is how you chase ghosts.</summary>
    TimeSpan DataAge);

/// <summary>
/// Decides whether to move a live order, and by how much.
///
/// The problem this solves is not "how do I stay top of book" — staying top is trivial and ruinous. It is
/// "when is stepping forward still worth it", because an order that keeps stepping is an order someone else
/// can drive.
///
/// The specific attack: post a tick better than the top, wait for the incumbent to lazily re-post a tick
/// better than THAT, and repeat until their price is somewhere silly — at which point the attacker's original
/// order fills against the mess and the walker eats the loss. It works precisely because the victim reacts
/// immediately and unconditionally. Three things break it:
///
///   1. Confirmation. A step needs the beating price to survive several polls. A bait order that gets pulled
///      the moment it is beaten never survives that long.
///   2. A profit floor. Every step is checked against the margin the flip was ENTERED for, not against the
///      current book, so being walked always ends in "hold" rather than in a bad fill.
///   3. A walk budget. Total drift from the entry price is capped, and the cap tightens the more steps have
///      already been taken. A price war therefore terminates on our side by construction.
///
/// The counterweight is patience: an order that never moves never fills. So the floor relaxes with age —
/// generous early, break-even eventually — because capital tied up in an unsold position costs more than the
/// last few coins of margin. Timings come from how the market behaves rather than from taste: the book moves
/// on a minute cadence, a good flip can take hours, and a position still open after several hours is a
/// position that was mispriced.
/// </summary>
public static class RepricePolicy
{
    /// <summary>Hypixel prices in tenths of a coin, so a tick is 0.1.</summary>
    public const double Tick = 0.1;

    /// <summary>Below this the numbers are noise, not a decision.</summary>
    private const double Epsilon = 0.0001;

    /// <summary>Polls the beating price must persist for before we react. At a 60s cadence this is ~2 minutes.</summary>
    public const int ConfirmPolls = 2;

    /// <summary>
    /// A level thinner than this, when it appears far from our price, reads as bait rather than as demand.
    /// Real competition at the top of a liquid book is rarely a handful of units.
    /// </summary>
    public const int SuspiciouslyThinDepth = 16;

    /// <summary>Steps after which we stop treating this as a market and start treating it as a fight.</summary>
    public const int WarStepThreshold = 4;

    public static readonly TimeSpan HoldPhase = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan WorkPhase = TimeSpan.FromMinutes(60);
    public static readonly TimeSpan ClearPhase = TimeSpan.FromHours(6);

    /// <summary>
    /// The least margin per unit we will still step for, as a fraction of what the flip was entered for.
    ///
    /// Early on we are picky — there are other trades. Later the calculus inverts: a position that has been
    /// open for hours is costing us every other flip we could have run with that capital, so we accept
    /// thinner and thinner margin, down to break-even, rather than hold out for the original number.
    /// </summary>
    public static double MarginFloorFraction(TimeSpan age) => age switch
    {
        _ when age < HoldPhase => 0.60,
        _ when age < WorkPhase => 0.30,
        _ when age < ClearPhase => 0.05,
        _ => 0.0
    };

    /// <summary>How far the price may drift from where the leg started, as a fraction of the entry price.</summary>
    public static double WalkBudgetFraction(TimeSpan age) => age switch
    {
        _ when age < HoldPhase => 0.01,
        _ when age < WorkPhase => 0.03,
        _ when age < ClearPhase => 0.10,
        _ => 0.20
    };

    public static RepriceDecision Decide(RepriceContext ctx)
    {
        // Never act on a book we know is stale — that is how you chase prices that no longer exist. The
        // service refreshes about once a minute, so anything much older than that is a missed refresh.
        if (ctx.DataAge > TimeSpan.FromMinutes(3))
        {
            return new RepriceDecision(RepriceAction.Hold, ctx.OurPrice,
                $"book data is {ctx.DataAge.TotalSeconds:F0}s old — too stale to price against");
        }

        // Are we still the best on our side? On a buy leg "better" is higher, on a sell leg lower.
        var beaten = ctx.IsBuyLeg
            ? ctx.BestOnOurSide > ctx.OurPrice + Epsilon
            : ctx.BestOnOurSide < ctx.OurPrice - Epsilon;

        if (!beaten)
        {
            return new RepriceDecision(RepriceAction.Hold, ctx.OurPrice, "still best on our side");
        }

        // A position held past the last phase is not going to fill on patience alone. Clearing it beats
        // holding it: for goods we own, cross the spread; for a buy that never filled, simply walk away —
        // nothing was bought, so there is nothing to salvage and the capital is better used elsewhere.
        if (ctx.Age >= ClearPhase)
        {
            return ctx.IsBuyLeg
                ? new RepriceDecision(RepriceAction.Abandon, ctx.OurPrice,
                    $"unfilled after {ctx.Age.TotalHours:F1}h — cancelling to free the coins for a better trade")
                : new RepriceDecision(RepriceAction.CrossSpread, ctx.BestOnOtherSide,
                    $"still holding after {ctx.Age.TotalHours:F1}h — taking the best bid to clear it");
        }

        if (ctx.PollsBeaten < ConfirmPolls)
        {
            return new RepriceDecision(RepriceAction.Hold, ctx.OurPrice,
                $"beaten for {ctx.PollsBeaten} poll(s); waiting for {ConfirmPolls} before reacting");
        }

        var candidate = ctx.IsBuyLeg
            ? Math.Round(ctx.BestOnOurSide + Tick, 1)
            : Math.Round(ctx.BestOnOurSide - Tick, 1);

        // Would this step still leave a flip worth doing? Margin is always measured against what we actually
        // paid (or would pay), never against the current book, so a walked price cannot talk us into a loss.
        var floor = ctx.EntryMarginPerUnit * MarginFloorFraction(ctx.Age);
        var margin = ctx.IsBuyLeg
            ? ctx.EntryMarginPerUnit - (candidate - ctx.EntryPrice)
            : (candidate * (1 - BazaarTax)) - ctx.CostPerUnit;

        if (margin < floor)
        {
            return new RepriceDecision(RepriceAction.Hold, ctx.OurPrice,
                $"stepping to {candidate} leaves {margin:F1}/unit, under the {floor:F1} floor for this stage — " +
                "holding and letting it fill on its own terms");
        }

        // Total drift from where this leg started.
        var drift = Math.Abs(candidate - ctx.EntryPrice);
        var budget = ctx.EntryPrice * WalkBudgetFraction(ctx.Age);
        if (drift > budget)
        {
            return new RepriceDecision(RepriceAction.Hold, ctx.OurPrice,
                $"stepping to {candidate} is {drift:F1} from entry, past the {budget:F1} walk budget — holding");
        }

        // The bait test. A thin level, a long way from where we are, that we have already stepped towards
        // several times, is someone playing with us rather than a market repricing.
        var gap = Math.Abs(ctx.BestOnOurSide - ctx.OurPrice);
        if (ctx.CompetingDepth is > 0 and < SuspiciouslyThinDepth && gap > Tick * 3 && ctx.StepsTaken >= 2)
        {
            return new RepriceDecision(RepriceAction.Hold, ctx.OurPrice,
                $"the price beating us is only {ctx.CompetingDepth} units, {gap:F1} away, after {ctx.StepsTaken} " +
                "steps — that is a ladder, not demand; holding");
        }

        if (ctx.StepsTaken >= WarStepThreshold)
        {
            return new RepriceDecision(RepriceAction.Hold, ctx.OurPrice,
                $"{ctx.StepsTaken} steps on this leg already — disengaging from the price war and waiting");
        }

        return new RepriceDecision(RepriceAction.Step, candidate,
            $"beaten for {ctx.PollsBeaten} polls by a {ctx.CompetingDepth}-unit level; stepping to {candidate} " +
            $"keeps {margin:F1}/unit");
    }

    /// <summary>Kept in step with BazaarCompanion's own constant; the API publishes it on market/health.</summary>
    private const double BazaarTax = 0.01125;
}
