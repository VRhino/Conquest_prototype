# Paso 8: Facción, tecnología, sigilo y estandartes (análisis y dudas)

2026-11-15. Víctor contestó: las dudas van como se propusieron. La diplomacia, entera en la tanda 4. El sigilo también como textura
para el 3D (duda 1: la misma textura vale para la tela del estandarte). Duda 9: el cambio del motor (el `Sigilo` en el ticket de batalla)
lo hace otra sesión; Conquest pone el estandarte sobre el héroe. Ampliación de Víctor (13:31), para la tanda 4: las banderas de las zonas
de captura llevan el sigilo del bando que tiene la zona; cada escuadra lleva su estandarte con el sigilo; y ver el estandarte de las tropas
es una opción de los ajustes del juego, encendida por defecto.
Tandas 1, 2 y 3 aprobadas (sin commit); tanda 4 hecha (a revisar): `Docs/Arquitectura/31_Faccion_Y_Sigilo_2026-11-15.md`. Traspaso a una sesión nueva: `tasks/traspaso-sesion-nueva.md`.

## Qué abarca

La hoja de ruta (bloque 4, paso 8): «Facción, tecnología, sigilo y estandartes. El sigilo, compuesto por ids, va también en la batalla
(antes CQ-009)» (§3.11 y §3.12 del inventario). Además, lo apuntado para aquí:
- el fondo de refundación del campamento y su caravana, solo con Facción y sin plaza (pendiente del paso 4, doc 26);
- el blasón y el sigilo en el selector de personaje y en el HUD de la plaza, que hoy son texto y color (docs 25 y 30);
- el sigilo de las plazas en el mapa y el color de Facción del sigilo en lugar de la paleta de reserva (docs 24 y 27);
- «Adoptar» en el árbol de reclutamiento (duda 31 del paso 6 y duda 7 del paso 7);
- los cargos de Facción en la Base (paso 6: «los cargos y la Facción, solo si los tiene»);
- lo que falte de tecnologías para desbloquear tropas.

Capturas del prototipo y de lo que hay en Conquest: en el informe del paso 8.

## Lo que hay

### En BronzeAgeClient

**La ventana de Facción** (`main.ts:1835-1900`, `ui/pestanaFaccion.ts`), desde el botón «Facción» de la barra del jugador.
- Sin Facción: «Elige tu facción» con dos tarjetas, «↗ Pedir ingreso» y «✦ Crear facción».
  - Crear: el nombre (32 letras), el selector de sigilo y «Crear facción». La validación sale antes de pulsar: nombre vacío o
    repetido, sigilo incompleto, fondo de un solo color, sigilo repetido. Orden `crearFaccion {nombre, sigilo}`.
  - Pedir ingreso: buscador, «Ocultar las de bots» y la lista de Facciones (las conocidas con su escudo y sus datos, las «Sin contacto»
    más pequeñas). Orden `solicitarIngreso {faccionId}`.
- Con Facción, a pantalla completa:
  - Lateral fijo: el sigilo grande con el medallón de nivel, el nombre, la experiencia con su barra, la franja (miembros, prestigio,
    reputación), lo que da el prestigio, el Rey y el Embajador, la Liga, los cupos (plazas y niveles ② a ⑤), los títulos del servidor y
    «Dejar la Facción».
  - Pestañas: Miembros, Plazas, Ejércitos, Tecnología, Tablón, Misiones, Mercenarios, Diplomacia, Ajustes del Rey.
    - Miembros: las solicitudes con Aceptar o Denegar (solo el Rey, `responderSolicitud`); designar Embajador y traspasar el trono
      (`asignarEmbajador`, `asignarRey`); la lista de miembros por participación, con sus cargos y su residencia; `dejarFaccion`.
    - Plazas: las propias, primero la capital, con «Ver en el mapa» y un desplegable con población, almacén, obras y guarnición.
    - Tecnología: el árbol (abajo).
    - Diplomacia: la lista de Facciones y la ficha de la elegida; alianza, vasallaje con tributo, guerra, paz, romper, rebelión,
      anexión y fusión (unas 14 órdenes).
    - Ajustes del Rey: «Ataques abiertos a otras Facciones» (`admitirOtrasFacciones`).
