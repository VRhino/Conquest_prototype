using UnityEngine;
using ConquestTactics.Dialogue;

namespace ConquestTactics.Dialogue
{
    /// <summary>
    /// Efecto de diálogo que otorga experiencia al héroe.
    /// </summary>
    [CreateAssetMenu(fileName = "GiveExperienceDialogueEffect", menuName = "Dialogue/Effects/Give Experience", order = 2)]
    public class GiveExperienceDialogueEffect : DialogueEffect
{
    [Header("Experience Settings")]
    [SerializeField] private int experienceAmount = 100;
    [SerializeField] private bool showNotification = true;
    [SerializeField] private string customMessage;

    public override bool Execute(HeroData hero, string npcId = null, DialogueParameters parameters = null)
    {
        if (!CanExecute(hero, npcId))
        {
            return false;
        }

        int targetExperience = parameters?.intParameter ?? experienceAmount;

        if (targetExperience <= 0)
        {
            Debug.LogWarning($"[GiveExperienceDialogueEffect] Invalid experience amount: {targetExperience}");
            return false;
        }

        // Agregar experiencia al héroe
        hero.currentXP += targetExperience;

        if (showNotification)
        {
            string message = !string.IsNullOrEmpty(customMessage) 
                ? customMessage 
                : $"Gained {targetExperience} experience";
            
            Debug.Log($"[Dialogue] {message}");
        }

        OnEffectExecuted(hero, npcId);
        return true;
    }

    public override string GetPreviewText()
    {
        return $"Give {experienceAmount} experience";
    }

    public override int GetExecutionPriority()
    {
        return 5; // Mayor prioridad que items/monedas
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        
        if (experienceAmount < 0)
        {
            experienceAmount = 0;
        }
    }
}
}
