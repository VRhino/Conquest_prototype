using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;

namespace ConquestTactics.Net
{
    /// <summary>
    /// Un golpe resuelto en el servidor, para pintar en el cliente su número de daño y su chispa. Es lo que el juego guarda
    /// en un DamageFeedbackEvent; <see cref="Propio"/> dice si el héroe o los soldados de ese jugador lo dieron o lo recibieron.
    /// </summary>
    public struct GolpeRed : IRpcCommand
    {
        public float3 Posicion;
        public byte Categoria;
        public float Cantidad;
        public byte Propio;
    }

    /// <summary>Un proyectil disparado en el servidor: de dónde sale, adónde va y cómo vuela. El cliente pinta el suyo, que no hace daño.</summary>
    public struct ProyectilRed : IRpcCommand
    {
        public FixedString32Bytes Reserva;
        public float3 Origen;
        public float3 Destino;
        public byte Trayectoria;
        public byte Bando;
    }

    /// <summary>
    /// La reserva de proyectiles del juego (ObjectPoolSystem) y los proyectiles de cada tropa de tiro. En el juego las
    /// pone la escena (ObjectPoolSystem y RangedCombatBootstrap), pero solo BattleTestScene las tiene: en BattleScene, sin
    /// reserva, ProjectileSpawnSystem no dispara nada y las peticiones se quedan para siempre. Para la batalla en red se
    /// crean aquí si faltan, en el servidor y en el cliente.
    /// </summary>
    public static class ReservaDeProyectiles
    {
        const int PorTropa = 20;

        public static void Asegurar()
        {
            if (ObjectPoolSystem.Instance == null)
                new GameObject("ObjectPoolSystem (red)").AddComponent<ObjectPoolSystem>();
            int tropas = 0;
            foreach (var tropa in SquadDataService.GetAllSquads())
            {
                if (tropa == null || !tropa.IsRanged)
                    continue;
                var tiro = tropa.rangedData;
                if (string.IsNullOrEmpty(tiro.projectilePoolKey) || tiro.projectilePrefab == null)
                    continue;
                ObjectPoolSystem.Instance.WarmUpPool(tiro.projectilePoolKey, tiro.projectilePrefab, PorTropa);
                tropas++;
            }
            Debug.Log($"[Red] Reserva de proyectiles lista para {tropas} tropas de tiro");
        }
    }

    /// <summary>
    /// Servidor de la batalla de verdad: recoge los golpes que DamageCalculationSystem deja apuntados para la interfaz y
    /// avisa de cada uno a los jugadores a los que les toca (los suyos, y los que pasan a menos de 40 m de su héroe). En
    /// el juego de un jugador esos apuntes los consume DamageFeedbackPresentationSystem, que en el servidor no corre: sin
    /// este sistema se quedarían ahí para siempre.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(DamageCalculationSystem))]
    public partial class GolpesSystem : SystemBase
    {
        const float Cerca = 40f;

        readonly List<(Entity heroe, Entity conexion, float3 posicion)> _jugadores = new List<(Entity, Entity, float3)>();
        public static int Enviados { get; private set; }

        protected override void OnCreate()
        {
            RequireForUpdate<DamageFeedbackEvent>();
            Enabled = OpcionesRed.BatallaReal > 0;
        }

