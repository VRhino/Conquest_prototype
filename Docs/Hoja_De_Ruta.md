# Hoja de ruta de Conquest

Documento vivo: **qué falta para que Conquest sea el juego**, en qué orden y qué hay decidido. Se actualiza en el mismo
commit que cierra o abre algo: lo hecho se marca y se resume en una línea con su enlace, lo nuevo entra en su bloque y lo que
no tiene sitio va a «Sueltos». Nada se queda solo en una conversación.

Estados: ✅ hecho · 🔄 en curso · ⬜ por hacer · ❓ espera una decisión del autor.

## 0. Cómo es el producto

- **Tres piezas** (decisión del 2026-11-11): el **motor** (BronzeAgeFase0, Rust) manda en el mundo y guarda lo persistente;
  el **servidor de batalla** es Conquest compilado como servidor dedicado (Netcode for Entities), un proceso por batalla
  arrancado por el lanzador del motor; el **cliente** es Conquest, para todo: batalla y fuera de ella.
- **BronzeAgeClient** (`../BronzeAgeClient`, HTML y TypeScript) fue un prototipo rápido mientras se migraba a Unity: **todo lo
  que tiene va a Unity** (decisión del 2026-11-13). No tiene nada de la batalla; tiene todo lo demás. La base y las
  dimensiones de su mapa de mundo son las buenas (velocidad de movimiento y tamaño de los elementos); sus maquetas son de
  relleno.
- **Fuente de verdad de las reglas:** el motor (`Docs/Game` y `Docs/Mechanics`; si se contradicen, gana Mechanics). La forma de
  los mensajes: `BronzeAgeFase0/proto/bronzeage/v1/`. Lo que el motor espera de Conquest, con sus cifras:
  [`Trabajo_En_Conquest.md`](../../BronzeAgeFase0/Docs/Coordinacion/Trabajo_En_Conquest.md).
- **Orden:** primero la red (hecha), después el juego con el motor y al final el arte, que va en paralelo.

## 1. Red y lanzador ✅

Cerrado el 2026-11-13, dentro del mapa de pruebas y con los héroes, mapas y escuadras que hay (alcance que fijó el autor).
Todo en [`Arquitectura/22_Servidor_De_Batalla_Red_2026-11-11.md`](Arquitectura/22_Servidor_De_Batalla_Red_2026-11-11.md):
la batalla de verdad de Game.ECS en el servidor con fantasmas espejo (camino A), el héroe del jugador predicho, cámara,
interfaz, partida, controles, proyectiles; el ticket del motor y su resultado; robustez; incorporaciones; salidas y
reaparición. Las dos pruebas del motor con Conquest (`crates/lanzador/tests/lanzador.rs`) pasan con la batalla de verdad.

Lo que se dejó fuera a propósito va en los bloques 3 y 5 (terreno del ticket, catálogos).

## 2. Limpieza de lo que se rehace o era relleno

| | Estado |
|---|---|
| Borrar el mapa de mundo de Conquest (scripts, escenas, datos, Yoge, VegetationSpawner, Addressables) | ✅ 591c30fa |
| Borrar el mapa de asentamiento de Conquest (SettlementPreview y sus escenas) | ✅ 591c30fa |
| Borrar el modelo de cooperación entre repositorios (`Docs/Coordinacion` de Conquest, `propuestas/` del motor) | ✅ (este commit) |
| Quitar el paquete `terrain-tools` si nada de la batalla lo usa | ⬜ comprobar antes |

**Se queda** (decisión del 2026-11-13): FeudoScene (base de los mapas de plaza, pueblo, aldea, avanzada y campamento de
mercenarios), los modelos de caballos (nuestros, hechos en Unity y Blender), el esqueleto de Synty y los sistemas que tiran
de él (el héroe modular, el aspecto por ids de piezas) y el mapa de la batalla.

## 3. Datos del motor en lugar del relleno

Regla: cada dato del motor entra con lo suyo (prefab, formaciones, habilidades, módulos) y, en cuanto nada usa una pieza de
relleno, se borra. Las cifras y los ids salen de `Trabajo_En_Conquest.md` §2-§4.

