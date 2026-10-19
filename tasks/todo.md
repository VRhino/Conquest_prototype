# Plan: lote 1 de movimiento (C1-C4)

Estado: **solo planificación, sin implementar**. Fecha: 2026-10-19.
Origen: `Docs/Design/Movimiento_Formaciones.md`: §2 (L-3 y L-4), §3 (parámetros), §4 (C1-C4) y §7 (decisiones D1-D11).

## Objetivo
Eliminar tres problemas visibles sin cambiar la arquitectura:
- el arranque en acordeón;
- el parpadeo al llegar al slot;
- el avance a tirones al seguir al héroe.

Antes hay que sacar las constantes de movimiento a un único asset de ajuste. Cada cambio cabe en un commit y se mide por separado.

## Fuera de este lote
- Distancia de parada derivada y correa en combate (C5-C6).
- Giro limitado (C7).
- Ley de seguimiento con marcha de carrera (C9).
- ORCA (C14).
- Lote 3 entero.

Las constantes de combate solo **se mueven** al asset, con su valor actual: el lote 1 no cambia el equilibrio del combate.

## Orden de trabajo
C4 primero, porque los demás leen sus parámetros. Después C1, C2 y C3, un commit por cambio, en ese orden.

---

## Cómo se trabaja con Unity
El editor está abierto y conectado por MCP: proyecto `Conquest_prototype`, Unity 6000.5.8f1, 177 tests listados. Dudas de uso: sesión "Integración Unity con Claude Desktop".

