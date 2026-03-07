using MinecraftProtoNet.Bazaar.Api.Dtos;
using MinecraftProtoNet.Bazaar.Configuration;
using MinecraftProtoNet.Bazaar.Gui;
using MinecraftProtoNet.Bazaar.Orders;
using MinecraftProtoNet.Bazaar.Safety;
using MinecraftProtoNet.Bazaar.Services;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Core.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MinecraftProtoNet.Bazaar.Engine;

/// <summary>
/// Main orchestrator for autonomous Bazaar trading. Runs as a state machine
/// hooked to the game loop via PostTick events.
/// </summary>
public sealed class BazaarTradingEngine : IDisposable
{
    private readonly IMinecraftClient _client;
    private readonly MarketDataService _marketData;
    private readonly OrderManager _orderManager;
    private readonly OrderWalker _orderWalker;
    private readonly TradingSafetyGuard _safetyGuard;
    private readonly BazaarGuiNavigator _guiNavigator;
    private readonly BazaarTradingConfig _config;
    private readonly IChatEventBus _chatEventBus;
    private readonly ILogger<BazaarTradingEngine> _logger;

    private TradingEngineState _state = TradingEngineState.Idle;
    private string? _haltReason;
    private int _tickCounter;
    private int _assessTickInterval;

    public TradingEngineState State => _state;
    public string? HaltReason => _haltReason;
    public TradingState FinancialState { get; } = new();
    public OrderManager Orders => _orderManager;
    public bool IsRunning => _state != TradingEngineState.Idle && _state != TradingEngineState.Halted;

    public BazaarTradingEngine(
        IMinecraftClient client,
        MarketDataService marketData,
        OrderManager orderManager,
        OrderWalker orderWalker,
        TradingSafetyGuard safetyGuard,
        BazaarGuiNavigator guiNavigator,
        IChatEventBus chatEventBus,
        IOptions<BazaarTradingConfig> config,
        ILogger<BazaarTradingEngine> logger)
    {
        _client = client;
        _marketData = marketData;
        _orderManager = orderManager;
        _orderWalker = orderWalker;
        _safetyGuard = safetyGuard;
        _guiNavigator = guiNavigator;
        _chatEventBus = chatEventBus;
        _config = config.Value;
        _logger = logger;

        // Convert poll intervals to tick counts (20 ticks/sec)
        _assessTickInterval = (int)(_config.FlipPollInterval.TotalSeconds * 20);

        // Subscribe to chat events for order confirmations
        _chatEventBus.OnSystemChat += OnSystemChat;
    }

    /// <summary>Starts the trading engine.</summary>
    public void Start(double initialBalance = 0)
    {
        if (_state != TradingEngineState.Idle && _state != TradingEngineState.Halted)
        {
            _logger.LogWarning("Engine already running in state {State}", _state);
            return;
        }

        FinancialState.CoinBalance = initialBalance;
        _state = TradingEngineState.Monitoring;
        _haltReason = null;
        _tickCounter = 0;
        _logger.LogInformation("Bazaar trading engine started with balance {Balance:N0}", initialBalance);
    }

    /// <summary>Stops the trading engine gracefully.</summary>
    public void Stop()
    {
        _state = TradingEngineState.Idle;
        _logger.LogInformation("Bazaar trading engine stopped");
    }

    /// <summary>
    /// Called every game tick from PostTick. Drives the state machine.
    /// Should not block — schedules async work internally.
    /// </summary>
    public void OnTick(IMinecraftClient client)
    {
        if (_state == TradingEngineState.Idle || _state == TradingEngineState.Halted)
            return;

        _tickCounter++;

        // Only run the assess loop at the configured interval
        if (_tickCounter % _assessTickInterval != 0)
            return;

        // Fire-and-forget the async trading loop (non-blocking on game thread)
        _ = Task.Run(async () =>
        {
            try
            {
                await RunTradingCycleAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Trading cycle error");
                FinancialState.ConsecutiveFailures++;
            }
        });
    }

