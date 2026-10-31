using Unity.Entities;

/// <summary>
/// Managed component that carries visual appearance data for a hero entity.
/// Added by BattleSceneController when spawning remote heroes so that
/// HeroVisualAppearanceSystem can apply the correct avatar parts and equipment.
/// </summary>
public class HeroAppearanceComponent : IComponentData
{
    public AvatarParts avatar   = new();
    public Equipment   equipment = new();
    public string      gender   = "Male";

    public static HeroAppearanceComponent Capture(AvatarParts avatar, Equipment equipment, string gender)
        => new HeroAppearanceComponent
        {
            avatar = avatar == null ? new AvatarParts() : UnityEngine.JsonUtility.FromJson<AvatarParts>(UnityEngine.JsonUtility.ToJson(avatar)),
            equipment = equipment == null ? new Equipment() : UnityEngine.JsonUtility.FromJson<Equipment>(UnityEngine.JsonUtility.ToJson(equipment)),
            gender = gender
        };
}
