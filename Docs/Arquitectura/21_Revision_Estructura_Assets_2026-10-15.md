# Revisión de la estructura de Assets (2026-10-15)

> **Registro histórico (2026-10-15).** Describe el estado y las decisiones de esa fecha; no se reescribe. Para el estado vigente, ver [Arquitectura actual](1_Arquitectura_Actual.md) y, para movimiento, [TroopMovementPipeline](../TroopMovementPipeline.md). Los nombres de sistemas o valores citados aquí pueden haber cambiado.

## Estado de las propuestas

- **R-Assets-01 — Inventariar carpetas, assemblies, escenas y referencias:** hecha. Solo lectura.
- **R-Assets-02 — Sacar las cachés de Graphify de `Assets`:** hecha; se retiraron 1.145 JSON y 1.147 `.meta` regenerables; la salida canónica de la raíz sigue intacta.
- **R-Assets-03 — Separar laboratorio de contenido de campaña:** hecha; las escenas del juego están en `GameScenes`, el laboratorio en `WorldMapTools` y `WorldMap-Lab` está fuera del build. La cadena de generación y los experimentos históricos están clasificados.
- **R-Assets-04 — Acordar una ubicación y política para recursos y datos:** hecha; las cargas dinámicas y referencias directas están clasificadas, ocho assets consolidados en `Resources/Data`, nombres uniformados y configuración directa de HUD fuera de `Resources`.
- **R-Assets-05 — Aclarar límites ECS, sincronización y presentación:** hecha como mapa y decisión de límites; `Game.ECS` se mantiene unida mientras haya dependencias bidireccionales entre sus dominios. El desglose y los motivos están documentados abajo.
- **R-Assets-06 — Ordenar herramientas propias y contenido externo:** hecha; scripts de Editor separados en assemblies editoriales, incluida la herramienta de preview de Synty; sus 11 tipos de runtime siguen bajo su carpeta de proveedor.

## Cambios aplicados

- Retiré las dos cachés de Graphify de `Assets`; el grafo canónico de la raíz permanece.
- Excluí `WorldMap-Lab` del build Addressables y conservé su escena, datos, dirección y herramientas de Editor.
- Moví ocho assets de `Scripts/Resources/Data` a `Resources/Data`, con los metadatos `.meta` originales. Las rutas `Resources.Load` siguen siendo las mismas.
- Actualicé el README de WorldMap para indicar que el laboratorio se conserva como herramienta y no se empaqueta.
- Moví los tres scripts propios de `Assets/Editor` a `Scripts/Editor`, dentro de `Game.Editor`; conservé sus `.meta` y renombré `AdaptRigAndAnimation.cs.cs` a `AdaptRigAndAnimation.cs`.
- Normalicé `Resources/items` a `Resources/Items` y `Maps/mapDatabase.asset` a `Maps/MapDatabase.asset` mediante `AssetDatabase.MoveAsset`; los 60 GUID del conjunto afectado se conservaron y ambas bases de datos siguen cargando.
- Moví `FCTCategoryConfig.asset` desde `Resources` a `ScriptableObjects/UI/Battle`; su GUID y referencias directas desde BattleTestScene y BattleScene se conservaron.
- Moví `CustomPreviewEditor.cs` desde el assembly mixto `Game.ECS` a `Game.Editor`; añadí esa referencia a las dos assemblies de Editor de Synty que lo consumen y se conservó el GUID del script.

## Diagnóstico

El inventario inicial registró 15.188 archivos de contenido en `Assets`; tras retirar las cachés quedan 14.043. El tamaño actual ronda 2,20 GB en el checkout observado. `Scenes` ocupa 380,7 MB, `Synty` 647,9 MB, `WorldMap` 565,4 MB y `Yoge` 265,5 MB; `Screenshots` ocupa 103,9 MB. Son tamaños de archivos en el disco del checkout, no una medición del build. La estructura ya agrupa razonablemente las escenas del flujo principal en `Scenes/GameScenes` y el código por dominios como `Hero`, `Squads`, `Combat`, `Inventory`, `Map` y `UI`.

### Prioridad alta: contenido de desarrollo mezclado con producción

`Scenes/WorldMapTools` contiene 2.555 de los 2.578 archivos bajo `Scenes`; es principalmente un gran conjunto de terrenos y experimentos. La división de producción y autoría ya existe: el juego entra por `Scenes/GameScenes`, sus sectores finales están en `WorldMap/Campaign`, y las pruebas están en `Scenes/WorldMapTools`. Parte del código editor usa `Experiments/MountainsV9` para generar la campaña. `WorldMap-Lab` mantiene la dirección `worldmap/lab` con `m_IncludeInBuild: 0`. La cadena de autoría `MountainsV9 → CampaignScale → CampaignRegion → StrategicTrials → WorldMapCampaign` se conserva como fuente reproducible. MountainsV7 y V8 quedan clasificados como versiones históricas sin uso por el juego actual; se mantienen bajo `Experiments` como referencia y no se presentan como contenido de producción.

