# HeroData God Object — Plan de Refactor (A-01)

> Fecha: 2026-03-28
> Contexto: `CouplingAnalysis.md` sección A-01

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
