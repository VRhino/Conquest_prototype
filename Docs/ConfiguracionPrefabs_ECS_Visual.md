# Configuración de Prefabs ECS + Visual para Sistema de Animaciones

## Resumen de Arquitectura

En Unity 2022.3.x con Entities 1.3.14, la arquitectura utiliza dos prefabs separados que trabajan juntos. La instanciación y vinculación es **automática**: `HeroSpawnSystem` crea la entidad ECS y `HeroVisualManagementSystem` instancia el visual; para squads, `SquadSpawningSystem` crea las entidades y `SquadVisualManagementSystem` instancia los visuals. `VisualPrefabRegistry` gestiona el lookup de prefabs.

1. **`HeroEntity_pure.prefab`** - Entidad ECS pura (lógica)
2. **`ModularCharacter.prefab`** - Representación visual (GameObject híbrido)

## 🎯 Prefab 1: HeroEntity_pure.prefab (Entidad ECS Pura)

### Configuración Actual ✅
Este prefab ya debería tener los siguientes Authoring Components:

```
HeroEntity_pure.prefab
├── GameObject Root
    ├── HeroAuthoring (script)
    ├── HeroStatsAuthoring
    ├── HeroInputAuthoring
    ├── HeroMovementAuthoring  
    ├── HeroCombatAuthoring
    ├── HeroLifeAuthoring
    ├── StaminaAuthoring
    └── IsLocalPlayerAuthoring
```

### ⚠️ Verificaciones Necesarias

1. **HeroInputAuthoring debe generar HeroInputComponent** con las nuevas propiedades:
```csharp
// Verificar que HeroInputAuthoring genere:
public struct HeroInputComponent : IComponentData
{
    public float2 MoveInput;
    public bool IsSprintPressed;
    public bool IsAttackPressed;
    public bool UseSkill1;
    public bool UseSkill2;
    public bool UseUltimate;
    public bool IsWalkTogglePressed; // ← NUEVO
}
```

2. **Transform debe estar configurado** para la posición inicial del héroe

### 🔧 No Requiere Cambios Adicionales
- ✅ Este prefab mantiene su configuración actual
- ✅ Solo contiene lógica ECS pura
- ✅ No tiene componentes visuales ni de animación

## 🎭 Prefab 2: ModularCharacter.prefab (Visual + Animación)

### Configuración ANTES (Sistema Tradicional)
```
ModularCharacter.prefab
├── Root GameObject
    ├── Animator
    ├── SamplePlayerAnimationController (ANTIGUO)
    ├── InputReader (ANTIGUO)
    ├── SampleCameraController
    ├── CharacterController
    └── Modular Character Assets (Synty)
        ├── Meshes
        ├── Materials
        └── Bones/Skeleton
```

### Configuración DESPUÉS (Sistema ECS Híbrido) ✅

```
ModularCharacter.prefab
├── Root GameObject
    ├── Animator (mantener)
    ├── SamplePlayerAnimationController_ECS (NUEVO) 🔥
    ├── EcsAnimationInputAdapter (NUEVO) 🔥
    ├── HeroCameraController (ACTUALIZADO) 🔥
    ├── CharacterController (mantener)
    ├── EntityVisualSync (NUEVO) 🔥
    └── Modular Character Assets (Synty)
        ├── Meshes (mantener)
        ├── Materials (mantener)
        └── Bones/Skeleton (mantener)
```

### 🔄 Cambios Específicos en ModularCharacter.prefab

#### 1. ELIMINAR Componentes Antiguos
- ❌ **SamplePlayerAnimationController** (original de Synty)
- ❌ **InputReader** (sistema de input tradicional)
- ❌ **SampleCameraController** (reemplazado por HeroCameraController)

#### 2. AGREGAR Componentes Nuevos

##### A) SamplePlayerAnimationController_ECS
```csharp
// Configuración en el Inspector:
[Header("External Components")]
Camera Controller: [Asignar HeroCameraController] ← ACTUALIZADO
Input Adapter: [Asignar EcsAnimationInputAdapter]
Animator: [Asignar Animator del prefab]
Controller: [Asignar CharacterController]

[Header("Player Locomotion")]
Always Strafe: true
Walk Speed: 1.4
Run Speed: 2.5
Sprint Speed: 7.0
Speed Change Damping: 10
Rotation Smoothing: 10
```

##### B) EcsAnimationInputAdapter
```csharp
// Configuración en el Inspector:
[Header("ECS Configuration")]
Auto Find Hero Entity: true
Input Threshold: 0.01

[Header("Debug")]
Enable Debug Logs: false (true para testing)
```

