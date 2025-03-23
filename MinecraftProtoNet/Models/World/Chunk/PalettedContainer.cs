using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Models.World.Chunk;

public class PalettedContainer
{
    private IPalette _palette;
    private BitStorage? _storage;
    private readonly PaletteType _paletteType;

    public PalettedContainer(PaletteType paletteType)
    {
        _paletteType = paletteType;
        _palette = CreatePalette(0);
    }

    public int? Get(int index)
    {
        if (_storage is null) return null;

        var paletteId = _storage.Get(index);
        return _palette.RegistryIdFor(paletteId);
    }

    public void Read(ref PacketBufferReader reader)
    {
        var bitsPerEntry = reader.ReadUnsignedByte();
        _palette = CreatePalette(bitsPerEntry);
        _palette.Read(ref reader);

        var size = _paletteType == PaletteType.BlockState ? 4096 : 64;

        // We still need to read the data even if bitsPerEntry is 0???? Why....
        var bitData = reader.ReadPrefixedArray<long>();
        if (bitsPerEntry is 0) return;

        _storage = new BitStorage(bitsPerEntry, size, bitData);
    }

    public void Set(int index, int registryId)
    {
        if (_storage is null)
        {
            if (_palette is SingleValuePalette singleValuePalette)
            {
                try
                {
                    singleValuePalette.IdFor(registryId);
                    return;
                }
                catch (InvalidOperationException)
                {
                    var size = _paletteType == PaletteType.BlockState ? 4096 : 64;
                    _palette = CreatePalette(4);
                    _storage = new BitStorage(4, size, null);

                    var oldRegistryId = singleValuePalette.RegistryIdFor(0);
                    var oldPaletteId = _palette.IdFor(oldRegistryId);

                    for (var i = 0; i < size; i++)
                    {
                        _storage.Set(i, oldPaletteId);
                    }
                }
            }
            else
            {
                var bitsNeeded = Math.Max(4, (int)Math.Ceiling(Math.Log2(registryId + 1)));
                var size = _paletteType == PaletteType.BlockState ? 4096 : 64;
                _storage = new BitStorage(bitsNeeded, size, null);
                _palette = CreatePalette(bitsNeeded);
            }
        }

        try
        {
            var paletteId = _palette.IdFor(registryId);
            _storage.Set(index, paletteId);
        }
        catch (InvalidOperationException)
        {
            UpgradePalette();
            var paletteId = _palette.IdFor(registryId);
            _storage.Set(index, paletteId);
        }
    }

    private void UpgradePalette()
    {
        var currentBits = _palette switch
        {
            SingleValuePalette => 0,
            IndirectPalette indirect => indirect.Bits,
            DirectPalette => 8,
            GlobalPalette => 16,
            _ => throw new InvalidOperationException("Unknown palette type")
        };

        var newBits = Math.Min(currentBits + 1, 16);
        var newPalette = CreatePalette(newBits);
        var size = _paletteType == PaletteType.BlockState ? 4096 : 64;
        var newStorage = new BitStorage(newBits, size, null);

        if (_storage != null)
        {
            for (var i = 0; i < size; i++)
            {
                var oldPaletteId = _storage.Get(i);
                var registryId = _palette.RegistryIdFor(oldPaletteId);
                var newPaletteId = newPalette.IdFor(registryId);
                newStorage.Set(i, newPaletteId);
            }
        }

        _palette = newPalette;
        _storage = newStorage;
    }

    private IPalette CreatePalette(int bitsPerEntry)
    {
        return bitsPerEntry switch
        {
            0 => new SingleValuePalette(),
            <= 4 => new IndirectPalette(bitsPerEntry),
            <= 8 => new DirectPalette(),
            _ => new GlobalPalette()
        };
    }
}
