# Paso 11: los ejércitos (análisis)

2026-11-16. Solo texto, sin capturas del «antes».

## Qué abarca

La hoja de ruta (bloque 4, paso 11): «Ejércitos: convocatorias, unirse, perseguir y batallas en curso» (§3.8 del inventario). Entra también
lo que quedó apuntado para aquí:
- el modo «Ejército» de «Salir al mundo», pendiente del paso 4;
- las caravanas enganchadas a un ejército (adjuntar, soltar, cargar, entregar y aparcar), con el carro 3D del paso 10 detrás del ejército.

## Lo que hay

### En BronzeAgeClient (≈1.600 líneas)

Los ficheros son `ventanaSalida.ts`, `salidaComoEjercito.ts`, `convocatoria.ts`, `ejercitos.ts`, `caravanasAdjuntas.ts`,
`unirseDesdePlaza.ts`, `panelBatalla.ts` e `interaccionAjena.ts`.

- **«Salir al mundo», «Cómo sales»**: «Columna personal» o «Ejército».
  - El ejército solo se ofrece desde tu plaza de residencia o desde un campamento.
  - Con «Ejército», se elige la política de unión: abierto (se une quien llegue) o decide el Líder (10 s). Se ve la lista «Ejércitos que se
    están preparando aquí», con «Unirme» o «Pedir unirme».
  - El botón pasa a «Convocar ejército» (`convocarEjercito`, con la tropa y la carga elegidas).
- **La preparación** (`convocatoria.ts`): con una convocatoria tuya, la ventana de salida se cambia por la del ejército en preparación.
  - A la izquierda, los integrantes, con su tropa. Al Líder le salen las peticiones, con Aceptar o Rechazar, y, si hay Caravanas de
    Fundación de la Facción en ese sitio, «Se queda aquí» o «Llevárnosla».
  - A la derecha, tu carga, con «Guardar carga» y «Descartar».
  - Abajo, tu tropa en huecos (‹ › para ordenar, ✕ y «+»), con el Liderazgo y «Guardar cambios» (`cambiarSeleccionDeConvocatoria`).
  - El Líder tiene «Cancelar la salida» (`cancelarConvocatoria`) y «Salir con el ejército» (`partirConvocatoria`, con la caravana de
    fundación si se la lleva). Los demás tienen «Separarme (sigues dentro)».
- **La ventana Ejército del mapa** (⚑ en la barra del mapa).
  - Lleva el título y el resumen (héroes, hombres y estado), los integrantes con su tropa, la carga y las caravanas enganchadas.
  - **Columna personal sola**: «Organizar ejército (abierto)» o «(decido yo)» (`organizarEjercito`, que forma en el sitio hasta tener 3).
  - **En formación**: «Se deshace en…». El Líder tiene «Cancelar la formación»; los demás, «Separarme».
  - **Ejército, Líder**: las peticiones de unión (`responderPeticionDeUnion`), «Acampar aquí» (`estacionarEjercito`) y «Ceder el mando» a
    otro integrante (`cederLiderazgo`).
  - **Ejército, no Líder**: «Separarme del ejército» (`separarseDelEjercito`).
  - El prototipo tiene además «Replegar», pero el motor ya quitó esa orden: no entra.
- **Unirse**:
  - La ficha de un ejército o una formación de tu Facción en el mapa tiene «Unirme» o «Pedir unirme» (`unirseEnCampo`) si vas sola en tu
    columna personal y estás cerca.
  - En la plaza, la entrada «Unirse a un ejército» del riel sale si un ejército de tu Facción está a su puerta. Tú resides y tienes tropa
    libre: se marcan las escuadras que aportas y se elige el ejército (`unirseAEjercito`).
  - La ficha de una batalla enseña el tipo, el estado y los dos lados, con «Unirse» (`unirseABatalla`). En una persecución, el lado: «Con
    quien persigue» o «Con el perseguido». La puerta de una plaza o un pueblo con batalla tiene «La batalla».
