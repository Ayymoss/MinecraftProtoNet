using MinecraftProtoNet.Core.Core.Abstractions;
using MinecraftProtoNet.Core.Models.Core;

namespace MinecraftProtoNet.Core.Services;

/// <summary>
/// Singleton implementation of ISignEventBus. Publishes sign editor events
/// that external systems can subscribe to.
/// </summary>
public sealed class SignEventBus : ISignEventBus
{
    public event Func<SignEditorEventArgs, Task>? OnSignEditorOpened;

    public async Task<SignEditorEventArgs> PublishSignEditorOpenedAsync(Vector3<int> position, bool isFrontText)
    {
        var args = new SignEditorEventArgs(position, isFrontText);

        var handler = OnSignEditorOpened;
        if (handler is not null)
        {
            foreach (var subscriber in handler.GetInvocationList().Cast<Func<SignEditorEventArgs, Task>>())
            {
                await subscriber(args);
                if (args.Handled)
                    break;
            }
        }

        return args;
    }
}
