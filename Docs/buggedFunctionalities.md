# Estado actual de problemas y deuda técnica

Actualizado: 2026-09-15. Verificado contra `3eef0e17`.

Este documento es un registro de estado, no una lista histórica de síntomas. Las reparaciones detalladas viven en `Docs/Arquitectura/`; los problemas cerrados no deben seguir tratándose como comportamiento vigente.

## Resumen

| Área | Estado actual |
|---|---|
| Autoridad física del héroe local | Resuelta: intención ECS → motor local → `CharacterController` → estado confirmado ECS |
| Movimiento del héroe remoto | Resuelto para IA local: `NavMeshAgent` con publicación a ECS |
| Animación remota | Separada en `RemoteHeroAnimationDriver` |
| Órdenes de squads | Productores separados y arbitraje explícito |
| Formación y hold | Confirmación única, slots estables y ancla persistente |
| Retirada por swap/muerte | Conectada, persistente y protegida contra duplicación |
| Destinos NavMesh | Proyección, fallo recordado y reintento controlado |
| Bodyblock | Orden físico explícito, corrección independiente de FPS y sin GC caliente |
| Targeting | Candidatos compartidos por squad y objetivo final por unidad |
| Estado ECS de movimiento sin lectores | Eliminado |
| Contrato de red y reconciliación | No implementado |

## Problemas abiertos

### OPEN-001 — Propietario de la animación de ataque local

`EntityVisualSync` todavía consume `HeroAnimationComponent.triggerAttack` para el héroe local y escribe `HeroCombatComponent.isAttacking` en el Animator. La locomoción local pertenece a `EcsAnimationInputAdapter`/`SamplePlayerAnimationController_ECS`, por lo que la presentación local continúa dividida entre dos componentes.

Riesgo: al ampliar combos, cancelaciones o predicción puede aparecer más de un consumidor del mismo pulso. La siguiente extracción debe elegir un único dueño local y conservar consumo one-shot del trigger.

### OPEN-002 — Red real y reconciliación

Los héroes llamados “remotos” son actualmente entidades controladas por IA en el mismo proceso. No existen snapshots versionados, ownership de servidor, interpolación de red, predicción ni reconciliación. `Entity`, managed components, `UnityEngine.Object` y estado de `NavMeshAgent` son internos y no deben cruzar el futuro contrato con BronzeAge.

### OPEN-003 — Ciclo de vida remoto/desconexión

La retirada por muerte depende de que la entidad dueña exista y publique `HeroLifeComponent`. La desaparición del owner o una desconexión no tiene todavía una política equivalente explícita. Debe resolverse en el contrato de red, no inferirse silenciosamente como muerte.

### OPEN-004 — Validación end-to-end y visual

Las suites verifican contratos aislados y compilación Player, pero no certifican una partida completa con spawn, combate, muerte, retirada, respawn, swaps repetidos y destrucción de visuales en todos los mapas. También falta inspección visual sistemática de animación remota, escalones y pendientes representativos.

### OPEN-005 — Configuración runtime del NavMeshAgent remoto

`HeroVisualInstantiationSystem` puede añadir un `NavMeshAgent` si el prefab no lo trae. Es tolerante, pero mantiene parámetros fallback en código y puede ocultar una configuración incompleta del prefab. La decisión pendiente es estandarizar el agente en el prefab o mover todos los tunables a una configuración explícita.

### OPEN-006 — `EntityVisualSync` sigue siendo un hub

Después de extraer movimiento local y animación remota, aún combina búsqueda/vinculación de entidad, selección de rol, ciclo de vida del GameObject, sincronización de pose no NavMesh, debug y ataque local. No es ya autoridad de movimiento, pero sigue siendo uno de los nodos más conectados del grafo. Conviene separar por contrato sólo cuando cada nuevo componente tenga un dueño inequívoco.

## Problemas cerrados

### FIX-001 — Órdenes competidoras

`SquadControlSystem`, `HeroAIExecutionSystem` y `CombatReactionSystem` publican intents independientes. `OrderResolutionSystem` decide el ganador y escribe `SquadResolvedOrderComponent`, incluyendo `OrderSource`; `SquadOrderSystem` aplica únicamente la orden resuelta. Las peticiones IA sobreviven a overrides temporales.

