using UnityEngine;

/// <summary>
/// Authoring MonoBehaviour to configure StaminaComponent for DOTS baking.
/// </summary>
public class StaminaAuthoring : MonoBehaviour
{
    [Header("Stamina Configuration")]
    public float maxStamina = 100f;
    public float regenRate = 20f;
    public bool startExhausted = false;
    
    [Header("Initial Values")]
    public float currentStamina = 100f;
}
