# Implementation lessons

## Battle preparation countdown (2026-10-15)

- Manual Start and preparation-time expiry must share the same final-countdown path; a direct scene transition bypasses the team review window. Keep the countdown duration Inspector-configurable.

## Integrated battle tests (2026-09-26)

- Isolate SaveSystem and LoadSystem before initializing a session; inventory listeners can save as a side effect.
- Give each scene integration test its own ECS world. Stop its player-loop updates before unloading baked scene blobs, unload GameObjects while EntityManager is still valid, then dispose the world and restore the prior one.
- Successful order resolution alone does not prove navigation: assert agent registration on NavMesh and actual displacement. Damage-event injection does not validate weapon hitboxes.

## Bodyblock scale profiling (2026-09-14)

- Clearing a dictionary of lists discards cell mappings, not the list allocations; pool buckets independently from the map.
- Warm the system before measuring steady-state allocation and report both agent count and contact density.
- A 900-agent sparse fixture can disprove a GC concern but cannot represent a rendered battle or dense-contact worst case.
- Keep the simpler managed design when measured cost is small; migrate to Native containers/Burst only with profiling evidence.

## Hybrid hero movement authority (2026-09-14)

- Configure CharacterController ownership from the linked ECS identity; component presence alone does not identify the local hero.
- Guard managed NavMesh APIs with both `enabled` and `isOnNavMesh`, and treat `SetDestination` as a fallible operation.
- Reset a rejected path before setting `isStopped`; Unity may otherwise leave the observable stop state cleared.
- Clear movement intent when navigation cannot execute, or animation will retain a false movement signal.
- Validate the actual prefab colliders and project collision matrix before adding another collision system.

## NavMesh destination failures (2026-09-14)

- Keep tactical intent separate from the effective reachable destination; do not overwrite formation ownership to repair navigation.
- Project destinations with the agent type and area mask before calling `SetDestination`, and check both its return value and later path status.
- Arrival/state systems must consume the same effective destination that movement consumes.
- A failed destination needs a stable fallback and a movement-based retry condition; retrying the same invalid point every frame creates permanent churn.
- Pursuit failure should degrade to the formation slot, while formation failure must let navigation complete safely.

## Bodyblock timing (2026-09-14)

- Clamp physical corrections in distance per second, then multiply by delta time; per-frame caps change behavior with frame rate.
- Exact overlap needs a deterministic fallback direction rather than being skipped indefinitely.
- Order agent.Move before the authoritative NavMesh-to-ECS position snapshot.
- Preserve intentional cross-team and wall policies while repairing numerical/timing defects; policy changes need gameplay evidence.
- Keep collision tunables in ECS configuration and profile managed spatial-grid allocations before rewriting them.

## Formation geometry and timing (2026-09-14)

- Preserve fractional formation centres; rounding an even grid's midpoint introduces a systematic half-cell bias.
- Validate quaternions by norm, not by one component, and normalize before rotation.
- Precompute pattern bounds once outside unit loops; compatibility overloads may remain for callers outside hot paths.
- Stable slot identity comes from slotIndex, not from a compacting squad buffer index.
- Unit formation state belongs to each unit's slot error; squad outliers and distance to the anchor are not valid global gates.
- Classify anchor motion from speed using delta time and keep gameplay thresholds in configuration.
- Validate blob creation, length and selected entry before taking a ref into blob memory.

## Owner-death retirement (2026-09-14)

- Connect lifecycle transitions to their required operational components; a FSM enum alone does not execute retirement.
- Keep committed retirement independent of owner respawn and preserve the old reference until cleanup to prevent duplicate instances.
- Missing destinations need a persistent pending state and a frozen anchor, not a fabricated origin target.
- Cleanup must conditionally unlink only the entity it owns and persist survivors before destroying it.
- All-dead squads still need committed retirement cleanup; do not divert them to KO first.
- Re-deployment must gate on owner life and reject eliminated reserves; include actual spawn in regression tests.

## Swap and retreat repairs (2026-09-14)

- Validate replacement prerequisites before retiring the active squad; charge cooldown only on accepted execution.
- A reserve snapshot is not permission to spawn a second copy while the original instance is still retreating.
- Remove the actual active-squad marker, not a similarly named hero marker.
- Resolve remote identity from the hero's map, and preserve initial strength when death compacts unit buffers.
- Navigation observers must not also write motor destinations. Arrival must cover all survivors at their assigned slots, not just the leader at an anchor.
- Once retirement is committed, later movement orders must not reset its transition.

## Hero lifecycle repairs (2026-09-14)

- Death queries must use the health component produced by hero authoring and damaged by combat.
- Clear movement intent on every inhibited/early-exit path; also gate its physical consumer against death and pending spawn.
- A position write alone is not a teleport when a hybrid motor writes its old pose back. Publish an explicit pose revision and consume it once before movement/synchronization.
- Do not increment a spawn revision until a valid spawn point is selected. Preserve the existing controller enabled state during teleport and reset accumulated gravity.
- Keep the larger extraction of the legacy visual motor separate from this lifecycle fix.
- Death and respawn must transition both the health/life fields and the `IsDeadComponent` marker; clear the marker only after a valid spawn point publishes the new pose, and model actual lethal-damage output in tests.
- Local-only respawn must not mean local-only death detection: remote heroes still need `isAlive = false` to stop AI, attacks, capture, and owner squads.

## Squad control repairs (2026-09-14)

