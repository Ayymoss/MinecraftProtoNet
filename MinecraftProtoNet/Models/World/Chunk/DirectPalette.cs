using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Models.World.Chunk;

public class DirectPalette : IPalette
{
    private readonly Dictionary<int, int> _registryToId = new();
    private readonly List<int> _registryIds = [];

    public int IdFor(int registryId)
    {
        if (_registryToId.TryGetValue(registryId, out var id)) return id;

        id = _registryIds.Count;
        _registryIds.Add(registryId);
        _registryToId[registryId] = id;
        return id;
    }

    public int RegistryIdFor(int paletteId)
    {
        if (paletteId < 0 || paletteId >= _registryIds.Count) throw new IndexOutOfRangeException($"Invalid palette id: {paletteId}");
        return _registryIds[paletteId];
    }

    public void Read(ref PacketBufferReader reader)
    {
        var size = reader.ReadVarInt();
        _registryIds.Clear();
        _registryToId.Clear();

        for (var i = 0; i < size; i++)
        {
            var registryId = reader.ReadVarInt();
            _registryIds.Add(registryId);
            _registryToId[registryId] = i;
        }
    }
}
