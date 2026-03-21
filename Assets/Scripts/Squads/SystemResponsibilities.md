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

### HeroVisualManagementSystem
- **Responsabilidad**: Instancia el prefab visual del héroe y configura `EntityVisualSync`
- **Output**: `HeroVisualInstance`, GameObject con `EntityVisualSync` configurado
- **NavMeshAgent**: Para héroes remotos, llama `agent.Warp(position)` post-spawn y valida `agent.isOnNavMesh`
- **Proceso post-ECB**: Recolecta `NavMeshAgent` en lista durante OnUpdate, adjunta tras playback del ECB
- **Usa**: `VisualPrefabRegistry`, `VisualSyncUtility.SetupVisualSync()`

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
