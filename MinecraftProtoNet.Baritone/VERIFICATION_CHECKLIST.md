# Verification Checklist: Baritone Vanilla Dependencies

This checklist validates completeness before Baritone integration. Use this to ensure all required vanilla components are implemented and verified.

---

## Pre-Integration Verification

### Phase 1: Critical Components (Priority 1)

#### DimensionType
- [ ] `DimensionType` class/struct exists
- [ ] `minY()` property/method returns `int` (typically -64)
- [ ] `height()` property/method returns `int` (typically 384)
- [ ] Accessible from `Level` (e.g., `level.DimensionType.minY()`)
- [ ] Initialized from server configuration
- [ ] Default values set correctly for 1.18+ (-64, 384)
- [ ] Test: Bounds checking in BlockStateInterface works

**Test Code:**
```csharp
var level = client.State.Level;
var minY = level.DimensionType.MinY; // Should return -64
var height = level.DimensionType.Height; // Should return 384
```

---

#### WorldBorder
- [ ] `WorldBorder` class exists
- [ ] Bounds properties exist (MinX, MaxX, MinZ, MaxZ)
- [ ] Center and size properties
- [ ] Distance calculation method
- [ ] Stored in `Level` or `ClientState`
- [ ] Initialized from server packets (WorldBorderCenter, WorldBorderSize)
- [ ] Test: Pathfinding bounds checking works

**Test Code:**
```csharp
var worldBorder = level.WorldBorder;
var isWithinBounds = worldBorder.Contains(x, z);
```

---

#### Block Breaking State Management
- [ ] Block breaking state tracking exists
- [ ] `continueDestroyBlock(BlockPos, Direction)` method implemented
- [ ] `stopDestroyBlock()` method implemented
- [ ] `isHittingBlock()` property/method exists
- [ ] State managed in `IInteractionManager` or separate class
- [ ] Test: Start, continue, and stop block breaking works

**Test Code:**
```csharp
await interactionManager.StartDigBlockAsync(pos, face); // Returns bool
var isHitting = interactionManager.IsHittingBlock; // Should return true
await interactionManager.ContinueDigBlockAsync(pos, face); // Returns bool
await interactionManager.StopDigBlockAsync(); // Stops breaking
```

---

#### Entity.blockPosition() Method
- [ ] `blockPosition()` method exists on `Entity` class
- [ ] Returns `Vector3<int>` (block coordinates)
- [ ] Converts `Position` (double) to block coordinates (int)
- [ ] Uses floor operation: `(int)Math.Floor(Position.X/Y/Z)`
- [ ] Test: Entity position conversion works

**Test Code:**
```csharp
var entity = client.State.LocalPlayer.Entity;
var blockPos = entity.BlockPosition(); // Should return Vector3<int>
// blockPos should equal floor of entity.Position
```

---

#### ChunkStatus Enum
- [ ] `ChunkStatus` enum exists
- [ ] `Full` value exists (at minimum)
- [ ] Used in `ChunkManager.GetChunk()` method (optional parameter)
- [ ] Test: Chunk access with status works

**Test Code:**
```csharp
var chunk = chunkManager.GetChunk(chunkX, chunkZ, ChunkStatus.Full);
// Or: var chunk = chunkManager.GetChunk(chunkX, chunkZ); // Defaults to Full
```

---

#### Camera Entity Access
- [ ] Camera entity access exists
- [ ] `GetCameraEntity()` method on `IMinecraftClient` or `ClientState`
- [ ] Returns `Entity` or null
- [ ] Defaults to `LocalPlayer.Entity` if no camera entity
- [ ] Test: Camera entity access works

**Test Code:**
```csharp
var cameraEntity = client.GetCameraEntity(); // Should return Entity
// Or: var cameraEntity = client.State.GetCameraEntity();
```

---

#### Thread Safety Checking
- [ ] `IsSameThread()` method exists on `IMinecraftClient`
- [ ] Returns `bool` indicating if current thread is main/game thread
- [ ] Uses `SynchronizationContext` or thread ID comparison
- [ ] Test: Thread safety check works

**Test Code:**
```csharp
var isMainThread = client.IsSameThread(); // Should return true on main thread
```

---

#### ClientChunkCache Abstraction
- [ ] `IClientChunkCache` interface exists (or verify `IChunkManager` sufficient)
- [ ] `GetChunk(int, int, ChunkStatus?, bool)` method exists
- [ ] `HasChunk(int, int)` method exists
- [ ] Thread-safe chunk access (if needed)
- [ ] Test: Chunk access works for pathfinding

**Test Code:**
```csharp
var chunk = chunkCache.GetChunk(chunkX, chunkZ, ChunkStatus.Full, false);
var hasChunk = chunkCache.HasChunk(chunkX, chunkZ);
```

---

### Phase 2: Pathfinding Requirements (Priority 2)

#### BlockPos Class (Optional - Verify Vector3<int> Sufficient)
- [ ] Decision made: Use `Vector3<int>` OR create `BlockPos` class
- [ ] If using `Vector3<int>`: Helper methods for immutable operations exist
- [ ] If using `BlockPos`: Class implements required methods
- [ ] Methods: `above()`, `below()`, `relative(Direction)`, `distSqr(BlockPos)`
- [ ] Test: Block position arithmetic works

**Test Code:**
```csharp
// If using Vector3<int>:
var pos = new Vector3<int>(0, 64, 0);
var above = pos + new Vector3<int>(0, 1, 0); // Helper method

// If using BlockPos:
var pos = new BlockPos(0, 64, 0);
var above = pos.Above();
```

---

