using System.Numerics;
using Humanizer;
using MinecraftProtoNet.Core.Models.Core;

namespace MinecraftProtoNet.Core.Utilities;

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
    /// Formats a packet's full type name into a professional human-readable string.
    /// Example: "MinecraftProtoNet.Core.Packets.Play.Clientbound.LoginPacket" -> "Play -> Login"
    /// </summary>
    public static string NamespaceToPrettyString(this string fullname, int? packetId = null)
    {
        var parts = fullname.Split('.');
        if (parts.Length < 5) return fullname;

        var state = parts[3];
        var name = parts[5].Replace("Packet", string.Empty).Titleize();
        
        return packetId.HasValue 
            ? $"[{state} : {name} (0x{packetId.Value:X2})] ->" 
            : $"[{state} : {name}] ->";
    }
}
