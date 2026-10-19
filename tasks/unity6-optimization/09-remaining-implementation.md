# Implementación de mejoras pendientes

Inicio: 2026-10-17. Autorización: «implementa las faltantes». Orquestador: agente principal. Subagentes: GPT-6 Luna.

## Plan

- [x] Revisar checkout, backlog, grafo y contratos; conservar cambios previos.
- [x] OPT-02/03/04: snapshot e índice espacial de detección, consulta Burst y publicación determinista antes de AI. Responsable: Luna `cpu_detection`.
- [x] OPT-05/06/12/13: eliminar escrituras redundantes, compartir estado físico confirmado y preservar colisiones animadas fuera de cámara. Responsable: Luna `hybrid_bodyblock`.
- [ ] OPT-08/09/10/11: censo y perfiles gráficos experimentales reversibles, aislando Forward+, GRD, oclusión y SSAO. Responsable: Luna `render_gpu`; ejecución del Editor centralizada.
- [x] OPT-07: spike de consultas LowLevel, comprobar equivalencia/lifetime antes de cualquier sustitución de proyección. Responsable: Luna `render_gpu`, revisión del orquestador.
- [ ] OPT-00: compilar, ejecutar regresiones y recoger perfil representativo; diagnosticar la espera de contacto del fixture completo. Responsable: orquestador.
- [ ] Revisar diffs e integrar; actualizar documentación, lessons, estados y grafo con los resultados reales.

## Condiciones de integración

No se altera el orden de candidatos, el radio exacto 3D, la autoridad NavMesh/CharacterController, las fuerzas de bodyblock ni la publicación antes de los consumidores del mismo frame. Las opciones gráficas se comparan por separado y se restaura la configuración activa. Una nueva API de proyección no sustituye SamplePosition sin demostrar que conserva sus resultados. Las optimizaciones de animación deben conservar el movimiento de colliders de armas/escudos fuera de cámara. No se considera terminada una tarea sólo por disponer de un informe.

## Reutilización del trabajo existente

| Área | Elementos reutilizados | Decisión |
|---|---|---|
| Detección | EnemyDetectionSystem, buffers DetectedEnemy/SquadTargetEntity, regresiones de arquitectura | Extender sistema y pruebas, no crear un segundo dueño de targeting |
| Navegación/animación | NavMeshPositionSyncSystem, UnitAnimationMovementComponent, EntityVisualSync y VisualSyncUtility | Extender puentes y componentes existentes |
| Render | WorldMapFpsProbe y fixture RenderedPerformanceProfile | Conservar capturas existentes; herramienta Editor separada sólo si no existe equivalente para clones/censo |
| Proyección | UnitNavMeshSystem.TrySampleDestination y caches de NavAgentComponent | Instrumentar el helper compartido y contrastar LowLevel aisladamente |

## Evidencia y resultados

Las 130 pruebas EditMode y 31 PlayMode de la ronda anterior no validan aún estos cambios nuevos.

Se recuperó el último perfil completo de la ronda anterior, que terminó aprobado después de la interrupción del CLI: 600 frames, 552 con contacto, 20 héroes y 240→216 unidades. Se preservan XML/JSON en `Logs/unity6-optimization-round2/full-battle-before.*`. Detección P50/P95 1,0319/1,0616 ms; targeting 0,1413/0,1623 ms. Esta evidencia confirma prioridad para detección. El primer intento fallido permanece documentado por separado.

## Cambios integrados, antes de validar

- Detección: snapshot e índice XZ persistentes, búsqueda Burst en un IJob, orden por índice original de ocurrencia y publicación síncrona antes de AI; radio exacto 3D, duplicados y excepciones no finitas preservados. No se alteró el algoritmo de targeting. Se añadieron contadores y marcadores Snapshot/SearchAndWait/Publish.
- Puentes: EntityVisualSync omite la escritura de LocalTransform cuando no cambia, conservando rotación y el puente de ataque. NavMeshPositionSync publica velocidad física en UnitAnimationMovementComponent y UnitAnimation reutiliza ese dato después de bodyblock. Se conserva fallback por delta para entidades sin agente y velocidad cero para agentes deshabilitados/fuera de NavMesh.
- Animación: RemoteHeroAnimationDriver evita repetir bool/int/float idénticos e invalida caché al cambiar vínculo, Animator/controller o enabled. Los triggers continúan siendo pulsos. Los rigs que mueven colliders de armas o escudos conservan actualización fuera de cámara; no se aplica AlwaysAnimate a visuales sin esos hitboxes. Esta protección es una corrección funcional que puede costar CPU, no una promesa de FPS.
- Render: RenderOptimizationExperiments ofrece censo, clones A/B/C/D y SSAO on/off, validación de requisitos y restauración de quality override/BRG tras fin o reload. Los clones se reutilizan por GUID y hash de dependencias; renderer features aisladas y SaveAssetIfDirty sólo sobre assets propios. El fixture tiene seis entradas explícitas separadas y escribe censo fuera de la ventana medida.
- Navegación: se instrumentó TrySampleDestination sin alterar su consulta/cache. El spike CompareLowLevelProjection usa NavWorld.MapLocation en IJobFor, registra AddDependency y completa antes de disponer world/arrays. Compara contra SamplePosition con el mismo tipo de agente y máscara. Sigue siendo diagnóstico: caja y radio no son equivalentes por contrato.