- No hay tesoro de Facción, ni orden para cambiar el sigilo o el nombre, ni para invitar o expulsar.

**La tecnología** (`ui/panelTecnologia.ts`, `ui/arbolTecnologias.ts`, sobre el tablero del árbol de tropas `ui/arbolTablero.ts`).
- Carriles (Gobierno y obras, Infantería, Metalurgia, Proyectiles, Caballería, Ingeniería y asedio, Otras) × Eras. Las Eras se
  descubren de una en una, al adoptar el 30 % de la actual.
- Estados de carta: adoptada ✓, adoptable !, revelada por un Aeda ♪, logro ✦, bloqueada, futura. Cada carta dice lo que abre (tropas,
  edificios, niveles, recetas, metal); el letrero trae requisitos y «Abre».
- Única orden: `adoptarTecnologia {faccionId, tecnologiaId}`, del Rey dentro de la capital, con la tarifa de la Era del almacén de la
  capital. Debajo, «Logros del mundo cumplidos».
- En el árbol de tropas, «Adoptar» es solo una etiqueta, no un botón: «Solo falta adoptar X: … el Rey puede adoptarla en la capital».

**El sigilo** (`sigilo/`).
- Son 8 ids: forma (6), campo (18), color primario y secundario, emblema (56, de game-icons.net, CC BY 3.0, con sus créditos), color del
  emblema, orla (6) y su color. Hay 12 colores. El catálogo es copia del motor (`catalogoSigilos.json`, versión 4).
- Se dibuja en SVG (`svgSigilo`, lienzo de 100×120): silueta recortada, campo, orla, contorno (dorado y con corona si es Gran Rey) y el
  emblema con un halo de contraste. En el mapa 2D se rasteriza a imagen.
- Se elige al crear la Facción («no se podrá cambiar»): una vista previa y 8 desplegables, con un sigilo al azar para empezar.
- Dónde sale:
  - el lateral de Facción (168 px);
  - las listas y fichas de Diplomacia (26 a 72 px);
  - el blasón del HUD de la plaza (64 px);
  - las plazas del mapa (40 px en 3D, 26 en 2D);
  - las columnas y caravanas (26 px);
  - la etiqueta de ciudad, la taberna y el selector de partidas (30 px).
- El color de Facción es el primario del sigilo (`faccionColor`): fronteras, rutas, nodos.

**El fondo de refundación** (`ui/pantallaCampamento.ts:52-67, 348-404`).
- Una subpestaña «Fondo» del campamento, solo con Facción y sin ninguna plaza suya a la vista.
- Muestra el precio de la caravana, reunido contra total, con una fila por recurso.
- Botones «Aportar» y «Retirar» (del almacén o del carro) y «Comprar caravana».
- Órdenes `aportarARefundacion`, `retirarDeRefundacion` y `comprarCaravanaDeRefundacion`.

**La batalla:** el prototipo no pinta estandartes; solo el nombre de la Facción en texto.

### En el motor (cc1e5ec)

- `Faccion`: lleva el `sigilo`, el Rey y el Embajador, el nivel y la experiencia, los ciudadanos y las solicitudes, la reputación y el
  prestigio, la Liga, los miembros con sus cargos (`rey`, `embajador`, `gobernador@as-3`…), los cupos y `cap_fundacion`.
  - De una Facción ajena llega su forma pública en `VISTA`.
  - `DirectorioFacciones` (en `VISTA`) trae todas las Facciones con su sigilo: es la fuente del sigilo de los marcadores del mapa.
- `TecnologiasFaccion` (`aparecidas`, `adoptadas`, `reveladas`) y `EstadoTecnologia` (la Era del mundo). Lo que abre cada tecnología
  viene en los datos (`requiereTecnologia`, `TROPAS_RECLUTABLES[..].tecnologia`).
