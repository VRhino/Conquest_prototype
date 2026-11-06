# Habilidades y perks del héroe

Estado: implementado (2026-10-22, Fase 2 de `Docs/Design/Aprendizajes_Referencia.md`). Patrones tomados del análisis
estático de la referencia (`game-re/notes/skill-lifecycle.md`, `preconditions.md`, `effects-catalog.md`); ningún código ni
dato de la referencia. Ampliado el 2026-10-27 (bloque 3): habilidades de escuadra (L2-9, §3.6), condiciones ricas de los
perks (L2-15), panel de perks del feudo (H5) y uso de habilidades por la IA con elección de aliado (H1, L4-08, §8).

## 1. Resumen

- Una **habilidad** es una lista de **fases** con eventos en instantes concretos, en un blob (`SkillBlob`). El ataque
  básico, las cuatro habilidades de clase y los perks activos son habilidades.
- Todo lanzamiento, del jugador o de la IA, pasa por **una sola puerta** (`SkillGate`), que devuelve el motivo del
  rechazo.
- El **enfriamiento** va por **grupo** y con tiempo absoluto (`CooldownGroupElement`). El grupo 0 no enfría.
- Los **efectos** (daño en área, modificadores temporales, escudo absorbente, embestida) son eventos de fase que aplica
  `SkillEffectSystem`.
- Los **perks** son efectos con condición sobre la pila de modificadores, sin lógica propia. Los pasivos son auras o
  efectos al golpear; los activos son habilidades cortas.
- Las **escuadras** lanzan sus habilidades con la misma puerta, las mismas fases y los mismos efectos, y enfrían en sus
  propios grupos (§3.6).

## 2. Teclas

| Tecla | Hueco (`SkillSlot`) | Qué lanza |
|---|---|---|
| Clic izquierdo | `Basic` | Ataque básico (cadena de 3 golpes) |
| Q / E / R | `Q` / `E` / `R` | Habilidades de clase |
| T | `Ultimate` | Definitiva (2026-10-27: F queda para interactuar) |
| 1 / 2 / 3 | `Squad1` / `Squad2` / `Squad3` | Habilidades de la escuadra activa, en orden de desbloqueo (las lanza la escuadra) |
| 4 / 5 | `ActivePerk1` / `ActivePerk2` | Perks activos del loadout |
| F | — | Interactuar (puntos de suministro, NPC del feudo) |
| `ALT` mantenido (batalla) | — | Menú radial y cursor libre (§6.1) |
| K (feudo) | — | Panel de perks del loadout (H5) |

Con varias teclas pulsadas gana la definitiva, luego R, E, Q, perks y el básico (`HeroSkillRequestSystem`). Las teclas
de escuadra van aparte: se pueden pulsar a la vez que una del héroe, y entre ellas gana la del último desbloqueo.

Mapa definitivo (2026-10-27, H11): la escuadra va en 1/2/3, como pide el GDD §12.3, y los perks activos en 4/5.

## 3. Ciclo de vida

```
Entrada / IA ──► HeroSkillRequestSystem ──► SkillRequestComponent (+ teclas mantenidas)
                                                     │
SkillExecutionSystem ◄───────────────────────────────┘
  1. termina todo si: muerto, fuera de batalla, aturdido o EndSkillsRequestComponent (cambio de escuadra)
  2. avanza la aparcada (sin efectos) y la actual (SkillTimeline.Advance), aplica sus eventos
  3. soltar la tecla de una fase sin fin → fase siguiente (carga)
  4. evalúa la petición con SkillGate; si pasa: cobra aguante y enfriamiento, interrumpe o aparca la actual, empieza la nueva
  5. publica isAttacking, hitboxFired, triggerAttack y WeaponHitboxActiveTag
SkillEffectSystem ◄── SkillEffectRequestComponent (eventos de efecto con pose y equipo del lanzador)
```

### 3.1 Puerta (`SkillGate.Evaluate`), en este orden

1. `NotInBattle`
2. `Dead`
3. `Immobilised`: el aturdimiento por rotura de escudo prohíbe cualquier habilidad. La referencia también prohíbe por
   defecto cuando hay control.
