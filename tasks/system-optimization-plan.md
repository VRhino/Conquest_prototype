# Reparación y optimización de sistemas — 2026-10-13

Base: revisión R01–R16 en Docs/Arquitectura/19_Revision_Sistemas_Optimizacion_2026-10-13.md.
Conservar cambios locales preexistentes. No cambiar políticas de combate ni formatos persistentes sin migración verificada.

- [x] Fases de batalla, muerte y frescura de snapshots (R01/R02/R06).
- [x] Lookup de proyectiles, capacidades Animator, targeting y scratch visual/melee (R03/R04/R05/R07/R08).
- [x] Cache de formación y comandos IA, adaptación experimental y medición bodyblock (R09/R10/R11/R15).
- [x] Reconciliación de sectores, presupuesto A* y refrescos precisos de inventario (R12/R13/R14).
- [x] Revisar identidad/persistencia y definir solución compatible (R16).
- [x] Pruebas de regresión, compilación, grafo, documentación y lessons.

Registrar aquí evidencia de validación y límites que requieran perfil renderizado.

## Implementación y validación

- Reparaciones R01–R15 implementadas; R07 conserva spawn sin presupuesto hasta disponer de perfil real.
- R16: identidad persistente implementada con GUID y fallback heredado sólo para coincidencias únicas. En la continuación se consolida almacenamiento en versión 1: bloque battleProgress separado, importación compatible al guardar y archivo original conservado. Sigue sin inferirse el dueño antiguo.
- Editor real: Unity 6000.5.8f1, Entities 6.5.0; el contexto histórico de 2022.3/Entities 1.3 no representa el proyecto actual.
- 117/117 pruebas EditMode y 28/28 PlayMode pasan después del job paralelo.
- Player Windows: 69 assemblies compiladas; éxito confirmado por `ARCHITECTURE_PLAYER_SCRIPTS_OK` en la consola del Editor, aunque la llamada RPC agotó sus 5 segundos de espera. Evidencia en `Logs/system-optimization-player-confirmation.json`.
- Resultados: `Logs/system-optimization-editmode.json` y `Logs/system-optimization-playmode.json`.
- Bodyblock: job por agente sin escrituras compartidas; tamaño de lote y umbral para paralelizar expuestos en SquadSpawnConfigAuthoring. Memoria persistente liberada al destruir el sistema.
- La primera comparación aislada de 900 agentes concentrados fue 14,90 ms con job secuencial frente a 5,81 ms paralelo; mediciones de un frame, no benchmark estadístico ni FPS renderizados. La repetición con lote 32 pasó: 4,18 ms denso, 1,44 ms disperso y 0 bytes administrados en el hilo principal; `Logs/system-optimization-bodyblock.json` y `Logs/system-optimization-bodyblock-timing.txt`.
- Diff de los archivos de implementación sin errores de whitespace; conservados cambios locales preexistentes.

- `graphify update .` completado: 7.699 nodos, 14.649 relaciones y 423 comunidades; actualizados graph.json/graph.html/GRAPH_REPORT.md. Etiquetas de comunidades reajustadas por hubs, sin extracción semántica adicional mediante LLM.
- Documentación de arquitectura/responsabilidades/modelo híbrido y patrones en `tasks/lessons.md` actualizados.

## Límites

- Perfil renderizado de dos squads completado en Editor (300 frames, 24 unidades, dos héroes); pendientes Player/GPU, carga masiva y baseline comparable. No se prometen ganancias de FPS.
- Almacenamiento consolidado; pendiente convergencia del modelo por héroe: el formato heredado no permite atribuir toda la progresión a un dueño sin resolver conflictos de identidad/datos.

## Continuación: persistencia y perfil de batalla

