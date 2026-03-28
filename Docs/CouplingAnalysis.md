# Análisis de Acoplamiento — Codebase Completo

> Fecha: 2026-03-28
> Scope: todos los subsistemas fuera de Squad/Unit movement (ya documentado en SquadCombatRefactor_Architecture.md)

---

## Resumen ejecutivo

| Severidad | Cantidad | Áreas |
|-----------|----------|-------|
| CRÍTICO | 4 | Inventory, Hero Visual, AI Perception, VisualPrefabRegistry |
| ALTO | 5 | Hero↔Squad coupling, Hero.Data god object, AI sin abstracción, DataCacheService, SamplePlayerAnimationController |
| MEDIO | 6 | Combate+animación, detección hardcoded, magic numbers, UI violations, eventos inconsistentes |
| BAJO | 2 | GameTags, HeroClassDefinition |

---

## CRÍTICO

---

### C-01 — Inventory services acoplados estáticamente a HeroData

**Archivos:**
- `Assets/Scripts/Inventory/InventoryManager.cs`
- `Assets/Scripts/Inventory/Services/EquipmentManagerService.cs`
- `Assets/Scripts/Inventory/Services/InventoryStorageService.cs`
- `Assets/Scripts/Inventory/Services/ConsumableManagerService.cs`

**Problema:** Los cuatro servicios de inventario mantienen una referencia estática `_currentHero: HeroData` y acceden directamente a los slots de equipamiento muteando la estructura:

```csharp
private static HeroData _currentHero;
private static readonly Dictionary<(ItemType, ItemCategory), Func<InventoryItem>> _equipmentGetters = new()
{
    { (ItemType.Weapon, ItemCategory.None), () => _currentHero.equipment.weapon },
    { (ItemType.Armor, ItemCategory.Helmet), () => _currentHero.equipment.helmet },
    // ... 6 slots más accedidos directamente
};
```

Los `ItemEffect` concretos (`EquipmentGrantEffect`, `ConsumableManagerService`, etc.) también mutan `HeroData` directamente en su `Execute()`.

**Por qué causa problemas:**
- Cualquier cambio en la estructura de `HeroData.equipment` rompe los 4 servicios simultáneamente
- Imposible testear inventario sin una instancia de `HeroData`
- Multi-héroe o inventario de squad requiere refactor total de los 4 servicios
- El inventario no puede funcionar en contexto headless (servidor)

**Fix sugerido:** Introducir interfaz `IEquippable` que exponga solo los slots necesarios. Los servicios dependen de la interfaz, no de `HeroData` concreto.

---

### C-02 — BattleWorldState.System.cs — God object de percepción AI (319 LOC)

**Archivo:** `Assets/Scripts/Hero/AI/Systems/BattleWorldState.System.cs`

**Problema:** Un solo sistema de 319 líneas agrega estado de percepción leyendo directamente **9+ componentes** de dos subsistemas distintos:

- Del subsistema Hero: `HeroHealthComponent`, `HeroLifeComponent`, `StaminaComponent`, `HeroSquadReference`, `HeroCombatComponent`
- Del subsistema Squad: `SquadDataComponent`, `SquadAIComponent`, `SquadStateComponent`, `SquadOwnerComponent`, `SquadFormationAnchorComponent`

Construye un `TeamWorldState` usando `List<T>` manejado (GC pressure cada frame), y los sistemas de decisión AI (`HeroAIBalanced`, `HeroAIRusher`) dependen directamente de la estructura interna del blackboard sin ninguna capa de abstracción.

**Por qué causa problemas:**
- Un cambio en cualquier componente de Hero o Squad obliga a modificar el sistema de percepción AI y potencialmente los dos sistemas de comportamiento
- No se puede añadir un nuevo tipo de héroe o squad sin tocar el sistema de percepción
- Los comportamientos AI no son intercambiables (no hay interfaz `IHeroBehavior`)
- 9+ `ComponentLookup` cacheados = overhead de mantenimiento alto

