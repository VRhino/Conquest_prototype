# 26. El interior de un campamento de mercenarios (2026-11-13)

Paso 4 del bloque 4 de [`Hoja_De_Ruta.md`](../Hoja_De_Ruta.md) (inventario §3.15 y §3.8). Va en dos tandas:

- **4A**: estar dentro y salir. Cuando el héroe está dentro de un campamento de mercenarios, se ve el interior en FeudoScene: el
  héroe del motor andando a pie en tercera persona por el campamento que manda el motor. Desde ahí se sale al mundo.
- **4B**: las interfaces (residir, comprar, reclutar, repostar y salir por la puerta). El autor eligió «todo como se propone» en la tabla
  del final (2026-11-13).

## El camino

- **Cuándo se entra.** El mapa del paso 2 mira cada 250 ms dónde está el héroe. Si está dentro de un campamento de mercenarios y ya llegó
  su ámbito `CAMPAMENTO` con la planta, abre el interior. Eso pasa al nacer, al pulsar «Entrar» en la ficha del mapa, al volver del
  selector de personaje o al reconectar estando dentro. Los demás sitios (plazas, pueblos, avanzadas) siguen con el aviso «Estás dentro
  de…» hasta su paso.
- **Cómo se abre.** Como el creador y el selector: FeudoScene se carga de forma aditiva encima de `MundoScene` y pasa a ser la escena
  activa, con su luz y donde nace el héroe.
  - La cámara del mundo se apaga, porque FeudoScene trae la suya.
  - El objeto `Mundo`, con la sesión, sigue encendido: el reloj, los avisos, la reconexión y la sesión reemplazada siguen como siempre.
  - Si algo obliga a cambiar de pantalla con el interior abierto, se cierra solo.
- **La cabecera** (UI Toolkit, `Interior.uxml`):
  - dónde estás: el nombre de su región y «Campamento de mercenarios · N residentes · tu residencia / de paso»;
  - Avisos, «Salir al mundo» y Cerrar sesión;
  - abajo, la ayuda: «WASD: andar · Ratón: mirar · Alt: soltar el ratón para pulsar la interfaz».
- **«Salir al mundo»** (BronzeAgeClient: `ui/ventanaSalida.ts`, `ui/cargaDeSalida.ts`) enseña solo lo que se puede elegir de verdad:
  - **Tropa:** tus escuadras que esperan en el campamento, si resides ahí. De paso, sales con tu columna tal como está.
  - **Carga:** lo de tu almacén personal, también solo si resides.
  - **Orden:** manda `salirDelCampamento {campamentoId, heroeId, escuadronIds, carga}`. Si se acepta, se cierra el interior y sale el
    mapa con la columna a la puerta. Si se rechaza, la frase del rechazo aparece en la ventana.
  - **Lo que queda fuera:** el modo «Ejército» (convocatorias) es del paso 11. La línea de velocidad y el tope del carro llegan con la
    tropa (paso 6); hoy el motor rechaza con su frase lo que no cabe.
- **La vuelta.** Desde el mapa, «Marchar aquí» al campamento y, ya parado a la puerta, «Entrar» trae de nuevo al interior.

## FeudoScene en modo «desde el motor»

`InteriorDelMotor` (`Game.ECS`, `Scripts/Interior`) se engancha al cargar la escena si hay `SelectorDePersonaje.Interior`. Sin él,
FeudoScene hace lo de siempre. **El fichero de la escena no se toca**: todo se hace en tiempo de ejecución.

| Qué | En este modo | Por qué |
|---|---|---|
| `DotsWorld` (SubScene: héroe y punto de aparición), la cámara que sigue al héroe, la luz, `visualRegistry`, `EventSystem` | **Encendido** | Es el esqueleto: el héroe ECS andando en tercera persona |
| `UICanvas` (HUD, inventario, detalle del héroe, barracas, tienda, diálogos, recompensas) | Apagado | Vive de `PlayerSessionService.SelectedHero` y de datos de relleno |
| `MinimapCanvas` | Apagado | El minimapa es del feudo hecho a mano |
| `NPCs` (el herrero «Smith») | Apagado | Su diálogo abre la tienda de relleno |
| `Matchmaking` | Apagado | Batallas simuladas: el paso a la batalla es el paso 5 |
| `TestEnv` | Apagado y con guarda | Cargaría `player_save.json` |
| `Squad_Preview`, `Hero_Preview` | Apagados | Cámaras de las vistas previas del HUD |
| `Map` (el recinto Synty hecho a mano) | Apagado en cuanto hay campamento; hasta entonces solo su `Ground`, donde aparece el héroe | Lo sustituye el campamento del motor. Para volver a verlo: `InteriorDelMotor.ConRecintoDelFeudo = true` |

