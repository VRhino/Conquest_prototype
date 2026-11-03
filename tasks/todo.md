# Plan: efectos de habilidad del catálogo (L2-13, L2-14, L2-10, L2-11, L2-12)

Estado: en curso (2026-11-03), rama `main` local, sin subir. Catálogo: `Docs/Design/Aprendizajes_Referencia.md` §4.2.

## Decisiones del usuario (2026-11-03)
- **L2-12:** parámetro float `AttackSpeed` en el Animator del héroe (`AC_Polygon_Masculine`) como multiplicador de los
  estados de ataque; la capa visual lo escribe desde ECS.
- **Datos:** Flecha Llameante pasa de 3 pulsos directos a quemadura (daño en el tiempo, L2-11). El resto, sin datos.

## Diseño
- **L2-13:** `SkillEvent.maxTargets` (0 = todos): los objetivos de un área se toman de más cercano a más lejano.
- **L2-14:** `SkillEvent.boundToSkill`: el modificador lleva la instancia que lo creó y se retira cuando la habilidad termina
  o la interrumpen (evento de fin de instancia y limpieza).
- **L2-10:** `StatModifier.stacks` (0 = 1, compatible); `SkillEvent.maxStacks` y modo al reaplicar (refrescar, apilar,
  ignorar). Stats `EffectDurationOut` (lanzador) y `EffectDurationIn` (objetivo), multiplicadores de la duración.
- **L2-11:** evento `DamageOverTime` (valor por tic, `period`, `duration`); buffer en el objetivo y sistema que emite un
  `PendingDamageEvent` por tic (forma Habilidad), con el perfil del lanzador.
- **L2-12:** stat `AttackSpeed` (base 1) en `CombatStatsComponent`; `SkillPhase.scalesWithAttackSpeed`; `SkillTimeline`
  avanza esas fases a `dt × velocidad`; la capa visual pone `AttackSpeed` en el Animator.

## Pasos (un commit por punto o pareja)
- [x] 1. L2-13 + L2-14; tests; EditMode, S6.
- [ ] 2. L2-10; tests; EditMode.
- [ ] 3. L2-11 + Flecha Llameante; tests; EditMode.
- [ ] 4. L2-12 + Animator; tests; EditMode y comprobación en Play.
- [ ] 5. Docs: `SkillsAndPerks.md`, catálogo §5, `Por_Abordar`, lecciones.

---

# Plan: movimiento del catálogo (L5-7, L5-5; L5-6 y L5-8 bloqueados)

Estado: **hecho** (2026-11-03), rama `main` local, sin subir. Catálogo: `Docs/Design/Aprendizajes_Referencia.md` §4.5.

## Decisiones del usuario (2026-11-03)
- **L5-7:** aguante por escuadra. Correr gasta por segundo según los soldados que corren; andar y parar recuperan. Sin
  aguante, la escuadra no corre hasta recuperar un umbral. Valores en `SquadData`, neutros (sin coste) hasta rellenarlos.
- **L5-5:** radio de cuerpo por tipo de escuadra en datos (0 = el de `MovementTuningConfig`, 0,5 m, como hoy), usado por
  pareja (rA + rB) en contacto, evitación, empuje, anillo de contacto y alcance.
- **L5-6 y L5-8:** bloqueados por falta de contenido de caballería; documentados.

## Pasos (un commit por punto)
- [x] 1. L5-7: `SquadStaminaComponent`, `SquadStaminaSystem` y marcha limitada en `UnitFollowFormationSystem`; tests; EditMode, S1/S4/S6.
- [x] 2. L5-5: `UnitBodyRadiusComponent` y radios por pareja; tests; EditMode, S1/S5/S6/S7/S9.
- [x] 3. Docs: catálogo §5, `Movimiento_Formaciones.md`, `Por_Abordar` (L5-6, L5-8), lecciones.

---

# Plan: IA del catálogo (L4-10, L4-05, L4-03, L4-06; L4-07 aplazado)

Estado: **hecho** (2026-11-03), rama `main` local, sin subir. Catálogo: `Docs/Design/Aprendizajes_Referencia.md` §4.4.

