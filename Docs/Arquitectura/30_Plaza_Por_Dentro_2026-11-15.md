# 30. Tu plaza por dentro (2026-11-15)

Paso 7 del bloque 4 de [`Hoja_De_Ruta.md`](../Hoja_De_Ruta.md) (inventario §3.6, §3.7 y la armería de §3.9). Va en tres tandas con
parada; el análisis y las dudas, en `tasks/bloque4-paso7-plan.md`. Víctor contestó «Continua» (2026-11-15): las 13 dudas van como se
propusieron. El minimapa queda para más adelante: su hueco del HUD se deja vacío.

## Tanda 1: entrar en tu plaza y su HUD

### Cuándo se entra

Como el campamento (doc 26): el mapa mira cada 250 ms dónde está el héroe. Si está dentro de una plaza de **su Facción** y ya llegó su
interior (ámbito `ASENTAMIENTO:as-N`, con `trazado` y edificios), se abre FeudoScene encima con la plaza montada. La plaza ajena, de
visita, es de la tanda 3: hasta entonces sigue el aviso del mapa («Estás dentro de…»).

**El almacén del cliente, arreglado.** La plaza viaja en dos ámbitos: la ficha propia de la Facción (`FACCION`, sin trazado ni recintos)
y su interior (`ASENTAMIENTO`). `Almacen.Buscar` devolvía la primera que encontraba, y salía la ficha sin trazado. Ahora, para una plaza
o un campamento, manda su interior si está (`ASENTAMIENTO:<id>`, `CAMPAMENTO:<id>`); si no, lo de siempre.

### La plaza en 3D (`InteriorDelMotor`)

El mismo montaje del campamento, desde lo que manda el motor, en cuadros:

- **Edificios** (`Asentamiento.edificios` de dentro; los de mapa no): centro y huella en unidades locales, el color del plano del
  prototipo (`EDIFICIO_COLOR`, en `Mundo/Edificios.cs`) y un alto por tipo. Lo que en el prototipo es suelo (plaza, plaza de armas,
  parque, granja) va plano. **En cola**, una losa gris; **en obra**, una caja gris al 45 % del alto, con su cartel «Obra · 5 h 19 min»
  (o «Mejora», «Reparación») encima, como la vista 3D del prototipo.
- **Calles y caminos**: `trazado.calles` y `trazado.caminos`, planos.
- **Muralla**: celda a celda (`recintos[].celdas`, de `unidades_por_celda`). Muro de 4 m, torre de 6 m y algo más ancha, la puerta como
  hueco. El color, por nivel: empalizada de madera, adobe y ciclópea. Lo comprometido y sin levantar (las celdas después de `avance`),
  una zanja plana. Con una mejora en obra, las primeras `mejoradas` celdas ya llevan el nivel nuevo.
- **La puerta**: la de la muralla más de fuera; sin muralla, el extremo del camino que más se aleja del centro. El héroe aparece en ella
  y la zona «Salir al mundo» queda justo fuera, hacia donde mira la puerta.
- **Cuando cambia la planta** (una obra acaba, algo entra o sale de la cola, la muralla avanza), se vuelve a montar en el mismo sitio.
- **El edificio elegido** (su ficha abierta) se tiñe de oro, como el contorno dorado del prototipo, en su copia (`MaterialPropertyBlock`):
  el material compartido no se toca.
- **Las zonas** (F): una por edificio. En una plaza los edificios están juntos y las zonas se pisan; manda la del edificio cuya huella
  queda más cerca del héroe (antes, el centro más cercano: al lado de una casa pequeña ganaba la casa aunque estuvieras pegado al otro).

### El HUD (`PantallasDeMundo.Plaza`, `Plaza.uxml`, `Plaza.uss`)

Calcado de `montarAsentamiento` y `hudPlaza.ts`:

