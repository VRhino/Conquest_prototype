# 26. El interior de un campamento de mercenarios (2026-11-13)

Paso 4 del bloque 4 de [`Hoja_De_Ruta.md`](../Hoja_De_Ruta.md) (inventario §3.15 y §3.8). Va en dos tandas:

- **4A** (esta): estar dentro y salir. Cuando el héroe está dentro de un campamento de mercenarios, se ve el interior en FeudoScene: el
  héroe del motor andando a pie en tercera persona por el campamento que manda el motor. Desde ahí se sale al mundo.
- **4B**: las interfaces (residir, comprar, reclutar…), una por una, según decida el autor sobre la propuesta del final.

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

## Propuesta para la 4B: interfaz por interfaz

Lo que hace BronzeAgeClient en el campamento (`ui/pantallaCampamento.ts`, `ui/arbolReclutamiento.ts`, `ui/ventanaSalida.ts`), qué
hay en FeudoScene que podría alojarlo y la recomendación. **Lo decide el autor; nada de esto está hecho.**

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
