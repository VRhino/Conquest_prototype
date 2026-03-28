# HeroData God Object — Plan de Refactor (A-01)

> Fecha: 2026-03-28
> Contexto: `CouplingAnalysis.md` sección A-01

---

## Decisión de arquitectura: Opción B+C (menor riesgo)

Mantener `HeroData` como **único DTO de serialización** e introducir **interfaces de lectura y escritura** que la clase implementa. Los consumidores pasan a depender de interfaces enfocadas, no del blob completo.

**Por qué no otras opciones:**
- **No split en múltiples clases serializables**: `JsonUtility` necesita campos públicos en una clase `[Serializable]`. Partir `HeroData` en sub-objetos requeriría migrar el save file y cambiar `PlayerData.heroes` — riesgo alto sin ningún beneficio funcional.
- **No HeroRuntimeProfile inmediato**: es la opción correcta a largo plazo (multi-hero, servidor) pero requiere cambiar la API de `PlayerSessionService` y ~20 consumidores. Se aplaza a Fase 5.

---

## Estado actual relevante

### Campos de HeroData y su naturaleza

| Campo | Tipo | Naturaleza | Mutado por |
|-------|------|-----------|-----------|
| `classId` | `string` | Config inmutable | Solo `HeroDataService.CreateNewHero()` |
| `heroName` | `string` | Config inmutable | Solo creación |
| `gender` | `string` | Config inmutable | Solo creación |
| `avatar` | `AvatarParts` | Config inmutable | Solo creación |
| `level` | `int` | Progresión | (sin mutador encontrado, XP es external) |
| `currentXP` | `int` | Progresión | `GiveExperienceDialogueEffect` |
| `attributePoints` | `int` | Progresión | `HeroDetailStatsPanel` |
| `perkPoints` | `int` | Progresión | Sin mutador encontrado |
| `strength/dexterity/armor/vitality` | `int` | Atributos mutables | `HeroDetailStatsPanel` |
| `unlockedPerks` | `List<int>` | Colección progresión | Sin mutador encontrado |
| `availableSquads` | `List<string>` | Colección progresión | `UnlockSquadDialogueEffect` |
| `loadouts` | `List<LoadoutSaveData>` | Estado gameplay | Loadout UI |
| `squadProgress` | `List<SquadInstanceData>` | Estado gameplay | `BarracksMenuUIController` |
| `inventory` | `List<InventoryItem>` | Inventario | `InventoryStorageService` |
| `equipment` | `Equipment` | Inventario | `EquipmentManagerService` |
| `bronze/silver/gold` | `int` | Economía | `CurrencyService` |

### Mecanismo de persistencia actual
```
Mutación directa de campo en _currentHero (referencia compartida)
  → InventoryEventService.AutoSaveHeroData()
    → SaveSystem.SavePlayer(PlayerSessionService.CurrentPlayer)
      → JsonUtility.ToJson(playerData) → File.WriteAllText
```

No hay paso de copia. `PlayerSessionService.SelectedHero` apunta al mismo objeto que se serializa.

### Dependencia circular entre servicios
```
PlayerSessionService → DataCacheService → EquipmentManagerService
      ↑                                          ↓
      └──────── InventoryEventService ←──────────┘
                       ↓
                  SaveSystem → PlayerData
```

---

## Plan de migración — 5 fases

### Fase 1 — Introducir interfaces de lectura (zero behavior change)

**Riesgo: Mínimo** — solo archivos nuevos y una adición a HeroData. Sin cambios en consumidores todavía.

**Nuevo archivo:** `Assets/Scripts/Data/Persistence/IHeroDataFacets.cs`

```csharp
public interface IHeroIdentity
{
    string ClassId    { get; }
    string HeroName   { get; }
    string Gender     { get; }
    AvatarParts Avatar { get; }
}

public interface IHeroProgression
{
    int Level          { get; }
    int CurrentXP      { get; }
    int AttributePoints { get; }
    int PerkPoints     { get; }
    int Strength       { get; }
    int Dexterity      { get; }
    int Armor          { get; }
    int Vitality       { get; }
    List<int> UnlockedPerks { get; }
}

public interface IHeroEconomy
{
    int Bronze { get; }
    int Silver { get; }
    int Gold   { get; }
}

public interface IHeroSquads
{
    List<string>            AvailableSquads { get; }
    List<LoadoutSaveData>   Loadouts        { get; }
    List<SquadInstanceData> SquadProgress   { get; }
}

public interface IHeroInventory
{
    List<InventoryItem> Inventory  { get; }
    Equipment           Equipment  { get; }
}
```

**Cambio en `Hero.Data.cs`:** Declarar que `HeroData` implementa las interfaces con implementación explícita. Los campos públicos originales no se tocan (JsonUtility los necesita).

