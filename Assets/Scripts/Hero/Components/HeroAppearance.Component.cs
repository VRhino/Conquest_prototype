using Unity.Entities;

/// <summary>
/// Managed component that carries visual appearance data for a hero entity.
/// Added by BattleSceneController when spawning remote heroes so that
/// HeroVisualManagementSystem can apply the correct avatar parts and equipment.
/// </summary>
public class HeroAppearanceComponent : IComponentData
{
    public AvatarParts avatar   = new();
    public Equipment   equipment = new();
    public string      gender   = "Male";
}
