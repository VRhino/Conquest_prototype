# Plan: lote 2 de movimiento (C5, C6, C7, C9, C14)

Estado: **solo planificación, sin implementar**. Fecha: 2026-10-20. Rama prevista: `feat/movement-batch2`, a partir de `feat/movement-batch1`.

Origen:
- `Docs/Design/Movimiento_Formaciones.md`: §2 (L-3, L-6, L-7, L-8), §3, §4 (C5-C9, C14), §7 (decisiones D1-D11) y §9 (resultado del lote 1).
- Lote 1 archivado en `tasks/movement-batch1-plan.md`.

## Objetivo
Que la tropa se pegue y se separe como en la referencia:
- **Contacto:** paran a una distancia derivada de los cuerpos y del alcance del arma, sin meterse dentro del enemigo (C5).
- **Combate:** no persiguen más allá de la correa cuando reaccionan a un enemigo (C6), y giran hacia él sin hacerlo en un solo frame (C7).
- **Marcha:** recuperan su slot en marcha, con marcha de carrera explícita (C9).
- **Aliados:** dejan de atravesarse, gracias a la evitación recíproca ORCA (C14, decisión D1).

## Decisiones que gobiernan el lote (de §7)
- **D1, aliados:** se esquivan por velocidad (ORCA), sin empuje. Responsabilidad 0,5 entre soldados y 1,0 del soldado frente al héroe y los obstáculos. Vecinos a ≤ 4 m. Radio combinado de 1 m.
- **D2, enemigos:** no se esquivan entre sí; los separa la distancia de parada (C5). El interruptor de colisión por escuadra (C16) queda para el lote 3.
- **D4, héroe y tropa:** el tope de recuperación es la velocidad de la marcha; si el soldado se queda lejos, pasa a marcha de carrera. El héroe no se frena.
- **D8, escala:** 900 agentes es un objetivo real. Los sistemas nuevos no deben sumar más de 2 ms con 900 agentes.
- **D10, cuerpo:** radio de 0,5 m (`rc`) para evitación y contacto.

## Fuera de este lote
- C8, la separación posicional; solo se hace si ORCA se retrasa.
- C10-C13, C15 y C16: lote 3.
- El radio por tipo de unidad (L5-5) y las monturas.

## Orden de trabajo
1. Paso 0 (métricas y escenarios nuevos, más línea base del lote 2).
2. C7, porque es pequeño y aislado.
3. C5.
4. C6.
5. C9.
6. C14.

Un commit por paso, medido contra la línea base. El cuello de botella es C14 (ORCA); el resto son cambios acotados en sistemas que ya existen.

---

## Cómo se trabaja con Unity
Igual que en el lote 1 (ver `tasks/movement-batch1-plan.md`, "Cómo se trabaja con Unity"). Cuidados aprendidos:
- `run_tests` en PlayMode responde 400 aunque la ejecución arranca. Esperar con `wait_for` a que `isPlaying` pase a true y después a false, y leer `test_status`.
- Tras cada recompilación, esperar a que `isCompiling` sea false antes del siguiente comando.
- Para leer resultados EditMode, filtrar por clase (`filter`), porque la salida completa es muy larga.
- Los escenarios `MovementBaseline` ya resuelven el foco de entrada, la orden FollowHero (tecla C) y la dirección de marcha (−z).

## Paso 0. Métricas de contacto y separación, y escenarios nuevos
- [ ] Ampliar `MovementMetricsRecorderSystem` (sigue siendo de solo lectura y solo para Editor/Development):
  - por unidad: objetivo de combate, distancia al objetivo, `IsEngagingTag`, distancia a su slot de formación, marcha actual (cuando exista C9) y velocidad del agente;
  - por frame: pares aliados solapados (`dist < 0,9 · 2rc`, dentro de la escuadra y entre escuadras aliadas) y pares enemigos solapados.
  - Los pares se calculan con una rejilla 2D, no O(n²), para que el grabador aguante 900 agentes.
- [ ] Ampliar `MovementMetricsAnalyzer` con sus tests:
  - solapes aliados y enemigos por frame (media y p95), y pares enemigos solapados más de 0,3 s;
  - distancia en contacto: media y mínimo de la distancia al objetivo mientras se ataca;
  - atacantes por objetivo (máximo y p95);
  - unidades en reacción de combate a más de la correa de su slot;
  - frames en los que el giro de combate supera la velocidad angular máxima (para C7).