```csharp
[Serializable]
public class HeroData : IHeroIdentity, IHeroProgression, IHeroEconomy, IHeroSquads, IHeroInventory
{
    // Campos públicos existentes — sin cambios
    public string classId = string.Empty;
    // ...

    // Implementación explícita (read-only, apunta a los campos)
    string   IHeroIdentity.ClassId  => classId;
    string   IHeroIdentity.HeroName => heroName;
    AvatarParts IHeroIdentity.Avatar => avatar;
    // ... etc para todas las interfaces
}
```

**Archivos tocados:** 2 (`Hero.Data.cs` + archivo nuevo)
**Verificación:** Proyecto compila. Play mode sin cambios. Save/load idéntico.
**Desbloquea:** Migrar consumidores uno a uno sin riesgo.

---

### Fase 2 — Migrar consumidores de solo lectura a interfaces (un archivo por commit)

**Riesgo: Bajo** — cada cambio es independiente y verificable.

**Patrón de migración:**
1. Identificar qué campos lee el archivo
2. Cambiar el tipo del parámetro/campo de `HeroData` a la interfaz más estrecha
3. Reemplazar acceso directo (`hero.heroName`) por propiedad de interfaz (`hero.HeroName`)

**Orden de migración:**

| Orden | Archivo | Interfaz(es) | Campos usados |
|-------|---------|-------------|---------------|
| 1 | `HeroVisualManagement.System.cs` | `IHeroIdentity`, `IHeroInventory` | gender, equipment |
| 2 | `BattleDebugCreator.cs` | `IHeroIdentity`, `IHeroSquads`, `IHeroInventory` | heroName, classId, gender, avatar, equipment, squadProgress |
| 3 | `HeroExperienceCalculator.cs` | `IHeroProgression` | level, currentXP |
| 4 | `HeroLeadershipCalculator.cs` | `IHeroProgression` | strength, dexterity, armor, vitality |
| 5 | `HeroAttributeValidator.cs` | `IHeroProgression` | strength, dexterity, armor, vitality, attributePoints |
| 6 | `DataCacheService.cs` | `IHeroIdentity`, `IHeroProgression` | heroName, classId, strength, dexterity, armor, vitality |
| 7-15 | UI controllers de display | Según campos leídos | Uno a la vez |

**Verificación por cada archivo:** Compilar. Abrir la escena correspondiente en play mode. Confirmar que la UI muestra los datos correctamente.
**Desbloquea:** Consumidores de lectura aislados del shape completo de HeroData.

---

### Fase 3 — Introducir interfaces de escritura para servicios mutadores

**Riesgo: Medio** — los servicios cambian pero el comportamiento es idéntico.

**Nuevo archivo o sección en `IHeroDataFacets.cs`:**

```csharp
public interface IHeroProgressionMutator : IHeroProgression
{
    new int CurrentXP       { get; set; }
    new int AttributePoints { get; set; }
    new int PerkPoints      { get; set; }
    new int Strength        { get; set; }
    new int Dexterity       { get; set; }
    new int Armor           { get; set; }
    new int Vitality        { get; set; }
}

public interface IHeroEconomyMutator : IHeroEconomy
{
    new int Bronze { get; set; }
    new int Silver { get; set; }
    new int Gold   { get; set; }
}

public interface IHeroSquadsMutator : IHeroSquads
{
    // Las listas ya son mutables por referencia — no se necesita setter
}

public interface IHeroInventoryMutator : IHeroInventory
{
    new Equipment Equipment { get; set; }
    // La lista inventory ya es mutable por referencia
}
```

**Migración de servicios mutadores:**

| Servicio | Patrón actual | Patrón nuevo |
|---------|--------------|-------------|
| `CurrencyService` | `_currentHero.bronze -= amount` | `_heroEconomy.Bronze -= amount` donde `_heroEconomy: IHeroEconomyMutator` |
| `EquipmentManagerService` | `_currentHero.equipment.weapon = item` | `_heroInventory.Equipment.weapon = item` donde `_heroInventory: IHeroInventoryMutator` |
| `InventoryStorageService` | `_currentHero.inventory.Add(item)` | `_heroInventory.Inventory.Add(item)` |
| `GiveExperienceDialogueEffect` | `hero.currentXP += amount` | `progression.CurrentXP += amount` donde `progression: IHeroProgressionMutator` |
| `UnlockSquadDialogueEffect` | `hero.availableSquads.Add(id)` | `squads.AvailableSquads.Add(id)` |
| `HeroDetailStatsPanel` | `_heroData.strength = val` | `progression.Strength = val` |
| `BarracksMenuUIController` | `hero.squadProgress.Add(...)` | `squads.SquadProgress.Add(...)` |

**Verificación por cada servicio:** Compile. Ejercitar el flujo específico en play mode (comprar ítem, equipar, gastar atributo) y verificar que el save file se actualiza correctamente.
**Desbloquea:** Enforcement en tiempo de compilación de "quién puede escribir qué". Un UI controller que accidentalmente intente mutar un campo no compilará si solo tiene una interfaz de lectura.

