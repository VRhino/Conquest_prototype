# Formaciones

## 1. Resumen

Las formaciones son configuraciones tácticas que los squads adoptan bajo instrucción directa del héroe. Son **herramientas críticas**, no solo visuales.

- 6 formaciones disponibles, cada una con un **multiplicador de masa** que afecta la resolución de cargas.
- El cambio de formación se puede hacer con doble-tap `X` (ciclar en secuencia) o `F1`–`F4` (selección directa).
- **No todas las formaciones están disponibles para todos los squads** — la compatibilidad depende del tipo de unidad y el nivel de la escuadra.
- Los cambios de formación son inmediatos si el squad no está en medio de una carga activa.

### Estado frente al código (2026-10-21)
| Apartado | Código | Estado |
|---|---|---|
| Tipos de formación | `FormationType`: Line, Dispersed, Testudo, Wedge, Column, Square, ShieldWall | **Schiltron no existe en el código**; Column y Square existen pero no están en las tablas de este doc |
| Bajas y huecos (§5) | Plantillas por soldados vivos y reparto por cercanía (`FormationSystem`) | Sincronizado; el detalle está en `Docs/Design/Movimiento_Formaciones.md` §11 |
| Disposición y slots (§6) | `FormationDefinition` (datos), `FormationSystem` (reparto), `GridFormationUpdateSystem` (giro con el ancla) | Sincronizado; `FormationGrid.System.cs` ya no existe |
| Adaptación dinámica (§5) | `FormationAdaptationSystem` calcula `requiresAdaptation` | **Nadie lo consume** (ver `Docs/Por_Abordar/Formaciones_y_Posturas.md`) |
| Datos y posturas | `Docs/Mechanics/FormationPostures.md` | Sincronizado |

---

## 2. Tabla de formaciones

| Formación | Multiplicador de Masa | Velocidad relativa | Uso ideal |
|-----------|----------------------|--------------------|-----------|
| **Línea** | x1.0 | Normal | Combate frontal general; disponible para todos los squads |
| **Testudo** | x2.0 | Lenta | Defensa contra proyectiles; solo Escuderos |
| **Dispersa** | x0.5 | Normal | Evitar AoE y proyectiles; solo Arqueros |
| **Cuña** | x1.3 | Normal | Carga y ruptura de línea; Piqueros y Spearmen |
| **Schiltron** | x1.5 | Estática | Anti-carga / anti-caballería; solo Piqueros |
| **Muro de Escudos** | x1.5 | Muy lenta | Máxima defensa frontal; Escuderos y Spearmen |

### Compatibilidad por tipo de squad

| Squad | Línea | Testudo | Dispersa | Cuña | Schiltron | Muro de Escudos |
|-------|:-----:|:-------:|:--------:|:----:|:---------:|:---------------:|
| Escuderos | ✔ | ✔ | | | | ✔ |
| Arqueros | ✔ | | ✔ | | | |
| Piqueros | ✔ | | | ✔ | ✔ | |
| Spearmen | ✔ | | | ✔ | | ✔ |

Las formaciones no listadas para un squad **no pueden seleccionarse** — la UI las muestra deshabilitadas o no las presenta.

---

## 3. Mass multiplier

La masa total de un squad se calcula como:

```
MasaTotal = SquadData.masaBase * FormationProfile.multiplicador
```

Esta masa solo afecta la **resolución de cargas** (impacto, ruptura de formación enemiga). No modifica:
- Navegación ni pathfinding
- Daño en combate normal
- Defensa, bloqueo o precisión

Formaciones **más cerradas** (Testudo, Schiltron, Muro de Escudos) aumentan la masa y la resistencia a embestidas. Formaciones **abiertas** (Dispersa) reducen masa, priorizando movilidad sobre empuje.

---

## 4. Cambio de formación

