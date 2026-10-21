# System Responsibilities & Data Flow

## Contratos actualizados — 2026-10-19

- SceneTransitionService aplica la política de cursor del destino tras cargar; HeroCamera/FullscreenPanelManager/SupplyPointUI la respetan. Las interfaces requieren cursor libre incluso al cerrar paneles.
- MatchmakingService conserva la escena de origen de cola; MatchmakingUI en PostBattle sólo atiende Salir y restablece el estado de cola al retornar.
- DamageCalculation → UnitDeath → EnemyDetection → BattleWorldState → decisiones/órdenes → targeting.
- HeroRespawn precede detección y captura; MatchController resuelve captura antes del snapshot.
- Un bootstrap local aceptado reinicia MatchState/rearma MatchController antes de resolver el nuevo frame.
- BattleWorldState publica fase incluso fuera de InBattle y precede SquadAI: detección/salud/zonas frescas, estado táctico del ciclo previo.
- LocalHeroCaptureTracker lee después de CaptureZoneTrigger y SupplyInteraction.
- UnitAttack/RangedAttack leen pose después de NavMeshPositionSync y excluyen atacantes muertos. Fin de batalla limpia ataques/intenciones y detiene agentes; cleanup y presentación siguen disponibles.
- `NavMeshPositionSyncSystem` captura posición de agentes con sync habilitado y snapshot físico de velocidad/límite para animación de unidades; `UnitAnimationSystem` lo consume después en el mismo grupo.
- Héroes remotos se crean con `syncPositionFromNavMesh = false`; `EntityVisualSync` mantiene para ellos el puente GO → ECS y evita `SetComponentData` cuando la pose no cambia. En unidades con sync activo publica la posición NavMesh y conserva la publicación de una rotación distinta.
- EnemyDetection construye candidatos y un índice espacial nativo reutilizable, busca vecinos/exact distance en `SearchCandidatesJob` Burst, completa el job antes de publicar los buffers en el mismo frame.
- `RemoteHeroAnimationDriver` omite escrituras de parámetros sin cambios con caché invalidada al cambiar binding/Animator/controller/estado enabled; los triggers se siguen enviando como eventos.
- `EntityVisualSync` pone `AlwaysAnimate` solo en Animator que contienen un hitbox de arma o escudo en su jerarquía; los demás mantienen culling.
- VisualSyncUtility registra instancias por World/Entity; EntityVisualSync registra/desregistra su vínculo. ProjectileSpawn entrega colliders del tirador sin búsquedas de escena.
- Bodyblock calcula los mismos pares en un job Burst con buffers nativos persistentes; sólo el puente managed aplica NavMeshAgent.Move. Su peor caso denso sigue siendo cuadrático, sin recortar contactos.
- Gestores visuales sólo actualizan con instancias pendientes; contactos y targeting reutilizan scratch. Eventos de daño nacen en su arquetipo final y conservan consumo durante el mismo frame.
- GridFormationUpdate omite geometría de slots estables mediante la revisión del ancla y una clave de orden/estado, formación, slot y terreno; remuestrea alturas al cambiar Terrain, TerrainData, origen o heightmap.
- InventoryEventService publica OnEquipmentChanged una vez por transacción de equipo exitosa o cambio de contexto. Guardar no invalida atributos; el cambio de sesión sí.
- HeroData/BattleHeroData conservan heroId; bootstrap/preparación/supply resuelven por ID y admiten sólo fallback heredado no ambiguo. Ambos archivos de guardado siguen coexistiendo.


Este archivo documenta las responsabilidades específicas de cada sistema ECS y controlador MonoBehaviour del juego. Cada sistema tiene **una sola responsabilidad** — no agregar concerns fuera de su scope.

---

## ECS Systems (Hero)

### HeroInputSystem
- **Responsabilidad**: Captura raw input del jugador (WASD, mouse, hotkeys)
- **Output**: `HeroInputComponent` y `PlayerInteractionComponent`; `HeroMovementSystem` produce después `HeroMoveIntent`
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

---

## ECS Systems (Squads)

### SquadControlSystem
- **Responsabilidad**: Captura órdenes de squad del jugador (C, X, V, F1-F4)
- **Output**: `SquadInputComponent` (flags transitorios) y `SquadPlayerOrderIntentComponent` (orden y formación solicitadas), antes del resolvedor. Cambiar solo formación no reemite movimiento.
- `SquadPlayerCommandResolver` aplica la petición sin leer dispositivos ni mutar ECS directamente; una formación inválida no descarta una orden válida simultánea.

