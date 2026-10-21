using Unity.Collections;
using Unity.Entities;

/// <summary>
/// Shows the floating combat text of <see cref="DamageFeedbackEvent"/>s and destroys them (L1-03): the visual side of
/// damage, kept out of the logic so <see cref="DamageCalculationSystem"/> could run without UI (server).
/// </summary>
[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial class DamageFeedbackPresentationSystem : SystemBase
{
    /// <summary>Text appears this far above the target's origin, m.</summary>
    const float TextHeightOffset = 1.8f;

    protected override void OnUpdate()
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        var manager = FloatingCombatTextManager.Instance;
        foreach (var (evt, entity) in SystemAPI.Query<RefRO<DamageFeedbackEvent>>().WithEntityAccess())
        {
            var e = evt.ValueRO;
            if (manager != null)
                manager.Spawn(new UnityEngine.Vector3(e.position.x, e.position.y + TextHeightOffset, e.position.z),
                    e.category, e.amount);
            ecb.DestroyEntity(entity);
        }
        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
}
