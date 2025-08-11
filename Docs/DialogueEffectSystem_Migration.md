# Guía de Migración: DialogueEffect System

## Resumen
Esta guía explica cómo migrar del sistema antiguo de `customEvent` al nuevo sistema de `DialogueEffect` ScriptableObjects, que proporciona mayor flexibilidad y modularidad.

## Arquitectura del Nuevo Sistema

### Componentes Principales

1. **DialogueEffect (Base Class)**
   - Clase abstracta base para todos los efectos
   - Métodos: `Execute()`, `CanExecute()`, `GetPreviewText()`
   - Ubicación: `Assets/Scripts/Dialogue/DialogueEffect.cs`

2. **DialogueEffectDatabase**
   - Singleton que gestiona todos los efectos disponibles
   - Carga automática desde Resources
   - Ubicación: `Assets/Scripts/Dialogue/DialogueEffectDatabase.cs`

3. **DialogueEffectSystem**
   - Sistema de ejecución con validación y manejo de errores
   - Ubicación: `Assets/Scripts/Dialogue/DialogueEffectSystem.cs`

4. **Efectos Específicos**
   - `AddItemDialogueEffect`: Agregar ítems al inventario
   - `GiveExperienceDialogueEffect`: Dar experiencia al héroe
   - `UnlockSquadDialogueEffect`: Desbloquear nuevos squads

### Estructura de Archivos
```
Assets/Scripts/Dialogue/
├── DialogueEffect.cs (base class)
├── DialogueEffectDatabase.cs (database)
├── DialogueEffectSystem.cs (execution engine)
├── Effects/
│   ├── AddItemDialogueEffect.cs
│   ├── GiveExperienceDialogueEffect.cs
│   └── UnlockSquadDialogueEffect.cs
├── Examples/
│   └── ExampleNpcDialogue.cs
└── Test/
    └── DialogueEffectSystemTest.cs
```

## Migración Paso a Paso

### Paso 1: Crear Efectos ScriptableObject

#### Antes (Sistema Antiguo):
```csharp
// En NpcTriggerZone.cs
case "Add_Item":
    InventoryService.AddItem("TorMADef", 1);
    break;
```

#### Después (Sistema Nuevo):
1. Crear asset: `Right-click > Create > Conquest Tactics > Dialogue > Add Item Effect`
2. Configurar en Inspector:
   - Name: "AddWeapon"
   - Description: "Adds a weapon to hero inventory"
   - Priority: 1

### Paso 2: Actualizar DialogueOption Data

#### Antes:
```csharp
DialogueOption option = new DialogueOption
{
    optionText = "Take sword",
    optionType = DialogueOptionType.Normal,
    customEvent = "Add_Item" // Hard-coded
};
```

#### Después:
```csharp
DialogueOption option = new DialogueOption
{
    optionText = "Take sword",
    optionType = DialogueOptionType.ExecuteEffects,
    dialogueEffectIds = new string[] { "AddWeapon" }, // Flexible
    effectParameters = new DialogueParameters
    {
        stringParameter = "WeaS&SBasic", // Item ID
        intParameter = 1, // Quantity
        floatParameter = 0f,
        boolParameter = false
    }
};
```

### Paso 3: Configurar Efectos en Unity Editor

#### Para AddItemDialogueEffect:
1. Navegar a `Assets/Resources/DialogueEffects/`
2. Crear nuevo: `Create > Conquest Tactics > Dialogue > Add Item Effect`
3. Configurar:
   - **Effect ID**: "AddWeapon"
   - **Description**: "Adds weapon to inventory"
   - **Priority**: 1

#### Para GiveExperienceDialogueEffect:
1. Crear: `Create > Conquest Tactics > Dialogue > Give Experience Effect`
2. Configurar:
   - **Effect ID**: "GiveExperience"
   - **Description**: "Gives experience to hero"
   - **Priority**: 2

## Compatibilidad y Transición

### Backward Compatibility
El sistema nuevo es compatible con el antiguo:
- Los `customEvent` existentes siguen funcionando
- `NpcTriggerZone.cs` maneja ambos sistemas
- Migración gradual posible

### Casos de Uso Comunes

#### 1. Agregar Ítem Simple
```csharp
// Efecto: AddItem
// Parámetros:
effectParameters = new DialogueParameters
{
    stringParameter = "ItemID", // ID del ítem
    intParameter = 1, // Cantidad
    floatParameter = 0f, // Costo (opcional)
    boolParameter = false // Restar dinero
};
```