**Lo que se desacopló en Conquest:** cuatro guardas mínimas; sin ellas, nada cambia.

- `TestEnvironmentInitializer`: en el modo del motor no carga `player_save.json`.
- `HeroInputSystem`: en el modo del motor las teclas I, P, B, K, Esc y Alt no abren los paneles de Conquest, que están apagados. Alt la
  usa el interior para soltar el ratón (`BattleCommandInput.SetPointerMode`).
- `HeroVisualAppearanceSystem`: el aspecto lo pone `VestidoDelMotor`, compartido con el selector de personaje: el género y las piezas
  del motor, y las piezas de inicio del tipo de armadura que lleva en el torso.
- **Limpieza al salir:** al descargarse la escena, `InteriorDelMotor` borra del World de ECS la entidad del héroe local (si no, al
  volver no aparecería otro) y suelta el ratón.

**Lo que sigue igual:** `HeroSpawnSystem` saca el héroe en el punto de aparición de la SubScene. La fase de juego es Feudo, así que el
héroe anda sin caballo. `HeroSpawnSystem` registra un error porque el escuadrón del héroe va vacío: es ruido de los datos de relleno y
no bloquea nada.

## La planta del campamento

El motor la manda desde el 2026-10-27 (paridad P7; BronzeAgeFase0 `Docs/Mechanics/Campamentos_Mercenarios.md` §7). La hoja de ruta
y el inventario decían que no (antes CQ-007); ya están corregidos. Llega en `CampamentoMercenarios`, en el ámbito `CAMPAMENTO` (dentro,
o con la columna parada a la puerta):

- `planta`: cada `Edificio` con su `tipo` (taberna, mercado y 5 puestos, el militar, 10 viviendas, y plaza de armas, plaza, pozo y
  parque), su centro (`posicion`, en milésimas) y su huella (`ancho` × `alto`) en unidades locales;
- `trazado.calles`;
- `empalizada` y `empalizada_puerta`, rectángulos por la esquina.

Un campamento típico trae 19 piezas, 24 calles y unos 35 tramos de empalizada.

**Cómo se monta.** Con cuadros, como el mapa del paso 2, sin inventar nada:

- el suelo;
- las calles, planas;
- cada edificio, una caja con su huella y el color del plano de BronzeAgeClient (`ui/planoCampamento.ts`), con alturas de 2 a 5 m; lo
  plano (plaza, plaza de armas, parque) sin altura;
- la empalizada, de 3 m;
- la puerta, sin muro.

Todo choca menos lo plano: el héroe no atraviesa edificios ni la empalizada. El campamento se coloca de modo que la puerta queda donde
aparece el héroe.

**Escala:** 1 unidad local = 1 m (`InteriorDelMotor.MetrosPorUnidadLocal`). No sale de `EscalaDelMapa`. El espacio local de un
asentamiento es propio y no se mide contra el mapa (Trazado_Urbano: «la ciudad no se mide contra el exterior»). Con 1 m, una vivienda
de 2 × 2 celdas es una tienda de 6 × 6 m y el campamento (radio de 60 u.l.) mide unos 120 m: va a la escala del héroe de Synty.

## Acciones a la puerta, solo con la columna parada

En la ficha del mapa, «Entrar» salía en cuanto la columna estaba dentro del radio de la puerta, aunque aún llegara. El motor mide la
puerta con la columna donde él la tiene y la rechazaba («la columna no está a la puerta del campamento»). Ahora todas las acciones a la
puerta de la ficha (entrar, cargar, abrir un alijo…) salen solo con la columna parada (`EstadoColumna.ColumnaEstacionada`) y sin
formación, como BronzeAgeClient 0.195.1 («Entrar solo cuando se puede»). Es `Contexto.EnMarcha` en `CosasDelMapa`.

## Rendimiento

En la prueba, con el héroe andando por el campamento (2,5 s, unos 3.000 fotogramas, en batch): mediana de 0,8 ms por fotograma, p95 de
1,0 ms y el peor de 4–5 ms. El interior son unas 80 cajas.

## Las interfaces del campamento (4B)

**Cómo se abren.** Con el mismo mecanismo que la puerta y el herrero del feudo (`DoorInteractable`, `NpcTriggerZone`):

- Un collider disparador alrededor de la pieza de la planta (`ZonaDelInterior`) detecta al héroe local.
- La tecla F llega por `PlayerInteractionComponent`.
- Solo responde la zona donde está el héroe.
- El cartel («F · Taberna», «F · Mercado», «F · Barracón: tropa», «F · Salir al mundo») lo pone la interfaz en español. El cartel del
  herrero vive en el HUD de Conquest, que en este modo está apagado.
