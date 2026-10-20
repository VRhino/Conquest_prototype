# Plan: formaciones por datos (modelo de la referencia) y reparación del movimiento

Estado: **plan aprobado en lo esencial; pendiente de arrancar**. Fecha: 2026-10-20. Rama prevista: `feat/formation-layout`, a partir de `feat/movement-batch2`.

Origen:
- Petición del usuario: sustituir la rejilla fija de Conquest por un modelo repensado a partir de Conqueror's Blade, y después reparar los tirones y la llegada al slot.
- Referencia (solo patrones, sin código ni datos): `game-re/notes/formations-orders.md` §1-2 y §7, `game-re/notes/mounts-movement.md` §6, `game-re/notes/formation-postures.md`.
- Lo que queda fuera de este plan, con el porqué y cómo abordarlo: `Docs/Por_Abordar/`.
- Diseño: `Docs/Design/Movimiento_Formaciones.md` §3 (espaciado `s`), D7 (§7) y §10 (lote 2).

## Qué cambia, en una frase por capa
| Capa | Hoy (Conquest) | Después (modelo de la referencia) |
|---|---|---|
| Datos de la formación | `GridFormationScriptableObject`: `Vector2Int[]` en celdas de 1 m | `FormationDefinition`: tipo de disposición, espaciado propio y plantillas por número de soldados |
| Posición de la ranura | celda entera × `CELL_WIDTH` (constante de código) | `ranura (float2) × espaciado × escala de escuadra + ruido determinista` |
| Plantilla | una por tipo de formación | la primera con `mínimo ≤ vivos`; al caer soldados, cambia de forma |
| Espaciado | igual para todo, no cambia | por formación, cambiable en partida, con suelo de 0,3 m |
| Fila | se deduce de `gridPosition.y` (y la animación la recibe invertida) | dato explícito de la ranura (`row`, 0 = frente) |
| Rumbo del soldado | todos miran hacia donde mira la escuadra | **rumbo local por ranura**, girado con la formación (los costados pueden mirar hacia fuera) |
| Papel del soldado | no existe | **papel por ranura**: `Front`, `Flank`, `Interior`, `Rear` |
| Postura | `BracedShields` solo al llegar en `ShieldWall`; no llega al Animator | postura por (formación, papel, estado), con efecto en la defensa y la animación (paso 6) |
| Dispersa | rejilla escrita a mano | disposición Scatter: celdas distintas al azar (semilla por escuadra) × escala + desplazamiento |
| Cuerpo y evitación | radio combinado = espaciado (1 m), parche de 0,9 en C14 | radio del cuerpo independiente; el radio entre compañeros sale del espaciado vigente de la escuadra |
| Encaje en la malla | radio global de 2 m | `min(espaciado, 0,65)` |

Lo que **no** se copia de la referencia: el reparto ranura k = soldado k se mantiene (ya es así en Conquest). La elección de plantilla por encaje en la malla (C13) queda para después.

## Decisiones por defecto (a confirmar)
1. **Espaciado inicial por formación:** Línea 1,1 m, Cuña 1,1 m, Testudo 0,8 m (compacta: los cuerpos se solapan a propósito), Dispersa con escala 2,0 m. Hoy todo es 1,0 m.
2. **Ruido orgánico:** radio 0,08 m y ±3° en Línea y Cuña; 0 en Testudo. Determinista por (escuadra, ranura), para que no tiemble entre frames.
3. **Plantillas por número de soldados:** el modelo las admite. Las formaciones actuales se migran con **una sola plantilla** (se comportan como hoy). Las plantillas reducidas (p. ej. 15, 10 y 5 lanceros) son trabajo de contenido aparte.
4. **Espaciado en partida:** componente y API para cambiarlo (habilidades, órdenes "apretar/abrir"). Sin tecla asignada en este lote.
5. **Papel por ranura en la migración:** fila delantera = `Front`; columnas extremas del resto = `Flank`; última fila = `Rear`; el resto = `Interior`. Se puede retocar en el editor.
6. **Rumbo por ranura en la migración:** 0° (hacia delante) en todas. En Testudo, los `Flank` miran hacia fuera (±90°) y la `Rear`, hacia atrás (180°).
7. **Posturas por defecto (paso 6):** Testudo: `Front` escudo al frente, `Flank` escudo al costado, `Interior` y `Rear` escudo arriba. ShieldWall: todos escudo al frente y afianzados. El resto: normal.

