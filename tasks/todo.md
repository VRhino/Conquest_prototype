# Plan: presentación de la muerte del héroe (animación, retirar el cuerpo y reaparecer)

Estado: **hecho** (2026-10-27), rama `main` local, sin subir. Plan anterior archivado en `tasks/block3-skills-perks-plan.md`.

## Problema
Cuando un héroe muere (`HeroRespawnSystem` pone `HeroLifeComponent.isAlive = false`), su visual sigue de pie en el campo
durante toda la espera, sin ninguna animación. Los remotos y la IA no resucitan, así que su cuerpo se queda para siempre.

## Diseño
- **ECS, un solo escritor.** Nuevo `HeroDeathPresentationComponent` (horneado por `HeroEntityBaker`, así lo llevan el
  local y los remotos): `deathRevision` (sube en cada muerte), `timeSinceDeath` y `visualHidden`. Lo escribe solo
  `HeroDeathPresentationSystem`, `[UpdateAfter(HeroSpawnSystem)]`, que lee `HeroLifeComponent` y `HeroSpawnComponent`:
  - vivo → muerto: `deathRevision++`, `timeSinceDeath = 0`;
  - muerto: acumula tiempo y oculta cuando pasa `deathVisualHideDelay`;
  - vivo y `hasSpawned` (la pose de reaparición ya está publicada): visible.
- **Config.** `HeroGameplayConfigComponent.deathVisualHideDelay`, con su campo en `HeroGameplayConfigAuthoring`. Sin el
  singleton (tests), se usa la constante por defecto del componente, la misma que inicializa el authoring.
- **Visual, solo lectura.** `HeroDeathVisualPresenter` (añadido por `EntityVisualSync`, local y remotos): en `LateUpdate`,
  después de que el motor local aplique la pose de reaparición, apaga o enciende los `Renderer` según `visualHidden`.
  Animación: `EntityVisualSync` (local) y `RemoteHeroAnimationDriver` (remotos) comparan `deathRevision` con la última
  vista y disparan `TriggerDeath`; mantienen `IsDead`. Ninguno escribe en ECS. Solo se tocan parámetros que el Animator
  tenga, para que un controlador sin ellos no avise.
- **Animator.** `AC_Polygon_Masculine` gana `TriggerDeath` (Trigger) e `IsDead` (Bool) y un estado `Death` sin clip
  (el cuerpo se congela en su pose) con salida cuando `IsDead` es falso. Falta el clip: `Docs/Por_Abordar`.

## Pasos
- [x] 1. ECS: componente, config, sistema y test EditMode de la secuencia (morir → oculto tras el retardo → visible al
      reaparecer; el remoto queda oculto).
- [x] 2. Animator: parámetros y estado de respaldo; hashes en `AnimationHashes`.
- [x] 3. Visual: presentador, disparo del trigger en local y remotos.
- [x] 4. PlayMode: ampliar `BattleSceneAppliesDamageRemovesDeadUnitAndRespawnsHero` (renderers apagados tras el retardo,
      encendidos tras reaparecer en la pose de spawn).
- [x] 5. EditMode en verde (SystemOrderTests incluido), `BattleSceneIntegrationTests` en verde.
- [x] 6. Docs: `Docs/Por_Abordar` (clip real, colisiones del cuerpo oculto), lecciones.

## Resultado
- EditMode: 353 de 353 en verde (dos tests nuevos en `HeroLifecycleRegressionTests`; `SystemOrderTests` en verde), también tras la corrección.
- PlayMode: `BattleSceneIntegrationTests`, 11 de 11 en dos corridas seguidas tras la corrección (morir → animar → fuera de la jerarquía → visual nuevo en la pose de
  spawn; el remoto se retira y no vuelve).
- De paso: `EntityVisualSync.Update` ya no llama a `Exists` con el mundo destruido. Ensuciaba el `SetUp` del test
  siguiente con `ObjectDisposedException`.
- Pendiente (clip real, colisión y agente del cuerpo oculto, otros controladores): `Docs/Por_Abordar/Muerte_Heroe.md`.

## Corrección del usuario (2026-10-27): retirar el cuerpo, no solo ocultarlo
"No basta con ocultarlo, hay que sacarlo de la jerarquía y que cuando spawnee entre de nuevo; así nos evitamos la
colisión."
- [x] `HeroVisualInstantiationSystem` destruye el visual de los héroes muertos cuando `visualHidden` (lo desactiva en
      el acto) y quita `HeroVisualInstance`, `HeroVisualAppearanceApplied`, `NavAgentComponent` y `NavMeshAgent`. Su
      pasada normal lo vuelve a crear en la pose de spawn al reaparecer. `HeroDeathPresentationSystem` va antes.
