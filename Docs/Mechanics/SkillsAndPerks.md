# Habilidades y perks del héroe

Estado: implementado (2026-10-22, Fase 2 de `Docs/Design/Aprendizajes_Referencia.md`). Patrones tomados del análisis
estático de la referencia (`game-re/notes/skill-lifecycle.md`, `preconditions.md`, `effects-catalog.md`); ningún código ni
dato de la referencia.

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

## 2. Teclas

| Tecla | Hueco (`SkillSlot`) | Qué lanza |
|---|---|---|
| Clic izquierdo | `Basic` | Ataque básico (cadena de 3 golpes) |
| Q / E / R | `Q` / `E` / `R` | Habilidades de clase |
| F | `Ultimate` | Definitiva |
| 1 / 2 | `ActivePerk1` / `ActivePerk2` | Perks activos del loadout |
| G | — | Interactuar (antes F) |

Con varias teclas pulsadas gana la definitiva, luego R, E, Q, perks y el básico (`HeroSkillRequestSystem`).

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

### 3.2 Fases (`SkillPhase`)

- `duration`. Si es negativa, la fase no termina sola: es la espera de una carga.
- `interruptible`. Solo una fase interrumpible deja entrar a otra habilidad.
- `final`. Es la fase de liberación, siempre la última.
- `charge`. Marca la fase de carga.

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

## 4. Efectos (`SkillEventType`)

| Evento | Qué hace |
|---|---|
| `Animation` | Dispara la animación de ataque (provisional hasta tener la propia de cada habilidad) |
| `HitboxOpen` / `HitboxClose` | Abre o cierra el golpe del arma |
| `AreaDamage` | Un `PendingDamageEvent` (categoría `Ability`, perfil del héroe × `value`) a cada enemigo vivo en el círculo o el cono (`radius`, `angle`). El centro va `distance` metros por delante del héroe |
| `ApplyModifier` | Modificador temporal en la pila de cada objetivo (fuente `Effect`, id = la habilidad). Al relanzar se refresca, no se acumula |
| `AbsorbShield` | Escudo absorbente (`value`, `duration`). Si ya hay uno, se queda el mayor |
| `Dash` | Embestida de `distance` metros en `duration` segundos. El héroe local la hace con su controlador y la IA con su `NavMeshAgent` |

**Objetivos** (`SkillTarget`):
- `Self`: el propio héroe.
- `OwnSquad`: las unidades de su escuadra.
- `AlliesInRadius`: aliados en el radio, con el héroe incluido.
- `EnemiesInRadius`: enemigos en el radio, alrededor del centro del efecto.

### 4.1 Habilidades de clase (datos en `Assets/ScriptableObjects/Hero/Abilities/`)

| Clase | Q | E | R | F |
|---|---|---|---|---|
| Espada y escudo | Empuje de Escudo: embestida 3 m + cono 90° | Defensa Reforzada: +30 % de defensa a aliados (5 m, 8 s) | Intercepción: embestida 4 m + círculo | Muro Imparable: +50 % de defensa a aliados (6 m, 10 s) |
| Espada a dos manos | Corte Giratorio: círculo 3 m ×1,2 | Carga Brutal: embestida 6 m + cono 120° | Lluvia de Acero: 3 golpes en cono | Juicio de Acero: cono ×2 + escuadra +20 % de daño (8 s) |
| Lanza | Barrido Largo: semicírculo + ralentiza a los enemigos | Estocada Precisa: cono estrecho 3,5 m ×1,5 | Despliegue Defensivo: +25 % de defensa a aliados | Muro de Púas: área por delante + ralentización |
| Arco | Disparo Enfocado: tiro cargado (cono estrecho 25 m) | Lluvia de Flechas: 3 oleadas a 15 m | Flecha Silenciadora: ralentiza (aproximación) | Flecha Llameante: 3 pulsos de área a 15 m |

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
- **Selección:** `PerkLoadout` toma del loadout activo (`HeroData.loadouts[].perkIDs`) hasta 5 pasivos y 2 activos, entre
  los perks de la clase. Los héroes remotos y la IA no tienen perks de loadout.
- **Movimiento:** `HeroMotionSystem` mide cuánto lleva quieto el héroe y desde cuándo no se mueve.
- **Auras:** `PerkAuraSystem` aplica los efectos `Always`. Mientras se cumple la condición, refresca cada 0,25 s un
  modificador que dura el doble (fuente `Perk`). Así sigue a la escuadra y se apaga solo.
- **Al golpear:** `PerkOnHitSystem` aplica los efectos `OnHit` al objetivo de cada golpe del héroe, antes de que se
  calcule el daño.
- **Activos:** cada perk activo es una habilidad de una fase que aplica sus efectos, con el enfriamiento del perk y
  refresh `Yield`.

| Perk | Tipo | Efecto implementado |
|---|---|---|
| Línea Inquebrantable | Pasivo | Héroe quieto 2 s → su escuadra +10 % de defensa |
| Cobertura de Hostigamiento | Pasivo | Héroe quieto 3 s → sus golpes ralentizan al objetivo un 15 % (2 s) |
| Estocada Desestabilizadora | Pasivo | Tras moverse → el golpe ralentiza al objetivo un 30 % (1,5 s) |
| Orden de Resistencia | Activo (30 s) | Aliados a 8 m +15 % de defensa (6 s) |
| Carga Coordinada | Activo (25 s) | Héroe y escuadra +20 % de daño (3 s) |
| Flecha Marcadora | Activo (20 s) | Enemigos a 12 m −10 % de defensa (6 s) |
| Muro de Púas, Zona de Exclusión | Pasivo | Sin datos: piden daño por contacto y zonas persistentes |

## 6. HUD

`UIBattleDataSystem` copia en `UIHeroBattleDataComponent.skillCooldown` la fracción de enfriamiento de Q, E, R y F.
`HUDController` la pinta en sus cuatro huecos. Los perks activos aún no tienen hueco en el HUD.

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
