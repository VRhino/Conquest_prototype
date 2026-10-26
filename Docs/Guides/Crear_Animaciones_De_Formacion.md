# Crear e integrar animaciones de formación

Fecha: 2026-10-25. Caso de referencia: **Line de lanceros**.

Esta guía recoge el proceso con Blender y Unity, los fallos encontrados y cómo
repetirlo con otras tropas. El usuario confirmó el funcionamiento de la formación.
La validación automatizada de la integración cubrió 13 pruebas distintas en
EditMode; no equivale a una prueba automatizada de batalla completa.

## 1. Separar disposición, postura y movimiento

| Responsabilidad | Datos y código existentes |
|---|---|
| Dónde se coloca cada soldado y hacia dónde mira | `FormationDefinition.templates`, `FormationSystem`, `UnitGridSlotComponent` |
| Qué postura corresponde a su papel y estado | `FormationDefinition.postures`, `UnitFormationStateSystem`, `UnitPostureSystem` |
| Cómo se representa y mezcla con locomoción | `UnitAnimationAdapter`, `UnitAnimatorController`, Animator del prefab visual |

La formación pertenece a la escuadra; cada soldado recibe una ranura y una postura.
No se importa una animación de veinte soldados unidos. Si un soldado cambia de
papel, puede cambiar de postura con él.

Para Line se reutilizó esta estructura: se añadieron dos valores a `UnitStance`,
dos clips Humanoid, una capa de Animator y la tabla de posturas del asset. No se
añadieron sistemas ECS ni se cambió la navegación.

## 2. Interpretar las referencias

Las seis imágenes mostraban la misma Line desde delante, detrás, ambos lados y
vistas elevadas. La cobertura de escudos no debía confundirse con las formaciones
`ShieldWall` o `Testudo` del proyecto.

1. Identificar filas y papeles repetidos.
2. Usar los laterales para entender rodillas, torso y altura de manos.
3. Usar la vista trasera para interpretar agarres ocultos por los escudos.
4. Usar vistas elevadas para revisar separación, orientación y cruces de armas.
5. Reconocer las partes ocultas: varias imágenes permiten aproximar las poses,
   pero no recuperar exactamente todas las articulaciones de cada soldado.

Aquí bastaron dos poses base: frente arrodillado con escudo frontal y retaguardia
de pie con escudo elevado. Las imágenes estáticas no describen el movimiento de
arrodillarse, levantarse o atacar; eso requiere autoría o referencias adicionales.

## 3. Partir del prefab real

La fuente fue `Assets/Resources/Squads/Spearmen/Spearmen_GO_Squeleton.prefab`.
Se reutilizaron su Avatar, sus 14 piezas visibles, el escudo y la lanza. Importar
todo el FBX modular habría mostrado piezas que la tropa no usa.

Geometría original:

- `Assets/Synty/PolygonFantasyHeroCharacters/Models/ModularCharacters.fbx`
- `Assets/Synty/PolygonFantasyKingdom/Models/SM_Wep_Spear_06.fbx`
- `Assets/Synty/PolygonFantasyHeroCharacters/Models/Weapons/SM_Wep_Shield_Long_14.fbx`

El prefab ECS y el visual son distintos. `Spearmen.asset` referencia
`UnitPrefab_ECS_Spearmen`; el registro visual resuelve el modelo visible mediante
`Assets/ScriptableObjects/VisualPrefabConfig.asset`. El Animator se asigna al
prefab **visual**.

## 4. Construir las poses en Blender

Las fuentes están en `ArtSource/SpearmenLine/`, fuera de `Assets`.

| Archivo | Función |
|---|---|
| `inspect_source.py` | Lee las piezas visibles del prefab e importa el esqueleto original |
| `build_preview.py` | Reconstruye escala, materiales y equipo |
| `pose_preview.py` | Crea las dos poses, la escuadra de revisión y exporta a JSON |
| `preview_settings.json` | Ajustes de articulaciones, agarres, separación y presentación |
| `spearman_source.blend` | Entrada de la etapa de ensamblado |
| `spearman_assembled.blend`, `assembly_report.json` | Entradas de la etapa de poses; el JSON contiene matrices necesarias |
| `spearmen_line_pose_review.blend` | Archivo editable de revisión con seis soldados |

Aprendizajes:

- **Escala:** el esqueleto FBX tenía un factor 100 respecto a las mallas. Se
  normalizó con el factor de unidades y la escala 0,9 del prefab. Hay que conservar
  las matrices originales antes de modificar padres o escalas.
- **Materiales:** sumar las máscaras del shader producía colores lavados. Se
  reconstruyó su selección por canales. Blender aproxima el aspecto; Unity y sus
  materiales originales siguen siendo la referencia final.
- **Alcance:** elevar el escudo sin considerar la longitud del brazo dejaba objetivos
  inalcanzables. Se ajustaron posición, muñeca y polos de codo.
- **Agarre:** orientar brazo y mano no basta. También se ajustaron dedos y posición
  local del equipo para aproximar la palma al asa.
