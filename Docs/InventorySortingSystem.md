# Sistema de Ordenamiento de Inventario - Documentación

## Descripción
El sistema de ordenamiento de inventario permite organizar automáticamente todos los items del héroe según criterios predefinidos, combinando stacks duplicados y compactando el espacio disponible.

## Funcionalidad Principal

### `InventoryManager.SortByType()`
Método principal que ejecuta el ordenamiento completo del inventario.

**Orden de Clasificación:**
1. **Por Tipo** (prioridad principal):
   - **Armaduras**: Helmet → Torso → Gloves → Pants → Boots
   - **Armas**: Weapon
   - **Consumibles**: Consumable → Visual

2. **Por Rareza** (dentro de cada tipo):
   - Legendary → Epic → Rare → Uncommon → Common

3. **Por Nombre** (criterio final para consistencia)

### Proceso de Ordenamiento

#### Paso 1: Combinación de Stackables
- Busca items stackables con el mismo `itemId`
- Los combina usando `ItemInstanceService.StackItems()`
- Remueve duplicados del inventario
- Solo afecta items con `IsStackable = true`

#### Paso 2: Ordenamiento
- Aplica algoritmo de ordenamiento estable
- Respeta la jerarquía: Tipo → Rareza → Nombre
- Mantiene la individualidad de equipment único

#### Paso 3: Compactación
- Reasigna `slotIndex` secuencialmente desde 0
- Elimina espacios vacíos
- Optimiza el uso del espacio del inventario

#### Paso 4: Persistencia
- Actualiza `_currentHero.inventory`
- Dispara eventos de cambio
- Guarda automáticamente los cambios

## Restricciones de Uso

### Solo con Filtro "All"
- El sort **solo funciona** cuando `_currentFilter == ItemFilter.All`
- Con otros filtros, el botón de sort se desactiva automáticamente
- Esto previene confusión visual y problemas de estado

### Validaciones
- Verifica que el sistema esté inicializado
- Requiere un héroe válido
- Maneja errores graciosamente con logging

## Interfaz de Usuario

### Botón de Sort
- **Activo**: Solo cuando filtro = "All"
- **Inactivo**: Con filtros Equipment o Stackable
- **Visual**: El botón se desactiva automáticamente

### Feedback al Usuario
- Logging detallado en consola
- Mensajes de éxito/error
- Información de combinar stacks

## Casos de Uso

### 1. Inventario Desordenado
**Antes:**
```
[Poción] [Espada Epic] [Casco Common] [Poción] [Espada Rare]
```

**Después:**
```
[Casco Common] [Espada Epic] [Espada Rare] [Poción x2]
```

### 2. Items Stackables Duplicados
**Antes:**
```
[Poción x3] [Armadura] [Poción x2] [Pergamino x1]
```

**Después:**
```
[Armadura] [Poción x5] [Pergamino x1]
```

### 3. Equipment Único
Los equipment mantienen su individualidad:
```
[Espada Legendary A] [Espada Legendary B] [Espada Epic C]
```

## API Pública

### Métodos Principales
```csharp
// Ejecutar sort completo
bool success = InventoryManager.SortByType();

// Verificar si se puede hacer sort
bool canSort = inventoryPanel.CanPerformSort();

// Trigger desde UI
inventoryPanel.SortInventory();
```

### Métodos Helper (Privados)
```csharp
CombineStackableItems()           // Combinar stacks
GetItemTypePriority(ItemType)     // Prioridad por tipo
GetRarityPriority(ItemRarity)     // Prioridad por rareza
```

## Performance

### Complejidad
- **Tiempo**: O(n log n) donde n = número de items
- **Espacio**: O(n) para arrays temporales
- **Optimizaciones**: Single-pass para combinación, in-place updates

### Consideraciones
- Operación relativamente rápida para inventarios típicos (< 100 items)
- No afecta performance durante gameplay normal
- Se ejecuta solo cuando el usuario lo solicita

## Integración con Otros Sistemas

### InventoryEventService
- Dispara `OnInventoryChanged` después del sort
- Mantiene la integridad de datos
- Guarda automáticamente

### InventoryUIUtils
- Se integra con `RefreshFullUI()`
- Actualiza visualización automáticamente
- Mantiene tooltips coherentes

### Filtros de UI
- Respeta el sistema de filtros existente
- Solo funciona con filtro "All"
- Preserva el estado de filtros después del sort

## Troubleshooting

### "Sort no funciona"
- Verificar que el filtro esté en "All"
- Confirmar que hay items en el inventario
- Revisar logs de consola para errores

### "Items no se combinan"
- Verificar que sean del mismo `itemId`
- Confirmar que `IsStackable = true`
- Revisar configuración en `ItemData`

### "Orden incorrecto"
- Verificar configuración de `ItemType` en base de datos
- Confirmar valores de `ItemRarity`
- Revisar lógica de prioridades

## Extensibilidad

### Agregar Nuevos Criterios
Para agregar nuevos criterios de ordenamiento:

1. Modificar el `Sort()` comparison delegate
2. Agregar nuevos métodos `GetXPriority()`
3. Actualizar documentación

### Personalización por Usuario
Futuras mejoras podrían incluir:
- Múltiples tipos de ordenamiento
- Configuración de criterios
- Orden personalizado por usuario
