# Formaciones

Revisado línea a línea contra el código el 2026-10-22.

## 1. Resumen

Las formaciones son configuraciones tácticas que los squads adoptan bajo instrucción directa del héroe. Son **datos** (`FormationDefinition`), no código: cada squad lleva su lista en `SquadData.formations`, que se hornea en un `FormationLibraryBlob`.

- Siete tipos en `FormationType`: Line, Dispersed, Testudo, Wedge, Column, Square, ShieldWall. Cada squad dispone solo de las que tiene en su lista.
- Un octavo, `Rest` («En reposo», 2026-11-11): la formación de reposo de cada tropa, la que toma al seguir al héroe. Ver §2.1.
- El cambio de formación se hace con doble-tap `X` (ciclar por la lista del squad) o `F1`–`F4` (índice 0–3 de esa lista). `SquadPlayerCommandResolver.TryResolveFormation` ignora un índice que el squad no tiene.
- Cooldown entre cambios: `MovementTuningComponent.formationChangeCooldown` (1 s por defecto en `MovementTuningComponent.Default`).

### Estado frente al código (2026-10-22)
| Apartado | Código | Estado |
|---|---|---|
| Multiplicador de masa por formación (§3) | No existe `massMultiplier`; la masa viene de `SquadData.massValue` | **Diseño no implementado**: la formación no cambia la masa |
| Tabla de formaciones y compatibilidad por tipo de squad (§2) | La compatibilidad es qué formaciones lista cada `SquadData` | La tabla del diseño original (Schiltron, Muro de Escudos con multiplicadores) no existe tal cual; se conserva abajo como diseño |
| Menú radial `ALT` | `ALT` solo alterna el cursor de interfaz | No implementado |
| Bajas y huecos (§5) | Plantillas por soldados vivos, reparto por cercanía y huecos cerrados con espera (`FormationSystem`, `FormationLayout.CompactSlots`) | Sincronizado (2026-11-03, L3-02); detalle en `Docs/Design/Movimiento_Formaciones.md` §11 y §24 |
| Disposición y slots (§6) | `FormationDefinition`, `FormationSystem`, `GridFormationUpdateSystem` | Sincronizado; `FormationGrid.System.cs` ya no existe |
| Adaptación dinámica (§5) | `FormationAdaptationSystem` calcula `requiresAdaptation` (espacio estrecho) | **Nadie lo consume** (ver `Docs/Por_Abordar/Formaciones_y_Posturas.md`) |
| Datos y posturas | `Docs/Mechanics/FormationPostures.md` | Sincronizado |

---

## 2. Tipos de formación

| `FormationType` | Muro (bodyblock) | Notas |
|---|:-:|---|
| Line | ✔ | Combate frontal general |
| Testudo | ✔ | Defensa con escudos; posturas de escudo por papel |
| Wedge | ✔ | Carga y ruptura de línea |
| Square | ✔ | |
| ShieldWall | ✔ | Afianzada al formar, escudo al frente en marcha (`Tools/Formations/Apply Default Postures`) |
| Dispersed | | Abierta: se puede atravesar entre huecos |
| Column | | Abierta |

| Rest | | «En reposo»: al seguir al héroe. Abierta |

"Muro" es la clasificación de `UnitBodyblockSystem.IsWallFormation` (solo afecta al modo muelle heredado; ver `BodyblockSystem.md`). Los assets de ejemplo están en `Assets/ScriptableObjects/Formations/` (Line, Dispersed, Testudo, Wedge) y `Assets/Resources/Squads/Spearmen/Formations/` (Line, Dispersed, Wedge).

### 2.1. Formación de reposo («En reposo»)

Toda tropa tiene una (`SquadData.restFormation`, tipo `Rest`): es como se coloca cuando sigue al héroe. La disposición de
cada soldado la deciden sus datos, como en las demás formaciones: una leva (granjeros, arqueros de leva) va dispersa y
desordenada (`Scatter`, mucho espaciado y ruido); una tropa con instrucción (lanceros, piqueros) va junta y en orden
(bloque de `Template`, espaciado corto y ruido casi nulo). `Tools/Formations/Create Rest Formations`
(`RestFormationSetup`) crea la de cada tropa que no la tiene, con un perfil por `SquadData.id`; luego se ajusta a mano.

- **Siempre disponible**: no la abre ningún nivel ni tecnología.
- **No es de F1-F4 ni del ciclo con X**: el baker la pone la última de la biblioteca y `TryResolveFormation` no la cuenta.
  Desde el reposo, X vuelve a la primera formación de la lista.
- **La pide FollowHero** (`SquadPlayerCommandResolver.TryApply`): seguir al héroe pone la escuadra en reposo. Una
  escuadra sin formación de reposo se abre en `Dispersed`, si la tiene (lo de antes).
