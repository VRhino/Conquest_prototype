# System Responsibilities & Data Flow

Este archivo documenta las responsabilidades específicas de cada sistema ECS y controlador MonoBehaviour del juego. Cada sistema tiene **una sola responsabilidad** — no agregar concerns fuera de su scope.

---

## ECS Systems (Hero)

### HeroInputSystem
- **Responsabilidad**: Captura raw input del jugador (WASD, mouse, hotkeys)
- **Output**: `HeroInputComponent`, `HeroMoveIntent`
- **Regla**: Solo lee input — nunca modifica estado de héroe ni squads

### HeroMovementSystem
- **Responsabilidad**: Mueve el héroe en base a `HeroMoveIntent`
- **Output**: `LocalTransform`
- **Regla**: No lee input directamente — solo procesa `HeroMoveIntent`

### HeroStateSystem
- **Responsabilidad**: Detecta cambio de estado del héroe (Idle/Moving)
- **Output**: `HeroStateComponent`

### HeroSpawnSystem
- **Responsabilidad**: Crea la entidad ECS del héroe local al inicio de la batalla
- **Output**: entidad héroe con todos sus componentes + `HeroSquadSelectionComponent` linkeando a la escuadra activa (`instanceId = 0`)
- **Nota**: el `instanceId = 0` debe mantenerse sincronizado con `BattleSceneController.SyncBattleDataToECS` que asigna ID 0 a la escuadra activa

### HeroVisualInstantiationSystem
- **Responsabilidad**: Instancia el prefab visual del héroe y configura `EntityVisualSync`, hitbox y NavMeshAgent
- **Output**: `HeroVisualInstance`, GameObject con `EntityVisualSync` configurado
- **NavMeshAgent**: Para héroes remotos, llama `agent.Warp(position)` post-spawn
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
- **Output**: `SquadInputComponent`

### SquadOrderSystem
- **Responsabilidad**: Convierte `SquadInputComponent` en cambios de estado de squad
- **Output**: `SquadStateComponent`

### SquadFSMSystem
- **Responsabilidad**: Gestiona transiciones de estado del squad (Following/Holding/Retreating)
- **Regla**: Solo transiciones — no mueve unidades

### FormationSystem
- **Responsabilidad**: Calcula posiciones de formación según tipo y centro del squad
- **Output**: `UnitTargetPositionComponent` por unidad

### UnitFormationStateSystem
- **Responsabilidad**: Gestiona todos los cambios de estado de unidades (Moving/Formed/Waiting)
- **Regla**: Único owner de transiciones de estado de unidades

### UnitNavMeshSystem
- **Responsabilidad**: Única autoridad para decisiones NavMesh por unidad: destino + rotación
- **Orden**: `[UpdateAfter(UnitFormationStateSystem)]` `[UpdateBefore(UnitFollowFormationSystem)]`
- **Owner exclusivo**: `agent.SetDestination()` y `agent.updateRotation`
- **Destino**: formación slot (default) ó stop-point cerca del target (si hay combatTarget y orden ≠ HoldPosition)
- **Rotación combate**: Si dist ≤ 3.5u → `updateRotation=false` + rota `LocalTransform` para mirar al target
- **Rotación normal**: `updateRotation=true` — NavMesh controla la orientación durante movimiento
- **`UnitTargetPositionComponent`**: solo lectura — nunca escribe (ownership exclusivo de sistemas de formación)

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
- **Fuerza**: `WallStrength = 60f` para Line/Testudo/Wedge/Square en estado Formed; `RepulsionStrength = 8f` para Dispersed/Column
- **Regla**: Solo cross-team — aliados nunca se repelen; `Formed vs Formed` sin push (evita vibración)
- **Algoritmo**: Spatial grid (cell = `BodyblockRadius`) → 9 celdas vecinas → O(n×k)
- **Orden**: `[UpdateAfter(UnitNavMeshSystem)]`
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
- **Responsabilidad**: Detecta squads enemigos en rango y popula los 3 buffers de detección por frame
- **Output**: `DetectedEnemy` (por squad), `SquadTargetEntity` (por squad), `UnitDetectedEnemy` (por unidad)
- **Orden**: `[UpdateBefore(SquadAISystem)]` — SquadAI necesita detección fresca para decidir intents
- **Regla**: Solo escribe buffers de detección — nunca decide comportamiento ni asigna targets de unidades
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
- **Output**: `UnitCombatComponent.combatTarget` por unidad
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
- **Prioridad local**: `heroOrdenCooldown activo → Player` / `reactToEnemy → CombatReaction` / `otherwise → Player`
- **Prioridad remoto**: `reactToEnemy → CombatReaction` / `otherwise → AI`
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
- **Responsabilidad**: Sincroniza `LocalTransform` ECS → `transform` de GameObject cada frame
- **Safe teleport**: Deshabilita `CharacterController` antes de aplicar posición ECS, lo rehabilita después
- **Constantes**: `GROUND_CHECK_BUFFER = -0.5f`, `TERMINAL_VELOCITY = -50f`
- **Regla**: Solo lee ECS — nunca escribe en ECS

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