### SquadSpawningSystem
- Crea la escuadra activa y sus unidades a partir de la definición horneada y del snapshot de campaña.
- `SquadUnitSpawnBuilder` compone los componentes iniciales de cada unidad después de que el sistema calcule su slot; no selecciona órdenes, snapshots ni posiciones.
- Al inicializar reservas, construye una tabla temporal de tamaños por ID de definición una sola vez; no recorre todas las definiciones por cada reserva.

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

### SquadAnchorSystem
- **Responsabilidad**: Único escritor de `SquadFormationAnchorComponent` (posición, rotación, velocidad) y de `SquadAnchorMovingTag`. Calcula el punto objetivo (mantener posición, retirada, distancia de tiro o héroe + desplazamiento) y lleva el ancla hasta él con `AnchorArrival` (C10: aceleración `anchorAcceleration`, frenado √(2·a·d), salto más allá de `anchorSnapDistance`); la rotación va directa (D5)
- **Orden**: `[UpdateAfter(UnitTargetingSystem)]` `[UpdateBefore(FormationSystem)]`
- **Ref**: `Docs/Design/Movimiento_Formaciones.md` L-1 y §14

### FormationSystem
- **Responsabilidad**: Único escritor de la disposición de la escuadra (`FormationComponent`: formación, plantilla, espaciado, escala, semilla) y de cada `UnitGridSlotComponent` (ranura, posición local, fila, papel, rumbo, desplazamiento local).
- **Datos**: `FormationDefinition` (plantillas por soldados vivos, espaciado propio, ruido, Scatter, tabla de posturas) horneado en `FormationLibraryBlob`; funciones puras en `FormationLayout`.
- **Cuándo rehace la disposición**: cambio de formación pedido (con cooldown), cambio de plantilla por bajas, o `SquadFormationSpacingRequest` (que consume y elimina).
- **Reparto**: en una disposición nueva, cada unidad toma la ranura libre más cercana (voraz, solo en el evento); si no cambia la plantilla, cada unidad conserva su ranura tras bajas.
- **Output**: también el `UnitTargetPositionComponent` inicial; después lo mantiene `GridFormationUpdateSystem`, que solo gira el desplazamiento local con el ancla.

### UnitFormationStateSystem
- **Responsabilidad**: Gestiona todos los cambios de estado de unidades (Moving/Formed/Waiting)
- **Regla**: Único owner de transiciones de estado de unidades
- Decide por error de cada unidad respecto a su slot; no usa la unidad más alejada como gate global. Umbrales en SquadSpawnConfig.

### UnitNavMeshSystem
- **Responsabilidad**: Única autoridad para decisiones NavMesh por unidad: destino + rotación
- **Orden**: `[UpdateAfter(UnitFormationStateSystem)]` `[UpdateBefore(UnitFollowFormationSystem)]`
- **Owner exclusivo**: `agent.SetDestination()` y `agent.updateRotation`
- **Destino**: formación slot (default) ó stop-point cerca del target (si hay combatTarget y orden ≠ HoldPosition); ya en alcance, el punto donde termina de frenar (`MeleeReach.BrakingStopPoint`), nunca "aquí" yendo deprisa
- Mantener posición preserva el ancla y los slots incluso en combate. Seguimiento aplica leash también en `InCombat`; `Attack` permite perseguir. Sin objetivo elegible se vuelve al slot respetando `Waiting`.
- Lee la orden desde `UnitOwnerComponent.squad`. Proyecta slots y stop-points al NavMesh. `NavAgentComponent` conserva el destino efectivo y usa `navMeshDestinationChangeThreshold` para evitar reemitir destinos sin cambios; fallos de proyección/path usan fallback estable y reintentan cuando cambia el slot.
- **Rotación combate**: Si dist ≤ 3.5u → `updateRotation=false` + rota `LocalTransform` para mirar al target
- **Rotación normal**: `updateRotation=true` — NavMesh controla la orientación durante movimiento
- **`UnitTargetPositionComponent`**: solo lectura — nunca escribe (ownership exclusivo de sistemas de formación)
- **Llegada**: `UnitFormationStateSystem` y `SquadNavigationSystem` comparan contra el destino efectivo asociado al slot solicitado.

