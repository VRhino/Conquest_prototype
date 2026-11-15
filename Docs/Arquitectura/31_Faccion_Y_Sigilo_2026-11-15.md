# 31. La Facción, la tecnología, el sigilo y los estandartes (paso 8 del bloque 4)

2026-11-15. El plan, las tandas y las dudas contestadas: `tasks/bloque4-paso8-plan.md`.

## Tanda 1: el sigilo y crear tu Facción (a revisar)

### El sigilo

- **Los datos**: ocho ids del catálogo (`Sigilo` del contrato: forma, campo, emblema, colores primario, secundario y del emblema, orla y
  su color).
- **Los ficheros**: el catálogo, los emblemas y las insignias son copia de los del prototipo, en `Resources/MundoUI/Sigilo/`
  (`catalogoSigilos.json`, `emblemas.json` e `insignias.json`). Los emblemas son de game-icons.net (CC BY 3.0), con sus autores en
  `CREDITOS.txt` y en el «Créditos» del selector.
- **`Sigilos`** (`Assets/Scripts/Mundo/Sigilos.cs`, C# puro): el mismo SVG que `svgSigilo` del prototipo, en un lienzo de 100×120.
  - Por capas: la silueta recortada, los 18 campos, las 6 orlas, el contorno, el emblema con su halo de contraste y la corona del Gran Rey.
  - Además: el sigilo de una Facción (de su entidad o del directorio de la `VISTA`), su color primario, la clave por sus ids, el neutro y
    uno al azar.
  - Dos cambios frente al prototipo, que no se ven en el dibujo: el `#0008` va como negro con opacidad, y con la corona todo baja 30 en
    lugar de usar un origen negativo, que Unity no admite.
- **`TexturaDeSigilo`** (`Assets/Scripts/UI/`): lee el SVG con el paquete de gráficos vectoriales de Unity (`SVGParser`,
  `TessellateScene`, `BuildSprite` sobre el lienzo entero, `RenderSpriteToTexture2D`) y lo deja en una `Texture2D` guardada por sus ids
  y su tamaño.
  - En espacio de color lineal, los colores se devuelven a los del SVG.
  - La misma textura vale para UI Toolkit, para la UI de siempre (`SpriteDe`) y, en la tanda 4, para la tela de los estandartes.
- **Dónde sale**:
  - el blasón del HUD de la plaza (64 px, con la corona si es la del Gran Rey);
  - los carteles del mapa: 40 px en las plazas y 26 en las columnas de una Facción (`CosasEnElMapa.SigiloDe`);
  - el selector de personaje: el escudo en el hueco `Icon` de su ficha;
  - la lista de Facciones (56 px las completas, 30 px las del directorio);
  - la vista previa de «Crear facción» (88 px).
- **El color de Facción** (`CosasDelMapa.ColorDeFaccion(almacén, id)`) es el primario de su sigilo (`faccionColor`). Si aún no se conoce
  el sigilo, sigue la paleta de reserva. Pinta zonas, fronteras, rutas y los bordes de los carteles.

### La ventana de Facción sin Facción

- **La bandera «Facción»** va en la barra del jugador, junto al yelmo de la Base, como en el prototipo.
  - Sale solo si el héroe no tiene Facción: con ella, su ventana llega en la tanda 2.
  - Abre la ventana a pantalla completa (`PantallasDeMundo.Faccion.cs`, hoja `Faccion.uss`). Esc la cierra.
  - Abrirla cierra la Base, y con ella abierta el héroe no anda.
- **«Elige tu facción»**: dos tarjetas, «↗ Pedir ingreso» y «✦ Crear facción».
- **«Crear facción»**:
  - El nombre (32 letras) y el selector de sigilo («no se podrá cambiar»), con la vista previa y los 8 desplegables. Empieza con uno al
    azar.
  - Los motivos de `motivosParaCrearFaccion` salen debajo en cuanto se toca algo, y «Crear facción» se apaga mientras haya alguno. Los
    motivos: nombre vacío o repetido, una pieza que no es del catálogo, el fondo de un solo color, un sigilo idéntico a otro.
  - Manda `CrearFaccion` con el sigilo.
- **«Pedir ingreso»**:
  - El buscador, «Ocultar las de bots» (marcado de entrada) y la lista.
  - Las Facciones completas: sigilo, nombre, datos, miembros y plazas.
  - «Sin contacto»: las del directorio, con el escudo pequeño.
  - Cada fila lleva «Pedir ingreso» (`SolicitarIngreso`), «Pedido: decide su Rey» o «🤖 De bots».
- **Las Facciones del mundo** llegan por la `VISTA`. Dentro de un interior al que se entró sin pasar por el mapa, aún no se ha pedido:
  la ventana la pide como el mapa (`Sesion.TieneVista`).

### Pruebas

- **`SigilosTests.CadaFormaCampoYOrlaSePinta`**: 18 muestras (todas las formas, los campos y las orlas), más el neutro y una con la
  corona. Con capturas, guarda el PNG y su SVG. Comparado con el mismo SVG en Chrome: iguales.
- **`PantallasDeMundoTests.TuFaccionSinFaccion`** (ratón y teclado reales):
  - Otra cuenta crea «Esparta» con un sigilo.
  - Un héroe sin Facción abre la bandera, la cierra con Esc y la vuelve a abrir.
  - Pide ingreso a Esparta, que sale con su sigilo, y el motor apunta la solicitud.
  - Crear: sin nombre, «Crear» está apagado; con el fondo de un solo color, se apaga y sale su motivo.
  - Crea su Facción con un sigilo de forma torre y campo cuartelado, y el motor la tiene con ese sigilo.
  - La ventana se cierra y la bandera se va.
- **`PantallasDeMundoTests.ElSelectorConElSigilo`**: una cuenta con Facción; el selector enseña su escudo y el nombre de la Facción.
- **`TuPlazaPorDentro`**: el escudo del blasón es el sigilo.
- **`CosasDelMapaTests.ElColorDeUnaFaccionEsElPrimarioDeSuSigilo`**: el color sale del sigilo, y sin sigilo la paleta.
- **`LaBasePersonal`**: con la Base abierta, la barra tiene la Base, la Facción (sin Facción) y los Avisos.

## Tanda 4: diplomacia, estandartes y opciones (a revisar)

- **Antes de la tanda** (Víctor):
  - La ficha corta de una tropa en la plaza trae «Edificios y pueblos», «En campaña» y «Combate» (`PantallasDeMundo.FichaDeTropa.cs`), como
    `tipTropa` y `htmlFichaCombate`:
    - el nivel de plaza, los edificios a su nivel y los pueblos del equipo;
    - poder, velocidad y liderazgo;
    - el tier con su rareza, las cifras sueltas y la tabla por tipo de daño (las filas a cero no salen).
  - Todo sale del balance (`TROPAS_RECLUTABLES[..].combate`).
- **El motor** de pruebas está en 01799ceb y el contrato se regeneró desde ese clon (`actualizar.ps1 -Proto ../BronzeAgeMotorPruebas/proto`).
  Ahora `BattleSide.sigilo` y `BattleParticipantSnapshot.sigilo` vienen siempre.
- **La pestaña Diplomacia** (`PantallasDeMundo.Diplomacia.cs`), entre Tecnología y Ajustes del Rey, calcada de `pestanaFaccion.ts`,
  `panelDiplomacia.ts`, `panelAnexion.ts` y `panelFusion.ts`:
  - **La lista, a la derecha:**
    - la tuya;
    - «Conocidas», con contacto: tu Liga, relación o una plaza suya que conoces;
    - «Sin contacto».
    - Las dos secciones se pliegan y tienen «Ocultar las de bots». Cada fila lleva el sigilo, el nombre y la relación o «Nivel N · M miembros».
  - **La tuya, en el centro:**
    - tus relaciones, con «Romper alianza», «Liberar vasallo», «Rebelarse» y «Ofrecer» o «Aceptar la paz»;
    - las propuestas recibidas (Aceptar y Rechazar, el Rey) y las enviadas (Retirar);
    - la Anexión y la Fusión, con su formulario;
    - la Liga, con el Gran Rey.
  - **Otra Facción, en el centro:**
    - su cabecera con las insignias y sus datos (nivel, miembros, plazas conocidas, reputación, Rey, Embajador, capital, Liga, ¿bot?);
    - lo vuestro y las propuestas activas;
    - «Nueva propuesta»: una cláusula, que puede ser alianza, vasallaje con tributo (recurso y cantidad por minuto), anexión, fusión (nombre
      y Rey) o «Declarar la guerra»;
    - las plazas suyas que conoces.
  - **Las órdenes** del contrato son 13: proponer, responder y retirar relación, anexión y fusión; romper; rebelarse; declarar la guerra y
    proponer la paz.
    - Llevan las confirmaciones y los textos del prototipo.
    - Lo que no se puede hacer no sale. Proponer y retirar es del Rey o del Embajador; responder y la fusión, solo del Rey.
  - **Iconos:** los del prototipo. En la barra de propuestas, 🤝 `glifo/alianza`, 👑 `glifo/nobleza`, 🏛 `glifo/templo`, 🔗 `glifo/fusion` y ⚔
    `glifo/atacar`; en «Ocultar las de bots», 🤖 `glifo/bots`. 🔗 y 🤖 son de Twemoji, bajados con permiso de Víctor (`generar.mjs --emoji`).
- **Los estandartes en la batalla** (`Assets/Scripts/Visual/Estandarte.cs`):
  - **El sigilo de cada héroe** es el del ticket: el suyo, o el del bando si no lo trae. El de cada bando va en `EstandartesDeBatalla`.
    - Lo ponen `BatallaRealDeTicket.Montar` e `Incorporar`, y queda en `BattleHeroData.sigilo`.
    - En la entidad del héroe va como `EstandarteComponent` (`BattleSceneController`).
    - Los bandidos llevan `bandidos`, y quien no tiene Facción, `neutro`, como manda el motor.
  - **En red:**
    - `VisualRed` lleva el sigilo y el tipo de estandarte: el del héroe, o el del abanderado de escuadra.
    - El abanderado es el primer soldado vivo de la escuadra; si cae, pasa al siguiente (`EspejoSystem.Abanderados`).
    - `PartidaRed` lleva los sigilos de los dos bandos, para las zonas.
    - El cliente los copia a `EstandarteComponent` y a `EstandartesDeBatalla` (`EspejoClienteSystem.Estandartes`).
    - El servidor de pruebas (`Builds/PruebaRed`) se reconstruyó con esto.
  - **`EstandarteVisual`** va en cada `EntityVisualSync`, solo con gráficos.
    - El héroe lleva siempre su estandarte; el abanderado lleva el de la escuadra si la opción está encendida.
    - Va a la espalda, de pie, siguiendo el pecho. Fuera de red, el abanderado sale del `SquadUnitElement`.
  - **La pieza** es la bandera de las zonas de Synty con su asta, en `Resources/Batalla/Estandarte.prefab`, a 0,3. Los «Battle Banner» de Synty
    llevan la tela en el atlas y no admiten textura.
    - La tela se compone una vez por sigilo: la franja de muestras de Synty, el color primario y el sigilo en medio.
    - Va en una copia del material (`TelaDeSigilo`).
  - **Las banderas de las zonas de captura** (`BanderaDeZona`, puesta por `CapturePointSetup`) llevan el sigilo del bando que tiene la zona
    (`teamOwner`; sin dueño, el neutro).
- **La ventana «Opciones»** (Víctor):
  - Sale de la primera entrada del menú ☰ de la barra del mapa (`PantallasDeMundo.Opciones.cs`).
  - Pinta secciones y filas declaradas en un único servicio, `OpcionesDelJuego` (`Assets/Scripts/Core`), que guarda en PlayerPrefs y que leen
    los sistemas.
  - La primera opción es «Estandarte de las tropas», encendida por defecto. Esc y «Cerrar» cierran la ventana.
  - La batalla no tiene menú de pausa: Esc solo cierra sus paneles (`FullscreenPanelManager`). Así que allí no sale.
- **Pruebas**:
  - **`TuDiplomaciaDelRey`**, con dos Reyes, ratón y teclado:
    - Elige la otra Facción y le propone una alianza, que se retira.
    - Le declara la guerra (con su confirmación) y ofrece la paz. La otra la ofrece también y la guerra acaba.
    - La otra propone un vasallaje con 5 de trigo por minuto. El Rey lo acepta desde lo suyo (con su confirmación) y sale «Eres vasalla de …»
      con «Rebelarse» y el tributo.
    - Capturas a los cuatro tamaños.
  - **`LaBasePersonal`**: «Opciones» va la primera del menú. La opción, encendida por defecto, se apaga con el ratón y queda en PlayerPrefs.
    Esc la cierra, al volver sigue apagada y se enciende otra vez. Capturas a los cuatro tamaños.
  - **`DelMapaALaBatalla`**:
    - El estandarte del héroe lleva su sigilo; hay abanderados y banderas en las zonas.
    - Con la opción apagada desaparecen los de escuadra y siguen los de los héroes y las banderas de las zonas; al encenderla, vuelven.
    - Fotos de cerca del estandarte y de una bandera de zona.
  - **`TuTecnologiaDelRey`**: la ficha corta trae las cuatro secciones y ningún id de recurso.

## Tanda 3: la tecnología (a revisar)

- **El modelo** (`ArbolDeTecnologias`, en `Conquest.Mundo`): `construirGrafo`, `disponer`, `estadoDe` y las Eras que se descubren
  (`ultimaEraVisible`, `cupoEra`) de `arbolTecnologias.ts`, portados tal cual. Los requisitos salen de `ArbolDeTropas`, que ahora los da
  hacia fuera (`RequisitosDeTecnologia`, `LogroDeTecnologia`, lo que abre cada tecnología) y lleva también el nivel de plaza.
  - Carriles por tema (Gobierno y obras, Infantería, Metalurgia, Proyectiles, Caballería, Ingeniería y asedio, Otras) por Eras.
  - Las líneas son la lista fija del prototipo (`DependeDe`) o las tecnologías del hito.
  - Se ve hasta la Era siguiente a la que tiene adoptado el 30 % (con las de arranque).
  - **`ArbolDeTecnologiasTests`** compara la disposición con la del prototipo sobre los mismos datos:
    - columna, fila, carril, Era y previas de cada carta, y columnas y filas por carril, con las Eras I, III y V a la vista;
    - el esperado lo sacó el código del prototipo, empaquetado con esbuild.
- **El tablero común** (`TableroComun` en `PantallasDeMundo.Reclutamiento.cs`, `htmlTablero` y `montarTablero` de `arbolTablero.ts`).
  - Lo usan el árbol de tropas y el de tecnologías.
  - Coloca las cartas, traza las líneas, ilumina el camino, se arrastra y deja fijas las cabeceras.
  - Las cartas de alto variable (las tecnologías) y la cabecera de Era se miden, como las filas `auto` del prototipo.
- **La pestaña Tecnología** de tu Facción (`PantallasDeMundo.Tecnologia.cs`), entre Plazas y Ajustes del Rey:
  - Los mandos: «Ver todas las conexiones» y la ayuda; la leyenda, con los estados que hay.
  - La cabecera de cada Era: su duración y «Adoptar cada una» con la tarifa (`TARIFA_ADOPCION`). En la última visible, «La Era N aparece
    con X de Y adoptadas: llevas Z», con su barra. Además, «Era del mundo» o «Bloqueada».
  - Cada carta lleva:
    - el nombre y la medalla (✓ adoptada, ! adoptable, ♪ revelada, ✦ logro);
    - lo que abre en iconos (tropas, edificios y niveles, recetas, bonus, efecto, metal del héroe y canon);
    - «Pide X» si viene de otro carril, «De arranque» si es de arranque;
    - el estado: «Adoptada», «ADOPTAR» con la tarifa, «Revelada · hito a/b», «Logro del mundo ✓ · falta tu hito», «Bloqueada: la Era N
      aún no empieza», o la barra del logro «x / y».
  - Al pasar el ratón, la ficha corta (`tipTec`):
    - el carril, la Era y el estado en tu Facción;
    - los requisitos: «Servidor», con el logro del mundo en barras, y «Facción», con el hito (con ✓ y ✗ si la reveló un Aeda) y lo que
      piden esos edificios;
    - «Abre».
  - Al pulsar, la ficha fija con sus acciones (`fichaAcciones`): solo al Rey y con la tecnología aparecida.
    - Fuera de la capital, «Para adoptarla: tienes que estar en la capital, X».
    - Dentro, «Adoptar» con la tarifa del almacén de la capital, apagado si no llega (`AdoptarTecnologia`).
    - Esc o un clic fuera la cierran; Esc otra vez cierra la ventana.
  - Debajo, «Logros del mundo cumplidos», con su ⓘ.
  - La ficha sigue abierta al repintar, y la corta también, porque los contadores del mundo suben y repintan a menudo.
- **En el árbol de tropas:**
  - La carta «adoptable» tiene ahora su color (verde) y la medalla «!».
  - Su franja lleva la etiqueta «ADOPTAR» del prototipo con el nombre de la tecnología. Aquí la etiqueta abre la pestaña Tecnología de tu
    Facción con la ficha fija de esa tecnología (duda 7).
  - La ficha corta (en la plaza y en el campamento) trae «Tecnologías de tu Facción»: lo que pide la tropa, con ✓ adoptada, ! aparecida y
    ✗ falta, su Era y por qué la pide. Esto quita el «Sin Facción ni tecnologías» del campamento.
  - El árbol se sigue viendo entero, como decidió Víctor en el paso 6.
- **Pruebas**:
  - **`TuTecnologiaDelRey`**, con ratón y teclado reales:
    - El Rey funda su plaza (la capital, con la Vía Rápida), y `fijar_tecnologias` dos veces deja los Escudos ligeros aparecidos sin
      adoptar.
    - En el árbol de la plaza, los Lanceros de mimbre salen adoptables con «!» y la etiqueta; su ficha corta dice «Solo falta adoptar» y
      trae las tecnologías de tu Facción.
    - La etiqueta abre la pestaña Tecnología con la ficha fija y «Adoptar», a los cuatro tamaños.
    - Esc cierra la ficha y no la ventana. Luego, la ficha corta de otra carta y «Ver todas las conexiones».
    - Con la ficha otra vez, «Adoptar»: el motor la tiene adoptada, la carta pasa a adoptada y aparece la Era II.
    - En el árbol de tropas, los Lanceros dejan de ser adoptables.
  - **`TuFaccionDelRey`**: ahora son cuatro pestañas.
  - Capturas del prototipo: `tasks/pruebas/cdp/tecnologia.mjs` y `reclutamiento.mjs`.
- **Arreglos de la revisión de Víctor (15-11, 16:14)**:
  - **Nombres de recurso donde el prototipo pone icono.** Se repasaron todas las llamadas que escriben un recurso por su nombre en las
    pantallas de los pasos 1 a 8, comparadas una a una con el prototipo.
    - **La raíz:** `CosasDelMapa.NombresDeRecurso` no tenía 15 recursos fabricados de `RECURSO_NOMBRE` (armas, armaduras, chatarras, el
      cuero de calidad y el carro de guerra). Salía el id con mayúscula («ArmaMadera»). Ya están.
    - **La ficha corta de una tropa en la plaza:** «La escuadra» pasa a «Por soldado», como `tipTropa`. Cada pieza del equipo lleva su
      icono y su cifra, y de dónde sale: el edificio y su nivel, la tecnología y lo que consume, o la Leñera y la Granja como fichas. Debajo,
      el oro con su icono y «1 campesino libre de la plaza» con el suyo.
    - **El resto:**
      - las «Tecnologías de tu Facción» del árbol de tropas: el «para» con los iconos del recurso y del edificio;
      - el oro por soldado del campamento, con su icono;
      - el consumo pausado, con el icono delante de cada bien;
      - la receta de la bodega y la prensa, con el icono del insumo.
    - Lo demás ya escribe el nombre, como el prototipo: el almacén de la taberna, el mercado, el Fondo, el informe y los títulos.
  - **El contraste de los costes en los botones dorados:** cada coste va en su píldora oscura (`.ta-ficha-boton .obra-coste` del
    prototipo). Vale para «Reclutar N» y las obras de la plaza (`.plaza-coste`) y para «Adoptar» (`.tec-rec`). Los de «Reponer» y del
    campamento ya la llevaban (`.chip-recurso`).

## Tanda 2: tu Facción (a revisar)

- **La bandera** sale siempre. Con Facción abre la ventana de tu Facción (`PantallasDeMundo.FaccionPropia.cs`); al crear una, la ventana pasa
  a ella.
- **El lateral** (`htmlLateralFaccion`), de arriba abajo:
  - el sigilo grande con el medallón de su nivel (con la corona si es la del Gran Rey), el nombre y «Tu facción»;
  - la experiencia con su barra (`NIVEL_FACCION.xpParaNivel`);
  - la franja: miembros, prestigio con su explicación y reputación;
  - lo que da el prestigio (alianzas activas de las que caben, estatuas, Maravilla) y lo próximo que abre (`PRESTIGIO`);
  - el Rey y el Embajador, «Hay héroes que piden entrar» (al Rey) y la Liga;
  - los cupos: 🏛 las plazas y caravanas de fundación contra su tope, ② a ⑤ por nivel, y lo que abre el nivel siguiente
    (`CAP_FUNDACION_POR_NIVEL`, `CUPO_NIVEL_ASENTAMIENTO`);
  - los títulos del servidor, con su insignia (`insignias.json`) y el sigilo pequeño de quien lo tiene;
  - «Dejar la Facción», con su confirmación.
- **Las pestañas** que ya hay: Miembros, Plazas y Ajustes del Rey. Las solicitudes pendientes salen en Miembros, solo al Rey. Tecnología
  (tanda 3) y Diplomacia (tanda 4) llegan con su tanda; Ejércitos, Tablón, Misiones y Mercenarios, con su paso.
  - **Miembros**:
    - «Piden entrar», con Aceptar y Denegar (`ResponderSolicitud`);
    - los cargos, solo al Rey: designar Embajador (`AsignarEmbajador`) y traspasar el trono con su confirmación (`AsignarRey`), sin los
      mercenarios con marca;
    - la lista de miembros por participación, con sus cargos y dónde residen.
  - **Plazas**: las de tu Facción, primero la capital, con su nivel («rinde como N»), dónde está y los residentes. Pulsando la fila se
    despliega: el almacén, la población, las obras y la guarnición.
  - **Ajustes del Rey**: «Ataques abiertos a otras Facciones» (`AdmitirOtrasFacciones`). Si no eres el Rey, la nota y el estado.
- **La Base**: bajo el nombre del héroe, el sigilo pequeño de su Facción y sus cargos («Rey», «Gobernador de Tirinto»…), solo si los tiene.
- **El Fondo de refundación** (`PantallasDeMundo.Fondo.cs`; pendiente del paso 4): la taberna del campamento tiene dos subpestañas,
  «Resumen | Fondo», si tienes Facción y ninguna plaza suya a la vista.
  - Arriba, el precio de la caravana (`derivados.costo_refundacion`), con lo reunido contra el total y una fila por recurso.
  - Para mover recursos: el recurso, del almacén personal (si resides) o del carro; la cantidad; «Lo que falta»; «Aportar» y «Retirar»
    (en milésimas).
  - «Comprar caravana» se enciende con el precio completo; si no, el aviso de lo que falta. Con la caravana comprada, «Caravana de
    Fundación lista».
- **Lo que falta o cambia**:
  - Los iconos 🏆 ⚖️ 🤝 🏛 🗿 del lateral son de Twemoji (con permiso de Víctor), bajados con `generar.mjs --emoji` (`glifo/prestigio`,
    `reputacion`, `alianza`, `templo` y `estatua`).
  - «Ver en el mapa» de Plazas no sale, porque la cámara del mapa sigue siempre al héroe.
- **Pruebas**:
  - **`TuFaccionDelRey`**, con dos cuentas y ratón y teclado reales:
    - Otra cuenta pide entrar en la Facción del Rey.
    - El Rey abre la bandera: el lateral está entero y salen las tres pestañas.
    - Acepta la solicitud (el motor la hace ciudadana) y la nombra Embajador (el motor y el lateral lo tienen).
    - Plazas está vacía; en Ajustes admite a otras Facciones.
    - Esc cierra la ventana; en la Base salen el sigilo y «Rey».
    - En la taberna, la subpestaña Fondo: aporta 10 de madera del almacén y el motor la tiene en el fondo de su Facción.
  - **`TuFaccionSinFaccion`**: al crear la Facción, la ventana pasa a la tuya.
  - **`TuPlazaPorDentro`**:
    - si el trazado no deja un edificio aislado, elige el que tiene más hueco alrededor (le pasaba con algún trazado);
    - si el sitio no deja cerrar el anillo de muralla (`murallas.no_se_puede_cerrar` al fundar), no espera muralla.
