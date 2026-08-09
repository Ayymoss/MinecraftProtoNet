using System.IO.Compression;
using System.Net.Sockets;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using MinecraftProtoNet.Core.Core.Abstractions;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Services;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Core;

public sealed class Connection : IPacketSender, IDisposable
{
    // --- Constants ---
    private const bool EnableDebugLogging = true;
    private const int MaxPacketSize = 2 * 1024 * 1024;
    private const int MaxUncompressedPacketSize = 8 * 1024 * 1024;

    // --- Network ---
    private TcpClient _client = new();
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

    // Serializes concurrent SendPacketAsync calls from GameLoop, packet handlers, and external callers.
    // Vanilla uses Netty's event loop for this; we use a semaphore since we don't have Netty.
    private SemaphoreSlim _sendLock = new(1, 1);

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

            // Vanilla forces TCP_NODELAY on the client channel.
            // Reference: minecraft-26.2-REFERENCE-ONLY/net/minecraft/network/Connection.java:435
            //   channel.config().setOption(ChannelOption.TCP_NODELAY, true)
            //
            // .NET defaults Socket.NoDelay to false, so Nagle held our small frames until the previous
            // segment was ACKed — up to a full RTT, and on Windows it interacts with delayed-ACK for as much
            // as ~200 ms. That added RTT-correlated jitter to every reply and changed our segmentation
            // pattern versus a real client, both of which are visible at the transport layer.
            _client.NoDelay = true;

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
            // AES/CFB8 with IV = the shared secret, one independent feedback register per direction.
            _decryptTransform = new AesCfb8Transform(sharedSecret, encrypting: false);
            _encryptTransform = new AesCfb8Transform(sharedSecret, encrypting: true);

            _decryptStream = new CryptoStream(_rawStream, _decryptTransform, CryptoStreamMode.Read, leaveOpen: true);
            _encryptStream = new CryptoStream(_rawStream, _encryptTransform, CryptoStreamMode.Write, leaveOpen: true);
            _useEncryption = true;
            _logger.LogInformation("AES/CFB8 encryption enabled (BCL)");
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

    // ===== Outbound rate meter =====
    //
    // Hypixel kicks with "Sending packets too fast!" and dumps the player in the lobby, which leaves the bot
    // holding a stale world: cached entity ids still resolve, so every interact silently does nothing and the
    // session is finished without ever saying so. The logs could not settle whether we deserved that kick,
    // because the highest-frequency packets are logged silently and so were invisible to any after-the-fact
    // count. This meters EVERY outbound packet at the one point they all pass through, and reports the
    // composition of any second that goes over budget — so the next occurrence names its own cause.
    private readonly object _rateGate = new();
    private readonly Dictionary<string, int> _rateByType = [];
    private DateTime _rateWindowStart = DateTime.UtcNow;
    private int _rateCount;

    /// <summary>
    /// The last two minutes of completed per-second windows, kept so that a kick can be explained after the
    /// fact. A threshold warning alone cannot answer "what did we send in the seconds before this", and the
    /// kick is exactly when that question gets asked.
    /// </summary>
    private readonly Queue<(DateTime Start, int Count, int Bytes, string Composition)> _rateHistory = new();

    /// <summary>
    /// On-wire bytes in the window being accumulated. A proxy rate limiter caps bytes per second as well as
    /// packets per second (Velocity's SimpleBytesPerSecondLimiter enforces both), so a packet count alone
    /// cannot tell us which limit we crossed.
    /// Reference: velocity-26.2-REFERENCE-ONLY/proxy/src/main/java/com/velocitypowered/proxy/network/limiter/SimpleBytesPerSecondLimiter.java
    /// </summary>
    private int _rateBytes;

    /// <summary>
    /// The window a Velocity-style limiter averages over. Its rate is sum/interval across a sliding window,
    /// so a one-second spike barely moves it and only a sustained excess trips it — which makes the
    /// per-second peak the wrong number to judge a kick by.
    /// Reference: velocity-26.2-REFERENCE-ONLY/proxy/src/main/java/com/velocitypowered/proxy/config/VelocityConfiguration.java:1015
    /// </summary>
    private const int LimiterWindowSeconds = 7;

    // Per-tick write coalescing was tried here (2026-08-08) and REMOVED, for two reasons worth recording so
    // nobody re-adds it:
    //   1. It is not what vanilla does. Measured from a real-client capture: the vanilla client answers a
    //      server Ping in 4 ms median (42% within 2 ms), and its Pongs are NOT tick-aligned — median 7 ms from
    //      the nearest ClientTickEnd, only 12.4% within 1 ms. Netty flushes per event-loop iteration, not at
    //      the tick boundary, so holding packets until ClientTickEnd makes the client LESS vanilla-like and
    //      adds up to a tick of latency to ping replies.
    //   2. The guard that let handshake/login bypass the buffer was per-connection state that was never reset
    //      on reconnect, so the second connection buffered its login packets waiting for a tick that had not
    //      happened yet — three consecutive CONNECT/SPAWN FAILED.