## Decisiones del usuario (2026-11-03)
- **L4-03:** el patrón de la referencia por pareja escuadra → enemigo (no por soldado): rayos de física contra el escenario
  (capas en config), cursor round-robin y presupuesto global por frame repartido por demanda; un enemigo nuevo empieza
  visible. La detección solo publica enemigos visibles y los arqueros exigen línea de tiro.
- **L4-05:** mecánica de sesgo de aggro con caducidad + evento de habilidad `Taunt`, sin asignarlo aún a ninguna habilidad.
- **L4-06:** marca `[ServerAuthoritative]` en los sistemas que deciden y test de arquitectura; sin mover el orden.
- **L4-07:** aplazado y documentado: hace falta medir en Player con escuadras de sobra antes de un LOD.
- **L4-10 (mi elección, sin pregunta):** alcance de detección, radio de combate y sesgo hacia héroes como stats de la pila
  de la escuadra (fuente `Effect`).

## Pasos (un commit por punto)
- [x] 1. L4-10: stats de IA de escuadra en la pila, consumidos por detección y objetivo; tests; EditMode y S6/S7.
- [x] 2. L4-05: `AggroBias` con caducidad en la puntuación de objetivo y evento `Taunt`; tests; EditMode y S6.
- [x] 3. L4-03: visibilidad por pareja con presupuesto; detección y arqueros; tests; EditMode y S1/S6/S7/S9.
- [x] 4. L4-06: marca y test de arquitectura.
- [x] 5. Docs: catálogo §5, `Movimiento_Formaciones.md` o doc de IA, `Por_Abordar` (L4-07), lecciones.

---

# Plan: formaciones del catálogo (L3-05, L3-06 y compactar huecos de L3-02)

Estado: **hecho** (2026-11-03), rama `main` local, sin subir. Catálogo: `Docs/Design/Aprendizajes_Referencia.md` §4.3.

## Decisiones del usuario (2026-11-03)
- **L3-05:** plazos como hora de fin. La orden resuelta lleva `expiresAt`; la insistencia del jugador y su ventana pasan
  a horas absolutas de simulación (T2). Mismo comportamiento. La IA no gana órdenes que caduquen y la retirada no cambia.
- **Huecos:** se compactan con una espera corta (config) tras la baja, agrupando las bajas seguidas. Solo se mueven los
  soldados que ocupan ranuras sobrantes (de prioridad menor que el número de vivos), cada uno al hueco libre más cercano.

## Diseño
- **L3-05:** `SquadPlayerOrderIntentComponent` cambia `insistenceTimer`/`heroOrdenCooldownTimer`/`heroOrdenCooldownActive`
  por `insistenceEndsAt` y `overrideEndsAt` (double). `SquadPlayerCommandResolver.TryApply` recibe `now`.
  `SquadControlSystem` deja de descontar. `OrderResolutionSystem` decide la insistencia con `now < overrideEndsAt` y la
  publica en `SquadResolvedOrderComponent.expiresAt` (0 = hasta que la sustituyan). `SquadFSMSystem` lee la orden
  resuelta en vez de la intención del jugador.
- **L3-06:** `FormationChangePending` (enableable) en cada escuadra. La encienden `OrderResolutionSystem` (formación pedida
  distinta de la actual), `UnitDeathSystem` (baja) y `FormationSystem` al ver una petición de espaciado.
  `FormationSystem` solo recorre las escuadras con la marca y la apaga cuando no queda nada pendiente. El enfriamiento del
  cambio de formación pasa a hora absoluta (`formationChangeReadyAt`).
- **Huecos:** en `FormationSystem` (único escritor de las ranuras). Con la misma plantilla y menos vivos, programa
  `FormationComponent.compactAt = ahora + formationCompactDelay` (`MovementTuningConfig`) y, al llegar, reparte los
  sobrantes a los huecos por cercanía (regla pura `FormationLayout.CompactSlots`).

