using MinecraftProtoNet.Core.Models.World.Chunk;
using MinecraftProtoNet.Core.Physics.Shapes;
using MinecraftProtoNet.Core.State.Base;

namespace MinecraftProtoNet.Core.Physics;

public interface IBlockShapeRegistry
{
    VoxelShape GetShape(BlockState blockState);
}

public class BlockShapeRegistry : IBlockShapeRegistry
{
    // Caches could be added here for complex calculated shapes

    public VoxelShape GetShape(BlockState blockState)
    {
        // DEVIATION FROM VANILLA: In vanilla, ladders have hasCollision=false (no collision shape).
        // The player walks through the ladder and hits the solid wall behind it, which triggers
        // HorizontalCollision and enables the 0.2 Y climb boost.
        // We intentionally add thin collision shapes here so the bot gets HorizontalCollision
        // from colliding with the ladder itself, without needing to reach the wall behind it.
        // This is complemented by forced HorizontalCollision logic in PhysicsService.Move().
        if (blockState.Name.Equals("minecraft:ladder", StringComparison.OrdinalIgnoreCase))
        {
            var facing = blockState.Properties.GetValueOrDefault("facing", "north");
            return facing.ToLower() switch
            {
                "north" => Shapes.Shapes.Box(0, 0, 0.8125, 1, 1, 1),
                "south" => Shapes.Shapes.Box(0, 0, 0, 1, 1, 0.1875),
                "west" => Shapes.Shapes.Box(0.8125, 0, 0, 1, 1, 1),
                "east" => Shapes.Shapes.Box(0, 0, 0, 0.1875, 1, 1),
                _ => Shapes.Shapes.Empty()
            };
        }

        if (!blockState.BlocksMotion || blockState.IsAir || blockState.IsLiquid)
        {
            return Shapes.Shapes.Empty();
        }

        if (blockState.IsSlab)
        {
            var type = blockState.Properties.GetValueOrDefault("type", "bottom");
            if (type == "top")
                return Shapes.Shapes.Box(0, 0.5, 0, 1, 1, 1);
            if (type == "bottom")
                return Shapes.Shapes.Box(0, 0, 0, 1, 0.5, 1);
            return Shapes.Shapes.Block(); // Double slab
        }
        
        if (blockState.IsSnow)
        {
            var layers = blockState.SnowLayers;
            if (layers > 0)
                return Shapes.Shapes.Box(0, 0, 0, 1, layers * 0.125, 1);
            return Shapes.Shapes.Empty();
        }

        if (blockState.IsStairs)
        {
            var isTop = blockState.IsTop;
            var baseBox = isTop 
                ? Shapes.Shapes.Box(0, 0.5, 0, 1, 1, 1) 
                : Shapes.Shapes.Box(0, 0, 0, 1, 0.5, 1);
            
            // Add the step part of the stair
            var facing = blockState.Properties.GetValueOrDefault("facing", "north");
            VoxelShape stepBox;
            if (isTop)
            {
                // Top stairs: step is at the bottom half
                stepBox = facing switch
                {
                    "north" => Shapes.Shapes.Box(0, 0, 0.5, 1, 0.5, 1),
                    "south" => Shapes.Shapes.Box(0, 0, 0, 1, 0.5, 0.5),
                    "west" => Shapes.Shapes.Box(0.5, 0, 0, 1, 0.5, 1),
                    "east" => Shapes.Shapes.Box(0, 0, 0, 0.5, 0.5, 1),
                    _ => Shapes.Shapes.Empty()
                };
            }
            else
            {
                // Bottom stairs: step is at the top half
                stepBox = facing switch
                {
                    "north" => Shapes.Shapes.Box(0, 0.5, 0.5, 1, 1, 1),
                    "south" => Shapes.Shapes.Box(0, 0.5, 0, 1, 1, 0.5),
                    "west" => Shapes.Shapes.Box(0.5, 0.5, 0, 1, 1, 1),
                    "east" => Shapes.Shapes.Box(0, 0.5, 0, 0.5, 1, 1),
                    _ => Shapes.Shapes.Empty()
                };
            }

            return Shapes.Shapes.Or(baseBox, stepBox);
        }

        if (ClientState.BlockTags.HasTag(blockState.Name, "wool_carpets"))
        {
             return Shapes.Shapes.Box(0, 0, 0, 1, 0.0625, 1);
        }

        // Default to full block
        return Shapes.Shapes.Block();
    }
}
