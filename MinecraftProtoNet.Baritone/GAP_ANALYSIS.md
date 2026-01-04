# Gap Analysis: Baritone Vanilla Dependencies

This document provides a structured analysis of what exists vs. what's missing in the C# project for Baritone implementation.

**Last Updated:** Generated as part of Baritone Vanilla Dependencies Audit Plan

---

## Status Legend

- ✅ **EXISTS**: Equivalent exists with full parity
- ⚠️ **PARTIAL**: Equivalent exists but missing methods/properties
- ❌ **MISSING**: No equivalent exists
- 🔄 **NEEDS_VERIFICATION**: Needs detailed method parity check

---

## Existing Components (With Verification Status)

### Client Core Classes

| Java Class | C# Equivalent | Location | Status | Notes |
|------------|---------------|----------|--------|-------|
| `Minecraft` | `IMinecraftClient` | `MinecraftProtoNet.Core/Core/IMinecraftClient.cs` | ⚠️ PARTIAL | Missing camera entity, thread check, direct player/gameMode access |
| `LocalPlayer` | `Entity` (via `State.LocalPlayer.Entity`) | `MinecraftProtoNet.Core/State/Entity.cs` | ⚠️ PARTIAL | Missing `blockPosition()` method, `getEyeHeight()` as method |
| `ClientLevel` | `Level` | `MinecraftProtoNet.Core/State/Level.cs` | ⚠️ PARTIAL | Missing DimensionType, WorldBorder, ChunkSource abstraction |
| `ClientChunkCache` | `IChunkManager` / `ChunkManager` | `MinecraftProtoNet.Core/State/` | ⚠️ PARTIAL | Missing ChunkStatus parameter, thread-safe access |
| `MultiPlayerGameMode` | `IInteractionManager` | `MinecraftProtoNet.Core/Actions/` | ⚠️ PARTIAL | Missing block breaking state, inventory clicks, InteractionResult enum |

### World/Level Classes

| Java Class | C# Equivalent | Location | Status | Notes |
|------------|---------------|----------|--------|-------|
| `Level` / `ClientLevel` | `Level` | `MinecraftProtoNet.Core/State/Level.cs` | ⚠️ PARTIAL | See ClientLevel above |
| `DimensionType` | ❌ MISSING | - | ❌ MISSING | **Critical gap** - Required for BlockStateInterface bounds |
| `WorldBorder` | ❌ MISSING | - | ❌ MISSING | **Critical gap** - Required for pathfinding bounds |
| `ChunkSource` | ❌ MISSING | - | ❌ MISSING | Interface abstraction missing (ChunkManager exists but no interface) |

### Block/BlockState Classes

| Java Class | C# Equivalent | Location | Status | Notes |
|------------|---------------|----------|--------|-------|
| `BlockState` | `BlockState` | `MinecraftProtoNet.Core/Models/World/Chunk/BlockState.cs` | ✅ EXISTS | Good parity, properties exist |
| `Block` | String-based (BlockState.Name) | - | ⚠️ PARTIAL | Type checking via string, works but no Block object |
| `Blocks` | `BlockStateRegistry` | `MinecraftProtoNet.Core/State/Base/ClientState.cs` | ⚠️ PARTIAL | Registry exists, no constants (e.g., `Blocks.AIR`) |
| `BlockPos` | `Vector3<int>` | `MinecraftProtoNet.Models.Core.Vector3<int>` | ⚠️ PARTIAL | Works but no dedicated class, missing immutable operations |
| `BlockPos.MutableBlockPos` | ❌ MISSING | - | ❌ MISSING | Can use mutable Vector3, but dedicated class cleaner |
| `BlockGetter` | `IChunkManager` | `MinecraftProtoNet.Core/State/IChunkManager.cs` | ✅ EXISTS | Interface exists, methods match |

### Chunk Classes

