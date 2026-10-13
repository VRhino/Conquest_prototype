# Worldgen y mapa de campaña: estado, divergencias y calibración

Fecha de revisión: **12 de octubre de 2026**.

Este documento describe lo construido durante la sesión, su diferencia con el worldgen de BronzeAgeFase0 y las consecuencias de `Calibracion_Distancias_Definicion.md`. Es una revisión documental: no modifica generación, escenas, radios, velocidades, recursos ni partidas.

> **Actualización posterior, 12 de octubre:** el usuario ha deprecado la prueba previa de Unity y dado prioridad a los acuerdos del documento de Claude, que ahora fija **factor 12** y un mundo estratégico de **2.666,67 × 2.000** sobre los 32.000 × 24.000 de Unity. Las referencias anteriores a radios/velocidad aceptados, capacidad objetivo y conversiones hipotéticas describen la historia de la sesión; no son requisitos vigentes. Los apartados 7–9 analizan la revisión anterior de la calibración. Consultar el apartado 12 para el contrato actualizado. La geografía y las cifras de contenido siguen describiendo el prototipo existente hasta que se aplique el nuevo balance.

## 1. Qué tenemos realmente

Tenemos un **prototipo regional de geografía estratégica y su representación en Unity**, separado de la generación de partidas que sigue usando BronzeAgeFase0. La superficie visual ya permite trabajar con regiones, montañas, bosques, agua, recursos y escala de campaña. La integración económica y de servidor no está terminada.

Conviene distinguir cuatro estados que tienen nombres parecidos:

| Estado | Ubicación y función | Situación |
|---|---|---|
| Worldgen original al iniciar la sesión | BronzeAgeFase0, entonces v15 | Mundo por ruido, perfiles geográficos individuales, mapa por defecto de 2.000 × 2.000 |
| Worldgen actual de la línea principal | BronzeAgeFase0 y worktree de Claude, v17 | Conserva ese pipeline; añade hierro y estaño extra respecto a v15 |
| Primera mejora experimental | Worktree `worldgen-relieve`, worldgen numérico v16 | Estructura, pendiente, conectividad, drenaje y subsemillas; no equivale a la v16 posterior de la línea principal |
| Mundo regional y campaña Unity | Generador regional independiente y Conquest_prototype | Mundo rectangular de 32.000 × 24.000, 16 áreas, exportación e importación por sectores; manifiesto actual `regional-17-costas-1 (experimental)` |

**Los números de versión no establecen compatibilidad entre estas líneas.** La v16 experimental y la v16 que añadió hierro son cambios distintos. Tampoco el nombre `V9` de un snapshot visual ni `regional-17` equivalen a una partida v17 de BronzeAge. Para integrar habrá que identificar algoritmo, formato, configuración y contenido mediante contratos inequívocos.

El manifiesto actual de Unity conserva el identificador local `campaign-anatolia-v9-seed42`, pero apunta al paquete posterior `campaign-coasts-v1-seed42`. Esa denominación histórica no debe usarse como prueba de que los datos siguen siendo los de la primera exportación V9.

### Orientación de diseño previa y contexto histórico

- Mundo inspirado en Grecia, Egeo, Anatolia, Levante, Mesopotamia y Egipto, desde el final de la Edad del Bronce hasta la época de Alejandro. Es una composición evocadora, no una reproducción cartográfica exacta.
- Regiones de tamaños y formas diferentes, conectadas por tierra; islas secundarias por ahora.
- Montañas, pasos, cuencas y corredores con utilidad territorial. Llanuras amplias que permitan fundar y disputar asentamientos.
- Identidad visual low poly compatible con Synty y lectura de campaña similar a Total War/Conqueror’s Blade.
- Ciudades como miniaturas que indiquen nivel y murallas; ejércitos y caravanas como símbolos 3D. Los modelos no representan literalmente toda su superficie ni cada soldado.
- Bosques como masas de cobertura, con interiores casi cerrados, claros y márgenes irregulares. Madera permanente, sin simulación de tala y recuperación.
- Objetivo de al menos 500 jugadores y aproximadamente 100 o más asentamientos. La distribución debe admitir conflicto dentro de cada región, incluyendo al menos dos reinos de dos asentamientos en las regiones más limitadas.
- No exigir autosuficiencia completa, idéntica riqueza ni conservación de todas las fuentes al crecer. Varios nodos del mismo tipo dentro de una influencia son válidos.

## 2. Cómo funciona el worldgen original

La entrada es `generarMapa(config)`, con semilla, dimensiones y perfil geográfico opcional. Devuelve datos de mundo; el motor los consulta mediante la fachada `Mapa`.

El pipeline de la línea principal revisada es:

```text
semilla y configuración
  → campo de elevación y guía regional opcional
  → ríos
  → campo de fertilidad
  → bosques
  → piedra, cobre, estaño, oro y fauna
  → hierro
  → estaño adicional
  → MapaGenerado
```

### Relieve y regiones

La altura procede de un campo continuo de ruido fractal. El suavizado reduce el detalle fino de las bandas jugables; el terraceo de versiones anteriores se retiró. Los perfiles individuales aportan guías de silueta, crestas y ríos troncales. El mundo libre tiene un tratamiento de bordes.

No es un tablero de casillas. Las rejillas auxiliares de navegación o dibujo no sustituyen por sí mismas la consulta continua de terreno.

En el original, la clasificación por altura pesa mucho en el coste de movimiento. El mar está bloqueado; una cima sigue siendo cara pero transitable. En la línea principal actual ya existen además costes de bosque y cruces fluviales mediante vados/ciudades en el grafo: no son novedades exclusivas de nuestro prototipo.

### Ríos, fertilidad y biomas

Los ríos procedurales originales siguen el gradiente y los troncales siguen guías geográficas con meandros. El terreno, el campo de fertilidad y la cercanía a ríos intervienen en el bioma. La generación regional posterior añade una red de drenaje más explícita y lagos con contornos y cotas.

### Bosques y explotación

Los bosques originales son discos con ID, centro, radio y densidad, cuya densidad combina fertilidad y azar. Pueden solaparse. El render puede fusionar sus siluetas, aunque la explotación sigue resolviendo bosque a bosque.

Esto importa: en el motor existe capacidad de leñeras por bosque. Una cobertura bonita o una muestra con densidad suficiente no demuestra que haya una leñera disponible para otra ciudad.

### Recursos

Se generan cantidades por tipo/rareza, condicionadas por bioma, separación y exclusión de bosques. El fallback original puede sacrificar separación y, si ni siquiera encuentra hábitat, colocar un punto sin cumplir el filtro. Nuestra revisión experimental endureció esa validez.