| Sitio | Pieza |
|---|---|
| Arriba a la izquierda | **Blasón**: escudo de la Facción (su color; el sigilo llega con el paso 8), nombre con su lápiz si resides, Facción, «rinde como N», medallón del nivel (abre «Subir de nivel»; si el Gobernador ya puede pedirla, brilla con su «!»), la casa y la corona con sus tarjetas. Debajo, la **subida en curso** y las **obras**: las 4 que antes acaban con su barra, lo que falta y ✕; la **cola** en casillas numeradas con ◀ ✕ ▶ al pasar el ratón (con cargo) |
| Arriba al centro | **Población** (total con el techo y cada clase con la raya de lo que cabe, el rombo de lo que pide el nivel siguiente, el tramo rojo, la flecha y la Vivienda con su «+»), **Almacén** (la barra automática: hasta 8, en el orden en que llegan) y **Estado** (mantenimiento y nutrición en verde, ámbar o rojo). Cada pieza, con su letrero al pasar el ratón |
| Arriba a la derecha | La barra del jugador (Base, Avisos y el menú) |
| Al borde derecho | La **ficha del edificio** |
| Abajo | **«Salir al mundo»** a la izquierda y el **riel** en el centro. El hueco del minimapa, vacío |

- **La ficha** (con F junto al edificio; `htmlFicha`): nivel y estado, sus notas (dañado, parado por almacén lleno), lo que suma un
  Albergue, la obra en marcha con su barra, las recetas de su nivel, la receta de la bodega o la prensa (con «Hacer cerveza»… si cabe:
  la misma cuenta que el motor, `recetas_unicas`), el buey del molino («Comprar el buey») y, con cargo, «Mejorar a N2» con su coste
  (en rojo lo que falta) y su tiempo, «Quitar de la cola» o «Cancelar la obra». Lo que el cliente ya sabe que no se puede (nivel de
  plaza, edificio o tecnología que pide la mejora) no sale. La producción por edificio no está: el motor no la manda por edificio.
- **«Subir de nivel»** (el medallón; `htmlAscensoPlaza`): los dos escudos, los pilotos de los requisitos, el tiempo de obra y el botón
  (solo el Gobernador y con todo cumplido); el aviso de solvencia (`insolvencia_ascenso`), que no bloquea; los requisitos desplegados
  con sus barras. Durante la obra, su avance.
- **Las tarjetas del blasón**: «Resides aquí» (tus cargos y «Dejar», con su confirmación) o «Hacerla mi base»; la capital («Designar»
  si eres el Rey; sin Palacio, apagado con el porqué).
- **Cambiar el nombre**: el lápiz, el campo, ✓ (o Intro) y ✕ (o Esc).
- **Los rechazos** salen flotando, con su frase. «Sin cambios» no es un rechazo y no avisa.
- **El riel**: solo lo que se puede abrir. Construcción y Gobierno llegan en la tanda 2; Defensa, Reclutamiento y la Armería, en la 3;
  lo de otros pasos, en el suyo. En esta tanda no tiene botones y no sale.
- **«Salir al mundo»**: la misma ventana del campamento (doc 26), para la plaza. De tu residencia, la tropa de tu campamento (la de
  guarnición no sale) y la carga del almacén de la plaza (`salirAlMundo`); los víveres, solo la línea, como el prototipo. De otra
  plaza, «Salir y retomar mi columna» (`salirDeAsentamiento`). También andando hasta la puerta.
- **Con la ficha, «Subir de nivel» o el nombre abiertos**, el héroe no anda y el ratón queda libre, como con un panel del campamento.
  Esc recoge primero lo que cuelga del blasón, luego la ventana y luego la ficha.
- **Cada parte del HUD se rehace solo cuando cambia lo suyo** (el blasón, las obras, las placas, la ficha, la ventana): así el nombre a
  medio escribir o la cola con el ratón encima no se pierden en cada paso de minuto. Las cuentas atrás, en cada vuelta.
- **El tamaño**: lo que el prototipo mide con `clamp()` y la ventana (la columna izquierda, la barra de arriba entre el blasón y los
  botones del jugador, la ventana bajo la barra, el riel centrado en lo que deja el minimapa) se coloca al cambiar de tamaño
  (`AjustarPlaza`). Si la barra de arriba no cabe, el Almacén va en dos filas y, más estrecha aún, detrás de Estado.

### Iconos

