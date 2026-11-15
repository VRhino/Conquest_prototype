# Paso 7: la plaza por dentro (análisis y dudas)

2026-11-15. Víctor contestó «Continua»: las 13 dudas van como se propusieron (el minimapa, más adelante; su hueco, vacío).
Tandas 1 y 2 hechas; tanda 3 hecha (a revisar): `Docs/Arquitectura/30_Plaza_Por_Dentro_2026-11-15.md`.

## Qué abarca

La hoja de ruta (bloque 4, paso 7): «Plaza por dentro sobre FeudoScene, con su HUD, gobierno y defensa, y la armería» (§3.6, §3.7 y la
armería de §3.9 del inventario). Además, lo apuntado para aquí:
- la guarnición (duda 11 del paso 6);
- cargar y descargar el carro en la plaza y cerrar el almacén a los ciudadanos (duda 23 del paso 6);
- reclutar en la plaza (el árbol de la tanda 3 del paso 6, con `lugarPlaza` y `reclutarTropa`);
- el fallo de `SquadDetailPanel`, que cambia la capa del prefab compartido.

Capturas de lo que hay (el prototipo en una plaza fundada en mi servidor de prueba y los paneles de Conquest en FeudoScene): ver el
informe del paso 7.

## Lo que hay

### En BronzeAgeClient (plaza propia, `main.ts:2470-3208` y `ui/*`)

La plaza en 3D a pantalla completa (`ciudad3d/`, unas 4.500 líneas, 1.782 de ellas de modelos hechos a mano) con cámara libre (WASD,
Q/E, R/F, rueda) y el HUD encima:

| Sitio | Pieza |
|---|---|
| Arriba a la izquierda | Blasón: escudo, nombre (lápiz para renombrar), nivel en medallón («rinde como N»), casa (residencia) y corona (capital), con sus tarjetas. Debajo, la subida de nivel en curso y las obras (hasta 4 en marcha con su barra; la cola en casillas con ◀ ▶ ✕) |
| Arriba al centro | Placas Población (clases, techo, «+ Vivienda»), Almacén (hasta 8 recursos; abre la ventana Almacén) y Estado (mantenimiento y nutrición). Debajo, la franja de ocupación |
| Arriba a la derecha | Barra del jugador y rastreador de misiones; la ficha del edificio al pulsarlo |
| Abajo | «Salir al mundo», el riel de ventanas y el minimapa |

El riel, en orden (cada uno sale solo si toca: cargo, edificio activo, plaza propia):
1. **Construcción**: un cajón en lugar del riel; pestañas Ciudad, Recursos, Comida, Talleres, Militar, Gobierno, Otros; «Construidos»;
   auto-construcción. `anadirEdificioManualmente`, `alternarAutoConstruccion`.
2. **Gobierno**: Cargos (6 sillas; `asignarCargoLocal`; la participación) y Políticas (`activarPolitica`).
3. **Defensa**: Guarnición (`asignarGuarnicion`, `retirarGuarnicion`; cupo en Liderazgo), Muralla (plano, anillos;
   `comprometerRecinto`, `mejorarRecinto`, `abandonarRecinto`) y Puerta (`fijarPuerta`, `vetarJugador`, `alternarReabastecerAliados`).
4. **Territorio**: pueblos y aldeas, y Fundación (caravana de fundación).
5. **Reclutamiento**: el árbol `lugarPlaza` (`reclutarTropa`) y «Cerrar a los ciudadanos» (`cerrarReclutamientoACiudadanos`).
6. **Taberna** (intel y mercenarios de la región), 7. **Mercado**, 8. **Armería** (Tienda: `comprarObjeto`, `venderObjeto`; Forja:
   `forjarObjeto`, `configurarForja`), 9. **Unirse a un ejército**, 10. **Aedas**.

