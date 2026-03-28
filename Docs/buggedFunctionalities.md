# Remote Heroes — Pipeline de Movimiento y Animaciones

> Fecha de análisis: 2026-03-28
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

> Fecha de análisis: 2026-03-28
> Scope: comportamiento de squads cuando reciben daño o detectan enemigos mientras ejecutan órdenes de movimiento

### 6.1 Estado actual del sistema

#### Pipeline de órdenes (local hero)
```
Teclado (C/X/V/F1-F4)
  → SquadControlSystem  [solo IsLocalSquadActive]
    → SquadInputComponent.hasNewOrder = true
      → SquadOrderSystem
        → SquadStateComponent.currentOrder
          → SquadFSMSystem
            → SquadFSMState (Idle | FollowingHero | HoldingPosition | InCombat | Retreating | KO)
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
- Solo tiempo: 3 segundos mínimo en `SquadFSMSystem` (hardcoded, no parametrizable)
- No hay chequeo de: "todos los enemigos muertos", "enemigo salió del rango", "todas las unidades muertas"

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

**Síntoma**: El AI (`HeroAIExecutionSystem`) puede emitir una orden de movimiento que saca al squad del estado `InCombat` incluso cuando hay enemigos activos en rango de detección. Las unidades abandonan el combate a mitad de engagement.

**Causa raíz**: `SquadFSMSystem` tiene un mínimo de 3 segundos en `InCombat`, pero pasado ese tiempo cualquier `currentOrder` de movimiento escrito por `HeroAIExecutionSystem` puede hacer transicionar el FSM a `FollowingHero`. No existe una comprobación de "¿hay enemigos activos en rango?" antes de permitir la salida de `InCombat` para squads de remote heroes.

**Archivos**:
- `Assets/Scripts/Squads/Systems/SquadFSM.System.cs` — condición de salida de InCombat (solo timer)
- `Assets/Scripts/Hero/AI/Systems/HeroAIExecution.System.cs` — escribe SquadInputComponent sin respetar InCombat

---

#### BUG-007 — No existe el sistema `heroOrdenCooldown` para squads del local hero
**Severidad**: Alta
**Afecta**: Squad del local hero (IsLocalSquadActive)

**Síntoma**: Cuando el jugador emite órdenes de movimiento repetidas rápidamente (doble/triple tap) para sacar al squad de un combate, las unidades NO responden — el FSM permanece en `InCombat` los 3 segundos mínimos y luego sigue reaccionando a cualquier enemigo en rango. No hay mecanismo para que el jugador fuerce el movimiento mediante órdenes insistentes.

**Causa raíz**: No existen:
- Componente `HeroOrdenCooldown` (ni similar)
- Contador de "cuántas veces se emitió la misma orden en ventana T"
- Lógica en `SquadControlSystem` o `SquadOrderSystem` que detecte órdenes repetidas y eleve su prioridad
- Estado `MovimientoForzado` en el FSM del squad

**Archivos donde debe implementarse**:
- `Assets/Scripts/Squads/Systems/SquadControl.System.cs` — detección de orden repetida + ventana de tiempo
- `Assets/Scripts/Squads/Systems/SquadFSM.System.cs` — nuevo estado o flag de override
- `Assets/Scripts/Squads/Components/SquadStateComponent.cs` — campos: `ordenRepeatCount`, `ordenRepeatTimer`, `heroOrdenCooldownActive`, `heroOrdenCooldownTimer`

---

#### BUG-008 — Condición de salida de InCombat solo por timer, no por estado real del combate
**Severidad**: Media
**Afecta**: Todos los squads

**Síntoma**: Después de 3 segundos en `InCombat`, el squad puede transicionar a otro estado aunque:
- Todavía hay enemigos vivos dentro del rango de detección
- Las unidades están en medio de un ataque (IsEngagingTag activo)

Inversamente, el squad puede permanecer en `InCombat` 3 segundos aunque el único enemigo murió en el frame 2.

**Causa raíz**: `SquadFSMSystem` usa únicamente un timer hardcoded. No evalúa `DetectedEnemy.buffer.IsEmpty` ni el estado de los targets.

**Archivo**: `Assets/Scripts/Squads/Systems/SquadFSM.System.cs` — condición de salida de InCombat

---

#### BUG-009 — No hay distinción de comportamiento local vs remote en el FSM del squad al recibir órdenes durante combate
**Severidad**: Media
**Afecta**: Lógica de prioridad de órdenes

**Síntoma**: `SquadFSMSystem` y `SquadOrderSystem` tratan de forma idéntica las órdenes provenientes del jugador local y las generadas por `HeroAIExecutionSystem`. No existe ningún flag o componente que marque "esta orden viene de un jugador humano con intención insistente" vs "esta orden viene del AI y debe ceder al combate activo".

**Causa raíz**: `SquadInputComponent.hasNewOrder` es un bool sin metadata de origen. No hay:
- Campo `isPlayerForced` en `SquadInputComponent`
- Distinción en `SquadOrderSystem` entre squads de `IsLocalSquadActive` vs squads de `HeroAITag`
- Prioridad diferenciada en el FSM para cada origen de orden

**Archivos**:
- `Assets/Scripts/Squads/Components/SquadInput.Component.cs` — falta campo de origen/prioridad
- `Assets/Scripts/Squads/Systems/SquadOrder.System.cs` — falta distinción local vs remote
- `Assets/Scripts/Squads/Systems/SquadFSM.System.cs` — falta rama de lógica por origen de orden

---

### 6.4 Resumen de bugs — Sistema de órdenes y combate

| ID | Severidad | Fix sugerido |
|----|-----------|-------------|
| BUG-006 | **Alta** | En `SquadFSMSystem`, bloquear transición desde `InCombat` para squads de remote heroes mientras `DetectedEnemy` buffer no esté vacío. |
| BUG-007 | **Alta** | Añadir `ordenRepeatCount` + `ordenRepeatTimer` a `SquadStateComponent`. En `SquadControlSystem`, detectar N pulsaciones de la misma orden en ventana T y activar `heroOrdenCooldownActive`. En FSM, ese flag fuerza salida de `InCombat` ignorando enemigos. |
| BUG-008 | Media | Reemplazar timer hardcoded por condición compuesta: `timer > minDuration && DetectedEnemy.IsEmpty`. |
| BUG-009 | Media | Añadir `bool isPlayerForced` a `SquadInputComponent`. `SquadControlSystem` lo setea a `true`. FSM lo usa para decidir si una orden puede interrumpir `InCombat`. |
