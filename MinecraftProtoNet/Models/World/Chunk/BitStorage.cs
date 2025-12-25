namespace MinecraftProtoNet.Models.World.Chunk;

public class BitStorage
{
    private readonly int _bitsPerEntry;
    private readonly long[]? _data;
    private readonly int _size;
    private readonly long _maxEntryValue;
    private readonly int _valuesPerLong;

    public BitStorage(int bitsPerEntry, int size, long[]? data)
    {
        if (bitsPerEntry is < 1 or > 32)
            throw new ArgumentOutOfRangeException(nameof(bitsPerEntry), "Bits per entry must be between 1 and 32, inclusive.");

        _bitsPerEntry = bitsPerEntry;
        _size = size;
        _maxEntryValue = (1L << bitsPerEntry) - 1;
        _valuesPerLong = 64 / bitsPerEntry;

        var expectedLength = (size + _valuesPerLong - 1) / _valuesPerLong;

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
        if (_data is null) return 0;

        var longIndex = index / _valuesPerLong;
        var bitOffset = (index - longIndex * _valuesPerLong) * _bitsPerEntry;

        return (int)((_data[longIndex] >> bitOffset) & _maxEntryValue);
    }

    public void Set(int index, int value)
    {
        if (index < 0 || index >= _size) throw new IndexOutOfRangeException($"Index {index} out of bounds for size {_size}");
        if (_data is null) return;

        var longValue = (long)value & _maxEntryValue;
        var longIndex = index / _valuesPerLong;
        var bitOffset = (index - longIndex * _valuesPerLong) * _bitsPerEntry;

        _data[longIndex] = (_data[longIndex] & ~(_maxEntryValue << bitOffset)) | (longValue << bitOffset);
    }
}
