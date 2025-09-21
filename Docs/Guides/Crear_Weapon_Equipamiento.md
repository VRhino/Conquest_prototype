# Crear_Equipamiento(Arma)

# 🗡️ Cómo Crear un Equipamiento (Weapon) desde Cero

> Guía completa para implementar armas visuales y funcionales dentro del sistema de ítems y personajes.
> 

---

## 🧱 1. Crear Prefabs Visuales del Arma

Ubicación: `Assets/Prefabs/Equipment/`

- Crea todos los `Prefabs` visuales que formarán parte del arma (modelos 3D, efectos visuales, etc).

---

## 🧩 2. Añadir el Prefab al Héroe

- Inserta el prefab visual en el `GameObject` del héroe, en la jerarquía adecuada:

```
ModularHero
├── Root
│   └── Hand_R
│       └── WeaponCarrier → aquí va el arma principal
│   └── Hand_L
│       └── OffWeaponCarrier → aquí va el arma secundaria (si aplica)
```

---

## 🧾 3. Registrar el Visual Part ID

Ubicación: `Assets/Resources/Data/Avatar/AvatarPartsDatabase`

### 3.1 Crear un VisualPartEntry para el arma

- Campos requeridos:

| Campo | Descripción | ejemplo |
| --- | --- | --- |
| `🆔 id` | Identificador único del visual part | steel_sword_visual |
| `🦴 boneTargetMale` | Hueso destino para personaje masculino (ej. `WeaponCarrier`) | WeaponCarrier |
| `🦴 boneTargetFemale` | Hueso destino para personaje femenino | WeaponCarrier |
| `🧩 prefabPathMale` | Nombre del GameObject dentro del héroe (paso 2) | SM_Wep_Sword_Large_01 |
| `🧩 prefabPathFemale` | Igual que `prefabPathMale` si usan el mismo modelo | SM_Wep_Sword_Large_01 |

✅ Este paso asegura que el arma se muestre correctamente en runtime.

---

## 📦 4. Crear el Scriptable Object del Item

Ubicación: `Assets/Resources/items/`

📌 En EL menu superior → `Tools -> Items -> Item Creator Wizard`

NOTA: al usar el itemCreatorWizard se crea una carpeta para dicho item en la ruta antes mencionada con el id elegido por nombre de la carpeta, dejar dentro de dicha carpeta todos assets que use dicho item:
* Scripable Object (itemData)
* png (miniatures, etc...)

---

## 5. completar el itemDataSO

Ubicación: `Assets/Resources/items/weapons/{ID}/{ID}.asset`

### 5.1 Campos del nuevo ítem:

- `🆔 id`: Identificador único del arma.
- `🏷️ name`: Nombre visible en el tooltip del inventario.
- `🖼️ iconPath`: Ruta al ícono del arma.
- `🧾 description`: Descripción breve del arma.
- `🎖️ rarity`: Rareza (`Common`, `Uncommon`, `Rare`, `Epic`, `Legendary`).
- `⚔️ itemType`: `Weapon`.
- `itemCateogry`: Categoria (itemCategory Enum)
- `📦 stackable`: `false` (los ítems de tipo `Equipment` no son apilables).
- `🧩 visualPartId`: ID del VisualPart creado en el paso 3.
- `📈 statsGenerators`: Lista de generadores de estadísticas para este arma.

---

## 6. Asegurarse que este en ItemSODatabase

Ubicacion: `Assets/Resources/items/ItemSODatabase.asset`

---

## 🧪 7. Crear StatsGenerators

Ubicación: `Assets/ScriptableObjects/Items/`

📌 Clic derecho en la carpeta → `Create → Item → StatsGenerators → [Tipo de stat]`

### 7.1 Campos del `StatsGenerator`

- `🆔 id`: Identificador único.
- `📝 displayName`: Nombre mostrado en el editor.
- `🧾 description`: Descripción del efecto.
- `📊 stat`: Stat que modifica (`piercing`, `slashing`, `blunt damage`, etc.).
- `📉 min`: Valor mínimo del stat.
- `📈 max`: Valor máximo del stat.

✅ Estos valores permiten la aleatorización de stats al generar el ítem.

---

## 🗂️ Ejemplo de Configuración

```json
{
    "id": "steel_sword_001",
    "name": "Espada de Acero",
    "iconPath": "UI/Icons/sword_steel",
    "description": "Una espada fiable y bien equilibrada.",
    "rarity": "Rare",
    "itemType": "Weapon",
    "stackable": false,
    "visualPartId": "steel_sword_visual",
    "statsGenerators": [
        {
            "id": "piercing_10_50",
            "stat": "piercingDamage",
            "min": 10,
            "max": 50
        }
    ]
}
```

---

> 🧠 Usa esta guía como plantilla base para cualquier arma del juego. Puedes duplicarla y modificar valores para crear variantes rápidamente.
