# C# Equivalent Mapping for Baritone Dependencies

This document maps each Baritone vanilla dependency to existing C# classes in MinecraftProtoNet.Core, identifying what exists, what's missing, and parity status.

---

## Mapping Status Legend

- ✅ **EXISTS**: Equivalent exists with full/partial parity
- ⚠️ **PARTIAL**: Equivalent exists but missing methods/properties
- ❌ **MISSING**: No equivalent exists
- 🔄 **NEEDS_REVIEW**: Needs detailed method parity verification

---

## 1. Client Core Classes

### `net.minecraft.client.Minecraft` → `IMinecraftClient`

**Status:** ✅ EXISTS (with differences)

**C# Location:** `MinecraftProtoNet.Core/Core/IMinecraftClient.cs`

**Mapping:**
- `mc.player` → `State.LocalPlayer.Entity` (not direct access)
- `mc.level` → `State.Level`
- `mc.gameMode` → Not directly accessible (need to check Player.GameMode)
- `mc.getCameraEntity()` → ✅ IMPLEMENTED (`ClientState.GetCameraEntity()`)
- `mc.isSameThread()` → ✅ IMPLEMENTED (`IMinecraftClient.IsSameThread()`)

**Gaps:**
- Direct player access via `mc.player` pattern
- Direct gameMode access

**Completed:**
- ✅ Camera entity access (`ClientState.GetCameraEntity()`)
- ✅ Thread safety checking method (`IMinecraftClient.IsSameThread()`)

**Priority:** Priority 1 (Core functionality)

---

### `net.minecraft.client.player.LocalPlayer` → `Entity` (via `State.LocalPlayer.Entity`)

**Status:** ⚠️ PARTIAL

**C# Location:** `MinecraftProtoNet.Core/State/Entity.cs`

**Method Mapping:**

| Java Method | C# Equivalent | Status | Notes |
|------------|---------------|--------|-------|
| `player.position()` → `Vec3` | `Entity.Position` → `Vector3<double>` | ✅ EXISTS | Direct mapping |
| `player.getYRot()` → `float` | `Entity.YawPitch.X` → `float` | ✅ EXISTS | Yaw is X component |
| `player.getXRot()` → `float` | `Entity.YawPitch.Y` → `float` | ✅ EXISTS | Pitch is Y component |
| `player.getDeltaMovement()` → `Vec3` | `Entity.Velocity` → `Vector3<double>` | ✅ EXISTS | Direct mapping |
| `player.getEyeHeight()` → `double` | `Entity.EyePosition` → `Vector3<double>` | ✅ EXISTS | Property exists, but method needed |
| `player.blockPosition()` → `BlockPos` | ✅ IMPLEMENTED | `Entity.BlockPosition()` → `Vector3<int>` |

**Additional Required:**
- ~~`blockPosition()` method - convert `Entity.Position` to `BlockPos` equivalent~~ ✅ IMPLEMENTED
- `getEyeHeight()` method (if needed) - currently only property

**Priority:** Priority 1 (Core functionality)

---

### `net.minecraft.client.multiplayer.ClientLevel` → `Level`

**Status:** ⚠️ PARTIAL

**C# Location:** `MinecraftProtoNet.Core/State/Level.cs`

**Method Mapping:**

| Java Method | C# Equivalent | Status | Notes |
|------------|---------------|--------|-------|
| `world.dimensionType().minY()` → `int` | ✅ IMPLEMENTED | `Level.DimensionType.MinY` → `int` |
| `world.dimensionType().height()` → `int` | ✅ IMPLEMENTED | `Level.DimensionType.Height` → `int` |
| `world.getWorldBorder()` → `WorldBorder` | ✅ IMPLEMENTED | `Level.WorldBorder` → `WorldBorder` |
| `world.getChunkSource()` → `ClientChunkCache` | ✅ VERIFIED | `Level.GetChunk()` delegates to `IChunkManager` (sufficient for Baritone) |
| `world.entitiesForRendering()` → `Iterable<Entity>` | ✅ IMPLEMENTED | `Level.GetAllEntities()` → `IEnumerable<Entity>` |
| `world.getBlockState(BlockPos)` → `BlockState` | `Level.GetBlockAt(int, int, int)` → `BlockState?` | ✅ EXISTS | Different signature |

