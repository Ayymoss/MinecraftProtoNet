namespace MinecraftProtoNet.Models.World.Chunk;

public class BlockState(int id, string name)
{
    public int Id { get; } = id;
    public string Name { get; } = name;
    public bool IsAir => Id is 0;

    public bool IsLiquid => Name.Contains("water", StringComparison.CurrentCultureIgnoreCase) ||
                            Name.Contains("lava", StringComparison.CurrentCultureIgnoreCase);

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
