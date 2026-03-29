# Squad & Unit Movement/Combat — Análisis de Acoplamiento y Plan de Refactor

> Fecha: 2026-03-28
> Scope: todos los sistemas que intervienen en movimiento y combate de unidades de squad
> Estado: Plan verificado contra código — todas las claims validadas con líneas exactas

---

## 1. Diagnóstico — Nivel de acoplamiento actual: 7/10

### Nodos críticos de acoplamiento

```
SquadDataComponent  ←── 11 sistemas lo leen  (GOD OBJECT, 37 campos)
SquadStateComponent ←── mezcla FSM + órdenes + combate + formación  (GOD OBJECT, 10 campos)
        ↓
isInCombat escrito en DOS sitios:
  SquadAIComponent.isInCombat       (escrito por SquadAISystem, línea 113)
  SquadStateComponent.isInCombat    (escrito por el mismo SquadAISystem, línea 114)
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

| Problema | Archivo(s) | Línea(s) | Impacto |
|----------|-----------|----------|---------|
| `SquadDataComponent` god object (37 campos) | `SquadData.Component.cs` | 10-65 | Cambiar un stat requiere coordinar 11 sistemas |
| `isInCombat` duplicado en 2 componentes | `SquadAI.System.cs` | 113-114 | Divergencia silenciosa de estado posible |
| Formación en 3 sitios distintos | `SquadState`, `Formation`, `SquadStatus` components | — | Sin fuente de verdad clara |
| `SquadFSMStateComponent` duplica `SquadStateComponent.currentState` | `SquadFSMState.Component.cs` | 10 | Estado redundante |
| Rotación: 2 escritores, orden importa | `UnitNavMesh.System.cs`, `UnitFollowFormation.System.cs` | 183, 179 | Si cambia el orden de ejecución, visual de combate roto |
| Squads acoplados al hero entity | `UnitFollowFormation.System.cs`, `UnitFormationState.System.cs` | 59, 33 | Imposible unit-testear squads sin héroe |
| `UnitNavMesh` escribe `agent.transform.rotation` directamente | `UnitNavMesh.System.cs` | 183 | Viola barrera ECS/GO, bypasea `EntityVisualSync` |
| Mínimo de combate hardcoded (`3f`) | `SquadFSM.System.cs` | 96 | No parametrizable, salida por timer no por estado real |
| Sin sistema de prioridad de órdenes | `SquadFSM.System.cs` (if/else implícito) | 72-88 | Órdenes de movimiento pueden interrumpir combate activo |

### Inventario de componentes actual

| Componente | Archivo | Campos | Rol |
|-----------|---------|--------|-----|
| SquadDataComponent | `SquadData.Component.cs` | 37 | God object — stats, definición, movilidad, ranged |
| SquadStateComponent | `SquadState.Component.cs` | 10 | God object — FSM + orders + combat + formation |
| SquadInputComponent | `SquadInput.Component.cs` | 5 | Input del jugador/AI |
| SquadStatusComponent | `SquadStatus.Component.cs` | 4 | Estado visual + activeFormationId (duplicado) |
| SquadAIComponent | `SquadAI.Component.cs` | 3 | AI intent + isInCombat (duplicado) |
| SquadFormationAnchorComponent | `SquadFormationAnchor.Component.cs` | 2 | Posición/rotación del anchor (YA EXISTE) |
| FormationComponent | `Formation.Component.cs` | 1 | currentFormation (duplicado en SquadState) |
| SquadFSMStateComponent | `SquadFSMState.Component.cs` | 1 | currentState (duplicado de SquadState) |
| NavAgentComponent | `NavAgent.Component.cs` | 0 | Tag component |

### Pipeline actual (con dependencias implícitas)

```
SquadControlSystem          → SquadInputComponent
  [UpdateBefore(SquadOrderSystem, FormationSystem)]
  → SquadOrderSystem        → SquadStateComponent.currentOrder
    → SquadAISystem         → SquadStateComponent.isInCombat  ← escribe estado de combate aquí
      [UpdateBefore(SquadFSMSystem)]
      → SquadFSMSystem      → SquadStateComponent.currentState (lee Y escribe el mismo componente)
        [UpdateAfter(SquadOrderSystem)]
        → FormationSystem
          → GridFormationUpdateSystem
            [UpdateAfter(FormationSystem)]
            → UnitFormationStateSystem
              [UpdateAfter(GridFormationUpdateSystem)]
              → UnitNavMeshSystem         ← escribe agent.transform.rotation (línea 183)
                [UpdateAfter(UnitFormationStateSystem), UpdateBefore(UnitFollowFormationSystem)]
                → UnitFollowFormationSystem ← también escribe agent.transform.rotation (línea 179)
                  [UpdateAfter(GridFormationUpdateSystem)]

