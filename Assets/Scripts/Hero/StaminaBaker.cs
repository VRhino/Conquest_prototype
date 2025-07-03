using Unity.Entities;

/// <summary>
/// Baker for StaminaAuthoring. Adds StaminaComponent to the entity during baking.
/// </summary>
public class StaminaBaker : Baker<StaminaAuthoring>
{
    public override void Bake(StaminaAuthoring authoring)
    {
        var entity = GetEntity(TransformUsageFlags.Dynamic);
        AddComponent(entity, new StaminaComponent
        {
            currentStamina = authoring.currentStamina,
            maxStamina = authoring.maxStamina,
            regenRate = authoring.regenRate,
            isExhausted = authoring.startExhausted
        });
    }
}
