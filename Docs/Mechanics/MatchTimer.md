# Match Timer

## 1. Resumen

El timer de partida es el eje de la asimetría atacante/defensor:

- Los **atacantes** deben capturar los puntos antes de que llegue a 0.
- Los **defensores** ganan si el timer expira sin que la base sea capturada.
- El timer es **corto por diseño** (tensión base) pero se extiende al capturar puntos normales.
- El **cap absoluto** es 1800 s (30 minutos) — ninguna extensión puede superarlo.
- Capturar la base termina la partida **instantáneamente**, ignorando el timer.

---

## 2. Duración base

La duración inicial del timer se configura por mapa:

| Fuente | Campo |
|--------|-------|
| Datos del mapa | `mapData.battleDuration` |
| Fallback si no definido | 900 s (15 min) [configurable — verificar en `BattleSceneController`] |

El valor se carga en `BattleSceneController` al iniciar la escena de batalla y se pasa al sistema de timer de UI.

---

## 3. Cap absoluto

El timer nunca puede superar **1800 s (30 minutos)**:

| Parámetro | Valor | Ubicación |
|-----------|-------|-----------|
| `MaxBattleDurationSeconds` | 1800 | `BattleSceneController.cs` |

Toda extensión aplica la lógica: `nuevoTimer = Min(timerActual + extension, MaxBattleDurationSeconds)`.

---

## 4. Extensión por captura

Al completarse la captura de un punto **normal**:

1. `BattleTimerExtension.System.cs` detecta el evento de captura.
2. Llama a `BattleSceneController.ExtendTimer(seconds)`.
3. `ExtendTimer` incrementa el timer respetando el cap: si el resultado supera `MaxBattleDurationSeconds`, el timer queda en el cap.
4. La UI actualiza el timer visible inmediatamente.

> El valor de extensión por captura es [configurable — verificar en `Battle.Data.cs` o en `BattleTimerExtension.System.cs`].
> El GDD menciona como ejemplo una extensión de ~5 minutos (300 s) por captura, pero no lo establece como valor fijo.

Capturar la **base** no extiende el timer — termina la partida directamente (ver sección 6).

---

## 5. Victoria por timer (tiempo agotado)

Cuando el timer llega a 0:

1. `BattleSceneController.OnBattleTimerExpired()` se dispara.
2. Se escribe en `MatchStateComponent`: `winnerTeam = 1` (defensores), `victoryConditionMet = true`.
3. `MatchController.System.cs` detecta el flag y emite `MatchState.EndMatch`.
4. La escena transiciona a `PostBattleScene` con resultado: **defensores ganan**.

El timer no puede llegar a negativo — se detiene exactamente en 0.

---

## 6. Victoria instantánea (captura de base)

Si el atacante captura el punto de base (independientemente del tiempo restante):

1. `CaptureZoneTrigger.System.cs` detecta captura completa de punto tipo `Base`.
2. Se escribe en `MatchStateComponent`: `winnerTeam = 0` (atacantes), `victoryConditionMet = true`.
3. `MatchController.System.cs` detecta el flag en el mismo frame y emite `MatchState.EndMatch`.
4. La escena transiciona a `PostBattleScene` con resultado: **atacantes ganan**.

La victoria por base tiene prioridad sobre cualquier otro evento del mismo frame.

---

## 7. Edge cases

| Caso | Resolución |
|------|-----------|
| Captura completa ocurre exactamente cuando timer llega a 0 | Victoria del atacante tiene prioridad — `victoryConditionMet` se evalúa antes del timeout [verificar orden en `MatchController`] |
| Timer en cap (1800 s) y se captura un punto normal | `ExtendTimer` se llama pero el timer permanece en 1800 s; no hay overflow ni error |
| Partida inicia con `battleDuration` no configurado en el mapa | Fallback a 900 s (15 min) |
| Extensión parcial: quedan 200 s y la extensión es 300 s | Timer queda en el cap (1800 s), no en 500 s — la extensión no puede superar el cap |
| Ambos equipos tienen héroes en base simultáneamente | La barra de captura se pausa (defensor presente) — no hay victoria hasta que salga el defensor |

---

## 8. Infraestructura existente

| Archivo | Rol |
|---------|-----|
| `Assets/Scripts/UI/Battle/BattleSceneController.cs` | `ExtendTimer(seconds)`, `OnBattleTimerExpired()`, `MaxBattleDurationSeconds`; controla el timer de UI |
| `Assets/Scripts/UI/BattlePreparation/TimerController.cs` | Muestra el timer de cuenta regresiva pre-batalla (3 min de preparación) |
| `Assets/Scripts/Map/BattleTimerExtension.System.cs` | Sistema ECS que escucha capturas y llama a `ExtendTimer` |
| `Assets/Scripts/Shared/MatchState.Component.cs` | `victoryConditionMet`, `winnerTeam`, `remainingTime` |
| `Assets/Scripts/Shared/MatchController.System.cs` | Evalúa condiciones de victoria/derrota cada frame |
| `Assets/Scripts/Data/Battle/Battle.Data.cs` | `battleDuration`, valores de extensión por captura |

---

## 9. Technical Reference

### Componentes ECS

| Archivo | Rol |
|---------|-----|
| `Assets/Scripts/Shared/MatchState.Component.cs` | Estado global: `remainingTime`, `victoryConditionMet`, `winnerTeam` |

### Sistemas ECS

| Archivo | Rol |
|---------|-----|
| `Assets/Scripts/Map/BattleTimerExtension.System.cs` | Detecta evento de captura normal y llama a `ExtendTimer` vía interop con MonoBehaviour |
| `Assets/Scripts/Shared/MatchController.System.cs` | Tick del timer; detecta expiración y condición de victoria; emite `EndMatch` |

### MonoBehaviours / UI

| Archivo | Rol |
|---------|-----|
| `Assets/Scripts/UI/Battle/BattleSceneController.cs` | `ExtendTimer(float seconds)` — extiende con cap; `OnBattleTimerExpired()` — callback al llegar a 0; `MaxBattleDurationSeconds` — constante del cap |
| `Assets/Scripts/UI/BattlePreparation/TimerController.cs` | Timer de cuenta regresiva de la fase de preparación (independiente del timer de batalla) |
| `Assets/Scripts/Data/Battle/Battle.Data.cs` | `battleDuration`: duración base del mapa; valores de extensión por captura |
