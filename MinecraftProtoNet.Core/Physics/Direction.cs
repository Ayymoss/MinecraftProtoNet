using System;
using MinecraftProtoNet.Enums;
using MinecraftProtoNet.Models.Core;

namespace MinecraftProtoNet.Physics;

public static class Direction
{
    public static BlockFace GetApproximateNearest(double x, double y, double z)
    {
        BlockFace face = BlockFace.North;
        double max = float.MinValue;

        foreach (BlockFace f in Enum.GetValues(typeof(BlockFace)))
        {
            var normal = GetNormal(f);
            double dot = x * normal.X + y * normal.Y + z * normal.Z;
            if (dot > max)
            {
                max = dot;
                face = f;
            }
        }
        return face;
    }
    
    public static Vector3<int> GetNormal(BlockFace face) => face switch
    {
        BlockFace.Bottom => new Vector3<int>(0, -1, 0),
        BlockFace.Top => new Vector3<int>(0, 1, 0),
        BlockFace.North => new Vector3<int>(0, 0, -1),
        BlockFace.South => new Vector3<int>(0, 0, 1),
        BlockFace.West => new Vector3<int>(-1, 0, 0),
        BlockFace.East => new Vector3<int>(1, 0, 0),
        _ => Vector3<int>.Zero
    };
    
    public static BlockFace GetOpposite(this BlockFace face) => face switch
    {
        BlockFace.Bottom => BlockFace.Top,
        BlockFace.Top => BlockFace.Bottom,
        BlockFace.North => BlockFace.South,
        BlockFace.South => BlockFace.North,
        BlockFace.West => BlockFace.East,
        BlockFace.East => BlockFace.West,
        _ => face
    };
    
    public static Axis Axis(this BlockFace face) => face switch
    {
        BlockFace.West or BlockFace.East => Physics.Axis.X,
        BlockFace.Bottom or BlockFace.Top => Physics.Axis.Y,
        _ => Physics.Axis.Z
    };
}
