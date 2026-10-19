# U6-01 — Detección, targeting y cálculos ECS

Revisión estática para Unity `6000.5.8f1` / Entities `6.5.0`. No se modificó código, escenas, assets ni tests. El informe está basado en el checkout de `HEAD c7bda70c36e37a382d841587f577cc2507d0f0c3`, con cambios locales preexistentes. El grafo coincide con HEAD, pero eso no certifica que refleje los cambios sin commit. Manifest y versión de Editor: `Packages/manifest.json`, `ProjectSettings/ProjectVersion.txt`.

## Hallazgos priorizados

1. **El costo dominante potencial sigue siendo el trabajo por pares.** `EnemyDetectionSystem` recorre las unidades de los squads rivales para cada squad observador; `UnitTargetingSystem` compara cada unidad sin target válido con todos los candidatos compartidos. La detección puede beneficiarse primero de un índice espacial; targeting ya está razonablemente optimizado para allocations, pero conserva O(unidades × candidatos) en reacquisición y redistribución.
2. **Hay optimizaciones ya implementadas que no deben duplicarse.** El buffer de candidatos es compartido por squad, no por unidad; targeting reutiliza `HashSet`/`Dictionary`; bodyblock ya demuestra el patrón de grid, scratch nativo persistente y Burst job con aplicación física managed separada.
3. **La propuesta de índice + jobs es viable, pero no debe comenzar paralelizando escrituras ECS.** Construir/consultar el índice para reducir candidatos es la primera hipótesis a medir. Después, un Burst job puede acelerar cálculos puros. Las escrituras a `UnitCombatComponent` y buffers requieren ownership sin alias y dependencias explícitas.
4. **No se debe introducir un cap de candidatos.** El cap existente es un reparto suave de melee por target y una dispersión blanda para ranged; candidatos nuevos/alternativos y héroes detectables forman parte del comportamiento. Truncar el buffer cambiaría gameplay.
5. **No se puede afirmar una ganancia Unity 6 con las medidas disponibles.** La referencia aislada de 900 unidades procede de la revisión del 15-sep; el perfil reciente de batalla mide detección, pero no aísla targeting y carece de baseline comparable.

## Evidencia y complejidad por fase

### EnemyDetectionSystem

En `Assets/Scripts/Combat/EnemyDetection.System.cs:35-62` se limpia una vez cada buffer por squad elegible, se promedia el centroide de unidades vivas y se calcula el radio al cuadrado. Entre `:64-96`, para cada squad observador, se vuelve a recorrer la query de todos los squads y sus unidades; se filtran equipo, entidad existente, transform y muerte y se prueba distancia centroide-unidad. Un squad enemigo se agrega a `DetectedEnemy` en su primera coincidencia y cada unidad dentro del rango se agrega a `SquadTargetEntity`. En `:98-118` se hace otra pasada por héroes/entidades `DetectableEntityTag`, respetando equipo, `IsDeadComponent` y `HeroLifeComponent`.

Sea S el número de squads observadores con buffers, U el total de unidades en squads candidatos, H el número de entidades detectables no-squad, y Uₐ el tamaño de cada squad observador. La suma de centroides es O(ΣUₐ); la búsqueda de unidades es O(S×U) en el peor caso (con filtrado de equipos dentro del loop); la pasada de héroes es O(S×H). Las distancias exactas son O(1) por pareja examinada. Publicar los resultados cuesta O(C), C = entradas candidatas emitidas. No hay sorting ni límite de buffer. Esto es peor que una etiqueta vaga «cuadrática» para pocos squads y grandes tamaños; la expresión precisa depende de distribución y teams.

**Semántica a preservar:** la detección usa distancia 3D del centroide a cada entidad, sin AABB, oclusión ni physics query (`:11-12`, `:84-87`, `:111-112`). Conserva el orden producido por el buffer de unidades del squad y la query de héroes. `DetectedEnemy` alimenta además visibilidad/percepción táctica de héroes, así que cambiar detección afecta más consumidores que targeting (`BattleWorldState.System.cs:12,53,73`; `Docs/Arquitectura/20_Contraste_Handoff_IA_Tactica_2026-10-14.md:52`).

