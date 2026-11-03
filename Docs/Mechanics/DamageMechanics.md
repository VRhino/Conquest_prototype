# Mecánicas de Daño

## 1. Resumen

El sistema de daño es **event-driven**: los ataques generan un `PendingDamageEvent` que el sistema central consume y resuelve en el mismo frame. Esto desacopla la detección de colisiones del cálculo de daño.

Características clave:
- **3 tipos de daño**: Blunt, Slashing, Piercing — cada uno con defensa y penetración independiente.
- **Resta plana por tipo con suelo del 5 %**: la defensa neta (defensa − penetración) se resta del daño de cada tipo,
  pero de cada tipo llega siempre al menos el 5 %.
- **Bonificaciones situacionales**: velocidad cinética y ventaja de altura amplifican el daño.
- **Golpe único por swing**: un atacante genera como máximo un evento de daño por animación de ataque.
- **Escudo después del cálculo**: si el golpe tocó primero el collider del escudo, el escudo absorbe el daño ya calculado;
  si se queda a 0 se rompe y aturde.
- **Escudo absorbente** (§6.7): una reserva temporal, separada de la vida y del escudo físico, que se lleva el golpe primero.
- **Stats desde la pila de modificadores** (§4.1): vida, defensa, daño, velocidad y crítico los escribe un único sistema.
- **Rasgos del golpe por arma** (§5.5): golpes que ignoran el escudo, desgaste del bloqueo y del aguante, variación
  aleatoria y bonus de carga, declarados en los datos del arma.
- **Modificadores condicionados** (§5.6): "+X % contra caballería" y similares, evaluados solo en el impacto.
- **Terreno del mapa** (§4.2), **suelo de vida** tras reaparecer (§7.4) y **robo de vida** (§7.5).

### Estado frente al código (2026-10-21, tras la Fase 1 de `Docs/Design/Aprendizajes_Referencia.md`)
| Apartado | Código de referencia | Estado |
|---|---|---|
| Origen de los stats (§4.1) | `Stats/StatResolve.System.cs`, `Stats/StatMath.cs`, `Stats/StatStack.cs` | Sincronizado (T1) |
| Fórmula (§5) | `Combat/DamageFormula.cs` (`DamageFormula.Compute`, pura; constantes en `CombatTuning`) | Sincronizado (D1 y L1-03 de `Docs/Design/Aprendizajes_Referencia.md`) |
| Escudo (§6) | `DamageCalculation.System.cs`, `ShieldHitboxBehaviour`, `BlockRegen.System.cs` | Sincronizado; la orientación no se usa; el aturdimiento va por la pila (L5-3) |
| Escudo absorbente (§6.7) | `Combat/AbsorbShield.cs`, `AbsorbShield.Component.cs` | Sincronizado (L1-06); aún sin fuente |
| Crítico (§5.3) | `Stats/CritFormula.cs`, tirada `CombatRandom.RollCritical` en `MeleeContactResolution.System.cs` y `RangedAttack.System.cs` | Sincronizado (L1-04); tirada reproducible (L1-05) |
| Rasgos del golpe (§5.5) | `Combat/HitTraits.cs`, `ShieldBlock.cs`, `ChargeTrack.cs`, `CombatRandom.RollVariance` | Sincronizado (L1-08, L1-10, L1-11, L1-12; 2026-11-03) |
| Condicionados (§5.6) | `Combat/HitConditions.cs`, `DamageCalculation.System.cs` | Sincronizado (L1-02; 2026-11-03) |
| Terreno (§4.2) | `Map/BattleTerrain.cs`, `UnitStatsUtility.ApplySquadTypeModifiers` | Sincronizado (L1-14; 2026-11-03) |
| Suelo y robo de vida (§7.4, §7.5) | `Combat/HealthFloor.cs`, `Combat/LifeSteal.cs`, `HeroRespawn.System.cs` | Sincronizado (L1-07, L1-13; 2026-11-03) |

---

## 2. Precondiciones

| Condición | Detalle |
|-----------|---------|
| Atacante activo | Tiene `UnitWeaponComponent` (unidades) o `WeaponColliderComponent` (héroe) |
| Objetivo válido | Tiene `HealthComponent` (unidades) o `HeroHealthComponent` (héroe) |
| Teams diferentes | `TeamComponent` del atacante ≠ `TeamComponent` del objetivo; friendly fire desactivado |
| Ventana de golpe activa | `WeaponHitboxActiveTag` habilitado (unidades) o `WeaponColliderComponent.isActive` (héroe) |
| Cooldown en 0 | `UnitCombatComponent.attackCooldown == 0` antes de iniciar el swing |
| Objetivo vivo | Objetivo sin `IsDeadComponent`; targeting lo descarta automáticamente |

---

## 3. Flujo de Daño