- **Escuadra:** una pose individual correcta puede atravesar al vecino. Se
  revisaron ambas filas juntas y los ejes de lanzas contra escudos. Esa prueba no
  valida todas las colisiones posibles de mallas, dedos y armaduras.

El estudio usa 0,67 m entre columnas y 0,65 m entre filas. La primera integración
conservaba 1,1 m en Unity. El 2026-10-26 se ajustó Line a las distancias de Blender.
La separación de Blender no se copia automáticamente al asset: se configura en
`Assets/Resources/Squads/Spearmen/Formations/SpearmenLineFormation.asset`.

### Ajustar la distancia desde el Inspector de Unity

- `Spacing = 0.67`: separación entre columnas, cuyas coordenadas X difieren en 1.
- `Templates → Slots → Position Y`: `+0.4850746` en la primera fila y `-0.4850746`
  en la segunda. Esta Y representa profundidad de la plantilla, no altura del soldado.
  La distancia entre filas resulta de `0.67 × (0.4850746 - (-0.4850746)) = 0.65 m`.
- `Noise Radius = 0` y `Noise Angle Degrees = 0`: alineación regular como Blender.
- Las distancias anteriores corresponden a escala de formación 1. Una petición de
  escala de escuadra las multiplica. Cambiar Spacing multiplica ambos ejes; para
  conservar una distancia entre filas concreta, recalcular su Y como
  `± distanciaEntreFilas / (2 × Spacing)`.

La evitación propia entre compañeros limita su distancia a una fracción del
espaciado (actualmente 0,9 × 0,67 = 0,603 m), inferior a los 0,65 m entre filas.
El bodyblock ignora parejas del mismo equipo y la evitación nativa del NavMeshAgent
se desactiva en runtime. No se redujeron radios globales de combate o colisión.
El baker puede advertir que el espaciado es inferior al diámetro corporal genérico
de 1 m: es una advertencia, no un límite que impida guardar la formación. Revisar
las llegadas y los contactos en batalla al ajustar la densidad.

No hace falta regenerar las animaciones al modificar la distancia. Volver a entrar
en Play permite cargar el asset rehorneado; los blobs de una batalla ya iniciada
no se actualizan simplemente por editar el Inspector.

## 5. Convertir al Avatar de Unity

`pose_preview.py` exporta `unity_pose_source.json`: deltas de huesos y equipo
respecto a la pose de referencia, convertidos a los ejes de Unity.

`Assets/Scripts/Editor/SpearmenLineAnimationSetup.cs` ejecuta estos pasos:

1. Instancia el prefab en una escena temporal y desactiva sus comportamientos.
2. Coloca la raíz en el origen con rotación identidad, conservando su escala.
3. Aplica los deltas a las matrices originales del prefab, de padres a hijos.
4. Ajusta los anclajes de lanza y escudo.
5. Obtiene músculos y postura corporal con `HumanPoseHandler` y el Avatar original.
6. Guarda curvas constantes Humanoid y curvas locales del equipo en dos clips.
7. Configura el controlador del lancero y la tabla de posturas de Line.

Precauciones que resultaron necesarias:

- **Origen:** el prefab guardaba una posición de escena. Convertirlo directamente
  contaminaba `HumanPose.bodyPosition` y `RootT`. Se normalizó y se comprobó también
  una instancia desplazada y girada.
- **Ejes y jerarquía:** no copiar ángulos directamente. Algunos huesos auxiliares
  tienen padres distintos; se usan matrices de referencia y nombres. Los duplicados
  `.001` de Blender se corresponden aquí con nombres terminados en ` 1` en Unity.
- **Dedos:** `HumanTrait.MuscleName` no coincide siempre con el binding serializado.
  Por ejemplo, `Left Thumb 1 Stretched` pasa a `LeftHand.Thumb.1 Stretched`.
- **Colliders:** `WeaponHitbox` es hermano de la malla de lanza. Animar solo la malla
  lo dejaría atrás; se anima su padre común `Weapon_Carrier`. Para el escudo se
  utiliza `Off_Hand_Weapon_Carrier`.
- **Raíz de movimiento:** root motion sigue desactivado. La pose no debe desplazar
  la raíz controlada por el sistema de movimiento.

## 6. Conectar posturas y formación

En `SpearmenLineFormation.asset`:

| Papel | Formado | En movimiento |
|---|---|---|
| Front | `SpearLineFront` | Normal |
| Rear | `SpearLineRear` | Normal |
| Flank | `SpearLineRear` | Normal |
| Interior | `SpearLineRear` | Normal |

Los extremos de la segunda fila tienen papel **Flank**: omitirlos dejaría dos
soldados sin la pose elevada. Interior tiene una asignación de respaldo; la
plantilla actual tiene dos filas. Los valores específicos de Line evitan activar
estas poses mediante las posturas genéricas de ShieldWall/Testudo.

```text
Spearmen.asset → FormationDefinition → SquadDefinitionBaker → biblioteca ECS
    → FormationSystem asigna ranuras y papeles
    → UnitFormationStateSystem determina si está formado
    → UnitPostureSystem elige y publica la postura
    → UnitAnimationAdapter lee la entidad enlazada por EntityVisualSync
    → UnitAnimatorController escribe Stance / IsStopped / IsAttacking
    → AC_Spearmen reproduce la pose
```

