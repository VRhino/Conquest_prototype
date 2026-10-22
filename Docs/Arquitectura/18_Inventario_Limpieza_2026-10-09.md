# Inventario de limpieza · 9 de octubre de 2026

> **Registro histórico (2026-10-09).** Describe el estado y las decisiones de esa fecha; no se reescribe. Para el estado vigente, ver [Arquitectura actual](1_Arquitectura_Actual.md) y, para movimiento, [TroopMovementPipeline](../TroopMovementPipeline.md). Los nombres de sistemas o valores citados aquí pueden haber cambiado.

**Pendiente de tu selección. No se ha eliminado ni modificado código, escenas, assets ni configuración en esta auditoría.** Los cambios de inventario previos se conservan.

Hay **32 grupos**, con **3.744 archivos candidatos** y aproximadamente **1.556,82 MiB** en el checkout. El total incluye propuestas opcionales y condicionadas: **no es una propuesta de borrar todo**. Los tamaños excluyen `.meta`; eliminar assets del checkout no libera automáticamente sus objetos históricos de Git/LFS. Las builds y caches locales se distinguen de los archivos del proyecto.

El listado exacto de cada archivo, su `.meta`, tamaño, pertenencia a dependencias activas y referencias externas está en [el CSV de candidatos](<C:/Users/VRINO/Desarrollo/Conquest_prototype/Docs/Arquitectura/18_Limpieza_Candidatos_2026-10-09.csv>). Cada ID se puede aprobar o rechazar de forma independiente, respetando las condiciones indicadas. Las carpetas solo se retirarían cuando quedasen vacías.

## Qué se ha comprobado

- Grafo vigente: [GRAPH_REPORT.md](<C:/Users/VRINO/Desarrollo/Conquest_prototype/graphify-out/GRAPH_REPORT.md>), actualizado el 9 de octubre: 7.277 nodos y 13.830 relaciones.
- Inventario estático: 30.736 rutas de assets/metadatos/configuración y 662 archivos C# examinados, incluyendo herramientas y código importado. Búsqueda de consumidores de tipos, rutas de carga y referencias GUID. Los assets binarios se contrastaron mediante Unity.
- Unity Editor: dependencias recursivas de **234 raíces**: las 29 escenas propias, 136 entradas Addressables y los assets en Resources, deduplicadas. Configuración: nueve escenas habilitadas en Build Settings.
- Se separaron **2.017 dependencias del flujo del juego/Resources/Addressables de campaña** de la entrada de laboratorio `worldmap/lab`. Ningún archivo del CSV pertenece a esas 2.017 dependencias. Esto **no** equivale a ausencia de uso manual en el Editor o de entradas por nombre desde herramientas.
- Se revisaron referencias externas a cada grupo, especialmente prefabs, materiales compartidos, generadores y validadores. `MenuItem`, `CustomEditor`, ScriptableObjects polimórficos y sistemas ECS automáticos se trataron como entradas válidas aunque no tengan un llamador C#.

Es un análisis de referencias y responsabilidad, no una prueba de eliminación: no se ejecutaron tests ni builds nuevos porque no cambió código. Las búsquedas no prueban que un prefab sin instancias carezca de utilidad futura como material de autoría.

## Mapa que se debe conservar

La escena del juego actual es [WorldMapCampaign.unity](<C:/Users/VRINO/Desarrollo/Conquest_prototype/Assets/Scenes/GameScenes/WorldMapCampaign.unity>). Usa el snapshot V9 con las adaptaciones posteriores de campaña: costas, masas forestales, navegación y streaming. Las direcciones de los sectores son `worldmap/v9/seed42/sector/...`.

Las versiones completas anteriores que siguen presentes son **MountainsV7 y MountainsV8**. No hay carpetas de escenas V1–V6 en este árbol; aparecen revisiones históricas en documentos y capturas. `Coasts V1` es una revisión de costas distinta de las versiones de montañas, por lo que su nombre no basta para clasificarla como mapa obsoleto.

Hay recursos actuales dentro de carpetas llamadas «Experiments»:

| Ubicación | Dependencias actuales que hay que conservar |
|---|---|
| `Assets/Scenes/WorldMapTools/Experiments/MountainsV9` | **438 assets** usados por escena, sectores o Addressables actuales. Mantener la carpeta V9 para su autoría y generación. |
| `Assets/Scenes/WorldMapTools/Experiments/CampaignScale` | **7 materiales**: Adobe, Bronze, Door shadow, Faction red, Flat clay roofs, Limestone walls y Timber. Están en los símbolos de ciudades/ejército de la campaña. |
| Raíz de `Assets/Scenes/WorldMapTools/Experiments` | **21 assets**: siete prefabs de árboles y sus 14 mallas LOD Middle/Far. Se comparten con campaña y V9. |
| Misma raíz | **ForestDensityProfile.asset y RegionalGroundProfile.asset**: cargados por nombre para regenerar V9, aunque no sean dependencias del runtime. |
| `Assets/BronzeAgeRegionalImports/Seed_42 6` | Última importación; seis materiales compartidos siguen activos. Mantener esta fuente por ahora. |

Referencias: [WorldMapStructure.md](<C:/Users/VRINO/Desarrollo/Conquest_prototype/Docs/WorldMapStructure.md>), [WorldMapCampaignPlan.md](<C:/Users/VRINO/Desarrollo/Conquest_prototype/Docs/WorldMapCampaignPlan.md>), [WorldMapMountainTrial.cs](<C:/Users/VRINO/Desarrollo/Conquest_prototype/Assets/Scripts/Editor/WorldMap/WorldMapMountainTrial.cs>), [WorldMapCampaignCoastBuilder.cs](<C:/Users/VRINO/Desarrollo/Conquest_prototype/Assets/Scripts/Editor/WorldMap/WorldMapCampaignCoastBuilder.cs>).

## Versiones anteriores y datos del mapa

| Grupo | Candidato | Archivos | MiB | Clasificación | Condición / evidencia |
|---|---|---:|---:|---|---|
| **M01** | World map MountainsV7 | 628 | 87.98 | Recomendado | Sin dependencias del juego actual; el cache incluido referencia el perfil de V7. Mantener recursos compartidos de la raÃ­z Experiments. |
| **M02** | World map MountainsV8 | 642 | 96.96 | Recomendado | Sin dependencias del juego actual; el cache incluido referencia el perfil de V8. Mantener recursos compartidos de la raÃ­z Experiments. |
| **M03** | Importaciones repetidas Seed_42 1, 2 y 3 | 330 | 27.39 | Recomendado | No se detectaron consumidores externos a estas tres carpetas. |
| **M04** | Importaciones Seed_42 4 y 5 | 220 | 18.23 | Condicionado | Eliminar junto con M01 y M02: sus materiales pertenecen a las escenas V7 y V8. |
| **M05** | Bosques horneados antiguos y Foliage.mat | 44 | 212.15 | Recomendado | 43 Forest_*.asset y un material: sin referencias serializadas ni dependencias de las raÃ­ces examinadas; no se detectÃ³ carga por nombre en cÃ³digo propio. |
| **M06** | Cache Synty global duplicado de V9 | 1 | 20.42 | Opcional | Sin consumidor actual; WorldMapSyntyBaker puede regenerarlo durante el horneado. No incluye la carpeta MountainsV9 ni Surroundings.asset. |
| **M07** | LandscapeLab y sus datos exclusivos | 498 | 66.67 | Condicionado | Retirar la entrada Addressables worldmap/lab y adaptar sus menÃºs/validadores. Mantener los dos perfiles de generaciÃ³n y 21 assets compartidos del bosque. |
| **M08** | Preview inicial e importaciÃ³n Seed_42 original | 111 | 9.14 | Condicionado | Preview de autorÃ­a, no flujo del jugador. Dependencia del laboratorio y de sus generadores; decidir junto con M07. No incluye arte comÃºn de CampaignArt. |
| **M09** | Prueba CampaignScale: escena, cuatro terrenos y reportes | 7 | 9.18 | Condicionado | Siete archivos. Fuente de BuildCampaignRegion y de los sÃ­mbolos en BuildWorldMapCampaign. Preservar siete materiales de la campaÃ±a; adaptar constructores antes de retirar. |
| **M10** | Prueba CampaignRegion | 26 | 15.02 | Condicionado | No entra en el juego; es fuente de BuildStrategicTrials y sus validaciones. |
| **M11** | StrategicTrials | 75 | 54.57 | Condicionado | BuildWorldMapCampaign genera la campaÃ±a desde esta escena; ValidateWorldMapCampaign consulta sus terrenos. Revisar ademÃ¡s las referencias de baseline usadas por BuildCampaignForestMasses. |
| **M12** | Materiales Influence y Journey de calibraciÃ³n | 2 | 0.01 | Condicionado | Sin uso en campaÃ±a; sÃ­ en CampaignRegion y StrategicTrials. Eliminar despuÃ©s de M10/M11. |

