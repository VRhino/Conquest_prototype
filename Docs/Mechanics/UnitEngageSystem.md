# UnitNavMeshSystem — Combat Engage Logic

> Revisado línea a línea contra `UnitNavMesh.System.cs` el 2026-10-22. El pseudocódigo resume la lógica; el anillo de contacto y el punto de frenado se describen en `Docs/Design/Movimiento_Formaciones.md` (§13, §12). Los valores de `MovementTuningConfig` (LFS) no se pudieron leer y los de la tabla vienen del documento anterior.

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
    → UnitRotationIntentComponent (prioridad Combat, cuando en combatFacingRange); UnitRotationResolutionSystem la aplica
UnitFollowFormationSystem         ← orientación Formed (intención de prioridad Formation = 5)
    → UnitRotationIntentComponent (Formation) solo si no hay una de mayor prioridad (Combat = 10)
MeleeContactRingSystem             ← (antes de UnitNavMeshSystem) reserva el anillo de contacto
UnitAvoidanceSystem / UnitBodyblockSystem ← evitación entre aliados / contacto físico entre enemigos
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

// Correa (C6): un Attack que viene de una reacción de combate solo persigue dentro de
// unitLeashDistance desde el slot; el Attack del jugador o de la IA persigue sin correa.
if combatTarget exists AND CanPursueTarget(order, resolvedOrder.source, slot, target, leash):
    dist       = distanceXZ(unit, target)
    // Parada derivada (C5, MeleeReach): cuerpos separados y objetivo dentro del golpe.
    reach      = attackRange − bodyRadius
    stopDist   = clamp(2·bodyRadius + meleeStopReachFraction·reach,
                       2·bodyRadius + 0.05, attackRange + bodyRadius)
    resumeDist = attackRange + bodyRadius        // a distancia: range·rangedStopFraction / range
    // Histéresis: se acerca hasta stopDist y no vuelve a avanzar hasta pasar resumeDist.
    if dist > resumeDist: combatApproaching = true
    elif dist <= stopDist: combatApproaching = false

    // Destino
    if order != HoldPosition AND combatApproaching:
        destination = posición reservada en el anillo (MeleeContactSlotComponent);
                      sin reserva: targetPos + rotatedDir(±45°, desfase estable por unidad) × stopDist;
                      anillo lleno: espera más lejos, en su lado
    elif order != HoldPosition:
        destination = punto donde termina de frenar (MeleeReach.BrakingStopPoint), no "aquí" yendo deprisa

    // Rotación
    if dist <= combatFacingRange (3.5; a distancia: attackRange + 0.25):
        updateRotation = false
        UnitRotationIntent(Combat) = LookAt(target)  // AABB apunta al enemigo
    else:
        updateRotation = true                        // NavMesh rota durante marcha
else:
    updateRotation = true                            // sin target: comportamiento normal

agent.SetDestination(destination)
```

## Constantes configurables

| Constante | Valor | Descripción |
|-----------|-------|-------------|
Todas viven en `MovementTuningConfig` (`DefaultMovementTuning.asset`):

| Constante | Valor | Descripción |
|-----------|-------|-------------|
| `bodyRadius` | `0.5` | Radio del cuerpo (D10), para parada, "en rango" y evitación |
| `meleeStopReachFraction` | `0.6` | Parte del alcance útil (`attackRange − bodyRadius`) que se recorre más allá del contacto |
| `rangedStopFraction` | `0.92` | Parada a distancia, como fracción de `range` |
| `combatFacingRange` | `3.5` | Distancia para activar la rotación manual hacia el objetivo |
| `combatTurnRate` | `450` °/s | Tope del giro de combate (C7, `UnitRotationResolutionSystem`) |

"En rango" (`UnitAttackSystem`, `UnitTargetingSystem`) usa `MeleeReach.InAttackRange`:
`dist ≤ attackRange + bodyRadius`, porque `attackRange` llega al borde del hitbox, no al centro del objetivo.
El baker de escuadras avisa si un arma deja menos de 0,2 m de alcance útil más allá del cuerpo.

## Relación con UnitFollowFormationSystem
Las dos escriben `UnitRotationIntentComponent` y gana la mayor prioridad (`RotationSource`: NavMesh 0, Formation 5, Combat 10); `UnitRotationResolutionSystem` la aplica con tope `combatTurnRate`. Una unidad `Formed` sin objetivo mira en la dirección del escuadrón; en cuanto tiene objetivo a `combatFacingRange`, la intención de combate prevalece.
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
