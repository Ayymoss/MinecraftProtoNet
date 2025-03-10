using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Models.World.Chunk;

public class BitStorage
{
    private readonly int _bitsPerEntry;
    private readonly long[] _data;
    private readonly int _size;
    private readonly long _maxEntryValue;

    public BitStorage(int bitsPerEntry, int size)
    {
        _bitsPerEntry = Math.Max(bitsPerEntry, 1);
        _size = size;
        _maxEntryValue = (1L << bitsPerEntry) - 1;

        var storageNeeded = (size * bitsPerEntry + 63) / 64;
        _data = new long[storageNeeded];
    }

    public int Get(int index)
    {
        if (index < 0 || index >= _size)
            throw new IndexOutOfRangeException($"Index {index} out of bounds for size {_size}");

        if (_bitsPerEntry == 0)
            return 0;

        var bitIndex = index * _bitsPerEntry;
        var longIndex = bitIndex >> 6; // Divide by 64
        var bitOffset = bitIndex & 0x3F; // Modulo 64

        if (bitOffset + _bitsPerEntry <= 64)
        {
            return (int)((_data[longIndex] >> bitOffset) & _maxEntryValue);
        }

        var part1 = 64 - bitOffset;
        var part2 = _bitsPerEntry - part1;

        var val1 = _data[longIndex] >> bitOffset;
        var val2 = _data[longIndex + 1] & ((1L << part2) - 1);

        return (int)((val1 | (val2 << part1)) & _maxEntryValue);
    }

    public void Read(ref PacketBufferReader reader)
    {
        var length = reader.ReadVarInt(); // TODO: Validate with PrefixedArray helper.
        for (var i = 0; i < length && i < _data.Length; i++)
        {
            _data[i] = reader.ReadSignedLong();
        }
    }
}