- Con un panel abierto se usa el bloqueo de los diálogos de Conquest (`DialogueUIState.IsDialogueOpen`): el héroe no anda, la cámara no
  gira y el ratón queda libre. Al cerrar (botón o Esc) vuelve el control.
- La zona de la puerta está justo fuera de la empalizada: el héroe aparece en el hueco y tiene que salir para que se abra.
- Qué pieza da a qué zona sale de la planta: `taberna`; `mercado` y sus `puestoMercado`; el edificio militar, que es el de
  `MERCENARIOS.edificiosMilitares`.

Los paneles (`PantallasDeMundo.Campamento.cs`, UI Toolkit, con la letra y el estilo de las demás pantallas) se portan de
`ui/pantallaCampamento.ts`:

- **Taberna** (la parte «Resumen» del prototipo):
  - si resides o no, con la explicación del prototipo;
  - los residentes;
  - «Residir aquí» (`residirEnCampamento`) solo si no resides. Si tienes tropa prestada de otro campamento, avisa antes de que se
    retira, como el prototipo;
  - tu tropa (cuántos hombres, dónde está y si es prestada), tu almacén y los edificios del campamento.
  - No hay «dejar de residir»: el motor no tiene esa orden.
- **Mercado** (`mercado` y `cablearMercado`):
  - lo que hay en venta (el residente, todo menos el oro; el visitante, solo trigo; pagando con el carro, sin trigo, que se reposta
    gratis);
  - del bien elegido: lo que hay, lo que tienes donde pagas, el cupo de hoy (`cupo_diario` menos `Heroe.cupo_campamento` del día de
    mundo), tu oro y el precio por unidad (`precios_mercado`, en millonésimas);
  - una cantidad hasta el máximo que se puede pagar, y lo que pagas, lo que te queda y lo que tendrás;
  - «Comprar N por X de oro» (`comprarEnCampamento`). El residente con columna elige si paga con su almacén o con el carro.
  - La sesión no trae lo servido en la respuesta (`ResultadoOrden.datos` va vacío). «Compraste N de X por Y de oro» sale de lo que
    tenías antes y después, y avisa si no cabía o no alcanzaba para más.
- **Tropa** (`tropa`, `lugarCampamento` y `accionesCampamento`), en una lista en lugar del árbol de carriles, solo para el residente:
  - los reclutas del campamento (`poblacion`, un ritmo) y tu oro, con «Pagar con» si llevas columna;
  - **lo que te prestan:** las tropas de leva comunal de `TROPAS_RECLUTABLES` (el motor dice «solo se presta la tropa de leva
    comunal»; son cuatro, y el prototipo tenía tres escritas a mano). Cada una con «Pedir prestada» (`pedirPrestamo`) o lo que ya te
    prestaron;
  - **el edificio militar:** las tropas que ofrece (`reclutables`), con su precio por soldado o «el campamento aún no la ofrece». Para
    las ofrecidas, «Reclutar N · X de oro» (`reclutarEnCampamento`, con `precio_escuadra`). Si faltan reclutas o no llega el oro, sale
    desactivado con su motivo.
  - Un héroe tiene una escuadra por tropa: la suya sale como «tu escuadra n/N».
  - **Las barracas de Conquest no se tocan:** se adaptan cuando exista la correspondencia tropa del motor ↔ `SquadData` (apuntado en
    el paso 6 de la hoja de ruta).
- **«Salir al mundo»** (de la 4A), ahora también:
  - con la carga, el total en el carro frente a `LOGISTICA.capacidadCarroPorJugador`;
  - los víveres: los que tienes frente a `LOGISTICA.capacidadViveresPorHeroe`, y «Llenar víveres» (`repostarEnCampamento`) solo si no
    están llenos. El prototipo repostaba solo al entrar en tu residencia y desde la ficha del mapa;
  - se abre también andando hasta la puerta.
- **Los rechazos:** todos los de residir, comprar, reclutar, préstamo y salir tienen su frase en `Textos`. El catálogo del motor no trae
  frase y el prototipo enseñaba el texto del servidor tal cual.
- **Los recursos:** su nombre en español (`CosasDelMapa.NombreDeRecurso`, de `RECURSO_NOMBRE` del prototipo): Estaño, Ganado…
- **Fuera de esta tanda,** con su nota en la hoja de ruta:
  - el fondo de refundación (paso 8);
  - el tablón de mercenarios por contrato y la taberna (paso 12);
  - lo que lleva el héroe en una ventana propia (paso 6).

