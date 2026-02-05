using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Models.Core;
using MinecraftProtoNet.Core.Models.SlotDisplay.Base;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Clientbound;

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
