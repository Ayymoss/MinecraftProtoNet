using System.IO.Compression;
using System.Net.Sockets;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Services;
using MinecraftProtoNet.Utilities;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;

using MinecraftProtoNet.Core.Abstractions;

namespace MinecraftProtoNet.Core;

public sealed class Connection : IPacketSender, IDisposable
{
    // --- Constants ---
    private const bool EnableDebugLogging = true;
    private const int MaxPacketSize = 2 * 1024 * 1024;
    private const int MaxUncompressedPacketSize = 8 * 1024 * 1024;

    // --- Network ---
    private readonly TcpClient _client = new();
    private readonly ILogger<Connection> _logger = LoggingConfiguration.CreateLogger<Connection>();
    private NetworkStream? _rawStream;
    private bool _useEncryption;
    private int _compressionThreshold = -1;
    private bool UseCompression => _compressionThreshold >= 0;

    // --- Encryption ---
    private CryptoStream? _decryptStream;
    private CryptoStream? _encryptStream;
    private ICryptoTransform? _decryptTransform;
    private ICryptoTransform? _encryptTransform;

    private bool _disposed;

    #region Setup

    public async Task ConnectAsync(string host, int port, CancellationToken cancellationToken = default)
    {
        if (_client.Connected)
        {
            _logger.LogWarning("Already connected");
            return;
        }

        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            await _client.ConnectAsync(host, port, cancellationToken);
            _rawStream = _client.GetStream();
            _logger.LogInformation("Connected to {Host}:{Port}", host, port);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to {Host}:{Port}", host, port);
            throw;
        }
    }

    public void EnableEncryption(byte[] sharedSecret)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_rawStream == null) throw new InvalidOperationException("Cannot enable encryption before connecting.");
        if (sharedSecret is not { Length: 16 })
            throw new ArgumentException("Shared secret must be 16 bytes for AES-128.", nameof(sharedSecret));
        if (_useEncryption)
        {
            _logger.LogWarning("Encryption is already enabled");
            return;
        }

        try
        {
            var keyParam = new KeyParameter(sharedSecret);
            var ivParam = new ParametersWithIV(keyParam, sharedSecret, 0, 16);

            var decryptEngine = new AesEngine();
            var decryptCipher = new CfbBlockCipher(decryptEngine, 8);
            decryptCipher.Init(false, ivParam);
            _decryptTransform = new BouncyCastleCryptoTransform(decryptCipher);

            var encryptEngine = new AesEngine();
            var encryptCipher = new CfbBlockCipher(encryptEngine, 8);
            encryptCipher.Init(true, ivParam);
            _encryptTransform = new BouncyCastleCryptoTransform(encryptCipher);

            _decryptStream = new CryptoStream(_rawStream, _decryptTransform, CryptoStreamMode.Read, leaveOpen: true);
            _encryptStream = new CryptoStream(_rawStream, _encryptTransform, CryptoStreamMode.Write, leaveOpen: true);
            _useEncryption = true;
            _logger.LogInformation("AES/CFB8 encryption enabled (using BouncyCastle)");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize AES streams");
            throw;
        }
    }

    public void EnableCompression(int threshold)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_rawStream == null) throw new InvalidOperationException("Cannot enable compression before connecting.");

        if (_compressionThreshold == threshold)
        {
            _logger.LogWarning("Compression threshold already set to: {Threshold}", threshold);
            return;
        }

        if (threshold < 0)
        {
            _logger.LogInformation("Compression disabled (threshold {Threshold})", threshold);
            _compressionThreshold = -1;
            return;
        }

        _compressionThreshold = threshold;
        _logger.LogInformation("Compression enabled with threshold: {Threshold}", threshold);
    }

    #endregion

    #region Read & Send Methods

    public async Task<byte[]> ReadPacketBytesAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var inputStream = GetInputStream();

        // 1. Read Total Packet Length (includes Data Length VarInt + compressed/uncompressed data)
        var totalPacketLength = await ReadVarIntAsync(inputStream, cancellationToken);

        if (totalPacketLength <= 0) throw new InvalidDataException($"Invalid total packet length received: {totalPacketLength}");
        if (totalPacketLength > MaxPacketSize)
            throw new InvalidDataException($"Total packet length {totalPacketLength} exceeds maximum allowed size {MaxPacketSize}.");

        // 2. Read the entire packet content based on totalPacketLength
        var packetContentBuffer = new byte[totalPacketLength];
        await inputStream.ReadExactlyAsync(packetContentBuffer, 0, totalPacketLength, cancellationToken);

        // 3. Process based on compression state
        if (!UseCompression)
        {
            return packetContentBuffer;
        }

        using var contentStream = new MemoryStream(packetContentBuffer);

        // 3a. Read Data Length (Length of Uncompressed Packet ID + Data)
        var dataLength = await ReadVarIntAsync(contentStream, cancellationToken); // Sync read from MemoryStream is fine

        if (dataLength == 0)
        {
            var payloadLength = (int)(contentStream.Length - contentStream.Position);

            var finalPayload = new byte[payloadLength];
            await contentStream.ReadExactlyAsync(finalPayload, 0, payloadLength, cancellationToken);
            return finalPayload;
        }

        if (dataLength < _compressionThreshold)
        {
            _logger.LogWarning("Received compressed packet with Data Length ({DataLength}) below threshold ({Threshold})",
                dataLength, _compressionThreshold);
        }

        if (dataLength > MaxUncompressedPacketSize)
        {
            throw new InvalidDataException(
                $"Declared uncompressed data length {dataLength} exceeds maximum allowed size {MaxUncompressedPacketSize}.");
        }

        var decompressedData = DecompressZLib(contentStream);
        if (decompressedData.Length != dataLength)
        {
            throw new InvalidDataException(
                $"Decompressed data length ({decompressedData.Length}) does not match declared Data Length ({dataLength}).");
        }

        return decompressedData;
    }

    public async Task SendPacketAsync(IServerboundPacket packet, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var outputStream = GetOutputStream();

        // 1. Serialize Packet ID + Data payload into a temporary buffer
        var payloadWriter = new PacketBufferWriter();
        payloadWriter.WriteVarInt(packet.GetPacketAttributeValue(p => p.PacketId));
        packet.Serialize(ref payloadWriter);
        var packetPayload = payloadWriter.WrittenSpan.ToArray();

        // 2. Determine final bytes to send based on compression state
        var finalPacketStream = new MemoryStream();

        if (!UseCompression)
        {
            await WriteVarIntAsync(finalPacketStream, packetPayload.Length, cancellationToken);
            await finalPacketStream.WriteAsync(packetPayload, cancellationToken);
        }
        else
        {
            var shouldCompress = packetPayload.Length >= _compressionThreshold;

            if (shouldCompress)
            {
                var compressedData = CompressZLib(packetPayload);
                var dataLengthVarIntBytes = WriteVarIntToArray(packetPayload.Length);
                var packetLength = dataLengthVarIntBytes.Length + compressedData.Length;

                await WriteVarIntAsync(finalPacketStream, packetLength, cancellationToken);
                await finalPacketStream.WriteAsync(dataLengthVarIntBytes, cancellationToken);
                await finalPacketStream.WriteAsync(compressedData, cancellationToken);
            }
            else
            {
                const byte dataLengthVarInt = 0x00;
                var packetLength = 1 + packetPayload.Length;

                await WriteVarIntAsync(finalPacketStream, packetLength, cancellationToken);
                finalPacketStream.WriteByte(dataLengthVarInt);
                await finalPacketStream.WriteAsync(packetPayload, cancellationToken);
            }
        }

        LogPacketSend(packet);

        // 3. Write the final constructed packet
        finalPacketStream.Position = 0;
        await finalPacketStream.CopyToAsync(outputStream, cancellationToken);

        // 4. Flush the actual output stream - This shouldn't be necessary
        await outputStream.FlushAsync(cancellationToken);
    }

    #endregion

    #region Stream Helpers

    private Stream GetInputStream()
    {
        return (_useEncryption ? _decryptStream : _rawStream as Stream)
               ?? throw new InvalidOperationException("Input stream not available. Not connected or encryption setup failed.");
    }

    private Stream GetOutputStream()
    {
        return (_useEncryption ? _encryptStream : _rawStream as Stream)
               ?? throw new InvalidOperationException("Output stream not available. Not connected or encryption setup failed.");
    }

    #endregion

    #region Logging Helper

    private void LogPacketSend(IServerboundPacket packet)
    {
        var silent = packet.GetPacketAttributeValue(p => p.Silent);
        if (silent) return;

        var packetNamePretty = packet.GetType().FullName?.NamespaceToPrettyString(packet.GetPacketAttributeValue(p => p.PacketId));
        _logger.LogDebug("[->SERVER] {PacketName} {Properties}", packetNamePretty, packet.GetPropertiesAsString());
    }

    #endregion

    #region VarInt Helpers

    private async Task<int> ReadVarIntAsync(Stream stream, CancellationToken cancellationToken)
    {
        var value = 0;
        var shift = 0;
        var buffer = new byte[1];

        while (shift < 35)
        {
            var bytesRead = await stream.ReadAsync(buffer.AsMemory(0, 1), cancellationToken);
            if (bytesRead == 0) throw new EndOfStreamException("Stream ended while reading VarInt.");

            var b = buffer[0];
            value |= (b & 0x7F) << shift;
            shift += 7;

            if ((b & 0x80) == 0) return value;
        }

        throw new InvalidDataException("VarInt too long.");
    }

    private int ReadVarInt(Stream stream)
    {
        var value = 0;
        var shift = 0;

        while (shift < 35)
        {
            var readByte = stream.ReadByte();
            if (readByte == -1) throw new EndOfStreamException("Stream ended while reading VarInt.");

            value |= (readByte & 0x7F) << shift;
            shift += 7;

            if ((readByte & 0x80) == 0) return value;
        }

        throw new InvalidDataException("VarInt too long.");
    }

    private async Task WriteVarIntAsync(Stream stream, int value, CancellationToken cancellationToken)
    {
        var buffer = WriteVarIntToArray(value);
        await stream.WriteAsync(buffer, cancellationToken);
    }

    private byte[] WriteVarIntToArray(int value)
    {
        var buffer = new byte[5];
        var index = 0;
        var unsignedValue = (uint)value;

        do
        {
            var temp = (byte)(unsignedValue & 0x7F);
            unsignedValue >>= 7;
            if (unsignedValue != 0)
            {
                temp |= 0x80;
            }

            buffer[index++] = temp;
        } while (unsignedValue != 0);

        return buffer.AsSpan(0, index).ToArray();
    }

    #endregion

    #region ZLib

    private byte[] CompressZLib(ReadOnlySpan<byte> data)
    {
        using var outputStream = new MemoryStream();
        using var zlibStream = new ZLibStream(outputStream, CompressionLevel.Optimal, leaveOpen: true);
        zlibStream.Write(data);
        return outputStream.ToArray();
    }

    private byte[] DecompressZLib(Stream compressedStream)
    {
        using var outputStream = new MemoryStream();
        using (var zlibStream = new ZLibStream(compressedStream, CompressionMode.Decompress, leaveOpen: true))
        {
            zlibStream.CopyTo(outputStream);
        }

        return outputStream.ToArray();
    }

    #endregion

    #region Disposal

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            _logger.LogInformation("Disposing connection");
            _decryptStream?.Dispose();
            _encryptStream?.Dispose();
            _decryptTransform?.Dispose();
            _encryptTransform?.Dispose();
            _rawStream?.Dispose();
            _client.Dispose();
        }

        _rawStream = null;
        _decryptStream = null;
        _encryptStream = null;
        _decryptTransform = null;
        _encryptTransform = null;
        _useEncryption = false;
        _compressionThreshold = -1;

        _disposed = true;
    }

    ~Connection()
    {
        Dispose(false);
    }

    #endregion

    #region BouncyCastle

    private class BouncyCastleCryptoTransform : ICryptoTransform
    {
        private readonly IBufferedCipher _cipher;

        public BouncyCastleCryptoTransform(IBufferedCipher cipher)
        {
            _cipher = cipher ?? throw new ArgumentNullException(nameof(cipher));
        }

        public BouncyCastleCryptoTransform(IBlockCipher blockCipher)
        {
            _cipher = new BufferedBlockCipher(blockCipher ?? throw new ArgumentNullException(nameof(blockCipher)));
        }

        public int InputBlockSize => _cipher.GetBlockSize();
        public int OutputBlockSize => _cipher.GetBlockSize();
        public bool CanTransformMultipleBlocks => true;
        public bool CanReuseTransform => false;

        public int TransformBlock(byte[] inputBuffer, int inputOffset, int inputCount, byte[] outputBuffer, int outputOffset)
        {
            if (inputCount <= 0) return 0;

            var processed = _cipher.ProcessBytes(inputBuffer, inputOffset, inputCount);
            if (processed is not { Length: > 0 }) return 0;

            if (outputBuffer.Length < outputOffset + processed.Length) throw new ArgumentException("outputBuffer too small");

            Buffer.BlockCopy(processed, 0, outputBuffer, outputOffset, processed.Length);
            return processed.Length;
        }

        public byte[] TransformFinalBlock(byte[] inputBuffer, int inputOffset, int inputCount)
        {
            var finalBytes = _cipher.DoFinal(inputBuffer, inputOffset, inputCount);
            return finalBytes ?? [];
        }

        public void Dispose()
        {
            _cipher.Reset();
            GC.SuppressFinalize(this);
        }

        ~BouncyCastleCryptoTransform()
        {
            Dispose();
        }
    }

    #endregion
}
