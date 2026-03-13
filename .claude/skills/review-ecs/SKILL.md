---
name: review-ecs
description: Comprehensive review of ECS code changes before commit. Checks layer separation, hybrid model compliance, naming conventions, logging, service usage, and utility reuse. Manual invocation only.
disable-model-invocation: true
allowed-tools: Read, Grep, Glob, Bash(git diff*), Bash(git log*)
---

# Review ECS — Comprehensive ECS Code Review

## Usage
```
/review-ecs
```
Reviews all staged and unstaged ECS-related changes.

## Step 1: Gather context

Run these commands to identify changed files:
```bash
git diff --cached --name-only
git diff --name-only
git diff --cached --stat
```

Filter for ECS-related files:
- `*.System.cs` — Systems
- `*Component*.cs`, `*Element*.cs`, `*Tag*.cs` — Components
- Files in `Assets/Scripts/Squads/`, `Assets/Scripts/Hero/`
- Files referencing ECS namespaces (`Unity.Entities`, `Unity.Burst`, etc.)

## Step 2: Review checklist

For each changed file, verify:

### A. Layer Separation (Hybrid Model)
Reference: `Docs/ModeloHybrido.md`

| Rule | Check |
|------|-------|
| ECS systems never reference GameObjects directly | Grep for `GameObject`, `Transform`, `MonoBehaviour` in System files |
| GameObjects never write to ECS | Grep for `EntityManager.Set*`, `ecb.Set*` in non-System files |
| Sync goes through `EntityVisualSync` only | Verify visual updates use the sync layer |
| Visual instantiation uses `VisualPrefabRegistry` | No manual `Instantiate()` calls in ECS systems |

### B. System Responsibilities
Reference: `Assets/Scripts/Squads/SystemResponsibilities.md`

- Each system handles ONE concern
- Systems don't cross data flow boundaries
- New systems declare `[UpdateInGroup]` and ordering attributes

### C. Naming Conventions

| Type | Convention |
|------|-----------|
| System file | `{Subject}.System.cs` |
| Component file | `{Name}.Component.cs` |
| System class | `partial struct {Name}System : ISystem` |
| Component class | `struct {Name} : IComponentData` |

### D. Logging Convention
All log messages should use `[ClassName]` prefix:
```csharp
Debug.Log($"[SquadFSMSystem] State changed to {newState}");
```

### E. Service Usage
Verify code uses Core Services instead of direct data access:
- `HeroDataService` for hero data
- `SquadDataService` for squad data
- `DataCacheService` for cached attributes
- `PlayerSessionService` for session state

### F. Utility Reuse
Check that changed code doesn't duplicate logic from:
- `HeroPositionUtility` — hero position retrieval
- `UnitStatsUtility` — stat application
- `FormationPositionCalculator` — formation math

### G. Component Quality
For new/changed components:
- Pure data structs (no logic)
- No field duplication with existing components
- Appropriate types used

### H. No Magic Parameters
Reference: `/no-magic-params` skill

- No hardcoded numeric literals in logic (except 0, 1, -1 as math identities)
- Timers, thresholds, multipliers, and balance values must come from config components or `[SerializeField]`
- ECS systems use singleton config pattern: `SystemAPI.GetSingleton<ConfigComponent>()`
- MonoBehaviours use `[SerializeField]` fields
- Strings used as tags/layers/identifiers must be constants or config values

## Step 3: Output

```
## ECS Code Review

**Files reviewed:** {count}
**Scope:** {staged/unstaged/both}

### Summary
| Category | Pass | Warn | Fail |
|----------|------|------|------|
| Layer Separation | ... | ... | ... |
| System Responsibilities | ... | ... | ... |
| Naming | ... | ... | ... |
| Logging | ... | ... | ... |
| Service Usage | ... | ... | ... |
| Utility Reuse | ... | ... | ... |
| Component Quality | ... | ... | ... |
| No Magic Parameters | ... | ... | ... |

### Issues Found

#### FAIL — Must fix before commit
1. ...

#### WARN — Should fix
1. ...

### Files Reviewed
| File | Status | Notes |
|------|--------|-------|
| ... | PASS/WARN/FAIL | ... |
```