**Fix sugerido:** Capa de abstracción de percepción. `BattleWorldState` escribe a un `HeroAIPerceptionSnapshot` (componente plano, sin referencias a otros componentes). Los sistemas de comportamiento leen solo el snapshot, nunca los componentes originales.

---

### C-03 — HeroVisualManagement.System.cs — 6 responsabilidades, anti-patrones críticos

**Archivo:** `Assets/Scripts/Hero/Systems/HeroVisualManagement.System.cs`

**Problema:** El sistema tiene al menos 6 responsabilidades distintas mezcladas:
1. Instanciación de prefab visual
2. Setup de `CharacterController` vs `NavMeshAgent`
3. Aplicación de personalización visual (apariencia, equipamiento)
4. Configuración del adaptador de animación
5. Setup de `EntityVisualSync`
6. Gestión de eventos de equipamiento

Además usa anti-patrones graves:
- `Object.FindObjectsOfType<GameObject>()` (búsqueda global, no testeable, lenta)
- Dependencia hardcoded de `VisualPrefabRegistry.Instance` (singleton)
- Acceso estático a `InventoryManager.IsInitialized`
- Asume estructura específica del prefab (`EcsAnimationInputAdapter` debe existir como hijo)

**Por qué causa problemas:**
- No puede correr en jobs (acceso a MonoBehaviours)
- Imposible testear unitariamente
- Cambiar la estructura del prefab visual rompe el sistema
- Añadir un nuevo tipo de héroe visual requiere modificar este sistema

**Fix sugerido:** Separar en: `HeroVisualSpawnSystem` (instanciación), `HeroVisualSetupSystem` (configuración CC/NavMesh), `HeroAppearanceSystem` (customización). Eliminar `FindObjectsOfType` con entity queries. Inyectar el registry en lugar de singleton.

---

### C-04 — VisualPrefabRegistry — Singleton global, punto único de fallo

**Archivo:** `Assets/Scripts/Hero/VisualPrefabRegistry.cs`

**Problema:** Singleton MonoBehaviour con `DontDestroyOnLoad`. Cinco archivos dependen de él directamente:
- `HeroVisualManagement.System.cs`
- `SquadVisualManagement.System.cs`
- `SquadDetailPanel.UI.cs`
- `VisualPrefab.Authoring.cs`

Tiene hacks de compatibilidad legacy (`"Synty"` → `"HeroSynty"`) indicando deuda de esquema acumulada.

**Por qué causa problemas:**
- Cualquier test que instancie estos sistemas necesita el singleton inicializado
- Si el registro no está listo cuando un sistema lo necesita → NullReferenceException silenciosa
- UI y ECS acoplados al mismo ciclo de vida del MonoBehaviour
- Añadir un nuevo tipo de visual requiere registrarlo aquí

**Fix sugerido:** Convertir a componente singleton ECS (`VisualPrefabRegistryComponent` con BlobAssetReference). Los sistemas lo buscan via query, no via `Instance`. La UI accede via un servicio ligero, no directamente al registry ECS.

---

## ALTO

---

### A-01 — Hero.Data — God object de persistencia (config + estado runtime mezclados)

**Archivo:** `Assets/Scripts/Data/Persistence/Hero.Data.cs`

**Problema:** Una sola clase de 95 líneas mezcla:
- Identidad y config: `classId`, `level`, `xp`
- Economía: `bronze`, `silver`, `gold`
- Atributos de juego: `strength`, `dexterity`, `armor`, `vitality`
- Progresión: `unlockedPerks`, `attributePoints`, `perkPoints`
- Loadouts y squads: `availableSquads`, `squadProgress`, `loadouts`
- Inventario y equipamiento: referencias a colecciones de items
- Visual: `AvatarCustomization`

Usado como DTO de persistencia Y como objeto de estado runtime mutado por servicios.

**Por qué causa problemas:**
- 15+ archivos dependen de esta clase — un cambio de nombre de campo es un cambio global
- Serialización de persistencia acoplada al modelo de dominio
- Mutación del estado de moneda/atributos dispersa en múltiples servicios sin punto central
- No hay separación entre "lo que se guarda" y "lo que se usa en runtime"

