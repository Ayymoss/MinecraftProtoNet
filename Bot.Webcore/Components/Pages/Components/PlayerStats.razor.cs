namespace Bot.Webcore.Components.Pages.Components;

public partial class PlayerStats
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
}
