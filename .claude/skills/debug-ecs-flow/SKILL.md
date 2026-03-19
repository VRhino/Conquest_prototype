---
name: debug-ecs-flow
description: >
  Trace and diagnose data flow issues between ECS systems. AUTO-INVOKE when:
  a system isn't receiving expected data, components aren't updating, execution order
  produces wrong results, entities don't appear/disappear as expected, animations
  don't play, units don't move, the NavMesh agent isn't following ECS position,
  a singleton isn't found, or the user says things like "it's not working", "nothing
  happens", "the system doesn't run", "units are stuck", "visual isn't updating".
disable-model-invocation: false
allowed-tools: Read, Grep, Glob, Bash(git log*), Bash(git diff*)
---

# Debug ECS Flow — Trace Data Flow Issues Between Systems

## Usage
```
/debug-ecs-flow <symptom description>
```
Examples:
- `/debug-ecs-flow "units don't move after squad FSM changes state"`
- `/debug-ecs-flow "HeroVisualManagementSystem not updating animation"`
- `/debug-ecs-flow "SquadSpawnConfig singleton not found"`

## Step 1: Map the symptom to the data flow

Read `CLAUDE.md` and `Assets/Scripts/Squads/SystemResponsibilities.md`.

Identify which stage of the pipeline the symptom is in:

```
Input → SquadControlSystem → SquadOrderSystem → SquadFSMSystem
                                                      ↓
         UnitFormationStateSystem ← FormationSystem ──┘
                  ↓
         UnitFollowFormationSystem (movement only)
                  ↓
         SquadVisualManagementSystem → EntityVisualSync (per-unit)
```

Hero flow:
```
HeroInputSystem → HeroMoveIntent → HeroMovementSystem → HeroStateSystem → HeroVisualManagementSystem → EntityVisualSync
```

**Identify the last system that should have written the data** and **the first system that is failing to read it**.

## Step 2: Read the relevant systems

For each system in the suspected pipeline segment, read the full file and check:

### A. Query correctness
| Problem | What to look for |
|---------|-----------------|
| Missing component in query | Does the `IJobEntity` `Execute()` signature include all required components? |
| Wrong access (`in` vs `ref`) | `in` = read-only. `ref` = read-write. Using `in` when needing to write silently fails. |
| Filtered-out entities | Check for `.WithAll<T>()`, `.WithNone<T>()`, `.WithDisabled<T>()` filters |
| Singleton not found | Check `state.RequireForUpdate<T>()` and whether the singleton exists in the subscene |

### B. Execution order
- Check `[UpdateInGroup]` — is the system in the correct group?
- Check `[UpdateBefore]` / `[UpdateAfter]` — are dependencies declared?
- If no ordering, Unity may run systems in arbitrary order within the group.

### C. ECB timing
- Commands buffered with `EndSimulationEntityCommandBufferSystem` play back at **end of frame**.
- If a system reads data in the same frame an ECB write was scheduled, it will see the OLD data.
- Fix: use `BeginSimulationEntityCommandBufferSystem` or restructure ordering.

### D. IEnableableComponent
- If a Tag uses `IEnableableComponent`, check that it's **enabled** (not just present).
- Disabled components are invisible to queries by default.
- Use `WithAll<T>()` to include disabled + enabled, or `WithDisabled<T>()` for disabled only.

### E. Hybrid Model (EntityVisualSync)
- Visual updates (animations, position sync) must go through `EntityVisualSync`.
- ECS systems write to ECS components → `EntityVisualSync.Update()` reads them each frame.
- If visual doesn't update: check `EntityVisualSync` is active and the matching component field is being written.

### F. NavMesh / Agent sync issues
- `NavMeshAgent` is a managed MonoBehaviour — ECS systems cannot access it directly.
- Movement intent lives in ECS (e.g., `MoveTarget`, `NavAgentConfig`); `EntityVisualSync` applies it via `NavMeshAgent.SetDestination()`.
- If units/heroes don't move: check (1) ECS component has updated destination, (2) `EntityVisualSync` is reading it, (3) `NavMeshAgent.isOnNavMesh` is true, (4) agent wasn't warped to an off-mesh position.
- If position desyncs: check that `EntityVisualSync` writes back the GO position to `LocalTransform` after the agent moves.

## Step 3: Search for the data being written

For the component suspected of not updating, grep for writes:
```
Grep: Assets/Scripts/ "ref <ComponentName>"
Grep: Assets/Scripts/ "ecb.Set*<{ComponentName}>"
Grep: Assets/Scripts/ "ecb.Add*<{ComponentName}>"
```

Confirm that at least one system writes to this component and that it runs **before** the reading system.

## Step 4: Common issues & fixes

| Symptom | Likely cause | Fix |
|---------|-------------|-----|
| System never runs | `RequireForUpdate<T>` fails — singleton or component missing | Add authoring to subscene, or check prefab has the component |
| System runs but data unchanged | Query doesn't match — wrong filters or `in` instead of `ref` | Fix the query signature |
| Flickering or one-frame delay | ECB plays back too late | Switch to `BeginSimulationECB` or split ordering |
| Visual doesn't update | ECS component written but `EntityVisualSync` not reading it | Add field read in `EntityVisualSync.SyncVisuals()` |
| Entities destroyed in wrong order | Structural changes mid-frame collision | Use ECB, not direct `EntityManager` during iteration |
| Burst compile error | Managed type or static access in Burst code | Remove managed references, use `FixedString` instead of `string` |
| `InvalidOperationException` on singleton | Multiple entities with the supposed singleton component | Ensure only one entity in subscene has the config authoring |

## Step 5: Output

Report findings in this format:

```
## ECS Flow Debug: {Symptom}

**Suspected pipeline segment:** {A} → {B}
**Root system suspected:** {SystemName}

### Diagnosis
| Check | Finding |
|-------|---------|
| Query match | PASS/FAIL — {details} |
| Execution order | PASS/FAIL — {details} |
| ECB timing | PASS/FAIL — {details} |
| IEnableableComponent state | PASS/FAIL/N/A |
| Hybrid layer | PASS/FAIL/N/A |

### Root Cause
{Explanation of what is wrong}

### Fix
{Concrete code change to make}
```

## Reference files

- `CLAUDE.md` — full data flow and system responsibilities summary
- `Assets/Scripts/Squads/SystemResponsibilities.md` — system responsibilities
- `Docs/ModeloHybrido.md` — hybrid ECS-GameObject layer rules
- `Assets/Scripts/Visual/EntityVisualSync.cs` — sync layer
