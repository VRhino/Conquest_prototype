# Selección Synty para el mapa de campaña

Inventario local revisado el 2026-10-02. La selección inicial de árboles, rocas, acantilados y suelo ya está integrada en la preview mediante `WorldMapSyntyBaker`.

## Paquetes encontrados

| Paquete | Prefabs | FBX | PNG | Papel en la campaña |
|---|---:|---:|---:|---|
| PolygonFantasyKingdom | 2.187 | 2.269 | 75 | Entorno, vegetación, edificios, utilería y suelo |
| PolygonFantasyHeroCharacters | 900 | 777 | 35 | Héroes y representaciones de ejércitos |
| AnimationBaseLocomotion | 0 | 347 | 0 | Locomoción de personajes; no aporta geografía |

`Assets/Synty/Tools` es una carpeta adicional de herramientas. Los conteos incluyen variantes y piezas del paquete; no equivalen a modelos únicos aptos para el mapa.

## Candidatos principales de Fantasy Kingdom

Rutas relativas a `Assets/Synty/PolygonFantasyKingdom/Prefabs/`.

| Uso | Modelos o familias | Tratamiento propuesto |
|---|---|---|
| Bosque mediterráneo | `Environments/SM_Env_Tree_Round_01…04`, `SM_Env_Tree_Thin_01…06` | Variantes de copa y silueta para bosque abierto y ribera; escala de campaña parametrizada |
| Bosque de montaña | `Environments/SM_Env_Tree_Pine_Cluster_01`, `02`, `04`, `05` | Grupos de coníferas según tipo forestal, pendientes y altura |
| Vegetación estratégica lejana | `Generic/SM_Generic_Tree_01…04` | Candidatos para menor detalle; medir geometría antes de asignar LOD |
| Matorral | `Environments/SM_Env_Bush_*` | 23 variantes en esa familia; reservar detalle para zonas próximas |
| Laderas y acantilados | `Environments/SM_Env_Rock_Cliff_01…05` | Dar caras rocosas a pendientes y costas, sin alterar corredores ni colisión autoritativa |
| Rocas y nodos de piedra | `Environments/SM_Env_Rock_01…04`, `SM_Env_Rock_Chunk_01…03` | Agrupaciones visuales y marcadores de extracción con los IDs actuales |
| Montañas de vista general | `Generic/SM_Generic_Mountains_Grass_01…02` | Evaluar como símbolos lejanos; el relieve jugable sigue representado por Terrain |
| Cultivos | `Environments/SM_Env_Ground_Farm_Flat_01`, `SM_Env_Ground_Farm_Row_01…05` | Usar cuando haya datos de cultivos o explotación; no deducir asentamientos de la fertilidad |
| Cruces | `Environments/SM_Env_Bridge_Stone_01`, `SM_Env_Bridge_Stone_Path_01` | Asociar a cruces válidos y ajustar sus apoyos; el modelo por sí solo no concede transitabilidad |
| Agua | `Environments/SM_Env_Water_Plane_01`, `SM_Env_Water_Slope_01`, `Generic/SM_Generic_Water_Plane_01…02` | Referencias visuales; conservar las mallas geográficas del mar, ríos y lagos |
| Iconos futuros de plazas | `Buildings/Preset_Houses/SM_Bld_Preset_Hut_01…02_Optimized` y casas optimizadas | Selección histórica pendiente; muchos edificios del paquete son medievales/fantásticos |
| Caravanas y embarcaciones | `Vehicles/SM_Veh_Cart_01…04`, `SM_Veh_Boat_Dinghy_01` | Representación de entidades reales de la campaña |

## Texturas y materiales

En `Textures/Ground` hay 15 PNG: hierba (tres familias más versión oscura), barro (tres), arena (tres) y cinco mapas de normales. Reutilizar las texturas de suelo sobre Terrain con una paleta mediterránea; barro para ribera y llanura húmeda, arena para costa/zona seca, hierba para tierra fértil y bosque. La roca seguirá necesitando un material de terreno específico.

Las texturas `PolygonFantasyKingdom_Texture_01_A…04_A` son atlas/paletas de los modelos: mantener sus UV y materiales; no tratarlas como texturas de suelo repetibles.

Los árboles redondeados, el grupo de pinos, el acantilado y el árbol genérico inspeccionados comparten `Materials/PolygonFantasyKingdom_Mat_01_A.mat`. Se verificó su shader URP Lit y su instancing activado. Esto facilita agrupar el render, pero no demuestra por sí solo el rendimiento del conjunto. Los materiales de agua inspeccionados también apuntan a URP Lit; no constituyen por sí solos un sistema de agua animada con orillas.

Los prefabs de árbol/acantilado inspeccionados incluyen MeshCollider, algunos múltiples. El dibujado de campaña usa sus mallas y materiales sin instanciar sus colliders. Los límites medidos varían mucho: árboles redondeados de 6–8 m, grupos de pinos de 23–25 m y rocas planas de apenas 0,27–0,29 m de altura. Por eso la escala se normaliza por altura para árboles/acantilados y por dimensión mayor para rocas. El detalle lejano usa una selección explícita de árboles genéricos, no LOD automáticos certificados del paquete.

## Orden de aplicación

1. Sustituir las copas procedurales por una selección pequeña de árboles Synty, preservando densidad, tipo forestal y distribución del generador.
2. Añadir rocas/acantilados a zonas con pendiente y costa; ajustar variedad y presencia según región.
3. Completar suelo con barro y variantes de hierba/arena y normales. Mantener materiales y atlas de los modelos.
4. Preparar representaciones de nodos y, cuando exista su estado real, cruces y entidades de campaña.
5. Medir triángulos, llamadas de dibujo y memoria con los modelos elegidos; fijar escalas y niveles de detalle para carga por zonas.

Selección aplicada: Round 01–03, Thin 01–02, Pine Cluster 01–02, Generic Tree 01–02, Rock 01–02/Chunk 01 y Cliff 01–02. El suelo incorpora barro en ribera. Cultivos, edificios, cruces, ejércitos y caravanas quedan pendientes de sus datos de juego. Los controles y el procedimiento de reconstrucción están en `Assets/WorldMap/README.md`.
