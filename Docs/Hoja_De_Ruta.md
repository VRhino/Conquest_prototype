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
| **Borrar el relleno del héroe** según se sustituye: 4 clases (`Resources/Data/HeroClasses`), 19 objetos (`Resources/Items`) y lo que solo usen ellos. El de las escuadras va con el arte (§5) | ⬜ con las clases y los objetos |

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
| 10 | **Mercado y caravanas**, con `cancelarOrdenMercado`, `aparcarCaravana` y `reclamarCaravana`, que BronzeAgeClient no tiene (§3.14) | 🔶 Análisis (2026-11-15) en `tasks/bloque4-paso10-plan.md`, en tres tandas con parada (órdenes; caravanas y escolta; rutas y trueques). Víctor contestó: Región en la tanda 3; nada de caravanas en la columna del héroe; un carro 3D reutilizable para las caravanas que viajan solas (y, en el paso 11, detrás del ejército). **Tanda 1 (2026-11-16, a revisar):** el Mercado en el riel de la plaza (Órdenes: la boleta, las órdenes en pie y «✕» para cancelarlas), las órdenes de una plaza ajena (comerciar en la puerta) y la ventana Mercado desde la ficha de la plaza en el mapa ([`33_Mercado_Y_Caravanas`](Arquitectura/33_Mercado_Y_Caravanas_2026-11-16.md)). **Tanda 1 aprobada (commit).** **Tanda 2 (2026-11-16, a revisar):** la pestaña Caravanas (la flota: crear, carros y animales, nombre, reservar, viaje y cancelar, aparcadas), la escolta comercial y el carro 3D reutilizable con su animal para las caravanas que viajan solas. **Tanda 2 aprobada (commit a8832b0d).** **Tanda 3 (2026-11-16, a revisar):** el Encargo de las caravanas (rutas y envíos con sus paradas) y la pestaña Rutas, la pestaña Trueques, la Taberna con el informe de la región y la pestaña Región (tomar órdenes a distancia). |
| 11 | **Ejércitos:** convocatorias, unirse, perseguir y batallas en curso (§3.8) | 🔶 Análisis (2026-11-16) en `tasks/bloque4-paso11-plan.md`, en tres tandas con parada (salir como ejército por convocatoria; el ejército en el mapa, unirse y la batalla; las caravanas del ejército con el carro 3D detrás). Dudas contestadas por Víctor: la caravana comercial se engancha en la preparación, junto a la de fundación (tanda 3). **Tanda 1 (2026-11-16, en commit `8a2bd3ea`):** salir como ejército por convocatoria: «Cómo sales» con Ejército, la política y las convocatorias del sitio, y la preparación entera (integrantes, peticiones, tropa y carga, cancelar, salir y separarse) ([`34_Ejercitos`](Arquitectura/34_Ejercitos_2026-11-16.md)). **Tanda 2 (2026-11-16, en commit `9b7ee80b`):** el ejército en el mapa: la ventana Ejército (⚑) con organizar, formación, peticiones, acampar, ceder el mando y separarse; «Unirme» en la ficha de un ejército de tu Facción; «Unirse» en la ficha de una batalla (con su lado en una persecución); «Unirse a un ejército» en el riel de la plaza. **Añadido (2026-11-16, a revisar):** las tarjetas de los ejércitos a los que te puedes unir (a la puerta y preparándose dentro) en el lateral derecho de la plaza, con sus integrantes plegables y «Unirse» (en commit `3e4ceba4`). **Tanda 3 (2026-11-16, a revisar):** las caravanas del ejército: las comerciales de la plaza en la preparación («Llevárnosla», enganchadas al salir), cargar y entregar en la ventana ⚑, aparcar dentro de la plaza, «Entrar con el ejército» y el carro 3D detrás del ejército. |
| 12 | **Mercenarios por contrato, taberna y misiones** (§3.15, §3.16, §3.17) | 🔶 Análisis (2026-11-16) en `tasks/bloque4-paso12-plan.md`, en tres tandas con parada: la Taberna (intel y mercenarios de la región, en la plaza y en el campamento); los contratos (el tablón del campamento y Facción › Mercenarios); las misiones (diario, rastreador arriba en el lateral de la plaza y tablones). Dudas contestadas por Víctor (1a 2b 3a 4a 5b 6: la Taberna con lo que tu cargo permite, pagando con el fondo de la plaza quien tiene cargo y con sus monedas el resto; el tablón en la pieza Militar; también a la puerta del campamento; la barra de órdenes; las valoraciones guardadas en el equipo; el rastreador arriba a la derecha en todas partes). La intel la pagan los cargos con el oro del almacén (7a). **Tanda 1 (2026-11-16, a revisar):** la Taberna: Intel (Mirada, informe de plaza, informe de región de la región de la plaza, rumores, tus Miradas e informes) en la plaza, en el campamento y a su puerta ([`35_Taberna`](Arquitectura/35_Taberna_Contratos_Y_Misiones_2026-11-16.md)); «Mercenarios de la región» pasa a la tanda 2 (en commit `eced9885`). **Tanda 2 (2026-11-16, en commit `051e82b5`):** los contratos: el tablón de mercenarios en la pieza Militar del campamento, a su puerta y como «Mercenarios de la región» en la Taberna de la plaza; Facción › Mercenarios con la barra de órdenes, prorrogar, mudar, despedir y valorar (guardado en el equipo); los avisos `mercenarios.*`. **Tanda 3 (2026-11-16, en commit `16c97fc4`):** las misiones: el botón «Misiones» con su insignia y el diario (seguir y reclamar), el rastreador arriba a la derecha en la plaza, el mapa y el campamento, los tablones de recurrentes de la plaza (en el diario) y de la Facción (Facción › Tablón) con aceptar, reclamar y elegir la semana, y Facción › Misiones. Con ella se cierra el paso 12. Del paso 4 quedaba el tablón de mercenarios y la taberna del campamento (la pieza `taberna` de la planta es su sitio natural). |
| 13 | **Aedas, épicas y crónica** en el mundo y la interfaz (antes CQ-008) (§3.18) | 🔶 Análisis (2026-11-16) en `tasks/bloque4-paso13-plan.md`, en dos tandas con parada: los Aedas (la ficha del itinerante en el mapa, la ventana Aedas de la plaza con comprar y las épicas de los residentes, y los avisos `aedas.*`); la crónica del mundo (su botón en la barra y su panel). Dudas contestadas por Víctor (1a 2a 3a: la crónica en un botón de la barra tras Avisos; el itinerante, un cuadro hasta tener modelos y el parado junto a su plaza; la compra, probada con el reloj adelantado si un itinerante para en la plaza). **Tanda 1 (2026-11-16, en commit `615e23d4`):** los Aedas: el itinerante con su nombre en el mapa (el parado, junto a su plaza) y su ficha, la ventana Aedas de la plaza (comprar, residentes y épicas) y los avisos `aedas.*` ([`36_Aedas`](Arquitectura/36_Aedas_Epicas_Y_Cronica_2026-11-16.md)). **Tanda 2 (2026-11-16, en commit `37eb571b`):** la crónica del mundo: el botón «Crónica» tras Avisos, su panel con icono, frase y fecha, lo tuyo marcado, «Más antiguas» y lo nuevo en vivo. Con ella se cierra el paso 13 y el bloque 4. |

