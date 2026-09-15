# Modelo híbrido ECS–GameObject

Actualizado: 2026-09-15. Verificado contra `3eef0e17`.

Este documento describe el modelo híbrido implementado. La estructura completa de datos, persistencia y DTO de batalla está en [Arquitectura actual](Arquitectura/1_Arquitectura_Actual.md); el detalle temporal de squads está en [Pipeline de movimiento](TroopMovementPipeline.md).

## Principio rector

ECS conserva identidad, intención y estado de gameplay. Los GameObjects aportan capacidades de Unity que el ECS actual no reemplaza —`CharacterController`, `NavMeshAgent`, `Animator`, render, colliders y efectos— y devuelven únicamente resultados físicos confirmados por puentes explícitos.

```text
ECS: decisión e intención
        │
        ▼
motor físico apropiado de Unity
        │
        ▼
pose/contactos confirmados
        │
        ├──→ ECS para simulación posterior
        └──→ Animator para presentación
```

No existe una única dirección universal ECS → GameObject. La dirección depende del rol y está definida por contrato.

## Entidades y representaciones

```text
Hero entity 1 ── 0..1 HeroVisualInstance ── 1 GameObject de héroe
      │
      └── 0..1 HeroSquadReference ── 1 Squad entity
                                      │
                                      └── 0..N SquadUnitElement ── Unit entity
                                                                         │
                                                                         └── 0..1 UnitVisualInstance
```

Los componentes `HeroVisualInstance` y `UnitVisualInstance` son managed components runtime. No forman parte de persistencia ni de un contrato de red.

## Matriz de autoridad

| Caso | Intención/decisión | Ejecutor físico | Publicación a ECS | Presentación |
|---|---|---|---|---|
| Héroe local | `HeroInputSystem` → `HeroMovementSystem` → `HeroMoveIntent` | `LocalHeroCharacterMotor` → `CharacterController.Move()` | El motor escribe `LocalTransform` y `HeroMotorStateComponent` | `EcsAnimationInputAdapter` + `SamplePlayerAnimationController_ECS` |
| Héroe remoto/IA | `HeroAIExecutionSystem` | `NavMeshAgent` | `NavMeshPositionSyncSystem` | `RemoteHeroAnimationDriver` |
| Unidad navegable | formación/targeting → `UnitNavMeshSystem` | `NavMeshAgent` | `NavMeshPositionSyncSystem` | adaptador/controlador de unidad |
| Visual sin NavMesh | sistema ECS propietario | pose ECS | no aplica | `EntityVisualSync` copia ECS → GameObject |

Reglas:

- Sólo `LocalHeroCharacterMotor` puede habilitar y mover el `CharacterController` del héroe local.
- Un héroe remoto mantiene su `CharacterController` deshabilitado.
- `RemoteHeroAnimationDriver` no escribe pose, órdenes ni destinos.
- `EntityVisualSync` no ejecuta movimiento local ni conduce locomoción remota.
- Para agentes con `NavAgentComponent.syncPositionFromNavMesh`, la publicación de pose pertenece a `NavMeshPositionSyncSystem`; `EntityVisualSync` no duplica esa escritura.

## Héroe local

```text
hardware
  → HeroInputSystem
  → HeroInputComponent
  → HeroMovementSystem
  → HeroMoveIntent { Direction, Speed }
  → LocalHeroCharacterMotor
  → CharacterController.Move
  ├─ LocalTransform
  └─ HeroMotorStateComponent { velocity, grounded, hitSides, hitCeiling }
```

`HeroMovementSystem` no integra `LocalTransform`: transforma input a intención relativa a cámara, aplica sprint/stamina y bloqueos de gameplay, y limpia la intención al morir, esperar spawn o no disponer de cámara.

El motor local procesa gravedad, suelo, pendientes, escalones y colisiones. Su velocidad horizontal confirmada alimenta la animación, de modo que una intención bloqueada por geometría no produce carrera visual.

### Spawn y teleport

`HeroSpawnSystem` selecciona un punto válido, actualiza `HeroSpawnComponent.spawnPosition/spawnRotation`, incrementa `positionRevision` y publica la pose ECS. El motor consume cada revisión una sola vez, deshabilita temporalmente el controller, aplica la pose, reinicia velocidad vertical y vuelve a publicar el resultado confirmado. Un spawn sin punto válido no incrementa la revisión.

## Héroe remoto

```text
HeroAIPerceptionSystem
  → HeroAIBlackboard
  → HeroAIRusherSystem / HeroAIBalancedSystem
  → HeroAIDecision
  → HeroAIExecutionSystem
      ├─ proyección/validación de destino
      ├─ NavMeshAgent.SetDestination
      ├─ HeroMoveIntent informativo
      └─ SquadAIOrderIntentComponent
```

El `NavMeshAgent` mueve el GameObject. `NavMeshPositionSyncSystem` publica la pose física a `LocalTransform`. `RemoteHeroAnimationDriver` lee velocidad real, sprint decidido y componentes de combate para escribir el `Animator`; deshabilita los consumidores de input local.

Un destino inválido se proyecta al NavMesh. Los fallos se recuerdan y sólo se reintentan al cambiar suficientemente la orden, evitando `SetDestination` repetido cada frame.

## Squads y unidades