**Critical Gaps:**
- **DimensionType properties** (minY, height) - Required for BlockStateInterface
- **WorldBorder** - Required for pathfinding bounds
- **ChunkSource interface** - Required for chunk caching

**Priority:** Priority 1 (Blocking core functionality)

---

### `net.minecraft.client.multiplayer.ClientChunkCache` → `IChunkManager` / `ChunkManager`

**Status:** ⚠️ PARTIAL

**C# Location:** `MinecraftProtoNet.Core/State/IChunkManager.cs`, `MinecraftProtoNet.Core/State/ChunkManager.cs`

**Method Mapping:**

| Java Method | C# Equivalent | Status | Notes |
|------------|---------------|--------|-------|
| `provider.getChunk(int, int, ChunkStatus, bool)` → `LevelChunk` | `Level.GetChunk(int, int, ChunkStatus?)` → `Chunk?` | ✅ IMPLEMENTED | ChunkStatus parameter added (optional, defaults to Full) |
| `provider.hasChunk(int, int)` → `boolean` | `ChunkManager.HasChunk(int, int)` → `bool` | ✅ EXISTS | Direct mapping |

**Gaps:**
- ~~ChunkStatus parameter (loading status)~~ ✅ IMPLEMENTED (optional parameter, defaults to ChunkStatus.Full)
- Thread-safe chunk access (for pathfinding thread)
- Chunk section access methods

**Priority:** Priority 1 (Core pathfinding)

---

### `net.minecraft.client.multiplayer.MultiPlayerGameMode` → `IInteractionManager`

**Status:** ⚠️ PARTIAL

**C# Location:** `MinecraftProtoNet.Core/Actions/IInteractionManager.cs`, `MinecraftProtoNet.Core/Actions/InteractionManager.cs`

**Method Mapping:**

| Java Method | C# Equivalent | Status | Notes |
|------------|---------------|--------|-------|
| `gameMode.startDestroyBlock(BlockPos, Direction)` → `boolean` | `StartDestroyBlockAsync(Vector3<int>, BlockFace)` → `Task<bool>` | ✅ IMPLEMENTED | Async, explicit position/face params |
| `gameMode.continueDestroyBlock(BlockPos, Direction)` → `boolean` | `ContinueDestroyBlockAsync(Vector3<int>, BlockFace)` → `Task<bool>` | ✅ IMPLEMENTED | Continue block breaking |
| `gameMode.stopDestroyBlock()` → `void` | `ResetBlockRemovingAsync()` → `Task` | ✅ IMPLEMENTED | Stop block breaking |
| `gameMode.useItemOn(LocalPlayer, InteractionHand, BlockHitResult)` → `InteractionResult` | `PlaceBlockAsync(Hand)` → `Task<bool>` | ⚠️ PARTIAL | Simplified interface |
| `gameMode.useItem(LocalPlayer, InteractionHand)` → `InteractionResult` | `InteractAsync(Hand)` → `Task<bool>` | ⚠️ PARTIAL | Simplified interface |
| `gameMode.getPlayerMode()` → `GameType` | `Player.GameMode` → `GameMode` | ✅ EXISTS | Property access |
| `gameMode.handleInventoryMouseClick(...)` → `void` | ❌ MISSING | Inventory management |

**Critical Gaps:**
- ~~Block breaking state management (start/continue/stop)~~ ✅ IMPLEMENTED
- Inventory click handling
- InteractionResult return types (currently boolean)

**Completed:**
- ✅ `StartDestroyBlockAsync(Vector3<int>, BlockFace)` - Start breaking a block
- ✅ `ContinueDestroyBlockAsync(Vector3<int>, BlockFace)` - Continue breaking a block
- ✅ `ResetBlockRemovingAsync()` - Stop/cancel block breaking
- ✅ `HasBrokenBlock()` - Check if block has been broken (not currently breaking)