**El bloque 4 está cerrado (2026-11-16) y en `main`** (la rama `migracion`, juntada con `main` por avance rápido). **Lo que queda fuera de los pasos y lo que no tiene prueba:**

- **Fuera de los pasos, pendiente:**
  - **centrar el mapa desde una ventana** (el 📍 del prototipo: un mercenario, su objetivo o una misión);
  - **los modelos 3D** del Aeda itinerante y de los carros: hasta tenerlos, cuadros (Víctor);
  - **los pedidos al motor de abajo**, y una orden de administración para colocar Aedas, dar monedas y llenar el fondo de una plaza, que haría
    falta para probar lo de la lista siguiente.
- **Sin prueba en el bloque 4:**
  - ~~**los mercenarios de la región**~~ ✅ 2026-11-16: `TuTaberna` contrata un bot de la región pagando con el fondo de la plaza
    (`dar_fondo_monedas` y `dar_monedas`, motor `ecfb23d`);
  - **el rastreador de misiones en el mapa** (paso 12); en la plaza y en el campamento sí;
  - **los Aedas** (paso 13). Desde el 2026-11-16, `TusAedas` pone un itinerante y un residente con `colocar_aeda` y ve los dos en la ventana. Siguen
    sin prueba:
    - la compra a un itinerante: en una partida nueva no vende nada (Víctor, 3a: se prueba lo que haya);
    - las épicas del residente: salen de las tecnologías de la Facción, y una nueva no tiene;
    - la ficha del itinerante en el mapa;
  - **la crónica** (paso 13): «Más antiguas» y la entrada en vivo;
  - **«Entregar» de una caravana del ejército** (paso 11): haría falta un trueque en vigor con otra Facción;
  - **«Reponer» en la Base** (paso 6): en un mundo recién hecho el campamento aún no recluta.

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

