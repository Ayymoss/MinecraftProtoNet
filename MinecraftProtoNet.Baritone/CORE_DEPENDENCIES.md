# Core Baritone Dependencies Analysis

This document identifies the **minimum viable set** required for basic Baritone functionality, focusing on the three core interfaces: `IPlayerContext`, `BlockStateInterface`, and `IPlayerController`.

---

## 1. IPlayerContext Interface Requirements

**Java Interface:** `baritone/api/utils/IPlayerContext.java`

### Core Methods

#### `Minecraft minecraft()`
**C# Equivalent:** `IMinecraftClient`

**Status:** ⚠️ PARTIAL

**Required Access:**
- `mc.player` → `State.LocalPlayer.Entity` ✅
- `mc.level` → `State.Level` ✅
- `mc.gameMode` → `State.LocalPlayer.GameMode` ✅
- `mc.getCameraEntity()` → ❌ MISSING
- `mc.isSameThread()` → ❌ MISSING

**Gaps:**
- Camera entity access
- Thread safety checking

**Priority:** Priority 1

---

#### `LocalPlayer player()`
**C# Equivalent:** `State.LocalPlayer.Entity`

**Status:** ⚠️ PARTIAL

**Required Methods/Properties:**
- `player().position()` → `Entity.Position` (Vector3<double>) ✅
- `player().getYRot()` → `Entity.YawPitch.X` (float) ✅
- `player().getXRot()` → `Entity.YawPitch.Y` (float) ✅
- `player().getDeltaMovement()` → `Entity.Velocity` (Vector3<double>) ✅
- `player().getEyeHeight()` → `Entity.EyePosition` (Vector3<double>) ✅ (property exists, method not needed)
- `player().blockPosition()` → ❌ MISSING (need method)

**Gaps:**
- `blockPosition()` method

**Priority:** Priority 1

---

#### `Level world()`
**C# Equivalent:** `Level`

**Status:** ⚠️ PARTIAL

**Required Methods:**
- `world().dimensionType().minY()` → ❌ MISSING
- `world().dimensionType().height()` → ❌ MISSING
- `world().getWorldBorder()` → ❌ MISSING
- `world().getBlockState(BlockPos)` → `Level.GetBlockAt(int, int, int)` ✅ (different signature)
- `world().entitiesForRendering()` → ⚠️ PARTIAL (use `Level.GetAllEntityIds()` + `GetEntityOfId()`)

**Gaps:**
- DimensionType (minY, height)
- WorldBorder
- Direct entity iteration method (currently need to combine methods)

**Priority:** Priority 1

---

#### `IPlayerController playerController()`
**C# Equivalent:** `IInteractionManager`

**Status:** ⚠️ PARTIAL (see IPlayerController section below)

**Gaps:** See IPlayerController requirements

**Priority:** Priority 1

---

#### `IWorldData worldData()`
**C# Equivalent:** World cache system (to be implemented)

**Status:** ❌ MISSING (Baritone-specific, not vanilla)

**Note:** This is Baritone's world cache system, not a vanilla dependency. Will be implemented as part of Baritone integration.

**Priority:** N/A (Baritone implementation, not vanilla dependency)

---

#### `HitResult objectMouseOver()`
**C# Equivalent:** `RaycastHit`

**Status:** ✅ EXISTS

**C# Location:** `MinecraftProtoNet.Core/Models/World/Meta/RaycastHit.cs`

**Method Mapping:**
- `getBlockPos()` → `BlockPosition` (Vector3<int>) ✅
- `getDirection()` → `Face` (BlockFace) ✅
- Hit information ✅

**Gaps:** None

**Priority:** ✅ Complete

---

### IPlayerContext Default Methods

#### `BetterBlockPos playerFeet()`
**Requirements:**
- `player().position()` → ✅ EXISTS
- `world().getBlockState(BlockPos)` → ✅ EXISTS (different signature)
- `blockState.getBlock() instanceof SlabBlock` → ✅ EXISTS (`BlockState.IsSlab`)
- `blockPos.above()` → ⚠️ PARTIAL (need BlockPos helper or use Vector3)

**Status:** ⚠️ PARTIAL

**Gaps:**
- BlockPos immutable operations (above(), below(), etc.)

**Priority:** Priority 2

---

#### `Vec3 playerFeetAsVec()`
**Requirements:**
- `player().position()` → ✅ EXISTS

**Status:** ✅ EXISTS

**Priority:** ✅ Complete

---

#### `Vec3 playerHead()`
**Requirements:**
- `player().position()` → ✅ EXISTS
- `player().getEyeHeight()` → ✅ EXISTS (EyePosition property)

**Status:** ✅ EXISTS

**Priority:** ✅ Complete

---

#### `Vec3 playerMotion()`
**Requirements:**
- `player().getDeltaMovement()` → ✅ EXISTS (Velocity property)

