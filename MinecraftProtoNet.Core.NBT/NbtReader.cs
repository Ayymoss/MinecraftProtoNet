using System.Buffers.Binary;
using System.Text;
using MinecraftProtoNet.Core.NBT.Enums;
using MinecraftProtoNet.Core.NBT.Tags;
using MinecraftProtoNet.Core.NBT.Tags.Abstract;
using MinecraftProtoNet.Core.NBT.Tags.Primitive;

namespace MinecraftProtoNet.Core.NBT;

public ref struct NbtReader(ReadOnlySpan<byte> bytes)
{
    private readonly ReadOnlySpan<byte> _buffer = bytes;
    private int _readPosition = 0;
    public int ConsumedBytes => _readPosition;

    public NbtTag? ReadNbtTag()
    {
        var result = ReadRecursive();
        return result;
    }

    private NbtTag? ReadRecursive(NbtTagType? parentTagType = null, NbtTagType? nextTagType = null)
    {
        var tagType = nextTagType ?? ReadTagType();
        if (tagType is NbtTagType.End) return new NbtEnd();

        var rootName = parentTagType != null && parentTagType != NbtTagType.List ? ReadString() : null;
        if (IsTypePrimitive(tagType)) return ReadPrimitive(tagType, rootName);

        switch (tagType)
        {
            case NbtTagType.List:
            {
                var listTagType = ReadTagType();
                var listLength = BinaryPrimitives.ReadInt32BigEndian(ReadBytes(sizeof(int)));
                if (listLength <= 0 || listTagType is NbtTagType.End) return new NbtList(rootName, listTagType);

                var list = new NbtList(rootName, listTagType);
                for (var i = 0; i < listLength; i++)
                {
                    var innerTag = ReadRecursive(tagType, listTagType);
                    if (innerTag is not null or NbtEnd) list.Value.Add(innerTag);
                }

                return list;
            }
            case NbtTagType.Compound:
            {
                var compound = new NbtCompound(rootName);
                while (true)
                {
                    var innerTag = ReadRecursive(tagType);
                    if (innerTag is null or NbtEnd) break;
                    compound.Value.Add(innerTag);
                }

                return compound;
            }
        }

        return null;
    }

    private NbtTag ReadPrimitive(NbtTagType tagType, string? rootName)
    {
        return tagType switch
        {
            // @formatter:off
            NbtTagType.End => new NbtEnd(),
            NbtTagType.Byte => new NbtByte(rootName, ReadByte()),
            NbtTagType.Short => new NbtShort(rootName, BinaryPrimitives.ReadInt16BigEndian(ReadBytes(sizeof(short)))),
            NbtTagType.Int => new NbtInt(rootName, BinaryPrimitives.ReadInt32BigEndian(ReadBytes(sizeof(int)))),
            NbtTagType.Long => new NbtLong(rootName, BinaryPrimitives.ReadInt64BigEndian(ReadBytes(sizeof(long)))),
            NbtTagType.Float => new NbtFloat(rootName, BinaryPrimitives.ReadSingleBigEndian(ReadBytes(sizeof(float)))),
            NbtTagType.Double => new NbtDouble(rootName, BinaryPrimitives.ReadDoubleBigEndian(ReadBytes(sizeof(double)))),
            NbtTagType.ByteArray => new NbtByteArray(rootName, ReadBytes(BinaryPrimitives.ReadInt32BigEndian(ReadBytes(sizeof(int)))).ToArray()),
            NbtTagType.String => new NbtString(rootName, ReadString()),
            NbtTagType.IntArray => new NbtIntArray(rootName, ReadIntArray()),
            NbtTagType.LongArray => new NbtLongArray(rootName, ReadLongArray()),
            _ => throw new ArgumentOutOfRangeException(nameof(tagType), tagType, null)
            // @formatter:on
        };
    }

    private long[] ReadLongArray()
    {
        var length = BinaryPrimitives.ReadInt32BigEndian(ReadBytes(sizeof(int)));
        if (length is 0) throw new ArgumentOutOfRangeException(nameof(length));

        var array = new long[length];
        for (var i = 0; i < length; i++)
        {
            array[i] = BinaryPrimitives.ReadInt64BigEndian(ReadBytes(sizeof(long)));
        }

        return array;
    }

    private int[] ReadIntArray()
    {
        var length = BinaryPrimitives.ReadInt32BigEndian(ReadBytes(sizeof(int)));
        if (length is 0) throw new ArgumentOutOfRangeException(nameof(length));

        var array = new int[length];
        for (var i = 0; i < length; i++)
        {
            array[i] = BinaryPrimitives.ReadInt32BigEndian(ReadBytes(sizeof(int)));
        }

        return array;
    }

    private bool IsTypePrimitive(NbtTagType tag)
    {
        switch (tag)
        {
            case NbtTagType.List:
            case NbtTagType.Compound:
                return false;
            default:
                return true;
        }
    }

    private string ReadString()
    {
        var length = BinaryPrimitives.ReadUInt16BigEndian(ReadBytes(sizeof(ushort)));
        var span = ReadBytes(length);
        return Encoding.UTF8.GetString(span);
    }

    private NbtTagType ReadTagType()
    {
        var tag = (NbtTagType)ReadByte();
        return tag;
    }

    private byte ReadByte()
    {
        if (_readPosition >= _buffer.Length)
        {
            throw new IndexOutOfRangeException();
        }

        return _buffer[_readPosition++];
    }

    private ReadOnlySpan<byte> ReadBytes(int length)
    {
        if (_readPosition + length > _buffer.Length)
        {
            throw new IndexOutOfRangeException($"Length: {length.ToString()}");
        }

        var span = _buffer.Slice(_readPosition, length);
        _readPosition += length;
        return span;
    }
}