- Connect both producers and consumers when migrating intents; component creation alone is not a pipeline.
- Only the formation applier commits currentFormation, slots and cooldown; a request writer must not mark its request completed.
- HoldPosition is an order, not the absence of combat. Preserve its anchor while targeting/attacking remain independent.
- Persist AI requests through temporary overrides and compare destination/target as well as type/source.
- Publish structural hold changes before downstream anchor reads; BeginSimulation playback from Simulation defers them a frame.
- Register managed UnityEngine types queried before visual creation.
- Validate NavMeshAgent in PlayMode and compare destinations to the baked surface, not the nominal source geometry height.
- Keep detection candidates once per squad when every unit sees the same set; only the selected target is per-unit state.
- Gate targeting before allocating native accounting maps, and clear both the stale target and its enableable engagement tag on every inactive path.
- Before preserving compatibility state, prove that it has a reader. Write-only ECS components and command buffers are dead pipeline cost, not architecture.
- Split hybrid authority by contract: ECS owns movement intent, one CharacterController motor owns physical execution, and ECS receives a separate confirmed result. A visual sync component should not also be the motor.
- Locomotion animation should use confirmed horizontal velocity, not raw input alone; otherwise blocked movement still looks like running. Keep input only for direction and button events.
- A hybrid pose bridge must not also own remote animation. Bind a presentation-only driver to confirmed NavMesh/ECS state, keep it away from transforms and destinations, and exclude ordinary unit visuals explicitly.
- Unit death cleanup is gameplay logic and cannot require a successfully instantiated visual. A missing prefab must not leave an immortal squad member.
- Failed NavMesh destinations need a time-based retry as well as a position-change retry, because the navigation surface can change without moving the target.

## Architecture repairs (2026-09-14)

- Read engine/package versions from ProjectVersion.txt and manifest.json, not historical docs.
- An Editor folder beneath a runtime asmdef requires an editor-only assembly boundary. Validate Player scripts, not only Editor compilation.
- Share baking conversions, declare asset dependencies and register persistent blobs with AddBlobAsset.
- Progression changes must reach the components combat actually reads; recalculation must not heal or refill ammunition.
- Consume explicit reward events once. Do not generate XP or save complete snapshots every frame of a phase.
- Use persistent squad IDs for saved data; battle-local indices can be reused and must not identify a saved instance.
- Capture component values before command-buffer playback: structural changes invalidate RefRW and DynamicBuffer handles.
- Event cleanup must distinguish disposable event entities from components attached to living gameplay entities.
- Verify file transactions using isolated temporary files, never the player's real save.
- Track runtime-owned profile entities through their owner and release them on owner teardown. Snapshot ranged damage at fire time so in-flight projectiles survive that teardown.
- Validate required ECS singleton configuration before instantiating a GameObject; exceptions after instantiation but before tagging cause repeated orphan visuals.

## Settlement preview (2026-09-11)

- Resolved BronzeAge footprints already include rotation and local-unit conversion. Do not rotate twice or multiply by cell size again.
- For mouse picking, exclude only the actual GUI panel rectangle, not an entire screen column. Convert bottom-origin input coordinates to top-origin GUI coordinates.
- MaterialPropertyBlock colors are not serialized into saved scenes; rebuild runtime geometry on Play. Use Lit plus a directional light when cube volume must remain readable in an angled view.
- Validate replacement geometry before deleting the previous generated root, and disable the old runtime root before deferred destruction to avoid duplicate active colliders.

## Walkable settlement and scale (2026-09-11)

- Reuse the existing ECS hero, visual registry, EntityVisualSync and follow camera. Use a separate baked prefab-reference component to avoid triggering the battle HeroSpawnSystem. Keep creation/destruction in an ECS lifecycle system, not UI code.
- Editor SubScene setup needs its own editor assembly referencing Unity.Scenes; do not add that dependency to gameplay solely for editor tooling.
- Validate movement with a temporary keyboard and a fixed camera heading. Async editor tests need Application.runInBackground, restored in finally along with device cleanup; otherwise focus loss can stop gameplay advancement.
- Measure skinned meshes in an idle pose with BakeMesh(mesh, true) and consistent coordinate transforms. Rest-pose arms and double-applied import scale produce misleading body widths.
- Keep local-to-Unity XZ conversion uniform and building height independent. Changing only street meshes would overlap the authoritative footprints. This prototype's 3.5 multiplier expands parcels too; it is not yet a shared tactical coordinate contract.
- When adding visual separation between footprint blockouts, shrink each cube around the footprint centre. Do not alter the authoritative rectangle, world conversion or placement; keep the fill ratio as an exposed presentation parameter.
- A swappable projection cannot serialize one fixture's settlement ID into the scene. Auto-resolve only when the payload has exactly one settlement; require an explicit ID for multi-settlement payloads and report the available IDs on mismatch.
- Geometry fixture validation must follow the payload's settlement IDs and cover optional wall arrays. Exact counts tied to one historical fixture make the validation menu reject valid Lab exports.

## Squad and hero contracts (2026-09-25)

