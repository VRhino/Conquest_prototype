# Remote Heroes — Pipeline de Movimiento y Animaciones

> Fecha de análisis: 2026-03-29 (actualizado — pipeline cambió en sprint 6-7)
> Scope: todo héroe sin `IsLocalPlayer` component (héroes controlados por AI/remote)

---

## 1. Diferenciación Local vs Remote

La distinción se hace por presencia de componentes ECS, no por un flag booleano:

| Aspecto | Local Hero | Remote Hero |
|---------|-----------|-------------|
| Tag ECS | `IsLocalPlayer` | `HeroAITag` |
| `EntityVisualSync.IsLocalHero` | `true` | `false` |
| CharacterController | Habilitado | **Deshabilitado** |
| NavMeshAgent | No | Sí |
| `EcsAnimationInputAdapter` | Habilitado | **Deshabilitado** |
| Fuente de movimiento | Teclado/mouse | AI (NavMeshAgent) |
| Fuente de animación | `HeroInputComponent` | `NavMeshAgent.velocity` |

**Código de detección** — `EntityVisualSync.cs:91`:
```csharp
IsLocalHero = _entityManager.HasComponent<IsLocalPlayer>(_heroEntity);
```

---

## 2. Pipeline de Movimiento

### Flujo completo

```
HeroAIPerceptionSystem
  Lee: posición de enemigos, estado de zonas, squads propios
  Escribe: HeroAIBlackboard
        ↓
HeroAIRusherSystem / HeroAIBalancedSystem
  Lee: HeroAIBlackboard
  Escribe: HeroAIDecision { action, targetPosition, shouldSprint }
        ↓
HeroAIExecution.System.cs  ← NODO CENTRAL
  ├─ NavMeshAgent.SetDestination(dec.targetPosition)
  ├─ Escribe HeroMoveIntent { Direction = agent.desiredVelocity, Speed }
  └─ Escribe SquadInputComponent (órdenes al squad)
        ↓
NavMeshAgent (Unity runtime)
  Mueve el GameObject en el mundo (pathfinding automático)
        ↓
EntityVisualSync.Update() — líneas 184–192
  Si usesNavMesh (agent != null && enabled && isOnNavMesh):
    └─ GO.position/rotation → ECS LocalTransform  ← GO es autoritativo
```

### Archivos clave — Movimiento

| Sistema | Archivo | Rol |
|---------|---------|-----|
| `HeroAIPerceptionSystem` | `Assets/Scripts/Hero/AI/Systems/HeroAIPerception.System.cs` | Lee mundo, escribe Blackboard |
| `HeroAIRusherSystem` | `Assets/Scripts/Hero/AI/Systems/HeroAIRusher.System.cs` | Comportamiento agresivo |
| `HeroAIBalancedSystem` | `Assets/Scripts/Hero/AI/Systems/HeroAIBalanced.System.cs` | Comportamiento balanceado |
| `HeroAIExecution.System.cs` | `Assets/Scripts/Hero/AI/Systems/HeroAIExecution.System.cs` | Traduce decisión → NavMesh + MoveIntent |
| `EntityVisualSync` | `Assets/Scripts/Visual/EntityVisualSync.cs` | Sincroniza GO pos → ECS (líneas 184–207) |

### Setup del NavMeshAgent

Realizado en `HeroVisualManagement.System.cs:148–163`:
```csharp
// Heroes remotos usan NavMeshAgent para navegar sin teleport
if (!isLocalPlayer && pendingNavAgents != null)
{
    var agent = visualInstance.GetComponent<NavMeshAgent>();
    if (agent == null)
    {
        // WARNING: NavMeshAgent missing from prefab — adding programmatically
        agent = visualInstance.AddComponent<NavMeshAgent>();
        agent.stoppingDistance = 0.5f;
        agent.autoBraking     = true;
        agent.angularSpeed    = 360f;
        agent.acceleration    = 12f;
        // speed se sobreescribe cada frame desde stats.baseSpeed
    }
    agent.Warp(visualInstance.transform.position);
    pendingNavAgents.Add((entity, agent));
}
```

