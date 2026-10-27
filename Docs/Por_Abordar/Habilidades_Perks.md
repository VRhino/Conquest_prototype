# Habilidades y perks: lo que queda fuera

Fecha: 2026-10-22. Sale del cierre de la Fase 2 de `Docs/Design/Aprendizajes_Referencia.md` (lanzador de habilidades y
perks; plan en `tasks/todo.md`). La mecánica implementada está en `Docs/Mechanics/SkillsAndPerks.md`.

## Resumen
| # | Tema | Prioridad | Estado |
|---|---|---|---|
| H1 | La IA no usa habilidades ni perks | Media | **Hecho** (2026-10-27, bloque 3; resto en H12) |
| H2 | Animación propia de cada habilidad | Media | Bloqueado (animaciones; sesión anim-posturas) |
| H3 | Proyectil de las habilidades del arco del héroe | Media | Pendiente |
| H4 | Efectos que faltan: silencio, zonas persistentes, daño por contacto | Baja | Pendiente |
| H5 | Selección de perks (árbol de talentos y su UI) | Media | **Hecho** el panel (2026-10-27); el árbol y sus puntos, pendientes |
| H6 | Huecos de los perks activos en el HUD | Baja | Pendiente |
| H7 | Enfriamiento del cambio de escuadra como grupo | Baja | Pendiente |
| H8 | Nombres de los perks activos dicen "(Passive)" | Baja | Pendiente (dato) |
| H9 | `HUDController.OnDestroy` lanza al salir de Play | Baja | Pendiente (anterior a la fase) |
| H10 | Habilidades de escuadra en el HUD | Media | Pendiente |
| H11 | Teclas de las habilidades de escuadra (GDD 1/2/3, hoy 3/4/5) | Media | Pendiente (decisión del usuario) |
| H12 | Lo que queda de H1 y L4-08 | Baja | Pendiente |
| H13 | El enfriamiento de la escuadra se pierde al cambiarla | Baja | Pendiente |
| H14 | Perks con las condiciones nuevas (L2-15) | Baja | Pendiente (datos) |

Bloque 3 (2026-10-27, `tasks/todo.md`): L2-9 (habilidades de escuadra), L2-15 (condiciones ricas), H5 (panel) y H1 con
L4-08. La mecánica está en `Docs/Mechanics/SkillsAndPerks.md` §3.6, §5 y §8.

---

## H1. La IA no usa habilidades ni perks
**Hecho (2026-10-27).** `HeroAISkillSystem` decide en cada pensamiento qué habilidad de clase y qué habilidad de escuadra
lanzar, con reglas puras (`HeroAISkills`). Solo mira las habilidades que la puerta dejaría pasar (enfriamiento y aguante).
Elige el aliado al que apoyar (L4-08). Ver `SkillsAndPerks.md` §8. Los perks de loadout de la IA siguen sin existir: la
IA no tiene loadout. Lo que queda, en H12.

**Qué era.** `HeroSkillRequestSystem` solo pide el ataque básico para los héroes IA (`HeroAIDecision.shouldAttack`). La puerta,
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
**Hecho el panel (2026-10-27).**
- **Qué hay:** tecla K en el feudo → `PerkSelectionPanel`. Es un prefab uGUI en `Resources/UI` que registra
  `FullscreenPanelManager`; la escena no se tocó. Escribe y guarda `perkIDs` del loadout activo dentro de 5 + 2
  (`PerkSelection`, con tests EditMode y uno de PlayMode).
- **Falta:**
  - el árbol de 5 ramas con puntos (`HeroData.perkPoints` existe, nada lo gasta);
  - un botón en el HUD del feudo, que pide tocar `FeudoScene`;
  - revisar el aspecto del panel en pantalla: el test lo comprueba por código, nadie lo ha mirado en el juego.
- **Datos:** hoy cada clase tiene 2 perks desbloqueados de serie, y nada rellena `unlockedPerks`.

**Qué era.** El GDD (6.2) pide un árbol de talentos de 5 ramas con puntos por nivel, y 5 pasivos + 2 activos por loadout. El
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

## H10. Habilidades de escuadra en el HUD
El GDD (§4.6 y §12.3) pide lanzarlas desde el HUD y verlas con su enfriamiento. Hoy solo se lanzan con las teclas 3, 4
y 5, y el HUD no las muestra. **Cómo abordarlo:**
- llevar a `UIHeroBattleDataComponent` la fracción de enfriamiento de la escuadra activa
  (`CooldownUtility.Fraction` sobre los grupos de la escuadra);
- añadir huecos con icono (`AbilityData.icon`) en `HUDController`; va junto con H6.

## H11. Teclas de las habilidades de escuadra
El GDD §12.3 pone las habilidades de escuadra en 1/2/3 (y en el menú radial `ALT`). La Fase 2 dio 1 y 2 a los perks
activos, así que de momento van en 3/4/5 (`HeroInputSystem`). **Decisión del usuario:** dejar 3/4/5 o mover los perks
activos a otras teclas. Cambiarlo es tocar solo `HeroInputSystem` y la tabla de `SkillsAndPerks.md` §2.

## H12. Lo que queda de H1 y L4-08
- La IA no se acerca al aliado elegido. Solo lanza la habilidad de apoyo si el aliado ya está a su alcance; mover al
  héroe es cosa del comportamiento (Rusher o Balanced).
- Las ofensivas cuentan enemigos en un círculo alrededor del héroe. No giran el cono hacia el grupo.
- La IA pide la primera habilidad de escuadra lista mientras la escuadra combate, sin mirar su propósito ni el alcance
  desde la escuadra.
- No usa el motivo de rechazo de `SkillGate`. Mira las mismas entradas (enfriamiento y aguante) antes de pedir, y si la
  fase actual no se puede interrumpir, lo reintenta en el siguiente pensamiento.

## H13. El enfriamiento de la escuadra se pierde al cambiarla
Los grupos de enfriamiento viven en la entidad de la escuadra. Al cambiarla en un punto de suministro y volver a sacarla,
la escuadra reaparece con los grupos vacíos. Si molesta en juego, guardar los grupos en `InactiveSquadElement` al
retirarla y restaurarlos al aparecer, junto con H7.

## H14. Perks con las condiciones nuevas (L2-15)
Las condiciones ricas (probabilidad, tipo y clase del objetivo, efectos activos, escuadra restante) están implementadas y
probadas, pero ningún perk las usa todavía. Son datos: por ejemplo, "Carga Sanguinaria" (GDD 6.2) con probabilidad, o un
perk de "última resistencia" con `squadAliveMax`.
