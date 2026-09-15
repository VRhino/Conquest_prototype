# System Responsibilities & Data Flow

Este archivo documenta las responsabilidades específicas de cada sistema ECS y controlador MonoBehaviour del juego. Cada sistema tiene **una sola responsabilidad** — no agregar concerns fuera de su scope.

---

## ECS Systems (Hero)

### HeroInputSystem
- **Responsabilidad**: Captura raw input del jugador (WASD, mouse, hotkeys)
- **Output**: `HeroInputComponent`, `HeroMoveIntent`
- **Regla**: Solo lee input — nunca modifica estado de héroe ni squads

### HeroMovementSystem
- **Responsabilidad actual**: Calcula intención desde input, cámara y stats, después de input y spawn.
- **Output**: `HeroMoveIntent`; lo limpia si el héroe está muerto, sin ubicación de spawn o sin cámara.
- No modifica la pose: el movimiento físico pertenece a `LocalHeroCharacterMotor`.

### HeroRespawnSystem
- Lee `HeroHealthComponent`, después de daño y antes de spawn. Detecta muerte, cuenta el cooldown y solicita ubicación poniendo `hasSpawned = false` al revivir.

### HeroSpawnSystem
- **Responsabilidad**: Crea la entidad ECS del héroe local al inicio de la batalla
- Publica `spawnPosition`, `spawnRotation` y una nueva `positionRevision` al colocar al héroe. Sin punto válido no confirma ubicación ni incrementa revisión.
- **Output**: entidad héroe con todos sus componentes + `HeroSquadSelectionComponent` linkeando a la escuadra activa (`instanceId = 0`)
- **Nota**: el `instanceId = 0` debe mantenerse sincronizado con `BattleSceneController.SyncBattleDataToECS` que asigna ID 0 a la escuadra activa

### HeroVisualInstantiationSystem
- **Responsabilidad**: Instancia el prefab visual del héroe y configura `EntityVisualSync`, hitbox y NavMeshAgent
- **Output**: `HeroVisualInstance`, GameObject con `EntityVisualSync` configurado
- **NavMeshAgent**: Para héroes remotos intenta `Warp(position)` y, si falla, proyecta antes de repetir; una colocación imposible se advierte una vez
- **Proceso post-ECB**: Recolecta `NavMeshAgent` en lista durante OnUpdate, adjunta tras playback del ECB
- **Usa**: `VisualPrefabRegistry`, `VisualSyncUtility.SetupVisualSync()`

### HeroVisualAppearanceSystem
- **Responsabilidad**: Aplica customización de avatar (head, hair, beard, eyebrow) y equipamiento visual al spawn
- **Output**: `HeroVisualAppearanceApplied` tag — garantiza que la apariencia se aplica una única vez por héroe
- **Cubre**: héroe local (vía `PlayerSessionService.SelectedHero`) y héroe remoto (vía `HeroAppearanceComponent`)

### HeroVisualEquipmentSystem
- **Responsabilidad**: Actualiza visualmente el equipamiento del héroe local en tiempo real (equip/unequip)
- **Trigger**: eventos `InventoryManager.OnItemEquipped` / `OnItemUnequipped`
- **Usa**: `AvatarVisualUtils.ToggleArmorVisibilityByAvatarPartId`, `AvatarVisualUtils.UnequipSlotVisual`

---

## ECS Systems (Squads)

### SquadControlSystem
- **Responsabilidad**: Captura órdenes de squad del jugador (C, X, V, F1-F4)
- **Output**: `SquadInputComponent` y `SquadPlayerOrderIntentComponent`, antes del resolvedor. Cambiar solo formación no reemite movimiento.

### HeroAIExecutionSystem
- Controla el `NavMeshAgent` remoto y publica `SquadAIOrderIntentComponent` antes del resolvedor.
- Proyecta objetivos, comprueba `SetDestination`/`pathStatus`, retiene fallos hasta que el objetivo cambie y limpia `HeroMoveIntent` si no puede navegar.
- Conserva solicitudes de escuadra durante combate; no arbitra ni escribe entrada legacy.

### SquadOrderSystem
- **Responsabilidad**: Aplica `SquadResolvedOrderComponent` a estado y componente hold, con playback antes del cálculo del ancla.
- **Output**: `SquadStateComponent`
- No confirma `FormationComponent.currentFormation`.

### SquadFSMSystem
- **Responsabilidad**: Gestiona transiciones de estado del squad (Following/Holding/Retreating)
- **Regla**: Solo transiciones — no mueve unidades
- Mantiene retiradas comprometidas incluso tras la última baja. No inicia retirada por un booleano del dueño sin componentes operativos.