| | Estado |
|---|---|
| **Tropas:** las 37 del motor con su `tropaId`, formaciones, nivel 1-10, habilidades en 4/7/10 y soldados por escuadra. Hoy hay 4 en la base de escuadras, clonadas del relleno, y sus formaciones tienen 20 huecos (las de 25 y 30 sacan 20 al campo). Detalle y propuesta por tropa: [`Tropas_Del_Motor.md`](Tropas_Del_Motor.md) | 🔄 importador, versión, formaciones del ticket, nivel y habilidades hechos |
| **Clases del héroe:** de 4 a las 5 del motor; la clase sale del arma del ticket; el héroe se monta desde el `HeroSnapshot` (nivel, atributos, aspecto). Hoy una tabla de paso en `BatallaRealDeTicket.Clase` y el aspecto de héroes de prueba | ⬜ |
| **Objetos y cifras de combate:** ids y cifras del catálogo `OBJETOS`, piezas forjadas del ticket, equipo y montura del ticket, todo como modificadores de la pila de stats | ⬜ |
| **Rechazar** lo que Conquest no conozca: clase, objeto y versiones de catálogo (hoy solo tropas) | ⬜ con los tres anteriores |
| **Borrar el relleno** según se sustituye: 7 escuadras (`Resources/Squads/*` menos `BronzeAge/`; son las bases de las que se copian las tropas del motor, así que se van cuando cada base tenga su arte), 4 clases (`Resources/Data/HeroClasses`), 19 objetos (`Resources/Items`) y lo que solo usen ellos | ⬜ a medida |
| **El contenido de las Eras I-III** (tropas y edificios nuevos: caballería, carros, levas sin formación; antes CQ-006) | ⬜ con las tropas |

## 4. Fuera de la batalla: migrar BronzeAgeClient a Unity

Todo lo de BronzeAgeClient pasa a Conquest. El inventario, pantalla por pantalla (qué muestra, qué órdenes y datos del motor
usa, de qué depende y cuánto código es), está en [`Inventario_BronzeAgeClient.md`](Inventario_BronzeAgeClient.md): unas 43.000
líneas de TypeScript en 19 bloques, que mandan 180 de las 191 órdenes del contrato. El autor aprobó el inventario y este orden
el 2026-11-13.

**Orden.** Primero una tajada que se juega de punta a punta contra el motor de verdad (pasos 1 a 5: entrar, nacer, salir,
marchar, pelear contra bandidos en Conquest y volver), y después el resto por lo que más se usa. Cada paso se prueba con el
servidor real. Entre paréntesis, la sección del inventario.

| | Paso | Estado |
|---|---|---|
| 1 | **Conexión, acceso y avisos:** Conquest como cliente de la sesión del contrato 05 (WebSocket con Protobuf y zstd: `Hola`, ámbitos, foto y delta, órdenes, reloj, reconexión), el almacén de ámbitos, login y partidas, pantalla de carga, los datos de `/v1/datos`, el artefacto de mundo y el relieve, y el sistema de avisos. El contrato ya está compilado en C# (`Assets/Plugins/BronzeAge`); hoy solo lo usa el lanzador (§3.1, §3.2, §3.19) | ⬜ siguiente, base de todo lo demás |
| 2 | **Mapa de mundo con cuadros:** terreno del relieve, agua y niebla planos, la cámara y las medidas de BronzeAgeClient, tu columna, clic para marchar, lo ajeno como cuadros, la lista bajo el clic y la ficha con las acciones a la puerta. Después se rellena poco a poco con modelos propios (§3.4) | ⬜ |
| 3 | **Crear el héroe:** la clase, el creador de avatar de Conquest y elegir campamento sobre el mapa del paso 2 (§3.3) | ⬜ |
| 4 | **Campamento de mercenarios por dentro** sobre FeudoScene, con lo básico (residir, comprar, reclutar) y la ventana «Salir al mundo». El campamento se monta desde la planta que da el servidor; el `.proto` aún no la transmite (antes CQ-007) (§3.15, §3.8) | ⬜ |
| 5 | **El paso del mundo a la batalla y vuelta:** atacar un campamento de bandidos desde el mapa, recibir en `Batalla.asignacion_propia` la instancia y el token, entrar en el servidor de batalla, volver al mundo al acabar o al salir, e informe de combate. Incluye `cancelarBatalla`. Aquí se cierra el ciclo con el motor (§3.8, §3.19) | ⬜ |
| 6 | **Base personal y reclutamiento:** héroe, equipo, tropas, carro y el árbol de tropas (§3.9, §3.10) | ⬜ |
| 7 | **Plaza por dentro** sobre FeudoScene, con su HUD, gobierno y defensa, y la armería (§3.6, §3.7, §3.9) | ⬜ |
| 8 | **Facción, tecnología, sigilo y estandartes.** El sigilo, compuesto por ids, va también en la batalla (antes CQ-009) (§3.11, §3.12) | ⬜ |
| 9 | **Mapa estratégico, pueblos, aldeas y avanzadas** (§3.5, §3.13) | ⬜ |
| 10 | **Mercado y caravanas**, con `cancelarOrdenMercado`, `aparcarCaravana` y `reclamarCaravana`, que BronzeAgeClient no tiene (§3.14) | ⬜ |
| 11 | **Ejércitos:** convocatorias, unirse, perseguir y batallas en curso (§3.8) | ⬜ |
| 12 | **Mercenarios por contrato, taberna y misiones** (§3.15, §3.16, §3.17) | ⬜ |
| 13 | **Aedas, épicas y crónica** en el mundo y la interfaz (antes CQ-008) (§3.18) | ⬜ |

**Decidido** (2026-11-13):

