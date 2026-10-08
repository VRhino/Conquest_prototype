# Animación y locomoción: reparación y validación

Fecha: 2026-10-08. Editor conectado: Unity 6000.5.8f1.

## Resultado

22/22 tests PlayMode y 79/79 tests de la assembly Conquest.Architecture.Tests en EditMode aprobados. La comprobación adicional de pose de ataque también pasó tras incorporarla al escenario de las tres tropas. Compilación sin errores.

La validación usa Assets/Scenes/BattleTestScene.unity, sus agentes y los prefabs/controllers reales. El mundo ECS, los dispositivos de input y la persistencia en memoria se aíslan por escenario; se restauran al terminar.

## Contrato de locomoción

- Héroe local: LocalHeroCharacterMotor publica velocidad horizontal; EcsAnimationInputAdapter expone esa velocidad y el sprint aceptado por HeroMoveIntent. SamplePlayerAnimationController_ECS escribe la animación con esos resultados. El adaptador es el único dueño del contador de duración de movimiento.
- Héroe remoto: RemoteHeroAnimationDriver usa la velocidad horizontal del NavMeshAgent y su límite de velocidad aceptado. Una petición shouldSprint sin cambio del agente no activa sprint visual.
- Unidades: UnitAnimationSystem se ejecuta después de movimiento, publicación de pose y resolución de rotación. Lee velocidad horizontal del agente; los visuales sin agente usan diferencia horizontal de posiciones. UnitAnimationMovementComponent conserva velocidad base, velocidad aceptada, velocidad actual y dirección mundial.
- UnitAnimationAdapter convierte la dirección mundial al espacio local del visual. Se eliminó el criterio basado en distancia GO/ECS y la asignación de la primera entidad como fallback.
- Los adaptadores tienen orden -80, el controlador local y el controlador de unidades -60, el driver remoto -50. El motor local mantiene -100.

## Velocidad física y Blend Trees

LocomotionAnimationSettings comparte la conversión de velocidad física a coordenadas del árbol Synty: caminar 3, correr 5, sprint 12. La velocidad base de gameplay corresponde al punto de carrera; la velocidad de sprint aceptada corresponde al punto de sprint. Los puntos intermedios interpolan según velocidad real. No se cambia la velocidad física para satisfacer al Animator.

Los puntos y umbrales se pueden ajustar en el bloque _locomotion de cada adaptador/controlador. SquadSpawnConfigAuthoring expone animationMovingSpeedThreshold y animationRunningSpeedRatio para la observación ECS. Los valores de velocidad base y multiplicador de sprint siguen perteneciendo a las estadísticas de gameplay.

Las unidades ya no consideran sprint alcanzar el 80 % de su velocidad base. Sprint requiere un límite de navegación superior a la base y velocidad real por encima del umbral configurado.

## Transiciones y autoridad

MovementInputTapped/Pressed pulsan al arrancar; Held representa movimiento mantenido. Al detenerse, los tres son falsos y MoveSpeed es cero, permitiendo la transición real ToIdle. IsWalking representa el gait de caminar.

Root motion está desactivado en ModularHeroVisual, HeroVisual_Remote y los tres prefabs de tropas; VisualSyncUtility también lo garantiza al instanciar. Los clips importados inspeccionados tenían averageSpeed cero: esta corrección protege la autoridad de movimiento, no demuestra que root motion fuese el origen del síntoma.

Los controllers de Squires y Levy Archers usan CombatUpperBody.mask en su capa Combat. La máscara excluye raíz y piernas. No se crean clips de combate que faltan en los assets.

## Cobertura verificada

BattleSceneIntegrationTests tiene seis escenarios:

1. Spawn, movimiento real, propagación de velocidad del agente al Animator, órdenes V/C y formación F2.
2. Daño, retirada de unidad muerta y respawn del héroe.
3. Carrera local con un clip Run real, sprint y agotamiento manteniendo Shift, parada y estado Idle_Standing.
4. Colisión contra una pared con W mantenida: velocidad confirmada y animación de locomoción a cero. La cámara se estabiliza para que el muro sea perpendicular a la intención de movimiento.
5. Héroe remoto en un corredor navegable despejado: carrera real, petición de sprint no aceptada, sprint aceptado y parada.
6. Rigs/controllers de Squires, Spearmen y Levy Archers: dirección local correcta al mirar al este, carrera sin sprint a velocidad base y regreso a idle. En Squires se comprueba además el estado Attack y que el peso de Combat no modifica las rotaciones locales de las piernas a igual instante de animación.

HeroVisualRespawnRegressionTests cubre adicionalmente desplazamiento vertical sin locomoción y conversión coherente para distintas velocidades base. Las regresiones de navegación, colisiones y autoridad del motor siguen pasando.

Estas pruebas no equivalen a una revisión artística completa de todos los clips ni a una compilación de Player.