### SquadOwnerDeathRetreatSystem
- Después de HeroRespawn y antes de órdenes/intercambio/HUD, conecta HeroLife con una retirada persistente y actualiza lastOwnerAlive.
- Sin punto aliado, conserva petición y ancla. No reinicia retiradas de intercambio ni permite duplicación al revivir el dueño.
- RetreatLogic persiste efectivos y limpia la referencia solo si sigue apuntando a la escuadra retirada. Spawning excluye dueño muerto y reservas eliminadas.

### FormationSystem
- **Responsabilidad**: Calcula posiciones de formación según tipo y centro del squad
- **Output**: `UnitTargetPositionComponent` por unidad
- Consume `desiredFormation` independientemente de movimiento; confirma formación, slots, espaciado y cooldown tras validar capacidad del patrón.
- Usa centro fraccional y rotación normalizada. En cambios explícitos reasigna slotIndex; la actualización continua conserva esos índices tras bajas.

### UnitFormationStateSystem
- **Responsabilidad**: Gestiona todos los cambios de estado de unidades (Moving/Formed/Waiting)
- **Regla**: Único owner de transiciones de estado de unidades
- Decide por error de cada unidad respecto a su slot; no usa la unidad más alejada como gate global. Umbrales en SquadSpawnConfig.

### UnitNavMeshSystem
- **Responsabilidad**: Única autoridad para decisiones NavMesh por unidad: destino + rotación
- **Orden**: `[UpdateAfter(UnitFormationStateSystem)]` `[UpdateBefore(UnitFollowFormationSystem)]`
- **Owner exclusivo**: `agent.SetDestination()` y `agent.updateRotation`
- **Destino**: formación slot (default) ó stop-point cerca del target (si hay combatTarget y orden ≠ HoldPosition)
- Mantener posición preserva el ancla y los slots incluso en combate. Seguimiento aplica leash también en `InCombat`; `Attack` permite perseguir. Sin objetivo elegible se vuelve al slot respetando `Waiting`.
- Proyecta slots y stop-points al NavMesh. `NavAgentComponent` conserva el destino efectivo; fallos de proyección/path usan fallback estable y reintentan cuando cambia el slot.
- **Rotación combate**: Si dist ≤ 3.5u → `updateRotation=false` + rota `LocalTransform` para mirar al target
- **Rotación normal**: `updateRotation=true` — NavMesh controla la orientación durante movimiento
- **`UnitTargetPositionComponent`**: solo lectura — nunca escribe (ownership exclusivo de sistemas de formación)
- **Llegada**: `UnitFormationStateSystem` y `SquadNavigationSystem` comparan contra el destino efectivo asociado al slot solicitado.

### UnitFollowFormationSystem
- **Responsabilidad**: Mueve unidades sin NavMesh + aplica rotación Formed para unidades NavMesh
- **Output**: `LocalTransform` (non-NavMesh), `navAgent.transform.rotation` (NavMesh Formed state)
- **Orden**: corre DESPUÉS de `UnitNavMeshSystem` — su rotación Formed es el último write (prioridad más alta)
- **Regla**: Nunca cambia el estado de unidades — solo las mueve/orienta

### SquadVisualManagementSystem
- **Responsabilidad**: Instancia prefabs visuales de unidades y configura `EntityVisualSync`
- **Usa**: `VisualPrefabRegistry`, `VisualSyncUtility.SetupVisualSync()`

### UnitBodyblockSystem
- **Responsabilidad**: Repulsión física per-frame entre entidades de equipos distintos via `agent.Move()`
- **Cubre**: unidades vs unidades + héroes remotos vs unidades (héroe local bloqueado por CapsuleCollider físico)
- **Fuerza**: valores configurables en `SquadSpawnConfig`; por defecto 60 para Line/Testudo/Wedge/Square en estado Formed y 8 para Dispersed/Column
- **Regla**: Solo cross-team — aliados nunca se repelen; `Formed vs Formed` sin push (evita vibración)
- **Algoritmo**: Spatial grid (cell = `bodyblockRadius`) → 9 celdas vecinas → O(n×k); buckets administrados reutilizados mediante pool
- **Orden**: después de `UnitNavMeshSystem` y antes de `NavMeshPositionSyncSystem`, para capturar la corrección en ECS en el mismo ciclo
- **Tiempo**: clamp en m/s mediante `bodyblockMaxPushSpeed * deltaTime`; parámetros en SquadSpawnConfig.
- **Solapamiento exacto**: dirección determinista por par de entidades; no se omite el contacto.
- **Ref**: `Docs/Mechanics/BodyblockSystem.md`

