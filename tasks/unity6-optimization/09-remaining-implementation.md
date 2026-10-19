# Implementación de mejoras pendientes

Inicio: 2026-10-17. Autorización: «implementa las faltantes». Orquestador: agente principal. Subagentes: GPT-6 Luna.

## Plan

- [x] Revisar checkout, backlog, grafo y contratos; conservar cambios previos.
- [ ] OPT-02/03/04: snapshot e índice espacial de detección, consulta Burst y publicación determinista antes de AI. Responsable: Luna `cpu_detection`.
- [ ] OPT-05/06/12/13: eliminar escrituras redundantes, compartir estado físico confirmado y preservar colisiones animadas fuera de cámara. Responsable: Luna `hybrid_bodyblock`.
- [ ] OPT-08/09/10/11: censo y perfiles gráficos experimentales reversibles, aislando Forward+, GRD, oclusión y SSAO. Responsable: Luna `render_gpu`; ejecución del Editor centralizada.
- [ ] OPT-07: spike de consultas LowLevel, comprobar equivalencia/lifetime antes de cualquier sustitución de proyección. Responsable: Luna `render_gpu`, revisión del orquestador.
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
