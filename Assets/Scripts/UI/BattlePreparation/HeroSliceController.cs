using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Controlador para elementos HeroSlice en el battle preparation UI.
/// Maneja la visualización de información del héroe y gestión dinámica de Squad_Icons.
/// Escucha eventos para actualizaciones automáticas de squads disponibles.
/// </summary>
public class HeroSliceController : MonoBehaviour
{
    #region UI References
    
    [Header("Hero Information")]
    [SerializeField] public Image playerIcon;
    [SerializeField] public Image heroClassIcon;
    [SerializeField] public TMP_Text heroNameText;
    [SerializeField] public TMP_Text heroLevelText;
    [SerializeField] public Image houseImage;
    
    [Header("Squad Management")]
    [SerializeField] public Transform squadIconContainer;
    [SerializeField] public GameObject squadIconPrefab;
    
    #endregion
    
    #region Private Fields
    private HeroSliceData _heroData;
    private List<SquadIconController> _squadIconControllers;
    
    #endregion
    
    #region Unity Lifecycle
    
    void Awake()
    {
        _squadIconControllers = new List<SquadIconController>();
        
        // Validar referencias UI críticas
        ValidateComponents();
    }
    
    void OnEnable()
    {
        // Suscribirse a eventos de squad updates
        BattlePreparationEvents.SquadsUpdated += OnSquadsUpdated;
    }
    
    void OnDisable()
    {
        // Desuscribirse de eventos
        BattlePreparationEvents.SquadsUpdated -= OnSquadsUpdated;
    }
    
    void OnDestroy()
    {
        // Cleanup completo
        ClearSquadIcons();
        _heroData = null;
    }
    
    #endregion
    
    #region Public API
    
    /// <summary>
    /// Inicializa el controlador con datos del héroe.
    /// Configura toda la información visual y carga los squad icons iniciales.
    /// </summary>
    public void Initialize(HeroSliceData heroData)
    {
        if (heroData == null)
        {
            Debug.LogWarning("[HeroSliceController] HeroSliceData is null");
            ClearHeroInfo();
            return;
        }
        _heroData = heroData;
        SetupHeroInfo();
        // Configurar squad icons
        SetupSquadIcons();
    }
    
    /// <summary>
    /// Limpia toda la información del héroe
    /// </summary>
    public void ClearHeroInfo()
    {
        if (heroNameText != null) heroNameText.text = "";
        if (heroLevelText != null) heroLevelText.text = "";
        if (heroClassIcon != null) heroClassIcon.sprite = null;
        if (playerIcon != null) playerIcon.gameObject.SetActive(false);
        if (houseImage != null) houseImage.gameObject.SetActive(false);
        
        ClearSquadIcons();
    }
    #endregion
    
    /// <summary>
    /// Configura toda la información visual del héroe
    /// </summary>
    private void SetupHeroInfo()
    {
        if (_heroData == null) return;

        if (playerIcon != null) playerIcon.sprite = _heroData.heroIcon;
        if (heroClassIcon != null) heroClassIcon.sprite = _heroData.classIcon;
        if (heroNameText != null) heroNameText.text = _heroData.heroName;
        if (heroLevelText != null) heroLevelText.text = _heroData.heroLevel.ToString();
        if (houseImage != null) houseImage.sprite = _heroData.houseIcon;
    }
    
    
    #region Private Methods - Squad Icons Management
    
    /// <summary>
    /// Configura todos los squad icons basado en la lista interna _heroData.selectedSquads
    /// </summary>
    private void SetupSquadIcons()
    {
        if (_heroData.selectedSquads == null || _heroData.selectedSquads.Count == 0)
        {
            ClearSquadIcons();
            return;
        }
        
        // Limpiar icons existentes
        ClearSquadIcons();
        
        // Crear nuevo icon para cada squad en nuestra lista interna
        foreach (SquadIconData squad in _heroData.selectedSquads)
        {
            CreateSquadIcon(squad);
        }
        
        Debug.Log($"[HeroSliceController] Creados {_squadIconControllers.Count} squad icons para héroe: {_heroData.heroName}");
    }
    
    /// <summary>
    /// Crea un nuevo squad icon para el squadId especificado
    /// </summary>
    /// <param name="squadId">ID del squad para crear el icon</param>
    private void CreateSquadIcon(SquadIconData squadIconData)
    {
        if (squadIconData == null || squadIconContainer == null || squadIconPrefab == null)
        {
            Debug.LogWarning($"[HeroSliceController] No se puede crear squad icon - squadData: {squadIconData?.squadId}, container: {squadIconContainer}, prefab: {squadIconPrefab}");
            return;
        }
        
        // Instanciar prefab
        GameObject squadIconGO = Instantiate(squadIconPrefab, squadIconContainer);
        SquadIconController iconController = squadIconGO.GetComponent<SquadIconController>();
        
        if (iconController == null)
        {
            Debug.LogError($"[HeroSliceController] Squad icon prefab no tiene SquadIconController component");
            Destroy(squadIconGO);
            return;
        }
        
        // Inicializar y agregar a la lista
        iconController.Initialize(squadIconData);
        _squadIconControllers.Add(iconController);

        Debug.Log($"[HeroSliceController] Squad icon creado para: {squadIconData.squadId}");
    }
    
