# Paso 13: Aedas, épicas y crónica (análisis)

2026-11-16. Solo texto, sin capturas del «antes». Es el último paso del bloque 4.

## Qué abarca

La hoja de ruta (bloque 4, paso 13; el antiguo CQ-008) y el inventario (§3.18):
- los Aedas itinerantes en el mapa;
- la ventana Aedas de la plaza: comprar tecnologías a un itinerante y las épicas de los residentes;
- la crónica del mundo.

## Lo que hay

### En BronzeAgeClient

- **Los Aedas itinerantes** (`ui/aedas.ts`, `mundo/aedas.ts`):
  - **En el mapa:**
    - una lira donde va, o una pequeña junto a la plaza donde está parado (el modo mapa);
    - en el mundo 3D, su figura a pie con su burro.
  - **El nombre** lo pone el cliente, porque el motor solo da `aeda-N`: una lista de 24 bardos griegos (Demódoco, Femio, Tamiris…), siempre el
    mismo para el mismo N.
  - **Su ficha del mapa** (un clic en él):
    - quién es y dónde está (parado en tal plaza, o de camino);
    - la distancia y el tiempo hasta tu columna;
    - qué hace: va de plaza en plaza, se queda `estanciaMinutos` en cada una y revela lo que sabe;
    - parado en una plaza tuya, lo que os vende: las tecnologías reveladas, con su precio y «Comprar».
  - **Los avisos** de `aedas.*` para tu Facción:
    - revela y venta;
    - llega un residente y se va un residente;
    - empieza una épica, cierra un capítulo, se cumple, se abandona (con su motivo: conquista, aparecida, unificada u orden de la plaza).
- **La ventana Aedas de la plaza** (`ui/ventanaAedas.ts`). Un icono más en el riel, la lira, que solo sale en una plaza tuya con algún Aeda
  dentro (Víctor, 2026-11-07: «los Aedas son de plaza, no de Facción»). Dentro:
  - **El itinerante parado:**
    - su nombre, qué hace y cuánto se queda;
    - lo que os vende: cada tecnología revelada con su precio en 🪙 y «Comprar»;
    - el oro del almacén de la plaza, que es quien paga.
  - **Los residentes:**
    - qué dan a la plaza: felicidad y crecimiento de la nobleza;
    - su épica en curso: su nombre, la tecnología, una barra de avance, el capítulo con su título, los hechos que lleva y los que pide, cuándo puede
      contar otro, y «Abandonar» con confirmación;
    - sin épica, las que puede cantar, con «Que la cante».
- **La crónica del mundo** (`ui/cronica.ts`):
  - **Dónde:** el botón «Crónica» de la barra (el libro), que abre un panel lateral como el de Avisos.
  - **Qué enseña:** lo mismo para todos los jugadores, de lo más nuevo a lo más viejo, cada entrada con su icono, su frase y la fecha. Lo de tu Facción
    va marcado.
  - **Qué entradas:** caídas, ruinas y fundaciones de plazas; guerras, vasallajes, anexiones y fusiones; títulos que nacen o cambian de manos;
    logros del mundo y Eras; los descubrimientos que cantan los Aedas; las épicas cumplidas; la primera plaza que llega a cada nivel.
  - **Las frases** las compone el cliente, con dos variantes por tipo elegidas con un hash de la entrada (siempre la misma para la misma).
  - **Por páginas:** las 200 últimas y «Más antiguas»; lo nuevo llega en vivo.
- **En Facción › Tecnología**, el árbol dice qué tecnologías reveló un Aeda y lo que cobraría (ya está en Conquest).
- **En la Puerta de la plaza**, el grupo «Aedas» para cerrarles la puerta (ya está en Conquest).

### En el motor (main)

- **Las órdenes existen todas:**
  - `comprarTecnologiaAeda { asentamiento_id, tecnologia_id }`;
  - `empezarEpica { asentamiento_id, aeda_id, tecnologia_id }`;
  - `abandonarEpica { asentamiento_id, aeda_id }`.
- **Quién puede** (`Autoriza`):
  - **Comprar** (`CompraTecnologiaAeda`): la cuenta del héroe, dentro de la plaza y Rey o Gobernador. Paga el oro del almacén de la plaza.
    Precio: `TARIFA_ADOPCION[era].oro × AEDAS.venta.factorOro`, solo hasta `ordenEraMaximo`.
  - **Empezar o abandonar una épica** (`DirigeAedas`): el Rey de la dueña, el Gobernador o el Sacerdote de la plaza, dentro.
  - **La crónica** (`PedirCronica` en la sesión): cualquier sesión, también quien llegó tarde. Contesta `Cronica { entradas, mas_antiguas,
    mas_recientes, version }`; lo nuevo llega en las `Delta` como el hecho `cronica.entrada`, con su `tipo` y sus datos en el payload.
- **Qué llega y dónde:**
  - **El itinerante** (`AedaItinerante`, `aeda-N`) por la `VISTA`, como una columna ajena: su posición, el tramo visible de su plan y la plaza
    donde está parado (`en_asentamiento_id`).
  - **El residente** (`AedaResidente`, `aeda-rN`) en `FACCION`, solo de la dueña: su nombre, su plaza, cuándo llegó y su épica (`EpicaEnCurso`:
    tecnología, capítulo, hechos, último hecho, capítulos, hechos necesarios).
  - **Lo revelado y las épicas posibles** van en la tecnología de la Facción: `reveladas`, `detalle_reveladas` y `epicas_posibles`.
  - **Los datos de `/v1/datos`:** `AEDAS` (venta, itinerantes, residentes, épica), `EPICAS` (nombre y capítulos) y `TITULO_CAPITULO`.
