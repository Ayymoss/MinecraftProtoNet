/*
 * This file is part of Baritone.
 *
 * Baritone is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Lesser General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * Baritone is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with Baritone.  If not, see <https://www.gnu.org/licenses/>.
 *
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/cache/CachedChunk.java
 */

using System.Collections;
using MinecraftProtoNet.Baritone.Api.Utils;
using MinecraftProtoNet.Baritone.Pathfinding;

namespace MinecraftProtoNet.Baritone.Cache;

/// <summary>
/// Cached chunk implementation.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/cache/CachedChunk.java
/// </summary>
public class CachedChunk
{
    public static readonly HashSet<string> BlocksToKeepTrackOf = new()
    {
        "minecraft:ender_chest",
        "minecraft:furnace",
        "minecraft:chest",
        "minecraft:trapped_chest",
        "minecraft:end_portal",
        "minecraft:end_portal_frame",
        "minecraft:spawner",
        "minecraft:barrier",
        "minecraft:observer",
        "minecraft:white_shulker_box",
        "minecraft:orange_shulker_box",
        "minecraft:magenta_shulker_box",
        "minecraft:light_blue_shulker_box",
        "minecraft:yellow_shulker_box",
        "minecraft:lime_shulker_box",
        "minecraft:pink_shulker_box",
        "minecraft:gray_shulker_box",
        "minecraft:light_gray_shulker_box",
        "minecraft:cyan_shulker_box",
        "minecraft:purple_shulker_box",
        "minecraft:blue_shulker_box",
        "minecraft:brown_shulker_box",
        "minecraft:green_shulker_box",
        "minecraft:red_shulker_box",
        "minecraft:black_shulker_box",
        "minecraft:nether_portal",
        "minecraft:hopper",
        "minecraft:beacon",
        "minecraft:brewing_stand",
        "minecraft:creeper_head",
        "minecraft:creeper_wall_head",
        "minecraft:dragon_head",
        "minecraft:dragon_wall_head",
        "minecraft:player_head",
        "minecraft:player_wall_head",
        "minecraft:zombie_head",
        "minecraft:zombie_wall_head",
        "minecraft:skeleton_skull",
        "minecraft:skeleton_wall_skull",
        "minecraft:wither_skeleton_skull",
        "minecraft:wither_skeleton_wall_skull",
        "minecraft:enchanting_table",
        "minecraft:anvil",
        "minecraft:white_bed",
        "minecraft:orange_bed",
        "minecraft:magenta_bed",
        "minecraft:light_blue_bed",
        "minecraft:yellow_bed",
        "minecraft:lime_bed",
        "minecraft:pink_bed",
        "minecraft:gray_bed",
        "minecraft:light_gray_bed",
        "minecraft:cyan_bed",
        "minecraft:purple_bed",
        "minecraft:blue_bed",
        "minecraft:brown_bed",
        "minecraft:green_bed",
        "minecraft:red_bed",
        "minecraft:black_bed",
        "minecraft:dragon_egg",
        "minecraft:jukebox",
        "minecraft:end_gateway",
        "minecraft:cobweb",
        "minecraft:nether_wart",
        "minecraft:ladder",
        "minecraft:vine"
    };

    public readonly int Height;
    public readonly int X;
    public readonly int Z;
    public readonly long CacheTimestamp;

    public int Size => CalculateSize(Height);
    public int SizeInBytes => CalculateSizeInBytes(Size);

    private readonly BitArray _data;
    private readonly Dictionary<int, string>? _special; // Position index -> block name
    private readonly object[] _overview; // BlockState array for top blocks (256 entries)
    private readonly int[] _heightMap;
    private readonly Dictionary<string, List<BetterBlockPos>> _specialBlockLocations;

    public CachedChunk(int x, int z, int height, BitArray data, object[] overview, 
        Dictionary<string, List<BetterBlockPos>> specialBlockLocations, long cacheTimestamp)
    {
        ValidateSize(data);

        X = x;
        Z = z;
        Height = height;
        _data = data;
        _overview = overview;
        _heightMap = new int[256];
        _specialBlockLocations = specialBlockLocations;
        CacheTimestamp = cacheTimestamp;

        if (specialBlockLocations.Count > 0)
        {
            _special = new Dictionary<int, string>();
            SetSpecial();
        }
        else
        {
            _special = null;
        }

        CalculateHeightMap();
    }

