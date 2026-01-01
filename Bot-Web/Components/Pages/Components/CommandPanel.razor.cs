namespace Bot_Web.Components.Pages.Components;

public partial class CommandPanel
{
    private string _selectedCommand = "";
    private string _arguments = "";
    private string _result = "";
    private bool _success;
    private bool _executing;

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

    private IEnumerable<(string Name, string Description)> GetAvailableCommands()
    {
        return Bot.CommandRegistry.GetExternalCommands()
            .Select(c => (c.Name, c.Description))
            .OrderBy(c => c.Name);
    }

    private async Task ExecuteCommand()
    {
        if (string.IsNullOrEmpty(_selectedCommand)) return;

        _executing = true;
        _result = "";
        StateHasChanged();

        try
        {
            var args = string.IsNullOrWhiteSpace(_arguments)
                ? Array.Empty<string>()
                : _arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            var (success, message) = await Bot.CommandRegistry.ExecuteExternalAsync(
                _selectedCommand,
                args,
                Bot.Client);

            _success = success;
            _result = message;
        }
        catch (Exception ex)
        {
            _success = false;
            _result = $"Error: {ex.Message}";
        }
        finally
        {
            _executing = false;
            StateHasChanged();
        }
    }
}
