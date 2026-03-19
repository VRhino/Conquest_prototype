---
name: analyze-system
description: >
  Analyze an ECS system to verify it follows single-responsibility rules, correct data flow,
  hybrid model layer separation, and project naming conventions.
  AUTO-INVOKE this skill whenever: (1) a new or modified *.System.cs file was just written,
  (2) the user asks "does this system look right / follow conventions?", (3) before committing
  any ECS-related change, or (4) a system is producing unexpected behavior. Also invoke when
  the user says "review my system", "check this system", or "analyze the system".
disable-model-invocation: false
allowed-tools: Read, Grep, Glob
---

# Analyze System — ECS System Compliance Review

## Usage
```
/analyze-system <SystemName or file path>
```
If no argument is given, analyze all modified `.System.cs` files in the current git diff.

## Analysis checklist

### 1. Single Responsibility
Read `Assets/Scripts/Squads/SystemResponsibilities.md` for the canonical responsibility list.

Verify the system does ONE thing:
- **SquadControlSystem**: captures raw input only
- **SquadOrderSystem**: converts input to squad state changes only
- **SquadFSMSystem**: handles squad-level state transitions only
- **UnitFormationStateSystem**: owns all unit state transitions (Moving/Formed/Waiting)
- **UnitFollowFormationSystem**: moves units physically; never changes state
- **FormationSystem**: calculates formation slot positions only
- **DestinationMarkerSystem**: reads state, never writes it

Flag any system that handles more than one concern.

### 2. Data Flow Compliance
Verify the system fits in the correct position of the data flow:
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

Flag systems that read/write data from the wrong stage.

### 3. Hybrid Model Layer Separation
Read `Docs/ModeloHybrido.md` for reference.

Verify:
- System does NOT directly access GameObjects or MonoBehaviours
- System does NOT call `GetComponent<T>()` on GameObjects
- Visual updates go through `EntityVisualSync` only
- ECS state is never modified from GameObject side

### 4. Naming Convention
- File: `{Subject}.System.cs` or `{Subject}{Verb}.System.cs`
- Class: `partial struct {Name}System : ISystem`
- Verify `[BurstCompile]` where applicable

### 5. System Ordering
- Check for `[UpdateInGroup(typeof(...))]`
- Check for `[UpdateBefore(typeof(...))]` / `[UpdateAfter(typeof(...))]`
- Verify ordering is consistent with data flow

### 6. Shared Utilities Usage
Check if the system duplicates logic available in:
- `HeroPositionUtility`
- `UnitStatsUtility`
- `FormationPositionCalculator`
- `VisualPrefabRegistry` — for visual instantiation (never use `Instantiate()` directly)

### 7. NavMesh / Physics Integration (if applicable)
If the system moves entities, verify:
- NavMeshAgent usage goes through `NavMeshAgent.SetDestination()` on the GO side via `EntityVisualSync`
- ECS systems never directly access `NavMeshAgent` (that's a managed type — use a sync component)
- Physics forces/velocity changes use `Unity.Physics` components, not `Rigidbody`

### 8. Final step
After analysis, if issues were found, recommend running `/review-ecs` before committing.

## Output format

```
## System Analysis: {SystemName}

**File:** {path}
**Responsibility:** {what it does}
**Data Flow Position:** {where it sits in the pipeline}

### Results
| Check | Status | Notes |
|-------|--------|-------|
| Single Responsibility | PASS/WARN/FAIL | ... |
| Data Flow | PASS/WARN/FAIL | ... |
| Layer Separation | PASS/WARN/FAIL | ... |
| Naming | PASS/WARN/FAIL | ... |
| System Ordering | PASS/WARN/FAIL | ... |
| Utility Reuse | PASS/WARN/FAIL | ... |

### Issues Found
- ...

### Recommendations
- ...
```
