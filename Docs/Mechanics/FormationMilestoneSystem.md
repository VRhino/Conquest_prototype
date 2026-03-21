# Formation Milestone System — Framework de hitos del ciclo de vida de unidades

## Propósito

Permite enganchar comportamientos a momentos específicos del ciclo de vida del movimiento de
unidades **sin modificar los sistemas core** (`UnitFormationStateSystem`,
`UnitFollowFormationSystem`, `FormationSystem`).

Sin este framework, cualquier sistema que quisiera reaccionar a "la unidad acaba de llegar a su
slot" tendría que duplicar la lógica de detección de `UnitFormationStateSystem` o acoplarse
directamente a él. Los milestone tags actúan como señales de un solo frame — un contrato público
que cualquier sistema downstream puede consumir de forma independiente.

---

## Componentes

### Tags de hito (IEnableableComponent)

| Componente              | Tipo                               | Descripción                                                     |
|-------------------------|------------------------------------|-----------------------------------------------------------------|
| `UnitStartedMovingTag`  | `IComponentData, IEnableableComponent` | Pulso 1 frame: unidad transiciona Waiting/Formed → Moving   |
| `UnitArrivedAtSlotTag`  | `IComponentData, IEnableableComponent` | Pulso 1 frame: unidad transiciona Moving → Formed           |

Archivo: `Assets/Scripts/Squads/UnitFormationMilestone.Component.cs`

### Componente de postura

| Componente                     | Tipo            | Campo    | Descripción                                           |
|--------------------------------|-----------------|----------|-------------------------------------------------------|
| `UnitFormationStanceComponent` | `IComponentData`| `stance` | Postura táctica actual (enum `UnitStance`)            |

Archivo: `Assets/Scripts/Squads/UnitFormationStance.Component.cs`

### Enum de postura

| Valor            | Int | Descripción                                                        |
|------------------|-----|--------------------------------------------------------------------|
| `Normal`         | 0   | Postura estándar de movimiento y combate                           |
| `BracedShields`  | 1   | Shield Wall: escudos bloqueados, bonus de masa, actúa como muro NavMesh |

---

## Campos añadidos a componentes existentes

### `UnitAnimationMovementComponent` (`Assets/Scripts/Squads/UnitAnimationMovement.Component.cs`)

| Campo           | Tipo         | Descripción                                                                  |
|-----------------|--------------|------------------------------------------------------------------------------|
| `CurrentStance` | `UnitStance` | Espejo de `UnitFormationStanceComponent.stance`. Leído por `UnitAnimationAdapter`. |
| `SlotRow`       | `int`        | Fila en la formación (`UnitGridSlotComponent.gridPosition.y`). 0 = fila delantera. Selecciona variante de animación. |

Escritos por `FormationStanceSystem` al detectar un milestone tag activo.

---

## Sistemas implicados

| Sistema                    | Grupo                   | UpdateAfter                   | Responsabilidad única                                                           |
|----------------------------|-------------------------|-------------------------------|---------------------------------------------------------------------------------|
| `UnitFormationStateSystem` | `SimulationSystemGroup` | `GridFormationUpdateSystem`   | Owner de transiciones de estado. Resetea tags al inicio, los activa en transición. |
| `FormationStanceSystem`    | `SimulationSystemGroup` | `UnitFormationStateSystem`    | Lee milestone tags → actualiza `UnitFormationStanceComponent` + propaga a `UnitAnimationMovementComponent`. |

Archivos:
- `Assets/Scripts/Squads/Systems/UnitFormationState.System.cs`
- `Assets/Scripts/Squads/Systems/FormationStance.System.cs`

---

## Flujo de datos

```
GridFormationUpdateSystem
        │
        ▼
UnitFormationStateSystem
  ┌─────────────────────────────────────────────────────────────┐
  │  Por cada unidad:                                           │
  │  1. Deshabilita UnitStartedMovingTag                        │
  │  2. Deshabilita UnitArrivedAtSlotTag                        │
  │  3. Evalúa transición de estado:                            │
  │     Waiting → Moving  → habilita UnitStartedMovingTag       │
  │     Moving  → Formed  → habilita UnitArrivedAtSlotTag       │
  │     Waiting → Formed  → sin tag (transición silenciosa)     │
  └─────────────────────────────────────────────────────────────┘
        │
        ▼
FormationStanceSystem
  ┌─────────────────────────────────────────────────────────────┐
  │  Por cada squad → por cada unidad:                          │
  │  • Si UnitStartedMovingTag enabled:                         │
  │      stance = Normal                                        │
  │  • Si UnitArrivedAtSlotTag enabled:                         │
  │      stance = ShieldWall ? BracedShields : Normal           │
  │  • Escribe UnitFormationStanceComponent                     │
  │  • Propaga CurrentStance + SlotRow a                        │
  │    UnitAnimationMovementComponent                           │
  └─────────────────────────────────────────────────────────────┘
        │
        ▼
UnitAnimationAdapter (MonoBehaviour, sync layer)
  Lee UnitAnimationMovementComponent.CurrentStance + SlotRow
  → Animator Controller (IsBraced, SlotRow params)
```

