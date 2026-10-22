# Pipeline de Movimiento de Tropas

Documento técnico que describe el flujo completo de movimiento de las tropas (squads/units), desde la captura de input del jugador hasta la representación visual en pantalla.

Actualizado: 2026-10-22. Verificado contra el código de `Assets/Scripts/Squads` (atributos `[UpdateAfter]`/`[UpdateBefore]` y componentes). El diseño, las decisiones y las medidas del movimiento están en `Docs/Design/Movimiento_Formaciones.md`; la tabla de parámetros, en su §3. Los valores de `DefaultMovementTuning.asset` no se repiten aquí: el asset está en Git LFS y no se pudo leer al verificar este documento.

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
UnitTargetingSystem        — asigna objetivo a cada unidad (si InCombat)
  ↓
SquadAnchorSystem          — única autoridad del ancla de formación
  ↓
FormationSystem            — disposición de la escuadra y ranura de cada unidad
  ↓
GridFormationUpdateSystem  — mantiene la posición de cada slot respecto al ancla
  ↓
UnitFormationStateSystem   — máquina de estados por unidad (Formed/Waiting/Moving)
  ↓
MeleeContactRingSystem     — reserva de posición en el anillo del objetivo (melé)
  ↓
UnitNavMeshSystem          — única autoridad de destinos y movimiento NavMesh
  ↓
UnitFollowFormationSystem  — velocidad (marcha/carrera) y orientación de formación
  ↓
UnitAvoidanceSystem        — evitación por velocidad entre aliados (ORCA)
  ↓
UnitBodyblockSystem        — empuje entre enemigos (cápsulas)
  ↓
UnitHitKnockbackSystem     — retroceso del golpe
  ↓
NavMeshPositionSyncSystem  — posición NavMesh GameObject → ECS
  ↓
UnitRotationResolutionSystem — resuelve la intención de rotación
  ↓
UnitAnimationSystem / UnitPostureSystem / DestinationMarkerSystem
```

Todos los sistemas pertenecen al `SimulationSystemGroup`. El orden exacto se define por atributos y está en la §12.

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

La orientación local la aplica `SamplePlayerAnimationController_ECS` sobre el visual durante `Update`. El motor publica en `LateUpdate` esa rotación final en `LocalTransform`, después de resolver el desplazamiento con `CharacterController`; la animación no mueve físicamente al héroe.

---

## 3. Captura de Input — `SquadControlSystem`

La lectura de teclado permanece aquí; `SquadPlayerCommandResolver` aplica la combinación de orden, formación y hurry sin depender del dispositivo. Una formación no disponible no cancela una orden válida emitida en el mismo fotograma.

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

Al presionar X, el sistema lanza un raycast contra el layer de terreno. Si falla, usa un plano fallback en Y=0. La posición resultante se guarda en `SquadPlayerOrderIntentComponent.holdPosition`.

### Componentes de salida

```csharp
public struct SquadInputComponent : IComponentData
{
    public bool hasNewOrder;
    public bool hurryToComander;
}
```

`SquadInputComponent` conserva flags transitorios. La orden y formación solicitadas por el jugador se publican en `SquadPlayerOrderIntentComponent`. La IA publica `SquadAIOrderIntentComponent` y `CombatReactionSystem` publica `SquadCombatReactionIntentComponent`; ningún productor escribe directamente el estado aplicado.

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
**Atributos:** `[UpdateInGroup(typeof(SimulationSystemGroup))]`, `[UpdateAfter(typeof(SquadOrderSystem))]`

- Único escritor de `FormationComponent` (formación, plantilla, espaciado, escala, semilla) y de cada `UnitGridSlotComponent`.
- Rehace la disposición cuando cambia la formación pedida (con cooldown `MovementTuningComponent.formationChangeCooldown`), cuando cambia la plantilla por bajas o cuando llega un `SquadFormationSpacingRequest`.
- Lee la formación solicitada desde `SquadResolvedOrderComponent`; un cambio de formación no reemite la orden de movimiento.
- Lee `SquadDataComponent.formationLibrary` (blob horneado desde `FormationDefinition`) y calcula slots con `FormationLayout` y `FormationPositionCalculator.TryBuildSlot()`.
- Escribe el `UnitTargetPositionComponent` inicial; después lo mantiene `GridFormationUpdateSystem`.

### GridFormationUpdateSystem

**Archivo:** `Assets/Scripts/Squads/Systems/GridFormationUpdate.System.cs`
**Atributos:** `[UpdateInGroup(typeof(SimulationSystemGroup))]`, `[UpdateAfter(typeof(FormationSystem))]`

- Mantiene `UnitTargetPositionComponent.position` girando el desplazamiento local de cada slot con el ancla.
- Omite la geometría de slots estables (revisión del ancla + clave de orden/estado, formación, slot y terreno) y remuestrea alturas al cambiar el terreno.

### Tipos y datos de formación

`FormationType`: Line, Dispersed, Testudo, Wedge, Column, Square. Cada formación es un `FormationDefinition` con espaciado propio, ruido, plantillas por soldados vivos (o `Scatter`) y tabla de posturas; se hornea en `FormationLibraryBlob` (`FormationDataBlob`: tipo, layout, espaciado, ruido, plantillas, posturas). Ver `Docs/Mechanics/Formations.md` y `Docs/Mechanics/FormationPostures.md`.

### FormationPositionCalculator (utilidad central)

**Archivo:** `Assets/Scripts/Squads/FormationPositionCalculator.cs`

| Método | Propósito |
|--------|-----------|
| `GetSquadCenter()` | Centro de la escuadra (héroe o centro de Hold Position) |
| `DesiredPosition()` | Convierte un desplazamiento local de slot en posición mundo según el estado de la escuadra |
| `TryBuildSlot()` | Construye un slot a partir de una plantilla del blob |
| `IsUnitInSlot()` | Check de distancia entre unidad y slot deseado |
| `calculateTerraindHeight()` | Altura del terreno en una posición |

---

## 7. Estados de Unidad — `UnitFormationStateSystem`

**Archivo:** `Assets/Scripts/Squads/Systems/UnitFormationState.System.cs`
**Atributos:** `[UpdateInGroup(typeof(SimulationSystemGroup))]`, `[UpdateAfter(typeof(GridFormationUpdateSystem))]`

### Estados

```csharp
public enum UnitFormationState { Formed, Waiting, Moving }

