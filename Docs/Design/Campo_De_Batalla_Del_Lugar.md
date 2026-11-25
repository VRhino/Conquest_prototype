# Campo de batalla del lugar

> **Estado: diseño acordado con el autor el 2026-11-22 y revisado el 2026-11-23 y el 2026-11-25 (los ríos del mundo, §2.3), sin implementar.** Es un sistema de Conquest. Lo que toca al motor (el recorte del lugar en el ticket, §3) está apuntado en la entrada 78 de `Docs/Mecanicas a desarrollar.md` del motor (repositorio BronzeAge). Las cifras son placeholders por calibrar. Contrastado contra el código del motor (`BronzeAgeFase0`, commit `e5be5790`) y de Conquest (rama `main` del 2026-11-21), no contra los documentos. La implementación se prueba con el autor antes de subir a `main`. Los huecos que quedan por atender están en §11.

## 1. Qué es

El mapa interior de una batalla deja de ser siempre el mismo y sale del sitio del mundo donde se abrió la batalla. Si la batalla cae junto a una playa, la playa está en el campo. Si cae junto a un río, el río corre por el campo, y si cae en un vado, el vado es un cuello de botella. El bosque, las colinas y los caminos de alrededor también entran. El relieve nunca es liso.

**Alcance de esta versión: solo batallas en campo abierto** (autor, 2026-11-23). Asedios, plazas, pueblos, aldeas, avanzadas y campamentos quedan para otro momento (§9).

La regla de fondo ya está escrita en el contrato del motor (`proto/bronzeage/v1/battle.proto:158`): «El terreno de la batalla sale del artefacto de mundo: Unity no lo inventa». Conquest la cumple solo a medias: lee el artefacto para dibujar el mapa de mundo, pero en la batalla no.

## 2. Lo que hay hoy

### 2.1 Lo que entrega el motor

| Pieza | Dónde | Qué aporta | Qué le falta |
|---|---|---|---|
| `BattleTicket.mapa_referencia` | `battle.proto:158-162, 207-210`; se rellena en `crates/nucleo/src/proyeccion/batalla.rs:179-181` | `mapa_id` (hash del artefacto) y `centro` (el `Batalla.punto`) en toda batalla que no es asedio | El recorte del lugar (§3): relieve, rejillas, ríos y caminos del campo, y de dónde viene cada bando |
| `Batalla.punto` | `crates/combate/src/ciclo.rs:367-388` | En campo abierto es la posición del **defensor** en `t` (`batallas.rs:404`), no el punto medio | Nada |
| `SettlementBattleSnapshot` | `battle.proto:181-187` | Edificios y celdas de muralla del asedio | El punto de la plaza: el asedio no lleva `centro` |
| Artefacto de mundo | `world.proto:105-131`, `crates/mundo/src/lib.rs` | Rejillas de 4 u (48 m): `altura`, `terreno` (agua, costa, llano, colina, montaña, cima), `densidad_bosque`, `humedad`, `transitable`, `coste`; ríos con su ancho y su caudal en cada punto, su orden de Strahler y sus vados como punto (`RADIO_VADO` 0,75 u, `lib.rs:93`). El ancho y el caudal van desde el contrato 0.33.0 (motor `ea24360`, 2026-11-25); §2.3 | Nada para esto |
| Paquete de relieve | `world.proto:157-177` | Altura cada 0,5 u (6 m) y bit de agua (mar y lagos) por muestra. En el eje de cada río, la altura es la cota de su cauce (§2.3) | Nada |
| Red de caminos | `entities.proto:1255-1270` | Tramos con su escalón (sendero, camino, calzada) | Es dinámica y va por la vista con niebla: el servidor de batalla no la tiene |
| Plan de cada columna | `Ejercito.plan`, `crates/nucleo/src/estado.rs:540` | De dónde venía cada bando | No se manda |
| Azar determinista | `base::Rng` (`crates/base/src/lib.rs:242`), `trazado::azar::hash` | El patrón de semilla por entidad ya existe (`urbanismo.rs:203`) | No hay semilla de batalla; `battle_id` es un UUID determinista |

Escala: 1 u = 12 m (`factor_unity`). Un río mide en el mundo de 0,4 a 5 u de ancho (de 5 a 60 m), según su caudal, y el paso de un vado como poco 0,8 u (unos 10 m). A escala 1:1 el vado ya es un cuello de botella para una escuadra de 20 soldados.

### 2.2 Lo que hace Conquest

| Pieza | Dónde | Estado |
|---|---|---|
| Escena de batalla | `BattleScene.unity` | Una sola. El suelo es un Plane de 1500×1500 m, plano, con MeshCollider y NavMesh horneado en el editor (`NavMesh-Ground.asset`). Encima, piezas Synty puestas a mano (murallas, calles) |
| Lectura del ticket | `Net/BatallaRealDeTicket.cs:16, 36-81` | Lee bandos, reglas y contexto. **Ignora `mapa`**: su comentario dice «el mapa es el de siempre» |
| Spawns | `DOTSWorld.unity` | Cuatro a mano, a unos 200 m entre bandos (tres atacantes y un defensor; los defensores 5 y 6 caen al de reserva) |
| Captura y suministro | `Map/CapturePointSetup.cs`, `Map/SupplyPointSetup.cs` | 3 y 3, puestos a mano; leen su posición del transform en `Start()` |
| Modificadores de terreno | `Map/BattleTerrain.cs`, `UnitStatsUtility.cs:116-136`, `BattleBootstrap.System.cs:81-88` | Ya existe `TerrainModifier` por tipo de escuadra (bosque, montaña, nieve), pero es **uno por batalla** y sale del `MapDataSO`, que siempre es `DefaultMap` (llano). Nunca se aplica nada |
| Altura del suelo | `FormationPositionCalculator.calculateTerraindHeight` | Ya muestrea `Terrain.activeTerrain`. Sin Terrain no hace nada |
| Lectura del mundo | `Mundo/Relieve.cs` (`AlturaEn`, `AguaEn`, `RasterDeMundo`), `Mapa/RiosYNiebla.cs` (`CaucesDelMapa`: `ACauce`, `AUnVado`, `SomeroEn`) | C# puro, sin referencias a Unity (`noEngineReferences`). Lo puede usar también el servidor de batalla. Calcula en `double` y `Relieve` lee de las teselas del `PaqueteRelieve` entero, no de un recorte (§11, H8) |
| Vegetación | `Mundo/ReglasDeVegetacion.cs`, `MosaicoDeVegetacion.cs` | Determinista desde bosque, humedad, fertilidad y pendiente. Sirve para decidir dónde, no la densidad de batalla |
| Movimiento | Unidades: `NavMeshAgent` (`UnitNavMesh.System`). Héroe en red: `MovimientoMandoSystem` (`Net/MandoSystem.cs:104-110`), con su `Velocidad` y `NavMesh.SamplePosition(AllAreas)`. Héroe fuera de red: `LocalHeroCharacterMotor`, con `CharacterController` | Los agentes atraviesan los colliders físicos: solo los frena lo que está en el NavMesh. El héroe en red no pasa por `StatResolveSystem` ni por el coste de área |
| Red | `Net/EspejoClienteSystem.cs:63-74`, `Net/MandoSystem.cs:105-128` | Servidor autoritativo. Cada cliente carga BattleScene por su NavMesh y predice su héroe con él: **los seis jugadores necesitan la misma malla que el servidor** |
| NavMesh en marcha | ninguna llamada a `BuildNavMesh`/`UpdateNavMesh` | El paquete `com.unity.ai.navigation` 2.0.14 está instalado; se puede hornear en marcha |

Los clientes ya descargan el artefacto y el relieve enteros y los guardan en disco (`Mundo/MundoEnDisco.cs:45, 57`). El proceso servidor de batalla no: solo recibe el ticket del lanzador. Y no debe descargar el mundo entero para usar un trozo de 8 u (decisión del autor, 2026-11-23: menos tiempo de carga de batalla).

### 2.3 Los ríos del mundo donde se abre la batalla

Lo que el mundo tiene en el sitio de la batalla es lo que el campo tiene que representar (§5). El agua es lo que más pesa: decide el tipo, las orillas de cada bando y por dónde se cruza. Desde el 2026-11-25 los ríos del mundo son otros (generador del motor, `ea24360`; mapa de mundo del cliente, 0.228.0), y el campo los lee así.

**Cómo es un río en el artefacto:**

- **Va por tramos, de confluencia a confluencia.** Cada tramo es una polilínea con un punto cada 10 a 45 m. El tramo que sale de una junta empieza en el mismo punto en que acaban los que llegan.
- **Cada punto lleva su caudal** (hectáreas drenadas) **y su ancho**, que crece aguas abajo con la raíz del caudal: de 0,4 u en un arroyo a 5 u en el río más grande. Cada tramo lleva además su orden de Strahler. `navegable` existe, pero hoy es siempre falso.
- **El trazado es el de un río real.** En el llano hace meandros proporcionados a su ancho: ondas de 7 a 10 anchos y, entre lazo y lazo, los ejes a 5 anchos o más (casi 4 anchos de tierra). Antes los lazos quedaban más juntos que el propio ancho del río.
  - Cuando un cuello se cierra, el lazo cortado queda como **brazo muerto**: un lago en media luna junto al río. En el recorte es agua de lago.
  - Donde baja con pendiente media, se abre en **trenzas**: ríos cortos (`…-trenza-N`) que salen de su río y vuelven a él por puntos de su propia polilínea.
  - En la boca al mar se ensancha en estuario.
- **El cauce está en el relieve y siempre baja.** En el eje del río, la altura del paquete de relieve es la cota de su cauce, que baja de cada punto al siguiente.
  - El cauce tiene el ancho del río y de 0,5 a 3 m de hondo (un 5 % del ancho).
  - Su orilla nunca pasa de una pendiente de 0,35, así que los pasos de montaña siguen siendo transitables.
  - En el paquete de relieve (0,5 u), los ríos de más de 1 u se ven como un surco. Los estrechos, apenas.

**Cómo lo pinta el mapa de mundo** (cliente 0.228.0, `src/mundo3d/rios.ts`):

- **El río es parte del suelo, como el mar y los lagos.** El agua va al nivel de la cota del eje (lo más bajo desde el nacimiento), en el cauce tallado con su ancho. Comparte la misma lámina que el mar y los lagos.
- La orilla es donde el suelo corta el agua.
- Las juntas y la boca en un lago o en el mar se unen solas.
- Las olas bajan con la corriente.

**Lo que esto pide al campo:**

