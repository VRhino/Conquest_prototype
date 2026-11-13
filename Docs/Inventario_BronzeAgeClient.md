# Inventario de BronzeAgeClient

Qué hay en BronzeAgeClient (`../BronzeAgeClient`, el prototipo en HTML y TypeScript) y de qué depende cada pieza, para
llevarlo a Conquest. El orden y las decisiones están en el bloque 4 de [`Hoja_De_Ruta.md`](Hoja_De_Ruta.md); aquí va el
detalle, pantalla por pantalla. Aprobado por el autor el 2026-11-13.

Hecho sobre BronzeAgeClient 0.207.0 (main ee802bc), el motor en main 4a16dab (contrato 0.24.0) y Conquest en main b169c8c1.
Las cuentas de órdenes salen de buscar cada caso de `Orden` (`proto/bronzeage/v1/orders.proto`) en el código del cliente, sin
contar comentarios ni el código generado.

## 1. De un vistazo

- Unas **43.000 líneas de TypeScript propio** (sin `src/contrato/gen`) y **6.400 de CSS** (`src/style.css`).
- Manda **180 de las 191 órdenes** del contrato (las 11 que no, en §4).
- Recibe y guarda los **32 tipos de entidad** de la sesión y usa todos los mensajes que tocan a un jugador: foto, delta,
  revocado, resultado, crónica, reloj, aviso y error.

| Bloque | Líneas | Órdenes |
|---|---:|---:|
| Conexión al motor | 4.500 | — |
| Acceso, partidas y carga | 520 | 1 |
| Crear el héroe y elegir campamento | 1.200 | 1 |
| Mapa de mundo en 3D | 7.500 | ~10 |
| Mapa estratégico en 2D | 2.100 | — |
| Plaza por dentro (vista 3D y su HUD) | 7.300 | 14 |
| Gobierno y defensa de la plaza | 1.150 | 17 |
| Columna y ejército | 1.900 | 30 |
| Héroe, base personal y armería | 1.600 | 19 |
| Reclutamiento y árbol de tropas | 990 | 4 |
| Tecnología | 720 | 1 |
| Facción, diplomacia y sigilo | 1.460 | 19 |
| Pueblos, aldeas y avanzadas | 1.700 | 27 |
| Mercado y caravanas | 1.600 | 25 |
| Campamento de mercenarios y contratos | 1.370 | 16 |
| Taberna, intel y rumores | 710 | 5 |
| Misiones y tablón | 700 | 3 |
| Aedas, épicas y crónica | 490 | 3 |
| Avisos, informes y piezas comunes | 1.350 | 2 |
| Pegamento (`main.ts`: flujo de pantallas, riel, modos) | 3.700 | 32 |

Las líneas cuentan los ficheros de cada bloque; las órdenes, las distintas que nombra (una orden puede salir en dos bloques).

## 2. Lo que ya tiene Conquest

- **El contrato en C#:** `Assets/Plugins/BronzeAge/BronzeAge.Contrato.dll` y `Google.Protobuf.dll`, generados del `.proto` del
  motor con `Tools/ContratoBronzeAge`. Hoy solo los usa el enlace con el lanzador (`Net/EnlaceLanzador.cs`, TCP local). Para la
  sesión del mundo faltan el WebSocket, zstd y el almacén de ámbitos.
- **FeudoScene:** un recinto amurallado con piezas de Synty, calles, el héroe a pie en tercera persona, NPCs con diálogo,
  minimapa, inventario, ficha del héroe, tienda, barracón y emparejamiento. Es la base de plaza, pueblo, aldea, avanzada y
  campamento.
- **Pantallas de relleno fuera de la batalla:** LoginScene, CharacterSelection, AvatarCreator, BarraconScene, BattlePrepScene,
  PostBattleScene, emparejamiento y tienda. Todas leen un guardado local (`player_save.json`), no el motor. Se borran cuando
  llega la pantalla migrada que las sustituye, salvo el creador de avatar, que se queda: `OrdenCrearHeroe` ya pide las piezas por
  id (`cabeza_id`, `pelo_id`, `barba_id`, `cejas_id`).
