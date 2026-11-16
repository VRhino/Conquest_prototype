# Paso 12: mercenarios por contrato, taberna y misiones (análisis)

2026-11-16. Solo texto, sin capturas del «antes».

## Qué abarca

La hoja de ruta (bloque 4, paso 12) y el inventario (§3.15, §3.16 y §3.17):
- **§3.15, contratos**: el tablón de mercenarios del campamento y lo que se hace después con ellos (contratar, prorrogar, ordenar, mudar,
  despedir y valorar). El interior del campamento ya está, de los pasos 4 y 8.
- **§3.16, la Taberna**: Miradas, informe de plaza, rumores, informe de región, órdenes a distancia y mercenarios de la región.
- **§3.17, misiones**: el diario, el rastreador y los tablones de recurrentes (7 diarias y 7 semanales) de la plaza y de la Facción.

## Lo que hay

### En BronzeAgeClient

- **La Taberna** (`panelIntel.ts`, `tabernaRegion.ts`, `rumores.ts`):
  - **Dónde está**:
    - en la plaza donde estás, o en una tuya con tu columna a la puerta, con la taberna activa;
    - en el campamento donde estás o a cuya puerta tienes la columna, si tienes Facción.
  - **Quién paga**: en la plaza, su almacén; en el campamento, el almacén personal si resides o el carro si no.
  - **A la izquierda**:
    - la taberna, si hay varias, y el oro y el cupo de Miradas;
    - **Mirada**: alrededor de una plaza ajena, un campamento o un punto, con su precio por distancia;
    - **informe de plaza**: de una plaza ajena conocida que no esté en espera;
    - **informe de región**: plazas u órdenes;
    - **rumores**: vetas de estaño.
  - **A la derecha**: tus Miradas, tus informes (edificios, recintos y guarnición) y tus informes de región.
  - **Dónde se abre**:
    - en la plaza, el riel «Taberna», con las subpestañas Intel y «Mercenarios de la región»;
    - en el campamento, la parte «Taberna»;
    - a la puerta de los dos, desde la ficha del mapa.
  - Las órdenes a distancia van en «Región» del Mercado.
- **Los contratos** (`mercenarios.ts`):
  - **El tablón** (campamento, y «Mercenarios de la región» en la Taberna de la plaza): cada oferta con su rango, sus medallas y su tropa.
    - Plazos 1, 3 o 7 días, o «Permanente», cada uno con su precio en oro o monedas.
    - Los trabajos que tus cargos alcanzan (defender, escoltar, atacar, acompañar), con el objetivo en un desplegable por cercanía.
    - «Firmarás como …», «Pagar con» y «Ordenar por renombre o precio».
    - La orden es `contratarMercenario`.
  - **Facción › Mercenarios**:
    - el resumen;
    - una **barra de órdenes** (hasta 16 marcados: guardar, ir a, atacar, unirse), `ordenarMercenario`;
    - cada contrato con su plazo, «Prorrogar» (+1, +3 o +7, en oro o monedas), «Mudar» (permanente; el Rey o el Embajador) y «Despedir»;
    - «Valóralos» 👍 o 👎 (`valorarMercenario`), sacado de los hechos `mercenarios.terminado` que guarda el cliente.
  - **El premio de rango** (`reclamarPremioDeRango`) es solo de bots.
- **Las misiones** (`misiones.ts`, `tablon.ts`):
  - el botón «Misiones» en la barra, con insignia de las que puedes reclamar;
  - la ventana diario (Mías, Mi plaza, Mi Facción), con «Seguir» (hasta 3) y «Reclamar»;
  - el **rastreador** de las seguidas: arriba a la derecha en el mundo, en el lateral derecho de la plaza y a la izquierda en el campamento;
  - el **tablón**, en columnas Diarias y Semanales, con «Aceptar» y «Reclamar»; el Gobernador o el Rey eligen las de la semana que viene
    (`elegirTablon`);
  - dentro de la plaza, la pestaña «Tablón de X»; el de la Facción, en Facción › Tablón.

### En el motor (main)

- **Las órdenes existen todas.**
  - Intel: `comprarMirada`, `comprarInformePlaza`, `comprarRumor`, `comprarInformeRegion` y `tomarOrdenADistancia`.
  - Contratos: `contratarMercenario`, `prorrogarContrato`, `ordenarMercenario`, `mudarMercenario`, `despedirMercenario`, `valorarMercenario`
    y `reclamarPremioDeRango`.
  - Misiones: `aceptarMision`, `elegirTablon` y `reclamarRecompensa`.
