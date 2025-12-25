using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

[Packet(0x89, ProtocolState.Play, true)]
public class TrackedWaypointPacket : IClientboundPacket
{
    public void Deserialize(ref PacketBufferReader buffer)
    {
        // TODO: Implement deserialization for TrackedWaypoint (Complex structure with Either, Icon, Polymorphic Type).
        // For now, we leave this empty to suppress "Unknown Packet" logs.
        // The packet data is isolated in the buffer, so not reading it is safe.
    }
}
