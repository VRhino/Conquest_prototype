# U6-05 — Render Graph y presentación por distancia

Auditoría del proyecto en Unity `6000.5.8f1`, URP `17.5.0`, Entities `6.5.0` y AI Navigation `2.0.14` (ProjectVersion y Packages/manifest.json). Se inspeccionaron la configuración Graphics/Quality, los renderer data, los scripts de presentación/combat, la ruta VisualPrefabRegistry y los tres prefabs de unidad registrados. No se modificaron código, recursos ni escenas; no se ejecutaron pruebas ni se controló el Editor.

## Resultado

Render Graph ya es el camino configurado para URP en esta versión. `RenderGraphSettings` en el PackageCache instalado está obsoleto desde `6000.4` y `enableRenderCompatibilityMode` es una propiedad de sólo lectura que retorna `false` (`Library/PackageCache/com.unity.render-pipelines.universal@73b4c4ff130e/Runtime/Settings/RenderGraphSettings.cs:42-43, 62-66`). No hay un toggle útil por activar en los assets del proyecto; una tarea de backlog para “activar Render Graph” quedó obsoleta.

No hay clases runtime del proyecto derivadas de `ScriptableRendererFeature`/`ScriptableRenderPass`, ni llamadas runtime del proyecto a `RecordRenderGraph`, `AddUnsafePass`, `Blitter.Blit` o `CommandBuffer.Blit`. Los renderer data contienen SSAO oficial de URP en los perfiles Balanced y High Fidelity, y ningún Renderer Feature en Performant. El pass SSAO que trae el paquete implementa `RecordRenderGraph` (`.../Runtime/Passes/ScreenSpaceAmbientOcclusionPass.cs:347`); es una ruta de paquete activa, no una feature propia pendiente de migración.

Para presentación por distancia, los tres prefabs de unidad enlazados por el `VisualPrefabConfig` de la escena activa carecen de `LODGroup`. Sus `SkinnedMeshRenderer` tienen `updateWhenOffscreen: 0`; sus `Animator` serializan culling mode 1 y el puente común cambia el modo runtime a `CullCompletely`. La siguiente oportunidad concreta es una capa visual de menor detalle para las unidades a distancia, pero no se puede apagar el GameObject raíz ni su rig sin proteger primero las colisiones de melee: el `WeaponHitbox` de los visuales Squires y Spearmen es hijo del nodo `Weapon_Carrier`, y los contactos dependen del trigger físico durante la ventana de ataque.

## Configuración activa y features

`ProjectSettings/QualitySettings.asset:7` fija calidad actual 2, `High Fidelity`; el perfil Standalone también apunta a índice 2 (`160-176`). `ProjectSettings/GraphicsSettings.asset:50-51` asigna `URP-HighFidelity.asset` como pipeline Graphics. Por lo tanto, en este checkout el renderer activo esperado es `URP-HighFidelity-Renderer`, índice cero del asset (`URP-HighFidelity.asset:19-21`). El Runtime puede cambiar perfil por calidad; no se encontró llamada de proyecto a `QualitySettings.SetQualityLevel`.

| Perfil | Renderer Feature | Configuración serializada | Lectura práctica |
|---|---|---|---|
| High Fidelity, activo por defecto | SSAO activa (`URP-HighFidelity-Renderer.asset:12-15, 55-57`) | `AOMethod=1`, `Downsample=0`, `Source=1` (DepthNormals), `Samples=0` (High en el enum instalado), `Radius=0.25` (`17-28`). | Pass SSAO de alta calidad, a resolución completa. Su coste es candidato a medir en el informe GPU, no una conclusión de cuello de botella de esta auditoría. |
| Balanced | SSAO activa (`URP-Balanced-Renderer.asset:12-15, 41-45`) | `Downsample=1`, `Source=0` (Depth), `SampleCount=4` legado (`18-28`). `ScreenSpaceAmbientOcclusion.Create()` convierte el valor legado 4 a método interleaved y opción Low en URP (`ScreenSpaceAmbientOcclusion.cs:143-162`). | Preset más austero existente; el asset ya contiene la alternativa y no necesita un Renderer Feature nuevo. |
| Performant | Ninguna (`URP-Performant-Renderer.asset:18-20`) | — | Ya omite SSAO. |

Los valores de sombras, MSAA y luces también varían mucho por calidad. No se recomiendan cambios de esos valores en U6-05 porque su impacto requiere la captura GPU del perfil concreto y el equipo que audita GRD; evitar duplicar esa investigación aquí.

## Inspección Render Graph

