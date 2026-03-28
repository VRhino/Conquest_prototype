# Squad & Unit Movement/Combat — Análisis de Acoplamiento y Plan de Refactor

> Fecha: 2026-03-28
> Scope: todos los sistemas que intervienen en movimiento y combate de unidades de squad

---

## 1. Diagnóstico — Nivel de acoplamiento actual: 7/10

### Nodos críticos de acoplamiento

```
SquadDataComponent  ←── 11 sistemas lo leen  (GOD OBJECT)
SquadStateComponent ←── mezcla FSM + órdenes + combate + formación  (GOD OBJECT)
        ↓
isInCombat escrito en DOS sitios:
  SquadAIComponent.isInCombat       (escrito por SquadAISystem)
  SquadStateComponent.isInCombat    (escrito por el mismo SquadAISystem, misma línea)
        ↓
Rotación de unidad escrita por DOS sistemas (last-write-wins, frágil):
  UnitNavMesh.System      ──┐
                            ├── agent.transform.rotation  ← VIOLACIÓN ECS directa
  UnitFollowFormation.System─┘
        ↓
UnitFollowFormationSystem y UnitFormationStateSystem
  requieren hero entity válido → si el héroe no existe, el squad se congela
```

### Tabla de problemas

| Problema | Archivo(s) | Impacto |
|----------|-----------|---------|
| `SquadDataComponent` god object (50+ campos) | `SquadData.Component.cs` | Cambiar un stat requiere coordinar 11 sistemas |
| `isInCombat` duplicado en 2 componentes | `SquadAI.System.cs:107-108` | Divergencia silenciosa de estado posible |
| Formación en 3 sitios distintos | `SquadState`, `Formation`, `SquadStatus` components | Sin fuente de verdad clara |
| `SquadFSMStateComponent` duplica `SquadStateComponent.currentState` | `SquadFSMState.Component.cs` | Estado redundante |
| Rotación: 2 escritores, orden importa | `UnitNavMesh.System.cs:181`, `UnitFollowFormation.System.cs:173` | Si cambia el orden de ejecución, visual de combate roto |
| Squads acoplados al hero entity | `UnitFollowFormation.System.cs:53-58`, `UnitFormationState.System.cs` | Imposible unit-testear squads sin héroe |
| `UnitNavMesh` escribe `agent.transform.rotation` directamente | `UnitNavMesh.System.cs:181` | Viola barrera ECS/GO, bypasea `EntityVisualSync` |
| Mínimo de combate hardcoded (`3f`) | `SquadFSM.System.cs` | No parametrizable, salida por timer no por estado real |
| Sin sistema de prioridad de órdenes | `SquadFSM.System.cs` (if/else implícito) | Órdenes de movimiento pueden interrumpir combate activo |

### Pipeline actual (con dependencias implícitas)

```
SquadControlSystem          → SquadInputComponent
  → SquadOrderSystem        → SquadStateComponent.currentOrder
    → SquadAISystem         → SquadStateComponent.isInCombat  ← escribe estado de combate aquí
      → SquadFSMSystem      → SquadStateComponent.currentState (lee Y escribe el mismo componente)
        → FormationSystem
          → GridFormationUpdateSystem
            → UnitFormationStateSystem
              → UnitNavMeshSystem         ← escribe agent.transform.rotation directamente
                → UnitFollowFormationSystem ← también escribe agent.transform.rotation
```

### Magic numbers hardcoded

| Valor | Sistema | Propósito |
|-------|---------|-----------|
| `3f` | `SquadFSM.System.cs` | Duración mínima en estado InCombat |
| `0.75f` | `UnitNavMesh.System.cs` | StopDistanceFactor: parar al 75% del rango de ataque |
| `3.5f` | `UnitNavMesh.System.cs` | EngagementRange: distancia de rotación manual |
| `6f` | `UnitNavMesh.System.cs` | leashDistance por defecto |
| `100f` | `UnitFormationState.System.cs` | formationRadiusSq: radio de cohesión de formación |
| `0.04f` | `UnitFormationState.System.cs` | slotThresholdSq: detección "en slot" |
| `1.0f` | `UnitFormationState.System.cs` | holdPositionThresholdSq |
| `0.5f` | `SquadControl.System.cs` | DOUBLE_CLICK_THRESHOLD |

