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
    /// Servidor: juega la batalla de un ticket del motor, que llega de un fichero (<c>-ticket</c>) o del lanzador
    /// (<c>--lanzador</c>, Batalla_Unity_Ciclo §8.8). Con la asignación del lanzador, cada cliente se presenta con su token y
    /// lleva el héroe de ese token; uno que no casa se desconecta. Con fichero, los héroes humanos se dan por orden de
    /// llegada. Empieza cuando están todos o pasa <c>-espera</c> (los que falten, con la IA), coloca a cada bando en su lado,
    /// cierra por aniquilación o por tiempo (gana el defensor) y manda o escribe el resultado.
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

        double _creada = -1;
        double _empezada;
        bool _relojEnMarcha;
        /// <summary>Batalla de verdad: los que se unieron con la batalla en marcha y aún no han salido al campo.</summary>
        readonly System.Collections.Generic.List<IncorporacionBatalla> _porEntrar = new System.Collections.Generic.List<IncorporacionBatalla>();

        /// <summary>
        /// Con <c>-batallaReal</c> (o la variable CONQUEST_BATALLA_REAL=1) el ticket se juega con la batalla de verdad de
        /// Conquest (<see cref="BatallaRealDeTicket"/>) en vez de con las cápsulas de prueba.
        /// </summary>
        static bool Real => OpcionesRed.BatallaReal > 0;

        protected override void OnCreate()
        {
            RequireForUpdate<PrefabsRed>();
            if (!string.IsNullOrEmpty(OpcionesRed.Lanzador))
            {
                EnlaceLanzador.Actual = EnlaceLanzador.Conectar(OpcionesRed.Lanzador);
                return;
            }
            if (string.IsNullOrEmpty(OpcionesRed.Ticket))
            {
                Enabled = false;
                return;
            }
            Preparar(BatallaDeTicket.Leer(OpcionesRed.Ticket));
        }

        /// <summary>CQ-003: una tropa que Conquest no tiene rechaza la batalla entera, nunca se juega con una por defecto.</summary>
        void Preparar(BattleTicket ticket)
        {
            var desconocidas = BatallaDeTicket.TropasDesconocidas(ticket,
                id => Real ? BatallaRealDeTicket.TropaConocida(id) : TropasRed.Buscar(id) != null);
            if (desconocidas.Count > 0)
            {
                // Sin resultado, el motor vence la batalla por su plazo, sin castigo (§8.8).
                Debug.LogError($"[Red] RECHAZADA {ticket.BattleId}: tropas que Conquest no tiene: {string.Join(", ", desconocidas)}");
                Enabled = false;
                Salir(3);
                return;
            }
            BatallaDeTicket.Actual = new BatallaDeTicket(ticket, OpcionesRed.Duracion);
            _creada = -1;
            Debug.Log($"[Red] Ticket {ticket.BattleId} (revisión {ticket.TicketRevision}): {BatallaDeTicket.Actual.Heroes.Count} héroes, " +
                      $"{BatallaDeTicket.Actual.Escuadras.Count} escuadras, {BatallaDeTicket.Actual.DuracionSegundos} s");
        }

        protected override void OnUpdate()
        {
            double ahora = SystemAPI.Time.ElapsedTime;
            LeerLanzador();
            var batalla = BatallaDeTicket.Actual;
            if (batalla == null)
                return;
            if (_creada < 0) _creada = ahora;

            if (!batalla.Empezada)
            {
                if (EnlaceLanzador.Actual != null && batalla.Asignacion == null)
                    return; // sin la asignación no hay tokens con los que recibir a nadie
                RepartirHeroes(batalla);
                bool todos = batalla.Heroes.TrueForAll(h => h.EsBot || h.Dueno != 0);
                if (todos || ahora - _creada >= OpcionesRed.EsperaJugadores)
                    Empezar(batalla, ahora);
                return;
            }
            if (batalla.Terminada)
                return;
            if (Real)
            {
                // Quien entra con la batalla en marcha (el que vuelve tras caerse, el que se unió después) recibe su héroe.
                RepartirHeroes(batalla);
                _porEntrar.RemoveAll(i => BatallaRealDeTicket.Incorporar(batalla, i));
                // La batalla de verdad acaba cuando lo dice el juego: su ganador y, si fue por tiempo, la razón.
                if (!SystemAPI.TryGetSingleton<MatchStateComponent>(out var partida))
                    return;
                // La batalla cuenta desde que arranca su reloj, no desde que se empezó a cargar el mapa.
                if (!SystemAPI.HasSingleton<BattleClockComponent>())
                    return;
                long ahoraMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                if (!_relojEnMarcha)
                {
                    _relojEnMarcha = true;
                    batalla.InicioMs = ahoraMs;
                }
                // El plazo del ticket manda sobre el reloj del juego, que cada captura alarga: nunca le queda más de lo
                // que falta para el plazo (menos un margen), y al agotarse gana el defensor, como pide el motor.
                float queda = math.max(0f, batalla.DuracionSegundos - BatallaRealDeTicket.MargenDelPlazo - (ahoraMs - batalla.InicioMs) / 1000f);
                var relojDelJuego = SystemAPI.GetSingletonRW<BattleClockComponent>();
                if (relojDelJuego.ValueRO.remainingSeconds > queda)
                    relojDelJuego.ValueRW.remainingSeconds = queda;
                BatallaRealDeTicket.ContarBajas(EntityManager, batalla);
                if (partida.currentState != MatchState.EndMatch)
                    return;
                bool porTiempo = SystemAPI.TryGetSingleton<BattleClockComponent>(out var reloj) && reloj.expired;
                Terminar(batalla, partida.winnerTeam == 1 ? LadoId.Atacante : LadoId.Defensor,
                    porTiempo ? RazonVictoria.TiempoAgotado : RazonVictoria.ObjetivosCapturados);
                return;
            }

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

        void LeerLanzador()
        {
            var enlace = EnlaceLanzador.Actual;
            if (enlace == null)
                return;
            while (enlace.Recibidos.TryDequeue(out var m))
            {
                var batalla = BatallaDeTicket.Actual;
                switch (m.CuerpoCase)
                {
                    case MensajeParaProcesoDeBatalla.CuerpoOneofCase.Ticket:
                        // Otro ticket antes de empezar = revisión subida: se empieza de nuevo con él (§8.8, punto 4).
                        if (batalla == null || !batalla.Empezada) Preparar(m.Ticket);
                        else Debug.LogWarning($"[Red] Ticket con revisión {m.Ticket.TicketRevision} con la batalla ya empezada: ignorado");
                        break;
                    case MensajeParaProcesoDeBatalla.CuerpoOneofCase.Asignacion:
                        if (batalla != null) batalla.Asignacion = m.Asignacion;
                        Debug.Log($"[Red] Asignación {m.Asignacion.IntentoAsignacionId}: {m.Asignacion.TokensParticipante.Count} humanos con token");
                        break;
                    case MensajeParaProcesoDeBatalla.CuerpoOneofCase.Tokens:
                        batalla?.Asignacion?.TokensParticipante.AddRange(m.Tokens.TokensParticipante);
                        break;
                    case MensajeParaProcesoDeBatalla.CuerpoOneofCase.Incorporacion:
                        if (batalla == null || !batalla.Incorporar(m.Incorporacion))
                            break;
                        string unido = m.Incorporacion.Participante.HeroeId;
                        Debug.Log($"[Red] Incorporación {m.Incorporacion.Secuencia}: {unido} se une al bando {m.Incorporacion.Lado}" +
                                  (batalla.Empezada ? " con la batalla en marcha" : " antes de empezar"));
                        // Antes de empezar entra con todos. Después, la batalla de verdad lo saca al campo; la de cápsulas
                        // (ponytail) solo lo cuenta: sus escuadras vuelven enteras.
                        if (batalla.Empezada && Real) _porEntrar.Add(m.Incorporacion);
                        else if (batalla.Empezada) batalla.DejarFuera(unido);
                        break;
                    case MensajeParaProcesoDeBatalla.CuerpoOneofCase.Acuse:
                        var acuse = m.Acuse;
                        Debug.Log($"[Red] Acuse del motor a «{acuse.Mensaje}»: {acuse.Resultado} {acuse.Rechazo?.Codigo}");
                        if (acuse.Mensaje == "resultado")
                            Salir(acuse.Resultado == Resultado.OrdenAceptada || acuse.Resultado == Resultado.OrdenSinCambios ? 0 : 2);
                        break;
                }
            }
            if (enlace.Cerrado)
            {
                Debug.LogError("[Red] El lanzador cerró el socket");
                EnlaceLanzador.Actual = null;
                Salir(4);
            }
        }

        /// <summary>Da a cada cliente que espera el héroe de su token (con asignación) o el primero libre (con fichero).</summary>
        void RepartirHeroes(BatallaDeTicket batalla)
        {
            long ahoraMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            while (BatallaDeTicket.ConexionesSinHeroe.Count > 0)
            {
                var (id, conexion, token) = BatallaDeTicket.ConexionesSinHeroe.Dequeue();
                HeroeEnJuego heroe;
                if (batalla.Asignacion != null)
                {
                    TokenParticipante valido = null;
                    foreach (var t in batalla.Asignacion.TokensParticipante)
                        if (t.Token == token && (t.ExpiraEnMs == 0 || ahoraMs <= t.ExpiraEnMs)) valido = t;
                    // En la batalla de verdad, quien entra otra vez con su token releva a su conexión anterior (su proceso
                    // cayó y el servidor aún no se ha enterado): el héroe es de quien trae el token.
                    heroe = valido == null ? null : batalla.Heroes.Find(h => h.Id == valido.HeroeId && (h.Dueno == 0 || Real));
                    if (heroe == null)
                    {
                        Debug.LogWarning($"[Red] El jugador {id} no trae un token válido: fuera");
                        if (EntityManager.Exists(conexion)) EntityManager.AddComponent<NetworkStreamRequestDisconnect>(conexion);
                        continue;
                    }
                }
                else
                    heroe = batalla.Heroes.Find(h => !h.EsBot && h.Dueno == 0);
                if (heroe != null)
                {
                    if (heroe.Dueno != 0 && heroe.Dueno != id)
                    {
                        Debug.Log($"[Red] El jugador {id} entra otra vez con el token de {heroe.Id}: se corta su conexión anterior ({heroe.Dueno})");
                        MandoSystem.Cortar(EntityManager, heroe.Dueno);
                    }
                    heroe.Dueno = id;
                }
                Debug.Log(heroe != null ? $"[Red] El jugador {id} lleva a {heroe.Id}" : $"[Red] El jugador {id} mira: no quedan héroes");
            }
        }

        static void Salir(int codigo)
        {
#if !UNITY_EDITOR
            Application.Quit(codigo);
#endif
        }

        void Empezar(BatallaDeTicket batalla, double ahora)
        {
            batalla.Empezada = true;
            batalla.InicioMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            _empezada = ahora;
            EnlaceLanzador.Actual?.Enviar(new MensajeDeServidorDeBatalla
            {
                Inicio = new InicioBatalla
                {
                    SchemaVersion = 1, BattleId = batalla.Ticket.BattleId, TicketRevision = batalla.Ticket.TicketRevision,
                    IntentoAsignacionId = batalla.Asignacion?.IntentoAsignacionId ?? OpcionesRed.Intento,
                },
            });
            if (Real)
            {
                BatallaRealSystem.CargarDeTicket(batalla);
                return;
            }
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
            var resultado = batalla.Resultado(ganador, razon, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                batalla.Asignacion?.IntentoAsignacionId ?? OpcionesRed.Intento);
            var fallos = BatallaDeTicket.Comprobar(batalla.Ticket, resultado, batalla.Incorporaciones);
            Debug.Log($"[Red] RESULTADO {ganador} por {razon} en {(resultado.FinMs - resultado.InicioMs) / 1000f:F1} s; " +
                      (fallos.Count == 0 ? "el motor lo aceptaría" : "FALLOS: " + string.Join(", ", fallos)));
            foreach (var e in resultado.PorEscuadra)
                Debug.Log($"[Red]   escuadra {e.SquadId}: {e.Desplegados} en el campo, {e.Muertos} muertos, vuelven {e.SupervivientesAlCierre}");
            if (!string.IsNullOrEmpty(OpcionesRed.Resultado))
            {
                BatallaDeTicket.Escribir(OpcionesRed.Resultado, resultado);
                Debug.Log($"[Red] Resultado escrito en {OpcionesRed.Resultado}");
            }
            if (EnlaceLanzador.Actual != null)
                EnlaceLanzador.Actual.Enviar(new MensajeDeServidorDeBatalla { Resultado = resultado }); // se sale con su acuse
            else if (!string.IsNullOrEmpty(OpcionesRed.Resultado))
                Salir(fallos.Count == 0 ? 0 : 2);
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
