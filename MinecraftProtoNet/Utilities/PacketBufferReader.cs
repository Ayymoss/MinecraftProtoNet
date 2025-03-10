using System.Buffers.Binary;
using System.Numerics;
using System.Text;
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

    public Guid ReadUUID()
    {
        var bytes = ReadBuffer(16);
        return new Guid(bytes); // Guid constructor directly handles big-endian
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

    public T[] ReadPrefixedArray<T>()
    {
        var length = ReadVarInt();
        var array = new T[length];

        for (var i = 0; i < length; i++)
        {
            array[i] = ReadObject<T>();
        }

        return array;
    }

    private T ReadObject<T>()
    {
        switch (typeof(T))
        {
            case var type when type == typeof(VarInt):
                return (T)(object)new VarInt(ReadVarInt());
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
            default: throw new NotSupportedException($"Unsupported type {typeof(T).Name}");
        }
    }

    public Vector3 ReadAsPosition()
    {
        var positionRaw = ReadSignedLong();
        var x = (float)(positionRaw >> 38);
        var y = (float)(positionRaw << 52 >> 52);
        var z = (float)(positionRaw << 26 >> 38);
        return new Vector3(x, y, z);
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
}

public readonly struct VarInt(int value)
{
    public int Value { get; } = value;
}
