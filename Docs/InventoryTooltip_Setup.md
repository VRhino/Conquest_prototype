# Instrucciones para crear el Tooltip UI en Unity

## Estructura de GameObject necesaria para el Tooltip

Crear la siguiente jerarquía en Unity:

```
InventoryTooltip (GameObject)
├── Canvas (Canvas Component)
│   ├── Tooltip Panel (GameObject con RectTransform)
│   │   ├── Title_Panel (GameObject con RectTransform)
│   │   │   ├── Background (Image Component) 
│   │   │   ├── Divider (Image Component)
│   │   │   ├── Title (Image Component)
│   │   │   └── Miniature (Image Component)
│   │   ├── Content_Panel (GameObject con RectTransform)
│   │   │   ├── Description (Text - TMP_Text Component)
│   │   │   ├── Armor (Text - TMP_Text Component)
│   │   │   ├── Category (Text - TMP_Text Component)
│   │   │   ├── Durability (Text - TMP_Text Component)
│   │   │   └── Stats_Panel (GameObject con RectTransform)
│   │   │       └── Stats Container (GameObject con VerticalLayoutGroup)
│   │   └── Interaction_Panel (GameObject con RectTransform)
│   │       └── Action (Text - TMP_Text Component)
└── InventoryTooltipController (Script Component)
```

## Configuración de Componentes

### Canvas Setup
- **Canvas Component**: 
  - Render Mode: Screen Space - Overlay
  - Sort Order: 100 (para que aparezca sobre el inventario)

### Tooltip Panel
- **RectTransform**: 
  - Anchor: Top-Left
  - Size: Width=300, Height=400 (ajustable)
  - Background: Opcional, color semi-transparente

### Title_Panel
- **Background (Image)**:
  - Component: Image
  - Source Image: Variable según rareza (será asignado por código)
  - Image Type: Sliced (para escalado adecuado)

- **Divider (Image)**:
  - Component: Image
  - Color: Se asignará dinámicamente por código usando InventoryUtils.GetRarityColor
  - Puede ser una línea horizontal simple

- **Title (Image)**:
  - Component: Image  
  - Source Image: Variable según rareza (será asignado por código)
  - Representa el marco/borde del título

- **Miniature (Image)**:
  - Component: Image
  - Source Image: Se asignará dinámicamente según el ítem
  - Size: 64x64 píxeles recomendado

### Content_Panel
- **Description (TMP_Text)**:
  - Component: TextMeshPro - Text (UI)
  - Text: "Descripción del ítem"
  - Word Wrapping: Enabled
  - Font Size: 12-14

- **Armor (TMP_Text)**:
  - Component: TextMeshPro - Text (UI)
  - Text: "Armadura: Ligera/Media/Pesada"
  - Font Size: 11-12
  - Solo visible para ítems de armadura

- **Category (TMP_Text)**:
  - Component: TextMeshPro - Text (UI)
  - Text: "Tipo: Arma/Casco/etc."
  - Font Size: 11-12

- **Durability (TMP_Text)**:
  - Component: TextMeshPro - Text (UI)
  - Text: "Durabilidad: 100/100"
  - Font Size: 11-12

### Stats_Panel
- **Stats Container**:
  - Component: VerticalLayoutGroup
  - Child Controls Size: Width=True, Height=True
  - Child Force Expand: Width=True, Height=False
  - Spacing: 2-5 píxeles
  - Este contenedor será poblado dinámicamente por el código

### Interaction_Panel
- **Action (TMP_Text)**:
  - Component: TextMeshPro - Text (UI)
  - Text: "Clic derecho: Equipar | Arrastrar: Mover"
  - Font Size: 10-11
  - Text Align: Center

## Prefab para Stats Entry

Crear también un prefab separado para las entradas de estadísticas:

```
StatEntry (Prefab)
├── Stat Entry (GameObject con RectTransform)
│   ├── Stat Name (TMP_Text Component)
│   └── Stat Value (TMP_Text Component)
└── TooltipStatEntry (Script Component)
```

### StatEntry Configuration
- **Layout**: Horizontal Layout Group recomendado
- **Stat Name**: Texto alineado a la izquierda
- **Stat Value**: Texto alineado a la derecha
- **Size**: Height=20-25 píxeles

## Scripts Assignments

### En InventoryTooltipController:
Asignar en el inspector todas las referencias:
- `tooltipPanel`: El GameObject "Tooltip Panel"
- `tooltipCanvas`: El componente Canvas
- `titlePanel`: El GameObject "Title_Panel"
- `backgroundImage`: La Image "Background" 
- `dividerImage`: La Image "Divider"
- `titleImage`: La Image "Title"
- `miniatureImage`: La Image "Miniature"
- `contentPanel`: El GameObject "Content_Panel"
- `descriptionText`: El TMP_Text "Description"
- `armorText`: El TMP_Text "Armor"
- `categoryText`: El TMP_Text "Category"
- `durabilityText`: El TMP_Text "Durability"
- `statsPanel`: El GameObject "Stats_Panel"
- `statsContainer`: El Transform del "Stats Container"
- `statEntryPrefab`: El prefab de StatEntry creado anteriormente
- `interactionPanel`: El GameObject "Interaction_Panel"
- `actionText`: El TMP_Text "Action"

### Sprites de Rareza:
En el inspector, asignar sprites para cada rareza:
- `commonBackgroundSprite`
- `uncommonBackgroundSprite`
- `rareBackgroundSprite`
- `epicBackgroundSprite`  
- `legendaryBackgroundSprite`

Y lo mismo para los title sprites.

## Manager Setup

1. Crear un GameObject vacío llamado "TooltipManager"
2. Agregar el componente `InventoryTooltipManager`
3. El manager se conectará automáticamente al tooltip controller

## Notas importantes:
- El tooltip debe empezar desactivado (tooltipPanel.SetActive(false))
- El Canvas debe tener un Sort Order alto para aparecer sobre otros UI
- Las imágenes de rareza deben estar configuradas como UI Sprites
- El sistema soporta seguimiento del mouse automáticamente
- El delay de aparición es configurable (por defecto 0.5 segundos)

## Testing:
Una vez configurado todo, usar los métodos de debugging en InventoryTooltipManager:
- Context Menu "Test Show Tooltip"
- Context Menu "Test Hide Tooltip"
- Context Menu "Refresh Connections"