### MeleeContactRingSystem
- **Responsabilidad**: Anillo de contacto (C11, L-7): reserva para cada atacante cuerpo a cuerpo una posición en el anillo de su objetivo (radio = menor distancia de parada, ⌊2π·r / (2·bodyRadius + contactRingMargin)⌋ posiciones en ángulos fijos del mundo); con el anillo lleno, espera (slot −1)
- **Owner exclusivo**: `MeleeContactSlotComponent`
- **Orden**: `[UpdateAfter(UnitTargetingSystem)]` `[UpdateBefore(UnitNavMeshSystem)]`
- **Regla**: Sin cupo duro (D6): el cupo sigue en `UnitTargetingSystem`. Se reconstruye cada frame desde los atacantes vivos; muerte o cambio de objetivo liberan la posición sin eventos. `UnitNavMeshSystem` solo lee la reserva (va a la posición, o espera a `d_reanudar + s_arco`)

### UnitFollowFormationSystem
- **Responsabilidad**: Velocidad de seguimiento y marcha (C9, `UnitMovementGearComponent`) + rotación Formed para unidades NavMesh
- **Output**: `navAgent.speed` (sube y baja como mucho a la aceleración del agente; frena hasta `brakingFactor` veces más rápido; hacia un destino quieto, como mucho √(2·a·d) para llegar frenando), intención de rotación Formed: rumbo de la formación × rumbo de la ranura
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
- **Algoritmo**: Spatial grid nativo persistente (cell = `bodyblockRadius`) → 9 celdas vecinas → O(n×k); el job Burst consume este único grid, sin duplicado managed
- **Orden**: después de `UnitNavMeshSystem` y antes de `NavMeshPositionSyncSystem`, para capturar la corrección en ECS en el mismo ciclo
- **Tiempo**: clamp en m/s mediante `bodyblockMaxPushSpeed * deltaTime`; parámetros en SquadSpawnConfig.
- **Solapamiento exacto**: dirección determinista por par de entidades; no se omite el contacto.
- **Ref**: `Docs/Mechanics/BodyblockSystem.md`

### UnitHitKnockbackSystem
- **Responsabilidad**: Retroceso del golpe (patrón de `hit_curve` de la referencia): consume `UnitHitImpactComponent` (lo habilita `DamageCalculationSystem` en cada golpe que llega al cuerpo sin matar) y empuja a la unidad lejos del atacante con `agent.Move` a lo largo de `HitKnockback` (`hitKnockbackDistance` en `hitKnockbackDuration`, smoothstep).
- **Owner exclusivo**: `UnitHitKnockbackComponent`
- **Orden**: después de `UnitBodyblockSystem`, antes de `NavMeshPositionSyncSystem`.
- **Ref**: `Docs/Design/Movimiento_Formaciones.md` §19

### UnitAvoidanceSystem
- **Responsabilidad**: Evitación por velocidad entre aliados (ORCA, C14); único escritor de la velocidad evitada (`agent.velocity`).
- **Regla**: enemigos no se esquivan (D2); dentro de la escuadra solo las unidades que se mueven, con radio combinado 0,9 × espaciado; la velocidad cambia como mucho a la aceleración del agente.
- **Orden**: después de `UnitFollowFormationSystem`, antes de `UnitBodyblockSystem` y `NavMeshPositionSyncSystem`.
- **Ref**: `Docs/Mechanics/BodyblockSystem.md`, `Docs/Design/Movimiento_Formaciones.md` §10-11

### NavMeshPositionSyncSystem
- **Responsabilidad**: captura resultados físicos del `NavMeshAgent` después del movimiento.
- **Posición ECS**: copia la posición del agente válido solo cuando `NavAgentComponent.syncPositionFromNavMesh` está activo; no escribe la rotación.
- **Snapshot de animación**: guarda validez, velocidad horizontal confirmada y `agent.speed` en `UnitAnimationMovementComponent` cuando el agente está disponible. Agentes inválidos publican velocidad cero.
- **Orden**: después de `UnitNavMeshSystem`/bodyblock y antes de `UnitRotationResolutionSystem` y `UnitAnimationSystem`.

### UnitAnimationSystem
- **Responsabilidad**: actualiza el movimiento de animación a partir de la pose del frame.
- Para unidades con snapshot de NavMesh usa la velocidad confirmada que publicó `NavMeshPositionSyncSystem`, evitando volver a consultar el componente managed; conserva el fallback por delta de pose para entidades sin snapshot de agente.
- **Orden**: después de `NavMeshPositionSyncSystem` y `UnitRotationResolutionSystem`.

