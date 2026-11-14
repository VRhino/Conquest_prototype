# Paso 5: preparación de batalla y después de la batalla (análisis y dudas)

2026-11-13. Es solo análisis: no se ha tocado nada de Assets ni hay commits. Todo se queda así hasta que Víctor conteste las dudas del
final.

El flujo que pide Víctor:

> Mundo --atacar--> BattlePrep (elegir, de lo que hay, con qué entras) --> Batalla --gana/pierde--> PostBattle (qué hizo en combate, cómo
> lo hicieron las tropas, tabla de integrantes) --Salir--> mundo de vuelta.

Fuentes:
- Conquest: `Assets/Scripts/UI/BattlePreparation/*`, las escenas BattlePrepScene y PostBattleScene, `BattleSceneController`,
  `SceneTransitionService`, `Assets/Scripts/Net/*`, `Docs/GDD.md` y `Docs/TDD.md`.
- Motor 358af5ff: `batallas.rs`, `battle.proto`, `orders.proto`, `entities.proto`, `Batalla_Unity_Ciclo.md`, `Trabajo_En_Conquest.md`
  y `Heroe_Y_Tropa.md`.

## Lo que hay hoy

### BattlePrepScene (UGUI de Conquest)

Es una pantalla que funciona, pero no es del motor.

**Qué deja elegir:**
- Las escuadras, por su id de instancia y con el tope de Liderazgo (`FooterController` 226-287, 405-437, 509-522).
- Guardar el loadout, en el `player_save.json` local (477-499). «Ver loadouts» está sin hacer.
- El equipo del héroe, contra el inventario local (`BattlePreparationController` 379-402).
- El punto de aparición en el minimapa (404-443). Tiene un `[TODO] enviar el evento`.
- No deja elegir perks ni formación.

**Cómo empieza la batalla.** El botón Empezar, o el fin del temporizador (60 s, `Battle.Data.cs:18`), lanza una cuenta atrás de 10 s. Al
acabar, `TransitionToBattle` pasa la elección en `BattleData` a `BattleTransitionData` y carga BattleScene (465-500). No hay estado de
«listo», no espera a otros jugadores y no tiene camino de red.

**De dónde saca los datos:**
- `BattleTransitionData` y, si no hay, `TestEnvironmentInitializer` con `player_save.json` y una `BattleData` inventada
  (`BattleDebugCreator`).
- El héroe es siempre `PlayerSessionService.SelectedHero`, que es local.
- Las escuadras, el Liderazgo, el inventario y los loadouts son todos del guardado local. Nada viene del motor.

**Cómo se llega.** Se entra desde el matchmaking viejo y falso (`MatchmakingUI` 255-260, `MatchmakingService` 443-479).

**La Hoja** (línea 104) dice que estas pantallas de fuera de la batalla son provisionales y se borran cuando llegue su sustituta.

### PostBattleScene

- **No es más que un hueco.** Tiene un texto «PostBattle» y un botón «Salir» que vuelve por `MatchmakingService.ReturnFromBattle`, o a
  Feudo si no hay servicio.
- **No enseña nada.** `BattleTransitionData.WinnerTeam` se rellena (`BattleSceneController.cs:246`), pero no lo lee nadie.
- **El cliente en red no llega a ella.** `HandleMatchEnd` solo la carga con `!NoLocalHero` (186-188), y el cliente en red tiene
  `NoLocalHero = true`. Se queda en BattleScene con el panel de victoria o derrota.
- **El GDD la quiere con tres pestañas** (§8, 1599-1611): General (resultado, tiempo y puntos), Escuadras (bajas causadas, tiempo en
  punto y daño recibido) y Héroe (habilidad más usada, perks, daño por tipo y asistencias), más la experiencia.

### Lo que sabe cada parte al acabar

**El servidor de batalla:**
- Por escuadra, los desplegados y los muertos (`EscuadraEnJuego`), y por héroe, si cayó (`HeroeEnJuego.Caido`). Lo cuenta
  `BatallaRealDeTicket.ContarBajas`.
