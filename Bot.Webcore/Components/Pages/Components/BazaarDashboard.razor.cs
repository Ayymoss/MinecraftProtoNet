using MinecraftProtoNet.Bazaar.Engine;
using MinecraftProtoNet.Bazaar.Orders;

namespace Bot.Webcore.Components.Pages.Components;

public partial class BazaarDashboard
{
    private BazaarTradingEngine Engine => Bot.BazaarEngine;
    private TradingState Financial => Engine.FinancialState;
    private IReadOnlyList<OrderRecord> ActiveOrders => Engine.Orders.ActiveOrders;
    private IReadOnlyList<TradeRecord> CompletedTrades => Financial.CompletedTrades;

    protected override void OnInitialized()
    {
        Bot.OnStateChanged += HandleStateChanged;
    }

    private void HandleStateChanged() => InvokeAsync(StateHasChanged);

    private string GetStateBadgeClass() => Engine.State switch
    {
        TradingEngineState.Idle => "bg-slate-500/15 text-slate-400 border border-slate-500/20",
        TradingEngineState.Monitoring => "bg-emerald-500/15 text-emerald-400 border border-emerald-500/20",
        TradingEngineState.Halted => "bg-rose-500/15 text-rose-400 border border-rose-500/20",
        _ => "bg-violet-500/15 text-violet-400 border border-violet-500/20"
    };

    private static string GetOrderStatusClass(OrderStatus status) => status switch
    {
        OrderStatus.Active => "bg-emerald-500/15 text-emerald-400",
        OrderStatus.Pending => "bg-amber-500/15 text-amber-400",
        OrderStatus.Filled => "bg-blue-500/15 text-blue-400",
        OrderStatus.Cancelled => "bg-slate-500/15 text-slate-400",
        _ => "bg-slate-500/15 text-slate-400"
    };

    public void Dispose()
    {
        Bot.OnStateChanged -= HandleStateChanged;
    }
}