Primera compilación detectó ajustes de referencias/APIs de Editor; corregidos. La primera suite EditMode dio 130/133: tres fixtures nuevos retenían buffers durante cambios estructurales; se repararon sin cambiar producción. XML inicial preservado en `editmode-initial-failed.xml`. Validación en curso.

## Checkpoint para push — 2026-10-19

La recompilación terminó sin errores. EditMode: 133/133 aprobadas (`Logs/unity6-optimization-round2/editmode-post.xml`). PlayMode: 33/34 aprobadas en la ejecución terminada el 17/10; el test nuevo `BattleSceneMeleeWeaponRigsContinueAnimatingOutsideAllCameraFrusta` espera un WeaponHitboxBehaviour en el prefab de Spearmen y obtiene null. Falta ajustar el fixture a los colliders reales y repetirlo. No se da esta ronda por validada ni se afirma mejora de FPS. El usuario pidió pushear el checkpoint antes de continuar: se suben sólo los cambios de optimización y se conservan aparte sus cambios locales previos.

Checkpoint subido y comprobado en origin: `codex/unity6-dots-optimization`, `a1190da7288e33bf2e40e61f2aa837b84a5b2a76`. Los cambios de preparación de batalla, coordinación, fuentes y layout del usuario quedaron fuera. Sólo se incluyeron las lecciones propias del último hunk de lessons.

## Continuación del 2026-10-19

Se confirmó mediante Editor que Spearmen tenía un WeaponHitbox con BoxCollider trigger pero carecía tanto de WeaponHitboxBehaviour como de Rigidbody. Se añadieron ambos usando PrefabUtility, copiando la política física de Squires (kinematic, sin gravedad), sin cambiar geometría, daños ni alcance. El diff semántico del asset LFS conserva el collider y añade la conexión; incluye la normalización automática de campos de Inspector obsoletos al guardar.

El controller actual de Spearmen es el base de locomoción y no tiene layer Combat. La prueba fuera de cámara valida el Attack existente de Squires y el Run existente de Spearmen; no inventa una máquina de estados ni afirma que Spearmen tenga ya una animación de ataque enlazada. Comprueba componentes físicos, culling, invisibilidad real y movimiento del collider del arma. PlayMode posterior: **34/34 aprobadas**, 37,97 s, XML real finalizado el 19/10 a las 08:00:28 UTC y preservado en `playmode-post.xml`.

La lectura de resultados fuera del workspace debe hacerse en el mismo contexto del Editor: el sandbox de Windows devolvía una copia antigua de LocalLow. Se confirmó la ruta con Application.persistentDataPath y se copió el XML actual al workspace desde el contexto autorizado. No se confunde el XML del 17/10 con las nuevas ejecuciones.

## Validación adicional y comparación gráfica — 2026-10-19

- EditMode: 133/133; PlayMode: 34/34. Diferencial de detección: aprobado con Burst activado y desactivado, conservando las opciones originales del Editor.
- Spike LowLevel: aprobado funcionalmente como herramienta, pero rechazado como sustituto de producción. En 64 posiciones y tres máscaras, MapLocation difiere en una validez para AllAreas y área 0; la caja no reproduce el radio de SamplePosition. Tampoco fue más rápido en esta muestra. Se mantienen SamplePosition y sus caches.
- Primera captura posterior completa: detección P50/P95 0,2213/0,3114 ms; no constituye A/B controlado frente a la captura histórica (población final, culling y opciones Burst distintos). No se atribuye una mejora global de FPS.
- Se detectó que el censo serializaba 186.640 SkinnedMeshRenderer, incluidos inactivos: 126 MB de diagnóstico podían contaminar el perfil. Se corrigió con totales completos, totales activos separados y un máximo explícito de 256 filas activas de detalle. La herramienta compila sin errores. La comparación de siete perfiles se repite con esta corrección; la captura anterior se conserva como evidencia descartada para tiempos globales.
- Los perfiles GRD/oclusión/SSAO siguen aislados de producción mientras se comprueba inicialización real y coste. Al terminar se deben restaurar quality 2, URP-HighFidelity, BRG KeepIfEntitiesGraphics y Burst False/False originales.

