# GDD

GDD mvp

Concepts clave

1. Heroes: avatar del jugador, creado por el no viene pre creados Como las squads
2. Skins de heroe: skins visuals a las piezas de armadura o arma, solo visuals no afectan en nada a Sus atributos
3. Armas: piezas con las q ataca el heroes solo hay Una pieza, esta determinanla clase del heroes, hay 4 tipos, espalda y escudos, lanza, Arco y espalda a 2 manos
4. Piezas de armadura: son las piezas de armadura q se equipa el heroes, son 4 piezas casco, guantes, Pero y pantalones, hay de 3 tipos, ligera, media y pesada
5. Squads: conjunto de unidades qué posee UN jugador, el cual tiene formaciones(linea, dispersal, testudo, etc…) y ordenes(seguro, atacar, manner posicion), Durante la batalla no puede Haber mas de Un squad activo por heroes Al mismo tiempo, pueden Haber n cantidad de unidades dentro de UN squad
6. Skins de unidad: skin visual para la unidad
7. Unidad: individuo que conforman UN Squad solo puede Haber UN Tipo de unidad dentro de UN squad
8. Perks:  Los talentos (perks) son **modificadores pasivos y activos** que el jugador desbloquea para personalizar su estilo de juego, su rol en combate y la sinergia con su escuadra. El sistema está basado en un **árbol de progresión tipo Path of Exile**, con rutas ramificadas y desbloqueo secuencia
9. Formacion: son las distintas formaciones qué puede adopter UN squad por indication directa del heroe, dependiendo de la squad tiene Unas formaciones u otras
    - Linea
    - Testudo
    - Dispersa
    - Cuña
    - Schiltron
    - Muro de escudos
10. Orden: ordenes qué Les puede dar el heroes a las squads.
    - Seguir: la squad siguen Al heroes defendiendole
    - Mantener posicion: la squad SE mantiene el la ubicacion donde lo coloque el heroe, manteniendo la formacion qué Tenga activa de momento
    - Atacar: el squad ataca a Los enemigos q Tengan dentro de rango
11. Equipamiento: piezas de armadura de unidad, qué SE pierden cuando mueren unidades Durante partida, Una unidad si tiene menos de 50% de equipamiento entra con debufffs y si tienes 0% no puede ser no puede Entrar a partida
12. Loadout: Los jugadores pueden tener Unas squads preseleccionadas fuera de partida qué SE pueden elegir antes de Entrar a partida Como conjunto
    - Viene limitada por la cantidad de liderazgo q tiene el heroes, es decir si el heroe tiene 3 de liderazgo y hay 4 squads, arqueros 1 de liderazgo, piqueros 2 de liderazgo, lanceros 1 de liderazgo y escuderos 2 de liderazgo, en el loadout no puede guardar arqueros(1), piqueros(2) y lanceros(1) por qué la Suma seria 4 y el maximo para el son 3, SE puede menos o igual pero nunca mas
13. Liderazgo: es lo que limita la cantidad de squads q puede llevar UN heroe a batalla  y la cantidad de squads q entran en UN loadout, el heroe tiene UN Valor y Los squads tienen UN coste de liderazgo
14. estamina: es lo que los heroes utilizan para realizar ataques sprintar y lanzar habilidades
15. ////batalla 
16. Batalla: Punto central del juego aqui es donde Los jugadores luchan en 2 bandos(atacantes y defensores) capturando banderas para ganar(atacantes) o defendiendolas hasta que acabe el tiempo(defensores) para ganar
17. Supply point: Este es UN elemento presente en la batalla donde si Tu bando ya lo capturo puede curar pasivamente si esta el heroes o su squad esta dentro de rango de action, o puede cambiar su tropas.activa de entre las que trajo a batalla interactuando con el avatar del suppli point. Si Un heroe entra en el rango de action de UN suppli point que no pretence a SU bando y no hay ninguna heroe del bando owner de ese supply point empieza en tiempo de capturas, si Durante la captura entra UN heroe del bando owner SE cancela la captura y SE reinicia el contador. Pueden existir supply point de 3 tipos segun la perspective de jugador
    - Aliadas: pertenece Al bando del jugador. aparecen de color Azul en el minimapa, el borde de su rango de action SE muestra de Este colour y el avatar del suppli tambien tiene Los detalles de Este colour, en Este el jugador puede interactuar con el supply point y el y su squad SE curan pasivamente con estar dentro del radio de accion
    - Enemigo: pertenece Al bando contrario Al jugador. Aparece el con Todos sus detalles en rojo, el usuario puede capturarlo mas no interactuar con el, ni SE curan ni el ni au squad
    - Neutral: no pertenece a nadie aun. SE muestra de color gris, cualquier bando puede capturarlo, nadie puede interactuar con el ni curarce
18. Punto de captura: son las banderas qué qué tienen q captura Los atacantes para ganar o defenderlas hasta q SE arcane el tiempo Los defensores para ganar la partida. Existen de dos tipos las normales y las del base. Cada Bandera tiene un rango de action, es mas grande en las banderas del base
    - Normales: SE identifican con Una letra, A, B, C, etc… y Al conquistarlas aumentan UN Poco el tiempo de partida para darle mas tiempo a Los atacantes, es decir si quedaban 10 min en el timer Justo cuando SE completa la conquista de Una Bandera normal, SE le agregarian 5 min(por ejemplo) y el timer quedaria 15min
    - De base: Al conquistar esta Bandera SE termina la partida y gana el bando de Los atacantes, para desbloquea esta Bandera y sea capturable es necesario conquistar las banderas normales
19. Captura de banderas: la captura de banderas funciona de la siguiente Manera
    - Tiene qué Entrar Al menos uN heroe del bando contrario para q Inicie la Barra de captura
    - No puede Haber ninguna heroe del bando owner dentro del rango de action de la Bandera
    - Si UN heroe del bando owner entra dentro del rango de action de la Bandera mientras SE esta conquistando, SE interrumpe la Barra de captura hasta q no haya ninguna heroe del bando owner dentro de dicho rango
    - El avance en la Barra de captura no SE pierde Al set interrumpida, continua desde el mismo Punto donde quedo
    - cuando un heroe esta dentro del rango de la bandera que se esta capturando va ganado puntos de captura que se muestan al final de la partida en el post batalla
20. Maquinaria de asedio: elemetos Como Torres de asedio y arietes qué SE usan para asediar en Una batalla, solo el bando atacante tiene acceso a ellas y Debe interactuar con ellos para qué su unidad Los empuje de su Punto de partida a SU Punto de contacto
    - Torres de asedio: para tener acceso a lo alto de las murallas
    - Ariete: para romper puertas
21. Bandos: conjunto de jugadores qué juegan juntos para ganar Una partida o batalla existen 2 bandos atacantes y defensores
22. Spawn points: puntos de aparcion para los heroes con sus squads al empezar la partida
23. //// Combate
24. Daños y defensa: existe 3 tipos de daño, contundente(blunt), perforante(piercing), y cortante(slashing) cada squad y heroes poseen estos 3 tipos de daños en diferentes proporciones, y existe Una contraparte defensive de cada Uno(defensa contundente, perforante y cortante) q SE toman en cuenta a la hora De la asignacion de daño
25. Penetracion de armadura: la penetracion de armadura es otro Valor q SE Toma en cuenta a hora De asignacion de daño, existen Una por cada Tipo de daño(contundente, perforante y cortante) y el Valor de cada Una SE Salta la defensa de ese Tipo y ese daño directo a la Vida, por ejemplo si voy a hacer 1000 puntos de daño cortante y el enemigos tienen 450 de defensa cortante entran 550, Pero si el qué hace daño tiene 100 de penetracion de armadura cortante entran 1000dñC - (450dfC -100paC) qué es igual a 650dñC siendo dñC(daño Cortante), dfC(defensa Cortante) y paC( penetracion de armadura cortante)
26. //// Barracon
27. barracon: este es el espacio donde el jugador puede consultar, desbloquear, formar o desvandar Squads fuera de partida
    - al inicio del juego el usuario no tiene acceso a todas las squads, las va desbloqueando en este apartado
    - Formar: crear un squad del tipo de squad desbloqueado, las squads formadas son las que van ganando exp en partidas y pueden desbloquear cosas en consecuencia
    - Desvandar: destruye una Squad formada con aterioridad perdiendo progreso nivel y demas cosas asociadas a ella

## Flujo o journey de un jugador

vamos a aondar un poco mas en la mecanicas de los mapas de feudo para el MVP,

las batallas de feudo (vamos a llamarlas solo "Batallas" de ahora en adelante  porq son las unicas que hay en este MVP) son el core del juego.
vamos a defenir un flujo de actividad del usuario dentro del juego desde que abre el juego:

1. al iniciar se le muestra una interfaz para llenar los datos de "usuario" y "password"
2. una vez introducidos estos datos y son correctos, pasa a la seleccion de personajes
3. aqui en la interfaz de seleccion de personajes, el usuario puede:
3.1) crear personajes
3.2) elegir entre los personajes que tenga creados
3.3) al elegir un personaje se visualiza el personaje en 3d con las armaduras que tenga activas y las skins
3.4) y una vez elige un personaje se le ativa un boton en la interfaz que dicen "entrar" y lo presiona
4. una vez presionado el boton entrar y con el personaje valido escogido, aparece la pantalla de carga, mientras se carga el unico feudo o ciudad que tendremos donde aparecera e interactuaran todos los jugadores (el feudo)
5. al terminar de cargar el mapa el usuario aparece en el  feudo
6. dentro de la interfaz general del usuario cuando esta en el feudo aparece un boton en parte superior derecha, con la forma de una miniatura de un castillo, que al presionarlo lo mete en la cola de espera para entrar en "Batalla"
7. el juego por detras recibe todas las solicitudes de batalla y los junta de forma aleatoria hasta llenar un Lobby automatico de batalla(ya sea 3vs3, 5vs5 o 15vs15), tomaremos en este ejemplo que es 3vs3
8. una vez lleno el lobby, el juego saca a los jugadores 6 jugadores que conforman el lobby y los coloca de forma aleatoria en los bandos(3 atacantes y 3 defensores y les muestra a pantalla completa la interfaz de la fase de preparacion de la batalla
9. en esta interfaz de la fase de preparacion de batalla, el jugador puede:
9.1) elegir las Squads de entre las que tiene disponibles sin superar su limite de liderazgo
9.2) elegir el punto de respawn donde quiere parecer de entre los que tiene disponible su bando
9.2.1) en la interfaz se muestra un mini mapa del mapa de batalla y los puntos de spawn se muestran en este mini mapa con una miniatura interactuable que al usuario clickar en ellas se selecciona y cambia de color
9.3) elegir entre los loadouts de tropas que tiene
9.4) y presionar continuar y se le bloquea la seleccion hasta que el resto de jugadores presionen continuar o se acababe el tiempo
9.5) si se acaba el tiempo y el jugador no ha dado continuar pasa automaticamente con lo que tenga elegido
9.6) pasan a la pantalla de carga mientras se carga el mapa de batalla y se cargan todos los heroes y las tropas en el mismo
9.7) una vez cargado todo se muestra el personaje y se desarrolla la batalla
10. una vez terminada la batalla y se haya determinado ganador, se pasa a una interfaz de pantalla completa con el resumen de la batalla(cuantas unidades mato el jugador con cada unidad, cuales Squads utilizo y cuanto mato con cada una, ademas la exp que gano el heroe y cada unidad, y en esta interfaz un boton continuar
10.1) al presionar continuar, lo saca de la vista de resumen de batalla y lo lleva al mapa del feudo

## Scenes(por no encontrar una mejor manera de llamarlas)