### FormationStanceSystem
- **Responsabilidad**: Lee milestone tags de pulso 1 frame (`UnitStartedMovingTag`, `UnitArrivedAtSlotTag`) → actualiza `UnitFormationStanceComponent` + propaga `CurrentStance` y `SlotRow` a `UnitAnimationMovementComponent`
- **Orden**: `[UpdateAfter(UnitFormationStateSystem)]`
- **NO hace**: no cambia estado de formación (`UnitFormationStateComponent`), no mueve unidades, no escribe `SquadStateComponent`
- **Ref**: `Docs/Mechanics/FormationMilestoneSystem.md`

### DestinationMarkerSystem
- **Responsabilidad**: Lee estado de squad para mostrar el marcador de destino (Hold Position)
- **Regla**: Solo lee — nunca escribe componentes ECS

---

## ECS Systems (Combat)

Pipeline de combate en orden de ejecución por frame:
```
EnemyDetection ──→ DamageCalculation ──→ [SquadAISystem] ──→ [SquadFSMSystem]
                        ↓ (UnitDeath)         ↓                    ↓
                                        UnitTargeting        BraceWeaponActivation
                                        CombatReaction            ↓
                                              ↓              BraceWeapon
                                        OrderResolution
                                              ↓
                                        UnitAttack → PendingDamageEvent → (next frame DamageCalculation)
                                        BlockRegen
```

### EnemyDetectionSystem
- **Responsabilidad**: Detecta squads y entidades enemigas en rango y publica dos buffers compartidos por squad
- **Output**: `DetectedEnemy` (squads enemigos) y `SquadTargetEntity` (candidatos individuales)
- **Orden**: `[UpdateBefore(SquadAISystem)]` — SquadAI necesita detección fresca para decidir intents
- **Regla**: Solo escribe buffers compartidos de detección — nunca decide comportamiento ni asigna targets de unidades
- **Algoritmo**: Distancia centroide (sin AABB/physics queries)

### DamageCalculationSystem
- **Responsabilidad**: Aplica `PendingDamageEvent` con mitigación flat-reduction por tipo de daño
- **Output**: `HeroHealthComponent` / unit health reducido; elimina `PendingDamageEvent`; activa `IsUnderAttackTag`
- **Orden**: `[UpdateBefore(SquadAISystem)]` — el daño debe resolverse antes de que el AI tome decisiones del frame
- **Fórmula**: `contrib = max(rawDmg - netDefense, rawDmg * 0.05)` por tipo {Blunt, Slashing, Piercing}; bonuses cinético y de altura aplicados post-mitigación
- **Shield check**: Bloquea hits si `HasComponent<UnitShieldComponent> && currentBlock > 0` — corre antes del daño; solo entidades cuyo prefab tiene `ShieldHitboxBehaviour` reciben este componente
- **Regla**: Solo lee `PendingDamageEvent` — nunca decide cuándo atacar ni genera eventos de daño

### UnitDeathSystem
- **Responsabilidad**: Destruye entidades de unidades que llegaron a 0 HP tras `DamageCalculationSystem`
- **Output**: Entidades eliminadas del World
- **Orden**: `[UpdateAfter(DamageCalculationSystem)]`
- **Regla**: Solo reacciona a health ≤ 0 — no aplica daño

### UnitTargetingSystem
- **Responsabilidad**: Asigna un target enemigo específico a cada unidad del squad según estado `InCombat`
- **Input compartido**: `SquadTargetEntity`; no existe una copia de candidatos por unidad
- **Output**: `UnitCombatComponent.target` por unidad
- **Orden**: `[UpdateAfter(SquadAISystem)]` `[UpdateAfter(SquadFSMSystem)]`
- **Gate**: Solo asigna targets si `SquadFSMState.InCombat` — las tres formas de entrar en combate (tecla V, daño recibido, aliado impactado) convergen correctamente en el FSM
- **Regla**: No decide cuándo entrar en combate — solo distribuye targets cuando ya está en `InCombat`

### CombatReactionSystem
- **Responsabilidad**: Convierte `SquadCombatStateComponent.isInCombat` en `SquadCombatReactionIntentComponent` para el pipeline de órdenes
- **Output**: `SquadCombatReactionIntentComponent.reactToEnemy`, `.reactTarget`
- **Orden**: `[UpdateAfter(SquadAISystem)]` `[UpdateBefore(OrderResolutionSystem)]`
- **Regla**: Solo traduce estado de combate a intent — no toma decisiones propias