- [ ] Escenario **S4, cambio de formación**: con la escuadra quieta, cambiar Línea → Cuña → Línea (F2/F1, como `BattleSceneSpawnsAndAcceptsHeroMovementAndSquadOrders`). Repetirlo marchando. Las unidades se cruzan, así que es el caso que mide ORCA entre aliados.
- [ ] Escenario **S6, choque**: marchar con W hacia el héroe remoto, unos 40 m en +z, hasta que haya reacción de combate, y mantener 15 s de melé. Mide la parada en contacto, los solapes con enemigos, la correa y el giro de combate.
- [ ] Escenario **S8, coste con 900 agentes**: benchmark EditMode explícito del solver de ORCA y del grabador, con 900 agentes sintéticos (Burst y `Stopwatch`), sin escena.
  - La escena real de 900 agentes (`BattleSceneRenderedPerformanceProfile`) falla hoy en SetUp por el paquete de VFX que falta (ver `tasks/lessons.md`): es la decisión pendiente n.º 6.
- [ ] Grabar la línea base del lote 2: S1, S3, S4 y S6 con vídeo y CSV, sobre el estado final del lote 1.
- [ ] Correr EditMode completo y PlayMode `SquadNavigationRegressionTests` como referencia en verde.

## C7. Giro de combate con velocidad angular limitada
**Qué:** hoy `UnitRotationResolutionSystem` asigna la rotación de golpe (`agent.transform.rotation = intent`), así que una unidad que entra en combate gira 180° en un frame. Con C7, la rotación de origen `Combat` avanza a `combatTurnRate · dt` como máximo (L5-4). El giro de formación sigue con su slerp actual: por D5, la rejilla gira entera y la suavidad la da el seguimiento.

- [ ] Función pura `UnitTurn.Step(current, target, maxDegreesPerSecond, dt)` en `Squads/UnitTurn.cs`, equivalente a `RotateTowards` con `quaternion`, compatible con Burst.
- [ ] `UnitRotationResolutionSystem`: si `intent.source == RotationSource.Combat`, aplicar `UnitTurn.Step` con `tuning.combatTurnRate`; el resto de fuentes, igual que ahora. Sigue siendo el único sistema que escribe la rotación (lección: un solo dueño de la rotación del root).
- [ ] Tuning nuevo: `combatTurnRate` = 450 °/s (rango del documento: 360-540).
- [ ] Tests EditMode `UnitTurnTests`:
  - nunca supera `rate · dt`;
  - llega exactamente al objetivo cuando falta menos de un paso;
  - no depende del sentido del giro.
- [ ] Comprobación en S6: 0 frames por encima de la velocidad angular máxima, y un giro de 180° tarda unos 0,4 s.

**Riesgo:** bajo. Durante unos 0,2-0,4 s la unidad puede atacar sin estar orientada del todo; `UnitAttack` no exige orientación (comprobado: solo mide distancia).

## C5. Distancia de parada derivada de los cuerpos y del alcance
**Qué:** en el código, `attackRange` es la distancia del origen del atacante al borde lejano de su hitbox (`BraceWeaponProfiles.Component.cs:31`). Hoy los melé paran a `0,75 · attackRange` desde el centro del objetivo: los lanceros a 0,75 m, con dos cuerpos de 0,5 m, es decir, dentro del enemigo. La parada se deriva así, **sin cambiar los datos de armas** (L-7 adaptado a esta semántica):

```
reach          = attackRange − rcA                (alcance del arma por delante del propio cuerpo)
golpe posible  = d ≤ attackRange + rcB            (el hitbox alcanza el cuerpo del objetivo)
d_parada       = rcA + rcB + k_stop · reach       (sin solape en reposo y dentro del golpe)
d_reanudar     = attackRange + rcB                (si se aleja de ahí, vuelve a acercarse)
```

Ejemplos con `rc` = 0,5 y `k_stop` = 0,6:
- lanceros (`attackRange` 1,0): reach 0,5, parada a 1,30 m, golpe hasta 1,5 m;
- escuderos (`attackRange` 1,5): reach 1,0, parada a 1,60 m, golpe hasta 2,0 m.

Arqueros: parada a `rangedStopFraction · range` (0,92) y reanudar a `range` (fin del temblor).

