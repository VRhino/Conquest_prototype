# CQ-002 — Escuadras sin héroe y héroes bot en el servidor de batalla

**Destino:** `Conquest_prototype`
**Estado:** LISTO_PARA_REVISION
**Prioridad:** Alta
**Solicitante:** BronzeAge / Claude
**Fecha:** 2026-09-13
**Origen:** decisiones del autor del proyecto (2026-09-13). Reglas de juego en
`BronzeAgeFase0/Docs/Game/5_Sistema_Militar_y_Combate.md` §5.15; contrato en
`BronzeAgeFase0/Docs/Coordinacion/01_Modelo_de_datos_compartido.md` §15.

## Problema

En el modelo acordado, una escuadra solo combate si su héroe entra en la batalla y la usa; cuántas lleva lo
limita su liderazgo. Hay dos excepciones en las que una escuadra combate sin su héroe, manejada por IA de
videojuego (no LLM): la **escolta** de una caravana y la **guarnición** de un asentamiento. Además, las
Facciones NPC tendrán **héroes bot**, también manejados por IA.

El contrato anterior (versión previa de doc 01 §15) exigía al menos un participante humano por bando y no
preveía combatientes manejados por IA. Una caravana escoltada atacada, o un asentamiento sin defensores
presentes, no cabían.

## Comportamiento solicitado

- El servidor de batalla maneja con IA las **escuadras sin héroe** de un bando: la guarnición de un
  asentamiento asediado y la escolta de una caravana atacada.
- Maneja con IA los **héroes bot** (`controlador: 'bot'`) con sus escuadras, igual que a un héroe pero sin
  jugador conectado. Los héroes bot los crea el admin en BronzeAge y viven allí como un héroe más (decisión
  del autor, 2026-09-14); su comportamiento EN batalla es de Conquest.
- Maneja con IA las **tropas de un campamento de bandidos** atacado por un héroe humano (decisión del autor,
  2026-09-14): un bando sin héroes, en `escuadrasSinHeroe` pero sin `heroeId`, porque no son de nadie. Su
  composición la fija BronzeAge: en v1, una escuadra de milicia de lanceros con 15 unidades.
- Igual, los **carreteros** de una caravana sin escolta atacada por un héroe humano: una escuadra de milicia
  con 13 unidades, sin dueño, manejada por IA.
- Una batalla válida tiene **al menos un héroe humano en total**. Un bando puede ser solo IA o estar vacío:
  un asentamiento sin guarnición ni defensores presentes se asedia igualmente, sin defensores.
- `capacidadMaxima` de un bando cuenta **héroes**: 15 en un asedio, 5 en mundo abierto, contra una caravana o
  contra un campamento de bandidos (valores del autor, 2026-09-14). Las escuadras sin héroe no
  ocupan plaza y entran directamente. Los héroes que superan la capacidad esperan en cola y entran cuando
  caen otros (ya recogido en `Notas_de_integracion.md`).
- La guarnición la maneja siempre la IA: aunque su héroe esté presente en la batalla, no puede tomarla.

## Forma de datos

```text
BattleTicket.bandos[]:
  { ladoId, capacidadMinima, capacidadMaxima,
    participantes: BattleParticipantSnapshot[],     cada uno con controlador 'humano' | 'bot'
    escuadrasSinHeroe: SquadSnapshot[] }            guarnición o escolta, manejadas por IA
```

El `BattleResult` sigue exigiendo completitud: una entrada en `porEscuadra` también para cada escuadra sin
héroe, con su `xpGanada` (CQ-001).

## Invariantes de autoridad

- BronzeAge decide qué escuadras sin héroe entran (guarnición asignada, escolta de la caravana). Unity no
  añade ni quita combatientes del ticket.
- Los héroes bot los controla el servidor de batalla; ningún cliente los representa.
- Las consecuencias para el bando que pierde (héroes, escuadras, guarnición) las aplica BronzeAge (Doc 5.15).
  Unity solo informa hechos y XP.

## Compatibilidad

Sustituye la regla "al menos un participante humano por bando". Las batallas NPC contra NPC siguen fuera:
BronzeAge las resuelve con números y nunca llegan a Unity.

## Criterios de aceptación

- Asedio con defensores humanos más guarnición manejada por IA.
- Asedio sin defensores presentes: solo la guarnición, con IA.
- Asedio sin guarnición ni defensores: la batalla se juega sin defensores.
- Caravana con escolta (IA) atacada por un héroe humano.
- Héroe humano contra héroes bot más la guarnición de un asentamiento NPC.
- Héroe humano contra un campamento de bandidos (tropas manejadas por IA, sin dueño).
- 15 héroes defensores más una guarnición: la guarnición entra sin ocupar plaza y el héroe 16 espera en cola.

## Dudas para Codex

- ¿Conquest ya tiene IA de escuadra reutilizable, o hay que crearla?
- ¿Cómo se representa en el servidor una escuadra sin comandante (formación por defecto, órdenes de IA)?
- El comportamiento o la dificultad de un héroe bot: ¿va como parámetro del ticket (`BattleRules`) o es cosa
  de Conquest?