public struct UnitFormationStateComponent : IComponentData
{
    public UnitFormationState State;
    public float DelayTimer;
    public float DelayDuration;
    public float CalmTimer;
}
```

La transición es una función pura, `FormationArrival.Step` (`Assets/Scripts/Squads/FormationArrival.cs`), con histéresis (L-4 de `Movimiento_Formaciones.md`). Decide por el error de **cada unidad** respecto a su slot; no usa la unidad más lejana como gate global. Los umbrales salen de `MovementTuningComponent`, no son constantes del sistema:

| Parámetro | Uso |
|-----------|-----|
| `arrivalRadiusIn` | Se entra en `Formed` por debajo de este radio |
| `arrivalRadiusOut` | Se sale de `Formed` por encima de este radio (seguir al héroe) |
| `arrivalCalmTime` | Segundos dentro de `arrivalRadiusIn` con el ancla quieta antes de pasar a `Formed` |
| `holdReformRadius` | Radio de salida de `Formed` en Hold Position |
| `startRowDelay`, `startJitterDelay` | Retardo de arranque por fila y aleatorio (`FormationStartDelay.Compute`) |

### Transiciones

```
Formed → Waiting:   la unidad supera el radio de salida (arrivalRadiusOut, o holdReformRadius en Hold)
                    Se asigna un retardo de arranque

Waiting → Moving:   el retardo expira (DelayTimer >= DelayDuration)

Waiting → Formed:   la unidad vuelve dentro del radio antes de que expire el retardo
                    (en Hold basta con estar dentro de holdReformRadius; si no, calma)

