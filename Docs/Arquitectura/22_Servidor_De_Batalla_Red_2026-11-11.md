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
  la IA y, cada tick, escribe en su `HeroAIDecision` lo que pide el jugador: atacar, la habilidad y la orden van en los
  mismos campos que usa la IA. De ahí en adelante el juego no distingue: mismos golpes por collider, mismas órdenes a la
  escuadra. **No se ha tocado ningún sistema del juego.** Si el jugador se va, el héroe vuelve a la IA. El movimiento va
  aparte, predicho (§8.5).
- El espejo del héroe lleva `SoldadoRed.Dueno` (el id de red de quien lo lleva): el cliente sabe cuál es el suyo y pone
  la cámara encima.
- Teclas del cliente, las del juego: WASD, Mayús corre, clic ataca, Q/E/R/T habilidades, C/X/V seguir, mantener y
  atacar para la escuadra.

**Comprobado** con dos clientes que juegan solos (`-bot`: empujan en ocho, atacan, dan una orden cada 8 s y lanzan la Q
cada 5 s), uno por bando: el servidor mueve cada héroe según su entrada, cuenta sus golpes (unos 5 cada 10 s) y recibe
cada orden y cada habilidad una sola vez; en una de las pruebas los dos héroes se encontraron y uno mató al otro. Con dos
jugadores: 262 fantasmas, 370-410 kbit/s de bajada y 60 kbit/s de subida por cliente, tick del servidor 5,2-5,6 ms.

Lo que falta, anotado:

- El héroe del jugador gira hacia donde camina y no apunta con la cámara, como los de la IA; no lleva el control fino
  del juego de un jugador (`LocalHeroCharacterMotor`).
- La cámara es fija, detrás y arriba; la del juego (órbita con el ratón) y la interfaz (vida, habilidades, escuadra)
  siguen sin atarse al héroe del jugador: lo que se ve en pantalla es la interfaz vacía de un jugador.
- Las formaciones (F1-F4), montar, bloquear y el modo táctico no viajan todavía.

### 8.5 El héroe del jugador responde al instante: predicción sobre la malla de navegación (paso 3)

El plan decía pasar el héroe a Unity Physics para poder predecirlo. Se ha hecho de otra forma (propuesta al autor el
2026-11-12, pendiente de su visto bueno): el héroe del jugador se mueve **sobre la malla de navegación** de la escena de
batalla, que es la misma en el servidor y en el cliente, dentro de la predicción de Netcode. No hay que rehacer los
colliders del mapa en Unity Physics, y los soldados ya se mueven por esa malla.

- La entidad de mando lleva también la posición del héroe (`LocalTransform`) y `MandoMovil` (sus velocidades de paso y
  de carrera y si puede moverse). `MovimientoMandoSystem` corre en `PredictedSimulationSystemGroup` en los dos lados:
  avanza según la entrada y deja el paso en el punto de la malla más cercano (`NavMesh.SamplePosition`), así que junto a
  una muralla resbala por su borde en vez de atravesarla.
- En el servidor, `MandoSystem` pone al héroe de verdad donde está su mando (`NavMeshAgent.Warp`) y le da su marcha para
  la animación (`HeroNetworkMotion`). Si el juego mueve al héroe por su cuenta (un empujón, reaparecer tras caer), el
  mando se va con él y el cliente recibe la corrección. Caído, el mando no se mueve.
- En el cliente, el fantasma del héroe propio se pone cada fotograma donde está su mando predicho
  (`EspejoClienteSystem.AdelantarAlPropio`), así que su modelo y la cámara van con la entrada de ahora mismo.

**Medido** (cliente sin pantalla, bot andando a 3 m/s, en local): el mando predicho va 0,3-0,6 m por delante del héroe
que llega del servidor, unos 130 ms que el jugador ya no espera (el retraso de la interpolación más la ida y vuelta). El
servidor sigue a 5,4 ms por tick con dos jugadores.

Lo que falta, anotado:

- Sin probar con latencia y pérdida simuladas, ni cuánto se nota una corrección (hoy salta, sin suavizar).
- El cliente no predice chocar con soldados ni con otros héroes: lo decide el servidor y llega como corrección.
- Sin aceleración ni resistencia: correr no gasta. La animación del héroe propio arranca con el retraso de la red (la
  marcha llega del servidor), aunque el cuerpo ya se mueve.