    // Short-window burst tracking, which per-second buckets cannot show.
    private static readonly TimeSpan BurstWindow = TimeSpan.FromMilliseconds(200);
    private readonly Queue<DateTime> _sendTimes = new();
    private int _peakBurst;
    private DateTime _peakBurstAt;
    private string _peakBurstTypes = "";

    /// <summary>
    /// Packets per second above which the window is reported.
    ///
    /// A vanilla client in motion already sends about 60: one ClientTickEnd and one movement packet per tick,
    /// plus a Pong for the per-tick latency ping Hypixel sends. Anything at or below that is normal traffic,
    /// so the bar sits above it — the interesting case is traffic a real client would never produce.
    /// </summary>
    private const int OutboundRateWarnThreshold = 100;

    private readonly object _inGate = new();
    private readonly Dictionary<string, int> _inByType = [];
    private DateTime _inWindowStart = DateTime.UtcNow;

    /// <summary>
    /// Counts what the server sends us, so an outbound rate can be judged against what provoked it.
    /// Answering "are we replying too often" needs both halves: 20 Pongs a second is a fault if the server
    /// pinged once, and correct if it pinged twenty times.
    /// </summary>
    public void RecordInbound(IClientboundPacket packet)
    {
        lock (_inGate)
        {
            var now = DateTime.UtcNow;
            if (now - _inWindowStart >= TimeSpan.FromSeconds(1))
            {
                _inSecond = _inByType.ToDictionary(kv => kv.Key, kv => kv.Value);

                if (_inSecond.Count > 0)
                {
                    // Ping and KeepAlive are named explicitly because they are what our own send rate is a
                    // reply to, and in a crowded hub the entity firehose pushes them out of any top-N list —
                    // which is exactly the comparison the dump exists to make.
                    var always = new[] { "PingPacket", "KeepAlivePacket" }
                        .Select(t => $"{t}x{_inSecond.GetValueOrDefault(t)}");

                    var composition = string.Join(", ", always.Concat(_inSecond
                        .Where(kv => kv.Key is not ("PingPacket" or "KeepAlivePacket"))
                        .OrderByDescending(kv => kv.Value)
                        .Take(6)
                        .Select(kv => $"{kv.Key}x{kv.Value}")));

                    // Keyed on the truncated wall-clock second, not on this meter's own window start. The two
                    // meters open their windows at whatever instant a packet happens to arrive, so those
                    // starts drift apart and a lookup by window start almost never matches — which is why
                    // dumps were reporting "(not held)" for inbound against perfectly good outbound rows.
                    var key = Truncate(_inWindowStart);
                    if (_inHistory.TryAdd(key, composition)) _inOrder.Enqueue(key);
                    else _inHistory[key] = composition;

                    // Evict the OLDEST, tracked explicitly. This used to call Keys.First(), which returns an
                    // arbitrary key rather than the earliest — so once the window filled, the meter discarded
                    // random seconds including the ones a kick report was about to ask for. That is why the
                    // dumps kept saying "(not held)" for inbound against perfectly good outbound rows.
                    while (_inOrder.Count > 180 && _inOrder.TryDequeue(out var oldest))
                    {
                        _inHistory.Remove(oldest);
                    }
                }

                _inWindowStart = now;
                _inByType.Clear();
            }

            var name = packet.GetType().Name;
            _inByType[name] = _inByType.GetValueOrDefault(name) + 1;

            // Only the packets worth reading in a trace; the entity firehose would bury everything else.
            if (name is not ("MoveEntityPositionPacket" or "MoveEntityRotationPacket"
                or "MoveEntityPositionRotationPacket" or "RotateHeadPacket" or "LevelParticlesPacket"
                or "SetEntityMotionPacket" or "SetEntityDataPacket" or "EntityPositionSyncPacket"
                or "UpdateAttributesPacket" or "SetEquipmentPacket" or "TeleportEntityPacket"
                or "PingPacket" or "SetHealthPacket" or "BundleDelimiterPacket"))
            {
                Trace(_recentIn, "<--", name);
            }
        }
    }

    private Dictionary<string, int> _inSecond = [];

    /// <summary>Inbound composition keyed by wall-clock second, so a dump can line it up against outbound.</summary>
    private readonly Dictionary<string, string> _inHistory = [];

    /// <summary>Insertion order for <see cref="_inHistory"/>, so eviction drops the oldest and not a random one.</summary>
    private readonly Queue<string> _inOrder = new();

