# Masas forestales de campaña

Cambio visual aplicado a los 48 sectores Addressables de WorldMapCampaign. Conserva las 67497 posiciones de copas y todos los datos de geografía. No modifica el bosque económico, los recursos ni el worldgen. Las escenas de experimentación previas permanecen intactas como referencia.

El estado anterior usaba una copa por celda de 50u, ancho original x2.2 y altura x0.65. La nueva versión normaliza el diámetro por el tamaño real del modelo Synty: antes un árbol delgado y una copa ancha recibían el mismo multiplicador y quedaban huecos poco coherentes. Se mantienen los modelos redondeados y grupos de pinos; 9706 copas delgadas se sustituyen por variantes redondeadas de los mismos assets.

La concentración procede de los árboles nativos del V9, agregados en coordenadas globales. Vecindario de 3x3 celdas de 50u: combinación de ocupación (60%) y cantidad (40%). Así no se reinicia el cálculo en límites de sector. Interiores densos: diámetro hasta 98u; márgenes: alrededor de 34u; altura 21–32u. Ruido continuo varía tamaño, altura y matiz por manchas amplias. Promedio de diámetro 80.1u; 45417 copas clasificadas como interiores densos y 8950 como márgenes abiertos. Es concentración visual, no densidad económica.

Se conserva la irregularidad de posiciones y claros ya exportados; no se añaden árboles en zonas originalmente sin ellos. Se limita expansión cerca del agua usando la rejilla local horneada y se mantienen claros alrededor de las ciudades de prueba. La rejilla de 62.5u hace conservadora esa protección de orilla. Los claros existentes de la prueba A/B tampoco se regeneran. No hay simulación de tala ni recuperación.

Datos editables: Assets/WorldMap/Campaign/ForestMasses/Profile.asset. Parámetros: diámetros/alturas de interior y margen, variación y escalas del ruido. Tras editar, ejecutar AgentScripts/BuildCampaignForestMasses.cs para hornear. Cada sector usa su copia TerrainData en ForestMasses; los datos anteriores permanecen intactos. Docs/ForestMassBaseline.json guarda sus GUID para restaurar referencias si se necesita comparar. El horneado siempre parte de esos datos anteriores para no acumular escalas o colores.

Validación estructural: alturas exactas de los 48 sectores, 1360 IDs de recursos, 67497 copas, colliders sincronizados y sin referencias de terreno detallado en la escena principal. Play: nueve sectores cargados, cambio de zona sin error de streaming. Capturas forest-masses-close.png y forest-masses-panorama.png revisadas: mayor continuidad de cobertura, márgenes abiertos y tamaños diferenciados. La inspección de la península costera confirma que no se ha llenado de bosque una zona originalmente despejada. El conteo igual no garantiza idénticos FPS: aumenta el solapamiento y requiere una medición de rendimiento específica.

Pendiente para futuras pasadas: color según región y contraste con suelo; representación forestal en terrenos lejanos sin detalle; refinamiento de cobertura en orillas y pendientes y perfilado de sombras/sobreposición. Esta entrega modifica solo las masas de copas de sectores detallados.


Contenido Addressables actualizado: RunNow de BuildCampaignContent completado correctamente; duración de compilación 37.666s. Consola del Editor sin errores al cerrar la revisión. Las capturas se validaron en Play del Editor; no se ha realizado un benchmark de FPS ni una nueva build ejecutable.
