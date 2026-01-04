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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/command/argument/ArgConsumer.java
 */

using MinecraftProtoNet.Baritone.Api.Command.Argument;
using MinecraftProtoNet.Baritone.Api.Command.Manager;

namespace MinecraftProtoNet.Baritone.Command.Argument;

/// <summary>
/// Argument consumer implementation.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/command/argument/ArgConsumer.java
/// </summary>
public class ArgConsumer : IArgConsumer
{
    private readonly ICommandManager _manager;
    private readonly List<ICommandArgument> _args;
    private readonly List<ICommandArgument> _consumed = new();

    public ArgConsumer(ICommandManager manager, List<string> stringArgs)
    {
        _manager = manager;
        _args = stringArgs.Select((arg, index) => new CommandArgument(arg, index) as ICommandArgument).ToList();
    }

    public ArgConsumer(ICommandManager manager, List<ICommandArgument> args)
    {
        _manager = manager;
        _args = new List<ICommandArgument>(args);
    }

    public IReadOnlyList<ICommandArgument> GetArgs() => _args.AsReadOnly();
    public IReadOnlyList<ICommandArgument> GetConsumed() => _consumed.AsReadOnly();

    public bool Has(int num) => _args.Count >= num;
    public bool HasAny() => Has(1);
    public bool HasAtMost(int num) => _args.Count <= num;
    public bool HasAtMostOne() => HasAtMost(1);
    public bool HasExactly(int num) => _args.Count == num;
    public bool HasExactlyOne() => HasExactly(1);

    public ICommandArgument Peek(int index)
    {
        if (index < 0 || index >= _args.Count)
        {
            throw new IndexOutOfRangeException($"Index {index} out of range for {_args.Count} arguments");
        }
        return _args[index];
    }

    public ICommandArgument Peek() => Peek(0);

    public ICommandArgument Get()
    {
        if (_args.Count == 0)
        {
            throw new InvalidOperationException("No arguments left");
        }
        var arg = _args[0];
        _args.RemoveAt(0);
        _consumed.Add(arg);
        return arg;
    }

    public string GetString() => Get().GetValue();

    public T GetEnum<T>() where T : struct, Enum
    {
        var value = GetString();
        if (Enum.TryParse<T>(value, true, out var result))
        {
            return result;
        }
        throw new ArgumentException($"Invalid enum value: {value}");
    }

    public T GetAs<T>()
    {
        var value = GetString();
        // TODO: Implement proper type parsing when datatype system is available
        // For now, use basic conversions
        if (typeof(T) == typeof(string))
        {
            return (T)(object)value;
        }
        if (typeof(T) == typeof(int) && int.TryParse(value, out var intVal))
        {
            return (T)(object)intVal;
        }
        if (typeof(T) == typeof(double) && double.TryParse(value, out var doubleVal))
        {
            return (T)(object)doubleVal;
        }
        if (typeof(T) == typeof(bool) && bool.TryParse(value, out var boolVal))
        {
            return (T)(object)boolVal;
        }
        throw new InvalidOperationException($"Cannot convert '{value}' to {typeof(T).Name}");
    }

    public T GetAsOrDefault<T>(T def)
    {
        try
        {
            return GetAs<T>();
        }
        catch
        {
            return def;
        }
    }

    public T? GetAsOrNull<T>()
    {
        try
        {
            return GetAs<T>();
        }
        catch
        {
            return default(T);
        }
    }

    public void RequireMin(int min)
    {
        if (_args.Count < min)
        {
            throw new InvalidOperationException($"Requires at least {min} arguments, but only {_args.Count} available");
        }
    }

    public void RequireMax(int max)
    {
        if (_args.Count > max)
        {
            throw new InvalidOperationException($"Requires at most {max} arguments, but {_args.Count} available");
        }
    }

    public void RequireExactly(int args)
    {
        if (_args.Count != args)
        {
            throw new InvalidOperationException($"Requires exactly {args} arguments, but {_args.Count} available");
        }
    }

    public bool HasConsumed() => _consumed.Count > 0;
    public ICommandArgument Consumed() => _consumed.Count > 0 ? _consumed[^1] : throw new InvalidOperationException("No arguments consumed yet");
    public string ConsumedString() => Consumed().GetValue();

    public IArgConsumer Copy()
    {
        return new ArgConsumer(_manager, new List<ICommandArgument>(_args));
    }

    private class CommandArgument(string value, int index) : ICommandArgument
    {
        public string GetValue() => value;
        public int GetIndex() => index;

        public T GetEnum<T>() where T : struct, Enum
        {
            if (Enum.TryParse<T>(value, true, out var result))
            {
                return result;
            }
            throw new ArgumentException($"Invalid enum value: {value}");
        }
    }
}

