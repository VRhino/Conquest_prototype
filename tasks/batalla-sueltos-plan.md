# Sueltos de la batalla: 12, 13 y 16 (análisis)

2026-11-16. Solo texto, sin código. Del reparto de Víctor:
- el 12, el 13 y el 16, aquí;
- el 14 (el Transport de Unity en Windows), en otra sesión;
- el 15 (probarla con alguien al teclado), él.

## 12. Los arqueros no lanzan proyectiles en la escena de un jugador

- **La causa.** `ProjectileSpawnSystem` (`Assets/Scripts/Combat/Systems/ProjectileSpawn.System.cs:26-27`) no hace nada sin `ObjectPoolSystem`.
  - Las peticiones de disparo (`ProjectileSpawnRequest`) se quedan para siempre.
  - La reserva (`ObjectPoolSystem`) y su precarga (`RangedCombatBootstrap`) solo están en `Assets/Scenes/BattleTestScene.unity`. Lo he mirado
    por el GUID de sus scripts: no están en BattleScene.
  - En red se crean a mano con `ReservaDeProyectiles.Asegurar()` (`Assets/Scripts/Net/EfectosRedSystems.cs:39-60`), que llaman
    `BatallaRealSystem` (77 y 106) y `EspejoClienteSystem` (65).
- **El arreglo, en un solo sitio para todas las escenas.**
  - `ReservaDeProyectiles` pasa de `Conquest.Net` a `Game.ECS`, junto a `RangedCombatBootstrap`.
  - `ProjectileSpawnSystem` la llama cuando falta la reserva y hay peticiones.
  - Las llamadas de red siguen igual.
- **La prueba.** Una PlayMode sin escena:
  - sin `ObjectPoolSystem`, una petición de disparo de una tropa de tiro de verdad;
  - comprueba que se crea la reserva, que sale el proyectil y que la petición se consume.
  - Además, una captura de los arqueros disparando en BattleScene.

## 13. Cambiar de escuadra en red (con interactuar y apuntar con la cámara)

- **La causa común.** El comando de red `EntradaJugador` (`Assets/Scripts/Net/ComponentesRed.cs:18-41`) no lleva ni la escuadra pedida ni la
  dirección de la cámara. Y el cliente de red no tiene héroe con `IsLocalPlayer`: `NoLocalHero`.
- **Cambiar de escuadra:**
  - **El servidor ya tiene lo de un jugador.** Corre todo `Game.ECS`: `SquadSwapSystem`, la canalización y `SquadSwapExecutionSystem`
    (`Assets/Scripts/Squads/Systems/`).
  - **Nadie lo pide.**
    - `MandoSystem` (`Net/MandoSystem.cs:137-193`) nunca pone `SquadSwapRequest`.
    - `EntradaClienteSystem` (`Net/ClienteRedSystems.cs:59-159`) no lee ninguna elección.
    - `SupplyPointUIController` (`UI/Zone/SupplyPointUIController.cs:262-272`) busca un héroe con `IsLocalPlayer`, y en red no lo encuentra.
  - **El cliente no tiene la reserva.**
    - `InterfazRed` solo manda la primera escuadra del mapa (`MandoSystem.cs:319-320`), que deja de ser la activa tras un cambio.
    - El `DataContainer` del cliente va vacío (`EspejoClienteSystem.cs:321-349`).
- **Interactuar va con él.** En la batalla, interactuar es la tecla F que abre el panel de suministro, y es cosa del cliente: hoy el cliente de
  red no la lee (`HeroInputSystem` no corre allí).
- **Apuntar con la cámara va aparte**, aunque toque el mismo comando.
  - El mando gira hacia donde se mueve (`MandoSystem.cs:65-79`).
  - El servidor copia la rotación solo si se mueve (`MandoSystem.cs:256-260`).
  - La dirección de la cámara nunca viaja: solo sirve para pasar WASD a ejes del mundo (`ClienteRedSystems.cs:176-191`).
- **El arreglo:**
  1. **El comando.** `EntradaJugador` lleva `CambiarEscuadra` (un `InputEvent`), `EscuadraPedida` (el `squadId`) y `Mirar` (la dirección de la
     cámara en el plano).
  2. **El servidor.** En `MandoSystem`, con `CambiarEscuadra`, pone `SquadSwapRequest { newSquadId, zoneId }` si el héroe está en un punto de
     suministro propio. Lo demás lo valida `SquadSwapSystem`.
     - `InterfazRed` pasa a la escuadra activa.
     - Un búfer `ReservaRed` para el dueño lleva las escuadras de reserva, cuál está activa, si canaliza y la espera.
  3. **El cliente.**
     - F marca `interactPressed` en el fantasma del héroe propio.
     - `SupplyPointUIController` encuentra ese fantasma por su dueño, lee `ReservaRed` y deja la petición para que `EntradaClienteSystem` la
       mande.
  4. **Apuntar.**
     - `MovimientoMandoSystem` gira hacia `Mirar` también quieto.
     - `MandoSystem.Llevar` copia esa rotación al héroe aunque no se mueva.