    /// <summary>
    /// The last packets in each direction with millisecond stamps, newest last.
    ///
    /// Per-second composition answers "how much"; this answers "in what order, and what was last". If a
    /// server objects to a particular packet, the objectionable one is the one immediately before the
    /// complaint — and a per-second bucket cannot show which that was.
    /// </summary>
    private readonly Queue<string> _recentOut = new();
    private readonly Queue<string> _recentIn = new();

    private void Trace(Queue<string> into, string direction, string name)
    {
        into.Enqueue($"{DateTime.Now:HH:mm:ss.fff} {direction} {name}");
        while (into.Count > 80) into.Dequeue();
    }

    /// <summary>The last packets seen in each direction, interleaved by time — the finest view available.</summary>
    public string DumpRecentPackets()
    {
        List<string> all;
        lock (_rateGate) all = [.. _recentOut];
        lock (_inGate) all.AddRange(_recentIn);

        return all.Count == 0
            ? "(nothing traced)"
            : string.Join("\n", all.OrderBy(l => l, StringComparer.Ordinal).Select(l => "  " + l));
    }

    /// <summary>Bucket key for a timestamp: the whole second it falls in, so both meters agree.</summary>
    private static string Truncate(DateTime t) => t.ToString("HH:mm:ss");

    /// <summary>What the server sent during a given second, or null if that second is no longer held.</summary>
    private string? InboundFor(DateTime second)
    {
        lock (_inGate)
        {
            // The adjacent second is accepted as a fallback: a window that opened a few milliseconds either
            // side of the boundary lands in the neighbouring bucket, and a near-miss is far more useful than
            // reporting nothing at all.
            return _inHistory.GetValueOrDefault(Truncate(second))
                   ?? _inHistory.GetValueOrDefault(Truncate(second.AddSeconds(-1)))
                   ?? _inHistory.GetValueOrDefault(Truncate(second.AddSeconds(1)));
        }
    }

    /// <summary>The most recently completed second of inbound traffic, by packet type.</summary>
    public string DescribeLastInboundSecond()
    {
        lock (_inGate)
        {
            if (_inSecond.Count == 0) return "(nothing recorded)";
            return string.Join(", ", _inSecond
                .OrderByDescending(kv => kv.Value)
                .Take(8)
                .Select(kv => $"{kv.Key}x{kv.Value}"));
        }
    }

    private void RecordOutbound(IServerboundPacket packet, int wireBytes)
    {
        string? report = null;
        int count;

        lock (_rateGate)
        {
            var now = DateTime.UtcNow;
            if (now - _rateWindowStart >= TimeSpan.FromSeconds(1))
            {
                var composition = string.Join(", ", _rateByType
                    .OrderByDescending(kv => kv.Value)
                    .Select(kv => $"{kv.Key}x{kv.Value}"));

                count = _rateCount;
                if (count > 0)
                {
                    _rateHistory.Enqueue((_rateWindowStart, count, _rateBytes, composition));
                    while (_rateHistory.Count > 120) _rateHistory.Dequeue();
                }

                if (count >= OutboundRateWarnThreshold) report = composition;

                _rateWindowStart = now;
                _rateCount = 0;
                _rateBytes = 0;
                _rateByType.Clear();

                if (report is not null)
                {
                    _logger.LogWarning("[OutboundRate] {Count} packets in one second: {Composition} | inbound that second: {Inbound}",
                        count, report, DescribeLastInboundSecond());
                }
            }

            _rateCount++;
            _rateBytes += wireBytes;
            var name = packet.GetType().Name;
            _rateByType[name] = _rateByType.GetValueOrDefault(name) + 1;
            Trace(_recentOut, "-->", name);

            // Per-second buckets average a burst away: fifty packets inside 100ms and nothing for the rest of
            // the second reads as a harmless 50/s. A rate limiter measuring a short window sees the burst, so
            // the burst is tracked separately.
            _sendTimes.Enqueue(now);
            while (_sendTimes.Count > 0 && now - _sendTimes.Peek() > BurstWindow) _sendTimes.Dequeue();
            if (_sendTimes.Count > _peakBurst)
            {
                _peakBurst = _sendTimes.Count;
                _peakBurstAt = now;
                _peakBurstTypes = string.Join(", ", _rateByType
                    .OrderByDescending(kv => kv.Value).Take(4).Select(kv => $"{kv.Key}x{kv.Value}"));
            }
        }
    }