- Los golpes no se predicen: el ataque sale cuando el servidor lo confirma.
- Un cliente sin el mapa cargado (los bots sin pantalla) predice sin malla y el servidor lo corrige en las murallas.

### 8.6 Medida con 30 héroes y cómo baja el ancho de banda (paso 4)

```
VENTANA=no SERVIDOR="-alcance 80" TODOS="-tasaSoldados 20" Tools/ver_batalla_real.sh <nombre> 15 130 60
```

Batalla de 15 contra 15 (360 soldados y 30 héroes; cada héroe con una escuadra de 12, que es lo que da hoy Conquest),
servidor sin pantalla y dos clientes sin pantalla que llevan un héroe cada uno, en este PC.

**Servidor:** tick de 6,3-7,6 ms de media (peor 14-20), 60 ticks por segundo, 3,5 núcleos. Cabe a 60 Hz con más de la
mitad del tick libre. De 240 a 360 soldados el tick crece casi en proporción (unos 20 µs por soldado): si siguiera así,
unos 750 soldados llenarían el tick a 60 Hz y unos 1.500 a 30 Hz. No está medido con más de 360.

**Bajada por cliente** (una prueba de 130 s por fila; cada batalla sale distinta, así que son cifras orientativas):

| Opciones | kbit/s por cliente | Veces por segundo que llega cada soldado |
|---|---:|---:|
| Ninguna (60 envíos por segundo) | 440-455 | 53-56 |
| `-tasaRed 30` | 254 | 29 |
| `-tasaRed 20` | 220 | 17 (ya toca el tope de un paquete por envío) |
| `-tasaSoldados 20` (los héroes siguen a 60) | 243 | 23 |
| `-alcance 80` | 85 con 41 fantasmas cerca; 367 con 239 | 60 |
| `-tasaSoldados 20 -alcance 80` | 212-217 (con 340 fantasmas cerca) | 23 |

La subida de cada cliente (su entrada, 60 veces por segundo) son 63 kbit/s.

- **`-tasaSoldados N`** (servidor y clientes: va en el prefab fantasma, `MaxSendRate`): cada soldado se manda como mucho
  N veces por segundo y los héroes siguen a la tasa de red. Es la que más baja sin tocar lo que el jugador nota (su
  héroe y los héroes enemigos): la mitad con 20. La interpolación del cliente rellena entre envíos.
- **`-alcance M`** (servidor, `AlcanceSystem`): a cada jugador solo le llega lo que está a menos de M metros de su
  héroe (el juego deja de pintar las unidades a 120 m). Usa la relevancia de Netcode; sale a M + 15 y entra a M para que
  no parpadee en el borde. Baja mucho cuando la batalla está repartida por el mapa y nada cuando todos están juntos.
- **`-tasaRed N`** baja todo por igual, también los héroes.
- **No combinar `-tasaRed 30` con `-tasaSoldados`**: medido, con `-tasaRed 30 -tasaSoldados 15` los soldados siguen
  llegando 29,6 veces por segundo (281 kbit/s). Netcode calcula el intervalo del tope contra la tasa de red y con 30 le
  sale 2 ticks de simulación, que es lo mismo que no tener tope. Con la tasa de red a 60 el tope sí se cumple.

Lo que falta, anotado:

- Con `-alcance`, lo que vuelve a entrar en el radio se crea otra vez en el cliente, modelo incluido: no se ha medido el
  tirón al cruzar el borde con muchas unidades a la vez, ni se ha visto en pantalla.
- `AlcanceSystem` repasa todos los pares jugador-fantasma 4 veces por segundo; con 30 jugadores habrá que repartirlo por
  celdas (`GhostDistancePartitioning`). Las claves de fantasmas ya destruidos se quedan en el conjunto.
- Posición y giro viajan con la precisión por defecto (milímetros, cuaternión entero); un soldado en pie solo necesita
  centímetros y el giro sobre el eje vertical. Es el siguiente recorte si hace falta.
- El cliente de ventana gasta 3,5-4 núcleos con 260 modelos; sigue sin mirarse.
- Sin medir con latencia y pérdida simuladas.

### 8.7 Primera prueba con teclado y ratón (2026-11-12)

El autor jugó dos partidas contra el servidor local. Los controles funcionan (mover, correr, atacar, habilidades y las
órdenes C/X/V, que la escuadra obedece). Lo que salió y cómo quedó:

