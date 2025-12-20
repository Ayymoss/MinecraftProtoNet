using System.Buffers.Binary;
using System.Collections;
using System.Diagnostics.Contracts;
using System.Text;
using MinecraftProtoNet.Models.Core;
using MinecraftProtoNet.Models.World.Meta;
using MinecraftProtoNet.NBT;
using MinecraftProtoNet.NBT.Tags;
using MinecraftProtoNet.NBT.Tags.Abstract;

namespace MinecraftProtoNet.Utilities;

public ref struct PacketBufferReader(ReadOnlySpan<byte> bytes)
{
    private readonly ReadOnlySpan<byte> _buffer = bytes;
    public int ReadPosition { get; private set; } = 0;
    public int ReadableBytes => _buffer.Length - ReadPosition;
    public ReadOnlySpan<byte> GetReadableSpan() => _buffer[ReadPosition..];

    public int ReadVarInt()
    {
        var bytesRead = 0;
        var result = 0;
        byte read;

        do
        {
            read = _buffer[ReadPosition + bytesRead];
            var value = read & 127;
            result |= value << (7 * bytesRead);

            bytesRead++;
            if (bytesRead > 5) throw new ArithmeticException("VarInt too long");
        } while ((read & 0b10000000) != 0);

        ReadPosition += bytesRead;
        return result;
    }

    public long ReadVarLong()
    {
        long value = 0;
        var shift = 0;
        var bytesRead = 0;

        byte byteRead;
        do
        {
            if (ReadPosition + bytesRead >= _buffer.Length)
            {
                throw new InvalidOperationException("VarLong is too long");
            }

            byteRead = _buffer[ReadPosition + bytesRead];
            value |= (long)(byteRead & 0x7F) << shift;
            shift += 7;
            bytesRead++;
        } while ((byteRead & 0x80) != 0);

        ReadPosition += bytesRead;
        return value;
    }

    public string ReadString()
    {
        var length = ReadVarInt();
        var span = ReadBytes(length);
        var str = Encoding.UTF8.GetString(span);
        return str;
    }

    public ReadOnlySpan<byte> ReadRestBuffer()
    {
        var bytes = _buffer[ReadPosition..];
        ReadPosition = _buffer.Length;
        return bytes;
    }

    public ReadOnlySpan<byte> ReadBytes(int length)
    {
        if (ReadPosition + length > _buffer.Length)
        {
            throw new IndexOutOfRangeException();
        }

        var bytes = _buffer.Slice(ReadPosition, length);
        ReadPosition += length;
        return bytes;
    }

    public sbyte ReadSignedByte()
    {
        var b = _buffer[ReadPosition];
        ReadPosition++;
        return (sbyte)b;
    }

    public byte ReadUnsignedByte()
    {
        var b = _buffer[ReadPosition];
        ReadPosition++;
        return b;
    }

    public ushort ReadUnsignedShort()
    {
        var value = BinaryPrimitives.ReadUInt16BigEndian(_buffer[ReadPosition..]);
        ReadPosition += sizeof(ushort);
        return value;
    }

    public BitArray ReadFixedBitSet(int length)
    {
        var bytesNeeded = (length + 7) / 8;
        var bytes = ReadBytes(bytesNeeded);
        var bitArray = new BitArray(bytes.ToArray());

        if (bitArray.Length <= length) return bitArray;
        var result = new BitArray(length);
        for (var i = 0; i < length; i++)
        {
            result[i] = bitArray[i];
        }

        return result;
    }

    public HashSet<TEnum> ReadEnumSet<TEnum>() where TEnum : Enum
    {
        var values = Enum.GetValues(typeof(TEnum)).Cast<TEnum>().ToArray();

        var bitSet = ReadFixedBitSet(values.Length);
        HashSet<TEnum> readValues = [];

        for (var i = 0; i < values.Length; i++)
        {
            if (bitSet[i])
            {
                readValues.Add(values[i]);
            }
        }

        return readValues;
    }

    public ReadOnlySpan<byte> ReadBuffer(int count)
    {
        if (count > ReadableBytes) throw new ArgumentOutOfRangeException(nameof(count), "Cannot read beyond the readable bytes.");

        var bytes = _buffer.Slice(ReadPosition, count);
        ReadPosition += count;
        return bytes;
    }

    public long[] ReadBitSet()
    {
        var length = ReadVarInt();
        var bitSet = new long[length];
        for (var i = 0; i < length; i++)
        {
            bitSet[i] = ReadSignedLong();
        }

        return bitSet;
    }

    public Guid ReadUuid()
    {
        var bytes = ReadBuffer(16);
        return new Guid(bytes, true);
    }

    public bool ReadBoolean()
    {
        var b = ReadUnsignedByte();
        var state = b is 1;
        return state;
    }

    public NbtTag? ReadOptionalNbtTag() => ReadBoolean() ? ReadNbtTag() : null;

    public NbtTag ReadNbtTag()
    {
        var reader = new NbtReader(_buffer[ReadPosition..]);
        var tag = reader.ReadNbtTag();
        ReadPosition += reader.ConsumedBytes;
        return tag ?? new NbtEnd();
    }

    public ChunkBlockEntityInfo ReadChunkBlockEntity()
    {
        var packed = ReadUnsignedByte();
        var x = packed >> 0x4;
        var z = packed & 0xF;
        var y = ReadSignedShort();
        var type = ReadVarInt();
        var nbtData = ReadNbtTag();

        return new ChunkBlockEntityInfo((byte)x, y, (byte)z, type, nbtData);
    }

    // TODO: Can't use func within struct this way... Find another solution.
    public T[] ReadPrefixedArray<T>(Func<T>? customReader = null)
    {
        var length = ReadVarInt();
        var array = new T[length];

        for (var i = 0; i < length; i++)
        {
            if (customReader is not null) array[i] = customReader();
            else array[i] = ReadObject<T>();
        }

        return array;
    }

    private T ReadObject<T>()
    {
        switch (typeof(T))
        {
            case var type when type == typeof(VarInt):
                return (T)(object)new VarInt(ReadVarInt());
            case var type when type == typeof(VarLong):
                return (T)(object)new VarLong(ReadVarLong());
            case var type when type == typeof(string):
                return (T)(object)ReadString();
            case var type when type == typeof(byte):
                return (T)(object)ReadUnsignedByte();
            case var type when type == typeof(long):
                return (T)(object)ReadSignedLong();
            case var type when type == typeof(byte[]):
                return (T)(object)ReadBuffer(ReadVarInt()).ToArray();
            case var type when type == typeof(ChunkBlockEntityInfo):
                return (T)(object)ReadChunkBlockEntity();
            case var type when type == typeof(Guid):
                return (T)(object)ReadUuid();
            default: throw new NotSupportedException($"Unsupported type {typeof(T).Name}");
        }
    }

    public Vector3<double> ReadCoordinatePosition()
    {
        var positionRaw = ReadSignedLong();
        var x = (double)(positionRaw >> 38);
        var y = (double)(positionRaw << 52 >> 52);
        var z = (double)(positionRaw << 26 >> 38);
        return new Vector3<double>(x, y, z);
    }

    public Vector3<float> ReadChunkCoordinatePosition()
    {
        var positionRaw = ReadSignedLong();
        var x = (float)(positionRaw >> 42);
        var y = (float)(positionRaw << 44 >> 44);
        var z = (float)(positionRaw << 22 >> 42);
        return new Vector3<float>(x, y, z);
    }

    public long ReadSignedLong()
    {
        try
        {
            var value = BinaryPrimitives.ReadInt64BigEndian(_buffer[ReadPosition..]);
            ReadPosition += sizeof(long);
            return value;
        }
        catch (ArgumentOutOfRangeException e)
        {
            Console.WriteLine();
            throw;
        }
    }

    public short ReadSignedShort()
    {
        var value = BinaryPrimitives.ReadInt16BigEndian(_buffer[ReadPosition..]);
        ReadPosition += sizeof(short);
        return value;
    }

    public int ReadSignedInt()
    {
        var value = BinaryPrimitives.ReadInt32BigEndian(_buffer[ReadPosition..]);
        ReadPosition += sizeof(int);
        return value;
    }

    public float ReadFloat()
    {
        var value = BinaryPrimitives.ReadSingleBigEndian(_buffer[ReadPosition..]);
        ReadPosition += sizeof(float);
        return value;
    }

    public double ReadDouble()
    {
        var value = BinaryPrimitives.ReadDoubleBigEndian(_buffer[ReadPosition..]);
        ReadPosition += sizeof(double);
        return value;
    }

    public uint ReadUnsignedInt()
    {
        var value = BinaryPrimitives.ReadUInt32BigEndian(_buffer[ReadPosition..]);
        ReadPosition += sizeof(uint);
        return value;
    }

    /// <summary>
    /// Reads a low-precision Vec3 (LpVec3) format used for entity motion in MC 26.1+.
    /// This is a compressed format that encodes a Vec3 in 1-7 bytes.
    /// </summary>
    public Vector3<double> ReadLpVec3()
    {
        const int DataBitsMask = 32767; // 15 bits
        const double MaxQuantizedValue = 32766.0;
        const int ContinuationFlag = 4;

        var lowest = ReadUnsignedByte();
        if (lowest == 0)
        {
            return new Vector3<double>(0, 0, 0);
        }

        var middle = ReadUnsignedByte();
        var highest = ReadUnsignedInt();

        // Combine into 48-bit buffer
        long buffer = ((long)highest << 16) | ((long)middle << 8) | lowest;

        // Extract scale from lowest 2 bits, with optional continuation
        long scale = lowest & 3;
        if ((lowest & ContinuationFlag) == ContinuationFlag)
        {
            scale |= ((long)ReadVarInt() & 0xFFFFFFFFL) << 2;
        }

        // Unpack X, Y, Z from bit positions 3, 18, 33 (15 bits each)
        var x = Unpack(buffer >> 3) * scale;
        var y = Unpack(buffer >> 18) * scale;
        var z = Unpack(buffer >> 33) * scale;

        return new Vector3<double>(x, y, z);

        static double Unpack(long value)
        {
            return Math.Min(value & DataBitsMask, MaxQuantizedValue) * 2.0 / MaxQuantizedValue - 1.0;
        }
    }

    [Pure]
    public static int ReadVarInt(ReadOnlySpan<byte> buffer, out int bytesRead)
    {
        bytesRead = 0;
        var result = 0;
        byte read;

        do
        {
            read = buffer[bytesRead];
            var value = read & 127;
            result |= value << (7 * bytesRead);

            bytesRead++;
            if (bytesRead > 5) throw new ArithmeticException("VarInt too long");
        } while ((read & 0b10000000) != 0);

        return result;
    }

    [Pure]
    public static bool TryReadVarInt(byte[] buffer, out int value, out int bytesRead)
    {
        try
        {
            value = ReadVarInt(buffer, out bytesRead);
            return true;
        }
        catch (Exception)
        {
            value = 0;
            bytesRead = 0;
            return false;
        }
    }
}

public readonly struct VarInt(int value)
{
    public int Value { get; } = value;
}

public readonly struct VarLong(long value)
{
    public long Value { get; } = value;
}
