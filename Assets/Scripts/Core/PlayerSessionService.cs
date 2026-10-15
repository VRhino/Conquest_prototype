using UnityEngine;

public static class PlayerSessionService
{
    public static PlayerData CurrentPlayer { get; private set; }
    public static HeroData SelectedHero { get; private set; }

    public static void SetPlayer(PlayerData data)
    {
        if (data == null)
        {
            Debug.LogError("PlayerSessionService: Tried to set null PlayerData.");
            return;
        }

        Clear();
        CurrentPlayer = data;
        SelectedHero = null; // Reiniciar héroe por si hay cambio de usuario
    }

    public static void SetSelectedHero(HeroData hero)
    {
        if (hero == null)
        {
            Debug.LogError("PlayerSessionService: Tried to set null HeroData.");
            return;
        }
        DataCacheService.CleanupEventListeners();
        SelectedHero = hero;
        InventoryManager.Initialize(hero);
        InventoryEventService.Initialize(hero, () => SaveSystem.SavePlayer(CurrentPlayer));
        DataCacheService.CacheAttributes(hero);
        Debug.Log($"[PlayerSessionService]Hero seleccionado: {hero.heroName}");
        Debug.Log($"[PlayerSessionService]Pantalones del héroe seleccionado: {hero.equipment.pants}");
        
        // Inicializar listeners para el nuevo héroe
        DataCacheService.InitializeEventListeners();
        InventoryEventService.NotifyContextChanged();
    }

    public static bool IsSessionActive => CurrentPlayer != null;
    public static bool HasHero => SelectedHero != null;

    public static void Clear()
    {
        // Limpiar listeners antes de cerrar sesión
        DataCacheService.CleanupEventListeners();
        DataCacheService.Clear();
        InventoryManager.Reset();
        
        CurrentPlayer = null;
        SelectedHero = null;
        InventoryEventService.NotifyContextChanged();
        Debug.Log("Sesión reiniciada.");
    }
}