---

### Fase 4 — Romper la dependencia circular de servicios

**Riesgo: Medio** — toca el flujo de guardado.

**Problema:** `InventoryEventService.AutoSaveHeroData()` llama a `SaveSystem.SavePlayer(PlayerSessionService.CurrentPlayer)` directamente, creando el ciclo `PlayerSessionService ↔ InventoryEventService`.

**Solución: Inyectar un callback de guardado.**

`InventoryEventService.cs` — cambiar `Initialize`:
```csharp
private static Action _saveAction;

public static void Initialize(HeroData hero, Action saveAction)
{
    _currentHero = hero;
    _saveAction  = saveAction;
}

public static void AutoSaveHeroData()
{
    _saveAction?.Invoke();  // ya no conoce ni SaveSystem ni PlayerSessionService
}
```

`PlayerSessionService.cs` — al inicializar servicios, pasar el callback:
```csharp
InventoryEventService.Initialize(hero, () => SaveSystem.SavePlayer(CurrentPlayer));
```

**Archivos tocados:** 2-3 (`InventoryEventService.cs`, `PlayerSessionService.cs`, + cualquier otro caller de `Initialize`)
**Verificación:** Equipar ítem → save file actualizado. Comprar con moneda → save file actualizado.
**Desbloquea:** Los servicios de inventario pueden usarse en contexto headless con un callback no-op. Testeable con un mock de guardado.

---

### Fase 5 — HeroRuntimeProfile (diferir hasta multi-hero o servidor)

Solo implementar cuando haya un feature concreto que lo requiera (multi-hero, servidor headless, comparador de héroes).

```csharp
public class HeroRuntimeProfile
{
    public HeroData PersistentData { get; }       // el DTO serializable, sin cambios
    public HeroCalculatedAttributes Cached { get; set; }

    public IHeroIdentity   Identity   => PersistentData;
    public IHeroProgression Progression => PersistentData;
    public IHeroEconomy    Economy    => PersistentData;
    public IHeroSquads     Squads     => PersistentData;
    public IHeroInventory  Inventory  => PersistentData;
}
```

`PlayerSessionService.SelectedHero` pasaría a devolver `HeroRuntimeProfile`. Esto toca ~20 consumidores y es la mayor parte del trabajo. No hacerlo antes de tener el caso de uso.

---

## Resumen de riesgo por fase

| Fase | Riesgo | Archivos cambiados | Deployable independientemente |
|------|--------|-------------------|-------------------------------|
| 1 | Mínimo — solo adición | 2 (1 nuevo) | Sí |
| 2 | Bajo — un archivo por commit | ~15 en total | Sí, cada archivo |
| 3 | Medio — servicios mutadores | 8 archivos | Sí, un servicio a la vez |
| 4 | Medio — flujo de guardado | 2-3 archivos | Sí |
| 5 | Alto — API de sesión | ~20 archivos | Diferir |

## Decisiones de diseño clave

1. **HeroData no se parte en clases separadas.** `JsonUtility` requiere campos públicos en `[Serializable]`. Partir el objeto requeriría migrar el save file en disco de todos los jugadores.

2. **Implementación explícita de interfaces en HeroData.** Los consumidores que sigan usando `HeroData` directamente (durante la migración) ven los campos originales. Solo los consumidores ya migrados a interfaz ven la API limpia. Migración gradual sin ruptura.

3. **Interfaces de escritura heredan de lectura.** `IHeroEconomyMutator : IHeroEconomy`. Un servicio que necesita leer Y escribir recibe el mutador. Uno que solo lee recibe la interfaz de lectura. El compilador hace cumplir el contrato.

4. **El callback de guardado (Fase 4) es el cambio mínimo para romper el ciclo.** No se necesita service locator, ni event bus, ni dependency injection framework — solo un `Action` inyectado en `Initialize`.

---

## Archivos críticos

| Archivo | Fase |
|---------|------|
| `Assets/Scripts/Data/Persistence/Hero.Data.cs` | 1, 3 |
| `Assets/Scripts/Data/Persistence/IHeroDataFacets.cs` | 1 (nuevo) |
| `Assets/Scripts/Core/PlayerSessionService.cs` | 4 |
| `Assets/Scripts/Inventory/Services/InventoryEventService.cs` | 4 |
| `Assets/Scripts/Core/DataCacheService.cs` | 2 |
| `Assets/Scripts/Inventory/Services/EquipmentManagerService.cs` | 3 |
| `Assets/Scripts/Inventory/Services/InventoryStorageService.cs` | 3 |
| `Assets/Scripts/Services/CurrencyService.cs` | 3 |
| `Assets/Scripts/Hero/Systems/HeroVisualManagement.System.cs` | 2 |
