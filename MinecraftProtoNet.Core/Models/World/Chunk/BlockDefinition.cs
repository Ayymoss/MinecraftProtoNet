namespace MinecraftProtoNet.Core.Models.World.Chunk;

/// <summary>
/// Block-class identity for a block (shared by all of its block states), sourced from the vanilla
/// data report's "definition" object. <see cref="Type"/> is the block-type id (e.g. minecraft:slab,
/// minecraft:ladder, minecraft:liquid) and is the non-heuristic equivalent of the Java Block subclass.
/// <see cref="Fluid"/> is set for liquid blocks (minecraft:water / minecraft:lava).
/// </summary>
public sealed record BlockDefinition(string Type, string? Fluid);
