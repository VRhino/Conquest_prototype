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

**Nota sobre interrupción**: el defensor no necesita hacer ninguna acción; basta con entrar al radio. El progreso de captura acumulado **no se pierde** al ser interrumpido — continúa exactamente desde donde quedó cuando el defensor abandona la zona.

---

## 4. Estados de zona

| Estado | Descripción | Condición de entrada |
|--------|-------------|----------------------|
| **Neutral** | Pertenece al bando defensor, disponible para captura | Estado inicial de todos los puntos normales |
| **Bloqueado** | No capturable aún; su precondición no se cumplió | El punto anterior en la secuencia no ha sido capturado |
| **Desbloqueado** | Capturable; precondición cumplida | Se capturó el punto anterior |
| **En captura** | Barra activa avanzando | Héroe atacante en zona, sin defensores |
| **Pausado** | Barra detenida, progreso conservado | Héroe defensor entró a la zona |
| **Capturado** | Pertenece al atacante — irreversible | Barra llegó al 100% |

Las transiciones entre estados son:
`Bloqueado → Desbloqueado → En captura ↔ Pausado → Capturado`

---

## 5. Extensión de timer

Al completarse la captura de un punto **normal** (no el de base):

1. El sistema llama a `BattleSceneController.ExtendTimer(seconds)`.
2. El timer de partida se incrementa, sin exceder el cap de 1800 s (30 min).
3. Si el timer ya está en el cap, la extensión no tiene efecto.

La extensión es procesada por `BattleTimerExtension.System.cs` que detecta el evento de captura y emite la llamada.

> El valor exacto de extensión por captura es [configurable — verificar en código / `Battle.Data.cs`].

---

## 6. Victoria instantánea

Capturar el punto de **base** termina la partida de forma inmediata:

1. La captura se completa → `CapturePointProgress` llega a 100%.
2. El sistema detecta que el punto es de tipo `Base`.
3. Se escribe `victoryConditionMet = true` en `MatchStateComponent` con `winnerTeam = 0` (atacantes).
4. `MatchController.System.cs` detecta el flag en el mismo frame y emite `MatchState.EndMatch`.
5. La escena transiciona a `PostBattleScene`.

No hay animación de captura diferida — la victoria ocurre en el frame en que se completa la barra.

---

## 7. Edge cases

| Caso | Resolución |
|------|-----------|
| Dos héroes atacantes en la zona | El progreso avanza normalmente; múltiples atacantes no aceleran la captura — basta con que haya al menos uno |
| Héroe defensor entra mientras barra está al 99% | Captura se pausa; el progreso se conserva; al salir el defensor continúa desde 99% |
| Zona bloqueada: héroe atacante entra | No ocurre nada — la zona no responde a presencia hasta desbloquearse |
| Héroe muere durante captura | Si era el único atacante en zona, la barra se pausa; si hay otro atacante aliado, continúa |
| Timer llega a 0 exactamente cuando se completa captura | La victoria del atacante tiene prioridad sobre el fin de tiempo [verificar orden de sistemas en `MatchController`] |
| Captura de punto normal cuando timer está en cap (30 min) | `ExtendTimer` se llama pero la extensión es ignorada — el timer permanece en el cap |

---

## 8. Infraestructura existente

Los siguientes archivos ya contienen implementación parcial o relacionada:

| Archivo | Rol |
|---------|-----|
| `Assets/Scripts/Map/CapturePoint.Tag.cs` | Tag ECS que identifica entidades de punto de captura |
| `Assets/Scripts/Map/CapturePointProgress.Component.cs` | Componente con barra de progreso, estado y tipo (Normal/Base) |
| `Assets/Scripts/Map/ZoneTrigger.Component.cs` | Define el radio de detección de héroes |
| `Assets/Scripts/Map/CaptureZoneTrigger.System.cs` | Detecta héroes dentro del radio, actualiza presencia de equipos |
| `Assets/Scripts/Map/LocalHeroCaptureTracker.System.cs` | Rastrea el progreso del héroe local para mostrar en UI y post-batalla |
| `Assets/Scripts/Map/BattleTimerExtension.System.cs` | Escucha eventos de captura y llama a `ExtendTimer` |
| `Assets/Scripts/Map/CapturePointSetup.cs` | MonoBehaviour que crea la entidad ECS del punto de captura al inicio |
| `Assets/Scripts/Shared/MatchState.Component.cs` | Estado global del match: `victoryConditionMet`, `winnerTeam` |
| `Assets/Scripts/Shared/MatchController.System.cs` | Evalúa condiciones de victoria cada frame y emite `EndMatch` |
| `Assets/Scripts/UI/Zone/CapturePointUIController.cs` | Muestra barra de progreso, estado y color de zona en HUD |

### Componentes / sistemas nuevos necesarios (estimado)

| Componente / Sistema | Propósito |
|----------------------|-----------|
| `CapturePointUnlockSystem` | Detecta captura de punto N y desbloquea punto N+1 en la secuencia |
| `CaptureProgressEvent` | Evento emitido al completar captura para comunicar a timer y victory check |

---

## 9. Technical Reference

### Componentes ECS

| Archivo | Rol |
|---------|-----|
| `Assets/Scripts/Map/CapturePoint.Tag.cs` | Tag para consultar entidades de puntos de captura |
| `Assets/Scripts/Map/CapturePointProgress.Component.cs` | `float progress` (0–1), `CapturePointType type` (Normal/Base), `bool isBlocked`, `int ownerTeam` |
| `Assets/Scripts/Map/ZoneTrigger.Component.cs` | `float radius`, usado por el sistema de detección de zona |
| `Assets/Scripts/Shared/MatchState.Component.cs` | `bool victoryConditionMet`, `int winnerTeam`, `float remainingTime` |

### Sistemas ECS

| Archivo | Rol |
|---------|-----|
| `Assets/Scripts/Map/CaptureZoneTrigger.System.cs` | Detecta presencia de equipos en zona, pausa/reanuda progreso |
| `Assets/Scripts/Map/LocalHeroCaptureTracker.System.cs` | Acumula puntos de captura del héroe local para el post-batalla |
| `Assets/Scripts/Map/BattleTimerExtension.System.cs` | Extiende el timer al completarse una captura normal |
| `Assets/Scripts/Shared/MatchController.System.cs` | Detecta `victoryConditionMet` o timer = 0 y termina la partida |

### UI y Setup

| Archivo | Rol |
|---------|-----|
| `Assets/Scripts/Map/CapturePointSetup.cs` | Crea entidad ECS + configura radio y tipo desde el Inspector |
| `Assets/Scripts/UI/Zone/CapturePointUIController.cs` | Barra de progreso, color de zona, estado bloqueado/desbloqueado |
| `Assets/Scripts/UI/Battle/BattleSceneController.cs` | `ExtendTimer(seconds)`, `MaxBattleDurationSeconds` |
| `Assets/Scripts/Data/Battle/Battle.Data.cs` | Valores de duración base y extensión por captura |
