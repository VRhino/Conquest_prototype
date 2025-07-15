using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class AvatarPartSelector : MonoBehaviour
{
    public enum Gender { Male, Female }
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
            currentPartGroups["Hair"] = allGenderParts.transform.Find("All_01_Hair");

        if (gender == Gender.Male)
        {
            var head = maleParts.transform.Find("Male_00_Head/Male_Head_All_Elements");
            var eyebrow = maleParts.transform.Find("Male_01_Eyebrows");
            var facial = maleParts.transform.Find("Male_02_FacialHair");
            currentPartGroups["Head"] = head;
            currentPartGroups["Eyebrow"] = eyebrow; ;
            currentPartGroups["FacialHair"] = facial;
        }
        else
        {
            currentPartGroups["Head"] = femaleParts.transform.Find("Female_00_Head/Female_Head_All_Elements");
            currentPartGroups["Eyebrow"] = femaleParts.transform.Find("Female_01_Eyebrows");
        }

        SetupSliders();
    }

    private void SetupSliders()
    {
        SetSlider(hairSlider, "Hair");
        SetSlider(headSlider, "Head");
        SetSlider(eyebrowSlider, "Eyebrow");

        if (facialHairSlider != null)
        {
            facialHairSlider.gameObject.SetActive(currentGender == Gender.Male);
            if (currentGender == Gender.Male)
                SetSlider(facialHairSlider, "FacialHair");
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
        string GetActiveName(string key)
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
            headId = GetActiveName("Head"),
            hairId = GetActiveName("Hair"),
            eyebrowId = GetActiveName("Eyebrow"),
            beardId = currentGender == Gender.Male ? GetActiveName("FacialHair") : string.Empty
        };
    }
}