- Charge stamina when an action actually starts, not for every frame its input remains pressed. Continuous sprint costs require movement intent, and AI must participate in regeneration and exhaustion.
- Only the unit rotation resolver or NavMeshAgent may rotate the unit root; animation components should not rotate the same root independently.
- If a temporary combat reaction overrides an order, compare the winning order every frame so the stored player order resumes when the reaction ends.
- Runtime-created units need a direct owner reference as well as squad membership in the squad buffer. The visual and death paths can retain a legacy buffer lookup for older baked entities.
- Do not require a baked prefab at swap validation when runtime spawning composes units with CreateEntity.
- Keep one owner for requested formation and let the resolver publish it independently of movement orders; formation-only changes must not reissue movement.
- Read a unit's squad through UnitOwnerComponent instead of rebuilding a reverse buffer map every frame. Keep a measurable destination-change threshold so stable NavMesh paths are not recalculated continuously.
- A local-only input write should run directly; scheduling a parallel job and immediately completing it adds synchronization without useful parallel work.
- Gate physical contact admission at the hybrid boundary as well as at ECS resolution; otherwise idle `OnTriggerStay` callbacks can flood the queue and stale contacts can outlive a strike window.
- Keep a valid NavMesh path instead of reissuing an unchanged destination every frame; request a new path only when the destination changes beyond the configured threshold or the current path fails.
- When a presentation controller rotates a hybrid root after its physical motor runs, publish the final rotation after all `Update` writers; publishing earlier leaves ECS one frame behind even if translation authority is correct.
- Remove write-only ECS components from both the baker and runtime spawn path, and update architecture docs to name the surviving source of truth. A component with a real reader but no current producer is a separate decision, not automatically dead code.
- Resolve independent input axes independently: an invalid formation request must not discard a simultaneous valid squad order. Keep pure command application testable without keyboard or scene setup.
- For reserve initialization at spawn, index static definition sizes once for that spawn; a nested definition query for every reserve entry scales unnecessarily and obscures snapshot fallback rules.
- Split a large spawn system along the data boundary: keep owner/snapshot/slot selection in the system, and move repeated per-unit component composition to a builder that receives explicit immutable context.
- Unity preview builders cannot open an additive scene while the initial untitled scene is unsaved. Use Single in disposable batch projects; interactive replacement must first run Unity's save-modified-scenes prompt.
- A world Terrain preview must expose depth on first entry: use an orbiting perspective camera by default, ground-relative focus and a quick local inspection action. Keep top-down orthographic projection optional rather than making real terrain look like the previous 2D viewer.
- Retain canonical surface masks alongside imported TerrainData. A render passing geometry checks can still lose forest/soil distributions; validate non-empty geographic layers and forest meshes as well as counts, and inspect rendered images before claiming visual completion.
- Copy Unity asset metadata with hidden files included when preparing isolated validation projects: prefab material GUIDs break if texture/material .meta files are omitted. Keep ScriptableObject/MonoBehaviour filenames aligned with class names so baked assets can serialize their script references.
- Persist URP renderer/pipeline assets and initialize the pipeline before submitting off-screen capture requests in batch validation. Assert image variation; geometry counts alone do not catch an all-black capture. Normalize flat rock models by their longest dimension rather than their very small height.
- Avoid coplanar mountain overlays on Unity Terrain: LOD differences produce checkerboard intersections and can cover water. Apply faceted lighting to the terrain shader instead. Reapply owned shaders when refreshing cached materials, and reject magenta pixels as well as blank off-screen captures.
- Retain the imported lake outline before smoothing, and verify a second bake produces identical vertices. Clip river strips against styled lake outlines so they do not appear as lines across the lake; sample longitudinally and across the channel to avoid terrain intersections. Keep tree roots clear of the rendered channel while retaining canonical forest/resource data.
- Forest density must affect candidate spacing as well as acceptance. Validate global separation across species and terrain borders, and compare canopy appearance at campaign scale; fixed-radius rejection cannot produce closed forests.
- A forest trial limited to rectangular Terrain sectors creates artificial straight edges even when its canonical mask is organic. Bake neighboring sectors consistently before changing geography, and apply world-space edge variation only within canonical forest coverage.
- Terrain alphamap continuity requires shared slope evaluation: native GetSteepness can disagree at adjacent tile endpoints even with matching heights and seamless masks. Average neighboring gradients at shared edges/corners and validate serialized layer weights across every border. Keep texture diagnostics scoped to cloned lab TerrainData, layers and materials; restore package references before saving.

- Asymmetric mountain width must blend continuously across the ridge's signed distance: a sign branch can jump at spline endpoints even when the closest distance is continuous. Test local gradients, pass shoulders and traversal independently.
- A smooth maximum of mountain/branch heights must preserve zero: an unconditional blend adds phantom elevation outside every branch and creates a wall at the influence bounding box. Weight the blend by nonzero contribution and test its exterior.
- New geography needs its own depth texture and water meshes/materials as well as TerrainData. Global named river/lake caches silently deform comparison scenes when rebuilding a different world. Pass an explicit experiment asset folder.
- Unity background editor delayCall may remain pending while CLI eval still works. Inspect and remove only the callback created for the task, then call the known public builder directly; never invoke opaque pending delegates or rerun a timed-out build while it is still writing assets.
- A narrow Gaussian ridge kernel cannot use the broad kernel's cutoff: truncation creates foot-slope cliffs; even without cutoff, nearest-segment switching can sharpen distant curved foothills. Widen the kernel as distance increases while retaining sharp mixing near the crest, and check pass approaches on multiple seeds.
- Campaign cliff prefabs with wide, flat bottoms can hang over sloping terrain even after centering and embedding their bounds. Anchor their base relative to the lowest footprint support, sampling adjacent tiles, and expose this as an opt-in bake setting so existing scenes remain unchanged.

- Visual ridge variation is not enough to remove an extruded-wall silhouette. Inspect campaign captures, then combine radial mountain bodies with lower connecting ridges rather than declaring success from prominence tests alone.
- A resource position with a gentle local slope and a nearby traversable cell can still sit on a disconnected island/ledge. Filter strategic resource sites by the measured mainland component before reserving quotas, and retain multi-seed balance regressions.

