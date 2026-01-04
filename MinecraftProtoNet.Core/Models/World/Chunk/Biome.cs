namespace MinecraftProtoNet.Core.Models.World.Chunk;

public class Biome(int id, string name)
{
    public int Id { get; } = id;
    public string Name { get; } = name;

    public override bool Equals(object? obj)
    {
        if (obj is Biome other)
        {
            return Id == other.Id;
        }

        return false;
    }

    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }

    public override string ToString()
    {
        return Name;
    }
}
