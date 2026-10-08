# Preview del mundo regional

Primera fase del plan de mapa de campaña. Abre **Tools → BronzeAge → Abrir preview del mundo regional** y pulsa Play. El menú permite guardar la escena que estabas editando antes de cambiarla y adapta los materiales del paquete a URP.

Escena: `Assets/Scenes/WorldMapTools/WorldMapPreview.unity`. Mundo local de 32 × 24 km, seed 42, 48 terrenos y 1.354 marcadores de recursos. Los datos vienen del paquete regional-11 validado en el worktree worldgen-relieve.

Abre en perspectiva 3D sobre terrenos reales de Unity. Controles: arrastrar con botón izquierdo o central para mover; botón derecho para orbitar e inclinar; rueda para acercar la cámara; doble clic sobre el terreno para inspeccionar esa zona; clic sobre un marcador para consultar sus datos. El panel permite volver a la vista general, activar una vista cenital opcional y filtrar recursos. La cámara sigue la altura del terreno y conserva una separación mínima del suelo. La entrada usa eventos de IMGUI y no cambia la configuración de Input System del proyecto.

La preview carga el mundo completo. No es todavía el streaming del cliente: sirve para inspeccionar escala y distribución. No hay simulación, entidades ECS, red ni niebla. Los identificadores de recursos se conservan para la futura conexión al servidor.

## Representación de campaña

Al abrir desde el menú se aplica `WorldCampaignArt`: copias de las texturas Synty de hierba y arena adaptadas a la paleta del Mediterráneo oriental, roca procedural mate, suelo forestal, luz cálida lateral y agua con gradiente costero y movimiento suave. Las texturas originales del paquete artístico no se modifican.

Las máscaras geográficas originales se conservan como `tile-*.layers.bytes` junto a los terrenos. Se usan para reconstruir la mezcla de superficies y distribuir modelos de Fantasy Kingdom: árboles redondeados, árboles de ribera y grupos de pinos de montaña. Se conservan las mallas, UV y paletas originales. `WorldMapSyntyRenderer` dibuja instancias en grupos de hasta 1.023 por malla/material, descarta grupos fuera de cámara y usa árboles genéricos menos densos a distancia. No crea GameObjects ni colliders por árbol; no afecta a la economía ni representa árboles talables.

Rocas y acantilados acompañan las pendientes. Cada nodo de piedra recibe una roca visual conservando su identidad y posición canónica. Las rocas se escalan por su dimensión mayor para no convertir modelos planos en enormes losas; los árboles y acantilados se escalan por altura.

La revisión 5 refuerza las montañas: la cobertura de roca aumenta entre 8 y 26 grados de pendiente, usa textura mineral y normales propias, y reduce visualmente el suelo forestal en las caras abruptas. Los acantilados siguen la orientación de la ladera, se ensanchan y se hunden parcialmente para integrarse. `Campaign Terrain` y su pase lejano añaden iluminación facetada sobre la geometría original, sin mallas superpuestas ni desplazamiento de alturas. Se derivan del Terrain Lit de URP 17.5; conservan su licencia en `Shaders/URP-Terrain-LICENSE.md`. Al actualizar URP deben revisarse las diferencias con los shaders oficiales. La prueba compara todas las muestras de altura antes/después y rechaza capturas vacías o con materiales magenta.

`Assets/WorldMap/CampaignArt/SyntySettings.asset` permite ajustar familias, separación, escala, pendientes, densidad de rocas y distancia de detalle. Tras cambiar parámetros de distribución, usa **Tools → BronzeAge → Reconstruir modelos Synty del mundo** y guarda la escena. Los datos generados se reutilizan en aperturas posteriores. Los parámetros de distancia se leen al dibujar. La revisión 3 usa grupos de 9 árboles en bosque denso y 3 en bosque abierto, extendidos sobre 100 m, frente a los grupos anteriores de 3 y 1 sobre 55 m. El detalle lejano conserva uno de cada 8 árboles.

En Play, los recursos se muestran mediante símbolos de consulta en lugar de esferas. La vista inicial se acerca a una zona de campaña; el botón Mundo conserva el acceso al mapa completo. La interfaz usa una paleta de bronce y carbón.

`Tools/BronzeAge/Aplicar aspecto de campaña a la preview` permite aplicarlo sobre la escena abierta. Guarda la escena después de usar ese comando. La primera aplicación sustituye las copas procedurales anteriores.

`WorldCampaignWater` redondea las esquinas de los lagos y añade bandas de agua somera junto al borde. `WorldMapLakeSource` conserva la malla importada para que las sucesivas reconstrucciones no reduzcan el lago otra vez. Los ríos usan muestras cada 6 m y cinco vértices por sección, curvas de desplazamiento limitado y animación de corriente. Las tiras se recortan al entrar en lagos o mar, y los árboles mantienen una separación del cauce. El detalle del terreno sigue limitado por las alturas originales cada 31,25 m: no hay excavación de cauces ni simulación hidráulica nueva.

