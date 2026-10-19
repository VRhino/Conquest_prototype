# U6-02 — Bodyblock y puentes híbridos de navegación, pose y animación

Auditoría documental del workspace en Unity `6000.5.8f1`, Entities `6.5.0`, AI Navigation `2.0.14` y Burst `1.8.30` (ProjectVersion y Packages/manifest.json). No se editaron fuentes, assets, escenas ni pruebas. El grafo local informa que parte del resumen fue generado desde `c7bda70c`; el `git rev-parse HEAD` no pudo completarse por un fallo local de Git LFS, así que no se usa el grafo como evidencia de actualidad del checkout.

## Hallazgo principal

La hipótesis del doble grid se confirma en el código actual. `UnitBodyblockSystem` construye el diccionario administrado `_grid` y obtiene listas del pool (`UnitBodyblock.System.cs:47-48, 156-168`), pero después no lee ese diccionario ni esas listas. El job consulta exclusivamente `_nativeGrid`, que se llena en `171-183` y se pasa a `RepulsionJob` en `184-190`. Es trabajo duplicado muerto en el camino caliente. El código actual ya es híbrido: recopila agentes en main thread, calcula contactos con Burst y buffers nativos persistentes y vuelve a main thread para ejecutar `NavMeshAgent.Move` (`95-205`). No es una alternativa entre grid administrado o grid nativo: el nativo ya es el grid de producción.

`Complete()` no se puede quitar conservando el mismo flujo. `Schedule(...).Complete()` ocurre inmediatamente en el mismo `OnUpdate` (`191-193`); cada offset debe estar listo antes de su `agent.Move` (`195-205`), y `NavMeshPositionSyncSystem` lee luego la posición corregida para copiarla a `LocalTransform` (`NavMeshPositionSync.System.cs:29-44`). Sin espera, esos offsets no se pueden leer de forma segura. Sí puede haber una espera diferida en una futura división de fases: programar el job desde una fase que capture el snapshot y completar en una fase posterior que aplique los movimientos, siempre antes de `NavMeshPositionSyncSystem`. Eso sólo gana solapamiento si entre ambos puntos hay trabajo independiente útil y los buffers/snapshot sobreviven hasta el consumidor; hoy no se demuestra esa ventana. `UnitFollowFormationSystem.OnUpdate` llama además `Dependency.Complete()` al entrar (`UnitFollowFormation.System.cs:21-24`), por lo que un job ECS pendiente de antes también podría quedar bloqueado ahí. La revisión debe medir el wait real antes de mover la barrera.

## Hechos, hipótesis y autoridad

