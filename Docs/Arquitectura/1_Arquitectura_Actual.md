# Arquitectura actual de Conquest Prototype

| | |
|---|---|
| **Versión** | 1.1 |
| **Actualizado** | 2026-09-14 |
| **Verificado contra** | base `5f52f8cab1127357174c56b74a7b191f01171a00` + reparaciones locales sin commit |
| **Motor** | Unity 6000.5.8f1 |
| **Runtime de datos** | Entities 6.5.0, arquitectura híbrida ECS/GameObject |

> Este documento describe el código que existe actualmente. No define la arquitectura objetivo ni el
> contrato futuro con BronzeAge. Las propuestas compartidas viven en
> [`../Coordinacion/`](../Coordinacion/README.md), especialmente en
> [`03_Modelo_compartido_entidades_v0.md`](../Coordinacion/03_Modelo_compartido_entidades_v0.md).

## Historial

| Versión | Fecha | Cambio |
|---|---|---|
| 1.1 | 2026-09-14 | Reparaciones de compilación, bake compartido, progresión, guardado, combate e identidad ECS. Ver [registro y límites de validación](2_Reparaciones_Arquitectura_2026-09-14.md). |
| 1.0 | 2026-09-13 | Inventario inicial verificado del modelo persistente, catálogos ScriptableObject, DTO de batalla, puente entre escenas, entidades ECS y estado real del contrato de red. Se documenta la coexistencia de dos persistencias locales. |

## 1. Propósito y alcance

Conquest Prototype es el cliente y runtime táctico de Conquest Tactics. Contiene:

- menús, preparación de batalla y presentación mediante GameObjects/MonoBehaviours;
- configuración estática mediante ScriptableObjects;
- persistencia local mediante clases C# serializables y JSON;
- simulación de batalla mediante Unity Entities/ECS;
- sincronización visual desde ECS hacia GameObjects;
- un matchmaking simulado que genera batallas locales de prueba.

La arquitectura de datos no está centralizada en un solo modelo. Actualmente existen tres representaciones
principales del mismo dominio:

1. datos persistentes y de sesión en clases C# serializables;
2. definiciones estáticas en ScriptableObjects;
3. componentes y buffers ECS para la ejecución de la batalla.

Entre persistencia y ECS hay una cuarta representación temporal: `BattleData`/`BattleHeroData`.

Este documento cubre esas representaciones, sus relaciones y sus fronteras. No intenta enumerar cada
componente de cámara, UI, animación o depuración; documenta los componentes que forman el modelo de juego o
participan en su conversión.

## 2. Método de verificación

El inventario se obtuvo inspeccionando:

- clases `[Serializable]` bajo `Assets/Scripts/Data/Persistence/` y `Assets/Scripts/Shared/`;
- catálogos y definiciones `ScriptableObject`;
- componentes `IComponentData` y buffers `IBufferElementData`;
- bakers de héroes, squads, unidades, mapas y definiciones estáticas;
- servicios de save/load, sesión, matchmaking y transición de batalla;
- sistemas de spawn que materializan los DTO en ECS;
- assets registrados bajo `Assets/Resources/`;
- búsquedas de transportes o primitivas de red existentes.

No se ejecutaron pruebas de juego para redactar esta versión: es una verificación estructural contra código y
assets. La fecha y el commit deben actualizarse cuando cambie alguna forma serializada, identidad, relación o
flujo descrito aquí.

## 3. Vista global

```text
PERSISTENCIA LOCAL Y SESIÓN

  player_save.json                         player_progress.json
        │                                          │
        ▼                                          ▼
    PlayerData                         LocalSaveSystem.PlayerProgressData
        │                                          │
        └── HeroData                               ├── LoadoutData
              ├── LoadoutSaveData                 └── LocalSaveSystem.SquadInstanceData
              ├── SquadInstanceData
              ├── InventoryItem / Equipment
              └── AvatarParts
                        │
                        │ IDs y referencias de Unity
                        ▼
CONFIGURACIÓN ESTÁTICA

  HeroClassDefinition   SquadData   ItemDataSO   MapDataSO   AvatarPartDatabase
                              │
                              │ selección/proyección
                              ▼
TRANSFERENCIA LOCAL DE BATALLA

                  BattleData
                  ├── MapDataSO
                  ├── attackers: BattleHeroData[]
                  └── defenders: BattleHeroData[]
                              │
                              ▼
                  BattleTransitionData
                              │
                              ▼
                  BattleSceneController
                              │
                              ▼
RUNTIME ECS

  DataContainer singleton ──► Hero entity ──► Squad entity ──► Unit entities
              │                                      │
              └──────────────► mapa, spawns, zonas y estado del match
```