### UnitPostureSystem
- **Responsabilidad**: Único escritor de `UnitFormationStanceComponent`: postura por papel de ranura según la tabla de la formación (formado / en marcha); en melé conserva la de formado. Propaga postura, fila y papel a `UnitAnimationMovementComponent`.
- **Orden**: `[UpdateAfter(UnitFormationStateSystem)]`
- **NO hace**: no cambia estado de formación, no mueve unidades.
- **Ref**: `Docs/Mechanics/FormationPostures.md`

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
- **Algoritmo**: snapshot de candidatos y broadphase XZ en contenedores nativos persistentes; `SearchCandidatesJob` Burst filtra por equipo y distancia exacta. Ordena índices para conservar el orden de candidatos antes de publicar los buffers.
- **Sincronicidad**: completa el job antes de publicar `DetectedEnemy` y `SquadTargetEntity` en el mismo frame de simulación.

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
- **Responsabilidad**: Convierte `SquadAIComponent.isInCombat` en `SquadCombatReactionIntentComponent` para el pipeline de órdenes
- **Output**: `SquadCombatReactionIntentComponent.reactToEnemy`, `.reactTarget`
- **Orden**: `[UpdateAfter(SquadAISystem)]` `[UpdateBefore(OrderResolutionSystem)]`
- **Regla**: Solo traduce estado de combate a intent — no toma decisiones propias

### OrderResolutionSystem
- **Responsabilidad**: Árbitro de órdenes — resuelve el intent ganador entre Player, CombatReaction y AI
- **Output**: `SquadResolvedOrderComponent` (orden ganadora del frame)
- **Orden**: `[UpdateAfter(CombatReactionSystem)]` `[UpdateBefore(SquadOrderSystem)]`
- **Prioridad local**: ante nueva orden, insistencia activa o HoldPosition conserva Player; en otro caso la reacción puede sustituirla.
- **Prioridad remoto**: HoldPosition conserva AI; para otras órdenes una reacción puede sustituirlas temporalmente. Sin reacción vuelve a la intención conservada.
- Detecta cambios de destino y objetivo además de tipo/fuente para órdenes remotas. La formación viaja en el resultado sin reemitir movimiento.
- **Regla**: Solo lee intents y escribe el resultado — nunca modifica estado de squad directamente

### BraceWeaponActivationSystem
- **Estado actual**: La activación automática de `Brace` está deshabilitada (`shouldBrace = false`). Si un squad estaba en `Brace`, lo devuelve a `Normal`.
- **Orden**: `[UpdateAfter(SquadFSMSystem)]`
- **Regla**: No aplica weapon overrides.

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
- **Reloj**: solo muestra `BattleClockComponent` (L7-1); no cuenta ni escribe el resultado

### BattleClockSystem
- **Responsabilidad**: Descuenta `BattleClockComponent` en `InBattle`; al llegar a 0 emite `BattleTimeExpiredEvent` una vez (L7-1)
- **Orden**: después de `BattleBootstrapSystem`, antes de `MatchControllerSystem`

### MatchOutcomeSystem
- **Responsabilidad**: Único escritor de `MatchStateComponent.winnerTeam` y `victoryConditionMet` (L7-2): consume `FinalZoneCapturedEvent` y `BattleTimeExpiredEvent` con orden fijo (gana la captura) y nunca sobrescribe un resultado decidido
- **Orden**: después de `CaptureZoneTriggerSystem` y `BattleClockSystem`, antes de `MatchControllerSystem`

### EntityVisualSync
- **Responsabilidad**: vincula entidad y representación y sincroniza la pose de visuales no locales.
- Decide local/remoto desde `IsLocalPlayer`, pero delega toda locomoción local a `LocalHeroCharacterMotor`.
- Delega la presentación de héroes remotos a `RemoteHeroAnimationDriver`; no escribe parámetros de locomoción remota.
- Nunca llama `CharacterController.Move()`. Con NavMesh válido, si `syncPositionFromNavMesh` es `true` no copia posición al ECS (lo hace `NavMeshPositionSyncSystem`); puede publicar una rotación distinta. Para héroes remotos con flag `false`, conserva el puente GO → ECS de posición/rotación y escribe solo si alguno cambió. En visuales sin NavMesh válido mantiene el flujo ECS → GO.
- Consume el pulso de ataque del héroe local; no cambiar de propietario sin coordinar el consumidor del Animator.
- Protege fuera de cámara únicamente Animator antecesores de hitboxes de arma/escudo con `AlwaysAnimate`; los Animator sin esos hitboxes conservan `CullCompletely`.
- Vincula `HeroVisualEquipmentInitializer` al visual local; no aplica cambios de inventario.

