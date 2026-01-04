using MinecraftProtoNet.Core.Attributes;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Enums;
using MinecraftProtoNet.Core.Models.Core;
using MinecraftProtoNet.Core.Packets.Base;
using MinecraftProtoNet.Core.Packets.Base.Definitions;
using MinecraftProtoNet.Core.Utilities;

namespace MinecraftProtoNet.Core.Packets.Play.Clientbound;

[Packet(0x74, ProtocolState.Play, true)]
public class SoundPacket : IClientboundPacket
{
    public int SoundId { get; set; }
    public SoundEvent? SoundEvent { get; set; }
    public SoundCategory SoundCategory { get; set; }
    public int EffectPositionXRaw { get; set; }
    public int EffectPositionYRaw { get; set; }
    public int EffectPositionZRaw { get; set; }

    public Vector3<float> EffectPosition => new(EffectPositionYRaw / 8f, EffectPositionYRaw / 8f, EffectPositionZRaw / 8f);
    public float Volume { get; set; }
    public float Pitch { get; set; }
    public long Seed { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        // 0 if value of type X is given inline; otherwise registry ID + 1.
        var id = buffer.ReadVarInt();
        if (id is 0)
        {
            var name = buffer.ReadString();
            var hasFixedRange = buffer.ReadBoolean();
            SoundEvent = new SoundEvent
            {
                Name = name,
                HasFixedRange = hasFixedRange,
                FixedRange = hasFixedRange ? buffer.ReadFloat() : null
            };
        }
        else
        {
            SoundId = id - 1;
        }

        SoundCategory = (SoundCategory)buffer.ReadVarInt();
        EffectPositionXRaw = buffer.ReadSignedInt();
        EffectPositionYRaw = buffer.ReadSignedInt();
        EffectPositionZRaw = buffer.ReadSignedInt();
        Volume = buffer.ReadFloat();
        Pitch = buffer.ReadFloat();
        Seed = buffer.ReadSignedLong();
    }
}
