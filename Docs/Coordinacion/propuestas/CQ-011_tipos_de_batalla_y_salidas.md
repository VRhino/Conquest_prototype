# CQ-011 — Tipos de batalla, convocatoria del asedio, salidas de héroe y bandos mixtos

**Destino:** `Conquest_prototype`
**Estado:** LISTO_PARA_REVISION
**Prioridad:** Alta — cambia qué modos de partida monta el servidor de batalla y cuándo recibe el ticket
**Solicitante:** BronzeAge / Claude
**Fecha:** 2026-10-06
**Origen:** decisiones del autor del proyecto (2026-10-06), recogidas en `BronzeAgeFase0/Consideraciones/Batalla_Este_Lado_Definicion.md`.
Reglas de juego en `BronzeAgeFase0/Docs/Game/5_Sistema_Militar_y_Combate.md` §5.12.1, §5.14.4, §5.15.1b y §5.16.4. Contrato
en `BronzeAgeFase0/Docs/Coordinacion/01_Modelo_de_datos_compartido.md` §15 y `02_Contrato_Comunicacion_Servidor_Cliente.md` §3.

Absorbe a CQ-010 en lo que toca a las salidas (abandono y desconexión): ver §3.

## Principio

**Unity decide todo lo que pasa dentro de una batalla** —banderas, puntos de aparición, reaparición y su espera,
rendición, cómo se gana, cómo cambian de manos las caravanas—. **BronzeAge solo decide que la batalla ocurra, dónde,
quién entra, quién sale y qué le queda a cada uno en el mundo.** Esta propuesta no mete reglas de partida en el contrato:
solo le dice a Unity *qué clase de batalla* es, para que elija modo, y le pide los avisos que BronzeAge necesita.

## 1. Qué clase de batalla es

`contextoEstrategico` distingue:

| `tipo` | Cuándo | Observaciones |
|---|---|---|
| `asedio` | Un ejército ataca una plaza | Convocatoria de 30 s antes de publicar el ticket (§2) |
| `campo_abierto` con `columnas: 'ejercitos'` | Batalla campal: ejército contra ejército | **Nadie de fuera puede unirse** |
| `campo_abierto` con `columnas: 'solitarios'` | Persecución: héroes en solitario | Cualquiera se une, al bando que elija |
| `caravana` | Asalto a una caravana suelta | |
| `campamento_bandidos` | **Evento PvE**: todos los héroes contra la IA del campamento | Nadie puede ayudar a los bandidos |

### Diseño propuesto por el autor para cada modo (a decidir en Unity)

No es contrato: es la intención, para que Conquest la valore.

- **Asedio:** el atacante gana si captura la bandera base antes de que acabe el tiempo; el defensor gana si se agota el
  tiempo con la base en pie. A medida que el atacante conquista banderas adelanta su punto de aparición (el modo
  *Batalla* actual de Conquest, con `CapturePoints.md` y `MatchTimer.md`). Quien muere no reaparece.
- **Batalla campal:** dos frentes. Por decidir: banderas (un fuerte por lado y el centro más la bandera del contrario) o
  gana quien se mantenga en pie, con opción de rendirse los dos bandos pasados unos 10 minutos. Se reaparece hasta el
  final, con una espera que **crece en cada muerte** (hoy el código usa 5 s fijos, `HeroDeath.md`).
- **Persecución:** una bandera por bando. Si el atacante pierde la suya gana el defensor y al revés; si se agota el
  tiempo gana el defensor. Quien muere no reaparece.
- **Asalto de caravana:** gana quien se queda con las caravanas, que pueden cambiar de dueño dentro de la partida.
  **No hay definición de este modo en la documentación de Conquest** que BronzeAge haya podido encontrar (se buscó
  `caravan`, `convoy`, `wagon` y `carreta` en `Docs/` y `Assets/`). Si existe, indicar dónde.
- **Campamento de bandidos:** sin cambios (los héroes contra la tropa del campamento).

Quien muere sin reaparición sale al mundo (§3).

## 2. Convocatoria del asedio: el ticket llega 30 s después