## Pasos (un commit por punto)
- [x] 1. L3-05: plazos absolutos y `expiresAt`; tests; EditMode y S1/S6.
- [x] 2. L3-06: marca de cambio pendiente y enfriamiento absoluto; tests; EditMode y S1/S4/S6.
- [x] 3. Huecos: compactación con espera; tests; EditMode y S4 con bajas (BL/BT/BSL/BSW), S1, S6, S9.
- [x] 4. Docs: `Movimiento_Formaciones.md`, `Formations.md`, catálogo §5, `Por_Abordar`, lecciones.

---

# Plan: combate pendiente del catálogo de la referencia (L1-08, L1-10, L1-07, L1-12, L1-13, L1-02, L1-11, L1-14)

Estado: **hecho** (2026-11-03), rama `main` local, sin subir. Catálogo: `Docs/Design/Aprendizajes_Referencia.md` §4.1.
L1-09 (reacciones al golpe) queda fuera: necesita animaciones; se documenta como bloqueado.

## Decisiones del usuario (2026-11-03)
- **L1-02:** el conjunto más completo del patrón: lado atacante o receptor; héroe o soldado; tipo de escuadra y tipo de
  unidad (Infantry/Cavalry/Distance, `SquadData.unitType`); forma del golpe (melé, distancia, habilidad); habilidad del
  golpe (lista de ids); probabilidad sembrada; efecto activo propio y del otro (triestados); golpe crítico (triestado);
  vida del receptor en un rango; armadura total del otro en un rango. Todo con Y; listas "cualquiera de".
- **L1-11:** misma estructura que la referencia (umbral del arma, peso `min(d/X, 1)`, tope) con `d` = metros recorridos
  hacia el objetivo justo antes del golpe.
- **L1-14:** un terreno por mapa (`MapDataSO`), declarado por la batalla; cada tipo de escuadra declara sus modificadores.
- **L1-07:** `MinHealthRatio` como stat de la pila, escrito por `StatResolveSystem` en `HeroHealthComponent`; al
  reaparecer, un modificador temporal (ratio y segundos en `HeroGameplayConfig`).

## Diseño
- **Rasgos del golpe** (`HitTraits`, serializable): van dentro de `DamageProfileComponent`, así viajan con el perfil en
  línea de melé, proyectiles y habilidades. Fuente: `SquadMeleeData`/`SquadRangedData` (soldados) y `HeroClassDefinition`
  (héroe). Valores por defecto neutros (0): ningún asset cambia de comportamiento sin datos. `StatResolveSystem` conserva
  los rasgos al reescribir el perfil.
- **L1-08/L1-10:** `ignoresShield` (el golpe no se bloquea), `blockDamageRatio` (daño al bloqueo = efectivo × (1 + r)) y
  `staminaDamageRatio` (aguante que pierde un héroe que bloquea = efectivo × r). Regla pura `ShieldBlock.Resolve`.
- **L1-07:** `StatId.MinHealthRatio`; `HealthFloor.Apply` = `max(vida − daño, min(vida, suelo · máx))`.
- **L1-12:** `damageVariance` f del arma; el creador del golpe tira `1 ± f` con `CombatRandom` (sal propia, con el objetivo
  en la semilla) y lo guarda en `PendingDamageEvent.varianceOffset` (0 = neutro); la fórmula lo multiplica al final.
- **L1-13:** `StatId.LifeSteal`, derivado en `CombatStatsComponent` (nuevo, lo escribe `StatResolveSystem`); cura al
  atacante la fracción de la vida realmente quitada, después de restarla.
- **L1-02:** buffer `ConditionalModifier {condición, ratio de daño, fuente, id, caducidad}`; `DamageCalculationSystem` suma
  los del atacante (lado Atacante) y del receptor (lado Receptor) que cumplen y multiplica el daño por `(1 + Σ)` una vez
  (regla T1). Lógica pura en `HitConditions`. Fuentes: `SquadData` (permanente, en cada soldado) y un evento de
  habilidad `ApplyConditional` (temporal). El evento de daño lleva `hitForm` y `skillId`.
- **L1-11:** `ChargeTrackSystem` muestrea la posición de cada atacante (ventana en config); la melé guarda en el evento
  la distancia recorrida hacia el objetivo; la fórmula aplica `1 + peso · maxBonus` si supera el umbral del arma.
