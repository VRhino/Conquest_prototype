# Revisión de sistemas principales e interacciones

> **Registro histórico (2026-10-13).** Describe el estado y las decisiones de esa fecha; no se reescribe. Para el estado vigente, ver [Arquitectura actual](1_Arquitectura_Actual.md) y, para movimiento, [TroopMovementPipeline](../TroopMovementPipeline.md). Los nombres de sistemas o valores citados aquí pueden haber cambiado.

- [x] **R01 — Hecha:** cerrar la IA al terminar la partida y permitir otra partida en el mismo World.
- [x] **R02 — Hecha:** impedir ataques de unidades muertas y limpiar hitboxes al finalizar.
- [x] **R03 — Hecha:** localizar al tirador mediante registro, sin buscar todos los visuales por proyectil.
- [x] **R04 — Hecha:** cachear las capacidades del Animator.
- [x] **R05 — Hecha:** reutilizar candidatos y mapas de targeting; validar objetivos sin recorrer toda la lista.
- [x] **R06 — Hecha:** fijar el orden y la frescura de detección, decisiones, captura y ataques.
- [x] **R07 — Hecha:** reutilizar listas visuales y evitar actualizaciones sin trabajo pendiente.
- [x] **R08 — Hecha:** reutilizar contactos melee y crear eventos de daño con su arquetipo final.
- [x] **R09 — Hecha:** el ancla publica revisión y la geometría estable se omite con clave de orden/estado, formación, slot, escuadrón y terreno; el perfil de batalla completa pasa.
- [x] **R10 — Hecha:** cachear destinos IA proyectados y evitar ResetPath repetido.
- [ ] **R11 — Parcial:** Burst y batalla 10 contra 10 validados en Editor; falta perfil Player/GPU y mayor densidad.
- [x] **R12 — Hecha:** reutilizar colecciones de streaming y resolver vecinos por coordenadas.
- [x] **R13 — Hecha:** A* disperso y reutilizable, con presupuesto y cancelación durante toda la búsqueda.
- [x] **R14 — Hecha:** agrupar notificaciones de equipo, actualizar caché al guardar y evitar rebinding de celdas sin cambios.
- [x] **R15 — Hecha:** desactivar por defecto la adaptación de entorno experimental.
- [ ] **R16 — Parcial:** GUID, guardado consolidado y XP persistente implementados; quedan registros heredados sin dueño identificado.

**Estado: 14 hechas y 2 parciales.** R11 espera perfil Player/GPU y mayor densidad; R16 espera atribución explícita de registros heredados. «Hecha» no significa que la ganancia de FPS esté medida.

Actualizado: **2026-10-15**. Base de revisión: `22c6a785` más cambios locales sin commit. Se conservaron los cambios previos del workspace.

## Alcance

Se revisaron las interacciones principales de batalla, visuales, IA, navegación, campaña, inventario y persistencia, partiendo de un inventario de 77 tipos ECS. El alcance no cubre cada método, prefab o asset del proyecto.

Las correcciones mantienen la separación ECS / Sync / GameObject y reutilizan los servicios existentes. Los detalles de ejecución permanecen en [el plan de trabajo](../../tasks/system-optimization-plan.md); los contratos actuales están en [Modelo híbrido](../ModeloHybrido.md) y [responsabilidades de sistemas](../../Assets/Scripts/Squads/SystemResponsibilities.md).

## Qué cambió y qué queda

