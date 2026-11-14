# Paso 6: base personal y reclutamiento (análisis y dudas)

2026-11-14. Solo análisis: no hay nada tocado en Assets ni commits. Queda así hasta que Víctor conteste las dudas.

## Qué abarca

La hoja de ruta (bloque 4, paso 6) lo resume como «héroe, equipo, tropas, carro y el árbol de tropas». Corresponde a las secciones §3.9 y
§3.10 del inventario. Además, lo que dejamos apuntado para este paso:
- el equipo puesto de verdad en el héroe 3D (doc 25; `VestidoDelMotor` solo pone las piezas de inicio de su tipo de armadura);
- el arma del héroe en la batalla, que aún sale de `TestHeroPool` (doc 28; `BatallaRealDeTicket`);
- el tipo de armadura, que hoy se saca del id de la pieza por convención (`TipoDeArmadura`);
- las barracas de Conquest adaptadas al reclutamiento del campamento (doc 26);
- el árbol de tropas;
- la ficha de detalle de tropa con las estrellas (doc 27);
- la tropa del campamento en «Nace tu héroe» (doc 27);
- lo que lleva el héroe en una ventana propia (doc 26);
- el límite del carro y la línea de velocidad en «Salir al mundo» (doc 26).

## Lo que hay

### En BronzeAgeClient

**La base personal** (`ui/basePersonal.ts`, 458 líneas)
- Es una ventana a pantalla completa. Se abre con el yelmo de la barra del jugador o con la tecla B, y se cierra con Esc.
- A la izquierda, un lateral del 25 % con:
  - el retrato con el nivel, el nombre y la clase;
  - dónde está, su Facción y si está herido;
  - la barra de experiencia, el Liderazgo, «N puntos sin gastar», las monedas y los víveres (con cuánto aguantan);
  - «Resides en» con «Ver en el mapa», y sus cargos.
- El resto son pestañas.

**Las pestañas**
- **Tropas:** la lista con ↑↓, la formación, la moral, la experiencia, dónde está, «Reponer» y la guarnición.
  - Órdenes: `ordenarEscuadras`, `elegirFormacion`, `reponerPrestamo`, `reclutarEnCampamento`, `reclutarTropa`, `asignarGuarnicion` y
    `retirarGuarnicion`.
- **Equipo** (`objetosHeroe.ts`, 337 líneas):
  - los 7 huecos, con montura;
  - la mochila de 72 y el almacén de objetos de 144;
  - la ficha de la pieza con su daño, penetración y defensa;
  - las acciones solo donde se pueden.
  - Órdenes: `equipar`, `desequipar`, `guardar`, `sacar` y `usarObjeto`.
- **Bienes** (`panelCarro.ts`): el carro con su barra, el almacén personal, «Todo al almacén personal» y pasar a víveres.
  - Órdenes: `guardarEnAlmacenPersonal`, `sacarDelAlmacenPersonal`, `vaciarCarro` y `pasarAViveres`. Las de la plaza (`dejarEnPlaza`,
    `cargarDePlaza` y `cerrarAlmacenACiudadanos`) son del paso 7.
- **Loadouts y Héroe** (`panelHeroe.ts`):
  - los atributos, con «Repartir puntos» (`repartirPuntos`);
  - los loadouts, con su editor y su Liderazgo (`guardarLoadout` y `borrarLoadout`);
  - la ficha de mercenario, que es del paso 12.
- **Historial y Plazas conocidas:** solo lectura.

**Lo demás**
- La barra del jugador (`barraJugador.ts`): Base, Facción, Misiones, Avisos, Crónica y Mapa.
- Los estados (`estados.ts`): Herido, Saliendo, Moral baja, Plaza ocupada y Hambre.
- El árbol de reclutamiento (`arbolReclutamiento.ts` y `arbolTablero.ts`, unas 770 líneas):
  - un carril por edificio (Campamento, Barracón, Galería de tiro, Caballerizas) y una columna por Era;
  - cartas unidas con líneas, un tooltip con los requisitos y la ficha de combate;
  - en el campamento, «Pedir prestada» y «Reclutar».
- La armería de la plaza (`armeria.ts`) es del paso 7.

### En Conquest (UGUI, con datos del guardado local, apagado en el modo «desde el motor»)

