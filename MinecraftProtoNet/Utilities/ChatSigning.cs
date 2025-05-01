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
            bufferWriter.WriteSignedInt(1);
            bufferWriter.WriteUUID(auth.Uuid);
            bufferWriter.WriteUUID(chatSessionUuid);
            bufferWriter.WriteSignedInt(messageIndex);
            bufferWriter.WriteSignedLong(salt);

            var timestampSeconds = timestampMillis / 1000L;
            bufferWriter.WriteSignedLong(timestampSeconds);

            var messageBytes = Encoding.UTF8.GetBytes(message);
            bufferWriter.WriteSignedInt(messageBytes.Length);
            bufferWriter.WriteBuffer(messageBytes);

            var lastSeenCount = Math.Min(lastSeenSignatures.Count, 20);
            bufferWriter.WriteSignedInt(lastSeenCount);

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

        for (var i = 0; i < numSeen; i++)
        {
            var bitIndex = (20 - numSeen) + i;

            if (bitIndex is >= 0 and < 20)
            {
                var byteIndex = bitIndex / 8;
                var bitInByte = bitIndex % 8;
                acknowledgedBytes[byteIndex] |= (byte)(1 << bitInByte);
            }
            else
            {
                Console.WriteLine($"[WARN] Calculated invalid bitIndex {bitIndex} for acknowledgment (numSeen={numSeen}, i={i}).");
            }
        }

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
}
