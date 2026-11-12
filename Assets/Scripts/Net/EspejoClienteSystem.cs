using System.Collections.Generic;
using ConquestTactics.Animation;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace ConquestTactics.Net
{
    /// <summary>Cliente: este espejo ya tiene su modelo en la escena.</summary>
    public struct ConVisual : IComponentData { }

    /// <summary>Cliente: lo último visto del espejo de un héroe, para sacar de ahí su dirección y sus golpes nuevos.</summary>
    public struct HeroeVisto : IComponentData
    {
        public float3 Posicion;
        public byte Ataques;
    }

    /// <summary>
    /// Cliente de la batalla de verdad (<c>-batallaReal</c> también en el cliente): carga la escena de batalla y pinta cada
    /// soldado y cada héroe espejo con el mismo modelo y los mismos Animators que el juego. Para eso pone en el fantasma
    /// los componentes que leen <c>EntityVisualSync</c>, <c>UnitAnimationAdapter</c> y <c>RemoteHeroAnimationDriver</c> y
    /// los rellena con lo que llega por la red; el modelo no lleva agente de navegación ni colliders de golpe: aquí no se
    /// decide nada.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(DibujoRedSystem))]
    public partial class EspejoClienteSystem : SystemBase
    {
        bool _escenaPedida;
        VisualPrefabRegistry _registro;
        /// <summary>El modelo de cada héroe, para retirarlo mientras está caído y devolverlo al reaparecer.</summary>
        readonly Dictionary<Entity, GameObject> _heroes = new Dictionary<Entity, GameObject>();
        /// <summary>El marcador de destino de cada soldado de la escuadra propia que lo tiene.</summary>
        readonly Dictionary<Entity, Entity> _marcadores = new Dictionary<Entity, Entity>();
        readonly Dictionary<Entity, float3> _conDestino = new Dictionary<Entity, float3>();
        readonly List<Entity> _sobran = new List<Entity>();
        int _marcadoresAntes;
        Entity _camara, _interfaz, _datos, _reloj, _estado;
        byte _estadoAntes = 255;
        double _proximoResumen;
        /// <summary>Hay héroe propio y la cámara del juego lo sigue: DibujoRedSystem no toca la cámara.</summary>
        public static bool CamaraDelJuego { get; private set; }
        bool _fotoDeMarcadores;

        protected override void OnCreate() => Enabled = OpcionesRed.BatallaReal > 0 && !Application.isBatchMode;

        protected override void OnUpdate()
        {
            if (!_escenaPedida)
            {
                _escenaPedida = true;
                BattleBootstrapRequests.NoLocalHero = true;
                ReservaDeProyectiles.Asegurar();
                SceneManager.LoadScene(SceneTransitionService.SceneNames.Battle);
                return;
            }
            if (_registro == null)
                _registro = Object.FindFirstObjectByType<VisualPrefabRegistry>();
            if (_registro == null)
                return;

            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);
            CrearSoldados(ecb);
            CrearHeroes(ecb);
            ecb.Playback(EntityManager);
            AdelantarAlPropio();
            AnimarSoldados();
            AnimarHeroes();
            MarcarDestinos();
            Unicos();
            CamaraEInterfaz();
            Partida();
        }

        /// <summary>El modelo, en la capa de su bando y sin autoridad: ni navega ni golpea. Los golpes los decide el servidor.</summary>
        GameObject Instanciar(GameObject prefab, in LocalTransform pose, string capa, Entity entidad)
        {
            var visual = Object.Instantiate(prefab, pose.Position, pose.Rotation);
            visual.name = $"Espejo_{entidad.Index}";
            int numero = LayerMask.NameToLayer(capa);
            foreach (var hijo in visual.GetComponentsInChildren<Transform>(true))
                hijo.gameObject.layer = math.max(0, numero);
            foreach (var golpe in visual.GetComponentsInChildren<WeaponHitboxBehaviour>(true)) Object.Destroy(golpe);
            foreach (var escudo in visual.GetComponentsInChildren<ShieldHitboxBehaviour>(true)) Object.Destroy(escudo);
            var agente = visual.GetComponent<NavMeshAgent>();
            if (agente != null) agente.enabled = false;
            return visual;
        }

        void CrearSoldados(EntityCommandBuffer ecb)
        {
            foreach (var (soldado, modelo, animacion, pose, entidad) in SystemAPI
                         .Query<RefRO<SoldadoRed>, RefRO<VisualRed>, RefRO<AnimacionRed>, RefRO<LocalTransform>>()
                         .WithNone<ConVisual, AparienciaRed>().WithEntityAccess())
            {
                // El modelo llega con el primer snapshot completo; hasta entonces el nombre está vacío.
                if (modelo.ValueRO.Prefab.IsEmpty)
                    continue;
                var prefab = _registro.GetPrefab(modelo.ValueRO.Prefab.ToString(), true)
                             ?? _registro.GetDefaultUnitPrefab((SquadType)modelo.ValueRO.TipoDeEscuadra, true);
                if (prefab == null)
                    continue;
                var visual = Instanciar(prefab, pose.ValueRO, soldado.ValueRO.Bando == 1 ? "Units_B" : "Units_A", entidad);
                ecb.AddComponent<ConVisual>(entidad);
                // El bando, para que un proyectil de aquí no se pare en los suyos; y si es de caballería, su caballo.
                ecb.AddComponent(entidad, new TeamComponent { value = soldado.ValueRO.Bando == 1 ? Team.TeamB : Team.TeamA });
                if (animacion.ValueRO.Montura != 0)
                    ecb.AddComponent<MountComponent>(entidad);
                ecb.AddComponent(entidad, new UnitAnimationMovementComponent { PreviousPosition = pose.ValueRO.Position });
                ecb.AddComponent<UnitCombatComponent>(entidad);
                ecb.AddComponent<RangedAttackStateComponent>(entidad);
                ecb.AddComponent<ResilienceComponent>(entidad);
                if ((animacion.ValueRO.Banderas & AnimacionRed.ConMarcha) != 0)
                    ecb.AddComponent<UnitMovementGearComponent>(entidad);
                VisualSyncUtility.SetupVisualSync(visual).SetHeroEntity(entidad);
            }
        }

        void CrearHeroes(EntityCommandBuffer ecb)
        {
            foreach (var (soldado, modelo, aspecto, pose, entidad) in SystemAPI
                         .Query<RefRO<SoldadoRed>, RefRO<VisualRed>, RefRO<AparienciaRed>, RefRO<LocalTransform>>()
                         .WithNone<ConVisual>().WithEntityAccess())
            {
                if (modelo.ValueRO.Prefab.IsEmpty || aspecto.ValueRO.Texto.IsEmpty)
                    continue;
                var prefab = _registro.GetPrefab(modelo.ValueRO.Prefab.ToString(), true);
                var apariencia = AparienciaRed.Leer(aspecto.ValueRO.Texto.ToString());
                if (prefab == null || apariencia == null)
                    continue;
                var visual = Instanciar(prefab, pose.ValueRO, soldado.ValueRO.Bando == 1 ? "Heroes_B" : "Heroes_A", entidad);
                bool vestido;
                try
                {
                    vestido = HeroVisualAppearanceSystem.ApplyHeroVisualCustomization(visual, apariencia);
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[Red] No se pudo vestir al héroe {entidad.Index}: {e.Message}");
                    vestido = false;
                }
                if (!vestido)
                {
                    // Aún no están cargados los datos del avatar: se reintenta en el fotograma siguiente.
                    visual.SetActive(false);
                    Object.Destroy(visual);
                    continue;
                }
                ecb.AddComponent<ConVisual>(entidad);
                ecb.AddComponent(entidad, new HeroeVisto { Posicion = pose.ValueRO.Position });
                // HeroMoveIntent es lo que le dice a EntityVisualSync que es un héroe remoto y le pone su RemoteHeroAnimationDriver.
                ecb.AddComponent<HeroMoveIntent>(entidad);
                ecb.AddComponent<HeroNetworkMotion>(entidad);
                ecb.AddComponent<HeroStatsComponent>(entidad);
                ecb.AddComponent<HeroAnimationComponent>(entidad);
                ecb.AddComponent<HeroCombatComponent>(entidad);
                ecb.AddComponent<HeroDeathPresentationComponent>(entidad);
                ecb.AddComponent<ResilienceComponent>(entidad);
                ecb.AddComponent(entidad, new TeamComponent { value = soldado.ValueRO.Bando == 1 ? Team.TeamB : Team.TeamA });
                // Con HeroLifeComponent, EntityVisualSync le pone al modelo su HeroMountPresenter, que saca el caballo de
                // HeroMountComponent, la clase del héroe y la velocidad de HeroMotorStateComponent.
                ecb.AddComponent(entidad, new HeroLifeComponent { isAlive = true });
                ecb.AddComponent<HeroMountComponent>(entidad);
                ecb.AddComponent<HeroMotorStateComponent>(entidad);
                ecb.AddComponent(entidad, new HeroClassComponent { heroClass = (HeroClass)modelo.ValueRO.TipoDeEscuadra });
                _heroes[entidad] = visual;
                // Los componentes llegan al reproducir el búfer, antes del Start del modelo, que es cuando los mira.
                VisualSyncUtility.SetupVisualSync(visual).SetHeroEntity(entidad);
            }
        }

        /// <summary>
        /// El héroe que lleva este jugador no espera al servidor: su fantasma se pone donde está su mando, que el cliente
        /// predice con la entrada de ahora mismo. Los modelos leen la posición del fantasma en el fotograma siguiente,
        /// antes de que llegue otra vez la del servidor.
        /// </summary>
        void AdelantarAlPropio()
        {
            if (!SystemAPI.TryGetSingleton<Unity.NetCode.NetworkId>(out var yo))
                return;
            LocalTransform? mando = null;
            foreach (var (pose, movil) in SystemAPI.Query<RefRO<LocalTransform>, RefRO<MandoMovil>>().WithAll<Unity.NetCode.GhostOwnerIsLocal>())
                if (movil.ValueRO.Activo != 0)
                    mando = pose.ValueRO;
            if (!mando.HasValue)
                return;
            foreach (var (pose, soldado) in SystemAPI.Query<RefRW<LocalTransform>, RefRO<SoldadoRed>>().WithAll<ConVisual>())
                if (soldado.ValueRO.Dueno == yo.Value && soldado.ValueRO.Tipo == SoldadoRed.Heroe)
                    pose.ValueRW = mando.Value;
        }

        /// <summary>
        /// Los marcadores de destino de la escuadra propia: donde va a formar cada soldado mientras cumple «mantener
        /// posición». El punto lo calcula el servidor (<see cref="DestinoRed"/>); aquí se pinta con el mismo prefab que
        /// usa el juego (DestinationMarkerSystem), que llega en la subescena de la batalla.
        /// </summary>
        void MarcarDestinos()
        {
            Entity prefab = Entity.Null;
            foreach (var marcador in SystemAPI.Query<RefRO<DestinationMarkerPrefabComponent>>())
            {
                prefab = marcador.ValueRO.markerPrefab;
                break;
            }
            if (prefab == Entity.Null || !SystemAPI.TryGetSingleton<Unity.NetCode.NetworkId>(out var yo))
                return;

            _conDestino.Clear();
            foreach (var (destino, soldado, entidad) in SystemAPI.Query<RefRO<DestinoRed>, RefRO<SoldadoRed>>().WithEntityAccess())
                if (destino.ValueRO.Activo != 0 && soldado.ValueRO.Dueno == yo.Value)
                    _conDestino[entidad] = destino.ValueRO.Punto;

            // Fuera los de quien ya llegó, murió o dejó de mantener; dentro o movidos los demás.
            _sobran.Clear();
            foreach (var par in _marcadores)
                if (!_conDestino.ContainsKey(par.Key) || !EntityManager.Exists(par.Value))
                    _sobran.Add(par.Key);
            foreach (var soldado in _sobran)
            {
                if (EntityManager.Exists(_marcadores[soldado])) EntityManager.DestroyEntity(_marcadores[soldado]);
                _marcadores.Remove(soldado);
            }
            foreach (var par in _conDestino)
            {
                if (!_marcadores.TryGetValue(par.Key, out var marca))
                    _marcadores[par.Key] = marca = EntityManager.Instantiate(prefab);
                EntityManager.SetComponentData(marca, LocalTransform.FromPosition(par.Value));
            }
            if (_marcadores.Count != _marcadoresAntes)
            {
                // Al registro solo cuando aparecen o se van todos; y, con -captura, una foto con ellos a la vista.
                if (_marcadores.Count == 0 || _marcadoresAntes == 0)
                    Debug.Log($"[Red] Marcadores de destino de la escuadra propia: {_marcadores.Count}");
                _marcadoresAntes = _marcadores.Count;
            }
            if (!_fotoDeMarcadores && _marcadores.Count >= 6 && SystemAPI.Time.ElapsedTime > 12d && !string.IsNullOrEmpty(OpcionesRed.Captura))
            {
                _fotoDeMarcadores = true;
                ScreenCapture.CaptureScreenshot(OpcionesRed.Captura.Replace(".png", "-marcadores.png"));
            }
        }

        /// <summary>
        /// La cámara y la interfaz del juego, atadas al héroe que lleva este jugador. La cámara (HeroCameraController) sigue
        /// a la entidad que diga CameraTargetComponent: aquí, el fantasma del héroe propio, con lo poco que esa cámara lee de
        /// él. La interfaz (HUDController, SkillBarController) lee UIHeroBattleDataComponent: aquí se rellena con lo que el
        /// servidor manda en el mando (<see cref="InterfazRed"/>), en vez de con UIBattleDataSystem.
        /// </summary>
        void CamaraEInterfaz()
        {
            if (!SystemAPI.TryGetSingleton<Unity.NetCode.NetworkId>(out var yo))
                return;
            Entity propio = Entity.Null;
            bool vivo = true;
            foreach (var (soldado, animacion, entidad) in SystemAPI.Query<RefRO<SoldadoRed>, RefRO<AnimacionRed>>().WithAll<ConVisual>().WithEntityAccess())
                if (soldado.ValueRO.Dueno == yo.Value && soldado.ValueRO.Tipo == SoldadoRed.Heroe)
                {
                    propio = entidad;
                    vivo = (animacion.ValueRO.Banderas & AnimacionRed.Muerto) == 0;
                }
            CamaraDelJuego = propio != Entity.Null;
            if (propio != Entity.Null)
            {
                if (!EntityManager.HasComponent<HeroVisualInstance>(propio) && _heroes.TryGetValue(propio, out var visual) && visual != null)
                    EntityManager.AddComponentData(propio, new HeroVisualInstance { visualInstanceId = visual.GetEntityId() });
                if (_camara == Entity.Null || !EntityManager.Exists(_camara))
                {
                    // Los mismos valores que CameraBootstrapSystem pone para el héroe local.
                    _camara = EntityManager.CreateEntity();
                    EntityManager.AddComponentData(_camara, new CameraTargetComponent { followTarget = propio, zoomLevel = 2f, offset = new float3(0f, 1.8f, 1f) });
                    EntityManager.AddComponentData(_camara, new CameraSettingsComponent { minZoom = 1f, maxZoom = 10f, zoomSpeed = 10f, rotationSensitivity = 35f });
                    EntityManager.AddComponentData(_camara, new CameraStateComponent());
                }
                else if (EntityManager.GetComponentData<CameraTargetComponent>(_camara).followTarget != propio)
                {
                    var objetivo = EntityManager.GetComponentData<CameraTargetComponent>(_camara);
                    objetivo.followTarget = propio;
                    EntityManager.SetComponentData(_camara, objetivo);
                }
            }

            foreach (var (red, casillas) in SystemAPI.Query<RefRO<InterfazRed>, DynamicBuffer<HabilidadRed>>().WithAll<Unity.NetCode.GhostOwnerIsLocal>())
            {
                var r = red.ValueRO;
                var ui = new UIHeroBattleDataComponent
                {
                    currentHealth = r.Vida, maxHealth = r.VidaMaxima, currentStamina = r.Resistencia, maxStamina = r.ResistenciaMaxima,
                    heroAlive = r.Vivo != 0, combo = r.Combo, ultimateComboCost = r.CosteDeDefinitiva, unaffordableSlots = r.SinPagar,
                    aliveUnits = r.Soldados, totalUnits = r.SoldadosTotales, currentFormation = (FormationType)r.Formacion,
                    isInCaptureZone = r.EnZona != 0, captureProgress = r.Captura,
                };
                // Lo que la interfaz saca del DataContainer del juego: de qué bando es el jugador y qué tropa lleva.
                BattleBootstrapRequests.NetworkTeam = r.Bando;
                var datos = EntityManager.GetComponentData<DataContainerComponent>(_datos);
                if (datos.teamID != r.Bando || datos.selectedSquadBaseID != r.Escuadra)
                {
                    datos.playerID = yo.Value;
                    datos.teamID = r.Bando;
                    datos.selectedSquadBaseID = r.Escuadra;
                    datos.isReady = true;
                    EntityManager.SetComponentData(_datos, datos);
                }
                ui.skills.Length = UIHeroBattleDataComponent.SkillSlotCount;
                for (int i = 0; i < casillas.Length && i < ui.skills.Length; i++)
                    ui.skills[i] = new UISkillSlotData { skillId = casillas[i].Id, cooldown = casillas[i].Recarga, remaining = casillas[i].Queda };
                EntityManager.SetComponentData(_interfaz, ui);
                break;
            }
        }

        /// <summary>
        /// Las entidades sueltas donde la interfaz del juego busca sus datos (en el juego de un jugador las crean sistemas
        /// de Game.ECS que aquí no corren). Se crean una vez, fuera de cualquier recorrido: crear una entidad en mitad de
        /// uno lo invalida.
        /// </summary>
        void Unicos()
        {
            if (_interfaz != Entity.Null && EntityManager.Exists(_interfaz))
                return;
            _interfaz = EntityManager.CreateEntity(typeof(UIHeroBattleDataComponent));
            _datos = EntityManager.CreateEntity(typeof(DataContainerComponent));
            EntityManager.AddBuffer<SquadIdMapElement>(_datos);
            _reloj = EntityManager.CreateEntity(typeof(BattleClockComponent));
            _estado = EntityManager.CreateEntity(typeof(MatchStateComponent));
        }

        /// <summary>
        /// La partida que manda el servidor, volcada donde la interfaz del juego la busca: el reloj en
        /// BattleClockComponent, el estado y el ganador en MatchStateComponent, y cada zona en la entidad de zona que la
        /// escena creó aquí (se casan por la posición). Los indicadores de zona, el reloj y el panel de victoria o derrota
        /// del juego hacen el resto.
        /// </summary>
        void Partida()
        {
            foreach (var (partida, zonas) in SystemAPI.Query<RefRO<PartidaRed>, DynamicBuffer<ZonaRed>>())
            {
                var p = partida.ValueRO;
                EntityManager.SetComponentData(_reloj, new BattleClockComponent { remainingSeconds = p.Reloj, expired = p.Agotado != 0 });
                bool acabada = p.Estado == (byte)MatchState.EndMatch;
                EntityManager.SetComponentData(_estado, new MatchStateComponent
                    { currentState = (MatchState)p.Estado, winnerTeam = p.Ganador, victoryConditionMet = acabada });
                if (p.Estado != _estadoAntes)
                {
                    _estadoAntes = p.Estado;
                    Debug.Log(System.FormattableString.Invariant(
                        $"[Red] Partida: {(MatchState)p.Estado}, reloj {p.Reloj:F0} s, gana {p.Ganador}, {zonas.Length} zonas, mi bando {BattleBootstrapRequests.NetworkTeam}"));
                }

                bool resumir = SystemAPI.Time.ElapsedTime >= _proximoResumen;
                if (resumir) _proximoResumen = SystemAPI.Time.ElapsedTime + 10d;
                var vistas = new Unity.Collections.NativeList<ZonaRed>(8, Unity.Collections.Allocator.Temp);
                using var copia = zonas.ToNativeArray(Unity.Collections.Allocator.Temp);
                var locales = SystemAPI.QueryBuilder().WithAll<ZoneTriggerComponent, LocalTransform>().Build();
                using var entidades = locales.ToEntityArray(Unity.Collections.Allocator.Temp);
                using var poses = locales.ToComponentDataArray<LocalTransform>(Unity.Collections.Allocator.Temp);
                for (int i = 0; i < entidades.Length; i++)
                    foreach (var z in copia)
                    {
                        if (math.distancesq(z.Posicion, poses[i].Position) > 4f)
                            continue;
                        var zona = EntityManager.GetComponentData<ZoneTriggerComponent>(entidades[i]);
                        if (zona.teamOwner != z.Dueno)
                            Debug.Log(System.FormattableString.Invariant(
                                $"[Red] La zona de ({z.Posicion.x:F0}, {z.Posicion.z:F0}) pasa del equipo {zona.teamOwner} al {z.Dueno}"));
                        zona.teamOwner = z.Dueno;
                        zona.isActive = (z.Banderas & ZonaRed.Activa) != 0;
                        zona.isLocked = (z.Banderas & ZonaRed.Bloqueada) != 0;
                        EntityManager.SetComponentData(entidades[i], zona);
                        if (EntityManager.HasComponent<CapturePointProgressComponent>(entidades[i]))
                        {
                            var captura = EntityManager.GetComponentData<CapturePointProgressComponent>(entidades[i]);
                            captura.captureProgress = z.Progreso;
                            captura.capturingTeam = z.Equipo;
                            captura.isContested = (z.Banderas & ZonaRed.Disputada) != 0;
                            captura.isBeingCaptured = (z.Banderas & ZonaRed.Capturandose) != 0;
                            EntityManager.SetComponentData(entidades[i], captura);
                        }
                        else if (EntityManager.HasComponent<SupplyPointComponent>(entidades[i]))
                        {
                            var suministro = EntityManager.GetComponentData<SupplyPointComponent>(entidades[i]);
                            suministro.captureProgress = z.Progreso;
                            suministro.currentTeam = z.Equipo;
                            suministro.isContested = (z.Banderas & ZonaRed.Disputada) != 0;
                            suministro.isCapturing = (z.Banderas & ZonaRed.Capturandose) != 0;
                            EntityManager.SetComponentData(entidades[i], suministro);
                        }
                        break;
                    }
                // Al registro, lo que ha quedado escrito en las zonas de aquí (no lo que llegó): para compararlo con el servidor.
                if (resumir)
                {
                    for (int i = 0; i < entidades.Length; i++)
                    {
                        var zona = EntityManager.GetComponentData<ZoneTriggerComponent>(entidades[i]);
                        var v = new ZonaRed { Posicion = poses[i].Position, Dueno = (byte)zona.teamOwner };
                        if (zona.isActive) v.Banderas |= ZonaRed.Activa;
                        if (zona.isLocked) v.Banderas |= ZonaRed.Bloqueada;
                        if (EntityManager.HasComponent<CapturePointProgressComponent>(entidades[i]))
                        {
                            var captura = EntityManager.GetComponentData<CapturePointProgressComponent>(entidades[i]);
                            v.Progreso = captura.captureProgress;
                            v.Equipo = (byte)captura.capturingTeam;
                            if (captura.isContested) v.Banderas |= ZonaRed.Disputada;
                            if (captura.isBeingCaptured) v.Banderas |= ZonaRed.Capturandose;
                        }
                        else if (EntityManager.HasComponent<SupplyPointComponent>(entidades[i]))
                        {
                            var suministro = EntityManager.GetComponentData<SupplyPointComponent>(entidades[i]);
                            v.Progreso = suministro.captureProgress;
                            v.Equipo = (byte)suministro.currentTeam;
                            if (suministro.isContested) v.Banderas |= ZonaRed.Disputada;
                            if (suministro.isCapturing) v.Banderas |= ZonaRed.Capturandose;
                        }
                        vistas.Add(v);
                    }
                    Debug.Log(System.FormattableString.Invariant($"[Red] Zonas (reloj {p.Reloj:F0} s): ") + ZonaRed.Resumen(vistas.AsArray()));
                }
                vistas.Dispose();
                break;
            }
        }

        void AnimarSoldados()
        {
            foreach (var (animacion, pose, movimiento, combate, distancia, aguante, entidad) in SystemAPI
                         .Query<RefRO<AnimacionRed>, RefRO<LocalTransform>, RefRW<UnitAnimationMovementComponent>, RefRW<UnitCombatComponent>,
                             RefRW<RangedAttackStateComponent>, RefRW<ResilienceComponent>>()
                         .WithAll<ConVisual>().WithEntityAccess())
            {
                var a = animacion.ValueRO;
                // La dirección no viaja: sale de cómo se mueve el fantasma ya interpolado.
                float3 paso = pose.ValueRO.Position - movimiento.ValueRO.PreviousPosition;
                paso.y = 0f;
                movimiento.ValueRW.PreviousPosition = pose.ValueRO.Position;
                if (math.lengthsq(paso) > 1e-8f)
                    movimiento.ValueRW.MovementDirection = math.normalize(paso);
                movimiento.ValueRW.CurrentSpeed = a.Velocidad;
                movimiento.ValueRW.MaxSpeed = a.VelocidadBase;
                movimiento.ValueRW.RequestedSpeed = a.VelocidadPedida;
                movimiento.ValueRW.IsMoving = (a.Banderas & AnimacionRed.Moviendose) != 0;
                movimiento.ValueRW.CurrentStance = (UnitStance)a.Postura;
                movimiento.ValueRW.SlotRow = a.Fila;
                movimiento.ValueRW.SlotRole = a.Papel;
                combate.ValueRW.isAttacking = (a.Banderas & AnimacionRed.Atacando) != 0;
                distancia.ValueRW.isFiring = (a.Banderas & AnimacionRed.Disparando) != 0;
                distancia.ValueRW.isReloading = (a.Banderas & AnimacionRed.Recargando) != 0;
                aguante.ValueRW.reaction = (HitReaction)a.Reaccion;
                aguante.ValueRW.reactionRevision = a.RevisionDeReaccion;
                if (SystemAPI.HasComponent<UnitMovementGearComponent>(entidad))
                    SystemAPI.SetComponent(entidad, new UnitMovementGearComponent
                        { gear = (a.Banderas & AnimacionRed.Corre) != 0 ? MovementGear.Run : MovementGear.Walk });
            }
        }

        void AnimarHeroes()
        {
            foreach (var (animacion, pose, visto, marcha, heroe, combate, muerte, entidad) in SystemAPI
                         .Query<RefRO<AnimacionRed>, RefRO<LocalTransform>, RefRW<HeroeVisto>, RefRW<HeroNetworkMotion>,
                             RefRW<HeroAnimationComponent>, RefRW<HeroCombatComponent>, RefRW<HeroDeathPresentationComponent>>()
                         .WithAll<ConVisual>().WithEntityAccess())
            {
                var a = animacion.ValueRO;
                float3 paso = pose.ValueRO.Position - visto.ValueRO.Posicion;
                paso.y = 0f;
                visto.ValueRW.Posicion = pose.ValueRO.Position;
                float3 direccion = math.lengthsq(paso) > 1e-8f ? math.normalize(paso) : math.normalizesafe(marcha.ValueRO.velocity);
                marcha.ValueRW.velocity = (a.Banderas & AnimacionRed.Moviendose) != 0 ? direccion * a.Velocidad : float3.zero;
                marcha.ValueRW.requestedSpeed = a.VelocidadPedida;
                heroe.ValueRW.attackSpeed = a.VelocidadDeAtaque;
                // Cada golpe nuevo del servidor dispara aquí la animación; RemoteHeroAnimationDriver baja el disparador al usarlo.
                if (a.Ataques != visto.ValueRO.Ataques)
                {
                    visto.ValueRW.Ataques = a.Ataques;
                    heroe.ValueRW.triggerAttack = true;
                }
                combate.ValueRW.isAttacking = (a.Banderas & AnimacionRed.Atacando) != 0;
                muerte.ValueRW.isDead = (a.Banderas & AnimacionRed.Muerto) != 0;
                muerte.ValueRW.deathRevision = a.RevisionDeMuerte;
                SystemAPI.SetComponent(entidad, new HeroLifeComponent { isAlive = (a.Banderas & AnimacionRed.Muerto) == 0 });
                // El caballo: HeroMountPresenter lo saca o lo quita cuando cambia la revisión.
                var montura = SystemAPI.GetComponent<HeroMountComponent>(entidad);
                montura.state = (HeroMountState)a.Montura;
                montura.revision = a.RevisionDeMontura;
                montura.lastEnd = (HeroMountEnd)a.FinDeMontura;
                SystemAPI.SetComponent(entidad, montura);
                SystemAPI.SetComponent(entidad, new HeroMotorStateComponent { velocity = marcha.ValueRO.velocity, isGrounded = true });
                var estadisticas = SystemAPI.GetComponent<HeroStatsComponent>(entidad);
                estadisticas.baseSpeed = a.VelocidadBase;
                SystemAPI.SetComponent(entidad, estadisticas);
                var aguante = SystemAPI.GetComponent<ResilienceComponent>(entidad);
                aguante.reaction = (HitReaction)a.Reaccion;
                aguante.reactionRevision = a.RevisionDeReaccion;
                SystemAPI.SetComponent(entidad, aguante);
                // Caído y con el cuerpo retirado en el servidor: aquí también, hasta que reaparezca.
                if (_heroes.TryGetValue(entidad, out var visual) && visual != null)
                {
                    bool aLaVista = (a.Banderas & AnimacionRed.Oculto) == 0;
                    if (visual.activeSelf != aLaVista) visual.SetActive(aLaVista);
                }
            }
        }
    }
}