- **El cliente se quedaba «pegado».** La ventana se paraba al perder el foco (al cambiar a otra ventana), dejaba de
  contestar y el servidor cortaba la conexión a los 30 s; al volver, nada se movía. Ahora el arranque de red pone
  `Application.runInBackground`. Sigue faltando avisar en pantalla cuando se pierde la conexión y reconectar.
- **X mandaba mantener donde estaba el héroe**, no donde apunta el ratón como en el juego. `EntradaJugador.PuntoDeOrden`
  lleva ahora el punto del terreno bajo el ratón.
- **No salían los marcadores de destino** al pulsar X. El servidor calcula, para la escuadra de cada jugador, el punto
  donde va a formar cada soldado mientras cumple «mantener» y aún no ha llegado (lo mismo que `DestinationMarkerSystem`
  hace para la escuadra local del juego) y lo manda en `DestinoRed`; el cliente pinta ahí el prefab de marcador del
  juego. Los soldados de la escuadra llevan también `SoldadoRed.Dueno`. Confirmado en pantalla por el autor.
- **La pantalla de carga tapaba los primeros 30 s** (esperaba a un héroe local que en red no existe). Con
  `NoLocalHero` se quita a los 3 s.
- **El héroe salía pegado a la muralla** y la cámara fija quedaba dentro de ella. `MandoSystem` da ahora, de su bando, el
  primer héroe al que la cámara del cliente ve sin nada por medio. Es un apaño mientras la cámara sea fija: la del juego
  es lo que lo arregla de verdad.
- El registro del servidor dice cada 10 s, por jugador, qué orden pidió, cuál ganó (y si fue la reacción al combate) y
  a qué distancia está su escuadra.

## 9. Meter el resto del juego en la batalla en red

Orden acordado con el autor el 2026-11-12, tras la primera prueba con teclado: (1) cámara e interfaz del juego atadas al
héroe del jugador; (2) la partida entera: reloj, zonas de captura y suministro, final; (3) los controles que faltan:
formaciones, montar, interactuar y cambiar de escuadra, habilidades de escuadra y perks; (4) lo que aún no se ve:
proyectiles, caballos, efectos, números de daño, caída de los soldados; (5) unirlo al motor: héroes y escuadras del
ticket y el resultado de vuelta; (6) robustez: aviso y reconexión, latencia simulada, CPU del cliente.

### 9.1 Cámara e interfaz del juego atadas al héroe del jugador

- **Cámara.** El cliente crea la entidad de cámara que `CameraBootstrapSystem` crea para el héroe local
  (`CameraTargetComponent` y compañía, con los mismos valores) apuntando al fantasma del héroe propio, y le pone a ese
  fantasma lo poco que `HeroCameraController` lee de él (`HeroLifeComponent`, `HeroVisualInstance`). La cámara del juego
  hace el resto: órbita con el ratón, zoom y no atravesar paredes. Como la entrada del cliente ya era relativa a la
  cámara, WASD sigue a la vista. Quien solo mira conserva la cámara fija de `DibujoRedSystem`.
- **Interfaz.** El servidor calcula para el héroe de cada jugador lo que `UIBattleDataSystem` calcula para el héroe
  local (vida, resistencia, si vive, las casillas de habilidades con su recarga, el combo y lo que no puede pagar, y los
  soldados y la formación de su escuadra) y lo manda en su mando (`InterfazRed` y el búfer `HabilidadRed`), que solo le
  llega a él (`SendToOwner`). El cliente lo vuelca en `UIHeroBattleDataComponent`, que es lo que leen `HUDController`,
  `SkillBarController` y el menú radial. Único cambio en el juego: `UIBattleDataSystem.WriteSkills` y `WriteCosts` son
  públicos.

Comprobado con una captura de un cliente que juega solo: la cámara del juego detrás del héroe y la interfaz con sus
cifras de verdad (vida 1540/1540, resistencia gastándose, barra de habilidades).

Lo que falta, anotado: nadie lo ha probado aún con el ratón.

### 9.2 La partida entera: reloj, zonas y final

```
SERVIDOR="-duracion 80" Tools/ver_batalla_real.sh <nombre> 10 110 40 -bot      (una batalla de 80 s, para ver el final)
```

- **Un fantasma para la partida** (`PrefabsRed.Partida`, lo crea `PartidaSystem` en el servidor): `PartidaRed` lleva el
  reloj, el estado (`MatchState`) y el ganador, y su búfer `ZonaRed` lleva cada zona de captura o de suministro con su
  posición, su dueño, quién la captura, el progreso y si está activa, bloqueada, disputada o capturándose. Son los datos
  que el juego guarda en `BattleClockComponent`, `MatchStateComponent` y las entidades de zona.