`CampaignArt/WaterSettings.asset` permite ajustar paso de muestreo, separación vertical, ancho visual, redondeado, franja somera y separación de árboles. Tras cambiarlos aplica el aspecto de campaña; si cambias el ancho o la separación forestal, reconstruye también los modelos Synty. Los colores de agua interior se definen en `WorldCampaignWater`. El fixture valida 17 lagos y 48 tramos de río, alturas originales, recursos, estabilidad de las reconstrucciones y renders en URP.

La integración Synty se validó en un proyecto temporal con Unity 6000.5.8f1 y URP 17.5, comprobando referencias de malla/material, límites de instancias y renders de conjunto, detalle y cercanía. Se inspeccionaron las imágenes y se rechazaron capturas sin variación de color. El fixture seed 42 genera 57.274 árboles, 796 rocas y 569 acantilados, distribuidos en 477 grupos. La geometría total de detalle suma aproximadamente 27,4 millones de triángulos; la representación lejana, 3,2 millones. Son totales del mundo, no una medición de triángulos visibles ni de FPS. La preview todavía carga todos los terrenos y datos de instancias; el descarte visual no sustituye el streaming pendiente.

El código vive en los ensamblados independientes `Conquest.WorldMap` y `Conquest.WorldMap.Editor`. El importador es la copia del paquete validado. `WorldMapPreviewBuilder` permite crear otra escena desde un manifest sin sobrescribir las anteriores; `WorldMapPreviewController` controla solamente la presentación. No se han añadido fases ni cambiado las escenas de asentamiento o combate.

## Validación

### Perfil del Mediterráneo oriental (revisión 8)

`Assets/WorldMap/CampaignArt/EasternMediterranean.asset` define la dirección visual para el periodo entre el final de la Edad de Bronce y Alejandro: suelo seco ocre, roca clara inspirada en calizas, verdes contenidos y riberas fértiles. Los modelos son representaciones Synty genéricas; los árboles disponibles no equivalen a una selección botánica definitiva de olivos, cipreses o palmeras. Se usan únicamente árboles, rocas y acantilados, sin construcciones de fantasía del paquete.

El perfil permite ajustar colores, contraste de las texturas, escala de la roca, variación amplia del terreno, intensidad de facetas, sol, luz ambiente y aspecto del agua. Reaplicar mediante **Tools → BronzeAge → Aplicar aspecto de campaña a la preview**. Si la preview está abierta al recompilar, se actualiza una sola vez cuando falta el perfil o la caché está desactualizada; la escena queda marcada para guardar, sin guardado automático de cambios del usuario. Abrir desde el menú general aplica y guarda la escena como antes.

Las superficies usan cuatro capas normalizadas para evitar el segundo pase de mezcla que requería la quinta. La roca se proyecta en tres ejes sobre las pendientes, usando normales de la geometría para funcionar también con Terrain instanciado. La variación amplia usa coordenadas del mundo en los pases cercano y lejano para conservar continuidad entre terrenos. El perfil mantiene el material detallado hasta 6,5 km para que la cámara de campaña no pase prematuramente a una textura lejana con roca estirada; esta distancia es editable. La proyección triplanar añade muestras en las laderas; la reducción a cuatro capas no constituye por sí sola una medición de FPS.

El agua utiliza un shader URP con constantes compatibles con SRP Batcher, reflejos suaves y corriente siguiendo la distancia del trazado. Mar, lagos y ríos tienen colores independientes; los ríos usan un tono más sedimentario. El shader conserva el gradiente local de orillas y no cambia geometría, elevaciones ni vados.

La caché Synty se reconstruye en la revisión 8. Los grupos cercanos ocupan celdas de 1 km, configurables en `SyntySettings.asset`. Una segunda caché agrupa por terreno la representación lejana y las rocas para la vista global. Se usa solo cuando la distancia desde la cámara a los límites de todas las instancias garantiza que ninguna necesita detalle. Las dos cachés son alternativas y nunca se dibujan juntas. El renderer reutiliza los seis planos del frustum para evitar asignaciones por cámara.

Comparación reproducible de geometría Synty enviada para seed 42 (cámara idéntica, mismas posiciones y modelos; no incluye las llamadas del terreno y del agua):

| Vista | Grupos de 4 km: llamadas / triángulos | Grupos de 1 km y caché global: llamadas / triángulos |
| --- | --- | --- |
| Campaña | 93 / 5.043.058 | 335 / 3.468.542 |
| Cercana | 33 / 3.554.828 | 73 / 1.932.984 |
| Mundo completo | 253 / 3.772.632 | 253 / 3.772.632 |

El descarte reduce aproximadamente un 46 % los triángulos enviados en la vista cercana medida, a cambio de más llamadas. No es una afirmación de mejora de FPS; falta perfilar CPU/GPU en la escena final. Se mantienen 57.274 árboles, 796 rocas y 569 acantilados. El fixture conserva 48 terrenos, 1.354 recursos, 17 lagos y 48 ríos. La validación en Unity 6000.5.8f1 / URP 17.5 comprueba alturas sin cambios, máscaras normalizadas, shaders y estabilidad de lagos al repetir la aplicación.

MicroSplat, Stylized Water 3, GPU Instancer Pro y MicroVerse siguen siendo opciones de integración posterior. Esta revisión usa los recursos ya disponibles y no depende de esos paquetes.

