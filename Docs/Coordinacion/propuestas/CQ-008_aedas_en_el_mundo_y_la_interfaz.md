# CQ-008 — Aedas: presencia en el mundo 3D y en la interfaz

**Destino:** `Conquest_prototype`
**Estado:** BORRADOR
**Prioridad:** Baja — nada de Unity depende de esto para jugar una batalla; es presentación de una mecánica nueva del mundo estratégico
**Solicitante:** BronzeAge / Claude
**Fecha:** 2026-10-05
**Origen:** cierre y primera implementación de los Aedas en `BronzeAgeFase0` (rama `claude/sleepy-goldwasser-15b7aa`, sin fusionar a
`main` todavía). Canon en `Docs/Game/6_Sistema_de_Tecnologia_y_Aedas.md` §6.7; modelo en
`Docs/Coordinacion/01_Modelo_de_datos_compartido.md` §22; decisiones en `Consideraciones/Aedas_Definicion.md`.

## Problema

BronzeAge tiene una mecánica nueva, los **Aedas** (poetas-narradores), que Conquest no sabe que existe. Hay dos clases:

- **Itinerantes:** NPC neutrales que recorren el mundo por la red de caminos, se detienen unas horas en cada asentamiento y, mientras
  están, le **revelan** a su Facción la tecnología que conocen y se la **venden** por oro.
- **Residentes:** viven en un asentamiento con Palacio y Nobleza y cantan una **épica por capítulos** que, al cumplirse, desbloquea una
  tecnología. Dan felicidad y crecimiento de nobleza.

Además hay una **crónica** pública de los hechos del servidor (logros, Eras, títulos, descubrimientos, caídas de plazas, guerras).

**No afectan a la batalla.** No combaten, no se atacan, no entran en un `BattleTicket` y `BattleResult` no cambia. Esta propuesta solo
pide cómo se ven y cómo se consultan en el cliente estratégico 3D cuando Conquest lo construya.

## Qué llega por la proyección del jugador

Ver `01_Modelo_de_datos_compartido.md` §22 de BronzeAge para el modelo completo. Resumen:

```text
aedasAvistados[]        { id, posicion, enAsentamientoId? }        itinerantes que se ven ahora (regla de avistamiento de ejércitos)
aedasResidentes[]       { id, nombre, asentamientoId, epica? }     solo los de la Facción propia; epica: tecnologiaId, nombre, capitulo,
                                                                    capitulos, tituloCapitulo, hechos, hechosNecesarios, ultimoHechoEn?
tecnologia.reveladas[]  { tecnologiaId, descubridorFaccionId, hito }
GET /jugador/partidas/:gameId/cronica?desde=&limite=              entradas públicas de la crónica, iguales para todos
```

Comandos nuevos: `comprarTecnologiaAeda`, `empezarEpica`, `abandonarEpica` (todos con la autorización de BronzeAge; Unity solo los envía).

## Comportamiento solicitado

1. **Marcador de Aeda itinerante en el mapa estratégico.** Una figura pequeña, neutral (sin color de Facción), en `posicion`. Distinguir
   **detenido** (`enAsentamientoId` presente: junto a la plaza) de **de camino**. Sigue los caminos porque BronzeAge ya los sigue; Conquest
   solo interpola entre posiciones.
2. **Aeda residente en la vista del asentamiento** (cuando exista, como la de campamento de BA-005/CQ-007): una figura de poeta en la
   plaza. Su nombre y su épica salen de `aedasResidentes`; Conquest no decide nada.
3. **Panel de crónica**: lista cronológica de `cronica`, y las que lleguen por el canal de eventos. Es texto ya redactado en `mensaje`.
4. **Panel de tecnología**: la Facción ve sus tecnologías (`oculta` → `revelada` → `aparecida` → `adoptada`) y, de las reveladas, quién las
   desbloqueó y el `hito` completo para comparar con lo que ya tiene. Con un Aeda detenido en su plaza, un botón de **comprar**.
5. **Panel de épicas del residente**: tecnología objetivo, capítulo en curso, hechos contados y cuándo puede contar otro (enfriamiento).

## Fuera de alcance

- Cualquier lógica de reglas: precios, cupos, hechos que cuentan, enfriamientos, etc. viven en BronzeAge y llegan resueltos.
- Modelos y arte: los decide Conquest (no se define aquí ningún asset).

## Criterios de aceptación

1. Con una partida de BronzeAge donde haya Aedas, el cliente muestra los itinerantes avistados, detenidos o de camino, sin cálculo propio de su ruta.
2. La crónica se ve completa y se actualiza en vivo; abrirla en un cliente nuevo muestra las últimas entradas.
3. Comprar una tecnología revelada y empezar una épica se hacen con los comandos de arriba, y el cliente muestra el motivo del rechazo si
   `codigoError` es `aedas.venta_invalida` o `aedas.epica_invalida`.

## Pendiente de BronzeAge

El contrato HTTP/OpenAPI de la proyección del jugador (`ProyeccionJugador`) todavía no vive en `src/contratos/v1/`; hoy se consume como
tipo del backend. Si Conquest quiere fixtures de estos campos, que lo diga y BronzeAge los publica con el resto de la proyección.
