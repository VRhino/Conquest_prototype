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

**Regla (Víctor, 2026-11-13).** Cada pantalla migrada trae del prototipo sus iconos y piezas visuales (barras, chips, colores,
medallones), no solo sus datos. Los iconos salen del registro único `IconosDelMundo`, sacado de BronzeAgeClient por script
(`Tools/IconosDelPrototipo/generar.mjs`); no se dibujan a mano ni se inventan los que el prototipo no tiene. Los emoji son de un juego
libre (Twemoji, CC BY 4.0), no los del sistema. **Excepción (Víctor, 2026-11-13):** toda interfaz que muestre tropas usa las piezas de tropa de Conquest (la miniatura y la
tarjeta con la rareza, `PiezasDeTropa`), no el icono dibujado del prototipo. Ver [`Arquitectura/27_Iconos_Del_Mundo`](Arquitectura/27_Iconos_Del_Mundo_2026-11-13.md).

| | Paso | Estado |
|---|---|---|
| 1 | **Conexión, acceso y avisos:** Conquest como cliente de la sesión del contrato 05 (WebSocket con Protobuf y zstd: `Hola`, ámbitos, foto y delta, órdenes, reloj, reconexión), el almacén de ámbitos, login y partidas, pantalla de carga, los datos de `/v1/datos`, el artefacto de mundo y el relieve, y el sistema de avisos. El contrato ya está compilado en C# (`Assets/Plugins/BronzeAge`); hoy solo lo usa el lanzador (§3.1, §3.2, §3.19) | ✅ conexión, acceso y partidas, carga y avisos en `MundoScene` ([`23_Conexion_Al_Motor`](Arquitectura/23_Conexion_Al_Motor_2026-11-13.md)) |
| 2 | **Mapa de mundo con cuadros:** terreno del relieve, agua y niebla planos, la cámara y las medidas de BronzeAgeClient, tu columna, clic para marchar, lo ajeno como cuadros, la lista bajo el clic y la ficha con las acciones a la puerta. Después se rellena poco a poco con modelos propios (§3.4) | ✅ el mapa con cuadros en `MundoScene`: suelo, agua, niebla, tu columna, todo lo demás, la lista bajo el clic, la ficha con sus acciones y la salida de los sitios ([`24_Mapa_De_Mundo`](Arquitectura/24_Mapa_De_Mundo_2026-11-13.md)) |
| 3 | **Crear el héroe:** la clase, el creador de avatar de Conquest y elegir campamento sobre el mapa del paso 2 (§3.3) | ✅ «Nace tu héroe» (el campamento sobre el mapa, con su región, el recomendado, «Elegir por mí» y comparar dos, y el nombre) y, al nacer, el creador de personaje de Conquest con las clases del motor, el género y el aspecto; con héroe, al entrar, el selector de personaje de Conquest con el héroe en 3D ([`25_Crear_El_Heroe`](Arquitectura/25_Crear_El_Heroe_2026-11-13.md)) |
| 4 | **Campamento de mercenarios por dentro** sobre FeudoScene, con lo básico (residir, comprar, reclutar) y la ventana «Salir al mundo». El campamento se monta desde la planta que da el servidor: viaja en `CampamentoMercenarios` (`planta`, `trazado`, `empalizada`, `empalizada_puerta`) desde el motor del 2026-10-27; la nota antigua (CQ-007) decía que no (§3.15, §3.8) | ✅ El interior en FeudoScene en modo «desde el motor», con el campamento montado desde la planta y el héroe del motor andando (4A), y sus interfaces (4B): la taberna (residir), el mercado (comprar), el edificio militar (pedir prestada y reclutar) y «Salir al mundo» con tropa, carga y víveres, abiertas con la tecla F en su zona como el herrero de Conquest ([`26_Interior_Del_Campamento`](Arquitectura/26_Interior_Del_Campamento_2026-11-13.md)). Sin verificar contra el motor: reclutar aceptado (en una partida nueva el campamento aún no ofrece tropa) |
| 5 | **El paso del mundo a la batalla y vuelta:** atacar un campamento de bandidos desde el mapa, recibir en `Batalla.asignacion_propia` la instancia y el token, entrar en el servidor de batalla, volver al mundo al acabar o al salir, e informe de combate. Incluye `cancelarBatalla`. Aquí se cierra el ciclo con el motor (§3.8, §3.19) | ✅ **5A ✅ (2026-11-13):** «⚔ Atacar» a unos bandidos, la ficha de la batalla (quién contra quién con su tropa, el estado, el plazo, la frase de «fallida sin castigo»), «Cancelar la batalla» mientras se puede, todos los rechazos con su frase y, con `asignacion_propia`, la entrada en el servidor de batalla con la **sesión del mundo viva** (DontDestroyOnLoad). Probado de punta a punta con batalla real (motor 358af5ff con batallas de Unity, lanzador de verdad, `PruebaRed.exe`). Ver [`Arquitectura/28`](Arquitectura/28_Del_Mundo_A_La_Batalla_2026-11-13.md). **Tanda 1 ✅ (2026-11-14):** la preparación en su escena (`PreparacionScene`, UI Toolkit, piezas de tropa): el orden y la formación de las escuadras de la columna (solo para esa batalla, los aplica el servidor de batalla; el motor no guarda nada), el equipo, el campo con tu punto de aparición, quién combate y «Listo»; el servidor de batalla empieza con todos listos o a los 60 s; el aspecto de verdad del héroe en la batalla; cancelar o anular vuelve a MundoScene con la sesión viva; lo estático, reiniciado entre batallas. **Tanda 2 ✅ (2026-11-14):** el informe de combate en su escena (`InformeScene`, calcado del de BronzeAgeClient: resultado del motor, bajas por escuadra de los dos bandos con sus piezas de tropa, héroes, botín, carro, experiencia y herida), también desde el aviso del mundo; «Salir» al mapa en el sitio de la batalla; «Abandonar la batalla» con confirmación; conexión perdida; la batalla en el mapa mientras dura. Dos batallas terminadas seguidas en la misma sesión, probado de punta a punta |
| 6 | **Base personal y reclutamiento:** héroe, equipo, tropas, carro y el árbol de tropas (§3.9, §3.10) | 🔶 En tres tandas con parada; las dudas y las respuestas de Víctor, en `tasks/bloque4-paso6-plan.md`. **Tanda 1 ✅ (2026-11-14):** la Base personal a pantalla completa sobre el mapa o el interior (yelmo o B; la barra solo con Base y Avisos mientras está abierta; lo demás de la barra en un menú), con tres pestañas: Héroe (calco del detalle del héroe de Conquest: atributos, el héroe en 3D girable sobre el fondo de la ventana y el equipo puesto con su rareza), Tropas (filtros, rejilla de tarjetas, la tropa en 3D y sus datos en «Características» y «Atributos» como el panel del barracón; formación guardada en el motor, «Reponer» solo en tu campamento con «Pagar con»; sin ordenar) y Bienes (el almacén personal de objetos y de recursos a la izquierda; la mochila y el carro a la derecha; los objetos pasan entre mochila y almacén dentro de tu residencia). En «Salir al mundo», ◀ ▶ para el orden de combate, que se guarda en el motor al salir ([`29_Base_Personal`](Arquitectura/29_Base_Personal_2026-11-14.md)). **Tanda 2 ✅ (2026-11-15):** el equipo y el héroe: los objetos del motor con la pieza de Conquest de su hueco y peso, teñida de su metal, y su rareza por tier; equipar y quitar donde el motor deja, con la ficha del objeto y la comparación; el héroe vestido pieza a pieza en el retrato, el selector, la Base, el interior y la batalla, con el arma de su clase y cascos, hacha, dagas y escudo redondo provisionales de Synty; el pelaje del caballo por montura; atributos del motor con «Repartir puntos», perks solo para ver, «Usar» en la mochila y la pestaña Loadouts (cuarta pestaña). Repartir y usar, sin verificar: no hay orden de admin para dar experiencia ni objetos. **Tanda 3 (2026-11-15, a revisar):** el árbol de reclutamiento del prototipo en el edificio militar del campamento (`ArbolDeTropas`, la misma disposición que el prototipo), entero, con la tarjeta de tropa de Conquest en cada nudo y su estado; la ficha de la tropa a la derecha con «Pedir prestada» o «Reclutar» y «Pagar con» |
| 7 | **Plaza por dentro** sobre FeudoScene, con su HUD, gobierno y defensa, y la armería (§3.6, §3.7, §3.9) | 🔶 En tres tandas con parada; las dudas y las respuestas de Víctor, en `tasks/bloque4-paso7-plan.md`. **Tanda 1 (2026-11-15, a revisar):** la plaza montada desde el motor (edificios por tipo y estado, calles, muralla celda a celda) con el héroe andando y F en cada edificio; el HUD calcado del prototipo (blasón con renombrar, residencia y capital; obras y cola; Población, Almacén y Estado; «Salir al mundo»); la ficha de cada edificio (recetas, el buey, la receta de la bodega o la prensa, mejorar, quitar de la cola, cancelar la obra) y «Subir de nivel». **Tanda 2 (2026-11-15, a revisar):** el riel; Construcción en un cajón (pestañas por tipo, la cola, auto-construcción, «Construidos»); Gobierno (cargos con los residentes y políticas); el Almacén a pantalla completa (la barra elegida, el neto por minuto, la extracción, la reserva del Tesorero, pausar el consumo, los talleres) y «Tu carga». **Tanda 3 (2026-11-15, a revisar):** Defensa (guarnición, muralla y puerta con los aliados), Reclutamiento con el árbol del campamento y los estados de la plaza, la Armería (mochila, tienda, forja y abrirla) y la plaza ajena de visita con lo que manda el motor desde dentro ([`30_Plaza_Por_Dentro`](Arquitectura/30_Plaza_Por_Dentro_2026-11-15.md)). Con la Vía Rápida desde el principio en las pruebas (motor cc1e5ec), verificados la guarnición, la muralla entera y vetar. Sin verificar: subir de nivel de verdad (sin orden de admin para la población), forjar (sin orden de admin para planos ni crisoles). |
| 8 | **Facción, tecnología, sigilo y estandartes.** El sigilo, compuesto por ids, va también en la batalla (antes CQ-009) (§3.11, §3.12) | 🔶 En cuatro tandas con parada; las dudas y las respuestas de Víctor, en `tasks/bloque4-paso8-plan.md`. **Tanda 1 (2026-11-15, a revisar):** el sigilo del prototipo como textura (el blasón de la plaza, el selector, los carteles del mapa y el color de Facción) y la ventana de Facción sin Facción (elegir, crear con el selector de sigilo y pedir ingreso) ([`31_Faccion_Y_Sigilo`](Arquitectura/31_Faccion_Y_Sigilo_2026-11-15.md)). **Tanda 1 aprobada (sin commit).** **Tanda 2 (2026-11-15, a revisar):** tu Facción (el lateral, Miembros con solicitudes y cargos, Plazas, Ajustes del Rey), los cargos en la Base y el fondo de refundación del campamento (pendiente del paso 4). **Tanda 2 aprobada (sin commit).** **Tanda 3 (2026-11-15, a revisar):** la pestaña Tecnología (el árbol por carriles y Eras sobre el tablero común del árbol de tropas, la ficha fija con «Adoptar» para el Rey en la capital y los logros del mundo) y, en el árbol de tropas, «adoptable» con la etiqueta que abre esa tecnología y las tecnologías de tu Facción en la ficha corta. **Tanda 3 aprobada (sin commit).** **Tanda 4 (2026-11-15, a revisar):** la ficha de tropa completa (Edificios y pueblos, En campaña, Combate), la pestaña Diplomacia entera (lista, fichas, «Nueva propuesta» y las 13 órdenes), los estandartes con el sigilo del ticket en la batalla (el del héroe, uno por escuadra y las banderas de las zonas; bandidos y neutro) y la ventana «Opciones» con «Estandarte de las tropas». |
| 9 | **Mapa estratégico, pueblos, aldeas y avanzadas** (§3.5, §3.13) | 🔶 Análisis (2026-11-15) en `tasks/bloque4-paso9-plan.md`, en tres tandas con parada. Víctor contestó: la avanzada por dentro como el prototipo; en el mapa 2D, clic en un punto para marchar como en el 3D. **Tanda 1 (2026-11-15, a revisar):** el mapa estratégico 2D a pantalla completa (suelo del artefacto de lejos y de cerca con árboles, ríos, caminos, zonas, niebla, iconos, capas; el clic en el mundo marcha o elige), elegir un punto en él, y Territorio en el riel de la plaza («Pueblos y aldeas» con marcar aldea o extractor, las tarjetas de pueblo y obra, y «Fundación»), con las obras de mapa en el HUD ([`32_Mapa_Estrategico_Y_Territorio`](Arquitectura/32_Mapa_Estrategico_Y_Territorio_2026-11-15.md)). **Tanda 1 aprobada (sin commit).** **Tanda 2 (2026-11-15, a revisar):** las avanzadas en Territorio (colocar la atalaya eligiendo el sitio en el mapa 2D, mejorar, reparar, destruir, tropa de serie), la puerta desde la ficha del mapa (entregar materiales en la obra, guarnición de pueblo y avanzada, carga al almacén, llenar víveres) y la capa de parcelas de bosque. **Tanda 2 aprobada (sin commit).** **Tanda 3 (2026-11-15, a revisar):** por dentro del pueblo y la aldea (FeudoScene con la planta del motor) y de la avanzada (la planta de su etapa hecha en el cliente, como el prototipo), con su riel (Resumen, Guarnición, Acarreo y escolta, Carga; Almacén y Tropa de serie) y la escolta del carro de acarreo. **Tanda 3 aprobada; paso 9 en commit.** |
| 10 | **Mercado y caravanas**, con `cancelarOrdenMercado`, `aparcarCaravana` y `reclamarCaravana`, que BronzeAgeClient no tiene (§3.14) | 🔶 Análisis (2026-11-15) en `tasks/bloque4-paso10-plan.md`, en tres tandas con parada (órdenes; caravanas y escolta; rutas y trueques). Víctor contestó: Región en la tanda 3; nada de caravanas en la columna del héroe; un carro 3D reutilizable para las caravanas que viajan solas (y, en el paso 11, detrás del ejército). **Tanda 1 (2026-11-16, a revisar):** el Mercado en el riel de la plaza (Órdenes: la boleta, las órdenes en pie y «✕» para cancelarlas), las órdenes de una plaza ajena (comerciar en la puerta) y la ventana Mercado desde la ficha de la plaza en el mapa ([`33_Mercado_Y_Caravanas`](Arquitectura/33_Mercado_Y_Caravanas_2026-11-16.md)). |
| 11 | **Ejércitos:** convocatorias, unirse, perseguir y batallas en curso (§3.8) | ⬜ Pendiente del paso 4: en «Salir al mundo», «Cómo sales» solo ofrece «Columna personal»; el modo «Ejército» del prototipo (`salidaComoEjercito.ts`, con su preparación) entra aquí |
| 12 | **Mercenarios por contrato, taberna y misiones** (§3.15, §3.16, §3.17) | ⬜ Pendiente del paso 4: el tablón de mercenarios por contrato y la taberna del campamento (la pieza `taberna` de la planta es su sitio natural) |
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
- **Las escenas nuevas del juego van en `Assets/Scenes/BronzeAge`** (Víctor, 2026-11-14): MundoScene, PreparacionScene e InformeScene. `GameScenes` es lo de Conquest de antes (BattleScene, FeudoScene, AvatarCreator, CharacterselecctionScene, BattlePrepScene, PostBattleScene…).
- **Las pantallas de relleno de Conquest de fuera de la batalla** (login, barracón, preparación,
  posbatalla, emparejamiento, tienda) se borran cuando llega la que las sustituye.