| Hecho observado | Evidencia | Implicación |
|---|---|---|
| El bodyblock sólo recoge unidades con NavMesh activo y héroes remotos con NavMesh activo; omite agentes nulos, deshabilitados o fuera del mesh. | `UnitBodyblock.System.cs:95-147` | La repulsión usa la pose del `NavMeshAgent`, no una pose ECS supuesta. El héroe local sigue en el contrato de CharacterController/colliders. |
| La política sigue siendo cross-team; paredes se identifican con formación y estado no Moving; dos unidades Engaging reciben una regla suave propia; sólo Moving recibe la mayoría de pushes. | `UnitBodyblock.System.cs:24-31, 83-123, 244-270` | No simplificar contactos, filtros, desempate de solapamiento ni acumulación sin prueba de equivalencia. |
| El grid nativo almacena un elemento por agente y consulta las nueve celdas vecinas; todos los pares cercanos se conservan. El caso denso continúa cuadrático localmente. | `UnitBodyblock.System.cs:171-183, 230-275`; `SystemResponsibilities.md:14` | No introducir un tope de vecinos como «optimización» sin una decisión de gameplay. |
| El resultado de Burst no modifica Unity objects: el job produce offsets y contadores; sólo el loop administrado llama `agent.Move`. | `UnitBodyblock.System.cs:221-275, 195-205` | Mantiene la frontera ECS/job → puente administrado → física NavMesh. No intentar acceder a `NavMeshAgent` desde Burst. |
| El contrato asigna la publicación de posición NavMesh → ECS a `NavMeshPositionSyncSystem`, pero `EntityVisualSync` todavía llama incondicionalmente a `SetComponentData(LocalTransform)` en la rama NavMesh: la bandera sólo evita volver a asignar Position; Rotation puede cambiar. | `NavAgent.Component.cs:6-12`; `NavMeshPositionSync.System.cs:29-44`; `EntityVisualSync.cs:146-155` | Hay una escritura adicional desde GO que debe auditarse antes de afirmar que existe un único publicador de pose. Preservar la autoridad de rotación y verificar flags de los prefabs antes de proponer su retirada. |
| `UnitRotationResolutionSystem` resuelve la intención y escribe rotación GO después de `UnitFollowFormationSystem`; NavMesh conserva auto-rotación si no hubo intención. | `UnitRotationResolution.System.cs:4-13, 25-41`; `UnitFollowFormation.System.cs:77-87` | No combinar la escritura de rotación con el job de repulsión ni introducir otro writer. |
| `UnitAnimationSystem` se ordena tras NavMesh sync, rotación, formación y spawn. Usa la velocidad/dirección de `NavMeshAgent` activo y `LocalTransform` como respaldo; mantiene `PreviousPosition`. | `UnitAnimation.System.cs:13-18, 34-48, 50-81, 97-113` | El dato animado refleja velocidad física del agente, no la intención de movimiento ni el offset pedido al bodyblock. Conservar esta autoridad. |
| Cada visual de unidad tiene un `UnitAnimationAdapter.Update` con `DefaultExecutionOrder(-80)` que valida vínculo y lee ECS; `UnitAnimatorController.Update` tiene orden `-60` y emite parámetros al Animator. | `UnitAnimationAdapter.cs:12-13, 87-108, 146-160, 169-254`; `UnitAnimatorController.cs:12-16, 144-179` | El adaptador precede al controlador por atributo; no hay evidencia de una inversión entre esos dos MonoBehaviours. Esto no demuestra el orden efectivo de ambos respecto a ECS/NavMesh: comprobar PlayerLoop antes de atribuir una latencia. |
| `EntityVisualSync.Update` opera por GameObject; visual local delega en `LocalHeroCharacterMotor`; para NavMesh evita escribir la posición ECS cuando `syncPositionFromNavMesh` está activo y el remoto delega animación a `RemoteHeroAnimationDriver`. | `EntityVisualSync.cs:106-188, 311-374`; `ModeloHybrido.md`, sección Matriz de autoridad | Unidad visual y héroe remoto tienen caminos diferentes; no generalizar el puente de héroe a todas las unidades. |
| Animators configurados por `VisualSyncUtility` usan `CullCompletely` y root motion desactivado. | `VisualSyncUtility.cs:32-48` | El culling evita evaluación visual fuera de cámara según modo Animator; el controlador aún puede ejecutar su `Update` salvo que el componente/GO se desactive. Medir ambos costes por separado. |

### `Complete()` y ventana de ejecución

La llamada inmediata convierte la parte paralela en un bloque síncrono: el hilo principal espera en `Complete()` si los workers no acabaron. La obligación causal inmediata es que offsets, contadores y buffers estén terminados antes de leerlos y llamar `Move`. Una barrera posterior sólo es válida si el sistema que aplica `Move` completa el handle primero, mantiene intactos los arrays y se ejecuta antes del snapshot NavMesh → ECS. No hay otro consumidor del offset después del loop de aplicación que permita dejar el trabajo pendiente al final del frame.

