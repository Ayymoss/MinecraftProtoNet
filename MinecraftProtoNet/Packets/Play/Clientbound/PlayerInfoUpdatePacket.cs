using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Models.Player;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.State;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

[Packet(0x40, ProtocolState.Play)]
public class PlayerInfoUpdatePacket : IClientPacket
{
    // TODO: Properly implement the object types.
    public PlayerAction[] Actions { get; set; }
    public Player[] Players { get; set; }

    public void Deserialize(ref PacketBufferReader buffer)
    {
        Actions = buffer.ReadEnumSet<PlayerAction>().ToArray();
        var entries = buffer.ReadVarInt();
        Players = new Player[entries];
        for (var count = 0; count < entries; count++)
        {
            Players[count] = new Player
            {
                Uuid = buffer.ReadUuid(),
                Objects = []
            };

            foreach (var action in Actions)
            {
                switch (action)
                {
                    case PlayerAction.AddPlayer:
                        Players[count].Username = buffer.ReadString();
                        var addPlayerLength = buffer.ReadVarInt();
                        var addPlayerProperties = new Property[addPlayerLength];
                        for (var i = 0; i < addPlayerLength; i++)
                        {
                            addPlayerProperties[i] = new Property
                            {
                                Name = buffer.ReadString(),
                                Value = buffer.ReadString(),
                                Signature = buffer.ReadBoolean() ? buffer.ReadString() : null
                            };
                        }

                        Players[count].Objects.Add(addPlayerProperties);
                        break;
                    case PlayerAction.InitChat:
                        var sessionUuid = buffer.ReadUuid();
                        var publicKeyExpiration = buffer.ReadVarLong();
                        var encodedPublicKey = buffer.ReadPrefixedArray<byte>();
                        var publicKeySignature = buffer.ReadPrefixedArray<byte>();
                        Players[count].Objects.Add(sessionUuid);
                        Players[count].Objects.Add(publicKeyExpiration);
                        Players[count].Objects.Add(encodedPublicKey);
                        Players[count].Objects.Add(publicKeySignature);
                        break;
                    case PlayerAction.UpdateGameMode:
                        var gameMode = buffer.ReadVarInt();
                        Players[count].Objects.Add(gameMode);
                        break;
                    case PlayerAction.UpdateListed:
                        var listed = buffer.ReadBoolean();
                        Players[count].Objects.Add(listed);
                        break;
                    case PlayerAction.UpdateLatency:
                        var latency = buffer.ReadVarInt();
                        Players[count].Objects.Add(latency);
                        break;
                    case PlayerAction.UpdateDisplayName:
                        var displayName = buffer.ReadOptionalNbtTag();
                        Players[count].Objects.Add(displayName);
                        break;
                    case PlayerAction.UpdateListPriority:
                        var listPriority = buffer.ReadVarInt();
                        Players[count].Objects.Add(listPriority);
                        break;
                    case PlayerAction.UpdateHat:
                        var hatVisible = buffer.ReadBoolean();
                        Players[count].Objects.Add(hatVisible);
                        break;
                }
            }
        }
    }

    public enum PlayerAction : byte
    {
        AddPlayer = 0x01,
        InitChat = 0x02,
        UpdateGameMode = 0x04,
        UpdateListed = 0x08,
        UpdateLatency = 0x10,
        UpdateDisplayName = 0x20,
        UpdateListPriority = 0x40,
        UpdateHat = 0x80
    }
}
