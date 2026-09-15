# Pipeline de Movimiento de Tropas

Documento técnico que describe el flujo completo de movimiento de las tropas (squads/units), desde la captura de input del jugador hasta la representación visual en pantalla.

Actualizado: 2026-09-15. Verificado contra `3eef0e17`.

---

## 1. Resumen General

```
Input (teclado/mouse)
  ↓
SquadControlSystem         — captura input, publica intención del jugador
  ↓
OrderResolutionSystem      — arbitra intent del jugador, IA y reacción de combate
  ↓
SquadOrderSystem           — aplica la orden resuelta
  ↓
SquadFSMSystem             — máquina de estados del escuadrón
  ↓
SquadAnchorSystem          — única autoridad del ancla de formación
  ↓
FormationSystem            — asigna posiciones de formación iniciales
  ↓
GridFormationUpdateSystem  — sincroniza posiciones de slot cada frame
  ↓
UnitFormationStateSystem   — máquina de estados por unidad (Formed/Waiting/Moving)
  ↓
UnitNavMeshSystem          — única autoridad de destinos y movimiento NavMesh
  ↓
UnitFollowFormationSystem  — velocidad y orientación de formación
  ↓
UnitBodyblockSystem        — corrección física entre enemigos
  ↓
NavMeshPositionSyncSystem  — posición NavMesh GameObject → ECS
  ↓
UnitRotationResolutionSystem — resuelve la intención de rotación
```

Todos los sistemas pertenecen al `SimulationSystemGroup`.

---

## 2. Pipeline del Héroe (Referencia)

El héroe es el punto de referencia para la formación de tropas. Su pipeline:

```
HeroInputSystem → HeroMovementSystem → HeroMoveIntent → LocalHeroCharacterMotor
```

| Sistema | Lee | Escribe |
|---------|-----|---------|
| `HeroInputSystem` | Teclado/Mouse | `HeroInputComponent` (MoveInput, IsSprintPressed, etc.) |
| `HeroMovementSystem` | `HeroInputComponent`, `HeroStatsComponent`, `StaminaComponent` | `HeroMoveIntent` (Direction, Speed) |
| `LocalHeroCharacterMotor` | `HeroMoveIntent`, vida y revisión de spawn | Mueve el `CharacterController` y publica `LocalTransform` + `HeroMotorStateComponent` |

No existe actualmente `HeroStateSystem` ni `HeroStateComponent`. La animación local combina dirección/eventos de input con velocidad y grounded confirmados por `HeroMotorStateComponent`; la animación remota usa la velocidad del `NavMeshAgent`.

**Autoridad física:** para el héroe local, `LocalHeroCharacterMotor` es el único ejecutor y el `CharacterController` resuelve el resultado físico. Para unidades y héroes remotos con `syncPositionFromNavMesh`, el `NavMeshAgent` es la autoridad física y `NavMeshPositionSyncSystem` publica su posición en ECS.

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

### Componentes de salida

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

`SquadInputComponent` conserva flags y configuración compartida, pero la orden local candidata se publica en `SquadPlayerOrderIntentComponent`. La IA publica `SquadAIOrderIntentComponent` y `CombatReactionSystem` publica `SquadCombatReactionIntentComponent`; ningún productor escribe directamente el estado aplicado.

---

## 4. Procesamiento de Órdenes — `SquadOrderSystem`

**Archivo:** `Assets/Scripts/Squads/Systems/SquadOrder.System.cs`
**Orden relevante:** `OrderResolutionSystem` se ejecuta después de reacción de combate y antes de `SquadOrderSystem`.

### Conversión de órdenes a estado

| `SquadOrderType` | → `SquadFSMState` |
|---|---|
| `FollowHero` | `FollowingHero` |
| `HoldPosition` | `HoldingPosition` |
| `Attack` | `InCombat` |
| Default | `Idle` |

### Lógica

