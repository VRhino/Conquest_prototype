# Sistema de Drag and Drop para Inventario

## Descripción General

El sistema de drag and drop permite a los jugadores mover ítems dentro de la grilla del inventario arrastrándolos con el mouse. Cuando se arrastra un ítem, se muestra una miniatura del ítem en el cursor, y al soltarlo sobre otra celda, el ítem se mueve a esa posición.

## Componentes Principales

### 1. InventoryDragHandler.cs
- **Propósito**: Maneja toda la lógica de drag and drop para una celda individual
- **Funciones principales**:
  - Detecta el inicio, movimiento y fin del arrastre
  - Crea y maneja el visual que sigue al cursor durante el drag
  - Comunica con el InventoryPanelController para realizar el intercambio de ítems

### 2. InventoryItemCellController.cs (Modificado)
- **Nuevas funciones**:
  - Integración automática con InventoryDragHandler
  - Métodos para establecer y obtener el índice de celda
  - Comunicación con el drag handler cuando se asignan/limpian ítems

### 3. InventoryPanelController.cs (Modificado)
- **Nuevas funciones**:
  - Método `SwapItems()` para intercambiar ítems entre celdas
  - Asignación automática de índices de celda durante la creación de la grilla
  - Mapeo entre índices visuales e índices reales del inventario

## Configuración en Unity

### Requisitos del Prefab
Para que el drag and drop funcione correctamente, el prefab de `inventoryItemCellPrefab` debe tener:

1. **Un Collider o Image Component** para detectar raycast
2. **RectTransform** correctamente configurado
3. **InventoryItemCellController** componente
4. **El InventoryDragHandler se agrega automáticamente** en runtime

### Configuración del Canvas
- El Canvas padre debe tener **GraphicRaycaster** componente
- Recomendado: Canvas con **Canvas Scaler** para diferentes resoluciones

## Funcionamiento del Sistema

### Flujo de Drag and Drop

1. **Inicio del Drag (OnBeginDrag)**:
   - Se verifica que hay un ítem válido en la celda
   - Se crea un visual temporal que sigue al cursor
   - La celda original se vuelve semi-transparente

2. **Durante el Drag (OnDrag)**:
   - El visual sigue la posición del mouse
   - Se mantiene al frente de otros elementos UI

3. **Fin del Drag (OnEndDrag)**:
   - Se busca la celda objetivo bajo el cursor
   - Si se encuentra una celda válida, se llama a `SwapItems()`
   - Se destruye el visual temporal
   - Se restaura la transparencia de la celda original

### Intercambio de Ítems

El método `SwapItems()` maneja tres casos:

1. **Intercambio entre dos celdas con ítems**: Los ítems cambian de posición
2. **Mover ítem a celda vacía**: El ítem se mueve a la nueva posición
3. **Celda vacía a celda vacía**: No se realiza ningún cambio

## Características Técnicas

### Visual de Drag
- **Tamaño**: 72x72 píxeles (igual que las celdas)
- **Transparencia**: 80% opacity durante el drag
- **Sprite**: Usa la miniatura del ítem original
- **Color**: Mantiene el color de rareza del ítem
- **Comportamiento**: No interfiere con raycast (blocksRaycasts = false)

### Integración con Filtros
- El sistema respeta los filtros activos del inventario
- Los índices visuales se mapean correctamente a índices reales del inventario
- Los intercambios funcionan incluso con filtros aplicados

### Persistencia Automática
- Todos los cambios se guardan automáticamente usando el sistema de persistencia
- Se emiten eventos de inventario después de cada intercambio
- La UI se actualiza automáticamente

## Eventos Emitidos

Después de un intercambio exitoso:
- `InventoryEvents.OnInventoryChanged`
- Guardado automático via `SaveSystem`

## Debugging y Logs

El sistema incluye logs detallados:
- Inicio y fin de drag operations
- Errores de validación de índices
- Intercambios exitosos
- Problemas de configuración

## Ejemplo de Uso

```csharp
// El sistema funciona automáticamente una vez configurado
// No se requiere código adicional para uso básico

// Para acceder programáticamente:
var panelController = GetComponent<InventoryPanelController>();
panelController.SwapItems(sourceIndex, targetIndex);
```

## Notas de Implementación

### Resolución de Dependencias
- El sistema usa reflexión para evitar errores de compilación durante el desarrollo
- Los componentes se agregan automáticamente en runtime
- Manejo robusto de errores si algún componente no está disponible

### Rendimiento
- El visual de drag se crea/destruye solo durante el arrastre
- Se utiliza pooling implícito del sistema de UI de Unity
- Raycast optimizado usando GraphicRaycaster

### Compatibilidad
- Compatible con todos los filtros del inventario
- Funciona con ítems stackeable y no stackeable
- Respeta las validaciones del sistema de inventario

## Posibles Mejoras Futuras

1. **Animaciones**: Añadir tweening suave para los intercambios
2. **Feedback Visual**: Resaltar celdas válidas durante el drag
3. **Restricciones**: Implementar reglas especiales para ciertos tipos de ítems
4. **Multi-selección**: Permitir arrastrar múltiples ítems simultáneamente
5. **Drag fuera del inventario**: Equipar ítems arrastrándolos a slots de equipo
