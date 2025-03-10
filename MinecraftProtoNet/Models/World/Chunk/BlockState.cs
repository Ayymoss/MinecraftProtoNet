namespace MinecraftProtoNet.Models.World.Chunk;

public class BlockState(int id, string name)
{
    public static readonly BlockState Air = new(0, "minecraft:air");

    public int Id { get; } = id;
    public string Name { get; } = name;

    public override bool Equals(object? obj)
    {
        if (obj is BlockState other)
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
        return $"BlockState({Id})";
    }
}
