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
- Las maquetas de cada cosa en vez de cajas; las figuras de columnas y caravanas.
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

