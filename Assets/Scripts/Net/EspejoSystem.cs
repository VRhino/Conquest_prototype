using ConquestTactics.Animation;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine.AI;

namespace ConquestTactics.Net
{
    /// <summary>Servidor: el fantasma que lleva por la red a este soldado o héroe de la batalla de verdad.</summary>
    public struct Espejo : ICleanupComponentData
    {
        public Entity Fantasma;
    }

    /// <summary>
    /// Servidor de la batalla de verdad (<c>-batallaReal</c>): cada soldado y cada héroe de Game.ECS tiene un fantasma
    /// espejo que copia cada tick su posición y lo que su Animator necesita, y eso es lo único que viaja a los clientes.
    /// Así los 99 sistemas del juego no saben nada de Netcode. Cuando el de verdad muere, su espejo se borra.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    [UpdateBefore(typeof(FinTickServidorSystem))]
    public partial struct EspejoSystem : ISystem
    {
        EntityQuery _conEspejo;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PrefabsRed>();
            _conEspejo = SystemAPI.QueryBuilder().WithAll<Espejo, LocalTransform>().Build();
            state.Enabled = OpcionesRed.BatallaReal > 0;
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            var prefabs = SystemAPI.GetSingleton<PrefabsRed>();
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (pose, bando, visual, entidad) in SystemAPI.Query<RefRO<LocalTransform>, RefRO<TeamComponent>, RefRO<UnitVisualReference>>()
                         .WithAll<UnitCombatComponent>().WithNone<Espejo>().WithEntityAccess())
            {
                var modelo = new VisualRed { Prefab = visual.ValueRO.visualPrefabName };
                if (SystemAPI.HasComponent<UnitOwnerComponent>(entidad))
                {
                    var escuadra = SystemAPI.GetComponent<UnitOwnerComponent>(entidad).squad;
                    if (SystemAPI.HasComponent<SquadDefinitionComponent>(escuadra))
                        modelo.TipoDeEscuadra = (byte)SystemAPI.GetComponent<SquadDefinitionComponent>(escuadra).squadType;
                }
                Crear(ref ecb, prefabs.Espejo, entidad, pose.ValueRO, bando.ValueRO.value, SoldadoRed.Soldado, modelo);
            }
            // El héroe espera a tener su aspecto (se lo pone quien lo crea), que viaja con el espejo al aparecer.
            foreach (var (pose, bando, aparicion, entidad) in SystemAPI.Query<RefRO<LocalTransform>, RefRO<TeamComponent>, RefRO<HeroSpawnComponent>>()
                         .WithAll<HeroAppearanceComponent>().WithNone<Espejo>().WithEntityAccess())
            {
                // Como HeroVisualInstantiationSystem para un héroe que no es el local.
                var modelo = new VisualRed { Prefab = aparicion.ValueRO.visualPrefabId.ToString() + "_Remote" };
                var fantasma = Crear(ref ecb, prefabs.EspejoHeroe, entidad, pose.ValueRO, bando.ValueRO.value, SoldadoRed.Heroe, modelo);
                ecb.SetComponent(fantasma, new AparienciaRed { Texto = AparienciaRed.De(em.GetComponentObject<HeroAppearanceComponent>(entidad)) });
            }
            // Al destruirse el de verdad solo le queda este componente: se va su fantasma y luego él.
            foreach (var (espejo, entidad) in SystemAPI.Query<RefRO<Espejo>>().WithNone<LocalTransform>().WithEntityAccess())
            {
                ecb.DestroyEntity(espejo.ValueRO.Fantasma);
                ecb.RemoveComponent<Espejo>(entidad);
            }
            ecb.Playback(em);

            using var espejos = _conEspejo.ToComponentDataArray<Espejo>(Allocator.Temp);
            using var poses = _conEspejo.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            for (int i = 0; i < espejos.Length; i++)
                em.SetComponentData(espejos[i].Fantasma, poses[i]);

