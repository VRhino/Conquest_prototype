# Implementation lessons

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