| Método | Tecla | Comportamiento |
|--------|-------|----------------|
| **Ciclar** | Doble-tap `X` | Avanza al siguiente en la lista de formaciones disponibles para ese squad |
| **Selección directa** | `F1`–`F4` | Selecciona una formación específica (mapeo configurable) |
| **Menú radial** | `ALT` | Abre menú radial con todas las formaciones disponibles del squad |

**Condiciones de cambio:**
- Cambio **inmediato** si el squad no está ejecutando una carga activa.
- Si el squad está en estado **Hold Position**: el cambio de formación se aplica en el lugar donde está.
- Si el squad está en **combate normal** (no carga): el cambio se aplica; las unidades reorganizan posiciones en el siguiente tick de `FormationSystem`.

El cambio de formación **no interrumpe** el movimiento en curso — el squad continúa hacia su destino y reorganiza mientras se mueve.

---

## 5. Adaptación dinámica

`FormationAdaptation.System.cs` ajusta la formación cuando el contexto lo requiere:

- **Unidades muertas**: los slots vacantes se redistribuyen para mantener la coherencia visual de la formación.
- **Terreno**: si el terreno impide mantener la geometría exacta, las posiciones se ajustan manteniendo la orientación.

Al perder unidades, cuando los vivos bajan del mínimo de la plantilla actual, la formación pasa a la plantilla más pequeña que les corresponde y cada unidad toma la ranura libre más cercana. Si la formación solo tiene una plantilla, quedan huecos hasta que se añadan plantillas reducidas (F1 en `Docs/Por_Abordar/Formaciones_y_Posturas.md`).

---

## 6. Grid layout y posiciones

El sistema de grid asigna a cada unidad un **slot** dentro de la formación:

1. `FormationSystem` elige la plantilla de `FormationDefinition` según los soldados vivos y reparte las ranuras (la libre más cercana al rehacer la disposición).
2. `FormationPositionCalculator` calcula la posición 3D de cada slot relativa al centro del squad (ranura × espaciado + ruido determinista), usando `calculateTerrainHeight` para ajustar la elevación del terreno.
3. `UnitFollowFormation.System.cs` mueve cada unidad hacia su slot asignado frame a frame.
4. `UnitFormationState.System.cs` gestiona las transiciones de estado de las unidades (`Moving` / `Formed` / `Waiting`) en respuesta al movimiento de formación.
   - **Llegada con histéresis** (`FormationArrival.Step`): una unidad entra en `Formed` por debajo de `arrivalRadiusIn` (0,2 m) cuando el ancla lleva `arrivalCalmTime` (0,15 s) quieta, y solo sale por encima de `arrivalRadiusOut` (0,5 m) o de `holdReformRadius` en Hold.
   - **Arranque por filas** (`FormationStartDelay`): al salir de `Formed`, la espera es `fila × startRowDelay` (0,08 s) más una variación determinista por slot de hasta `startJitterDelay` (0,1 s). La fila delantera (fila 0 de la ranura) sale primero.
   - **Destino adelantado** (`UnitNavMeshSystem`, `FormationDestinationPrediction`): con el slot en marcha, el agente recibe el slot proyectado adelantado `destinationLeadTime` (0,4 s, tope 1 m) y solo se reenvía al cambiar más de `max(0,25 m, v × 0,25 s)`.
   - Todos estos valores viven en `Assets/ScriptableObjects/Movement/DefaultMovementTuning.asset` (`MovementTuningConfig`), horneado como `MovementTuningComponent` por `SquadSpawnConfigAuthoring`.
5. `GridFormationUpdate.System.cs` gira el desplazamiento local de cada slot con el ancla.

El centro de la formación sigue al **punto de destino del squad** (orden de movimiento) o permanece en su posición actual (Hold Position).

---

## 7. Edge cases

