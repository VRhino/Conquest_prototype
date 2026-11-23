// El horno de las figuras del mapa (tema 7, prueba técnica B): saca de los mismos modelos de batalla las «tiras» que pinta `ModelosDeFiguras`
// (una malla por fotograma del andar y del reposo). Cada soldado, del prefab de batalla de su tropa y su variante (`SquadData.visualPrefabName`
// con A–D, como en batalla; la tropa sin figuras propias, el de su tipo de escuadra, que es el de relleno de la batalla). El héroe, del modelo
// modular vestido con lo que lleva (`VestidoDelMotor`), montado en el caballo de héroe (`HeroHorse`) con los clips de jinete de su arma y la
// pelvis llevada a la silla, como `HeroMountPresenter`. Se hornea una clave cada vez, sin GameObject que quede: el modelo se pone en un taller
// apagado (sin despertar sus scripts), se le quitan todos los componentes salvo los que pintan y el Animator, se le avanza el Animator a mano
// fotograma a fotograma y se cuece cada pose con `BakeMesh`, juntando las piezas por material.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ConquestTactics.Mundo.Mapa;
using ConquestTactics.Visual;
using UnityEngine;
using UnityEngine.Rendering;

namespace ConquestTactics.Mundo.UI
{
    public sealed class HornoDeFiguras : MonoBehaviour
    {
        /// <summary>Fotogramas del andar y del reposo.</summary>
        public static int FotogramasAndar = 12, FotogramasReposo = 6;
        /// <summary>La velocidad del clip de andar de las tropas (`AC_milicia_lanceros`: «Locomocion», `VelocidadReal` 1,15) y del caballo al paso.</summary>
        const float AndarTropa = 1.15f, AndarCaballo = 1.1f;

        VisualPrefabConfiguration prefabs;
        Func<string, SelectorDePersonaje.HeroeParaVer> heroe;
        readonly Queue<string> cola = new Queue<string>();
        readonly Dictionary<(Material, Color?), Material> materiales = new Dictionary<(Material, Color?), Material>();
        bool horneando;
        public static readonly List<string> Bitacora = new List<string>();
        /// <summary>Apunta en la bitácora el tamaño de cada pieza cocida (para depurar escalas).</summary>
        public static bool Diagnostico;

        public void Iniciar(VisualPrefabConfiguration prefabs, Func<string, SelectorDePersonaje.HeroeParaVer> heroe)
        {
            this.prefabs = prefabs;
            this.heroe = heroe;
            ModelosDeFiguras.Pedir = clave => cola.Enqueue(clave);
            ModelosDeFiguras.Activos = prefabs != null;
        }

        void OnDestroy()
        {
            if (lector != null) Destroy(lector.gameObject);
            ModelosDeFiguras.Activos = false;
            ModelosDeFiguras.Pedir = null;
        }

        void Update()
        {
            if (!horneando && cola.Count > 0) StartCoroutine(Hornear(cola.Dequeue()));
        }

        IEnumerator Hornear(string clave)
        {
            horneando = true;
            var p = clave.Split('|');
            TiraDeFiguras tira = null;
            float t0 = Time.realtimeSinceStartup;
            try
            {
                if (p[0] == "tropa") tira = Tropa(p[1], int.Parse(p[2]));
                else if (p[0] == "heroe") tira = Heroe(p[1]);
                else if (p[0] == "animal") tira = Animal(p[1]);
                else if (p[0] == "carro") tira = Carro(p[1], p.Length > 2 ? p[2] : null);
                else if (p[0] == "aeda") tira = Aeda();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[HornoDeFiguras] {clave}: {e.Message}\n{e.StackTrace}");
            }
            if (tira != null) ModelosDeFiguras.Listos[clave] = tira;
            Bitacora.Add($"{clave}: {(tira == null ? "sin modelo" : $"{tira.Triangulos} triángulos, {tira.Materiales.Length} materiales")}, {(Time.realtimeSinceStartup - t0) * 1000:0} ms");
            yield return null;
            horneando = false;
        }

        // ------------------------------------------------------------------ las tropas

        GameObject PrefabDeTropa(string tropaId, int variante)
        {
            var d = PiezasDeTropa.Datos(tropaId);
            VisualPrefabConfiguration.UnitPrefabEntry e = null;
            if (d != null && d.visualVariants > 0 && !string.IsNullOrEmpty(d.visualPrefabName))
            {
                var id = $"{d.visualPrefabName}_{(char)('A' + variante % d.visualVariants)}";
                e = prefabs.UnitPrefabs.FirstOrDefault(x => x.unitId == id);
            }
            // sin figuras propias, el de su tipo de escuadra (el de relleno de la batalla)
            if (e == null)
            {
                var tipo = d != null ? d.type : SquadType.Spearmen;
                e = prefabs.UnitPrefabs.FirstOrDefault(x => x.squadType == tipo && x.isDefault) ?? prefabs.UnitPrefabs.FirstOrDefault(x => x.squadType == tipo);
            }
            return e == null ? null : e.battleVisualPrefab != null ? e.battleVisualPrefab : e.visualPrefab;
        }

        TiraDeFiguras Tropa(string tropaId, int variante)
        {
            var prefab = PrefabDeTropa(tropaId, variante);
            if (prefab == null) return null;
            var taller = Taller();
            try
            {
                var go = Instanciar(prefab, taller);
                Limpiar(go, taller);
                taller.SetActive(true);
                var a = go.GetComponentInChildren<Animator>();
                if (a == null) return null;
                var tira = new TiraDeFiguras { PasoDelClip = AndarTropa };
                var mats = new List<Material>();
                var porte = PorteDeMarcha ? Porte(go.transform, a) : null;
                tira.Andar = Ciclo(new[] { a }, AndarTropa, 1, FotogramasAndar, go.transform, mats, porte, out tira.DuracionAndar);
                tira.Reposo = Ciclo(new[] { a }, 0, 0, FotogramasReposo, go.transform, mats, porte, out tira.DuracionReposo);
                return Terminar(tira, mats);
            }
            finally { Destroy(taller); }
        }

        /// <summary>El porte de marcha: el arma larga (más de `ArmaLarga` m) va derecha, de la mano derecha hacia el hombro derecho y vencida hacia
        /// atrás como mucho `InclinacionMarcha` grados, en vez de tendida como en guardia. Solo en el horneado del mapa; el clip y el prefab no se
        /// tocan. Con `false`, como en batalla.</summary>
        public static bool PorteDeMarcha = true;
        const float ArmaLarga = 2f, InclinacionMarcha = 18f;

