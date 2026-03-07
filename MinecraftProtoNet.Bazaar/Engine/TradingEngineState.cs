namespace MinecraftProtoNet.Bazaar.Engine;

/// <summary>
/// States of the Bazaar trading engine state machine.
/// </summary>
public enum TradingEngineState
{
    /// <summary>Engine is idle, not actively trading.</summary>
    Idle,

    /// <summary>Fetching flip opportunities from BazaarCompanion API.</summary>
    FetchingOpportunities,

    /// <summary>Opening the Bazaar GUI via /bazaar command.</summary>
    OpeningBazaar,

    /// <summary>Navigating to a product in the Bazaar GUI.</summary>
    NavigatingToProduct,

    /// <summary>Placing a buy order or sell offer.</summary>
    PlacingOrder,

    /// <summary>Monitoring active orders for fills and undercuts.</summary>
    Monitoring,

    /// <summary>Walking (adjusting) an order price to stay competitive.</summary>
    WalkingOrder,

    /// <summary>Claiming filled orders from the Bazaar GUI.</summary>
    ClaimingOrder,

    /// <summary>Engine is halted due to safety guard trigger.</summary>
    Halted,

    /// <summary>Engine encountered an error and is in recovery.</summary>
    Recovering
}