### UnitTargetingSystem

El sistema lee `maxUnitsPerTarget` desde `SquadSpawnConfigComponent`, con fallback actual a 2 (`Assets/Scripts/Combat/UnitTargeting.System.cs:22-24`). Procesa squads con `SquadAIComponent`; permite asignación solo en `InCombat` y mientras el match está en batalla (`:26-42`). Fuera de combate o sin candidatos limpia target e `IsEngagingTag` (`:41-53`). En la ruta de combate reconstruye el conjunto de candidatos válidos comprobando existencia, transform, muerte y vida del héroe (`:56-68`).

Fases medidas conceptualmente:

| Fase | Trabajo | Complejidad peor caso por squad |
|---|---|---|
| Validar candidatos | Deduplicar/filtrar C candidatos | O(C) esperado; HashSet administrado reutilizado |
| Retener o adquirir target | N unidades; por cada unidad cuyo target ya no es válido, escanea C candidatos y calcula distancias | O(N×C) |
| Contabilizar melee/ranged | Añade/recuenta targets ya asignados | O(N) esperado; diccionarios administrados reutilizados |
| Redistribuir melee saturado | Para cada unidad melee fuera del rango de ataque que excede el cap, escanea candidatos hasta hallar uno con espacio | O(N×C) |
| Dispersar ranged | Si hay alternativa y supera soft cap + slack, escanea C para escoger un target menos ocupado | O(Nᵣ×C) |
| Sincronizar engagement | Actualiza tag por unidad | O(N) |

Por tanto, el peor caso conserva O(C + N×C), más costos de queries/acceso indirecto. La ruta común puede ser O(N) si los targets actuales siguen válidos; esto importa porque la primera pasada no busca un target más cercano cuando el target actual continúa siendo candidato (`:84-105`).

**Selección, desempates y cap:** el objetivo más cercano se cambia solo cuando `dist < bestDist`, por lo que un empate queda con el primer candidato en el buffer (`:88-105`). HashSet solo valida/filtra; no selecciona. La redistribución melee escoge el primer candidato con hueco en orden de buffer, no el más cercano (`:147-167`), y solo retargetea unidades fuera del `attackRange` (`:135-145`). Si no hay hueco no fuerza el cap, por eso es límite blando. Ranged tiene recuento independiente y un soft cap calculado como `ceil(rangedUnits / availableTargets)` con 1 de slack; solo cambia si mejora el reparto en al menos dos unidades (`:169-229`). Mantener el orden de candidatos es necesario para conservar empates y orden de redistribución; iterar un hash map/set para escoger target no lo preserva.

## Orden del frame, muerte y dependencias híbridas

El orden explícito actual es: daño antes de squad AI; `UnitDeathSystem` después de daño y antes de detección/targeting; `EnemyDetectionSystem` después de respawn y antes de `SquadAISystem`; snapshot `BattleWorldStateSystem` después de detección y antes de AI; targeting después de AI, FSM y muerte; `UnitAttackSystem` y `RangedAttackSystem` después de targeting. Fuentes: `UnitDeath.System.cs:11-15`, `EnemyDetection.System.cs:14-17`, `UnitTargeting.System.cs:10-14`, `BattleWorldState.System.cs:18-27`, `UnitAttack.System.cs:20-22`, `Systems/RangedAttack.System.cs:15` y `Assets/Scripts/Squads/SystemResponsibilities.md`.