### Prioridad alta: cachés generadas dentro de Assets

Había dos árboles de Graphify dentro de Unity: `Assets/graphify-out` (616 JSON) y `Assets/Scripts/graphify-out` (529 JSON), además de sus metadatos. Se retiraron tras confirmar que son cachés ignoradas por Git, que no hay consumidores en las escenas/código/Addressables y que la salida canónica está en `graphify-out/GRAPH_REPORT.md`, fuera de `Assets`.

### Prioridad media: datos y recursos dispersos

Los datos se reparten entre `Resources`, `ScriptableObjects`, `Config`, `Settings` y `WorldMap`. Consolidé ocho assets de avatar, clases y tienda desde `Scripts/Resources/Data` a `Resources/Data`; sus claves lógicas `Data/...` no cambiaron. `Resources` queda reservado a objetos que el código busca por clave en runtime y a sus datos dependientes. Los assets referenciados directamente desde escenas, prefabs o perfiles permanecen junto a su dominio; por esa política moví `FCTCategoryConfig` a `ScriptableObjects/UI/Battle`.

El inventario estático de `Resources.Load` está contrastado con las rutas de los assets. Las claves de avatar, clases, tienda, squads, diálogo y debug corresponden a assets existentes. Se normalizaron los nombres `Items` y `MapDatabase` para mantener una convención consistente. **Es una mejora de organización:** [Resources.Load ignora mayúsculas y minúsculas](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Resources.Load.html), y ambas cargas funcionaron en el Editor antes y después del cambio. La advertencia anterior sobre un posible fallo de esas cargas por capitalización era incorrecta.

| Clave de carga | Asset actual | Estado |
| --- | --- | --- |
| `Data/Avatar/AvatarPartDatabase` | `Resources/Data/Avatar/AvatarPartDatabase.asset` | Coincide |
| `Data/Avatar/DefaultAvatarBasePartsDefinition` | `Resources/Data/Avatar/DefaultAvatarBasePartsDefinition.asset` | Coincide |
| `Data/HeroClasses/{classId}` | `Resources/Data/HeroClasses/{Bow,Spear,SwordAndShield,TwoHandedSword}.asset` | Coincide |
| `Data/Store/StoreDatabase` | `Resources/Data/Store/StoreDatabase.asset` | Coincide |
| `Squads/SquadDatabase` | `Resources/Squads/SquadDatabase.asset` | Coincide |
| `Items/ItemSODatabase` | `Resources/Items/ItemSODatabase.asset` | Normalizado y cargado en Unity |
| `Maps/MapDatabase` | `Resources/Maps/MapDatabase.asset` | Normalizado y cargado en Unity |
| `DialogueEffectDatabase` | `Resources/DialogueEffectDatabase.asset` | Coincide |
| `Debug/TestHeroPool` | `Resources/Debug/TestHeroPool.json` | Coincide |
| Referencia de escena, sin `Resources.Load` | `ScriptableObjects/UI/Battle/FCTCategoryConfig.asset` | Fuera de `Resources`; dos escenas conservan la referencia GUID |

La normalización conservó los `.meta` y los GUID. Los futuros movimientos que cambien una clave lógica requieren actualizar sus consumidores y verificar la carga. `Resources` es una política de carga en runtime, no un destino general para cualquier ScriptableObject; los datos enlazados directamente necesitan una clasificación aparte.

Los iconos `.png` de objetos comparten nombre y carpeta con sus `.asset`; los ScriptableObjects los enlazan mediante GUID. El inventario no encontró GUID duplicados ni `.meta` huérfanos, así que esos pares no son por sí mismos evidencia de referencias rotas.

### Prioridad media: nombre y alcance de la assembly

`Scripts/Game.ECS.asmdef` contiene 513 archivos C#: ECS, servicios, UI, MonoBehaviours y sincronización. El recuento por archivo encontró 283 que usan `Unity.Entities`, 278 que usan `UnityEngine` y 103 que usan ambas; además, 143 declaran componentes, 77 contienen sistemas, 131 derivan de `MonoBehaviour` y 20 mencionan `EntityVisualSync` o `VisualSyncUtility`. Las categorías se solapan.

