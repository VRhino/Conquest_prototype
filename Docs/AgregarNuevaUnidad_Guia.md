# Guía: Agregar Nueva Unidad Visual y ECS

## 📋 Resumen

Esta guía te muestra paso a paso cómo agregar una nueva unidad al juego, incluyendo tanto el prefab visual como su contraparte ECS, usando la nueva arquitectura data-driven.

## 🎯 Requisitos Previos

- Unity abierto con el proyecto
- Conocimiento básico de prefabs y ECS
- Tener el modelo 3D/visual de la nueva unidad

## 🔧 Paso 1: Crear el Prefab Visual

### 1.1 Importar el Modelo
1. Importa tu modelo 3D a Unity (drag & drop a la carpeta `Assets/Art/Models/`)
2. Configura las opciones de importación según necesites

### 1.2 Crear el Prefab Visual
1. Arrastra el modelo a la escena
2. Agrégale los componentes necesarios:
   - `Animator` (si tiene animaciones)
   - `MeshRenderer` y `MeshFilter` (si no los tiene)
   - Cualquier componente visual específico
3. Configura los materiales y texturas
4. Guarda como prefab en `Assets/Prefabs/Units/Visual/`
5. Nombra el prefab: `[TipoUnidad]_Visual` (ej: `Knight_Visual`)

### 1.3 Ejemplo de Estructura
```
Knight_Visual (GameObject)
├── Model (GameObject con MeshRenderer/MeshFilter)
├── Animator (Component)
├── AudioSource (Component, opcional)
└── Particles (GameObject, opcional)
```

## 🔧 Paso 2: Crear el Prefab ECS

### 2.1 Crear el Prefab ECS
1. Crea un GameObject vacío en la escena
2. Nómbralo: `[TipoUnidad]_ECS` (ej: `Knight_ECS`)
3. Agrega el componente `UnitEntityAuthoring`
4. Configura las propiedades de la unidad:
   - **Squad Type**: Selecciona o crea un nuevo tipo
   - **Unit Stats**: Configura vida, daño, velocidad, etc.
   - **Formation Settings**: Posición en formación
   - **Combat Settings**: Configuración de combate

### 2.2 Configuración del UnitEntityAuthoring
```csharp
// Ejemplo de configuración
Squad Type: Knights          // Nuevo tipo de escuadrón
Health: 150                 // Vida de la unidad
Damage: 25                  // Daño base
Speed: 3.5                  // Velocidad de movimiento
Armor: 5                    // Armadura
Range: 1.5                  // Alcance de ataque
```

### 2.3 Guardar el Prefab ECS
1. Guarda como prefab en `Assets/Prefabs/Units/ECS/`
2. Elimina el GameObject de la escena

## 🔧 Paso 3: Agregar el Nuevo Tipo de Escuadrón (si es necesario)

### 3.1 Modificar SquadType Enum
1. Abre `Assets/Scripts/Squads/SquadTypes.cs`
2. Agrega el nuevo tipo al enum:

```csharp
public enum SquadType
{
    Squires,
    Archers,
    Pikemen,
    Spearmen,
    Knights    // ← Nuevo tipo
}
```

### 3.2 Actualizar Referencias
Si hay código que usa switch statements con SquadType, actualízalo para incluir el nuevo tipo.

## 🔧 Paso 4: Configurar en VisualPrefabConfiguration

### 4.1 Abrir la Configuración
1. En el Project, busca tu `VisualPrefabConfiguration` asset
2. Selecciónalo para verlo en el Inspector

### 4.2 Agregar Nueva Entrada de Unidad
En la sección **Unit Visual Prefabs**, agrega una nueva entrada:

- **Squad Type**: `Knights` (tu nuevo tipo)
- **Unit Id**: `Basic` (o un ID específico como `HeavyKnight`)
- **Display Name**: `Caballero Pesado`
- **Visual Prefab**: Arrastra tu prefab `Knight_Visual`
- **Is Default**: ✅ Marcado (si es la unidad por defecto de este tipo)
- **Variants**: Arrastra variantes si las tienes

### 4.3 Ejemplo de Configuración
```
Squad Type: Knights
Unit Id: Basic
Display Name: Caballero Pesado
Visual Prefab: Knight_Visual
Is Default: ✅
Variants: [Knight_Elite_Visual, Knight_Veteran_Visual]
```

## 🔧 Paso 5: Configurar en el Sistema de Spawn

### 5.1 Actualizar SquadSpawningSystem
Si tu sistema de spawn usa lógica específica para tipos, actualízalo:

```csharp
// En SquadSpawningSystem.cs, busca métodos que usen SquadType
private Entity CreateSquadEntity(SquadType squadType)
{
    // Asegúrate de que el nuevo tipo esté manejado
    switch (squadType)
    {
        case SquadType.Squires:
            return CreateSquireSquad();
        case SquadType.Archers:
            return CreateArcherSquad();
        case SquadType.Pikemen:
            return CreatePikemenSquad();
        case SquadType.Spearmen:
            return CreateSpearmenSquad();
        case SquadType.Knights:  // ← Agregar nuevo caso
            return CreateKnightSquad();
        default:
            return CreateSquireSquad();
    }
}
```

### 5.2 Implementar el Método de Creación
```csharp
private Entity CreateKnightSquad()
{
    // Lógica específica para crear escuadrón de caballeros
    // Usar el prefab ECS Knight_ECS
    return InstantiateSquadFromPrefab("Knight_ECS");
}
```

## 🔧 Paso 6: Probar la Nueva Unidad

### 6.1 Validar Configuración
1. Ejecuta el juego
2. Revisa la consola para logs de `[VisualPrefabRegistry]`
3. Deberías ver: `Cached unit prefab: Unit_Knights_Basic`

### 6.2 Crear Escuadrón de Prueba
1. Usa tu sistema de spawn para crear un escuadrón del nuevo tipo
2. Verifica que:
   - El prefab ECS se instancia correctamente
   - El prefab visual se sincroniza
   - La unidad se comporta como esperado

### 6.3 Debugging
Si hay problemas:
- Verifica que el SquadType esté agregado al enum
- Confirma que el prefab ECS tenga UnitEntityAuthoring
- Revisa que el prefab visual esté asignado en la configuración
- Checa los logs de error en la consola

## 📊 Ejemplo Completo: Agregar "Mago"

### Estructura de Archivos
```
Assets/
├── Art/Models/Mage_Model.fbx
├── Prefabs/
│   ├── Units/
│   │   ├── ECS/Mage_ECS.prefab
│   │   └── Visual/Mage_Visual.prefab
└── Scripts/Squads/SquadTypes.cs (modificado)
```

### Configuración en VisualPrefabConfiguration
```
Squad Type: Mages
Unit Id: Basic
Display Name: Mago Básico
Visual Prefab: Mage_Visual
Is Default: ✅
```

### Código Actualizado
```csharp
// En SquadTypes.cs
public enum SquadType
{
    Squires,
    Archers,
    Pikemen,
    Spearmen,
    Mages    // ← Nuevo
}

// En SquadSpawningSystem.cs
case SquadType.Mages:
    return CreateMageSquad();
```

## 🎯 Mejores Prácticas

### Nomenclatura
- Prefabs ECS: `[TipoUnidad]_ECS`
- Prefabs Visual: `[TipoUnidad]_Visual`
- Unit IDs: `Basic`, `Elite`, `Veteran`, etc.

### Organización
- Mantén los prefabs organizados en carpetas por tipo
- Usa nombres descriptivos y consistentes
- Documenta configuraciones especiales

### Testing
- Siempre prueba la nueva unidad en combate
- Verifica que las animaciones funcionen
- Confirma que el rendimiento sea aceptable

## 🔧 Troubleshooting

### Problemas Comunes

**Error: "Prefab visual no encontrado"**
- Verifica que el prefab esté asignado en VisualPrefabConfiguration
- Confirma que el Squad Type coincida exactamente

**Error: "Unit Entity Authoring no encontrado"**
- Asegúrate que el prefab ECS tenga el componente UnitEntityAuthoring
- Verifica que el prefab esté en la carpeta correcta

**Error: "Enum no reconocido"**
- Confirma que agregaste el nuevo tipo al enum SquadType
- Recompila el proyecto si es necesario

**Visual no se sincroniza**
- Verifica que VisualPrefabRegistry esté en la escena
- Confirma que la configuración esté asignada al Registry
- Revisa los logs de inicialización

## 🚀 Extensiones Avanzadas

### Variantes de Unidad
Para agregar variantes (Elite, Veteran, etc.):
1. Crea prefabs visuales adicionales
2. Agrega entradas separadas en la configuración
3. Usa diferentes Unit IDs para cada variante

### Unidades con Habilidades Especiales
Para unidades con habilidades únicas:
1. Crea componentes ECS específicos
2. Agrega el componente al prefab ECS
3. Implementa sistemas que manejen las habilidades

### Animaciones Personalizadas
Para unidades con animaciones especiales:
1. Configura el Animator Controller
2. Agrega componentes de animación necesarios
3. Implementa sistemas de animación si es necesario

---

¡Tu nueva unidad está lista para la batalla! 🏰⚔️
