# Aprendizajes de la referencia para Conquest Tactics

Estado: propuesta de diseño; las Fases 0, 1 y 2 y los puntos de combate (salvo L1-09) están implementados (§5). Fecha de
la verificación: 2026-10-19.
Ampliación del 2026-10-26: 20 puntos más sacados de las notas de `game-re` que el catálogo no recogía, marcados
"(amp.)" en las tablas de la sección 4. El estado de todo lo pendiente está al final de la sección 5.
Revisión del 2026-11-06: el 3v3 es el prototipo y el objetivo es NvN. Lo que la sección 6 descartaba "por ser un 3v3" se
ha vuelto a valorar; los puntos recuperados están marcados "(NvN)" en la sección 4.
Las propuestas marcadas "(verificado)" tienen su afirmación "Hoy en el proyecto" comprobada leyendo el código citado.

**Prioridad declarada (2026-10-19):** lo primero es la experiencia de las unidades en pantalla: marcha, formación, giro,
separación entre aliados, contacto con el enemigo y distancias. El modelo objetivo, la tabla de parámetros, los cambios
C1-C15 y el plan de validación están en `Docs/Design/Movimiento_Formaciones.md`. La hoja de ruta (§5) pasa a empezar por ahí.

## 1. Propósito y reglas

Este documento reúne mejoras para el proyecto sacadas de comparar siete sistemas (combate, habilidades, formaciones,
IA, movimiento, red, progresión y modos) con un juego de referencia estudiado por ingeniería inversa estática.

Reglas:
- **Solo se transfieren patrones, técnicas y órdenes de magnitud.** Nunca código, pseudocódigo, nombres internos ni
  tablas o valores de diseño de la referencia. Cada propuesta está redactada con palabras propias.
- Cuando la técnica es pública se cita su fuente: RVO2/ORCA (van den Berg et al., Apache-2.0), KCP (MIT),
  cuantización de posiciones y "smallest three" para cuaterniones, árboles de comportamiento y *time slicing*.
- Los números de ancho de banda y de milisegundos son estimaciones de diseño del proyecto; hay que medir antes de fijarlos.
- Todo respeta el modelo híbrido de `CLAUDE.md` y `Docs/ModeloHybrido.md`: la lógica vive en ECS, `EntityVisualSync`
  sincroniza y la capa GameObject solo visualiza; un sistema, una responsabilidad. Donde una propuesta lo tensa se dice.
- Origen de las notas de referencia (por ruta, no enlace): `C:\Users\VRINO\Desarrollo\game-re\notes`. Los informes de
  trabajo están en `C:\Users\VRINO\Desarrollo\game-re\output\l1_findings.md` a `l7_findings.md`.

Convención de IDs: `L1-xx` combate, `L2-x` habilidades, `L3-xx` formaciones, `L4-xx` IA, `L5-x` movimiento,
`l6-xx` red, `L7-x` progresión y modos, `D1` documentación. Los transversales (sección 3) agrupan IDs de varios sistemas.

## 2. Resumen ejecutivo: las 5 mejoras que más aportan

1. **Pila única de modificadores por stat y canal** (L1-01, L7-3, L5-3, L7-4). Hoy no existe ningún buff y los perks
   no tienen consumidor (verificado). Es el prerrequisito de perks, auras, debuffs, aturdimientos y cooldowns escalables;
   sin ella cada sistema inventaría su propio camino de cálculo.
2. **Lanzador de habilidades con puerta única, fases y cooldown por grupo** (L2-1, L2-2, L2-5). El input Q/E/R/F ya
   llega a ECS pero nada lo consume (verificado, con matiz en 4.2). Es la mayor laguna funcional frente al GDD y el
   cuello de botella de combos, cargas y habilidades de escuadra.
3. **Reloj de decisión con presupuesto e histéresis** (L4-01, L4-02). Sustituye `frameCount % 5` por tiempo de
   simulación escalonado por entidad y evita que los bots cambien de rama por un tick de proximidad. Es barato (S) y
   mejora a la vez rendimiento, estabilidad y determinismo.
4. **Evitación recíproca ORCA y formaciones validadas sobre NavMesh** (L5-1, L3-01, L3-02). Las unidades no evitan
   (ni a aliados) y los huecos tras bajas no se rellenan, a pesar de que el doc de mecánicas dice lo contrario
   (ambos verificados). Es lo más visible para el jugador en combate masivo.
5. **Red por entidad de equipo + autoridad desde ya** (l6-01, l6-02, l6-03, l6-07, L7-1, L7-2, L1-03). Replicar 6 héroes
   y 6 escuadras (~30 kbps) en lugar de 900 agentes. Para llegar ahí no hace falta tocar red ahora: hace falta que el reloj
   de partida y el resultado vivan en ECS, que el daño sea una función pura y que el RNG sea sembrado.

Además, por barato y urgente: sincronizar los docs con el código (sección 8).

## 3. Temas transversales

### T1. Pila de modificadores por stat y canal (L1-01, L1-02, L1-04, L1-06, L5-3, L7-3, L7-4, L7-6, L2-5)
Tres informes distintos (combate, movimiento, progresión) piden lo mismo con nombres distintos: un buffer de
modificadores por entidad, una regla de combinación única y un único escritor de los stats derivados. Se unifican:
- **Estructura:** `DynamicBuffer<StatModifier>` con `{statId, operación (Ratio/Flat/Override), valor, canal, fuente, expiresAt}`.
- **Regla de combinación:** `(1 + Σ ratios) * base + Σ planos`; un override, si existe, gana. Los ratios se suman entre
  sí y se multiplican una sola vez (patrón estándar de RPG; evita la explosión multiplicativa).
- **Canales:** Base, Ataque, Defensa, y los condicionados por etiqueta (tipo de daño, melee/rango, clase objetivo) que
  solo evalúa el sistema de daño en el impacto. Se empezó con 3-4 canales; se añaden más cuando un efecto los pida.
- **Un escritor:** `StatResolveSystem` recompone `DefenseComponent`, `PenetrationComponent`, `UnitStatsComponent`,
  velocidad y escala de cooldown desde una base inmutable, y solo cuando una marca `ModifiersDirty` (enableable) lo pide.
- **Un enum `StatId` compartido** que sirve de clave de persistencia (L7-6) y de red. Esto sustituye a `Dictionary<string,float>`.
Consumidores: equipo del héroe (hoy solo el jugador local lo recibe, verificado en L7-3), nivel de escuadra, perks,
aturdimientos y futuros efectos. Va **antes** que perks, escudos absorbentes y crítico por rating.

### T2. Reloj de decisión con presupuesto (L4-01, L4-07, L2-5, L3-05)
El mismo patrón aparece en IA (cadencia ~10 Hz escalonada por hash del `Entity`, con tope de pensamientos por frame y
cursor persistente), en cooldowns (tiempos absolutos en lugar de decrementar por frame) y en órdenes temporales de IA
(`expiresAt`). Regla común: **todo temporizador de lógica usa tiempo de simulación (`SystemAPI.Time`), nunca
`Time.frameCount`**, y el trabajo caro se reparte en fases por entidad. Es *time slicing*, técnica estándar de IA de juegos.
Las histéresis (L4-02) y los cooldowns usan el mismo `dt` acumulado.

### T3. Documentación desincronizada con el código (D1, L3-02, L7-2, L7-7, L6)
Cinco documentos contradicen al código (lista completa en la sección 8). La causa común es que el doc se escribió como
diseño y nadie lo ha reconciliado. Propuesta de proceso: cada doc de `Docs/Mechanics` lleva una tabla "Estado vs código"
con fecha, y las constantes de tuning (5 m/s, 0,5 m, 5 %, 300 s, 1800 s, ×2 de prisa) pasan a un SO/singleton
(`CombatTuningConfig`, `BattleClockConfig`, `MovementGearTable`) para que doc y código apunten a la misma fuente.

### T4. Preparación para red: autoridad, determinismo y RNG (l6-01, l6-07, l6-08, L1-03, L1-05, L2-1, L2-8, L4-06, L7-1, L7-2)
No se implementa red ahora; se eliminan los bloqueos. Checklist de "listo para red" que condiciona otras propuestas:
1. **Reloj y resultado de partida en ECS** (L7-1, L7-2): hoy los escribe un MonoBehaviour (verificado).
2. **Daño como función pura sin visuales** (L1-03): el FCT sale del sistema de lógica a un buffer de eventos de presentación.
3. **Un solo RNG sembrado por (tick, entidad)** (L1-05): hoy hay `UnityEngine.Random` y `Unity.Mathematics.Random` mezclados.
4. **Puerta de habilidades pura** (L2-1) para que el servidor repita `Evaluate`; cooldowns como tiempo absoluto (L2-5).
5. **Sistemas de lógica sin `Time.frameCount` ni `UnityEngine.Random`** (L4-01, L4-06) y agrupados para poder asignarlos
   a un `WorldSystemFilter` de servidor sin reescribir.