- **Dentro de una plaza se empieza con la vista de Conquest:** andar con el héroe por FeudoScene. Una vez dentro se ve,
  interfaz por interfaz, cuál de BronzeAgeClient se migra tal cual y cuál se adapta.
- **La interfaz de fuera de la batalla se hace con UI Toolkit** (UXML y USS), con la guía visual «bronce y arcilla» de
  BronzeAgeClient (`docs/Diseno_Interfaz.md`). La de la batalla sigue en UGUI.
- **El creador de avatar de Conquest se queda** para crear el héroe (`OrdenCrearHeroe` ya pide cabeza, pelo, barba y cejas por
  id). Lo que le falte al motor para el aspecto se apunta aquí y se le pide después.
- **El mapa estratégico** se hace de la forma más barata y que más reutilice: una textura del mundo entero con los rasters del
  artefacto que ya baja la conexión, la niebla de la sesión encima y los iconos con UI Toolkit. Sin teselas ni modelos 3D.
- **Lo que no se migra** (bancos de pruebas, adaptadores al REST viejo, repintado por sondeo, panel F3) está en el inventario,
  §5.
- **Las pantallas de relleno de Conquest de fuera de la batalla** (login, selección de personaje, barracón, preparación,
  posbatalla, emparejamiento, tienda) se borran cuando llega la que las sustituye.

**Pedidos al motor que salgan de la migración:** ninguno todavía.

## 5. Arte (en paralelo)

| | Estado |
|---|---|
| **Tropas del motor a medias, a la espera de arte** (decisión del 2026-11-13; [`Tropas_Del_Motor.md`](Tropas_Del_Motor.md)): los **carros de guerra** no salen (un ticket con ellos se rechaza) hasta que haya carro; **honderos** y **escaramuzadores, peltastas y agrianos** disparan flechas hasta que haya honda y jabalina; los **inmortales** van solo como arqueros hasta que haya lanza y arco; los **espadachines y hacheros sin escudo** llevan el modelo de los escuderos; las **levas** (leñadores, granjeros, mineros) usan el de los lanceros | ⬜ |
| **Inventario de Synty a sustituir**, por categoría (armaduras, armas, edificios, props). Hoy 58 de nuestros 126 prefabs dependen de `Assets/Synty` y no hay ningún modelo propio en `Assets/Art/Models` | ⬜ |
| **Norma para los modelos propios:** mismo estilo y detalle que Synty, sobre su esqueleto, y detrás de los mismos ids para no tocar código | ⬜ |
| **Terreno del ticket:** la batalla en el sitio del mundo (hash del artefacto, centro, paquete de relieve; `06_Contrato_de_Mundo.md` §4 y §8). Va después del mapa de mundo nuevo | ⬜ |
| **CPU del cliente:** con 130 modelos, la red son 13 puntos de 180 y el resto es animar (~83) y pintar (~84). Menos Animators para los lejanos o animación por GPU | ⬜ |
| **Interfaz del juego** para el aviso de conexión y el botón de abandonar (hoy una caja provisional y F10 dos veces) | ⬜ |

## 6. Decisiones pendientes del autor ❓

- Predecir al héroe del jugador sobre la malla de navegación, como está, en lugar de con Unity Physics (doc 22 §8).
- Dos plazos de paso: 10 s sin señal para dar a un jugador por caído y 60 s de espera a que entren antes de empezar.

## 7. Sueltos

- En la escena de un jugador (BattleScene) los arqueros no disparan: falta la reserva de proyectiles (`ObjectPoolSystem`,
  `RangedCombatBootstrap`); en red la suple `ReservaDeProyectiles.Asegurar()`.
- En la batalla en red faltan cambiar de escuadra e interactuar, y el héroe apunta con el movimiento, no con la cámara.
- Unity Transport 6.5 en Windows deja sordo al servidor cuando muere el proceso de un cliente en una máquina que contesta
  «puerto cerrado»: tapado con margen (`DriversDeBatalla`), no arreglado. Arreglo de verdad: Unity, o servidor en Linux.
- Probar con alguien al teclado, en el cliente con ventana, el retardo, las salidas y la reaparición (solo lo han probado
  bots).
- Un ticket que vuelve con la revisión subida después de una incorporación pierde las incorporaciones que ya habían llegado.
- La XP y el botín los pone el motor (D6, D7); en Conquest queda quitar el cálculo local cuando se juegue en modo conectado.

## Historial

- 2026-11-13: creado al cerrar la red. Absorbe lo vivo del modelo de cooperación (las propuestas CQ-001 a CQ-011: lo hecho
  está en el bloque 1; CQ-006 a CQ-009 están en los bloques 3 y 4) y las decisiones del autor de ese día.
- 2026-11-13: inventario de BronzeAgeClient ([`Inventario_BronzeAgeClient.md`](Inventario_BronzeAgeClient.md)) y orden de la
  migración aprobados por el autor: el bloque 4 pasa a ser la lista ordenada de 13 pasos.
