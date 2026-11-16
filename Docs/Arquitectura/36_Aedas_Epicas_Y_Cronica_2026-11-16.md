# 36. Los Aedas, las épicas y la crónica (paso 13 del bloque 4)

2026-11-16. Lo que BronzeAgeClient hace con los Aedas itinerantes y residentes, las épicas y la crónica del mundo (§3.18 del inventario; el
antiguo CQ-008), en Conquest. El plan, las respuestas de Víctor (1a 2a 3a) y las dudas están en `tasks/bloque4-paso13-plan.md`.

## Tanda 1: los Aedas (2026-11-16, a revisar)

Todo en `PantallasDeMundo.Aedas.cs`, calcado de `ui/aedas.ts`, `ui/ventanaAedas.ts` y `mundo/aedas.ts`.

- **En el mapa** (`CosasDelMapa`), Víctor 2a:
  - el itinerante, un cuadro hasta tener los modelos;
  - el parado, un cuadro pequeño junto a su plaza (antes no se pintaba);
  - su nombre, de la lista de bardos del prototipo (`NombreDeAeda`: `aeda-3` → «Tamiris»), porque el motor no los nombra;
  - el detalle, «Parado en …» o «De camino a otra plaza».
- **Su ficha** (`FichaDeAeda`), debajo de la distancia y el tiempo:
  - qué hace y cuánto se queda en cada plaza;
  - parado en una plaza de tu Facción, lo que os vende con su precio en oro;
  - es solo para mirar: se le compra dentro, con la ventana Aedas.
- **La ventana Aedas de la plaza** (`VentanaAedas`): la lira del riel, que solo sale en una plaza tuya con algún Aeda dentro.
  - **El itinerante parado:**
    - sus nombres y qué hace;
    - lo que os vende, cada tecnología con su precio en 🪙;
    - «Comprar» solo para el Rey o el Gobernador, presentes, y con el oro en el almacén;
    - el oro del almacén de la plaza.
  - **Los residentes:**
    - qué dan a la plaza;
    - su épica en curso: barra, capítulo con su título, hechos y cuándo cuenta otro, y «Abandonar» con confirmación;
    - sin épica, las que puede cantar, con «Que la cante», solo para el Rey, el Gobernador o el Sacerdote, presentes.
  - Sin botones apagados ni el porqué (regla de Víctor): a quien no puede, no le sale.
- **Los avisos** de `aedas.*` para tu Facción (`Avisos.TextoDeAeda`), en «Aedas y tecnología»:
  - revela y venta;
  - llega o se va un residente;
  - empieza, avanza, se cumple o se abandona una épica, con su motivo.
  - Salen en pantalla la venta, la épica cumplida, el residente que se va y la épica perdida por conquista.
- **La prueba** es `TusAedas`:
  - el Rey y Gobernador funda su plaza;
  - en un servidor recién abierto, los tres primeros itinerantes nacen parados en ella; si no, se adelanta el reloj hasta que uno pare
    (Víctor, 3a);
  - abre la ventana desde la lira del riel y ve al itinerante.
  - **Sin prueba:**
    - la compra: en una partida recién empezada nada está revelado y no os vende nada;
    - los residentes y las épicas: piden Palacio y nobleza, y el servidor de pruebas no tiene cómo ponerlos;
    - la ficha del mapa.
  - Con `MUNDO_PROTO=aedas`, solo la escena para `tasks/pruebas/cdp/aedas.mjs`.
