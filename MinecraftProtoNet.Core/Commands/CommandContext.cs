using MinecraftProtoNet.Actions;
using MinecraftProtoNet.Auth.Dtos;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Core.Abstractions;
using MinecraftProtoNet.State;
using MinecraftProtoNet.State.Base;

namespace MinecraftProtoNet.Commands;

/// <summary>
/// Context for command execution, extending ActionContext with command-specific data.
/// </summary>
public class CommandContext(
    IMinecraftClient client,
    ClientState state,
    AuthResult authResult,
    Guid senderGuid,
    string[] arguments)
    : ActionContext(client, state, authResult)
{
    /// <summary>
    /// The arguments passed to the command (excludes the command name itself).
    /// </summary>
    public string[] Arguments { get; } = arguments ?? [];

    /// <summary>
    /// The UUID of the player who triggered the command.
    /// </summary>
    public Guid SenderGuid { get; } = senderGuid;

    /// <summary>
    /// The player who triggered the command (may be null if not found).
    /// </summary>
    public Player? Sender { get; } = state.Level.GetPlayerByUuid(senderGuid);

    /// <summary>
    /// Tries to get an argument at the specified index.
    /// </summary>
    public bool TryGetArg(int index, out string value)
    {
        if (index >= 0 && index < Arguments.Length)
        {
            value = Arguments[index];
            return true;
        }
        value = string.Empty;
        return false;
    }

    /// <summary>
    /// Tries to get and parse an argument at the specified index.
    /// </summary>
    public bool TryGetArg<T>(int index, out T value) where T : IParsable<T>
    {
        if (TryGetArg(index, out var strValue))
        {
            return T.TryParse(strValue, null, out value!);
        }
        value = default!;
        return false;
    }

    /// <summary>
    /// Gets all arguments starting from the specified index joined as a single string.
    /// </summary>
    public string GetRemainingArgsAsString(int startIndex)
    {
        if (startIndex >= Arguments.Length) return string.Empty;
        return string.Join(" ", Arguments.Skip(startIndex));
    }

    /// <summary>
    /// Gets all arguments starting from the specified index.
    /// </summary>
    public string[] GetRemainingArgs(int startIndex)
    {
        if (startIndex >= Arguments.Length) return [];
        return Arguments.Skip(startIndex).ToArray();
    }

    /// <summary>
    /// Checks if there are at least the specified number of arguments.
    /// </summary>
    public bool HasMinArgs(int count) => Arguments.Length >= count;
}