## 4. Capas y responsabilidades

| Capa | Forma de datos | Responsabilidad actual | Persistencia |
|---|---|---|---|
| Sesión | `PlayerSessionService` | Mantiene `CurrentPlayer` y `SelectedHero` en memoria | No directamente |
| Persistencia principal | POCOs `[Serializable]` | Perfil, héroes, inventario, equipo, loadouts y progreso de squads | `player_save.json` |
| Persistencia ECS heredada | Clases anidadas de `LocalSaveSystem` | Nivel, loadouts y estado de despliegue consumido por sistemas ECS/UI | `player_progress.json` |
| Configuración | `ScriptableObject` | Definiciones estáticas, balance y referencias visuales | Assets Unity |
| Proyección de batalla | `BattleData`, `BattleHeroData` | Selección temporal de participantes y datos necesarios para abrir una batalla | Sólo memoria |
| Runtime táctico | ECS components/buffers | Estado mutable y simulación de una batalla | No se guarda como World ECS |
| Visual | GameObjects y MonoBehaviours | Render, animación, HUD, colliders y efectos | Prefabs/assets |

Regla de arquitectura existente: el estado de simulación se modifica desde ECS. La capa visual consume ECS a
través de `EntityVisualSync`, registros visuales y componentes puente; no debe convertirse en otra fuente de
verdad del combate.

## 5. Persistencia principal: `player_save.json`

### 5.1 Raíz `PlayerData`

Archivo: `Assets/Scripts/Data/Persistence/Player.Data.cs`.

| Campo | Tipo | Significado |
|---|---|---|
| `playerName` | `string` | Nombre visible de la cuenta local |
| `accountLevel` | `int` | Nivel global de cuenta |
| `accountXP` | `int` | Experiencia global de cuenta |
| `gold` | `int` | Moneda global mantenida por `PlayerData` |
| `heroes` | `List<HeroData>` | Héroes creados por el jugador |

Cardinalidad actual:

```text
PlayerData 1 ── 0..N HeroData
```

`SaveSystem` usa `JsonUtility.ToJson` y un `LocalSaveProvider` cuyo archivo por defecto es
`Application.persistentDataPath/player_save.json`. `LoadSystem` reconstruye la misma raíz mediante
`JsonUtility.FromJson<PlayerData>`.

### 5.2 `HeroData`

Archivo: `Assets/Scripts/Data/Persistence/Hero.Data.cs`.

`HeroData` es el agregado persistente principal y actualmente concentra varias responsabilidades:

| Grupo | Campos principales |
|---|---|
| Identidad | `classId`, `heroName`, `gender`, `avatar` |
| Progresión | `level`, `currentXP`, `attributePoints`, `perkPoints` |
| Economía | `bronze`, `silver`, `gold` |
| Atributos base | `strength`, `dexterity`, `armor`, `vitality` |
| Desbloqueos | `unlockedPerks`, `availableSquads` |
| Organización táctica | `loadouts`, `squadProgress` |
| Inventario | `inventory`, `equipment` |

Implementa interfaces de lectura y mutación (`IHeroIdentity`, `IHeroProgression`, `IHeroEconomy`,
`IHeroSquads`, `IHeroInventory` y variantes mutadoras), pero esas interfaces no cambian la forma JSON: los
campos públicos siguen siendo el contrato efectivo de persistencia.

No existe `heroId`. El nombre `heroName` actúa como identidad práctica en varios flujos, incluida la búsqueda
del participante local dentro de `BattleData`.

### 5.3 Loadouts y escuadras persistentes

```text
HeroData
├── loadouts: List<LoadoutSaveData>
│     └── squadInstanceIDs: List<string>
└── squadProgress: List<SquadInstanceData>
      ├── id: string
      └── baseSquadID: string ──► SquadData.id
```

`LoadoutSaveData` contiene:

- nombre;
- IDs de instancias de escuadra;
- IDs de perks como `string`;
- liderazgo total precalculado;
- flag `isActive`.

`SquadInstanceData` contiene:

- `id`: GUID/string generado al crear la instancia;
- `baseSquadID`: definición estática del squad;
- nivel y experiencia;
- habilidades y formaciones desbloqueadas;
- formación seleccionada;
- efectivos totales, heridos y muertos;
- equipo perdido.

`unitsAlive` es una propiedad derivada:

```text
unitsAlive = unitsInSquad - unitsInjured - unitsKilled
```

