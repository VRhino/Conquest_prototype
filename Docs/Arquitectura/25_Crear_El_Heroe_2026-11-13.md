# 25. Crear el héroe (2026-11-13)

Paso 3 del bloque 4 de [`Hoja_De_Ruta.md`](../Hoja_De_Ruta.md) (inventario §3.3). Una cuenta sin héroe ya no se queda en la pantalla de la
sesión viva. Al entrar en la partida pasa por dos pantallas (y una cuenta que ya tiene héroe, por el selector de personaje; ver más abajo):

1. **«Nace tu héroe»** (`MundoScene`, UI Toolkit), sobre el mapa del paso 2. Aquí solo se elige el lugar y se escribe el nombre.
2. **El creador de personaje de Conquest** (la escena `AvatarCreator`, UGUI). Se abre encima con «Nacer en <sitio>». Allí se elige la
   clase entre las del motor, el género y el aspecto. Al confirmar sale `crearHeroe`. Si el motor lo acepta, vuelves al mapa con el
   héroe dentro de su campamento.

## Dónde está

| Pieza | Dónde | Qué |
|---|---|---|
| `ClaseDeHeroe` | `Conquest.Mundo` | Las clases de `Objetos_Del_Heroe.CLASES_HEROE` con su equipo inicial de `OBJETOS`: el arma de tier 1 de su clase, las cuatro piezas de su armadura inicial (sin casco) y la montura de tiro. En BronzeAgeClient: `ui/clasesHeroe.ts` |
| `AnalisisDeCampamento` | `Conquest.Mundo` | Lo que rodea a cada campamento, sacado del artefacto (el motor no lo manda). En BronzeAgeClient: `mundo/analisisCampamento.ts`. Ver el detalle debajo de la tabla |
| `SelectorDePersonaje` | `Conquest.Mundo` | El puente entre las dos pantallas. `Actual` lleva el nombre, el lugar, las clases, `Confirmar` (manda `crearHeroe` y devuelve el rechazo o null) y `Volver`. En null, el creador hace lo de siempre. `Heroe` es lo mismo para el selector de personaje |
| `PantallasDeMundo.CrearHeroe()` | `Conquest.Mundo.UI` | «Nace tu héroe». Abre y cierra el creador de forma aditiva y le pasa el encargo |
| `MapaDeMundo` (modo sin héroe) | `Conquest.Mundo.Mapa` | El mismo mapa, mirando al campamento, con 150 u en pantalla. Muestra los campamentos y bandidos del artefacto y los aros de 150 y 250 u del elegido. Usa `Enfoque`, `RadioVisionFijo`, `Adicionales` y `RayasAdicionales` |
| `AvatarCreatorUIController`, `HeroClassSelector` | `Game.ECS` (`Scripts/UI/avatarCreator`) | El creador. Con un encargo pasa al modo del motor; sin él, nada cambia |
| `HeroSelectionSceneController` | `Game.ECS` (`Scripts/UI`) | El selector de personaje. Con `SelectorDePersonaje.Heroe` pasa al modo del motor; sin él, nada cambia |
| `CrearHeroe.uxml` | `Assets/Resources/MundoUI` | «Nace tu héroe» |

`AnalisisDeCampamento` calcula, para cada campamento:

- suelo, fertilidad y bosque a 150 u;
- yacimientos a 150 u, a 250 u y el más cercano;
- río, agua, bandidos a 250 u y el campamento vecino;
- el nombre de su región;
- su nota para el «Recomendado».

## Decisiones

- **El mismo mapa del paso 2**, no uno aparte. Un clic sobre un campamento lo elige; si hay varias cosas bajo el clic, se elige en la lista.
  Un clic en el suelo no quita la elección. No hay niebla, porque aún no hay héroe ni VISTA. A esta escala, las cajas de campamentos y
  bandidos van más grandes, como marcas, y sin los nombres de los nodos (serían cientos).
- **Ids.** La `Bienvenida` y el artefacto nombran los campamentos de forma distinta (`camp-N` y `campamento-N`). Se cruzan por N, como en
  BronzeAgeClient.
- **Los rótulos del mapa** se colocan en fracción de la vista de la cámara sobre su capa. Antes salían desplazados en las capturas de
  la prueba, en las que la interfaz pinta en una textura de otro tamaño; en el juego ya coincidían.
- **Nombre del campamento.** Cada campamento lleva el nombre de su región del generador, con las 16 áreas de BronzeAgeClient
  (`regionDe`). Si dos caen en la misma región, se añade hacia dónde queda cada uno («Valle meridional (noroeste)»); si aún chocan, un
  número romano. El artefacto no trae la semilla, así que cerca de una linde el nombre puede ser el de la región vecina. Sin tamaño de
  mundo en el manifiesto, el nombre es «Campamento N».
