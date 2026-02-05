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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/utils/BetterBlockPos.java
 */

namespace MinecraftProtoNet.Baritone.Api.Utils;

/// <summary>
/// A better BlockPos that has fewer hash collisions and slightly more performant offsets.
/// Reference: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/utils/BetterBlockPos.java
/// </summary>
public sealed class BetterBlockPos
{
    private const int NumXBits = 26;
    private const int NumZBits = NumXBits;
    private const int NumYBits = 64 - NumXBits - NumZBits;
    private const int YShift = NumZBits;
    private const int XShift = YShift + NumYBits;
    private const long XMask = (1L << NumXBits) - 1L;
    private const long YMask = (1L << NumYBits) - 1L;
    private const long ZMask = (1L << NumZBits) - 1L;

    public static readonly BetterBlockPos Origin = new(0, 0, 0);

    public readonly int X;
    public readonly int Y;
    public readonly int Z;

    public BetterBlockPos(int x, int y, int z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public BetterBlockPos(double x, double y, double z)
        : this((int)Math.Floor(x), (int)Math.Floor(y), (int)Math.Floor(z))
    {
    }

    /// <summary>
    /// Like constructor but returns null if pos is null.
    /// </summary>
    public static BetterBlockPos? From((int X, int Y, int Z) pos)
    {
        return new BetterBlockPos(pos.X, pos.Y, pos.Z);
    }

    public override int GetHashCode()
    {
        return (int)LongHash(X, Y, Z);
    }

    public static long LongHash(BetterBlockPos pos)
    {
        return LongHash(pos.X, pos.Y, pos.Z);
    }

    public static long LongHash(int x, int y, int z)
    {
        // Reference: baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/utils/BetterBlockPos.java:90-109
        long hash = 3241;
        hash = 3457689L * hash + x;
        hash = 8734625L * hash + y;
        hash = 2873465L * hash + z;
        return hash;
    }

    public override bool Equals(object? obj)
    {
        if (obj is BetterBlockPos other)
        {
            return other.X == X && other.Y == Y && other.Z == Z;
        }
        if (obj is (int x, int y, int z))
        {
            return x == X && y == Y && z == Z;
        }
        return false;
    }

    public BetterBlockPos Above()
    {
        return new BetterBlockPos(X, Y + 1, Z);
    }

    public BetterBlockPos Above(int amt)
    {
        return amt == 0 ? this : new BetterBlockPos(X, Y + amt, Z);
    }

    public BetterBlockPos Below()
    {
        return new BetterBlockPos(X, Y - 1, Z);
    }

    public BetterBlockPos Below(int amt)
    {
        return amt == 0 ? this : new BetterBlockPos(X, Y - amt, Z);
    }

    public BetterBlockPos North()
    {
        return new BetterBlockPos(X, Y, Z - 1);
    }

    public BetterBlockPos North(int amt)
    {
        return amt == 0 ? this : new BetterBlockPos(X, Y, Z - amt);
    }

    public BetterBlockPos South()
    {
        return new BetterBlockPos(X, Y, Z + 1);
    }

    public BetterBlockPos South(int amt)
    {
        return amt == 0 ? this : new BetterBlockPos(X, Y, Z + amt);
    }

    public BetterBlockPos East()
    {
        return new BetterBlockPos(X + 1, Y, Z);
    }

    public BetterBlockPos East(int amt)
    {
        return amt == 0 ? this : new BetterBlockPos(X + amt, Y, Z);
    }

    public BetterBlockPos West()
    {
        return new BetterBlockPos(X - 1, Y, Z);
    }

    public BetterBlockPos West(int amt)
    {
        return amt == 0 ? this : new BetterBlockPos(X - amt, Y, Z);
    }

    public double DistanceSq(BetterBlockPos to)
    {
        double dx = X - to.X;
        double dy = Y - to.Y;
        double dz = Z - to.Z;
        return dx * dx + dy * dy + dz * dz;
    }

    public double DistanceTo(BetterBlockPos to)
    {
        return Math.Sqrt(DistanceSq(to));
    }

    public override string ToString()
    {
        return $"BetterBlockPos{{x={X},y={Y},z={Z}}}";
    }

    public static long SerializeToLong(int x, int y, int z)
    {
        return ((long)x & XMask) << XShift | ((long)y & YMask) << YShift | ((long)z & ZMask);
    }

    public static BetterBlockPos DeserializeFromLong(long serialized)
    {
        int x = (int)((serialized << (64 - XShift - NumXBits)) >> (64 - NumXBits));
        int y = (int)((serialized << (64 - YShift - NumYBits)) >> (64 - NumYBits));
        int z = (int)((serialized << (64 - NumZBits)) >> (64 - NumZBits));
        return new BetterBlockPos(x, y, z);
    }

    public void Deconstruct(out int x, out int y, out int z)
    {
        x = X;
        y = Y;
        z = Z;
    }
}