La cadena de autoría actual es: **V9 → CampaignScale → CampaignRegion → StrategicTrials → WorldMapCampaign**. El juego puede conservar sus assets finales al retirar esos laboratorios, pero se perdería la reconstrucción original si no se adaptan sus herramientas. [BuildWorldMapCampaign.cs](<C:/Users/VRINO/Desarrollo/Conquest_prototype/AgentScripts/BuildWorldMapCampaign.cs>) abre StrategicTrials y toma símbolos de CampaignScale; [ValidateWorldMapCampaign.cs](<C:/Users/VRINO/Desarrollo/Conquest_prototype/AgentScripts/ValidateWorldMapCampaign.cs>) también consulta terrenos de StrategicTrials. No son carpetas independientes desde el punto de vista de autoría.

Además, [BuildCampaignForestMasses.cs](<C:/Users/VRINO/Desarrollo/Conquest_prototype/AgentScripts/BuildCampaignForestMasses.cs>) contempla una baseline por GUID en [ForestMassBaseline.json](<C:/Users/VRINO/Desarrollo/Conquest_prototype/Docs/ForestMassBaseline.json>). En el checkout actual ese documento contiene `{}`, sin registros de sectores: no sirve como prueba de que se puedan retirar fuentes de bosque. Hay que normalizar esa baseline y comprobar el horneado antes de eliminar sus posibles fuentes en M11.

M07 elimina el laboratorio y sus datos exclusivos, pero no autoriza eliminar las clases completas de `WorldMapVegetationTrial`, `WorldMapLandscapeLab`, `WorldMapRegionalGroundTrial`, `WorldMapAdaptiveForest` o `WorldMapForestLod`: V9 utiliza perfiles, constantes y funciones de estas clases. Sus menús de pruebas pueden simplificarse después de separar lo compartido.

## Código sin uso detectado y controles desconectados

| Grupo | Candidato | Archivos | MiB | Clasificación | Condición / evidencia |
|---|---|---:|---:|---|---|
| **C01** | Archivos vacÃ­os o totalmente comentados | 3 | 0.00 | Recomendado | No contienen cÃ³digo compilable; no tienen referencias serializadas. |
| **C02** | Datos y utilidad sin consumidores | 5 | 0.00 | Recomendado | Sin consumidores C# externos ni referencias serializadas detectadas. No son sistemas ECS que se ejecuten automÃ¡ticamente. |
| **C03** | Authoring antiguo sin uso | 5 | 0.00 | Recomendado | Sin uso en escenas/prefabs ni consumidores. HeroEntity.Authoring realiza el baking de vida, spawn, stats e input; DataContainerSystem crea su singleton. |
| **C04** | Controles y helpers desconectados | 5 | 0.02 | Opcional | Sin referencias actuales. LoadoutSelectionUI usa el flujo antiguo LocalSaveSystem/DataContainer; el detalle de hÃ©roe usa HeroDetail3DPreview. Mantener los servicios/sistemas compartidos. |
| **C05** | UI de chat y captura no conectada | 2 | 0.01 | DecisiÃ³n funcional | Sin referencias a sus componentes, pero expresan funcionalidades previstas; CapturePointUIController aparece en TDD y Mechanics/CapturePoints. No eliminar los sistemas de chat/captura. |