Se encontró y corrigió un defecto del helper: ConfigureVariant asignaba renderingMode pero no marcaba UniversalRendererData como dirty. Tras recarga los clones B/C/D seguían Forward (m_RenderingMode=0) y Unity rechazaba GRD. Ahora se reaplica configuración a clones existentes después de validar aislamiento, se marca el renderer propio y se guarda. Comprobación en disco mediante Editor: A=0, B/C/D=2; SSAO conserva 0. Las capturas anteriores se preservan en render-invalid-forwardplus y no se atribuyen a Forward+/GRD. Se repite la batería completa.

Revisión final cruzada de Luna: sin defecto accionable en los cambios del test offscreen ni en el sentinel LowLevel. El primero comprueba rig/culling, no la entrega completa de daño; GPU timings son agregados de frames completados con retraso, no parejas CPU/GPU alineadas por índice. La batalla continúa evolucionando durante las 600 muestras; VSync=1 y Main Thread incluye espera/presentación. No se consideran estos perfiles un experimento render-only ni un benchmark Player.

Tras corregir persistencia, GRD confirmó inicialización real. La repetición de Production agotó la espera de contacto; se conserva la captura Production válida anterior, pero no se mezcla como A/B controlado de esta batería.

La batería corregida terminó 4/7: AForward, BForwardPlus, CForwardPlusGRD y SSAOOn aprobados; Production y D agotaron espera de contacto; SSAOOff encontró un experimento activo heredado del fallo anterior. Se añadió restauración explícita al inicio de UnityTearDown: el runner puede abortar un IEnumerator anidado sin disponer el wrapper, por lo que finally no bastaba. Se repite SSAOOff aisladamente.

Los diagnósticos de los timeouts muestran MatchState InBattle, cero candidatos/contactos y héroes con decisiones de captura/defensa, varios agentes inmóviles con remainingDistance=0 lejos del target de la decisión. No se atribuye este fallo al renderer ni se altera navegación para forzar el benchmark. La comparación de render puro permanece pendiente de un fixture de carga estable.

## Resultados gráficos válidos tras las correcciones

SSAOOff aislado: 1/1 aprobado, 80,48 s. Las cinco capturas siguientes contienen 600 frames renderizados cada una. Son diagnósticos de batalla dinámica con Burst activado, DX11, RTX 4080 SUPER, 1920×1080 y VSync=1; no son un A/B con simulación congelada.

| Perfil | Unidades finales | Frames con contacto | GPU P50/P95 ms | Main Thread P50 ms |
|---|---:|---:|---:|---:|
| Forward | 239 | 600 | 3,746 / 7,304 | 19,968 |
| Forward+ | 216 | 102 | 3,082 / 6,083 | 15,604 |
| Forward+ GRD | 216 | 429 | 3,459 / 6,288 | 14,201 |
| SSAO off | 228 | 392 | 2,487 / 4,442 | 16,656 |
| SSAO on | 216 | 184 | 3,214 / 5,955 | 13,549 |

GRD inicializado confirmado en su captura; oclusión desactivada como corresponde a C. D no tiene captura válida por timeout de contacto. Main Thread incluye esperas; la diferencia de población/contactos impide atribuir toda la variación al perfil. Decisión: conservar los seis clones opt-in; no promover ningún ajuste gráfico a producción con esta evidencia. Pendiente: fixture render-only estable, repetición de D y validación visual/Frame Debugger antes de una decisión de calidad.

El censo acotado de Production enumera 4.191 MeshRenderer totales / 4.091 activos y 186.640 SkinnedMeshRenderer totales / 4.353 activos; 3.841 candidatos conservadores a GRD, no prueba de batching efectivo. JSON de 175 KB frente a 126 MB antes de limitar detalles.

## Cierre solicitado — 2026-10-19

El usuario solicita commitear todos los cambios locales e integrarlos en main, dando por cerrada esta ronda. La última repetición PlayMode, terminada a las 15:27:33, aprobó **34/34** con las opciones originales del Editor; se conserva también la ejecución 32/34 anterior. No se atribuye una causa definitiva a los fallos transitorios de F2/click. EditMode previo: 133/133. Se restauró y comprobó quality=2, URP-HighFidelity, BRG KeepIfEntitiesGraphics y Burst False/False.

Se cierra la implementación y se mantienen documentados los límites gráficos: cinco capturas válidas, GRD inicializado, ningún ajuste experimental promovido por defecto y sin afirmación de ganancia global de FPS. El benchmark de oclusión y la comparación render-only estable no quedan validados. No se ejecuta una nueva compilación Player en este cierre. La integración incluye también los cambios locales previos del usuario por autorización expresa; AGENTS.md permanece local.