## Paso 1. Modelo de datos
- [ ] `FormationDefinition` (ScriptableObject, `Squads/Formations/`): `formationType`, `formationIcon`, `layout` (`Template` | `Scatter`), `spacing`, `noiseRadius`, `noiseAngleDegrees`, `templates[]` (`minUnits`, `slots[]` con `position` float2 en unidades de espaciado, `row`, `role` (`FormationSlotRole`) y `headingDegrees` local), y para Scatter `scatterScale` y `scatterJitter`.
- [ ] Validación en el SO y en el baker: espaciado ≥ 0,3 (si no, 0,3 con aviso, como la referencia); plantillas ordenadas por `minUnits` descendente; ninguna ranura repetida; aviso si `spacing < 2·bodyRadius` (Testudo lo acepta).
- [ ] Blob nuevo `FormationLibraryBlob` v2 (`FormationDataBlob`: tipo, disposición, espaciado, ruido, plantillas con `BlobArray<FormationSlotBlob>`). `SquadDefinitionBaker.BakeFormationLibrary` lo construye.
- [ ] `SquadData.gridFormations` → `formations` (`FormationDefinition[]`), con `FormerlySerializedAs` solo durante la migración.
- [ ] Migración de editor (una vez, desde el menú): los 7 assets actuales pasan a `FormationDefinition` conservando el orden de ranuras; `row` = fila contada desde el frente; espaciado y ruido según las decisiones 1-2. Se repuntan Spearmen, Squires y Levy Archers. Se borran los assets viejos tras comprobar el horneado.
- [ ] Editor de plantillas: sustituye a `GridFormationEditor`. Pinta ranuras sobre una rejilla con paso de 0,5 (comodidad de autoría; los datos quedan en float2), muestra filas y la huella real con el espaciado.
- [ ] Se eliminan `GridFormationScriptableObject`, `GridFormationEditor`, `FormationGridSystem` (`CELL_WIDTH/DEPTH`) y `UnitSpacingComponent` (C15, sin lectores).

## Paso 2. Disposición en tiempo de ejecución
- [ ] Funciones puras `FormationLayout` (Burst): `SelectTemplate(ref formation, aliveCount)`, `SlotLocal(ref formation, template, slotIndex, spacing, squadSeed)` (con ruido determinista), `ScatterCells(n, seed)` (celdas enteras distintas, sin la (0,0)), `Center(ref template)`.
- [ ] `FormationComponent` gana `spacing` (vigente) y `scale` (de la escuadra, por defecto 1). Su único escritor es `FormationSystem`; `SquadFormationSpacingRequest` (componente de petición) lo cambia en partida, con suelo de 0,3 m.
- [ ] `UnitGridSlotComponent`: `gridPosition int2` → `localPosition float2`, `row int`, `role` y `headingLocal` (radianes). `slotIndex` y `worldOffset` siguen.
- [ ] Orientación al formar: `UnitFollowFormationSystem` orienta a la unidad `Formed` con `rotación de la formación × rumbo de su ranura` (con ruido angular determinista) en lugar del rumbo de la escuadra.
- [ ] `FormationPositionCalculator`, `FormationSystem`, `GridFormationUpdateSystem` (incluida su caché: entra el espaciado vigente y la plantilla), `DestinationMarkerSystem`, `SquadSpawningSystem`, `SquadSwapExecutionSystem` y `FormationVisualizer` pasan a la API nueva.
- [ ] Cambio de plantilla por bajas: `FormationSystem` re-elige la plantilla cuando cambia el número de vivos (como la referencia) y reescribe los slots sin reordenar unidades.
- [ ] Consumidores de la fila: `FormationStartDelay` (C2), `BraceWeaponSystem`, `FormationStanceSystem` y `SquadUnitSpawnBuilder` leen `row`. Se corrige así la fila invertida que hoy recibe la animación (`SlotRow`).

## Paso 3. Consumidores del espaciado
- [ ] `UnitAvoidanceSystem`: `squadmateCombinedRadius = min(2·bodyRadius, 0,9·espaciado de la escuadra)` por agente (dato nuevo en `AllyAvoidance.Agent`); "cerca del slot" = menos de un espaciado. Se quita la dependencia de `CELL_WIDTH`.
- [ ] `MovementTuningComponent.Validate`: los radios de llegada se validan contra el **menor** espaciado de las formaciones horneadas (aviso en el baker) en lugar de una constante.
- [ ] `UnitNavMeshSystem`: el radio de proyección del slot pasa a `min(espaciado, 0,65)` (L3-04); el radio global de 2 m queda solo como reintento.

## Paso 4. Tests y medida
- [ ] Ayudante de test común para construir bibliotecas de formación (hoy hay 7 tests que montan blobs a mano con `int2`).
- [ ] EditMode `FormationLayoutTests`: selección de plantilla por vivos; posición = ranura × espaciado × escala; ruido determinista y acotado; Scatter sin celdas repetidas; suelo de 0,3; filas; rumbo de ranura girado con la formación; papeles de la migración.
- [ ] Actualizar `FormationGeometryRegressionTests`, `ArchitectureRegressionTests`, `SquadControlRegressionTests`, `SquadOwnerDeathRegressionTests`, `SquadSwapRegressionTests`, `MovementTuningCoherenceTests`.
- [ ] S1, S3, S4 y S6 contra el cierre del lote 2. Esperado: la huella crece un 10 % en Línea; solapes aliados al formar ≤ los de §10; formado ≤ 1 s.