- Se lo manda al motor en `BattleResult`. Al cliente no le manda nada de eso.
- No cuenta el daño hecho ni el recibido, ni las bajas causadas por escuadra, ni el tiempo en un punto, ni el uso de habilidades.
- `HeroResourcesComponent.kills` es un recurso que gastan las habilidades, no un total.

**El cliente en red:**
- `PartidaRed`: el estado, el ganador y el reloj.
- `InterfazRed`: su héroe ahora mismo (vida, aguante, y los soldados de su escuadra activa).
- Los `GolpeRed` de cada golpe, que valen para los números que saltan. Se podrían sumar, pero hoy nadie lo hace.

**El motor (y el mundo, porque la sesión sigue viva):**
- `Batalla.resultado`: el ganador, el motivo, y por escuadra los desplegados, los supervivientes y los muertos. Por héroe, si participó
  y si sobrevivió.
- El ticket, con quién entró y con qué: héroes y escuadras, con nivel, experiencia y moral al abrirse.
- Esto se ve en el ámbito `BATALLA:<uuid>` durante 60 min de mundo.
- Los hechos:
  - `evento_pve.aplicada` (`BatallaCerrada`): el ganador y el motivo.
  - `heroe.botin` (objetos a la mochila o al almacén).
  - `heroe.nivel_subio`.
- Sin hecho propio:
  - **La herida de batalla** no lanza `heroe.herido`, que solo sale con la orden de administración. Se ve en `herido_hasta_ms`.
  - **La experiencia** de escuadras y héroe (4 si se gana, 2 si se pierde), las monedas, los recursos de los bandidos al carro y la
    mitad del carro perdida. Solo se ven comparando el estado de antes con el de después.

### El motor ante «elegir con qué entras»

- **`OrdenAtacar`** solo tiene `heroe_id`, `objetivo` y `declara_guerra`.
- **Al atacar entra la columna entera.** `heroes_de_columna` (`batallas.rs:354-366`) toma a todos los héroes sanos de la columna, cada
  uno con todas sus escuadras de la columna que tengan soldados. Tampoco `UnirseABatalla` elige.
- **Hay loadouts en el motor.** Son la entidad `Loadout` (`squad_ids`, `activo`, `liderazgo_total`) y la orden `OrdenGuardarLoadout`.
  Pero solo los usa la defensa de un asedio (los residentes entran con su loadout activo). Al atacar desde el mapa, no se usan.
- **La formación sí se elige, pero antes.** `OrdenElegirFormacion` se puede mandar en cualquier momento salvo en batalla. El ticket la
  congela al abrir la batalla (`SquadSnapshot.formacion` y `formaciones_permitidas`), y Conquest la usa.
- **El equipo** se cambia solo dentro de una plaza, un pueblo o un campamento, y no con la escuadra en batalla. El ticket lo congela.
- **El punto de aparición** no está en el ticket. Lo pone el servidor de batalla por orden: del 1 al 3 los atacantes, del 4 al 6 los
  defensores (`BatallaRealDeTicket` 46, 58 y 125).
- **El ticket no cambia en la batalla.** El resultado tiene que traer todas las escuadras del ticket (§8.5.3). Quien no se conecta antes
  del inicio sale como `no_conectado`, sin castigo, y la batalla vuelve a convocarse.
- **Cuándo empieza.** Desde `asignada` hay hasta 5 min de mundo. Empieza cuando el servidor de batalla manda `InicioBatalla`, y lo decide
  Conquest. Hoy espera a los humanos o a `-espera` (60 s): `BatallaDeTicketSystems` 111-137.
- **Los documentos del motor no esperan** una pantalla de preparación ni una de después de la batalla.

## Las dudas, por tramo del flujo

Cada una dice qué hay hoy, por qué es una duda, las opciones y la que recomiendo.

### (a) Mundo → atacar → preparación

