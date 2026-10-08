# Interacción inicial de campaña

WorldMapCampaign: clic en la miniatura o etiqueta de ciudad, ficha inferior y botón Entrar. Se puede consultar desde lejos; entrar exige estar a <=18 unidades horizontales de su acceso y tener cargado el terreno del jugador. Dos ciudades locales con identificadores propios de presentación, nombre, nivel, región y acceso. El componente WorldMapSettlementTarget contiene la fuente y el ID de la proyección que abre SettlementPreview. Ambos ejemplos usan settlement2, el único trazado usado en esta prueba; no son dos ciudades reales del servidor.

SettlementPreview: botón Salir al mapa. Recupera la posición exterior, cámara, seguimiento y progreso previos. No teletransporta a un centro de ciudad ni genera una sesión nueva. Al salir del modo de paseo, se restaura el cursor. La escena de asentamiento permanece intacta; recibe source/settlementId antes de Start mediante sceneLoaded.

Controles: WASD cámara; rueda zoom; botón derecho órbita; Escape cierra ficha. Seguir jugador centra el ejército. Viajar a ciudad B es todavía el recorrido local A/B. F3 muestra el diagnóstico de streaming. Se conserva movimiento a 12 u/s y el radio de entrada configurable en Inspector.

Referencia revisada: A Total War Saga: Troy, HUD de campaña en https://www.gameuidatabase.com/gameData.php?id=1234&autoload=48118 . Se toma la composición de etiquetas y ficha inferior, sin copiar sus recursos gráficos. La interfaz reutiliza IMGUI, utilizado por SettlementPreview, con paneles oscuros y bordes bronce. Próximos permisos/acciones deben venir de la proyección autorizada, no de esta ficha local.

Validación en Editor Play: selección por rayo de City A=true; entrada en A=true; entrada a B desde A=false; recorrido de 1200 unidades habilita B; entrada mediante RequestSelectedEntry llega a SettlementPreview con settlement2 y asentamiento-0-40-80, geometría generada; regreso distancia horizontal al acceso de B=0, progreso=1200, 9 sectores y 1 puente persistente. Capturas campaign-interaction.png y campaign-settlement-menu.png revisadas. Consola sin errores tras parada. No es una prueba de sesión de servidor ni una build de jugador.


## Separación de escena jugable y visor

La entrada actual abre Assets/Scenes/GameScenes/SettlementScene.unity, copia de la geometría existente con showPreviewUI=false, enterOnStart=true y allowPreviewExit=false. SettlementPreview se conserva en Assets/Scenes/WorldMapTools/SettlementPreview.unity, con el mismo GUID y sus controles originales, fuera de Build Settings. SettlementScene sustituye a la preview en la build.

En la escena jugable: WASD caminar; Shift correr; ratón cámara. Mantener Alt libera el cursor y bloquea la locomoción para usar Salir al mapa; Esc sale directamente al mapa si hay contexto de entrada. No vuelve al visor cenital. Se reutiliza el sistema ECS existente de ciclo de vida del héroe.

Validación: entrada desde City A → SettlementScene; Requested=true, Ready=true, showPreviewUI=false, allowPreviewExit=false; captura settlement-scene-gameplay.png revisada con modelo 3D. Regreso → WorldMapCampaign; distancia al acceso A=0, nueve sectores y un único puente. Compilación sin errores. La geometría sigue siendo el blockout existente y la proyección local compartida.
