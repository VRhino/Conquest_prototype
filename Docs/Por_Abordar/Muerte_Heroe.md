# Muerte del héroe: lo que queda fuera

Fecha: 2026-10-27. Sale del plan de presentación de la muerte del héroe (`tasks/todo.md`): animación de muerte,
retirar el cuerpo tras un retardo y volver a crearlo al reaparecer.

## Qué hay
- `HeroDeathPresentationSystem` (único escritor de `HeroDeathPresentationComponent`, entre `HeroSpawnSystem` y
  `HeroVisualInstantiationSystem`) sube `deathRevision` en cada muerte, pone `isDead`, pide retirar el cuerpo
  (`visualHidden`) cuando pasan `HeroGameplayConfigComponent.deathVisualHideDelay` segundos (por defecto
  `DefaultDeathVisualHideDelay`, 3 s, en `HeroGameplayConfigAuthoring`) y lo deja volver cuando el héroe está vivo y
  `HeroSpawnSystem` ha publicado la pose de reaparición.
- `HeroVisualInstantiationSystem`, dueño del ciclo de vida del visual, saca el GameObject de la jerarquía mientras
  `visualHidden`: lo desactiva en el acto (sin colisión, hitboxes ni agente) y lo destruye. También quita
  `HeroVisualInstance`, `HeroVisualAppearanceApplied`, `NavAgentComponent` y el `NavMeshAgent` de la entidad. Al
  reaparecer, su pasada normal lo vuelve a crear en la pose de spawn, y `HeroVisualAppearanceSystem` le aplica la
  apariencia de nuevo. Mientras no hay visual, la cámara sigue a `LocalTransform`.
- `HeroDeathAnimationBridge` (lo usan `EntityVisualSync` para el local y `RemoteHeroAnimationDriver` para remotos e IA)
  dispara `TriggerDeath` una vez por muerte y mantiene `IsDead`. No escribe en ECS. Si el Animator no tiene esos
  parámetros, no hace nada.
- `AC_Polygon_Masculine` tiene `TriggerDeath` (Trigger), `IsDead` (Bool) y un estado `Death` con `Hero_Death` (Any State →
  `Death` con `TriggerDeath`; `Death` → salida cuando `IsDead` es falso). Ver D1.
- Tests: `HeroLifecycleRegressionTests.DeathPresentation*` (EditMode) y, en PlayMode,
  `BattleSceneIntegrationTests.BattleSceneAppliesDamageRemovesDeadUnitAndRespawnsHero` (local: morir → animar → fuera
  de la jerarquía → visual nuevo en la pose de spawn) y `BattleSceneRemoteHeroDeathAnimatesThenRemovesTheBody` (el
  remoto se retira, sin agente, y no vuelve).

## Resumen
| # | Tema | Prioridad | Estado |
|---|---|---|---|
| D1 | Clip real de la animación de muerte | Media | **Hecho** (2026-11-10: `Hero_Death`, 1,8 s; retardo 3,5 s) |
| D2 | El cuerpo oculto sigue teniendo colisión y agente de NavMesh | Media | **Hecho** (2026-10-27: el visual se retira de la jerarquía) |
| D3 | Renderers que aparecen mientras el cuerpo está oculto | Baja | **Hecho** (2026-10-27: no hay visual que mostrar) |
| D4 | Otros controladores de héroe sin los parámetros de muerte | Baja | Pendiente |
| D5 | Coste de recrear el visual al reaparecer | Baja | Pendiente (medir) |

---

## D1. Clip real de la animación de muerte
**Hecho (2026-11-10).** `Death.motion` es `Assets/Art/Animations/Humanoid/Hero_Death.fbx`: golpe, cae de rodillas y se
desploma de frente; dura 1,8 s, sin bucle y con todo el desplazamiento integrado en la pose. `DefaultDeathVisualHideDelay`
pasa de 3 a 3,5 s: el cuerpo queda 1,7 s en el suelo antes de retirarse. Se hizo con Blender por línea de comandos
(`blender-tools/conquest-anims`) sobre el mismo esqueleto del Modular Hero, y en Unity se comprobó que el clip
reproducido sobre `ModularCharacters` no atraviesa el suelo más de 8 mm. `Write Defaults` sigue apagado: el clip anima todos
los músculos.

**Qué era.** El estado `Death` de `AC_Polygon_Masculine` no tenía clip: el cuerpo se congelaba en su pose durante el retardo.

**Por qué no entraba.** No había clip de muerte en el proyecto.

**Para cambiarlo.** Se regenera el clip en `blender-tools/conquest-anims` y se copia el FBX encima: el `.meta` guarda la
importación. Si cambia su duración, hay que ajustar `DefaultDeathVisualHideDelay`.

## D2. El cuerpo oculto seguía teniendo colisión y agente de NavMesh
**Hecho (2026-10-27).** La primera versión solo apagaba los `Renderer`. A petición del usuario, ahora el visual sale de
la jerarquía y se vuelve a crear al reaparecer. Ya no queda `CharacterController`, hitbox ni `NavMeshAgent` del muerto.

## D3. Renderers que aparecían mientras el cuerpo estaba oculto
**Hecho (2026-10-27).** Ya no aplica: mientras el héroe está retirado, no existe su visual.

## D4. Otros controladores de héroe sin los parámetros de muerte
**Qué es.** Solo `AC_Polygon_Masculine` (el de `ModularHeroVisual`) tiene `TriggerDeath`, `IsDead` y `Death`. Otro
controlador de héroe no avisa ni falla, pero no anima la muerte; la retirada y la reaparición sí funcionan.

**Cómo abordarlo.** Repetir en ese controlador los dos parámetros y el estado (el procedimiento está en
`tasks/lessons.md`, apartado "Presentación de la muerte del héroe").

## D5. Coste de recrear el visual al reaparecer
**Qué es.** Reaparecer instancia el prefab otra vez. `HeroVisualAppearanceSystem` busca el GameObject con
`FindObjectsOfType<GameObject>()` y reaplica avatar y equipo. Con un héroe local que muere de vez en cuando no se nota,
pero puede dar un pico de un frame.

**Cómo abordarlo.** Medirlo en el perfil de batalla completa. Si se nota, guardar el visual desactivado en un pool por
entidad en vez de destruirlo, y buscar por `VisualSyncUtility.TryGetVisual` en vez de recorrer todos los GameObjects.