---

## Mecanismo de pulso (IEnableableComponent)

Los tags son `IEnableableComponent`: la entidad **siempre tiene el componente** en memoria,
pero su estado enabled/disabled cambia por frame.

Funcionamiento:

1. **Reset al inicio** — `UnitFormationStateSystem` deshabilita ambos tags para cada unidad
   **antes** de evaluar su transición. Garantiza que ningún frame "acumula" señales anteriores.
2. **Activación en transición** — Solo si la transición ocurre en ese frame, el tag
   correspondiente se habilita.
3. **Lectura downstream** — `FormationStanceSystem` corre después y comprueba con
   `SystemAPI.IsComponentEnabled<T>()`. Si ninguno está habilitado, salta la unidad (`continue`).
4. **Limpieza automática** — En el siguiente frame, el reset del paso 1 deshabilita el tag
   antes de que cualquier otro sistema pueda leerlo dos veces.

Resultado: cada tag representa exactamente **una transición**, visible por **un único frame**.

---

## Caso de uso: Shield Wall

### Activación

1. El jugador ordena `ShieldWall` (F4 / menú radial).
2. `FormationSystem` recalcula los slots de la nueva formación.
3. Las unidades Formed detectan que su slot se desplazó → transicionan a `Waiting` → `Moving`.
4. `UnitFormationStateSystem` habilita `UnitStartedMovingTag` en la transición `Waiting → Moving`.
5. `FormationStanceSystem` lee el tag → `stance = Normal` (unidades en movimiento, escudos abajo).
6. Las unidades llegan a sus nuevos slots → transición `Moving → Formed`.
7. `UnitFormationStateSystem` habilita `UnitArrivedAtSlotTag`.
8. `FormationStanceSystem` lee el tag, comprueba `formation == FormationType.ShieldWall` →
   `stance = BracedShields`.
9. Propaga `CurrentStance = BracedShields` y `SlotRow = gridPosition.y` a
   `UnitAnimationMovementComponent`.

### Efectos en cadena

| Sistema / Capa          | Efecto                                                                    |
|-------------------------|---------------------------------------------------------------------------|
| `UnitBodyblockSystem`   | Lee `UnitFormationStanceComponent`. `BracedShields` → `WallStrength = 60f` (ya configurado por formación-muro). |
| `UnitAnimationAdapter`  | Lee `CurrentStance` → activa param `IsBraced` en Animator.               |
| `UnitAnimationAdapter`  | Lee `SlotRow` → selecciona variante (escudo al frente en fila 0, escudo en alto en filas posteriores). |

---

## Cómo añadir un nuevo comportamiento al framework

1. **No modifiques** `UnitFormationStateSystem` ni `FormationStanceSystem` salvo que necesites
   un nuevo tipo de transición (nuevo milestone tag).
2. Crea un nuevo sistema `[UpdateAfter(typeof(UnitFormationStateSystem))]` dentro de
   `SimulationSystemGroup`.
3. En su `OnUpdate`, itera sobre las entidades relevantes y comprueba:
   ```csharp
   bool startedMoving = SystemAPI.IsComponentEnabled<UnitStartedMovingTag>(unit);
   bool arrived       = SystemAPI.IsComponentEnabled<UnitArrivedAtSlotTag>(unit);
   if (!startedMoving && !arrived) continue;
   ```
4. Aplica tu lógica solo en los frames en que el tag está activo.
5. **No deshabilites los tags manualmente** — `UnitFormationStateSystem` se encarga del reset.

---

## Edge cases

| Caso                                           | Comportamiento                                                                                  |
|------------------------------------------------|-------------------------------------------------------------------------------------------------|
| Squad sin `UnitStartedMovingTag` (pre-existente) | `hasMilestoneTags = false` en `UnitFormationStateSystem` → tags nunca se habilitan; `FormationStanceSystem` hace `continue` por cada unidad → sin efecto. Stance queda en valor por defecto (`Normal = 0`). |
| Transición `Waiting → Formed` (héroe regresa mientras unidad espera) | **Sin tag**: ningún milestone se emite. La unidad pasa a Formed silenciosamente. Si estaba en `BracedShields` por un arribo previo, **mantiene** la postura hasta el próximo `StartedMoving`. |
| Formación cambia mientras unidad está en `BracedShields` | La unidad sale de Formed → Waiting → Moving → emite `UnitStartedMovingTag` → `FormationStanceSystem` fuerza `stance = Normal`. Al llegar al nuevo slot, `UnitArrivedAtSlotTag` re-evalúa según la nueva formación. |
| Unidad sin `UnitFormationStanceComponent`      | `FormationStanceSystem` hace `continue` antes de leer el tag → sin efecto, sin error.          |
| Unidad sin `UnitAnimationMovementComponent`    | `FormationStanceSystem` actualiza `UnitFormationStanceComponent` pero salta la propagación de animación. |
