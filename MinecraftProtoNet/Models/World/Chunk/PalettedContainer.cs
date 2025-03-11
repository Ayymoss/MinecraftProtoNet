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
