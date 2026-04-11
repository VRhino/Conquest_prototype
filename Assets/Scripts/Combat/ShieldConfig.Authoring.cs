using Unity.Entities;
using UnityEngine;

public class ShieldConfigAuthoring : MonoBehaviour
{
    public float defaultMaxBlock          = 100f;
    public float defaultRegenRate         = 5f;
    public float defaultBreakStunDuration = 3f;
    public float forwardBlockDotThreshold = 0.5f;

    class Baker : Baker<ShieldConfigAuthoring>
    {
        public override void Bake(ShieldConfigAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new ShieldConfigComponent
            {
                defaultMaxBlock          = authoring.defaultMaxBlock,
                defaultRegenRate         = authoring.defaultRegenRate,
                defaultBreakStunDuration = authoring.defaultBreakStunDuration,
                forwardBlockDotThreshold = authoring.forwardBlockDotThreshold
            });
        }
    }
}
