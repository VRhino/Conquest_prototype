# Servidor de batalla en red: prueba con Netcode for Entities (2026-11-11)

Primer paso hacia el servidor de batallas: Conquest como servidor dedicado (Netcode for Entities) que juega un ticket del
motor de BronzeAge y le devuelve el resultado. Es una prueba aislada: el juego actual no cambia.

## 1. Qué hay

| Pieza | Dónde | Qué hace |
|---|---|---|
| Paquete | `Packages/manifest.json` | `com.unity.netcode` 6.5.0 en lugar de `com.unity.netcode.gameobjects` (nadie lo usaba) |
| Arranque | `Assets/Scripts/Net/ArranqueRed.cs` | `ClientServerBootstrap` propio. Sin `-red` y fuera de la escena de prueba crea el mundo local de siempre. Con red, los mundos de servidor y cliente llevan solo los sistemas de Unity y los de `Conquest.Net`: los de `Game.ECS` aún no separan servidor y cliente |
| Prefabs fantasma | `PrefabsRedSystem.cs` | Héroe y soldado creados por código, iguales en servidor y cliente (sin subescena) |
| Héroe | `MovimientoRedSystems.cs`, `ClienteRedSystems.cs` | El cliente solo manda la dirección (`EntradaHeroe`); `MovimientoHeroeSystem` corre en el servidor y, predicho, en el cliente dueño. Sustituye a `LocalHeroCharacterMotor` en la red |
| Soldados | `MovimientoRedSystems.cs` | El servidor los mueve a su hueco detrás del héroe; el cliente los recibe interpolados |
| Batalla de ticket | `BatallaDeTicket.cs`, `BatallaDeTicketSystems.cs` | Lee un ticket del motor, coloca bandos, combate simple, cierra por aniquilación o tiempo y escribe el `BattleResult` |
| Contrato | `Assets/Plugins/BronzeAge/` | `BronzeAge.Contrato.dll` (los `.proto` del motor compilados) y `Google.Protobuf.dll`. Se regeneran con `Tools/ContratoBronzeAge/actualizar.ps1`, que lee los `.proto` del checkout del motor (`../BronzeAgeFase0/proto`) |
| Medición | `MedicionRedSystems.cs`, `Tools/medir_red.sh` | Bytes por la red (Unity Transport), coste del tick, CPU, RTT y cuántas veces por segundo llega cada soldado |
| Escena y ejecutable | `Assets/Scenes/NetTest/PruebaRed.unity`, menú `Conquest/Red` | `PruebaRedEditor.Construir` deja `Builds/PruebaRed/PruebaRed.exe` |

Al abrir `PruebaRed.unity` y darle a Play, el editor debería crear servidor y cliente en el mismo proceso (WASD mueve al
héroe, amarillo, y sus soldados le siguen). **Sin probar en el editor**: lo medido es con el ejecutable.

## 2. Cómo se lanza

```
Builds/PruebaRed/PruebaRed.exe -batchmode -nographics -red servidor -puerto 7979 [-soldados 20] [-heroesExtra 28] [-tasaRed 60] [-paquete 0]
Builds/PruebaRed/PruebaRed.exe -red cliente -ip 127.0.0.1 -puerto 7979 [-bot]
Tools/medir_red.sh <nombre> <clientes> <soldados> <héroes extra> [tasa] [segundos] [bytes por snapshot]
```

Batalla de ticket (sin motor):

```
PruebaRed.exe -batchmode -nographics -red servidor -ticket <MensajeDeServidor .json|.bin> -resultado resultado.bin -intento a1 [-duracion 120] [-espera 15]
```

Las conexiones toman los héroes humanos del ticket por orden de llegada; a los `-espera` segundos empieza con la IA en
los que falten. Al cerrar escribe `resultado.bin` (un `MensajeDeCliente` con `batalla.resultado`, como lo mandará al
motor) y `resultado.json`, comprueba el resultado con las reglas del motor y sale.

## 3. Medidas (este PC, servidor y dos clientes en local, 30 s)

Un héroe por jugador; los héroes extra son del servidor y dan vueltas, para llenar un 15 contra 15 (30 héroes).
«Actualizaciones» es cuántas veces por segundo llega de verdad cada soldado al cliente.

