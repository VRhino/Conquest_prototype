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
| H6 | Huecos de los perks activos en el HUD | Baja | **Hecho** (2026-10-27, barra de habilidades) |
| H7 | Enfriamiento del cambio de escuadra como grupo | Baja | **Hecho** (2026-10-27, bloque 4) |
| H8 | Nombres de los perks activos dicen "(Passive)" | Baja | **Hecho** (2026-10-27, bloque 4) |
| H9 | `HUDController.OnDestroy` lanza al salir de Play | Baja | **Hecho** (2026-10-27, bloque 4) |
| H10 | Habilidades de escuadra en el HUD | Media | **Hecho** (2026-11-02: se ven y se lanzan con clic y con el radial de `ALT`) |
| H11 | Teclas de las habilidades de escuadra (GDD 1/2/3, hoy 3/4/5) | Media | **Hecho** (2026-10-27: escuadra 1/2/3, perks 4/5) |
| H12 | Lo que queda de H1 y L4-08 | Baja | Pendiente |
| H13 | El enfriamiento de la escuadra se pierde al cambiarla | Baja | **Hecho** (2026-10-27, bloque 4) |
| H14 | Perks con las condiciones nuevas (L2-15) | Baja | Pendiente (datos) |
| H15 | Lo que dejan abierto los efectos L2-10 a L2-14 | Baja | Pendiente (arqueros y Defensa Reforzada) |
| H16 | Combo y bajas del héroe en el HUD (L2-16) | Media | Pendiente (HUD; sesión hud-radial) |

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
**Hecho (2026-10-27).** La barra de habilidades (`SkillBarController`, prefab en `Resources/UI/SkillBar`) muestra los
nueve huecos con su icono y enfriamiento; ver `Docs/Mechanics/SkillsAndPerks.md` §6. Los cuatro huecos antiguos de
`HUDController` no estaban conectados en ninguna escena y se quitaron. El enfriamiento del cambio de escuadra (H7) sigue
viéndose solo en el panel del punto de suministro.

`UIHeroBattleDataComponent.skillCooldown` solo lleva Q, E, R y F. Ampliarlo a los huecos 1 y 2 y añadir sus iconos.

## H7. Enfriamiento del cambio de escuadra
**Hecho (2026-10-27).** El cambio usa el grupo `SquadSwapCooldown.Group` (1000) del `CooldownGroupElement` del héroe,
10 s sin escalar por `CooldownScale`. Se borraron `SquadSwapCooldownComponent` y `SquadSwapCooldownSystem`; `SquadSwapSystem`
y `SupplyPointUIController` leen el grupo. Mostrarlo en el HUD va con H6/H10.

`SquadSwapCooldownComponent` sigue con su propio temporizador por frame. Pasarlo a un `CooldownGroupElement` del héroe lo
uniría con el resto (L2-5) y con la HUD.

## H8. Nombres de los perks activos
**Hecho (2026-10-27).** Los tres assets ya no llevan "(Passive)" en `perkName`.

Orden de Resistencia, Carga Coordinada y Flecha Marcadora tienen `isPassive = false` pero su `perkName` termina en
"(Passive)". Es un dato de los assets.

## H9. `HUDController.OnDestroy` al salir de Play
**Hecho (2026-10-27).** `OnDestroy` no libera nada si el mundo ya no está creado.

Al salir de Play, `HUDController.OnDestroy` libera consultas de un mundo ya destruido y lanza `NullReferenceException`. Ya
pasaba antes de esta fase. Basta con comprobar que el mundo sigue creado antes de liberar.

## H10. Habilidades de escuadra en el HUD
**Hecho (2026-11-02).** Se ven en la barra con su enfriamiento y se lanzan con 1/2/3, con clic y con el menú radial.
Decisiones del usuario: `ALT` se mantiene, no se alterna; sin pausa; el radial lleva habilidades de escuadra y formaciones,
y se elige con clic o soltando `ALT` sobre un sector; la barra solo admite clics con `ALT` mantenido, porque en batalla
el cursor va bloqueado. Solo los huecos de escuadra reciben clics (GDD §4.6). Ver `Docs/Mechanics/SkillsAndPerks.md` §6.1.

