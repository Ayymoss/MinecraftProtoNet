using Serilog;

namespace MinecraftProtoNet.Auth.Dtos;

public class ChatContext
{
    public Guid ChatSessionGuid { get; } = Guid.NewGuid();
    public int Index { get; set; } = -1;

    public List<byte[]> LastSeenSignatures { get; set; } = [];

    public int UnacknowledgedMessagesCount { get; set; } = 0;

    public void AddReceivedMessageSignature(byte[] signature)
    {
        if (signature is not { Length: 256 })
        {
          Log.Warning("[WARN] Attempted to add invalid signature (length != 256)");
            return;
        }

        LastSeenSignatures.Add(signature);
        while (LastSeenSignatures.Count > 20)
        {
            LastSeenSignatures.RemoveAt(0);
        }

        UnacknowledgedMessagesCount++;
        UnacknowledgedMessagesCount = Math.Min(UnacknowledgedMessagesCount, 20);
        Log.Debug("[DEBUG AddSig] Added signature. List now has {Count} entries. Unacknowledged count: {UnacknowledgedMessagesCount}", LastSeenSignatures.Count, UnacknowledgedMessagesCount);
        Log.Debug("[DEBUG AddSig] Newest signature tail: {Sig}", BitConverter.ToString(LastSeenSignatures.LastOrDefault()?.TakeLast(8).ToArray() ?? []));
    }
}
