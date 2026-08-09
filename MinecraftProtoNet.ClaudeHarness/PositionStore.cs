using System.Text.Json;

namespace MinecraftProtoNet.ClaudeHarness;

/// <summary>
/// One open position as it survives outside the process.
///
/// Written as a flat record rather than reusing <see cref="Position"/> so that adding a field to the live type
/// cannot silently change the on-disk format, and so a file written by an older build still loads.
/// </summary>
public sealed record PositionSnapshot(
    string ProductKey,
    string Name,
    int Quantity,
    double EntryMarginPerUnit,
    string Side,
    double OrderPrice,
    double LegEntryPrice,
    DateTime LegStarted,
    int Steps,
    int UnitsBought,
    double CoinsSpent,
    int UnitsSold,
    double CoinsReceived,
    /// <summary>
    /// Whether <see cref="CoinsSpent"/> is measured or assumed. Persisted, because a position inherited with a
    /// guessed basis stays a guess across restarts — without this the guess is reloaded from a file that looks
    /// authoritative and starts counting towards P&amp;L. Defaults to true so files written before this field
    /// existed still load as the measured positions they were.
    /// </summary>
    bool BasisKnown = true,
    /// <summary>
    /// When the whole flip started. Optional so files written before it existed still load; those fall back
    /// to the leg start, which is the closest thing they recorded.
    /// </summary>
    DateTime? Opened = null,
    /// <summary>
    /// When the flip was closed. Optional so files written before it existed still load; those show as
    /// unknown rather than being back-dated to something invented, which would corrupt the P&amp;L timeline.
    /// </summary>
    DateTime? ClosedAt = null);

/// <param name="Open">Positions with a live order. Matched against the order menu on startup.</param>
/// <param name="Closed">
/// Completed flips, kept so realised profit accumulates across restarts rather than resetting to zero every
/// time the process cycles. Deliberately a SEPARATE list: adoption only ever matches against <paramref
/// name="Open"/>, so a finished position can never lend its cost basis to a new order of the same name.
/// </param>
public sealed record PortfolioStateFile(
    DateTime SavedUtc,
    List<PositionSnapshot> Open,
    List<PositionSnapshot>? Closed = null);

/// <summary>
/// Remembers open positions across process restarts.
///
/// The Bazaar is the authority on WHICH orders exist — it is a live market and the file cannot be — but it is
/// not an authority on what they cost us. An order row shows its own price, so a restart that rebuilds state
/// from the menu alone has to assume the offer price was the cost, which for a sell offer placed at a profit
/// is wrong in the direction that erases the profit: a shard bought at 3,915 and offered at 7,792 comes back
/// as a 7,792 cost basis and books the flip as break-even. Cost basis comes from Hypixel's claim messages and
/// exists only in this process, so it is what gets persisted; the menu still decides what is actually live.
/// </summary>
public static class PositionStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static string FilePath { get; } = ResolveFilePath();

    /// <summary>
    /// Finds the one ledger, from wherever the binary happens to live.
    ///
    /// This used to walk a FIXED four parents up from the binary, which silently made the path depend on how
    /// deep the output directory was. Running from bin/Debug/net10.0 landed on the repository root, while the
    /// per-arm copy in bin/tradebot landed one level higher — so the two builds kept SEPARATE ledgers and each
    /// booted believing the other's closed flips had never happened. That is what made realised P&amp;L jump by
    /// an order of magnitude depending on which build started, and it is not a number anyone can sanity-check
    /// after the fact.
    ///
    /// Anchoring on the solution file instead means every build resolves to the same place no matter how it was
    /// published or where it was copied.
    /// </summary>
    private static string ResolveFilePath()
    {
        const string fileName = "bazaar-open-positions.json";

        // Explicit override first, so a test or a deliberately isolated arm can have its own book.
        if (Environment.GetEnvironmentVariable("MCPROTO_STATE_DIR") is { Length: > 0 } overrideDir)
            return Path.Combine(overrideDir, fileName);

        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (dir.GetFiles("MinecraftProtoNet.slnx").Length > 0)
                return Path.Combine(dir.FullName, "_ServerReferences", fileName);
        }

        // Published somewhere with no solution beside it. Keeping the book next to the binary is wrong in a
        // different way, but it is at least predictable and cannot be confused with the repository's copy.
        return Path.Combine(AppContext.BaseDirectory, "_ServerReferences", fileName);
    }

    /// <summary>
    /// Saves the open book. Failure is swallowed: losing the file costs accuracy on a future restart, whereas
    /// throwing here would abort a cycle that is holding real orders.
    /// </summary>
    public static void Save(IEnumerable<Position> open, IEnumerable<Position> closed, Action<string>? log = null)
    {
        try
        {
            var snapshot = new PortfolioStateFile(
                DateTime.UtcNow,
                open.Select(ToSnapshot).ToList(),
                closed.Select(ToSnapshot).ToList());

            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(snapshot, Options));
        }
        catch (Exception ex)
        {
            log?.Invoke($"  could not save position state ({ex.Message})");
        }
    }

    private static PositionSnapshot ToSnapshot(Position p) => new(
        p.ProductKey, p.Name, p.Quantity, p.EntryMarginPerUnit, p.Side.ToString(),
        p.OrderPrice, p.LegEntryPrice, p.LegStarted, p.Steps,
        p.UnitsBought, p.CoinsSpent, p.UnitsSold, p.CoinsReceived, p.BasisKnown, p.Opened, p.ClosedAt);

    public static PortfolioStateFile Load(Action<string>? log = null)
    {
        try
        {
            if (!File.Exists(FilePath)) return new PortfolioStateFile(DateTime.UtcNow, [], []);

            var file = JsonSerializer.Deserialize<PortfolioStateFile>(File.ReadAllText(FilePath), Options);
            return file ?? new PortfolioStateFile(DateTime.UtcNow, [], []);
        }
        catch (Exception ex)
        {
            log?.Invoke($"  could not read saved position state ({ex.Message}) — falling back to the order menu");
            return new PortfolioStateFile(DateTime.UtcNow, [], []);
        }
    }

    /// <summary>Rebuilds a completed flip, so its profit still counts after a restart.</summary>
    public static Position Rehydrate(PositionSnapshot s) =>
        new(s.ProductKey, s.Name, s.Quantity, s.EntryMarginPerUnit)
        {
            Side = PositionSide.Closed,
            OrderPrice = s.OrderPrice,
            LegEntryPrice = s.LegEntryPrice,
            Opened = s.Opened ?? s.LegStarted,
            LegStarted = s.LegStarted,
            Steps = s.Steps,
            UnitsBought = s.UnitsBought,
            CoinsSpent = s.CoinsSpent,
            UnitsSold = s.UnitsSold,
            CoinsReceived = s.CoinsReceived,
            BasisKnown = s.BasisKnown,
            ClosedAt = s.ClosedAt
        };
}