    /// <summary>
    /// Every outbound second recorded in the last two minutes, newest last. Call this the moment something
    /// unexplained happens to the connection — it is the only record of the traffic that caused it.
    /// </summary>
    public string DumpRecentOutbound(int seconds = 45)
    {
        lock (_rateGate)
        {
            var cutoff = DateTime.UtcNow.AddSeconds(-seconds);
            var rows = _rateHistory.Where(w => w.Start >= cutoff).ToList();
            if (rows.Count == 0) return "(no outbound traffic recorded)";

            // Rendered in local time to match the report header. They used to disagree by the UTC offset,
            // which made every row look an hour older than the kick it belonged to — enough to read a
            // ten-second-old login as an hour-old one.
            var lines = rows.Select(w =>
                $"  {w.Start.ToLocalTime():HH:mm:ss}  OUT {w.Count,3} {w.Bytes,6}B  {w.Composition}\n" +
                $"            IN       {InboundFor(w.Start) ?? "(not held)"}");
            var peak = rows.Max(w => w.Count);
            var total = rows.Sum(w => w.Count);

            var burst = _peakBurst == 0
                ? "no burst recorded"
                : $"peak burst {_peakBurst} packets in {BurstWindow.TotalMilliseconds:F0}ms at " +
                  $"{_peakBurstAt.ToLocalTime():HH:mm:ss.fff} ({_peakBurstTypes})";

            // A limiter averages over a sliding window rather than a calendar second, so report the window
            // that actually matters alongside the per-second peak. Consecutive-second runs only: a gap means
            // the seconds are not adjacent and averaging across it would understate the rate.
            var pkPkt = 0.0;
            var pkByte = 0.0;
            var pkAt = default(DateTime);
            for (var i = 0; i + LimiterWindowSeconds <= rows.Count; i++)
            {
                var span = rows.GetRange(i, LimiterWindowSeconds);
                if ((span[^1].Start - span[0].Start).TotalSeconds != LimiterWindowSeconds - 1) continue;
                var pps = span.Sum(w => w.Count) / (double)LimiterWindowSeconds;
                var bps = span.Sum(w => w.Bytes) / (double)LimiterWindowSeconds;
                if (pps > pkPkt) (pkPkt, pkAt) = (pps, span[0].Start);
                if (bps > pkByte) pkByte = bps;
            }

            var sliding = pkPkt == 0
                ? ""
                : $"peak {LimiterWindowSeconds}s sliding average {pkPkt:F1} packets/s and {pkByte:F0} bytes/s " +
                  $"from {pkAt:HH:mm:ss}; ";

            return $"last {rows.Count}s of outbound traffic (peak {peak}/s, total {total}; {sliding}{burst}):\n" +
                   string.Join("\n", lines);
        }
    }

    public async Task SendPacketAsync(IServerboundPacket packet, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _sendLock.WaitAsync(cancellationToken);
        try
        {
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

            // The frame is fully built at this point, so its length is exactly what goes on the wire — which is
            // what a proxy's byte limiter accounts against, not the decoded payload size.
            RecordOutbound(packet, (int)finalPacketStream.Length);

            // 3. Write the final constructed packet
            finalPacketStream.Position = 0;
            await finalPacketStream.CopyToAsync(outputStream, cancellationToken);

            // 4. Flush the actual output stream - This shouldn't be necessary
            await outputStream.FlushAsync(cancellationToken);
        }
        finally
        {
            _sendLock.Release();
        }
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

        // Same reflection cost as the inbound path, on the send lock this time — so it delays the next packet
        // out as well as the next one in.
        if (!_logger.IsEnabled(LogLevel.Information)) return;

        var packetNamePretty = packet.GetType().FullName?.NamespaceToPrettyString(packet.GetPacketAttributeValue(p => p.PacketId));
        _logger.LogInformation("[->SERVER] {PacketName} {Properties}", packetNamePretty, packet.GetPropertiesAsString());
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

    #region Disconnect & Disposal

    /// <summary>
    /// Closes the current connection and resets state so the Connection object can be reused
    /// for a new connection. Unlike Dispose(), this does not permanently invalidate the object.
    /// </summary>
    public void Disconnect()
    {
        _logger.LogInformation("Disconnecting connection (resettable)");
        _decryptStream?.Dispose();
        _encryptStream?.Dispose();
        _decryptTransform?.Dispose();
        _encryptTransform?.Dispose();
        _rawStream?.Dispose();
        _client.Dispose();

        _rawStream = null;
        _decryptStream = null;
        _encryptStream = null;
        _decryptTransform = null;
        _encryptTransform = null;
        _useEncryption = false;
        _compressionThreshold = -1;

        // Reset send lock for the next connection
        _sendLock.Dispose();
        _sendLock = new SemaphoreSlim(1, 1);

        // Create a fresh TcpClient for the next connection
        _client = new TcpClient();
        _disposed = false;
    }

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
            _sendLock.Dispose();
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

}