**Pedidos al motor que salgan de la migración** (pedidos al hilo del motor el 2026-11-16: elegir las escuadras en batallas que no son de columna, bajarse sin cancelar, ver cómo acabó al abandonar, el hecho de cierre de la batalla, la tropa del campamento en «Nace tu héroe» y borrar el héroe):

**Los pedidos 1-4, 6 y 7, en el motor** (`ecfb23d`, contrato 0.28.0, 2026-11-16; análisis y tandas en `tasks/motor-ecfb23d-plan.md`):

- ✅ **Elegir las escuadras al entrar** (`OrdenUnirseABatalla.escuadron_ids`), con la regla de Víctor:
  - en la preparación, **disponibles** (todas las tropas si está en su base; si no, las que lleva) y **elegidas** (lo que cabe en el Liderazgo);
  - el héroe entra siempre con las elegidas y en la batalla solo cambia entre ellas;
  - el motor solo deja elegir al defender desde dentro de tu base, y al unirte;
  - ✅ en Conquest desde la tanda 3 (2026-11-16): «Asedio a tu plaza · Defender» y el panel de elegidas; prueba `TuDefensaDeTuPlaza`.
- ✅ **Bajarse de una batalla sin cancelarla** (`OrdenBajarseDeBatalla`), en Conquest desde la tanda 1.
- ✅ **Que quien abandona vea cómo acabó** (`Batalla.salida_propia`), en Conquest desde la tanda 1.
- ✅ **El hecho de cierre** (`heroe.cierre_batalla`), en Conquest desde la tanda 2: el informe saca de él lo que ganas y pierdes y la
  experiencia por escuadra. Quien sale antes del final (abandono, caída, desconexión) no lo recibe: Conquest pone sus
  cifras con el ticket y la escuadra de ahora, sin experiencia, y **se queda así** (Víctor, 2026-11-16: no se pide al motor).
- ✅ **El id de la tropa en `EscuadraQueLleva`.**
- ✅ **Órdenes de administración para las pruebas:** `dar_monedas`, `dar_fondo_monedas` y `colocar_aeda`.

**Decidido el 2026-11-16 (Víctor), para el motor `ecfb23d`:**

- **Defender desde dentro de tu plaza:** un aviso arriba en la plaza, «Asedio a tu plaza · Defender», que abre el panel de elegir (tanda 3).
- **Quien inició la batalla** tiene «Cancelar», que la acaba para todos, y «Bajarse», que se la pasa a otro atacante. Si es el único humano,
  solo «Cancelar».
- **El Aeda de la prueba:** se prueba lo que haya. En una partida nueva el itinerante no vende nada, así que la compra se queda sin prueba.
- ✅ **Cambiar las elegidas ya dentro de la batalla** (`OrdenElegirEscuadrasDeBatalla`, motor c94a960; Víctor, 12a), antes de empezar: «Elegir
  escuadras» en la preparación abre el mismo panel. Corregido al leer el motor: en las batallas de Unity **nadie entra de oficio**, tampoco el
  residente que está dentro (`tests/batallas.rs:1450`); `defensa_de_plaza` es del asedio con números. El residente se une con «Defender».

**Lo que queda de los pedidos de la migración:**

- **A futuro: la tropa del campamento en «Nace tu héroe»** (paso 6, 2026-11-14, duda 34 del plan, opción b): enseñar al elegir campamento qué tropa recluta cada uno. Hoy el motor no la da antes de nacer; no entra en esta migración.

