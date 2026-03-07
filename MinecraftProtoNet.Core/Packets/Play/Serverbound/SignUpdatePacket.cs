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
            buffer.WriteString(i < Lines.Length ? Lines[i] : "");
        }
    }
}
