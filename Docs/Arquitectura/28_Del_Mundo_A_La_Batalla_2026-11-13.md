# Del mundo a la batalla (bloque 4, paso 5A)

**Qué es.** Desde el mapa, «⚔ Atacar» un campamento de bandidos abre una batalla de Unity en el motor. Mientras espera servidor, el jugador
ve su ficha y puede cancelarla. Cuando el motor le da su asignación (servidor y token), entra en el servidor de batalla de Conquest con su
héroe y su tropa. La vuelta y el informe de combate son la tanda 5B.

**Fuentes:**
- Motor 358af5ff: `Docs/Mechanics/Batalla_Unity_Ciclo.md`, `Docs/Coordinacion/Trabajo_En_Conquest.md`, `crates/lanzador`,
  `proto/bronzeage/v1/battle.proto`.
- Hilo de tropas: `Assets/Scripts/Net` y el doc 22.
- BronzeAgeClient: `ui/panelBatalla.ts`, `ui/eleccionClic.ts`, `main.ts` (`alcanceDeAtaque`, `AccionPuerta`).

## El ciclo, del lado del jugador

1. **La ficha de unos bandidos** ofrece «⚔ Atacar» solo si se puede. Se cumple todo esto:
   - sano;
   - con columna, que no esté en batalla ni formándose;
   - a ≤ `radioEncuentro`;
   - lejos de un campamento de mercenarios.

   Con la columna en una batalla no sale ninguna acción, porque el motor rechaza todo lo que toque la columna (`batalla.bloqueado`).
2. **`atacar`** (`OrdenAtacar{campamento_id}`) se acepta y llega la `Batalla` en el ámbito `BATALLA:<uuid>`. La columna queda con
   `en_batalla`.
3. **La ficha de la batalla** (`PantallasDeMundo.Batalla.cs`, como `panelBatalla.ts`) lleva:
   - «Evento: campamento de bandidos · estado»;
   - «⚔ Contra bandidos · nivel N»;
   - «Atacan» y «Defienden», con la tropa del ticket (el dibujo de cada una, su nombre y sus hombres);
   - qué pasa ahora;
   - el plazo, en tiempo de mundo.
4. **«Cancelar la batalla»** sale solo para quien la inició y solo en convocando o asignada (`batalla.no_puede_cancelar`). En cuanto
   cambia el estado desaparece.
   - Cancelada: «Batalla cancelada: tu columna queda libre.»
   - Si vence el plazo (5 min sin servidor, o 5 más sin empezar): «No llegó a empezar a tiempo: la batalla se anula sin castigo y tu
     columna queda libre.» (Ciclo §2.4: `fallida`, sin castigo.)
5. **Con `asignacion_propia`** (host, puerto y token de tu héroe) se entra solo.

## La entrada: la sesión del mundo sigue viva

- **Lo que se mueve.** `PantallasDeMundo` y `ConexionDeMundo` están en el mismo objeto: las pantallas y la sesión, que se bombea en su
  `Update`. Ese objeto pasa a `DontDestroyOnLoad`. Su interfaz se oculta y el mapa se quita.
- **Cómo se arranca la red.** `EntradaEnBatalla.Entrar` quita y libera los mundos ECS que haya, pone `OpcionesRed` (Cliente, Ip, Puerto,
  Token, `BatallaReal = 1`) y llama a `DefaultWorldInitialization.Initialize`.
  - Es lo mismo que `-red cliente -ip -puerto -token -batallaReal 1`, sin salir del proceso.
  - El patrón es el de `BatallaRealServidorTests`.
  - `ArranqueRed` crea el mundo cliente, `EntrarEnBatallaClienteSystem` presenta el token y `EspejoClienteSystem` carga BattleScene
    (Single).
- **Por qué se puede:**
  - `Conquest.Net` no destruye objetos ajenos (solo usa `DontDestroyOnLoad` para su propio aviso).
  - Reiniciar los mundos ECS no toca GameObjects.
  - No hay choque de hilos: `Sesion.Bombear` va en el hilo principal.
  - No hay choque de puertos: el WebSocket es TCP al motor, y Netcode es UDP al puerto del lanzador.
- **Qué da:**
  - el estado de la batalla sigue llegando (asignada → en curso → aplicada, fallida o cancelada), y con él la señal de vuelta de la 5B;
  - el resultado y los hechos (`evento_pve.*`, `heroe.herido`, `heroe.botin`) están en el almacén al volver;
  - se ve una sesión reemplazada;
  - no hay reconexión que pueda fallar al volver.
