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

    [Header("Hero Input")]
    public TMPro.TMP_InputField heroNameInput;
    [Header("Buttons")]
    [SerializeField] private Button continueButton;
    [SerializeField] private Button cancelButton;

    void Start()
    {
        maleButton.onClick.AddListener(() => avatarPartSelector.SetGender(AvatarPartSelector.Gender.Male));
        femaleButton.onClick.AddListener(() => avatarPartSelector.SetGender(AvatarPartSelector.Gender.Female));
        continueButton.onClick.AddListener(OnContinuePressed);
        cancelButton.onClick.AddListener(OnCancelPressed);

        // Asignar sliders a AvatarPartSelector
        avatarPartSelector.hairSlider = hairSlider;
        avatarPartSelector.headSlider = headSlider;
        avatarPartSelector.eyebrowSlider = eyebrowSlider;
        avatarPartSelector.facialHairSlider = facialHairSlider;

        // Set default gender (optional)
        avatarPartSelector.SetGender(AvatarPartSelector.Gender.Male);
    }

    private void OnContinuePressed()
    {
        string heroName = heroNameInput.text.Trim();
        if (string.IsNullOrWhiteSpace(heroName))
        {
            Debug.LogWarning("Nombre del héroe vacío.");
            return;
        }

        AvatarParts avatar = avatarPartSelector.GetCurrentSelection();
        HeroData hero = new HeroData
        {
            heroName = heroName,
            avatar = avatar,
            gender = avatarPartSelector.currentGender.ToString(),
            level = 1,
            currentXP = 0,
            attributePoints = 0,
            perkPoints = 0,
            bronze = 0,            
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

        SceneManager.LoadScene("FeudoScene");
    }
    private void OnCancelPressed()
    {
        PlayerSessionService.Clear();
        SceneManager.LoadScene("LoginScene");
    }
}
