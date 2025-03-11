using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Models.World.Chunk;

public class SingleValuePalette : IPalette
{
    private int _registryId;
    private bool _hasValue;

    public int IdFor(int registryId)
    {
        if (!_hasValue)
        {
            _registryId = registryId;
            _hasValue = true;
        }
        else if (_registryId != registryId)
        {
            throw new InvalidOperationException("Cannot add more than one value to SingleValuePalette");
        }

        return 0;
    }

    public int RegistryIdFor(int paletteId)
    {
        if (paletteId != 0 || !_hasValue) throw new IndexOutOfRangeException($"Invalid palette id: {paletteId}");

        return _registryId;
    }

    public void Read(ref PacketBufferReader reader)
    {
        _registryId = reader.ReadVarInt();
        _hasValue = true;
    }
}
