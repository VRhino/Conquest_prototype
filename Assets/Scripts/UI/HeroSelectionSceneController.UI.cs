using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>El selector de personaje: el héroe del motor, con «Entrar al mundo» y «Cerrar sesión».</summary>
public class HeroSelectionSceneController : MonoBehaviour
{
    [Header("UI")]
    public Transform heroButtonContainer;
    public GameObject heroButtonPrefab;
    public Button confirmButton;

    [Header("Preview")]
    public Transform dummyRoot;

    [Header("Buttons")]
    [SerializeField] private Button createHeroButton;
    [SerializeField] private Button deleteHeroButton;
    [SerializeField] private Button exitButton;


    void Start()
    {
        // El héroe de la cuenta en el motor (el mundo de BronzeAge abre esta escena con él). La lista de héroes de player_save.json, fuera.
        if (ConquestTactics.Mundo.SelectorDePersonaje.Heroe != null)
            ModoDelMotor(ConquestTactics.Mundo.SelectorDePersonaje.Heroe);

    }

    // ───────────── desde el mundo de BronzeAge ─────────────
    // Un solo héroe (el motor tiene uno por cuenta y partida): sin crear ni borrar, «Entrar al mundo» y «Cerrar sesión», textos en español.

    private void ModoDelMotor(ConquestTactics.Mundo.SelectorDePersonaje.HeroeParaVer heroe)
    {
        // Crear y borrar, fuera: el motor tiene un héroe por cuenta y no hay orden de borrarlo (pedido a futuro en el bloque 4 de la Hoja).
        createHeroButton.gameObject.SetActive(false);
        deleteHeroButton.gameObject.SetActive(false);
        // El lienzo está hecho a 1920×1080 con píxeles fijos: a otro tamaño «Entrar» y la ficha se salían. Aquí escala con la ventana, como
        // el creador.
        var escalador = confirmButton.GetComponentInParent<CanvasScaler>();
        if (escalador)
        {
            escalador.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            escalador.referenceResolution = new Vector2(1920, 1080);
            escalador.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand; // los 1920×1080 caben siempre enteros
        }
        PonerTexto(confirmButton, "Entrar al mundo");
        PonerTexto(exitButton, "Cerrar sesión");
        var salir = exitButton.GetComponentInChildren<TMP_Text>();
        if (salir) { salir.enableWordWrapping = false; salir.enableAutoSizing = true; salir.fontSizeMin = 8; salir.fontSizeMax = salir.fontSize; }
        // «Cerrar sesión» no cabe en el botón de «Exit»: más ancho, creciendo hacia la derecha.
        var cajaSalir = (RectTransform)exitButton.transform;
        var ancho = Mathf.Max(cajaSalir.rect.width, 150);
        cajaSalir.anchoredPosition += new Vector2((ancho - cajaSalir.rect.width) * cajaSalir.pivot.x, 0);
        cajaSalir.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, ancho);
        var titulo = heroButtonContainer.parent.Find("Title")?.GetComponentInChildren<TMP_Text>();
        if (titulo) titulo.text = "Tu héroe";
        confirmButton.onClick.AddListener(() => { confirmButton.interactable = false; exitButton.interactable = false; heroe.Entrar(); });
        exitButton.onClick.AddListener(() => { confirmButton.interactable = false; exitButton.interactable = false; heroe.CerrarSesion(); });

        foreach (Transform child in heroButtonContainer)
            Destroy(child.gameObject);
        var ficha = Instantiate(heroButtonPrefab, heroButtonContainer);
        ficha.name = "Heroe del motor";
        // El escudo de su Facción (30 px, como la pantalla de partidas del prototipo); sin Facción, nada.
        var icono = ficha.transform.Find("Icon");
        if (icono)
        {
            var imagen = icono.GetComponent<Image>();
            icono.gameObject.SetActive(heroe.Sigilo != null && imagen != null);
            if (heroe.Sigilo != null && imagen != null)
            {
                imagen.sprite = TexturaDeSigilo.SpriteDe(heroe.Sigilo, 60);
                imagen.color = Color.white;
                imagen.preserveAspect = true;
            }
        }
        var nombre = ficha.transform.Find("Name")?.GetComponent<TMP_Text>();
        if (nombre) nombre.text = heroe.Nombre;
        var nivel = ficha.transform.Find("Level")?.GetComponent<TMP_Text>();
        if (nivel)
        {
            nivel.text = $"Nivel {heroe.Nivel} · {heroe.Clase?.Nombre ?? "clase desconocida"} · {heroe.Faccion}";
            nivel.enableAutoSizing = true;
            nivel.fontSizeMin = 10;
            nivel.fontSizeMax = nivel.fontSize;
        }
        VestirDelMotor(heroe);
    }

    private static void PonerTexto(Button boton, string texto)
    {
        var t = boton ? boton.GetComponentInChildren<TMP_Text>() : null;
        if (t) t.text = texto;
    }

    /// <summary>El maniquí vestido con el héroe del motor (`VestidoDelMotor`).</summary>
    private void VestirDelMotor(ConquestTactics.Mundo.SelectorDePersonaje.HeroeParaVer heroe)
    {
        if (dummyRoot == null || dummyRoot.childCount == 0) return;
        dummyRoot.gameObject.SetActive(true);
        VestidoDelMotor.Vestir(dummyRoot, heroe);
    }
}
