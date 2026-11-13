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
es el norte; con z = y saldría en espejo). El mapa entero mide 32 × 24 km: la precisión de `float` sigue por debajo del centímetro.

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

- El pasto por fertilidad y la mezcla de colores por altura del suelo (BronzeAgeClient: `colorDelSuelo`), que dan variedad al llano.
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