- [x] Reutilizar LocalSaveProvider/ProgressFileStore para consolidar el bloque de progreso sin asignarle un héroe por inferencia.
- [x] Probar importación, idempotencia, backup/corrupción, versiones futuras y escrituras intercaladas.
- [x] Perfilar el fixture de BattleTestScene renderizado; registrar escenario, unidades, percentiles y límites del Editor.
- [x] Validar suites/Player, actualizar grafo, informe y lessons.

Evidencia de la continuación:

- 117/117 EditMode; seis regresiones nuevas de consolidación y recuperación sin sobrescribir datos desconocidos.
- Copia del perfil real migrada en `Temp/SystemOptimizationMigrationProbe`: ocho héroes iguales, original de progresión intacto, versión 1 y backup. Resultado: `Logs/system-optimization-migration-real-copy.json`.
- Perfil explícito de BattleTestScene: 300 eventos de render, 1920×1080, 24 unidades y dos héroes; p95 hilo principal 21,6345 ms, repulsión 0,088 ms y detección 0,043 ms.
- GC global p50 61.259 bytes y p99 745.710 bytes: incluye Editor y runner; requiere atribución en Player antes de responsabilizar un sistema. Resultado: `Logs/system-optimization-rendered-profile.json`.
- Fixture de batalla ahora inyecta ProgressFileStore temporal; no utiliza la progresión del usuario. El perfil se ejecuta separado mediante `--filter BattleSceneRenderedPerformanceProfile --include_explicit true`.

- Suite final: 117/117 EditMode, 28/28 PlayMode, 1/1 perfil explícito; Player Windows recompilado con 69 assemblies confirmadas en consola. La petición RPC agotó su espera, la compilación finalizó correctamente.
- Comparación binaria posterior: player_save.json y player_progress.json reales coinciden con las copias originales; la validación no los modificó.

- Grafo final regenerado: 7.720 nodos, 14.752 relaciones y 420 comunidades. Documentación v1.5 y lessons actualizados.

## Continuación: destinatario de XP e identidad temporal

- [x] Capturar identidad persistente en el componente de progresión al aceptar bootstrap.
- [x] Eventos XP con destinatario explícito y commit sobre HeroData, sin tocar progreso antiguo sin dueño.
- [x] Proteger progreso reciente frente a snapshots antiguos de inventario; refrescar sesión/caché tras guardar.
- [x] Corregir comprobaciones de atributos temporales por GUID; probar homónimos, cambio de selección y segunda partida.
- [x] Validar suites, Player, documentación y grafo.

- Seis regresiones nuevas: GUID temporal/homónimos, dueño capturado con selección distinta, fallo de commit/reintento, IDs desconocidos/duplicados, segunda partida/ticket legado y guardado antiguo de inventario tras XP reciente. 123/123 EditMode pasan.
- Contenedor v2: lectura compatible de 0/1; progressRevision sólo gobierna campos de progreso. XPEventComponent debe especificar heroId; actualmente no hay productores runtime de ese evento en el repositorio, por lo que no se añade una política nueva de recompensas.

- Identidad antigua: JsonUtility genera GUIDs al leer campos ausentes; lectura de identidad serializada con BattleHeroData sin generador marca requiresIdentitySave. Bootstrap persiste el perfil antes de aceptar recompensas. Nueva regresión carga JSON sin GUID, verifica backup y recompensa correcta; 124/124 EditMode pasan.

Reutilización para esta continuación:

| Elemento | Uso | Decisión |
|---|---|---|
| HeroProgressComponent | Captura dueño persistente del bootstrap | Extendido; ningún sistema nuevo |
| HeroDataService | Búsqueda por ID único sin fallback por selección/nombre | Extendido |
| LocalSaveProvider/SaveSystem | Lock, versión, commit y protección de snapshot | Extendidos |
| HeroExperienceCalculator | Umbral floor del combate conservando constantes existentes | Extendido; eliminadas fórmulas duplicadas de sistemas |
| BattleHeroData | Inspección de ID serializado sin generador GUID | Reutilizado en wrapper privado de lectura dentro del proveedor |
| DataCacheService | Recalcular derivados cuando cambia el nivel | Reutilizado |

