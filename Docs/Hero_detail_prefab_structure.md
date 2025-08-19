
Hero_Detail_UI
├── RawImage (Raw Image) -> preview texture
├── Left_Panel (gameObject)
|   ├── HeroStatusPanel (gameObject) 
|   │   ├── name (text) -> 
|   │   ├── exp 
│   │   ├── Bar (Prefab) -> img
|   │   │   │   └── border (Prefab) -> img
|   │   │   │       └── Foreground (Prefab) -> img with filling para mostrar que tan avanzdo la barra
|   │   │   ├── actualLevel (text)
|   │   │   └── nextLevel (text) -> aqui seria (exp/TotalExpToNextLevel)
|   │   └── level (text) -> sprite varia por rarity
|   ├── Basic_Stats_Panel (gameObject)
|   │   ├── Leadership (gameObject) 
|   │   │   ├── Leadership_icon (image) -> sprite por editor
|   │   │   ├── Leadership_text (text) -> texto por editor
|   │   │   └── Leadership_value (gameObject) -> 
|   │   │       ├── more (button) -> escondido a menos que tenga puntos de nivel disponibles para añadir
|   │   │       ├── minus (button) -> escondido a menos que haya asignado u punto y lo pueda quitar
|   │   │       └── Leadership_value (text) -> valor real, cambia a verde cuando se le agrega un 1 punto(100+1)
|   │   ├── Strength (gameObject) 
|   │   │   ├── Strength_icon (image) -> sprite por editor
|   │   │   ├── Strength_text (text) -> texto por editor
|   │   │   └── Strength_value (gameObject) -> 
|   │   │       ├── more (button) -> escondido a menos que tenga puntos de nivel disponibles para añadir
|   │   │       ├── minus (button) -> escondido a menos que haya asignado u punto y lo pueda quitar
|   │   │       └── Strength_value (text) -> valor real, cambia a verde cuando se le agrega un 1 punto(100+1)
|   │   ├── Agility (gameObject) 
|   │   │   ├── Agility_icon (image) -> sprite por editor
|   │   │   ├── Agility_text (text) -> texto por editor
|   │   │   └── Agility_value (gameObject) -> 
|   │   │       ├── more (button) -> escondido a menos que tenga puntos de nivel disponibles para añadir
|   │   │       ├── minus (button) -> escondido a menos que haya asignado u punto y lo pueda quitar
|   │   │       └── Agility_value (text) -> valor real, cambia a verde cuando se le agrega un 1 punto(100+1)
|   │   ├── Armor (gameObject) 
|   │   │   ├── Armor_icon (image) -> sprite por editor
|   │   │   ├── Armor_text (text) -> texto por editor
|   │   │   └── Armor_value (gameObject) -> 
|   │   │       ├── more (button) -> escondido a menos que tenga puntos de nivel disponibles para añadir
|   │   │       ├── minus (button) -> escondido a menos que haya asignado u punto y lo pueda quitar
|   │   │       └── Armor_value (text) -> valor real, cambia a verde cuando se le agrega un 1 punto(100+1)
|   │   └── Toughness (gameObject) 
|   │       ├── Toughness_icon (image) -> sprite por editor
|   │       ├── Toughness_text (text) -> texto por editor
|   │       └── Toughness_value (gameObject) -> 
|   │           ├── more (button) -> escondido a menos que tenga puntos de nivel disponibles para añadir
|   │           ├── minus (button) -> escondido a menos que haya asignado u punto y lo pueda quitar
|   │           └── Toughness_value (text) -> valor real, cambia a verde cuando se le agrega un 1 punto(100+1)
|   └── Buttons_Panel (gameObject)
|       ├── More_details_button
|       └── Reset_button
├── Rigth_Panel (gameObject)
|   ├── Equipment_parts (gameObject) -> heroData.equipment
|   │   ├──  Helmet (Prefab) -> recibe itemData del slot que le toca
|   │   ├──  Torso (Prefab) -> recibe itemData del slot que le toca
|   │   ├──  Gloves (Prefab) -> recibe itemData del slot que le toca
|   │   ├──  Pants (Prefab) -> recibe itemData del slot que le toca
|   │   ├──  Boots (Prefab) -> recibe itemData del slot que le toca
|   │   └──  Weapon (Prefab) -> recibe itemData del slot que le toca
|   └── Repair_Panel (gameObject)
|       ├── repair (button)
|       └── repair_all (button)
└── Attributes_detail_panel (gameObject) -> nota 1 y nota 2
    ├── health (text)
    ├── stamina(text)
    ├── Piercing Penetration (text)
    ├── Slashing Penetration (text)
    ├── Blunt Penetration (text)
    ├── Piercing Damage (text)
    ├── Slashing Damage (text)
    ├── Blunt Damage (text)
    ├── Piercing Defense (text)
    ├── Slashing Defense (text)
    ├── Blunt Defense (text)
    ├── Block (text)
    └── Block Regen (text)


Nota:
1) oculto hasta que se cumpla 1 de dos 2 condiciones
- que se presione el boton More_details_button, que es un toggle
- que añada un punto en alguno de los Basic_Stats_Panel presionando un more (button) para refeljar en cuanto se agrega
2) todos los valores de los hijos vienen de la cache de stats del heroe