La madera no es un nodo mineral; procede del bosque. La agricultura depende del suelo y de los productores. Los nodos minerales/fauna tienen stock y reposición gestionados por el motor, separados de la generación inicial. No hay que reinventar esa reposición en Unity.

### Determinismo y guardados

El pipeline principal consume una secuencia aleatoria compartida. Añadir llamadas antes de una etapa puede desplazar todo lo posterior. Los parámetros de generación están congelados y los cambios de mundo requieren revisar la versión. La carga por semilla no permite cambiar el algoritmo y asumir que la partida sigue significando lo mismo.

## 3. Qué construimos diferente

### 3.1 Primera fase: relieve estratégico

La primera implementación experimental incorporó estructura geográfica serializable, cordilleras, estribaciones, pasos, perfiles de conectividad y una reducción del microrelieve en llanuras. Separó el azar por etapas y comprobó recursos y navegación con restricciones de terreno.

Añadió drenaje D8, acumulación, lagos con cuenca y finales de río; aplicó pendiente a transitabilidad y validó segmentos de rutas. Los informes de esa fase registran campañas de 600 mapas y paridad con el cliente, pero **no certifican automáticamente el generador regional posterior ni la economía actual**.

Esta fase sigue en el worktree aislado. No se ha sustituido con ella el worldgen de la rama principal.

### 3.2 Un único mundo regional

El recorrido pasó de perfiles individuales y un mundo experimental de 12 km a una composición mediterránea rectangular de **32.000 × 24.000 unidades**. Los mapas individuales dejaron de ser la prioridad del visor.

El generador `generarRegionalVisor` es una entrada distinta de `generarMapa`. Su flujo actual es:

```text
semilla + dimensiones + densidad de recursos
  → composición continental, áreas y barreras
  → campo continuo de relieve
  → valles troncales, drenaje, ríos y lagos
  → revisión de conexión y reducción acotada de lagos si hace falta
  → campo costero y nueva comprobación de conexión
  → campos de bosque y suelo
  → recursos generales, cobre disperso y estaño por concentraciones
  → preparación geográfica de productores
  → diagnóstico de acceso y filtrado de fuentes aisladas
  → comprobación de presencia regional de cobre y estaño
  → datos del visor y paquete Unity
```

Las 16 áreas tienen centros y extensiones variables. Se combinan mediante influencias espaciales deformadas; sus límites no levantan automáticamente muros. La seed cambia centros, extensiones, alturas, costas, crestas y pasos **dentro de una composición base diseñada**. Por tanto, no genera cualquier continente imaginable: conserva la intención mediterránea.

Las barreras usan curvas y morfologías distintas: macizos, cordilleras, formas longitudinales, escarpes y mesetas. Se añadieron ramales, cumbres y disección para abandonar los valles cuadrados y las crestas uniformes. Los pasos reducen el relieve de forma gradual; no deberían ser cortes verticales aislados.

### 3.3 Hidrografía y costa

Los grandes valles y ríos se integran con cotas descendentes. Los afluentes y confluencias forman una red dirigida con orden fluvial. El suavizado comprueba que los tramos sigan descendiendo. Los lagos buscan una orilla compatible con la cota del agua y evitan bloquear pasos previstos.

Existen límites: no es una simulación completa de erosión o clima, y los lagos no resuelven todavía todos los casos de emisario/desbordamiento. Dibujar una desembocadura o un cruce no concede automáticamente una mecánica de puerto, puente o control militar.

Las costas dejaron de ser solo una transición suave al mar. `costas-1` genera plataformas y frentes de roca/acantilado a partir de la semilla y el campo de altura, con variación y protección de desembocaduras. Se aplica **antes** de bosques, suelos y recursos de una generación nueva. Delta y marisma conservan un tratamiento suave.

En el paquete de comparación importado se preservaron IDs y posiciones horizontales de recursos; una generación completa nueva vuelve a colocarlos después del relieve costero. Son procesos diferentes.

### 3.4 Bosque continuo y suelo regional

El bosque regional es un campo serializable de densidad, no una colección exclusiva de discos. Combina humedad, masas de ruido amplio, detalle, altura, pendiente y proximidad a ríos. Distingue bosque abierto, denso, de ribera y montano; incorpora claros y transiciones.

Los suelos combinan humedad, pendiente, altura, ruido y agua fluvial/lacustre. Producen fertilidad, pastos y clasificación de suelo: húmedo, seco, aluvial, estepa o pedregoso. La geología condiciona la prospección mineral, sin obligar a que todas las áreas sean iguales.

**La integración económica del bosque continuo con IDs, capacidad y asignación de leñeras originales está pendiente.** No basta con sustituir el círculo dibujado: hay que definir qué fuente controla cada productor.

### 3.5 Recursos, influencia y disputas

Se ensayaron densidades generales ×1/×2/×4/×8; ×4 es la configuración del paquete actual. El cobre se distribuye por áreas y distancia entre fuentes, con geología como ponderación, evitando que solo aparezca en unas pocas manchas.

El estaño se reemplazó por fuentes ordinarias: dos aisladas por área y cuatro concentraciones de seis fuentes. Las concentraciones actuales son amplias, con separaciones pensadas para varios asentamientos. **No son los distritos compactos de la nueva calibración.**

Se preparan enclaves de estaño buscando terreno suave, bosque existente y una cantera próxima; si falta una cantera, el algoritmo puede añadirla. Es una etapa determinista de generación, no una recolocación manual posterior del mapa. No demuestra por sí misma que el asentamiento tenga economía viable.

Los análisis reutilizan los polígonos de influencia del motor. Un nodo dentro del alcance de varias ubicaciones potenciales es un objetivo de disputa; no significa que dos ciudades puedan explotarlo simultáneamente.

El diseño asigna atractivo principal y secundario por región: bosque, agricultura, ganadería, minería, conexión o defensa. Los diagnósticos buscan evidencia geográfica. Defensa, interdicción, peajes y tráfico real no quedan certificados por la forma del mapa.

## 4. Comparación directa