**Fix sugerido:** Separar en `HeroSaveData` (solo persistencia, serializable) + `HeroRuntimeState` (estado en memoria durante la sesión). `PlayerSessionService` mantiene el runtime, `HeroDataService` traduce entre ambos.

---

### A-02 — AI Behavior sin interfaz — no intercambiable

**Archivos:**
- `Assets/Scripts/Hero/AI/Systems/HeroAIRusher.System.cs`
- `Assets/Scripts/Hero/AI/Systems/HeroAIBalanced.System.cs`
- `Assets/Scripts/Hero/AI/Systems/HeroAIPerception.System.cs`

**Problema:** Los tres sistemas de comportamiento son clases concretas no polimórficas. Ambos leen directamente del `HeroAIBlackboard` (estructura concreta) sin ninguna interfaz intermedia. Añadir un tercer comportamiento (`Tactician`, `Defensive`, etc.) requiere:
1. Crear un nuevo sistema concreto
2. Modificar `HeroAIExecutionSystem` para enrutar hacia el nuevo sistema
3. Recompilar todo el pipeline de AI

**Fix sugerido:** Interfaz `IHeroBehavior` con método `Decide(in HeroAIBlackboard blackboard, ref HeroAIDecision decision)`. Los sistemas de comportamiento implementan la interfaz. `HeroAIExecutionSystem` llama a la implementación activa sin conocer el tipo concreto.

---

### A-03 — DataCacheService — estado estático global con eventos propios

**Archivo:** `Assets/Scripts/Core/DataCacheService.cs`

**Problema:** Clase estática con diccionarios estáticos de estado y un sistema de eventos propio (`OnHeroCacheUpdated`) que bypasea el sistema de eventos unificado del proyecto. `PlayerSessionService` y múltiples UI controllers lo llaman directamente.

**Por qué causa problemas:**
- Estado global mutable no testeable en aislamiento
- El evento propio crea un segundo bus de eventos paralelo al sistema central
- Cambios en el esquema de cache afectan a todos los consumidores simultáneamente

**Fix sugerido:** Convertir a instancia inyectable. Unificar eventos con el sistema existente en `Assets/Scripts/Events/`.

---

### A-04 — SamplePlayerAnimationController_ECS — híbrido MonoBehaviour+ECS, 50+ magic strings

**Archivo:** `Assets/Scripts/Hero/SamplePlayerAnimationController_ECS.cs`

**Problema:**
- MonoBehaviour que lee datos ECS directamente — mezcla de capas
- 50+ llamadas `Animator.StringToHash` con strings literales (magic strings de Animator)
- Dependencias serializadas en `HeroCameraController` y `EcsAnimationInputAdapter`
- Ángulos de strafe y thresholds de lean hardcoded
- Asume estructura de animator específica de Synty

**Por qué causa problemas:**
- Renombrar un parámetro del Animator rompe silenciosamente la animación (sin error de compilación)
- No testeable sin un GameObject con Animator completo
- Cambiar la cámara requiere modificar el controlador de animación

**Fix sugerido:** Centralizar los hashes en una clase `HeroAnimatorParams` con constantes. Separar la lógica de cámara. Considerar migrar a `AnimatorControllerPlayable` para eliminar la dependencia del MonoBehaviour.

---

### A-05 — Acoplamiento bidireccional Hero ↔ Squad

**Archivos involucrados:**
- `Assets/Scripts/Hero/Components/HeroSquadReference.Component.cs`
- `Assets/Scripts/Hero/AI/Systems/BattleWorldState.System.cs`
- `Assets/Scripts/Squads/Components/SquadOwner.Component.cs`

**Problema:**
- Hero apunta al Squad: `HeroSquadReference.squadEntity`
- Squad apunta al Hero: `SquadOwnerComponent.hero`
- El sistema de percepción AI lee ambas direcciones en el mismo frame

