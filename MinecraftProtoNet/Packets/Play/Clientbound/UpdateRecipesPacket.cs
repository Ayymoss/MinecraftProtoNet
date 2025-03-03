using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Models.Core;
using MinecraftProtoNet.Models.SlotDisplay.Base;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

[Packet(0x7E, ProtocolState.Play)]
public class UpdateRecipesPacket : IClientPacket
{
    public Dictionary<int, Recipe> Recipes { get; set; } = new();

    public void Deserialize(ref PacketBufferReader buffer)
    {
        // TODO: Implement this method
    }

    public class Recipe
    {
        public IdSet IdSet { get; set; }
        public SlotDisplay SlotDisplay { get; set; }
    }
}
