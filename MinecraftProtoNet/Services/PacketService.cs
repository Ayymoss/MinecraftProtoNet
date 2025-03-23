using MinecraftProtoNet.Core;
using MinecraftProtoNet.Handlers.Base;
using MinecraftProtoNet.Packets.Base;

namespace MinecraftProtoNet.Services;

public class PacketService : IPacketService
{
    private readonly Dictionary<ProtocolState, Dictionary<int, IPacketHandler>> _handlers = new();
    private readonly IEnumerable<IPacketHandler> _allHandlers;

    public PacketService(IEnumerable<IPacketHandler> allHandlers)
    {
        _allHandlers = allHandlers;
        RegisterHandlers();
    }

    private void RegisterHandlers()
    {
        foreach (var handler in _allHandlers)
        {
            RegisterHandler(handler);
        }
    }

    private void RegisterHandler(IPacketHandler handler)
    {
        foreach (var (state, packetId) in handler.RegisteredPackets)
        {
            if (!_handlers.TryGetValue(state, out var value))
            {
                value = new Dictionary<int, IPacketHandler>();
                _handlers[state] = value;
            }

            if (!value.TryAdd(packetId, handler))
            {
                throw new ArgumentException($"Handler for state {state} and packet ID {packetId} is already registered.");
            }
        }
    }

    public async Task HandlePacketAsync(IClientPacket packet, IMinecraftClient client)
    {
        var packetId = packet.GetPacketAttributeValue(p => p.PacketId);
        if (_handlers.TryGetValue(client.ProtocolState, out var stateHandlers) &&
            stateHandlers.TryGetValue(packetId, out var handler))
        {
            await handler.HandleAsync(packet, client);
        }
    }