| Aspecto | Original / línea principal | Mundo regional de esta sesión |
|---|---|---|
| Extensión por defecto | 2.000 × 2.000 | 32.000 × 24.000 en el paquete actual |
| Organización | Mundo libre o perfil geográfico individual | Una composición de 16 áreas de distintos tamaños |
| Base del relieve | Ruido fractal suavizado y guías por perfil | Áreas, barreras, macizos, pasos y valles más ruido condicionado |
| Silueta | Depende del perfil o borde del mundo libre | Composición mediterránea con costas y golfos variables |
| Azar | Secuencia compartida | Subsemillas por subsistema; siguen existiendo dependencias entre etapas |
| Pendiente | Coste por terreno/altura; cima cara pero transitable | Bloqueo por pendiente en el experimento, además del agua |
| Hidrografía | Descenso por gradiente y troncales guiados | Drenaje, cuencas, confluencias y lagos con contorno/cota |
| Bosque | Discos con ID y capacidad de leñeras | Campo continuo para geografía; explotación aún sin adaptar al motor |
| Suelo | Fertilidad y bioma consultados bajo demanda | Campos regionales de fertilidad, pastos y geología |
| Cobre | Cuota y predicado de bioma | Cuota por áreas y dispersión dentro de cada una |
| Estaño | 24 originales; 64 en v17 con el extra | 56 en el paquete actual, aislados y concentraciones |
| Hierro | 120 nodos en la v17 revisada | No forma parte del catálogo regional exportado actual |
| Influencia | Inicio 30; máximos 60/90/120/150/180 | Propuesta de ensayo 90; máximos 180/270/360/450/540 |
| Persistencia | Identidad de partida y regeneración versionada | Paquete visual con campos canónicos, archivos y checksums |
| Representación | Cliente 2D existente | Terrain, mallas, materiales, copas y símbolos 3D en Unity |
| Estado dinámico | Economía, propiedad, stock, movimiento y visión del motor | Prueba local; adaptador de lectura preparado, backend postergado |
| Niebla | Implementada en motor y cliente 2D | Investigación hecha; adaptación visual y transporte a campaña pendientes |

La intención de autoridad se mantiene: BronzeAge debe decidir lo que afecta reglas y Unity representarlo. Nuestra campaña local permite prototipar, pero todavía no satisface todo el contrato de una partida real.

## 5. Radiografía del paquete que usa Unity

Lectura del manifiesto y `world.json` actuales; son cifras de **seed 42**, no garantías para cualquier semilla.

| Dato | Valor |
|---|---:|
| Dimensiones | 32.000 × 24.000 |
| Superficie rectangular total, incluido mar | 768 millones de unidades² |
| Áreas geográficas | 16 |
| Sectores de terreno | 48, de 4.000 × 4.000 |
| Nivel del mar en Unity | 375 |
| Ríos/tramos publicados de la red | 59 |
| Lagos publicados | 13 |
| Pasos registrados | 12 |
| Piedra | 616 |
| Cobre | 240 |
| Estaño | 56 |
| Oro | 48 |
| Ganadería | 400 |
| Total de nodos | **1.360** |
| Estaño aislado / en concentraciones | 32 / 24 |
| Enclaves de estaño con testigo geográfico preparado | 56 de 56 |
| Copas visuales conservadas en campaña | 67.497 |
| Frentes rocosos/acantilados exportados | 304 |

Cada área tiene dos fuentes aisladas de estaño; cuatro áreas de este paquete tienen además seis de concentración. No hay nodos de madera en ese total. Las copas tampoco son 67.497 fuentes económicas independientes.

### Cómo aumentaron los recursos respecto al mundo pequeño

El rectangular actual tiene **192 veces** el área total de un cuadrado 2.000 × 2.000, no 292. El cociente de tierra útil será distinto y exige medir la máscara terrestre.

La referencia histórica v15 tenía 468 nodos: 240 piedra, 80 cobre, 24 estaño, 24 oro y 100 fauna. La v17 principal revisada tiene 628: añade 120 de hierro y 40 de estaño. El regional actual tiene 1.360 y carece de hierro.

Por tanto, 1.360 es aproximadamente 2,91 veces 468, o 2,17 veces 628. **No se conservó la densidad media por superficie bruta.** Frente a esos dos puntos de comparación, es aproximadamente 66 o 89 veces menor. Es una comparación de escala, no un diagnóstico automático de mala economía: hay mar, geografía especializada y concentración deliberada.

Lo que hay que medir es cuántas fundaciones tienen productores accesibles dentro de su polígono, y qué oferta real dan sus fuentes. Multiplicar todos los nodos por el cociente de áreas no garantiza un mundo interesante.

### Qué prueban las auditorías

La auditoría revisada del snapshot V9 y las semillas 7/99 demostró 64 posiciones iniciales: cuatro por área, con madera y piedra y separación inicial suficiente. Es evidencia de un mínimo geográfico para dos reinos de dos ciudades; **no es la capacidad máxima ni una simulación de 500 jugadores**.

La prueba antigua que exigía 1.080 de separación y círculos completos de N5 era demasiado restrictiva como criterio de fundación. Su propuesta de mover una piedra no se aplicó ni fue necesaria para el mínimo revisado. Crecer con fronteras recortadas y disputar fuentes es válido.

Las auditorías de V9 son anteriores a las costas actuales. No trasladar sus conclusiones al nuevo paquete sin volver a comprobar los emplazamientos afectados. Asimismo, tener varios nodos del mismo tipo dentro de una ciudad nunca fue un error de diseño.

## 6. Qué está construido en Unity y qué falta

### Representación y carga

Hay una preview global para autoría y una escena de campaña local con carga de sectores por Addressables. La campaña usa hasta nueve sectores detallados cercanos, con cargas transitorias, y 48 terrenos lejanos simplificados sin colliders ni bosque económico. La escena evita referenciar directamente todos los terrenos detallados.

Los materiales de suelo aceptados se basan en las texturas incorporadas de Stylized Nature Textures/Yoge y en el reparto regional. Su distribución se restauró; también se conservaron los materiales de montaña V9. No estamos usando sin más la primera pintura uniforme ni únicamente los suelos del paquete Synty.

Las masas forestales normalizan el diámetro de la copa por modelo. Los interiores alcanzan diámetros de hasta 98 unidades; los márgenes, alrededor de 34; hay variación continua y protección de orillas. Se conserva el campo económico y la cantidad de instancias visuales. El solapamiento añadido puede costar rendimiento aunque el conteo no cambie.

Las costas se representan con Terrain y frentes de malla derivados de puntos canónicos. La última validación registrada conserva 82 uniones de altura exactas entre bloques. El error de muestreo en centros de celda del paquete actual alcanza aproximadamente 29,15 unidades verticales: es una medición concreta, no una cota global. Terrain no debe sustituir la autoridad económica o de rutas del motor por esa aproximación visual.

