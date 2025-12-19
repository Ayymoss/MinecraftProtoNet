using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using MinecraftProtoNet.Auth.Dtos;
using MinecraftProtoNet.Packets.Play.Serverbound;
using Serilog;

namespace MinecraftProtoNet.Utilities;

public static class ChatSigning
{
    private static byte[]? PrepareSignatureData(AuthResult auth, Guid chatSessionUuid, int messageIndex, long salt, long timestampMillis,
        string message, List<byte[]> lastSeenSignatures)
    {
        using var bufferWriter = new PacketBufferWriter(1024);

        try
        {

            Console.WriteLine($"[DEBUG Sign] AuthUUID: {auth.Uuid}, SessionUUID: {chatSessionUuid}");
            
            // 1. Write constant 1 (protocol version marker for signed messages)
            bufferWriter.WriteSignedInt(1);
            Console.WriteLine($"[DEBUG Sign] Wrote constant int 1");
            
            // 2. SignedMessageLink: sender UUID, session UUID, message index
            // Debug: Show UUID bytes to verify format matches Java's big-endian (MSB first, LSB second)
            var authUuidBytes = GuidToJavaBytes(auth.Uuid);
            var sessionUuidBytes = GuidToJavaBytes(chatSessionUuid);
            Console.WriteLine($"[DEBUG Sign] AuthUUID bytes (Java format): {Convert.ToHexString(authUuidBytes)}");
            Console.WriteLine($"[DEBUG Sign] SessionUUID bytes (Java format): {Convert.ToHexString(sessionUuidBytes)}");
            
            bufferWriter.WriteBuffer(authUuidBytes);
            bufferWriter.WriteBuffer(sessionUuidBytes);
            bufferWriter.WriteSignedInt(messageIndex);
            Console.WriteLine($"[DEBUG Sign] MessageIndex: {messageIndex}");
            
            // 3. SignedMessageBody: salt, timestamp (seconds), message, lastSeen
            bufferWriter.WriteSignedLong(salt);
            Console.WriteLine($"[DEBUG Sign] Salt: {salt} (0x{salt:X16})");

            var timestampSeconds = timestampMillis / 1000L;
            Console.WriteLine($"[DEBUG Sign] Timestamp: {timestampMillis} (ms) -> {timestampSeconds} (s)");
            bufferWriter.WriteSignedLong(timestampSeconds);

            var messageBytes = Encoding.UTF8.GetBytes(message);
            bufferWriter.WriteSignedInt(messageBytes.Length);
            bufferWriter.WriteBuffer(messageBytes);
            Console.WriteLine($"[DEBUG Sign] Message: '{message}' ({messageBytes.Length} bytes)");

            var lastSeenCount = Math.Min(lastSeenSignatures.Count, 20);
            bufferWriter.WriteSignedInt(lastSeenCount);
            Console.WriteLine($"[DEBUG Sign] LastSeenCount: {lastSeenCount}");

            var startIndex = Math.Max(0, lastSeenSignatures.Count - lastSeenCount);
            for (var i = startIndex; i < lastSeenSignatures.Count; i++)
            {
                var sig = lastSeenSignatures[i];
                if (sig.Length == 256)
                {
                    bufferWriter.WriteBuffer(sig);
                }
                else
                {
                    Log.Warning("[WARN] Invalid signature length ({SigLength}) in last seen list for signing at index {I}. Skipping",
                        sig.Length, i);
                    return null;
                }
            }

            return bufferWriter.ToArray();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[ERROR] Exception while preparing signature data");
            return null;
        }
    }

