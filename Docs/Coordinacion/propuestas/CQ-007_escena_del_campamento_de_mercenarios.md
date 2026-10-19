# CQ-007 — Escena del campamento de mercenarios

**Destino:** `Conquest_prototype`
**Estado:** ACEPTADO
**Prioridad:** Media — sin ella el héroe que nace o entra en un campamento no tiene escena que ver en Unity
**Solicitante:** BronzeAge / Claude
**Fecha:** 2026-10-05
**Origen:** cierre de los cabos del campamento de mercenarios (`Docs/Mecanicas a desarrollar.md` §40, ya retirada) con el autor
del proyecto. Fuentes en `BronzeAgeFase0` (rama `claude/intelligent-liskov-6026e5`, sin fusionar a `main` todavía): canon en
`Docs/Game/1_Sistema_de_Mundo_y_Territorio.md` §1.9b; contrato en `src/contratos/v1/` (commit `9a792de`); planta en
`src/engine/layoutCampamento.ts` (commit `cb35101`); modelo de datos en `Docs/Coordinacion/01_Modelo_de_datos_compartido.md` §10
y proyección en `02_Contrato_Comunicacion_Servidor_Cliente.md` §4.2b.

## Problema

BronzeAge tiene una entidad nueva del mundo, el **campamento de mercenarios**: un enclave neutral, sin Facción ni zona, donde
nace cada héroe (elige uno al crearse), puede residir, comprar, reclutar y refundar. Un héroe **está dentro** de él, igual que
dentro de una plaza (`UbicacionHeroe.tipo === 'mercenarios'`, canon 1.9b, D73-D74), y desde dentro actúa sin columna en el mapa.
Hasta hoy no existía su interior: la lista de edificios no tenía posiciones. Conquest no tiene nada que dibujar cuando el
jugador está dentro de uno, ni forma de saber qué edificios hay ni dónde.

Cuando Conquest hable de «vista de asentamiento» de un campamento, el autor pide lo mismo que para una plaza (nota de integración 1):
traducir la planta 2D a un campamento 3D con modelos por edificio.

## Comportamiento solicitado

### 1. Montar la escena de un campamento a partir de `EscenaCampamento`

BronzeAge publica la planta ya resuelta; Conquest no calcula trazado. Llega en la proyección del jugador como `escenaCampamento`,
**solo cuando el héroe está dentro** de un campamento (como el interior de la plaza que se pisa); fuera, el campo no viaja.

```text
EscenaCampamento
  campamentoId, origen, layoutVersion, unidadesPorCelda
  edificios[]   { edificioId, tipo, posicion (centro, unidades), ancho, alto (celdas) }
  calles[]      { col, row, ancho, alto }                 celdas, suelo transitable
  empalizada[]  { col, row, clase: 'muro' | 'puerta' }    celdas
```

Mismas convenciones que `SettlementBattleSnapshot` (doc 01 §17): coordenadas locales con el origen en el centro de la taberna,
`y` hacia abajo, la celda (col, row) ocupa de (col, row) a (col + 1, row + 1), una celda = `unidadesPorCelda` = 3 unidades, y la
conversión a Unity es la de BA-005: `(X, Z) = (x·S, −y·S)`. BronzeAge no conoce `S`.

La planta sale de reutilizar el trazado de asentamientos **una sola vez**, con la **taberna como ancla principal** (hace el papel
del Centro Urbano: siembra la red de calles y a ella se pegan las viviendas). El campamento no crece, no tiene mantenimiento, no se
degrada ni sube de nivel, así que la planta no cambia nunca.

### 2. Edificios que pueden aparecer (`tipo`)

| `tipo` | Huella (celdas) | Notas |
|---|---|---|
| `taberna` | 4×4 | Siempre una, en el origen. Hoy sin función de juego (rumores: entrada 18 de BronzeAge) |
| `vivienda` | 1×1 | Diez |
| `mercado` | 3×2 (puede venir girado, 2×3) | Uno |
| `barracon` / `galeriaDeTiro` / `caballerizas` | 2×2 / 2×4 / 3×2 | Exactamente uno de los tres, el que dicta la posición del campamento |
| `puestoMercado` | 1×2, 1×3 o 1×1 | Cinco, alrededor del mercado (de relleno del trazado) |
| `plazaDeArmas` | 2×2 | Ancla del edificio militar: aparece con él |
| `plaza`, `pozo`, `parque` | 2×2, 1×1, 3×2 | Anclas residenciales; pueden aparecer o no según el trazado |

Los cinco últimos son de relleno del trazado de asentamientos, no edificios que tenga el campamento: su aspecto es de Conquest
y no afectan a ninguna regla. Modelos por edificio, como en una plaza (varias variantes por tipo).