### OrderResolutionSystem
- **Responsabilidad**: Árbitro de órdenes — resuelve el intent ganador entre Player, CombatReaction y AI
- **Output**: `SquadResolvedOrderComponent` (orden ganadora del frame)
- **Orden**: `[UpdateAfter(CombatReactionSystem)]` `[UpdateBefore(SquadOrderSystem)]`
- **Prioridad local**: ante nueva orden, insistencia activa o HoldPosition conserva Player; en otro caso la reacción puede sustituirla.
- **Prioridad remoto**: HoldPosition conserva AI; para otras órdenes una reacción puede sustituirlas temporalmente. Sin reacción vuelve a la intención conservada.
- Detecta cambios de destino, objetivo y formación además de tipo/fuente para órdenes remotas.
- **Regla**: Solo lee intents y escribe el resultado — nunca modifica estado de squad directamente

### BraceWeaponActivationSystem
- **Responsabilidad**: Detecta cuando un squad entra en `HoldingPosition` y activa `Brace` mode
- **Output**: `SquadCombatModeComponent.mode = Brace` / `Normal`
- **Orden**: `[UpdateAfter(SquadFSMSystem)]` — debe reaccionar a transiciones de estado del frame actual
- **Regla**: No aplica weapon overrides — solo activa/desactiva el modo

### BraceWeaponSystem
- **Responsabilidad**: Aplica overrides de arma por fila (`BraceRowProfile`) cuando el squad está en Brace mode
- **Output**: `UnitWeaponComponent` (shape/timing de arma) por unidad según su fila en la formación
- **Orden**: `[UpdateAfter(BraceWeaponActivationSystem)]`
- **Regla**: Solo aplica overrides cuando `mode == Brace` — no activa ni desactiva el modo

### UnitAttackSystem
- **Responsabilidad**: State machine de ataque por unidad — 3 fases: Decision → Strike window → Cooldown
- **Output**: `WeaponHitboxActiveTag` (enable/disable), `PendingDamageEvent` generado por `WeaponHitboxBehaviour`
- **Orden**: `[UpdateAfter(UnitTargetingSystem)]` — necesita `combatTarget` asignado para decidir ataque
- **Fases**:
  - Decision: target válido + en rango OBB + `!isAttacking` + cooldown=0 → `isAttacking=true`
  - Strike window: timer en `[strikeWindowStart, strikeWindowStart+strikeWindowDuration]` → habilita `WeaponHitboxActiveTag`
  - End: timer ≥ `attackAnimationDuration` → `isAttacking=false`, aplica `attackInterval` cooldown
- **Regla**: No aplica daño directamente — el tag es la gate que `WeaponHitboxBehaviour` lee

### BlockRegenSystem
- **Responsabilidad**: Regenera `UnitShieldComponent.currentBlock` con `regenRate * dt` por frame
- **Output**: `UnitShieldComponent.currentBlock` (incremento hasta `maxBlock`)
- **Orden**: `[UpdateAfter(DamageCalculationSystem)]` — la regen ocurre DESPUÉS del daño del frame
- **Regla**: Solo regenera — nunca bloquea hits (eso lo hace `DamageCalculationSystem`)

---

## MonoBehaviour Controllers

### BattleSceneController
- **Responsabilidad**: Inicialización de batalla, loading screen, victory/defeat UI, transición a PostBattleScene
- **Inicialización**:
  - Lee `BattleTransitionData`, resetea `DialogueUIState.IsDialogueOpen = false`
  - Fallback a `TestEnvironmentInitializer` si no hay `BattleData`
  - Llama `ConfigureCameraLayerCulling()` en Awake
- **Layer culling**: Units a 120 m, Heroes a 150 m; `layerCullSpherical = true`
- **Loading screen**: Descartada cuando todos los `HeroVisualInstance` están listos + 3 s delay; timeout de seguridad a 30 s
- **Victory/Defeat**: Monitorea `MatchStateComponent.EndMatch`, activa `_victoryDefeatPanel`, guarda `WinnerTeam` en `BattleTransitionData`
- **Timer expired**: defensores ganan (`winnerTeam = 2`)

### EntityVisualSync
- **Responsabilidad**: vincula entidad y representación y sincroniza la pose de visuales no locales.
- Decide local/remoto desde `IsLocalPlayer`, pero delega toda locomoción local a `LocalHeroCharacterMotor`.
- Delega la presentación de héroes remotos a `RemoteHeroAnimationDriver`; no escribe parámetros de locomoción remota.
- Nunca llama `CharacterController.Move()` ni escribe la pose local en ECS.