    /// <summary>
    /// Creates the RSA-SHA256 signature for a chat message based on the specification.
    /// </summary>
    /// <param name="auth">Authentication result containing player UUID, private key, and chat session context.</param>
    /// <param name="message">The chat message content (1-256 bytes UTF8).</param>
    /// <param name="timestamp">Timestamp in milliseconds since epoch (UTC).</param>
    /// <param name="salt">A random 64-bit salt.</param>
    /// <returns>A 256-byte signature, or null if signing is not possible or fails.</returns>
    public static byte[]? CreateChatSignature(AuthResult auth, string message, long timestamp, long salt)
    {
        if (auth.PlayerPrivateKey == null || auth.ChatSession == null)
        {
            Console.WriteLine("[WARN] Cannot create chat signature: Missing private key or chat session info.");
            return null;
        }

        if (string.IsNullOrEmpty(message) || Encoding.UTF8.GetByteCount(message) > 256)
        {
            Console.WriteLine("[WARN] Cannot create chat signature: Invalid message length.");
            return null;
        }

        var chatContext = auth.ChatSession.ChatContext;
        var messageIndex = chatContext.Index;

        var dataToSign = PrepareSignatureData(auth, chatContext.ChatSessionGuid, messageIndex, salt, timestamp, message,
            chatContext.LastSeenSignatures);

        if (dataToSign == null)
        {
            Console.WriteLine("[ERROR] Failed to prepare data for signing.");
            return null;
        }

        Console.WriteLine($"[DEBUG SignData] Data to sign ({dataToSign.Length} bytes): {Convert.ToHexString(dataToSign)}");

        try
        {
            var signature = auth.PlayerPrivateKey.SignData(dataToSign, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

            if (signature.Length == 256) return signature;
            Console.WriteLine($"[ERROR] Generated signature has incorrect length: {signature.Length}. Expected 256.");
            return null;
        }
        catch (CryptographicException ex)
        {
            Console.WriteLine($"[ERROR] Cryptographic exception during signing: {ex.Message}");
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Unexpected exception during signing: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Creates and populates a ChatPacket for sending a signed message.
    /// Handles index incrementing, signature creation, and setting acknowledgement info.
    /// </summary>
    /// <param name="auth">Authentication context.</param>
    /// <param name="messageContent">The message text.</param>
    /// <returns>A populated ChatPacket ready for serialization, or null on failure.</returns>
    public static ChatPacket? CreateSignedChatPacket(AuthResult auth, string messageContent)
    {
        if (auth?.PlayerPrivateKey == null || auth.ChatSession == null)
        {
            Console.WriteLine("[ERROR] Cannot create signed chat packet: Auth context invalid.");
            return null;
        }

        if (string.IsNullOrEmpty(messageContent) || Encoding.UTF8.GetByteCount(messageContent) > 256)
        {
            Console.WriteLine("[ERROR] Cannot create signed chat packet: Invalid message length.");
            return null;
        }

        var chatContext = auth.ChatSession.ChatContext;
        var nextIndex = chatContext.Index + 1;
        var messageCountToSend = chatContext.UnacknowledgedMessagesCount;

        Console.WriteLine(
            $"[DEBUG PreSign] Preparing to sign message. Next Index: {nextIndex}, Unacked Count: {messageCountToSend}, Current Seen Sig Count: {chatContext.LastSeenSignatures.Count}");
        if (chatContext.LastSeenSignatures.Count > 0)
        {
            Console.WriteLine("[DEBUG PreSign] Current LastSeenSignatures (Oldest to Newest):");
            for (var i = 0; i < chatContext.LastSeenSignatures.Count; i++)
            {
                var sig = chatContext.LastSeenSignatures[i];
                Console.WriteLine(
                    $"  [{i}]: {Convert.ToHexString(sig.Take(4).ToArray())}...{Convert.ToHexString(sig.TakeLast(4).ToArray())}");
            }
        }
        else
        {
            Console.WriteLine("[DEBUG PreSign] Current LastSeenSignatures: Empty");
        }

        chatContext.Index = nextIndex;

        byte[] acknowledgedBytes = [0, 0, 0];
        var numSeen = Math.Min(chatContext.LastSeenSignatures.Count, 20);

        // Reference: acknowledged.set(i, true) where i is the position in the tracking array
        // Bits 0-19 correspond to positions 0-19 in the 20-message window
        // Bit 0 = oldest tracked message, Bit 19 = newest (if window is full)
        for (var i = 0; i < numSeen; i++)
        {
            // Set bit i directly - this matches the reference implementation
            var bitIndex = i;
            var byteIndex = bitIndex / 8;
            var bitInByte = bitIndex % 8;
            acknowledgedBytes[byteIndex] |= (byte)(1 << bitInByte);
        }
        Console.WriteLine($"[DEBUG PreSign] Acknowledged bitset: [{acknowledgedBytes[0]:X2}, {acknowledgedBytes[1]:X2}, {acknowledgedBytes[2]:X2}] for {numSeen} messages");

        chatContext.UnacknowledgedMessagesCount = 0;

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var salt = Random.Shared.NextInt64();
        Console.WriteLine(
            $"[DEBUG PreSign] Calling CreateChatSignature with: Index={nextIndex}, Timestamp={timestamp}, Salt={salt}, Msg='{messageContent}'");
        var signature = CreateChatSignature(auth, messageContent, timestamp, salt);

        if (signature == null)
        {
            Console.WriteLine("[ERROR] Failed to create chat signature. Aborting packet creation.");
            return null;
        }

        Console.WriteLine($"[DEBUG PreSign] Generated Signature ({signature.Length} bytes): {Convert.ToHexString(signature)}");

        var packet = new ChatPacket
        {
            Message = messageContent,
            Timestamp = timestamp,
            Salt = salt,
            Signature = signature,
            MessageCount = messageCountToSend,
            Acknowledged = acknowledgedBytes,
            Checksum = 0, // TODO: Placeholder for checksum; calculate if needed
        };

        return packet;
    }

    /// <summary>
    /// Updates the chat context when a signed chat message is received from the server.
    /// Call this method when you decode a client-bound chat packet that has a signature.
    /// </summary>
    /// <param name="auth">Authentication context containing the ChatSession.</param>
    /// <param name="receivedSignature">The 256-byte signature from the received message.</param>
    public static void ChatMessageReceived(AuthResult auth, byte[]? receivedSignature)
    {
        if (auth?.ChatSession?.ChatContext == null)
        {
            Console.WriteLine("[WARN] Cannot process received chat message: Chat context not available.");
            return;
        }

        if (receivedSignature == null)
        {
            Console.WriteLine("[WARN] Received null signature. Ignoring.");
            return;
        }

        auth.ChatSession.ChatContext.AddReceivedMessageSignature(receivedSignature);
    }

    /// <summary>
    /// Converts a C# Guid to Java UUID byte format.
    /// Java UUID: writes mostSigBits (long, big-endian) then leastSigBits (long, big-endian)
    /// C# Guid internal format is different, so we need to extract the bytes correctly.
    /// </summary>
    private static byte[] GuidToJavaBytes(Guid guid)
    {
        // Get the raw bytes of the Guid
        Span<byte> guidBytes = stackalloc byte[16];
        guid.TryWriteBytes(guidBytes);
        
        // C# Guid internal layout (little-endian for first 3 fields):
        // Bytes 0-3: Data1 (int, LE -> need to swap to BE)
        // Bytes 4-5: Data2 (short, LE -> need to swap to BE)
        // Bytes 6-7: Data3 (short, LE -> need to swap to BE)
        // Bytes 8-15: Data4 (already in correct order)
        
        // Convert to Java UUID format (big-endian: MSB first, LSB second)
        var result = new byte[16];
        
        // Data1: swap 4 bytes
        result[0] = guidBytes[3];
        result[1] = guidBytes[2];
        result[2] = guidBytes[1];
        result[3] = guidBytes[0];
        
        // Data2: swap 2 bytes  
        result[4] = guidBytes[5];
        result[5] = guidBytes[4];
        
        // Data3: swap 2 bytes
        result[6] = guidBytes[7];
        result[7] = guidBytes[6];
        
        // Data4: copy as-is (bytes 8-15)
        guidBytes[8..16].CopyTo(result.AsSpan(8));
        
        Console.WriteLine($"[DEBUG GuidToJavaBytes] Input: {guid}");
        Console.WriteLine($"[DEBUG GuidToJavaBytes] Raw C# bytes: {Convert.ToHexString(guidBytes)}");
        Console.WriteLine($"[DEBUG GuidToJavaBytes] Java format:  {Convert.ToHexString(result)}");
        
        return result;
    }
}
