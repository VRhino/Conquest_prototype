using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ConquestTactics.Net
{
    /// <summary>
    /// Cliente: escribe la entrada de su héroe. Con teclado, WASD relativo a la cámara; con <c>-bot</c>, un ocho
    /// para medir sin nadie delante.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(GhostInputSystemGroup))]
    public partial class EntradaClienteSystem : SystemBase
    {
        /// <summary>Tiempo máximo entre dos toques para que cuenten como doble (el de SquadControlSystem).</summary>
        const double DobleToque = 0.5d;

        double _proximaOrdenDeBot, _proximaHabilidadDeBot, _proximaFormacionDeBot, _proximaMonturaDeBot = 12d;
        double _ultimaC = -10d, _ultimaX = -10d, _dentroDesde = -1d;
        // Los datos de cada InputEvent (la escuadra pedida, la orden y su punto, la ranura, la formación) van en todas las entradas con su último
        // valor: el aviso llega al servidor en el tick que toca, pero los demás campos son los del último fotograma de ese tick, y uno sin ellos
        // los mandaría a 0.
        int _ultimaPedida;
        byte _ultimoTipoDeOrden, _ultimaRanura, _ultimaFormacion;
        float3 _ultimoPuntoDeOrden;
        int _ordenesDeBot, _formacionesDeBot;

        protected override void OnCreate() => RequireForUpdate<NetworkStreamInGame>();

        /// <summary>
        /// El bot de prueba abandona con <c>-abandonarA</c>. El jugador, desde las Opciones de la batalla (Esc), con su confirmación.
        /// </summary>
        void Abandono()
        {
            double ahora = SystemAPI.Time.ElapsedTime;
            if (_dentroDesde < 0d) _dentroDesde = ahora;
            if (OpcionesRed.Bot)
            {
                if (OpcionesRed.AbandonarA > 0f && ahora - _dentroDesde >= OpcionesRed.AbandonarA)
                    ReconexionClienteSystem.Abandonar(EntityManager);
                return;
            }
        }

        protected override void OnUpdate()
        {
            Abandono();
            float2 mover = OpcionesRed.BotAlChoque ? HaciaElEnemigo() : OpcionesRed.Bot ? MovimientoDeBot((float)SystemAPI.Time.ElapsedTime) : MovimientoDeTeclado();
            foreach (var entrada in SystemAPI.Query<RefRW<EntradaHeroe>>().WithAll<GhostOwnerIsLocal>())
                entrada.ValueRW.Mover = mover;

            // El héroe de la batalla de verdad: las mismas teclas que el juego (Mayús corre, clic ataca, Q/E/R/T
            // habilidades, C/X/V seguir, mantener y atacar para la escuadra). El bot empuja y ataca sin parar.
            // El panel de suministro del juego (F lo abre) pide el cambio de escuadra en el héroe de red que le da el espejo
            // (EspejoClienteSystem.HeroeDelPanel): se recoge aquí, fuera del recorrido, y viaja en la entrada.
            int pedida = -1;
            var delPanel = SupplyPointUIController.NetworkHero;
            if (delPanel != Entity.Null && EntityManager.Exists(delPanel))
            {
                if (EntityManager.HasComponent<SquadSwapRequest>(delPanel))
                {
                    pedida = EntityManager.GetComponentData<SquadSwapRequest>(delPanel).newSquadId;
                    EntityManager.RemoveComponent<SquadSwapRequest>(delPanel);
                }
                EntityManager.SetComponentData(delPanel, new PlayerInteractionComponent { interactPressed = Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame });
            }
            foreach (var entrada in SystemAPI.Query<RefRW<EntradaJugador>>().WithAll<GhostOwnerIsLocal>())
            {
                if (pedida >= 0) _ultimaPedida = pedida;
                var e = new EntradaJugador { Mover = mover, EscuadraPedida = _ultimaPedida };
                if (pedida >= 0)
                    e.CambiarEscuadra.Set();
                // (con la cámara libre, el teclado es de la cámara)
                var teclado = CamaraLibre.Activa ? null : Keyboard.current;
                if (OpcionesRed.Bot)
                {
                    // Además de empujar y atacar, cada 8 s da una orden a su escuadra (van rotando) y cada 5 s lanza la Q.
                    e.Atacar = 1;
                    double t = SystemAPI.Time.ElapsedTime;
                    if (t >= _proximaOrdenDeBot)
                    {
                        _proximaOrdenDeBot = t + 8d;
                        e.Orden.Set();
                        e.TipoDeOrden = (byte)(_ordenesDeBot++ % 3);
                        // «Mantener» la manda a 12 m de su héroe, para que la escuadra tenga que ir hasta allí.
                        if (e.TipoDeOrden == (byte)SquadOrderType.HoldPosition)
                            foreach (var mando in SystemAPI.Query<RefRO<LocalTransform>>().WithAll<MandoMovil, GhostOwnerIsLocal>())
                                e.PuntoDeOrden = mando.ValueRO.Position + new float3(12f, 0f, 0f);
                    }
                    // El bot del choque no lanza habilidades: algunas mueven al héroe, y esa corrección no es del choque.
                    if (t >= _proximaHabilidadDeBot && !OpcionesRed.BotAlChoque)
                    {
                        _proximaHabilidadDeBot = t + 5d;
                        e.Habilidad.Set();
                        e.Ranura = (byte)SkillSlot.Q;
                    }
                    // Y cada 11 s pide la formación siguiente (F1-F4 por turno) y cada 25 s monta o desmonta.
                    if (t >= _proximaFormacionDeBot)
                    {
                        _proximaFormacionDeBot = t + 11d;
                        e.Formacion.Set();
                        e.IndiceDeFormacion = (byte)(_formacionesDeBot++ % 4);
                    }
                    if (t >= _proximaMonturaDeBot && !OpcionesRed.BotAlChoque)
                    {
                        _proximaMonturaDeBot = t + 25d;
                        e.Montar.Set();
                    }
                }
                else if (teclado != null)
                {
                    e.Correr = (byte)(teclado.leftShiftKey.isPressed ? 1 : 0);
                    e.Atacar = (byte)(Mouse.current != null && Mouse.current.leftButton.isPressed ? 1 : 0);
                    // Las órdenes a la escuadra, con las mismas teclas y los mismos dobles toques que SquadControlSystem:
                    // C seguir (doble C, además, cambia la prisa); X mantener en el punto del ratón (doble X, la formación
                    // siguiente); V atacar; F1-F4 y el menú radial, una formación.
                    double ahora = SystemAPI.Time.ElapsedTime;
                    int orden = -1, formacion = -1;
                    if (teclado.cKey.wasPressedThisFrame)
                    {
                        orden = (int)SquadOrderType.FollowHero;
                        if (ahora - _ultimaC <= DobleToque) e.Prisa.Set();
                        _ultimaC = ahora;
                    }
                    else if (teclado.xKey.wasPressedThisFrame)
                    {
                        if (ahora - _ultimaX <= DobleToque) formacion = EntradaJugador.SiguienteFormacion;
                        else orden = (int)SquadOrderType.HoldPosition;
                        _ultimaX = ahora;
                    }
                    else if (teclado.vKey.wasPressedThisFrame)
                        orden = (int)SquadOrderType.Attack;
                    if (orden >= 0)
                    {
                        e.Orden.Set();
                        e.TipoDeOrden = (byte)orden;
                        if (orden == (int)SquadOrderType.HoldPosition)
                            e.PuntoDeOrden = PuntoDelRaton();
                    }
                    int delMenu = BattleCommandInput.ConsumeFormation();
                    if (teclado.f1Key.wasPressedThisFrame) formacion = 0;
                    else if (teclado.f2Key.wasPressedThisFrame) formacion = 1;
                    else if (teclado.f3Key.wasPressedThisFrame) formacion = 2;
                    else if (teclado.f4Key.wasPressedThisFrame) formacion = 3;
                    else if (delMenu >= 0) formacion = delMenu;
                    if (formacion >= 0)
                    {
                        e.Formacion.Set();
                        e.IndiceDeFormacion = (byte)formacion;
                    }
                    // Habilidades: Q/E/R y T (definitiva) del héroe, 1-3 de la escuadra (o desde la barra), 4-5 perks.
                    var deLaBarra = BattleCommandInput.ConsumeSquadSkill();
                    int ranura = teclado.qKey.wasPressedThisFrame ? (int)SkillSlot.Q
                        : teclado.eKey.wasPressedThisFrame ? (int)SkillSlot.E
                        : teclado.rKey.wasPressedThisFrame ? (int)SkillSlot.R
                        : teclado.tKey.wasPressedThisFrame ? (int)SkillSlot.Ultimate
                        : teclado.digit1Key.wasPressedThisFrame ? (int)SkillSlot.Squad1
                        : teclado.digit2Key.wasPressedThisFrame ? (int)SkillSlot.Squad2
                        : teclado.digit3Key.wasPressedThisFrame ? (int)SkillSlot.Squad3
                        : teclado.digit4Key.wasPressedThisFrame ? (int)SkillSlot.ActivePerk1
                        : teclado.digit5Key.wasPressedThisFrame ? (int)SkillSlot.ActivePerk2
                        : deLaBarra.HasValue ? (int)deLaBarra.Value : -1;
                    if (ranura >= 0)
                    {
                        e.Habilidad.Set();
                        e.Ranura = (byte)ranura;
                    }
                    if (teclado.gKey.wasPressedThisFrame)
                        e.Montar.Set();
                    e.Mirar = MiradaDeLaCamara();
                }
                if (e.Orden.IsSet) { _ultimoTipoDeOrden = e.TipoDeOrden; _ultimoPuntoDeOrden = e.PuntoDeOrden; }
                else { e.TipoDeOrden = _ultimoTipoDeOrden; e.PuntoDeOrden = _ultimoPuntoDeOrden; }
                if (e.Habilidad.IsSet) _ultimaRanura = e.Ranura; else e.Ranura = _ultimaRanura;
                if (e.Formacion.IsSet) _ultimaFormacion = e.IndiceDeFormacion; else e.IndiceDeFormacion = _ultimaFormacion;
                entrada.ValueRW = e;
            }
        }

        /// <summary>El punto del terreno bajo el ratón (como SquadControlSystem en el juego); (0,0,0) si no hay cámara o no toca nada.</summary>
        static float3 PuntoDelRaton()
        {
            var camara = Camera.main;
            if (camara == null || Mouse.current == null)
                return float3.zero;
            var rayo = camara.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Physics.Raycast(rayo, out var toque, Mathf.Infinity, ~0, QueryTriggerInteraction.Ignore))
                return toque.point;
            return new Plane(Vector3.up, Vector3.zero).Raycast(rayo, out float distancia) ? (float3)rayo.GetPoint(distancia) : float3.zero;
        }

        /// <summary>Hacia dónde mira la cámara, en el plano del mundo: el héroe la encara también quieto. (0,0) sin cámara.</summary>
        static float2 MiradaDeLaCamara()
        {
            var camara = Camera.main;
            if (camara == null)
                return float2.zero;
            Vector3 adelante = Vector3.ProjectOnPlane(camara.transform.forward, Vector3.up);
            return adelante.sqrMagnitude < 1e-6f ? float2.zero : math.normalizesafe(new float2(adelante.x, adelante.z));
        }

        /// <summary>
        /// Bot de la medición de la predicción «c» (<c>-botAlChoque</c>): va hacia el cuerpo enemigo más cercano, para meterse en su escuadra y
        /// empujar contra ella. Sin mando o sin enemigos, el paseo de siempre.
        /// </summary>
        float2 HaciaElEnemigo()
        {
            foreach (var (pose, movil) in SystemAPI.Query<RefRO<LocalTransform>, RefRO<MandoMovil>>().WithAll<GhostOwnerIsLocal>())
            {
                float3 yo = pose.ValueRO.Position;
                float mejor = float.MaxValue;
                float2 hacia = float2.zero;
                foreach (var (cuerpo, soldado) in SystemAPI.Query<RefRO<LocalTransform>, RefRO<SoldadoRed>>())
                {
                    if (soldado.ValueRO.Bando == movil.ValueRO.Bando) continue;
                    float2 d = cuerpo.ValueRO.Position.xz - yo.xz;
                    float q = math.lengthsq(d);
                    if (q < mejor) { mejor = q; hacia = d; }
                }
                if (mejor == float.MaxValue) break;
                double t = SystemAPI.Time.ElapsedTime;
                // Con el mapa (-conMapa), por el camino de la malla hasta él: va a la próxima esquina.
                if (t >= _recalcularCamino)
                {
                    _recalcularCamino = t + 0.5d;
                    _conCamino = UnityEngine.AI.NavMesh.SamplePosition(yo + new float3(hacia.x, 0f, hacia.y), out var destino, 3f, UnityEngine.AI.NavMesh.AllAreas)
                                 && UnityEngine.AI.NavMesh.CalculatePath(yo, destino.position, UnityEngine.AI.NavMesh.AllAreas, _camino) && _camino.corners.Length > 1;
                }
                if (_conCamino)
                {
                    var esquinas = _camino.corners;
                    int i = 1;
                    while (i < esquinas.Length - 1 && math.distance(((float3)esquinas[i]).xz, yo.xz) < 0.5f) i++;
                    var aEsquina = ((float3)esquinas[i]).xz - yo.xz;
                    if (math.lengthsq(aEsquina) > 0.01f) return math.normalize(aEsquina);
                }
                // Sin camino (el bot sin pantalla no tiene malla): si en un segundo no ha avanzado medio metro, rodea 2 s de lado.
                if (t >= _revisarAtasco)
                {
                    if (math.distance(yo.xz, _dondeEstaba.xz) < 0.5f && t >= _rodeoHasta) { _rodeoHasta = t + 2d; _rodeoALaDerecha = !_rodeoALaDerecha; }
                    _dondeEstaba = yo;
                    _revisarAtasco = t + 1d;
                }
                var dir = math.normalizesafe(hacia);
                if (t < _rodeoHasta) dir = _rodeoALaDerecha ? new float2(dir.y, -dir.x) : new float2(-dir.y, dir.x);
                return dir;
            }
            return MovimientoDeBot((float)SystemAPI.Time.ElapsedTime);
        }

        float3 _dondeEstaba;
        double _revisarAtasco, _rodeoHasta, _recalcularCamino;
        bool _rodeoALaDerecha, _conCamino;
        readonly UnityEngine.AI.NavMeshPath _camino = new UnityEngine.AI.NavMeshPath();

        static float2 MovimientoDeBot(float t) => math.normalizesafe(new float2(math.cos(t * 0.5f), math.sin(t)));

        static float2 MovimientoDeTeclado()
        {
            var teclado = Keyboard.current;
            if (teclado == null || CamaraLibre.Activa)
                return float2.zero;
            float2 ejes = new float2(
                (teclado.dKey.isPressed ? 1 : 0) - (teclado.aKey.isPressed ? 1 : 0),
                (teclado.wKey.isPressed ? 1 : 0) - (teclado.sKey.isPressed ? 1 : 0));
            var camara = Camera.main;
            if (camara == null)
                return math.normalizesafe(ejes);
            Vector3 adelante = Vector3.ProjectOnPlane(camara.transform.forward, Vector3.up).normalized;
            Vector3 derecha = Vector3.ProjectOnPlane(camara.transform.right, Vector3.up).normalized;
            Vector3 deseo = adelante * ejes.y + derecha * ejes.x;
            return math.normalizesafe(new float2(deseo.x, deseo.z));
        }
    }

    /// <summary>
    /// Cliente: pinta héroes y soldados con cápsulas instanciadas y deja la cámara detrás del héroe propio.
    /// ponytail: sin los modelos de Synty; el puente a EntityVisualSync llega cuando el juego entre en red.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class DibujoRedSystem : SystemBase
    {
        const int Tanda = 1023;
        static readonly Color[] ColoresBando = { new Color(0.75f, 0.2f, 0.2f), new Color(0.2f, 0.35f, 0.8f) };

        Mesh _capsula;
        int _capturas;
        HeroCameraController _controlDeCamara;
        double _proximoFoco;
        float3 _foco;
        Material[] _materialesBando;
        Material _materialHeroe;
        Material _materialPropio;
        readonly List<Matrix4x4>[] _soldados = { new List<Matrix4x4>(), new List<Matrix4x4>() };
        readonly List<Matrix4x4> _heroes = new List<Matrix4x4>();
        readonly List<Matrix4x4> _propio = new List<Matrix4x4>();

        protected override void OnCreate()
        {
            if (Application.isBatchMode)
            {
                Enabled = false;
                return;
            }
            var primitiva = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            _capsula = primitiva.GetComponent<MeshFilter>().sharedMesh;
            Object.Destroy(primitiva);
            var sombreador = Shader.Find("Universal Render Pipeline/Lit");
            _materialesBando = new[] { Material(sombreador, ColoresBando[0]), Material(sombreador, ColoresBando[1]) };
            _materialHeroe = Material(sombreador, Color.white);
            _materialPropio = Material(sombreador, new Color(1f, 0.8f, 0.1f));
        }

        static Material Material(Shader sombreador, Color color)
        {
            var material = new Material(sombreador) { enableInstancing = true, color = color };
            return material;
        }

        protected override void OnUpdate()
        {
            foreach (var lista in _soldados) lista.Clear();
            _heroes.Clear();
            _propio.Clear();

            float3 suma = float3.zero;
            int cuantos = 0;
            float3? propio = null;
            int yo = SystemAPI.TryGetSingleton<NetworkId>(out var miId) ? miId.Value : -1;
            foreach (var (pose, soldado, entidad) in SystemAPI.Query<RefRO<LocalTransform>, RefRO<SoldadoRed>>().WithEntityAccess())
            {
                suma += pose.ValueRO.Position;
                cuantos++;
                // El héroe de la batalla de verdad que lleva este jugador: la cámara va con él.
                if (soldado.ValueRO.Dueno == yo && soldado.ValueRO.Tipo == SoldadoRed.Heroe)
                    propio = pose.ValueRO.Position;
                // Los que ya tienen su modelo (EspejoClienteSystem) no llevan cápsula.
                if (EntityManager.HasComponent<ConVisual>(entidad))
                    continue;
                float talla = soldado.ValueRO.Tipo == SoldadoRed.Heroe ? 1.3f : 0.8f;
                _soldados[soldado.ValueRO.Bando & 1].Add(Matrix4x4.TRS(pose.ValueRO.Position + new float3(0, talla * 1.25f, 0), pose.ValueRO.Rotation, Vector3.one * talla));
            }

            foreach (var (pose, entidad) in SystemAPI.Query<RefRO<LocalTransform>>().WithAll<HeroeRed>().WithEntityAccess())
            {
                var matriz = Matrix4x4.TRS(pose.ValueRO.Position + new float3(0, 1.2f, 0), pose.ValueRO.Rotation, Vector3.one * 1.2f);
                if (EntityManager.IsComponentEnabled<GhostOwnerIsLocal>(entidad))
                {
                    _propio.Add(matriz);
                    propio = pose.ValueRO.Position;
                }
                else
                    _heroes.Add(matriz);
            }

            for (int b = 0; b < 2; b++) Dibujar(_materialesBando[b], _soldados[b]);
            Dibujar(_materialHeroe, _heroes);
            Dibujar(_materialPropio, _propio);

            var camara = Camera.main;
            // La cámara de la escena de batalla sigue al héroe local del juego de un jugador, que aquí no existe: la lleva este sistema.
            if (OpcionesRed.BatallaReal > 0 && _controlDeCamara == null)
                _controlDeCamara = Object.FindFirstObjectByType<HeroCameraController>();
            if (_controlDeCamara != null)
                _controlDeCamara.enabled = EspejoClienteSystem.CamaraDelJuego;
            if (EspejoClienteSystem.CamaraDelJuego)
            {
                // La cámara del juego sigue al héroe propio (EspejoClienteSystem): aquí no se toca.
            }
            else if (CamaraLibre.Activa)
            {
                // La cámara libre la lleva VistaDeBatalla.
            }
            else if (camara != null && propio.HasValue)
            {
                Vector3 objetivo = propio.Value;
                // ponytail: cámara fija detrás y arriba; en la batalla de verdad, más empinada para ver por encima de las
                // murallas. La cámara del juego (órbita con el ratón) llegará cuando el cliente tenga su héroe de verdad.
                Vector3 detras = objetivo + (OpcionesRed.BatallaReal > 0 ? MandoSystem.CamaraDelCliente : new Vector3(0, 9f, -12f));
                camara.transform.position = Vector3.Lerp(camara.transform.position, detras, 1f - Mathf.Exp(-6f * SystemAPI.Time.DeltaTime));
                camara.transform.LookAt(objetivo + Vector3.up);
            }
            else if (camara != null && cuantos > 0)
            {
                // Sin héroe propio (espectador de la batalla de verdad). Con -camaraCerca, la cámara va al choque: el soldado
                // con el enemigo más cerca. Si no, mira al centro de la batalla desde arriba.
                float3 centro = suma / cuantos;
                float radio = 0f;
                foreach (var pose in SystemAPI.Query<RefRO<LocalTransform>>().WithAll<SoldadoRed>())
                    radio = math.max(radio, math.distance(pose.ValueRO.Position, centro));
                radio = math.clamp(radio * 0.6f, 25f, 140f);
                if (OpcionesRed.CamaraCerca)
                {
                    if (SystemAPI.Time.ElapsedTime >= _proximoFoco)
                    {
                        _proximoFoco = SystemAPI.Time.ElapsedTime + 1d;
                        _foco = Choque(centro);
                    }
                    centro = _foco;
                    radio = 12f;
                }
                camara.farClipPlane = 1000f;
                camara.transform.position = centro + new float3(0, radio, -radio * 0.6f);
                camara.transform.LookAt(centro + new float3(0, 1, 0));
            }

            // Dos capturas: la primera como esté la cámara y, 6 s después, otra de toda la batalla desde arriba.
            if (_capturas < 2 && !string.IsNullOrEmpty(OpcionesRed.Captura) && SystemAPI.Time.ElapsedTime >= OpcionesRed.CapturaA + 6 * _capturas)
            {
                string ruta = _capturas == 0 ? OpcionesRed.Captura : OpcionesRed.Captura.Replace(".png", "-general.png");
                ScreenCapture.CaptureScreenshot(ruta);
                int conModelo = SystemAPI.QueryBuilder().WithAll<ConVisual>().Build().CalculateEntityCount();
                Debug.Log($"[Red] Captura en {ruta}: {cuantos} soldados y héroes, {conModelo} con modelo, cámara en {(camara != null ? camara.transform.position : default)}");
                _capturas++;
                OpcionesRed.CamaraCerca = false;
            }
        }

        /// <summary>El punto medio de los dos soldados enemigos que más cerca están uno de otro. ponytail: O(n²), una vez por segundo y solo en el espectador.</summary>
        float3 Choque(float3 siNoHay)
        {
            var todos = SystemAPI.QueryBuilder().WithAll<SoldadoRed, LocalTransform>().Build();
            using var soldados = todos.ToComponentDataArray<SoldadoRed>(Unity.Collections.Allocator.Temp);
            using var poses = todos.ToComponentDataArray<LocalTransform>(Unity.Collections.Allocator.Temp);
            float mejor = float.MaxValue;
            float3 punto = siNoHay;
            for (int i = 0; i < soldados.Length; i++)
                for (int j = i + 1; j < soldados.Length; j++)
                {
                    if (soldados[i].Bando == soldados[j].Bando) continue;
                    float d = math.distancesq(poses[i].Position, poses[j].Position);
                    if (d < mejor)
                    {
                        mejor = d;
                        punto = (poses[i].Position + poses[j].Position) * 0.5f;
                    }
                }
            return punto;
        }

        void Dibujar(Material material, List<Matrix4x4> matrices)
        {
            var parametros = new RenderParams(material);
            for (int inicio = 0; inicio < matrices.Count; inicio += Tanda)
                Graphics.RenderMeshInstanced(parametros, _capsula, 0, matrices, math.min(Tanda, matrices.Count - inicio), inicio);
        }
    }
}