            // Lo que lee UnitAnimationAdapter del soldado de verdad, resumido para el Animator del cliente.
            foreach (var (espejo, movimiento, combate, entidad) in SystemAPI.Query<RefRO<Espejo>, RefRO<UnitAnimationMovementComponent>, RefRO<UnitCombatComponent>>()
                         .WithEntityAccess())
            {
                var m = movimiento.ValueRO;
                var animacion = new AnimacionRed
                {
                    Velocidad = m.IsMoving ? m.CurrentSpeed : 0f,
                    VelocidadBase = m.MaxSpeed,
                    VelocidadPedida = m.RequestedSpeed,
                    VelocidadDeAtaque = CombatStatsComponent.AttackSpeedOf(SystemAPI.HasComponent<CombatStatsComponent>(entidad),
                        SystemAPI.HasComponent<CombatStatsComponent>(entidad) ? SystemAPI.GetComponent<CombatStatsComponent>(entidad) : default),
                    Postura = (byte)m.CurrentStance,
                    Fila = (byte)m.SlotRow,
                    Papel = (byte)m.SlotRole,
                };
                if (m.IsMoving) animacion.Banderas |= AnimacionRed.Moviendose;
                if (combate.ValueRO.isAttacking) animacion.Banderas |= AnimacionRed.Atacando;
                if (SystemAPI.HasComponent<RangedAttackStateComponent>(entidad))
                {
                    var distancia = SystemAPI.GetComponent<RangedAttackStateComponent>(entidad);
                    if (distancia.isFiring) animacion.Banderas |= AnimacionRed.Disparando;
                    if (distancia.isReloading) animacion.Banderas |= AnimacionRed.Recargando;
                }
                if (SystemAPI.HasComponent<UnitMovementGearComponent>(entidad))
                {
                    animacion.Banderas |= AnimacionRed.ConMarcha;
                    if (SystemAPI.GetComponent<UnitMovementGearComponent>(entidad).gear == MovementGear.Run)
                        animacion.Banderas |= AnimacionRed.Corre;
                }
                if (SystemAPI.HasComponent<ResilienceComponent>(entidad))
                {
                    var aguante = SystemAPI.GetComponent<ResilienceComponent>(entidad);
                    animacion.Reaccion = (byte)aguante.reaction;
                    animacion.RevisionDeReaccion = aguante.reactionRevision;
                }
                em.SetComponentData(espejo.ValueRO.Fantasma, animacion);
            }

            // Y lo que lee RemoteHeroAnimationDriver del héroe de verdad: su marcha sale de su agente de navegación.
            foreach (var (espejo, estadisticas, entidad) in SystemAPI.Query<RefRO<Espejo>, RefRO<HeroStatsComponent>>()
                         .WithAll<HeroSpawnComponent>().WithEntityAccess())
            {
                var animacion = em.GetComponentData<AnimacionRed>(espejo.ValueRO.Fantasma);
                byte ataques = animacion.Ataques;
                animacion = new AnimacionRed { VelocidadBase = estadisticas.ValueRO.baseSpeed, VelocidadDeAtaque = 1f, Ataques = ataques };
                var agente = em.HasComponent<NavMeshAgent>(entidad) ? em.GetComponentObject<NavMeshAgent>(entidad) : null;
                if (agente != null && agente.enabled && agente.isOnNavMesh)
                {
                    var velocidad = agente.velocity;
                    velocidad.y = 0f;
                    animacion.Velocidad = velocidad.magnitude;
                    animacion.VelocidadPedida = agente.speed;
                    if (animacion.Velocidad > 0.05f) animacion.Banderas |= AnimacionRed.Moviendose;
                }
                if (SystemAPI.HasComponent<HeroAnimationComponent>(entidad))
                {
                    var heroe = SystemAPI.GetComponent<HeroAnimationComponent>(entidad);
                    if (heroe.attackSpeed > 0f) animacion.VelocidadDeAtaque = heroe.attackSpeed;
                    // El disparador dura hasta que el Animator del servidor lo consume en el fotograma siguiente: se ve una vez.
                    if (heroe.triggerAttack) animacion.Ataques++;
                }
                if (SystemAPI.HasComponent<HeroCombatComponent>(entidad) && SystemAPI.GetComponent<HeroCombatComponent>(entidad).isAttacking)
                    animacion.Banderas |= AnimacionRed.Atacando;
                if (SystemAPI.HasComponent<HeroDeathPresentationComponent>(entidad))
                {
                    var muerte = SystemAPI.GetComponent<HeroDeathPresentationComponent>(entidad);
                    if (muerte.isDead) animacion.Banderas |= AnimacionRed.Muerto;
                    if (muerte.visualHidden) animacion.Banderas |= AnimacionRed.Oculto;
                    animacion.RevisionDeMuerte = muerte.deathRevision;
                }
                if (SystemAPI.HasComponent<ResilienceComponent>(entidad))
                {
                    var aguante = SystemAPI.GetComponent<ResilienceComponent>(entidad);
                    animacion.Reaccion = (byte)aguante.reaction;
                    animacion.RevisionDeReaccion = aguante.reactionRevision;
                }
                em.SetComponentData(espejo.ValueRO.Fantasma, animacion);
            }
        }

        static Entity Crear(ref EntityCommandBuffer ecb, Entity prefab, Entity deVerdad, in LocalTransform pose, Team bando, byte tipo, in VisualRed modelo)
        {
            var fantasma = ecb.Instantiate(prefab);
            ecb.SetComponent(fantasma, pose);
            ecb.SetComponent(fantasma, new SoldadoRed { Bando = (byte)(bando == Team.TeamB ? 1 : 0), Tipo = tipo });
            ecb.SetComponent(fantasma, modelo);
            ecb.AddComponent(deVerdad, new Espejo { Fantasma = fantasma });
            return fantasma;
        }
    }
}