- Las mediciones de FPS deben comprobar endCameraRendering de la cámara del mapa:
  una ventana oculta puede ejecutar la simulación sin render y producir miles de FPS inválidos.
- El bosque distante se agrupa en coordenadas globales, antes de dividir lotes;
  agrupar por Terrain puede introducir costuras artificiales en los bordes de 4 km.
- Los ajustes de Terrain por cámara se restauran al terminar el render para no
  contaminar la vista regional, otras cámaras ni los datos serializados.


- En iteraciones de relieve, FocusArea ajusta Y al Terrain. Para comparar imágenes
  con encuadre idéntico hay que fijar también la posición mundial de la cámara;
  compartir centro, zoom y ángulos no basta si la altura del terreno ha cambiado.


- Una prueba de escala de campaña debe mostrar ciudad, figura, bosque y relieve
  en la misma zona; una llanura vacía no permite calibrar sus relaciones.
- Comparar bosques simbólicos exige ajustar ancho de copa y altura por separado:
  aumentar ambos puede ocultar ciudades y volver a la lectura de terreno local.

- Worldmap Addressables: Editor Play exit / scene unload can invalidate operation handles before MonoBehaviour cleanup. Check IsValid and result before reading terrain or neighbors, and release a pending completion even when the owning scene has disappeared. Avoid connecting neighbors through stale leases.

- En herramientas Unity, GetComponent puede devolver un objeto con null simulado: usar if (!component), no ??, antes de AddComponent.

- Al generar copias de TerrainData para masas forestales, copiar y verificar explícitamente los alphamaps regionales aceptados. Ocho TerrainLayers asignadas no prueban que la distribución esté conservada; validar los pesos de pintura, también en el overview.

- Al validar R16 contra Terrain.GetHeights, Unity puede introducir hasta un paso adicional de cuantización (medido 0,01528 m para altura 1000). Comparar contra una tolerancia derivada del formato; los bordes entre Terrain sí deben coincidir exactamente.

- Confirmado: persistir una copia de TerrainData puede reiniciar sus splat textures. Aplicar y comprobar SetAlphamaps DESPUÉS de CreateAsset/CopySerialized, sobre el asset persistente. La validación previa a guardar no detecta este fallo.

- La causa final de la pérdida de pintura es persistencia de los SUBASSETS SplatAlpha: marcar también cada TerrainData.alphamapTextures con EditorUtility.SetDirty. Guardar y forzar reimportación; comparar el hash de los pesos antes/después. SetDirty(TerrainData) y comprobación inmediata no bastan.

- Coastal mesh faces must follow the imported height grid, including across sector boundaries. Broad planar faces over a curved Terrain intersect it and expose triangular teeth. Tessellate the canonical footprint and sample the shared R16 surface with a small visual lift; clamp seaward skirts at world bounds.

- GetInterpolatedHeight is bilinear, while rendered Terrain is piecewise planar. Coastal overlays need the envelope of both grid diagonals and a short shoulder apron; matching bilinear vertex heights alone is insufficient. Rebuild Addressables before inspecting loaded sector prefabs.

- At campaign scale a finite-grid coastal overlay needs a conservative face margin, tapered on its inland shoulder. Validate using rebuilt Addressables and a capture after streaming completes, not just matching mesh vertices. The initial 1.2-unit margin still let Terrain triangle tips show through; the final 2.5-unit margin works after fixing the stale GPU buffer (the earlier margin experiments were viewing old geometry).



- EditorUtility.CopySerialized can change Mesh CPU arrays without refreshing an already uploaded GPU vertex buffer. The coastal screenshot continued showing old triangle tips while vertex validators passed. Rebuild existing Mesh assets through Clear, vertices, uv, triangles, normals, bounds and UploadMeshData(false), preserving the GUID. A direct live vertex-buffer refresh confirmed the cause.


- Locomotion repair (2026-10-08): a CurrentGait enum does not select a Synty clip if MoveSpeed still addresses the wrong blend coordinates. Convert confirmed physical speed through shared editable settings; derive sprint from accepted movement, including stamina, and test the actual controller clip output.
- All movement input flags must be false when stopped. A negated MovementInputPressed prevents ToIdle even when MoveSpeed is zero. A single adapter must own movement duration and execute before its consumer.
- NavMesh animation uses confirmed horizontal velocity. GO/ECS position disagreement measures synchronization lag, not speed; world direction must be converted to the visual root's local axes for strafe trees. Never bind an unassociated visual to the first unit.
- Keep root motion disabled when CharacterController/NavMesh owns movement; inspect imported clip averageSpeed before blaming root motion. Use an upper-body combat mask and compare leg poses with combat weight off/on at the same animation time.
- Integration animation tests need stable physical conditions: align blockers with the requested movement direction after grounding/camera initialization; use a clear NavMesh corridor for full-speed remote tests. Crowd-limited motion correctly produces a slower gait.


