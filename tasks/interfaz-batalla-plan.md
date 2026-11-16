# La interfaz de la batalla sobre la de Conquest (análisis)

2026-11-16. Solo texto. El prototipo no tiene interfaz de batalla (Víctor): se hace con la de Conquest (UGUI de BattleScene: `HUDController`,
`SquadSectionController`, `SupplyPointUi.prefab`) y con sus reglas: solo lo que se puede hacer, sin huecos vacíos ni botones apagados.

## Lo que hay hoy

1. **El aviso de conexión** (`Net/ReconexionClienteSystem.cs`, `AvisoDeRed`): una caja de `OnGUI` arriba en el centro con el texto de
   `ReconexionClienteSystem.Aviso`:
   - «Conectando con el servidor…»;
   - «Sin noticias del servidor…»;
   - «Conexión perdida. Reconectando…»;
   - «El servidor cerró la conexión.»;
   - «Has abandonado la batalla.»;
   - los recados, como «Pulsa F10 otra vez para abandonar».
2. **Abandonar** tiene dos caminos:
   - F10 dos veces en 3 s (`ClienteRedSystems.cs:31-49`);
   - el botón «Abandonar la batalla» arriba a la derecha (`PantallasDeMundo.Informe.cs`, en la capa de UI Toolkit del mundo encima de
     BattleScene), con su confirmación. Es el que pulsa `DelMapaALaBatalla`.
3. **El panel de suministro** (`UI/Zone/SupplyPointUIController.cs` y `SupplyPointUi.prefab`):
   - el visor de tropas pinta 10 huecos «+» vacíos detrás de las escuadras (`TroopsViewerController.RenderPlaceholder`);
   - «Aceptar» sale apagado hasta elegir;
   - Esc lo cierra.

## Lo que haría

1. **El aviso**: una banda UGUI arriba en el centro, con el panel oscuro y el texto del HUD de Conquest, en lugar de la caja de `OnGUI`.
   - Los mismos textos.
   - «Reconectando…» con su cuenta atrás hasta darla por perdida (60 s).
   - Desaparece cuando todo va bien.
2. **Abandonar**: Esc abre un menú pequeño de la batalla (UGUI) con «Volver a la batalla» y «Abandonar la batalla». «Abandonar» pide
   confirmación en el mismo menú: «Tu héroe sale con las bajas que lleve y queda herido» y «Abandonar» / «Seguir».
   - Fuera F10 y el botón de UI Toolkit de arriba a la derecha.
   - Esc ya lo usa el panel de suministro para cerrarse. Si está abierto, Esc lo cierra y no abre el menú.
3. **El panel de suministro**:
   - fuera los huecos vacíos: solo las escuadras que puedes sacar (las elegidas, sin la activa ni las eliminadas);
   - «Cambiar» no existe hasta que eliges una: sale al elegir, en vez de estar apagado;
   - sin escuadras que sacar, el panel no se abre y el aviso de F no sale.

**Pruebas:**
- `DelMapaALaBatalla`:
  - abandona con Esc, «Abandonar la batalla» y su confirmación, en vez del botón de UI Toolkit;
  - el panel de suministro, sin huecos, con «Cambiar» solo tras elegir.
- **El aviso:** sin red no se puede forzar en la prueba de punta a punta. Una prueba PlayMode pequeña pone `Aviso` y mira que la banda sale con
  el texto, y que se va sin él.

## Dudas

1. **Cómo se abandona.**
   - a) Esc abre el menú de la batalla, con «Abandonar la batalla» y su confirmación. Fuera F10 y el botón de arriba a la derecha.
     **(propuesta)**
   - b) Un botón fijo en el HUD de la batalla (UGUI), con su confirmación. Fuera F10.
   - c) Las dos cosas.
2. **Cambiar de escuadra en el panel.**
   - a) Clic en la escuadra para elegirla y «Cambiar» para confirmar. «Cambiar» solo sale con una elegida. **(propuesta)**
   - b) Clic en la escuadra y cambia ya, sin confirmar (un clic de más, y se gasta el cambio).

## Respuestas de Víctor (2026-11-16)

- **14:** no hay menú nuevo. Se reutiliza el de Opciones (el ☰ del mapa), que en la batalla abre Esc, y se le añade «Abandonar la batalla»
  con su confirmación. «Volver a la batalla» no hace falta: las Opciones se cierran con Esc, con «Cerrar» o con un clic fuera. Fuera F10 y el
  botón aparte.
- **15a:** clic en la escuadra para elegirla y «Aceptar» para confirmar, dentro del panel del punto de suministro. «Aceptar» sale solo con
  una elegida.