- **Las caravanas enganchadas** (`caravanasAdjuntas.ts`), en «Lo que llevas» y en la ventana «Caravanas de tu columna» de la puerta de una
  plaza.
  - Cada una tiene «Soltar» (`soltarCaravana`), «Cargar» de una plaza de tu Facción cercana (`cargarCaravana`) y «Entregar» por trueque
    pendiente (`entregarDeCaravana`).
  - **No hay botón para enganchar una caravana comercial**: el código de «enganchar» existe, pero ningún botón lo usa. Solo se engancha la
    de fundación al salir.
  - En el mapa, el carro va pegado detrás del ejército.
- **Lo ajeno** (`interaccionAjena.ts`): inspeccionar, perseguir, dejar de perseguir y atacar, con sus motivos de bloqueo y «⚠ Va tras tu
  columna».

### En el motor (main)

- **Las entidades**: `Ejercito`, `Convocatoria`, `PeticionUnion`, `Participante` y `SeleccionDeSalida`.
  - `Ejercito` lleva tipo, participantes, líder, política, peticiones, formación, `caravanas_adjuntas_ids` y `caravana_fundacion_id`.
  - `Convocatoria` lleva sitio, líder, política, integrantes con su selección y peticiones.
  - La batalla trae `lado_para_unirse` a quien puede unirse.
- **Las órdenes** existen todas, y sus permisos están en `comando.rs`:
  - con la cuenta del héroe: todas las de convocatoria, unión y batalla;
  - del Líder de la columna: adjuntar, soltar y aparcar;
  - de cualquier integrante: cargar y entregar.
- **Reglas que mira el motor**:
  - unirse en campo exige ir sola en una columna personal, de la misma Facción, cerca, con la política distinta de «rechazar»;
  - unirse desde la plaza exige residir allí y el ejército a su puerta;
  - partir la convocatoria sale entera o no sale;
  - una persecución exige elegir lado;
  - una columna personal no lleva caravanas.
- **`movilizar_ejercito`** sale como ejército de golpe, sin preparación. El prototipo no la usa: sale por convocatoria.
- **`replegar_ejercito`** ya no existe: cancelar la marcha es `estacionar_ejercito`.
- **Para las pruebas** hay órdenes de admin: `crear_ejercito`, que crea un ejército de un bot con su cuenta y su Facción; `dar_escuadra`,
  `dar_caravana_fundacion`, `dar_suministro`, `colocar_heroe` y `fijar_faccion_de_heroe`.

### En Conquest

- **«Salir al mundo»** (`PantallasDeMundo.cs:575`): «Cómo sales» solo trae «Columna personal», y la ayuda dice que el ejército llega más
  adelante. Se reutilizan la ventana entera (tropa en tarjetas con ✕ y «+», carga, víveres), su comprobación del Liderazgo y su orden.
- **El mapa** ya distingue Ejército, Formación y Ejército ajeno (`CosasDelMapa`).
  - Lo ajeno ya tiene Inspeccionar, Perseguir y Atacar.
  - Lo propio (ejército, formación, batalla) no tiene acciones: un clic solo marcha hasta allí.
- **La batalla** solo sigue la tuya (`MiBatalla`): no hay «Unirse» ni lee `lado_para_unirse`. `cancelarBatalla` ya sale en la preparación.
- **La carga** («Tu carga», `DejarEnPlaza` y `CargarDePlaza`) y el carro 3D (`Mapa/CarroDeCaravana.cs`), que ya está preparado para ir
  detrás de un ejército.
- **Las tarjetas y miniaturas de tropa** (`PiezasDeTropa.Tarjeta` y `Miniatura`) sirven también para la tropa de otros héroes, con los
  `tropa_ids` de la selección.
- **Las caravanas del paso 10**: la flota ya enseña «Con un ejército» para la adjunta.
- **Los textos**: faltan casi todos los `columna.*`, `convocatoria.*`, los `batalla.*` de unirse y los `caravana.*` de enganchar.

## Lo que voy a hacer