| Caso | Fantasmas | Tasa | Snapshot | Por cliente (bajada) | Actualizaciones por soldado | Servidor (subida total) | Tick del servidor |
|---|---:|---:|---|---:|---:|---:|---:|
| 2 héroes × 20 | 42 | 60 | MTU | 108 kbit/s | 60 Hz | 209 kbit/s | 0,09 ms |
| 30 héroes × 20 | 630 | 60 | MTU | 685 kbit/s | 31 Hz | 1,3 Mbit/s | 0,13 ms |
| 30 héroes × 60 | 1.830 | 60 | MTU | 684 kbit/s | 7,3 Hz | 1,3 Mbit/s | 0,17 ms |
| 30 héroes × 60 | 1.830 | 30 | MTU | 342 kbit/s | 2,8 Hz | 0,7 Mbit/s | 0,18 ms |
| 30 héroes × 60 | 1.830 | 30 | 8 KB | 1,78 Mbit/s | 30 Hz | 3,5 Mbit/s | 0,17 ms |
| 30 héroes × 60 | 1.830 | 20 | 8 KB | 1,31 Mbit/s | 19 Hz | 2,5 Mbit/s | 0,16 ms |

La subida del cliente (sus órdenes) es de 60 kbit/s en todos los casos. El RTT local, 16-30 ms.

**Lo que dicen:**
- Cada actualización de un soldado cuesta unos 4 bytes (posición y giro, comprimidos por delta). Lo que limita es el
  tamaño del snapshot: con el de Netcode por defecto (un MTU, ~1.400 bytes) cabe todo hasta unos 600 fantasmas a 30 Hz;
  con 1.800 cada soldado se actualiza solo 3-7 veces por segundo.
- Con todos los soldados frescos, un 15 contra 15 de 1.800 soldados pide 1,3-1,8 Mbit/s por jugador (20-30 Hz). Con 30
  jugadores conectados, el servidor sube 40-55 Mbit/s. Cabe en una máquina con un enlace normal, pero no es barato.
- El tick del servidor apenas cuesta porque la prueba no tiene combate real. El combate de Conquest (detección,
  targeting, bodyblock, NavMesh) es lo que decidirá cuántas batallas caben por máquina; hay que medirlo cuando entre en red.

**Lo que bajaría la cuenta** (sin hacer): relevancia (no mandar lo que el héroe no ve, que además tapa la niebla),
importancia por distancia (`GhostDistanceImportance`: lo cercano a 30 Hz, lo lejano a 5), mandar del soldado solo
posición y rumbo (no el cuaternión entero) y, en último término, mandar la escuadra como bloque y que el cliente coloque a
cada soldado en su hueco.

## 4. La batalla de ticket

Jugada con el dorado `MensajeDeServidor__batalla_ticket_de_caravana_con_dos.json` (20 bandidos contra `heroe-3` con 25
milicianos): con la IA sola y con un cliente que toma `heroe-3`, gana el defensor por aniquilación en 10-13 s y el
resultado pasa la comprobación. `BatallaDeTicket.Comprobar` repite lo que el motor valida y Unity puede saber
(`combate::resultado::validar_resultado`, puntos 2 a 6 y 8): ids sin repetir, todas las escuadras y héroes, cada escuadra
cierra sobre lo autorizado, tiempo agotado solo lo gana el defensor, duración en plazo y carros que existen. Pruebas en
`Assets/Tests/EditMode/BatallaDeTicketTests.cs`, contra el dorado del checkout del motor.

**Experiencia y botín:** no van en el resultado. Desde el 2026-11-12 los pone el motor (D7; `xp_ganada` y `botin` son
`reserved` en el contrato).

**Provisional:** si el atacante gana un asalto de caravana se pierden todos los carros; el combate es de prueba (todos
contra el más cercano, sin formaciones ni las reglas de daño de Conquest).

## 5. Tropas del motor (CQ-003)