1. **Si «Atacar» lleva a la preparación o la preparación lleva a atacar.**
   - Hoy «Atacar» manda la orden y abre la batalla en el motor. En ese momento el ticket ya está cerrado con la columna entera.
   - Si la preparación va después, lo que se elija ahí no cambia qué escuadras entran (ver b).
   - Opciones:
     - (A) «Atacar» abre primero la preparación en el cliente. Al confirmar se ordenan los cambios que el motor admite (formación) y
       luego `atacar`.
     - (B) «Atacar» manda la orden y la preparación va mientras se convoca o se asigna.
     - (C) Las dos cosas: lo que cambia el ticket va antes de atacar y lo de dentro de la batalla, después.
   - **Recomiendo (C).** Antes de atacar: la formación y, si el motor lo admite, las escuadras. Después de atacar, mientras llega el
     servidor: el punto de aparición y lo que decida el servidor de batalla.
2. **Si la preparación es la escena BattlePrepScene de Conquest o una pantalla nueva.** La escena está hecha sobre el guardado local
   y no habla con el motor. La Hoja dice que es provisional. Víctor la nombra.
   - Opciones:
     - (A) Reutilizar BattlePrepScene y cambiarle las fuentes de datos (motor en vez de `player_save.json`).
     - (B) Hacer la preparación en UI Toolkit, encima del mapa, y retirar la escena.
     - (C) Rehacer la escena UGUI desde cero.
   - **Recomiendo (A).** Se queda su forma (escuadras con Liderazgo, minimapa con puntos, cuenta atrás, equipo) y se le quita lo local.
3. **Si la preparación tapa el mundo** (escena aparte, el mapa se va) o se monta encima (aditiva).
   - Hoy la entrada a la batalla quita el mapa y deja viva la sesión.
   - **Recomiendo** una escena aparte. El objeto de la sesión ya sobrevive con DontDestroyOnLoad, y así se cargan BattlePrep y luego
     BattleScene como hoy.
4. **Qué ataques pasan por la preparación.** Hoy solo hay bandidos. Están también las plazas, los pueblos, las avanzadas, las caravanas,
   las columnas y unirse a una batalla.
   - **Recomiendo** que en el paso 5 solo los bandidos. Lo demás, cuando llegue su paso, con la misma preparación.

### (b) Dentro de la preparación: qué se elige de verdad

5. **Elegir qué escuadras entran.**
   - Hoy entra toda la columna, y en el motor no hay orden para elegirlo.
   - Opciones:
     - (A) **Pedido al motor:** `escuadron_ids` en `OrdenAtacar` (y en `UnirseABatalla`), o que use el loadout activo como en la defensa
       de un asedio, filtrado a lo que va en la columna.
     - (B) Sin pedido al motor: la escuadra que no entra se «deja» en el servidor de batalla. No vale, porque el resultado tiene que
       traer todas las del ticket y quedaría «desplegada y sin bajas» sin haber entrado.
     - (C) Elegirlo al salir del campamento (lo que lleva la columna, que ya existe), y que la preparación solo lo enseñe.
   - **Recomiendo (A)** para hacerlo bien y **(C) mientras tanto**. Hay que decidir si va el pedido al motor.
6. **El tope de Liderazgo.** La escena lo calcula con datos locales (`DataCacheService`). El motor tiene `Loadout.liderazgo_total` y
   las reglas de Liderazgo en `Heroe_Y_Tropa`.
   - **Recomiendo** sacar el tope y el coste del motor, de `/v1/datos` y del héroe, y no del guardado local.
7. **Los loadouts de la escena** («Guardar loadout», que va al guardado local) frente a los del motor (`OrdenGuardarLoadout`).
   - **Recomiendo** que, si se eligen escuadras, se guarde con la orden del motor. Si no se eligen, que se oculte el botón.
8. **La formación.** La escena no la deja elegir. En el motor ya existe `OrdenElegirFormacion` y el ticket la lleva.
   - **Recomiendo** añadirla a la preparación, enviada antes de atacar (ver la duda 1). Una vez abierta la batalla no se cambia.
9. **El equipo del héroe.** La escena lo cambia contra el inventario local. En el motor solo se puede dentro de un sitio, y en el mapa
   no.
   - **Recomiendo** enseñarlo sin poder cambiarlo, y decir dónde se cambia.
