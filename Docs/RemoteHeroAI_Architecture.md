# Remote Hero AI — Arquitectura

> Revisado línea a línea contra el código el 2026-10-22 (anterior: 2026-09-15, `3eef0e17`).
> Diseño del sistema de comportamiento para héroes remotos (non-local-player).
> Todos los behaviors compiten para **ganar la partida**. La diferencia es la sofisticación táctica.

---

## Principio de diseño

Cada behavior es un archivo `.System.cs` aislado. Agregar o quitar un behavior = agregar o quitar un archivo. Zero cambios en Perception, Execution, ni en ningún sistema existente.

La capa de Execution reutiliza `HeroMoveIntent` como descripción de movimiento, pero la autoridad física remota es el `NavMeshAgent`. Para órdenes de squad publica `SquadAIOrderIntentComponent`, que compite en `OrderResolutionSystem`; para presentación, `RemoteHeroAnimationDriver` consume la velocidad confirmada del agente.

**Separación QUÉ hay / QUÉ me importa / QUÉ hago**:
- `BattleWorldState` → **QUÉ hay** en el mundo (datos crudos, filtrados por visibilidad)
- `HeroAIPerception` → **QUÉ me importa a MÍ** de esos datos (distancias, proximidad per-hero)
- `Behavior Systems` → **QUÉ hago** con esa información (decisiones tácticas)

---

## Arquitectura — 4 capas

```
 DATA SERVICE                      PERCEPTION                        DECISION (swappable)              EXECUTION
 BattleWorldState.System  ──►  HeroAIPerception.System  ──►  HeroAIRusher.System      ──►  HeroAIExecution.System
 Lee: mundo ECS completo         Reloj de decisión (0,1 s)         HeroAIBalanced.System          Lee: HeroAIDecision
 Filtra: fog-of-war via          Lee: TeamWorldState            (Tactician: no existe) Escribe:
 DetectedEnemy buffers           Calcula: derived per-hero      (solo 1 corre por entidad)       → HeroMoveIntent
 Escribe: TeamWorldState         Escribe: HeroAIBlackboard      Lee: TeamWorldState +            → SquadAIOrderIntentComponent
 (singleton managed)             (agnóstico de intención)       HeroAIBlackboard                → NavMeshAgent.SetDestination
                                                                Escribe: HeroAIDecision          │
                                                                                                 ▼
                                                                                       Pipeline compartido por contratos
```

---

## Componentes

Ubicación: `Assets/Scripts/Hero/AI/Components/`

| Componente | Tipo | Descripción |
|-----------|------|-------------|
| `TeamWorldState` | Managed singleton | Servicio de datos centralizado. Vistas por equipo con filtro de visibilidad. Agnóstico de intención. |
| `HeroAITag` | Tag struct | Marca héroes AI. Nunca en local player. |
| `RusherBehaviorActive` | IEnableableComponent | Tag que activa el behavior Rusher en este héroe. Swappable en runtime. |
| `BalancedBehaviorActive` | IEnableableComponent | Tag que activa el behavior Balanced en este héroe. Swappable en runtime. |
| `IsAttackerRole` | Tag struct | Marca héroes con rol atacante según match config. |
| `HeroAIBlackboard` | Managed class | Output de Perception. Solo datos derived per-hero (distancias, proximidad). Sin listas. |
| `HeroAIDecision` | Struct | Output de Decision. Interfaz estable que Execution traduce a comandos. |

### TeamWorldState — estructura

```
Singleton managed. Helpers: ws.For(myTeam) / ws.EnemyOf(myTeam) / ws.SpawnFor(myTeam)

TeamView (una por equipo):
  allyHeroes[]           — HeroSnapshot de TODOS los héroes del equipo (incluye jugador local)
  visibleEnemyHeroes[]   — HeroSnapshot de héroes enemigos detectados por fog-of-war
                           ⚠ El héroe local TAMBIÉN aparece aquí para el equipo contrario
                             si algún squad enemigo lo tiene en su DetectedEnemy buffer.
                             No hay trato especial entre héroes locales y AI.
  allySquads[]           — SquadSnapshot con posición, tipo, estado de combate
  visibleEnemySquads[]   — SquadSnapshot filtrado por fog-of-war

Compartido (sin fog-of-war, siempre visible para ambos equipos):
  zones[]       — ZoneSnapshot: entity, position, radius, teamOwner,
                  captureProgress, isContested, isBeingCaptured, isLocked, isFinal, zoneType
  match         — MatchContext: isActive, stateTimer, winnerTeam, state
  spawnPositionTeamA / spawnPositionTeamB
```

