using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Serverbound;

[Packet(0x08, ProtocolState.Play)]
public class ChatCommandSignedPacket : IServerboundPacket
{
    [SetsRequiredMembers]
    public ChatCommandSignedPacket(string command)
    {
        Command = command;
        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        Salt = Random.Shared.NextInt64();
        ArgumentSignatures = [];
        MessageCount = 0;
        Acknowledged = [0, 0, 0];
        Checksum = 0;
    }

    public ChatCommandSignedPacket()
    {
    }

    public required string Command { get; init; }
    public long Timestamp { get; init; }
    public long Salt { get; init; }

    public List<ArgumentSignature> ArgumentSignatures { get; init; } = [];

    public int MessageCount { get; init; }
    public byte[] Acknowledged { get; init; } = [0, 0, 0];
    public byte Checksum { get; init; }

    public void Serialize(ref PacketBufferWriter buffer)
    {
        buffer.WriteString(Command);
        buffer.WriteSignedLong(Timestamp);
        buffer.WriteSignedLong(Salt);

        buffer.WriteVarInt(ArgumentSignatures.Count);
        foreach (var argSig in ArgumentSignatures)
        {
            buffer.WriteString(argSig.Name);
            buffer.WriteBuffer(argSig.Signature);
        }

        buffer.WriteVarInt(MessageCount);
        buffer.WriteBuffer(Acknowledged);
        buffer.WriteUnsignedByte(Checksum);
    }
}

public record ArgumentSignature(string Name, byte[] Signature);