```text
Productores
  SquadControlSystem ───────→ SquadPlayerOrderIntentComponent
  HeroAIExecutionSystem ────→ SquadAIOrderIntentComponent
  CombatReactionSystem ─────→ SquadCombatReactionIntentComponent
                                  │
                                  ▼
                         OrderResolutionSystem
                                  │
                                  ▼
                         SquadResolvedOrderComponent
                                  │
                                  ▼
                           SquadOrderSystem
                                  │
                 ┌────────────────┼─────────────────┐
                 ▼                ▼                 ▼
          SquadFSMSystem     FormationSystem   SquadAnchorSystem
                                  │                 │
                                  └────────┬────────┘
                                           ▼
                          UnitFormationStateSystem
                                           ▼
                     UnitTargetingSystem / UnitNavMeshSystem
                                           ▼
                      UnitFollowFormationSystem / bodyblock
                                           ▼
                              NavMeshPositionSyncSystem
```

La orden resuelta conserva origen y prioridad. `SquadOrderSystem` aplica la orden ganadora, pero sólo `FormationSystem` confirma formación, slots y cooldown después de validar el patrón.

`HoldPosition` conserva un ancla táctica aunque el squad entre en combate. Targeting selecciona candidatos compartidos por squad y mantiene sólo el objetivo final como estado por unidad. La navegación de squad observa llegada; el sistema motor de unidades es el único que escribe destinos.

## Retirada y muerte del dueño

La muerte del héroe local es detectada por `HeroRespawnSystem` desde `HeroHealthComponent`. Mientras está muerto se consume `deathTimer`; al expirar se restaura salud y `HeroSpawnSystem` solicita una nueva pose revisionada.

En paralelo, `SquadOwnerDeathRetreatSystem` compromete inmediatamente la retirada de su squad activo:

- registra `SquadOwnerDeathRetreatComponent`;
- fija FSM y transición en `Retreating`;
- bloquea órdenes mediante `retreatTriggered`;
- elimina `IsLocalSquadActive`;
- usa un punto aliado activo, o espera conservando la decisión si aún no existe.

`RetreatLogicSystem` espera llegada de todos los supervivientes a sus slots o el timeout configurado. Antes de destruir squad y unidades persiste efectivos en `InactiveSquadElement` y elimina `HeroSquadReference` sólo si todavía apunta a esa instancia. El respawn del dueño no cancela una retirada ya comprometida.

## Creación y ciclo de vida visual

```text
entidad sin instancia visual
  → HeroVisualInstantiationSystem / SquadVisualManagementSystem
  → VisualPrefabRegistry
  → Instantiate(GameObject)
  → VisualSyncUtility.SetupVisualSync
  → EntityVisualSync.SetHeroEntity
  → configuración de autoridad por componentes
```

Si la entidad vinculada desaparece, `EntityVisualSync` destruye el GameObject asociado. Los sistemas de gestión son dueños de la creación; UI y gameplay no deben instanciar representaciones paralelas.

## Animación

| Rol | Fuente de locomoción | Dueño del Animator |
|---|---|---|
| Local | `HeroMotorStateComponent.velocity/isGrounded` + eventos de input | `SamplePlayerAnimationController_ECS` mediante `EcsAnimationInputAdapter` |
| Remoto | `NavMeshAgent.velocity` + `HeroAIDecision.shouldSprint` | `RemoteHeroAnimationDriver` |
| Unidad | estado/velocidad del agente de unidad | adaptadores de unidad |

Los hashes están centralizados en `AnimationHashes`. El pulso `HeroAnimationComponent.triggerAttack` se consume una vez; el estado sostenido proviene de `HeroCombatComponent.isAttacking`.

## Límites actuales

- El orden relativo ECS `SimulationSystemGroup` ↔ `MonoBehaviour.Update` sigue siendo una frontera híbrida, no una simulación física ECS determinista.
- `EntityVisualSync` aún conserva el consumo del ataque local; su extracción requiere coordinar un único consumidor con el controlador local de animación.
- La autoridad remota actual es IA/NavMesh local. No existe todavía reconciliación, snapshots ni autoridad de servidor implementada.
- `Entity`, `UnityEngine.Object`, `NavMeshAgent` y managed components no pueden formar parte del DTO neutral con BronzeAge.

## Validación vigente

- 62/62 pruebas EditMode.
- 12/12 pruebas PlayMode.
- 64 assemblies de Player Windows compiladas.
- Pruebas específicas cubren motor local, teleport revisionado, exclusión de autoridad remota, animación confirmada y ausencia de drivers de héroe en unidades.

## Referencias

- [Arquitectura actual](Arquitectura/1_Arquitectura_Actual.md)
- [Pipeline de movimiento](TroopMovementPipeline.md)
- [Control de squads](Arquitectura/3_Control_Escuadras_2026-09-14.md)
- [Retirada por muerte](Arquitectura/6_Retirada_Muerte_Dueno_2026-09-14.md)
- [Destinos NavMesh](Arquitectura/9_Destinos_NavMesh_Fallos_Path_2026-09-14.md)
- [Motor local](Arquitectura/14_Motor_Local_Heroe_2026-09-15.md)
- [Animación remota](Arquitectura/15_Animacion_Remota_Separada_2026-09-15.md)
