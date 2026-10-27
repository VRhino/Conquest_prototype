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
- [ ] P2: **parado, decisión del usuario.** El prefab hornea una sola clase para todos los héroes; asignar un asset
      daría a todos esos atributos. Opciones en `Docs/Por_Abordar/Pila_Modificadores.md` P2.
- [x] EditMode en verde (355 de 355) y commit.