SquadAnchorSystem [UpdateBefore(FormationSystem)] → SquadFormationAnchorComponent

HeroAIExecutionSystem [UpdateAfter(HeroAIRusher/Balanced)] → SquadInputComponent (líneas 109-113)
  ← NO chequea estado de combate antes de emitir órdenes
```

### Magic numbers hardcoded

| Valor | Sistema | Línea | Propósito |
|-------|---------|-------|-----------|
| `3f` | `SquadFSM.System.cs` | 96 | Duración mínima en estado InCombat |
| `0.75f` | `UnitNavMesh.System.cs` | 35 | StopDistanceFactor: parar al 75% del rango de ataque |
| `3.5f` | `UnitNavMesh.System.cs` | 38 | EngagementRange: distancia de rotación manual |
| `6f` | `UnitNavMesh.System.cs` | 89 | leashDistance por defecto (fallback) |
| `100f` | `UnitFormationState.System.cs` | 20 | formationRadiusSq: radio de cohesión de formación |
| `0.04f` | `UnitFormationState.System.cs` | 21 | slotThresholdSq: detección "en slot" |
| `1.0f` | `UnitFormationState.System.cs` | 80 | holdPositionThresholdSq |
| `0.5f` | `SquadControl.System.cs` | 22 | DOUBLE_CLICK_THRESHOLD |

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
| `heroOrdenInsistenceCount` | int | 3 | Número de pulsaciones para activar |
| `heroOrdenInsistenceWindow` | float | 0.8s | Ventana de tiempo entre pulsaciones |
| `heroOrdenCooldownDuration` | float | 4s | Duración del modo movimiento forzado |

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

### `SquadDataComponent` (37 campos) → 4 componentes enfocados

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

### `SquadStateComponent` (10 campos) → 4 componentes enfocados

```csharp
SquadFSMComponent                // currentState: SquadFSMState, stateTimer: float
                                 // Dueño único: SquadFSMSystem
                                 // (absorbe SquadFSMStateComponent redundante)

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
                                 // insistenceCount: int, insistenceTimer: float,
                                 // heroOrdenCooldownActive: bool, heroOrdenCooldownTimer: float
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
                                 // Resuelto por: UnitRotationResolutionSystem (único escritor de rotation)
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

`EntityVisualSync` deja de escribir posición para unidades con `NavAgentComponent.syncPositionFromNavMesh == true`. Para unidades sin NavMesh, el sync ECS→GO sigue igual.

---

## 5. Comparativa antes/después

| Métrica | Actual | Propuesta |
|---------|--------|-----------|
| Escritores de `isInCombat` | 2 (mismo sistema, 2 componentes) | 1 (`SquadCombatStateComponent`, solo `SquadAISystem`) |
| Fuentes de verdad de formación activa | 3 | 1 (`SquadActiveFormationComponent`) |
| Fuentes de verdad de FSM state | 2 | 1 (`SquadFSMComponent`) |
| Lectores máximos de un componente | 11 (`SquadDataComponent`) | 4 (`SquadDefinitionComponent`) |
| Escritores de rotación de unidad | 2 (last-write-wins) | 1 (`UnitRotationResolutionSystem`) |
| Hero entity requerido en sistemas de formación | Sí (skip si null) | No (`SquadFormationAnchorComponent` + `SquadAnchorMovingTag`) |
| Barrera ECS/GO para NavMesh | Violada en `UnitNavMesh.System.cs:183` | Un único bridge (`NavMeshPositionSyncSystem`) |
| Sistema de prioridad de órdenes | Implícito (if/else en FSM, líneas 72-88) | Explícito, parametrizable, por `OrderResolutionSystem` |
| `heroOrdenCooldown` (local hero override) | No existe | Implementado en `SquadPlayerOrderIntentComponent` |
| Bloqueo de movimiento en combate (remote) | No existe | Regla en `OrderResolutionSystem` |

---

## 6. Plan de migración por sprints — Ejecución exhaustiva

Orden de menor a mayor riesgo. Cada sprint es independiente y verificable.

### Grafo de dependencias entre sprints

```
Sprint 1 (cleanup)     ──→ Sprint 2 (dual-write) ──→ Sprint 6 (intent/resolution)
                                                   ──→ Sprint 7 (SquadData decomp)
Sprint 3 (hero decouple) ──→ (independiente, puede ir en paralelo con 4)
Sprint 4 (rotation)      ──→ Sprint 5 (NavMesh bridge)
```

---

### Sprint 1 — Limpieza segura (zero behavior change)
**Riesgo: Bajo** | **Objetivo: Eliminar duplicaciones obvias sin cambiar comportamiento**