**Status:** ✅ EXISTS

**Priority:** ✅ Complete

---

#### `Rotation playerRotations()`
**Requirements:**
- `player().getYRot()` → ✅ EXISTS
- `player().getXRot()` → ✅ EXISTS

**Status:** ✅ EXISTS

**Priority:** ✅ Complete

---

#### `Optional<BlockPos> getSelectedBlock()`
**Requirements:**
- `objectMouseOver()` → ✅ EXISTS
- `HitResult.getType() == HitResult.Type.BLOCK` → ⚠️ PARTIAL (check Block property)
- `BlockHitResult.getBlockPos()` → ✅ EXISTS (BlockPosition property)

**Status:** ⚠️ PARTIAL

**Gaps:**
- HitResult.Type enum (currently check Block property null)

**Priority:** Priority 2

---

## 2. BlockStateInterface Requirements

**Java Class:** `baritone/utils/BlockStateInterface.java`

### Constructor Requirements

#### `BlockStateInterface(IPlayerContext ctx)`
**Required:**
- `ctx.world()` → ✅ EXISTS
- `ctx.world().dimensionType().minY()` → ❌ MISSING
- `ctx.world().dimensionType().height()` → ❌ MISSING
- `ctx.world().getWorldBorder()` → ❌ MISSING
- `ctx.worldData()` → Baritone-specific (not vanilla)
- `ctx.minecraft().isSameThread()` → ❌ MISSING
- `(ClientChunkCache) ctx.world().getChunkSource()` → ⚠️ PARTIAL (ChunkManager exists, no ClientChunkCache abstraction)

**Gaps:**
- DimensionType (minY, height)
- WorldBorder
- Thread safety check
- ClientChunkCache abstraction

**Priority:** Priority 1

---

### Core Methods

#### `BlockState get0(int x, int y, int z)`
**Required Operations:**

1. **Bounds Checking:**
   ```java
   y -= world.dimensionType().minY();
   if (y < 0 || y >= world.dimensionType().height()) return AIR;
   ```
   - `world.dimensionType().minY()` → ❌ MISSING
   - `world.dimensionType().height()` → ❌ MISSING

2. **Chunk Access:**
   ```java
   LevelChunk chunk = provider.getChunk(x >> 4, z >> 4, ChunkStatus.FULL, false);
   ```
   - `provider.getChunk(int, int, ChunkStatus, bool)` → ⚠️ PARTIAL (`ChunkManager.GetChunk(int, int)` exists, missing ChunkStatus)
   - `chunk.isEmpty()` → ❌ MISSING
   - `chunk.getPos().x` → ⚠️ PARTIAL (Chunk.ChunkX exists, no ChunkPos object)

3. **Block State Access:**
   ```java
   BlockState type = cached.getBlock(x & 511, y + world.dimensionType().minY(), z & 511);
   ```
   - Block state access → ✅ EXISTS (via CachedRegion - Baritone-specific)
   - `world.dimensionType().minY()` → ❌ MISSING

**Gaps:**
- DimensionType (minY, height) - **CRITICAL**
- ChunkStatus enum - **CRITICAL**
- Chunk.isEmpty() - **IMPORTANT**
- ClientChunkCache abstraction - **IMPORTANT**

**Priority:** Priority 1

---

#### `boolean isLoaded(int x, int z)`
**Required:**
- `provider.getChunk(int, int, ChunkStatus, bool)` → ⚠️ PARTIAL
- `chunk.isEmpty()` → ❌ MISSING

**Gaps:**
- ChunkStatus parameter
- Chunk.isEmpty()

**Priority:** Priority 1

---

#### `BlockState getFromChunk(LevelChunk chunk, int x, int y, int z)`
**Required:**
- `chunk.getSection(int y)` → ❌ MISSING (ChunkSection access)
- Section block state access

**Gaps:**
- Chunk section access methods

**Priority:** Priority 2

---

## 3. IPlayerController Requirements

**Java Interface:** `baritone/api/utils/IPlayerController.java`

### Core Methods

#### `void syncHeldItem()`
**C# Equivalent:** Need to verify

**Status:** ❌ MISSING

**Usage:** Synchronize held item with server

**Priority:** Priority 2

---

#### `boolean hasBrokenBlock()`
**C# Equivalent:** Block breaking state check

**Status:** ❌ MISSING

**Required:**
- `gameMode.isHittingBlock()` → Block breaking state
- Track breaking state

**Gaps:**
- Block breaking state management

**Priority:** Priority 1

---

#### `boolean onPlayerDamageBlock(BlockPos pos, Direction side)`
**C# Equivalent:** Continue block breaking

**Status:** ❌ MISSING

**Required:**
- `gameMode.continueDestroyBlock(BlockPos, Direction)` → Continue breaking

