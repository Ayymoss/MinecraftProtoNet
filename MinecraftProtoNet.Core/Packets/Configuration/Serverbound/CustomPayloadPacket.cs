using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Configuration.Serverbound;

// Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/network/protocol/common/ServerboundCustomPayloadPacket.java
// Reference: minecraft-26.1.1-REFERENCE-ONLY/net/minecraft/network/protocol/common/custom/BrandPayload.java
[Packet(0x02, ProtocolState.Configuration)]
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
    /// Creates a brand payload matching vanilla's BrandPayload format.
    /// The brand string is written as a VarInt-prefixed UTF-8 string.
    /// </summary>
    public static CustomPayloadPacket CreateBrand(string brand = "vanilla")
    {
        // BrandPayload.write() calls output.writeUtf(brand)
        // writeUtf = VarInt length + UTF-8 bytes
        var brandBytes = System.Text.Encoding.UTF8.GetBytes(brand);
        using var ms = new System.IO.MemoryStream();
        // Write VarInt length
        var value = brandBytes.Length;
        while (value > 0x7F)
        {
            ms.WriteByte((byte)(value & 0x7F | 0x80));
            value >>= 7;
        }
        ms.WriteByte((byte)value);
        ms.Write(brandBytes);

        return new CustomPayloadPacket
        {
            Channel = "minecraft:brand",
            Data = ms.ToArray()
        };
    }
}