        static Action Porte(Transform raiz, Animator a)
        {
            // el arma: la pieza rígida más larga, si pasa de `ArmaLarga`
            Transform arma = null;
            Vector3 eje = default;
            float largo = ArmaLarga;
            foreach (var mf in raiz.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                var t = Vector3.Scale(mf.sharedMesh.bounds.size, mf.transform.lossyScale);
                var l = Mathf.Max(Mathf.Abs(t.x), Mathf.Abs(t.y), Mathf.Abs(t.z));
                if (l <= largo) continue;
                largo = l;
                arma = mf.transform;
                var s = mf.sharedMesh.bounds.size;
                eje = s.x >= s.y && s.x >= s.z ? Vector3.right : s.y >= s.z ? Vector3.up : Vector3.forward;
            }
            if (arma == null) { Bitacora.Add($"  porte de marcha {raiz.name}: sin arma larga"); return null; }
            // la mano que la lleva: la humanoide derecha o, en los rigs genéricos de las tropas, el hueso «hand» del que cuelga el arma (o su padre);
            // el hombro, dos huesos más arriba (antebrazo y brazo)
            Transform mano = a.isHuman ? a.GetBoneTransform(HumanBodyBones.RightHand) : null;
            for (var t = arma.parent; mano == null && t != null && t != raiz; t = t.parent)
                if (t.name.IndexOf("hand", StringComparison.OrdinalIgnoreCase) >= 0) mano = t;
            if (mano == null) mano = arma.parent;
            var hombro = a.isHuman ? a.GetBoneTransform(HumanBodyBones.RightUpperArm) : mano?.parent?.parent;
            Bitacora.Add($"  porte de marcha {raiz.name}: {arma.name}, {largo:0.00} m, mano {mano?.name}, hombro {hombro?.name}");
            if (mano == null || hombro == null) return null;
            // el otro brazo: los mismos huesos con el lado cambiado (`_R` → `_L`, `Right` → `Left`)
            var codo = mano.parent;
            Transform Otro(Transform t)
            {
                if (t == null) return null;
                var n = t.name.Replace("_R", "_L").Replace(".R", ".L").Replace("Right", "Left");
                return n == t.name ? null : raiz.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == n);
            }
            var (manoI, codoI, hombroI) = (Otro(mano), Otro(codo), Otro(hombro));
            bool primera = true;
            Vector3 agarre = default; // la mano, en el espacio del arma (en la pose del clip)
            float punta = 1;           // hacia dónde del eje está la punta (la que en guardia va hacia delante)
            int fotograma = 0;
            return () =>
            {
                if (primera)
                {
                    primera = false;
                    agarre = arma.InverseTransformPoint(mano.position);
                    agarre = Vector3.Scale(agarre, eje); // sobre el eje del asta
                    punta = Vector3.Dot(arma.TransformDirection(eje), raiz.forward) >= 0 ? 1 : -1;
                }
                // los brazos, sobre la pose del clip: el derecho bajado junto al cuerpo con el codo doblado y la mano a la altura del pecho, sobre
                // el asta; el izquierdo colgando junto al cuerpo con un balanceo pequeño. El codo nunca en ala: todo en el plano del cuerpo.
                var (arriba, delante, derecha) = (raiz.up, raiz.forward, raiz.right);
                Apuntar(hombro, codo, (-arriba + delante * 0.12f + derecha * 0.03f).normalized);
                Apuntar(codo, mano, (arriba * 0.85f + delante * 0.5f - derecha * 0.08f).normalized);
                if (BrazoLibre && hombroI != null && codoI != null && manoI != null)
                {
                    float vaiven = 0.14f * Mathf.Sin(2 * Mathf.PI * fotograma / FotogramasAndar);
                    Apuntar(hombroI, codoI, (-arriba + delante * vaiven - derecha * 0.05f).normalized);
                    Apuntar(codoI, manoI, (-arriba + delante * (0.25f + vaiven)).normalized);
                }
                fotograma++;
                // el asta: de la mano, apoyada en el hombro y vencida hacia atrás `InclinacionMarcha`
                var d = (arriba * Mathf.Cos(InclinacionMarcha * Mathf.Deg2Rad) - delante * Mathf.Sin(InclinacionMarcha * Mathf.Deg2Rad)).normalized;
                var ahora = arma.TransformDirection(eje * punta);
                arma.rotation = Quaternion.FromToRotation(ahora, d) * arma.rotation;
                arma.position += mano.position - arma.TransformPoint(agarre);
            };
        }

        /// <summary>Corrige el brazo izquierdo en el porte de marcha (colgando, con su balanceo). Con `false`, se queda como en el clip.</summary>
        public static bool BrazoLibre = true;

        /// <summary>Gira `hueso` para que su hijo `hacia` quede en la dirección `d` (en el mundo).</summary>
        static void Apuntar(Transform hueso, Transform hacia, Vector3 d)
        {
            if (hueso == null || hacia == null) return;
            var ahora = hacia.position - hueso.position;
            if (ahora.sqrMagnitude < 1e-8f) return;
            hueso.rotation = Quaternion.FromToRotation(ahora, d) * hueso.rotation;
        }

        // ------------------------------------------------------------------ el héroe a caballo

        static HeroClass ClaseDe(SelectorDePersonaje.HeroeParaVer h)
        {
            var id = (h?.Clase?.Id ?? "").ToLowerInvariant();
            return id.Contains("lanz") || id.Contains("spear") || id.Contains("pica") ? HeroClass.Spear
                : id.Contains("arc") || id.Contains("bow") ? HeroClass.Bow : HeroClass.SwordAndShield;
        }

        /// <summary>El héroe a caballo montado en un taller (encendido): el caballo de héroe con su capa y sus riendas, el cuerpo vestido con el clip
        /// de jinete de su arma, y cómo llevar la pelvis a la silla. Null si falta algo (y el taller ya destruido).</summary>
        (GameObject taller, Animator caballo, Animator jinete, Action sentar)? MontarHeroe(string heroeId)
        {
            var h = heroe?.Invoke(heroeId);
            var prefabHeroe = prefabs.GetDefaultHeroPrefab();
            var caballo = Resources.Load<HeroHorseVisual>(HeroHorseVisual.ResourcePath);
            if (h == null || prefabHeroe == null || caballo == null) return null;
            var taller = Taller();
            var montura = Instanciar(caballo.gameObject, taller);
            var hv = montura.GetComponent<HeroHorseVisual>();
            if (hv == null) { Destroy(taller); return null; }
            var clase = ClaseDe(h);
            var controlJinete = hv.RiderFor(clase);
            hv.SetReins(clase);
            // la capa de la montura que lleva puesta (sin correspondencia, 0: la del prefab), como `HeroMountPresenter`
            var montado = h.Piezas != null && h.Piezas.TryGetValue("montura", out var m) ? m : null;
            hv.SetCoat(VestidoDelMotor.PelajeDe(montado));
            // el caballo de guerra de su montura, con las señas de este héroe (sin montura, el de siempre)
            CaballosDeGuerra.Cargar()?.Vestir(montura, montado, heroeId);
            var cuerpo = Instanciar(prefabHeroe, taller);
            // vestido antes de quitarle nada (el vestido busca sus piezas y huesos en el modelo entero)
            VestidoDelMotor.Vestir(cuerpo.transform, h);
            Limpiar(montura, taller, typeof(HeroHorseVisual));
            Limpiar(cuerpo, taller);
            taller.SetActive(true);
            var ah = cuerpo.GetComponentInChildren<Animator>();
            var ac = montura.GetComponentInChildren<Animator>();
            if (ah == null || ac == null || controlJinete == null || hv.Saddle == null) { Destroy(taller); return null; }
            ah.runtimeAnimatorController = controlJinete;
            var cadera = ah.isHuman ? ah.GetBoneTransform(HumanBodyBones.Hips) : null;
            // la silla en reposo, para llevar la pelvis a ella (`HeroMountPresenter.LateUpdate`)
            var correccion = Quaternion.Inverse(hv.Saddle.rotation) * montura.transform.rotation;
            void Sentar()
            {
                if (cadera == null) return;
                var asiento = hv.Saddle.rotation * correccion;
                var raizJinete = ah.transform;
                var inversa = Quaternion.Inverse(raizJinete.rotation);
                var local = inversa * (cadera.position - raizJinete.position);
                cadera.SetPositionAndRotation(hv.Saddle.position + asiento * local, asiento * (inversa * cadera.rotation));
            }
            return (taller, ac, ah, Sentar);
        }