6. **Ensamblados:** `Game.ECS` no referencia transporte; buffers `NetOutboxEvent`/`NetInboxCommand` como único puente (l6-07).
No hace falta simulación determinista ni rollback: la física es `CharacterController`/NavMesh y hay cientos de agentes.

## 4. Propuestas por sistema

Leyenda: coste S/M/L; riesgo B/M/A (bajo/medio/alto); prioridad A/M/B. "Ref." es el patrón de la referencia en una línea.

### 4.1 Combate

| ID | Título | Problema actual | Propuesta | Beneficio | Coste | Riesgo | Prio. |
|---|---|---|---|---|---|---|---|
| L1-01 (verificado) | Pila de modificadores por stat | Stats fijados una vez; sin componente de buff; `HeroPerk.effectValue` sin lector (`Squads/UnitStatsUtility.cs:55-80`, `Perks/HeroPerk.Component.cs`) | Ver T1 | Perks, auras, debuffs sin tocar `DamageCalculation` | M | M | A |
| L1-03 (verificado) | Fórmula de daño pura y Burst | Fórmula, escudo, vida y FCT en un `SystemBase` managed que llama a `FloatingCombatTextManager` (`Combat/DamageCalculation.System.cs:136,147,191`) | `static DamageFormula.Compute(in DamageInput)` con desglose; FCT vía buffer `DamageFeedbackEvent` + sistema visual aparte | Tests EditMode en ms; separa lógica de visual; base de servidor | S-M | B | A |
| L1-05 (verificado) | RNG sembrado por golpe | Melee usa `Random.value` (`Combat/MeleeContactResolution.System.cs:107`); disparo usa `Unity.Mathematics.Random` (`Combat/Systems/RangedAttack.System.cs:130`) | `crit`/`rollBits` en `PendingDamageEvent`, semilla (tick, atacante) | Reproducible y verificable | S | B | M |
| L1-02 (hecho, `DamageMechanics.md` §5.6) | Canales atacante/objetivo y condicionados | El evento lleva contexto pero no hay "+X % contra caballería" (`PendingDamageEvent.cs`) | Modificadores condicionados con máscara de bits, evaluados solo en el impacto | Contrajuego entre composiciones | M | M | M |
| L1-04 | Crítico con rendimientos decrecientes | Probabilidad fija por arma (`MeleeContactResolution.System.cs:107`, `HeroEntity.Authoring.cs:108`) | `rating/(rating+K)` y anti-crítico por defensa | Acumular crítico no rompe balance | S | B | M |
| L1-06 | Escudo absorbente separado de la vida | `UnitShieldComponent` es solo físico (`DamageCalculation.System.cs:123-160`) | `AbsorbShield {value, expiresAt}` entre cálculo y resta de HP | Primer efecto defensivo temporal | S | B | M |
| L1-08 (hecho, `DamageMechanics.md` §5.5) | Golpes que ignoran escudo | Bloqueo depende del collider y `currentBlock > 0` (`DamageCalculation.System.cs:125`) | Flag `ignoresShield`/`shieldDamageMult` en el perfil de daño | Identidad de armas | S | B | B |
| L1-07 (hecho, `DamageMechanics.md` §7.4) | Suelo de vida / protección tras respawn | Vida cae a 0 sin mecanismo de supervivencia (`DamageCalculation.System.cs:172-185`) | `minHealthRatio` en `HeroHealthComponent` | Menos spawn-kill | S | B | B |
| D1 (verificado) | Sincronizar `DamageMechanics.md` | Doc dice cap de mitigación 95 % y coste de bloqueo fijo 50; código resta plana `max(raw - netDef, raw*0.05)` y el escudo absorbe daño real (`DamageCalculation.System.cs:82-83,127`) | Reescribir doc y mover constantes a `CombatTuningConfig` | Diseño no balancea contra una fórmula inexistente | S | B | A |
| L1-09 (amp., bloqueado: animaciones, `Por_Abordar/Combate.md` C4) | Reacción al golpe por tipo y clase de objetivo | Solo hay retroceso del cuerpo (`Combat/HitKnockback.cs`) y aturdimiento al romperse el escudo (`Combat/DamageCalculation.System.cs:113`); sin tipos de reacción ni inmunidad | Enum `HitReaction` (ninguna, retroceso, derribo, despedido, rotura de guardia) por golpe, con permiso por clase de objetivo (soldado, héroe, grande) y un stat de resistencia al control que la anula | Golpes con peso distinto; base de las animaciones de reacción | M | M | M |
| L1-10 (amp., hecho, §5.5) | Desgaste del bloqueo por arma | El escudo absorbe el daño real hasta romperse (`DamageCalculation.System.cs:98-129`); todas las armas lo gastan igual | Multiplicador de daño al bloqueo en el perfil de daño, y daño al aguante del héroe que bloquea; va junto a L1-08 | Armas contundentes que abren escudos | S | B | M |
| L1-11 (amp., hecho, §5.5) | Bonus de carga por aproximación | El daño no depende de cómo se llega al golpe | Bonus del atacante según la distancia recorrida hacia el objetivo, en su dirección, justo antes del golpe; con umbral y tope por arma | Carga de lanceros y de la futura caballería | M | M | M |
| L1-12 (amp., hecho, §5.5) | Variación aleatoria del daño | Para la misma entrada el daño es siempre el mismo (`DamageFormula`) | Factor `1 ± f` por arma o habilidad, tirado con el RNG sembrado del golpe (L1-05) | Números menos mecánicos sin perder reproducibilidad | S | B | B |
| L1-13 (amp., hecho, §7.5) | Robo de vida | No existe | Stat de la pila: fracción del daño hecho que cura al atacante, aplicada después de restar la vida | Perks y habilidades de sustento | S | B | B |
| L1-14 (amp., hecho, §4.2) | Modificadores por terreno según el tipo de unidad | El terreno no afecta a los stats | El tipo de unidad declara modificadores por tipo de terreno (vida, daño, defensa, velocidad), aplicados con una fuente propia según el terreno del mapa | Identidad de cada unidad según el mapa | M | B | B |
| L1-15 (NvN) | Emboscada y flanco | El daño no depende de por dónde llega el golpe; `UnitCombatComponent.isFlanked` existe pero nadie lo escribe (`Combat/UnitCombat.Component.cs:15`) | Clase de golpe "por el flanco" o "por la espalda" según el ángulo entre la dirección del golpe y el frente del objetivo, y "emboscada" si el objetivo no veía al atacante (L4-03); multiplicador por clase como stat de la pila | Maniobrar con escuadras tiene premio; base de la caballería | M | M | A |
| L1-16 (NvN) | Resiliencia (aguante al golpe) | Solo el escudo roto aturde (`Combat/DamageCalculation.System.cs`); el resto de golpes no tiene peso propio | Barra de resiliencia por soldado y héroe que cada golpe gasta según su peso y que se recupera; al vaciarse, el objetivo sufre la reacción del golpe (L1-09) | Golpes pesados que rompen líneas; separa daño de control | M | M | A |
| L1-17 (NvN) | Atenuación del proyectil por distancia | Un proyectil hace el mismo daño a 5 m y a 60 m | Factor por distancia recorrida en el perfil del arma (sin pérdida hasta un umbral, luego lineal hasta un mínimo); etapa de `DamageFormula` | Los arqueros a quemarropa y a tope de alcance no valen igual | S | B | M |
| L1-18 (NvN) | Inmunidades por tipo de objetivo | Todo golpe daña a todo; solo L1-09 propone permisos por clase de objetivo para la reacción | Máscara de clases de objetivo que el golpe no puede dañar (héroe, soldado, estructura), en el perfil del golpe; la etapa devuelve cero con motivo en el desglose | Habilidades contra soldados que no borran héroes y al revés | S | B | M |
| L1-19 (NvN) | Alivio porcentual del daño | La defensa solo resta un plano por tipo con suelo (`Combat/DamageFormula.cs`) | Stat de la pila de reducción porcentual por tipo de daño, aplicado después de la resta plana; con tope | Fuego y veneno que no se anulan con armadura; resistencias de perk | S | B | M |