---

## 2. Arquitectura propuesta

### Principio central: Intent → Resolution → Resolved Order

Todos los "inputs" (jugador, AI, reacción de combate) escriben un **intent** con peso de prioridad. Un único sistema de resolución elige el ganador y escribe `SquadResolvedOrderComponent`, que aguas abajo **solo se lee, nunca se escribe**.

### Nuevo pipeline

```
[FASE A — Intent Writers, independientes entre sí, pueden correr en paralelo]
SquadControlSystem          →  SquadPlayerOrderIntentComponent
SquadAISystem               →  SquadAIOrderIntentComponent + SquadCombatStateComponent
CombatReactionSystem (new)  →  SquadCombatReactionIntentComponent

[FASE B — Resolución única]
OrderResolutionSystem (new) →  SquadResolvedOrderComponent  (único escritor)

[FASE C — FSM consume el resultado]
SquadFSMSystem              →  lee SquadResolvedOrderComponent + SquadCombatStateComponent
                               escribe SquadFSMComponent  (único escritor)

[FASE D — Geometría de formación]
FormationSystem + GridFormationUpdateSystem → leen SquadActiveFormationComponent

[FASE E — Estado y movimiento de unidades]
UnitFormationStateSystem    →  lee SquadFSMComponent (sin dependencia del hero entity)
UnitNavMeshSystem           →  agent.SetDestination() + escribe UnitRotationIntentComponent(priority=10)
UnitFollowFormationSystem   →  escribe UnitRotationIntentComponent(priority=5)
UnitRotationResolution(new) →  lee intent de mayor prioridad → escribe LocalTransform.Rotation (único escritor)

[FASE F — Bridge ECS/GO]
NavMeshPositionSyncSystem(new) →  agent.transform.position → LocalTransform.Position (único bridge)
```

### Reglas de prioridad en OrderResolutionSystem

**Squad del local hero** (`IsLocalPlayer` en el héroe dueño):
1. Orden insistente del jugador (N pulsaciones de la misma orden en ventana T) → `heroOrdenCooldown` activo
2. Si `heroOrdenCooldown` activo → unidades ignoran combate y ejecutan movimiento
3. Si `heroOrdenCooldown` expira o llega orden de combate → cancelar, volver a reactivo
4. Sin insistencia: combate > AI > última orden del jugador

**Squad de remote hero** (sin `IsLocalPlayer`):
1. Combate activo (`reactToEnemy == true`) → bloqueo total de movimiento
2. Combate termina solo cuando: enemigos muertos, unidades muertas, o enemigos fuera de rango
3. Órdenes de movimiento del AI ignoradas mientras haya combate activo

---

## 3. Funcionalidades a implementar (Sprint 6)

Estas funcionalidades no existen en el código actual. Se implementan en Sprint 6 aprovechando la arquitectura Intent/Resolution que ese sprint introduce. La spec aquí es la fuente de verdad del comportamiento esperado.

---

### Funcionalidad 1 — Bloqueo de combate para squads de remote heroes

**Comportamiento esperado:**
- Cuando un squad de remote hero está en movimiento y cualquier unidad detecta un enemigo en rango **O** recibe daño → el squad entra en modo combate y **bloquea completamente todas las órdenes de movimiento** del AI (`HeroAIExecutionSystem`)
- El bloqueo se mantiene hasta que se cumpla **una** de estas condiciones de salida:
  1. Todos los enemigos en rango de detección son eliminados
  2. Todas las unidades del squad mueren
  3. Los enemigos se alejan completamente fuera del rango de detección