Fuera del riel: **Subir de nivel** (desde el medallón; `solicitarAscenso`), **Almacén** (desde la placa: reserva del Tesorero
`calibrarReservaManual`, `pausarConsumo`, talleres `alternarReceta`; «Tu carga»: `dejarEnPlaza`, `cargarDePlaza`,
`guardarEnAlmacenPersonal`, `sacarDelAlmacenPersonal`, `vaciarCarro`, `cerrarAlmacenACiudadanos`), la **ficha de edificio**
(`mejorarEdificioAhora`, `elegirReceta`, `asignarBuey`, `quitarDeCola`, `cancelarConstruccion`; desde el Barracón, la Armería, etc.
abre su ventana) y las obras (`moverEnCola`, `quitarDeCola`, `cancelarConstruccion`). El blasón: `renombrarAsentamiento`,
`cambiarResidencia`, `dejarResidencia`, `designarCapital`.

No se migra: F3, sondeo y repintado por diferencias, `#/legacy`, la vista 2D sin WebGL, los bancos.

### En Conquest

- **FeudoScene** en modo «desde el motor» apaga toda la interfaz UGUI de Conquest y sus zonas F (`InteriorDelMotor.Apagados`,
  doc 26). Lo de Conquest que se parece: barracas (`BarracksMenuUIController`, `SquadSelectionPanel`, `SquadDetailPanel`), detalle del
  héroe, inventario y la tienda del herrero (`UIStoreController`: «Inventory» a la izquierda, «Blacksmith Store» con «Buy» a la
  derecha).
- **El interior del campamento** (paso 4): `InteriorDelMotor.Montar` monta piezas de la planta como cajas con colisión, las calles y la
  empalizada planas, las zonas F (`ZonaDelInterior`) y abre paneles de UI Toolkit. Casi todo vale para la plaza; cambia:
  - los edificios vienen de `Asentamiento.edificios` (tipo, posición, ancho y alto, estado, nivel, mejora), no de la planta;
  - las calles, de `trazado.calles` y `trazado.caminos`;
  - la muralla no viene en rectángulos: viene en `recintos[].celdas` (muro, puerta, torre), en celdas de 3 u.l.
- **Datos:** el almacén del cliente ya guarda el `Asentamiento` entero del ámbito `ASENTAMIENTO:` (edificios, recintos, cargos,
  políticas, almacén, trazado, guarnición, forja, metales de la armería…). Hoy solo se usa su posición y su nombre: dentro de una plaza
  sale «Su interior llega con su paso».
- **Órdenes:** todas las de arriba están en el contrato; ninguna se manda hoy.
- **El fallo de `SquadDetailPanel`** (`Assets/Scripts/UI/SquadDetailPanel.UI.cs`, `instantiate3DModel`): `:288` cambia la capa del
  prefab compartido (`Squires_GO_Squeleton`) y `:283` apaga su `EntityVisualSync`, antes de instanciarlo. El arreglo: hacerlo en la copia.
- **Pruebas:** el servidor de prueba no tiene orden de administración para crear una plaza. Se funda con las órdenes del jugador
  (`CrearFaccion`, salir del campamento, `dar_caravana_fundacion`, marchar, `Fundar`) y se llena con `dar_recursos` y
  `fijar_nivel_asentamiento`. Así fundé la de las capturas; quien funda es Gobernador de oficio y reside en ella.

## Tandas (con parada en cada una)

1. **Entrar en tu plaza y su HUD.**
   - El interior desde el motor: edificios por tipo, nivel y estado (en cola, en obra, activo), calles y muralla por celdas, y el héroe
     andando.
   - El blasón con sus tarjetas, el nivel y «Subir de nivel», y las obras y la cola.
   - Las placas Población, Almacén y Estado, el riel, «Salir al mundo» y el minimapa.
   - La ficha de cada edificio, con sus recetas, «Mejorar» y la cola.
   - El arreglo de `SquadDetailPanel`.
2. **Construir y gobernar.**
   - Construcción (el cajón con su rejilla y la auto-construcción).
   - Gobierno (cargos y políticas).
   - La ventana Almacén: reserva, consumo pausado y talleres; «Tu carga» con dejar y cargar en la plaza y cerrar el almacén.