La casa y la corona del blasón son las del prototipo (`CASA` y `CORONA` de `hudPlaza.ts`), sacadas por el script de iconos
(`simbolo/residencia`, `simbolo/capital`). Los emoji que no hay en Twemoji ya bajado van con el más parecido de los que hay: 👑 de la
nobleza y de la capital, la corona ♛; 🛠️ del mantenimiento, ⚒; ⏳, el reloj ⧗. Si se quieren los de verdad, hay que bajarlos (con
permiso).

### Lo arreglado de paso

- **`SquadDetailPanel.instantiate3DModel`** (duda 12): apagaba `EntityVisualSync` y cambiaba la capa del **prefab compartido** antes de
  instanciarlo. Ahora la copia nace bajo un padre apagado (sin `Awake` ni `OnEnable`), se le hace eso a ella y pasa al contenedor. El
  prefab no se toca.

### Lo que el motor hace a su manera

- **◀ ▶ en la cola** intercambian la prioridad con la vecina (`mover_en_cola`). Lo que se pone a mano entra con la misma prioridad
  (900): entre dos así, ▶ no cambia nada (el motor contesta «sin cambios»).
- **«Mejorar»** pide una cuadrilla libre: con todas en obras, el motor lo rechaza («no queda cuadrilla libre»). El botón sale igual,
  como en el prototipo.

### Pruebas

- **Regla de las pruebas con plaza (Víctor, 2026-11-15): la Vía Rápida desde el principio.** Nada más fundar la plaza y antes de encolar
  nada, su héroe se nombra Maestro de Obras y activa la Vía Rápida de Construcción (`ConViaRapida`, que llama `FundarPlaza`: toda prueba
  que funde o use una plaza pasa por ahí). En el motor de prueba (cc1e5ec o posterior) deja toda obra en el 1 % de su tiempo, con un mínimo
  de un minuto: edificios, mejoras, cada celda de muralla y la subida de nivel. Como toda política dura 150 minutos de juego (dos minutos y
  medio con el reloj deprisa), `ConViaRapidaHasta` la vuelve a activar mientras se espera algo largo (la muralla, el Barracón). Con la Vía
  Rápida, la muralla se levanta deprisa y se lleva los recursos: la prueba los vuelve a dar antes de cada tanda de obras, y la segunda tanda
  (lo largo) se pone ya con el reloj a su paso, para que quede en obra o en la cola mientras dura la prueba.
- **`PantallasDeMundoTests.TuPlazaPorDentro`** (PlayMode, contra el motor cc1e5ec, ratón y teclado reales). El motor de prueba no tiene
  orden de administración para crear una plaza (duda 13): un héroe nuevo la funda como un jugador (crea su Facción, sale con una
  caravana de fundación, marcha y funda; el bosque denso no deja y se prueba en otro sitio). La caravana dada por la administración va con
  su columna mientras la columna exista y se pierde cuando se disuelve, por ejemplo al entrar el héroe en un asentamiento (motor:
  `crates/nucleo/src/nucleo/fundacion.rs`). Por eso, en un servidor ya usado, un intento puede quedarse sin ella («fundacion.sin_caravana»):
  la prueba le da otra si la columna ya no la lleva. El servidor de prueba la sube a nivel 3 y
  le da recursos; sin auto-construcción, el héroe pone obras: lo corto acaba con el reloj deprisa y lo largo queda en obra y en la
  cola. Luego:
  1. Entra por el selector: la plaza montada (una zona por edificio y la de la puerta), solo la cámara del interior, el HUD con su nombre,
     su nivel, las placas, las obras y la cola, la casa, la corona y el lápiz, y sin riel. Los cuatro tamaños.
  2. Anda hasta un edificio sin otro pegado, el cartel «F · …» y F: su ficha, con el héroe quieto.
  3. El molino: «Comprar el buey» y lo tiene. La bodega: otra receta y la hace.
  4. Alt suelta el ratón. La cola: al pasar el ratón, ◀ ✕ ▶; ▶; ✕ la quita y la planta 3D se rehace sin ella.
  5. Cancelar una obra desde las obras, con su confirmación.
  6. La curtiduría: su ficha en los cuatro tamaños y «Mejorar a N2»: la mejora en la ficha, en las obras y su cartel sobre el edificio.
  7. El medallón: «Subir de nivel» en los cuatro tamaños (aún no se cumple: el botón apagado). Esc la cierra.
  8. La casa y la corona, con sus tarjetas («Designar» apagado sin Palacio).
  9. El nombre: lápiz, «Tirinto», ✓, y el motor lo tiene.
  10. «Salir al mundo»: su ventana en los cuatro tamaños y, al salir, el héroe fuera.