10. **El punto de aparición.**
    - La escena lo deja elegir en el minimapa, pero no lo envía a ningún sitio (TODO).
    - En red, el servidor reparte los puntos por orden de llegada.
    - Hace falta un mensaje nuevo del cliente al servidor de batalla: el código del hilo de tropas.
    - **Recomiendo** dejarlo **visible y fijo** (el que le da el servidor) en esta tanda, y pedir el mensaje al hilo de tropas.
11. **El mapa del minimapa.** Hoy es el de la escena de prueba. El ticket trae `mapa_referencia` y un punto, y el terreno de batalla
    desde el ticket está pendiente (bloque 3, línea 122).
    - **Recomiendo** usar el mapa que va a salir de verdad (el que use el servidor) y no inventar uno.
12. **Los perks y las habilidades.** El GDD los pone en la preparación. En el motor los perks se abren solos con el nivel (D15), y el
    loadout perdió `perks_seleccionados`.
    - **Recomiendo** no ofrecer elegir perks, porque no hay qué elegir. Como mucho, enseñar los que tiene.
13. **El temporizador de la preparación** (60 s más 10 s, de relleno) frente a los plazos del motor (5 min hasta la asignación y 5 min
    hasta el inicio).
    - **Recomiendo** que la cuenta atrás que se ve salga de los plazos de verdad. El servidor de batalla decide cuándo empieza.
14. **Lo que la escena enseña hoy de relleno:** `BattleDebugCreator`, `TestEnvironmentInitializer`, los héroes de prueba y el lado
    contrario inventado.
    - **Recomiendo** enseñar solo lo del ticket y lo del motor. Si no hay dato, no sale.

### (c) Cuándo ocurre la preparación respecto a los estados

15. **En qué estado se prepara.**
    - Opciones:
      - (A) Mientras se convoca: en las batallas contra bandidos se publica en el acto, así que la espera es la de un servidor (en las
        pruebas, segundos).
      - (B) Ya asignada, con el token, antes del inicio. Hasta 5 min. El servidor de batalla espera a los humanos o a `-espera`.
      - (C) Dentro del servidor de batalla, como fase de preparación antes de `InicioBatalla`.
    - **Recomiendo (B)+(C).** El cliente entra con el token a una fase de preparación que lleva el servidor de batalla, con «listo», y
      el servidor empieza cuando todos están listos o se acaba el tiempo. Así el que llega tarde y los varios jugadores tienen sitio, y
      el servidor puede respetar lo que se elige dentro de la batalla (punto de aparición).
    - Toca el código del hilo de tropas (ver f).
16. **Qué pasa con la ficha de espera de la 5A.**
    - **Recomiendo** que la preparación la sustituya mientras hay batalla. El estado y el plazo se quedan como cabecera de la
      preparación.
17. **Dónde va «Cancelar la batalla».**
    - Hoy está en la ficha de espera, para quien la inició, en convocando o asignada.
    - **Recomiendo** que esté en la preparación, con las mismas reglas, y que desaparezca al empezar.
18. **Batalla «fallida»** (5 min sin servidor o sin empezar).
    - **Recomiendo** cerrar la preparación, decir la frase de «se anula sin castigo» y volver al mapa con la columna libre.
19. **Volver de la preparación sin combatir** (atrás). Si la batalla ya está abierta, la única forma es cancelarla.
    - **Recomiendo** que no haya «atrás» suelto: solo «Cancelar la batalla», si se puede.

### (d) Batalla → después de la batalla: de dónde sale cada dato

20. **Cuándo se va el cliente a PostBattle.**
    - Hoy el cliente en red se queda en BattleScene con el panel de victoria o derrota.
    - **Recomiendo** dejar el panel unos segundos (TDD §9.8 habla de 10 s) y pasar a PostBattle. Cambia `HandleMatchEnd`, que es código
      del hilo de tropas.
