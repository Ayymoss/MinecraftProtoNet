using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Models.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Serverbound;

/// <summary>
/// Client sends edited sign text back to server.
/// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/network/protocol/game/ServerboundSignUpdatePacket.java
/// </summary>
[Packet(0x3D, ProtocolState.Play)]
public class SignUpdatePacket : IServerboundPacket
{
    public required Vector3<int> Position { get; set; }
    public bool IsFrontText { get; set; } = true;
    public required string[] Lines { get; set; }

    public void Serialize(ref PacketBufferWriter buffer)
    {
        buffer.WriteBlockPos(Position);
        buffer.WriteBoolean(IsFrontText);

        for (var i = 0; i < 4; i++)
        {
            // Deliberately NOT clamped here — see MinecraftFont.TypedSignLine and the note below.
            //
            // Vanilla's width filter applies to the line the player is TYPING, not to the packet: the edit
            // screen loads all four lines from the sign and sends back untouched ones exactly as they were
            // (AbstractSignEditScreen.java:58 filters the text field; the other lines are never re-filtered).
            // Clamping every line here would rewrite text the server itself put on the sign, which is its own
            // divergence — in the opposite direction. Authored text is clamped at the point it is authored.
            buffer.WriteString(i < Lines.Length ? Lines[i] : "");
        }
    }
}
