# Combate: lo que queda fuera

Fecha: 2026-11-03. Sale del cierre de los puntos de combate del catálogo de la referencia (L1-02, L1-07, L1-08, L1-10 a
L1-14; `Docs/Design/Aprendizajes_Referencia.md` §4.1; plan en `tasks/todo.md`). La mecánica está en
`Docs/Mechanics/DamageMechanics.md` §4.2, §5.5, §5.6, §7.4 y §7.5.

## Resumen
| # | Tema | Prioridad | Estado |
|---|---|---|---|
| C1 | Valores de los rasgos del golpe en los datos de cada arma | Media | **Hecho** (2026-11-03, tabla aprobada) |
| C2 | Terreno de los mapas y modificadores de cada tipo de escuadra | Media | **Hecho** (2026-11-03, tabla aprobada) |
| C3 | Datos de modificadores condicionados y perks que los den | Media | Pendiente (datos) |
| C4 | Reacciones al golpe por tipo y clase de objetivo (L1-09) | Media | **Hecho** (2026-11-10, cuatro clips y capa `Hit Reaction`) |
| C5 | Variación del daño por habilidad | Baja | Pendiente |
| C6 | Carga en habilidades y "golpe de pasada" | Baja | Pendiente (cuando haya caballería) |
| C7 | Señal visual del robo de vida y de la protección al reaparecer | Baja | Pendiente |
| C8 | Suelo de vida de soldados y de héroes remotos | Baja | Pendiente (sin caso de uso) |
| C9 | Valores de resiliencia por escuadra, clase y arma (L1-16) | Alta | **Hecho** (2026-11-06, tabla aprobada) |
| C10 | Señal visual del aturdimiento y de flanco o emboscada | Media | **Hecho** (2026-11-06, texto flotante) |
| C11 | El aturdimiento no corta la habilidad del héroe que ya está en curso | Baja | **No era así** (2026-11-06, ya la corta; test añadido) |
| C12 | Demasiados textos flotantes en batallas de muchas escuadras | Media | **Hecho** (2026-11-07, los del jugador y lo importante) |
| C13 | El héroe derribado o despedido no se desplaza | Baja | Pendiente |
| C14 | Qué armas y habilidades derriban o despiden | Media | Pendiente (datos, aprobar tabla) |
| C15 | Reacción según la dirección del golpe (de espaldas cae de frente) | Baja | Pendiente |
| C16 | Escala de vida del héroe frente al daño | Alta | **Hecho** (2026-11-11: vida base ×10) |

---

## C1. Valores de los rasgos del golpe
**Hecho (2026-11-03).** El usuario aprobó la tabla de `Docs/Mechanics/DamageMechanics.md` §5.5 y está en los assets
(`Resources/Squads/*` y `Resources/Data/HeroClasses/*`). S1, S6 y S9 siguen en verde. Quedan dos efectos a vigilar:
- el escudo del héroe (unos 100 de bloqueo) se rompe casi de un golpe de soldado con o sin desgaste extra;
- los rasgos del héroe apenas se notan contra escuadras, porque su daño casi no pasa la defensa (P3 en
  `Pila_Modificadores.md`).

**Qué es.** `HitTraits` (ignorar el escudo, desgaste del bloqueo y del aguante, variación, carga) está en
`SquadMeleeData`, `SquadRangedData` y `HeroClassDefinition`, pero ningún asset lo rellena: todo vale 0, que es neutro.
La mecánica funciona y está probada; los números son balance.

**A considerar.** Ideas para la identidad de cada arma, para que el usuario las valide:
- Lanceros y piqueros: carga (umbral de unos metros, bono moderado).
- Mandobles y mazas: `blockDamageRatio` positivo (abren escudos) y algo de `staminaDamageRatio`.
- Arco: `ignoresShield` no (el escudo debe servir contra flechas); alguna variación.
- Variación baja en general (≤ 0,1) para que el daño no parezca arbitrario.

**Cómo abordarlo.** Fijar una tabla por arma, rellenar los assets y medir con S6 y S9 que el choque de líneas no cambia
más de lo querido.

## C2. Terreno de los mapas
**Hecho (2026-11-03).** `DefaultMap` es `Plains` (neutra) y cada tipo de escuadra declara sus modificadores para
Bosque, Montaña y Nieve (tabla en `Docs/Mechanics/DamageMechanics.md` §4.2). Con un solo mapa no cambia ninguna batalla:
los datos quedan listos para el segundo mapa. Los héroes siguen sin modificadores de terreno.

**Qué es.** `MapDataSO.terrain` existe, pero ningún mapa lo declara (vale `Any`) y ningún `SquadData` tiene
`terrainModifiers`. Los tipos de terreno (`Plains`, `Forest`, `Mountain`, `Snow`) son una propuesta.

**A considerar.**
- La lista de terrenos es un enum de un byte: solo se añade al final.
- Los héroes no reciben modificadores de terreno (el catálogo habla del tipo de unidad). Si se quiere, la clase del héroe
  podría declararlos igual.
