namespace Bot_Web.Components.Pages.Components;

public partial class PlayerList
{
    protected override void OnInitialized()
    {
        Bot.OnStateChanged += HandleStateChanged;
    }

    private void HandleStateChanged()
    {
        InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        Bot.OnStateChanged -= HandleStateChanged;
    }

    private int GetOtherPlayerCount()
    {
        var localUuid = Bot.State.LocalPlayer?.Uuid ?? Guid.Empty;
        return Bot.State.Level.GetAllPlayers().Count(p => p.Uuid != localUuid);
    }

    private string FormatPosition(double x, double y, double z)
    {
        return $"{x:F0}, {y:F0}, {z:F0}";
    }

    private string GetItemName(int itemId)
    {
        var name = Bot.ItemRegistry.GetItemName(itemId);
        if (string.IsNullOrEmpty(name)) return $"#{itemId}";
        var shortName = name.Replace("minecraft:", "").Replace("_", " ");
        return shortName.Length > 12 ? shortName[..11] + "…" : shortName;
    }
}