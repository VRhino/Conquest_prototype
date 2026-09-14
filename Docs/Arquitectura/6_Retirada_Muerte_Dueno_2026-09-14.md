# Retirada por muerte del dueño

Fecha: 2026-09-14. Cambios locales sin commit.

## Contrato aplicado

`SquadOwnerDeathRetreatSystem` lee `HeroLifeComponent` después de `HeroRespawnSystem`, actualiza `lastOwnerAlive` y publica una retirada completa antes de órdenes, intercambio y actualización del HUD. La FSM ya no inicia por su cuenta una retirada sin sus componentes operativos.

- `SquadOwnerDeathRetreatComponent` conserva la decisión aunque el dueño reviva.
- La retirada bloquea órdenes y combate mediante estado Retreating y `retreatTriggered`, y retira el marcador de escuadra local activa. El HUD no vuelve a añadirlo.
- Un punto aliado activo habilita `RetreatComponent` y navegación. Sin punto válido, se espera conservando posición y rotación del ancla: no se inventa un destino en el origen ni se sigue al héroe revivido.
- La referencia del héroe se conserva hasta finalizar para impedir otra instancia simultánea. Una retirada previa por intercambio nunca se reinicia ni cambia de causa.
- `RetreatLogicSystem` conserva supervivientes, total inicial e identidad en reservas antes de eliminar unidades/escuadra. Solo elimina HeroSquadReference si todavía apunta a esa escuadra.
- Spawning no crea escuadras para héroes muertos ni reservas eliminadas. Al revivir y completar ubicación, la selección existente permite desplegar únicamente los supervivientes, una vez terminada la retirada anterior.
- La FSM mantiene la retirada incluso si muere la última unidad; no desvía a KO una limpieza comprometida.

Configuración: `SquadSpawnConfigAuthoring.ownerDeathRetreatDuration` (5 s por defecto) y `retreatArrivalThreshold` (0,5 m). La duración se publica en el componente de retirada al encontrar destino; esperar destino no consume el timeout. Creadores manuales de SquadSpawnConfigComponent deben configurar estos campos; duración cero permite finalización inmediata.

## Verificación reproducible

```powershell
unity test . --mode EditMode --filter 'ArchitectureRegressionTests|SquadControlRegressionTests|HeroLifecycleRegressionTests|SquadSwapRegressionTests|SquadOwnerDeathRegressionTests' --output Logs/owner-death-results.xml --timeout 300 -- -nographics -logFile Logs/owner-death-tests.log
unity test . --mode PlayMode --filter 'SquadNavigationRegressionTests|HeroVisualRespawnRegressionTests' --output Logs/owner-death-playmode-results.xml --timeout 300 -- -nographics -logFile Logs/owner-death-playmode-tests.log
unity run . --timeout 300 -- -nographics -executeMethod ArchitectureBuildValidation.CompilePlayerScripts -logFile Logs/owner-death-player-compile.log
```

Los casos nuevos cubren héroe vivo, muerte, respawn antes/después de completar retirada, ausencia de punto y posterior recuperación, ancla congelada, conservación de efectivos y redespliegue real ECS, eliminación total, referencia más reciente y coexistencia con retirada por intercambio. Las regresiones PlayMode mantienen el NavMesh y el puente visual anteriores; no simulan una batalla completa.

Resultado final del 2026-09-14: **46/46 EditMode**, **4/4 PlayMode**, **64 ensamblados Player Windows compilados**. `git diff --check` sin errores en los scripts intervenidos comprobados. XML y logs en `Logs/`; persisten avisos históricos del editor/licencia/memoria. La comprobación Player compila scripts, no produce un ejecutable completo.

## Límites

La desaparición de la entidad dueña sin un HeroLifeComponent legible no equivale aquí a muerte; falta una política explícita de desconexión/destrucción. Los héroes remotos deben publicar correctamente su estado de vida; su detección de muerte/respawn no se implementa en esta fase. Sin puntos aliados activos la retirada espera, incluso después del respawn: es una condición pendiente del mapa, no una eliminación silenciosa.

Persisten geometría/formaciones, dependencia de FPS, bodyblock, separación del motor visual, cancelación completa de estados de ataque y persistencia de progreso durante redespliegues. La reserva conserva efectivos, no constituye un nuevo snapshot completo de progresión. El grafo anterior no se considera actualizado; las evidencias son scripts y pruebas. No hay cambios de contrato de red.