espacios donde estara el jugador o pantallas diferenciadas o stages donde el jugador interactuara con la interfaz

- login
- seleccion de personaje:
    - selecciona de entre los avatares que tenga creados, se identifican por nombre
- creacion de personaje:
    - solo se podra elegir entre 3 avatares pre creados, pero podra crear un nombre para identificarlo
- pantallas de carga
- feudo: espacio central donde coexistiran los jugadores mientras no esten en partida
    - en este espacio o Scene el jugador podra ver su avatar y moverse con el por el feudo pero no podra combatir, lanzar habiliades, usar perks, o desplegar Squads
- preparacion de batalla:
    - aqui es donde el usuario elige los squads que puede llevar a la partida siempre limitado por su atributo de liderazgo, ademas puede elegir uno de sus 3 loadout que aparecen como botones en la parte inferior de este panel y se llenara automaticamente los slots de la parte superior mostrando las unidades que conforman ese loadout
    - se ven en el panel lateral izquierdo todos los jugadores que forman parte del equipo del jugador
    - en la panel inferior central  esta UI de seleccion de squads
    - en la parte superior el timer para empezar partida(3 miin)
    - en la parte central y derecha un mapa del feudo de la batalla, en ella se muestran los Spawn points para que los seleccione el heroe y la ubicacion de los puntos de captura y supply points
    - en la parte inferior derecha esta el panel donde el usuario puede ver y cambiar los slots de armaduras y arma que llevara a batalla
- batalla
    - aqui es donde se desarrolla todo el combate
    - los jugadores ven sus avatar y su squad activo, pueden dar ordenes a sus squads, cambiar formaciones, luchar contra unidades enemigas, conquistar supply points y puntos de captura y demas
    - en la parte inferior izquierda de la hud esta la ui de tropa que muestra, imagen de unidad, posibles ordenes, posibles formaciones, cantidad de unidades vivas/total
    - en la parte central inferior, icono de clase de heroe, barra de vida y panel de habilidades que puede utilizar
    - cuando se elimina a una unidad enemiga aparace por unos segundos el contador de unidades enemigas eliminadas, pegado al derecho de la pantalla centrado verticalmente
    - cuando una unidad asiga daño sale un popup damage que muestra el valor del daño que hizo y desaparece en menos de un segundo
    - si la bandera de base esta disponible para conquistar se muestra el icono(rombo con el color ya sea aliada o enemiga con el icono del tipo de badera en el centro) con la barra de conquista circular a su alrededor con su progreso
    - el timer de batalla pegado a la parte superior, centrada horizontalmente
    - en la parte inferior derecha, pegado al borde se muesta el minimapa de la batalla donde se muestran los puntos de captura y supply points y su estado actual, mas la ubicacion de todos los heroes aliados
- post batalla
    - es una pantalla donde se muestra el resumen de batalla, que Squads utilizo el heroe, que unidades mato con cada squad, exp que gano con cada una, y si se gano o se perdio la partida, se divide en 3 pestañas
        - pestaña de overall, muestra daño total, daño total recibido, contador de unidades matadas general, experiecia de heroe y de Squad ganado, y distintos elementos ganados en la partida
        - pestaña de Squads: muestra las Squads utilizadas, cuantas unidades mato cada una cuanta exp gano cada una
        - pestaña de equipo: muestra 2 tablas, una a la izquierda con todos los jugadores aliados, cuanto mato cada heroe, su puntaje, si fue MVP, puntos de captura y en la parte derecha muestra

## 📌 Nombre tentativo: Conquest Tactics

---

## 1. 🌟 Resumen Ejecutivo (versión ajustada)

**Nombre tentativo:** *Feudos: Guerra de Escuadras*

**Género:** Acción táctica multijugador con control de escuadras (PvP 3v3 – MVP)

**Plataforma objetivo:** PC

**Duración estimada por partida:** 5 a 10 minutos

**Estilo visual:** Realismo medieval táctico (inspiración en la Europa feudal)

---

### 🎯 Visión del Juego

*Feudos* es un juego competitivo multijugador donde los jugadores controlan un **héroe comandante** que lidera una **escuadra de soldados** en el campo de batalla. La victoria no depende del combate individual, sino de la **coordinación estratégica entre héroes y escuadras aliadas**.

El jugador actúa como un **líder de tropas**, dando órdenes, controlando formaciones y activando habilidades para asegurar puntos estratégicos. En este mundo, **un héroe solo no es suficiente para ganar una batalla**, pero un equipo bien coordinado puede romper cualquier línea enemiga.

---

### 🧱 Pilares del Diseño

1. **Táctica sobre acción:** la estrategia grupal prevalece sobre el desempeño individual.
2. **Coordinación entre jugadores:** cada héroe aporta su escuadra; juntos forman un ejército eficaz.
3. **Roles definidos por arma y escuadra:** el héroe es solo una pieza más en la sinfonía de guerra.
4. **Despliegue y control de escuadras:** cada jugador gestiona una escuadra activa en tiempo real.
5. **Diversidad de formaciones y órdenes:** decisiones tácticas cambian el curso del combate.

### 🧱 Pilares del Juego

- Control simultáneo de héroe + escuadra
- Sistema de órdenes tácticas y formaciones
- Progresión persistente del héroe y escuadras
- Batallas estratégicas con objetivos dinámicos
- Combate cuerpo a cuerpo inmersivo

---

### 🧍‍♂️ Conceptos Clave Redefinidos

- **Héroe:** es el avatar del jugador, un comandante personalizable. No es un “guerrero overpower”, sino el eje táctico que dirige su escuadra.
- **Clase del héroe:** se define por el arma equipada (espada + escudo, lanza, arco, etc.). Afecta habilidades activas y estilo de liderazgo.
- **Escuadra (Squad):** grupo de soldados homogéneos (piqueros, arqueros, etc.) que actúan bajo las órdenes del héroe. El jugador controla solo una escuadra a la vez.
- **Unidad:** cada soldado individual dentro de una escuadra. Tienen estadísticas, equipo, habilidades propias y progresan junto con su escuadra.
- **Formaciones:** posiciones tácticas que las escuadras pueden adoptar (línea, cuña, testudo, etc.). Cambian comportamiento y efectividad.
- **Órdenes:** comandos básicos dados por el héroe (seguir, mantener posición, atacar) para controlar a la escuadra.
- **Perks:** sistema de talentos que mejora el desempeño del héroe y/o su escuadra según el estilo de juego del jugador.
- **Liderazgo:** atributo clave que limita cuántas escuadras se pueden traer a la batalla (mediante loadouts).
- **Equipamiento:** armaduras y armas de los héroes y sus escuadras. Afecta estadísticas, se pierde en combate y se debe mantener.
- **Loadout:** preconfiguración de escuadras válidas que el jugador selecciona antes de entrar en batalla.

---

### ⚔️ Filosofía de Combate

- El **protagonista no es el héroe, sino el conjunto de escuadras y su sinergia**.
- Un héroe sin su escuadra es vulnerable y poco efectivo en combate directo.
- Las batallas se ganan por **uso táctico del terreno, formaciones, habilidades grupales** y control de puntos estratégicos.
- No hay lugar para “jugadas heroicas individuales” sin soporte o planificación.

---

## 2. 🔁 Core Gameplay Loop (Versión Reescrita)

### 🎯 Objetivo del Loop

Crear un flujo centrado en la **preparación táctica, liderazgo en combate y colaboración entre jugadores**, minimizando el peso del 1vX individual y maximizando la experiencia de **batalla estratégica por escuadras**.

---

### 🔄 Ciclo Principal del Jugador

```
1. Ingresar al juego
2. Seleccionar y personalizar un héroe (avatar comandante)
3. Formar y administrar escuadras en el barracón
4. Preparar un loadout válido según liderazgo
5. Entrar en cola de batalla (Quick Join)
6. Fase de preparación táctica: elegir escuadras y punto de despliegue
7. Desarrollar batalla por escuadras:
     - Dar órdenes a escuadra activa
     - Activar habilidades tácticas
     - Coordinar con aliados para capturar y defender puntos clave
8. Finaliza batalla ➜ resumen de rendimiento
9. Obtener recompensas, experiencia y recursos
10. Volver al feudo ➜ progresar, ajustar estrategia y repetir
```

---

### 📌 Notas de Diseño Importantes

- **El foco está en el “héroe como líder”, no como asesino.**
- Cada decisión antes y durante la batalla influye en la **efectividad del escuadrón activo**.
- El combate directo del héroe **debe tener consecuencias claras**: atacar sin escuadra = riesgo real de ser eliminado.
- **La escuadra es tu herramienta principal** para ganar terreno, proteger objetivos y generar impacto real.

---

### 🧠 Recompensa del Loop

- Satisfacción por decisiones tácticas acertadas.
- Progresión persistente de héroe y escuadras.
- Colaboración con otros jugadores para superar desafíos grupales.
- Profundidad y rejugabilidad al optimizar combinaciones de escuadras, perks y formaciones.

## 3. 🧍 Mecánicas del Jugador (Versión Reescrita)

### 🎮 Control del Héroe

- Vista en **tercera persona**, con control directo tipo RPG.
- El héroe funciona como **comandante de escuadra**, no como unidad de choque principal.
- No puede **sobrevivir solo contra escuadras enemigas completas** sin apoyo táctico y posicionamiento inteligente.

---

### ⚔️ Combate del Héroe

- **Ataques básicos** (según clase/arma equipada).
- **3 habilidades activas** + **1 ultimate**.
- El daño del héroe es **complementario**, útil para asistir o rematar, **no para borrar unidades completas**.

### 🔋 Recurso clave: Estamina

- Se consume al:
    - Atacar
    - Esquivar o correr
    - Activar habilidades
- **Estamina baja = héroe expuesto** ➜ fomenta el uso táctico, no el spam.

---

### 🪖 Interacción con la Escuadra

- El héroe siempre tiene una **escuadra activa**, la cual puede:
    - Recibir órdenes directas
    - Cambiar de formación
    - Activar habilidades propias
- **Sin escuadra activa, el héroe está en clara desventaja.**

### 🔧 Órdenes básicas (Hotkeys por defecto)

| Orden | Tecla | Efecto |
| --- | --- | --- |
| **Seguir** | `C` | La escuadra sigue al héroe, protegiéndolo |
| **Mantener posición** | `X` | Se queda defendiendo un punto |
| **Atacar** | `V` | Ataca automáticamente a enemigos en rango |

---

### 🧠 Gestión táctica en tiempo real

- Cambiar escuadra activa (en supply points).
- Activar habilidades de escuadra desde la interfaz de HUD.
- Cambiar formación con teclas rápidas (`F1` a `F3`).
- Posicionar escuadra aprovechando terreno, cobertura y línea de visión.

---

### ⚠️ Restricciones de poder individual

- El héroe **no puede limpiar escuadras solo**.
- Su valor está en **coordinar, sobrevivir y posicionar correctamente a su escuadra**.
- Ser eliminado como héroe tiene un castigo: **pierde control táctico durante el respawn**.

---

### 🧩 Integración con Perks

- Algunos perks afectan al **héroe directamente** (movilidad, defensa, habilidades).
- Otros mejoran el **rendimiento de escuadras** (moral, velocidad, bonus situacionales).
- El sistema fomenta **sinergias específicas** entre clase de héroe y tipo de escuadra.

---

### 🎯 Rol del Jugador

El jugador es un **comandante táctico con presencia física en el campo**, que:

1. **Protege** su escuadra con posicionamiento y liderazgo.
2. **Dirige** ofensivas con formaciones y habilidades sincronizadas.
3. **Se retira o cambia de escuadra** cuando la situación lo exige.
4. **Colabora activamente** con sus aliados para tomar decisiones conjuntas en el frente.

