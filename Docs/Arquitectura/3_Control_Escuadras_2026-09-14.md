# Control de escuadras: reparación por fases

Fecha: 2026-09-14. Cambios locales, sin commit. Primera fase de la revisión del movimiento de escuadras y héroe.

## Alcance aplicado

Se corrige primero el contrato entrada → resolución → aplicación → formación/navegación, conservando los motores actuales.

1. `HeroAIExecutionSystem` publica `SquadAIOrderIntentComponent`, incluyendo `holdPosition`, antes del resolvedor. Conserva la intención aunque una reacción de combate la sustituya temporalmente; deja de escribir el canal legacy de entrada.
2. `SquadControlSystem` precede al resolvedor. Cambiar solo formación no vuelve a emitir movimiento. Corregidos el primer clic confundido con doble clic, el command buffer de salidas tempranas, timers sobrescritos por entrada del mismo frame y el ciclo sobre biblioteca vacía.
3. El resolvedor remoto compara tipo, fuente, destino, objetivo y formación. Una nueva posición con el mismo tipo de orden se aplica. Mantener posición no se sustituye por una reacción de combate, para jugador ni IA.
4. `SquadOrderSystem` deja de confirmar la formación. `FormationSystem` confirma tipo, slots, espaciado y cooldown tras asignar el patrón; rechaza patrones insuficientes y actualiza también el índice de slot.
5. Las altas/bajas de `SquadHoldPositionComponent` se publican al final de la aplicación de órdenes, antes de calcular el ancla, no en el siguiente frame.
6. Ancla, cálculo de centro, estado de formación y orientación reconocen mantener posición por la orden aunque la FSM esté `InCombat`. La retirada prevalece sobre el centro defensivo anterior.
7. Mantener posición permite atacar/orientarse, pero su destino sigue siendo el slot. Seguimiento aplica el leash aunque haya combate; `Attack` permite perseguir. Sin objetivo elegible se vuelve al slot respetando `Waiting`, sin borrar el objetivo usado por ataque.
8. Eliminados dos mapas por frame de navegación usados para omitir el leash según FSM/intención.
9. Registro explícito de `NavMeshAgent` en ECS: la prueba de IA detectó una excepción al consultar su presencia antes de crear el primer visual.

## Validación reproducible

Unity 6000.5.8f1, Entities 6.5.0.

```powershell
unity test . --mode EditMode --filter 'ArchitectureRegressionTests|SquadControlRegressionTests' --output Logs/squad-control-regression-results.xml --timeout 300 -- -nographics -logFile Logs/squad-control-regression-tests.log
unity test . --mode PlayMode --filter SquadNavigationRegressionTests --output Logs/squad-navigation-results.xml --timeout 300 -- -nographics -logFile Logs/squad-navigation-tests.log
unity run . --timeout 300 -- -nographics -executeMethod ArchitectureBuildValidation.CompilePlayerScripts -logFile Logs/squad-control-player-compile.log
```

EditMode: conexión IA→resolvedor, cambios de destino/objetivo, conservación de intención, mantener posición local/remota, visibilidad inmediata de hold, prioridad de retirada, formación/slots y política de persecución. Incluye las 14 regresiones anteriores.

PlayMode: NavMesh temporal y agente real; destinos durante combate para HoldPosition, FollowHero fuera del leash y Attack. El destino se compara con la superficie horneada, cuya altura puede diferir de la geometría fuente por voxelización.

Resultado final: **28/28 EditMode**, **3/3 PlayMode**, **64 ensamblados de Player Windows compilados**. Ejecuciones finales del 2026-09-14; XML y logs en `Logs/`. `git diff --check` sin errores en los archivos intervenidos comprobados.

Los primeros intentos detectaron el registro ausente de NavMeshAgent y una expectativa de altura incorrecta en el fixture. Ambos se corrigieron antes de estas ejecuciones finales. Persisten avisos del editor/licencia/memoria; no se consideran resueltos. La comprobación Player compila scripts, no genera un ejecutable completo.

## Pendientes explícitos

Actualización posterior: la corrección del respawn local, cancelación de intención y pose revisionada se implementó en [fase 2](4_Ciclo_Vida_Heroe_2026-09-14.md). La extracción completa del motor y los demás puntos siguen pendientes.

1. Muerte/respawn: salud correcta, cancelar intención, teletransporte explícito y autoridad de posición del héroe.
2. Intercambio/retirada: validar antes de retirar, marcadores activos e identidad consistentes, buffers de reserva, muerte del dueño y destinos competidores.
3. Formación/geometría: criterio por unidad, independencia de FPS, centro fraccional, cuaterniones válidos, slots estables tras bajas y lectura segura de bibliotecas.
4. Navegación física: llegada/fallo de ruta, proyección sobre suelo navegable, bodyblock y sincronización ordenados, perfilado.
5. Consolidar componentes duplicados y sacar el motor del héroe de presentación.

La FSM no se ha migrado completamente al resolvedor. El arbitraje local conserva su política histórica para nuevas órdenes explícitas. `Attack` también puede proceder de reacción de combate y mantiene permiso de persecución. No se ha validado una partida completa, animaciones, llegada final o todas las escenas/prefabs. No cambia el contrato de red.

El grafo previo no debe tratarse como evidencia actualizada de estos cambios; las referencias verificadas son los scripts y las pruebas.