- ~~**Salir del campamento con destino en una sola orden**~~ (paso 2, 2026-11-13): **no se pide** (Víctor, 2026-11-16). Desde dentro de un sitio, el clic en el mapa no hace nada; se sale con la salida de siempre. Fuera, el clic traza la ruta y mueve.
- ~~**Guardar el género y el avatar del héroe**~~ (paso 3, 2026-11-13) ✅ resuelto en el motor 358af5ff (contrato 0.25.0, partida nueva): `crearHeroe` guarda el género (`""`, `masculino` o `femenino`) y las cuatro piezas (ids de hasta 32 caracteres; si no, `heroe.aspecto_invalido`) y los devuelve en `Heroe.femenino` y `Heroe.avatar`. Conquest los manda desde el creador de personaje y la prueba los lee de vuelta. El catálogo de piezas sigue en Conquest (`AvatarPartDatabase.asset`).
- **A futuro, fuera de esta migración: borrar el héroe y crear otro** (paso 3, 2026-11-13, pedido del autor). Hoy el motor tiene un héroe por cuenta y partida y no hay orden de borrarlo. Cuando exista, el selector de personaje recupera en el modo «desde el motor» los botones «Crear héroe» y «Borrar héroe», que ahí quedan ocultos.

## 5. Arte (en paralelo)

| | Estado |
|---|---|
| **Tropas del motor a medias, a la espera de arte** (decisión del 2026-11-13; [`Tropas_Del_Motor.md`](Tropas_Del_Motor.md)): los **carros de guerra** van con el aspecto de la caballería (sus cifras, las del motor) hasta que haya carro; **honderos** y **escaramuzadores, peltastas y agrianos** disparan flechas hasta que haya honda y jabalina; los **inmortales** van solo como arqueros hasta que haya lanza y arco; los **espadachines y hacheros sin escudo** llevan el modelo de los escuderos; las **levas** (leñadores, granjeros, mineros) usan el de los lanceros | ⬜ |
| **Borrar el relleno de las escuadras** (pasado a Arte, Víctor, 2026-11-16, a): las 7 escuadras de `Resources/Squads` menos `BronzeAge/` son las bases visuales (modelo y animación) de las que se copian las tropas del motor; cada una se va cuando su tropa tenga modelo y texturas propios. Los datos de 36 de las 37 tropas del motor ya están en Conquest | ⬜ a medida |
| **El contenido de las Eras I-III** (pasado a Arte, Víctor, 2026-11-16, a): las tropas ya están con sus datos; falta su aspecto propio (caballería, carros, hondas, jabalinas, levas sin formación) y los edificios nuevos | ⬜ con el arte |
| **Inventario de Synty a sustituir**, por categoría (armaduras, armas, edificios, props). Hoy 58 de nuestros 126 prefabs dependen de `Assets/Synty` y no hay ningún modelo propio en `Assets/Art/Models` | ⬜ |
| **Norma para los modelos propios:** mismo estilo y detalle que Synty, sobre su esqueleto, y detrás de los mismos ids para no tocar código | ⬜ |
| **Terreno del ticket:** la batalla en el sitio del mundo (hash del artefacto, centro, paquete de relieve; `06_Contrato_de_Mundo.md` §4 y §8). Va después del mapa de mundo nuevo | ⬜ |
| **CPU del cliente:** con 130 modelos, la red son 13 puntos de 180 y el resto es animar (~83) y pintar (~84). Menos Animators para los lejanos o animación por GPU | ⬜ |
| ~~**Interfaz de Conquest** para el aviso de conexión y el botón de abandonar~~ ✅ 2026-11-16: el aviso es una banda UGUI arriba con la cuenta atrás de la reconexión; se abandona desde las Opciones (Esc), con su confirmación; fuera F10 y el botón aparte (Víctor, 14) | ✅ |
| **Marco de bronce de la interfaz de fuera de la batalla** (UI Toolkit, `Assets/Resources/MundoUI/Mundo.uss`): USS no tiene degradados ni sombras, así que hoy la placa va solo con bordes y sin dentículos. Arte en 9-slice para la placa, el botón de oro y la moldura de dentículo, como la guía «bronce y arcilla» (decisión del 2026-11-13) | ⬜ |

## 6. Decisiones pendientes del autor ❓

- ~~Predecir al héroe del jugador~~ ✅ decidido 2026-11-16 (Víctor, c): sobre la malla de navegación, como está, y además predecir el choque
  con los soldados cercanos (2-3 días), para quitar el tirón al meterse en una escuadra. **Va lo último**, después de lo demás.
