using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class AvatarCreatorUIController : MonoBehaviour
{
    [Header("Gender Buttons")]
    public Button maleButton;
    public Button femaleButton;

    [Header("Sliders Controller")]
    public AvatarPartSliderController sliderController;

    [Header("Class Selector")]
    [SerializeField]private HeroClassSelector classSelector;

    [Header("Hero Input")]
    public TMPro.TMP_InputField heroNameInput;
    [Header("Buttons")]
    [SerializeField] private Button continueButton;
    [SerializeField] private Button cancelButton;

    void Start()
    {
        maleButton.onClick.AddListener(() => { sliderController.avatarPartSelector.SetGender(Gender.Male); sliderController.SetupSliders(); });
        femaleButton.onClick.AddListener(() => { sliderController.avatarPartSelector.SetGender(Gender.Female); sliderController.SetupSliders(); });
        continueButton.onClick.AddListener(OnContinuePressed);
        cancelButton.onClick.AddListener(OnCancelPressed);

        // Set default gender (optional)
        sliderController.avatarPartSelector.SetGender(Gender.Male);
        sliderController.SetupSliders();
    }

    private void OnContinuePressed()
    {
        string heroName = heroNameInput.text.Trim();
        if (string.IsNullOrWhiteSpace(heroName))
        {
            Debug.LogWarning("Nombre del héroe vacío.");
            return;
        }
        Equipment equipment = classSelector.GetCurrentEquipment();
        AvatarParts avatar = sliderController.avatarPartSelector.GetCurrentSelection();
        HeroData hero = new HeroData
        {
            heroName = heroName,
            avatar = avatar,
            classId = classSelector.selectedClass.name,
            gender = sliderController.avatarPartSelector.currentGender.ToString(),
            level = 1,
            currentXP = 0,
            attributePoints = 0,
            perkPoints = 0,
            bronze = 0,
            equipment = equipment,
            availableSquads = new List<string> { "sqd01", "arc01", "spm01" }
        };

        PlayerData player = PlayerSessionService.CurrentPlayer;

        if (player == null)
        {
            Debug.LogError("No hay sesión de jugador activa.");
            return;
        }

        player.heroes.Add(hero);
        PlayerSessionService.SetSelectedHero(hero);
        SaveSystem.SavePlayer(player);

        SceneTransitionService.LoadScene("CharacterSelecctionScene");
    }
    private void OnCancelPressed()
    {
        SceneTransitionService.LoadScene("CharacterSelecctionScene");
    }
}
