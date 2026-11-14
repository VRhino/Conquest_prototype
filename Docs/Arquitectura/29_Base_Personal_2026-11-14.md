# 29. La base personal (bloque 4, paso 6, tanda 1)

2026-11-14. Calco de la «Base personal» de BronzeAgeClient (`ui/basePersonal.ts`, `ui/panelCarro.ts`) en UI Toolkit, encima del mapa o del
interior del campamento. Las dudas y lo que contestó Víctor están en `tasks/bloque4-paso6-plan.md`.

## Qué hay

**La ventana.** Se abre con el yelmo de la barra de arriba o con la tecla B (no mientras se escribe en un campo ni con otro panel del
campamento abierto) y se cierra con «Cerrar» o Esc. Ocupa la pantalla bajo la barra. Mientras está abierta:
- la barra solo enseña la Base y los Avisos;
- en el interior, el héroe no anda y el ratón queda libre (`InteriorParaVer.PanelAbierto`);
- en el mapa, ni el ratón ni el teclado mueven la cámara (`MapaDeMundo.EntradaApagada`).

**La barra del jugador** (`ui/barraJugador.ts`): el yelmo de la Base y la campana de Avisos en iconos redondos, con la cuenta de los no
leídos en una pastilla. Nodos, Fertilidad, Marcha forzada, Cambiar de partida y Cerrar sesión pasan a un menú (☰). Los iconos son la
familia `barra` de `IconosDelMundo`, sacada por el generador de la tabla `ICONO` del prototipo; `menu` lo dibuja el generador.

**Segunda forma (Víctor, 2026-11-14, tras ver la primera).** Tres pestañas centradas arriba del todo, la activa subrayada en oro: Héroe,
Tropas y Bienes. Las demás pestañas del prototipo salen; el lateral de la primera forma desaparece.

**Héroe** (calco del detalle del héroe de Conquest, `UIHero_Detail`).
- A la izquierda:
  - el nombre, la clase, y el nivel con la experiencia y su barra (la curva del motor, `PROGRESION_HEROE`);
  - los atributos con los iconos de Conquest: Liderazgo, Fuerza, Destreza, Armadura y Dureza (Dureza es la vitalidad del motor). Son los
    `atributos_base` del motor: la base de su clase más los puntos repartidos;
  - «Puntos disponibles»;
  - debajo, lo que iba en el lateral del prototipo: dónde está, herido, 💰 monedas, víveres, y 🏕️ residencia con «Ver en el mapa».
- En el centro, el héroe en 3D sobre el fondo de la ventana (textura con transparencia), de frente y girable arrastrando.
- A la derecha, el equipo puesto: las seis casillas de Conquest en dos columnas, más la montura del motor.
  - Cada casilla tiene el fondo de su rareza y la línea de su color, como `Inventory_Item_Cell`.
  - La imagen es provisional (la de su tipo en el prototipo) y lleva el nombre del motor.
  - La rareza sale del `tier` del objeto (1 común … 5 legendario): el motor no tiene rareza.
  - El tooltip lleva el nombre, la rareza y las estadísticas.
- Sin «Guardar», «Cancelar», «More», «Free Reset», «Repair» ni «Repair All»: repartir puntos y equipar son de la tanda 2.

**Tropas** (disposición de la captura de inspiración de Víctor; datos de `UISquadDetail`).
- A la izquierda:
  - los filtros por tipo (todas, infantería, a distancia, caballería) con iconos de tropa del prototipo;
  - los filtros por rareza: cuadros con los colores de `SquadUtils`.
- La rejilla:
  - el buscador;
  - las tarjetas de tropa de Conquest en 4 columnas, con los huecos vacíos apagados hasta llenar las filas (mínimo dos);
  - «Ordenar» (orden de combate, Liderazgo, rareza o nombre; solo cómo se ven: el orden de combate no se cambia aquí);
  - «Reponer las prestadas».
- En el centro, un soldado de la tropa elegida en 3D, sobre el fondo de la ventana y girable. Es el prefab visual de su tropa
  (`VisualPrefabConfig`), sin el cubo marcador de la batalla.
- A la derecha, dos subpestañas:
  - **Características:**
    - nombre, tipo y estrellas, con las marcas prestada o en guarnición;
    - las cifras con icono: Liderazgo, hombres, moral, y nivel con la experiencia y su barra;
    - «Formaciones»: botones con el icono de cada formación de su tropa en Conquest que permite el motor. La elegida va marcada y, al pulsar
      otra, se guarda con `elegirFormacion`;
    - «Órdenes»: las habilidades de Conquest, solo para ver;
    - «Dónde está»;
    - «Pagar con» y «Reponer» con su coste, solo donde se puede.
  - **Atributos**, los del panel del barracón:
    - Básicos: vida, hombres, velocidad, Liderazgo, alcance y munición;
    - Ataque: penetración y daño perforante, cortante y contundente;
    - Defensa: las tres, bloqueo y recuperación del bloqueo.
    - Las cifras son las de su tropa en Conquest, las que usa la batalla. Lo que no aplica sale con «-».
  - Sin «Rasgos»: ni el motor ni las fichas de Conquest los dan como lista.

Los modelos 3D son `ModeloDeLaBase`: lejos de todo, en su capa (`hero_preview` o `squad_preview`), con su cámara (solo esa capa) y su luz.
Se quitan al cambiar de pestaña. MundoScene enlaza `PantallasDeMundo.PrefabsVisuales` con `ScriptableObjects/VisualPrefabConfig`.

**Bienes** (Víctor, 2026-11-14). Dos paneles; el de la izquierda, más ancho.

