# Implementation lessons

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

- Calibración de campaña (2026-10-12): `viewSize` no es el ancho visible. Medir los rayos laterales al plano del jugador, ajustar según el aspecto real y verificar posiciones proyectadas; preservar el ancho al retornar entre escenas. Compartir unidades/reloj en un perfil y probar el recorrido sobre sectores cargados. Antes de manipular accesos, comprobar el tipo real: `WorldMapSettlementTarget.entrance` es una posición Vector3, no un Transform.

- Héroe de campaña (2026-10-12): el rig modular importado no da una medida fiable del tamaño animado mediante BakeMesh antes de evaluar el Animator; la normalización produjo un factor 534 y un héroe gigante. Conservar la escala visual aprobada por el usuario (6) en el perfil y comprobar tamaño en Play, sin convertir ese tamaño en una distancia estratégica ni recalcularlo al reconstruir la prueba.

- Navegación de campaña (2026-10-13): verificar pendientes también al simplificar una ruta; una máscara de celdas libre no garantiza que el segmento sea seguro. Separar residencia de terreno del héroe y de la cámara. Reutilizar el visual de ruta existente para evitar dos líneas superpuestas; los clics remotos deben funcionar sin depender de colliders de sectores cargados.
