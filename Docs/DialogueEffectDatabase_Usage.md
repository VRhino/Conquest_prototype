# Guía Completa: DialogueEffectDatabase

## ¿Cómo Funciona?

El **DialogueEffectDatabase** es un sistema centralizado que gestiona todos los efectos de diálogo disponibles en el juego. Funciona como un **singleton** que carga automáticamente desde la carpeta Resources.

### Arquitectura del Sistema:

```
DialogueEffectDatabase (ScriptableObject)
├── Array de DialogueEffect[] 
├── Dictionary<string, DialogueEffect> (para búsqueda rápida)
└── Métodos estáticos para acceso global
```

## Configuración Paso a Paso

### 1. Crear la Base de Datos (Una sola vez)

```bash
# En Unity Editor:
Right-click en Assets/Resources/ 
> Create > Dialogue > Dialogue Effect Database
> Nombrar: "DialogueEffectDatabase"
```

**⚠️ IMPORTANTE**: El archivo DEBE llamarse exactamente `DialogueEffectDatabase` y estar en `Assets/Resources/` para que el singleton funcione.

### 2. Crear Efectos Individuales

```bash
# Para cada tipo de efecto:
Right-click en Assets/Scripts/Dialogue/Effects/
> Create > Dialogue > Effects > [Tipo de Efecto]
```

**Tipos Disponibles:**
- **Add Item Effect** → Agregar ítems al inventario
- **Give Experience Effect** → Dar experiencia al héroe  
- **Unlock Squad Effect** → Desbloquear nuevos escuadrones

### 3. Configurar IDs Únicos

Para cada efecto que crees, debes configurar:

#### Campos Obligatorios:
- **Effect ID**: Identificador único (ej: "AddWeapon", "GiveExperience")
- **Display Name**: Nombre legible (ej: "Add Weapon", "Give Experience")
- **Description**: Descripción del efecto

#### Ejemplos de IDs Recomendados:

```csharp
// Para AddItemDialogueEffect:
Effect ID: "AddWeapon"     // Para armas
Effect ID: "AddArmor"      // Para armaduras  
Effect ID: "AddPotion"     // Para pociones

// Para GiveExperienceDialogueEffect:
Effect ID: "SmallXP"       // 50-100 XP
Effect ID: "MediumXP"      // 100-250 XP
Effect ID: "LargeXP"       // 250+ XP

// Para UnlockSquadDialogueEffect:
Effect ID: "UnlockElites"  // Desbloquear élites
Effect ID: "UnlockArchers" // Desbloquear arqueros
```

### 4. Registrar en la Database

Una vez creados los efectos:

1. Seleccionar el `DialogueEffectDatabase` en `Assets/Resources/`
2. En el Inspector, expandir **Dialogue Effects**
3. Cambiar **Size** al número de efectos que tienes
4. Arrastrar cada efecto individual a los slots

### 5. Verificar Configuración

El sistema incluye validaciones automáticas:

```csharp
// Verificar en Inspector:
[ContextMenu("Log Debug Info")]
// Mostrará todos los efectos registrados

// Verificar en código:
bool exists = DialogueEffectDatabase.HasEffect("AddWeapon");
DialogueEffect effect = DialogueEffectDatabase.GetEffect("AddWeapon");
```

## Uso Práctico

### En DialogueOption Data:

```csharp
DialogueOption option = new DialogueOption
{
    optionText = "Accept reward",
    optionType = DialogueOptionType.ExecuteEffects,
    
    // ¡ESTOS son los IDs que configuraste!
    dialogueEffectIds = new string[] { 
        "AddWeapon",    // Debe coincidir exactamente con Effect ID
        "MediumXP"      // Debe coincidir exactamente con Effect ID
    },
    
    effectParameters = new DialogueParameters
    {
        stringParameter = "WeaS&SBasic", // ItemID para AddWeapon
        intParameter = 150,              // XP amount para MediumXP
        floatParameter = 0f,
        boolParameter = false
    }
};
```