- ~~Dos plazos de paso~~ ✅ decidido 2026-11-16 (Víctor):
  - **10 s sin señal** para dar a un jugador por caído: se quedan (`ArranqueRed.cs`, `DriversDeBatalla.PlazoSinSenalMs`);
  - **60 s de espera** a que entren antes de empezar: se quedan (`BatallaDeTicketSystems.EsperaPorDefecto`). Más adelante los mandará el motor
    en el ticket según el tipo de batalla, por ejemplo más para un asedio.
- **Las clases y los objetos del motor en la batalla** (Víctor, 2026-11-16, la a en todas; `tasks/clases-objetos-motor-plan.md`):
  - las cuentas del héroe van en la escala del motor: las piezas tal cual y los atributos recalibrados contra las tropas;
  - los carros de guerra llevan el aspecto de la caballería hasta que haya arte;
  - una clase o un objeto desconocidos, u otra versión de su catálogo, rechazan la batalla con su motivo, como las tropas;
  - se borran las clases y los objetos de relleno y las pantallas viejas de un jugador que solo viven de ellos (con la lista exacta antes);
  - las dagas, con sus cifras y el aspecto de espada y escudo hasta que haya arte.
  - hecho en la batalla (tanda 2): el héroe del ticket con sus atributos, sus piezas (forjadas o de ficha) y su montura, con las reglas del héroe que propuso el autor del motor (vida 1000 + 30 por vitalidad + la salud de la armadura; alivio del 0,25 % por punto de defensa y de armadura; golpe del arma por 1 + 1 % por punto del atributo de la clase).
  - un héroe del ticket sin montura va a pie: no puede llamar al caballo (Víctor, 2026-11-16, 22a).
  - tanda 3 (Víctor, 2026-11-17, 23a y 24a): las 5 clases de Conquest llevan el id del motor; el aspecto y el icono de cada pieza salen de `Resources/Heroe/aspecto.json`; el creador y el selector, solo con el motor; fuera la preparación vieja (`BattlePrepScene`). El inventario, la tienda y la ficha viejos del campamento, con sus 22 piezas de relleno, esperan a que se rehaga el campamento.
- **Orden de trabajo, decidido** (Víctor, 2026-11-16):
  1. la tanda 3 del motor (elegir escuadras al defender desde tu base);
  2. la interfaz de Conquest del aviso de conexión y abandonar, junto con el panel de suministro;
  3. (Víctor, 2026-11-16, a) las 5 clases del héroe con sus atributos desde el `HeroSnapshot`; los objetos del catálogo del motor y las piezas
     forjadas, con sus cifras; los carros de guerra, al menos su dato, para que un ticket con carros no rechace la batalla; rechazar o avisar
     de la clase o el objeto desconocidos (hoy entran sin aviso como espada y escudo, o con el hueco vacío); borrar el relleno del héroe; y
     quitar `terrain-tools` si la batalla no lo usa. El relleno de las escuadras y las Eras I-III van con el arte (§5);
  4. al final, la predicción con choque contra los soldados (c). Hecha (Víctor, 2026-11-17, a): encendida por defecto, `-sinChoque` la apaga. Con soldados quietos o lentos, las correcciones al meterse en una escuadra bajan de 8-41 por segundo a menos de 1; en plena lucha a 200 ms quedan 1,3 por segundo (doc 22 §8.6.1).
- **Pendiente (Víctor, 2026-11-17, 28b): el teclado de prueba en `BattleSceneIntegrationTests`.** En las pruebas de un jugador la tecla no
  llega a la batalla, por lo menos desde el 2026-11-14 a las 19:39 (`Logs/mundo/nuestro4.xml`):
  - «W input did not move the local hero», «Key 1 did not launch the squad's ability», «Key 3 did not launch the Advance» y la del ataque;
  - hoy también `ProductionBattleSceneSpawnsAndAcceptsHeroMovementAndSquadOrders` y `PikemenRecruitmentPreparationAndProductionBattle`
    (esta llega a la batalla con su escuadra y se para en la F2).
  - No es `HeroInputSystem` ni lo que apaga al héroe local en red (`BattleBootstrapRequests.NoLocalHero`). Falta buscar la causa común.