**L1-01.** Ref.: el poder se compone con una fórmula de ratios sumados + planos y un override; los efectos no conocen el
atributo. Ver T1. **L1-03.** Ref.: la calculadora es una cadena de etapas con entradas explícitas y desglose de salida
por etapa; así se testea y se muestra. Se empezó con pocas etapas; L1-15 a L1-19 añaden las que valen en NvN. **L1-05.** Ref.: el contexto del golpe ya trae
el azar resuelto y la calculadora solo lo consume. **L1-02.** Ref.: los mismos stats se leen por canal y algunos efectos
solo aplican si se cumple una condición del contexto. **L1-04.** Ref.: tasa `V/(P+V)`, asintótica; es la curva de
rendimientos decrecientes habitual en RPG. **L1-06.** Ref.: el escudo de buff absorbe primero y el exceso pasa a la vida;
un golpe de daño 0 no lo consume. **L1-08.** Ref.: hay golpes que perforan la defensa y reacciones que dependen de la
clase del objetivo. **L1-07.** Ref.: un suelo de vida fijado externamente, `max(vida - daño, suelo*vidaMax)`. **D1.**
Ref.: la nota de daño mantiene una tabla de cambios cada vez que el código cambia.
**L1-09 (amp.).** Ref.: el tipo de reacción lo decide un preprocesado del golpe, con un permiso por clase de objetivo que
un atributo puede forzar o anular; la inmunidad al control consiste en sustituir ese atributo. **L1-10.** Ref.: el golpe
lleva un daño propio contra el bloqueo y contra el aguante, aparte del daño a la vida. **L1-11.** Ref.: el bonus de carga
usa la distancia proyectada sobre la dirección del atacante, acotada, y solo cuenta por encima de un umbral del arma.
**L1-12.** Ref.: cada habilidad declara una amplitud de dispersión y el factor se tira al construir el golpe. **L1-13.**
Ref.: el robo de vida es un stat más de los efectos. **L1-14.** Ref.: los modificadores de terreno los declara el tipo de
unidad, filtrados por el terreno del nivel, y se suman a los ratios antes de multiplicar (T1).
**L1-15 a L1-19 (NvN).** Ref.: son etapas de su cadena de daño (`game-re/notes/damage-pipeline.md`): la clase de golpe
(emboscada, cabeza, crítico) va al principio; la atenuación del proyectil y el alivio porcentual van tras la penetración;
las inmunidades cortan antes de calcular; la resiliencia va al final y decide la reacción, no el daño. Coste a escala:
cada etapa nueva debe leer datos que ya viajan en el golpe o en la entidad objetivo, sin búsquedas extra por impacto.
Se dejan fuera las de valor bajo (§6).

### 4.2 Habilidades

| ID | Título | Problema actual | Propuesta | Beneficio | Coste | Riesgo | Prio. |
|---|---|---|---|---|---|---|---|
| L2-1 (verificado, corregido) | Puerta de lanzamiento única | `HeroInputSystem` escribe `UseSkill1/UseSkill2/UseUltimate` (`Hero/Systems/HeroInput.System.cs:56-58`) pero ningún sistema los lee fuera de tests; las condiciones de ataque están inline y duplicadas jugador/IA (`Hero/Systems/HeroAttack.System.cs:41-53,72-84`) | `SkillRequest` enableable + `SkillGate.Evaluate` Burst con enum de motivos; `SkillStartSystem` | Una fuente de reglas, motivo para la HUD, base de servidor | M | B | A |
| L2-5 (verificado) | Cooldowns por grupo y tiempo absoluto | `CooldownSystem` managed itera cada frame (`Shared/Cooldown.System.cs:14-24`, `isReady` redundante); `attackCooldown` aparte; swap con otro componente | `DynamicBuffer<CooldownGroupElement>{groupId,endTime}` + `CooldownScale` + `CooldownUtility` | Un solo punto para HUD y red; sin sistema por frame | M | B | A |
| L2-2 | Instancia de habilidad por fases | Trío fijo de ventanas de ataque (`Hero/Components/HeroCombat.Component.cs:19-30`) | `SkillInstance` + buffers de fases y eventos horneados desde SO | Combos, recuperación cancelable, cargas | L | M | A |
| L2-3 | Combos con ventana de encadenado | Pulsación durante `isAttacking` se descarta (`HeroAttack.System.cs:41`) | `nextSkillId`, `chainWindow` y buffer de entrada (~0,2 s) | Respuesta del ataque | S (tras L2-2) | B | M |
| L2-4 | Interrupción `interrupt` vs `yield` | Ataque no cancelable | `ParkedSkillInstance` enableable; fases interrumpibles; terminar al morir/cambiar escuadra | Sin estados colgados | M | M | M |
| L2-7 | Multiproyectil y tope de proyectiles | Un proyectil por petición; sin tope de vivos (`RangedAttack.System.cs:140-150`) | `LauncherScheme` (modo, cuenta, ángulo) y descarte del más antiguo | Abanico, ballesta, protección ante picos | M | B | M |
| L2-6 | Carga / mantener pulsado | No existe (GDD ~977-1078 la requiere) | Fase `esCarga` infinita + evento "soltar" | Tiro cargado | S (tras L2-2) | B | B |
| L2-8 | Proyectil simulado en ECS | `ProjectileController` (MonoBehaviour) y `ConcurrentQueue` estática | `ProjectileComponent` + sistema Burst con raycast Unity Physics | Determinismo, rendimiento | L | A | B |
| L2-9 (amp., hecho, `SkillsAndPerks.md` §3.6) | Habilidades de escuadra por el mismo lanzador | El GDD (§4.6, §12.3) las pide; hay datos (`SquadAbilityDefinitionComponent`, `AbilityByLevelElement`, `abilityUnlockLevel`) pero ningún sistema las lanza | La escuadra pide con `SkillRequestComponent`, pasa por `SkillGate` y enfría en su propio `CooldownGroupElement`; los efectos reutilizan `SkillEffectSystem` | Cierra el mayor hueco frente al GDD sin un segundo lanzador | M | M | A |
| L2-10 (amp., hecho, `SkillsAndPerks.md` §4) | Pilas y duración de los efectos | `StatModifier` no tiene pilas; reaplicar un efecto añade otro modificador (`Stats/StatTypes.cs:49-59`) | `maxStacks` y modo al reaplicar (refrescar, apilar, ignorar); stats de duración del efecto, del lado de quien lo aplica y de quien lo recibe | Efectos acumulables y resistencia a la duración | M | M | M |
| L2-11 (amp., hecho, §4) | Daño en el tiempo | No existe (`Docs/Por_Abordar/Habilidades_Perks.md` H4); Flecha Llameante se aproxima con daño directo | Efecto con periodo y duración que emite un `PendingDamageEvent` en cada tic | Quemadura, sangrado, veneno | S-M | B | M |
| L2-12 (amp., hecho, §3.2) | Velocidad de ataque | Las fases duran lo que dice el blob (`Skills/SkillTimeline.cs`); ningún stat las acorta | Stat de velocidad de ataque; las fases marcadas dividen su duración por él y la animación se acelera en la misma proporción | Buffs de ritmo; armas rápidas | S | B | M |
| L2-13 (amp., hecho, §4) | Tope de objetivos por habilidad | El daño en área golpea a todos los que están dentro (`Skills/SkillEffect.System.cs`, `SkillArea.Contains`) | Máximo de objetivos en el evento de efecto, los más cercanos primero | Áreas que se pueden equilibrar contra escuadras de 30 | S | B | M |
| L2-14 (amp., hecho, §4) | Efectos ligados a la habilidad | Los modificadores de una habilidad solo caducan por tiempo | Marcar el modificador con la instancia que lo creó y retirarlo cuando la habilidad termina o la interrumpen | Posturas y canalizaciones sin efectos residuales | S | B | B |
| L2-15 (amp., hecho, `SkillsAndPerks.md` §5) | Condiciones de efecto más ricas | `PerkCondition` solo tiene `HeroStill` y `HeroMovedRecently` (`Perks/PerkTypes.cs:15-22`) | Probabilidad, tipo de objetivo (héroe, soldado, clase), objetivo o atacante con un efecto activo, soldados restantes de la escuadra en un rango; combinadas con Y y con triestados | Los perks del GDD que hoy no tienen datos | M | B | M |
| L2-16 (NvN) | Recurso de combo y de bajas | Las habilidades solo piden aguante y enfriamiento (`SkillGate`); el combo del ataque básico es una cadena, no un recurso | Stat de la pila que suben los golpes y las bajas y bajan el tiempo y el gasto; condición nueva en `SkillGate` con su motivo de rechazo ("falta combo", "faltan bajas") | Remates que se ganan peleando; ritmo en batallas largas | S-M | B | M |
| L2-17 (NvN) | Habilidades y enfriamientos por soldado | La escuadra lanza su habilidad como un bloque y enfría por escuadra (L2-9) | Buffer `CooldownGroupElement` por soldado y lanzamiento por soldado con `SkillGate`, escalonado dentro de la orden de escuadra; la escuadra sigue dando la orden | Una andanada que se deshace a su ritmo; soldados que actúan cuando pueden | M | M | M |

