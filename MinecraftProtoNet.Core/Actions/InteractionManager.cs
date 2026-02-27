using Microsoft.Extensions.Logging;
using MinecraftProtoNet.Core.Core;
using MinecraftProtoNet.Core.Data;
using MinecraftProtoNet.Core.Enums;
using MinecraftProtoNet.Core.Models.Core;
using MinecraftProtoNet.Core.Models.World.Chunk;
using MinecraftProtoNet.Core.Packets.Base.Definitions;
using MinecraftProtoNet.Core.Packets.Play.Serverbound;
using MinecraftProtoNet.Core.State;
using MinecraftProtoNet.Core.State.Base;

namespace MinecraftProtoNet.Core.Actions;

public class InteractionManager : IInteractionManager
{
    private readonly IMinecraftClient _client;
    private readonly ILogger<InteractionManager> _logger;

    public double ReachDistance { get; set; } = 4.5;

    // Block breaking state
    private Vector3<int>? _breakingBlockPosition;
    private BlockFace? _breakingBlockFace;
    private bool _isBreakingBlock;
    private long _startBreakingTick;
    private double _totalBreakingTicks;
    
    /// <summary>
    /// The last held slot index that was sent to the server.
    /// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/client/multiplayer/MultiPlayerGameMode.java:81
    /// Java field: private int carriedIndex;
    /// </summary>
    private short _lastSentCarriedSlot = -1;