Los archivos exactos son:

### C01 · Archivos vacÃ­os o totalmente comentados

- [DataContainerBaker.cs](<C:/Users/VRINO/Desarrollo/Conquest_prototype/Assets/Scripts/Shared/DataContainerBaker.cs>)
- [SquadFormationData.Authoring.cs](<C:/Users/VRINO/Desarrollo/Conquest_prototype/Assets/Scripts/Squads/SquadFormationData.Authoring.cs>)
- [SquadRarity.cs](<C:/Users/VRINO/Desarrollo/Conquest_prototype/Assets/Scripts/Squads/SquadRarity.cs>)

### C02 · Datos y utilidad sin consumidores

- [SquadCombat.Component.cs](<C:/Users/VRINO/Desarrollo/Conquest_prototype/Assets/Scripts/Combat/SquadCombat.Component.cs>)
- [HeroEquipmentMappingUtils.cs](<C:/Users/VRINO/Desarrollo/Conquest_prototype/Assets/Scripts/Hero/Utils/HeroEquipmentMappingUtils.cs>)
- [InventoryStats.cs](<C:/Users/VRINO/Desarrollo/Conquest_prototype/Assets/Scripts/Inventory/InventoryStats.cs>)
- [SquadSkill.Component.cs](<C:/Users/VRINO/Desarrollo/Conquest_prototype/Assets/Scripts/Shared/SquadSkill.Component.cs>)
- [UnitFollowDelay.Component.cs](<C:/Users/VRINO/Desarrollo/Conquest_prototype/Assets/Scripts/Squads/UnitFollowDelay.Component.cs>)

### C03 · Authoring antiguo sin uso

- [HeroInput.Authoring.cs](<C:/Users/VRINO/Desarrollo/Conquest_prototype/Assets/Scripts/Hero/HeroInput.Authoring.cs>)
- [HeroLife.Authoring.cs](<C:/Users/VRINO/Desarrollo/Conquest_prototype/Assets/Scripts/Hero/HeroLife.Authoring.cs>)
- [HeroSpawn.Authoring.cs](<C:/Users/VRINO/Desarrollo/Conquest_prototype/Assets/Scripts/Hero/HeroSpawn.Authoring.cs>)
- [HeroStats.Authoring.cs](<C:/Users/VRINO/Desarrollo/Conquest_prototype/Assets/Scripts/Hero/HeroStats.Authoring.cs>)
- [DataContainer.Authoring.cs](<C:/Users/VRINO/Desarrollo/Conquest_prototype/Assets/Scripts/Shared/DataContainer.Authoring.cs>)

### C04 · Controles y helpers desconectados

- [PoolBootstrapper.cs](<C:/Users/VRINO/Desarrollo/Conquest_prototype/Assets/Scripts/Shared/Pooling/PoolBootstrapper.cs>)
- [ScenePhaseUpdater.cs](<C:/Users/VRINO/Desarrollo/Conquest_prototype/Assets/Scripts/Shared/ScenePhaseUpdater.cs>)
- [FormationVisualizer.cs](<C:/Users/VRINO/Desarrollo/Conquest_prototype/Assets/Scripts/Squads/FormationVisualizer.cs>)
- [HeroDetailPreviewCamera.cs](<C:/Users/VRINO/Desarrollo/Conquest_prototype/Assets/Scripts/UI/HeroDetail/HeroDetailPreviewCamera.cs>)
- [LoadoutSelectionUI.cs](<C:/Users/VRINO/Desarrollo/Conquest_prototype/Assets/Scripts/UI/LoadoutSelectionUI.cs>)

### C05 · UI de chat y captura no conectada

- [ChatUIController.cs](<C:/Users/VRINO/Desarrollo/Conquest_prototype/Assets/Scripts/UI/Chat/ChatUIController.cs>)
- [CapturePointUIController.cs](<C:/Users/VRINO/Desarrollo/Conquest_prototype/Assets/Scripts/UI/Zone/CapturePointUIController.cs>)