El acuerdo BA-003 planteaba enviar semilla/configuración para microdetalle cosmético. Nuestro prototipo **también exporta alturas, capas y geometría canónica** como contenido estático de autoría, para evitar duplicar en C# el generador estratégico. Es una divergencia de transporte/producción de contenido que debe formalizarse antes de integrar. No supone enviar todo el mapa en cada consulta: los archivos se identifican por versión/hash, se empaquetan y se cargan por sectores. La distribución y caché en una sesión real todavía están pendientes.

### Herramientas y pruebas realizadas

| Herramienta o recurso | Evidencia y papel actual |
|---|---|
| Synty Fantasy Kingdom | Modelos, atlas y materiales para vegetación, roca y entorno; las fuentes originales se conservan |
| Stylized Nature Textures / Yoge | Ocho superficies de suelo con normales, mezcla por campos regionales y variación en coordenadas globales |
| Terrain Tools 5.3.3 | Instalado en el proyecto; disponible para autoría/pruebas, no reemplaza el generador regional |
| Addressables 4.1.0 | Instalado y usado para contenido de sectores de campaña |
| Vegetation Spawner 1.1.0 | Importado y usado en el laboratorio de paisaje para prototipos nativos de Terrain |
| Muestreo forestal propio | Radio variable, ruido global de bordes y exclusión entre especies/sectores; complementa el paquete anterior |
| LOD forestales propios | Mallas simplificadas horneadas sobre copias del laboratorio, conservando la paleta UV Synty |

La prueba de Vegetation Spawner llegó a 192.974 instancias en 48 sectores del laboratorio, con 152.875 en densidades visuales altas. Su menú personalizado usa las máscaras regionales; el botón de respawn nativo no sustituye esas reglas. **Ese conteo no es el de la campaña actual**, que adoptó masas de copas y conserva 67.497 símbolos. Son ensayos distintos.

Se investigaron otras opciones como MicroSplat, MicroVerse, GPU Instancer Pro y Stylized Water 3. La documentación local las deja como posibles integraciones posteriores; no deben figurar como herramientas aplicadas a esta campaña.

### Iteraciones de relieve y rendimiento registrado

V7, V8 y V9 fueron experimentos sucesivos de montaña; la documentación registra la retirada de las escenas de prueba V5/V6. Que el worktree conserve paquetes exportados antiguos no significa que esas escenas estén activas en el juego.

V8 diferenció morfologías y añadió representación lejana. V9 concentró el trabajo en Anatolia, con barrancos, hombros secundarios y variación de roca. Algunos hombros seguían leyéndose como conos: esa limitación precede a la prueba posterior de modelos Synty.

El benchmark guardado de **V9, 3 de octubre**, midió una preview completa en Player visible a 1.920 × 1.080, RTX 4080 SUPER / Ryzen 7 7800X3D, con cinco segundos de calentamiento y quince por vista:

| Vista | FPS medio | 1 % bajo |
|---|---:|---:|
| Anatolia | 84,48 | 67,25 |
| Bosques y valles | 123,85 | 93,42 |
| Escarpes | 221,02 | 145,25 |
| Mundo completo | 433,37 | 293,04 |

La vista global usa representación distante; no compara la misma carga que una cámara cercana. Esta medición no incluye carga inicial, red, streaming ni niebla, ni mide CPU/GPU por separado. **No es un benchmark de la campaña actual con nuevas masas, costas y modelos.** Queda como referencia histórica que hay que repetir sobre el contenido definitivo.

### Escenas e interacción

- `GameScenes/WorldMapCampaign`: campaña destinada al juego, actualmente local.
- `GameScenes/SettlementScene`: entrada directa a la vista 3D del asentamiento.
- `WorldMapTools/SettlementPreview`: herramienta de inspección del asentamiento.
- `WorldMapTools/WorldMapPreview`: preview global.
- `WorldMapTools/Experiments`: laboratorios de escala, región y representación.
- `GameScenes/BattleScene`: escena de batalla ya existente.

La campaña permite seleccionar una ciudad local, acercarse, entrar y regresar junto a ella, conservando contexto. Esas ciudades son fixtures de presentación; las transiciones no ejecutan las reglas reales de entrada/salida del servidor. El backend y las interacciones quedaron postergados por decisión del usuario.

### Última prueba de montañas: no aceptada

Se hornearon 24 modelos de montaña Synty, cinco nevados, y 18 rocas costeras en dos áreas de prueba, con 49.665 triángulos y 39 grupos de dibujo según el informe de construcción. Las posiciones usan semilla y comprobaciones de huella.

También se implementó una adaptación de las bases a Terrain, pero su revisión visual final quedó incompleta. El usuario señaló que las piezas se leen como protuberancias sobre las caras. **Esta entrega no debe presentarse como solución final aceptada.**

La dirección propuesta después es usar Synty como base de autoría para crear caras, laderas y crestas integradas, posiblemente mediante Blender. No se confirmó conexión con Blender, ni se construyeron esas piezas. Deben generarse/adaptarse al relieve por semilla, sin tapar pasos ni alterar accesos mediante decoración arbitraria.

### Niebla y red

La investigación distingue terreno desconocido, explorado sin visión actual y visible. Las memorias de ciudades no deben actualizarse con información viva; los ejércitos que salen de visión desaparecen. La máscara visual se añade al final del plan funcional, pero el filtrado del servidor debe existir desde la conexión real.

Está preparado un adaptador opcional de lectura de proyección BronzeAge, con validaciones de identidad, posición y hash. No hay una sesión real configurada ni integración completa de comandos, recursos, ciudades y batalla. Las rutas locales horneadas son un mecanismo del prototipo, no paridad demostrada con el grafo de BronzeAge.

## 7. Qué dice la nueva calibración

Fuente completa leída en el worktree de Claude: `Consideraciones/Calibracion_Distancias_Definicion.md`, estado **en diseño, cero código y canon sin tocar**. La inspección de sus constantes confirma que todavía están los radios y escala antiguos.

La propuesta diagnostica falta de densidad de actividad cerca del jugador. Mantiene el mapa 2.000 × 2.000 y un reloj de 60 segundos por tick económico, y cambia la distribución de ciudades, territorios y recursos.

