# CLAUDE.md

## Workflow

- Plan mode for any non-trivial task (3+ steps). Write plan to `tasks/todo.md`.
- After corrections: update `tasks/lessons.md` with the pattern to avoid repeating it.
- Use subagents for research/exploration to keep main context clean. Haiku for simple tasks.
- Simplicity first. Minimal code impact. Find root causes — no temp fixes.

## Project

**Conquest Tactics** — Unity 2022.3.x + DOTS/ECS (Entities 1.3.14). Squad-based 3v3 PvP tactical game.
Docs: `Docs/ModeloHybrido.md` (architecture), `Docs/GDD.md`, `Docs/TDD.md`, `Assets/Scripts/Squads/SystemResponsibilities.md`.

## Architecture: Hybrid ECS-GameObject Model

**Never mix responsibilities between layers.**

| Layer | Responsibility | Key Types |
|-------|---------------|-----------|
| **ECS World** | Game logic, state, calculations | Entities, Components, Systems |
| **Sync Layer** | Frame-by-frame bridging | `EntityVisualSync` (MonoBehaviour) |
| **GameObject World** | Visualization only | Synty prefabs, animations, rendering |

ECS state is never modified from the GameObject side.

### Data Flow

```
Input → SquadControlSystem → SquadOrderSystem → SquadFSMSystem
                                                      ↓
         UnitFormationStateSystem ← FormationSystem ──┘
                  ↓
         UnitFollowFormationSystem (movement only)
                  ↓
         SquadVisualManagementSystem → EntityVisualSync (per-unit)
```

Hero: `HeroInputSystem → HeroMoveIntent → HeroMovementSystem → HeroStateSystem → HeroVisualManagementSystem → EntityVisualSync`

Combat: `EnemyDetection → DamageCalculation → UnitDeath` (pre-AI, before SquadAISystem)
`→ UnitTargeting → CombatReaction → OrderResolution` (post-AI arbitration)
`→ BraceWeaponActivation → BraceWeapon → UnitAttack → BlockRegen` (execution)

### System Responsibility Rules

Each system has ONE responsibility:
- **SquadControlSystem**: raw input only
- **SquadOrderSystem**: input → squad state changes only
- **SquadFSMSystem**: squad-level state transitions only
- **UnitFormationStateSystem**: owns all unit state transitions (Moving/Formed/Waiting)
- **UnitFollowFormationSystem**: moves units physically; never changes state
- **FormationSystem**: formation slot positions only
- **DestinationMarkerSystem**: reads state, never writes it

### Visual Instantiation (Automated)

- **Hero**: `HeroSpawnSystem` → `HeroVisualManagementSystem` → `VisualPrefabRegistry` → instantiate + `EntityVisualSync`
- **Units**: `SquadSpawningSystem` → `SquadVisualManagementSystem` → `VisualPrefabRegistry` → instantiate + `EntityVisualSync`
- **Supply Points**: `SupplyPointSetup` (MonoBehaviour) creates ECS entity → `SupplyPointZoneIndicator`

`VisualPrefabRegistry` (`Assets/Scripts/Hero/VisualPrefabRegistry.cs`) — singleton for prefab lookup/caching.

### Core Services (`Assets/Scripts/Core/`)

Prefer these over direct data access: `HeroDataService`, `SquadDataService`, `DataCacheService`, `PlayerSessionService`, `ItemService`, `MapService`. Inventory services: `Assets/Scripts/Inventory/Services/`.

### Shared Utilities

- `HeroPositionUtility` — hero position (used by 6+ systems)
- `FormationPositionCalculator` — formation math + `calculateTerraindHeight`
- `GameTags` (`Assets/Scripts/Shared/GameTags.cs`) — tag constants; avoid magic strings
- `VisualSyncUtility` (`Assets/Scripts/Shared/VisualSyncUtility.cs`) — configures `AnimatorCullingMode`, gets/adds `EntityVisualSync`
- `UnitStatsUtility` — stat application for progression systems

## graphify

Knowledge graph at `graphify-out/`.
- Before answering architecture questions, read `graphify-out/GRAPH_REPORT.md` (god nodes, communities, surprising connections)
- Open `graphify-out/obsidian/` as an Obsidian vault for interactive navigation (3,551 notes, community overviews, graph.canvas)
- Interactive HTML graph: `graphify-out/graph.html` (open in browser)
- After modifying code: run `/graphify --update` to incrementally rebuild the graph