---

## 4. 🪖 Unidades y Escuadras (Squads)

- **Composición:** solo un tipo de unidad por escuadra
- **Tipos:** arqueros, lanceros, escuderos, piqueros
- **IA:** comportamiento automático, con orden siempre activa
- **Habilidades:** activadas manualmente por el jugador
- **Progresión:** hasta nivel 30, desbloquean habilidades y formaciones
- **Equipamiento:**
    - Pierden equipamiento al morir
    - <50% ➜ debuffs
    - 0% ➜ no desplegable
- **Colisión:** solo contra enemigos

### 🧱 4.1 Concepto General

- Una **Escuadra (Squad)** es un grupo homogéneo de unidades **controladas tácticamente por el jugador a través del héroe**.
- Solo **una escuadra puede estar activa al mismo tiempo** por héroe.
- Las escuadras representan el **verdadero poder de combate** del jugador: sin ellas, el héroe está en seria desventaja.

### 🛠️ 4.2 Composición

- Cada escuadra está compuesta por **un solo tipo de unidad** (ej. todos arqueros, todos piqueros, etc.).
- Su tamaño varía según el tipo (más unidades si son ligeras, menos si son pesadas).
- Las unidades comparten:
    - Nivel
    - Tipo de armadura
    - Estadísticas
    - Armas
    - Comportamiento
    
    ---
    

### 🧠 4.3 Inteligencia Artificial

- Las unidades **actúan automáticamente** tras recibir una orden.
- No atacan por iniciativa propia sin dirección.
- Tienen un **ángulo de visión** limitado: enemigos fuera de visión no son detetados.
- Siempre mantienen **formación activa** salvo que estén desorganizadas o en combate caótico.
- Prioridad de ataque: objetivo más cercano.

---

### 🧭 4.4 Sistema de Comando

El héroe puede dar las siguientes órdenes tácticas:

| Orden | Efecto |
| --- | --- |
| **Seguir** | La escuadra protege al héroe mientras se mueve. |
| **Mantener posición** | La escuadra defiende el punto actual. |
| **Atacar** | Atacan al objetivo más cercano dentro de rango. |
| **Retirada táctica** (futura) | Retroceden a una posición segura. |

> Las órdenes se dan en tiempo real con hotkeys configurables.
> 

**Hotkeys sugeridas (MVP):**

- C = Seguir
- X = Mantener posición
- V = Atacar

---

### 🧰 4.5 Formaciones

Las escuadras pueden entrar en formaciones específicas según su tipo. Las formaciones son **herramientas tácticas críticas**, no solo visuales.

| Formación | Función táctica |
| --- | --- |
| **Línea** | Defensa básica, contacto directo. |
| **Testudo** | Protección contra proyectiles. |
| **Dispersa** | Minimiza daño de área. |
| **Cuña** | Penetra líneas enemigas. |
| **Schiltron** | Anticarga en defensa circular. |
| **Muro de Escudos** | Sólida contención frontal. |

> Cambiar de formación toma tiempo y puede dejar a la escuadra vulnerable.
> 

Cada escuadra tiene acceso a **formaciones** diferentes según su tipo. Las formaciones afectan:

- Dirección del enfrentamiento
- Comportamiento defensivo/ofensivo
- Tamaño de colisión
- Buffs situacionales

---

### 🧩 4.6 Habilidades de Escuadra

- Activadas manualmente por el jugador desde el HUD.
- Cada escuadra puede tener entre **1 y 2 habilidades únicas**, según su tipo.
- Tipos de habilidades:
    - **Ofensivas:** bonus de daño, cargas sincronizadas.
    - **Defensivas:** bloqueos reforzados, posicionamiento.
    - **Tácticas:** alteración de moral, resistencia a efectos, velocidad.

---

### 📈 4.7 Progresión de Escuadra

- Ganan **experiencia propia** al combatir.
- Suben hasta **nivel 30 (en el MVP)**.
- Progresar otorga:
    - Mejora de atributos base
    - Nuevas formaciones
    - Habilidades de escuadra
    - Mejor equipamiento

> El progreso de cada escuadra es independiente del héroe.
> 

---

### 🛡️ 4.8 Equipamiento de Escuadra

- Cada unidad de la escuadra **tiene armadura propia**.
- Al morir pierden partes de ese equipamiento.
- Condiciones:
    - **>50% equipamiento**: sin penalización
    - **<50%**: entran a batalla con debuffs
    - **0%**: no pueden desplegarse

---

### 📦 4.9 Barracón y Administración

Desde el **Barracón**, los jugadores pueden:

- **Formar escuadras** nuevas (si ya las desbloquearon).
- **Ver experiencia y equipamiento** de cada escuadra.
- **Desvandar** escuadras que ya no quieran (se pierde todo progreso).
- **Organizar loadouts tácticos** de escuadras según su liderazgo disponible.

---

### 🎯 4.10 Liderazgo y Loadouts

- Cada escuadra tiene un **costo de liderazgo**.
- El héroe tiene un límite total de liderazgo según su progreso.
- Los jugadores pueden preparar **loadouts personalizados** para cada batalla, **sin exceder el liderazgo máximo del héroe**.

---

### 🧬 4.11 Sinergia con el Héroe

- Algunas clases se benefician de ciertas escuadras:
    - Arco + Arqueros ➜ fuego coordinado desde retaguardia
    - Espada y Escudo + Escuderos ➜ muralla defensiva móvil
    - Lanza + Piqueros ➜ control total de zona
- Otras combinaciones son posibles, pero la efectividad **depende del uso táctico**, no del poder bruto.

---

### 📊 4.12 Atributos y Estadísticas de Unidad (MVP)

Cada unidad dentro de una escuadra posee un conjunto de atributos que determinan su rendimiento en batalla. Estos se ven afectados por el tipo de unidad, su armadura, nivel, habilidades desbloqueadas y perks aplicados por el héroe.

### 📋 Atributos Básicos

| Atributo | Descripción |
| --- | --- |
| **Vida** | Salud base de la unidad. Aumenta con el nivel. |
| **Defensas** | Reducción de daño por tipo: **Cortante**, **Perforante**, **Contundente**. |
| **Daño** | Se separa por tipo: Cortante, Perforante o Contundente, según el arma. |
| **Penetración** | Cantidad de defensa que se ignora del enemigo según tipo de daño. |
| **Velocidad** | Afecta movimiento, respuesta a órdenes y capacidad de reposicionamiento. |
| **Masa** | Determina su capacidad para resistir empujes o romper líneas enemigas. |
| **Peso** | Categoría general de carga (ligero, medio, pesado). Influye en velocidad. |
| **Bloqueo** | Capacidad de bloquear ataques frontales (solo si usa escudo). |
| **Liderazgo** | Coste que esa unidad impone al límite de liderazgo del héroe. |

### 🎯 Atributos Exclusivos de Unidades a Distancia

| Atributo | Descripción |
| --- | --- |
| **Alcance** | Máxima distancia efectiva de ataque. |
| **Precisión** | Porcentaje base de acierto. Afectado por movimiento, distancia y perks. |
| **Cadencia de fuego** | Ritmo de disparo (ej.: 1 disparo cada 1.5 segundos). |
| **Velocidad de recarga** | Tiempo para reponer munición tras agotar un ciclo de disparos. |
| **Munición** | Carga total de proyectiles disponibles por batalla. |

---

> ⚠️ Importante (MVP):
> 
> - Estos atributos **no se modifican directamente** por el jugador.
> - Se ven influenciados por: **nivel de la escuadra**, **formación activa**, **perks del héroe**, y **habilidades de unidad**.
> - **No hay moral** ni efectos derivados de esta en el MVP.

---

### 4.13 🧾 Fichas de Squads (MVP)

### 🛡️ Escuderos

**Descripción**
Unidad defensiva diseñada para sostener la línea de batalla. Su alta masa y escudos pesados los hacen ideales para contener avances enemigos y proteger zonas clave del mapa.

**Comportamiento**

- Mantienen posición firme en formación.
- Efectivos bloqueando ataques frontales.
- Vulnerables a flanqueos o unidades con alta penetración.

| Atributo | Valor Base (Nivel 1) | Notas |
| --- | --- | --- |
| **Tipo** | Cuerpo a cuerpo (defensiva) | Línea de contención |
| **Arma** | Espada corta + escudo pesado | Alta defensa frontal |
| **Vida** | 120 | Resistencia sólida |
| **Defensas** | C: 20 / P: 15 / T: 25 | Buen contra contundente y cortante |
| **Daño (tipo)** | Cortante: 14 | Corto alcance, daño moderado |
| **Penetración** | Cortante: 3 | Baja |
| **Alcance** | 1.5m | Rango de espada |
| **Velocidad** | 2.5 | Lentos pero estables |
| **Bloqueo** | 40 | Excelente protección frontal |
| **Peso** | 6 | Pesados |
| **Masa** | 8 | Difíciles de empujar |
| **Liderazgo** | 2 | Costo medio |

**Habilidades de Escuadra**

| Nivel | Nombre | Tipo | Descripción |
| --- | --- | --- | --- |
| 1 | Bloqueo Coordinado | Activa | +30% al bloqueo durante 6 segundos. |
| 10 | Rompe Avance | Activa | Golpe con escudo que empuja enemigos. |
| 20 | Tenacidad Blindada | Pasiva | +15% a defensas si no se mueven. |
| 30 | Muro Inamovible | Activa | Ignoran retroceso y mantienen formación por 5s. |

**Formaciones disponibles**

- Línea
- Muro de Escudos
- Testudo

---

### 🏹 Arqueros

**Descripción**
Unidad de hostigamiento a distancia. Especializados en atacar desde lejos, son frágiles pero muy efectivos si se posicionan adecuadamente detrás de líneas aliadas.

**Comportamiento**

- Disparan automáticamente a enemigos en rango.
- Reaccionan a órdenes del héroe, no actúan por sí mismos.
- Extremadamente vulnerables a cuerpo a cuerpo o cargas.

| Atributo | Valor Base (Nivel 1) | Notas |
| --- | --- | --- |
| **Tipo** | Apoyo a distancia | Flanqueo o presión |
| **Arma** | Arco largo | Sin escudo |
| **Vida** | 80 | Muy frágiles |
| **Defensas** | C: 5 / P: 8 / T: 5 | Vulnerables |
| **Daño (tipo)** | Perforante: 22 | Daño directo |
| **Penetración** | Perforante: 6 | Eficaz contra unidades ligeras |
| **Alcance** | 25m | Muy largo |
| **Velocidad** | 3.2 | Rápidos |
| **Bloqueo** | 0 | No bloquean |
| **Peso** | 2 | Livianos |
| **Masa** | 2 | Fácil de empujar |
| **Liderazgo** | 1 | Bajo coste |
| **Precisión** | 70% | Se reduce con distancia o movimiento enemigo |
| **Cadencia** | 1.5s | Estándar |
| **Velocidad de recarga** | 2s | Al agotar ciclo |
| **Munición** | 20 | Limitada |

**Habilidades de Escuadra**

| Nivel | Nombre | Tipo | Descripción |
| --- | --- | --- | --- |
| 1 | Descarga Coordinada | Activa | Disparo sincronizado con +25% daño. |
| 10 | Puntería Estática | Pasiva | +15% precisión si no se mueven por 3s. |
| 20 | Flechas Empaladoras | Activa | +50% penetración por 5s. |
| 30 | Emboscada Silenciosa | Pasiva | +10% daño los primeros 5s si no han sido detectados. |

**Formaciones disponibles**

- Línea
- Dispersa

---

### 🪓 Piqueros

