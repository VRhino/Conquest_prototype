# Paso 9: mapa estratégico 2D, pueblos, aldeas y avanzadas (análisis)

2026-11-15. Víctor contestó (17:48): la avanzada por dentro, como el prototipo (duda a: hecha en el cliente según la etapa). Y en el
mapa 2D, un clic en un punto vacío traza el camino hasta allí y el héroe marcha, igual que el clic en el mundo 3D (tanda 1).
Tanda 1 hecha (a revisar): `Docs/Arquitectura/32_Mapa_Estrategico_Y_Territorio_2026-11-15.md`.
Capturas del prototipo junto a lo que hay hoy en Conquest: en la página del informe.

## Qué abarca

La hoja de ruta (bloque 4, paso 9): «Mapa estratégico, pueblos, aldeas y avanzadas» (§3.5 y §3.13 del inventario).
- §3.5: el mundo entero con zoom y arrastre, desde cualquier sitio (desde dentro de una plaza, solo para mirar); sirve para elegir
  el sitio de una aldea o de una atalaya. «Una sola textura del mundo entero con los rasters del artefacto, la niebla encima y los
  iconos con UI Toolkit. Sin teselas ni modelos 3D.»
- §3.13: las 21 órdenes de pueblos, aldeas, obras y avanzadas, y sus interiores (vista, almacén, guarnición).
- Lo apuntado para aquí: la pestaña Territorio del riel de la plaza (doc 30) y su subpestaña Fundación, que el paso 7 dejó fuera;
  los requisitos de subir de nivel ya citan «(pestaña Territorio)».

## Lo que hay

### En BronzeAgeClient (≈2.800 líneas de mapa, ≈1.700 de pueblos)

**El mapa 2D** (`main.ts:2006-2160`, `render.ts:536-1114`, `ui/pantallaMapa.ts`, `ui/geometriaVista.ts`, `ui/capasMapa.ts`).
- Se abre con «Mapa» de la barra del jugador desde cualquier pantalla, y con «Ver en el mapa» (Base, Facción → Plazas, ejércitos),
  centrado en ese punto. Esc o ✕ lo cierran.
- Cámara: del mundo entero (zoom mínimo) al diámetro de visión del héroe (máximo); empieza a 0,25 de la visión. Rueda ×1,15 hacia
  el cursor, botones +/− ×1,4; arrastrar, acotado al mundo. Abre siempre centrado en el héroe (no recupera la cámara guardada).
- Pinta, por orden: el suelo del artefacto, bosques, ríos y vados, nodos, caminos, fronteras y zonas ajenas (vistas o recordadas),
  plazas recordadas, la niebla, las capas, tu zona de influencia, plazas propias y ajenas, rutas de caravanas, pueblos, aldeas,
  avanzadas y obras, caravanas, ejércitos y batallas, campamentos de bandidos y mercenarios, alijos y la cruz del héroe.
- Capas: nombres de nodos, fertilidad de un cultivo y parcelas de bosque, con su leyenda. Sin tooltips al pasar el ratón.
- Clic sobre el mundo: lo que hay debajo (una cosa se selecciona, varias abren la lista), se cierra el mapa y la columna marcha.
  Desde una plaza, solo mirar.
- Elegir un punto: una franja arriba con el texto, la zona de la plaza rayada y, para la aldea, los sitios buenos en verde (en gris
  los fértiles pegados a algo). El clic manda la orden; si el motor la rechaza, su mensaje sale en la franja y se puede volver a
  pulsar.

**La ventana Territorio** (riel de la plaza; `ui/pueblos.ts:248-660`, `:1138-1215`; `ui/panelFundacionDePlaza.ts`).
- Subpestañas «Pueblos y aldeas» y «Fundación» (la ajena solo la primera).
- Izquierda: la zona de influencia (una tarjeta por tipo de nodo y Bosque, «N libres de T»), el cupo de pueblos y una fila de
  iconos: «+» (marcar), uno por pueblo y uno por obra; al pulsar se abre su detalle debajo.
  - Marcar: la tarjeta Aldea (cultivo, coste, anillo «de X a Y u», «🗺 Elegir el sitio») y la de Extractor (los nodos libres de la
    zona y «⛏ Marcar»), con sus textos de bloqueo.
  - Un pueblo: nivel, fertilidad, almacén, rebaño, el carro de acarreo y su escolta; «⬆ Nivel N», «🛒 Reponer carro», «✕ Destruir».
  - Una obra: los materiales con su barra o la cuenta atrás, y «✕ Cancelar».
- Derecha, Avanzadas: el cupo, una tarjeta por avanzada (tropa de serie, «⬆ Fuerte/Ciudadela», «🔧 Reparar», «✕ Destruir») y
  «🗺 Elegir el sitio de la atalaya».
- Solo gestiona el Gobernador o el Maestro de Obras, residente y dentro; los demás leen un aviso.
- Fundación: el coste contra el almacén, cinco requisitos con luz, «🏕️ Lanzar caravana» y la lista de caravanas con «✕ Desarmar».
- Fuera de la ventana: las obras en el HUD izquierdo de la plaza y, en el mapa, las fichas de obra («Entregar materiales»), pueblo y
  avanzada (Entrar, Guarnición, Atacar; la avanzada también «🌾 Llenar víveres» y «📦 Carga»).

**Por dentro** (`montarPueblo`, `montarAvanzada`, `ui/avanzadas.ts`).
- Pueblo o aldea: el mismo esqueleto que la plaza, con la vista 3D de `escenasPueblo` y un minimapa SVG. Riel: Resumen (mejorar),
  Guarnición (dejar y retirar escuadras), Acarreo y escolta, Carga (al carro / dejar).