`UnitDeathSystem` retira de `SquadUnitElement` y destruye unidad antes de que detección/targeting se ejecuten (`UnitDeath.System.cs:45-67`). El consumidor debe seguir filtrando `IsDeadComponent`/héroes sin vida porque entidades muertas pueden seguir visibles durante el frame o aparecer en candidates externos (`EnemyDetection.System.cs:81-82,107-109`; `UnitTargeting.System.cs:62-65`). Bodyblock lee agentes NavMesh administrados, calcula offsets y llama `NavMeshAgent.Move()` en hilo principal. Corre después de `UnitNavMeshSystem` y antes de `NavMeshPositionSyncSystem` (`UnitBodyblock.System.cs:21-22,95-147,195-205`; `Docs/ModeloHybrido.md`, sección squads). No debe depender de las poses o candidatos de un job diferido que aún no haya completado antes del puente físico y de la sincronización de pose.

Targeting tampoco debe escribir poses o destinos: `UnitNavMeshSystem` consume `UnitCombatComponent.target` para perseguir/encarar y `UnitAttackSystem`/`RangedAttackSystem` consumen el mismo contrato para atacar. Mantener el target como estado por unidad y `SquadTargetEntity` como candidatos compartidos conserva las responsabilidades del modelo híbrido.

## Reutilización revisada (reuse-check)

| Elemento encontrado | Archivo | Relevancia | ¿Reusar? |
|---|---|---|---|
| `HeroDataService`, `SquadDataService`, `DataCacheService`, `PlayerSessionService`, `ItemService`, `MapService` | `Assets/Scripts/Core/` | Servicios de datos/sesión/inventario; no resuelven búsqueda espacial de combate | No |
| `HeroPositionUtility`, `UnitStatsUtility`, `FormationPositionCalculator`, `VisualSyncUtility`, `GameTags` | `Assets/Scripts/Shared/` y otros Shared | Posición héroe, stats, geometría, sync visual/tags; no tienen query de enemigos reutilizable | Parcial: utilidades matemáticas/sync permanecen fuera de este cambio |
| Servicios de inventario | `Assets/Scripts/Inventory/Services/` | Sin relación con detección/targeting | No |
| Buffers/componentes existentes `SquadUnitElement`, `SquadTargetEntity`, `DetectedEnemy`, `UnitCombatComponent`, `IsEngagingTag`, `SquadDefinitionComponent`, `SquadSpawnConfigComponent` | `Assets/Scripts/Squads/`, `Assets/Scripts/Combat/` | Ya expresan roster, candidatos, detección de squad, target, gate de engagement y configuración | Sí; ampliar contrato solo con evidencia y compatibilidad, evitar duplicar buffers por unidad |
| Sistemas existentes `EnemyDetectionSystem`, `UnitTargetingSystem`, `UnitBodyblockSystem` | `Assets/Scripts/Combat/`, `Assets/Scripts/Squads/Systems/` | Detección/selección y ejemplo productivo de grid + Burst | Sí; extender los dueños existentes |
| `NativeParallelMultiHashMap<int2,int>`, arrays nativos persistentes, `RepulsionJob` | `UnitBodyblock.System.cs:49-52,156-193,221-289` | Patrón existente para partición espacial y Burst con copia compacta y aplicación managed posterior | Parcial; reusar patrón, no el grid/los datos de bodyblock directamente |
| Eventos en `Assets/Scripts/Events/` | Búsqueda de archivos y referencias dentro de `Assets/Scripts/` | No se necesita evento para el flujo actual síncrono detección→AI→targeting | No |
| Regresiones EditMode existentes | `Assets/Tests/EditMode/ArchitectureRegressionTests.cs:610-668,671-708,777-794` | Fixture 900 unidades, nearest target, limpieza al salir de combate y exclusión de muerto | Sí; extender como red de equivalencia si se implementa |
| Perfil renderizado existente | `Assets/Tests/PlayMode/BattleSceneIntegrationTests.cs:625-675` | Tiene marcador `Conquest.EnemyDetection`, frames y percentiles | Sí; agregar targeting/counters si una decisión lo requiere |