1. `OrderResolutionSystem` arbitra intención de jugador, IA y reacción de combate y escribe `SquadResolvedOrderComponent` con `OrderSource`.
2. `SquadOrderSystem` procesa sólo `resolved.hasNewOrder`.
3. Si `retreatTriggered` está activo, descarta la orden sin desbloquear la retirada.
4. Copia `order`, `isExecutingOrder` y `transitionTo` a `SquadStateComponent`.
5. **Hold Position:** crea/actualiza `SquadHoldPositionComponent` con centro, rotación y formación original.
6. **Otros estados:** elimina `SquadHoldPositionComponent` si existe.
7. No confirma formación: `FormationSystem` lo hace únicamente después de validar y asignar slots.

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
| `InCombat` | Otro estado | Tras `SquadSpawnConfigComponent.minCombatDuration`, salvo override insistente del jugador |
| Cualquiera | `KO` | Todas las unidades del buffer `SquadUnitElement` están muertas |
| Cualquiera | `Retreating` | Petición operativa de swap o `SquadOwnerDeathRetreatSystem` |

### Lógica

- Aplica transición pendiente: `currentState = transitionTo`
- Incrementa `stateTimer` cada frame con `deltaTime`
- Mantiene `InCombat` mientras `SquadAIComponent.isInCombat`, salvo override insistente del jugador
- Lee `minCombatDuration` desde `SquadSpawnConfigComponent` (fallback interno de 1 s)
- Conserva retiradas comprometidas incluso al morir la última unidad; no crea por sí misma componentes de retirada

---

## 6. Sistema de Formaciones

### FormationSystem

**Archivo:** `Assets/Scripts/Squads/Systems/Formation.System.cs`
**Atributos:** `[UpdateInGroup(typeof(SimulationSystemGroup))]`

- Asigna posiciones de formación cuando cambia la formación seleccionada
- Lee `SquadDataComponent.formationLibrary` (blob asset)
- Cooldown entre cambios de formación: **1 segundo** (`formationChangeCooldown = 1f`)
- Lee `SquadHoldPositionComponent` si existe y lo pasa a `CalculateDesiredPosition()` para que en modo Hold Position las posiciones se calculen respecto al holdCenter
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
| `GetFarestUnitDistanceSq()` | Distancia de la unidad más lejana (para verificar radio de 10m) |
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
| `formationRadiusSq` | `100f` (10m²) | Radio para considerar que el héroe está "cerca" (soporta formaciones de hasta 20 unidades) |
| `slotThresholdSq` | `0.04f` (~0.2m²) | Distancia para considerar unidad "en slot" |
| `holdPositionThresholdSq` | `1.0f` (1m²) | Distancia para detectar drift en Hold Position |

### Transiciones — Modo Follow Hero

```
Formed → Waiting:  El héroe sale del radio de 10m (usa GetFarestUnitDistanceSq)
                   O la unidad ya no está cerca de su slot asignado (detecta cambios de formación)
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

## 8. Movimiento físico — `UnitNavMeshSystem`

**Archivo:** `Assets/Scripts/Squads/Systems/UnitNavMesh.System.cs`
**Atributos:** después de `UnitFormationStateSystem` y `UnitTargetingSystem`; antes de `UnitFollowFormationSystem`.

### Condición de movimiento

Es la única autoridad que llama `NavMeshAgent.SetDestination()`. El destino efectivo es el slot de formación o un punto de parada próximo al objetivo de combate. Las unidades `Waiting` sin persecución elegible conservan su espera; `HoldPosition` nunca autoriza persecución.

### Cálculo de velocidad

```
finalSpeed = baseSpeed × speedMultiplier × hurryOrCombatBonus

donde:
  baseSpeed       = UnitStatsComponent.velocidad  (fallback: defaultMoveSpeed = 5f)
  speedMultiplier = UnitMoveSpeedVariation.speedMultiplier
  hurryOrCombatBonus = 2.0 si hurryToComander o existe target válido, sino 1.0
```

### Destino y autoridad

```
UnitTargetPositionComponent
  → proyección con NavMesh.SamplePosition
  → NavMeshAgent.SetDestination(destino efectivo)
  → UnitBodyblockSystem aplica agent.Move(offset)
  → NavMeshPositionSyncSystem copia agent.transform.position a LocalTransform
