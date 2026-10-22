# Guía: Agregar un Nuevo Squad/Unidad al Juego

Revisada contra el código el 2026-10-22 (tipos y campos de `SquadData`, `SquadDatabase`, `VisualPrefabConfiguration`, `FormationDefinition`). Las rutas de assets que no existen hoy en el repositorio se indican como «convención»; no se verificó el flujo completo en el editor.

Un squad se define con un `SquadData` (ScriptableObject) registrado en el `SquadDatabase`, que `SquadDatabaseAuthoring` hornea a entidades ECS (una por `SquadData.id`). Los visuales se resuelven en tiempo de ejecución con `VisualPrefabRegistry` (ver `Docs/ConfiguracionPrefabs_ECS_Visual.md`).

---

## 1. Crear el prefab ECS de la unidad

- Prefab con el componente `UnitEntityAuthoring` (`Assets/Scripts/Squads/UnitEntity.Authoring.cs`) correctamente configurado.
- Ubicación por convención: `Assets/Prefabs/Squads/`.

## 2. Crear el prefab visual de la unidad

- Prefab con modelo, animaciones y materiales. Ubicación por convención: `Assets/Prefabs/esqueletos/`.
- No lleva lógica de juego: `EntityVisualSync` lo configura al instanciarlo (`SquadVisualManagementSystem`).

## 3. Crear los datos de combate y progresión

`SquadData` referencia módulos opcionales; crea los que correspondan:

- `meleeData` (`SquadMeleeData`) para combate cuerpo a cuerpo y `rangedData` (`SquadRangedData`) para distancia. Si es `null` el squad no ataca de ese modo.
- `progressionData` (`SquadProgressionData`): curvas de XP y desbloqueo de habilidades por nivel.
- Ejemplos existentes: `Assets/Resources/Squads/Squires/` y `Assets/Resources/Squads/Levy_Archers/`.

## 4. Crear las formaciones

- Las formaciones son assets `FormationDefinition` (espaciado, ruido, plantillas por soldados vivos, posturas). Ver `Docs/Mechanics/Formations.md`.
- Ejemplos: `Assets/Resources/Squads/Spearmen/Formations/` y `Assets/ScriptableObjects/Formations/`.

## 5. Crear las AbilityData

- Assets `AbilityData` (`Assets/Scripts/Squads/AbilityData.cs`) que el squad desbloquea al subir de nivel. El nivel de desbloqueo sale de `progressionData`.

## 6. Crear icono y miniatura

- Iconos en `Assets/Art/UI/units/` (ruta de los existentes, p. ej. `escuderos.png`). El icono va en `icon` y la miniatura en `unitImage`.

## 7. Crear el SquadData

Asset `SquadData` (menú `Squads/Squad Data`); los existentes están en `Assets/Resources/Squads/{Nombre}/`. Campos principales:

| Campo | Contenido |
|---|---|
| `id` | Identificador único (lo usa el bake como clave) |
| `squadName`, `type` (`SquadType`), `unitType`, `rarity` | Identidad y clasificación |
| `prefab` | Prefab ECS del paso 1 |
| `icon`, `background`, `unitImage` | Imágenes de UI |
| `formations` | `FormationDefinition[]` del paso 4 (antes `gridFormations`) |
| `abilitiesByLevel` | `AbilityData` del paso 5 |
| `meleeData`, `rangedData`, `progressionData` | Módulos del paso 3 |
| `leadershipCost`, `behaviorProfile`, `unitCount` | Despliegue e IA |
| `baseHealth`, `baseSpeed`, `massValue`, `totalWeight`, `block`, `blockRegenRate`, `shieldBreakStunDuration`, defensas, `detectionRange` | Stats base; los derivados los resuelve `StatResolveSystem` |
| `visualPrefabName` | Debe coincidir con el id de la entrada de visual del paso 8 |

## 8. Registrar el visual en la configuración de prefabs

- Añade una entrada `UnitPrefabEntry` (`squadType`, `unitId`, `displayName`, `visualPrefab`, `isDefault`) a la `VisualPrefabConfiguration` que usa el `VisualPrefabRegistry` de la escena.
- En el repositorio hay `Assets/ScriptableObjects/VisualPrefabConfig.asset` y `Assets/Config/DefaultVisualPrefabConfig.asset`; no se verificó cuál referencia cada escena.

## 9. Añadir el SquadData al SquadDatabase

- Abre `Assets/Resources/Squads/SquadDatabase.asset` y añade el `SquadData` a la lista `allSquads`.
- `SquadDatabaseAuthoring` (en el subscene) lo hornea; los squads sin `id` se ignoran.

---

## Checklist rápido

- [ ] Prefab ECS con `UnitEntityAuthoring`
- [ ] Prefab visual
- [ ] `meleeData` / `rangedData` / `progressionData` según el tipo de squad
- [ ] `FormationDefinition` asignadas en `formations`
- [ ] `AbilityData` asignadas en `abilitiesByLevel`
- [ ] Icono y miniatura asignados
- [ ] `visualPrefabName` coincide con la entrada de `VisualPrefabConfiguration`
- [ ] Squad añadido a `SquadDatabase.allSquads`
