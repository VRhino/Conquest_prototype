# Puntos de Captura

## 1. Resumen

Los puntos de captura son los objetivos estratégicos principales del modo Batalla. El bando atacante debe conquistarlos para ganar; el defensor debe evitar su captura hasta que el tiempo expire.

Características fundamentales:
- **Captura irreversible**: una vez capturado un punto, el bando defensor no puede recuperarlo en esa partida.
- **Desbloqueo secuencial**: los puntos están encadenados; capturar el anterior desbloquea el siguiente.
- **Base = victoria instantánea**: capturar el punto de base termina la partida inmediatamente a favor de los atacantes.
- **Extensión de timer**: capturar un punto normal añade tiempo al reloj de partida.

---

## 2. Precondiciones

Para que una zona sea capturable deben cumplirse todas las condiciones siguientes:

| Condición | Detalle |
|-----------|---------|
| Zona no bloqueada | El punto anterior en la secuencia ya fue capturado, o el punto no tiene precondición |
| Héroe atacante presente | Al menos un héroe del bando atacante dentro del radio de la zona |
| Sin héroe defensor en zona | Ningún héroe del bando propietario dentro del radio activo |
| Punto no es ya del atacante | El punto pertenece al bando defensor (captura irreversible = no se recaptura) |

Si el punto está bloqueado, no muestra barra de progreso y no responde a presencia de héroes atacantes.

---

## 3. Flujo de captura

```
┌─────────────────────────────────────────────────────────────────┐
│ 1. Héroe atacante entra en el radio de la zona                  │
│    → Zona no bloqueada y sin defensores presentes               │
│    → Se activa la barra de progreso de captura                  │
│                                                                 │
│ 2. Progreso avanza mientras el atacante permanece               │
│    → Si un defensor entra: progreso se PAUSA (no se pierde)     │
│    → Al salir el defensor: progreso continúa desde donde quedó  │
│                                                                 │
│ 3. Barra llega al 100%                                          │
│    → Captura completada                                         │
│    → Punto cambia de color (rojo → azul para equipo atacante)   │
│    → Se ejecutan consecuencias (ver sección 5 y 6)              │
└─────────────────────────────────────────────────────────────────┘
```

**Nota sobre interrupción**: si atacantes y defensores coinciden en el radio, la zona queda disputada (`isContested`) y el progreso se congela, sin perderse. Si quedan solo defensores, el progreso **retrocede** a la velocidad de captura (`captureSpeed`) hasta 0 (`CaptureZoneTriggerSystem`).

---

## 4. Estados de zona

| Estado | Descripción | Condición de entrada |
|--------|-------------|----------------------|
| **Neutral** | Pertenece al bando defensor, disponible para captura | Estado inicial de todos los puntos normales |
| **Bloqueado** | No capturable aún; su precondición no se cumplió | El punto anterior en la secuencia no ha sido capturado |
| **Desbloqueado** | Capturable; precondición cumplida | Se capturó el punto anterior |
| **En captura** | Barra activa avanzando | Héroe atacante en zona, sin defensores |
| **Pausado** | Barra detenida, progreso conservado | Atacantes y defensores dentro de la zona a la vez (con solo defensores el progreso retrocede) |
| **Capturado** | Pertenece al atacante — irreversible | Barra llegó al 100% |

Las transiciones entre estados son:
`Bloqueado → Desbloqueado → En captura ↔ Pausado → Capturado`

---

## 5. Extensión de timer

Al completarse la captura de un punto **normal** (no el de base):

1. `CaptureZoneTriggerSystem` emite `ZoneCapturedEvent`.
2. `BattleTimerExtensionSystem` suma `captureTimeExtensionSeconds` (300 s por defecto, en `MapDataSO`) al reloj de ECS
   (`BattleClockComponent`), sin pasar de `maxBattleDurationSeconds` (1800 s por defecto).
3. Si el reloj ya está en el cap, se queda en el cap. Detalle en `MatchTimer.md`.

---

## 6. Victoria instantánea

Capturar el punto de **base** termina la partida de forma inmediata:

1. La captura se completa → `CapturePointProgress` llega a 100%.
2. La zona es la final (`ZoneTriggerComponent.isFinal`): `CaptureZoneTriggerSystem` emite `FinalZoneCapturedEvent` con el equipo que captura.
3. `MatchOutcomeSystem` (único escritor del resultado) escribe `victoryConditionMet = true` y `winnerTeam = 1` (atacantes; 2 serían los defensores) y pone la fase `PostPartida`.
4. `MatchControllerSystem` (`MatchController.System.cs`) detecta el flag y emite `MatchState.EndMatch`.
5. La escena transiciona a `PostBattleScene`.

No hay animación de captura diferida: el evento se crea en el frame en que se completa la barra y se resuelve en el siguiente.

---

## 7. Edge cases