- **Sin verificar contra el motor**: pedir la subida de nivel aceptada (la plaza de prueba no cumple los requisitos), «Dejar» la
  residencia, «Hacerla mi base» y «Designar» la capital (se ven, no se pulsan) y la muralla (una plaza recién fundada no tiene).

## Tanda 2: construir y gobernar

Víctor (2026-11-15): «sigue con la siguiente parte», sin parar en las capturas de los iconos. Calcado de `main.ts` («LAS VENTANAS DE LA PLAZA»),
`panelObras.ts`, `panelCentro.ts`, `participacion.ts`, `panelTesoreria.ts`, `hudPlaza.ts`, `vistaCiudad.ts`, `consumoPausado.ts` y `panelCarro.ts`
(`PantallasDeMundo.PlazaGobierno`, el USS al final de `Plaza.uss`).

### Los iconos

- 👑 🛠️ ⏳ (nobleza y capital, mantenimiento, el reloj de las obras) son ya los de Twemoji 15.1.0 (`glifo/nobleza`, `glifo/mantenimiento`,
  `glifo/espera`), bajados con permiso de Víctor por el script de iconos; sus créditos, con los demás de Twemoji.
- Los botones del riel (Construcción, Gobierno…) son los dibujos del prototipo (`ICONO_RIEL` de `hudPlaza.ts`), sacados por el script (`riel/`).
- El candado (🔒 del prototipo) se dibuja en el script (`simbolo/candado`). Los demás emoji de esta tanda que no están bajados van con el más
  parecido de los que hay: 🏛️ del Gobernador, el Palacio; 🔨, ⚒; 🐂, 🐄; 🔥 del Sacerdote, la estatua; 🥇🥈🥉, el puesto en un círculo de oro,
  plata y bronce; 🧳, la tienda; 👪, 👥; 🧱, la muralla; 🗺️, la bandera; ⏸, ❚❚.

### El riel

Solo lo que se puede abrir (`ventanaDisponible`): **Construcción** con cargo de Gobernador o Maestro de Obras (con el «!» si no cabe más gente o
con las obras en marcha) y **Gobierno**. Al pasar el ratón, su nombre encima. Defensa, Reclutamiento y la Armería llegan en la tanda 3.

### Construcción: el cajón en el sitio del riel

- «← Volver», «Construidos N», el título y la **auto-construcción** (`alternarAutoConstruccion`).
- Las pestañas por tipo (Ciudad, Recursos, Comida, Talleres, Militar, Gobierno y Otros), cada una con cuántos se pueden pagar ya; sin «Todos».
- Una casilla por edificio con su icono, su nombre y su coste (en rojo lo que falta; con más de dos recursos, «+N»), y al pasar el ratón el
  letrero con el coste y el tiempo (con las políticas en vigor). Un clic lo pone en la cola (`anadirEdificioManualmente`).
- Lo que el cliente sabe que no se puede (único ya levantado, nivel de plaza, tecnología, edificio que pide, prestigio de la Maravilla y las
  estatuas, tope de bodegas y prensas, la Gran Fundición fuera de la capital) no sale. Lo demás (sitio, topes de almacenes y viviendas, cola
  llena) lo dice el motor al pedirlo.
- **Construidos** sube encima del cajón con su ancho: los edificios en pie por tipo (×N y el nivel más alto); un clic elige uno de ese tipo
  (el siguiente con otro clic), lo resalta y abre su ficha.
- El «+» de la Vivienda en la placa de Población, con cargo, abre Construcción en Ciudad con la Vivienda marcada.

### Gobierno

