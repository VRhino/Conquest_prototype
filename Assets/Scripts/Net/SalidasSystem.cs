using System.Collections.Generic;
using BronzeAge.Contract.V1;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.AI;

namespace ConquestTactics.Net
{
    /// <summary>
    /// Servidor: este héroe ha salido de la batalla (abandonó, se desconectó o cayó donde no se reaparece). Para el juego
    /// está caído, así que nadie lo ataca ni él actúa y su escuadra se retira; no reaparece y los clientes no lo ven.
    /// </summary>
    public struct FueraDeBatalla : IComponentData { }

    /// <summary>RPC del cliente: su jugador abandona la batalla.</summary>
    public struct AbandonarBatalla : IRpcCommand { }

    /// <summary>Las reglas de la batalla que decide el autor y aplica el servidor (decisiones del 2026-11-12, Doc 5.15.1 del motor).</summary>
    public static class ReglasDeBatalla
    {
        /// <summary>Segundos que la IA lleva al héroe de un jugador que se ha desconectado, por si vuelve, antes de sacarlo de la batalla.</summary>
        public const double GraciaDeDesconexion = 60d;

        /// <summary>
        /// Si el héroe que muere reaparece: lo decide el tipo de batalla. En campo abierto (columna contra columna) no: al
        /// morir sale de la batalla, herido y con sus bajas. En los demás tipos, y en la batalla de prueba sin ticket, sí.
        /// </summary>
        public static bool Reaparece(BattleTicket ticket) =>
            ticket?.ContextoEstrategico?.TipoCase != ContextoEstrategico.TipoOneofCase.CampoAbierto;
    }

    /// <summary>
    /// Servidor de la batalla de verdad con ticket: saca de la batalla a quien tiene que salir a media partida y se lo dice
    /// al motor con una <see cref="SalidaBatalla"/>, que lleva las bajas de sus escuadras hasta ese momento:
    /// <list type="bullet">
    /// <item><b>desconexión</b>: el héroe de un jugador lleva <see cref="ReglasDeBatalla.GraciaDeDesconexion"/> segundos sin
    /// jugador (mientras, lo lleva la IA y puede volver);</item>
    /// <item><b>abandono</b>: su jugador lo pide (<see cref="AbandonarBatalla"/>);</item>
    /// <item><b>eliminado</b>: cae en un tipo de batalla sin reaparición.</item>
    /// </list>
    /// Salir es, para el juego, caer sin volver: el héroe queda caído y oculto y su escuadra se retira en el acto y queda
    /// guardada con los soldados que le quedaban. El resultado final ya no nombra ni al héroe ni a sus escuadras.
    /// ponytail: al jugador eliminado no se le echa del servidor (se queda mirando); devolverlo al mundo es cosa del
    /// cliente del juego cuando exista.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(MandoSystem))]
    public partial class SalidasSystem : SystemBase
    {
        /// <summary>Lo que se espera a que el juego retire la escuadra del que sale antes de mandar su salida de todos modos.</summary>
        const double EsperaALaEscuadra = 5d;

        readonly Dictionary<string, (MotivoSalida motivo, double desde)> _saliendo = new Dictionary<string, (MotivoSalida, double)>();
        readonly List<string> _hechos = new List<string>();
        bool _caidaDePrueba;

        protected override void OnCreate() => Enabled = OpcionesRed.BatallaReal > 0;

        protected override void OnUpdate()
        {
            var batalla = BatallaDeTicket.Actual;
            if (batalla == null || !batalla.Empezada || batalla.Terminada)
                return;
            double ahora = SystemAPI.Time.ElapsedTime;
            CaidaDePrueba(batalla);

            // Quien pide abandonar: sale, y su conexión se cierra (su cliente ya lo sabe: lo pidió él).
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (origen, peticion) in SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>>().WithAll<AbandonarBatalla>().WithEntityAccess())
            {
                var conexion = origen.ValueRO.SourceConnection;
                ecb.DestroyEntity(peticion);
                if (!SystemAPI.HasComponent<NetworkId>(conexion))
                    continue;
                int jugador = SystemAPI.GetComponent<NetworkId>(conexion).Value;
                var suyo = batalla.Heroes.Find(h => h.Dueno == jugador && !h.Salio);
                if (suyo != null)
                    Empezar(suyo.Id, MotivoSalida.SalidaAbandono, ahora);
                ecb.AddComponent<NetworkStreamRequestDisconnect>(conexion);
            }
            ecb.Playback(EntityManager);
            ecb.Dispose();