- **El selector de personaje de Conquest** (`CharacterselecctionScene`) sustituye a la pantalla de partidas cuando la cuenta tiene
  héroe (paso 3, 2026-11-13). El creador de personaje (`AvatarCreator`) es el de crear el héroe en el motor.

- **Sin barras de desplazamiento si el contenido cabe** (Víctor, 2026-11-14). Si de verdad no cabe, la barra es estrecha (6 px), sin flechas,
  con el riel casi invisible y el asidero del bronce del marco: un estilo común de `Mundo.uss` que heredan todas las pantallas. Ver
  [`29_Base_Personal`](Arquitectura/29_Base_Personal_2026-11-14.md).

**Pedidos al motor que salgan de la migración:**

- **Elegir las escuadras al entrar en batallas que no son de columna** (paso 5, 2026-11-14). La defensa por loadout ya vale para el paso de los asedios.
- **Bajarse de una batalla sin cancelarla** (paso 5, 2026-11-14), y que se cancele si se baja el último. Quien se baja vuelve a su columna tal cual.
- **Que quien abandona una batalla vea cómo acabó** (paso 5, 2026-11-14): hoy, al salir, deja de combatir y la proyección le quita la batalla y su resultado.
- **A futuro: la tropa del campamento en «Nace tu héroe»** (paso 6, 2026-11-14, duda 34 del plan, opción b): enseñar al elegir campamento qué tropa recluta cada uno. Hoy el motor no la da antes de nacer; no entra en esta migración.
- **Un hecho de cierre de la batalla** (paso 5, 2026-11-14) con el botín, lo perdido del carro, la herida y la experiencia, para el informe sin comparar el estado de antes con el de después.

