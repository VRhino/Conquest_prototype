# Pipeline de Movimiento de Tropas

Documento técnico que describe el flujo completo de movimiento de las tropas (squads/units), desde la captura de input del jugador hasta la representación visual en pantalla.

---

## 1. Resumen General

```
Input (teclado/mouse)
  ↓
SquadControlSystem         — captura input, escribe SquadInputComponent
  ↓
SquadOrderSystem           — convierte input en órdenes de estado
  ↓
SquadFSMSystem             — máquina de estados del escuadrón
  ↓
FormationSystem            — asigna posiciones de formación iniciales
  ↓
GridFormationUpdateSystem  — sincroniza posiciones de slot cada frame
  ↓
UnitFormationStateSystem   — máquina de estados por unidad (Formed/Waiting/Moving)
  ↓
UnitFollowFormationSystem  — movimiento físico de unidades
  ↓
DestinationMarkerSystem    — marcadores visuales de destino
  ↓
EntityVisualSync           — sincronización ECS → GameObject (visual)
```

Todos los sistemas pertenecen al `SimulationSystemGroup`.

---

## 2. Pipeline del Héroe (Referencia)

El héroe es el punto de referencia para la formación de tropas. Su pipeline:

```
HeroInputSystem → HeroMovementSystem → HeroStateSystem → EntityVisualSync
```

| Sistema | Lee | Escribe |
|---------|-----|---------|
| `HeroInputSystem` | Teclado/Mouse | `HeroInputComponent` (MoveInput, IsSprintPressed, etc.) |
| `HeroMovementSystem` | `HeroInputComponent`, `HeroStatsComponent`, `StaminaComponent` | `HeroMoveIntent` (Direction, Speed) |
| `HeroStateSystem` | `LocalTransform`, `UnitPrevLeaderPosComponent` | `HeroStateComponent` (State: Idle/Moving) |

**Detección de movimiento:** compara posición actual vs anterior; si `distSq > 0.0025f` → `HeroState.Moving`, sino `Idle`.

**Autoridad visual:** Para el héroe, el **GameObject es autoritativo**. `EntityVisualSync` usa `CharacterController.Move()` con gravedad (`-9.81f`) y escribe la posición de vuelta al ECS. Para las unidades es al revés: **ECS es autoritativo**.

---

## 3. Captura de Input — `SquadControlSystem`

**Archivo:** `Assets/Scripts/Squads/Systems/SquadControl.System.cs`
**Atributos:** `[UpdateInGroup(typeof(SimulationSystemGroup))]`, `[UpdateBefore(typeof(SquadOrderSystem))]`, `[UpdateBefore(typeof(FormationSystem))]`

### Controles

| Tecla | Acción | Resultado |
|-------|--------|-----------|
| **C** (simple) | Follow Hero | `orderType = FollowHero` |
| **C** (doble clic) | Hurry to Commander | Toggle `hurryToComander = true/false` |
| **X** (simple) | Hold Position | `orderType = HoldPosition` + raycast posición |
| **X** (doble clic) | Cycle Formation | Avanza circular a siguiente formación |
| **V** | Attack | `orderType = Attack` |
| **F1–F4** | Formación directa | `desiredFormation` = índice 0–3 |

### Detección de doble clic

```
DOUBLE_CLICK_THRESHOLD = 0.5f segundos
```

Si la misma tecla se presiona dos veces dentro del threshold, se activa la acción de doble clic.

### Raycast para Hold Position

Al presionar X, el sistema lanza un raycast contra el layer de terreno. Si falla, usa un plano fallback en Y=0. La posición resultante se guarda en `SquadInputComponent.holdPosition`.

### Componente de salida

```csharp
public struct SquadInputComponent : IComponentData
{
    public SquadOrderType orderType;
    public bool hasNewOrder;
    public FormationType desiredFormation;
    public float3 holdPosition;
    public bool hurryToComander;
}
```

---

## 4. Procesamiento de Órdenes — `SquadOrderSystem`

**Archivo:** `Assets/Scripts/Squads/Systems/SquadOrder.System.cs`
**Atributos:** `[UpdateInGroup(typeof(SimulationSystemGroup))]`, `[UpdateAfter(typeof(SquadControlSystem))]`

### Conversión de órdenes a estado

| `SquadOrderType` | → `SquadFSMState` |
|---|---|
| `FollowHero` | `FollowingHero` |
| `HoldPosition` | `HoldingPosition` |
| `Attack` | `InCombat` |
| Default | `Idle` |

### Lógica