`SquadDataService.CreateSquadInstance` crea el GUID y copia el tamaño y las formaciones permitidas desde
`SquadData`.

### 5.4 Inventario y equipo

```text
HeroData
├── inventory: List<InventoryItem>
└── equipment: Equipment
      ├── weapon: InventoryItem
      ├── helmet: InventoryItem
      ├── torso: InventoryItem
      ├── gloves: InventoryItem
      ├── pants: InventoryItem
      └── boots: InventoryItem
```

`InventoryItem` mezcla dos variantes:

- item apilable: `instanceId == null`, cantidad mayor o igual que uno;
- equipo único: `instanceId` no vacío, cantidad normalmente uno y stats generados.

Campos relevantes:

| Campo | Tipo | Relación |
|---|---|---|
| `itemId` | `string` | Resuelve `ItemDataSO.id` |
| `itemType` | enum | Copia de clasificación para filtrado |
| `quantity` | `int` | Cantidad del stack |
| `price` | `int` | Precio calculado por unidad |
| `slotIndex` | `int` | Posición del inventario |
| `instanceId` | `string` | Identidad de una pieza única |
| `serializedStats` | `SerializableStat[]` | Forma persistente de los stats generados |

`Equipment` guarda objetos `InventoryItem` completos en cada slot. No guarda referencias a `instanceId`.
Esto permite conservar los stats, pero también admite que inventario y equipo contengan copias divergentes de
la misma pieza.

### 5.5 Avatar

`AvatarParts` almacena cuatro IDs (`headId`, `hairId`, `beardId`, `eyebrowId`). Se resuelven contra
`AvatarPartDatabase`/`AvatarPartDefinition`, que a su vez contienen `VisualAttachment` con rutas de prefab y
bones por género.

Son datos visuales persistentes; las referencias a prefabs no deberían convertirse en autoridad de gameplay.

## 6. Persistencia paralela: `player_progress.json`

Archivo: `Assets/Scripts/Shared/LocalSave.System.cs`.

`LocalSaveSystem` define un segundo modelo completo:

```text
PlayerProgressData
├── level
├── currentXP
├── perkPoints
├── loadouts: List<LoadoutData>
└── squads: List<LocalSaveSystem.SquadInstanceData>
```

Este modelo no es equivalente al principal:

| Concepto | Modelo principal | Modelo de `LocalSaveSystem` |
|---|---|---|
| Archivo | `player_save.json` | `player_progress.json` |
| Squad ID | `string` GUID | `int` |
| Definición de squad | `baseSquadID: string` | `squadType: SquadType` |
| XP de squad | `int experience` | `float currentXP` |
| Estado de efectivos | total/heridos/muertos | `armorPercent` |
| Habilidades | `List<string>` | `List<int>` |
| Formaciones | índices `int` | `FormationType` |

Consumidores encontrados:

- `HeroLevelSystem`;
- `DataContainerSystem`;
- `LoadoutSelectionUI`;
- `UnitDeploymentValidationSystem`.

Por tanto, no es código muerto. La coexistencia de ambos archivos es una bifurcación vigente de fuente de
verdad y una frontera explícita para cualquier migración o contrato de red.

## 7. Catálogos y definiciones ScriptableObject

Los datos estáticos se agrupan mediante assets Unity y servicios de resolución.

### 7.1 Escuadras

```text
SquadDatabase
└── allSquads: List<SquadData>
      ├── meleeData: SquadMeleeData?
      ├── rangedData: SquadRangedData?
      ├── progressionData: SquadProgressionData
      ├── gridFormations: GridFormationScriptableObject[]
      ├── abilitiesByLevel: List<AbilityData>
      └── prefab: GameObject
```

`SquadData` es la definición táctica base. Incluye identidad, rareza, tipos, coste de liderazgo, perfil de
comportamiento, tamaño de unidad, detección, defensas, vida, velocidad, masa, bloqueo y referencias visuales.

Los módulos opcionales determinan capacidad de combate:

- `meleeData != null` habilita daño, penetración y timings melee;
- `rangedData != null` habilita daño, munición, precisión, cadencia y proyectil;
- ambos pueden existir, aunque el bake actual prioriza los valores de daño melee cuando hay ambos.

`SquadDatabaseAuthoring` y `SquadDataAuthoring` convierten estas definiciones a entidades ECS estáticas con
`SquadDataComponent`, `SquadDefinitionComponent`, `SquadDataIDComponent`, `SquadStatsComponent`, buffers de
stats y blobs de formaciones.

### 7.2 Items

