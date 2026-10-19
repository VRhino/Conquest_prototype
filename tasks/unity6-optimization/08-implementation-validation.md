# Implementación y validación — primera ronda

Fecha: 2026-10-16. Coordinación: agente principal; implementación acotada: GPT-6 Luna `hybrid_bodyblock` y `cpu_detection`; revisión independiente: Luna `render_gpu`.

## Plan de esta ronda

- [x] Recuperar Pipeline y registrar Editor `ready` en 7800.
- [x] Registrar baseline aislado de detección/targeting de 900 unidades.
- [x] Instrumentar `Conquest.UnitTargeting` y el perfil renderizado existente sin cambiar gameplay.
- [x] Mejorar el test existente de bodyblock para comparar pares/contactos y huella de posición fuera del tramo cronometrado.
- [x] Registrar tres ejecuciones instrumentadas de bodyblock con grid original.
- [ ] Registrar baseline renderizado de batalla completa: intento fallido al no producir contactos; diagnóstico pendiente, no se considera baseline válido.
- [x] Retirar exclusivamente grid managed muerto, preservar grid nativo y verificar equivalencia.
- [x] Ejecutar regresiones funcionales, perfil pequeño postcambio y compilación de scripts Player.
- [ ] Revisar diff, actualizar documentación/lessons y grafo.

## Infraestructura

El listener 7800 estaba activo, pero faltaba `Library/Pipeline/.unity-pipeline-port`. Se trajo Unity al frente y se detuvo/arrancó únicamente el servidor desde Window/Pipeline; entonces el CLI confirmó `ready`. Los menús nativos abiertos pueden bloquear el dispatcher del hilo principal: se cerraron antes de emitir comandos. Tras editar C#, se pidió recompile y se comprobó `recompile_status=completed, failed=false`; un corte de transporte durante recarga no se trató como prueba de fallo de compilación.

Se conservan cambios locales previos. No se modifica la configuración gráfica ni la autoridad física. Los tests de integración sustituyen persistencia por proveedores en memoria/temporales y restauran el estado en teardown.

## Entorno observado

Unity 6000.5.8f1, Ryzen 7 7800X3D, RTX 4080 SUPER, Direct3D11, calidad High Fidelity, 1920×1080, VSync=1. Metadatos completos: `Logs/unity6-optimization/environment.json`. La escena inicial LoginScene estaba limpia y fuera de PlayMode.

## Baseline aislado

`ArchitectureRegressionTests.EnemyDetectionKeepsOneCandidateBufferPerSquadAtNineHundredUnits`: 1/1 pasa. Un frame caliente: detección 0,65 ms y targeting 1,68 ms; 0 B managed registrados por ambas fases. Datos: `detection-scale-baseline.xml`. Es muestra aislada de Editor, no benchmark estadístico ni FPS.

El primer ensayo de bodyblock cargó la versión anterior del test; se conserva en `bodyblock-baseline-01.xml`, pero se excluye de la comparación instrumentada. Tras recompilar, `bodyblock-baseline-02.xml` a `04.xml` registran:

| Carga 900 agentes | Tiempos de Update (ms, tres ejecuciones) | Pares candidatos/contactos | Huella de posiciones |
|---|---|---|---|
| Dispersa | 0,78 / 0,68 / 0,66 | 900 / 0 | E3F898B5 |
| Densa | 2,95 / 2,87 / 2,83 | 115428 / 59048 | 2D9298BE |

0 B managed en el hilo principal del Update medido en las tres ejecuciones. El cronómetro se construye antes del contador GC; pares y huella se capturan después de cerrar ambos tramos. La huella FNV de XYZ cuantizado a 1e-4 aporta comparación compacta, no demuestra por sí sola equivalencia física ni es un hash de todo el estado del juego.

Todos los datos nuevos se guardan en `Logs/unity6-optimization/`; no se confunden con los perfiles históricos, que se copian por separado antes de volver a usar las rutas de salida del fixture.

## Cambios aplicados y revisión

`UnitBodyblock.System.cs`: retirados `_grid`, `_bucketPool`, su construcción y helpers sin consumidores. Se mantiene el grid nativo que ya usaba `RepulsionJob`, los parámetros, filtros, fuerzas, orden de cálculo y la barrera antes de `NavMeshAgent.Move`. El test consulta ahora la capacidad nativa reutilizada en vez de un pool eliminado. La documentación de responsabilidades y mecánica refleja ese único grid.

`UnitTargeting.System.cs` añade el scope `Conquest.UnitTargeting`; el fixture renderizado existente captura sus percentiles y exige que el recorder sea válido. No se cambió selección de objetivos. Esto cubre la duración de targeting de OPT-02, no aún todos los subcontadores ni el coste aislado de la instrumentación.

Durante el perfil completo aparecieron advertencias repetidas del Animator: ambos puentes de héroe escriben `AnimationHashes.IsAttacking`, pero el controller compartido declaraba `isAttacking`. Se corrigió el nombre mediante AnimatorController/AssetDatabase en el Editor, conservando `TriggerAttack` y las transiciones. El test nuevo valida nombre/hash/tipo, publicación del bool local/remoto y consumo del pulso ECS. No prueba la transición al clip Attack ni su finalización; ninguna transición de este controller usa el bool, por lo que este parche repara el contrato y las advertencias, no diseña una nueva máquina de estados.