**Implementación:**
- `OrderResolutionSystem` descarta cualquier `SquadAIOrderIntentComponent` con orden de movimiento mientras `SquadCombatStateComponent.isInCombat == true` en un squad de remote hero
- La condición de salida evalúa `DetectedEnemy` buffer vacío (no un timer)
- Parámetro configurable: `detectionRange` (ya existe en `SquadDefinitionComponent`)

---

### Funcionalidad 2 — heroOrdenCooldown para squads del local hero

**Comportamiento esperado:**
- El jugador emite la misma orden de movimiento **N veces** dentro de una ventana de tiempo **T** → se activa el modo `heroOrdenCooldown`
- Durante `heroOrdenCooldown` activo:
  - Las unidades **ignoran** detección de enemigos y reacciones de combate
  - Ejecutan el movimiento ordenado sin interrupciones
- `heroOrdenCooldown` se desactiva cuando:
  - Expira el timer `heroOrdenCooldownDuration`
  - **O** el jugador emite cualquier orden de combate explícita (V=Attack, acción defensiva)
- Para reactivar después de que expire: el jugador debe volver a emitir N órdenes en ventana T

**Parámetros configurables** (a añadir al config component del squad):

| Parámetro | Tipo | Default sugerido | Descripción |
|-----------|------|-----------------|-------------|
| `heroOrdenInsistenceCount` | int | 2–3 | Número de pulsaciones para activar |
| `heroOrdenInsistenceWindow` | float | ~0.8s | Ventana de tiempo entre pulsaciones |
| `heroOrdenCooldownDuration` | float | ~3–5s | Duración del modo movimiento forzado |

**Estado necesario en `SquadPlayerOrderIntentComponent`:**
```
insistenceCount:         int    // se incrementa con cada pulsación de la misma orden dentro de la ventana
insistenceTimer:         float  // tiempo transcurrido desde la primera pulsación de la secuencia
heroOrdenCooldownActive: bool   // true = modo movimiento forzado activo
heroOrdenCooldownTimer:  float  // tiempo restante del cooldown
```

---

### Tabla de comportamiento: local hero vs remote hero

| Situación | Squad local hero | Squad remote hero |
|-----------|-----------------|-------------------|
| Movimiento + enemigo entra en rango | Entra en combate (reactivo) | Entra en combate, **bloquea movimiento** |
| Movimiento + unidad recibe daño | Entra en combate (reactivo) | Entra en combate, **bloquea movimiento** |
| `heroOrdenCooldown` activo + enemigo en rango | Ignora combate, sigue moviéndose | N/A — no existe en remote |
| Orden de combate explícita durante cooldown | Cancela cooldown, entra en combate | Siempre combate si hay enemigos |
| Condición de salida de combate | Timer mínimo + `DetectedEnemy` vacío | Solo `DetectedEnemy` vacío o unidades muertas |
| Puede ignorar combate activo | Sí, con heroOrdenCooldown activo | **Nunca** |

---

## 4. Descomposición de componentes

### `SquadDataComponent` → 4 componentes enfocados

```csharp
SquadDefinitionComponent        // squadType, unitCount, leadershipCost, prefab, leashDistance,
                                 // detectionRange, behaviorProfile, formationLibrary (BlobRef)
                                 // Lectores: ~4 sistemas

SquadCombatStatsComponent        // damage (blunt/slash/pierce), defense, penetration,
                                 // attackRange, attackInterval, strikeWindow, kineticMultiplier
                                 // Lectores: ~3 sistemas de combate

SquadMobilityComponent           // baseSpeed, mass, weight
                                 // Lectores: ~2 sistemas de navegación

SquadRangedStatsComponent        // isRangedUnit, range, accuracy, fireRate, ammoCapacity
                                 // Lectores: ~1 sistema de combate a distancia
```

### `SquadStateComponent` → 4 componentes enfocados