- **Interfaz:** todo es UGUI con TextMeshPro; no hay nada en UI Toolkit. La ficha del héroe (2.700 líneas) y el inventario
  (3.900) pueden servir de piezas si se alimentan del motor.

## 3. Pantalla por pantalla

### 3.1 Conexión al motor (4.500 líneas)

- **Qué hace:** la sesión del contrato 05: WebSocket con tramas binarias y zstd negociado, `Hola` y `Bienvenida`; foto, delta y
  revocado por ámbito; niebla parcial por tramos; la plaza de dentro con máscara de campos; `ResultadoOrden` causal; ping y reloj
  del mundo; reconexión y «sesión reemplazada» (una sesión por cuenta). El acceso por HTTP (`/v1/registro`, `/v1/sesiones`). Los
  datos de reglas de `GET /v1/datos` (unos 45 lectores: capacidades, radios, catálogos, costes). El artefacto de mundo y el
  paquete de relieve por hash, con caché. Los códigos de rechazo traducidos a frases y los nombres de héroe.
- **Ficheros:** `contrato/` (sesión, almacén, niebla, plaza, acceso), `apiCliente.ts`, `motor/`, `tiposDominio.ts`,
  `ui/erroresServidor.ts`.
- **Para Unity:** `motor/proyeccion.ts`, `motor/ordenes.ts` y `tiposDominio.ts` (3.400 líneas) son adaptadores para que los
  paneles del REST viejo siguieran andando; en Unity no hacen falta, los paneles leen los tipos del contrato y dan `Orden`
  directamente. Se portan el almacén (217 líneas, puerto del almacén de referencia del motor), la sesión, la interpolación de
  rutas al instante y los lectores de `/v1/datos`.
- **Depende de:** nada. Todo lo demás depende de esto.

### 3.2 Acceso, partidas y carga (520 líneas)

- **Qué muestra:** Entrar y Crear cuenta; la lista de partidas abiertas con tu héroe en cada una (nombre, Facción, emblema,
  nivel); la pantalla de carga entre el mundo y los interiores y al entrar.
- **Órdenes:** `cerrarSesion`; lo demás es HTTP.
- **Depende de:** conexión. Sustituye a LoginScene y CharacterSelection.

### 3.3 Crear el héroe y elegir campamento (1.200 líneas)

- **Qué muestra:** la clase obligatoria (las 5 del motor, con su equipo inicial) y el mapa del mundo con los campamentos de
  mercenarios donde se puede nacer y lo que rodea a cada uno (nodos, plazas cercanas, bandidos).
- **Órdenes:** `crearHeroe` (nombre, campamento, clase, avatar).
- **Datos:** `Bienvenida.campamentos` (`CampamentoParaElegir`), el artefacto de mundo, `CLASES_HEROE` y `OBJETOS`.
- **Depende de:** conexión y el terreno del mapa de mundo. En Unity se le suma el creador de avatar de Conquest.

### 3.4 Mapa de mundo en 3D (7.500 líneas)

- **Qué muestra:** el terreno por teselas de 64 u alrededor del héroe con el relieve de 0,5 u; mar, lagos y ríos con vados; la
  niebla de tres estados (celdas de 5 u, 534 × 400); árboles, matas y piedras; nodos de recurso; maquetas de plazas, pueblos,
  aldeas, avanzadas, obras, campamentos, bandidos, alijos y batallas; caminos, fronteras fusionadas, zona propia y anillo de los
  mercenarios; columnas propias y avistadas con sus carros, caravanas y formaciones; ruta y destino; nombres; capas (nombres de
  nodos, fertilidad). Clic en el suelo para marchar; con varias cosas bajo el clic, una lista para elegir; ficha de lo elegido
  con las acciones a la puerta y el tiempo de marcha.
