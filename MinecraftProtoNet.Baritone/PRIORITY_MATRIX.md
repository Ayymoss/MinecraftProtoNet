# Implementation Priority Matrix

This document prioritizes all Baritone vanilla dependencies by implementation priority, from blocking core functionality to nice-to-have features.

---

## Priority 1: Critical - Blocking Core Functionality

**Status:** Must implement before basic Baritone functionality can work.

**Impact:** Blocks `IPlayerContext`, `BlockStateInterface`, or `IPlayerController` from functioning.

| Component | Status | Complexity | Java Reference | Notes |
|-----------|--------|------------|----------------|-------|
| **DimensionType** (minY, height) | ✅ IMPLEMENTED | Low | `BlockStateInterface.java:98-100` | Created `DimensionType` class, added to `Level` |
| **WorldBorder** | ✅ IMPLEMENTED | Low-Medium | `BetterWorldBorder.java`, `AStarPathFinder.java:66` | Created `WorldBorder` class, added to `Level` |
| **Block breaking state** (continue/stop) | ✅ IMPLEMENTED | Medium | `BaritonePlayerController.java:54-67` | Added `StartDestroyBlockAsync`, `ContinueDestroyBlockAsync`, `ResetBlockRemovingAsync`, `HasBrokenBlock` to `IInteractionManager` |
| **Entity.blockPosition()** | ✅ IMPLEMENTED | Low | `BaritonePlayerContext.java:74`, `MineProcess.java:351` | Added `BlockPosition()` method to `Entity` class |
| **ChunkStatus enum** | ✅ IMPLEMENTED | Low | `BlockStateInterface.java:115` | Created `ChunkStatus` enum with `Full` value, added optional parameter to `GetChunk` |
| **Camera entity access** | ✅ IMPLEMENTED | Low | `BaritonePlayerContext.java:74` | Added `GetCameraEntity()` method to `ClientState` |
| **Thread safety checking** | ✅ IMPLEMENTED | Low | `BlockStateInterface.java:72-74` | Added `IsSameThread()` method to `IMinecraftClient` |
| **ClientChunkCache abstraction** | ✅ VERIFIED | Medium | `BlockStateInterface.java:69` | Verified `IChunkManager` is sufficient - Baritone uses `Level.getChunk()` directly |

**Total:** 8 components

**Estimated Implementation Time:** 2-3 days for all Priority 1 components

---

## Priority 2: Required for Pathfinding

**Status:** Required for full pathfinding functionality but not blocking basic setup.

**Impact:** Limits pathfinding capabilities or requires workarounds.

| Component | Status | Complexity | Java Reference | Notes |
|-----------|--------|------------|----------------|-------|
| **BlockPos class** | ✅ IMPLEMENTED | Medium | `BetterBlockPos.java` (extends BlockPos) | Vector3<int> with extension methods provides equivalent functionality |
| **MutableBlockPos class** | ✅ IMPLEMENTED | Low | `BlockStateInterface.java:47` | Created `MutableBlockPos` class with mutable operations |
| **InteractionResult enum** | ✅ IMPLEMENTED | Low | `IPlayerController.java:50-52` | Created `InteractionResult` enum with Success, Consume, Pass, Fail |
| **Chunk.isEmpty()** | ✅ IMPLEMENTED | Low | `BlockStateInterface.java:116` | Added `IsEmpty()` method to `Chunk` class |
| **Chunk section access** | ✅ IMPLEMENTED | Medium | `BlockStateInterface.java:154+` | Added `GetSection(int sectionY)` method to `Chunk` class |
| **Entity iteration method** | ✅ IMPLEMENTED | Low | `IPlayerContext.java:50-51` | Added `GetAllEntities()` method to `Level` class |
| **BlockPos immutable ops** (above/below/relative) | ✅ IMPLEMENTED | Low-Medium | Used extensively | Created `Vector3IntExtensions` with Above, Below, Relative, DistSqr methods |
| **HitResult.Type enum** | ✅ IMPLEMENTED | Low | `IPlayerContext.java:120-122` | Created `HitResultType` enum with Miss, Block, Entity |

**Total:** 8 components

**Estimated Implementation Time:** 2-3 days for all Priority 2 components

---

## Priority 3: Required for Advanced Features

**Status:** Required for advanced Baritone features (inventory management, item pickup, etc.)

**Impact:** Limits advanced functionality but core pathfinding works.

| Component | Status | Complexity | Java Reference | Notes |
|-----------|--------|------------|----------------|-------|
| **Inventory click handling** | ✅ IMPLEMENTED | Medium-High | `InventoryBehavior.java` | Added `WindowClickAsync` method to `IInteractionManager` |
| **ClickType enum** | ✅ IMPLEMENTED | Low | `IPlayerController.java:46` | Created `ClickType` enum matching Java Minecraft ClickType |
| **ChunkPos class** | ✅ IMPLEMENTED | Low | Used extensively | Created `ChunkPos` struct with coordinate methods |
| **Block constants** (Blocks.AIR) | ✅ IMPLEMENTED | Low | `BlockStateInterface.java:56` | Created `Blocks` static class with common block constants |
| **BlockGetter interface** | ✅ VERIFIED | N/A | `BlockStateInterfaceAccessWrapper.java` | `IChunkManager` provides equivalent functionality via `GetBlockAt` |

