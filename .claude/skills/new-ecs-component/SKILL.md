---
name: new-ecs-component
description: Scaffold a new ECS Component, Tag, or Buffer Element following Conquest project conventions. Auto-invoked when creating new ECS data types. Ensures struct purity, correct naming, and no duplication.
disable-model-invocation: false
allowed-tools: Read, Grep, Glob, Write, Edit
---

# New ECS Component — Scaffold an ECS Data Type

## Usage
```
/new-ecs-component <ComponentName> [--type component|tag|buffer|enableable] [--squad|--hero]
```

## Step 1: Reuse check FIRST

Run `/validate-component <ComponentName>` or manually search:
```
Glob: Assets/Scripts/**/*Component*.cs
Glob: Assets/Scripts/**/*Element*.cs
Glob: Assets/Scripts/**/*Tag*.cs
```

Also search for keywords: 
```
Grep: Assets/Scripts/ "<field you need>"
```

If an existing component already holds the same data, **extend it** rather than creating a new component.

## Step 2: Classify the type

| Use case | Interface | Naming | File |
|----------|-----------|--------|------|
| State/data per entity | `IComponentData` | `{Name}` or descriptive noun | `{Name}.Component.cs` |
| Enabled/Disabled toggle | `IEnableableComponent` | `{Name}Tag` or `{Name}` | `{Name}Tag.cs` |
| Empty marker / flag | `IComponentData` (empty struct) | `{Name}Tag` | `{Name}Tag.cs` |
| List of data per entity | `IBufferElementData` | `{Name}Element` | `{Name}Element.cs` |

## Step 3: Determine the correct location

| Domain | Location |
|--------|----------|
| Hero-related | `Assets/Scripts/Hero/Components/` |
| Squad/Unit-related | `Assets/Scripts/Squads/Components/` |
| Shared | `Assets/Scripts/Shared/` |

## Step 4: Generate the component file

### Template — IComponentData

```csharp
using Unity.Entities;
using Unity.Mathematics; // if using float3, quaternion, etc.

/// <summary>
/// {ComponentName} — {One-line description of what data this holds and why}.
/// Owner: {SystemThatWritesIt}
/// Readers: {SystemsThatReadIt}
/// </summary>
public struct {ComponentName} : IComponentData
{
    public {Type} {FieldName};
    // Add only fields essential to this component's single purpose
}
```

### Template — Enableable Tag (IEnableableComponent)

```csharp
using Unity.Entities;

/// <summary>
/// {ComponentName}Tag — Marks an entity as {description}.
/// Used by: {SystemName} to enable/disable this tag as a signal.
/// </summary>
public struct {ComponentName}Tag : IComponentData, IEnableableComponent { }
```

### Template — Pure Tag (marker, no data)

```csharp
using Unity.Entities;

/// <summary>
/// {ComponentName}Tag — Marks an entity as {description}.
/// This is a zero-size marker only; no data is stored.
/// </summary>
public struct {ComponentName}Tag : IComponentData { }
```

### Template — IBufferElementData

```csharp
using Unity.Entities;

/// <summary>
/// {ComponentName}Element — Buffer element that stores {description}.
/// Each entity can hold a dynamic list of these.
/// </summary>
[InternalBufferCapacity(8)] // adjust capacity to typical count
public struct {ComponentName}Element : IBufferElementData
{
    public {Type} {FieldName};
}
```

## Step 5: Field type guidelines

| Data | Recommended type |
|------|-----------------|
| Position / direction | `float3` (Unity.Mathematics) |
| Rotation | `quaternion` (Unity.Mathematics) |
| Entity reference | `Entity` |
| Short text | `FixedString32Bytes` / `FixedString64Bytes` |
| Enum | Plain C# `enum` (blittable) |
| Timers | `float` |
| Health / stats | `float` or `int` |

**Never use**: `string`, `List<T>`, `UnityEngine.GameObject`, `UnityEngine.Transform`, or any managed reference.

## Step 6: Should this have an Authoring/Baker?

| Needs Authoring? | Reason |
|-----------------|--------|
| **Yes** | Component needs to be set in the Unity Editor on a prefab/subscene |
| **No** | Component is added/removed at runtime only by systems |

If yes, proceed to `/new-authoring` after creating this component.

## Step 7: Validate after creation

Run `/validate-component <ComponentName>` to verify:
- Naming convention
- Struct purity
- No duplication with existing components
- Minimal fields