### HeroVisualEquipmentInitializer
- **Responsabilidad**: aplica la apariencia del equipamiento confirmado al visual local vinculado por `EntityVisualSync`.
- **Trigger**: `InventoryManager.OnInventoryChanged`; sólo actualiza cuando cambia el conjunto de objetos equipados.
- Restaura las piezas base retiradas y aplica las piezas finales con `AvatarVisualUtils`; no busca todos los GameObjects ni modifica ECS.

### Inventory and UI
- `PlayerSessionService` establece el héroe compartido por los servicios existentes y notifica el cambio de contexto sin guardar una operación.
- `InventoryEventService.RunTransaction` conserva el estado previo de mochila, slots, stats y monedas; un fallo revierte todo, un éxito guarda y publica el estado final una vez.
- La tienda y el arrastre envían operaciones a los servicios; las vistas no modifican directamente los datos persistentes.
- Consumibles con selección sólo se consumen cuando se entrega toda la recompensa. El diálogo admite confirmación, cancelación y reintento.
- Celdas y tooltips usan un contexto de interacción explícito para inventario, equipo, producto, venta, selector y recompensa.

### LocalHeroCharacterMotor
- **Responsabilidad**: único ejecutor físico del héroe local.
- **Input**: `HeroMoveIntent`, `HeroLifeComponent` y revisiones de `HeroSpawnComponent`.
- **Ejecución**: gravedad, suelo, escalones y colisiones mediante `CharacterController.Move()`.
- **Output confirmado**: `LocalTransform` y `HeroMotorStateComponent` (velocidad real, grounded, laterales y techo).
- `EcsAnimationInputAdapter` usa velocidad horizontal y grounded confirmados; el sprint visual procede de la intención aceptada. `SamplePlayerAnimationController_ECS` convierte la velocidad física a las coordenadas del Blend Tree mediante `LocomotionAnimationSettings`.
- **Safe teleport**: deshabilita temporalmente el controller, aplica la pose, reinicia gravedad y consume una sola vez cada revisión.
- El motor no se crea ni permanece activo para héroes remotos.

### RemoteHeroAnimationDriver
- **Responsabilidad**: presentación de locomoción y combate del héroe remoto.
- **Input confirmado**: velocidad horizontal y límite aceptado del `NavMeshAgent`, `HeroStatsComponent`, `HeroAnimationComponent` y `HeroCombatComponent`. Una petición IA de sprint por sí sola no cambia la animación.
- **Output**: parámetros y triggers del `Animator`; no escribe transforms, destinos ni intención ECS.
- Deshabilita los adaptadores de input/animación local para evitar dos escritores sobre el mismo Animator.
- Evita escribir de nuevo parámetros float/int/bool que no cambiaron; invalida la caché al cambiar el binding, referencia/estado del Animator o controller. Los triggers de ataque no se deduplican.
- Sólo se instala para entidades remotas con `HeroMoveIntent`; los visuales de unidades no reciben este componente.

---

## Shared Utilities

### VisualSyncUtility (`Assets/Scripts/Shared/VisualSyncUtility.cs`)
- `SetupVisualSync(GameObject)` — configura inicialmente `AnimatorCullingMode.CullCompletely`, desactiva root motion en los Animators hijos y añade/obtiene `EntityVisualSync`. `EntityVisualSync` solo cambia a `AlwaysAnimate` los rigs que contienen hitboxes de arma/escudo; esa excepción mantiene funcionalidad y tiene coste de CPU en esos rigs.
- Usada por: `HeroVisualInstantiationSystem`, `SquadVisualManagementSystem`

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

### Progresión de héroe

BattleBootstrapSystem captura el GUID y la progresión de HeroData en HeroProgressComponent al aceptar la partida. HeroLevelSystem consume sólo XPEventComponent dirigido a ese GUID; guarda antes de modificar ECS/consumir el evento, sin leer SelectedHero ni escribir el bloque heredado sin dueño. Un ticket legado puede usar el GUID explícito del llamador validado en el perfil. SaveSystem confirma el dueño único, actualiza revisión y sesión, y conserva los otros héroes.

SquadProgressionSystem valida dueño e instancia contra el mapa capturado y guarda en HeroData.squadProgress. Consume cada evento confirmado sobre un snapshot, después del commit y antes de procesar el siguiente; actualiza mapa/ECS y genera LevelUp. La identidad remota sólo cambia runtime, sin escribir el perfil local. El bloque de progreso antiguo no recibe XP nueva.