- **Órdenes:** `marcharA`, `marchaForzada`, `entrarEnAsentamiento`, `entrarEnCampamento`, `entrarEnPueblo`, `entrarEnAvanzada`,
  `atacar`, `inspeccionar`, `perseguir`, `dejarDePerseguir`, `abrirAlijo`.
- **Datos:** artefacto de mundo (contrato 06) y paquete de relieve; la VISTA que se pide con `CambiarVista`; casi todas las
  entidades del almacén.
- **Para Unity:** las medidas son las buenas y están en la tabla «Valores» de `BronzeAgeClient/docs/Mundo_3D.md` (1 u = 12 m,
  copa de árbol 0,3 u, plazas al 10 % del radio de su zona, tropa ×2, cámara de 35° con inclinación de 10° a 90°, zoom por la
  visión de la columna). Las maquetas, el agua, la vegetación y el humo son relleno: en la primera pasada, cuadros y colores
  planos.
- **Depende de:** conexión.

### 3.5 Mapa estratégico en 2D (2.100 líneas)

- **Qué muestra:** el mundo entero con zoom y arrastre libres, desde cualquier sitio (también desde dentro de una plaza, solo
  para mirar). Sirve para elegir un punto: el sitio de una aldea o de una avanzada. Guarda la vista por partida.
- **Para Unity:** una sola textura del mundo entero hecha con los rasters del artefacto, la niebla de la sesión encima y los
  iconos con UI Toolkit. Sin teselas ni modelos 3D.
- **Depende de:** conexión y artefacto. Lo necesitan pueblos, aldeas y avanzadas.

### 3.6 Plaza por dentro (7.300 líneas)

- **Qué muestra:** la ciudad en 3D desde el trazado que manda el motor: calles y calzadas por nivel, murallas con torres y
  puertas, edificios por tipo y nivel, puestos de mercado, minimapa. Encima, el HUD: barra de recursos, riel de ventanas,
  Construir (rejilla de edificios y autoconstrucción), la cola de obras, el Centro urbano con la subida de nivel, la ficha de
  cada edificio con sus recetas, los tooltips con ficha.
- **Órdenes:** `anadirEdificioManualmente`, `alternarAutoConstruccion`, `mejorarEdificioAhora`, `moverEnCola`, `quitarDeCola`,
  `cancelarConstruccion`, `alternarReceta`, `elegirReceta`, `asignarBuey`, `pausarConsumo`, `solicitarAscenso`,
  `renombrarAsentamiento`.
- **Datos:** `Asentamiento` visto desde dentro (trazado, edificios, cola, almacén, cargos); catálogo de edificios y recetas.
- **Para Unity:** sobre FeudoScene, empezando por la vista de Conquest (ver la hoja de ruta). `src/ciudad3d/modelos.ts` (1.800
  líneas) son los edificios hechos a mano con geometría: relleno.
- **Depende de:** conexión y pantalla de carga.

### 3.7 Gobierno y defensa de la plaza (1.150 líneas)

- **Qué muestra:** Muralla (recintos), Puerta (a quién se cierra, vetos, aliados), Guarnición, Residencia y capital, Reserva y
  Políticas, Fundación (la caravana de fundación), protección de la plaza recién fundada, cargos locales y participación.
- **Órdenes:** `comprometerRecinto`, `mejorarRecinto`, `abandonarRecinto`, `fijarPuerta`, `vetarJugador`,
  `alternarReabastecerAliados`, `asignarGuarnicion`, `retirarGuarnicion`, `cambiarResidencia`, `dejarResidencia`,
  `designarCapital`, `activarPolitica`, `calibrarReservaManual`, `lanzarCaravanaFundacion`, `desarmarCaravanaFundacion`, `fundar`,
  `asignarCargoLocal`.
- **Depende de:** plaza por dentro.

### 3.8 Columna y ejército (1.900 líneas)

