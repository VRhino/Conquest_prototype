# Relación entre Squad Data, ECS Prefab y Visual Prefab

Este documento detalla cómo se conectan los datos de configuración de los escuadrones con las entidades de ECS y sus representaciones visuales en Unity.

## 1. SquadData (ScriptableObject)
El `SquadData` es el punto de entrada y la "fuente de verdad" para un tipo de escuadrón.

*   **`prefab` (GameObject):** Este es el **ECS Prefab**. Es un GameObject vacío que contiene el componente `UnitEntityAuthoring`. No tiene malla ni renderers; su única función es definir qué componentes de lógica ECS tendrá cada unidad del escuadrón.
*   **`visualPrefabName` (string):** Es el nombre identificador del **Visual Prefab**. Este nombre se usa en tiempo de ejecución para buscar el GameObject real (con mallas, animaciones, etc.) en el `VisualPrefabRegistry`.

---

## 2. Proceso de Baking (Conversión a ECS)
Existen dos niveles de baking:

### A. Squad Data Entity
`SquadDataAuthoring` toma el ScriptableObject y crea una entidad de datos persistente:
*   El `GameObject prefab` se convierte en una `Entity` de tipo prefab-only y se guarda en `SquadDataComponent.unitPrefab`.
*   Toda la estadística (vida, velocidad, etc.) se guarda en componentes y buffers de esta entidad.

### B. Unit ECS Prefab
El GameObject asignado al campo `prefab` del `SquadData` tiene un `UnitEntityAuthoring` que:
*   Define componentes de lógica como `UnitSpacingComponent`, `UnitOrientationComponent`, etc.
*   **Importante:** También guarda el `visualPrefabName` en un componente `UnitVisualReference`.

---

## 3. Flujo en Tiempo de Ejecución

### Fase 1: Spawning (`SquadSpawningSystem`)
Cuando un héroe aparece, este sistema:
1.  Crea la entidad lógica del **Squad**.
2.  Instancia N entidades de **Unit** usando el `unitPrefab` (el ECS Prefab).
3.  **Nota Detectada:** Actualmente, el `SquadSpawningSystem` tiene un mapeo interno (`GetUnitVisualPrefabName`) que asigna el nombre del visual basado en el `SquadType`, lo cual podría estar ignorando el valor configurado directamente en el ScriptableObject en algunos casos.

### Fase 2: Visualización (`SquadVisualManagementSystem`)
Este sistema reacciona a la creación de unidades:
1.  Busca entidades que tengan `UnitVisualReference` pero no tengan una instancia visual todavía.
2.  Usa el `visualPrefabName` para obtener el prefab real desde `VisualPrefabRegistry.Instance`.
3.  Instancia el GameObject visual en la escena de Unity.
4.  Añade el componente `EntityVisualSync` al GameObject para sincronizar su Transform con la entidad ECS.
5.  Añade `UnitVisualInstance` a la entidad ECS para guardar el ID de la instancia y evitar duplicados.

---

## Resumen de la Cadena de Referencias
`SquadData (SO)` ➔ `SquadDataComponent.unitPrefab (Entity)` ➔ `Spawned Unit (Entity)` ➔ `UnitVisualReference (Component)` ➔ `Visual GameObject (UnityEngine.Object)`

## Componentes Clave
| Componente | Función |
| :--- | :--- |
| `SquadDataComponent` | Almacena el `unitPrefab` (el ECS-only prefab). |
| `UnitVisualReference` | Contiene el nombre del prefab visual a buscar en el registro. |
| `UnitVisualInstance` | Guarda la referencia a la instancia del GameObject creado. |
| `EntityVisualSync` | Script de MonoBehaviour que sincroniza el Visual con el ECS. |