- **Detalle del héroe** (`HeroDetailUIController`, en FeudoScene), visto en la captura 60:
  - a la izquierda, el nombre, la barra de nivel y los atributos con su icono: Liderazgo, Fuerza, Destreza, Armadura y Dureza;
  - «More >>>» y «Free Reset»;
  - en el centro, la vista 3D del héroe;
  - a la derecha, los 6 huecos del equipo y «Repair» / «Repair All».
- **Inventario** (`InventoryPanelController`), visto en la captura 62:
  - a la izquierda, «Inventory» con tres filtros, una rejilla de 8×9, «Sort», las monedas (bronce, plata y oro) y «Exit»;
  - a la derecha, el mismo panel de equipo.
- **Barracas** (`BarracksMenuUIController`, `SquadSelectionPanel`, `SquadDetailPanel`, `SquadOption`): la lista de tropas para reclutar y la ficha
  de la escuadra. No salió en la captura de esta pasada: la saco en su tanda.
- **Lo que ya hay para el motor:**
  - `PiezasDeTropa` (la miniatura, la tarjeta y las estrellas);
  - `TropasDelMotor`: las 37 tropas del motor como `SquadData`, así que el casamiento tropa ↔ `SquadData` que pedía la nota ya existe;
  - `VestidoDelMotor`.
- **No hay árbol de tropas.**

### En el motor

Los datos:
- `Heroe`: `equipamiento` (7 huecos), `inventario` (la mochila), `almacen_objetos`, `almacen_personal`, `atributos_base`,
  `puntos_de_atributo_sin_gastar`, `loadouts`, `monedas`, `viveres`, `herido_hasta_ms` y `plazas_recordadas`.
- `Escuadron`: `moral`, `experiencia`, `nivel`, `formacion`, `contenedor` y la guarnición.
- `CampamentoMercenarios.reclutables`.

Las órdenes y sus rechazos están en `Heroe_Y_Tropa.md` (líneas 221-410), `Movimiento_Ejercitos.md` (línea 190) y `Campamentos_Mercenarios.md`.

El catálogo de objetos tiene unos 136: 87 armaduras `{hueco}_{peso}_{metal}`, 30 armas `{clase}_{metal}`, 5 monturas, 5 materiales y
9 consumibles. Conquest tiene unos 27 `ItemDataSO`.

## Plan por tandas (con parada en cada una)

- **Tanda 1: la base personal.**
  - La ventana a pantalla completa con su lateral.
  - La barra del jugador (B).
  - Las pestañas **Tropas** (orden, formación, reponer en el campamento) y **Bienes** (carro, almacén personal, víveres, vaciar).
  - El límite del carro y la línea de velocidad en «Salir al mundo».
- **Tanda 2: el equipo y el héroe.**
  - Los objetos del motor en Conquest (catálogo y miniaturas).
  - La pestaña **Equipo** con la mochila y el almacén de objetos, y sus acciones donde se puedan.
  - Vestir al héroe 3D pieza a pieza: en el selector, el campamento y la batalla, con el arma de verdad.
  - **Héroe:** los atributos y «Repartir puntos».
  - **Loadouts**.
- **Tanda 3: el reclutamiento.**
  - Las barracas o el árbol, con la ficha de tropa y sus estrellas.
  - La tropa del campamento en «Nace tu héroe».

## Dudas

### A. La ventana de la base personal

1. **De dónde se calca.** Conquest tiene el detalle del héroe y el inventario (UGUI, local); el prototipo tiene la base personal con
   pestañas.
   - Recomiendo la estructura del prototipo (lateral y pestañas: es lo que tiene los datos del motor), en UI Toolkit y a pantalla completa.
   - Dentro, las pestañas **Equipo** y **Héroe** calcarían la disposición del detalle del héroe de Conquest: atributos a la izquierda,
     héroe 3D en el centro, huecos a la derecha, y el inventario al lado como en la captura 62.
   - ¿O lo prefieres al revés, todo como Conquest?
2. **Escena aparte o capa encima.** Se abre desde el mapa y desde el interior del campamento. Recomiendo una capa a pantalla completa
   encima de lo que haya, sin cambiar de escena (el mapa o el interior siguen debajo y la sesión no se mueve). ¿O una escena propia en
   `Scenes/BronzeAge`, como la preparación?