#### Tarea 1.1 — Eliminar SquadFSMStateComponent [S]
- **Archivo a modificar**: `Assets/Scripts/Squads/SquadFSMState.Component.cs`
- **Archivos dependientes**: Buscar todos los lectores de `SquadFSMStateComponent` (2 sistemas) y migrarlos a leer `SquadStateComponent.currentState`
- **Acción**: Reemplazar lecturas de `SquadFSMStateComponent` → `SquadStateComponent.currentState`. Luego eliminar el archivo del componente.
- **Verificación**: Compilar. Jugar BattleScene, verificar que las transiciones de estado de squad (Idle → FollowingHero → HoldingPosition → InCombat) funcionan idéntico.

#### Tarea 1.2 — Consolidar isInCombat [S]
- **Archivo a modificar**: `Assets/Scripts/Squads/Systems/SquadAI.System.cs` (línea 114)
- **Acción**: Eliminar la escritura a `SquadStateComponent.isInCombat` (línea 114). Solo mantener `SquadAIComponent.isInCombat` (línea 113).
- **Archivo a modificar**: `Assets/Scripts/Squads/Systems/SquadFSM.System.cs` (línea 76)
- **Acción**: Cambiar lectura de `state.isInCombat` a leer desde `SquadAIComponent.isInCombat`. Esto requiere agregar `SquadAIComponent` al query del FSM como ReadOnly.
- **Archivo a modificar**: `Assets/Scripts/Squads/SquadState.Component.cs`
- **Acción**: Eliminar campo `isInCombat` de SquadStateComponent.
- **Verificación**: Compilar. Jugar BattleScene, verificar que un squad entra y sale de combate correctamente al detectar enemigos.

#### Tarea 1.3 — Unificar formación activa [M]
- **Fuente de verdad elegida**: `FormationComponent.currentFormation`
- **Archivos a modificar**:
  - `Assets/Scripts/Squads/SquadState.Component.cs` — eliminar campo `currentFormation`
  - `Assets/Scripts/Squads/SquadStatus.Component.cs` — eliminar campo `activeFormationId`
  - `Assets/Scripts/Squads/Systems/GridFormationUpdate.System.cs` (línea 36) — cambiar lectura a `FormationComponent`
  - Todos los sistemas que lean formación de SquadStateComponent o SquadStatusComponent — migrar a FormationComponent
- **Acción**: Grep exhaustivo de `currentFormation` y `activeFormationId`. Migrar cada lectura a FormationComponent.
- **Verificación**: Compilar. Jugar BattleScene, verificar ciclo de formaciones (X doble clic, F1-F4) y que GridFormation se actualiza visualmente.

#### Verificación Sprint 1 Completo
- [ ] Compilación sin errores ni warnings nuevos
- [ ] Transiciones de estado de squad correctas
- [ ] Combate se activa/desactiva correctamente
- [ ] Formaciones cambian y se visualizan correctamente
- [ ] No hay regresiones en movimiento de unidades
- **Rollback**: Revert de los commits del sprint (cambios de una línea, fácil de revertir individualmente)

---

### Sprint 2 — Nuevos componentes en paralelo (aditivo)
**Riesgo: Bajo** | **Objetivo: Crear componentes nuevos que escriben en paralelo a los viejos, validar equivalencia**

#### Tarea 2.1 — Crear SquadCombatStateComponent [S]
- **Archivo nuevo**: `Assets/Scripts/Squads/Components/SquadCombatState.Component.cs`
- **Campos**: `isInCombat: bool`, `engagedTarget: Entity`
- **Archivo a modificar**: `Assets/Scripts/Squads/Systems/SquadAI.System.cs`
- **Acción**: Agregar escritura al nuevo componente ADEMÁS de la escritura existente a SquadAIComponent.isInCombat. Dual-write temporal.
- **Authoring**: Agregar al baker del squad entity existente.
- **Verificación**: Verificar con debugger/log que ambos componentes contienen el mismo valor cada frame.

#### Tarea 2.2 — Crear SquadFSMComponent [S]
- **Archivo nuevo**: `Assets/Scripts/Squads/Components/SquadFSM.Component.cs`
- **Campos**: `currentState: SquadFSMState`, `stateTimer: float`
- **Archivo a modificar**: `Assets/Scripts/Squads/Systems/SquadFSM.System.cs`
- **Acción**: Agregar escritura al nuevo componente ADEMÁS de las escrituras existentes en SquadStateComponent. Dual-write temporal.
- **Authoring**: Agregar al baker del squad entity.
- **Verificación**: Verificar que ambas rutas producen el mismo estado.

