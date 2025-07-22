# Data Persistence System in Conquest Tactics

## Overview
This project uses a modular data persistence system to save and load player progress, including all heroes, squads, equipment, and more. The system is designed for Unity 2022.3.x and supports Entities 1.3.14.

## Main Components
- **ISaveProvider**: Interface that defines methods for saving and loading `PlayerData`. This allows for different storage strategies (local, cloud, etc.).
- **LocalSaveProvider**: Default implementation of `ISaveProvider` that serializes data to JSON and stores it in `Application.persistentDataPath`.
- **SaveSystem**: Static class that manages saving. Uses an `ISaveProvider` (default: `LocalSaveProvider`). You can switch providers with `SetProvider()`.
- **LoadSystem**: Static class that manages loading. Uses an `ISaveProvider` (default: `LocalSaveProvider`). You can switch providers with `SetProvider()`.
- **PlayerData**: Root class for all persistent player information. Contains a list of `HeroData` and other progression values.
- **HeroData, SquadInstanceData, Equipment, InventoryItem, LoadoutSaveData, etc.**: Serializable classes representing all aspects of player progress.

## How Saving Works
1. Call `SaveSystem.SavePlayer(PlayerData data)` to save the current player state.
2. Before saving, `PlayerData.ClearCachedAttributes()` is called to remove any runtime-only/cached data.
3. The active `ISaveProvider` serializes the data and writes it to disk (or other storage).

## How Loading Works
1. Call `LoadSystem.LoadPlayer()` to load the saved player state.
2. The active `ISaveProvider` deserializes the data from disk (or other storage).
3. After loading, `DataCacheService.RecalculateAttributes(data)` is called to regenerate any runtime/cached data.

## Extensibility
- You can implement new providers (e.g., `CloudSaveProvider`) by inheriting from `ISaveProvider` and plugging them in with `SetProvider()`.
- The system is compatible with all serializable classes used for player progression.

## File Location
- Local saves are stored as `player_save.json` in `Application.persistentDataPath` (e.g., `C:/Users/<User>/AppData/LocalLow/<Company>/<Project>/`).

## Example Usage
```csharp
// Save
SaveSystem.SavePlayer(currentPlayerData);

// Load
PlayerData loaded = LoadSystem.LoadPlayer();
```

## Notes
- Always clear cached attributes before saving and recalculate them after loading.
- The system is ready for future cloud or remote save extensions.
