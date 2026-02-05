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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/utils/pathing/Avoidance.java
 */

using MinecraftProtoNet.Baritone.Api.Utils;
using MinecraftProtoNet.Baritone.Core;
using MinecraftProtoNet.Core.State;

namespace MinecraftProtoNet.Baritone.Utils.Pathing;

/// <summary>
/// Avoidance zones for pathfinding.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/utils/pathing/Avoidance.java
/// </summary>
public class Avoidance
{
    private readonly int _centerX;
    private readonly int _centerY;
    private readonly int _centerZ;
    private readonly double _coefficient;
    private readonly int _radius;
    private readonly int _radiusSq;

    public Avoidance(int centerX, int centerY, int centerZ, double coefficient, int radius)
    {
        _centerX = centerX;
        _centerY = centerY;
        _centerZ = centerZ;
        _coefficient = coefficient;
        _radius = radius;
        _radiusSq = radius * radius;
    }

    public double Coefficient(int x, int y, int z)
    {
        int xDiff = x - _centerX;
        int yDiff = y - _centerY;
        int zDiff = z - _centerZ;
        return xDiff * xDiff + yDiff * yDiff + zDiff * zDiff <= _radiusSq ? _coefficient : 1.0D;
    }

    public static List<Avoidance> Create(IPlayerContext ctx)
    {
        if (!Core.Baritone.Settings().Avoidance.Value)
        {
            return new List<Avoidance>();
        }
        var res = new List<Avoidance>();
        double mobSpawnerCoeff = Core.Baritone.Settings().MobSpawnerAvoidanceCoefficient.Value;
        double mobCoeff = Core.Baritone.Settings().MobAvoidanceCoefficient.Value;
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/utils/pathing/Avoidance.java:66-76
        if (mobSpawnerCoeff != 1.0D)
        {
            // Get locations of mob spawners from cached world
            var worldData = ctx.WorldData();
            if (worldData != null)
            {
                var cachedWorld = worldData.Cache;
                var playerFeet = ctx.PlayerFeet();
                if (playerFeet != null)
                {
                    // Get locations of mob_spawner blocks within radius
                    // Parameters: block name, maximum count, center X, center Z, max region distance squared
                    var spawnerLocations = cachedWorld.GetLocationsOf("mob_spawner", 1, playerFeet.X, playerFeet.Z, 2);
                    foreach (var spawner in spawnerLocations)
                    {
                        res.Add(new Avoidance(spawner.X, spawner.Y, spawner.Z, mobSpawnerCoeff, Core.Baritone.Settings().MobSpawnerAvoidanceRadius.Value));
                    }
                }
            }
        }
        if (mobCoeff != 1.0D)
        {
            // Get mob entities and create avoidance zones
            var world = ctx.World() as Level;
            if (world != null)
            {
                var allEntities = world.GetAllEntities();
                var player = ctx.Player() as Entity;
                foreach (var entity in allEntities)
                {
                    // Check if entity is a mob (non-player entity with health > 0)
                    // TODO: When entity type system is available, check if entity is Mob type
                    if (entity.Health > 0 && (player == null || entity.EntityId != player.EntityId))
                    {
                        var blockPos = entity.BlockPosition();
                        res.Add(new Avoidance(blockPos.X, blockPos.Y, blockPos.Z, mobCoeff, Core.Baritone.Settings().MobAvoidanceRadius.Value));
                    }
                }
            }
        }
        return res;
    }

    public void ApplySpherical(Dictionary<long, double> map)
    {
        for (int x = -_radius; x <= _radius; x++)
        {
            for (int y = -_radius; y <= _radius; y++)
            {
                for (int z = -_radius; z <= _radius; z++)
                {
                    if (x * x + y * y + z * z <= _radius * _radius)
                    {
                        long hash = BetterBlockPos.LongHash(_centerX + x, _centerY + y, _centerZ + z);
                        map[hash] = map.GetValueOrDefault(hash, 1.0D) * _coefficient;
                    }
                }
            }
        }
    }
}

