# 32. El mapa estratégico 2D y Territorio (paso 9 del bloque 4)

2026-11-15. El plan, las tandas y las respuestas de Víctor: el plan del paso 9 (retirado de `tasks/` el 2026-11-23; sigue en git).

## Tanda 1: el mapa 2D, elegir un punto, Territorio y Fundación (a revisar)

### El mapa estratégico 2D

El «modo mapa» del prototipo (`main.ts: alternarVistaMapa`, `render.ts: pintarTerreno`, `ui/pantallaMapa.ts`), a pantalla completa encima
de cualquier pantalla. Es interfaz (UI Toolkit), no una cámara: el mapa 3D sigue siendo el mundo por donde anda el héroe.

- **`GeometriaVista`** (`Assets/Scripts/Mundo/`, C# puro): la cámara `{ Escala, Cx, Cy }` de `geometriaVista.ts` (topes, acotar, zoom hacia
  el cursor, arrastrar). Sus comprobaciones son las del `demo()` del prototipo, en `GeometriaVistaTests`.
- **`SueloDelMapa2D`** (`Mapa/`): `pintarArtefacto.ts`.
  - De lejos, una imagen de una celda por píxel hecha una vez: color por clase de terreno, ladera con luz del noroeste (con el raster
    `altura` u16), bosque por densidad y ríos.
  - De cerca (10 px o más por celda), la vista a media resolución de pantalla, con bordes de clase orgánicos y el suelo del bosque
    oscurecido. Al arrastrar se rehace como mucho cada 0,12 s.
  - Encima, los árboles sueltos (`Arboles`).
  - La capa de fertilidad de un cultivo, sobre lo explorado.
- **`MapaEstrategico`** (`Mapa/`, un `VisualElement`): las capas en el orden del prototipo.
  - El suelo y los árboles; bajo la niebla los ríos y vados de cerca, los caminos, las zonas ajenas y lo que solo se recuerda.
  - La niebla de la `VISTA` como textura estirada.
  - La fertilidad.
  - Encima, tu zona, los anillos, las rutas de caravanas y de tu columna, las batallas, el sitio que se elige, tu marca y tu destino.
  - Los iconos de `CosasDelMapa` con los dibujos del prototipo (`IconosDelMundo.DeCosa`), los nombres de los nodos de cerca y el sigilo en
    las plazas.
  - Las rayas van con `Painter2D` (con trazo discontinuo propio). `RayaDelMapa` lleva ahora su clase (camino, zona de Facción, zona de plaza,
    anillo) y su Facción.
  - Controles: rueda ×1,15 hacia el cursor, +/− ×1,4, arrastrar (un clic es soltar a menos de 5 px), ✕ y Esc. Las capas abajo a la
    izquierda: nombres de los nodos y la fertilidad de cada cultivo desbloqueado, con su leyenda.
  - Si no hay `VISTA` (dentro de una plaza, sin mapa 3D), la pide del mundo entero.
- **`PantallasDeMundo.Mapa2D`**: el botón «Mapa» de la barra del jugador, el último (detrás de los avisos), en el mundo y en los interiores.
  - **El clic, en el mundo** (Víctor, 2026-11-15):
    - en el suelo, la columna marcha allí (la misma orden y la misma ruta que el clic en el 3D) y la ruta sale en el mapa 2D, que sigue
      abierto;
    - sobre algo, se cierra el mapa, se elige y se marcha (`resolverClic`);
    - con varias cosas, la lista del mapa.
  - Desde dentro de un sitio, solo se mira.
  - `MapaDeMundo` gana `AbrirLista` y `CerrarLista`; con el mapa 2D abierto, el 3D no se mueve (`EntradaApagada`).

### Elegir un punto