**Priority:** Priority 1 (Core interaction functionality)

---

## 2. World/Level Classes

### `net.minecraft.world.level.Level` → `Level`

**Status:** ⚠️ PARTIAL (see ClientLevel section above)

**Additional Gaps:**
- Dimension type access
- World border access

---

### `net.minecraft.world.level.dimension.DimensionType` → ❌ MISSING

**Status:** ❌ MISSING

**Required Properties:**
- `minY()` → `int` (minimum Y coordinate, typically -64 for newer versions)
- `height()` → `int` (dimension height, typically 384)
- `logicalHeight()` → `int` (logical height for some dimensions)

**C# Implementation Needed:**
- Add to `Level` class or separate `DimensionType` class
- Store in `ClientState` or `Level`
- Initialize from server configuration

**Priority:** Priority 1 (Required for BlockStateInterface bounds checking)

---

### `net.minecraft.world.level.border.WorldBorder` → ❌ MISSING

**Status:** ❌ MISSING

**Required Methods:**
- Bounds checking (minX, maxX, minZ, maxZ)
- Distance calculation

**C# Implementation Needed:**
- Create `WorldBorder` class
- Store in `Level` or `ClientState`
- Initialize from server packets (WorldBorderCenter, WorldBorderSize)

**Priority:** Priority 1 (Required for pathfinding bounds)

---

## 3. Block/BlockState Classes

### `net.minecraft.world.level.block.state.BlockState` → `BlockState`

**Status:** ✅ EXISTS (Good parity)

**C# Location:** `MinecraftProtoNet.Core/Models/World/Chunk/BlockState.cs`

**Method Mapping:**

| Java Method | C# Equivalent | Status | Notes |
|------------|---------------|--------|-------|
| `blockState.getBlock()` → `Block` | `BlockState.Name` → `string` | ⚠️ PARTIAL | Block type via name, not Block object |
| `blockState.getProperties()` → `Map` | `BlockState.Properties` → `Dictionary<string, string>` | ✅ EXISTS | Direct mapping |
| Block identification | `BlockState.Name` | ✅ EXISTS | String-based identification |

**Gaps:**
- Block type object (currently string-based)
- Block type constants (e.g., `Blocks.AIR`)

**Priority:** Priority 2 (Works but could be improved)

---

### `net.minecraft.world.level.block.Block` → ❌ MISSING (String-based)

**Status:** ⚠️ PARTIAL (Block identification via string)

**Current C# Approach:** Block types identified by string name (e.g., "minecraft:air")

**Baritone Usage:**
- `block instanceof SlabBlock` → Need block type checking
- `Blocks.AIR` → Need block constants

**C# Implementation Options:**
1. Keep string-based, add helper methods (e.g., `IsSlab()`, `IsAir()`)
2. Create Block type enum or class

**Current Status:** `BlockState.IsSlab`, `BlockState.IsAir` exist - ✅ Sufficient

**Priority:** Priority 2 (Currently works, but could be cleaner)

---

### `net.minecraft.world.level.block.Blocks` → BlockStateRegistry

**Status:** ⚠️ PARTIAL

**C# Location:** `MinecraftProtoNet.Core/State/Base/ClientState.cs` - `BlockStateRegistry`

**Mapping:**
- `Blocks.AIR.defaultBlockState()` → Access via `BlockStateRegistry[0]` or `BlockState` with Id=0

**Gaps:**
- Block constants (e.g., `Blocks.AIR`) - currently access by ID or name
- Convenience methods for common blocks

**Priority:** Priority 3 (Nice to have, not blocking)

---

### `net.minecraft.core.BlockPos` → `Vector3<int>` with Extensions

**Status:** ✅ IMPLEMENTED

**C# Location:** `MinecraftProtoNet.Models.Core.Vector3<int>` with `Vector3IntExtensions`