4. `NoSkill`
5. `CurrentNotInterruptible`: la fase actual no deja pasar.
6. `NotChained`
7. `OnCooldown`
8. `Exhausted`
9. `NotEnoughStamina`
10. `NotEnoughCombo`: la habilidad pide más combo del que tiene el héroe (L2-16, abajo).
11. `NotEnoughKills`: la habilidad pide más soldados muertos de los que tiene el héroe (L2-16).

**Combo y bajas (L2-16, 2026-11-06).** Dos recursos del héroe, como en la referencia (un punto de combo del actor y un
contador de bajas de soldado, cada uno con su motivo de rechazo), en `HeroResourcesComponent`:
- **Combo:** +1 por cada golpe del héroe que llega a la vida (melé, flecha o habilidad; no los tics de quemadura), tope 30.
  Tras 6 s sin acertar se pierde a 5 por segundo. La pérdida se calcula al leerlo (`HeroResources.Combo`), sin sistema
  por frame. Ajustes en `HeroGameplayConfigAuthoring.combo` (`ComboTuning`).
- **Bajas:** +1 por cada soldado que mata el héroe; no se pierden con el tiempo.
- **Coste:** `HeroAbility.comboCost` y `killCost` piden y gastan al empezar, como el aguante. Las cuatro definitivas
  cuestan 20 de combo (aprobado por el usuario) y siguen con su enfriamiento de 60 s. Nada cuesta bajas todavía.
- **Quién escribe:** `DamageCalculationSystem` suma al acertar y al matar; `SkillExecutionSystem` gasta al lanzar. La IA
  no elige una habilidad que no puede pagar (`HeroAISkillSystem`).
- **HUD:** no se muestra todavía (`Docs/Por_Abordar/Habilidades_Perks.md` H16).

### 3.2 Fases (`SkillPhase`)

- `duration`. Si es negativa, la fase no termina sola: es la espera de una carga.
- `interruptible`. Solo una fase interrumpible deja entrar a otra habilidad.
- `final`. Es la fase de liberación, siempre la última.
- `charge`. Marca la fase de carga.
- `scalesWithAttackSpeed` (L2-12). La fase dura `duration / AttackSpeed` y sus eventos llegan antes. El ataque básico
  escala entero; las habilidades, solo las fases que lo marquen. Una habilidad de escuadra usa la media de `AttackSpeed`
  de sus soldados vivos, porque la escuadra no tiene ese stat propio y los efectos sobre ella caen en sus soldados.
- El ataque de los soldados (`UnitAttackSystem`) no es una habilidad: su `AttackSpeed` acelera todo su ritmo, el golpe y
  la pausa entre golpes (`attackInterval`). Las ventanas de golpe quedan en el mismo punto del golpe.
- En el Animator, el parámetro `AttackSpeed` multiplica la velocidad de los estados de ataque. Lo tienen el ataque del
  héroe (`swordAttackIsolated` de `AC_Polygon_Masculine`) y los de los soldados: `AC_Pikemen` (`swordAttackIsolated`,
  `Thrust Front`, `Thrust Rear`), `AC_Spearmen` (`swordAttackIsolated`) y `AC_Unit_Combat` de escuderos
  (`Combat/Attack`). `UnitAnimatorController` se lo pasa a cada soldado.

Una habilidad sin fases recibe tres por defecto:
- Preparación, 0,25 s, interrumpible.
- Activa, 0,35 s, no interrumpible.
- Recuperación, 0,4 s, interrumpible y final.

Su `areaRadius` y su `damageMultiplier` se convierten en un golpe en área al empezar la fase activa.

### 3.3 `interrupt` y `yield`

- `Interrupt` (por defecto): la nueva sustituye a la actual.
- `Yield`: si la actual no está en su fase final, se aparca y se restaura cuando la nueva termina.
- Solo hay una aparcada. Su reloj sigue corriendo y sus eventos no se aplican mientras está aparcada.
- Si la aparcada termina mientras está aparcada, no vuelve.
- Al volver conserva su id de instancia, así que sus efectos ligados (L2-14) siguen. Cada lanzamiento toma un id nuevo
  de `SkillInstanceComponent.launches`, que no retrocede al restaurar.
- Los perks activos son `Yield`: no cortan el ataque en curso.

### 3.4 Combo del ataque básico

- **Tres golpes encadenados** (`nextSkillId`). El siguiente se acepta en dos casos:
  - si se pulsa durante la recuperación del golpe anterior;
  - si se pulsa hasta `comboChainWindow` (0,4 s) después de que acabe.