- **El cliente los vuelca donde su interfaz los busca** (`EspejoClienteSystem.Partida`): el reloj y el estado en sus
  propias entidades sueltas, y cada zona en la entidad de zona que la escena creó en el cliente (se casan por la
  posición, no por el id). El reloj de pantalla, los indicadores de zona y el panel de victoria o derrota son los del
  juego, sin tocar.
- **El bando y la tropa del jugador** viajan en su `InterfazRed` y el cliente crea con ellos el `DataContainerComponent`
  que la interfaz consulta: los colores de aliado y enemigo de las zonas y el panel de la escuadra salen de ahí.
  También viaja si su héroe está dentro de una zona que se captura, y cuánto lleva (la barra «Capturando»).
- **El final.** Cambio en el juego: `BattleSceneController` enseña el panel también a un cliente de red, que sabe su
  bando por `BattleBootstrapRequests.NetworkTeam`, y ese cliente se queda en la escena de batalla (la de después de la
  batalla necesita la sesión de un jugador). El servidor apunta el final en el registro y cierra el proceso a los 30 s.
- `-duracion N` hace que la batalla de verdad dure N segundos en vez de lo que diga el mapa.

**Comprobado** con una batalla de 80 s y dos clientes que juegan solos: el reloj llega a cero, el servidor da por ganador
al defensor (tiempo agotado) y el cliente del bando defensor enseña «Victory» con el reloj en 00:00; el panel de la
escuadra enseña ya la tropa del jugador. Llegan las 7 zonas del mapa.

El registro del servidor y el del cliente dicen cada 10 s cómo está cada zona (dueño, quién la captura, progreso y
banderas): en una prueba de 130 s las dos líneas coinciden zona por zona, incluida una ya capturada al 100 % y cerrada.

Lo que falta, anotado: en las pruebas la IA no llegó a capturar ninguna zona con los clientes ya dentro, así que **el
momento en que una zona cambia de dueño no se ha visto llegar al cliente** (sí su estado, que coincide); el resultado no
se manda a nadie (punto 5); el cliente no tiene pantalla de después de la batalla.

### 9.3 Los controles que faltaban: formaciones, prisa, habilidades de escuadra, perks y montar

- **La escuadra del jugador en red se manda como la del jugador local.** Antes sus órdenes iban por el hueco de la IA
  (`SquadAIOrderIntentComponent`), que no sabe de formaciones. Ahora `MandoSystem` las aplica con
  `SquadPlayerCommandResolver.TryApply`, lo mismo que hace `SquadControlSystem` con el teclado del jugador local: seguir,
  mantener en un punto, atacar, la prisa, las formaciones y la insistencia (tres veces la misma orden pasa por encima de
  la reacción al combate). Cambio en el juego: `OrderResolutionSystem` trata como escuadra del jugador también la de un
  héroe con `HeroNetworkMotion` (el que lleva alguien por la red). Al tomar un héroe de la IA, su escuadra sigue con la
  orden y la formación que tenía.
- **Teclas del cliente**, las de `SquadControlSystem`: C seguir (doble C cambia la prisa), X mantener en el punto del
  ratón (doble X pasa a la formación siguiente), V atacar, F1-F4 una formación, y el menú radial del juego.
- **Habilidades**: además de Q/E/R/T, las de la escuadra en 1-3 (o pulsando en la barra) y los perks activos en 4-5. Las
  de escuadra las lanza la escuadra (`useSquadSkill`), como con la IA.
- **Montar** (G): el jugador dice si quiere ir a caballo (`HeroMountRequestComponent`) y `HeroMountSystem` hace el resto.
  Cambio en el juego: `HeroAIMountSystem` no decide por un héroe llevado en red. A caballo, el mando predicho usa los
  aires del caballo (trote y galope); subiendo o bajando no se mueve. Si lo derriban, no vuelve a montar solo.

**Comprobado** con dos clientes que juegan solos (dan una orden cada 8 s, piden una formación cada 11 s y montan o
desmontan cada 25 s): en el registro del servidor la orden de la escuadra «gana el jugador» y cambia como se pide
(seguir, mantener, atacar), la formación pasa por muro de escudos, testudo y reposo, y el héroe pasa de a pie a montado
(velocidad de 3 a 6 m/s) y vuelta.