#### Tarea 2.3 — Crear SquadActiveFormationComponent [S]
- **Archivo nuevo**: `Assets/Scripts/Squads/Components/SquadActiveFormation.Component.cs`
- **Campos**: `currentFormation: FormationType`, `formationChangeCooldown: float`
- **Archivo a modificar**: `Assets/Scripts/Squads/Systems/SquadOrder.System.cs`
- **Acción**: Agregar escritura al nuevo componente ADEMÁS de FormationComponent. Dual-write temporal.
- **Authoring**: Agregar al baker del squad entity.
- **Verificación**: Verificar equivalencia entre FormationComponent y SquadActiveFormationComponent.

#### Tarea 2.4 — Validar equivalencia durante 1 sprint [S]
- **Acción**: Agregar logs de validación temporales con tag `[DualWriteValidation]` que comparen valores entre componentes viejos y nuevos cada frame.
- **Verificación**: Jugar 2-3 partidas completas sin diferencias en los logs.
- **Al completar**: Marcar los componentes como validados. Los viejos se eliminan progresivamente en sprints posteriores.

#### Verificación Sprint 2 Completo
- [ ] Compilación sin errores
- [ ] Dual-write produce valores idénticos en todos los escenarios
- [ ] No hay impacto en performance (medir FPS antes/después)
- [ ] Los componentes nuevos aparecen en Entity Debugger
- **Rollback**: Eliminar los archivos nuevos y quitar las líneas de dual-write. Zero impacto.

---

### Sprint 3 — Desacoplamiento del hero entity
**Riesgo: Medio** | **Objetivo: Squads funcionan sin referencia directa al hero entity**

#### Tarea 3.1 — Crear SquadAnchorMovingTag [S]
- **Archivo nuevo**: `Assets/Scripts/Squads/Components/SquadAnchorMoving.Tag.cs`
- **Tipo**: `IComponentData, IEnableableComponent` (tag enableable)
- **Authoring**: Agregar al baker del squad entity (disabled por defecto).

#### Tarea 3.2 — Extender SquadAnchor.System.cs [M]
- **Archivo a modificar**: `Assets/Scripts/Squads/Systems/SquadAnchor.System.cs`
- **Acción**: Agregar lógica para habilitar/deshabilitar `SquadAnchorMovingTag`:
  - Calcular delta de posición del anchor entre frames
  - Si `math.lengthsq(delta) > threshold` → Enable tag
  - Si no → Disable tag
- **Nota**: El anchor ya se actualiza cada frame (posición/rotación). Solo falta el tracking de movimiento.
- **Verificación**: En Entity Debugger, verificar que el tag se enciende cuando el héroe se mueve y se apaga cuando está quieto.

#### Tarea 3.3 — Migrar UnitFormationStateSystem [L]
- **Archivo a modificar**: `Assets/Scripts/Squads/Systems/UnitFormationState.System.cs`
- **Líneas críticas**: 33 (hero entity lookup), 38 (HeroStateComponent read), 156 (heroMovingForSquad)
- **Acción**:
  - Reemplazar `Entity hero = SystemAPI.GetComponent<SquadOwnerComponent>(squadEntity).hero` (línea 33) por lectura de `SquadFormationAnchorComponent` del squad entity
  - Reemplazar `heroState == HeroState.Moving` (línea 156) por lectura de `SquadAnchorMovingTag.IsEnabled`
  - Eliminar guard clause de hero entity (línea 34) — ya no es necesaria
  - Reemplazar hero position reads por `SquadFormationAnchorComponent.position`
- **Verificación**: Jugar BattleScene, verificar transiciones Moving/Formed/Waiting. Especialmente:
  - Unidades se forman cuando héroe para
  - Unidades se mueven cuando héroe avanza
  - Hold position funciona igual

#### Tarea 3.4 — Migrar UnitFollowFormationSystem [L]
- **Archivo a modificar**: `Assets/Scripts/Squads/Systems/UnitFollowFormation.System.cs`
- **Líneas críticas**: 59 (hero entity lookup), 66 (heroPosition), 70 (heroForward), 155-159 (hold position rotation)
- **Acción**:
  - Reemplazar `Entity leader = ownerLookup[entity].hero` (línea 59) por lectura de `SquadFormationAnchorComponent`
  - Reemplazar `heroPosition` (línea 66) por `anchor.position`
  - Reemplazar `heroForward` (línea 70) por `math.forward(anchor.rotation)`
  - Eliminar guard clause de hero transform (líneas 60-63)
  - Actualizar hold position para usar anchor rotation
- **Verificación**: Jugar BattleScene, verificar:
  - Unidades siguen la posición correcta en formación
  - Rotación de formación es correcta
  - Hold position mantiene orientación correcta

#### Tarea 3.5 — Verificar orden de ejecución [S]
- **Estado actual verificado**: SquadAnchor tiene `[UpdateBefore(typeof(FormationSystem))]`. El orden es:
  `SquadAnchor → Formation → GridFormationUpdate → UnitFormationState/UnitFollowFormation`. **Correcto.**
