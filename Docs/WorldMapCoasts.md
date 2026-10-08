# Costas reproducibles de campaña

El generador regional produce `costas-1`: plataformas costeras y frentes con intensidad, altura y anchura que varían con la semilla. La clasificación conserva costa suave en delta/marismas y protege desembocaduras. El evaluador de altura aplica el campo antes de generar bosque, suelos, recursos y accesos. No cambia la entrada de partidas v16 ni activa conexión al backend.

Unity importa vértices y alturas del paquete. Los frentes no se colocan manualmente ni dependen de Unity.Random. La seed 42 aceptada se reconstruye desde campos canónicos guardados, conservando IDs y posiciones horizontales de recursos, ríos y lagos. Una nueva generación normal coloca recursos después del nuevo relieve.

Paquete de comparación: `C:/Users/VRINO/.codex/worktrees/worldgen-relieve/BronzeAgeFase0/exports-regional/campaign-coasts-v1-seed42/manifest.json`.

- Menú: Tools/BronzeAge/Campaña/Importar costas del worldgen (abrir WorldMapCampaign y salir de Play).
- Construcción: WorldMapCampaignCoastBuilder aplica el paquete con checksum y coincidencia de seed/dimensiones.
- Assets: Assets/WorldMap/Campaign/Coasts. 48 TerrainData independientes y frentes por sector.
- 304 frentes rocosos exportados. La representación tesela su huella a 6 unidades y sigue el heightmap con 2,5 unidades de separación visual, evitando intersecciones triangulares. Cerca y lejos alternan con el streaming actual.
- Pintura V9 conservada, roca añadida sobre los frentes. Se reutilizan los TerrainData persistentes para no sustituir su estado nativo de texturas mediante CopySerialized.
- Cada SplatAlpha se marca para guardado. Tras guardar, se recargan los 48 sectores y 48 overviews y se verifica que el hash de la mezcla es idéntico. Esta prueba detecta la regresión del suelo uniforme.
- Navegación local recalculada; source SHA coincide con el nuevo paquete.

Validación: 20 pruebas regionales, semillas 1/7/42, coste/costas/serialización y exportación; checksums y 82 bordes exactos del paquete. En Unity, alturas dentro de un paso R16, caras exactas, árboles anclados, 1360 recursos, 67497 copas y carga de como máximo 9 sectores. No se ha medido un nuevo benchmark de FPS.

Esta es la primera iteración del perfil costero. Su altura, frecuencia y transiciones siguen siendo parámetros de worldgen que deben calibrarse con la vista de campaña; no son posiciones autoradas a mano.


Representación: 113502 triángulos de costa en todo el mundo. La malla sigue la envolvente de las dos diagonales del R16 y cubre 24 unidades adicionales del hombro para evitar puntas expuestas por el Terrain. Material CampaignCoastalRock con variación mineral y estratos en coordenadas globales; conserva los materiales de suelo y montaña existentes. No usa piedras individuales ni decisiones aleatorias de ubicación.