`WorldMapPreviewBuilder.BatchBuild` compila, construye y vuelve a abrir la escena en un proyecto temporal con Unity 6000.5.8f1. Verifica el número de terrenos/recursos y las referencias serializadas de cámara y raíz. Se ejecuta con `BRONZEAGE_PREVIEW_MANIFEST` apuntando al paquete y `-executeMethod Conquest.WorldMap.Editor.WorldMapPreviewBuilder.BatchBuild`. Los conteos de esa prueba corresponden al fixture seed 42, no restringen la importación desde el menú.

La validación automática no sustituye la revisión visual ni las mediciones en el proyecto completo. El paquete de alturas usa muestras cada 31,25 m; su máximo error medido en centros de celda es 7,64 m.

## Laboratorio de herramientas gratuitas

Instalados y compilados en Unity 6000.5.8f1: Terrain Tools 5.3.3 y Addressables 4.1.0.
La escena `Assets/Scenes/WorldMapTools/Experiments/LandscapeLab.unity` es una copia guardada de la preview
con sus 48 TerrainData y materiales de terreno independientes, además de un perfil artístico propio.
Abrir con `Tools/BronzeAge/Pruebas/Abrir laboratorio de paisaje`.
El menú de creación no sobrescribe un laboratorio existente y deja abierta la escena anterior.

Los modelos, texturas, TerrainLayers y la caché de instancias Synty siguen siendo referencias compartidas:
no editar esos assets originales desde el laboratorio. Para probar Vegetation Spawner, desactivar
`WorldMapSyntyRenderer` únicamente en esta escena para evitar árboles duplicados. Asignar los prefabs Synty
y adaptar las reglas a las máscaras regionales existentes; el paquete no incorpora biomas regionales por sí solo.
No cambiar la geografía ni los recursos del servidor durante esta prueba visual.

Vegetation Spawner 1.1.0 está importado desde Asset Store y compila en Unity 6000.5.8f1.
`Tools/BronzeAge/Pruebas/Generar bosques con Vegetation Spawner` configura cuatro sectores próximos
a la cámara de campaña con prefabs Synty adaptados a la escala visual (55 m),
variación de tamaño y tres familias. La prueba inicial de 65 m produjo 3.092 instancias.
Ahora utiliza muestreo propio de radio variable en coordenadas globales, con interpolación bilineal
de densidad y exclusión mutua entre especies y sectores. Vegetation Spawner registra los prototipos
nativos de Terrain; su muestreo de distancia fija y su filtro de probabilidad de capas no intervienen.
Se conservan los campos canónicos de densidad/tipo de bosque y las exclusiones de lagos, pendientes y ríos.
No usar el botón nativo de respawn como sustituto de este menú: omite estas reglas.
La prueba inicial de densidad variable generó 29.954 instancias en cuatro sectores.
Sus bordes mostraban cortes rectos porque allí se retomaba la representación anterior.
Ahora el laboratorio hornea los 48 sectores conjuntamente: 192.974 instancias,
152.875 en densidades visuales >=0,8. La preview principal conserva su representación anterior.

`Tools/BronzeAge/Pruebas/Editar densidad de bosques` selecciona `ForestDensityProfile.asset`.
La curva densidad/separación parte de 65 m a densidad 0,18, 50 m a 0,35, 32 m a 0,60,
23 m a 0,80 y 17 m a 1. La variación de escala es 0,85–1,15. Editar el perfil y ejecutar
el menú de generación para reconstruir y guardar; no se regenera durante la navegación.
Los núcleos cerrados se construyen reduciendo la separación, sin descartes aleatorios que abran agujeros.
`edgeNoiseScale` (180 m), `edgeDetailScale` (45 m) y `edgeDensityVariation` (0,24)
reducen la densidad de la zona de transición con ruido coherente en coordenadas mundiales.
Esto produce entrantes y arbolado disperso sin extender árboles fuera de la máscara forestal
canónica. El núcleo con densidad >=0,9 queda intacto. No usar las fronteras de Terrain como
fronteras del bosque: el horneado conjunto y el ruido mundial evitan discontinuidades de sector.
La semilla controla candidatos, orden, modelo, rotación y tamaño. Repetir con la misma selección
de sectores y perfil genera exactamente las mismas instancias. El futuro horneado completo debe
resolver las fronteras conjuntamente (o con halos); cambiar el conjunto de sectores de una generación
parcial puede cambiar candidatos aceptados junto a sus bordes.

`AgentScripts/WorldMapForestValidation.cs` comprueba repetibilidad, distancias entre especies/sectores,
densidad, lagos, pendientes, altura de los árboles, TerrainData independiente y geografía/recursos intactos.
Resultado guardado en `ForestValidation.json`: mínimo 17,0001 m, 30.454 pares vecinos entre sectores
comprobados, 798.768 alturas iguales y 1.354 recursos. Tiempo de generación/guardado observado:
aproximadamente 8,8 s para los 48 sectores en Editor; no equivale a rendimiento de renderizado.
Capturas: `Assets/Screenshots/vegetation-organic-border.png` (misma cámara del corte anterior),
`vegetation-organic-transition.png` (borde natural) y `vegetation-organic-world.png` (vista global).