21. **El resultado: si lo da el servidor de batalla o el motor.**
    - El servidor lo sabe al instante, pero no se lo manda al cliente.
    - El motor lo tiene en cuanto lo aplica (`Batalla.resultado`) y la sesión viva lo trae.
    - **Recomiendo** el motor como fuente de lo que cuenta (ganador, bajas por escuadra, quién cayó) y el servidor solo para lo que el
      motor no tiene. Se espera en PostBattle a que llegue «aplicada», con un «calculando…».
22. **«Qué hizo en combate» y el desempeño de las tropas** (bajas causadas, daño hecho y recibido, tiempo en punto, habilidad más
    usada).
    - Hoy no lo cuenta nadie: ni el servidor ni el cliente.
    - Opciones:
      - (A) Que el servidor de batalla lo cuente por escuadra y por héroe y se lo mande al cliente antes de cerrar. Es código del hilo de
        tropas, y es lo bueno.
      - (B) Que el cliente sume los `GolpeRed` que ve. Es incompleto, porque solo ve lo que le llega.
      - (C) Enseñar solo lo que hay: los desplegados, los supervivientes y los muertos de cada escuadra, y el ganador y el motivo.
    - **Recomiendo (C) ahora y pedir (A)** al hilo de tropas para las estadísticas del GDD.
23. **La tabla de integrantes.**
    - El ticket trae todos los héroes y escuadras de cada lado, y el resultado sus bajas.
    - **Recomiendo** una tabla con una fila por escuadra (icono de tropa, dueño, entraron, quedan y muertos) y los héroes con si cayeron,
      de los dos lados, como `informeCombate.ts`.
24. **La experiencia ganada.**
    - No hay hecho. Es fija: 4 si se gana, 2 si se pierde, más el 10 % con la tecnología de ejército permanente.
    - Opciones:
      - (A) Comparar el antes y el después de las escuadras en `HEROE`.
      - (B) Calcularla con la regla.
      - (C) Pedir al motor un hecho con la experiencia.
    - **Recomiendo (A).** Es lo que de verdad pasó, sin copiar reglas.
25. **El botín, «Pierdes del carro» y «Quedas herido»**, como el informe del prototipo.
    - Los recursos al carro y la mitad perdida: comparando el carro antes y después.
    - Las monedas: comparando.
    - Los objetos: `heroe.botin`.
    - La herida: `herido_hasta_ms`.
    - **Recomiendo** hacerlo así, y pedir al motor un hecho de cierre con todo eso para no tener que comparar (Pedido al motor).
26. **Las pestañas del GDD** (General, Escuadras, Héroe).
    - **Recomiendo** General y Escuadras con lo que hay, y la de Héroe solo con lo que exista (cayó o no, herido, nivel). Sin datos
      inventados.
27. **Si el informe de combate se ve también desde el mundo** (como en el prototipo: un aviso con «ver informe»).
    - **Recomiendo** que sí. Es la misma vista, reutilizada en el panel de avisos, mientras el motor la guarda (60 min). Después, lo que
      se apunte en el aviso.

### (e) Salir → mundo de vuelta

28. **Cómo se vuelve.**
    - Hay que quitar el mundo ECS cliente, recrear el de por defecto, reiniciar los estáticos de red y volver a enseñar el mapa.
    - El objeto de las pantallas y la sesión sigue vivo.
    - **Recomiendo** cargar MundoScene y que el objeto vivo la adopte (y destruir el duplicado que traiga la escena), o volver a enseñar
      la interfaz viva sobre una escena vacía.
    - MundoScene no está en `SceneTransitionService` ni en la lista de escenas del build: hay que añadirla.
29. **Dónde aparece el jugador.**
    - El motor devuelve la columna al mapa donde estaba y reanuda su plan (Ciclo §9). Si la batalla fue dentro del campamento, en la
      puerta.
    - **Recomiendo** el mapa centrado en su columna, con la ficha del sitio de la batalla abierta.
30. **Salir por tu cuenta en mitad de la batalla** (hoy F10 dos veces; no hay botón).
    - El motor lo apunta como `abandono`, con bajas y herida.
    - **Recomiendo** un botón «Abandonar la batalla» con confirmación, que lleve a PostBattle con lo que haya. Es código del hilo de
      tropas.
