using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Clientbound;

/// <summary>
/// Sent when entities mount/dismount a vehicle.
/// </summary>
[Packet(0x6A, ProtocolState.Play)]
public class SetPassengersPacket : IClientboundPacket
{
    /// <summary>
    /// The vehicle entity ID.
    /// </summary>
    public int VehicleEntityId { get; set; }
    
    /// <summary>
    /// Entity IDs of all passengers (empty array means no passengers).
    /// </summary>
    public int[] PassengerEntityIds { get; set; } = [];

    public void Deserialize(ref PacketBufferReader buffer)
    {
        VehicleEntityId = buffer.ReadVarInt();
        PassengerEntityIds = buffer.ReadVarIntArray();
    }
}
