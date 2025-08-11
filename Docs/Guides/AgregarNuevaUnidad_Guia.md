
# Guía Actualizada: Agregar un Nuevo Squad/Unidad al Juego

Esta guía describe el proceso actualizado y obligatorio para agregar una nueva unidad (Squad) completamente funcional al juego, incluyendo todos los assets, configuraciones y referencias necesarias.

---

## 1. Crear el Prefab ECS de la Unidad

- Crea el prefab ECS de la unidad en:
  - `Assets/Prefabs/Squads/{NombreUnidad}_ECS.prefab`
- Este prefab debe tener el componente `UnitEntityAuthoring` correctamente configurado.

## 2. Crear el Prefab Visual de la Unidad

- Crea el prefab visual en:
  - `Assets/Prefabs/Esqueletos/{NombreUnidad}_Visual.prefab`
- Este prefab debe tener todos los componentes visuales, animaciones y materiales necesarios.

## 3. Crear el ScriptableObject SquadData

- Crea un nuevo `SquadData` en:
  - `Assets/ScriptableObjects/Squad/{NombreDelSquad}.asset`
- Configura:
  - `id`: identificador único del squad
  - `prefab`: referencia al prefab ECS creado en el paso 1
  - `unitImage`: imagen miniatura de la unidad (ver paso 7)
  - `icon`: icono de la unidad (ver paso 6)
  - `gridFormations`: asigna las formaciones creadas en el paso 4
  - `abilitiesByLevel`: asigna las habilidades creadas en el paso 5
  - `visualPrefabName`: debe coincidir con el id definido en el paso 1 y 8
  - Otros campos según el diseño de la unidad

## 4. Crear las GridFormations

- Crea los ScriptableObjects de formaciones en:
  - `Assets/ScriptableObjects/Formations/{NombreDelSquadFormation}.asset`
- Define la composición y tamaño de la formación según el squad.

## 5. Crear las AbilityData

- Crea los ScriptableObjects de habilidades en:
  - `Assets/ScriptableObjects/Abilities/{NombreDelSquadAbility}.asset`
- Asigna las habilidades que tendrá el squad.

## 6. Crear el Icono del Squad

- Crea el icono de la unidad y guárdalo en:
  - `Assets/UI/units/{NombreDelSquadIcon}.png`
- Este icono se usará en la UI y en el SquadData.

## 7. Crear la Imagen Miniatura (unitImage)

- Genera una imagen miniatura de la unidad usando el prefab visual y guárdala en:
  - `Assets/UI/units/{NombreDelSquadImage}_miniature.png`
- Asigna esta imagen en el campo `unitImage` del SquadData.

## 8. Registrar el VisualPrefab en VisualPrefabRegistryConfiguration

- Abre el asset:
  - `Assets/ScriptableObjects/VisualPrefabConfig.asset`
- Agrega una nueva entrada con:
  - `id`: debe coincidir con el `visualPrefabName` definido en el SquadData
  - `prefab`: referencia al prefab visual creado en el paso 2

## 9. Añadir el SquadData al SquadDatabase

- Abre el asset:
  - `Assets/Scripts/Resources/Squad/SquadDatabase.asset`
- Agrega el nuevo `SquadData` a la lista `allSquads`.

---

## Resumen de Rutas y Nombres

- Prefab ECS: `Assets/Prefabs/Squads/{NombreUnidad}_ECS.prefab`
- Prefab Visual: `Assets/Prefabs/Esqueletos/{NombreUnidad}_Visual.prefab`
- SquadData: `Assets/ScriptableObjects/Squad/{NombreDelSquad}.asset`
- GridFormations: `Assets/ScriptableObjects/Formations/{NombreDelSquadFormation}.asset`
- AbilityData: `Assets/ScriptableObjects/Abilities/{NombreDelSquadAbility}.asset`
- Icono: `Assets/UI/units/{NombreDelSquadIcon}.png`
- Imagen Miniatura: `Assets/UI/units/{NombreDelSquadImage}_miniature.png`
- VisualPrefabConfig: `Assets/ScriptableObjects/VisualPrefabConfig.asset`
- SquadDatabase: `Assets/Scripts/Resources/Squad/SquadDatabase.asset`

---

## Checklist Rápido

- [ ] Prefab ECS creado y configurado
- [ ] Prefab visual creado y funcional
- [ ] SquadData creado y referenciado
- [ ] GridFormations asignadas
- [ ] AbilityData asignadas
- [ ] Icono y miniatura generados y asignados
- [ ] VisualPrefabConfig actualizado
- [ ] SquadDatabase actualizado

---

¡Listo! Siguiendo estos pasos tu nueva unidad estará completamente integrada y funcional en el juego.