**Descripción**
Unidad de control de área y defensa contra cargas. Su largo alcance les permite mantener a raya a enemigos cuerpo a cuerpo antes de que lleguen a contacto.

**Comportamiento**

- Ideales para aguantar cargas.
- Su mejor rendimiento es en estático.
- Vulnerables si pierden formación o son rodeados.

| Atributo | Valor Base (Nivel 1) | Notas |
| --- | --- | --- |
| **Tipo** | Cuerpo a cuerpo (control de área) | Anticarga |
| **Arma** | Pica larga | Sin escudo |
| **Vida** | 100 | Moderada |
| **Defensas** | C: 12 / P: 18 / T: 10 | Balance defensivo |
| **Daño (tipo)** | Perforante: 16 | Buen daño inicial |
| **Penetración** | Perforante: 5 | Contra unidades ligeras |
| **Alcance** | 3.5m | Rango extendido |
| **Velocidad** | 2.8 | Lentos |
| **Bloqueo** | 0 | Sin defensa directa |
| **Peso** | 5 | Medios |
| **Masa** | 6 | Resistencia aceptable |
| **Liderazgo** | 2 | Coste medio-alto |

**Habilidades de Escuadra**

| Nivel | Nombre | Tipo | Descripción |
| --- | --- | --- | --- |
| 1 | Punta Firme | Pasiva | +10% daño y +5% penetración en formación. |
| 10 | Círculo Defensivo | Activa | Formación Schiltron inmune a cargas 6s. |
| 20 | Emboscada de Acero | Activa | +30% daño si el enemigo viene corriendo. |
| 30 | Disuasión Implacable | Pasiva | Enemigos que golpean a esta escuadra pierden 10% velocidad por 3s. |

**Formaciones disponibles**

- Línea
- Schiltron
- Cuña

---

### 🛡️ Lanceros

**Descripción**
Unidad versátil con lanza y escudo, adaptables tanto en ataque como defensa. Buenos en avance táctico y resistencia en combate prolongado.

**Comportamiento**

- Mantienen formación al moverse.
- Resisten bien embestidas ligeras.
- Frágiles si pierden cohesión o son superados en masa.

| Atributo | Valor Base (Nivel 1) | Notas |
| --- | --- | --- |
| **Tipo** | Cuerpo a cuerpo (versátil) | Antiflanco |
| **Arma** | Lanza corta + escudo | Equilibrados |
| **Vida** | 110 | Alta |
| **Defensas** | C: 15 / P: 12 / T: 14 | Buenas resistencias mixtas |
| **Daño (tipo)** | Perforante: 14 / Cortante: 6 | Dual |
| **Penetración** | Perforante: 4 | Media |
| **Alcance** | 2.2m | Correcto |
| **Velocidad** | 3.0 | Buena movilidad |
| **Bloqueo** | 25 | Defensa frontal útil |
| **Peso** | 4 | Medio |
| **Masa** | 4 | Equilibrados |
| **Liderazgo** | 1 | Muy rentables |

**Habilidades de Escuadra**

| Nivel | Nombre | Tipo | Descripción |
| --- | --- | --- | --- |
| 1 | Carga Escudada | Activa | +15% masa y +10% bloqueo por 5s. |
| 10 | Contraataque Dirigido | Pasiva | +10% daño a enemigos que hayan bloqueado sus golpes. |
| 20 | Avance Disciplinado | Activa | Mantienen formación en movimiento. |
| 30 | Muralla Viviente | Pasiva | +10 defensa en modo “mantener posición”. |

**Formaciones disponibles**

- Línea
- Muro de Escudos
- Cuña

## 5. 🧝 Héroes y Personalización

El héroe es el eje de la experiencia táctica del jugador, pero no está diseñado para ser una fuerza dominante individual. En este sistema, el jugador lidera, coordina y potencia a sus escuadras, y su efectividad depende en gran medida del uso estratégico del entorno, habilidades y formaciones, no del combate uno contra uno.

Un héroe sin apoyo de sus tropas debe tener dificultades reales para sobrevivir en combate directo. Esta filosofía diferencia el juego de otros títulos centrados en héroes individualistas.

---

### 5.1 Clases y Equipamiento

### 🔀 Clases según arma equipada:

- Espada + escudo
- Espada a dos manos
- Lanza
- Arco

> Cada arma define la clase del héroe y otorga un set único de habilidades activas + 1 habilidad ultimate.
> 

### 🛡️ Armadura equipada

- 4 piezas: casco, guantes, pechera y pantalones
- Tipos: ligera / media / pesada
- Define la defensa base y la penalización o bonificación a velocidad / estamina

### 🎨 Skins

- 100% cosméticos, para héroe y tropas
- No modifican estadísticas

### 💪 Estamina

- Usada para habilidades, correr y esquivar
- Se regenera con el tiempo y fuera de combate

---

### 5.2 Atributos

### 🧱 **Atributos Modificables**

| Atributo | Descripción breve | Impacta en… |
| --- | --- | --- |
| **Fuerza** | Representa potencia física y brutalidad | Daño cortante y contundente |
| **Destreza** | Precisión, velocidad de ataque y agilidad táctica | Daño perforante, velocidad de acciones |
| **Armadura** | Capacidad de absorción de daño | Mitigación general de daño |
| **Vitalidad** | Resistencia física, salud general del héroe | Vida total |

---

### 📐 **Atributos Derivados**

Estos no se modifican directamente, sino que se calculan a partir de atributos base y equipo:

### ⚔️ Daño por tipo

- **Contundente** = base + `2 × Fuerza`
- **Cortante** = base + `1 × Fuerza` + `1 × Destreza`
- **Perforante** = base + `2 × Destreza`

### 🛡️ Penetración y defensa por tipo

- **Penetración**: se determina por el arma equipada
- **Defensa**: se determina por piezas de armadura + perks activos

### ❤️ Vida

- `Vida total` = base por clase + `1 × Vitalidad`

### 🛡️ Mitigación de daño

- `Mitigación` = armadura base de equipo + bonificadores pasivos + `1 × Atributo de Armadura`

### 🪖 Capacidad de unidades (liderazgo)

- Valor base por clase o nivel
- Aumentable por perks y bonificaciones de equipo

### 🧠 Influencia táctica

- No se escala directamente
- Se modifica por perks o habilidades que mejoran el rendimiento de las escuadras aliadas cercanas

---

### 🧪 Ejemplo de Aplicación

**Ejemplo 1 – Héroe con 10 Fuerza, 5 Destreza:**

- Daño contundente = base + 20
- Daño cortante = base + 15
- Daño perforante = base + 10

**Ejemplo 2 – Héroe con 8 Armadura, equipo de 60 defensa:**

- Mitigación = 60 + 8 = 68 defensa aplicada al cálculo de reducción de daño.

---

### ⚠️ Notas de diseño

- El balance debe asegurarse en la progresión: un personaje full Fuerza no debería ser automáticamente superior si descuida Armadura o Destreza.
- Las habilidades, perks y escuadras deberían tener condiciones que aprovechen combinaciones específicas (ej.: perks que escalan con Destreza pero requieren Armadura mínima).
- El liderazgo puede convertirse en una *build support* muy válida: menos poder individual pero más control de tropas.

### 🔢 Sistema de progresión

- El jugador gana **+1 punto de atributo por nivel**.
- Desde nivel 1 hasta 30: **30 puntos de atributo disponibles** para distribuir.
- Los puntos se pueden asignar manualmente en cualquier momento desde la **interfaz del personaje**.
- No hay costos por reasignar puntos (reseteo libre desde la interfaz o barracón).

---

### 🧱 Atributos base por clase al nivel 1

| Clase (Arma) | Fuerza | Destreza | Armadura | Vitalidad |
| --- | --- | --- | --- | --- |
| Espada y Escudo | 4 | 2 | 4 | 3 |
| Espada a Dos Manos | 5 | 3 | 2 | 3 |
| Lanza | 3 | 4 | 2 | 3 |
| Arco | 2 | 5 | 1 | 2 |
- Estos valores base **no se pueden modificar** y definen la identidad inicial de cada clase.
- A partir de ahí, el jugador invierte los **30 puntos ganados por nivel** como prefiera.

---

### 💡 Ventajas del sistema

- Permite builds versátiles (tanques veloces, arqueros resistentes, etc.)
- Refuerza la fantasía de especialización sin encerrar al jugador.
- Compatible con perks que escalen por atributo.

### Límites Máximos y Mínimos por Atributo Según Clase

Este sistema establece un **rango permitido de cada atributo por clase**, con el objetivo de:

- Mantener la identidad de cada clase.
- Evitar builds rotas (ej. un arquero con más fuerza que un espadón).
- Permitir flexibilidad sin romper balance.

---

### 🎯 Reglas generales

- Todos los atributos empiezan con un valor base según clase.
- El jugador puede invertir los 30 puntos de nivel en cualquier atributo, **respetando los límites definidos**.
- Si se desea permitir superar los límites en el futuro, puede habilitarse mediante perks o equipo especial.

---

### 📏 Tabla de Límites por Clase

| Clase (Arma) | Fuerza (min/max) | Destreza (min/max) | Armadura (min/max) | Vitalidad (min/max) |
| --- | --- | --- | --- | --- |
| Espada y Escudo | 4 / 12 | 2 / 8 | 4 / 12 | 3 / 10 |
| Espada a Dos Manos | 5 / 14 | 3 / 9 | 2 / 8 | 3 / 10 |
| Lanza | 3 / 9 | 4 / 12 | 2 / 8 | 3 / 10 |
| Arco | 2 / 8 | 5 / 14 | 1 / 6 | 2 / 9 |

---

### 🧠 Notas de balance

- **Fuerza**: solo llega a 14 en Espada a Dos Manos (máximo del MVP).
- **Destreza**: alto en Lanza y Arco, define estilos más móviles y precisos.
- **Armadura**: solo Espada y Escudo puede alcanzar 12 (builds tanque puras).
- **Vitalidad**: equilibrado entre clases, ninguna supera 10 en MVP.

---

### 🛠️ Consideraciones técnicas

- El sistema de interfaz debe bloquear la asignación de puntos si se intenta pasar el límite.
- Se pueden mostrar valores como “8 / 12” al jugador para claridad.
- Una mecánica futura podría permitir “romper el límite” mediante equipo legendario o talentos élite.

### 5.3 Sistema de Asignación de Puntos de Atributo

### 🎮 ¿Qué es?

Una **interfaz de personaje** donde el jugador distribuye los puntos de atributo ganados al subir de nivel. Este sistema permite personalizar al héroe para adaptar su estilo de juego, sin dejar de respetar los **límites máximos por clase** definidos anteriormente.

---

### 🧱 Estructura de la UI de Atributos

| Elemento en pantalla | Descripción |
| --- | --- |
| **Atributos visibles** | Fuerza, Destreza, Armadura, Vitalidad (con su valor actual y máximo) |
| **Puntos disponibles** | Contador en parte superior (“Puntos sin asignar: X”) |
| **Botones de asignación** | [+] y [-] junto a cada atributo para sumar o quitar puntos (hasta el límite) |
| **Vista previa derivada** | Muestra cómo cambiarán los atributos derivados (vida, daño, etc.) |
| **Botón Confirmar** | Aplica los cambios realizados |
| **Botón Resetear** | Devuelve los puntos sin penalización, habilitado solo fuera de batalla |

---

### 🔄 Funcionalidad

- El jugador puede asignar los puntos disponibles cuando quiera, siempre que no esté en combate.
- Al cambiar un atributo, se actualizan en tiempo real los atributos derivados (por ejemplo, al subir Vitalidad se actualiza la barra de vida).
- Si se presiona **“Resetear”**, todos los puntos se devuelven y el jugador puede reconfigurar su build desde cero.

