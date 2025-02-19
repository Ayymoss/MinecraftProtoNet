using System.Buffers.Binary;
using System.Text;

namespace MinecraftProtoNet.Utilities;

public ref struct PacketBufferReader(ReadOnlySpan<byte> bytes)
{
    private readonly ReadOnlySpan<byte> _buffer = bytes;
    private int _readPosition = 0;

    public int ReadableBytes => _buffer.Length - _readPosition;

    public ReadOnlySpan<byte> GetReadableSpan() => _buffer[_readPosition..];

    public int ReadVarInt()
    {
        var bytesRead = 0;
        var result = 0;
        byte read;

        do
        {
            read = _buffer[_readPosition + bytesRead];
            var value = read & 127;
            result |= value << (7 * bytesRead);

            bytesRead++;
            if (bytesRead > 5) throw new ArithmeticException("VarInt too long");
        } while ((read & 0b10000000) != 0);

        _readPosition += bytesRead;
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
            if (_readPosition + bytesRead >= _buffer.Length)
            {
                throw new InvalidOperationException("VarLong is too long");
            }

            byteRead = _buffer[_readPosition + bytesRead];
            value |= (long)(byteRead & 0x7F) << shift;
            shift += 7;
            bytesRead++;
        } while ((byteRead & 0x80) != 0);

        _readPosition += bytesRead;
        return value;
    }


    public string ReadString()
    {
        var length = ReadVarInt();
        var strSpan = _buffer.Slice(_readPosition, length);
        _readPosition += length;
        return Encoding.UTF8.GetString(strSpan);
    }

    public ReadOnlySpan<byte> ReadRestBuffer()
    {
        ReadOnlySpan<byte> bytes = _buffer[_readPosition..];
        _readPosition = _buffer.Length;
        return bytes;
    }

    public byte ReadByte()
    {
        var b = _buffer[_readPosition];
        _readPosition++;
        return b;
    }

    public ushort ReadUnsignedShort()
    {
        var value = BinaryPrimitives.ReadUInt16BigEndian(_buffer[_readPosition..]);
        _readPosition += sizeof(ushort);
        return value;
    }

    public ReadOnlySpan<byte> ReadBuffer(int count)
    {
        if (count > ReadableBytes) throw new ArgumentOutOfRangeException(nameof(count), "Cannot read beyond the readable bytes.");

        var bytes = _buffer.Slice(_readPosition, count);
        _readPosition += count;
        return bytes;
    }

    public Guid ReadUUID()
    {
        var bytes = ReadBuffer(16);
        return new Guid(bytes); // Guid constructor directly handles big-endian
    }

    public long ReadSignedLong()
    {
        var value = BinaryPrimitives.ReadInt64BigEndian(_buffer[_readPosition..]);
        _readPosition += sizeof(long);
        return value;
    }

    public bool ReadBoolean()
    {
        return ReadByte() is 1;
    }
}
