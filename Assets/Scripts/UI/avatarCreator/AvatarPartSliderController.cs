using UnityEngine;
using UnityEngine.UI;

public class AvatarPartSliderController : MonoBehaviour
{
    [Header("Sliders (assigned from UI)")]
    public Slider hairSlider;
    public Slider headSlider;
    public Slider eyebrowSlider;
    public Slider facialHairSlider; // Only active for male

    [Header("References")]
    public AvatarPartSelector avatarPartSelector;

    private void Start()
    {
        SetupSliders();
    }

    public void SetupSliders()
    {
        SetupSlider(hairSlider, "Hair");
        SetupSlider(headSlider, "Head");
        SetupSlider(eyebrowSlider, "Eyebrow");

        if (facialHairSlider != null)
        {
            facialHairSlider.gameObject.SetActive(avatarPartSelector.currentGender == Gender.Male);
            if (avatarPartSelector.currentGender == Gender.Male)
                SetupSlider(facialHairSlider, "FacialHair");
        }
    }

    private void SetupSlider(Slider slider, string partType)
    {
        if (slider == null) return;
        var group = avatarPartSelector.GetPartGroup(partType);
        if (group == null) return;
        int count = group.childCount;
        slider.maxValue = count - 1;
        slider.minValue = 0;
        slider.wholeNumbers = true;
        slider.onValueChanged.RemoveAllListeners();
        slider.onValueChanged.AddListener(index => avatarPartSelector.ShowOnly(group, (int)index));
        // Mostrar la primera parte al cambiar género
        slider.value = 0;
        avatarPartSelector.ShowOnly(group, 0);
    }
}
