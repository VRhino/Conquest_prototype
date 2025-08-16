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