```
┌─────────────────────────────────────────────────────────────┐
│  FASE 1 — ATAQUE                                            │
│                                                             │
│  UnitAttackSystem / SkillExecutionSystem (héroe)            │
│     │                                                       │
│     ├─ Verifica cooldown, rango y stamina (héroe)           │
│     ├─ Activa WeaponHitboxActiveTag / WeaponCollider        │
│     └─ Timer de ventana de golpe: [strikeStart, strikeEnd]  │
└─────────────────────────────────────────────────────────────┘
                        │
                        ▼
┌─────────────────────────────────────────────────────────────┐
│  FASE 2 — CONSTRUCCIÓN DEL GOLPE                            │
│                                                             │
│  MeleeContactResolutionSystem / ProjectileImpactSystem /    │
│  SkillEffectSystem (daño en área)                           │
│     ├─ Tira el crítico (CombatRandom, tick + atacante)      │
│     ├─ Tira la variación del arma (L1-12)                   │
│     ├─ Anota forma, habilidad y carga del golpe (L1-02/11)  │
│     ├─ Crea PendingDamageEvent con el perfil en línea       │
│     └─ Marca hitboxFired = true (un solo evento por swing)  │
└─────────────────────────────────────────────────────────────┘
                        │
                        ▼
┌─────────────────────────────────────────────────────────────┐
│  FASE 3 — CÁLCULO Y APLICACIÓN                              │
│                                                             │
│  DamageCalculationSystem (procesa PendingDamageEvent)       │
│     ├─ Valida entidades y friendly fire                     │
│     ├─ Suma los condicionados de los dos lados (L1-02)      │
│     ├─ DamageFormula.Compute: mitigación por tipo, cinética,│
│     │  altura, carga, variación y condicionados             │
│     ├─ Bloqueo físico (ShieldBlock, L1-08/L1-10) o          │
│     ├─ escudo absorbente (L1-06), y el resto a la vida      │
│     ├─ Suelo de vida del héroe (L1-07)                      │
│     ├─ Robo de vida del atacante (L1-13)                    │
│     ├─ Si salud ≤ 0: añade IsDeadComponent                  │
│     └─ Elimina PendingDamageEvent                           │
└─────────────────────────────────────────────────────────────┘
```

---

## 4. Tipos de Daño, Defensa y Penetración

Cada tipo de daño mapea a campos específicos en los componentes de defensa y penetración:

| Tipo de Daño | Daño | Defensa (objetivo) | Penetración (arma + atacante) |
|---|---|---|---|
| Blunt | `DamageProfileComponent.bluntDamage` | `DefenseComponent.bluntDefense` | `DamageProfileComponent.bluntPenetration` + `PenetrationComponent.bluntPenetration` |
| Slashing | `DamageProfileComponent.slashingDamage` | `DefenseComponent.slashDefense` | `DamageProfileComponent.slashingPenetration` + `PenetrationComponent.slashPenetration` |
| Piercing | `DamageProfileComponent.piercingDamage` | `DefenseComponent.pierceDefense` | `DamageProfileComponent.piercingPenetration` + `PenetrationComponent.piercePenetration` |

**Notas:**
- Un tipo con daño 0 no contribuye (ni siquiera con el suelo del 5 %).
- `PenetrationComponent` en el atacante añade penetración extra por tipo; su presencia es opcional.
- La defensa siempre se obtiene del **objetivo**; la penetración, del perfil del arma y del **atacante**.

### 4.1 De dónde salen los stats (pila de modificadores, T1)

Ningún sistema de combate escribe vida máxima, defensa, daño, velocidad ni crítico. Cada entidad lleva una base por stat
(`StatBaseElement`, indexada por `StatId`) y una lista de modificadores (`StatModifier`: stat, operación, valor, fuente,
id de fuente, caducidad). `StatResolveSystem` es el **único escritor** de los derivados y aplica una sola regla
(`StatMath.Resolve`):

```
valor = override si hay alguno (gana el último)
      = (1 + Σ ratios) × base + Σ planos      en otro caso; los modificadores caducados no cuentan
```

| Fuente | Quién la escribe | Qué añade |
|---|---|---|
| Base de unidad | `UnitStatsUtility.ApplyStatsToUnit` | vida, velocidad, defensas, daños y crítico desde `SquadDataComponent` |
| `Level` | `UnitStatsUtility` | curva de nivel como ratio (`multiplicador − 1`) |
| Base de héroe | `HeroInitializationSystem` | atributos (fuerza, destreza, armadura, vitalidad), velocidad y crítico |
| `Equipment` | `HeroInitializationSystem` | equipo como planos sobre los atributos, para todos los héroes (IA incluidos) |
| `Effect` | `DamageCalculationSystem`, `HeroRespawnSystem`, `SkillEffectSystem` | aturdimiento por rotura de escudo: override de velocidad a 0 que caduca (§6.3); suelo de vida al reaparecer (§7.4); efectos de habilidades |
| `UnitType` | `UnitStatsUtility.ApplySquadTypeModifiers` | modificadores condicionados que declara el tipo de escuadra (§5.6), en el buffer `ConditionalModifier` |
| `Terrain` | `UnitStatsUtility.ApplySquadTypeModifiers` | modificadores del tipo de escuadra para el terreno del mapa (§4.2) |

Stats de combate añadidos el 2026-11-03: `MinHealthRatio` (suelo de vida del héroe, en `HeroHealthComponent.minHealthRatio`)
y `LifeSteal` (robo de vida, en `CombatStatsComponent.lifeSteal`). Los dos valen 0 de base.

