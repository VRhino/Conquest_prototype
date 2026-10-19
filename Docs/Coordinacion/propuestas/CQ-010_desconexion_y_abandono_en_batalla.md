# CQ-010 — Desconexión y abandono de un héroe en una batalla de Unity (D33b)

**Destino:** `Conquest_prototype`
**Estado:** SUSTITUIDO — por [CQ-011](CQ-011_tipos_de_batalla_y_salidas.md)
**Prioridad:** Media — hoy no bloquea (con todos los bots y las batallas con números); bloquea abrir partidas con jugadores humanos en batallas de Unity
**Solicitante:** BronzeAge / Claude
**Fecha:** 2026-10-06
**Origen:** decisión D33b del autor del proyecto (`BronzeAgeFase0/Consideraciones/Campamentos_Entrada_Fundacion_Definicion.md`,
2026-10-03). Antes vivía solo como pendiente en la lista de mecánicas de BronzeAge; se traslada aquí porque exige trabajo
conjunto. Contrato vigente de la batalla en `BronzeAgeFase0/Docs/Coordinacion/02_Contrato_Comunicacion_Servidor_Cliente.md`
§3.2-§3.4 y `01_Modelo_de_datos_compartido.md` §15.

## Problema

Hoy, en BronzeAge, **quien está en una batalla no sale del mundo hasta que termina**: no hay forma de abandonarla ni de
reconectar a ella, y una caída de conexión durante la batalla no tiene tratamiento definido. D33b fija la regla:

| Situación | Qué le pasa al héroe |
|---|---|
| Se desconecta (involuntario) y **reconecta antes de que la batalla caduque** | **Vuelve a entrar en ella** |
| Se desconecta (involuntario) y no vuelve | Recibe **el resultado de su bando**: ganador si su equipo gana, perdedor si pierde |
| **Abandona por su propia mano** («abandonar batalla», mecanismo nuevo) | Se le aplica **derrota** |

## Comportamiento solicitado

### 1. Reentrada

- El servidor de batalla acepta que un héroe ya asignado se vuelva a conectar mientras la batalla siga viva, con el mismo
  `token` o con uno reemitido. BronzeAge ya permite recuperar la propia asignación (`GET .../asignacion`, §3.4); hace falta
  que Unity lo use al reconectar y que el servidor de batalla devuelva al héroe a su escuadra y posición de forma
  coherente (qué ve al volver: estado actual, sin repetir lo que pasó).
- **Pregunta abierta para Conquest:** qué ocurre en la simulación con las escuadras de un héroe desconectado durante la
  ausencia (se quedan inmóviles, siguen una IA mínima —ver CQ-002—, o desaparecen del bando). BronzeAge solo necesita saber
  cuál es para reflejarlo en el resultado.

### 2. Abandono voluntario

Acción del jugador en Unity («abandonar batalla»), confirmada. Conquest la comunica a BronzeAge:

```text
POST /v1/batallas/:battleId/abandonos   (credencial batalla-servidor)

AbandonoBatalla
  schemaVersion, battleId
  heroeId
  voluntario                 true
  en                         ISO-8601
```

- BronzeAge marca al héroe como derrotado en el resultado final, sea cual sea el resultado de su bando.
- El héroe deja de formar parte del bando en la instancia; sus escuadras se tratan como pierde las que no sobreviven
  (**pregunta abierta:** si abandonar implica perder las escuadras que llevaba en columna o solo cuenta como derrota
  del héroe — esto es una regla de juego, la decide el autor).

### 3. Desconexión involuntaria que no vuelve

- Pasado el tiempo de gracia que fije Conquest (la instancia tiene su propio plazo de caducidad), el héroe se da por
  perdido de la instancia sin abandono voluntario. En el `BattleResult`, su entrada de `porHeroe` lleva el **resultado de su
  bando** (ganador o perdedor), no derrota por abandono.
- Hace falta un campo para distinguirlo en el resultado: `porHeroe[].salida: 'normal' | 'abandono' | 'desconexion'`.
  BronzeAge lo usa para aplicar derrota (abandono) o el resultado del bando (desconexión).

## Contrato afectado

- `BattleResult.porHeroe[].salida` (nuevo, opcional con valor por defecto `'normal'`; sube el contrato de forma compatible).
- Endpoint nuevo `POST /v1/batallas/:battleId/abandonos` y su schema `AbandonoBatalla` en `contratos.schema.json`.
- Sin cambios en el ticket ni en las incorporaciones (CQ-005).

BronzeAge, al recibir la propuesta aceptada, añade el endpoint, el schema, el fixture y la lógica de aplicar el resultado
(derrota por abandono, resultado de bando por desconexión) y deja al héroe libre para salir del mundo en cuanto la batalla
se cierra.

## Criterios de aceptación

- Un héroe que cierra Unity en mitad de una batalla y vuelve antes de la caducidad regresa a la misma instancia.
- Un héroe que pulsa «abandonar batalla» queda derrotado en el resultado aunque su bando gane.
- Un héroe que se desconecta y no vuelve recibe el resultado de su bando.
- `BattleResult` sigue cubriendo a todos (conservación de efectivos, CQ-005 §3).

## Revisión de Conquest — 2026-10-15

La necesidad de reconexión y la distinción entre abandono y desconexión siguen vigentes. CQ-011 las
absorbe y el contrato actual las expresa mediante `SalidaBatalla` y `ResultadoHeroe.derrotado`.
No implementar `/abandonos`, `AbandonoBatalla` ni `porHeroe.salida` descritos aquí.

Reconectar debe recuperar la misma instancia y estado sin repetir spawn/reservas. La entrega privada
de instancia/token al jugador aún necesita forma de mensaje en la sesión (G2).

Para que una desconexión sin regreso reciba el resultado de su bando, mantener al héroe reservado
hasta conocerlo; una salida temprana con `derrotado` fijo no permite representar esa decisión futura.
Conducta de la tropa durante la ausencia y plazos de gracia son configuración táctica pendiente,
sin convertir una caída de transporte en abandono. Recompensas de salidas y confirmaciones requieren
G3/G4. Detalle en CQ-011 y en la [revisión conjunta](README.md).