- Inventory refactor (2026-10-09): una operación compuesta debe devolver todas sus piezas o revertirse, incluidos monedas, cantidades, slots y stats. Publicar eventos específicos sólo sobre el estado final y guardar/publicar el evento general una vez; no confiar en rollback parcial de cada vista.
- Dividir una pila necesita colocación explícita sin autoapilado. Calcular compatibilidad de equipo con el slot que libera la entrada; vender usa cantidad y referencia de la pila seleccionada.
- Una selección de recompensa es una operación pendiente. No consumir hasta entregar, cancelar debe liberar la pendiente, clonar el SO de configuración y proteger aceptar y ofertas de tienda contra callbacks repetidos antes de Destroy.
- Las celdas reutilizadas deben limpiar datos tanto en controller como interacción/drag. Reasignar callbacks de forma idempotente; filas de tooltip ocultas requieren GetComponentsInChildren(..., true) al refrescar texto.
- Los paneles ligados a una escena no deben persistir referencias a sus GameObjects al descargarla. Limpiar el modo UI al destruir el gestor: un flag modal residual puede bloquear input de movimiento en las pruebas siguientes.
- Validar inventario con providers en memoria, UI con las escenas/prefabs reales y apariencia con EntityVisualSync local. No crear servicios DontDestroyOnLoad mediante sus APIs de juego desde eval de EditMode. Separar el spawn ajeno al propósito de una prueba de UI y documentar el aislamiento.

- Calibración de campaña (2026-10-12): `viewSize` no es el ancho visible. Medir los rayos laterales al plano del jugador, ajustar según el aspecto real y verificar posiciones proyectadas; preservar el ancho al retornar entre escenas. Compartir unidades/reloj en un perfil y probar el recorrido sobre sectores cargados. Antes de manipular accesos, comprobar el tipo real: `WorldMapSettlementTarget.entrance` es una posición Vector3, no un Transform.

- Héroe de campaña (2026-10-12): el rig modular importado no da una medida fiable del tamaño animado mediante BakeMesh antes de evaluar el Animator; la normalización produjo un factor 534 y un héroe gigante. Conservar la escala visual aprobada por el usuario (6) en el perfil y comprobar tamaño en Play, sin convertir ese tamaño en una distancia estratégica ni recalcularlo al reconstruir la prueba.

- Navegación de campaña (2026-10-13): verificar pendientes también al simplificar una ruta; una máscara de celdas libre no garantiza que el segmento sea seguro. Separar residencia de terreno del héroe y de la cámara. Reutilizar el visual de ruta existente para evitar dos líneas superpuestas; los clics remotos deben funcionar sin depender de colliders de sectores cargados.


## Sistemas y optimización — 2026-10-13

- RequireForUpdate de MatchState sólo garantiza existencia: las decisiones/ataques deben comprobar fase y limpiar intención/rutas al salir de combate.
- Ordenar productores y consumidores de snapshots exige definir frescura por dato. Leer FSM después de las decisiones que alimenta introduce un ciclo; fijar estado táctico previo y percepción física actual.
- Filtrar atacantes muertos independientemente de destrucción; daño → cleanup → detección/targeting debe ser explícito. Los proyectiles ya disparados conservan su snapshot de daño.
- Guardar JSON no es motivo para vaciar toda la caché de atributos. Invalidar por cambio de dominio/sesión, manteniendo eventos precisos de equipo y rollback transaccional.
- Nombre visible no es identidad de caché ni de participante de batalla: conservar GUID en JSON/DTO; rechazar fallback por nombre ambiguo o un ID explícito distinto.
- Registros de visuales necesitan clave World/Entity y desregistro al sustituir/destruir el vínculo. Colliders prestados se consumen al inicializar, sin retener listas scratch compartidas; restaurar IgnoreCollision al reutilizar/desactivar.
- Pool de A* requiere un contexto por iterator activo y Dispose explícito al cancelar. El finally sólo limpia el contexto que posee, sin borrar una búsqueda nueva iniciada por un callback.
- Burst calcula sobre datos nativos; NavMeshAgent/Animator permanecen en el puente managed. Disponer memoria persistente al destruir el sistema y procesar sólo el conteo vigente.
- Los fixtures de combate deben establecer InBattle: el valor inicial de MatchState representa otra fase. No relajar un gate correcto para acomodar un fixture incompleto.
- Antes de EditMode comprobar que el Editor haya salido de Play Mode. La API puede dejar el runner pendiente si la escena de prueba anterior sigue ejecutándose.
- En Windows, usar encoding UTF-8 explícito en cada lectura/escritura Python; la codificación predeterminada puede convertir comentarios y textos UTF-8 en mojibake.

- La actualización derivada de equipamiento pertenece al commit exitoso, antes del evento general de inventario: un debounce con coroutine puede dejar stats antiguos al entrar inmediatamente en batalla. Coalescer la transacción permite recalcular una vez sin ese retraso.

- Si un World se reutiliza entre partidas, el bootstrap aceptado debe reiniciar MatchState y reactivar MatchController; de otro modo un gate correcto de EndMatch deja detenida la siguiente batalla.

- Al paralelizar repulsión, cada agente escribe sólo su propio offset; visitar pares en ambos sentidos evita carreras sin desactivar checks de seguridad. Usar orden canónico para dirección de solapamiento y reducir contadores después del job; comparar casos dispersos y densos porque el scheduling puede costar más con pocos contactos.

- Consolidar archivos no autoriza inferir propiedad: preservar progresión heredada sin dueño en un bloque explícito, versionar el contenedor y rechazar versiones futuras. Los escritores de equipo y progresión deben compartir lock y preservar el estado más reciente del otro dominio; cargar no migra archivos en disco.
- Las pruebas de escena deben inyectar también el store de progresión, además de SaveSystem/LoadSystem, para que un perfil de rendimiento no modifique guardados reales. Confirmar eventos de render durante el muestreo y separar coste de Editor/runner de un Player.