1. Lee `SquadInputComponent` (solo si `hasNewOrder == true`)
2. Copia la orden al `SquadStateComponent`:
   - `currentOrder`, `isExecutingOrder`, `transitionTo`, `currentFormation`
3. Actualiza `FormationComponent.currentFormation`
4. **Hold Position:** crea/actualiza `SquadHoldPositionComponent` con `holdCenter` y `originalFormation`
5. **Otros estados:** elimina `SquadHoldPositionComponent` si existe

---

## 5. Máquina de Estados del Escuadrón — `SquadFSMSystem`

**Archivo:** `Assets/Scripts/Squads/Systems/SquadFSM.System.cs`
**Atributos:** `[UpdateInGroup(typeof(SimulationSystemGroup))]`, `[UpdateAfter(typeof(SquadOrderSystem))]`

### Estados

```
SquadFSMState:
  - Idle
  - FollowingHero
  - HoldingPosition
  - InCombat
  - Retreating
  - KO
```

### Transiciones

| Desde | Hacia | Condición |
|-------|-------|-----------|
| Cualquiera | Estado pendiente | `transitionTo` != estado actual |
| `InCombat` | Otro estado | Solo después de **mínimo 3 segundos** en combate |
| Cualquiera | `KO` | Todas las unidades del buffer `SquadUnitElement` están muertas |
| Cualquiera | `Retreating` | `lastOwnerAlive == false` y no se ha activado retreat |

### Lógica

- Aplica transición pendiente: `currentState = transitionTo`
- Incrementa `stateTimer` cada frame con `deltaTime`
- Enforces mínimo de 3s en combate antes de permitir salida

---

## 6. Sistema de Formaciones

### FormationSystem

**Archivo:** `Assets/Scripts/Squads/Systems/Formation.System.cs`
**Atributos:** `[UpdateInGroup(typeof(SimulationSystemGroup))]`

- Asigna posiciones de formación cuando cambia la formación seleccionada
- Lee `SquadDataComponent.formationLibrary` (blob asset)
- Cooldown entre cambios de formación: **1 segundo** (`formationChangeCooldown = 1f`)
- Llama a `FormationPositionCalculator.CalculateDesiredPosition()` para cada unidad
- Escribe `UnitTargetPositionComponent`, `UnitGridSlotComponent`, `UnitSpacingComponent`

### GridFormationUpdateSystem

**Archivo:** `Assets/Scripts/Squads/Systems/GridFormationUpdate.System.cs`
**Atributos:** `[UpdateInGroup(typeof(SimulationSystemGroup))]`, `[UpdateAfter(typeof(FormationSystem))]`

- Se ejecuta **cada frame** después de `FormationSystem`
- Recalcula `UnitTargetPositionComponent.position` usando `FormationPositionCalculator.CalculateDesiredPosition()`
- Mantiene las posiciones de slot sincronizadas con el centro de formación (héroe o holdCenter)

### Tipos de formación

Line, Dispersed, Testudo, Wedge, Column, Square.

### Blob Assets

```
FormationLibraryBlob
  └── BlobArray<FormationDataBlob> formations
        ├── FormationType formationType
        └── BlobArray<int2> gridPositions    ← coordenadas discretas de grilla
```

### FormationPositionCalculator (utilidad central)

**Archivo:** `Assets/Scripts/Squads/FormationPositionCalculator.cs`

| Método | Propósito |
|--------|-----------|
| `GetSquadCenter()` | Retorna `holdCenter` en Hold Position, sino `heroPos` |
| `CalculateDesiredPosition()` | Convierte posición de grilla a posición world. Centra la grilla, aplica `GridToRelativeWorld()`, ajusta altura de terreno |
| `IsUnitInSlot()` | Check de distancia entre unidad y slot deseado |
| `GetFarestUnitDistanceSq()` | Distancia de la unidad más lejana (para verificar radio de 5m) |
| `GetClosestUnitDistanceSq()` | Distancia de la unidad más cercana |

---

## 7. Estados de Unidad — `UnitFormationStateSystem`

**Archivo:** `Assets/Scripts/Squads/Systems/UnitFormationState.System.cs`
**Atributos:** `[UpdateInGroup(typeof(SimulationSystemGroup))]`, `[UpdateAfter(typeof(GridFormationUpdateSystem))]`

### Estados

```csharp
public enum UnitFormationState
{
    Formed,   // En su slot asignado
    Waiting,  // Esperando delay aleatorio antes de moverse
    Moving    // Moviéndose hacia su slot
}

public struct UnitFormationStateComponent : IComponentData
{
    public UnitFormationState State;
    public float DelayTimer;
    public float DelayDuration;
}
```

