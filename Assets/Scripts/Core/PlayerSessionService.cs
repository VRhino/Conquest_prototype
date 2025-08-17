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

        SelectedHero = hero;
        Debug.Log($"[PlayerSessionService]Hero seleccionado: {hero.heroName}");
        Debug.Log($"[PlayerSessionService]Pantalones del héroe seleccionado: {hero.equipment.pants}");
    }

    public static bool IsSessionActive => CurrentPlayer != null;
    public static bool HasHero => SelectedHero != null;

    public static void Clear()
    {
        CurrentPlayer = null;
        SelectedHero = null;
        Debug.Log("Sesión reiniciada.");
    }
}
