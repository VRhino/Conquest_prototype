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
