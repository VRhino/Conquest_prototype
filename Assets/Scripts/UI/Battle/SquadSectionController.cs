using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Main controller for the Squad Section of the battle HUD.
/// Displays squad image, name, unit count, per-unit health bars,
/// and formation icons with active formation highlighting.
/// </summary>
public class SquadSectionController : MonoBehaviour
{
    [Header("Squad Image Section")]
    [SerializeField] private Image _unitImage;
    [SerializeField] private TMP_Text _unitCountText;

    [Header("Squad Name")]
    [SerializeField] private TMP_Text _squadNameText;

    [Header("Unit Health Section")]
    [SerializeField] private Transform _unitHealthContainer;
    [SerializeField] private GameObject _unitHealthBarPrefab;

    [Header("Formations Section")]
    [SerializeField] private Transform _formationsContainer;
    [SerializeField] private GameObject _formationIconPrefab;

    private SquadData _squadData;
    private List<UnitHealthBarController> _healthBars = new();
    private List<HUDFormationIconController> _formationIcons = new();
    private bool _initialized;

    /// <summary>
    /// Call once when squad data is known to set up static UI and formation icons.
    /// </summary>
    public void Initialize(SquadData squadData)
    {
        _squadData = squadData;
        _initialized = true;

        if (_unitImage != null && squadData.unitImage != null)
            _unitImage.sprite = squadData.unitImage;
        if (_squadNameText != null)
            _squadNameText.text = squadData.squadName;

        // Spawn formation icons
        ClearContainer(_formationsContainer);
        _formationIcons.Clear();
        for (int i = 0; i < squadData.gridFormations.Length; i++)
        {
            var go = Instantiate(_formationIconPrefab, _formationsContainer);
            var ctrl = go.GetComponent<HUDFormationIconController>();
            if (ctrl != null)
            {
                ctrl.Initialize(squadData.gridFormations[i], i);
                _formationIcons.Add(ctrl);
            }
        }
    }

    /// <summary>
    /// Called every frame from HUDController to refresh dynamic ECS data.
    /// </summary>
    public void UpdateFromECS(EntityManager em)
    {
        if (!_initialized) return;

        // --- Squad status (alive/total) ---
        var squadQuery = em.CreateEntityQuery(
            ComponentType.ReadOnly<SquadStatusComponent>(),
            ComponentType.ReadOnly<IsLocalSquadActive>());
        if (squadQuery.IsEmptyIgnoreFilter) return;

        Entity squadEntity = squadQuery.GetSingletonEntity();
        var status = em.GetComponentData<SquadStatusComponent>(squadEntity);

        if (_unitCountText != null)
            _unitCountText.text = $"{status.aliveUnits}/{status.totalUnits}";

        // --- Active formation highlight ---
        if (em.HasComponent<SquadStateComponent>(squadEntity))
        {
            var state = em.GetComponentData<SquadStateComponent>(squadEntity);
            foreach (var icon in _formationIcons)
                icon.SetActive(icon.FormationType == state.currentFormation);
        }

        // --- Per-unit health bars ---
        if (em.HasBuffer<SquadUnitElement>(squadEntity))
        {
            var units = em.GetBuffer<SquadUnitElement>(squadEntity);
            EnsureHealthBars(units.Length);

            for (int i = 0; i < units.Length; i++)
            {
                var unitEntity = units[i].Value;
                if (i < _healthBars.Count && em.Exists(unitEntity)
                    && em.HasComponent<HealthComponent>(unitEntity))
                {
                    var hp = em.GetComponentData<HealthComponent>(unitEntity);
                    float pct = hp.maxHealth > 0f ? hp.currentHealth / hp.maxHealth : 0f;
                    _healthBars[i].gameObject.SetActive(true);
                    _healthBars[i].SetHealthPercent(pct);
                }
                else if (i < _healthBars.Count)
                {
                    _healthBars[i].gameObject.SetActive(false);
                }
            }
            // Hide excess bars
            for (int i = units.Length; i < _healthBars.Count; i++)
                _healthBars[i].gameObject.SetActive(false);
        }
    }

    private void EnsureHealthBars(int count)
    {
        if (_unitHealthBarPrefab == null || _unitHealthContainer == null) return;

        while (_healthBars.Count < count)
        {
            var go = Instantiate(_unitHealthBarPrefab, _unitHealthContainer);
            var ctrl = go.GetComponent<UnitHealthBarController>();
            if (ctrl != null)
            {
                _healthBars.Add(ctrl);
            }
            else
            {
                Debug.LogWarning("[SquadSectionController] UnitHealthBarController component missing on prefab. Aborting health bar creation.");
                Destroy(go);
                break;
            }
        }
    }

    private void ClearContainer(Transform container)
    {
        if (container == null) return;
        foreach (Transform child in container)
            Destroy(child.gameObject);
    }
}