No hay una dirección clara de ownership. Si la entidad de squad desaparece antes que la del héroe (o viceversa), hay lookups que pueden devolver `Entity.Null` sin manejo explícito.

**Fix sugerido:** Definir ownership unidireccional: Hero es dueño del Squad. Squad referencia al hero SOLO a través de `SquadFormationAnchorComponent` (ya existe, ver `SquadCombatRefactor_Architecture.md` Sprint 3).

---

## MEDIO

---

### M-01 — Daño acoplado a timing de animación

**Archivos:** `Assets/Scripts/Combat/DamageCalculation.System.cs`, `UnitAttack.System.cs`

**Problema:** La ventana de daño se calcula con el timer de animación:

```csharp
bool inWindow = c.attackAnimationTimer >= weapon.strikeWindowStart
             && c.attackAnimationTimer < weapon.strikeWindowStart + weapon.attackAnimationDuration;
```

La lógica de combate es inseparable del estado de animación.

**Impacto:** Imposible simular combate headless (servidor). Cambiar la duración de animación rompe el balance de combate.

**Fix sugerido:** Separar `StrikeWindowComponent` (activado/desactivado por un sistema de animación) del cálculo de daño. El sistema de daño lee el componente, no el timer directamente.

---

### M-02 — EnemyDetection.System hardcodeado para héroes vs unidades

**Archivo:** `Assets/Scripts/Combat/EnemyDetection.System.cs`

**Problema:** El sistema tiene un "PASS 3" explícito que detecta héroes enemigos con una query separada que usa `HeroLifeComponent` como discriminador. Añadir un nuevo tipo de entidad combatiente (boss, summoned unit) requiere añadir un cuarto pass.

**Fix sugerido:** Tag genérico `DetectableEntityTag` (o usar `TeamComponent` ya existente como único discriminador). La detección es uniforme para cualquier entidad con `TeamComponent` + `LocalTransform`.

---

### M-03 — Magic numbers dispersos en sistemas de Hero

| Valor | Sistema | Propósito |
|-------|---------|-----------|
| `15f` | `HeroAttack.System.cs` | Coste de stamina por ataque |
| `20f` | `HeroStamina.System.cs` | Coste de stamina en sprint |
| `1.5f` | `HeroAttack.System.cs` | Multiplicador de golpe crítico |
| `0.5f` | `HeroVisualManagement.System.cs` | NavMesh stoppingDistance |
| `30` frames | `HeroSpawn.System.cs` | Espera antes de verificar spawn |
| `0.0025f` | `HeroState.System.cs` | Threshold de detección de movimiento |

**Fix sugerido:** `HeroGameplayConfig` ScriptableObject con todos estos valores. Los sistemas lo reciben como componente singleton ECS (`HeroGameplayConfigComponent`).

---

### M-04 — UI accede a capas incorrectas

**Archivos:**
- `Assets/Scripts/UI/Battle/HUDController.cs` — lee `EntityManager` de ECS directamente
- `Assets/Scripts/UI/CharacterSelection/HeroSelectionSceneController.cs` — llama `ItemService.GetItemById()` directamente (bypasa la capa de Core services)
- `Assets/Scripts/UI/Squad/SquadDetailPanel.UI.cs` — mantiene estado de juego (`SquadInstanceData`, `SquadData`) como campos del controlador UI

**Impacto:** Los controladores UI están acoplados a implementaciones concretas. Cambiar cómo se almacena el estado del squad o cómo funciona el ECS world requiere modificar la UI.

**Fix sugerido:** Los controladores UI solo hablan con la capa de servicios (`Core/`). Los servicios exponen ViewModels simples, no datos de dominio crudos.

---

### M-05 — Sistema de eventos inconsistente

**Problema:** Existen al menos tres mecanismos de eventos paralelos:
1. `Assets/Scripts/Events/` — sistema centralizado del proyecto
2. `DataCacheService.OnHeroCacheUpdated` — evento propio estático
3. `MatchmakingService` — callbacks propios como MonoBehaviour

`BattlePreparationEvents` existe en el sistema central pero la mayoría de controladores de esa escena no lo usan.

