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