- **La prueba**, con el patrón de `Assets/Tests/PlayMode/BatallaRealServidorTests.cs`: el mundo servidor sin cliente y un mando simulado.
  - **El cambio.** El héroe en un punto de suministro propio con dos escuadras, `CambiarEscuadra`; comprueba que cambia la escuadra activa y
    que sale la nueva.
  - **Apuntar.** Quieto y con `Mirar` a +X, el héroe acaba mirando a +X.
  - Además, un vídeo o capturas del panel y del cambio con un cliente de verdad.

## 16. Quitar el cálculo de experiencia y botín que hace Conquest por su cuenta

- **Lo que hay.** En modo conectado, Conquest ya no calcula ni experiencia ni botín.
  - **No hay nada que lo alimente.** Los sistemas de experiencia (`Squads/Systems/SquadProgression.System.cs` y `Hero/Systems/HeroLevel.System.cs`)
    solo consumen eventos (`SquadXPEvent`, `XPEventComponent`) que nadie crea fuera de las pruebas.
  - **No hay botín local de batalla.**
  - **La batalla real va sin héroe local.** Monta la batalla sin héroe local (`BattleBootstrap.System.cs:147-156`, `NoLocalHero`), así que
    tampoco guardaría nada.
  - **Lo que se manda al motor no lleva ni experiencia ni botín** (`Net/BatallaDeTicket.cs:174-215`), porque los pone el motor.
- **Lo que se rompería al quitarlo: nada que se vea.** El informe del mundo (`PantallasDeMundo.Informe.cs`) compara el antes y el después que da
  el motor; no usa nada local.
- **No depende del hecho de cierre pedido al motor.** Ese hecho solo mejoraría el informe: hoy resta antes y después, y no enseña la
  experiencia de las escuadras.
- **Lo único local que sí corre al cerrar una batalla** es el guardado de las bajas como `armorPercent`
  (`Squads/Systems/UnitDeploymentValidation.System.cs:128-184`), y solo en el juego de un jugador.
- **Lo que queda** es elegir qué hacer con los sistemas que hoy no alimenta nada (duda 1).

## Lo que voy a hacer

- **Ahora, el 12:** sin dudas.
- **Después, el 13:** con la propuesta de la duda 2.
- **El 16:** espera a la duda 1.
- **Cada arreglo, con su prueba.**
- **Al acabar:**
  - la suite en dos lotes y las dos batallas;
  - capturas o vídeo de los arqueros disparando y del cambio de escuadra;
  - la Hoja y el traspaso al día;
  - los servidores apagados.
- **Sin commit** hasta que Víctor lo diga.

## Dudas

1. **Los sistemas de experiencia de un jugador**, que hoy no alimenta nada: `SquadProgressionSystem`, `HeroLevelSystem`,
   `HeroExperienceCalculator.GetBattleXPForNextLevel` y sus pruebas.
   - a) Borrarlos con sus pruebas: la experiencia la pone el motor, y en modo conectado no hacen nada. **(propuesta)**
   - b) Dejarlos para un modo de un jugador sin motor y apuntar en la Hoja que el 16 ya está resuelto en modo conectado.
2. **Apuntar con la cámara en red.**
   - a) En la misma tanda que el cambio de escuadra: toca el mismo comando y son pocas líneas. **(propuesta)**
   - b) Aparte, después.
3. **Dos escuadras en la prueba del cambio.** Sin ticket, `BatallaRealSystem.Cargar` da una sola escuadra a cada héroe
   (`BatallaRealSystem.cs:102-103`).
   - a) Una opción de arranque para dar N escuadras por héroe (como `-tropas`), que sirva también al bot. **(propuesta)**
   - b) Probar con un ticket de verdad.

## Respuestas de Víctor (2026-11-16)

1. **a)** Borrados los sistemas de experiencia de un jugador con sus pruebas.
2. **a)** Apuntar con la cámara va con el cambio de escuadra.
3. **Ninguna de las dos tal cual.** No hay opción de arranque de N escuadras. La prueba usa un ticket de verdad con dos escuadras elegidas
   (`DelMapaALaBatalla`: leñadores y granjeros, 14 de 14 de Liderazgo). La regla:
   - en la preparación hay dos listas: **disponibles** (todas las tropas si está en su base; si no, las que lleva) y **elegidas** (lo que cabe
     en el Liderazgo, un subconjunto de las disponibles);
   - el héroe entra siempre con las elegidas, aunque sea una;
   - en la batalla solo cambia entre las elegidas.
   - Hoy el ticket del motor ya lleva solo lo que el héroe lleva (columna o loadout activo), pero Conquest no deja elegir en la preparación
     ni el motor lo recibe: va en el pedido 1 al motor (elegir escuadras), con un campo de escuadras elegidas.
