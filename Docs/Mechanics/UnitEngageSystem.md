# UnitNavMeshSystem — Combat Engage Logic

## Responsabilidad

`UnitNavMeshSystem` es la **única autoridad** para todas las decisiones de navegación
NavMesh de unidades. Esto incluye el comportamiento de engagement en combate.

## Por qué no existe un UnitEngageSystem separado

Un sistema separado requeriría:
- Flag de coordinación `isFacingTarget` en `UnitCombatComponent` (coupling implícito)
- Múltiples escritores a `agent.updateRotation` sin orden garantizado
- Sobreescritura de `UnitTargetPositionComponent` desde fuera del pipeline de formación

`UnitNavMeshSystem` resuelve todo en un lugar: primero decide el destino, luego la rotación.

## Pipeline de ejecución

```
FormationSystem / GridFormationUpdateSystem
    → escribe UnitTargetPositionComponent (slot — ownership exclusivo)
UnitFormationStateSystem
    → transiciona Moving/Formed/Waiting
[UnitNavMeshSystem]               ← engagement + movimiento + rotación combate
    → agent.SetDestination(destination)
    → agent.updateRotation = false/true
    → LocalTransform.Rotation (cuando en EngagementRange)
UnitFollowFormationSystem         ← orientación Formed (last write = prioridad más alta)
    → navAgent.transform.rotation (Formed state, slerp hacia hero/hold direction)
UnitBodyblockSystem               ← repulsión física cross-team
UnitAttackSystem                  ← dispara si target en AABB del arma
```

## Algoritmo de decisión

### Fase 0 — Build unit→order map
```
foreach squad:
    foreach unit in squad:
        _unitToOrder[unit] = squad.currentOrder
```
O(total_units). Evita joins por unidad.

### Fase 1 — Por cada unidad con NavAgentComponent

```
destination = formationSlot    // default: formation slot (UnitTargetPositionComponent)

if combatTarget exists:
    dist     = distanceXZ(unit, target)
    stopDist = attackRange × 0.75       // dentro del AABB de ataque

    // Destino
    if order != HoldPosition AND dist > stopDist:
        destination = targetPos + normalize(unitPos - targetPos) × stopDist

    // Rotación
    if dist <= EngagementRange (3.5u):
        updateRotation = false
        LocalTransform.Rotation = LookAt(target)    // AABB apunta al enemigo
    else:
        updateRotation = true                        // NavMesh rota durante marcha
else:
    updateRotation = true                            // sin target: comportamiento normal

agent.SetDestination(destination)
```

## Constantes configurables

| Constante | Valor | Descripción |
|-----------|-------|-------------|
| `StopDistanceFactor` | `0.75f` | Fracción de `attackRange` donde la unidad se detiene |
| `EngagementRange` | `3.5f` | Distancia (u) para activar rotación manual |

## Relación con UnitFollowFormationSystem

`UnitNavMeshSystem` corre **antes** que `UnitFollowFormationSystem` (atributo
`[UpdateBefore(UnitFollowFormationSystem)]`).

Para unidades en estado `Formed`:
- `UnitNavMeshSystem` setea `updateRotation = true` (no hay target o fuera de rango)
- `UnitFollowFormationSystem` corre después y sobreescribe con `updateRotation = false`
  + `navAgent.transform.rotation = Slerp(heroForward / holdRotation)` — **última escritura gana**

Esto asegura que unidades en formación siempre miren en la dirección del escuadrón,
no en la del enemigo — comportamiento correcto para disciplina táctica.

## HoldPosition

Con `SquadOrderType.HoldPosition`:
- Unidades **no persiguen** al target (destination = formationSlot)
- Si el enemigo entra a `EngagementRange`, **sí rotan** para mirarlo
- `UnitAttackSystem` puede disparar porque el AABB ahora apunta al enemigo

## Verificación en Play Mode

1. Squad A vs Squad B en `BattleTestScene`
2. Unidades con target (línea blanca en Gizmos) se mueven hacia el enemigo
3. Al entrar en `stopDist`, se detienen y rotan para mirarlo
4. Gizmo naranja de hitbox se activa → `UnitAttackSystem` disparó
5. `HoldPosition`: unidades no se mueven pero atacan si el enemigo se acerca
6. Unidades `Formed` sin target siguen mirando en la dirección del héroe