El servidor de batalla busca cada escuadra del ticket por su `tropaId` entre las `SquadData` de `Resources/Squads`
(`TropasRed`). Si falta una, **rechaza la batalla entera** (sale con código 3, sin resultado): nunca juega con una tropa
por defecto. Los soldados siguen a su héroe en la formación de reposo de su tropa (`FormacionDeReposo`), una escuadra
detrás de otra.

Las cuatro primeras, creadas con `Tools/BronzeAge/Crear tropas del motor` (`TropasBronzeAgeSetup`) en
`Assets/Resources/Squads/BronzeAge/<tropaId>/`, con las unidades y el escalón del catálogo del motor:

| `tropaId` | Soldados | Escalón | Base provisional | Reposo |
|---|---:|---:|---|---|
| `granjeros` | 30 | 1 | Spearmen | disperso (2,4 m, mucho ruido) |
| `milicia_lanceros` | 25 | 2 | Spearmen | bloque de 5, suelto (1,25 m) |
| `hoplitas_ciudadanos` | 18 | 3 | Spearmen | bloque de 6, apretado (1,05 m) |
| `arqueros` | 18 | 3 | Levy Archers | bloque de 6 (1,4 m) |

Cada una tiene su propio módulo cuerpo a cuerpo o a distancia (copia del de la base, libre de cambiar) y comparte con
la base el prefab, las animaciones, las formaciones de F1-F4 y las habilidades. No están en la `SquadDatabase`: el
juego no las ofrece aún. El dorado `MensajeDeServidor__batalla_ticket_de_caravana_con_dos.json` usa `bandidos`, que no
es una tropa del catálogo del motor, y por eso se rechaza; con `granjeros` en su lugar se juega entero.

## 6. El lanzador del motor

El motor arranca un proceso de Conquest por batalla (BronzeAge `crates/lanzador`, Batalla_Unity_Ciclo §8.8, fusionado en
su `main` en `e89f8d3`) con `--batalla <uuid> --lanzador 127.0.0.1:<p> --puerto <n>` detrás de sus argumentos fijos
(`LANZADOR_ARGUMENTOS`, aquí `-batchmode -nographics -espera 60` o lo que se quiera). Con `--lanzador`:

- `EnlaceLanzador` se conecta a ese socket TCP; los mensajes son Protobuf delimitados (`WriteDelimitedTo`/`ParseDelimitedFrom`).
- Llegan el `ticket` y la `asignacion` (un token por humano). Un ticket nuevo antes de empezar (revisión subida) vuelve a
  preparar la batalla. Los `tokens` posteriores se añaden; las `incorporacion` se registran pero aún no se juegan.
- Cada cliente se presenta con `-token <t>` (va en la RPC `EntrarEnBatalla`). Si el token casa con un héroe libre del
  ticket y no ha caducado, lleva a ese héroe; si no, se le desconecta. A los `-espera` segundos empieza con la IA en los
  que falten.
- Manda `inicio` al empezar y `resultado` al cerrar, con el `intento_asignacion_id` de la asignación, y sale con 0 al
  llegar el acuse del resultado (2 si el motor lo rechaza). Una tropa desconocida (3) o el socket cerrado (4) salen sin
  resultado: el motor vence la batalla por su plazo, sin castigo.

