# Crear_NPC_Interactivo

# 🤖 Cómo Crear un NPC Interactivo desde Cero

> Guía paso a paso para implementar NPCs con diálogo interactivo, efectos y sistema de interacción contextual.
> 

---

## 🧩 1. Crear el NPCDialogData

Ubicación: `Assets/ScriptableObjects/Dialogue/`

### 1.1 Campos requeridos:

- `🧠 Name`: Nombre identificador del NPC.
- `🖼️ Image`: Imagen que se muestra en la UI del diálogo.
- `💬 Text`: Texto inicial del menú principal del diálogo.
- `📜 Options (DialogueOptions)`: Lista de opciones disponibles en el diálogo.

---

### 1.2 Campos de cada `DialogueOption`:

- `💬 text`: Texto mostrado al jugador.
- `🎯 type`: Tipo de opción (`OpenMenu`, `CloseMenu`, `ExecuteEffects`).
- `📂 nextMenuId`: Solo para `OpenMenu`; define el menú que se abre.
- `🧪 dialogueEffectsId`: Lista de IDs de efectos que se aplican al elegir esta opción.
- `⚙️ effectParameters` (opcional):
    - `📝 stringParameter`
    - `🔢 intParameter`
    - `📏 floatParameter`
    - `✅ boolParameter`
- `🔐 requireEffectsCanExecute`: Define si se requiere validación previa con `CanExecute`.

---

### 1.3 Crear los `DialogueEffects` (ScriptableObjects)

Ubicación: `Assets/ScriptableObjects/Dialogue/`

📌 Clic derecho en la carpeta deseada → `Create → Dialogue → Effects → [Tipo de efecto]`

Tipos disponibles (MVP):

| Tipo | Descripción |
| --- | --- |
| 🎁 `AddItemDialogEffect` | Agrega un ítem al jugador |
| ⭐ `GiveExperienceDialogEffect` | Otorga experiencia |
| 🪖 `UnlockSquadDialogEffect` | Desbloquea una escuadra |

---

### 🧬 Campos del `AddItemDialogEffect`:

- `🆔 id`: Identificador único del efecto (para usarlo en `dialogueOptions`).
- `📝 displayName`: Nombre mostrado en editor.
- `🧾 description`: Descripción del efecto.
- `🖼️ effectIcon`: Ícono visual del efecto.
- `🎯 itemId`: ID del ítem que se entrega.
- `🔢 quantity`: Cantidad del ítem.
- `💬 customMessage`: Mensaje personalizado que se muestra al ejecutar el efecto.

---

### 1.4 Registrar los efectos creados

Ubicación: `Assets/Resources/DialogueEffectsDatabase`

🔧 Asegúrate de agregar todos los `DialogueEffects` aquí para que puedan ser referenciados desde `NPCDialogData`.

---

## 🧱 2. Crear el Prefab del NPC

Ubicación: `Assets/Prefabs/NPC/`

### 2.1 Estructura básica del Prefab:

- `🧍 NPCRoot (GameObject)`
    - 🎯 `Trigger_Area (Empty GameObject)`
        - `📦 Collider (Sphere o Box, isTrigger = true)`
        - `📜 NPCTriggerZone (Script)` → Configurar `BuildingId`
        - `🔗 NPCDialogReference (Script)` → Asignar el `NPCDialogData` creado
    - 🧾 `Canvas` -> Npc Billboard component
        - `📋 Interaction_Panel`
            - `🪟 Background`
            - `💬 Display_Text` (texto mostrado al entrar en zona de interacción)
    - (Opcional) `⭐ NPC_Icon` → Ícono flotante representativo

---

## ✅ Ejemplo de Jerarquía en Unity:

```
NPC_Merchant (Prefab)
├── Trigger_Area
│   ├── SphereCollider (isTrigger: ✅)
│   ├── NPCTriggerZone (BuildingId: Smith)
│   └── NPCDialogReference (DialogData: "MerchantDialog_001")
├── Canvas
│   └── Interaction_Panel
│       ├── Background
│       └── Display_Text ("Presiona F para hablar")
└── NPC_Icon (opcional)
```

---

## 🗂️ Notas adicionales

- Asegúrate de usar rutas y nombres exactos como los descritos.
- El sistema espera los `DialogueEffects` disponibles en el `DialogueEffectsDatabase`.
- La UI de interacción contextual aparece automáticamente al entrar en el `Trigger_Area`.

---

> 🧠 Usa este archivo como plantilla para añadir más NPCs con efectos personalizados. A medida que se implementen más DialogueEffects, actualiza esta guía con sus campos respectivos.
>