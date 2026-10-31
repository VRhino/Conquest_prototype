# Plan: visuales ligeros de héroes y unidades en batalla

Fecha: 2026-10-29. Estado: propuesta de implementación; reglas de gameplay confirmadas por el usuario, implementación pendiente.

## Cierre parcial solicitado — 2026-10-30

La implementación principal está activa en `Assets/ScriptableObjects/VisualPrefabConfig.asset`. El usuario pidió cerrar lo posible y resumir; el plan completo no se considera terminado.

Completado:
- Cuatro prefabs de tropas ligeros generados y registrados, conservando los originales para edición.
- Rigs ligeros local/remoto con catálogo de mallas compartidas y montaje único de la apariencia seleccionada; arma/hitboxes permanecen en el rig.
- Snapshot copiado del ticket al contenedor de batalla y al héroe; respawn conserva el componente. Remotos reciben una copia independiente.
- Bloqueo central de equipar/desequipar/intercambiar durante combate y retirada del actualizador dinámico de equipo del héroe ligero.
- Regeneración manual desde el menú de editor y comprobación de equivalencia de cuatro tropas y 68 apariencias de héroe.
- 309/309 EditMode, 7/7 regresiones PlayMode de movimiento/respawn y perfil renderizado aprobado. La corrección final del bootstrap se verificó repitiendo el perfil; no se repitió toda la suite EditMode después de ese último cambio.
- El bootstrap ahora establece Combate en el mundo que acepta el ticket: la escena de pruebas creaba un mundo nuevo y perdía la fase asignada al anterior, lo que seleccionaba los prefabs originales.

Evidencia: `Logs/BattleVisuals/validation.txt`, `baseline-rendered-profile.json`, `lightweight-rendered-profile.json` y censos asociados. Los censos se toman al inicio del perfil, antes de esperar a la segunda escuadra: 9.332→218 SkinnedMeshRenderer cargados, con 218 activos en ambos. No son el conteo final de los 24 soldados. El perfil posterior sí recoge 300 frames con 24 unidades y dos héroes; main thread p50 4,8846→4,5246 ms, p95 5,5734→5,3987 ms. Una ejecución por versión en Editor, Burst desactivado: no demuestra una ganancia general de FPS, memoria ni rendimiento en Player.

Pendiente para completar todo el plan: perfiles repetidos con cargas masivas en Player, medición de memoria/spawn, cobertura visual de animación más extensa, validación de build, detección automática de salidas obsoletas y prueba de regeneración idempotente. Las variantes de unidades y cambio de arma siguen siendo extensiones futuras, no están habilitadas.

## Objetivo y reglas confirmadas

Reducir objetos, componentes y coste de creación de los personajes en batalla, conservando apariencia, animación, combate, personalización fuera de batalla y arquitectura híbrida.

- La apariencia corporal del héroe es fija durante toda la batalla, incluidos respawn y paso por supply points. Regla confirmada por el usuario; no queda pendiente de investigación.
- Cambiar el arma podría incorporarse en el futuro. Mantener el arma separada; este trabajo no implementa ni habilita esa mecánica.
- Todas las unidades actuales tienen apariencia fija en batalla.
- Las futuras excepciones de unidades cambiarán entre unas pocas configuraciones prearmadas, aproximadamente tres. Es una lista de datos, no un límite hardcodeado.
- Las mejoras de equipo o cambios de personalización fuera de batalla se reflejan en la siguiente entrada a batalla.
- Un personaje de batalla instancia solamente la geometría necesaria para su apariencia actual. El catálogo completo no forma parte de cada instancia.

## Evidencia y límites de partida