- **Qué muestra:** la ventana «Salir al mundo» (una para todos los sitios, con lo que cargas y la velocidad resultante), salir
  como columna o como ejército, «Lo que llevas» (tropa y carro), convocatorias dentro de una plaza o campamento, la ventana
  Ejército de la Facción, unirse a un ejército que pasa, batallas en curso a las que sumarse, atacar, perseguir e inspeccionar lo
  ajeno.
- **Órdenes:** `salirAlMundo`, `salirDeAsentamiento`, `salirDelCampamento`, `salirDePueblo`, `salirDeAvanzada`, `cargarDePlaza`,
  `dejarEnPlaza`, `ordenarEscuadras`, `convocarEjercito`, `unirseAConvocatoria`, `cambiarSeleccionDeConvocatoria`,
  `cancelarConvocatoria`, `partirConvocatoria`, `separarseDeConvocatoria`, `responderPeticionDeConvocatoria`, `organizarEjercito`,
  `cancelarFormacion`, `estacionarEjercito`, `cederLiderazgo`, `separarseDelEjercito`, `responderPeticionDeUnion`,
  `unirseEnCampo`, `unirseAEjercito`, `unirseABatalla`, `guarnecer`, `repostar`.
- **Para Unity:** aquí se engancha el paso a la batalla: el cliente recibe `Batalla.asignacion_propia`, entra en el servidor de
  batalla y vuelve al mundo.
- **Depende de:** mapa de mundo y un interior desde el que salir.

### 3.9 Héroe, base personal y armería (1.600 líneas)

- **Qué muestra:** la Base personal a pantalla completa: héroe con atributos, nivel y loadouts; Equipo (huecos, mochila, almacén
  de objetos); Tropas (escuadras con experiencia, formación, reponer); Carro y almacén personal. La Armería de la plaza (tienda
  y forja). Barra del jugador y estados activos.
- **Órdenes:** `repartirPuntos`, `guardarLoadout`, `borrarLoadout`, `equiparObjeto`, `desequiparObjeto`, `guardarObjeto`,
  `sacarObjeto`, `usarObjeto`, `elegirFormacion`, `reponerPrestamo`, `pasarAViveres`, `guardarEnAlmacenPersonal`,
  `sacarDelAlmacenPersonal`, `vaciarCarro`, `cerrarAlmacenACiudadanos`, `comprarObjeto`, `venderObjeto`, `forjarObjeto`,
  `configurarForja`.
- **Depende de:** conexión. Va en cualquier sitio.

### 3.10 Reclutamiento y árbol de tropas (990 líneas)

- **Qué muestra:** el árbol de reclutamiento de la plaza (carriles por familia, columnas por Era, cartas con su ficha de
  combate) y el mismo árbol en el campamento, con «Pedir prestada».
- **Órdenes:** `reclutarTropa`, `cerrarReclutamientoACiudadanos`, `reclutarEnCampamento`, `pedirPrestamo`.
- **Depende de:** plaza o campamento por dentro. Las tropas del motor en Conquest son del bloque 3.

### 3.11 Tecnología (720 líneas)

- **Qué muestra:** el árbol de tecnologías de la Facción por Eras, con requisitos y lo adoptado.
- **Órdenes:** `adoptarTecnologia`.
- **Depende de:** Facción. Comparte el tablero con el árbol de tropas.

### 3.12 Facción, diplomacia y sigilo (1.460 líneas)

- **Qué muestra:** crear Facción (con su sigilo) o pedir ingreso; el resumen con cupos y plazas; relaciones (alianza, vasallaje,
  guerra, paz, Liga), anexión y fusión; cargos (Rey, Embajador) y dejar la Facción; admitir a otras Facciones en los ataques. El
  sigilo se compone de ids (campo, orla, emblema).
- **Órdenes:** `crearFaccion`, `solicitarIngreso`, `responderSolicitud`, `proponerRelacion`, `responderRelacion`,
  `retirarRelacion`, `romperRelacion`, `declararGuerra`, `proponerPaz`, `rebelionVasallo`, `proponerAnexion`, `responderAnexion`,
  `retirarAnexion`, `proponerFusion`, `responderFusion`, `retirarFusion`, `asignarRey`, `asignarEmbajador`, `dejarFaccion`,
  `admitirOtrasFacciones`.