- **Reglas que mira el motor**:
  - **Toda compra de intel en una plaza** (Mirada, informe y rumor, no solo el de región) pide estar dentro o a la puerta y ser Rey, Embajador
    o Gobernador (`Autoriza::CompraIntel`).
    - El prototipo enseña Mirada e informe a cualquiera y deja que el motor rechace.
    - En el campamento basta la Facción.
  - **Contratos**:
    - contratar y prorrogar son en persona;
    - ordenar, mudar y despedir, a distancia;
    - cada trabajo pide su cargo;
    - con monedas, cualquiera.
  - **Misiones**:
    - las de plaza las reclama el Gobernador (o un residente si no hay);
    - las de Facción, el Rey;
    - aceptar tiene tope semanal y puede pedir cargo.
- **Qué llega y dónde**:
  - las misiones y el contrato del héroe → HEROE;
  - las misiones, el tablón, los contratos y los informes de región de la Facción → FACCION;
  - las misiones y el tablón de la plaza, y sus mercenarios de la región, → **solo desde dentro** (ASENTAMIENTO);
  - el tablón del campamento → CAMPAMENTO (dentro o con la columna parada a la puerta);
  - las Miradas y los informes de plaza → VISTA.
- **Los datos de `/v1/datos`**: `INTEL`, `MERCENARIOS_CONTRATO`, `TRABAJOS_MERCENARIOS`, `MEDALLAS_MERCENARIOS`, `MISIONES`,
  `MISIONES_RECURRENTES` y `TEXTOS_MISIONES`.
- **No hay lista de contratos por valorar**: la saca el cliente de los hechos.

### En Conquest

- **La Taberna de la plaza** (paso 10): el riel solo con el informe de región, a quien compra (`CompraInforme`, `InformeDeRegion` en
  `PantallasDeMundo.Rutas.cs`), y «Región» del Mercado con las órdenes a distancia.
  - El último informe se busca **sin mirar la región de la plaza**: se corrige aquí.
- **El campamento** (paso 4): las piezas Taberna (residir, tu tropa, tu almacén y el Fondo), Mercado y Militar (reclutar y préstamo), y la
  puerta. Falta la intel y el tablón de mercenarios. Su ficha del mapa solo trae «Entrar».
- **Las misiones**: solo el aviso `mision.cumplida`, con su título (`TEXTOS_MISIONES`). No hay botón, diario, rastreador ni tablón.
- **El lateral derecho de la plaza**: las tarjetas del ejército y de las caravanas (paso 11). Víctor quiere las misiones **arriba**, encima de
  ellas.
- **Facción** no tiene las pestañas Tablón, Misiones ni Mercenarios.
- **Para reutilizar**:
  - `PiezasDeTropa` (la tropa de las ofertas);
  - las Miradas, ya pintadas en el mapa;
  - el aviso `asentamiento.informe_pedido`;
  - los textos de todos los rechazos `intel.*`, `mercenario.*`, `mision.*` y `tablon.*`, ya en `Textos.cs`;
  - el patrón de tarjetas del lateral.

## Lo que voy a hacer

- **Calcar el prototipo** panel por panel, con sus iconos. Solo sale lo que el jugador puede hacer.
- **Tres tandas, con parada al acabar cada una**:
  - **Tanda 1, la Taberna**:
    - en la plaza, Intel (Mirada, informe de plaza, rumores, informe de región, tus Miradas, tus informes y tus informes de región con su
      detalle) y «Mercenarios de la región» (el tablón, pagado con el almacén o el fondo de la plaza, o con monedas);
    - en el campamento, su Taberna con la intel;
    - el informe de región, el de la región de la plaza.
    - Prueba: el Gobernador compra una Mirada y un informe de plaza, y lo ve en el mapa y en «Tus informes».
  - **Tanda 2, los contratos**:
    - el tablón del campamento (contratar con su plazo, trabajo, objetivo y pago);
    - Facción › Mercenarios: la barra de órdenes, prorrogar, mudar, despedir y valorar;
    - los avisos de `mercenarios.*`.
    - Prueba: contratar a un mercenario de un campamento, darle una orden, prorrogarlo y despedirlo.
  - **Tanda 3, las misiones**:
    - el botón «Misiones» con su insignia y su ventana diario (Mías, Mi plaza, Mi Facción, seguir y reclamar);
    - el rastreador, arriba en el lateral derecho de la plaza (encima de las tarjetas del ejército) y en el mapa;
    - los tablones de la plaza (dentro) y de la Facción (Facción › Tablón), con aceptar, reclamar y elegir la semana.
    - Prueba: aceptar una misión del tablón, cumplirla, verla en el rastreador y reclamarla.