**L2-1.** Ref.: toda habilidad pasa por una puerta con comprobaciones en orden fijo y código de motivo de rechazo; el
estado de control prohíbe por defecto. La corrección frente al informe: el input existe; falta el consumidor. Solo hay 3
flags (dos habilidades y definitiva), no 4: hay que decidir cómo mapean a Q/E/R/F (ver sección 7).
**L2-5.** Ref.: el reloj vive por grupo, no por habilidad; grupo 0 = sin enfriamiento; la duración usa un multiplicador
con tope. Un tiempo absoluto facilita reconciliar con servidor. **L2-2.** Ref.: habilidad = lista de fases con flags de
interrumpible/final/carga y eventos con instante. **L2-3.** Ref.: la siguiente de una cadena se acepta si la anterior fue
prerrequisito o ha pasado poco desde que acabó; el *input buffering* es práctica pública de juegos de acción.
**L2-4.** Ref.: una actual y una aparcada; una fase no interrumpible bloquea. **L2-7.** Ref.: esquema de lanzador con
modo, cuenta y ángulo; dispersión creciente con el movimiento; tope duro de proyectiles vivos. **L2-6.** Ref.: la carga
es una fase de duración infinita; soltar avanza a la final. **L2-8.** Ref.: el proyectil vive en un gestor con id con
generación, no como GameObject. Rompe el principio "la física de GO no decide lógica" mientras no se migre; justificar.
**L2-9 (amp.).** Ref.: las escuadras lanzan sus habilidades con el mismo ciclo de vida que el héroe; la referencia además
enfría por soldado (L2-17). **L2-10.** Ref.: la duración y las pilas máximas son stats que modifican por
separado quien aplica el efecto y quien lo recibe. **L2-11.** Ref.: el daño en el tiempo es un efecto con periodo y
duración propios. **L2-12.** Ref.: solo las fases marcadas escalan con la velocidad, y la animación se reproduce a la
proporción correspondiente. **L2-13.** Ref.: cada habilidad lleva un número máximo de impactos, modificable por efectos.
**L2-14.** Ref.: al terminar una habilidad se retiran los efectos enlazados a ella. **L2-15.** Ref.: las condiciones se
combinan con Y, las listas son "cualquiera de" y los triestados distinguen "no importa", "debe" y "no debe".
**L2-16 (NvN).** Ref.: el combo es una propiedad del actor y la puerta de lanzamiento rechaza con motivos propios por falta
de combo o de bajas. **L2-17.** Ref.: cada actor, soldados incluidos, lleva su gestor de enfriamientos, y la legión tiene
otro; los soldados lanzan por un camino propio. En ECS cuesta un buffer pequeño por soldado que solo se lee al lanzar.

### 4.3 Formaciones

| ID | Título | Problema actual | Propuesta | Beneficio | Coste | Riesgo | Prio. |
|---|---|---|---|---|---|---|---|
| L3-02 (verificado) | Rellenar huecos tras bajas | Slots por identidad (`UnitGridSlotComponent`, `Squads/Systems/Formation.System.cs:117-126`): supervivientes conservan slot, huecos fijos; `Docs/Mechanics/Formations.md:79,101` afirma que se comprime y `Docs/Arquitectura/7_*.md:35` lo marca pendiente | Varios patrones por tipo con `minUnits` + `FormationCompactSystem` con asignación voraz por distancia, con intervalo | Sin huecos en combate largo; menos recorrido | M | B | A |
| L3-01 (hecho, C13) | Validar slots sobre NavMesh por escuadra | Cada unidad proyecta su slot con `SamplePosition` radio 2 m por separado (`Squads/Systems/UnitNavMesh.System.cs:364`) | `FormationSlotResolveSystem` con `NavMeshQuery`, BFS acotado y `UnitResolvedSlotComponent`; solo al cambiar la revisión del ancla | Sin apilado en esquinas ni slots en otra isla | M | M | A |
| L3-03 | Jitter determinista en Dispersa | `Dispersed` es una rejilla fija de `int2` | `posJitter`/`yawJitter` por hash (unidad, slot) | Aspecto natural | S | B | M |
| L3-04 | Radio de snap por formación | Un `navMeshDestinationSampleRadius` global de 2 m (`UnitNavMesh.System.cs:89`) | `snapRadius` en `FormationDataBlob` | Formaciones cerradas no se rompen | S | B | M |
| L3-05 (hecho, `Formations.md` §4) | Caducidad de órdenes | Temporizadores dispersos por IA | `expiresAt` en `SquadResolvedOrderComponent` | Menos temporizadores; contrato de red pequeño | S | B | B |
| L3-06 (hecho, `Formations.md` §4) | Cambio de formación por tag | `FormationSystem` recorre todas las escuadras cada frame (`Formation.System.cs:25-41`) | `FormationChangePendingTag` enableable | Trabajo casi nulo aunque haya muchas escuadras | S | B | B |

**L3-02.** Ref.: la plantilla se elige por número de miembros (primera con `min <= N`), así que la formación se recompone
al bajar N. Asignación voraz basta con N<=30. **L3-01.** Ref.: la formación se convierte en destinos sobre una rejilla
local de ~1 m y los conflictos se reubican con BFS de 4 vecinos dentro de la misma isla de malla. **L3-03.** Ref.: la
dispersión usa celdas enteras distintas más variación angular limitada. **L3-04.** Ref.: radio de encaje = mínimo entre
espaciado y un tope. **L3-05/06.** Ref.: estado de orden como diccionario con caducidad por clave y cambio de formación
por llamada explícita, sin sondeo; aquí se toma solo la caducidad y el evento.

### 4.4 IA

| ID | Título | Problema actual | Propuesta | Beneficio | Coste | Riesgo | Prio. |
|---|---|---|---|---|---|---|---|
| L4-01 (verificado) | Reloj de pensamiento escalonado | `Time.frameCount % 5` (`Hero/AI/Systems/HeroAIPerception.System.cs:27`), igual frame para todos los bots; `SquadAISystem` recorre todo por frame (`Squads/Systems/SquadAI.System.cs:26-100`) | `AIThinkClock` + `AIThinkSchedulerSystem`, fase por hash del `Entity`, periodo en singleton `AIConfig`, tope por frame | Independiente de FPS, sin picos, escala a NvN | S | B | A |
| L4-02 (verificado) | Puertas con histéresis | `isUnderAttack` se activa por proximidad a 20 m o combate de escuadra (`HeroAIPerception.System.cs:105-107`); sin temporizadores | `DebouncedGate` (T_apertura/T_cierre) en el `HeroAIBlackboard` + compromiso mínimo de rama | Bots sin parpadeo; menos órdenes redundantes | S | B | A |
| L4-04 (hecho, `Movimiento_Formaciones.md` §21) | Puntuación de objetivo con saturación | Más cercano y dos pasadas de reparto (`Combat/UnitTargeting.System.cs:92-250`) | `Score` con 4 términos y candidatos K más cercanos | Menos retargets; O(K) por unidad | M | M | M |
| L4-03 (hecho, `DamageMechanics.md` §9.1) | Línea de visión con presupuesto de rayos | Detección solo por distancia al centroide (`Combat/EnemyDetection.System.cs:139-176`) | `LineOfSightBudgetSystem` por par escuadra-enemigo con cursor | Cobertura real, coste acotado | M | M | M |
| L4-06 (hecho: marca y test) | IA server-only | Todos los bots corren localmente | Agrupar `EnemyDetection`, `SquadAI`, perfil bot y `OrderResolution` en grupos de servidor | Evita rediseñar con Netcode | M | M | M |
| L4-05 (hecho, §9.2) | Sesgo de aggro / provocación | No existe aggro, solo represalia | `AggroBias` con expiración | Roles de tanque | S | B | B |
| L4-07 (aplazado, `Por_Abordar/IA.md` I1) | LOD de IA | Sin cotas; NvN sin medir | `AILodSystem` con tres niveles | Sostener NvN | M | M | B |
| L4-08 (amp., hecho con H1, `SkillsAndPerks.md` §8) | Elegir aliado al que apoyar | La IA no elige aliados ni lanza habilidades (`Docs/Por_Abordar/Habilidades_Perks.md` H1) | Puntuación de aliado por cercanía, vida perdida y si es héroe, dentro de un radio; el elegido se mantiene mientras siga siendo válido | IA de curación y de buffs coherente | S-M | B | M |
| L4-09 (amp., hecho con L4-04) | Búsqueda de enemigo por estrategia de escuadra | `EnemyDetection` usa la distancia al centroide con radio fijo (`Combat/EnemyDetection.System.cs:139-176`) | Radio de combate que crece con la velocidad de la escuadra, entre un mínimo y un máximo; descarte por diferencia de altura y por pendiente; variantes (arquero con línea de tiro, atacar avanzando, atacar una zona). Complementa L4-04 | Escuadras que no se enganchan con enemigos en otra cota | M | M | M |
| L4-10 (amp., hecho, §9.2) | Efectos que ajustan la IA de escuadra | Los parámetros de IA son constantes de configuración | Parámetros de IA (radio de combate, pesos) como stats de la pila de la escuadra, con fuente `Effect` | Órdenes y habilidades tácticas sin código nuevo | S | B | B |

