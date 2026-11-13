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