3. **Defender, reclutar y la armería.**
   - Defensa: guarnición, muralla y puerta con aliados.
   - Reclutamiento: el árbol de la tanda 3 del paso 6 con la plaza y «Cerrar a los ciudadanos».
   - La armería: tienda y forja.
   - Una plaza ajena de visita, con lo que deje.

Fuera de este paso, por su paso propio:
- Territorio y Fundación: paso 9, con la caravana del paso 8.
- Mercado: paso 10.
- Unirse a un ejército: paso 11.
- Taberna: paso 12.
- Aedas: paso 13.

## Dudas (con mi propuesta)

1. **Cómo se ve la plaza.** El prototipo usa una cámara libre de ciudad; Conquest anda con el héroe, como en el campamento (lo decidido
   el 2026-11-13). Propongo andar con el héroe y que cada edificio se abra con F en su zona, como el campamento. ¿Quieres también el
   clic en el edificio?
2. **Los edificios en 3D.** Propongo cajas por tipo con su color, como el campamento, más bajas y en gris mientras están en cola o en
   obra, con el cartel de la obra. Los modelos de verdad, después. ¿Bien?
3. **La muralla.** Viene por celdas (muro, puerta y torre). Propongo una caja por celda, con torres más altas y la puerta como hueco. La
   puerta coloca al héroe al entrar y abre «Salir al mundo». ¿Bien?
4. **El HUD.** Propongo calcarlo del prototipo: blasón arriba a la izquierda, placas arriba en el centro, riel abajo, «Salir al mundo»
   abajo a la izquierda y minimapa abajo a la derecha, con la barra del jugador como en el mapa. ¿Quieres el minimapa ya, o después?
5. **La ficha del edificio.** El prototipo la saca en el borde derecho. Propongo lo mismo: con F se abre su ficha y, si el edificio
   tiene ventana (Barracón, Armería), un botón la abre. ¿O que F abra directamente la ventana del Barracón y de la Armería?
6. **La armería.** Hay dos opciones: calcar la del prototipo (Tienda y Forja) o adaptar la tienda de Conquest («Inventory» y
   «Blacksmith Store»). Propongo una mezcla, como con el equipo: la estructura y las órdenes del prototipo, con la mochila a la izquierda
   y las fichas de objeto de Conquest de la tanda 2. ¿Bien?
7. **Reclutar en la plaza.** Propongo el mismo árbol del campamento, entero, con los estados de la plaza (falta su tecnología, falta el
   edificio a su nivel, lista, tuya), con las tecnologías de tu Facción que ya llegan. Sin «Adoptar», que es del paso 8. ¿Bien?
8. **La guarnición.** Va en Defensa > Guarnición, como el prototipo, y no en la Base. ¿Bien?
9. **Cargar y descargar en la plaza.** Va en la ventana Almacén > «Tu carga», como el prototipo, y no en Bienes de la Base. ¿Bien?
10. **Las ventanas de otros pasos** (Territorio, Mercado, Taberna, Unirse, Aedas). Propongo que sus botones no salgan en el riel hasta
    su paso, sin mensaje. ¿Bien?
11. **Una plaza ajena.** El prototipo enseña lo de visita. Propongo dejarlo para la tanda 3, con lo que haya de esos pasos. ¿Bien?
12. **El fallo de `SquadDetailPanel`.** No entra en el modo del motor, porque esa interfaz está apagada, pero estropea el prefab en el
    modo de siempre. Propongo arreglarlo en la tanda 1: son dos líneas, aplicadas a la copia y no al prefab. ¿Bien?
13. **Las pruebas.** Como no hay orden de administración para crear una plaza, la prueba la funda con las órdenes del jugador (crea su
    Facción, sale con caravana, marcha y funda). ¿Lo hago así, o pedimos al motor una orden de administración `crear_asentamiento`?