- **Salir del campamento con destino en una sola orden** (paso 2, 2026-11-13): `salirDelCampamento` con `objetivo` contesta `objetivo.no_soportado`, así que el clic en el mapa desde dentro de un campamento manda dos órdenes (salir y luego `marcharA`). Funciona así; con una sola orden sería atómico.
- ~~**Guardar el género y el avatar del héroe**~~ (paso 3, 2026-11-13) ✅ resuelto en el motor 358af5ff (contrato 0.25.0, partida nueva): `crearHeroe` guarda el género (`""`, `masculino` o `femenino`) y las cuatro piezas (ids de hasta 32 caracteres; si no, `heroe.aspecto_invalido`) y los devuelve en `Heroe.femenino` y `Heroe.avatar`. Conquest los manda desde el creador de personaje y la prueba los lee de vuelta. El catálogo de piezas sigue en Conquest (`AvatarPartDatabase.asset`).
- **A futuro, fuera de esta migración: borrar el héroe y crear otro** (paso 3, 2026-11-13, pedido del autor). Hoy el motor tiene un héroe por cuenta y partida y no hay orden de borrarlo. Cuando exista, el selector de personaje recupera en el modo «desde el motor» los botones «Crear héroe» y «Borrar héroe», que ahí quedan ocultos.

