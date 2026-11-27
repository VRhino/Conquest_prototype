using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.AI;

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
        /// <summary>Dónde lo puso el mando en el último tick: si amanece en otro sitio, lo ha movido el juego.</summary>
        public float3 Puesto;
        public bool Colocado;
        /// <summary>Si el jugador quiere ir a caballo (G lo cambia), y si iba montado en el tick anterior.</summary>
        public bool QuiereMontar;
        public bool IbaMontado;
        public int Formaciones;
        /// <summary>La marca de tiempo de lo último que llegó de su jugador, y cuándo cambió por última vez.</summary>
        public uint UltimaMarca;
        public double OidoEn;
        public bool Callado;
    }

    /// <summary>Servidor: quién lleva este mando (su token; en la batalla de prueba, la identidad de su proceso), para devolverle su héroe si vuelve.</summary>
    public struct IdentidadDeMando : IComponentData
    {
        public FixedString128Bytes Token;
    }

    /// <summary>Servidor: este mando ya tiene héroe.</summary>
    public struct MandoConHeroe : IComponentData { }

    /// <summary>
    /// Mueve el mando de cada jugador con su entrada, dentro de la predicción de Netcode: el cliente lo adelanta y el
    /// servidor lo confirma con las mismas cuentas. El paso se pega a la malla de navegación de la escena de batalla, que
    /// es la misma en los dos lados; así el héroe responde al instante y no atraviesa murallas. Un cliente que lleva medio
    /// segundo sin oír al servidor deja de adelantarlo. Un cliente sin el mapa
    /// cargado (los bots sin pantalla) se mueve libre y el servidor lo corrige.
    /// Además choca con los cuerpos enemigos cercanos (<see cref="ChoqueDelMando"/>), tal como los ve su cliente (el servidor, con su retraso
    /// de interpolación), para que el servidor no tenga que corregirlo al meterse
    /// en una escuadra; con <c>-sinChoque</c> en el servidor, solo la malla.
    /// ponytail: sin aceleración ni gastar resistencia al correr.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    public partial class MovimientoMandoSystem : SystemBase
    {
        const float Margen = 1f;
        /// <summary>Ticks de cuerpos que se guardan (más que el mayor retraso de interpolación más la ida y vuelta, a 60 Hz).</summary>
        const uint Guardados = 128;
        EntityQuery _cuerpos;
        /// <summary>
        /// Los cuerpos (posición y bando) de cada tick. En el servidor, los de ese tick: con el retraso de interpolación de cada cliente
        /// (<see cref="CommandDataInterpolationDelay"/>) choca contra los cuerpos tal como ese cliente los veía. En el cliente, los que vio al
        /// predecir ese tick por primera vez, para que al volver a predecirlo choque contra lo mismo.
        /// </summary>
        readonly Dictionary<uint, (float3[] pos, byte[] bando)> _vistos = new();

        protected override void OnCreate() => _cuerpos = GetEntityQuery(ComponentType.ReadOnly<SoldadoRed>(), ComponentType.ReadOnly<LocalTransform>());

        (float3[] pos, byte[] bando) Ahora()
        {
            using var poses = _cuerpos.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            using var soldados = _cuerpos.ToComponentDataArray<SoldadoRed>(Allocator.Temp);
            var pos = new float3[poses.Length];
            var bando = new byte[poses.Length];
            for (int i = 0; i < pos.Length; i++) { pos[i] = poses[i].Position; bando[i] = soldados[i].Bando; }
            return (pos, bando);
        }

        protected override void OnUpdate()
        {
            // El cliente sin noticias del servidor no adelanta a su héroe: allí también está parado.
            if (World.IsClient() && ReconexionClienteSystem.SinNoticias)
                return;
            float dt = SystemAPI.Time.DeltaTime;
            // Los cuerpos de cada bando (soldados y héroes en red), por tick: los del servidor en ese tick, o los que vio el cliente al predecirlo.
            var tiempo = SystemAPI.GetSingleton<NetworkTime>();
            uint tick = tiempo.ServerTick.IsValid ? tiempo.ServerTick.TickIndexForValidTick : 0u;
            bool servidor = World.IsServer();
            if (servidor || !_vistos.ContainsKey(tick)) _vistos[tick] = Ahora();
            _vistos.Remove(tick - Guardados);
            using var enemigos = new NativeList<float3>(64, Allocator.Temp);
            foreach (var (pose, entrada, movil, mando) in SystemAPI.Query<RefRW<LocalTransform>, RefRO<EntradaJugador>, RefRO<MandoMovil>>().WithAll<Simulate>().WithEntityAccess())
            {
                float2 mover = entrada.ValueRO.Mover;
                float2 mirar = entrada.ValueRO.Mirar;
                // Mira a donde mira la cámara, también quieto (como el héroe del juego de un jugador, que siempre encara la cámara).
                if (movil.ValueRO.Activo != 0 && math.lengthsq(mirar) > 1e-4f)
                    pose.ValueRW.Rotation = quaternion.LookRotationSafe(new float3(mirar.x, 0f, mirar.y), math.up());
                if (movil.ValueRO.Activo == 0 || math.lengthsq(mover) < 1e-4f)
                    continue;
                if (math.lengthsq(mover) > 1f) mover = math.normalize(mover);
                float3 direccion = new float3(mover.x, 0f, mover.y);
                float velocidad = entrada.ValueRO.Correr != 0 ? movil.ValueRO.VelocidadAlCorrer : movil.ValueRO.Velocidad;
                float3 destino = pose.ValueRO.Position + direccion * velocidad * dt;
                // Con mapa: el paso acaba en el punto de la malla más cercano (junto a una muralla, resbala por su borde).
                bool conMalla = NavMesh.SamplePosition(pose.ValueRO.Position, out _, 2f, NavMesh.AllAreas);
                if (conMalla)
                {
                    if (!NavMesh.SamplePosition(destino, out var toque, Margen, NavMesh.AllAreas))
                        continue;
                    destino = toque.position;
                }
                // El choque con los cuerpos enemigos (la predicción «c»). Si el punto apartado se sale de la malla, vale el de la malla sola.
                if (movil.ValueRO.Choque > 0f)
                {
                    // En el servidor, los cuerpos como los veía este cliente: los de hace su retraso de interpolación (si aún se guardan).
                    uint visto = tick;
                    if (servidor && SystemAPI.HasComponent<CommandDataInterpolationDelay>(mando))
                        visto = tick - SystemAPI.GetComponent<CommandDataInterpolationDelay>(mando).Delay;
                    var cuerpos = _vistos.TryGetValue(visto, out var c) ? c : _vistos[tick];
                    enemigos.Clear();
                    for (int i = 0; i < cuerpos.pos.Length; i++)
                        if (cuerpos.bando[i] != movil.ValueRO.Bando) enemigos.Add(cuerpos.pos[i]);
                    var apartado = ChoqueDelMando.Apartar(pose.ValueRO.Position, destino, enemigos.AsArray(), movil.ValueRO.Choque);
                    if (!math.all(math.isfinite(apartado))) apartado = destino;
                    else if (conMalla) apartado = NavMesh.SamplePosition(apartado, out var enMalla, Margen, NavMesh.AllAreas) ? (float3)enMalla.position : destino;
                    destino = apartado;
                }
                pose.ValueRW.Position = destino;
                if (math.lengthsq(mirar) <= 1e-4f)
                    pose.ValueRW.Rotation = quaternion.LookRotationSafe(direccion, math.up());
            }
        }
    }

    /// <summary>
    /// Servidor de la batalla de verdad: da a cada jugador que entra un héroe de la batalla. El héroe va donde va el mando
    /// del jugador (<see cref="MovimientoMandoSystem"/>, predicho) y lo demás de su entrada (<see cref="EntradaJugador"/>)
    /// se pone donde la IA de ese héroe pondría su decisión (<c>HeroAIDecision</c>): los mismos golpes, habilidades y
    /// órdenes a la escuadra que un héroe de la IA. Si el juego mueve al héroe por su cuenta (un empujón, reaparecer), el
    /// mando se va con él y el cliente recibe la corrección. Si de su jugador no llega nada (un corte de red), el héroe
    /// se queda quieto: su última entrada no se repite. Si el jugador se va (o el corte pasa del plazo de la conexión),
    /// el héroe vuelve a la IA; si vuelve, se le devuelve el mismo (por su token o su identidad).
    /// ponytail: sin ticket, el héroe se da por orden de llegada, alternando bando.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(HeroAIRusherSystem))]
    [UpdateAfter(typeof(HeroAIBalancedSystem))]
    [UpdateAfter(typeof(HeroAISkillSystem))]
    [UpdateBefore(typeof(HeroAIExecutionSystem))]
    public partial class MandoSystem : SystemBase
    {
        /// <summary>Dónde pone el cliente su cámara respecto a su héroe (DibujoRedSystem).</summary>
        public static readonly Vector3 CamaraDelCliente = new Vector3(0f, 14f, -7f);

        /// <summary>Más lejos que esto de donde lo dejó el mando, al héroe lo ha movido el juego.</summary>
        const float Desvio = 0.5f;
        /// <summary>Medición: veces que el juego movió a un héroe de jugador más que <see cref="Desvio"/> (el mando se fue con él: una corrección que no es del choque), y la mayor.</summary>
        public static int Recolocaciones;
        public static float MayorRecolocacion;
        /// <summary>Segundos sin que llegue nada de un jugador para dejar de repetir su última entrada.</summary>
        const double Silencio = 0.5d;

        readonly List<(Entity mando, int jugador)> _sinHeroe = new List<(Entity, int)>();
        readonly List<Entity> _huerfanos = new List<Entity>();
        /// <summary>De cada jugador conectado, la marca de tiempo de lo último que llegó de él.</summary>
        readonly Dictionary<int, uint> _marcas = new Dictionary<int, uint>();
        /// <summary>Batalla de prueba: el héroe que llevó cada identidad, por si vuelve.</summary>
        readonly Dictionary<string, Entity> _heroeDe = new Dictionary<string, Entity>();
        double _proximoAviso;

        protected override void OnCreate() => Enabled = OpcionesRed.BatallaReal > 0;

        protected override void OnUpdate()
        {
            _marcas.Clear();
            foreach (var (id, acuse) in SystemAPI.Query<RefRO<NetworkId>, RefRO<NetworkSnapshotAck>>())
                _marcas[id.ValueRO.Value] = acuse.ValueRO.LastReceiveTimestamp;
            // Con ticket: el héroe de un jugador que ya no está conectado queda libre, y vuelve a ser suyo si entra otra
            // vez con su token.
            if (BatallaDeTicket.Actual != null)
                foreach (var h in BatallaDeTicket.Actual.Heroes)
                    if (h.Dueno != 0 && !_marcas.ContainsKey(h.Dueno))
                        h.Dueno = 0;
            Repartir();

            double ahora = SystemAPI.Time.ElapsedTime;
            bool avisar = SystemAPI.Time.ElapsedTime >= _proximoAviso;
            if (avisar) _proximoAviso = SystemAPI.Time.ElapsedTime + 10d;
            _huerfanos.Clear();
            foreach (var (jugador, decision, vida, heroe) in SystemAPI
                         .Query<RefRW<HeroeDeJugador>, RefRW<HeroAIDecision>, RefRO<HeroLifeComponent>>().WithEntityAccess())
            {
                var mando = jugador.ValueRO.Mando;
                if (!SystemAPI.Exists(mando) || !SystemAPI.HasComponent<EntradaJugador>(mando))
                {
                    _huerfanos.Add(heroe);
                    continue;
                }
                var entrada = SystemAPI.GetComponent<EntradaJugador>(mando);
                // Netcode repite la última entrada mientras no llega otra: con el jugador callado, se da por vacía.
                if (_marcas.TryGetValue(jugador.ValueRO.Jugador, out uint marca) && marca != jugador.ValueRO.UltimaMarca)
                {
                    jugador.ValueRW.UltimaMarca = marca;
                    jugador.ValueRW.OidoEn = ahora;
                }
                bool callado = ahora - jugador.ValueRO.OidoEn > Silencio;
                if (callado != jugador.ValueRO.Callado)
                {
                    jugador.ValueRW.Callado = callado;
                    Debug.Log(callado ? $"[BatallaReal] Del jugador {jugador.ValueRO.Jugador} no llega nada: su héroe se queda quieto"
                        : $"[BatallaReal] El jugador {jugador.ValueRO.Jugador} se vuelve a oír");
                }
                if (callado)
                    entrada = default;
                float3 posicion = SystemAPI.GetComponent<LocalTransform>(heroe).Position;
                float2 mover = entrada.Mover;
                if (math.lengthsq(mover) > 1f) mover = math.normalize(mover);

                float3 velocidad = Llevar(heroe, mando, ref jugador.ValueRW, posicion, vida.ValueRO.isAlive && !callado, entrada, mover);

                // El héroe no decide adónde ir (va con el mando); el resto es lo que la IA pondría en su decisión.
                var d = new HeroAIDecision { shouldAttack = entrada.Atacar != 0 };
                if (entrada.Habilidad.IsSet)
                {
                    // Las de la escuadra las lanza la escuadra; las del héroe (y sus perks), el héroe.
                    var ranura = (SkillSlot)entrada.Ranura;
                    if (BattleCommandInput.IsSquadSlot(ranura))
                    {
                        d.useSquadSkill = true;
                        d.squadSkillSlot = ranura;
                    }
                    else
                    {
                        d.useSkill = true;
                        d.skillSlot = ranura;
                    }
                    jugador.ValueRW.Habilidades++;
                }
                decision.ValueRW = d;
                if (entrada.Orden.IsSet) jugador.ValueRW.Ordenes++;
                if (entrada.Formacion.IsSet) jugador.ValueRW.Formaciones++;
                MandarEscuadra(heroe, entrada, posicion);
                Montura(heroe, entrada, ref jugador.ValueRW);
                CambiarEscuadra(heroe, entrada);
                Marcar(heroe, jugador.ValueRO.Jugador);
                MarcarEscuadra(heroe, jugador.ValueRO.Jugador);
                Interfaz(heroe, mando);
                if (avisar)
                {
                    int golpes = SystemAPI.HasComponent<Espejo>(heroe)
                        ? SystemAPI.GetComponent<AnimacionRed>(SystemAPI.GetComponent<Espejo>(heroe).Fantasma).Ataques : 0;
                    Debug.Log("[BatallaReal] " + Escuadra(heroe, posicion));
                    Debug.Log(System.FormattableString.Invariant(
                        $"[BatallaReal] Héroe del jugador {jugador.ValueRO.Jugador} en ({posicion.x:F1}, {posicion.z:F1}), entrada ({mover.x:F2}, {mover.y:F2}), velocidad {math.length(velocidad):F1}, ataca {entrada.Atacar}, golpes lanzados {golpes} (cuenta de 0 a 255), órdenes recibidas {jugador.ValueRO.Ordenes}, habilidades {jugador.ValueRO.Habilidades}, formaciones {jugador.ValueRO.Formaciones}, montura {EstadoDeMontura(heroe)}{(callado ? ", CALLADO" : "")}"));
                }
            }

            // Fuera del recorrido: añadir un componente en mitad de uno lo invalida.
            foreach (var (heroe, peticion) in _cambios)
            {
                if (EntityManager.HasComponent<SquadSwapRequest>(heroe)) EntityManager.SetComponentData(heroe, peticion);
                else EntityManager.AddComponentData(heroe, peticion);
            }
            _cambios.Clear();

            foreach (var heroe in _huerfanos)
            {
                Debug.Log($"[BatallaReal] El jugador {EntityManager.GetComponentData<HeroeDeJugador>(heroe).Jugador} se fue: su héroe vuelve a la IA");
                Marcar(heroe, 0);
                MarcarEscuadra(heroe, 0);
                EntityManager.SetComponentEnabled<BalancedBehaviorActive>(heroe, true);
                EntityManager.RemoveComponent<HeroeDeJugador>(heroe);
                EntityManager.RemoveComponent<HeroNetworkMotion>(heroe);
            }
        }

        /// <summary>
        /// Pone al héroe donde está el mando, o el mando donde está el héroe si el juego lo ha movido (o aún no se había
        /// colocado). Devuelve la velocidad con la que se mueve, para su animación.
        /// </summary>
        float3 Llevar(Entity heroe, Entity mando, ref HeroeDeJugador jugador, float3 posicion, bool vivo, in EntradaJugador entrada, float2 mover)
        {
            var estadisticas = SystemAPI.GetComponent<HeroStatsComponent>(heroe);
            float paso = SystemAPI.HasComponent<MoveSpeedComponent>(heroe) ? SystemAPI.GetComponent<MoveSpeedComponent>(heroe).value : estadisticas.baseSpeed;
            var movil = new MandoMovil { Activo = (byte)(vivo ? 1 : 0), Velocidad = paso, VelocidadAlCorrer = paso * math.max(1f, estadisticas.sprintMultiplier) };
            // El choque predicho con los cuerpos enemigos: el contacto único del juego, la suma de los dos radios de cuerpo.
            movil.Bando = (byte)(SystemAPI.HasComponent<TeamComponent>(heroe) && SystemAPI.GetComponent<TeamComponent>(heroe).value == Team.TeamB ? 1 : 0);
            movil.Choque = !OpcionesRed.SinChoque && SystemAPI.TryGetSingleton<MovementTuningComponent>(out var ajuste) ? 2f * ajuste.bodyRadius : 0f;
            // A caballo, los aires del caballo (trote y galope); subiendo o bajando, el héroe no se mueve.
            if (SystemAPI.HasComponent<HeroMountComponent>(heroe))
            {
                var montura = SystemAPI.GetComponent<HeroMountComponent>(heroe);
                if (montura.state == HeroMountState.Mounted)
                {
                    var aires = SystemAPI.TryGetSingleton<HeroGameplayConfigComponent>(out var config) ? config.mount.OrDefault().For(EntityManager, heroe) : MountTuning.Default.For(EntityManager, heroe);
                    movil.Velocidad = aires.trotSpeed;
                    movil.VelocidadAlCorrer = aires.gallopSpeed;
                }
                else if (montura.state != HeroMountState.OnFoot)
                    movil.Activo = 0;
            }
            var agente = EntityManager.HasComponent<NavMeshAgent>(heroe) ? EntityManager.GetComponentObject<NavMeshAgent>(heroe) : null;
            if (agente == null || !agente.enabled || !agente.isOnNavMesh)
                movil.Activo = 0;
            SystemAPI.SetComponent(mando, movil);

            var poseMando = SystemAPI.GetComponent<LocalTransform>(mando);
            float3 velocidad = float3.zero;
            bool movidoPorElJuego = !jugador.Colocado || math.distance(posicion.xz, jugador.Puesto.xz) > Desvio;
            if (jugador.Colocado && movidoPorElJuego && movil.Activo != 0) { Recolocaciones++; MayorRecolocacion = math.max(MayorRecolocacion, math.distance(posicion.xz, jugador.Puesto.xz)); }
            if (movil.Activo == 0 || movidoPorElJuego)
            {
                poseMando.Position = posicion;
                poseMando.Rotation = SystemAPI.GetComponent<LocalTransform>(heroe).Rotation;
                SystemAPI.SetComponent(mando, poseMando);
                jugador.Colocado = movil.Activo != 0;
            }
            else
            {
                if (math.distancesq(poseMando.Position.xz, posicion.xz) > 1e-6f)
                    agente.Warp(poseMando.Position);
                if (math.lengthsq(mover) > 1e-4f || math.lengthsq(entrada.Mirar) > 1e-4f)
                    agente.transform.rotation = poseMando.Rotation;
                if (math.lengthsq(mover) > 1e-4f)
                {
                    velocidad = new float3(mover.x, 0f, mover.y) * (entrada.Correr != 0 ? movil.VelocidadAlCorrer : movil.Velocidad);
                }
            }
            jugador.Puesto = poseMando.Position;
            SystemAPI.SetComponent(heroe, new HeroNetworkMotion
                { velocity = velocidad, requestedSpeed = entrada.Correr != 0 ? movil.VelocidadAlCorrer : movil.Velocidad });
            return velocidad;
        }

        /// <summary>
        /// Lo que la interfaz del juego enseña de este héroe y su escuadra, con las mismas cuentas que UIBattleDataSystem
        /// hace para el héroe local: va a su mando y solo le llega a su jugador.
        /// ponytail: falta la zona de captura (LocalHeroCaptureStateComponent es del héroe local; llega con las zonas).
        /// </summary>
        void Interfaz(Entity heroe, Entity mando)
        {
            var ui = new UIHeroBattleDataComponent();
            double ahora = SystemAPI.Time.ElapsedTime;
            ui.skills.Length = UIHeroBattleDataComponent.SkillSlotCount;
            if (SystemAPI.HasBuffer<HeroSkillElement>(heroe) && SystemAPI.HasBuffer<CooldownGroupElement>(heroe))
            {
                var habilidades = SystemAPI.GetBuffer<HeroSkillElement>(heroe);
                UIBattleDataSystem.WriteSkills(ref ui.skills, habilidades, SystemAPI.GetBuffer<CooldownGroupElement>(heroe), ahora, SkillSlot.Q, SkillSlot.ActivePerk2);
                var ajuste = SystemAPI.TryGetSingleton<HeroGameplayConfigComponent>(out var config) ? config.combo.OrDefault() : ComboTuning.Default;
                var recursos = SystemAPI.HasComponent<HeroResourcesComponent>(heroe) ? SystemAPI.GetComponent<HeroResourcesComponent>(heroe) : default;
                UIBattleDataSystem.WriteCosts(ref ui, habilidades, recursos, ahora, ajuste);
            }
            var red = new InterfazRed { Combo = ui.combo, CosteDeDefinitiva = ui.ultimateComboCost, SinPagar = ui.unaffordableSlots };
            if (SystemAPI.HasComponent<HeroHealthComponent>(heroe))
            {
                var vida = SystemAPI.GetComponent<HeroHealthComponent>(heroe);
                red.Vida = vida.currentHealth;
                red.VidaMaxima = vida.maxHealth;
            }
            if (SystemAPI.HasComponent<StaminaComponent>(heroe))
            {
                var resistencia = SystemAPI.GetComponent<StaminaComponent>(heroe);
                red.Resistencia = resistencia.currentStamina;
                red.ResistenciaMaxima = resistencia.maxStamina;
            }
            red.Vivo = (byte)(SystemAPI.GetComponent<HeroLifeComponent>(heroe).isAlive ? 1 : 0);
            if (SystemAPI.HasComponent<HeroSquadReference>(heroe))
            {
                var escuadra = SystemAPI.GetComponent<HeroSquadReference>(heroe).squad;
                if (SystemAPI.Exists(escuadra))
                {
                    if (SystemAPI.HasBuffer<HeroSkillElement>(escuadra) && SystemAPI.HasBuffer<CooldownGroupElement>(escuadra))
                        UIBattleDataSystem.WriteSkills(ref ui.skills, SystemAPI.GetBuffer<HeroSkillElement>(escuadra),
                            SystemAPI.GetBuffer<CooldownGroupElement>(escuadra), ahora, SkillSlot.Squad1, SkillSlot.Squad3);
                    if (SystemAPI.HasComponent<SquadStatusComponent>(escuadra))
                    {
                        var estado = SystemAPI.GetComponent<SquadStatusComponent>(escuadra);
                        red.Soldados = estado.aliveUnits;
                        red.SoldadosTotales = estado.totalUnits;
                    }
                    else if (SystemAPI.HasBuffer<SquadUnitElement>(escuadra))
                    {
                        // SquadStatusUpdateSystem solo lleva la del héroe local, y en el servidor no hay: los vivos, contados aquí (el total lo pone el
                        // HUD con el de la tropa, porque los muertos salen del búfer)
                        var unidades = SystemAPI.GetBuffer<SquadUnitElement>(escuadra);
                        int vivos = 0;
                        for (int u = 0; u < unidades.Length; u++)
                            if (SystemAPI.Exists(unidades[u].Value) && !SystemAPI.HasComponent<IsDeadComponent>(unidades[u].Value)) vivos++;
                        red.Soldados = vivos;
                        red.SoldadosTotales = 0;
                    }
                    if (SystemAPI.HasComponent<FormationComponent>(escuadra))
                        red.Formacion = (byte)SystemAPI.GetComponent<FormationComponent>(escuadra).currentFormation;
                }
            }
            red.Bando = (byte)(SystemAPI.GetComponent<TeamComponent>(heroe).value == Team.TeamB ? 2 : 1);
            Reserva(heroe, mando, ref red, ahora);
            EnZona(SystemAPI.GetComponent<LocalTransform>(heroe).Position, ref red);
            SystemAPI.SetComponent(mando, red);
            // la vida de cada soldado de su escuadra, para las barras del HUD (de más a menos, como las ordena la ficha)
            var vidas = SystemAPI.GetBuffer<VidaDeSoldadoRed>(mando);
            vidas.Clear();
            if (SystemAPI.HasComponent<HeroSquadReference>(heroe))
            {
                var escuadra = SystemAPI.GetComponent<HeroSquadReference>(heroe).squad;
                if (SystemAPI.Exists(escuadra) && SystemAPI.HasBuffer<SquadUnitElement>(escuadra))
                {
                    var unidades = SystemAPI.GetBuffer<SquadUnitElement>(escuadra);
                    for (int u = 0; u < unidades.Length; u++)
                    {
                        var unidad = unidades[u].Value;
                        float v = 0f;
                        if (SystemAPI.Exists(unidad) && !SystemAPI.HasComponent<IsDeadComponent>(unidad) && SystemAPI.HasComponent<HealthComponent>(unidad))
                        {
                            var hp = SystemAPI.GetComponent<HealthComponent>(unidad);
                            v = hp.maxHealth > 0f ? math.saturate(hp.currentHealth / hp.maxHealth) : 0f;
                        }
                        vidas.Add(new VidaDeSoldadoRed { Vida = (byte)math.round(v * 255f) });
                    }
                    for (int a = 1; a < vidas.Length; a++)
                        for (int b = a; b > 0 && vidas[b].Vida > vidas[b - 1].Vida; b--) { var t = vidas[b]; vidas[b] = vidas[b - 1]; vidas[b - 1] = t; }
                }
            }
            var casillas = SystemAPI.GetBuffer<HabilidadRed>(mando);
            casillas.ResizeUninitialized(ui.skills.Length);
            for (int i = 0; i < ui.skills.Length; i++)
                casillas[i] = new HabilidadRed { Id = ui.skills[i].skillId, Recarga = ui.skills[i].cooldown, Queda = ui.skills[i].remaining };
        }

        /// <summary>
        /// Si el héroe está dentro de una zona activa de captura, o de una de suministro que se está capturando, y cuánto
        /// lleva: lo mismo que LocalHeroCaptureTrackerSystem mira para el héroe local.
        /// </summary>
        void EnZona(float3 posicion, ref InterfazRed red)
        {
            foreach (var (zona, pose, entidad) in SystemAPI.Query<RefRO<ZoneTriggerComponent>, RefRO<LocalTransform>>().WithEntityAccess())
            {
                if (!zona.ValueRO.isActive || math.distancesq(posicion, pose.ValueRO.Position) > zona.ValueRO.radius * zona.ValueRO.radius)
                    continue;
                if (SystemAPI.HasComponent<CapturePointProgressComponent>(entidad))
                {
                    red.EnZona = 1;
                    red.Captura = SystemAPI.GetComponent<CapturePointProgressComponent>(entidad).captureProgress;
                    return;
                }
                if (SystemAPI.HasComponent<SupplyPointComponent>(entidad) && SystemAPI.GetComponent<SupplyPointComponent>(entidad).isCapturing)
                {
                    red.EnZona = 1;
                    red.Captura = SystemAPI.GetComponent<SupplyPointComponent>(entidad).captureProgress;
                    return;
                }
            }
        }

        /// <summary>
        /// El cambio de escuadra del jugador: con el héroe en un punto de suministro, se pide como lo pide el panel del juego de un jugador
        /// (<see cref="SquadSwapRequest"/>); <c>SquadSwapSystem</c> valida lo demás (vivo, sin espera, la escuadra fuera de combate, la zona
        /// propia y sin disputar) y <c>SquadSwapExecutionSystem</c> lo hace.
        /// </summary>
        readonly System.Collections.Generic.List<(Entity heroe, SquadSwapRequest peticion)> _cambios = new System.Collections.Generic.List<(Entity, SquadSwapRequest)>();

        void CambiarEscuadra(Entity heroe, in EntradaJugador entrada)
        {
            if (!entrada.CambiarEscuadra.IsSet)
                return;
            float3 posicion = SystemAPI.GetComponent<LocalTransform>(heroe).Position;
            foreach (var (zona, pose) in SystemAPI.Query<RefRO<ZoneTriggerComponent>, RefRO<LocalTransform>>().WithAll<SupplyPointComponent>())
            {
                if (math.distancesq(posicion, pose.ValueRO.Position) > zona.ValueRO.radius * zona.ValueRO.radius)
                    continue;
                _cambios.Add((heroe, new SquadSwapRequest { newSquadId = entrada.EscuadraPedida, zoneId = zona.ValueRO.zoneId }));
                Debug.Log($"[BatallaReal] El héroe {heroe} pide cambiar a la escuadra {entrada.EscuadraPedida} en la zona {zona.ValueRO.zoneId}");
                return;
            }
        }

        /// <summary>
        /// La escuadra activa (la de <see cref="HeroSquadSelectionComponent"/>, no la primera del ticket), el cambio en curso y su espera, y
        /// la reserva (las del ticket con sus soldados y si cayeron): lo que el panel de suministro lee del héroe local.
        /// </summary>
        void Reserva(Entity heroe, Entity mando, ref InterfazRed red, double ahora)
        {
            int activa = SystemAPI.HasComponent<HeroSquadSelectionComponent>(heroe) ? SystemAPI.GetComponent<HeroSquadSelectionComponent>(heroe).instanceId : -1;
            red.EscuadraActiva = activa;
            if (SystemAPI.HasComponent<SquadSwapChannelingComponent>(heroe))
            {
                var c = SystemAPI.GetComponent<SquadSwapChannelingComponent>(heroe);
                red.Canalizando = c.duration > 0f ? math.max(0.01f, c.timer / c.duration) : 0.01f;
            }
            red.EsperaDeCambio = SquadSwapCooldown.Remaining(EntityManager, heroe, ahora);
            var reserva = SystemAPI.GetBuffer<ReservaRed>(mando);
            reserva.Clear();
            if (!SystemAPI.HasBuffer<SquadIdMapElement>(heroe))
                return;
            var mapa = SystemAPI.GetBuffer<SquadIdMapElement>(heroe);
            var inactivas = SystemAPI.HasBuffer<InactiveSquadElement>(heroe) ? SystemAPI.GetBuffer<InactiveSquadElement>(heroe) : default;
            for (int i = 0; i < mapa.Length; i++)
            {
                var e = new ReservaRed { Id = mapa[i].squadId, Tropa = mapa[i].baseSquadID, Vivos = red.Soldados };
                if (mapa[i].squadId == activa)
                    red.Escuadra = mapa[i].baseSquadID;
                if (inactivas.IsCreated)
                    for (int k = 0; k < inactivas.Length; k++)
                        if (inactivas[k].squadId == mapa[i].squadId)
                        {
                            e.Vivos = inactivas[k].aliveUnits;
                            e.Eliminada = (byte)(inactivas[k].isEliminated ? 1 : 0);
                        }
                reserva.Add(e);
            }
            if (red.Escuadra.Length == 0 && mapa.Length > 0)
                red.Escuadra = mapa[0].baseSquadID;
        }

        string EstadoDeMontura(Entity heroe) =>
            SystemAPI.HasComponent<HeroMountComponent>(heroe) ? SystemAPI.GetComponent<HeroMountComponent>(heroe).state.ToString() : "sin caballo";

        /// <summary>Para el registro: qué orden tiene la escuadra del héroe, de quién viene y a qué distancia está el centro de sus soldados.</summary>
        string Escuadra(Entity heroe, float3 posicion)
        {
            if (!SystemAPI.HasComponent<HeroSquadReference>(heroe))
                return "sin escuadra";
            var escuadra = SystemAPI.GetComponent<HeroSquadReference>(heroe).squad;
            if (!SystemAPI.Exists(escuadra) || !SystemAPI.HasComponent<SquadStateComponent>(escuadra))
                return "escuadra sin estado";
            var estado = SystemAPI.GetComponent<SquadStateComponent>(escuadra);
            // SystemAPI no puede ir dentro de una cadena interpolada: el generador de código no lo traduce.
            string resuelta = "?";
            if (SystemAPI.HasComponent<SquadResolvedOrderComponent>(escuadra))
            {
                var orden = SystemAPI.GetComponent<SquadResolvedOrderComponent>(escuadra);
                resuelta = orden.order + " de " + orden.source;
            }
            string pedida = "?";
            if (SystemAPI.HasComponent<SquadPlayerOrderIntentComponent>(escuadra))
            {
                var intencion = SystemAPI.GetComponent<SquadPlayerOrderIntentComponent>(escuadra);
                pedida = intencion.orderType + " en " + intencion.desiredFormation;
            }
            if (SystemAPI.HasComponent<FormationComponent>(escuadra))
                pedida += ", formada en " + SystemAPI.GetComponent<FormationComponent>(escuadra).currentFormation;
            float3 centro = float3.zero;
            int vivos = 0;
            if (SystemAPI.HasBuffer<SquadUnitElement>(escuadra))
                foreach (var soldado in SystemAPI.GetBuffer<SquadUnitElement>(escuadra))
                    if (SystemAPI.Exists(soldado.Value) && SystemAPI.HasComponent<LocalTransform>(soldado.Value))
                    {
                        centro += SystemAPI.GetComponent<LocalTransform>(soldado.Value).Position;
                        vivos++;
                    }
            float lejos = vivos > 0 ? math.distance((centro / vivos).xz, posicion.xz) : 0f;
            return System.FormattableString.Invariant(
                $"Escuadra: pedida {pedida}, resuelta {resuelta}, en curso {estado.currentOrder} ({estado.currentState}), {vivos} soldados a {lejos:F1} m del héroe");
        }

        /// <summary>
        /// Las órdenes del jugador a su escuadra, por el mismo camino que las del jugador local del juego
        /// (SquadPlayerCommandResolver): seguir, mantener en un punto, atacar, la prisa y las formaciones, con su
        /// insistencia. OrderResolutionSystem trata esta escuadra como la del jugador porque su héroe lleva
        /// HeroNetworkMotion.
        /// </summary>
        void MandarEscuadra(Entity heroe, in EntradaJugador entrada, float3 posicion)
        {
            if (!entrada.Orden.IsSet && !entrada.Formacion.IsSet && !entrada.Prisa.IsSet)
                return;
            if (!SystemAPI.HasComponent<HeroSquadReference>(heroe))
                return;
            var escuadra = SystemAPI.GetComponent<HeroSquadReference>(heroe).squad;
            if (!SystemAPI.Exists(escuadra) || !SystemAPI.HasComponent<SquadInputComponent>(escuadra)
                || !SystemAPI.HasComponent<SquadPlayerOrderIntentComponent>(escuadra))
                return;
            var comando = new SquadControlCommand
            {
                orderIssued = entrada.Orden.IsSet,
                order = (SquadOrderType)entrada.TipoDeOrden,
                holdPosition = math.lengthsq(entrada.PuntoDeOrden) > 0f ? entrada.PuntoDeOrden : posicion,
                toggleHurry = entrada.Prisa.IsSet,
                formationRequested = entrada.Formacion.IsSet,
                formationIndex = entrada.IndiceDeFormacion == EntradaJugador.SiguienteFormacion ? -1 : entrada.IndiceDeFormacion,
                cycleFormation = entrada.IndiceDeFormacion == EntradaJugador.SiguienteFormacion,
            };
            var mando = SystemAPI.GetComponent<SquadInputComponent>(escuadra);
            var intencion = SystemAPI.GetComponent<SquadPlayerOrderIntentComponent>(escuadra);
            var definicion = SystemAPI.HasComponent<SquadDefinitionComponent>(escuadra) ? SystemAPI.GetComponent<SquadDefinitionComponent>(escuadra) : default;
            if (!SquadPlayerCommandResolver.TryApply(comando, definicion, ref mando, ref intencion, SystemAPI.Time.ElapsedTime))
                return;
            SystemAPI.SetComponent(escuadra, mando);
            SystemAPI.SetComponent(escuadra, intencion);
        }

        /// <summary>
        /// Al tomar el jugador un héroe de la IA, su escuadra sigue como estaba: la intención del jugador empieza con la
        /// orden y la formación que la escuadra ya tenía, en vez de con las de por defecto.
        /// </summary>
        void HeredarEscuadra(Entity heroe)
        {
            if (!EntityManager.HasComponent<HeroSquadReference>(heroe))
                return;
            var escuadra = EntityManager.GetComponentData<HeroSquadReference>(heroe).squad;
            if (!EntityManager.Exists(escuadra) || !EntityManager.HasComponent<SquadPlayerOrderIntentComponent>(escuadra))
                return;
            var intencion = EntityManager.GetComponentData<SquadPlayerOrderIntentComponent>(escuadra);
            if (EntityManager.HasComponent<SquadStateComponent>(escuadra))
                intencion.orderType = EntityManager.GetComponentData<SquadStateComponent>(escuadra).currentOrder;
            if (EntityManager.HasComponent<FormationComponent>(escuadra))
                intencion.desiredFormation = EntityManager.GetComponentData<FormationComponent>(escuadra).currentFormation;
            if (EntityManager.HasComponent<SquadHoldPositionComponent>(escuadra))
                intencion.holdPosition = EntityManager.GetComponentData<SquadHoldPositionComponent>(escuadra).holdCenter;
            EntityManager.SetComponentData(escuadra, intencion);
        }

        /// <summary>
        /// G monta o desmonta: el jugador dice si quiere ir a caballo y HeroMountSystem hace el resto, como con los héroes
        /// de la IA (HeroAIMountSystem no decide por un héroe llevado en red). Si el juego lo baja del caballo (lo
        /// derriban, muere el caballo), deja de querer montar: no vuelve a subir solo.
        /// </summary>
        void Montura(Entity heroe, in EntradaJugador entrada, ref HeroeDeJugador jugador)
        {
            if (!SystemAPI.HasComponent<HeroMountComponent>(heroe) || !SystemAPI.HasComponent<HeroMountRequestComponent>(heroe))
                return;
            var estado = SystemAPI.GetComponent<HeroMountComponent>(heroe).state;
            bool montado = estado == HeroMountState.Mounted || estado == HeroMountState.Mounting;
            if (entrada.Montar.IsSet)
                jugador.QuiereMontar = estado == HeroMountState.OnFoot;
            else if (jugador.IbaMontado && estado == HeroMountState.OnFoot)
                jugador.QuiereMontar = false;
            jugador.IbaMontado = montado;
            SystemAPI.SetComponent(heroe, new HeroMountRequestComponent { ride = jugador.QuiereMontar });
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

        /// <summary>
        /// Los soldados de la escuadra del jugador llevan también su id, y mientras cumplen «mantener posición» sin haber
        /// llegado, el punto donde van a formar (lo mismo que calcula DestinationMarkerSystem para la escuadra local del
        /// juego de un jugador): su cliente pinta ahí el marcador de destino.
        /// </summary>
        void MarcarEscuadra(Entity heroe, int jugador)
        {
            if (!SystemAPI.HasComponent<HeroSquadReference>(heroe))
                return;
            var escuadra = SystemAPI.GetComponent<HeroSquadReference>(heroe).squad;
            if (!SystemAPI.Exists(escuadra) || !SystemAPI.HasBuffer<SquadUnitElement>(escuadra) || !SystemAPI.HasComponent<SquadStateComponent>(escuadra))
                return;
            var estado = SystemAPI.GetComponent<SquadStateComponent>(escuadra);
            bool mantiene = jugador != 0 && estado.currentOrder == SquadOrderType.HoldPosition
                && estado.currentState != SquadFSMState.Retreating && SystemAPI.HasComponent<SquadFormationAnchorComponent>(escuadra);
            var ancla = mantiene ? SystemAPI.GetComponent<SquadFormationAnchorComponent>(escuadra) : default;
            SquadHoldPositionComponent? parada = SystemAPI.HasComponent<SquadHoldPositionComponent>(escuadra)
                ? SystemAPI.GetComponent<SquadHoldPositionComponent>(escuadra) : null;
            using var soldados = SystemAPI.GetBuffer<SquadUnitElement>(escuadra).ToNativeArray(Allocator.Temp);
            foreach (var soldado in soldados)
            {
                var unidad = soldado.Value;
                if (!SystemAPI.Exists(unidad) || !SystemAPI.HasComponent<Espejo>(unidad))
                    continue;
                var fantasma = SystemAPI.GetComponent<Espejo>(unidad).Fantasma;
                var red = SystemAPI.GetComponent<SoldadoRed>(fantasma);
                if (red.Dueno != jugador)
                {
                    red.Dueno = jugador;
                    SystemAPI.SetComponent(fantasma, red);
                }
                var destino = new DestinoRed();
                if (mantiene && SystemAPI.HasComponent<UnitGridSlotComponent>(unidad) && SystemAPI.HasComponent<UnitFormationStateComponent>(unidad))
                {
                    var paso = SystemAPI.GetComponent<UnitFormationStateComponent>(unidad).State;
                    if (paso == UnitFormationState.Moving || paso == UnitFormationState.Waiting)
                    {
                        destino.Activo = 1;
                        destino.Punto = FormationPositionCalculator.DesiredPosition(SystemAPI.GetComponent<UnitGridSlotComponent>(unidad).localOffset,
                            estado, parada, ancla.position, ancla.rotation, true);
                    }
                }
                var antes = SystemAPI.GetComponent<DestinoRed>(fantasma);
                if (antes.Activo != destino.Activo || math.any(antes.Punto != destino.Punto))
                    SystemAPI.SetComponent(fantasma, destino);
            }
        }

        /// <summary>
        /// Con un ticket del motor, cada jugador lleva el héroe que le dio su token (BatallaDeTicketSystem lo apunta en
        /// <see cref="HeroeEnJuego.Dueno"/>): aquí se busca la entidad de ese héroe por su id del ticket. Quien no tiene
        /// héroe en el ticket solo mira.
        /// </summary>
        void RepartirDelTicket()
        {
            var batalla = BatallaDeTicket.Actual;
            if (batalla == null)
                return;
            var participantes = SystemAPI.QueryBuilder().WithAll<BattleParticipantComponent, HeroAITag, HeroAIDecision>().WithNone<HeroeDeJugador>().Build();
            using var heroes = participantes.ToEntityArray(Allocator.Temp);
            using var ids = participantes.ToComponentDataArray<BattleParticipantComponent>(Allocator.Temp);
            foreach (var (mando, jugador) in _sinHeroe)
            {
                var suyo = batalla.Heroes.Find(h => h.Dueno == jugador);
                if (suyo == null)
                    continue;
                for (int i = 0; i < heroes.Length; i++)
                {
                    if (ids[i].heroId.ToString() != suyo.Id)
                        continue;
                    EntityManager.AddComponentData(heroes[i], new HeroeDeJugador { Mando = mando, Jugador = jugador });
                    EntityManager.AddComponent<HeroNetworkMotion>(heroes[i]);
                    HeredarEscuadra(heroes[i]);
                    EntityManager.AddComponent<MandoConHeroe>(mando);
                    EntityManager.SetComponentEnabled<RusherBehaviorActive>(heroes[i], false);
                    EntityManager.SetComponentEnabled<BalancedBehaviorActive>(heroes[i], false);
                    Debug.Log($"[BatallaReal] El jugador {jugador} lleva a {suyo.Id}, el héroe de su token");
                    break;
                }
            }
        }

        /// <summary>Cierra la conexión de ese jugador, si sigue abierta.</summary>
        internal static void Cortar(EntityManager em, int jugador)
        {
            using var consulta = em.CreateEntityQuery(ComponentType.ReadOnly<NetworkId>());
            using var conexiones = consulta.ToEntityArray(Allocator.Temp);
            foreach (var conexion in conexiones)
                if (em.GetComponentData<NetworkId>(conexion).Value == jugador && !em.HasComponent<NetworkStreamRequestDisconnect>(conexion))
                    em.AddComponent<NetworkStreamRequestDisconnect>(conexion);
        }

        void Repartir()
        {
            _sinHeroe.Clear();
            foreach (var (dueno, mando) in SystemAPI.Query<RefRO<GhostOwner>>().WithAll<EntradaJugador>().WithNone<MandoConHeroe>().WithEntityAccess())
                _sinHeroe.Add((mando, dueno.ValueRO.NetworkId));
            if (_sinHeroe.Count == 0)
                return;

            var libres = SystemAPI.QueryBuilder().WithAll<HeroAITag, HeroAIDecision, TeamComponent, HeroLifeComponent, LocalTransform>().WithNone<HeroeDeJugador>().Build();
            using var heroes = libres.ToEntityArray(Allocator.Temp);
            using var bandos = libres.ToComponentDataArray<TeamComponent>(Allocator.Temp);
            using var poses = libres.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            // Lo que puede tapar la cámara: todo menos los propios soldados y héroes.
            int tapa = ~LayerMask.GetMask("Units", "Units_A", "Units_B", "Heroes", "Heroes_A", "Heroes_B");
            var tomados = new NativeHashSet<Entity>(8, Allocator.Temp);
            if (BatallaDeTicket.Actual != null || !string.IsNullOrEmpty(OpcionesRed.Ticket) || !string.IsNullOrEmpty(OpcionesRed.Lanzador))
            {
                RepartirDelTicket();
                tomados.Dispose();
                return;
            }
            foreach (var (mando, jugador) in _sinHeroe)
            {
                var quiere = jugador % 2 == 1 ? Team.TeamA : Team.TeamB;
                Entity elegido = Entity.Null, cualquiera = Entity.Null;
                // El que vuelve recupera su héroe, si sigue libre.
                string identidad = SystemAPI.HasComponent<IdentidadDeMando>(mando) ? SystemAPI.GetComponent<IdentidadDeMando>(mando).Token.ToString() : "";
                if (identidad.Length > 0 && _heroeDe.TryGetValue(identidad, out var suyo) && EntityManager.Exists(suyo))
                {
                    // Si aún lo tiene su conexión anterior (su proceso cayó y el servidor no se ha enterado), se corta esa
                    // y se espera a que el héroe quede libre.
                    if (EntityManager.HasComponent<HeroeDeJugador>(suyo))
                    {
                        Cortar(EntityManager, EntityManager.GetComponentData<HeroeDeJugador>(suyo).Jugador);
                        continue;
                    }
                    if (heroes.Contains(suyo) && !tomados.Contains(suyo))
                        elegido = suyo;
                }
                bool vuelve = elegido != Entity.Null;
                // Si no, de su bando, el primero al que la cámara del cliente ve sin una muralla o un tejado por medio (el
                // punto de salida de algunos está pegado a la muralla); si ninguno, el primero libre.
                for (int i = 0; i < heroes.Length && elegido == Entity.Null; i++)
                {
                    if (bandos[i].value != quiere || tomados.Contains(heroes[i]))
                        continue;
                    if (cualquiera == Entity.Null) cualquiera = heroes[i];
                    Vector3 cabeza = (Vector3)poses[i].Position + Vector3.up * 1.5f;
                    if (!Physics.Linecast(cabeza, (Vector3)poses[i].Position + CamaraDelCliente, tapa, QueryTriggerInteraction.Ignore))
                        elegido = heroes[i];
                }
                if (elegido == Entity.Null) elegido = cualquiera;
                // Los héroes aún no han salido (la batalla está cargando): se vuelve a intentar en el tick siguiente.
                if (elegido == Entity.Null)
                    continue;
                tomados.Add(elegido);
                if (identidad.Length > 0) _heroeDe[identidad] = elegido;
                EntityManager.AddComponentData(elegido, new HeroeDeJugador { Mando = mando, Jugador = jugador });
                EntityManager.AddComponent<HeroNetworkMotion>(elegido);
                HeredarEscuadra(elegido);
                EntityManager.AddComponent<MandoConHeroe>(mando);
                // Nadie más escribe su decisión: las dos conductas de la IA quedan apagadas.
                EntityManager.SetComponentEnabled<RusherBehaviorActive>(elegido, false);
                EntityManager.SetComponentEnabled<BalancedBehaviorActive>(elegido, false);
                Debug.Log(vuelve ? $"[BatallaReal] El jugador {jugador} vuelve y recupera su héroe"
                    : $"[BatallaReal] El jugador {jugador} lleva un héroe del bando {(quiere == Team.TeamA ? "atacante" : "defensor")}");
            }
            tomados.Dispose();
        }
    }
}