**Total:** 5 components

**Estimated Implementation Time:** 1-2 days for all Priority 3 components

---

## Priority 4: Nice to Have

**Status:** Not required for core functionality but improves user experience.

**Impact:** Limited impact on functionality.

| Component | Status | Complexity | Java Reference | Notes |
|-----------|--------|------------|----------------|-------|
| **Chat components** (Component, MutableComponent, etc.) | ❌ MISSING | Medium | `WaypointBehavior.java`, `ExampleBaritoneControl.java` | Required for command output formatting |
| **Chat formatting** (ChatFormatting) | ❌ MISSING | Low | `WaypointBehavior.java` | Text formatting codes |
| **GUI components** | ❌ MISSING | High | Launch/mixin only | Not required for core pathfinding |
| **ResourceKey<T>** | ❌ MISSING | Medium | Used in cache | Lower priority, string-based works |
| **ClipContext** | ❌ MISSING | Low | `GuiClick.java` | Raycast context configuration |

**Total:** 5+ components

**Estimated Implementation Time:** Variable, lower priority

---

## Implementation Order Recommendation

### Phase 1: Critical Foundation (Week 1)
**Focus:** Priority 1 components only

1. DimensionType (minY, height) - **1 day**
2. WorldBorder - **0.5 days**
3. Entity.blockPosition() - **0.5 days**
4. ChunkStatus enum - **0.5 days**
5. Camera entity access - **0.5 days**
6. Thread safety checking - **0.5 days**
7. Block breaking state management - **1 day**
8. ClientChunkCache abstraction (verify/implement) - **1 day**

**Total:** ~5-6 days

---

### Phase 2: Pathfinding Requirements (Week 2)
**Focus:** Priority 2 components

1. BlockPos class (or verify Vector3<int> sufficient) - **1 day**
2. MutableBlockPos (or use mutable Vector3<int>) - **0.5 days**
3. InteractionResult enum - **0.5 days**
4. Chunk.isEmpty() - **0.5 days**
5. Chunk section access - **1 day**
6. Entity iteration method - **0.5 days**
7. BlockPos helper methods (if using Vector3<int>) - **0.5 days**
8. HitResult.Type enum (or verify current approach) - **0.5 days**

**Total:** ~5 days

---

### Phase 3: Advanced Features (Week 3)
**Focus:** Priority 3 components (if needed)

1. Inventory click handling - **2 days**
2. ClickType enum - **0.5 days**
3. ChunkPos class (optional) - **0.5 days**
4. Block constants (optional) - **0.5 days**

**Total:** ~3-4 days

---

### Phase 4: Nice to Have (As Needed)
**Focus:** Priority 4 components

- Chat components (when command output needed)
- Other nice-to-have features

---

## Dependency Graph

```
Priority 1 (Critical)
├── DimensionType
│   └── Required by: BlockStateInterface bounds checking
├── WorldBorder
│   └── Required by: Pathfinding bounds validation
├── Block breaking state
│   └── Required by: IPlayerController
├── Entity.blockPosition()
│   └── Required by: IPlayerContext, entity positioning
├── ChunkStatus
│   └── Required by: BlockStateInterface chunk access
├── Camera entity
│   └── Required by: IPlayerContext viewer position
├── Thread safety
│   └── Required by: BlockStateInterface construction
└── ClientChunkCache abstraction
    └── Required by: BlockStateInterface chunk access

Priority 2 (Pathfinding)
├── BlockPos class (depends on Priority 1)
├── InteractionResult enum (depends on IPlayerController)
├── Chunk.isEmpty() (depends on Chunk access)
└── Chunk section access (depends on Chunk access)

Priority 3 (Advanced)
└── Inventory clicks (depends on Priority 2 completion)

Priority 4 (Nice to Have)
└── Independent features
```

---

## Blocking Dependencies

**Cannot start Baritone implementation without:**
- All Priority 1 components

**Cannot fully test pathfinding without:**
- All Priority 1 + Priority 2 components

**Cannot use advanced features without:**
- All Priority 1 + Priority 2 + Priority 3 components

---

## Verification Points

### After Priority 1 Completion
- [ ] IPlayerContext can be fully instantiated
- [ ] BlockStateInterface can be constructed
- [ ] BlockStateInterface can access blocks with bounds checking
- [ ] IPlayerController can start/continue/stop block breaking
- [ ] Basic pathfinding setup works

### After Priority 2 Completion
- [ ] Full BlockStateInterface functionality
- [ ] Complete IPlayerController functionality
- [ ] Pathfinding can execute without workarounds
- [ ] All core Baritone processes can run

### After Priority 3 Completion
- [ ] Inventory management works
- [ ] Advanced Baritone features available
- [ ] Full feature parity with Java Baritone

---

*Generated as part of Baritone Vanilla Dependencies Audit Plan*