- [ ] Función pura `MeleeReach` en `Combat/MeleeReach.cs`: `StopDistance`, `ResumeDistance` e `InAttackRange(dist, attackRange, rcTarget)`.
- [ ] `UnitNavMeshSystem` (:204, `stopDist`): usar `MeleeReach.StopDistance`. Hoy la unidad para solo si `dist > stopDist`; con C5 habrá histéresis: se acerca si `dist > d_reanudar` y se queda si `dist ≤ d_reanudar`, y el destino de aproximación es el anillo a `d_parada`.
- [ ] `UnitAttackSystem` (:106) y `UnitTargetingSystem` (:157): "en rango" pasa a ser `MeleeReach.InAttackRange` (`dist ≤ attackRange + rcB`), coherente con el hitbox. Sin esto, los lanceros parados a 1,3 m nunca empezarían a atacar.
- [ ] Tuning: `bodyRadius` = 0,5 (D10); `meleeStopReachFraction` (`k_stop`) = 0,6, que sustituye a `meleeStopDistanceFactor`; `rangedStopFraction` = 0,92.
- [ ] Validación en el baker de la escuadra (`SquadDefinitionBaker`): aviso si `attackRange − bodyRadius < 0,2` (un arma que no sobresale del cuerpo).
- [ ] Validación de coherencia del tuning: `0 < k_stop < 1` y `rangedStopFraction < 1`.
- [ ] Tests:
  - EditMode `StopDistanceTests` con todas las escuadras horneadas (`Resources/Squads`): `d_parada ≥ 2rc + ε` y `d_parada < attackRange + rc`;
  - ampliar `MeleeContactRegressionTests`: el golpe conecta desde `d_parada`.
- [ ] Comprobación en S6: 0 pares enemigos solapados más de 0,3 s, y la distancia media en contacto queda cerca de `d_parada`.

**Riesgo:** medio. Cambia el equilibrio, porque se ataca desde un poco más lejos (coherente con el hitbox), y el borde del abanico de ±45° del objetivo se recalcula. Revisar en vídeo que el golpe visual conecta.

## C6. Correa activa en reacción de combate
**Qué:** hoy, con reacción de combate, `OrderResolutionSystem` (:68-75) pone la orden `Attack`, y `CanPursueTarget` (`UnitNavMesh.System.cs`) devuelve siempre true para `Attack`. Así `unitLeashDistance` (6 m) no se aplica nunca en combate. Con C6, la correa rige cuando el origen es `OrderSource.CombatReaction`. Un `Attack` ordenado por el jugador sigue sin correa.

- [ ] `UnitNavMeshSystem`: leer `SquadResolvedOrderComponent.source` de la escuadra (de solo lectura; el escritor sigue siendo `OrderResolutionSystem`).
- [ ] Añadir una sobrecarga `CanPursueTarget(order, source, slot, target, leash)`. La actual se mantiene para los tests existentes (`SquadControlRegressionTests.PursuitDependsOnOrderAndLeashNotCombatState`).
- [ ] Mantener 6 m desde el slot de la unidad. La fórmula `R + 2·d_parada` del documento mide desde el ancla, pero aquí la distancia se mide desde el slot, y 6 m ≈ 2 + 2·1,5 + margen. Se revisa si S6 muestra persecuciones cortas.
- [ ] Tests EditMode: casos nuevos de `PursuitDependsOnOrderAndLeashNotCombatState` con `source = CombatReaction` (con correa) y `source = Player` (sin correa).
- [ ] Comprobación en S6: 0 unidades en reacción de combate a más de la correa de su slot durante más de 0,5 s.

**Riesgo:** medio. Las escuadras persiguen menos en reacción; el jugador conserva la persecución con V (`Attack`).

## C9. Seguimiento con recuperación y marchas explícitas
**Qué:** hoy `navAgent.speed = baseSpeed · variación`, con un `× hurrySpeedMultiplier` (2) solo con prisa u objetivo de combate. Los rezagados no recuperan (error en marcha de 1,1-1,3 m tras el lote 1). Con C9 (L-3 con D4):

```
d      = |slot − pos| (XZ)        v_ff = |slotVelocity| (ya existe, C3)
marcha = Run  si prisa, objetivo de combate o d > runEnterDistance
         Walk si d < runExitDistance (histéresis)
v_max  = baseSpeed · variación · (Run ? runSpeedMultiplier : 1)
v      = clamp(v_ff + max(0, (d − r_in) / followRecoveryTime), followMinSpeedRatio · baseSpeed, v_max)
```

