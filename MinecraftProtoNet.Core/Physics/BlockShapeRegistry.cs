using System;
using System.Collections.Generic;
using MinecraftProtoNet.Enums;
using MinecraftProtoNet.Models.World.Chunk;
using MinecraftProtoNet.Models.World.Meta;
using MinecraftProtoNet.Physics.Shapes;

namespace MinecraftProtoNet.Physics;

public interface IBlockShapeRegistry
{
    VoxelShape GetShape(BlockState blockState);
}

public class BlockShapeRegistry : IBlockShapeRegistry
{
    // Caches could be added here for complex calculated shapes

    public VoxelShape GetShape(BlockState blockState)
    {
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
            // Migrated naive logic from ChunkManager, should be improved to full shape later
            var isTop = blockState.IsTop;
            return isTop 
                ? Shapes.Shapes.Box(0, 0.5, 0, 1, 1, 1) 
                : Shapes.Shapes.Box(0, 0, 0, 1, 0.5, 1);
            // Note: Missing the vertical part of stairs, but maintains parity with old simple logic for now.
        }

        if (blockState.Name.Contains("carpet"))
        {
             return Shapes.Shapes.Box(0, 0, 0, 1, 0.0625, 1);
        }

        // Default to full block
        return Shapes.Shapes.Block();
    }
}
