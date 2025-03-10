using MinecraftProtoNet.State.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Models.World.Chunk;

public class PalettedContainer<T>
{
    private IPalette<T> _palette;
    private BitStorage _storage;
    private PaletteType _paletteType;

    public PalettedContainer(PaletteType paletteType)
    {
        _paletteType = paletteType;
        _palette = CreatePalette(0);
        var size = _paletteType == PaletteType.BlockState ? 4096 : 64;
        _storage = new BitStorage(0, size);
    }

    private Dictionary<int, T> GetRegistry()
    {
        return _paletteType switch
        {
            PaletteType.BlockState => (Dictionary<int, T>)(object)ClientState.BlockStateRegistry,
            PaletteType.Biome => (Dictionary<int, T>)(object)ClientState.BiomeRegistry,
            _ => throw new ArgumentOutOfRangeException(nameof(_paletteType), _paletteType, null)
        };
    }

    public T Get(int index)
    {
        var paletteId = _storage.Get(index);
        return _palette.ValueFor(paletteId);
    }

    public void Read(ref PacketBufferReader reader)
    {
        var bitsPerEntry = reader.ReadUnsignedByte();
        _palette = CreatePalette(bitsPerEntry);
        _palette.Read(ref reader);

        var size = _paletteType == PaletteType.BlockState ? 4096 : 64;
        _storage = new BitStorage(bitsPerEntry, size);
        _storage.Read(ref reader);
    }

    private IPalette<T> CreatePalette(int bitsPerEntry)
    {
        var registry = GetRegistry();

        return bitsPerEntry switch
        {
            0 => new SingleValuePalette<T>(registry),
            <= 4 => new LinearPalette<T>(registry, bitsPerEntry),
            <= 8 => new HashMapPalette<T>(registry, bitsPerEntry),
            _ => new GlobalPalette<T>(registry)
        };
    }
}