Queda fuera, porque son datos: las habilidades de escuadra no tienen icono (`AbilityData.icon` vacío en sus seis assets), así
que barra y radial enseñan la tecla, y el radial, además, el nombre.

## H11. Teclas de las habilidades de escuadra
**Hecho (2026-10-27).** Decisión del usuario: la escuadra en 1/2/3 (como el GDD) y los perks activos en 4/5. En la
barra de habilidades la escuadra va delante de los perks para que se lea 1-2-3-4-5.

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
**Hecho (2026-10-27).** `InactiveSquadElement.cooldowns` guarda los grupos aún activos (tiempo absoluto) al retirar la
escuadra, por cambio o por muerte del dueño, y `SquadSpawningSystem` los restaura al sacarla de nuevo.

Los grupos de enfriamiento viven en la entidad de la escuadra. Al cambiarla en un punto de suministro y volver a sacarla,
la escuadra reaparece con los grupos vacíos. Si molesta en juego, guardar los grupos en `InactiveSquadElement` al
retirarla y restaurarlos al aparecer, junto con H7.

## H15. Lo que dejan abierto los efectos L2-10 a L2-14
- **Datos.** Hecho (2026-11-04, tabla aprobada por el usuario; `SkillsAndPerks.md` §4.1 y §5): topes en Corte
  Giratorio, Lluvia de Flechas, Barrido Largo y Muro de Púas; la quemadura de Flecha Llameante apila hasta 2; Corte
  Giratorio, Barrido Largo y Lluvia de Acero siguen la velocidad de ataque; Juicio de Acero da +15 % de velocidad de
  ataque a la escuadra; Línea Inquebrantable y Flecha Marcadora dan `EffectDurationIn` y `EffectDurationOut`.
  `SkillCatalogueDataTests` los vigila. Queda **Defensa Reforzada ligada a la habilidad**: para que su defensa dure
  "mientras se sostiene el escudo" la habilidad tendría que durar 8 s, y hoy el héroe se quedaría todo ese tiempo en la
  animación de ataque. Espera a su animación propia (H2). Con 60 s de enfriamiento y 2 s de quemadura, la pila de
  Flecha Llameante solo se notará si algo alarga la quemadura.
- **Velocidad de ataque.** Hecho (2026-11-04) para las habilidades de escuadra (media de sus soldados vivos), el ataque
  cuerpo a cuerpo de los soldados (golpe y pausa) y los estados de ataque de los controladores de soldados. Una prueba
  PlayMode comprueba en la escena de batalla que el héroe y un escudero animan su ataque al doble. Queda: el disparo y la
  recarga de los arqueros (`RangedAttackSystem`) no la usan, y cuando cada habilidad tenga su animación (H2) habrá que
  marcar también sus estados.
- **Efectos ligados y aparcada.** Hecho (2026-11-04): la aparcada vuelve con su id y los ids salen de un contador de
  lanzamientos propio (`SkillInstanceComponent.launches`).
- **Tope por efectos.** Hecho (2026-11-04): stat `MaxTargets` del lanzador sobre el tope de cada evento
  (`SkillEvents.Cap`).

## H14. Perks con las condiciones nuevas (L2-15)
Las condiciones ricas (probabilidad, tipo y clase del objetivo, efectos activos, escuadra restante) están implementadas y
probadas, pero ningún perk las usa todavía. Son datos: por ejemplo, "Carga Sanguinaria" (GDD 6.2) con probabilidad, o un
perk de "última resistencia" con `squadAliveMax`.

## H16. Combo y bajas en el HUD (L2-16)
El héroe gana combo y bajas y las definitivas cuestan 20 de combo (`Docs/Mechanics/SkillsAndPerks.md` §3.1), pero el HUD
no lo enseña: el jugador no sabe cuánto lleva ni por qué la definitiva no sale. La puerta ya devuelve `NotEnoughCombo` y
`NotEnoughKills`. Por decisión del usuario se deja para el HUD (sesión hud-radial), para no chocar con su trabajo:
- un contador de combo junto a la casilla de la definitiva, que se vacía al gastarlo;
- la casilla apagada mientras falte combo, como con el enfriamiento;
- leer `HeroResources.Combo` en cada refresco del HUD (la pérdida se calcula al leer).