3. **Cómo se abre.** Con la tecla B y la barra del jugador del prototipo. Recomiendo la barra con solo lo que funciona hoy (Base y Avisos)
   y que el resto (Facción, Misiones, Crónica) vaya saliendo con su paso. ¿Dónde va la barra? Recomiendo arriba, donde está hoy la barra
   del mapa, sustituyéndola.
4. **El retrato del lateral.** El prototipo pone un yelmo con el nivel. Recomiendo el héroe 3D de Conquest (su vista de detalle) con su
   aspecto de verdad.
5. **Lo que lleva el lateral:**
   - el nivel y la experiencia, el Liderazgo, los puntos sin gastar, las monedas, los víveres (y cuánto aguantan), si está herido y dónde
     reside con «Ver en el mapa»;
   - los cargos y la Facción, solo si los tiene (paso 8).
   - ¿Todo esto?
6. **Los estados activos** (Herido, Saliendo del mundo, Moral baja, Plaza ocupada, Hambre) como chips con su tooltip. ¿En el lateral, en
   la barra del jugador, o en los dos?

### B. Tropas

7. **Cómo se ve la lista.**
   - El prototipo usa filas; la regla de las tropas pide las piezas de Conquest.
   - Recomiendo un riel de tarjetas como en «Salir al mundo» (con los huecos vacíos apagados hasta el Liderazgo) y, debajo, la ficha de la
     escuadra elegida: estrellas, nivel, experiencia, moral, formación, dónde está y sus acciones.
   - ¿O la ficha de escuadra de Conquest (`SquadDetailPanel`, que saco en la tanda)?
8. **El orden** (`ordenarEscuadras`). Aquí, fuera de la batalla, sí se guarda en el héroe: para eso es la orden, y es el orden de entrada
   en combate. ¿Te parece bien? En la preparación de batalla no se guarda nada, como decidiste.
9. **La formación** (`elegirFormacion`). Aquí también se guarda (fuera de la batalla). ¿Bien?
10. **Reponer.**
    - En el campamento, «Reponer» las prestadas (`reponerPrestamo`) y las reclutadas (`reclutarEnCampamento`, si residen ahí).
    - En la plaza, `reclutarTropa`, que es del paso 7.
    - Recomiendo solo el campamento ahora. ¿Bien?
11. **La guarnición** (`asignarGuarnicion` / `retirarGuarnicion`). Solo tiene sentido en una plaza. Recomiendo dejarla para el paso 7 y
    ocultarla ahora. ¿Bien?
12. **«Pagar con»** (almacén personal o carro), como en el prototipo. ¿Entra?

### C. Equipo

13. **Siete huecos.** El motor tiene 7 (con montura); el detalle de Conquest tiene 6. La preparación de batalla de Conquest sí tiene el
    hueco del caballo. Recomiendo los 7, poniendo el de la montura donde lo pone la preparación. ¿Bien?
14. **La mochila (72) y el almacén de objetos (144).** ¿Con la rejilla del inventario de Conquest (8 columnas, celdas de su prefab) o con la
    del prototipo (12 por fila, con el color de la rareza y la pila ×n)? Recomiendo la de Conquest, que es la que se calca.
15. **Los objetos del motor en Conquest** (136 frente a 27). Opciones:
    - (a) un importador como el de las tropas (`ObjetosDelMotor`), que cree un `ItemDataSO` por objeto del motor con su miniatura y su pieza
      visual, sacadas de las de Conquest por hueco y peso (ligera, media, pesada) y las armas por clase;
    - (b) una tabla fija de correspondencia en código;
    - (c) los emoji por hueco del prototipo.
    - Recomiendo (a). Duda: los metales (cobre, bronce, hierro…) no tienen modelo propio en Conquest. ¿Se ven igual que la pieza de su peso,
      con su nombre y sus cifras del motor, o quieres que se distingan (por ejemplo, con un tinte)?
16. **Vestir al héroe 3D pieza a pieza** con lo que lleva puesto: en el selector, el interior del campamento, la base personal y la
    batalla. Con (a), sale de la pieza visual de cada objeto. ¿Bien?
