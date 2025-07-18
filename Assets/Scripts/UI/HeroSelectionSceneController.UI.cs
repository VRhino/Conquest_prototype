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

    [SerializeField] private List<string> basePartIds = new();

    void Start()
    {
        LoadHeroButtons();
        confirmButton.onClick.AddListener(OnConfirm);
        createHeroButton.onClick.AddListener(OnCreateHero);
    }
    void OnCreateHero()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene("AvatarCreator");
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
        var equipment = hero.equipment;
        var equipmentIds = new List<string> {
            equipment.weaponId,
            equipment.helmetId,
            equipment.torsoId,
            equipment.glovesId,
            equipment.pantsId
        };
        ItemDatabase itemDB = Resources.Load<ItemDatabase>("Data/Items/ItemDatabase");
        foreach (var itemId in equipmentIds)
        {
            if (!string.IsNullOrEmpty(itemId))
            {
                ItemData itemData = itemDB.GetItemDataById(itemId);
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
        UnityEngine.SceneManagement.SceneManager.LoadScene("FeudoScene");
    }
}
