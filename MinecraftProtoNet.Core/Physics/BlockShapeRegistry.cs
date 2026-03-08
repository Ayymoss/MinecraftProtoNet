using MinecraftProtoNet.Core.Models.World.Chunk;
using MinecraftProtoNet.Core.Physics.Shapes;
using MinecraftProtoNet.Core.State.Base;

namespace MinecraftProtoNet.Core.Physics;

public interface IBlockShapeRegistry
{
    /// <summary>
    /// Gets the collision shape for physics/movement. Empty for non-solid blocks like signs.
    /// </summary>
    VoxelShape GetShape(BlockState blockState);

    /// <summary>
    /// Gets the outline/interaction shape for raycasting and block selection.
    /// Non-empty for interactive blocks like signs even though they have no collision.
    /// Reference: BlockBehaviour.java — getShape() vs getCollisionShape()
    /// </summary>
    VoxelShape GetOutlineShape(BlockState blockState);
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

    /// <summary>
    /// Returns the outline/interaction shape for raycasting. Falls back to collision shape
    /// for most blocks, but returns proper shapes for non-collision interactive blocks like signs.
    /// Reference: BlockBehaviour.java:302 — getShape() returns outline, getCollisionShape() checks hasCollision
    /// </summary>
    public VoxelShape GetOutlineShape(BlockState blockState)
    {
        if (blockState.IsAir || blockState.IsLiquid)
            return Shapes.Shapes.Empty();

        // Standing signs: centered 8/16 wide column, full height
        // Reference: SignBlock.java:186 — SHAPE = Block.column(8.0, 0.0, 16.0)
        if (ClientState.BlockTags.HasTag(blockState.Name, "standing_signs"))
        {
            // column(8, 0, 16) → half=4 → Box(4/16, 0, 4/16, 12/16, 1, 12/16)
            return Shapes.Shapes.Box(0.25, 0, 0.25, 0.75, 1, 0.75);
        }

        // Wall signs: thin plate on the wall face, depends on facing
        // Reference: WallSignBlock.java:94 — SHAPES = Shapes.rotateHorizontal(Block.boxZ(16.0, 4.5, 12.5, 14.0, 16.0))
        if (ClientState.BlockTags.HasTag(blockState.Name, "wall_signs"))
        {
            var facing = blockState.Properties.GetValueOrDefault("facing", "north");
            return facing.ToLower() switch
            {
                // boxZ(16, 4.5, 12.5, 14, 16) → Box(0, 4.5/16, 14/16, 1, 12.5/16, 1)
                // Then rotated for each direction
                "north" => Shapes.Shapes.Box(0, 4.5 / 16, 14.0 / 16, 1, 12.5 / 16, 1),
                "south" => Shapes.Shapes.Box(0, 4.5 / 16, 0, 1, 12.5 / 16, 2.0 / 16),
                "west" => Shapes.Shapes.Box(14.0 / 16, 4.5 / 16, 0, 1, 12.5 / 16, 1),
                "east" => Shapes.Shapes.Box(0, 4.5 / 16, 0, 2.0 / 16, 12.5 / 16, 1),
                _ => Shapes.Shapes.Box(0.25, 0, 0.25, 0.75, 1, 0.75) // fallback to standing
            };
        }

        // Ceiling hanging signs: centered 10/16 wide column, full height
        // Reference: CeilingHangingSignBlock.java:143 — SHAPE_DEFAULT = Block.column(10.0, 0.0, 16.0)
        if (ClientState.BlockTags.HasTag(blockState.Name, "ceiling_hanging_signs"))
        {
            // column(10, 0, 16) → half=5 → Box(3/16, 0, 3/16, 13/16, 1, 13/16)
            return Shapes.Shapes.Box(3.0 / 16, 0, 3.0 / 16, 13.0 / 16, 1, 13.0 / 16);
        }

        // Wall hanging signs: plank + chain shape, depends on facing axis
        // Reference: WallHangingSignBlock.java:149-150
        if (ClientState.BlockTags.HasTag(blockState.Name, "wall_hanging_signs"))
        {
            var facing = blockState.Properties.GetValueOrDefault("facing", "north");
            var axis = facing.ToLower() switch
            {
                "north" or "south" => "z",
                _ => "x"
            };
            // Simplified: use column(16, 4, 14, 16) plank shape
            // column(16, 4/16, 14/16, 1) for Z-axis
            return axis == "z"
                ? Shapes.Shapes.Box(0, 4.0 / 16, 1.0 / 16, 1, 1, 15.0 / 16)
                : Shapes.Shapes.Box(1.0 / 16, 4.0 / 16, 0, 15.0 / 16, 1, 1);
        }

        // For all other blocks, the outline shape is the same as the collision shape
        return GetShape(blockState);
    }
}
