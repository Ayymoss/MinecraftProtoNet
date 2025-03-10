namespace MinecraftProtoNet.Packets.Base.Definitions;

public class LightData(
    long[] skyLightMask,
    long[] blockLightMask,
    long[] emptySkyLightMask,
    long[] emptyBlockLightMask,
    byte[][] skyLight,
    byte[][] blockLight)
{
    public long[] SkyLightMask { get; set; } = skyLightMask;
    public long[] BlockLightMask { get; set; } = blockLightMask;
    public long[] EmptySkyLightMask { get; set; } = emptySkyLightMask;
    public long[] EmptyBlockLightMask { get; set; } = emptyBlockLightMask;
    public byte[][] SkyLight { get; set; } = skyLight;
    public byte[][] BlockLight { get; set; } = blockLight;
}