Moving → Formed:    dentro de arrivalRadiusIn con el ancla quieta durante arrivalCalmTime
```

El estado del ancla se lee de `SquadAnchorMovingTag` (habilitable), no del estado del héroe. En Hold Position el sistema usa `holdReformRadius`. Con `UnitStartedMovingTag`/`UnitArrivedAtSlotTag` presentes publica los hitos (ver `Docs/Mechanics/FormationMilestoneSystem.md`).

---

## 8. Movimiento físico — `UnitNavMeshSystem` y seguimiento

**Archivo:** `Assets/Scripts/Squads/Systems/UnitNavMesh.System.cs`
**Atributos:** `[UpdateAfter(UnitFormationStateSystem)]`, `[UpdateAfter(UnitTargetingSystem)]`, `[UpdateBefore(UnitFollowFormationSystem)]`

### Destino

Es la única autoridad que llama `NavMeshAgent.SetDestination()`. El destino efectivo es el slot de formación o un punto de parada próximo al objetivo de combate (`MeleeReach.BrakingStopPoint`; en melé, la posición del anillo que reserva `MeleeContactRingSystem`). Las unidades `Waiting` sin persecución elegible conservan su espera; `HoldPosition` nunca autoriza persecución.

```
UnitTargetPositionComponent
  → proyección con NavMesh.SamplePosition
  → NavMeshAgent.SetDestination(destino efectivo)
  → UnitAvoidanceSystem ajusta la velocidad entre aliados
  → UnitBodyblockSystem aplica agent.Move(offset) entre enemigos
  → NavMeshPositionSyncSystem copia agent.transform.position a LocalTransform
```

### Velocidad — `UnitFollowFormationSystem`

**Atributos:** `[UpdateAfter(GridFormationUpdateSystem)]`; en la práctica corre después de `UnitNavMeshSystem`.

La velocidad base es la resuelta por la pila de stats (`UnitStatsComponent.speed`), multiplicada por `UnitMoveSpeedVariation.speedMultiplier` si el componente existe. Sobre ella actúa el engranaje `UnitMovementGearComponent` (Walk/Run, histéresis `runEnterDistance`/`runExitDistance`, `runSpeedMultiplier`); `hurryToComander` (doble C) o un objetivo de combate fuerzan Run. Para seguir un slot en movimiento, `followRecoveryTime`, `followMinSpeedRatio` y `walkCatchUpRatio` acotan el margen de recuperación. `navAgent.speed` sube y baja como mucho a la aceleración del agente (frena hasta `brakingFactor` veces más rápido). Detalle y medidas: `Movimiento_Formaciones.md` L-3, §12 y §15.

### Orientación

- `UnitNavMeshSystem` publica orientación de combate cuando el objetivo está próximo.
- `UnitFollowFormationSystem` publica la orientación Formed (rumbo de la formación × rumbo de la ranura).
- `UnitRotationResolutionSystem` aplica la intención ganadora; no hay una segunda ruta directa de rotación ECS.

### Evitación, bodyblock y retroceso

- `UnitAvoidanceSystem`: evitación por velocidad entre aliados (ORCA); enemigos no se esquivan.
- `UnitBodyblockSystem`: empuje entre enemigos con cápsulas (`Docs/Mechanics/BodyblockSystem.md`).
- `UnitHitKnockbackSystem`: retroceso del golpe.

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
- La unidad sale de `Formed` al superar `holdReformRadius`
- Las formaciones se recalculan respecto al `holdCenter` en vez del héroe

### Hurry to Commander (doble C)

- Toggle que activa/desactiva `SquadInputComponent.hurryToComander`
- Fuerza el engranaje Run de `UnitFollowFormationSystem`

### Cycling de formaciones (doble X)

- Avanza de forma circular por la librería de formaciones del escuadrón
- Sujeto al cooldown `formationChangeCooldown`

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
| `SquadInputComponent` | Flags transitorios de orden nueva y hurry | `SquadControlSystem` |
| `SquadStateComponent` | Estado actual del escuadrón, timer, orden | `SquadOrderSystem`, `SquadFSMSystem` |
| `SquadHoldPositionComponent` | Centro y formación de Hold Position | `SquadOrderSystem` |
| `SquadFormationAnchorComponent` | Posición, rotación y velocidad del ancla | `SquadAnchorSystem` |
| `FormationComponent` | Formación confirmada, plantilla, espaciado, escala, semilla | `FormationSystem` |
| `UnitOwnerComponent` | Referencia de la unidad a su squad y héroe | `SquadSpawningSystem` |
| `SquadDataComponent` | Datos del escuadrón (formationLibrary blob) | Setup/Spawn |
| `UnitTargetPositionComponent` | Posición deseada (slot de formación) | `FormationSystem` (inicial), `GridFormationUpdateSystem` |
| `UnitGridSlotComponent` | Ranura, posición local, fila, papel, rumbo | `FormationSystem` |
| `UnitFormationStateComponent` | Estado de la unidad (Formed/Waiting/Moving) | `UnitFormationStateSystem` |
| `UnitMovementGearComponent` | Engranaje Walk/Run | `UnitFollowFormationSystem` |
| `MeleeContactSlotComponent` | Posición reservada en el anillo del objetivo | `MeleeContactRingSystem` |
| `UnitStatsComponent` | Stats de la unidad (velocidad, etc.), derivados de la pila | `StatResolveSystem` |
| `UnitMoveSpeedVariation` | Multiplicador opcional de velocidad; el sistema usa 1 si falta | Sin productor en el spawn actual; `UnitFollowFormationSystem` lo consume si está presente |
| `MovementTuningComponent` | Singleton de ajuste del movimiento | Horneado por `SquadSpawnConfigAuthoring` desde `MovementTuningConfig` |
| `NavAgentComponent` | Estado del destino efectivo, fallos y autoridad NavMesh | `UnitNavMeshSystem` |
| `UnitRotationIntentComponent` | Propuesta priorizada de orientación | NavMesh/Follow; aplicada por `UnitRotationResolutionSystem` |
| `LocalTransform` | Pose publicada en ECS | `NavMeshPositionSyncSystem`, `UnitRotationResolutionSystem` |
| `HeroInputComponent` | Input del héroe (WASD, sprint, skills) | `HeroInputSystem` |
| `HeroMoveIntent` | Dirección y velocidad de movimiento | `HeroMovementSystem` |
| `HeroMotorStateComponent` | Velocidad y contacto físico confirmados | `LocalHeroCharacterMotor` |
| `UnitDestinationMarkerComponent` | Referencia al marcador visual | `DestinationMarkerSystem` |

---

## 12. Orden de Ejecución de Sistemas

Todos en `SimulationSystemGroup`. Cadena declarada por `[UpdateAfter]`/`[UpdateBefore]` (verificada en el código):

```
1.  HeroInputSystem → HeroMovementSystem
2.  EnemyDetectionSystem → SquadAISystem (Before SquadFSMSystem) → CombatReactionSystem
3.  SquadControlSystem (Before SquadOrderSystem, OrderResolutionSystem, FormationSystem)
    CombatReactionSystem → OrderResolutionSystem → SquadOrderSystem → SquadFSMSystem