```text
EnhancedItemDatabase
└── ItemDataSO[]
      ├── visualPartId ──► AvatarPartDefinition.id
      ├── statGenerator: ItemStatGenerator?
      ├── effects: ItemEffect[]?
      └── pricingConfig: ScriptableObject?
```

Una definición con `statGenerator` se considera equipo. Una definición con efectos se considera consumible.
La instancia persistente no almacena el asset: conserva `itemId` y sus datos dinámicos.

### 7.3 Clases, habilidades y perks

`HeroClassDefinition` contiene:

- enum `HeroClass`;
- atributos base;
- constantes y multiplicadores de cálculo;
- habilidades activas `HeroAbility`;
- perks válidos `HeroPerk`.

El bake convierte parte de esta definición a componentes y buffers ECS. No todos los campos del
ScriptableObject aparecen necesariamente en una única representación runtime.

### 7.4 Mapas

```text
MapDatabase
└── MapDataSO[]
      ├── supplyPointIds: string[]
      ├── capturePointIds: string[]
      ├── attackerSpawnPointIds: string[]
      ├── defenderSpawnPointIds: string[]
      ├── battleDuration
      └── preparationMap: GameObject
```

El mapa de catálogo no contiene directamente el estado runtime de captura. Los spawns y zonas se hornean o
crean como entidades ECS de escena.

### 7.5 Contenido registrado en el commit verificado

| Catálogo | Contenido observado |
|---|---|
| Squads | 3 referencias: `sqd01`, `arc01`, `spm01` |
| Items | 19 referencias serializadas en `ItemSODatabase`; 18 assets `ItemDataSO` localizados bajo `Assets/Resources/items/` |
| Mapas | 1 mapa: `default` |
| Clases de héroe | `Bow`, `Spear`, `TwoHandedSword`, `SwordAndShield` bajo `Scripts/Resources/Data/HeroClasses/`, más otro asset `Spear` fuera de esa ruta |

La última referencia de `ItemSODatabase` (`28a0c4deab655eb46a370426218f6efb`) no tiene `.meta` correspondiente
en el árbol inspeccionado. Debe tratarse como referencia serializada potencialmente rota hasta verificarla en
el editor.

## 8. DTO temporal de batalla

### 8.1 `BattleData`

Archivo: `Assets/Scripts/Data/Battle/Battle.Data.cs`.

```text
BattleData
├── battleID: string
├── mapData: MapDataSO
├── attackers: List<BattleHeroData>
├── defenders: List<BattleHeroData>
├── PreparationTimer: int
└── BattleTimer: int
```

`playerSide` y `findHeroDataByName` localizan al participante mediante `heroName`.

### 8.2 `BattleHeroData`

Archivo: `Assets/Scripts/Data/Battle/BattleHero.Data.cs`.

```text
BattleHeroData
├── heroName
├── classID
├── level
├── spawnPointId
├── squadInstances: List<SquadInstanceData>
├── avatar: AvatarParts
├── equipment: Equipment
└── gender
```

`BattleDebugCreator.ConvertHeroToBattleHero` copia identidad, nivel, avatar y equipo desde `HeroData`. Para
las escuadras recorre el primer loadout y busca cada `squadInstanceID` dentro de `HeroData.squadProgress`.

Esta conversión no crea una forma neutral o inmutable: reutiliza las mismas clases `SquadInstanceData`,
`AvatarParts` y `Equipment` del modelo persistente.

## 9. Flujo de entrada a batalla

```text
PlayerSessionService.SelectedHero
        │
        ▼
MatchmakingService.StartMatchmaking
        │
        │ espera simulada de 2–5 segundos
        ▼
BattleDebugCreator.CreateBattleWithLocalHero
        │
        ▼
BattleData
        │
        ▼
BattleTransitionData.SetBattleData
        │
        │ cambio de escena
        ▼
BattleSceneController
        ├── SyncBattleDataToECS       configura al jugador local
        └── SpawnRemoteHeroes         crea héroes remotos simulados
```

`BattleTransitionData` es un `ScriptableObject` creado dinámicamente que conserva una referencia a
`BattleData` en memoria. `GetAndClearBattleData` entrega esa referencia y la elimina del contenedor.

No hay serialización, copia defensiva, validación de versión ni transporte en este tramo.

## 10. Puente `BattleData` → ECS

`BattleSceneController.SyncBattleDataToECS` entrega una petición administrada a
`BattleBootstrapRequests`. `BattleBootstrapSystem` es quien escribe el estado local en ECS:

1. obtiene `PlayerSessionService.SelectedHero.heroName`;
2. busca el `BattleHeroData` por nombre;
3. convierte `spawnPointId: string` a `int`, con fallback `1`;
4. deriva `teamID` desde la pertenencia a attackers/defenders;
5. actualiza el singleton `DataContainerComponent`;
6. toma el primer squad como activo;
7. asigna índices de batalla `0..N-1`, conservando además el ID persistente;
8. crea `SquadIdMapElement` con identidad, definición, nivel, XP, formación, efectivos y supervivientes.

Transformación actual de identidad:

```text
SquadInstanceData.id: string/GUID
        │ se conserva como persistentId
        ▼
DataContainer.selectedSquads: int[] secuencial
        │
        ├── 0 = squad activo
        └── SquadIdMapElement { squadId, persistentId, baseSquadID, snapshot... }
```

El ID real llega a `SquadInstanceComponent.persistentId`; `id` sigue siendo el índice temporal.
El ID `0` sigue identificando la escuadra inicialmente activa. Los héroes remotos reciben su propio
buffer mediante una petición consumida por ECS, sin reutilizar el loadout del jugador local.

## 11. Entidades ECS principales

En ECS una entidad no corresponde a una clase única. Su identidad funcional surge del conjunto de
componentes y buffers que posee.

### 11.1 Singleton de datos locales

`DataContainerComponent` contiene:

- `playerID`, `playerName` y `teamID`;
- `selectedLoadoutID`;
- `selectedSquads` y `selectedPerks` como listas fijas de `int`;
- liderazgo seleccionado;
- spawn seleccionado;
- flag de preparación;
- `selectedSquadBaseID` string.

En la misma entidad existe `DynamicBuffer<SquadIdMapElement>` para traducir el índice runtime de squad a su
definición estática string.

### 11.2 Entidad de héroe

Composición conceptual observada:

| Área | Componentes representativos |
|---|---|
| Identidad/configuración | `HeroClassComponent`, `HeroClassReference`, `HeroAttributesComponent` |
| Progreso | `HeroProgressComponent` |
| Vida/combate | `HeroLifeComponent`, `HeroHealthComponent`, `HeroCombatComponent`, `StaminaComponent` |
| Movimiento/input | `HeroInputComponent`, `HeroMoveIntent`, `HeroStatsComponent` |
| Apariencia | `HeroAppearanceComponent`, `HeroVisualReference`, `HeroVisualInstance` |
| Propiedad táctica | `HeroSquadSelectionComponent`, `HeroSquadReference` |
| Multijugador simulado | `TeamComponent`, `IsLocalPlayer` o componentes AI |
| Squads no activos | `DynamicBuffer<InactiveSquadElement>` |

`HeroSquadReference.squad` apunta a la entidad del squad activo.

### 11.3 Entidades estáticas de definición de squad

Cada `SquadData` horneado produce una entidad consultable por `SquadDataIDComponent.id`. Contiene:

- stats y módulos de combate en `SquadDataComponent`;
- identidad/configuración en `SquadDefinitionComponent`;
- tipo y comportamiento en `SquadStatsComponent`;
- prefab de unidad como `Entity`;
- biblioteca blob de formaciones;
- buffer de stats base.

Las entidades de definición no son las instancias desplegadas. `HeroSquadSelectionComponent.squadDataEntity`
apunta a la definición horneada. El spawn copia los datos a la escuadra desplegada y establece
`SquadDataReference.dataEntity = squad`: esta segunda referencia apunta a la propia instancia.
Ambos bakers comparten `SquadDefinitionBaker<T>`, registran los blobs y crean el buffer de habilidades.

### 11.4 Entidad de squad desplegado

Composición conceptual:

| Área | Componentes/buffers representativos |
|---|---|
| Identidad runtime | `SquadInstanceComponent` |
| Propiedad | `SquadOwnerComponent.hero` |
| Definición | `SquadDataReference`, `SquadDefinitionComponent` |
| Miembros | `DynamicBuffer<SquadUnitElement>` |
| Progreso | `SquadProgressComponent`, abilities desbloqueadas |
| Orden/estado | `SquadStateComponent`, `SquadFSMComponent`, intents y orden resuelta |
| Formación | `FormationComponent`, formación activa, anchor y patrón |
| Navegación | `SquadNavigationComponent`, `NavAgentComponent` |
| Combate | `SquadCombatComponent`, targets y reacción |
| Swap/retirada | cooldown, channeling, tags y requests de cambio |

### 11.5 Entidades de unidad

Cada soldado es una entidad temporal perteneciente a un squad:

```text
UnitOwnerComponent
├── squad: Entity
└── hero: Entity
```