- **L1-14:** `BattleTerrain` (enum) en `MapDataSO`; `BattleBootstrapSystem` crea el singleton `BattleTerrainComponent`;
  `SquadData.terrainModifiers` (terreno, stat, op, valor) entra en la pila con `ModifierSource.Terrain` al aplicar los
  stats de cada soldado (y del perfil de la escuadra si el stat es de daño).
- Sin números mágicos: rasgos en el SO del arma; ventana de carga y protección al reaparecer en config.

## Pasos (un commit por punto o pareja)
- [x] 1. L1-08 + L1-10: `HitTraits`, `ShieldBlock`, aguante del héroe; tests de la regla; EditMode, S1/S6.
- [x] 2. L1-07: stat `MinHealthRatio`, suelo en el daño, protección al reaparecer; tests.
- [x] 3. L1-12: variación sembrada en melé, proyectil y habilidad; tests (reproducible y acotada).
- [x] 4. L1-13: `LifeSteal`, `CombatStatsComponent`; tests.
- [x] 5. L1-02: condiciones, buffer, fuentes; tests de cada condición y de la suma.
- [x] 6. L1-11: muestreo de carga y bono; tests.
- [x] 7. L1-14: terreno del mapa y modificadores por tipo de escuadra; tests.
- [x] 8. Docs: `DamageMechanics.md`, catálogo §5, `Docs/Por_Abordar` (lo que quede abierto y L1-09 bloqueado), lecciones.

## Resultado
- Commits: `5fb4defc` (L1-08 + L1-10), `f84c7a17` (L1-07), `6c115b83` (L1-12), `84463226` (L1-13), `d2007943` (L1-02),
  `579f81ff` (L1-11), `4974ca7d` (L1-14) y el de docs. EditMode 451/451; PlayMode S1 y S6 en verde tras cada punto, y S9
  con la carga.
- Toda la mecánica es neutra sin datos: ningún asset rellena aún rasgos, condicionados ni terrenos, así que el balance actual
  no cambia. Lo abierto (valores, terrenos de los mapas, perks con condicionados, L1-09 bloqueado) está en
  `Docs/Por_Abordar/Combate.md` C1-C8.

---

# Plan: H10, lanzar habilidades de escuadra con el ratón y menú radial de `ALT`

Estado: **hecho** (2026-11-02), rama `main` local, sin subir.

## Decisiones
- Aprobadas por el usuario: `ALT` se mantiene (no se alterna), sin pausa; radial con habilidades de escuadra y formaciones
  (GDD §12.3); se elige con clic o soltando `ALT` sobre un sector, y soltar en el centro cancela; la barra solo se puede
  clicar con `ALT` mantenido; con `ALT` pulsado ningún clic ataca.
- Clic solo en los huecos de escuadra: el GDD §4.6 pide activar desde el HUD las habilidades de escuadra. Las de clase y
  los perks activos (§6.2) siguen con teclas.

## Diseño
- `BattleCommandInput` (estático, como `DialogueUIState`): `PointerMode` y una petición pendiente de un frame. La UI solo
  escribe aquí.
- `HeroInputSystem` convierte la petición en `UseSquadAbilityN` durante un frame: es la misma entrada que la tecla, así
  que pasa por `HeroSkillRequestSystem` y `SkillGate`. `SquadControlSystem` hace lo mismo con `F1-F4`.
- `HeroInputSystem` no ataca con `PointerMode` ni si el botón sigue pulsado al salir de él.
- `HeroCameraController` deja el cursor libre y no gira ni hace zoom con `PointerMode`; sigue al héroe.
- `SkillBarSlot` recibe el clic (`IPointerClickHandler`); `SkillBarController` lo traduce. Un hueco en enfriamiento no emite.
- `BattleRadialMenu`: prefab en `Resources/UI`, lo crea `HUDController`, sin tocar escenas. Lee `ALT`, publica `PointerMode`
  y emite. Geometría pura en `RadialMenuLayout`. Lo que muestra sale de `UIHeroBattleDataComponent`.