##### C) EntityVisualSync (NUEVO - Crear este script)
```csharp
// Este script sincroniza la posición entre la entidad ECS y el GameObject visual
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

public class EntityVisualSync : MonoBehaviour
{
    [SerializeField] private bool _autoFindHeroEntity = true;
    [SerializeField] private bool _syncPosition = true;
    [SerializeField] private bool _syncRotation = true;
    
    private Entity _heroEntity;
    private EntityManager _entityManager;
    private World _world;
    
    void Start()
    {
        _world = World.DefaultGameObjectInjectionWorld;
        _entityManager = _world.EntityManager;
        
        if (_autoFindHeroEntity)
        {
            FindHeroEntity();
        }
    }
    
    void Update()
    {
        SyncTransformFromEcs();
    }
    
    private void FindHeroEntity()
    {
        var query = _entityManager.CreateEntityQuery(typeof(HeroInputComponent));
        if (query.CalculateEntityCount() > 0)
        {
            _heroEntity = query.GetSingletonEntity();
        }
    }
    
    private void SyncTransformFromEcs()
    {
        if (_heroEntity == Entity.Null || !_entityManager.Exists(_heroEntity))
            return;
            
        var ecsTransform = _entityManager.GetComponentData<LocalTransform>(_heroEntity);
        
        if (_syncPosition)
        {
            transform.position = ecsTransform.Position;
        }
        
        if (_syncRotation)
        {
            transform.rotation = ecsTransform.Rotation;
        }
    }
}
```

#### 3. CONFIGURAR Animator Controller

##### Parámetros a Mantener en el Animator Controller:
- ✅ MovementInputTapped (bool)
- ✅ MovementInputPressed (bool)
- ✅ MovementInputHeld (bool)
- ✅ ShuffleDirectionX (float)
- ✅ ShuffleDirectionZ (float)
- ✅ MoveSpeed (float)
- ✅ CurrentGait (int)
- ✅ StrafeDirectionX (float)
- ✅ StrafeDirectionZ (float)
- ✅ ForwardStrafe (float)
- ✅ CameraRotationOffset (float)
- ✅ IsStrafing (float)
- ✅ IsTurningInPlace (bool)
- ✅ IsWalking (bool)
- ✅ IsStopped (bool)
- ✅ IsStarting (bool)
- ✅ LeanValue (float)
- ✅ HeadLookX (float)
- ✅ HeadLookY (float)
- ✅ BodyLookX (float)
- ✅ BodyLookY (float)
- ✅ LocomotionStartDirection (float)

##### Parámetros eliminados del Animator Controller:
Los siguientes parámetros fueron eliminados por no ser necesarios en el modelo híbrido:
IsJumping, FallingDuration, IsCrouching, IsGrounded, y cualquier parámetro relacionado con aiming/lock-on.

## 🔗 Sincronización Entre Prefabs

### Durante Runtime
1. **HeroEntity_pure.prefab** se instancia como Entity ECS
2. **ModularCharacter.prefab** se instancia como GameObject
3. **EntityVisualSync** conecta ambos automáticamente
4. **EcsAnimationInputAdapter** lee del Entity ECS
5. **SamplePlayerAnimationController_ECS** maneja las animaciones

### Flujo de Datos
```
Input Hardware
    ↓
HeroInputSystem (ECS)
    ↓
HeroInputComponent (Entity)
    ↓
EcsAnimationInputAdapter (GameObject)
    ↓
SamplePlayerAnimationController_ECS (GameObject)
    ↓
Animator (Synty Animations)
```

## 📂 Scripts Necesarios a Crear

### 1. EntityVisualSync.cs
```csharp
// Crear en: Assets/Scripts/Visual/EntityVisualSync.cs
// (Ya mostrado arriba)
```

### 2. Actualizar HeroInputAuthoring (si es necesario)
```csharp
// Verificar que incluya la nueva propiedad:
public bool isWalkTogglePressed;

// En GetComponent():
return new HeroInputComponent
{
    MoveInput = float2.zero,
    IsSprintPressed = false,
    IsAttackPressed = false,
    UseSkill1 = false,
    UseSkill2 = false,
    UseUltimate = false,
    IsWalkTogglePressed = false // ← NUEVO
};
```

## 🎮 Setup en Escena

### Flujo Automatizado (Producción)

El setup de prefabs en escena es **completamente automatizado**. No se requiere colocar manualmente prefabs en la escena ni configurar spawners.

**Héroe:**
```
HeroSpawnSystem (ECS)
    ↓ Crea la entidad ECS pura
HeroVisualManagementSystem (ECS)
    ↓ Detecta entidades sin visual (WithNone<HeroVisualInstance>)
    ↓ Obtiene el prefab visual via VisualPrefabRegistry
    ↓ Instancia el GameObject visual automáticamente
    ↓ Configura EntityVisualSync para sincronización
```

**Squads / Unidades:**
```
SquadSpawningSystem (ECS)
    ↓ Crea las entidades ECS de squad y unidades
SquadVisualManagementSystem (ECS)
    ↓ Detecta entidades de unidad sin visual (WithNone<UnitVisualInstance>)
    ↓ Obtiene el prefab visual via VisualPrefabRegistry
    ↓ Instancia los GameObjects de unidad automáticamente
    ↓ Configura EntityVisualSync por cada unidad
```

Ambos sistemas de visual management consultan `VisualPrefabRegistry` (`Assets/Scripts/Hero/VisualPrefabRegistry.cs`), que a su vez lee la configuración de `VisualPrefabConfiguration` (`Assets/Scripts/Hero/VisualPrefabConfiguration.cs`).