31. **Eliminado sin que te devuelvan.** En campo abierto el héroe que cae se queda mirando. Contra bandidos reaparece, así que no aplica
    al paso 5.
    - **Recomiendo** dejarlo para cuando haya batallas campales, con la señal del servidor que haga falta.
32. **Batalla cancelada o fallida.** No hay batalla, así que tampoco PostBattle.
    - **Recomiendo** la frase y el mapa.
33. **Que se caiga la conexión con el servidor de batalla.** El servidor da 60 s de gracia con la IA y luego `desconexion`.
    - Opciones: reintentar (lo hace ya `ReconexionClienteSystem`) y, si no vuelve, que el motor lo cierre y se vuelva con lo que diga.
    - **Recomiendo** que, si no vuelve, se pase a PostBattle con «perdiste la conexión».
34. **Reiniciar el estado estático entre batallas.** Es necesario para la segunda batalla del mismo proceso.
    - Víctor pidió no implementar nada todavía.
    - **Recomiendo** hacerlo en la primera tanda que se apruebe.

### (f) Lo que toca código del hilo de tropas

35. **BattlePrepScene:** cambiar sus fuentes de datos al motor y la forma de entrar (no por el matchmaking). ¿Lo hace este paso o ese
    hilo?
    - **Recomiendo** este paso, porque es fuera de la batalla y su sustituta es parte de esta migración.
36. **La fase de preparación en el servidor de batalla:**
    - un estado «preparando» con temporizador;
    - el «listo» de cada jugador;
    - el punto de aparición elegido;
    - que `EspejoClienteSystem` cargue la preparación y no BattleScene al conectar.

    **Recomiendo** que lo haga el hilo de tropas o que yo lo haga con su visto bueno, porque cambia su diseño.
37. **`HandleMatchEnd`:** dejar que el cliente en red pase a PostBattle. Es un cambio mínimo, de una condición.
38. **Que el servidor mande el resultado y las estadísticas al cliente antes de cerrarse** (duda 22 A).
39. **Reiniciar el estado estático** (`OpcionesRed`, `NoLocalHero`, `NetworkTeam`, `ReconexionClienteSystem`, `BattleTransitionData`).
40. **El error de `BattleSceneController.Awake`** sin BattleData en red. Si la preparación pasa la elección a BattleScene como hoy
    (`BattleTransitionData`), el error se va solo.
41. **`IsLocalHero`, que el espejo de red no marca.** Le hace falta a cualquier cosa que busque «mi héroe» en BattleScene.
    - **Recomiendo** que lo marque el espejo. Es código del hilo de tropas.
42. **`TestHeroPool`:** el aspecto y el equipo en batalla salen de héroes de prueba. En la preparación y en PostBattle habría que
    enseñar el héroe de verdad.
    - **Recomiendo** que la preparación enseñe el del motor, y que la batalla siga así hasta que el bloque 3 lo cierre.
43. **El botón para salir** (duda 30) **y el aviso de conexión** (hoy un recuadro OnGUI). Son pendientes del bloque 3, línea 124.

### (g) La interfaz

44. **Escenas UGUI de Conquest o UI Toolkit.**
    - La regla de Víctor: UGUI en la batalla y UI Toolkit fuera.
    - La preparación y PostBattle están en medio.
    - **Recomiendo** UGUI para las dos, porque son escenas de Conquest pegadas a la batalla, y con la paleta y las letras del mundo
      (bronce, Inter y Cinzel) para que no cambie de golpe. Hay que confirmarlo.
45. **El idioma.** Esas escenas tienen textos en inglés y en español mezclados.
    - **Recomiendo** todo en español, como el resto.
46. **Los iconos del prototipo en esas escenas UGUI.**
    - El registro (`IconosDelMundo`) da VectorImage de UI Toolkit, que UGUI no pinta directamente.
    - Opciones:
      - (A) Que el script saque también PNG a 4× para UGUI.
      - (B) `SVGImage` del paquete Vector Graphics.
    - **Recomiendo (A).** Es sencillo, con el mismo script y los mismos dibujos.
