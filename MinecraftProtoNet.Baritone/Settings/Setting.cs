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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/Settings.java:1558-1619
 */

namespace MinecraftProtoNet.Baritone.Settings;

/// <summary>
/// A setting with a value and default value.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/Settings.java:1558-1619
/// </summary>
public class Setting<T>
{
    public T Value;
    public readonly T DefaultValue;
    private string? _name;
    private bool _javaOnly;

    public Setting(T value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value), "Cannot determine value type class from null");
        }
        Value = value;
        DefaultValue = value;
        _javaOnly = false;
    }

    /// <summary>
    /// Gets the name of this setting.
    /// </summary>
    public string? GetName() => _name;

    internal void SetName(string name) => _name = name;

    internal void SetJavaOnly(bool javaOnly) => _javaOnly = javaOnly;

    /// <summary>
    /// Gets the value class type.
    /// </summary>
    public Type GetValueClass() => typeof(T);

    /// <summary>
    /// Resets this setting to its default value.
    /// </summary>
    public void Reset()
    {
        Value = DefaultValue;
    }

    /// <summary>
    /// Returns whether this setting is Java-only.
    /// </summary>
    public bool IsJavaOnly() => _javaOnly;

    public override string ToString()
    {
        return $"{_name ?? "Unknown"} = {Value}";
    }
}

