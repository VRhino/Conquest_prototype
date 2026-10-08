# Relación entre SquadData, unidades ECS y visuales

> Actualizado el 2026-09-26. Describe el camino de despliegue usado por `SquadSpawningSystem`.

## Datos y baking

`SquadData` es el ScriptableObject con estadísticas, formaciones y datos de presentación. `SquadDefinitionBaker` crea una entidad de datos con `SquadDataComponent`, `SquadDefinitionComponent` y la biblioteca de formaciones. El campo `SquadData.prefab` puede seguir sirviendo a la interfaz, pero no se convierte en un `unitPrefab` dentro de `SquadDefinitionComponent`.

`UnitEntityAuthoring` y su baker todavía existen para prefabs ECS antiguos o independientes. El despliegue de squads no instancia ese prefab.

## Despliegue

1. `SquadSpawningSystem` elige snapshot, formación y slots; crea el squad y cada unidad ECS mediante `CreateEntity`. `SquadUnitSpawnBuilder` compone los componentes iniciales de cada unidad.
2. Cada unidad recibe `UnitOwnerComponent` con referencias al squad y al héroe. El squad mantiene su `DynamicBuffer<SquadUnitElement>` con las mismas unidades.
3. El sistema compone estadísticas, combate, slot y `UnitVisualReference` desde los datos del squad. `GetUnitVisualPrefabName` elige la clave visual a partir de `SquadType`.
4. `SquadVisualManagementSystem` busca esa clave en `VisualPrefabRegistry`, instancia el GameObject y vincula `EntityVisualSync`, hitbox y `NavMeshAgent`.
5. `UnitNavMeshSystem` controla el destino del agente; `NavMeshPositionSyncSystem` publica la posición confirmada en ECS. Al morir la unidad, `UnitDeathSystem` elimina el visual y la pertenencia al buffer.

## Referencias principales

`SquadData` → entidad de datos del squad → `SquadSpawningSystem` → unidad ECS (`UnitOwnerComponent`, `UnitVisualReference`) → `VisualPrefabRegistry` → GameObject visual.

La clave visual del camino actual procede de `SquadType`. Si se quiere usar una clave personalizada del ScriptableObject, debe añadirse explícitamente a este contrato de spawn y probarse con prefabs reales.
