# Formation Milestone System — Hitos del ciclo de vida de unidades

Revisado línea a línea contra el código el 2026-10-22.

> `FormationStanceSystem` ya no existe. La postura la decide `UnitPostureSystem` por papel de ranura y estado (formado / en marcha), con la tabla de posturas de cada `FormationDefinition`, y **no lee los hitos**: ver [FormationPostures.md](./FormationPostures.md). Hoy ningún sistema consume `UnitStartedMovingTag` ni `UnitArrivedAtSlotTag`; solo los escribe `UnitFormationStateSystem` y los añade `SquadUnitSpawnBuilder` al crear la unidad. El framework sigue disponible como contrato para quien necesite reaccionar a esas transiciones.

## Propósito

Permite enganchar comportamientos a momentos específicos del ciclo de vida del movimiento de unidades **sin modificar los sistemas core** (`UnitFormationStateSystem`, `UnitFollowFormationSystem`, `FormationSystem`). Los tags actúan como señales de un solo frame.

---

## Componentes

### Tags de hito (IEnableableComponent)

| Componente              | Descripción                                                     |
|-------------------------|-----------------------------------------------------------------|
| `UnitStartedMovingTag`  | Pulso 1 frame: la unidad pasa de Waiting a Moving               |
| `UnitArrivedAtSlotTag`  | Pulso 1 frame: la unidad pasa de Moving a Formed                |

Archivo: `Assets/Scripts/Squads/UnitFormationMilestone.Component.cs`

### Postura (escrita por `UnitPostureSystem`, no por los hitos)

`UnitFormationStanceComponent.stance` (enum `UnitStance`: `Normal`, `BracedShields`, `ShieldFront`, `ShieldSide`, `ShieldUp`). `UnitPostureSystem` la copia, junto con fila y papel de ranura, a `UnitAnimationMovementComponent` (`CurrentStance`, `SlotRow`, `SlotRole`), y `UnitAnimatorController` escribe los parámetros `Stance` y `SlotRole` del Animator si existen.

---

## Sistemas implicados

| Sistema                    | UpdateAfter                   | Responsabilidad                                                                 |
|----------------------------|-------------------------------|---------------------------------------------------------------------------------|
| `UnitFormationStateSystem` | `GridFormationUpdateSystem`   | Dueño de las transiciones. Resetea los tags al inicio de cada unidad y habilita el que corresponde |
| `UnitPostureSystem`        | `UnitFormationStateSystem`    | Postura por papel de ranura; no lee los tags |

---

## Mecanismo de pulso

```
UnitFormationStateSystem (por unidad, solo si tiene los tags):
  1. Deshabilita UnitStartedMovingTag y UnitArrivedAtSlotTag
  2. Ejecuta FormationArrival.Step
     StartedMoving (Waiting → Moving) → habilita UnitStartedMovingTag
     Arrived       (Moving  → Formed)  → habilita UnitArrivedAtSlotTag
     Resettled     (Waiting → Formed)  → sin tag (transición silenciosa)
```

`Arrived` se emite una sola vez por llegada: la unidad está a menos de `arrivalRadiusIn` y el ancla lleva `arrivalCalmTime` quieta (ver `TroopMovementPipeline.md` §7). Una vuelta a `Formed` desde `Waiting` sin haberse movido (`Resettled`) no emite hito.

Los tags son `IEnableableComponent`: la entidad siempre tiene el componente, pero su estado enabled cambia por frame. Cada tag representa exactamente una transición, visible durante un único frame. No se deshabilitan manualmente desde otros sistemas: lo hace el reset del paso 1.

---

## Cómo añadir un comportamiento nuevo

1. No modifiques `UnitFormationStateSystem` salvo que necesites un nuevo tipo de transición (nuevo tag).
2. Crea un sistema `[UpdateAfter(typeof(UnitFormationStateSystem))]` en `SimulationSystemGroup`.
3. Comprueba los tags:
   ```csharp
   bool startedMoving = SystemAPI.IsComponentEnabled<UnitStartedMovingTag>(unit);
   bool arrived       = SystemAPI.IsComponentEnabled<UnitArrivedAtSlotTag>(unit);
   if (!startedMoving && !arrived) continue;
   ```
4. Aplica tu lógica solo en los frames en que el tag está activo.

---

## Edge cases

| Caso                                           | Comportamiento                                                                                  |
|------------------------------------------------|-------------------------------------------------------------------------------------------------|
| Unidad sin los tags (`hasMilestoneTags = false`) | `UnitFormationStateSystem` no los habilita nunca; la transición de estado funciona igual |
| `Waiting → Formed` (vuelve al radio durante la espera) | Sin tag: ningún hito |
| Unidad sin `UnitAnimationMovementComponent` | `UnitPostureSystem` no propaga la postura a la animación |

---

## Historia

Hasta el 2026-10-19 un `FormationStanceSystem` leía estos tags para fijar `BracedShields` en Shield Wall al llegar y `Normal` al ponerse en marcha, y `UnitBodyblockSystem` leía la postura para elegir la fuerza de muro. Ambas dependencias desaparecieron: la postura sale de la tabla por papel y el muro de bodyblock se decide por `FormationType` (ver `BodyblockSystem.md`).