- `ModularHeroVisual.prefab`, registrado como `Player` en `Assets/ScriptableObjects/VisualPrefabConfig.asset`, contiene 820 GameObjects y 692 SkinnedMeshRenderer serializados. Hay 672 GameObjects con activeSelf desactivado. No son conteos de renderers visibles en ejecución ni mediciones de memoria.
- Los renderers tienen rootBone y arrays de huesos diferentes; las longitudes observadas van de 1 a 18. Cambiar sharedMesh sin resolver sus bindings no es una conversión segura.
- Los campos boneTarget del catálogo también identifican grupos de variantes; no se deben confundir con el array de huesos de skinning de cada renderer.
- HeroVisualAppearanceSystem aplica partes base y equipo; HeroVisualEquipmentInitializer reacciona a cambios del inventario. Las rutas de equipar/desequipar revisadas no bloquean cambios por fase de batalla.
- SupplyPointUIController gestiona cambio de escuadra; no se encontró allí una operación de equipo del héroe.
- SquadVisualManagementSystem instancia un prefab registrado por unidad. VisualPrefabConfiguration ya declara variantes, pero no constituye por sí mismo una implementación de selección/transición.
- AvatarPartSelector depende de grupos de hijos e índices. Conservar inicialmente ese montaje para creación/personalización fuera de batalla.
- El último intento de Unity CLI detectó Unity 6000.5.8f1 y Pipeline instalado, pero el servidor local no respondió. Falta medición en vivo. No usar el contexto histórico de Unity 2022 como versión efectiva sin comprobar ProjectVersion y paquetes.
- Actualización: Unity CLI conecta fuera del sandbox; el Editor estaba en LoginScene sin Play. Inspección mediante AssetDatabase de la configuración `Assets/ScriptableObjects/VisualPrefabConfig.asset`: Squires y Spearmen tienen 844 transforms y 720 SkinnedMeshRenderer cada uno; LevyArchers 842 y 720; Pikemen 814 y 692. Los conteos incluyen objetos inactivos y no miden rendimiento ni visibilidad en batalla. Pendiente confirmar la configuración usada por cada escena y obtener el perfil en Player.
- Los assets de malla/material pueden compartirse entre personajes; la jerarquía y sus componentes sí se multiplican por instancia. No extrapolar el tamaño del prefab en disco a RAM ni prometer FPS.

## Reutilización prevista

| Elemento existente | Ubicación | Decisión |
|---|---|---|
| HeroDataService, PlayerSessionService, ItemService | Assets/Scripts/Core/ | Reutilizar identidad, sesión y resolución de ítems. No duplicar servicios de inventario. |
| HeroData, BattleHeroData y HeroAppearanceComponent | Assets/Scripts/Data/ y Assets/Scripts/Hero/Components/ | Reutilizar el contrato de entrada a batalla; capturar valores de apariencia independientes del estado mutable de sesión. |
| AvatarPartDatabase, AvatarPartDefinition, VisualAttachment | Assets/Scripts/Data/ | Extender/reutilizar para resolución de IDs; generar datos de render y bindings sin renombrar IDs existentes. |
| AvatarVisualUtils | Assets/Scripts/UI/avatarCreator/AvatarVisualUtils.cs | Reutilizar reglas de selección; desacoplar la resolución de piezas de SetActive sobre el catálogo completo. |
| VisualPrefabConfiguration y VisualPrefabRegistry | Assets/Scripts/Hero/ | Registrar rigs/prefabs ligeros y variantes; conservar la entrada actual de spawn. |
| HeroVisualInstantiationSystem, HeroVisualAppearanceSystem | Assets/Scripts/Hero/Systems/ | Extender el montaje inicial del héroe y la marca de apariencia aplicada. |
| SquadVisualManagementSystem | Assets/Scripts/Squads/Systems/ | Mantener spawn/vínculo, usando prefabs ligeros generados. |
| HeroVisualInstance, UnitVisualInstance, UnitVisualReference | Assets/Scripts/Hero/ y Assets/Scripts/Squads/ | Reutilizar identidad y referencias. Evitar un segundo registro global de personajes. |
| VisualSyncUtility, EntityVisualSync | Assets/Scripts/Shared/ y Assets/Scripts/Visual/ | Conservar vínculo, movimiento y autoridad actuales. |
| EquipmentManagerService, InventoryEventService | Assets/Scripts/Inventory/Services/ | Aplicar la regla de batalla en las operaciones existentes y conservar transacciones. |
| HeroVisualEquipmentInitializer | Assets/Scripts/Hero/Components/ | Evitar suscripción/refresco corporal en batalla; conservar comportamiento fuera de batalla. |
| ModularCharacterManager y herramientas Synty | Assets/Scripts/SyntyEditorTools-master/ | Conservar como herramientas de autoría; no transportar sus listas completas al prefab ligero. |
| Perfiles/regresiones de batalla existentes | Assets/Tests/ y herramientas existentes de perfil | Extender escenarios relevantes para comparación, sin crear otro framework de pruebas. |

