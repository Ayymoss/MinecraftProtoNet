using System.Numerics;
using Humanizer;
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

    // TODO: Move these to a more appropriate location
    public static Vector3<TTo> ToVector3<TFrom, TTo>(this Vector3<TFrom> array) where TFrom : INumber<TFrom> where TTo : INumber<TTo>
    {
        return new Vector3<TTo>(TTo.CreateChecked(array.X), TTo.CreateChecked(array.Y), TTo.CreateChecked(array.Z));
    }

    /// <summary>
    /// Used for internal packet namespaces. Will not work for other namespaces.
    /// </summary>
    /// <param name="fullname"></param>
    /// <param name="packetId"></param>
    /// <returns></returns>
    public static string NamespaceToPrettyString(this string fullname, int packetId)
    {
        var parts = fullname.Split('.');
        if (parts.Length < 5) return fullname;
        return $"[white][[[/][yellow]{parts[2]}[/][white] -> [/](0x{packetId:X2}) " +
               $"[cyan]{parts[4].Replace("Packet", string.Empty).Titleize()}[/][white]]][/]";
    }
}