C01 contiene solo comentarios o archivos vacíos. C02/C03 no tienen consumidores C# externos, referencias GUID ni dependencias de ninguna de las escenas y raíces cargables examinadas. En particular, [HeroEntity.Authoring.cs](<C:/Users/VRINO/Desarrollo/Conquest_prototype/Assets/Scripts/Hero/HeroEntity.Authoring.cs>) ya hornea vida, spawn, stats e input; [DataContainer.System.cs](<C:/Users/VRINO/Desarrollo/Conquest_prototype/Assets/Scripts/Shared/DataContainer.System.cs>) crea el singleton sin DataContainerAuthoring.

C04 elimina componentes desconectados, no sus servicios. `LocalSaveSystem`, `DataContainerComponent/System`, `ObjectPoolSystem` y `SceneTransitionService` **siguen teniendo consumidores**. El preview conectado al detalle de héroe es [HeroDetail3DPreview.cs](<C:/Users/VRINO/Desarrollo/Conquest_prototype/Assets/Scripts/UI/HeroDetail/HeroDetail3DPreview.cs>); su inspector personalizado en [HeroDetail3DPreviewSetup.cs](<C:/Users/VRINO/Desarrollo/Conquest_prototype/Assets/Scripts/UI/HeroDetail/HeroDetail3DPreviewSetup.cs>) también sigue siendo una entrada útil del Editor.

C05 requiere una decisión de producto. La interfaz de captura aparece descrita en [CapturePoints.md](<C:/Users/VRINO/Desarrollo/Conquest_prototype/Docs/Mechanics/CapturePoints.md>) y TDD; que hoy no tenga un componente asignado no significa que quieras abandonar esa funcionalidad.

## Escenas y prefabs

| Grupo | Candidato | Archivos | MiB | Clasificación | Condición / evidencia |
|---|---|---:|---:|---|---|
| **S01** | Escenas antiguas de hÃ©roe e input | 4 | 2.03 | Opcional | Fuera de Build Settings y Addressables; subescenas referenciadas Ãºnicamente por sus respectivas escenas antiguas. BattleTestScene sigue conservada. |
| **S02** | BarraconScene aislada | 1 | 0.01 | Opcional | Sin ruta de carga ni referencias encontradas. La UI actual del barracÃ³n sigue dentro de otras escenas; no incluye sus scripts/prefabs. |
| **S03** | HiddenCityGeneralScene aislada | 1 | 1.43 | Opcional | Fuera del flujo registrado; podrÃ­a ser un mapa de autorÃ­a que quieras conservar. No incluye modelos ni DoorInteractable, que sÃ­ estÃ¡ activo. |
| **P01** | Prefabs de prueba modular | 2 | 3.80 | Opcional | Sin referencias detectadas; conservar modelos/modulares y prefabs de hÃ©roe utilizados por el registro visual. |
| **P02** | Prefabs de mapa sin instancias referenciadas | 23 | 0.87 | DecisiÃ³n de autorÃ­a | 23 prefabs, incluido TestMap; pueden ser catÃ¡logo de construcciÃ³n manual. Se enumeran exactamente en CSV; no implica que los assets Synty base sean eliminables. |
| **P03** | Prefabs antiguos de cÃ¡mara, HUD y marcador | 3 | 0.03 | Opcional | Sin referencias ni rutas de carga por nombre detectadas; no incluye cÃ¡maras/HUD incorporados a escenas activas. |
| **P04** | Configuraciones y efectos no asignados | 4 | 0.00 | Opcional | Sin asignaciones detectadas. Conservar clases de formaciÃ³n, efectos y SimplePricingConfig: son extensiones disponibles en el Editor y otros assets utilizan sus contratos. |


### S01 · Escenas antiguas de hÃ©roe e input

- [HeroTestCameraInputScene.unity](<C:/Users/VRINO/Desarrollo/Conquest_prototype/Assets/Scenes/HeroTestCameraInputScene.unity>)
- [DotsWorld.unity](<C:/Users/VRINO/Desarrollo/Conquest_prototype/Assets/Scenes/HeroTestInputScene/DotsWorld.unity>)
- [HeroTestScene.unity](<C:/Users/VRINO/Desarrollo/Conquest_prototype/Assets/Scenes/HeroTestScene.unity>)
- [TestSubScene.unity](<C:/Users/VRINO/Desarrollo/Conquest_prototype/Assets/Scenes/HeroTestScene/TestSubScene.unity>)

