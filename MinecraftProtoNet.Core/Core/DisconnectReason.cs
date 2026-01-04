namespace MinecraftProtoNet.Core;

/// <summary>
/// Reasons for disconnection from the server.
/// </summary>
public enum DisconnectReason
{
    /// <summary>
    /// Unknown disconnection reason.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Graceful disconnection initiated by the client.
    /// </summary>
    ClientDisconnect,

    /// <summary>
    /// Server closed the connection.
    /// </summary>
    ServerDisconnect,

    /// <summary>
    /// Connection was lost (network error).
    /// </summary>
    ConnectionLost,

    /// <summary>
    /// Connection was forcibly closed by the remote host.
    /// </summary>
    ConnectionReset,

    /// <summary>
    /// End of stream was reached unexpectedly.
    /// </summary>
    EndOfStream,

    /// <summary>
    /// The operation was cancelled.
    /// </summary>
    Cancelled,

    /// <summary>
    /// An error occurred.
    /// </summary>
    Error
}