    public static int CalculateSize(int dimensionHeight)
    {
        return 2 * 16 * 16 * dimensionHeight;
    }

    public static int CalculateSizeInBytes(int size)
    {
        return size / 8;
    }

    private void SetSpecial()
    {
        foreach (var entry in _specialBlockLocations)
        {
            foreach (var pos in entry.Value)
            {
                var index = GetPositionIndex(pos.X & 15, pos.Y, pos.Z & 15);
                _special![index] = entry.Key;
            }
        }
    }

    public object? GetBlock(int x, int y, int z, int minY, int logicalHeight, bool hasCeiling, string dimensionId)
    {
        var adjY = y - minY;
        var index = GetPositionIndex(x, y, z);
        var type = GetType(index);
        var internalPos = (z & 15) << 4 | (x & 15);

        if (_heightMap[internalPos] == adjY && type != PathingBlockType.Avoid)
        {
            // Surface block
            return _overview[internalPos];
        }

        if (_special != null && _special.TryGetValue(index, out var blockName))
        {
            // Will be converted to BlockState when integrated
            return blockName;
        }

        if (type == PathingBlockType.Solid)
        {
            if (adjY == logicalHeight - 1 && hasCeiling)
            {
                // Nether roof is always unbreakable
                return "minecraft:bedrock";
            }

            if ((dimensionId == "minecraft:overworld" || dimensionId == "minecraft:the_nether") && adjY < 5)
            {
                // Solid blocks below 5 are commonly bedrock
                return "minecraft:obsidian";
            }
        }

        return PathingTypeToBlock(type, dimensionId);
    }

    private PathingBlockType GetType(int index)
    {
        return PathingBlockTypeExtensions.FromBits(_data[index], _data[index + 1]);
    }

    private void CalculateHeightMap()
    {
        for (int z = 0; z < 16; z++)
        {
            for (int x = 0; x < 16; x++)
            {
                var index = z << 4 | x;
                _heightMap[index] = 0;
                for (int y = Height - 1; y >= 0; y--)
                {
                    var i = GetPositionIndex(x, y, z);
                    if (_data[i] || _data[i + 1])
                    {
                        _heightMap[index] = y;
                        break;
                    }
                }
            }
        }
    }

    public object[] GetOverview() => _overview;

    public Dictionary<string, List<BetterBlockPos>> GetRelativeBlocks() => _specialBlockLocations;

    public List<BetterBlockPos>? GetAbsoluteBlocks(string blockType)
    {
        if (!_specialBlockLocations.TryGetValue(blockType, out var locations))
        {
            return null;
        }

        var result = new List<BetterBlockPos>();
        foreach (var pos in locations)
        {
            result.Add(new BetterBlockPos(pos.X + X * 16, pos.Y, pos.Z + Z * 16));
        }
        return result;
    }

    public byte[] ToByteArray()
    {
        var bytes = new byte[SizeInBytes];
        _data.CopyTo(bytes, 0);
        return bytes;
    }

    public static int GetPositionIndex(int x, int y, int z)
    {
        return (x << 1) | (z << 5) | (y << 9);
    }

    private void ValidateSize(BitArray data)
    {
        var expectedSize = CalculateSize(Height);
        if (data.Length > expectedSize)
        {
            throw new ArgumentException("BitArray of invalid length provided");
        }
    }

    private static string PathingTypeToBlock(PathingBlockType type, string dimensionId)
    {
        return type switch
        {
            PathingBlockType.Air => "minecraft:air",
            PathingBlockType.Water => "minecraft:water",
            PathingBlockType.Avoid => "minecraft:lava",
            PathingBlockType.Solid => dimensionId switch
            {
                "minecraft:the_nether" => "minecraft:netherrack",
                "minecraft:the_end" => "minecraft:end_stone",
                _ => "minecraft:stone"
            },
            _ => "minecraft:air"
        };
    }
}

