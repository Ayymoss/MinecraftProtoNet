using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace MinecraftProtoNet.Auth.Utilities;

public static class CryptographyHelper
{
    public static string GetServerHash(byte[] data)
    {
        var hash = SHA1.HashData(data);
        var littleEndianHash = hash.Reverse().ToArray();
        var num = new BigInteger(littleEndianHash);
        return num.ToString("x");
    }

    public static string GetServerHash(string serverId, byte[] sharedSecret, byte[] serverPublicKey)
    {
        var dataToHash = Encoding.UTF8.GetBytes(serverId)
            .Concat(sharedSecret)
            .Concat(serverPublicKey)
            .ToArray();
        return GetServerHash(dataToHash);
    }
}