- El fondo de refundación es `CampamentoMercenarios.fondos[f-N]` (ámbito `CAMPAMENTO`); la caravana, una `Caravana` de tipo fundación.
- **El ticket de batalla no lleva el sigilo**: solo `faccion_id` en cada bando y en cada participante. `SquadSnapshot.estandarte` viaja
  vacío a propósito (decisión D20: no hay estandarte en el catálogo).

### En Conquest

- No hay ventana de Facción. La barra del jugador solo tiene la Base y los Avisos.
- La Facción se lee para su nombre, su nivel, las relaciones y la capital.
- Su color sale de una paleta de reserva por el número de `f-N` (`CosasDelMapa.ColorDeFaccion`).
- El blasón de la plaza es un escudo de ese color (`PantallasDeMundo.Plaza.cs:568-572`).
- El selector de personaje pone el nombre de la Facción en texto (`HeroSelectionSceneController.UI.cs:84`).
- Las frases de los rechazos (`faccion.*`, `faccion.sigilo_*`, `tecnologia.*`) ya están en `Textos.cs`.
- Las tecnologías adoptadas y aparecidas ya bloquean:
  - las tropas en el árbol de la plaza (estado «adoptable», pintado como bloqueada);
  - «Mejorar» de un edificio;
  - las formaciones.
- `ArbolDeTropas` ya monta el grafo de tecnologías y Eras.
- El árbol del campamento dice «Sin Facción ni tecnologías (paso 8)».
- En batalla no hay nada: ni sigilo en el ticket ni en los componentes de red; solo `Bando`.
- Hay arte de Synty: banderas, estandartes de batalla y mástiles (`PolygonFantasyKingdom/Prefabs/Props/Banners/`, materiales
  `Mat_Flag_Symbol_*`), con símbolos fijos.
- El paquete de gráficos vectoriales ya está (`com.unity.modules.vectorgraphics`): los iconos del mundo son SVG.

## Tandas (con parada en cada una)

1. **El sigilo y crear tu Facción.**
   - El sigilo en Conquest: el catálogo del prototipo (con los créditos de los emblemas), dibujado igual y guardado en caché por sus ids.
   - Dónde va: el blasón del HUD de la plaza, el selector de personaje, las plazas, columnas y caravanas del mapa, y el color de Facción
     sacado del sigilo.
   - El botón «Facción» en la barra del jugador y la ventana sin Facción: «Elige tu facción», crear con el selector de sigilo y su
     validación, y pedir ingreso con la lista de Facciones.
2. **Tu Facción.**
   - El lateral entero.
   - Las pestañas Miembros (solicitudes, Rey y Embajador, la lista, dejarla), Plazas y Ajustes del Rey.
   - Los cargos de Facción y el sigilo en la Base.
   - El fondo de refundación del campamento y su caravana.
3. **La tecnología.**
   - La pestaña Tecnología: el árbol por carriles y Eras, sobre el mismo tablero del árbol de tropas, con «Adoptar» (el Rey, en la
     capital) y los logros del mundo.
   - En el árbol de tropas, «adoptable» con su etiqueta y su frase, y fuera el «Sin Facción ni tecnologías» del campamento.
4. **Diplomacia y estandartes.**
   - La pestaña Diplomacia: la lista y la ficha, alianza, vasallaje, guerra y paz, anexión y fusión, la Liga y los títulos.
   - Los estandartes con el sigilo en la batalla (duda 9).

Fuera de este paso: Ejércitos (paso 11), Misiones y Tablón, Mercenarios y Aedas, cada uno con su paso. Sus pestañas no salen hasta
entonces. En las pruebas con plaza, la Vía Rápida desde el principio (`ConViaRapida`).

## Dudas (con mi propuesta)

