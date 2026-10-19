# U6-04 — Consultas NavMesh desde jobs en Unity 6.5

Revisión estática de Unity 6000.5.8f1, Entities 6.5.0 y com.unity.ai.navigation 2.0.14. No se modificó código, escenas, assets, tests ni Editor. Fuentes: Packages/manifest.json, Packages/packages-lock.json, ProjectSettings/ProjectVersion.txt, `C:/Program Files/Unity/Hub/Editor/6000.5.8f1/Editor/Data/Managed/UnityEngine/UnityEngine.AIModule.xml` y documentación oficial de Unity 6000.5. El orquestador corroboró los tipos LowLevel en el XML instalado y contrastó NavWorld/MapLocation/AddDependency con la referencia oficial.

## Conclusión

Unity.AI.Navigation.LowLevel existe en 6000.5.8 y sus operaciones de consulta pueden ejecutarse desde IJob/IJobFor. NavWorld no mueve ni actualiza NavMeshAgent: no reemplaza SetDestination, avoidance, velocidad, rotación, Warp, isStopped ni la sincronización física/GameObject. Se justifica un spike de proyección por lote, pero no hay evidencia de que esa fase sea hoy el costo dominante.

La propuesta previa requiere esta precisión: “paralelizar consultas de navegación” describe consultas low-level, no llamadas a NavMeshAgent ni su movimiento. La documentación confirma jobs, pero no garantiza que las consultas sean Burst-compatible; el spike debe comprobarlo si se desea Burst. MapLocation no reproduce exactamente SamplePosition, así que no recomiendo sustituir el helper actual hasta medir diferencias y rendimiento.

## API verificada

