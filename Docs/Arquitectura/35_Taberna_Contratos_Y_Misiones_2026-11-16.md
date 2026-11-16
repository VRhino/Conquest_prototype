# 35. La Taberna, los contratos y las misiones (paso 12 del bloque 4)

2026-11-16. Lo que BronzeAgeClient hace con la intel de la taberna, los mercenarios por contrato y las misiones (§3.15, §3.16 y §3.17 del
inventario), en Conquest. El plan, las respuestas de Víctor y las dudas están en `tasks/bloque4-paso12-plan.md`.

## Tanda 1: la Taberna (2026-11-16, en commit `eced9885`)

- **El panel Intel** (`PantallasDeMundo.Taberna.cs`, calcado de `renderPanelIntel`) sirve igual para las tabernas de la plaza y del campamento.
  - **A la izquierda**:
    - la taberna con su oro y sus Miradas abiertas (👁 n/cupo);
    - Mirada e informe de plaza, de dos en dos:
      - **Mirada**: alrededor de una plaza ajena o de un campamento, o de unas coordenadas. Lleva su precio (la base más un tanto por unidad
        de distancia a tus ojos: la taberna, tus plazas y tus columnas) y se apaga con la taberna al límite;
      - **informe de plaza**: de una plaza ajena que conoces, sin uno reciente, con su precio por nivel en la lista;
    - debajo, a lo ancho:
      - el **informe de región**, solo en la plaza (el del paso 10), ahora el de la región de la plaza;
      - los **rumores**: las vetas de estaño de la región, solo si la región tiene alguna, o «Tu Facción ya lo sabe».
  - **A la derecha**: tus Miradas (abiertas o enfriándose), tus informes de plaza y tus informes de región.
    - El informe de plaza que abras sale con sus edificios en iconos, su guarnición en miniaturas y sus murallas con lo levantado.
  - Los «Comprar» llevan el texto y el precio en oro uno junto al otro. El del informe de región del paso 10 tenía el mismo choque.
- **Quién paga y quién compra** (Víctor 1a y 7a; el motor, `taberna.rs`):
  - en la plaza, el Rey, el Embajador o el Gobernador, con el oro del almacén;
  - en el campamento, cualquiera con Facción, con su almacén personal si reside o con el carro de su columna si no.
- **La región**: con el raster `region` del artefacto (`RegionEn`) y sus nombres (`NombreDeRegion`). El informe de región ya no toma el último
  de su tipo de cualquier región.
- **Dónde se abre**:
  - en la plaza, la Taberna del riel, pestaña «Intel»;
  - en el campamento, la sección «Intel» de su Taberna, con Facción;
  - a la puerta del campamento, el botón «Taberna» en la ficha del mapa, con Facción y la columna quieta a la puerta (Víctor 3a).
- **«Mercenarios de la región» pasa a la tanda 2**: es el mismo tablón de contratar que el del campamento. Hasta entonces, la Taberna de la
  plaza sale solo a quien tiene cargo, porque sin él no tendría nada dentro.
- **La prueba** es `TuTaberna`:
  - el Gobernador, con oro, compra una Mirada sobre un campamento (sale en «Tus Miradas») y el informe de región de sus plazas (sale en
    «Tus informes de región»);
  - el informe de plaza solo si conoce alguna ajena: en la prueba no la conoce hasta comprar el de región.
  - Con `MUNDO_PROTO=taberna` solo deja la escena para `tasks/pruebas/cdp/taberna.mjs`.

## Tanda 2: los contratos (2026-11-16, a revisar)

Todo en `PantallasDeMundo.Mercenarios.cs`, calcado de `ui/mercenarios.ts`.