- **El premio de rango no entra**: es solo de bots.
- **Pruebas** con ratón y teclado a los cuatro tamaños, con las capturas junto al prototipo. Al acabar cada tanda, la suite en dos lotes y
  las dos batallas.

## Respuestas de Víctor (2026-11-16)

- **1a**: la Taberna de la plaza se abre con la taberna activa y dentro sale solo lo que tu cargo permite. **Para pagar**:
  - quien tiene cargo paga con el **fondo de monedas de la plaza**;
  - los demás no ven el fondo y pagan con **sus monedas**.
- **2b**: el tablón de mercenarios del campamento va en la pieza **Militar**, junto a reclutar.
- **3a**: también a la puerta del campamento, con un botón en la ficha del mapa.
- **4a**: la barra de órdenes, calcada en Facción › Mercenarios.
- **5b**: los contratos por valorar se guardan en el equipo (PlayerPrefs) hasta que caducan.
- **6**: el rastreador de misiones va **arriba a la derecha en todas partes** (mapa, plaza y campamento), por coherencia.

- **Lo que dice el motor del pago (1a)**:
  - **Los mercenarios** (`contratos/firma.rs`) admiten tres formas de pago:
    - el almacén o el fondo de la plaza, solo el Rey, el Embajador o el Gobernador, dentro;
    - las monedas del héroe, cualquiera.
    - Con cargo, el fondo; sin cargo, las monedas.
  - **La intel de la plaza** (`taberna.rs`, `resolver_compra` y `cobrar`) es solo con cargo y se cobra siempre en **oro del almacén** de la
    plaza: no admite el fondo ni las monedas. Sin cargo no se enseña.
  - **Duda 7, contestada (7a, 2026-11-16)**: la intel de la plaza se paga con el oro del almacén, como el motor. Antes era:
    - a) con el oro del almacén, como el motor **(propuesta; con ella empieza la tanda 1)**;
    - b) con el fondo, cambiando el motor.

## Dudas (contestadas arriba)

1. **Quién ve la Taberna de la plaza.** El motor pide cargo (Rey, Embajador o Gobernador) para toda la intel de plaza. Los mercenarios de la
   región se pagan también con monedas, sin cargo.
   - a) Abrirla con la taberna activa y, dentro, solo lo que tu cargo permite: la intel con cargo y los mercenarios con monedas para
     cualquiera. **(propuesta)**
   - b) Solo con cargo, como hoy.
   - c) Todo a la vista, como el prototipo, y que rechace el motor.
2. **El tablón de mercenarios en el campamento.**
   - a) En la pieza Taberna, como otra pestaña junto a la intel. **(propuesta)**
   - b) En la pieza Militar, junto a reclutar.
   - c) En un panel propio.
3. **Taberna y mercenarios desde la puerta del campamento** (el prototipo los abre desde la ficha del mapa; el motor manda el tablón con la
   columna parada a la puerta).
   - a) También a la puerta, con un botón en la ficha. **(propuesta)**
   - b) Solo dentro.
4. **La barra de órdenes a los mercenarios** (hasta 16 marcados).
   - a) Calcarla en Facción › Mercenarios. **(propuesta)**
   - b) Una orden por contrato, sin marcar varios.
5. **Valorar un contrato acabado.** El motor no manda cuáles faltan por valorar; el prototipo los saca de los hechos y los pierde al recargar.
   - a) Como el prototipo.
   - b) Guardarlos en el equipo (PlayerPrefs) hasta que caduquen. **(propuesta)**
   - c) Pedir al motor un campo nuevo.
6. **El rastreador de misiones fuera de la plaza.**
   - a) También en el mapa, arriba a la derecha como el prototipo, y a la izquierda en el campamento. **(propuesta)**
   - b) Solo en la plaza; en el mapa, el botón «Misiones».
