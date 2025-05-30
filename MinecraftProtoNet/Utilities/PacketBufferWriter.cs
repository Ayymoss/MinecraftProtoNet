﻿using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics.Contracts;
using System.Text;
using MinecraftProtoNet.Models.Core;

namespace MinecraftProtoNet.Utilities;

public ref struct PacketBufferWriter
{
    private byte[]? _array;
    private Span<byte> _buffer;
    private int _writePosition;

    public PacketBufferWriter(int initialCapacity = 256)
    {
        _array = ArrayPool<byte>.Shared.Rent(initialCapacity);
        _buffer = _array;
        _writePosition = 0;
    }

    public int BytesWritten => _writePosition;
    public ReadOnlySpan<byte> WrittenSpan => _buffer[.._writePosition];

    public void WriteVarInt(int value)
    {
        EnsureCapacity(5);
        var data = _buffer.Slice(_writePosition, 5);

        var unsigned = (uint)value;
        byte len = 0;

        do
        {
            var temp = (byte)(unsigned & 127);
            unsigned >>= 7;
            if (unsigned != 0)
            {
                temp |= 128;
            }

            data[len++] = temp;
        } while (unsigned != 0);

        _writePosition += len;
    }

    public void WriteVarLong(long value)
    {
        EnsureCapacity(10);
        var buffer = _buffer.Slice(_writePosition, 10);
        var bytesWritten = 0;

        do
        {
            var temp = (byte)(value & 0x7F);
            value >>>= 7;
            if (value != 0)
            {
                temp |= 0x80;
            }

            buffer[bytesWritten++] = temp;
        } while (value != 0);

        _writePosition += bytesWritten;
    }

    public void WriteString(string value)
    {
        var maxLength = Encoding.UTF8.GetByteCount(value);
        EnsureCapacity(GetVarIntSize(maxLength) + maxLength);

        var stringLength = Encoding.UTF8.GetBytes(value, _buffer[(_writePosition + GetVarIntSize(maxLength))..]);
        WriteVarInt(stringLength);

        _writePosition += stringLength;
    }

    public void WriteUnsignedShort(ushort value)
    {
        EnsureCapacity(sizeof(ushort));
        BinaryPrimitives.WriteUInt16BigEndian(_buffer[_writePosition..], value);
        _writePosition += sizeof(ushort);
    }

    public void WriteBuffer(ReadOnlySpan<byte> bytes)
    {
        EnsureCapacity(bytes.Length);
        bytes.CopyTo(_buffer[_writePosition..]);
        _writePosition += bytes.Length;
    }

    public void WriteUnsignedByte(byte value)
    {
        EnsureCapacity(sizeof(byte));
        _buffer[_writePosition] = value;
        _writePosition++;
    }

    public void WriteUUID(Guid uuid)
    {
        EnsureCapacity(16);
        if (!uuid.TryWriteBytes(_buffer[_writePosition..]))
        {
            throw new InvalidOperationException("Not enough space to write UUID.");
        }

        _writePosition += 16;
    }

    public void WriteSignedLong(long payload)
    {
        EnsureCapacity(sizeof(long));
        BinaryPrimitives.WriteInt64BigEndian(_buffer[_writePosition..], payload);
        _writePosition += sizeof(long);
    }

    public void WriteBoolean(bool value)
    {
        WriteUnsignedByte(value ? (byte)1 : (byte)0);
    }

    private static int GetVarIntSize(int value)
    {
        var size = 0;
        do
        {
            value >>>= 7;
            size++;
        } while (value != 0);

        return size;
    }

    private static int GetVarLongSize(long value)
    {
        var size = 0;
        do
        {
            value >>>= 7;
            size++;
        } while (value != 0);

        return size;
    }

    private void EnsureCapacity(int required)
    {
        if (_writePosition + required > _buffer.Length)
        {
            ExpandBuffer(required);
        }
    }

    private void ExpandBuffer(int required)
    {
        var newSize = Math.Max((_array?.Length ?? 0) * 2, _writePosition + required);
        var newArray = ArrayPool<byte>.Shared.Rent(newSize);

        if (_array != null)
        {
            _buffer[.._writePosition].CopyTo(newArray);
            ArrayPool<byte>.Shared.Return(_array);
        }

        _array = newArray;
        _buffer = newArray;
    }

    public void Dispose()
    {
        if (_array == null) return;
        ArrayPool<byte>.Shared.Return(_array);
        _array = null;
        _buffer = default;
        _writePosition = 0;
    }

    public byte[] ToArray()
    {
        return _buffer[.._writePosition].ToArray();
    }

    public void WritePosition(Vector3<double> position)
    {
        var x = (long)Math.Floor(position.X);
        var y = (long)Math.Floor(position.Y);
        var z = (long)Math.Floor(position.Z);

        x &= 0x3FFFFFF;
        y &= 0xFFF;
        z &= 0x3FFFFFF;

        var packed = (x << 38) | (z << 12) | y;
        WriteSignedLong(packed);
    }

    public void WriteFloat(float value)
    {
        EnsureCapacity(sizeof(float));
        BinaryPrimitives.WriteSingleBigEndian(_buffer[_writePosition..], value);
        _writePosition += sizeof(float);
    }

    public void WriteDouble(double value)
    {
        EnsureCapacity(sizeof(double));
        BinaryPrimitives.WriteDoubleBigEndian(_buffer[_writePosition..], value);
        _writePosition += sizeof(double);
    }

    public void WriteSignedInt(int payload)
    {
        EnsureCapacity(sizeof(int));
        BinaryPrimitives.WriteInt32BigEndian(_buffer[_writePosition..], payload);
        _writePosition += sizeof(int);
    }

    public void WriteSignedShort(short slot)
    {
        EnsureCapacity(sizeof(short));
        BinaryPrimitives.WriteInt16BigEndian(_buffer[_writePosition..], slot);
        _writePosition += sizeof(short);
    }

    [Pure]
    public static int WriteVarInt(Span<byte> buffer, int value)
    {
        var bytesWritten = 0;
        do
        {
            var temp = (byte)(value & 0x7F);
            value >>>= 7;
            if (value != 0)
            {
                temp |= 0x80;
            }

            buffer[bytesWritten++] = temp;
        } while (value != 0);

        return bytesWritten;
    }
}