- El enum antiguo `TerrainType` (Abierto, Estrecho, Escalera, Puerta) sigue sin escritor y no tiene que ver con esto.

## C3. Datos de modificadores condicionados
**Qué es.** La mecánica (`ConditionalModifier`, `HitCondition`) y dos fuentes (`SquadData.conditionalModifiers` y el
evento de habilidad `ApplyConditional`) están hechas, pero sin datos. Los perks pasivos (`PerkEffect`) solo dan
modificadores normales.

**A considerar.**
- El caso de libro ("+X % contra caballería") necesita caballería: hoy `UnitType.Cavalry` no tiene escuadras.
- Una habilidad aplica un solo condicionado: relanzarla lo refresca por id de habilidad. Si una necesita dos, hay que
  distinguirlos por evento.
- Los perks podrían dar condicionados añadiendo un `ConditionalDamage` opcional a `PerkEffect`.

## C4. Reacciones al golpe (L1-09)
**Hecho (2026-11-10).** `DamageMechanics.md` §5.10:
- Cuando la resiliencia se rompe, el golpe provoca su reacción (`HitTraits.reaction` o `SkillEvent.reaction`):
  retroceso, derribo o despedido.
- Un escudo roto provoca rotura de guardia.
- Permiso por clase (`reactionLimitedOn`) y stat `ControlResistance`.
- Hay cuatro clips Humanoid hechos en Blender (`Assets/Art/Animations/Humanoid/Hit_*`) y la capa `Hit Reaction` en los
  cinco controladores.
- El aturdimiento dura lo que su clip, y el derribo y el despedido empujan más al soldado.
- Quedan C13 a C15.

**Qué era.** Bloqueado: necesitaba animaciones de reacción (retroceso, derribo, despedido, rotura de guardia). Lo que propone el
catálogo: un enum `HitReaction` por golpe, con permiso por clase de objetivo (soldado, héroe, grande) y un stat de
resistencia al control que lo anula. Hoy solo hay retroceso del cuerpo (`HitKnockback`) y el aturdimiento al romperse el
escudo. Encaja como un rasgo más de `HitTraits` y una etapa de `DamageCalculationSystem` que publique la reacción.
Desde el 2026-11-06 la resiliencia (L1-16, `DamageMechanics.md` §5.8) decide cuándo un golpe provoca reacción: hoy es un
aturdimiento sin animación propia; con las animaciones, la rotura podría elegir la reacción según el golpe.

## C5. Variación por habilidad
**Qué es.** El catálogo pide la variación "por arma o habilidad". Hoy es del arma: un daño en área de una habilidad usa
la variación del arma de quien la lanza. Para que una habilidad declare la suya bastaría un campo en `SkillEvent` que, si
es mayor que 0, sustituya al del perfil.

## C6. Carga en habilidades y golpe de pasada
**Qué es.** Solo la melé mide la carga. Una embestida (`Dash`) seguida de un área no la cuenta. La referencia mide cuánto
ha rebasado el atacante al objetivo (golpe de pasada); se descartó por decisión del usuario, pero podría volver como modo
del arma si llega la caballería.

## C7. Señal visual
**Qué es.** El robo de vida y la protección al reaparecer no tienen texto flotante ni efecto. Si hace falta, una categoría
`Heal` en `DamageCategory` y un efecto en el héroe protegido.

## C8. Suelo de vida fuera del héroe local
**Qué es.** `MinHealthRatio` solo se escribe en `HeroHealthComponent`. Los soldados no lo tienen, y los héroes remotos y
de IA no reaparecen (no hay contrato de reaparición), así que no reciben la protección.

## C9. Valores de resiliencia (L1-16)
**Hecho (2026-11-06).** El usuario aprobó la tabla de `Docs/Mechanics/DamageMechanics.md` §5.8 y está en los assets
(`Resources/Squads/*` y `Resources/Data/HeroClasses/*`).

**Qué era.** La mecánica está (`DamageMechanics.md` §5.8), pero `SquadData.baseResilience`,
`HeroClassDefinition.baseResilience` y `HitTraits.resilienceDamage` valen 0 en todos los assets: nadie se aturde. Hace
falta una tabla por escuadra, clase y arma, aprobada por el usuario, y medir con S6 que el choque de líneas no se rompe.

## C10. Señal visual
**Hecho (2026-11-06).** Cuatro categorías de texto flotante (`DamageMechanics.md` §12): "Flanco", "Espalda" y
"Emboscada" con el número, y "Aturdido" al romperse la resiliencia. No hay efecto en el cuerpo ni animación (L1-09, C4).

**Qué era.** El aturdimiento por resiliencia y los golpes de flanco, espalda o emboscada no tienen texto flotante ni
efecto. Una categoría más en `DamageCategory` necesita su entrada en el asset de `FCTCategoryConfig`.