            // El héroe de un jugador que no está: la IA lo lleva un rato, por si vuelve; después sale.
            foreach (var h in batalla.Heroes)
            {
                if (h.EsBot || h.Salio || _saliendo.ContainsKey(h.Id))
                    continue;
                if (h.Dueno != 0)
                    h.SinJugadorDesde = -1d;
                else if (h.SinJugadorDesde < 0d)
                    h.SinJugadorDesde = ahora;
                else if (ahora - h.SinJugadorDesde > ReglasDeBatalla.GraciaDeDesconexion)
                    Empezar(h.Id, MotivoSalida.SalidaDesconexion, ahora);
            }

            // Donde no se reaparece, el que cae sale.
            if (!ReglasDeBatalla.Reaparece(batalla.Ticket))
                foreach (var (participante, vida) in SystemAPI.Query<RefRO<BattleParticipantComponent>, RefRO<HeroLifeComponent>>().WithNone<FueraDeBatalla>())
                {
                    if (vida.ValueRO.isAlive)
                        continue;
                    string id = participante.ValueRO.heroId.ToString();
                    var caido = batalla.Heroes.Find(h => h.Id == id);
                    if (caido != null && !caido.Salio)
                        Empezar(id, MotivoSalida.SalidaEliminado, ahora);
                }

            Sacar(batalla, ahora);
        }

        void Empezar(string heroeId, MotivoSalida motivo, double ahora)
        {
            if (_saliendo.ContainsKey(heroeId))
                return;
            _saliendo[heroeId] = (motivo, ahora);
            Debug.Log($"[Red] {heroeId} sale de la batalla: {motivo}");
        }

        /// <summary>
        /// Lleva a cada héroe que está saliendo hasta el final: caído y oculto para el juego, su escuadra retirada en el
        /// acto y, con ella ya guardada (o pasado un plazo), su salida al motor.
        /// </summary>
        void Sacar(BatallaDeTicket batalla, double ahora)
        {
            if (_saliendo.Count == 0)
                return;
            _hechos.Clear();
            foreach (var par in _saliendo)
            {
                Entity heroe = Entidad(par.Key);
                bool escuadraEnElCampo = false;
                if (heroe != Entity.Null)
                {
                    if (!EntityManager.HasComponent<FueraDeBatalla>(heroe))
                    {
                        EntityManager.AddComponent<FueraDeBatalla>(heroe);
                        // Ya no lo lleva nadie: su jugador, si sigue conectado, se queda mirando.
                        if (EntityManager.HasComponent<HeroeDeJugador>(heroe)) EntityManager.RemoveComponent<HeroeDeJugador>(heroe);
                        if (EntityManager.HasComponent<HeroNetworkMotion>(heroe)) EntityManager.RemoveComponent<HeroNetworkMotion>(heroe);
                        var vida = EntityManager.GetComponentData<HeroLifeComponent>(heroe);
                        if (vida.isAlive)
                        {
                            vida.isAlive = false;
                            EntityManager.SetComponentData(heroe, vida);
                        }
                    }
                    // Con su héroe caído el juego manda retirarse a la escuadra; aquí se le quita la espera.
                    if (EntityManager.HasComponent<HeroSquadReference>(heroe))
                    {
                        var escuadra = EntityManager.GetComponentData<HeroSquadReference>(heroe).squad;
                        if (EntityManager.Exists(escuadra))
                        {
                            escuadraEnElCampo = true;
                            if (EntityManager.HasComponent<RetreatComponent>(escuadra))
                            {
                                var retirada = EntityManager.GetComponentData<RetreatComponent>(escuadra);
                                if (retirada.retreatDuration > 0f)
                                {
                                    retirada.retreatDuration = 0f;
                                    EntityManager.SetComponentData(escuadra, retirada);
                                }
                            }
                        }
                    }
                }
                if (escuadraEnElCampo && ahora - par.Value.desde < EsperaALaEscuadra)
                    continue;

                BatallaRealDeTicket.ContarBajas(EntityManager, batalla);
                var salida = batalla.Salida(par.Key, par.Value.motivo, batalla.Asignacion?.IntentoAsignacionId ?? OpcionesRed.Intento);
                foreach (var e in salida.Escuadras)
                    Debug.Log($"[Red]   SALIDA de {par.Key} ({par.Value.motivo}): escuadra {e.SquadId}, {e.Muertos} muertos, vuelven {e.Supervivientes}");
                if (salida.Escuadras.Count == 0)
                    Debug.Log($"[Red]   SALIDA de {par.Key} ({par.Value.motivo}), sin escuadras");
                EnlaceLanzador.Actual?.Enviar(new MensajeDeServidorDeBatalla { Salida = salida });
                _hechos.Add(par.Key);
            }
            foreach (var id in _hechos)
                _saliendo.Remove(id);
        }

        Entity Entidad(string heroeId)
        {
            foreach (var (participante, entidad) in SystemAPI.Query<RefRO<BattleParticipantComponent>>().WithEntityAccess())
                if (participante.ValueRO.heroId.ToString() == heroeId)
                    return entidad;
            return Entity.Null;
        }

