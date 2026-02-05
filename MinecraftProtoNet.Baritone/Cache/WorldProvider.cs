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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/cache/WorldProvider.java
 */

using MinecraftProtoNet.Baritone.Api;
using MinecraftProtoNet.Baritone.Api.Cache;
using MinecraftProtoNet.Baritone.Api.Utils;
using MinecraftProtoNet.Core.State;

namespace MinecraftProtoNet.Baritone.Cache;

/// <summary>
/// World provider implementation.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/cache/WorldProvider.java
/// </summary>
public class WorldProvider : IWorldProvider
{
    private static readonly Dictionary<string, WorldData> WorldCache = new();

    private readonly IBaritone _baritone;
    private readonly IPlayerContext _ctx;
    private WorldData? _currentWorld;
    private Level? _mcWorld; // Track world to detect broken load/unload hooks

    public WorldProvider(IBaritone baritone)
    {
        _baritone = baritone;
        _ctx = baritone.GetPlayerContext();
    }

    public IWorldData? GetCurrentWorld()
    {
        DetectAndHandleBrokenLoading();
        return _currentWorld;
    }

    public void IfWorldLoaded(Action<IWorldData> callback)
    {
        var world = GetCurrentWorld();
        if (world != null)
        {
            callback(world);
        }
    }

    public void InitWorld(Level world)
    {
        var dirs = GetSaveDirectories(world);
        if (dirs == null)
        {
            return;
        }

        var (worldDir, readmeDir) = dirs.Value;

        try
        {
            Directory.CreateDirectory(readmeDir);
            File.WriteAllText(
                Path.Combine(readmeDir, "readme.txt"),
                "https://github.com/cabaletta/baritone\n"
            );
        }
        catch (Exception)
        {
            // Ignore
        }

        var worldDataDir = GetWorldDataDirectory(worldDir, world);
        try
        {
            Directory.CreateDirectory(worldDataDir);
        }
        catch (Exception)
        {
            // Ignore
        }

        lock (WorldCache)
        {
            _currentWorld = WorldCache.GetValueOrDefault(worldDataDir);
            if (_currentWorld == null)
            {
                var dimType = world.DimensionType;
                var dimensionId = GetDimensionId(world); // Will be implemented when dimension ID is available
                _currentWorld = new WorldData(
                    worldDataDir,
                    dimType.MinY,
                    dimType.Height,
                    false, // hasCeiling - will be determined from dimension
                    dimensionId
                );
                WorldCache[worldDataDir] = _currentWorld;
            }
        }

        _mcWorld = _ctx.World() as Level;
    }

    public void CloseWorld()
    {
        var world = _currentWorld;
        _currentWorld = null;
        _mcWorld = null;
        world?.OnClose();
    }

    private string GetWorldDataDirectory(string parent, Level world)
    {
        var dimensionId = GetDimensionId(world);
        var height = world.DimensionType.Height;
        
        // Parse dimension ID (e.g., "minecraft:overworld" -> namespace "minecraft", path "overworld")
        var parts = dimensionId.Split(':');
        var ns = parts.Length > 1 ? parts[0] : "minecraft";
        var path = parts.Length > 1 ? parts[1] : parts[0];
        
        return Path.Combine(parent, ns, $"{path}_{height}");
    }

    private string GetDimensionId(Level world)
    {
        // Will be properly implemented when dimension ID is available in Core
        // For now, return a default
        return "minecraft:overworld";
    }

    private (string WorldDir, string ReadmeDir)? GetSaveDirectories(Level world)
    {
        string worldDir;
        string readmeDir;

        // For headless client, we'll use a simpler directory structure
        // Singleplayer/multiplayer detection will be added when integrated
        var baritoneDir = ((Core.Baritone)_baritone).GetDirectory();
        worldDir = Path.Combine(baritoneDir, "worlds", "default");
        readmeDir = baritoneDir;

        return (worldDir, readmeDir);
    }

    private void DetectAndHandleBrokenLoading()
    {
        var currentWorld = _ctx.World() as Level;
        if (_mcWorld != currentWorld)
        {
            if (_currentWorld != null)
            {
                Console.WriteLine("mc.world unloaded unnoticed! Unloading Baritone cache now.");
                CloseWorld();
            }
            if (currentWorld != null)
            {
                Console.WriteLine("mc.world loaded unnoticed! Loading Baritone cache now.");
                InitWorld(currentWorld);
            }
        }
        else if (_currentWorld == null && currentWorld != null)
        {
            Console.WriteLine("Retrying to load Baritone cache");
            InitWorld(currentWorld);
        }
    }
}

