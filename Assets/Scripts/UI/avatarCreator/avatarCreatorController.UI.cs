using System.Collections.Generic;
using ConquestTactics.Mundo;
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
        maleButton.onClick.AddListener(() => { sliderController.avatarPartSelector.SetGender(Gender.Male); sliderController.SetupSliders(); if (SelectorDePersonaje.Actual != null) classSelector.VestirClaseDelMotor(); });
        femaleButton.onClick.AddListener(() => { sliderController.avatarPartSelector.SetGender(Gender.Female); sliderController.SetupSliders(); if (SelectorDePersonaje.Actual != null) classSelector.VestirClaseDelMotor(); });
        continueButton.onClick.AddListener(OnContinuePressed);
        cancelButton.onClick.AddListener(OnCancelPressed);

        // Set default gender (optional)
        sliderController.avatarPartSelector.SetGender(Gender.Male);
        sliderController.SetupSliders();

        if (SelectorDePersonaje.Actual != null) PrepararParaElMotor(SelectorDePersonaje.Actual);
    }

    private void OnContinuePressed()
    {
        if (SelectorDePersonaje.Actual != null) { NacerEnElMotor(); return; }
        string heroName = heroNameInput.text.Trim();
        if (string.IsNullOrWhiteSpace(heroName))
        {
            Debug.LogWarning("Nombre del héroe vacío.");
            return;
        }
        Equipment equipment = classSelector.GetCurrentEquipment();
        AvatarParts avatar = sliderController.avatarPartSelector.GetCurrentSelection();
        HeroData hero = HeroDataService.CreateNewHero(
            heroName, 
            avatar, 
            classSelector.selectedClass.name, 
            sliderController.avatarPartSelector.currentGender.ToString(), 
            equipment
        );

        PlayerData player = PlayerSessionService.CurrentPlayer;

        if (player == null)
        {
            Debug.LogError("No hay sesión de jugador activa.");
            return;
        }

        player.heroes.Add(hero);
        PlayerSessionService.SetSelectedHero(hero);
        SaveSystem.SavePlayer(player);

        SceneTransitionService.LoadScene(SceneTransitionService.SceneNames.CharacterSelection);
    }
    private void OnCancelPressed()
    {
        if (SelectorDePersonaje.Actual != null) { SelectorDePersonaje.Actual.Volver(); return; }
        SceneTransitionService.LoadScene(SceneTransitionService.SceneNames.CharacterSelection);
    }

    // ───────────── desde el mundo de BronzeAge ─────────────
    // Se llega con el lugar y el nombre ya elegidos en «Nace tu héroe»: aquí se ven como texto y se eligen la clase del motor, el género y el
    // aspecto. «Nacer» manda `crearHeroe` por el encargo (nada de `player_save.json`); «Volver» vuelve sin crear nada.

    private TMPro.TMP_Text errorDelMotor;
    private bool enviando;

    private void PrepararParaElMotor(SelectorDePersonaje.Encargo encargo)
    {
        heroNameInput.text = string.IsNullOrEmpty(encargo.Nombre) ? "Sin nombre (se llamará «Héroe N»)" : encargo.Nombre;
        heroNameInput.interactable = false;
        var panel = heroNameInput.transform.parent.parent;
        var titulo = panel.Find("PanelTitle")?.GetComponentInChildren<TMPro.TMP_Text>();
        if (titulo) { titulo.text = $"Naces en {encargo.Lugar}"; titulo.enableWordWrapping = false; titulo.enableAutoSizing = true; titulo.fontSizeMin = 14; titulo.fontSizeMax = titulo.fontSize; }
        var tituloNombre = heroNameInput.transform.parent.Find("NameTitle")?.GetComponentInChildren<TMPro.TMP_Text>();
        if (tituloNombre) tituloNombre.text = "Tu nombre";
        continueButton.GetComponentInChildren<TMPro.TMP_Text>().text = "Nacer";
        cancelButton.GetComponentInChildren<TMPro.TMP_Text>().text = "Volver";

        // El rechazo del motor, encima de los botones.
        if (tituloNombre)
        {
            errorDelMotor = Instantiate(tituloNombre, continueButton.transform.parent);
            errorDelMotor.name = "ErrorDelMotor";
            var r = errorDelMotor.rectTransform;
            var b = (RectTransform)continueButton.transform;
            r.anchorMin = b.anchorMin; r.anchorMax = b.anchorMax; r.pivot = new Vector2(0, 0);
            r.anchoredPosition = b.anchoredPosition + new Vector2(-b.sizeDelta.x * b.pivot.x, b.sizeDelta.y * (1 - b.pivot.y) + 6);
            r.sizeDelta = new Vector2(420, 60);
            errorDelMotor.enableWordWrapping = true;
            errorDelMotor.alignment = TMPro.TextAlignmentOptions.BottomLeft;
            errorDelMotor.fontSize = Mathf.Min(errorDelMotor.fontSize, 20);
            errorDelMotor.color = new Color(0.86f, 0.33f, 0.31f);
            errorDelMotor.text = "";
        }
    }

    private async void NacerEnElMotor()
    {
        var encargo = SelectorDePersonaje.Actual;
        if (enviando || encargo == null) return;
        if (classSelector.ClaseDelMotor == null) { MostrarError("Elige tu clase."); return; }
        enviando = true;
        continueButton.interactable = false;
        MostrarError("");
        var piezas = sliderController.avatarPartSelector;
        var avatar = piezas.GetCurrentSelection();
        string rechazo;
        try
        {
            rechazo = await encargo.Confirmar(new SelectorDePersonaje.Eleccion
            {
                ClaseId = classSelector.ClaseDelMotor.Id,
                Genero = piezas.currentGender == Gender.Female ? "femenino" : "masculino",
                Cabeza = avatar.headId, Pelo = avatar.hairId, Barba = avatar.beardId, Cejas = avatar.eyebrowId,
            });
        }
        catch (System.Exception e) { rechazo = e.Message; }
        enviando = false;
        if (this == null) return; // aceptado: la escena ya se ha descargado
        continueButton.interactable = true;
        MostrarError(rechazo);
    }

    private void MostrarError(string texto)
    {
        if (errorDelMotor) errorDelMotor.text = texto ?? "";
        else if (!string.IsNullOrEmpty(texto)) Debug.LogWarning(texto);
    }
}