---

### 🔐 Restricciones técnicas

- No se puede superar el límite máximo de cada atributo definido por clase.
- No se puede reducir un atributo por debajo del mínimo de clase (valor base).
- Solo es posible resetear puntos desde:
    - Feudo
    - Barracón
    - Menú de personaje fuera de batalla

---

### 🧠 Ejemplo de UI textual

```
plaintext
CopiarEditar
─────────────────────────────
🧍 Personaje: Arco

Puntos sin asignar: 5

[Fuerza]     2 / 8   [-] [+]
[Destreza]   6 / 14  [-] [+]
[Armadura]   1 / 6   [-] [+]
[Vitalidad]  3 / 9   [-] [+]
─────────────────────────────
🔹 Vida total: 230
🔹 Daño perforante: +18%
🔹 Defensa: 9
─────────────────────────────
[ Confirmar cambios ]   [ Resetear ]

```

---

### 💡 Ventajas de este sistema

- Flexibilidad total para el jugador.
- Transparente y visualmente claro.
- Fomenta la experimentación y las combinaciones de builds.
- Base para un futuro sistema de roles o builds predefinidas.

### 5.4 clases

### 🛡️ Clase: Espada y Escudo

**Rol:** Soporte defensivo / Coordinador de líneas

**Arma:** Espada corta + escudo pesado

> El Espada y Escudo es un héroe centrado en mantener la línea de frente. No es un duelista, sino un pilar de resistencia que protege a su escuadra, bloquea avances enemigos y permite estabilizar puntos críticos del campo de batalla. Su presencia impone orden y estructura al frente.
> 

---

### 🧾 Atributos Base (visual)

- **Daño:** ⚫⚫⚪⚪⚪
- **Defensa:** ⚫⚫⚫⚫⚪
- **Velocidad:** ⚫⚫⚪⚪⚪
- **Control de escuadra:** ⚫⚫⚫⚫⚪

### 🧬 Atributos de héroe por clase

- **Fuerza:** 3
- **Destreza:** 1
- **Vitalidad:** 4
- **Armadura:** 2

---

### 🧠 Habilidades

- **Empuje de Escudo** – Rompe formaciones enemigas, útil para liberar a tu escuadra.
- **Defensa Reforzada** – Aumenta tus defensas y las de tu escuadra.
- **Intercepción** – Interrumpe unidades enemigas que se aproximan a tus tropas.
- **Ultimate: Muro Imparable** – Tú y tu escuadra ganan inmunidad al retroceso y +defensas.

---

### ✅ Ventajas

- Ideal para **aguantar puntos clave** y proteger aliados.
- Muy buen sinergizador con escuadras lentas y defensivas.

### ❌ Desventajas

- Daño personal muy limitado.
- Mal desempeño sin escuadra.

---

### ⚔️ Clase: Espada a Dos Manos

**Rol:** Disruptor de formaciones / Iniciador de escuadra

**Arma:** Espada larga

> Diseñado para romper líneas enemigas cuando se coordina con su escuadra. Este héroe abre brechas, no gana duelos. Su potencia ofensiva depende de golpear en sincronía con sus tropas.
> 

---

### 🧾 Atributos Base (visual)

- **Daño:** ⚫⚫⚫⚫⚪
- **Defensa:** ⚫⚫⚪⚪⚪
- **Velocidad:** ⚫⚫⚫⚪⚪
- **Control de escuadra:** ⚫⚫⚪⚪⚪

### 🧬 Atributos de héroe por clase

- **Fuerza:** 4
- **Destreza:** 2
- **Vitalidad:** 2
- **Armadura:** 1

---

### 🧠 Habilidades

- **Corte Giratorio** – Daño en área que abre espacios para tu escuadra.
- **Carga Imponente** – Atraviesa enemigos y desorganiza líneas.
- **Lluvia de Acero** – Combo de 3 golpes.
- **Ultimate: Juicio de Acero** – Golpe masivo que potencia tu escuadra si acierta.

---

### ✅ Ventajas

- Iniciador ofensivo con **alto potencial en presión**.
- Ideal para rematar formaciones ya debilitadas.

### ❌ Desventajas

- Frágil sin aliados.
- Expuesto al control y flanqueo.

---

### 🪓 Clase: Lanza

**Rol:** Control de zona / Coordinación táctica de bloqueos

**Arma:** Lanza larga

> La lanza es la clase de interrupción, hostigamiento y anticarga. Su función es contener, retrasar y dividir al enemigo. Perfecta para posicionamiento avanzado y aprovechar errores tácticos del rival.
> 

---

### 🧾 Atributos Base (visual)

- **Daño:** ⚫⚫⚫⚪⚪
- **Defensa:** ⚫⚫⚫⚪⚪
- **Velocidad:** ⚫⚫⚫⚫⚪
- **Control de escuadra:** ⚫⚫⚫⚫⚪

### 🧬 Atributos de héroe por clase

- **Fuerza:** 2
- **Destreza:** 4
- **Vitalidad:** 2
- **Armadura:** 2

---

### 🧠 Habilidades

- **Barrido Largo** – Detiene cargas, controla espacio.
- **Estocada Precisa** – Ideal para romper escudos o líneas.
- **Despliegue Defensivo** – Gana resistencia si está en primera línea.
- **Ultimate: Muro de Púas** – Zona peligrosa que limita el paso.

---

### ✅ Ventajas

- Versátil para **formaciones estáticas** y apoyo.
- Funciona bien como segunda línea.

### ❌ Desventajas

- Mal 1v1 directo.
- Requiere buen posicionamiento.

---

### 🏹 Clase: Arco

**Rol:** Soporte a distancia / Asesino de flancos

**Arma:** Arco largo

> El arquero no está diseñado para acumular kills solo. Su poder viene de debilitar y desorganizar, no de eliminar. Acompaña a su escuadra a distancia, crea aperturas para aliados y castiga errores enemigos desde retaguardia.
> 

---

### 🧾 Atributos Base (visual)

- **Daño:** ⚫⚫⚫⚫⚪
- **Defensa:** ⚫⚪⚪⚪⚪
- **Velocidad:** ⚫⚫⚫⚫⚫
- **Control de escuadra:** ⚫⚫⚪⚪⚪

### 🧬 Atributos de héroe por clase

- **Fuerza:** 1
- **Destreza:** 5
- **Vitalidad:** 2
- **Armadura:** 0

---

### 🧠 Habilidades

- **Disparo Enfocado** – Penetra armadura, gran daño a unidades pesadas.
- **Lluvia de Flechas** – Control de área y presión.
- **Flecha Sorda** – Niega habilidades enemigas temporalmente.
- **Ultimate: Flecha Llameante** – Ataque zonal de daño prolongado.

---

### ✅ Ventajas

- Alto control situacional.
- Excelente contra escuadras mal posicionadas.

### ❌ Desventajas

- Cuerpo a cuerpo = muerte segura.
- Dependiente de visión y cobertura.

---

## 

## 6. 📈 Progresión y Sistema de Perks

---

### 🎯 Filosofía General

En este juego, la progresión está diseñada para **reforzar la cooperación entre el héroe y sus escuadras**. El poder no proviene de un héroe sobrepotenciado, sino de cómo usa sus perks, builds y tácticas para **potenciar a sus tropas**.

El sistema recompensa a los jugadores que entienden:

- Cuándo usar su escuadra ofensiva o defensiva.
- Qué perks aplicar según mapa o composición enemiga.
- Cómo adaptarse a las necesidades del equipo.

---

### 🧬 6.1 Sistema de Progresión del Héroe

- El héroe sube de nivel desde **1 hasta 30** en el MVP.
- Cada nivel otorga:
    - `+1 punto de atributo` para distribuir (ver sección 5).
    - `+1 punto de talento` para desbloquear perks (ver abajo).
- No hay "prestigio" ni reset con bonificación en el MVP.
- El progreso es persistente, accesible desde el **feudo o barracón**.

---

### 🌱 6.2 Sistema de Perks

El sistema de perks es un **árbol de talentos ramificado**, inspirado en juegos como *Path of Exile* o *Last Epoch*, pero simplificado para accesibilidad táctica.

### 📚 Características clave:

- Dividido en **5 ramas**:
    - **Ofensiva**
    - **Defensiva**
    - **Táctica**
    - **Liderazgo**
    - **Especialización de Clase**
- Incluye **perks pasivos y activos**.
- El jugador puede activar hasta:
    - `5 perks pasivos`
    - `2 perks activos`
- Cada rama tiene sinergia con ciertas builds o tipos de escuadra.
- Los buffs y habilidades con efecto de área aplican a **cualquier escuadra aliada** cercana, no solo a la propia.

> 🔄 Perks se pueden resetear libremente desde la interfaz fuera de batalla.
> 

### 🧠 Perks en acción:

- No otorgan poder directo abrumador.
- Permiten **ajustar el estilo de mando** del jugador.
- Ej.: un jugador puede elegir ser un comandante táctico con buffs de formación, o un hostigador que mejora el rendimiento de unidades ligeras.

### 🧩 Integración con Loadouts

- Cada **loadout del héroe** incluye:
    - Arma / clase
    - Escuadras equipadas (según liderazgo)
    - Perks activos y pasivos
- Esto permite adaptarse antes de entrar a una partida (no en medio de combate).

### 📊 Ejemplo de perks (resumen):

| Nombre | Rama | Tipo | Efecto |
| --- | --- | --- | --- |
| **Maniobra Rápida** | Táctica | Pasivo | -30% al tiempo de cambio de formación |
| **Inspiración de Batalla** | Liderazgo | Pasivo | +1 punto de liderazgo base |
| **Carga Sanguinaria** | Ofensiva | Activo | La próxima carga inflige sangrado |
| **Flecha Llameante** | Clase (Arco) | Activo | Flecha especial con quemadura |
| **Tenacidad de Hierro** | Defensiva | Pasivo | +10% mitigación si el héroe no se mueve |

---

### 🏰 6.3 Barracón y Progresión de Escuadras

### 📌 ¿Qué es el Barracón?

El **barracón** es el centro de gestión de escuadras del jugador dentro del feudo. Aquí se visualizan, mejoran y reconfiguran las tropas disponibles para cada héroe.

### 🎯 Filosofía del sistema:

- Las escuadras son **el pilar del combate**.
- Su crecimiento es **paralelo al del héroe**, pero se centra en:
    - Mejorar estadísticas.
    - Desbloquear habilidades de escuadra.
    - Acceder a nuevas formaciones.

---

### 🪖 6.4 Progresión de Escuadras

- Cada escuadra sube de **nivel 1 a 30**.
- El progreso se guarda fuera de batalla.
- Se comparte entre loadouts si es la misma escuadra.

### Al subir de nivel, una escuadra puede:

| Desbloqueo | Frecuencia | Impacto |
| --- | --- | --- |
| + Estadísticas base | Cada nivel | Mejora vida, daño, etc. |
| + Nueva habilidad de escuadra | Cada 10 niveles | Añade 1 habilidad activa o pasiva |
| + Formación adicional | Cada ciertos niveles | Desbloquea nuevas posiciones tácticas |

---

### 🧩 6.5 Sistema de Liderazgo

- Cada escuadra tiene un **costo de liderazgo** (1–3 puntos).
- El héroe tiene un **valor de liderazgo base** que puede escalar con perks o equipo.
- Solo se pueden equipar escuadras cuyo costo total **no exceda el liderazgo del héroe**.

> Ejemplo: un héroe con 6 puntos de liderazgo puede llevar:
> 
> - 3 escuadras de costo 2
> - o 1 de 3, 1 de 2 y 1 de 1