- **Depende de:** conexión. El sigilo también va en la batalla (estandartes).

### 3.13 Pueblos, aldeas y avanzadas (1.700 líneas)

- **Qué muestra:** desde la plaza madre: marcar un pueblo sobre un nodo o una aldea en tierra fértil, obras de mapa a las que
  llevar material, mejorar, guarnecer, escoltar, destruir. Por dentro: la vista del pueblo, la aldea o la avanzada, su almacén y
  su guarnición.
- **Órdenes:** `marcarPueblo`, `marcarAldea`, `mejorarAldea`, `entregarEnObra`, `cancelarObra`, `destruirPueblo`,
  `reponerCarroDeAcarreo`, `guarnecerPueblo`, `retirarDePueblo`, `cargarDePueblo`, `dejarEnPueblo`, `asignarEscolta`,
  `quitarEscolta`, `colocarAvanzada`, `mejorarAvanzada`, `repararAvanzada`, `destruirAvanzada`, `elegirTropaDeAvanzada`,
  `guarnecerAvanzada`, `retirarDeAvanzada`, `depositarEnAvanzada`.
- **Depende de:** plaza por dentro, mapa estratégico (para elegir el sitio) y FeudoScene para los interiores.

### 3.14 Mercado y caravanas (1.600 líneas)

- **Qué muestra:** órdenes de compra y venta de la plaza, la flota de caravanas (crear, cargar, escoltar, enviar), trueques
  entre dos plazas, encargos (rutas permanentes), caravanas adjuntas a tu columna, tomar una orden en persona desde la ficha del
  mapa.
- **Órdenes:** `colocarOrdenMercado`, `comerciarEnPlaza`, `crearCaravana`, `agregarCarroCaravana`, `comprarAnimalCaravana`,
  `moverCarroCaravana`, `prepararCaravana`, `cancelarCaravana`, `reservarCaravana`, `renombrarCaravana`,
  `enviarCaravanaAlOrigen`, `moverCargaCaravanaAparcada`, `proponerTrueque`, `aceptarTrueque`, `rechazarTrueque`,
  `encargarCaravana`, `cambiarEncargo`, `cancelarEncargo`, `detenerEncargo`, `adjuntarCaravana`, `cargarCaravana`,
  `entregarDeCaravana`, `soltarCaravana`.
- **Depende de:** plaza por dentro y mapa de mundo (las caravanas se ven fuera).

### 3.15 Campamento de mercenarios y contratos (1.370 líneas)

- **Qué muestra:** el campamento por dentro, donde nace el héroe: residir, comprar, reclutar, pedir prestado, aportar a una
  refundación, salir. El tablón de mercenarios por contrato: contratar, prorrogar, ordenar, mudar, despedir, valorar, premios de
  rango.
- **Órdenes:** `residirEnCampamento`, `comprarEnCampamento`, `reclutarEnCampamento`, `pedirPrestamo`, `repostarEnCampamento`,
  `aportarARefundacion`, `retirarDeRefundacion`, `comprarCaravanaDeRefundacion`, `contratarMercenario`, `prorrogarContrato`,
  `ordenarMercenario`, `mudarMercenario`, `despedirMercenario`, `valorarMercenario`, `reclamarPremioDeRango`.
- **Depende de:** conexión y FeudoScene. Es el primer interior que pisa un héroe nuevo. La planta del campamento aún no viaja
  en el `.proto` (antes CQ-007).

### 3.16 Taberna, intel y rumores (710 líneas)

- **Qué muestra:** la taberna regional (informes de la región, órdenes a distancia, mercenarios de la región), Miradas (un ojo
  sobre un punto del mapa), informes de plaza y rumores (vetas de estaño).