## 5. Arte (en paralelo)

| | Estado |
|---|---|
| **Tropas del motor a medias, a la espera de arte** (decisión del 2026-11-13; [`Tropas_Del_Motor.md`](Tropas_Del_Motor.md)): los **carros de guerra** no salen (un ticket con ellos se rechaza) hasta que haya carro; **honderos** y **escaramuzadores, peltastas y agrianos** disparan flechas hasta que haya honda y jabalina; los **inmortales** van solo como arqueros hasta que haya lanza y arco; los **espadachines y hacheros sin escudo** llevan el modelo de los escuderos; las **levas** (leñadores, granjeros, mineros) usan el de los lanceros | ⬜ |
| **Inventario de Synty a sustituir**, por categoría (armaduras, armas, edificios, props). Hoy 58 de nuestros 126 prefabs dependen de `Assets/Synty` y no hay ningún modelo propio en `Assets/Art/Models` | ⬜ |
| **Norma para los modelos propios:** mismo estilo y detalle que Synty, sobre su esqueleto, y detrás de los mismos ids para no tocar código | ⬜ |
| **Terreno del ticket:** la batalla en el sitio del mundo (hash del artefacto, centro, paquete de relieve; `06_Contrato_de_Mundo.md` §4 y §8). Va después del mapa de mundo nuevo | ⬜ |
| **CPU del cliente:** con 130 modelos, la red son 13 puntos de 180 y el resto es animar (~83) y pintar (~84). Menos Animators para los lejanos o animación por GPU | ⬜ |
| **Interfaz del juego** para el aviso de conexión y el botón de abandonar (hoy una caja provisional y F10 dos veces) | ⬜ |
| **Marco de bronce de la interfaz de fuera de la batalla** (UI Toolkit, `Assets/Resources/MundoUI/Mundo.uss`): USS no tiene degradados ni sombras, así que hoy la placa va solo con bordes y sin dentículos. Arte en 9-slice para la placa, el botón de oro y la moldura de dentículo, como la guía «bronce y arcilla» (decisión del 2026-11-13) | ⬜ |

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
