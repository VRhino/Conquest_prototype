# 33. El Mercado y las caravanas (paso 10 del bloque 4)

2026-11-16. Lo que BronzeAgeClient hace con el comercio de la plaza (§3.14 del inventario), en Conquest. El plan, las dudas y las respuestas de
Víctor están en `tasks/bloque4-paso10-plan.md`.

## Tanda 1: las órdenes del Mercado (2026-11-16, a revisar)

- **Mercado en el riel de la plaza**, entre Reclutamiento y la Armería.
  - Sale con el edificio activo (`mercado_activo` o un Mercado en pie), también de visita.
  - La ficha del Mercado y la del Albergue de caravanas tienen «Abrir el Mercado».
  - La firma del riel incluye ahora el Mercado y la Armería activos. Antes, la Armería recién acabada no aparecía hasta otro cambio.
- **En tu plaza, la pestaña Órdenes** (`PantallasDeMundo.Mercado.cs`, calcada de `htmlOrdenes`). A ventana completa:
  - a la izquierda, la boleta: ▲ Vender o ▼ Comprar, el almacén como casillas (primero lo que hay), el recurso elegido, la cantidad, el precio
    («mercado» si va vacío) y el total;
  - a la derecha, las órdenes en pie: el recurso con su flecha, «Vende/Compra N», la barra de lo cumplido, el precio y lo que les queda.
  - **«✕»** cancela una orden (`cancelarOrdenMercado`; el prototipo no la manda).
  - La boleta y el ✕ salen solo al Gobernador o al Tesorero residente: el motor no deja a nadie más (`comando.rs`), aunque el prototipo
    enseñe la boleta a cualquier residente.
- **En una plaza ajena**, sus órdenes abiertas (`htmlMercadoDePlaza`): la cantidad y «Comprar» o «Vender», que el Líder de la columna toma con
  su carro (`comerciarEnPlaza`).
- **Desde el mapa**: con la columna a la puerta de una plaza con Mercado, su ficha tiene «Mercado».
  - En tu plaza, si gestionas las órdenes, abre la pestaña Órdenes; si no, sus órdenes abiertas.
  - La ventana es ancha (900 px), con la boleta arriba y las órdenes en pie debajo, como la del prototipo.
- **Textos**: los rechazos de `mercado.*`, `orden.*` y `mostrador.*`, y el aviso `mercado.orden_cancelada`.
- **Cantidades y precios** van en milésimas, como en el resto del contrato. El precio de mercado del trigo en el servidor de prueba es
  945,78 de oro, igual en el prototipo.
- **La prueba** es `TuMercado` (`PantallasDeMundoTests.Mercado.cs`).
  - La plaza se funda con `mercadoEnPie`: el Mercado va entre las obras cortas, en lugar de la bodega, porque la cola admite cuatro.
  - El recorrido: el riel, vender madera a 2, pedir trigo a precio de mercado, cancelar la venta y la ventana Mercado desde la ficha de la
    plaza en el mapa.