Recomendación: extender la cadena actual. Crear únicamente los datos generados y la herramienta de extracción/montaje que no tengan equivalente, tras una revisión de reutilización específica en implementación. No crear un nuevo sistema ECS por cada variante.

## Diseño objetivo

### Catálogo de autoría y representación de batalla

Conservar los prefabs modulares completos como fuente de edición. Una herramienta de editor extrae y valida las piezas y produce recursos de batalla que no referencien el prefab completo ni componentes con listas de todas las variantes.

Una pieza deformable requiere malla compartida, materiales compartidos, rutas estables de huesos en orden, rootBone, transform local, bounds y propiedades relevantes del renderer. Preservar bindposes, blendshapes y ajustes de render que existan. Resolver huesos por ruta respecto al rig, evitando coincidencias ambiguas por nombre.

Para piezas rígidas conservar punto de montaje y transform. Armas, escudos, hitboxes y accesorios con lógica requieren extracción explícita de componentes y referencias; no convertirlos ciegamente a una malla.

### Tropas fijas

Generar en editor un prefab por configuración/variante realmente utilizada. Contendrá el rig y la geometría seleccionada, junto a los componentes de gameplay/presentación necesarios. Instanciar directamente este prefab en batalla; no clonar el catálogo completo para destruir después los hijos sobrantes.

Conservar huesos, ancestros, sockets y objetos referenciados por animaciones, rigging, colliders, efectos y scripts aunque estén inactivos. Revisar referencias serializadas tras la generación. Los originales permanecen como fuente de autoría.

### Héroes

Al aceptar la entrada de cada héroe, capturar una descripción inmutable de género, personalización y equipo corporal. Preferir extender los DTO existentes; copiar los valores necesarios, sin conservar referencias mutables a Equipment o AvatarParts de la sesión.

Resolver piezas base + personalización + equipo una vez, con precedencia explícita equivalente al comportamiento actual. Instanciar el rig ligero local/remoto y montar únicamente las piezas finales. Reutilizar esa descripción durante respawn y para héroes que se incorporen más tarde. Invalidarla al terminar la batalla.

El montaje se completa antes de exponer el visual como listo y antes de localizar/vincular hitboxes. Evitar que la marca de apariencia aplicada se añada si faltan datos o falla el montaje. No reiniciar el Animator después de empezar a animar ni dejar un frame con el cuerpo incompleto.

El arma/escudo queda en un módulo separado con sus puntos de montaje y vínculo de propietario. Mantener las reglas actuales de combate; una futura sustitución de arma deberá actualizar de forma coordinada hitboxes, escudo, clase/habilidades y animación. Esa sustitución queda fuera de este plan.

### Unidades con variantes futuras

Identificar excepciones por una lista explícita de configuraciones permitidas en la definición, reutilizando VisualPrefabConfiguration donde encaje. Una sola configuración implica apariencia fija; no inferir capacidad por nombre, tipo de malla o cantidad de hijos.

Reservar la posibilidad de cambiar la geometría conservando entidad, rig, Animator y vínculo. No crear todavía un sistema de transición sin ninguna unidad que lo necesite. Al implementarlo, el estado autorizado vendrá de ECS y se aplicará únicamente al cambiar la variante. Precargar recursos compartidos si la transición lo requiere, sin instanciar todas las variantes por soldado.