**Impacto:** Añadir una nueva reacción a un evento requiere buscar en los tres sistemas. Debugging de flujo de eventos es opaco.

**Fix sugerido:** Unificar en el sistema de `Assets/Scripts/Events/`. Eliminar eventos custom de `DataCacheService` y `MatchmakingService`.

---

### M-06 — MatchmakingService es MonoBehaviour con lógica de negocio

**Archivo:** `Assets/Scripts/Services/MatchmakingService.cs`

**Problema:** Servicio de negocio implementado como MonoBehaviour. Depende directamente de `SceneTransitionService` para navegar después de encontrar partida.

**Impacto:** Lógica de matchmaking acoplada al ciclo de vida de Unity (`Start`, `Update`, `OnDestroy`). No puede testearse sin una escena activa.

---

## BAJO

---

### B-01 — GameTags cubre solo 2 tags

**Archivo:** `Assets/Scripts/Shared/GameTags.cs`

Solo define `Player` y `Terrain`. Hay strings de tags usados en otros lugares que no están centralizados aquí.

**Fix:** Ampliar con todos los tags usados en el proyecto (`Heroes`, `Units`, `SupplyPoint`, etc.).

---

### B-02 — HeroClassDefinition mezcla gameplay y visual

**Archivo:** `Assets/Scripts/Data/HeroClassDefinition.cs` (ScriptableObject)

Mezcla stats de gameplay (`baseHealth`, `baseStamina`) con identifiers visuales (`visualPrefabId`) y constantes de balance en el mismo asset. Un diseñador que solo quiere cambiar el prefab visual tiene que abrir el mismo asset que define el balance de combate.

**Fix:** Separar en `HeroClassStats` (balance) + `HeroClassVisual` (prefab ids, appearance).

---

## Patrones bien implementados (no tocar)

| Patrón | Archivo | Por qué funciona |
|--------|---------|-----------------|
| `FormationPositionCalculator` | `Squads/FormationPositionCalculator.cs` | Reutilizado en 8 sistemas, sin duplicación |
| `TeamComponent` en detección de enemigos | `EnemyDetection.System.cs` | Genérico, no hardcodea tipos |
| `ItemEffect` polimorfismo | `Data/Items/ItemEffect.cs` | ScriptableObject + subclases concretas, extensible |
| `VisualSyncUtility` | `Shared/VisualSyncUtility.cs` | Reutilizado correctamente por Hero y Squad visual management |
| `HeroPositionUtility` | `Shared/` | Reutilizado en 6+ sistemas sin duplicación |

---

## Orden de abordaje recomendado

Agrupado por impacto en desarrollo futuro y coste de fix:

| Prioridad | ID | Cuándo abordarlo | Prerequisito |
|-----------|----|-----------------|-------------|
| 1 | C-01 | Antes de añadir multi-hero o cualquier feature de inventario | Ninguno |
| 2 | A-01 | Antes de añadir persistencia cloud o replay | Ninguno |
| 3 | C-02 | Antes de añadir nuevo comportamiento AI | Ninguno |
| 4 | A-02 | Al mismo tiempo que C-02 | C-02 |
| 5 | C-04 | Antes de Sprint 5 del refactor de Squad | Ninguno |
| 6 | C-03 | Al mismo tiempo que C-04 | C-04 |
| 7 | A-03 | Junto con M-05 | Ninguno |
| 8 | M-05 | Antes de añadir nuevas escenas con eventos | A-03 |
| 9 | M-01 | Antes de implementar modo servidor/headless | Ninguno |
| 10 | A-04 | Al añadir nuevas animaciones de héroe | Ninguno |
| 11 | M-04 | Continuo — al tocar cada controlador UI | Ninguno |
| 12 | A-05 | Sprint 3 del refactor de Squad (ya planificado) | SquadCombatRefactor Sprint 3 |
| 13-16 | M-02, M-03, M-06, B-01, B-02 | Low hanging fruit, cualquier momento | Ninguno |
