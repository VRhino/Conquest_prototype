# Mecánicas de Daño

## 1. Resumen

El sistema de daño es **event-driven**: los ataques generan un `PendingDamageEvent` que el sistema central consume y resuelve en el mismo frame. Esto desacopla la detección de colisiones del cálculo de daño.

Características clave:
- **3 tipos de daño**: Blunt, Slashing, Piercing — cada uno con defensa y penetración independiente.
- **Mitigación capeada al 95%**: siempre llega al menos el 5% del daño base.
- **Bonificaciones situacionales**: velocidad cinética y ventaja de altura amplifican el daño.
- **Golpe único por swing**: un atacante genera como máximo un evento de daño por animación de ataque.
- **Escudo pre-cálculo**: el bloqueo de escudo se evalúa antes de aplicar la fórmula; si bloquea, no se reduce la salud.

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
│  UnitAttackSystem / HeroAttackSystem                        │
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

| Tipo de Daño | Campo de Defensa | Campo de Penetración |
|---|---|---|
| Blunt | `DefenseComponent.bluntDefense` | `DamageProfileComponent.penetration` + `PenetrationComponent.bluntPenetration` |
| Slashing | `DefenseComponent.slashDefense` | `DamageProfileComponent.penetration` + `PenetrationComponent.slashPenetration` |
| Piercing | `DefenseComponent.pierceDefense` | `DamageProfileComponent.penetration` + `PenetrationComponent.piercePenetration` |

**Notas:**
- `DamageProfileComponent.penetration` es la penetración base del arma (siempre activa).
- `PenetrationComponent` en el atacante añade penetración extra por tipo; su presencia es opcional.
- La defensa siempre se obtiene del **objetivo**; la penetración, del **atacante**.

---

## 5. Fórmula de Cálculo de Daño

### 5.1 Mitigación base

```
ratio      = defensa / max(penetración, 0.001)
mitigación = min(ratio, 0.95)
```

El cap de 0.95 garantiza que siempre llegue al menos el **5% del daño base**, independientemente de la defensa.

### 5.2 Daño efectivo final

```
dañoEfectivo = baseDamage × multiplier × (1 − mitigación)
                           × bonoCinético
                           × bonoAltura
```

### 5.3 Bonificaciones situacionales

| Bonificación | Condición de activación | Fórmula |
|---|---|---|
| **Crítico** | `random(0,1) ≤ criticalChance` | `multiplier = criticalMultiplier` (ej. 1.5×) |
| **Cinética** | `attackerSpeed > 0` | `× (1 + (velocidad / 5.0) × kineticMultiplier)` |
| **Altura** | Atacante >0.5 m sobre objetivo | `× (1 + deltaAltura × 0.1)` (~10% por metro) |

- `kineticMultiplier` viene de `UnitWeaponComponent`; escala el bono por velocidad del atacante.
- La ventaja de altura se calcula usando `attackerPosition.y − target.position.y`.
- El multiplicador crítico y la probabilidad son configurables por arma en `UnitWeaponComponent`.

### 5.4 Parámetros clave

| Parámetro | Valor | Descripción |
|---|---|---|
| Penetración mínima | 0.001 | Evita división por cero |
| Cap de mitigación | 0.95 | Daño mínimo garantizado: 5% |
| Velocidad de referencia cinética | 5.0 m/s | Normaliza la bonificación |
| Umbral de altura | 0.5 m | Diferencia mínima para activar bono |
| Reducción de escudo por bloqueo | 50 pts | Costo fijo de bloquear un golpe |

---

## 6. Sistema de Escudo

El escudo se evalúa **antes** de la fórmula de daño. Si un golpe es bloqueado, no se reduce la salud.

### 6.1 Componente de escudo

`UnitShieldComponent` almacena:
- `currentBlock`: puntos de bloqueo disponibles.
- `maxBlock`: capacidad máxima.
- `regenRate`: puntos regenerados por segundo fuera de combate.
- `orientation`: dirección desde la que protege el escudo.

### 6.2 Orientaciones de escudo

| Orientación | Descripción |
|---|---|
| Forward | Bloquea ataques frontales (dot(`attackDirection`, `shield.forward`) > 0.5) |
| Left / Right | Protección lateral (reservado; sin lógica activa actualmente) |
| All | Bloquea desde cualquier dirección |

### 6.3 Flujo de bloqueo

```
PendingDamageEvent llega al objetivo
  │
  ├─ ¿currentBlock > 0?
  │     └─ No → daño pasa a la fórmula normal
  │
  ├─ ¿orientación cubre la dirección del ataque?
  │     └─ No → daño pasa a la fórmula normal
  │
  └─ Sí → currentBlock −= 50
           PendingDamageEvent eliminado (sin daño a salud)
```

### 6.4 Regeneración de escudo

`BlockRegenSystem` incrementa `currentBlock` cada frame:
```
currentBlock = min(maxBlock, currentBlock + regenRate × deltaTime)
```
No hay condición explícita de "fuera de combate" en la implementación actual; el regen ocurre siempre.

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
   ├─ Pase 1: detecta unidades enemigas dentro del rango del squad (distancia al centroide)
   ├─ Pase 2: propaga lista SquadTargetEntity → UnitDetectedEnemy por unidad
   └─ Limpia y repuebla buffers cada frame