| Java Class | C# Equivalent | Location | Status | Notes |
|------------|---------------|----------|--------|-------|
| `LevelChunk` | `Chunk` | `MinecraftProtoNet.Core/Models/World/Chunk/Chunk.cs` | ⚠️ PARTIAL | Missing `isEmpty()`, section access methods |
| `LevelChunkSection` | `ChunkSection` | `MinecraftProtoNet.Core/Models/World/Chunk/ChunkSection.cs` | ✅ EXISTS | Exists, need to verify methods |
| `ChunkPos` | Properties (ChunkX, ChunkZ) | `Chunk` class | ⚠️ PARTIAL | No dedicated class, properties exist |
| `ChunkStatus` | ❌ MISSING | - | ❌ MISSING | Loading status enum missing |
| `PalettedContainer` | `PalettedContainer` | `MinecraftProtoNet.Core/Models/World/Chunk/PalettedContainer.cs` | ✅ EXISTS | Exists |

### Entity Classes

| Java Class | C# Equivalent | Location | Status | Notes |
|------------|---------------|----------|--------|-------|
| `Entity` | `Entity` | `MinecraftProtoNet.Core/State/Entity.cs` | ✅ EXISTS | Good parity, missing `blockPosition()` method |
| `LocalPlayer` | `Entity` (via LocalPlayer.Entity) | `MinecraftProtoNet.Core/State/Entity.cs` | ⚠️ PARTIAL | See LocalPlayer above |
| `Player` | `Player` | `MinecraftProtoNet.Core/State/Player.cs` | ✅ EXISTS | Exists |
| `LivingEntity` | - | - | ❌ MISSING | Not needed for core pathfinding (lower priority) |
| `ItemEntity` | `WorldEntity` | `MinecraftProtoNet.Core/State/WorldEntityRegistry.cs` | ⚠️ PARTIAL | Generic entity system exists |

### Interaction Classes

| Java Class | C# Equivalent | Location | Status | Notes |
|------------|---------------|----------|--------|-------|
| `InteractionHand` | `Hand` | `MinecraftProtoNet.Core/Enums/Hand.cs` | ✅ EXISTS | Perfect mapping |
| `InteractionResult` | ❌ MISSING | - | ❌ MISSING | Currently boolean, enum needed |
| `BlockHitResult` | `RaycastHit` | `MinecraftProtoNet.Core/Models/World/Meta/RaycastHit.cs` | ✅ EXISTS | Good parity |
| `HitResult` | `RaycastHit` | `MinecraftProtoNet.Core/Models/World/Meta/RaycastHit.cs` | ✅ EXISTS | Exists |
| `GameType` | `GameMode` | `MinecraftProtoNet.Core/Enums/GameMode.cs` | ✅ EXISTS | Perfect mapping |
| `ClickType` | ❌ MISSING | - | ❌ MISSING | Inventory click type enum |

### Math/Physics Classes

| Java Class | C# Equivalent | Location | Status | Notes |
|------------|---------------|----------|--------|-------|
| `Vec3` | `Vector3<double>` | `MinecraftProtoNet.Models.Core.Vector3<double>` | ✅ EXISTS | Perfect mapping |
| `Vec3i` | `Vector3<int>` | `MinecraftProtoNet.Models.Core.Vector3<int>` | ✅ EXISTS | Perfect mapping |
| `AABB` | `AABB` | `MinecraftProtoNet.Core/Physics/Shapes/AABB.cs` | ✅ EXISTS | Exists |
| `VoxelShape` | `VoxelShape` | `MinecraftProtoNet.Core/Physics/Shapes/` | ✅ EXISTS | Exists |
| `ClipContext` | ❌ MISSING | - | ❌ MISSING | Raycast context (lower priority) |

### Direction/Position Classes

| Java Class | C# Equivalent | Location | Status | Notes |
|------------|---------------|----------|--------|-------|
| `Direction` | `BlockFace` enum + `Direction` static class | `MinecraftProtoNet.Core/Enums/BlockFace.cs`, `MinecraftProtoNet.Core/Physics/Direction.cs` | ✅ EXISTS | Different structure (enum + static class) but works |

### Registry/Resource Classes