| Hallazgo | Evidencia | Estado y acción |
|---|---|---|
| URP PackageCache local marca `RenderGraphSettings` obsolete `#from(6000.4)`; getter compatibility devuelve `false`. | `.../Settings/RenderGraphSettings.cs:42-43, 62-66` | La compatibilidad está deshabilitada por el API instalado; no añadir un parámetro de config ni invocar un accessor obsoleto. |
| Los perfiles URP enlazan renderer data por default index 0; calidad actual e `GraphicsSettings` apuntan a High Fidelity. | `QualitySettings.asset:7, 160-176`; `GraphicsSettings.asset:50-51`; `URP-HighFidelity.asset:19-21` | Contexto de configuración del proyecto, no captura de una cámara en ejecución. |
| High Fidelity/Balanced llevan la feature SSAO oficial; Performant no incluye features. | `URP-HighFidelity-Renderer.asset:12-28, 55-57`; `URP-Balanced-Renderer.asset:12-28, 41-45`; `URP-Performant-Renderer.asset:18-20` | Reutilizar los presets. Comparar solo mediante perfil de GPU equivalente si se cambia perfil/calidad. |
| El pass SSAO oficial agrega su pass cuando cámara/recursos lo permiten y lo encola en `AddRenderPasses`; `ScreenSpaceAmbientOcclusionPass` registra su ruta Render Graph. | PackageCache URP: `Runtime/RendererFeatures/ScreenSpaceAmbientOcclusion.cs:165-219`; `Runtime/Passes/ScreenSpaceAmbientOcclusionPass.cs:347` | No hace falta escribir feature de SSAO ni migrar un pass del proyecto. |
| Búsqueda de `ScriptableRendererFeature`, `ScriptableRenderPass`, `RecordRenderGraph`, `AddUnsafePass`, `Blitter.Blit`, `cmd.Blit` sobre `Assets/Scripts` no encontró uso runtime. | `Assets/Scripts/**` búsqueda completa; único `Graphics.Blit` está en `Assets/Scripts/Editor/WorldMap/WorldCampaignArt.cs:1-12, 114`. | El blit hallado pertenece a generación de arte de campaña en Editor, no a un render pass de cámara del Player. Mantenerlo fuera de backlog runtime/Render Graph. |

No hay una razón para migrar el blit de `WorldCampaignArt` a Render Graph: corre desde herramientas/editor y opera con textura temporal durante un bake/preview (`WorldCampaignArt.cs:28-38, 108-116`). Si en el futuro se añade un pase de cámara propio, exigir `RecordRenderGraph` en primera implementación y revisar dependencias de recursos/cámara en Render Graph Viewer; no abrir una tarea de migración preventiva ahora.

## Presentación de unidades por distancia

### Qué prefabs llegan a runtime

`Assets/Prefabs/Map/visualRegistry.prefab:47` enlaza `Assets/ScriptableObjects/VisualPrefabConfig.asset` (GUID local `c19d2bd056c44e64fa31196c39db8e13`). Ese asset lista Squires, LevyArchers y Spearmen (`VisualPrefabConfig.asset:26-44`) y sus referencias resuelven a:

| Tipo registrado | Prefab visual | Animator y datos serializados |
|---|---|---|
| Squires | `Assets/Resources/Squads/Squires/Squires_GO_Squeleton.prefab` | 1 Animator, modo culling serializado 1, modo Update 0, root motion 0; `39454-75`. No `LODGroup`. |
| Spearmen | `Assets/Resources/Squads/Spearmen/Spearmen_GO_Squeleton.prefab` | 1 Animator, culling serializado 1, Update 0, root motion 0; `6571-92`. No `LODGroup`. |
| LevyArchers | `Assets/Resources/Squads/Levy_Archers/Levy_Archer_GO_Squeleton.prefab` | 1 Animator, culling serializado 1, Update 0, root motion 0; `46878-99`. No `LODGroup`. |

Los tres prefabs tienen muchos nodos modulares serializados (720 componentes SKinnedMeshRenderer cada uno, de los cuales la mayoría pertenece a GameObjects inactivos de variantes); una inspección de YAML encontró aproximadamente 13–17 renderers activos en el árbol serializado según tipo. Esos conteos no son un conteo de draw calls ni una medición runtime; el prefab/instanciación efectiva debe capturarse en Editor o Player antes de decidir el tamaño del LOD.

