namespace MinecraftProtoNet.Core;

public enum ProtocolState
{
    Transfer = -1, // This should be moved.
    Undefined = 0,
    Handshaking = 1,
    Status = 2,
    Login = 3,
    Configuration = 4,
    Play = 5
}