- Vista de experiencia corregida: Level persistido y XP restante; cálculo de umbral compartido con batalla. Regresión nivel 2/50 XP y floor de nivel 7. 125/125 EditMode pasan.
- Prueba de animación con espera fija produjo fallo transitorio; repetición aislada pasó y se reemplazó la espera por clip Run real con plazo, conservando aserciones de gait/velocidad/peso.

- Validación de copia real v2: ocho héroes preservados, requiresIdentitySave true→false al guardar, progresión antigua intacta y backup. `Logs/system-optimization-identity-real-copy.json`; no se usa el archivo real como destino.

- Validación funcional final de la continuación: 125/125 EditMode y 28/28 PlayMode, incluidas transición real de animación y nuevas reglas de bootstrap. Los dos archivos reales siguen iguales a sus copias originales tras la validación.

- Cierre de continuación: 125/125 EditMode, 28/28 PlayMode y 69 assemblies Player Windows confirmadas. Grafo regenerado: 7.736 nodos, 14.869 relaciones y 407 comunidades. Documentación v1.6 y lessons actualizados.

## Continuación: XP de escuadrones

- [x] Guardar nivel/XP en la instancia de HeroData por GUID de dueño e instancia.
- [x] Proteger snapshots viejos de inventario sin pisar formación/efectivos.
- [x] Confirmar cada evento antes de consumirlo; conservar eventos fallidos y evitar duplicar los ya confirmados en un lote.
- [x] Regresiones de dueño/GUID, reintento, múltiples eventos y siguiente bootstrap.
- [x] Suites, compilación Player, grafo, documentación y lessons.

Reutilización: SquadDataService para lookup, SquadInstanceData para revisión, SaveSystem para commit, HeroProgressComponent/SquadIdMapElement para dueño capturado. No se crean sistemas ni archivos C# nuevos.

- Cinco regresiones nuevas: dueño capturado/selección ajena y bootstrap real siguiente, fallo de segundo commit/reintento exacto, índice reciclado con GUID distinto, propiedad ambigua/desconocida y inventario viejo con formación/efectivos conservados. 130/130 EditMode pasan.
- SquadDetailPanel usa CalculateNext y MaxLevel compartidos; al alcanzar el máximo muestra MAX/barra completa, eliminando 1000/10 provisionales.

- Caso adicional: XP de escuadrón propio después de quitar IsLocalSquadActive por retirada; se identifica por dueño, no por estado activo. 131/131 EditMode pasan.

- Validación funcional final: 131/131 EditMode, 28/28 PlayMode; los guardados reales coinciden con las copias originales. Revisión v3 y documentación de arquitectura v1.7.

- Cierre retomado el 2026-10-14: grafo final confirmado en disco (7.753 nodos, 14.981 relaciones y 415 comunidades); 131/131 EditMode, 28/28 PlayMode y 69 assemblies Player Windows en los resultados guardados. Documentación v1.7 y lessons al día.

## Informe simplificado — 2026-10-14

- [x] Estado individual R01–R16 al principio del informe y eliminación de cronología repetida/afirmaciones históricas desactualizadas.
- El estado global distingue 13 correcciones implementadas de R09 (geometría estable), R11 (perfil grande Player/GPU) y R16 (propiedad de registros heredados), que siguen parciales. Los cierres anteriores corresponden al alcance implementado y sus pruebas, no a estos pendientes.

- 2026-10-14: BattleTestScene confirmada como escena para comprobar comportamiento en pantalla. Repetición funcional 7/7 PlayMode pasa; resultados en Logs/battletest-behavior-2026-10-14.json. Editor restaurado a CalibrationFactor12, fuera de Play y sin cambios pendientes. R11 sigue pendiente de batalla grande y perfil Player/GPU.