**Probado** con `Tools/LanzadorDePrueba` (C#, hace con el proceso lo mismo que el lanzador del motor, sin motor): solo
IA, con un cliente con su token (lleva a `heroe-3`) y con un token falso (se le desconecta y juega la IA); en los tres
casos llega inicio y resultado y el proceso sale con 0.

**Probado con el motor y el lanzador reales** (2026-11-11): dos pruebas en BronzeAge, `crates/lanzador/tests/lanzador.rs`
(`con_conquest_*`), con el núcleo, su WebSocket, el acceso y el lanzador de verdad sobre una retícula, y este
`PruebaRed.exe` como proceso. Sin cliente, la IA juega a Ana y el núcleo aplica el resultado (Ana pasa de 43 soldados a
23). Con cliente, Ana entra en el motor como jugadora, recibe su `asignacion_propia` (host, puerto y token), su cliente de
Conquest se conecta con ese token, el servidor le da `heroe-2` y el resultado se aplica igual. Solo corren con
`CONQUEST_BATALLA=<ruta a PruebaRed.exe>`; sin ella se saltan:

```
CONQUEST_BATALLA=C:/Users/VRINO/Desarrollo/Conquest_prototype/Builds/PruebaRed/PruebaRed.exe cargo test -p lanzador --test lanzador con_conquest -- --test-threads=1
```

El contrato (`Assets/Plugins/BronzeAge/BronzeAge.Contrato.dll`) se compiló con los `.proto` de BronzeAge `457c278` (2026-11-12).
`Tools/ContratoBronzeAge/actualizar.ps1` lee los de `../BronzeAgeFase0/proto`: ese checkout tiene que estar en un
`main` que incluya el PR #18, o el contrato pierde los mensajes del lanzador.

## 7. Lo que falta

1. **Meter el juego en red.** Los sistemas de `Game.ECS` corren hoy en un solo mundo. Para el servidor hay que decidir
   cuáles son de servidor (los `[ServerAuthoritative]` y todo lo que decide daño o muerte), cuáles de cliente (entrada,
   cámara, animación, `EntityVisualSync`) y cuáles predicen; y pasar el héroe a un controlador de personaje de Unity
   Physics, porque `CharacterController` no corre dentro de la predicción. Es el grueso del trabajo.
2. **Formación elegida.** El ticket ya trae `formacion` y `formaciones_permitidas` (familias del motor, que casan una a
   una con `FormationType`: reposo = `Rest`, linea, columna, dispersa, muro_escudos, testudo, cuadro, cuna); el servidor
   de prueba aún no las usa.
3. **Las incorporaciones a media batalla y las salidas** (`salida` cuando un jugador se va o cae), y una partida de
   verdad desde el panel, con el cliente en pantalla (§6).
4. **Servidor de verdad.** La prueba usa el reproductor normal en `-batchmode -nographics`: el módulo Dedicated Server
   de Unity no está instalado en este PC. Para Linux hace falta ese módulo.
5. **Predicción medida.** El héroe se predice y se corrige, pero no se ha medido el error de predicción ni probado con
   latencia y pérdida simuladas.

## 8. El combate real en el servidor: camino A (2026-11-12)

El combate de Conquest no es solo ECS: los golpes cuerpo a cuerpo los detectan los colliders de las armas animadas
(`WeaponHitboxBehaviour.OnTriggerStay` → `MeleeContactQueue` → `MeleeContactResolutionSystem`), los soldados se mueven con
`NavMeshAgent` (`UnitNavMeshSystem`, `NavMeshPositionSync`) y el héroe con `CharacterController`. Decisión del autor
(2026-11-12): **camino A**, el servidor de batalla corre también esa capa de GameObjects, sin pantalla, y los clientes
reciben el resultado. Rehacerlo en ECS puro (camino B) perdería la navegación y el combate por colisión tal como son.

**Lo que cuesta** (`BattleSceneServerCostTests`, batalla de 10 contra 10: 240 soldados y 20 héroes, IA contra IA, en el
editor con `-batchmode -nographics`, Burst encendido, 45 s desde el primer choque):

| | ms por fotograma (media) |
|---|---:|
| Fotograma entero | 16,0 |
| Sistemas ECS (`SimulationSystemGroup`) | 8,9 |
| MonoBehaviours (`Update`) | 1,7 |
| Físicas (`FixedUpdate`) | 1,5 |
| Animators | 1,4 |
| NavMesh | 0,4 |

- La capa de GameObjects que añade A (Animators, físicas, NavMesh, MonoBehaviours) son unos 5 ms; lo que más pesa son los
  sistemas ECS del combate, que corren en el hilo principal. Eso no depende de A o B.
- Con estas cifras, una batalla de 240 soldados cabe a 60 Hz. Una de 1.800 no: hay que bajar el tick del servidor (30 Hz),
  medir en ejecutable y optimizar los sistemas ECS más caros (falta sacar cuáles; los marcadores por sistema no salen
  con `ProfilerRecorder`).
- Los Animators de las armas ya van en `AlwaysAnimate` (`EntityVisualSync.ProtectAnimatedGameplayHitboxes`), así que sin
  cámara sigue habiendo golpes: en la medida hubo 30-40 soldados heridos.

### 8.1 La batalla de verdad en el mundo servidor (paso 2b-2)

```
Builds/PruebaRed/PruebaRed.exe -batchmode -nographics -red servidor -batallaReal 10 [-medir 150 -informe informe.txt]
```

Con `-batallaReal N` el mundo servidor de Netcode lleva, además de los sistemas de Unity y de `Conquest.Net`, todos los
de `Game.ECS`, y `BatallaRealSystem` monta una batalla de N héroes por bando (todos de IA, cada uno con una escuadra
`sqd01`) y carga `BattleScene`. La escena, sus subescenas y sus GameObjects (agentes, Animators, colliders de armas) viven
en ese proceso; no hay jugador local. Cada 10 s el registro dice cuántos soldados quedan por bando, qué hacen los héroes
y a qué distancia están los bandos.

Lo que hubo que tocar en el juego, y nada más:

- `BattleBootstrapRequests.NoLocalHero`: con él, `BattleBootstrapSystem` arranca la partida (fase, reloj, terreno) sin
  buscar al héroe local, `HeroSpawnSystem` no crea ninguno, y `BattleSceneController` saca **todos** los héroes de la
  batalla como remotos (IA) y no pasa a la escena de después de la batalla.
- Cuatro sistemas (`DataContainer`, `GameState`, `MatchController`, `Chat`) creaban su singleton en
  `World.DefaultGameObjectInjectionWorld`; ahora en su propio mundo, que en el juego de un jugador es el mismo.

**Medida** (10 contra 10, 240 soldados y 20 héroes, este PC):

| | Tick del servidor (`SimulationSystemGroup` entero) | Ticks por segundo |
|---|---:|---:|
| Editor, `-batchmode -nographics` (`BatallaRealServidorTests`, 45 s desde el choque) | 8,0-9,9 ms | 57-60 |
| Ejecutable sin pantalla, 150 s | **4,1 ms** de media (peor 14,2) | 60 |

- En el ejecutable los bandos chocan a los 35 s y a los 150 s quedan 84 y 98 soldados de 120: el combate por colliders y
  la navegación funcionan en el proceso servidor tal como en el juego.
- El ejecutable cuesta la mitad que el editor. Con 4 ms por 240 soldados, y si el coste creciera en proporción, 1.800
  soldados serían unos 30 ms: a 30 Hz cabría justo, antes de optimizar nada. Falta medirlo de verdad con ese tamaño.
- El proceso gasta 1,8 núcleos (hilos de trabajo, Animators y físicas incluidos).
- La IA de los héroes va a capturar zonas y los bandos chocan cuando se encuentran: según los puntos de salida que toquen,
  el primer choque llega entre los 35 y los 90 s, y en una de tres pruebas del editor no llegó en 2 minutos.
- Al cerrar el proceso, `CapturePointZoneIndicator.OnDestroy` lanza una `NullReferenceException` (libera una consulta de
  un mundo ya destruido). No afecta a la batalla; queda anotado.

Lo que **no** hace todavía: no la juega un ticket del motor (hoy `-batallaReal` y `-ticket` son dos caminos distintos) y
no escribe resultado.

### 8.2 Los clientes ven la batalla: fantasmas espejo (paso 2b-3, primera parte)

```
Tools/ver_batalla_real.sh <nombre> <héroes por bando> [segundos del servidor] [segundo en que entran los clientes]
```

`EspejoSystem` (servidor) da a cada soldado y a cada héroe de la batalla de verdad un **fantasma espejo**: una entidad
aparte, con el prefab fantasma de soldado, que copia su `LocalTransform` al final de cada tick. Solo los espejos viajan
por la red. Los sistemas de `Game.ECS` y sus entidades no llevan ningún componente de Netcode, y lo que el cliente
necesite saber de más (tropa, animación) se añadirá al espejo, no al juego. Cuando el de verdad muere, su espejo se borra
(`Espejo` es un componente de limpieza). `SoldadoRed.Tipo` dice si el espejo es de soldado o de héroe.

El cliente que entra solo mira (no recibe héroe) y pinta los espejos como cápsulas sobre el plano de la escena de prueba,
con la cámara encima del centro de la batalla. `-captura ruta.png -capturaA 20` guarda una captura a los 20 s.

**Medida** (10 contra 10; servidor sin pantalla y dos clientes en este PC, que entran en el segundo 60):

| | |
|---|---:|
| Fantasmas al entrar los clientes | 248 (228 soldados vivos y 20 héroes) |
| Bajada por cliente | 280-295 kbit/s |
| Veces por segundo que llega cada soldado | 60 en el cliente sin pantalla; 47 de media en el de ventana (sin mirar aún por qué) |
| Tick del servidor con dos clientes | 4,7 ms (peor 15,9) |

- Son unos 1,2 kbit/s por fantasma, menos que en la prueba de cápsulas (2,6 a 60 Hz, §3): los que están quietos casi no
  ocupan. Bajarlo más (menos envíos para los lejanos, relevancia) es el paso 4.

### 8.3 El cliente con el mapa y los modelos (paso 2b-3, segunda parte)

```
Tools/ver_batalla_real.sh <nombre> <héroes por bando> [segundos] [segundo de entrada] [-camaraCerca]
PruebaRed.exe -red cliente -ip 127.0.0.1 -batallaReal 1 [-camaraCerca] [-captura ruta.png -capturaA 70]
```

Con `-batallaReal` también en el cliente, `EspejoClienteSystem` carga `BattleScene` (el mapa y el registro de modelos) y
da a cada soldado espejo **el mismo modelo y los mismos Animators que el juego**, sin tocar el juego:

- El espejo es ahora un tercer prefab fantasma (`PrefabsRed.Espejo`) que, además de posición y bando, lleva `VisualRed`
  (el nombre del modelo y el tipo de escuadra; solo viaja al aparecer) y `AnimacionRed` (velocidad, postura, fila y
  papel en la formación, si ataca, dispara o recarga, la reacción al golpe y su revisión). El servidor lo rellena cada
  tick con lo mismo que `UnitAnimationAdapter` lee del soldado de verdad.
- En el cliente, el fantasma recibe los componentes que leen `EntityVisualSync` y `UnitAnimationAdapter`
  (`UnitAnimationMovementComponent`, `UnitCombatComponent`, `RangedAttackStateComponent`, `ResilienceComponent`,
  `UnitMovementGearComponent`) y el sistema los rellena con lo que llega. El modelo se instancia desde
  `VisualPrefabRegistry` como en `SquadVisualManagementSystem`, pero sin agente de navegación ni colliders de golpe: el
  cliente no decide nada. La dirección de la marcha no viaja; sale del movimiento del fantasma ya interpolado.
- Cuando el soldado muere en el servidor, su fantasma desaparece y `EntityVisualSync` destruye el modelo, como en el juego.

**Comprobado** (10 contra 10, servidor sin pantalla, un cliente sin pantalla y otro con ventana): el de ventana pinta el
mapa de la batalla y 214 soldados con su modelo, en formación y en postura de combate; dos capturas en
`Logs/red/<nombre>/`. Con la animación, la bajada sube a 350-370 kbit/s por cliente (antes 280-295) y el tick del servidor
queda en 4,5-4,9 ms.

**Los héroes** van igual, con un cuarto prefab fantasma (`PrefabsRed.EspejoHeroe`) que añade `AparienciaRed`: el género,
las cuatro partes del avatar y las seis piezas del equipo en un texto corto (`id:tipo` separados por `|`, unos 100
caracteres; el JSON entero del héroe son 2 KB), que solo viaja al aparecer. El cliente instancia `Player_Remote`, lo viste
con `HeroVisualAppearanceSystem.ApplyHeroVisualCustomization` y le pone los componentes que lee
`RemoteHeroAnimationDriver`. Su `AnimacionRed` lleva además la marcha (del agente de navegación del servidor), un
contador de golpes (cada golpe nuevo dispara la animación de ataque en el cliente), la muerte con su revisión y si el
cuerpo está retirado hasta reaparecer. Único cambio en el juego: `RemoteHeroAnimationDriver` saca la marcha de
`HeroNetworkMotion` cuando el héroe no tiene agente (en el cliente nunca lo tiene). Comprobado: 245 de 245 fantasmas con
su modelo, héroes incluidos, y ninguna excepción en servidor ni clientes.

Lo que falta de este paso, anotado:

- Del héroe no viajan todavía la montura (`HeroMountPresenter`), las habilidades ni hacia dónde mira la cabeza.

- La animación está comprobada en capturas (posturas correctas), no vista en movimiento por una persona.
- El cliente de ventana gasta 3-5 núcleos con 214 modelos; no se ha mirado por qué (los Animators, las sombras o la
  interfaz de un jugador, que sigue en pantalla aunque el espectador no tenga héroe).
- El registro no conoce el modelo `UnitVisual_Escudero` que piden los escuderos y cae al modelo por tipo de escuadra
  (pasa igual en el juego); los dos bandos se ven iguales.
- Proyectiles, efectos y sonido de los golpes no viajan todavía.

### 8.4 El jugador lleva un héroe (paso 2b-4)

```
Tools/ver_batalla_real.sh <nombre> <héroes por bando> [segundos] [segundo de entrada] [-bot]
PruebaRed.exe -red cliente -ip 127.0.0.1 -batallaReal 1        (se juega con el teclado)
```

El que entra en la batalla de verdad ya no solo mira: recibe uno de los héroes de la batalla.

- Al entrar, el servidor le crea una **entidad de mando** (`PrefabsRed.Mando`, un fantasma predicho por su dueño que
  solo lleva `EntradaJugador`): mover (en el plano del mundo), correr, atacar, lanzar una habilidad y dar una orden a
  la escuadra. Viaja como comando de Netcode, con sus eventos (`InputEvent`) para lo que pasa una sola vez.
- `MandoSystem` (servidor) le da un héroe libre (por orden de llegada, alternando bando), le apaga las dos conductas de
  la IA y, cada tick, escribe en su `HeroAIDecision` lo que pide el jugador: empujar en una dirección es «ve 4 m hacia
  allí», y atacar, la habilidad y la orden van en los mismos campos que usa la IA. De ahí en adelante el juego no
  distingue: mismo movimiento por la malla de navegación, mismos golpes por collider, mismas órdenes a la escuadra.
  **No se ha tocado ningún sistema del juego.** Si el jugador se va, el héroe vuelve a la IA.
- El espejo del héroe lleva `SoldadoRed.Dueno` (el id de red de quien lo lleva): el cliente sabe cuál es el suyo y pone
  la cámara encima.
- Teclas del cliente, las del juego: WASD, Mayús corre, clic ataca, Q/E/R/T habilidades, C/X/V seguir, mantener y
  atacar para la escuadra.

**Comprobado** con dos clientes que juegan solos (`-bot`: empujan en ocho, atacan, dan una orden cada 8 s y lanzan la Q
cada 5 s), uno por bando: el servidor mueve cada héroe según su entrada, cuenta sus golpes (unos 5 cada 10 s) y recibe
cada orden y cada habilidad una sola vez; en una de las pruebas los dos héroes se encontraron y uno mató al otro. Con dos
jugadores: 262 fantasmas, 370-410 kbit/s de bajada y 60 kbit/s de subida por cliente, tick del servidor 5,2-5,6 ms.

Lo que falta, anotado:

- **Sin predicción**: el héroe responde con el retraso de la red (en local no se nota; por internet sí). Es el paso 3.
- El héroe del jugador se mueve como los de la IA (agente de navegación), no con el control fino del juego de un
  jugador (`LocalHeroCharacterMotor`): gira hacia donde camina y no apunta con la cámara.
- La cámara es fija, detrás y arriba; la del juego (órbita con el ratón) y la interfaz (vida, habilidades, escuadra)
  siguen sin atarse al héroe del jugador: lo que se ve en pantalla es la interfaz vacía de un jugador.
- Las formaciones (F1-F4), montar, bloquear y el modo táctico no viajan todavía.
- Nadie ha jugado con el teclado: lo comprobado es con bots.
