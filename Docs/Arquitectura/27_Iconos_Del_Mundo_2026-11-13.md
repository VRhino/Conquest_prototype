# Los iconos del mundo (bloque 4): un registro, sacado del prototipo por script

**Por qué.** Las pantallas migradas de BronzeAgeClient llegaban solo con texto y cifras. El prototipo ya trae iconos provisionales: los
edificios, las tropas y las partes del interior dibujados a mano, los del mapa de game-icons.net y un emoji por recurso. Ahora vienen con
cada pantalla. La regla está en la Hoja (bloque 4): cada pantalla migrada trae sus iconos y piezas visuales, no solo los datos.

## La técnica

- **De dónde salen.** `Tools/IconosDelPrototipo/generar.mjs`, que se corre desde la carpeta de Conquest y necesita `../BronzeAgeClient`
  con su `node_modules`:
  - Empaqueta con esbuild `entrada.ts`, que importa los mismos módulos del prototipo: `iconoEdificio`, `iconoTropa`, `iconoDeParte`,
    `ICONOS_MAPA`, `RECURSO_ICONO` y `RECURSO_COLOR`.
  - Escribe un SVG por icono en `Assets/Resources/MundoUI/Iconos/{edificio,tropa,parte,mapa,recurso}/<id>.svg`.
  - Escribe también `iconos.json`, con la lista, los autores de los del mapa y el emoji y el color de cada recurso, y `CREDITOS.md`.
  - Nada se dibuja ni se copia a mano: si el prototipo cambia un dibujo, se vuelve a correr.
- **Emoji de los recursos.** Con `--emoji`, el script baja de Twemoji 15.1.0 (cdn.jsdelivr.net/gh/jdecked/twemoji, CC BY 4.0) el SVG del
  mismo punto de código que `RECURSO_ICONO`, el 📦 de los recursos sin emoji propio y el `LICENSE-GRAPHICS`; y los glifos sueltos (familia `glifo`) que usan las pantallas migradas: ⚔ 🚪 👁 🎁 🌾 ⛺ ☠ 📜 ⚒ 👥 🌲 〰️ 🌊 🧑 ⚠️. Los SVG bajados solo tienen
  `path`, `circle` y `ellipse`. No se usa el emoji del sistema (Segoe UI Emoji de Windows), por la licencia.
- **En Unity.** Los SVG se importan como `VectorImage` de UI Toolkit, con el módulo Vector Graphics que trae Unity 6
  (`com.unity.modules.vectorgraphics`).
  - `Assets/Scripts/Editor/IconosDelMundoImportador.cs` pone `SVGType.VectorImage` a todo lo de esa carpeta. Así los iconos son
    vectoriales y se ven nítidos a cualquier tamaño, sin texturas de 4×.
