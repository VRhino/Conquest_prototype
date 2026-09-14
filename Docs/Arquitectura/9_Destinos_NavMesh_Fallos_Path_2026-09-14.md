# Destinos NavMesh y fallos de path

Fecha: 2026-09-14. Sexta fase del pipeline de movimiento. Cambios locales sin commit.

## Problema

`UnitTargetPositionComponent` expresa el slot táctico, pero ese punto no garantiza que exista sobre el NavMesh ni que sea alcanzable desde la isla actual. El sistema enviaba el punto directamente a `SetDestination()` e ignoraba su resultado. Una unidad podía permanecer en `Moving` y una retirada en `isNavigating` indefinidamente.

## Cambios aplicados

- `UnitNavMeshSystem` proyecta cada slot mediante `NavMesh.SamplePosition` usando el tipo de agente y su máscara de áreas.
- `NavAgentComponent` conserva por separado el slot solicitado y el destino de formación efectivo. El sistema de formación mantiene ownership exclusivo de `UnitTargetPositionComponent`.
- `UnitFormationStateSystem` y `SquadNavigationSystem` evalúan llegada contra el destino efectivo solo si corresponde al último slot solicitado.
- Si la proyección falla, `SetDestination()` rechaza el punto o el path anterior es parcial/inválido, se cancela el path y la posición actual pasa a ser el fallback alcanzable.
- Un destino fallido no se solicita cada frame. Se vuelve a evaluar cuando el slot solicitado se desplaza al menos `navMeshFailureRetryDistance`.
- Los stop-points de persecución también se proyectan; si no existe una proyección cercana, la unidad vuelve a su destino de formación.

Configuración por defecto:

- `navMeshDestinationSampleRadius`: 2 m.
- `navMeshFailureRetryDistance`: 1 m.

## Validación

```powershell
unity test . --mode EditMode --filter 'BodyblockRegressionTests|FormationGeometryRegressionTests|ArchitectureRegressionTests|SquadControlRegressionTests|HeroLifecycleRegressionTests|SquadSwapRegressionTests|SquadOwnerDeathRegressionTests' --output Logs/navigation-failure-results.xml --timeout 300 -- -nographics -logFile Logs/navigation-failure-tests.log
unity test . --mode PlayMode --filter 'SquadNavigationRegressionTests|HeroVisualRespawnRegressionTests' --output Logs/navigation-failure-playmode-results.xml --timeout 300 -- -nographics -logFile Logs/navigation-failure-playmode-tests.log
unity run . --timeout 300 -- -nographics -executeMethod ArchitectureBuildValidation.CompilePlayerScripts -logFile Logs/navigation-failure-player-compile.log
```

Resultado: **58/58 EditMode**, **6/6 PlayMode** y **64 assemblies de Player Windows** compiladas correctamente.

PlayMode construye una superficie temporal y verifica que un slot muy fuera del NavMesh se convierte en un fallback alcanzable en vez de dejar la escuadra bloqueada.

## Límites

El fallback evita el bloqueo lógico, pero no intenta reconstruir una formación óptima junto a precipicios o islas desconectadas. El caso de path parcial está gestionado en ejecución mediante `NavMeshAgent.pathStatus`, aunque todavía conviene añadir un escenario PlayMode con dos islas cuando el fixture pueda esperar de forma determinista el cálculo asíncrono.

No se añaden logs por unidad: con cientos de agentes convertirían una degradación recuperable en spam. El estado `formationDestinationFailed` queda disponible para telemetría o visualización de depuración futura.