- **Calcar el prototipo** panel por panel, con sus iconos. Solo sale lo que el jugador puede hacer.
- **Salir como ejército por convocatoria**, como el prototipo: no uso `movilizar_ejercito`.
- **El carro 3D detrás del ejército**: la misma pieza del paso 10, un carro por caravana enganchada, en fila detrás de la columna y
  mirando hacia donde marcha. La caja de esas caravanas desaparece del mapa.
- **Tres tandas, con parada al acabar cada una**:
  - **Tanda 1, salir como ejército**:
    - «Cómo sales» con «Ejército», la política y la lista de convocatorias del sitio («Unirme» o «Pedir unirme»);
    - la preparación entera: integrantes, peticiones, tu tropa y carga con «Guardar», cancelar, partir con la caravana de fundación o sin
      ella, y separarse.
    - Prueba con dos héroes de la misma Facción en la misma plaza: uno convoca, el otro pide unirse y el Líder acepta y parte.
  - **Tanda 2, el ejército en el mapa**:
    - la ventana Ejército (⚑ en la barra del mapa): organizar, formación, peticiones, acampar, ceder el mando y separarse;
    - «Unirme» o «Pedir unirme» en la ficha de un ejército de tu Facción;
    - «Unirse a un ejército» en el riel de la plaza;
    - la ficha de la batalla con «Unirse» y su lado, y «La batalla» en la puerta.
    - Prueba con un ejército de un bot (`crear_ejercito`) y una batalla.
  - **Tanda 3, las caravanas del ejército**:
    - «Caravanas de tu columna» y su sitio en la ventana Ejército: soltar, cargar, entregar por trueque y aparcar (`aparcarCaravana`, que
      el prototipo no tiene);
    - el carro 3D detrás del ejército;
    - los textos de todos los rechazos.
    - **Enganchar una caravana comercial** (Víctor, 2026-11-16, duda 1 opción b): en la preparación, junto a la Caravana de Fundación,
      las caravanas comerciales libres de la plaza, cada una con «Se queda aquí» o «Llevárnosla». Si no hay ninguna libre, el bloque no
      sale. La ventana «Caravanas de tu columna» de la puerta no lo lleva.
      - En el motor, `partirConvocatoria` solo lleva la de fundación. Las comerciales se enganchan justo después de partir, con
        `adjuntarCaravana` por cada una: el Líder, el ejército recién salido en la puerta de la plaza (`puerta_de`, dentro de
        `radioPuerta`) y la caravana `disponible` con esa plaza de origen, o `aparcada` en ella. Encaja, pero no es de golpe: si un
        enganche falla, el ejército ya ha salido y se enseña el rechazo.
      - Solo desde una plaza: en un campamento no hay caravanas.
    - El animal del carro: el caballo para todos hasta tener los modelos; el animal real va en el icono de la ventana (Víctor, 2026-11-16).
    - Prueba: un ejército engancha una caravana de su plaza, carga, sale con el carro 3D detrás y la aparca en otra plaza de la Facción.
- **Pruebas** con ratón y teclado a los cuatro tamaños y capturas junto al prototipo. Al acabar cada tanda, la suite en dos lotes y la
  batalla.

## Dudas

1. **Enganchar una caravana comercial a un ejército**. El motor lo deja (`adjuntarCaravana`: el Líder, con la caravana parada en la plaza y
   el ejército a su puerta), pero el prototipo no tiene botón para hacerlo: solo engancha la de fundación al salir.
   - a) Ponerlo en la ventana «Caravanas de tu columna» de la puerta de la plaza: las caravanas libres de la plaza, con «Enganchar».
   - b) Ponerlo al salir como ejército, junto a la caravana de fundación: «Se queda aquí» o «Llevárnosla» para cada caravana de la plaza.
   - c) No ponerlo, como en el prototipo. Entonces no habría caravanas comerciales detrás del ejército, y aparcar no tendría uso.
   - Propongo a): es donde el prototipo ya pone lo de las caravanas de tu columna, y sirve igual con el ejército entrando, saliendo o de
     paso.