En los héroes, `HeroStatFormulaComponent` (coeficientes de la clase) convierte los atributos resueltos en vida, aguante,
daño y defensa. Los héroes no tienen penetración en batalla (nunca la tuvieron). El resolvedor conserva la fracción de
vida y de aguante al cambiar el máximo. Corre después de `UnitStatScalingSystem`; un modificador añadido durante el
frame lo ve el daño en el siguiente.

### 4.2 Terreno del mapa (L1-14)

Como en la referencia, el terreno es uno por mapa y los modificadores los declara el tipo de unidad, filtrados por ese
terreno:

- `MapDataSO.terrain` (`BattleTerrain`: `Any`, `Plains`, `Forest`, `Mountain`, `Snow`; enum de un byte, solo se añade al
  final). `BattleBootstrapSystem` lo publica en el singleton `BattleTerrainComponent` al empezar la batalla. Un mapa sin
  terreno (o una escena de pruebas sin bootstrap) vale `Any`.
- `SquadData.terrainModifiers`: lista de `{terreno, stat, operación, valor}`. `Any` en un modificador significa "en
  cualquier terreno".
- `UnitStatScalingSystem` (al aparecer y al subir de nivel) llama a `UnitStatsUtility.ApplySquadTypeModifiers`, que
  sustituye los de fuente `Terrain` por los del terreno actual. Vida, defensa y velocidad van a cada soldado; el daño, al
  soldado (solo se muestra) y al perfil compartido de la escuadra; la penetración, solo al perfil, porque la del soldado
  se suma a la del perfil en la fórmula.
- Ningún tipo de escuadra declara aún modificadores de terreno (ver `Docs/Por_Abordar/Combate.md` C2).

---

## 5. Fórmula de Cálculo de Daño

### 5.1 Contribución de cada tipo

```
bruto        = daño_tipo × multiplier
defensaNeta  = max(defensa_tipo − (penetración_perfil + penetración_atacante), 0)
contribución = max(bruto − defensaNeta, bruto × 0.05)      // suelo del 5 % por tipo
```

### 5.2 Daño efectivo final

```
dañoEfectivo = Σ contribuciones × bonoCinético × bonoAltura × bonoCarga × (1 + variación) × (1 + Σ condicionados)
               (nunca negativo; cada factor por separado tampoco)
```

`DamageResult` devuelve cada factor por separado (`kineticBonus`, `heightBonus`, `chargeBonus`, `varianceFactor`,
`conditionalFactor`), para los tests y para mostrarlo. Son cinco etapas multiplicativas, no las ~17 de la referencia
(`Aprendizajes_Referencia.md` §6).

### 5.3 Bonificaciones situacionales

| Bonificación | Condición de activación | Fórmula |
|---|---|---|
| **Crítico** | tirada `< V / (P + V)` del atacante, sembrada por (tick, atacante) con `CombatRandom` (L1-05); el defensor no cuenta en la tasa | `multiplier = 1 + max(Vd / Pd − AV / AP + ratio, 0.2)`; `AV/AP` es el anticrítico del objetivo |
| **Cinética** | `attackerSpeed > 0` | `× (1 + (velocidad / 5.0) × kineticMultiplier)`; `kineticMultiplier` del arma (0.3 si no hay) |
| **Altura** | Atacante > 0.5 m sobre el objetivo | `× (1 + deltaAltura × 0.1)` (~10 % por metro) |

- La ventaja de altura se calcula con `attackerPosition.y − target.position.y`.
- Crítico como la referencia (L1-04, `CritFormula`), con los siete stats de crítico de la pila (`CritStatsComponent`). Las
  bases de cada arma se derivan de su `criticalChance` y `criticalMultiplier` (valor `c / (1 − c)` con parámetro 1; daño
  `m − 1`), así que el resultado es el de antes hasta que equipo o perks sumen a esos stats. Un arma con multiplicador
  menor que 1.2 haría 1.2 por el suelo. Sin `CritStatsComponent`, el atacante usa la probabilidad y el multiplicador del arma.

### 5.4 Parámetros clave

| Parámetro | Valor | Descripción |
|---|---|---|
| Suelo por tipo | 5 % | Fracción del daño bruto de cada tipo que llega siempre |
| Velocidad de referencia cinética | 5.0 m/s | Normaliza la bonificación |
| Multiplicador cinético por defecto | 0.3 | Si el atacante no tiene `UnitWeaponComponent` |
| Umbral de altura | 0.5 m | Diferencia mínima para activar el bono |
| Bono de altura | 10 % por metro | Por encima del umbral |
| Suelo del daño extra de crítico | 0.2 | `CombatTuning.criticalExtraFloor`, como la referencia |
| Ventana de la carga | 1 s | `CombatTuning.chargeWindowSeconds`: la carrera medida tiene entre 0,5 y 1 s (§5.5) |

### 5.5 Rasgos del golpe por arma (L1-08, L1-10, L1-11, L1-12)

