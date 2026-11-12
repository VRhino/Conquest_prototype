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
    }

    /// <summary>Servidor: este mando ya tiene héroe.</summary>
    public struct MandoConHeroe : IComponentData { }

    /// <summary>
    /// Mueve el mando de cada jugador con su entrada, dentro de la predicción de Netcode: el cliente lo adelanta y el
    /// servidor lo confirma con las mismas cuentas. El paso se pega a la malla de navegación de la escena de batalla, que
    /// es la misma en los dos lados; así el héroe responde al instante y no atraviesa murallas. Un cliente sin el mapa
    /// cargado (los bots sin pantalla) se mueve libre y el servidor lo corrige.
    /// ponytail: sin aceleración, sin gastar resistencia al correr y sin chocar con soldados (eso lo corrige el servidor).
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    public partial class MovimientoMandoSystem : SystemBase
    {
        const float Margen = 1f;

        protected override void OnUpdate()
        {
            float dt = SystemAPI.Time.DeltaTime;
            foreach (var (pose, entrada, movil) in SystemAPI.Query<RefRW<LocalTransform>, RefRO<EntradaJugador>, RefRO<MandoMovil>>().WithAll<Simulate>())
            {
                float2 mover = entrada.ValueRO.Mover;
                if (movil.ValueRO.Activo == 0 || math.lengthsq(mover) < 1e-4f)
                    continue;
                if (math.lengthsq(mover) > 1f) mover = math.normalize(mover);
                float3 direccion = new float3(mover.x, 0f, mover.y);
                float velocidad = entrada.ValueRO.Correr != 0 ? movil.ValueRO.VelocidadAlCorrer : movil.ValueRO.Velocidad;
                float3 destino = pose.ValueRO.Position + direccion * velocidad * dt;
                // Con mapa: el paso acaba en el punto de la malla más cercano (junto a una muralla, resbala por su borde).
                if (NavMesh.SamplePosition(pose.ValueRO.Position, out _, 2f, NavMesh.AllAreas))
                {
                    if (!NavMesh.SamplePosition(destino, out var toque, Margen, NavMesh.AllAreas))
                        continue;
                    destino = toque.position;
                }
                pose.ValueRW.Position = destino;
                pose.ValueRW.Rotation = quaternion.LookRotationSafe(direccion, math.up());
            }
        }
    }

    /// <summary>
    /// Servidor de la batalla de verdad: da a cada jugador que entra un héroe de la batalla. El héroe va donde va el mando
    /// del jugador (<see cref="MovimientoMandoSystem"/>, predicho) y lo demás de su entrada (<see cref="EntradaJugador"/>)
    /// se pone donde la IA de ese héroe pondría su decisión (<c>HeroAIDecision</c>): los mismos golpes, habilidades y
    /// órdenes a la escuadra que un héroe de la IA. Si el juego mueve al héroe por su cuenta (un empujón, reaparecer), el
    /// mando se va con él y el cliente recibe la corrección. Si el jugador se va, el héroe vuelve a la IA.
    /// ponytail: el héroe se da por orden de llegada, alternando bando; con un ticket del motor lo dirá su token.
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
                float3 posicion = SystemAPI.GetComponent<LocalTransform>(heroe).Position;
                float2 mover = entrada.Mover;
                if (math.lengthsq(mover) > 1f) mover = math.normalize(mover);

                float3 velocidad = Llevar(heroe, mando, ref jugador.ValueRW, posicion, vida.ValueRO.isAlive, entrada, mover);

                // El héroe no decide adónde ir (va con el mando); el resto es lo que la IA pondría en su decisión.
                var d = new HeroAIDecision { shouldAttack = entrada.Atacar != 0 };
                if (entrada.Habilidad.IsSet)
                {
                    d.useSkill = true;
                    d.skillSlot = (SkillSlot)entrada.Ranura;
                    jugador.ValueRW.Habilidades++;
                }
                if (entrada.Orden.IsSet)
                {
                    d.squadOrder = (SquadOrderType)entrada.TipoDeOrden;
                    d.squadOrderPosition = math.lengthsq(entrada.PuntoDeOrden) > 0f ? entrada.PuntoDeOrden : posicion;
                    d.hasNewSquadOrder = true;
                    jugador.ValueRW.Ordenes++;
                }
                decision.ValueRW = d;
                Marcar(heroe, jugador.ValueRO.Jugador);
                MarcarEscuadra(heroe, jugador.ValueRO.Jugador);
                Interfaz(heroe, mando);
                if (avisar)
                {
                    int golpes = SystemAPI.HasComponent<Espejo>(heroe)
                        ? SystemAPI.GetComponent<AnimacionRed>(SystemAPI.GetComponent<Espejo>(heroe).Fantasma).Ataques : 0;
                    Debug.Log("[BatallaReal] " + Escuadra(heroe, posicion));
                    Debug.Log(System.FormattableString.Invariant(
                        $"[BatallaReal] Héroe del jugador {jugador.ValueRO.Jugador} en ({posicion.x:F1}, {posicion.z:F1}), entrada ({mover.x:F2}, {mover.y:F2}), velocidad {math.length(velocidad):F1}, ataca {entrada.Atacar}, golpes lanzados {golpes} (cuenta de 0 a 255), órdenes recibidas {jugador.ValueRO.Ordenes}, habilidades {jugador.ValueRO.Habilidades}"));
                }
            }

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
            var agente = EntityManager.HasComponent<NavMeshAgent>(heroe) ? EntityManager.GetComponentObject<NavMeshAgent>(heroe) : null;
            if (agente == null || !agente.enabled || !agente.isOnNavMesh)
                movil.Activo = 0;
            SystemAPI.SetComponent(mando, movil);

            var poseMando = SystemAPI.GetComponent<LocalTransform>(mando);
            float3 velocidad = float3.zero;
            bool movidoPorElJuego = !jugador.Colocado || math.distance(posicion.xz, jugador.Puesto.xz) > Desvio;
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
                if (math.lengthsq(mover) > 1e-4f)
                {
                    agente.transform.rotation = poseMando.Rotation;
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
                    if (SystemAPI.HasComponent<FormationComponent>(escuadra))
                        red.Formacion = (byte)SystemAPI.GetComponent<FormationComponent>(escuadra).currentFormation;
                }
            }
            SystemAPI.SetComponent(mando, red);
            var casillas = SystemAPI.GetBuffer<HabilidadRed>(mando);
            casillas.ResizeUninitialized(ui.skills.Length);
            for (int i = 0; i < ui.skills.Length; i++)
                casillas[i] = new HabilidadRed { Id = ui.skills[i].skillId, Recarga = ui.skills[i].cooldown, Queda = ui.skills[i].remaining };
        }

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
            string pedida = SystemAPI.HasComponent<SquadAIOrderIntentComponent>(escuadra)
                ? SystemAPI.GetComponent<SquadAIOrderIntentComponent>(escuadra).suggestedOrder.ToString() : "?";
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
            foreach (var (mando, jugador) in _sinHeroe)
            {
                var quiere = jugador % 2 == 1 ? Team.TeamA : Team.TeamB;
                // De su bando, el primero al que la cámara del cliente ve sin una muralla o un tejado por medio (el punto de
                // salida de algunos está pegado a la muralla); si ninguno, el primero libre.
                Entity elegido = Entity.Null, cualquiera = Entity.Null;
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
                EntityManager.AddComponentData(elegido, new HeroeDeJugador { Mando = mando, Jugador = jugador });
                EntityManager.AddComponent<HeroNetworkMotion>(elegido);
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