17. **El arma en la batalla** (hoy de `TestHeroPool`).
    - Con (a), el servidor de batalla pondría el arma de verdad del ticket (`equipamiento.arma`), por su clase.
    - Las cifras del héroe en combate: ¿las de Conquest de esa pieza, o los `atributos_efectivos` que manda el motor en el ticket?
    - Recomiendo que el aspecto salga de la pieza y que las cifras, si hace falta tocarlas, vayan aparte con el hilo de tropas. ¿Qué
      prefieres?
18. **«Repair» y «Repair All»** de Conquest no existen en el motor. Recomiendo ocultarlos. ¿Bien?
19. **La ficha de la pieza.** El daño, la penetración y la defensa salen de `estadisticas` del motor. ¿Con la ficha del prototipo, o con el
    tooltip de objeto de Conquest (`InventoryTooltipController`)?
20. **Dónde se puede tocar el equipo.** Equipar, desequipar, guardar, sacar y usar solo se puede dentro de una plaza, un pueblo o un
    campamento (`equipo.fuera_de_lugar`). Recomiendo que en el mapa se vea todo sin botones, con «el equipo se cambia dentro de una
    plaza, un pueblo o un campamento», como en la preparación. ¿Bien?
21. **Usar objetos** (los consumibles; la medalla, sobre una escuadra). ¿Entran ahora?

### D. Bienes

22. **El carro, el almacén personal y los víveres.** Conquest no tiene nada; se calca el `panelCarro` del prototipo. ¿Bien?
23. **Lo que solo se hace en la plaza** (`dejarEnPlaza`, `cargarDePlaza`, `cerrarAlmacenACiudadanos`). Recomiendo el paso 7. ¿Bien?
24. **El límite del carro y la línea de velocidad en «Salir al mundo»** (pendiente del paso 4). ¿Entran en la tanda 1?

### E. Héroe y loadouts

25. **Los atributos.**
    - Conquest muestra Liderazgo, Fuerza, Destreza, Armadura y Dureza con sus iconos.
    - El motor tiene fuerza, destreza, armadura y vitalidad, con puntos que se reparten (`repartirPuntos`).
    - Recomiendo el panel de atributos de Conquest con un «+» por atributo mientras haya puntos y «Repartir puntos», y ocultar «Free Reset»
      y «More >>>». ¿Bien? ¿«Dureza» es la vitalidad del motor?
26. **Los perks y las habilidades.** Se abren solos con el nivel (D15), y Conquest los aplica en la batalla. ¿Se enseñan aquí (de solo
    lectura), o nada?
27. **El editor de loadouts.**
    - El prototipo usa casillas por escuadra con el Liderazgo en vivo.
    - Recomiendo el riel de tarjetas de «Salir al mundo» (con ✕ y «+»), con «Activar», «Guardar» y «Borrar» (este último con
      confirmación). ¿Bien?
28. **La ficha de mercenario** de la pestaña Héroe. Recomiendo el paso 12. ¿Bien?

### F. Historial y plazas conocidas

29. **El historial.** Las hazañas y los informes de combate solo los tiene el motor una hora, y la base de datos aparte es de otro hilo.
    Recomiendo dejar la pestaña fuera hasta que esa base exista. ¿Bien?
30. **Las plazas conocidas** (`plazas_recordadas`). Una lista de solo lectura. ¿Ahora o con el mapa estratégico (paso 9)?

### G. Reclutamiento

31. **El árbol del prototipo** (carriles por edificio × Eras, con líneas y requisitos) frente a **las barracas de Conquest** (lista y
    ficha de la escuadra) frente a la lista de hoy.
    - Recomiendo calcar las barracas de Conquest con los datos del campamento: es la interfaz de tropas de Conquest y lleva sus piezas.
    - El árbol, en una pestaña aparte de esa ventana, cuando entre la tecnología (paso 8), porque sus requisitos son las tecnologías de
      la Facción.
    - ¿O quieres el árbol ya?
32. **Dónde se abre.** Hoy el panel de tropa se abre en el edificio militar del interior del campamento (F). ¿Las barracas portadas lo
    sustituyen ahí?