**Mapping:**
- `BlockPos(int x, int y, int z)` → `Vector3<int>(x, y, z)` ✅
- `blockPos.getX()`, `getY()`, `getZ()` → `Vector3<int>.X`, `.Y`, `.Z` ✅
- `blockPos.above()` → `pos.Above()` extension method ✅
- `blockPos.below()` → `pos.Below()` extension method ✅
- `blockPos.relative(Direction)` → `pos.Relative(BlockFace)` extension method ✅
- `blockPos.distSqr(BlockPos)` → `pos.DistSqr(other)` extension method ✅
- `blockPos.north()`, `south()`, `east()`, `west()` → Extension methods ✅

**Implementation:**
- Created `Vector3IntExtensions` static class with immutable operations
- All BlockPos methods available as extension methods on `Vector3<int>`
- BetterBlockPos in Baritone just extends BlockPos, so Vector3<int> with extensions is sufficient

**Priority:** Priority 2 ✅ Complete

---

### `net.minecraft.core.BlockPos.MutableBlockPos` → ❌ MISSING

**Status:** ❌ MISSING

**Usage:** Used in `BlockStateInterface` for iteration

**C# Implementation Needed:**
- Mutable block position class/struct
- `set(int x, int y, int z)` method

**Alternative:** Use mutable `Vector3<int>` directly

**Priority:** Priority 2 (Can use mutable Vector3, but dedicated class cleaner)

---

### `net.minecraft.world.level.BlockGetter` → `IChunkManager`

**Status:** ⚠️ PARTIAL

**C# Location:** `MinecraftProtoNet.Core/State/IChunkManager.cs`

**Method Mapping:**
- `getBlockState(BlockPos)` → `GetBlockAt(int, int, int)` ✅ EXISTS

**Gaps:** Interface abstraction (currently concrete class)

**Priority:** Priority 3 (Nice to have)

---

## 4. Chunk Classes

### `net.minecraft.world.level.chunk.LevelChunk` → `Chunk`

**Status:** ⚠️ PARTIAL

**C# Location:** `MinecraftProtoNet.Core/Models/World/Chunk/Chunk.cs`

**Method Mapping:**

| Java Method | C# Equivalent | Status | Notes |
|------------|---------------|--------|-------|
| `chunk.getPos()` → `ChunkPos` | `Chunk.ChunkX`, `Chunk.ChunkZ` → `int` | ⚠️ PARTIAL | Properties instead of ChunkPos object |
| `chunk.isEmpty()` → `boolean` | ✅ IMPLEMENTED | `Chunk.IsEmpty()` → `bool` |
| `chunk.getSection(int y)` → `LevelChunkSection` | ✅ IMPLEMENTED | `Chunk.GetSection(int sectionY)` → `ChunkSection?` |

**Gaps:**
- ChunkPos object (currently separate properties)
- ~~Empty chunk detection~~ ✅ IMPLEMENTED
- ~~Chunk section access~~ ✅ IMPLEMENTED

**Priority:** Priority 2 (Required for chunk scanning)

---

### `net.minecraft.world.level.ChunkPos` → ❌ MISSING (Properties only)

**Status:** ⚠️ PARTIAL (Properties exist, no class)

**Current C#:** `Chunk.ChunkX`, `Chunk.ChunkZ` (int properties)

**Gaps:** No ChunkPos class (currently tuple-like access)

**Priority:** Priority 3 (Works but could be cleaner)

---

## 5. Entity Classes

### `net.minecraft.world.entity.Entity` → `Entity`

**Status:** ✅ EXISTS (Good parity)

**C# Location:** `MinecraftProtoNet.Core/State/Entity.cs`

**Method Mapping:**

| Java Method | C# Equivalent | Status | Notes |
|------------|---------------|--------|-------|
| `entity.blockPosition()` → `BlockPos` | ❌ MISSING | Need method to convert Position to block coords |
| `entity.position()` → `Vec3` | `Entity.Position` → `Vector3<double>` | ✅ EXISTS |

