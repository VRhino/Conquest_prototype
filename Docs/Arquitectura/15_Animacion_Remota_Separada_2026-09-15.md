# Animación remota separada

Fecha: 2026-09-15. Continuación de la separación de responsabilidades iniciada con el motor local del héroe.

## Problema

`EntityVisualSync` combinaba tres tareas distintas: vincular la entidad con su GameObject, sincronizar pose y conducir directamente el `Animator` de héroes remotos. También deshabilitaba los controladores de animación local. Esto hacía que cualquier cambio de presentación remota obligara a modificar el puente de autoridad física y sincronización.

## Separación aplicada

```text
Héroe local
  HeroMoveIntent → LocalHeroCharacterMotor → CharacterController
                                      └────→ pose/estado confirmado ECS

Héroe remoto
  HeroAIExecutionSystem → NavMeshAgent → pose física
                               ├───────→ NavMeshPositionSyncSystem → ECS
                               └───────→ RemoteHeroAnimationDriver → Animator

EntityVisualSync
  entidad ↔ GameObject y selección del componente apropiado
```

- `RemoteHeroAnimationDriver` es el único dueño de los parámetros de locomoción y combate del `Animator` remoto.
- Lee velocidad real del `NavMeshAgent`; `HeroAIDecision.shouldSprint` sólo clasifica el gait cuando existe desplazamiento confirmado.
- Lee orientación de cabeza y pulso de ataque desde `HeroAnimationComponent`, y el estado sostenido desde `HeroCombatComponent`.
- Si el agente no está habilitado o aún no se encuentra sobre NavMesh, publica estado detenido en vez de conservar animación residual.
- Deshabilita `SamplePlayerAnimationController_ECS` y `EcsAnimationInputAdapter` en el héroe remoto para evitar escritores competidores.
- No escribe `Transform`, `LocalTransform`, destinos NavMesh ni componentes de intención.

## Frontera de instalación

`EntityVisualSync.ConfigureMovementAuthority()` decide por componentes, no por nombre del prefab:

- `IsLocalPlayer` instala/vincula `LocalHeroCharacterMotor` y libera cualquier driver remoto.
- Una entidad no local con `HeroMoveIntent` instala/vincula `RemoteHeroAnimationDriver` y deshabilita su `CharacterController`.
- Una unidad ordinaria sin `HeroMoveIntent` no recibe ninguno de los dos componentes.

Esta discriminación conserva el uso compartido de `EntityVisualSync` por héroes y unidades sin convertir toda representación no local en un héroe IA.

## Validación

- Regresiones específicas PlayMode: 4/4, incluyendo autoridad remota y exclusión explícita de unidades.
- Suite completa EditMode: 62/62.
- Suite completa PlayMode: 12/12.
- Compilación de scripts Player Windows: 64 assemblies.

## Pendiente deliberado

`EntityVisualSync` todavía conserva el ataque del héroe local además de la vinculación y pose. Su extracción debe coordinarse con `SamplePlayerAnimationController_ECS` para establecer un único consumidor del pulso `HeroAnimationComponent.triggerAttack`; no se mezcla con esta migración remota.
