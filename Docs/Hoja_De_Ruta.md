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
| **Tropas:** las 37 del motor con su `tropaId`, formaciones, nivel 1-10, habilidades en 4/7/10 y soldados por escuadra. Hoy hay 4 en la base de escuadras, clonadas del relleno, y sus formaciones tienen 20 huecos (las de 25 y 30 sacan 20 al campo) | ⬜ siguiente |
| **Clases del héroe:** de 4 a las 5 del motor; la clase sale del arma del ticket; el héroe se monta desde el `HeroSnapshot` (nivel, atributos, aspecto). Hoy una tabla de paso en `BatallaRealDeTicket.Clase` y el aspecto de héroes de prueba | ⬜ |
| **Objetos y cifras de combate:** ids y cifras del catálogo `OBJETOS`, piezas forjadas del ticket, equipo y montura del ticket, todo como modificadores de la pila de stats | ⬜ |
| **Rechazar** lo que Conquest no conozca: clase, objeto y versiones de catálogo (hoy solo tropas) | ⬜ con los tres anteriores |
| **Borrar el relleno** según se sustituye: 7 escuadras (`Resources/Squads/*` menos `BronzeAge/`), 4 clases (`Resources/Data/HeroClasses`), 19 objetos (`Resources/Items`) y lo que solo usen ellos | ⬜ a medida |
| **El contenido de las Eras I-III** (tropas y edificios nuevos: caballería, carros, levas sin formación; antes CQ-006) | ⬜ con las tropas |

## 4. Fuera de la batalla: migrar BronzeAgeClient a Unity

Todo lo de BronzeAgeClient pasa a Conquest. Antes de empezar, **hacer el inventario** de pantallas y vistas (ver la lista de
partida abajo) y decidir el orden con el autor.

| | Estado |
|---|---|
| **Conexión al motor:** Conquest como cliente de la sesión del contrato 05 (WebSocket con Protobuf: login, ámbitos, foto y delta, órdenes). Hoy Conquest solo habla con el motor a través del lanzador, en la batalla | ⬜ base de todo lo demás |
| **Mapa de mundo:** se migra tal como está en BronzeAgeClient (`src/mundo`, `src/mundo3d`), con cuadros y relleno, con sus medidas y velocidades; después se rellena poco a poco con modelos propios | ⬜ |
| **Mapas de plaza, pueblo, aldea, avanzada y campamento de mercenarios** sobre FeudoScene (en BronzeAgeClient, `src/ciudad3d`). El campamento se monta desde la planta que da el servidor; el `.proto` aún no la transmite (antes CQ-007) | ⬜ |
| **Pantallas y paneles** (acceso, partidas, carga, héroe, columna, ejército, mercado, tecnología, diplomacia, crónica, taberna, misiones…), con la guía visual `Docs/Diseno_Interfaz.md` de BronzeAgeClient («bronce y arcilla») | ⬜ |
| **Aedas, tecnologías, épicas y crónica** en el mundo y la interfaz (antes CQ-008) | ⬜ |
| **Sigilo de Facción y estandartes**, compuestos por ids, también en la batalla (antes CQ-009) | ⬜ |
| **El paso del mundo a la batalla y vuelta:** el cliente recibe en `Batalla.asignacion_propia` su instancia y su token, entra en el servidor de batalla y vuelve al mundo al acabar o al salir | ⬜ |

Lista de partida para el inventario (BronzeAgeClient, `src/ui`): acceso y partidas, pantalla de carga, mapa (capas, vista
guardada, elección de campamento), plaza (HUD, interior, puerta, obras, muralla, guarnición, residencia, mercado, trueques,
tesorería, cargos, admisión, protección, fundación), pueblos y avanzadas, columna y ejército (convocatoria, liderazgo, salida,
carro, velocidad), caravanas, héroe y base personal, reclutamiento y árbol de tropas, tecnología, Facción (diplomacia,
aliados, anexión, fusión), mercenarios, taberna, intel y rumores, misiones y tablón, aedas y crónica, informes de combate,
avisos y ayuda.

## 5. Arte (en paralelo)

| | Estado |
|---|---|
| **Inventario de Synty a sustituir**, por categoría (armaduras, armas, edificios, props). Hoy 58 de nuestros 126 prefabs dependen de `Assets/Synty` y no hay ningún modelo propio en `Assets/Art/Models` | ⬜ |
| **Norma para los modelos propios:** mismo estilo y detalle que Synty, sobre su esqueleto, y detrás de los mismos ids para no tocar código | ⬜ |
| **Terreno del ticket:** la batalla en el sitio del mundo (hash del artefacto, centro, paquete de relieve; `06_Contrato_de_Mundo.md` §4 y §8). Va después del mapa de mundo nuevo | ⬜ |
| **CPU del cliente:** con 130 modelos, la red son 13 puntos de 180 y el resto es animar (~83) y pintar (~84). Menos Animators para los lejanos o animación por GPU | ⬜ |
| **Interfaz del juego** para el aviso de conexión y el botón de abandonar (hoy una caja provisional y F10 dos veces) | ⬜ |

## 6. Decisiones pendientes del autor ❓

- Predecir al héroe del jugador sobre la malla de navegación, como está, en lugar de con Unity Physics (doc 22 §8).
- Dos plazos de paso: 10 s sin señal para dar a un jugador por caído y 60 s de espera a que entren antes de empezar.
- El orden de la migración de BronzeAgeClient (tras el inventario del bloque 4).

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