- [ ] Enum `MovementGear` (`Walk`, `Run`) en `Squads/Enums`, y componente `UnitMovementGearComponent { gear }`, cuyo único escritor es `UnitFollowFormationSystem`. Ocupa 1 byte y está pensado para la red. El ×2 actual pasa a ser `runSpeedMultiplier` (renombrando `hurrySpeedMultiplier`).
- [ ] Función pura `FormationFollowSpeed.Compute(...)` en `Squads/FormationFollowSpeed.cs`, que devuelve la velocidad y la marcha.
- [ ] `UnitFollowFormationSystem` (:96-108): aplicar la función solo a unidades que siguen formación (`Moving`). En Hold, `v_ff` = 0. En combate, marcha Run como hoy. `Waiting` no cambia (`ResetPath`).
- [ ] Tuning: `runEnterDistance` = 1,5 m, `runExitDistance` = 0,6 m, `followRecoveryTime` = 1,0 s, `followMinSpeedRatio` = 0,3.
- [ ] Coherencia: `runExitDistance < runEnterDistance` y `followRecoveryTime > 0`.
- [ ] Animación: `UnitAnimationSystem` sigue leyendo la velocidad confirmada del agente (sin cambios). Revisar en vídeo que el paso a carrera se ve en la animación (`animationRunningSpeedRatio` = 0,6).
- [ ] Tests EditMode `SlotFollowSpeedTests`:
  - `v` es monótona en `d`;
  - nunca supera `v_max` de la marcha;
  - con `d` = 0, `v = v_ff`;
  - la histéresis entre Walk y Run no oscila con ruido alrededor de los umbrales.
- [ ] Comprobación en S1 y S3: error en marcha de media ≤ 0,5 m (objetivo final 0,35·s), sin oscilaciones nuevas al llegar ni cambios Walk/Run más de una vez por arranque.

**Riesgo:** medio. Interactúa con C3 (destino adelantado) y con la animación. Si el héroe esprinta por encima de la carrera, la tropa se queda atrás, como pide D4.

## C14. Evitación recíproca ORCA entre aliados
**Qué:** hoy `UnitNavMeshSystem:139` fuerza `NoObstacleAvoidance` y `UnitBodyblockSystem:229` ignora a los aliados, así que se atraviesan. ORCA, según van den Berg, Guy, Lin y Manocha, "Reciprocal n-body collision avoidance" (2011), implementado con código propio a partir del paper: cada agente busca la velocidad más cercana a la deseada que no colisione en un horizonte τ, con un semiplano por vecino y un programa lineal 2D.

- [ ] **Sistema nuevo `UnitAvoidanceSystem`** (`Squads/Systems/UnitAvoidance.System.cs`):
  - Va después de `UnitFollowFormationSystem` (velocidad preferida y marcha ya decididas) y antes de `UnitBodyblockSystem` y `NavMeshPositionSyncSystem`.
  - Es el único escritor de la velocidad evitada.
  - Respeta la regla de `CLAUDE.md` de un sistema por responsabilidad: solo evita, no decide destinos ni velocidades de marcha.
- [ ] Datos de entrada por agente:
  - posición;
  - velocidad actual (`agent.velocity`);
  - velocidad preferida (`agent.desiredVelocity`, limitada a `agent.speed`);
  - radio (`bodyRadius`);
  - equipo y escuadra.
- [ ] Vecinos y responsabilidad:
  - rejilla espacial propia de celda `avoidanceNeighborRadius` (4 m) y como mucho `avoidanceMaxNeighbors` (8) vecinos, los más cercanos (tope por presupuesto, D8);
  - con soldados aliados, responsabilidad 0,5;
  - con el héroe local y los héroes, responsabilidad 1,0: el soldado se aparta y el héroe no entra en el solver;
  - con enemigos no hay evitación (D2).
- [ ] Horizonte: `avoidanceTimeHorizon` = 2 s en marcha y `avoidanceTimeHorizonNear` = 1 s cuando la unidad está a menos de un espaciado de su slot. Así esquiva pronto en marcha y ajusta cerca al formar; la referencia usa 5 s y 1 s.
- [ ] Solver como job Burst (`IJobParallelFor`) sobre arrays nativos persistentes, como el bodyblock (lección: memoria persistente y procesar solo el conteo vigente). Salida: velocidad nueva por agente.
- [ ] Aplicación en el puente managed: `agent.velocity = nueva` para las unidades en `Moving` o con objetivo. A las unidades `Formed` quietas se las considera vecinas (los demás las esquivan) pero no se les cambia la velocidad.
  - Lección: aplicar el movimiento antes del snapshot autoritativo NavMesh → ECS.