UnitTargetingSystem (cada frame)
   ├─ Asigna UnitCombatComponent.target al enemigo más cercano disponible
   └─ Redistribuye si >3 unidades apuntan al mismo objetivo
```

Solo las unidades en estado Attack, FollowHero o HoldPosition reciben asignación de target.

---

## 10. Edge Cases

| Caso | Resolución |
|------|-----------|
| Penetración = 0 en el arma | Se usa mínimo 0.001 para evitar división por cero en el ratio |
| Defensa muy alta sin penetración | Mitigación capeada al 95%; siempre llega al menos 5% del daño |
| Friendly fire | `DamageCalculationSystem` compara `TeamComponent`; descarta el evento si mismo equipo |
| Hitbox activo durante múltiples frames | Flag `hitboxFired` garantiza un único `PendingDamageEvent` por swing |
| Escudo con `currentBlock ≤ 0` | El chequeo falla; el daño pasa directamente a la fórmula normal |
| Escudo `orientation = All` | Bloquea independientemente de `attackDirection` |
| Objetivo destruido entre la creación y el procesado del evento | `DamageCalculationSystem` verifica existencia de la entidad al inicio; descarta si no existe |
| Múltiples atacantes en el mismo frame | Se procesan todos los `PendingDamageEvent` en orden; la salud puede caer a negativo, pero `IsDeadComponent` solo se añade una vez |
| Héroe sin stamina suficiente | `HeroAttackSystem` bloquea el swing; no se crea evento de daño |
| Modo Brace activado | `BraceWeaponSystem` aplica perfiles por fila antes de que `UnitAttackSystem` evalúe rango/timing |

---

## 11. Infraestructura existente

### Enums y ScriptableObjects

| Archivo | Rol |
|---------|-----|
| `Assets/Scripts/Combat/DamageType.cs` | Enum: Blunt, Slashing, Piercing |
| `Assets/Scripts/Combat/DamageCategory.cs` | Enum: Normal, Critical, Ability (para UI de popups) |
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
| `Assets/Scripts/Combat/UnitShield.Component.cs` | Bloqueo de escudo: currentBlock, maxBlock, regenRate, orientation |
| `Assets/Scripts/Combat/WeaponHitbox.Component.cs` | WeaponHitboxOwner, WeaponHitboxRef, WeaponHitboxActiveTag (IEnableableComponent) |
| `Assets/Scripts/Combat/WeaponCollider.Component.cs` | Hitbox del héroe: owner, isActive |
| `Assets/Scripts/Combat/Team.Component.cs` | Equipo de la entidad: TeamComponent.value |
| `Assets/Scripts/Hero/Components/HeroLife.Component.cs` | Ciclo de vida del héroe: isAlive, deathTimer, respawnCooldown |
| `Assets/Scripts/Combat/BraceWeaponProfiles.Component.cs` | SquadCombatModeComponent (Normal/Brace) y BraceRowProfile (buffer por fila) |

### Sistemas ECS

| Archivo | Rol |
|---------|-----|
| `Assets/Scripts/Combat/DamageCalculation.System.cs` | **Sistema central**: consume PendingDamageEvent, aplica fórmula, reduce salud, añade IsDeadComponent |
| `Assets/Scripts/Combat/HitboxCollision.System.cs` | Detección AABB → crea PendingDamageEvent durante la ventana de golpe |
| `Assets/Scripts/Combat/UnitAttack.System.cs` | FSM de 3 fases por unidad: decisión → ventana de golpe → fin de animación |
| `Assets/Scripts/Hero/Systems/HeroAttack.System.cs` | Ataque del héroe: verifica stamina, activa collider, detecta colisiones |
| `Assets/Scripts/Combat/WeaponHitboxPosition.System.cs` | Actualiza posición del hitbox para seguir la punta del arma cada frame |
| `Assets/Scripts/Combat/BlockRegen.System.cs` | Regenera currentBlock de todos los escudos cada frame |
| `Assets/Scripts/Hero/Systems/HeroRespawn.System.cs` | Gestiona muerte y respawn del héroe vía deathTimer |
| `Assets/Scripts/Combat/UnitTargeting.System.cs` | Asigna target a cada unidad; redistribuye si hay concentración excesiva |
| `Assets/Scripts/Combat/EnemyDetection.System.cs` | Detecta enemigos en rango y propaga buffers UnitDetectedEnemy |
| `Assets/Scripts/Combat/BraceWeaponActivation.System.cs` | Activa/desactiva modo Brace al entrar/salir de HoldingPosition |
| `Assets/Scripts/Combat/BraceWeapon.System.cs` | Aplica perfiles de arma por fila durante modo Brace |

### Authoring / Baker

| Archivo | Rol |
|---------|-----|
| `Assets/Scripts/Combat/DamageProfile.Authoring.cs` | Baker que convierte DamageProfile ScriptableObject → DamageProfileComponent en tiempo de bake |

---

## 12. Referencia Técnica Rápida

### Flujo de datos resumido

```
Input (teclado/IA)
    ↓
UnitAttackSystem / HeroAttackSystem
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