> **Regla de visibilidad**: un héroe (local o AI) aparece en `allyHeroes` de su equipo siempre.
> Aparece en `visibleEnemyHeroes` del equipo contrario **solo si** algún squad del equipo contrario
> lo tiene en su buffer `DetectedEnemy` (poblado por `EnemyDetectionSystem`). Fog-of-war gratuito.

### HeroAIBlackboard — campos clave

```
Self:       selfPosition, selfHealthPercent, selfStaminaPercent, selfIsAlive, selfTeam, selfIsAttacker
Own Squad:  ownSquadEntity, hasSquad, ownSquadType, ownSquadIsRanged, squadIsInCombat, squadCurrentOrder
Match:      matchIsActive, winnerTeam
Derived:    nearestEnemyHero, nearestEnemyPosition, nearestEnemyDistanceSq
            bestObjectiveZone, bestObjectivePosition
            threatZone, threatZonePosition
            isInsideAnyZone, zoneImInside, zoneImInsideInfo (ZoneSnapshot)
            spawnPosition, spawnPositionCached
```

> Las listas de enemigos, aliados y zonas ya **no** están en el Blackboard.
> Los behaviors leen `TeamWorldState` directamente para datos completos del equipo.

### HeroAIDecision — campos

```
action:             AIActionType  { MoveTo, AttackTarget, CaptureZone, DefendZone, Retreat, Idle }
targetPosition:     float3
targetEntity:       Entity
shouldSprint:       bool
shouldAttack:       bool
squadOrder:         SquadOrderType
squadOrderPosition: float3
hasNewSquadOrder:   bool
```

---

## Sistemas

Ubicación: `Assets/Scripts/Hero/AI/Systems/`

### BattleWorldState.System — Data Service
- Corre **todos los frames** y publica la fase del match (`MatchContext.isActive` solo es true en `MatchState.InBattle`; `state` lleva la fase real)
- **Agnóstico de intención**: nunca calcula objetivos, amenazas ni prioridades
- Crea el singleton `TeamWorldState` en `OnCreate`
- 4 passes por frame:
  1. **Visibilidad** — agrega buffers `DetectedEnemy` de todos los squads aliados → HashSet por equipo de entidades visibles
  2. **Héroes** — snapshot de todos los héroes (local + AI); se añaden a `allyHeroes` de su equipo y a `visibleEnemyHeroes` del contrario si están detectados
  3. **Squads** — snapshot de todos los squads con posición (`SquadFormationAnchorComponent`), tipo, estado de combate; misma lógica de visibilidad
  4. **Zonas** — snapshot de zonas activas (Capture + Supply); siempre visibles para ambos equipos

### HeroAIPerception.System
- Reloj de decisión (`AIThinkClock`, L4-01): cada héroe IA piensa cada `heroAIThinkInterval` s (0,1 s) de tiempo de simulación, con fase propia por hash de su entidad y como mucho `heroAIMaxThinksPerFrame` (8) por frame, los más atrasados primero. Antes: `Time.frameCount % 5`, todos en el mismo frame y dependiente de los FPS.
- **Lee**: `TeamWorldState` (datos ya recolectados — no hace queries propios al mundo ECS)
- Calcula derived per-hero: distancias a enemigos visibles, `isInsideZone`, `bestObjectiveZone`, `threatZone`, `nearestEnemyHero`
- `isUnderAttack` (enemigo a menos de 20 m o escuadra en combate) pasa por un `DebouncedGate` (L4-02): se abre tras `heroAIUnderAttackOpenTime` (0,3 s) seguidos y se cierra tras `heroAIUnderAttackCloseTime` (1 s) sin la condición, para que los bots no cambien de rama por un tick.
- **Escribe**: `HeroAIBlackboard` (solo datos derived per-hero, sin listas)

