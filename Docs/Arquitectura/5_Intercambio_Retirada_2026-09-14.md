# Intercambio y retirada por reemplazo

Fecha: 2026-09-14. Tercera fase del pipeline de escuadras. Cambios locales sin commit.

## Cambios

- La ejecución valida antes de retirar: referencia actual, estado/buffer/identidad, héroe vivo y ubicado, reserva disponible, configuración de spawn, datos/definición/prefab, formación seleccionada con slots y punto aliado de retirada activo.
- Un reemplazo inválido consume la petición, pero conserva referencia, marcador activo, reservas y escuadra actual. No inicia cooldown.
- Channeling ya no aplica cooldown; ejecución lo aplica al aceptar el intercambio. Se mantiene la duración existente de 10 segundos.
- Se rechaza reactivar una instancia mientras siga marcada retirándose para el mismo héroe.
- Se retira `IsLocalSquadActive`, no el marcador de héroe `IsLocalPlayer`.
- La reserva antigua se actualiza sin duplicar entradas. Usa el mapa del héroe (fallback al contenedor solo para local) o su definición seleccionada. El total conserva `initialUnitCount` en vez de reducirse con el buffer de supervivientes.
- La selección del reemplazo y la retirada se publican antes del spawn y de aplicar órdenes. Órdenes tardías no desbloquean `retreatTriggered`.
- `SquadNavigationSystem` deja de llamar a SetDestination. Es observador de llegada, después de actualizar slots. El motor de unidades sigue siendo dueño del destino.
- La finalización de retirada comprueba todos los supervivientes en sus destinos de slot, no solo al líder en el centro. Conserva la salida por timeout existente y actualiza efectivos de reserva antes de eliminar las entidades retiradas.

## Validación

```powershell
unity test . --mode EditMode --filter 'ArchitectureRegressionTests|SquadControlRegressionTests|HeroLifecycleRegressionTests|SquadSwapRegressionTests' --output Logs/squad-swap-results.xml --timeout 300 -- -nographics -logFile Logs/squad-swap-tests.log
unity test . --mode PlayMode --filter 'SquadNavigationRegressionTests|HeroVisualRespawnRegressionTests' --output Logs/squad-swap-playmode-results.xml --timeout 300 -- -nographics -logFile Logs/squad-swap-playmode-tests.log
unity run . --timeout 300 -- -nographics -executeMethod ArchitectureBuildValidation.CompilePlayerScripts -logFile Logs/squad-swap-player-compile.log
```

Nuevos casos: rechazo sin definición, sin punto de retirada, sin efectivos, con instancia todavía retirándose y sin biblioteca; aceptación con identidad remota y reserva no duplicada; espera de todos los supervivientes y persistencia de efectivos; inmunidad de retirada a órdenes tardías.

Resultado final del 2026-09-14: **40/40 EditMode**, **4/4 PlayMode**, **64 ensamblados Player Windows compilados**. `git diff --check` sin errores en los scripts intervenidos. XML/logs en `Logs/`. Persisten avisos históricos del editor/licencia/memoria; esta validación no construye un ejecutable completo.

## Límites

Actualización posterior: la retirada por muerte se conecta en [fase siguiente](6_Retirada_Muerte_Dueno_2026-09-14.md). El alcance original de este documento continúa siendo el intercambio.

Esta fase cubre retirada por intercambio, no conecta todavía la muerte del dueño con creación de todos los componentes de retirada. No constituye una transacción distribuida ni rollback frente a excepciones del spawn posterior. Las comprobaciones cubren prerrequisitos conocidos, no la integridad completa de prefabs/escenas.

Persisten timeout de retirada, políticas de progresión entre intercambios, validación de una partida completa, geometría de slots, llegada/fallo de ruta y bodyblock. Los casos PlayMode son regresiones aisladas del motor NavMesh y puente visual, no una simulación completa A→B→A. El grafo anterior sigue siendo histórico.