## Pasos
- [x] 1. Puente, entrada, cámara y clic en la barra, con tests EditMode.
- [x] 2. Radial: datos, controlador, geometría, prefab y tests.
- [x] 3. Integración BattleScene: clic y radial lanzan de verdad, enfriamiento, sin ataque.
- [x] 4. Play con capturas y resoluciones pequeñas (radial, cartel de interacción, suministro, perks).
- [x] 5. Docs: H10, `SkillsAndPerks.md`, README de Por_Abordar, GDD, lecciones.

---

# Plan: presentación de la muerte del héroe (animación, retirar el cuerpo y reaparecer)

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
- EditMode: 353 de 353 en verde (dos tests nuevos en `HeroLifecycleRegressionTests`; `SystemOrderTests` en verde), también tras la corrección.
- PlayMode: `BattleSceneIntegrationTests`, 11 de 11 en dos corridas seguidas tras la corrección (morir → animar → fuera de la jerarquía → visual nuevo en la pose de
  spawn; el remoto se retira y no vuelve).
- De paso: `EntityVisualSync.Update` ya no llama a `Exists` con el mundo destruido. Ensuciaba el `SetUp` del test
  siguiente con `ObjectDisposedException`.
- Pendiente (clip real, colisión y agente del cuerpo oculto, otros controladores): `Docs/Por_Abordar/Muerte_Heroe.md`.

## Corrección del usuario (2026-10-27): retirar el cuerpo, no solo ocultarlo
"No basta con ocultarlo, hay que sacarlo de la jerarquía y que cuando spawnee entre de nuevo; así nos evitamos la
colisión."
- [x] `HeroVisualInstantiationSystem` destruye el visual de los héroes muertos cuando `visualHidden` (lo desactiva en
      el acto) y quita `HeroVisualInstance`, `HeroVisualAppearanceApplied`, `NavAgentComponent` y `NavMeshAgent`. Su
      pasada normal lo vuelve a crear en la pose de spawn al reaparecer. `HeroDeathPresentationSystem` va antes.
- [x] Fuera `HeroDeathVisualPresenter` (apagaba los renderers).
- [x] Tests de PlayMode: el local sale de la jerarquía y entra un visual nuevo, con motor local, en la pose de spawn;
      el remoto se retira sin agente y no vuelve.
- [x] Docs: D2 y D3 hechos y D5 nuevo (coste de recrear) en `Docs/Por_Abordar/Muerte_Heroe.md`.

# Bloque 4 (2026-10-27): H9, H8, P2, H7 + H13

- [x] H9: `HUDController.OnDestroy` no libera consultas si el mundo ya no existe.
- [x] H8: quitar "(Passive)" del `perkName` de los tres perks activos (assets LFS).
- [x] H7: el cambio de escuadra usa el grupo `SquadSwapCooldown.Group` del `CooldownGroupElement` del héroe; fuera
      `SquadSwapCooldownComponent` y `SquadSwapCooldownSystem`.
- [x] H13: `InactiveSquadElement.cooldowns` guarda los grupos activos de la escuadra al retirarla (cambio o muerte del
      dueño) y `SquadSpawningSystem` los restaura.
- [x] P2, opción (a) del usuario: fuera la ruta horneada (prefab de clase, authorings, componente en `HeroEntity_Pure`,
      componentes sin lector y `HeroAttributeSystem`, con lo que P4 también queda cerrado). `HeroClassManager`, única fuente.
- [x] EditMode en verde (355 de 355) y commit.

# Barra de habilidades (2026-10-27): H6 + H10

- [x] `UIHeroBattleDataComponent.skills`: un hueco por `SkillSlot` (Q..Squad3) con skill, fracción y segundos; lo
      escribe `UIBattleDataSystem` desde el héroe local y su escuadra activa.
- [x] `SkillBarController` + `SkillBarSlot` + `SkillIconCatalog`; prefab `Resources/UI/SkillBar` creado con `eval_file`;
      `HUDController` lo crea (sin tocar `BattleScene`) y quita sus cuatro huecos sin conectar.
