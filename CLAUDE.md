# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**Conquest Tactics** is a squad-based tactical multiplayer game built in **Unity 2022.3.x** with **DOTS/ECS (Entities 1.3.14)**. Players control a hero commander who leads squads of soldiers in 3v3 PvP battles (~5-10 minutes).

## Build & Development

- **Engine**: Unity 2022.3.x — open and run via Unity Hub. No CLI build scripts exist.
- **IDE**: Visual Studio (solution: `prototipo_curado.sln`). Assemblies: `Assembly-CSharp`, `Game.ECS`, `Unity.Physics.Custom`.
- **Testing**: Unity Test Framework (`com.unity.test-framework`) — run via Unity's Test Runner window.
- **Docs**: `Docs/` contains architecture guides and creation guides (mostly in Spanish). Key files:
  - `Docs/ModeloHybrido.md` — canonical reference for the ECS-GameObject hybrid architecture
  - `Docs/GDD.md` — Game Design Document
  - `Docs/ScriptableObjects_Architecture.md` — data architecture patterns
  - `Assets/Scripts/Squads/SystemResponsibilities.md` — system responsibilities and data flow

## Architecture: Hybrid ECS-GameObject Model

The central architectural concept. **Never mix responsibilities between layers.**

| Layer | Responsibility | Key Types |
|-------|---------------|-----------|
| **ECS World** | Game logic, state, calculations | Entities, Components, Systems |
| **Sync Layer** | Frame-by-frame bridging | `EntityVisualSync` (MonoBehaviour) |
| **GameObject World** | Visualization only | Synty prefabs, animations, rendering |

ECS state is never modified from the GameObject side — only the sync layer reads ECS data to update GameObjects.

### ECS Systems Data Flow

```
Input → SquadControlSystem → SquadOrderSystem → SquadFSMSystem
                                                      ↓
         UnitFormationStateSystem ← FormationSystem ──┘
                  ↓
         UnitFollowFormationSystem (movement only)
                  ↓
         SquadVisualManagementSystem → EntityVisualSync (per-unit)
```

Hero flow: `HeroInputSystem → HeroMovementSystem → HeroStateSystem → HeroVisualManagementSystem → EntityVisualSync`

### System Responsibility Rules

Each ECS system has one responsibility — do not add concerns outside its scope:
- **SquadControlSystem**: captures raw input only
- **SquadOrderSystem**: converts input to squad state changes only
- **SquadFSMSystem**: handles squad-level state transitions only
- **UnitFormationStateSystem**: owns all unit state transitions (Moving/Formed/Waiting)
- **UnitFollowFormationSystem**: moves units physically; never changes their state
- **FormationSystem**: calculates formation slot positions only
- **DestinationMarkerSystem**: reads state, never writes it

### Core Services (Assets/Scripts/Core/)

Centralized services — prefer these over direct data access:
- `HeroDataService.cs` — hero creation and retrieval
- `SquadDataService.cs` — squad creation and management
- `DataCacheService.cs` — caches calculated hero attributes
- `PlayerSessionService.cs` — current player/session state
- `ItemService.cs` — item queries and management
- `MapService.cs` — map data access

Inventory services live in `Assets/Scripts/Inventory/Services/`.

### Shared Utilities

Reuse these utilities across systems to avoid duplication:
- `HeroPositionUtility` — hero position retrieval (used by 6+ systems)
- `UnitStatsUtility` — stat application for progression systems
- `FormationPositionCalculator` — formation math

## Scene Flow

Scenes (defined in `ProjectSettings/EditorBuildSettings.asset`):
1. `AvatarCreator` → `LoginScene` → `CharacterselecctionScene`
2. `BarraconScene` (squad management) → `FeudoScene` (hub)
3. `BattlePrepScene` → `BattleScene` (contains sub-scene `DOTSWorld.unity`) → `PostBattleScene`

`BattleScene/DOTSWorld.unity` is a separate subscene that hosts the ECS World.

## Key Directory Structure

```
Assets/Scripts/
├── Core/           - Services and persistence (HeroDataService, SquadDataService, etc.)
├── Hero/           - 24 ECS components + 12 ECS systems for the hero entity
├── Squads/         - Squad and unit ECS components + 21+ ECS systems
├── Inventory/      - Item system with ScriptableObject-based architecture
├── UI/             - UI controllers per scene (BattlePreparation/, Battle/, HeroDetail/, etc.)
├── Data/           - Data structures (Items, Battle, Maps, Attributes)
├── Events/         - Event system
└── Services/       - Currency, pricing, matchmaking services
```

## Data: ScriptableObjects

Items, effects, and game content use a ScriptableObject-based architecture (see `Docs/ScriptableObjects_Architecture.md`). When creating new items or equipment, follow the creation guides in `Docs/Guides/`.

## UI Patterns

UI controllers follow a Singleton pattern with `OpenPanel()` / `ClosePanel()` / `TogglePanel()` and `PopulateUI()` methods. Scene-level UI controllers live under `Assets/Scripts/UI/<SceneName>/`.

## Key Packages

- `com.unity.entities` 1.3.14 + `com.unity.burst` + `com.unity.collections` + `com.unity.mathematics` — ECS/DOTS stack
- `com.unity.physics` 1.3.14 — physics for ECS
- `com.unity.netcode.gameobjects` 1.5.1 — multiplayer
- `com.unity.render-pipelines.universal` 14.0.12 — URP rendering
- `com.unity.inputsystem` 1.14.0 — modern input
