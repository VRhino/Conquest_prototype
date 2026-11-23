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

## Tanda 1 del paso 5: la preparación de la batalla (2026-11-14)

Las decisiones son las respuestas de Víctor a las dudas del paso 5, que están en el plan del paso 5 (retirado de `tasks/` el 2026-11-23; sigue en git).

### Qué hace

- **Atacar lleva a la preparación.** «Atacar» a unos bandidos abre la escena `PreparacionScene`, que solo tiene una cámara. Las pantallas
  del mundo pasan a DontDestroyOnLoad con la sesión viva, que se sigue bombeando, y el mapa se quita. Sustituye a la ficha de espera de la
  5A: arriba van el estado y el plazo del motor, y lo que le queda a la preparación.
- **Solo se cambia el orden.** Contra bandidos se combate con lo que lleva la columna. La pantalla enseña sus escuadras con las piezas de
  tropa de Conquest y el Liderazgo del motor.
  - Solo deja **cambiar el orden** (◀ ▶): con la primera entra el héroe y las demás esperan para cambiarlas en los puntos de suministro.
  - También deja cambiar la **formación** de cada una: las que permite el ticket y tiene su tropa en Conquest.
- **No se guarda nada.** El orden y la formación los aplica **solo el servidor de batalla** al montar esa batalla. El motor no guarda
  nada (Víctor, 2026-11-14). Cada preparación empieza con el orden y la formación que el héroe tiene en el motor, es decir, los del
  ticket. Esto sustituye a la respuesta 54: además, `ordenarEscuadras` y `elegirFormacion` tienen candado de batalla y el motor los
  rechaza desde el ataque con `batalla.bloqueado`.
- **La interfaz es la BattlePrepScene de Conquest portada a UI Toolkit** (Víctor, 2026-11-14: portar, no rediseñar).
  - Calca la disposición de la original a 1950x1130: cabecera con el nombre del mapa, el temporizador rojo y la raya; el minimapa; la
    tira de héroes; el pie con el Liderazgo y las tarjetas; el equipo y el botón amarillo.
  - Las medidas, colores, letra (LiberationSans) y sprites (`Assets/Art/UI`) son los de su inventario (`PreparacionDeConquestTests`,
    que también saca las capturas de referencia).
  - El lienzo se escala entero para caber en la ventana. La original no escala: en las ventanas pequeñas se sale.
  - El minimapa es el mismo prefab UGUI de la original (`MapDataSO.preparationMap`), pintado en una textura con su propia cámara y sin
    EventSystem, con tu punto de aparición marcado con su selección.
  - Lo nuestro va en el sitio de lo más parecido: el orden y la formación, sobre cada tarjeta; «Listo» o «Volver al mapa», en «Start»;
    «Cancelar la batalla», en «Warband Builds»; el estado y el plazo del motor, a la derecha de la cabecera; quién está listo, al final de
    su fila.
  - Se ocultan sin mover nada «Save Build», elegir el punto y el inventario. Los huecos «+» del riel se ven, apagados (Víctor: que el riel no se vea vacío).
- **Lo que se enseña sin cambiarlo:**
  - el equipo, con «se cambia dentro de una plaza, un pueblo o un campamento»;
  - el campo de batalla, que es el minimapa del mapa `default`, con los puntos de aparición, suministro y captura, y el tuyo marcado: el
    que reparte el servidor;
  - quién combate, y si cada uno está listo.
  - Los perks no salen, porque no hay nada que elegir.
- **El servidor de batalla.** Con `asignacion_propia`, la preparación se conecta con el servidor de batalla. «Listo» se puede pulsar en
  cuanto hay conexión.
  - Empieza cuando todos los humanos están dentro y listos, o cuando pasan los 60 s de espera; quien no ha dicho «Listo» entra con lo que
    eligió.
  - Al dar la salida, la red carga BattleScene.
- **Cancelada o anulada.** Si la batalla se cancela o se anula antes de empezar, la preparación lo dice. «Volver al mapa» deja el
  servidor de batalla (el mundo de cliente se quita y vuelve el mundo ECS de siempre) y carga MundoScene.
  - El objeto «Mundo» que trae la escena se aparta (`PantallasDeMundo.Viva`), y las pantallas vivas montan el mapa con la sesión de
    siempre.
  - `VolverAlMundo()` es la misma vuelta que usará la tanda 2 al acabar la batalla.
- **Las escenas, en el build.** MundoScene y PreparacionScene están ahora en la lista de escenas del build (`EditorBuildSettings`); para
  cargarlas por nombre hace falta.

### El mensaje entre el cliente y el servidor de batalla