| Parámetro | Calibración propuesta |
|---|---|
| Tropa ligera | 30 unidades por minuto, equivalente a 0,5 unidades/s |
| Separación mínima entre plazas | 30 unidades; un minuto directo entre centros a esa velocidad |
| Ancho visible habitual | 15 unidades; cruce de pantalla en unos 30 segundos |
| Influencia inicial | 6 |
| Máximos N1–N5 | 10 / 15 / 20 / 25 / 30 |
| Ciudad representada | Aproximadamente 0,6–1,0 unidades de mapa |
| Campamentos | Separación de 600; 8–10 en las tres semillas ensayadas del mapa pequeño |
| Foco de actividad | Radio aproximado de 150 |
| Frontera | Distancia 150–250 al campamento más próximo |
| Tierra lejana | Más de 250, con distritos mineros y contenido de exploración |
| Estaño | Fuera de los focos, con exclusión propuesta de unos 200 al campamento |
| Distrito de estaño | 3–6 nodos en radio aproximado 20–30; una ciudad puede alcanzar varios |
| Total de estaño | Mantener 64; reagrupar, no crear supernodos |
| Bosques | Muchos pequeños/medianos en focos y pocos grandes compartidos en frontera |
| Movimiento y detección | Subpasos de 1–2 segundos dentro del tick económico |

Quedan abiertos en ese documento el reparto del oro y la capacidad de leñeras de grandes bosques. No se ha inspeccionado el simulador externo enlazado allí; este análisis se basa en el archivo y el código local.

## 8. Deducciones para nuestro worldgen

### 8.1 La escala debe expresarse en relaciones y tiempos

Hay tres magnitudes distintas: unidad estratégica de BronzeAge, unidad de representación Unity y unidad local del asentamiento. Añadir a eso el reloj económico evita confundir `30/tick` con `30/s`.

Nuestra prueba a 12 u/s y la calibración a 0,5 unidades estratégicas/s pueden tener la misma sensación de movimiento mediante una conversión. Como **ejemplo matemático**, conservar ambas velocidades exige:

```text
12 unidades Unity/s ÷ 0,5 unidades estratégicas/s
  = 24 unidades Unity por unidad estratégica
```

Esta conversión **no está aprobada ni implementada**. Sirve para revelar incompatibilidades:

| Magnitud | Calibración | Equivalente Unity con factor 24 | Prueba Unity actual |
|---|---:|---:|---:|
| Separación mínima | 30 | 720 | Sin regla autoritativa de fundación conectada; auditoría inicial a 180 |
| Influencia inicial | 6 | 144 | 90 |
| Máximos N1–N5 | 10/15/20/25/30 | 240/360/480/600/720 | 180/270/360/450/540 |
| Ancho visible | 15 | 360 | `viewSize=320` es un parámetro de cámara, no ancho visible medido |
| Entrada a puerta | 2 | 48 | Distancia de prueba 18 |
| Radio de foco | 150 | 3.600 | No hay focos del worldgen implementados |
| Separación de campamentos | 600 | 14.400 | No hay distribución equivalente integrada |
| Radio de distrito minero | 20–30 | 480–720 | Concentraciones de estaño amplias, no distritos compactos |

A ese factor, 32.000 × 24.000 representaría aproximadamente 1.333 × 1.000 unidades estratégicas. Un mapa estratégico de 2.000 × 2.000 requeriría 48.000 × 48.000 unidades Unity para conservar ese mismo factor. Es imposible conservar a la vez todas esas dimensiones, radios, velocidades y equivalencias sin elegir qué relación priorizar.

En el mundo actual, a 12 u/s, recorrer directamente 32.000 tarda unos 44,4 minutos; 24.000, 33,3; la diagonal de 40.000, 55,6. Son referencias sin obstáculos. El mapa de 2.000 a 0,5/s tarda unos 66,7 minutos por lado. No son el mismo tiempo global, aunque ambos pueden dar una campaña válida.

Como ya se aceptaron velocidad, miniaturas e influencia visual en Unity, la propuesta es conservar esa prueba como referencia y medir equivalencias antes de aplicar un factor global. No reescalar el mundo por el nombre «km».

### 8.2 Focos de actividad y regiones no son lo mismo

Una región define geografía e identidad; un foco concentra fundación, abastecimiento y encuentro. Una región amplia puede contener varios focos de distintos reinos, con frontera salvaje entre ellos. Las regiones secas pueden tener menos focos y conservar atractivo minero o de conexión.

Esto responde mejor a la necesidad de encuentros frecuentes que aumentar uniformemente la población o acelerar todo el mundo. Pero 8–10 focos del mapa pequeño no se pueden copiar sin medir al rectangular. Tampoco cuatro arranques por área prueban que haya suficientes focos para 100+ asentamientos.

### 8.3 El worldgen debe conocer los campamentos antes de distribuir economía

El original coloca campamentos después de generar terreno y recursos. Si su distancia define densidad de piedra, bosque, fauna y minería, hay que invertir esa dependencia:

```text
geografía y accesibilidad
  → emplazamientos aptos de campamentos/focos
  → campos de distancia y anillos de actividad
  → bosque explotable y recursos
  → distritos mineros y contenido asociado
  → validación de fundación, propiedad y suministro
```

La densidad forestal preliminar o el potencial de suelo pueden ayudar a elegir campamentos; después se fija la cobertura económica. Así se evita el ciclo «campamento necesita bosque definitivo, bosque definitivo necesita campamento».

Las ubicaciones deben quedar en datos versionados y ser reproducibles. No mover nodos manualmente para acomodar un campamento ya colocado, ni decidir los focos solo desde Unity.

### 8.4 Hacen falta dos escalas de bosque

La masa forestal visual puede seguir siendo continua e irregular. Dentro de ella hacen falta fuentes económicas pequeñas para fundaciones y comarcas grandes capaces de abastecer varias ciudades.

Una solución compatible sería asociar al campo continuo **sectores o masas explotables con ID y capacidad**, cuya propiedad se resuelva por las influencias reales. El aspecto no tiene por qué mostrar fronteras rectangulares de esos sectores. Esto es una propuesta de integración, no una implementación existente.

Bosque grande no equivale a suministro ilimitado para todas las ciudades: madera permanente y capacidad de productores son reglas diferentes. Hay que resolver el límite de leñeras que la calibración mantiene abierto.

### 8.5 El estaño necesita un cambio de distribución, no solo de cantidad

Nuestro mapa garantiza fuentes en todas las regiones y varias ciudades disputando concentraciones amplias. La calibración propone colonias mineras que capturen varios nodos próximos, lejos de los focos iniciales. Es un cambio de estructura económica.

El código regional todavía busca separaciones de estaño de al menos 1.100 unidades, pensadas para superar el diámetro N5 de 1.080. Esa restricción no debe mantenerse como requisito universal: el usuario confirmó que varias fuentes del mismo tipo dentro de una influencia son válidas. Una versión de distritos compactos debe revisar también el diagnóstico que suponía ese aislamiento.

