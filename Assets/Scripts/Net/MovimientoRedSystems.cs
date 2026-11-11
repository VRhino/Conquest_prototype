using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

namespace ConquestTactics.Net
{
    /// <summary>Huecos detrás del héroe en una rejilla, para la medición sin ticket (sin tropa).</summary>
    public static class FormacionBasica
    {
        const int Columnas = 5;
        const float Separacion = 1.6f;
        const float DistanciaAlHeroe = 2.5f;

        public static float3 Hueco(int indice, quaternion orientacionHeroe)
        {
            int fila = indice / Columnas;
            int columna = indice % Columnas;
            var local = new float3((columna - (Columnas - 1) * 0.5f) * Separacion, 0f, -(DistanciaAlHeroe + fila * Separacion));
            return math.mul(orientacionHeroe, local);
        }
    }

    /// <summary>
    /// Mueve al héroe con su entrada. Corre en el servidor (autoridad) y en el cliente dueño (predicción, con
    /// rebobinado cuando llega el snapshot). Sustituye a LocalHeroCharacterMotor en la red.
    /// ponytail: suelo plano y sin choques; el juego necesitará un controlador de personaje de Unity Physics.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [BurstCompile]
    public partial struct MovimientoHeroeSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;
            foreach (var (entrada, heroe, pose) in SystemAPI.Query<RefRO<EntradaHeroe>, RefRO<HeroeRed>, RefRW<LocalTransform>>()
                         .WithAll<Simulate>().WithNone<HeroeSinJugador>())
            {
                float2 mover = entrada.ValueRO.Mover;
                float largo = math.length(mover);
                if (largo < 0.01f)
                    continue;
                if (largo > 1f)
                    mover /= largo;
                var paso = new float3(mover.x, 0f, mover.y) * heroe.ValueRO.Velocidad * dt;
                pose.ValueRW.Position += paso;
                pose.ValueRW.Rotation = quaternion.LookRotationSafe(math.normalize(paso), math.up());
            }
        }
    }

    /// <summary>
    /// Servidor: los héroes sin jugador. En una batalla de ticket van a por el enemigo más cercano; si no, dan vueltas
    /// (la medición), cada uno con su fase.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [BurstCompile]
    public partial struct HeroesSinJugadorSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float t = (float)SystemAPI.Time.ElapsedTime;
            float dt = SystemAPI.Time.DeltaTime;
            foreach (var (bot, heroe, pose) in SystemAPI.Query<RefRO<HeroeSinJugador>, RefRO<HeroeRed>, RefRW<LocalTransform>>()
                         .WithNone<ObjetivoCombate>())
            {
                float angulo = t * 0.3f + bot.ValueRO.Fase;
                var direccion = new float3(-math.sin(angulo), 0f, math.cos(angulo));
                pose.ValueRW.Position += direccion * heroe.ValueRO.Velocidad * 0.6f * dt;
                pose.ValueRW.Rotation = quaternion.LookRotationSafe(direccion, math.up());
            }
            foreach (var (objetivo, heroe, pose) in SystemAPI.Query<RefRO<ObjetivoCombate>, RefRO<HeroeRed>, RefRW<LocalTransform>>()
                         .WithAll<HeroeSinJugador>())
                Acercarse.Hacia(ref pose.ValueRW, objetivo.ValueRO, heroe.ValueRO.Velocidad * dt);
        }
    }

    public static class Acercarse
    {
        /// <summary>Avanza hacia el enemigo y se para al alcance del golpe.</summary>
        public static void Hacia(ref LocalTransform pose, in ObjetivoCombate objetivo, float paso)
        {
            if (objetivo.Enemigo == Entity.Null)
                return;
            float3 falta = objetivo.Posicion - pose.Position;
            falta.y = 0f;
            float distancia = math.length(falta);
            if (distancia < 1e-3f)
                return;
            var direccion = falta / distancia;
            pose.Rotation = quaternion.LookRotationSafe(direccion, math.up());
            float sobra = distancia - CombateSystem.Alcance * 0.75f;
            if (sobra > 0f)
                pose.Position += direccion * math.min(sobra, paso);
        }
    }

    /// <summary>
    /// Servidor: cada soldado va a su hueco detrás de su héroe, salvo que tenga un enemigo cerca (o se haya quedado sin
    /// héroe): entonces carga contra él. El cliente no simula soldados: los recibe interpolados.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(PredictedSimulationSystemGroup))]
    [UpdateAfter(typeof(HeroesSinJugadorSystem))]
    [BurstCompile]
    public partial struct SeguirHeroeSystem : ISystem
    {
        const float VelocidadSoldado = 7f;
        const float Llegada = 0.05f;
        const float Carga = 10f;

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;
            var poses = SystemAPI.GetComponentLookup<LocalTransform>(true);
            var objetivos = SystemAPI.GetComponentLookup<ObjetivoCombate>(true);
            foreach (var (soldado, pose, entidad) in SystemAPI.Query<RefRO<SoldadoEnServidor>, RefRW<LocalTransform>>().WithEntityAccess())
            {
                bool tieneHeroe = poses.TryGetComponent(soldado.ValueRO.Heroe, out var heroe);
                if (objetivos.TryGetComponent(entidad, out var objetivo) && objetivo.Enemigo != Entity.Null
                    && (!tieneHeroe || objetivo.Distancia < Carga))
                {
                    Acercarse.Hacia(ref pose.ValueRW, objetivo, VelocidadSoldado * dt);
                    continue;
                }
                if (!tieneHeroe)
                    continue;
                float3 destino = heroe.Position + math.mul(heroe.Rotation, soldado.ValueRO.Desfase);
                float3 falta = destino - pose.ValueRO.Position;
                float distancia = math.length(falta);
                if (distancia < Llegada)
                    continue;
                float avance = math.min(distancia, VelocidadSoldado * dt);
                var direccion = falta / distancia;
                pose.ValueRW.Position += direccion * avance;
                pose.ValueRW.Rotation = quaternion.LookRotationSafe(direccion, math.up());
            }
        }
    }
}
