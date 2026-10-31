# Configuración vigente de prefabs ECS y visuales

## Visuales ligeros de batalla — 2026-10-30

La configuración activa `Assets/ScriptableObjects/VisualPrefabConfig.asset` conserva `visualPrefab` para autoría y previsualizaciones y declara `battleVisualPrefab` para combate. `VisualPrefabRegistry` selecciona la alternativa cuando el sistema de spawn solicita el visual de batalla. Las configuraciones antiguas sin alternativa siguen usando su prefab original.

Las cuatro tropas actuales usan prefabs generados en `Assets/Prefabs/BattleVisuals/`. El generador elimina hojas de geometría inactiva que no estén referenciadas por componentes o bindings de animación; conserva rig, colliders y hitboxes. Los héroes usan un rig ligero y un `BattleSkinCatalog` compartido: `BattleHeroVisual` monta las piezas finales antes del vínculo de sincronización y de asignar propietarios a las hitboxes. Las mallas y materiales permanecen compartidos.

La apariencia se copia a `HeroAppearanceComponent` desde el ticket de batalla, se transfiere al héroe local y se conserva durante respawn. Equipar, desequipar e intercambiar equipo se bloquea durante `GamePhase.Combate`. El cambio de arma durante batalla todavía no está habilitado; sus objetos y hitboxes se mantienen separados para esa futura mecánica.

Para regenerar, seleccionar `VisualPrefabConfiguration` y ejecutar `Tools > Characters > Generate Selected Battle Visual Configuration`. Repetirlo tras modificar las fuentes: todavía no hay invalidación automática por hash. No editar las salidas generadas como fuente de autoría. La comparación adicional está en `AgentScripts/ValidateBattleVisuals.cs`; valida cuatro tropas y 68 apariencias de héroe del catálogo actual. La transición entre variantes de futuras unidades se mantiene fuera de esta implementación.


Actualizado: 2026-09-15. Verificado contra `3eef0e17`.

Esta guía describe el montaje actual del runtime híbrido. Los componentes de movimiento y animación se asignan según la entidad vinculada; no todos deben serializarse en todos los prefabs.

## Contrato general

```text
Entidad ECS
  ├─ definición, identidad, vida, combate e intención
  ├─ HeroVisualInstantiationSystem / SquadVisualManagementSystem
  ▼
GameObject visual
  ├─ EntityVisualSync               vínculo y pose
  ├─ LocalHeroCharacterMotor        sólo héroe IsLocalPlayer, runtime
  ├─ RemoteHeroAnimationDriver      sólo héroe remoto, runtime
  └─ Animator / CharacterController / NavMeshAgent según rol
```

`VisualPrefabRegistry` resuelve prefabs desde `VisualPrefabConfiguration`. Los sistemas de gestión visual instancian el GameObject, añaden u obtienen `EntityVisualSync` mediante `VisualSyncUtility` y vinculan la entidad. No se debe colocar una segunda copia manual del visual para una entidad que ya participa en ese flujo.

## Héroe local

El prefab visual local necesita:

- `Animator` con su Runtime Animator Controller.
- `CharacterController` configurado para cápsula, escalones, pendientes y colisiones.
- `SamplePlayerAnimationController_ECS`.
- `EcsAnimationInputAdapter`.
- colliders/hitbox y renderers requeridos por presentación.

Al vincular una entidad con `IsLocalPlayer`, `EntityVisualSync` añade o reutiliza `LocalHeroCharacterMotor`. No es obligatorio serializar el motor en el prefab.

Flujo actual:

```text
HeroInputSystem
  → HeroMovementSystem
  → HeroMoveIntent
  → LocalHeroCharacterMotor
  → CharacterController.Move
  → LocalTransform + HeroMotorStateComponent
  → EcsAnimationInputAdapter
  → SamplePlayerAnimationController_ECS
  → Animator
```

El Animator usa la velocidad y el grounded confirmados de `HeroMotorStateComponent`; el input sólo conserva dirección y eventos walk/sprint. Un controller bloqueado no debe producir animación de carrera.

## Héroe remoto o IA

El visual remoto necesita:

- `Animator` compatible con los hashes definidos en `AnimationHashes`.
- `NavMeshAgent`, ya sea en el prefab o añadido por `HeroVisualInstantiationSystem`.
- la misma geometría visual/hitbox que corresponda al héroe.

Al vincular una entidad no local con `HeroMoveIntent`:

- el `CharacterController` se deshabilita;
- cualquier `LocalHeroCharacterMotor` libera autoridad;
- `RemoteHeroAnimationDriver` se añade o vincula;
- `EcsAnimationInputAdapter` y `SamplePlayerAnimationController_ECS` se deshabilitan para impedir escritores competidores.

