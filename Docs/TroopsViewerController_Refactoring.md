# Refactorización TroopsViewerController

## Resumen de Cambios

Se ha separado la funcionalidad del `TroopsViewerController` original en dos componentes:

1. **TroopsViewerController** - Widget reutilizable de paginación
2. **FooterController** - Controlador específico para battle preparation

## TroopsViewerController (Widget Reutilizable)

### Responsabilidades
- Paginación genérica de listas
- Navegación con chevrons (izquierda/derecha)  
- Manejo de contenedores y placeholders
- Instanciación de items (delegada al controlador superior)

### Componentes UI
```csharp
[Header("Navigation")]
[SerializeField] private Button rightChevron;
[SerializeField] private Button leftChevron;

[Header("Containers")]
[SerializeField] private GameObject itemContainerPlaceholder;
[SerializeField] private Transform itemContainer;
[SerializeField] private GameObject itemPrefab;

[Header("Configuration")]
[SerializeField] private int itemsPerPage = 5;
```

### API Pública
```csharp
// Configuración
void SetItems(List<string> itemIds)
void SetItemsPerPage(int itemsPerPageValue)
void RefreshCurrentPage()

// Información
int GetCurrentPageIndex()
int GetTotalPages()

// Comunicación con items
void TriggerItemClick(string itemId)
```

### Eventos
```csharp
// Se dispara cuando se hace click en un item
System.Action<string> OnItemClicked;

// Se dispara cuando el widget necesita crear un item
// Parámetros: itemId, container, prefab
// Retorna: GameObject creado
System.Func<string, Transform, GameObject, GameObject> OnItemsRequested;
```

## FooterController (Controlador Específico)

### Responsabilidades
- Lógica de negocio específica de battle preparation
- Gestión de loadouts y datos de héroe
- Creación específica de SquadOptionUI
- Integración con servicios (SquadDataService, PlayerSessionService, etc.)
- Manejo de UI específica (botones de loadout, display de leadership)

### Componentes UI
```csharp
[Header("Widget")]
[SerializeField] private TroopsViewerController troopsViewerWidget;

[Header("Actions")]
[SerializeField] private Button showLoadoutsButton;
[SerializeField] private Button saveLoadoutButton;

[Header("Display")]
[SerializeField] private TextMeshProUGUI totalLeadershipText;
```

### Datos Específicos del Dominio
```csharp
private List<string> _selectedSquadIds;
private HeroData _currentHero;
private LoadoutSaveData _activeLoadout;
private int _totalLeadershipCost = 0;
```

## Comunicación Entre Componentes

### Flujo de Inicialización
1. `FooterController.Start()` configura el widget:
   ```csharp
   troopsViewerWidget.OnItemClicked += OnSquadOptionClicked;
   troopsViewerWidget.OnItemsRequested += OnItemsRequested;
   troopsViewerWidget.SetItemsPerPage(5);
   ```

2. FooterController carga datos y actualiza widget:
   ```csharp
   _selectedSquadIds = loadout.squadIDs;
   troopsViewerWidget.SetItems(_selectedSquadIds);
   ```

### Flujo de Creación de Items
1. Widget calcula items para página actual
2. Widget llama `OnItemsRequested(squadId, container, prefab)`
3. FooterController crea SquadOptionUI específico con lógica de dominio
4. FooterController retorna GameObject creado
5. Widget lo agrega a su lista de items actuales

### Flujo de Interacción
1. Usuario hace click en SquadOptionUI
2. SquadOptionUI llama `troopsViewerWidget.TriggerItemClick(squadId)`
3. Widget dispara evento `OnItemClicked(squadId)`
4. FooterController recibe evento y ejecuta lógica específica
5. FooterController actualiza datos y refresca widget

## Ventajas de la Nueva Estructura

### Reutilización
- TroopsViewerController puede usarse para cualquier lista paginada
- Fácil crear nuevos controladores para otros contextos

### Mantenibilidad
- Separación clara entre UI genérica y lógica de negocio
- Widget independiente y testeable

### Configurabilidad
- `itemsPerPage` configurable desde Inspector
- Fácil ajustar comportamiento sin cambiar código

### Escalabilidad
- Agregar nuevas funcionalidades al widget no afecta controladores específicos
- Nuevos controladores pueden usar el mismo widget base

## Migración desde Código Existente

Si tienes código que usa el TroopsViewerController original:

### Antes:
```csharp
troopsViewer.AddSquadToSelection(squadId);
troopsViewer.OnSelectionChanged += OnTroopsChanged;
```

### Después:
```csharp
footerController.AddSquadToSelection(squadId);
footerController.OnSelectionChanged += OnTroopsChanged;
```

El FooterController mantiene la misma API pública que el TroopsViewerController original para facilitar la migración.
