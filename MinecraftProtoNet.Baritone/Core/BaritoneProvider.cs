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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/BaritoneProvider.java
 */

using MinecraftProtoNet.Baritone.Api;
using MinecraftProtoNet.Baritone.Api.Cache;
using MinecraftProtoNet.Baritone.Api.Command;
using MinecraftProtoNet.Baritone.Api.Schematic;
using MinecraftProtoNet.Baritone.Cache;
using MinecraftProtoNet.Baritone.Command;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Core.Core;

namespace MinecraftProtoNet.Baritone.Core;

/// <summary>
/// Baritone provider implementation.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/BaritoneProvider.java
/// </summary>
public sealed class BaritoneProvider : IBaritoneProvider
{
    private readonly List<IBaritone> _all;
    private readonly IReadOnlyList<IBaritone> _allView;

    public BaritoneProvider()
    {
        _all = new List<IBaritone>();
        _allView = _all.AsReadOnly();

        // Setup chat control, just for the primary instance
        // Note: This will be called when a Minecraft client is available
    }

    public IBaritone GetPrimaryBaritone()
    {
        return _all.Count > 0 ? _all[0] : throw new InvalidOperationException("No Baritone instances available");
    }

    public IReadOnlyList<IBaritone> GetAllBaritones() => _allView;

    public IBaritone CreateBaritone(IMinecraftClient minecraft)
    {
        lock (_all)
        {
            var baritone = GetBaritoneForMinecraft(minecraft);
            if (baritone == null)
            {
                _all.Add(baritone = new Baritone(minecraft));
            }
            return baritone;
        }
    }

    public bool DestroyBaritone(IBaritone baritone)
    {
        lock (_all)
        {
            return baritone != GetPrimaryBaritone() && _all.Remove(baritone);
        }
    }

    public IWorldScanner GetWorldScanner()
    {
        return FasterWorldScanner.Instance;
    }

    public ICommandSystem GetCommandSystem()
    {
        return CommandSystem.Instance;
    }

    public ISchematicSystem GetSchematicSystem()
    {
        // Schematic system excluded for headless client
        throw new NotSupportedException("Schematic system is not supported in headless client");
    }

    private IBaritone? GetBaritoneForMinecraft(IMinecraftClient minecraft)
    {
        return _all.FirstOrDefault(b => b.GetPlayerContext().Minecraft() == minecraft);
    }
}

