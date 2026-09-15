# Movimiento del héroe y autoridad física

Fecha: 2026-09-14. Séptima fase del pipeline de movimiento. Cambios locales sin commit.

## Autoridad actual

- Héroe local: `HeroMoveIntent` expresa la intención; desde la fase 14, `LocalHeroCharacterMotor` mueve el `CharacterController` y publica el resultado en `LocalTransform` y `HeroMotorStateComponent`.
- Héroe remoto: `HeroAIExecutionSystem` controla el `NavMeshAgent`; `NavMeshPositionSyncSystem` publica la pose en ECS y, desde la fase 15, `RemoteHeroAnimationDriver` conduce exclusivamente la animación.
- Unidades: `UnitNavMeshSystem` controla destinos; `NavMeshPositionSyncSystem` copia la posición a ECS.

El modelo continúa siendo híbrido, pero cada caso tiene ahora una sola autoridad de movimiento activa.

## Cambios aplicados

- `EntityVisualSync.ConfigureMovementAuthority()` activa y vincula `LocalHeroCharacterMotor` únicamente para la entidad con `IsLocalPlayer`; los remotos mantienen el `CharacterController` desactivado.
- `HeroAIExecutionSystem` comprueba `enabled` e `isOnNavMesh` antes de operar sobre el agente.
- Los objetivos de IA se proyectan con el mismo filtro y radio navegable que los destinos de unidades.
- Se comprueban el retorno de `SetDestination()` y el `pathStatus` posterior. Un destino fallido se recuerda y solo se reintenta cuando cambia al menos `navMeshFailureRetryDistance`.
- Al rechazar o completar una orden, primero se limpia el path y después se fija `isStopped`, evitando que `ResetPath()` deje el agente activo.
- `HeroMoveIntent.Direction` queda a cero si no existe movimiento navegable, evitando animación residual.
- El spawn remoto intenta `Warp`; si falla, proyecta la posición antes de repetirlo y emite una única advertencia si tampoco puede colocarse.
- `heroAIArrivalDistance` pasa a configuración ECS, con valor por defecto de 1,5 m.

## Contrato físico verificado

Los prefabs registrados de Squires, Spearmen y Levy Archers ya incluyen `CapsuleCollider` no trigger. El héroe local incluye `CharacterController` y `CapsuleCollider`. La matriz de colisiones configura:

- `Heroes_A` contra `Units_A`: ignorado.
- `Heroes_A` contra `Units_B`: colisión activa.
- La misma relación simétrica se mantiene para el equipo B.

Por tanto, el héroe local atraviesa aliados y queda bloqueado por enemigos sin involucrar el bodyblock ECS.

## Validación

```powershell
unity test . --mode EditMode --filter 'BodyblockRegressionTests|FormationGeometryRegressionTests|ArchitectureRegressionTests|SquadControlRegressionTests|HeroLifecycleRegressionTests|SquadSwapRegressionTests|SquadOwnerDeathRegressionTests' --output Logs/hero-movement-final-results.xml --timeout 300 -- -nographics -logFile Logs/hero-movement-final-tests.log
unity test . --mode PlayMode --filter 'SquadNavigationRegressionTests|HeroVisualRespawnRegressionTests' --output Logs/hero-movement-final-playmode-results.xml --timeout 300 -- -nographics -logFile Logs/hero-movement-final-playmode-tests.log
unity run . --timeout 300 -- -nographics -executeMethod ArchitectureBuildValidation.CompilePlayerScripts -logFile Logs/hero-movement-final-player-compile.log
```

Resultado histórico de esta fase: **58/58 EditMode**, **8/8 PlayMode** y **64 assemblies de Player Windows**. La extracción posterior se valida en [Motor local del héroe](14_Motor_Local_Heroe_2026-09-15.md).

PlayMode verifica destino remoto fuera del NavMesh, ausencia de excepción, detención, limpieza de intención y matriz de colisión aliado/enemigo.

## Límites

La integración continúa siendo híbrida por diseño: ECS decide y `CharacterController` resuelve terreno y colisiones. La fase 14 encapsula esa frontera en un único motor, aunque la entrega de intención y resultado todavía cruza `SimulationSystemGroup`/`MonoBehaviour.Update`.