### S02 · BarraconScene aislada

- [BarraconScene.unity](<C:/Users/VRINO/Desarrollo/Conquest_prototype/Assets/Scenes/GameScenes/BarraconScene.unity>)

### S03 · HiddenCityGeneralScene aislada

- [HiddenCityGeneralScene.unity](<C:/Users/VRINO/Desarrollo/Conquest_prototype/Assets/Scenes/MapsScenes/HiddenCityGeneralScene.unity>)

### P01 · Prefabs de prueba modular

- [ModularDummy.prefab](<C:/Users/VRINO/Desarrollo/Conquest_prototype/Assets/Prefabs/esqueletos/ModularDummy.prefab>)
- [ModularEquimentTest.prefab](<C:/Users/VRINO/Desarrollo/Conquest_prototype/Assets/Prefabs/esqueletos/ModularEquimentTest.prefab>)

### P03 · Prefabs antiguos de cÃ¡mara, HUD y marcador

- [Main Camera.prefab](<C:/Users/VRINO/Desarrollo/Conquest_prototype/Assets/Prefabs/Camera/Main Camera.prefab>)
- [HUD.prefab](<C:/Users/VRINO/Desarrollo/Conquest_prototype/Assets/Prefabs/HUD/HUD.prefab>)
- [locationStick.prefab](<C:/Users/VRINO/Desarrollo/Conquest_prototype/Assets/Prefabs/Squads/locationStick.prefab>)

### P04 · Configuraciones y efectos no asignados

- [BaseWedgeFormation.asset](<C:/Users/VRINO/Desarrollo/Conquest_prototype/Assets/ScriptableObjects/Formations/BaseWedgeFormation.asset>)
- [SimplePricingConfig.asset](<C:/Users/VRINO/Desarrollo/Conquest_prototype/Assets/ScriptableObjects/Items/SimplePricingConfig.asset>)
- [BasicBowGrant.asset](<C:/Users/VRINO/Desarrollo/Conquest_prototype/Assets/ScriptableObjects/Items/itemEffects/BasicBowGrant.asset>)
- [SampleSceneProfile.asset](<C:/Users/VRINO/Desarrollo/Conquest_prototype/Assets/Settings/SampleSceneProfile.asset>)

P02 contiene exactamente los 23 prefabs enumerados en el CSV: TestMap, piezas de City/CityWalls/Ground y escaleras de Map/stairs. No se detectaron referencias serializadas, dependencias de escenas ni carga por nombre en código propio. Son candidatos de catálogo; es razonable conservarlos si los usas para diseñar mapas a mano.

Las subescenas de S01 solo las referencian sus propias escenas antiguas. En cambio, **SettlementHeroWorld** sigue referenciado por SettlementScene y SettlementPreview y no es candidato.

## Herramientas de pruebas y migraciones

| Grupo | Candidato | Archivos | MiB | Clasificación | Condición / evidencia |
|---|---|---:|---:|---|---|
| **T01** | MigraciÃ³n WorldMap ya ejecutada | 1 | 0.00 | Recomendado | Script manual fuera de Assets; sus rutas origen ya no existen y las rutas destino sÃ­ existen. No es herramienta para reconstruir el mapa. |
| **T02** | Constructores y validadores de laboratorios | 8 | 0.04 | Condicionado | Retirar sÃ³lo si se aprueba abandonar los laboratorios correspondientes y se actualiza el constructor de campaÃ±a. Mantener validaciones y horneados de la campaÃ±a actual. |


### T01 · MigraciÃ³n WorldMap ya ejecutada

- [ReorganizeWorldMap.cs](<C:/Users/VRINO/Desarrollo/Conquest_prototype/AgentScripts/ReorganizeWorldMap.cs>)

### T02 · Constructores y validadores de laboratorios