        TiraDeFiguras Heroe(string heroeId)
        {
            if (!(MontarHeroe(heroeId) is var (taller, ac, ah, sentar))) return null;
            try
            {
                var tira = new TiraDeFiguras { PasoDelClip = AndarCaballo };
                var mats = new List<Material>();
                tira.Andar = Ciclo(new[] { ac, ah }, AndarCaballo, 1, FotogramasAndar, taller.transform, mats, sentar, out tira.DuracionAndar);
                tira.Reposo = Ciclo(new[] { ac, ah }, 0, 0, FotogramasReposo, taller.transform, mats, sentar, out tira.DuracionReposo);
                return Terminar(tira, mats);
            }
            finally { Destroy(taller); }
        }

        /// <summary>El héroe del juego, vivo (para compararlo con el horneado): el mismo montaje, con sus materiales de siempre y sus Animators
        /// andando solos, en `pie` mirando a `giro` y a la escala del mapa. Quien lo pide lo destruye.</summary>
        public GameObject HeroeVivo(string heroeId, Vector3 pie, Quaternion giro, float escala, bool andando)
        {
            if (!(MontarHeroe(heroeId) is var (taller, ac, ah, sentar))) return null;
            taller.name = "Héroe del juego (vivo)";
            foreach (var r in taller.GetComponentsInChildren<Renderer>(true)) r.forceRenderingOff = false;
            foreach (var a in new[] { ac, ah })
            {
                a.Rebind();
                foreach (var p in a.parameters)
                {
                    if (p.type != AnimatorControllerParameterType.Float) continue;
                    if (p.name == "MountGaitBlend") a.SetFloat(p.name, andando ? 1 : 0);
                    else if (p.name == "MountGaitScale") a.SetFloat(p.name, 1);
                    else if (p.name.Contains("Velocidad") || p.name.Contains("Speed")) a.SetFloat(p.name, andando ? AndarCaballo : 0);
                }
            }
            taller.transform.SetPositionAndRotation(pie, giro);
            taller.transform.localScale = Vector3.one * escala;
            taller.AddComponent<Sentado>().Sentar = sentar;
            return taller;
        }

        /// <summary>Lleva la pelvis del jinete a la silla después de animar (como `HeroMountPresenter`).</summary>
        sealed class Sentado : MonoBehaviour
        {
            public Action Sentar;
            void LateUpdate() => Sentar?.Invoke();
        }

        // ------------------------------------------------------------------ el taller

        static GameObject Taller()
        {
            var t = new GameObject("Taller de figuras");
            t.SetActive(false);
            t.transform.position = new Vector3(0, -5000, 0);
            return t;
        }

        static readonly Type[] QueSeQuedan = { typeof(Transform), typeof(Animator), typeof(SkinnedMeshRenderer), typeof(MeshRenderer), typeof(MeshFilter) };

