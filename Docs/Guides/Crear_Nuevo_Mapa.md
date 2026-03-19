# Guía: Cómo Crear un Nuevo Mapa de Batalla

Esta guía describe el proceso completo para agregar un nuevo mapa funcional al juego(solo la parte de los recursos, no la parte de la escena), incluyendo el ScriptableObject de datos, el prefab del minimapa y su registro en la base de datos.

Usa `DefaultMap` como referencia en todos los pasos que lo requieran.

---

## Resumen del Flujo

```
MapDataSO (datos del mapa)
    ↓
MapDatabase (registro global)
    ↓
Minimap Prefab (visual + puntos interactivos)
    ↓
MapDataSO._preparationMap → referencia al prefab
```

---

## 1. Crear el ScriptableObject `MapDataSO`

- Haz clic derecho en `Assets/Resources/Maps/`
- Selecciona **Create > Maps > Map Data**
- Nombra el asset: `{NombreMapa}.asset` (ej. `ForestMap.asset`)
- Configura los campos:

| Campo | Descripción | Ejemplo |
|-------|-------------|---------|
| `Map Id` | ID único del mapa (se llena automático desde el nombre del asset) | `"ForestMap"` |
| `Map Name` | Nombre visible en la UI | `"Bosque de Thornwood"` |
| `Battle Duration Seconds` | Duración base en segundos (mínimo 60) | `1800` |
| `Supply Point Ids` | IDs de los supply points del mapa | `"SP1"`, `"SP2"` |
| `Capture Point Ids` | IDs de los capture points del mapa | `"CP1"`, `"CP2"`, `"CP3"` |
| `Attacker Spawn Point Ids` | IDs de spawns de atacantes | `"1"`, `"2"`, `"3"` |
| `Defender Spawn Point Ids` | IDs de spawns de defensores | `"4"`, `"5"`, `"6"` |
| `Preparation Map` | Referencia al prefab del minimapa (se asigna en el paso 4) | — |

> Los IDs deben coincidir exactamente con los configurados en los componentes del prefab del minimapa (ver paso 3).

---

## 2. Registrar el Mapa en `MapDatabase`

- Abre el asset: `Assets/Resources/Maps/MapDatabase.asset`
- Agrega el nuevo `MapDataSO` a la lista `Maps`
- Guarda el asset

---

## 3. Crear el Prefab del Minimapa

Cada mapa tiene **su propio prefab de minimapa**. Sigue los pasos a continuación.

### 3.1 Duplicar el prefab base

- Localiza el prefab de referencia: `Assets/Resources/Maps/DefaultMap/DefaultMapMinimap.prefab`
- Duplícalo y renómbralo: `{NombreMapa}.prefab`
- Guárdalo en: `Assets/Resources/Maps/{NombreMapa}/{NombreMapa}.prefab`

### 3.2 Crear el sprite del minimapa

- Vuela la cámara del editor en **vista aérea (bird's eye)** sobre la escena del mapa
- Captura una imagen del terreno desde arriba con proporciones cuadradas o proporcionales a la UI
- Importa la imagen en: `Assets/UI/Maps/{NombreMapa}_minimap.png`
  - Import Settings: **Texture Type = Sprite (2D and UI)**
- En el prefab del minimapa, asigna el nuevo sprite al componente `Image` del GameObject raíz del mapa

### 3.3 Posicionar los Capture Points

- En el prefab, expande el grupo `CapturePoints`
- Para cada capture point del mapa:
  - Ajusta la posición (`RectTransform`) para que coincida con su ubicación en el mapa real
  - Configura el componente `CapturePointIconControllerUI`:
    - `Point Id`: debe coincidir con el ID en `MapDataSO._capturePointIds` (ej. `"CP1"`)
- Usa el `DefaultMap.prefab` como referencia de escala y posicionamiento

### 3.4 Posicionar los Spawn Points

- En el prefab, expande los grupos `AttackerSpawnPoints` y `DefenderSpawnPoints`
- Para cada spawn point:
  - Ajusta la posición (`RectTransform`) según la ubicación en el mapa real
  - Configura el componente `SpawnPointControllerUI`:
    - `Spawn Point Id`: debe coincidir con el ID en `MapDataSO` (atacantes: `"1"`–`"3"`, defensores: `"4"`–`"6"`)
    - `Spawn Point Type`: `Attackers` o `Defenders` según corresponda
- Verifica en `DefaultMap.prefab` cómo están distribuidos los spawns para mantener consistencia visual

### 3.5 Posicionar los Supply Points

- En el prefab, expande el grupo `SupplyPoints`
- Para cada supply point:
  - Ajusta la posición (`RectTransform`) según la ubicación en el mapa real
  - Configura el componente `SupplyPointIconControllerUI`:
    - `Point Id`: debe coincidir con el ID en `MapDataSO._supplyPointIds` (ej. `"SP1"`)

### 3.6 Llenar el `PreparationMapControllerUI`

- Selecciona el GameObject raíz del prefab (tiene el componente `PreparationMapControllerUI`)
- En el Inspector, asigna:
  - **Spawn Points**: arrastra todos los `SpawnPointControllerUI` (atacantes y defensores)
  - **Supply Points**: arrastra todos los `SupplyPointIconControllerUI`
  - **Capture Points**: arrastra todos los `CapturePointIconControllerUI`

> Importante: `PreparationMapControllerUI` filtra automáticamente qué spawns mostrar según el `Side` (Attackers/Defenders). Basta con incluir todos en la lista.

---

## 4. Vincular el Prefab al `MapDataSO`

- Abre el asset `{NombreMapa}.asset`
- En el campo `Preparation Map`, arrastra el prefab `{NombreMapa}.prefab` creado en el paso 3

---

## Resumen de Rutas y Nombres

| Asset | Ruta |
|-------|------|
| MapDataSO | `Assets/Resources/Maps/{NombreMapa}/{NombreMapa}.asset` |
| MapDatabase | `Assets/Resources/Maps/MapDatabase.asset` |
| Minimap Prefab | `Assets/Resources/Maps/{NombreMapa}/{NombreMapa}Minimap.prefab` |
| Sprite del minimapa | `Assets/UI/Maps/{NombreMapa}/{NombreMapa}Image.png` |

---

## Checklist Rápido

- [ ] `MapDataSO` creado con IDs de spawn, supply y capture points
- [ ] Mapa registrado en `MapDatabase`
- [ ] Prefab del minimapa duplicado desde `DefaultMap`
- [ ] Sprite bird's eye importado y asignado al prefab
- [ ] Capture points posicionados y sus IDs configurados
- [ ] Spawn points de atacantes posicionados y sus IDs configurados
- [ ] Spawn points de defensores posicionados y sus IDs configurados
- [ ] Supply points posicionados y sus IDs configurados
- [ ] `PreparationMapControllerUI` tiene todas las referencias asignadas
- [ ] `MapDataSO._preparationMap` apunta al prefab del minimapa

---

¡Listo! El nuevo mapa estará disponible en la pantalla de preparación de batalla y sus puntos estratégicos funcionarán correctamente.
