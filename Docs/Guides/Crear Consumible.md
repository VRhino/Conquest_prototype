# Crear Consumible

# 🧪 Cómo Crear un Consumible desde Cero

> Guía paso a paso para implementar objetos consumibles que otorgan efectos al jugador, como monedas.
> 

---

## 📦 1. Crear el Scriptable Object del Item

Ubicación: `Assets/Resources/items/`

📌 En EL menu superior → `Tools -> Items -> Item Creator Wizard`

NOTA: al usar el itemCreatorWizard se crea una carpeta para dicho item en la ruta antes mencionada con el id elegido por nombre de la carpeta, dejar dentro de dicha carpeta todos assets que use dicho item:
* Scripable Object (itemData)
* png (miniatures, etc...)

## 2. completar el itemDataSO

Ubicación: `Assets/Resources/items/{ID}/{ID}.asset`

### 2.1 Campos del nuevo ítem:

- `🆔 id`: Identificador único del ítem.
- `🏷️ name`: Nombre mostrado en el tooltip del inventario.
- `🖼️ icon`: ícono que se mostrará en la UI del inventario.
- `🧾 description`: Descripción del ítem.
- `🎖️ rarity`: Rareza del ítem (`Common`, `Uncommon`, `Rare`, `Epic`, `Legendary`).
- `🎯 itemType`: Tipo de ítem (`Consumable`).
- `📦 stackable`: Booleano; si se puede apilar en el inventario. 
- `🚫 visualPartId`: (No aplica para consumibles)
- `🚫 statsGenerators`: (No aplica para consumibles)
- `💥 effects`: Lista de `ItemEffects` que se ejecutan al usar el ítem.

---

## 3. Asegurarse que este en ItemSODatabase

Ubicacion: `Assets/Resources/items/ItemSODatabase.asset`

---

## 🧬 4. Crear el efecto del Consumible (ItemEffect)

Ubicación: `Assets/ScriptableObjects/Items/`

📌 Clic derecho en la carpeta → `Create → Item → Effects → Currency`

### 4.1 Tipo disponible: `CurrencyEffect`

> Añade monedas al inventario del jugador.

### 4.2 Campos del `CurrencyEffect`:

- `🆔 id`: Identificador del efecto.
- `📝 displayName`: Nombre visible en el editor de Unity.
- `🧾 description`: Descripción del efecto del consumible.
- `🖼️ effectIcon`: Ícono visual del efecto.
- `💰 currencyType`: Tipo de moneda (`Gold`, `Silver`, `Bronze`).
- `🔢 amount`:
    - `📉 min`: Cantidad mínima a añadir (e.g. `10`).
    - `📈 max`: Cantidad máxima a añadir (e.g. `50`).
- `🛑 hasMaximumLimit`: Booleano para validar si hay un tope.
- `🔝 maximumCurrencyLimit`: Límite superior de moneda permitido (e.g. `99999`).

---

## ✅ Ejemplo de configuración de ítem

```json
{
    "id": "gold_pouch_001",
    "name": "Bolsa de Oro",
    "iconPath": "UI/Icons/gold_pouch",
    "description": "Una pequeña bolsa que contiene monedas de oro.",
    "rarity": "Uncommon",
    "itemType": "Consumable",
    "stackable": true,
    "effects": [
        {
            "id": "gold_effect_001",
            "currencyType": "Gold",
            "amount": {
                "min": 10,
                "max": 50
            },
            "hasMaximumLimit": true,
            "maximumCurrencyLimit": 99999
        }
    ]
}
```

---

## 🗂️ Notas adicionales

- El `CurrencyEffect` es el único disponible por ahora.
- Puedes crear múltiples consumibles con distintos efectos de moneda.
- La lógica de aplicación del efecto se gestiona al consumir el ítem desde el sistema de inventario.

---

> 🧠 Usa esta guía como plantilla para futuros tipos de consumibles y efectos adicionales a medida que se expandan las mecánicas del juego.
>