- [x] Tests: `SkillBarDataTests` (EditMode) y `BattleSceneShowsTheSkillBarWithTheHeroSkills` (PlayMode).
- [x] EditMode 357/357; BattleSceneIntegrationTests 12/12. Sin revisar a ojo en partida.
- [x] H11 (2026-10-27, decisión del usuario): escuadra en 1/2/3 (GDD §12.3), perks activos en 4/5; la barra pone la
      escuadra delante de los perks.

# Contacto enemigo en la melé (2026-10-28): P0 + P1 de `game-re/notes/battle-movement-combat.md`
- [x] P0: `UnitBodyblockSystem` con una sola distancia de contacto enemigo (2·bodyRadius) y sin tocar al quieto.
- [x] P1: `EnemyContact` ("chocar y resbalar") al final de `UnitAvoidanceSystem`; interruptor `enemyContactEnabled`.
- [x] Tests: `EnemyContactTests` (EditMode); escenarios S6/S7 `LegacyContact` para comparar.
- [x] Medida (3+3 corridas intercaladas): solapes enemigos > 0,3 s S6 9 → 0, S7 12 → 0; picos S6 1,8 → 1,5 %.
- [x] P3: el retroceso del golpe recorta su paso contra enemigos y corre antes del bodyblock (neutro; solapes en 0).
- [x] P2: `SquadEnemyCollisionComponent` por escuadra y `passesEnemies` por golpe; el par choca solo si los dos lo tienen.
- [x] P4: `EnemyContact` dice quién bloquea (±30°), `UnitAvoidanceSystem` lo guarda en `UnitBlockedComponent` y
  `UnitTargetingSystem` vuelve a puntuar con él y un bonus antes de la saturación tras `blockedRetargetTime` (0,5 s),
  para formar líneas de combate. Variantes S6/S7 `NoBlockedRetarget` para comparar.
- [x] P5: mientras golpea, la unidad cuerpo a cuerpo solo frena (`attackMovementLock`). Velocidad al golpear S6 0,52 → 0,25 m/s;
  primer golpe igual. Variantes S6/S7 `NoAttackLock`.
- [x] P7: escenario S9 (dos líneas de frente) y métricas de línea en el grabador. Solapes 10 → 0, pero filas de atrás por
  delante de la primera 4 → 17 % y primer golpe +0,8 s.
- [x] S9 con los héroes fuera de la melé. Un Attack ordenado ya no lo pisa la reacción de combate (seguía atado a la correa y las escuadras volvían con su héroe). Rebase en melé real: con regla 0 / 0 / 16 / 25 %, sin ella 3 / 9 / 17 / 18 / 38 %.
- [x] `SquadNavigationRegressionTests` 13/13: 5 tests seguían dando la plantilla a sistemas que desde C13 (3000830d) leen `UnitResolvedSlotComponent`; las unidades no entraban en la consulta. Solo tests, sin cambios de juego.
- [x] Rebase de filas traseras cerrado: lo que quedaba eran extremos de la segunda fila alargando la línea o llenando huecos. `rearThroughFrontRatio` 0 % en 5 corridas; regla `rearRankTargetDepth` retirada (sin efecto medible).
- [ ] Siguiente: P6 solo si vuelven los picos.

# F1 + F17 (2026-11-01): plantillas por soldados vivos y ocupación entre escuadras aliadas

Lo que el código dice y cambia el planteamiento de `Docs/Por_Abordar`:
- Desde C12 el reparto al cambiar de plantilla es voraz por cercanía, no "ranura k = soldado k". Tras bajas los índices de
  ranura ya son dispersos, así que volver a k = k no es posible sin compactar: se mantiene el voraz y las plantillas se anidan.
- `SelectTemplate` exige `slots >= vivos`: un `minUnits` literal 15 con 15 ranuras caería en la plantilla mayor con 14 vivos.
  Cada plantilla de N ranuras lleva `minUnits` = siguiente N + 1 (20: 16-20, 15: 11-15, 10: 6-10, 5: 1-5).
- Con menos soldados que ranuras se llenan las n primeras: el orden del asset es la prioridad. Hay que ordenar las ranuras
  (delante y al centro primero) o un cambio de formación con 17 vivos deja la cuña sin punta.
- Squires son 12: plantillas 12, 10 y 5. Las de 20 y 15 no se usarían nunca.
- S5 no existe: hay que construirla (dos escuadras aliadas por un pasillo de 4 m).