- **Acción**: Agregar log temporal con `[ExecutionOrder]` tag para confirmar en runtime.

#### Verificación Sprint 3 Completo
- [ ] Compilación sin errores
- [ ] Squads siguen al héroe correctamente
- [ ] Formaciones se posicionan bien
- [ ] Hold position funciona
- [ ] Transiciones Moving/Formed/Waiting correctas
- [ ] El tag SquadAnchorMoving se activa/desactiva correctamente
- [ ] **Test crítico**: Si el hero entity es destruido, el squad no crashea (usa el anchor)
- **Rollback**: Revertir los 4 archivos modificados. Los componentes nuevos (tag) se pueden dejar sin impacto.

---

### Sprint 4 — Autoridad única de rotación
**Riesgo: Medio** | **Objetivo: Un único sistema escribe la rotación final de cada unidad**

#### Tarea 4.1 — Crear UnitRotationIntentComponent [S]
- **Archivo nuevo**: `Assets/Scripts/Squads/Components/UnitRotationIntent.Component.cs`
- **Campos**: `targetRotation: quaternion`, `priority: int`, `source: RotationSource`
- **Enum nuevo**: `RotationSource { NavMesh = 0, Formation = 5, Combat = 10 }`
- **Authoring**: Agregar al baker de unit entities.

#### Tarea 4.2 — Migrar UnitNavMeshSystem (rotación) [M]
- **Archivo a modificar**: `Assets/Scripts/Squads/Systems/UnitNavMesh.System.cs`
- **Línea crítica**: 183 (`agent.transform.rotation = Quaternion.LookRotation(...)`)
- **Acción**: Reemplazar write directo por escritura a `UnitRotationIntentComponent(priority=Combat=10)`. NO tocar agent.transform.rotation.
- **Verificación**: Compilar. La rotación visual NO funcionará todavía (se activa en Tarea 4.4).

#### Tarea 4.3 — Migrar UnitFollowFormationSystem (rotación) [M]
- **Archivo a modificar**: `Assets/Scripts/Squads/Systems/UnitFollowFormation.System.cs`
- **Línea crítica**: 179 (`navAgent.transform.rotation = Quaternion.Slerp(...)`)
- **Acción**: Reemplazar write directo por escritura a `UnitRotationIntentComponent(priority=Formation=5)`.
- **Verificación**: Compilar. La rotación visual NO funcionará todavía.

#### Tarea 4.4 — Crear UnitRotationResolutionSystem [M]
- **Archivo nuevo**: `Assets/Scripts/Squads/Systems/UnitRotationResolution.System.cs`
- **`[UpdateInGroup(SimulationSystemGroup)]`**
- **`[UpdateAfter(UnitFollowFormationSystem)]`**
- **Lógica**:
  1. Lee `UnitRotationIntentComponent` de cada unidad
  2. Escribe `agent.transform.rotation` con el intent de mayor prioridad
  3. Resetea el intent para el próximo frame
  4. Setea `agent.updateRotation = false` para todas las unidades con NavMeshAgent
- **Verificación**: Jugar BattleScene, verificar:
  - Unidades en combate rotan hacia el enemigo (priority Combat=10)
  - Unidades en formación rotan según la formación (priority Formation=5)
  - Transición suave entre rotaciones

#### Tarea 4.5 — Eliminar writes viejos de rotación [S]
- **Acción**: Code review + grep para confirmar que no queda ningún write directo a `agent.transform.rotation` fuera de `UnitRotationResolutionSystem`.

#### Verificación Sprint 4 Completo
- [ ] Compilación sin errores
- [ ] Rotación de combate correcta (unidades miran al enemigo)
- [ ] Rotación de formación correcta
- [ ] No hay "jitter" de rotación (un solo escritor)
- [ ] Transiciones de rotación suaves
- [ ] Grep confirma: solo UnitRotationResolutionSystem escribe agent.transform.rotation
- **Rollback**: Revertir UnitRotationResolutionSystem + restaurar writes directos en UnitNavMesh y UnitFollowFormation.

---

### Sprint 5 — Bridge ECS/NavMesh correcto
**Riesgo: Medio-Alto** | **Objetivo: Un único punto de sincronización GO↔ECS para posición**

#### Tarea 5.1 — Extender NavAgentComponent [S]
- **Archivo a modificar**: `Assets/Scripts/Squads/NavAgent.Component.cs`
- **Acción**: Agregar campo `syncPositionFromNavMesh: bool` (actualmente es tag vacío).
- **Nota**: Esto cambia el componente de tag a data component. Verificar que ningún sistema depende de que sea tag.