`SquadVisualManagementSystem` resuelve el prefab desde `VisualPrefabRegistry`, instancia el GameObject, agrega `EntityVisualSync` y enlaza hitboxes a la entidad (`Assets/Scripts/Squads/Systems/SquadVisualManagement.System.cs:77-131, 142-165`). La scene registry/config es la fuente concreta para las tres clases de unidades. `Assets/Config/DefaultVisualPrefabConfig.asset` es otro asset con un visual básico, pero no está referenciado por la escena `visualRegistry.prefab`; no asumir que esa configuración es la activa.

### Culling actual y dependencias de juego

| Mecanismo | Comportamiento actual | Consecuencia para distancia |
|---|---|---|
| `SkinnedMeshRenderer.updateWhenOffscreen = false` en renderers de las variantes registradas. | No fuerza la actualización del skinned mesh cuando está fuera de pantalla (`Squires...prefab:85`, patrón repetido en las tres). | Es culling de renderer ya activado; no equivale a eliminar LODs ni a detener `MonoBehaviour.Update`. Confirmar calidad de reaparición al entrar en pantalla en captura visual. |
| `VisualSyncUtility.SetupVisualSync` recorre todos los Animator hijos y ajusta `cullingMode=CullCompletely`, `applyRootMotion=false`. | Aplicado en creación de visual de unidad por `SquadVisualManagementSystem:125`; implementación `VisualSyncUtility.cs:35-42`. | Unity puede dejar de actualizar el Animator cuando los renderers están fuera de vista. Hay un hitbox melee hijo de `Weapon_Carrier` en Squires y Spearmen; evaluar impacto funcional fuera de cámara antes de cambiar culling o apagar ese árbol. |
| `UnitAnimationAdapter.Update` y `UnitAnimatorController.Update` siguen siendo componentes habilitados del visual. | Adaptador tiene `[DefaultExecutionOrder(-80)]`, `Update` en `UnitAnimationAdapter.cs:12-13, 102-108`; controlador tiene `-60` y escribe Animator en cada Update (`UnitAnimatorController.cs:12-16, 144-190`). | El Animator culling por sí solo no pausa esos scripts. La reducción de CPU de un futuro LOD requiere medir/gestionar scripts de presentación aparte, sin desactivar `NavMeshAgent`/sync. |
| Animator de unidades root motion desactivado; movimiento raíz lo gobierna NavMeshAgent/ECS sync. | Prefabs arriba; `VisualSyncUtility.cs:39-42`; `UnitAnimation.System.cs` y docs híbridos existentes. | Mantener locomoción física activa aunque se simplifique el esqueleto visual. |
| WeaponHitbox de Squires y Spearmen es un `BoxCollider` trigger + Rigidbody kinematic bajo `Weapon_Carrier`; el componente `WeaponHitboxBehaviour` encola contacto físico. | Prefabs `Squires...prefab:8347-8443` y `Spearmen...prefab:30932-30980`; padre de transform WeaponHitbox es el GO `Weapon_Carrier`; `WeaponHitboxBehaviour.cs:9-10, 22-41`. | Apagar Animator, rig o GameObject padre puede dejar el collider en pose quieta o impedir `OnTriggerStay`, alterando daño melee fuera de cámara. Hay que separar el collider de la parte visual animada o conservar evaluación suficiente antes de aplicar un culling más agresivo. |
| El tag ECS habilita la aceptación del contacto en ventana de strike. | `UnitAttack.System.cs:12-19, 53-68`; `MeleeContactQueue.cs:22-55`. | La ventana de gameplay sigue en ECS, pero el contacto físico sigue necesitando un trigger en posición útil. No sustituir la geometría por un Animator event inexistente. |
| Proyectiles de unidades de rango nacen de `ProjectileSpawnRequest` en ECS; el evento `isFiring` solo informa animación. | `RangedAttack.System.cs:5-12, 156-176`; `UnitAnimationAdapter.cs:34-38, 217-227`. | Pausar animación lejana no cancela el disparo ECS. Verificar que cualquier visual de flecha/fuego conserve su ciclo/consumo si se ajusta cadencia. |
| Los clips `.anim` propios inspeccionados declaran `m_Events: []`; no se localizaron callbacks de AnimationEvent en los controllers de unidad. | `Assets/Animations/*.anim` (OpenDoors, CloseDoors, LowRamp, Draw_arrow, swordAttackIsolated); controllers activos citados arriba. | No hay hook de AnimationEvent que se pueda retirar para optimizar; ataques/tiros de unidad se temporizan en ECS. |
| `ShieldHitboxBehaviour` se descubre en prefab y agrega capacidad de escudo; la búsqueda no encontró ese componente en los tres visuales actualmente listados en el registry. | `SquadVisualManagement.System.cs:103-107, 142-165`; búsqueda en los tres prefabs. | No atribuir la lógica de escudo a un evento animado o a estos tres modelos actuales. Otros héroes/variantes deben auditarse por separado antes de un cambio de culling global. |