`HitTraits` va dentro de `DamageProfileComponent`, así que viaja con el perfil en línea del golpe (melé, proyectil y
habilidad). Lo declara el arma: `SquadMeleeData.hitTraits` o `SquadRangedData.hitTraits` (soldados) y
`HeroClassDefinition.hitTraits` (héroe). `StatResolveSystem` reescribe solo el daño y la penetración del perfil y deja los
rasgos. Todos valen 0 por defecto, que es neutro: **hoy ningún asset los usa** (`Docs/Por_Abordar/Combate.md` C1).

| Rasgo | Efecto |
|---|---|
| `ignoresShield` | El golpe no se bloquea: va a la vida aunque toque primero el escudo, y no gasta bloqueo (§6.5) |
| `blockDamageRatio` | El bloqueo pierde `efectivo × (1 + r)`; −1 = no lo gasta (armas contundentes que abren escudos) |
| `staminaDamageRatio` | El héroe que bloquea pierde `efectivo × r` de aguante; el agotamiento lo lleva `HeroStaminaSystem` |
| `damageVariance` (f) | Factor `1 ± f`, tirado por quien construye el golpe con `CombatRandom.RollVariance` (semilla: tick, atacante y objetivo) y guardado en `PendingDamageEvent.varianceOffset` |
| `chargeThreshold`, `chargeFullDistance`, `chargeMaxBonus` | Bonus de carga, abajo |

**Carga (L1-11).** Misma estructura que la referencia: umbral del arma, peso `min(d / X, 1)` y bonus máximo. Por decisión
del usuario (2026-11-03), `d` son los metros que el atacante recorrió **hacia el objetivo** justo antes del golpe (la
referencia mide cuánto ha rebasado al objetivo, un "golpe de pasada"):

```
d     = max(0, (posición − posición hace 0,5-1 s) · dirección al objetivo)      en el plano del suelo
bono  = 1                                   si chargeMaxBonus ≤ 0 o d ≤ chargeThreshold
      = 1 + chargeMaxBonus · min(d / chargeFullDistance, 1)                     en otro caso
```

`ChargeTrackSystem` guarda dos muestras de posición por atacante (`ChargeTrackComponent`, en soldados y héroes) separadas
media ventana; `MeleeContactResolutionSystem` mide `d` y lo deja en `PendingDamageEvent.chargeDistance`. Solo la melé
carga. Convive con el bono cinético (velocidad en el instante del golpe).

### 5.6 Modificadores condicionados (L1-02)

Como en la referencia, un modificador con precondición que solo se evalúa en el impacto, con el contexto del golpe. Cada
entidad puede llevar un buffer `ConditionalModifier {condición, damageRatio, fuente, id, caducidad}`;
`DamageCalculationSystem` suma los ratios del atacante con lado `Attacker` y los del objetivo con lado `Defender` que se
cumplen, y multiplica el daño una vez por `(1 + Σ)` (regla T1).

Condiciones (`HitCondition`, lógica pura en `HitConditions.Matches`), todas con Y; las listas son "cualquiera de" y cada
valor por defecto significa "no importa". "El otro" es el objetivo si el dueño ataca y el atacante si defiende:

| Condición | Se cumple si |
|---|---|
| `chance` | la tirada sembrada (tick, dueño, modificador y el otro) es menor; 0 = siempre |
| `otherKind` | el otro es héroe o soldado |
| `otherSquadTypes` | el otro es soldado de alguna de esas clases de escuadra |
| `otherUnitTypes` | el otro es soldado de alguno de esos tipos de unidad (`Infantry`, `Cavalry`, `Distance`, de `SquadData.unitType`) |
| `forms` | el golpe es de alguna de esas formas (`PendingDamageEvent.hitForm`: melé, distancia o habilidad) |
| `skillIds` | el golpe sale de alguna de esas habilidades (`PendingDamageEvent.skillId`; en la melé del héroe, la que está corriendo) |
| `critical` | triestado sobre si el golpe es crítico |
| `selfHasEffect`, `otherHasEffect` | triestados sobre un efecto temporal activo en `effectStat` (como en los perks) |
| `defenderHealthMin/Max` | la vida del que recibe, antes del golpe, como fracción (máximo 0 = sin límite) |
| `otherArmorMin/Max` | la suma de las tres defensas del otro (0 = sin límite) |

Fuentes:
- `SquadData.conditionalModifiers` (permanentes, fuente `UnitType`): `UnitStatScalingSystem` los copia a cada soldado al
  aparecer y al subir de nivel.
- El evento de habilidad `ApplyConditional` (temporal, fuente `Effect`, id = la habilidad): ratio `value` durante
  `duration` a los objetivos del evento; relanzar lo refresca.
- Ningún dato los usa aún; los perks pasivos no pueden darlos todavía (`Docs/Por_Abordar/Combate.md` C3).

---

## 6. Sistema de Escudo

El escudo se evalúa **después** de la fórmula: si el golpe tocó primero el collider del escudo (`PendingDamageEvent.hitType == Shield`), el escudo absorbe el daño efectivo ya calculado y no se reduce la salud.

**Solo las unidades/héroes cuyo prefab visual contiene un `ShieldHitboxBehaviour` tienen escudo.** El componente ECS `UnitShieldComponent` se agrega automáticamente en runtime al detectar dicho MonoBehaviour — no todas las unidades tienen escudo.

