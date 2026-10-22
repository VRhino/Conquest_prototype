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

### Estado frente al código (2026-10-21, tras la Fase 1 de `Docs/Design/Aprendizajes_Referencia.md`)
| Apartado | Código de referencia | Estado |
|---|---|---|
| Origen de los stats (§4.1) | `Stats/StatResolve.System.cs`, `Stats/StatMath.cs`, `Stats/StatStack.cs` | Sincronizado (T1) |
| Fórmula (§5) | `Combat/DamageFormula.cs` (`DamageFormula.Compute`, pura; constantes en `CombatTuning`) | Sincronizado (D1 y L1-03 de `Docs/Design/Aprendizajes_Referencia.md`) |
| Escudo (§6) | `DamageCalculation.System.cs`, `ShieldHitboxBehaviour`, `BlockRegen.System.cs` | Sincronizado; la orientación no se usa; el aturdimiento va por la pila (L5-3) |
| Escudo absorbente (§6.7) | `Combat/AbsorbShield.cs`, `AbsorbShield.Component.cs` | Sincronizado (L1-06); aún sin fuente |
| Crítico (§5.3) | `Stats/CritFormula.cs`, tirada `CombatRandom.RollCritical` en `MeleeContactResolution.System.cs` y `RangedAttack.System.cs` | Sincronizado (L1-04); tirada reproducible (L1-05) |

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
│  FASE 2 — DETECCIÓN                                         │
│                                                             │
│  HitboxCollisionSystem (por frame, solo hitbox activo)      │
│     │                                                       │
│     ├─ Construye AABB desde parámetros del arma             │
│     ├─ OverlapAabb contra capa Hurtbox (layer 6)            │
│     ├─ Calcula si es golpe crítico (random ≤ critChance)    │
│     ├─ Crea PendingDamageEvent en la entidad objetivo       │
│     └─ Marca hitboxFired = true (un solo evento por swing)  │
└─────────────────────────────────────────────────────────────┘
                        │
                        ▼
┌─────────────────────────────────────────────────────────────┐
│  FASE 3 — CÁLCULO Y APLICACIÓN                              │
│                                                             │
│  DamageCalculationSystem (procesa PendingDamageEvent)       │
│     │                                                       │
│     ├─ Valida existencia de entidades                       │
│     ├─ Comprueba friendly fire (TeamComponent)              │
│     ├─ Evalúa bloqueo de escudo                             │
│     │     └─ Si bloquea: reduce currentBlock −50, cancela  │
│     ├─ Resuelve defensa y penetración por tipo de daño      │
│     ├─ Aplica fórmula de mitigación                         │
│     ├─ Aplica bonificaciones (cinética, altura, crítico)    │
│     ├─ Reduce currentHealth del objetivo                    │
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
| `Effect` | `DamageCalculationSystem` | aturdimiento por rotura de escudo: override de velocidad a 0 que caduca (§6.3) |

En los héroes, `HeroStatFormulaComponent` (coeficientes de la clase) convierte los atributos resueltos en vida, aguante,
daño y defensa. Los héroes no tienen penetración en batalla (nunca la tuvieron). El resolvedor conserva la fracción de
vida y de aguante al cambiar el máximo. Corre después de `UnitStatScalingSystem`; un modificador añadido durante el
frame lo ve el daño en el siguiente.

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
dañoEfectivo = Σ contribuciones × bonoCinético × bonoAltura      (nunca negativo)
```

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
  ├─ ¿hitType == Shield, tiene UnitShieldComponent, currentBlock > 0 y no está roto?
  │     └─ No → dañoEfectivo se resta de la salud
  │
  └─ Sí → currentBlock −= dañoEfectivo
           ├─ currentBlock ≤ 0 → escudo roto: currentBlock = 0, brokenTimer = breakStunDuration, FCT "ROTO",
           │                    velocidad 0 en la pila hasta ahora + breakStunDuration
           └─ si no → FCT "BLOQ" con el daño absorbido
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
| `Assets/Scripts/Combat/DamageProfile.Component.cs` | Runtime baked de DamageProfile: baseDamage, damageType, penetration |
| `Assets/Scripts/Combat/PendingDamageEvent.cs` | Evento de daño pendiente: target, damageSource, damageProfile, category, multiplier, attackDirection, attackerSpeed, attackerPosition |
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
