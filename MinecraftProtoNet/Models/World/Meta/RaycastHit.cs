using MinecraftProtoNet.Enums;
using MinecraftProtoNet.Models.Core;
using MinecraftProtoNet.Models.World.Chunk;

namespace MinecraftProtoNet.Models.World.Meta;

public class RaycastHit
{
    public BlockState? Block { get; set; }
    public BlockFace Face { get; set; }
    public Vector3<int> BlockPosition { get; set; }
    public Vector3<double> ExactHitPosition { get; set; }
    public double Distance { get; set; }
    public bool InsideBlock { get; set; }

    public Vector3<float> GetInBlockPosition()
    {
        var x = ExactHitPosition.X - Math.Floor(ExactHitPosition.X);
        var y = ExactHitPosition.Y - Math.Floor(ExactHitPosition.Y);
        var z = ExactHitPosition.Z - Math.Floor(ExactHitPosition.Z);

        switch (Face)
        {
            case BlockFace.Bottom:
                y = 0.0;
                break;
            case BlockFace.Top:
                y = 1.0;
                break;
            case BlockFace.North:
                z = 0.0;
                break;
            case BlockFace.South:
                z = 1.0;
                break;
            case BlockFace.West:
                x = 0.0;
                break;
            case BlockFace.East:
                x = 1.0;
                break;
        }

        x = Math.Round(x, 6);
        y = Math.Round(y, 6);
        z = Math.Round(z, 6);

        return new Vector3<float>((float)x, (float)y, (float)z);
    }

    public Vector3<int> GetAdjacentBlockPosition()
    {
        return Face switch
        {
            BlockFace.Bottom => new Vector3<int>(BlockPosition.X, BlockPosition.Y - 1, BlockPosition.Z),
            BlockFace.Top => new Vector3<int>(BlockPosition.X, BlockPosition.Y + 1, BlockPosition.Z),
            BlockFace.North => new Vector3<int>(BlockPosition.X, BlockPosition.Y, BlockPosition.Z - 1),
            BlockFace.South => new Vector3<int>(BlockPosition.X, BlockPosition.Y, BlockPosition.Z + 1),
            BlockFace.West => new Vector3<int>(BlockPosition.X - 1, BlockPosition.Y, BlockPosition.Z),
            BlockFace.East => new Vector3<int>(BlockPosition.X + 1, BlockPosition.Y, BlockPosition.Z),
            _ => BlockPosition
        };
    }
}
