# EquipmentGrantEffect - Documentación

## Descripción
El `EquipmentGrantEffect` es un efecto de ítem que permite otorgar piezas de equipment específicas al héroe. Este efecto:

- Crea una nueva instancia del equipment especificado
- La agrega automáticamente al inventario del héroe
- Genera stats aleatorios para el equipment (usando el ItemStatGenerator correspondiente)
- Maneja validaciones y eventos apropiados

## Configuración en el Inspector

### Campos Principales
- **Target Item Id**: ID del ítem de equipment a otorgar (debe existir en ItemDatabase)
- **Allow Duplicates**: Si permite múltiples instancias del mismo equipment
- **Force Add To Inventory**: Fuerza agregar al inventario (no usado actualmente)

### Validaciones
- **Validate Item Exists**: Verifica que el ítem existe en la base de datos
- **Require Inventory Space**: Verifica que hay espacio en el inventario antes de otorgar

## Uso

### 1. Crear el ScriptableObject
```csharp
// En el menú de Unity: Create > Items > Effects > Equipment Grant
```

### 2. Configurar el efecto
1. Asignar un **Target Item Id** válido (ej: "WeaBBasic", "TorLADef")
2. Configurar las opciones de validación según necesidades
3. Opcionalmente configurar si permite duplicados

### 3. Usar en un ítem consumible
1. Crear/editar un `ItemData` consumible
2. Agregar el `EquipmentGrantEffect` al array de `effects`
3. El ítem ahora otorgará el equipment cuando se use

## Ejemplo de Configuración

```csharp
// Configurar programáticamente
var effect = CreateInstance<EquipmentGrantEffect>();
effect.SetTargetItem("WeaBBasic"); // ID de una espada
effect.SetAllowDuplicates(false);   // Solo una por héroe
```

## Casos de Uso

### 1. Poción que otorga equipment
Un consumible "Cofre de Armas" que otorga una espada aleatoria:
- `EquipmentGrantEffect` con `targetItemId = "WeaBBasic"`
- `allowDuplicates = true` para permitir múltiples espadas

### 2. Recompensa de quest
Un ítem de quest que otorga armadura específica:
- `EquipmentGrantEffect` con `targetItemId = "TorLADef"`
- `allowDuplicates = false` para evitar duplicados

### 3. Kit de inicio
Un ítem que otorga equipment completo para nuevos jugadores:
- Múltiples `EquipmentGrantEffect` en el mismo ítem
- Cada uno con different `targetItemId`

## Validaciones Automáticas

El efecto incluye validaciones automáticas:

1. **Existencia del ítem**: Verifica que el `targetItemId` existe en `ItemDatabase`
2. **Es equipment**: Confirma que el ítem es equipment (tiene `ItemStatGenerator`)
3. **Espacio en inventario**: Verifica que hay espacio disponible
4. **Duplicados**: Respeta la configuración de `allowDuplicates`

## Prioridad de Ejecución

El `EquipmentGrantEffect` tiene **alta prioridad (80)** para ejecutarse antes que otros efectos como monedas o experiencia.

## Logging

El efecto incluye logging detallado:
- Éxitos en la creación de equipment
- Fallos por falta de espacio
- Errores de configuración
- Validaciones fallidas

## Limitaciones

1. Solo funciona con ítems de tipo equipment
2. Requiere que el sistema de inventario esté inicializado
3. Respeta el límite máximo del inventario
4. No puede otorgar equipment pre-existente (siempre crea nuevas instancias)

## Troubleshooting

### "Target item not found in database"
- Verificar que el `targetItemId` existe en `ItemDatabase`
- Revisar que no hay errores de escritura en el ID

### "Target item is not equipment"
- Confirmar que el ítem tiene un `ItemStatGenerator` asignado
- Verificar que `ItemData.IsEquipment` retorna `true`

### "No space available in inventory"
- El inventario del héroe está lleno
- Liberar espacio o aumentar `InventoryLimit`

### "Equipment already exists and duplicates not allowed"
- El héroe ya tiene este equipment y `allowDuplicates = false`
- Cambiar configuración o verificar lógica de negocio
