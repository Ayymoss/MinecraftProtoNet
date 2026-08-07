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
    /// <summary>
    /// Shared stateless instance for callers that don't have one injected (e.g. Baritone helpers).
    /// Shapes are derived purely from block identity/properties + static block tags, so a singleton is safe.
    /// </summary>
    public static readonly BlockShapeRegistry Shared = new();

    // Shape depends only on the block-state id (which encodes all properties), so cache per id.
    // ConcurrentDictionary: the network thread and the pathfinding thread can both call this.
    private readonly System.Collections.Concurrent.ConcurrentDictionary<int, VoxelShape> _shapeCache = new();

    public VoxelShape GetShape(BlockState blockState)
    {
        return _shapeCache.GetOrAdd(blockState.Id, _ => GetShapeCore(blockState));
    }

    private VoxelShape GetShapeCore(BlockState blockState)
    {
        // NOT a deviation (an earlier comment here claimed it was): vanilla ladders DO have collision. In
        // Blocks.java:1246 LADDER is registered WITHOUT noCollision() - compare RAIL on the next line, which
        // has it - so its collision shape is getShape(), i.e. LadderBlock.SHAPES = Block.boxZ(16, 13, 16),
        // the same thin slab against the support side that we build below.
        // Consequence worth knowing: while inside a ladder block the player is stopped by this slab and sits
        // tangent to the ladder's top face, so climbing alone never lands you on top of a ladder - you clear
        // the top rung and then walk forward onto it. Baritone never needs this (it exits a ladder by a flat
        // traverse onto a block level with the top rung), which is why paths that end on a ladder top drop.
        // Reference: minecraft-26.2-REFERENCE-ONLY/net/minecraft/world/level/block/LadderBlock.java:111
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

        // An OPEN fence gate or door has no collision at all, even though blocksMotion stays true for the
        // block. Without this they fell through to the full-cube case below, so the bot could open a gate and
        // still be walled out by it - it would open the gate, fail to walk through, and the next right-click
        // would close it again, toggling forever (observed on the gate1 course: state 8675<->8677, 10 times).
        // Reference: minecraft-26.2-REFERENCE-ONLY/net/minecraft/world/level/block/FenceGateBlock.java:84-86
        //   getCollisionShape -> state.getValue(OPEN) ? Shapes.empty() : SHAPE_COLLISION.get(axis)
        // Doors behave the same way: the leaf swings aside, clearing the doorway (DoorBlock.java:67-70).
        // Closed is modelled as a full cube rather than vanilla's thin leaf/post - conservative, and it only
        // means the bot will not try to stand inside a closed gate or doorway, which it has no reason to do.
        if (blockState.IsFenceGate || blockState.IsDoor)
        {
            return blockState.Properties.GetValueOrDefault("open", "false") == "true"
                ? Shapes.Shapes.Empty()
                : Shapes.Shapes.Block();
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