## La propuesta de la 4B, interfaz por interfaz (aprobada)

Lo que hace BronzeAgeClient en el campamento (`ui/pantallaCampamento.ts`, `ui/arbolReclutamiento.ts`, `ui/ventanaSalida.ts`), qué
hay en FeudoScene que podría alojarlo y la recomendación. El autor la aprobó entera; lo hecho está en «Las interfaces del campamento».

| Qué | Órdenes y datos del motor | Qué hay en Conquest | Recomendación y coste |
|---|---|---|---|
| **Residir** | `residirEnCampamento {heroeId, campamentoId}`. Rechazos `residencia.cooldown` y `residencia.ya_reside`. Datos: `residentes_ids` y `Heroe.residencia_id` | Nada propio. El herrero (`NpcTriggerZone` con diálogo) es el patrón de «acercarse y pulsar F» | **Adaptar:** una zona de `NpcTriggerZone` en la taberna de la planta que abra un diálogo UI Toolkit «Residir aquí». Si residir retira tropa prestada, la confirmación del prototipo. Bajo: una orden y una pregunta. El héroe que nace aquí ya reside |
| **Comprar** (mercado) | `comprarEnCampamento {recurso, cantidad, campamentoId, pagarCon}`. Datos: `mercado` (existencias), `precios_mercado` (solo en el ámbito), `cupo_diario` menos `Heroe.cupo_campamento`, almacén personal o carro y oro. 10 rechazos (`mercenarios.sin_stock`, `cupo_agotado`, `sin_oro`…). El visitante solo ve el trigo | La tienda de Conquest (`UIStoreController`, UGUI) vende objetos de `StoreDatabase` con bronce de `CurrencyService`. No casa: aquí se compran recursos a precio de motor, con cupo y con elección de dónde pagar | **Migrar** el panel del prototipo a UI Toolkit, abierto al acercarse al mercado o a sus puestos (misma zona con F). Para reusar la tienda haría falta un adaptador de recursos del motor a «objetos», otro precio y otra moneda: más caro que hacerlo nuevo. Medio |
| **Reclutar** | `reclutarEnCampamento {tropaId, pagarCon}`, solo el residente. Datos: `reclutables` (precio por soldado, unidades, desbloqueada), `poblacion` (reclutas), `TROPAS_RECLUTABLES` y el edificio militar del campamento. Rechazos `mercenarios.no_resides`, `al_tope`, `sin_reclutas`… | Las barracas (`BarracksMenuUIController`, UGUI) crean escuadras con `SquadDataService` y guardan en `player_save.json` | **Adaptar** las barracas cuando exista el casamiento tropa del motor ↔ `SquadData` de Conquest (lo necesita también la batalla, paso 5). Mientras, una lista UI Toolkit en el edificio militar (barracón, galería de tiro o caballerizas) con lo reclutable y su precio. Medio-alto si se adaptan las barracas; medio con la lista |
| **Pedir y reponer préstamo** | `pedirPrestamo {tropaIds}` y `reponerPrestamo {}`. Las tres tropas que se prestan están escritas a mano en el prototipo (`TROPAS_PRESTAMO`) | Las mismas barracas | **Junto con reclutar,** en el mismo panel (el carril «Campamento» del árbol del prototipo). Bajo una vez hecho reclutar |
| **Repostar víveres** | `repostarEnCampamento` (trigo para la columna) | Nada | **En la ventana «Salir al mundo»,** como la línea de víveres del prototipo. Bajo |
| **Fondo de refundación** y su caravana | `aportarARefundacion`, `retirarDeRefundacion` y `comprarCaravanaDeRefundacion`. Solo con Facción y sin plaza. Datos: `fondos` de tu Facción y `FUNDACION` | Nada | **Esperar** al paso 8 (Facción): sin Facción no sale. Medio |
| **Tablón de mercenarios por contrato** (contratar, prorrogar, ordenar, mudar, despedir, valorar, premios de rango) | `tablon` y 7 órdenes (`ui/mercenarios.ts`, 772 líneas) | Nada | **Paso 12.** Lo natural es la taberna de la planta. Alto |
| **Taberna** (intel y rumores) | §3.16 | Nada | **Paso 12** |
| **Lo que llevas** (almacén personal, carro) | `Heroe.almacen_personal`, `carro`, `fuera_carro` | El inventario de Conquest (`InventoryPanelController`), de objetos y de `HeroData` | **Paso 6** (equipo y tropa). En el campamento basta lo que ya enseña la ventana de salida |
| **Salir andando por la puerta** | La misma `salirDelCampamento` | `DoorInteractable` (la puerta del feudo, con aviso flotante y F) | **Proponer:** una zona en la puerta de la empalizada que abra «Salir al mundo» al llegar (el prototipo no anda; aquí tiene sentido). El botón de la cabecera se queda. Bajo |

