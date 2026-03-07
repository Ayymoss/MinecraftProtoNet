using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Models.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Clientbound;

/// <summary>
/// Server tells client to open the sign editor GUI.
/// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/network/protocol/game/ClientboundOpenSignEditorPacket.java
/// </summary>
[Packet(0x3C, ProtocolState.Play)]
public class OpenSignEditorPacket : IClientboundPacket
{
    public Vector3<int> Position { get; set; }
    public bool IsFrontText { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        Position = buffer.ReadBlockPos();
        IsFrontText = buffer.ReadBoolean();
    }
}