- **Cierre de la cadena:** mientras corre el tercer golpe no se acepta otro básico. Las habilidades sí pueden
  interrumpirlo.
- **Enfriamiento:** solo lo inician el primer y el último golpe, así que no bloquea la cadena.
- **Memoria de entrada:** una pulsación rechazada porque la fase actual no se puede interrumpir se recuerda durante
  `skillInputBuffer` (0,2 s).
- Los tres valores están en `HeroGameplayConfigAuthoring`, junto a `basicComboLength`.

### 3.5 Carga

- **Mecánica:** la habilidad espera en una fase sin fin mientras se mantiene su tecla. Al soltarla, pasa a la fase
  siguiente (`SkillTimeline.Release`).
- **Datos:** Disparo Enfocado (arco, Q) es un tiro cargado: tensar mientras se mantiene Q, disparo al soltar.

### 3.6 Habilidades de escuadra (L2-9)

Ref.: las escuadras lanzan con el mismo ciclo de vida que el héroe. El enfriamiento es por escuadra; la ejecución puede ser por soldado (L2-17, abajo).

- **Datos:** `AbilityData` (`Squads/Ability Data`) tiene fases, eventos y grupo como `HeroAbility`; sin fases, es una
  fase corta que dispara sus eventos al empezar. `SquadData.abilitiesByLevel` las ordena: la posición fija su hueco
  (`Squad1`-`Squad3`, como máximo 3) y su nivel de desbloqueo (`SquadProgressionData.abilityUnlockLevels`, 10/20/30).
  `SquadDefinitionBaker` hornea el blob en `SquadAbilityDefinitionComponent.skill`.
- **Lanzador:** `SquadSpawningSystem` da a la escuadra `SkillRequestComponent`, `SkillInstanceComponent`,
  `HeroSkillElement` y `CooldownGroupElement`. `UnitStatScalingSystem`, que ya llevaba los desbloqueos, añade cada
  habilidad al buffer cuando la escuadra llega a su nivel (al aparecer o al subir).
- **Ejecución:** `SkillExecutionSystem` recorre también las escuadras con la misma `SkillGate` (viva = le queda una unidad
  viva; sin aguante), `SkillTimeline` y `CooldownUtility`. Los efectos salen del centro de sus unidades vivas, mirando
  hacia donde mira su ancla, con su equipo. No hay aparcada ni golpe de arma ni animación (H2).
- **Efectos:** para una escuadra, `Self` y `OwnSquad` son sus soldados; el daño en área usa su perfil de daño
  (`SquadDamageProfileReference`); no hay embestida.
- **Enfriamiento:** por entidad de escuadra. Al cambiar de escuadra y volver, la escuadra reaparece con sus grupos vacíos
  (`Docs/Por_Abordar/Habilidades_Perks.md` H13).

| Escuadra | Nivel 10 (tecla 1) | Nivel 20 (tecla 2) |
|---|---|---|
| Escuderos | Shield Brace: +30 % de defensa a la escuadra (10 s; 40 s) | Shield Bash, por soldado: cada escudero golpea un cono de 90° y 1,8 m delante de él, a un enemigo, ×0,8, y le quita −30 % de velocidad (3 s; 30 s) |
| Arqueros | Focused Volley: +25 % de daño perforante (8 s; 35 s) | Light Step: +25 % de velocidad (8 s; 40 s) |
| Lanceros | Bristling Spears: +20 % de daño perforante y +15 % de defensa (8 s; 40 s) | Coordinated Charge: +20 % de velocidad y de daño perforante (5 s; 45 s) |

Datos en `Assets/ScriptableObjects/Squads/Abilities/`.

**Por soldado (L2-17, 2026-11-06).** Ref.: los soldados lanzan sus propias habilidades por un camino propio. Una habilidad
con `AbilityData.perSoldier` es una orden: la escuadra pasa la puerta y empieza su enfriamiento, pero no ejecuta nada.
Cada soldado vivo recibe un `SoldierSkillComponent` (habilitable) y:
- empieza en un momento propio entre 0 y 0,6 s tras la orden (tirada sembrada por tick y soldado);
- si está a mitad de un golpe o aturdido, espera a quedar libre hasta 1,5 s y, si no, se salta la orden;
- recorre sus fases y lanza los eventos desde su posición y hacia donde mira, como su escuadra (perfil, equipo, efectos).

