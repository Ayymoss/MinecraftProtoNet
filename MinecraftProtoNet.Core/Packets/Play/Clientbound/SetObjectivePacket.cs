using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Clientbound;

/// <summary>
/// Creates, updates, or removes a scoreboard objective.
/// Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/network/protocol/game/ClientboundSetObjectivePacket.java
/// </summary>
[Packet(0x6A, ProtocolState.Play, silent: false)]
public class SetObjectivePacket : IClientboundPacket
{
    public string ObjectiveName { get; set; } = string.Empty;
    public byte Method { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        ObjectiveName = buffer.ReadString();
        Method = buffer.ReadUnsignedByte();
        // Method 0 (create) and 2 (update) have additional fields:
        // DisplayName (chat component), RenderType (VarInt), NumberFormat (optional)
        // We consume the rest since we don't need scoreboard data.
        if (Method is 0 or 2)
        {
            buffer.ReadRestBuffer();
        }
    }
}
