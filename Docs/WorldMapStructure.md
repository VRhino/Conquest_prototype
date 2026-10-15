# Estructura WorldMap

- Assets/Scenes/GameScenes/WorldMapCampaign.unity: escena de campana destinada al juego; actualmente contiene un recorrido de prueba local.
- Assets/Scenes/GameScenes/SettlementScene.unity: entrada al asentamiento en paseo 3D, parte del juego.
- Assets/Scenes/WorldMapTools/SettlementPreview.unity: visor del asentamiento para inspeccion. BattleScene permanece en GameScenes.
- Assets/Scenes/WorldMapTools/WorldMapPreview.unity: preview global para autoria.
- Assets/Scenes/WorldMapTools/Experiments: pruebas de balance y representacion con sus datos auxiliares; fuera del flujo del jugador.
- La cadena `MountainsV9 -> CampaignScale -> CampaignRegion -> StrategicTrials` conserva fuentes reproducibles para construir la campaña; MountainsV7 y MountainsV8 son referencias históricas bajo `Experiments`, no contenido del juego.
- `AddressableAssetsData/AssetGroups/WorldMap-Lab` excluye `LandscapeLab` de la build (`m_IncludeInBuild: 0`); no mover fuentes de `WorldMapTools` sin actualizar primero sus constructores de Editor.
- Assets/Scripts/Map/WorldMap: runtime, streaming, datos y representacion; ensamblado Conquest.WorldMap.
- Assets/Scripts/Map/CampaignSceneBridge.cs: adaptacion al servicio compartido de escenas, fuera del ensamblado visual para evitar dependencias circulares.
- Assets/Scripts/Editor/WorldMap: herramientas de importacion y construccion; ensamblado exclusivo del Editor.
- Assets/WorldMap/Campaign: manifiesto y sectores empaquetables.
- Assets/WorldMap/CampaignArt y Shaders: recursos visuales.

Flujo previsto: acercarse al asentamiento, clic, menu contextual de acciones, Entrar, SettlementScene y regreso al mapa. La primera ficha contextual y la transicion estan implementadas para las dos ciudades locales. La identidad y los permisos del servidor siguen pendientes.

Se conservan nombres de escenas, GUID, ensamblados y direcciones Addressables. Se actualizan rutas de Build Settings, constructores y validadores. Las pruebas no se agregan a Build Settings por este traslado.