Al abrirse un asedio hay 30 segundos de mundo (`BATALLA.convocatoriaSegundos`) en los que se unen héroes según las
condiciones del Doc 5.15.1b; entran **en el ticket**, no como incorporaciones. Al cerrarse, el ticket se congela
(revisión 0) y solo entonces aparece en `GET /v1/batallas/pendientes`. Para Conquest: **nada que cambiar**, salvo que el
ticket llega 30 s más tarde y ya completo. Quien se une después sigue entrando por `incorporaciones` (CQ-005).

## 3. Salidas de héroe

```text
POST /v1/batallas/:battleId/salidas   (credencial batalla-servidor)

SalidaBatalla
  schemaVersion, battleId, intentoAsignacionId, ticketRevision
  heroeId
  motivo                     'no_conectado' | 'eliminado' | 'abandono' | 'desconexion'
  derrotado                  boolean
  escuadras                  [{ squadId, supervivientes, muertos }]   las de ese héroe
```

- **`no_conectado`**, solo con la batalla `asignada` (antes del `/inicio`): el héroe sale sin castigo (`derrotado: false`)
  con sus escuadras intactas. BronzeAge **sube `ticketRevision`**, borra la asignación y los tokens y devuelve la batalla
  a `convocando`; Conquest la recoge de nuevo por `/pendientes` y la reasigna. **No hay sustituto**: el bando sigue con
  los que quedan (meter al siguiente de la cola es cosa de Unity).
- **En curso** (`eliminado`, `abandono`, `desconexion`): BronzeAge aplica sus bajas, libera sus escuadras y su columna y,
  si `derrotado`, lo deja **herido**. **El ticket no cambia.** El `BattleResult` final **no repite a quien salió antes**:
  `porHeroe` y `porEscuadra` cubren solo a los que siguen dentro.
- `BattleResult.porHeroe[].derrotado` (nuevo, **opcional**): quién queda herido, sea del bando que sea. Sin él, BronzeAge
  hiere a todo el bando que pierde, como hasta ahora; con él, solo a quien venga marcado. Así el contrato actual sigue valiendo.

**Relación con CQ-010:** el abandono voluntario y la desconexión que no vuelve se comunican con esta misma ruta
(`motivo: 'abandono'` / `'desconexion'`) en vez de `POST .../abandonos` y de `porHeroe[].salida`. La regla de CQ-010
(abandonar = derrota; desconexión = resultado del bando) la aplica Unity al fijar `derrotado`. Preguntas abiertas de
CQ-010 que siguen siendo de Conquest: qué ocurre en la simulación con las escuadras de un héroe desconectado, y cuánto
espera antes de dar a alguien por no conectado.

## 4. Bandos con varias Facciones

Un bando puede mezclar Facciones (neutrales que se unen a un ataque, persecución libre). `BattleSide.faccionId` sigue
siendo la titular; cada `BattleParticipantSnapshot` e `IncorporacionBatalla.participante` lleva su `faccionId`. Unity no
tiene que hacer nada con él salvo no asumir que todo un bando es de una Facción.

## 5. Canal de tiempo real

El canal `batalla/<battleId>` (doc 02 §3.5) lleva los hechos públicos: `abierta`, `refuerzos`, `asignada`, `en_curso`,
`salida`, `revisada`, `aplicada`, `cancelada`, `fallida`. Conquest no tiene que implementar nada.

## Contrato afectado

- `ContextoEstrategico.campo_abierto.columnas` (nuevo).
- `BattleParticipantSnapshot.faccionId` (nuevo, obligatorio, `null` si no tiene).
- `BattleResult.porHeroe[].derrotado` (nuevo, opcional).
- Endpoint `POST /v1/batallas/:battleId/salidas` y su schema `SalidaBatalla`, con fixture.
- Checklist del resultado (doc 02 §3.3, puntos 2 y 4): cubre a los del ticket y las incorporaciones **menos los que salieron**.

## Criterios de aceptación

- Un asedio llega a `/pendientes` 30 s después de abrirse, con los héroes de la convocatoria ya en el ticket.
- Una batalla campal no admite incorporaciones; una persecución admite héroes de cualquier Facción.
- Un héroe que no conecta antes del inicio sale con `no_conectado`: la batalla vuelve a `convocando` con la revisión
  siguiente y se reasigna sin él.
- Un héroe eliminado sin reaparición, o que abandona, sale con su motivo y queda herido si `derrotado`.
- `BattleResult` cubre a todos los que siguen dentro y trae `derrotado` en cada uno.