### HeroAIRusher.System — Behavior: Rush Objectives
- Rush directo a objetivos. Pelea solo si le bloquean el paso.
- Lógica (por prioridad):
  1. No vivo → Idle
  2. Dentro de zona capturándola → quedarse, squad HoldPosition en zona
  3. Enemigo hero < 10m bloqueando → AttackTarget, squad Attack
  4. `bestObjectiveZone` existe → sprint hacia ella, squad FollowHero
  5. Fallback → Idle

### HeroAIBalanced.System — Behavior: Threat-Aware
- Evalúa amenazas. Defiende zonas propias. Se retira si está en desventaja.
- Lee `TeamWorldState` para HP del enemigo más cercano (advantage check).
- Lógica (por prioridad):
  1. No vivo → Idle
  2. HP < 30% → Retreat hacia spawn, squad FollowHero
  3. Zona propia siendo capturada → DefendZone (ir a contestar)
  4. Enemigo < 15m Y ventaja HP (≥15% más o propio HP ≥ 70%) → AttackTarget, squad Attack
  5. Dentro de zona no propia → quedarse, squad HoldPosition
  6. `bestObjectiveZone` → CaptureZone, sprint solo fuera de combate
  7. Fallback → Idle, squad FollowHero

### HeroAITactician — no implementado

No existe `HeroAITacticianSystem` ni `TacticianBehaviorActive`. Solo hay un diseño de coordinación de equipo en `Docs/Design/ia-tactica/` y `tasks/tactical-ai-design-plan.md`.

### HeroAIDashSystem

Mueve a los héroes de IA durante una embestida (`HeroDashComponent`) con su `NavMeshAgent`; corre después de `SkillEffectSystem` y de `HeroAIExecutionSystem`. Ver `Docs/Mechanics/SkillsAndPerks.md`.

### HeroAIExecution.System
- Lee `HeroAIDecision`, traduce a comandos:
  - **Movimiento**: `NavMeshAgent.SetDestination()` + `HeroMoveIntent`; la animación remota deriva locomoción de la velocidad NavMesh
  - **Ataque**: no lo ejecuta; `HeroSkillRequestSystem` convierte `HeroAIDecision.shouldAttack` en una petición que pasa por la misma puerta que la del jugador (`SkillExecutionSystem`)
  - **Squad orders**: escribe `SquadAIOrderIntentComponent` vía `HeroSquadReference`; `OrderResolutionSystem` decide frente a intención del jugador y reacción de combate

---

## Orden de ejecución

```
[SimulationSystemGroup]
    HeroInputSystem              (local only, sin cambios)
    EnemyDetectionSystem         (sin cambios — puebla DetectedEnemy buffers)
    BattleWorldStateSystem       (data service, todos los frames; After EnemyDetection, HeroRespawn, CaptureZoneTrigger, SupplyInteraction, MatchController; Before SquadAI)
        ↓
    HeroAIPerceptionSystem       (AI only, reloj de decisión por héroe)
        ↓
    HeroAIRusherSystem    ┐
    HeroAIBalancedSystem  ├── solo 1 corre por entidad (IEnableableComponent tag check)
                          ┘
        ↓
    HeroAIExecutionSystem        → HeroMoveIntent + SquadAIOrderIntentComponent + NavMeshAgent
        ↓
    HeroMovementSystem           (local only, sin cambios)
    OrderResolutionSystem        (arbitra Player / CombatReaction / AI)
        ↓
    SquadOrderSystem             (aplica sólo SquadResolvedOrderComponent)
    HeroSkillRequestSystem       (input local y decisión IA → petición)
    SkillExecutionSystem         (misma puerta y fases para jugador e IA)
    NavMeshPositionSyncSystem    (publica pose remota NavMesh → ECS)
    EntityVisualSync             (vincula entidad y sincroniza pose)
    RemoteHeroAnimationDriver    (NavMesh/ECS confirmado → Animator)
```

