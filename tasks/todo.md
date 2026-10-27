# Plan: presentación de la muerte del héroe (animación, ocultar y reaparecer)

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
- EditMode: 353 de 353 en verde (dos tests nuevos en `HeroLifecycleRegressionTests`; `SystemOrderTests` en verde).
- PlayMode: `BattleSceneIntegrationTests`, 11 de 11 (la secuencia local morir → animar → oculto → visible en la pose de
  spawn, y el remoto que se oculta y no reaparece).
- De paso: `EntityVisualSync.Update` ya no llama a `Exists` con el mundo destruido. Ensuciaba el `SetUp` del test
  siguiente con `ObjectDisposedException`.
- Pendiente (clip real, colisión y agente del cuerpo oculto, otros controladores): `Docs/Por_Abordar/Muerte_Heroe.md`.