### Constantes

| Nombre | Valor | Uso |
|--------|-------|-----|
| `formationRadiusSq` | `25f` (5m²) | Radio para considerar que el héroe está "cerca" |
| `slotThresholdSq` | `0.04f` (~0.2m²) | Distancia para considerar unidad "en slot" |
| `holdPositionThresholdSq` | `1.0f` (1m²) | Distancia para detectar drift en Hold Position |

### Transiciones — Modo Follow Hero

```
Formed → Waiting:  El héroe sale del radio de 5m (usa GetFarestUnitDistanceSq)
                   Delay aleatorio: 0.5–1.5s

Waiting → Moving:  El delay expira (DelayTimer >= DelayDuration)

Waiting → Formed:  El héroe vuelve al radio Y la unidad está en su slot

Moving → Formed:   La unidad llega al slot (~0.2m)
                   Y el héroe está dentro del radio
                   Y el héroe está quieto (HeroState.Idle)

Moving → Moving:   Si el héroe sigue moviéndose, la unidad continúa
```

### Transiciones — Modo Hold Position

```
Formed → Waiting:  La unidad se aleja >1m de su slot
                   Delay aleatorio: 0.5–1.0s

Waiting → Moving:  El delay expira

Waiting → Formed:  La unidad vuelve al slot durante el delay

Moving → Formed:   La unidad llega al slot (~0.2m)
```

### Detección de heroWithinRadius

Usa la **unidad más lejana** para determinar si todas las unidades están dentro del radio. Si la más lejana está dentro, todas lo están.

---

## 8. Movimiento Físico — `UnitFollowFormationSystem`

**Archivo:** `Assets/Scripts/Squads/Systems/UnitFollowFormation.System.cs`
**Atributos:** `[UpdateInGroup(typeof(SimulationSystemGroup))]`, `[UpdateAfter(typeof(GridFormationUpdateSystem))]`

### Condición de movimiento

Solo mueve unidades cuyo `UnitFormationStateComponent.State == Moving`.

### Cálculo de velocidad

```
finalSpeed = baseSpeed × speedMultiplier × hurryBonus

donde:
  baseSpeed       = UnitStatsComponent.velocidad  (fallback: defaultMoveSpeed = 5f)
  speedMultiplier = UnitMoveSpeedVariation.speedMultiplier
  hurryBonus      = 2.0 si hurryToComander activo, sino 1.0
```

### Paso de movimiento

```
diff = targetPosition - currentPosition
step = normalize(diff) × finalSpeed × deltaTime

// Anti-overshoot: si |step| > |diff|, clamp a diff
if (lengthsq(step) > distSq) → step = diff
```

### Condición de parada

```
stoppingDistanceSq = 0.04f  (~0.2m)
```

- **Hold Position:** para cuando `distSq <= stoppingDistanceSq`
- **Follow Hero (héroe quieto):** para cuando `distSq <= stoppingDistanceSq`
- **Follow Hero (héroe moviéndose):** no para, sigue moviéndose

### Orientación

- Tipo por defecto: `UnitOrientationType.FaceMovementDirection`
- Interpolación: `math.slerp()` con `rotationSpeed × deltaTime`
- `rotationSpeed = 5f` rad/s
- Solo plano horizontal (ignora componente Y)

---

## 9. Sincronización Visual

### SquadVisualManagementSystem

**Archivo:** `Assets/Scripts/Squads/Systems/SquadVisualManagement.System.cs`
**Atributos:** `[UpdateInGroup(typeof(SimulationSystemGroup))]`, `[UpdateAfter(typeof(SquadSpawningSystem))]`

1. Detecta unidades con `UnitVisualReference` pero sin `UnitVisualInstance`
2. Busca prefab en `VisualPrefabRegistry.GetPrefab()` o `GetDefaultUnitPrefab(squadType)`
3. Instancia el GameObject
4. Agrega y configura `EntityVisualSync` en el GameObject

### EntityVisualSync

**Archivo:** `Assets/Scripts/Visual/EntityVisualSync.cs`

Se ejecuta cada frame en `Update()` (MonoBehaviour):

| Entidad | Dirección de sync | Detalle |
|---------|-------------------|---------|
| **Héroe** | GameObject → ECS | Lee `transform.position/rotation`, escribe a `LocalTransform` del ECS |
| **Unidad** | ECS → GameObject | Lee `LocalTransform` del ECS, escribe a `transform.position/rotation` |

No hay interpolación en la sincronización — asignación directa de posición.

---

## 10. Mecánicas Especiales

