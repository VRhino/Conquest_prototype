using Unity.Entities;
using Unity.NetCode;
using Unity.Transforms;

namespace ConquestTactics.Net
{
    /// <summary>
    /// Crea por código los prefabs fantasma (héroe, soldado y espejo de la batalla de verdad), del mismo modo en servidor y cliente, que es lo
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

            var espejo = em.CreateEntity();
            em.AddComponentData(espejo, LocalTransform.Identity);
            em.AddComponent<SoldadoRed>(espejo);
            em.AddComponent<VisualRed>(espejo);
            em.AddComponent<AnimacionRed>(espejo);
            em.AddComponent<DestinoRed>(espejo);
            GhostPrefabCreation.ConvertToGhostPrefab(em, espejo, new GhostPrefabCreation.Config
            {
                Name = "ConquestEspejo",
                Importance = 1,
                MaxSendRate = (byte)OpcionesRed.TasaSoldados,
                SupportedGhostModes = GhostModeMask.Interpolated,
                DefaultGhostMode = GhostMode.Interpolated,
                OptimizationMode = GhostOptimizationMode.Dynamic,
            });

            var espejoHeroe = em.CreateEntity();
            em.AddComponentData(espejoHeroe, LocalTransform.Identity);
            em.AddComponent<SoldadoRed>(espejoHeroe);
            em.AddComponent<VisualRed>(espejoHeroe);
            em.AddComponent<AnimacionRed>(espejoHeroe);
            em.AddComponent<AparienciaRed>(espejoHeroe);
            GhostPrefabCreation.ConvertToGhostPrefab(em, espejoHeroe, new GhostPrefabCreation.Config
            {
                Name = "ConquestEspejoHeroe",
                Importance = 10,
                SupportedGhostModes = GhostModeMask.Interpolated,
                DefaultGhostMode = GhostMode.Interpolated,
                OptimizationMode = GhostOptimizationMode.Dynamic,
            });

            var mando = em.CreateEntity();
            em.AddComponentData(mando, LocalTransform.Identity);
            em.AddComponent<MandoMovil>(mando);
            em.AddComponent<InterfazRed>(mando);
            em.AddBuffer<HabilidadRed>(mando);
            em.AddComponent<GhostOwner>(mando);
            em.AddComponent<GhostOwnerIsLocal>(mando);
            em.AddComponent<EntradaJugador>(mando);
            em.AddComponent<InputBufferData<EntradaJugador>>(mando);
            em.AddComponentData(mando, new AutoCommandTarget { Enabled = true });
            // En el cliente, una corrección del servidor de entre 25 cm y 5 m no se aplica de golpe sino a 25 cm por
            // snapshot (15 m/s a 60 Hz): más deprisa de lo que corre el héroe, para que el error no crezca cuando empuja
            // contra algo que solo ve el servidor, y sin el salto de metros que daba con retardo.
            em.AddComponentData(mando, new DefaultSmoothingActionUserParams { maxDist = 5f, delta = 0.25f });
            GhostPrefabCreation.ConvertToGhostPrefab(em, mando, new GhostPrefabCreation.Config
            {
                Name = "ConquestMando",
                Importance = 100,
                SupportedGhostModes = GhostModeMask.All,
                DefaultGhostMode = GhostMode.OwnerPredicted,
                OptimizationMode = GhostOptimizationMode.Dynamic,
            });

            var partida = em.CreateEntity();
            em.AddComponent<PartidaRed>(partida);
            em.AddBuffer<ZonaRed>(partida);
            GhostPrefabCreation.ConvertToGhostPrefab(em, partida, new GhostPrefabCreation.Config
            {
                Name = "ConquestPartida",
                Importance = 100,
                SupportedGhostModes = GhostModeMask.Interpolated,
                DefaultGhostMode = GhostMode.Interpolated,
                OptimizationMode = GhostOptimizationMode.Dynamic,
            });

            em.CreateSingleton(new PrefabsRed { Heroe = heroe, Soldado = soldado, Espejo = espejo, EspejoHeroe = espejoHeroe, Mando = mando, Partida = partida });
            if (state.WorldUnmanaged.IsClient())
                SystemAPI.GetSingletonRW<GhostPredictionSmoothing>().ValueRW
                    .RegisterSmoothingAction<LocalTransform, DefaultSmoothingActionUserParams>(em, DefaultTranslationSmoothingAction.Action);
            state.Enabled = false;
        }
    }
}
