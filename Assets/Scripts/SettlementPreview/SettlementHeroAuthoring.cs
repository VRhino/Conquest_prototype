using Unity.Entities;
using UnityEngine;

namespace Conquest.SettlementPreview
{
    // Separate reference from HeroPrefabComponent: the battle spawner must not auto-spawn here.
    public struct SettlementHeroPrefab : IComponentData { public Entity Value; }
    public struct SettlementWalkingHero : IComponentData { }
    public sealed class SettlementHeroAuthoring : MonoBehaviour
    {
        public GameObject heroPrefab;
        private sealed class Baker : Baker<SettlementHeroAuthoring>
        {
            public override void Bake(SettlementHeroAuthoring authoring)
            {
                if (!authoring.heroPrefab) return;
                AddComponent(GetEntity(TransformUsageFlags.None), new SettlementHeroPrefab
                { Value = GetEntity(authoring.heroPrefab, TransformUsageFlags.Dynamic) });
            }
        }
    }
}