### 6.1 Patrón de descubrimiento (prefab-based)

El mismo patrón que `WeaponHitboxBehaviour`:

```
[Editor]
  Prefab Visual
    └── ShieldHitbox (child GO) ← solo en prefabs con escudo
          ├── ShieldHitboxBehaviour (orientation, maxBlockOverride, regenRateOverride)
          └── BoxCollider

[Runtime — visual instantiation]
  SquadVisualManagementSystem / HeroVisualInstantiationSystem
    └── GetComponentInChildren<ShieldHitboxBehaviour>(true)
          └── Si existe → wire ownerUnit + ecb.AddComponent<UnitShieldComponent>
```

### 6.2 Resolución de valores (prioridad)

| Prioridad | Fuente | Condición |
|---|---|---|
| 1 | `ShieldHitboxBehaviour.maxBlockOverride` | Si > 0 en el prefab |
| 2 | `SquadDataComponent.block` | Si el override es 0 y el squad tiene valor |
| 3 | Default (100f unidades / 100f héroes) | Fallback |

Misma lógica para `regenRate` con `blockRegenRate` de `SquadData`.

### 6.3 Componente de escudo

`UnitShieldComponent` almacena:
- `currentBlock`: puntos de bloqueo disponibles.
- `maxBlock`: capacidad máxima.
- `regenRate`: puntos regenerados por segundo.
- `brokenTimer` / `breakStunDuration`: escudo roto (sin bloqueo ni regeneración mientras dura). El aturdimiento en sí es un
  override de velocidad a 0 en la pila que caduca a la vez (`StatEffects.Immobilize`, L5-3): unidades, héroe local y héroes
  IA lo ven en su velocidad resuelta; la unidad frena con su deceleración.
- `orientation`: **no tiene lectores**. El bloqueo lo decide el collider del escudo, que cuelga de la mano.

### 6.4 Orientaciones de escudo

No hay lógica por orientación (ni `forwardBlockDotThreshold`): la dirección la decide la física, porque el golpe solo
cuenta como bloqueado si el arma toca primero el collider del escudo. Ver `Docs/Por_Abordar/Formaciones_y_Posturas.md` F16.

### 6.5 Flujo de bloqueo

```
PendingDamageEvent llega al objetivo
  │
  ├─ Se calcula dañoEfectivo (§5)
  │
  ├─ ¿hitType == Shield, el arma no ignora el escudo (ignoresShield), tiene UnitShieldComponent,
  │   currentBlock > 0 y no está roto?  (ShieldBlock.Blocks)
  │     └─ No → dañoEfectivo se resta de la salud
  │
  └─ Sí → currentBlock −= dañoEfectivo × (1 + blockDamageRatio)
           ├─ si es un héroe con aguante: currentStamina −= dañoEfectivo × staminaDamageRatio
           ├─ currentBlock ≤ 0 → escudo roto: currentBlock = 0, brokenTimer = breakStunDuration, FCT "ROTO",
           │                    velocidad 0 en la pila hasta ahora + breakStunDuration
           └─ si no → FCT "BLOQ" con lo que perdió el escudo
           (la salud no cambia)
```

### 6.6 Regeneración de escudo

`BlockRegenSystem` incrementa `currentBlock` cada frame:
```
currentBlock = min(maxBlock, currentBlock + regenRate × deltaTime)
```
El regen ocurre siempre (no hay condición explícita de "fuera de combate").

### 6.7 Escudo absorbente (L1-06)

`AbsorbShieldComponent {value, expiresAt}` es una reserva de absorción temporal, separada de la vida y del escudo físico.
Tras el bloqueo físico, `DamageCalculationSystem` le pasa el daño efectivo (`AbsorbShield.Absorb`), como en la referencia:

- absorbe primero y el exceso pasa a la vida;
- un golpe de daño 0 no lo consume;
- caducado (`expiresAt > 0` y pasado) no absorbe nada;
- un golpe absorbido entero muestra el FCT de bloqueo y no toca la vida; agotado o caducado, el componente se quita.

Aún no lo añade ninguna fuente; lo darán las habilidades.

---

## 7. Salud, Muerte y Respawn

### 7.1 Componentes de salud

| Componente | Entidad | Campos |
|---|---|---|
| `HealthComponent` | Unidades | `maxHealth`, `currentHealth` |
| `HeroHealthComponent` | Héroe | `maxHealth`, `currentHealth` |

### 7.2 Muerte

Cuando `currentHealth ≤ 0`, `DamageCalculationSystem` añade `IsDeadComponent` (tag vacío). Esto:
- Detiene el targeting: `UnitTargetingSystem` ignora entidades con `IsDeadComponent`.
- Detiene los ataques: `UnitAttackSystem` verifica que el objetivo esté vivo antes de atacar.
- Unidades: la muerte es **permanente** (no hay respawn de unidades).

### 7.3 Respawn del héroe

```
Salud ≤ 0
   │
   ├─ HeroRespawnSystem detecta la condición
   ├─ isAlive = false
   ├─ Inicia deathTimer (= respawnCooldown)
   │
   └─ deathTimer llega a 0
         ├─ isAlive = true
         ├─ currentHealth = maxHealth
         └─ Elimina IsDeadComponent
```