#### Tarea 5.2 — Crear NavMeshPositionSyncSystem [M]
- **Archivo nuevo**: `Assets/Scripts/Squads/Systems/NavMeshPositionSync.System.cs`
- **`[UpdateInGroup(SimulationSystemGroup)]`**
- **`[UpdateAfter(UnitNavMeshSystem)]`**
- **`[UpdateBefore(UnitRotationResolutionSystem)]`**
- **Lógica**:
  1. Para cada entidad con `NavAgentComponent.syncPositionFromNavMesh == true`:
  2. Lee `agent.transform.position` → escribe `LocalTransform.Position`
  3. Lee `agent.transform.rotation` → escribe `UnitRotationIntentComponent(priority=NavMesh=0)`
  4. NO escribe LocalTransform.Rotation (eso es de UnitRotationResolutionSystem)

#### Tarea 5.3 — Modificar EntityVisualSync [M]
- **Archivo a modificar**: `Assets/Scripts/Visual/EntityVisualSync.cs`
- **Líneas críticas**: 184-192 (sync GO→ECS para NavMesh units)
- **Acción**: Si la entidad tiene NavAgentComponent con `syncPositionFromNavMesh == true`, no sincronizar posición (ya lo hace NavMeshPositionSyncSystem). Mantener sync de otros datos.
- **Verificación**: Verificar que no hay "doble sync" de posición.

#### Tarea 5.4 — Verificar HeroAIExecutionSystem [S]
- **Archivo**: `Assets/Scripts/Hero/AI/Systems/HeroAIExecution.System.cs`
- **Acción**: Verificar que remote heroes también usan el bridge correctamente. Si escriben a SquadInputComponent, el flujo es: Input → Order → FSM → Formation → NavMesh → Bridge. Confirmar que no bypasea el bridge.

#### Verificación Sprint 5 Completo
- [ ] Compilación sin errores
- [ ] Posición de unidades correcta en BattleScene
- [ ] No hay "teleporting" ni desfase de posición
- [ ] EntityVisualSync no duplica sync de posición
- [ ] Remote heroes mueven squads correctamente
- [ ] Performance: medir FPS con 3 squads en movimiento simultáneo
- **Rollback**: Eliminar NavMeshPositionSyncSystem + revertir EntityVisualSync + revertir NavAgentComponent a tag.

---

### Sprint 6 — Intent/Resolution layer completo + nuevas funcionalidades
**Riesgo: Alto** | **Objetivo: Sistema explícito de prioridad de órdenes + combat blocking + heroOrdenCooldown**

> **IMPORTANTE**: Crear branch dedicado antes de empezar este sprint.

#### Fase 6A — Intent Components [S cada uno]

**Tarea 6A.1** — Crear `SquadPlayerOrderIntentComponent`
- **Archivo nuevo**: `Assets/Scripts/Squads/Components/SquadPlayerOrderIntent.Component.cs`
- **Campos**: `orderType`, `desiredFormation`, `holdPosition`, `insistenceCount: int`, `insistenceTimer: float`, `heroOrdenCooldownActive: bool`, `heroOrdenCooldownTimer: float`

**Tarea 6A.2** — Crear `SquadAIOrderIntentComponent`
- **Archivo nuevo**: `Assets/Scripts/Squads/Components/SquadAIOrderIntent.Component.cs`
- **Campos**: `tacticalIntent: TacticalIntent`, `suggestedOrder: SquadOrderType`, `targetEntity: Entity`

**Tarea 6A.3** — Crear `SquadCombatReactionIntentComponent`
- **Archivo nuevo**: `Assets/Scripts/Squads/Components/SquadCombatReactionIntent.Component.cs`
- **Campos**: `reactToEnemy: bool`, `reactTarget: Entity`

**Tarea 6A.4** — Crear `SquadResolvedOrderComponent`
- **Archivo nuevo**: `Assets/Scripts/Squads/Components/SquadResolvedOrder.Component.cs`
- **Campos**: `order: SquadOrderType`, `formation: FormationType`, `holdPosition: float3`, `targetEntity: Entity`, `source: OrderSource`
- **Enum nuevo**: `OrderSource { Player, AI, CombatReaction }`

#### Fase 6B — Nuevos sistemas [M-L]

**Tarea 6B.1** — Crear `CombatReactionSystem` [M]
- **Archivo nuevo**: `Assets/Scripts/Squads/Systems/CombatReaction.System.cs`
- **`[UpdateAfter(SquadAISystem)]`, `[UpdateBefore(OrderResolutionSystem)]`**
- **Lógica**: Extraer la lógica de detección/reacción de combate de `SquadAISystem`. Escribir a `SquadCombatReactionIntentComponent`.
- **Acción en `SquadAI.System.cs`**: Eliminar lógica de reacción de combate migrada. SquadAISystem solo escribe `SquadAIOrderIntentComponent`.

