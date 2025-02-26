using MinecraftProtoNet.Core;
using MinecraftProtoNet.Models.Core;
using MinecraftProtoNet.Models.SlotDisplay.Base;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

public class UpdateRecipesPacket : Packet
{
    public override int PacketId => 0x7E;
    public override PacketDirection Direction => PacketDirection.Clientbound;

    public Dictionary<int, Recipe> Recipes { get; set; } = new();

    public override void Deserialize(ref PacketBufferReader buffer)
    {
        // TODO: Implement this method
    }

    public class Recipe
    {
        public IdSet IdSet { get; set; }
        public SlotDisplay SlotDisplay { get; set; }
    }
}