- **Cargos**: una silla por cargo (Gobernador, Maestro de Obras, Tesorero, Maestro de Caravanas, General, Sacerdote) con su emblema y su titular
  o «Vacante». Quien puede nombrar (el Rey al Gobernador, el Gobernador al resto) ve el lápiz (＋ en la vacante): el combo con los ciudadanos (sin
  los mercenarios con marca) y ✓ (`asignarCargoLocal`). Debajo, los residentes por participación, como un ranking, y quien ha sumado sin residir.
- **Políticas**: cada cargo con sus slots, lo que tiene en vigor (con lo que le queda) y, si es el tuyo, las cartas que puede activar con sus
  efectos en chips (verde favorece, rojo castiga; `activarPolitica`). El Gobernador, las de cualquier pool: las de los demás, plegadas.

### El Almacén (un clic en su placa)

A pantalla completa, de la barra de arriba al riel. Dos subpestañas:
- **Almacén**: a la izquierda, **qué va en la barra de arriba** (hasta 8; la elección se guarda en el equipo y vale para todas tus plazas;
  «Que vaya sola» la borra; «Mostrar todos»), el **almacén** con el neto por minuto de cada recurso, lo que hay frente a lo que cabe y lo que entra
  frente a lo que sale, la **extracción** (mano de obra y extractores), la **reserva** del Tesorero (`calibrarReservaManual`, una orden por
  recurso) y **pausar el consumo** (`pausarConsumo`); a la derecha, los **talleres** con ▶ / ❚❚ (`alternarReceta`). Desde 1000 px de ancho, en dos
  columnas; más estrecho, una debajo de otra.
- **Tu carga**: lo tuyo (el carro, «Todo al almacén personal», el almacén personal) frente al almacén de la plaza, para pasar de uno a otro
  (`guardarEnAlmacenPersonal`, `sacarDelAlmacenPersonal`, `dejarEnPlaza`, `cargarDePlaza`; del almacén personal a la plaza, por el carro). Al
  Gobernador, «Cerrar el almacén a los ciudadanos» (`cerrarAlmacenACiudadanos`). Los rechazos, en la línea de error, como el prototipo.

### Pruebas

`TuPlazaPorDentro` sigue con la tanda 2 antes de «Salir al mundo» (`PantallasDeMundoTests.PlazaGobierno`): el riel; el cajón en los cuatro
tamaños, la pestaña Militar, poner algo en la cola (lo primero que el motor acepta), la auto-construcción encendida y apagada, «Construidos» y la
ficha de uno; Gobierno en los cuatro tamaños, nombrarse Tesorero y activar una política; el Almacén en los cuatro tamaños, quitar un recurso de la
barra y «Que vaya sola», guardar una reserva, pausar y reanudar el pan, parar y reanudar una receta; y «Tu carga» en los cuatro tamaños, madera de
la plaza al carro y de vuelta, y cerrar y abrir el almacén.

### Arreglos de paso (tanda 2)

- **El clic perdido en el cajón**: con ciertas proporciones de pantalla, «Salir al mundo» (más ancho en Conquest que los 210 px que le
  reserva el prototipo) tapaba el primer edificio del cajón y se llevaba el clic. El cajón empieza ahora después del botón.
- **El HUD ya no se rehace en cada paso de minuto si no cambia lo que enseña** (el cajón por su firma: lo que se puede pagar, la pestaña,
  lo construido); las ventanas que sí cambian (el Almacén) conservan el desplazamiento; y con el botón del ratón pulsado no se rehace nada,
  para que un pulsar y soltar no caiga a caballo de una reconstrucción.

## Tanda 3: defender, reclutar, la Armería y la plaza ajena

Víctor (2026-11-15): «vamos con lo siguiente». Calcado de `panelGuarnicion.ts`, `panelMuralla.ts`, `panelPuerta.ts`, `panelAliados.ts`,
`reclutamiento.ts` (con `lugarPlaza` de `arbolReclutamiento.ts`) y `armeria.ts` (`PantallasDeMundo.PlazaDefensa`, el USS al final de
`Plaza.uss`).

### El riel

En el orden del prototipo: Construcción, Gobierno, **Defensa**, **Reclutamiento** y la **Armería** (con una Armería activa). De visita en
una plaza ajena, solo la Armería. Territorio, Taberna, Mercado, Unirse a un ejército y Aedas, en su paso.

