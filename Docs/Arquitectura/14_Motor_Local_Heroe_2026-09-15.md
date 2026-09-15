# Motor local del héroe

Fecha: 2026-09-15. Undécima fase del pipeline de control y movimiento. Trabajo posterior al commit `e4eede48`.

## Decisión

Se conserva deliberadamente `CharacterController` como ejecutor físico del héroe local. ECS no reemplaza su resolución de suelo, escalones, pendientes y colisiones: publica la intención de gameplay y recibe después el resultado confirmado.

```text
HeroInputSystem
  → HeroMovementSystem
  → HeroMoveIntent                 intención ECS
  → LocalHeroCharacterMotor        único ejecutor físico
  → CharacterController.Move       suelo y colisiones
  → LocalTransform                 pose confirmada ECS
  → HeroMotorStateComponent        velocidad y contactos confirmados
```

## Separación aplicada

- Se extrajeron de `EntityVisualSync` gravedad, `CharacterController.Move()`, inhibición por muerte/spawn, teleports revisionados y publicación de la pose local.
- `LocalHeroCharacterMotor` es el único componente autorizado a habilitar y mover el controller local.
- `EntityVisualSync` conserva vinculación y sincronización de unidades/remotos. Para el héroe local solo asegura que el motor correcto esté vinculado; la animación remota se separó posteriormente en la fase 15.
- El motor se añade exclusivamente cuando la entidad tiene `IsLocalPlayer`; una entidad remota desactiva inmediatamente su controller.
- `HeroMotorStateComponent` forma parte del arquetipo horneado y expone `velocity`, `isGrounded`, `hitSides` y `hitCeiling`.
- `EcsAnimationInputAdapter` activa locomoción solo con velocidad horizontal confirmada. Conserva del input la dirección y los eventos walk/sprint, y propaga el grounded real al Animator.
- `DefaultExecutionOrder(-100)` garantiza que el motor publique el resultado antes de los consumidores visuales `Update` del mismo frame.
- El motor mantiene creación defensiva del estado únicamente para fixtures o flujos externos que no usen el prefab horneado.

## Teleport y ciclo de vida

Una revisión nueva de `HeroSpawnComponent.positionRevision` se consume una vez. El motor desactiva temporalmente el controller, aplica posición y rotación, lo rehabilita, reinicia velocidad vertical y publica la pose sin ejecutar desplazamiento normal ese frame. Un héroe muerto o todavía sin ubicación no consume intención de movimiento.

## Validación

- **62/62 EditMode** con `HeroMotorStateComponent` incluido en el baker.
- **3/3 PlayMode específicos**: teleport/movimiento confirmado, exclusión de autoridad remota y animación bloqueada sin desplazamiento físico.
- **11/11 PlayMode completo** después de conectar el consumidor de animación.
- **64 assemblies** de Player Windows compiladas correctamente.

## Límite

El diseño sigue siendo híbrido y depende del intercambio entre ECS y `MonoBehaviour.Update`. Esto es intencional para conservar `CharacterController`; el futuro contrato de red deberá transportar intención y estado confirmado/reconciliado, no asumir que `LocalTransform` es una integración ECS pura.

Continuación: [Animación remota separada](./15_Animacion_Remota_Separada_2026-09-15.md).
