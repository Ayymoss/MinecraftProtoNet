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
        var baseClass = "flex items-center gap-3 p-2 rounded-lg transition-all cursor-pointer ";

        if (isDisabled)
            return baseClass + "bg-slate-800/30 opacity-50 cursor-not-allowed";
        if (isSelected)
            return baseClass + "bg-amber-900/30 border border-amber-500/50";
        return baseClass + "bg-slate-800/50 border border-slate-700/50 hover:border-slate-600";
    }

    private string GetItemSlotClass() =>
        "relative w-10 h-10 rounded bg-slate-900/50 border border-slate-700/50 " +
        "flex items-center justify-center p-0.5";

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
