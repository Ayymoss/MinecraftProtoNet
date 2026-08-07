using System.Security.Cryptography;
using FluentAssertions;
using MinecraftProtoNet.Core.Utilities;
using Xunit;

namespace MinecraftProtoNet.Tests.Core;

/// <summary>
/// Validates the BCL AES/CFB8 stream cipher that replaced BouncyCastle on the protocol socket.
///
/// The important test is <see cref="MatchesNistCfb8Vectors"/>: it checks the implementation against the
/// published NIST SP 800-38A CFB8-AES128 vectors rather than against itself, so a cipher that is wrong but
/// self-consistent (which a round-trip test alone would happily accept) cannot pass.
/// </summary>
public class AesCfb8TransformTests
{
    // NIST SP 800-38A, F.3.7 (CFB8-AES128.Encrypt) / F.3.8 (Decrypt).
    private static readonly byte[] NistKey = Convert.FromHexString("2b7e151628aed2a6abf7158809cf4f3c");
    private static readonly byte[] NistIv = Convert.FromHexString("000102030405060708090a0b0c0d0e0f");
    private static readonly byte[] NistPlain = Convert.FromHexString("6bc1bee22e409f96e93d7e117393172aae2d");
    private static readonly byte[] NistCipher = Convert.FromHexString("3b79424c9c0dd436bace9e0ed4586a4f32b9");

    [Fact]
    public void MatchesNistCfb8Vectors()
    {
        using var encryptor = new AesCfb8Transform(NistKey, NistIv, encrypting: true);
        var encrypted = new byte[NistPlain.Length];
        encryptor.TransformBlock(NistPlain, 0, NistPlain.Length, encrypted, 0);
        encrypted.Should().Equal(NistCipher, "AES-CFB8 encryption must match the NIST SP 800-38A vector");

        using var decryptor = new AesCfb8Transform(NistKey, NistIv, encrypting: false);
        var decrypted = new byte[NistCipher.Length];
        decryptor.TransformBlock(NistCipher, 0, NistCipher.Length, decrypted, 0);
        decrypted.Should().Equal(NistPlain, "AES-CFB8 decryption must match the NIST SP 800-38A vector");
    }

    /// <summary>
    /// The feedback register must carry across calls: CryptoStream feeds the transform in arbitrary chunks as
    /// data arrives from the socket, so a cipher that resets per call would corrupt everything after the first
    /// read. This is exactly the trap that Aes.EncryptCfb (one-shot) would fall into.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(16)]
    [InlineData(17)]
    public void IsIdenticalWhenFedInChunks(int chunkSize)
    {
        var key = RandomNumberGenerator.GetBytes(16);
        var payload = RandomNumberGenerator.GetBytes(200);

        using var oneShot = new AesCfb8Transform(key, encrypting: true);
        var expected = new byte[payload.Length];
        oneShot.TransformBlock(payload, 0, payload.Length, expected, 0);

        using var chunked = new AesCfb8Transform(key, encrypting: true);
        var actual = new byte[payload.Length];
        for (int offset = 0; offset < payload.Length; offset += chunkSize)
        {
            var count = Math.Min(chunkSize, payload.Length - offset);
            chunked.TransformBlock(payload, offset, count, actual, offset);
        }

        actual.Should().Equal(expected, "the feedback register must persist across TransformBlock calls");
    }

    /// <summary>Minecraft seeds both directions with the shared secret as key AND IV.</summary>
    [Fact]
    public void RoundTripsWithKeyAsIv()
    {
        var sharedSecret = RandomNumberGenerator.GetBytes(16);
        var payload = RandomNumberGenerator.GetBytes(1024);

        using var encryptor = new AesCfb8Transform(sharedSecret, encrypting: true);
        using var decryptor = new AesCfb8Transform(sharedSecret, encrypting: false);

        var encrypted = new byte[payload.Length];
        encryptor.TransformBlock(payload, 0, payload.Length, encrypted, 0);
        encrypted.Should().NotEqual(payload);

        var decrypted = new byte[payload.Length];
        decryptor.TransformBlock(encrypted, 0, encrypted.Length, decrypted, 0);
        decrypted.Should().Equal(payload);
    }

    [Fact]
    public void RejectsWrongSizedKeyAndIv()
    {
        var act = () => new AesCfb8Transform(new byte[8], encrypting: true);
        act.Should().Throw<ArgumentException>();

        var actIv = () => new AesCfb8Transform(new byte[16], new byte[8], encrypting: true);
        actIv.Should().Throw<ArgumentException>();
    }
}