- **Si aun así se cae la conexión,** el motor lo tiene previsto:
  - conectarse, desconectarse y cerrar sesión no se bloquean en batalla (Ciclo §4.1; `comando.rs`);
  - la salida del mundo de quien se desconecta espera a que acabe la batalla, y reconectar la anula (Ciclo §5.2, `heroes.rs`,
    `batallas.rs`);
  - el héroe sigue en su columna, apartada (`en_batalla`);
  - en el servidor de batalla, 60 s de gracia con la IA y después `desconexion` (Trabajo_En_Conquest).
- **Un detalle de la dirección.** Se exige una IP, porque `NetworkEndpoint.Parse` no resuelve nombres. El lanzador da 127.0.0.1
  (`LANZADOR_HOST`). Si diera un nombre, la ficha lo dice y no entra.

## Símbolos

Los símbolos del prototipo que no son emoji (➜ ⧗ ⚑ ⛟ ⌂ ♜ ✦ ♛) no están en Inter, Cinzel ni Twemoji. Los dibuja el script de iconos
(familia `simbolo`; ver doc 27). Van donde los pone el prototipo:
- ➜ en «Marchar aquí»;
- en la lista bajo el clic, el de cada cosa (♛ las plazas).

## Pruebas

**`DelMapaALaBatalla`** es `[Explicit]` (`Assets/Tests/PlayMode/Mundo/PantallasDeMundoTests.Batalla.cs`). Se corre con
`Tools/BatallaDesdeElMundo/correr.sh` (con `MOTOR` y `MUNDO`), con la batalla de verdad:
- **Motor:** el servidor 358af5ff con `BRONZEAGE_BATALLAS_EN_UNITY=1`, en el puerto de pruebas 7891 y con una partida nueva.
- **Lanzador:** `bronzeage-lanzador` compilado de 358af5ff (git archive al scratchpad). Lo arranca la prueba después del primer ataque.
- **Servidor de batalla:** `Builds/PruebaRed/PruebaRed.exe`, recompilado de main porque el que había era anterior a d0995327. Corre con
  `-batchmode -nographics` y `CONQUEST_BATALLA_REAL=1`.
- **Cliente:** el editor sin `-batchmode`, porque `EspejoClienteSystem` no carga BattleScene en batchmode.

El recorrido:
1. Un héroe nuevo pide la milicia prestada y sale del campamento (por órdenes, como en la 4B).
2. Entra por la interfaz hasta el mapa.
3. Rechazos con su frase: `combate.lejos` y `batalla.no_existe`.
4. «Marchar aquí» a los bandidos más cercanos, con el reloj del mundo a ×30 mientras anda.
5. «⚔ Atacar» sin lanzador: la ficha con «convocándose», a los cuatro tamaños. Luego «Cancelar la batalla», cancelada y «Atacar» otra vez.
6. Con el lanzador: «Atacar», asignación con token, entrada, BattleScene y mi héroe con la cámara del juego. Hay más de 26 figuras (mi
   héroe, la milicia y los bandidos).
7. La sesión del mundo sigue abierta y ve la batalla en curso.
8. `cancelarBatalla`, ya en curso, se rechaza con `batalla.no_puede_cancelar` y su frase.

## Lo que es del hilo de tropas (no se ha tocado)

- **Error al cargar.** `BattleSceneController.Awake` registra un error al cargar BattleScene en red: «No hay BattleData disponible y no
  hay TestEnvironmentInitializer». Pasa también con `-red cliente` desde la línea de órdenes. La prueba lo admite a él solo.
- **Batchmode.** `EspejoClienteSystem` no carga BattleScene en batchmode (`!Application.isBatchMode`), así que la prueba no corre sin
  pantalla.
- **`IsLocalHero`.** El espejo de red no marca `IsLocalHero` en las figuras. El héroe propio se reconoce porque la cámara del juego lo
  sigue (`EspejoClienteSystem.CamaraDelJuego`).
- **El aspecto** del héroe en batalla sale de `TestHeroPool` (bloque 3, pendiente).
- **Para la 5B**, pendientes del visto bueno de Víctor:
  - reiniciar los estáticos al empezar una batalla de cliente;
  - la señal de vuelta. Con la sesión viva, el fin de la batalla llega del motor. Lo que queda en el cliente es «salgo por mi cuenta»
    y «me han eliminado en campo abierto».