    public InteractionManager(IMinecraftClient client, ILogger<InteractionManager> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<bool> DigBlockAsync()
    {
        if (!_client.State.LocalPlayer.HasEntity) return false;
        var entity = _client.State.LocalPlayer.Entity;
        
        // Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/client/multiplayer/MultiPlayerGameMode.java:221
        // Java calls ensureHasSentCarriedItem() in continueDestroyBlock
        await EnsureHasSentCarriedItemAsync();

        var tick = _client.State.Level.ClientTickCounter;

        // If we are already breaking a block, try to keep breaking it even if we aren't looking EXACTLY at it
        // as long as we are looking at SOME block and Baritone still wants to dig.
        // Actually, Baritone sets ClickLeft every tick it wants to dig.
        
        if (_isBreakingBlock)
        {
            var elapsed = tick - _startBreakingTick;
            if (elapsed >= _totalBreakingTicks)
            {
                _logger.LogInformation("Digging block at {Pos} finished after {Elapsed} ticks", _breakingBlockPosition, elapsed);
                await _client.SendPacketAsync(new PlayerActionPacket
                {
                    Status = PlayerActionPacket.StatusType.FinishedDigging,
                    Position = new Vector3<double>(_breakingBlockPosition!.X, _breakingBlockPosition.Y, _breakingBlockPosition.Z),
                    Face = _breakingBlockFace!.Value,
                    Sequence = entity.IncrementSequence()
                });
                await _client.SendPacketAsync(new SwingPacket { Hand = Hand.MainHand });

                // Client-side prediction: immediately set block to air locally.
                // This matches vanilla Minecraft behavior where the client predicts block removal.
                // Without this, Baritone's Prepared() still sees the block as solid on the next tick
                // (before the server's BlockUpdate packet arrives) and starts mining the same block again.
                // Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/client/multiplayer/MultiPlayerGameMode.java
                _client.State.Level.HandleBlockUpdate(
                    new Vector3<double>(_breakingBlockPosition!.X, _breakingBlockPosition.Y, _breakingBlockPosition.Z), 0);

                _isBreakingBlock = false;
                _breakingBlockPosition = null;
                return true;
            }
            
            // Periodically send ContinueDestroyBlockAsync (e.g. every 5 ticks)
            if (tick % 5 == 0)
            {
                await ContinueDestroyBlockAsync(_breakingBlockPosition!, _breakingBlockFace!.Value);
            }
            await _client.SendPacketAsync(new SwingPacket { Hand = Hand.MainHand });
            return true;
        }

        // Start breaking a new block
        var hit = entity.GetLookingAtBlock(_client.State.Level, ReachDistance);
        if (hit is null)
        {
            return false;
        }

        var pos = hit.BlockPosition;
        var face = hit.Face;

        _logger.LogInformation("Digging block at {Pos} (Face={Face}) started", pos, face);
        
        // Calculate mining time using the same formula as Java client
        // Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/level/block/state/BlockBehaviour.java:328-336
        // getDestroyProgress = playerDestroySpeed / blockDestroySpeed / modifier
        // modifier = 30 if correct tool, 100 otherwise
        // ticks to break = 1.0 / getDestroyProgress
        var block = _client.State.Level.GetBlockAt(pos.X, pos.Y, pos.Z);
        if (block == null) return false;

        float hardness = block.DestroySpeed;
        if (hardness < 0) 
        {
            _logger.LogWarning("DigBlockAsync: Attempted to break unbreakable block {BlockName}", block.Name);
            return false;
        }

        // Get player's held item to determine tool speed
        Slot heldItem = entity.Inventory.GetSlot(entity.HeldSlotWithOffset);
        double destroyProgress = CalculateDestroyProgress(heldItem, block);
        _totalBreakingTicks = destroyProgress > 0 ? Math.Ceiling(1.0 / destroyProgress) : 0;
        
        string? heldItemName = null;
        if (heldItem.ItemId.HasValue) ClientState.ItemRegistry?.TryGetValue(heldItem.ItemId.Value, out heldItemName);
        _logger.LogInformation("[Mining] Block={Block} at ({X},{Y},{Z}), Tool={Tool} (slot={Slot}), Hardness={Hardness}, Ticks={Ticks}",
            block.Name, pos.X, pos.Y, pos.Z, heldItemName ?? "empty hand", entity.HeldSlot, hardness, _totalBreakingTicks);

        _isBreakingBlock = true;
        _breakingBlockPosition = pos;
        _breakingBlockFace = face;
        _startBreakingTick = tick;

        await _client.SendPacketAsync(new PlayerActionPacket
        {
            Status = PlayerActionPacket.StatusType.StartedDigging,
            Position = new Vector3<double>(pos.X, pos.Y, pos.Z),
            Face = face,
            Sequence = entity.IncrementSequence()
        });
        await _client.SendPacketAsync(new SwingPacket { Hand = Hand.MainHand });
        
        // If it's instabreak, finish immediately
        if (_totalBreakingTicks <= 0)
        {
            await _client.SendPacketAsync(new PlayerActionPacket
            {
                Status = PlayerActionPacket.StatusType.FinishedDigging,
                Position = new Vector3<double>(pos.X, pos.Y, pos.Z),
                Face = face,
                Sequence = entity.IncrementSequence()
            });
            // Client-side prediction: set block to air immediately
            _client.State.Level.HandleBlockUpdate(new Vector3<double>(pos.X, pos.Y, pos.Z), 0);
            _isBreakingBlock = false;
            _breakingBlockPosition = null;
        }
        
        return true;
    }

    /// <summary>
    /// Calculates the per-tick destroy progress for a block, matching Java's getDestroyProgress.
    /// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/level/block/state/BlockBehaviour.java:328-336
    /// Formula: playerDestroySpeed / blockDestroySpeed / modifier
    ///   - modifier = 30 if player has correct tool for drops, 100 otherwise
    ///   - Returns a fraction per tick; ticks to break = ceil(1.0 / result)
    /// </summary>
    private static double CalculateDestroyProgress(Slot heldItem, BlockState blockState)
    {
        float blockDestroySpeed = blockState.DestroySpeed;
        if (blockDestroySpeed == -1.0f)
        {
            return 0.0; // Unbreakable
        }
        
        if (blockDestroySpeed == 0.0f)
        {
            return 1.0; // Instant break (e.g. tall grass)
        }

        // Get player's destroy speed (tool effectiveness)
        float playerSpeed = GetPlayerDestroySpeed(heldItem, blockState);
        
        // Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/entity/player/Player.java:hasCorrectToolForDrops
        // player.hasCorrectToolForDrops(state) = !state.requiresCorrectToolForDrops() || item.isCorrectToolForDrops(state)
        bool hasCorrectTool = !blockState.RequiresCorrectToolForDrops || IsCorrectToolForDrops(heldItem, blockState);
        int modifier = hasCorrectTool ? 30 : 100;

        return playerSpeed / blockDestroySpeed / modifier;
    }

    /// <summary>
    /// Gets the player's mining speed for a given block, based on the held item.
    /// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/entity/player/Player.java:getDestroySpeed
    /// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/item/DiggerItem.java:getDestroySpeed
    /// </summary>
    private static float GetPlayerDestroySpeed(Slot item, BlockState state)
    {
        if (item.ItemId == null || item.ItemCount <= 0)
        {
            return 1.0f;
        }
        
        string? itemName = null;
        ClientState.ItemRegistry?.TryGetValue(item.ItemId.Value, out itemName);
        
        if (string.IsNullOrEmpty(itemName))
        {
            return 1.0f;
        }
        
        var toolType = ToolData.GetToolType(itemName);
        if (toolType == ToolData.ToolType.None)
        {
            return 1.0f;
        }
        
        if (!ToolData.IsCorrectTool(toolType, state))
        {
            return 1.0f;
        }
        
        var tier = ToolData.GetToolTier(itemName);
        return ToolData.GetSpeed(tier);
    }

    /// <summary>
    /// Checks if the held item is the correct tool for obtaining drops from the block.
    /// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/world/item/ItemStack.java:isCorrectToolForDrops
    /// </summary>
    private static bool IsCorrectToolForDrops(Slot item, BlockState state)
    {
        if (item.ItemId == null || item.ItemCount <= 0)
        {
            return false;
        }
        
        string? itemName = null;
        ClientState.ItemRegistry?.TryGetValue(item.ItemId.Value, out itemName);
        
        if (string.IsNullOrEmpty(itemName))
        {
            return false;
        }
        
        var toolType = ToolData.GetToolType(itemName);
        return toolType != ToolData.ToolType.None && ToolData.IsCorrectTool(toolType, state);
    }

    public async Task<bool> PlaceBlockAsync(Hand hand = Hand.MainHand)
    {
        if (!_client.State.LocalPlayer.HasEntity) return false;
        var entity = _client.State.LocalPlayer.Entity;

        if (entity.HeldItem.ItemId is null) return false;

        var hit = entity.GetLookingAtBlock(_client.State.Level, ReachDistance);
        if (hit is null) return false;

        var cursor = hit.GetInBlockPosition();
        
        _logger.LogInformation("Placing block at {Pos} facing {Face}", hit.BlockPosition, hit.Face);

        await _client.SendPacketAsync(new UseItemOnPacket
        {
            Hand = hand,
            Position = new Vector3<double>(hit.BlockPosition.X, hit.BlockPosition.Y, hit.BlockPosition.Z),
            BlockFace = hit.Face,
            Cursor = new Vector3<float>((float)cursor.X, (float)cursor.Y, (float)cursor.Z),
            InsideBlock = hit.InsideBlock,
            Sequence = entity.IncrementSequence()
        });
        
        await _client.SendPacketAsync(new SwingPacket { Hand = hand });
        return true;
    }

    public async Task<bool> PlaceBlockAtAsync(int x, int y, int z, Hand hand = Hand.MainHand)
    {
        if (!_client.State.LocalPlayer.HasEntity) return false;
        var entity = _client.State.LocalPlayer.Entity;

        if (entity.HeldItem.ItemId is null) return false;

        var level = _client.State.Level;

        // Find an adjacent block to place against (HORIZONTALS_BUT_ALSO_DOWN)
        // Try directions: down, north, south, east, west
        var directions = new[]
        {
            (dx: 0, dy: -1, dz: 0, face: BlockFace.Top),    // down
            (dx: 0, dy: 0, dz: -1, face: BlockFace.South),  // north
            (dx: 0, dy: 0, dz: 1, face: BlockFace.North),   // south
            (dx: 1, dy: 0, dz: 0, face: BlockFace.West),    // east
            (dx: -1, dy: 0, dz: 0, face: BlockFace.East)    // west
        };

        foreach (var (dx, dy, dz, face) in directions)
        {
            var againstX = x + dx;
            var againstY = y + dy;
            var againstZ = z + dz;

            var againstBlock = level.GetBlockAt(againstX, againstY, againstZ);
            
            // Check if we can place against this block
            // CanPlaceAgainst: needs a solid face (has collision) or is glass
            bool canPlaceAgainst = againstBlock != null && 
                (againstBlock.HasCollision || againstBlock.Name.Contains("glass", StringComparison.OrdinalIgnoreCase));
            
            if (canPlaceAgainst)
            {
                // Calculate rotation to look at the face center
                var faceCenterX = (x + againstX + 1.0) * 0.5;
                var faceCenterY = (y + againstY + 0.5) * 0.5;
                var faceCenterZ = (z + againstZ + 1.0) * 0.5;

                // Calculate yaw and pitch to look at target
                var deltaX = faceCenterX - entity.Position.X;
                var deltaY = faceCenterY - (entity.Position.Y + 1.6); // Head position
                var deltaZ = faceCenterZ - entity.Position.Z;
                
                var yaw = (float)(Math.Atan2(-deltaX, deltaZ) * (180.0 / Math.PI));
                var horizontalDist = Math.Sqrt(deltaX * deltaX + deltaZ * deltaZ);
                var pitch = (float)(-Math.Atan2(deltaY, horizontalDist) * (180.0 / Math.PI));

                // Update entity rotation
                entity.YawPitch = new Vector2<float>(yaw, pitch);

                // Send rotation packet
                await _client.SendPacketAsync(new MovePlayerRotationPacket
                {
                    Yaw = yaw,
                    Pitch = pitch,
                    Flags = MovementFlags.None
                });

                // Calculate cursor position (within the adjacent block we're placing against)
                // This is typically the center of the face we're clicking on
                var cursorX = 0.5f;
                var cursorY = 0.5f;
                var cursorZ = 0.5f;

                // Adjust cursor based on face direction
                switch (face)
                {
                    case BlockFace.Bottom: cursorY = 1.0f; break;
                    case BlockFace.Top: cursorY = 0.0f; break;
                    case BlockFace.North: cursorZ = 1.0f; break;
                    case BlockFace.South: cursorZ = 0.0f; break;
                    case BlockFace.West: cursorX = 1.0f; break;
                    case BlockFace.East: cursorX = 0.0f; break;
                }

                _logger.LogInformation("Placing block at ({X}, {Y}, {Z}) facing {Face} (against block at ({AgainstX}, {AgainstY}, {AgainstZ}))",
                    x, y, z, face, againstX, againstY, againstZ);

                // Send placement packet
                await _client.SendPacketAsync(new UseItemOnPacket
                {
                    Hand = hand,
                    Position = new Vector3<double>(againstX, againstY, againstZ), // Block we're placing against
                    BlockFace = face,
                    Cursor = new Vector3<float>(cursorX, cursorY, cursorZ),
                    InsideBlock = false,
                    Sequence = entity.IncrementSequence()
                });

                await _client.SendPacketAsync(new SwingPacket { Hand = hand });
                return true;
            }
        }

        _logger.LogWarning("Cannot place block at ({X}, {Y}, {Z}) - no valid adjacent block to place against", x, y, z);
        return false;
    }

    public async Task<bool> InteractAsync(Hand hand = Hand.MainHand)
    {
        if (!_client.State.LocalPlayer.HasEntity) return false;
        var entity = _client.State.LocalPlayer.Entity;

        // Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/client/multiplayer/MultiPlayerGameMode.java:309,371,420,426
        // Java calls ensureHasSentCarriedItem() before ALL interactions (useItemOn, useItem, interact, interactAt, attack)
        await EnsureHasSentCarriedItemAsync();

        // 1. Try Entity Interaction
        // We replicate existing logic: Find entity in look direction.
        var allEntities = _client.State.Level.GetAllPlayers().Select(p => p.Entity).Where(e => e != null).Cast<Entity>()
            // Plus non-player entities which might need a different registry access if they exist
            // For now, let's assume Level.GetAllEntityIds -> GetEntityOfId covers all.
            .Concat(_client.State.Level.GetAllEntityIds().Select(id => _client.State.Level.GetEntityOfId(id)!));
            
        var targetEntity = entity.GetLookingAtEntity(entity, allEntities, ReachDistance);

        if (targetEntity != null)
        {
            _logger.LogInformation("Interacting with entity {Id}", targetEntity.EntityId);
            
            // Look at target (optional, but helpful for server validation)
            var yawPitch = entity.GetYawPitchToTarget(entity, targetEntity);
            await _client.SendPacketAsync(new MovePlayerRotationPacket
            {
                Yaw = yawPitch.X,
                Pitch = yawPitch.Y,
                Flags = MovementFlags.None
            });

            await _client.SendPacketAsync(new InteractPacket
            {
                EntityId = targetEntity.EntityId,
                Type = InteractType.Interact,
                Hand = hand,
                SneakKeyPressed = entity.IsSneaking
            });
            await _client.SendPacketAsync(new SwingPacket { Hand = hand });
            return true;
        }

        // 2. Fallback to Block Interaction
        var hit = entity.GetLookingAtBlock(_client.State.Level, ReachDistance);
        if (hit is null)
        {
            _logger.LogWarning("InteractAsync: No block found in reach ({ReachDistance})", ReachDistance);
            return false;
        }

        _logger.LogInformation("InteractAsync: Interacting with block at {Pos} facing {Face}", hit.BlockPosition, hit.Face);

        var cursor = hit.GetInBlockPosition();
        await _client.SendPacketAsync(new UseItemOnPacket
        {
            Hand = hand,
            Position = new Vector3<double>(hit.BlockPosition.X, hit.BlockPosition.Y, hit.BlockPosition.Z),
            BlockFace = hit.Face,
            Cursor = new Vector3<float>((float)cursor.X, (float)cursor.Y, (float)cursor.Z),
            InsideBlock = hit.InsideBlock,
            Sequence = entity.IncrementSequence()
        });
        
        await _client.SendPacketAsync(new SwingPacket { Hand = hand });
        return true;
    }


    public async Task<bool> AttackAsync()
    {
        if (!_client.State.LocalPlayer.HasEntity) return false;
        var entity = _client.State.LocalPlayer.Entity;

        var allEntities = _client.State.Level.GetAllPlayers().Select(p => p.Entity).Where(e => e != null).Cast<Entity>()
            .Concat(_client.State.Level.GetAllEntityIds().Select(id => _client.State.Level.GetEntityOfId(id)!));

        var target = entity.GetLookingAtEntity(entity, allEntities, ReachDistance);
        if (target != null)
        {
            await AttackEntityAsync(target);
            return true;
        }

        return false;
    }

    public async Task AttackEntityAsync(Entity target)
    {
        if (!_client.State.LocalPlayer.HasEntity) return;
        var entity = _client.State.LocalPlayer.Entity;
        
        var yawPitch = entity.GetYawPitchToTarget(entity, target);
        await _client.SendPacketAsync(new MovePlayerRotationPacket
        {
            Yaw = yawPitch.X,
            Pitch = yawPitch.Y,
            Flags = MovementFlags.None
        });

        await _client.SendPacketAsync(new InteractPacket
        {
            EntityId = target.EntityId,
            Type = InteractType.Attack,
            SneakKeyPressed = entity.IsSneaking
        });
        await _client.SendPacketAsync(new SwingPacket { Hand = Hand.MainHand });
    }

    public Task SwingHandAsync(Hand hand)
    {
        return _client.SendPacketAsync(new SwingPacket { Hand = hand });
    }

    public async Task<bool> DropHeldItemAsync()
    {
        if (!_client.State.LocalPlayer.HasEntity) return false;
        var entity = _client.State.LocalPlayer.Entity;

        if (entity.HeldItem.ItemId is null) return false;

        await _client.SendPacketAsync(new PlayerActionPacket
        {
            Status = PlayerActionPacket.StatusType.DropItemStack,
            Position = new Vector3<double>(0, 0, 0),
            Face = BlockFace.Bottom,
            Sequence = 0
        });
        
        // Update local inventory state immediately
        entity.Inventory.SetSlot((short)(entity.HeldSlot + 36), new Slot());
        return true;
    }

    public async Task<bool> SetHeldSlotAsync(short slot)
    {
        if (!_client.State.LocalPlayer.HasEntity) return false;
        if (slot is < 0 or > 8) return false;

        await _client.SendPacketAsync(new SetCarriedItemPacket { Slot = slot });
        _client.State.LocalPlayer.Entity.HeldSlot = slot;
        return true;
    }

    public async Task<bool> StartDestroyBlockAsync(Vector3<int> position, BlockFace face)
    {
        if (!_client.State.LocalPlayer.HasEntity) return false;
        var entity = _client.State.LocalPlayer.Entity;

        _logger.LogDebug("Starting to break block at {Position} (Face={Face})", position, face);

        await _client.SendPacketAsync(new PlayerActionPacket
        {
            Status = PlayerActionPacket.StatusType.StartedDigging,
            Position = new Vector3<double>(position.X, position.Y, position.Z),
            Face = face,
            Sequence = entity.IncrementSequence()
        });

        _breakingBlockPosition = position;
        _breakingBlockFace = face;
        _isBreakingBlock = true;

        return true;
    }

    public async Task<bool> ContinueDestroyBlockAsync(Vector3<int> position, BlockFace face)
    {
        if (!_client.State.LocalPlayer.HasEntity) return false;
        var entity = _client.State.LocalPlayer.Entity;

        // Only continue if we're breaking the same block
        if (!_isBreakingBlock || _breakingBlockPosition != position || _breakingBlockFace != face)
        {
            // Start breaking this block instead
            return await StartDestroyBlockAsync(position, face);
        }

        // In Java, continueDestroyBlock sends periodic updates to the server
        // For now, we'll just maintain the state. In a full implementation,
        // this might send periodic damage packets or update progress.
        _logger.LogDebug("Continuing to break block at {Position}", position);

        return true;
    }

    public async Task ResetBlockRemovingAsync()
    {
        if (!_client.State.LocalPlayer.HasEntity) return;
        if (!_isBreakingBlock) return;

        var entity = _client.State.LocalPlayer.Entity;
        var position = _breakingBlockPosition!;
        var face = _breakingBlockFace!.Value;

        _logger.LogDebug("Stopping block breaking at {Position}", position);

        await _client.SendPacketAsync(new PlayerActionPacket
        {
            Status = PlayerActionPacket.StatusType.CancelledDigging,
            Position = new Vector3<double>(position.X, position.Y, position.Z),
            Face = face,
            Sequence = entity.IncrementSequence()
        });

        _breakingBlockPosition = null;
        _breakingBlockFace = null;
        _isBreakingBlock = false;
    }

    public bool HasBrokenBlock()
    {
        // Returns true if we're NOT currently breaking a block (i.e., block has been broken/completed)
        return !_isBreakingBlock;
    }

    public async Task WindowClickAsync(int windowId, int slotId, int mouseButton, ClickType clickType)
    {
        if (!_client.State.LocalPlayer.HasEntity) return;
        var entity = _client.State.LocalPlayer.Entity;

        // Get state ID based on window
        int stateId;
        if (windowId == 0)
        {
            // Player inventory window
            stateId = entity.Inventory.StateId;
        }
        else if (entity.CurrentContainer?.ContainerId == windowId)
        {
            // Open container window
            stateId = entity.CurrentContainer.StateId;
        }
        else
        {
            _logger.LogWarning("Cannot click window {WindowId}: window not found or not accessible", windowId);
            return;
        }

        // Convert ClickType to ClickContainerMode
        var mode = clickType.ToClickContainerMode();

        // Build click packet
        var clickPacket = new ContainerClickPacket
        {
            WindowId = windowId,
            StateId = stateId,
            Slot = (short)slotId,
            Button = (sbyte)mouseButton,
            Mode = mode,
            ChangedSlots = new Dictionary<short, Slot>(),
            CarriedItem = entity.Inventory.CursorItem
        };

        await _client.SendPacketAsync(clickPacket);
        _logger.LogDebug("Clicked window {WindowId} slot {SlotId} with {ClickType} (button: {Button})", 
            windowId, slotId, clickType, mouseButton);
    }

    /// <summary>
    /// Ensures the server knows which hotbar slot is currently selected.
    /// Only sends a packet when the slot has actually changed since last sent.
    /// Reference: minecraft-26.1-REFERENCE-ONLY/net/minecraft/client/multiplayer/MultiPlayerGameMode.java:299-306
    /// Java method: private void ensureHasSentCarriedItem()
    /// </summary>
    public async Task EnsureHasSentCarriedItemAsync()
    {
        if (!_client.State.LocalPlayer.HasEntity) return;
        var entity = _client.State.LocalPlayer.Entity;
        
        short currentSlot = entity.HeldSlot;
        if (currentSlot != _lastSentCarriedSlot)
        {
            _lastSentCarriedSlot = currentSlot;
            await _client.SendPacketAsync(new SetCarriedItemPacket { Slot = currentSlot });
            _logger.LogDebug("Synced carried item to server (slot: {Slot})", currentSlot);
        }
    }
    
    /// <summary>
    /// Backwards-compatible wrapper. Prefer EnsureHasSentCarriedItemAsync for Java parity.
    /// </summary>
    public async Task SyncHeldItemAsync()
    {
        await EnsureHasSentCarriedItemAsync();
    }

    public void SetHittingBlock(bool hittingBlock)
    {
        // Sets the internal state of whether we're currently hitting/breaking a block
        // Equivalent to Java's setIsHittingBlock() - used for state management
        _isBreakingBlock = hittingBlock;
        
        // If setting to false, also clear the breaking block position and face
        if (!hittingBlock)
        {
            _breakingBlockPosition = null;
            _breakingBlockFace = null;
        }
        
        _logger.LogDebug("Set hitting block state to {HittingBlock}", hittingBlock);
    }
}