Se pueden conservar **distritos y fuentes aisladas por región**, pero su cantidad, exclusión del foco y cobertura territorial tienen que definirse juntas. El límite de un extractor por nodo favorece varios nodos ordinarios: aumentar la reserva de un supernodo no aumenta automáticamente su producción simultánea.

No hay obligación de que cada ciudad tenga estaño propio. Sí hay que comprobar que cada región conserve alternativas explotables: colonias, comercio y disputa. Los bots deben conocer esas alternativas. La reposición preserva las fuentes en el tiempo, pero no resuelve un monopolio territorial ni una ruta demasiado larga.

### 8.6 Densidad útil antes que multiplicador global

Reducir un radio a una sexta parte reduce su área a 1/36. Mantener recursos por influencia exige alrededor de 36 veces más densidad **en los lugares donde se funda**, sin obligar a multiplicar igual toda la tierra salvaje. Ese es el fundamento de concentrar recursos en focos.

En el ejemplo de conversión anterior, el radio Unity máximo pasaría de 540 a 720, aumentando su área alrededor de 1,78 veces. Esto demuestra por qué no se puede aplicar a la vez la regla ×36 y los números Unity sin identificar primero el sistema de unidades.

Además, nuestro catálogo regional todavía no incluye hierro. La calibración sí lo necesita en focos/frontera. Esa divergencia debe resolverse al integrar el catálogo económico; no basta con añadir un marcador decorativo.

### 8.7 Navegación, muestreo e interacción requieren precisiones distintas

La navegación local Unity usa 512 × 384 celdas de 62,5 unidades. La calibración pide un grafo que distinga plazas separadas 30 unidades estratégicas. La fórmula propuesta para el mundo 2.000, `max(8, 2000/250)`, produce paso 8, pero copiarla al rectangular sin conversión produce otra precisión.

El Terrain lejano sirve para imagen; el terreno detallado para representación próxima; el grafo para ruta global; la detección de puerta/encuentro exige segmentos o consultas locales más finos. No hay que resolver todo elevando uniformemente la resolución del mundo.

Con movimiento rápido y puertas pequeñas, comprobar solo los extremos de un tick puede saltarse la entrada. Los subpasos de 1–2 s reducen ese riesgo, pero deben incluir comprobación del segmento recorrido. La economía puede mantener su tick de un minuto. Es trabajo del motor, no del worldgen visual.

### 8.8 La ciudad y la provincia deben seguir desacopladas

La calibración conserva el trazado en unidades locales y cambia su conversión a mapa. Eso encaja con miniaturas urbanas de campaña y escenas de asentamiento independientes. No implica rehacer el trazado ni reducir el escenario de paseo/batalla a la escala de la miniatura.

El símbolo Unity necesita su propio perfil de escala y variantes. La influencia necesita un contrato de unidad estratégica. Mezclar ambos provoca ciudades enormes o territorios incoherentes aunque por separado se vean bien.

### 8.9 Streaming y niebla se benefician de separar capas

Los sectores de Terrain de 4.000 pueden mantenerse mientras se estudia densidad. Las entidades dinámicas y páginas de niebla pueden usar celdas más pequeñas; no tienen que compartir tamaño con el terreno.

Concentrar población alrededor de campamentos aumenta carga local de entidades. Por eso la validación debe medir el foco lleno, no solo nueve terrenos vacíos. Las máscaras de exploración/visión también requieren resolución acorde con ciudades y radios, sin que la cámara conceda visión.

## 9. Aclaraciones numéricas del archivo de calibración

Estas observaciones no cambian sus decisiones; ayudan a evitar aplicarlas con una interpretación equivocada.

1. El resumen conserva «900 entre focos, 30 min», pero la decisión posterior fija 600. A velocidad ligera 30/min, **600 son 20 min entre centros**. El hueco mínimo entre focos de radio 150 es 300 y tarda 10 min.
2. «Recorrer zona N2 en 30 s» corresponde a recorrer su **radio 15**, no su diámetro 30, que tarda un minuto.
3. Con radios iniciales 6 y centros a 30, el hueco entre bordes de influencia es 18: 36 s a 0,5/s. Los aproximadamente 50 s de borde a borde necesitan aclarar a qué borde se refieren.
4. «Sin estaño en el foco de 150» y «ninguno a menos de unos 200» dejan una franja adicional de exclusión de 50. Debe ser un parámetro explícito, no una cifra deducida ambiguamente.
5. 12–15 vetas de unos cinco nodos son una aproximación: para conservar exactamente 64, las cuotas reales deben sumar 64; no generar una cantidad variable accidental.
6. 90 plazas por foco es una estimación geométrica, no capacidad económica. El círculo del foco dividido por un círculo de radio 15 da 100 antes de pérdidas de empaquetado, exclusiones, bosque, pendiente y límites. Además, N5 tiene radio 30: los territorios de plazas vecinas a distancia 30 se recortarán mucho al crecer, algo permitido pero que exige volver a contar fuentes accesibles.
7. La capacidad 360–900 plazas de 8–10 focos no es número de jugadores. Nuestra referencia de cinco jugadores por asentamiento requiere aproximadamente 100 asentamientos para 500 jugadores; los bots y el crecimiento futuro deben contabilizarse aparte.

## 10. Qué conservar y qué revisar al continuar

**Conservar como base de prototipado:** composición mediterránea rectangular, variedad de áreas, campos reproducibles, pasos/valles, hidrografía, costas canónicas, texturas aceptadas, masas de copas, separación de escenas y carga por sectores.

**Revisar antes de integrar economía:** equivalencia de unidades/reloj, distancias entre plazas, focos, fuentes económicas de bosque, capacidades de leñeras, distribución compacta o amplia de estaño, hierro, radios territoriales y precisión local de detección. Los radios de la prueba Unity permanecen una referencia aceptada, no constantes ya aplicadas al motor.

**Pendiente de representación:** sustituir la prueba de protuberancias Synty por caras/laderas integradas y verificarlas en varias semillas. Blender es una opción de autoría, todavía sin entrega.

Orden sugerido, sin ejecutarlo en esta revisión:

