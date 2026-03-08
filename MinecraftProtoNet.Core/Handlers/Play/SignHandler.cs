using Microsoft.Extensions.Logging;
using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Core.Abstractions;
using MinecraftProtoNet.Core.Handlers.Base;
using MinecraftProtoNet.Core.NBT;
using MinecraftProtoNet.Core.NBT.Tags;
using MinecraftProtoNet.Core.NBT.Tags.Abstract;
using MinecraftProtoNet.Core.NBT.Tags.Primitive;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Services;
using MinecraftProtoNet.Core.Packets.Play.Clientbound;
using MinecraftProtoNet.Core.Packets.Play.Serverbound;

namespace MinecraftProtoNet.Core.Handlers.Play;

/// <summary>
/// Handles sign editor packets. Publishes events via ISignEventBus so external
/// systems (e.g., Bazaar trading) can auto-respond to sign editor prompts.
/// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/network/protocol/game/ClientboundOpenSignEditorPacket.java
/// </summary>
[HandlesPacket(typeof(OpenSignEditorPacket))]
public class SignHandler(ILogger<SignHandler> logger, ISignEventBus signEventBus) : IPacketHandler
{
    public IEnumerable<(ProtocolState State, int PacketId)> RegisteredPackets =>
        PacketRegistry.GetHandlerRegistrations(typeof(SignHandler));

    public async Task HandleAsync(IClientboundPacket packet, IMinecraftClient client)
    {
        if (packet is not OpenSignEditorPacket signPacket)
            return;

        logger.LogInformation("Sign editor opened at {Position} (front={IsFront})",
            signPacket.Position, signPacket.IsFrontText);

        // Read existing sign text from block entity NBT before notifying subscribers
        var existingLines = ReadSignLines(client, signPacket.Position, signPacket.IsFrontText);

        // Notify subscribers with existing lines available
        var args = await signEventBus.PublishSignEditorOpenedAsync(
            signPacket.Position, signPacket.IsFrontText, existingLines);

        // If a subscriber claimed the event and provided response lines, auto-send
        if (args.Handled && args.ResponseLines is not null)
        {
            logger.LogInformation("Auto-responding to sign editor with: {Lines}",
                string.Join(" | ", args.ResponseLines));

            await client.SendPacketAsync(new SignUpdatePacket
            {
                Position = signPacket.Position,
                IsFrontText = signPacket.IsFrontText,
                Lines = args.ResponseLines
            });
        }
    }

    /// <summary>
    /// Reads existing sign text lines from block entity NBT data.
    /// Reference: SignBlockEntity.java — front_text/back_text → messages[] → Component with "text" field
    /// </summary>
    private static string[] ReadSignLines(IMinecraftClient client, Models.Core.Vector3<int> position, bool isFrontText)
    {
        var lines = new string[4];
        var nbt = client.State.Level.GetBlockEntity(position);
        if (nbt is null) return lines;

        // Sign NBT: { front_text: { messages: [...] }, back_text: { messages: [...] } }
        var sideKey = isFrontText ? "front_text" : "back_text";
        var sideTag = nbt.FindTag<NbtCompound>(sideKey);
        if (sideTag is null) return lines;

        // Messages is a list of text components
        var messagesList = sideTag.FindTag<NbtList>("messages");
        if (messagesList is null) return lines;

        for (var i = 0; i < Math.Min(4, messagesList.Value.Count); i++)
        {
            var messageTag = messagesList.Value[i];

            // Each message is a text component — could be a string or compound
            if (messageTag is NbtString directString)
            {
                // Plain string (may be JSON text component as string)
                var text = directString.Value;
                if (text.StartsWith('{'))
                {
                    // JSON text component encoded as string — extract "text" field
                    // Simple extraction without full JSON parse
                    var textMatch = System.Text.RegularExpressions.Regex.Match(text, "\"text\"\\s*:\\s*\"([^\"]*)\"");
                    lines[i] = textMatch.Success ? textMatch.Groups[1].Value : "";
                }
                else
                {
                    lines[i] = text;
                }
            }
            else if (messageTag is NbtCompound compound)
            {
                // Direct NBT compound text component
                var textTag = compound.FindTag<NbtString>("text");
                lines[i] = textTag?.Value ?? "";
            }
        }

        return lines;
    }
}
