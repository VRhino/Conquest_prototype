using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class AvatarCreatorUIController : MonoBehaviour
{
    [Header("Gender Buttons")]
    public Button maleButton;
    public Button femaleButton;

    [Header("Sliders")]
    public Slider hairSlider;
    public Slider headSlider;
    public Slider eyebrowSlider;
    public Slider facialHairSlider;

    [Header("Character Selector")]
    public AvatarPartSelector avatarPartSelector;

    [Header("Class Selector")]
    [SerializeField]private HeroClassSelector classSelector;

    [Header("Hero Input")]
    public TMPro.TMP_InputField heroNameInput;
    [Header("Buttons")]
    [SerializeField] private Button continueButton;
    [SerializeField] private Button cancelButton;

    void Start()
    {
        maleButton.onClick.AddListener(() => avatarPartSelector.SetGender(Gender.Male));
        femaleButton.onClick.AddListener(() => avatarPartSelector.SetGender(Gender.Female));
        continueButton.onClick.AddListener(OnContinuePressed);
        cancelButton.onClick.AddListener(OnCancelPressed);

        // Asignar sliders a AvatarPartSelector
        avatarPartSelector.hairSlider = hairSlider;
        avatarPartSelector.headSlider = headSlider;
        avatarPartSelector.eyebrowSlider = eyebrowSlider;
        avatarPartSelector.facialHairSlider = facialHairSlider;

        // Set default gender (optional)
        avatarPartSelector.SetGender(Gender.Male);
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
        AvatarParts avatar = avatarPartSelector.GetCurrentSelection();
        HeroData hero = new HeroData
        {
            heroName = heroName,
            avatar = avatar,
            classId = classSelector.selectedClass.name,
            gender = avatarPartSelector.currentGender.ToString(),
            level = 1,
            currentXP = 0,
            attributePoints = 0,
            perkPoints = 0,
            bronze = 0,
            equipment = equipment,        
            // Aquí puedes configurar claseId, stats iniciales, etc.
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

        SceneManager.LoadScene("CharacterSelecctionScene");
    }
    private void OnCancelPressed()
    {
        PlayerSessionService.Clear();
        SceneManager.LoadScene("CharacterSelecctionScene");
    }
}
