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
        double _proximaOrdenDeBot, _proximaHabilidadDeBot;
        int _ordenesDeBot;

        protected override void OnCreate() => RequireForUpdate<NetworkStreamInGame>();

        protected override void OnUpdate()
        {
            float2 mover = OpcionesRed.Bot ? MovimientoDeBot((float)SystemAPI.Time.ElapsedTime) : MovimientoDeTeclado();
            foreach (var entrada in SystemAPI.Query<RefRW<EntradaHeroe>>().WithAll<GhostOwnerIsLocal>())
                entrada.ValueRW.Mover = mover;

            // El héroe de la batalla de verdad: las mismas teclas que el juego (Mayús corre, clic ataca, Q/E/R/T
            // habilidades, C/X/V seguir, mantener y atacar para la escuadra). El bot empuja y ataca sin parar.
            foreach (var entrada in SystemAPI.Query<RefRW<EntradaJugador>>().WithAll<GhostOwnerIsLocal>())
            {
                var e = new EntradaJugador { Mover = mover };
                var teclado = Keyboard.current;
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
                    if (t >= _proximaHabilidadDeBot)
                    {
                        _proximaHabilidadDeBot = t + 5d;
                        e.Habilidad.Set();
                        e.Ranura = (byte)SkillSlot.Q;
                    }
                }
                else if (teclado != null)
                {
                    e.Correr = (byte)(teclado.leftShiftKey.isPressed ? 1 : 0);
                    e.Atacar = (byte)(Mouse.current != null && Mouse.current.leftButton.isPressed ? 1 : 0);
                    int orden = teclado.cKey.wasPressedThisFrame ? (int)SquadOrderType.FollowHero
                        : teclado.xKey.wasPressedThisFrame ? (int)SquadOrderType.HoldPosition
                        : teclado.vKey.wasPressedThisFrame ? (int)SquadOrderType.Attack : -1;
                    if (orden >= 0)
                    {
                        e.Orden.Set();
                        e.TipoDeOrden = (byte)orden;
                        if (orden == (int)SquadOrderType.HoldPosition)
                            e.PuntoDeOrden = PuntoDelRaton();
                    }
                    int ranura = teclado.qKey.wasPressedThisFrame ? (int)SkillSlot.Q
                        : teclado.eKey.wasPressedThisFrame ? (int)SkillSlot.E
                        : teclado.rKey.wasPressedThisFrame ? (int)SkillSlot.R
                        : teclado.tKey.wasPressedThisFrame ? (int)SkillSlot.Ultimate : -1;
                    if (ranura >= 0)
                    {
                        e.Habilidad.Set();
                        e.Ranura = (byte)ranura;
                    }
                }
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

        static float2 MovimientoDeBot(float t) => math.normalizesafe(new float2(math.cos(t * 0.5f), math.sin(t)));

        static float2 MovimientoDeTeclado()
        {
            var teclado = Keyboard.current;
            if (teclado == null)
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