- **Teñir.** Los dibujos a mano del prototipo usan `currentColor` (el trazo) y `--relleno` (las superficies, al 25%). El script los deja
  en blanco: el trazo opaco y el relleno con `fill-opacity="0.25"`.
  - La interfaz los tiñe con `-unity-background-image-tint-color`, por defecto el `--oro` (#d8b878), igual que el color del prototipo
    según el estado.
  - Los del mapa van en blanco y se tiñen con su color.
  - Los emoji no se tiñen.
- **Genéricos.** Lo que no tiene dibujo propio lleva el genérico del prototipo, también sacado por script (`_otro`):
  - edificio: la casa de `casa(8,8,16,18)`;
  - tropa: la lanza con escudo;
  - recurso: el 📦.

## El registro: `IconosDelMundo` (`Assets/Scripts/Mundo/Mapa/IconosDelMundo.cs`)

Va en el ensamblado del mapa (`Conquest.Mundo.Mapa`) porque lo usan el mapa y las pantallas (`Conquest.Mundo.UI` ya lo referencia).
`Conquest.Mundo` no puede: no tiene referencias del motor.

| Pieza | Qué es (en el prototipo) |
|---|---|
| `Icono(familia, id, lado, tinte)` | un icono de `lado` px, teñido (bronce por defecto); sin dibujo propio, el genérico |
| `Imagen(familia, id)` | el `VectorImage` propio, o null (lo usa la prueba de cobertura, sin genérico) |
| `ConIcono(…, texto)` | icono y texto en fila |
| `ChipDeRecurso(recurso, cifra)` | `.asent-recurso`: el emoji y la cifra (en rojo con `chip-recurso--falta`, como `.obra-coste.falta`) |
| `ColorDeRecurso`, `ColorDeNodo` | `RECURSO_COLOR` y `colorNodo` (la piedra, gris) |
| `DeCosa(cosa)`, `DeNodo(recurso)` | el icono del mapa de cada cosa y su color, como el mapa 2D (`render.ts`, `ui/pueblos.ts`); los yacimientos con `ICONO_DE_NODO` |

Las clases USS van al final de `Mundo.uss`: `.con-icono`, `.chip-recurso`, `.chip`, `.carril`, `.mercado-*`,
`.excedido`, `.placa-almacen`, `.lista-fila-icono`, `.bandidos-nivel*` y `.elegir-*`.

## Qué pantalla usa qué

| Pantalla | Iconos y piezas (y de dónde salen en el prototipo) |
|---|---|
| Interior: cabecera | la tienda (`parte/campamento`, 40 px en oro; `htmlBlasonInterior` la pone a 46) junto al nombre del campamento |
| Interior: pie | la placa «Tu almacén» con el emoji y la cifra de cada recurso (`htmlPlacaDeRecursos`); se oculta si está vacío |
| Paneles del campamento | el título con su icono a 26 px (`htmlTituloDeParte`): taberna, mercado, barracón |
| Taberna (resumen) | cada escuadra con su miniatura de Conquest (`PiezasDeTropa.Miniatura`, la de `Squad_Icon`). El almacén y los edificios siguen en texto, como allí |
| Mercado | cada bien con su emoji, nombre y «N en venta · p oro/u» (`.mercado-bien`); la cabecera del bien con su emoji; «Pagas / Te queda / Tendrás» en tres cajas (`.mercado-total`); la compra hecha en verde (`.mercado-hecho`) |
| Tropa | los chips de reclutas (🧑) y oro (`.obra-chip`); los carriles con su icono a 30 (el del campamento para lo que se presta, el del edificio militar para lo que se recluta; `arbolTablero`); cada tropa que se presta o se ofrece en su tarjeta de Conquest (`PiezasDeTropa.Tarjeta`: nivel si ya es tuya, Liderazgo y hombres) con su línea y su botón debajo, y «Reclutar N» con el chip de oro (rojo si no llega); lo que aún no ofrece, en miniaturas |
| Salir al mundo | cada escuadra en su tarjeta de Conquest, elegida si sale (pulsar la tarjeta o «Sale»); cada fila de carga con su emoji (`.carro-fila`); «🌾 Llenar víveres»; la cifra del carro en rojo si no cabe (`.excedido`). Los víveres, en texto como allí |
| Mapa: nombres | delante del nombre, el icono del mapa 2D del prototipo en su color: columnas y ejércitos (`heroe`, color de facción), campamentos de mercenarios (#6fa3d6), bandidos (#d9534f) y yacimientos (`mena`, `ganado`, `piedra`, en su color). Plazas: su sigilo, que llega con el paso 8 |
| Mapa: lista bajo el clic | cada fila con su señal redonda de 26 px (`.eleccion-clic-icono`) y el símbolo del prototipo de cada cosa: ⚔ ⛺ ☠ 📜 ⚒ de Twemoji; ⧗ ⚑ ⛟ ⌂ ♜ ✦ ♛ dibujados por el script |
| Mapa: ficha | el nivel de los bandidos en tres círculos terracota (`.bandidos-nivel`); cada acción con su glifo (`.puerta-boton-icono`): 🚪 Entrar, ⚔ Atacar, 👁 Inspeccionar, 🎁 Abrir. y ➜ «Marchar aquí» (dibujado por el script) |
| Nace tu héroe: ficha | «🌾 Fertilidad», «🌲 Bosque», «〰️ Río» y «🌊 Costa o lago» (`htmlFicha`); los yacimientos con su icono del mapa en su color, a 18 px (`htmlNodos`); los bandidos con el suyo en rojo. En la lista, «👥» delante de la gente |
| Nace tu héroe: comparar | «Yacimientos a 150 u» con icono a 14 y cifra (`iconosCerca`); lo mejor de cada fila en verde sobre verde (`td.gana`) |
| Nace tu héroe: mapa | los nombres de campamentos y bandidos llevan su icono (los del mapa) |
| Acceso, partidas | el error con su ⚠️ delante (`.error-banner`). El sigilo de la partida llega con el paso 8 |
| Avisos | el prototipo no lleva iconos |

## Lo que no se ha traído, y en qué paso entra

- **Los glifos que no son emoji** (➜ ⧗ ⚑ ⛟ ⌂ ♜ ✦ ♛) no están en Twemoji ni en Inter o Cinzel, así que saldrían como cuadros. Desde el paso 5A
  los dibuja el propio script (familia `simbolo`, teñible, en 32×32). Van donde los pone el prototipo: ➜ en «Marchar aquí» y, en la
  lista bajo el clic, el de cada cosa (⧗ formación, ⚑ columnas, ⛟ caravanas, ⌂ pueblos, ♜ avanzadas, ✦ alijos, ♛ plazas). • y × están en
  las letras; ★ está en Inter.
- **Las etiquetas 3D del mapa** del prototipo llevan ⛺ y ☠ en el texto. Aquí llevan el icono del mapa 2D (la misma cosa, del mismo
  prototipo), que ya estaba.
- **La tropa del campamento en «Nace tu héroe»** (el edificio a 34 y los chips de tropa a 24): el prototipo adivina el edificio militar en el
  cliente (`edificioMilitarDe`, un hash de la posición). Es lógica, no una pieza visual. Entra con el **paso 6** (base personal y
  reclutamiento), junto con el árbol de tropas.
- **Las piezas de `juegoVisual.ts`.**
  - Barra de avance, marcas y medallón de nivel, chips de coste y medidores: **paso 7** (plaza por dentro: obras, Centro urbano,
    taberna de informes).
  - La carreta: **paso 7**, con la Caravana de Fundación y el mercado de la plaza.
  - El fondo de refundación del campamento: **paso 8**.
  - El registro ya tiene lo que necesitan: iconos, emoji y colores.

## Pruebas (`Assets/Tests/PlayMode/Mundo/IconosDelMundoTests.cs`)

- **`ElCatalogoEntero`:** cada id de `iconos.json` se carga como `VectorImage`. Ninguno sale en blanco en la captura: cada icono deja
  algún píxel distinto del fondo. Escribe `30_catalogo_de_iconos.png`.
- **`CadaCosaDeLasReglasTieneIcono`:** contra `/v1/datos` del servidor de prueba. Cada tropa de `TROPAS_RECLUTABLES` tiene su dibujo, y si
  no, falla. Además dice qué edificios (el `EDIFICIO_CATALOGO`, los militares y las piezas del campamento) y qué recursos de las reglas no
  tienen dibujo propio en el prototipo y llevan el genérico.

## Las piezas de tropa de Conquest (`PiezasDeTropa`)

**Decisión de Víctor (2026-11-13).** En las tropas, la imagen de Conquest sustituye al icono dibujado del prototipo. Es la excepción a la
regla de los iconos del prototipo, y solo para tropas: edificios, recursos, mapa y lo demás siguen con los del prototipo.

`Assets/Scripts/Mundo/UI/PiezasDeTropa.cs` (UI Toolkit) hace gemelas de las piezas UGUI del juego, sin tocarlas:

| Pieza | Gemela de | Qué lleva |
|---|---|---|
| `Miniatura(tropa, lado = 40)` | `Squad_Icon` (`SquadIconController`) | el fondo, el icono y una línea al pie del color de la rareza (1 px a 40 px) |
| `Tarjeta(tropa, nivel, liderazgo, vivos, total, elegida, ancho = 150)` | `Squad_option` (`SquadOptionUI`) | 150×236,75: el fondo y la imagen de la tropa a todo el alto; abajo, la franja oscura (28,6 px) con la línea de la rareza (4 px); a la izquierda el Liderazgo («14» y el icono de Conquest); a la derecha «vivos/total». Sin el nivel, como la original: con los tres datos juntos no se sabía cuál era cuál (Víctor, 2026-11-14); el borde tenue y, si está elegida, el borde de oro |
| `Estrellas(tropa, lado = 16)` | las de `SquadDetailPanel` | las enteras y la parcial recortada, como el `fillAmount` de Conquest, en el color de la rareza |

**De dónde salen los datos:**
- **Las imágenes y la rareza:** del `SquadData` de cada tropa del motor (`Resources/Squads/BronzeAge/<tropaId>`). Lo crea `TropasDelMotor`:
  el aspecto se copia de su base de Conquest y el escalón del motor da la rareza.
- **El color y las estrellas:** de `SquadUtils.RarityInfo`, una sola tabla.
- **Los textos, en español y del motor:**
  - el Liderazgo, de `LIDERAZGO.costePorEscalon` según el escalón de la tropa;
  - los hombres, `cantidad` sobre `unidadesPorDefecto`.
- **Cómo llega `Conquest.Mundo.UI` a ese código:** referencia a `Game.ECS`, donde están `SquadData` y `SquadUtils`. No hay ciclo,
  porque `Game.ECS` solo depende de `Conquest.Mundo`.

**Dónde va cada pieza:**
- **La miniatura:**
  - la taberna («tu tropa»);
  - la ficha de la batalla (atacan y defienden);
  - en el panel de Tropa, lo que el campamento aún no ofrece.
- **La tarjeta:** el panel de Tropa (préstamo y reclutar) y «Salir al mundo».
- **Lo que no se ha tocado:**
  - La ficha del mapa y la lista «AQUÍ HAY» no muestran tropas hoy, así que no se cambian.
  - «Nace tu héroe» aún no enseña la tropa del campamento: entra con el paso 6.
  - Los iconos de tropa dibujados del prototipo siguen en el registro (familia `tropa`, por la prueba de cobertura), pero ninguna
    pantalla los usa.
- **Las estrellas:** no hay hoy una ficha de detalle de tropa en lo migrado. La pieza está hecha y probada, y entra con el paso 6 (la
  ficha de la tropa en el barracón y en la base personal).

**Pruebas** (`Assets/Tests/PlayMode/Mundo/PiezasDeTropaTests.cs`):
- **`CadaTropaDelMotorTieneSusImagenes`:** cada SquadData del motor tiene sus tres imágenes y se encuentra por su id.
- **`CadaTropaDeLasReglasTieneSuPieza`:** cada tropa de `/v1/datos` tiene su miniatura y su tarjeta. Si falta, falla y la nombra; no
  hay reserva.
- **`LasPiezasComoLasDeConquest`:** pinta una tropa de cada rareza con los prefabs originales y con las piezas, para compararlas
  (`31_piezas_de_conquest_ugui` y `32_piezas_del_mundo`), y cuenta las estrellas de cada rareza.
- **Además,** en las pantallas a los cuatro tamaños, ninguna pieza de tropa pisa a otra ni se corta de ancho (`DentroDelMarco`).
