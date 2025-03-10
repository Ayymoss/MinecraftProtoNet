namespace MinecraftProtoNet.Utilities;

public static class BufferExtensions
{
    public static int[] ToIntArray(this VarInt[] varInts)
    {
        var intArray = new int[varInts.Length];
        for (var i = 0; i < varInts.Length; i++)
        {
            intArray[i] = varInts[i].Value;
        }

        return intArray;
    }
}