| R | Problema detectado | Cambio aplicado y límite |
|---|---|---|
| R01 | La IA podía conservar estado activo fuera de batalla. | Se publica la fase inactiva y se detienen decisiones, intención y agentes. Bootstrap rearma la siguiente partida en el mismo World. |
| R02 | Un atacante muerto podía iniciar un ataque. | Se excluyen muertos, se ordena su procesamiento y se limpian hitboxes al terminar la partida. |
| R03 | Cada proyectil buscaba visuales por toda la escena. | Registro por World/Entity con desregistro; se reutiliza la información de colliders recogida al crear el visual. |
| R04 | Se consultaban parámetros del Animator por unidad/frame. | Caché de parámetros con invalidación cuando cambia el controller. |
| R05 | Validación y reparto de objetivos repetían recorridos y reservas. | Conjunto de candidatos válidos, recuento por escuadrón y mapas reutilizados. Buscar el objetivo más cercano aún cuesta O(unidades × candidatos) cuando todos necesitan uno nuevo. |
| R06 | Algunos consumidores no tenían un contrato claro de orden. | Orden explícito entre muerte, detección, snapshot, decisiones, captura, tracker, pose y ataques. El snapshot usa estado táctico previo y detección/salud actuales. |
| R07 | Gestión visual reservaba listas incluso sin instancias pendientes. | Listas reutilizadas y actualización condicionada a trabajo pendiente. Presupuestar spawn queda como opción si un perfil demuestra picos. |
| R08 | Melee reservaba un diccionario y añadía componentes por contacto. | Colección reutilizada y creación directa del evento final; DamageCalculation mantiene acceso al daño en el mismo frame. |
| R09 | Formación repetía geometría y consultas de altura. | Caché por slot; ancla revisionada y clave por escuadrón/estado/orden/formación/slot permiten omitir la geometría estable. La invalidación de terreno conserva el remuestreo de altura. Perfil de batalla completa pasa; no mide el coste aislado de GridFormationUpdate. |
| R10 | La IA reproyectaba destinos estables y repetía ResetPath. | Caché con revalidación periódica usando la cadencia existente; ResetPath sólo cuando hay ruta o cálculo pendiente. |
| R11 | Bodyblock se encarecía con muchos agentes en pocas celdas. | Job paralelo Burst por agente, memoria nativa persistente y lote/umbral configurables. Mantiene todos los contactos; el peor caso denso sigue siendo cuadrático. El perfil 20 contra 20 en Editor se repitió; falta Player/GPU y más densidad. |
| R12 | Streaming reconstruía colecciones y buscaba vecinos repetidamente. | Colecciones reutilizadas, índice por coordenadas y actualización por lote al liberar. Se revalida el resultado de cargas asíncronas. |
| R13 | A* inicializaba toda la rejilla antes de repartir trabajo. | Contextos dispersos reutilizados por búsqueda; presupuesto también en reconstrucción/simplificación y Dispose explícito al cancelar. |
| R14 | Cambios de inventario amplificaban notificaciones y refrescos. | Evento de equipo agrupado por transacción, caché derivada actualizada antes de notificar y celdas iguales sin rebinding. Se conserva rollback y guardado; cambiar sesión invalida la caché. |
| R15 | La adaptación experimental hacía cálculos sin consumidor activo. | Configuración opt-in, desactivada por defecto; se conserva la limpieza de Brace. |
| R16 | Identidad por nombre y dos archivos de progresión permitían inconsistencias. | GUID de héroe/instancia, player_save.json v3 y progresión confirmada antes de consumir XP. El bloque heredado se conserva sin atribuir dueño por suposición. |

### R16: identidad y progresión

La identidad GUID llega al guardado, caché, atributos temporales y bootstrap. El fallback por nombre sólo acepta coincidencias únicas y no contradice un ID explícito. Cargar sigue siendo una operación de lectura; bootstrap confirma IDs nuevos con backup antes de recompensas.

La XP de héroes usa el dueño capturado al iniciar la batalla. La XP de escuadrones verifica tanto dueño como GUID de instancia: cambiar la selección o reciclar un índice no redirige recompensas. Cada evento se consume después de guardar; si falla otro evento del lote, los anteriores no se repiten. Las revisiones de progresión protegen nivel/XP frente a snapshots viejos de inventario, conservando cambios de formación y efectivos.

player_save.json versión 3 conserva lectura de versiones 0/1/2, escrituras atómicas, backup y bloqueo compartido. Una vez existe el perfil, las escrituras van al archivo canónico. player_progress.json se conserva y sus datos antiguos quedan en battleProgress. Los paneles usan nivel, XP restante, umbrales y máximo compartidos con la lógica de progresión.

**Pendiente:** identificar la propiedad de los registros antiguos antes de convertirlos al modelo de héroe/instancia. Su conservación evita pérdida de datos, pero no completa esa migración.

## Validación realizada

| Comprobación | Resultado |
|---|---|
| EditMode | **131/131** pruebas pasan. |
| PlayMode | **30/30** pruebas pasan. |
| Compilación de scripts Player Windows | **69 assemblies** compilados. |
| Migración sobre copia del perfil real | Ocho héroes conservados, progresión antigua intacta y backup creado. Los archivos reales permanecen intactos. |
| Grafo actualizado | 7.783 nodos, 15.055 relaciones y 419 comunidades. |

Resultados: [EditMode](../../Logs/system-optimization-editmode.json), [PlayMode](../../Logs/system-optimization-playmode.json), [confirmación Player](../../Logs/ui-flow-player-confirmation.json) y [grafo](../../graphify-out/GRAPH_REPORT.md).

El 2026-10-14 se repitieron las siete regresiones funcionales de BattleTestScene: **7/7 pasan** (movimiento/órdenes, daño/muerte/respawn, bloqueo, animaciones y equipo visual). El Editor recuperó su escena anterior sin cambios pendientes. [Resultado](../../Logs/battletest-behavior-2026-10-14.json). Esta repetición no incluye una prueba masiva ni una medición nueva de rendimiento.

### Mediciones y sus límites