- **El tablón** (`TablonDeMercenarios`, `htmlTablonMercenarios`):
  - cada oferta trae:
    - la insignia de rango (el escudo con sus galones, dibujado con `Painter2D`);
    - el renombre con su barra, las medallas con su icono y su grado, el historial y su tropa en miniaturas (`PiezasDeTropa`);
  - «Plazo y precio» en fichas (1, 3 o 7 días y Permanente): el precio se ve siempre y se elige solo lo que se puede firmar;
  - los trabajos que alcanzan tus cargos (`PuestoParaFirmar`, §3.2) y el objetivo en un desplegable por cercanía;
  - «Firmarás como …» y «Contratar» con el precio en oro o en monedas y su icono;
  - «Pagar con» si hay más de una forma y «Ordenar por renombre o precio».
- **Dónde sale**:
  - en la pieza Militar del campamento, pestaña «Mercenarios» junto a «Tropa» (Víctor 2b). Lo paga tu almacén personal si resides, el carro
    que llevas o tus monedas;
  - a la puerta del campamento, el botón «Mercenarios» de la ficha del mapa (Víctor 3a);
  - en las dos, solo con Facción y si alguien se ofrece, como el prototipo;
  - en la Taberna de la plaza, «Mercenarios de la región» (Víctor 1a), que paga el Rey, el Embajador o el Gobernador con el fondo de monedas
    de la plaza (`fondoPlaza`) o con las suyas, y los demás cargos con sus monedas. La Taberna se abre ya a quien puede firmar algún trabajo, y cada
    pestaña sale solo a quien la puede usar.
- **Facción › Mercenarios** (`PestanaMercenarios`, entre Tecnología y Diplomacia, con la cifra de lo que pide atención):
  - **«Contratos acabados: valóralos»**, con 👍 Buena y 👎 Mala:
    - salen del hecho `mercenarios.terminado`;
    - se guardan en el equipo por héroe (`ContratosPorValorar`, PlayerPrefs) hasta que caduca el plazo de valoración (Víctor 5b);
    - de cada contrato vigente se recuerda quién lo firmó, que el hecho no trae;
  - el **resumen** (a sueldo, temporales, permanentes, sin orden);
  - la **barra de órdenes** (Víctor 4a):
    - hasta 16 marcados, con «Marcar todos» y «Ninguno»;
    - las cuatro órdenes, encendidas solo las que admiten todos los marcados y tus cargos (`OrdenesPosibles`);
    - el objetivo entre los que valen, o el punto de «Ir a» con sus coordenadas;
  - cada **contrato**:
    - marcar, la insignia y dónde anda;
    - el trabajo y su objetivo, el plazo con su barra (en rojo al quedar poco) y la orden vigente;
    - quién firmó (✍);
    - «Prorrogar» (+1, +3 o +7 días, con oro o con monedas), «Mudar» (el permanente, el Rey o el Embajador) y «Despedir», con su confirmación.
- **Los avisos** de `mercenarios.*` (contratado, prorrogado, terminado, valorado, ordenado, orden cumplida y caducada, mudado), en `Avisos`.
- **El campamento por dentro pide la vista del mundo**, como la plaza: lo que «Atacar» ataca (bandidos, plazas en guerra) llega por la `VISTA`.
  - Una Facción sin plaza ni columna no tiene ojos: ve los bandidos solo con una Mirada.
- **Los iconos**: nueve Twemoji nuevos con permiso de Víctor: 🐂 ✋ 🏹 🧱 🔥 ⚓ 👍 👎 ✍.
- **Sin el 📍 del prototipo** (centrar el mapa en el mercenario o su objetivo): Conquest no tiene aún cómo centrar el mapa desde una ventana.
- **La prueba** es `TuTablonDeMercenarios`:
  - un bot se ofrece (una cuenta pasada a bot con `fijar_controlador`);
  - el Rey de una Facción sin plaza, en el mismo campamento, compra una Mirada sobre unos bandidos y lo contrata 1 día para atacarlos desde la
    pieza Militar;
  - en Facción › Mercenarios lo marca, le da «Ir a», prorroga, despide y lo valora «Buena».
  - Con `MUNDO_PROTO=mercenarios` solo deja la escena para `tasks/pruebas/cdp/mercenarios.mjs`.
  - «Mercenarios de la región» no tiene prueba propia: el servidor de pruebas no da monedas ni llena el fondo de la plaza.
