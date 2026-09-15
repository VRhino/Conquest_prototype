# CQ-005 — Héroes que se unen a una batalla ya abierta

**Destino:** `Conquest_prototype`
**Estado:** LISTO_PARA_REVISION
**Prioridad:** Alta — cambia cómo el servidor de batalla forma los bandos
**Solicitante:** BronzeAge / Claude
**Fecha:** 2026-09-15
**Origen:** decisión del autor del proyecto (2026-09-15). Contrato vigente en
`BronzeAgeFase0/Docs/Coordinacion/01_Modelo_de_datos_compartido.md` §15 ("Incorporaciones") y
`BronzeAgeFase0/Docs/Coordinacion/02_Contrato_Comunicacion_Servidor_Cliente.md` §3.2-§3.3. Reglas de juego en
`BronzeAgeFase0/Docs/Game/5_Sistema_Militar_y_Combate.md` §5.15.1.

## Problema

Hasta ahora el ticket de una batalla era la lista cerrada de quién combate: después de emitirlo no entraba
nadie más (BA-001, decisión 3). El autor ha decidido otra cosa para un mundo de cientos de jugadores: la
batalla es una instancia aparte, el resto del mundo sigue, y **mientras no termine, un héroe de la Facción de un
bando puede unirse a él si ese bando no está lleno**, también con la partida ya en marcha. Lo mismo en asedios,
en mundo abierto y en ataques a caravanas.

## Comportamiento solicitado

### 1. El ticket no cambia; las incorporaciones van aparte

```text
GET /v1/batallas/:battleId/incorporaciones   ->  IncorporacionBatalla[]

IncorporacionBatalla
  schemaVersion, battleId
  secuencia                  1, 2, 3… en el orden en que se unieron (la lista solo crece)
  lado                       'atacante' | 'defensor'
  participante               BattleParticipantSnapshot, igual que los del ticket
```

- No sube `ticketRevision`: una incorporación no invalida la asignación ni los tokens de nadie.
- BronzeAge solo acepta la incorporación si el bando tiene sitio (`capacidadMaxima`, en héroes). Los que
  estaban al abrir y sobran siguen esperando en cola como hasta ahora; los que llegan después, no.
- Las escuadras del incorporado quedan reservadas en BronzeAge como las del ticket.

### 2. Token del que se une después de la asignación

```text
POST /v1/batallas/:battleId/tokens   (credencial batalla-servidor)

TokensBatalla
  schemaVersion, battleId, intentoAsignacionId
  tokensParticipante: [{ heroeId, token, expiraEn }]
```

Si la batalla aún no tiene asignación, el token del incorporado va en la `BattleServerAssignment` normal, como
el de cualquier otro. El jugador lo recoge por la ruta de siempre (`GET .../asignacion`).

### 3. El resultado cubre a todos

`BattleResult` trae una entrada en `porEscuadra` por cada escuadra reservada en el ticket **o en sus
incorporaciones**, y en `porHeroe` a los incorporados. La conservación (`supervivientesAlCierre + muertos ==
efectivosAutorizados`) se cierra igual.

## Fixtures

`BronzeAgeFase0/src/contratos/v1/fixtures/incorporacionBatalla.json` y `tokensBatalla.json`, con sus
definiciones en `contratos.schema.json`. `battleResult.json` ya incluye a la heroína incorporada.

## Criterios de aceptación

- El servidor de batalla hace polling de `.../incorporaciones` mientras la partida está viva y mete al héroe en
  su bando cuando aparece una secuencia nueva.
- Un humano incorporado tras la asignación recibe su token por `POST .../tokens`.
- Un resultado que omite a un incorporado se rechaza (`409`), igual que si omitiera a uno del ticket.

## Dudas para Codex

1. ¿El servidor de batalla puede meter jugadores nuevos en una partida ya empezada, o hace falta cambiar el
   ciclo de la instancia?
2. ¿Dónde aparece el incorporado en el mapa táctico? En asedio y mundo abierto, BronzeAge solo sabe de qué
   bando es; el punto de entrada es decisión táctica de Conquest.
3. ¿Polling de `.../incorporaciones` os basta, o preferís que BronzeAge os avise? Hoy no hay canal de BronzeAge
   hacia el servidor de batalla.