**Tarea 6B.2** — Crear `OrderResolutionSystem` [L]
- **Archivo nuevo**: `Assets/Scripts/Squads/Systems/OrderResolution.System.cs`
- **`[UpdateAfter(CombatReactionSystem)]`, `[UpdateBefore(SquadFSMSystem)]`**
- **Lógica — Squad de local hero** (tiene `IsLocalPlayer` en el hero owner):
  1. Si `heroOrdenCooldownActive` → usar `SquadPlayerOrderIntentComponent` (movimiento forzado)
  2. Si no: CombatReaction (prioridad alta) > AI > Player (prioridad baja)
- **Lógica — Squad de remote hero** (sin `IsLocalPlayer`):
  1. Si `SquadCombatStateComponent.isInCombat == true` → descartar `SquadAIOrderIntentComponent` con orden de movimiento
  2. Combate bloquea completamente movimiento
  3. Salida solo por: `DetectedEnemy` buffer vacío O unidades muertas O enemigos fuera de rango
- **Escribe**: `SquadResolvedOrderComponent` (ÚNICO escritor)

**Tarea 6B.3** — Implementar `insistenceCount` en `SquadControlSystem` [M]
- **Archivo a modificar**: `Assets/Scripts/Squads/Systems/SquadControl.System.cs`
- **Acción**:
  - Escribir a `SquadPlayerOrderIntentComponent` en lugar de `SquadInputComponent`
  - Agregar lógica de insistencia: si la misma orden se repite N veces en ventana T, activar heroOrdenCooldown
  - Parámetros configurables (agregar a config component del squad):
    - `heroOrdenInsistenceCount: int = 3`
    - `heroOrdenInsistenceWindow: float = 0.8f`
    - `heroOrdenCooldownDuration: float = 4f`

#### Fase 6C — Migración de consumidores [M]

**Tarea 6C.1** — Migrar `SquadFSMSystem` [M]
- **Archivo a modificar**: `Assets/Scripts/Squads/Systems/SquadFSM.System.cs`
- **Acción**: Cambiar lectura de `SquadStateComponent.currentOrder` a `SquadResolvedOrderComponent.order`. Cambiar lectura de isInCombat a `SquadCombatStateComponent.isInCombat`.
- **Eliminar**: El hardcoded `3f` timer (línea 96) — usar `SquadCombatStateComponent` real state.

**Tarea 6C.2** — Migrar `HeroAIExecutionSystem` [M]
- **Archivo a modificar**: `Assets/Scripts/Hero/AI/Systems/HeroAIExecution.System.cs`
- **Acción**: En lugar de escribir a `SquadInputComponent` (líneas 109-113), escribir a `SquadAIOrderIntentComponent`.
- **Verificación crítica**: Remote heroes deben respetar combat blocking via OrderResolutionSystem.

**Tarea 6C.3** — Eliminar path viejo [S]
- **Acción**: Eliminar `SquadInputComponent.hasNewOrder` path en `SquadOrderSystem`. `SquadOrderSystem` puede reducirse o eliminarse (su rol lo absorbe `OrderResolutionSystem`).

#### Verificación Sprint 6 Completo
- [ ] Compilación sin errores
- [ ] **Test local hero**: Squad sigue órdenes del jugador normalmente
- [ ] **Test local hero**: Doble/triple clic activa heroOrdenCooldown, unidades ignoran combate y se mueven
- [ ] **Test local hero**: heroOrdenCooldown expira, unidades vuelven a reaccionar a combate
- [ ] **Test local hero**: Orden de combate explícita (V) cancela heroOrdenCooldown
- [ ] **Test remote hero**: Squad entra en combate y BLOQUEA órdenes de movimiento del AI
- [ ] **Test remote hero**: Combate termina cuando enemigos mueren/salen de rango
- [ ] **Test remote hero**: AI puede volver a mover el squad después de que termina combate
- [ ] No hay regresiones en formaciones, rotación, ni movimiento
- [ ] Resuelve BUG-006, BUG-007, BUG-008, BUG-009 de `buggedFunctionalities.md`
- **Rollback**: Revert completo del branch dedicado.

---

### Sprint 7 — Descomposición de `SquadDataComponent`
**Riesgo: Alto** | **Objetivo: Eliminar el god object, 11 sistemas coordinados**

> **IMPORTANTE**: Commits granulares (uno por subtarea 7.2.x). Crear branch dedicado.

#### Tarea 7.1 — Crear los 4 componentes nuevos [S cada uno]