**Gaps:**
- Block breaking continuation

**Priority:** Priority 1

---

#### `void resetBlockRemoving()`
**C# Equivalent:** Stop block breaking

**Status:** ❌ MISSING

**Required:**
- `gameMode.stopDestroyBlock()` → Stop breaking

**Gaps:**
- Block breaking cancellation

**Priority:** Priority 1

---

#### `void windowClick(int windowId, int slotId, int mouseButton, ClickType type, Player player)`
**C# Equivalent:** Inventory click handling

**Status:** ❌ MISSING

**Required:**
- `gameMode.handleInventoryMouseClick(...)` → Inventory management
- `ClickType` enum

**Gaps:**
- Inventory click handling
- ClickType enum

**Priority:** Priority 3

---

#### `GameType getGameType()`
**C# Equivalent:** Game mode access

**Status:** ✅ EXISTS

**C# Location:** `Player.GameMode` → `GameMode` enum

**Gaps:** None

**Priority:** ✅ Complete

---

#### `InteractionResult processRightClickBlock(LocalPlayer player, Level world, InteractionHand hand, BlockHitResult result)`
**C# Equivalent:** Place block

**Status:** ⚠️ PARTIAL

**C# Location:** `IInteractionManager.PlaceBlockAsync(Hand)` → `Task<bool>`

**Gaps:**
- Returns `bool` instead of `InteractionResult` enum
- Takes `Hand` instead of full `BlockHitResult` (simplified interface)

**Priority:** Priority 2 (Works but enum more accurate)

---

#### `InteractionResult processRightClick(LocalPlayer player, Level world, InteractionHand hand)`
**C# Equivalent:** Interact with entity/block

**Status:** ⚠️ PARTIAL

**C# Location:** `IInteractionManager.InteractAsync(Hand)` → `Task<bool>`

**Gaps:**
- Returns `bool` instead of `InteractionResult` enum

**Priority:** Priority 2 (Works but enum more accurate)

---

#### `boolean clickBlock(BlockPos loc, Direction face)`
**C# Equivalent:** Start block breaking

**Status:** ⚠️ PARTIAL

**C# Location:** `IInteractionManager.DigBlockAsync()` → `Task<bool>`

**Gaps:**
- Async method (Baritone uses sync)
- No BlockPos/Direction parameters (uses raycast instead)

**Priority:** Priority 2 (Works but different interface)

---

#### `void setHittingBlock(boolean hittingBlock)`
**C# Equivalent:** Set block breaking state

**Status:** ❌ MISSING

**Required:**
- Block breaking state management

**Gaps:**
- Block breaking state setter

**Priority:** Priority 1

---

#### `double getBlockReachDistance()`
**Status:** ✅ EXISTS

**C# Location:** `IInteractionManager.ReachDistance` → `double`

**Gaps:** None

**Priority:** ✅ Complete

---

## Summary: Minimum Viable Set

### Critical Gaps (Must Implement for Basic Baritone)

1. **DimensionType** (minY, height) - Priority 1
2. **WorldBorder** - Priority 1
3. **Block breaking state management** (start/continue/stop) - Priority 1
4. **Entity.blockPosition()** method - Priority 1
5. **ChunkStatus enum** - Priority 1
6. **Camera entity access** - Priority 1
7. **Thread safety checking** - Priority 1
8. **ClientChunkCache abstraction** (or verify ChunkManager sufficient) - Priority 1

### Important Gaps (Required for Full Functionality)

9. **BlockPos class** (or verify Vector3<int> sufficient) - Priority 2
10. **MutableBlockPos** (or use mutable Vector3<int>) - Priority 2
11. **InteractionResult enum** - Priority 2
12. **Chunk.isEmpty()** - Priority 2
13. **Chunk section access** - Priority 2
14. **Entity iteration method** - Priority 2

### Nice to Have (Can Work Around)

15. **Inventory click handling** - Priority 3
16. **Chat components** - Priority 4

---

## Implementation Checklist

Before implementing Baritone, ensure:

- [ ] DimensionType with minY() and height() accessible from Level
- [ ] WorldBorder class with bounds checking
- [ ] Block breaking state tracking (start/continue/stop)
- [ ] Entity.blockPosition() method
- [ ] ChunkStatus enum (at minimum FULL status)
- [ ] Camera entity access
- [ ] Thread safety checking method
- [ ] BlockPos class OR verify Vector3<int> sufficient
- [ ] InteractionResult enum OR verify boolean sufficient
- [ ] Chunk.isEmpty() method
- [ ] IPlayerContext can be fully instantiated
- [ ] BlockStateInterface can access all blocks
- [ ] IPlayerController can interact with world

---

*Generated as part of Baritone Vanilla Dependencies Audit Plan*