Los siete prefabs del laboratorio tienen tres LOD. `WorldMapForestLod` hornea agrupación de vértices,
conservando materiales y separaciones de la paleta UV, y reconstruye normales planas.
Solo escribe mallas y prefabs del laboratorio; no modifica Synty.
Triángulos cerca / medio / lejos: Round01 280/208/88, Round02 430/268/106,
Round03 676/481/184, Thin01 106/72/46, Thin02 104/77/44,
PineCluster01 1220/524/134, PineCluster02 1254/584/166.
El perfil expone umbrales de tamaño en pantalla (0,10 / 0,025 / 0,001),
fracciones de agrupación de malla (0,06 / 0,14) y distancia de visión (50 km).
Los Terrain del laboratorio ignoran los límites globales de calidad para respetar sus distancias
de autoría; la configuración global del proyecto y los Terrain de la preview principal no cambian.
Los LOD son mallas, no billboards generados. Falta medir CPU/GPU y cambios de LOD en Player;
la cobertura de copas todavía no se ha cuantificado. Esta preview completa no sustituye
el streaming y los límites de visibilidad necesarios en la escena de juego.

En el laboratorio, `Tools/BronzeAge/Pruebas/Alternar bosque actual y Vegetation Spawner` permite
comparar las versiones. El generador usa los árboles nativos de Terrain, mientras que la versión actual
usa DrawMeshInstanced. La caché de comparación elimina los árboles actuales en los sectores de prueba
y conserva rocas/acantilados. No usa la caché global agregada durante la prueba, para respetar la exclusión;
por ello no comparar sus FPS globales como si fuera una integración optimizada final.
Capturas con idéntica cámara: `Assets/Screenshots/vegetation-spawner-trial.png` y
`Assets/Screenshots/vegetation-baseline.png`. Se ha revisado el aspecto y la compilación, pero todavía
no existe un benchmark CPU/GPU. La distribución Poisson resulta más uniforme que las agrupaciones actuales.

Addressables tiene un grupo local `WorldMap-Lab` y la dirección `worldmap/lab` para esta escena de ensayo.
Esto prepara una prueba de carga de escena; todavía no implementa streaming por sectores ni construye bundles.
La preview principal no está registrada en este grupo. Antes de una prueba en Player, construir el contenido
Addressables; después implementar sectores y sus dependencias para el mapa de juego.

`AgentScripts/WorldMapLabSetup.cs` se ejecuta en memoria mediante la CLI de Unity y valida todos los valores
de altura, referencias de TerrainCollider, datos independientes, referencias de cámara/raíz y recursos.
El resultado está en `Assets/Scenes/WorldMapTools/Experiments/Validation.json`.

## Prueba de suelos regionales (Yoge)

`Stylized Nature Textures` está importado en `Assets/Yoge/Textures/Stylized - Nature`.
La prueba utiliza ocho superficies con sus normales originales: hierba verde (13 variante 2), tierra seca (3 variante 3),
roca/gravas claras (9), suelo forestal (7), limo (7 variante 2), arena (4), grava (6) y pasto seco (8).
No reutiliza las texturas anteriores del terreno. No se usa la escena de demostración ni sus materiales.

Abrir `LandscapeLab`, salir de Play y usar `Tools/BronzeAge/Pruebas/Editar suelos regionales`.
El perfil `RegionalGroundProfile.asset` permite cambiar texturas, tonos por superficie, escala en metros, normales, manchas,
transiciones y aridez/arena/suelo aluvial de las 16 regiones. Luego ejecutar `Aplicar suelos regionales`.
Estos parámetros son una primera dirección visual ajustable, no una modificación del clima del servidor.
`Actualizar texturas y tonos regionales` actualiza las superficies sin repetir el cálculo de alphamaps.
Los tonos corrigen el predominio ocre de los originales; las imágenes de Yoge permanecen intactas.

`WorldMapRegionalGroundTrial` lee los siete canales canónicos y mezcla fertilidad, pasto, densidad forestal,
región y pendiente real del Terrain. Interpola parámetros regionales, nunca números de región.
El muestreo global cruza sectores y respeta la posición central de las muestras de 62,5 m.
Las manchas de 850 y 180 m y las transiciones de 250 m usan coordenadas de mundo.
Las capas de cada sector alinean la fase de las texturas; no asignan un bioma uniforme a cada cuadrado.
En las uniones se promedian las pendientes nativas de ambos vecinos (cuatro en las esquinas),
para que los pesos de roca no cambien al atravesar el borde de un Terrain.
Las alphamaps se hornean a 256×256 por Terrain. No hay generación de suelos en el runtime.

