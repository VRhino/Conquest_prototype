# Plan: Fase 1 de `Docs/Design/Aprendizajes_Referencia.md` — pila de modificadores (T1)

Estado: en curso (2026-10-21), rama `feat/reference-phase0` (sigue sobre la Fase 0).
Petición del usuario: continuar con la Fase 1. Plan anterior archivado en `tasks/reference-phase0-plan.md`.
Referencia (solo patrones): `game-re/notes/mechanics.md` §2 (crítico), `damage-pipeline.md` (daño de crítico y
anticrítico), T1 de `Aprendizajes_Referencia.md` (regla de combinación).

## Decisiones del usuario (2026-10-21)
- **Crítico como la referencia (L1-04):** tasa `V / (P + V)` con el valor y el parámetro de crítico del atacante (el
  defensor no cuenta en la tasa); daño extra del crítico `max(Vd / Pd − AV / AP + base, 0,2)` con el anticrítico del
  defensor. Los valores base de cada arma se derivan de su probabilidad y multiplicador actuales, así que el balance no
  cambia hasta que algo (equipo, perks) sume a esos stats.
- **Equipo de todos los héroes por la pila (L7-3):** hoy solo el local recibe su equipo; los héroes IA también.
- **L7-6 (ítems con `StatId` en el guardado) queda para después**, documentado en `Docs/Por_Abordar/`.

## Diseño
- `StatId` (enum de 1 byte, clave estable para guardado y red): vida y aguante máximos, defensa / daño / penetración por
  tipo, velocidad, los siete stats de crítico, y los atributos del héroe (fuerza, destreza, armadura, vitalidad).
- `StatModifier` (buffer): `{stat, op (Ratio / Flat / Override), valor, fuente (Level / Equipment / Perk / Effect),
  idFuente, expiraEn}`. `StatBase` (buffer indexado por `StatId`): valores base inmutables tras el spawn.
- Regla única (`StatMath.Resolve`, pura): `(1 + Σ ratios) · base + Σ planos`; si hay override, gana el último añadido.
- `ModifiersDirty` (enableable) pide recomponer; `StatResolveSystem` es el **único escritor** de los stats derivados:
  `UnitStatsComponent`, `HealthComponent.maxHealth` (conserva la fracción de vida), `DefenseComponent`,
  `PenetrationComponent`, el `DamageProfileComponent` de la escuadra, los stats de crítico y, en héroes, vida y aguante
  máximos a partir de los atributos (fórmulas de clase de `HeroCalculatedAttributes`).
- Las unidades comparten el perfil de daño de su escuadra: el daño se resuelve en la entidad de la escuadra.

## Pasos (un commit cada uno; EditMode verde y regresiones de PlayMode antes de cada commit)
- [x] **1. Núcleo T1.** Tipos, `StatMath`, `StatResolveSystem` con sus objetivos y tests puros y de sistema. Sin
  consumidores nuevos todavía.
- [x] **2. Nivel de escuadra por la pila.** `UnitStatsUtility` deja de escribir stats: siembra la base desde
  `SquadDataComponent` y añade modificadores `Level` (curvas − 1). Mismos números que hoy (tests de escalado).
- [x] **3. Héroes y equipo (L7-3).** Base desde los atributos del héroe; el equipo entra como modificadores `Equipment`
  para todos los héroes (IA incluidos). `HeroInitializationSystem` deja de escribir vida, aguante y defensa.
  Hecho: los atributos son bases de la pila y el equipo modificadores planos sobre ellos; `HeroStatFormulaComponent`
  (coeficientes de clase) deja que el resolvedor derive vida, aguante, daño y defensa. La clase sale del guardado (local)
  o de `HeroClassComponent`, que pone `BattleSceneController` (remotos e IA): `HeroClassDefinition.prefab` apunta a un
  asset que no existe y hornea una clase vacía, así que en batalla ningún héroe se inicializaba. Verificado en
  `BattleTestScene`: el local da 320/117/26,5/45,4, igual que el cálculo antiguo; el IA lleva su equipo.
  Hallazgos sin tocar: los héroes nunca han tenido penetración en batalla (la fórmula la calcula, nadie la aplicaba),
  se deja igual para no cambiar el balance; y la referencia rota de `HeroClassDefinition.prefab` sigue ahí.
- [x] **4. Velocidad con capas (L5-3).** `MoveSpeed` por la pila; el aturdimiento por rotura de escudo es un override a 0
  con caducidad, y desaparece el `speed = 0` duplicado.
  Hecho: `DamageCalculationSystem` añade el override (`StatEffects.Immobilize`) al romper el escudo; las unidades leen
  `UnitStatsComponent.speed` y los héroes `MoveSpeedComponent` (velocidad resuelta; `HeroStatsComponent.baseSpeed`
  sigue siendo la referencia de la marcha). Se quitan las comprobaciones de `brokenTimer` en `UnitFollowFormation` (la
  unidad aturdida frena con su deceleración, como en una llegada) y `HeroMovement`; los héroes IA, que ignoraban el
  aturdimiento, ahora también se paran. Un primer intento paraba en seco con `speed <= 0` y volvía S1 no determinista
  (saltaba la marcha en los primeros frames, antes de resolver la velocidad). La animación de unidad conserva
  su velocidad de referencia mientras la velocidad es 0.
- [x] **5. Escudo absorbente (L1-06).** `AbsorbShieldComponent {valor, expiraEn}` entre el cálculo y la vida: absorbe
  primero y el exceso pasa a la vida; un golpe de 0 no lo consume. Sin fuente todavía (la darán las habilidades).
  Hecho: regla pura `AbsorbShield.Absorb` y su aplicación en `DamageCalculationSystem` tras el bloqueo físico; un golpe
  absorbido entero se muestra como bloqueado y no llega a la vida; el escudo agotado o caducado se quita.
- [x] **6. Crítico como la referencia (L1-04).** `CritFormula` (tasa y daño extra) con los stats de la pila; base del arma
  equivalente a hoy.
  Hecho: `CritFormula` (tasa `V/(P+V)`, extra `max(Vd/Pd − AV/AP + ratio, suelo)`, multiplicador `1 + extra`) y
  `CritStatsComponent`, que escribe el resolvedor. `StatStack.SeedCritical` siembra bases equivalentes a la probabilidad
  y el multiplicador del arma (valor `c/(1−c)` con parámetro 1; daño `m − 1`), en unidades y héroes; melé y distancia
  usan la fórmula contra el anticrítico del objetivo. El suelo de 0,2 va en `CombatTuning`. Límite: un arma con
  multiplicador menor que 1,2 haría 1,2 en el crítico (hoy todos los datos usan 1,5).
- [ ] **7. Tablas de progresión (L7-5).** La XP de escuadra (`100 × 1,1^n`) y el desbloqueo cada 10 niveles pasan a una
  tabla en datos (ScriptableObject → blob).
- [ ] **8. Documentación.** `DamageMechanics.md`, `SystemResponsibilities.md`, L7-6 en `Docs/Por_Abordar/`, lecciones.

## Verificación
EditMode completo; PlayMode `SquadNavigationRegressionTests` y MovementBaseline (el combate sigue: S6/S7); prueba en
juego de que el héroe ataca, gasta aguante y que una escuadra de nivel alto tiene más vida que una de nivel 1.
