using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

using Data.Avatar;

public class AvatarPartSelector : MonoBehaviour
{
    // Constantes para slots y claves
    private const string SLOT_HEAD = "Head";
    private const string SLOT_HAIR = "Hair";
    private const string SLOT_EYEBROW = "Eyebrow";
    private const string SLOT_FACIALHAIR = "FacialHair";
    private AvatarPartDatabase avatarPartDatabase;
    public Gender currentGender;

    [Header("References to part roots")]
    public GameObject allGenderParts;
    public GameObject maleParts;
    public GameObject femaleParts;

    [Header("Sliders (assigned from UI)")]
    public Slider hairSlider;
    public Slider headSlider;
    public Slider eyebrowSlider;
    public Slider facialHairSlider; // Only active for male


    private readonly Dictionary<string, Transform> currentPartGroups = new();

    private void Awake()
    {
        // Cargar la base de datos desde Resources
        avatarPartDatabase = Resources.Load<AvatarPartDatabase>("Data/Avatar/AvatarPartDatabase");
        if (avatarPartDatabase == null)
        {
            Debug.LogError("No se pudo cargar AvatarPartDatabase desde Resources/Data/Avatar/AvatarPartDatabase");
        }
    }

    public void SetGender(Gender gender)
    {
        currentGender = gender;

        // Mostrar solo grupos correspondientes
        allGenderParts.SetActive(true);
        maleParts.SetActive(gender == Gender.Male);
        femaleParts.SetActive(gender == Gender.Female);

        // Reasignar grupos de partes visibles
        currentPartGroups.Clear();

        if (allGenderParts != null)
            currentPartGroups[SLOT_HAIR] = allGenderParts.transform.Find("All_01_Hair");

        if (gender == Gender.Male)
        {
            var head = maleParts.transform.Find("Male_00_Head/Male_Head_All_Elements");
            var eyebrow = maleParts.transform.Find("Male_01_Eyebrows");
            var facial = maleParts.transform.Find("Male_02_FacialHair");
            currentPartGroups[SLOT_HEAD] = head;
            currentPartGroups[SLOT_EYEBROW] = eyebrow;
            currentPartGroups[SLOT_FACIALHAIR] = facial;
        }
        else
        {
            currentPartGroups[SLOT_HEAD] = femaleParts.transform.Find("Female_00_Head/Female_Head_All_Elements");
            currentPartGroups[SLOT_EYEBROW] = femaleParts.transform.Find("Female_01_Eyebrows");
        }

        SetupSliders();
    }

    private void SetupSliders()
    {
        SetSlider(hairSlider, SLOT_HAIR);
        SetSlider(headSlider, SLOT_HEAD);
        SetSlider(eyebrowSlider, SLOT_EYEBROW);

        if (facialHairSlider != null)
        {
            facialHairSlider.gameObject.SetActive(currentGender == Gender.Male);
            if (currentGender == Gender.Male)
                SetSlider(facialHairSlider, SLOT_FACIALHAIR);
        }
    }

    private void SetSlider(Slider slider, string partType)
    {
        if (!currentPartGroups.ContainsKey(partType) || slider == null) return;

        Transform group = currentPartGroups[partType];
        int count = group.childCount;

        slider.maxValue = count - 1;
        slider.minValue = 0;
        slider.wholeNumbers = true;
        slider.onValueChanged.RemoveAllListeners();
        slider.onValueChanged.AddListener(index => ShowOnly(group, (int)index));

        // Mostrar la primera parte al cambiar género
        slider.value = 0;
        ShowOnly(group, 0);
    }

    private void ShowOnly(Transform group, int indexToShow)
    {
        if (group == null)
            return;

        int total = group.childCount;

        for (int i = 0; i < total; i++)
        {
            var child = group.GetChild(i);
            bool isActive = i == indexToShow;
            child.gameObject.SetActive(isActive);
        }
    }
    public AvatarParts GetCurrentSelection()
    {
        string MapPrefabToId(string slot, string prefabName)
        {
            if (avatarPartDatabase == null || string.IsNullOrEmpty(prefabName)) return string.Empty;
            List<AvatarPartDefinition> list = null;
            switch (slot)
            {
                case SLOT_HEAD: list = avatarPartDatabase.faceParts; break;
                case SLOT_HAIR: list = avatarPartDatabase.hairParts; break;
                case SLOT_EYEBROW: list = avatarPartDatabase.eyebrowsParts; break;
                case SLOT_FACIALHAIR: list = avatarPartDatabase.beardParts; break;
            }
            if (list == null) return string.Empty;
            foreach (var def in list)
            {
                if (def.attachments == null) continue;
                foreach (var att in def.attachments)
                {
                    if (currentGender == Gender.Male && att.prefabPathMale == prefabName)
                        return def.id;
                    if (currentGender == Gender.Female && att.prefabPathFemale == prefabName)
                        return def.id;
                }
            }
            return string.Empty;
        }

        string GetActivePrefab(string key)
        {
            if (!currentPartGroups.TryGetValue(key, out var group)) return string.Empty;
            foreach (Transform child in group)
            {
                if (child.gameObject.activeSelf)
                    return child.name;
            }
            return string.Empty;
        }

        return new AvatarParts
        {
            headId = MapPrefabToId(SLOT_HEAD, GetActivePrefab(SLOT_HEAD)),
            hairId = MapPrefabToId(SLOT_HAIR, GetActivePrefab(SLOT_HAIR)),
            eyebrowId = MapPrefabToId(SLOT_EYEBROW, GetActivePrefab(SLOT_EYEBROW)),
            beardId = currentGender == Gender.Male ? MapPrefabToId(SLOT_FACIALHAIR, GetActivePrefab(SLOT_FACIALHAIR)) : string.Empty
        };
    }
}