- Avanzada: vista 3D hecha por el cliente según la etapa (atalaya, fuerte, ciudadela). Riel: Resumen, Guarnición, Almacén (dejar,
  llenar víveres), Tropa de serie.

### En el motor (01799ceb)

- Proyección: `Pueblo` (con `planta`, `trazado` y `nivel_madre` solo para quien está dentro), `Avanzada` (sin planta ni trazado),
  `ObraDeMapa`; el carro de acarreo es una `Caravana` de tipo acarreo.
- Las 21 órdenes existen. Dos notas: `entregarEnObra` ignora `materiales` y entrega lo que falta de la columna y sus caravanas;
  `cancelarObra` solo vale para avanzadas (la de un pueblo se cancela con `destruirPueblo`).
- La Vía Rápida acorta también las obras de pueblos y avanzadas. No hay orden de admin para «dar un pueblo» ni «acabar una obra»:
  el camino de prueba es fundar la plaza, Vía Rápida, `marcarAldea`, `dar_suministro` y `entregarEnObra`.

### En Conquest

- Mapa de mundo solo en 3D (`Mapa/MapaDeMundo.cs`). Ningún mapa 2D ni textura del mundo entero.
- Reutilizable: los rasters u8 del artefacto (`RasterDeMundo`; ≈667×500 celdas: terreno, fertilidad por cultivo, bosque), la niebla
  en textura RG16 (`NieblaDelMapa`, ≈534×400) y `CosasDelMapa` (reúne pueblos, aldeas, avanzadas y obras, con clic y acciones).
- Pueblos, aldeas y avanzadas ya se ven en el mapa 3D, con «Entrar» y «Atacar»; las obras, sin acciones. Dentro de uno sale el aviso
  «Estás dentro de… Su interior llega con su paso» con «salir».
- El riel de la plaza no tiene Territorio. Ninguna de las 21 órdenes, ni lanzar o desarmar la caravana de fundación.
- `InteriorDelMotor` (FeudoScene) es genérico sobre las piezas: sirve tal cual para la `planta` de un pueblo o una aldea.
- Los textos de rechazo `pueblo.*` ya están en `Textos.cs`.

## Lo que voy a hacer

- **El mapa 2D**, una ventana a pantalla completa sobre cualquier pantalla («Mapa»; «Ver en el mapa» donde ya existe): una
  textura del mundo hecha una vez con los rasters (terreno, ríos, bosque), la niebla RG16 encima con un shader de UI y los iconos de
  `CosasDelMapa` en UI Toolkit. Cámara, zoom, arrastre, capas (nombres, fertilidad, parcelas) y clic como el prototipo; desde una
  plaza, solo mirar. Abre centrado en el héroe, como el prototipo.
- **Elegir un punto** en ese mapa, con la franja, la zona rayada y los sitios de aldea en verde y gris.
- **Territorio** en el riel de la plaza, con sus dos subpestañas, calcado del prototipo, y las obras en el HUD izquierdo.
- **Las fichas del mapa 3D** ganan lo que les falta: obra («Entregar materiales», «Cancelar»), pueblo y avanzada (Guarnición,
  Llenar víveres, Carga).
- **Por dentro de un pueblo o una aldea**, en FeudoScene con su `planta`, y su riel (Resumen, Guarnición, Acarreo y escolta, Carga).
- **La avanzada por dentro**, según la duda.
- El mapa 3D sigue siendo el mundo donde anda el héroe; el 2D es la ventana «Mapa», como en el prototipo.
- La escolta del carro de acarreo va aquí; la de las caravanas del mercado, en el paso 10.

## Tandas (con parada en cada una)

1. El mapa 2D (textura, niebla, iconos, cámara, capas, clic) y el modo de elegir un punto. Territorio con Pueblos y aldeas
   (marcar los dos tipos, el detalle de pueblo y de obra, destruir, cancelar, reponer el carro) y Fundación; las obras en el HUD.
2. Las avanzadas: la columna de Territorio (colocar, mejorar, reparar, destruir, tropa de serie) y las fichas de obra, pueblo y
   avanzada en el mapa (entregar, guarnición, víveres, carga).
3. Por dentro: el pueblo y la aldea en FeudoScene, la avanzada, y sus rieles (guarnecer, retirar, cargar, dejar, escoltar).

## Pruebas

- PlayMode contra mi servidor 7891 con la Vía Rápida: fundar la plaza, marcar una aldea eligiendo el punto con el ratón, llevar el
  suministro, entregar en la obra y ver el pueblo activo; lo mismo con un extractor y con una atalaya; entrar, guarnecer y salir.
- El mapa 2D con ratón y teclado (rueda, arrastre, +/−, Esc) a los cuatro tamaños, con capturas junto al prototipo.
- EditMode: la geometría de la vista (zoom, acotar, de pantalla a mundo) y los sitios de aldea, con el `demo()` del prototipo como
  referencia.

## Duda

1. **La avanzada por dentro.** El motor no manda planta ni trazado de la avanzada.
   - a) La hago en el cliente según la etapa, como el prototipo (una torre; el fuerte con empalizada; la ciudadela con muralla),
     sobre FeudoScene. **Mi propuesta.**
   - b) Solo el HUD y el riel, sin escena (la vista del mundo detrás).
   - c) Pido al motor, en otra sesión, que mande la planta de la avanzada, y mientras tanto b).
