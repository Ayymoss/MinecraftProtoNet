using MinecraftProtoNet.Models.Core;
using MinecraftProtoNet.Packets.Base.Definitions;

namespace MinecraftProtoNet.State;

public class Entity
{
    public int EntityId { get; set; }
    public Vector3<double> Position { get; set; } = new();
    public Vector3<double> Velocity { get; set; } = new();
    public Vector2<float> YawPitch { get; set; } = new();
    public int BlockPlaceSequence { get; set; }

    public short HeldSlot { get; set; }
    public short HeldSlotWithOffset => (short)(HeldSlot + 36);
    public Slot HeldItem => Inventory[HeldSlotWithOffset];
    public Dictionary<short, Slot> Inventory { get; set; } = new();
}