        /// <summary>El prefab en el taller (apagado: sin Awake).</summary>
        static GameObject Instanciar(GameObject prefab, GameObject taller)
        {
            var go = Instantiate(prefab, taller.transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            return go;
        }

        /// <summary>Sin nada más que lo que pinta y el Animator (primero los scripts, de lo más hondo a la raíz, que piden colisores y demás; luego
        /// lo que quede); luego el taller se enciende.</summary>
        static void Limpiar(GameObject go, GameObject taller, Type conservar = null)
        {
            bool Guardar(Component c) => QueSeQuedan.Any(t => t.IsInstanceOfType(c)) || conservar != null && conservar.IsInstanceOfType(c);
            foreach (var c in go.GetComponentsInChildren<MonoBehaviour>(true).Reverse())
                if (c != null && !Guardar(c)) DestroyImmediate(c);
            foreach (var c in go.GetComponentsInChildren<Component>(true).Reverse())
                if (c != null && !Guardar(c)) DestroyImmediate(c);
            foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.forceRenderingOff = true;
            foreach (var a in go.GetComponentsInChildren<Animator>(true)) { a.cullingMode = AnimatorCullingMode.AlwaysAnimate; a.applyRootMotion = false; }
        }

        /// <summary>Un ciclo: los Animators con la velocidad `v` (lo que la tenga: `VelocidadReal`, `Speed`…) y el aire `aire` (`MountGaitBlend`),
        /// avanzados a mano; `n` poses repartidas en lo que dura el estado (`vueltas` veces), cada una cocida en una malla.</summary>
        Mesh[] Ciclo(Animator[] animators, float v, float aire, int n, Transform raiz, List<Material> mats, Action tras, out float duracion, int vueltas = 1)
        {
            duracion = Preparar(animators, v, aire) * vueltas;
            var r = new Mesh[n];
            for (int i = 0; i < n; i++)
            {
                tras?.Invoke();
                r[i] = Cocer(raiz, mats);
                foreach (var a in animators) a.Update(duracion / n);
            }
            return r;
        }

        /// <summary>Los Animators desde cero, con la velocidad y el aire puestos y asentados; lo que dura el estado del primero.</summary>
        static float Preparar(Animator[] animators, float v, float aire)
        {
            foreach (var a in animators)
            {
                a.Rebind();
                foreach (var p in a.parameters)
                {
                    if (p.type != AnimatorControllerParameterType.Float) continue;
                    if (p.name == "MountGaitBlend") a.SetFloat(p.name, aire);
                    else if (p.name == "MountGaitScale") a.SetFloat(p.name, 1);
                    else if (p.name.Contains("Velocidad") || p.name.Contains("Speed")) a.SetFloat(p.name, v);
                }
                a.Update(0);
                for (int i = 0; i < 10; i++) a.Update(0.05f);
            }
            return Mathf.Max(0.2f, animators[0].GetCurrentAnimatorStateInfo(0).length);
        }

        // ------------------------------------------------------------------ los animales y los carros de las caravanas (tema 7)

        /// <summary>Los modelos (`CaravanaSetup`), en Resources.</summary>
        public const string Caravana = "MapaMundo/Caravana/";
        public static readonly Dictionary<string, string> Animales = new Dictionary<string, string>
            { ["buey"] = "Ox_Draught", ["burro"] = "Donkey_Pack", ["camello"] = "Camel_Draught" };
        public static readonly Dictionary<string, string> Carros = new Dictionary<string, string>
            { ["basico"] = "Cart_Basic", ["reforzado"] = "Cart_Reinforced", ["fundacion"] = "Cart_Foundation", ["carrito"] = "Cart_Hero" };
        /// <summary>El caballo que tira de un carro: el de la montura de tiro (tracio castaño).</summary>
        public const string MonturaDeTiro = "montura_tiro";
        /// <summary>Lo que gira una rueda maciza de tres tablas hasta verse igual: media vuelta.</summary>
        const float SimetriaRueda = 180f;
        /// <summary>La punta de las varas, este tanto por debajo de lo alto del yugo o de la almohadilla (como en las láminas de Blender).</summary>
        const float BajoElYugo = 0.06f;
        /// <summary>Cuántos ciclos de andar puede cubrir la tira de un carro (12 fotogramas cada uno) para que sus ruedas cierren sin salto.</summary>
        const int CiclosDeCarro = 3;
        /// <summary>Lo más que se inclina un carro de dos ruedas que además tiene bisagra en las varas (el carrito): el resto, la bisagra.</summary>
        const float InclinacionConBisagra = 8f;
        /// <summary>Lo medido al enganchar y al hacer rodar cada carro, por clave (para la lámina).</summary>
        public static readonly Dictionary<string, string> Medidas = new Dictionary<string, string>();

        /// <summary>Cuánto de la amplitud del paso del caballo llevan las patas de cada animal (1: toda). Las patas cortas con todo el giro del
        /// caballo andan agachadas y levantan de más la mano.</summary>
        public static readonly Dictionary<string, float> AmplitudDePatas = new Dictionary<string, float> { ["buey"] = 0.6f, ["burro"] = 0.85f, ["camello"] = 0.7f };
        /// <summary>El cero de la copia de giros: la postura de pie del caballo (su parado), no el reposo de su FBX; así, de pie, el animal queda
        /// como se modeló.</summary>
        public static bool CeroDePie = true;
        /// <summary>Apunta en la bitácora lo doblado de las patas (para medir el paso).</summary>
        public static bool MedirPatas;
        static bool EsPata(string n) => n.StartsWith("Shoulder") || n.StartsWith("UpperArm") || n.StartsWith("Forearm") || n.StartsWith("Front")
                                        || n.StartsWith("Thigh") || n.StartsWith("Gaskin") || n.StartsWith("Hind");

        /// <summary>Lo doblado de la pata izquierda de delante (codo y rodilla) y de detrás (babilla y corvejón): el ángulo entre un tramo y el
        /// siguiente, en grados (0: recta).</summary>
        public static string Flexion(Transform raiz)
        {
            var h = new Dictionary<string, Transform>();
            foreach (var t in raiz.GetComponentsInChildren<Transform>(true)) if (!h.ContainsKey(t.name)) h[t.name] = t;
            float A(string a, string b, string c, string d) => h.ContainsKey(a) && h.ContainsKey(b) && h.ContainsKey(c) && h.ContainsKey(d)
                ? Vector3.Angle(h[b].position - h[a].position, h[d].position - h[c].position) : -1;
            float Aj(string a, string b, string c) => A(a, b, b, c);
            return $"codo {Aj("UpperArm.L", "Forearm.L", "FrontCannon.L"):0} rodilla {Aj("Forearm.L", "FrontCannon.L", "FrontPastern.L"):0} "
                   + $"babilla {Aj("Thigh.L", "Gaskin.L", "HindCannon.L"):0} corvejón {Aj("Gaskin.L", "HindCannon.L", "HindPastern.L"):0}";
        }

        /// <summary>Un animal de caravana que anda con el clip del caballo: un caballo de héroe escondido, en su taller, hace de reloj; antes de cocer
        /// cada pose se copia el giro de cada hueso del caballo, respecto a su postura de pie (`CeroDePie`), al hueso del mismo nombre del animal,
        /// sobre el reposo propio del animal, y en las patas solo la parte `AmplitudDePatas`. Solo giros: así el animal conserva el largo de sus
        /// patas y de su cuello (el clip, que no se toca, escribe también posiciones de hueso, que lo dejarían con las del caballo). `Escala`: lo
        /// alto de su pecho respecto al del caballo por la amplitud de sus patas (para su paso, sin patinar).</summary>
        (GameObject taller, Animator reloj, Action copiar, float escala)? Reloj(Transform animal, string tipo)
        {
            var prefab = Resources.Load<HeroHorseVisual>(HeroHorseVisual.ResourcePath);
            if (prefab == null) return null;
            var taller = Taller();
            var caballo = Instanciar(prefab.gameObject, taller);
            Limpiar(caballo, taller);
            var a = caballo.GetComponentInChildren<Animator>();
            var cuerpoC = caballo.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(r => r.name == "Horse");
            var cuerpoA = animal.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(r => r.name == "Body");
            if (a == null || cuerpoC == null || cuerpoA == null) { Destroy(taller); return null; }
            var huesosC = new Dictionary<string, Transform>();
            foreach (var t in cuerpoC.bones) if (t != null) huesosC[t.name] = t;
            var amplitud = AmplitudDePatas.TryGetValue(tipo, out var am) ? am : 1;
            // (lo alto del pecho, sobre las manos, de uno y otro)
            float Alto(SkinnedMeshRenderer r, Transform raiz) { var p = r.bones.FirstOrDefault(b => b != null && b.name == "Chest"); return p == null ? 0 : raiz.InverseTransformPoint(p.position).y; }
            var altoC = Alto(cuerpoC, caballo.transform);
            var escala = (altoC > 0.1f && Alto(cuerpoA, animal) > 0.1f ? Alto(cuerpoA, animal) / altoC : 1) * amplitud;
            if (MedirPatas) Bitacora.Add($"  patas {tipo}: caballo en reposo {Flexion(caballo.transform)}; {tipo} en reposo {Flexion(animal)}");
            taller.SetActive(true);
            // el cero: la postura de pie del caballo (parado y asentado) o su reposo
            if (CeroDePie) Preparar(new[] { a }, 0, 0);
            if (MedirPatas) Bitacora.Add($"  patas {tipo}: caballo de pie {Flexion(caballo.transform)}");
            var pares = new List<(Transform c, Transform a, Quaternion reposo, Quaternion cero, float f)>();
            foreach (var t in cuerpoA.bones)
                if (t != null && huesosC.TryGetValue(t.name, out var c))
                    pares.Add((c, t, t.localRotation, Quaternion.Inverse(c.localRotation), EsPata(t.name) ? amplitud : 1));
            void Copiar()
            {
                foreach (var (c, t, reposo, cero, f) in pares)
                {
                    var d = cero * c.localRotation;
                    t.localRotation = reposo * (f == 1 ? d : Quaternion.Slerp(Quaternion.identity, d, f));
                }
            }
            return (taller, a, Copiar, escala);
        }

        /// <summary>Un animal de caravana suelto (buey, burro o camello; «caballo», el de tiro), andando y parado.</summary>
        TiraDeFiguras Animal(string tipo)
        {
            var taller = Taller();
            GameObject reloj = null;
            try
            {
                var (go, a, copiar, escala) = Bestia(tipo, taller, out reloj);
                if (go == null) return null;
                taller.SetActive(true);
                var tira = new TiraDeFiguras { PasoDelClip = AndarCaballo * escala };
                var mats = new List<Material>();
                var medir = MedirPatas && reloj != null ? (Action)(() => { copiar?.Invoke(); Bitacora.Add($"  patas {tipo} andando: {Flexion(go.transform)} | caballo {Flexion(reloj.transform)}"); }) : copiar;
                tira.Andar = Ciclo(new[] { a }, AndarCaballo, 1, FotogramasAndar, go.transform, mats, medir, out tira.DuracionAndar);
                tira.Reposo = Ciclo(new[] { a }, 0, 0, FotogramasReposo, go.transform, mats, copiar, out tira.DuracionReposo);
                return Terminar(tira, mats);
            }
            finally
            {
                Destroy(taller);
                if (reloj != null) Destroy(reloj);
            }
        }

        /// <summary>El animal en el taller, con lo que lo mueve: el caballo, su propio Animator; los demás, el reloj del caballo y la copia de giros.</summary>
        (GameObject go, Animator a, Action copiar, float escala) Bestia(string tipo, GameObject taller, out GameObject reloj, bool sinAperos = false)
        {
            reloj = null;
            if (tipo == "caballo")
            {
                var prefab = Resources.Load<HeroHorseVisual>(HeroHorseVisual.ResourcePath);
                if (prefab == null) return default;
                var caballo = Instanciar(prefab.gameObject, taller);
                if (CaballosDeGuerra.Cargar()?.Vestir(caballo, MonturaDeTiro, null, deTiro: true) != true) return default;
                Limpiar(caballo, taller);
                return (caballo, caballo.GetComponentInChildren<Animator>(), null, 1);
            }
            if (!Animales.TryGetValue(tipo, out var nombre)) return default;
            var modelo = Resources.Load<GameObject>(Caravana + nombre);
            if (modelo == null) return default;
            var go = Instanciar(modelo, taller);
            Limpiar(go, taller);
            foreach (var an in go.GetComponentsInChildren<Animator>(true)) an.enabled = false;   // (lo mueve el caballo)
            if (sinAperos) foreach (var s in go.GetComponentsInChildren<SkinnedMeshRenderer>(true)) if (s.name == "Tack") s.gameObject.SetActive(false);
            if (!(Reloj(go.transform, tipo) is var (t, a, copiar, escala))) return default;
            reloj = t;
            return (go, a, copiar, escala);
        }

        /// <summary>Un carro de caravana (básico, reforzado o de fundación) enganchado a su animal, o el carrito del héroe (sin animal, a mano; con
        /// él, el burro sin alforjas). Las varas se llevan a lo alto del yugo o de la almohadilla del animal (en el caballo y el burro, a su cruz):
        /// las que van aparte, con bisagra (los de cuatro ruedas y el carrito), giran en ella; si no, el carro de dos ruedas se inclina sobre su eje.
        /// Lo que no case queda apuntado en `Medidas`. Las ruedas giran lo que avanza el carro, en una tira de uno a tres ciclos de andar que cierre
        /// en medias vueltas, sin salto.</summary>
        TiraDeFiguras Carro(string tipo, string animal)
        {
            if (!Carros.TryGetValue(tipo, out var nombre)) return null;
            var modelo = Resources.Load<GameObject>(Caravana + nombre);
            if (modelo == null) return null;
            var clave = animal == null ? tipo : tipo + "|" + animal;
            var taller = Taller();
            GameObject reloj = null;
            try
            {
                // (el modelo, dentro de un padre propio: el FBX de una sola raíz trae el cuerpo en la raíz, y para inclinarlo hace falta un padre)
                var carro = new GameObject("carro");
                carro.transform.SetParent(taller.transform, false);
                // (con el giro que trae el prefab: el FBX del carro lleva en la raíz el paso de los ejes de Blender)
                Limpiar(Instantiate(modelo, carro.transform, false), taller);
                var ruedas = carro.GetComponentsInChildren<Transform>(true).Where(t => t.name.Contains("_Wheel_")).ToArray();
                var cuerpo = carro.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(m => !m.name.Contains("_Wheel_") && !m.name.Contains("_Shafts"));
                // (las varas aparte, con bisagra en el frente de la caja: las de los carros de cuatro ruedas)
                var varas = carro.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(m => m.name.Contains("_Shafts"));
                if (cuerpo == null || ruedas.Length == 0) return null;
                // el radio: medio alto de la primera rueda, en el espacio del carro
                var rueda = ruedas[0].GetComponent<MeshFilter>();
                var aCarro = carro.transform.worldToLocalMatrix * ruedas[0].localToWorldMatrix;
                var alturas = rueda.sharedMesh.vertices.Select(v => aCarro.MultiplyPoint3x4(v).y).ToArray();
                float radio = (alturas.Max() - alturas.Min()) / 2;
                var eje = carro.transform.right;
                (GameObject go, Animator a, Action copiar, float escala) bestia = default;
                string medida = $"rueda {radio:0.00} m";
                if (animal != null)
                {
                    bestia = Bestia(animal, taller, out reloj, sinAperos: tipo == "carrito");
                    if (bestia.go == null) return null;
                    medida += ", " + Enganchar(carro.transform, cuerpo, varas, ruedas, bestia.go.transform, animal == "caballo" ? "Horse" : tipo == "carrito" ? "Body" : null);
                }
                var giroRueda = ruedas.Select(r => r.rotation).ToArray();
                taller.SetActive(true);
                var tira = new TiraDeFiguras { PasoDelClip = bestia.go != null ? AndarCaballo * bestia.escala : AndarTropa };
                var mats = new List<Material>();
                // lo que avanza en un ciclo de andar; la tira cubre de 1 a `CiclosDeCarro` ciclos, los que mejor cierren en medias vueltas de rueda
                float dur = bestia.a != null ? Preparar(new[] { bestia.a }, AndarCaballo, 1) : Mathf.PI * radio / AndarTropa;
                float avance = tira.PasoDelClip * dur;
                int ciclos = 1, medias = 1;
                float desliza = float.MaxValue;
                for (int c = 1; c <= (bestia.a != null ? CiclosDeCarro : 1); c++)
                {
                    int m = Mathf.Max(1, Mathf.RoundToInt(c * avance / (Mathf.PI * radio)));
                    float d = Mathf.Abs(m * Mathf.PI * radio - c * avance) / (c * avance);
                    if (d < desliza - 0.02f) (ciclos, medias, desliza) = (c, m, d);
                }
                medida += $", {medias} media{(medias > 1 ? "s" : "")} vuelta{(medias > 1 ? "s" : "")} de rueda en {ciclos} ciclo{(ciclos > 1 ? "s" : "")} de andar (desliza un {desliza:0%})";
                int andar = FotogramasAndar * ciclos;
                int fotograma = 0;
                void Rodar(int n)
                {
                    float angulo = SimetriaRueda * medias * fotograma++ / n;
                    for (int i = 0; i < ruedas.Length; i++) ruedas[i].rotation = Quaternion.AngleAxis(angulo, eje) * giroRueda[i];
                    bestia.copiar?.Invoke();
                }
                if (bestia.a != null)
                {
                    tira.Andar = Ciclo(new[] { bestia.a }, AndarCaballo, 1, andar, taller.transform, mats, () => Rodar(andar), out tira.DuracionAndar, ciclos);
                    fotograma = 0;
                    tira.Reposo = Ciclo(new[] { bestia.a }, 0, 0, FotogramasReposo, taller.transform, mats, () => { fotograma = 0; Rodar(1); }, out tira.DuracionReposo);
                }
                else
                {
                    tira.Andar = new Mesh[FotogramasAndar];
                    for (int i = 0; i < FotogramasAndar; i++) { Rodar(FotogramasAndar); tira.Andar[i] = Cocer(taller.transform, mats); }
                    tira.DuracionAndar = dur;
                    fotograma = 0;
                    Rodar(1);
                    var quieto = Cocer(taller.transform, mats);
                    tira.Reposo = Enumerable.Repeat(quieto, FotogramasReposo).ToArray();
                    tira.DuracionReposo = 2;
                }
                Medidas[clave] = medida;
                return Terminar(tira, mats);
            }
            finally
            {
                Destroy(taller);
                if (reloj != null) Destroy(reloj);
            }
        }

        /// <summary>Pone el animal delante del carro con lo alto de su yugo (o almohadilla, o la cruz del caballo) a la punta de las varas; si no
        /// llega y el carro es de dos ruedas, lo inclina sobre su eje. Devuelve lo medido.</summary>
        static string Enganchar(Transform carro, MeshFilter cuerpo, MeshFilter varas, Transform[] ruedas, Transform animal, string cruzDe)
        {
            // la punta de las varas: lo más adelantado del carro (de sus varas, si van aparte)
            Vector3 Punta()
            {
                var mf = varas != null ? varas : cuerpo;
                var M = carro.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                var vs = mf.sharedMesh.vertices.Select(v => M.MultiplyPoint3x4(v)).ToArray();
                var frente = vs.Max(v => v.z);
                var punta = vs.Where(v => v.z > frente - 0.05f).ToArray();
                return new Vector3(0, punta.Average(v => v.y), punta.Average(v => v.z));
            }
            // el apoyo del animal: lo alto de sus aperos y su centro a lo largo (el yugo, la almohadilla); en el caballo, lo alto de la cruz
            Vector3 Apoyo()
            {
                var r = animal.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(s => s.name == (cruzDe ?? "Tack"));
                if (r == null) return Vector3.zero;
                var m = new Mesh();
                r.BakeMesh(m, true);
                var M = animal.worldToLocalMatrix * r.transform.localToWorldMatrix;
                var vs = m.vertices.Select(v => M.MultiplyPoint3x4(v)).ToArray();
                Destroy(m);
                if (cruzDe != null)
                {
                    var pecho = r.bones.FirstOrDefault(b => b != null && b.name == "Chest");
                    float z = pecho != null ? animal.InverseTransformPoint(pecho.position).z : vs.Average(v => v.z);
                    var cruz = vs.Where(v => Mathf.Abs(v.x) < 0.12f && Mathf.Abs(v.z - z) < 0.15f).ToArray();
                    return cruz.Length == 0 ? Vector3.zero : new Vector3(0, cruz.Max(v => v.y), z);
                }
                return new Vector3(0, vs.Max(v => v.y), vs.Average(v => v.z));
            }
            var apoyo = Apoyo();
            var p = Punta();
            float falta = apoyo.y - BajoElYugo - p.y;   // (> 0: las varas quedan bajas)
            float inclinacion = 0;
            float bisagra = 0;
            if (Mathf.Abs(falta) > 0.03f && ruedas.Length == 2)
            {
                // de dos ruedas: se inclina sobre el eje hasta que la punta llegue; si además tiene bisagra (el carrito), como mucho
                // `InclinacionConBisagra` grados (que la carga no parezca caerse), y lo demás lo hace la bisagra
                var ejeP = carro.InverseTransformPoint(ruedas[0].position);
                var r = new Vector2(p.z - ejeP.z, p.y - ejeP.y);
                float objetivo = Mathf.Asin(Mathf.Clamp((apoyo.y - BajoElYugo - ejeP.y) / r.magnitude, -1, 1)) * Mathf.Rad2Deg;
                inclinacion = objetivo - Mathf.Atan2(r.y, r.x) * Mathf.Rad2Deg;
                if (varas != null) inclinacion = Mathf.Clamp(inclinacion, -InclinacionConBisagra, InclinacionConBisagra);
                // (sobre +x, un giro positivo baja lo de delante)
                var pivote = carro.TransformPoint(new Vector3(0, ejeP.y, ejeP.z));
                foreach (Transform hijo in carro) hijo.RotateAround(pivote, carro.right, -inclinacion);
                p = Punta();
                falta = apoyo.y - BajoElYugo - p.y;
            }
            if (Mathf.Abs(falta) > 0.03f && varas != null)
            {
                // varas con bisagra: giran sobre ella hasta que la punta llegue
                var b = carro.InverseTransformPoint(varas.transform.position);
                var r = new Vector2(p.z - b.z, p.y - b.y);
                float objetivo = Mathf.Asin(Mathf.Clamp((apoyo.y - BajoElYugo - b.y) / r.magnitude, -1, 1)) * Mathf.Rad2Deg;
                bisagra = objetivo - Mathf.Atan2(r.y, r.x) * Mathf.Rad2Deg;
                varas.transform.RotateAround(varas.transform.position, carro.right, -bisagra);
                p = Punta();
                falta = apoyo.y - BajoElYugo - p.y;
            }
            // delante: el apoyo, 2 cm por detrás de la punta (como en las láminas); el animal, en el suelo
            animal.localPosition = new Vector3(0, 0, p.z - apoyo.z - 0.02f);
            animal.localRotation = Quaternion.identity;
            return $"varas a {p.y:0.00} m, {(cruzDe != null ? "cruz" : "yugo")} a {apoyo.y:0.00} m" + (inclinacion != 0 ? $", inclinado {inclinacion:0.0}°" : "") + (bisagra != 0 ? $", varas giradas {bisagra:0.0}° en su bisagra" : "")
                   + (Mathf.Abs(falta) > 0.03f ? $", NO CASA: faltan {falta * 100:0} cm" : ", casa");
        }

        // ------------------------------------------------------------------ el Aeda (tema 7)

        /// <summary>El prefab del Aeda (`AedaSetup`), en Resources.</summary>
        public const string RutaAeda = "MapaMundo/Aeda/Aeda";

        /// <summary>El Aeda, andando y parado, con los clips de las levas tal cual y la corrección de brazos de `BrazosDelAeda` encima.</summary>
        TiraDeFiguras Aeda()
        {
            var prefab = Resources.Load<GameObject>(RutaAeda);
            if (prefab == null) return null;
            var taller = Taller();
            try
            {
                var go = Instanciar(prefab, taller);
                Limpiar(go, taller);
                var a = go.GetComponentInChildren<Animator>();
                if (a == null) return null;
                var brazos = new BrazosDelAeda(go.transform);    // (con el modelo aún en su reposo de Synty, antes de animar)
                taller.SetActive(true);
                var tira = new TiraDeFiguras { PasoDelClip = AndarTropa };
                var mats = new List<Material>();
                tira.Andar = Ciclo(new[] { a }, AndarTropa, 1, FotogramasAndar, go.transform, mats, () => brazos.Aplicar(true), out tira.DuracionAndar);
                tira.Reposo = Ciclo(new[] { a }, 0, 0, FotogramasReposo, go.transform, mats, () => brazos.Aplicar(false), out tira.DuracionReposo);
                return Terminar(tira, mats);
            }
            finally { Destroy(taller); }
        }

        /// <summary>La corrección de brazos del Aeda, encima de los clips de las levas (que son de herramienta a dos manos y la llevan al hombro;
        /// no se editan): el brazo derecho, pegado al costado y algo adelantado, con la mano en el agarre del bastón (IK de dos huesos con el
        /// codo atrás y abajo); el bastón, pieza rígida aparte, vertical con el pie en el suelo por delante y a la derecha del pie derecho y,
        /// andando, con un vaivén corto contrario a la pierna derecha; el brazo izquierdo, en reposo, colgando junto al muslo, y andando, con el
        /// balanceo del clip. Lo mismo que en Blender (`BronzeAgeArte/mapa/figuras/aeda/aeda.py`, `corregir_brazos`).</summary>
        sealed class BrazosDelAeda
        {
            const float Agarre = 1.04f, Delante = 0.25f, Lado = 0.12f, Vaiven = 0.05f, AtrasDelAgarre = 0.03f;
            /// <summary>La mano izquierda en reposo: tanto fuera del hueso del muslo y tanto por debajo (m).</summary>
            const float ManoFuera = 0.10f, ManoAbajo = 0.16f;
            readonly Transform raiz, baston, cadera, tobilloR, hombroR, codoR, manoR, hombroL, codoL, manoL, musloL;
            // (en el mundo: el taller no gira; la raíz del modelo puede traer el giro de ejes del FBX, así que no se usan sus ejes)
            readonly Vector3 pieDesdeCadera, adelante, derecha;
            readonly Quaternion manoLReposo, bastonReposo;
            /// <summary>La escala del esqueleto: el nodo `Rig_Leva` lleva el paso de cm a m de Synty (0,01), así que los huesos están a esa escala
            /// del origen del modelo y la malla se ve a su tamaño. Lo visible (el bastón, las medidas en m) va en el espacio del modelo.</summary>
            float e = 1;
            readonly float anchoReposo;
            readonly Vector3 escalaBaston;
            /// <summary>El largo del bastón (m): el FBX lo trae a la escala de cm de Synty y se lleva a su tamaño.</summary>
            const float LargoBaston = 1.5f;
            int medidas;
            Vector3 Ver(Vector3 hueso) => raiz.position + (hueso - raiz.position) / e;
            Vector3 Hueso(Vector3 visible) => raiz.position + (visible - raiz.position) * e;

            public BrazosDelAeda(Transform raiz)
            {
                this.raiz = raiz;
                var h = new Dictionary<string, Transform>();
                foreach (var t in raiz.GetComponentsInChildren<Transform>(true)) if (!h.ContainsKey(t.name)) h[t.name] = t;
                Transform T(string n) => h.TryGetValue(n, out var t) ? t : null;
                cadera = T("Hips"); tobilloR = T("Ankle_R"); hombroR = T("Shoulder_R"); codoR = T("Elbow_R"); manoR = T("Hand_R");
                hombroL = T("Shoulder_L"); codoL = T("Elbow_L"); manoL = T("Hand_L"); musloL = T("UpperLeg_L");
                baston = h.Values.FirstOrDefault(t => t.name.Contains("Baston"));
                // (la escala del esqueleto: al animarlo, el clip de las levas lo deja a otra escala que la del modelo en reposo; se mide en cada
                // pose por el ancho de hombros, y lo visible —la malla, el bastón— va a la del reposo)
                anchoReposo = (hombroR.position - hombroL.position).magnitude;
                if (baston != null && baston.GetComponent<MeshFilter>() is MeshFilter mb && mb.sharedMesh != null)
                {
                    var alto = Vector3.Scale(mb.sharedMesh.bounds.size, baston.lossyScale);
                    float largo = Mathf.Max(alto.x, alto.y, alto.z);
                    if (largo > 1e-4f) baston.localScale *= LargoBaston / largo;
                }
                if (baston != null) escalaBaston = baston.lossyScale;      // (la del mundo, que se mantiene: su padre puede cambiar de escala)
                if (cadera == null || tobilloR == null || manoR == null || manoL == null)
                {
                    Bitacora.Add($"  aeda: faltan huesos (cadera {cadera != null}, tobillo {tobilloR != null}, manos {manoR != null}/{manoL != null})");
                    return;
                }
                pieDesdeCadera = (tobilloR.position - cadera.position) / e;
                pieDesdeCadera.y = 0;
                manoLReposo = manoL.localRotation;
                if (baston != null) bastonReposo = baston.rotation;
                // hacia dónde mira la figura: de los talones a las puntas, en el suelo; su derecha, del hombro izquierdo al derecho
                var puntas = T("Toes_R") ?? T("Ball_R");
                adelante = puntas != null ? puntas.position - tobilloR.position : Vector3.forward;
                adelante.y = 0; adelante.Normalize();
                derecha = hombroR.position - hombroL.position;
                derecha.y = 0; derecha = (derecha - adelante * Vector3.Dot(derecha, adelante)).normalized;
            }

            public void Aplicar(bool andar)
            {
                if (cadera == null || manoR == null) return;
                e = Mathf.Max(1e-6f, (hombroR.position - hombroL.position).magnitude / anchoReposo);
                Vector3 adel = adelante, der = derecha, arriba = Vector3.up;
                var pie = Ver(cadera.position) + pieDesdeCadera + der * Lado + adel * Delante;
                pie.y = raiz.position.y;                                                    // (en el suelo)
                if (andar)
                    pie += adel * Mathf.Clamp(-Vaiven * Vector3.Dot(tobilloR.position - cadera.position, adel) / e / 0.30f, -Vaiven, Vaiven);
                var hom = Ver(hombroR.position);
                DosHuesos(hombroR, codoR, manoR, Hueso(pie + arriba * Agarre - adel * AtrasDelAgarre), Hueso(hom - der * 0.05f - adel * 0.45f - arriba * 0.45f));
                if (baston != null)
                {
                    baston.SetPositionAndRotation(pie, bastonReposo);
                    // (si cuelga de la armadura, su escala sigue a la del esqueleto: se le devuelve la suya)
                    var padre = baston.parent != null ? baston.parent.lossyScale.x : 1;
                    baston.localScale = escalaBaston / Mathf.Max(1e-6f, padre);
                }
                if (MedirPatas && medidas++ < 3)
                    Bitacora.Add($"  aeda {(andar ? "andando" : "parado")}: raíz {raiz.position}, cadera {cadera.position}, hombro {hombroR.position}, codo {codoR.position}, mano {manoR.position}, " +
                                 $"objetivo {pie + arriba * Agarre - adel * AtrasDelAgarre}, mano vista {Ver(manoR.position)}, escala {e}, bastón {(baston != null ? baston.name + " " + baston.position + " " + baston.lossyScale + " " + (baston.GetComponent<Renderer>()?.bounds.size.ToString() ?? "-") : "NO")}");
                if (!andar && musloL != null)
                {
                    DosHuesos(hombroL, codoL, manoL, Hueso(Ver(musloL.position) - der * ManoFuera - arriba * ManoAbajo),
                              Hueso(Ver(hombroL.position) + der * 0.05f - adel * 0.45f - arriba * 0.45f));
                    manoL.localRotation = manoLReposo;
                }
            }

            /// <summary>IK de dos huesos (los del brazo de Synty no están unidos): el codo, en el plano del hombro, el objetivo y el polo.</summary>
            static void DosHuesos(Transform hombro, Transform codo, Transform mano, Vector3 objetivo, Vector3 polo)
            {
                Vector3 s = hombro.position, e = codo.position, h = mano.position;
                float a = (e - s).magnitude, b = (h - e).magnitude;
                float d = Mathf.Clamp((objetivo - s).magnitude, Mathf.Abs(a - b) + 1e-3f, a + b - 1e-3f);
                var eje = (objetivo - s).normalized;
                var pv = Vector3.ProjectOnPlane(polo - s, eje).normalized;
                float ca = (a * a + d * d - b * b) / (2 * a * d);
                var e2 = s + eje * a * ca + pv * a * Mathf.Sqrt(Mathf.Max(0, 1 - ca * ca));
                hombro.rotation = Quaternion.FromToRotation(e - s, e2 - s) * hombro.rotation;
                var e3 = codo.position;
                codo.rotation = Quaternion.FromToRotation(mano.position - e3, s + eje * d - e3) * codo.rotation;
            }
        }

        Material Copia(Material m, Color? tinte)
        {
            if (!materiales.TryGetValue((m, tinte), out var c))
            {
                // con textura: el sombreado del mapa (su luz y su bruma) y, en los atlas de tropa, el color de bando por el alfa; sin ella, el suyo
                var tex = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : m.HasProperty("_MainTex") ? m.GetTexture("_MainTex") : null;
                if (tex != null && sombreadoFigura == null) sombreadoFigura = Resources.Load<Shader>("MapaMundo/MapaFigura");
                if (tex != null && sombreadoFigura != null)
                {
                    c = new Material(sombreadoFigura) { enableInstancing = true, name = m.name + " (mapa)" };
                    c.SetTexture("_BaseMap", tex);
                    var basico = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white;
                    c.SetColor("_BaseColor", tinte ?? basico);
                    c.SetFloat("_Mascara", m.name.EndsWith("_Atlas", StringComparison.Ordinal) ? 1 : 0);
                }
                else
                {
                    c = new Material(m) { enableInstancing = true, name = m.name + " (mapa)" };
                    if (tinte != null)
                    {
                        if (c.HasProperty("_BaseColor")) c.SetColor("_BaseColor", tinte.Value);
                        if (c.HasProperty("_Color")) c.SetColor("_Color", tinte.Value);
                    }
                }
                materiales[(m, tinte)] = c;
            }
            return c;
        }

        // (perezoso: Unity no deja crearlo en el inicializador estático de un MonoBehaviour, que corre al añadir el componente)
        static MaterialPropertyBlock bloque_;
        static MaterialPropertyBlock bloque => bloque_ ??= new MaterialPropertyBlock();
        static Shader sombreadoFigura;

        readonly Dictionary<Mesh, Mesh> legibles = new Dictionary<Mesh, Mesh>();
        SkinnedMeshRenderer lector;

        /// <summary>Una copia legible de una malla rígida (la de las armas: FBX sin lectura), una vez por malla.</summary>
        Mesh Legible(Mesh m)
        {
            if (m.isReadable) return m;
            if (legibles.TryGetValue(m, out var c)) return c;
            if (lector == null)
            {
                var go = new GameObject("Lector de mallas") { hideFlags = HideFlags.HideAndDontSave };
                go.transform.position = new Vector3(0, -6000, 0);
                lector = go.AddComponent<SkinnedMeshRenderer>();
                lector.forceRenderingOff = true;
            }
            lector.sharedMesh = m;
            c = new Mesh { indexFormat = IndexFormat.UInt32, name = m.name + " (legible)" };
            lector.BakeMesh(c, true);
            legibles[m] = c;
            return c;
        }

        /// <summary>La pose de ahora, en una malla con un submesh por material (en el orden de `mats`; los nuevos, al final).</summary>
        Mesh Cocer(Transform raiz, List<Material> mats)
        {
            var grupos = new Dictionary<Material, List<CombineInstance>>();
            var temporales = new List<Mesh>();
            var aRaiz = raiz.worldToLocalMatrix;
            foreach (var r in raiz.GetComponentsInChildren<Renderer>(false))
            {
                if (!r.enabled) continue;   // (los apagados en el prefab: ayudas de colocación, cubos de prueba…)
                // (el cubo de Unity que traen algunos modelos de tropa, con el Lit por defecto: no es del modelo)
                if (r is MeshRenderer && r.TryGetComponent<MeshFilter>(out var cubo) && cubo.sharedMesh != null && cubo.sharedMesh.name == "Cube"
                    && r.sharedMaterials.All(m => m != null && m.name == "Lit")) continue;
                Mesh m;
                Matrix4x4 M;
                if (r is SkinnedMeshRenderer s)
                {
                    if (s.sharedMesh == null) continue;
                    m = new Mesh();
                    temporales.Add(m);
                    s.BakeMesh(m, true);
                    // (la pose cocida queda en el espacio local de la pieza, sin su escala: muchas piezas de Synty van a ×0,01)
                    M = aRaiz * s.transform.localToWorldMatrix;
                }
                else if (r is MeshRenderer && r.TryGetComponent<MeshFilter>(out var mf) && mf.sharedMesh != null)
                {
                    // (lo rígido colgado de un hueso: el arma, el escudo… Viene de FBX sin lectura y CombineMeshes no lo junta: se lee
                    // cociéndolo con un SkinnedMeshRenderer de paso, sin huesos)
                    m = Legible(mf.sharedMesh);
                    if (m == null) continue;
                    M = aRaiz * r.transform.localToWorldMatrix;
                }
                else continue;
                Color? tinte = null;
                if (r.HasPropertyBlock())
                {
                    r.GetPropertyBlock(bloque);
                    if (bloque.HasColor("_BaseColor")) tinte = bloque.GetColor("_BaseColor");
                }
                var sm = r.sharedMaterials;
                for (int i = 0; i < m.subMeshCount; i++)
                {
                    var mat = sm.Length == 0 ? null : sm[Mathf.Min(i, sm.Length - 1)];
                    if (mat == null) continue;
                    var c = Copia(mat, tinte);
                    if (!grupos.TryGetValue(c, out var l)) grupos[c] = l = new List<CombineInstance>();
                    l.Add(new CombineInstance { mesh = m, subMeshIndex = i, transform = M });
                }
                if (Diagnostico)
                {
                    var b = m.bounds;
                    var alto = (M.MultiplyPoint3x4(b.max) - M.MultiplyPoint3x4(b.min)).magnitude;
                    Bitacora.Add($"  pieza {r.name} ({r.GetType().Name}) tamaño {alto:0.00} m, escala {r.transform.lossyScale}, raíz {raiz.lossyScale}");
                }
            }
            foreach (var c in grupos.Keys) if (!mats.Contains(c)) mats.Add(c);
            var partes = new List<CombineInstance>();
            foreach (var c in mats)
            {
                var p = new Mesh { indexFormat = IndexFormat.UInt32 };
                if (grupos.TryGetValue(c, out var l)) p.CombineMeshes(l.ToArray(), true, true);
                partes.Add(new CombineInstance { mesh = p });
            }
            var final = new Mesh { indexFormat = IndexFormat.UInt32, name = "figura" };
            final.CombineMeshes(partes.ToArray(), false, false);
            foreach (var t in temporales) Destroy(t);
            foreach (var p in partes) Destroy(p.mesh);
            final.RecalculateBounds();
            final.UploadMeshData(true);
            return final;
        }

        static TiraDeFiguras Terminar(TiraDeFiguras t, List<Material> mats)
        {
            t.Materiales = mats.ToArray();
            var m = t.Andar.Length > 0 ? t.Andar[0] : null;
            t.Triangulos = m == null ? 0 : Enumerable.Range(0, m.subMeshCount).Sum(i => (int)m.GetIndexCount(i) / 3);
            t.Alto = m == null ? 1.8f : m.bounds.max.y;
            return t;
        }
    }
}