- `PreparacionDelJugador` (cliente → servidor, cada segundo hasta empezar): el orden, las formaciones y «Listo».
- `EstadoDePreparacion` (servidor → todos, cada medio segundo): lo que queda, si ya empezó y cada héroe humano con su estado
  (sin conectar, preparándose, listo) y su punto de aparición.

Lo lleva `PreparacionRed`, en `Conquest.Net`, entre la pantalla y la red. Un cliente sin pantalla de preparación (`-red cliente`, como
PruebaRed) se da por listo al entrar y carga BattleScene enseguida, como antes.

### Lo estático entre batallas

`EntradaEnBatalla.Entrar` y `EntradaEnBatalla.Salir` ponen como al arrancar:
- `PreparacionRed`;
- `ReconexionClienteSystem` (aviso, reconexiones, «abandonada»);
- `BattleBootstrapRequests.NoLocalHero` y `NetworkTeam`;
- `BattleTransitionData`.

`EspejoClienteSystem.CamaraDelJuego` se pone a falso al crear el mundo de cliente. Así se entra en otra batalla sin salir del proceso.

### Lo que esta tanda toca del código de batalla (para el hilo de tropas)

- **`BatallaDeTicketSystem`:**
  - recibe `PreparacionDelJugador` y manda `EstadoDePreparacion`;
  - empieza con todos dentro **y listos**, o al acabarse la espera;
  - un ticket con la revisión subida conserva lo elegido.
- **`BatallaRealDeTicket`:**
  - `Preparado()` pone primera la escuadra elegida y la formación de cada una, si su tropa la tiene;
  - `Heroe()` viste al héroe de un jugador con su aspecto del motor (`HeroSnapshot`: cabeza, pelo, barba, cejas y género; un hueco vacío
    se queda vacío) y con la armadura de inicio de Conquest del tipo de la suya.
  - `TipoDeArmadura` saca el tipo del id de la pieza (`torso_ligera_…`), por convención, hasta que el catálogo de objetos llegue a Conquest
    (paso 6).
  - El arma y el capitán de una escuadra sin héroe siguen saliendo de `TestHeroPool`.
- **`HeroeEnJuego`:** `Orden`, `Formaciones` y `Listo`.
- **`EspejoClienteSystem`:** desde el mundo, no carga BattleScene hasta la salida (`PreparacionRed.ConPantalla`).
- **`ReconexionClienteSystem`:** `Reiniciar()`.
- **`BattleSceneController.Awake`:** ya no registra «No hay BattleData…» en el cliente en red (`NoLocalHero`), porque allí no hace falta.
- **No se marca `IsLocalHero` en el espejo.** `EntityVisualSync` lo usa para darle al modelo el motor físico local, que escribe la posición;
  en red la posición es del servidor y se pelearían. El héroe propio se sigue reconociendo con `EspejoClienteSystem.CamaraDelJuego`.

### Pendiente: monturas que se deslizan sin animar

Víctor vio, mirando las pruebas en su pantalla, «las monturas de los héroes se están moviendo sin la animación, tiesas». No está
reproducido: en la batalla contra bandidos de la prueba de punta a punta no monta nadie. Falta saber si eran héroes o caballería (las tropas
a caballo).

La prueba ya lo vigila: `CaballosAlPaso` falla si un héroe montado se desplaza sin el paso (`MountGaitBlend`) en el Animator de su
caballo. Lo dejamos para después (Víctor, 2026-11-14).

### Pedidos al motor (de las dudas del paso 5)

- **Elegir las escuadras** al entrar en batallas que no son de columna. La defensa por loadout ya vale para el paso de los asedios
  (respuesta 57).
- **Bajarse de una batalla sin cancelarla**, y que se cancele si se baja el último (respuesta 17). Quien se baja vuelve a su columna tal
  cual (respuesta 56).
- **Un hecho de cierre** con el botín, lo perdido del carro, la herida y la experiencia (duda 25).

## Tanda 2 del paso 5: después de la batalla y la vuelta (2026-11-14)

### Qué hace

- **El final de la partida.** Al acabar, el cartel de victoria o derrota de Conquest se queda 5 s y luego se pasa al informe de combate,
  en su escena (`InformeScene`), con la sesión viva.
  - Primero se carga la escena, que quita BattleScene y sus figuras, y después se deja el servidor de batalla
    (`EntradaEnBatalla.Salir`). Las figuras leen el EntityManager del mundo de cliente: en el otro orden se rompe.
  - No hace falta tocar `HandleMatchEnd`: lo vigila el mundo (`ReconexionClienteSystem.Acabada`).