    public IClientPacket CreateIncomingPacket(ProtocolState state, int packetId)
    {
        IClientPacket packet = state switch
        {
            ProtocolState.Handshaking => packetId switch
            {
                _ => throw new ArgumentOutOfRangeException(nameof(packetId),
                    $"Unknown packet ID {packetId} (0x{packetId:X2}) for Handshaking state.")
            },
            ProtocolState.Status => packetId switch
            {
                0x00 => new Packets.Status.Clientbound.StatusResponsePacket(),
                0x01 => new Packets.Status.Clientbound.PongResponsePacket(),
                _ => throw new ArgumentOutOfRangeException(nameof(packetId),
                    $"Unknown packet ID {packetId} (0x{packetId:X2}) for Status state.")
            },
            ProtocolState.Login => packetId switch
            {
                0x00 => new Packets.Login.Clientbound.DisconnectLoginPacket(),
                0x02 => new Packets.Login.Clientbound.LoginSuccessPacket(),
                0x03 => new Packets.Login.Clientbound.SetCompressionPacket(),
                _ => throw new ArgumentOutOfRangeException(nameof(packetId),
                    $"Unknown packet ID {packetId} (0x{packetId:X2}) for Login state.")
            },
            ProtocolState.Configuration => packetId switch
            {
                0x01 => new Packets.Configuration.Clientbound.CustomPayloadPacket(),
                0x02 => new Packets.Configuration.Clientbound.DisconnectPacket(),
                0x03 => new Packets.Configuration.Clientbound.FinishConfigurationPacket(),
                0x04 => new Packets.Configuration.Clientbound.KeepAlivePacket(),
                0x07 => new Packets.Configuration.Clientbound.RegistryDataPacket(),
                0x0C => new Packets.Configuration.Clientbound.UpdateEnabledFeaturesPacket(),
                0x0D => new Packets.Configuration.Clientbound.UpdateTagsPacket(),
                0x0E => new Packets.Configuration.Clientbound.SelectKnownPacksPacket(),
                _ => throw new ArgumentOutOfRangeException(nameof(packetId),
                    $"Unknown packet ID {packetId} (0x{packetId:X2}) for Configuration state.")
            },
            ProtocolState.Play => packetId switch
            {
                // Connection & Core Game State
                0x00 => new Packets.Play.Clientbound.BundleDelimiterPacket(),
                0x27 => new Packets.Play.Clientbound.KeepAlivePacket(),
                0x37 => new Packets.Play.Clientbound.PingPacket(),
                0x38 => new Packets.Play.Clientbound.PongResponsePacket(),
                0x1D => new Packets.Play.Clientbound.DisconnectPacket(),

                // Player Initial Login & Setup
                0x2C => new Packets.Play.Clientbound.LoginPacket(),
                0x0B => new Packets.Play.Clientbound.ChangeDifficultyPacket(),
                0x3A => new Packets.Play.Clientbound.PlayerAbilitiesPacket(),
                0x63 => new Packets.Play.Clientbound.SetHeldSlotPacket(),
                0x5B => new Packets.Play.Clientbound.SetDefaultSpawnPositionPacket(),
                0x42 => new Packets.Play.Clientbound.PlayerPositionPacket(),

                // Player Information
                0x40 => new Packets.Play.Clientbound.PlayerInfoUpdatePacket(),
                0x3F => new Packets.Play.Clientbound.PlayerInfoRemovePacket(),
                0x61 => new Packets.Play.Clientbound.SetExperiencePacket(),
                0x62 => new Packets.Play.Clientbound.SetHealthPacket(),

                // Player Status & Chat
                0x3B => new Packets.Play.Clientbound.PlayerChatPacket(),
                0x73 => new Packets.Play.Clientbound.SystemChatPacket(),
                0x3E => new Packets.Play.Clientbound.PlayerCombatKillPacket(),
                0x3D => new Packets.Play.Clientbound.PlayerCombatEnterPacket(),
                0x3C => new Packets.Play.Clientbound.PlayerCombatEndPacket(),

                // Entity Creation & Removal
                0x01 => new Packets.Play.Clientbound.AddEntityPacket(),
                0x02 => new Packets.Play.Clientbound.AddExperienceOrbPacket(),
                0x47 => new Packets.Play.Clientbound.RemoveEntitiesPacket(),

                // Entity Movement & Position
                0x30 => new Packets.Play.Clientbound.MoveEntityPositionRotationPacket(),
                0x2F => new Packets.Play.Clientbound.MoveEntityPositionPacket(),
                0x32 => new Packets.Play.Clientbound.MoveEntityRotationPacket(),
                0x20 => new Packets.Play.Clientbound.EntityPositionSyncPacket(),
                0x4D => new Packets.Play.Clientbound.RotateHeadPacket(),
                0x5F => new Packets.Play.Clientbound.SetEntityMotionPacket(),
                0x25 => new Packets.Play.Clientbound.HurtAnimationPacket(),

                // Entity State & Properties
                0x5D => new Packets.Play.Clientbound.SetEntityDataPacket(),
                0x1F => new Packets.Play.Clientbound.EntityEventPacket(),
                0x7C => new Packets.Play.Clientbound.UpdateAttributesPacket(),
                0x60 => new Packets.Play.Clientbound.SetEquipmentPacket(),
                0x03 => new Packets.Play.Clientbound.AnimatePacket(),
                0x1A => new Packets.Play.Clientbound.DamageEventPacket(),
                0x76 => new Packets.Play.Clientbound.TakeItemEntity(),

                // World & Environment
                0x28 => new Packets.Play.Clientbound.LevelChunkWithLightPacket(),
                0x22 => new Packets.Play.Clientbound.ForgetLevelChunkPacket(),
                0x4E => new Packets.Play.Clientbound.SectionBlocksUpdatePacket(),
                0x09 => new Packets.Play.Clientbound.BlockUpdatePacket(),
                0x05 => new Packets.Play.Clientbound.BlockChangedAcknowledgementPacket(),
                0x58 => new Packets.Play.Clientbound.SetChunkCacheCenterPacket(),
                0x6B => new Packets.Play.Clientbound.SetTimePacket(),
                0x23 => new Packets.Play.Clientbound.GameEventPacket(),
                0x29 => new Packets.Play.Clientbound.LevelEventPacket(),
                0x2A => new Packets.Play.Clientbound.LevelParticlesPacket(),
                0x06 => new Packets.Play.Clientbound.BlockDestructionPacket(),

                // Game Mechanics
                0x78 => new Packets.Play.Clientbound.TickingStatePacket(),
                0x79 => new Packets.Play.Clientbound.TickingStepPacket(),
                0x7E => new Packets.Play.Clientbound.UpdateRecipesPacket(),
                0x15 => new Packets.Play.Clientbound.ContainerSetSlotPacket(),
                0x6F => new Packets.Play.Clientbound.SoundPacket(),
                0x13 => new Packets.Play.Clientbound.ContainerSetContentPacket(),
                _ => new UnknownPacket()
            },
            _ => throw new ArgumentOutOfRangeException(nameof(state), $"Invalid protocol state {state}.")
        };

        return packet;
    }
}
