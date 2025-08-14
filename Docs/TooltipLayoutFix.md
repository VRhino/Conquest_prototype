# Configuración de Layout para Tooltip del Inventario

## Problemas Identificados

1. **Conflicto entre Layout Groups y Content Size Fitters**: Unity advierte que los hijos de Layout Groups no deberían tener Content Size Fitters
2. **Falta de rebuild del layout**: Cuando el contenido cambia dinámicamente, Unity no recalcula automáticamente los tamaños
3. **Solapamiento de paneles**: Los paneles se solapan cuando el contenido del Content_Panel cambia de tamaño

## Configuración Recomendada para la UI

### Tooltip Panel (Raíz)
```
- RectTransform
- VerticalLayoutGroup:
  * Child Alignment: Upper Left
  * Child Force Expand Width: true
  * Child Force Expand Height: false
  * Spacing: 5-10px
- ContentSizeFitter:
  * Horizontal Fit: Preferred Size
  * Vertical Fit: Preferred Size
```

### Title_Panel
```
- RectTransform
- HorizontalLayoutGroup (opcional, para organizar elementos internos)
- LayoutElement:
  * Min Height: valor fijo (ej: 60px)
  * Preferred Height: valor fijo (ej: 60px)
  * Flexible Height: 0
```

### Content_Panel
```
- RectTransform
- VerticalLayoutGroup:
  * Child Alignment: Upper Left
  * Child Force Expand Width: true
  * Child Force Expand Height: false
  * Spacing: 3-5px
- LayoutElement:
  * Min Height: valor mínimo (ej: 50px)
  * Preferred Height: -1 (para que se calcule automáticamente)
  * Flexible Height: 1 (permite expansion)
```

### Stats_Panel
```
- RectTransform
- VerticalLayoutGroup:
  * Child Alignment: Upper Left
  * Child Force Expand Width: true
  * Child Force Expand Height: false
  * Spacing: 2-3px
- LayoutElement:
  * Min Height: valor mínimo (ej: 20px)
  * Preferred Height: -1
  * Flexible Height: 0
```

### Stat_item (Prefab)
```
- RectTransform
- HorizontalLayoutGroup:
  * Child Alignment: Middle Left
  * Child Force Expand Width: false
  * Child Force Expand Height: true
  * Spacing: 10px
- LayoutElement:
  * Min Height: valor fijo (ej: 20px)
  * Preferred Height: valor fijo (ej: 20px)
```

### stat_name y stat_value (Textos dentro de Stat_item)
```
- RectTransform
- Text (TMP)
- LayoutElement:
  * Min Width: 0
  * Preferred Width: -1 (auto)
  * Flexible Width: 1 (para stat_value) / 0 (para stat_name)
```

### Interaction_Panel
```
- RectTransform
- LayoutElement:
  * Min Height: valor fijo (ej: 30px)
  * Preferred Height: valor fijo (ej: 30px)
  * Flexible Height: 0
```

## Elementos a REMOVER para evitar conflictos

### Content Size Fitters a eliminar:
1. **stat_name y stat_value**: NO deben tener ContentSizeFitter si están dentro de un HorizontalLayoutGroup
2. **Stats_Panel**: REMOVER ContentSizeFitter si está dentro del VerticalLayoutGroup del Content_Panel
3. **Content_Panel**: REMOVER ContentSizeFitter si está dentro del VerticalLayoutGroup del Tooltip Panel

## Configuración de Text Components

### Para todos los textos (TMP_Text):
```
- Overflow: Overflow
- Wrapping: Enabled (para textos largos como descripción)
- Auto Size: Disabled (usar tamaño fijo)
- Font Size: valor fijo apropiado
```

## Orden de Aplicación de Cambios

1. **Eliminar Content Size Fitters conflictivos**
2. **Configurar Layout Elements con valores específicos**
3. **Ajustar Layout Groups con configuraciones recomendadas**
4. **Probar con diferentes tipos de ítems**
5. **Ajustar valores Min/Preferred Height según necesidades**

## Validación

Para validar que la configuración funciona:

1. Probar con ítem consumible (contenido mínimo)
2. Cambiar a ítem de equipment (contenido máximo)
3. Verificar que no hay solapamiento
4. Confirmar que los stats se muestran correctamente horizontalmente
5. Verificar que el tooltip se redimensiona apropiadamente

## Notas Importantes

- Los **Layout Elements** son clave para controlar el tamaño de cada sección
- Los **Content Size Fitters** solo deben estar en el elemento raíz del tooltip
- El **VerticalLayoutGroup** del tooltip principal debe tener Child Force Expand Height = false
- Los **Min Height** evitan que los paneles desaparezcan o se colapsen
- Los **Preferred Height** = -1 permiten cálculo automático del tamaño preferido