**L4-01.** Ref.: cada contexto acumula `dt` y piensa a ~10 Hz; un cursor round-robin limita cuántos se actualizan por
frame. Fuente pública: *time slicing* / tick rate de IA. Riesgo concreto: no perder pulsos de `IsUnderAttackTag` (BUG-008).
**L4-02.** Ref.: decorador de precondición que exige condición continua T_apertura para abrir y T_cierre para cerrar. No
requiere intérprete de árboles. **L4-04.** Ref.: puntuación por cercanía + foco de escuadra + sesgo, menos penalización por
aliados ya asignados y bonus al objetivo actual (histéresis). **L4-03.** Ref.: máscara de visibilidad con rayos pedidos en
round-robin y presupuesto por paso de física. **L4-06.** Ref.: el servidor decide qué enemigos ve cada escuadra. **L4-05.**
Ref.: sesgo por escuadra sumado a la puntuación. **L4-07.** Ref.: tope de soldados considerados y LOD por distancia.
**L4-08 (amp.).** Ref.: la IA de apoyo elige el aliado con mayor puntuación (cercanía, vida perdida, héroe) y lo mantiene
mientras sigue siendo válido y no aparece otro claramente mejor. **L4-09.** Ref.: cada estrategia de escuadra fija su
referencia y su foco; el radio de búsqueda se interpola con la velocidad y hay filtros de altura y pendiente. **L4-10.**
Ref.: los efectos pueden cambiar los parámetros de la estrategia de IA a la que van dirigidos.

### 4.5 Movimiento

| ID | Título | Problema actual | Propuesta | Beneficio | Coste | Riesgo | Prio. |
|---|---|---|---|---|---|---|---|
| L5-1 (verificado) | Evitación recíproca ORCA | `NoObstacleAvoidance` en todas las unidades (`Squads/Systems/UnitNavMesh.System.cs:118`); `UnitBodyblock` salta a los aliados (`Squads/Systems/UnitBodyblock.System.cs:229`) | `UnitAvoidanceSystem` Burst tras `UnitNavMeshSystem`, reutilizando la rejilla del bodyblock; responsabilidades 0,5 / 1,0 / 0 para el héroe local | Elimina solapes y atascos aliados | L | M | A |
| L5-2 (verificado) | Marchas como dato | `speedMultiplier *= 2f` fijo para prisa o combate (`Squads/Systems/UnitFollowFormation.System.cs:107`) | `MovementGear` + tabla por clase + `UnitSpeedSystem` | Sin ×2 mágico; una fuente para animación y red (1 byte) | M | B | A |
| L5-3 (parcial) | Velocidad con capas | `UnitSpeedCalculator` ya centraliza la velocidad base (`Squads/UnitSpeedCalculator.cs`); el aturdimiento sigue duplicado en dos sistemas | `SpeedModifier` como caso de T1 + override a 0 | Un punto para buffs y stun | S-M | B | M |
| L5-4 | Giro proporcional con tope | Asignación directa de rotación en `UnitRotationResolution` | `RotateTowards` con `turnRateMoving/Idle` | Sin giros de 180° en un frame | S | B | M |
| L5-5 (hecho, `Movimiento_Formaciones.md` §25) | Radio escalable | `bodyblockRadius` único de 0,8 m | `UnitBodyRadius`, celda = máximo | Prepara unidades grandes | S | B | B |
| L5-6 (bloqueado: sin caballería, `Por_Abordar/Movimiento.md` M17) | Vínculo jinete-montura | No hay monturas | `MountBinding` + `MotorMode` | Evita rediseño | L | M | B |
| L5-7 (amp., hecho, §25) | Aguante según la marcha | Solo el sprint del héroe gasta aguante (`Hero/Systems/HeroStamina.System.cs`); las escuadras corren sin coste | Coste por marcha (cantidad fija, % del máximo o por segundo) para escuadras, escalado por la pila; sin aguante, la marcha de carrera baja un nivel | Correr tiene un precio táctico | S-M | M | B |
| L5-8 (amp., bloqueado: sin caballería, M17) | Montura: arco de ataque y aguante propio | No hay monturas | Arco de giro respecto a la montura fuera del cual el jinete no ataca; aguante de la montura aparte del del jinete | Evita rediseño si llega la caballería | S (tras L5-6) | B | B |

**L5-1.** Ref.: ORCA (van den Berg et al., "Reciprocal n-body collision avoidance", RVO2, Apache-2.0): un semiplano por
vecino y un programa lineal para la velocidad más cercana a la deseada; el esfuerzo se reparte con responsabilidad 0,5
entre iguales. Mejor que el héroe local no entre en el solver (su `CharacterController` es físico). **L5-2.** Ref.: nivel
de marcha en enum de cuatro posiciones con tabla de velocidades por motor; la animación consume la marcha en lugar de
inferirla. **L5-3.** Ref.: base por marcha × varios multiplicadores independientes, con override y "sin override".
**L5-4.** Ref.: giro `min(k*|error|, giroMax)*dt` con giro máximo distinto parado y en marcha. **L5-5/6.** Ref.: radios
propios por actor; el jinete reenvía el input al motor de la montura.
**L5-7 (amp.).** Ref.: el aguante elige un modo según el motor y la marcha, con tres formas de coste. **L5-8.** Ref.: si el
jinete gira fuera del arco permitido respecto a la montura, pierde la capacidad de atacar.

### 4.6 Red

| ID | Título | Problema actual | Propuesta | Beneficio | Coste | Riesgo | Prio. |
|---|---|---|---|---|---|---|---|
| l6-01 | Reparto de autoridad | Sin reparto; `IsLocalPlayer` decide quién mueve (`EntityVisualSync.ConfigureMovementAuthority`); `Docs/ModeloHybrido.md` lo admite | Servidor simula todo; cliente predice solo su héroe; reconciliación por mezcla de offset | Anti-trampas básico | L | M | A |
| l6-02 | Replicar héroe y escuadra, no unidades | 900 agentes locales (`Docs/Arquitectura/11_*`) | 6 héroes + 6 escuadras; formación recalculada en cliente | ~15x menos ancho de banda (~30 kbps vs ~0,5 Mbps) | M | M | A |
| l6-03 | Cuantización e ids cortos | NGO serializa `Vector3` de 12 B | u16 por eje sobre límites del mapa; ids de 1 B; enum de mensajes | 2-3x menos bytes | S | B | A |
| l6-07 | Ensamblados y puente ECS-red | `Game.ECS.asmdef` monolito | `Net.Shared/Server/Client`; buffers de entrada/salida; test de arquitectura | Servidor probable sin Netcode | M | B | A |
| l6-08 | Reloj de red a 30 Hz | Simulación sin tick de red | `NetTick` solo para lo autoritativo; interpolar remotos ~100 ms atrás | Interpolación estable | S | B | A |
| l6-04 | Transporte con pipelines | `com.unity.transport 6.5.0` sin uso | Canales no fiable (snapshots) y fiable (comandos); KCP solo si hace falta | Sin ARQ propio | S | B | A |
| l6-05 | Latido y reconexión | Nada | Ping 1 s, sospecha a 3 s, corte a 8 s, reenvío por secuencia, snapshot completo si hay hueco | Un corte corto no pierde partida | M | M | M |
| l6-06 | Sesión de batalla separada | `PlayerSessionService` sin implementar | Ticket firmado de batalla; servidor dedicado por partida | Aislamiento de fallos | M | B | M |
| l6-09 (NvN) | Relevancia por cliente | Sin red; l6-02 manda todos los héroes y escuadras a todos | Cada cliente recibe con detalle lo que tiene cerca o a la vista y con menos frecuencia lo lejano; prioridad por distancia y por si es su escuadra o su objetivo | Con NvN el límite es el ancho de banda de subida del servidor, no la CPU | M | M | A |
| l6-10 (NvN) | Transporte de respaldo | Sin red | Si UDP no conecta (redes que lo bloquean), abrir la sesión por WebSocket/TCP con el mismo protocolo; solo cuando falla, no dos conexiones a la vez | Jugadores tras cortafuegos no se quedan fuera | S-M | B | B |

**l6-01.** Ref.: el servidor decide lo que cambia el resultado; el cliente es dueño de su propio movimiento y la cola de
estados predichos se reproduce sobre la corrección. Reconciliación sin rebobinado: error pequeño se ignora, medio se
mezcla con decaimiento exponencial, grande teletransporta. **l6-02.** Ref.: las consecuencias viajan, no la formación
(hipótesis [hip] de la nota). **l6-03.** Ref.: ids cortos, half de 16 bits, mensajes agregados; cuaterniones "smallest
three" (técnica pública). **l6-04.** Ref.: KCP (MIT) agrupa segmentos y retransmite agresivo; solo compensa con pérdidas
del 3-5 %. **l6-05.** Ref.: latido, prueba tras silencio y reenvío por secuencia; aquí se mejora enviando snapshot completo
en lugar de cerrar. **l6-06/07/08.** Ref.: sesión de gateway distinta de la de sala; despachadores de red separados de la
lógica. Recomendación del informe: protocolo propio sobre Unity Transport o NGO como cáscara; no adoptar Netcode for
Entities mientras héroe y unidades se muevan con `CharacterController` y `NavMeshAgent`.
**l6-06 (NvN).** La referencia separa un gateway maestro (sesión larga del jugador) de uno esclavo por sala. Aquí basta el
ticket firmado; una red de gateways propia solo compensa si hay muchos servidores de partida a la vez.
**l6-09 (NvN).** Ref.: no hay compresión de paquete; el ahorro viene de cuantizar y de no mandar todo. La relevancia por
cliente no sale de las notas: es la técnica pública (*interest management*) que hace falta cuando N crece.
**l6-10.** Ref.: abre una conexión de apoyo por el transporte contrario (TCP y KCP en paralelo); aquí se toma solo la idea
del respaldo, abierto bajo demanda.