## Batalla completa desde preparación — 2026-10-14

- [x] Analizar BattleDebugCreator → BattlePreparationController → SceneTransitionService → BattleSceneController/bootstrap.
- [x] Extender el fixture existente con una ejecución explícita N contra N, roster determinista y proveedores aislados.
- [x] Ejecutar el flujo de inicio real, comprobar héroes/tropas/contactos y registrar perfil renderizado con sus límites.
- [x] Actualizar informe y grafo tras la verificación.

- Escena real reparada mediante Editor: ShieldConfigAuthoring ausente; Ground corregido como layer, no tag, en SquadControlSystem. Prueba explícita 10 contra 10 pasa, 20 héroes/240 soldados y HoldPosition confirmado. Perfil de 600 frames: 275 con contacto, máximos de 78 candidatos y 27 contactos; p95 bodyblock 0,3244 ms, detección 1,0366 ms e hilo principal 22,7345 ms. Proveedores aislados; no equivale a Player/GPU ni al extremo900.

- Verificación final del flujo completo: 1/1 explícita pasa; 131/131 EditMode, 28/28 PlayMode y 69 assemblies Player Windows confirmados tras las correcciones. Grafo actualizado: 7.737 nodos, 14.982 relaciones y 403 comunidades. Editor restaurado a su escena anterior.

## Prioridad: cursor en preparación y retorno de batalla — 2026-10-14

- [x] Reproducir Feudo → preparación con cursor heredado bloqueado e identificar quién lo vuelve a bloquear.
- [x] Corregir la política de cursor en las transiciones de interfaz y comprobar interacción por mouse.
- [x] Revisar el botón salir y el contexto de origen antes de entrar en cola.
- [x] Ejecutar regresiones, actualizar lessons/informe/grafo.

- Regresión inicial confirma Locked al entrar en preparación. Tras la corrección pasa la prueba de cursor libre y click InputSystem real sobre equipo. PostBattle incorpora Salir y pasa retorno a Feudo desde Assigned, con héroe conservado y nueva cola permitida. Ambas pruebas usan proveedores en memoria.
- La primera suite completa se canceló al quedar el Editor detenido por un diálogo; el usuario confirmó que lo cerró. Se retomó la verificación en ejecuciones aisladas y luego la suite completa.

- Cierre de cursor/retorno: las dos regresiones aisladas pasan; suite completa 30/30 PlayMode, 131/131 EditMode y 69 assemblies Player confirmadas. Grafo 7.782 nodos, 15.054 relaciones, 417 comunidades. Documentación v1.8 actualizada. Retorno a escena, sin snapshot de pose anterior.

- Confirmación final tras normalizar nombres/fases: 30/30 PlayMode y Player 69 assemblies. Grafo final 7.783 nodos, 15.055 relaciones, 419 comunidades; Editor en LoginScene, fuera de Play/pausa y sin cambios pendientes.

## Continuación: geometría estable de formación — 2026-10-15

- [x] Revisionar el ancla sólo cuando cambia posición o rotación.
- [x] Omitir el recálculo de slots estables según dueño, orden/estado, formación, slot y claves de terreno.
- [x] Ejecutar el perfil explícito BattlePrepScene → BattleScene con la caché activa.

- Resultado: `BattleSceneIntegrationTests.FullBattlePreparationRenderedPerformanceProfile` pasa 1/1; 20 héroes, 240 unidades iniciales, 228 al finalizar y 600/600 frames con contacto. p95 Editor: hilo principal 18,665 ms, bodyblock 0,3331 ms, detección 1,0737 ms. El perfil no atribuye el coste a GridFormationUpdate y no constituye comparación antes/después ni medición Player/GPU.
- Editor restaurado a LoginScene, sin cambios pendientes, en estado ready y fuera de Play Mode.
- Permanecen R11 (densidad mayor y Player/GPU) y R16 (propiedad de los registros heredados).
