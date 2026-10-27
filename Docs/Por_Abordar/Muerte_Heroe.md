# Muerte del héroe: lo que queda fuera

Fecha: 2026-10-27. Sale del plan de presentación de la muerte del héroe (`tasks/todo.md`): animación de muerte,
ocultar el cuerpo tras un retardo y volver a mostrarlo al reaparecer.

## Qué hay
- `HeroDeathPresentationSystem` (único escritor de `HeroDeathPresentationComponent`, después de `HeroSpawnSystem`) sube
  `deathRevision` en cada muerte, pone `isDead`, oculta el visual cuando pasan
  `HeroGameplayConfigComponent.deathVisualHideDelay` segundos (por defecto `DefaultDeathVisualHideDelay`, 3 s, en
  `HeroGameplayConfigAuthoring`) y lo muestra cuando el héroe está vivo y `HeroSpawnSystem` ha publicado la pose.
- `HeroDeathAnimationBridge` (lo usan `EntityVisualSync` para el local y `RemoteHeroAnimationDriver` para remotos e IA)
  dispara `TriggerDeath` una vez por muerte y mantiene `IsDead`. No escribe en ECS. Si el Animator no tiene esos
  parámetros, no hace nada.
- `HeroDeathVisualPresenter` apaga los `Renderer` encendidos mientras `visualHidden` y los vuelve a encender en
  `LateUpdate`, después de que el motor local haya aplicado la pose de reaparición.
- `AC_Polygon_Masculine` tiene `TriggerDeath` (Trigger), `IsDead` (Bool) y un estado `Death` sin clip (Any State →
  `Death` con `TriggerDeath`; `Death` → salida cuando `IsDead` es falso). Sin clip y sin escribir valores por defecto,
  el cuerpo se queda congelado en la pose en que murió.
- Tests: `HeroLifecycleRegressionTests.DeathPresentation*` (EditMode) y, en PlayMode,
  `BattleSceneIntegrationTests.BattleSceneAppliesDamageRemovesDeadUnitAndRespawnsHero` (local: morir → animar →
  oculto → reaparecer visible en la pose de spawn) y `BattleSceneRemoteHeroDeathAnimatesThenHidesTheBody` (remoto).

## Resumen
| # | Tema | Prioridad | Estado |
|---|---|---|---|
| D1 | Clip real de la animación de muerte | Media | Bloqueado (animación) |
| D2 | El cuerpo oculto sigue teniendo colisión y agente de NavMesh | Media | Pendiente |
| D3 | Renderers que aparecen mientras el cuerpo está oculto | Baja | Pendiente |
| D4 | Otros controladores de héroe sin los parámetros de muerte | Baja | Pendiente |

---

## D1. Clip real de la animación de muerte
**Qué es.** El estado `Death` de `AC_Polygon_Masculine` no tiene clip: el cuerpo se congela en su pose durante el retardo.

**Por qué no entra.** No hay clip de muerte en el proyecto.

**Cómo abordarlo.** Asignar el clip a `Death.motion` (sin bucle), activar `Write Defaults` solo si el clip lo pide y
ajustar `deathVisualHideDelay` en `HeroGameplayConfigAuthoring` a lo que dure el clip más la pausa que se quiera. No hay
que tocar código. Si el clip tiene desplazamiento de raíz, revisar que `applyRootMotion` siga apagado en el visual.

## D2. El cuerpo oculto sigue teniendo colisión y agente de NavMesh
**Qué es.** Solo se apagan los `Renderer`. El `CharacterController` del local y el `NavMeshAgent` de los remotos siguen
activos, así que un remoto muerto (que no reaparece) sigue ocupando sitio para la evitación de los agentes, y sus
hitboxes siguen en la escena.

**Por qué no entra.** El encargo era el visual; apagar el agente o la colisión toca el movimiento y su prueba propia.

**Cómo abordarlo.** Que el presentador (u otra pieza de la capa visual que solo lea) desactive la evitación del
`NavMeshAgent` (`obstacleAvoidanceType = NoObstacleAvoidance`) y las capas de colisión del cuerpo mientras `visualHidden`.
Comprobar con S1-S7 que las escuadras ya no rodean un cuerpo invisible.

## D3. Renderers que aparecen mientras el cuerpo está oculto
**Qué es.** El presentador recuerda los renderers que apagó. Si mientras está oculto se cambia el equipo
(`HeroVisualEquipmentInitializer`) o aparece un efecto hijo, ese renderer nuevo se verá.

**Cómo abordarlo.** Si pasa en la práctica, volver a recorrer los renderers mientras `visualHidden` cuando cambie el
número de hijos, o hacer que el equipo consulte el presentador antes de encender.

## D4. Otros controladores de héroe sin los parámetros de muerte
**Qué es.** Solo `AC_Polygon_Masculine` (el de `ModularHeroVisual`) tiene `TriggerDeath`, `IsDead` y `Death`. Otro
controlador de héroe no avisa ni falla, pero no anima la muerte; el ocultado y la reaparición sí funcionan.

**Cómo abordarlo.** Repetir en ese controlador los dos parámetros y el estado (el script de la sesión está descrito en
`tasks/lessons.md`).