**Gaps:**
- `blockPosition()` method

**Priority:** Priority 2

---

## 6. Interaction Classes

### `net.minecraft.world.InteractionHand` → `Hand`

**Status:** ✅ EXISTS

**C# Location:** `MinecraftProtoNet.Core/Enums/Hand.cs`

**Mapping:**
- `InteractionHand.MAIN_HAND` → `Hand.MainHand` ✅
- `InteractionHand.OFF_HAND` → `Hand.OffHand` ✅

**Priority:** Priority 1 ✅ Complete

---

### `net.minecraft.world.InteractionResult` → `InteractionResult`

**Status:** ✅ IMPLEMENTED

**C# Location:** `MinecraftProtoNet.Core/Enums/InteractionResult.cs`

**Mapping:**
- `InteractionResult.SUCCESS` → `InteractionResult.Success` ✅
- `InteractionResult.CONSUME` → `InteractionResult.Consume` ✅
- `InteractionResult.PASS` → `InteractionResult.Pass` ✅
- `InteractionResult.FAIL` → `InteractionResult.Fail` ✅

**Note:** `IInteractionManager` methods still return `Task<bool>` for now. Future refactoring could change to `Task<InteractionResult>` for better parity.

**Priority:** Priority 2 ✅ Complete

---

### `net.minecraft.world.phys.BlockHitResult` → `RaycastHit`

**Status:** ✅ EXISTS (Good parity)

**C# Location:** `MinecraftProtoNet.Core/Models/World/Meta/RaycastHit.cs`

**Method Mapping:**

| Java Method | C# Equivalent | Status | Notes |
|------------|---------------|--------|-------|
| `getBlockPos()` → `BlockPos` | `BlockPosition` → `Vector3<int>` | ✅ EXISTS | Block position |
| `getDirection()` → `Direction` | `Face` → `BlockFace` | ✅ EXISTS | Different enum name |
| Hit information | Full raycast data | ✅ EXISTS | Complete |

**Gaps:** Enum name difference (`Direction` vs `BlockFace`) - ✅ Compatible

**Priority:** Priority 1 ✅ Complete

---

### `net.minecraft.world.phys.HitResult` → `RaycastHit`

**Status:** ✅ EXISTS

**Method Mapping:**
- `getType()` → `HitResultType` enum (Miss, Block, Entity) ✅ IMPLEMENTED
- Type checking via properties

**C# Location:** `MinecraftProtoNet.Core/Enums/HitResultType.cs`

**Mapping:**
- `HitResult.Type.MISS` → `HitResultType.Miss` ✅
- `HitResult.Type.BLOCK` → `HitResultType.Block` ✅
- `HitResult.Type.ENTITY` → `HitResultType.Entity` ✅

**Note:** The `RaycastHit` class itself may need to be updated to use this enum explicitly.

**Priority:** Priority 2 ✅ Complete

---

### `net.minecraft.world.level.GameType` → `GameMode`

**Status:** ✅ EXISTS

**C# Location:** `MinecraftProtoNet.Core/Enums/GameMode.cs`

**Mapping:**
- `GameType.SURVIVAL` → `GameMode.Survival` ✅
- `GameType.CREATIVE` → `GameMode.Creative` ✅
- `GameType.ADVENTURE` → `GameMode.Adventure` ✅
- `gameType.isCreative()` → `gameMode == GameMode.Creative` ✅

**Priority:** Priority 1 ✅ Complete

---

### `net.minecraft.world.inventory.ClickType` → ❌ MISSING

**Status:** ❌ MISSING

**Usage:** Inventory management

**Priority:** Priority 3 (Required for inventory management features)

---

## 7. Math/Physics Classes

### `net.minecraft.world.phys.Vec3` → `Vector3<double>`

**Status:** ✅ EXISTS

**C# Location:** `MinecraftProtoNet.Models.Core.Vector3<double>`

