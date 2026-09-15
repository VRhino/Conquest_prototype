# Limpieza de estado muerto en movimiento

Fecha: 2026-09-15. Décima fase del pipeline de control y movimiento. Trabajo posterior al commit `7ec35082`.

## Hallazgo

`UnitFollowFormationSystem` conservaba dos componentes de compatibilidad que ya no tenían lectores reales:

- `UnitPrevLeaderPosComponent` se actualizaba o añadía estructuralmente a cada unidad, pero `UnitAnimationSystem` solo construía un lookup que nunca consultaba. La animación usa `UnitAnimationMovementComponent.PreviousPosition`.
- `UnitLocalTargetComponent` podía actualizarse si existía, pero ningún authoring, spawn o sistema lo creaba o consumía.

El mismo sistema retenía además `UpdateUnitOrientation`, una segunda implementación privada de orientación que no tenía llamadas. La ruta activa publica `UnitRotationIntentComponent` y deja la aplicación final a `UnitRotationResolutionSystem`.

## Cambio

- Eliminados `UnitPrevLeaderPosComponent` y `UnitLocalTargetComponent`, incluidos sus metadatos Unity.
- Eliminado el lookup sin uso de `UnitAnimationSystem`.
- Eliminado el `EntityCommandBuffer` que solo añadía el componente muerto.
- Eliminado el método de orientación inaccesible y variables locales sin consumidores.
- Conservado el nombre `UnitFollowFormationSystem` para evitar una migración amplia de tipo; su documentación aclara su responsabilidad actual: velocidad NavMesh y orientación de formación, no destino ni movimiento físico.
- Unificado el acceso opcional a `SquadSpawnConfigComponent` en `SquadAnchorSystem`; sin singleton usa los defaults seguros de 0,1 m/s y 2 m de offset, en lugar de aceptar su ausencia y fallar después en la rama Follow.

Antes de borrar se comprobaron tanto referencias C# como los GUID de ambos scripts en escenas, prefabs y assets. No existían referencias serializadas.

## Contrato vigente

```text
GridFormationUpdateSystem
  → UnitFormationStateSystem
  → UnitNavMeshSystem              selecciona destino y llama SetDestination
  → UnitFollowFormationSystem      configura velocidad y orientación de formación
  → UnitBodyblockSystem            aplica corrección física
  → NavMeshPositionSyncSystem      publica posición GameObject/NavMesh en ECS
  → UnitRotationResolutionSystem   aplica la intención de rotación ganadora
```

`Docs/TroopMovementPipeline.md` se corrigió para retirar el antiguo movimiento ECS directo, el inexistente `HeroStateSystem` y la autoridad visual invertida.

## Validación

- **62/62 EditMode**, incluida la regresión del ancla Follow sin configuración.
- **9/9 PlayMode** con NavMesh, bodyblock disperso y escenario denso.
- **64 assemblies** de Player Windows compiladas correctamente.
- Búsqueda final sin referencias runtime a los dos componentes ni al método retirado.