1. Fijar un contrato de escala entre estrategia, Unity, asentamiento y reloj; medir ancho visible real, no solo el parámetro de cámara.
2. Comparar en el mismo mapa un entorno denso de plazas y su frontera, manteniendo la referencia visual aceptada.
3. Generar focos y campos de distancia desde campamentos aptos y conectados, sin perder identidad regional.
4. Definir fuentes forestales explotables y recurso por influencia; integrar hierro y elegir distribución de estaño.
5. Auditar simultáneamente arranques, recortes de frontera y productores: madera/piedra, stock/reposición, rendimientos, mantenimiento, comercio y bots. Contar nodos por tipo sin imponer un máximo de uno.
6. Comparar semillas y medir viajes reales, acceso minero, variación regional y carga de focos llenos. Los costes diagnósticos no son segundos de viaje.
7. Integrar identidad de mundo, estado dinámico y comandos con BronzeAge; después completar niebla visual y sus pruebas, conservando filtrado del servidor desde el primer momento.

La conclusión de la calibración para este trabajo es concreta: **el siguiente salto del worldgen es organizar dónde ocurre la actividad y dónde se disputa la riqueza**, apoyándose en la geografía ya construida. El aspecto del mapa y la cantidad bruta de nodos no bastan para decidir su densidad jugable.

## 11. Fuentes y límites de esta revisión

Se leyeron código, manifiestos, campos exportados y documentos existentes. Los recuentos actuales se extrajeron del paquete que referencia Unity. No se ejecutó una nueva campaña de tests, benchmark de FPS ni simulación económica durante esta revisión. Las validaciones citadas son registros anteriores con el alcance indicado.

### BronzeAge principal y calibración de Claude

- [Pipeline principal](C:/Users/VRINO/Desarrollo/BronzeAgeFase0/src/worldgen/index.ts), [configuración](C:/Users/VRINO/Desarrollo/BronzeAgeFase0/src/worldgen/config.ts) y [versiones](C:/Users/VRINO/Desarrollo/BronzeAgeFase0/src/worldgen/types.ts).
- [Fachada del mapa y explotación](C:/Users/VRINO/Desarrollo/BronzeAgeFase0/src/world/mapa.ts), [grafo de navegación](C:/Users/VRINO/Desarrollo/BronzeAgeFase0/src/world/grafoNavegacion.ts) y [constantes económicas/territoriales](C:/Users/VRINO/Desarrollo/BronzeAgeFase0/src/constants.ts).
- [Frontera aceptada entre geografía y presentación](C:/Users/VRINO/Desarrollo/BronzeAgeFase0/Docs/Coordinacion/propuestas/BA-003_worldgen_y_asentamientos_3d.md).
- [Calibración completa, worktree de Claude](C:/Users/VRINO/Desarrollo/BronzeAgeFase0/.claude/worktrees/eloquent-rubin-db3879/Consideraciones/Calibracion_Distancias_Definicion.md).

### Generador regional experimental

- [Informe de la primera fase v16](C:/Users/VRINO/.codex/worktrees/worldgen-relieve/BronzeAgeFase0/Docs/Arquitectura/11_Informe_Worldgen_v16.md).
- [Entrada regional](C:/Users/VRINO/.codex/worktrees/worldgen-relieve/BronzeAgeFase0/scripts/visor-worldgen.regional.ts) y [composición del relieve](C:/Users/VRINO/.codex/worktrees/worldgen-relieve/BronzeAgeFase0/src/worldgen/regional.ts).
- [Bosque continuo](C:/Users/VRINO/.codex/worktrees/worldgen-relieve/BronzeAgeFase0/src/worldgen/forestacionRegional.ts), [suelos y recursos](C:/Users/VRINO/.codex/worktrees/worldgen-relieve/BronzeAgeFase0/src/worldgen/suelosRegional.ts), [enclaves](C:/Users/VRINO/.codex/worktrees/worldgen-relieve/BronzeAgeFase0/src/worldgen/enclavesRegional.ts) y [costas](C:/Users/VRINO/.codex/worktrees/worldgen-relieve/BronzeAgeFase0/src/worldgen/costasRegional.ts).
- [Auditoría revisada de atractivo regional](C:/Users/VRINO/.codex/worktrees/worldgen-relieve/BronzeAgeFase0/Docs/Arquitectura/24_Atractivos_Regionales.md) y [comparación de semillas](C:/Users/VRINO/.codex/worktrees/worldgen-relieve/BronzeAgeFase0/Docs/Arquitectura/25_Validacion_Semillas_Regionales.md).
- [Contrato y plan de exportación/niebla](C:/Users/VRINO/.codex/worktrees/worldgen-relieve/BronzeAgeFase0/Docs/Arquitectura/22_Exportacion_Unity_Regional.md).
- [Manifiesto de costas actual](C:/Users/VRINO/.codex/worktrees/worldgen-relieve/BronzeAgeFase0/exports-regional/campaign-coasts-v1-seed42/manifest.json) y [datos canónicos exportados](C:/Users/VRINO/.codex/worktrees/worldgen-relieve/BronzeAgeFase0/exports-regional/campaign-coasts-v1-seed42/world.json).

### Unity

- [Manifiesto instalado](C:/Users/VRINO/Desarrollo/Conquest_prototype/Assets/WorldMap/Campaign/Manifest.asset).
- [Plan de campaña](C:/Users/VRINO/Desarrollo/Conquest_prototype/Docs/WorldMapCampaignPlan.md), [organización de escenas](C:/Users/VRINO/Desarrollo/Conquest_prototype/Docs/WorldMapStructure.md) y [conexión preparada con BronzeAge](C:/Users/VRINO/Desarrollo/Conquest_prototype/Docs/BronzeAgeCampaignConnection.md).
- [Masas forestales](C:/Users/VRINO/Desarrollo/Conquest_prototype/Docs/WorldMapForestMasses.md), [costas](C:/Users/VRINO/Desarrollo/Conquest_prototype/Docs/WorldMapCoasts.md) y [restauración de suelos](C:/Users/VRINO/Desarrollo/Conquest_prototype/Docs/CampaignGroundRestoration.txt).
- [Informe de la prueba Synty](C:/Users/VRINO/Desarrollo/Conquest_prototype/Docs/ReliefModelBuild.txt) y [constructor de esa prueba](C:/Users/VRINO/Desarrollo/Conquest_prototype/Assets/Scripts/Editor/WorldMap/WorldMapReliefModelBuilder.cs).
- [Historial de herramientas y experimentos](C:/Users/VRINO/Desarrollo/Conquest_prototype/Assets/WorldMap/README.md), [paquetes instalados](C:/Users/VRINO/Desarrollo/Conquest_prototype/Packages/manifest.json) y [benchmark histórico V9](C:/Users/VRINO/Desarrollo/Conquest_prototype/Assets/Scenes/WorldMapTools/Experiments/MountainsV9/FpsBenchmark.md).

