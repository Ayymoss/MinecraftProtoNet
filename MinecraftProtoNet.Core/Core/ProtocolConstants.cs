namespace MinecraftProtoNet.Core.Core;

/// <summary>
/// Protocol-level constants for Minecraft 1.21.x (Protocol 775).
/// </summary>
public static class ProtocolConstants
{
    /// <summary>
    /// Protocol version for Minecraft 1.21.x (stable release).
    /// </summary>
    public const int ProtocolVersion = 775;

    /// <summary>
    /// Snapshot base protocol version for 26.1 Snapshot 1.
    /// Combined with bit 30 set: (1 << 30) | SnapshotVersion
    /// </summary>
    public const int SnapshotVersion = 287;

    /// <summary>
    /// Bit mask for identifying snapshot protocol versions.
    /// </summary>
    public const int SnapshotBitMask = 1 << 30;

    /// <summary>
    /// Connection intentions used in the handshake packet.
    /// </summary>
    public static class Intention
    {
        public const int Status = 1;
        public const int Login = 2;
        public const int Transfer = 3;
    }

    /// <summary>
    /// Computes the full protocol version number for snapshots.
    /// </summary>
    public static int GetSnapshotProtocolVersion() => SnapshotBitMask | SnapshotVersion;
}