| Java Class | C# Equivalent | Location | Status | Notes |
|------------|---------------|----------|--------|-------|
| `BuiltInRegistries` | `BlockStateRegistry`, `ItemRegistry` | `MinecraftProtoNet.Core/State/Base/ClientState.cs` | ⚠️ PARTIAL | Registry exists, different API |
| `ResourceKey<T>` | ❌ MISSING | - | ❌ MISSING | Resource key generic (lower priority) |
| `Identifier` | String-based | - | ⚠️ PARTIAL | String-based identifiers work |

---

## Missing Components (By Priority)

### Priority 1 (Critical - Blocking Core Functionality)

#### 1. DimensionType
**Status:** ❌ MISSING

**Required Properties:**
- `minY()` → `int` (typically -64 for 1.18+)
- `height()` → `int` (typically 384)
- `logicalHeight()` → `int` (optional)

**Usage in Baritone:**
- `BlockStateInterface.get0()` - Bounds checking (y < minY || y >= minY + height)
- `WorldScanner` - Coordinate iteration order
- `CachedChunk` / `CachedRegion` - Dimension bounds

**Implementation Complexity:** Low

**Implementation Notes:**
- Add to `Level` class or separate `DimensionType` class
- Store in `ClientState` or `Level`
- Initialize from server configuration (likely from Login/Respawn packets)
- Default values: minY = -64, height = 384 (1.18+)

**Java Reference:**
- `baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/utils/BlockStateInterface.java:98-100`
- `baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/cache/WorldScanner.java:53`

---

#### 2. WorldBorder
**Status:** ❌ MISSING

**Required Methods/Properties:**
- Bounds checking (minX, maxX, minZ, maxZ)
- Center and size
- Distance calculation

**Usage in Baritone:**
- `BetterWorldBorder` wrapper class
- Pathfinding bounds checking
- `AStarPathFinder` - Bounds validation

**Implementation Complexity:** Low-Medium

**Implementation Notes:**
- Create `WorldBorder` class
- Store in `Level` or `ClientState`
- Initialize from server packets (WorldBorderCenter, WorldBorderSize)
- Default: infinite border (no restrictions)

**Java Reference:**
- `baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/utils/pathing/BetterWorldBorder.java`
- `baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/pathing/calc/AStarPathFinder.java:66`

---

#### 3. Block Breaking State Management
**Status:** ❌ MISSING

**Required Methods:**
- `continueDestroyBlock(BlockPos, Direction)` → `boolean`
- `stopDestroyBlock()` → `void`
- `isHittingBlock()` → `boolean` (state check)

**Usage in Baritone:**
- `BaritonePlayerController.hasBrokenBlock()` - Checks breaking state
- `BaritonePlayerController.resetBlockRemoving()` - Resets breaking state
- Block breaking sequence management

**Implementation Complexity:** Medium

**Implementation Notes:**
- Add to `IInteractionManager`
- Track breaking state (current block position, progress)
- Manage breaking sequence packets (PlayerActionPacket with actions: START_DIGGING, CANCEL_DIGGING, FINISH_DIGGING, DROP_ITEM_STACK, DROP_ITEM, RELEASE_USE_ITEM)

**Java Reference:**
- `baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/utils/player/BaritonePlayerController.java:54-67`
- `baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/utils/IPlayerController.java:40-44`

---

#### 4. Entity.blockPosition() Method
**Status:** ❌ MISSING

**Required Method:**
- `blockPosition()` → `BlockPos` / `Vector3<int>`

**Usage in Baritone:**
- `BaritonePlayerContext.viewerPos()` - Camera entity block position
- `MineProcess` - Item entity positions
- `FollowProcess` - Following entity position

**Implementation Complexity:** Low

**Implementation Notes:**
- Add method to `Entity` class
- Convert `Position` (Vec3/double) to block coordinates (int)
- `return new Vector3<int>((int)Math.Floor(Position.X), (int)Math.Floor(Position.Y), (int)Math.Floor(Position.Z));`

**Java Reference:**
- `baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/utils/player/BaritonePlayerContext.java:74-75`
- `baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/process/MineProcess.java:351`

---

#### 5. ChunkStatus Enum
**Status:** ❌ MISSING

**Required Values:**
- `FULL` - Fully loaded chunk
- Other statuses (optional, mainly need FULL)

