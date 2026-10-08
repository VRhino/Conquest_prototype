# Integración de campaña regional

Orden autorizado: conservar preview; escena jugable y sectores; movimiento autoritativo; transiciones; geografía persistida y estado dinámico; niebla al final.

La primera entrega usa el snapshot V9 como contenido estático local. Cada sector se carga por Addressables y se libera fuera del radio de precarga. La escena no referencia los 48 TerrainData detallados. Los terrenos lejanos son aproximaciones visuales sin colliders ni bosque económico.

El recorrido A/B y los símbolos urbanos son fixtures de presentación. La entrada de prueba lleva a SettlementScene; no identifica una ciudad real del servidor. El estado de retorno conserva cámara y figura local, sin duplicar PlayerSessionService. No conectar estas coordenadas a órdenes de producción o combate.

Pendientes tras validar el primer recorrido: proveedor de sesión BronzeAge, identidad del mundo persistido, proyección de ciudades/ejércitos, órdenes de movimiento y rutas del motor, entrada real por ID, batalla con su contrato, área de interés, niebla y pruebas de carga. La preview de autoría conserva todo el mundo; el cliente jugable deberá respetar filtrado de entidades desde que conecte al servidor.


## Primer hito completado: escena local con sectores

Escena: `Assets/Scenes/GameScenes/WorldMapCampaign.unity`. Se conserva la preview completa para autoría y balance.

- 48 sectores de 4.000 unidades: carga independiente por Addressables; hasta nueve cercanos detallados y una carga transitoria pendiente.
- 48 terrenos lejanos de resolución 33, sin árboles ni colliders, mantienen la continuidad visual.
- Las alturas detalladas siguen idénticas al snapshot V9; se conservan 1.360 IDs de recursos estáticos. Los 67.497 símbolos de copas son representación visual. El despeje local de ciudades y recorrido no modifica el bosque económico.
- Recorrido A/B: 1.200 unidades a 12 u/s, 100 segundos simulados sin obstáculos. Entrada de prueba al alcanzar el radio de 18 unidades.
- Entrada a SettlementPreview y retorno al mapa conservan posición, cámara y recorrido. Un único puente persistente reutiliza SceneTransitionService.

Validación estructural: 48 alturas comparadas, direcciones únicas, IDs conservados y ausencia de dependencias directas de sectores detallados en la escena. Validación en Play con contenido empaquetado: proveedor BundledAssetProvider, nueve sectores cargados, cambio de zona con cargas/liberaciones, entrada/regreso y parada sin errores de consola. La compilación del contenido Addressables terminó correctamente. Esto no equivale a una build de jugador ni a un benchmark de FPS.

Para probar: abrir la escena y pulsar Play; «Viaje local a B», «Entrar en asentamiento de prueba» y «Volver al mapa de campaña». WASD desplaza la cámara, botón derecho orbita y rueda ajusta zoom. Los botones de zona comprueban streaming y no teletransportan el ejército.

Siguiente hito: contrato de sesión/mundo persistido y proyección autoritativa del movimiento; después entrada real por ID y batalla, caché de geografía y estado dinámico, y finalmente niebla. Esta escena todavía no envía órdenes, no identifica ciudades reales y no aplica filtrado del servidor. Las transiciones reales deberán consumir esos contratos; el recorrido local no sustituye la navegación del motor.


La interacción local ya dispone de clic/etiqueta, ficha y Entrar para la ciudad seleccionada; se eliminó la entrada fija a B de la interfaz habitual. Salir al mapa conserva el punto exterior. Detalles y límites: WorldMapInteraction.md.


Primera etapa del proveedor BronzeAge preparada: adaptador de proyección de jugador, identidad/posición validada y transporte HTTP de lectura opcional. No configurado con sesión real. Ver BronzeAgeCampaignConnection.md para contratos, validación y pendientes.


El usuario posterga el backend: prioridad actual campaña autocontenida Unity. Navegación local por destinos y ficha Acercarse implementadas; ver WorldMapLocalPlay.md para límites y próximos pasos.


Prioridad actual: representación del mundo, movimiento e interacciones en pausa. Primera pasada de masas forestales aplicada y revisada; ver WorldMapForestMasses.md.
