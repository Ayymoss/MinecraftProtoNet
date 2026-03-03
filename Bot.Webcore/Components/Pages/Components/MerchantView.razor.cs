using Microsoft.AspNetCore.Components;
using MinecraftProtoNet.Core.State;

namespace Bot.Webcore.Components.Pages.Components;

public partial class MerchantView
{
    [Parameter, EditorRequired] public required MerchantState MerchantData { get; set; }

    [Parameter] public EventCallback<int> OnTradeSelected { get; set; }

    private int GetXpProgress()
    {
        // XP thresholds per level: 0, 10, 70, 150, 250
        int[] thresholds = { 0, 10, 70, 150, 250, int.MaxValue };
        var level = Math.Clamp(MerchantData.VillagerLevel, 1, 5);
        var current = thresholds[level - 1];
        var next = thresholds[level];
        var progress = MerchantData.VillagerXp - current;
        var range = next - current;
        return range > 0 ? (int)(100.0 * progress / range) : 100;
    }

    private string GetTradeClass(bool isSelected, bool isDisabled)
    {
        var baseClass = "flex items-center gap-4 p-4 rounded-2xl transition-all duration-200 cursor-pointer group ";

        if (isDisabled)
            return baseClass + "bg-slate-900/20 opacity-50 cursor-not-allowed border border-slate-800/40 grayscale-[0.5]";
        if (isSelected)
            return baseClass + "bg-amber-500/10 border border-amber-500/40 shadow-lg shadow-amber-500/5 ring-1 ring-amber-500/20 scale-[1.01]";
        
        return baseClass + "bg-slate-900/40 border border-slate-800/60 hover:bg-slate-900/60 hover:border-slate-700 hover:scale-[1.01] shadow-sm";
    }

    private string GetItemSlotClass(bool isResult)
    {
        var baseClass = "relative w-12 h-12 rounded-xl flex items-center justify-center p-1 transition-all duration-200 overflow-hidden ";
        
        if (isResult)
            return baseClass + "bg-amber-500/10 border border-amber-500/30 group-hover:border-amber-400";
            
        return baseClass + "bg-slate-950/60 border border-slate-800 group-hover:border-slate-700 shadow-inner";
    }

    private string GetItemName(int itemId)
    {
        var name = Bot.ItemRegistry.GetItemName(itemId);
        if (string.IsNullOrEmpty(name)) return $"#{itemId}";
        return name.Replace("minecraft:", "").Replace("_", " ");
    }

    private async Task SelectTrade(int index)
    {
        if (MerchantData.Offers[index].IsOutOfStock) return;
        await OnTradeSelected.InvokeAsync(index);
    }
}
