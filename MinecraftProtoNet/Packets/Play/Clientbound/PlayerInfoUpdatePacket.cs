using MinecraftProtoNet.Attributes;
using MinecraftProtoNet.Core;
using MinecraftProtoNet.Models.Player;
using MinecraftProtoNet.NBT.Tags;
using MinecraftProtoNet.Packets.Base;
using MinecraftProtoNet.Utilities;

namespace MinecraftProtoNet.Packets.Play.Clientbound;

[Packet(0x40, ProtocolState.Play)]
public class PlayerInfoUpdatePacket : IClientPacket
{
    // TODO: Properly implement the object types.
    public PlayerAction[] Actions { get; set; }
    public PlayerInfo[] PlayerInfos { get; set; } = [];

    public void Deserialize(ref PacketBufferReader buffer)
    {
        Actions = buffer.ReadEnumSet<PlayerAction>().ToArray();

        var entries = buffer.ReadVarInt();
        PlayerInfos = new PlayerInfo[entries];
        for (var i = 0; i < entries; i++)
        {
            PlayerInfos[i] = new PlayerInfo(buffer.ReadUuid())
            {
                Actions = new PlayerActionBase[Actions.Length]
            };
            for (var j = 0; j < Actions.Length; j++)
            {
                var action = Actions[j];
                switch (action)
                {
                    case PlayerAction.AddPlayer:

                        var username = buffer.ReadString();
                        var addPlayerLength = buffer.ReadVarInt();
                        var addPlayerProperties = new Property[addPlayerLength];
                        for (var prop = 0; prop < addPlayerLength; prop++)
                        {
                            addPlayerProperties[prop] = new Property
                            {
                                Name = buffer.ReadString(),
                                Value = buffer.ReadString(),
                                Signature = buffer.ReadBoolean() ? buffer.ReadString() : null
                            };
                        }

                        PlayerInfos[i].Actions[j] = new AddPlayer(action)
                        {
                            Username = username,
                            Properties = addPlayerProperties
                        };
                        break;
                    case PlayerAction.InitChat:
                        if (!buffer.ReadBoolean()) break;
                        var sessionUuid = buffer.ReadUuid();
                        var publicKeyExpiration = buffer.ReadVarLong();
                        var encodedPublicKey = buffer.ReadPrefixedArray<byte>();
                        var publicKeySignature = buffer.ReadPrefixedArray<byte>();
                        PlayerInfos[i].Actions[j] = new InitChat(action)
                        {
                            SessionUuid = sessionUuid,
                            PublicKeyExpiration = publicKeyExpiration,
                            EncodedPublicKey = encodedPublicKey,
                            PublicKeySignature = publicKeySignature
                        };
                        break;
                    case PlayerAction.UpdateGameMode:
                        var gameMode = buffer.ReadVarInt();
                        PlayerInfos[i].Actions[j] = new UpdateGameMode(action)
                        {
                            GameMode = gameMode
                        };
                        break;
                    case PlayerAction.UpdateListed:
                        var listed = buffer.ReadBoolean();
                        PlayerInfos[i].Actions[j] = new UpdateListed(action)
                        {
                            Listed = listed
                        };
                        break;
                    case PlayerAction.UpdateLatency:
                        var latency = buffer.ReadVarInt();
                        PlayerInfos[i].Actions[j] = new UpdateLatency(action)
                        {
                            Latency = latency
                        };
                        break;
                    case PlayerAction.UpdateDisplayName:
                        var displayName = buffer.ReadOptionalNbtTag();
                        PlayerInfos[i].Actions[j] = new UpdateDisplayName(action)
                        {
                            DisplayName = displayName
                        };
                        break;
                    case PlayerAction.UpdateListPriority:
                        var listPriority = buffer.ReadVarInt();
                        PlayerInfos[i].Actions[j] = new UpdateListPriority(action)
                        {
                            ListPriority = listPriority
                        };
                        break;
                    case PlayerAction.UpdateHat:
                        var hatVisible = buffer.ReadBoolean();
                        PlayerInfos[i].Actions[j] = new UpdateHat(action)
                        {
                            HatVisible = hatVisible
                        };
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

    public class PlayerInfo(Guid uuid)
    {
        public Guid Uuid { get; set; } = uuid;
        public PlayerActionBase[] Actions { get; set; }

        public override string ToString()
        {
            return Uuid.ToString();
        }
    }

    public abstract class PlayerActionBase(PlayerAction action)
    {
        public PlayerAction Action { get; set; }
    }

    public class AddPlayer(PlayerAction action) : PlayerActionBase(action)
    {
        public required string Username { get; set; } = string.Empty;
        public required Property[] Properties { get; set; } = [];
    }

    public class InitChat(PlayerAction action) : PlayerActionBase(action)
    {
        public required Guid SessionUuid { get; set; }
        public required long PublicKeyExpiration { get; set; }
        public required byte[] EncodedPublicKey { get; set; } = [];
        public required byte[] PublicKeySignature { get; set; } = [];
    }

    public class UpdateGameMode(PlayerAction action) : PlayerActionBase(action)
    {
        public required int GameMode { get; set; }
    }

    public class UpdateListed(PlayerAction action) : PlayerActionBase(action)
    {
        public required bool Listed { get; set; }
    }

    public class UpdateLatency(PlayerAction action) : PlayerActionBase(action)
    {
        public required int Latency { get; set; }
    }

    public class UpdateDisplayName(PlayerAction action) : PlayerActionBase(action)
    {
        public required NbtTag? DisplayName { get; set; }
    }

    public class UpdateListPriority(PlayerAction action) : PlayerActionBase(action)
    {
        public required int ListPriority { get; set; }
    }

    public class UpdateHat(PlayerAction action) : PlayerActionBase(action)
    {
        public required bool HatVisible { get; set; }
    }
}