## Fases de implementación

### Fase 0 — Inventario y baseline

- [ ] Restablecer conexión del editor por Unity CLI; confirmar versión y escena. Si sigue inaccesible, registrar el bloqueo sin presentar conteos estáticos como perfil real.
- [ ] Censar prefabs efectivos de tropas, héroe local/remoto, dependencias, renderers activos, rig, materiales, hitboxes y referencias a variantes.
- [ ] Elegir una tropa representativa para piloto y una escena reproducible con cámara, resolución, iluminación y carga fijas.
- [ ] Registrar baseline de creación, memoria, conteo de objetos/componentes, render y CPU/GPU. Aprovechar los fixtures existentes.

### Fase 1 — Contrato fijo de batalla

- [ ] Capturar y conservar la descripción de apariencia por héroe de batalla, incluidos remotos y entradas tardías.
- [ ] Aplicar bloqueo de cambios de equipo corporal en los servicios existentes: equipar, desequipar, intercambiar y operaciones indirectas que retiren armadura por incompatibilidad de arma. Rechazar antes de mutar/guardar; mantener lectura del inventario.
- [ ] Reflejar la restricción en UI y liberar el bloqueo al salir de batalla, incluso en rutas de salida/cancelación.
- [ ] Mantener deshabilitada la futura excepción de cambio de arma hasta que exista su implementación completa.
- [ ] Hacer que el visual corporal de batalla consuma la descripción capturada, no cambios posteriores de SelectedHero; conservar actualización de equipo fuera de batalla.

### Fase 2 — Generación y tropa piloto

- [ ] Implementar extracción validada desde autoría y generación reproducible de prefab ligero, con informe de referencias inválidas/piezas ausentes.
- [ ] Preservar GUID de salidas existentes al regenerar, definir rutas y detectar cambios de la fuente para evitar salidas obsoletas.
- [ ] Generar la tropa piloto, registrar la alternativa mediante VisualPrefabRegistry y comparar contra el original.
- [ ] Verificar animación, navegación, ataque, hitboxes, daño, muerte, desaparición, sombras, materiales y bounds fuera de cámara.
- [ ] Medir el piloto antes de convertir el resto; mantener los originales disponibles para reversión durante desarrollo.

### Fase 3 — Héroe ligero

- [ ] Generar catálogo de piezas y rigs ligeros local/remoto desde la fuente existente.
- [ ] Resolver grupos visuales y solapamientos con resultado determinista; validar ambos géneros y equipo compuesto.
- [ ] Adaptar sistemas existentes para montar solamente las piezas de la descripción de batalla, con arma separada.
- [ ] Eliminar el refresco corporal por eventos de inventario en batalla y búsquedas globales de GameObjects donde ya existe un vínculo directo reutilizable.
- [ ] Validar respawn, héroes remotos/IA, entrada tardía, preview del héroe en batalla y vuelta al creador/selección fuera de batalla.
- [ ] Gestionar liberación de referencias al salir; cualquier caché se limita a recursos/descripciones compartidos y no retiene clones completos ni crece entre batallas.

### Fase 4 — Resto de tropas y contrato de variantes

- [ ] Convertir los prefabs realmente registrados y usados, con validación por tipo de unidad.
- [ ] Documentar cómo añadir una configuración fija o una lista de variantes prearmadas.
- [ ] Detectar configuraciones incompletas antes de entrar en batalla. No ocultar errores cargando silenciosamente el prefab enorme como fallback de producción.
- [ ] Mantener el montaje de autoría y las herramientas de edición funcionales.

### Fase 5 — Validación y adopción