### Defensa

- **Guarnición**: tu cupo (por héroe, en Liderazgo) con su barra; tus escuadras en la guarnición con ✕ para sacarlas (al campamento si
  resides; a tu columna si las dejaste de visita) y las de los demás con su dueño; «+» con las que puedes dejar (residente, las del
  campamento; de visita, las de tu columna), apagadas las que no caben (`asignarGuarnicion`, `retirarGuarnicion`). Sin Barracón ni
  Galería de tiro el cupo es 0 y se dice.
- **Muralla** (desde el nivel de la empalizada o con recintos): el plano celda a celda (en pie, la que se levanta, lo que falta, puertas y
  torres), una tarjeta por anillo (material, puertas, torres, celdas, el multiplicador de defensa, el avance, la celda en curso con su
  cuenta atrás o «parada», lo que queda y la tarifa por celda; mejorar al material siguiente o abandonar, con confirmación) y los tres
  anillos para trazar el siguiente (`comprometerRecinto`, `mejorarRecinto`, `abandonarRecinto`). Las cifras salen de `MURALLA` (nivel de
  plaza por anillo, minutos y tarifa por celda, con `factorTiempoMuralla` de las políticas). El prototipo cuenta `avance` como el índice de
  la última celda en pie (una de más); aquí, como el contrato: las celdas levantadas.
- **Puerta**: el portón con su estado; una casilla por grupo (neutrales, aliados, enemigos, Aedas) que se abre o se cierra en un borrador y
  «Fijar la puerta» (el Gobernador o el Rey; `fijarPuerta`); los vetados con ✕ y «Vetar» con los ciudadanos y los héroes a la vista
  (`vetarJugador`, solo el Gobernador); y **reabastecer a los aliados** con su interruptor (`alternarReabastecerAliados`, el Gobernador o
  el Tesorero).

### Reclutamiento

El mismo árbol del campamento (paso 6, tanda 3), entero, en su misma ventana: el «lugar» del árbol vale ahora también para una plaza
(`PantallasDeMundo.Reclutamiento` generalizado; el estado de la tanda 3 del paso 6 queda copiado en `tasks/paso6-tanda3/`). En la plaza:

- el primer carril es el **Centro Urbano**, con todas sus tropas, y los demás por su edificio;
- el estado de cada tropa: **lista**, **tuya**, falta el **edificio** a su nivel, falta **adoptar** su tecnología (sin el botón «Adoptar»,
  que es del paso 8), faltan **tecnologías** o su **Era** no ha empezado, con las tecnologías de tu Facción que ya llegan;
- en la ficha, «Reclutar N» con su coste del almacén de la plaza (en rojo lo que falta), la gente libre de la que sale y, a quien no
  reside, cómo recluta aquí (a su columna, o solo reponer si la plaza lo cerró) (`reclutarTropa`);
- arriba, la gente libre de la plaza y, al Gobernador o al General residentes, «Cerrar el reclutamiento a los ciudadanos»
  (`cerrarReclutamientoACiudadanos`).

### La Armería (duda 6)

A pantalla completa, con la estructura y las órdenes del prototipo: a la izquierda la **mochila** en casillas, con las fichas de objeto
de la Base (paso 6, tanda 2) al pasar el ratón y, al elegir una, lo que pagan por ella y «Vender» (`venderObjeto`); a la derecha:

- **Tienda**: armas, armaduras y monturas hasta `tiendaHastaTier`, con el metal que abre la Facción de la plaza, filtradas por tipo y
  «solo lo de tu clase», cada una con su precio y «Comprar» o lo que te falta (`comprarObjeto`);
- **Forja** (si está abierta a tu grupo): las piezas con receta del metal abierto y del tier que abrió la Era, con el plano, los crisoles,
  los materiales a mano y el impuesto (en rojo lo que falta) y «Forjar» o lo que falta (`forjarObjeto`);
- **Abrir la forja** (el Tesorero o el Gobernador, residentes y dentro): el fondo de la plaza y, por grupo de fuera, abierta o cerrada y
  su recargo, con «Guardar» (`configurarForja`).