- **«Recomendado», «Elegir por mí» y comparar dos**, como BronzeAgeClient. Son ayudas del cliente: el motor no valora nada.
  - La nota pondera cinco cosas, cada una comparada con los demás campamentos: poca gente (0,3), tierra fértil (0,25), yacimientos a
    150 u (0,25), pocos bandidos a 150 u (0,1) y agua cerca (0,1).
  - «Elegir por mí» sortea entre los tres mejores, con más peso cuanto mejor.
  - Al marcar dos campamentos, la ficha se cambia por la tabla de comparación, con el mejor de cada fila en oro y un botón para elegir
    cada uno.
  - Falta, porque el paso 3 no lo tiene, la fila «Tropa» (el edificio militar del campamento y lo que recluta desde el día 1).
- **Solo lo que se puede hacer.**
  - «Nacer en …» aparece cuando hay campamento; mientras no lo hay, se dice lo que falta.
  - El nombre puede ir vacío: el motor lo llama «Héroe N».
  - Un rechazo del motor se ve en el creador, encima de los botones. Si el rechazo es por el nombre (`heroe.nombre_en_uso`,
    `heroe.nombre_invalido`…), al «Volver» también aparece junto al campo, que es donde se cambia.
- **El nombre y «Nacer» siempre a la vista.** El pie del panel no se encoge (`flex-shrink: 0`); si la ficha no cabe, se encoge ella y saca
  su barra. La prueba lo comprueba en 1280×720, 1600×900, 1920×1080 y 1950×1130. Lo que se pulsa en acceso, partidas, la ficha del mapa y
  la lista bajo el clic (cuando sale) también queda dentro de la ventana y de su placa en esos cuatro tamaños.

## El creador de personaje

- **Se carga de forma aditiva** sobre `MundoScene` y pasa a ser la escena activa, con su luz y su cielo. Mientras está abierto se apartan
  la interfaz del mundo, el mapa y su cámara. El objeto `Mundo`, con la sesión, sigue encendido y bombeando, así que la sesión con el
  motor sobrevive al cambio. Con «Volver» se descarga y queda «Nace tu héroe» como estaba: el mismo campamento y el mismo nombre. Si la
  sesión obliga a cambiar de pantalla con el creador abierto (por ejemplo, una sesión reemplazada), el creador se cierra solo.
- **Modo del motor** (cuando hay `SelectorDePersonaje.Actual`):
  - Muestra las 5 clases de `/v1/datos`, empezando por la primera. La ficha de cada una enseña sus armaduras, atributos y equipo
    inicial. El maniquí se viste con la armadura inicial de la clase (ligera, media o pesada) y se vuelve a vestir al cambiar de género.
  - Las ventajas y habilidades de las clases de relleno no se enseñan.
  - El nombre y el lugar aparecen como texto («Naces en …» y el campo del nombre sin poder editarse). Las etiquetas pasan al
    español (Género, Hombre, Mujer, Cabeza, Pelo, Cejas, Barba —sin barba ni su título para una mujer—, Tu clase). Los nombres de
    clase, largos, ocupan dos o tres líneas en su parte de la fila. Los botones son «Nacer» y
    «Volver».
- **El aspecto que se manda.** El género va como `masculino` o `femenino`. Las piezas salen de `AvatarPartSelector.GetCurrentSelection()`
  con los ids de `AvatarPartDatabase.asset` (`head_00`, `hair_01`…) y van a `cabeza_id`, `pelo_id`, `barba_id` y `cejas_id`.