## Paso 5. Reparación del movimiento (tras el paso 4)
Medido al cierre del lote 2 (aceleraciones por encima de 20 m/s², con agentes de 8 m/s²): S1 0 % → 0,7 %, S4 0 % → 6,7 % (máx. 197 m/s²).
- [ ] **ORCA respeta la aceleración:** `v_nueva = v_actual + recorte(v_orca − v_actual, a·dt)`, con `a` = `agent.acceleration`. Hoy `agent.velocity` se escribe directamente y se salta el límite del agente.
- [ ] **Cambios de velocidad suaves en C9:** `agent.speed` sube y baja como mucho a `a·dt` por frame; el umbral de slot quieto (0,5 m/s) pasa a mezcla continua o con histéresis. Hoy el tope cae de golpe (Run → Walk, mínimo ↔ máximo) y el agente recorta la velocidad en un frame.
- [ ] **Llegada al slot:** revisar el desvío (recorrido / línea recta hasta 2,2 en S3 con ORCA, 1,0 antes) y el atasco detrás de cuerpos quietos (S4). Hipótesis a medir: el héroe como obstáculo de responsabilidad 1 dentro de la formación, y escuadras vecinas formadas.
- [ ] Métrica nueva en el analizador: porcentaje de frames con aceleración > 2·`a` y desvío de llegada. Objetivo: 0 % por encima de 20 m/s² y desvío ≤ 1,2.
- [ ] Reconsiderar el parche de C14 con el espaciado nuevo (1,1 m deja holgura frente al cuerpo de 1 m).

## Paso 6. Posturas por ranura (aparte, tras el paso 5)
Patrón de la referencia (`game-re/notes/formation-postures.md`): la ranura da el papel y el rumbo; la postura es un dato de cada soldado y cambia también por dónde se le puede golpear. Aquí lo resuelve un solo sistema.
- [ ] Datos: `FormationDefinition.postures`: tabla papel → postura para el estado formado (y opcionalmente en marcha). `UnitStance` pasa a `Normal`, `ShieldFront`, `ShieldSide`, `ShieldUp` y `Braced`.
- [ ] `UnitPostureSystem`, único escritor de `UnitFormationStanceComponent`: calcula la postura por (formación, papel, estado formado / en marcha / en combate). Sustituye la regla fija de `FormationStanceSystem`, que se elimina.
- [ ] Defensa: la dirección de bloqueo sale de la postura y del rumbo, no del prefab. `ShieldOrientation` gana `Up` (cubre lo que viene desde arriba: flechas en parábola). `DamageCalculationSystem` usa la orientación vigente de la unidad.
- [ ] Animación: parámetros `Stance` (int) y `SlotRole` (int) en `UnitAnimationAdapter` y en los Animator Controllers de las tropas con escudo. Inventario previo de clips disponibles (escudo al frente, al costado y arriba). Si falta alguno, se deja la postura activa en lógica y se avisa (contenido pendiente en `Docs/Por_Abordar/`).
- [ ] Tests: EditMode de la tabla papel → postura y de la dirección de bloqueo con `Up`; PlayMode: en Testudo formada, una flecha en parábola contra el interior se bloquea y una flecha frontal contra un `Flank` no.
- [ ] Medida: vídeo de Testudo y ShieldWall formadas y en marcha; comprobar que la postura no parpadea al llegar (usa la histéresis de C1).

## Documentación al cerrar
- [ ] `Docs/Design/Movimiento_Formaciones.md`: §11 con el modelo nuevo y la medida; actualizar L-2 y la fila `s` de §3.
- [ ] `Assets/Scripts/Squads/SystemResponsibilities.md` y `CLAUDE.md` (cambia el escritor y los datos de formación).
- [ ] `Docs/Mechanics/` (nuevo o ampliado): posturas, papel de ranura y dirección de bloqueo.
- [ ] `Docs/Por_Abordar/`: marcar lo que este plan haya cerrado.
- [ ] `tasks/lessons.md`.

## Riesgos
- Medio-alto: muchos consumidores de `gridPosition` y de los blobs; se mitiga con el ayudante de test y compilando por paso.
- La migración de assets debe conservar GUID de las escuadras y comprobar el horneado antes de borrar nada.
- La huella mayor (1,1 m) cambia el aspecto y puede afectar a pasillos estrechos del mapa.

---

## Hecho anteriormente
- [x] Lote 2 de movimiento (C5, C6, C7, C9, C14): plan en `tasks/movement-batch2-plan.md` y resultados en `Docs/Design/Movimiento_Formaciones.md` §10.
- [x] Lote 1 de movimiento (C1-C4): plan en `tasks/movement-batch1-plan.md` y resultados en §9.
- [x] Estudio de movimiento y formaciones `Docs/Design/Movimiento_Formaciones.md`, con las decisiones del usuario en §7.
