# Movimiento del héroe y autoridad física

Fecha: 2026-09-14. Séptima fase del pipeline de movimiento. Cambios locales sin commit.

## Autoridad actual

- Héroe local: `HeroMoveIntent` expresa la intención; `EntityVisualSync` mueve el `CharacterController`; la posición y rotación del GameObject vuelven a `LocalTransform`.
- Héroe remoto: `HeroAIExecutionSystem` controla el `NavMeshAgent`; `EntityVisualSync` copia GameObject/NavMesh hacia ECS y conduce la animación.
- Unidades: `UnitNavMeshSystem` controla destinos; `NavMeshPositionSyncSystem` copia la posición a ECS.

El modelo continúa siendo híbrido, pero cada caso tiene ahora una sola autoridad de movimiento activa.

## Cambios aplicados

- `EntityVisualSync.ConfigureMovementAuthority()` activa el `CharacterController` únicamente para la entidad con `IsLocalPlayer`. Corrige el método anterior, que anunciaba desactivación pero siempre escribía `enabled = true`.
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

Resultado: **58/58 EditMode**, **8/8 PlayMode** y **64 assemblies de Player Windows** compiladas correctamente.

PlayMode verifica destino remoto fuera del NavMesh, ausencia de excepción, detención, limpieza de intención y matriz de colisión aliado/enemigo.

## Límites

La integración sigue dependiendo del orden entre Simulation ECS y `MonoBehaviour.Update`; no se ha convertido el héroe local a un motor ECS puro. Los colliders de las unidades se mueven como parte de GameObjects gobernados por NavMeshAgent y no por Rigidbody. Es una decisión válida para el prototipo, pero debe perfilarse junto con el bodyblock en la prueba de 900 unidades.