- JsonUtility puede materializar objetos anidados vacíos para campos null/ausentes: una migración de bloque debe usar versión/proveniencia, no sólo `??=`, para no confundir defaults con datos ya importados. Cubrir JSON del formato anterior serializado por Unity además de JSON escrito a mano.

- Los eventos de XP necesitan destinatario persistente; capturarlo al aceptar el bootstrap y no leer SelectedHero al consumir la recompensa. Reasignar el singleton al iniciar otra partida y descartar eventos de otro dueño.
- Commit de recompensa antes de consumir evento o modificar ECS. Un proveedor puede devolver un objeto vivo: clonar/stagear impide que un fallo de Save mute el estado original. Una revisión por héroe preserva XP reciente frente a snapshots viejos de inventario, sin bloquear cambios del snapshot actual.
- Tras migrar identidad a GUID, revisar comparaciones internas con heroName en servicios auxiliares. Modificaciones temporales y cálculo de puntos deben verificar que el ID corresponde al héroe usado como base.

- JsonUtility ejecuta inicializadores de HeroData para un heroId ausente: comprobar únicamente string.IsNullOrEmpty tras cargar puede ocultar IDs regenerados. Inspeccionar la identidad serializada con el DTO neutral sin generador y marcar el perfil; confirmar IDs al aceptar bootstrap antes de recompensas dirigidas. Carga pasiva y backup del formato original siguen siendo obligatorios.

- Una velocidad/gait de carrera no garantiza que la transición del Animator haya finalizado tras medio segundo: esperar el clip real con plazo y mantener las aserciones de peso/velocidad. Un fallo intermitente de integración merece corregir esa condición temporal, no eliminar la comprobación del clip.

- HeroData/ECS mantienen XP restante por nivel; una vista no debe inferir Level con una función de XP total ni restar de nuevo los umbrales históricos. Usar el Level guardado y el mismo threshold floor del consumidor de recompensas.

- La progresión de squad debe volver a la misma instancia GUID del HeroData que consume el bootstrap, no sólo al mapa runtime o al archivo antiguo. Verificar GUID e índice de roster antes de persistir; los índices battle-local se reciclan.
- Si un lote tiene varios commits, un ECB diferido de destrucción puede duplicar recompensas anteriores al fallar una posterior. Procesar un snapshot de eventos y consumir cada commit confirmado de inmediato, sin iterar una query viva durante cambios estructurales.
- Mantener revisiones independientes de héroe y squad: proteger sólo sus campos XP/nivel, conservando formación, inventario y efectivos de otros dominios. El commit lee el perfil más reciente y actualiza la sesión por dueño/instancia explícitos.
- Los paneles no deben usar thresholds/caps placeholder cuando el consumidor ECS ya define la fórmula y el máximo. Mostrar XP restante con el cálculo compartido.

- IsLocalSquadActive es estado de control, no identidad de dueño: la retirada lo elimina antes de destruir el squad. Identificar recompensas por dueño local persistente y roster validado también después de perder esa etiqueta.

- Una escena de prueba con configuración completa puede ocultar dependencias ausentes en la escena de producción. Validar preparación → escena real y comprobar los singletons requeridos por sus prefabs; BattleScene necesitaba ShieldConfigAuthoring para crear tropas con escudo. Corregir el authoring mediante Editor, sin inyectar valores sólo en el test.
- Para perfilar bodyblock durante combate, registrar contactos en la propia ventana y empezar al detectar el choque: una espera de calentamiento posterior puede dejar una muestra sin contactos aunque el flujo haya combatido antes.

- Ground está registrado como layer, no como tag. CompareTag con un nombre no registrado lanza una excepción aunque el hit sea otro objeto. Al proyectar órdenes, comprobar la capa Ground/TerrainCollider y conservar fallback; validar la orden con cursor en la escena real, no sólo la entrada de teclado.

- Cursor.lockState y Cursor.visible sobreviven al cambio de escena. Una interfaz debe aplicar su política al terminar la carga, después de los OnDestroy que restauran gameplay; cerrar paneles o recibir foco tampoco debe bloquear el cursor en BattlePrep/PostBattle. Probar entrada desde cursor Locked y clicks reales del InputSystem, no sólo invocar onClick.

- Guardar la escena de origen al aceptar la entrada en cola, no al cargar preparación/resultados. El servicio persistente debe limpiar Assigned y su coroutine antes de volver para permitir otra partida; cargar por path requiere normalizar sólo la clave de fase y conservar el path para SceneManager.

- En Windows, comprobar que el alias de ejecución `unity.exe` no precede a la instalación oficial de Unity CLI en `PATH`. Si `unity status` devuelve `PIPELINE_LOAD_PENDING`, comparar `Packages/manifest.json`, `packages-lock.json` y `Library/PackageCache`; traer el Editor al frente hasta resolver/recompilar el paquete y verificar tanto el descriptor `.unity-pipeline-port` como `unity status` en estado `ready`.
- Para omitir cálculos ECS estables, invalidar la caché con todos sus inputs de gameplay y entorno. En formación: pose del ancla revisionada, orden/estado, formación, slot, dueño y Terrain/Data/origen/heightmap; al cambiar terreno hay que volver a muestrear altura aunque el slot conserve X/Z.
- Unity CLI: invocar `C:\Users\VRINO\AppData\Local\Unity\bin\unity.exe` cuando WindowsApps intercepte el alias. Si el menú muestra Start deshabilitado pero CLI no descubre el servidor, detenerlo y arrancarlo desde Window/Pipeline; confirmar `ready` con CLI. Cerrar la sesión de computer use al terminar la intervención.
- Organización de Resources: `Resources.Load` ignora mayúsculas; una diferencia de capitalización no demuestra un fallo de carga. Separar la normalización de nombres de una reparación funcional; mover desde AssetDatabase, conservar GUID y comprobar las cargas antes y después.

