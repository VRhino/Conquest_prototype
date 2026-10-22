> Revisada contra el código el 2026-10-22: nombres de clases y campos de `Assets/Scripts/Dialogue/` y `Assets/Scripts/Triggers/NpcTriggerZone.cs`. La tecla de interacción actual es G (ver `Docs/Mechanics/SkillsAndPerks.md`).

# Crear_NPC_Interactivo

# 🤖 Cómo Crear un NPC Interactivo desde Cero

> Guía paso a paso para implementar NPCs con diálogo interactivo, efectos y sistema de interacción contextual.
> 

---

## 🧩 1. Crear el NpcDialogueData

Ubicación: `Assets/ScriptableObjects/Dialogue/`

### 1.1 Campos requeridos:

- `🧠 npcName`: Nombre identificador del NPC.
- `🖼️ npcImage`: Imagen que se muestra en la UI del diálogo.
- `💬 dialogueText`: Texto inicial del menú principal del diálogo.
- `📜 options` (lista de `DialogueOption`): Lista de opciones disponibles en el diálogo.

---

### 1.2 Campos de cada `DialogueOption`:

- `💬 optionText`: Texto mostrado al jugador.
- `🎯 optionType` (`DialogueOptionType`): Tipo de opción (`OpenMenu`, `CloseMenu`, `ExecuteEffects`).
- `📂 nextMenuId`: Solo para `OpenMenu`; define el menú que se abre.
- `🧪 dialogueEffectIds`: Lista de IDs de efectos que se aplican al elegir esta opción.
- `⚙️ effectParameters` (opcional):
    - `📝 stringParameter`
    - `🔢 intParameter`
    - `📏 floatParameter`
    - `✅ boolParameter`
- `🔐 requireEffectsCanExecute`: Define si se requiere validación previa con `CanExecute`.

---

### 1.3 Crear los `DialogueEffects` (ScriptableObjects)

Ubicación: `Assets/ScriptableObjects/Dialogue/`

📌 Clic derecho en la carpeta deseada → `Create → NPC → Dialogue → Effects → [Tipo de efecto]` (el asset de datos del NPC: `Create → NPC → NPC Dialogue Data`)

Tipos disponibles (MVP):

| Tipo | Descripción |
| --- | --- |
| 🎁 `AddItemDialogueEffect` | Agrega un ítem al jugador |
| ⭐ `GiveExperienceDialogueEffect` | Otorga experiencia |
| 🪖 `UnlockSquadDialogueEffect` | Desbloquea una escuadra |
| 🏪 `OpenStoreDialogueEffect` | Abre la tienda del NPC |
| 🖥️ `OpenFullScreenMenuDialogueEffect` | Abre un menú a pantalla completa |

---

### 🧬 Campos del `AddItemDialogueEffect`:

- `🆔 effectId`: Identificador único del efecto (para usarlo en `dialogueOptions`).
- `📝 displayName`: Nombre mostrado en editor.
- `🧾 description`: Descripción del efecto.
- `🖼️ effectIcon`: Ícono visual del efecto.
- `🎯 itemId`: ID del ítem que se entrega.
- `🔢 quantity`: Cantidad del ítem.
- `💬 customMessage`: Mensaje personalizado que se muestra al ejecutar el efecto.
- `🔔 showNotification`: Si muestra notificación al entregar el ítem.

---

### 1.4 Registrar los efectos creados

Ubicación: `Assets/Resources/DialogueEffectDatabase.asset`

🔧 Asegúrate de agregar todos los `DialogueEffects` aquí para que puedan ser referenciados desde `NpcDialogueData`.

---

## 🧱 2. Crear el Prefab del NPC

Ubicación: `Assets/Prefabs/NPC/`

### 2.1 Estructura básica del Prefab:

- `🧍 NPCRoot (GameObject)`
    - 🎯 `Trigger_Area (Empty GameObject)`
        - `📦 Collider (Sphere o Box, isTrigger = true)`
        - `📜 NpcTriggerZone (Script)` → Configurar `buildingID`
        - `🔗 NpcDialogueReference (Script)` → Asignar el `NpcDialogueData` creado
    - 🧾 `Canvas` -> `NpcPromptBillboard` (script)
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
│   ├── NpcTriggerZone (buildingID: Smith)
│   └── NpcDialogueReference (DialogData: "MerchantDialog_001")
├── Canvas
│   └── Interaction_Panel
│       ├── Background
│       └── Display_Text ("Presiona G para hablar")
└── NPC_Icon (opcional)
```

---

## 🗂️ Notas adicionales

- Asegúrate de usar rutas y nombres exactos como los descritos.
- El sistema espera los `DialogueEffects` disponibles en el `DialogueEffectDatabase`.
- La UI de interacción contextual aparece automáticamente al entrar en el `Trigger_Area`.

---

> 🧠 Usa este archivo como plantilla para añadir más NPCs con efectos personalizados. A medida que se implementen más DialogueEffects, actualiza esta guía con sus campos respectivos.
>