---

## 3. Pipeline de Animaciones

### Flujo completo

```
[LOCAL HERO]
HeroInputComponent (teclado/mouse)
  → EcsAnimationInputAdapter (HABILITADO)
    → SamplePlayerAnimationController_ECS.Update()
      → Animator.SetFloat / SetBool (MoveSpeed, CurrentGait, IsWalking...)

[REMOTE HERO]
NavMeshAgent.velocity
  → EntityVisualSync.Update():194–197
    → EcsAnimationInputAdapter.DriveFromVelocity(velocity, speed)
      → Solo setea _moveComposite y _movementInputDetected
        → SamplePlayerAnimationController_ECS.Update()
          → Animator.SetFloat(MoveSpeed), SetInt(CurrentGait), SetBool(IsWalking, IsStopped)
```

### Deshabilitación del EcsAnimationInputAdapter

`HeroVisualManagement.System.cs:125–128`:
```csharp
// Héroes remotos no deben leer input local — deshabilitar el adaptador de animación
// (EntityVisualSync lo pilota desde la velocidad del NavMeshAgent)
var animAdapter = visualInstance.GetComponentInChildren<EcsAnimationInputAdapter>(true);
if (animAdapter != null) animAdapter.enabled = false;
```

### DriveFromVelocity — lo que SÍ alimenta

`EcsAnimationInputAdapter.cs:317–336` — solo alimenta:
- `_moveComposite` → `Vector2(0, normalizedSpeed)`
- `_movementInputDetected` → `bool`
- `movementDuration` → contador de tiempo en movimiento

### Parámetros del Animator que SÍ funcionan para remote heroes

`SamplePlayerAnimationController_ECS.cs:438–481`:
- `MoveSpeed` (float) — velocidad normalizada
- `CurrentGait` (int: 0=Idle, 1=Walk, 2=Run, 3=Sprint)
- `IsWalking` (bool)
- `IsStopped` (bool)
- `MovementInputHeld` (bool)
- `IsGrounded` (bool) — hardcoded `true`

### Parámetros del Animator que NO se alimentan para remote heroes

