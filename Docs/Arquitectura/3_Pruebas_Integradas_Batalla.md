# Pruebas integradas de batalla

> **Registro histórico (2026-09-26).** Describe el estado y las decisiones de esa fecha; no se reescribe. Para el estado vigente, ver [Arquitectura actual](1_Arquitectura_Actual.md) y, para movimiento, [TroopMovementPipeline](../TroopMovementPipeline.md). Los nombres de sistemas o valores citados aquí pueden haber cambiado.

Fecha: 2026-09-26. Editor: Unity 6000.5.8f1.

Resultado verificado en el editor conectado: **16/16 tests PlayMode aprobados**,
incluidos los dos escenarios integrados, sin omitidos (11,29 s de ejecución de tests).
Recompilación previa: cero errores. La suite EditMode no se repitió en esta pasada.

`Assets/Tests/PlayMode/BattleSceneIntegrationTests.cs` carga la escena real
`Assets/Scenes/BattleTestScene.unity` desde Unity Test Runner, en PlayMode.

## Datos y aislamiento

El escenario usa el primer héroe de `Resources/Debug/TestHeroPool.json`, un squad
`sqd01` por equipo y un ticket `BattleData` preparado antes de cargar la escena.
No necesita `player_save.json`: sustituye los proveedores de `SaveSystem` y
`LoadSystem` por un proveedor en memoria durante toda la prueba y su limpieza.
Restaura después los proveedores originales, la sesión previa, el generador
aleatorio y `Application.runInBackground`. Los dispositivos de entrada son
temporales y se retiran al terminar.

Cada escenario tiene su propio mundo ECS. La limpieza debe detener su actualización,
descargar la escena (permitiendo que HUD/cámara liberen sus referencias), y sólo
después destruir el mundo. Descargar la SubScene manteniendo squads activos deja
referencias a blobs de formación liberados; destruir primero el mundo deja a los
MonoBehaviours con un EntityManager ya destruido. El mundo previo se restaura al final.

## Cobertura

- Spawn del héroe local, su visual y dos squads.
- Agentes locales activos sobre el NavMesh de la escena.
- Tecla W: desplazamiento real del héroe superior a 3 metros y estado del motor publicado.
- Teclas V/C: intención Attack/FollowHero y resolución de FollowHero.
- Desplazamiento físico del líder superior a 0,5 metros.
- Tecla F2: aplicación de la segunda formación disponible.
- Integridad de miembros y posiciones finitas.
- Daño letal controlado: eliminación de una unidad y reducción del buffer del squad.
- Muerte y reaparición local: revisión de pose nueva, vida restaurada, marcador de
  muerte eliminado y posición confirmada próxima al punto de reaparición.

El daño se introduce mediante `PendingDamageEvent`: estos escenarios no prueban
el contacto de hitboxes, el vuelo de proyectiles ni una batalla completa entre IA.
Tampoco sustituyen la revisión manual de animaciones, cámara, obstáculos y escalones,
la compilación de Player ni las mediciones de rendimiento.

## Ejecución

En Unity: abrir Test Runner, seleccionar PlayMode y ejecutar
`BattleSceneIntegrationTests`. El runner gestiona entrada/salida de PlayMode y
restauración de la escena del editor. Guardar los cambios de escena antes de ejecutarlo.

Con la CLI de Unity y el editor conectado:

```powershell
unity command run_tests --mode playmode --filter BattleSceneIntegrationTests --async_tests true --json
unity command test_status --json
```

El informe de la integración se escribe en `Temp/pipeline_test_status.json` y se
sobrescribe en la siguiente ejecución. Los avisos del Animator requieren revisión
independiente aunque las aserciones de movimiento y ciclo de vida pasen.

En esta ejecución se observó un parámetro ausente al escribir
`AnimationHashes.IsAttacking` desde `EntityVisualSync` y `RemoteHeroAnimationDriver`.
Queda por revisar el contrato de parámetros del Animator de los prefabs utilizados.