#### MutableBlockPos Class (Optional)
- [ ] Decision made: Use mutable `Vector3<int>` OR create `MutableBlockPos`
- [ ] If using mutable `Vector3<int>`: Works for iteration
- [ ] If using `MutableBlockPos`: Class with `set(int, int, int)` method
- [ ] Test: Mutable position iteration works

---

#### InteractionResult Enum
- [ ] `InteractionResult` enum exists
- [ ] Values: `Success`, `Consume`, `Pass`, `Fail`
- [ ] `IInteractionManager` methods return `InteractionResult` (or verify `bool` sufficient)
- [ ] Test: Interaction results handled correctly

**Test Code:**
```csharp
var result = await interactionManager.PlaceBlockAsync(Hand.MainHand);
// Should return InteractionResult.Success or InteractionResult.Fail
```

---

#### Chunk.isEmpty() Method
- [ ] `isEmpty()` method exists on `Chunk` class
- [ ] Returns `bool`
- [ ] Checks if chunk has data (all sections empty or no data)
- [ ] Test: Empty chunk detection works

**Test Code:**
```csharp
var chunk = chunkManager.GetChunk(chunkX, chunkZ);
var isEmpty = chunk?.IsEmpty() ?? true;
```

---

#### Chunk Section Access
- [ ] `GetSection(int y)` method exists on `Chunk` class
- [ ] Returns `ChunkSection`
- [ ] Section indexing correct (y >> 4)
- [ ] Test: Chunk section access works

**Test Code:**
```csharp
var chunk = chunkManager.GetChunk(chunkX, chunkZ);
var section = chunk?.GetSection(y >> 4);
```

---

#### Entity Iteration Method
- [ ] Entity iteration method exists on `Level`
- [ ] Returns `IEnumerable<Entity>` or similar
- [ ] Iterates all entities (players + world entities)
- [ ] Test: Entity iteration works

**Test Code:**
```csharp
foreach (var entity in level.EntitiesForRendering())
{
    // Process entity
}
```

---

### Phase 3: Advanced Features (Priority 3)

#### Inventory Click Handling
- [ ] `HandleInventoryClick()` method exists
- [ ] Parameters: windowId, slotId, mouseButton, clickType, player
- [ ] `ClickType` enum exists
- [ ] Sends ClickContainerPacket correctly
- [ ] Test: Inventory clicks work

---

#### ClickType Enum
- [ ] `ClickType` enum exists
- [ ] Values match Minecraft click types
- [ ] Used in inventory click handling
- [ ] Test: Click types work correctly

---

### Phase 4: Integration Verification

#### IPlayerContext Integration
- [ ] `IPlayerContext` equivalent can be fully instantiated
- [ ] All required methods accessible
- [ ] `minecraft()` equivalent works
- [ ] `player()` equivalent works
- [ ] `world()` equivalent works
- [ ] `playerController()` equivalent works
- [ ] `objectMouseOver()` equivalent works
- [ ] Test: IPlayerContext provides all required data

**Test Code:**
```csharp
var ctx = new BaritonePlayerContext(client);
var player = ctx.Player(); // Should return Entity
var world = ctx.World(); // Should return Level
var hit = ctx.ObjectMouseOver(); // Should return RaycastHit
```

---

#### BlockStateInterface Integration
- [ ] `BlockStateInterface` equivalent can be constructed
- [ ] Bounds checking works (DimensionType)
- [ ] Chunk access works (ClientChunkCache)
- [ ] Block state access works
- [ ] World border checking works
- [ ] Test: BlockStateInterface can access all blocks

**Test Code:**
```csharp
var bsi = new BlockStateInterface(ctx);
var blockState = bsi.Get0(x, y, z); // Should return BlockState
var isLoaded = bsi.IsLoaded(x, z); // Should return bool
```

---

#### IPlayerController Integration
- [ ] `IPlayerController` equivalent can be instantiated
- [ ] Block breaking works (start/continue/stop)
- [ ] Block placing works
- [ ] Entity interaction works
- [ ] Game mode access works
- [ ] Test: All IPlayerController methods work

**Test Code:**
```csharp
var controller = new BaritonePlayerController(client);
var canBreak = controller.ClickBlock(pos, face); // Should return bool
var result = controller.ProcessRightClickBlock(player, world, hand, hitResult);
```

---

## Integration Test Checklist

Before starting Baritone implementation:

- [ ] All Priority 1 components implemented and tested
- [ ] All Priority 2 components implemented and tested (or verified sufficient)
- [ ] `IPlayerContext` can be fully instantiated
- [ ] `BlockStateInterface` can access all blocks
- [ ] `IPlayerController` can interact with world
- [ ] Entity system provides all required data
- [ ] Chunk system provides all required data
- [ ] Dimension system provides all required data
- [ ] World border provides all required data

---

## Verification Test Suite

Create unit tests for each component:

```csharp
// Example test structure
[Test]
public void TestDimensionType()
{
    var level = CreateTestLevel();
    Assert.That(level.DimensionType.MinY, Is.EqualTo(-64));
    Assert.That(level.DimensionType.Height, Is.EqualTo(384));
}

[Test]
public void TestWorldBorder()
{
    var worldBorder = CreateTestWorldBorder();
    Assert.That(worldBorder.Contains(0, 0), Is.True);
}

// ... more tests
```

---

## Sign-Off Checklist

Before proceeding to Baritone implementation:

- [ ] All Priority 1 components: ✅ Complete
- [ ] All Priority 2 components: ✅ Complete (or verified sufficient)
- [ ] Integration tests: ✅ Passing
- [ ] Code review: ✅ Completed
- [ ] Documentation: ✅ Updated

**Date:** ________________

**Reviewer:** ________________

**Status:** ✅ Ready for Baritone Integration

---

*Generated as part of Baritone Vanilla Dependencies Audit Plan*