- **Reglas** (`Tecnologia_Aedas` §6 a §8):
  - un residente necesita Palacio activo y algo de nobleza; los que caben dependen del nivel (`cupoPorNivel`), y llega uno cada
    `llegadaMinutos` (más lento con la reputación baja);
  - una épica avanza un hecho como mucho cada `enfriamientoMinutos`;
  - la crónica es pública y la guarda el motor entera.
- **No hay órdenes de administración para colocar Aedas.**

### En Conquest

- **Ya está:**
  - `Sesion.PedirCronica` (desde, hasta, límite) y su respuesta;
  - los Aedas en el almacén (`TipoEntidad.Aeda`, itinerantes y residentes);
  - el itinerante de camino como un cuadro en el mapa («Aeda · De camino»), sin ficha propia, y el parado no se pinta (va en su plaza);
  - el icono 📜 de los Aedas en el mapa, la lira del riel (`riel/aedas`) y el libro de la barra (`barra/cronica`), ya traídos con el resto;
  - `PrecioDeAeda` en Facción › Tecnología y lo revelado en el árbol;
  - el grupo «Aedas» de la Puerta de la plaza;
  - la categoría «Aedas y tecnología» de los avisos (vacía);
  - los textos de los rechazos `aedas.*`.
- **Falta:**
  - la ficha del Aeda en el mapa;
  - la ventana Aedas de la plaza;
  - los avisos de `aedas.*`;
  - la crónica: el botón y su panel.

## Lo que voy a hacer

- **Calcar el prototipo** panel por panel, con sus iconos. Solo sale lo que el jugador puede hacer: ni botones apagados ni el porqué.
- **Dos tandas, con parada al acabar cada una:**
  - **Tanda 1, los Aedas:**
    - la ficha del itinerante en el mapa: su nombre, dónde está, la distancia y el tiempo hasta tu columna, qué hace y, parado en una plaza tuya,
      lo que os vende, solo para mirar, porque se compra dentro;
    - el parado, marcado junto a su plaza en el mapa;
    - la ventana Aedas de la plaza, en el riel, solo con un Aeda dentro:
      - el itinerante, con lo que vende y «Comprar» con su precio, para el Rey o el Gobernador presente;
      - los residentes, con lo que dan, su épica en curso (barra, capítulo, hechos y espera) y «Abandonar»;
      - sin épica, «Que la cante» para el Rey, el Gobernador o el Sacerdote presente;
    - los avisos de `aedas.*`.
    - Prueba: el Gobernador con un residente (la plaza subida de nivel y con Palacio, y el reloj adelantado) le encarga una épica, la ve
      avanzar y la abandona. Con un itinerante parado en la plaza, compra una tecnología.
  - **Tanda 2, la crónica:**
    - el botón «Crónica» de la barra (el libro), con su panel: lo más nuevo arriba, cada entrada con su icono, su frase y la fecha;
    - lo de tu Facción, marcado; «Más antiguas»; lo nuevo, en vivo.
    - Prueba: fundar una plaza y verla en la crónica; pedir las más antiguas.
- **Los iconos de la crónica**, con los que ya hay, sin Twemoji nuevos:
  - ⚔ conquista, 🔥 ruinas, ⌂ fundación, ⚑ diplomacia, ✦ títulos;
  - ⚒ logros, ☀️ Eras, 📜 Aedas, ♛ niveles.
- **Pruebas** con ratón y teclado a los cuatro tamaños, con las capturas junto al prototipo. Al acabar cada tanda, la suite en dos lotes y las
  dos batallas.

## Respuestas de Víctor (2026-11-16)

- **1a**: la crónica, en un botón «Crónica» de la barra tras Avisos, con su panel lateral como el de Avisos.
- **2a**: el itinerante, un cuadro en el mapa hasta tener los modelos; el parado, junto a su plaza.
- **3a**: la compra se prueba con el reloj adelantado hasta que un itinerante pare en la plaza de la prueba; si no para en un tiempo razonable,
  se queda sin prueba y se dice.

## Dudas (contestadas arriba)

1. **Dónde va la crónica.**
   - a) Un botón «Crónica» en la barra, tras la campana de Avisos, que abre su panel lateral como el de Avisos, como el prototipo. **(propuesta)**
   - b) Una pestaña «Crónica» dentro del panel de Avisos, sin botón propio.
2. **El Aeda itinerante en el mapa.**
   - a) Un cuadro como ahora, hasta tener los modelos (como los carros, Víctor 2026-11-16), y el parado con una marca junto a su plaza. **(propuesta)**
   - b) Su figura a pie con el burro, como el mundo 3D del prototipo.
3. **Cómo probar la compra a un itinerante.** El motor no tiene órdenes de administración para colocar Aedas, y el itinerante va a la plaza que
   quiere.
   - a) Probarla con el reloj adelantado hasta que uno pare en la plaza de la prueba. Si no para en un tiempo razonable, la compra se queda
     sin prueba y se dice. **(propuesta)**
   - b) Pedir al motor una orden de administración que pare un Aeda en una plaza (`colocar_aeda`), y probarla con ella cuando esté.

## Hecho

- **Tanda 1 (los Aedas), en commit `615e23d4`**: doc 36. Sin prueba la compra (nada revelado en una partida nueva), los residentes y las épicas
  (piden Palacio y nobleza) y la ficha del mapa.
- **Tanda 2 (la crónica), 2026-11-16, a revisar**: doc 36. Los datos del servidor de prueba salen de `Temp/` (Unity la vaciaba y el motor perdía
  la crónica). Sin prueba: «Más antiguas» y la entrada en vivo.
