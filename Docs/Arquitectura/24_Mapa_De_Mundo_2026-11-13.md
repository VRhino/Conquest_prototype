# 24. El mapa de mundo con cuadros (2026-11-13)

Paso 2 del bloque 4 de [`Hoja_De_Ruta.md`](../Hoja_De_Ruta.md). El mapa de mundo de BronzeAgeClient (`docs/Mundo_3D.md`, `src/mundo3d/`)
pasa a Conquest con relleno de cuadros y colores planos, con sus medidas, velocidades y tamaños; los modelos propios vienen después.
No se porta su arte de relleno (maquetas con geometría, olas, especies de árbol, matas, humo, pájaros).

## Dónde está

| Pieza | Dónde | Qué |
|---|---|---|
| `Relieve`, `RasterDeMundo` | `Conquest.Mundo` (C# puro) | El paquete de relieve por teselas (predicción y zstd) y los rasters del artefacto |
| `CamaraDelMapa` | `Conquest.Mundo` | La cámara (encuadre, zooms, inclinación, ladera) y el rayo del clic contra el relieve |
| `MapaDeMundo` | `Conquest.Mundo.Mapa` | El mapa: suelo, agua, ríos, niebla, cámara, entrada, tu columna, ruta y destino, `marcharA` y `marchaForzada` |
| `TerrenoDelMapa`, `RiosDelMapa`, `NieblaDelMapa` | `Conquest.Mundo.Mapa` | El suelo por teselas, los ríos y vados, la textura de la niebla |
| `EscalaDelMapa`, `PinturaDelMapa` | `Conquest.Mundo.Mapa` | La escala y los colores y tamaños, en un solo sitio |
| `MapaColor.shader` | `Assets/Resources/MapaMundo` | Color de vértice, luz y niebla de guerra con sus tres estados (URP) |

`Conquest.Mundo.Mapa` es un asmdef propio: el mapa es 3D pero es interfaz de fuera de la batalla, así que no va con `Conquest.Net` ni con el
ECS de la batalla, y tampoco dentro de `Conquest.Mundo`, que es C# puro y se prueba sin Unity. `Conquest.Mundo.UI` lo monta
(`PantallasDeMundo.Mapa()`): al entrar en la partida con héroe sale el mapa; la pantalla de la sesión viva queda como diagnóstico con F2.

## Escala

Una unidad de Unity es un metro. Una unidad del mapa (u) son `factor_unity` metros (12, del manifiesto del artefacto; `EscalaDelMapa`), y la
altura del relieve, que ya viene en metros, va tal cual, sin exagerar. Ejes: x = x del mapa, y = altura, z = −y del mapa (la fila 0 del mapa
es el norte; con z = y saldría en espejo). El mapa entero (el mundo de prueba de 2026-11) mide 5.334 × 4.000 u, 64 × 48 km: la precisión de `float` sigue por debajo del centímetro.

## Valores

De BronzeAgeClient (`docs/Mundo_3D.md`, tabla «Valores», y `src/mundo3d/config.ts`):

| | Valor |
|---|---|
| Apertura de la cámara | 35° |
| Inclinación | libre, de 10° a 90°; arranca a 30° |
| En pantalla | normal = la visión de la columna; alejado = 1,5 veces; de cerca = 0,6 del normal |
| Visión | `Movimiento_Ejercitos.VISION` de `/v1/datos`: `jugadorSolo` (15) o, con soldados en la columna, `ejercito` (30) |
| Suavizado | zoom 7/s; sube 14/s y baja 3/s sobre las laderas |
| Suelo | teselas de 64 u a menos de 7 veces la distancia de la cámara (tope 320 u); paso de 0,5 u a menos de 80 u, 1 u a menos de 160, 2 u más allá; dos por fotograma, las cercanas primero |
| Agua | lo que el relieve marca como agua: el mar a su nivel (`Generacion_Del_Mundo.ELEVACION.umbralAgua` de `/v1/datos`, 375 m), los lagos tal cual (ya vienen planos); honda si cubre más de 30 m |
| Ríos | cintas de 3 u (navegables) o 1,5 u sobre el relieve; vados de grava de orilla a orilla |
| Niebla | la de la VISTA (celdas de 5 u): nunca visto 206/255, recordado 158/255, a la vista 0, sobre el color de fondo de BronzeAgeClient |
| Entrada | arrastrar gira (0,006 rad/px) e inclina (0,25°/px); un clic es un arrastre de 5 px o menos; la rueda, un paso de zoom (no más de uno cada 0,28 s); WASD o flechas giran (1,4 rad/s) e inclinan (40°/s) |

Los colores van en sRGB en `PinturaDelMapa` y se pasan a lineal al pintar (el proyecto trabaja en espacio lineal).

## Decisiones

- **La VISTA se pide como en BronzeAgeClient:** el mundo entero, una vez, en cuanto hay héroe; el servidor ya la filtra por la niebla.
- **Salir del campamento con un clic:** el servidor aún no acepta `salirDelCampamento` con destino (`objetivo.no_soportado`): el clic desde
  dentro sale primero y luego marcha (dos órdenes).
- **Sin colisionador:** el clic es un rayo contra el relieve (`CamaraDelMapa.TocarRelieve`), como en BronzeAgeClient.

## Rendimiento

Con el mapa cargado y la columna marchando (prueba de PlayMode, 300 fotogramas, editor en batch): 65 teselas, fotograma mediano de 0,5 ms,
p95 de 0,6 ms y el peor de 31 ms, el de construir teselas nuevas. Si ese pico molesta, la construcción de la malla pasa a un hilo.

## Pruebas

- `Assets/Tests/EditMode/Mundo/MapaTests.cs`: encuadres y zooms, el encuadre desde arriba y en inclinación, la cámara sobre una ladera, el
  rayo del clic y el decodificado de una tesela del relieve.
- `Assets/Tests/PlayMode/Mundo/PantallasDeMundoTests.cs`: tras nacer el héroe, entra en el mapa, espera al suelo y a la VISTA, hace un clic
  (sale del campamento y marcha), comprueba que la columna avanza, pone la marcha forzada y mide los fotogramas. Con `MUNDO_CAPTURAS`
  guarda lo que ve la cámara.

## Tanda 2B: todo lo demás, la lista, la ficha y los interiores

| Pieza | Dónde | Qué |
|---|---|---|
| `CosasDelMapa` | `Conquest.Mundo` | Reúne del almacén lo que se pinta (plazas a la vista y recordadas, pueblos y aldeas, avanzadas, obras, campamentos de mercenarios y de bandidos, alijos, batallas, columnas y formaciones de tu Facción y ajenas, caravanas, Aedas de camino y los nodos del artefacto) con los tamaños de BronzeAgeClient (`huellas.ts`: maqueta 0,8, campamentos y alijos ×2, pueblos y avanzadas ×1,6, nodos ×0,5); las rayas (caminos, zonas de Facción tal cual llegan, fronteras de cada plaza, anillo de los mercenarios y Miradas); qué hay bajo el clic; y las acciones a la puerta |
| `TiempoDeMarcha` | `Conquest.Mundo` | A* sobre el grafo de navegación del artefacto y la velocidad de la columna (`TROPAS_RECLUTABLES.velocidad` o `velocidadJugador`, ×marcha forzada) |
| `CosasEnElMapa` | `Conquest.Mundo.Mapa` | Cajas instanciadas (`Graphics.RenderMeshInstanced`, color por instancia), cintas pegadas al relieve para las rayas, el aro de lo seleccionado y los nombres (UI Toolkit, colocados cada fotograma) |
| `PantallasDeMundo.Mapa()` | `Conquest.Mundo.UI` | La lista bajo el clic, la ficha abajo a la derecha, el aviso de «estás dentro» con su salida y los botones de capas |

- **El clic** (BronzeAgeClient: `resolverClic`): sin nada debajo, se quita la selección y se marcha; con una cosa, se elige (y se marcha hacia
  ella si es una plaza, un campamento, una batalla, una columna de tu Facción, un alijo, un pueblo o una avanzada); con dos o más, la lista.
  La zona de clic de cada cosa es su huella más 0,12 u (columnas 0,8; caravanas 0,7; Aedas 0,5), y como poco 13 px de radio. Mientras la lista
  está abierta, un clic en el mapa solo la cierra. A diferencia de BronzeAgeClient, que solo mira el centro más cercano, aquí la lista sale
  con todo lo que tiene el punto en su zona.
- **La ficha**: el nombre, qué es, la distancia y el tiempo de marcha (en tiempo real con el ritmo del reloj), «Marchar aquí» y las acciones
  a la puerta. **Solo sale lo que se puede hacer** (regla del autor): entrar (plaza propia o tu residencia, campamento de mercenarios,
  pueblo y avanzada propios) a menos del radio de puerta; atacar a menos del radio de encuentro, sin estar herido ni junto a un campamento de
  mercenarios (una plaza, solo con un ejército con soldados); inspeccionar a menos del radio de inspección; perseguir y dejar de perseguir;
  abrir un alijo; y reclamar la Caravana de Fundación que vuelve sola (BronzeAgeClient no lo tiene). Radios de `/v1/datos`.
- **Dentro de un sitio** (los interiores son los pasos 4 y 7): un aviso arriba con dónde estás y «Salir al mundo», con la orden que toca
  (`salirAlMundo` desde tu residencia, `salirDeAsentamiento`, `salirDelCampamento`, `salirDePueblo`, `salirDeAvanzada`), sin elegir tropa
  ni carga todavía. El mapa sigue debajo, centrado en el sitio.
- **Capas**: los nombres de los nodos (sí de serie) y la fertilidad (el suelo con la rampa de BronzeAgeClient), en la barra; se recuerdan.
- **El bosque**, como relleno plano: el sotobosque tiñe el suelo según la densidad de bosque del artefacto.
- **Colores de Facción**: la paleta de reserva de BronzeAgeClient por el número de la Facción; los colores del sigilo entran con el paso 8.

Pruebas: `CosasDelMapaTests` (EditMode: el clic, las acciones que se enseñan y las que no, el tiempo de marcha por el grafo y lo que se reúne
del almacén) y la de PlayMode, que ahora sale del campamento con el botón, elige el campamento con un clic del ratón (o en la lista si hay
varias cosas), ve su ficha, quita la selección con un clic en el suelo y marcha, prueba la capa de fertilidad y monta un mapa nuevo con la
columna ya en marcha para comprobar que la ruta sale desde el primer fotograma. Rendimiento con todo pintado: fotograma mediano 2,2 ms, p95
2,4 ms, el peor 14 ms.

## Para la fase de arte

- ~~El pasto por fertilidad y la mezcla de colores por altura del suelo~~: hecho con el suelo con texturas (sección «El suelo», 2026-11-20).
- Las maquetas de cada cosa en vez de cajas: hecho para las plazas (sección «Asentamientos vistos desde el mundo», 2026-11-21); faltan las murallas, pueblos y aldeas, avanzadas y lo demás, y las figuras de columnas y caravanas.
- El marco de bronce de la interfaz en 9-slice (bloque 5 de la hoja de ruta).

## Ríos y cintas (arreglo del 2026-11-13)

Tras la 2B no se veía ningún río, ni caminos, fronteras, zonas, anillos ni Miradas. Dos causas:

- **El orden de los vértices de las cintas.** Con z = −y el mapa se da la vuelta, y las cintas quedaban mirando hacia abajo: el sombreado
  (que pinta una cara) no las dibujaba desde la cámara. Se corrigió el orden donde se construyen (`RiosDelMapa`, `CosasEnElMapa.Cintas`); el
  suelo y las cajas ya estaban bien.
- **El cauce excavado.** El relieve excava el cauce de cada río, pero la malla del suelo (paso de 0,5 a 2 u) es más gruesa que él y, al
  interpolar, lo rellena: a 1,5 u del cauce el suelo está de 0,9 a 15 m más alto, y la cinta del agua quedaba enterrada. Ahora el suelo baja
  hasta el cauce en la franja del río más el paso de su tesela (`CaucesDelMapa`), y el agua va plana de orilla a orilla en lo más bajo del
  cauce más 0,04 u, como el `asiento: 'bajo'` de BronzeAgeClient. El agua queda dentro del cauce, sin flotar ni hacer escalones. Con teselas de
  paso 2 (lejos) el cauce bajado mide hasta 5,5 u de ancho; con modelos y texturas de verdad (fase de arte) habría que bajar el suelo con el
  perfil del cauce y no solo hasta su fondo.

Pruebas: `MapaConTodoTests` (PlayMode) monta el mapa real con una cosa de cada tipo junto al campamento 4 (que tiene un lago a 1 u y un río a
15 u), comprueba que todas las cintas miran hacia arriba y que el píxel del río, el del mar (junto al campamento 7) y el del vado (junto al
campamento 2) tienen su color, y guarda capturas con los nombres encima. La de `PantallasDeMundoTests` comprueba que la cinta del río
cercana queda por encima de la malla del suelo pintada.

## El suelo (fase de arte, tema 1: suelo y relieve, 2026-11-20)

Estilo de Synty (pintado, formas legibles) con más detalle, y la paleta la deciden los datos del motor: ocre y pasto amarillento donde es
seco, verde donde hay humedad y fertilidad, roca clara en las cimas y nieve solo en los picos más altos.

**Capas** (`Assets/Resources/MapaMundo/Suelo`, tres Texture2DArray de 8 cortes de 1024², más un ruido suave de 512²):

| Corte | Capa | Dónde va |
|---|---|---|
| 0 | pasto seco | el llano y las colinas con poca humedad y fertilidad |
| 1 | pasto verde | donde la media de humedad y fertilidad pasa de `_Verdor` (0,28 → 0,55) |
| 2 | tierra apisonada | a manchas dentro de lo más seco (media por debajo de 0,12, con borde ancho) |
| 3 | arena | por debajo del umbral de la costa (420 m), con borde de 20 m |
| 4 | grava | a manchas en la subida de la colina a la montaña (570–600 m) y al pie de la roca de pendiente |
| 5 | roca en facetas | desde la montaña hacia la cima (620–695 m) y en toda pendiente fuerte (1 − normal.y de la pendiente ancha, de 0,14 a 0,4, con ruido); triplanar, a 2/3 de la escala de las demás |
| 6 | nieve | por encima de la cima más la mitad del tramo hasta el máximo del relieve (≈ 842 m), con un borde de 70 m roto por ruido de 25 a 900 m, menos en pendientes anchas fuertes |
| 7 | sotobosque | según la densidad de bosque (0,08 → 0,85), como mucho al 80 %: el pasto asoma |

`suelo_color` va en sRGB; `suelo_normal` (normal en RGB, +y de la textura hacia arriba) y `suelo_mascara` (R altura para la mezcla, G
suavidad), en lineal. Los importa `ImportarSueloDelMapa` (Editor, `Conquest.Mundo.Mapa.Editor`): al regenerarlas y copiarlas se importan bien
sin tocar nada.

**Datos que usa** el sombreado `Conquest/MapaSuelo` (solo el suelo; ríos, cintas y lo propio siguen con `MapaColor`):
- La altura del vértice (el relieve fino) y su pendiente.
- Los umbrales del generador de `/v1/datos` (`ELEVACION.umbralCosta`, `umbralColina`, `umbralMontana`, `umbralCima`).
- La fertilidad, la humedad y la densidad de bosque del artefacto, en una textura de una celda por texel (667 × 500, celdas de 48 m) leída con
  filtro bilineal; la posición se mueve hasta celda y media con ruido, y no quedan escalones de 48 m.
- La clase de terreno ya no se usa en el suelo: las zonas salen de la altura con umbrales suaves, como `colorDelSuelo` de BronzeAgeClient.

**Laderas (ronda 5).** En las laderas todas las capas van triplanares (pesos con la normal geométrica, a la octava potencia;
en lo casi llano, solo la proyección de arriba). También va así la altura de la máscara con que se mezclan. Esa era la causa del estirado
de la ronda 4: la mezcla leía la máscara solo desde arriba, y en una ladera empinada esa proyección se estira por la pendiente, así que el
borde entre nieve y roca salía en rayas verticales. La segunda escala de cerca va además girada 37°. La nieve lleva su propia luz: luz
envolvente y, en lo que no mira al sol, azul claro.

**Cómo pinta.** Pesos de partida por capas de pintura (el llano, encima el bosque, la grava, la arena, la roca y la nieve), y luego mezcla por
altura de textura (el borde sigue las matas y las piedras). UV en espacio de mundo: cerca, cada capa son dos muestras a 48 m y a 83 m por
repetición mezcladas por ruido (contra la repetición); lejos (de 220 a 900 m de la cámara), una a 200 m. Encima, variación de luz y de tono a
unos 170 y 900 m. Luz como `MapaColor`, y la niebla de guerra igual. El agua y la capa de fertilidad van sin textura, con su color de vértice
(alfa 0), como antes.

**La pendiente ancha.** Cada vértice lleva también (UV0) la normal del relieve suavizado con una caja de ±24 m
(`PinturaDelMapa.RadioPendienteAncha`). La roca de pendiente y la nieve la miran en vez de la de la malla: con la de la malla, el borde seguía
las diagonales de los triángulos y salía en dientes de sierra cada 6 m, y una arista estrecha salía en dos cintas de roca. Donde la pendiente
de la malla es fuerte pero la ancha no, va grava.

**Teselas.** La normal sale de las alturas con el anillo del vecino (ya no de `RecalculateNormals` por tesela) y cada tesela lleva un faldón
de un paso de alto que tapa la grieta con una vecina de otro paso. El faldón suma un 4 % de triángulos.

**Rendimiento** (mismo método que arriba, `PantallasDeMundoTests.CrearCuentaEntrarYCerrarSesion`, marchando, 300 fotogramas, editor en
batch):

| | Teselas | Mediana | p95 | Peor |
|---|---|---|---|---|
| Antes | 23 | 1,6 ms | 1,9 ms | 15,9 ms |
| Ronda 1 | 23 (425.984 triángulos con faldones) | 1,5 ms | 1,8 ms | 18,3 ms |
| Ronda 2 | 23 | 1,6 ms | 1,7 ms | 15,7 ms |
| Ronda 3, con niebla y cielo | 23 | 1,5 ms | 1,7 ms | 16,6 ms |
| Final (ronda 5) | 23 | 1,6 ms | 1,9 ms | 18,8 ms |

Construir una tesela lleva de 3 a 4 ms (48 teselas en 150–170 ms); con la pendiente ancha (ronda 3), de 4 a 6,5 ms (48 en 200 ms, 23 en 150); el peor fotograma sigue siendo el de construirlas.

**Regenerar las texturas.** La roca y la grava se hornean en Blender desde geometría de verdad. `hornear.py` pone bloques y lajas
low-poly, o cantos de tres tamaños, repetidos 3 × 3. Desde la ronda 5, la roca son masas de tamaños mezclados, sin hiladas: pocas
grandes, bastantes medianas y cascajo. El estrato solo se insinúa con el buzamiento común de la mitad de las lajas. Hornea, del cuadro del centro, la normal, la oclusión y la altura a
`horneado/*.npy`, y guarda `roca.blend` y `grava.blend`:
`"C:\Program Files\Blender Foundation\Blender 5.3\blender.exe" -b --python hornear.py -- todo`, unos 40 s.
Luego, desde `BronzeAgeArte/mapa/suelo`: `python generar.py --conquest <checkout de Conquest>` (numpy, Pillow y scipy; misma semilla, mismas
texturas). La roca y la grava pintan su color a partir de lo horneado: un tono base, luz en las caras de arriba, sombra en las cavidades y
aristas más claras. Saca cada capa por separado, la hoja `salida/hoja_3x3.png` (cada capa repetida 3 × 3, para ver que no se
nota la junta) y las tiras que copia a `Assets/Resources/MapaMundo/Suelo`. Lo que se afina sin regenerar está en las propiedades del
sombreado (`_Escalas`, `_Verdor`, `_Roca`, `_Nieve`, `_Mezcla`, `_FuerzaNormal`).

**Pruebas y capturas.** `SueloDelMapaTests` (PlayMode, con `BRONZEAGE_PRUEBA_URL`) busca en los rasters una costa, un llano seco, uno fértil,
el borde de un bosque, colinas, una montaña con roca en la pendiente y el pico más alto. Captura cada sitio de cerca, a media altura y desde
arriba, más dos juntas de teselas, la capa de fertilidad y el borde de la niebla. Comprueba que la nieve es blanca, que el llano fértil es más
verde que el seco y que cada tesela lleva su faldón. Capturas en `Logs/capturas-arte-suelo/`.

**Pendiente de este tema** (tras la ronda 5, lo decide Víctor):
- Vista desde arriba, la roca de las laderas muy empinadas sale en escorzo, con los bloques comprimidos en astillas cortas.
- De cerca, la nieve es un blanco limpio.
- En la textura de roca se reconocen los bloques grandes que se repiten; en el mapa lo rompe la segunda escala girada.
**Visto de otros temas:** la orilla del agua sale en escalera (la máscara de agua a 0,5 u; tema 2) y en la costa la roca de
pendiente llega hasta el agua; el sotobosque sin árboles encima se ve como manchas oscuras (tema 3).

## Niebla de guerra, bruma y cielo (calcados de BronzeAgeClient, 2026-11-20)

**Qué hace el prototipo** (`src/mundo3d/niebla.ts`, `cielo.ts` y `vista.ts`):
- **Niebla de guerra.** Una textura de una celda por texel (R explorado, G a la vista) con filtro **bilineal**: el borde se funde en una
  celda (5 u). Alfa = mezcla(nunca visto 206/255, mezcla(recordado 158/255, a la vista 0, G), R) sobre #0a0e17. Se mezcla en sRGB, después
  de convertir el color.
- **Sombras de nubes**, dentro del mismo parche. Un ruido de valor de 3 octavas a 0,065 por u (manchas de unas 15 u) oscurece un 20 % entre
  0,5 y 0,75 y deriva a (0,04, 0,015) por segundo en unidades del ruido.
- **Bruma lejana** (`THREE.Fog` lineal, después de la niebla de guerra) hacia el color del horizonte #b7c6cf. Empieza en
  min(2,2·d, 0,4·alcance) y acaba en alcance = min(carga, 7·d), en u, con d la distancia de la cámara.
- **Cielo**: una esfera con degradado del horizonte #b7c6cf al cénit #3d72b0 (1 − e^(−6·h)), bajo el horizonte hacia #8e9a98. Nubes de
  fbm de 5 octavas proyectadas en un plano (d.xz / (h + 0,12) · 1,6, deriva 0,004 y 0,0015 por segundo, umbral 0,52–0,85, hasta 55 % de
  blanco). El sol en (−60, 110, 45), con el disco pow(s, 600)·1,2 y el halo pow(s, 24)·0,22 en (1, 0,86, 0,62).

**Qué cambia en Conquest** (`Resources/MapaMundo/MapaComun.hlsl`, que incluyen `MapaColor` y `MapaSuelo`):
- **Antes** la niebla iba por celdas de 5 u sin fundir (un borde en escalera), se mezclaba en lineal (más oscura que en el prototipo) y no
  había nubes ni bruma: el mapa cargado acababa en un corte contra un fondo liso.
- **Ahora** todo lo de arriba, con los mismos valores. `NieblaDelMapa` pone la textura en bilineal y el color en sRGB. `MapaDeMundo` pone
  la bruma cada fotograma (`_Bruma`: cerca y lejos en metros, metros por u) y el cielo (`CieloDelMapa.shader`, como `RenderSettings.skybox`).
  El sol de la escena, si lo crea el mapa, sale de donde lo pinta el cielo.
- Lo propio (`_ConNiebla` 0) va sin nubes ni niebla de guerra, pero con la bruma, como allí.

**No calcado:** las luces del prototipo (hemisférica 1,4 y direccional 2,2 con sombras), el humo y los pájaros. La luz del suelo sigue
siendo la de `MapaColor`, con el sol en la dirección del cielo; las sombras van con el tema de cielo y luz.

Capturas: `Logs/capturas-arte-suelo/ronda3/` (`niebla_*`, `cielo_*`) y las del banco 3D del prototipo, para comparar, en
`Logs/capturas-arte-suelo/prototipo/`.

## El agua (tema 2, 2026-11-20)

Calcada del cliente jugador (BronzeAgeClient: `src/mundo3d/agua.ts`, `terreno.ts`, `paisaje.ts`, `lineas.ts`), con sus valores.

**Lámina.** Mar y lagos son una lámina transparente aparte por tesela (`TerrenoDelMapa.Lamina`, hijo «agua» de la tesela, al mismo paso que
el suelo) con el sombreado `Conquest/MapaAgua`:
- Cubre toda celda con alguna esquina de agua.
- Nivel: `max(375 m, h) − 1 m`. El mar queda a su nivel y cada lago a su altura plana. Mar y lagos usan el mismo material.
- Las esquinas de tierra toman el nivel del agua más cercana y quedan bajo el suelo, que corta la lámina en la orilla.
- Cada vértice lleva en UV0.x lo hondo que queda el fondo (u), sacado de las alturas al construir la tesela. No hace falta depth ni opaque
  texture.
- Color de somero #5aaea6 a hondo #1d4d72 con smoothstep(0, 2,4 u), y alfa de 0,45 a 0,9 con smoothstep(0, 1,4 u).
- Seis ondas en la normal: frecuencias de 1,7 a 16,7 por u, velocidades de 1,2 a 5,2 y amplitudes de 0,045 a 0,009. Se atenúan ×0,45 en lo
  somero y ×0,25 entre 15 y 90 u de la cámara.
- Brillo de Phong #d8e4e8 con 70, y fresnel⁴ hacia el horizonte del cielo (#b7c6cf): 55 % del color y 50 % del alfa.
- Espuma #e8f0ea entre 0,02 y 0,2 u de hondo, con su pulso `sin(x·2,3 + y·1,7 + t·1,7)`.
- Encima, las nubes, la niebla de guerra y la bruma de `MapaComun.hlsl`.

**Orilla** (`TerrenoDelMapa.Construir`). Cada tesela mira una ventana con 6 u de margen:
- La máscara de agua emborronada (dos pasadas de caja de 3 × 3). La costa es la línea donde vale 0,5.
- La distancia a tierra por chaflán.
- En el agua, el fondo baja de 0 a 2,6 u en los primeros 6 u desde la orilla (si el relieve ya es más hondo, se respeta).
- En tierra, el suelo baja hacia la lámina con 0,8 u de playa, sin quedar a menos de 0,03 u sobre ella.
- El suelo bajo el agua lleva lo hondo en el rojo del color de vértice. `MapaSuelo` lo pinta de fondo: la textura de arena, que se oscurece
  hasta 2 u.
- Así desaparece la orilla en sierra: antes el agua era el propio suelo subido a la lámina, y la orilla seguía los triángulos.

**Ríos** (`RiosDelMapa`, `CaucesDelMapa`):
- El trazado se suaviza con Chaikin (dos pasadas). Se alarga hasta el agua si acaba a menos de 8 u y entra 0,5 u en la lámina. Lo comparten
  la cinta y el cauce.
- Cinta de 3 u (navegables) o 1,5 u, plana en lo más bajo del corte más 0,04 u. UV0: x de orilla a orilla, y los u andados.
- El centro es más hondo (parábola de 1,5 u) y los bordes, someros con espuma. La corriente baja a 1,1 u/s.
- Orilla mojada de 1,2 u a cada lado (#6b5d40, alfa de 0,8 a 0).
- El cauce tiene ahora perfil:
  - Cada vértice a menos del medio ancho efectivo (el del río más medio paso de la tesela) baja a una parábola por debajo de la lámina,
    0,12 u más honda en el centro.
  - Fuera, una ribera de 1 u que sube 0,8 u hasta el suelo.
  - Ya no es una zanja plana de hasta 5,5 u.
- Vados: agua clara y somera con guijarros de orilla a orilla, con los bordes difuminados.
- Orden de pintado fijo, para que no parpadeen sobre el mar: lámina 3000, orilla mojada 3001 (desfase −1), río 3002 (−2), vado 3003 (−4).

**Lo que no se calca:** las sombras sobre el agua (tema 8). El reflejo es un color de cielo plano, como en el prototipo: no hay
reflexión ni refracción.

**Rendimiento** (método de arriba, marchando, 23 teselas):

| | Mediana | p95 | Peor | Construir 23 teselas |
|---|---|---|---|---|
| Antes | 1,6 ms | 1,9 ms | 18,8 ms | 129 ms |
| Después | 1,6 ms | 1,8 ms | 18,1 ms | 208 ms |

En la costa, con 57 teselas, hay 212.628 triángulos de agua frente a 580.608 de suelo.

**Pruebas y capturas:** `SueloDelMapaTests.ElAguaEnCadaSitio` busca en el relieve y en el artefacto los sitios: costa, mar hondo, lago,
ríos, vado y desembocadura. Comprueba que hay lámina aparte y que el mar hondo es agua y más oscuro que el lago, y guarda las capturas en
`Logs/capturas-arte-agua/`. El mundo de pruebas no tiene ríos navegables.

**Pendiente:**
- Desde arriba el vado se lee poco.
- La orilla mojada apenas se nota.
- En teselas de paso 2 el cauce sigue siendo más ancho que el río, aunque ya en V suave.
- Construir una tesela con agua cuesta más.

### El agua, ronda 2: detalle (2026-11-20)

La base del cliente jugador se queda, y encima va más detalle en estilo Synty.

- **Olas.** Mapa de normales propio (`Resources/MapaMundo/Agua/agua_olas`, generado por FFT con espectro de Phillips en
  `BronzeAgeArte/mapa/agua/generar_agua.py`; se repite sin costura y lleva la altura de la ola en A). Va a tres escalas y direcciones
  (9; 3,7 y 1,3 u), cada una desplazándose, con una variación grande de amplitud por ruido. Sustituye a las seis ondas, que desde arriba
  hacían bandas.
- **Destellos:** en las crestas de las olas pequeñas, con especular estrecho (260).
- **Reflejo:** el del cielo de verdad (`ColorDelCielo`, en `MapaComun.hlsl`, el mismo que pinta `CieloDelMapa`; MapaDeMundo pone
  `_CieloCenit`, `_CieloHorizonte`, `_CieloSuelo` y `_CieloSol`).
- **Color:** turquesa #6cccbd en lo muy somero, luego el somero y el hondo, con variación suave.
- **Lagos** (la lámina por encima del mar, UV0.y = 1): olas al 45 % y a la mitad de velocidad, un punto más verdes y sin rompiente.
- **Rompiente:** líneas de espuma que avanzan hacia la orilla siguiendo lo hondo (`sin(hondo·7 + t·0,35)`), rotas con ruido, más la franja
  fina del borde.
- **Suelo junto al agua** (`MapaSuelo`, con el color de vértice: verde = arena mojada, azul = ribera):
  - Arena mojada más oscura en la orilla.
  - Bajo el agua, manchas de guijarros y alga oscura, y cáusticas animadas que se pierden con lo hondo.
  - Junto al agua, el relieve se suaviza (quitaba surcos que salían a rayas en la playa).
  - Las capas que no son roca se proyectan solo desde arriba hasta unos 55°.
- **Ríos:**
  - El ancho varía con ruido (±25 %), crece río abajo (hasta +60 %) y se abre en abanico (×2,5) los últimos 3 u antes del agua; lo
    comparten la cinta y el cauce.
  - La ribera de arena y guijarros, de ancho irregular, la pinta el suelo a partir de la distancia al eje (hasta 1,6 u), con tierra mojada.
  - La cinta se funde con el suelo en los bordes (sin filete) y, en la desembocadura, se aclara y se desvanece hacia el mar.
  - Normales que fluyen río abajo, más deprisa en el centro, con vetas de espuma.
  - Aguas blancas donde la lámina baja más del 3–12 %.
- **Vados:** un tramo ancho (1,6 u a lo largo del río, el ancho del río más 1,2 u de largo) de guijarros claros, con piedras que asoman y
  espumilla.
- **Interruptor de calidad:** `PinturaDelMapa.AguaConDetalle` (sin destellos ni tercera capa de olas).
- **Coste:** las teselas sin río no consultan el cauce (`CaucesDelMapa.HayRioCerca`). Construir una tesela de interior vuelve a unos 3 ms;
  con agua o río, de 5 a 9 ms. Marchando: mediana 1,6 ms, p95 1,9 ms y peor 15,1 ms.

**Pendiente:**
- En los taludes de la costa quedan surcos del propio relieve, más arriba de donde se suaviza.
- La ribera del río aún se lee como dos franjas a lo largo del cauce.
- El abanico de la desembocadura es discreto.

### El agua, ronda 3: pulido (2026-11-20)

- **Púas en la playa: la causa.** El relieve era liso: lo comprobé muestra a muestra, es un acantilado de unos 30 m pegado al agua. La tierra
  solo bajaba hacia la lámina donde la máscara emborronada era mayor que 0, unas 2 muestras. Esa frontera seguía la escalera de la malla y,
  en la diagonal de la costa, el acantilado quedaba recortado en escalones alternos. Ahora la tierra baja bajo un techo continuo: 0,03 u
  sobre la lámina, la playa de 0,8 u por la máscara y, más allá, 0,8 u por u de distancia al agua (`DistanciaAlAgua`: chaflán que lleva
  también el nivel del agua más cercana). Lo mismo quitó el corte recto de la orilla de un lago.
- **Orilla:** banda de espuma dibujada que entra y sale, y arena mojada por la distancia al agua (1,2 u).
- **Fondo somero:** manchas de guijarros y alga menos frecuentes y más suaves. El ruido de 25 m solo se repetía en estampado; ahora se mezcla
  con el de 170 m.
- **Ríos:**
  - Verde azulado de la familia de lo somero del mar, más oscuro en el centro; en los bordes se ve el lecho.
  - Ondas alargadas río abajo (el mapa de olas estirado a lo largo; nada de líneas concéntricas) y pocas vetas de espuma.
  - Alguna roca con espuma, y aguas blancas solo donde la lámina baja más de un 10–30 %.
  - Borde del agua irregular; la fuente y la desembocadura se desvanecen.
  - Ribera de ancho muy variable, más ancha en el interior de las curvas (las barras, por la curvatura del trazado).
- **Coste:** construir las 23 teselas de la marcha cuesta 312 ms (208 en la base) por la distancia al agua y la búsqueda de la ribera.
  Las de interior siguen en unos 3 ms. Marchando: mediana 1,5 ms, p95 1,7 ms y peor 15,1 ms.

**Pendiente:**
- Desde arriba el río aún se lee bastante regular.
- Si el coste de construir molesta, pasar la construcción de teselas a un hilo.

## Caminos y territorio (tema 4, 2026-11-20)

Dos estilos, conmutables y lado a lado en las pruebas: «Prototipo» (etapa 1, el port tal cual de BronzeAgeClient) y «Mejorado» (etapa 2, por
defecto). `TrazosEnElMapa.Estilo` decide los caminos; `TerritorioDelMapa.Estilo`, el relleno y las líneas del territorio.
`PinturaDelMapa.CaminosYTerritorio` apagado vuelve a las rayas planas de antes.

### Dónde está

| Pieza | Qué hace |
|---|---|
| `TrazadoDelMapa` (C# puro) | Port exacto de `suavizarCamino`, `recorridos`, `tramosEnCaja`, `partirFronteras` y `zonasDeLoRecordado`; además `PorLosVados` |
| `TexturaDeCamino` | El pintor de texturas del prototipo (sendero y camino, 128 × 512) |
| `TrazosYTerritorio.Reunir` | Del almacén a trazos (caminos con sus anclas, fronteras, zonas, anillos) y rellenos por Facción |
| `TrazosEnElMapa` | Las cintas, en trozos con presupuesto (2 ms por fotograma, los más cercanos primero) cuando cambia el suelo de debajo |
| `TerritorioDelMapa` | El lienzo del territorio (2048 px sobre 1200 u, recentrado cada 128 u), pintado en otro hilo |
| `MapaCamino`, `MapaCaminoMejorado`, `MapaRayaMejorada` | Los sombreados del camino del prototipo, del camino «Mejorado» y de las líneas «Mejorado» |
| `BronzeAgeArte/mapa/caminos` | `hornear.py` (Blender, desde geometría) y `generar.py`: el detalle y la normal de sendero y camino, dos variantes |

### Etapa 1: como el prototipo

- **Caminos:** cinta con una muestra cada 0,5 u, cada borde sobre su suelo; anchos de 0,4 u el sendero y 0,65 u el camino, con textura
  (u a lo ancho, v = lo andado / 4 × ancho).
- **Fronteras partidas:** exterior de 0,12 u a trazos (1,2 / 0,7); entre provincias, 0,06 u con opacidad 0,45; la zona fusionada propia,
  0,18 u a trazos por encima de la niebla; el anillo de los mercenarios, 0,1 u con opacidad 0,7.
- **Relleno:** 0,18 lo propio y 0,13 lo ajeno, con borde interior de 6 capas.
- **Vegetación:** fuera de los caminos (árboles a medio ancho + 0,2 u, matas y capa baja a + 0,1 u).
- **Pruebas:** `TrazadoDelMapaTests` (EditMode, contra vectores de referencia generados por `tasks/referencias/trazado.mts` desde el código
  del prototipo).

### Etapa 2: «Mejorado»

**Caminos** (congelados así el 2026-11-20 por el coordinador):
- **El camino es suelo:**
  - Su color sale del color medio del suelo de alrededor, en HSV.
  - El tono se desplaza hasta 6° hacia el rojo (en pradera, 35°) y la saturación queda al 85 % de la del suelo (en pradera, 0,44).
  - La luminancia sube un 8 % (pradera −3 %, arena +2 %, roca +5 %).
  - De la textura de tierra solo toma la luminancia.
- **Detalle horneado** en Blender desde geometría:
  - Sendero: traza estrecha que serpentea, con hierba aplastada.
  - Camino: rodadas blandas a trechos, lomo de matas, huellas y guijarros.
  - Dos variantes que alternan por ruido. El sendero, además, serpentea con ruido en espacio de mundo.
- **Rodadas y guijarros a media distancia**, también calculados en el sombreado, para que a zoom habitual se siga leyendo un camino de tierra.
- **El lomo nunca se transparenta:** son matas a trechos (salía una línea clara recta).
- **Bordes y franja:** bordes irregulares (±15 %) y malla 1,8 veces el camino con un halo de terreno pisado; la capa baja ralea junto al camino.
- **Parches** de tierra apisonada en cruces y extremos.
- **Despeje en el bosque:** crece con la copa (medio ancho + 0,2 u + 0,6 × el radio de la copa).
- **Agua:** junto al agua el camino no se pinta encima. En un vado cruza por el punto como rodada mojada, pintada después del agua.
- **Lejos:** ancho mínimo en píxeles (2,2 el sendero y 3 el camino).

**Territorio** (el filo, opción 2; Víctor la eligió el 2026-11-21: «Vamos con el 2». Es el aspecto final y el valor por defecto,
`EstiloDeTerritorio.Mejorado`. «Prototipo» y «Suave» quedan solo como parámetros; nada del proyecto las usa):
- **Interior** muy suave: 0,05 lo propio y 0,035 lo ajeno, conservando la luminancia del suelo.
- **Filo pintado** por dentro de la línea: pico de 0,55 que cae a la mitad cada 0,45 u, con un ancho visible de alrededor de 1 u.
  - Ahí el suelo toma el tono de la Facción con una saturación de 0,30 a 0,45, en HSV y sin pasar por gris.
  - El contraste de brillo se comprime a la mitad alrededor del color medio de las capas.
  - Las Facciones oscuras o sin color solo oscurecen el suelo (×0,7 en el pico).
- **Reborde claro** para las Facciones que contrastan poco con el suelo (oscuras, o de tono de pasto o de tierra): una línea fina pegada a los
  trazos por dentro (centro a 0,35 u, σ 0,15 u o dos píxeles del lienzo, opacidad 0,5).
- **Líneas** con las medidas del prototipo, extremos redondeados, perfil oscuro fino y ancho mínimo de 2 píxeles.
- **Lo que no se tiñe:** la hierba baja dentro del territorio, como en el prototipo (solo el suelo).

**Banco y pruebas:**
- `BancoDeCaminos` inyecta sobre el mundo de pruebas una red trazada por el grafo de navegación y un territorio inventado:
  - un pasillo de bosque cerrado, una ladera, un cruce, plaza y pueblo, el vado;
  - un sitio por suelo (pradera, tierra seca, arena y roca, con caminos rectos por los nodos);
  - tres zonas sobre pradera (azul, verde y negra).
- `LosCaminosYElTerritorio` (etapa 1) y `LosCaminosMejorados` (etapa 2) son explícitas y guardan las capturas, el color del camino frente
  al suelo y el color del territorio a 0,5, 1,5 y 3 u del borde.

**Coste** (zoom habitual, mundo actual):
- Quieto, igual con los dos estilos: mediana 3,1–3,2 ms y p95 3,6–3,7 ms.
- Rehacer las cintas enteras: hasta 22,5 ms con «Mejorado» y 9 ms con el prototipo.
- Rehacer un trozo por su suelo: hasta unos 5 ms en un fotograma.
- El lienzo del territorio: en su hilo.

### Vados: el punto del vado (2026-11-20)

**La regla del motor** (main desde el PR #31 del 18-11; contrato 06 §2 y Movimiento_Ejercitos §4.3): el vado es un punto.
- El cauce va cerrado entero. Cada vado es un nodo más del grafo, justo en su punto de `Rio.vados`. Sus ids van detrás de los de la retícula,
  en el orden de los ríos y de sus vados.
- Ese nodo se une por aristas `vado` a un máximo de 3 nodos de cada orilla, a 6 u o menos (10 u si un lado se queda sin ninguno).
- Toda ruta que cruza un río pasa exactamente por ese punto y lo pisa a 0,75 u de él (`mundo::RADIO_VADO`).

**El mundo de las pruebas.** Desde el 2026-11-20 el servidor de prueba (7891) carga el mundo actual:
- Hash `a98e65c7…`, generador `regional-20-costas-2+estrategico-3+cauce-2`.
- 201 602 nodos, 770 134 aristas, 349 de vado, 116 vados. Los últimos 116 nodos son los puntos de `Rio.vados`, en orden y al milésimo.
- 18 nodos de vado no tienen ninguna arista de vado. El motor cuenta 23 vados sin alguna de sus dos orillas, casi todos en gargantas.
- El mundo anterior (`c83ef5e2…`, `…+cauce-1`, 6 163 aristas de vado) abría una franja de unas 16 u por vado. De ahí salió el «trecho
  cruzable» que se quitó.

**Caminos.**
- Los puntos de `Rio.vados` son anclas del suavizado (`TrazosYTerritorio.Reunir`, parámetro `vados`).
- El camino cruza recto por el vado a 0,75 u a cada lado del punto, en la dirección de la ruta sin suavizar (`TrazadoDelMapa.PorLosVados`).
- Sin vados, el suavizado es el del prototipo, idéntico a sus vectores de referencia.
- Pruebas: `ElCaminoCruzaPorElPuntoDelVado` (EditMode) y `LosVadosPorElPunto` (PlayMode, sobre el banco). Todo camino que cruza un río pasa
  a menos de 0,05 u del punto de su vado.

**Arte del vado.** Se dibujan los 116 vados de `Rio.vados`. Víctor eligió la **B** el 2026-11-20 («me gusta mas el 2 vamos con ese»): es el aspecto final y el valor por defecto. La A y la C quedan solo como parámetros; nada del proyecto las usa.
- **A.** `PinturaDelMapa.VadoComoTrecho` apagado: la franja de guijarros del tema del agua (1,6 u), con el camino pasando por encima como
  traza mojada.
- **B**, la elegida: un río poco hondo, corto y centrado en el punto.
  - Núcleo de 2 × `VadoMedioLargo` = 1,5 u (lo que se pisa), con el lecho de guijarros a la vista y playas de grava.
  - Transición de `VadoTransicion` = 2,5 u a cada lado; unas 6,5 u en total.
  - En el núcleo, la barra de guijarros de orilla a orilla (`_Barra` de `MapaAgua`, solo con el núcleo corto): contorno irregular, piedras que
    asoman y espuma donde el agua sale de ella. Se distingue de lejos como la A: contraste del núcleo frente al agua honda de 1,23 en el vado
    del banco y de 1,62 en otro, frente a 1,20 y 1,71 de la A.
  - El camino, encima, es una rodada mojada: guijarros oscuros y aplastados, el agua por encima a la mitad y los bordes deshechos. Se pinta
    después del agua.
- **C.** El trecho largo (`VadoMedioLargo` 10 y `VadoTransicion` 3), solo como referencia.

**Cómo se pinta el somero.** Lo somero (0..1) va en UV1.x del río y de la orilla, y en UV1.y de las teselas del suelo.
- Río: agua más clara y transparente sobre un lecho de guijarros, piedras que asoman, rizos y algo de espuma.
- Orilla mojada: guijarros mojados.
- Suelo: el cauce baja menos (×0,3) y la ribera es más ancha (×2,6), más tendida y con playa de grava.
- Con lo somero a 0, todo hace exactamente lo de antes: fuera de los vados el agua no cambia.
- El camino «Mejorado» sigue sobre el vado como traza mojada entre los guijarros. Fuera de un vado no se pinta sobre el agua.

## Nodos de recursos (tema 5, 2026-11-21)

Tres estilos, conmutables con `NodosDelMapa.Estilo`. Por defecto, «Mejorado»: la segunda pasada de la etapa 2, que Víctor aprobó el
2026-11-21. «Prototipo» queda como interruptor. «Cajas» es lo de antes: la caja de
`CosasEnElMapa`, que solo se pinta con ese estilo. «Ninguno» solo sirve a las pruebas.

| Pieza | Qué hace |
|---|---|
| `RocasDeNodos` (C# puro) | El bloque de nodos de `paisaje.ts`, tal cual: `azar` (32 bits de JavaScript), las rocas, el color y el dodecaedro aplastado |
| `NodosDelMapa` | Las teselas de 64 u: armarlas en el hilo principal (con la altura de `AlturaPintada`), rehacerlas cuando cambia la firma del suelo, dibujarlas recortadas por la cámara |
| `MapaColor` | Las rocas del prototipo y los cascotes |
| `MapaNodo`, `MapaParcheNodo` | El afloramiento «Mejorado» y su parche de suelo |
| `BronzeAgeArte/mapa/nodos` | `nodos.py` (Blender: alto polígono pintado por vértice, horneado a bajo; la piedra), `metales.py` (los cuatro metales) y `vista.py` y `laminas.py` (revisión: fichas y lámina en gris) |
| `ImportarNodosDelMapa` | Cómo se importan los FBX y las PNG de `Resources/MapaMundo/Nodos` |

**Mundo de pruebas (a98e65c7):** 4986 nodos.
- Por tipo: piedra 2790, hierro 1265, cobre 843, estaño 64 y oro 24. Sin ganado.
- Rareza: común la piedra, intermedio hierro y cobre, raro estaño y oro.
- Dónde caen: 567 en bosque cerrado, 810 en montaña, 75 a menos de 3 u de un río y 73 junto a la costa.
- Hay 10 distritos de vetas.
- Lo cuenta `tasks/referencias/nodos.cjs`.

**Datos que llegan:**
- Del artefacto: id, tipo, rareza, posición, cantidad inicial y anillo.
- No llega ni lo que queda ni si está agotado. La entidad `Nodo` del contrato (stock, regenera) no la emite el motor; solo llegan `Pueblo.nodo_id` y `ObraDeMapa.nodo_id`.

**Etapa 1, «Prototipo»:**
- Cada nodo, un montón de dodecaedros aplastados a 0,6: 5 rocas la piedra y 3 los metales.
- Distancia hasta 0,5 u (0,3 u los metales); radio de 0,075 a 0,225 u (0,06 a 0,15 u los metales).
- Giro Euler XYZ (0,2 sen 3a, a, 0,2 cos 5a).
- Color: gris #8d8d8d con el 70 % de `RECURSO_COLOR`, mezclado en lineal. La paleta es la de `MundoUI/Iconos/iconos.json`.
- Prueba: `NodosDelMapaTests` frente a `Datos/nodos_referencia.json`, que sale del propio código del prototipo con `tasks/referencias/nodos.mts`.
- Los nombres de los nodos no se tocan en este tema.

**Etapa 2, «Mejorado»:**
- **Un afloramiento por nodo que llena la huella:** radio 0,65 u la piedra y 0,5 u los metales (`RadioPiedra`, `RadioMetal`); alto 0,46 u y 0,43 u. Es una diferencia declarada con el prototipo.
- **Variantes y niveles:** tres variantes por tipo, por el hash del nodo; tres niveles de detalle (cerca hasta 160 m, medio hasta 400 m y lejos).
- **Siempre vertical**, con giro libre solo alrededor del eje vertical. La base se apoya en lo más bajo de la huella y sube hacia el centro sin destapar la base enterrada del modelo.
- **Modelos de Blender** (segunda pasada, 2026-11-21; la definitiva):
  - Las semillas son estables: los metales, con números fijos; la piedra, con el crc32 de «tipo_v». Antes la piedra salía de `hash()` de un texto, que cambia en cada proceso. La piedra definitiva es la misma receta que la que vio Víctor con otra tirada: cambian el ancho de los bancos y dónde caen los bloques sueltos.
  - Una tercera pasada (cobre, oro y hierro con trozos rotos) quedó aparte, en `BronzeAgeArte/mapa/nodos/pasada3`, sin entrar en el proyecto.
  - Los metales se arman como la piedra: bloques y lajas angulosos de caras planas.
  - El color se pinta en el marco de cada pieza con un criterio geométrico (la capa, la junta, la cara de fractura, la orientación de la cara), nunca con ruido en remolinos.
  - Bajo polígono de caras planas, con el color y la oclusión horneados. Sin mapa de normales.
- **Cada tipo:**
  - piedra: caliza en bancos con frente en escalones y bloques sueltos; es la referencia.
  - cobre: de 3 a 4 lajas inclinadas unos 30°, de roca parda ocre, con cardenillo en las juntas y chorreando por los lados, y nódulos de cobre nativo.
  - estaño: bolos de granito gris frío partidos, con racimos de cristales cortos azul acero (saturación 0,55) en los cantos.
  - oro: esquirlas de cuarzo que apuntan arriba, con oro mate en las juntas y en más de la mitad de las caras, pepitas y mancha de miel en el pie.
  - hierro: columnas de capas finas de pizarra oscura en escalones, con una capa fina de óxido de cada tres y diaclasas rectas.
- **Parche:** el pedregal del propio suelo (capas de grava y roca), `Parche` = 1,7 veces la huella.
  - Su valor es el del suelo o algo más oscuro (×0,85), nunca más claro.
  - Contorno en dos o tres lóbulos, con las fases por nodo en uv1. Se deshace en manchas y se alarga ladera abajo, hasta 1,8 veces.
  - Tinte como mucho del 15 % y hacia oscuro: óxido el hierro, gris verdoso el cobre; ninguno la piedra, el oro y el estaño.
  - Nunca sobre el agua: ni sobre el relieve con agua ni a menos de 0,15 u de un cauce.
- **Cascotes:** de 7 a 12, fragmentos del mismo modelo, con su material, volcados y medio enterrados. Son mayores junto al afloramiento y siempre van con el nivel lejos.
- **Vegetación:** sin árboles ni matas dentro del parche (`MapaDeMundo.Claros`, con media copa de margen); la hierba baja, a un tercio (`VegetacionDelMapa.Ralo`).

**Medidas** (primera pasada; detalle en la página del tema):
- Tamaño: se queda en 0,65 u la piedra y 0,5 u los metales (el coordinador retiró el objetivo de 5 veces los píxeles).
- Color al zoom habitual (distancia RGB entre las medias):
  - cobre frente a hierro: 60 en pradera, 95 en tierra seca y 94 en montaña;
  - oro frente a piedra en pradera: 72.
  - Siguen cerca: oro frente a cobre (de 27 a 45) y piedra frente a estaño en montaña (23).
- Coste con sombras, en la vista con más nodos del mundo:
  - Fotograma: unos +0,45 ms.
  - Mandar a dibujar: 0,16 ms.
  - Armar una tesela: hasta 2,5 ms. Se hace con un presupuesto de 2 ms por fotograma, las más cercanas primero.
  - La etapa 1, sin coste apreciable: tesela de 0,08 ms.

**Pruebas:**
- `LosNodosDeRecursos`: capturas por estilo, píxeles, laderas y bosque con y sin claro.
- `LasLaminasDeNodos`: los cinco tipos en fila sobre tres suelos, las variantes y los colores.
- `ElCosteDeLosNodos`: el coste.
- Las tres son explícitas.

## Asentamientos vistos desde el mundo (tema 6, 2026-11-21)

Las plazas, los pueblos y aldeas, las avanzadas, las obras, los campamentos, el alijo y las batallas: lo que BronzeAgeClient dibuja como
maquetas (`maquetas.ts`). Tres estilos, conmutables con `MaquetasDelMapa.Estilo`; por defecto, «Prototipo».
- «Cajas» es lo de antes: la caja de `CosasEnElMapa`, que solo se pinta con ese estilo.
- «Prototipo» (etapa 1) es el puerto de `maquetas.ts` y `huellas.ts`.
- «Mejorado» (etapa 2) pinta las plazas con miniaturas de Blender. Lo demás sigue como «Prototipo».

| Pieza | Qué hace |
|---|---|
| `MaquetasDelPrototipo` (C# puro) | El puerto de `maquetas.ts` y `huellas.ts`: las piezas de cada cosa (cajas, anillos, banderas, tiendas), sus radios y sus claros |
| `MaquetasDelMapa` | Rehacer las maquetas cuando cambia lo que hay, asentarlas en el suelo pintado y dibujarlas (instanciadas en tandas de 1023); en «Mejorado», las miniaturas |
| `CosasDelMapa.HuellaDePlaza` | El radio de la huella de una plaza: la miniatura, su suelo, su claro, el aro y el clic |
| `MapaAsentamiento`, `MapaParcheAsentamiento` | El sombreado de las miniaturas y el del suelo pisado bajo ellas |
| `ImportarAsentamientosDelMapa` | Cómo se importan los FBX y las PNG de `Resources/MapaMundo/Asentamientos` |
| `MenuDeAsentamientos` (editor) | La casilla del estilo y el banco de asentamientos en el editor |
| `BronzeAgeArte/mapa/asentamientos` | `villa1.py` (caserío), `villa2.py` (poblado), `villa3.py` (villa y, con `nivel`, los niveles 4 y 5), `plaza.py` (hornear y exportar), `lamina.py` y `componer1.py` (láminas de revisión); `visto_v14` guarda lo que se vio en la página versión 14 |

**Etapa 1, «Prototipo»:**
- Las piezas de `maquetas.ts`, tal cual, con el giro de three pasado al marco de Unity (z cambiada de signo; cuaternión (−x, −y, z, w)).
- Cada pieza, a la altura del suelo pintado más su base.
- Prueba: `MaquetasDelMapaTests` frente a `Datos/maquetas_referencia.json`, que sale del propio código del prototipo con `tasks/referencias/maquetas.mts`: 53 casos, 6120 piezas y 71 banderas iguales a 1e-9.

**Etapa 2, «Mejorado»: las plazas.**
- **Una miniatura por plaza**, de Blender, por nivel:
  - nivel 1, caserío;
  - nivel 2, poblado;
  - nivel 3, villa;
  - nivel 4, ciudad;
  - nivel 5, capital.
- Las variantes:
  - Los niveles 1 y 2 tienen dos variantes cada uno, elegidas por la semilla de la plaza (`VarianteDe`).
  - Los niveles 3, 4 y 5 tienen una variante cada uno.
- Una sola malla con el color, la oclusión y la máscara de las banderas horneados.
- **Tres niveles de detalle**, elegidos por el ancho en pantalla, no por la distancia: el cercano desde 80 px, el medio desde 40 px y el lejano por debajo (`AnchoCerca`, `AnchoMedio`).
- Topes de triángulos por nivel de detalle (cerca, medio, lejos):
  - nivel 1: 4000 / 1500 / 400;
  - nivel 2: 6500 / 2000 / 500;
  - nivel 3: 12 000 / 4000 / 800;
  - niveles 4 y 5: 16 000 / 5000 / 800.
- Texturas:
  - niveles 1 y 2: 512 px;
  - nivel 3: 1024 px;
  - niveles 4 y 5: 2048 px de color y 1024 de máscara.
- **Muralla:** va aparte de la villa y se dibuja solo si la plaza la tiene (ver «Murallas», abajo).
- **Asiento:**
  - La miniatura se coloca a la altura media de su huella (25 puntos).
  - Se inclina la mitad de la pendiente, hasta 8,5°.
  - Se gira con la puerta hacia su camino (ver «Murallas»); sin camino, el giro sale de la semilla.
- **Detalle de las casas (niveles 3 a 5):** se hornea desde una geometría rica sobre la ligera, así que no añade triángulos al mapa. Incluye puertas, ventanucos, cabezas de viga, el pretil, el zócalo manchado y enseres en las azoteas.
- **Regla de tamaño** (la eligió Víctor: «1,5»): `CosasDelMapa.HuellaDePlaza` es el mayor de dos radios.
  - Uno es el `RadioDePlaza` del prototipo (la décima parte de la zona × 0,8 × 0,62, 0,8 o 1 según el nivel). El otro es 0,9 u, el mínimo.
  - Con eso, el caserío recién fundado (zona de 12 u) pasa de 0,60 a 0,90 u, unos 140 px a zoom habitual. Lo demás queda igual y nunca encoge al subir de nivel.
  - Solo se aplica en «Mejorado» (`MaquetasDelMapa.Estilo` avisa a `CosasDelMapa.HuellaMejorada`).
  - «Prototipo» y «Cajas» siguen con la huella del prototipo, así que sus pruebas contra el prototipo valen.
- **Luz propia:** las miniaturas llevan más luz de ambiente y una sombra menos honda que el suelo (`LuzDelMapa` con 0,9 y 0,6), para que lo construido se encuentre solo.
  - Luminancia de lo construido frente al suelo, a zoom habitual:
    - nivel 1: 2,09 en hierba y 1,23 en seco;
    - nivel 2: 1,61;
    - nivel 3: 2,42;
    - nivel 4: 1,90;
    - nivel 5: 1,81.
  - En los niveles 3 a 5, las luces se comprimen por encima de 0,6 lineal (`_Rodilla`): lo más claro al sol queda en 228-235 y nada pasa de 240.
- **Suelo pisado:** el patio, las calles y las sendas se pintan en el suelo, no en la malla. Es un disco pegado al suelo pintado (`MapaParcheAsentamiento`), guiado por la máscara horneada con cada modelo (R).
  - Pinta la tierra desnuda del propio suelo, un 18 % más oscura, con grava a manchas en los patios.
  - En los niveles 4 y 5, la máscara trae también los huertos (G) y las eras (B) del borde.
- **Vegetación:** ni árboles ni matas hasta la huella y media (terreno pastado). Hasta un radio más, la capa baja queda rala (un tercio) y a media altura (`MaquetasDelMapa.Ralo`, junto al de los nodos).
- **Coste:** con sombras y una plaza de cada nivel del 1 al 5 en pantalla, el hilo principal tarda 3,31 ms frente a 3,64 ms del prototipo a zoom habitual. Se hacen 10 llamadas y unos 40 000 triángulos. La GPU no se puede medir en las pruebas sin ventana.

### Murallas (parte 3, 2026-11-21)

- **Qué lleva cada nivel.** Se pinta según el nivel de la plaza, como el prototipo: el mapa solo recibe `tiene_muralla`, no la clase del recinto.
  - En el motor (`Docs/Mechanics/Murallas.md` de BronzeAge), el recinto tiene su propio nivel: 1 empalizada (desde plaza de nivel 2), 2 muro de piedra (desde nivel 3) y 3 muralla con adarve (desde nivel 4). Las torres solo existen en los recintos 2 y 3.
  - Nivel 1: nunca lleva muralla. Si llega con `tiene_muralla`, no se dibuja nada.
  - Nivel 2: empalizada sobre terraplén y foso, toda de madera y tierra. La torre de la puerta y las garitas son de postes y tablas con techo de paja.
  - Nivel 3: la muralla ciclópea, dentro de su FBX (`plaza3_<v>_muralla_*`).
  - Nivel 4: zócalo de piedra gris y cuerpo de adobe almenado. Torres a 1,4 veces el lienzo, puerta mayor con rampa y un portillo.
  - Nivel 5: zócalo más alto, enlucido ocre con banda roja, antemuro bajo almenado y puerta monumental.
- **Ficheros.** Las de los niveles 2, 4 y 5 van en su propio FBX (`muralla<nivel>_<v>`, con su color y su máscara), uno por variante. Así no se tocan las villas aprobadas.
  - Cada una trae un json con su radio, en huellas, y el ángulo de su puerta.
  - Cada villa trae otro json (`plaza<nivel>_<v>.json`) con el ángulo de su puerta.
  - En las de nivel 4 y 5 no se dibuja la muralla vieja que trae su FBX, porque la sustituye la suya aparte.
- **Solo con `tiene_muralla`.** Las plazas recordadas van sin muralla.
- **Huella.** La villa se dibuja con su huella de siempre, y la muralla queda por fuera. Con muralla, el aro, el clic y el claro llegan al radio de la muralla, con tope de 1,12 (`CosasDelMapa.HuellaConMuralla`, `MaquetasDelMapa.RadioMaximoDeMuralla`). El nivel 3 sigue en 1,00.
- **En pendiente.** La villa se inclina la mitad de la pendiente, y la muralla sigue la pendiente entera del suelo bajo su anillo. Va sesgada, no inclinada (`Miniatura.MMuralla`), así que los muros quedan a plomo. El lienzo no se entierra cuesta arriba ni vuela cuesta abajo, y cuesta una matriz más por plaza.
- **Al hornearlas**, la muralla se separa de la villa por una marca en las caras de la villa (`muralla_hornear.marcar_lo_viejo`), no contando caras, porque el bmesh reutiliza los huecos de las borradas. Cada muralla se revisa desde los cuatro lados (`ver_muralla.py`, y en el mapa la vuelta de `LasMurallasModeladas`).
- **Puerta hacia el camino.** De los tramos de la red que empiezan o acaban dentro de la huella, se elige el de menor id (`tramo-N`, por su número). La villa se gira para que su puerta mire a su primer punto fuera (`MaquetasDelMapa.RumboDelCamino`). Sin camino, el giro sale de la semilla.
- **Triángulos de la muralla** (cerca / medio / lejos):
  - nivel 2: 1500 / 600 / 100 (de cerca también en cinta, de 150 dientes: los troncos sueltos pasan del tope y el diezmado se los come);
  - nivel 4: 3000 / 1500 / 200;
  - nivel 5: unos 5200 / 2000 / 250, sin diezmar de cerca (72 tramos, torres de cuatro merlones y la banda roja en tira): así no se pierden las almenas.
- **Radios** (en huellas):
  - nivel 2: 1,07 y 1,07;
  - nivel 4: 1,11 y 1,06;
  - nivel 5: 1,12 y 1,09.
- **Variantes.** Los niveles 3, 4 y 5 tienen segunda variante (`plaza<nivel>_2`): palacio al noroeste y templo o zigurat al oeste.

**Lo que sigue como en el prototipo:**
- pueblos y aldeas;
- avanzadas;
- obras;
- campamentos de mercenarios y de bandidos;
- el alijo;
- las batallas;
- la muralla de las plazas sin miniatura (las de «Prototipo»).

**En el editor:**
- `Conquest/Mundo/Asentamientos: Mejorado`: la casilla del estilo. Se guarda en EditorPrefs, se aplica al entrar en Play y viene apagada.
- `Conquest/Mundo/Banco de asentamientos`:
  - Abre una escena vacía sin guardar y entra en Play.
  - Monta el mapa en local, sin servidor, con el mundo que ya hay en disco (`persistentDataPath/mundo`, el de la última partida).
  - Pone plazas de los niveles 1 a 5 con zonas reales, en tierra firme, llana y despejada, separadas.
- `Conquest/Mundo/Banco: siguiente plaza`: pasa a la plaza siguiente del banco.

**Pruebas:**
- Sin capturas:
  - `MaquetasDelMapaReglasTests`: variante por semilla, nivel de detalle, huella y rumbo del camino.
  - `LasMiniaturasQueSeDibujan`: muralla solo con muralla, color de Facción, variante, aro y clic con lo dibujado, y «Prototipo» como antes.
  - `LasMurallasDeLasPlazas`: el nivel 1 sin muralla aunque la tenga; la del 3, la de su FBX; las del 2, 4 y 5, la suya aparte de su variante; el aro y el clic, al radio de la muralla; la puerta, hacia el camino de menor id.
  - `ElBancoDeAsentamientosSeMonta` (EditMode): monta el banco del menú y comprueba que hay una miniatura de cada nivel y ningún error.
- Con capturas, explícitas:
  - `LasPlazasModeladas`: niveles 1 a 5 frente al prototipo y el suelo en lo seco.
  - `LasMurallasModeladas`: las murallas frente a las del prototipo y las segundas variantes sin muralla.
  - Con `MUNDO_PLAZAS_MURALLA=1`, la luminancia y el coste se miden con muralla.
  - `LaLuminanciaDeLasPlazas`: luminancia y ancho frente al prototipo.
  - `ElCosteDeLasPlazasModeladas`: coste por fotograma.
  - `LosAsentamientos` y `ElCosteDeLosAsentamientos`: los de la etapa 1.

## La vegetación (tema 3, 2026-11-20)

Árboles, matas y capa baja del mapa. Las reglas de colocación son las del prototipo (BronzeAgeClient: `src/mundo3d/arboles.ts`, `matas.ts`),
con un mosaico de «wave function collapse» encima. Los modelos son propios, horneados en Blender fuera del repo
(`BronzeAgeArte/mapa/vegetacion`).

### Dónde está

| Pieza | Dónde | Qué |
|---|---|---|
| `ReglasDeVegetacion` | `Conquest.Mundo` (C# puro) | Las reglas del prototipo portadas tal cual (ver abajo) |
| `MosaicoDeVegetacion` | `Conquest.Mundo` (C# puro) | El WFC: modelo de baldosas simple, rejilla de 4 vecinos, menor entropía, propagación, reintentos y caída al prototipo |
| `VegetacionDelMapa` | `Conquest.Mundo.Mapa` | Reparto por tesela en un hilo, trozos de dibujo de 8 u, niveles de detalle y dibujo instanciado |
| `MapaVegetacion.shader` | `Assets/Resources/MapaMundo` | Follaje mate: translucidez, viento, variación por instancia y por rodal, hondura del dosel, aclarado, tinte de hierba y color de flor |
| `MapaSombraArbol.shader` | `Assets/Resources/MapaMundo` | La sombra falsa en el suelo |
| `ImportarVegetacionDelMapa` | `Conquest.Mundo.Mapa.Editor` | La importación de los FBX y las texturas de `Resources/MapaMundo/Vegetacion` |
| `Vegetacion/vegetacion.json` | `Assets/Resources/MapaMundo` | El manifiesto de submallas: corteza, hojas, masa y lejos |
| `Vegetacion/mosaico.json` | `Assets/Resources/MapaMundo` | La tabla del mosaico |

`ReglasDeVegetacion` contiene `Azar`, `Manchas`, `RasterEn`, `ZonaDe` y `EspecieDe` (pesos `ZONAS`/`MANDAN`, rodales de 22 u). También
`Probabilidades` (los cinco tipos de mata), `DistanciaAlAgua` y las bandas por distancia al héroe.

El reparto muestrea las alturas en el hilo principal, con 1,5 ms por fotograma.

### Reglas del prototipo portadas

- **Densidad:** 0,55 / copa² × bosque. No hay árboles por debajo de 0,2.
- **Exclusiones:** nada con pendiente de más de 1,1, margen de 0,2 u a los cauces, y los claros de las maquetas.
- **Tamaño:** de 0,75 a 1,25, y alto de 0,9 a 1,2.
- **Matas:** una candidata cada 0,5 u con `probabilidades(h, p, f, b, d, m)`.
- **Bandas por distancia al héroe:**
  - Árboles: 48 / 80 / 128 / ∞ u con parte 1, ½, ¼ y ⅛. Las copas crecen 1/√parte.
  - Matas: 20 / 34 / ∞ u con parte 1, ½ y ¼, y crecen 1, 1,2 y 1,4.
- **Copa:** ×1,4 sobre la del prototipo (`VegetacionDelMapa.Copa = CopaGrande`, 1,06 u). La del prototipo sigue como `CopaDelPrototipo`.

Las pruebas EditMode comparan con valores calculados del TypeScript.

### El mosaico

Encima de las reglas, el WFC decide:
- la estructura, en celdas de 2 u;
- el rodal (la especie que manda), en celdas de 8 u, con una celda mixta entre rodales distintos.

El prototipo sigue poniendo cada árbol con sus mismos azares. Con `VegetacionDelMapa.Mosaico = false` sale exactamente su reparto.

- **Estados:** bosque cerrado, abierto, linde, claro, matorral, pradera, ribera, cañaveral y pedregal.
- **Lo que manda el motor** se pone antes de resolver y nunca se viola:
  - densidad < 0,2: sin árboles; desde 0,7: cerrado;
  - pendiente > 1,1: pedregal;
  - ribera solo a menos de 2,5 u del agua y en lo bajo; cañaveral pegado al agua.
- **Junta de 64 u:** el anillo de celdas del borde sale de una función pura y no se propaga, así que no se ve la junta entre teselas.
- **Vecindades:**
  - El cerrado no toca pradera, matorral ni pedregal sin pasar por linde o abierto.
  - El claro solo va entre abierto, linde u otro claro.
- **Pesos y refuerzos** (en `mosaico.json`):
  - Árboles: cerrado ×3,6, abierto 1, linde 0,35, ribera 0,8.
  - Matas, con su peso por estado.
  - Afinidad propia 3,5, para que salgan manchas y no sal y pimienta.
  - Manchas de 12 u para pradera, matorral y claro.
  - 6 reintentos.
- **Bordes y detalles:** bordes orgánicos (la posición se mueve ±0,9 u con un ruido de 3 u), árboles jóvenes en la linde, y el olivo no manda
  en el cerrado.
- **Depuración:** `VegetacionDelMapa.VerMosaico` pinta cada celda con el color de su estado.

### Especies por zona

Once modelos con tres niveles de detalle, medidos desde el centro del trozo:
- cerca: hasta 160 m;
- medio: hasta 300 m;
- lejos: cúpula de 32 a 38 triángulos con la vista cenital horneada.

**Árboles:**
- encina, olivo y pino;
- álamo en la ribera;
- ciprés, de acento en la colina;
- abeto en la montaña;
- palmera en la costa.

**Matas.** El prototipo solo tiene «mata» y «junco»; la especie la pone `VegetacionDelMapa`:
- **Adelfa:** en la orilla de ríos y ramblas (por los cauces y sobre la línea de costa).
- **Retama:** en el matorral (50 %; lentisco 35 %; el resto, hueco) y algo en la linde.
- **Lentisco:** en lo demás.
- **Cañas** (el junco): uno de cada dos, o todos en el cañaveral. Hay una caña alta de 3,5 m.
- **Escala:** las matas van ×1,6; las cañas, ×1,2–1,8.
- **Corros en el matorral:** una madre de cada 2,5, con 4 a 9 hijas.

**Colores:** ningún verde se sale más de un 10–12 % en valor del rango de encina, pino y olivo. Las flores no se recolorean.

### Capa baja

Hierba, flores y piedras sueltas del prototipo, con `TAMANO` ×1,3. Solo se dibujan hasta 48 u del héroe (`VegetacionDelMapa.CapaHasta`),
con las bandas de las matas.

El mosaico modula la fórmula:

| Estado | Cambio |
|---|---|
| Pradera | hierba ×1,3 |
| Claro | flores ×1,5 |
| Matorral | hierba ×0,6, piedra ×1,5 |
| Bosque cerrado | hierba ×0,3 |
| Pedregal | piedra ×4, con alguna grande (hasta 0,35 u) |

**Hierba:**
- Sale en corros con calvas: una mancha de 3 u multiplica la probabilidad entre ×0,3 y ×1,6.
- En la arena de la costa, ×0,15.
- Su color sale de la misma mezcla que pinta el suelo debajo. `TerrenoDelMapa` pone como globales los datos del motor y el ruido del suelo
  (`_VegDatos`, `_VegRuido`, `_VegVerdor`, `_VegUmbrales`), y `SueloBajo` (en `MapaComun.hlsl`) los lee en el vértice.
- Resultado: pajiza sobre el seco, verde sobre el verde, rica en lo más fértil y de duna en la arena.

**Flores:** una mancha de un color por celda de 9 u (amapola, amarilla, margarita y malva, los colores del prototipo), elegido en el sombreado.

**Piedras:**
- Angulosas y facetadas, con la roca caliza del suelo.
- Hundidas un 30 %, de ×0,5 a ×2,2.
- Ninguna en roca viva: pendiente > 1 o la roca de la altura.
- Se juntan al pie de las laderas (canchal).

### Aclarado

Las copas se aclaran con un tramado alrededor del héroe y en una cápsula hacia la cámara. El tramado usa la cobertura del MSAA con ruido por
píxel.
- Radio del héroe: 1,6 u de cerca, 2,2 u a zoom habitual y 3 u alejado. El núcleo queda limpio hasta el 60 % del radio.
- Las otras columnas que se ven (ejércitos, caravanas, formaciones y batallas, hasta 8 en total): 1,4 u.
- Los troncos y las sombras se quedan.

### Interruptores

| Interruptor | Dónde | Para qué |
|---|---|---|
| `Mosaico` | `VegetacionDelMapa` (estático) | Apagado: el reparto exacto del prototipo |
| `VerMosaico` | `VegetacionDelMapa` (estático) | Depuración: cada celda con el color de su estado |
| `Copa` | `VegetacionDelMapa` (estático) | `CopaGrande` (1,06 u) o `CopaDelPrototipo` (0,5 u) |
| `CapaBaja` | `VegetacionDelMapa` (estático) | Hierba, flores y piedras |
| `AclararCopas` | `MapaDeMundo` (estático) | El aclarado sobre las columnas (las capturas de especies lo apagan) |

### Coste

Medido con `SueloDelMapaTests.LaVegetacionEstadoFinal`: 300 fotogramas, Editor en batch, 1600 × 900 con MSAA ×4.

| Zona | Zoom | Plantas | Triángulos | Llamadas | Mediana | p95 |
|---|---|---|---|---|---|---|
| Bosque denso | cerca | 2.361 | 986 k | 168 | 2,8 ms | 3,1 ms |
| Bosque denso | habitual | 3.277 | 737 k | 170 | 3,0 ms | 3,4 ms |
| Bosque denso | alejado | 4.523 | 462 k | 164 | 3,0 ms | 3,3 ms |
| Pradera fértil | cerca | 2.600 | 106 k | 111 | 2,6 ms | 2,9 ms |
| Pradera fértil | habitual | 4.024 | 85 k | 92 | 2,5 ms | 2,8 ms |
| Pradera fértil | alejado | 5.012 | 124 k | 92 | 2,5 ms | 2,8 ms |
| Costa | cerca | 2.194 | 448 k | 92 | 2,3 ms | 2,7 ms |
| Costa | habitual | 3.111 | 399 k | 131 | 2,7 ms | 3,0 ms |
| Costa | alejado | 4.068 | 232 k | 126 | 2,7 ms | 3,2 ms |
| Montaña | cerca | 989 | 874 k | 72 | 2,2 ms | 2,5 ms |
| Montaña | habitual | 1.694 | 326 k | 81 | 2,4 ms | 2,7 ms |
| Montaña | alejado | 2.333 | 112 k | 68 | 2,4 ms | 2,9 ms |

- **Dónde se va el tiempo:** la CPU de `Dibujar` es de 0,12 a 0,30 ms. En el bosque denso, lo demás es GPU: el follaje con alpha to coverage.
- **Llamadas en la pradera:** la capa baja pasó de 199 a entre 92 y 126 llamadas. Antes había una tanda por cada tinte de hierba, cada color de
  flor y cada hondura del dosel; ahora los tres salen del sombreado.
- **Nivel de detalle:** se mide al centro del trozo. Con el punto más cercano de la caja, el nivel de cerca se colaba a zoom habitual (1,1
  millones de triángulos en el bosque denso).
- **En marcha** (de la pradera al bosque denso a 0,15 u por fotograma, 1.200 fotogramas): mediana 2,8 ms, p95 17,7 ms, p99 55,8 ms y peor 113 ms.
  - Los picos vienen de construir las teselas de suelo (la peor, unos 64 ms, ya costaba eso antes) y del reparto.
  - El peor fotograma quieto (unos 85 ms, cada ~300 fotogramas) es la recolección de basura de siempre.
- **Pendiente:** el bosque denso queda en p95 3,3–3,4 ms, por encima del objetivo de 3,0. La siguiente palanca es la GPU del nivel medio: menos
  tarjetas por copa o grumos de menos triángulos.

### Pruebas y capturas

- **EditMode:**
  - `ReglasDeVegetacionTests`, contra el TypeScript.
  - `MosaicoDeVegetacionTests`: determinismo, vecindades, lo que manda el motor, anillo del borde y caída al prototipo.
- **PlayMode:** `SueloDelMapaTests.ElRepartoDeLaVegetacion` comprueba el determinismo y que no hay plantas en el agua, la cima, la nieve ni los
  cauces.
- **Explícitas, de capturas** (`MUNDO_CAPTURAS`):
  - `LaVegetacionEnElMapa`;
  - `ElMosaicoEnElMapa`;
  - `LasEspeciesEnElMapa`;
  - `LaCapaBajaEnElMapa`;
  - `LaVegetacionEstadoFinal`, que además mide el coste de arriba.

### Para otros temas

- **Caminos:** sin vegetación sobre ellos. El prototipo los tiene en `matas.ts`; aquí aún no hay caminos en el mapa.
- **Olivares** en hileras junto a los pueblos.
- **Sombras de verdad** sobre el agua (tema 8).
- **Juntar más tandas:** un atlas común de especies y variantes.

## La luz y las sombras (tema 8 adelantado, 2026-11-20)

Sombras reales del sol en el mapa, como en el prototipo (BronzeAgeClient: `src/mundo3d/vista.ts`). Hay dos fuentes:

- **Las sombras de URP**, con cascadas, para lo cercano: árboles, matas, cosas y columnas.
- **La sombra del relieve**, horneada por tesela, para las montañas sobre los valles.

### El sol

- **Por defecto**: `PinturaDelMapa.Sol` tiene el acimut del prototipo y **40° de elevación**. Lo eligió Víctor el 2026-11-20: «me gusta la de 40 grados».
- **El del prototipo**: (−60, 110, 45) con z = y del mapa, unos 56°. Queda como `PinturaDelMapa.SolDelPrototipo`.
- **Otras elevaciones**: `SolConElevacion(grados)`.
- **Quién lo usa**: el cielo (`_CieloSol`), la luz direccional que crea `MapaDeMundo` (#fff0d8, sombras suaves), la sombra del relieve y la mancha de relleno de los árboles. La mancha cae hacia el lado contrario al sol, con el largo que corresponde a la elevación.
- Las nubes no dependen del sol.

### Luz

`MapaComun.hlsl` reúne `LuzDelMapa`, `SombraDelSol` y `ConRelieve`. Lo usan `MapaSuelo`, `MapaAgua`, `MapaColor` y `MapaVegetacion`.

- **Sin sombras reales** (`PinturaDelMapa.SombrasReales = false`): la luz de antes, ambiente + directa, en gris.
- **Con sombras reales**, la luz del prototipo:
  - ambiente: el cielo como hemisferio, #dfe9f0 arriba y #6b5a40 abajo, según N.y;
  - directa: el sol #fff0d8;
  - las dos normalizadas a luminancia 1, así que a pleno sol la luminancia es la misma que antes (medido: 1,00).
- **Sol más bajo**: con el sol más bajo que el del prototipo, la directa se multiplica por `_CompensaSol` (seno de su elevación entre el seno de la del sol actual). Así el llano a pleno sol sigue igual. Solo afecta al suelo, el agua y las cosas: las copas no se compensan.
- **A la sombra** queda esta parte de la luz al sol, con un tinte de cielo frío y azulado, no negro:

| Dónde | Lineal | En pantalla (sRGB) |
|---|---|---|
| Suelo y cosas | 0,36 | ~0,62 |
| Copas | 0,45 | ~0,70 |
| Agua | la mitad que en el suelo | — |

- **Copas**: más suave para no matar la luz horneada en el color de vértice.
- **Agua**: el tinte de la luz va solo al 40 %, para no calentar su color.
- **Corteza**: tiene un suelo de luminancia, así que se lee marrón y nunca negra, también bajo la copa aclarada del héroe.

### URP

El asset de URP es de todo el proyecto (batalla y feudo) y en disco no se toca.

- **Al montarse**, el mapa:
  - clona el asset activo en memoria;
  - le pone 3 cascadas (cortes 0,62 y 0,8) y sesgo normal 1,5, contra el acné en las copas;
  - lo pone en `QualitySettings.renderPipeline`.
- **Al irse** (al desactivarse, al destruirse o al salir de Play) devuelve el original y destruye la copia.
- **Distancia de sombra**: sigue al zoom, 1,6 veces la distancia de la cámara (el radio del prototipo), como mucho 700 m.
- **Primera cascada**: llega hasta el héroe y algo más. De cerca se reconoce la silueta de la copa.

### Qué arroja y qué recibe

- **Reciben**: suelo, agua, vegetación y `MapaColor`, con `multi_compile` de `_MAIN_LIGHT_SHADOWS(_CASCADE)` y `_SHADOWS_SOFT*`.
- **Pasada `ShadowCaster`** en `MapaVegetacion`:
  - el mismo viento;
  - `clip` del alfa de las tarjetas, sin alpha to coverage;
  - sin el aclarado: la copa aclarada sobre el héroe sigue dando sombra al suelo, y así se lee «bajo el bosque».
- **Pasada `ShadowCaster`** en `MapaColor`: las cosas y tu columna.
- **Arrojan**, por nivel (`VegetacionDelMapa.DibujarSinMedir`):
  - árboles de cerca y a media distancia: siempre con la misma malla que se dibuja;
  - árboles de lejos: nada, porque quedan más allá de la distancia de sombra;
  - matas: solo de cerca;
  - hierba, flores y piedras: nunca.
- **Regla**: la malla que arroja no puede sobresalir de la que se dibuja. Una vuelta que arrojaba los árboles de media distancia con su cúpula de lejos dio sombra propia falsa y cuñas duras en las copas vecinas, y se deshizo.
- **No arrojan**: el suelo, el agua ni los ríos.
- **La mancha bajo los árboles** (`MapaSombraArbol`) se apaga entre el 75 % y el 100 % de la distancia de sombra (`_SombraHasta`). Más allá sigue sola.

### La sombra del relieve

`SombraDelRelieve` (en `Conquest.Mundo`, C# puro) recorre el relieve desde cada punto hacia el sol, hasta 64 u, con pasos que crecen con la distancia, y mira el horizonte. La visibilidad es `(elevación del sol − horizonte) / 6° + 0,5`: una penumbra de 6°.

- **Rejilla**: se calcula en una de 2 u alineada al mundo. Las teselas vecinas dan lo mismo en el borde: no hay costura.
- **Cuándo se hornea**: no al construir la tesela. `TerrenoDelMapa.HornearRelieve` la hace poco a poco, con un presupuesto de 1 ms por fotograma; el relieve no se puede leer desde otro hilo. La tesela la recibe al acabar.
- **Dónde se guarda**: en la malla, como «lo que tapa»: UV1.x en el suelo y el faldón, UV0.z en el agua.
- **Cómo se combina**: el sombreado toma el mínimo entre esta sombra y la de URP.
- **Interruptor**: `PinturaDelMapa.SombraDelRelieve`.
- **Cuánta hay** en el mundo de pruebas (rejilla de 6 u sobre la tierra): 0,76 % de la tierra a la sombra del relieve con el sol a 56° y **2,89 % con el de 40°**. Con la regla de dejarla encendida si pasa del 2 %, queda encendida.
- **Dónde hay más**: teselas (22, 6), (26, 6) y (34, 21), con un 42 a 46 %.
- **Al cruzar un borde de tesela**: peor fotograma 31,1 ms con ella y 30,2 ms sin ella. El relieve gasta como mucho 1,5 ms en un fotograma; lo demás es construir el suelo, que ya estaba.
- **Pendientes conocidos**:
  - El terreno no arroja en el mapa de sombras: lo que está dentro de la sombra horneada de una montaña (un árbol, una columna) no se oscurece.
  - El horneado del relieve llega a 12–15 ms en un solo fotograma la primera vez que la marcha toca teselas de relieve aún sin descomprimir: la descompresión no tiene presupuesto. Al cruzar un borde de tesela ya cargada, como mucho 1,5 ms.

### Coste

Sol a 40°, zoom habitual, 300 fotogramas:

| Zona | Antes: mediana / p95 | Con sombras: mediana / p95 | Llamadas de vegetación (las que arrojan) |
|---|---|---|---|
| Bosque denso | 2,9 / 3,3 ms | 3,9 / 4,5 ms | 170 (94) |
| Pradera fértil | 2,5 / 2,8 ms | 2,8 / 3,1 ms | 92 (16) |
| Costa | 2,7 / 3,0 ms | 3,5 / 4,0 ms | 131 (71) |
| Montaña | 2,3 / 7,9 ms* | 2,9 / 3,4 ms | 81 (46) |

*La montaña «antes» coincidió con teselas en construcción.

La pasada de sombra repite en cada una de las 3 cascadas las llamadas que arrojan. Sin recortes, el bosque denso daba 5,0 ms de p95.

**Las copas**, medidas contra la segunda pasada (antes de los recortes y de la compensación):
- luminancia y tono iguales (montaña 56,8 / 89,9° → 56,8 / 89,8°);
- borde, denso y costa a ±2 de luminancia y ±0,5° de tono.

**El mar**:
- en la costa, en conjunto, apenas cambia: azul − rojo de 0,205 a 0,200 a zoom habitual;
- en el agua somera junto al campamento 7 sí baja: 0,040 con la luz antigua, 0,028 con la nueva y el sol a 56°, 0,016 con el de 40°;
- no es el destello ni el tinte del agua: es el sol a 40° sobre lo somero;
- Se acepta el cambio en lo somero, que en pantalla no se aprecia.
- `MapaConTodoTests.SeVeUnaCosaDeCadaTipo` conserva su condición (azul > rojo + 0,03), pero ahora mira el agua más honda de la misma vista (a menos de 6 u del punto de mar), con la media de 9 × 9.

### Pruebas

- **EditMode** (`SombraDelRelieveTests`):
  - un muro da una sombra de h / tan(elevación);
  - el fondo de un pozo estrecho queda a la sombra y el de uno ancho no;
  - el llano está siempre al sol;
  - dos teselas vecinas dan lo mismo en su borde;
  - la dirección del sol del prototipo.
- **PlayMode**:
  - `SueloDelMapaTests.ElSolDelMapaEstaA40Grados`: 40° y el acimut del prototipo.
  - `MapaConTodoTests.SeVeUnaCosaDeCadaTipo`: mira el agua honda de la vista con la media de 9 × 9 y deja en el registro el píxel y la media (ver «El mar»).
- **Explícitas, de capturas** (`MUNDO_CAPTURAS`):
  - `LaLuzYLasSombras`:
    - antes, con el sol a 56° y a 40°, y la montaña a 40° sin la sombra del relieve, en seis sitios a los tres zooms; además, un árbol suelto con los dos soles y el coste.
    - Comprueba que el mapa trabaja con su copia del asset de URP y que al irse vuelve el original con sus valores.
    - Comprueba que en el borde de un bosque, de cerca, las copas oscurecen más del 5 % de la imagen.
    - Comprueba que con el sol a 40° hay valles a la sombra del relieve.
  - `LaSombraDelRelieveEnElMundo`: el porcentaje en el mundo, las teselas con más sombra, sus capturas con y sin ella, y el peor fotograma al cruzar un borde de tesela.
