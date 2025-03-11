using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Models.World.Chunk;

public class IndirectPalette(int bits) : IPalette
{
    private readonly int[] _registryIds = new int[1 << bits];
    private int _size = 0;

    public int IdFor(int registryId)
    {
        for (var i = 0; i < _size; i++)
            if (_registryIds[i] == registryId)
                return i;

        if (_size >= _registryIds.Length) throw new InvalidOperationException("Palette is full");

        _registryIds[_size] = registryId;
        return _size++;
    }

    public int RegistryIdFor(int paletteId)
    {
        if (paletteId < 0 || paletteId >= _size) throw new IndexOutOfRangeException($"Invalid palette id: {paletteId}");
        return _registryIds[paletteId];
    }

    public void Read(ref PacketBufferReader reader)
    {
        _size = reader.ReadVarInt();
        for (var i = 0; i < _size; i++)
        {
            _registryIds[i] = reader.ReadVarInt();
        }
    }
}