- **Fallo intermitente (2026-11-17): `DelMapaALaBatalla`.** Falló dos de tres vueltas seguidas, cada vez en un sitio: de la preparación cancelada
  al mapa («de vuelta en MundoScene con el mapa») y del informe al mapa con «Salir» («de vuelta en el mapa tras el informe»; el clic, con el
  ratón de verdad, no llega). La tercera pasó. Ninguno toca el movimiento del héroe; puede ser lo mismo que el teclado de prueba (28b).
- **Fallo intermitente (2026-11-17): `TuPlazaPorDentro` a 640×480.** Con el PC cargado (los dos lotes a la vez), el interruptor «pausar-pan» del
  panel de gobierno queda fuera de la pantalla (y = −260): el desplazamiento de la ventana a ese tamaño. Falló una de cada dos vueltas; no es el
  reloj de prueba (`AvanzarReloj`, ya arreglado).
- El motor en local es solo para las pruebas: irá en la nube.

## 7. Sueltos

- ~~En la escena de un jugador (BattleScene) los arqueros no disparan~~ ✅ 2026-11-16: `ProjectileSpawnSystem` crea la reserva de
  proyectiles con el primer disparo en cualquier escena (prueba en BattleScene).
- ~~En la batalla en red faltan cambiar de escuadra e interactuar, y el héroe apunta con el movimiento~~ ✅ 2026-11-16: F abre el panel de
  suministro en un punto propio, el cambio va al servidor y el héroe encara la cámara también quieto (prueba en `DelMapaALaBatalla`, con un
  ticket de dos escuadras elegidas).
- ~~**El panel de suministro de la batalla**~~ ✅ 2026-11-16: solo se eligen las escuadras que se pueden sacar; los huecos vacíos se ven, apagados y sin «+» (la zona de tropa nunca queda vacía); «Aceptar» sale al elegir
  una (Víctor, 15a), no apagado; sin escuadras que sacar, ni el aviso de F ni el panel.
- ~~**En red, las órdenes, las habilidades y las formaciones** podían llegar con el tipo, la ranura o la formación a 0~~ ✅ 2026-11-16 (Víctor):
  el aviso del comando llega en su tick, pero el dato era el del último fotograma de ese tick. El cliente manda ahora en cada comando el
  último dato de cada aviso. Probado en red: la Q, «mantener» y «seguir». Las levas tienen una sola formación: F1-F4 van por el mismo camino.
- ~~Unity Transport 6.5 en Windows deja sordo al servidor cuando muere el proceso de un cliente en una máquina que contesta
  «puerto cerrado»~~ ✅ 2026-11-17: arreglado en el Transport embebido (`Packages/com.unity.transport`): la recepción fallida
  (el ICMP vuelve como WSAECONNRESET, `SIO_UDP_CONNRESET`) devuelve su búfer a la cola. Quitado el margen de 16.384 de
  `DriversDeBatalla`. Prueba `ClienteMuertoTransportTests` (roja sin el parche) y `Tools/cliente_muerto.sh`. Ver doc 22 §9.6.
- Probar con alguien al teclado, en el cliente con ventana, el retardo, las salidas y la reaparición (solo lo han probado
  bots).
- ~~Un ticket que vuelve con la revisión subida después de una incorporación pierde las incorporaciones~~ ✅ 2026-11-16: no pasa. Con la
  revisión subida el motor vuelve a mandar el ticket y **todas** las incorporaciones vigentes (`sesiones.rs`, `publicar_batallas`: desde 0), el
  lanzador las reenvía tras el ticket nuevo y el servidor de Conquest las vuelve a incorporar. Probado el ticket revisado en `TuDefensaDeTuPlaza`.
- ~~La XP y el botín los pone el motor (D6, D7); en Conquest queda quitar el cálculo local~~ ✅ 2026-11-16: borrados los sistemas de
  experiencia de un jugador y sus pruebas (no había botín local).

## Historial

- 2026-11-13: creado al cerrar la red. Absorbe lo vivo del modelo de cooperación (las propuestas CQ-001 a CQ-011: lo hecho
  está en el bloque 1; CQ-006 a CQ-009 están en los bloques 3 y 4) y las decisiones del autor de ese día.
- 2026-11-13: inventario de BronzeAgeClient ([`Inventario_BronzeAgeClient.md`](Inventario_BronzeAgeClient.md)) y orden de la
  migración aprobados por el autor: el bloque 4 pasa a ser la lista ordenada de 13 pasos.
