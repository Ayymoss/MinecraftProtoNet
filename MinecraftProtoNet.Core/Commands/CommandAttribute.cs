namespace MinecraftProtoNet.Core.Commands;

/// <summary>
/// Attribute for marking and configuring command classes for auto-registration.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class CommandAttribute(string name) : Attribute
{
    /// <summary>
    /// The primary name of the command (without the ! prefix).
    /// </summary>
    public string Name { get; } = name ?? throw new ArgumentNullException(nameof(name));

    /// <summary>
    /// A brief description of what the command does.
    /// </summary>
    public string Description { get; set; } = "";

    /// <summary>
    /// Alternative names for the command.
    /// </summary>
    public string[] Aliases { get; set; } = [];

    /// <summary>
    /// Whether this command requires an in-game player context (e.g., sender position, target entity).
    /// Commands with this set to true cannot be executed from external sources like web UIs.
    /// Default: false (can be executed from anywhere).
    /// </summary>
    public bool PlayerContextRequired { get; set; }
}