---

### 🧪 6.6 Consideraciones de balance

- Las escuadras no deberían ser intercambiables en medio de batalla.
- El progreso debe enfocarse en que cada escuadra tenga **roles únicos**.
- Evitar “meta builds” abusivas basadas solo en stats: **formación + sinergia táctica** debe ser la clave.

---

### ✅ Ventajas del sistema

- Progresión clara pero profunda.
- Reforzamiento del concepto de escuadra como "unidad básica".
- Build del héroe ≠ build de combate directo, sino de comando.

## 7. ⚔️ Combate y Sistema de Daño

---

### 🎯 Filosofía del sistema de combate

El combate en este juego gira en torno a la **interacción entre escuadras**, sus **formaciones**, y la **coordinación entre héroes aliados**. El jugador **no es un guerrero solitario**, sino un **comandante de campo táctico**.

El **héroe no puede enfrentarse solo a escuadras enteras**: su función principal es **dirigir, apoyar y ejecutar con precisión**, aprovechando los puntos débiles del enemigo y el posicionamiento de sus tropas.

---

### 💥 Tipos de daño

Todo el daño, tanto de héroes como de unidades, se divide en tres tipos:

| Tipo | Efectividad principal |
| --- | --- |
| **Contundente** | Ideal contra unidades con armadura ligera o en formación cerrada |
| **Cortante** | Versátil, efectivo en combate general contra tropas medianas |
| **Perforante** | Eficaz contra escudos, formaciones densas o tropas muy defensivas |

---

### 🛡️ Defensas y penetraciones

Cada unidad y héroe tiene valores de **defensa** contra los tres tipos de daño:

- **DEF Cortante**
- **DEF Perforante**
- **DEF Contundente**

Además, cada ataque tiene su valor de **penetración** por tipo, que puede reducir el efecto de la defensa enemiga.

---

### 🧮 Fórmula de daño

```
plaintext
CopiarEditar
Daño efectivo = Daño base - (DEF del objetivo - PEN del atacante)

```

- Si `(DEF - PEN)` < 0, se ignora y se aplica **daño completo**.
- El sistema asegura que unidades con **buena penetración** sean viables contra formaciones pesadas, y que los **valores defensivos altos no bloqueen daño completamente** sin apoyo.

---

### 👁️ Sistema de detección y enfrentamientos

- Las unidades detectan enemigos por **ángulo de visión**, no por una esfera completa. Esto permite tácticas como el **flanqueo real**, donde el enemigo no te detecta hasta que estás en rango lateral o trasero.
- Al encontrarse dos escuadras cuerpo a cuerpo, **se forma automáticamente una línea de combate** (sin desorganización inicial).
- No existe niebla de guerra, pero sí **limitación de visibilidad realista** (terreno, ángulo, obstáculos).

---

### ❌ Control de masas

No se incluirán mecánicas de **CC (control de masas)** como:

- Aturdimientos
- Congelación
- Desarmes

> Esto se alinea con la idea de que el juego no gira en torno al micro-control de unidades individuales, sino al macro-posicionamiento y manejo de escuadras.
> 

---

### 🤝 Sin Friendly Fire

- Las escuadras **aliadas no sufren daño** de otras escuadras amigas (ni de héroes aliados).
- Esto permite coordinar ataques de múltiples frentes sin temor a dañar aliados.
- Las habilidades de área de héroes tampoco aplican daño a aliados.

---

### 🧱 Interacción con formaciones

- Las formaciones modifican las **zonas de impacto, alcance, masa, y defensa**.
- Por ejemplo, unidades en "muro de escudos" tienen menor penetración ofensiva, pero mayor **bloqueo y resistencia a empuje**.
- El sistema de combate tiene en cuenta **colisión física (masa)**: escuadras más pesadas pueden **empujar o frenar** a escuadras más ligeras.

---

### ⚙️ Otras reglas clave

- No hay niebla de guerra (*fog of war*).
- Todos los enemigos son visibles si están en campo abierto.

| Componente | Regla |
| --- | --- |
| **Cuerpo a cuerpo** | Solo ocurre si hay espacio entre formaciones. Tropas no atraviesan líneas ocupadas. |
| **Flanqueo** | Golpear desde el lateral o la retaguardia **ignora parte de la defensa enemiga**. |
| **Terreno** | La altura y obstáculos afectan línea de visión y movimiento. |
| **Interacción con héroes** | El héroe recibe daño como cualquier otra unidad, y puede ser eliminado si no está con su escuadra. |

---

### 🆕 7.2 🪦 Muerte del Héroe y Sistema de Respawn

Cuando un héroe es eliminado durante una batalla:

- Se activa un **tiempo de respawn** (cooldown), que **aumenta progresivamente** con cada muerte.
- El héroe reaparece en el **punto de spawn seleccionado** durante la fase de preparación.
- Reaparece con la **escuadra que le quede viva**, o **solo el héroe** si no quedan unidades.
- Durante el tiempo muerto, el jugador puede **espectar a aliados** con cámara libre, cambiando entre ellos con una tecla.

### 🧠 Comportamiento de escuadra mientras el héroe está muerto:

- La escuadra **mantiene posición** en su ubicación actual.
- Cuando faltan **10 segundos para el respawn**, la escuadra inicia **retirada automatizada hacia el punto de spawn**, eligiendo un camino que **evite las zonas con mayor presencia enemiga**.
- La escuadra **puede recibir daño durante la retirada**.
- A los **5 segundos de haber iniciado la retirada**, la escuadra **desaparece por completo del campo de batalla**.

---

### 🆕 7.3 👁️ Visibilidad y Detección

- No existe **niebla de guerra (fog of war)**.
- Cualquier unidad enemiga visible en campo abierto es **automáticamente revelada**.
- El terreno (muros, obstáculos, elevaciones) puede bloquear la visión y ocultar unidades detrás de cobertura.

> Este diseño favorece el juego táctico con posicionamiento, uso de terreno y scouting manual por parte del jugador.
> 

---

## 8. 🌍 Mapas y Modo de Juego

---

### 🎯 Filosofía de diseño

El mapa y el modo de juego están diseñados para **fomentar la toma de decisiones tácticas en equipo**. No se trata solo de posicionar escuadras, sino de **coordinar a tres héroes por bando**, cada uno con un rol distinto, para lograr la victoria a través del control del terreno, líneas defensivas y uso inteligente de supply points.

---

### 🏷️ Modo único del MVP: *Batalla*

| Parámetro | Valor |
| --- | --- |
| **Jugadores** | 3 vs 3 |
| **Duración estimada** | 15–20 minutos |
| **Condiciones de victoria** | <ul><li>**Atacantes**: Capturar los puntos de control antes de que termine el tiempo.</li><li>**Defensores**: Evitar la captura durante todo el tiempo límite.</li></ul> |

---

### 🧭 Estructura del mapa MVP

El mapa tiene un diseño **asimétrico semi-lineal**, con tres zonas clave:

### 🅰️ Puntos de Captura

Los **puntos de captura** son objetivos estratégicos que deben ser conquistados por el bando atacante para avanzar y ganar la partida. Su funcionamiento es diferente al de los supply points:

- **Propiedad inicial:** Todos los puntos de captura pertenecen al bando defensor al inicio de la partida.
- **Captura irreversible:** Una vez que un punto de captura es conquistado por el bando atacante, **no puede ser recuperado por el bando defensor** durante esa partida.
- **Desbloqueo secuencial:** Algunos puntos de captura están **bloqueados** al inicio y solo se pueden capturar si se ha conquistado previamente el punto anterior (precondición). Un punto bloqueado **no puede ser capturado** hasta que se desbloquee.
- **Punto de base:** Si el atacante conquista el punto de base, la partida termina inmediatamente con la victoria del bando atacante.
- **Progresión:** Al capturar un punto previo, se desbloquea el siguiente punto de captura en la secuencia, permitiendo el avance del equipo atacante.
- **Diferencia con supply points:** A diferencia de los supply points, los puntos de captura **no pueden cambiar de dueño varias veces**; su captura es definitiva para el resto de la partida.


### 🔄 Supply Points (2 por bando)

- Son **zonas seguras** donde los héroes pueden:
    - **Cambiar de escuadra activa**
    - **Recuperar recursos**
    - **Reorganizar formaciones**
- **Condiciones de uso:**
    - No pueden estar **en disputa** (es decir, ningún enemigo debe estar en su radio).
    - El cambio de escuadra **consume tiempo** (~3 segundos de canalización).
    - Solo puede haber **una escuadra activa por héroe** a la vez.

> Esto permite adaptación táctica, pero evita abuso de swaps constantes o en medio del caos del combate.
> 

### 🧱 Elementos del entorno

| Elemento | Interacción |
| --- | --- |
| **Puertas fortificadas** | Pueden ser destruidas por escuadras o habilidades pesadas |
| **Obstáculos físicos** | Bloquean línea de visión y movimiento |
| **Terreno elevado** | Aumenta alcance y visibilidad para unidades a distancia |
| **Pasillos estrechos** | Favorecen escuadras defensivas o emboscadas |

---

### 🧠 Dinámica de combate

- El mapa está pensado para **crear situaciones de interdependencia entre jugadores**:
    - Un jugador sostiene el punto.
    - Otro hostiga desde un flanco elevado.
    - El tercero intenta rotar o apoyar un sector débil.
- El tiempo y el control del mapa son más importantes que las kills:
    - **Capturar mal una posición puede dejarte sin refuerzos.**
    - **Tener una escuadra mal elegida puede costarte una rotación clave.**

---

### 📊 Ritmo de partida

| Fase | Duración aproximada | Objetivos clave |
| --- | --- | --- |
| **Inicio (0–3 min)** | Escaramuzas, escuadras defensivas despliegan | Control inicial del Punto A |
| **Medio (4–12 min)** | Reagrupamientos, cambios de escuadra, escaramuzas múltiples | Se decide la captura o pérdida de A |
| **Final (últimos 5 min)** | Defensa final del Punto Base o counter-push de defensores | Máxima coordinación de perks, ultimates y formaciones |

---

## 9. 📏 Flujo del Usuario (Scenes)

### 🧭 Objetivo

Esta sección describe la **secuencia lógica de navegación del jugador** a través de las diferentes pantallas (scenes) del juego. Está diseñada para ser **ágil, clara y funcional**, priorizando la **preparación estratégica y la progresión** por encima de cosméticos o microgestión irrelevante.

---

### 🔄 Flujo Completo

1. **Login**
2. **Selección o creación de personaje**
3. **Ingreso al Feudo (hub)**
4. **Barracón / Menú de personaje**
5. **Cola de batalla**
6. **Pantalla de preparación**
7. **Cargado del mapa y despliegue**
8. **Batalla**
9. **Post-partida: resumen y recompensas**

---

### 🗺️ Desglose de Escenarios

### 1. **Login**

- Acceso a la cuenta del jugador.
- Verificación de progreso, perks y escuadras asociadas.

### 2. **Selección o creación de personaje**

- El jugador elige entre 3 avatares base (masculino/femenino/neutro).
- Puede personalizar nombre y clase inicial.
- Se asignan atributos base y loadout inicial.

### 3. **Feudo (Hub social)**

- Zona libre de combate donde los jugadores pueden:
    - Ver a otros jugadores (multiplayer social).
    - Acceder a barracón, herrero, armería, etc.
    - Iniciar cola de batalla.
- Interfaz diegética: los menús están integrados en edificios o NPCs.

### 4. **Barracón / Interfaz de gestión**