- [x] Fuera `HeroDeathVisualPresenter` (apagaba los renderers).
- [x] Tests de PlayMode: el local sale de la jerarquía y entra un visual nuevo, con motor local, en la pose de spawn;
      el remoto se retira sin agente y no vuelve.
- [x] Docs: D2 y D3 hechos y D5 nuevo (coste de recrear) en `Docs/Por_Abordar/Muerte_Heroe.md`.

# Bloque 4 (2026-10-27): H9, H8, P2, H7 + H13

- [x] H9: `HUDController.OnDestroy` no libera consultas si el mundo ya no existe.
- [x] H8: quitar "(Passive)" del `perkName` de los tres perks activos (assets LFS).
- [x] H7: el cambio de escuadra usa el grupo `SquadSwapCooldown.Group` del `CooldownGroupElement` del héroe; fuera
      `SquadSwapCooldownComponent` y `SquadSwapCooldownSystem`.
- [x] H13: `InactiveSquadElement.cooldowns` guarda los grupos activos de la escuadra al retirarla (cambio o muerte del
      dueño) y `SquadSpawningSystem` los restaura.
- [x] P2, opción (a) del usuario: fuera la ruta horneada (prefab de clase, authorings, componente en `HeroEntity_Pure`,
      componentes sin lector y `HeroAttributeSystem`, con lo que P4 también queda cerrado). `HeroClassManager`, única fuente.
- [x] EditMode en verde (355 de 355) y commit.

# Barra de habilidades (2026-10-27): H6 + H10

- [x] `UIHeroBattleDataComponent.skills`: un hueco por `SkillSlot` (Q..Squad3) con skill, fracción y segundos; lo
      escribe `UIBattleDataSystem` desde el héroe local y su escuadra activa.
- [x] `SkillBarController` + `SkillBarSlot` + `SkillIconCatalog`; prefab `Resources/UI/SkillBar` creado con `eval_file`;
      `HUDController` lo crea (sin tocar `BattleScene`) y quita sus cuatro huecos sin conectar.
- [x] Tests: `SkillBarDataTests` (EditMode) y `BattleSceneShowsTheSkillBarWithTheHeroSkills` (PlayMode).
- [x] EditMode 357/357; BattleSceneIntegrationTests 12/12. Sin revisar a ojo en partida.
- [x] H11 (2026-10-27, decisión del usuario): escuadra en 1/2/3 (GDD §12.3), perks activos en 4/5; la barra pone la
      escuadra delante de los perks.

# Contacto enemigo en la melé (2026-10-28): P0 + P1 de `game-re/notes/battle-movement-combat.md`
- [x] P0: `UnitBodyblockSystem` con una sola distancia de contacto enemigo (2·bodyRadius) y sin tocar al quieto.
- [x] P1: `EnemyContact` ("chocar y resbalar") al final de `UnitAvoidanceSystem`; interruptor `enemyContactEnabled`.
- [x] Tests: `EnemyContactTests` (EditMode); escenarios S6/S7 `LegacyContact` para comparar.
- [x] Medida (3+3 corridas intercaladas): solapes enemigos > 0,3 s S6 9 → 0, S7 12 → 0; picos S6 1,8 → 1,5 %.
- [x] P3: el retroceso del golpe recorta su paso contra enemigos y corre antes del bodyblock (neutro; solapes en 0).
- [x] P2: `SquadEnemyCollisionComponent` por escuadra y `passesEnemies` por golpe; el par choca solo si los dos lo tienen.
- [x] P4: `EnemyContact` dice quién bloquea (±30°), `UnitAvoidanceSystem` lo guarda en `UnitBlockedComponent` y
  `UnitTargetingSystem` vuelve a puntuar con él y un bonus antes de la saturación tras `blockedRetargetTime` (0,5 s),
  para formar líneas de combate. Variantes S6/S7 `NoBlockedRetarget` para comparar.
- [x] P5: mientras golpea, la unidad cuerpo a cuerpo solo frena (`attackMovementLock`). Velocidad al golpear S6 0,52 → 0,25 m/s;
  primer golpe igual. Variantes S6/S7 `NoAttackLock`.
- [x] P7: escenario S9 (dos líneas de frente) y métricas de línea en el grabador. Solapes 10 → 0, pero filas de atrás por
  delante de la primera 4 → 17 % y primer golpe +0,8 s.
- [ ] Siguiente: que las filas de atrás no rebasen a la primera en S9; P6 solo si vuelven los picos.
