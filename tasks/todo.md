# Plan: L4-04 + L4-09 — puntuación de objetivo con saturación blanda y búsqueda de enemigo por escuadra

Estado: **hecho** (2026-10-26), rama `main` local, sin subir. Resultados en `Docs/Design/Movimiento_Formaciones.md` §21.
Petición: bloque 2 del plan de la referencia. Plan anterior archivado en `tasks/c13-slot-resolve-plan.md`.
Fuentes: `Docs/Design/Aprendizajes_Referencia.md` (L4-04, L4-09), `Docs/Design/Movimiento_Formaciones.md` (D6, L-7, §13,
§17-20), `Docs/Por_Abordar/Movimiento.md` (M2, M6, M12, M14). Referencia (solo patrones): puntuación por cercanía + foco de
escuadra + sesgo, menos penalización por aliados ya asignados y bonus al objetivo actual; cada estrategia de escuadra fija
su referencia y su foco, el radio de búsqueda se interpola con la velocidad y hay filtros de altura y pendiente.

## Problema
- `UnitTargetingSystem` elige el enemigo más cercano y después reparte con un **cupo duro de 2 por escuadra**
  (`maxUnitsPerTarget`). El recuento es por escuadra: dos escuadras pueden poner 4 atacantes en el mismo enemigo, y el
  cupo duro cambia de objetivo a unidades que ya iban hacia él (cambios bruscos de destino en la melé, M12).
- No hay histéresis explícita: una unidad conserva su objetivo mientras sea válido y salta al más cercano cuando deja de
  serlo, sin mirar si otro está mejor repartido.
- Todo lo que detecta `EnemyDetectionSystem` (8 m al centroide, en 3D) es candidato, sin mirar la estrategia de la
  escuadra, su velocidad ni la cota del enemigo (un enemigo en lo alto de un muro es candidato).

## Diseño
- **Lógica pura `TargetScoring`** (EditMode):
  - `Score = (w_d·cercanía + w_f·foco + sesgo) · max(suelo, 1 − n·k) + histéresis·[actual]` (D6), con
    cercanía = 1 − d/R, `n` = atacantes de melé ya fijados en ese enemigo (todas las escuadras, sin contarse a sí mismo).
  - Candidatos: los K más cercanos a la unidad.
- **Lógica pura `EnemySearch`** (L4-09, EditMode):
  - Radio de combate por estrategia: mantener posición = mínimo (zona); ataque ordenado = máximo (atacar avanzando);
    seguir al héroe o reacción = interpolado entre mínimo y máximo con la velocidad del ancla. Arqueros: al menos su
    alcance. El radio se mide más allá del contorno de la escuadra (distancia al ancla menos el radio de la escuadra), para
    que una unidad de flanco no ignore al enemigo que tiene al lado.
  - Filtro de cota: descarta si |Δy| supera `searchMaxHeightDifference`, o si supera `searchStepHeight` con una pendiente
    mayor que `searchMaxSlopeDegrees`.
- **`UnitTargetingSystem`** (sigue siendo el único escritor de `UnitCombatComponent.target`):
  1. Recuento global de atacantes de melé por enemigo (todas las escuadras).
  2. Por escuadra: candidatos válidos que pasan `EnemySearch`; foco = escuadra enemiga de la orden, o la que más atacantes
     de esta escuadra tiene.
  3. Melé: la unidad en alcance de su objetivo válido lo conserva (enganche); el resto se reevalúa con `TargetScoring` cada
     `targetReevaluateInterval` con fase por entidad (sin picos, independiente de los FPS) o al perder el objetivo, y solo
     cambia si otro supera al actual con la histéresis. Sin cupo duro (D6).
  4. Distancia: igual que antes.
- **Datos** en `MovementTuningConfig` (sección de melé): pesos, `k`, suelo, histéresis, K, intervalo, radios, velocidad de
  radio máximo, alturas y pendiente. `maxUnitsPerTarget` desaparece; S7 multitud pasa a simular tres escuadras con `k/3`.
- `MeleeContactRingSystem` no cambia de lógica: el anillo coloca, la puntuación reparte. Se actualiza su comentario.
- Métrica nueva: cambios de objetivo por unidad y segundo con objetivo (los que pasan de un enemigo a otro).

## Pasos (un commit cada uno; EditMode en verde y PlayMode S1/S6 antes de cada commit)
- [x] **1. Métrica de cambios de objetivo** (analizador + EditMode) y línea base: S1, S4, S6 ×3, S7 multitud ×5. Hecho (`b39d44ed`).
- [x] **2. `TargetScoring` y `EnemySearch` puros + EditMode.** Hecho (`b8f94afd`).
- [x] **3. `UnitTargetingSystem` con puntuación, saturación blanda e histéresis (L4-04)**; datos; adiós al cupo duro; S7 con
  `k/3`. EditMode (reparto entre escuadras, histéresis, enganche). Medir S6 ×3, S7 ×5, S1, S4.
- [x] **4. Búsqueda por escuadra (L4-09)**: radio por estrategia y velocidad, filtro de cota. EditMode. Medir.
- [x] **5. Documentación**: `Movimiento_Formaciones.md` §21, `Aprendizajes_Referencia.md` §5, `Docs/Por_Abordar`
  (M2, M6, M12, M14 y README), `SystemResponsibilities.md`, lecciones.

## Verificación
- S6 y S7 multitud: pares enemigos solapados > 0,3 s, aceleración > 20 m/s², atacantes por objetivo, cambios de objetivo;
  comparar con la línea base de este bloque y con §20 (S6 11, 11; S7 14 / 1,75 %).
- S1 y S4 idénticos (la marcha no tiene objetivos).
- EditMode completo en verde.

## Resultado
- Pasos 3 y 4 en un solo commit (`7c82cbd3`): el sistema hace las dos cosas en la misma pasada; la búsqueda se midió
  aparte apagándola con un cambio temporal. Cambio frente al diseño, por lo medido: radio mínimo 6 m (= la correa) en lugar
  de 3 m, dentro del ruido pero coherente con lo que la escuadra puede perseguir.
- S6: picos de aceleración 2,39 → 1,54 %, p95 de atacantes por objetivo 6 → 2, cambios de objetivo 0,22 → 0,16 por unidad y
  segundo; solapes sin cambio (11 → 11,5). S7: picos 1,98 → 1,43 %, solapes 10 → 9,5. S1 y S4 iguales.
- Abiertos: M2 (solapes, siguiente C16/F9), M12 (empujes del bodyblock), M16 (paradas en la melé), variantes de L4-09 que
  piden L4-03 (línea de tiro) y una orden de atacar zona.
