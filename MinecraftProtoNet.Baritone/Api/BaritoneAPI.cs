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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/BaritoneAPI.java
 */

namespace MinecraftProtoNet.Baritone.Api;

/// <summary>
/// Exposes the IBaritoneProvider instance and the Settings instance for API usage.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/BaritoneAPI.java
/// </summary>
public static class BaritoneAPI
{
    private static readonly IBaritoneProvider Provider;
    private static readonly Settings.Settings SettingsInstance;

    static BaritoneAPI()
    {
        SettingsInstance = new Settings.Settings();
        // TODO: Read and apply settings from file when SettingsUtil is implemented

        try
        {
            // Use reflection to get BaritoneProvider
            var providerType = Type.GetType("MinecraftProtoNet.Baritone.Core.BaritoneProvider, MinecraftProtoNet.Baritone");
            if (providerType == null)
            {
                throw new InvalidOperationException("BaritoneProvider type not found");
            }
            Provider = (IBaritoneProvider)Activator.CreateInstance(providerType)!;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to create BaritoneProvider", ex);
        }
    }

    public static IBaritoneProvider GetProvider() => Provider;

    public static Settings.Settings GetSettings() => SettingsInstance;
}