| Necesidad | Herramienta | Cuidado |
|---|---|---|
| Estado del editor y compilación | `editor_status`, `recompile_status`, `console` | Tras cada cambio de código: esperar a que termine de compilar y revisar errores de consola antes de seguir. |
| Tests EditMode | `run_tests` con `mode=editor` y `filter` por clase | — |
| Tests PlayMode | `run_tests` con `mode=playmode`, `async_tests=true` y luego `test_status` | Antes, `save_all`; el editor no debe estar en Play Mode; no lanzar otros comandos mientras corren. Los `[Explicit]` (p. ej. `BattleSceneRenderedPerformanceProfile`) solo con `include_explicit=true`. |
| Encontrar y editar la configuración en escena | `open_scene`, `find_gameobjects` (`type=SquadSpawnConfigAuthoring`), `get_serialized_fields`, `set_serialized_field`, `save_scene` | Hacerlo en cada escena con el authoring: producción y `BattleTestScene`. |
| Crear el asset de ajuste | `create_asset` (tras compilar la clase) | — |
| Fotos de control | `capture_game_view` con `save_path` | Unos pocos fps como mucho: sirve para fotos, no para vídeo. |
| Vídeo | Unity Recorder (`com.unity.recorder`, aprobado), lanzado por un script de Editor | Instalar con `package_add` en el Paso 0; la salida va fuera de `Assets/` y fuera de git. |
| Instantánea del ECS en Play Mode | `eval` (C# de un solo uso, timeout de 5 s) | No sirve para muestrear cada frame. |
| Métricas por frame y marcadores del Profiler | Grabador de solo Editor (Paso 0) con `ProfilerRecorder` | No hay herramienta MCP para el Profiler; `get_performance_stats` solo da render, memoria y tiempo de frame. |

## Paso 0. Escena de pruebas, grabador y línea base
- [ ] Actualizar `BattleTestScene` con los valores de la escena de producción (decisión D11). Hoy usa `followForwardOffset` 3 y retardos de 0,3-0,6.
  - Localizar las escenas con `find_gameobjects` (`type=SquadSpawnConfigAuthoring`), abriendo cada escena candidata, y comparar con `get_serialized_fields`.
  - Lección: una escena de prueba con configuración completa puede ocultar dependencias que faltan en producción. Hay que comprobar los singletons en ambas.
- [ ] Crear `MovementMetricsRecorder` en `Assets/Scripts/Debug/`, compilado solo con `UNITY_EDITOR || DEVELOPMENT_BUILD`.
  - Solo lee: ni escribe componentes ni contiene lógica de juego.
  - Cada frame vuelca a CSV (`Logs/MovementMetrics/<escena>_<fecha>.csv`): por unidad, el estado de formación, la distancia al slot y el error al destino.
  - Por escuadra: el número de transiciones de estado y si el ancla se mueve.
  - Lee `Conquest.NavMeshProjection` y el tiempo de `UnitFormationState` y `UnitNavMesh` con `ProfilerRecorder`.
  - Añadir a `UnitNavMeshSystem` un contador de `SetDestination` de solo depuración, siguiendo el patrón de los contadores de proyección que ya existen (:33-68).
- [ ] Instalar Unity Recorder (aprobado): `package_add` con `com.unity.recorder` y una versión fijada compatible con Unity 6000.5.
  - Después, `package_status`, compilación y consola limpias.
  - Commit propio que solo toca `Packages/manifest.json` y `packages-lock.json`.
- [ ] Crear el script de Editor `MovementBaselineCapture` en `Assets/Scripts/Editor/Optimization/`.
  - Configura Recorder por código (`RecorderControllerSettings` + `MovieRecorderSettings`): MP4, 30 fps, resolución fija, cámara fija y duración fija por escena.
  - Arranca a la vez el `MovementMetricsRecorder`, para que vídeo y CSV compartan el mismo intervalo.
  - Salida en `Recordings/MovementBaseline/<escena>_<fecha>.mp4`, fuera de `Assets/`. Añadir `Recordings/` a `.gitignore`.
- [ ] Grabar la línea base en `BattleTestScene` con el grabador y el vídeo activos:
  - S1 (marcha recta de 20 s);
  - S3 (10 ciclos de parar, marchar y parar).

  Hacer también fotos de control con `capture_game_view` en momentos fijos: al arrancar, a mitad de la marcha y 1 s tras parar.
- [ ] Sacar del CSV las métricas de partida:
  - oscilaciones de estado en los 3 s posteriores a la llegada;
  - tiempo hasta quedar formado (95 % dentro de `r_in`);
  - diferencia entre la primera y la última unidad en arrancar;
  - `SetDestination` por unidad y por segundo;
  - ms de `UnitFormationState` y `UnitNavMesh`.
- [ ] Correr la batería actual para tener la referencia en verde: `run_tests` EditMode completo y PlayMode `SquadNavigationRegressionTests` y `BattleSceneIntegrationTests`, más `NineHundredBodyblockAgentsReuseNativeStorageAfterWarmup` como caso de escala (D8).

## C4. Asset de ajuste de movimiento (`MovementTuningConfig`)
**Qué:** un ScriptableObject con los parámetros de movimiento, horneado como singleton ECS, con una sola fuente de verdad.

- [ ] Crear el ScriptableObject `MovementTuningConfig` en `Assets/Scripts/Squads/MovementTuningConfig.cs` y su asset por defecto en `Assets/ScriptableObjects/Movement/DefaultMovementTuning.asset`.
- [ ] Crear `MovementTuningComponent` (IComponentData singleton) con los campos de la tabla de abajo.
- [ ] Hornearlo desde `SquadSpawnConfigAuthoring`:
  - nuevo campo `movementTuning` y `DependsOn(asset)` en el baker, en la misma entidad que `SquadSpawnConfigComponent`;
  - si falta el asset: hornear los valores por defecto y emitir un aviso de horneado. Lección: validar los singletons requeridos antes de instanciar visuales.
- [ ] Validación en `OnValidate`/baker, con aviso y sin error. Las reglas de coherencia de §3 que aplican a este lote:
  - `rIn < rOut`;
  - `rOut ≤ s/2` y `rIn < s/4`, con `s` = tamaño de celda de `FormationGridSystem` (1,0 hoy);
  - `leadTime ≥ 0`.

  Si D7 lleva el espaciado al SO de formación, la regla pasará a usar el `s` de cada formación.
- [ ] Hacer que lean el singleton (`RequireForUpdate<MovementTuningComponent>`):
  - `UnitFormationStateSystem`;
  - `UnitNavMeshSystem` (`StopDistanceFactor` :71, `EngagementRange` :74 y el umbral de llegada :96);
  - `UnitFollowFormationSystem` (`rotSpeed = 5f` :160 y `*= 2f` :107);
  - `FormationSystem` (`formationChangeCooldown = 1f` :136).
- [ ] Quitar de `SquadSpawnConfigAuthoring/Component` los campos que pasan al asset: `slotArrivalThreshold`, `holdReformThreshold`, `unitMoveDelayMin/Max` y `unitFollowDelayMin/Max`.
  - Lección: no dejar campos de solo escritura.
  - `navMeshDestinationChangeThreshold` **se queda**: también lo usa `HeroAIExecution.System.cs:47`.
- [ ] Actualizar los tests que construyen la configuración a mano: `Tests/EditMode/FormationGeometryRegressionTests.cs:100` y `Tests/PlayMode/SquadNavigationRegressionTests.cs:230`. Ambos deben crear también el singleton nuevo.
- [ ] Asignar el asset en todas las escenas que tengan `SquadSpawnConfigAuthoring` (producción y `BattleTestScene`).
- [ ] Test EditMode `MovementTuningCoherenceTests`: el asset por defecto cumple las reglas de arriba.

Campos del lote 1 (los nuevos, en negrita; los demás conservan su valor actual):

| Campo | Valor inicial | Sustituye a |
|---|---|---|
| `arrivalRadiusIn` (r_in) | 0,2 m | `slotArrivalThreshold` |
| **`arrivalRadiusOut`** (r_out) | 0,5 m | (no existe; hoy es el mismo 0,2) |
| **`arrivalCalmTime`** | 0,15 s | — |
| `holdReformRadius` | 1,0 m | `holdReformThreshold` |
| **`startRowDelay`** | 0,08 s | `unitFollowDelayMin/Max` y `unitMoveDelayMin/Max` |
| **`startJitterDelay`** | 0,10 s | ídem |
| `destinationChangeMin` | 0,25 m | `navMeshDestinationChangeThreshold` (solo en formación) |
| **`destinationChangeSpeedFactor`** | 0,05 s | — |
| **`destinationLeadTime`** | 0,4 s | — |
| **`slotVelocitySmoothing`** | 0,15 s | — |
| **`destinationLeadMaxDistance`** | 1,0 m | — |
| `meleeStopDistanceFactor` | 0,75 | `StopDistanceFactor` (constante) |
| `combatFacingRange` | 3,5 m | `EngagementRange` (constante) |
| `formedRotationSpeed` | 5 | `rotSpeed = 5f` |
| `hurrySpeedMultiplier` | 2 | `*= 2f` |
| `formationChangeCooldown` | 1 s | `= 1f` |

**Riesgo:** bajo; no cambia el comportamiento. **Comprobación:** los tests EditMode y PlayMode existentes pasan sin cambiar sus expectativas, y la escena se ve igual que en la línea base.

## C1. Histéresis de llegada
**Qué:** dos radios en lugar de uno. Se entra en Formado por debajo de `r_in`, con el ancla parada durante `arrivalCalmTime`, y se sale por encima de `r_out` (L-4).

- [ ] Extraer la lógica de transición a una función pura compatible con Burst, en un fichero nuevo `Squads/FormationArrival.cs`. Debe recibir: estado, distancia², `rIn²`, `rOut²`, si el ancla se mueve, el temporizador de calma y dt. Así se puede probar sin escena.
- [ ] Añadir `CalmTimer` a `UnitFormationStateComponent`. Acumula mientras la unidad está dentro de `r_in` y `SquadAnchorMovingTag` está desactivado; se pone a 0 en cualquier otro caso.
- [ ] En `UnitFormationStateSystem`, rama normal:
  - **de Formado a En espera** solo si `d > r_out` (hoy :120-122);
  - **de En movimiento o En espera a Formado** si `d < r_in` y `CalmTimer ≥ arrivalCalmTime` (hoy :140, :149).

  La regla actual de `heroMovingForSquad` (:149-162) se conserva: con el ancla en marcha, la unidad sigue En movimiento.
- [ ] Rama Hold: se mantiene `holdReformRadius` como radio de salida y `r_in` como radio de entrada; no cambia de comportamiento.
- [ ] `UnitArrivedAtSlotTag` debe seguir disparándose una sola vez al entrar en Formado, para no romper los hitos de `FormationMilestoneSystem.md`.
- [ ] `UnitNavMeshSystem` sigue usando `r_in` para `commandStillValid` y para detectar rutas fallidas (:96, :275, :335).
- [ ] Tests EditMode en `ArrivalHysteresisTests`:
  - con ruido de ±`r_in` alrededor de cada radio, como máximo una transición;
  - entra en Formado solo con el ancla parada `arrivalCalmTime`;
  - sale solo por encima de `r_out`.

**Riesgo:** bajo. Una unidad formada puede quedar hasta 0,5 m fuera de su slot sin recolocarse; se acepta porque lo absorbe la rejilla de 1 m. **Comprobación (S3):** como máximo 1 oscilación por unidad en los 3 s posteriores a la llegada.

## C2. Arranque determinista por filas
**Qué:** sustituir el `Random.Range` de 0,5-1,5 s por un retardo que depende de la fila más una pequeña variación por identidad (L-4). La fila delantera sale primero y desaparece el acordeón.

- [ ] Crear la función pura `FormationStartDelay.Compute(rowRank, slotIndex, gridPosition, rowDelay, jitterDelay)` en `Squads/FormationStartDelay.cs`:
  - `t = rowRank · rowDelay + hash01(slotIndex, gridPosition) · jitterDelay`;
  - el hash usa `math.hash`, sin `UnityEngine.Random`. Así es reproducible y queda listo para red (T4).
- [ ] Calcular `rowRank` a partir de `UnitGridSlotComponent.gridPosition.y`, la profundidad (`GridToRelativeWorld`: y → z):
  - en una primera pasada por las unidades de la escuadra, dentro del mismo bucle de `UnitFormationStateSystem`, se saca `maxY`;
  - `rowRank = maxY − y`.
  - **Antes, confirmar el signo** (que la fila delantera sea la de mayor y) con `FormationGeometryRegressionTests`.
- [ ] Sustituir los dos `Random.Range` de `UnitFormationStateSystem` (:82 Hold y :126 normal) por `FormationStartDelay.Compute`. Ya no queda ningún uso de `UnityEngine.Random` en el sistema.
- [ ] Mantener la semántica de En espera: en `UnitNavMeshSystem:249-254` la unidad espera quieta, y eso es lo que produce la onda de arranque.
- [ ] Perfil por formación (D3): en este lote hay un solo perfil. Dejar anotado en el asset que la formación Dispersa usará `jitterDelay` mayor cuando llegue el perfil por formación.
- [ ] Tests EditMode en `StartDelayTests`:
  - mismas entradas dan el mismo retardo;
  - el retardo de la fila delantera es menor o igual que el de cualquier fila trasera;
  - el máximo, con 4 filas, es como mucho `4·0,08 + 0,1 ≈ 0,42 s`.

**Riesgo:** bajo; se pierde algo de aspecto orgánico, que compensa la variación por identidad. **Comprobación (S3):** la fila delantera arranca primero y el último en salir lo hace en menos de 0,5 s.

## C3. Destino con predicción y umbral dinámico
**Qué:** mandar al agente a un punto adelantado `leadTime` según la velocidad del slot, para que no frene contra un destino que se mueve a saltos (L-3).

- [ ] Añadir a `NavAgentComponent`: `prevRequestedSlot`, `slotVelocity` y `hasSlotVelocity`. Solo los escribe `UnitNavMeshSystem`, que ya es el único que llama a `SetDestination`.
- [ ] En `UnitNavMeshSystem`, antes de `ResolveFormationDestination` (:125):
  - `slotVelocity` = media móvil exponencial de `(requested − prev)/dt`, solo en XZ, con constante `slotVelocitySmoothing`;
  - si el slot salta más de `destinationLeadMaxDistance · 2` en un frame (cambio de formación o teletransporte), se reinicia a 0.
- [ ] Crear la función pura `FormationDestinationPrediction.Predict(requested, slotVelocity, leadTime, maxLead)` = `requested + clampLength(v · leadTime, maxLead)`. Va en `Squads/FormationDestinationPrediction.cs`.
- [ ] Umbral dinámico de cambio de destino: `max(destinationChangeMin, |slotVelocity| · destinationChangeSpeedFactor)`. Solo para destinos de formación; el héroe y el combate no cambian.
- [ ] **Separar la intención del destino efectivo.** Lección: no sobrescribir lo que pertenece a la formación para reparar la navegación.
  - `effectiveFormationDestination` sigue siendo la proyección del **slot real**: la leen `GetEffectiveFormationDestination` y la llegada de C1.
  - El punto predicho **solo** se usa como comando de `SetDestination`.
  - Si el punto predicho no cae en la malla, el comando vuelve al slot real proyectado.
- [ ] Coste: proyectar el punto predicho solo cuando `|slotVelocity| > 0,1` y el comando cambie más del umbral; si no, se reutiliza la proyección del slot. Medir el marcador `Conquest.NavMeshProjection` contra la línea base.
- [ ] Con el ancla parada, `slotVelocity` tiende a 0 y el comportamiento es idéntico al actual.
- [ ] Tests:
  - EditMode `DestinationPredictionTests`: la predicción no supera `maxLead`, con velocidad 0 devuelve el slot, y el salto de slot reinicia la velocidad;
  - PlayMode, ampliando `SquadNavigationRegressionTests`: en marcha recta, `SetDestination` como máximo 4 veces por unidad y segundo, sin regresión en la llegada.

**Riesgo:** bajo-medio. Junto a una pared, el punto predicho puede caer fuera de la malla; lo cubre el fallback. **Comprobación (S1):** velocidad sin dientes de sierra y menos `SetDestination` por segundo que en la línea base.

---

## Validación del lote
Después de cada C, no solo al final: compilar, revisar la consola y pasar los tests EditMode. PlayMode y el grabador, al cerrar cada C.
- [ ] EditMode con `run_tests` (`mode=editor`): todos los tests nuevos y los existentes (`FormationGeometry`, `SquadControl`, `Bodyblock`, `MeleeContact`, `Architecture`).
- [ ] PlayMode con `run_tests` (`mode=playmode`, `async_tests=true`, tras `save_all`): `SquadNavigationRegressionTests` y `BattleSceneIntegrationTests`.
- [ ] `BattleTestScene`: repetir S1 y S3 con `MovementBaselineCapture`, con el mismo vídeo, el mismo grabador y las mismas fotos de control. Comparar los vídeos lado a lado y los CSV con la línea base. Objetivos:
  - como máximo 1 oscilación al llegar;
  - formado en 2 s como máximo;
  - el último en arrancar, en menos de 0,5 s;
  - menos `SetDestination` por segundo.
- [ ] Escala (D8: 900 agentes es el objetivo real): `NineHundredBodyblockAgentsReuseNativeStorageAfterWarmup` y el test explícito `BattleSceneRenderedPerformanceProfile` (`include_explicit=true`), más los ms de `UnitFormationState` y `UnitNavMesh` del grabador en el caso denso de `Docs/Arquitectura/11_*`. Sin regresión frente a la línea base.

## Documentación al cerrar el lote
- [ ] `Docs/Mechanics/FormationMilestoneSystem.md` y `Docs/Mechanics/Formations.md`: radios de llegada, arranque por filas y que los parámetros viven en `MovementTuningConfig`.
- [ ] `Docs/Design/Movimiento_Formaciones.md`: marcar C1-C4 como hechos, con las métricas antes y después.
- [ ] `CLAUDE.md`: añadir `MovementTuningConfig` en "Core Services / Shared Utilities". Aprovechar para corregir la versión de Entities (1.3.14 → 6.5.0, desfase n.º 5).
- [ ] Si hay correcciones durante la implementación, anotar el patrón en `tasks/lessons.md`.

## Decisiones antes de implementar (valores por defecto confirmados por el usuario, 2026-10-19)
- `r_out` = 0,5 m y `arrivalCalmTime` = 0,15 s.
- `destinationLeadTime` = 0,4 s, con un tope de 1 m.
- El arranque por filas también en Hold (hoy usa un aleatorio de 0,5-1,0 s).
- Quitar sin transición los campos antiguos de `SquadSpawnConfigAuthoring`. Hay que reasignar el asset en las escenas en el mismo commit.
- ~~Unity Recorder~~ **Aprobado (2026-10-19)**: se usará `com.unity.recorder` para grabar vídeo. Todavía no está instalado; se instala en el Paso 0.

---

## Hecho anteriormente
- [x] Comparación por sistema con la referencia y el informe `Docs/Design/Aprendizajes_Referencia.md`.
- [x] Estudio de movimiento y formaciones `Docs/Design/Movimiento_Formaciones.md`, con las decisiones del usuario en §7.
