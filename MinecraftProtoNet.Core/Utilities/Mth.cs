namespace MinecraftProtoNet.Core.Utilities;

/// <summary>
/// Vanilla's trigonometry, which is deliberately NOT exact: a 65536-entry float lookup table indexed by
/// angle. Every server-side movement prediction is computed with these values, so using an accurate
/// <see cref="MathF.Sin"/> instead makes the client MORE precise than vanilla and therefore divergent —
/// the table's quantisation is ~4.8e-5 in the sine, which is exactly the size of the per-tick lateral
/// offset an anticheat sees when a client substitutes real trig.
///
/// Measured against GrimAC on the local server: Java Baritone's predicted and actual velocity agreed to
/// ~1e-17 (machine epsilon) while ours diverged by 1-3e-5 on every tick of straight-line sprinting, with
/// the error concentrated entirely in the lateral component.
///
/// Reference: minecraft-26.2-REFERENCE-ONLY/net/minecraft/util/Mth.java:34-55
/// </summary>
public static class Mth
{
    private const double SinScale = 10430.378350470453;

    private static readonly float[] Sine = BuildSineTable();

    private static float[] BuildSineTable()
    {
        var sin = new float[65536];
        for (var i = 0; i < sin.Length; i++)
        {
            sin[i] = (float)Math.Sin(i / SinScale);
        }

        return sin;
    }

    /// <summary>
    /// Reference: minecraft-26.2-REFERENCE-ONLY/net/minecraft/util/Mth.java:49-51
    /// </summary>
    public static float Sin(double radians) => Sine[(int)((long)(radians * SinScale) & 65535L)];

    /// <summary>
    /// Reference: minecraft-26.2-REFERENCE-ONLY/net/minecraft/util/Mth.java:53-55
    /// </summary>
    public static float Cos(double radians) => Sine[(int)((long)(radians * SinScale + 16384.0) & 65535L)];
}