> Accesible desde el feudo o directamente desde el menú principal.
> 
- Permite:
    - Configurar escuadras (ver stats, habilidades, formaciones).
    - Asignar y redistribuir atributos del héroe.
    - Equipar perks activos y pasivos.
    - Organizar loadouts.
    - Visualizar liderazgo disponible y escuadras compatibles.

### 5. **Cola de batalla (quick join)**

- Busca partida 3v3 con jugadores similares en nivel de escuadras o perks.
- Puede mostrar tiempo estimado o permitir seguir en el feudo mientras tanto.
- El emparejamiento es **aleatorio entre jugadores disponibles**.
- En el MVP no hay sistema de MMR ni emparejamiento por nivel.

### 6. **Preparación de batalla**

> Pantalla crítica para el pre-match. Todo se decide aquí.
> 
- Selección de:
    - Loadout del héroe
    - Escuadra inicial (según liderazgo)
    - Perks equipados
    - Formación de inicio
- Muestra:
    - Minimapa del escenario
    - Posibles rutas, posiciones iniciales aliadas y supply points
- Temporizador (90–120 segundos) para tomar decisiones

### 7. **Batalla**

- Se despliega HUD minimalista con:
    - Barra de vida del héroe y escuadra
    - Habilidades disponibles
    - Estado de formación / moral / posicionamiento
    - Objetivos activos (captura, defensa, etc.)
- Permite:
    - Dar órdenes a la escuadra
    - Activar habilidades de escuadra o héroe
    - Interactuar con supply points para cambiar escuadra (si está permitido)

### 8. **Post-partida: resumen y recompensas**

Pantalla de cierre estructurada en 3 pestañas:

| Pestaña | Contenido |
| --- | --- |
| **General** | Resultado (victoria/derrota), tiempo, puntos de control logrados |
| **Escuadras** | Rendimiento individual de cada unidad usada (kills, tiempo en punto, daño recibido) |
| **Héroe** | Habilidad más usada, perks activos durante la partida, daño causado por tipo, asistencias |
- Se otorgan:
    - Puntos de experiencia para el héroe y las escuadras utilizadas
    - Recompensas cosméticas o desbloqueos progresivos
    - Estadísticas para ajustar futuras builds

---

### 🔄 Notas sobre UX y futuro

- Todos los menús deben ser **rápidos, legibles y pensados para la táctica**.
- Se evita sobrecarga visual: solo se muestra información relevante.
- En el futuro, podrían añadirse:
    - Múltiples colas (ranked, evento, asimétrico)
    - Skins desbloqueables por hitos
    - Social features (formar escuadras con amigos desde el feudo)

---

### 9.1 ⚙️ Sistema de Matchmaking (MVP)

En el MVP, el emparejamiento funciona de forma simple:

- El jugador entra en **cola rápida 3v3**.
- El sistema forma equipos de manera **aleatoria**, sin considerar nivel, clase, escuadra ni estadísticas previas.
- No existe **MMR ni balance por habilidad** en esta fase del desarrollo.

> El matchmaking avanzado podrá integrarse en versiones posteriores, considerando rendimiento o composición de roles.
> 

---

## 10. 💰 Economía y Recompensas

### 🎯 Filosofía

La economía del MVP está centrada en **progresión táctica**, no en acumulación de poder. Las recompensas están diseñadas para:

- **Incentivar la cooperación** entre héroe y escuadra.
- Premiar el **uso estratégico de formaciones y sinergias**.
- Evitar loops de farmeo o desbalance por “grind”.

---

### 🎁 Recompensas por partida

Cada partida otorga recompensas en tres ejes:

| Recompensa | Descripción | Afecta a… |
| --- | --- | --- |
| **EXP de Héroe** | Subida de nivel del personaje jugado. Otorga puntos de atributo y perks. | Héroe |
| **EXP de Escuadras** | Experiencia para las unidades utilizadas. Desbloquea estadísticas y habilidades. | Escuadras equipadas |
| **Bronce** | Moneda base para progresión cosmética o logística (en versiones futuras). | Cuenta |
- La cantidad recibida escala según:
    - Resultado de partida (victoria/derrota).
    - Tiempo activo del jugador.
    - Objetivos completados (puntos capturados, asistencias).
    - Participación del jugador como comandante (uso de habilidades, órdenes a escuadra).

---

### 🪖 Equipamiento de Escuadras

Aunque no hay gestión manual de equipo en el MVP, se simula el desgaste de combate con reglas simples:

- **Recuperación automática**:
    - El equipamiento estándar de las escuadras se **reabastece automáticamente al final de cada partida**.
- **Penalización por pérdida total**:
    - Si una escuadra pierde **más del 90% de sus miembros** en batalla, su equipo sufre una penalización simbólica (solo narrativa en el MVP).
    - Esto no genera costes ni impacto mecánico, pero **podría habilitar restricciones en builds o selección futura** (para testeo de desgaste logístico en versiones posteriores).

---

### 🎨 Skins y personalización

- Solo existen **skins visuales**.
- **No afectan en absoluto el rendimiento o progresión.**
- En el MVP:
    - No se pueden desbloquear (todo lo visual es fijo).
    - El sistema de personalización está deshabilitado o limitado a selección inicial del personaje.
- En versiones futuras se pueden obtener vía:
    - Logros por escuadra.
    - Eventos.
    - Recompensas de temporada.

---

### ❌ Elementos excluidos del MVP

- No hay:
    - Rarezas de objetos.
    - Inventario de piezas.
    - Loot boxes ni tiendas.
    - Economía basada en intercambio.

Esto garantiza que la **única fuente de progreso es la experiencia táctica acumulada** por el jugador.

---

### 10.1 🛒 Fuentes de Equipamiento del Héroe

El equipo del héroe (armadura y armas) se consigue a través de:

- **Drops al finalizar la partida**
    - Recompensas aleatorias según desempeño y victoria/derrota.
- **Compra en el herrero** dentro del **feudo**.
    - Los jugadores pueden usar bronce para adquirir piezas específicas.
- **No hay crafteo** ni rarezas de equipamiento en el MVP.

> Todas las piezas de armadura y armas son iguales en stats dentro de cada tipo (ligera, media, pesada). Solo las skins visuales alteran su apariencia.
> 

---

## 11. 🏐 Alcance del MVP (Versión Jugable Inicial)

### 🎯 Objetivo del MVP

Demostrar el **núcleo táctico** del juego:

**la sinergia entre el héroe y su escuadra**, en un entorno PvP estructurado y limitado, pero funcional y representativo del gameplay final.

---

### 🧪 Componentes incluidos en el MVP

| Elemento | Estado en MVP | Descripción |
| --- | --- | --- |
| **Modo de juego principal** | ✅ | *Batalla 3v3*: captura de puntos vs defensa. |
| **Mapa** | ✅ | 1 solo mapa jugable con elementos estratégicos (terreno, supply points, puntos de captura). |
| **Clases de héroe** | ✅ | 2 clases iniciales: `Espada y Escudo` y `Espada a Dos Manos`. |
| **Escuadras disponibles** | ✅ | 4 tipos: `Escuderos`, `Arqueros`, `Lanceros`, `Piqueros`. |
| **Sistema de perks** | ✅ | Árbol funcional con perks activos y pasivos por rama. |
| **Atributos de héroe** | ✅ | Sistema de distribución de puntos con interfaz de asignación. |
| **Cambio de formación** | ✅ | Escuadras pueden cambiar formación en tiempo real. |
| **Comandos activos a escuadra** | ✅ | Habilidades desbloqueables y utilizables en combate. |
| **Cambio de escuadra** | ✅ | Solo desde **supply points seguros**, 1 escuadra activa a la vez. |
| **Feudo (hub social)** | ✅ | Espacio compartido entre jugadores, con NPCs y otras funciones básicas. |
| **NPC Herrero** | ✅ | Punto de interacción narrativa o futura gestión de equipo. |
| **Chat social y agrupación** | ✅ | Lobby social, chat de texto básico, y sistema para formar equipos pre-partida. |
| **HUD minimalista** | ✅ | UI inspirada en *Conqueror’s Blade*: información clara, sin sobrecargar. |

---

### 🧱 Exclusiones del MVP

| Sistema | Estado | Justificación |
| --- | --- | --- |
| Sistema de loot, objetos y rarezas | ❌ | No aplica. Se omite para evitar desbalance o loops de farmeo. |
| Personalización visual (skins, emotes) | ❌ | Reservado para versiones futuras. |
| Progresión por piezas de equipo | ❌ | MVP solo contempla experiencia y atributos. |
| PvE o campañas | ❌ | No contemplado en esta etapa. |
| Editor de escuadras profundo | ❌ | Escuadras predefinidas con progresión limitada. |

---

### 📌 Resumen

> El MVP debe permitir validar lo más importante:
> 
> 
> el **sistema de combate táctico**,
> 
> la **sinergia entre héroe y escuadra**,
> 
> y la **progresión estratégica** sin depender de power creep.
> 

---

## 12. 🧭 UI y HUD

### 🎯 Principios de diseño

- **Minimalista y funcional**: el jugador debe ver lo justo y necesario en combate.
- **Táctica primero**: prioridad a información de tropas, formaciones, cooldowns y posicionamiento.
- **Legibilidad clara**: íconos claros, sin saturación de elementos en pantalla.
- **Inspiración**: *Conqueror’s Blade*, *Total War: Arena*, *Battlefield 1 (modo comandante)*.

---

### 12.1 🧱 Elementos del HUD en Batalla

| Elemento | Posición | Descripción |
| --- | --- | --- |
| **Barra de vida del héroe** | Inferior izquierda | Muestra salud actual, clase y estado de armadura. |
| **Mini retrato del héroe** | Junto a la barra de vida | Ícono de clase y escuadra activa. |
| **Cooldown de habilidades** | Parte inferior central | 4 slots (3 normales + ultimate), con temporizador y efecto visual. |
| **Indicadores de escuadra** | Inferior derecha | Nombre de escuadra activa, salud total, número de unidades vivas. |
| **Formación activa** | Junto al indicador de escuadra | Ícono con tooltip desplegable. |
| **HUD de órdenes** | Tecla contextual (ej. Shift) | Rueda o botones para ordenar: mover, mantener, cargar, cambiar formación. |
| **Mapa táctico / minimapa** | Superior derecha | Muestra terreno, supply points, aliados, enemigos detectados. |
| **Notificación de objetivo** | Superior centro | Objetivo actual: capturar, defender, replegar. |
| **Mensajes del equipo / chat** | Inferior izquierda (colapsable) | Chat de equipo. Solo visible fuera de combate por defecto. |

---

### 12.2 📋 Pantallas de interfaz (UI)

| Pantalla | Funcionalidad |
| --- | --- |
| **Feudo** | Acceso a barracón, herrero, loadouts, perks y atributos. |
| **Barracón** | Gestión y visualización de escuadras. Muestra nivel, habilidades, formaciones y fichas. |
| **Pantalla de preparación de partida** | Vista previa de mapa, elección de escuadra inicial, perks activos y formación de inicio. |
| **Pantalla de personaje** | Atributos, distribución de puntos, perks activos/pasivos, resumen de estadísticas derivadas. |
| **Pantalla de loadout** | Combina clase, escuadra activa, perks, formación inicial. Permite guardar presets. |
| **Post-batalla** | 3 pestañas: resumen general, rendimiento de escuadras, estadísticas del héroe. |
| **Menú de pausa / ESC** | Permite ver el estado actual, objetivos activos, cambiar opciones gráficas/sonido. |

---

### 12.3 🎮 Controles rápidos / input clave