    /// <summary>
    /// Limpia todos los squad icons existentes
    /// </summary>
    private void ClearSquadIcons()
    {
        foreach (var controller in _squadIconControllers)
        {
            if (controller != null && controller.gameObject != null)
            {
                Destroy(controller.gameObject);
            }
        }
        
        _squadIconControllers.Clear();
    }
    
    #endregion
    
    #region Event Handlers
    
    /// <summary>
    /// Maneja actualizaciones de squads disponibles para héroes
    /// </summary>
    /// <param name="heroId">ID del héroe actualizado</param>
    /// <param name="availableSquads">Lista actualizada de squads disponibles</param>
    private void OnSquadsUpdated(string heroId, List<SquadIconData> selectedSquads)
    {
        // Verificar si este evento es para nuestro héroe
        if (_heroData == null || !IsHeroMatch(heroId))
            return;
        
        Debug.Log($"[HeroSliceController] Recibiendo actualización de squads para héroe: {heroId}");
        
        // Actualizar SOLO nuestra lista interna, NUNCA el HeroData original
        _heroData.selectedSquads = new List<SquadIconData>(selectedSquads);
        
        // Recrear squad icons basado en nuestra lista interna
        SetupSquadIcons();
    }
    
    /// <summary>
    /// Maneja actualizaciones de progreso de squads específicos
    /// </summary>
    /// <param name="heroId">ID del héroe propietario</param>
    /// <param name="squadId">ID del squad actualizado</param>
    private void OnSquadProgressUpdated(string heroId, string squadId)
    {
        // Verificar si este evento es para nuestro héroe
        if (_heroData == null || !IsHeroMatch(heroId))
            return;
        
        Debug.Log($"[HeroSliceController] Recibiendo actualización de progreso - Héroe: {heroId}, Squad: {squadId}");
        
        // Para actualizaciones de progreso, podríamos actualizar solo el squad específico
        // Por simplicidad, recreamos todos por ahora
        SetupSquadIcons();
    }
    
    /// <summary>
    /// Verifica si un heroId corresponde a nuestro héroe actual
    /// </summary>
    /// <param name="heroId">ID del héroe a verificar</param>
    /// <returns>True si es nuestro héroe</returns>
    private bool IsHeroMatch(string heroId)
    {
        // Usar heroName como identificador (ajustar según sistema real)
        return string.Equals(heroId, _heroData.heroName, StringComparison.OrdinalIgnoreCase);
    }
    
    #endregion
    
    #region Validation and Debug
    
    /// <summary>
    /// Valida que todos los componentes UI críticos estén asignados
    /// </summary>
    private void ValidateComponents()
    {
        List<string> missingComponents = new List<string>();
        
        if (heroNameText == null) missingComponents.Add("heroNameText");
        if (heroLevelText == null) missingComponents.Add("heroLevelText");
        if (heroClassIcon == null) missingComponents.Add("heroClassIcon");
        if (squadIconContainer == null) missingComponents.Add("squadIconContainer");
        if (squadIconPrefab == null) missingComponents.Add("squadIconPrefab");
        
        if (missingComponents.Count > 0)
        {
            Debug.LogWarning($"[HeroSliceController] Componentes faltantes: {string.Join(", ", missingComponents)}");
        }
    }
    #endregion
}
/// <summary>
/// Data container para información del héroe en HeroSliceController
/// heroIcon, classIcon, heroName, heroLevel, houseIcon, selectedSquads
/// </summary>
public class HeroSliceData
{
    public Sprite heroIcon;
    public Sprite classIcon;
    public string heroName;
    public int heroLevel;
    public Sprite houseIcon;
    public List<SquadIconData> selectedSquads;

    public HeroSliceData(Sprite heroIcon, Sprite classIcon, string heroName, int heroLevel, Sprite houseIcon, List<SquadIconData> selectedSquads)
    {
        this.heroIcon = heroIcon ?? null;
        this.classIcon = classIcon ?? null;
        this.heroName = heroName;
        this.heroLevel = heroLevel;
        this.houseIcon = houseIcon ?? null;
        this.selectedSquads = selectedSquads ?? new List<SquadIconData>();
    }

}