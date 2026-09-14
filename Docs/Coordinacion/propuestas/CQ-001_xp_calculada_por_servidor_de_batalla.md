# CQ-001 — XP de batalla calculada por el servidor de batalla

**Destino:** `Conquest_prototype`
**Estado:** LISTO_PARA_REVISION
**Prioridad:** Alta
**Solicitante:** BronzeAge / Claude
**Fecha:** 2026-09-13
**Origen:** decisión del autor del proyecto (2026-09-13). Contrato vigente en
`BronzeAgeFase0/Docs/Coordinacion/01_Modelo_de_datos_compartido.md` §15 y
`BronzeAgeFase0/Docs/Coordinacion/02_Contrato_Comunicacion_Servidor_Cliente.md` §3.3 (punto 11 del checklist).

## Problema

BA-001, BA-004 y la revisión de Codex del 2026-09-11 (punto 7: "Unity reporta hechos, no deltas de
progresión") fijaban que el servidor de batalla solo informa hechos y que BronzeAge calcula XP y nivel.
El autor del proyecto lo ha cambiado: la XP depende del desempeño en la batalla (unidades y héroes
abatidos, capturas de bandera, daño hecho y recibido, MVP, puesto en la tabla de su bando y otros
factores). Esos datos solo existen en el servidor de batalla; BronzeAge no puede calcularla.

## Comportamiento solicitado

- El servidor de batalla calcula la XP ganada en ESA batalla por cada héroe participante y por cada
  escuadra del ticket, y la incluye en el `BattleResult`.
- `xpGanada` es un delta de la batalla: entero, mayor o igual que 0. Nunca un total acumulado ni un nivel.
- La fórmula de XP es de Conquest (regla táctica). BronzeAge no la conoce ni la replica.
- Los hechos que alimentan la fórmula (bajas atribuidas, capturas, daño...) no necesitan viajar a
  BronzeAge: solo el resultado.
- `herido` **no** viaja en el resultado (decisión del autor, 2026-09-13): BronzeAge se lo aplica a todos los
  héroes del bando perdedor al aplicar el resultado.

## Forma de datos

```text
BattleResult
  ...
  porEscuadra: [{ squadId, desplegados, supervivientesAlCierre, muertos, xpGanada }]
  porHeroe:    [{ heroeId, participo, sobrevivioAlCierre, xpGanada }]
```

La completitud ya exigida sigue vigente: una entrada por CADA escuadra del ticket, aunque no se
desplegara (en ese caso `xpGanada: 0`).

## Invariantes de autoridad y seguridad

- Solo el servidor de batalla autenticado produce `xpGanada`. Un cliente Unity individual nunca: el mismo
  nivel de confianza que ya tiene para declarar muertos y ganador.
- BronzeAge valida antes de aplicar: entero, ≥ 0 y, si `BattleRules` define un tope de XP por batalla, por
  debajo de ese tope. Si falla, rechaza el resultado entero (`409`), sin aplicar nada.
- BronzeAge suma el delta a la experiencia persistida y aplica la curva de nivel del catálogo versionado.
  Unity no envía el nivel resultante.
- Idempotencia: reenviar el mismo `resultId` no duplica XP (el delta se aplica una sola vez, controlado por
  `Batalla.appliedResultId`).

## Compatibilidad y migración

- Sustituye el punto 7 de `REVISION_CODEX_2026-09-11.md` y lo que decía R05 de
  `REVISION_CONTRATOS_CODEX_2026-09-11.md` sobre qué hechos alimentan la XP. Los textos de esas revisiones
  se conservan como historial; la regla vigente está en doc 01 §15.
- Todavía no hay schema publicado (`src/contratos/v1/` no existe). Esto entra en el primer schema de
  `BattleResult`.

## Criterios de aceptación

- Un fixture de `BattleResult` con `xpGanada` por héroe y por escuadra es aceptado por BronzeAge.
- `xpGanada` negativa, no entera o por encima del tope se rechaza con `409` y no aplica ninguna
  consecuencia.
- Reenviar el mismo `resultId` no duplica la XP.
- Dos escuadras de héroes distintos reciben su XP de forma independiente.

## Dudas

Respuestas del autor del proyecto (2026-09-13):

- La XP de escuadra **también** la calcula el servidor de batalla, con toda la información de la partida.
- "Herido": lo sufren todos los héroes del bando perdedor, durante 2 minutos de mundo, y lo aplica BronzeAge
  con el resultado; por eso el campo `herido` se quita del `BattleResult`. Mientras dura, el héroe no puede
  ser perseguido, perseguir ni entrar en batallas (BronzeAge no lo incluirá en ningún ticket).

Para Codex:

- La curva XP→nivel (héroe y escuadra): ¿se publica como catálogo versionado compartido para que BronzeAge
  la aplique, igual que el resto de catálogos puente?
- ¿Tiene sentido un tope de XP por batalla desde vuestro sistema de puntuación? Su valor sería de balance.
- Mapeo de los campos de XP que ya persiste Conquest (`Hero.Data.cs` `currentXP`, progresión de
  `SquadInstance.Data.cs`) a este delta.