#### 2. Recompensa de Misión (Múltiples Efectos)
```csharp
dialogueEffectIds = new string[] { "GiveExperience", "AddItem" };
effectParameters = new DialogueParameters
{
    stringParameter = "RewardSword", // Ítem de recompensa
    intParameter = 100, // Experiencia
    floatParameter = 0f,
    boolParameter = false
};
```

#### 3. Comerciante (Con Validación)
```csharp
// Solo mostrar opción si el héroe tiene suficiente dinero
requireEffectsCanExecute = true;
effectParameters = new DialogueParameters
{
    stringParameter = "ExpensiveWeapon",
    intParameter = 1,
    floatParameter = 500f, // Costo
    boolParameter = true // Validar dinero
};
```

## Ventajas del Nuevo Sistema

### Para Programadores:
- **Modularidad**: Efectos reutilizables
- **Extensibilidad**: Fácil agregar nuevos tipos
- **Mantenibilidad**: Lógica centralizada
- **Testing**: Cada efecto es testeable independientemente

### Para Diseñadores:
- **Sin Código**: Crear efectos desde Unity Editor
- **Flexibilidad**: Parámetros configurables
- **Reutilización**: Mismos efectos en múltiples NPCs
- **Validación**: Prevención de errores de configuración

### Para el Proyecto:
- **Consistencia**: Comportamiento uniforme
- **Debugging**: Mejor rastreabilidad de errores
- **Performance**: Carga optimizada con Resources
- **Escalabilidad**: Fácil expansión del sistema

## Ejemplos de Migración

### Ejemplo 1: NPC Comerciante

#### Antes:
```csharp
// Multiple custom events needed
customEvent = "Add_Item" // Hard-coded logic
```

#### Después:
```csharp
dialogueOptions = new DialogueOption[]
{
    new DialogueOption
    {
        optionText = "Buy sword (100 gold)",
        optionType = DialogueOptionType.ExecuteEffects,
        dialogueEffectIds = new string[] { "AddItem" },
        effectParameters = new DialogueParameters
        {
            stringParameter = "BasicSword",
            intParameter = 1,
            floatParameter = 100f,
            boolParameter = true // Subtract gold
        },
        requireEffectsCanExecute = true
    }
};
```

### Ejemplo 2: Quest Reward

#### Antes:
```csharp
// Multiple separate custom events
customEvent = "Give_Experience"
// + separate item giving logic
```

#### Después:
```csharp
new DialogueOption
{
    optionText = "Complete quest",
    optionType = DialogueOptionType.ExecuteEffects,
    dialogueEffectIds = new string[] { "GiveExperience", "AddItem", "UnlockSquad" },
    effectParameters = new DialogueParameters
    {
        stringParameter = "QuestReward",
        intParameter = 200, // XP amount
        floatParameter = 0f,
        boolParameter = false
    }
};
```

## Checklist de Migración

### Pre-Migración:
- [ ] Identificar todos los `customEvent` existentes
- [ ] Documentar parámetros actuales de cada evento
- [ ] Crear backup del proyecto

### Durante Migración:
- [ ] Crear DialogueEffect assets para cada tipo
- [ ] Configurar IDs y parámetros
- [ ] Actualizar DialogueOption data
- [ ] Testear cada efecto individualmente

### Post-Migración:
- [ ] Verificar que todos los efectos funcionan
- [ ] Remover código obsoleto gradualmente
- [ ] Documentar nuevos efectos creados
- [ ] Entrenar al equipo en el nuevo sistema

## Troubleshooting

### Problemas Comunes:

1. **"Effect not found"**
   - Verificar que el asset esté en `Resources/DialogueEffects/`
   - Confirmar que el Effect ID coincida exactamente

2. **"Cannot execute effect"**
   - Revisar validación en `CanExecute()`
   - Verificar que los parámetros sean correctos

3. **"Compilation errors"**
   - Verificar imports de namespace `ConquestTactics.Dialogue`
   - Confirmar que todos los scripts estén en las carpetas correctas

### Debug Tools:
- Use `DialogueEffectSystemTest.cs` para testing
- `DialogueEffectDatabase.GetDebugInfo()` para información del sistema
- Console logs en cada efecto para troubleshooting

## Conclusión

El nuevo sistema DialogueEffect proporciona:
- ✅ Mayor flexibilidad que customEvents
- ✅ Reutilización de código
- ✅ Configuración sin programación
- ✅ Validación automática
- ✅ Mejor mantenibilidad

La migración puede ser gradual, manteniendo compatibilidad con el sistema anterior mientras se implementan nuevos efectos.
