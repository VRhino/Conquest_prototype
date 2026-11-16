# 33. El Mercado y las caravanas (paso 10 del bloque 4)

2026-11-16. Lo que BronzeAgeClient hace con el comercio de la plaza (§3.14 del inventario), en Conquest. El plan, las dudas y las respuestas de
Víctor están en `tasks/bloque4-paso10-plan.md`.

## Tanda 1: las órdenes del Mercado (2026-11-16, a revisar)

- **Mercado en el riel de la plaza**, entre Reclutamiento y la Armería.
  - Sale con el edificio activo (`mercado_activo` o un Mercado en pie), también de visita.
  - La ficha del Mercado y la del Albergue de caravanas tienen «Abrir el Mercado».
  - La firma del riel incluye ahora el Mercado y la Armería activos. Antes, la Armería recién acabada no aparecía hasta otro cambio.
- **En tu plaza, la pestaña Órdenes** (`PantallasDeMundo.Mercado.cs`, calcada de `htmlOrdenes`). A ventana completa:
  - a la izquierda, la boleta: ▲ Vender o ▼ Comprar, el almacén como casillas (primero lo que hay), el recurso elegido, la cantidad, el precio
    («mercado» si va vacío) y el total;
  - a la derecha, las órdenes en pie: el recurso con su flecha, «Vende/Compra N», la barra de lo cumplido, el precio y lo que les queda.
  - **«✕»** cancela una orden (`cancelarOrdenMercado`; el prototipo no la manda).
  - La boleta y el ✕ salen solo al Gobernador o al Tesorero residente: el motor no deja a nadie más (`comando.rs`), aunque el prototipo
    enseñe la boleta a cualquier residente.
- **En una plaza ajena**, sus órdenes abiertas (`htmlMercadoDePlaza`): la cantidad y «Comprar» o «Vender», que el Líder de la columna toma con
  su carro (`comerciarEnPlaza`).
- **Desde el mapa**: con la columna a la puerta de una plaza con Mercado, su ficha tiene «Mercado».
  - En tu plaza, si gestionas las órdenes, abre la pestaña Órdenes; si no, sus órdenes abiertas.
  - La ventana es ancha (900 px), con la boleta arriba y las órdenes en pie debajo, como la del prototipo.
- **Textos**: los rechazos de `mercado.*`, `orden.*` y `mostrador.*`, y el aviso `mercado.orden_cancelada`.
- **Cantidades y precios** van en milésimas, como en el resto del contrato. El precio de mercado del trigo en el servidor de prueba es
  945,78 de oro, igual en el prototipo.
- **La prueba** es `TuMercado` (`PantallasDeMundoTests.Mercado.cs`).
  - La plaza se funda con `mercadoEnPie`: el Mercado va entre las obras cortas, en lugar de la bodega, porque la cola admite cuatro.
  - El recorrido: el riel, vender madera a 2, pedir trigo a precio de mercado, cancelar la venta y la ventana Mercado desde la ficha de la
    plaza en el mapa.

## Tanda 2: las caravanas, su escolta y el carro 3D (2026-11-16, a revisar)

- **La pestaña Caravanas del Mercado** (`PantallasDeMundo.Flota.cs`, calcada de la vista de flota de `htmlCaravanas`).
  - Arriba, el Mercado y el Albergue, con los cupos de la flota en casillas: caravanas y carros, del nivel de los dos edificios y de la
    política `cupoCaravanaExtra`. También el Liderazgo de escolta.
  - «＋ Crear» sale si resides, hay Mercado y la flota tiene sitio; durante la espera entre caravanas, «Otra, en N min».
  - A la izquierda, la lista: primero las que están en la plaza, luego las aparcadas aquí y al final las que andan fuera.
  - A la derecha, la elegida:
    - el nombre, con ✎ para ponérselo (`renombrarCaravana`) y «Reservada» (`reservarCaravana`);
    - lo que carga, su velocidad y lo que tarda en prepararse;
    - lo que lleva, con «⬇» para descargarlo al almacén;
    - los carros en huecos. Con «+» se añade un carro y su animal a la vez: el carro solo entre los que se fabrican en la plaza, y lo que no
      se puede pagar sale apagado (`agregarCarroCaravana` + `comprarAnimalCaravana`). Un carro elegido abre su ficha, para comprarle el
      animal que le falte o pasarlo a otra caravana parada (`moverCarroCaravana`);
    - la escolta;
    - el viaje: destino, carga del almacén contra la capacidad y «Lanzar ya» o «Preparar (N min) y lanzar» (`prepararCaravana`). Mientras se
      prepara, «Cancelar preparación» (`cancelarCaravana`).
  - Las aparcadas de otra plaza: su carga, «⬆ Cargar» y «⬇ Descargar» por recurso (`moverCargaCaravanaAparcada`) y «↩ Enviar al origen»
    (`enviarCaravanaAlOrigen`).
  - El Encargo (rutas y envíos) llega en la tanda 3.