`EntityVisualSync.Update` posee una escritura relevante para cualquier refactor híbrido: para un NavMesh agent con `syncPositionFromNavMesh`, omite asignar el campo Position, pero sigue llamando incondicionalmente `SetComponentData(_heroEntity, ecsTransform)` después de gestionar Rotation (`Assets/Scripts/Visual/EntityVisualSync.cs:146-155`). Por tanto el contrato evita una segunda asignación de posición en ese bloque, pero aún vuelve a escribir el `LocalTransform` completo cuando la rotación está sincronizada. La fuente de pose de unidad y los hitboxes deben conservar el orden observado al separar la raíz de navegación de la presentación.

## Reutilización revisada

| Elemento existente | Ruta | Qué ya resuelve | Recomendación |
|---|---|---|---|
| `VisualPrefabRegistry` + `VisualPrefabConfiguration` | `Assets/Scripts/Hero/VisualPrefabRegistry.cs:10-16, 57-100, 178-202`; `VisualPrefabConfiguration.cs:115-151`; `Assets/Prefabs/Map/visualRegistry.prefab:47` | Selección/cache de los prefabs que el sistema instancia; config registra los tres tipos activos. | Extender el registro/config si se requieren tiers visuales alternativos; no crear un segundo registry. |
| `SquadVisualManagementSystem` | `Assets/Scripts/Squads/Systems/SquadVisualManagement.System.cs:77-131, 142-165` | Instanciación, `EntityVisualSync`, wiring de atacante y capacidades de escudo en entidades visuales. | Mantener como dueño del ciclo de vida; no apagar la raíz al alejar la cámara. |
| `VisualSyncUtility` / `EntityVisualSync` | `Assets/Scripts/Shared/VisualSyncUtility.cs:32-48`; `Assets/Scripts/Visual/EntityVisualSync.cs:106-188` | Configuración de Animator y enlace de identidad/pose. | Punto existente para evaluar política de culling y posible separar raíz física/presentación; cambio global requiere test de hitboxes y héroes. |
| `UnitAnimationAdapter` / `UnitAnimatorController` | `Assets/Scripts/Squads/UnitAnimationAdapter.cs`; `Assets/Scripts/Squads/UnitAnimatorController.cs` | Traducción de estado ECS a parámetros del Animator; orden de ejecución explícito entre adapter `-80` y controller `-60`. | Reusar; no crear nuevos drivers por tier sin perfilar. |
| ECS melee/ranged systems + comportamiento de hitbox | `Assets/Scripts/Combat/UnitAttack.System.cs`, `Assets/Scripts/Combat/MeleeContactQueue.cs`, `Assets/Scripts/Combat/WeaponHitboxBehaviour.cs`, `Assets/Scripts/Combat/Systems/RangedAttack.System.cs` | Temporización y autoridad del combate/daño. | Reusar como fuente de verdad al validar la distancia visual; evitar que presentation LOD cambie lógica. |
| LOD/renderer feature/pase de cámara propio | Búsqueda en `Assets/Scripts`, `Assets/Resources/Squads` y renderer data actuales | No hay `LODGroup` en los tres prefabs ni feature/pass de cámara runtime del proyecto. | Para LOD, crear tiers/LODGroup en los prefabs visuales registrados sólo tras definir meshes y límites gameplay; no crear RenderFeature. |
| Servicios Core, Inventory y otros eventos | `Assets/Scripts/Core/`, `Assets/Scripts/Inventory/Services/`, `Assets/Scripts/Events/` | Búsqueda de reuse extendida; no administran renderer tiers, rig ni Animator de unidades. | No aplican a U6-05. |

## Tareas propuestas para backlog