Los ajustes están en `SquadSpawnConfigAuthoring.soldierOrders` (`SoldierOrderTuning`). El enfriamiento sigue siendo de la
escuadra: la orden siempre la da ella (decisión del usuario). Solo las acciones van por soldado (Shield Bash); las
mejoras de toda la escuadra siguen siendo un único efecto. Desde su momento de empezar hasta el final de su parte, el
soldado no empieza golpes ni disparos normales (`SoldierOrders.HoldsAttacks`): acaba el que lleva y ejecuta la orden;
antes de su momento sigue luchando. Un aturdimiento por resiliencia corta su parte, como corta la habilidad del héroe
(C11). No hay animación propia todavía (H2). Escena de comprobación: `MovementBaselineS9ShieldBash`
(`Logs/MovementMetrics/shield_bash.txt`).

## 4. Efectos (`SkillEventType`)

| Evento | Qué hace |
|---|---|
| `Animation` | Dispara la animación de ataque (provisional hasta tener la propia de cada habilidad) |
| `HitboxOpen` / `HitboxClose` | Abre o cierra el golpe del arma |
| `AreaDamage` | Un `PendingDamageEvent` (categoría `Ability`, perfil del héroe × `value`) a cada enemigo vivo en el círculo o el cono (`radius`, `angle`). El centro va `distance` metros por delante del héroe |
| `ApplyModifier` | Modificador temporal en la pila de cada objetivo (fuente `Effect`, id = la habilidad). Al relanzar sigue `reapply` (L2-10): refrescar (por defecto), apilar hasta `maxStacks` o ignorar. Con `boundToSkill` se retira cuando la habilidad termina o la interrumpen (L2-14) |
| `AbsorbShield` | Escudo absorbente (`value`, `duration`). Si ya hay uno, se queda el mayor |
| `Dash` | Embestida de `distance` metros en `duration` segundos. El héroe local la hace con su controlador y la IA con su `NavMeshAgent` |
| `ApplyConditional` | Modificador condicionado temporal (L1-02, `DamageMechanics.md` §5.6) a cada objetivo: ratio de daño `value` durante `duration`, solo en los golpes que cumplen `condition`. Al relanzar se refresca. Ninguna habilidad lo usa aún |
| `Taunt` | Provocación (L4-05): las escuadras de los enemigos alcanzados apuntan su melé al lanzador (a su escuadra, si es una habilidad de escuadra) con un sesgo `value` durante `duration`. Ninguna habilidad lo usa aún |
| `DamageOverTime` | Daño en el tiempo (L2-11): los enemigos del área (como `AreaDamage`) reciben cada `period` segundos, durante `duration`, un golpe del perfil del lanzador × `value`, forma `Ability`, por la misma fórmula, escudos y suelo. El primer tic llega un periodo después; al relanzar sigue `reapply` y conserva el ritmo |

Comunes a los eventos de área y de efecto:
- `maxTargets` (L2-13): como mucho esos objetivos, los más cercanos al centro primero; 0 = todos. Los efectos lo
  cambian con el stat `MaxTargets` del lanzador, que toma el tope del evento como base: un ratio lo escala y un plano
  suma objetivos (`SkillEvents.Cap`). Siempre queda al menos uno, y un evento sin tope sigue sin tope.
- Duración (L2-10): la de cada efecto se multiplica por `EffectDurationOut` del lanzador y `EffectDurationIn` del objetivo
  (stats de la pila, base 1).
- **Corrección (2026-11-03):** `SkillExecutionSystem` reenviaba solo área, modificador, escudo y embestida, así que
  `ApplyConditional` y `Taunt` no llegaban desde una habilidad real; ahora todos los eventos de efecto pasan por
  `SkillEvents.IsEffect`, para héroes y escuadras.

Un `ApplyModifier` sobre los stats de escuadra (`AIDetectionRange`, `AICombatRadius`, `AIHeroBias`, L4-10, y
`StaminaCost`, M18) se aplica a la escuadra de cada soldado alcanzado, una vez. Un perk de aura `OwnSquad` con uno de esos
stats también va a la entidad de escuadra, no a sus soldados. Los golpes de `AreaDamage` llevan la forma `Ability` y el id de la habilidad, para las condiciones, y tiran la variación
del arma del lanzador (L1-12).