Lo que falta, anotado: **cambiar de escuadra en un punto de suministro** (y la tecla de interactuar) no está; hoy cada
héroe trae una sola escuadra, llegará cuando el ticket del motor traiga varias. El caballo no se ve en el cliente
(punto 4). Bloquear, el modo táctico y andar despacio no viajan. Nadie lo ha probado con el teclado.

### 9.4 Lo que aún no se veía: proyectiles, golpes y caballos

```
SERVIDOR="-tropas arc01,lcv01,sqd01" Tools/ver_batalla_real.sh <nombre> 10 150 40 -bot     (arqueros, caballería y escuderos)
```

- **Proyectiles.** `ProyectilesSystem` (servidor) avisa a todos de cada disparo con un RPC (`ProyectilRed`: de qué
  reserva sale, origen, destino, trayectoria y bando), justo antes de que `ProjectileSpawnSystem` se lleve la petición.
  El cliente saca el proyectil de la reserva del juego y lo lanza con `ProjectileController`, sin perfil de daño: vuela
  y se clava igual, pero no hiere a nadie (el daño lo decide el del servidor).
- **Golpes.** `GolpesSystem` (servidor) recoge cada `DamageFeedbackEvent` y avisa con un RPC (`GolpeRed`) a los jugadores a
  los que les toca: los golpes de su héroe o de sus soldados, y los que pasan a menos de 40 m. El cliente pinta el número
  con `FloatingCombatTextManager` y la chispa con `HitImpactEffectManager`, los del juego.
- **Caballos.** El espejo dice si el soldado va montado (el cliente le pone `MountComponent` y su modelo de caballería
  hace el resto) y, del héroe, el estado de su montura con su revisión; el cliente le pone los componentes que lee
  `HeroMountPresenter` (`HeroMountComponent`, la clase del héroe, la velocidad) y el presentador del juego saca y quita
  el caballo. Los fantasmas llevan también su `TeamComponent`, para que un proyectil no se pare en los suyos.
- `-tropas a,b,c` reparte esas tropas por turno entre los héroes de la batalla de verdad.
- El juego no tiene sonido ni más efectos que la chispa del golpe: no hay nada más que llevar.

**Dos fallos del juego que salieron aquí** (no son de la red, pero sin arreglarlos el servidor no aguanta):

- **En `BattleScene` los arqueros no disparan.** La reserva de proyectiles (`ObjectPoolSystem`) y quien la llena
  (`RangedCombatBootstrap`) solo están en `BattleTestScene`. Sin reserva, `ProjectileSpawnSystem` vuelve sin hacer nada y
  las peticiones de disparo se acumulan para siempre. Para la batalla en red, `ReservaDeProyectiles.Asegurar` la crea si
  falta, en el servidor y en el cliente. **La escena `BattleScene` sigue sin tenerla: en el juego de un jugador hay que
  añadirle esos dos objetos.**
- **Los avisos de golpe se quedaban para siempre en el servidor**: los consume `DamageFeedbackPresentationSystem`, que es
  de presentación y en el servidor no corre. Ahora los consume `GolpesSystem`.

**Comprobado** con arqueros, caballería de lanza y escuderos mezclados y dos clientes que juegan solos, 150 s: el
servidor avisó de 401 proyectiles y al cliente sin pantalla le llegaron los 401 (al de ventana, que entró después de
cargar el mapa, 368); de 233 avisos de golpe le llegaron 218 a uno y 16 al otro (cada uno recibe solo los suyos y los
cercanos). En la captura del cliente se ve la caballería con sus caballos, el reloj corriendo y la interfaz con la tropa
del jugador («Lance Cavalry»). El tick del servidor sigue en 4,3-4,8 ms.

Lo que falta, anotado: las flechas y los números no se han visto en una captura (se sabe que llegan y que se piden a la
reserva, no cómo quedan en pantalla); el caballo del héroe tampoco se ha visto montado en una captura; las tropas del
motor (`arqueros`, etc.) no están en la base de escuadras y su proyectil no entra en la reserva.

### 9.5 La batalla de verdad desde un ticket del motor, y su resultado

```
python Tools/ticket_de_prueba.py Logs/red/real/ticket.json
PruebaRed.exe -batchmode -nographics -red servidor -batallaReal 1 -ticket Logs/red/real/ticket.json -resultado Logs/red/real/resultado.bin -duracion 90 -espera 25
CONQUEST_BATALLA_REAL=1 dotnet run --project Tools/LanzadorDePrueba -- Builds/PruebaRed/PruebaRed.exe Logs/red/real/ticket.json -cliente
```