La operación rechaza escenas distintas del laboratorio o TerrainData compartidos. Comprueba que todos
los árboles y las alturas permanecen iguales, y normaliza los ocho pesos por muestra.
`RegionalGround/Validation.txt` y `RegionalGround/RegionalCoverage.csv` registran la validación y
la proporción media de superficies por región. Las capas son assets exclusivos del laboratorio.
Ocho capas implican dos pasadas de splat en el shader actual: revisar su coste en Player antes de
adoptarlas para la escena de juego. El laboratorio conserva la preview principal como comparación.
`BronzeAge/Regional Ground Lab` y su Add Pass usan deformación continua de UV en coordenadas de mundo:
reducen franjas repetidas sin nuevas lecturas de textura. El perfil expone sus escalas y amplitudes.
El camino se activa solo en los shaders del laboratorio; el shader de campaña anterior no lo activa.
Las normales acompañan las UV del suelo. La roca conserva la proyección triplanar y el relieve conserva
la iluminación facetada. La representación lejana usa la basemap existente y no deforma sus UV;
revisar la transición de distancia al medir rendimiento. No se ha realizado un benchmark en Player.
`AgentScripts/WorldMapGroundValidation.cs` verifica todas las muestras de las alphamaps guardadas,
los 82 bordes compartidos, referencias Yoge, shader, árboles y recursos.
Resultado: 3.145.728 muestras, diferencia máxima de capa entre bordes 0, 192.974 árboles y 1.354 recursos.
El error máximo de suma después de guardar es 0,011765 por cuantización de los mapas de control;
antes de guardar es 0,0000002384. Las alturas conservadas son 798.768.
Capturas revisadas: `ground-regional-mediterranean.png`, `ground-regional-delta.png` y
`ground-regional-steppe.png` en `Assets/Screenshots`.

## Orden pendiente

1. Revisar visualmente la preview y ajustar precisión, cámara y representación.
2. Implementar `WorldMapScene` con carga/descarga por zonas y detalle según zoom.
3. Integrar transiciones y limpiar el estado de cada contexto sin perder sesión/caché.
4. Conectar mapa persistido y actualizaciones del servidor por área de interés, conservando el filtrado de entidades del servidor.
5. Validar funcionamiento y rendimiento.
6. Trasladar la niebla del cliente 2D: dos máscaras autoritativas, memoria y visión compartida temporal, con actualizaciones por zonas.
7. Validar niebla, reconexiones y cambios de escena. La preview de desarrollo conserva la vista completa.




## Limpieza de experimentos anteriores

El 2026-10-03 se retiraron MountainsV5, MountainsV5_Initial y MountainsV6, junto con sus perfiles Synty exclusivos. V7 no depende de ellos y es la única prueba de montañas vigente. LandscapeLab y RegionalGround permanecen disponibles.

## Campaña regional 14 / MountainsV7

Nueva generación experimental inspirada en la composición de macizos de Three
Kingdoms, las cordilleras ramificadas de Warhammer III y los escarpes áridos de
Pharaoh Dynasties, conservando la identidad Synty y las superficies Yoge aprobadas.
La generación usa coronas a dos escalas, costillas laterales, ramales más largos y
altos, y escarpes con tres bandas de erosión de transición continua. Los puertos
conservan sus coordenadas y se vuelven a verificar junto con la hidrología.

WorldMapMountainTrial crea MountainsV7/WorldMapMountainsV7.unity; las pruebas V5 y V6 fueron eliminadas.
El perfil forestal de V7 reduce la escala visual a .68–.92, conserva el espaciado
denso de 17 m, introduce claros suaves de 480 m y reduce gradualmente el arbolado
en pendientes entre 12 y 30 grados. Estos parámetros solo afectan a la presentación;
no alteran el bosque económico ni la disponibilidad permanente de madera.
Los nuevos parámetros son neutros para los perfiles forestales anteriores.
Se reduce el uso de frentes Synty aislados (probabilidad .04, altura 90 m): el
cuerpo de la montaña lo define el Terrain, sin superponer mallas coplanares.

Fuente separada: exports-regional/campaign-massifs-v7-seed42.
La escena es una preview completa; no representa el streaming de la escena jugable
ni implica una mejora de FPS medida. La interfaz de inspección sigue siendo la
herramienta existente: esta iteración aborda paisaje y escala de vegetación.

La primera iteración V6, retirada, tenía crestas que aún parecían
laderas extruidas. V7 sustituye la mayor parte de esa cresta por coronas radiales
compuestas con contornos lobulados, mientras conserva una cresta de unión menor.
La verificación visual se debe realizar en V7; las capturas V6 no son el resultado
final. La colocación del estaño consulta ahora el componente continental medido,
además de verificar su salida local, para no reservar fuentes en islas secundarias
o repisas aisladas. No cambia las cuotas, los clusters ni su renovación económica.

Validación final V7: 48 TerrainData, 177.024 árboles nativos, 1.358 recursos,
56 fuentes de estaño y 239 de cobre, ambos metales en las 16 regiones. Los 82
bordes coinciden en alturas y alphamaps. Error de raíz forestal máximo 0,000030 m,
diferencia R16 nativa máxima 0,015281 m. Error medido de interpolación del paquete
en centros de celda: 13,755 m (la simulación sigue usando el campo continuo).
El perfil rocoso final combina 88% de color mineral y 12% de textura Yoge,
con detalle a 140 m, variación mineral de 260 m y estratificación tenue opcional.
El valor de estratificación cero conserva el aspecto de los perfiles anteriores.
Capturas finales: mountains-v7-anatolia.png, mountains-v7-massifs.png,
mountains-v7-western-crowns.png y mountains-v7-escarpments.png.