        /// <summary>Prueba (<c>-caeA segundos</c>): pasado ese tiempo de batalla, el primer héroe humano del ticket cae.</summary>
        void CaidaDePrueba(BatallaDeTicket batalla)
        {
            if (_caidaDePrueba || OpcionesRed.CaeA <= 0f || batalla.InicioMs <= 0)
                return;
            if (System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - batalla.InicioMs < OpcionesRed.CaeA * 1000f)
                return;
            var humano = batalla.Heroes.Find(h => !h.EsBot && !h.Salio);
            Entity heroe = humano == null ? Entity.Null : Entidad(humano.Id);
            if (heroe == Entity.Null || !EntityManager.HasComponent<HeroHealthComponent>(heroe))
                return;
            _caidaDePrueba = true;
            var salud = EntityManager.GetComponentData<HeroHealthComponent>(heroe);
            salud.currentHealth = 0f;
            EntityManager.SetComponentData(heroe, salud);
            Debug.Log($"[Red] Prueba: {humano.Id} cae");
        }
    }

    /// <summary>
    /// Servidor de la batalla de verdad: los héroes caídos reaparecen en su punto de salida, igual los de los jugadores que
    /// los de la IA (decisión del autor, 2026-11-12), salvo en los tipos de batalla sin reaparición
    /// (<see cref="ReglasDeBatalla.Reaparece"/>) y salvo quien ya salió de la batalla. El juego solo hace reaparecer al
    /// héroe local (<c>HeroRespawnSystem</c>), y en el servidor no hay ninguno: esto hace lo mismo para los demás, con su
    /// misma cuenta atrás y su misma protección al volver. Su escuadra, que se retiró al caer él, vuelve a salir sola
    /// con los soldados que le quedaban (<c>SquadSpawningSystem</c>).
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(HeroRespawnSystem))]
    public partial class ReaparicionSystem : SystemBase
    {
        protected override void OnCreate() => Enabled = OpcionesRed.BatallaReal > 0;

        protected override void OnUpdate()
        {
            var batalla = BatallaDeTicket.Actual;
            if (batalla != null && !ReglasDeBatalla.Reaparece(batalla.Ticket))
                return;
            float dt = SystemAPI.Time.DeltaTime;
            double ahora = SystemAPI.Time.ElapsedTime;
            SystemAPI.TryGetSingleton<HeroGameplayConfigComponent>(out var config);
            foreach (var (vida, salud, punto, pose, heroe) in SystemAPI
                         .Query<RefRW<HeroLifeComponent>, RefRW<HeroHealthComponent>, RefRO<HeroSpawnComponent>, RefRW<LocalTransform>>()
                         .WithNone<IsLocalPlayer, FueraDeBatalla>().WithEntityAccess())
            {
                if (vida.ValueRO.isAlive)
                    continue;
                vida.ValueRW.deathTimer -= dt;
                if (vida.ValueRO.deathTimer > 0f)
                    continue;
                vida.ValueRW.isAlive = true;
                vida.ValueRW.deathTimer = vida.ValueRO.respawnCooldown;
                salud.ValueRW.currentHealth = salud.ValueRO.maxHealth;
                // La misma protección al volver que HeroRespawnSystem da al héroe local.
                if (config.respawnProtectionSeconds > 0f && config.respawnProtectionHealthRatio > 0f && EntityManager.HasBuffer<StatModifier>(heroe))
                {
                    StatEffects.ApplyTimed(EntityManager.GetBuffer<StatModifier>(heroe), StatId.MinHealthRatio, ModifierOp.Flat,
                        config.respawnProtectionHealthRatio, ahora + config.respawnProtectionSeconds, ModifierSource.Effect,
                        StatEffects.RespawnProtection);
                    if (EntityManager.HasComponent<ModifiersDirty>(heroe)) EntityManager.SetComponentEnabled<ModifiersDirty>(heroe, true);
                }
                // A su punto de salida. Si lo lleva un jugador, MandoSystem ve que el juego lo ha movido y lleva allí su mando.
                pose.ValueRW.Position = punto.ValueRO.spawnPosition;
                pose.ValueRW.Rotation = punto.ValueRO.spawnRotation;
                var agente = EntityManager.HasComponent<NavMeshAgent>(heroe) ? EntityManager.GetComponentObject<NavMeshAgent>(heroe) : null;
                if (agente != null && agente.enabled)
                    agente.Warp(punto.ValueRO.spawnPosition);
                string quien = SystemAPI.HasComponent<BattleParticipantComponent>(heroe)
                    ? SystemAPI.GetComponent<BattleParticipantComponent>(heroe).heroId.ToString() : heroe.Index.ToString();
                Debug.Log($"[BatallaReal] {quien} reaparece en su punto de salida");
            }
        }
    }
}