`EleccionEnMapa`: la franja con el texto, la zona de la plaza rayada, los sitios de la aldea (verdes los válidos, grises los pegados a algo)
y el clic que manda la orden. El rechazo del motor sale en la franja; aceptada, el mapa se cierra. Los sitios los busca `SitiosDeAldea`
(`Assets/Scripts/Mundo/`, C# puro, `sitiosDeAldea` del prototipo), con su prueba en `GeometriaVistaTests`.

### Territorio

En el riel de la plaza, entre Defensa y Reclutamiento, a ventana completa (`PantallasDeMundo.Territorio`).

- **«Pueblos y aldeas»** (también de visita, solo para mirar):
  - la zona de influencia, una ficha por tipo de nodo y otra para el bosque;
  - el cupo con sus casillas;
  - la fila de pueblos y obras, con «+» delante; cada uno abre su detalle debajo, uno a la vez. «Marcar» sale abierto de serie si la plaza
    no tiene ninguno.
  - **Marcar**: la aldea (cultivo, coste con el carro y el buey, anillo, si hay tierra buena, «Elegir el sitio») y el extractor (los nodos
    libres más cerca primero, «Marcar»).
  - **La tarjeta del pueblo**: nivel, chips, almacén, rebaño y carro de acarreo; para quien gestiona, «⬆ Nivel N», «Reponer carro» y
    «✕ Destruir» con su confirmación.
  - **La tarjeta de la obra**: los materiales entregados con su barra, o lo que le queda; «✕ Cancelar» (la de pueblo o aldea, con
    `destruirPueblo` del pueblo de su número).
- **«Fundación»**: la caravana nueva con su coste contra el almacén, los cinco requisitos con su porqué, «Lanzar caravana»; las caravanas de
  la plaza con su titular, su estado, la caducidad y «✕ Desarmar». El «!» en el riel y en la pestaña cuando se puede lanzar.
- **Las obras de mapa en el HUD de obras** de la plaza: «Pueblos y aldeas · N» y «Avanzadas · N», con el % entregado o lo que les queda.

**Los nodos del motor** (`NodosDelMotor`): el motor llama a los nodos `nodo-N` (su orden entre los de piedra, ganado y metales del
artefacto), no por el id del artefacto. Es el id de `marcarPueblo`, de `pueblo.nodo_id`, de `obra.nodo_id` y de `edificio.fuente_id`
(`nodosConIdDelMotor` del prototipo).

### Iconos

- **Nuevo símbolo dibujado**: el de capas del mapa (`simbolo/capas`, el `ICONO` de `capasMapa.ts`).
- **Sin Twemoji nuevo**. Donde el prototipo pone un emoji que no tenemos, va el más parecido que ya hay:
  - ⛏ → el edificio de la mina o la cantera;
  - 🏘 → la casa (⌂);
  - 🗺 → el icono «Mapa» de la barra;
  - 🛒 → el carro (⛟);
  - 🌱 y 🌾 → 🌾;
  - 🐄 y 🐂 → el recurso ganado;
  - 📦 → el genérico de recurso;
  - 🔨 → ⚒;
  - ⏱ → ⏳;
  - 🏕️ → ⛺/🏕.

### Pruebas

- **PlayMode, contra el servidor 7891**:
  - `TuMapaEstrategico`: desde la plaza, «Mapa», rueda, +/−, arrastrar, solo mirar y Esc; en el mundo, el clic en el suelo marcha con la
    ruta en el mapa y el clic en la plaza la elige.
  - `TuTerritorio`, con la Vía Rápida: marcar un extractor (en la fila, en su detalle y en el HUD), elegir el sitio de la aldea con un clic
    en el mapa, cancelar la obra y Fundación.
  - Capturas a los cuatro tamaños (110 a 119).
- **EditMode**: `GeometriaVistaTests`.

### Lo que no entra en esta tanda

- La escolta del carro de acarreo y los interiores: la tanda 3.

## Tanda 2: las avanzadas, la puerta desde la ficha del mapa y las parcelas (a revisar)

La tanda 1 quedó aprobada (sin commit).

### Las avanzadas en Territorio

`PantallasDeMundo.Avanzadas` es la columna de la derecha de «Pueblos y aldeas», solo en una plaza de tu Facción (`htmlAvanzadasDeLaPlaza`).

- El cupo: `AVANZADAS.cupoPorNivelDeLaMadre` × el nivel operativo.
- **Una tarjeta por avanzada**:
  - sus escuadras de serie y de jugadores, y lo que guarda;
  - la tropa de serie de cada escalón, que el Gobernador elige entre las que la madre recluta (`elegirTropaDeAvanzada`, con un desplegable);
  - «⬆ Fuerte» o «⬆ Ciudadela», apagado si la plaza no llega a su nivel;
  - «Reparar» en ruinas, con la fracción de la reparación;
  - «✕ Destruir» con su confirmación;
  - su obra de mejora o de reparación, debajo.
- Las obras de atalaya nueva, con «✕ Cancelar» por `cancelarObra`.
- Lo que pide la atalaya y **«Elegir el sitio de la atalaya»**: el mapa 2D eligiendo un punto dentro de la zona, que manda `colocarAvanzada`.
- `TarjetaObra` cancela con `cancelarObra` las obras de avanzada, y con `destruirPueblo` las de pueblo o aldea.

### La puerta desde la ficha del mapa

`PantallasDeMundo.Puerta`, con `FICHA_OBRA`, `FICHA_PUEBLO`, `puertaDeAvanzada` y `htmlEntrega` del prototipo.

- **La ficha de una obra** lleva su entrega:
  - lo entregado de cada material con su barra, lo que falta y lo que lleva tu columna (verde si alcanza);
  - «Entregar materiales» (`entregarEnObra`, con lo que falta), apagado con el motivo si la columna está lejos o no hay columna.
- **Un clic en una obra tuya sin terminar** la elige y lleva la columna a su puerta, como en el prototipo (`MapaDeMundo.Elegir`).
- **Pueblo y avanzada**: un botón por ventana, detrás de las acciones; la ventana es una placa encima del mapa.
  - «Obra», si tiene una de mejora o de reparación.
  - «Guarnición»:
    - del pueblo: los de serie, tu cupo, «Retirar» las tuyas y «Dejar» las de tu columna;
    - de la avanzada: si resides en la madre, tu cupo de su etapa (`AVANZADA.cupoGuarnicion`) con lo mismo.
  - «Carga»: dejar lo del carro en el almacén del fuerte o la ciudadela, recurso a recurso con su cantidad (`depositarEnAvanzada`).
- **«Llenar víveres»** en la avanzada, para el Líder, a la puerta y con trigo en su almacén (`repostar`), entre las acciones de `CosasDelMapa`.
- Solo sale lo que se puede hacer desde donde está la columna (quieta, a `radioPuerta`).
- La obra de mapa en el mapa 3D se llama por su tipo («Obra de avanzada»…).

### La capa «Parcelas de bosque» del mapa 2D

`pintarParcelas` del prototipo: el círculo de 4 u de cada parcela explorada, más lleno cuanto más densa.

- Verdes las libres, ámbar las que ya tienen Leñera, rojas las que caen en la zona de una plaza de otra Facción (`ClasificadorDeParcelas`).
- Con su casilla y su leyenda en las capas.

### Pruebas

- `TuAvanzadaYSuObra`:
  - la columna de Avanzadas y elegir el sitio de la atalaya con un clic en el mapa 2D (la obra, en la columna y en el HUD);
  - salir con los materiales y una escuadra;
  - un clic en la obra en el mapa 2D lleva la columna a su puerta, y «Entregar materiales» la completa;
  - levantada la atalaya, su «Guarnición» deja la escuadra;
  - la capa de parcelas.
- Capturas 120 a 124 a los cuatro tamaños.

### Iconos

Cuatro Twemoji nuevos con el permiso de Víctor (2026-11-15), bajados con `generar.mjs --emoji`:

- ⛏ `glifo/pico`: el extractor, su obra y «Marcar»;
- 🛒 `glifo/carrito`: «Reponer carro»;
- 🗺 `glifo/mapa`: «Elegir el sitio» de la aldea y de la atalaya;
- 🏘 `glifo/pueblos`: el cupo de pueblos.

Sustituyen a los provisionales de la tanda 1.

### Los sitios verdes de la aldea, como los mira el motor

Un sitio que el mapa pintaba en verde salía rechazado («ese punto no es tierra firme»).

- **La causa**: para el motor (`zonas.rs: es_tierra_firme`), tierra firme es lo que no es agua y además se puede pisar según el raster
  `transitable` del artefacto. `sitiosDeAldea` del prototipo solo mira la clase de terreno (quita el agua y las cimas): al prototipo le pasa
  lo mismo.
- **El arreglo, en Conquest**: `SitiosDeAldea` lee ese raster de bits (`SitiosDeAldea.Transitable`) y deja las cimas como el motor. Lo que
  sale en verde es lo que el motor acepta, salvo el camino hasta la plaza, que solo sabe el motor.
- **La prueba**: `TuTerritorio` vuelve a pulsar el primer sitio verde que se ve. `GeometriaVistaTests` comprueba que lo que no se pisa no sale.

## Tanda 3: por dentro del pueblo, la aldea y la avanzada, y la escolta del carro (a revisar)

La tanda 2 quedó aprobada (sin commit).

### Entrar y salir

- **Entrar** («Entrar» de la ficha del mapa, a la puerta): el mapa ve que el héroe está dentro de un pueblo o una avanzada y monta su
  interior, como la plaza y el campamento (`PantallasDeMundo.Refrescar`).
- **Salir** («Salir al mundo» o F en la puerta): «Vuelves a la puerta de… con todo lo que trajiste; tu guarnición se queda», con
  `salirDePueblo` o `salirDeAvanzada`. Se cierra FeudoScene y vuelve el mapa con la columna a la puerta.

### El HUD

`PantallasDeMundo.Sitios` usa el esqueleto de la plaza (`Plaza.uxml` y las medidas de `AjustarPlaza`).

- El blasón del sitio: su emblema, su nombre, «De {madre}», «De tu Facción» o «De visita» y el nivel.
- Su almacén en el pie; la atalaya no tiene.
- El aviso de batalla si lo atacan.
- El riel con sus partes, cada una con su ventana:
  - **pueblo y aldea** (`pestanasDePueblo`):
    - Resumen: fertilidad, lo que saca, el uso de su cultivo, el rebaño y «Mejorar a nivel N» para quien gestiona la madre;
    - Guarnición: los de serie, tu cupo, «Retirar» y «Dejar» con tu columna;
    - Acarreo y escolta;
    - Carga: «Al carro» lo que guarda y «Dejar» lo que produce, con su cantidad;
  - **avanzada** (`partesDeAvanzada`):
    - Resumen: ruinas o ataque y sus cifras;
    - Guarnición: la de serie por escalón, si está mermada, y la tuya;
    - Almacén: lo que guarda con su barra, dejar lo del carro y «Llenar víveres»;
    - Tropa de serie: el Gobernador de la madre la elige.

### La escena (FeudoScene)

- **El pueblo** se monta con la planta que manda el motor al que está dentro (`Pueblo.planta` y `trazado`), con el estilo de su plaza
  madre hasta el 3. El héroe aparece al sur de todo, porque el pueblo no tiene puerta.
  - `Almacen.Buscar` devuelve del pueblo la copia que trae la planta (la de la `VISTA`); las de los demás ámbitos van sin ella.
- **La avanzada** se monta con la planta de su etapa hecha en el cliente, como el prototipo (`ciudad3d/avanzada.ts`):
  - atalaya: empalizada de 6 × 6 celdas con la torre de vigía, el pozo y la leñera;
  - fuerte: muro de 10 × 10 con torres en las esquinas, torre del homenaje, barracón, almacén, leñera y pozo;
  - ciudadela: muro redondo de radio 9 con sus torres, torre del homenaje grande, barracón, almacén, granero, patio de armas y pozo.

  En ruinas, el muro queda en pie solo en un 45 %.

### La escolta del carro de acarreo

`escolta` y `htmlEscoltaDentro` del prototipo:

- el cupo, que es el del Mercado de la madre por su nivel (`CARAVANA_ESCOLTA.liderazgoPorNivelMercado`) o el del primer nivel;
- lo cedido, con «Retirar»;
- con el carro parado: «Escoltar» con las escuadras de tu columna, dentro del pueblo, o «Ceder» con las del campamento, desde la tarjeta del
  pueblo en Territorio, si resides en la madre.

### Lo de las tandas anteriores que se toca

Los botones de las ventanas de la puerta (tanda 2) mandan la orden con `OrdenDePlaza`, que vale dentro y fuera.

### Prueba

`PorDentroDelPuebloYDeLaAvanzada`:

- marca una cantera y coloca una atalaya, sale con una escuadra y los materiales, entrega en las dos obras y, levantadas, entra en la
  cantera;
- en la cantera: el riel, la planta, la escuadra a la guarnición y de vuelta, «Acarreo y escolta», «Carga» y salir;
- entra en la atalaya: la empalizada, Guarnición, Tropa de serie y salir.

Capturas 125 a 131 a los cuatro tamaños, y la escena de FeudoScene de cada una (125c y 129c).