        protected override void OnUpdate()
        {
            var conexiones = new NativeHashMap<int, Entity>(8, Allocator.Temp);
            foreach (var (id, conexion) in SystemAPI.Query<RefRO<NetworkId>>().WithAll<NetworkStreamInGame>().WithEntityAccess())
                conexiones[id.ValueRO.Value] = conexion;
            _jugadores.Clear();
            foreach (var (jugador, pose, heroe) in SystemAPI.Query<RefRO<HeroeDeJugador>, RefRO<LocalTransform>>().WithEntityAccess())
                if (conexiones.TryGetValue(jugador.ValueRO.Jugador, out var conexion))
                    _jugadores.Add((heroe, conexion, pose.ValueRO.Position));

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (golpe, entidad) in SystemAPI.Query<RefRO<DamageFeedbackEvent>>().WithEntityAccess())
            {
                var g = golpe.ValueRO;
                if (_jugadores.Count > 0)
                {
                    Entity deQuien = FloatingTextRelevance.OwnerHero(EntityManager, g.source);
                    Entity aQuien = FloatingTextRelevance.OwnerHero(EntityManager, g.target);
                    foreach (var (heroe, conexion, posicion) in _jugadores)
                    {
                        bool propio = heroe == deQuien || heroe == aQuien;
                        if (!propio && math.distancesq(posicion, g.position) > Cerca * Cerca)
                            continue;
                        var aviso = ecb.CreateEntity();
                        ecb.AddComponent(aviso, new GolpeRed
                            { Posicion = g.position, Categoria = (byte)g.category, Cantidad = g.amount, Propio = (byte)(propio ? 1 : 0) });
                        ecb.AddComponent(aviso, new SendRpcCommandRequest { TargetConnection = conexion });
                        Enviados++;
                    }
                }
                ecb.DestroyEntity(entidad);
            }
            ecb.Playback(EntityManager);
            conexiones.Dispose();
        }
    }

    /// <summary>
    /// Servidor de la batalla de verdad: avisa a todos de cada proyectil que se dispara, antes de que ProjectileSpawnSystem
    /// se lleve la petición. El destino se fija aquí con la misma cuenta que ProjectileController (el pecho del objetivo
    /// en el momento del disparo, o 20 m en la dirección del tiro).
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(RangedAttackSystem))]
    [UpdateBefore(typeof(ProjectileSpawnSystem))]
    public partial class ProyectilesSystem : SystemBase
    {
        public static int Enviados { get; private set; }

        protected override void OnCreate()
        {
            RequireForUpdate<ProjectileSpawnRequest>();
            Enabled = OpcionesRed.BatallaReal > 0;
        }

        protected override void OnUpdate()
        {
            if (SystemAPI.QueryBuilder().WithAll<NetworkStreamInGame>().Build().IsEmpty)
                return;
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var peticion in SystemAPI.Query<RefRO<ProjectileSpawnRequest>>())
            {
                var p = peticion.ValueRO;
                float3 destino = p.spawnPosition + p.attackDirection * 20f;
                if (SystemAPI.Exists(p.target) && SystemAPI.HasComponent<LocalTransform>(p.target))
                    destino = SystemAPI.GetComponent<LocalTransform>(p.target).Position + new float3(0f, 0.9f, 0f);
                var aviso = ecb.CreateEntity();
                ecb.AddComponent(aviso, new ProyectilRed
                {
                    Reserva = p.poolKey, Origen = p.spawnPosition, Destino = destino,
                    Trayectoria = (byte)p.trajectory, Bando = (byte)p.sourceTeam,
                });
                ecb.AddComponent<SendRpcCommandRequest>(aviso);
                Enviados++;
            }
            ecb.Playback(EntityManager);
        }
    }

    /// <summary>
    /// Cliente: pinta los golpes y los proyectiles que avisa el servidor, con lo mismo que usa el juego: el texto flotante
    /// (FloatingCombatTextManager), la chispa del golpe (HitImpactEffectManager) y el proyectil de la reserva
    /// (ObjectPoolSystem), que aquí vuela sin perfil de daño y por tanto no hiere a nadie. Un cliente sin pantalla solo
    /// los cuenta y los tira.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class EfectosClienteSystem : SystemBase
    {
        /// <summary>El texto sale a esta altura sobre el golpe, como en DamageFeedbackPresentationSystem.</summary>
        const float AlturaDelTexto = 1.8f;

        readonly List<GolpeRed> _golpes = new List<GolpeRed>();
        readonly List<ProyectilRed> _proyectiles = new List<ProyectilRed>();
        readonly List<Collider> _sinColliders = new List<Collider>();
        public static int Golpes { get; private set; }
        public static int Proyectiles { get; private set; }

        protected override void OnUpdate()
        {
            _golpes.Clear();
            _proyectiles.Clear();
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (golpe, entidad) in SystemAPI.Query<RefRO<GolpeRed>>().WithAll<ReceiveRpcCommandRequest>().WithEntityAccess())
            {
                _golpes.Add(golpe.ValueRO);
                ecb.DestroyEntity(entidad);
            }
            foreach (var (proyectil, entidad) in SystemAPI.Query<RefRO<ProyectilRed>>().WithAll<ReceiveRpcCommandRequest>().WithEntityAccess())
            {
                _proyectiles.Add(proyectil.ValueRO);
                ecb.DestroyEntity(entidad);
            }
            ecb.Playback(EntityManager);
            Golpes += _golpes.Count;
            Proyectiles += _proyectiles.Count;
            if (Application.isBatchMode)
                return;

            foreach (var g in _golpes)
            {
                var punto = new Vector3(g.Posicion.x, g.Posicion.y, g.Posicion.z);
                if (FloatingCombatTextManager.Instance != null)
                    FloatingCombatTextManager.Instance.Spawn(punto + Vector3.up * AlturaDelTexto, (DamageCategory)g.Categoria, g.Cantidad, g.Propio != 0);
                if (HitImpactEffectManager.Instance != null && g.Categoria != (byte)DamageCategory.Blocked)
                    HitImpactEffectManager.Instance.Spawn(punto + Vector3.up * 1.1f);
            }

            foreach (var p in _proyectiles)
            {
                if (ObjectPoolSystem.Instance == null)
                    break;
                var objeto = ObjectPoolSystem.Instance.GetFromPool(p.Reserva.ToString());
                var proyectil = objeto != null ? objeto.GetComponent<ProjectileController>() : null;
                if (proyectil == null)
                    continue;
                // ProjectileController saca su destino del «objetivo» en el momento del disparo: una entidad de paso en ese punto.
                var blanco = EntityManager.CreateEntity(typeof(LocalTransform));
                EntityManager.SetComponentData(blanco, LocalTransform.FromPosition(p.Destino - new float3(0f, 0.9f, 0f)));
                proyectil.Initialize(Entity.Null, blanco, p.Origen, math.normalizesafe(p.Destino - p.Origen), Entity.Null, default,
                    (Team)p.Bando, 1f, p.Reserva.ToString(), (ProjectileTrajectory)p.Trayectoria, _sinColliders);
                EntityManager.DestroyEntity(blanco);
            }
        }
    }
}
