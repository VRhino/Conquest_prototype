// La ruta y el destino en el mapa de mundo (ronda 2 de «Escalas y ruta», 2026-11-23), en vez de la banda naranja y la caja roja de antes:
//  * Tu columna: la mira bajo ella (un aro crema, la del prototipo), su ruta en cinta punteada crema con filo oscuro (trazos de 0,5 u y
//    huecos de 0,35 u sobre una cinta de 0,12 u), y en el destino un aro de 0,75 u de radio con la lanza clavada (1,6 u de alto) y su
//    cinta del color de la Facción, que se mece. Si el destino es un asentamiento (o cualquier cosa fija con huella), la marca se planta
//    fuera de su huella (`CosaDelMapa.Radio`, la de «Distancias de plaza»), sobre la línea por la que llega la ruta, con el aro entero y
//    tangente al borde (su centro a huella + radio del aro), y la ruta acaba en ella. Es solo dibujo: el motor lleva la columna al centro.
//  * Las demás columnas de la Facción y las caravanas propias: su rastro, punteado más largo y más tenue (las caravanas, más aún), como el
//    prototipo (`mundo3d/vista.ts`).
// Todo va a la escala de lo que se mueve (`ColumnasDelMapa.FactorDeFiguras`), por `AnchoDeCinta` y `TamanoDeMarca` (la previa los mueve).
// Las cintas usan `MapaRayaMejorada` (el trazo con extremos redondos, el filo y el ancho mínimo en píxeles los hace el sombreado); la lanza,
// `MapaFigura` con su atlas (`Resources/MapaMundo/Destino`).
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ConquestTactics.Mundo.Mapa
{
    public sealed class RutasDelMapa
    {
        public enum Clase { Mia, Faccion, Caravana }

        /// <summary>El ancho de la cinta de la ruta (u, con el filo), antes del factor de lo que se mueve.</summary>
        public static double AnchoDeCinta = 0.12;
        /// <summary>El tamaño de la marca del destino (1: la lanza de 1,6 u y el aro de 0,75 u de radio), antes del factor de lo que se mueve.</summary>
        public static double TamanoDeMarca = 1;
        public const double RadioDelAro = 0.75, AltoDeLanza = 1.6, RadioDeMira = 0.45, AnchoDeMira = 0.05;
        /// <summary>Los trazos (lleno y periodo, u): los de tu ruta y los del rastro de las demás.</summary>
        const double LlenoMia = 0.5, PeriodoMia = 0.85, LlenoOtras = 0.6, PeriodoOtras = 1.0;
        /// <summary>El filo oscuro del sombreado es 0,35 del medio ancho de la línea a cada lado: la línea es el ancho total / 1,35.</summary>
        const double ConFilo = 1.35;
        /// <summary>Sobre el suelo pintado (m) y adelanto en el z-buffer, por encima de las rayas del territorio.</summary>
        const double Sobre = 0.1;
        const float Desplazar = -5;
        static readonly Color Crema = new Color32(0xf1, 0xe6, 0xc8, 255);

        public static double Factor => ColumnasDelMapa.FactorDeFiguras;

        readonly Func<double, double, float> alturaPintada, alturaVisible;
        readonly Transform padre;
        readonly Material materialMia, materialOtras;
        Mesh mallaLanza;
        Material materialLanza, materialCinta;
        int subLanza = 0, subCinta = 1;

        sealed class Pieza
        {
            public GameObject Go;
            public Mesh Malla;
            public string Clave;
            public float Hecha;
            public bool Usada;
            public int Puntos;
        }

        sealed class Lanza
        {
            public GameObject Go;
            public Material Cinta;
            public bool Usada;
        }

        readonly Dictionary<string, Pieza> piezas = new Dictionary<string, Pieza>();
        readonly Dictionary<string, Lanza> lanzas = new Dictionary<string, Lanza>();

        public RutasDelMapa(Transform padre, Func<double, double, float> alturaPintada, Func<double, double, float> alturaVisible)
        {
            this.padre = padre;
            this.alturaPintada = alturaPintada;
            this.alturaVisible = alturaVisible;
            var sombreado = Resources.Load<Shader>("MapaMundo/MapaRayaMejorada");
            materialMia = Raya(sombreado, "ruta (tuya)", LlenoMia, PeriodoMia);
            materialOtras = Raya(sombreado, "ruta (de la Facción)", LlenoOtras, PeriodoOtras);
        }

        static Material Raya(Shader s, string nombre, double lleno, double periodo)
        {
            var m = new Material(s) { name = nombre };
            m.SetFloat("_ConNiebla", 0);
            m.SetFloat("_Desplazar", Desplazar);
            m.SetFloat("_MinPx", 2);
            m.SetFloat("_Lleno", (float)(lleno * Factor));
            m.SetFloat("_Periodo", (float)(periodo * Factor));
            return m;
        }

        /// <summary>Al empezar el fotograma: todo sin usar.</summary>
        public void Empezar()
        {
            foreach (var p in piezas.Values) p.Usada = false;
            foreach (var l in lanzas.Values) l.Usada = false;
            materialMia.SetFloat("_Lleno", (float)(LlenoMia * Factor));
            materialMia.SetFloat("_Periodo", (float)(PeriodoMia * Factor));
            materialOtras.SetFloat("_Lleno", (float)(LlenoOtras * Factor));
            materialOtras.SetFloat("_Periodo", (float)(PeriodoOtras * Factor));
        }

        /// <summary>Al acabar: se apaga lo que no se ha usado.</summary>
        public void Terminar()
        {
            foreach (var p in piezas.Values) if (!p.Usada && p.Go.activeSelf) p.Go.SetActive(false);
            foreach (var l in lanzas.Values) if (!l.Usada && l.Go.activeSelf) l.Go.SetActive(false);
        }

        /// <summary>Puntos de la cinta de la ruta `id` pintada en este fotograma (0 sin ella).</summary>
        public int PuntosDe(string id) => piezas.TryGetValue("ruta|" + id, out var p) && p.Usada ? p.Puntos : 0;

        /// <summary>Dónde se planta la marca de la ruta `id` (u), si la hay.</summary>
        public (double x, double y)? MarcaDe(string id) => lanzas.TryGetValue(id, out var l) && l.Usada ? ((double, double)?)Desde(l.Go.transform.localPosition) : null;

        static (double, double) Desde(Vector3 p) { var (x, _, y) = EscalaDelMapa.AlMapa(p); return (x, y); }

        /// <summary>La ruta `id` (de donde está a su destino, en u). La tuya, con su mira, y su destino con el aro y la lanza (la cinta, de
        /// `colorFaccion`); si `huella` (centro y radio de lo que hay en el destino), cortada fuera de ella: la tuya, con la marca tangente a su
        /// borde (a huella + radio del aro); las demás, en el borde.</summary>
        public void Ruta(string id, IReadOnlyList<(double x, double y)> puntos, Clase clase, Color colorFaccion = default, (double x, double y, double r)? huella = null)
        {
            if (puntos == null || puntos.Count < 2) return;
            double f = Factor, ancho = AnchoDeCinta * f, radioAro = RadioDelAro * TamanoDeMarca * f;
            var l = huella is var (hx, hy, hr) ? Cortar(puntos, hx, hy, clase == Clase.Mia ? hr + radioAro : hr) : new List<(double x, double y)>(puntos);
            if (clase == Clase.Mia)
            {
                Aro("mira|" + id, l[0], RadioDeMira * f, AnchoDeMira * f, 1);
                if (l.Count < 2) return;
                var fin = l[l.Count - 1];
                Aro("aro|" + id, fin, radioAro, ancho, 1);
                PonerLanza(id, fin, colorFaccion);
            }
            if (l.Count < 2) return;
            float alfa = clase == Clase.Mia ? 0.9f : clase == Clase.Faccion ? 0.6f : 0.35f;
            Cinta("ruta|" + id, l, false, ancho, alfa, true, clase == Clase.Mia ? materialMia : materialOtras);
        }

        /// <summary>La ruta hasta el borde de la huella (cx, cy, r): del primer tramo que entra en ella, hasta el punto en que entra. Si ya se
        /// empieza dentro, solo el primer punto.</summary>
        public static List<(double x, double y)> Cortar(IReadOnlyList<(double x, double y)> puntos, double cx, double cy, double r)
        {
            double D((double x, double y) p) => Math.Sqrt((p.x - cx) * (p.x - cx) + (p.y - cy) * (p.y - cy));
            var l = new List<(double x, double y)> { puntos[0] };
            if (D(puntos[0]) <= r) return l;
            for (int i = 1; i < puntos.Count; i++)
            {
                var (a, b) = (puntos[i - 1], puntos[i]);
                if (D(b) > r) { l.Add(b); continue; }
                // de a (fuera) a b (dentro): donde el tramo cruza el círculo
                double dx = b.x - a.x, dy = b.y - a.y, ex = a.x - cx, ey = a.y - cy;
                double qa = dx * dx + dy * dy, qb = 2 * (ex * dx + ey * dy), qc = ex * ex + ey * ey - r * r;
                double t = qa > 0 ? (-qb - Math.Sqrt(Math.Max(0, qb * qb - 4 * qa * qc))) / (2 * qa) : 0;
                t = Math.Max(0, Math.Min(1, t));
                l.Add((a.x + dx * t, a.y + dy * t));
                break;
            }
            return l;
        }

        void Aro(string clave, (double x, double y) c, double radio, double ancho, float alfa)
        {
            const int Lados = 40;
            var l = new List<(double x, double y)>(Lados + 1);
            for (int k = 0; k <= Lados; k++) { double a = k * 2 * Math.PI / Lados; l.Add((c.x + radio * Math.Cos(a), c.y + radio * Math.Sin(a))); }
            Cinta(clave, l, true, ancho, alfa, false, materialMia);
        }

        /// <summary>Una cinta sobre el suelo por `l` (u): la malla de `MapaRayaMejorada` (como las rayas del territorio). Se rehace si cambia lo
        /// que la hace o cada segundo (el suelo se afina al cargar).</summary>
        void Cinta(string clave, List<(double x, double y)> l, bool cerrada, double ancho, float alfa, bool aTrazos, Material material)
        {
            if (!piezas.TryGetValue(clave, out var p))
            {
                p = new Pieza { Go = new GameObject(clave), Malla = new Mesh { name = clave, indexFormat = IndexFormat.UInt32 } };
                p.Go.transform.SetParent(padre, false);
                p.Go.AddComponent<MeshFilter>().sharedMesh = p.Malla;
                var mr = p.Go.AddComponent<MeshRenderer>();
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
                piezas[clave] = p;
            }
            p.Usada = true;
            if (!p.Go.activeSelf) p.Go.SetActive(true);
            p.Go.GetComponent<MeshRenderer>().sharedMaterial = material;
            var a = l[0];
            var z = l[l.Count - 1];
            var firma = $"{l.Count}|{a.x:0.00},{a.y:0.00}|{z.x:0.00},{z.y:0.00}|{ancho:0.000}|{alfa}";
            if (firma == p.Clave && Time.unscaledTime - p.Hecha < 1) return;
            p.Clave = firma;
            p.Hecha = Time.unscaledTime;
            p.Puntos = Hacer(p.Malla, l, cerrada, ancho, alfa, aTrazos);
        }

        int Hacer(Mesh malla, List<(double x, double y)> l, bool cerrada, double ancho, float alfa, bool aTrazos)
        {
            // muestras cada 0,25 u; lo andado se cuenta desde el final (los trazos no corren mientras la ruta se acorta por delante)
            var xs = new List<double>();
            var ys = new List<double>();
            var s = new List<double>();
            double total = 0;
            for (int i = 1; i < l.Count; i++) total += Math.Sqrt((l[i].x - l[i - 1].x) * (l[i].x - l[i - 1].x) + (l[i].y - l[i - 1].y) * (l[i].y - l[i - 1].y));
            double andado = 0;
            xs.Add(l[0].x); ys.Add(l[0].y); s.Add(total);
            for (int i = 1; i < l.Count; i++)
            {
                var (a, b) = (l[i - 1], l[i]);
                double d = Math.Sqrt((b.x - a.x) * (b.x - a.x) + (b.y - a.y) * (b.y - a.y));
                if (d == 0) continue;
                int n = Math.Max(1, (int)Math.Ceiling(d / 0.25));
                for (int k = 1; k <= n; k++) { xs.Add(a.x + (b.x - a.x) * k / n); ys.Add(a.y + (b.y - a.y) * k / n); s.Add(total - andado - d * k / n); }
                andado += d;
            }
            int cuantas = xs.Count;
            var e = EscalaDelMapa.MetrosPorUnidad;
            double medio = ancho / ConFilo / 2, mallaU = medio * 1.5;
            var color = Crema.linear;
            color.a = alfa;
            var extra = new Vector4(aTrazos ? 1 : 0, 0, 0, 0);
            var V = new List<Vector3>(cuantas * 2);
            var N = new List<Vector3>(cuantas * 2);
            var T = new List<Vector4>(cuantas * 2);
            var Uv = new List<Vector2>(cuantas * 2);
            var Ex = new List<Vector4>(cuantas * 2);
            var Me = new List<Vector2>(cuantas * 2);
            var C = new List<Color>(cuantas * 2);
            var tri = new List<int>(cuantas * 6);
            for (int k = 0; k < cuantas; k++)
            {
                int ia = k - 1, ib = k + 1;
                if (cerrada) { if (ia < 0) ia = cuantas - 2; if (ib >= cuantas) ib = 1; }
                else { ia = Math.Max(0, ia); ib = Math.Min(cuantas - 1, ib); }
                double tx = xs[ib] - xs[ia], ty = ys[ib] - ys[ia], ll = Math.Sqrt(tx * tx + ty * ty);
                if (ll == 0) ll = 1;
                double ux = -ty / ll, uy = tx / ll, x = xs[k], y = ys[k];
                double hc = alturaPintada(x, y), hi = alturaPintada(x + ux * mallaU, y + uy * mallaU), hd = alturaPintada(x - ux * mallaU, y - uy * mallaU);
                double subir = Math.Max(0, hc - (hi + hd) / 2);
                var hacia = new Vector3((float)-ux, 0, (float)uy);
                foreach (var lado in new[] { 1, -1 })
                {
                    V.Add(EscalaDelMapa.AEscena(x + lado * ux * mallaU, y + lado * uy * mallaU, (float)((lado > 0 ? hi : hd) + subir + Sobre * e)));
                    N.Add(Vector3.up);
                    T.Add(new Vector4(hacia.x, hacia.y, hacia.z, (float)(-lado * mallaU * e)));
                    Uv.Add(new Vector2(-lado, (float)s[k]));
                    Ex.Add(extra);
                    Me.Add(new Vector2((float)medio, (float)mallaU));
                    C.Add(color);
                }
                if (k > 0) { int a0 = 2 * (k - 1); tri.AddRange(new[] { a0, a0 + 1, a0 + 2, a0 + 1, a0 + 3, a0 + 2 }); }
            }
            malla.Clear();
            malla.SetVertices(V);
            malla.SetNormals(N);
            malla.SetTangents(T);
            malla.SetUVs(0, Uv);
            malla.SetUVs(1, Ex);
            malla.SetUVs(2, Me);
            malla.SetColors(C);
            malla.SetTriangles(tri, 0);
            malla.RecalculateBounds();
            return cuantas;
        }

        // ------------------------------------------------------------------ la lanza

        void PonerLanza(string id, (double x, double y) p, Color colorFaccion)
        {
            if (!Cargar()) return;
            if (!lanzas.TryGetValue(id, out var l))
            {
                l = new Lanza { Go = new GameObject("lanza|" + id), Cinta = new Material(materialCinta) { name = "cinta|" + id } };
                l.Go.transform.SetParent(padre, false);
                l.Go.AddComponent<MeshFilter>().sharedMesh = mallaLanza;
                var mr = l.Go.AddComponent<MeshRenderer>();
                var mats = new Material[2];
                mats[subLanza] = materialLanza;
                mats[subCinta] = l.Cinta;
                mr.sharedMaterials = mats;
                mr.shadowCastingMode = PinturaDelMapa.SombrasReales ? ShadowCastingMode.On : ShadowCastingMode.Off;
                // un rumbo fijo por ruta: la cinta, de lado a la cámara de partida
                l.Go.transform.localRotation = Quaternion.Euler(0, 35 + (id.GetHashCode() & 0x1f), 0);
                lanzas[id] = l;
            }
            l.Usada = true;
            if (!l.Go.activeSelf) l.Go.SetActive(true);
            if (colorFaccion.a > 0) l.Cinta.SetColor("_BaseColor", colorFaccion);
            var e = EscalaDelMapa.MetrosPorUnidad;
            l.Go.transform.localPosition = EscalaDelMapa.AEscena(p.x, p.y, alturaVisible(p.x, p.y));
            l.Go.transform.localScale = Vector3.one * (float)(AltoDeLanza * TamanoDeMarca * Factor * e);
        }

        /// <summary>La malla de la lanza (con lo que el FBX trae en su jerarquía, de 1 de alto sobre su pie) y sus dos materiales.</summary>
        bool Cargar()
        {
            if (mallaLanza != null) return true;
            if (materialLanza != null) return false; // ya se intentó
            var sombreado = Resources.Load<Shader>("MapaMundo/MapaFigura");
            var atlas = Resources.Load<Texture2D>("MapaMundo/Destino/lanza");
            materialLanza = new Material(sombreado) { name = "lanza" };
            materialLanza.SetTexture("_BaseMap", atlas);
            materialLanza.SetFloat("_ConNiebla", 0);
            materialCinta = new Material(materialLanza) { name = "cinta" };
            materialCinta.SetFloat("_Mece", 1);
            materialCinta.SetFloat("_Cull", 0);
            var raiz = Resources.Load<GameObject>("MapaMundo/Destino/Destino");
            var mf = raiz != null ? raiz.GetComponentInChildren<MeshFilter>() : null;
            if (mf == null || mf.sharedMesh == null) { Debug.LogWarning("[Mapa] sin la lanza del destino (Resources/MapaMundo/Destino/Destino)"); return false; }
            var mats = mf.GetComponent<MeshRenderer>()?.sharedMaterials;
            if (mats != null && mats.Length == 2 && mats[0] != null && mats[0].name.StartsWith("Cinta")) (subLanza, subCinta) = (1, 0);
            var m = UnityEngine.Object.Instantiate(mf.sharedMesh);
            m.name = "lanza del destino";
            // (el giro de ejes del FBX va en el transform de la raíz o del hijo: todo menos el sitio)
            var a = mf.transform.localToWorldMatrix;
            a.SetColumn(3, new Vector4(0, 0, 0, 1));
            var v = m.vertices;
            var n = m.normals;
            for (int i = 0; i < v.Length; i++) v[i] = a.MultiplyPoint3x4(v[i]);
            for (int i = 0; i < n.Length; i++) n[i] = a.MultiplyVector(n[i]).normalized;
            float alto = 0;
            foreach (var q in v) alto = Mathf.Max(alto, q.y);
            float k = alto > 0 ? 1 / alto : 1;
            for (int i = 0; i < v.Length; i++) v[i] *= k;
            m.vertices = v;
            m.normals = n;
            m.RecalculateBounds();
            mallaLanza = m;
            return true;
        }

        public void Destruir()
        {
            foreach (var p in piezas.Values) { UnityEngine.Object.Destroy(p.Malla); UnityEngine.Object.Destroy(p.Go); }
            foreach (var l in lanzas.Values) { UnityEngine.Object.Destroy(l.Cinta); UnityEngine.Object.Destroy(l.Go); }
            piezas.Clear();
            lanzas.Clear();
            UnityEngine.Object.Destroy(materialMia);
            UnityEngine.Object.Destroy(materialOtras);
            if (materialLanza != null) UnityEngine.Object.Destroy(materialLanza);
            if (materialCinta != null) UnityEngine.Object.Destroy(materialCinta);
            if (mallaLanza != null) UnityEngine.Object.Destroy(mallaLanza);
        }
    }
}