**Izquierda, el almacén personal**, en dos subpestañas:
- **Objetos.** El almacén de objetos (`almacen_objetos`), con las 144 casillas de `OBJETOS.casillasAlmacen`, las vacías a la vista.
  - Cada objeto lleva el fondo y la línea de su rareza (por su tier), una imagen provisional y su cantidad. Su ficha sale al pasar el ratón.
  - Al pulsar uno, debajo sale su nombre y «A la mochila» (`sacarObjeto`).
- **Recursos.** El almacén personal de recursos con su barra y «Al carro».

**Derecha**, en dos subpestañas:
- **Mochila.** La mochila (`inventario`), con sus 72 casillas y «Al almacén» (`guardarObjeto`).
- **Carro.**
  - «Pasar a víveres», solo para el Líder;
  - el carro con su barra verde, ámbar o roja;
  - «Al almacén personal» y «Todo al almacén personal»;
  - lo aparcado en la puerta.

Las dos órdenes de objetos solo valen dentro de tu residencia (`exigir_dentro_de_su_residencia`): fuera, el botón no sale, sin mensaje
que lo explique (duda 20). Equipar es de la tanda 2.

Lo de la plaza (`dejarEnPlaza`, `cargarDePlaza`, cerrar el almacén) es del paso 7.

**«Salir al mundo».** Cada tarjeta del riel lleva ◀ ▶ para el orden de combate. Al salir, si las que salen no están así a la cabeza del
héroe, se manda antes `ordenarEscuadras` con ellas en el orden del riel (el resto, detrás). Un rechazo se enseña y no se sale. El límite
del carro y la línea de velocidad ya estaban (doc 26).

**Repintado.** La Base se repinta sola cuando cambia la versión del héroe, de sus escuadras, de su columna o de su campamento. Con un
número a medio escribir, no se repinta. Tras una orden aceptada, solo se repinta si algo cambió: un repintado sin motivo se llevaba el
clic que estuviera a medias.

## Barras de desplazamiento (Víctor, 2026-11-14)

Regla para toda la interfaz del mundo: si el contenido cabe, no hay barra. Si de verdad no cabe, la barra es estrecha y forma parte del diseño:
- 6 px de ancho, sin flechas;
- el riel casi invisible y el asidero del bronce del marco, redondeado.

Va como estilo común en `Mundo.uss` (selectores bajo `.unity-scroll-view`), así que la heredan todas las pantallas, también las nuevas.

En la Base:
- las rejillas de Bienes ajustan el tamaño de sus casillas al panel (16 columnas para las 144 del almacén, 9 para las 72 de la mochila), y
  caben sin barra a los cuatro tamaños;
- la prueba comprueba que no aparece ninguna barra en Héroe, Tropas, Atributos, Bienes y Carro.

Donde sí queda una barra, como en la lista de tropa del edificio militar del campamento, que llega en la tanda 3, sale la estrecha.

## Pruebas

`PantallasDeMundoTests.LaBasePersonal` (PlayMode, servidor de prueba propio, teclado y ratón del Input System):
- nace un héroe y se le dan oro, trigo, una prestada y una suya mermada (`dar_escuadra`);
- nace un héroe con aspecto (pelo y cejas);
- B abre la Base en el interior, con la barra solo con Base y Avisos;
- Héroe: los cinco atributos (Dureza = vitalidad del motor), las siete casillas, el héroe de frente y con su pelo, y que gira al arrastrar;
- Tropas: las dos filas con huecos vacíos, la tropa en 3D sin el cubo, la formación pulsada y guardada en el motor, los atributos, y el filtro a distancia;
- los modelos tienen fondo transparente (alfa 0 en la esquina) y se quitan al cambiar de pestaña;
- «Reponer» solo si el campamento la recluta: en un mundo recién hecho aún no recluta nada, así que no sale y no se prueba aceptado;
- Bienes: las 144 y 72 casillas; unas botas quitadas (`desequiparObjeto`) pasan de la mochila al almacén con el ratón; Recursos al carro, y «Todo al almacén personal» en Carro;
- la ventana de Héroe es opaca a los cuatro tamaños (un punto de su fondo tiene el color del fondo, sin restos del fotograma anterior);
- Esc cierra;
- en «Salir al mundo», ▶ cambia el orden y el motor lo guarda al salir;
- en el mapa, el yelmo la abre (con el mapa quieto), «Cerrar» la cierra y el menú tiene lo demás;
- capturas de Héroe, Tropas (las dos subpestañas), Bienes y «Salir al mundo» a 1280×720, 1600×900, 1920×1080 y 1950×1130.

En modo batch la ventana no tiene el foco, y el sistema de eventos propio de UI Toolkit no recibe el ratón: se midieron 0 pulsaciones.
MundoScene no tiene EventSystem. Por eso, en la parte del mapa la prueba pone uno con el módulo del Input System, como el del interior.
Fuera de la prueba, con la ventana enfocada, no hace falta.

**El ratón que no llegaba (resuelto).** Con lo de otra prueba ya cargado, se llega al interior mientras la pantalla de carga aún se
desvanece, y esa pantalla seguía tapando el ratón durante su medio segundo de salida. Se veía la ventana de la Base medio transparente
sobre la imagen de carga, y la prueba medía 0 eventos.

Ahora la pantalla de carga deja pasar el ratón en cuanto empieza a irse (`PantallaCarga.Cerrar`), y la prueba espera a que se vaya.
También se arreglaron, aunque no eran la causa:
- las pantallas del mundo quitan su mapa y sus PanelSettings al destruirse;
- cada textura de captura se limpia al crearla.