### Hold Position

- Centro fijo en `SquadHoldPositionComponent.holdCenter` (posición del raycast al dar la orden)
- Threshold de **1m** para detectar que una unidad salió de posición
- Las formaciones se recalculan respecto al `holdCenter` en vez del héroe

### Hurry to Commander (doble C)

- Toggle que activa/desactiva `hurryToComander`
- Duplica la velocidad de movimiento (`× 2.0`)

### Cycling de formaciones (doble X)

- Avanza de forma circular por la librería de formaciones del escuadrón
- Sujeto a cooldown de 1 segundo

### Marcadores de destino — DestinationMarkerSystem

**Archivo:** `Assets/Scripts/Squads/Systems/DestinationMarker.System.cs`
**Atributos:** `[UpdateInGroup(typeof(SimulationSystemGroup))]`, `[UpdateAfter(typeof(UnitFollowFormationSystem))]`

- Solo muestra marcadores en **Hold Position** cuando la unidad está en estado **Moving**
- Crea instancias de marker desde `DestinationMarkerPrefabComponent`
- Actualiza posición del marcador al `UnitTargetPositionComponent` de la unidad
- Destruye marcadores cuando la unidad sale de Moving o el squad sale de Hold Position

---

## 11. Tabla de Componentes Clave

| Componente | Propósito | Escrito por |
|------------|-----------|-------------|
| `SquadInputComponent` | Input del jugador (orden, formación, hurry) | `SquadControlSystem` |
| `SquadStateComponent` | Estado actual del escuadrón, timer, orden | `SquadOrderSystem`, `SquadFSMSystem` |
| `SquadHoldPositionComponent` | Centro y formación de Hold Position | `SquadOrderSystem` |
| `FormationComponent` | Formación actual del escuadrón | `SquadOrderSystem` |
| `SquadDataComponent` | Datos del escuadrón (formationLibrary blob) | Setup/Spawn |
| `UnitTargetPositionComponent` | Posición deseada (slot de formación) | `FormationSystem`, `GridFormationUpdateSystem` |
| `UnitGridSlotComponent` | Coordenadas de grilla y offset world | `FormationSystem` |
| `UnitFormationStateComponent` | Estado de la unidad (Formed/Waiting/Moving) | `UnitFormationStateSystem` |
| `UnitStatsComponent` | Stats de la unidad (velocidad, etc.) | Setup/Spawn |
| `UnitMoveSpeedVariation` | Multiplicador individual de velocidad | Setup/Spawn |
| `UnitSpacingComponent` | Slot de spacing en formación | `FormationSystem` |
| `LocalTransform` | Posición/rotación en ECS | `UnitFollowFormationSystem` |
| `HeroInputComponent` | Input del héroe (WASD, sprint, skills) | `HeroInputSystem` |
| `HeroMoveIntent` | Dirección y velocidad de movimiento | `HeroMovementSystem` |
| `HeroStateComponent` | Estado del héroe (Idle/Moving) | `HeroStateSystem` |
| `UnitDestinationMarkerComponent` | Referencia al marcador visual | `DestinationMarkerSystem` |

---

## 12. Orden de Ejecución de Sistemas

Todos en `SimulationSystemGroup`. El orden se define por atributos `[UpdateAfter]` y `[UpdateBefore]`:

```
1.  HeroInputSystem
2.  HeroMovementSystem
3.  HeroStateSystem
4.  SquadControlSystem          [UpdateBefore(SquadOrderSystem, FormationSystem)]
5.  SquadOrderSystem            [UpdateAfter(SquadControlSystem)]
6.  SquadFSMSystem              [UpdateAfter(SquadOrderSystem)]
7.  FormationSystem
8.  GridFormationUpdateSystem   [UpdateAfter(FormationSystem)]
9.  UnitFormationStateSystem    [UpdateAfter(GridFormationUpdateSystem)]
10. UnitFollowFormationSystem   [UpdateAfter(GridFormationUpdateSystem)]
11. DestinationMarkerSystem     [UpdateAfter(UnitFollowFormationSystem)]
12. SquadVisualManagementSystem [UpdateAfter(SquadSpawningSystem)]
13. EntityVisualSync            (MonoBehaviour Update — después de todos los sistemas ECS)
```

> **Nota:** `UnitFormationStateSystem` y `UnitFollowFormationSystem` ambos declaran `[UpdateAfter(GridFormationUpdateSystem)]` pero no tienen dependencia explícita entre sí. Unity puede ejecutarlos en cualquier orden relativo, aunque en la práctica el state system tiende a ejecutarse primero.