- **Bodyblock aislado, 900 agentes densos:** 14,90 ms secuencial y 4,18 ms paralelo con lote 32. Caso disperso paralelo: 1,44 ms. Asignación administrada medida en el hilo principal: 0 bytes. Son muestras de un frame, no una comparación estadística ni una medición de FPS.
- **BattleTestScene renderizada en Editor:** 300 frames a 1920×1080, 24 unidades y dos héroes. p95 del hilo principal: 21,63 ms; bodyblock: 0,088 ms; detección: 0,043 ms. El GC global incluye Editor y runner.
- **BattlePrepScene → BattleScene, perfil actualizado el 2026-10-15:** 1/1 prueba explícita pasa. 20 héroes, 240 unidades iniciales y 228 al terminar; 600/600 frames con contacto. p95 en Editor: hilo principal 18,665 ms, bodyblock 0,3331 ms y detección 1,0737 ms; GC p50 127.155 B y p95 182.877 B. Esta muestra verifica el flujo con R09 activo, pero no aísla GridFormationUpdate ni se puede comparar directamente con la captura anterior: cambió la densidad de contactos. Sin afirmación de mejora global.

No hay baseline renderizado comparable ni perfil Player/GPU. Estas mediciones no permiten atribuir una mejora global de FPS. Evidencia: [tiempos aislados](../../Logs/system-optimization-bodyblock-timing.txt) y [perfil renderizado](../../Logs/system-optimization-rendered-profile.json).

### Batalla completa: BattlePrepScene → BattleScene

El flujo real se probó con **10 contra 10 héroes**, cada uno con un escuadrón activo de 12 soldados: **20 héroes y 240 unidades**. Se verificaron el botón de inicio de preparación, los equipos, el escuadrón de cada héroe, la orden HoldPosition y contactos autónomos entre equipos. Después pasan de nuevo 131/131 EditMode, 28/28 PlayMode y la compilación Player de 69 assemblies.

Dos fallos encontrados y reparados al usar la escena real:

- Faltaba ShieldConfigAuthoring en su subescena DOTS; las tropas con escudo no podían crear su visual. Se añadió mediante el Editor con los valores existentes del authoring.
- La selección de posición comparaba Ground como tag, aunque está definido como layer. Ahora comprueba la capa y TerrainCollider, conservando el fallback.

Perfil de **600 frames renderizados**, 1920×1080, con contacto en **275 frames**. Máximo: 78 pares candidatos y 27 pares en contacto. p95: bodyblock **0,3244 ms**, detección **1,0366 ms** e hilo principal **22,7345 ms**. La muestra terminó con 20 héroes y 240 soldados.

Esto valida el flujo y mide un choque real moderado en Editor. No representa el extremo de 900 agentes comprimidos ni una comparación antes/después; el GC incluye Editor/runner y falta Player/GPU. [Prueba completa](../../Logs/system-optimization-full-battle-result.json) y [perfil](../../Logs/system-optimization-full-battle-profile.json).

La ejecución explícita se reutiliza en BattleSceneIntegrationTests.FullBattlePreparationRenderedPerformanceProfile. N está en FullBattleHeroesPerSide (actualmente 10); el creador recibe N−1 atacantes remotos y N defensores porque añade al héroe local aparte. Sólo hay un escuadrón activo por héroe en esta muestra, no todas sus reservas simultáneamente.

### Prioridad del flujo UI: cursor y regreso desde PostBattle

La transición desde Feudo dejaba el cursor global bloqueado en BattlePrepScene. La política compartida ahora libera y muestra el cursor al terminar la carga de una escena de interfaz; cámara, paneles y supply respetan esa política al cerrar. La regresión entra con cursor Locked y verifica que un click real abra el selector de equipo y que cerrar el menú conserve el cursor libre. [Prueba de cursor](../../Logs/ui-flow-preparation-cursor.json).

PostBattleScene incorpora un botón **Salir** inferior, reutilizando MatchmakingUI. MatchmakingService captura la escena al aceptar la cola; Salir vuelve a ella, limpia el estado Assigned y conserva el héroe seleccionado, permitiendo otra cola. La prueba verifica PostBattle → Feudo y un nuevo inicio de matchmaking. Esto restaura la **escena de origen**; no captura la posición exacta del héroe antes de la cola. La pantalla completa de resultados sigue pendiente. Validación final: **131/131 EditMode, 30/30 PlayMode y 69 assemblies Player**; grafo actualizado. [Prueba de retorno](../../Logs/ui-flow-postbattle-return.json).

## Siguiente trabajo

1. **R11:** llevar el escenario 10 contra 10 ya validado a Player y aumentar densidad, con escenario repetible y percentiles; separar simulación, NavMesh/física, animación y GPU. Decidir nuevas optimizaciones según el coste observado.
2. **R16:** definir cómo identificar al dueño de los registros heredados y migrarlos con backup y pruebas de compatibilidad. Los registros ambiguos necesitan una regla explícita.

Índice espacial para targeting y presupuesto de spawn quedan como opciones condicionadas a mediciones, sin considerarlos correcciones obligatorias ya demostradas.
