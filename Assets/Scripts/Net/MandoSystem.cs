using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;

namespace ConquestTactics.Net
{
    /// <summary>Servidor: este héroe de la batalla de verdad lo lleva un jugador; su entrada llega en la entidad de mando.</summary>
    public struct HeroeDeJugador : IComponentData
    {
        public Entity Mando;
        public int Jugador;
        /// <summary>Órdenes a la escuadra y habilidades que han llegado de su jugador (para el registro).</summary>
        public int Ordenes;
        public int Habilidades;
    }

    /// <summary>Servidor: este mando ya tiene héroe.</summary>
    public struct MandoConHeroe : IComponentData { }

    /// <summary>
    /// Servidor de la batalla de verdad: da a cada jugador que entra un héroe de la batalla y pone su entrada
    /// (<see cref="EntradaJugador"/>) donde la IA de ese héroe pondría su decisión (<c>HeroAIDecision</c>). De ahí en
    /// adelante el juego lo trata igual que a un héroe de la IA: el mismo movimiento por la malla de navegación, los mismos
    /// golpes y las mismas órdenes a la escuadra. Si el jugador se va, el héroe vuelve a la IA.
    /// ponytail: el héroe se le da por orden de llegada, alternando bando; con un ticket del motor lo dirá su token. Sin
    /// predicción en el cliente: el héroe responde con el retraso de la red (paso 3).
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(HeroAIRusherSystem))]
    [UpdateAfter(typeof(HeroAIBalancedSystem))]
    [UpdateAfter(typeof(HeroAISkillSystem))]
    [UpdateBefore(typeof(HeroAIExecutionSystem))]
    public partial class MandoSystem : SystemBase
    {
        /// <summary>A cuántos metros por delante se le pide ir al héroe mientras el jugador empuja en una dirección.</summary>
        const float Adelanto = 4f;

        readonly List<(Entity mando, int jugador)> _sinHeroe = new List<(Entity, int)>();
        readonly List<Entity> _huerfanos = new List<Entity>();
        double _proximoAviso;

        protected override void OnCreate() => Enabled = OpcionesRed.BatallaReal > 0;

        protected override void OnUpdate()
        {
            Repartir();

            bool avisar = SystemAPI.Time.ElapsedTime >= _proximoAviso;
            if (avisar) _proximoAviso = SystemAPI.Time.ElapsedTime + 10d;
            _huerfanos.Clear();
            foreach (var (jugador, decision, pose, heroe) in SystemAPI
                         .Query<RefRW<HeroeDeJugador>, RefRW<HeroAIDecision>, RefRO<LocalTransform>>().WithEntityAccess())
            {
                var mando = jugador.ValueRO.Mando;
                if (!SystemAPI.Exists(mando) || !SystemAPI.HasComponent<EntradaJugador>(mando))
                {
                    _huerfanos.Add(heroe);
                    continue;
                }
                var entrada = SystemAPI.GetComponent<EntradaJugador>(mando);
                float3 posicion = pose.ValueRO.Position;
                var d = new HeroAIDecision();
                float2 mover = entrada.Mover;
                if (math.lengthsq(mover) > 1f) mover = math.normalize(mover);
                if (math.lengthsq(mover) > 0.01f)
                {
                    d.action = AIActionType.MoveTo;
                    d.targetPosition = posicion + new float3(mover.x, 0f, mover.y) * Adelanto;
                    d.shouldSprint = entrada.Correr != 0;
                }
                d.shouldAttack = entrada.Atacar != 0;
                if (entrada.Habilidad.IsSet)
                {
                    d.useSkill = true;
                    d.skillSlot = (SkillSlot)entrada.Ranura;
                    jugador.ValueRW.Habilidades++;
                }
                if (entrada.Orden.IsSet)
                {
                    d.squadOrder = (SquadOrderType)entrada.TipoDeOrden;
                    d.squadOrderPosition = posicion;
                    d.hasNewSquadOrder = true;
                    jugador.ValueRW.Ordenes++;
                }
                decision.ValueRW = d;
                Marcar(heroe, jugador.ValueRO.Jugador);
                if (avisar)
                {
                    int golpes = SystemAPI.HasComponent<Espejo>(heroe)
                        ? SystemAPI.GetComponent<AnimacionRed>(SystemAPI.GetComponent<Espejo>(heroe).Fantasma).Ataques : 0;
                    Debug.Log(System.FormattableString.Invariant(
                        $"[BatallaReal] Héroe del jugador {jugador.ValueRO.Jugador} en ({posicion.x:F1}, {posicion.z:F1}), entrada ({mover.x:F2}, {mover.y:F2}), ataca {entrada.Atacar}, golpes lanzados {golpes} (cuenta de 0 a 255), órdenes recibidas {jugador.ValueRO.Ordenes}, habilidades {jugador.ValueRO.Habilidades}"));
                }
            }

            foreach (var heroe in _huerfanos)
            {
                Debug.Log($"[BatallaReal] El jugador {EntityManager.GetComponentData<HeroeDeJugador>(heroe).Jugador} se fue: su héroe vuelve a la IA");
                Marcar(heroe, 0);
                EntityManager.SetComponentEnabled<BalancedBehaviorActive>(heroe, true);
                EntityManager.RemoveComponent<HeroeDeJugador>(heroe);
            }
        }

        /// <summary>El espejo del héroe dice quién lo lleva: así el cliente sabe cuál es el suyo.</summary>
        void Marcar(Entity heroe, int jugador)
        {
            if (!SystemAPI.HasComponent<Espejo>(heroe))
                return;
            var fantasma = SystemAPI.GetComponent<Espejo>(heroe).Fantasma;
            var soldado = SystemAPI.GetComponent<SoldadoRed>(fantasma);
            if (soldado.Dueno == jugador)
                return;
            soldado.Dueno = jugador;
            SystemAPI.SetComponent(fantasma, soldado);
        }

        void Repartir()
        {
            _sinHeroe.Clear();
            foreach (var (dueno, mando) in SystemAPI.Query<RefRO<GhostOwner>>().WithAll<EntradaJugador>().WithNone<MandoConHeroe>().WithEntityAccess())
                _sinHeroe.Add((mando, dueno.ValueRO.NetworkId));
            if (_sinHeroe.Count == 0)
                return;

            var libres = SystemAPI.QueryBuilder().WithAll<HeroAITag, HeroAIDecision, TeamComponent, HeroLifeComponent>().WithNone<HeroeDeJugador>().Build();
            using var heroes = libres.ToEntityArray(Allocator.Temp);
            using var bandos = libres.ToComponentDataArray<TeamComponent>(Allocator.Temp);
            var tomados = new NativeHashSet<Entity>(8, Allocator.Temp);
            foreach (var (mando, jugador) in _sinHeroe)
            {
                var quiere = jugador % 2 == 1 ? Team.TeamA : Team.TeamB;
                Entity elegido = Entity.Null;
                for (int i = 0; i < heroes.Length && elegido == Entity.Null; i++)
                    if (bandos[i].value == quiere && !tomados.Contains(heroes[i]))
                        elegido = heroes[i];
                // Los héroes aún no han salido (la batalla está cargando): se vuelve a intentar en el tick siguiente.
                if (elegido == Entity.Null)
                    continue;
                tomados.Add(elegido);
                EntityManager.AddComponentData(elegido, new HeroeDeJugador { Mando = mando, Jugador = jugador });
                EntityManager.AddComponent<MandoConHeroe>(mando);
                // Nadie más escribe su decisión: las dos conductas de la IA quedan apagadas.
                EntityManager.SetComponentEnabled<RusherBehaviorActive>(elegido, false);
                EntityManager.SetComponentEnabled<BalancedBehaviorActive>(elegido, false);
                Debug.Log($"[BatallaReal] El jugador {jugador} lleva un héroe del bando {(quiere == Team.TeamA ? "atacante" : "defensor")}");
            }
            tomados.Dispose();
        }
    }
}
