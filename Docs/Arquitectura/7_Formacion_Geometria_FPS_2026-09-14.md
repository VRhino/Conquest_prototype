# Formación, geometría y estabilidad temporal

Fecha: 2026-09-14. Cuarta fase del pipeline de escuadras. Cambios locales sin commit.

## Cambios aplicados

- El centro de una formación es `float2` y conserva medias celdas. Patrones pares quedan centrados de forma simétrica: por ejemplo, columnas 0 y 1 producen offsets -0,5 y +0,5.
- Una rotación se considera válida por la longitud del cuaternión, se normaliza antes de usarla y admite giros válidos cuyo componente `w` sea cero, incluido 180°.
- Los límites del patrón se calculan una vez por formación y actualización relevante, no una vez por unidad. El overload público anterior se conserva como compatibilidad; los bucles principales usan el centro precalculado.
- Spawn y cambio de formación aplican la rotación del ancla en el mismo frame.
- `GridFormationUpdateSystem` y `DestinationMarkerSystem` validan blob, cantidad de formaciones, formación seleccionada y componentes antes de desreferenciar. Un componente malformado se ignora de forma segura.
- Las actualizaciones continuas usan `UnitGridSlotComponent.slotIndex`, no la posición actual dentro de `SquadUnitElement`. Al morir una unidad y compactarse el buffer, los supervivientes conservan destino e identidad de slot. Un cambio explícito de formación continúa reasignando slots de forma deliberada.
- El estado de formación se decide por unidad y por distancia a su propio slot. Una unidad atascada no bloquea a las demás, y una unidad correctamente colocada en una formación extensa no falla por estar lejos del centro.
- Se eliminó el recorrido de la unidad más lejana y dos utilidades de distancia sin consumidores.
- `SquadAnchorMovingTag` se calcula con velocidad (`distancia / deltaTime`) y no con desplazamiento por frame. La clasificación es consistente a distintas frecuencias.
- Los umbrales históricos dejan de ser constantes internas: `slotArrivalThreshold` (0,2 m), `holdReformThreshold` (1 m) y `anchorMovingSpeedThreshold` (0,1 m/s) viven en `SquadSpawnConfigAuthoring`/`Component`.
- El marcador reconoce HoldPosition por la orden táctica incluso durante combate, igual que navegación, ancla y estado de formación.

## Validación reproducible

```powershell
unity test . --mode EditMode --filter 'FormationGeometryRegressionTests|ArchitectureRegressionTests|SquadControlRegressionTests|HeroLifecycleRegressionTests|SquadSwapRegressionTests|SquadOwnerDeathRegressionTests' --output Logs/formation-geometry-results.xml --timeout 300 -- -nographics -logFile Logs/formation-geometry-tests.log
unity test . --mode PlayMode --filter 'SquadNavigationRegressionTests|HeroVisualRespawnRegressionTests' --output Logs/formation-geometry-playmode-results.xml --timeout 300 -- -nographics -logFile Logs/formation-geometry-playmode-tests.log
unity run . --timeout 300 -- -nographics -executeMethod ArchitectureBuildValidation.CompilePlayerScripts -logFile Logs/formation-geometry-player-compile.log
```

Nuevos casos: formación par sin sesgo, cuaternión de media vuelta con `w=0`, slot estable después de compactar buffer, blob inválido seguro, estado individual con otra unidad fuera de slot y movimiento del ancla a 30/60/144 FPS.

Resultado final del 2026-09-14: **54/54 EditMode**, **4/4 PlayMode**, **64 ensamblados Player Windows compilados**. `git diff --check` sin errores en el alcance intervenido. XML y logs en `Logs/`; persisten avisos históricos del editor/licencia/memoria. Player valida scripts, no construye un ejecutable completo.

## Límites

La altura sigue usando Terrain activo cuando existe; todavía no se proyectan todos los slots al NavMesh ni se modelan plataformas/puentes como una superficie común. No se resuelve en esta fase el destino inalcanzable, path status, reasignación óptima del hueco dejado por una baja ni el bodyblock.

Los patrones continúan asignándose por orden de buffer durante un cambio explícito. La estabilidad implementada evita movimientos accidentales por compactación; no introduce una estrategia táctica de relleno de huecos. Los umbrales deben calibrarse en partida real. PlayMode conserva pruebas aisladas anteriores, no mide sensación, animación ni formaciones grandes en una escena completa. El grafo anterior sigue siendo histórico y no cambia el contrato de red.
