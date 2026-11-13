using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using Data.Items;

public class HeroSelectionSceneController : MonoBehaviour
{
    [Header("UI")]
    public Transform heroButtonContainer;
    public GameObject heroButtonPrefab;
    public Button confirmButton;

    [Header("Preview")]
    public Transform dummyRoot;

    private HeroData selectedHero;

    [Header("Buttons")]
    [SerializeField] private Button createHeroButton;
    [SerializeField] private Button deleteHeroButton;
    [SerializeField] private Button exitButton;

    [SerializeField] private List<string> basePartIds = new();

    void Start()
    {
        // Desde el mundo de BronzeAge: el héroe de la cuenta en el motor, en vez de los de player_save.json.
        if (ConquestTactics.Mundo.SelectorDePersonaje.Heroe != null)
        {
            ModoDelMotor(ConquestTactics.Mundo.SelectorDePersonaje.Heroe);
            return;
        }
        LoadHeroButtons();
        confirmButton.onClick.AddListener(OnConfirm);
        createHeroButton.onClick.AddListener(OnCreateHero);
        deleteHeroButton.onClick.AddListener(OnDeleteHero);
        exitButton.onClick.AddListener(OnExitPressed);

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
        var icono = ficha.transform.Find("Icon");
        if (icono) icono.gameObject.SetActive(false);
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

    /// <summary>El maniquí con el aspecto que guardó el motor (género, cabeza, pelo, barba, cejas) y la armadura que lleva (ligera, media o
    /// pesada; las piezas de inicio de Conquest de ese tipo). El equipo puesto pieza a pieza espera a la armería (paso 6 del bloque 4).</summary>
    private void VestirDelMotor(ConquestTactics.Mundo.SelectorDePersonaje.HeroeParaVer heroe)
    {
        if (dummyRoot == null || dummyRoot.childCount == 0) return;
        dummyRoot.gameObject.SetActive(true);
        var piezas = Resources.Load<Data.Avatar.AvatarPartDatabase>("Data/Avatar/AvatarPartDatabase");
        if (piezas == null) { Debug.LogError("No se pudo cargar AvatarPartDatabase."); return; }
        var genero = heroe.Femenino ? Gender.Female : Gender.Male;
        var aspecto = new List<string>();
        foreach (var id in new[] { heroe.Cabeza, heroe.Pelo, heroe.Barba, heroe.Cejas })
            if (!string.IsNullOrEmpty(id)) aspecto.Add(id);
        Data.Avatar.AvatarVisualUtils.ResetModularDummyToBase(dummyRoot, piezas, aspecto, genero);
        var sufijo = heroe.Armadura == "pesada" ? "H" : heroe.Armadura == "media" ? "M" : "L";
        foreach (var itemId in new[] { $"Tor{sufijo}ADef", $"Glo{sufijo}ADef", $"Pan{sufijo}ADef", $"Boot{sufijo}ADef" })
        {
            var item = ItemService.GetItemById(itemId);
            if (item != null && !string.IsNullOrEmpty(item.visualPartId))
                Data.Avatar.AvatarVisualUtils.ToggleArmorVisibilityByAvatarPartId(dummyRoot, piezas, item.visualPartId, genero);
        }
    }

    void OnCreateHero()
    {
        SceneTransitionService.LoadScene(SceneTransitionService.SceneNames.AvatarCreator);
    }

    void LoadHeroButtons()
    {
        var heroes = PlayerSessionService.CurrentPlayer.heroes;

        foreach (Transform child in heroButtonContainer)
            Destroy(child.gameObject);

        foreach (var hero in heroes)
        {
            var buttonGO = Instantiate(heroButtonPrefab, heroButtonContainer);

            // Asignar icono de clase
            var iconTransform = buttonGO.transform.Find("Icon");
            var icon = iconTransform ? iconTransform.GetComponent<Image>() : null;
            if (icon != null)
            {
                // Obtener el ScriptableObject de la clase por id
                var classDef = Resources.Load<HeroClassDefinition>($"Data/HeroClasses/{hero.classId}");
                if (classDef != null)
                    icon.sprite = classDef.icon;
                else
                    Debug.LogWarning($"No se encontró HeroClassDefinition para classId '{hero.classId}'");
            }

            // Asignar nombre
            var nameTransform = buttonGO.transform.Find("Name");
            var nameLabel = nameTransform ? nameTransform.GetComponent<TMP_Text>() : null;
            if (nameLabel != null)
                nameLabel.text = hero.heroName;

            // Asignar nivel
            var levelTransform = buttonGO.transform.Find("Level");
            var levelLabel = levelTransform ? levelTransform.GetComponent<TMP_Text>() : null;
            if (levelLabel != null)
                levelLabel.text = $"Lv {hero.level}";

            // Asignar callback
            var btn = buttonGO.GetComponent<Button>();
            btn.onClick.AddListener(() => OnSelectHero(hero));
        }

        if (heroes.Count > 0)
            OnSelectHero(heroes[0]);
        else
            dummyRoot.gameObject.SetActive(false);
    }

    void OnSelectHero(HeroData hero)
    {
        selectedHero = hero;

        // Activar el dummy existente en la escena
        if (dummyRoot == null || dummyRoot.childCount == 0)
        {
            Debug.LogWarning("No hay dummy en la escena para mostrar el héroe.");
            return;
        }
        var dummy = dummyRoot.gameObject;
        dummy.SetActive(true);

        // Obtener bases de datos necesarias
        var avatarPartDatabase = Resources.Load<Data.Avatar.AvatarPartDatabase>("Data/Avatar/AvatarPartDatabase");
        if (avatarPartDatabase == null)
        {
            Debug.LogError("No se pudo cargar AvatarPartDatabase.");
            return;
        }
        // 1) Resetear dummy a partes base visuales (avatar)
        Data.Avatar.AvatarVisualUtils.ResetModularDummyToBase(
            dummy.transform,
            avatarPartDatabase,
            basePartIds,
            hero.gender == "Male" ? Gender.Male : Gender.Female
        );

        // 2) Asignar partes base visuales
        var avatarParts = hero.avatar;
        var baseVisualPartIds = new List<string>();
        if (!string.IsNullOrEmpty(avatarParts.headId)) baseVisualPartIds.Add(avatarParts.headId);
        if (!string.IsNullOrEmpty(avatarParts.hairId)) baseVisualPartIds.Add(avatarParts.hairId);
        if (!string.IsNullOrEmpty(avatarParts.beardId)) baseVisualPartIds.Add(avatarParts.beardId);
        if (!string.IsNullOrEmpty(avatarParts.eyebrowId)) baseVisualPartIds.Add(avatarParts.eyebrowId);
        // Puedes agregar más partes si tu sistema lo requiere

        Data.Avatar.AvatarVisualUtils.ResetModularDummyToBase(
            dummy.transform,
            avatarPartDatabase,
            baseVisualPartIds,
            hero.gender == "Male" ? Gender.Male : Gender.Female
        );

        // 2) Activar piezas de equipo funcionales
        var equipment = hero.GetEquipment();

        foreach (var itemId in equipment)
        {
            if (!string.IsNullOrEmpty(itemId))
            {
                ItemDataSO itemData = ItemService.GetItemById(itemId);
                if (itemData != null && !string.IsNullOrEmpty(itemData.visualPartId))
                {
                    Data.Avatar.AvatarVisualUtils.ToggleArmorVisibilityByAvatarPartId(
                        dummy.transform,
                        avatarPartDatabase,
                        itemData.visualPartId,
                        hero.gender == "Male" ? Gender.Male : Gender.Female
                    );
                }
                else
                {
                    Debug.LogWarning($"No se encontró ItemData o visualPartId para el itemId '{itemId}'");
                }
            }
        }
    }

    void OnConfirm()
    {
        PlayerSessionService.SetSelectedHero(selectedHero);
        SceneTransitionService.LoadScene(SceneTransitionService.SceneNames.Feudo);
    }
    
    void OnDeleteHero()
    {
        if (selectedHero == null)
        {
            Debug.LogWarning("No hay héroe seleccionado para eliminar.");
            return;
        }

        heroPendingDeletion = selectedHero;
    }

    HeroData heroPendingDeletion;

    void OnDisable() => heroPendingDeletion = null;

    void OnGUI()
    {
        if (heroPendingDeletion == null) return;
        var rect = new Rect((Screen.width - 420f) / 2f, (Screen.height - 170f) / 2f, 420f, 170f);
        GUI.ModalWindow(73101, rect, _ =>
        {
            GUILayout.Label($"Delete hero '{heroPendingDeletion.heroName}'? This action cannot be undone.");
            if (GUILayout.Button("Cancel")) heroPendingDeletion = null;
            if (GUILayout.Button("Delete")) ConfirmHeroDeletion();
        }, "Delete Hero");
    }

    void ConfirmHeroDeletion()
    {
        var heroToDelete = heroPendingDeletion;
        heroPendingDeletion = null;
        if (heroToDelete == null) return;
        var player = PlayerSessionService.CurrentPlayer;
        if (player == null)
        {
            Debug.LogError("No hay sesión de jugador activa.");
            return;
        }

        if (player.heroes.Remove(heroToDelete))
        {
            SaveSystem.SavePlayer(player);
            LoadHeroButtons();
            if (selectedHero == heroToDelete) selectedHero = null;
            Debug.Log($"Héroe '{heroToDelete.heroName}' eliminado correctamente.");
        }
        else
        {
            Debug.LogWarning($"No se pudo eliminar el héroe '{heroToDelete.heroName}'.");
        }
    }
    void OnExitPressed()
    {
        PlayerSessionService.Clear();
        SceneTransitionService.LoadScene(SceneTransitionService.SceneNames.Login);
    }
}