**7.1.1** — `SquadDefinitionComponent`
- **Campos**: squadType, unitCount, leadershipCost, prefab, leashDistance, detectionRange, behaviorProfile, formationLibrary (BlobRef)
- **Lectores estimados**: ~4 sistemas

**7.1.2** — `SquadCombatStatsComponent`
- **Campos**: damage (blunt/slash/pierce), defense, penetration, attackRange, attackInterval, strikeWindow, kineticMultiplier
- **Lectores estimados**: ~3 sistemas de combate

**7.1.3** — `SquadMobilityComponent`
- **Campos**: baseSpeed, mass, weight
- **Lectores estimados**: ~2 sistemas de navegación

**7.1.4** — `SquadRangedStatsComponent`
- **Campos**: isRangedUnit, range, accuracy, fireRate, ammoCapacity
- **Lectores estimados**: ~1 sistema

#### Tarea 7.2 — Migrar en orden de menor a mayor impacto

**7.2.1** — Migrar `SquadRangedStatsComponent` primero [S]
- Identificar el sistema que lee campos ranged de SquadDataComponent
- Reemplazar lectura por SquadRangedStatsComponent
- Eliminar campos ranged de SquadDataComponent

**7.2.2** — Migrar `SquadMobilityComponent` [S]
- Identificar los 2 sistemas de navegación
- Reemplazar lecturas por SquadMobilityComponent
- Eliminar campos de movilidad de SquadDataComponent

**7.2.3** — Migrar `SquadCombatStatsComponent` [M]
- Identificar los 3 sistemas de combate
- Reemplazar lecturas por SquadCombatStatsComponent
- Eliminar campos de combate de SquadDataComponent

**7.2.4** — Migrar `SquadDefinitionComponent` [L]
- Más lectores (4+), incluye BlobRef de formaciones
- **Precaución**: Validar lifecycle de `BlobAssetReference` — si se mueve a otro componente, el blob debe seguir vivo
- Reemplazar lecturas sistema por sistema
- Eliminar campos de SquadDataComponent progresivamente

#### Tarea 7.3 — Eliminar SquadDataComponent [S]
- **Prerequisito**: Todos los campos migrados, cero lectores
- **Acción**: Eliminar archivo + eliminar authoring/baker asociado
- **Verificación**: Grep exhaustivo confirma cero referencias

#### Tarea 7.4 — Actualizar Authoring/Bakers [M]
- Crear o actualizar bakers para los 4 componentes nuevos
- Fuente de datos: el mismo ScriptableObject/SquadData que alimentaba SquadDataComponent
- Verificar que los valores se bakean correctamente en `DOTSWorld.unity`

#### Verificación Sprint 7 Completo
- [ ] Compilación sin errores
- [ ] `SquadDataComponent` ya no existe en el código
- [ ] Todos los sistemas leen del componente correcto (grep confirma)
- [ ] `BlobAssetReference` de formaciones funciona correctamente
- [ ] Bakeo de componentes correcto en subscena
- [ ] Performance: medir FPS — debería ser igual o mejor (queries más pequeños)
- [ ] Partida completa de BattleScene sin errores
- **Rollback**: Revert parcial posible gracias a commits granulares por subtarea.

---

## 7. Archivos críticos de referencia

| Archivo | Sprints que lo tocan |
|---------|---------------------|
| `Assets/Scripts/Squads/Systems/SquadAI.System.cs` | 1, 2, 6 |
| `Assets/Scripts/Squads/Systems/SquadFSM.System.cs` | 1, 2, 6 |
| `Assets/Scripts/Squads/Systems/SquadControl.System.cs` | 6 |
| `Assets/Scripts/Squads/Systems/SquadOrder.System.cs` | 1, 6 |
| `Assets/Scripts/Squads/Systems/UnitNavMesh.System.cs` | 4, 5 |
| `Assets/Scripts/Squads/Systems/UnitFollowFormation.System.cs` | 3, 4 |
| `Assets/Scripts/Squads/Systems/UnitFormationState.System.cs` | 3 |
| `Assets/Scripts/Squads/Systems/SquadAnchor.System.cs` | 3 |
| `Assets/Scripts/Squads/Systems/GridFormationUpdate.System.cs` | 1 |
| `Assets/Scripts/Visual/EntityVisualSync.cs` | 5 |
| `Assets/Scripts/Hero/AI/Systems/HeroAIExecution.System.cs` | 6 |
| `Assets/Scripts/Squads/SquadData.Component.cs` | 7 |
| `Assets/Scripts/Squads/SquadState.Component.cs` | 1, 2 |
| `Assets/Scripts/Squads/SquadFSMState.Component.cs` | 1 (eliminar) |
| `Assets/Scripts/Squads/SquadStatus.Component.cs` | 1 |
| `Assets/Scripts/Squads/NavAgent.Component.cs` | 5 |
