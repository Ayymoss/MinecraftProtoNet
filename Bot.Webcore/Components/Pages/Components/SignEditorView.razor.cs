using Bot.Webcore.Services;
using Microsoft.AspNetCore.Components;
using MinecraftProtoNet.Core.Packets.Play.Serverbound;

namespace Bot.Webcore.Components.Pages.Components;

public partial class SignEditorView
{
    private SignEditorState? SignEditor => Bot.CurrentSignEditor;

    protected override void OnInitialized()
    {
        Bot.OnStateChanged += HandleStateChanged;
    }

    private void HandleStateChanged() => InvokeAsync(StateHasChanged);

    private void OnLineChanged(int lineIndex, ChangeEventArgs e)
    {
        if (SignEditor is null) return;
        SignEditor.Lines[lineIndex] = e.Value?.ToString() ?? "";
    }

    private async Task Submit()
    {
        if (SignEditor is null) return;

        await Bot.Client.SendPacketAsync(new SignUpdatePacket
        {
            Position = SignEditor.Position,
            IsFrontText = SignEditor.IsFrontText,
            Lines = SignEditor.Lines
        });

        Bot.CurrentSignEditor = null;
        await InvokeAsync(StateHasChanged);
    }

    private async Task Cancel()
    {
        if (SignEditor is null) return;

        // Send empty lines to close the sign editor on the server side
        await Bot.Client.SendPacketAsync(new SignUpdatePacket
        {
            Position = SignEditor.Position,
            IsFrontText = SignEditor.IsFrontText,
            Lines = SignEditor.Lines
        });

        Bot.CurrentSignEditor = null;
        await InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        Bot.OnStateChanged -= HandleStateChanged;
    }
}