### 4.7 Progresión y modos

| ID | Título | Problema actual | Propuesta | Beneficio | Coste | Riesgo | Prio. |
|---|---|---|---|---|---|---|---|
| L7-1 (verificado) | Reloj de partida en ECS | `BattleTimerExtensionSystem` llama a `FindAnyObjectByType<BattleSceneController>().ExtendTimer(300)` (`Map/BattleTimerExtension.System.cs:19-21`); el timeout escribe `winnerTeam = 2` desde el GameObject (`UI/Battle/BattleSceneController.cs:619-633`); `MatchState` no tiene `remainingTime` | `BattleClockComponent` + `BattleClockSystem` + `BattleTimeExpiredEvent`; valores en SO | Corrige dos violaciones del modelo híbrido; listo para replicar | S | B | A |
| L7-2 (verificado) | Resolutor único de resultado | Captura final escribe `winnerTeam` con `capturingTeam` (`Map/CaptureZoneTrigger.System.cs:119`), el timeout con 2; convenciones divergentes | Captura solo emite evento; `MatchOutcomeSystem` único con orden fijo y enum `MatchWinner` | Sin carreras ni empate ambiguo | S | B | A |
| L7-3 (verificado) | Buffer unificado de modificadores | Equipo solo para el local (`Hero/Systems/HeroInitialization.System.cs:117-119`), escuadra por curvas, perks sin lector | Ver T1 | Una ruta para equipo, perks y habilidades | M | M | A |
| L7-4 (verificado) | Perks como efectos con canal | `HeroPerkComponent` solo guarda un id; sin sistema lector (`Perks/HeroPerk.Component.cs`) | SO con `StatModifier[]` + `HeroPerkLoadout` (5+2) + `PerkApplySystem` | Cierra el hueco GDD vs código | M | B | M |
| L7-5 | Curvas de XP y desbloqueos en datos | XP de escuadra `100 × 1,1^n` en código; `(i+1)*10`; atributos revalidados por frame | `ProgressionTable` SO a blob; validar atributos al asignar | Balance sin recompilar | S | B | M |
| L7-6 | Stats de objeto con `StatId` | `Dictionary<string,float>` (`Inventory/statsGenerators/BasicArmorStatGenerator.cs:23-46`) | `{statId, valor}` + semilla de generación | Guardado pequeño; validable en servidor | M | M | M |
| L7-7 | Estados de captura explícitos | Cuatro flags en `CapturePointProgress.Component.cs`; doc define 6 estados | Enum `ZoneState` y `CaptureMode` en SO | HUD sin reconstruir; variantes por campo | S | B | M |
| L7-8 (amp.) | Marcador por equipo y escuadra | No hay estadísticas de partida; la vista de `Tab` del GDD (§12.1.1) no existe | Buffer de estadísticas por equipo con una fila por escuadra (bajas, daño, capturas), escrito a partir de los eventos de daño y de captura | Base del marcador y del resumen de partida | M | B | M |
| L7-9 (amp.) | Maestría que sustituye una habilidad | No existe | Un desbloqueo que sustituye el id de una habilidad (o de una fase) por otro al montar el loadout | Progresión de arma sin lógica nueva en el lanzador | S | B | B |

**L7-1/2.** Ref.: el inicio y el fin de partida son una transición de estado autoritativa; el cliente solo hace cuenta atrás
visual y hay un único mensaje de fin con resultado. **L7-3/4.** Ref.: el poder no se calcula por fuente; equipo, talentos y
huecos inyectan efectos en listas por fase de uso, y un talento no lleva lógica propia. **L7-5.** Ref.: tabla de diseño
de XP y función lineal a trozos acumulada (tarifa progresiva) para atributos. **L7-6.** Ref.: ids estables de efecto y
referencia a fila de diseño más lista corta de extras. **L7-7.** Ref.: progreso y propietario los decide la capa
autoritativa; variantes de captura.
**L7-8 (amp.).** Ref.: las estadísticas se agrupan por equipo y, dentro, por escuadra, con unos pocos contadores y banderas
por escuadra. **L7-9.** Ref.: la maestría no añade lógica: sustituye la habilidad lanzada por otra.

## 5. Hoja de ruta por fases

Ordenada por dependencias. Cada fase deja el proyecto jugable.

**Orden vigente por prioridad del usuario:** primero el movimiento, con los lotes de `Movimiento_Formaciones.md` §4:
C1-C4 (datos y umbrales), luego C5-C9 (contacto, giro, separación y seguimiento) y después C10-C15 (estructura). Esos
lotes absorben la antigua Fase 3 y los retoques L3-03/L3-04 de la Fase 0. Después siguen las fases de abajo en su orden.

**Fase 0 — Higiene y bajo riesgo (sin dependencias).**
> **Hecha (2026-10-21, rama `feat/reference-phase0`):** D1, L4-01, L4-02, L7-1, L7-2, L1-03 y L1-05, un commit cada una (`tasks/todo.md`). L3-03 y L3-04 ya entraron con las formaciones por datos.
D1 y sincronización de docs (sección 8); L4-01 y L4-02 (reloj y histéresis); L7-1 y L7-2 (reloj y resultado en ECS);
L1-03 (daño puro) y L1-05 (RNG sembrado); L3-03/L3-04 como retoques. Estas piezas son prerrequisito de red (T4) y no
dependen de nada.

**Fase 1 — Pila de modificadores (T1).**
> **Hecha (2026-10-21, rama `feat/reference-phase0`):** núcleo T1, nivel de escuadra, héroes y equipo (L7-3, IA incluidos),
> L5-3, L1-06, L1-04 y L7-5 (XP y desbloqueos), un commit cada paso (`tasks/todo.md`). Regla y fuentes en
> `Docs/Mechanics/DamageMechanics.md` §4.1. L7-6 queda para después, en `Docs/Por_Abordar/Pila_Modificadores.md` P1.
L1-01 y L7-3 con `StatId`; después L5-3 (velocidad), L1-06 (escudo absorbente), L1-04 (crítico por rating), L7-5
(tablas) y L7-6 (ítems con `StatId`, tras decidir migración de guardados). Va antes que los perks.

**Fase 2 — Perks y lanzador.**
> **Hecha (2026-10-22, rama `feat/reference-phase0`):** L2-1, L2-2, L2-3, L2-4, L2-5, L2-6, L2-7 y L7-4, con efectos
> genéricos para las 16 habilidades de clase (`tasks/todo.md`). Mecánica en `Docs/Mechanics/SkillsAndPerks.md`; lo que
> queda, en `Docs/Por_Abordar/Habilidades_Perks.md`. L2-8 (proyectil en ECS) sigue fuera.
L7-4 (perks, tras Fase 1). L2-1 (puerta) y L2-5 (cooldowns por grupo) antes que L2-2 (fases); después L2-3 (combos),
L2-4 (interrupción), L2-6 (carga) y L2-7 (multiproyectil). El lanzador va antes que los combos; L2-5 usa
`CooldownScale` de la pila de Fase 1.

**Fase 3 — Formación y movimiento.**
L3-02 (huecos) y L3-01 (validación NavMesh, absorbe L3-04); L5-2 (marchas) y L5-4 (giro); después L5-1 (ORCA), que
reutiliza la rejilla del bodyblock y requiere L5-2 para saber la velocidad preferida. L5-5 y L5-6 solo si hay monturas.

**Fase 4 — IA avanzada.**
L4-04 (puntuación) y L4-03 (línea de visión, si la visibilidad es regla de juego se adelanta a antes de la red);
L4-05 (aggro) solo si hay habilidades de provocación; L4-07 (LOD) tras medir en Player.

**Fase 5 — Preparar autoridad y determinismo.**
L4-06 (agrupar sistemas server-only), l6-07 (ensamblados y test de arquitectura), L2-8 (proyectil ECS) y L3-05 (caducidad
de órdenes). Aquí se confirma el checklist T4.

**Fase 6 — Red (al final).**
l6-03 (cuantización, puro y sin dependencias de red) y l6-08 (`NetTick`), luego l6-04 (transporte), l6-01 y l6-02
(autoridad y réplica por equipo), l6-06 (ticket) y l6-05 (latido/reconexión). Decisión previa obligatoria: Netcode for
Entities frente a Unity Transport/NGO (sección 7); si fuera NfE, decidirlo antes de escribir el protocolo propio.

**Ampliación (2026-10-26).** Dónde encajan los puntos "(amp.)":
- **Fase 2b — Habilidades de escuadra y efectos:** L2-9 primero (prioridad alta), después L2-15, L2-10, L2-11, L2-13,
  L2-12 y L2-14. Junto con la elección de perks (H5) cierran lo que el GDD pide de habilidades.
