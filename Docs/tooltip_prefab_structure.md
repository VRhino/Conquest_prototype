tooltip (gameObject)
├── Title_Panel (gameObject)
│   ├── background (img) -> varia por rarity, sprite asignado por código
│   ├── divider (img) -> color dinámico con InventoryUtils.GetRarityColor
│   ├── title (img) -> sprite varia por rarity
│   └── miniature (img) -> miniatura del objeto
├── Content_Panel (gameObject)
│   ├── armor (text) -> solo para armaduras (Torso/Helmet/Gloves/Pants/Boots)
│   ├── category (text) -> categoría del objeto (itemType)
│   ├── durability (text) -> durabilidad del objeto
│   └── Stats_panel (gameObject) -> solo para equipment con stats, stats_item container
│       └── Stats_item (gameObject) -> elemento de la lista de stats
│           ├── statName (text) -> nombre de la estadística
│           └── statValue (text) -> valor de la estadística
└── interaction_panel (gameObject)
    └── action (text) -> acciones disponibles (Equipar, Usar, etc.)


Reward_Dialog
├── TitlePanel
│   └──  titulo (text)
├── Text_Panel 
│   └──  text (text)
├── Reward_Section -> item container / horizontal layout
│   └──  Inventory_Item_cell (puede ser 1 o N y se agregand dinamicamente) -> prefab existente
└── Button_section
    └── Button (button)

UIStore (IFullscreenPanel)
├── Title(text)
├── Goods (container) -> item container /vertical layout
|   └── Store_Item (prefab) -> puede ser 1 o N se agrega dinamicamente
|       ├── Item_Sell (prefab) (BaseItemCellController)
|       ├── Product_Name (text)
|       ├── Currency_Icon (img)
|       ├── Cost (text)
|       └── Button -> accion (comprar)
└── Buttons
    └── Exit_Button 