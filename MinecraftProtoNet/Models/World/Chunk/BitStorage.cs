namespace MinecraftProtoNet.Models.World.Chunk;

public class BitStorage
{
    private readonly int _bitsPerEntry;
    private readonly long[]? _data;
    private readonly int _size;
    private readonly long _maxEntryValue;

    public BitStorage(int bitsPerEntry, int size, long[]? data)
    {
        if (bitsPerEntry is < 1 or > 32)
            throw new ArgumentOutOfRangeException(nameof(bitsPerEntry), "Bits per entry must be between 1 and 32, inclusive.");

        _bitsPerEntry = bitsPerEntry;
        _size = size;

        _maxEntryValue = (1L << bitsPerEntry) - 1;
        var valuesPerLong = 64 / bitsPerEntry;
        var expectedLength = (size + valuesPerLong - 1) / valuesPerLong;

        if (data is not null)
        {
            if (data.Length != expectedLength)
            {
                Array.Resize(ref data, expectedLength);
            }

            _data = data;
        }
        else
        {
            _data = new long[expectedLength];
        }
    }

    public int Get(int index)
    {
        if (index < 0 || index >= _size) throw new IndexOutOfRangeException($"Index {index} out of bounds for size {_size}");
        if (_bitsPerEntry == 0 || _data is null) return 0;

        var bitIndex = index * _bitsPerEntry;
        var longIndex = bitIndex >> 6; // Divide by 64
        var bitOffset = bitIndex & 0x3F; // Modulo 64

        if (bitOffset + _bitsPerEntry <= 64) return (int)((_data[longIndex] >> bitOffset) & _maxEntryValue);

        var part1 = 64 - bitOffset;
        var part2 = _bitsPerEntry - part1;

        var val1 = _data[longIndex] >> bitOffset;
        var val2 = _data[longIndex + 1] & ((1L << part2) - 1);

        return (int)((val1 | (val2 << part1)) & _maxEntryValue);
    }

    public void Set(int index, int value)
    {
        if (index < 0 || index >= _size) throw new IndexOutOfRangeException($"Index {index} out of bounds for size {_size}");
        if (_data is null) return;

        var longValue = value & _maxEntryValue;
        var bitIndex = index * _bitsPerEntry;
        var longIndex = bitIndex >> 6; // Divide by 64
        var bitOffset = bitIndex & 0x3F; // Modulo 64

        if (bitOffset + _bitsPerEntry <= 64)
        {
            _data[longIndex] = (_data[longIndex] & ~(_maxEntryValue << bitOffset)) | (longValue << bitOffset);
        }
        else
        {
            var part1 = 64 - bitOffset;
            var part2 = _bitsPerEntry - part1;
            _data[longIndex] = (_data[longIndex] & ~(((1L << part1) - 1) << bitOffset)) | (longValue << bitOffset);
            _data[longIndex + 1] = (_data[longIndex + 1] & ~((1L << part2) - 1)) | (longValue >> part1);
        }
    }
}