- **La escolta comercial** es la misma pieza que la del carro de acarreo (`Escolta`), con su cupo, su título y su nota.
  - El cupo es el del nivel del Mercado; sin Mercado, 0, como el motor (`cupos_de`).
  - Cede escuadras del campamento quien reside en la plaza.
- **En el mapa**: la ficha de una caravana propia lleva su nombre, sus carros y lo que lleva.
- **El carro 3D** (`Mapa/CarroDeCaravana.cs`) es una pieza reutilizable.
  - Lleva el carro de Synty (`Resources/MapaMundo/CarroDeCaravana.prefab`, que arma `Editor/CarroDeCaravanaSetup.cs`) con el animal
    delante, a escala real.
  - Mira hacia donde avanza; el animal anda mientras se mueve y se para cuando se para.
  - `CosasEnElMapa` lo pone en lugar de la caja para las caravanas que viajan solas, y en el paso 11 servirá detrás del ejército.
  - **El animal es el caballo del héroe para todos**: no hay modelo de buey ni de camello. La ventana sí enseña el animal de verdad de cada
    carro (🐂🐎🐫). Es duda planteada a Víctor; mientras contesta, va así.
- **La prueba** es `TuFlota`: crear una caravana, añadirle un carro con su buey, ceder escolta, lanzarla con trigo a otra plaza de la Facción
  y verla en el mundo con su carro 3D y su ficha.
  - La otra plaza la funda otra cuenta de la misma Facción: `FundarPlaza` con `enLaFaccion`, y la Facción subida a nivel 3, porque a nivel 1
    solo cabe una plaza.

## Tanda 3: rutas, trueques y la región (2026-11-16, a revisar)

Todo está en `PantallasDeMundo.Rutas.cs`.

- **El Encargo de una caravana** (`encargoCaravana.ts`). Sin encargo y parada, la caravana tiene «Viaje» o «Encargo» para quien los da: el
  Gobernador, el Tesorero o el Maestro de Caravanas, residente.
  - **El formulario**:
    - el modo (una ruta permanente entre plazas tuyas o un envío de ida y vuelta) y el destino;
    - en un envío, «Salir dentro de N min»;
    - las dos paradas, con botones de recurso: «Recoge», lo que hay en el almacén del origen; «Deja», solo lo que se recoge en la otra.
      Cada línea lleva su cantidad, y su precio en una plaza ajena. También «Esperar a que haya todo» y «Tirar lo que no quepa»;
    - «Dar encargo» (`encargarCaravana`) o «Guardar cambios» (`cambiarEncargo`).
  - **Con encargo**: la ruta dibujada (origen y destino, la carreta en el camino, la fase, las vueltas y la barra de la carga o la descarga),
    las paradas en fichas y «✎ Cambiar», «⏹ Detener ruta» (`detenerEncargo`) y «Cancelar encargo» (`cancelarEncargo`), estos dos con su
    confirmación.
- **La pestaña Rutas**: las caravanas de la plaza con encargo, cada una con su ruta, y el recuento de rutas y envíos.
- **La pestaña Trueques** (`panelTrueques.ts`):
  - los pactos en curso y los cerrados, con las dos plazas frente a frente, lo que da cada una (lo entregado, en vigor), el estado y el
    plazo;
  - «✓ Aceptar» y «✕ Rechazar» los recibidos;
  - «Proponer un trueque»: la otra plaza y hasta tres líneas por lado, con la balanza en medio (`proponerTrueque`).
  - Lo manejan el Gobernador o el Tesorero, como en el motor.
- **La Taberna en el riel de la plaza**, por ahora solo con el informe de la región (`htmlServicioRegion`): «Sus plazas» y «Sus órdenes de
  mercado».
  - Cada informe cuesta oro del almacén (`comprarInformeRegion`), con cuándo se puede volver a comprar y cuánto queda a la vista.
  - Sale con la Taberna activa, al Rey, al Embajador o al Gobernador. Su ficha de edificio tiene «Abrir la Taberna».
  - El resto de la taberna (miradas, informes de plaza, rumores, mercenarios) llega en el paso 12: Víctor eligió la opción a) el 2026-11-16.
- **La pestaña Región del Mercado**: sale con un informe de órdenes vigente (`htmlOrdenesRegion`).
  - Enseña las órdenes activas de las plazas ajenas, con la cantidad y «Comprarle» o «Venderle», que manda la primera caravana libre
    (`tomarOrdenADistancia`).
  - **Sin el nombre de la región**: el prototipo lo saca de las regiones del relieve, y Conquest aún no las lee.
- **La prueba** es `TuComercioADistancia`: una ruta a otra plaza de la Facción con sus paradas, verla en Rutas y detenerla, proponer un
  trueque, comprar el informe de órdenes en la Taberna y abrir Región.
  - La plaza se funda con `mercadoEnPie`: ahora el Mercado y la Taberna van entre lo corto.