```csharp
SquadFSMComponent                // currentState: SquadFSMState, stateTimer: float
                                 // Dueño único: SquadFSMSystem

SquadCombatStateComponent        // isInCombat: bool, engagedTarget: Entity
                                 // Dueño único: SquadAISystem
                                 // (elimina el duplicado entre SquadAI y SquadState)

SquadActiveFormationComponent    // currentFormation: FormationType, formationChangeCooldown: float
                                 // Dueño único: FormationResolutionSystem
                                 // (elimina los 3 sitios donde se guarda la formación activa)

SquadLifecycleComponent          // retreatTriggered: bool, lastOwnerAlive: bool
                                 // Dueño único: RetreatLogicSystem
```

### Nuevos componentes de intent

```csharp
SquadPlayerOrderIntentComponent  // orderType, desiredFormation, holdPosition,
                                 // insistenceCount: int, insistenceWindow: float
                                 // Escrito por: SquadControlSystem

SquadAIOrderIntentComponent      // tacticalIntent: TacticalIntent, suggestedOrder, targetEntity
                                 // Escrito por: SquadAISystem

SquadCombatReactionIntentComponent // reactToEnemy: bool, reactTarget: Entity
                                   // Escrito por: CombatReactionSystem (nuevo)

SquadResolvedOrderComponent      // order, formation, holdPosition, targetEntity,
                                 // source: OrderSource (Player | AI | CombatReaction)
                                 // Escrito por: OrderResolutionSystem ÚNICAMENTE
```

### Rotación de unidades

```csharp
UnitRotationIntentComponent      // targetRotation: quaternion, priority: int,
                                 // source: RotationSource (Combat=10 | Formation=5 | NavMesh=0)
                                 // Escrito por: UnitNavMeshSystem y UnitFollowFormationSystem
                                 // Resuelto por: UnitRotationResolutionSystem (único escritor de LocalTransform.Rotation)
```

### Desacoplamiento del hero entity

Reemplazar referencias directas al héroe en sistemas de formación por:

```csharp
// Ya existe — SquadAnchorSystem lo mantiene actualizado cada frame
SquadFormationAnchorComponent    // position: float3, rotation: quaternion

// Nuevo — enableable component en el squad entity
SquadAnchorMovingTag             // IEnableableComponent
                                 // SquadAnchorSystem lo habilita/deshabilita según si el anchor se mueve
                                 // UnitFormationStateSystem lo lee en lugar de HeroStateComponent
```

Resultado: `UnitFollowFormationSystem` y `UnitFormationStateSystem` no requieren el hero entity para funcionar.

### Bridge ECS/NavMesh

```csharp
// Sistema nuevo, corre UpdateAfter(UnitNavMeshSystem)
NavMeshPositionSyncSystem
  // Lee: agent.transform.position
  // Escribe: LocalTransform.Position
  // Lee: agent.transform.rotation
  // Escribe: UnitRotationIntentComponent(priority=NavMesh=0)
  // NO escribe LocalTransform.Rotation (eso es responsabilidad de UnitRotationResolutionSystem)
```

`EntityVisualSync` deja de escribir posición para unidades con `NavAgentComponent` activo (flag en el componente). Para unidades sin NavMesh, el sync ECS→GO sigue igual.

---

## 5. Comparativa antes/después

| Métrica | Actual | Propuesta |
|---------|--------|-----------|
| Escritores de `isInCombat` | 2 (mismo sistema, 2 componentes) | 1 (`SquadCombatStateComponent`, solo `SquadAISystem`) |
| Fuentes de verdad de formación activa | 3 | 1 (`SquadActiveFormationComponent`) |
| Fuentes de verdad de FSM state | 2 | 1 (`SquadFSMComponent`) |
| Lectores máximos de un componente | 11 (`SquadDataComponent`) | 4 (`SquadDefinitionComponent`) |
| Escritores de rotación de unidad | 2 (last-write-wins) | 1 (`UnitRotationResolutionSystem`) |
| Hero entity requerido en sistemas de formación | Sí (crash si null) | No (`SquadFormationAnchorComponent` + `SquadAnchorMovingTag`) |
| Barrera ECS/GO para NavMesh | Violada en `UnitNavMesh.System.cs:181` | Un único bridge (`NavMeshPositionSyncSystem`) |
| Sistema de prioridad de órdenes | Implícito (if/else en FSM) | Explícito, parametrizable, por `OrderResolutionSystem` |
| `heroOrdenCooldown` (local hero override) | No existe | Implementado en `SquadPlayerOrderIntentComponent` |
| Bloqueo de movimiento en combate (remote) | No existe | Regla en `OrderResolutionSystem` |

