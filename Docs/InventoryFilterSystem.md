# Sistema de Filtros de Inventario - Documentación

## Descripción
El sistema de filtros de inventario permite mostrar selectivamente diferentes tipos de items, ocultando temporalmente aquellos que no coinciden con el filtro seleccionado.

## Funcionalidad Principal

### Tipos de Filtros Disponibles
```csharp
public enum ItemFilter
{
    All,        // Muestra todos los items en sus posiciones originales
    Equipment,  // Solo muestra items equipables (armas, armaduras)
    Stackable   // Solo muestra items stackeables (consumibles)
}
```

### Comportamiento de Filtros

#### **Filtro "All" (Por Defecto)**
- **Comportamiento**: Muestra todos los items en sus posiciones originales (`slotIndex`)
- **Drag & Drop**: ✅ **HABILITADO** - Se pueden mover items libremente
- **Sort**: ✅ **HABILITADO** - Se puede ordenar el inventario
- **Persistencia**: Las posiciones se mantienen según `slotIndex` real

#### **Filtros "Equipment" y "Stackable"**
- **Comportamiento**: Compacta items filtrados desde slot 0 consecutivamente
- **Drag & Drop**: ❌ **DESHABILITADO** - Previene movimientos accidentales
- **Sort**: ❌ **DESHABILITADO** - Solo funciona con filtro "All"
- **Persistencia**: Solo visual, no afecta `slotIndex` reales

## Arquitectura Técnica

### Flujo de Filtrado

```
Usuario hace clic en botón filtro
    ↓
ApplyFilter(filter) actualiza _currentFilter
    ↓
RefreshFullUI() → UpdateInventoryDisplay()
    ↓
GetFilteredItems() aplica lógica de filtrado
    ↓
Items se muestran según comportamiento del filtro
```

### Métodos Clave

#### `GetFilteredItems()`
```csharp
private List<InventoryItem> GetFilteredItems()
{
    var allItems = InventoryManager.GetAllItems();
    
    switch (_currentFilter)
    {
        case ItemFilter.All:
            return allItems;
        case ItemFilter.Equipment:
            return allItems.Where(item => InventoryUtils.IsEquippableType(item.itemType)).ToList();
        case ItemFilter.Stackable:
            return allItems.Where(item => InventoryUtils.IsStackable(item.itemId)).ToList();
        default:
            return allItems;
    }
}
```

#### `UpdateInventoryDisplay()` - Lógica Principal
```csharp
private void UpdateInventoryDisplay()
{
    var itemsToShow = GetFilteredItems();
    
    if (_currentFilter == ItemFilter.All)
    {
        // Mantener posiciones originales
        foreach (var item in itemsToShow)
        {
            if (item.slotIndex >= 0 && item.slotIndex < totalCells)
                slotItems[item.slotIndex] = item;
        }
    }
    else
    {
        // Compactar desde slot 0
        for (int i = 0; i < itemsToShow.Count && i < totalCells; i++)
            slotItems[i] = itemsToShow[i];
    }
}
```

## Control de Interacciones

### Sistema de Validación

#### **Drag & Drop**
- **Método**: `CanPerformDragDrop()` → Solo true con filtro "All"
- **Implementación**: `InventoryDragHandler` consulta antes de iniciar drag
- **Feedback**: Log de advertencia cuando se intenta drag con filtros activos

#### **Sort**
- **Método**: `CanPerformSort()` → Solo true con filtro "All" + héroe válido
- **Implementación**: `SortInventory()` valida antes de ejecutar
- **UI**: Botón sort se desactiva automáticamente con filtros

#### **Botones de Filtro**
- **Método**: `UpdateFilterButtons()` 
- **Comportamiento**: El botón del filtro activo se desactiva (no clickeable)
- **Visual**: Indica claramente qué filtro está activo

## Casos de Uso

### 1. Ver Solo Equipment
```
Estado: Inventario con mezcla de armas, armaduras y consumibles
Acción: Click en botón "Equipment"
Resultado: Solo se muestran armas y armaduras, compactados desde slot 0
```

### 2. Ver Solo Stackables
```
Estado: Inventario con items variados
Acción: Click en botón "Stackable" 
Resultado: Solo se muestran consumibles y items stackeables
```

### 3. Intentar Drag con Filtro Activo
```
Estado: Filtro "Equipment" activo
Acción: Intentar arrastrar un item
Resultado: Drag no se inicia, se muestra warning en consola
```

### 4. Agregar Item con Filtro Activo
```
Estado: Filtro "Equipment" activo (no muestra consumibles)
Acción: Usar consumible que otorga una poción al inventario
Resultado: La poción se agrega al inventario real pero no se muestra hasta cambiar filtro
```

## Beneficios del Diseño

### ✅ **Solo Visual - No Destructivo**
- Los filtros nunca modifican `slotIndex` reales
- El inventario persistente permanece intacto
- Cambiar filtros es instantáneo y seguro

### ✅ **Reutilización de Código Existente**
- `GetFilteredItems()` ya existía y funcionaba perfectamente
- `InventoryUtils.IsEquippableType()` y `InventoryUtils.IsStackable()` reutilizados
- Lógica de UI (`UpdateFilterButtons()`) ya implementada

### ✅ **Prevención de Errores**
- Drag & Drop deshabilitado previene movimientos accidentales
- Sort deshabilitado evita inconsistencias visuales
- Validaciones claras con feedback al usuario

### ✅ **Compatibilidad Total**
- No afecta sistema de sort existente
- No afecta persistencia de datos
- Compatible con todos los sistemas existentes (tooltips, eventos, etc.)

## Limitaciones y Consideraciones

### **Filtros Son Solo Visuales**
- No hay feedback de cuántos items están ocultos
- Items agregados con filtros activos no se muestran inmediatamente

### **Restricciones de Interacción**
- Drag & Drop solo con filtro "All" (por diseño)
- Sort solo con filtro "All" (por consistencia)

### **Dependencias**
- Requiere `InventoryUtils.IsEquippableType()` y `InventoryUtils.IsStackable()`
- Requiere `InventoryManager.GetAllItems()`
- Integrado con sistema de drag & drop existente

## Testing y Validación

### Casos de Prueba Recomendados
1. **Filtro All**: Verificar que todos los items se muestran en posiciones originales
2. **Filtro Equipment**: Solo armas/armaduras, compactadas desde slot 0
3. **Filtro Stackable**: Solo consumibles, compactadas desde slot 0
4. **Drag Disabled**: Intentar drag con filtros activos (debe fallar)
5. **Sort Disabled**: Botón sort desactivado con filtros
6. **Add Item**: Agregar item con filtro que no coincide (no debe mostrarse)
7. **Filter Switching**: Cambio rápido entre filtros (debe ser fluido)

### Logs de Debug
- `[InventoryPanelController] Filtro aplicado: {filter}`
- `[InventoryPanelController] Display updated with filter '{filter}': showing {count} items`
- `[InventoryDragHandler] Drag & Drop is disabled when filters are active`

## Conclusión

El sistema de filtros implementado es:
- **Seguro**: No modifica datos persistentes
- **Eficiente**: Reutiliza código existente al máximo
- **Intuitivo**: Comportamiento predecible y consistente
- **Robusto**: Previene errores mediante validaciones
- **Extensible**: Fácil agregar nuevos tipos de filtros en el futuro
