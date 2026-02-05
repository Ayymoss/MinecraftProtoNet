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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/process/FollowProcess.java
 */

using MinecraftProtoNet.Baritone.Api;
using MinecraftProtoNet.Baritone.Api.Pathing.Goals;
using MinecraftProtoNet.Baritone.Api.Process;
using MinecraftProtoNet.Baritone.Api.Utils;
using MinecraftProtoNet.Baritone.Core;
using MinecraftProtoNet.Baritone.Pathfinding.Goals;
using MinecraftProtoNet.Baritone.Utils;
using MinecraftProtoNet.Core.State;

namespace MinecraftProtoNet.Baritone.Process;

/// <summary>
/// Follow process implementation.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/process/FollowProcess.java
/// </summary>
public class FollowProcess : BaritoneProcessHelper, IFollowProcess
{
    private Func<Entity, bool>? _filter;
    private List<Entity>? _cache;
    private bool _into; // walk straight into the target, regardless of settings

    public FollowProcess(IBaritone baritone) : base(baritone)
    {
    }

    public override PathingCommand OnTick(bool calcFailed, bool isSafeToCancel)
    {
        ScanWorld();
        if (_cache == null || _cache.Count == 0)
        {
            return new PathingCommand(null, PathingCommandType.RequestPause);
        }
        var goals = _cache.Select(Towards).ToArray();
        var goal = new GoalComposite(goals);
        return new PathingCommand(goal, PathingCommandType.RevalidateGoalAndPath);
    }

    private Goal Towards(Entity following)
    {
        var blockPos = following.BlockPosition();
        BetterBlockPos pos = new BetterBlockPos(blockPos.X, blockPos.Y, blockPos.Z);
        if (Core.Baritone.Settings().FollowOffsetDistance.Value == 0 || _into)
        {
            // pos already set
        }
        else
        {
            // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/process/FollowProcess.java:67-68
            // Calculate offset position based on direction
            // TODO: Implement GoalXZ.fromDirection when available
            // For now, use the block position directly
        }
        if (_into)
        {
            return new GoalBlock(pos);
        }
        return new GoalNear(pos, Core.Baritone.Settings().FollowRadius.Value);
    }

    private bool Followable(Entity entity)
    {
        if (entity == null)
        {
            return false;
        }
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/process/FollowProcess.java:83
        // Check if entity is alive
        // For Entity class, we assume it's alive if it exists
        // TODO: Add IsAlive property to Entity when available
        
        var player = Ctx.Player();
        if (player != null && entity.Equals(player))
        {
            return false;
        }
        
        int maxDist = Core.Baritone.Settings().FollowTargetMaxDistance.Value;
        if (maxDist != 0 && player != null)
        {
            var playerEntity = player as Entity;
            if (playerEntity != null)
            {
                var dx = entity.Position.X - playerEntity.Position.X;
                var dy = entity.Position.Y - playerEntity.Position.Y;
                var dz = entity.Position.Z - playerEntity.Position.Z;
                var distSq = dx * dx + dy * dy + dz * dz;
                if (distSq > maxDist * maxDist)
                {
                    return false;
                }
            }
        }
        
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/process/FollowProcess.java:109-110
        // Check if entity is in world
        var world = Ctx.World() as Level;
        if (world != null)
        {
            // Check if entity exists in world
            // TODO: Implement entity registry lookup when available
            return true; // For now, assume entity is in world
        }
        return false;
    }

    private void ScanWorld()
    {
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/process/FollowProcess.java:114-125
        // Get entities from world
        _cache = new List<Entity>();
        
        var world = Ctx.World() as Level;
        if (world == null) return;
        
        // Get all entities from world
        var allEntities = world.GetAllEntities();
        
        foreach (var entity in allEntities)
        {
            if (Followable(entity))
            {
                if (_filter == null || _filter(entity))
                {
                    if (!_cache.Contains(entity))
                    {
                        _cache.Add(entity);
                    }
                }
            }
        }
    }

    public override bool IsActive()
    {
        if (_filter == null)
        {
            return false;
        }
        ScanWorld();
        return _cache != null && _cache.Count > 0;
    }

    public override void OnLostControl()
    {
        _filter = null;
        _cache = null;
    }

    public override string DisplayName()
    {
        if (_cache == null || _cache.Count == 0)
        {
            return "Following (none)";
        }
        return $"Following {_cache.Count} entities";
    }

    public void Follow(System.Predicate<object> filter)
    {
        _filter = e => filter(e);
        _into = false;
    }

    public void Pickup(System.Predicate<object> filter)
    {
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/process/FollowProcess.java:159-164
        // Convert ItemStack filter to Entity filter
        // In Java, this checks if entity is ItemEntity and tests filter against the item stack
        _filter = e =>
        {
            if (e is Entity entity)
            {
                // Check if entity is an item entity (ItemEntity in Java)
                // In C#, we would check entity type or components
                // For now, we'll check if entity has item-related properties
                // TODO: When entity type system is available, check if entity is ItemEntity
                // and extract ItemStack to test against filter
                // This is a simplified version - full implementation would:
                // 1. Check if entity is ItemEntity type
                // 2. Get ItemStack from entity
                // 3. Test filter against ItemStack
                // For now, just test the filter against the entity itself
                return filter(e);
            }
            return false;
        };
        _into = true;
    }

    public IReadOnlyList<object> Following()
    {
        return _cache?.Cast<object>().ToList() ?? new List<object>();
    }

    public System.Predicate<object>? CurrentFilter()
    {
        if (_filter == null)
        {
            return null;
        }
        return obj => obj is Entity e && _filter(e);
    }

    public void Cancel()
    {
        OnLostControl();
    }
}