## Pruebas

- **`CosasDelMapaTests`** (EditMode): con la columna en marcha no hay acciones a la puerta; a la puerta de un campamento, parada, sí
  sale «Entrar».
- **`PantallasDeMundoTests.CrearCuentaEntrarYCerrarSesion`** (PlayMode, contra un servidor del motor propio, 358af5ff):
  1. Cuenta nueva y nacer (paso 3).
  2. El interior: FeudoScene cargada, lo de relleno apagado, sin héroe de `player_save.json`, el campamento montado y la heroína en la
     puerta.
  3. Capturas en los cuatro tamaños (la escena 3D y la cabecera).
  4. La heroína anda con W (teclado del Input System) y se mide el rendimiento.
  5. «Salir al mundo»: su ventana en los cuatro tamaños, y el mapa con la columna fuera.
  6. Marchar con un clic.
  7. Vuelta al campamento con «Marchar aquí». Mientras llega no hay «Entrar»; al parar a la puerta, sí. «Entrar» lleva al interior
     otra vez, con un héroe nuevo en el World.
  8. Salir y seguir con el resto del paso 2.
- **`PantallasDeMundoTests.ConHeroeSaleElSelectorDePersonaje`:** el héroe nació en un campamento y sigue dentro, así que tras «Entrar al
  mundo» sale su interior, y de ahí al mapa.
- **`PantallasDeMundoTests.ElCampamentoPorDentro`** (PlayMode, contra el motor 358af5ff). El servidor de prueba da 300 de oro al héroe
  con la orden de administración `dar_almacen_personal`, desde una cuenta admin registrada con el código de un solo uso que imprime al
  arrancar (`BRONZEAGE_PRUEBA_ADMIN`; `Sesion.Tipo = ClienteAdminWeb`), porque el héroe recién nacido no tiene oro. La prueba recorre:
  1. **Las cuatro zonas, andando de verdad.** Desde la puerta hasta la taberna, el mercado, el edificio militar y la salida, con el
     teclado del Input System. La prueba busca un camino libre en una rejilla de 0,5 m con el radio del héroe (más un margen) contra las
     colisiones del campamento y lo sigue con W/A/S/D según la cámara (unos 9, 12, 12 y 27 m). Falla si no hay camino o si el héroe se
     queda tres segundos sin avanzar. En cada zona salen el cartel y, con la tecla F, el panel.
  2. **El bloqueo.** Con un panel abierto el héroe no anda; al cerrar, vuelve el control.
  3. **La taberna:** residente, sin «Residir aquí». El servidor de prueba le quita la residencia (`fijar_residencia`); al volver a
     pulsar F sale «Residir aquí» y, pulsado, el motor lo acepta y la taberna vuelve a «tu residencia».
  4. **El mercado:** se compra una unidad y aparece en el almacén, con la línea «Compraste…».
  5. **La tropa:** se pide prestada la milicia de lanceros y se ve como prestada. Si hay tropa ofrecida con oro bastante, se recluta;
     si no, sale su motivo.
  6. **Un rechazo de cada orden**, con su frase en español: `residencia.cooldown` (recién mudado), `mercenarios.cantidad_invalida`,
     `mercenarios.tropa_no_disponible` y `mercenarios.ya_prestada`.
  7. **La salida.** Andando hasta la zona de la puerta, el cartel y F abren la ventana de salida, con la escuadra prestada y el
     cobre comprado como carga. «Llenar víveres» lleva de 0 a 350. Al salir, la columna lleva esa escuadra y ese cobre en el carro.
  8. **Los cuatro tamaños** de ventana, con lo que se pulsa dentro de su placa, para la taberna, el mercado, la tropa y la ventana de
     salida.
- **Sin verificar contra el motor:**
  - **Reclutar aceptado:** en una partida nueva el campamento no ofrece aún ninguna tropa; todas esperan a su tecnología.
- **Las capturas a otros tamaños** (`EnTamanos`) se hacen en texturas nuevas, que traen lo que hubiera en esa memoria de la tarjeta. De ahí
  salía el mapa detrás del mercado a 1280×720. Ahora se limpian antes, y la prueba comprueba que dentro del campamento solo está encendida
  la cámara de FeudoScene: en el juego no hay mapa detrás.