`HeroLifeComponent` gestiona: `isAlive`, `deathTimer`, `respawnCooldown`.

### 7.4 Suelo de vida y protección al reaparecer (L1-07)

Como en la referencia, un suelo fijado desde fuera: `nuevaVida = max(vida − daño, min(vida, suelo · vidaMáx))`
(`HealthFloor.Apply`). El `min` evita que un suelo por encima de la vida actual cure.

- El suelo es el stat `MinHealthRatio` de la pila; `StatResolveSystem` lo escribe en `HeroHealthComponent.minHealthRatio`
  (entre 0 y 1). Solo lo tienen los héroes.
- Al reaparecer, `HeroRespawnSystem` añade un plano temporal (fuente `Effect`, id `StatEffects.RespawnProtection`) con
  `HeroGameplayConfig.respawnProtectionHealthRatio` durante `respawnProtectionSeconds` (0,3 y 5 s por defecto). Con un
  suelo de 1 el héroe es invulnerable.
- Una habilidad o un perk pueden dar el mismo stat ("no puede morir durante X s").

### 7.5 Robo de vida (L1-13)

Un stat más de la pila (`LifeSteal`), como en la referencia. Después de restar la vida, el atacante vivo se cura
`vidaQuitada × lifeSteal`, hasta su máximo (`LifeSteal.Heal`). La vida quitada es la real: tras escudo, escudo absorbente y
suelo, y nunca más de la que le quedaba al objetivo. Lo leen soldados y héroes en `CombatStatsComponent`, que añaden
`UnitStatsUtility` y `HeroInitializationSystem`. Sin FCT propio.

---

## 8. Modo Brace (Piqueros en Hold Position)

Cuando un squad con formación de piqueros entra en estado `HoldingPosition`, `BraceWeaponActivationSystem` activa el modo **Brace**. En este modo:

- `BraceWeaponSystem` sobreescribe los parámetros del arma de cada unidad según su fila (`BraceRowProfile`).
- Cada fila recibe un perfil diferente: rango, dimensiones del hitbox y timing de animación ajustados para la formación defensiva.
- Al salir de `HoldingPosition`, se revierte al modo Normal.

Esto permite que las lanzas de filas traseras alcancen a enemigos en el frente sin cambiar su posición.

---

## 9. Detección de Enemigos y Targeting

El ciclo de detección precede al ataque:

```
EnemyDetectionSystem (cada frame)
   ├─ Detecta squads y entidades enemigas dentro del rango del squad (distancia al centroide)
   └─ Limpia y repuebla DetectedEnemy y SquadTargetEntity una vez por squad

UnitTargetingSystem (cada frame)
   ├─ Consume directamente el SquadTargetEntity compartido
   ├─ Asigna UnitCombatComponent.target al enemigo más cercano disponible
   └─ Redistribuye si >3 unidades apuntan al mismo objetivo
```

Solo las unidades en estado Attack, FollowHero o HoldPosition reciben asignación de target.

---

## 10. Edge Cases

| Caso | Resolución |
|------|-----------|
| Penetración = 0 en el arma | La defensa se resta entera (defensa neta = defensa); el suelo del 5 % sigue valiendo |
| Defensa muy alta sin penetración | De cada tipo con daño llega siempre al menos el 5 % del bruto |
| Friendly fire | `DamageCalculationSystem` compara `TeamComponent`; descarta el evento si mismo equipo |
| Hitbox activo durante múltiples frames | Flag `hitboxFired` garantiza un único `PendingDamageEvent` por swing |
| Escudo con `currentBlock ≤ 0` o roto (`brokenTimer > 0`) | No bloquea; el daño efectivo va a la salud |
| Golpe que no toca primero el escudo | `hitType = Body`: va a la salud aunque la unidad tenga escudo (la orientación no se evalúa) |
| Objetivo destruido entre la creación y el procesado del evento | `DamageCalculationSystem` verifica existencia de la entidad al inicio; descarta si no existe |
| Múltiples atacantes en el mismo frame | Se procesan todos los `PendingDamageEvent` en orden; la salud se queda en 0 (no baja de 0) y `IsDeadComponent` solo se añade una vez |
| Héroe sin stamina suficiente | `SkillGate` rechaza el ataque (`NotEnoughStamina`); no se crea evento de daño |
| Modo Brace activado | `BraceWeaponSystem` aplica perfiles por fila antes de que `UnitAttackSystem` evalúe rango/timing |
| Golpe con `ignoresShield` contra el escudo | Va a la vida (y al escudo absorbente); el bloqueo no pierde nada |
| Héroe protegido con la vida ya por debajo del suelo | El golpe no le quita nada, pero tampoco le cura |
| Golpe que mata con robo de vida | El atacante se cura solo por la vida que le quedaba al objetivo |
| Condicionado con `chance` en un golpe en área | Cada objetivo tira aparte (el otro va en la semilla) |

---

## 11. Infraestructura existente

### Enums y ScriptableObjects

