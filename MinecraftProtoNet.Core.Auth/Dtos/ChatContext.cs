using Serilog;

namespace MinecraftProtoNet.Core.Auth.Dtos;

/// <summary>
/// Tracks last seen chat messages using a circular buffer, matching Java's LastSeenMessagesTracker.
/// </summary>
public class ChatContext
{
    public const int LastSeenCount = 20;
    
    public Guid ChatSessionGuid { get; } = Guid.NewGuid();
    public int Index { get; set; } = -1;

    // Circular buffer tracking - matches Java's LastSeenMessagesTracker
    private readonly byte[]?[] _trackedMessages = new byte[]?[LastSeenCount];
    private int _tail;
    private int _offset;
    private byte[]? _lastTrackedMessage;

    /// <summary>
    /// Adds a pending message signature to the tracker.
    /// Call this when a player chat message is received.
    /// </summary>
    /// <param name="signature">The 256-byte message signature</param>
    /// <param name="wasShown">Whether the message was shown to the user</param>
    /// <returns>True if the message was added, false if it was a duplicate</returns>
    public bool AddPending(byte[] signature, bool wasShown)
    {
        if (signature is not { Length: 256 })
        {
            Log.Warning("[WARN] Attempted to add invalid signature (length != 256)");
            return false;
        }

        // Skip if this is the same as the last tracked message (duplicate check)
        if (_lastTrackedMessage != null && signature.SequenceEqual(_lastTrackedMessage))
        {
            return false;
        }

        _lastTrackedMessage = signature;
        AddEntry(wasShown ? signature : null);
        
        Log.Debug("[DEBUG AddPending] Added signature at tail {Tail}. Offset now: {Offset}", _tail, _offset);
        return true;
    }

    private void AddEntry(byte[]? entry)
    {
        int index = _tail;
        _tail = (index + 1) % _trackedMessages.Length;
        _offset++;
        _trackedMessages[index] = entry;
    }

    /// <summary>
    /// Gets and clears the current offset (number of messages since last update).
    /// Used for ServerboundChatAckPacket.
    /// </summary>
    public int GetAndClearOffset()
    {
        int originalOffset = _offset;
        _offset = 0;
        return originalOffset;
    }

    /// <summary>
    /// Current offset (for checking if we need to send an ack packet).
    /// </summary>
    public int Offset => _offset;

    /// <summary>
    /// Generates an update for sending with a chat packet.
    /// Returns the offset, acknowledged bitset, and list of signatures for signing.
    /// Matches Java's LastSeenMessagesTracker.generateAndApplyUpdate().
    /// </summary>
    public (int Offset, byte[] Acknowledged, List<byte[]> SignaturesForSigning, byte Checksum) GenerateAndApplyUpdate()
    {
        int offset = GetAndClearOffset();
        byte[] acknowledged = new byte[3]; // 20 bits = 3 bytes
        var signaturesForSigning = new List<byte[]>(LastSeenCount);

        for (int i = 0; i < LastSeenCount; i++)
        {
            int index = (_tail + i) % _trackedMessages.Length;
            byte[]? message = _trackedMessages[index];
            
            if (message != null)
            {
                // Set bit i in the acknowledged bitset
                int byteIndex = i / 8;
                int bitInByte = i % 8;
                acknowledged[byteIndex] |= (byte)(1 << bitInByte);
                
                signaturesForSigning.Add(message);
                
                // Mark as acknowledged (keep the signature but it's now acknowledged)
                // In Java this transitions from pending=true to pending=false
                // We keep the same array reference - it stays non-null
            }
        }

        // Compute checksum (Java: LastSeenMessages.computeChecksum)
        byte checksum = ComputeChecksum(signaturesForSigning);

        Log.Debug("[DEBUG GenerateUpdate] Offset: {Offset}, Acknowledged: [{Ack0:X2}, {Ack1:X2}, {Ack2:X2}], Signatures: {Count}, Checksum: {Checksum}",
            offset, acknowledged[0], acknowledged[1], acknowledged[2], signaturesForSigning.Count, checksum);

        return (offset, acknowledged, signaturesForSigning, checksum);
    }

    /// <summary>
    /// Computes the checksum for the last seen messages.
    /// Matches Java's LastSeenMessages.computeChecksum().
    /// </summary>
    private static byte ComputeChecksum(List<byte[]> signatures)
    {
        int checksum = 1;
        
        foreach (var sig in signatures)
        {
            // Java: MessageSignature.checksum() returns Arrays.hashCode(bytes)
            int sigChecksum = ComputeArrayHashCode(sig);
            checksum = 31 * checksum + sigChecksum;
        }

        byte checksumByte = (byte)checksum;
        return checksumByte == 0 ? (byte)1 : checksumByte;
    }

    /// <summary>
    /// Computes hash code matching Java's Arrays.hashCode(byte[]).
    /// NOTE: Java bytes are signed (-128 to 127), C# bytes are unsigned (0-255).
    /// We need to convert to signed byte for correct hash calculation.
    /// </summary>
    private static int ComputeArrayHashCode(byte[] array)
    {
        if (array == null) return 0;
        
        int result = 1;
        foreach (byte element in array)
        {
            // Convert unsigned byte (0-255) to signed byte (-128 to 127) like Java
            sbyte signedElement = unchecked((sbyte)element);
            result = 31 * result + signedElement;
        }
        return result;
    }

    /// <summary>
    /// Legacy method for backward compatibility during transition.
    /// </summary>
    [Obsolete("Use AddPending(signature, wasShown) instead")]
    public void AddReceivedMessageSignature(byte[] signature)
    {
        AddPending(signature, wasShown: true);
    }
}