## C11. Aturdimiento y habilidad en curso
**Cerrado (2026-11-06): ya la cortaba.** `SkillExecutionSystem` termina la habilidad en curso y la aparcada cuando el
héroe está inmovilizado (L2-4), y la rotura de resiliencia lo inmoviliza. El test `AResilienceBreakEndsTheRunningSkill`
prueba la cadena entera (golpe, rotura, stat resuelto, habilidad terminada). La nota de abajo era un error de lectura.

**Lo que se creía.** Con la velocidad a 0, `SkillGate` no deja lanzar, pero la habilidad que el héroe ya estaba ejecutando sigue
hasta el final. Los soldados sí pierden el golpe en curso. Si se quiere que el aturdimiento interrumpa, la interrupción
de habilidades (L2-4) ya existe; faltaría pedirla desde la rotura.

## C12. Textos flotantes a escala NvN
**Hecho (2026-11-07).** Se ven los golpes que da o recibe el jugador (héroe, escuadras y soldados); del resto, solo
muerte, escudo roto y aturdido; uno por golpe con un tope de 30 a la vez (`DamageMechanics.md` §12, decisión del
usuario). Test `FloatingTextRelevanceTests`.

**Qué era.** Cada golpe de todo el campo creaba su texto: con 26 o más soldados por bando se amontonaban.

## C13. El héroe derribado o despedido no se desplaza
**Qué es.** Los clips de reacción van en el sitio. A un soldado lo desplaza el empujón de ECS (`UnitHitKnockbackSystem`),
pero el héroe no tiene empujón: su cuerpo lo mueve `LocalHeroCharacterMotor`, y en los remotos el agente. Un héroe
despedido sale volando y cae donde estaba.

**Cómo abordarlo.** Una velocidad de empujón en el motor local, con la misma curva (`HitKnockback`) y la distancia de
`HitReactions.Push`, y `agent.Move` en los remotos, como hacen los soldados.

## C14. Qué armas y habilidades derriban o despiden
**Qué es.** El mecanismo está, pero ningún dato lo usa: toda rotura de resiliencia es un retroceso. Hace falta una tabla
aprobada por el usuario, como la de C9. Propuesta para empezar:
- Mandoble del héroe: derribo.
- Piqueros: derribo, que en héroes se queda en retroceso.
- Habilidades de embestida o golpe de escudo: despedido o derribo.
- Una habilidad definitiva con `ControlResistance` 3 mientras dura.

**Cómo abordarlo.** Rellenar `reaction` y `reactionLimitedOn` en el `HitTraits` de cada arma
(`Resources/Squads/*`, `Resources/Data/HeroClasses/*`) y en los eventos de las habilidades, y medir con S6 que el choque
de líneas no se rompe.

## C15. Reacción según la dirección del golpe
**Qué es.** Los clips suponen un golpe de frente: el cuerpo cae o retrocede hacia atrás. Un golpe por la espalda empuja
hacia delante (el empujón va en contra del atacante), pero el clip cae hacia atrás.

**Cómo abordarlo.** Variantes "de espaldas" de los cuatro clips y un parámetro más (`HitDirection` ya está en el golpe,
§5.7). También se pueden girar los estados, si la variante no compensa.

## C16. Escala de vida del héroe
**Qué era.** El test `BattleSceneSpawnsAndAcceptsHeroMovementAndSquadOrders` fallaba de forma intermitente: "W input did
not move the local hero by 3m". La W funcionaba; el héroe estaba muerto.
- **La causa.** Desde que la IA usa habilidades (H1), el héroe enemigo con arco lanza Focused Shot (`Bow1`), que es un cono
  instantáneo de 25 m y ×1,8.
  - Su perfil de daño suma 125, así que el disparo hace unos 225.
  - El héroe local tenía 146 de vida (`baseHealth` 100, el valor por defecto de `HeroClassDefinition`). Moría de un
    disparo a los 7 s de batalla en cuanto avanzaba hacia el enemigo.
  - Un solo soldado tiene 1000-1600 de vida y un golpe de lancero hace 100: el héroe era unas diez veces más frágil que
    un soldado.
- **Hecho (2026-11-11, decisión del usuario: opción A).** En las cuatro clases (`Resources/Data/HeroClasses`):
  - `baseHealth` pasa de 100 a 1000 y `healthPerVitality` se multiplica por 10. Arco 20, lanza 30, espada y escudo 100,
    mandoble 70.
  - Un héroe de arco de nivel 1 tiene unos 1460 de vida: Focused Shot le quita un 15 %, un golpe de lancero un 7 % y hacen
    falta unos 12 ataques básicos de héroe para matarlo.
  - Se mantienen las diferencias entre clases, así que espada y escudo es la más resistente (unos 3300 con la misma
    vitalidad).
  - El test vuelve a pasar sin cambiarlo. Si vence el plazo, ahora vuelca la entrada, la intención, el motor, la vida, los
    héroes de IA y las escuadras.
- **Queda abierto:** H3 (que Focused Shot sea una flecha real, que puede fallar) y equilibrar el daño de héroe contra
  héroe con datos reales.
