using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Serverbound;

[Packet(0x2B, ProtocolState.Play)]
public class PlayerInputPacket(PlayerInputPacket.MovementFlag flag) : IServerboundPacket
{
    /// <summary>
    /// The movement flags being sent to the server.
    /// Exposed as a property so the packet logger can display it.
    /// </summary>
    public MovementFlag Flags => flag;
    
    public void Serialize(ref PacketBufferWriter buffer)
    {
        buffer.WriteUnsignedByte((byte)flag);
    }
    
    public override string ToString()
    {
        return $"Flags: {flag} (0x{(byte)flag:X2})";
    }

    [Flags]
    public enum MovementFlag : byte
    {
        None = 0x00,
        Forward = 0x01,
        Backward = 0x02,
        Left = 0x04,
        Right = 0x08,
        Jump = 0x10,
        Sneak = 0x20,
        Sprint = 0x40,
    }
}
