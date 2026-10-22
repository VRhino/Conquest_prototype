# Crear_Equipamiento(Arma)

> Revisada contra el código el 2026-10-22 (menús `CreateAssetMenu`, `ItemCreatorWizard`, `ItemDataSO`, `CurrencyEffect`). La carpeta `Assets/Prefabs/Equipment/` que cita no existe hoy en el repositorio: usa la que corresponda a tus prefabs.

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

Ubicación: `Assets/Resources/Data/Avatar/AvatarPartDatabase.asset` (clase `AvatarPartDatabase`)

### 3.1 Crear una `AvatarPartDefinition` (con un `VisualAttachment`) para el arma

- En el código no existe `VisualPartEntry`: la lista es `AvatarPartDefinition` (`id`, `displayName`, `slot` de `AvatarSlot`, `attachments`) en la lista del slot de `AvatarPartDatabase`, y cada adjunto es un `VisualAttachment`. Los campos de la tabla son los del `VisualAttachment` (el identificador se llama `visualID`); `id`/`slot` van en la definición. No verifiqué qué id enlaza con `ItemDataSO.visualPartId`; copia el patrón de un ítem existente (p. ej. `Assets/Resources/Items/armors/BootHADef/`).
- Campos:

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

Ubicación: `Assets/Resources/Items/`

📌 En EL menu superior → `Tools -> Items -> Item Creator Wizard`

NOTA: al usar el itemCreatorWizard se crea una carpeta para dicho item en la ruta antes mencionada con el id elegido por nombre de la carpeta, dejar dentro de dicha carpeta todos assets que use dicho item:
* Scripable Object (itemData)
* png (miniatures, etc...)

---

## 5. completar el itemDataSO

Ubicación: `Assets/Resources/Items/weapons/{ID}/{ID}.asset`

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

Ubicacion: `Assets/Resources/Items/ItemSODatabase.asset`

---

## 🧪 7. Crear StatsGenerators

Ubicación: `Assets/ScriptableObjects/Items/`

📌 Clic derecho en la carpeta → `Create → Items → Stat Generators → [Tipo de stat]`

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