- [ ] Rejilla compartida: en la v1 cada sistema tiene la suya. Si S8 da más de 1 ms, extraer un `UnitSpatialGridSystem` con un solo escritor que alimente al bodyblock y a ORCA (se decide con la medida).
- [ ] Tuning: `avoidanceNeighborRadius` = 4, `avoidanceMaxNeighbors` = 8, `avoidanceTimeHorizon` = 2, `avoidanceTimeHorizonNear` = 1 y `avoidanceEnabled` (interruptor para comparar).
- [ ] Tests:
  - EditMode `OrcaSolverTests`, todos puros: dos agentes de frente se esquivan sin tocarse; un agente con responsabilidad 1 frente al héroe cede del todo; sin vecinos, la velocidad no cambia; nunca supera la velocidad máxima;
  - benchmark explícito con 900 agentes, ≤ 1 ms;
  - PlayMode: `SquadNavigationRegressionTests` en verde, y un caso nuevo de dos aliados cruzándose sin solaparse.
- [ ] Comprobación en S4 y S1: 0 pares aliados solapados en marcha estable y menos del 1 % de los pares en cambio de formación; el tiempo hasta formar no empeora más de 0,3 s.

**Riesgo:** alto. Puede frenar la llegada al slot si los vecinos formados empujan, porque el horizonte cercano es corto. También hay que verificar que `agent.velocity` asignado convive con el `Move` del bodyblock. Si no converge, el plan B es C8 (separación posicional) para el lote 2 y ORCA pasa al lote 3.

---

## Validación del lote
Tras cada C: compilación, consola y tests EditMode del paso. Al cerrar cada C: PlayMode y los escenarios afectados.
- [ ] EditMode completo y PlayMode `SquadNavigationRegressionTests`.
- [ ] S1, S3, S4 y S6 con vídeo y CSV, comparados con la línea base del lote 2. Objetivos de §5.1:
  - solapes aliados 0 en marcha estable;
  - pares enemigos solapados más de 0,3 s: 0;
  - formado ≤ 2 s;
  - error en marcha (media ≤ 0,5 m en este lote);
  - unidades fuera de la correa en reacción: 0.
- [ ] Coste (D8): benchmark de 900 agentes y ms de `UnitFollowFormation`, `UnitAvoidance` y `UnitNavMesh` en S6; ≤ 2 ms sumados.

## Documentación al cerrar el lote
- [ ] `Docs/Design/Movimiento_Formaciones.md`: §10 con los resultados del lote 2.
- [ ] `Docs/Mechanics/BodyblockSystem.md`: relación con ORCA (aliados por velocidad, enemigos por bodyblock y parada).
- [ ] `Docs/Mechanics/UnitEngageSystem.md`: parada derivada, "en rango" con el radio del objetivo y correa en reacción.
- [ ] `Docs/Mechanics/DamageMechanics.md`: solo si cambia algo del contacto (sin cambios de fórmula previstos).
- [ ] `tasks/lessons.md` con lo aprendido.

## Decisiones antes de implementar (valores por defecto propuestos)
1. `k_stop` = 0,6. Con él, los lanceros paran a 1,30 m y los escuderos a 1,60 m.
2. Cambiar "en rango" a `attackRange + radio del objetivo`, coherente con el hitbox. Se ataca un poco más lejos que hoy.
3. Que los lanceros solo sobresalgan 0,5 m de su cuerpo (`attackRange` 1,0) es un dato de diseño. ¿Se deja así o se sube su alcance? No forma parte de este lote.
4. Giro de combate a 450 °/s.
5. Marcha de carrera por encima de 1,5 m del slot y vuelta a paso por debajo de 0,6 m. Tiempo de recuperación de 1 s.
6. ORCA con un tope de 8 vecinos (la referencia no limita), horizonte de 2 s en marcha y 1 s cerca del slot.
7. Escena real de 900 agentes: o importar el paquete "Travis Game Assets" (lo haces tú), o extender a los tests de rendimiento la excepción de logs de los escenarios de medición.

---

## Hecho anteriormente
- [x] Lote 1 de movimiento (C1-C4): plan en `tasks/movement-batch1-plan.md` y resultados en `Docs/Design/Movimiento_Formaciones.md` §9.
- [x] Comparación por sistema con la referencia y el informe `Docs/Design/Aprendizajes_Referencia.md`.
- [x] Estudio de movimiento y formaciones `Docs/Design/Movimiento_Formaciones.md`, con las decisiones del usuario en §7.