Los documentos de entregas previas conservan cifras como 1.354 recursos, 55 estaños o 238 cobres. Son estados anteriores. Para este recap prevalecen los **1.360 / 56 / 240** leídos del paquete actual; no se han reescrito retrospectivamente esos informes.

## 12. Contrato actualizado: prioridad de la calibración de Claude

El usuario establece que la prueba anterior de Unity está deprecada y que los valores del ámbito de Claude tienen prioridad. Se ha releído el archivo completo actualizado, incluidos los nuevos apartados 2.1, 9 y 10. El documento continúa indicando diseño sin código; esta actualización tampoco modifica implementación.

### Unidad y reloj

```text
posición horizontal Unity = posición estratégica × 12
distancia Unity = distancia estratégica × 12
velocidad Unity/s = velocidad estratégica por tick × 12 / 60
```

El tick económico sigue siendo de 60 segundos. Los subpasos de movimiento/detección y la comprobación del segmento corresponden al motor; una animación fluida Unity no sustituye esas reglas.

El mapa visual conserva 32.000 × 24.000. Su extensión estratégica exacta es 32.000/12 × 24.000/12, aproximadamente 2.666,67 × 2.000. El valor 2.667 del documento es redondeado para exposición: no debe introducirse como factor distinto por eje. No se modifica en esta conciliación la forma de montañas, costas, ríos, lagos y pasos.

### Objetivos de la próxima prueba Unity

| Magnitud | Valor Unity derivado de la calibración |
|---|---:|
| Columna ligera, 30 por minuto | 6 u/s |
| Héroe solo, 33 por minuto | 6,6 u/s |
| Montada, 42 por minuto | 8,4 u/s |
| Caravana de caballo, 36 por minuto | 7,2 u/s |
| Caravana de camello, 28 por minuto | 5,6 u/s |
| Media / buey, 24 por minuto | 4,8 u/s |
| Pesada, 18 por minuto | 3,6 u/s |
| Fundación que vuelve sola, 15 por minuto | 3 u/s |
| Influencia inicial / N1 / N2 / N3 / N4 / N5 | 72 / 120 / 180 / 240 / 300 / 360 |
| Separación mínima entre plazas | 360 |
| Ancho visible habitual, medido sobre el terreno | 180 |
| Radio de foco / límite de frontera | 1.800 / 3.000 |
| Separación de campamentos | 7.200 |
| Exclusión de estaño alrededor de campamento | 2.400 |
| Radio de distrito minero | 240–360 |
| Puerta / encuentro | 24 / 36 |

La tabla de §10 de Claude llama «héroe (ligera)» al ensayo de 6 u/s; la tabla de §4.1 distingue ligera 30 y héroe solo 33. Para probar sin mezclar casos, 6 u/s representa la columna ligera y 6,6 al héroe solo. No se cambia ninguna decisión de velocidad.

La sensación objetivo es cruzar el ancho de pantalla en unos 30 s: 180/6. Se medirá a la profundidad del actor y con un recorrido adecuado a la proyección de la cámara. `viewSize` es un parámetro de distancia/encuadre; asignarle 180 no demuestra que se vean 180 unidades de terreno.

La miniatura urbana debe tener su perfil propio conforme a §10, que propone radio 7–12 Unity. Su medida debe identificarse expresamente como radio o extensión total al validar modelos. Las copas de hasta 98 de diámetro no son una regla de escala urbana. No se cambian el tamaño del escenario de asentamiento ni el trazado local para igualarlos al símbolo de campaña.

### Qué criterios anteriores quedan deprecados

- Velocidad local 12 u/s, entrada 18 y radios 90–540 como referencias de balance vigentes.
- La conversión hipotética 24 del recap y mi propuesta posterior de factor 16: el contrato acordado es 12.
- Capacidad objetivo de 500 jugadores/100 plazas y cuatro arranques por región como condiciones obligatorias. Las auditorías existentes siguen siendo evidencia histórica, pero el usuario ahora prioriza distancias y sensación; la capacidad resultante se mide.
- Separación de estaño ≥1.100 Unity y cualquier diagnóstico que impida varios nodos dentro de una influencia. Los distritos compactos deben admitir múltiples fuentes por ciudad.
- Las siete ambigüedades señaladas en el apartado 9 como problemas pendientes: el documento actualizado las resuelve explícitamente.

### Prioridad de trabajo

Primero probar en Unity velocidad, ancho visible, miniatura, influencia y proximidad con factor 12, sin red ni cambios al motor de Claude. Una ruta entre centros a 360 debe durar 60 s con columna ligera; del centro al borde N2, 30 s; su diámetro, 60 s. El lado largo del mundo tarda 88,89 min y la diagonal 111,11 min sin obstáculos.

Después medir la máscara de tierra y emplazamientos aptos de campamentos a separación 7.200, manteniendo identidad regional y conexión. Puede haber varios focos en una región o ninguno si su atractivo es minero. No copiar el número de campamentos medido en el cuadrado anterior.

El siguiente balance de recursos debe usar campamentos antes que economía, anillos, estaño fuera de exclusión y distritos compactos más fuentes aisladas compatibles, totales en proporción a tierra firme e hierro. El bosque continuo conserva el aspecto; fuentes explotables con ID/capacidad requieren todavía resolver la integración económica y la decisión abierta de leñeras. El reparto de oro también sigue abierto.

Las pruebas actuales no certifican este contrato hasta ejecutarlo. Esta revisión solo actualiza prioridades documentales.

### Aplicación posterior en Unity (2026-10-12)

El usuario autorizó aplicar el contrato y probarlo. Se incorporaron el perfil `WorldMapScaleContract`, la conversión de velocidades, influencias de referencia, miniaturas con escala propia y cámara medida sobre el plano del jugador. La prueba separada `CalibrationFactor12` reutiliza la geografía de campaña y coloca dos ciudades a 360 unidades. El validador ejecutado en Play confirma ligera 60 s, héroe solo 54,545 s, cámara de 180 unidades para tres relaciones de aspecto, conservación del zoom al retornar y límites de proximidad 24. Detalles y alcance: [WorldMap_Calibracion_Factor12.md](WorldMap_Calibracion_Factor12.md).

Esta aplicación no equivale a implementar el pipeline económico de §12: campamentos, anillos de recursos, hierro, distritos de estaño, capacidades forestales y reglas del backend continúan pendientes.
