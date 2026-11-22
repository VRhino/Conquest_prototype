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
        /// avanzados a mano; `n` poses repartidas en lo que dura el estado, cada una cocida en una malla.</summary>
        Mesh[] Ciclo(Animator[] animators, float v, float aire, int n, Transform raiz, List<Material> mats, Action tras, out float duracion)
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
            duracion = Mathf.Max(0.2f, animators[0].GetCurrentAnimatorStateInfo(0).length);
            var r = new Mesh[n];
            for (int i = 0; i < n; i++)
            {
                tras?.Invoke();
                r[i] = Cocer(raiz, mats);
                foreach (var a in animators) a.Update(duracion / n);
            }
            return r;
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