    private async Task RunTradingCycleAsync()
    {
        // Phase 1: Safety check
        var health = await _marketData.GetMarketHealthAsync();
        var safetyIssue = _safetyGuard.CheckSafety(FinancialState, health);
        if (safetyIssue is not null)
        {
            Halt(safetyIssue);
            return;
        }

        // Phase 2: Check for stale orders that need walking
        var staleOrders = _orderManager.GetStaleOrders();
        foreach (var order in staleOrders)
        {
            var decision = await _orderWalker.EvaluateAsync(order);
            switch (decision.Action)
            {
                case WalkAction.Walk:
                    _logger.LogInformation("Walking order {OrderId}: {Reason}", order.Id, decision.Reason);
                    // TODO: GUI interaction to cancel and re-place at new price
                    order.WalkCount++;
                    order.PricePerUnit = decision.NewPrice!.Value;
                    break;

                case WalkAction.Cancel:
                    _logger.LogInformation("Cancelling order {OrderId}: {Reason}", order.Id, decision.Reason);
                    _orderManager.UpdateStatus(order.Id, OrderStatus.Cancelled);
                    break;
            }
        }

        // Phase 3: Look for new opportunities if we have capacity
        if (_orderManager.CanPlaceBuyOrder)
        {
            var opportunities = await _marketData.GetOpportunitiesAsync();
            foreach (var opp in opportunities)
            {
                if (!_orderManager.CanPlaceBuyOrder)
                    break;

                if (!_safetyGuard.IsOpportunitySafe(opp))
                    continue;

                if (!_safetyGuard.IsProfitable(opp.BestBidPrice, opp.BestAskPrice))
                    continue;

                // Check we don't already have a buy order for this product
                if (_orderManager.GetActiveBuyOrdersForProduct(opp.ProductKey).Count > 0)
                    continue;

                // Calculate quantity
                var maxQty = (int)(_config.MaxPerTradeInvestment / opp.BestBidPrice);
                var qty = Math.Max(1, Math.Min(maxQty, 64)); // Cap at one stack

                _logger.LogInformation("Opportunity: {Product} — buy {Qty} @ {Bid:F1}, sell @ {Ask:F1}, score {Score:F1}",
                    opp.ProductKey, qty, opp.BestBidPrice, opp.BestAskPrice, opp.OpportunityScore);

                // TODO: Phase 2 — GUI navigation to actually place the order
                // For now, just log the opportunity
            }
        }
    }

    private void Halt(string reason)
    {
        _state = TradingEngineState.Halted;
        _haltReason = reason;
        _logger.LogError("Trading engine HALTED: {Reason}", reason);
    }

    /// <summary>Resumes after a halt (resets halt state).</summary>
    public void Resume()
    {
        if (_state != TradingEngineState.Halted)
            return;

        _haltReason = null;
        FinancialState.ConsecutiveFailures = 0;
        _state = TradingEngineState.Monitoring;
        _logger.LogInformation("Trading engine resumed");
    }

    private void OnSystemChat(SystemChatEventArgs args)
    {
        // Parse Bazaar-specific messages
        var message = ChatMessageParser.Parse(args.TranslateKey, args.TextParts);
        if (message is null)
            return;

        _logger.LogDebug("Bazaar chat: {Type} — {Product} x{Qty} @ {Price}",
            message.Type, message.ProductName, message.Quantity, message.PricePerUnit);

        switch (message.Type)
        {
            case BazaarMessageType.BuyOrderPlaced:
                // Order confirmed by server
                break;

            case BazaarMessageType.BuyOrderFilled:
                // Our buy order was filled — we now have items to sell
                break;

            case BazaarMessageType.SellOfferFilled:
                // Our sell offer was filled — coins ready to claim
                break;

            case BazaarMessageType.CoinsClaimed:
                if (message.TotalCoins.HasValue)
                    FinancialState.RecordCoinsClaimed(message.TotalCoins.Value);
                break;

            case BazaarMessageType.OrderCancelled:
                if (message.TotalCoins.HasValue)
                    FinancialState.RecordRefund(message.TotalCoins.Value);
                break;
        }
    }

    public void Dispose()
    {
        _chatEventBus.OnSystemChat -= OnSystemChat;
        _guiNavigator.Dispose();
    }
}