| Caso | Resolución |
|------|-----------|
| Squad con menos unidades que slots | Si hay una plantilla más pequeña para ese número de vivos se usa; si no, quedan slots vacíos |
| Formación cambiada durante una carga activa | El cambio se encola; la formación anterior completa la carga y luego se aplica la nueva [verificar en `Formation.System.cs`] |
| Squad en Hold Position al cambiar formación | La formación se reorganiza in situ; las unidades se mueven a sus nuevos slots sin salir del área de Hold |
| Squad intenta formación no compatible con su tipo | La UI no permite la selección; el sistema ECS ignora el request si llega por error |
| Una sola unidad sobrevive en el squad | La formación degrada a una unidad; el multiplicador de masa sigue aplicándose sobre `masaBase` |
| Formación cambiada mientras squad se mueve hacia destino | La reorganización ocurre mientras se sigue moviendo; no se interrumpe el pathfinding |

---

## 8. Infraestructura existente

| Archivo | Rol |
|---------|-----|
| `Assets/Scripts/Squads/Formation.Component.cs` | Tipo de formación activa del squad, índice en la lista |
| `Assets/Scripts/Squads/FormationLibraryBlob.cs` | Blob asset con todos los perfiles de formación (nombre, multiplicador, geometría de grid) |
| `Assets/Scripts/Squads/Systems/Formation.System.cs` | Aplica la formación al squad; valida compatibilidad con el tipo de unidad |
| `Assets/Scripts/Squads/Systems/FormationAdaptation.System.cs` | Calcula `requiresAdaptation` (sin consumidor hoy) |
| `Assets/Scripts/Squads/Systems/GridFormationUpdate.System.cs` | Gira el desplazamiento local de los slots con el ancla |
| `Assets/Scripts/Squads/Systems/UnitFormationState.System.cs` | Gestiona estados Moving / Formed / Waiting de cada unidad |
| `Assets/Scripts/Squads/Systems/UnitFollowFormation.System.cs` | Mueve físicamente cada unidad hacia su slot asignado |
| `Assets/Scripts/Squads/FormationPositionCalculator.cs` | Calcula posiciones 3D de cada slot; `calculateTerrainHeight` para elevación |
| `Assets/Scripts/UI/Battle/HUDFormationIconController.cs` | Muestra la formación activa en el HUD; deshabilita las incompatibles |

---

## 9. Technical Reference

### Componentes ECS

| Archivo | Rol |
|---------|-----|
| `Assets/Scripts/Squads/Formation.Component.cs` | `FormationType currentFormation`, `int formationIndex`; referencia a `FormationLibraryBlob` |
| `Assets/Scripts/Squads/FormationLibraryBlob.cs` | Blob asset con `FormationProfile[]`: nombre, `float massMultiplier`, grid geometry |

### Sistemas ECS (en orden de pipeline)

| Archivo | UpdateInGroup | Rol |
|---------|---------------|-----|
| `Assets/Scripts/Squads/Systems/Formation.System.cs` | SimulationSystemGroup | Aplica cambio de formación solicitado; valida compatibilidad |
| `Assets/Scripts/Squads/Systems/FormationGrid.System.cs` | SimulationSystemGroup (post Formation) | Asigna slots a unidades vivas |
| `Assets/Scripts/Squads/Systems/GridFormationUpdate.System.cs` | SimulationSystemGroup (post Grid) | Recalcula grid tras cambio o baja de unidades |
| `Assets/Scripts/Squads/Systems/FormationAdaptation.System.cs` | SimulationSystemGroup | Ajusta posiciones por terreno o unidades muertas |
| `Assets/Scripts/Squads/Systems/UnitFormationState.System.cs` | SimulationSystemGroup | Transiciones de estado: Moving → Formed → Waiting |
| `Assets/Scripts/Squads/Systems/UnitFollowFormation.System.cs` | SimulationSystemGroup (post State) | Mueve unidades hacia sus slots; no modifica estado |

### Utilidades y UI

| Archivo | Rol |
|---------|-----|
| `Assets/Scripts/Squads/FormationPositionCalculator.cs` | `calculateTerrainHeight(position)`, posiciones relativas de grid |
| `Assets/Scripts/UI/Battle/HUDFormationIconController.cs` | Iconos de formación en HUD; estado activo/deshabilitado según compatibilidad |
