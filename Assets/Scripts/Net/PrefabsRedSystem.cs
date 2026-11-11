using Unity.Entities;
using Unity.NetCode;
using Unity.Transforms;

namespace ConquestTactics.Net
{
    /// <summary>
    /// Crea por código los dos prefabs fantasma (héroe y soldado), del mismo modo en servidor y cliente, que es lo
    /// que exige Netcode para que casen. Sin subescena: la prueba no depende de ningún asset.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [CreateAfter(typeof(DefaultVariantSystemGroup))]
    public partial struct PrefabsRedSystem : ISystem
    {
        const float VelocidadHeroe = 6f;

        public void OnCreate(ref SystemState state)
        {
            var em = state.EntityManager;

            var heroe = em.CreateEntity();
            em.AddComponentData(heroe, LocalTransform.Identity);
            em.AddComponent<GhostOwner>(heroe);
            // El baker de GhostAuthoring lo pone solo; en un prefab hecho por código hay que ponerlo a mano.
            em.AddComponent<GhostOwnerIsLocal>(heroe);
            em.AddComponent<EntradaHeroe>(heroe);
            em.AddComponent<InputBufferData<EntradaHeroe>>(heroe);
            em.AddComponentData(heroe, new AutoCommandTarget { Enabled = true });
            em.AddComponentData(heroe, new HeroeRed { Velocidad = VelocidadHeroe });
            GhostPrefabCreation.ConvertToGhostPrefab(em, heroe, new GhostPrefabCreation.Config
            {
                Name = "ConquestHeroe",
                Importance = 100,
                SupportedGhostModes = GhostModeMask.All,
                DefaultGhostMode = GhostMode.OwnerPredicted,
                OptimizationMode = GhostOptimizationMode.Dynamic,
            });

            var soldado = em.CreateEntity();
            em.AddComponentData(soldado, LocalTransform.Identity);
            em.AddComponent<SoldadoRed>(soldado);
            GhostPrefabCreation.ConvertToGhostPrefab(em, soldado, new GhostPrefabCreation.Config
            {
                Name = "ConquestSoldado",
                Importance = 1,
                SupportedGhostModes = GhostModeMask.Interpolated,
                DefaultGhostMode = GhostMode.Interpolated,
                OptimizationMode = GhostOptimizationMode.Dynamic,
            });

            em.CreateSingleton(new PrefabsRed { Heroe = heroe, Soldado = soldado });
            state.Enabled = false;
        }
    }
}