- **Combate, junto a L1-02 y L1-08:** L1-10, L1-11, L1-12, L1-13 y L1-14; L1-09 cuando haya animaciones de reacción.
- **Fase 3 (movimiento):** L5-7, que se apoya en las marchas (L5-2, ya hechas con C9).
- **Fase 4 (IA):** L4-09 con L4-04; L4-08 con H1 (la IA usa habilidades); L4-10 después de L2-9.
- **Según alcance:** L5-8 solo con caballería (con L5-6); L7-8 cuando se haga la vista de `Tab`; L7-9 si el GDD la pide.

**Revisión NvN (2026-11-06).** Dónde encajan los puntos "(NvN)":
- **Combate:** L1-15 (flanco) y L1-16 (resiliencia) primero; L1-16 desbloquea la parte de datos de L1-09 aunque falten
  animaciones. Después L1-19, L1-18 y L1-17, que son etapas sueltas de `DamageFormula`.
- **Habilidades:** L2-16 (combo y bajas) con la pila y `SkillGate` ya hechas; L2-17 (por soldado) después, junto a L2-9.
- **Red:** l6-09 (relevancia) entra en la Fase 6 junto a l6-02; l6-10 al final, con l6-04.

### Estado a 2026-10-27
- **Hecho:** Fases 0, 1 y 2; lotes de movimiento C1-C15 de `Movimiento_Formaciones.md`, que cubren L3-02 (C12),
  L5-1 (C14), L5-2 (C9), L5-4 (C7, con la decisión D5) y L3-01 (C13, 2026-10-26, §20: slots validados por escuadra con
  BFS y D9); L3-03 y L3-04 entraron con las formaciones por datos. L4-04 y L4-09 (2026-10-26,
  `Movimiento_Formaciones.md` §21): puntuación de objetivo con saturación blanda entre escuadras (D6) e histéresis, y
  búsqueda de enemigo por escuadra con radio por estrategia y velocidad y filtro de cota. De L4-09 quedan fuera las
  variantes que piden L4-03 (arquero con línea de tiro) y una orden de "atacar una zona" con punto propio (hoy la zona es
  el ancla de mantener posición).
- **Hecho en el bloque 3 (2026-10-27, `Docs/Mechanics/SkillsAndPerks.md` §3.6, §5 y §8):**
  - L2-9: las escuadras lanzan sus habilidades por `SkillGate` con enfriamiento propio, al nivel de desbloqueo, con las
    teclas 3, 4 y 5. Hay dos habilidades por escuadra (Escuderos, Arqueros y Lanceros).
  - L2-15: condiciones de perk con probabilidad sembrada, tipo y clase del objetivo, triestados de efecto activo y
    escuadra restante.
  - H5: panel de perks del feudo.
  - H1 con L4-08: la IA usa habilidades de clase y de escuadra, y elige y mantiene un aliado al que apoyar.
- **Hecho en formaciones (2026-11-03, `Movimiento_Formaciones.md` §24):** L3-05 (los plazos de las órdenes son horas de
  fin y viajan con la orden resuelta en `expiresAt`; mismo comportamiento), L3-06 (`FormationSystem` solo visita las
  escuadras con `FormationChangePending`) y el resto de L3-02 (los huecos dentro de una plantilla se cierran 1,5 s tras la
  primera baja, moviendo solo a los sobrantes).
- **Hecho en IA (2026-11-03, `DamageMechanics.md` §9):** L4-10 (radio de detección, radio de combate y sesgo hacia
  héroes como stats de la pila de la escuadra), L4-05 (sesgo de aggro con caducidad y evento `Taunt`), L4-03 (línea de
  visión por escuadra y enemigo con rayos por lotes y presupuesto, como la referencia pero sin bajar al soldado) y L4-06
  (marca `[ServerAuthoritative]` y test de arquitectura, sin mover el orden). **L4-07 aplazado:** hay que medir antes
  (`Docs/Por_Abordar/IA.md` I1).
- **Hecho en movimiento (2026-11-03, `Movimiento_Formaciones.md` §25):** L5-7 (aguante por escuadra: correr gasta por
  soldado, sin aguante no se corre hasta recuperar un umbral; neutro sin datos) y L5-5 (radio de cuerpo por tipo de
  escuadra, usado por pareja en contacto, evitación, empuje y alcance; todos a 0,5 m como antes). **L5-6 y L5-8 quedan
  bloqueados** hasta que haya caballería (`Docs/Por_Abordar/Movimiento.md` M17).
- **Hecho en efectos de habilidad (2026-11-03/04, `SkillsAndPerks.md` §3.2 y §4):** L2-13 (tope de objetivos, los más
  cercanos primero), L2-14 (modificadores ligados a la instancia de la habilidad), L2-10 (pilas con modo al reaplicar y
  duración escalada por quien aplica y quien recibe), L2-11 (daño en el tiempo; Flecha Llameante pasa a impacto más
  quemadura) y L2-12 (velocidad de ataque en las fases marcadas y en el Animator del héroe). El 2026-11-04 (H15): el
  tope de L2-13 se cambia con efectos (stat `MaxTargets`), L2-14 sobrevive a la habilidad aparcada, y L2-12 llega a las
  habilidades de escuadra, al ataque de los soldados y a sus Animator.
- **Pendiente del catálogo:** L1-15 a L1-19, L2-16 y L2-17 (NvN, 2026-11-06); L2-8; L5-6 y L5-8 (bloqueados); l6-01 a l6-10; L7-6 (para después, por decisión del usuario), L7-7; y los puntos "(amp.)" salvo L4-09,
  L2-9, L2-15, L4-08 y los de combate (L1-10 a L1-14 hechos el 2026-11-03; L1-09 bloqueado).
- **Hecho en formaciones (2026-11-02, `Movimiento_Formaciones.md` §23):** L3-02 se completa en la parte de plantillas: Spearmen y
  Squires tienen una plantilla por tramo de soldados vivos y la formación se recompone al caer soldados (F1, `1eb56b03`).
  La compactación dentro de un tramo llegó el 2026-11-03 (§24). Los slots no se comparten entre escuadras aliadas (F17, `8f3b45cf`).
- **Hecho en combate (2026-11-03, `Docs/Mechanics/DamageMechanics.md` §4.2, §5.5, §5.6, §7.4 y §7.5):** L1-08 y L1-10
  (rasgos del golpe en el perfil de daño: ignorar el escudo, desgaste del bloqueo y del aguante), L1-07 (suelo de vida como
  stat y protección al reaparecer), L1-12 (variación sembrada), L1-13 (robo de vida), L1-02 (condicionados de atacante y
  receptor con el conjunto completo de condiciones), L1-11 (carga: metros recorridos hacia el objetivo, por decisión del
  usuario, con la estructura de umbral y tope de la referencia) y L1-14 (un terreno por mapa y modificadores por tipo de
  escuadra). La mecánica está; los datos (valores por arma, terrenos de los mapas, condicionados) quedan en
  `Docs/Por_Abordar/Combate.md` C1-C3. **L1-09 queda bloqueado** por las animaciones de reacción (C4). C1 (valores por
  arma) y C2 (terreno) se rellenaron después con tablas aprobadas por el usuario.
- **Abierto de lo ya implementado:** `Docs/Por_Abordar/` (H2-H14 habilidades y perks, P1-P6 pila, F4-F16 formaciones y
  posturas, M1-M15 movimiento, C1-C8 combate).
- **Orden recomendado** (prioridad del usuario: movimiento y formación): para los solapes de la melé (M2),
  el interruptor de colisión por escuadra (C16, F9). En habilidades, L4-10 (efectos sobre la IA de escuadra, ya con L2-9)
  y L2-10 (pilas), y H10 (habilidades de escuadra en el HUD).

## 6. Lo que no conviene copiar

Revisado el 2026-11-06 para NvN: el tamaño de partida no es motivo para descartar nada. Lo que se descartaba por "ser un
3v3" ha pasado a la sección 4 (L1-15 a L1-19, L2-16, L2-17, l6-09, l6-10). Lo que queda aquí se descarta por otro motivo.

- **Etapas de daño de valor bajo:** golpe en la cabeza (pide colisión por hueso en cientos de soldados), bonus por vida
  restante o por diferencia de armadura (balance de MMO, poco legible) y daño a estructuras (no hay asedio). Se reservan.
- **Depósito de ~200 propiedades de actor con notificación a observadores**: en ECS el equivalente son componentes
  tipados, `IEnableableComponent` y `ChangeFilter`; replicarlo añade indirección sin beneficio.
- **Datos de diseño cifrados y rutas por hash FNV** (atributos, ids de mensaje, estados): un enum `StatId` y un enum de
  mensajes de 1 B son más pequeños y depurables. Además no se copian valores.
- **Intérprete de árboles de comportamiento y acciones por scripting**: de él solo se toman cadencia (L4-01) e histéresis
  (L4-02). Un segundo lenguaje de control fuera de ECS no se puede compilar con Burst ni repartir en jobs.
- **Plantillas de formación con herencia (vtable de 33 slots), rutas por corredor y rejilla por waypoint**: el blob de
  datos más un calculador cubre el caso; basta una validación al cambiar el ancla.