**Objetivos** (`SkillTarget`):
- `Self`: el propio héroe.
- `OwnSquad`: las unidades de su escuadra.
- `AlliesInRadius`: aliados en el radio, con el héroe incluido.
- `EnemiesInRadius`: enemigos en el radio, alrededor del centro del efecto.

### 4.1 Habilidades de clase (datos en `Assets/ScriptableObjects/Hero/Abilities/`)

| Clase | Q | E | R | T |
|---|---|---|---|---|
| Espada y escudo | Empuje de Escudo: embestida 3 m + cono 90° | Defensa Reforzada: +30 % de defensa a aliados (5 m, 8 s) | Intercepción: embestida 4 m + círculo | Muro Imparable: +50 % de defensa a aliados (6 m, 10 s) |
| Espada a dos manos | Corte Giratorio: círculo 3 m ×1,2, como mucho 6 objetivos | Carga Brutal: embestida 6 m + cono 120° | Lluvia de Acero: 3 golpes en cono | Juicio de Acero: cono ×2 + escuadra +20 % de daño y +15 % de velocidad de ataque (8 s) |
| Lanza | Barrido Largo: semicírculo (como mucho 5 golpeados) + ralentiza a todos los enemigos alcanzados | Estocada Precisa: cono estrecho 3,5 m ×1,5 | Despliegue Defensivo: +25 % de defensa a aliados | Muro de Púas: área por delante + ralentización, como mucho 10 objetivos |
| Arco | Disparo Enfocado: tiro cargado (cono estrecho 25 m) | Lluvia de Flechas: 3 oleadas a 15 m, como mucho 8 objetivos por oleada | Flecha Silenciadora: ralentiza (aproximación) | Flecha Llameante: impacto ×0,34 y quemadura de 2 tics ×0,34 (1 s) a 15 m; la quemadura apila hasta 2 |

Siguen la velocidad de ataque (L2-12) todas las fases de Corte Giratorio, Barrido Largo y Lluvia de Acero, porque son
golpes del arma. Los datos de tope, pilas y velocidad salen de la tabla H15 aprobada el 2026-11-04.

Aproximaciones:
- Flecha Silenciadora ralentiza, porque no existe un efecto de "silencio".
- Muro de Púas es daño más ralentización, sin zona persistente.
- Las habilidades del arco usan un área por delante, no un proyectil.

## 5. Perks (L7-4)

- **Datos:** el SO `HeroPerk` lista `PerkEffect`:
  - disparador (`Always` o `OnHit`);
  - condición (`None`, `HeroStill` o `HeroMovedRecently`, con sus segundos);
  - objetivo (propio, escuadra, aliados en radio, objetivo del golpe o enemigos en radio);
  - modificador.
- **Condiciones ricas (L2-15):** se combinan con Y con la de movimiento. Sus valores por defecto significan "no importa",
  así que los assets anteriores no cambian:
  - `chance`: probabilidad entre 0 y 1 (0 = siempre). La tirada es sembrada por tick, héroe, objetivo y perk (L1-05).
  - `targetKind`: el objetivo es un héroe o un soldado.
  - `targetSquadTypes`: el soldado es de alguna de estas clases.
  - `targetHasEffect` y `selfHasEffect`: triestados (no importa, debe, no debe) sobre "tiene un efecto temporal activo en
    `effectStat`" (fuente `Effect`).
  - `squadAliveMin` y `squadAliveMax`: fracción de soldados vivos de su escuadra (un máximo de 0 = sin límite).

  En las auras, lo del héroe se mira una vez y lo del objetivo filtra cada objetivo. Lógica pura en `PerkConditions`; el
  contexto lo reúne `PerkContext`. Ningún perk usa aún las condiciones nuevas (H14).
- **Selección:** `PerkLoadout` toma del loadout activo (`HeroData.loadouts[].perkIDs`) hasta 5 pasivos y 2 activos, entre
  los perks de la clase. Los héroes remotos y la IA no tienen perks de loadout.
- **Panel del feudo (H5):** la tecla K abre `PerkSelectionPanel`. Es un prefab uGUI en `Resources/UI` que
  `FullscreenPanelManager` instancia y registra, sin tocar la escena. Lista los perks de la clase que el héroe tiene
  desbloqueados (`isUnlockedByDefault` o `HeroData.unlockedPerks`) y muestra los contadores 5 + 2. Cada botón añade o
  quita el perk en `perkIDs` del loadout activo (lo crea si no hay ninguno) y guarda al momento. Reglas en
  `PerkSelection`. El árbol de talentos y sus puntos, más adelante.