- El campo tiene que ver el mismo río: el mismo trazado y el mismo ancho en cada punto, a escala (§6.1), y el agua al nivel del cauce, no por encima de su suelo (§6.3).
- Un brazo muerto es un lago más, y una trenza, un río más.
- **Un río grande ocupa mucho campo.** A 75 m por u, un río de 4,3 u mide 322 m: un tercio del lado de un campo grande. En la mesa, con los meandros de antes, uno así cubría el 41 % del área jugable y no dejaba sitio a los spawns. Con los de ahora, el mismo sitio sale ribera con spawns (§12.2).

## 3. Quién aporta qué

**El ticket lleva solo lo que la batalla necesita** (decisión del autor, 2026-11-23). El motor recorta del mundo el cuadrado del campo y lo manda dentro del ticket. Ni el servidor de batalla ni los clientes descargan ni leen el artefacto para la batalla, y la carga es más corta.

**El ticket trae datos del mapa, no de la batalla** (autor, 2026-11-23). Lleva aquello con lo que Conquest construye la base de la escena: el suelo del lugar y la dirección por la que llega cada bando. Todo lo de la batalla en sí (los spawns, los puntos de suministro y de captura, los objetivos) es de Conquest y no va en el ticket.

Cambios en el motor:

0. **`ContextoCaravana` gana `solitarios`**, como el de `ContextoCampoAbierto` (autor, 2026-11-23): verdadero si ninguno de los bandos es un ejército (una caravana suelta o de un héroe contra una columna). Decide el tamaño de la batalla (§6.1).
1. `BattleMapReference` gana un `RecorteDelLugar`, de `lado_recorte` (el lado del mundo del área jugable más el fondo: unas 13 u en la pequeña, unas 25 u en la grande) centrado en `centro`:
   - `alturas`: las muestras del relieve, cada 0,5 u, en u16 (unas 27×27, un par de KB), con su agua por muestra: tierra, lago o mar. El bit de agua solo no basta: los tipos costa y orilla de lago (§5) necesitan distinguir el mar del lago (mesa, 2026-11-23).
   - `punto`: el `Batalla.punto`, la posición del defensor. Cuando el recorte se desplaza hacia la tierra (punto 2), el `centro` ya no es el punto, y sin él se pierde dónde estaba el defensor (mesa, 2026-11-23).
   - `terreno`, `bosque`, `humedad` y `fertilidad`: las celdas de 4 u que toca el recorte (4×4 o 5×5, según caiga respecto a la rejilla), con el origen de la rejilla para alinearlas. La fertilidad da el verdor del suelo, como en el mapa de mundo (`colorDelSuelo`), y la usa la vegetación (mesa, 2026-11-23).
   - `rios`: los trozos de polilínea que cruzan el recorte, con sus vados y **el ancho de cada punto** (el del artefacto, §2.3), en milésimas de u como en el contrato (mesa, 2026-11-25). Las trenzas van como un río más. El caudal y el orden no hacen falta en el campo. Un ticket sin anchos (de un artefacto anterior al contrato 0.33.0) usa `ancho_rio_mundo`, el doble si el río es navegable.
   - `caminos`: los tramos de la red de caminos que cruzan el recorte, con su escalón.
   - `procedencia_atacante` y `procedencia_defensor`: un `Punto` por bando, a `distancia_procedencia` hacia atrás sobre su plan (o la plaza, el campamento o el pueblo de donde sale). Si un bando estaba quieto, su posición. Solo da la dirección: dónde va el spawn lo decide Conquest (§6.8).
   - El motor ya tiene todo eso: `Mundo::altura_en`, `densidad_bosque_en`, `terreno_en`, los ríos del artefacto, el paquete de relieve y la red de caminos.
2. **El campo abierto es siempre terrestre** (autor, 2026-11-23): nunca se abre una batalla del todo dentro del agua. Si más de la mitad del área jugable es agua, el motor desplaza el recorte hacia donde haya más tierra, hasta que la tierra pase de la mitad. El `centro` del ticket es entonces el del recorte desplazado, no el `Batalla.punto`. Esta regla hay que añadirla a la entrada 78 del motor.
3. `mapa_id` se queda, como comprobación de que el recorte sale del mundo vigente.
4. Fuera de esta versión, para asedios: `SettlementBattleSnapshot` gana `centro` y el mismo recorte (§9).

Conquest aporta todo lo demás: la lectura del lugar, la generación del campo, su montaje en Unity y sus efectos en el combate.

## 4. Las tres piezas en Conquest

Siguen el modelo híbrido: la generación es datos puros; Unity solo monta lo que sale; la lógica de combate lee una rejilla ECS.

