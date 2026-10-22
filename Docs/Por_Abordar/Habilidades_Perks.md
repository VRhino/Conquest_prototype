# Habilidades y perks: lo que queda fuera

Fecha: 2026-10-22. Sale del cierre de la Fase 2 de `Docs/Design/Aprendizajes_Referencia.md` (lanzador de habilidades y
perks; plan en `tasks/todo.md`). La mecánica implementada está en `Docs/Mechanics/SkillsAndPerks.md`.

## Resumen
| # | Tema | Prioridad | Estado |
|---|---|---|---|
| H1 | La IA no usa habilidades ni perks | Media | Pendiente |
| H2 | Animación propia de cada habilidad | Media | Bloqueado (animaciones; sesión anim-posturas) |
| H3 | Proyectil de las habilidades del arco del héroe | Media | Pendiente |
| H4 | Efectos que faltan: silencio, zonas persistentes, daño por contacto | Baja | Pendiente |
| H5 | Selección de perks (árbol de talentos y su UI) | Media | Pendiente |
| H6 | Huecos de los perks activos en el HUD | Baja | Pendiente |
| H7 | Enfriamiento del cambio de escuadra como grupo | Baja | Pendiente |
| H8 | Nombres de los perks activos dicen "(Passive)" | Baja | Pendiente (dato) |
| H9 | `HUDController.OnDestroy` lanza al salir de Play | Baja | Pendiente (anterior a la fase) |

---

## H1. La IA no usa habilidades ni perks
**Qué es.** `HeroSkillRequestSystem` solo pide el ataque básico para los héroes IA (`HeroAIDecision.shouldAttack`). La puerta,
las fases y los efectos ya les sirven igual que al jugador.

**Cómo abordarlo.** Añadir a `HeroAIDecision` el hueco a lanzar y que los comportamientos (Rusher, Balanced) lo decidan con
reglas sencillas (enemigos en radio, aliados heridos, enfriamiento listo). Conviene usar el motivo de `SkillGate` para no
insistir con lo que está enfriándose.

## H2. Animación propia de cada habilidad
**Qué es.** El evento `Animation` dispara la animación de ataque básica para todas las habilidades. La sesión de animaciones
de lanceros espera las referencias del usuario.

**Cómo abordarlo.** Añadir al evento un identificador de animación (o el `animationTag` del SO, que existe sin uso) y que
`EntityVisualSync` / `RemoteHeroAnimationDriver` lo traduzcan a un trigger del Animator.

## H3. Proyectil de las habilidades del arco del héroe
**Qué es.** Disparo Enfocado, Lluvia de Flechas y Flecha Llameante usan daño en área por delante del héroe, no proyectiles.
Los pools de proyectiles se registran por escuadra de arqueros (`RangedCombatBootstrap`) y el héroe no tiene pool propio.

**Cómo abordarlo.** Registrar un pool y prefab del héroe (en `HeroGameplayConfig` o en el SO de clase) y traducir el evento
`Projectile` a `ProjectileSpawnRequest` en `SkillEffectSystem`, con el esquema de lanzador (L2-7).

## H4. Efectos que faltan
- **Silencio** (Flecha Silenciadora): impedir habilidades al objetivo un tiempo. Podría ser un modificador con un stat nuevo que
  `SkillGate` mire, como el aturdimiento.
- **Zonas persistentes** (Muro de Púas, Zona de Exclusión): una entidad con radio y duración que aplique su efecto a quien
  esté dentro cada cierto tiempo.
- **Daño por contacto** (perk Muro de Púas): daño a quien toque al héroe en formación defensiva.

## H5. Selección de perks
**Qué es.** El GDD (6.2) pide un árbol de talentos de 5 ramas con puntos por nivel, y 5 pasivos + 2 activos por loadout. El
código ya lee `HeroData.loadouts[].perkIDs`, pero nada lo rellena: hoy ningún héroe lleva perks a batalla.

**Cómo abordarlo.** Un panel en el feudo que escriba `perkIDs` del loadout activo entre los `validClassPerks` desbloqueados
(`unlockedPerks`). El árbol y sus puntos, después.

## H6. Perks activos en el HUD
`UIHeroBattleDataComponent.skillCooldown` solo lleva Q, E, R y F. Ampliarlo a los huecos 1 y 2 y añadir sus iconos.

## H7. Enfriamiento del cambio de escuadra
`SquadSwapCooldownComponent` sigue con su propio temporizador por frame. Pasarlo a un `CooldownGroupElement` del héroe lo
uniría con el resto (L2-5) y con la HUD.

## H8. Nombres de los perks activos
Orden de Resistencia, Carga Coordinada y Flecha Marcadora tienen `isPassive = false` pero su `perkName` termina en
"(Passive)". Es un dato de los assets.

## H9. `HUDController.OnDestroy` al salir de Play
Al salir de Play, `HUDController.OnDestroy` libera consultas de un mundo ya destruido y lanza `NullReferenceException`. Ya
pasaba antes de esta fase. Basta con comprobar que el mundo sigue creado antes de liberar.
