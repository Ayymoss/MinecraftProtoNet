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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/command/argument/IArgConsumer.java
 */

namespace MinecraftProtoNet.Baritone.Api.Command.Argument;

/// <summary>
/// Interface for argument consumer.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/command/argument/IArgConsumer.java
/// </summary>
public interface IArgConsumer
{
    /// <summary>
    /// Gets the arguments.
    /// </summary>
    IReadOnlyList<ICommandArgument> GetArgs();

    /// <summary>
    /// Gets the consumed arguments.
    /// </summary>
    IReadOnlyList<ICommandArgument> GetConsumed();

    /// <summary>
    /// Checks if there are at least num arguments left.
    /// </summary>
    bool Has(int num);

    /// <summary>
    /// Checks if there is at least 1 argument left.
    /// </summary>
    bool HasAny();

    /// <summary>
    /// Checks if there are at most num arguments left.
    /// </summary>
    bool HasAtMost(int num);

    /// <summary>
    /// Checks if there is at most 1 argument left.
    /// </summary>
    bool HasAtMostOne();

    /// <summary>
    /// Checks if there are exactly num arguments left.
    /// </summary>
    bool HasExactly(int num);

    /// <summary>
    /// Checks if there is exactly 1 argument left.
    /// </summary>
    bool HasExactlyOne();

    /// <summary>
    /// Peeks at the argument at the specified index without consuming it.
    /// </summary>
    ICommandArgument Peek(int index);

    /// <summary>
    /// Peeks at the next argument without consuming it.
    /// </summary>
    ICommandArgument Peek();

    /// <summary>
    /// Gets the next argument and consumes it.
    /// </summary>
    ICommandArgument Get();

    /// <summary>
    /// Gets the value of the next argument and consumes it.
    /// </summary>
    string GetString();

    /// <summary>
    /// Gets an enum value from the next argument.
    /// </summary>
    T GetEnum<T>() where T : struct, Enum;

    /// <summary>
    /// Gets a value parsed as the specified type.
    /// </summary>
    T GetAs<T>();

    /// <summary>
    /// Gets a value parsed as the specified type, or returns default if parsing fails.
    /// </summary>
    T GetAsOrDefault<T>(T def);

    /// <summary>
    /// Gets a value parsed as the specified type, or returns null if parsing fails.
    /// </summary>
    T? GetAsOrNull<T>();

    /// <summary>
    /// Requires at least min arguments.
    /// </summary>
    void RequireMin(int min);

    /// <summary>
    /// Requires at most max arguments.
    /// </summary>
    void RequireMax(int max);

    /// <summary>
    /// Requires exactly args arguments.
    /// </summary>
    void RequireExactly(int args);

    /// <summary>
    /// Returns if this consumer has consumed at least one argument.
    /// </summary>
    bool HasConsumed();

    /// <summary>
    /// Gets the last consumed argument.
    /// </summary>
    ICommandArgument Consumed();

    /// <summary>
    /// Gets the value of the last consumed argument.
    /// </summary>
    string ConsumedString();

    /// <summary>
    /// Returns a copy of this consumer.
    /// </summary>
    IArgConsumer Copy();
}