47. **Lo que hoy es de relleno y hay que ocultar**, por la regla de «solo lo que el jugador puede hacer»:
    - «Ver loadouts»;
    - el inventario local;
    - el lado contrario inventado;
    - el temporizador de 60 s;
    - el punto de aparición si no se respeta;
    - los perks.
    - **Recomiendo** ocultar todo eso hasta que tenga detrás algo de verdad.

### (h) Varios jugadores en la misma batalla

48. **Contra bandidos solo ataca tu columna,** pero en ella puede haber varios héroes (un ejército), y otros pueden unirse
    (`UnirseABatalla`, incorporación).
    - Cada humano con token se prepara por su cuenta.
    - **Recomiendo** que la preparación aguante a varios desde el principio (la lista de quién está listo sale del servidor de batalla),
      pero probarlo con uno en el paso 5. Varios jugadores de verdad, en el paso de las batallas grandes.
49. **Quien llega tarde** (se une con la batalla en curso). El motor lo admite como incorporación.
    - **Recomiendo** que entre directo a la batalla, sin preparación (o con una corta), y dejarlo para el paso de unirse a batallas.

## Tandas que propongo (cada una con parada)

- **5B: preparación.**
  - Al atacar unos bandidos: se elige la formación de cada escuadra (orden del motor) y se ven las escuadras de la columna con su
    Liderazgo.
  - «Atacar» y BattlePrepScene alimentada por el motor, con el estado, el plazo y «Cancelar la batalla».
  - Al llegar el token, a la batalla.
  - Si se aprueban las dudas 15 y 36: la fase de preparación en el servidor con «listo».
  - Se verifica con batalla real.
- **5C: después de la batalla y vuelta.**
  - Del panel de victoria o derrota a PostBattle con el resultado del motor (bajas por escuadra, quién cayó, experiencia, botín y
    herida).
  - «Salir» lleva al mapa en su sitio con la sesión viva, y se reinician los estáticos.
  - También la batalla cancelada o fallida, y el abandono.
  - Se verifica con batalla real, que tiene que acabar: con `CONQUEST_DURACION` corto, o atacando de verdad.
- **Pedidos al motor**, si Víctor los aprueba:
  - elegir las escuadras que entran (duda 5);
  - un hecho de cierre con la experiencia, el botín, las monedas y lo perdido del carro (duda 25).
- **Pedidos al hilo de tropas**, si no los hace este paso:
  - la fase de preparación en el servidor (36);
  - las estadísticas por escuadra y héroe al cliente (38);
  - `IsLocalHero` (41);
  - el botón para abandonar (43).

## Respuestas de Víctor (2026-11-14) y lo que implican

Lo que no contestó lo da por bueno tal como se propuso.

- **1. Bandidos:** atacas en mundo abierto, así que solo cuenta la tropa que llevas. La preparación enseña lo que cargas y deja **cambiar el
  orden**: con la primera entras y el resto se cambia en los puntos de suministro. El motor ya tiene `ordenarEscuadras` (comando 57). Falta
  comprobar que el ticket conserva ese orden y que el servidor de batalla empieza con la primera.
- **4.** Todos los ataques pasan por la preparación; se van abordando según lleguen.
- **5.** Bandidos: (C). Defender tu residencia: eliges de entre todas tus tropas lo que quepa en el Liderazgo. Encaja con el motor, que
  mete en la defensa el **loadout activo** de quien se une: la preparación guarda ese loadout (`guardarLoadout`) y después se une. Va con
  su paso, el de los asedios.
- **6.** Liderazgo, del motor.
- **10. «Punto de entrada»** = punto de aparición en el mapa de la batalla. Hay que explicárselo.
- **13. Los tiempos** = plazos del motor (Batalla_Unity_Ciclo: 5 min hasta asignar, 5 min hasta empezar) y la espera del servidor
  (`-espera`, 60 s). Hay que explicárselo.
- **15. Por qué toca el código del hilo de tropas:** la fase «listo» vive en el servidor de batalla (`Conquest.Net`). El mensaje de Víctor
  quedó cortado («Pero si como dices»): hay que preguntarle.
