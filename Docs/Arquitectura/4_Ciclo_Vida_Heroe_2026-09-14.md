# Muerte, movimiento inhibido y respawn del héroe local

Fecha: 2026-09-14. Segunda fase del control/movimiento. Cambios locales, sin commit.

## Implementación

- `HeroRespawnSystem` consulta `HeroHealthComponent`, no la salud de unidades, y se ejecuta después de daño y antes de spawn.
- `HeroMovementSystem` se ejecuta después de input y spawn. Limpia la intención anterior si el héroe está muerto, espera ubicación o no tiene cámara.
- `HeroSpawnComponent.positionRevision` identifica publicaciones de pose. Spawn incrementa la revisión solo al colocar al héroe en un punto válido.
- El visual local consume la revisión una sola vez antes del movimiento y de la sincronización GO→ECS legacy. Desactiva temporalmente el CharacterController, aplica posición/rotación, restaura su estado y reinicia gravedad. No mueve en ese mismo Update.
- El consumidor físico también bloquea intenciones residuales de héroes muertos o pendientes de ubicación. No añade escrituras ECS desde presentación para confirmar el teletransporte; recuerda localmente la revisión aplicada.

Se conserva el puente híbrido actual y los cambios previos del usuario en EntityVisualSync. No se presenta este arreglo como extracción completa del motor de presentación.

## Pruebas

```powershell
unity test . --mode EditMode --filter 'ArchitectureRegressionTests|SquadControlRegressionTests|HeroLifecycleRegressionTests' --output Logs/hero-lifecycle-results.xml --timeout 300 -- -nographics -logFile Logs/hero-lifecycle-tests.log
unity test . --mode PlayMode --filter 'SquadNavigationRegressionTests|HeroVisualRespawnRegressionTests' --output Logs/hero-visual-respawn-results.xml --timeout 300 -- -nographics -logFile Logs/hero-visual-respawn-tests.log
unity run . --timeout 300 -- -nographics -executeMethod ArchitectureBuildValidation.CompilePlayerScripts -logFile Logs/hero-lifecycle-player-compile.log
```

EditMode comprueba muerte con salud canónica, temporizador, solicitud de respawn, intención cancelada, ausencia de punto válido y publicación única de revisión. Incluye las regresiones anteriores.

PlayMode comprueba el Update real de EntityVisualSync con CharacterController en un mundo aislado: la posición de respawn alcanza GO y ECS, la revisión no se reaplica y la muerte inhibe intención residual. Invoca Update explícitamente para aislar el orden del puente; no simula una partida completa. Incluye los tres casos NavMesh anteriores.

Resultado final del 2026-09-14: **32/32 EditMode**, **4/4 PlayMode**, **64 ensamblados Player Windows compilados**. `git diff --check` sin errores en los scripts intervenidos. XML y logs en `Logs/`. Persisten avisos históricos del editor/licencia/memoria; no se consideran resueltos. Player valida scripts, no construye un ejecutable completo.

## Límites y siguiente bloque

Siguen pendientes intercambio/retirada de escuadras, muerte del dueño, motor y autoridad general de rotación, estados de ataque al morir, ciclo de vida de héroes remotos, geometría y bodyblock. El cambio de salud habilita la ruta de respawn local; no certifica todas las consecuencias de muerte en otros subsistemas. No hay ejecutable completo ni verificación visual de todas las escenas. El grafo anterior sigue siendo histórico.
