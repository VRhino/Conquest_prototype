// La vista de la batalla (pedido del autor, 2026-11-27): el minimapa abajo a la derecha y la cámara libre.
// - El minimapa: el campo del lugar visto desde arriba (`MontajeDelCampo.Actual`: zonas, agua y relieve sombreado; sin campo, un fondo liso) con
//   un punto por soldado y por héroe: los tuyos en azul, los del otro bando en rojo y tu héroe en blanco, más grande. Con la cámara libre, un clic
//   en el minimapa la lleva allí.
// - La cámara libre: el botón de encima del minimapa, o Tab, la despega del héroe (`CamaraLibre`). WASD o las flechas la mueven (Mayús, más
//   deprisa), Q y E la giran, la rueda la acerca y aleja, y con el botón derecho se mira alrededor. Otra vez, vuelve al héroe.
// En UI Toolkit, con la placa de bronce y los estilos del HUD (HudBatalla.uss). Lee los soldados de la batalla en red (`SoldadoRed`, en el mundo cliente) y, si no, los de la batalla local (`TeamComponent`). Solo dibuja.
using System.Collections.Generic;
using ConquestTactics.Mundo.Campo;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace ConquestTactics.Net
{
    public class VistaDeBatalla : MonoBehaviour
    {
        const float SinCampo = 800f; // m que abarca el minimapa sin campo del lugar
        static readonly Color Tuyos = new Color(0.4f, 0.65f, 1f), Suyos = new Color(0.95f, 0.3f, 0.22f), TuHeroe = Color.white;

        VisualElement mapa, puntos, sobreLaInterfaz;
        Label textoBoton;
        readonly List<VisualElement> marcas = new List<VisualElement>();
        float lado = SinCampo;
        CampoGenerado campoPintado;
        bool pintado;
        float proximo;
        // la cámara libre
        float giro, inclinacion;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Enganchar()
        {
            SceneManager.sceneLoaded -= AlCargar;
            SceneManager.sceneLoaded += AlCargar;
        }

        static void AlCargar(Scene escena, LoadSceneMode modo)
        {
            if (escena.name != SceneTransitionService.SceneNames.Battle || Application.isBatchMode) return;
            CamaraLibre.Poner(false);
            var go = new GameObject("VistaDeBatalla");
            SceneManager.MoveGameObjectToScene(go, escena);
            go.AddComponent<VistaDeBatalla>();
        }

        void Start()
        {
            var ajustes = Instantiate(Resources.Load<PanelSettings>("HudBatalla/PanelDeBatalla"));
            ajustes.sortingOrder = 31;
            var hijo = new GameObject("Interfaz");
            hijo.SetActive(false);
            hijo.transform.SetParent(transform, false);
            var doc = hijo.AddComponent<UIDocument>();
            doc.panelSettings = ajustes;
            hijo.SetActive(true);
            var raiz = doc.rootVisualElement;
            raiz.pickingMode = PickingMode.Ignore;
            raiz.styleSheets.Add(Resources.Load<StyleSheet>("MundoUI/Mundo"));
            raiz.styleSheets.Add(Resources.Load<StyleSheet>("HudBatalla/HudBatalla"));

            // el minimapa, abajo a la derecha, y encima el botón de la cámara libre
            var caja = new VisualElement { pickingMode = PickingMode.Ignore };
            caja.AddToClassList("hud-minimapa-caja");
            raiz.Add(caja);
            var boton = new Button(() => CamaraLibre.Poner(!CamaraLibre.Activa));
            boton.AddToClassList("boton-secundario");
            boton.AddToClassList("hud-camara");
            textoBoton = new Label { pickingMode = PickingMode.Ignore };
            textoBoton.AddToClassList("hud-camara-texto");
            boton.Add(textoBoton);
            caja.Add(boton);
            var marco = new VisualElement();
            marco.AddToClassList("hud-placa");
            marco.AddToClassList("hud-minimapa");
            caja.Add(marco);
            mapa = new VisualElement();
            mapa.AddToClassList("hud-minimapa-mapa");
            marco.Add(mapa);
            puntos = new VisualElement { pickingMode = PickingMode.Ignore };
            puntos.AddToClassList("hud-minimapa-puntos");
            mapa.Add(puntos);
            mapa.RegisterCallback<PointerDownEvent>(ClicEnElMapa);
            sobreLaInterfaz = caja;
            CamaraLibre.AlCambiar += AlCambiarCamara;
            AlCambiarCamara(CamaraLibre.Activa);
        }

        void OnDestroy()
        {
            CamaraLibre.AlCambiar -= AlCambiarCamara;
            CamaraLibre.Poner(false);
        }

        void AlCambiarCamara(bool libre)
        {
            if (textoBoton != null) textoBoton.text = libre ? "Volver al héroe  ·  Tab" : "Cámara libre  ·  Tab";
            var control = HeroCameraController.Instance;
            if (control != null) control.SetCameraFollowEnabled(!libre);
            SceneTransitionService.ApplyCursorState(libre || BattleCommandInput.PointerMode);
            var cam = Camera.main;
            if (libre && cam != null)
            {
                var e = cam.transform.eulerAngles;
                giro = e.y;
                inclinacion = Mathf.Clamp(e.x > 180 ? e.x - 360 : e.x, 15f, 85f);
            }
        }

        void Update()
        {
            var teclado = Keyboard.current;
            if (teclado != null && teclado.tabKey.wasPressedThisFrame && !DialogueUIState.IsDialogueOpen) CamaraLibre.Poner(!CamaraLibre.Activa);
            if (CamaraLibre.Activa) MoverCamara(teclado);
            if (mapa != null && (!pintado || campoPintado != MontajeDelCampo.Actual)) PintarCampo();
            if (Time.unscaledTime >= proximo)
            {
                proximo = Time.unscaledTime + 0.1f;
                PonerPuntos();
            }
        }

        void MoverCamara(Keyboard teclado)
        {
            var cam = Camera.main;
            if (cam == null) return;
            float dt = Time.unscaledDeltaTime;
            var raton = Mouse.current;
            if (raton != null && raton.rightButton.isPressed)
            {
                var d = raton.delta.ReadValue();
                giro += d.x * 0.15f;
                inclinacion = Mathf.Clamp(inclinacion - d.y * 0.15f, 10f, 89f);
            }
            float rapido = teclado != null && teclado.leftShiftKey.isPressed ? 3f : 1f;
            var t = cam.transform;
            Vector3 mover = Vector3.zero;
            if (teclado != null)
            {
                if (teclado.qKey.isPressed) giro -= 70f * dt;
                if (teclado.eKey.isPressed) giro += 70f * dt;
                var adelante = Quaternion.Euler(0, giro, 0) * Vector3.forward;
                var derecha = Quaternion.Euler(0, giro, 0) * Vector3.right;
                if (teclado.wKey.isPressed || teclado.upArrowKey.isPressed) mover += adelante;
                if (teclado.sKey.isPressed || teclado.downArrowKey.isPressed) mover -= adelante;
                if (teclado.dKey.isPressed || teclado.rightArrowKey.isPressed) mover += derecha;
                if (teclado.aKey.isPressed || teclado.leftArrowKey.isPressed) mover -= derecha;
            }
            float suelo = Suelo(t.position);
            float altura = Mathf.Max(3f, t.position.y - suelo);
            // más alto, más deprisa
            var p = t.position + mover.normalized * (20f + altura * 1.2f) * rapido * dt;
            if (raton != null && !EstaSobreLaInterfaz())
            {
                float rueda = raton.scroll.ReadValue().y;
                if (Mathf.Abs(rueda) > 0.01f) altura = Mathf.Clamp(altura * (rueda > 0 ? 0.85f : 1.18f), 4f, 600f);
            }
            float medio = lado / 2f + 300f;
            p.x = Mathf.Clamp(p.x, -medio, medio);
            p.z = Mathf.Clamp(p.z, -medio, medio);
            p.y = Suelo(p) + altura;
            t.position = p;
            t.rotation = Quaternion.Euler(inclinacion, giro, 0);
        }

        /// <summary>En red: los soldados de la escuadra de este jugador que el cliente ve (los fantasmas con su dueño). Lo usa el HUD cuando el
        /// servidor no manda los efectivos.</summary>
        public static int SoldadosPropios()
        {
            foreach (var mundo in World.All)
            {
                if (!mundo.IsCreated || !mundo.IsClient()) continue;
                var em = mundo.EntityManager;
                using var qid = em.CreateEntityQuery(ComponentType.ReadOnly<NetworkId>());
                if (qid.CalculateEntityCount() != 1) continue;
                int yo = qid.GetSingleton<NetworkId>().Value, n = 0;
                using var q = em.CreateEntityQuery(ComponentType.ReadOnly<SoldadoRed>());
                using var todos = q.ToComponentDataArray<SoldadoRed>(Allocator.Temp);
                foreach (var s in todos) if (s.Tipo == SoldadoRed.Soldado && s.Dueno == yo) n++;
                return n;
            }
            return 0;
        }

        /// <summary>En red: la vida (0..1) de cada soldado de la escuadra activa de este jugador, de más a menos, como la manda el servidor.
        /// Falso si no hay batalla en red.</summary>
        public static bool VidasDeMiEscuadra(List<float> vidas)
        {
            foreach (var mundo in World.All)
            {
                if (!mundo.IsCreated || !mundo.IsClient()) continue;
                var em = mundo.EntityManager;
                using var q = em.CreateEntityQuery(ComponentType.ReadOnly<VidaDeSoldadoRed>(), ComponentType.ReadOnly<GhostOwnerIsLocal>());
                if (q.IsEmpty) continue;
                var buffer = em.GetBuffer<VidaDeSoldadoRed>(q.GetSingletonEntity(), true);
                for (int i = 0; i < buffer.Length; i++) vidas.Add(buffer[i].Vida / 255f);
                return true;
            }
            return false;
        }

        static float Suelo(Vector3 p)
        {
            var terreno = Terrain.activeTerrain;
            return terreno != null ? terreno.SampleHeight(p) + terreno.transform.position.y : 0f;
        }

        // ¿Está el ratón sobre el minimapa o su botón? (la rueda, entonces, no es de la cámara)
        bool EstaSobreLaInterfaz()
        {
            if (sobreLaInterfaz?.panel == null || Mouse.current == null) return false;
            var p = Mouse.current.position.ReadValue();
            var enPanel = RuntimePanelUtils.ScreenToPanel(sobreLaInterfaz.panel, new Vector2(p.x, Screen.height - p.y));
            return sobreLaInterfaz.worldBound.Contains(enPanel);
        }

        // un clic en el minimapa con la cámara libre la lleva allí
        void ClicEnElMapa(PointerDownEvent e)
        {
            if (!CamaraLibre.Activa) return;
            var r = mapa.contentRect;
            float u = e.localPosition.x / r.width, v = 1 - e.localPosition.y / r.height;
            var cam = Camera.main;
            if (cam == null) return;
            float altura = Mathf.Max(30f, cam.transform.position.y - Suelo(cam.transform.position));
            var destino = new Vector3((u - 0.5f) * lado, 0, (v - 0.5f) * lado);
            // la cámara mira hacia ese punto, un poco por detrás según su giro
            var atras = Quaternion.Euler(0, giro, 0) * Vector3.back * altura / Mathf.Tan(inclinacion * Mathf.Deg2Rad);
            var p = destino + atras;
            p.y = Suelo(p) + altura;
            cam.transform.position = p;
        }

        // El campo desde arriba: la zona de cada celda, con el relieve sombreado; el agua, por su hondura.
        void PintarCampo()
        {
            campoPintado = MontajeDelCampo.Actual;
            pintado = true;
            var c = campoPintado;
            if (c == null)
            {
                lado = SinCampo;
                mapa.style.backgroundImage = new StyleBackground(StyleKeyword.None);
                mapa.style.backgroundColor = new Color(0.33f, 0.4f, 0.25f, 1);
                return;
            }
            // el área jugable y 100 m más, como el Terrain jugable
            lado = c.Jugable / 1000f + 200f;
            const int n = 256;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[n * n];
            var alturas = new Rejilla(-c.Lado / 2, -c.Lado / 2, c.PasoAlturas, c.NAlturas, c.NAlturas, c.Alturas);
            float paso = lado / n;
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    // (la fila 0 de la textura es el sur: y del campo positiva)
                    float x = -lado / 2 + (i + 0.5f) * paso, z = -lado / 2 + (j + 0.5f) * paso;
                    long X = (long)(x * 1000), Y = (long)(-z * 1000);
                    int zi = Mathf.Clamp((int)((X + c.Lado / 2) / c.CeldaZonas), 0, c.NZonas - 1), zj = Mathf.Clamp((int)((Y + c.Lado / 2) / c.CeldaZonas), 0, c.NZonas - 1);
                    var zona = c.Zonas[zj * c.NZonas + zi];
                    Color col = zona switch
                    {
                        Zona.Colina => new Color(0.42f, 0.47f, 0.27f),
                        Zona.BosqueClaro => new Color(0.27f, 0.39f, 0.2f),
                        Zona.BosqueCerrado => new Color(0.18f, 0.29f, 0.15f),
                        Zona.Arena => new Color(0.78f, 0.71f, 0.52f),
                        Zona.Somero => new Color(0.45f, 0.72f, 0.72f),
                        Zona.Honda => new Color(0.2f, 0.42f, 0.55f),
                        Zona.Camino => new Color(0.66f, 0.58f, 0.42f),
                        Zona.Acantilado => new Color(0.5f, 0.47f, 0.42f),
                        _ => new Color(0.47f, 0.54f, 0.3f),
                    };
                    if (zona != Zona.Somero && zona != Zona.Honda)
                    {
                        // sombra del relieve: luz del noroeste
                        float e = paso * 1000;
                        float dx = (alturas.Muestra(X + (long)e, Y) - alturas.Muestra(X - (long)e, Y)) / 1000f, dy = (alturas.Muestra(X, Y + (long)e) - alturas.Muestra(X, Y - (long)e)) / 1000f;
                        float luz = Mathf.Clamp(1f + (-dx + dy) / (2 * paso) * 1.5f, 0.6f, 1.3f);
                        col *= luz;
                    }
                    // fuera del área jugable, más oscuro
                    if (Mathf.Abs(x) > c.Jugable / 2000f || Mathf.Abs(z) > c.Jugable / 2000f) col *= 0.7f;
                    col.a = 1;
                    px[j * n + i] = col;
                }
            tex.SetPixels32(px);
            tex.Apply();
            mapa.style.backgroundImage = new StyleBackground(tex);
        }

        void PonerPuntos()
        {
            if (puntos == null) return;
            int usados = 0;
            void Punto(Vector3 p, Color color, float tam)
            {
                float u = p.x / lado + 0.5f, v = p.z / lado + 0.5f;
                if (u < 0 || u > 1 || v < 0 || v > 1) return;
                if (usados == marcas.Count)
                {
                    var nuevo = new VisualElement { pickingMode = PickingMode.Ignore };
                    nuevo.AddToClassList("hud-minimapa-punto");
                    puntos.Add(nuevo);
                    marcas.Add(nuevo);
                }
                var m = marcas[usados++];
                m.style.display = DisplayStyle.Flex;
                m.style.backgroundColor = color;
                m.style.width = m.style.height = tam;
                m.style.left = Length.Percent(u * 100);
                m.style.top = Length.Percent((1 - v) * 100);
                m.EnableInClassList("hud-minimapa-tu", color == TuHeroe);
            }
            bool enRed = false;
            foreach (var mundo in World.All)
            {
                if (!mundo.IsCreated || !mundo.IsClient()) continue;
                var em = mundo.EntityManager;
                using var q = em.CreateEntityQuery(ComponentType.ReadOnly<SoldadoRed>(), ComponentType.ReadOnly<LocalTransform>());
                if (q.IsEmpty) continue;
                enRed = true;
                int yo = -1;
                using (var qid = em.CreateEntityQuery(ComponentType.ReadOnly<NetworkId>()))
                    if (qid.CalculateEntityCount() == 1) yo = qid.GetSingleton<NetworkId>().Value;
                using var soldados = q.ToComponentDataArray<SoldadoRed>(Allocator.Temp);
                using var poses = q.ToComponentDataArray<LocalTransform>(Allocator.Temp);
                int miBando = -1;
                for (int i = 0; i < soldados.Length; i++) if (soldados[i].Tipo == SoldadoRed.Heroe && soldados[i].Dueno == yo) miBando = soldados[i].Bando;
                for (int i = 0; i < soldados.Length; i++)
                {
                    bool heroe = soldados[i].Tipo == SoldadoRed.Heroe, propio = heroe && soldados[i].Dueno == yo && yo >= 0;
                    var color = propio ? TuHeroe : soldados[i].Bando == miBando || (miBando < 0 && soldados[i].Bando == 0) ? Tuyos : Suyos;
                    Punto(poses[i].Position, color, propio ? 10f : heroe ? 7f : 3f);
                }
            }
            if (!enRed && World.DefaultGameObjectInjectionWorld != null && World.DefaultGameObjectInjectionWorld.IsCreated)
            {
                var em = World.DefaultGameObjectInjectionWorld.EntityManager;
                int miBando = -1;
                using (var qp = em.CreateEntityQuery(ComponentType.ReadOnly<IsLocalPlayer>(), ComponentType.ReadOnly<TeamComponent>()))
                    if (qp.CalculateEntityCount() > 0) miBando = (int)qp.ToComponentDataArray<TeamComponent>(Allocator.Temp)[0].value;
                using var q = em.CreateEntityQuery(ComponentType.ReadOnly<TeamComponent>(), ComponentType.ReadOnly<LocalTransform>());
                using var bandos = q.ToComponentDataArray<TeamComponent>(Allocator.Temp);
                using var poses = q.ToComponentDataArray<LocalTransform>(Allocator.Temp);
                using var entidades = q.ToEntityArray(Allocator.Temp);
                for (int i = 0; i < bandos.Length; i++)
                {
                    bool heroe = em.HasComponent<HeroLifeComponent>(entidades[i]), propio = em.HasComponent<IsLocalPlayer>(entidades[i]);
                    Punto(poses[i].Position, propio ? TuHeroe : (int)bandos[i].value == miBando ? Tuyos : Suyos, propio ? 10f : heroe ? 7f : 3f);
                }
            }
            for (int i = usados; i < marcas.Count; i++) marcas[i].style.display = DisplayStyle.None;
            // tu héroe, encima de todo
            foreach (var m in marcas) if (m.ClassListContains("hud-minimapa-tu") && m.style.display == DisplayStyle.Flex) m.BringToFront();
        }
    }
}