La búsqueda cubrió servicios Core, utilidades compartidas, servicios Inventory, todos los patrones `*Component*`, `*Element*`, `*Tag*`, sistemas bajo `Assets/Scripts/`, eventos y términos de grid, candidatos y buffers nativos. No encontré un índice espacial general para targeting. Bodyblock sí mantiene grid y buckets, pero sus entradas son solo NavMeshAgents activos y sus posiciones provienen de GameObjects; su radio/criterios responden a repulsión cross-team. No conviene acoplar targeting a ese estado ni ampliar la responsabilidad de bodyblock.

## Burst/IJobEntity con SystemBase

**Sí: no hace falta convertir estos sistemas a `ISystem`.** La documentación oficial de Entities 6.5 dice que `IJobEntity` funciona igual con `SystemBase` e `ISystem`, y muestra un `SystemBase.OnUpdate()` que llama `new Job().ScheduleParallel()` ([Unity Entities 6.5 — IJobEntity](https://docs.unity3d.com/Packages/com.unity.entities@6.5/manual/iterating-data-ijobentity.html), líneas 115-138). También especifica acceso `DynamicBuffer<T>` como input de `Execute` y marca `ref` para RW / `in` para RO (misma página, líneas 248-280). La clase `SystemBase` existente puede alojar job structs parciales; el cambio requiere mover cálculo fuera de las llamadas `SystemAPI` inmediatas, pasar `ComponentLookup`/`BufferLookup` actualizados y nativos/blobs al job, y programar con `Dependency`/handle correcto. La comprobación inicial del subagente utilizó documentación oficial versionada. El orquestador confirmó además el paquete instalado en `Library/PackageCache/com.unity.entities@ef040875dea9`, incluida su documentación y fuente; la carpeta sí está disponible en este workspace.

El job Burst no puede capturar `HashSet`, `Dictionary`, `EntityManager`, `SystemAPI`, `NavMeshAgent` ni APIs managed. Buffers y lookups deben declararse con acceso correcto. `DynamicBuffer<SquadTargetEntity>` RW por squad es paralelizable si cada ejecución escribe solo el buffer del squad propio; lectura de otros candidatos debe ser RO. En targeting, escribir `UnitCombatComponent` a través de `ComponentLookup` desde una iteración por squad requiere cuidado: la seguridad de jobs no infiere que las unidades referenciadas por dos `SquadUnitElement` son disjuntas. `NativeDisableParallelForRestriction` solo sería aceptable tras asegurar/exponer ownership único; un error permitiría escrituras cruzadas/races. Alternativa segura: job produce asignaciones en scratch por unidad/squad y un paso dueño las aplica, o job serial por squads hasta probar que la fase merece paralelismo. También serializar las fases que coordinan los conteos/caps por squad; paralelizar unidades individualmente cambiaría el resultado o exigiría reducción y segunda fase determinista.

La API de scheduling paralela no promete orden de iteración. La semántica actual de empate depende del orden de `SquadTargetEntity`; el buffer debe conservar ese orden, o el job debe comparar explícitamente por `(distanciaSq, índiceOriginal)` usando índice original solo como tie-break exacto. No iterar candidatos de un hash map para seleccionar. `Entity.Index` como tie-break nuevo puede ser determinista dentro de un world, pero cambia la selección actual y puede variar con la asignación de entidades, así que requiere decisión de gameplay.

## Propuesta ejecutable

### Mínima, condicionada a perfil

1. Añadir contadores de diagnóstico sin alterar gameplay: S, U, H, C por squad, número de units con target conservado/nuevo, scans de candidatos y retargets por cap. Registrar targeting junto a `Conquest.EnemyDetection` en el fixture existente; comparar mediana/p95 en Editor y un Player de la misma build/escenario.
2. Solo si la detección domina en Player, construir grid por posiciones ECS `LocalTransform` de las entidades targeteables y consultar celdas tocadas por el círculo horizontal de detección de cada centroide, con distancia 3D exacta al final. Usar tamaño de celda configurable o derivado y revisar rango heterogéneo: si `detectionRange` cambia mucho, un cellSize fijo puede hacer que la consulta vuelva a escanear demasiado; si es grande, consultar muchas celdas. Héroes `DetectableEntityTag` y unidades de squads deben conservar team, entidad, posición y secuencia de ocurrencias de la fuente. El índice puede deduplicar datos de búsqueda, pero la salida debe reconstruir todas las ocurrencias y el orden del barrido actual salvo que se demuestre una invariante de unicidad/disjunción. Los duplicados afectan también al centroide de `SquadAnchorSystem`, no sólo al targeting. Mantener la diferencia entre candidatos y `DetectedEnemy` de squad.
3. Tras la grid CPU estable y validada, mover la búsqueda de vecinos a Burst. No migrar ambos sistemas de una vez; usar la equivalencia del buffer candidate y el orden de query actual. Si solo se paraleliza detección, separar snapshot/grid build, consulta por squad y compactación ordenada en pasos con dependencias claras.

La mejora mínima para targeting no necesita Burst si el perfil no lo justifica. Si Targeting se vuelve dominante, evaluar escaneo nearest con índices espaciales por unidad solo después de definir cómo conservar exactamente el empate y cómo encontrar el vecino más cercano fuera de la celda inicial sin falso negativo. Por el momento no hay argumento para implementar un índice separado en targeting más allá de una prueba de concepto.

### Evolución Burst/jobs

- **Detección:** snapshot compacto nativo de objetivos vivos con team/posición/Entity/orden fuente; grid nativo persistente reutilizado, como patrón de bodyblock; construir en job y consultar por squad con exact narrow phase. Compactar/poblar buffers compartidos desde un job por squad (write exclusivo) o entregar buffers en el job con seguridad comprobada. No incluir GameObject/`NavMeshAgent` ni consultar pose managed.
- **Targeting:** mantener procesamiento determinista por squad y fases. Comenzar con job Burst serial por squad/`IJobEntity.Schedule` para que los mapas de conteo sigan siendo privados por squad; comparar coste de scheduling. Luego, si el benchmark lo justifica, modelar una fase de cálculo que escriba slots de salida independientes y otra que aplique `UnitCombatComponent`/`IsEngagingTag`. La redistribución/caps siguen coordinadas y no se deben convertir en escrituras concurrentes sin reducción estable.
- **Memoria:** reutilizar buffers/capacidades por sistema o `World.UpdateAllocator` para scratch de vida de frame. Para `Allocator.Persistent`, redimensionar con Dispose/recreate y liberar siempre en `OnDestroy`, igual que `UnitBodyblockSystem:278-297`; cuidado con disposal mientras hay handles activos.

No proponer cap de vecinos/candidatos ni cooldown de detección sin especificar la semántica de frescura: `BattleWorldState` y `SquadAISystem` dependen de datos del ciclo actual; muerte y respawn también cambian candidatos dentro del frame. Cualquier cadencia reducida introduce latencia táctico-visual y no es equivalente a un índice exacto.

## Tareas pequeñas y verificables

| Tarea | Archivos candidatos | Criterio de aceptación | Validación concreta | Riesgos |
|---|---|---|---|---|
| U6-01a: perf counters y baseline Unity 6 | `EnemyDetection.System.cs`, `UnitTargeting.System.cs`, `ArchitectureRegressionTests.cs`, `BattleSceneIntegrationTests.cs` | Contadores/marcadores distinguen costos por fase; fixture no cambia candidatos ni targets | Ejecutar EditMode 900 unidades y perfil repetible en Player con mismo escenario/semilla; registrar N, S, C, mediana/p95 y GC | No comparar Editor con Player ni usar muestras de distinta densidad; counters pueden añadir overhead si se dejan activos |
| U6-01b: grid de detección CPU exacta | `EnemyDetection.System.cs`; configuración existente solo si hace falta tunable; pruebas `ArchitectureRegressionTests.cs` | Los buffers de candidatos coinciden con barrido de referencia para rangos, teams, héroes, dead/live y variación de centroide; orden/ties se conservan | Equivalencia byte/Entity-list con versión de referencia en escenarios dispersos y densos, frontera de celda/rango, muerte y respawn; revisar p95 Player | Complejidad extra cuando densidad llena celdas; rango variable; ocultar/reordenar duplicados; celdas frontera |
| U6-01c: Burst para detección | `EnemyDetection.System.cs` o job helper ya existente (no crear clase antes de reusar check) | Mismos candidatos ordenados y mismo `DetectedEnemy`; sin safety warnings/leaks ni dependencia incompleta | Burst on/off equivalencia, checks de Collections safety, profiler del Player repetible; pérdida máxima cero | Burst añade marshal/copia/scheduling; posible dependencia con AI si se completa tarde |
| U6-01d: Burst targeting solo si es dominante | `UnitTargeting.System.cs`, regresiones EditMode de targeting | Igual target retenido, adquisición, empate, cap blando, ranged spread, limpieza y tags | Ejecutar regresiones de nearest, targets muertos y fuera de combate; añadir empates, candidate invalidado, melee en rango/saturado y ranged con/sin alternativa; safety checks y perfil Player | Escrituras cruzadas via lookup, no determinismo al reducir conteos, scheduling no amortizado para pocos squads |

La propuesta previa resumida por el orquestador coincide en priorizar reducir candidatos antes de paralelizar y en no afirmar FPS. La precisión que añadiría es: grid de detección y grid de nearest-target no son la misma optimización; targeting todavía tiene una ruta caliente O(N×C), pero una grid por unidad puede alterar alcance/búsqueda exacta y los desempates; IJobEntity funciona desde `SystemBase`, pero `ComponentLookup` RW de unidades referenciadas exige demostrar ownership único o separar el cálculo de la escritura. Los pilotos de cooldown/BlockRegen deben quedar fuera de U6-01 y medirse por separado; no comparten el problema ni las semánticas de targeting.

## Fuentes

- Código actual: `Assets/Scripts/Combat/EnemyDetection.System.cs`, `UnitTargeting.System.cs`, `UnitDeath.System.cs`, `SquadTargetEntity.cs`, `Assets/Scripts/Squads/Systems/UnitBodyblock.System.cs`, `Assets/Scripts/Squads/Systems/UnitNavMesh.System.cs`, `Assets/Scripts/Hero/AI/Systems/BattleWorldState.System.cs`.
- Contratos: `Assets/Scripts/Squads/SystemResponsibilities.md`, `Docs/ModeloHybrido.md`, `Docs/Arquitectura/20_Contraste_Handoff_IA_Tactica_2026-10-14.md`.
- Optimización y medidas previas: `Docs/Arquitectura/12_Deteccion_Targeting_Escala_2026-09-15.md` (0,62 ms detección y 3,08 ms targeting; 900 unidades, EditMode), `Docs/Arquitectura/19_Revision_Sistemas_Optimizacion_2026-10-13.md:38-39,77-81,92-96,109` (medidas de batalla recientes y límites; índice es condicional a perfil).
- Regresiones/perfil: `Assets/Tests/EditMode/ArchitectureRegressionTests.cs:610-668,671-708,777-794`, `Assets/Tests/PlayMode/BattleSceneIntegrationTests.cs:625-675`.
- API `IJobEntity`: [documentación oficial Entities 6.5.0](https://docs.unity3d.com/Packages/com.unity.entities@6.5/manual/iterating-data-ijobentity.html), verifica soporte explícito de `SystemBase` y `ISystem`, ejemplo `ScheduleParallel`, parámetros `DynamicBuffer` RO/RW y atributos Burst.