**Usage in Baritone:**
- `BlockStateInterface` - Chunk loading status check
- `ClientChunkCache.getChunk()` parameter

**Implementation Complexity:** Low

**Implementation Notes:**
- Create enum `ChunkStatus` with `Full` value
- Add parameter to `ChunkManager.GetChunk()` (optional, default to Full)
- Currently chunks are either loaded or not, so status check may be simpler

**Java Reference:**
- `baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/utils/BlockStateInterface.java:115`

---

#### 6. Camera Entity Access
**Status:** ❌ MISSING

**Required Method:**
- `mc.getCameraEntity()` → `Entity`

**Usage in Baritone:**
- `BaritonePlayerContext.viewerPos()` - Camera position for rendering

**Implementation Complexity:** Low

**Implementation Notes:**
- Add to `IMinecraftClient` or `ClientState`
- Typically same as LocalPlayer, but can be different (spectator mode, etc.)
- Default to `LocalPlayer.Entity` if no camera entity

**Java Reference:**
- `baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/utils/player/BaritonePlayerContext.java:74`

---

#### 7. Thread Safety Checking
**Status:** ❌ MISSING

**Required Method:**
- `mc.isSameThread()` → `boolean`

**Usage in Baritone:**
- `BlockStateInterface` constructor - Ensures construction on main thread

**Implementation Complexity:** Low

**Implementation Notes:**
- Add to `IMinecraftClient`
- Check if current thread is main/game thread
- Can use `SynchronizationContext` or thread ID comparison

**Java Reference:**
- `baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/utils/BlockStateInterface.java:72-74`

---

### Priority 2 (Required for Pathfinding)

#### 8. BlockPos Class
**Status:** ⚠️ PARTIAL (Vector3<int> exists, but no dedicated class)

**Required Features:**
- Immutable block position
- Methods: `above()`, `below()`, `relative(Direction)`, `distSqr(BlockPos)`
- Better semantics than generic Vector3<int>

**Usage in Baritone:**
- Extensive use throughout pathfinding
- Block position arithmetic

**Implementation Complexity:** Medium

**Implementation Notes:**
- Create `BlockPos` struct or class
- Can wrap `Vector3<int>` or create new
- Provide immutable operations
- Consider if `Vector3<int>` is sufficient (works but less semantic)

**Priority Note:** Currently works with `Vector3<int>`, but dedicated class would be cleaner.

**Java Reference:**
- Used extensively - `baritone/api/utils/BetterBlockPos.java` extends BlockPos

---

#### 9. MutableBlockPos Class
**Status:** ❌ MISSING

**Required Features:**
- Mutable block position
- `set(int x, int y, int z)` method

**Usage in Baritone:**
- `BlockStateInterface.isPassableBlockPos` - Mutable position for iteration

**Implementation Complexity:** Low

**Implementation Notes:**
- Create `MutableBlockPos` class
- Alternative: Use mutable `Vector3<int>` directly (works but less semantic)

**Java Reference:**
- `baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/utils/BlockStateInterface.java:47`

---

#### 10. InteractionResult Enum
**Status:** ❌ MISSING

**Required Values:**
- `SUCCESS` - Interaction succeeded
- `CONSUME` - Interaction consumed item
- `PASS` - Interaction passed (not handled)
- `FAIL` - Interaction failed

**Usage in Baritone:**
- `IPlayerController.processRightClickBlock()` return type
- `IPlayerController.processRightClick()` return type

**Implementation Complexity:** Low

**Implementation Notes:**
- Create enum `InteractionResult`
- Update `IInteractionManager` methods to return `InteractionResult` instead of `bool`
- Currently boolean works, but enum more accurate

**Java Reference:**
- `baritone-1.21.11-REFERENCE-ONLY/src/api/java/baritone/api/utils/IPlayerController.java:50-52`

---

#### 11. Chunk.isEmpty() Method
**Status:** ❌ MISSING

**Required Method:**
- `isEmpty()` → `boolean`

**Usage in Baritone:**
- `BlockStateInterface` - Check if chunk has data
- Chunk loading validation

**Implementation Complexity:** Low

**Implementation Notes:**
- Add to `Chunk` class
- Check if all sections are empty or chunk has no data