33. **La ficha de tropa con sus estrellas** (`SquadDetailPanel` de Conquest, calcada), al elegir una tropa en las barracas y en la base.
    ¿Bien?
34. **La tropa del campamento en «Nace tu héroe».**
    - El prototipo adivinaba el edificio militar en el cliente (un hash de la posición): no es un dato del motor.
    - Para enseñar de verdad qué ofrece cada campamento antes de nacer, el motor tendría que darlo en la lista de campamentos para elegir.
    - ¿Lo pedimos al motor, o se deja fuera?

### H. Varios

35. **Los rechazos nuevos** (equipo, mochila, almacén de objetos, inventario, escuadra, formación) llevan su frase en `Textos`, como siempre.
36. **La barra de arriba del mapa.** Si la barra del jugador la sustituye (duda 3), los botones de hoy (Nodos, Fertilidad, Marcha forzada,
    Avisos, Cambiar de partida, Cerrar sesión) ¿se quedan en ella o se van a un menú?

## Pedidos al motor que salen

- **Duda 34:** lo que ofrece cada campamento (su edificio militar y las tropas reclutables) en la lista de campamentos para elegir al
  nacer.
- **Duda 15, si hace falta:** el `tipoArmadura` en el ticket (`HeroSnapshot.equipamiento`). Así el servidor de batalla no depende de que el
  id del objeto siga la convención. Con el importador (a) no hace falta, porque Conquest tendría el catálogo.

## Nota de Víctor (2026-11-14, 21:11)

- **Fallo de capas y cámaras en Conquest.** La textura de la vista de detalle del héroe tiene que enseñar solo al héroe, sin fondo. Se
  arregla poniendo al héroe en su capa y haciendo que esa cámara solo vea esa capa.
- **Va en la tanda 2**, al portar el detalle del héroe, comprobado en captura.
- **Sobre la captura 60:** la calle del fondo la puso mi cámara de captura (ve todas las capas), no la de Conquest. La vista 3D de Conquest
  es el cuadro blanco, que en batch no llegó a pintarse.
- **La causa** (encontrada al mirarlo, sin tocar nada):
  - La cámara del detalle se pone en la capa «Heroes» (`HeroDetail3DPreview.cs:127`), pero esa capa no existe en `TagManager`.
  - Al no encontrarla, cae a la capa Default (línea 131) y pinta lo que hay en Default: el escenario sin cielo.
  - `HeroVisualInstantiationSystem` pone al héroe en `Heroes_A` o `Heroes_B` (`HeroVisualInstantiation.System.cs:149`), con todos sus hijos.
  - La cámara mira al héroe del mundo, no a una copia.
- **Arreglo previsto (tanda 2):**
  - que la cámara vea solo la capa del héroe;
  - fondo transparente;
  - la capa puesta también en las piezas que se instancien al vestirlo;
  - mirar si el selector y el creador tienen lo mismo.

## Respuestas de Víctor (2026-11-14, 21:27)

Lo que no comenta, como se propuso.

- **8. El orden en la base:** no. El orden de las escuadras solo se elige al salir al mundo o al entrar en batalla, fuera de la base. La base
  no tiene ↑↓.
- **15. Los objetos:** si ya tienen rareza, al menos una imagen provisional y la rareza como en Conquest (su precedente para objetos), en UI
  Toolkit como todo lo demás.
- **19. La ficha del objeto:** en UI Toolkit, mezcla de la interfaz de BronzeAge con todos los valores que muestra Conquest, sin perder el
  tooltip de comparación.
- **20.** En el mapa el equipo se ve sin poder cambiarlo, **sin** el mensaje que lo explica.
- **25.** Sí: la Dureza de Conquest es la vitalidad del motor.
- **27. Los loadouts:** con la tarjeta grande de tropa.
- **31. El reclutamiento:** el árbol del prototipo, con las tarjetas de tropa de Conquest.
- **34.** Víctor pregunta si «Nace tu héroe» no estaba cerrada, o si no es la pantalla donde se elige el campamento. Hay que aclarárselo.
- **El retrato del detalle del héroe** (captura de Víctor): en el centro debería verse el héroe 3D sobre fondo negro, que gire con el clic.
  Ya funcionaba, y un error en las capas del héroe y las de la cámara de la textura lo rompió.
