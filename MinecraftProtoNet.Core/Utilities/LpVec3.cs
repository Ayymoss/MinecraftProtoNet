using MinecraftProtoNet.Core.Models.Core;

namespace MinecraftProtoNet.Core.Utilities;

/// <summary>
/// "Low precision" Vec3 wire format introduced in 26.x: a 48-bit packed triple of normalised components plus a
/// shared integer scale, so a short vector costs 6 bytes instead of 24.
///
/// Layout, least-significant byte first:
///   bits 0..1   scale, low two bits
///   bit  2      continuation flag — the rest of the scale follows the fixed part as a VarInt
///   bits 3..17  x, quantised to 15 bits over [-1, 1]
///   bits 18..32 y
///   bits 33..47 z
/// The all-zero first byte is the encoding of Vec3.ZERO and terminates the packet early.
///
/// Reference: minecraft-26.2-REFERENCE-ONLY/net/minecraft/network/LpVec3.java
/// </summary>
public static class LpVec3
{
    private const double MaxQuantizedValue = 32766.0;
    private const int ContinuationFlag = 4;
    public const double AbsMaxValue = 1.7179869183E10;
    public const double AbsMinValue = 3.051944088384301E-5;

    public static void Write(ref PacketBufferWriter buffer, Vector3<double> value)
    {
        var x = Sanitize(value.X);
        var y = Sanitize(value.Y);
        var z = Sanitize(value.Z);

        // Chessboard (L-infinity) length: the scale has to cover the largest component.
        var chessboardLength = AbsMax(x, AbsMax(y, z));
        if (chessboardLength < AbsMinValue)
        {
            buffer.WriteUnsignedByte(0);
            return;
        }

        var scale = CeilLong(chessboardLength);
        var isPartial = (scale & 3L) != scale;
        var markers = isPartial ? (scale & 3L) | ContinuationFlag : scale;
        var packed = markers
                     | (Pack(x / scale) << 3)
                     | (Pack(y / scale) << 18)
                     | (Pack(z / scale) << 33);

        buffer.WriteUnsignedByte((byte)packed);
        buffer.WriteUnsignedByte((byte)(packed >> 8));
        buffer.WriteSignedInt((int)(packed >> 16));
        if (isPartial)
        {
            buffer.WriteVarInt((int)(scale >> 2));
        }
    }

    public static Vector3<double> Read(ref PacketBufferReader buffer)
    {
        int lowest = buffer.ReadUnsignedByte();
        if (lowest == 0) return new Vector3<double>(0, 0, 0);

        int middle = buffer.ReadUnsignedByte();
        long highest = (uint)ReadBigEndianInt(ref buffer);
        var packed = (highest << 16) | ((long)(middle << 8)) | (long)lowest;

        long scale = lowest & 3;
        if ((lowest & ContinuationFlag) == ContinuationFlag)
        {
            scale |= ((long)buffer.ReadVarInt() & 4294967295L) << 2;
        }

        return new Vector3<double>(
            Unpack(packed >> 3) * scale,
            Unpack(packed >> 18) * scale,
            Unpack(packed >> 33) * scale);
    }

    private static int ReadBigEndianInt(ref PacketBufferReader buffer)
    {
        var bytes = buffer.ReadBytes(4);
        return (bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3];
    }

    private static double Sanitize(double value) =>
        double.IsNaN(value) ? 0.0 : Math.Clamp(value, -AbsMaxValue, AbsMaxValue);

    private static long Pack(double value) => (long)Math.Round((value * 0.5 + 0.5) * MaxQuantizedValue, MidpointRounding.AwayFromZero);

    private static double Unpack(long value) => Math.Min(value & 32767L, MaxQuantizedValue) * 2.0 / MaxQuantizedValue - 1.0;

    /// <summary>Reference: minecraft-26.2-REFERENCE-ONLY/net/minecraft/util/Mth.java — absMax</summary>
    private static double AbsMax(double a, double b) => Math.Max(Math.Abs(a), Math.Abs(b));

    /// <summary>Reference: minecraft-26.2-REFERENCE-ONLY/net/minecraft/util/Mth.java — ceilLong</summary>
    private static long CeilLong(double value)
    {
        var truncated = (long)value;
        return value > truncated ? truncated + 1L : truncated;
    }
}