---

## Cambios a sistemas existentes

| Sistema | Cambio | Impacto |
|---------|--------|---------|
| `BattleSceneController.SpawnRemoteHeroes()` | Agregar `HeroAITag`, `RusherBehaviorActive`/`BalancedBehaviorActive`, `IsAttackerRole`, `HeroAIBlackboard`, `HeroAIDecision`, `HeroMoveIntent` al spawn | Mínimo — solo spawn |
| `HeroSkillRequestSystem` | Bucle con `WithAll<HeroAITag>()` que pide el ataque básico desde `HeroAIDecision.shouldAttack` | Misma puerta que el jugador (Fase 2, L2-1) |
| `OrderResolutionSystem` | Arbitra intents de jugador, reacción de combate e IA y conserva su `OrderSource` | Evita escritores competidores |
| `SquadOrderSystem` | Consume únicamente la orden resuelta y respeta el bloqueo de retirada | Aplicación única |
| `HeroMovementSystem` | **Sin cambios** — AI usa NavMeshAgent, no este system | — |

---

## File structure

```
Assets/Scripts/Hero/AI/
    Components/
        HeroAITag.Component.cs
        RusherBehaviorActive.Component.cs
        BalancedBehaviorActive.Component.cs
        IsAttackerRole.cs
        HeroAIBlackboard.Component.cs
        HeroAIDecision.Component.cs
        TeamWorldState.Component.cs
    Systems/
        BattleWorldState.System.cs
        HeroAIPerception.System.cs
        HeroAIRusher.System.cs
        HeroAIBalanced.System.cs
        HeroAIExecution.System.cs
    Enums/
        AIActionType.Type.cs
```

---

## Fases de implementación

| Fase | Objetivo | Estado |
|------|----------|--------|
| 1 — Skeleton | Componentes + Perception + Rusher + Execution | ✅ Completo |
| 2 — BattleWorldState | `TeamWorldState` + `BattleWorldState.System` + refactor Blackboard y Perception | ✅ Completo |
| 3 — Tactician con squad roles | Frontline se posiciona entre enemigo y arqueros aliados; ningún archivo existe | 🔲 Pendiente |
| 4 — Polish | Tunning de thresholds, arrival distance, behavior swap en runtime | 🔲 Pendiente |

---

## Decisiones de diseño

| Decisión | Razón |
|----------|-------|
| `BattleWorldStateSystem` agnóstico de intención | Cada behavior interpreta los datos según su lógica propia. El servicio de datos no sabe qué quieren hacer los héroes. |
| Managed class para `TeamWorldState` y `Blackboard` | Contienen listas variables. Max 6 héroes (3v3) — sin concern de perf. |
| Behaviors leen `TeamWorldState` directamente | Datos completos del equipo accesibles sin que el Blackboard actúe de intermediario. Evita copias innecesarias. |
| Fog-of-war via `DetectedEnemy` buffers | `EnemyDetectionSystem` ya puebla estos buffers — reutilización 100%, sin sistema adicional. |
| El héroe local en `allyHeroes` **y** en `visibleEnemyHeroes` del contrario | Regla uniforme: todos los héroes (local o AI) siguen la misma lógica de visibilidad. |
| NavMeshAgent para movimiento | Ya está en remote heroes. Obstacle avoidance + pathfinding gratuito. |
| 1 system por behavior | Open/Closed: agregar behavior = nuevo archivo, sin tocar existentes. |
| `SquadAIOrderIntentComponent` para órdenes | Reutiliza el arbitraje común sin escribir directamente el estado aplicado ni competir con jugador/combate. |
| Petición de la IA por la misma puerta | Una sola regla para jugador e IA; `HeroSkillRequestSystem` solo convierte `shouldAttack` en el ataque básico (la IA no pide Q/E/R/F ni perks) |
| `BattleWorldState` corre todos los frames | Datos siempre frescos. `HeroAIPerception` (reloj de decisión) es la capa cara — el servicio de datos es barato. |
