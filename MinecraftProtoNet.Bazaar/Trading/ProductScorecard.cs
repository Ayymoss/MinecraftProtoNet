using System.Text.Json;

namespace MinecraftProtoNet.Bazaar.Trading;

/// <summary>What a product has actually earned us, as opposed to what the book predicted it would.</summary>
public sealed record ProductRecord(
    string ProductKey,
    string Name,
    /// <summary>Time-decayed coins per hour of capital committed. The number selection leans on.</summary>
    double ProfitPerHour,
    /// <summary>When <see cref="ProfitPerHour"/> was last brought up to date, so decay can be applied since.</summary>
    DateTime UpdatedUtc,
    int Trades,
    double TotalProfit);

public sealed record ScorecardFile(DateTime SavedUtc, List<ProductRecord> Products);

/// <summary>
/// Remembers which products actually pay, and forgets it at a controlled rate.
///
/// The API ranks candidates on the current book, which says what a flip *should* earn but not what it *did*.
/// Those differ: a product can show a wide spread all day and never fill, while another turns over three
/// times an hour. Tracking realised profit per hour of committed capital lets the bot lean into what is
/// working — the point of this class.
///
/// The forgetting matters as much as the remembering. A product that was extraordinary this morning and is
/// worth two coins a trade this afternoon must stop being favoured, and no fixed window does that well: too
/// short and one good run is forgotten, too long and a dead market is traded for days on yesterday's numbers.
/// So the score decays exponentially with a half-life — after <see cref="HalfLifeHours"/> of silence a
/// product's record counts for half, after two half-lives a quarter, converging on neutral rather than on
/// "bad". A genuinely good product that keeps trading keeps refreshing its score and stays favoured for as
/// long as it deserves to be, which is the "abuse it until it stops working" case.
/// </summary>
public sealed class ProductScorecard
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    /// <summary>
    /// How long a product's record takes to lose half its weight. Six hours is deliberately shorter than a
    /// day: the failure the user described — great on day one, worthless on day two — has to be caught well
    /// inside a day, and a product still trading refreshes its own score anyway.
    /// </summary>
    public const double HalfLifeHours = 6.0;

    /// <summary>
    /// Trades a product must have before a bad score is held against it. One disappointing flip is noise —
    /// benching on it would rule out products that were simply unlucky once.
    /// </summary>
    private const int MinTradesToBench = 2;

    /// <summary>Coins per hour below which a proven-poor product is skipped entirely.</summary>
    private const double BenchBelowProfitPerHour = 250;

    /// <summary>
    /// How long a bench lasts before the product is willing to be tried again.
    ///
    /// Without this a bench is permanent, and not by design: a poor score decays toward zero, and zero is
    /// still below the floor, so a product condemned on one bad afternoon would never be picked again. Bazaar
    /// markets are cyclical — an item worth nothing today can carry a wide spread next week — so the
    /// PENALTY has to expire as well as the praise. A day is long enough that a dead market is not retried
    /// every few minutes, and short enough to catch a revival while it is still worth having.
    ///
    /// Note what the product comes back as: its score has decayed to roughly zero, which this class treats as
    /// unknown rather than bad, so it re-enters the pool on equal footing with anything never tried. One flip
    /// then re-measures it, and because the blend weight rises with elapsed time that single fresh result —
    /// good or bad — is what its score becomes.
    /// </summary>
    public const double BenchHealHours = 24.0;

    public static string FilePath { get; } = Path.Combine(
        new DirectoryInfo(AppContext.BaseDirectory).Parent?.Parent?.Parent?.Parent?.FullName ?? AppContext.BaseDirectory,
        "_ServerReferences",
        "bazaar-product-history.json");

    private readonly Dictionary<string, ProductRecord> _records = new(StringComparer.OrdinalIgnoreCase);

    public static ProductScorecard Load(Action<string>? log = null)
    {
        var scorecard = new ProductScorecard();
        try
        {
            if (!File.Exists(FilePath)) return scorecard;

            var file = JsonSerializer.Deserialize<ScorecardFile>(File.ReadAllText(FilePath), Options);
            foreach (var record in file?.Products ?? []) scorecard._records[record.ProductKey] = record;
        }
        catch (Exception ex)
        {
            log?.Invoke($"  could not read the product scorecard ({ex.Message}) — starting from no history");
        }

        return scorecard;
    }

    public void Save(Action<string>? log = null)
    {
        try
        {
            var file = new ScorecardFile(DateTime.UtcNow, _records.Values.OrderBy(r => r.Name).ToList());
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(file, Options));
        }
        catch (Exception ex)
        {
            log?.Invoke($"  could not save the product scorecard ({ex.Message})");
        }
    }

    /// <summary>The decay factor applied to a score after <paramref name="hours"/> without an update.</summary>
    public static double Decay(double hours) => Math.Pow(0.5, Math.Max(0, hours) / HalfLifeHours);

    /// <summary>
    /// The product's score as of <paramref name="now"/>, decayed for the time since it was last updated.
    /// Zero for anything never traded — unknown is neutral, never a penalty, or the bot would never try
    /// anything new.
    /// </summary>
    public double ScoreOf(string productKey, DateTime now)
    {
        if (!_records.TryGetValue(productKey, out var record)) return 0;
        return record.ProfitPerHour * Decay((now - record.UpdatedUtc).TotalHours);
    }

    /// <summary>
    /// Whether a product has proved itself not worth the bot's time *at the moment*. Requires evidence —
    /// several trades and a score below the floor — and expires after <see cref="BenchHealHours"/> so that a
    /// market which has since turned around is not shut out by a judgement made days ago.
    /// </summary>
    public bool IsBenched(string productKey, DateTime now)
    {
        if (!_records.TryGetValue(productKey, out var record)) return false;
        if (record.Trades < MinTradesToBench) return false;

        // The bench heals. A verdict is only worth acting on while the evidence behind it is current.
        if ((now - record.UpdatedUtc).TotalHours >= BenchHealHours) return false;

        return ScoreOf(productKey, now) < BenchBelowProfitPerHour;
    }

    /// <summary>
    /// Folds a completed flip into the product's record.
    ///
    /// The sample is profit per hour of capital committed, not profit per trade: a flip earning 500 coins in
    /// ten minutes is worth six times one earning 500 in an hour, and capital is the scarce resource. Blending
    /// is time-weighted — the longer since the last sample, the more the new one counts — so a product's score
    /// tracks its recent behaviour rather than averaging over its whole history.
    /// </summary>
    public void RecordOutcome(string productKey, string name, double profit, TimeSpan held, DateTime now)
    {
        // A flip that closes in seconds would divide by ~zero and produce a nonsense rate; floor the duration.
        var hours = Math.Max(held.TotalHours, 1.0 / 60);
        var sample = profit / hours;

        if (_records.TryGetValue(productKey, out var existing))
        {
            var elapsed = (now - existing.UpdatedUtc).TotalHours;

            // The prior is decayed BEFORE blending, not after. Blending first and decaying later lets a huge
            // stale number dominate: a product worth 100,000/h yesterday and 2/h today would still score
            // thousands, and go on being favoured — the exact failure this class exists to prevent.
            var decayed = existing.ProfitPerHour * Decay(elapsed);

            // How much the new observation counts. A stale prior is worth little, so a long gap hands the
            // score to the new sample; the floor keeps a rapidly-repeating product responsive without letting
            // one flip erase its history.
            var alpha = Math.Max(0.4, 1 - Decay(elapsed));

            var blended = decayed * (1 - alpha) + sample * alpha;

            _records[productKey] = existing with
            {
                Name = name,
                ProfitPerHour = blended,
                UpdatedUtc = now,
                Trades = existing.Trades + 1,
                TotalProfit = existing.TotalProfit + profit
            };
        }
        else
        {
            _records[productKey] = new ProductRecord(productKey, name, sample, now, 1, profit);
        }
    }

    /// <summary>The best-performing products right now, for reporting what the bot has decided to favour.</summary>
    public IEnumerable<(string Key, string Name, double Score, int Trades, double Total)> Top(DateTime now, int count)
    {
        return _records.Values
            .Select(r => (Key: r.ProductKey, r.Name, Score: ScoreOf(r.ProductKey, now), r.Trades, Total: r.TotalProfit))
            .Where(r => r.Trades > 0)
            .OrderByDescending(r => r.Score)
            .Take(count);
    }
}