### LocalHeroCharacterMotor
- **Responsabilidad**: único ejecutor físico del héroe local.
- **Input**: `HeroMoveIntent`, `HeroLifeComponent` y revisiones de `HeroSpawnComponent`.
- **Ejecución**: gravedad, suelo, escalones y colisiones mediante `CharacterController.Move()`.
- **Output confirmado**: `LocalTransform` y `HeroMotorStateComponent` (velocidad real, grounded, laterales y techo).
- `EcsAnimationInputAdapter` usa la velocidad horizontal y grounded confirmados; el input conserva dirección y eventos de sprint, pero ya no puede activar carrera si el controller está bloqueado.
- **Safe teleport**: deshabilita temporalmente el controller, aplica la pose, reinicia gravedad y consume una sola vez cada revisión.
- El motor no se crea ni permanece activo para héroes remotos.

### RemoteHeroAnimationDriver
- **Responsabilidad**: presentación de locomoción y combate del héroe remoto.
- **Input confirmado**: velocidad del `NavMeshAgent`, `HeroAIDecision`, `HeroAnimationComponent` y `HeroCombatComponent`.
- **Output**: parámetros y triggers del `Animator`; no escribe transforms, destinos ni intención ECS.
- Deshabilita los adaptadores de input/animación local para evitar dos escritores sobre el mismo Animator.
- Sólo se instala para entidades remotas con `HeroMoveIntent`; los visuales de unidades no reciben este componente.

---

## Shared Utilities

### VisualSyncUtility (`Assets/Scripts/Shared/VisualSyncUtility.cs`)
- `SetupVisualSync(GameObject)` — configura `AnimatorCullingMode.CullCompletely` y añade/obtiene `EntityVisualSync`
- Usada por: `HeroVisualManagementSystem`, `SquadVisualManagementSystem`

### GameTags (`Assets/Scripts/Shared/GameTags.cs`)
- Constantes de tags de Unity: `Player = "Player"`, `Terrain = "Terrain"`
- Evita magic strings dispersos en sistemas

### HeroPositionUtility
- Recuperación de posición del héroe — usado por 6+ sistemas

### FormationPositionCalculator
- Matemáticas de formación y cálculo de altura de terreno (`calculateTerraindHeight`)

### UnitStatsUtility
- Aplicación de stats para sistemas de progresión
- Sincroniza también salud, defensa y perfil de daño usados por combate.
- Conserva el porcentaje de salud y el estado de munición/recarga.

### BattleBootstrapSystem
- Consume peticiones administradas del controlador de batalla; es dueño de las escrituras del bootstrap local en ECS.
- Conserva identidad persistente, progreso y efectivos en SquadIdMapElement.
- Los mapas de escuadras remotas pertenecen a cada héroe, no al singleton local.

### SquadProgressionSystem / UnitStatScalingSystem
- Progresión consume SquadXPEvent una vez; no genera recompensas por permanecer en PostPartida.
- El escalado inicial ocurre una vez por instancia y después solo para las escuadras indicadas por eventos.
- La fórmula y entrega autenticada de BattleResult siguen pendientes; estos eventos son locales.

## Estado de migración de movimiento — 2026-09-14

Actualización geométrica: centro fraccional, validación segura de blobs/cuaterniones, slots estables y movimiento del ancla por velocidad. Referencia y límites: `Docs/Arquitectura/7_Formacion_Geometria_FPS_2026-09-14.md`.

Actualización posterior: conectada la retirada por muerte del dueño con espera persistente de destino y conservación de efectivos. Referencia y límites: `Docs/Arquitectura/6_Retirada_Muerte_Dueno_2026-09-14.md`. Los párrafos históricos siguientes describen el alcance anterior de intercambio.

Actualización de intercambio: `SquadSwapExecutionSystem` valida reemplazo antes de retirar y consume cooldown solo al aceptar. `SquadNavigationSystem` ahora observa llegada sin escribir destinos; `RetreatLogicSystem` espera a todos los supervivientes en sus slots o timeout. `SquadOrderSystem` respeta el bloqueo de retirada. La retirada por muerte del dueño sigue pendiente. Referencia: `Docs/Arquitectura/5_Intercambio_Retirada_2026-09-14.md`.

El contrato local vigente es `HeroMovementSystem → HeroMoveIntent → LocalHeroCharacterMotor → LocalTransform/HeroMotorStateComponent`. `EntityVisualSync` ya no ejecuta movimiento local ni conduce la animación remota. El respawn conserva pose revisionada. Referencias: fases 4, 10, 14 y 15 en `Docs/Arquitectura/`.