- **17.** Con varios héroes en la convocatoria, uno puede bajarse sin cancelar, y si se baja el último se cancela. El motor no tiene una
  orden para bajarse antes de empezar: **pedido al motor**.
- **27.** El informe también desde el mundo, pero **guardado fuera del motor, en una base de datos aparte**, que se pueda consultar, y
  ver qué más se puede llevar allí. Es una decisión de arquitectura: hay que preguntar dónde va y quién escribe en ella.
- **29.** La batalla, como miniatura en el mapa, en sus coordenadas y con la que se puede interactuar:
  - con bandidos, se unen aliados mientras quepan;
  - en una batalla campal, nadie entra de fuera;
  - en un asedio, la plaza cambia de miniatura y sigue sus reglas;
  - al acabar, los héroes vuelven a sus coordenadas.

  El motor ya da `Batalla.punto` y `lado_para_unirse`. Esto entra con el paso de unirse a batallas (11).
- **42.** El aspecto **real** también en la batalla. El ticket ya lo trae (`HeroSnapshot.avatar` y `femenino`), y el servidor de batalla
  tendría que vestir con él en vez de usar `TestHeroPool`: es código del hilo de tropas.
- **44.** Pregunta si se pueden migrar a la interfaz nueva. Sí: UI Toolkit va en cualquier escena.

## Preguntas nuevas (numeración que ve Víctor) y lo propuesto

- **54.** ¿El orden cambiado se guarda en el héroe o vale solo para esa batalla? Propuesta: se guarda (`ordenarEscuadras`).
- **55.** ¿La preparación cabe en los 5 min del motor o se alarga? Propuesta: cabe.
- **56.** ¿Quien se baja sin cancelar vuelve a su columna tal cual? Propuesta: sí.
- **57.** La defensa de la residencia por loadout, ¿va en el paso de asedios? Propuesta: sí.
- **Fuera de este paso:**
  - la base de datos de informes, que se analiza en un hilo aparte del motor;
  - unirse desde fuera a la batalla de la miniatura, que va en su paso.
- **En este paso (propuesto):**
  - la miniatura de la batalla en el mapa, solo para verla;
  - la vuelta de los héroes a las coordenadas de la batalla;
  - el aspecto real del héroe en la batalla, que lo hago yo aquí.
- **15/36:** propuesto hacerlo aquí (el «listo» en el servidor de batalla). Falta su respuesta y el final de la frase que se cortó.
- **Decidido, salvo que diga otra cosa:** la preparación y el después de la batalla, en UI Toolkit con las piezas de tropa.
- Nada de código hasta que conteste.

## Respuestas de Víctor (2026-11-14 17:13)

- **54:** sí, con `ordenarEscuadras` del motor.
- **55:** cabe. La preparación va sin el temporizador de 60 s, dentro del plazo del motor.
- **56:** sí.
- **57:** sí.
- **El resto, como se propuso:**
  - 15/36: el «listo» en el servidor de batalla, aquí;
  - el aspecto real en la batalla, aquí;
  - la miniatura en el mapa, solo para verla, y la vuelta a las coordenadas;
  - UI Toolkit.

**Desbloqueado: se empieza por la tanda 1.**

## Tanda 1: lo hecho (2026-11-14)

Los cambios sobre lo acordado, decididos por Víctor el 14-11:

- **No se guarda nada en el motor.** El orden y la formación los aplica solo el servidor de batalla en esa batalla. Sustituye a la 54.
- **La preparación va en escena aparte (`PreparacionScene`).** Cancelar o anular vuelve a MundoScene con la sesión viva
  (`VolverAlMundo`), y esa vuelta la reutiliza la tanda 2.
- **`IsLocalHero` no se marca en el espejo**: le pondría el motor físico local.
- **La prueba del reinicio:** atacar, conectar, cancelar, volver al mapa, atacar otra vez, cambiar el orden, «Listo» y entrar en la batalla.

Todo lo demás está en Docs/Arquitectura/28, en «Tanda 1 del paso 5».