4.  UnitTargetingSystem (After SquadAISystem, SquadFSMSystem, UnitDeathSystem)
5.  SquadAnchorSystem (After UnitTargetingSystem, Before FormationSystem)
    FormationSystem (After SquadOrderSystem) → GridFormationUpdateSystem
6.  UnitFormationStateSystem → UnitPostureSystem
7.  MeleeContactRingSystem (After UnitTargetingSystem, Before UnitNavMeshSystem)
    UnitNavMeshSystem (After UnitFormationStateSystem y UnitTargetingSystem)
8.  UnitFollowFormationSystem → UnitAvoidanceSystem → UnitBodyblockSystem → UnitHitKnockbackSystem
9.  NavMeshPositionSyncSystem (After UnitNavMeshSystem, Before UnitRotationResolutionSystem)
10. UnitRotationResolutionSystem → UnitAnimationSystem / DestinationMarkerSystem / visuales
```

Las dependencias críticas están declaradas de forma explícita: `UnitNavMeshSystem` espera a `UnitFormationStateSystem` y `UnitTargetingSystem` y fuerza a `UnitFollowFormationSystem` a ejecutarse después. `UnitBodyblockSystem` y `UnitHitKnockbackSystem` corrigen la pose antes de que `NavMeshPositionSyncSystem` la publique en ECS.

Un ciclo de `[UpdateBefore]`/`[UpdateAfter]` dentro del grupo congela el editor al entrar en Play (Entities no lo reporta); `SystemOrderTests` lo comprueba.