- [ ] Pruebas de contratos: bloqueo transaccional, snapshot independiente de sesión, resolución de piezas y bindings, regeneración idempotente y ausencia de referencias al catálogo completo en prefabs ligeros.
- [ ] Regresiones PlayMode relevantes: spawn/respawn local/remoto, tropas, combate, animación, preview y salida de batalla. Usar perfiles temporales; no modificar partidas guardadas reales.
- [ ] Comparación visual de configuraciones representativas y casos de casco/pelo/barba, piezas compuestas, armas y escudos.
- [ ] Perfil A/B en Player con las mismas condiciones y cargas crecientes; separar carga fría de spawn con recursos calientes. Registrar varias muestras y percentiles de tiempo de frame/spawn, memoria, GC, renderers y draw calls.
- [ ] Adoptar por tipo únicamente después de demostrar equivalencia funcional y reducción de objetos/componentes y coste de memoria/creación. Investigar cualquier regresión sostenida de tiempo de frame; no fijar ganancias de FPS sin baseline.
- [ ] Actualizar ModeloHybrido, configuración de prefabs, SystemResponsibilities y guía de armaduras. Registrar patrones de errores corregidos en tasks/lessons.md y ejecutar graphify update tras cambios de código cuando corresponda.

## Criterios de aceptación

1. Cada visual de batalla contiene solo las piezas actuales y los nodos necesarios para su funcionamiento. No incluye cientos de alternativas inactivas ni listas que obliguen a conservarlas.
2. La apariencia corporal del héroe permanece idéntica durante batalla y respawn aunque cambien objetos de sesión externos; las operaciones prohibidas no alteran inventario, stats ni guardado.
3. Personalización y equipo fuera de batalla conservan sus IDs y funcionamiento. No se requiere migrar partidas guardadas por este cambio visual.
4. Animación, rig, movimiento, autoridad ECS/Sync/GO, hitboxes y efectos conservan comportamiento.
5. Una configuración de tropa se prepara una vez en editor; un héroe se resuelve al entrar. No hay polling de apariencia ni reconstrucción corporal por frame.
6. Las variantes futuras usan el mismo catálogo y una lista explícita; no necesitan arrastrar todas las piezas del catálogo a cada unidad.
7. La mejora se demuestra con mediciones comparables y limitaciones declaradas. Menos objetos no se presenta automáticamente como menos draw calls o más FPS.

## Fuera de la primera implementación

- Cambio de arma durante batalla y unidades nuevas con transición de variante.
- Combinación de mallas, atlas nuevos, GPU skinning, cambio de sistema de animación o migración de personajes a otro renderer.
- Addressables o streaming como requisito. La primera mejora elimina instancias innecesarias; el catálogo compartido todavía puede mantener recursos cargados. Optimizar residencia de recursos se decidirá con el perfil.
- Pooling general nuevo: se evaluará solo si el perfil de spawn/respawn lo justifica.

Orden recomendado: baseline → contrato fijo → tropa piloto → héroe ligero → resto de tropas → validación y adopción. La combinación de mallas/LOD queda como fase posterior basada en evidencia.

## Avance de implementación — 2026-10-29

- Escrito bloqueo central de equipar/desequipar/intercambiar durante GamePhase.Combate en EquipmentManagerService; HeroVisualEquipmentInitializer evita refrescar el cuerpo en esa fase. Pendientes pruebas de transacción, UI y límites de fase.
- Añadido BattleVisualPrefabBuilder para generar copias de tropas y conservar referencias serializadas y bindings de animación. No se han sustituido referencias del registro.
- Generación ejecutada mediante Unity CLI: Squires 844→141 transforms, 720→17 SkinnedMeshRenderer; LevyArchers 842→135, 720→13; Spearmen 844→138, 720→14; Pikemen 814→135, 692→13.
- Copias en Assets/Prefabs/BattleVisuals/. Esto acredita reducción estructural; no acredita equivalencia visual, gameplay ni mejora de FPS.
- Añadida comprobación de geometría activa, materiales, huesos y cantidades de colliders/hitboxes; pendiente ejecutar tras importación y completar validación en PlayMode/Player.
- Pendientes snapshot/montaje ligero del héroe, contrato completo de bloqueo, adopción de prefabs, regresiones y perfil A/B. El plan no está completo.