Flujo actual:

```text
HeroAIExecutionSystem
  → NavMeshAgent.SetDestination
  → movimiento físico NavMesh
  ├─ NavMeshPositionSyncSystem → LocalTransform
  └─ RemoteHeroAnimationDriver → Animator
```

El driver remoto puede leer `HeroAIDecision`, `HeroAnimationComponent` y `HeroCombatComponent`, pero nunca escribe transform, destino o intención.

## Unidades de squad

Los prefabs visuales de unidad se registran por `SquadType` en `VisualPrefabConfiguration`. `SquadVisualManagementSystem` instancia y vincula cada unidad mediante `EntityVisualSync`.

Una unidad ordinaria no tiene `HeroMoveIntent`, por lo que no recibe `LocalHeroCharacterMotor` ni `RemoteHeroAnimationDriver`. Su movimiento pertenece a `UnitNavMeshSystem`; `NavMeshPositionSyncSystem` publica la pose resultante en ECS y el adaptador de animación de unidad consume su propio estado.

## Parámetros de Animator

Los nombres no deben repetirse como strings en scripts: se centralizan en `Assets/Scripts/Shared/AnimationHashes.cs`. El controller usado por héroes debe conservar, como mínimo, los parámetros que consumen los controladores activos, incluidos locomoción, grounded, look y ataque.

No elimine `IsGrounded`: el héroe local publica el contacto real del `CharacterController` y el remoto lo establece desde su contrato NavMesh.

## Registro y creación runtime

```text
VisualPrefabConfiguration
  → VisualPrefabRegistry
  → HeroVisualInstantiationSystem / SquadVisualManagementSystem
  → Instantiate(prefab)
  → VisualSyncUtility.SetupVisualSync
  → EntityVisualSync.SetHeroEntity
  → selección local/remoto/unidad
```

Requisitos:

1. `VisualPrefabRegistry` y su configuración deben estar disponibles en la escena.
2. Los IDs o tipos usados por ECS deben tener una entrada visual válida o un fallback deliberado.
3. El héroe ECS debe hornear `HeroMoveIntent`, `HeroLifeComponent`, `HeroSpawnComponent` y `HeroMotorStateComponent` para el flujo local completo.
4. Los héroes remotos deben tener `HeroAITag`, decisión IA, `HeroMoveIntent` y componentes de navegación configurados.
5. El NavMesh y los puntos de spawn deben estar horneados y activos antes de emitir destinos.

## Checklist

### Prefab ECS del héroe

- [ ] Sin renderer ni lógica visual duplicada.
- [ ] Componentes de identidad, equipo, stats, vida, salud, spawn e input horneados.
- [ ] `HeroMoveIntent` y `HeroMotorStateComponent` presentes.
- [ ] `IsLocalPlayer` sólo en la instancia local.

### Prefab visual del héroe

- [ ] `Animator` y controller compatibles con `AnimationHashes`.
- [ ] `CharacterController` correctamente dimensionado para el héroe local.
- [ ] Adaptador/controlador ECS de animación local configurados.
- [ ] Sin scripts antiguos de input que compitan con ECS.
- [ ] `NavMeshAgent` configurado en prefab o creación runtime aceptada explícitamente.

### Prefab visual de unidad

- [ ] Registrado bajo el `SquadType` correcto.
- [ ] Animator/adaptador de unidad configurado.
- [ ] Sin componentes exclusivos del héroe local o remoto.
- [ ] Escala, collider y pivote coherentes con NavMesh y formación.

## Validación vigente

Las regresiones de autoridad comprueban que:

- sólo el héroe local recibe un motor con autoridad sobre `CharacterController`;
- un héroe remoto recibe `RemoteHeroAnimationDriver` y mantiene el controller deshabilitado;
- una unidad no recibe ninguno de esos componentes;
- la animación local usa movimiento físico confirmado.

La última matriz documentada en fase 15 pasó 62/62 EditMode, 12/12 PlayMode y compiló 64 assemblies de Player Windows.

## Referencias

- [Arquitectura actual](Arquitectura/1_Arquitectura_Actual.md)
- [Movimiento del héroe y autoridad física](Arquitectura/10_Movimiento_Heroe_Autoridad_Fisica_2026-09-14.md)
- [Motor local del héroe](Arquitectura/14_Motor_Local_Heroe_2026-09-15.md)
- [Animación remota separada](Arquitectura/15_Animacion_Remota_Separada_2026-09-15.md)
- [Pipeline de movimiento de tropas](TroopMovementPipeline.md)
