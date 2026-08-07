using System.Security.Cryptography;

namespace MinecraftProtoNet.Core.Utilities;

/// <summary>
/// AES/CFB8 stream cipher for the Minecraft protocol, using the BCL's own CFB8 implementation.
///
/// Minecraft encrypts the post-login socket with AES-128 in CFB8, where the IV is the shared secret itself and
/// one continuous feedback register runs for the whole connection, per direction.
///
/// The crypto here is entirely <see cref="Aes.EncryptCfb(ReadOnlySpan{byte}, ReadOnlySpan{byte}, Span{byte}, PaddingMode, int)"/>
/// / <c>DecryptCfb</c> with <c>feedbackSizeInBits: 8</c> — hardware-accelerated and maintained by the platform.
/// The only thing this class adds is carrying the register across calls, because those APIs are one-shot and
/// restart from the supplied IV every time. That is trivial for CFB8: the next register is simply the last 16
/// CIPHERTEXT bytes of what was just processed (the output when encrypting, the input when decrypting), and
/// for a chunk shorter than a block, the register shifts left by the chunk length and the ciphertext is
/// appended. Verified against the NIST SP 800-38A CFB8-AES128 vectors, including chunked feeding.
///
/// Two BCL routes were rejected:
///   * <c>CipherMode.CFB</c> + <c>CreateEncryptor()</c> — the returned ICryptoTransform reports a 16-byte block
///     size and throws "TransformBlock may only process bytes in block sized increments", so it cannot handle a
///     byte-granular socket stream where packets and reads are any length.
///   * Calling EncryptCfb/DecryptCfb without carrying the register — they restart from the IV each call, which
///     silently corrupts everything after the first read.
///
/// This replaced a BouncyCastle CfbBlockCipher whose buffered wrapper allocated a byte[] per call and copied it
/// into the caller's buffer, and which shifted its register 15 bytes for every payload byte. Profiling put 32%
/// of a connect-phase trace in Buffer.MemmoveInternal there, and the resulting backlog delayed inbound block
/// updates by up to 1.6s after joining — long enough that the bot re-clicked doors it thought had not opened.
/// </summary>
public sealed class AesCfb8Transform : ICryptoTransform
{
    private const int BlockSize = 16;

    private readonly Aes _aes;
    private readonly bool _encrypting;

    /// <summary>The live CFB feedback register; seeded with the IV, then always the last 16 ciphertext bytes.</summary>
    private readonly byte[] _register = new byte[BlockSize];

    /// <param name="key">The 16-byte shared secret. Minecraft uses it as both the AES key and the initial IV.</param>
    /// <param name="encrypting">True for the outbound (client to server) stream, false for inbound.</param>
    public AesCfb8Transform(byte[] key, bool encrypting) : this(key, key, encrypting)
    {
    }

    /// <summary>
    /// Separate key and IV. Minecraft always uses IV = key (see the other constructor); this overload exists so
    /// the implementation can be validated against the standard AES-CFB8 test vectors, which use a distinct IV.
    /// </summary>
    public AesCfb8Transform(byte[] key, byte[] iv, bool encrypting)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(iv);
        if (key.Length != BlockSize)
        {
            throw new ArgumentException($"Minecraft AES/CFB8 requires a {BlockSize}-byte key, got {key.Length}.", nameof(key));
        }
        if (iv.Length != BlockSize)
        {
            throw new ArgumentException($"AES/CFB8 requires a {BlockSize}-byte IV, got {iv.Length}.", nameof(iv));
        }

        _encrypting = encrypting;
        iv.CopyTo(_register, 0);

        _aes = Aes.Create();
        _aes.Key = key;
    }

    public int InputBlockSize => 1;  // CFB8 is a byte-granular stream cipher.
    public int OutputBlockSize => 1;
    public bool CanTransformMultipleBlocks => true;
    public bool CanReuseTransform => false;

    public int TransformBlock(byte[] inputBuffer, int inputOffset, int inputCount, byte[] outputBuffer, int outputOffset)
    {
        ArgumentNullException.ThrowIfNull(inputBuffer);
        ArgumentNullException.ThrowIfNull(outputBuffer);
        if (inputCount <= 0) return 0;
        if (outputBuffer.Length - outputOffset < inputCount)
        {
            throw new ArgumentException("Output buffer is too small for the transformed data.", nameof(outputBuffer));
        }

        var input = inputBuffer.AsSpan(inputOffset, inputCount);
        var output = outputBuffer.AsSpan(outputOffset, inputCount);

        // When decrypting, the ciphertext is the INPUT — capture its tail before transforming, in case the
        // caller passed the same array for input and output.
        Span<byte> tail = stackalloc byte[BlockSize];
        int tailLength = Math.Min(inputCount, BlockSize);
        if (!_encrypting)
        {
            input[^tailLength..].CopyTo(tail);
        }

        if (_encrypting)
        {
            _aes.EncryptCfb(input, _register, output, PaddingMode.None, feedbackSizeInBits: 8);
            output[^tailLength..].CopyTo(tail); // ciphertext is the OUTPUT here
        }
        else
        {
            _aes.DecryptCfb(input, _register, output, PaddingMode.None, feedbackSizeInBits: 8);
        }

        AdvanceRegister(tail[..tailLength]);
        return inputCount;
    }

    /// <summary>
    /// Slides the feedback register along by the ciphertext just processed. A full block replaces it outright;
    /// anything shorter shifts the register left and appends.
    /// </summary>
    private void AdvanceRegister(ReadOnlySpan<byte> ciphertextTail)
    {
        if (ciphertextTail.Length >= BlockSize)
        {
            ciphertextTail[^BlockSize..].CopyTo(_register);
            return;
        }

        var keep = BlockSize - ciphertextTail.Length;
        _register.AsSpan(ciphertextTail.Length, keep).CopyTo(_register);
        ciphertextTail.CopyTo(_register.AsSpan(keep));
    }

    public byte[] TransformFinalBlock(byte[] inputBuffer, int inputOffset, int inputCount)
    {
        if (inputCount <= 0) return [];

        var output = new byte[inputCount];
        TransformBlock(inputBuffer, inputOffset, inputCount, output, 0);
        return output;
    }

    public void Dispose()
    {
        _aes.Dispose();
        CryptographicOperations.ZeroMemory(_register);
    }
}
