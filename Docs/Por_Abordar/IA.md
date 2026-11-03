# IA: lo que queda fuera

Fecha: 2026-11-03. Sale del bloque de IA del catálogo de la referencia (L4-03, L4-05, L4-06, L4-07, L4-10;
`Docs/Design/Aprendizajes_Referencia.md` §4.4; plan en `tasks/todo.md`). La mecánica está en
`Docs/Mechanics/DamageMechanics.md` §9.

## Resumen
| # | Tema | Prioridad | Estado |
|---|---|---|---|
| I1 | LOD de IA (L4-07) | Media | Pendiente (medir antes) |
| I2 | Datos: habilidades con `Taunt` y con stats de IA de escuadra | Media | Pendiente (diseño de habilidades) |
| I3 | Visibilidad por soldado y línea de tiro por disparo | Baja | Pendiente (la referencia lo hace por soldado) |
| I4 | La provocación y la visibilidad en los héroes de IA | Media | Pendiente |
| I5 | Grupo de servidor real (L4-06) | Baja | Pendiente (cuando haya red) |

---

## I1. LOD de IA (L4-07)
**Qué es.** El catálogo propone un `AILodSystem` con tres niveles (tope de soldados considerados y frecuencia por
distancia, como la referencia). **Por qué queda fuera.** Un LOD sin medidas es optimizar a ciegas, y hoy no hay escenario
de muchas escuadras ni build de Player para medir 15v15 (decisión del usuario, 2026-11-03).

**Cómo abordarlo.** Primero un escenario PlayMode con 10-15 escuadras por bando que registre el coste por frame de
`EnemyDetectionSystem`, `LineOfSightSystem`, `UnitTargetingSystem`, `SquadAISystem` y la IA de héroe (ya tienen marcadores de
perfil o contadores `Debug*`), y repetirlo en un Player. Con esos datos, decidir qué baja de frecuencia y a qué distancia.
`LineOfSightSystem` ya tiene presupuesto propio (`raysPerFrame`).

## I2. Datos de provocación y de parámetros de IA
**Qué es.** El evento `Taunt` (L4-05) y los stats `AIDetectionRange`, `AICombatRadius` y `AIHeroBias` (L4-10) funcionan,
pero ninguna habilidad los usa. Candidatas: Shield Bash o Muro Imparable para la provocación; una orden de "vigilar" que
amplíe la detección; un perk que reduzca el sesgo hacia héroes.

## I3. Visibilidad por soldado
**Qué es.** La referencia guarda la visibilidad por soldado y enemigo, y sus arqueros comprueban la línea de tiro con un
rayo propio desde el ancla. Aquí es por escuadra y enemigo (decisión del usuario): en el borde de un muro, toda la
escuadra ve o no ve a la vez, y un arquero dispara a un enemigo visible para su escuadra aunque él tenga un muro delante.
**Cómo abordarlo.** Si se nota, un rayo de línea de tiro en `RangedAttackSystem` antes de disparar, con su propio
presupuesto.

## I4. Héroes de IA
**Qué es.** `Taunt` solo afecta a la puntuación de los soldados. La IA de héroe elige objetivo por su cuenta
(`HeroAIPerceptionSystem`), así que ni la provocación ni la visibilidad la cambian, salvo a través de lo que detecta su
escuadra. **Cómo abordarlo.** Leer `AggroBiasElement` y `SquadVisibilityElement` desde la percepción del héroe.

## I5. Grupo de servidor real (L4-06)
**Qué es.** Los sistemas que deciden llevan `[ServerAuthoritative]` y un test vigila que no lean entrada ni presentación,
pero siguen en `SimulationSystemGroup`. Cuando haya red, se filtran a un mundo de servidor por la marca. Meterlos ahora en
un grupo propio cambiaría el orden, y un ciclo de ordenación cuelga el editor (`SystemOrderTests`).
