# Guía: Agregar un Nuevo Hero AI Behavior

Esta guía describe los pasos exactos para agregar un nuevo comportamiento autónomo
a los héroes remotos. El sistema usa `IEnableableComponent` — Unity ECS auto-descubre
el nuevo system, y el filtrado es a nivel de chunk (cero overhead en runtime).

---

## Resumen rápido

| Paso | Archivo | Acción |
|------|---------|--------|
| 1 | `Components/MiBehaviorActive.Component.cs` | Crear tag IEnableableComponent |
| 2 | `Systems/HeroAIMiBehavior.System.cs` | Crear behavior system |
| 3 | `BattleSceneController.cs` | Registrar tag en spawn |

---

## Paso 1 — Crear el tag `IEnableableComponent`

**Ubicación:** `Assets/Scripts/Hero/AI/Components/`

**Nombre:** `{NombreBehavior}BehaviorActive.Component.cs`

```csharp
using Unity.Entities;

/// <summary>
/// Enableable tag — presente en todos los héroes AI, activo solo cuando este héroe
/// usa el behavior <see cref="HeroAI{NombreBehavior}System"/>.
/// Activar/desactivar con <c>SetComponentEnabled</c> para cambiar behavior en runtime.
/// </summary>
public struct {NombreBehavior}BehaviorActive : IComponentData, IEnableableComponent { }
```

**Ejemplo** (`FlankerBehaviorActive.Component.cs`):
```csharp
using Unity.Entities;

public struct FlankerBehaviorActive : IComponentData, IEnableableComponent { }
```

---

## Paso 2 — Crear el behavior system

**Ubicación:** `Assets/Scripts/Hero/AI/Systems/`

**Nombre:** `HeroAI{NombreBehavior}.System.cs`

El system solo corre para héroes con el tag activo — no se necesita ningún check de profile.

```csharp
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Decision system para el behavior {NombreBehavior}.
///
/// Filosofía: [describe aquí el objetivo táctico del behavior]
///
/// Pipeline: BattleWorldStateSystem → HeroAIPerceptionSystem → THIS → HeroAIExecutionSystem
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(HeroAIPerceptionSystem))]
[UpdateBefore(typeof(HeroAIExecutionSystem))]
public partial class HeroAI{NombreBehavior}System : SystemBase
{
    protected override void OnUpdate()
    {
        // Leer TeamWorldState para datos del equipo
        TeamWorldState ws = null;
        SystemAPI.ManagedAPI.TryGetSingleton<TeamWorldState>(out ws);

        // La query filtra SOLO héroes con este behavior activo — ECS lo hace a nivel de chunk
        foreach (var (transform, life, entity) in
                 SystemAPI.Query<RefRO<LocalTransform>,
                                 RefRO<HeroLifeComponent>>()
                          .WithAll<HeroAITag, {NombreBehavior}BehaviorActive>()
                          .WithEntityAccess())
        {
            var bb = EntityManager.GetComponentObject<HeroAIBlackboard>(entity);
            if (bb == null) continue;

            var dec = new HeroAIDecision();

            // 1. Dead → idle (siempre el primer check)
            if (!bb.selfIsAlive)
            {
                dec.action = AIActionType.Idle;
                SystemAPI.SetComponent(entity, dec);
                continue;
            }

            // TODO: implementar lógica del behavior aquí
            // Lee bb.* para datos per-hero (distancias, zona actual, HP)
            // Lee ws.For(bb.selfTeam).* para datos completos del equipo

            dec.action = AIActionType.Idle;
            SystemAPI.SetComponent(entity, dec);
        }
    }
}
```

### Datos disponibles

**`HeroAIBlackboard bb`** — datos per-hero pre-calculados por Perception:
```
bb.selfPosition, bb.selfHealthPercent, bb.selfStaminaPercent, bb.selfIsAlive
bb.selfTeam, bb.selfIsAttacker
bb.ownSquadType, bb.ownSquadIsRanged, bb.squadIsInCombat, bb.squadCurrentOrder
bb.nearestEnemyHero, bb.nearestEnemyPosition, bb.nearestEnemyDistanceSq
bb.bestObjectiveZone, bb.bestObjectivePosition
bb.threatZone, bb.threatZonePosition
bb.isInsideAnyZone, bb.zoneImInside, bb.zoneImInsideInfo
bb.spawnPosition
```

