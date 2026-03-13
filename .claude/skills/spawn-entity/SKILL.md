---
name: spawn-entity
description: Guide for correctly adding a new entity type (hero, unit, supply point, or custom) to the project. Covers the full pipeline from component design to visual instantiation. Use when adding a completely new kind of entity rather than adding components to an existing type.
disable-model-invocation: false
allowed-tools: Read, Grep, Glob, Write, Edit
---

# Spawn Entity — Add a New Entity Type to the Project

## Usage
```
/spawn-entity <EntityTypeName> [--type hero|unit|point|custom]
```

## Overview

The project uses a fully automated visual instantiation pipeline. **Never manually call `Instantiate()` inside an ECS system.**

| Entity type | Spawn System | Visual Registry |
|-------------|-------------|-----------------|
| Hero | `HeroSpawnSystem` | `VisualPrefabRegistry` |
| Unit | `SquadSpawningSystem` | `VisualPrefabRegistry` |
| Supply Point | `SupplyPointSetup` (MonoBehaviour) | direct instantiation |
| Custom | New `{Name}SpawnSystem` | `VisualPrefabRegistry` or direct |

Read `Docs/ConfiguracionPrefabs_ECS_Visual.md` before starting.

## Step 1: Reuse check

Read `CLAUDE.md` and search existing spawn systems:
```
Grep: Assets/Scripts/ "SpawnSystem"
Grep: Assets/Scripts/ "ISystem" --include="*.System.cs"
```

Is there an existing spawn system that can be extended, or do you need a new one?

## Step 2: Define the ECS data for the new entity

### 2a. Identify required components

For the new entity, list:
- **Identity/config data**: (e.g., `SquadData`, `HeroClassDefinition`)
- **State data**: (e.g., `SquadFSMState`, `HeroState`)
- **Physical data**: `LocalTransform`, `PhysicsCollider` (if needed)
- **Visual reference**: `VisualPrefabReference` or similar
- **Owner**: `SquadOwner`, `HeroOwner`, or custom ownership component

### 2b. Create components that don't exist yet

Run `/new-ecs-component` for each new component needed.

### 2c. Create an Authoring for prefab-baked data

Run `/new-authoring` for each component that needs editor configuration.

## Step 3: Create the Spawn System

Run `/new-ecs-system {EntityTypeName}SpawnSystem` and follow the pattern below:

### Spawn system pattern (canonical)

Reference: `Assets/Scripts/Hero/Systems/HeroSpawnSystem.cs` + `Assets/Scripts/Squads/Systems/SquadSpawningSystem.cs`

```csharp
// The spawn system should:
// 1. Query for a request component (tag or data) that signals "spawn this entity"
// 2. Instantiate the ECS entity from a prefab entity
// 3. Configure its components
// 4. Remove the request component (or destroy the request entity)
// 5. NOT directly instantiate GameObjects — that is the visual management system's job

[BurstCompile]
[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial struct {EntityType}SpawnSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<{SpawnRequest}>();
        state.RequireForUpdate<{ConfigOrDatabase}>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var ecb = SystemAPI.GetSingleton<EndInitializationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(state.WorldUnmanaged);

        foreach (var (request, entity) in 
            SystemAPI.Query<RefRO<{SpawnRequest}>>().WithEntityAccess())
        {
            // Instantiate from prefab entity
            var newEntity = ecb.Instantiate(request.ValueRO.PrefabEntity);
            
            // Configure initial components
            ecb.SetComponent(newEntity, new {StateComponent} { /* ... */ });
            
            // Remove the request
            ecb.DestroyEntity(entity);
        }
    }
}
```

## Step 4: Set up visual management

The visual GameObject is instantiated by a **Visual Management System**, not the spawn system.

### Visual Management System pattern

Reference: `Assets/Scripts/Hero/Systems/HeroVisualManagement.System.cs`  
Reference: `Assets/Scripts/Squads/Systems/SquadVisualManagementSystem.cs`

```csharp
// Visual Management System:
// 1. Detects when a new entity was spawned (no visual yet, query with WithNone<VisualInstanceRef>)
// 2. Looks up the prefab from VisualPrefabRegistry
// 3. Calls VisualPrefabRegistry to instantiate the GameObject
// 4. Adds the EntityVisualSync component to the GO
// 5. Stores the GO reference in the ECS entity via a VisualInstanceRef component
```

Read `Assets/Scripts/Hero/VisualPrefabRegistry.cs` for the API.

### Register the prefab in VisualPrefabRegistry

Open `Assets/Scripts/Hero/VisualPrefabConfiguration.cs` and add an entry for the new entity type.  
Then in the subscene, configure the `VisualPrefabRegistry` MonoBehaviour to include the new prefab.

## Step 5: Set up EntityVisualSync

Each visual GameObject needs an `EntityVisualSync` component that reads ECS state each frame.

1. Read `Assets/Scripts/Visual/EntityVisualSync.cs` to understand what fields are already synced.
2. If the new entity needs additional visual fields, add them to `EntityVisualSync` or create a new sync component.
3. The sync reads from ECS components every `Update()` — never write to ECS from here.

## Step 6: Register in the subscene

Open `DOTSWorld.unity` (subscene inside `BattleScene`) and:
1. Add an authoring GameObject with the `{EntityType}Authoring` components.
2. Configure the prefab references in `VisualPrefabRegistry`.
3. Add any config singletons (spawn config, balance config) as separate GameObjects.

## Step 7: Test in Play Mode

1. Enter Play Mode in Unity.
2. Trigger the spawn (via input, event, or direct component add via Entities window).
3. In the **Entities** window (Window > Entities > Hierarchy), confirm the entity appears with all expected components.
4. Confirm the visual GameObject appears in the scene.
5. Run `/analyze-system {EntityType}SpawnSystem` to verify correctness.

## Reference files

| File | Purpose |
|------|---------|
| `Assets/Scripts/Hero/Systems/HeroSpawnSystem.cs` | Hero spawn pattern |
| `Assets/Scripts/Squads/Systems/SquadSpawningSystem.cs` | Unit spawn pattern |
| `Assets/Scripts/Hero/Systems/HeroVisualManagement.System.cs` | Visual management pattern |
| `Assets/Scripts/Hero/VisualPrefabRegistry.cs` | Prefab registry API |
| `Assets/Scripts/Hero/VisualPrefabConfiguration.cs` | Prefab config asset |
| `Docs/ConfiguracionPrefabs_ECS_Visual.md` | Visual prefab setup guide |
| `Docs/ModeloHybrido.md` | Hybrid ECS-GO architecture |
| `Docs/Guides/AgregarNuevaUnidad_Guia.md` | Guide for adding a new unit type |
| `Docs/Guides/Spawn_Heroe_Guia.md` | Guide for spawning a hero |