### FIX-002 — Formación confirmada antes de aplicarse

`SquadOrderSystem` ya no marca la formación como aplicada. `FormationSystem` valida el patrón, asigna slots estables y sólo entonces confirma formación y cooldown.

### FIX-003 — Hold confundido con ausencia de combate

`SquadHoldPositionComponent` conserva centro y rotación. El hold define ancla táctica, no inmunidad a targeting o ataque; la retirada tiene prioridad sobre esa ancla.

### FIX-004 — Retirada incompleta o duplicable

El swap valida reemplazo y recursos antes de retirar. La muerte del dueño crea una decisión persistente, bloquea órdenes, espera punto aliado sin inventar origen y conserva efectivos antes del cleanup. Respawn y creación de squads impiden dos instancias activas de la misma reserva.

### FIX-005 — Dos escritores de destinos

`SquadNavigationSystem` observa llegada. `UnitNavMeshSystem` es dueño de `SetDestination` para unidades y `HeroAIExecutionSystem` para héroes IA. La llegada de retirada se evalúa sobre supervivientes y destinos efectivos de slot.

### FIX-006 — Destinos imposibles y retry por frame

Los destinos se proyectan al NavMesh, se valida `SetDestination`/`pathStatus` y se recuerda el comando fallido hasta que cambie lo suficiente. Las unidades no permanecen artificialmente en movimiento por un punto nominal inalcanzable.

### FIX-007 — Geometría y bodyblock dependientes de FPS

La formación conserva centro fraccional y slots por identidad. Bodyblock usa solapamiento real, pools reutilizables y corrección física antes de publicar la pose a ECS.

### FIX-008 — Targeting duplicado por unidad

Los candidatos equivalentes se almacenan una vez por squad. Cada unidad conserva únicamente su target seleccionado y limpia tanto target como tag de engagement al quedar inactiva.

### FIX-009 — Estado muerto de movimiento

Se retiraron componentes y buffers escritos sin consumidores, incluidos estados redundantes de movimiento/animación que podían divergir de las fuentes reales.

### FIX-010 — Autoridad local ambigua

`HeroMovementSystem` sólo produce `HeroMoveIntent`. `LocalHeroCharacterMotor` es el único que llama `CharacterController.Move`, consume teleports revisionados y publica `LocalTransform`/`HeroMotorStateComponent`. Un héroe remoto no puede conservar esa autoridad.

### FIX-011 — Animación desde input no confirmado

La locomoción local usa velocidad horizontal y grounded confirmados. La animación remota pertenece a `RemoteHeroAnimationDriver`, que usa velocidad real de NavMesh y estado ECS de combate. Las unidades no reciben drivers de héroe.

## Evidencia de validación

Estado al cerrar la fase 15:

- 62/62 EditMode.
- 12/12 PlayMode.
- 64 assemblies de Player Windows.
- prueba de escala de targeting con 900 unidades.
- prueba de escala de bodyblock con 900 agentes y frame caliente sin asignaciones administradas.

Estos resultados son evidencia de regresión automatizada; no sustituyen la validación end-to-end indicada en `OPEN-004`.

## Referencias

- [Arquitectura actual](Arquitectura/1_Arquitectura_Actual.md)
- [Control de squads](Arquitectura/3_Control_Escuadras_2026-09-14.md)
- [Retirada por muerte](Arquitectura/6_Retirada_Muerte_Dueno_2026-09-14.md)
- [Destinos NavMesh](Arquitectura/9_Destinos_NavMesh_Fallos_Path_2026-09-14.md)
- [Targeting a escala](Arquitectura/12_Deteccion_Targeting_Escala_2026-09-15.md)
- [Limpieza de movimiento](Arquitectura/13_Limpieza_Estado_Movimiento_2026-09-15.md)
- [Motor local](Arquitectura/14_Motor_Local_Heroe_2026-09-15.md)
- [Animación remota](Arquitectura/15_Animacion_Remota_Separada_2026-09-15.md)