**Requisitos para que funcione:**
1. `VisualPrefabRegistry` debe estar presente como singleton en la escena
2. `VisualPrefabConfiguration` debe tener los prefabs registrados con sus claves
3. Los spawn points deben estar configurados en la escena

## ✅ Checklist de Configuración

### HeroEntity_pure.prefab
- [ ] Mantiene todos los Authoring Components existentes
- [ ] HeroInputAuthoring incluye `IsWalkTogglePressed`
- [ ] Transform configurado en posición inicial
- [ ] No tiene componentes visuales

### ModularCharacter.prefab
- [ ] Eliminado `SamplePlayerAnimationController` original
- [ ] Eliminado `InputReader`
- [ ] Agregado `SamplePlayerAnimationController_ECS`
- [ ] Agregado `EcsAnimationInputAdapter`
- [ ] Agregado `EntityVisualSync`
- [ ] Todas las referencias conectadas en el Inspector
- [ ] Animator Controller limpiado (sin parámetros innecesarios)

### Herramientas de Validación
- [ ] Usar `PrefabConfigurationValidator` para verificar configuración
- [ ] Usar `HybridHeroSpawner` para setup de escena
- [ ] Usar `EcsAnimationTester` para debugging

### Testing
- [ ] Ambos prefabs pueden instanciarse sin errores
- [ ] El input ECS se refleja en las animaciones
- [ ] Las transiciones idle/walk/run/sprint funcionan
- [ ] El sistema de strafe responde correctamente
- [ ] No hay errores en consola relacionados con parámetros faltantes

## 🛠️ Herramientas de Desarrollo Incluidas

### 1. PrefabConfigurationValidator
**Ubicación:** `Assets/Scripts/Testing/PrefabConfigurationValidator.cs`

**Propósito:** Valida que ambos prefabs tengan la configuración correcta.

**Uso:**
1. Agregar el componente a un GameObject en la escena
2. Asignar los prefabs a validar
3. Ejecutar "Validate Configuration" desde el Context Menu
4. Revisar los logs para verificar la configuración

### 2. HeroVisualManagementSystem (Existente)
**Ubicación:** `Assets/Scripts/Hero/HeroVisualManagementSystem.cs`

**Propósito:** Sistema ECS que maneja automáticamente la creación de GameObjects visuales.

**Uso:** 
- Funciona automáticamente después del HeroSpawnSystem
- Crea automáticamente el ModularCharacter.prefab cuando se spawnea una entidad
- No requiere configuración manual

### 3. EcsAnimationTester
**Ubicación:** `Assets/Scripts/Testing/EcsAnimationTester.cs`

**Propósito:** Debug y testing del sistema de animaciones ECS.

**Uso:**
1. Agregar a un GameObject con los componentes de animación
2. Activar "Show Debug Info" para información en tiempo real
3. Usar "Enable Manual Testing" para pruebas manuales con teclado

### 4. HybridSystemMonitor
**Ubicación:** `Assets/Scripts/Testing/HybridSystemMonitor.cs`

**Propósito:** Monitor en tiempo real del estado del sistema híbrido completo.

**Uso:**
1. Agregar a cualquier GameObject en la escena
2. Activar "Show On Screen Info" para overlay visual
3. Los componentes se detectan automáticamente
4. Usar "Log Current Status" para reportes detallados en consola

## 🔧 VisualPrefabRegistry y Configuración

La gestión centralizada de prefabs visuales se realiza a través de:

- **`VisualPrefabRegistry`** (`Assets/Scripts/Hero/VisualPrefabRegistry.cs`): Singleton MonoBehaviour que gestiona el registro, caching y lookup de prefabs visuales en runtime. Es utilizado por `HeroVisualManagementSystem` y `SquadVisualManagementSystem` para obtener los prefabs correctos al instanciar visuals.
- **`VisualPrefabConfiguration`** (`Assets/Scripts/Hero/VisualPrefabConfiguration.cs`): ScriptableObject que define la lista de prefabs disponibles con sus claves de búsqueda (por ejemplo `"HeroSynty"`, `"SquirePrefab"`).

### Convención de nombres para prefabs de unidades

Los prefabs visuales de unidades (esqueletos/skeletons para visualización) siguen la convención `*_GO_Squeleton.prefab`. Ejemplos:
- `Assets/Resources/Squads/Squires/Squires_GO_Squeleton.prefab`
- `Assets/Resources/Squads/Spearmen/Spearmen_GO_Squeleton.prefab`
- `Assets/Resources/Squads/Levy_Archers/Levy_Archer_GO_Squeleton.prefab`

Estos prefabs se ubican bajo `Assets/Resources/Squads/<TipoSquad>/` y son los que `SquadVisualManagementSystem` instancia automáticamente para cada unidad.

## 🚀 Resultado Final

Con esta configuración tendrás:
- **Separación limpia** entre lógica ECS y visuales
- **Input unificado** procesado por ECS
- **Animaciones de calidad** de Synty mantenidas
- **Arquitectura escalable** para múltiples héroes
- **Performance mejorada** con procesamiento batch ECS

¡La configuración híbrida ECS + Visual está lista! 🎉
