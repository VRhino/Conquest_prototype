# 36. Los Aedas, las épicas y la crónica (paso 13 del bloque 4)

2026-11-16. Lo que BronzeAgeClient hace con los Aedas itinerantes y residentes, las épicas y la crónica del mundo (§3.18 del inventario; el
antiguo CQ-008), en Conquest. El plan, las respuestas de Víctor (1a 2a 3a) y las dudas están en el plan del paso 13 (retirado de `tasks/` el 2026-11-23; sigue en git).

## Tanda 1: los Aedas (2026-11-16, en commit `615e23d4`)

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

## Tanda 2: la crónica (2026-11-16, a revisar)

Todo en `PantallasDeMundo.Cronica.cs`, calcado de `ui/cronica.ts`.

- **El botón «Crónica»** de la barra (el libro), tras la campana de Avisos (Víctor, 1a). Abre su panel lateral, el mismo marco que el de
  Avisos; abrir uno cierra el otro.
- **El panel:**
  - lo mismo para todos, de lo más nuevo a lo más viejo;
  - cada entrada con su icono, su frase y la fecha del mundo;
  - lo de tu Facción, marcado con el filete de oro.
- **Los datos:**
  - la primera página (200) se pide al abrirlo (`Sesion.PedirCronica`);
  - «Más antiguas» pide la anterior a la más vieja;
  - lo nuevo llega en vivo, del hecho `cronica.entrada` de las `Delta`, en cuanto la crónica ya se pidió;
  - una sesión nueva empieza de cero.
- **Las frases**, las del prototipo, con sus dos variantes por tipo. La variante sale del mismo hash y la misma clave (`versión|tipo|JSON de
  sus datos`), así que es la misma que en el prototipo. Cubren:
  - conquista, ruinas y fundación;
  - guerra, vasallajes, anexión y fusión;
  - títulos, logros del mundo y Eras;
  - descubrimientos cantados y épicas cumplidas;
  - la primera plaza en un nivel.
- **Los iconos**, de los que ya había: ⚔ conquista, 🔥 ruinas, ⌂ fundación, ⚑ diplomacia, ✦ títulos, ⚒ logros, ☀️ Eras, 📜 Aedas, ♛ niveles.
- **Arreglado de paso, en las pruebas:** el servidor de pruebas guardaba sus datos en `Temp/`, y Unity vacía esa carpeta al arrancar. El motor
  perdía el diario de la crónica y contestaba vacío. Ahora los guarda en `Logs/pruebas-mundo` y `Logs/pruebas-batalla`.
- **La prueba** es `LaCronica`:
  - el Rey y Gobernador funda su plaza y abre la crónica desde el libro, tras la campana;
  - la fundación sale marcada como suya, con su icono y su fecha;
  - la campana cierra la crónica y abre los Avisos.
  - **Sin prueba:** «Más antiguas», porque una partida nueva no llega a 200 entradas, y la entrada en vivo.
  - Con `MUNDO_PROTO=cronica`, solo la escena para `tasks/pruebas/cdp/cronica.mjs`.
