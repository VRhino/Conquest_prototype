---
name: new-authoring
description: >
  Scaffold the Baker and Authoring MonoBehaviour for an existing ECS component.
  AUTO-INVOKE when: (1) a new ECS component needs to be configured in the Unity Inspector,
  (2) the user says "make this editable in the editor / inspector", "add authoring",
  "I need to set values in the subscene", or "how do I bake this component",
  (3) after /new-ecs-component when the component needs editor configuration,
  (4) a config singleton needs to be placed in DOTSWorld.unity.
  Always run AFTER the component file exists.
disable-model-invocation: false
allowed-tools: Read, Grep, Glob, Write, Edit
---

# New Authoring — Scaffold Baker + Authoring for an ECS Component

## Usage
```
/new-authoring <ComponentName> [<ComponentFilePath>]
```

If no file path is given, search `Assets/Scripts/**/{ComponentName}.Component.cs` automatically.

## Step 1: Read the component

Read the component file to understand:
- All fields and their types
- The component struct name
- Whether it's `IComponentData`, `IBufferElementData`, or a Tag

## Step 2: Check for existing authoring

Search for:
```
Glob: Assets/Scripts/**/{ComponentName}.Authoring.cs
Glob: Assets/Scripts/**/{ComponentName}Authoring.cs
```

If one exists, read it and propose extending it rather than replacing it.

## Step 3: Determine location

Place the authoring file in the **same directory** as the component file.

- Component at `Assets/Scripts/Squads/Components/Foo.Component.cs`
- Authoring at `Assets/Scripts/Squads/Components/Foo.Authoring.cs`

## Step 4: Generate the Authoring file

### Template for IComponentData

```csharp
using Unity.Entities;
using UnityEngine;

/// <summary>
/// Authoring MonoBehaviour for <see cref="{ComponentName}"/>.
/// Attach to a GameObject in the subscene to configure initial values.
/// </summary>
public class {ComponentName}Authoring : MonoBehaviour
{
    [Header("{ComponentName} Settings")]
    // Mirror each field from the component:
    public {FieldType} {FieldName};

    public class Baker : Baker<{ComponentName}Authoring>
    {
        public override void Bake({ComponentName}Authoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new {ComponentName}
            {
                {FieldName} = authoring.{FieldName},
            });
        }
    }
}
```

### Template for IBufferElementData

```csharp
using Unity.Entities;
using UnityEngine;
using System.Collections.Generic;

public class {ComponentName}Authoring : MonoBehaviour
{
    [Header("{ComponentName} Elements")]
    public List<{ElementFieldType}> InitialElements = new();

    public class Baker : Baker<{ComponentName}Authoring>
    {
        public override void Bake({ComponentName}Authoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            var buffer = AddBuffer<{ComponentName}Element>(entity);
            foreach (var item in authoring.InitialElements)
            {
                buffer.Add(new {ComponentName}Element { Value = item });
            }
        }
    }
}
```

### Template for a Config singleton (added as singleton)

Canonical reference: `Assets/Scripts/Squads/SquadSpawnConfig.Authoring.cs`

```csharp
using Unity.Entities;
using UnityEngine;

/// <summary>
/// Authoring for <see cref="{ConfigName}"/> — place once in the subscene.
/// </summary>
public class {ConfigName}Authoring : MonoBehaviour
{
    [Header("Config")]
    public {FieldType} {FieldName} = {DefaultValue};

    public class Baker : Baker<{ConfigName}Authoring>
    {
        public override void Bake({ConfigName}Authoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new {ConfigName}
            {
                {FieldName} = authoring.{FieldName},
            });
        }
    }
}
```

## Step 5: Baker rules

| Rule | Reason |
|------|--------|
| Baker only calls `AddComponent`/`AddBuffer`, nothing else | Bakers run at bake time, not at runtime |
| No runtime logic in Baker | Logic belongs in systems |
| Use `GetEntity(TransformUsageFlags.None)` unless the entity needs a transform | Avoids unnecessary transform overhead |
| Use `GetEntity(TransformUsageFlags.Dynamic)` if the entity moves at runtime | For units, heroes, moving objects |
| Reference to other GameObjects: use `GetEntity(otherGO)` in the Baker | Converts GO reference to Entity reference safely |

## Step 6: Setup in Unity (manual step — tell the user)

After generating the file, the user must:
1. Open the subscene `DOTSWorld.unity` in the Unity Editor.
2. Create or find the appropriate authoring GameObject in the subscene.
3. Add the `{ComponentName}Authoring` MonoBehaviour to that GameObject.
4. Configure the fields in the Inspector.

For config singletons: only one GameObject with this authoring should exist in the subscene.

## Step 7: Validate

- Verify the Baker does NOT contain logic — only `AddComponent`/`AddBuffer` calls.
- Verify field names match between the Authoring and the Component.
- Run `/validate-component <ComponentName>` to confirm the component itself is valid.

## Reference examples in the project

- `Assets/Scripts/Squads/SquadSpawnConfig.Authoring.cs` — config singleton pattern
- `Assets/Scripts/Squads/SquadData.Authoring.cs` — complex data authoring
- `Assets/Scripts/Hero/HeroEntity.Authoring.cs` — hero entity authoring
- `Assets/Scripts/Squads/DestinationMarker.Authoring.cs` — simple prefab reference authoring