`SquadUnitSpawnBuilder` añade los componentes necesarios. `SquadVisualManagementSystem`
obtiene el prefab del registro y enlaza su entidad con `EntityVisualSync`. BattleScene
y BattleTestScene referencian el registro configurado con el lancero. El baker
declara dependencia del asset de formación: los cambios deben llegar al horneado
de la subescena antes de probarlos.

## 7. Conservar locomoción y permitir interrupciones

`AC_Spearmen.controller` conserva las cuatro capas originales y añade
`Spear Line Formation`. El controlador compartido de Synty no se modificó.

- Front y Rear usan `SpearLine_Front` y `SpearLine_Rear`.
- La entrada exige Stance correspondiente, IsStopped verdadero e IsAttacking falso.
- La salida ocurre por cambio de postura, movimiento **o** ataque. Son transiciones
  separadas para expresar OR, no una transición que exija las tres condiciones.
- La mezcla dura 0,25 s. Se configura en `unity_integration_settings.json` y se
  aplica al regenerar; también se puede editar directamente en el Animator.

| Fallo encontrado | Causa | Corrección aplicada |
|---|---|---|
| Cuerpo hundido al volver a locomoción | El clip de salida con solo curvas Transform interfería con la traslación Humanoid | Estado vacío con Write Defaults para liberar el cuerpo y restaurar anclajes |
| Entrada en postura que nunca terminaba | AnyState con interrupciones no ordenadas reiniciaba la mezcla cada frame | Ordered Interruption activado con SourceThenDestination |

Estas soluciones se verificaron en este controlador. Al añadir capas, máscaras
o animaciones de equipo hay que revisar sus canales; no aplicar Write Defaults
indiscriminadamente a otros controladores.

En melé, ECS puede conservar la postura de formado. IsAttacking libera la capa
visual durante el ataque; no crea una estocada ni sustituye la lógica de combate.

## 8. Regenerar y validar

Desde la raíz del repositorio, usando el ejecutable instalado de Blender:

```text
blender.exe --background --factory-startup --python ArtSource/SpearmenLine/inspect_source.py
blender.exe --background --factory-startup --python ArtSource/SpearmenLine/build_preview.py --python ArtSource/SpearmenLine/pose_preview.py
```

Después, fuera de Play: **Conquest → Spearmen → Rebuild Line Formation Animations**.

El proceso sobrescribe los archivos de trabajo generados, los dos clips, los
estados de la capa y la tabla de posturas de Line. Conserva los GUID existentes de
clips/controlador. Guardar aparte las ediciones manuales antes de regenerar.
Blender no recarga un archivo abierto cuando cambia en disco; editar un `.blend`
manualmente tampoco actualiza por sí solo el JSON exportado.

Las pruebas deben entrar en los estados **mediante parámetros**. `Animator.Play`
sirve para revisar una pose, pero salta las transiciones y ocultó el fallo de AnyState.

Pruebas de regresión conservadas:

- `Assets/Tests/EditMode/SpearmenLineAnimationTests.cs`: apoyos, alturas, entradas,
  salidas, equipo, interrupciones durante mezcla, raíz trasladada/girada y locomoción.
- `Assets/Tests/EditMode/UnitPostureTests.cs`: papeles, propagación ECS, marcha y
  cambio de Line a otra formación.

En batalla: desplegar una escuadra nueva, seleccionar Line, esperar la llegada,
ordenar marcha, cambiar de formación, volver a Line y entrar en combate. Revisar
extremos, apoyos sobre terreno y colisiones. Complementa las pruebas automatizadas.

## 9. Fuentes y limpieza

Se conservan código de autoría, ajustes, entradas de cada etapa, el `.blend`
editable, el JSON exportado, los assets integrados y las pruebas de regresión.
`assembly_report.json`, pese a su nombre, **es una entrada necesaria del exportador**.

Se retiraron scripts puntuales de inspección/render de Unity, capturas de revisión,
informes desechables y copias `.blend1`. El `.gitignore` local evita incorporar esos
resultados al regenerar; no elimina las pruebas de regresión.

El importador actual está especializado en lanceros. Para otra formación se puede
reutilizar el proceso, pero hay que revisar nombres de equipo, papeles, poses y
condiciones; el mapeo Front/Rear no constituye un importador universal.

## Alcance actual

Dos poses sostenidas, mezcla de entrada/salida y retorno a locomoción. No incluye
respiración, estocadas nuevas, desplazamiento manteniendo pose ni transiciones
coreografiadas. El escudo mueve su collider, pero no añade bonificaciones de defensa
ni garantiza por sí solo un muro sin huecos.

Ver también [Posturas por ranura](../Mechanics/FormationPostures.md),
[Añadir una unidad](AgregarNuevaUnidad_Guia.md) y
[fuentes de autoría](../../ArtSource/SpearmenLine/README.md).