| Archivo | Rol |
|---------|-----|
| `Assets/Scripts/Combat/DamageType.cs` | Enum: Blunt, Slashing, Piercing |
| `Assets/Scripts/Combat/DamageCategory.cs` | Enum: Normal, Critical, Ability, Blocked, Death, ShieldBreak — indexa el tipo de FCT a mostrar |
| `Assets/Scripts/Combat/DamageProfile.cs` | ScriptableObject: baseDamage, damageType, penetration |
| `Assets/Scripts/Combat/Team.cs` | Enum: None, TeamA, TeamB |

### Componentes ECS

| Archivo | Rol |
|---------|-----|
| `Assets/Scripts/Combat/DamageProfile.Component.cs` | Daño y penetración por tipo (stats de la pila) y `traits` del arma (§5.5) |
| `Assets/Scripts/Combat/PendingDamageEvent.cs` | Evento de daño pendiente: target, damageSource, damageProfile (o en línea), category, multiplier, attackerSpeed, attackerPosition, hitType, y el contexto ya resuelto del golpe: varianceOffset, hitForm, skillId, chargeDistance |
| `Assets/Scripts/Combat/Health.Component.cs` | Salud de unidades: maxHealth, currentHealth |
| `Assets/Scripts/Hero/Components/HeroHealth.Component.cs` | Salud del héroe: maxHealth, currentHealth |
| `Assets/Scripts/Combat/Defense.Component.cs` | Defensa por tipo: bluntDefense, slashDefense, pierceDefense |
| `Assets/Scripts/Combat/Penetration.Component.cs` | Penetración extra por tipo: bluntPenetration, slashPenetration, piercePenetration |
| `Assets/Scripts/Combat/IsDead.Component.cs` | Tag vacío añadido cuando salud ≤ 0 |
| `Assets/Scripts/Combat/UnitWeapon.Component.cs` | Stats del arma: damageProfile, attackRange, attackInterval, criticalChance, criticalMultiplier, hitbox shape, strike window, kineticMultiplier |
| `Assets/Scripts/Combat/UnitCombat.Component.cs` | Estado de ataque de la unidad: target, attackCooldown, isAttacking, attackAnimationTimer, hitboxFired |
| `Assets/Scripts/Combat/SquadCombat.Component.cs` | Parámetros de ataque a nivel squad: attackRange, attackInterval, attackTimer |
| `Assets/Scripts/Combat/UnitShield.Component.cs` | Bloqueo de escudo: currentBlock, maxBlock, regenRate, brokenTimer, breakStunDuration (orientation sin lectores) |
| `Assets/Scripts/Combat/WeaponHitbox.Component.cs` | WeaponHitboxOwner, WeaponHitboxRef, WeaponHitboxActiveTag (IEnableableComponent) |
| `Assets/Scripts/Combat/WeaponCollider.Component.cs` | Hitbox del héroe: owner, isActive |
| `Assets/Scripts/Combat/Team.Component.cs` | Equipo de la entidad: TeamComponent.value |
| `Assets/Scripts/Hero/Components/HeroLife.Component.cs` | Ciclo de vida del héroe: isAlive, deathTimer, respawnCooldown |
| `Assets/Scripts/Combat/BraceWeaponProfiles.Component.cs` | SquadCombatModeComponent (Normal/Brace) y BraceRowProfile (buffer por fila) |
| `Assets/Scripts/Combat/AbsorbShield.Component.cs` | Escudo absorbente: value, expiresAt (§6.7) |
| `Assets/Scripts/Stats/StatTypes.cs` | `StatId`, `StatModifier`, `StatBaseElement`, `ModifiersDirty`, `StatModifierExpiry` (§4.1) |
| `Assets/Scripts/Stats/CritStats.Component.cs` | Stats de crítico resueltos (§5.3) |
| `Assets/Scripts/Stats/MoveSpeed.Component.cs` | Velocidad resuelta del héroe; 0 mientras está aturdido |
| `Assets/Scripts/Stats/HeroStatFormula.cs` | Coeficientes de clase del héroe y su fórmula (§4.1) |
| `Assets/Scripts/Stats/CombatStats.Component.cs` | Robo de vida resuelto (§7.5) |
| `Assets/Scripts/Combat/HitTraits.cs` | Rasgos del golpe por arma, dentro del perfil de daño (§5.5) |
| `Assets/Scripts/Combat/ShieldBlock.cs` | Regla pura del bloqueo: si bloquea, daño al bloqueo y al aguante (§6.5) |
| `Assets/Scripts/Combat/ChargeTrack.cs` | Muestras de posición del atacante y regla del bonus de carga (§5.5) |
| `Assets/Scripts/Combat/HitConditions.cs` | Condiciones, `ConditionalModifier` y su regla pura (§5.6) |
| `Assets/Scripts/Combat/HealthFloor.cs`, `LifeSteal.cs` | Suelo de vida y robo de vida (§7.4, §7.5) |
| `Assets/Scripts/Map/BattleTerrain.cs` | Terreno del mapa, singleton y modificadores por terreno (§4.2) |

### Sistemas ECS