```

### Orientación

- `UnitNavMeshSystem` publica orientación de combate cuando el objetivo está próximo.
- `UnitFollowFormationSystem` publica orientación disciplinada hacia el héroe o el ancla de Hold cuando la unidad está formada y no está engaging.
- `UnitRotationResolutionSystem` aplica la intención ganadora; no hay una segunda ruta directa de rotación ECS.

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
| **Héroe local** | Delegado | Vincula el visual; `LocalHeroCharacterMotor` ejecuta y publica la pose |
| **Unidad/remoto NavMesh** | GameObject/NavMesh → ECS | `NavMeshPositionSyncSystem` copia la posición física; `EntityVisualSync` evita duplicar esa escritura |

La posición física no se interpola en ECS; la rotación pasa por `UnitRotationIntentComponent` y su sistema de resolución.

La animación tampoco forma ya parte de este puente. Para un héroe remoto, `RemoteHeroAnimationDriver` lee la velocidad confirmada del `NavMeshAgent` y el estado ECS de IA/combate, y escribe exclusivamente el `Animator`. No se añade a unidades ni tiene autoridad sobre pose o destino.

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
| `FormationComponent` / `SquadActiveFormationComponent` | Formación confirmada y cooldown | `FormationSystem` |
| `SquadDataComponent` | Datos del escuadrón (formationLibrary blob) | Setup/Spawn |
| `UnitTargetPositionComponent` | Posición deseada (slot de formación) | `FormationSystem`, `GridFormationUpdateSystem` |
| `UnitGridSlotComponent` | Coordenadas de grilla y offset world | `FormationSystem` |
| `UnitFormationStateComponent` | Estado de la unidad (Formed/Waiting/Moving) | `UnitFormationStateSystem` |
| `UnitStatsComponent` | Stats de la unidad (velocidad, etc.) | Setup/Spawn |
| `UnitMoveSpeedVariation` | Multiplicador individual de velocidad | Setup/Spawn |
| `UnitSpacingComponent` | Slot de spacing en formación | `FormationSystem` |
| `NavAgentComponent` | Estado del destino efectivo, fallos y autoridad NavMesh | `UnitNavMeshSystem` |
| `UnitRotationIntentComponent` | Propuesta priorizada de orientación | NavMesh/Follow; aplicada por `UnitRotationResolutionSystem` |
| `LocalTransform` | Pose publicada en ECS | `NavMeshPositionSyncSystem`, `UnitRotationResolutionSystem` |
| `HeroInputComponent` | Input del héroe (WASD, sprint, skills) | `HeroInputSystem` |
| `HeroMoveIntent` | Dirección y velocidad de movimiento | `HeroMovementSystem` |
| `HeroMotorStateComponent` | Velocidad y contacto físico confirmados | `LocalHeroCharacterMotor` |
| `UnitDestinationMarkerComponent` | Referencia al marcador visual | `DestinationMarkerSystem` |

---

## 12. Orden de Ejecución de Sistemas

Todos en `SimulationSystemGroup`. El orden se define por atributos `[UpdateAfter]` y `[UpdateBefore]`:

```
1.  HeroInputSystem → HeroMovementSystem
2.  EnemyDetectionSystem → SquadAISystem → CombatReactionSystem
3.  SquadControlSystem → OrderResolutionSystem → SquadOrderSystem → SquadFSMSystem
4.  UnitTargetingSystem
5.  SquadAnchorSystem → FormationSystem → GridFormationUpdateSystem
6.  UnitFormationStateSystem → UnitNavMeshSystem
7.  UnitFollowFormationSystem → UnitBodyblockSystem → NavMeshPositionSyncSystem
8.  UnitRotationResolutionSystem
9.  DestinationMarkerSystem / UnitAnimationSystem / visuales
```

Las dependencias críticas están declaradas de forma explícita: `UnitNavMeshSystem` espera a `UnitFormationStateSystem` y `UnitTargetingSystem`, y fuerza a `UnitFollowFormationSystem` a ejecutarse después. `UnitBodyblockSystem` corrige la pose antes de que `NavMeshPositionSyncSystem` la publique en ECS.