| Grupo | Dependencia observada | Límite recomendado |
| --- | --- | --- |
| Ensamblados del mapa | `Game.ECS` consume `Conquest.WorldMap`; `Conquest.WorldMap.Editor` consume `Conquest.WorldMap` y VegetationSpawner. El grafo registra nueve relaciones editor→runtime de WorldMap y no la dirección inversa. | Mantener el runtime del mapa separado; frontera clara y sin ciclo de assemblies. |
| ECS, sincronización y visuales | `EntityVisualSync` y `VisualSyncUtility` son los puentes explícitos. `UnitAnimationAdapter` lee datos ECS y alimenta Animator; la sincronización visual también se consume desde animación y presentación. | Mantener el puente visual en un grupo compartido hasta que sus contratos puedan aislarse sin referencias de vuelta. |
| Animación y presentación | El grafo encuentra relaciones `ConquestTactics.Visual → ConquestTactics.Animation` (9) y la dirección inversa (3), incluida `UnitAnimationAdapter → EntityVisualSync`. | No dividir estos namespaces en assemblies todavía; primero extraer contratos sin Unity o ECS si se necesita el aislamiento. |
| Datos de objetos | `Data.Items` recibe referencias desde servicios y UI (158 relaciones del grafo) y devuelve referencias a tipos globales y persistencia (28). `ItemDataSO` también usa enums sin namespace. | Mantener los datos con el dominio y evitar extraerlo como assembly hasta eliminar las dependencias globales y de persistencia. |

No hay ciclos entre assemblies: `Game.ECS` referencia `Conquest.WorldMap`; `Game.Editor`, `SettlementPreview.Editor` y las pruebas dependen de `Game.ECS`; `Conquest.WorldMap.Editor` depende de `Conquest.WorldMap` y VegetationSpawner. No hay `.asmref`. El grafo extrae 572 relaciones entre namespaces dentro de `Assets/Scripts`, aunque el namespace global y las referencias cruzadas hacen que no convenga convertir los nombres de carpetas directamente en assemblies. La separación ECS/sync/presentación queda fijada como límite lógico; un corte de assembly se hará sólo con contratos unidireccionales.

### Prioridad baja: herramientas, terceros y convenciones

Las herramientas propias están en `Scripts/Editor` y `Scripts/SettlementPreview/Editor`; se retiró `Assets/Editor`. El preview genérico de Synty ahora compila en `Game.Editor`; Icon Creator y Modular Character Editor declaran esa referencia y siguen limitadas a Editor. Los 11 scripts Synty restantes en `Game.ECS` son modelos, componentes, extensiones y demo de runtime que usan los visuales; se mantienen bajo `SyntyEditorTools-master` para conservar procedencia. Permanecen convenciones dispares como `avatarCreator`, `statsGenerators`, `esqueletos` y `CharacterselecctionScene`; renombrar escenas requiere revisar rutas literales, Addressables y build settings.

## Estructura objetivo gradual

Mantendría las carpetas técnicas que Unity espera, como `Resources`, `Editor` donde corresponda, `Settings` y `AddressableAssetsData`. Para el contenido propio, la meta puede ser una jerarquía consistente por dominio (`Scripts`, `Scenes`, `Prefabs`, `Data` y `Art`), con `Development` para laboratorios, fixtures y capturas; el contenido externo debe quedar claramente identificado como tercero. `Scripts` debería expresar responsabilidades ECS, sincronización y presentación con convenciones de carpetas y assemblies decididas después del mapa de dependencias.

## Siguientes pasos

1. **Hecho:** verificar y retirar las cachés de Graphify de `Assets`.
2. **Hecho:** laboratorio separado por carpetas y excluido del build; fuentes activas e históricas clasificadas.
3. **Hecho:** política de `Resources` fijada y aplicada a referencias directas frente a cargas dinámicas.
4. **Hecho:** mapa de dependencias de assemblies, dominios y puentes visuales documentado; `Game.ECS` permanece unida por sus referencias cruzadas.
5. **Hecho:** código de Editor propio y de Synty separado de `Game.ECS`; los modelos de runtime de terceros conservan su carpeta de origen.

Las operaciones de AssetDatabase preservaron los GUID de los 60 assets de Items/MapDatabase y del asset FCT. Se comprobó que las dos escenas de batalla siguen refiriendo FCT por GUID, que las cargas de Items y MapDatabase resuelven sus rutas nuevas y que Unity terminó la recompilación de assemblies editoriales. No se ejecutaron suites ni una build nueva. Las cifras describen el checkout local y pueden incluir importaciones de terceros.
