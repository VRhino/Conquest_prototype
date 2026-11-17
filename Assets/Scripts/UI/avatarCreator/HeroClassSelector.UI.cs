using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;



using Data.Avatar;


public class HeroClassSelector : MonoBehaviour
{
    [Header("Modelo 3D")]
    [SerializeField] private Transform modularDummy; // Asignar desde el editor
    [Header("Setup")]
    [SerializeField] private Transform classContainer;
    [SerializeField] private GameObject classButtonPrefab;    

    [Header("Perks UI")]
    [SerializeField] private Transform perksContainer; // Asignar desde el editor
    [SerializeField] private GameObject perkItemPrefab; // Prefab con hijos "Icon" (Image) y "Label" (TMP_Text)
    [Header("Abilities UI")]
    [SerializeField] private Transform abilitiesContainer; // Asignar desde el editor
    [SerializeField] private string qAbilityName = "QAbility";
    [SerializeField] private string eAbilityName = "EAbility";
    [SerializeField] private string rAbilityName = "RAbility";
    [SerializeField] private string ultimateAbilityName = "Ultimate";

    [Header("UI de detalles")]
    [SerializeField] private TMP_Text classNameText;
    [SerializeField] private Image classIconImage;
    [SerializeField] private TMP_Text classDescriptionText;
    [SerializeField] private TMP_Text vitalityText;
    [SerializeField] private TMP_Text dexterityText;
    [SerializeField] private TMP_Text strengthText;
    [SerializeField] private TMP_Text armorText;

    [Header("Character Selector")]
    [SerializeField] private AvatarPartSelector avatarPartSelector;

    
    private AvatarPartDatabase avatarPartDatabase;


    void Start()
    {
        // Cargar la base de datos desde Resources si no está asignada
        if (avatarPartDatabase == null)
        {
            avatarPartDatabase = Resources.Load<AvatarPartDatabase>("Data/Avatar/AvatarPartDatabase");
            if (avatarPartDatabase == null)
            {
                Debug.LogError("No se pudo cargar AvatarPartDatabase desde Resources/Data/Avatar/AvatarPartDatabase");
            }
        }

        // Las clases del motor (el mundo de BronzeAge abre el creador con su encargo).
        if (ConquestTactics.Mundo.SelectorDePersonaje.Actual != null)
            BotonesDelMotor(ConquestTactics.Mundo.SelectorDePersonaje.Actual.Clases);
    }
    // ───────────── clases del motor (BronzeAge) ─────────────

    /// <summary>La clase del motor elegida (solo en el flujo de BronzeAge).</summary>
    public ConquestTactics.Mundo.ClaseDeHeroe ClaseDelMotor { get; private set; }