| Caso | Resolución |
|------|-----------|
| Dos héroes atacantes en la zona | El progreso avanza normalmente; múltiples atacantes no aceleran la captura — basta con que haya al menos uno |
| Héroe defensor entra mientras barra está al 99% | Con atacantes y defensores la zona queda disputada y el progreso se congela; si quedan solo defensores, **retrocede** a la velocidad de captura |
| Zona bloqueada: héroe atacante entra | No ocurre nada — la zona no responde a presencia hasta desbloquearse |
| Héroe muere durante captura | Si era el único atacante en zona y no hay nadie más, la barra se queda donde estaba; si hay otro atacante aliado, continúa |
| Timer llega a 0 cuando se completa la captura final | `MatchOutcomeSystem` da prioridad a la captura si ambos eventos coinciden; un resultado ya decidido no se sobrescribe |
| Captura de punto normal cuando el reloj está en el cap | El reloj se queda en el cap |

---

## 8. Infraestructura existente

Los siguientes archivos ya contienen implementación parcial o relacionada:

| Archivo | Rol |
|---------|-----|
| `Assets/Scripts/Map/CapturePoint.Tag.cs` | Tag ECS que identifica entidades de punto de captura |
| `Assets/Scripts/Map/CapturePointProgress.Component.cs` | Componente con progreso (0–100), velocidad, equipo que captura y flags de disputa; el punto de base se marca con `ZoneTriggerComponent.isFinal` |
| `Assets/Scripts/Map/ZoneTrigger.Component.cs` | Define el radio de detección de héroes |
| `Assets/Scripts/Map/CaptureZoneTrigger.System.cs` | Detecta héroes dentro del radio, actualiza presencia de equipos |
| `Assets/Scripts/Map/LocalHeroCaptureTracker.System.cs` | Rastrea el progreso del héroe local para mostrar en UI y post-batalla |
| `Assets/Scripts/Map/BattleTimerExtension.System.cs` | Escucha eventos de captura y llama a `ExtendTimer` |
| `Assets/Scripts/Map/CapturePointSetup.cs` | MonoBehaviour que crea la entidad ECS del punto de captura al inicio |
| `Assets/Scripts/Shared/MatchState.Component.cs` | Estado global del match: `victoryConditionMet`, `winnerTeam` |
| `Assets/Scripts/Shared/MatchController.System.cs` | Evalúa condiciones de victoria cada frame y emite `EndMatch` |
| `Assets/Scripts/UI/Zone/CapturePointUIController.cs` | Muestra barra de progreso, estado y color de zona en HUD |

### Desbloqueo en secuencia (ya implementado)

No existe un `CapturePointUnlockSystem` ni un `CaptureProgressEvent`. `CaptureZoneTriggerSystem` desbloquea la zona siguiente al completar una captura: recorre las zonas con `ZoneLinkComponent.requiredZoneId` igual a la zona capturada y pone `ZoneTriggerComponent.isLocked = false`. `CapturePointSetup` añade el `ZoneLinkComponent`. Los consumidores reciben `ZoneCapturedEvent` y `FinalZoneCapturedEvent`.

---

## 9. Technical Reference

### Componentes ECS

| Archivo | Rol |
|---------|-----|
| `Assets/Scripts/Map/CapturePoint.Tag.cs` | Tag para consultar entidades de puntos de captura |
| `Assets/Scripts/Map/CapturePointProgress.Component.cs` | `float captureProgress` (0–100), `float captureSpeed`, `int capturingTeam`, `bool isContested`, `bool isBeingCaptured` |
| `Assets/Scripts/Map/ZoneTrigger.Component.cs` | `ZoneTriggerComponent`: `zoneId`, `zoneType`, `teamOwner`, `isActive`, `radius`, `isLocked`, `isFinal`, `pointLabel` |
| `Assets/Scripts/Map/ZoneLink.Component.cs` | `requiredZoneId`: zona que debe capturarse para desbloquear esta |
| `Assets/Scripts/Shared/MatchState.Component.cs` | `bool victoryConditionMet`, `int winnerTeam` (0 / 1 atacantes / 2 defensores); el tiempo vive en `BattleClockComponent` |

### Sistemas ECS

| Archivo | Rol |
|---------|-----|
| `Assets/Scripts/Map/CaptureZoneTrigger.System.cs` | Detecta presencia de equipos en zona, pausa/reanuda progreso |
| `Assets/Scripts/Map/LocalHeroCaptureTracker.System.cs` | Acumula puntos de captura del héroe local para el post-batalla |
| `Assets/Scripts/Map/BattleTimerExtension.System.cs` | Extiende el timer al completarse una captura normal |
| `Assets/Scripts/Shared/MatchOutcome.System.cs` | Único escritor del resultado: captura final o fin del reloj |
| `Assets/Scripts/Shared/MatchController.System.cs` | Pasa a `EndMatch` cuando `victoryConditionMet` |

### UI y Setup

| Archivo | Rol |
|---------|-----|
| `Assets/Scripts/Map/CapturePointSetup.cs` | Crea entidad ECS + configura radio y tipo desde el Inspector |
| `Assets/Scripts/UI/Zone/CapturePointUIController.cs` | Barra de progreso, color de zona, estado bloqueado/desbloqueado |
| `Assets/Scripts/UI/Battle/BattleSceneController.cs` | `ExtendTimer(seconds)`, `MaxBattleDurationSeconds` |
| `Assets/Scripts/Data/Battle/Battle.Data.cs` | Valores de duración base y extensión por captura |
