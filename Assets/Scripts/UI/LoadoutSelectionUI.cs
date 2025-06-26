using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI controller used during the Preparation phase to select one of the
/// previously saved loadouts.
/// </summary>
public class LoadoutSelectionUI : MonoBehaviour
{
    [System.Serializable]
    public class LoadoutItem
    {
        public Text title;
        public Text squads;
        public Text perks;
        public Text leadership;
        public Button selectButton;

        public void SetData(LocalSaveSystem.LoadoutData data)
        {
            if (title != null)
                title.text = $"Loadout {data.loadoutID + 1}";
            if (squads != null)
                squads.text = string.Join(", ", data.squadIDs);
            if (perks != null)
                perks.text = string.Join(", ", data.perkIDs);
            if (leadership != null)
                leadership.text = data.leadershipUsed.ToString();
        }
    }

    [SerializeField] List<LoadoutItem> _loadouts = new();
    [SerializeField] Button _confirmButton;

    LocalSaveSystem.PlayerProgressData _progress;
    int _selectedIndex = -1;

    void Start()
    {
        _progress = LocalSaveSystem.LoadGame();

        for (int i = 0; i < _loadouts.Count; i++)
        {
            if (i < _progress.loadouts.Count)
            {
                var ld = _progress.loadouts[i];
                _loadouts[i].SetData(ld);
                int idx = i;
                if (_loadouts[i].selectButton != null)
                    _loadouts[i].selectButton.onClick.AddListener(() => SelectLoadout(idx));
            }
            else
            {
                if (_loadouts[i].selectButton != null)
                    _loadouts[i].selectButton.interactable = false;
            }
        }

        if (_confirmButton != null)
            _confirmButton.onClick.AddListener(ConfirmSelection);
    }

    /// <summary>Selects the loadout at the given index.</summary>
    /// <param name="index">Index of the loadout to activate.</param>
    public void SelectLoadout(int index)
    {
        _selectedIndex = index;
    }

    /// <summary>Copies the selected loadout into the DataContainer.</summary>
    void ConfirmSelection()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _progress.loadouts.Count)
            return;

        var em = World.DefaultGameObjectInjectionWorld.EntityManager;
        if (!SystemAPI.TryGetSingletonEntity<DataContainerComponent>(out var entity))
            return;

        var container = em.GetComponentData<DataContainerComponent>(entity);
        var data = _progress.loadouts[_selectedIndex];

        container.selectedLoadoutID = data.loadoutID;
        container.selectedSquads.Clear();
        foreach (int id in data.squadIDs)
            container.selectedSquads.Add(id);

        container.selectedPerks.Clear();
        foreach (int id in data.perkIDs)
            container.selectedPerks.Add(id);

        container.totalLeadershipUsed = data.leadershipUsed;
        container.isReady = true;

        em.SetComponentData(entity, container);
    }
}

