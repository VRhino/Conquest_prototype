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
        protected override void OnCreate() => RequireForUpdate<NetworkStreamInGame>();

        protected override void OnUpdate()
        {
            float2 mover = OpcionesRed.Bot ? MovimientoDeBot((float)SystemAPI.Time.ElapsedTime) : MovimientoDeTeclado();
            foreach (var entrada in SystemAPI.Query<RefRW<EntradaHeroe>>().WithAll<GhostOwnerIsLocal>())
                entrada.ValueRW.Mover = mover;
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
        bool _capturada;
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
            foreach (var (pose, soldado) in SystemAPI.Query<RefRO<LocalTransform>, RefRO<SoldadoRed>>())
            {
                float talla = soldado.ValueRO.Tipo == SoldadoRed.Heroe ? 1.3f : 0.8f;
                _soldados[soldado.ValueRO.Bando & 1].Add(Matrix4x4.TRS(pose.ValueRO.Position + new float3(0, talla * 1.25f, 0), pose.ValueRO.Rotation, Vector3.one * talla));
                suma += pose.ValueRO.Position;
                cuantos++;
            }

            float3? propio = null;
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
            if (camara != null && propio.HasValue)
            {
                Vector3 objetivo = propio.Value;
                Vector3 detras = objetivo + new Vector3(0, 9f, -12f);
                camara.transform.position = Vector3.Lerp(camara.transform.position, detras, 1f - Mathf.Exp(-6f * SystemAPI.Time.DeltaTime));
                camara.transform.LookAt(objetivo + Vector3.up);
            }
            else if (camara != null && cuantos > 0)
            {
                // Sin héroe propio (espectador de la batalla de verdad): la cámara mira al centro de la batalla desde arriba.
                float3 centro = suma / cuantos;
                float radio = 0f;
                foreach (var pose in SystemAPI.Query<RefRO<LocalTransform>>().WithAll<SoldadoRed>())
                    radio = math.max(radio, math.distance(pose.ValueRO.Position, centro));
                radio = math.clamp(radio * 0.6f, 25f, 140f);
                camara.farClipPlane = 1000f;
                camara.transform.position = centro + new float3(0, radio, -radio);
                camara.transform.LookAt(centro);
            }

            if (!_capturada && !string.IsNullOrEmpty(OpcionesRed.Captura) && SystemAPI.Time.ElapsedTime >= OpcionesRed.CapturaA)
            {
                _capturada = true;
                ScreenCapture.CaptureScreenshot(OpcionesRed.Captura);
                Debug.Log($"[Red] Captura en {OpcionesRed.Captura} con {cuantos} soldados y héroes a la vista");
            }
        }

        void Dibujar(Material material, List<Matrix4x4> matrices)
        {
            var parametros = new RenderParams(material);
            for (int inicio = 0; inicio < matrices.Count; inicio += Tanda)
                Graphics.RenderMeshInstanced(parametros, _capsula, 0, matrices, math.min(Tanda, matrices.Count - inicio), inicio);
        }
    }
}
