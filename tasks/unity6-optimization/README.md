# Optimización Unity 6 / DOTS — coordinación

Fecha: 2026-10-16. Orquestador: agente principal. Modelo de los subagentes: `gpt-6-luna`, razonamiento alto.

## Objetivo y alcance de esta ejecución

Investigar las cinco oportunidades propuestas, contrastarlas con código y documentación de la versión instalada y ejecutar cambios pequeños con pruebas y evidencia. Las primeras rondas entregaron análisis y diseños; la implementación posterior se registra en [08-implementation-validation.md](08-implementation-validation.md). Una recomendación no equivale a una optimización medida.

Base: Unity 6000.5.8f1, Entities/Entities Graphics/Collections/Physics 6.5.0, Burst 1.8.30, URP 17.5.0. Grafo construido desde c7bda70c. Hay cambios locales previos en código, assets, ajustes, documentación y lessons: deben conservarse.

## Plan y asignaciones

| ID | Trabajo | Responsable | Estado | Entrega |
|---|---|---|---|---|
| U6-00 | Verificar infraestructura de medición, evidencia histórica y criterios comunes | Orquestador | Completada; sin medidas runtime nuevas | [Baseline](00-baseline-validation.md) |
| U6-01 | Detección, targeting, índice espacial y jobs ECS | Luna `cpu_detection` | Completada y revisada | [Detección/targeting](01-detection-targeting.md) |
| U6-02 | Bodyblock, sincronización NavMesh y datos para animación | Luna `hybrid_bodyblock` | Completada y revisada | [Bodyblock/puentes](02-hybrid-bodyblock.md) |
| U6-03 | GPU Resident Drawer, oclusión, perfiles y materiales reales | Luna `render_gpu` | Completada y revisada | [Render GPU](03-gpu-rendering.md) |
| U6-04 | API de navegación LowLevel: disponibilidad y prueba incremental | Luna `cpu_detection`, segunda ronda | Completada y revisada; spike runtime pendiente | [Navegación desde jobs](04-navigation-jobs.md) |
| U6-05 | Render Graph, efectos y presentación por distancia | Luna `hybrid_bodyblock`, segunda ronda | Completada y revisada | [Render Graph/presentación](05-rendergraph-presentation.md) |
| U6-06 | Revisar evidencia, resolver contradicciones y ordenar tareas finales | Orquestador; revisión cruzada Luna `render_gpu` | Completada | [Backlog revisado](06-reviewed-backlog.md) |

## Contrato de entrega

Cada área debe incluir hechos con archivo/línea, tabla de reutilización, propuestas pequeñas con archivos afectados, dependencias, riesgo y criterios de aceptación, pruebas existentes reutilizables, y separación explícita entre observaciones estáticas, resultados históricos y medidas nuevas. APIs y compatibilidad se verifican contra paquetes instalados o documentación oficial. No se prometen porcentajes ni FPS sin experimentos comparables.

En la investigación, los subagentes escribieron sus informes; en la implementación recibieron archivos acotados y revisiones. El orquestador concentra el control del Editor para evitar interferencias y revisa cada resultado. No se crean chats adicionales ni se usan servicios de inventario como sustitutos de datos nativos de simulación.

## Criterio de revisión del orquestador

- Conservar contratos de autoridad y orden de sistemas, selección/desempates, contactos y cadencia de gameplay.
- Separar mejoras nuevas de Unity 6 de capacidades ya existentes en Entities 1.3.
- Reutilizar índices, buffers, utilidades y pruebas cuando corresponda, sin acoplar sistemas de distinta responsabilidad.
- No eliminar `Complete()` sin identificar el consumidor y una cadena válida de dependencias.
- No extrapolar GPU Resident Drawer a instancing manual ni a toda la animación de personajes.
- Aceptar una optimización de rendimiento sólo después de equivalencia funcional y medición comparable.
- Al terminar: actualizar estados, enlazar informes, registrar correcciones y límites reales.

## Resultado de la ronda

Cinco informes de área completados por GPT-6 Luna y contrastados por el orquestador, más revisión cruzada de detección/bodyblock. Hay 14 tareas OPT-00 a OPT-13 definidas con archivos, dependencias y aceptación en el backlog. OPT-01 ya está implementada y validada; OPT-00/02 están parcialmente completadas. Se retiró el grid managed muerto, se instrumentó targeting y se corrigió el contrato del bool de ataque del Animator. No se ha demostrado una ganancia de FPS.

Próxima prioridad: diagnosticar por qué el fixture de batalla completa no produjo contactos y recuperar una captura representativa de detección/targeting. Primera prueba gráfica: separar Forward→Forward+, GRD y oclusión. Navegación LowLevel necesita demostrar equivalencia antes de sustituir SamplePosition. Render Graph ya no necesita activarse; antes de LOD/culling hay que probar melee fuera de cámara.

Validación posterior: 130/130 EditMode, 31/31 PlayMode, stress de 900 agentes con pares/contactos/huellas conservados y 0 B managed en el tramo medido, perfil pequeño de 300 frames y 68 ensamblados Player compilados. Pipeline recuperado en 7800. El A/B de bodyblock está dentro del ruido; el perfil completo falló esperando contacto y no se usa como evidencia de rendimiento. Detalles y datos brutos en el registro 08.

Investigación posterior: [comparación de acceso Unity CLI entre Luna y Sol](07-unity-cli-access-diagnosis.md). Ambos ejecutan el CLI y reproducen los mismos errores al igualar el contexto: `STATUS_NO_INSTANCES` dentro del sandbox y `STATUS_PIPELINE_LOAD_PENDING` fuera. Las restricciones de interacción con el Editor en la primera ronda fueron instrucciones del orquestador, no una incapacidad demostrada del modelo Luna.
