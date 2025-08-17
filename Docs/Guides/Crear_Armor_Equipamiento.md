# Crear_Armor_Equipamiento

# 🛡️ Cómo Crear un Equipment (Armor) desde Cero

> Guía paso a paso para crear y registrar armaduras visuales y funcionales dentro del sistema de ítems y avatar del juego.
> 

---

## 🧱 1. Crear los Prefabs Visuales de la Armadura

Ubicación: `Assets/Prefabs/Equipment/`

- Crea todos los prefabs que componen la armadura (torso, hombreras, coderas, botas, etc.).

---

## 🧩 2. Añadir el Prefab al Héroe

- Inserta el prefab visual en el `GameObject` del héroe:

```
Modular_Character
├── [Bone] → Añadir aquí el prefab correspondiente
│   ├── Ejemplo: Chr_LegLeft_Male_03
│   └── Ejemplo: Chr_LegLeft_Female_03
```

> Un torso de nivel alto puede incluir múltiples piezas visuales.
> 

el Prefab del heroe es ModularHeroVisual (Assets/Prefabs/esqueletos)

---

## 🧾 3. Registrar el Visual Part ID

Ubicación: `Assets/Resources/Data/Avatar/AvatarPartsDatabase`

### 3.1 Crear entradas `VisualPartEntry` para cada pieza visual

- Campos requeridos:

| Campo | Descripción |  |
| --- | --- | --- |
| `🆔 id` | Identificador único del visual part | iron_chestplate_male_01 |
| `🦴 boneTargetMale` | Hueso destino en el esqueleto masculino (ej. `Male_12_Leg_Left`) | Male_03_Torso |
| `🦴 boneTargetFemale` | Hueso destino en el esqueleto femenino (ej. `Female_11_Leg_Left`) | Female_03_Torso |
| `🧩 prefabPathMale` | Nombre del GameObject añadido al prefab del héroe | Chr_Torso_Male_04 |
| `🧩 prefabPathFemale` | Nombre del GameObject añadido al prefab del héroe | Chr_Torso_Female_04 |

✅ Repite este paso por cada parte visual de la armadura.

---

## 📦 4. Crear el ítem en la ItemDatabase

Ubicación: `Assets/Scripts/Resources/Data/Items/ItemDatabase`

### 4.1 Campos del nuevo ítem (tipo Armor)

- `🆔 id`: Identificador único del ítem.
- `🏷️ name`: Nombre visible en el tooltip.
- `🖼️ iconPath`: Ruta al ícono.
- `🧾 description`: Descripción del ítem.
- `🎖️ rarity`: Rareza (`Common`, `Uncommon`, `Rare`, `Epic`, `Legendary`).
- `🛡️ itemType`: Debe ser `Armor`
- `itemCategory`: Categoria (itemCategory Enum)
- `📦 stackable`: `false` para equipos.
- `🧩 visualPartId`: ID del VisualPart creado en el paso 3.
- `📈 statsGenerators`: Lista de generadores de stats defensivos.

---

## 🧪 5. Crear StatsGenerators

Ubicación: `Assets/ScriptableObjects/Items/`

📌 Clic derecho → `Create → Item → StatsGenerators → [tipo de stat]`

### 5.1 Campos del `StatsGenerator`

- `🆔 id`: Identificador del stat generator.
- `📝 displayName`: Nombre mostrado en el editor.
- `🧾 description`: Descripción del stat.
- `📊 stat`: Stat a modificar (`piercing`, `slashing`, `blunt defense`, etc.).
- `📉 min`: Valor mínimo.
- `📈 max`: Valor máximo.

---

## 🗂️ Ejemplo de Configuración

```json
{
    "id": "iron_chestplate_001",
    "name": "Peto de Hierro",
    "iconPath": "UI/Icons/chest_iron",
    "description": "Protección básica para los combates cuerpo a cuerpo.",
    "rarity": "Uncommon",
    "itemType": "Torso",
    "stackable": false,
    "visualPartId": "iron_chestplate_male_01",
    "statsGenerators": [
        {
            "id": "CommonArmorStats",
            "stat": "slashingDefense",
            "min": 10,
            "max": 50,
            "stat": "piercingDefense",
            "min": 10,
            "max": 50,
            "stat": "bluntDefense",
            "min": 10,
            "max": 50,
            "stat": "vitality",
            "min": 10,
            "max": 50,
            "stat": "armor",
            "min": 10,
            "max": 50
        }
    ]
}
```

---

> 🧠 Usa esta guía como base para crear conjuntos completos de armaduras. Asegúrate de registrar cada pieza correctamente para una visualización coherente en personajes.
>