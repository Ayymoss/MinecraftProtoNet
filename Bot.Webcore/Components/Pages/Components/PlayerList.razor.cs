using MinecraftProtoNet.Core.State;

namespace Bot.Webcore.Components.Pages.Components;

public partial class PlayerList
{
    private string _searchFilter = string.Empty;
    private bool IsFollowing => Bot.FollowProcess?.Following().Count > 0;

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

    private void FollowPlayer(Player player)
    {
        if (player.Entity == null) return;
        var targetEntityId = player.Entity.EntityId;
        Bot.FollowProcess?.Follow(e => e is Entity entity && entity.EntityId == targetEntityId);
        Bot.NotifyStateChanged();
    }

    private void StopFollowing()
    {
        Bot.FollowProcess?.Cancel();
        Bot.NotifyStateChanged();
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