- Los parámetros del Animator sí requieren el nombre/hash exacto: contrastar contrato y tipo del controller real con AnimationHashes. Un bool `isAttacking` no satisface `IsAttacking`; corregir el asset y probar los puentes local/remoto, sin silenciar las advertencias mediante guards.
- Antes de comparar rendimiento, confirmar que el Editor cargó la versión instrumentada del test. Construir el cronómetro antes del tramo GC y capturar huellas/contadores después; conservar referencias brutas y no atribuir una ganancia a diferencias dentro del ruido.
- Un timeout del dispatcher de Pipeline no cancela necesariamente la operación del Editor. Comprobar el resultado durable (XML, log de compilación y ensamblados) antes de repetir una operación costosa o declarar fallo.
- Un DynamicBuffer adquirido en un fixture queda invalidado por AddBuffer/AddComponent/CreateEntity posteriores. Terminar cambios estructurales antes de adquirirlo o volver a pedirlo después de cada cambio; no debilitar los checks de seguridad de Collections para que la prueba pase.
- En herramientas de render, invalidar clones por el hash de dependencias del pipeline, además del GUID; clonar también renderer features y guardar sólo los assets propios. Verificar las APIs de EditorGraphicsSettings: un getter público no implica setter en la versión instalada.
- Un objeto llamado WeaponHitbox con BoxCollider trigger no completa el contrato de melee: necesita WeaponHitboxBehaviour y un cuerpo físico configurado para emitir callbacks. Auditar componentes del prefab real desde el Editor; mantener collider y balance originales al reparar la conexión.
- En pruebas NavMesh, comparar contra destination/nextPosition aceptados después de la proyección, no contra la coordenada solicitada: la altura del mesh horneado puede diferir. En animación, comprobar los clips y capas que tiene cada controller, sin asumir que todas las tropas comparten Combat.
- Un censo de renderers puede incluir cientos de miles de piezas modulares inactivas. Conservar totales completos y limitar filas de detalle a una muestra activa; no serializar inventarios enormes junto a una medición de rendimiento.
- Cambiar UniversalRendererData.renderingMode desde una herramienta no garantiza persistencia. Marcar el renderer propio como dirty, guardarlo y comprobar el modo tras recarga; reaplicar la configuración a clones experimentales existentes antes de medir GRD.
- Unity Test Runner puede abortar un IEnumerator anidado sin disponer el enumerador padre. Restaurar overrides globales también en UnityTearDown; no depender exclusivamente del finally del wrapper cuando una aserción interna falla.

## Lote 1 de movimiento y escenarios medidos por MCP (2026-10-20)

- Con el editor manejado por MCP, la ventana Game no tiene el foco y el Input System descarta el teclado. Los escenarios de PlayMode con un teclado virtual usan una copia temporal de `InputSettings` (`AllDeviceInputAlwaysGoesToGameView` + `IgnoreFocus`) y la restauran al acabar. Si el objeto anterior fue destruido, se crea uno por defecto.
- En `BattleTestScene` la escuadra local no arranca en FollowHero: hay que pulsar C (o fijar la orden) antes de esperar que siga al héroe. El héroe remoto aparece unos 40 m por delante (+z), así que un escenario de marcha debe ir hacia −z para no acabar en combate.
- `BattleSceneIntegrationTests` falla en SetUp si falta el paquete ignorado por git «Travis Game Assets» (`HitImpactEffectManager.hitEffectPrefab`). No se tapa en general: solo los escenarios explícitos de medición ignoran los errores registrados.
- `run_tests` en PlayMode responde 400 aunque la ejecución sí arranca. Esperar con `wait_for` a que `EditorApplication.isPlaying` pase a true y luego a false, y leer `test_status`. No lanzar otra ejecución hasta que el editor haya salido de Play Mode.
- Un umbral de reenvío de destino demasiado fino duplica `SetDestination` en cuanto la tropa sigue de verdad al héroe. Para medirlo, separar por fases (marcha frente a parada) en lugar de usar una media global. Para no duplicar las proyecciones de NavMesh, adelantar el slot ya proyectado en lugar de proyectar el punto adelantado.
- Un sistema de métricas de solo lectura con `ProfilerRecorder` debe usar el nombre de marcador de Entities, que es `"<nombre del World> <nombre del sistema>"`.

## Lote 2 de movimiento (2026-10-20)

- Burst no encontró como punto de entrada los `IJobParallelFor` anidados en una clase `static` (el log decía "not a known Burst entry point" y el job corría en managed, igual de lento con Burst activado que sin él). Declarar los jobs como structs de nivel superior y comprobar con un benchmark que Burst los aplica: comparar el tiempo con `BurstCompiler.Options.EnableBurstCompilation` en false y en true.
- ORCA con un radio combinado igual al espaciado de la rejilla deja cada slot en contacto con sus vecinos, y una fila formada es un muro que ORCA no rodea (no planifica caminos). Dentro de la escuadra, esquivar solo a las unidades que se mueven y con un radio por debajo del espaciado.
- Un umbral de cambio de marcha (Walk → Run) por encima del retraso real de arranque nunca se dispara. Medir primero el retraso en marcha estable (≈ 1 m) y poner la entrada por debajo y la salida por encima del retraso al que se quiere volver.
- Una ley de recuperación proporcional frena la llegada cuando el slot ya está quieto. Con el slot parado, dejar que el agente llegue a la velocidad de su marcha y frene con su propio `autoBraking`.
- Al limitar la persecución (correa), revisar los escenarios que daban por hecho el contacto: S6 paraba al empezar la reacción, a 8-9 m del enemigo, y dejó de llegar a la melé.
- S6 cambia mucho de un run a otro, y dentro de la suite completa se comporta distinto que solo. Comparar con dos o más corridas y mirar el CSV (reacción, objetivo, ataque) antes de atribuir un cambio al código.

