using FluentAssertions;
using MinecraftProtoNet.Core.Enums;
using MinecraftProtoNet.Core.Models.Core;
using MinecraftProtoNet.Core.Packets.Play.Serverbound;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Tests.Core.Packets;

/// <summary>
/// The 26.x Interact packet and the packed Vec3 it carries.
///
/// These exist because the pre-26 layout (an action enum, then three raw floats) decodes as garbage on a 26.x
/// server: the action byte lands where the hand is read, and only 0 and 1 are valid hands. The server throws
/// inside its decoder and drops the connection — on Hypixel that arrived as "A disconnect occurred in your
/// connection, so you were put in the SkyBlock Lobby!" roughly 100 ms after every single right-click on an NPC.
///
/// Reference: minecraft-26.2-REFERENCE-ONLY/net/minecraft/network/protocol/game/ServerboundInteractPacket.java
/// Reference: minecraft-26.2-REFERENCE-ONLY/net/minecraft/network/LpVec3.java
/// </summary>
public sealed class InteractPacketTests
{
    /// <summary>The writer is a ref struct, so it has to be passed by ref or every write lands on a copy.</summary>
    private delegate void WriteAction(ref PacketBufferWriter writer);

    private static byte[] Serialize(WriteAction write)
    {
        var writer = new PacketBufferWriter();
        write(ref writer);
        return writer.WrittenSpan.ToArray();
    }

    [Fact]
    public void LpVec3_Zero_IsASingleZeroByte()
    {
        // Vanilla short-circuits anything below ABS_MIN_VALUE to one zero byte, and the reader stops there.
        var bytes = Serialize((ref PacketBufferWriter w) => LpVec3.Write(ref w, new Vector3<double>(0, 0, 0)));
        bytes.Should().Equal((byte)0);
    }

    [Fact]
    public void LpVec3_SmallVector_IsSixBytes()
    {
        // Scale fits in the two marker bits, so no continuation VarInt follows the fixed 6-byte body.
        var bytes = Serialize((ref PacketBufferWriter w) => LpVec3.Write(ref w, new Vector3<double>(0, 1.0, 0)));
        bytes.Should().HaveCount(6);
        (bytes[0] & 4).Should().Be(0, "a scale of 1 needs no continuation");
    }

    [Fact]
    public void LpVec3_LargeVector_SetsContinuationBitAndAppendsScale()
    {
        var bytes = Serialize((ref PacketBufferWriter w) => LpVec3.Write(ref w, new Vector3<double>(9.5, -3.25, 0.5)));
        bytes.Should().HaveCountGreaterThan(6);
        (bytes[0] & 4).Should().Be(4, "a scale above 3 spills into the trailing VarInt");
    }

    [Theory]
    [InlineData(0.0, 1.0, 0.0)]
    [InlineData(0.3, 0.9, -0.45)]
    [InlineData(-0.5, 1.62, 0.5)]
    [InlineData(9.5, -3.25, 0.5)]
    [InlineData(-120.75, 64.0, 300.125)]
    public void LpVec3_RoundTrips_WithinQuantisationError(double x, double y, double z)
    {
        var bytes = Serialize((ref PacketBufferWriter w) => LpVec3.Write(ref w, new Vector3<double>(x, y, z)));
        var reader = new PacketBufferReader(bytes);
        var decoded = LpVec3.Read(ref reader);

        // 15 bits spread over [-scale, +scale], so a component is good to scale/32766 either way. The scale is
        // ceil of the largest component, which is what bounds the error for the whole vector.
        var scale = Math.Ceiling(Math.Max(Math.Abs(x), Math.Max(Math.Abs(y), Math.Abs(z))));
        var tolerance = scale / 32766.0 * 2;

        decoded.X.Should().BeApproximately(x, tolerance);
        decoded.Y.Should().BeApproximately(y, tolerance);
        decoded.Z.Should().BeApproximately(z, tolerance);
        reader.ReadableBytes.Should().Be(0, "the reader must consume exactly what the writer produced");
    }

    [Fact]
    public void Serialize_MatchesTheFieldOrderOfTheStreamCodec()
    {
        // entityId (VarInt), hand (VarInt), location (LpVec3), usingSecondaryAction (bool).
        var packet = new InteractPacket
        {
            EntityId = 410,
            Hand = Hand.OffHand,
            Location = new Vector3<double>(0, 1.0, 0),
            SneakKeyPressed = true
        };

        var bytes = Serialize((ref PacketBufferWriter w) => packet.Serialize(ref w));
        var reader = new PacketBufferReader(bytes);

        reader.ReadVarInt().Should().Be(410);
        reader.ReadVarInt().Should().Be((int)Hand.OffHand);
        var location = LpVec3.Read(ref reader);
        location.Y.Should().BeApproximately(1.0, 0.001);
        reader.ReadBoolean().Should().BeTrue();
        reader.ReadableBytes.Should().Be(0);
    }

    [Fact]
    public void AttackPacket_CarriesOnlyTheTargetId()
    {
        // Attack was split out of Interact in 26.x and lost every other field.
        var bytes = Serialize((ref PacketBufferWriter w) => new AttackPacket { EntityId = 7 }.Serialize(ref w));
        bytes.Should().Equal((byte)7);
    }
}
