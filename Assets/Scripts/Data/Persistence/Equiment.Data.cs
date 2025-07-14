/// <summary>
/// Current functional equipment worn by the hero.
/// Each field stores the ID of an item owned in the inventory.
/// </summary>
[Serializable]
public class Equipment
{
    public string weaponId = string.Empty;
    public string helmetId = string.Empty;
    public string torsoId = string.Empty;
    public string glovesId = string.Empty;
    public string pantsId = string.Empty;
}