- `StrafeDirectionX`, `StrafeDirectionZ` — siempre 0
- Head/Body look angles — siempre 0
- Lean values — siempre 0
- `IsTurningInPlace` — siempre false
- `TriggerAttack` — **NUNCA se llama** (ver Bug #1)

---

## 4. Bugs Confirmados

### BUG-001 — Animación de ataque nunca se ejecuta
**Severidad**: Alta
**Afecta**: Local Y remote heroes

**Síntoma**: El héroe ataca (daño calculado, cooldown activo) pero la animación de ataque nunca se reproduce.

**Causa raíz**: `HeroAttack.System.cs:44,80` setea `HeroAnimationComponent.triggerAttack = true`, pero **ningún código lo lee**. `SamplePlayerAnimationController_ECS.cs:438–481` no tiene ninguna línea que llame `Animator.SetTrigger("Attack")`. El bool se escribe y se ignora permanentemente.

Para remote heroes el problema es doble: además de que nadie lee el flag, `EcsAnimationInputAdapter` está deshabilitado, por lo que incluso si existiera código de lectura en el adaptador, tampoco funcionaría.

**Archivos involucrados**:
- `Assets/Scripts/Hero/Systems/HeroAttack.System.cs:44,80` — escribe el flag
- `Assets/Scripts/Hero/HeroAnimation.Component.cs` — define `triggerAttack`
- `Assets/Scripts/Hero/SamplePlayerAnimationController_ECS.cs:438–481` — lugar donde debería leerse

---

### BUG-002 — Remote hero pierde fidelidad de animación (upper body)
**Severidad**: Media
**Afecta**: Solo remote heroes

**Síntoma**: Remote heroes solo muestran walk/run genérico hacia adelante. Sin strafe, sin look at target, sin leaning, sin turning-in-place.

**Causa raíz**: `DriveFromVelocity` solo pasa velocidad forward. Todos los parámetros de upper body (head/body look angles, lean, strafe direction, turning detection) están desconectados de cualquier fuente para remote heroes.

**Archivo**: `Assets/Scripts/Visual/EntityVisualSync.cs:194–197`

---

### BUG-003 — NavMeshAgent no está en el prefab (warning activo en cada spawn)
**Severidad**: Baja
**Afecta**: Todos los remote heroes

**Síntoma**: En cada spawn de un remote hero aparece en consola:
`[HeroVisualManagementSystem] NavMeshAgent missing from hero visual prefab — adding programmatically.`

**Causa raíz**: El visual prefab del héroe no tiene `NavMeshAgent` en el prefab. Se agrega en runtime con parámetros hardcoded. Si Unity cambia el estado del NavMesh entre frames antes de que `Warp()` se ejecute, puede causar un warp inesperado al punto NavMesh más cercano.

**Archivo**: `Assets/Scripts/Hero/Systems/HeroVisualManagement.System.cs:154`

---

### BUG-004 — HeroStateComponent desconectado del sistema de animación
**Severidad**: Baja
**Afecta**: Local y remote heroes

**Síntoma**: `HeroStateComponent` existe en ECS con valores `Moving`/`Idle`, pero no tiene efecto observable en la animación.

**Causa raíz**: `HeroState.System.cs` calcula el estado por distancia (threshold 0.0025 units²), pero `SamplePlayerAnimationController_ECS` no lee `HeroStateComponent` — lee directamente de `EcsAnimationInputAdapter`. El componente ECS es redundante para animación y puede divergir del estado visual.

**Archivos**:
- `Assets/Scripts/Hero/Systems/HeroState.System.cs:27` — calcula estado
- `Assets/Scripts/Hero/SamplePlayerAnimationController_ECS.cs` — no lo lee

---

### BUG-005 — CharacterController habilitado brevemente en remote heroes al inicio
**Severidad**: Baja
**Afecta**: Remote heroes, solo durante el frame de spawn

**Síntoma**: Posible jitter o desplazamiento leve al spawnear un remote hero.

**Causa raíz**: `EntityVisualSync.cs:97–100` deshabilita CC, setea posición, y re-habilita CC para **todos** los heroes. Después, `HeroVisualManagementSystem.cs:99–100` deshabilita CC para remote heroes. Existe una ventana de frames entre la inicialización del sync y la ejecución del sistema de visual management donde el remote hero tiene CC habilitado. Si el NavMeshAgent ya está activo en ese intervalo, ambos componentes compiten por la posición del transform.

**Archivos**:
- `Assets/Scripts/Visual/EntityVisualSync.cs:97–100` — re-habilita CC sin distinción
- `Assets/Scripts/Hero/Systems/HeroVisualManagement.System.cs:99–100` — deshabilita CC para remote

---

## 5. Resumen de Bugs por Prioridad de Fix

| ID | Severidad | Fix sugerido |
|----|-----------|-------------|
| BUG-001 | **Alta** | En `SamplePlayerAnimationController_ECS`, leer `HeroAnimationComponent.triggerAttack` y llamar `Animator.SetTrigger("Attack")`. Para remote heroes, hacerlo desde `EntityVisualSync` leyendo el componente ECS directo. |
| BUG-002 | Media | Extender `DriveFromVelocity` o agregar un método separado en `EntityVisualSync` que alimente look-at target y strafe desde `HeroAIDecision.targetPosition`. |
| BUG-003 | Baja | Agregar `NavMeshAgent` al visual prefab del héroe para eliminar el AddComponent en runtime. |
| BUG-004 | Baja | Decidir si `HeroStateComponent` debe ser la fuente de verdad para animación (y conectarlo) o eliminarlo por ser redundante. |
| BUG-005 | Baja | En `EntityVisualSync.Initialize()`, no re-habilitar el CC si la entidad tiene `HeroAITag`. |

---

## 6. Sistema de Órdenes y Combate de Unidades

> Fecha de análisis: 2026-03-29 (actualizado — pipeline cambió en sprint 6-7)
> Scope: comportamiento de squads cuando reciben daño o detectan enemigos mientras ejecutan órdenes de movimiento

### 6.1 Estado actual del sistema

#### Pipeline de órdenes (local hero)
```
Teclado (C/X/V/F1-F4)
  → SquadControlSystem  [solo IsLocalSquadActive]
    ├─ SquadInputComponent.hasNewOrder = true
    └─ SquadPlayerOrderIntentComponent  ← NUEVO (sprint 6-7)
         insistenceCount / insistenceTimer / heroOrdenCooldownActive / heroOrdenCooldownTimer
         (si misma orden × 3 en 0.8s → activa cooldown de 4s)
      → SquadOrderSystem
        → SquadStateComponent.currentOrder
          → SquadFSMSystem
            ├─ Entrada a InCombat bloqueada si heroOrdenCooldownActive
            ├─ Salida de InCombat bypassa timer mínimo si heroOrdenCooldownActive
            └─ SquadFSMState (Idle | FollowingHero | HoldingPosition | InCombat | Retreating | KO)
              → UnitNavMeshSystem + UnitFollowFormationSystem
```

#### Pipeline de detección de combate
```
EnemyDetectionSystem  (distancia al centroide)
  → DetectedEnemy buffer (squad-level)
  → UnitDetectedEnemy buffer (por unidad)
        ↓
DamageCalculationSystem  (daño recibido)
  → IsUnderAttackTag  (pulso de 1 frame)
        ↓
SquadAI.System.cs:107-108
  isInCombat = enemiesDetected || wasHit
  if (wasHit) desiredState = TacticalIntent.Attacking  [override inmediato]
        ↓
SquadFSMSystem  →  transición a InCombat (mínimo 3 segundos hardcoded)
        ↓
UnitNavMeshSystem
  └─ TacticalIntent.Attacking → bypass del leash → persecución libre
```

#### Estados ECS relevantes

| Componente | Archivo | Campos clave |
|-----------|---------|-------------|
| `SquadStateComponent` | `Assets/Scripts/Squads/Components/` | `isInCombat`, `currentOrder`, `currentState` |
| `UnitFormationStateComponent` | `Assets/Scripts/Squads/Components/` | estado: Moving / Formed / Waiting |
| `IsUnderAttackTag` | `Assets/Scripts/Combat/IsUnderAttack.Tag.cs` | tag pulso (1 frame) |
| `DetectedEnemy` buffer | `Assets/Scripts/Combat/` | lista de enemigos en rango |
| `IsEngagingTag` | `Assets/Scripts/Squads/` | unidad en engagement, omite orientación de formación |

#### Condición de salida de combate actual
- Tiempo mínimo de 3 segundos en `SquadFSMSystem` (hardcoded)
- **Bypass** del timer si `heroOrdenCooldownActive = true` (local player únicamente)
- Aún no evalúa: "todos los enemigos muertos", "enemigo salió del rango", "todas las unidades muertas"

#### Nuevo componente: SquadPlayerOrderIntentComponent (sprint 6-7)
**Archivo**: `Assets/Scripts/Squads/Components/SquadPlayerOrderIntent.Component.cs`

| Campo | Tipo | Uso |
|-------|------|-----|
| `insistenceCount` | int | Cuántas veces se repitió la misma orden |
| `insistenceTimer` | float | Ventana de 0.8s para detectar repeticiones |
| `heroOrdenCooldownActive` | bool | Cooldown activo: ignora InCombat |
| `heroOrdenCooldownTimer` | float | Tiempo restante del cooldown (4s) |

**Constantes en `SquadControl.System.cs`**: `InsistenceCount=3`, `InsistenceWindow=0.8s`, `CooldownDuration=4s`

Este componente solo existe/se activa para squads del jugador local. Los squads AI nunca pasan por `SquadControl.System`, por lo que `heroOrdenCooldownActive` nunca se activa para ellos.

---

### 6.2 Comportamiento deseado (spec)

#### Remote hero squads
- Cuando el squad está en movimiento y detecta enemigos **o** recibe daño → **bloqueo total de órdenes de movimiento**
- Permanece en combate hasta que se cumpla **una** de:
  1. Todos los enemigos en rango son eliminados
  2. Todas las unidades del squad mueren
  3. Los enemigos salen completamente del rango de detección
- Mientras el bloqueo está activo: **ninguna orden de movimiento** del AI execution puede interrumpir el estado

#### Local hero squads
- Mismo bloqueo de combate que el remote, **excepto** por el override de "orden insistente":
  - Si el hero emite la misma orden de movimiento **N veces** (parametrizable, default 2-3) dentro de una ventana de tiempo **T** (parametrizable, `heroOrdenCooldown`) → se activa el modo **MovimientoForzado**
  - Durante `heroOrdenCooldown`: unidades ignoran detección de combate y enemigos, ejecutan el movimiento
  - Al expirar `heroOrdenCooldown`: las unidades vuelven al comportamiento reactivo normal
  - Si mientras está activo reciben **cualquier orden de combate** (e.g. acción defensiva, V=Attack) → cancelan `heroOrdenCooldown` inmediatamente y entran en combate
  - Para reactivar `heroOrdenCooldown` hace falta volver a emitir N órdenes en ventana T

---

### 6.3 Bugs / Features faltantes

#### BUG-006 — Remote hero squad no bloquea órdenes de movimiento al entrar en combate
**Severidad**: Alta
**Afecta**: Squads de remote heroes

**Síntoma**: El AI (`HeroAIExecution.System`) puede emitir una orden de movimiento que saca al squad del estado `InCombat` incluso cuando hay enemigos activos en rango de detección. Las unidades abandonan el combate a mitad de engagement.

**Causa raíz actualizada** (sprint 6-7 cambió el pipeline):
- `SquadFSM.System` ahora respeta `heroOrdenCooldownActive`, pero ese flag **solo se activa desde `SquadControl.System`** (input local). Los squads de remote heroes nunca tienen `heroOrdenCooldownActive = true`.
- `HeroAIExecution.System` escribe `SquadInputComponent.hasNewOrder = true` verificando únicamente `life.isAlive && dec.hasNewSquadOrder` — **no verifica si el squad está en `InCombat`**.
- Resultado: pasados los 3 segundos mínimos de `InCombat`, cualquier orden del AI desencadena transición a `FollowingHero` aunque haya enemigos vivos en rango.

**Archivos**:
- `Assets/Scripts/Hero/AI/Systems/HeroAIExecution.System.cs` — escribe `SquadInputComponent` sin verificar estado `InCombat`
- `Assets/Scripts/Squads/Systems/SquadFSM.System.cs` — condición de salida de `InCombat`: timer (3s) sin verificar `DetectedEnemy` buffer

---

#### ~~BUG-007~~ — FIXED (sprint 6-7)
**Era**: Sistema `heroOrdenCooldown` para squads del local hero no existía

**Implementado en**:
- `Assets/Scripts/Squads/Components/SquadPlayerOrderIntent.Component.cs` — nuevo componente con `insistenceCount`, `insistenceTimer`, `heroOrdenCooldownActive`, `heroOrdenCooldownTimer`
- `Assets/Scripts/Squads/Systems/SquadControl.System.cs` — detecta 3 pulsaciones de la misma orden en 0.8s → activa cooldown de 4s
- `Assets/Scripts/Squads/Systems/SquadFSM.System.cs` — respeta el flag: bloquea entrada a `InCombat` y bypassa timer de salida cuando `heroOrdenCooldownActive = true`

---

#### BUG-008 — Condición de salida de InCombat no refleja estado real del combate
**Severidad**: Media
**Afecta**: Todos los squads

**Síntoma**: Después de 3 segundos en `InCombat`, el squad puede transicionar aunque:
- Todavía hay enemigos vivos dentro del rango de detección
- Las unidades están en medio de un ataque (`IsEngagingTag` activo)

Inversamente, puede permanecer en `InCombat` 3 segundos aunque el único enemigo murió en el frame 2.

**Causa raíz actualizada** (sprint 6-7 cambió parcialmente):
- `SquadFSM.System` ahora usa: **timer (3s) + bypass por `heroOrdenCooldownActive`**
- El bypass ayuda al jugador local, pero **no resuelve el problema semántico**: ni para squads locales ni remotos se evalúa `DetectedEnemy.buffer.IsEmpty` ni si los targets siguen vivos
- `ai.isInCombat` (de `SquadAIComponent`) es la fuente de verdad para entrar a `InCombat`, pero ese flag tampoco verifica si los enemigos siguen en rango al momento de evaluar la salida

**Archivo**: `Assets/Scripts/Squads/Systems/SquadFSM.System.cs` — condición de salida de `InCombat`

---

#### BUG-009 — Distinción local vs remote en FSM es implícita, no explícita
**Severidad**: Media (reducida — comportamiento local ya funciona)
**Afecta**: Lógica de prioridad de órdenes (principalmente squads remotos)

**Síntoma**: `SquadOrder.System` trata de forma idéntica las órdenes del jugador local y las del AI. No hay metadata de origen en la orden.

**Estado actualizado** (sprint 6-7):
- La distinción existe **implícitamente**: `heroOrdenCooldownActive` solo se activa vía `SquadControl.System` (input local). Los squads AI nunca pasan por ese sistema, por lo que su flag permanece `false`.
- `SquadFSM.System` rama de `heroOrdenCooldownActive` efectivamente es solo-local.
- **Lo que sigue faltando**: `SquadOrder.System` no tiene distinción explícita. Si en el futuro el AI necesita un comportamiento diferenciado más granular, el mecanismo implícito no será suficiente.

**Archivos**:
- `Assets/Scripts/Squads/Components/SquadInput.Component.cs` — `isPlayerForced` aún no existe
- `Assets/Scripts/Squads/Systems/SquadOrder.System.cs` — no distingue origen de la orden

---

### 6.4 Resumen de bugs — Sistema de órdenes y combate

| ID | Severidad | Estado | Fix sugerido |
|----|-----------|--------|-------------|
| BUG-006 | **Alta** | Abierto | En `HeroAIExecution.System`, verificar `SquadFSMState.InCombat` antes de emitir órdenes de movimiento. En `SquadFSM.System`, bloquear salida de `InCombat` para squads remotos mientras `DetectedEnemy` buffer no esté vacío. |
| ~~BUG-007~~ | ~~Alta~~ | **FIXED** (sprint 6-7) | Implementado en `SquadPlayerOrderIntentComponent` + `SquadControl.System` + `SquadFSM.System`. |
| BUG-008 | Media | Abierto | Reemplazar timer hardcoded por condición compuesta: `timer > minDuration && DetectedEnemy.IsEmpty`. El bypass de `heroOrdenCooldownActive` ya existe, falta la condición semántica de "enemigos fuera de rango". |
| BUG-009 | Media (reducida) | Parcial | La distinción funciona implícitamente via `heroOrdenCooldownActive`. Fix completo requiere `bool isPlayerForced` en `SquadInputComponent` y rama explícita en `SquadOrder.System`. |