Revisión independiente Luna `render_gpu`: no detectó otro fallo material en los diffs; señaló ese límite de la prueba de Animator, incorporado aquí. No se modificaron perfiles gráficos ni navegación.

## Validación posterior

| Validación | Resultado | Evidencia |
|---|---|---|
| EditMode `Conquest.Architecture.Tests` | 130/130 pasan | `editmode-post.xml` |
| PlayMode `Conquest.Navigation.Tests`, no explícitos | 31/31 pasan, incluido puente de ataque local/remoto | `playmode-post.xml` |
| Stress de bodyblock, otras dos ejecuciones | 1/1 pasa en cada una | `bodyblock-post-01.xml`, `bodyblock-post-02.xml` |
| Perfil renderizado pequeño explícito | 1/1 pasa; 300 frames de cámara, 2 héroes y 24 unidades | `small-battle-post-profile-test.xml`, `small-battle-post-profile.json` |
| Compilación de scripts StandaloneWindows64 | 68 ensamblados, confirmación `ARCHITECTURE_PLAYER_SCRIPTS_OK` | `player-script-compilation.json`, `Temp/ArchitecturePlayerCompilation` |

La compilación Player terminó después del timeout de 5 s del dispatcher; se comprobó el log de éxito y los 68 DLL nuevos, sin repetirla. Es compilación de scripts, no build completo ni medida runtime de Player.

Bodyblock posterior, tres muestras (suite + dos aisladas):

| Carga 900 agentes | Tiempos (ms) | Mediana antes / después | Pares candidatos/contactos | Huella |
|---|---|---|---|---|
| Dispersa | 0,59 / 0,69 / 0,98 | 0,68 / 0,69 ms | 900 / 0 | E3F898B5 |
| Densa | 2,79 / 2,86 / 3,23 | 2,87 / 2,86 ms | 115428 / 59048 | 2D9298BE |

Las tres muestras mantienen los mismos pares/contactos/huellas que el baseline y 0 B managed en el Update medido del hilo principal. Se eliminó trabajo muerto, pero **los tiempos están dentro de la variabilidad y no demuestran mejora de CPU/FPS**. Los asserts funcionales de clamp, solapamiento y navegación de la suite también pasan.

Perfil pequeño posterior (Editor, sin baseline comparable):

| Tiempo | P50 | P95 |
|---|---|---|
| Main Thread | 5,9114 ms | 6,7717 ms |
| Bodyblock | 0,0412 ms | 0,0517 ms |
| Detección | 0,0255 ms | 0,0322 ms |
| Targeting | 0,0172 ms | 0,0215 ms |

0 frames con contacto; GC global P50 57359 B/frame incluye runner/Editor y no equivale al GC de estos sistemas. Esta captura valida el recorder y el muestreo renderizado; no identifica el cuello de una batalla a escala ni mide GPU.

## Perfil completo y límites pendientes

`FullBattlePreparationRenderedPerformanceProfile` alcanzó el roster esperado de 20 héroes y 240 unidades, pero falló esperando contacto bodyblock entre equipos durante 120 s (`full-battle-baseline-failed.xml`). No produjo un nuevo perfil válido. Se conservan aparte los JSON históricos y no se presentan como medidas nuevas.

La advertencia `UnitVisual_Escudero` corresponde a una clave solicitada que no coincide con el registro, pero `SquadVisualManagementSystem.FindUnitVisualPrefab` sí usa el prefab de respaldo del tipo. No apareció la advertencia posterior de ausencia de visual; esa clave por sí sola no demuestra la causa del fallo de contacto. Hace falta comprobar fase, órdenes, destinos, velocidad y distancia entre equipos antes de volver a perfilar la batalla completa. No se elimina la aserción de contacto ni se cambia gameplay para obtener un JSON.

Siguiente decisión de optimización: recuperar esa carga representativa y contrastar detección/targeting antes de un índice espacial o jobs nuevos. Forward+/GRD/oclusión, LowLevel NavMesh y LOD siguen condicionados a sus pruebas del backlog; no se dan por implementados con esta ronda.

### Resultado recuperado al continuar el 2026-10-17

El último reintento iniciado el 16/10 sí terminó mientras se interrumpía el acceso al CLI: XML con `FullBattlePreparationRenderedPerformanceProfile` 1/1 aprobado, duración 81,39 s, final 13:02:18 UTC. El JSON de 15:02:17 registra 600 frames renderizados, 20 héroes, 240 unidades iniciales/216 finales y 552 frames con contacto. Detección P50/P95 1,0319/1,0616 ms; targeting 0,1413/0,1623 ms; bodyblock 0,2644/0,2834 ms; Main Thread 13,6712/14,9632 ms. Corrige el estado pendiente del reintento, sin borrar el primer fallo. Se preservó antes de los nuevos cambios en `Logs/unity6-optimization-round2/full-battle-before.json` y `.xml`. Continúa siendo Editor, sin GPU ni equivalencia temporal exacta de población durante combate.