- **Movimiento:** `HeroMotionSystem` mide cuánto lleva quieto el héroe y desde cuándo no se mueve.
- **Auras:** `PerkAuraSystem` aplica los efectos `Always`. Mientras se cumple la condición, refresca cada 0,25 s un
  modificador que dura el doble (fuente `Perk`). Así sigue a la escuadra y se apaga solo.
- **Al golpear:** `PerkOnHitSystem` aplica los efectos `OnHit` al objetivo de cada golpe del héroe, antes de que se
  calcule el daño.
- **Activos:** cada perk activo es una habilidad de una fase que aplica sus efectos, con el enfriamiento del perk y
  refresh `Yield`.

| Perk | Tipo | Efecto implementado |
|---|---|---|
| Línea Inquebrantable | Pasivo | Héroe quieto 2 s → su escuadra +10 % de defensa, y los efectos que recibe el héroe duran un 30 % menos (`EffectDurationIn`) |
| Cobertura de Hostigamiento | Pasivo | Héroe quieto 3 s → sus golpes ralentizan al objetivo un 15 % (2 s) |
| Estocada Desestabilizadora | Pasivo | Tras moverse → el golpe ralentiza al objetivo un 30 % (1,5 s) |
| Orden de Resistencia | Activo (30 s) | Aliados a 8 m +15 % de defensa (6 s) |
| Carga Coordinada | Activo (25 s) | Héroe y escuadra +20 % de daño (3 s) |
| Flecha Marcadora | Activo (20 s) | Enemigos a 12 m −10 % de defensa (6 s); durante 6 s los efectos que aplica el héroe duran un 20 % más (`EffectDurationOut`). La marca de este lanzamiento no se alarga: el stat se resuelve en el frame siguiente |
| Muro de Púas, Zona de Exclusión | Pasivo | Sin datos: piden daño por contacto y zonas persistentes |

## 6. HUD

`UIBattleDataSystem` escribe en `UIHeroBattleDataComponent.skills` un hueco por `SkillSlot`, de Q a `Squad3`: la habilidad
(`skillId`, 0 si está vacío o sin desbloquear), la fracción de enfriamiento y los segundos que faltan. Q, E, R, T y los perks
activos 4 y 5 salen del héroe local; 1, 2 y 3, de su escuadra activa (`IsLocalSquadActive`).

`HUDController` crea la barra de habilidades desde `Resources/UI/SkillBar` (`SkillBarController`, H6 y H10), sin huecos en la
escena, abajo en el centro y por debajo de la barra de aguante: nueve huecos con icono, enfriamiento radial, segundos y tecla. Los huecos vacíos del héroe se atenúan y los de
escuadra sin desbloquear se ocultan. Los iconos los busca `SkillIconCatalog` por el id del skill: habilidades y perks de la
clase del héroe (`HeroClassManager`) y habilidades de todas las escuadras (`SquadDataService`).

### 6.1 Clic y menú radial (H10)

En batalla el cursor va bloqueado. **Mantener `ALT`** lo libera (`BattleCommandInput.PointerMode`), quieta la cámara y
muestra el menú radial; al soltarlo todo vuelve a su estado. El juego no se pausa: el héroe sigue andando y las teclas
siguen funcionando. Con el cursor libre:

- **Barra:** un clic en un hueco de escuadra lanza esa habilidad. Solo los de escuadra reciben clics: el GDD §4.6 pide
  activar desde el HUD las habilidades de escuadra, y las de clase y los perks activos siguen con sus teclas.
- **Radial:** habilidades de escuadra arriba y formaciones `F1-F4` abajo. Se elige con clic o soltando `ALT` sobre un
  sector. Soltar en el centro, o fuera de la rueda, cancela.
- **Sin ataque:** con `ALT` pulsado el clic izquierdo no ataca, y si el botón sigue pulsado al soltar `ALT`, tampoco hasta
  soltarlo. Con `ALT` pulsado no se elige nada mientras el héroe está muerto o hay un panel abierto.