## Formaciones por datos y reparación del movimiento (2026-10-20)

- Tras añadir un campo a un struct que usan jobs Burst, comprobar los tests con Burst activado y desactivado. Una vez Burst siguió ejecutando la disposición vieja del struct (leía `steered` donde ahora estaba `squadSpacing`) hasta que se reordenaron los campos y se recompiló. Síntoma: los mismos resultados erróneos con Burst, correctos sin él.
- Para sustituir un ScriptableObject sin romper referencias, renombrar la clase y el archivo conservando el `.meta` (mismo GUID de script), leer los datos viejos con `FormerlySerializedAs` en un campo oculto y convertirlos una vez desde un menú del editor. Escuadras, interfaz y prefabs siguen apuntando al mismo asset.
- No identificar unidades por su índice de slot en las métricas: cuando el reparto cambia, todas parecen "teletransportarse" en el mismo frame. Usar la entidad.
- Escribir `agent.velocity` directamente se salta la aceleración del agente (picos de 200 m/s²). Partir de la velocidad actual y limitar el cambio por frame; permitir frenar más rápido que acelerar, o las unidades pegadas a su slot se pasan medio metro cuando el héroe para.
- "Ranura k = soldado k" solo funciona si las plantillas de todas las formaciones mantienen la ranura k en una zona parecida. Si no, al cambiar de formación la tropa se cruza y ORCA la bloquea; repartir por cercanía en el evento.
- Antes de diseñar una "dirección de bloqueo", comprobar cómo se decide de verdad: aquí el bloqueo es el collider del escudo colgado de la mano, y `UnitShieldComponent.orientation` no lo leía nadie.
- Una métrica de solapes con umbral fijo marca como fallo una formación compacta a propósito (Testudo a 0,8 m). Hacerla relativa al espaciado de la formación.
- En Bash, los heredocs con plantillas JS que contienen comillas invertidas y `${…}` pueden romper el comando. Escribir el script a un archivo con la herramienta de escritura y ejecutarlo.

## Frenado de llegada (M12, M13, 2026-10-21)

- Antes de arreglar un pico, demostrar de dónde viene. Los picos de S6 se habían atribuido al bodyblock; un registro temporal de cada caída de `agent.velocity` mayor de 1,5 m/s en un frame, con el estado del agente (camino, destino, aturdimiento), mostró que eran paradas del propio `NavMeshAgent`. Y el CSV solo graba la escuadra enfocada: no sirve para medir distancias a enemigos.
- El `autoBraking` del `NavMeshAgent` no frena con antelación: si llega a un destino quieto a 4 m/s, para en un frame. Mandar "quédate aquí" (destino = posición actual) a una unidad en marcha es una parada instantánea. Limitar la velocidad con √(2·a·d) hacia destinos quietos y, para parar, apuntar al punto donde termina de frenar.
- No poner rampa (aceleración limitada) a una corrección de posición tipo muelle como el bodyblock: con retardo se pasa, sigue empujando cuando el contacto ya terminó y oscila. Si el problema es el tamaño del empujón, limitar su tope.
- S6 varía tanto entre corridas (solapes 8-15, picos 1,9-3,5 % sin cambiar nada) que una sola corrida no decide nada. Usar un parámetro que apague el cambio (aquí `arrivalDecelerationFraction` = 0) y comparar 3 o más corridas de cada lado sin recompilar.

## Reproducibilidad de los escenarios (M10, 2026-10-21)

- Un escenario que solo fija el paso de tiempo mientras graba no es reproducible: la carga y la preparación corren al ritmo real del editor y cada corrida empieza a medir en otro estado. Fijar `Time.captureFramerate` desde el `SetUp`, restaurarlo en el `TearDown` y volver a sembrar `Random` justo antes del escenario. Comprobarlo con `cmp` sobre las columnas de posición de dos CSV: deben coincidir hasta el primer evento no determinista.

## Llegada del ancla y margen en Walk (C10, M1, 2026-10-21)

- Con escenarios reproducibles, una corrida por variante basta para S1-S4, pero las llegadas son caóticas: un cambio pequeño en la trayectoria mueve el tiempo hasta formar unas décimas en cualquier sentido. Una mejora que solo aparece en el tiempo hasta formar de un escenario no prueba nada por sí sola; contrastarla con el error en marcha y con los demás escenarios antes de atribuirla.
- Una regla que tiene sentido en el diseño (no acelerar si la unidad va adelantada a su slot) empeoró S4. Probar cada hipótesis por separado con su medida y quitar la que no ayuda, aunque "debería" ayudar.
- Al copiar un parámetro de la referencia, mirar cuánto tiempo está activo, no solo su valor. El horizonte de 5 s de CB solo vale en el primer cambio de estado del soldado y después baja a 1 s; copiar "5 s" retrasó las llegadas, y 1 s (lo habitual allí) las mejoró.