- La IA no cambia de formación (`OrderResolutionSystem`), así que sus escuadras siguen sin reposo. Pendiente de decidir.
- El HUD no tiene icono para el reposo: con la escuadra en reposo no se ilumina ninguno de F1-F4.

### Diseño original no implementado

El diseño inicial planteaba Schiltron y Muro de Escudos con multiplicadores de masa (Línea ×1,0; Testudo ×2,0; Dispersa ×0,5; Cuña ×1,3; Schiltron ×1,5; Muro de Escudos ×1,5), velocidad relativa por formación y compatibilidad fija por tipo de squad (Escuderos, Arqueros, Piqueros, Spearmen). Nada de eso está en el código: no hay Schiltron en `FormationType`, no hay multiplicador de masa y la compatibilidad no es una tabla sino la lista `formations` de cada `SquadData`.

---

## 3. Masa

No hay multiplicador de masa por formación. `SquadData.massValue` se hornea como masa de la unidad (`SquadDefinitionBaker`); la resolución de cargas del diseño (`Docs/Mechanics/DamageMechanics.md` trata el daño) no consulta la formación.

---

## 4. Cambio de formación

| Método | Tecla | Comportamiento |
|--------|-------|----------------|
| **Ciclar** | Doble-tap `X` | Avanza a la siguiente formación de la lista del squad |
| **Selección directa** | `F1`–`F4` | Índice 0–3 de la lista del squad |

**Condiciones:**
- `FormationSystem` aplica el cambio cuando la formación pedida difiere de la actual y ha pasado
  `SquadStateComponent.formationChangeReadyAt` (hora absoluta; `formationChangeCooldown` de `MovementTuningConfig`, 1 s).
  FollowHero no espera. La petición viaja en `SquadResolvedOrderComponent`; una pedida durante el enfriamiento se aplica
  al acabar.
- **Solo se visitan las escuadras con cambios (L3-06).** `FormationChangePending` (enableable) la encienden
  `OrderResolutionSystem` (formación pedida distinta de la actual), `UnitDeathSystem` (baja) y la petición de espaciado;
  `FormationSystem` la apaga cuando no queda nada pendiente.
- En **Hold Position** el centro se conserva y la formación se coloca en el sitio (`SquadHoldPositionComponent.originalFormation` se actualiza).
- Un cambio de formación no reemite la orden de movimiento: el squad sigue hacia su destino y se reorganiza.

---

## 5. Adaptación dinámica

`FormationAdaptation.System.cs` solo **calcula** `requiresAdaptation` (espacio estrecho) en `EnvironmentAwarenessComponent`; ningún sistema lo consume, así que hoy no ajusta nada. El diseño original planteaba:

- **Unidades muertas**: los slots vacantes se redistribuyen para mantener la coherencia visual de la formación.
- **Terreno**: si el terreno impide mantener la geometría exacta, las posiciones se ajustan manteniendo la orientación.

Al perder unidades, cuando los vivos bajan del mínimo de la plantilla actual, la formación pasa a la plantilla más pequeña que les corresponde y cada unidad toma la ranura libre más cercana.

**Huecos dentro de una plantilla (L3-02, 2026-11-03).** Si cae un soldado sin que cambie la plantilla, `FormationSystem`
espera `formationCompactDelay` (`MovementTuningConfig`, 1,5 s) desde la primera baja, para agrupar las bajas seguidas, y
luego cierra los huecos (`FormationLayout.CompactSlots`): los n vivos ocupan las n primeras ranuras de la plantilla, que
es su orden de llenado. Solo se mueven los soldados que estaban en ranuras sobrantes (o sin ranura), cada uno al hueco
libre más cercano y sin cruzarse con los otros que se mueven; los demás conservan su ranura. Un cambio de plantilla o de
formación durante la espera lo reparte todo de nuevo y la cancela.

---

## 6. Grid layout y posiciones

El sistema de grid asigna a cada unidad un **slot** dentro de la formación:

1. `FormationSystem` elige la plantilla de `FormationDefinition` según los soldados vivos y reparte las ranuras (la libre más cercana al rehacer la disposición).
2. `FormationPositionCalculator` calcula la posición 3D de cada slot relativa al centro del squad (ranura × espaciado + ruido determinista), usando `calculateTerraindHeight` para ajustar la elevación del terreno.
3. `UnitFollowFormation.System.cs` mueve cada unidad hacia su slot asignado frame a frame.
4. `UnitFormationState.System.cs` gestiona las transiciones de estado de las unidades (`Moving` / `Formed` / `Waiting`) en respuesta al movimiento de formación.
   - **Llegada con histéresis** (`FormationArrival.Step`): una unidad entra en `Formed` por debajo de `arrivalRadiusIn` (0,2 m) cuando el ancla lleva `arrivalCalmTime` (0,15 s) quieta, y solo sale por encima de `arrivalRadiusOut` (0,5 m) o de `holdReformRadius` en Hold.
   - **Arranque por filas** (`FormationStartDelay`): al salir de `Formed`, la espera es `fila × startRowDelay` (0,08 s) más una variación determinista por slot de hasta `startJitterDelay` (0,1 s). La fila delantera (fila 0 de la ranura) sale primero.
   - **Destino adelantado** (`UnitNavMeshSystem`, `FormationDestinationPrediction`): con el slot en marcha, el agente recibe el slot proyectado adelantado `destinationLeadTime` (0,4 s, tope 1 m) y solo se reenvía al cambiar más de `max(0,25 m, v × 0,25 s)`.
   - Los valores citados son los de `MovementTuningComponent.Default` en código; los efectivos viven en `Assets/ScriptableObjects/Movement/DefaultMovementTuning.asset` (`MovementTuningConfig`), horneado como `MovementTuningComponent` por `SquadSpawnConfigAuthoring`.
5. `GridFormationUpdate.System.cs` gira el desplazamiento local de cada slot con el ancla.

El centro de la formación sigue al **punto de destino del squad** (orden de movimiento) o permanece en su posición actual (Hold Position).

---

## 7. Edge cases

| Caso | Resolución |
|------|-----------|
| Squad con menos unidades que slots | Si hay una plantilla más pequeña para ese número de vivos se usa; si no, quedan slots vacíos |
| Squad en Hold Position al cambiar formación | La formación se reorganiza in situ alrededor del `holdCenter` |
| Índice de formación que el squad no tiene | `TryResolveFormation` lo ignora y la orden válida simultánea sigue |
| Cambio antes de acabar el cooldown | No se aplica hasta que `formationChangeCooldown` llega a 0 |
| Formación cambiada mientras el squad se mueve | La reorganización ocurre mientras se sigue moviendo; no se interrumpe el pathfinding |
| Cambio durante una carga activa | **Sin carga de formación implementada**: no hay mecánica de carga que lo condicione |

---

## 8. Infraestructura existente

| Archivo | Rol |
|---------|-----|
| `Assets/Scripts/Squads/Formation.Component.cs` | `FormationComponent`: formación actual, plantilla, espaciado, escala, semilla |
| `Assets/Scripts/Squads/FormationLibraryBlob.cs` | Blob con las formaciones del squad (`FormationDataBlob`: tipo, layout, espaciado, ruido, plantillas, posturas) |
| `Assets/Scripts/Squads/Formations/FormationDefinition.cs`, `FormationLayout.cs` | Datos editables y funciones puras de disposición |
| `Assets/Scripts/Squads/Systems/Formation.System.cs` | Único escritor de `FormationComponent` y `UnitGridSlotComponent` |
| `Assets/Scripts/Squads/Systems/FormationAdaptation.System.cs` | Calcula `requiresAdaptation` (sin consumidor hoy) |
| `Assets/Scripts/Squads/Systems/GridFormationUpdate.System.cs` | Gira el desplazamiento local de los slots con el ancla |
| `Assets/Scripts/Squads/Systems/UnitFormationState.System.cs` | Estados Moving / Formed / Waiting de cada unidad |
| `Assets/Scripts/Squads/Systems/UnitFollowFormation.System.cs` | Velocidad y orientación Formed hacia el slot |
| `Assets/Scripts/Squads/FormationPositionCalculator.cs` | Posición 3D de cada slot; `calculateTerraindHeight` (sic) para la altura |
| `Assets/Scripts/UI/Battle/HUDFormationIconController.cs` | Muestra la formación activa en el HUD |

---

## 9. Technical Reference

### Sistemas ECS (en orden de pipeline)

| Archivo | Atributos de orden | Rol |
|---------|---------------|-----|
| `Formation.System.cs` | `UpdateAfter(SquadOrderSystem)` | Aplica el cambio de formación pedido y rehace slots |
| `GridFormationUpdate.System.cs` | `UpdateAfter(FormationSystem)` | Mantiene la posición de los slots respecto al ancla |
| `FormationAdaptation.System.cs` | `UpdateAfter(SquadControlSystem)` | Calcula `requiresAdaptation` |
| `UnitFormationState.System.cs` | `UpdateAfter(GridFormationUpdateSystem)` | Transiciones Moving / Formed / Waiting |
| `UnitFollowFormation.System.cs` | `UpdateAfter(GridFormationUpdateSystem)` | Mueve unidades hacia sus slots; no modifica estado |

Orden completo y componentes: `Docs/TroopMovementPipeline.md`.
