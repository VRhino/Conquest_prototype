# U6-00 — Baseline y validación común

Responsable: orquestador. Inspección: 2026-10-16. Estado: inspección estática y de infraestructura completada; nuevas medidas runtime pendientes.

## Infraestructura verificada en esta ejecución

- `unity status --format json`: `STATUS_NO_INSTANCES`, con advertencia de detección incompleta de procesos.
- `unity pipeline list --format json`: reconoce Conquest_prototype en ejecución, `hasPipelinePackage: true`, versión `0.8.0-exp.1`, `pipelineServer.isReachable: false`, `safeMode: null`.
- Conclusión: no hay canal de control del Editor utilizable confirmado. No concluir que Unity está cerrado ni que está en Safe Mode; no reiniciar el Editor ni iniciar un segundo Editor sobre el proyecto.
- No se ejecutaron pruebas, benchmarks ni cambios de configuración gráfica. Los resultados de Logs enumerados abajo son históricos, no resultados de esta ronda.

## Evidencia existente que sí podemos reutilizar

| Elemento | Ubicación | Qué aporta | Límite |
|---|---|---|---|
| Perfil de batalla completa | `Logs/system-optimization-full-battle-profile.json` | 600 muestras renderizadas, 20 héroes, 240 unidades iniciales / 228 al final, contactos durante las 600 muestras | Editor + test runner, VSync=1, sin baseline anterior ni GPU |
| Perfil de escena pequeña | `Logs/system-optimization-rendered-profile.json` | 300 muestras, 2 héroes y 24 unidades | No comparar directamente con batalla completa |
| Prueba sintética bodyblock | `Logs/system-optimization-bodyblock-timing.txt` | 900 agentes, 1,44 ms disperso / 4,18 ms denso y 0 bytes managed en el hilo medido | Mediciones warm-frame, no benchmark estadístico ni FPS |
| Perfil reproducible de batalla | `Assets/Tests/PlayMode/BattleSceneIntegrationTests.cs`, método `RenderedPerformanceProfile` | Main Thread, markers Conquest.Bodyblock y Conquest.EnemyDetection, GC y pares/contactos | Prueba explícita de Editor; no instrumenta targeting, GPU ni skinning |
| Pruebas de cuerpo/repulsión | `Assets/Tests/EditMode/BodyblockRegressionTests.cs`, `Assets/Tests/PlayMode/SquadNavigationRegressionTests.cs` | Clamp por segundo, solapamiento y escenarios físicos | Complementar equivalencia secuencial/paralela si se modifica el algoritmo |
| Sonda mapa | `Assets/Scripts/Map/WorldMap/WorldMapFpsProbe.cs` | Cuatro vistas, warmup, FPS/1% bajo/P95, CSV y control de cámara renderizada | El propio informe advierte que no mide CPU/GPU separadas, streaming ni carga inicial |
| Historial de optimización | `tasks/system-optimization-plan.md` | Cambios ya hechos y validación antigua | No certificar de nuevo suites históricas como si se hubieran ejecutado ahora |

En el perfil histórico de batalla completa, `detectionMs.p50=1.0187` y `bodyblockMs.p50=0.3061`; P95 1.0737 y 0.3331 respectivamente. Sirve para justificar investigar detección antes que una segunda gran reescritura de bodyblock. No demuestra que detección sea el mayor cuello de botella total. `mainThreadMs.p50=15.9519` incluye contexto de Editor, esperas y VSync; `gcBytesPerFrame.p50=127155` es global y no debe atribuirse a un sistema sin callstacks.

## Procedimiento exigido antes de aceptar rendimiento

1. Registrar commit, diff local relevante, Unity, paquetes, hardware, API gráfica, resolución, calidad, cámaras, VSync y cap de FPS. Conservar seed, posiciones, equipos y órdenes iniciales.
2. Separar pruebas funcionales de medición: aprobar regresiones relevantes primero. Crear baseline A del mismo escenario que B; conservar datos brutos por frame y metadatos.
3. Para simulación, ejecutar 300/600/900 unidades como tamaños experimentales, tanto distribuidas como en contacto denso; fijar número de escuadras y distribución porque el coste depende también de ellos. Calentar Burst y capacidades de contenedores, y medir varias repeticiones. Las bajas cambian la carga: escenario de rendimiento estable o registro por frame de población/contactos.
4. Registrar P50/P95/P99 en ms, tiempo de workers y esperas de jobs, allocations del sistema separadas del GC global, candidatos/contactos/objetivos. GPU debe medirse con contador/captura soportado; una muestra no disponible no equivale a cero.
5. Para render, comparar por separado GRD off, GRD on y GRD+occlusion cuando sea compatible. Mantener escena, cámara, calidad y efectos; validar materiales, sombras, LOD, aparición/desaparición y animación. Usar Player con render real para conclusiones de producto; Editor sirve para diagnóstico.
6. Aceptar sólo si el resultado supera la variabilidad entre repeticiones, conserva comportamiento y no desplaza el coste a GPU, memoria, latencia o otra fase. No fijar un porcentaje universal sin conocer ruido y presupuesto.

## Reutilización y límites de alcance

Se buscaron servicios Core, Inventory/Services, utilidades Shared, componentes/sistemas y eventos. Los servicios de inventario/persistencia no ofrecen una infraestructura de benchmarking apropiada. Se reutilizan tests, sonda de mapa, markers y config de bodyblock existentes; no se añade otra infraestructura C# en esta ronda.

Pendiente práctico: recuperar una conexión válida del Editor y elegir fixtures de carga reproducible antes de ejecutar A/B. No se cambia la instalación, no se cierran procesos ni se altera la escena actual como parte de este análisis.