| Acción | Tecla propuesta | Comentario |
| --- | --- | --- |
| Ordenar mover escuadra | RMB (clic derecho) | Apunta y mueve hacia zona indicada. |
| Ordenar mantener posición | `H` o botón contextual | Detiene movimiento de escuadra. |
| Cambiar formación | `F` o rueda contextual | Cambia a la siguiente formación disponible. |
| Usar habilidad de héroe | `Q / E / R` + `Ult: F` | Íconos con cooldown visibles. |
| Usar habilidad de escuadra | `1 / 2 / 3` | Muestra en HUD con cooldown. |
| Cambiar escuadra (en supply point) | `TAB` (en punto seguro) | Interfaz emergente para swap. |

---

### 🔍 Detalles visuales clave

- **Color coding**:
    - Azul: aliados
    - Rojo: enemigos
    - Gris: neutral / sin controlar
    - Verde: supply point disponible
- **Indicadores contextuales**:
    - Flechas direccionales en minimapa para refuerzos enemigos.
    - Iconos flotantes sobre escuadras (escudo, arco, lanza) para reconocimiento rápido.
- **Tooltips explicativos**:
    - Al pasar el mouse sobre perks, formaciones, habilidades, etc.

---

### 🧪 Pruebas de usabilidad esperadas

- El HUD debe permanecer **claro a pesar del caos visual** del combate.
- Las **órdenes deben sentirse reactivas** y reflejarse de forma inmediata en el HUD.
- El sistema de cambio de escuadra solo debe mostrarse cuando el jugador está en un **supply point no disputado**.
- El minimapa debe evitar mostrar información no relevante (no hay fog of war, pero sí restricción por línea de visión y aliados cercanos).

## 13. 📘 Glosario de Conceptos Clave

---

### 1. 🧍‍♂️ **Héroe**

El avatar del jugador. Es creado desde cero y completamente personalizable (nombre, apariencia, atributos). Solo puede haber **un héroe activo por jugador** en batalla, y su función es **liderar y coordinar** una escuadra, no brillar por fuerza individual.

---

### 2. 🧥 **Skins de Héroe**

Elementos visuales aplicables a las piezas de armadura o arma del héroe. No afectan atributos, estadísticas ni jugabilidad. Solo tienen valor **cosmético**.

---

### 3. ⚔️ **Armas**

Cada héroe equipa una única arma, y esta **define su clase**. Las clases disponibles en el MVP son:

- Espada y Escudo
- Espada a Dos Manos
- Lanza
- Arco

Las armas determinan las habilidades del héroe, su estilo de combate y sus límites de atributo.

---

### 4. 🛡️ **Piezas de Armadura**

El héroe puede equipar 4 piezas: **casco, guantes, pechera y pantalones**. Cada pieza puede ser:

- **Ligera** (mayor movilidad)
- **Media** (balance)
- **Pesada** (mayor defensa)

Las piezas de armadura contribuyen a la **mitigación de daño** y al peso total del personaje.

- Las piezas de armadura se consiguen por:
    - **Drops de partida** (recompensas al terminar).
    - **Compra en el herrero** dentro del feudo.
- No hay sistema de crafteo en el MVP.

---

### 5. 🪖 **Squads**

Conjunto de unidades controladas por IA bajo el mando del héroe. Cada héroe puede tener **solo una escuadra activa a la vez**. Las escuadras tienen:

- Formaciones tácticas (línea, testudo, etc.)
- Órdenes disponibles (seguir, atacar, mantener posición)
- Habilidades únicas
- Composición de unidades del mismo tipo

---

### 6. 🖼️ **Skins de Unidad**

Skins visuales aplicables a unidades de escuadra. Al igual que las skins del héroe, **no afectan estadísticas ni desempeño**. Son cosméticas.

---

### 7. 🧍‍♂️ **Unidad**

Individuo que conforma una escuadra. Cada escuadra solo puede estar compuesta por **un único tipo de unidad** (por ejemplo, arqueros o lanceros).

---

### 8. 🧠 **Perks**

Talentos que el jugador desbloquea mediante un árbol de progresión ramificado. Hay perks:

- **Pasivos** (bonificaciones constantes)
- **Activos** (habilidades utilizables)

Cada loadout permite seleccionar hasta **5 pasivos y 2 activos**. Los perks personalizan el estilo de mando del jugador y se dividen en ramas como ofensiva, táctica, defensiva, liderazgo o especialización de clase.

---

### 9. 🔺 **Formación**

Patrón de organización que adopta una escuadra para ganar ventajas tácticas. Las formaciones afectan:

- Cómo reciben daño
- Qué espacio ocupan
- Cómo se comportan al avanzar o defender

Formaciones disponibles:

- Línea
- Testudo
- Dispersa
- Cuña
- Schiltron
- Muro de Escudos

No todas las formaciones están disponibles para todas las escuadras.

---

### 10. 🎯 **Órdenes**

Instrucciones que el jugador puede dar a su escuadra durante el combate:

- **Seguir**: la escuadra acompaña y protege al héroe.
- **Mantener posición**: la escuadra se queda donde fue colocada, conservando su formación.
- **Atacar**: la escuadra prioriza atacar enemigos dentro de su rango de acción.

Estas órdenes pueden cambiar en tiempo real y adaptarse al contexto táctico.

### 11. 🧰 **Equipamiento (de Unidades)**

Son las **piezas que representan la armadura y armas** que usan las unidades dentro de una escuadra.

- Se degradan o **se pierden si más del 90% de la escuadra muere** durante la batalla.
- Si una unidad tiene **menos del 50% de su equipamiento**, entra con penalizaciones.
- Si está en 0%, la escuadra **no puede ser desplegada** hasta que se recupere.

---

### 12. 🎒 **Loadout**

Configuración táctica del jugador previa a la batalla. Incluye:

- Escuadras seleccionadas (según el **valor de liderazgo** del héroe).
- Perks activos y pasivos.
- Clase y equipamiento del héroe.

> El loadout se selecciona antes de cada partida y no puede ser modificado una vez dentro, salvo cambio de escuadra en puntos de suministro válidos.
> 

---

### 13. 🏅 **Liderazgo**

Recurso numérico que define:

- Cuántas escuadras puede llevar un héroe al campo.
- Cuáles puede tener equipadas en el **loadout**.

Cada escuadra tiene un **costo de liderazgo** y el héroe un valor máximo. No se pueden seleccionar escuadras si su suma excede el límite del héroe.

---

### 14. ⚡ **Estamina**

Recurso del héroe que se consume al:

- Atacar
- Correr o esquivar
- Usar habilidades activas

La buena gestión de estamina es clave para sobrevivir y apoyar eficazmente a la escuadra.

---

### 15. ⚔️ **Batalla**

El centro del gameplay. Un enfrentamiento estructurado entre **dos bandos (3 vs 3)** donde:

- Un bando ataca intentando capturar puntos.
- El otro defiende hasta que expire el tiempo.

El jugador participa con su héroe y **una sola escuadra activa** a la vez, aunque puede cambiarla en condiciones específicas.

---

### 16. 🩹 **Supply Point (Punto de Suministro)**

Estructura fija en el mapa con **efectos tácticos importantes**. Sus funciones incluyen:

- **Cambiar escuadra activa** del jugador (si no está en disputa).
- **Curar al héroe y su escuadra** pasivamente dentro de su radio.
- **Capturables** si no pertenecen al bando del jugador.

Tipos:

- **Aliado**: interactuable, cura.
- **Enemigo**: capturable, no interactuable.
- **Neutral**: puede capturarse por cualquier bando.

---

### 17. 🎌 **Punto de Captura**

Objetivo estratégico del mapa. Sirve para:

- Que **los atacantes avancen** y ganen tiempo.
- Que **los defensores resistan** y bloqueen el progreso enemigo.

Tipos:

- **Normal**: otorga tiempo adicional si es capturada.
- **Base**: su captura finaliza la partida a favor de los atacantes.

---

### 18. 🕰️ **Captura de Banderas**

Mecánica que regula la toma de puntos de captura:

- Solo se inicia si un **héroe del bando atacante entra en el rango**.
- Se interrumpe si un héroe defensor entra.
- El progreso **no se reinicia** si es interrumpido: se pausa.
- Aporta **puntos personales** en el post-batalla según tiempo dentro del punto.

---

### 19. 🏗️ **Maquinaria de Asedio**

Elementos del mapa que permiten avanzar en **batallas con estructuras defensivas**. Solo están disponibles para el bando atacante.

- **Torre de asedio**: permite cruzar murallas.
- **Ariete**: destruye puertas.

Las escuadras del jugador deben **empujar** estas estructuras tras interactuar con ellas.

---

### 20. 🛡️ **Bandos**

Grupos de jugadores enfrentados entre sí en una partida.

- **Atacantes**: deben capturar puntos antes que se acabe el tiempo.
- **Defensores**: deben resistir manteniendo los puntos hasta el final.

---

### 21. 🏳️ **Spawn Points (Puntos de Aparición)**

Son los lugares en el mapa donde los **héroes y sus escuadras aparecen** al inicio de la batalla.

- Determinados por el **bando del jugador**.
- Están **protegidos** y no se pueden capturar ni invadir.
- Solo se usan al inicio de partida o en ciertos eventos futuros (no en MVP).

---

### 22. 🗡️ **Daño y Defensa**

Existen tres **tipos de daño**:

- **Contundente** (Blunt): golpes, masa, impacto.
- **Cortante** (Slashing): espadas, hachas.
- **Perforante** (Piercing): lanzas, flechas.

Cada tipo de daño tiene una **defensa correspondiente** que lo mitiga. Las unidades y héroes poseen valores independientes para cada tipo.

---

### 23. 🔪 **Penetración de Armadura**

Atributo que representa la **capacidad de ignorar parte de la defensa del enemigo**.

- Existe un valor para cada tipo de daño.
- Se **resta directamente** de la defensa antes de calcular el daño recibido.

**Ejemplo de fórmula aplicada:**

```
Daño efectivo = D - (DEF - PEN)
```

---

### 24. ⚙️ **Formación (de Escuadra)**

Configuraciones tácticas que adoptan las unidades dentro de una escuadra según la orden del héroe.

Cada tipo de escuadra tiene disponibles **distintas formaciones**, como:

- Línea
- Dispersa
- Testudo
- Cuña
- Schiltron
- Muro de escudos

Afectan su comportamiento, defensas y sinergia con el terreno y enemigo.

---

### 25. 🗯️ **Órdenes (de Escuadra)**

Instrucciones directas que el héroe puede dar a su escuadra activa durante la batalla. Las principales son:

- **Seguir**: la escuadra sigue al héroe, protegiéndolo.
- **Mantener posición**: se mantiene en la ubicación actual con la formación activa.
- **Atacar**: se lanzan contra enemigos dentro de rango de detección.

Las órdenes pueden combinarse con formaciones para maximizar la efectividad táctica.

---

### 26. 🧱 **Barracón**

Interfaz y espacio donde el jugador **gestiona sus escuadras** fuera de combate.

- Permite **visualizar, desbloquear, formar o disolver** escuadras.
- Cada escuadra formada **progresa individualmente**.
- Las escuadras solo se pueden usar si han sido **formadas previamente**.

Opciones disponibles:

- **Formar**: consume desbloqueo y recursos, crea una escuadra lista para levear.
- **Desvandar**: elimina una escuadra formada y todo su progreso.

---

### 27. 🧪 **Progresión (de escuadras y héroe)**

Sistema por el cual héroes y escuadras **ganan experiencia y mejoran**:

- El **héroe sube de nivel** (1–30) y asigna puntos a atributos y perks.
- Las **escuadras suben de nivel** (1–30) y desbloquean mejores stats, habilidades y formaciones.
- El progreso es **persistente entre partidas**.

Cada sistema tiene su propia curva de progresión, diseñada para fomentar la especialización y el dominio táctico.