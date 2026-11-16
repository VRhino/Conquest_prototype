# Paso 10: el mercado y las caravanas (análisis)

2026-11-15. Solo texto, sin capturas del «antes» (Víctor, 2026-11-15).

## Qué abarca

La hoja de ruta (bloque 4, paso 10): «Mercado y caravanas» (§3.14 del inventario), con las tres órdenes que BronzeAgeClient no manda:
`cancelarOrdenMercado`, `aparcarCaravana` y `reclamarCaravana` (esta ya la manda Conquest desde el mapa, para la caravana de fundación).
Entra también la escolta de las caravanas del mercado, que salió del paso 9.

## Lo que hay

### En BronzeAgeClient (≈1.600 líneas, 27 órdenes)

- **La ventana Mercado del riel de la plaza** (`panelMercado`, 835 líneas):
  - en tu plaza, cuatro subpestañas:
    - **Órdenes**: colocar compra o venta (residentes);
    - **Caravanas**: la flota (crear, añadir carro y animal, mover un carro, reservar, renombrar, preparar y cancelar el envío), la
      escolta (Ceder y Retirar; del campamento si resides, de la columna si estás de visita; el cupo, por el nivel del Mercado), la
      descarga de una caravana aparcada y las caravanas ajenas aparcadas, con «Enviar al origen»;
    - **Rutas**: encargar, cambiar, detener y cancelar un encargo (Gobernador, Tesorero o Maestro de Caravanas);
    - **Trueques**: proponer, aceptar y rechazar;
  - en una plaza ajena, sus órdenes, con «Comerciar» en la puerta (`comerciarEnPlaza`).
- **Región** (taberna, 342 líneas): comprar el informe de la región y tomar una orden a distancia.
- **La puerta de una plaza desde el mapa**: las mismas ventanas del Mercado.
- **Las caravanas adjuntas a tu columna** (163 líneas): soltar, cargar, entregar y adjuntar.
- **La ficha de una caravana ajena**: inspeccionar, perseguir, dejar de perseguir y atacar.
- **El cupo de la flota**: del Mercado y del Albergue de caravanas (`cupoCaravanas`, `cupoCarros`), más la política `cupoCaravanaExtra`.
- **Código muerto, que no copio**: `data-carro-nuevo`, `data-cc` (enganchar) y `escoltaLiderazgo`, que nunca se rellena.

### En el motor (main)

- Las órdenes existen todas, con sus permisos (`comando.rs:890-937`):
  - el **Gobernador o el Tesorero** coloca y cancela órdenes, y propone, acepta o rechaza trueques;
  - el **Gobernador, el Tesorero o el Maestro de Caravanas** maneja los encargos y toma una orden a distancia;
  - un **residente dentro** maneja la flota;
  - **el líder de la columna** comercia en la puerta, adjunta, aparca y suelta.
- **La escolta comercial** tiene cupo 0 si la plaza no tiene Mercado. Es distinta de la del carro de acarreo, que cuenta como nivel 1.
- `aparcarCaravana` deja la caravana adjunta en una plaza de tu Facción, con la columna en la puerta o dentro.
- `cancelarOrdenMercado` deja la orden como Expirada.
- **Para las pruebas**: no hay código de admin para dar una caravana comercial, ni para construir el Mercado. El camino es el de los
  pasos 7 y 9: fundar, Vía Rápida, `dar_recursos`, construir el Mercado (y la Carpintería o el Albergue si hace falta) y `crearCaravana`.

### En Conquest

- **El mercado del campamento** (`PantallasDeMundo.Campamento.cs:240`): solo comprar. Sirve de plantilla visual (bien, cantidad, total).
- **El riel de la plaza no tiene Mercado**. Hay que añadir el caso en el título, las subpestañas y el cuerpo, y su firma de cambios.
  La ficha del edificio Mercado tendrá «Abrir el Mercado», como el Barracón y la Armería.
- **La escolta del carro de acarreo** del paso 9 (`Sitios.cs:444`) se puede reutilizar entera, cambiando:
  - sus textos;
  - el cupo, a la regla comercial.
- **En el mapa**:
  - las caravanas ajenas ya tienen Inspeccionar, Perseguir e Interceptar;
  - de las propias, solo «Reclamar» la de fundación;
  - las rutas se pintan en el mapa 2D.
- **Lo que ya está**: el cargo Maestro de Caravanas, las políticas, el enfriamiento de crear caravana (`Territorio.cs:715`) y el buey del
  molino.
- **Faltan unos 60 textos de rechazo** (`caravana.*`, `orden.*`, `mostrador.*`, `trueque.*`) y dos avisos (`mercado.orden_cancelada`,
  `ejercito.caravana_aparcada`). Además, `caravana.faccion_ajena` habla solo de la de fundación.

## Lo que voy a hacer

- Calcar la ventana Mercado del prototipo en el riel de la plaza, con sus cuatro subpestañas, la vista de una plaza ajena y la puerta
  desde el mapa.
- **Las dos órdenes nuevas**, donde el prototipo pondría su botón:
  - «✕ Cancelar» en cada orden propia;
  - «Aparcar aquí» (`aparcarCaravana`) va al paso 11: solo un ejército lleva caravanas adjuntas.
- **La escolta de las caravanas del mercado**: la misma del carro de acarreo, con el cupo del motor (0 sin Mercado).
- **Las acciones de las caravanas propias** en la ficha del mapa. Nada de enganchar caravanas a la columna del héroe: una columna
  personal no puede llevarlas, y solo se enseña lo que el jugador puede hacer.
- **El carro 3D con su animal**, en lugar de la caja de hoy, para las caravanas que viajan solas por el mundo 3D. Lleva su movimiento
  y su animación, y es una pieza reutilizable: en el paso 11 esa misma pieza seguirá al ejército que lleve caravanas.
- **Los textos** de todos los rechazos y avisos nuevos.
- **Tres tandas, con parada al acabar cada una**:
  - **Tanda 1, órdenes**: la ventana Mercado con Órdenes (colocar y cancelar), la plaza ajena con «Comerciar» en la puerta, y la puerta
    desde el mapa.
  - **Tanda 2, caravanas**: la flota entera, la escolta, las aparcadas (descargar y enviar al origen), la ficha del mapa de las propias
    y el carro 3D.
  - **Tanda 3, rutas y trueques**: Rutas (los encargos con sus paradas) y Trueques.
- **Pruebas**, una por tanda, con ratón y teclado a los cuatro tamaños, con capturas junto al prototipo, más la suite entera y la batalla.

## Respuestas de Víctor (2026-11-15, 23:42)

1. **Región**: aquí, en la tanda 3, como propuse (comprar el informe de la región y tomar una orden a distancia).
2. **La caravana adjunta, con un carro 3D detrás**: el motor lo confirma, solo un ejército lleva caravanas.
   - Las columnas personales no pueden: `columna.personal_sin_caravanas` (viaje.rs:756, fundacion.rs:349, salidas.rs:378).
   - En el paso 10, el carro 3D con su animal sustituye a la caja de las caravanas que viajan solas (tanda 2).
   - En el paso 11 (ejércitos), la misma pieza sigue al ejército, con las comerciales (`caravanas_adjuntas_ids`) y la de fundación
     (`caravana_fundacion_id`). Allí van también adjuntar, soltar, cargar, entregar y aparcar.
