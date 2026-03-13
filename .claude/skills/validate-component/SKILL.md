---
name: validate-component
description: Validate an ECS component, buffer element, or tag follows project conventions including naming, struct purity, minimal fields, and no duplication with existing components.
disable-model-invocation: false
allowed-tools: Read, Grep, Glob
---

# Validate Component — ECS Component Compliance Review

## Usage
```
/validate-component <ComponentName or file path>
```
If no argument is given, validate all new/modified component files in the current git diff.

## Validation checklist

### 1. Naming Convention
Verify correct naming based on interface:

| Interface | Naming Pattern | File Pattern | Example |
|-----------|---------------|--------------|---------|
| `IComponentData` | `{Name}Component` or descriptive name | `{Name}.Component.cs` | `SquadMovement.Component.cs` |
| `IBufferElementData` | `{Name}Element` | `{Name}Element.cs` | `InactiveSquadElement.cs` |
| `IEnableableComponent` | `{Name}Tag` or `{Name}Component` | varies | `SquadSwapExecuteTag.cs` |
| Tag (empty struct) | `{Name}Tag` | `{Name}Tag.cs` | `IsMovingTag.cs` |

### 2. Struct Purity
Verify:
- Component is a `struct`, not a `class`
- No methods with side effects (pure data only)
- No references to MonoBehaviours, GameObjects, or managed types (unless using `ICleanupComponentData`)
- No business logic — components are data containers only

### 3. No Duplication
Search existing components for overlapping data:
```
Glob: Assets/Scripts/**/*Component*.cs
Glob: Assets/Scripts/**/*Element*.cs
Glob: Assets/Scripts/**/*Tag*.cs
```

Check if any existing component already stores the same fields or serves the same purpose. Report overlaps.

### 4. Minimal Fields
Verify the component:
- Has only fields necessary for its purpose
- Doesn't combine unrelated data (should be split into separate components)
- Uses appropriate types (`Entity` for references, `float3` for positions, etc.)
- Uses `FixedString` instead of `string` for text data

### 5. Authoring (if applicable)
If the component has a Baker/Authoring class:
- Verify it follows the `{Name}Authoring` naming pattern
- Verify the Baker only adds the component, doesn't contain logic

## Output format

```
## Component Validation: {ComponentName}

**File:** {path}
**Type:** IComponentData / IBufferElementData / Tag / IEnableableComponent
**Fields:** {list of fields}

### Results
| Check | Status | Notes |
|-------|--------|-------|
| Naming | PASS/WARN/FAIL | ... |
| Struct Purity | PASS/WARN/FAIL | ... |
| No Duplication | PASS/WARN/FAIL | ... |
| Minimal Fields | PASS/WARN/FAIL | ... |
| Authoring | PASS/WARN/FAIL/N/A | ... |

### Duplicates Found
| Existing Component | File | Overlapping Fields |
|-------------------|------|--------------------|
| ... | ... | ... |

### Recommendations
- ...
```