**`TeamWorldState ws`** — datos completos del equipo:
```csharp
var myView = ws.For(bb.selfTeam);
myView.allyHeroes          // List<HeroSnapshot> — todos los aliados (incluye local)
myView.visibleEnemyHeroes  // List<HeroSnapshot> — enemigos detectados (fog-of-war)
myView.allySquads          // List<SquadSnapshot>
myView.visibleEnemySquads  // List<SquadSnapshot>
ws.zones                   // List<ZoneSnapshot> — todos los puntos del mapa
ws.match                   // MatchContext: isActive, stateTimer, winnerTeam
```

**`HeroAIDecision dec`** — lo que escribe este system para Execution:
```csharp
dec.action           = AIActionType.{MoveTo|AttackTarget|CaptureZone|DefendZone|Retreat|Idle};
dec.targetPosition   = float3;
dec.targetEntity     = Entity;
dec.shouldSprint     = bool;
dec.shouldAttack     = bool;
dec.squadOrder       = SquadOrderType.{FollowHero|HoldPosition|Attack};
dec.squadOrderPosition = float3;   // solo para HoldPosition
dec.hasNewSquadOrder = bool;
```

---

## Paso 3 — Registrar el tag en spawn

**Archivo:** `Assets/Scripts/UI/Battle/BattleSceneController.cs`

Busca el bloque de spawn de AI (cerca de línea 531). Añade **2 líneas** junto a los otros tags:

```csharp
// Todos los behaviors presentes — solo 1 activo a la vez
em.AddComponent<RusherBehaviorActive>(heroEntity);
em.SetComponentEnabled<RusherBehaviorActive>(heroEntity, false);
em.AddComponent<BalancedBehaviorActive>(heroEntity);   // activo por defecto
em.AddComponent<TacticianBehaviorActive>(heroEntity);
em.SetComponentEnabled<TacticianBehaviorActive>(heroEntity, false);
// ↓ Añadir aquí:
em.AddComponent<{NombreBehavior}BehaviorActive>(heroEntity);
em.SetComponentEnabled<{NombreBehavior}BehaviorActive>(heroEntity, false);
```

> El tag se añade **disabled** — `IEnableableComponent` se habilita por defecto al hacer
> `AddComponent`, por lo que hay que desactivarlo explícitamente si no es el behavior por defecto.

---

## Cambiar el behavior activo de un héroe

Para asignar el nuevo behavior a un héroe en runtime (ej. desde una UI de debug o matchmaking):

```csharp
// Desactivar el behavior actual
em.SetComponentEnabled<BalancedBehaviorActive>(heroEntity, false);

// Activar el nuevo
em.SetComponentEnabled<{NombreBehavior}BehaviorActive>(heroEntity, true);
```

Sin structural changes, Burst-friendly, efectivo el próximo frame.

---

## Checklist de verificación

- [ ] `{NombreBehavior}BehaviorActive.Component.cs` creado en `Hero/AI/Components/`
- [ ] `HeroAI{NombreBehavior}.System.cs` creado en `Hero/AI/Systems/` con `WithAll<HeroAITag, {NombreBehavior}BehaviorActive>()`
- [ ] 2 líneas añadidas en `BattleSceneController.cs` (AddComponent + SetComponentEnabled false)
- [ ] El system compila sin errores en Unity
- [ ] En Entity Debugger: el tag aparece **disabled** en héroes sin este behavior asignado
- [ ] Al habilitar el tag manualmente en Entity Debugger, el héroe cambia de comportamiento

---

## Referencia rápida — Behaviors existentes

| Behavior | Tag | System | Filosofía |
|----------|-----|--------|-----------|
| Rusher | `RusherBehaviorActive` | `HeroAIRusherSystem` | Rush directo a objetivos, pelea solo si le bloquean |
| Balanced | `BalancedBehaviorActive` | `HeroAIBalancedSystem` | Evalúa amenazas, se retira con HP bajo, defiende zonas propias |
| Tactician | `TacticianBehaviorActive` | `HeroAITacticianSystem` | Coordinación de equipo (stub — extiende Balanced) |

**Arquitectura completa:** ver `Docs/RemoteHeroAI_Architecture.md`