Con F junto al Barracón, la Galería de tiro, las Caballerizas o la Armería, su ficha lleva el botón de su ventana (duda 5).

### La plaza ajena, de visita

El motor manda el interior de una plaza a los de su Facción por `ASENTAMIENTO`; a quien está dentro de una ajena, por la `VISTA`, «desde
dentro»: su trazado, sus edificios en pie y en obra, la muralla y la forja; nada de su almacén, su población, sus cargos o su cola. Así se
monta igual, con lo que llega: el blasón (sin lápiz, casa ni corona, y el nivel solo para ver), sin obras ni placas, la Armería en el riel
(sin abrir la forja) y «Salir y retomar mi columna».

**Arreglado de paso**: el mapa solo pedía la `VISTA` cuando ya sabía dónde estaba el héroe, y dentro de una plaza ajena ese sitio llega
precisamente por la `VISTA`: no llegaba nunca. Ahora se pide en cuanto el mapa está cargado (`MapaDeMundo`).

### Los iconos de esta tanda

Los emoji que no hay bajados van con el más parecido de los que hay: 🕊️ de los neutrales, la chincheta 📍; 🤝 de los aliados, 👥; 🚪 de las
puertas, la de los botones; 🗼 de las torres, la torre ♜; 🛡 del bono, el escudo; 🌾 de reabastecer, la espiga; 📜 del plano de forja, el
pergamino; 🏺 de los crisoles, el del aceite (el mismo 🏺); 💰 de las monedas, la bolsa.

### Pruebas

- `TuPlazaPorDentro` sigue con la tanda 3 (`PantallasDeMundoTests.PlazaDefensa`): el riel entero; Defensa en los cuatro tamaños (la
  guarnición; abandonar el anillo de muralla en obra con su confirmación y trazarlo otra vez; abrir la puerta a los neutrales y fijarla;
  abrir el almacén a los aliados); el árbol de la plaza en los cuatro tamaños y reclutar los granjeros del Centro Urbano; la Armería
  desde la ficha de su edificio en los cuatro tamaños: comprar una pieza y venderla, la Forja y abrirla a los aliados.
- `TuPlazaAjenaDeVisita`: otra cuenta, sin Facción, sale de su campamento, marcha hasta la plaza (con la puerta abierta a los neutrales)
  y entra: la plaza montada con lo que llega, el HUD de visita en los cuatro tamaños, la Armería de visita y «Salir y retomar mi columna».
- **Verificado contra el motor, con la Vía Rápida**:
  - El Barracón, puesto en la cola desde el cajón (pestaña Militar), en pie en segundos: el cupo de guarnición pasa de 0 a 17.
  - La guarnición con el ratón: «+», dejar una escuadra de leñadores (escalón 1: 7 de Liderazgo; se la da `dar_escuadra`), sacarla con ✕ y
    dejarla otra vez.
  - La muralla de nivel 1 levantada entera (51 de 51 celdas) y su plano en Defensa.
  - Vetar a un ciudadano de tu Facción (el héroe de otra cuenta, hecho ciudadano con `fijar_faccion_de_heroe`) y levantarle el veto.
  - Reclutar una tropa de pago en la plaza: la milicia de lanceros, 50 de madera. La prueba la recluta antes que los granjeros, que se
    quedan sin pesants libres.
- **Arreglado al probarlo**: «Salir al mundo» desde tu residencia llevaba también lo que dejaste en la guarnición, y el motor lo rechazaba
  por Liderazgo. Ahora se queda en la plaza, como en `ventanaSalida.ts` (`!enGuarnicion`).
- **Sin verificar contra el motor**:
  - Subir de nivel de verdad: el nivel 4 pide 2000 pesants, 1600 artesanos, la Sala del Consejo y un recinto completo de nivel 2, y no hay
    orden de administración para la población.
  - Forjar: no hay orden de administración para planos ni crisoles.
  - «Reponer» no es de la plaza: es de la Base (paso 6), y su prueba lo pulsa solo si el campamento ya recluta esa tropa (en un mundo
    recién hecho, no).