---

## 6. Plan de migración por sprints

Orden de menor a mayor riesgo. Cada sprint es independiente y verificable.

---

### Sprint 1 — Limpieza segura (zero behavior change)
**Riesgo: Bajo** — cambios de una línea, sin nueva lógica

- [ ] Eliminar `SquadFSMStateComponent` — es redundante con `SquadStateComponent.currentState`. Actualizar los 2 sistemas que lo leen para leer `SquadStateComponent.currentState`.
- [ ] Consolidar `isInCombat` — eliminar `SquadStateComponent.isInCombat`, hacer que `SquadFSMSystem` lea `SquadAIComponent.isInCombat`. Cambio de una línea en `SquadFSM.System.cs:70`.
- [ ] Unificar formación activa en `FormationComponent.currentFormation` — eliminar `SquadStateComponent.currentFormation` y `SquadStatusComponent.activeFormationId`. Actualizar `GridFormationUpdateSystem:36`.

**Archivos a tocar**: `SquadFSM.System.cs`, `GridFormationUpdateSystem.cs`, los 2 lectores de `SquadFSMStateComponent`

---

### Sprint 2 — Nuevos componentes en paralelo (aditivo, sin eliminar nada)
**Riesgo: Bajo** — los componentes viejos siguen existiendo como fallback

- [ ] Crear `SquadCombatStateComponent` — `SquadAISystem` escribe en él además de en los campos viejos.
- [ ] Crear `SquadFSMComponent` — `SquadFSMSystem` escribe en él además de en `SquadStateComponent`.
- [ ] Crear `SquadActiveFormationComponent` — `SquadOrderSystem` escribe en él además de en `FormationComponent`.
- [ ] Verificar que ambas rutas producen el mismo resultado durante 1 sprint antes de eliminar las viejas.

---

### Sprint 3 — Desacoplamiento del hero entity
**Riesgo: Medio** — validar orden de ejecución del `SquadAnchorSystem`

- [ ] Añadir `SquadAnchorMovingTag` (IEnableableComponent) a `SquadAnchorSystem` — habilitar cuando el anchor se mueve, deshabilitar cuando está quieto.
- [ ] Migrar `UnitFormationStateSystem` — reemplazar lectura de `HeroStateComponent` por `SquadAnchorMovingTag`.
- [ ] Migrar `UnitFollowFormationSystem` — reemplazar lookup del hero entity por `SquadFormationAnchorComponent.position`.
- [ ] Verificar que `SquadAnchorSystem` corre **antes** de ambos sistemas de formación.

---

### Sprint 4 — Autoridad única de rotación
**Riesgo: Medio** — probar primero en un tipo de unidad aislado

- [ ] Crear `UnitRotationIntentComponent`.
- [ ] `UnitNavMeshSystem` — reemplazar `agent.transform.rotation = ...` por escribir `UnitRotationIntentComponent(priority=Combat=10)`.
- [ ] `UnitFollowFormationSystem` — reemplazar write de `navAgent.transform.rotation` por escribir `UnitRotationIntentComponent(priority=Formation=5)`.
- [ ] Crear `UnitRotationResolutionSystem` — lee intent de mayor prioridad, escribe `LocalTransform.Rotation` y setea `agent.transform.rotation` en UN solo lugar. Setea `agent.updateRotation = false` para todas las unidades.
- [ ] Eliminar writes de rotación en los dos sistemas anteriores.

---

### Sprint 5 — Bridge ECS/NavMesh correcto
**Riesgo: Medio-Alto** — toca la barrera GO/ECS, probar exhaustivamente