No se recomienda cambiar el update group a ciegas. La documentación oficial describe `SimulationSystemGroup` al final de la fase `Update` y `PresentationSystemGroup` en `PreLateUpdate`; el PlayerLoop real del proyecto debe capturarse en Unity 6.5, dado que el orden de MonoBehaviour, NavMesh interno, ECS, Animator y render afecta la edad de la pose. El Animator está en modo Normal por defecto/Inspector salvo override y Unity lo actualiza en el loop Update. Registrar marcas de tiempo/eventos por frame en un Player de desarrollo es más fiable que deducir el orden desde `[UpdateAfter]`, que sólo ordena sistemas ECS del grupo. Referencias: [Entities 6.5 — orden de sistemas](https://docs.unity3d.com/Packages/com.unity.entities@6.5/manual/systems-update-order.html), [Unity 6 — orden de ejecución](https://docs.unity.cn/6000.4/Documentation/Manual/execution-order.html), [Unity 6 — AnimatorUpdateMode](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AnimatorUpdateMode.html). Para esta auditoría se usan las reglas que el código y la configuración actuales confirman; no se atribuyen a Unity 6 APIs de Entities 1.3.14 ni se da por existente una función no verificada en Entities 6.5.

## Reutilización revisada

Se buscaron servicios Core e Inventory, utilidades Shared, componentes y sistemas ECS existentes, eventos, referencias de `NavMeshAgent`, bodyblock, animación, sync, jobs y perfiles. Core/Inventory no poseen el ciclo físico de unidades. El elemento reutilizable correcto es la ruta existente, y el trabajo descartable es la cuadrícula administrada sin consumidor.

| Elemento encontrado | Archivo | Relevancia | Reutilización |
|---|---|---|---|
| `UnitBodyblockSystem`, `RepulsionJob`, `RepulsionAgent` y buffers persistentes | `Assets/Scripts/Squads/Systems/UnitBodyblock.System.cs:22-58, 212-297` | Implementación de producción ya tiene filtros, reglas, Burst, capacidad persistente y punto de aplicación física. | Extender/limpiar; no crear otro sistema. |
| `_grid`, `_bucketPool`, `AcquireBucket`, `ToCell` | `UnitBodyblock.System.cs:47-48, 156-168, 314-325` | Código administrado ya no alimenta el algoritmo Burst ni diagnósticos de producción salvo `DebugBucketPoolSize`. | Eliminar en un cambio pequeño junto a la aserción de test obsoleta; conservar la cuadrícula nativa. |
| `NavAgentComponent.syncPositionFromNavMesh` y `NavMeshPositionSyncSystem` | `Assets/Scripts/Squads/NavAgent.Component.cs:1-12`; `.../NavMeshPositionSync.System.cs:18-45` | Contrato explícito de propiedad de la pose NavMesh→ECS. | Reusar; no añadir una segunda sincronización en visual o animación. |
| `UnitAnimationMovementComponent`, `UnitAnimationSystem`, `UnitAnimationAdapter`, `UnitAnimatorController` | `Assets/Scripts/Squads/UnitAnimationMovement.Component.cs`; `.../Systems/UnitAnimation.System.cs`; `.../UnitAnimationAdapter.cs`; `.../UnitAnimatorController.cs` | Camino existente para transformar estado/velocidad física en parámetros del Animator. | Reusar; preservar velocidad real y consumer único por Animator. |
| `EntityVisualSync` / `VisualSyncUtility` | `Assets/Scripts/Visual/EntityVisualSync.cs`; `Assets/Scripts/Shared/VisualSyncUtility.cs` | Registro y puente de visuales, configuración común; no es el dueño de la pose NavMesh de unidades activas. | Reusar sólo donde aplica el contrato visual. |
| `SquadSpawnConfigComponent` / `Authoring` | `Assets/Scripts/Squads/SquadSpawnConfig.Component.cs:13-21`; `...Authoring.cs:15-23, 71-79` | Radio, fuerzas, clamps, umbral y batch ya son configurables. | Reusar; no introducir literales de tuning. |
| `VisualSyncUtility` y `FormationPositionCalculator` | `Assets/Scripts/Shared/VisualSyncUtility.cs`; `Assets/Scripts/Squads/FormationPositionCalculator.cs` | Servicios compartidos de visual/posiciones; rutas confirmadas por el orquestador. | Sin papel en la generación del grid de contactos; no usarlos para duplicar una cache. |
| Servicios Core e Inventory | `Assets/Scripts/Core/`, `Assets/Scripts/Inventory/Services/` | Búsqueda de reuse-check; no hay dueño de NavMesh, transformación física o Animator de unidad. | No aplican al cambio. |
| Sistemas de movimiento y eventos | `Assets/Scripts/Squads/Systems/*.System.cs`, `Assets/Scripts/Events/` | No se encontró otro publicador de offset bodyblock que resuelva este mismo contrato. | No crear nuevo evento/componente para la limpieza sugerida. |

## Datos de pruebas y benchmarks existentes

- `Docs/Arquitectura/11_Escala_Bodyblock_900_Agentes_2026-09-14.md:18-42` registra mediciones históricas de una muestra aislada: 900 agentes, 0 B administrados tras calentamiento; disperso 0,86–0,88 ms y caso denso 16,72–16,93 ms en la versión y código de esa fase. Ese documento precede a la adición del job Burst y no debe presentarse como el rendimiento de hoy.
- `Docs/Arquitectura/19_Revision_Sistemas_Optimizacion_2026-10-13.md:75-94` registra después, con job Burst, mediciones de una muestra: 900 densos 14,90 ms secuencial / 4,18 ms paralelo (batch 32), disperso paralelo 1,44 ms, y 0 B administrados medidos en hilo principal. Perfil integrado Editor: bodyblock p95 0,3331 ms en 20 héroes/240 unidades iniciales (228 al final), 600 frames con contacto; otro perfil de batalla 20 héroes/240 unidades reporta p95 0,3244 ms. Son datos de distintas muestras, Editor/test runner, sin baseline comparable ni Player/GPU.
- `Logs/system-optimization-bodyblock-timing.txt:1-2` repite 1,44 ms disperso y 4,18 ms denso con 0 B en la muestra aislada. `Logs/system-optimization-full-battle-profile.json:1-12` registra versión Editor `6000.5.8f1`, 600 frames, 20 héroes, 228 unidades, bodyblock p50 0,3061 ms / p95 0,3331 ms, sin baseline.
- `Assets/Tests/PlayMode/SquadNavigationRegressionTests.cs:202-241` verifica que dos agentes enemigos exactamente solapados se separan realmente tras el `Update` del sistema. `244-318` mide 900 agentes dispersos y densos y GC después de calentamiento, pero el test tiene nombre y aserción de pool administrado (`282-293`). Ese contrato de test quedará obsoleto al retirar `_bucketPool`; adaptar el test a la nueva intención y conservar reporte de timing/allocation.
- `Assets/Tests/EditMode/BodyblockRegressionTests.cs:8-27` comprueba clamp equivalente en 30/60/144 FPS y dirección finita/determinista en solapamiento exacto.
- `BattleSceneIntegrationTests.cs:594-598, 608-670` ya contiene perfil renderizado explícito con p50/p95/p99/max, cámara verificada, contadores de pares, Main Thread, `Conquest.Bodyblock`, GC y detección. Úsese para validación contextual; no mezclar tiempos de este perfil con la prueba aislada de bodyblock.
- El informe de optimización resume pruebas de pared, bloqueo, animación física y otras regresiones en `Docs/Arquitectura/19_Revision_Sistemas_Optimizacion_2026-10-13.md:61-73`, pero no constituye una nueva medición en esta auditoría.

No se ejecutaron tests ni se tomaron mediciones nuevas en esta tarea. No se promete ganancia de FPS; el ahorro esperado por quitar el grid muerto queda por medir y puede ser pequeño frente a la construcción de `_nativeGrid`, NavMesh, Animator y render.

## Cambios mínimos propuestos

### Prioridad 1 — limpieza de coste muerto, bajo riesgo

Eliminar `_grid`, `_bucketPool`, `_usedBucketCount`, `ToCell` y `AcquireBucket`, y el bloque que limpia/llena el grid administrado (`UnitBodyblock.System.cs:47-48, 53, 156-168, 314-325`). Conservar `_nativeGrid` sin cambios. Retirar o sustituir `DebugBucketPoolSize`; actualizar `NineHundredBodyblockAgentsReuseSpatialBucketsAfterWarmup` para que mida la propiedad que se quiere sostener, por ejemplo 0 B tras warmup y contadores de candidato/contacto válidos, sin acoplarse al pool ya borrado. Este cambio elimina duplicación demostrable, pero el impacto temporal requiere medición before/after.

Pseudodiff:

```diff
- private readonly Dictionary<(int, int), List<int>> _grid = new();
- private readonly List<List<int>> _bucketPool = new();
- private int _usedBucketCount;
- public int DebugBucketPoolSize => _bucketPool.Count;
  private NativeParallelMultiHashMap<int2, int> _nativeGrid;
...
- _grid.Clear();
- _usedBucketCount = 0;
- for (int i = 0; i < count; i++) { /* llenar diccionario administrado */ }
  EnsureNativeCapacity(count);
  _nativeGrid.Clear();
  // llenar y consultar exclusivamente el grid nativo como hoy
```

### Prioridad 2 — determinar si hace falta compartir snapshot

Hoy Bodyblock lee `agent.transform.position` antes de `Move`; NavMeshPositionSync vuelve a leer después de `Move`. Son lecturas similares con momentos semánticos diferentes. La segunda no es redundante porque publica la pose corregida. UnitAnimationSystem lee `LocalTransform` para actualizar `PreviousPosition`, pero cuando NavMesh está activo sustituye velocidad/dirección calculadas por `agent.velocity`; no reutilizar el snapshot pre-Move como velocidad o pose final.

Antes de crear un buffer/snapshot compartido, instrumentar número de accesos y duración en una batalla real. Si el acceso managed repetido es material, extender el puente existente manteniendo fases explícitas: capturar estado NavMesh, resolver bodyblock, aplicar Move y después publicar pose corregida a ECS; un snapshot anterior no puede alimentar al consumidor posterior con un valor viejo. La interfaz de `UnitAnimationMovementComponent` ya es la salida adecuada al Animator y no requiere una nueva clase sólo para eliminar una lectura.

### Prioridad 3 — retrasar la espera sólo con ventana probada

Una variante con fase de cálculo y fase de aplicación puede programar el job y aplazar `Complete()` hasta el consumer de offsets, antes de `NavMeshPositionSyncSystem`. Necesita demostrar en el PlayerLoop/System window una banda de trabajo independiente entre esas fases, conservar nativos/handle, disponer la ruta de cleanup si cambia el World/entidades y no alterar el frame de pose que leen ataques/animación. Si el wait medido en `Complete` es menor que la sobrecarga de fase/sync, mantener el diseño de un solo `OnUpdate`.

## Plan de equivalencia y medición para implementación

1. Primero cambiar sólo la cuadrícula administrada sin alterar la política o el orden. Ejecutar la suite bodyblock/navegación existente y comparar por fixture: posiciones finales/offsets, `DebugCandidatePairs`, `DebugContactPairs`, asignación tras calentamiento. La eliminación del grid administrado debe conservar exactamente la población del grid nativo.
2. Verificar PlayMode en NavMesh real: solapamiento exacto, agente frente a pared Line/Testudo, formación abierta Dispersed, dos Moving, ambos Formed, par Engaging, héroe remoto y límite físico del héroe local con collider. Comprueba que `agent.Move` permanece antes del sync; la prueba de dos unidades ya existente sólo cubre el solapamiento.
3. En escena renderizada, comprobar ciclo completo: `NavMeshAgent` se desplaza o recibe corrección → `LocalTransform` coincide tras sync → UnitAnimationMovement refleja velocidad física/idle correcta → Animator reproduce locomoción física y deja de correr cuando el agente se bloquea por pared. Incluir giro Formed/Combat y ataque para detectar cambios de rotación o desfase de consumo.
4. Medir Player Development standalone con la misma máquina, escena, resolución, vSync, carga y semillas; calentar el sistema; ejecutar varias capturas de miles de frames y reportar p50/p95/p99/max, GC, agentes, celdas y densidad de contactos. Separar `Conquest.Bodyblock`, tiempo de worker/wait, Unity NavMesh/AI, `UnitAnimationSystem`, scripts adapter/controller, Animator, render/GPU y Main Thread. Repetir Editor como diagnóstico, nunca compararlo directamente con Player.
5. Para decidir sobre `Complete()`, añadir marcas por etapa (`collect`, construir grid, job scheduled, espera, aplicar Move, NavMeshPositionSync, UnitAnimation, adapters/Animator) y registrar el PlayerLoop/Sistemas efectivo. Comparar mínimo el caso por debajo/encima de `bodyblockParallelThreshold`, escena 20v20/240 unidades y carga densa 900. Informar resultados por muestra y dispersión, sin inferir FPS global de un marker aislado.

## Riesgos y límites

- El test de escala hoy aserta el pool que se propone borrar. No eliminar la instrumentación hasta acordar una métrica reemplazo; el cambio queda en código más test, fuera de este informe.
- Los buffers persistentes se redimensionan juntos y se liberan en `OnDestroy` (`278-297`). Si una refactorización difiere la terminación del job, debe garantizar que resize/dispose no se cruce con un handle activo.
- El cálculo usa todos los contactos de las nueve celdas, pero en una celda densa el volumen de contactos sigue creciendo cuadráticamente. Eliminar el grid administrado no resuelve ese extremo.
- `UnitAnimationAdapter.Update` hace varias llamadas `EntityManager.HasComponent/GetComponentData` por visual/unidad; los parámetros del Animator se emiten cada Update aunque no cambien (`UnitAnimationAdapter.cs:146-254`, `UnitAnimatorController.cs:156-190`). Es una hipótesis de coste por unidad, no evidencia de que hoy sea cuello de botella. Los cambios de caché/cadencia pueden introducir retraso visible o perder estado de disparo/ataque; perfilar y validar consumo de eventos antes.
- La autoridad/orden cambia entre componentes managed y ECS. `UpdateAfter` de ECS no ordena un MonoBehaviour; verificar PlayerLoop real antes de optimizar latencia o compartir una pose.
- El orquestador verificó la descripción del orden también en `Library/PackageCache/com.unity.entities@ef040875dea9/Documentation~/systems-update-order.md:75-76`. La comprobación final del PlayerLoop efectivo debe hacerse en el Editor 6000.5.8f1 instalado. No inferir que Entities 1.3.14 y 6.5.0 ofrecen el mismo API o comportamiento.

## Orden recomendado de trabajo

1. Limpiar el diccionario administrado muerto y adaptar el test de escala a métricas que siguen vigentes.
2. Capturar baseline comparables en Player con la escena real y separar main thread, workers, NavMesh, animación y render.
3. Sólo si el coste lo justifica, diseñar snapshot explícito o dividir schedule/apply alrededor de `NavMeshPositionSyncSystem`, con equivalencia física y de animación.
