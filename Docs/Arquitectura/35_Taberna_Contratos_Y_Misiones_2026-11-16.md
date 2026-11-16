# 35. La Taberna, los contratos y las misiones (paso 12 del bloque 4)

2026-11-16. Lo que BronzeAgeClient hace con la intel de la taberna, los mercenarios por contrato y las misiones (§3.15, §3.16 y §3.17 del
inventario), en Conquest. El plan, las respuestas de Víctor y las dudas están en `tasks/bloque4-paso12-plan.md`.

## Tanda 1: la Taberna (2026-11-16, a revisar)

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
