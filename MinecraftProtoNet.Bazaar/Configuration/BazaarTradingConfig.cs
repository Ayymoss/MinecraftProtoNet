namespace MinecraftProtoNet.Bazaar.Configuration;

/// <summary>
/// Configuration for autonomous Bazaar trading. Bound from appsettings.json "BazaarTrading" section.
/// </summary>
public sealed class BazaarTradingConfig
{
    public const string SectionName = "BazaarTrading";
    public const double DefaultTaxRate = 0.01125;

    // Capital limits
    public double MaxTotalInvestment { get; set; } = 10_000_000;
    public double MaxPerTradeInvestment { get; set; } = 1_000_000;
    public double MinProfitPerUnit { get; set; } = 50;
    public double MinProfitPercent { get; set; } = 1.0;

    // Order limits (Bazaar max = 7 buy + 7 sell = 14 total)
    public int MaxConcurrentOrders { get; set; } = 14;
    public int MaxBuyOrders { get; set; } = 7;
    public int MaxSellOffers { get; set; } = 7;

    // Order walking (price adjustment to stay competitive)
    public TimeSpan OrderStaleThreshold { get; set; } = TimeSpan.FromMinutes(5);
    public double MaxWalkSlippagePercent { get; set; } = 2.0;
    public int MaxWalksPerOrder { get; set; } = 3;

    // Safety circuit breakers
    public double MaxLossBeforeHalt { get; set; } = 500_000;
    public double MinMarketHealthScore { get; set; } = 25;
    public double ManipulationIntensityThreshold { get; set; } = 0.7;
    public int MaxConsecutiveFailures { get; set; } = 5;

    // Polling intervals
    public TimeSpan FlipPollInterval { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan MarketHealthPollInterval { get; set; } = TimeSpan.FromMinutes(2);

    // API filters (passed to BazaarCompanion /api/bot/flips)
    public double MinOpportunityScore { get; set; } = 3.0;
    public double MinBidPrice { get; set; } = 100;
    public double? MaxBidPrice { get; set; }
    public double MinWeeklyAskVolume { get; set; } = 25_000;

    // GUI timing
    public int GuiClickDelayMs { get; set; } = 150;
    public int GuiWaitForUpdateMs { get; set; } = 500;
    public int ChatConfirmationTimeoutMs { get; set; } = 5000;

    // BazaarCompanion API
    public string BazaarCompanionBaseUrl { get; set; } = "http://localhost:5000";
    public string BazaarCompanionApiKey { get; set; } = "";

    // Tax
    public double TaxRate { get; set; } = DefaultTaxRate;
}
