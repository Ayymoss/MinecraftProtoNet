using System.Security.Cryptography;
using System.Text;
using MinecraftProtoNet.Core.Auth.Dtos;
using MinecraftProtoNet.Core.Packets.Play.Serverbound;
using Serilog;

namespace MinecraftProtoNet.Core.Utilities;

public static class ChatSigning
{
    private static byte[]? PrepareSignatureData(AuthResult auth, Guid chatSessionUuid, int messageIndex, long salt, long timestampMillis,
        string message, List<byte[]> lastSeenSignatures)
    {
        using var bufferWriter = new PacketBufferWriter(1024);

        try
        {
            // 1. Write constant 1 (protocol version marker for signed messages)
            bufferWriter.WriteSignedInt(1);
            
            // 2. SignedMessageLink: sender UUID, session UUID, message index
            var authUuidBytes = GuidToJavaBytes(auth.Uuid);
            var sessionUuidBytes = GuidToJavaBytes(chatSessionUuid);
            
            bufferWriter.WriteBuffer(authUuidBytes);
            bufferWriter.WriteBuffer(sessionUuidBytes);
            bufferWriter.WriteSignedInt(messageIndex);
            
            // 3. SignedMessageBody: salt, timestamp (seconds), message, lastSeen
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
    /// Creates the RSA-SHA256 signature for a chat message with explicit last seen signatures.
    /// </summary>
    /// <param name="auth">Authentication result containing player UUID, private key, and chat session context.</param>
    /// <param name="message">The chat message content (1-256 bytes UTF8).</param>
    /// <param name="timestamp">Timestamp in milliseconds since epoch (UTC).</param>
    /// <param name="salt">A random 64-bit salt.</param>
    /// <param name="lastSeenSignatures">List of signatures to include in the signed data.</param>
    /// <returns>A 256-byte signature, or null if signing is not possible or fails.</returns>
    public static byte[]? CreateChatSignatureWithLastSeen(AuthResult auth, string message, long timestamp, long salt, List<byte[]> lastSeenSignatures)
    {
        if (auth.PlayerPrivateKey == null || auth.ChatSession == null)
        {
            Log.Warning("Cannot create chat signature: Missing private key or chat session info.");
            return null;
        }

        if (string.IsNullOrEmpty(message) || Encoding.UTF8.GetByteCount(message) > 256)
        {
            Log.Warning("Cannot create chat signature: Invalid message length.");
            return null;
        }

        var chatContext = auth.ChatSession.ChatContext;
        var messageIndex = chatContext.Index;

        var dataToSign = PrepareSignatureData(auth, chatContext.ChatSessionGuid, messageIndex, salt, timestamp, message,
            lastSeenSignatures);

        if (dataToSign == null)
        {
            Log.Error("Failed to prepare data for signing.");
            return null;
        }

        try
        {
            var signature = auth.PlayerPrivateKey.SignData(dataToSign, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

            if (signature.Length == 256) return signature;
            Log.Error("Generated signature has incorrect length: {SigLength}. Expected 256.", signature.Length);
            return null;
        }
        catch (CryptographicException ex)
        {
            Log.Error(ex, "Cryptographic exception during signing");
            return null;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Unexpected exception during signing");
            return null;
        }
    }

    /// <summary>
    /// Creates the RSA-SHA256 signature for a chat message based on the specification.
    /// This is a legacy method - consider using CreateChatSignatureWithLastSeen instead.
    /// </summary>
    [Obsolete("Use CreateChatSignatureWithLastSeen with explicit signature list from GenerateAndApplyUpdate")]
    public static byte[]? CreateChatSignature(AuthResult auth, string message, long timestamp, long salt)
    {
        if (auth.PlayerPrivateKey == null || auth.ChatSession == null)
        {
            Log.Warning("Cannot create chat signature: Missing private key or chat session info.");
            return null;
        }

        // Generate update to get signatures - note: this will clear the offset
        var (_, _, signaturesForSigning, _) = auth.ChatSession.ChatContext.GenerateAndApplyUpdate();
        return CreateChatSignatureWithLastSeen(auth, message, timestamp, salt, signaturesForSigning);
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
            Log.Error("Cannot create signed chat packet: Auth context invalid.");
            return null;
        }

        if (string.IsNullOrEmpty(messageContent) || Encoding.UTF8.GetByteCount(messageContent) > 256)
        {
            Log.Error("Cannot create signed chat packet: Invalid message length.");
            return null;
        }

        var chatContext = auth.ChatSession.ChatContext;
        var nextIndex = chatContext.Index + 1;
        
        // Generate the update - this clears the offset and builds the acknowledged bitset
        var (offset, acknowledged, signaturesForSigning, checksum) = chatContext.GenerateAndApplyUpdate();

        chatContext.Index = nextIndex;

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var salt = Random.Shared.NextInt64();
        
        // Create signature using the signatures collected for signing
        var signature = CreateChatSignatureWithLastSeen(auth, messageContent, timestamp, salt, signaturesForSigning);

        if (signature == null)
        {
            Log.Error("Failed to create chat signature. Aborting packet creation.");
            return null;
        }

        var packet = new ChatPacket
        {
            Message = messageContent,
            Timestamp = timestamp,
            Salt = salt,
            Signature = signature,
            MessageCount = offset,
            Acknowledged = acknowledged,
            Checksum = checksum,
        };

        return packet;
    }

    // Commands with signable arguments (MessageArgument in Java's Brigadier).
    // Reference: net.minecraft.commands.arguments.MessageArgument implements SignedArgument
    // The key is the command name, the value is (argumentName, extractContent func).
    // The extractContent func takes the full command args (after command name) and returns the signable content.
    private static readonly Dictionary<string, (string ArgName, Func<string, string?> ExtractContent)> SignableCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        // /me <action> — Reference: net.minecraft.server.commands.EmoteCommands
        ["me"] = ("action", args => args),
        // /msg <target> <message> — Reference: net.minecraft.server.commands.MsgCommand
        ["msg"] = ("message", SkipFirstArg),
        ["tell"] = ("message", SkipFirstArg),
        ["w"] = ("message", SkipFirstArg),
        // /say <message> — Reference: net.minecraft.server.commands.SayCommand
        ["say"] = ("message", args => args),
        // /teammsg <message> — Reference: net.minecraft.server.commands.TeamMsgCommand
        ["teammsg"] = ("message", args => args),
        ["tm"] = ("message", args => args),
    };

    /// <summary>
    /// Skips the first space-delimited argument, returning the remainder.
    /// Used for commands like /msg &lt;target&gt; &lt;message&gt; where only &lt;message&gt; is signed.
    /// </summary>
    private static string? SkipFirstArg(string args)
    {
        var spaceIndex = args.IndexOf(' ');
        if (spaceIndex < 0 || spaceIndex + 1 >= args.Length) return null;
        return args[(spaceIndex + 1)..];
    }

    /// <summary>
    /// Creates and populates a ChatCommandSignedPacket for sending a signed command.
    /// Automatically detects commands with signable arguments (MessageArgument in vanilla)
    /// and creates argument signatures for them.
    /// </summary>
    /// <param name="auth">Authentication context.</param>
    /// <param name="command">The command string (without leading slash).</param>
    /// <returns>A populated ChatCommandSignedPacket ready for serialization, or null on failure.</returns>
    public static ChatCommandSignedPacket? CreateSignedChatCommandPacket(AuthResult auth, string command)
    {
        if (auth?.PlayerPrivateKey == null || auth.ChatSession == null)
        {
            Log.Error("Cannot create signed chat command packet: Auth context invalid.");
            return null;
        }

        var chatContext = auth.ChatSession.ChatContext;
        var nextIndex = chatContext.Index + 1;

        // Generate the update - this clears the offset and builds the acknowledged bitset
        var (offset, acknowledged, signaturesForSigning, checksum) = chatContext.GenerateAndApplyUpdate();

        chatContext.Index = nextIndex;

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var salt = Random.Shared.NextInt64();

        List<ArgumentSignature> argumentSignatures = [];

        // Check if this command has a signable argument (MessageArgument in vanilla Brigadier)
        // Reference: net.minecraft.commands.arguments.MessageArgument implements SignedArgument
        var spaceIndex = command.IndexOf(' ');
        if (spaceIndex > 0)
        {
            var commandName = command[..spaceIndex];
            var commandArgs = command[(spaceIndex + 1)..];

            if (SignableCommands.TryGetValue(commandName, out var signable))
            {
                var content = signable.ExtractContent(commandArgs);
                if (!string.IsNullOrEmpty(content))
                {
                    var signature = CreateChatSignatureWithLastSeen(auth, content, timestamp, salt, signaturesForSigning);
                    if (signature != null)
                    {
                        argumentSignatures.Add(new ArgumentSignature(signable.ArgName, signature));
                    }
                }
            }
        }

        var packet = new ChatCommandSignedPacket
        {
            Command = command,
            Timestamp = timestamp,
            Salt = salt,
            ArgumentSignatures = argumentSignatures,
            MessageCount = offset,
            Acknowledged = acknowledged,
            Checksum = checksum,
        };

        return packet;
    }

    /// <summary>
    /// Creates a signature for a specific command argument.
    /// </summary>
    public static byte[]? CreateArgumentSignature(AuthResult auth, string content, long timestamp, long salt, List<byte[]> lastSeenSignatures)
    {
        return CreateChatSignatureWithLastSeen(auth, content, timestamp, salt, lastSeenSignatures);
    }

    /// <summary>
    /// Updates the chat context when a signed chat message is received from the server.
    /// Call this method when you decode a client-bound chat packet that has a signature.
    /// </summary>
    /// <param name="auth">Authentication context containing the ChatSession.</param>
    /// <param name="receivedSignature">The 256-byte signature from the received message.</param>
    /// <param name="wasShown">Whether the message was shown to the user (default true).</param>
    public static void ChatMessageReceived(AuthResult auth, byte[]? receivedSignature, bool wasShown = true)
    {
        if (auth?.ChatSession?.ChatContext == null)
        {
            Log.Warning("Cannot process received chat message: Chat context not available.");
            return;
        }

        if (receivedSignature == null)
        {
            Log.Warning("Received null signature. Ignoring.");
            return;
        }

        auth.ChatSession.ChatContext.AddPending(receivedSignature, wasShown);
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
        
        return result;
    }
}
