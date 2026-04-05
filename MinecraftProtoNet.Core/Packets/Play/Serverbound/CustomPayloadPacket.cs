using System.Text;
using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Serverbound;

// Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/network/protocol/common/ServerboundCustomPayloadPacket.java
[Packet(0x16, ProtocolState.Play)]
public class CustomPayloadPacket : IServerboundPacket
{
    public required string Channel { get; set; }
    public required byte[] Data { get; set; }

    public void Serialize(ref PacketBufferWriter buffer)
    {
        buffer.WriteString(Channel);
        buffer.WriteBuffer(Data);
    }

    /// <summary>
    /// Creates a minecraft:register packet with the given channel names.
    /// Format: null-byte (0x00) separated channel identifiers.
    /// </summary>
    public static CustomPayloadPacket CreateRegister(params string[] channels)
    {
        var payload = Encoding.UTF8.GetBytes(string.Join('\0', channels));
        return new CustomPayloadPacket
        {
            Channel = "minecraft:register",
            Data = payload
        };
    }
}
