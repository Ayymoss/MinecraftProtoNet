namespace MinecraftProtoNet.Core.Models.World.Chunk;

/// <summary>
/// Per-block destroy speed (hardness) and whether the correct tool is required for drops.
/// Generated from the Minecraft 26.2 source (Blocks.java strength(...) calls); see
/// scratchpad/extract_hardness.py. -1 hardness = unbreakable (bedrock, barrier, command blocks).
/// </summary>
public sealed record BlockHardness(float Hardness, bool RequiresCorrectTool);