### 3. Empalizada decorativa

Rodea todo con un anillo a una celda de margen. Es **decorativa y sin efecto de juego**: dentro de un campamento no hay combate
(protección de 60 unidades de mapa, D78). La `puerta` son dos celdas en el lado sur, por donde se entra y se sale.

### 4. `origen` es el aspecto, no la planta

`origen` (0 a 2) es la variante de aspecto para los modelos. **La planta no depende de él**: sale del `id` del campamento, así
que dos campamentos con el mismo `origen` tienen plantas distintas y un campamento siempre tiene la misma.

## Forma de datos

Contrato v1, con `schemaVersion` 1 (sin subir: son definiciones nuevas, nada existente cambia):

- `definitions/CampamentoMercenarios` y `definitions/EscenaCampamento` en `src/contratos/v1/contratos.schema.json`.
- Fixtures `src/contratos/v1/fixtures/campamentoMercenarios.json` y `escenaCampamento.json`. El segundo sale del motor real: si
  cambia el trazado, el JSON cambia y el test de BronzeAge falla hasta regenerarlo con `ACTUALIZAR_CONTRATOS=1`; así `layoutVersion`
  (hoy 2) avisa de cuándo reconstruir.

`CampamentoMercenarios` es la entidad completa tal como viaja en `campamentosMercenarios`, que trae los campamentos que la
Facción conoce (como un camino: los explorados, no solo los que ve ahora; el suyo siempre). Conquest puede ignorar sus campos de
mercado, fondos y población; los usa el cliente estratégico.

## Invariantes de autoridad

- BronzeAge decide qué hay en el campamento y dónde. Unity no calcula ni modifica la planta.
- Unity no envía nada sobre el campamento: acciones (comprar, reclutar, salir, entrar) siguen por comandos (doc 02 §4.2b).
- El estado persistente (residentes, mercado, población, fondo de refundación) es de BronzeAge, nunca de la escena.

## Compatibilidad

- Definiciones nuevas y campo nuevo opcional en la proyección: no rompe ningún consumidor existente.
- Una partida guardada con un `LAYOUT_VERSION` distinto se rechaza al cargar (ya es así para asentamientos).
- Los campamentos de partidas antiguas tienen dos viviendas; son del mundo anterior (D17: mundo nuevo, sin migración).

## Criterios de aceptación

- Con el fixture `escenaCampamento.json` se levanta una escena con la taberna en el origen, calles transitables, la empalizada
  con su puerta y cada edificio con su huella, sin solapes entre edificios ni sobre calles.
- Cada `tipo` de la tabla tiene al menos un modelo provisional (cubos de colores sirven, como en `SettlementPreview`).
- Un héroe dentro de un campamento recibe `escenaCampamento` en la proyección; fuera, no.
- Dos campamentos con el mismo `origen` y distinto `id` dan plantas distintas.

## Dudas para Codex

- ¿Basta con el centro y la huella en celdas, o necesitáis `visualSeed` por edificio como en el asentamiento (doc 01 §17,
  todavía no está en el dominio)? Hoy el campamento no lo lleva.
- ¿Queréis la orientación de la puerta fija (sur) o conviene que la planta la oriente hacia el camino de entrada?
- La vista caminable de otros héroes dentro del mismo campamento (presencia visible) no está definida por BronzeAge todavía: sería
  la siguiente propuesta si Conquest la necesita.

## Revisión de Conquest — 2026-10-15

Se acepta el interior 3D; implementación condicionada a publicar la planta actual. Usar el ámbito
`CAMPAMENTO` y `Heroe.ubicacion.campamento_id`; la escena sigue el estado confirmado y `Revocado` la libera.

**Hueco de contrato:** el actual `CampamentoMercenarios.edificios` es una lista de tipos. No existen
`EscenaCampamento`, posiciones/huellas, calles, empalizada ni versión de trazado del campamento en los
`.proto` leídos. No basta con migrar el nombre del JSON. El contrato 06 encarga el trazado al servidor;
Conquest no debe reconstruirlo desde el ID ni tratarlo como terreno inmutable del paquete de mundo.

Reutilizar la construcción por huellas de `SettlementPreview` con un adaptador del modelo de sesión;
su lector actual de JSON no es el contrato nuevo. Para el primer blockout no hace falta `visualSeed`;
las variantes decorativas pueden derivarse de IDs/versiones artísticas. La orientación de la puerta
se representa según la planta publicada. `residentes_ids` no publica posiciones caminables compartidas.

Se mantiene la necesidad completa de aceptación: planta recibida, calles, puerta y huellas coherentes,
sin solapes y con unidades locales documentadas. Pendiente G5 de la [revisión conjunta](README.md).