| API | Confirmación | Límites |
|---|---|---|
| NavWorld, NavLocation, NavNode, NavQueryBuffer, NavQueryStatus | Tipos presentes en UnityEngine.AIModule.xml local:1918-1946. Docs 6000.5 dicen que NavWorld agrupa surfaces/links y sus operaciones se ejecutan en IJob/IJobFor ([NavWorld](https://docs.unity.com/en-us/engine/6000.5/script-reference/unity/ai/navigation/lowlevel/navworld)). | Consultas de navegación; no exponen un agente ni su movimiento. Los docs consultados no garantizan Burst. |
| NavWorld.MapLocation(position, extents, agentTypeId, areaMask) | Retorna NavLocation (posición y NavNode) o inválido; filtra agent type y áreas; extents expande búsqueda en los tres ejes ([MapLocation](https://docs.unity.com/en-us/engine/6000.5/script-reference/unity/ai/navigation/lowlevel/navworld/maplocation)). | No equivale por contrato a SamplePosition(maxDistance). La posición puede no alinearse al HeightMesh y no busca NavMesh Links. |
| BeginFindPath → ContinueFindPath → EndFindPath → GetResultFromFindPath | Consulta un corredor de nodos con estado en NavQueryBuffer; ejemplo oficial ejecuta búsqueda en job ([NavQueryBuffer](https://docs.unity.com/en-us/engine/6000.5/script-reference/unity/ai/navigation/lowlevel/navquerybuffer), [BeginFindPath](https://docs.unity.com/ko-kr/engine/6000.5/script-reference/unity/ai/navigation/lowlevel/navworld/beginfindpath)). | El corredor no es un NavMeshPath de corners ni una orden de movimiento. Endpoint puede ajustarse a una ubicación cercana. Copiar NavQueryBuffer crea alias, no memoria independiente; no compartir buffer entre búsquedas concurrentes. |
| NavWorld.Raycast | Ray sobre NavMesh, usable desde jobs paralelos, similar en implementación a NavMesh.Raycast ([Raycast](https://docs.unity.com/en-us/engine/6000.5/script-reference/unity/ai/navigation/lowlevel/navworld/raycast)). | Comprueba línea recta, no ruta alternativa; no sustituye CalculatePath. No está en rutas hot encontradas. |
| GetDefaultWorld, AddDependency, Dispose | Hay un único NavWorld. Cada GetDefaultWorld() crea un handle que hay que liberar; conservarlo mientras se usa. AddDependency(JobHandle), desde main thread, impide cambios NavMesh hasta completar el job ([GetDefaultWorld](https://docs.unity.com/en-us/engine/6000.5/script-reference/unity/ai/navigation/lowlevel/navworld/getdefaultworld), [AddDependency](https://docs.unity.com/en-us/engine/6000.5/script-reference/unity/ai/navigation/lowlevel/navworld/adddependency)). | Un NavMesh update/carving no debe correr durante la consulta. Sin dependencia, Editor informa y fuerza espera antes de cambios. NavLocation puede invalidarse por cambios de NavMesh/carving; NavWorld pierde validez tras RemoveAllNavMeshData. |

La API está en UnityEngine.AIModule, no en las fuentes del paquete com.unity.ai.navigation@2.0.14. La instalación local confirma los tipos; las páginas oficiales 6000.5 confirman firmas y semántica. La nota de Unity 6.5 confirma el namespace y su relación con el API anterior ([Navigation and pathfinding](https://docs.unity3d.com/6000.5/Documentation/Manual/WhatsNewUnity65.html#navigation-and-pathfinding)).

### API anterior

UnityEngine.Experimental.AI.NavMeshQuery sigue listado en UnityEngine.AIModule.xml:1544-1557,1679-1690, pero la referencia 6000.5 lo marca deprecated y recomienda NavWorld ([referencia MapLocation antigua](https://docs.unity.com/en-us/engine/6000.5/script-reference/unityengine/experimental/ai/navmeshquery/maplocation)). El API anterior usa NavMeshWorld, NavMeshLocation, PolygonId, UpdateFindPath y GetPathResult; el actual usa NavWorld, NavLocation, NavNode, ContinueFindPath y NavQueryBuffer. Son namespaces y tipos distintos, no un wrapper de NavMeshAgent. Para trabajo nuevo, evaluar Unity.AI.Navigation.LowLevel.

## Llamadas actuales y cache

| Ruta | Evidencia y comportamiento |
|---|---|
| Unidad, slot de formación | UnitNavMeshSystem.ResolveFormationDestination llama TrySampleDestination (Assets/Scripts/Squads/Systems/UnitNavMesh.System.cs:259-285,315-331). Conserva effectiveFormationDestination si el slot cambia menos que threshold (:269-282). Al fallar usa posición actual y espera retry interval, o reintenta antes si el slot cambia más de retryDistance (:264-267,303-313). Detecta path incompleto/no existente (:288-301). |
| Unidad, stop point de combate | Mismo helper si cambia el stop-point (:136-170). Conserva último comando dentro de threshold y cae al destino de formación si no proyecta (:220-238). UnitNavMeshSystem es owner exclusivo de SetDestination y rotación (SystemResponsibilities.md:103-113). |
| Héroe remoto, orden IA | HeroAIExecutionSystem usa el helper al fallar cache (:82-106). Reutiliza proyección si destino no cambia, comando no falla y no vence nextProjectionRefreshTime; refresca por default cada retryInterval de 1 s (:91-106,44-51). Conserva/reintenta fallos y mantiene path válido mediante pathPending/hasPath/pathStatus (:86-90,108-149,166-174,219-235). |
| Spawn héroe remoto | Primero Warp a la posición pedida y, si falla, proyecta y repite Warp (HeroVisualInstantiation.System.cs:124-150); se ejecuta una vez por spawn. Usa el mismo sample radius (SystemResponsibilities.md:46-51). |
| Movimiento/path del Agent | SetDestination, pathPending, pathStatus, hasPath, ResetPath y desiredVelocity; no hay NavMesh.CalculatePath explícito en Assets/Scripts. El Agent mantiene su locomoción/avoidance; NavMeshPositionSync publica pose después (HeroAIExecution.System.cs:134-158; UnitNavMesh.System.cs:232-253; NavMeshPositionSync.System.cs:13-44). |

Hay una llamada directa a NavMesh.SamplePosition en Assets/Scripts: TrySampleDestination (:323); slots, stop-points e IA remota reutilizan ese wrapper, y spawn remoto llama al wrapper. No encontré NavMesh.CalculatePath ni NavMesh.Raycast en Assets/Scripts. Las coincidencias de Physics.Raycast o WorldMapLocalNavigation.Raycast son física y proyección propia de mapa.

Los valores vienen de SquadSpawnConfig: sample radius default 2 m, destination change threshold 0.25 m, failure retry distance 1 m e interval 1 s (SquadSpawnConfig.Authoring.cs:24-27; UnitNavMesh.System.cs:52-57; HeroAIExecution.System.cs:46-51). El filtro actual usa agent.agentTypeID y agent.areaMask (UnitNavMesh.System.cs:318-324). MapLocation acepta agentTypeId/areaMask, así que ambos pueden conservarse.

## Equivalencia y uso viable

SamplePosition define maxDistance y devuelve el punto nearest dentro de esa distancia. La documentación describe primero una proyección vertical por instancia y luego búsqueda alrededor; no considera obstrucciones ([SamplePosition 6000.5](https://docs.unity.com/en-us/engine/6000.5/script-reference/unityengine/ai/navmesh/sampleposition)). MapLocation busca dentro de caja definida por extents, prioriza surfaces cercanas arriba/abajo, luego laterales, omite Links y no alinea el resultado al HeightMesh ([MapLocation 6000.5](https://docs.unity.com/en-us/engine/6000.5/script-reference/unity/ai/navigation/lowlevel/navworld/maplocation)). Un AABB de extents radius incluye esquinas más lejanas que ese radio; un cubo inscrito puede excluir puntos que la muestra actual encontraría. Filtrar después no garantiza recuperar el mismo nearest. No puedo verificar equivalencia exacta a partir del contrato público.

TrySampleDestination construye NavMeshQueryFilter y retorna hit.position (UnitNavMesh.System.cs:318-327). LowLevel MapLocation puede tomar el mismo tipo de agente y areaMask, pero retorna NavLocation y difiere en límites de búsqueda/superficie. Comparar especialmente bordes, rampas, varios pisos, islas, áreas y agent types antes de aceptar diferencias.

NavMeshAgent.SetDestination solicita ruta al motor del agente; movimiento y pose salen del Agent/bridge. NavWorld.BeginFindPath calcula corredor desde dos NavLocation, con máscara/costos y endpoint potencialmente aproximado. Es útil para factibilidad/diagnóstico, no para garantizar que el Agent produzca una ruta idéntica ni para reemplazar avoidance. No sumar una segunda búsqueda por unidad al camino de SetDestination sin mostrar que baja el costo total.

## Reutilización revisada

| Elemento | Ruta | Recomendación |
|---|---|---|
| UnitNavMeshSystem.TrySampleDestination | Assets/Scripts/Squads/Systems/UnitNavMesh.System.cs:315-331 | Reusar como baseline; no duplicar wrapper. Aislar el spike y mantener helper actual hasta comparar. |
| NavAgentComponent | Assets/Scripts/Squads/NavAgent.Component.cs:14-33 | Reusar caches de slots, comandos, proyecciones y retry. No añadir otra cadencia ni guardar NavLocation sin invalidación por cambios del NavMesh. |
| SquadSpawnConfigComponent/Authoring | Assets/Scripts/Squads/SquadSpawnConfig.Component.cs:22-25; SquadSpawnConfig.Authoring.cs:24-27,80-83 | Reusar valores configurados. Documentar que extents no son sample radius si se prueba MapLocation. |
| HeroAIExecutionSystem | Assets/Scripts/Hero/AI/Systems/HeroAIExecution.System.cs:38-53,91-149 | Reusar caché, retry y path status; no duplicar gestión de path. |
| UnitBodyblockSystem | Assets/Scripts/Squads/Systems/UnitBodyblock.System.cs:184-205,221-289 | Reusar patrón arrays/job y aplicación managed de NavMeshAgent.Move en main thread; no usar el Agent desde job. |
| Tests de navegación | Assets/Tests/PlayMode/SquadNavigationRegressionTests.cs:71-112,134-196 | Si se implementa, extender casos de fallback y reintento con comparaciones de superficie/altura. No fueron modificados. |
| NavMeshPositionSyncSystem | Assets/Scripts/Squads/Systems/NavMeshPositionSync.System.cs:13-44 | Mantener el Agent como autoridad de pose; consultas no producen locomoción. |
| Core, Shared, Inventory Services, Events | Assets/Scripts/Core/, Assets/Scripts/Shared/, Assets/Scripts/Inventory/Services/, Assets/Scripts/Events/ | Revisión reuse-check: no hay wrapper LowLevel existente. No crear servicio/utilidad nuevo ahora. |

La arquitectura da a UnitNavMeshSystem ownership exclusivo de SetDestination/rotación; HeroVisualInstantiationSystem es dueño del spawn/Warp remoto; NavMeshPositionSyncSystem publica pose del Agent a ECS. Mantener separación ECS↔bridge↔GameObject (SystemResponsibilities.md:46-51,72-75,103-113; Docs/ModeloHybrido.md).

## Spike mínimo y A/B

### U6-04a: capacidad y semántica

1. Preparar un benchmark aislado con arrays nativos de posiciones, agentTypeID, areaMask y resultados del helper actual como baseline; representar slots estables y cambiantes del perfil real.
2. Usar NavWorld persistente y un IJobFor que llame MapLocation por índice y escriba en índice propio. Registrar el JobHandle mediante AddDependency desde main thread. Mantener vivos NavWorld/arrays hasta completar; completar antes de aplicar resultado a Agent o liberar memoria. Liberar todo en teardown; reacquirir world si RemoveAllNavMeshData lo invalidó.
3. Probar primero sin Burst y luego intentar BurstCompile para confirmar soporte en esta build. No prometer compatibilidad hasta que compile y corra en Player. Medir costo worker, schedule/Complete en main, allocations y tiempo total de frame.
4. Comparar punto, agent type, nodo/surface y validez frente a SamplePosition: plano, pendientes, bordes, agujeros, islas, áreas distintas, dos agent types y posiciones encima/debajo de superficies. Registrar discrepancias antes de cualquier sustitución.

No llamar GetDefaultWorld por agente/frame: cada llamada crea un handle a liberar. Mantenerlo mientras se usan queries. AddDependency debe registrar cada job mientras puede haber NavMesh updates/carving. Si el mismo sistema completa de inmediato, scheduling/wait puede cancelar el beneficio.

### U6-04b: piloto acotado

Solo si el perfil lo justifica y las diferencias se aceptan, probar los slots de formación en UnitNavMeshSystem que fallan la cache al cambiar/reintentar. Mantener managed el combate, selección de destino, SetDestination, path status, Waiting, fallbacks y NavAgentComponent. Completar dentro del mismo frame antes de SetDestination para no introducir latencia; medir Schedule + Complete en el mismo costo. No preconsultar paths LowLevel junto al Agent sin hipótesis separada.

Aceptación funcional: conserva filtro de agent type/áreas; éxito/fallo mantiene fallback coherente; no mueve agentes a otra planta/isla; llegada y SetDestination consumen el resultado aprobado; retry sucede por el timer/cambio configurado; no se lee/escribe datos mientras otro job/sistema los modifica.

Aceptación de rendimiento: misma build Player, escenario, agentes, solicitudes/cache misses; repetir frames calientes y ráfaga de cambios. Comparar p50/p95 de CPU total, main thread incluyendo Complete, query, GC y jobs. Aceptar solo una mejora repetible sin regresión material en peor caso; Editor y Player se reportan separado. Sin métricas actuales no hay afirmación de FPS.

## Tareas y riesgos

| Prioridad | Trabajo/archivos candidatos | Aceptación | Riesgo |
|---|---|---|---|
| 1 | Benchmark/profiling aislado; candidato en UnityEngine.AIModule | Confirmar volumen/cache misses; IJobFor corre; varios tipos y máscaras; Burst medido aparte | Scheduling puede dominar bajo volumen; distinguir query, wait y frame. |
| 2 | Spike de equivalencia, con baselines SquadNavigationRegressionTests.cs | Comparar punto/validez para borde, altura, agujero, áreas, tipos, NavMesh dinámica | Extents vs radio, HeightMesh, multilevel y cambio de isla. |
| 3 | Piloto solo UnitNavMeshSystem | Mismo frame, fallback/retry/cache y SetDestination preservados; A/B Player | Complete síncrono quita paralelismo; AddDependency bloquea modificaciones; resultado puede diferir. |
| 4 | Path queries solo si se mide costo de cálculo externo | Comparar consulta de factibilidad y pathStatus en casos completos/incompletos | Duplica búsqueda del Agent; NavQueryBuffer persistent y limitado; corredor no es path/corners del Agent. |

No recomiendo reescribir HeroAIExecutionSystem ni la locomoción de unidades como jobs: usan NavMeshAgent gestionado para destinos, rotación, stop/reset, velocidad y desiredVelocity. UnitNavMeshSystem es el límite correcto para mantener estos efectos en main thread y delegar solo proyección si el perfil la justifica.

## Referencias

- Código: UnitNavMesh.System.cs:31-90,136-170,215-331; HeroAIExecution.System.cs:25-53,82-187,209-235; HeroVisualInstantiation.System.cs:124-150; NavAgent.Component.cs:14-33; SquadSpawnConfig.Component.cs:22-25; SquadSpawnConfig.Authoring.cs:24-27,80-83; NavMeshPositionSync.System.cs:13-44.
- Contratos: Assets/Scripts/Squads/SystemResponsibilities.md:46-51,72-75,103-113; Docs/ModeloHybrido.md.
- Tests existentes: Assets/Tests/PlayMode/SquadNavigationRegressionTests.cs:71-112,134-196.
- API oficial: [NavWorld 6000.5](https://docs.unity.com/en-us/engine/6000.5/script-reference/unity/ai/navigation/lowlevel/navworld), [MapLocation](https://docs.unity.com/en-us/engine/6000.5/script-reference/unity/ai/navigation/lowlevel/navworld/maplocation), [SamplePosition](https://docs.unity.com/en-us/engine/6000.5/script-reference/unityengine/ai/navmesh/sampleposition), [AddDependency](https://docs.unity.com/en-us/engine/6000.5/script-reference/unity/ai/navigation/lowlevel/navworld/adddependency), [NavQueryBuffer](https://docs.unity.com/en-us/engine/6000.5/script-reference/unity/ai/navigation/lowlevel/navquerybuffer), [API anterior obsoleta](https://docs.unity.com/en-us/engine/6000.5/script-reference/unityengine/experimental/ai/navmeshquery/maplocation), [Unity 6.5 Navigation and pathfinding](https://docs.unity3d.com/6000.5/Documentation/Manual/WhatsNewUnity65.html#navigation-and-pathfinding).
