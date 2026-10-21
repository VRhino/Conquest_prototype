# Match Timer

## 1. Resumen

El timer de partida es el eje de la asimetría atacante/defensor:

- Los **atacantes** deben capturar los puntos antes de que llegue a 0.
- Los **defensores** ganan si el timer expira sin que la base sea capturada.
- El timer es **corto por diseño** (tensión base) pero se extiende al capturar puntos normales.
- Hay un **cap** por mapa (1800 s por defecto) que ninguna extensión supera.
- Capturar la base termina la partida **instantáneamente**, ignorando el timer.

### Estado frente al código (2026-10-21)
| Apartado | Código | Estado |
|---|---|---|
| Reloj | `BattleClockComponent` + `BattleClockSystem` (ECS) | Sincronizado (L7-1 de `Docs/Design/Aprendizajes_Referencia.md`); antes era un MonoBehaviour |
| Resultado | `MatchStateComponent.winnerTeam`: 0 sin decidir, 1 atacantes, 2 defensores | Sincronizado |

---

## 2. Duración base

`BattleBootstrapSystem` crea el reloj al entrar en `InBattle` (`BattleClock.InitialSeconds`):

| Prioridad | Fuente |
|---|---|
| 1 | `BattleData.BattleTimer` (900 s por defecto en `BattleData`) |
| 2 | `MapDataSO.battleDuration` |
| 3 | `BattleClock.DefaultBattleSeconds` (900 s) |

---

## 3. Cap y extensión por captura

| Campo | Valor por defecto | Dónde |
|---|---|---|
| `captureTimeExtensionSeconds` | 300 s | `MapDataSO` (copiado a `BattleClockComponent`) |
| `maxBattleDurationSeconds` | 1800 s | `MapDataSO` (copiado a `BattleClockComponent`) |

1. `CaptureZoneTriggerSystem` emite `ZoneCapturedEvent` al completar la captura de una zona normal.
2. `BattleTimerExtensionSystem` lo consume y aplica `BattleClock.Extend`: `restante = max(restante, min(restante + extensión, cap))`.
   Con 200 s restantes y 300 s de extensión, el reloj queda en 500 s; con 1700 s, en 1800 s.
3. Si el reloj ya expiró, la captura no lo reabre.

---

## 4. Victoria por timer (tiempo agotado)

1. `BattleClockSystem` descuenta el reloj con `SystemAPI.Time.DeltaTime` solo en `MatchState.InBattle`.
2. Al llegar a 0 marca `expired` y, si nadie ha ganado aún, escribe en `MatchStateComponent`:
   `winnerTeam = 2` (defensores), `victoryConditionMet = true`.
3. `MatchControllerSystem` detecta el flag y pasa a `MatchState.EndMatch`.

## 5. Victoria instantánea (captura de base)

1. Al completarse la captura de la zona final, `CaptureZoneTriggerSystem` escribe `winnerTeam = capturingTeam`
   (1 si captura el atacante) y `victoryConditionMet = true`.
2. `MatchControllerSystem` pasa a `EndMatch` en el mismo frame.

---

## 6. Edge cases

| Caso | Resolución |
|------|-----------|
| Captura final y fin del reloj en el mismo frame | La captura corre antes (`CaptureZoneTriggerSystem` → `MatchControllerSystem`; el reloj no sobrescribe un resultado ya decidido) |
| Reloj en el cap y se captura un punto normal | Se queda en el cap |
| El HUD | `BattleSceneController` solo muestra `BattleClock.Format(restante)` (mm:ss); no cuenta ni decide |

---

## 7. Infraestructura

| Archivo | Rol |
|---------|-----|
| `Assets/Scripts/Shared/BattleClock.Component.cs` | `BattleClockComponent`: restante, cap, extensión, expirado |
| `Assets/Scripts/Shared/BattleClock.cs` | Reglas puras: duración inicial, extensión con cap, tick, formato |
| `Assets/Scripts/Shared/BattleClock.System.cs` | Descuenta el reloj y da la victoria a los defensores al expirar |
| `Assets/Scripts/Shared/BattleBootstrap.System.cs` | Crea el reloj al entrar en batalla |
| `Assets/Scripts/Map/BattleTimerExtension.System.cs` | Extiende el reloj por captura |
| `Assets/Scripts/Shared/MatchController.System.cs` | Transición a `EndMatch` cuando `victoryConditionMet` |
| `Assets/Scripts/UI/Battle/BattleSceneController.cs` | Muestra el tiempo restante |
| `Assets/Scripts/UI/BattlePreparation/TimerController.cs` | Timer de la fase de preparación (independiente del de batalla) |
