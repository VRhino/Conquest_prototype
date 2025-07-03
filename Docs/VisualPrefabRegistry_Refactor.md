# Visual Prefab Registry - Arquitectura Data-Driven

## 📋 Resumen

El `VisualPrefabRegistry` ha sido refactorizado para soportar una arquitectura data-driven que escala a N unidades y héroes sin necesidad de modificar código.

## 🔧 Arquitectura Anterior vs Nueva

### ❌ Arquitectura Anterior (Hardcodeada)
```csharp
public class VisualPrefabRegistry : MonoBehaviour
{
    [SerializeField] private GameObject _unitEscudero;
    [SerializeField] private GameObject _unitArquero;
    [SerializeField] private GameObject _unitPikemen;
    [SerializeField] private GameObject _unitCaballo;
    [SerializeField] private GameObject _heroSynty;
    
    // Métodos específicos para cada tipo
    public GameObject GetUnitEscudero() => _unitEscudero;
    public GameObject GetUnitArquero() => _unitArquero;
    // ...
}
```

**Problemas:**
- Campos hardcodeados para tipos específicos
- Necesidad de modificar código para agregar nuevas unidades
- Mantenimiento manual de métodos específicos
- No escalable

### ✅ Arquitectura Nueva (Data-Driven)
```csharp
public class VisualPrefabRegistry : MonoBehaviour
{
    [SerializeField] private VisualPrefabConfiguration config;
    private Dictionary<string, GameObject> _runtimePrefabCache;
    
    // Métodos genéricos que funcionan con cualquier tipo
    public GameObject GetHeroPrefab(string heroId);
    public GameObject GetUnitPrefab(SquadType squadType, string unitId = null);
    public GameObject GetDefaultUnitPrefab(SquadType squadType);
}
```

**Ventajas:**
- Configuración externa mediante ScriptableObject
- Soporta N unidades/héroes dinámicamente
- No requiere modificar código para agregar tipos
- Cache de runtime para rendimiento
- Validación automática de configuración

## 🗂️ Componentes de la Arquitectura

### 1. VisualPrefabConfiguration (ScriptableObject)
```csharp
[CreateAssetMenu(fileName = "VisualPrefabConfig", menuName = "Conquest Tactics/Visual Prefab Configuration")]
public class VisualPrefabConfiguration : ScriptableObject
{
    [SerializeField] private HeroPrefabEntry[] heroPrefabs;
    [SerializeField] private UnitPrefabEntry[] unitPrefabs;
    [SerializeField] private GenericPrefabEntry[] additionalPrefabs;
}
```

**Características:**
- Definición de prefabs en datos externos
- Entradas separadas para heroes, unidades y prefabs adicionales
- Soporte para variantes de unidades
- Configuración de prefabs por defecto
- Validación de configuración integrada

### 2. VisualPrefabRegistry (Singleton)
```csharp
public class VisualPrefabRegistry : MonoBehaviour
{
    private static VisualPrefabRegistry _instance;
    private Dictionary<string, GameObject> _runtimePrefabCache;
    
    public static VisualPrefabRegistry Instance { get; }
}
```

**Características:**
- Singleton para acceso global
- Cache de runtime para búsquedas rápidas
- Inicialización automática al inicio
- Compatibilidad con claves legacy
- Registro dinámico de prefabs en runtime

## 🎯 Uso de la Nueva Arquitectura

### Configuración de Prefabs
1. Crear un asset de `VisualPrefabConfiguration`
2. Configurar las entradas de heroes y unidades
3. Asignar el config al `VisualPrefabRegistry`

### Acceso a Prefabs desde Código
```csharp
// Obtener prefab por defecto
GameObject heroPrefab = VisualPrefabRegistry.Instance.GetDefaultHeroPrefab();
GameObject unitPrefab = VisualPrefabRegistry.Instance.GetDefaultUnitPrefab(SquadType.Squires);

// Obtener prefab específico
GameObject specificHero = VisualPrefabRegistry.Instance.GetHeroPrefab("Knight");
GameObject specificUnit = VisualPrefabRegistry.Instance.GetUnitPrefab(SquadType.Archers, "EliteArcher");

// Obtener variantes
GameObject[] variants = VisualPrefabRegistry.Instance.GetUnitVariants(SquadType.Squires, "BasicSquire");
```

### Registro Dinámico
```csharp
// Registrar prefabs en runtime
VisualPrefabRegistry.Instance.RegisterPrefab("CustomUnit", customPrefab);
```

## 📊 Beneficios de la Refactorización

### 1. Escalabilidad
- **Antes:** Agregar nueva unidad = modificar código + recompilación
- **Después:** Agregar nueva unidad = configurar en ScriptableObject

### 2. Mantenibilidad
- **Antes:** Múltiples campos y métodos específicos
- **Después:** API unificada que funciona con cualquier tipo

### 3. Flexibilidad
- **Antes:** Estructura rígida predefinida
- **Después:** Configuración dinámica con variantes y alternativos

### 4. Rendimiento
- **Antes:** Búsquedas por reflection o comparaciones string
- **Después:** Cache de diccionario O(1) para búsquedas

### 5. Validación
- **Antes:** Errores de configuración solo en runtime
- **Después:** Validación automática con warnings/errors específicos

## 🔍 Compatibilidad Legacy

Para facilitar la transición, se mantiene compatibilidad con claves legacy:

```csharp
private string GetLegacyUnitKey(SquadType squadType)
{
    return squadType switch
    {
        SquadType.Squires => "UnitEscudero",
        SquadType.Archers => "UnitArquero", 
        SquadType.Pikemen => "UnitPikemen",
        SquadType.Lancers => "UnitCaballo",
        _ => null
    };
}
```

## 🚀 Extensiones Futuras

La nueva arquitectura permite fácilmente:
- Agregar nuevos tipos de unidades
- Configurar múltiples variantes por tipo
- Implementar sistemas de unlock/progression
- Soportar modding/customización de prefabs
- Implementar temas visuales intercambiables

## 📋 Checklist de Migración

Para proyectos existentes:
- [ ] Crear `VisualPrefabConfiguration` asset
- [ ] Migrar prefabs existentes a la configuración
- [ ] Actualizar referencias de código a usar la nueva API
- [ ] Probar que todos los prefabs se cargan correctamente
- [ ] Validar configuración usando `ValidateConfiguration()`
- [ ] Eliminar código legacy cuando sea seguro