Con `-batallaReal` (o la variable `CONQUEST_BATALLA_REAL=1`, porque el lanzador del motor arranca el proceso con sus tres
opciones y nada más), un ticket ya no se juega con las cápsulas de prueba sino con la batalla de verdad. Sin ella sigue
como estaba: las pruebas del motor contra Conquest (`con_conquest_*`) usan las cápsulas, que acaban en segundos.

- **Montar la batalla** (`BatallaRealDeTicket.Montar`): cada participante del ticket es un héroe con su id, su nombre, su
  nivel y sus escuadras; cada escuadra lleva el id del ticket, su tropa, su nivel y sus efectivos autorizados (sin pasar
  de los que caben en la tropa de Conquest: los que no salen no pueden morir). Atacantes en los puntos de salida 1-3 y
  defensores en los 4-6.
- **A cada jugador, el héroe de su token.** `BatallaDeTicketSystem` sigue llevando el ticket, la asignación del lanzador
  y los tokens; `MandoSystem` da al jugador la entidad de ese héroe. Cambio en el juego: el héroe de la batalla lleva su
  id (`BattleParticipantComponent`, que pone `BattleSceneController` al crearlo). Con un fichero (sin tokens), por orden
  de llegada. Quien no tiene héroe en el ticket solo mira.
- **El plazo del ticket manda sobre el reloj del juego.** En Conquest cada captura alarga el reloj; el motor rechaza un
  resultado que pase de `duracion_maxima_segundos`. El servidor no deja que al reloj le quede más de lo que falta para
  el plazo (menos 5 s de margen), así que al agotarse gana el defensor, como pide el motor. El plazo cuenta desde que
  arranca el reloj, no desde que se empieza a cargar el mapa.
- **El resultado** (`BatallaRealDeTicket.ContarBajas`): al cerrar el juego la partida, los muertos de cada escuadra son
  los soldados que le faltan a su entidad (los de una escuadra en reserva, los que tenía al guardarla) y un héroe está
  derrotado si está caído en ese momento. El ganador es el del juego; la razón, tiempo agotado u objetivos capturados.
  Se comprueba con las reglas del motor, se escribe o se manda al lanzador, y el proceso sale con su acuse.

**Lo que es de paso** hasta que estén en Conquest los catálogos del motor (lo dice también el código):

- La clase del héroe se traduce con una tabla de cuatro (`lanza_escudo_redondo`, `arco_lanza`, `hacha_dos_manos`; el
  resto, espada y escudo); su aspecto sale de la reserva de héroes de prueba; el mapa es siempre el mismo.
- Una **escuadra sin héroe** recibe un capitán de la IA (`capitan:<id de la escuadra>`) que no figura en el resultado.
  Propuesta pendiente del autor.
- Solo salen las tropas que están en la base de escuadras del juego. Las cuatro del motor que hay en Conquest están
  fuera de ella: un ticket con ellas se rechaza. Propuesta pendiente del autor: meterlas en la base.
- El héroe caído reaparece, como en el juego. Propuesta pendiente del autor: dejarlo así.

**Comprobado** con un ticket de prueba (un héroe de la IA con escuderos y una escuadra sin héroe de lanceros contra un
héroe humano con arqueros y otro de la IA con escuderos; 42 soldados):

- Desde un fichero, 90 s: el cliente que entra lleva al héroe humano, la batalla acaba por tiempo a los 84,8 s, gana el
  defensor y el resultado trae las bajas reales (2 muertos de 10 en una escuadra y 1 de 8 en otra). «El motor lo
  aceptaría», según `BatallaDeTicket.Comprobar`.
- Con el lanzador de prueba y un cliente con token, 120 s: llegan el ticket y la asignación, el cliente lleva «el héroe
  de su token», salen el inicio y el resultado, el lanzador los acusa y el proceso sale con 0.
- En la primera prueba una captura alargó el reloj de 62 a 352 s y la batalla no acabó en su plazo: de ahí el tope.

Lo que falta: probarlo contra el motor de verdad (hoy sus pruebas usan las cápsulas); las incorporaciones a media
batalla y las salidas; las formaciones permitidas del ticket; y todo lo de los catálogos (tropas, clases, objetos y
sus cifras), que es el trabajo siguiente a la red.