- **Diccionario dinámico de atributos "pure" por soldado**: se perdería seguridad de tipos y Burst; solo se toma la caducidad.
- **Cifrado propio por transporte (RC4 en TCP, AES-ECB con token en KCP)**: RC4 está roto y ECB es mal modo; bastan
  ticket firmado y DTLS de Unity Transport. No depende del tamaño.
- **Dos transportes abiertos a la vez y compresión de paquete**: del primero se toma el respaldo bajo demanda (l6-10); la
  compresión no la usa ni la referencia, que ahorra cuantizando (l6-03) y mandando menos (l6-09). La red de gateways
  queda condicionada a la escala de servidores (nota de l6-06).
- **Rollback completo o simulación determinista tipo juego de lucha**: física no determinista y cientos de agentes.
- **Quince canales de afijos, tecnología de legión, runas, asedio, nado y escalada, maestría decidida por servidor**:
  metajuego o mecánicas que el GDD no tiene.
- **Matriz de alcanzabilidad por soldado y enemigo, tabla "no atacable" por actor, IA de curación por soldado**: sin caso
  de uso hoy; reservar el patrón.

## 7. Dudas abiertas para el usuario

1. **Habilidades:** el input solo tiene `UseSkill1/UseSkill2/UseUltimate`, pero el GDD habla de Q/E/R/F. ¿R y F son ultimate
   y un cuarto slot, o hay que añadir input? ¿Se lanzan sin objetivo (área) o apuntadas? ¿El ataque básico tendrá combo?
2. **Buffs en el MVP:** ¿habrá buffs/habilidades temporales o solo perks pasivos? Cambia la prioridad de T1 y L1-06.
   ¿Los perks activos son habilidades nuevas o modificadores temporizados (L7-4)?
3. **Red:** ¿servidor dedicado de Unity sin gráficos (conserva `NavMeshAgent`) o servidor ECS puro? ¿Netcode for
   Entities (reescribe el movimiento) o protocolo propio? Objetivo de RTT y pérdida tolerada. Tamaño máximo de escuadra
   y del mapa (fijan la cuantización). ¿Relevar con IA al jugador desconectado o pausar? ¿Ocultar enemigos no detectados?
4. **Autoridad del daño:** ¿debe ser de servidor a medio plazo (OPEN-002)? Si sí, L1-03 y L1-05 suben a prioridad alta.
5. **Visibilidad:** ¿oclusión y campo visual son regla de gameplay o solo IA? Si lo primero, L4-03 entra antes de la red.
6. **Aliados:** ¿deben atravesarse (como ahora) o bloquearse? Define el diseño de L5-1. ¿Por qué sigue activo el
   `NavMeshAgent` con evitación desactivada: rendimiento o herencia de las pruebas de carving?
7. **Convenciones:** ¿`winnerTeam` es 0/1/2 o `Team`? Con un solo defensor en la zona, ¿el progreso se pausa (doc) o
   retrocede (código, verificado)? ¿Los docs de mecánicas son fuente de verdad (se corrige el código) o solo
   documentación (se corrige el doc)?
8. **Formaciones:** ¿Testudo, Schiltron y Muro de escudos deben mantener forma estricta y rechazar el cambio si no cabe, o
   degradar a Línea? ¿Cuál es la lista vigente de tipos? ¿Se conecta `FormationAdaptationSystem` (nadie consume
   `requiresAdaptation`) o se elimina? ¿Una petición dentro del cooldown de 1 s debe aplicarse al terminar?
9. **Feel:** ¿se aceptan cambios en el targeting (L4-04) a cambio de menos retargets? ¿Crítico por rating (L1-04) o
   probabilidades legibles para el jugador?
10. **Alcance:** ¿se prevén monturas/caballería (sube L5-5, L5-6)? ¿Tamaño máximo de batalla y hardware objetivo para
    el presupuesto de IA (L4-03, L4-07)?

## 8. Desfases entre la documentación y el código

| # | Documento | Dice | Código | Estado |
|---|---|---|---|---|
| 1 | `Docs/Mechanics/DamageMechanics.md:9,99-102,129,132,199` | Mitigación proporcional con cap 95 %, bloqueo con coste fijo de 50, orientación | Resta plana `max(raw - netDef, raw*0.05)` por tipo y escudo que absorbe el daño real hasta romperse (`Combat/DamageCalculation.System.cs:82-95,125-127`) | Verificado; **doc corregido (D1, 2026-10-21)** |
| 2 | `Docs/Mechanics/Formations.md:79,101` | La formación se comprime automáticamente al perder unidades | Slots por identidad; supervivientes conservan slot (`Formation.System.cs:117-126`); `Docs/Arquitectura/7_*.md:35` lo reconoce como límite | Verificado; **doc corregido (D1, 2026-10-21)** |
| 3 | `Docs/Mechanics/MatchTimer.md:62,101-102` | `MatchController` hace el tick y `MatchState` tiene `remainingTime` | El reloj es un MonoBehaviour (`BattleSceneController`) y `Shared/MatchState.Component.cs` no tiene `remainingTime` | Verificado; **código y doc corregidos (L7-1/L7-2, 2026-10-21)** |
| 4 | `Docs/Mechanics/MatchTimer.md` y `CapturePoints.md` | `winnerTeam` 0 atacantes / 1 defensores; captura gana si coincide con el timer; captura se pausa con defensor | Componente: 1/2; la captura escribe `capturingTeam` (`CaptureZoneTrigger.System.cs:119`); con solo defensores el progreso retrocede (`:60-67`); ningún sistema ordena el empate | Verificado (convención: informe L7); **código y doc corregidos (L7-1/L7-2, 2026-10-21)** |
| 5 | `CLAUDE.md:12` | Unity 2022.3 / Entities 1.3.14 | `Packages/manifest.json` usa Entities 6.5.0 (Unity 6) | Verificado; **doc corregido (D1, 2026-10-21)** |
| 6 | `Docs/Mechanics/Formations.md` vs `Docs/TroopMovementPipeline.md` | Listas de tipos de formación distintas (Cuña/Schiltron/Dispersa frente a Line/Dispersed/Testudo/Wedge/Column/Square); el código tiene también ShieldWall | Sin unificar | Del informe L3, sin comprobar; **doc corregido (D1, 2026-10-21)** |
| 7 | `Docs/TDD.md:410` | `SquadObstacleAvoidanceSystem` | No existe en `Assets/Scripts` | Del informe L5, sin comprobar; **doc corregido (D1, 2026-10-21)** |
| 8 | `Docs/Mechanics/DamageMechanics.md` | `forwardBlockDotThreshold` y orientación | `DamageCalculation` no los usa | Del informe L1, sin comprobar; **doc corregido (D1, 2026-10-21)** |
| 9 | Código | `FormationAdaptationSystem` calcula `requiresAdaptation` | Nadie lo consume (TODO en `UnitFollowFormation`) | Del informe L3, sin comprobar; **doc corregido (D1, 2026-10-21)** |
| 10 | Informe L2 | "No hay entrada Q/E/R/F" | `HeroInputSystem` sí escribe `UseSkill1/UseSkill2/UseUltimate` (`HeroInput.System.cs:56-58`); falta el consumidor | Corregido |
| 11 | Escenas de batalla | `HeroAttackSystem` y `HeroStaminaSystem` exigen `HeroGameplayConfigComponent` (`RequireForUpdate`) | `HeroGameplayConfigAuthoring` solo está en `SettlementHeroWorld.unity`; ninguna subescena de batalla lo tiene | **Confirmado en juego y corregido (2026-10-21):** sin la config, `HeroAttackSystem` (también el ataque de los héroes IA) y `HeroStaminaSystem` no corrían en batalla. Añadido `HeroGameplayConfig` a las dos subescenas de batalla; test `BattleSubsceneConfigTests` |
| 12 | `Assets/Prefabs/Hero/HeroClassDefinition.prefab` | El prefab del héroe referencia su clase | Apunta a un `HeroClassDefinition` inexistente y hornea una clase vacía: en batalla `HeroInitializationSystem` no inicializaba a nadie | **Confirmado en juego (2026-10-21):** la Fase 1 toma la clase del guardado o de `HeroClassComponent`; **Cerrado (2026-10-27):** se quitó la ruta horneada; `HeroClassManager` es la única fuente (`Docs/Por_Abordar/Pila_Modificadores.md` P2) |

Cierre de la verificación: se confirmaron 13 afirmaciones (Q/E/R/F sin lanzador con matiz de input, huecos de formación,
`NoObstacleAvoidance` y aliados ignorados, `frameCount % 5`, `MatchTimer` en MonoBehaviour, `DamageMechanics.md`
desfasado, perks sin consumidor, dos RNG, FCT dentro del sistema de daño, `isUnderAttack` por proximidad, ×2 fijo de
prisa, `winnerTeam` y captura que retrocede, `CLAUDE.md` desfasado). Una se corrigió (L2-1: el input existe) y L5-3 se
rebajó a parcial porque `UnitSpeedCalculator` ya centraliza la velocidad base.