## Medición de rendimiento y siguiente relieve

La limpieza del 2026-10-03 conserva únicamente V7 como experimento de montañas.
WorldMapFpsProbe es una sonda opcional, activada por --worldmap-fps-benchmark,
con --worldmap-fps-output para elegir el directorio del informe. No se crea en
el juego normal ni se guarda un componente adicional en la escena.
AgentScripts/WorldMapFpsBenchmark.cs permite crear una build Windows de solo V7
sin cambiar las escenas de EditorBuildSettings. Cada vista se calienta 5 segundos
y se muestrea 15 segundos; registra FPS medio, 1% bajo, P95 y todos los frames.
Los ajustes temporales de VSync, targetFrameRate y runInBackground se restauran.
La prueba incluye la preview completa, no representa el streaming jugable.

La inspección del campo regional detecta que macizos, cordilleras y montañas
longitudinales comparten el mismo kernel radial de cumbre; los escarpes sí usan
un perfil distinto. La siguiente iteración debe distinguir crestas alargadas,
macizos con hombros asimétricos y cumbres secundarias, mesetas elevadas y cuencas,
y piedemontes bajos de transición. La diferenciación debe actuar en el campo
canónico y volver a validar puertos, conectividad, drenaje y acceso a recursos.
Pharaoh Dynasties guía las mesetas áridas; Warhammer III la ramificación; Three
Kingdoms la composición de macizos y bosques. Se mantienen materiales Synty/Yoge
y las llanuras aprobadas. Esta revisión no altera todavía la geometría de V7.

Resultados medidos el 2026-10-03: Standalone Player visible, Unity 6000.5.8f1,
1920 × 1080, RTX 4080 SUPER y Ryzen 7 7800X3D. Anatolia 89,94 FPS;
bosques y valles 128,56; escarpes 240,42; mundo completo 9,77.
1% bajo: 66,80 / 94,00 / 184,53 / 7,64, respectivamente.
MountainsV7/FpsBenchmark.md y FpsBenchmark.csv conservan metodología y muestras.
La ejecución oculta inicial se descarta por no renderizar; no sirve de comparación.
El mapa completo necesita una representación lejana específica; los resultados
no permiten atribuir todavía el cuello de botella a CPU o GPU por separado.

## Campaña regional-15 / MountainsV8

WorldMapMountainTrial crea ahora MountainsV8/WorldMapMountainsV8.unity; V7 se
conserva como comparación. V5/V6 siguen retiradas. La fuente es el paquete
campaign-landforms-v8-seed42 del worktree de worldgen.

El campo canónico añade mesetas orientales de techo suave, crestas longitudinales
alineadas con las sierras, macizos de hombros asimétricos y cumbres secundarias.
La longitud de cada cumbre respeta las aproximaciones a los puertos existentes.
Se mantienen las costas y llanuras aprobadas; drenaje, máscaras y recursos se
regeneran contra la geometría completa. Las nuevas vistas Mesetas orientales y
Cumbres occidentales están en Tools > BronzeAge > Pruebas > Montañas.

WorldMapForestOverviewBaker deriva una caché global de copas facetadas de los
TreeInstance reales (agrupaciones de 90 m sin cortar por límites de Terrain).
En cámaras del mapa a partir de 9000 m de altura, WorldMapForestOverview sustituye
el dibujo de árboles nativos por copas instanciadas sin sombras y eleva el error
visual de malla a 12 píxeles. Al terminar esa cámara restaura exactamente las
banderas originales y el error 2; otras cámaras siguen usando su configuración.
Al acercarse se muestran de nuevo los árboles Synty densos. No cambia posiciones,
conteos, prefabs, máscaras ni suministro permanente de madera. La caché no realiza
streaming ni descarga los datos del mundo.

La sonda de FPS usa la escena del experimento actual y verifica que la
cámara haya renderizado cada frame. Los resultados de V7 se conservan en su carpeta.


Validación final V8: 48 TerrainData de 257, 178.008 árboles nativos, 1.359 recursos,
56 fuentes de estaño y 239 de cobre, ambos metales en las 16 regiones. Los 82 bordes
coinciden en alturas y alphamaps. La caché global contiene 25.791 copas en 27 lotes.
Tras renderizar la vista global y volver a campaña, las banderas de follaje y el
error de Terrain se restauran y la validación canónica vuelve a pasar.

FPS medidos en Player visible a 1920 × 1080, RTX 4080 SUPER / Ryzen 7 7800X3D:
Anatolia 85,38; bosques y valles 122,89; escarpes 235,92; mundo completo 434,51.
1% bajo: 65,98 / 85,91 / 131,79 / 303,45. V7 medía 89,94 / 128,56 / 240,42 / 9,77.
Son dos generaciones y pasadas distintas: las vistas regionales bajan alrededor
un 2–5%, mientras la vista global mejora mucho con representación agregada.
No es un resultado de streaming ni una atribución separada de coste CPU/GPU.
FpsBenchmark.md y FpsBenchmark.csv conservan las muestras y los frames renderizados.
Capturas revisadas: mountains-v8-anatolia.png, mountains-v8-plateaus.png,
mountains-v8-western.png y mountains-v8-overview.png. V8 queda abierta en Unity.