### Flujo de Ejecución:

```csharp
// 1. NpcTriggerZone recibe el DialogueOption
// 2. Extrae los dialogueEffectIds: ["AddWeapon", "MediumXP"]
// 3. DialogueEffectDatabase.GetEffects(ids) busca cada ID
// 4. DialogueEffectSystem.ExecuteDialogueEffects() ejecuta en orden
```

## Convenciones de Nombres

### Para Effect IDs (recomendado):

```csharp
// Patrón: [Acción][Objeto][Variación]
"AddWeapon"      // Agregar arma básica
"AddWeaponRare"  // Agregar arma rara
"GiveXPSmall"    // Dar XP pequeña cantidad
"GiveXPLarge"    // Dar XP gran cantidad
"UnlockSquad"    // Desbloquear escuadrón básico
"UnlockElite"    // Desbloquear escuadrón élite
```

### Para Display Names:

```csharp
"Add Weapon"           // Legible para desarrolladores
"Give Experience"      // Descriptivo
"Unlock Elite Squad"   // Específico
```

## Debugging y Troubleshooting

### Comandos de Debug:

```csharp
// En cualquier script:
Debug.Log(DialogueEffectDatabase.GetDebugInfo());

// Verificar si existe un efecto:
if (!DialogueEffectDatabase.HasEffect("MiEfecto"))
{
    Debug.LogError("Efecto no encontrado: MiEfecto");
}

// Listar todos los IDs:
string[] allIds = DialogueEffectDatabase.GetAllEffectIds();
```

### Problemas Comunes:

#### ❌ "DialogueEffectDatabase not found in Resources folder!"
**Solución**: Crear el archivo en `Assets/Resources/DialogueEffectDatabase.asset`

#### ❌ "Dialogue effect not found: [ID]"
**Solución**: 
1. Verificar que el Effect ID esté configurado correctamente en el ScriptableObject
2. Verificar que el efecto esté agregado al array en DialogueEffectDatabase
3. Verificar que no haya typos en dialogueEffectIds

#### ❌ "Duplicate dialogue effect ID: [ID]"
**Solución**: Cambiar el Effect ID de uno de los efectos duplicados

## Ejemplo Completo

### Crear un comerciante que vende armas:

1. **Crear el efecto**:
   ```bash
   Create > Dialogue > Effects > Add Item Effect
   Nombre: "SellBasicSword"
   ```

2. **Configurar el efecto**:
   ```csharp
   Effect ID: "SellBasicSword"
   Display Name: "Sell Basic Sword"
   Description: "Sells a basic sword to the hero"
   ```

3. **Agregarlo a la Database**:
   - Seleccionar DialogueEffectDatabase
   - Aumentar Size en 1
   - Arrastrar "SellBasicSword" al nuevo slot

4. **Usar en NPC**:
   ```csharp
   new DialogueOption
   {
       optionText = "Buy Basic Sword (100 gold)",
       optionType = DialogueOptionType.ExecuteEffects,
       dialogueEffectIds = new string[] { "SellBasicSword" },
       effectParameters = new DialogueParameters
       {
           stringParameter = "WeaS&SBasic", // Item ID
           intParameter = 1,                // Quantity
           floatParameter = 100f,           // Cost
           boolParameter = true             // Subtract gold
       }
   }
   ```

## Ventajas del Sistema ID

✅ **Flexibilidad**: Puedes reutilizar el mismo efecto con diferentes parámetros
✅ **Modularidad**: Efectos separados, fáciles de mantener
✅ **Debugging**: Fácil identificar qué efecto causó un problema
✅ **Performance**: Búsqueda O(1) por Dictionary
✅ **Validation**: Sistema detecta IDs duplicados o faltantes automáticamente

El sistema está diseñado para ser fácil de usar desde Unity Editor sin tocar código. ¡Los diseñadores pueden crear nuevos efectos de diálogo simplemente configurando ScriptableObjects!
