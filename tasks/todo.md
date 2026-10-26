# Plan: C13 / L3-01 + F2 (+ F15) — slots validados sobre la NavMesh

Estado: en curso (2026-10-26), rama `main` local, sin subir.
Petición: bloque 1 del plan de la referencia. Plan anterior archivado en `tasks/reference-phase2-plan.md`.
Fuentes: `Docs/Design/Movimiento_Formaciones.md` (L-2, C13, D9, §5.2 S7), `Docs/Design/Aprendizajes_Referencia.md`
(L3-01, L3-04), `Docs/Por_Abordar/Formaciones_y_Posturas.md` (F2, F15). Referencia (solo patrones): la formación es una
rejilla local de ~1 m y los conflictos se reubican con BFS de 4 vecinos en la misma isla.

## Problema
Cada soldado proyecta su slot por separado (`UnitNavMeshSystem.ResolveFormationDestination`): primero en su celda
(`min(espaciado, 0,65)`) y, si falla, con el radio ancho de 2 m. Con un muro, varios slots caen al mismo borde (apilados en
la esquina) o al otro lado del muro (otra zona de la malla, que solo se alcanza rodeando). Más allá de 2 m el destino se
rechaza y el soldado se queda quieto.

## Diseño
- **`FormationSlotResolveSystem`** (nuevo, después de `GridFormationUpdateSystem` y antes de `UnitFormationStateSystem` y
  `SquadNavigationSystem`): por escuadra valida los slots de la plantilla contra la NavMesh. Solo consulta cuando la
  plantilla se ha movido más de `slotResolveRevalidateDistance` desde la última validación (cambio de ancla, de formación
  o de slot); entre validaciones arrastra la corrección. Nada por soldado ni por frame.
- **Lógica pura `FormationSlotResolver`** (EditMode con una malla falsa):
  1. Origen = el ancla alcanzada sobre la malla (al seguir al héroe, el trazo héroe→ancla recortado en el borde, para que
     un ancla dentro de un muro no salte al otro lado).
  2. Un slot vale si su punto proyectado queda dentro de su propia celda y se llega en línea recta desde el origen
     (`NavMesh.Raycast`, la "misma isla" local).
  3. Los que no valen, en orden de slot, prueban primero el desplazamiento que ya tenían (estabilidad) y después la celda
     libre más cercana por BFS de 4 vecinos en la rejilla local de la formación (celda = espaciado), acotado a
     `slotResolveSearchCells`. El slot se desplaza por celdas enteras: conserva su ruido y su papel.
  4. Sin celda libre: queda sin resolver y `UnitNavMeshSystem` usa el camino de antes. Nunca se rechaza la orden (D9).
- **`UnitResolvedSlotComponent`** (posición resuelta, corrección, desplazamiento en celdas, estado). Único escritor: el
  sistema nuevo. La plantilla (`UnitTargetPositionComponent`) no se toca. Lo leen como slot de movimiento
  `UnitNavMeshSystem`, `UnitFormationStateSystem`, `SquadNavigationSystem`, `UnitFollowFormationSystem`,
  `UnitAvoidanceSystem` y el grabador de métricas.
- **`UnitNavMeshSystem`**: con el slot validado no vuelve a proyectar (adiós al radio de 2 m por soldado); el radio de
  encaje de la celda pasa a dato (`slotSnapRadiusMax`, antes la constante 0,65).
- **Datos** en `MovementTuningConfig`: `slotResolveEnabled` (comparar antes/después), `slotResolveRevalidateDistance`,
  `slotResolveSearchCells`, `slotSnapRadiusMax`.
- **F15**: los radios de llegada (`r_in`, `r_out`) escalan con el espaciado de la formación cuando es menor que el de
  referencia (Testudo 0,8 m), así siempre `r_out ≤ s/2` y `r_in < s/4`. Un solo cálculo para los tres lectores
  (`FormationArrivalRadii`, `FormationFollowSpeed`, umbral de llegada de `UnitNavMeshSystem`).

## Pasos (un commit cada uno; EditMode en verde y PlayMode S1/S6 antes de cada commit que toque movimiento)
- [ ] **1. Escenario S7 cuello de botella y métricas.** `MovementBaselineS7Bottleneck`: pasillo de 2 m con giro hecho con
  muros que tallan la malla, delante del héroe; el héroe lo recorre en Línea. El grabador cuenta por frame destinos que
  comparten celda y destinos no alcanzables en línea recta desde el ancla. Línea base con el código actual.
- [ ] **2. F15: radios de llegada por formación.** EditMode de coherencia para todas las formaciones; S4.
- [ ] **3. `FormationSlotResolver` puro + EditMode.** Campo abierto (nada cambia), pasillo, muro con el otro lado
  transitable, estabilidad, sin hueco, determinismo.
- [ ] **4. Sistema, componente y lectores.** S7 con y sin `slotResolveEnabled`; S1/S4/S6 de regresión; S8 no existe (M3).
- [ ] **5. Documentación.** `Movimiento_Formaciones.md` (sección nueva), `Aprendizajes_Referencia.md` §5,
  `Docs/Por_Abordar` (F2, F15 y README), `SystemResponsibilities.md`, lecciones.

## Verificación
- S7 cuello de botella: 0 destinos compartiendo celda, 0 destinos no alcanzables con el sistema activo; comparar con la
  línea base (apilados, rezagados fuera del pasillo, tiempo en cruzar).
- S1/S4 sin empeorar (error al slot, formado al 95 %, solapes); S6 dentro del ruido de M10.