| Prioridad | Tarea pequeña | Criterio de aceptación | Riesgo/validación |
|---|---|---|---|
| P0, cierre documental | Retirar del backlog la recomendación de «activar Render Graph/compatibility toggle» para el proyecto actual. | No existe tarea runtime de migración/toggle basada en `RenderGraphSettings`; si aparece un pass propio, nace con `RecordRenderGraph`. | Reabrir solo ante una feature/pass propio o un paquete que requiera compatibilidad; validar con la versión de URP instalada y captura Frame Debugger/Render Graph Viewer. |
| P1, investigación de gameplay | Reproducir melee con attacker fuera del frustum y target visible, para Squires y Spearmen, primero con el estado `CullCompletely` actual. | El `WeaponHitbox` sigue produciendo contacto en strike window, las unidades aplican daño una vez y al volver a cámara el rig no salta. | Si falla: desacoplar el trigger gameplay de `Weapon_Carrier` o mantener evaluación de la parte animada que lo gobierna. Medir costo antes de fijar modo. Añadir caso de escudo solo con prefab activo que contenga `ShieldHitboxBehaviour`. |
| P2, candidato de contenido | Diseñar meshes low-detail y añadir LODGroup por tier a los tres prefabs de unidad registrados, dejando `NavMeshAgent`, sync, colliders y raíz de combate fuera de la jerarquía visual que se desactiva. | Transiciones estables al acercar/alejar; mismo socket/pose, silhouette aceptada, hitbox melee funcional en ambos tiers, disparos ECS, sin render de dos tiers fuera del intervalo cruzado. | No hay mesh LOD bajo de reemplazo ni LODGroup actuales; es tarea de contenido + prefab. Validar en cámara orbitando, formación densa y perfil Player. No se predice ahorro hasta medir GPU/CPU. |
| P3, solo si el profile muestra CPU de presentación | Evaluar cadencia/pausa de `UnitAnimationAdapter` y `UnitAnimatorController` para tiers lejanos, manteniendo awake el root/NavMeshAgent y toda señal de gameplay. | El Animator retoma locomoción/ataque desde snapshot actual al entrar al tier cercano; `isFiring`, attack bools, strike-window y recarga no se pierden. | La ejecución `Update` sigue viva aunque Animator sea cull-complete. Bajar frecuencia puede aumentar latencia/aliasing y exige prueba de reaparición y combate fuera de cámara. |
| P4, condicionado a captura GPU | Comparar clones del mismo perfil High Fidelity variando sólo SSAO (off/on y después downsample/samples), usando los valores de Balanced como referencia para la feature, sin cambiar todo el perfil. | Datos Player reproducibles que separan SSAO/render pass, main thread, GPU y costo por frame; decisión documentada sin extrapolar FPS global. | La configuración High Fidelity SSAO está a resolución completa con depth normals/High samples; esto identifica un candidato de prueba, no una mejora demostrada. La captura y evaluación se coordinan con U6 GPU/GRD. |

## Validación visual y funcional

Para cualquier cambio de presentación, probar una cámara inmóvil y una órbita para que unidades entren/salgan repetidamente del frustum; tener Squires/Spearmen en strike window fuera de cámara golpeando objetivos visibles, y confirmar hitbox aceptado, daño único, escudo donde exista el prefab adecuado, y stop/locomoción al volver a plano. Para LevyArchers validar que la request de proyectil y munición siguen en ECS aun con el Animator fuera de cámara, y que la animación de disparo no se pierde al recuperar visibilidad.

Capturar además una formación densa con tier cercano/medio/lejos en Player Development: porcentajes de transición y distancias visibles, pop-in, silueta, armas/shields, sombras, skinned mesh y frame cost separado CPU/GPU. Comparar build idéntica antes/después en el mismo dispositivo y configuración, con cache caliente y suficiente duración. Frame Debugger/Render Graph Viewer sirve para confirmar qué passes y draws están activos; Profiler/RenderDoc/Unity GPU profiler sirve para tiempos. El modo Editor permite inspección pero no reemplaza la medición Player.

## Límites del hallazgo

- El contenido tiene muchos nodos de variantes inactivos y decenas de renderers visibles potencialmente distribuidos entre unidades; conteo YAML no equivale a batch, draw call, skinning cost ni ocupación GPU.
- El culling actual de Animator no apaga los adaptadores MonoBehaviour ni la lógica de combate ECS. Cualquier optimización por distancia debe indicar explícitamente qué parte del árbol se apaga y demostrar que no contiene autoridad física o trigger de gameplay.
- La evaluación `CullCompletely` puede congelar un rig fuera de vista; el hijo de melee bajo `Weapon_Carrier` lo convierte en riesgo funcional tangible, pero no se midió un fallo durante esta auditoría. El primer paso es la reproducción de aceptación, no asumir el resultado.
- La configuración High Fidelity parece activa según assets Graphics/Quality; cámaras concretas pueden usar renderer override o un runtime quality distinto. Confirmar la cámara de batalla y perfil real en Player al recoger métricas.
- No se generaron medidas nuevas de GPU/CPU y no hay afirmación de FPS.