- [BuildCampaignRegion.cs](<C:/Users/VRINO/Desarrollo/Conquest_prototype/AgentScripts/BuildCampaignRegion.cs>)
- [BuildStrategicTrials.cs](<C:/Users/VRINO/Desarrollo/Conquest_prototype/AgentScripts/BuildStrategicTrials.cs>)
- [ValidateCampaignRegion.cs](<C:/Users/VRINO/Desarrollo/Conquest_prototype/AgentScripts/ValidateCampaignRegion.cs>)
- [ValidateCampaignScale.cs](<C:/Users/VRINO/Desarrollo/Conquest_prototype/AgentScripts/ValidateCampaignScale.cs>)
- [ValidateStrategicTrials.cs](<C:/Users/VRINO/Desarrollo/Conquest_prototype/AgentScripts/ValidateStrategicTrials.cs>)
- [WorldMapForestValidation.cs](<C:/Users/VRINO/Desarrollo/Conquest_prototype/AgentScripts/WorldMapForestValidation.cs>)
- [WorldMapGroundValidation.cs](<C:/Users/VRINO/Desarrollo/Conquest_prototype/AgentScripts/WorldMapGroundValidation.cs>)
- [WorldMapLabSetup.cs](<C:/Users/VRINO/Desarrollo/Conquest_prototype/AgentScripts/WorldMapLabSetup.cs>)

`AgentScripts` está fuera de `Assets`: son herramientas manuales, no código del jugador. La ausencia de llamadas de Unity no las convierte en código muerto. Conservar los horneados y validadores actuales de campaña, costas, navegación y conexión BronzeAge, así como BuildCampaignContent, WorldMapBuildV9 y las comprobaciones de V9 que se quieran seguir usando.

## Capturas, builds locales y ejemplos

| Grupo | Candidato | Archivos | MiB | Clasificación | Condición / evidencia |
|---|---|---:|---:|---|---|
| **L01** | Capturas Mountains V5-V8 | 13 | 17.46 | Recomendado | 13 capturas de versiones anteriores; no se encontraron referencias documentales a estas imÃ¡genes. |
| **L02** | Otras capturas y diagnÃ³sticos | 52 | 76.36 | Opcional | Material de documentaciÃ³n, no runtime. Seis imÃ¡genes estÃ¡n enlazadas en documentos; actualizar esos enlaces si se eliminan. Coasts V1 no equivale a una versiÃ³n antigua de Mountains. |
| **L03** | Builds FPS V7 y V8 | 497 | 834.81 | Recomendado | 497 archivos locales ignorados por Git. Preservar WorldMapV9Fps como Ãºltima build del benchmark salvo aprobaciÃ³n distinta. |
| **L04** | Cache Graphify duplicada dentro de Assets | 529 | 2.10 | Recomendado | 529 JSON de cache, ignorados por Git. El grafo vigente y GRAPH_REPORT estÃ¡n en graphify-out en la raÃ­z; no se incluyen. |
| **L05** | Dump de batalla antiguo | 1 | 0.04 | Opcional | Un dump histÃ³rico local ignorado por Git; BattleDataDumpTool y su menÃº permanecen. |
| **V01** | Escena demo de texturas Yoge | 1 | 0.08 | Opcional | Una escena de ejemplo sin referencias detectadas. Mantener las texturas Yoge: el mapa actual sÃ­ las usa. |

L02 contiene 52 capturas restantes. Seis están enlazadas desde documentación: `campaign-interaction.png`, `campaign-settlement-menu.png`, `settlement-scene-gameplay.png`, `forest-masses-close.png`, `forest-masses-panorama.png` y `worldmap-local-route.png`. Si se aprueba borrar L02, hay que retirar o sustituir esos enlaces; se pueden excluir esas seis imágenes.

No incluyo Library, Temp ni logs del Editor en la propuesta: son datos de la sesión abierta. Los resultados históricos de tests en Logs se pueden archivar aparte, preservando las evidencias recientes de inventario/animación y de esta auditoría. `.git` y los objetos LFS no forman parte de esta limpieza.

## Limpiezas parciales de configuración

