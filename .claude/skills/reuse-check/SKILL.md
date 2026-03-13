---
name: reuse-check
description: Before creating any new class, component, system, utility, or service, exhaustively search the codebase for existing elements that can be reused or extended. Auto-invoked when Claude is about to create new code.
disable-model-invocation: false
allowed-tools: Read, Grep, Glob
---

# Reuse Check — Find reusable elements before creating new code

## When to activate
Activate this skill **before creating any new**:
- ECS System, Component, or Buffer Element
- Service or utility class
- Event type
- UI controller or helper

## Search procedure

Perform ALL of the following searches and report results:

### 1. Core Services (`Assets/Scripts/Core/`)
Search for services that already handle the domain you're about to create:
- `HeroDataService.cs` — hero creation and retrieval
- `SquadDataService.cs` — squad creation and management
- `DataCacheService.cs` — cached hero attributes
- `PlayerSessionService.cs` — player/session state
- `ItemService.cs` — item queries
- `MapService.cs` — map data

### 2. Shared Utilities
Check if these utilities already solve part of the problem:
- `HeroPositionUtility` — hero position retrieval
- `UnitStatsUtility` — stat application
- `FormationPositionCalculator` — formation math

### 3. Inventory Services (`Assets/Scripts/Inventory/Services/`)
Search for item/inventory-related services.

### 4. Existing ECS Components (`Assets/Scripts/Squads/`, `Assets/Scripts/Hero/`)
Search for components that already hold the data you need:
```
Glob: Assets/Scripts/**/*Component*.cs
Glob: Assets/Scripts/**/*Element*.cs
Glob: Assets/Scripts/**/*Tag*.cs
```

### 5. Existing ECS Systems
Search for systems with overlapping responsibility:
```
Glob: Assets/Scripts/**/*.System.cs
```

### 6. Events (`Assets/Scripts/Events/`)
Search for existing event types before creating new ones.

### 7. Keyword search
Search the entire `Assets/Scripts/` directory for keywords related to what you're about to create.

## Output format

Present findings in a table:

| Found Element | File Path | Relevance | Can Reuse? |
|--------------|-----------|-----------|------------|
| ... | ... | ... | Yes/Partial/No |

Then provide a recommendation:
- **REUSE**: Element X already does this. Use it directly.
- **EXTEND**: Element X does 80% of this. Extend it with [specific addition].
- **CREATE NEW**: Nothing suitable found. Proceed with new implementation.

## Rules
- NEVER skip this check. Even if you're confident nothing exists, search anyway.
- If you find a partial match, prefer extending over creating new.
- If creating new, explain why existing elements are insufficient.
