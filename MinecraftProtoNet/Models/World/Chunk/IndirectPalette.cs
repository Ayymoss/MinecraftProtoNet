using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Models.World.Chunk;

public class IndirectPalette(int bits) : IPalette
{
    public int Bits { get; } = bits;
    private int[] _registryIds = new int[1 << bits];
    private int _size;

    public int IdFor(int registryId)
    {
        for (var i = 0; i < _size; i++)
            if (_registryIds[i] == registryId)
                return i;

        if (_size >= _registryIds.Length)
        {
            // If it exceeds the theoretical capacity for these bits, we should probably resize
            // but the PalettedContainer handle resizing by upgrading the entire palette type.
            throw new InvalidOperationException($"Palette is full (size={_size}, capacity={_registryIds.Length})");
        }

        _registryIds[_size] = registryId;
        return _size++;
    }

    public int RegistryIdFor(int paletteId)
    {
        if (paletteId < 0 || paletteId >= _size) 
            throw new IndexOutOfRangeException($"Invalid palette id: {paletteId} (size: {_size})");
        return _registryIds[paletteId];
    }

    public void Read(ref PacketBufferReader reader)
    {
        _size = reader.ReadVarInt();
        // Ensure array is large enough for the incoming size, 
        // even if it exceeds 1 << Bits (though it shouldn't in valid packets)
        if (_size > _registryIds.Length)
        {
            _registryIds = new int[_size];
        }

        for (var i = 0; i < _size; i++)
        {
            _registryIds[i] = reader.ReadVarInt();
        }
    }
}
