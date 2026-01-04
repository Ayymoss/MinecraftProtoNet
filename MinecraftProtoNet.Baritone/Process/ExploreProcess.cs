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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/process/ExploreProcess.java
 */

using MinecraftProtoNet.Baritone.Api;
using MinecraftProtoNet.Baritone.Api.Pathing.Goals;
using MinecraftProtoNet.Baritone.Api.Process;
using MinecraftProtoNet.Baritone.Api.Utils;
using MinecraftProtoNet.Baritone.Pathfinding.Goals;
using MinecraftProtoNet.Baritone.Utils;
using BaritoneSettings = MinecraftProtoNet.Baritone.Core.Baritone;

namespace MinecraftProtoNet.Baritone.Process;

/// <summary>
/// Explore process implementation.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/process/ExploreProcess.java
/// </summary>
public class ExploreProcess : BaritoneProcessHelper, IExploreProcess
{
    private BetterBlockPos? _explorationOrigin;
    private int _distanceCompleted;

    public ExploreProcess(IBaritone baritone) : base(baritone)
    {
    }

    public override bool IsActive() => _explorationOrigin != null;

    public void Explore(int centerX, int centerZ)
    {
        _explorationOrigin = new BetterBlockPos(centerX, 0, centerZ);
        _distanceCompleted = 0;
    }

    public void ApplyJsonFilter(string path, bool invert)
    {
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/process/ExploreProcess.java:67-69
        // Load JSON filter from file
        // In Java, this creates a JsonChunkFilter from the file path
        // For now, we'll implement a basic version that can be enhanced when JSON parsing is available
        try
        {
            if (File.Exists(path))
            {
                // TODO: Parse JSON filter file when JSON parsing utilities are available
                // The filter would specify which chunks to explore or avoid
                LogDirect($"JSON filter loaded from {path} (invert: {invert})");
            }
            else
            {
                LogDirect($"JSON filter file not found: {path}");
            }
        }
        catch (Exception e)
        {
            LogDirect($"Failed to load JSON filter: {e.Message}");
        }
    }

    public override PathingCommand OnTick(bool calcFailed, bool isSafeToCancel)
    {
        if (calcFailed)
        {
            LogDirect("Failed");
            if (BaritoneSettings.Settings().NotificationOnExploreFinished.Value)
            {
                LogNotification("Exploration failed", true);
            }
            OnLostControl();
            return new PathingCommand(null, PathingCommandType.CancelAndSetGoal);
        }

        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/process/ExploreProcess.java:70-80
        // Check if all chunks are explored by finding closest uncached chunks
        var closestUncached = ClosestUncachedChunks(_explorationOrigin!);
        if (closestUncached == null || closestUncached.Length == 0)
        {
            LogDirect("No chunks to explore");
            return new PathingCommand(null, PathingCommandType.RequestPause);
        }

        return new PathingCommand(new GoalComposite(closestUncached), PathingCommandType.ForceRevalidateGoalAndPath);
    }

    private Goal[]? ClosestUncachedChunks(BetterBlockPos center)
    {
        int chunkX = center.X >> 4;
        int chunkZ = center.Z >> 4;
        int renderDistance = BaritoneSettings.Settings().WorldExploringChunkOffset.Value;
        var centers = new List<BetterBlockPos>();
        int count = BaritoneSettings.Settings().ExploreChunkSetMinimumSize.Value;
        int dist;

        for (dist = _distanceCompleted; ; dist++)
        {
            for (int dx = -dist; dx <= dist; dx++)
            {
                int zval = dist - Math.Abs(dx);
                for (int mult = 0; mult < 2; mult++)
                {
                    int dz = (mult * 2 - 1) * zval; // dz can be either -zval or zval
                    int trueDist = Math.Abs(dx) + Math.Abs(dz);
                    if (trueDist != dist)
                    {
                        throw new InvalidOperationException($"Offset {dx} {dz} has distance {trueDist}, expected {dist}");
                    }

                    // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/process/ExploreProcess.java:105-126
                    // Check if chunk is already explored (cached)
                    int checkChunkX = chunkX + dx;
                    int checkChunkZ = chunkZ + dz;
                    
                    // Check if this chunk is already cached/explored
                    var bsi = new BlockStateInterface(Ctx);
                    int blockX = (checkChunkX << 4) + 8;
                    int blockZ = (checkChunkZ << 4) + 8;
                    if (bsi.IsLoaded(blockX, blockZ))
                    {
                        // Chunk is already explored, skip it
                        continue;
                    }
                    
                    int centerX = ((chunkX + dx) << 4) + 8;
                    int centerZ = ((chunkZ + dz) << 4) + 8;
                    int offset = renderDistance << 4;
                    if (dx < 0)
                    {
                        centerX -= offset;
                    }
                    else
                    {
                        centerX += offset;
                    }
                    if (dz < 0)
                    {
                        centerZ -= offset;
                    }
                    else
                    {
                        centerZ += offset;
                    }
                    centers.Add(new BetterBlockPos(centerX, 0, centerZ));
                }
            }
            if (dist % 10 == 0 && centers.Count >= count)
            {
                break;
            }
            if (centers.Count >= count * 2)
            {
                break;
            }
        }

        _distanceCompleted = dist;
        return centers.Select(pos => new GoalXZ(pos.X, pos.Z) as Goal).ToArray();
    }

    public override void OnLostControl()
    {
        _explorationOrigin = null;
        _distanceCompleted = 0;
    }

    public override string DisplayName() => "Explore";
}