El squad mantiene la relación inversa mediante `DynamicBuffer<SquadUnitElement>`. El primer elemento del
buffer se considera líder.

Componentes principales:

- `UnitStatsComponent` y, si aplica, `UnitRangedStatsComponent`;
- `HealthComponent`, `DefenseComponent`, penetración, escudo y arma;
- `UnitCombatComponent` con target y cooldown;
- slot, estado y orientación de formación;
- destinos y navegación;
- componentes de sincronización visual y animación.

Las unidades no son persistentes. Su estado se destruye con el World ECS salvo las agregaciones que otros
sistemas copien explícitamente a persistencia.

### 11.6 Mapa, objetivos y partida

| Entidad conceptual | Componentes principales |
|---|---|
| Spawn point | `SpawnPointComponent`: ID, team, posición, activo |
| Zona | `ZoneTriggerComponent`: ID, tipo, dueño, radio, lock y final |
| Capture point | `CapturePointTag`, `CapturePointProgressComponent` |
| Supply point | `SupplyPointTag`, `SupplyPointComponent` |
| Registro de zonas | `ZoneManagerSingleton` + `ZoneReferenceBuffer` |
| Estado del match | `MatchStateComponent` |
| Fase global | `GameStateComponent` |

`ZoneLinkComponent.requiredZoneId` expresa dependencias de desbloqueo entre zonas mediante un `int`, no una
referencia `Entity`.

### 11.7 Eventos efímeros

El proyecto modela varias órdenes y eventos como entidades/componentes de vida corta:

- `PendingDamageEvent`;
- `ProjectileSpawnRequest`;
- `XPEventComponent` y `LevelUpEvent`;
- `SquadChangeEvent`, `SquadSwapRequest` y tags de ejecución;
- eventos de zona capturada;
- requests de selección de spawn;
- mensajes de chat ECS.

No constituyen persistencia ni un bus de red. Son mecanismos internos entre sistemas del mismo World ECS.

## 12. Relaciones autoritativas actuales

### 12.1 Persistencia y catálogos

| Origen | Campo | Destino | Tipo de enlace |
|---|---|---|---|
| `PlayerData` | `heroes[]` | `HeroData` | Composición |
| `HeroData` | `classId` | `HeroClassDefinition` | String; la resolución depende de servicios/assets |
| `HeroData` | `availableSquads[]` | `SquadData.id` | String |
| `HeroData` | `loadouts[]` | `LoadoutSaveData` | Composición |
| `HeroData` | `squadProgress[]` | `SquadInstanceData` | Composición |
| `LoadoutSaveData` | `squadInstanceIDs[]` | `SquadInstanceData.id` | String |
| `SquadInstanceData` | `baseSquadID` | `SquadData.id` | String |
| `HeroData` | `inventory[]` | `InventoryItem` | Composición |
| `InventoryItem` | `itemId` | `ItemDataSO.id` | String |
| `HeroData` | `equipment` | `Equipment` | Composición |
| `Equipment` | campos de slot | `InventoryItem` | Objeto completo embebido |
| `AvatarParts` | IDs de partes | `AvatarPartDefinition.id` | String |

### 12.2 Runtime ECS

| Origen | Campo/buffer | Destino | Cardinalidad |
|---|---|---|---|
| Hero | `HeroSquadReference.squad` | Squad activo | 0..1 |
| Squad | `SquadOwnerComponent.hero` | Hero propietario | 1 |
| Squad | `SquadUnitElement[]` | Units | 0..N |
| Unit | `UnitOwnerComponent.squad` | Squad | 1 |
| Unit | `UnitOwnerComponent.hero` | Hero | 1 |
| Hero | `InactiveSquadElement[]` | Estado agregado de squads no activos | 0..N |
| Squad instance | `SquadDataReference.dataEntity` | La propia instancia, con copia de los datos horneados | 1 |
| Unit combat | `target` | Hero o Unit enemigo | 0..1 |
| Zone manager | `ZoneReferenceBuffer[]` | Zonas | 0..N |

Las referencias `Entity` sólo son válidas dentro del World ECS que las creó. No son IDs persistentes ni deben
cruzar un contrato de red.

## 13. Estado actual del contrato de red

No existe un contrato de red implementado en este commit.

La clase `MatchmakingService` declara que gestiona comunicación con backend, pero su implementación actual:

1. espera un tiempo aleatorio local;
2. llama a `BattleDebugCreator.CreateBattleWithLocalHero`;
3. emite `OnBattleAssigned(BattleData)` dentro del proceso.

No se encontraron en `Assets/Scripts`:

- `NetworkBehaviour`, `NetworkObject` o primitivas de Unity Netcode;
- RPC de servidor/cliente;
- cliente HTTP para matchmaking o batalla;
- WebSocket/socket de gameplay;
- serialización versionada de `BattleData`;
- comandos, snapshots o resultados con envelope de protocolo.

Por tanto, `BattleData` es un DTO interno de transición local, no un contrato wire.

### 13.1 Por qué las clases actuales no son directamente un contrato de red

| Problema | Estado actual |
|---|---|
| Referencias Unity | `BattleData.mapData` es un `MapDataSO` |
| Identidad de héroe | Se usa `heroName` para buscar al participante |
| Identidad de squad | Cambia de GUID string a índice `int` secuencial |
| Spawn | `string` en `BattleHeroData`, `int` en ECS |
| Equipo | Se copian objetos `InventoryItem` completos |
| Mutabilidad | `BattleHeroData` reutiliza instancias del modelo persistente |
| Versionado | No hay `schemaVersion`, versión de catálogo o build compatible |
| Autoridad | No se distingue dato enviado por cliente de dato validado por servidor |
| Resultado | No existe `BattleResult` versionado e idempotente |

## 14. Matriz actual de identidades

| Concepto | Tipo actual | Ámbito real | Observación |
|---|---|---|---|
| Jugador local | `playerID: int` y `playerName: string` | Sesión/ECS | No existe un ID global persistente inequívoco |
| Héroe | `heroName: string` | Perfil/batalla | Nombre visible usado como llave |
| Clase | `classId`/`classID: string` y `HeroClass` enum | Persistencia/batalla/ECS | Naming y representación no uniformes |
| Instancia de squad | `SquadInstanceData.id: string` | Persistencia | Se genera como GUID |
| Squad runtime | `id: int` + `persistentId: FixedString64Bytes` | World ECS / correlación persistente | Índice temporal y GUID separados |
| Definición de squad | `baseSquadID`/`SquadDataIDComponent.id: string` | Catálogo/build | Es el enlace más estable del flujo actual |
| Item definición | `itemId: string` | Catálogo/build | Separado de la instancia |
| Item instancia | `instanceId: string?` | Inventario del héroe | Null para stackables |
| Mapa | `MapDataSO.mapId: string` | Catálogo/build | `BattleData` transporta el asset, no sólo el ID |
| Spawn | `string` → `int` | BattleData → ECS | Conversión con fallback silencioso |
| Zona | `zoneId: int` | Escena/World ECS | Puede enlazarse a otra zona mediante int |
| Entidad ECS | `Entity` | World ECS | Temporal; nunca persistente |

## 15. Deudas y riesgos estructurales observados

Esta sección registra hechos del estado actual; no decide todavía su solución.

### A-01. Dos fuentes de persistencia local

`player_save.json` y `player_progress.json` mantienen progresión, loadouts y squads con esquemas distintos y
consumidores activos.

### A-02. `HeroData` es un agregado muy amplio

Identidad, economía, atributos, progreso, inventario, equipo, avatar, perks y squads comparten una misma forma
serializada. Las interfaces reducen el acoplamiento de consumidores, pero no separan el almacenamiento.

### A-03. Identidad de héroe basada en nombre

`BattleData.findHeroDataByName` y `playerSide` dependen de `heroName`. Nombres duplicados o renombrados pueden
romper la correlación.

### A-04. Remapeo con pérdida de identidad de squad — corregido en 1.1

El GUID se conserva en `persistentId`. Los índices enteros locales permanecen para selección y swap.
La persistencia antigua sin GUID no se asocia automáticamente a una instancia moderna por coincidencia de índice.

### A-05. Tipos de ID incompatibles

El mismo dominio usa strings, enums e ints según la capa. Hay además diferencias de casing:
`classId`/`classID`, `baseSquadID`, `selectedSquadBaseID`.

### A-06. Referencias de Unity dentro del DTO de batalla

`BattleData.mapData` es un asset `MapDataSO`. Esto funciona entre escenas del mismo cliente, pero no define una
representación portable.

### A-07. Equipo embebido y potencialmente duplicado

Los slots de `Equipment` contienen `InventoryItem` completos en vez de referenciar la instancia del
inventario.

### A-08. Doble ruta de bake para squads

Existen dos puntos de entrada, pero la conversión está unificada en `SquadDefinitionBaker<T>`.
La escena y los prefabs todavía deben evitar registrar dos veces una misma definición.

### A-09. Definición y runtime no son isomorfos