1. **Cómo dibujar el sigilo.** Propongo montar en Unity el mismo SVG que el prototipo (silueta, campo, orla, contorno y emblema) y leerlo
   con el paquete de gráficos vectoriales que ya está. Saldría una textura por sigilo, guardada por sus 8 ids, que valdría igual para UI
   Toolkit, para la UI de siempre del selector y para la tela de un estandarte en 3D. La otra opción es dibujarlo a mano con `Painter2D`,
   pero los recortes por silueta y los 18 campos costarían mucho más. ¿Bien?
2. **Los emblemas** son de game-icons.net (CC BY 3.0). Propongo traerlos con sus créditos y un enlace «Créditos» en el selector de
   sigilo, como el prototipo. ¿Bien?
3. **El mapa.** Propongo como el mapa 3D del prototipo: el sigilo en el cartel de cada plaza (40 px) y en el de columnas y caravanas
   (26 px), y el color primario del sigilo en las zonas, fronteras y rutas, en lugar de la paleta. Los campamentos de bandidos y
   mercenarios siguen con su texto y su color. ¿Bien?
4. **El selector de personaje.** Propongo el escudo de 30 px junto a la línea de la Facción, como la pantalla de partidas del
   prototipo, y sin Facción como ahora. ¿Bien?
5. **Las pestañas de Facción.** Propongo en este paso Miembros, Plazas, Tecnología, Diplomacia y Ajustes del Rey; Ejércitos, Tablón,
   Misiones y Mercenarios, con su paso y sin salir hasta entonces. ¿Bien?
6. **El botón «Facción»** en la barra del jugador, con el icono de bandera, como el prototipo. ¿Bien?
7. **«Adoptar» en el árbol de tropas.** El prototipo solo pone la etiqueta y la frase, y se adopta en la pestaña Tecnología. Propongo
   lo mismo, con la etiqueta como acceso directo que abre la pestaña Tecnología en esa carta. ¿O sin acceso, como el prototipo?
8. **El árbol de tecnologías.** Propongo el mismo tablero que el árbol de tropas (arrastrar, la ficha fija al pulsar, «Ver todas las
   conexiones»), con los carriles y las Eras del prototipo. ¿Bien?
9. **Los estandartes en la batalla.** El ticket no lleva el sigilo, y el servidor de batalla no tiene la sesión del mundo para
   buscarlo. Propongo:
   - **El motor:** que añada el `Sigilo` a `BattleSide` y a `BattleParticipantSnapshot`. Es el mismo cambio de contrato que pidió
     CQ-009; lo haría otra sesión, como el de la Vía Rápida.
   - **Conquest:** el estandarte de batalla de Synty sobre el héroe, con la tela pintada con el sigilo (en una copia del material, con
     `MaterialPropertyBlock`). Los bandidos, con el sigilo neutro.
   - **¿Dónde más?** ¿Uno por escuadra, con un abanderado? ¿Y en las banderas de las zonas de captura, con el sigilo del dueño?
10. **Diplomacia.** Es grande (unas 14 órdenes). Propongo calcarla entera en la tanda 4. ¿O la partimos y la dejamos para otro paso?
11. **El fondo de refundación** va en la tanda 2 como subpestaña «Fondo» del interior del campamento, solo con Facción y sin plaza,
    como el prototipo. ¿Bien?
12. **Los cargos de Facción en la Base.** Propongo, bajo el nombre del héroe en el lateral, el sigilo pequeño de su Facción y sus cargos
    («Rey», «Embajador», «Gobernador de Tirinto»), solo si los tiene. ¿Bien?
13. **Las pruebas.**
    - Crear la Facción con su sigilo desde la ventana, con una cuenta nueva.
    - Pedir ingreso y aceptarlo con dos cuentas.
    - Adoptar: una tecnología que aparezca con `fijar_tecnologias` y el Rey en su capital.
    - Diplomacia con dos Facciones.
    - El fondo, con una Facción sin plaza.
    - Todo con ratón y teclado reales y la Vía Rápida en toda prueba con plaza. ¿Bien?
