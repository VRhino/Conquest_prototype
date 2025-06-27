using Unity.Entities;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Baking component that exposes a <see cref="NavMeshAgent"/> on the GameObject
/// and links it to an entity via <see cref="NavAgentComponent"/>.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class NavMeshAgentAuthoring : MonoBehaviour
{
    class NavMeshAgentBaker : Unity.Entities.Baker<NavMeshAgentAuthoring>
    {
        public override void Bake(NavMeshAgentAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            var agent = authoring.GetComponent<NavMeshAgent>();
            AddComponentObject(entity, agent);
            AddComponent<NavAgentComponent>(entity);
        }
    }
}
