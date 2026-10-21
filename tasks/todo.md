# Plan: Fase 0 de `Docs/Design/Aprendizajes_Referencia.md` (higiene y bajo riesgo)

Estado: **implementado** (2026-10-21), rama `feat/reference-phase0` (sale de `feat/movement-batch3`).
Petición del usuario: empezar la Fase 0 (D1, L4-01/L4-02, L7-1/L7-2, L1-03/L1-05), un commit por pieza.
Referencia: solo patrones de `game-re/notes/` (ningún código ni dato de CB). Plan anterior archivado en
`tasks/formation-layout-plan.md`.

## Decisiones por defecto (dudas abiertas de §7 que afectan a esta fase)
- Los docs de `Docs/Mechanics` son documentación: si contradicen al código, se corrige el doc (D1).
- `winnerTeam` sigue siendo un entero 0/1/2 (ninguno, atacantes, defensores), con constantes con nombre.
- Si en el mismo frame termina el reloj y se captura la zona final, gana la captura.
- Con solo defensores en la zona, el progreso sigue retrocediendo (como el código hoy).

## Pasos (un commit cada uno; tests EditMode verdes antes de cada commit)
- [x] **D1. Docs sincronizados.** `413d6ea5` `DamageMechanics.md` (resta plana por tipo con suelo del 5 %, escudo que absorbe el daño
  real hasta romperse, sin orientación), `Formations.md` (plantillas por supervivientes y reparto por cercanía),
  `CLAUDE.md` (versiones), `TDD.md` (`SquadObstacleAvoidanceSystem` no existe), lista de formaciones. Tabla
  "Estado frente al código" con fecha en cada doc tocado. `MatchTimer.md` y `CapturePoints.md` se actualizan con L7.
- [x] **L4-01. Reloj de decisión de la IA.** `c48f67d9` `HeroAIPerceptionSystem` deja `Time.frameCount % 5`: cada héroe IA piensa cada
  `heroAIThinkInterval` s de tiempo de simulación, con fase escalonada por hash de su entidad y un tope de
  `heroAIMaxThinksPerFrame` por frame (cursor que no pierde a nadie). Valores en `SquadSpawnConfig`.
- [x] **L4-02. Histéresis.** `4be05f6b` `isUnderAttack` del blackboard pasa por una puerta con tiempo de apertura y de cierre
  (`DebouncedGate`, pura y testeada): los bots no cambian de rama por un tick de proximidad.
- [x] **L7-1. Reloj de partida en ECS.** `a9e0d930` `BattleClockComponent` (restante, máximo) inicializado por el bootstrap desde los
  datos de batalla; `BattleClockSystem` lo descuenta en `InBattle` y emite `BattleTimeExpiredEvent`; la extensión por
  captura (300 s, tope 1800 s, en `MapDataSO`) la aplica `BattleTimerExtensionSystem` sobre el componente. La UI solo lee.
- [x] **L7-2. Resolutor único del resultado.** `af92ad8b` `MatchOutcomeSystem` es el único escritor de `winnerTeam` y
  `victoryConditionMet`: consume la captura final (`FinalZoneCapturedEvent`, emitido por `CaptureZoneTriggerSystem`) y el
  fin del reloj, con orden fijo. `BattleSceneController` deja de escribir ECS.
- [x] **L1-03. Daño como función pura.** `034eec4d` `DamageFormula.Compute(in DamageInput, in CombatTuning)` con desglose; las
  constantes (suelo 5 %, umbral y bonus de altura) a tuning. El texto flotante sale a `DamageFeedbackEvent` + un sistema de
  presentación aparte.
- [x] **L1-05. RNG sembrado por golpe.** `8636e23a` Crítico de melé y de distancia con `CombatRandom.ForHit(tick, atacante, sal)`
  (`Unity.Mathematics.Random`); ningún `UnityEngine.Random` en la lógica de combate.

## Verificación
EditMode completo; PlayMode `SquadNavigationRegressionTests` y `BattleSceneIntegrationTests` que no necesitan el paquete de
VFX; MovementBaseline S6 una vez (el combate sigue funcionando). Lecciones en `tasks/lessons.md`.

## Desviaciones
- Las constantes de daño viven en `CombatTuning.Default` (un solo sitio, sin asset todavía): no hay ninguna configuración de combate en las subescenas de batalla. Pasarlas a un ScriptableObject cuando haga falta ajustarlas en el editor.
- Los tiempos de la IA van en `SquadSpawnConfig` (ya está en las subescenas de batalla) porque `HeroGameplayConfigAuthoring` no está en ellas (desfase 11 de `Aprendizajes_Referencia.md` §8).
- El estado del reloj de la IA vive en `HeroAIBlackboard`: los componentes de IA se añaden desde `BattleSceneController` (violación del modelo híbrido ya existente, fuera de esta fase).
- Sigue habiendo `UnityEngine.Random` en la asignación de comportamiento de los bots (`BattleSceneController`), fuera del combate.
