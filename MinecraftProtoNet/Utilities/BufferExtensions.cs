using System.Numerics;
using MinecraftProtoNet.Models.Core;

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

    public static long[] ToLongArray(this VarLong[] varInts)
    {
        var intArray = new long[varInts.Length];
        for (var i = 0; i < varInts.Length; i++)
        {
            intArray[i] = varInts[i].Value;
        }

        return intArray;
    }

    // TODO: Move this to a more appropriate location
    public static Vector3<TTo> ToVector3<TFrom, TTo>(this Vector3<TFrom> array) where TFrom : INumber<TFrom> where TTo : INumber<TTo>
    {
        return new Vector3<TTo>(TTo.CreateChecked(array.X), TTo.CreateChecked(array.Y), TTo.CreateChecked(array.Z));
    }
}
