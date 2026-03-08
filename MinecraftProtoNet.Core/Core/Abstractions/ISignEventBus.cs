using MinecraftProtoNet.Core.Models.Core;

namespace MinecraftProtoNet.Core.Core.Abstractions;

/// <summary>
/// Event bus for sign editor events. Allows external systems (e.g., Bazaar trading)
/// to intercept and respond to sign editor open requests.
/// </summary>
public interface ISignEventBus
{
    event Func<SignEditorEventArgs, Task>? OnSignEditorOpened;
    Task<SignEditorEventArgs> PublishSignEditorOpenedAsync(Vector3<int> position, bool isFrontText, string[]? existingLines = null);
}

/// <summary>
/// Event args when the server requests the client to open a sign editor.
/// </summary>
public class SignEditorEventArgs(Vector3<int> position, bool isFrontText)
{
    public Vector3<int> Position { get; } = position;
    public bool IsFrontText { get; } = isFrontText;

    /// <summary>
    /// The existing text lines on the sign, read from block entity NBT data.
    /// </summary>
    public string[] ExistingLines { get; set; } = ["", "", "", ""];

    /// <summary>
    /// If set by a handler, the sign update will be sent with these lines
    /// instead of opening the sign editor GUI.
    /// </summary>
    public string[]? ResponseLines { get; set; }

    /// <summary>Whether a handler has claimed this event and will respond.</summary>
    public bool Handled { get; set; }
}