Los ScriptableObjects contienen sprites, prefabs, curvas y módulos; ECS contiene valores aplanados, blobs y
entidades. La conversión es código ejecutable y puede omitir campos o aplicar precedencias.

### A-10. No hay esquema ni compatibilidad versionada

Los JSON locales dependen del layout de campos públicos y los DTO de batalla no anuncian versión. Tampoco se
registra versión de catálogos o balance junto a una batalla.

### A-11. Referencia potencialmente rota en el catálogo de items

`ItemSODatabase.asset` contiene una referencia GUID sin `.meta` correspondiente en el árbol verificado.

## 16. Fronteras que deben conservarse

Según la arquitectura híbrida vigente:

- los ScriptableObjects son definiciones estáticas, no estado mutable de partida;
- las clases persistentes representan datos duraderos, no entidades ECS;
- `BattleData` es una proyección temporal y no debe confundirse con el perfil completo;
- las referencias `Entity` y los componentes visuales son internas al runtime;
- GameObjects/MonoBehaviours no deben mutar directamente el estado ECS;
- `EntityVisualSync` y los registros de prefabs son la frontera ECS → visual;
- cualquier contrato con BronzeAge debe usar DTO neutrales, sin `UnityEngine.Object` ni `Entity`.

## 17. Fuentes de código principales

### Persistencia y sesión

- `Assets/Scripts/Data/Persistence/Player.Data.cs`
- `Assets/Scripts/Data/Persistence/Hero.Data.cs`
- `Assets/Scripts/Data/Persistence/LoadoutSave.Data.cs`
- `Assets/Scripts/Data/Persistence/SquadInstance.Data.cs`
- `Assets/Scripts/Data/Persistence/inventoryItem.Data.cs`
- `Assets/Scripts/Data/Persistence/Equiment.Data.cs`
- `Assets/Scripts/Shared/LocalSave.System.cs`
- `Assets/Scripts/Core/SaveSystem.cs`
- `Assets/Scripts/Core/LoadSystem.cs`
- `Assets/Scripts/Core/PlayerSessionService.cs`

### Catálogos

- `Assets/Scripts/Squads/SquadData.cs`
- `Assets/Scripts/Data/SquadDatabase.cs`
- `Assets/Scripts/Data/Items/ItemDataSO.cs`
- `Assets/Scripts/Data/Items/EnhancedItemDatabase.cs`
- `Assets/Scripts/Hero/HeroClassDefinition.cs`
- `Assets/Scripts/Data/Maps/MapDataSO.cs`
- `Assets/Scripts/Data/Maps/MapDatabase.cs`
- `Assets/Scripts/Data/Avatar/AvatarPartDatabase.cs`

### Batalla y puente

- `Assets/Scripts/Data/Battle/Battle.Data.cs`
- `Assets/Scripts/Data/Battle/BattleHero.Data.cs`
- `Assets/Scripts/Services/MatchmakingService.cs`
- `Assets/Scripts/Debug/BattleDebugCreator.cs`
- `Assets/Scripts/UI/BattlePreparation/BattleTransitionData.cs`
- `Assets/Scripts/UI/Battle/BattleSceneController.cs`

### ECS

- `Assets/Scripts/Shared/DataContainer.Component.cs`
- `Assets/Scripts/Squads/SquadIdMapElement.cs`
- `Assets/Scripts/Hero/Components/`
- `Assets/Scripts/Squads/`
- `Assets/Scripts/Squads/Components/`
- `Assets/Scripts/Combat/`
- `Assets/Scripts/Map/`

## 18. Relación con la arquitectura compartida

Este documento es la línea base de Conquest. El diseño compartido propuesto define otra separación:

```text
entidad persistente autoritativa de BronzeAge
        │
        ▼
DTO de red neutral y versionado
        │
        ▼
adaptador de Conquest
        │
        ▼
entidades ECS/GameObjects temporales
```

Las clases `HeroData`, `SquadInstanceData`, `BattleData` y los componentes ECS aquí inventariados son entradas
para ese trabajo, no el contrato compartido definitivo.

## 19. Regla de mantenimiento

Cuando cambie alguna estructura documentada:

1. subir la versión y fecha de este documento;
2. actualizar el commit de verificación;
3. añadir una fila al historial;
4. revisar ambos modelos de persistencia;
5. revisar la conversión `HeroData → BattleHeroData → DataContainer/ECS`;
6. revisar la matriz de identidades;
7. volver a contar los assets registrados si cambia un catálogo;
8. distinguir claramente hechos actuales de propuestas futuras.

Un documento de arquitectura sin commit de verificación debe considerarse potencialmente obsoleto.
