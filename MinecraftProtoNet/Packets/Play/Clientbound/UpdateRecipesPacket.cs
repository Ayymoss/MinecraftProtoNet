using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Models.Core;
using MinecraftProtoNet.Models.SlotDisplay.Base;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

[Packet(0x84, ProtocolState.Play)]
public class UpdateRecipesPacket : IClientboundPacket
{
    public Dictionary<int, Recipe> Recipes { get; set; } = new();

    public void Deserialize(ref PacketBufferReader buffer)
    {
        // TODO: Implement this method
    }

    public class Recipe
    {
        public required IdSet IdSet { get; set; }
        public required SlotDisplay SlotDisplay { get; set; }
    }
}