| Archivo | Rol |
|---------|-----|
| `Assets/Scripts/Combat/DamageCalculation.System.cs` | **Sistema central**: consume PendingDamageEvent, aplica fórmula, reduce salud, añade IsDeadComponent |
| `Assets/Scripts/Combat/HitboxCollision.System.cs` | Detección AABB → crea PendingDamageEvent durante la ventana de golpe |
| `Assets/Scripts/Combat/UnitAttack.System.cs` | FSM de 3 fases por unidad: decisión → ventana de golpe → fin de animación |
| `Assets/Scripts/Skills/SkillExecution.System.cs` | Habilidades del héroe, ataque básico incluido: puerta, fases, ventana del golpe (ver `SkillsAndPerks.md`) |
| `Assets/Scripts/Skills/SkillEffect.System.cs` | Daño en área y efectos de las habilidades |
| `Assets/Scripts/Combat/WeaponHitboxPosition.System.cs` | Actualiza posición del hitbox para seguir la punta del arma cada frame |
| `Assets/Scripts/Combat/BlockRegen.System.cs` | Regenera currentBlock de todos los escudos cada frame |
| `Assets/Scripts/Stats/StatResolve.System.cs` | Único escritor de los stats derivados desde la pila (§4.1) |
| `Assets/Scripts/Hero/Systems/HeroRespawn.System.cs` | Gestiona muerte y respawn del héroe vía deathTimer |
| `Assets/Scripts/Combat/UnitTargeting.System.cs` | Asigna target a cada unidad; redistribuye si hay concentración excesiva |
| `Assets/Scripts/Combat/EnemyDetection.System.cs` | Detecta enemigos en rango y publica candidatos compartidos en SquadTargetEntity |
| `Assets/Scripts/Combat/BraceWeaponActivation.System.cs` | Activa/desactiva modo Brace al entrar/salir de HoldingPosition |
| `Assets/Scripts/Combat/BraceWeapon.System.cs` | Aplica perfiles de arma por fila durante modo Brace |
| `Assets/Scripts/Combat/ChargeTrack.System.cs` | Muestrea la posición de soldados y héroes para medir la carga (§5.5) |

### Authoring / Baker

| Archivo | Rol |
|---------|-----|
| `Assets/Scripts/Combat/DamageProfile.Authoring.cs` | Baker que convierte DamageProfile ScriptableObject → DamageProfileComponent en tiempo de bake |

---

## 12. Retroalimentación Visual — Floating Combat Text (FCT)

Cada golpe procesado por `DamageCalculationSystem` genera un popup visual flotante sobre la unidad receptora.
El tipo de popup se determina al final del cálculo de daño, una vez conocido el resultado real.

### Categorías y su visual

| Categoría | Color | Texto | Icono | Tamaño |
|-----------|-------|-------|-------|--------|
| `Normal` | Blanco | Valor numérico | — | 1× |
| `Critical` | Dorado #FFD700 | "CRITICO {n}" | — | 1.4× |
| `Blocked` | Gris #AAAAAA | "BLOQ" | Escudo amarillo | 1× |
| `Death` | Rojo #FF3333 | Valor numérico | Calavera | 1.2× |

### Lógica de categoría

```
if HP_post_damage <= 0  → Death
else if multiplier > 1  → Critical
else                    → PendingDamageEvent.category  (Normal / Ability)
```

`Blocked` se emite en la rama de escudo de `DamageCalculationSystem`, antes de aplicar daño al HP.

### Infraestructura FCT

| Archivo | Rol |
|---------|-----|
| `Assets/Scripts/Combat/DamageFeedbackEvent.cs` | Evento de presentación que crea `DamageCalculationSystem` (posición, categoría, cantidad); la lógica de daño no llama a la UI |
| `Assets/Scripts/Combat/DamageFeedbackPresentation.System.cs` | Consume los eventos en `PresentationSystemGroup` y llama a `FloatingCombatTextManager.Spawn` (1,8 m sobre el objetivo) |
| `Assets/Scripts/UI/Battle/FloatingCombatTextManager.cs` | Singleton + pool (20 entradas). `Spawn(pos, category, dmg)` |
| `Assets/Scripts/UI/Battle/FCTEntry.cs` | Canvas world-space por entrada. Billboard, animación float+fade, pool callback |
| `Assets/Scripts/Combat/DamageCategory.cs` | Enum indexado por el manager para resolver sprite de icono |

---

## 13. Referencia Técnica Rápida

### Flujo de datos resumido

```
Input (teclado/IA)
    ↓
UnitAttackSystem / SkillExecutionSystem (héroe)
    → activa WeaponHitboxActiveTag / WeaponCollider
    ↓
HitboxCollisionSystem
    → AABB overlap (Hitbox layer 7 vs Hurtbox layer 6)
    → crea PendingDamageEvent
    ↓
DamageCalculationSystem
    → shield check → fórmula → apply health
    → IsDeadComponent si health ≤ 0
```

### Layers de física relevantes

| Layer | Número | Descripción |
|---|---|---|
| Hurtbox | 6 | Cápsulas de colisión de unidades (reciben daño) |
| Hitbox | 7 | Cajas de armas (producen daño) |

### Grupos de actualización relevantes

Los sistemas de ataque y detección se ejecutan antes que `DamageCalculationSystem` para garantizar que los eventos existan cuando se procesen en el mismo frame.