## Iteración localizada de Anatolia / MountainsV9

La escena del experimento actual es MountainsV9/WorldMapMountainsV9.unity.
MountainsV8 y su medición se conservan como referencia. Fuente:
campaign-anatolia-v9-seed42 (regional-16 experimental, relieve regional-8).

Solo las coronas de la barrera de Anatolia incorporan disección: barrancos radiales
curvos con transición suave y dos hombros secundarios desiguales por corona. Se
aplica la misma protección de collados después de combinar crestas y ramales.
El generador no consume RNG adicional; las otras cordilleras mantienen su campo.
La hidrología, vegetación y colocación se recalculan contra el campo completo.

El perfil de roca añade variación de orientación, tono más claro en altura y
caras escarpadas más oscuras, con máscara elíptica de transición regional.
Los parámetros son editables en GroundProfile.asset; intensidad cero conserva
exactamente el comportamiento anterior. No añade texturas ni cambia los ocho
suelos Yoge, el suministro de madera, el sistema de influencia o la economía.
La caché de copas lejanas se genera a partir de los nuevos árboles reales.

Pruebas del campo y recursos: 23 tests en seis archivos, incluyendo seeds 1, 7,
42, 99 y 2026. Typecheck del servidor y scripts sin errores.


Validación Unity V9: 48 TerrainData aislados, 178.665 árboles, 1.360 recursos,
56 nodos de estaño y 240 de cobre, ambos en las 16 regiones. Los 82 bordes coinciden
exactamente en alturas y capas; error máximo de raíz forestal inferior a 0,001 m.
Caché lejana: 25.823 copas, 27 lotes. Se revisaron las capturas de Anatolia y una
vista cercana con la misma posición de cámara V8. La cámara de campaña normal
adapta su altura al terreno, por lo que su Y baja unos 47 m respecto a V8.

Límite visual observado: algunos hombros secundarios aún parecen conos aislados.
Esta prueba mejora la lectura de los flancos, pero no se considera la forma final
de todas las cordilleras; antes de extenderla conviene integrar esos hombros con
la arista principal y variar más la distribución de barrancos.


Player V9 visible a 1920 × 1080, RTX 4080 SUPER / Ryzen 7 7800X3D:
Anatolia 84,48 FPS (1% bajo 67,25); bosques 123,85 (93,42);
escarpes 221,02 (145,25); mundo completo 433,37 (293,04).
Frames renderizados verificados en las cuatro vistas; datos en FpsBenchmark.md/.csv.
V8: 85,38 / 122,89 / 235,92 / 434,51 FPS. Esta pasada baja cerca de 1% en Anatolia
 y 6% en escarpes; otras vistas permanecen cercanas. No atribuir diferencias de dos
pasadas a un único cambio ni extrapolar a servidor/streaming todavía ausentes.
Build terminada con Succeeded; contador errors=1 por timeout del comando Pipeline,
no por fallo de compilación (los dos errores de consola son límites de espera de
los comandos largos de construcción de escena y Player).


## Laboratorio de escala de campaña (2026-10-04)

Escena `Assets/Scenes/WorldMapTools/Experiments/CampaignScale/CampaignScale.unity`.
Menú Tools > BronzeAge > Pruebas > Abrir escala de campaña.
Es un experimento de representación y recorrido local sobre V9, sin cambios
al servidor, a asentamientos reales, a los recursos o a las reglas económicas.

Ciudades simbólicas de adobe/azoteas con variantes aldea, empalizada y piedra;
no representan el trazado urbano real ni son arte urbano definitivo. Caravana
Synty existente y figura de ejército representativa. Ciudad base Ø110 unidades,
multiplicador visual inicial 1,4 (Ø154); ejército 1,15. Esos multiplicadores no
cambian posiciones de juego, radios de influencia ni tiempos de desplazamiento.

La cámara acompaña al ejército (semivista320, inclinación48); Ver recorrido es
solo una vista comparativa. Viajar a B sigue una ruta precalculada local, no un
pathfinder del juego. Velocidad inicial8 u/s, recorrido1200 u, tiempo150 s.
El botón de entrada verifica proximidad18 u al acceso; todavía no enlaza una
escena de asentamiento real. Acelerar x10 comprime la espera de la prueba y
conserva el contador de tiempo de juego. Caravana circula a5 u/s.

Panel: radios90/180/270/360/450/540, zoom, tamaños de ciudad/ejército, velocidad,
comparación bosque nativo/masa representativa. El bosque de prueba agrupa árboles
en celdas50m, amplía copas x2,2 y altura x0,65, con claros para símbolos urbanos
y el recorrido. Conserva alturas, especies, máscaras y fuente económica. Solo
los TerrainData forestales del área comparada tienen copia de presentación;
la V9 y sus178665 árboles permanecen intactos. No es streaming ni niebla.

ValidateCampaignScale verifica distancias, recorrido a mitad/completo, entrada
permitida a17 y rechazada a19 u, aceleración temporal, alturas iguales en copias
forestales y árboles apoyados en Terrain. Calibration.txt y Validation.txt
registran la calibración de la escena que se ha construido.


