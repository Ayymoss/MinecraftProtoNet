using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace MinecraftProtoNet.Auth.Utilities;

public static class CryptographyHelper
{
    public static string GetServerHash(byte[] data)
    {
        var hash = SHA1.HashData(data);
        // C# BigInteger expects little-endian byte order
        var littleEndianHash = hash.Reverse().ToArray();
        var num = new BigInteger(littleEndianHash);
        
        // Java's BigInteger.toString(16) uses "-" prefix for negative numbers,
        // not two's complement like C#'s ToString("x"). Minecraft uses Java's format.
        if (num < 0)
            return "-" + BigInteger.Abs(num).ToString("x");
        return num.ToString("x");
    }

    public static string GetServerHash(string serverId, byte[] sharedSecret, byte[] serverPublicKey)
    {
        var dataToHash = Encoding.Latin1.GetBytes(serverId)
            .Concat(sharedSecret)
            .Concat(serverPublicKey)
            .ToArray();
        return GetServerHash(dataToHash);
    }
}