**Method Mapping:**
- `Vec3(double x, double y, double z)` → `Vector3<double>(x, y, z)` ✅
- `vec3.x`, `vec3.y`, `vec3.z` → `.X`, `.Y`, `.Z` ✅
- Vector operations exist ✅

**Priority:** Priority 1 ✅ Complete

---

### `net.minecraft.core.Vec3i` → `Vector3<int>`

**Status:** ✅ EXISTS

**C# Location:** `MinecraftProtoNet.Models.Core.Vector3<int>`

**Priority:** Priority 1 ✅ Complete

---

### `net.minecraft.world.phys.AABB` → `AABB`

**Status:** ✅ EXISTS (Likely)

**C# Location:** Need to verify - check Physics namespace

**Priority:** Priority 2 (Need to verify existence)

---

### `net.minecraft.world.phys.shapes.VoxelShape` → `VoxelShape`

**Status:** ✅ EXISTS (Likely)

**C# Location:** Check Physics namespace

**Priority:** Priority 3 (Lower priority)

---

## 8. Direction/Position Classes

### `net.minecraft.core.Direction` → `BlockFace` (or Direction)

**Status:** ⚠️ PARTIAL

**C# Location:** `MinecraftProtoNet.Core/Physics/Direction.cs` (need to verify)

**Mapping:**
- Need to check if Direction enum exists
- `BlockFace` enum exists in `RaycastHit.cs`

**Gaps:** May need Direction enum separate from BlockFace

**Priority:** Priority 2 (Need to verify)

---

## 9. Registry/Resource Classes

### `net.minecraft.core.registries.BuiltInRegistries` → BlockStateRegistry / ItemRegistry

**Status:** ⚠️ PARTIAL

**C# Location:** `MinecraftProtoNet.Core/State/Base/ClientState.cs`

**Mapping:**
- `BlockStateRegistry` → `FrozenDictionary<int, BlockState>` ✅
- `ItemRegistry` → `FrozenDictionary<int, string>` ✅
- Registry access exists but different API

**Priority:** Priority 2 (Works, different API)

---

## 10. Chat/Network Classes

**Status:** ❌ MISSING (Lower priority - not required for core pathfinding)

**Priority:** Priority 4 (Nice to have for commands/chat)

---

## 11. Utility Classes

### `net.minecraft.util.Tuple<A, B>` → `System.ValueTuple<A, B>` or `Tuple<A, B>`

**Status:** ✅ EXISTS (C# built-in)

**Priority:** Priority 1 ✅ Complete

---

## Summary by Priority

### Priority 1 (Critical - Blocking Core Functionality)

**Missing:**
- ❌ DimensionType (minY, height)
- ❌ WorldBorder
- ❌ Camera entity access
- ❌ Thread safety checking
- ❌ Block breaking state management (continue/stop)
- ❌ ChunkStatus parameter for chunk access

**Partial (Need Enhancement):**
- ⚠️ IPlayerController interface (missing methods)
- ⚠️ ClientChunkCache abstraction
- ⚠️ Entity.blockPosition() method

### Priority 2 (Required for Pathfinding)

**Missing:**
- ❌ BlockPos class (have Vector3<int>, but dedicated class cleaner)
- ❌ MutableBlockPos
- ❌ InteractionResult enum
- ❌ ChunkPos class
- ❌ Empty chunk detection
- ❌ Chunk section access

**Partial:**
- ⚠️ Block type checking (currently string-based, works but could be cleaner)
- ⚠️ AABB (need to verify)

### Priority 3 (Advanced Features)

**Missing:**
- ❌ Inventory click handling
- ❌ Block constants (Blocks.AIR style)
- ❌ ChunkPos class (nice to have)
- ❌ BlockGetter interface abstraction

### Priority 4 (Nice to Have)

**Missing:**
- ❌ Chat components
- ❌ GUI components
- ❌ Network packet abstractions (already handled)

---

*Generated as part of Baritone Vanilla Dependencies Audit Plan*