Ubicación revisada: A=(17452,366;6550,536), B=(18652,366;6550,536).
Se escogió un enclave fértil/boscoso para comparar la escala cerca de la cordillera.
La ruta tiene pendiente máxima medida1,642 grados. Las cuatro copias forestales
pasan de26808 a16156 árboles de representación; los árboles originales de V9 se
conservan. Claros urbanos125u y corredor visual55u de semiancho. La escala de copas
se bajó después de inspeccionar la primera captura para no ocultar los símbolos.


### Prueba regional de escala (12 ciudades / 5 reinos)

Escena independiente: `Assets/Scenes/WorldMapTools/Experiments/CampaignRegion/CampaignRegion.unity`.
Play muestra selección de ciudades, niveles inicial/N1–N5, influencias, nodos reales y comparación de tiempos directos a **12 u/s**, sin obstáculos ni navegación. La vista habitual sigue siendo una ciudad y su entorno; «Comparar región» es únicamente una herramienta de balance.

Se conservan miniaturas a escala 1.4 (diámetro visual 154 u), ejército a 1.8 en la prueba local y radios 90/180/270/360/450/540. La Cuenca fértil contiene 12 posiciones representativas de arranque con piedra existente y un punto de bosque ≥15 % dentro de 90 u, repartidas en cinco reinos de al menos dos ciudades. Centros separados ≥1080 u: ningún nodo se comparte incluso a N5. Las zonas se calculan con `computeZonaInfluencia` del motor; este todavía permite solapes entre ciudades de la misma facción, evitados mediante separación en esta muestra, sin cambiar la regla.

No se añade ningún nodo, ni se crean asentamientos del servidor. Las copas son proxies visuales (celdas de 50 u, anchura ×2.2 y altura ×0.65), derivados de los bosques originales en seis tiles. Las alturas del Terrain permanecen idénticas. El despeje visual de las miniaturas no altera la máscara forestal canónica ni su acceso económico.

Los testigos de madera y piedra prueban ubicación, no espacio para productores, mantenimiento, población, economía completa o capacidad máxima. El acceso a metales se cuenta por influencia, incluyendo nodos de áreas vecinas cuando caen dentro. Naranja=cobre; azul=estaño; gris=piedra; blanco=ganadería. Esta prueba no usa fundación automática ni determina el trazado de asentamientos real.

Análisis reproducible: `scripts/campaign-region-trial.ts` en el worktree de worldgen, a partir del `world.json` V9 seed 42 ya exportado. Datos: `campaign-region-trial.json`. Validación Unity: `AgentScripts/ValidateCampaignRegion.cs`. La escena `CampaignScale` conserva el recorrido local de 1200 u, ahora **100 segundos** a 12 u/s; `ValidateCampaignScale.cs` comprueba la duración, proximidad de entrada y aceleración ×10.


### Diagnóstico visual de atractivos regionales

Escena independiente: `Assets/Scenes/WorldMapTools/Experiments/StrategicTrials/StrategicTrials.unity`. Cinco bancos: Península de los golfos, Estepa abierta, Cuenca fértil, Tierras altas secas y Corredor levantino. Cada uno contiene cuatro ciudades de presentación en dos reinos; no funda entidades del servidor. La escena de escala anterior se conserva.

En Play: selector de región; entorno de ciudad o comparación regional; Inicio/N1–N5; recursos; focos; corredores. «Centrar foco» muestra la fuente o territorio productivo marcado. Rojo y azul identifican reinos; focos azules=estaño, naranja=cobre, verde=territorio, magenta=paso. Los corredores dorados proceden del diagnóstico de navegación entre anclas, recortados a la región. Sus costes ponderados NO son segundos, ni carreteras construidas. Los tiempos directos se calculan aparte a 12 u/s, sin obstáculos. Ríos y control militar siguen pendientes de mecánicas específicas.

Los polígonos de influencia se exportan desde `computeZonaInfluencia` en los seis niveles. Las dos ciudades aliadas se separan al menos 1080 unidades para evitar solapes permitidos entre aliados por el motor actual; las rivales pueden encontrarse y recortar fronteras. Varias fuentes de un mismo tipo por ciudad son válidas. Perder una fuente con crecimiento rival no invalida el arranque.

Los 48 Terrain usan copias de bosque con copas representativas (celdas 50 u, anchura ×2.2, altura ×0.65), alturas idénticas al V9. El despeje de miniaturas es visual, no modifica el campo forestal económico. Los datos proceden de la semilla 42; el exportador solo genera escenarios y líneas de diagnóstico, sin editar geografía o recursos: `scripts/exportar-pruebas-estrategicas.ts` en el worktree de worldgen. Construcción y validación: `AgentScripts/BuildStrategicTrials.cs` y `ValidateStrategicTrials.cs`.


### Campaña con carga por sectores

Escena: `Assets/Scenes/GameScenes/WorldMapCampaign.unity`. Ver `Docs/WorldMapCampaignPlan.md` para controles, resultados y límites de la prueba local. La preview global y los experimentos de balance se conservan.
