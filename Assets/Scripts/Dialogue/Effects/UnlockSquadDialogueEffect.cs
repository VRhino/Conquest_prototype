using UnityEngine;
using ConquestTactics.Dialogue;

namespace ConquestTactics.Dialogue
{
    /// <summary>
    /// Efecto de diálogo que desbloquea un nuevo tipo de escuadrón.
    /// </summary>
    [CreateAssetMenu(fileName = "UnlockSquadDialogueEffect", menuName = "Dialogue/Effects/Unlock Squad", order = 3)]
    public class UnlockSquadDialogueEffect : DialogueEffect
{
    [Header("Squad Settings")]
    [SerializeField] private string squadId;
    [SerializeField] private bool showNotification = true;
    [SerializeField] private string customMessage;

    public override bool Execute(HeroData hero, string npcId = null, DialogueParameters parameters = null)
    {
        if (!CanExecute(hero, npcId))
        {
            return false;
        }

        string targetSquadId = parameters?.stringParameter ?? squadId;

        if (string.IsNullOrEmpty(targetSquadId))
        {
            Debug.LogError($"[UnlockSquadDialogueEffect] No squad ID specified");
            return false;
        }

        // Verificar si ya está desbloqueado
        if (hero.availableSquads.Contains(targetSquadId))
        {
            Debug.LogWarning($"[UnlockSquadDialogueEffect] Squad {targetSquadId} already unlocked");
            return false;
        }

        // Desbloquear el escuadrón
        hero.availableSquads.Add(targetSquadId);

        if (showNotification)
        {
            string message = !string.IsNullOrEmpty(customMessage) 
                ? customMessage 
                : $"Unlocked new squad: {targetSquadId}";
            
            Debug.Log($"[Dialogue] {message}");
        }
        
        FullscreenPanelManager.Instance.ClosePanel<NpcDialogueUIController>();
        OnEffectExecuted(hero, npcId);
        return true;
    }

    public override string GetPreviewText()
    {
        return $"Unlock squad: {squadId}";
    }

    public override bool CanExecute(HeroData hero, string npcId = null)
    {
        if (!base.CanExecute(hero, npcId))
        {
            return false;
        }

        string targetSquadId = squadId;
        
        // No ejecutar si ya está desbloqueado
        return !hero.availableSquads.Contains(targetSquadId);
    }

    public override int GetExecutionPriority()
    {
        return 10; // Alta prioridad para desbloqueos
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        
        if (string.IsNullOrEmpty(squadId))
        {
            Debug.LogWarning($"[UnlockSquadDialogueEffect] No squad ID specified in {name}");
        }
    }
}
}