**Camino:** la UI solo llama a `BattleCommandInput`, que no toca ECS. `HeroInputSystem` convierte la petición en un frame de
`UseSquadAbilityN`, igual que la tecla, y `HeroSkillRequestSystem` y `SkillGate` hacen el resto. `SquadControlSystem` trata la
formación como `F1-F4`. Un hueco vacío o en enfriamiento no emite nada; el enfriamiento se ve en el mismo hueco, también
en el radial.

`BattleRadialMenu` es un prefab de `Resources/UI` que crea `HUDController`, sin huecos en la escena. La geometría de los
sectores está en `RadialMenuLayout` y sus radios en el prefab. Alt+Tab cierra el menú sin lanzar nada.

## 7. Archivos

| Archivo | Rol |
|---|---|
| `Skills/SkillTypes.cs` | Huecos, fases, eventos, blob y buffer de habilidades del héroe |
| `Skills/SkillBlobBuilder.cs`, `SkillLibrary.cs` | Construcción y caché de blobs (habilidad, básico, perk) |
| `Skills/SkillGate.cs`, `SkillTimeline.cs` | Puerta y avance de fases (puros) |
| `Skills/HeroSkillRequest.System.cs`, `SkillExecution.System.cs` | Petición y ejecución |
| `Skills/SkillEffect.System.cs`, `SkillArea.cs`, `HeroAIDash.System.cs` | Efectos |
| `Skills/Cooldown.cs` | Grupos de enfriamiento y `CooldownScaleComponent` |
| `Perks/PerkTypes.cs`, `PerkLoadout.cs`, `HeroMotion.System.cs`, `PerkAura.System.cs`, `PerkOnHit.System.cs` | Perks |
| `Perks/PerkContext.cs` | Contexto de las condiciones ricas (L2-15) |
| `Perks/PerkSelection.cs`, `UI/Perks/PerkSelectionPanelController.cs`, `PerkSelectionRow.cs` | Elección de perks en el feudo (H5) |
| `Squads/AbilityData.cs`, `Squads/SquadDefinitionBaker.cs`, `Squads/Systems/UnitStatScaling.System.cs` | Habilidades de escuadra: datos, horneado y desbloqueo (L2-9) |
| `UI/SkillBar/SkillBarController.cs`, `SkillBarSlot.cs`, `SkillIconCatalog.cs` | Barra de habilidades (H6, H10) |
| `UI/Battle/BattleCommandInput.cs`, `BattleRadialMenu.cs`, `RadialMenuLayout.cs` | Clic y menú radial de `ALT` (H10) |
| `Hero/AI/HeroAISkills.cs`, `Hero/AI/Systems/HeroAISkill.System.cs` | La IA usa habilidades y elige aliado (H1, L4-08) |

## 8. IA (H1, L4-08)

- **Cuándo piensa:** `HeroAISkillSystem` corre en cada pensamiento del héroe (`AIThinkClock`), después del
  comportamiento (Rusher o Balanced) y antes de `HeroSkillRequestSystem`. Añade a `HeroAIDecision` dos cosas: la
  habilidad de clase que lanzar (`useSkill`, `skillSlot`) y la habilidad de escuadra (`useSquadSkill`,
  `squadSkillSlot`). La petición pasa por la misma puerta que la del jugador.
- **Qué mira:** solo habilidades que la puerta dejaría pasar ahora (enfriamiento y aguante), en el orden F, R, E, Q.
- **Para qué sirve cada habilidad** (`HeroAISkills.PurposeOf`, según sus eventos):
  - ofensiva (daño en área o efecto a enemigos): con un héroe enemigo o `minEnemyUnits` soldados a su alcance;
  - sobre sí mismo o su escuadra: mientras combate;
  - sobre aliados en radio: cuando el aliado elegido está a su alcance y le falta al menos `supportMinMissingHealth` de
    vida.
- **Aliado (L4-08):** se puntúa cada aliado dentro de `allySearchRadius` por cercanía, vida perdida y si es héroe.
  El elegido (`HeroAISupportTarget`) gana un bonus de `allyHysteresis`: otro tiene que superarlo claramente para quitarle
  el puesto.
- **Escuadra:** mientras su escuadra combate, la IA pide la primera habilidad de escuadra lista.
- **Ajustes:** `SquadSpawnConfigAuthoring.heroAISkills`.
- **Pendiente:** acercarse al aliado elegido, apuntar el cono hacia el grupo de enemigos y elegir las habilidades de
  escuadra según su propósito (H12).
