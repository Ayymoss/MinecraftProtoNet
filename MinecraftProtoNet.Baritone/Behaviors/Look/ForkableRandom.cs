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
 * Ported from: baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/behavior/look/ForkableRandom.java
 */

namespace MinecraftProtoNet.Baritone.Behaviors.Look;

/// <summary>
/// Xoshiro256++ producing doubles, with <see cref="Fork"/> spawning a copy that shares the source's
/// state at fork time (for predictive aim lookahead). Reference: ForkableRandom.java.
/// </summary>
public sealed class ForkableRandom
{
    private const double DoubleUnit = 1.0 / (1L << 53); // 0x1.0p-53

    private readonly long[] _s;

    public ForkableRandom() : this(Environment.TickCount64 ^ System.Diagnostics.Stopwatch.GetTimestamp())
    {
    }

    public ForkableRandom(long seedIn)
    {
        long seed = seedIn;
        long Splitmix64()
        {
            seed += unchecked((long)0x9e3779b97f4a7c15UL);
            long z = seed;
            z = (z ^ (z >>> 30)) * unchecked((long)0xbf58476d1ce4e5b9UL);
            z = (z ^ (z >>> 27)) * unchecked((long)0x94d049bb133111ebUL);
            return z ^ (z >>> 31);
        }
        _s = [Splitmix64(), Splitmix64(), Splitmix64(), Splitmix64()];
    }

    private ForkableRandom(long[] s)
    {
        _s = s;
    }

    public double NextDouble()
    {
        return (Next() >>> 11) * DoubleUnit;
    }

    public long Next()
    {
        long result = Rotl(_s[0] + _s[3], 23) + _s[0];
        long t = _s[1] << 17;
        _s[2] ^= _s[0];
        _s[3] ^= _s[1];
        _s[1] ^= _s[2];
        _s[0] ^= _s[3];
        _s[2] ^= t;
        _s[3] = Rotl(_s[3], 45);
        return result;
    }

    public ForkableRandom Fork()
    {
        return new ForkableRandom((long[])_s.Clone());
    }

    private static long Rotl(long x, int k)
    {
        return (x << k) | (x >>> (64 - k));
    }
}