    /// <summary>Las clases de `/v1/datos`: un botón por clase, su ficha (armaduras, atributos y equipo inicial) y la armadura inicial en el
    /// maniquí. Las ventajas y habilidades son de las clases de relleno: no se enseñan.</summary>
    private void BotonesDelMotor(List<ConquestTactics.Mundo.ClaseDeHeroe> clases)
    {
        foreach (Transform child in classContainer)
            Destroy(child.gameObject);
        if (perksContainer) perksContainer.gameObject.SetActive(false);
        if (abilitiesContainer) abilitiesContainer.gameObject.SetActive(false);
        if (classIconImage) classIconImage.gameObject.SetActive(false);
        // Los atributos van en la ficha: sus cuatro cajas se pisan con los nombres en español.
        foreach (var t in new[] { vitalityText, dexterityText, strengthText, armorText })
            if (t) t.gameObject.SetActive(false);
        var titulo = classContainer.parent.Find("PanelTitle")?.GetComponentInChildren<TMP_Text>();
        if (titulo) titulo.text = "Tu clase";
        // Son 5 (las de relleno, 4) y con nombres largos: cada botón con su parte del ancho de la fila y el nombre en dos líneas si hace falta.
        var fila = classContainer.GetComponent<HorizontalLayoutGroup>();
        if (fila) { fila.spacing = 6; fila.padding = new RectOffset(0, 0, fila.padding.top, fila.padding.bottom); }
        var ancho = ((RectTransform)classContainer).rect.width / Mathf.Max(1, clases.Count) - 6;
        etiquetasDelMotor.Clear();
        foreach (var clase in clases)
        {
            var esta = clase;
            var buttonGO = Instantiate(classButtonPrefab, classContainer);
            buttonGO.name = "Clase " + clase.Id;
            var caja = (RectTransform)buttonGO.transform;
            caja.sizeDelta = new Vector2(ancho, caja.sizeDelta.y);
            var icon = buttonGO.transform.Find("Icon");
            if (icon) icon.gameObject.SetActive(false);
            var label = buttonGO.transform.Find("Label")?.GetComponent<TMP_Text>();
            if (label)
            {
                label.text = clase.Nombre;
                label.enableAutoSizing = true;
                label.enableWordWrapping = true;
                label.rectTransform.anchorMin = Vector2.zero;
                label.rectTransform.anchorMax = Vector2.one;
                label.rectTransform.offsetMin = new Vector2(2, 0);
                label.rectTransform.offsetMax = new Vector2(-2, 0);
                label.fontSizeMin = 10;
                label.fontSizeMax = Mathf.Min(label.fontSize, 17); // los nombres del motor son largos («Lanza y escudo grande redondo»): en dos líneas
                etiquetasDelMotor[clase] = label;
            }
            buttonGO.GetComponent<Button>()?.onClick.AddListener(() => ElegirDelMotor(esta));
        }
        if (clases.Count > 0) ElegirDelMotor(clases[0]);
    }

    private readonly Dictionary<ConquestTactics.Mundo.ClaseDeHeroe, TMP_Text> etiquetasDelMotor = new();
    private static readonly Color Elegida = new Color(0.85f, 0.72f, 0.47f);

    public void ElegirDelMotor(ConquestTactics.Mundo.ClaseDeHeroe clase)
    {
        ClaseDelMotor = clase;
        foreach (var kv in etiquetasDelMotor) kv.Value.color = kv.Key == clase ? Elegida : Color.white;
        int Atributo(string k) => clase.Atributos.TryGetValue(k, out var v) ? v : 0;
        if (classNameText) classNameText.text = clase.Nombre;
        if (classDescriptionText)
            classDescriptionText.text = $"{clase.TextoDeArmaduras}.\nFuerza {Atributo("fuerza")} · Destreza {Atributo("destreza")} · Armadura {Atributo("armadura")} · Vitalidad {Atributo("vitalidad")}\nNace con: {string.Join(" · ", clase.EquipoInicial)}";
        VestirClaseDelMotor();
    }

    /// <summary>El maniquí con la armadura inicial de la clase del motor (ligera, media o pesada), también al cambiar de género.</summary>
    public void VestirClaseDelMotor()
    {
        if (ClaseDelMotor == null) return;
        Data.Avatar.AvatarVisualUtils.ResetModularDummyToBase(modularDummy, avatarPartDatabase, new List<string>(), avatarPartSelector.currentGender);
        var peso = ClaseDelMotor.ArmaduraInicial == "pesada" || ClaseDelMotor.ArmaduraInicial == "media" ? ClaseDelMotor.ArmaduraInicial : "ligera";
        foreach (var hueco in new[] { "torso", "guantes", "pantalones", "botas" })
        {
            var parte = AspectoDePiezas.Parte($"{hueco}_{peso}_inicio");
            if (!string.IsNullOrEmpty(parte))
                Data.Avatar.AvatarVisualUtils.ToggleArmorVisibilityByAvatarPartId(modularDummy, avatarPartDatabase, parte, avatarPartSelector.currentGender);
        }
    }
}