1. **Lectura del lugar** (C# puro, junto a `Relieve.cs`). Lee el `RecorteDelLugar` del ticket: alturas, agua por muestra (tierra, lago o mar), clase de terreno, bosque, humedad, ríos y vados, caminos y procedencias. No toca el artefacto. No hay rotación: el norte del campo es el norte del mundo, y el minimapa de preparación casa con el mapa de mundo.
2. **Generación del campo** (C# puro, aritmética entera). Saca:
   - la rejilla de alturas (u16, paso `paso_alturas`);
   - la rejilla de zonas (u8 por celda: llano, colina, bosque claro, bosque cerrado, arena, agua somera, agua honda, camino, acantilado);
   - la lista de objetos (árboles, rocas, matas) con posición cuantizada;
   - la placa de spawn de cada bando (§6.8);
   - los sitios clave del campo (el vado, la boca del paso, las crestas), para la IA;
   - una consulta de sitio válido (seco, llano y alcanzable desde los spawns), para quien coloque los objetivos;
   - una huella (FNV) de todo lo anterior.
3. **Montaje** (Unity). Crea un Unity Terrain con esas alturas (así `calculateTerraindHeight` empieza a funcionar), los planos de agua, los árboles como instancias del Terrain y colliders solo en los troncos del área jugable, coloca los prefabs de spawn antes de su `Start()`, y hornea el NavMesh con un área nueva, agua somera, con su coste. La rejilla de zonas pasa al mundo ECS como un blob singleton.

**Los objetivos no son de este plan** (autor, 2026-11-23). Si hay puntos de captura o de suministro, cuántos y dónde, lo decide el modo de batalla. El campo solo ofrece la consulta de sitio válido (§11, H3 y H4).

BattleScene se queda con lo común (luz, gestores, subescena). El suelo plano y las piezas del castillo salen de ella; las piezas de muralla siguen sirviendo para los asedios.

## 5. Tipos de campo

**El campo es la representación del sitio, no una forma por tipo** (autor, 2026-11-23: «los tipos son irrelevantes»). El relieve, el agua, el bosque y los caminos salen del recorte tal como son en el mundo (§6), y nada se inventa según el tipo: ni el valle con acantilados del paso, ni las crestas de las colinas, ni el claro central del bosque.

El **tipo** queda solo como etiqueta del informe y para las dos reglas de orillas de los bandos: en un vado, orillas opuestas (§6.4); en una ribera, la misma orilla (§6.5). Se mira el área jugable (el cuadrado de `lado_mundo` del mundo) y se elige el primero que se cumpla:

| Orden | Tipo | Se da si en el área jugable | Regla que trae |
|---|---|---|---|
| 1 | Vado | Hay un vado | Los bandos, en orillas opuestas; el vado es el único paso |
| 2 | Paso | La mitad o más de las celdas son montaña o cima (no basta con la pendiente: medida cada 0,5 u, pasa de 15° en cualquier loma; mesa, 2026-11-23) | Ninguna |
| 3 | Costa | Hay mar | Ninguna |
| 4 | Orilla de lago | Hay agua de lago | Ninguna |
| 5 | Ribera | Un río la cruza sin vado | Los dos bandos en la misma orilla; el río no se cruza |
| 6 | Colinas | La diferencia de altura del mundo pasa de `relieve_colinas` | Ninguna |
| 7 | Bosque | El bosque medio pasa de `bosque_campo` | Ninguna |
| 8 | Llano | Si no se da nada de lo anterior | Ninguna |

## 6. Reglas de generación

### 6.0 La generación, paso a paso (la mesa)

Lo que hace el prototipo de la mesa (`generarCampo`, `pruebas/campo3d/generador.ts` de BronzeAgeClient), en su orden, con sus cifras. Es lo que Conquest tiene que portar (§4, con aritmética entera, §8). Las reglas y sus porqués están en §6.1 a §6.9; aquí va el cómo.

**Coordenadas y unidades:**
- El mundo va en u (x al este, y al sur); el campo, en metros desde su centro (x al este, y al sur). Un punto del mundo `w` cae en el campo en `(w − centro) · 75`.
- Las alturas del campo van en metros, relativas a la media del área jugable.
- «Metros absolutos» es la posición del mundo por 75: el ruido se toma ahí, así que el mismo sitio da siempre el mismo ruido.
- Rejillas: la de alturas, un vértice cada 2 m (`paso_alturas`; 1201 × 1201 en la grande con su anillo). La de zonas, una celda cada 4 m (600 × 600). Las dos cubren el campo entero, el área jugable más el anillo.

**0. Entrada.** El recorte del ticket (§3) y la procedencia de cada bando (`null` si estaba quieto). El generador no lee el mundo.

**1. Lectura del lugar** (`leerElLugar`). Se mide sobre el área jugable (`lado_mundo` de lado, centrada en `centro`):
- la fracción de montaña o cima (clase ≥ 5 de las celdas de 4 u, mirada cada 0,5 u);
- la pendiente media del mundo, en grados, por diferencias centradas en las muestras de 0,5 u;
- el desnivel (máximo menos mínimo de las alturas del mundo) y el bosque medio;
- si hay mar y si hay lago;
- el vado: el más cercano al centro dentro del área jugable, que no caiga en el agua ni a menos de 0,75 u de un lago o del mar;
- si un río cruza el área jugable.

Con eso, el tipo, por la tabla de §5.

**2. Medidas** (§6.1). Lado jugable `J` y distancia de cada spawn al centro, por el tamaño. El campo entero mide `L = J + 2 · anillo_fondo`. Zoom de altura: `75 / 12 = 6,25`.

**3. Rasgos del mundo** (`rasgosDelMundo`, sobre las muestras de 0,5 u del recorte):
- `rugosidad`: cuánto se aparta cada altura de la media de su entorno de 3 × 3 muestras. Es la raíz de la media del cuadrado de esa diferencia, en un entorno de 5 × 5.
- `pendiente`: la del mundo en m/m, por diferencias centradas, media en un entorno de ±1 u (5 × 5 muestras). Es la de la ladera, no la de cada muestra.
- `acentuada`: la altura con las crestas y las vaguadas multiplicadas por `acento_relieve`. A ×1, que es como va, es la altura tal cual.
- Colina y montaña, como 0/1 por celda de 4 u (clase 4; clase ≥ 5) e interpoladas en bilineal. Así la forma del detalle cambia suave de una clase a otra.

**4. Relieve base** (§6.2). En cada vértice, la altura del mundo en su punto en bicúbico (Catmull-Rom sobre las 16 muestras de alrededor). Se resta la media del área jugable y se multiplica por 6,25.

**5. Relieve mínimo.** Si el desnivel del área jugable no llega a `relieve_minimo` (8 m), se suma una ondulación hasta llegar a él: `manchas(X, Y, 220 m, semilla 11) − 0,5`, escalada. `X, Y` son metros absolutos.

**6. Detalle fractal** (§6.2):
- **Octavas:** de `detalle_longitud` (400 m) a la mitad cada vez, mientras pasen de 10 m: 400, 200, 100, 50, 25 y 12,5 m. Cada una pesa `detalle_persistencia` (0,5) veces la anterior.
- **En cada vértice:**
  - `amplitud = detalle_amplitud · (0,5 + 1 · colina + 2 · montaña) + detalle_rugosidad · rugosidad · 6,25`;
  - `ladera = detalle_pendiente · min(pendiente del mundo, 0,35)`.
- **Octava n, de largo `ℓ` y peso `w`:**
  - El ruido se gira un ángulo `a = 0,9 n + 0,4` rad. Sin el giro se vería la rejilla del ruido.
  - `v = manchas(Xr, Yr, ℓ, 20 + n)`; `ondulado = 2v − 1`; `arista = 1 − |ondulado|`.
  - `forma = (1 − montaña) · ondulado + montaña · (arista² − 1/3) · 1,5`: crestas en la montaña y ondulado en lo demás.
  - Se suma `min(ladera · ℓ + amplitud · w / Σw, 0,2 · ℓ) · forma`.

**7. Mar y lago** (§6.3). En la rejilla de zonas, una celda es agua si su muestra (bilineal del agua del recorte) llega a 0,5. Se calcula la distancia de cada celda a la tierra y al agua (chaflán, dos pasadas).
- **Nivel:** la media de la altura del campo en las celdas de tierra a 8 m o menos del agua. **Es uno para toda el agua quieta del campo**, mar o lago.
- **Fondo** (vértices en el agua), a `dt` m de la tierra:
  - hasta `ancho_somero` (25 m): de 0,2 a 0,8 m bajo el nivel;
  - más allá: `0,8 + min(3, (dt − 25) / 8)` m bajo el nivel.
- **Lámina:** el nivel.
- **Playa** (tierra a menos de `ancho_playa`, 60 m, del agua): de nivel + 0,25 m junto al agua, subiendo suave hasta el relieve a 60 m.
- **Zonas:** agua somera hasta 25 m de la tierra; más allá, honda.

**8. Ríos** (§6.3 a §6.5). Para cada río del recorte:
- **Línea:** la del ticket en el campo, suavizada con Chaikin (4 vueltas, sin cortar más de 200 m por esquina). Luego se pega al fondo del valle:
  - cada punto mira su perfil a lo ancho, `3 u` (225 m) a cada lado, a pasos de `max(2 m, 225 / 40)`;
  - se queda con el mínimo interior más bajo que tenga al menos 8 m de subida a cada lado;
  - el desplazamiento se suaviza con 12 puntos a cada lado.
- **Medio ancho en cada punto:** el ancho del ticket en el punto más cercano de la línea original, por 75 y entre 2. Sin anchos, `ancho_rio_mundo · 75 / 2`, el doble si es navegable.
- **Lámina inicial por punto:** lo más bajo del suelo ya tallado en 9 cortes a lo ancho (de −1 a 1 medio ancho), más 1,5 m. Se promedia con 3 puntos a cada lado. Si en el campo hay agua quieta, en los últimos 120 m antes de ella se va igualando a su nivel.
- **A qué río pertenece cada vértice y cada celda:** al río cuyo borde queda más cerca (`distancia − medio`). De él toma el medio ancho, la lámina interpolada y la distancia, a lo largo del río, al vado más cercano (`av`).
- **El suelo, vértice a vértice:**
  - **Dentro de un lago o del mar:** solo se ahonda el cauce, a 1,5 m bajo su agua.
  - **Paso del vado** (`d ≤ medio` y `av` menor que el ancho del paso, abajo): lecho de grava a `lámina − 0,25 − 0,3 · manchas(6 m)`, con su lámina somera.
  - **Cauce y orilla:** una hondonada `hond(d) = 1 − suave((d − medio) / max(8 m, medio / 2))`. El suelo pasa a `lerp(suelo, suelo alisado a 20 m, hond) − (altura_orilla + 1,5) · hond`. Dentro del cauce, lámina.
  - **Junto al vado** (`av ≤ medioVado + 15 m`): la orilla baja al agua en una rampa de 15 m.
  - **La bajada en embudo.** A `fuera = d − medio` del agua:
    - el embudo tiene de medio ancho `medioVado + 45 · (1 − e^(−fuera/25)) + 0,5 · fuera`;
    - se funde con la orilla a lo largo de `15 + 0,3 · fuera`;
    - el suelo no queda por encima de `lámina − 0,1 + subida(√(fuera² + max(0, av − medioVado)²))`;
    - `subida` empieza con pendiente 0,2 y llega a 0,9 a los 100 m.
  - **El camino pisado:** `(1 − suave((av − (0,8 · medioVado + 0,35 · fuera)) / (12 + 0,25 · fuera))) · (1 − suave((fuera − 40) / 60))`.
- **El paso del vado:** `medioVado = ancho_vado_mundo · 75 / 2 = 30 m`, por `(1 + 0,5 · (manchas(40 m) − 0,5) + 0,2 · (manchas(12 m) − 0,5)) · (1 + 0,35 · min(1, d / medio)²)`.
- **Zonas:** el cauce es agua honda; el paso del vado, somera; la bajada (pisado > 0,5) será camino.
- **Lámina final:**
  - en cada vértice de río, 1,5 m sobre el mínimo del suelo en 12 m alrededor (filtro de mínimo separable), salvo en el lago, el mar y el vado;
  - luego 4 vueltas de media con los 4 vecinos, con el lago y el mar fijos;
  - el lecho sigue a la lámina, conservando su hondura.

**9. Zonas de tierra** (§6.6). En cada celda de 4 m que no sea agua:
- **Densidad de bosque:** la del mundo más `(manchas(metros absolutos, 90 m, 13) − 0,5) · 0,35`.
- **Pendiente:** por diferencias a 2 m a cada lado.
- **La zona:**
  - **Arena:** a 60 m o menos de agua quieta.
  - **Acantilado:** más de 45°.
  - **Camino:** la bajada al vado.
  - **Bosque cerrado o claro:** densidad desde 0,6 o desde 0,25.
  - **Colina:** más de 12°.
  - **Llano:** lo demás.

**10. Spawns** (§6.8).
- **Eje:** de la procedencia del defensor a la del atacante. Si las dos están a menos de 20 m, del punto a la del atacante. Si aun así no hay eje, sale de la semilla del lugar.
- **Giros:** 0°, ±5°, ±10°… hasta ±180°. Para cada distancia, de la del tamaño hacia abajo a pasos de 25 m, hasta 100 m.
- **Placa válida:** un disco de `radio_placa` (25 m) dentro del área jugable, sin agua ni acantilado.
- **Que se alcancen:** componentes conexas (4 vecinos) de lo transitable, que es todo menos el agua honda y el acantilado. Las orillas se miran con las componentes sin el vado.
- **Orden de la búsqueda:**
  1. En un vado, primero por el lado de la línea del río (solo placa seca; el atacante, si se puede, en su orilla).
  2. Si no, con las reglas de orillas.
  3. Sin ellas, con aviso.
  4. Si tampoco, el llano forzado: el campo se rehace entero como llano, con el agua (§6.9).
  5. En el llano, si los bandos no se alcanzan, se avisa y el campo se queda.
- **Los tres puntos de cada bando:** a −15, 0 y +15 m a lo ancho del eje.

**11. Placas** (§6.2, paso 6). En cada placa, el plano `h = a + b · dx + c · dy` que mejor se ajusta al suelo del disco de 25 m (mínimos cuadrados). Se funde con el relieve a lo largo de otros 25 m. Después se vuelven a sacar las zonas de tierra.

**12. Árboles** (§6.6). Un hueco cada `separacion_cerrado` (4 m):
- **Dónde:** en tierra (no agua, arena ni acantilado) y con densidad desde 0,2.
- **Probabilidad:** `min(1, 0,55 / 36 · densidad · 4²)`, con `azar(gi, gj, 21)`. `gi` y `gj` son los metros absolutos entre 4, redondeados: el mismo árbol en el mismo sitio.
- **Dentro del hueco:** el árbol se mueve al azar ±40 % del paso.
- **Las placas:** nada a menos de 25 + 6 m.

Luego los **sotos**, con el aviso de `max_arboles` si se pasa:
- **Dónde:** una celda de 70 m donde la densidad no llega a 0,2 y la zona es llano o colina.
- **Cuántos:** con probabilidad `0,25 · (0,5 + fertilidad)`, un grupo de 3 a 10 árboles en 8–20 m de radio, más juntos en el centro.
- **Lejos de las placas:** a más de 25 + 25 m.

**13. Decoración del suelo** (§6.6). Un hueco cada `paso_decoracion` (3 m) en el área jugable y 40 m más, movido al azar dentro de su hueco:
- **En el agua:** en el paso del vado, un canto con probabilidad 0,35; en el resto del agua, nada.
- **En la bajada al vado:** el 80 % de los huecos, vacío.
- **En las placas:** nada.
- **En lo demás:** el tipo sale de las reglas de las matas del mapa de mundo (`probabilidades`, `src/mundo3d/matas.ts`). Recibe la altura del mundo (para costa, colina o montaña), la pendiente del campo, la fertilidad, el bosque, la distancia al agua en u del mundo y la mancha de flores (`manchas(30 m, 77)`). Las reglas:
  - **Junco:** a menos de 1,2 u del agua y por debajo de colina.
  - **Piedra:** más en las laderas y en el monte.
  - **Mata:** en el suelo pobre y en el borde del bosque.
  - **Flor:** en manchas, en suelo fértil sin bosque.
  - **Hierba:** por la fertilidad, menos en el bosque y en las laderas.
- **Lo que no sale:** lo que falta hasta 1 es suelo desnudo.

**14. Informe**:
- el tipo y si el llano fue forzado;
- el desplazamiento del recorte;
- las medidas de la lectura;
- el rumbo, el giro y la distancia de los spawns;
- los árboles y los sotos;
- el acantilado;
- el desnivel y la pendiente media del campo;
- la variación interna: la media cuadrática de lo que el relieve se aparta de su versión alisada a unos 160 m;
- los avisos y el tiempo.

**Lo que el prototipo no hace todavía:**
- los caminos, que no están en el artefacto;
- abrir sendas por el bosque cuando los spawns no se alcanzan (§6.9);
- los colliders y `max_colliders`;
- `intentos_validacion`;
- `celda_bosque` y `bosque_cerrado` (§13 los nombra, pero la mesa no los usa).

### 6.1 Tamaño y escala interna contra externa

- **Dos tamaños de batalla, por la entidad más grande que lucha** (autor, 2026-11-23):

  | Tamaño | Batallas | Área jugable | Entre bandos | Mundo que se lee | Recorte |
  |---|---|---|---|---|---|
  | Pequeña | Persecución (columna contra columna); caravana pequeña (caravana suelta o de un héroe contra una columna) | `lado_pequena`, 600 m | 550 m (`distancia_pequena`, 275 m cada uno) | 8 u | unas 27 u |
  | Grande | Campal (ejército contra ejército); caravana grande (ejército escoltando una caravana contra lo que sea, o caravana suelta contra un ejército) | `lado_grande`, 1000 m | 900 m (`distancia_grande`, 450 m cada uno; autor, 2026-11-25, antes 1500 m y 650) | unas 13 u | 32 u |

  El tamaño sale del contexto del ticket: en campo abierto, de `solitarios`, que ya existe («persecución entre columnas personales; si no, campal entre ejércitos»); en caravana, de una marca igual que el motor tiene que añadir (§3).
- Alrededor del área jugable, un anillo de `anillo_fondo` (700 m; autor, 2026-11-25, antes 200) de terreno no jugable para el horizonte. La niebla de la escena cae sobre él, por la distancia al centro del campo y no a la cámara: empieza a un cuarto del anillo y lo tapa entero en su borde, así nunca se ve dónde se corta el mapa (mesa, 2026-11-25).
- **La escala interna no es la del mundo.** A 1:1 (1 u = 12 m) los 600 m serían 50 u, unas 12 celdas: más que toda la zona de una plaza de nivel 1 completa (techo de 10 u de radio, 20 u de lado a lado; `ZONA_INFLUENCIA.radioMaximoPorNivel`, `crates/nucleo/src/estado.rs:449`), e incluso más que la de nivel 5 (60 u). El autor lo descarta: una batalla abarca menos que una provincia.
- **Escala interna (`escala_campo`): 75 m por u, la misma en las dos** (autor, 2026-11-23: se mantiene el zoom y la grande lee más mundo). Una loma mide lo mismo en una persecución que en una campal. Lo que se lee del mundo sale del lado: 600 m son 8 u (dos celdas, menos de la mitad de una provincia de nivel 1) y 1000 m, unas 13 u.
- **Qué escala y qué no:**
  - Escala la **disposición**: dónde está la costa, por dónde va el río, dónde está el vado, dónde sube la loma, dónde está el bosque, de dónde llega cada bando.
  - **El río y el vado sí escalan: son proporcionales al mapa de mundo** (autor, 2026-11-23). En el mapa de mundo, cada río mide lo que dice su punto, de 0,4 a 5 u según su caudal (§2.3; hasta el 2026-11-25, 1,5 u fijos y 3 si era navegable), y el vado es una franja de 0,8 u. En el campo, a 75 m por u: de 30 a 375 m, y un vado de 60 m. Antes iban fijos, a 24 m y 20 m, y al pasar del mundo a la batalla el río no cuadraba: en el mundo ocupa casi una quinta parte del área jugable, y en el campo ocupaba una veinticincoava.
  - No escalan, y van en metros de batalla (§13): el ancho de la playa y el de los caminos.
  - **La altura escala igual que el plano: el campo es un zoom sobre el sitio** (autor, 2026-11-23: «la inclinación se mantiene, solo que es más largo; no es un estirado, es como si hiciera un zoom sobre el punto donde está la batalla»). La altura del mundo se multiplica por 75 / 12 = ×6,25, como el plano. Así cada ladera tiene en el campo la pendiente que tiene en el mundo. Un cerro de 25 m de desnivel con 17° de pendiente media pasa a 163 m con 18,5°. Antes la altura no escalaba, y las pendientes salían seis veces más suaves que en el mapa de mundo: el campo parecía llano.
- Con dos celdas de lado, las rejillas de 4 u del artefacto dan muy poca información dentro del campo (2×2 valores de bosque o de clase de terreno). Lo que manda es el relieve (17×17 muestras de 0,5 u), la línea del río, los vados y el bit de agua; el bosque se interpola con ruido.

### 6.2 Relieve

1. **Zoom** (autor, 2026-11-23): la altura del recorte, menos la media del área jugable, por la escala del campo (×6,25, §6.1). Las pendientes son las del mundo.
2. Se interpola en **bicúbico**: con bilineal, cada muestra (37,5 m en el campo) dejaba una faceta.
3. Si la diferencia de altura del área jugable queda por debajo de `relieve_minimo`, se suma una ondulación de semilla del lugar hasta alcanzarlo. Así el llano no queda liso. Con el zoom casi nunca hace falta: 2 m de desnivel en el mundo ya son 12 m en el campo.
4. **Detalle fractal** (autor, 2026-11-23): lo que el mundo no puede tener entre sus muestras. Sin él, los campos de colinas, montaña y paso salen pobres: el mundo da una altura cada 37,5 m de campo (17×17 en los 600 m de una pequeña), el zoom agranda las formas grandes y entre muestra y muestra solo queda una superficie suave.
   - **Octavas** de `detalle_longitud` (400 m) a unos 10 m, cada una con el peso de la anterior por `detalle_persistencia` (0,5) y girada un ángulo distinto, para que no se vea la rejilla del ruido. Ruido de valor del sitio (en metros absolutos), determinista.
   - **Cuánto, en cada octava:** lo empinado que es el mundo ahí (su pendiente media en 1 u, con un tope de unos 19° para no volver pared la montaña), por el largo de la octava, por `detalle_pendiente` (0,3). Una ladera de 15° admite lomas de decenas de metros; el llano, casi nada. A eso se suman una base por clase de terreno (`detalle_amplitud`: la mitad en el llano, 1,5 veces en colina y 2,5 en montaña o cima) y la rugosidad del propio mundo a escala del campo (`detalle_rugosidad`), repartidas entre las octavas.
   - **Qué forma:** crestas (ruido de aristas) en montaña y cima; ondulado en colina y llano.
   - **Límite:** cada octava, como mucho un 20 % de su largo de alto, para no llenar el campo de acantilados.

   Medido en la mesa (variación interna: lo que el relieve se aparta de su versión suavizada a unos 160 m; y el acantilado):

   | Sitio | Sin detalle | Con detalle |
   |---|---|---|
   | Colinas (15°) | 1,3 m, 0 % | 2,7 m, 1 % |
   | Colinas (14°) | 0,8 m, 0 % | 2,2 m, 0 % |
   | Montaña (38°) | 5,2 m, 25 % | 7,4 m, 34 % |
   | Llano (6°) | 0,4 m, 0 % | 1,0 m, 0 % |

   Las primeras pruebas, con la altura del detalle fija (2 m de base) o ligada solo a la rugosidad del mundo, apenas se notaban: la rugosidad del mundo es pequeña (de 0,06 m en el llano a 1,2 m en montaña) y las octavas largas, que son las lomas, se quedaban sin altura. Lo que funciona es ligar la altura de cada octava a su largo y a la pendiente del sitio.
5. Por encima de `pendiente_max_transitable` (45°, lo que admite el agente del NavMesh), la zona es acantilado. Con el zoom, las laderas tienen la pendiente del mundo, y en el mundo se andan. Con 30°, un vado con 22,7° de pendiente media en el mundo tenía un 30 % de acantilado y no dejaba sitio a los spawns; con 45°, un 2 %.
6. Se alisan las placas de spawn (radio `radio_placa`), para que las escuadras puedan formar: no a nivel, sino al plano que mejor se ajusta al suelo del sitio (su altura y su pendiente media), fundido con el relieve a lo largo de otro radio. A nivel, en una ladera, cortaba una terraza con un anillo de talud (autor, 2026-11-25).

Medido en la mesa (pendiente media y desnivel del área jugable, en el mundo y en el campo):

| Sitio | Mundo | Campo |
|---|---|---|
| Cerro junto al mar | 17,3°, 25 m | 18,5°, 163 m |
| Colinas | 14,5°, 27 m | 14,8°, 173 m |
| Colinas | 12,5°, 23 m | 12,5°, 145 m |
| Vado en ladera | 22,7°, 30 m | 22,0°, 186 m |
| Llano junto a un río | 1,8°, 4 m | 2,5°, 30 m |

Una costa con acantilado (31° de media, 69 m) da 432 m de desnivel y un 37 % de acantilado aun con 45°: entre el mar y el acantilado no queda sitio para los spawns, y la mesa lo avisa.

Antes de entender que el campo es un zoom (mesa, 2026-11-23), se probó a acentuar solo las crestas y vaguadas (`acento_relieve`, `radio_acento`), pero no devolvía la pendiente del sitio. El ajuste sigue en la mesa, a ×1: no hace nada.

### 6.3 Agua

- Mar y lago: donde el bit de agua del relieve está puesto. La orilla se ensancha a `ancho_playa` de arena y `ancho_somero` de agua somera, transitable y lenta; más allá, agua honda, infranqueable.
- Río con vado: la polilínea del ticket, con el ancho de cada punto a la escala del campo (§2.3: por 75 m/u; sin anchos, `ancho_rio_mundo`). Junto al agua, la tierra mojada de la orilla, también como en el mapa de mundo (1,2 u, a escala). El cauce es agua honda. No se reutiliza `CaucesDelMapa.Consultar` para la profundidad, que está ajustada al mapa de mundo: solo su geometría.
- **La línea va por el fondo del valle** (mesa, 2026-11-25). Con el zoom, un desvío de la línea que en el mundo no se ve (unos metros) son decenas de metros en el campo, y el río subía por la ladera. Cada punto busca lo más hondo del relieve a `3 u` de lado, a lo ancho del río (alisado a 12 m), y se queda ahí solo si es un fondo de verdad: con al menos 8 m de subida a cada lado. Si no, la línea se queda como viene. Con los ríos nuevos del motor, que ya van por su cauce (§2.3), casi nunca se mueve.
- **El cauce sigue el relieve, sin paredes** (autor, 2026-11-25: «que el cauce siga el relieve, que no fuerce la pared»). No es un canal a nivel cortado en el suelo, sino una hondonada que se resta del relieve tal como baja. Dentro del ancho del río, el suelo se alisa a 20 m (el lecho de un río es liso) y baja `altura_orilla` + 1,5 m. La orilla va de ahí al relieve a lo largo de medio ancho del río (como poco, 8 m). Antes la lámina iba plana por tramos y el cauce se cortaba a su nivel: en una ladera quedaba una pared a cada lado.
- Río sin vado: paisaje (§6.5).
- **Ríos que llegan a un lago o al mar** (mesa, 2026-11-23; caso `?x=2185.84&y=714.72&ax=2183.84&ay=714.68&tipo=caravana_grande`): la línea del río del artefacto puede seguir por dentro del lago. Como en el mapa de mundo, el río se acaba en la orilla: donde ya hay lago o mar no se cava cauce ni se pone lámina propia, y en los últimos 120 m la lámina del río se va igualando al nivel del lago, sin escalón. **Un vado en el agua, o a menos de 0,75 u de un lago o del mar, no es un vado**: no cuenta para el tipo vado ni se pinta (antes salía un paso de grava en medio del lago). El cauce del río sigue unos metros dentro del lago, hondo, para que la franja somera de la orilla del lago no quede como una barra de arena atravesada en la boca.
- **El agua sigue al cauce** (autor, 2026-11-25: «que el agua siga la altura del cauce»). En cada vértice de río la lámina va 1,5 m sobre lo más hondo del lecho tallado en 12 m a su alrededor. Sale del suelo ya tallado, el mismo para todos los ríos, así que en una junta las aguas de los dos ríos coinciden solas.
  - Luego se alisa un poco (4 vueltas a los vecinos de 2 m), con el lago o el mar como borde fijo, y empalma con ellos.
  - El vado guarda su agua somera.
  - Donde la lámina baja deprisa (más de un 3 %, del todo a un 15 %), la vista la pinta de espuma, como rápidos o cascadas.
  - Antes la lámina de cada río iba por su lado y se alisaba 20 vueltas: en las juntas quedaban saltos de varios metros (hasta 7 m), que el alisado volvía rampas de espuma. Ahora el mayor salto es de 0,2 m.

### 6.4 Vado

- El punto del vado es una franja de agua somera de `ancho_vado_mundo` (a escala: 60 m) a lo ancho del cauce, con las orillas rebajadas en rampa.
- **Que parezca un paso de río** (autor, 2026-11-23):
  - **Lecho:** de grava, a 0,25–0,55 m bajo el agua y no del todo liso, con cantos sueltos que asoman.
  - **Forma del paso, natural, no un rectángulo** (autor, 2026-11-23): su ancho ondula a dos escalas (40 m, ±25 %; 12 m, ±10 %) y crece hasta un 35 % al llegar a cada orilla, donde se abre el embudo de la bajada: estrecho en el centro del río y ancho en las orillas.
  - **Bajada en embudo:** a cada lado, la orilla baja hacia la entrada del vado en un embudo, no en un pasillo recto. Junto al agua mide lo que el vado; se abre deprisa por la orilla (unos 45 m más en los primeros 25 m) y luego despacio ladera arriba. Dentro, el suelo baja hacia la entrada del vado desde el frente y en diagonal desde los flancos, como se llega andando. La pendiente es suave junto al agua (unos 11°) y crece poco a poco hasta unos 42° a 100 m, donde se encuentra con la ladera sin escalón. El borde del embudo se funde con la orilla. En el resto del río, las orillas quedan como salen.
  - **Camino pisado:** más estrecho que el embudo, marcado en el centro y junto al agua, y desvaído hacia los lados y ladera arriba; color de grava, con poca hierba (zona camino donde pasa de la mitad).
  - **Agua y lecho a 2 m:** la lámina y el color del lecho van por vértice (2 m), no por celda (4 m), con el color y la transparencia según la profundidad: clara y muy transparente donde hay poca agua (se ve la grava), verdosa y casi opaca donde cubre. Sin la escalera de celdas en las orillas y en el borde del paso.

  En el vado de la batalla con el río encajado en su valle, la bajada deja llegar por tierra de una orilla a la otra por el vado. Antes, el relieve no lo dejaba.
- El vado es agua rala: se cruza, pero ralentiza a quien la pisa (zona agua somera).
- **En un vado, los bandos van siempre en orillas opuestas** (autor, 2026-11-22).
- **Cómo se colocan** (autor, 2026-11-23): se gira sobre el centro hasta que cada spawn cae a un lado del río del vado, respetando el relieve como salga. La placa solo tiene que caer en seco; el lado lo da la línea del río, no que los spawns se alcancen por tierra. El atacante, si se puede, en la orilla de la que viene. Si el relieve no deja llegar por tierra de un spawn al otro (un río encajado en su valle, con las orillas en pared), se avisa y el campo se queda como es. Antes, el vado exigía placas sin acantilado y spawns que se alcanzaran; con el zoom y el detalle no los encontraba, y el campo caía al llano y perdía el río.
- **El vado es el único paso.** No hay paso de reserva: el cuello de botella es la gracia.
- El cauce fuera del vado es infranqueable.
- **Un vado que cae fuera del área jugable no se trae dentro** (autor, 2026-11-23). No hace falta: el mundo no deja que dos ejércitos se encuentren en orillas opuestas de un río sin un vado cerca, y esa batalla no tendría sentido. Bandos en orillas opuestas implica vado dentro del área jugable. La generación lo da por hecho y, si no se cumple, lo registra como error del recorte.

### 6.5 Ribera: río sin vado

- Si el río cruza el área jugable sin vado, **los dos bandos están en la misma orilla** (autor, 2026-11-23).
- El río es decoración: se coloca como va en el mundo y listo. No se cruza ni se hace nada con él (autor, 2026-11-23). Su cauce es agua honda, infranqueable, aunque parta el área jugable.

### 6.6 Bosque

- La densidad del artefacto (celdas de 48 m) se interpola y se le suma ruido de semilla del lugar, a celdas de `celda_bosque`.
- **Los árboles, los mismos por metro cuadrado que en el mapa de mundo** (mesa, 2026-11-23): donde la densidad pasa de 0,2, 0,55 / 6² árboles por m² por la densidad (la cifra del mapa de mundo, con su copa de 6 m), con las mismas especies por altura y agua cercana (`especieDe`). Antes iban por escalones (cerrado a 4 m), unas 3,5 veces más densos que el mundo, y el campo no se parecía al sitio.
- **Todo a escala del soldado** (autor, 2026-11-23): el mapa de mundo exagera las figuras (×2) y la hierba; el campo no. Con un soldado de 1,8 m (los personajes de Synty): árboles de 7 a 12 m de alto y copas de 6 a 10 m, unas cinco veces el soldado; hierba de 0,35 m (por la rodilla), mata de 0,9 m (por la cintura), junco de 1,3 m (por el pecho) y piedras de 0,5 m, mayores en las laderas. La mesa pone en cada punto de spawn una escuadra de referencia (20 soldados de 1,8 m, 5 de frente y 4 de fondo, a 1,5 m) con su estandarte, para ver la escala.
- **Sotos en la llanura sin bosque** (autor, 2026-11-23): donde el bosque del mundo no llega a 0,2 (no da árboles), de vez en cuando un grupito de 3 a 10 árboles en 8–20 m de radio, para que la llanura no quede pelada. Como mucho uno por celda de `sotos_celda` (70 m), con `sotos_probabilidad` (0,25) por la fertilidad (de ×0,5 a ×1,5); en tierra firme, fuera de la arena, el agua, los acantilados y las placas. En tres llanuras sin bosque de la mesa salen de 33 a 61 sotos en el campo entero, de 90 a 199 árboles jugables.
- **Decoración del suelo** (mesa, 2026-11-23; H11): hierba, flores, matas, juncos y piedras, con las reglas de las matas del mapa de mundo, un hueco cada 3 m del área jugable y 40 m más (§6.0, paso 13).
  - En el paso del vado, cantos sueltos que asoman del agua; en la bajada al vado, poca hierba.
  - Solo se ve: no cuenta para la táctica ni tiene collider.
  - En un llano salen unas 13 900 hierbas, 4600 matas y 600 piedras. En Unity, la hierba iría mejor como capa de detalle del Terrain.
- Las zonas de bosque (para lo que toque en la táctica): por encima de `umbral_bosque_cerrado`, bosque cerrado, con troncos con collider; por encima de `umbral_bosque_claro`, bosque claro, sin colliders salvo algún tronco.
- Los troncos con collider entran en el horneado del NavMesh: los `NavMeshAgent` de las unidades atraviesan cualquier collider que no esté en la malla (§11, H7).
- Las reglas de dónde no va un árbol (pendiente, agua, roca) son las de `ReglasDeVegetacion`, para que el bosque de batalla se parezca al del mapa de mundo.
- Las sendas se abren en el bosque cerrado: un camino del ticket que lo cruza, o una senda de semilla entre claros.

### 6.7 Caminos

Los tramos del ticket se aplanan a su anchura por escalón (`ancho_sendero`, `ancho_camino`, `ancho_calzada`) y abren paso por el bosque. Zona camino.

### 6.8 Spawns

- **Los spawns son de Conquest** (autor, 2026-11-23). El ticket solo da la dirección por la que llega cada bando, y Conquest coloca los spawns a partir de ella.
- **Los dos bandos arrancan a la misma distancia del centro, en lados opuestos** (autor, 2026-11-23). Ningún bando arranca en el centro. El atacante va en `centro + distancia_al_centro · eje` y el defensor en `centro − distancia_al_centro · eje`.
- **El eje** apunta de la procedencia del defensor a la del atacante.
  - Si un bando estaba quieto, su procedencia es su posición, y el eje sale de la procedencia del que sí la tiene. El quieto va al borde opuesto (autor, 2026-11-23).
  - Si las dos procedencias casi coinciden (los dos llegan del mismo sitio), el eje es la dirección del centro a la procedencia del atacante, y el defensor va al borde opuesto.
  - Si los dos estaban quietos, el eje sale de la semilla del lugar.
- En un vado, cada bando en su orilla. En una ribera, los dos en la misma.
- Los puntos de spawn de cada bando (1-3 del atacante, 4-6 del defensor) se reparten por la placa de su bando, a lo ancho del eje.
- **Cada spawn que se coloca se comprueba** (autor, 2026-11-23): su placa tiene que caer en seco, fuera de acantilado y en la orilla de su bando. Si uno no vale, se gira el eje lo mínimo, moviendo los dos spawns a la vez para que sigan a la misma distancia del centro, hasta que los dos valgan. Si ningún giro vale, se acorta `distancia_al_centro` por igual para los dos.

### 6.9 Validación

Antes de hornear, una inundación sobre la rejilla de zonas transitables comprueba que cada spawn llega al otro.

Si falla, se abre una senda por el bosque por el camino más corto y se vuelve a comprobar. No se abren pasos por el agua. Un campo que no pasa la validación tras `intentos_validacion` cae al tipo llano con el relieve del lugar. Así nunca hay una batalla imposible.

El llano forzado conserva el agua del lugar: el mar, los lagos y los ríos (autor, 2026-11-25). Antes la quitaba, y una batalla junto a un río ancho salía sin él. Solo se relajan las reglas de los spawns: la orilla de cada bando deja de contar y, si el agua no deja llegar por tierra de un spawn al otro, se avisa y el campo se queda como es, igual que en el vado (§6.4).

### 6.10 Cómo se ve (lo que pinta la mesa)

Lo que el montaje de Conquest (§4, pieza 3) tiene que dar para que el campo se parezca al sitio y al mapa de mundo. Son los colores y las medidas de la mesa (`pruebas/campo3d/banco.ts`). En Unity el arte será Synty (§11.1).

- **El suelo:** un color por vértice de 2 m, el del suelo del mapa de mundo (`colorDelSuelo`, `src/mundo3d/terreno.ts` del cliente). Sale de:
  - la altura del mundo (`alturaBase + altura / 6,25`), que decide costa, llano, colina, montaña o cima;
  - la pendiente, que pone roca en las laderas;
  - el bosque, que pone sotobosque;
  - la fertilidad, que pone el verdor del pasto, por manchas.
- **Encima del color del suelo:**
  - **Tierra mojada junto al agua:** `#6b5d40`, hasta 1,2 u del mundo (90 m), oscura junto al agua y desvaída hacia fuera (×0,8 · (1 − suave(0,1 … 1))); no en la arena.
  - **Bajada al vado:** `#b8a57c`, por lo pisado (×0,75).
  - **Bajo el agua:** el lecho, de grava `#c9b98f` donde hay poca agua a `#6f6650` donde cubre, entre 0,6 y 1,6 m.
  - **Fuera del área jugable:** todo un 20 % más oscuro.
- **El agua:** una lámina por vértice de 2 m, no por celda. Un vértice seco junto al agua toma la lámina de su vecino y el suelo lo tapa, así la orilla no sale en escalera.
  - **Color:** de `#a8d8d0` a `#3f8f9c` según cubra de 0,4 a 1,4 m.
  - **Opacidad:** 0,3 + 0,55 · lo de antes.
  - **Espuma** (`#f2f6f4`, rápidos y cascadas): donde la lámina baja más de un 3 %, del todo desde un 15 %.
  - Fuera del área jugable, un 30 % más oscura.
- **Los árboles, las especies del mapa de mundo:** `especieDe`, por la altura, el agua cercana y manchas de unas 22 u, donde manda una especie. El color de la copa es el suyo (`colorDeCopa`).
  - Tamaño: escala 8,5 · (0,75 a 1,25) y alto × (0,9 a 1,2); de 7 a 12 m de alto, con copas de 6 a 10 m.
- **La decoración, con las geometrías y los colores de las matas del mapa de mundo:**
  - Tamaños: hierba 0,35 m, flor 0,3, mata 0,9, junco 1,3 y piedra 0,5, cada pieza × (0,7 a 1,3).
  - Las piedras crecen con la ladera (× (1 + 3 · pendiente)).
  - Los cantos del vado van × 1,8, para que asomen.
- **La niebla, al final del anillo** (mesa, 2026-11-25): color `#9fb6cc`, el mismo que el cielo. Va por la distancia al centro del campo (la del cuadrado, la mayor de |x| y |z|), no a la cámara. Empieza a un cuarto del anillo y lo tapa entero en su borde.
- **Escala:** un soldado de 1,8 m. La mesa pone en cada punto de spawn una escuadra de referencia: 20 soldados, 5 de frente por 4 de fondo, a 1,5 m, con su estandarte.

### 6.11 El azar y el ruido

Todo lo que varía en el campo sale de dos funciones del cliente (`src/mundo3d/ruido.ts`), sin estado: el mismo sitio da siempre lo mismo.

- **`azar(i, j, k)`:** un número entre 0 y 1 por cada terna de enteros. Es un hash: `h = i·374761393 ^ j·668265263 ^ k·2147483647` (productos de 32 bits), luego `h = (h ^ h >>> 13) · 1274126177` y se devuelve `(h ^ h >>> 16) / 2³²`. `k` es la semilla de cada uso.
- **`manchas(x, y, escala, semilla)`:** ruido de valor entre 0 y 1. Los vértices de una rejilla de lado `escala` llevan un `azar`, y en medio se interpola con `suave` (3t² − 2t³).
- **Lo que se mide en metros absolutos** (la posición del mundo × 75) es el mismo en todas las batallas que tocan ese sitio, aunque el recorte se desplace (§8): el detalle fractal, la ondulación mínima, el ruido del bosque, los árboles, los sotos, la decoración, el lecho del vado y la forma de su paso.
- **Las semillas en uso:**

  | Semilla | Para qué |
  |---|---|
  | 11 | Ondulación mínima (220 m) |
  | 20 + n | Octava n del detalle |
  | 13 | Ruido del bosque (90 m) |
  | 21 a 23 | Árboles |
  | 61 a 67 | Sotos |
  | 5, 7, 11, 13, 17 | Decoración |
  | 77 | Mancha de flores (30 m) |
  | 91 y 92 | Forma del paso del vado (40 y 12 m) |
  | 93 | Lecho del vado (6 m) |
  | 31 | Eje con los dos bandos quietos |

- **En Conquest, entero** (§8): `azar` ya es aritmética entera. `manchas` interpola: hay que llevarla a enteros (posiciones en milímetros, interpolación en punto fijo), igual que el resto de la generación.

## 7. Efectos en la táctica

Los modificadores dejan de ser uno por batalla y pasan a ser **por la zona que pisa cada unidad**. `TerrainModifier` sirve tal cual: solo cambia de dónde sale el terreno.

- Un sistema ECS nuevo lee la rejilla de zonas en la posición de cada unidad y escribe su zona actual.
- `StatResolveSystem`, que es el único escritor de stats, aplica los modificadores de esa zona en vez del singleton global de `BattleBootstrap`.
- El héroe en red no pasa por ahí: `MovimientoMandoSystem` usa su propia `Velocidad` y está predicho, así que lee la zona igual en el cliente y en el servidor (§11, H5).
- El enum `BattleTerrain` gana agua somera. La altura no es una zona. Bosque, montaña y nieve quedan sin uso en batalla.

**Decisión del autor (2026-11-22):** bosque y camino no tienen efecto en batalla; afectan al movimiento de campaña, no al combate. En batalla son paisaje: el bosque se dibuja y sus troncos estorban como cualquier obstáculo, sin modificador. La arena también es paisaje. Solo cuenta el vado; la altura ya estaba implementada.

| Zona | Movimiento | Combate | Notas |
|---|---|---|---|
| Altura | Sin efecto nuevo | **Ya implementada; este sistema no la toca.** `DamageFormula.cs:126` | La cresta ya corta la línea de visión: el Terrain lleva collider y `LineOfSight.System` lanza rayos. Hoy no se nota porque el suelo es plano |
| Agua somera (vado, orilla) | Muy lento | Menos defensa; sin bracing | El cuello de botella del vado |
| Agua honda, acantilado, río sin vado | Infranqueable | | Flancos cerrados |
| Bosque, camino, arena | Sin efecto | Sin efecto | Paisaje |

La lentitud del vado va por dos lados a la vez: el coste de área del NavMesh (para que la ruta prefiera rodear) y el stat de velocidad de la zona (para que cruzar cueste de verdad).

Lo infranqueable tiene que frenar también al héroe fuera de red, que se mueve con `CharacterController` y no con el NavMesh (§11, H6).

La IA (escuadras y héroes de la IA) recibe de la generación los sitios clave del campo: el vado, la boca del paso, las crestas. Que el comportamiento Tactician los use queda para después de la v1.

## 8. Determinismo en red

La batalla es de servidor autoritativo, no de pasos sincronizados. Aun así, los seis clientes predicen su héroe sobre su NavMesh, y tienen que ver el mismo campo que el servidor.

- **Cada parte genera el campo por su cuenta** desde la misma entrada: el recorte del ticket, que reciben todos los combatientes. No depende de cachés en disco (ni de un mundo viejo en caché), y no se manda el campo generado por la red.
- **La generación es aritmética entera:** alturas en u16, posiciones cuantizadas, azar de `trazado::azar` portado a C# o SplitMix64. Nada de `Mathf.PerlinNoise` ni de `float` en lo que decide la forma. El ruido es entero, sobre una rejilla. Lo que se reutilice de `Relieve`, `ReglasDeVegetacion` o `CaucesDelMapa` hoy calcula en `double` (§11, H8).
- **La semilla es del lugar, no de la batalla:** hash de `mapa_id` y del centro cuantizado a la celda. El mismo lugar da un campo parecido, y los jugadores pueden aprenderlo (autor, 2026-11-22). El recorte se centra donde está el defensor, así que dos batallas en el mismo sitio pueden salir desplazadas unos metros: basta con que se parezcan, y el centro no se ajusta a ninguna rejilla (autor, 2026-11-23).
- **Huella:** cada cliente calcula la huella del campo y la manda al conectar; el servidor la compara con la suya. Si no casa, se registra y la batalla sigue. El servidor manda igualmente; lo peor es que ese cliente vea correcciones de predicción.
- **NavMesh:** cada parte la hornea de la misma geometría con la misma versión de Unity. Es lo único fuera de nuestro control. Las diferencias pequeñas solo cuestan correcciones, que ya se miden en `MedicionRedSystems.cs:335`.
- La versión del generador va en la huella, para que un cliente desactualizado lo detecte.

## 9. Fuera de esta versión

Esta versión es solo de batallas en campo abierto (autor, 2026-11-23). Lo demás queda para otro momento, con estas notas:

- **Asedio:** hoy lleva su propio mapa (`SettlementBattleSnapshot`) y no tiene centro. Con `centro` y el recorte, se montaría el terreno del lugar alrededor del trazado de la plaza.
- **Plazas, pueblos, aldeas, avanzadas y campamentos:** usan `mapa_referencia`, así que el terreno podría salir igual. Les faltaría la huella del asentamiento en el centro; usar el trazado real del motor (`urbanismo.rs:612, 657`) exigiría mandarlo en el ticket.
- **Avanzadas por etapas:** el doc del ciclo de batalla habla de un mapa interior por etapa. Con este sistema, cada etapa puede ser otro centro.

## 10. Límites de rendimiento

| Paso | Tamaño | Presupuesto |
|---|---|---|
| Lectura y generación | 1000×1000 m; alturas a 2 m (rejilla de 501², Terrain de 513²); zonas a 4 m (250²) | Menos de 300 ms en el servidor y en el cliente |
| Objetos | Árboles como instancias del Terrain (instanciado por GPU) | Hasta `max_arboles` (6000); colliders solo en troncos del área jugable, hasta `max_colliders` (1500) |
| NavMesh | Solo el área jugable, 600×600 m; vóxel de 0,25 (hoy 0,167) | Horneado asíncrono por teselas durante la preparación; objetivo, menos de 5 s. Es el paso con más riesgo: hay que medirlo antes de cerrar nada (§11, H10) |
| Agua | Planos sin física, la orilla somera con material propio | Despreciable |
| Ticket | El recorte, con el anillo de 700 m: unos 54×54 u16 de alturas en la pequeña, 65×65 en la grande; las celdas y unas pocas polilíneas | Unos 7,4 KB en protobuf la pequeña (medido en la mesa) y unos 11 KB la grande (unos 2,5 bytes por muestra); nada que descargar |
| Batalla grande | 2400×2400 m con el anillo de 700 (antes 1900×1900): alturas a 2 m (1201²) | Medido en la mesa (2026-11-25, 1000 m y anillo de 700): de 2 a 6 s generar en el navegador, con los alisados en ventana deslizante; con los de antes (1500 m, anillo de 200), 1 s generar y 1,2 s montar y 9105 árboles jugables en un sitio de colinas, por encima de `max_arboles`. Muy por encima de los 300 ms: el anillo podría generarse a menos resolución |

Todo cabe en la fase de preparación, que ya existe: el cliente genera y hornea mientras el jugador ve la pantalla de preparación, y el minimapa de esa pantalla (`PantallasDeMundo.Preparacion.cs:408`, hoy siempre `DefaultMap`) se pinta de la misma rejilla de alturas y zonas.

## 11. Decisiones y huecos

### 11.1 Decisiones del autor

Del 2026-11-22:

1. **Semilla del lugar:** sí. El mismo lugar da siempre el mismo campo.
2. **600×600 m jugables:** sí. La batalla abarca menos que una provincia: 8 u del mundo estiradas a 600 m (75 m por u).
3. **Vado:** el único paso es justo la gracia. Es agua rala y ralentiza. Los bandos, siempre en orillas opuestas.
4. **Efectos por lo que pisa cada unidad:** solo el vado. La altura ya está implementada (daño por diferencia de altura) y no cambia. Bosque, camino y arena son paisaje.
5. **Contrato con el motor:** sí.
6. **Arte:** de momento Synty; después se encarga.

Del 2026-11-23:

7. **El ticket lleva solo el recorte del campo;** nadie descarga el mundo para la batalla.
8. **El campo es un zoom sobre el sitio:** la altura escala igual que el plano (×6,25) y las pendientes son las del mundo. (El 2026-11-23 se apuntó primero «sin exageración vertical», entendido como no tocar la altura; el autor aclaró que quería el zoom.)
9. **Solo campo abierto:** asedios, plazas, pueblos, aldeas, avanzadas y campamentos, en otro momento.
10. **Spawns:** los dos bandos, a la misma distancia del centro y en lados opuestos. Nunca un bando en el centro. Un bando quieto va al borde opuesto del que llega.
11. **Río sin vado:** los dos bandos en la misma orilla y el río como decoración sin efecto. Un río sin vado con los bandos en orillas opuestas no se da: el mundo no lo permite por distancia y la batalla no tendría sentido.
12. **No se trae dentro un vado que cae fuera, y no se abren pasos someros.**
13. **Objetivos fuera de este plan:** la captura y el suministro los decide el modo de batalla. El campo no los coloca ni los valida.
14. **El ticket trae datos del mapa, no de la batalla:** spawns, suministro, captura y lo demás de la batalla son de Conquest.
15. **Río sin vado:** no se cruza ni se hace nada con él; se coloca como va.
16. **Spawns:** el ticket da la dirección; Conquest los coloca y comprueba que cada uno caiga en un sitio válido. Si no, se gira el eje y, si no basta, se acorta la distancia.
17. **Campo abierto, siempre terrestre:** si más de la mitad del área jugable es agua, el motor desplaza el recorte hacia la tierra.
18. **Primeras pruebas en la mesa 3D de BronzeAgeClient** (§12).
19. **El mismo lugar, un campo parecido:** no hace falta que sea idéntico; el centro del recorte no se ajusta a una rejilla.
20. **Los tipos no dan forma al campo:** el campo es la representación del sitio; los tipos solo son etiqueta y las reglas de orillas del vado y la ribera.
21. **Dos tamaños de batalla por la entidad más grande:** pequeña (persecución, caravana pequeña) de 600 m con 550 m entre bandos; grande (campal, caravana grande) de 1000 m con 900 m entre bandos (cada uno a 450 m del centro; autor, 2026-11-25, antes 1500 m y 650). El zoom no cambia (75 m por u): la grande lee más mundo. En caravana, el tamaño lo marca el motor en el ticket.

Del 2026-11-25:

22. **El anillo de fondo, de 700 m y bajo niebla:** la niebla va por la distancia al centro del campo y tapa el borde, para que no se vea dónde se corta el mapa (§6.1, §6.10).
23. **Las placas de spawn siguen la ladera:** el plano que mejor se ajusta al suelo, no un disco a nivel que cortaba una terraza (§6.2).
24. **El cauce sigue el relieve, sin paredes, y el agua sigue al cauce:** el río no se corta a nivel en la ladera; en las juntas, las aguas coinciden sin rampas de espuma (§6.3).
25. **El río del campo es el del mundo:** el ancho de cada punto, a escala, y los meandros, brazos muertos y trenzas que el mundo tenga (§2.3).
26. **El llano forzado conserva el agua del lugar** (§6.9).

### 11.2 Huecos por atender

H1 (río sin vado), H2 (spawn en sitio no válido) y H9 (el mismo lugar, el mismo campo) quedaron resueltos el 2026-11-23 (§6.5, §6.8 y §8).

| Id | Hueco | Propuesta | Quién |
|---|---|---|---|
| H3 | Los modos de batalla de Conquest (fuera de este plan) aún no están definidos. Mientras tanto, los puntos de captura y suministro de `DOTSWorld.unity` siguen puestos a mano y, con relieve, pueden quedar flotando, enterrados o en el agua | Hasta tener modos, colocar los actuales con la consulta de sitio válido, en sitios simétricos respecto al eje | Conquest |
| H4 | La consulta de sitio válido: qué recibe (un punto deseado, un radio) y qué devuelve | Recibe un punto y un radio máximo; devuelve el sitio seco, llano y alcanzable más cercano, o nada | Conquest |
| H5 | La lentitud del vado no llega al héroe en red: `MovimientoMandoSystem` usa su `Velocidad` y `SamplePosition(AllAreas)`, sin `StatResolveSystem` ni coste de área | Leer la zona en ese sistema (predicho, idéntico en cliente y servidor) y aplicar el factor de la zona a su velocidad | Conquest |
| H6 | El héroe fuera de red (`CharacterController`) no está atado al NavMesh: puede bajar al cauce hondo | Colliders invisibles sobre el agua honda y los acantilados | Conquest |
| H7 | Árboles: los `NavMeshAgent` atraviesan colliders que no estén en la malla, y los colliders de árbol del Terrain se activan para todo el terreno o para nada | Dos prototipos por árbol (con y sin collider); comprobar que `NavMeshSurface` recoge los troncos al hornear, o hornearlos aparte como obstáculos | Conquest (prueba) |
| H8 | Determinismo: `Relieve.AlturaEn`, `ReglasDeVegetacion` y la geometría de `CaucesDelMapa` calculan en `double`; `Relieve` lee de teselas, no del recorte | Portar a enteros lo que decide la forma, o aceptar `double` solo con suma, resta, multiplicación y división (sin `Math.Sin`, `Pow`…). Un adaptador que lea el recorte con la misma interfaz | Conquest |
| H10 | El horneado del NavMesh de 600×600 m en menos de 5 s está sin medir. La mesa 3D no lo cubre: es de Unity | Prueba aislada en Unity: Terrain de 513² con ruido, horneado por teselas con vóxel de 0,25, en el cliente y en el servidor | Conquest (prueba) |
| H11 | La llanura sale pelada: el detalle fractal casi no la toca (sin pendiente no hay lomas), y así debe ser, porque llanura es llanura. El autor, tras probar el detalle (2026-11-23): mejora mucho en montañas y colinas; la llanura se arreglará con decoración | Decoración del suelo del campo (matas, piedras, hierba alta, setos o majanos), con la densidad de la fertilidad y la humedad del ticket, como `matas.ts` del mapa de mundo. **Versión preliminar en la mesa (2026-11-23):** hierba, flores, matas, juncos y piedras con las reglas del mapa de mundo (`probabilidades` de `matas.ts`: altura del mundo, pendiente, fertilidad, bosque y agua cercana), un hueco cada `paso_decoracion` (3 m) en el área jugable y 40 m más; tamaño real (hierba 0,7 m, mata 1,4 m, junco 1,6 m, piedra 0,9 m, mayor en las laderas); nada en el agua ni en las placas. En un llano salen unas 13 900 hierbas, 4600 matas y 600 piedras. En Unity, la hierba iría mejor como capa de detalle del Terrain (por GPU) que como piezas sueltas | Conquest (arte) |

## 12. Primeras pruebas: la mesa 3D de BronzeAgeClient

Las primeras pruebas, para ver cómo quedan el campo, las distancias y los spawns, se hacen en la mesa 3D del repositorio BronzeAgeClient (autor, 2026-11-23), no en Unity.

- **Lo que ya hay:** el banco del mundo 3D (`pruebas/mundo3d/`, `npm run mundo3d`). Es three.js con el artefacto y el relieve reales del motor, sin servidor ni cuenta; los sirve el plugin `bancoMundo3d` de `vite.config.ts` desde `generador/salida/mundo/` del motor. Hoy esa carpeta no existe: hay que generar el mundo en el motor antes.
- **Lo que se añade:** una página hermana (`pruebas/campo3d/`) que:
  - elige un punto del mundo real y hace el recorte como lo haría el motor (13 u, desplazado si más de la mitad es agua);
  - deja marcar la dirección por la que llega cada bando, o un bando quieto;
  - genera el campo con las reglas de §5 y §6 y lo pinta a escala de batalla: 600 m jugables más el anillo, el río, el vado, el bosque, los caminos, las placas de spawn y el eje;
  - mide distancias sobre el campo y deja cambiar los parámetros de §13 en marcha.
- **Lo que no cubre:** el NavMesh, los colliders, el movimiento de las unidades y la red. Eso se prueba después en Unity (H5, H6, H7 y H10).
- **El generador de la mesa es un prototipo en TypeScript para calibrar.** El de verdad es el de C# de Conquest (§4), con aritmética entera. Cuando las reglas y las cifras queden cerradas en la mesa, se portan.

### 12.1 Cómo se lanza (montada el 2026-11-23)

1. En el motor, generar el mundo si no está: `cd generador && npm ci && npm run generar && npx tsx scripts/generar-mundo.ts 42 salida/mundo`.
2. En BronzeAgeClient: `npm ci` (la primera vez) y `npm run campo3d`. Se abre `http://localhost:5174/pruebas/campo3d/`.

**El campo sale solo del ticket.** Cada clic en el mapa hace el ticket como lo haría el motor (`crearTicket`: el recorte de §3 en JSON, con `punto`, el agua por muestra y la procedencia de cada bando con su «quieto»). El campo se genera del ticket pasado a texto y leído otra vez (`leerTicket`), sin tocar el mundo. El panel «Ticket» enseña su tamaño (unos 6 KB en JSON, unos 2 KB en protobuf) y un resumen, y deja verlo, descargarlo, copiarlo y cargar uno de un fichero. Uno cargado avisa si su `mapa_id` no es el del mundo de la mesa, y los parámetros lo regeneran sin rehacerlo. Así, cuando el motor saque tickets de verdad, se podrán probar en la mesa.

Ficheros: `pruebas/campo3d/generador.ts` (el prototipo), `pruebas/campo3d/banco.ts` y `index.html` (la página) y `pruebas/campo3d.test.ts`. La URL guarda el caso (`?x=&y=&ax=&ay=&dx=&dy=`) para compartirlo.

**Dos fases** (2026-11-23):

- **El mundo:** el mundo real a pantalla completa y a escala del héroe, con la vista del modo mundo del cliente (`VistaMundo3D`). WASD mueve al héroe (Mayús corre); las flechas y el arrastre giran la cámara; la rueda hace zoom. Hay botones para saltar a un vado, una costa, una ribera o un llano. Alrededor del héroe se ve el área que ocuparía la batalla si el clic fuera ahí: la jugable (8 u, línea continua) y el recorte del ticket (13 u, discontinua), ya apartadas del agua si hiciera falta.
- **La batalla:** un clic en el terreno abre la batalla ahí, con el defensor quieto en el clic y el atacante llegando desde donde está el héroe. El panel de la izquierda tiene el mundo en miniatura y todo lo de abajo. «Volver al mundo» deja al héroe donde estaba.

Con `?x=…&y=…` en la URL se entra directamente en la batalla de ese caso.

En la fase de la batalla: mapa del mundo (un clic pone el sitio), mapa del lugar de 24 u (un clic pone el sitio o la procedencia de cada bando; «quieto» por bando), botones de vado, costa, ribera y llano al azar, la vista 3D con el área jugable, las placas, los seis puntos, el eje y los vados, una regla de dos clics y los parámetros de §13 en vivo. El informe da el tipo, la lectura del mundo, el rumbo, el giro y la distancia de los spawns, los árboles, el acantilado y los tiempos.

Lo que el prototipo aún no hace: la forma propia de los tipos paso y colinas (valle con acantilados, crestas), el recorte de pendientes a `pendiente_max_transitable` (solo marca acantilado) y los caminos (no están en el artefacto).

### 12.2 Primeras observaciones

- **Una costa empinada o un cerro salían de tipo paso** (resuelto). La pendiente media del mundo, medida cada 0,5 u, pasa de 15° en cualquier loma (un cerro junto al mar daba 17,4°, con 0 % de montaña) o costa con acantilado (31°). El paso ya solo se da entre montañas: el cerro sale colinas y la costa, costa.
- **Con mucha agua, los spawns se acortan.** Con el 44 % de agua (por debajo de la mitad, así que el recorte no se desplaza), la franja de tierra solo deja los spawns a 150 m del centro, no a 275.
- **La clase costa del artefacto es una franja ancha**, de varias celdas tierra adentro. El campo saca la arena de la distancia al agua, no de esa clase.
- **El bosque cerrado a 4 m pasa de `max_arboles` enseguida:** un vado con bosque dio 3718 árboles en el área jugable, y un área jugable entera de bosque cerrado daría unos 19 000.
- **Un defensor pegado a la orilla de un río sin vado no tiene spawns válidos.** Con los spawns a la misma distancia del centro y en lados opuestos, si el centro está a menos de la placa (unos 25 m) del agua honda, uno de los dos cae siempre al otro lado del río. Caso: `?x=1233.00&y=1772.38&ax=1231.72&ay=1773.92`, con el centro a 21 m del río; el campo cae al llano. Hace falta una regla: o el motor aparta el centro del río (como lo aparta del agua, §3), o en la ribera los spawns dejan de ser simétricos.
- **Un bosque denso es caro:** con bosque 0,90 salieron 18 611 árboles en el área jugable (52 020 en total), y generar tardó de 1,1 a 2,6 s.
- **Generar tarda de 175 a 610 ms** en el navegador, por encima de los 300 ms de §10 cuando hay río. Con el anillo de 700 m y la batalla grande (2026-11-25) son de 2 a 6 s, ya con los alisados en ventana deslizante. El anillo se podría generar a menos resolución, si hace falta.
- **Los meandros de antes tapaban el campo** (2026-11-25). Caso `?x=2400.05&y=1724.83&ax=2399.96&ay=1722.83&tipo=caravana_grande`: un río de 4,2 u con lazos más juntos que su propio ancho cubría el 41 % del área jugable de agua honda. Ningún par de spawns tenía las placas en seco, y el campo caía al llano y, además, perdía el río. Con los meandros nuevos del motor (§2.3) sale ribera con spawns y sin avisos; y el llano forzado ya conserva el agua (§6.9).

## 13. Parámetros (placeholders)

| Parámetro | Valor | Qué es |
|---|---|---|
| `escala_campo` | 75 m/u | El zoom: metros del campo por u del mundo (el mapa de mundo va a 12 m/u). La altura escala igual |
| `lado_pequena` / `lado_grande` | 600 m / 1000 m | Lado del área jugable de la batalla pequeña y de la grande; lo que se lee del mundo es el lado entre `escala_campo` (8 u / unas 13 u) |
| `distancia_pequena` / `distancia_grande` | 275 m / 450 m | Distancia de cada spawn al centro, la misma para los dos bandos (550 m / 900 m entre ellos) |
| `anillo_fondo` | 700 m | Terreno no jugable alrededor, bajo la niebla; el recorte es (lado + 2 × anillo) / `escala_campo` (unas 27 u / 32 u) |
| `distancia_procedencia` | 2 u | Cuánto se retrocede sobre el plan para la procedencia |
| `relieve_minimo` | 8 m | Diferencia mínima de altura en el área jugable |
| `detalle_pendiente` | 0,3 | Detalle fractal: altura de cada octava por metro de largo y por unidad de pendiente del mundo (m por m, con tope en 0,35) |
| `detalle_longitud` / `detalle_persistencia` | 400 m / 0,5 | La octava más larga y lo que pesa cada octava respecto a la anterior (hasta unos 10 m) |
| `detalle_amplitud` / `detalle_rugosidad` | 2 m / ×1 | Base por clase de terreno (×0,5 llano, ×1,5 colina, ×2,5 montaña) y peso de la rugosidad del mundo, repartidos entre las octavas |
| `acento_relieve` | ×1 | Solo en la mesa: cuánto crecen las crestas y vaguadas respecto a su entorno (1 = como en el mundo; con el zoom no hace falta) |
| `radio_acento` | 3 u | Solo en la mesa: el entorno del acento |
| `pendiente_max_transitable` | 45° | Por encima, acantilado (lo que admite el agente) |
| `radio_placa` | 25 m | Placas llanas de spawn |
| `paso_alturas` / celda de zonas | 2 m / 4 m | |
| `relieve_colinas` | 15 m del mundo | |
| `bosque_campo` / `bosque_cerrado` | 0,3 / 0,7 | Densidad media para el tipo bosque |
| `umbral_bosque_claro` / `umbral_bosque_cerrado` | 0,25 / 0,6 | Por celda |
| `celda_bosque` / `separacion_cerrado` | 8 m / 4 m | |
| `ancho_rio_mundo` / `altura_orilla` | 1,5 u (unos 112 m; el doble si es navegable) / 2 m | Ancho del río cuando el ticket no trae el de cada punto (§3), por la escala del campo; y lo que baja la hondonada del cauce, más 1,5 m bajo la lámina (§6.3) |
| `ancho_vado_mundo` | 0,8 u (60 m) | Ancho del paso del vado en el mapa de mundo, por la escala del campo. Único paso |
| `ancho_playa` / `ancho_somero` | 60 m / 25 m | |
| `ancho_sendero` / `ancho_camino` / `ancho_calzada` | 3 / 5 / 8 m | |
| `intentos_validacion` | 3 | |
| `max_arboles` / `max_colliders` | 6000 / 1500 | |
| `paso_decoracion` | 3 m | Decoración del suelo (mesa): un hueco cada tantos metros; cada hueco, una pieza o nada según el sitio |
| `sotos_celda` / `sotos_probabilidad` | 70 m / 0,25 | Sotos donde el bosque del mundo no da árboles: uno como mucho por celda, con esa probabilidad (por la fertilidad) |