**Java Reference:**
- `baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/utils/BlockStateInterface.java:116`

---

#### 12. ChunkPos Class
**Status:** ⚠️ PARTIAL (Properties exist, no class)

**Required Features:**
- Chunk coordinates (x, z)
- Methods for chunk position arithmetic

**Usage in Baritone:**
- Chunk coordinate representation
- Chunk iteration

**Implementation Complexity:** Low

**Implementation Notes:**
- Create `ChunkPos` struct/class
- Currently `Chunk.ChunkX`, `Chunk.ChunkZ` properties work
- Nice to have but not blocking

**Priority Note:** Works with properties, but class cleaner.

---

### Priority 3 (Advanced Features)

#### 13. Inventory Click Handling
**Status:** ❌ MISSING

**Required Method:**
- `handleInventoryMouseClick(int windowId, int slotId, int mouseButton, ClickType, Player)` → `void`

**Usage in Baritone:**
- `InventoryBehavior` - Inventory management
- Item movement/sorting

**Implementation Complexity:** Medium-High

**Implementation Notes:**
- Add to `IInteractionManager` or separate inventory manager
- Requires `ClickType` enum
- Window/slot management
- Packet handling (ClickContainerPacket)

**Java Reference:**
- `baritone-1.21.11-REFERENCE-ONLY/src/main/java/baritone/behavior/InventoryBehavior.java`

---

#### 14. ClickType Enum
**Status:** ❌ MISSING

**Required Values:**
- Inventory click types (PICKUP, QUICK_MOVE, SWAP, CLONE, THROW, QUICK_CRAFT, PICKUP_ALL)

**Usage in Baritone:**
- Inventory management

**Implementation Complexity:** Low

**Implementation Notes:**
- Create enum matching Minecraft click types

---

### Priority 4 (Nice to Have)

#### 15. Chat Components
**Status:** ❌ MISSING (Lower priority)

**Classes:**
- `Component`, `MutableComponent`, `ClickEvent`, `HoverEvent`, `ChatFormatting`

**Usage in Baritone:**
- Command output formatting
- Waypoint messages
- Chat integration

**Implementation Complexity:** Medium

**Priority Note:** Not required for core pathfinding, nice for user experience.

---

## Summary Statistics

### By Status

| Status | Count | Percentage |
|--------|-------|------------|
| ✅ EXISTS | 15 | ~30% |
| ⚠️ PARTIAL | 18 | ~36% |
| ❌ MISSING | 17 | ~34% |

### By Priority

| Priority | Missing Components | Critical Gaps |
|----------|-------------------|---------------|
| Priority 1 | 7 | DimensionType, WorldBorder, Block breaking state, Entity.blockPosition(), ChunkStatus, Camera entity, Thread safety |
| Priority 2 | 5 | BlockPos class, MutableBlockPos, InteractionResult, Chunk.isEmpty(), ChunkPos |
| Priority 3 | 2 | Inventory clicks, ClickType |
| Priority 4 | 3+ | Chat components, GUI, etc. |

---

## Implementation Recommendations

### Phase 1: Critical Gaps (Priority 1)
1. **DimensionType** - Add to Level/ClientState
2. **WorldBorder** - Create class, store in Level
3. **Entity.blockPosition()** - Add method to Entity
4. **Block breaking state** - Add to IInteractionManager
5. **ChunkStatus** - Create enum, add to ChunkManager
6. **Camera entity** - Add to IMinecraftClient/ClientState
7. **Thread safety** - Add to IMinecraftClient

### Phase 2: Pathfinding Requirements (Priority 2)
1. **BlockPos class** (or verify Vector3<int> is sufficient)
2. **MutableBlockPos** (or use mutable Vector3<int>)
3. **InteractionResult enum** - Update IInteractionManager
4. **Chunk.isEmpty()** - Add to Chunk
5. **ChunkPos class** (or verify properties sufficient)

### Phase 3: Advanced Features (Priority 3+)
1. Inventory management
2. Chat components (if needed)
3. Other nice-to-have features

---

*Generated as part of Baritone Vanilla Dependencies Audit Plan*