| ID | Cambio propuesto | Evidencia y límite |
|---|---|---|
| **R01** | Retirar la entrada deshabilitada `Assets/Scenes/SampleScene.unity` de Build Settings. | El archivo no existe. Conservar las nueve escenas habilitadas. No eliminar EditorBuildSettings.asset. |
| **R02** | Retirar el alias `SceneNames.HeroSelection = "HeroSelectionScene"` y su entrada de fase. | Esa escena no existe y no se encontraron llamadas que carguen el alias. Mantener CharacterSelection y HeroSelectionSceneController, usados por el flujo actual. |

R01/R02 son ediciones puntuales, por eso no figuran como archivos para borrar en el CSV. M07 requiere además retirar la entrada Addressables `worldmap/lab` de WorldMap-Lab; las direcciones y grupos actuales de campaña se conservan.

## Elementos que parecen pruebas pero siguen activos

- **BattleTestScene y su DotsWorld**: banco actual de pruebas de locomoción, animación y combate. No retirar.
- **Assets/Tests/EditMode y PlayMode**: suite de regresión activa, incluidas las pruebas nuevas de inventario y visuales. No tratar su antigüedad como motivo de eliminación.
- **TestEnvironmentInitializer, BattleDebugCreator, TestHeroPool y Resources/Debug/TestHeroPool.json**: utilizados por escenas, matchmaking local y tests actuales.
- **CombatGizmosDebug**: asignado a BattleTestScene. Opcional como herramienta, pero tiene uso comprobado y queda fuera del inventario de código muerto.
- **SamplePlayerAnimationController_ECS, EcsAnimationInputAdapter y UnitAnimatorController**: forman parte de la animación activa. «Sample» no implica que sean ejemplos prescindibles.
- **SettlementPreview y SettlementWalkMode**: también participan en SettlementScene. SettlementWalkTests es una prueba manual invocable desde herramientas, no un archivo muerto demostrado.
- **Squad_Item_placeholder.prefab**, **DoorInteractable** y **NpcTriggerZone**: tienen referencias en escenas activas.
- **SimplePricingConfig, BasicArmorStatGenerator y clases de efectos de ítems/diálogo**: instanciables o serializadas de forma polimórfica; no necesitan un llamador directo por nombre para estar activas.
- **Sistemas ECS y Bakers vigentes**: Unity puede descubrirlos automáticamente; no eliminarlos solo por no encontrar llamadas a sus clases.
- **TextMesh Pro Examples & Extras**: hay fuentes/assets de ese árbol usados por el proyecto. No aprobar su carpeta completa por ser «Examples».
- **Texturas Yoge, assets Synty y VegetationSpawner**: comparten dependencias con el mapa y su generación. V01 solo comprende la escena demo especificada.

## Orden sugerido para una futura limpieza

1. **M01, M02, M03, M04, M05, C01, C02, C03, T01 y L01**: versiones y residuos con evidencia clara. M04 debe ir después de M01/M02. Revisar R01/R02 en la misma fase.
2. **L03 y L04**: liberar almacenamiento local de benchmarks antiguos y cache duplicada. Estos archivos están ignorados por Git.
3. **S01–S03, P01–P04 y C04–C05**: decidir qué material de autoría y funcionalidades pendientes deseas conservar.
4. **M06–M12 y T02**: retirar laboratorios solo con una decisión sobre la reconstrucción del mapa; preservar o trasladar recursos compartidos y adaptar las herramientas antes.
5. **L02, L05 y V01**: limpieza documental y de ejemplos según tus preferencias.

En la primera fase de assets/código propuesta hay aproximadamente **460,17 MiB**; L03/L04 añaden **836,91 MiB** locales. La selección completa recomendada, excluyendo M04 condicionado, suma **1.278,84 MiB**. Los otros grupos requieren tus decisiones de autoría o funcionalidad.

Tras una selección aprobada, la eliminación se haría por grupos con Unity para preservar referencias; se comprobarían compilación, las pruebas relevantes y el contenido Addressables cuando corresponda. Este documento y el CSV son propuestas revisables, no autorización para ejecutar borrados.