- [ ] Añadir flag `syncPositionFromNavMesh: bool` a `NavAgentComponent`.
- [ ] Crear `NavMeshPositionSyncSystem` — corre `UpdateAfter(UnitNavMeshSystem)`, lee `agent.transform.position`, escribe `LocalTransform.Position` solo si `syncPositionFromNavMesh == true`.
- [ ] En `EntityVisualSync` — no escribir posición GO si la entidad tiene `NavAgentComponent` con `syncPositionFromNavMesh == true`.
- [ ] Verificar que `HeroAIExecutionSystem` (remote heroes) también respeta este bridge.

---

### Sprint 6 — Intent/Resolution layer completo
**Riesgo: Alto** — nueva lógica de prioridad, afecta comportamiento de juego

- [ ] Crear `SquadPlayerOrderIntentComponent`, `SquadAIOrderIntentComponent`, `SquadCombatReactionIntentComponent`.
- [ ] Separar lógica de detección de reacción de combate de `SquadAISystem` → nuevo `CombatReactionSystem`.
- [ ] Crear `OrderResolutionSystem` con las reglas de prioridad (local vs remote hero descritas en §2).
- [ ] Implementar `insistenceCount` + `insistenceWindow` en `SquadControlSystem` para el `heroOrdenCooldown`.
- [ ] Migrar `SquadFSMSystem` para leer `SquadResolvedOrderComponent` en lugar de `SquadStateComponent.currentOrder`.
- [ ] Eliminar path viejo de `SquadInputComponent.hasNewOrder` en `SquadOrderSystem`.
- [ ] **Esto resuelve BUG-006, BUG-007, BUG-008 y BUG-009** de `buggedFunctionalities.md`.

---

### Sprint 7 — Descomposición de `SquadDataComponent`
**Riesgo: Alto** — 11 sistemas coordinados, migrar uno por vez

- [ ] Crear los 4 componentes nuevos (`SquadDefinitionComponent`, `SquadCombatStatsComponent`, `SquadMobilityComponent`, `SquadRangedStatsComponent`).
- [ ] Migrar `SquadRangedStatsComponent` primero (menos lectores).
- [ ] Migrar `SquadMobilityComponent` (solo sistemas de navegación).
- [ ] Migrar `SquadCombatStatsComponent` (sistemas de combate).
- [ ] Migrar `SquadDefinitionComponent` último (más lectores, incluye BlobRef de formaciones — validar lifecycle de blobs).
- [ ] Eliminar campos de `SquadDataComponent` a medida que los lectores son migrados.
- [ ] Eliminar `SquadDataComponent` cuando esté vacío.

---

## 7. Archivos críticos de referencia

| Archivo | Por qué es crítico |
|---------|-------------------|
| `Assets/Scripts/Squads/SquadData.Component.cs` | God object principal — Sprint 7 |
| `Assets/Scripts/Squads/SquadState.Component.cs` | God object de estado — Sprints 1-2 |
| `Assets/Scripts/Squads/Systems/SquadAI.System.cs` | Escribe isInCombat duplicado — Sprint 1 |
| `Assets/Scripts/Squads/Systems/SquadFSM.System.cs` | FSM sin prioridad de órdenes — Sprints 1, 6 |
| `Assets/Scripts/Squads/Systems/UnitNavMesh.System.cs` | Violación ECS rotación + posición — Sprints 4, 5 |
| `Assets/Scripts/Squads/Systems/UnitFollowFormation.System.cs` | Segundo escritor de rotación — Sprint 4 |
| `Assets/Scripts/Squads/Systems/UnitFormationState.System.cs` | Acoplado al hero entity — Sprint 3 |
| `Assets/Scripts/Squads/Systems/SquadControl.System.cs` | Necesita insistenceCount — Sprint 6 |
| `Assets/Scripts/Visual/EntityVisualSync.cs` | Bridge GO/ECS a modificar — Sprint 5 |
| `Assets/Scripts/Hero/AI/Systems/HeroAIExecution.System.cs` | Escribe SquadInputComponent ignorando combate — Sprint 6 |