- **Órdenes:** `comprarInformeRegion`, `tomarOrdenADistancia`, `comprarMirada`, `comprarInformePlaza`, `comprarRumor`.
- **Depende de:** plaza por dentro y mapa (las Miradas se pintan en él).

### 3.17 Misiones y tablón (700 líneas)

- **Qué muestra:** el diario de misiones y el tablón de recurrentes (7 diarias y 7 semanales) con sus recompensas.
- **Órdenes:** `aceptarMision`, `elegirTablon`, `reclamarRecompensa`.
- **Depende de:** conexión; el tablón, de la plaza o la Facción.

### 3.18 Aedas, épicas y crónica (490 líneas)

- **Qué muestra:** los Aedas itinerantes en el mapa, la ventana Aedas de la plaza (tecnologías de Aeda, épicas) y la crónica
  del mundo.
- **Órdenes:** `comprarTecnologiaAeda`, `empezarEpica`, `abandonarEpica`; la crónica se pide con `PedirCronica` en la sesión.
- **Depende de:** mapa de mundo y plaza por dentro.

### 3.19 Avisos, informes y piezas comunes (1.350 líneas)

- **Qué muestra:** avisos de los eventos del delta (los urgentes arriba) y su historial; informes de combate; diálogos de
  confirmación; ayuda «ⓘ».
- **Eventos:** el núcleo emite unos 85 códigos. El cliente nombra 14 en sus avisos y recibe los importantes por el mensaje
  tipado de `EventoDominio` (batallas, conquistas, asedios, hambre, guerra, obras terminadas, héroe herido o expulsado). El
  resto no se avisa porque el cambio ya llega en el delta. En Unity se decide aviso por aviso al migrar cada pantalla.
- **Depende de:** conexión. Los avisos se necesitan desde el principio.

## 4. Las 11 órdenes que el cliente no manda

Siete no son del jugador:

| Orden | Quién la da |
|---|---|
| `conectarse`, `desconectarse` | El servidor, al abrirse y cerrarse el socket |
| `controlarReloj`, `administracion` | Administración |
| `ofrecerseComoMercenario`, `retirarOferta` | Solo bots (un humano recibe `mercenario.solo_bots`) |
| `movilizarEjercito` | Jugador, pero el cliente sale como ejército por convocatoria (`convocarEjercito` y `partirConvocatoria`) |

Cuatro sí son del jugador y **faltan en BronzeAgeClient**; en Unity entran con su pantalla:

| Orden | Qué hace | Con qué pantalla |
|---|---|---|
| `cancelarOrdenMercado` | Retira una orden de compra o venta de la plaza | Mercado |
| `aparcarCaravana` | Deja la caravana adjunta aparcada en una plaza de tu Facción | Caravanas |
| `reclamarCaravana` | Toma la Caravana de Fundación que vuelve sola a su origen | Mapa de mundo |
| `cancelarBatalla` | Quien inició una batalla se echa atrás antes de que empiece | Paso a la batalla |

## 5. Lo que no se migra

- Los bancos de `pruebas/`.
- `src/terreno/` (copia del generador del backend viejo; solo quedan en uso sus tipos).
- La ruta `#/legacy` de `render.ts`.
- Los adaptadores al REST viejo (§3.1).
- El repintado por sondeo (`ui/repintado.ts`) y los ganchos de `main.ts`: en Unity se repinta al llegar el delta.
- El panel de rendimiento (F3): lo cubre el profiler.

La guía visual «bronce y arcilla» (`docs/Diseno_Interfaz.md` y `style.css`) sí se lleva, como estilo de la interfaz.

## 6. Recursos que no son código

- 12 imágenes de las pantallas de carga (`public/carga`).
- Los catálogos del sigilo: campos, orlas, emblemas e insignias (`src/sigilo/*.json`).
- Emblemas e iconos del mapa de game-icons.net (CC BY 3.0; autores en `src/sigilo/CREDITOS.md`, que hay que llevar con ellos).
- Iconos de edificios y de tropas dibujados a mano (provisionales).
