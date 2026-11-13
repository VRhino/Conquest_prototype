# 25. Crear el héroe (2026-11-13)

Paso 3 del bloque 4 de [`Hoja_De_Ruta.md`](../Hoja_De_Ruta.md) (inventario §3.3). Una cuenta sin héroe ya no se queda en la pantalla de la
sesión viva: al entrar en la partida, crea su héroe sobre el mapa del paso 2 y, al aceptarse `crearHeroe`, entra en el mapa dentro de su
campamento. Se hace en dos tandas:

- **3A** (esta): campamento sobre el mapa, clase y nombre, con un aspecto por defecto (como hace hoy BronzeAgeClient).
- **3B**: el creador de avatar de Conquest (`AvatarCreator`).

Se parte porque el creador es una escena UGUI con un personaje 3D modular de Synty (`ModularHeroVisual`), cámara orbital y su propia lógica
de clase (`HeroClassSelector`, con las 4 clases de relleno), y al terminar guarda en `player_save.json` (`HeroDataService`,
`PlayerSessionService`, `SaveSystem`). Sus piezas de aspecto sí sirven tal cual: `AvatarPartSelector.GetCurrentSelection()` da ids de texto
estables de `Resources/Data/Avatar/AvatarPartDatabase.asset` (`head_00`, `hair_01`, `eyebrow_01`, `facialhair_01`…) que encajan con
`cabeza_id`, `pelo_id`, `cejas_id` y `barba_id` del contrato.

## Dónde está

| Pieza | Dónde | Qué |
|---|---|---|
| `ClaseDeHeroe` | `Conquest.Mundo` | Las clases de `Objetos_Del_Heroe.CLASES_HEROE` con su equipo inicial de `OBJETOS`: el arma de tier 1 de su clase, las cuatro piezas de su armadura inicial (sin casco) y la montura de tiro (BronzeAgeClient: `ui/clasesHeroe.ts`) |
| `AnalisisDeCampamento` | `Conquest.Mundo` | Lo que rodea a cada campamento, sacado del artefacto (el motor no lo manda): suelo, fertilidad y bosque a 150 u; yacimientos a 150 y 250 u y el más cercano; río, agua, bandidos a 250 u y el campamento vecino (`mundo/analisisCampamento.ts`) |
| `PantallasDeMundo.CrearHeroe()` | `Conquest.Mundo.UI` | La pantalla: la lista de campamentos, la ficha del elegido, la clase, el nombre y «Nacer» |
| `MapaDeMundo` (modo sin héroe) | `Conquest.Mundo.Mapa` | `Enfoque`, `RadioVisionFijo`, `Adicionales` y `RayasAdicionales`: el mismo mapa, mirando al campamento, con 150 u en pantalla, los campamentos y bandidos del artefacto y los aros de 150 y 250 u del elegido |
| `CrearHeroe.uxml` | `Assets/Resources/MundoUI` | La pantalla |

## Decisiones

- **El mismo mapa del paso 2**, no uno aparte: el clic sobre un campamento lo elige (y la lista bajo el clic, si hay varias cosas); un clic en
  el suelo no quita la elección. Sin niebla: aún no hay héroe ni VISTA. A esta escala las cajas de los campamentos y de los bandidos van
  más grandes, como marcas, y sin los nombres de los nodos (serían cientos).
- **Ids:** la `Bienvenida` llama a los campamentos `camp-N` y el artefacto `campamento-N`; se cruzan por N, como en BronzeAgeClient.
- **Nombre del campamento:** «Campamento N». BronzeAgeClient les pone el de su región del generador; queda para cuando haga falta.
- **Solo lo que se puede hacer:** «Nacer en …» sale cuando hay campamento y clase; mientras, lo que falta. El nombre puede ir vacío (el
  motor lo deja sin nombre: «Héroe N»). Los rechazos (`heroe.nombre_en_uso`, `heroe.nombre_invalido`…) se dicen junto al campo con su frase.
- **El aspecto**, por defecto hasta la 3B: género masculino y avatar vacío, como BronzeAgeClient.
- **Lo que no entra:** la valoración «Recomendado», el «Elegir por mí» y la comparación de dos campamentos de BronzeAgeClient. Se empieza
  mirando al campamento con más gente, sin elegirlo.

## Pedido al motor

El motor recibe el género y el avatar de `crearHeroe` y los ignora (`sesiones/ordenes.rs`: «se ignoran hasta que Conquest publique su
catálogo»). Hasta que los guarde, el aspecto que se elija no llega a los demás jugadores ni a la batalla. El catálogo de piezas existe:
`AvatarPartDatabase.asset`. Apuntado en «Pedidos al motor» del bloque 4.

## Pruebas

- `NacerDelHeroeTests` (EditMode): las clases con su equipo inicial.
- `PantallasDeMundoTests` (PlayMode, con la interfaz pulsada como el jugador): una cuenta nueva entra en crear el héroe; «Nacer» no sale
  sin campamento ni clase; las 5 clases con su equipo; el campamento 4 elegido en la lista con lo que lo rodea; un nombre que ya tiene otro
  héroe se rechaza junto al campo; y al nacer, el mapa con el héroe dentro de su campamento (y de ahí sigue la prueba del paso 2).