Pasos:
- [x] 1. Métricas: evento de cambio de plantilla (recorrido, cruces, tiempo) y celdas compartidas entre escuadras aliadas.
- [x] 2. Escenarios S4Bajas (Squires y Spearmen, soldados muertos por script) y S5; medir la línea base.
- [x] 3. F1: plantillas, orden de ranuras, editor con plantillas superpuestas y test de coherencia. Medir S4Bajas, S1, S6.
- [x] 4. F17: `FormationSlotResolver` admite celdas ocupadas; `FormationSlotResolveSystem` en orden fijo y solo con aliados
      cercanos. Tests EditMode. Medir S5.
- [x] 5. Regresión S1, S4, S6, S7, S9.
- [x] 6. Docs: `Por_Abordar` (F1, F2, F7, F9, F17, README), catálogo §5, `Movimiento_Formaciones.md`, lecciones.

Estado: **hecho** (2026-11-02), rama `main` local, sin subir: `b355b32c` (métricas), `1eb56b03` (F1), `8f3b45cf` (F17).
Formas de F1 aprobadas por el usuario. Resultado en `Docs/Design/Movimiento_Formaciones.md` §23.

---

# Plan: HUD de batalla sin solapes en pantallas pequeñas

Estado: **hecho** (2026-11-02), rama `main` local, sin subir.

## Problema
En BattleTestScene, a 1280x720, los iconos de formación del panel de squad caen encima de las ranuras Q y E. Medido en
Play: barra x 387..893, y 5..53; `FormationsSections` x 375..556, y 27..68. Su canvas sigue en Constant Pixel Size
(escala 1) y `SkillBar` escala con la pantalla (0,667). BattleScene ya tiene el scaler correcto y a esa resolución deja
17 px de separación, pero `SkillBar` usa otro modo de match y se pisa con el aguante con aspecto < 1,57 o > 2,0.

## Solución (la de Feudo y BattlePrep)
`CanvasScaler` en `Scale With Screen Size`, 1920x1080, `Expand`, y contenedores estirados en vez de tamaño fijo. Con
Expand el canvas lógico nunca baja de 1920x1080, así que lo diseñado a esa medida cabe siempre. No hay Safe Area.

Pasos:
- [x] 1. BattleTestScene: scaler de Feudo e `InteractionPromp` en `y=92`, como en BattleScene (`de62733c`).
- [x] 2. `SkillBar` y `PerkSelectionPanel`: `Expand`.
- [x] 3. `SupplyPointUi`: raíz estirada en el prefab y overrides de las dos escenas.
- [x] 4. Test EditMode: los canvas de batalla comparten scaler (el HUD está duplicado y ya se desincronizó una vez).
- [x] 5. Capturas en Play de las dos escenas: 1920x1080, 1600x900, 1366x768, 1280x720, 1280x800, 1024x768 y 2560x1080.
      Revisar también el contenido dinámico y la legibilidad a 1024x768.
- [x] 6. EditMode (421/421) y `BattleSceneIntegrationTests` (18/18) en verde, lecciones y commit.

Notas:
- Sin prefab del HUD ni componente compartido: Feudo y BattlePrep lo tienen serializado y 44 RectTransform con
  referencias de escena no compensan frente al test del paso 4.
- `capture_game_view` guarda siempre bajo `Assets/`: borrar `Assets/Temp` al acabar.
- Si BattleScene tiene cambios locales del usuario al empezar, parar y preguntar antes de mezclar nada.

## Resultado
- Las dos escenas, en las siete resoluciones: HUD y `SkillBar` con la misma escala, sin solapes ni elementos fuera de
  pantalla entre barra, panel de squad, vida, aguante, cartel, temporizador y captura.
- `SupplyPointUi` cubre la pantalla en 1024x768 y 2560x1080 (antes dejaba bandas en 4:3).
- `BattleTestScene` añade reserialización de versión (TMP y renderers), la misma que ya tiene Feudo. `BattleScene` pierde
  `_abilitySlots`, un campo serializado que ya no existe en el código.