- **El informe** calca el de BronzeAgeClient (`informeCombate.ts`, `htmlInforme`, `.briefing`):
  - «Informe de combate» con la fecha del mundo, y «Victoria» o «Derrota» con lo que pasó, y por qué (la razón del resultado);
  - los dos bandos, con sus héroes y sus bajas;
  - «Tus escuadras» y «Escuadras rivales», con la pieza de tropa, el dueño, los que entraron (la escuadra en reserva que no salió, 0), los
    que vuelven y las bajas;
  - los héroes, con si cayeron;
  - lo que ganas o pierdes, comparando el héroe y su columna de antes (al empezar la batalla) y de después: el carro, las monedas, los
    objetos nuevos, el nivel o la experiencia, y la herida (`herido_hasta_ms`).
  - Mientras el motor no aplica el resultado, «Calculando el resultado…».
  - No hay estadísticas de daño ni de bajas causadas: no existen. Tampoco el poder de cada bando, que daba el prototipo y el motor ya no.
- **«Salir»** vuelve a MundoScene con la vuelta de la tanda 1. La columna está donde la deja el motor (`reponer_columna`): en el sitio de
  la batalla.
- **El mismo informe desde el mundo.** El final de una batalla propia deja un aviso con «Ver informe», que abre la misma vista encima del
  mapa mientras el motor guarda la batalla (una hora). No se guarda en ningún otro sitio.
- **«Abandonar la batalla»**, arriba a la derecha de BattleScene, pide confirmación y lleva al informe.
  - Quien abandona deja de combatir (`heroes_que_combaten`), y el motor ya no le enseña la batalla ni su resultado.
  - Por eso el informe del abandono dice solo lo suyo: que abandonó, la herida y lo que cambia en su carro, cuando su columna queda
    libre. Es un pedido al motor.
- **Conexión perdida.** Si la conexión con la batalla se pierde y no vuelve (el servidor la cierra, o 60 s sin ella), se pasa al informe
  con «Perdiste la conexión con la batalla». El héroe sigue en la batalla con la IA y el resultado llega como siempre.
- **La batalla en el mapa** sale en su punto mientras dura (convocándose, a punto de empezar o en curso), para todos los que la ven, con su
  ficha: el nombre, el estado y quién combate contra quién. Las ya cerradas, que el motor guarda una hora, no salen. Unirse desde fuera es
  del paso de unirse a batallas.

- **A pantalla completa** (Víctor, 2026-11-14). El informe del prototipo era una ventana flotante. Aquí es una escena entera y ocupa toda la
  ventana: arriba Victoria o Derrota con el motivo; tu bando a la izquierda y el rival a la derecha, cada uno con su recuadro, sus escuadras
  y sus héroes; debajo, en una fila, lo que ganas o pierdes; «Salir», abajo a la derecha. El contenido, el orden y el aspecto son los del
  prototipo.
- **El ratón.** BattleScene lo deja bloqueado y oculto, y la política de cursor de Conquest (`SceneTransitionService.RequiresFreeCursor`)
  solo lo soltaba en sus escenas de interfaz. Ahora también en las del mundo (MundoScene, PreparacionScene e InformeScene). Con eso se
  arregla que en el informe no se pudiera pulsar «Salir» (Víctor, 2026-11-14). La prueba pulsa «Salir» con un ratón de verdad del
  Input System y comprueba que el cursor está libre y a la vista.
- **Las escenas nuevas** están en `Assets/Scenes/BronzeAge`; `GameScenes` es lo de Conquest de antes.

### Lo que toca del código de batalla

- `ReconexionClienteSystem`: `Acabada` y `Perdida`, estáticos, con su reinicio.
- `SceneTransitionService.RequiresFreeCursor`: las escenas del mundo también son de interfaz.
- No toca `HandleMatchEnd` ni BattleSceneController.

### Pedidos al motor

- **Que quien abandona una batalla vea cómo acabó.** Hoy, al salir, deja de combatir y la proyección le quita la batalla, con el ticket y
  el resultado.

### Pruebas

- **`DelMapaALaBatalla`**, con `CONQUEST_DURACION=60` (el servidor de batalla acorta la batalla). Pasa:
  - la batalla acaba por tiempo y aparece el cartel;
  - el informe, con el resultado del motor, las filas de cada escuadra de los dos bandos, los héroes y la herida, a los cuatro tamaños;
  - «Salir» vuelve al mapa con la columna en el sitio de la batalla, y la batalla ya no sale en el mapa;
  - «Ver informe» desde el aviso abre el informe;
  - el reloj del mundo deprisa hasta que se cura la herida;
  - una segunda batalla en la misma sesión que se abandona, con su informe;
  - la vuelta otra vez, con la misma interfaz y la misma sesión.
- **Sin verificar:** la conexión perdida que no vuelve; una victoria con botín (contra los bandidos de la prueba, por tiempo, siempre se
  pierde); varios jugadores.
