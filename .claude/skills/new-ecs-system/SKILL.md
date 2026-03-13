---
name: new-ecs-system
description: Scaffold a new ECS System following Conquest project conventions. Auto-invoked when the user asks to create a new ECS system. Generates the system skeleton with correct naming, [BurstCompile], [UpdateInGroup], and ISystem pattern.
disable-model-invocation: false
allowed-tools: Read, Grep, Glob, Write, Edit
---

# New ECS System — Scaffold an ECS System

## Usage
```
/new-ecs-system <SystemName> [--group <SimulationSystemGroup|PresentationSystemGroup|...>] [--after <OtherSystem>] [--squad|--hero]
```

## Step 1: Reuse check FIRST

Before creating, invoke `/reuse-check` or manually search:
```
Glob: Assets/Scripts/**/*.System.cs
```
Search for systems with overlapping responsibility. If one exists, extend it rather than creating a new one.

Also read `Assets/Scripts/Squads/SystemResponsibilities.md` to see if the responsibility already belongs to an existing system.

## Step 2: Determine the correct location

| Domain | Location |
|--------|----------|
| Hero-related | `Assets/Scripts/Hero/Systems/` |
| Squad/Unit-related | `Assets/Scripts/Squads/Systems/` |
| Combat | `Assets/Scripts/Combat/Systems/` (if exists) |
| Shared/Cross-domain | `Assets/Scripts/Shared/` |

## Step 3: Determine the correct UpdateInGroup

Reference the data flow from `CLAUDE.md`:
```
Input → SquadControlSystem → SquadOrderSystem → SquadFSMSystem
                                                      ↓
         UnitFormationStateSystem ← FormationSystem ──┘
                  ↓
         UnitFollowFormationSystem (movement only)
                  ↓
         SquadVisualManagementSystem → EntityVisualSync (per-unit)
```

Common groups:
- `SimulationSystemGroup` — game logic (most systems go here)
- `PresentationSystemGroup` — visual/rendering systems
- `InitializationSystemGroup` — setup systems (run once or infrequently)

## Step 4: Generate the system file

File naming: `{Subject}.System.cs`

### Template — system with Entities.ForEach equivalent (IJobEntity)

```csharp
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;

/// <summary>
/// [{SystemName}] — {One-line description of its single responsibility}.
/// Reads: {ComponentA}, {ComponentB}
/// Writes: {ComponentC}
/// </summary>
[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(PreviousSystem))] // if ordering is needed
public partial struct {SystemName}System : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        // Require components so the system only runs when they exist
        state.RequireForUpdate<{RequiredComponent}>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        // Use EntityCommandBuffer for structural changes
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(state.WorldUnmanaged);

        // Schedule a job for Burst-compiled processing
        new {SystemName}Job
        {
            // Pass job data
        }.ScheduleParallel();
    }
}

[BurstCompile]
public partial struct {SystemName}Job : IJobEntity
{
    public EntityCommandBuffer.ParallelWriter Ecb;

    public void Execute(Entity entity, [ChunkIndexInQuery] int chunkIndex,
        ref {ComponentToWrite} writeTarget,
        in {ComponentToRead} readSource)
    {
        // Pure logic — no side effects, no managed types
    }
}
```

### Template — singleton / manager system (no job needed)

```csharp
using Unity.Burst;
using Unity.Entities;

[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial struct {SystemName}System : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<{RequiredSingleton}>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var singleton = SystemAPI.GetSingletonRW<{SingletonComponent}>();
        // Operate on singleton
    }
}
```

### Template — system that reads config singleton

```csharp
var config = SystemAPI.GetSingleton<{ConfigComponent}>();
// Use config.SomeValue instead of hardcoded values
```

## Step 5: Validate after creation

Run `/analyze-system <NewSystemName>` to verify:
- Single responsibility
- Correct data flow position
- Layer separation
- Naming conventions

Run `/no-magic-params <NewSystemFile>` to verify no hardcoded values.

## Rules

1. **One responsibility per system** — if you find yourself writing "and" in the description, split into two systems.
2. **Never access GameObjects directly from a system** — use the sync layer (`EntityVisualSync`).
3. **All mutable data via ECB or `ref`** — never store mutable state on the system struct itself (except `ComponentLookup` / `EntityQuery` cached in `OnCreate`).
4. **Use `[BurstCompile]`** on both the system struct and the job struct whenever possible.
5. **Log prefix**: All debug logs must use `[{SystemName}]` prefix.
6. **No magic numbers**: all tunable values must come from a config singleton (see `/no-magic-params`).
