using System;
using BronzeAge.Contract.V1;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;

namespace ConquestTactics.Net
{
    /// <summary>
    /// Servidor: juega la batalla de <c>-ticket</c>. Reparte los héroes humanos entre las conexiones por orden de
    /// llegada (ponytail: sin comprobar el token; eso llega con la asignación real), empieza cuando están todos o pasa
    /// <c>-espera</c>, coloca a cada bando en su lado y cierra por aniquilación o por tiempo (gana el defensor).
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(EntrarEnBatallaServidorSystem))]
    [UpdateAfter(typeof(BajasSystem))]
    public partial class BatallaDeTicketSystem : SystemBase
    {
        const float SeparacionBandos = 25f;
        const float SeparacionHeroes = 10f;
        const float VidaHeroe = 400f, DanoHeroe = 35f;
        const float VidaSoldado = 100f, DanoSoldado = 20f;

        double _creada;
        double _empezada;

        protected override void OnCreate()
        {
            if (string.IsNullOrEmpty(OpcionesRed.Ticket))
            {
                Enabled = false;
                return;
            }
            var ticket = BatallaDeTicket.Leer(OpcionesRed.Ticket);
            // CQ-003: una tropa que Conquest no tiene rechaza la batalla entera, nunca se juega con una por defecto.
            var desconocidas = BatallaDeTicket.TropasDesconocidas(ticket, id => TropasRed.Buscar(id) != null);
            if (desconocidas.Count > 0)
            {
                Debug.LogError($"[Red] RECHAZADA {ticket.BattleId}: tropas que Conquest no tiene: {string.Join(", ", desconocidas)}");
                Enabled = false;
#if !UNITY_EDITOR
                Application.Quit(3);
#endif
                return;
            }
            BatallaDeTicket.Actual = new BatallaDeTicket(ticket, OpcionesRed.Duracion);
            Debug.Log($"[Red] Ticket {ticket.BattleId}: {BatallaDeTicket.Actual.Heroes.Count} héroes, " +
                      $"{BatallaDeTicket.Actual.Escuadras.Count} escuadras, {BatallaDeTicket.Actual.DuracionSegundos} s");
            RequireForUpdate<PrefabsRed>();
            _creada = -1;
        }

        protected override void OnUpdate()
        {
            var batalla = BatallaDeTicket.Actual;
            double ahora = SystemAPI.Time.ElapsedTime;
            if (_creada < 0) _creada = ahora;

            if (!batalla.Empezada)
            {
                while (batalla.ConexionesSinHeroe.Count > 0)
                {
                    int id = batalla.ConexionesSinHeroe.Dequeue();
                    var libre = batalla.Heroes.Find(h => !h.EsBot && h.Dueno == 0);
                    if (libre != null) libre.Dueno = id;
                    Debug.Log(libre != null ? $"[Red] El jugador {id} lleva a {libre.Id}" : $"[Red] El jugador {id} mira: no quedan héroes");
                }
                bool todos = batalla.Heroes.TrueForAll(h => h.EsBot || h.Dueno != 0);
                if (todos || ahora - _creada >= OpcionesRed.EsperaJugadores)
                    Empezar(batalla, ahora);
                return;
            }
            if (batalla.Terminada)
                return;

            int vivosAtacante = 0, vivosDefensor = 0;
            foreach (var (bando, vida) in SystemAPI.Query<RefRO<BandoServidor>, RefRO<VidaServidor>>())
            {
                if (vida.ValueRO.Vida <= 0f) continue;
                if (bando.ValueRO.Bando == BatallaDeTicket.Atacante) vivosAtacante++; else vivosDefensor++;
            }
            if (vivosAtacante == 0 || vivosDefensor == 0)
                Terminar(batalla, vivosAtacante > 0 ? LadoId.Atacante : LadoId.Defensor, RazonVictoria.Aniquilacion);
            else if (ahora - _empezada >= batalla.DuracionSegundos)
                Terminar(batalla, LadoId.Defensor, RazonVictoria.TiempoAgotado);
        }

        void Empezar(BatallaDeTicket batalla, double ahora)
        {
            batalla.Empezada = true;
            batalla.InicioMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            _empezada = ahora;
            var prefabs = SystemAPI.GetSingleton<PrefabsRed>();
            var em = EntityManager;
            var lider = new Entity[batalla.Heroes.Count];
            var colocados = new int[2];

            for (int i = 0; i < batalla.Heroes.Count; i++)
            {
                var h = batalla.Heroes[i];
                var heroe = em.Instantiate(prefabs.Heroe);
                em.SetComponentData(heroe, Pose(h.Bando, colocados[h.Bando]++));
                em.SetComponentData(heroe, new GhostOwner { NetworkId = h.Dueno });
                if (h.Dueno == 0) em.AddComponentData(heroe, new HeroeSinJugador());
                Combatiente(em, heroe, h.Bando, VidaHeroe, DanoHeroe, -1, i);
                lider[i] = heroe;
            }

            // Cada héroe lleva sus escuadras una detrás de otra, cada una en la formación de reposo de su tropa.
            var fondo = new float[batalla.Heroes.Count];
            var desfases = new System.Collections.Generic.List<float3>();
            for (int e = 0; e < batalla.Escuadras.Count; e++)
            {
                var escuadra = batalla.Escuadras[e];
                Entity guia;
                desfases.Clear();
                var reposo = TropasRed.Buscar(escuadra.TropaId)?.restFormation;
                uint semilla = (uint)escuadra.Id.GetHashCode();
                if (escuadra.Heroe >= 0)
                {
                    guia = lider[escuadra.Heroe];
                    fondo[escuadra.Heroe] = FormacionDeReposo.Desfases(reposo, escuadra.Autorizados, semilla, fondo[escuadra.Heroe], desfases);
                }
                else
                {
                    // Escuadra sin héroe: la guía un ancla que solo vive en el servidor y que lleva la IA.
                    guia = em.CreateEntity();
                    em.AddComponentData(guia, Pose(escuadra.Bando, colocados[escuadra.Bando]++));
                    em.AddComponentData(guia, new HeroeRed { Velocidad = 5f });
                    em.AddComponentData(guia, new HeroeSinJugador());
                    em.AddComponentData(guia, new BandoServidor { Bando = escuadra.Bando });
                    em.AddComponentData(guia, new ObjetivoCombate());
                    FormacionDeReposo.Desfases(reposo, escuadra.Autorizados, semilla, 0f, desfases);
                }
                var poseGuia = em.GetComponentData<LocalTransform>(guia);
                for (int s = 0; s < escuadra.Autorizados; s++)
                {
                    var soldado = em.Instantiate(prefabs.Soldado);
                    em.SetComponentData(soldado, LocalTransform.FromPositionRotation(
                        poseGuia.Position + math.mul(poseGuia.Rotation, desfases[s]), poseGuia.Rotation));
                    em.SetComponentData(soldado, new SoldadoRed { Bando = escuadra.Bando });
                    em.AddComponentData(soldado, new SoldadoEnServidor { Heroe = guia, Desfase = desfases[s] });
                    Combatiente(em, soldado, escuadra.Bando, VidaSoldado, DanoSoldado, e, -1);
                }
            }
            Debug.Log($"[Red] Empieza la batalla {batalla.Ticket.BattleId}");
        }

        static LocalTransform Pose(byte bando, int orden)
        {
            float lado = bando == BatallaDeTicket.Atacante ? -1f : 1f;
            var posicion = new float3(lado * SeparacionBandos, 0f, orden * SeparacionHeroes);
            return LocalTransform.FromPositionRotation(posicion, quaternion.LookRotationSafe(new float3(-lado, 0f, 0f), math.up()));
        }

        static void Combatiente(EntityManager em, Entity entidad, byte bando, float vida, float dano, int escuadra, int heroe)
        {
            em.AddComponentData(entidad, new BandoServidor { Bando = bando });
            em.AddComponentData(entidad, new VidaServidor { Vida = vida, DanoPorSegundo = dano });
            em.AddComponentData(entidad, new ObjetivoCombate());
            em.AddComponentData(entidad, new RegistroEnTicket { Escuadra = escuadra, Heroe = heroe });
        }

        void Terminar(BatallaDeTicket batalla, LadoId ganador, RazonVictoria razon)
        {
            batalla.Terminada = true;
            var resultado = batalla.Resultado(ganador, razon, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), OpcionesRed.Intento);
            var fallos = BatallaDeTicket.Comprobar(batalla.Ticket, resultado);
            Debug.Log($"[Red] RESULTADO {ganador} por {razon} en {(resultado.FinMs - resultado.InicioMs) / 1000f:F1} s; " +
                      (fallos.Count == 0 ? "el motor lo aceptaría" : "FALLOS: " + string.Join(", ", fallos)));
            foreach (var e in resultado.PorEscuadra)
                Debug.Log($"[Red]   escuadra {e.SquadId}: {e.SupervivientesAlCierre} vivos, {e.Muertos} muertos");
            if (!string.IsNullOrEmpty(OpcionesRed.Resultado))
            {
                BatallaDeTicket.Escribir(OpcionesRed.Resultado, resultado);
                Debug.Log($"[Red] Resultado escrito en {OpcionesRed.Resultado}");
#if !UNITY_EDITOR
                Application.Quit(fallos.Count == 0 ? 0 : 2);
#endif
            }
        }
    }

    /// <summary>
    /// Servidor: cada combatiente busca al enemigo vivo más cercano y, si lo tiene al alcance, le quita vida.
    /// ponytail: búsqueda de todos contra todos, sobra para un ticket (decenas de soldados); con cientos, rejilla espacial.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(PredictedSimulationSystemGroup))]
    [UpdateBefore(typeof(HeroesSinJugadorSystem))]
    [BurstCompile]
    public partial struct CombateSystem : ISystem
    {
        public const float Alcance = 1.6f;

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var consulta = SystemAPI.QueryBuilder().WithAll<BandoServidor, VidaServidor, LocalTransform>().Build();
            if (consulta.IsEmpty)
                return;
            float dt = SystemAPI.Time.DeltaTime;
            var entidades = consulta.ToEntityArray(Allocator.Temp);
            var bandos = consulta.ToComponentDataArray<BandoServidor>(Allocator.Temp);
            var vidas = consulta.ToComponentDataArray<VidaServidor>(Allocator.Temp);
            var poses = consulta.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            var golpes = new NativeArray<float>(entidades.Length, Allocator.Temp);

            foreach (var (bando, pose, objetivo, entidad) in SystemAPI.Query<RefRO<BandoServidor>, RefRO<LocalTransform>, RefRW<ObjetivoCombate>>().WithEntityAccess())
            {
                int mejor = -1;
                float mejorDistancia = float.MaxValue;
                for (int i = 0; i < entidades.Length; i++)
                {
                    if (bandos[i].Bando == bando.ValueRO.Bando || vidas[i].Vida <= 0f) continue;
                    float d = math.distancesq(poses[i].Position, pose.ValueRO.Position);
                    if (d < mejorDistancia) { mejorDistancia = d; mejor = i; }
                }
                objetivo.ValueRW = mejor < 0
                    ? default
                    : new ObjetivoCombate { Enemigo = entidades[mejor], Posicion = poses[mejor].Position, Distancia = math.sqrt(mejorDistancia) };

                if (mejor >= 0 && objetivo.ValueRO.Distancia <= Alcance && SystemAPI.HasComponent<VidaServidor>(entidad))
                {
                    var propia = SystemAPI.GetComponent<VidaServidor>(entidad);
                    if (propia.Vida > 0f) golpes[mejor] += propia.DanoPorSegundo * dt;
                }
            }

            for (int i = 0; i < entidades.Length; i++)
                if (golpes[i] > 0f)
                    SystemAPI.SetComponent(entidades[i], new VidaServidor { Vida = vidas[i].Vida - golpes[i], DanoPorSegundo = vidas[i].DanoPorSegundo });
        }
    }

    /// <summary>Servidor: apunta en el ticket a quien cae y lo quita del mundo (el fantasma desaparece en los clientes).</summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(CombateSystem))]
    public partial class BajasSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var batalla = BatallaDeTicket.Actual;
            if (batalla == null || batalla.Terminada)
                return;
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (vida, registro, entidad) in SystemAPI.Query<RefRO<VidaServidor>, RefRO<RegistroEnTicket>>().WithEntityAccess())
            {
                if (vida.ValueRO.Vida > 0f) continue;
                if (registro.ValueRO.Escuadra >= 0) batalla.Escuadras[registro.ValueRO.Escuadra].Muertos++;
                if (registro.ValueRO.Heroe >= 0) batalla.Heroes[registro.ValueRO.Heroe].Caido = true;
                ecb.DestroyEntity(entidad);
            }
            ecb.Playback(EntityManager);
        }
    }
}
