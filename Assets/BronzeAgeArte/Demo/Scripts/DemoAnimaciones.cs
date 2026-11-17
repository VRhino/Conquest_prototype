// Galería de animaciones de BronzeAge, guiada por datos: al darle a Play lee Assets/BronzeAgeArte/Demo/puestos.json y
// monta un puesto por entrada (figura repitiendo su clip en bucle, peana y rótulo). Todos con el mismo reloj.
// Añadir un puesto = dejar el FBX del clip en Demo/Animaciones (y el modelo en Demo/Modelos si es nuevo) y una entrada
// en puestos.json. Un FBX de Animaciones que no esté en la lista sale igual, al final, «sin catalogar».
// Una entrada con «secuencia» (clips separados por comas) reproduce esos clips encadenados, uno tras otro, en bucle.
// Los puestos se acumulan: nunca se quitan; las versiones de una misma animación van una al lado de la otra.
// Teclas: Espacio pausa/reanuda, 1/2/3 velocidad x1/x0,5/x0,25, R reinicia, coma/punto fotograma a fotograma (en pausa).
// Independiente de los sistemas de batalla de Conquest: un PlayableGraph manual por figura. Solo en el editor (lee los
// assets con AssetDatabase).
// Fuera de Play (17-11-2026): la escena no guarda puestos. El componente monta una VISTA PREVIA con todos los puestos de
// la lista, cada figura en la postura del primer fotograma de su clip, con objetos HideFlags.DontSave (no se guardan en
// la escena ni la ensucian); se rehace sola al cambiar puestos.json o un FBX de la demo (DemoAnimacionesEditor) y se
// quita al entrar en Play, donde los puestos se montan como siempre.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace BronzeAge.Demo
{
    [Serializable]
    public class PuestoDemo
    {
        public string nombre;
        public string tropa;
        public Animator figura;
        public AnimationClip clip;
        public List<AnimationClip> secuencia = new List<AnimationClip>();   // si hay varios, se reproducen uno tras otro
    }

    /// <summary>Una entrada de puestos.json.</summary>
    [Serializable]
    public class EntradaPuesto
    {
        public string nombre;     // animación («Tajo vertical B»)
        public string version;    // «v3 (hacha recta)»
        public string tropa;      // «Leñadores»
        public string modelo;     // FBX en Demo/Modelos, sin extensión
        public string clip;       // FBX en Demo/Animaciones, sin extensión
        public int piel;          // tono de piel 1-4 (FantasyHero_TonoN); 0 = el material que traiga el modelo
        public string secuencia;  // opcional: clips separados por comas, en orden (p. ej. reposo,tajo,reposo,tajo)
    }

    [Serializable]
    public class ListaPuestos
    {
        public List<EntradaPuesto> puestos = new List<EntradaPuesto>();
    }

    [ExecuteAlways]
    public class DemoAnimaciones : MonoBehaviour
    {
        public const string RAIZ = "Assets/BronzeAgeArte/Demo";
        public const string LISTA = RAIZ + "/puestos.json";

        public List<PuestoDemo> puestos = new List<PuestoDemo>();
        public float fotogramasPorSegundo = 30f;
        public float separacion = 3f;          // entre puestos de una fila
        public float hueco = 1.5f;             // de más entre animaciones distintas
        public float separacionFilas = 7f;     // entre tropas

        readonly List<PlayableGraph> grafos = new List<PlayableGraph>();
        readonly List<AnimationClipPlayable> clips = new List<AnimationClipPlayable>();
        readonly Dictionary<int, (AnimationMixerPlayable mezcla, List<AnimationClipPlayable> pasos)> secuencias =
            new Dictionary<int, (AnimationMixerPlayable, List<AnimationClipPlayable>)>();
        double tiempo;
        bool pausa;
        float velocidad = 1f;

        public const string VISTA_PREVIA = "Vista previa de los puestos (no se guarda)";
        GameObject vistaPrevia;
        readonly List<PuestoDemo> previa = new List<PuestoDemo>();      // los puestos de la vista previa (no se serializan)

        void Awake()
        {
#if UNITY_EDITOR
            if (Application.isPlaying) MontarDesdeLista();
#endif
        }

        void OnEnable()
        {
#if UNITY_EDITOR
            // (al abrir la escena, OnEnable llega antes de que esté cargada: se monta en cuanto lo esté)
            if (!Application.isPlaying) UnityEditor.EditorApplication.delayCall += () => { if (this != null && isActiveAndEnabled) MontarVistaPrevia(); };
#endif
        }

        void OnDisable()
        {
            QuitarVistaPrevia();
        }

        void QuitarVistaPrevia()
        {
            if (vistaPrevia != null) DestroyImmediate(vistaPrevia);
            vistaPrevia = null;
            previa.Clear();
            // las que hubieran quedado de antes (recarga de scripts): por nombre, en la escena de este componente
            if (gameObject.scene.IsValid() && gameObject.scene.isLoaded)
                foreach (var r in gameObject.scene.GetRootGameObjects())
                    if (r.name == VISTA_PREVIA) DestroyImmediate(r);
        }

        static void Quitar(UnityEngine.Object o)
        {
            if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
        }

#if UNITY_EDITOR
        /// <summary>Vista previa fuera de Play: todos los puestos, en la postura inicial de su clip, sin guardarse.</summary>
        public void MontarVistaPrevia()
        {
            if (Application.isPlaying || !gameObject.scene.isLoaded) return;
            QuitarVistaPrevia();
            vistaPrevia = new GameObject(VISTA_PREVIA) { hideFlags = HideFlags.DontSave };
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(vistaPrevia, gameObject.scene);
            Montar(vistaPrevia.transform, previa);
            foreach (var t in vistaPrevia.GetComponentsInChildren<Transform>(true))
                t.gameObject.hideFlags = HideFlags.DontSave;
            foreach (var p in previa)
                if (p.figura != null && p.clip != null)
                    p.clip.SampleAnimation(p.figura.gameObject, 0f);
        }

        /// <summary>Rehace la vista previa de todas las demos abiertas (lo llama el editor al cambiar la lista o un FBX).</summary>
        public static void RefrescarVistasPrevias()
        {
            if (Application.isPlaying) return;
            foreach (var d in FindObjectsByType<DemoAnimaciones>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (d.isActiveAndEnabled) d.MontarVistaPrevia();
        }
#endif

#if UNITY_EDITOR
        /// <summary>Lee la lista y crea los puestos (quita los que hubiera guardados en la escena).</summary>
        void MontarDesdeLista()
        {
            foreach (var raiz in gameObject.scene.GetRootGameObjects())
                if (raiz.name.StartsWith("Puesto ") || raiz.name == VISTA_PREVIA) Destroy(raiz);
            puestos.Clear();
            Montar(null, puestos);
            Debug.Log($"[DemoAnimaciones] {puestos.Count} puestos desde {LISTA}");
        }

        /// <summary>Lee la lista y crea los puestos bajo 'padre' (o en la raíz), añadiéndolos a 'destino'.</summary>
        void Montar(Transform padre, List<PuestoDemo> destino)
        {

            var lista = new ListaPuestos();
            var ruta = Path.Combine(Path.GetDirectoryName(Application.dataPath), LISTA);
            try
            {
                if (File.Exists(ruta)) lista = JsonUtility.FromJson<ListaPuestos>(File.ReadAllText(ruta)) ?? new ListaPuestos();
            }
            catch (Exception e)
            {
                Debug.LogError($"[DemoAnimaciones] No se puede leer {LISTA}: {e.Message}");
            }
            var entradas = lista.puestos.Where(e => e != null && (!string.IsNullOrEmpty(e.clip) || !string.IsNullOrEmpty(e.secuencia))).ToList();
            // los FBX de Animaciones que no estén en la lista también salen
            var modeloPorDefecto = entradas.Select(e => e.modelo).FirstOrDefault(m => !string.IsNullOrEmpty(m)) ?? "Lenador_C_1m";
            var dir = Path.Combine(Path.GetDirectoryName(Application.dataPath), RAIZ, "Animaciones");
            if (Directory.Exists(dir))
                foreach (var f in Directory.GetFiles(dir, "*.fbx").Select(Path.GetFileNameWithoutExtension).OrderBy(x => x))
                    if (!entradas.Any(e => e.clip == f))
                        entradas.Add(new EntradaPuesto { nombre = f, version = "", tropa = "(sin catalogar)", modelo = modeloPorDefecto, clip = f });

            // orden: tropa (por su primera aparición), animación (ídem), y versiones en el orden de la lista
            var ordenTropa = entradas.Select(e => e.tropa ?? "").Distinct().ToList();
            var ordenNombre = entradas.Select(e => e.nombre ?? "").Distinct().ToList();
            var ordenadas = entradas.Select((e, i) => (e, i))
                .OrderBy(t => ordenTropa.IndexOf(t.e.tropa ?? "")).ThenBy(t => ordenNombre.IndexOf(t.e.nombre ?? "")).ThenBy(t => t.i)
                .Select(t => t.e).ToList();

            var peana = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>($"{RAIZ}/Materiales/Peana.mat");
            int n = 0;
            float xMax = 0, zMin = 0;
            foreach (var grupo in ordenadas.GroupBy(e => e.tropa ?? ""))
            {
                int fila = ordenTropa.IndexOf(grupo.Key);
                float x = 0;
                string anterior = null;
                foreach (var e in grupo)
                {
                    if (anterior != null && anterior != e.nombre) x += hueco;
                    anterior = e.nombre;
                    var pos = new Vector3(x, 0, -fila * separacionFilas);
                    if (CrearPuesto(e, pos, peana, ++n, padre, destino)) { xMax = Mathf.Max(xMax, x); zMin = Mathf.Min(zMin, pos.z); }
                    x += separacion;
                }
            }
            // el suelo cubre todos los puestos con margen (en Play; fuera de Play no se toca la escena)
            var suelo = Application.isPlaying ? GameObject.Find("Suelo") : null;
            if (suelo != null)
            {
                suelo.transform.position = new Vector3(xMax * 0.5f, 0, zMin * 0.5f + 2f);
                suelo.transform.localScale = new Vector3((xMax + 30f) / 10f, 1, (-zMin + 30f) / 10f);
            }
        }

        bool CrearPuesto(EntradaPuesto e, Vector3 pos, Material peanaMat, int n, Transform padre, List<PuestoDemo> destino)
        {
            var modelo = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>($"{RAIZ}/Modelos/{e.modelo}.fbx");
            var nombreClip = !string.IsNullOrEmpty(e.clip) ? e.clip : (e.secuencia ?? "").Split(',')[0].Trim();
            var clip = UnityEditor.AssetDatabase.LoadAllAssetsAtPath($"{RAIZ}/Animaciones/{nombreClip}.fbx")
                .OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
            if (modelo == null || clip == null)
            {
                Debug.LogWarning($"[DemoAnimaciones] Puesto «{e.nombre} {e.version}»: falta el modelo {e.modelo} o el clip {e.clip}");
                return false;
            }
            var titulo = string.IsNullOrEmpty(e.version) ? e.nombre : $"{e.nombre} {e.version}";
            var raiz = new GameObject($"Puesto {n} - {titulo}");
            if (padre != null) raiz.transform.SetParent(padre, false);
            raiz.transform.position = pos;

            var pea = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pea.name = "Peana";
            Quitar(pea.GetComponent<Collider>());
            pea.transform.SetParent(raiz.transform, false);
            pea.transform.localScale = new Vector3(2f, 0.02f, 2f);
            pea.transform.localPosition = new Vector3(0, 0.02f, 0);
            if (peanaMat != null) pea.GetComponent<Renderer>().sharedMaterial = peanaMat;

            var fig = Instantiate(modelo, raiz.transform);
            fig.name = e.clip;
            fig.transform.localPosition = new Vector3(0, 0.04f, 0);
            fig.transform.localRotation = Quaternion.Euler(0, 90, 0);      // de lado al paseo: el tajo se ve entero
            // materiales de la demo: la piel de Synty con su tono y la ropa/herramienta con el atlas de la tropa
            var tono = e.piel > 0 ? UnityEditor.AssetDatabase.LoadAssetAtPath<Material>($"{RAIZ}/Materiales/FantasyHero_Tono{e.piel}.mat") : null;
            // atlas de la tropa por el nombre del material del FBX («Lenadores_Atlas», «Mineros_Atlas»…); «madera», el de leñadores
            Material Atlas(string nom)
            {
                int k = nom.IndexOf("_Atlas");
                var n = k > 0 ? nom.Substring(0, k + 6) : nom.Contains("madera") ? "Lenadores_Atlas" : null;
                return n != null ? UnityEditor.AssetDatabase.LoadAssetAtPath<Material>($"{RAIZ}/Materiales/{n}.mat") : null;
            }
            foreach (var r in fig.GetComponentsInChildren<Renderer>())
            {
                var mats = r.sharedMaterials;
                bool cambio = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    var nom = mats[i] != null ? mats[i].name : "";
                    if (tono != null && nom.Contains("lambert2")) { mats[i] = tono; cambio = true; }
                    else if (Atlas(nom) is Material atlas && mats[i] != atlas) { mats[i] = atlas; cambio = true; }
                }
                if (cambio) r.sharedMaterials = mats;
            }
            var anim = fig.GetComponent<Animator>();
            if (anim == null) anim = fig.AddComponent<Animator>();
            anim.runtimeAnimatorController = null;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            var rot = new GameObject("Rotulo");
            rot.transform.SetParent(raiz.transform, false);
            rot.transform.localPosition = new Vector3(0, 2.45f, 0);
            var tmp = rot.AddComponent<TextMeshPro>();
            var version = string.IsNullOrEmpty(e.version) ? "" : $" · {e.version}";
            tmp.text = $"<b>{e.nombre}</b>{version}\n<size=70%>{clip.length:0.00} s · {e.tropa}</size>";
            tmp.fontSize = 2.2f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(0.12f, 0.1f, 0.08f);
            tmp.rectTransform.sizeDelta = new Vector2(2.9f, 1f);
            rot.AddComponent<RotuloHaciaCamara>();

            var puesto = new PuestoDemo { nombre = titulo, tropa = e.tropa, figura = anim, clip = clip };
            if (!string.IsNullOrEmpty(e.secuencia))
                foreach (var nom in e.secuencia.Split(','))
                {
                    var c = UnityEditor.AssetDatabase.LoadAllAssetsAtPath($"{RAIZ}/Animaciones/{nom.Trim()}.fbx")
                        .OfType<AnimationClip>().FirstOrDefault(x => !x.name.StartsWith("__preview__"));
                    if (c != null) puesto.secuencia.Add(c);
                    else Debug.LogWarning($"[DemoAnimaciones] Secuencia de «{titulo}»: falta el clip {nom}");
                }
            destino.Add(puesto);
            return true;
        }
#endif

        void Start()
        {
            if (!Application.isPlaying) return;
            foreach (var p in puestos)
            {
                if (p.figura == null || p.clip == null) { grafos.Add(default); clips.Add(default); continue; }
                var g = PlayableGraph.Create("Demo_" + p.nombre);
                g.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var salida = AnimationPlayableOutput.Create(g, "anim", p.figura);
                var c = AnimationClipPlayable.Create(g, p.clip);
                c.SetApplyFootIK(false);
                if (p.secuencia.Count > 1)
                {
                    var mezcla = AnimationMixerPlayable.Create(g, p.secuencia.Count);
                    var pasos = new List<AnimationClipPlayable>();
                    for (int k = 0; k < p.secuencia.Count; k++)
                    {
                        var paso = AnimationClipPlayable.Create(g, p.secuencia[k]);
                        paso.SetApplyFootIK(false);
                        g.Connect(paso, 0, mezcla, k);
                        pasos.Add(paso);
                    }
                    secuencias[grafos.Count] = (mezcla, pasos);
                    salida.SetSourcePlayable(mezcla);
                }
                else
                    salida.SetSourcePlayable(c);
                g.Play();
                grafos.Add(g);
                clips.Add(c);
            }
            Evaluar();
        }

        void Update()
        {
            if (!Application.isPlaying) return;
            bool espacio = false, uno = false, dos = false, tres = false, erre = false, punto = false, coma = false;
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            if (k != null)
            {
                espacio = k.spaceKey.wasPressedThisFrame; uno = k.digit1Key.wasPressedThisFrame; dos = k.digit2Key.wasPressedThisFrame;
                tres = k.digit3Key.wasPressedThisFrame; erre = k.rKey.wasPressedThisFrame;
                punto = k.periodKey.wasPressedThisFrame; coma = k.commaKey.wasPressedThisFrame;
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            espacio = Input.GetKeyDown(KeyCode.Space); uno = Input.GetKeyDown(KeyCode.Alpha1); dos = Input.GetKeyDown(KeyCode.Alpha2);
            tres = Input.GetKeyDown(KeyCode.Alpha3); erre = Input.GetKeyDown(KeyCode.R);
            punto = Input.GetKeyDown(KeyCode.Period); coma = Input.GetKeyDown(KeyCode.Comma);
#endif
            if (espacio) pausa = !pausa;
            if (uno) velocidad = 1f;
            if (dos) velocidad = 0.5f;
            if (tres) velocidad = 0.25f;
            if (erre) tiempo = 0;
            if (pausa && punto) tiempo += 1.0 / fotogramasPorSegundo;
            if (pausa && coma) tiempo = Math.Max(0, tiempo - 1.0 / fotogramasPorSegundo);
            if (!pausa) tiempo += Time.deltaTime * velocidad;
            Evaluar();
        }

        void Evaluar()
        {
            for (int i = 0; i < grafos.Count; i++)
            {
                if (!grafos[i].IsValid()) continue;
                if (secuencias.TryGetValue(i, out var sq))
                {
                    var lista = puestos[i].secuencia;
                    double total = lista.Sum(c => Math.Max(1e-3, c.length));
                    double t = tiempo % total;
                    int k = 0;
                    while (k < lista.Count - 1 && t >= lista[k].length) { t -= lista[k].length; k++; }
                    for (int j = 0; j < lista.Count; j++) sq.mezcla.SetInputWeight(j, j == k ? 1f : 0f);
                    sq.pasos[k].SetTime(t);
                    grafos[i].Evaluate(0f);
                    continue;
                }
                double largo = Math.Max(1e-3, puestos[i].clip.length);
                clips[i].SetTime(tiempo % largo);
                grafos[i].Evaluate(0f);
            }
        }

        void OnDestroy()
        {
            foreach (var g in grafos)
                if (g.IsValid()) g.Destroy();
        }

        void OnGUI()
        {
            if (!Application.isPlaying) return;
            double largo = puestos.Count > 0 && puestos[0].clip != null ? puestos[0].clip.length : 1;
            int fot = (int)Math.Floor((tiempo % largo) * fotogramasPorSegundo);
            string estado = (pausa ? "PAUSA" : "reproduciendo") + "  ·  velocidad x" + velocidad.ToString("0.##") + "  ·  fotograma " + fot
                            + "  ·  " + puestos.Count + " puestos";
            GUI.Box(new Rect(10, 10, 400, 128), GUIContent.none);
            GUI.Label(new Rect(20, 16, 380, 120),
                estado + "\n" +
                "Clic: capturar ratón (mirar) · Esc: soltarlo\n" +
                "WASD caminar · Mayús correr\n" +
                "Espacio pausa/reanuda · R reiniciar\n" +
                "1 / 2 / 3: velocidad x1 / x0,5 / x0,25\n" +
                ", / .: fotograma atrás / adelante (en pausa)");
        }
    }
}