- **Lo que se desacopló** para que el creador sirva a los dos flujos:
  - **El guardado.** En el modo del motor, «Nacer» no llama a `HeroDataService.CreateNewHero`, `PlayerSessionService` ni
    `SaveSystem.SavePlayer`: no se toca `player_save.json`.
  - **La salida.** No se carga `CharacterSelection`; quien abrió el creador decide adónde se va.
  - **Las clases.** Las del motor no son `HeroClassDefinition`. El selector las pinta aparte (`BotonesDelMotor`) y guarda la elegida en
    `ClaseDelMotor`. `selectedClass` y `GetCurrentEquipment()` siguen siendo del flujo viejo.
  - **El nombre.** En el modo del motor llega escrito y no se edita.
  - **Las referencias.** `Game.ECS` referencia ahora a `Conquest.Mundo` (C# puro), solo para el puente y `ClaseDeHeroe`. La sesión, el
    contrato y UI Toolkit no entran en `Game.ECS`.
- **El flujo viejo de Conquest sigue igual.** Sin encargo, el creador enseña sus 4 clases de relleno, deja escribir el nombre y al
  continuar guarda en `player_save.json` y va a `CharacterSelection`.

## El aspecto, guardado en el motor

Desde el motor 358af5ff (contrato 0.25.0; hace falta partida nueva), `crearHeroe` guarda el género y las cuatro piezas. Los devuelve en
`Heroe.femenino` y `Heroe.avatar`, tanto en el héroe propio como en el público, y en el ticket de batalla. El proto solo cambió en
comentarios, así que la DLL del contrato de Conquest no se regenera: `Heroe.Femenino` y `Heroe.Avatar` ya estaban.

- **Lo que acepta.** El género vale `""` (masculino), `masculino` o `femenino`; cualquier otro valor da `orden.forma_invalida`. Cada pieza
  va vacía (la de por defecto) o es un id de hasta 32 letras ASCII, cifras, `_` o `-`; si no, da `heroe.aspecto_invalido`, que tiene su
  frase en `Textos`. Los ids de `AvatarPartDatabase` (`head_01`, `hair_03`…) cumplen. Una mujer no tiene barba: va vacía.
- **Dónde se ve.** El almacén guarda el `Heroe` tal cual llega. El diagnóstico (F2) lo enseña en la línea «Aspecto» («mujer · cabeza
  head_01 · pelo hair_03 · barba por defecto · cejas eyebrow_02»). Todavía no hay otra pantalla que lo muestre.

## El selector de personaje

Con una cuenta que ya tiene héroe, la pantalla de partidas de BronzeAgeClient (una tarjeta «Mundo» con el héroe; era un apaño hasta
llegar a Unity) se sustituye por el selector de personaje de Conquest, la escena `CharacterselecctionScene` (UGUI). El motor tiene una
partida por servidor y un héroe por cuenta, así que no hay que elegir entre varios.

- **El camino.** Acceso → partidas (solo mientras se abre la sesión y llegan `HEROE` y `FACCION`, con la misma espera de antes) →
  selector → «Entrar al mundo» → carga → mapa. Sin héroe, el camino no cambia: partidas, «Entrar» y «Nace tu héroe». Si la carga del
  mundo falla, vuelve la tarjeta de partidas con el error y «Reintentar».
- **Lo que se movió.** Las reglas (`/v1/datos`) se leen antes, al abrir el selector, porque hacen falta para la clase y para el tipo de
  armadura (`ConexionDeMundo.CargarDatos`). La carga las vuelve a leer de la caché. Nada más cambió de orden.
- **Cómo se abre.** Igual que el creador: de forma aditiva, encima de `MundoScene`, con la interfaz del mundo apartada y la sesión
  viva. El puente es `SelectorDePersonaje.Heroe`: nombre, nivel, Facción, clase, tipo de armadura, género, las cuatro piezas, `Entrar` y
  `CerrarSesion`. Si la sesión obliga a cambiar de pantalla con el selector abierto, se cierra solo.
- **Lo que enseña** (`HeroSelectionSceneController`, modo «desde el motor»):
  - El maniquí, con el género y las piezas que guardó el motor.
  - La armadura que lleva puesta en el torso (ligera, media o pesada, buscada en `OBJETOS`) con las piezas de inicio de Conquest de ese
    tipo. Si no lleva nada en el torso, la armadura inicial de su clase.
  - Una ficha con su nombre y «Nivel N · clase · Facción».
  - «Entrar al mundo» y «Cerrar sesión». El título dice «Tu héroe».
- **Lo que no enseña.**
  - **«Crear héroe» y «Borrar héroe».** Con héroe no se puede crear otro y el motor no tiene orden de borrar. Es un pedido a futuro, fuera
    de esta migración, apuntado en el bloque 4. Cuando exista, estos botones vuelven en el modo «desde el motor».
  - **El blasón.** La pantalla de partidas de Unity tampoco lo tenía: enseñaba el nombre de la Facción como texto, y así sigue. El sigilo
    llega con el paso 8.
  - **El equipo puesto pieza a pieza.** Casar los objetos del motor con las piezas visuales de Conquest aún no existe. Queda pendiente
    para el paso 6 (equipo).
- **El lienzo** de la escena está hecho a 1920×1080 con píxeles fijos: a otro tamaño, «Entrar» y la ficha se salían de la pantalla. En
  el modo del motor escala con la ventana (1920×1080 de referencia, siempre entera). El flujo viejo sigue como estaba, y a otros tamaños
  se sigue saliendo.
- **El flujo viejo** (sin `SelectorDePersonaje.Heroe`) no cambia: los héroes de `player_save.json`, crear, borrar e ir a Feudo.

## Pruebas

- **`NacerDelHeroeTests`** (EditMode):
  - las clases con su equipo inicial;
  - el nombre de región, y el rumbo cuando dos campamentos caen en la misma;
  - el recomendado y sus razones, y «Elegir por mí».
- **`PantallasDeMundoTests.CrearCuentaEntrarYCerrarSesion`** (PlayMode, contra un servidor del motor propio). La interfaz se pulsa como
  el jugador; el mapa y el creador, con un ratón del Input System. La prueba recorre:
  1. Una cuenta nueva entra en «Nace tu héroe», sin clases, con un solo «Recomendado» y los nombres de región.
  2. Un clic con el ratón sobre el campamento del centro del mapa lo elige.
  3. «Elegir por mí».
  4. Marcar dos campamentos para compararlos y elegir uno desde la tabla.
  5. Elegir el campamento 4 en la lista. Ahí se comprueban el pie en los cuatro tamaños y el resto de pantallas en esos tamaños.
  6. Con un nombre ya usado, el creador se abre con las 5 clases (cada una pulsada con el ratón), «Nacer» da el rechazo allí, y «Volver»
     regresa con el campamento, el nombre y el rechazo junto al campo.
  7. Con otro nombre: otra clase, mujer y otro pelo, y «Nacer».
  8. El creador, capturado en los cuatro tamaños, con todo lo que se pulsa dentro de la imagen.
  9. El héroe aparece en el mapa dentro de su campamento. El `Heroe` propio vuelve con la clase elegida, mujer y las mismas tres piezas
     (cabeza, pelo y cejas no son las de por defecto; la barba, vacía), y F2 lo enseña. Desde ahí sigue la prueba del paso 2.
- **`PantallasDeMundoTests.ElCreadorDeConquestSigueConSusClases`** (PlayMode): sin encargo, el creador sigue con sus 4 clases y el
  nombre editable.
- **`PantallasDeMundoTests.ConHeroeSaleElSelectorDePersonaje`** (PlayMode, contra el motor). Dos cuentas creadas por la sesión, las dos
  sin Facción: una mujer de «Arco compuesto y lanza» con cabeza, pelo y cejas que no son los de por defecto, y un hombre de «Espada
  corta y escudo mediano» con barba. Para cada una:
  1. Entra por el acceso y llega al selector, sin la tarjeta de partidas y sin crear ni borrar.
  2. La ficha tiene su nombre, su clase y «Sin Facción».
  3. Cada pieza del motor está a la vista en el maniquí, con su género.
  4. La mujer se captura en los cuatro tamaños, con todo lo que se pulsa dentro, y entra con el ratón en «Entrar al mundo» hasta el
     mapa.
  5. Se cierra la sesión: la mujer desde el mapa, el hombre desde el selector.
- **`PantallasDeMundoTests.ElSelectorDeConquestSigueConSusHeroes`** (PlayMode): sin el motor, el selector enseña el héroe de la sesión
  local con crear y borrar.

## Un hueco vacío del aspecto no se rellena (2026-11-14)

**El síntoma.** Víctor eligió un pelo que no era la coleta y sin barba, y en el campamento el héroe salió con coleta y con barba. Eran dos
causas a la vez:

1. **Su motor local (4a16dab7) no tiene 358af5ff.** Es el commit que guarda el aspecto al crear el héroe, así que el héroe llega sin
   piezas. Con 358af5ff el pelo elegido sí se guarda y se ve.
2. **En el cliente,** `VestidoDelMotor` solo tocaba los huecos de las piezas que recibía. Un hueco vacío («sin barba») se quedaba con la
   pieza que trae el maniquí de Synty, que lleva coleta y barba. Ahora apaga lo que cuelga de los huecos vacíos de pelo, barba y cejas.
   Con un motor que no guarda el aspecto, el héroe sale sin pelo, barba ni cejas, no con unos inventados.

**Por qué la prueba no lo cazó:** creaba una mujer con todos los huecos llenos y solo miraba que lo elegido se viera. Ahora `SoloLoElegido`
comprueba que en cada hueco se ve lo elegido y nada más, en el selector (tres cuentas, una con el caso de Víctor) y con el héroe dentro del
campamento. Sin el arreglo, falla con «barba: se ve facialhair_01».
