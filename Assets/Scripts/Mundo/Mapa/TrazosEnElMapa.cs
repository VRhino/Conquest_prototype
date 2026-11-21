// Las rayas sobre el suelo del mapa de mundo: caminos (con la textura de su escalón) y las líneas del territorio, como las del prototipo
// (BronzeAgeClient: `mundo3d/trazos.ts` y `cinta` en `mundo3d/lineas.ts`). Cada raya, recortada a una caja alrededor del héroe, se hace cinta:
// una muestra cada 0,5 u con un vértice a cada lado, cada borde a la altura de su suelo pintado y toda ella subida si el centro asoma; los
// caminos, con la textura de lado a lado (u) y a lo largo (v = lo andado / 4 × ancho, contando desde el principio del camino aunque se recorte);
// las líneas a trazos, sin los cuadros que caen en el hueco. Se rehacen si cambian, si el héroe se aleja más de un cuarto de la caja o si
// cambia el suelo de debajo: cada trozo de cinta cuyo suelo cambió, aparte, primero los más cercanos al héroe y con un presupuesto por
// fotograma. El camino lleva por vértice (UV1.x) la sombra del relieve, como el suelo.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace ConquestTactics.Mundo.Mapa
{
    public sealed class TrazosEnElMapa
    {
        /// <summary>Lo que sube cada clase sobre el suelo (u) y cuánto se adelanta en el z-buffer (`CAPA`).</summary>
        static (double dy, float desplazar) Capa(TipoDeTrazo t) => t switch
        {
            TipoDeTrazo.Camino => (0.04, -2),
            TipoDeTrazo.Frontera => (0.06, -3),
            _ => (0.08, -4),
        };
        /// <summary>Raya discontinua: lleno y hueco (u).</summary>
        const double Lleno = 1.2, Hueco = 0.7, Paso = 0.5;
        /// <summary>Lo más que se gasta por fotograma en rehacer trozos cuyo suelo cambió (ms; al menos uno por fotograma).</summary>
        const double PresupuestoMs = 2;

        /// <summary>Un trozo de una raya dentro de la caja: su malla, y la caja y firma del suelo de debajo con que se hizo.</summary>
        sealed class Trozo
        {
            public Trazo Trazo;
            public List<(double x, double y)> Puntos;
            public double Desde;
            public (double x0, double y0, double x1, double y1) Caja;
            public int Firma;
            public GameObject Go;
            /// <summary>(«Mejorado») lo que va sobre el agua de un vado: los mismos vértices, pintados después del agua (si no, el agua lo tapa).</summary>
            public GameObject GoVado;
            public int Vertices;
        }

        readonly Transform padre;
        readonly TerrenoDelMapa terreno;
        readonly Relieve relieve;
        readonly Dictionary<string, Material> materiales = new Dictionary<string, Material>();
        readonly List<Trozo> trozos = new List<Trozo>();
        readonly List<Trozo> pendientes = new List<Trozo>();
        List<Trazo> trazos = new List<Trazo>();
        string clave = "";
        double centroX = double.NaN, centroY = double.NaN, radio;
        int version = -1;
        /// <summary>La sombra del relieve en la rejilla de 2 u (la misma que el suelo), guardada: los caminos no cambian de sitio.</summary>
        readonly Dictionary<(long, long), float> relieveSol = new Dictionary<(long, long), float>();

        public TrazosEnElMapa(Transform padre, TerrenoDelMapa terreno, Relieve relieve)
        {
            this.padre = padre;
            this.terreno = terreno;
            this.relieve = relieve;
        }

        /// <summary>Cuántos vértices tienen las cintas puestas (para las medidas).</summary>
        public int Vertices => trozos.Sum(t => t.Vertices) + parches.Sum(p => p.GetComponent<MeshFilter>().sharedMesh.vertexCount);

        /// <summary>Cómo se pintan los caminos: como el prototipo (etapa 1) o «Mejorado» (el camino es suelo: `MapaCaminoMejorado`, con parches en
        /// los cruces y los extremos). Las líneas del territorio no cambian. Se lee al hacer las cintas.</summary>
        public enum EstiloDeTrazos { Prototipo, Mejorado }
        public static EstiloDeTrazos Estilo = EstiloDeTrazos.Mejorado;
        static bool Mejorado(Trazo t) => Estilo == EstiloDeTrazos.Mejorado && t.Tipo == TipoDeTrazo.Camino;
        /// <summary>La malla del camino «Mejorado», × el ancho (el halo de terreno pisado), y su periodo a lo largo, × el ancho (como lo horneado).</summary>
        const double Franja = 1.8, Periodos = 6;
        readonly List<GameObject> parches = new List<GameObject>();

        public void Fijar(List<Trazo> nuevos)
        {
            var k = string.Join("|", nuevos.Select(t =>
            {
                var a = t.Puntos[0];
                var b = t.Puntos[t.Puntos.Count - 1];
                return $"{t.Tipo}:{t.Color}:{t.BajoNiebla}:{t.Discontinuo}:{t.HaciaDentro}:{t.Tenue}:{t.Ancho}:{t.Escalon}:{t.Puntos.Count}:{a.x},{a.y}:{b.x},{b.y}";
            }));
            if (k == clave) return;
            clave = k;
            trazos = nuevos;
            centroX = double.NaN; // a rehacer
        }

        /// <summary>Rehace las cintas si cambiaron o si (cx, cy) se alejó más de un cuarto de `radioU` de donde se hicieron; recorta a `radioU` u.</summary>
        public void Actualizar(double cx, double cy, double radioU)
        {
            if (!double.IsNaN(centroX) && Math.Sqrt((cx - centroX) * (cx - centroX) + (cy - centroY) * (cy - centroY)) < radio / 4)
            {
                if (terreno.Version != version)
                {
                    // los trozos cuyo suelo cambió, a la cola; el más cercano al héroe, primero
                    version = terreno.Version;
                    foreach (var t in trozos)
                        if (!pendientes.Contains(t) && terreno.Firma(t.Caja.x0, t.Caja.y0, t.Caja.x1, t.Caja.y1) != t.Firma) pendientes.Add(t);
                    double D(Trozo t) => Math.Max(Math.Abs((t.Caja.x0 + t.Caja.x1) / 2 - cx) - (t.Caja.x1 - t.Caja.x0) / 2, 0)
                                       + Math.Max(Math.Abs((t.Caja.y0 + t.Caja.y1) / 2 - cy) - (t.Caja.y1 - t.Caja.y0) / 2, 0);
                    pendientes.Sort((a, b) => D(a).CompareTo(D(b)));
                }
                if (pendientes.Count > 0)
                {
                    var reloj = System.Diagnostics.Stopwatch.StartNew();
                    int k = 0;
                    while (k < pendientes.Count && (k == 0 || reloj.Elapsed.TotalMilliseconds < PresupuestoMs)) HacerTrozo(pendientes[k++]);
                    pendientes.RemoveRange(0, k);
                    PeorMsTrozos = Math.Max(PeorMsTrozos, reloj.Elapsed.TotalMilliseconds);
                    TrozosRehechos += k;
                }
                return;
            }
            centroX = cx;
            centroY = cy;
            radio = radioU;
            Hacer();
        }

        /// <summary>Para las pruebas: lo más que ha tardado en rehacer las cintas (ms) y cuántas veces.</summary>
        public double PeorMsHacer { get; private set; }
        public int Hechas { get; private set; }
        /// <summary>Para las pruebas: los trozos rehechos porque cambió su suelo y lo más que se gastó en un fotograma en ellos (ms).</summary>
        public int TrozosRehechos { get; private set; }
        public double PeorMsTrozos { get; private set; }

        void Hacer()
        {
            var reloj = System.Diagnostics.Stopwatch.StartNew();
            HacerYa();
            PeorMsHacer = Math.Max(PeorMsHacer, reloj.Elapsed.TotalMilliseconds);
            Hechas++;
        }

        void HacerYa()
        {
            version = terreno.Version;
            foreach (var t in trozos) { Soltar(t.Go); Soltar(t.GoVado); }
            trozos.Clear();
            pendientes.Clear();
            foreach (var g in parches) Soltar(g);
            parches.Clear();
            foreach (var t in trazos)
            {
                var andado = t.Tipo == TipoDeTrazo.Camino ? TrazadoDelMapa.Recorridos(t.Puntos) : null;
                double correr = t.HaciaDentro != 0 ? t.HaciaDentro * (t.Ancho / 2 + 0.02) : 0;
                foreach (var (desde, hasta) in TrazadoDelMapa.TramosEnCaja(t.Puntos, centroX - radio, centroY - radio, centroX + radio, centroY + radio))
                {
                    var tramo = t.Puntos.Skip(desde).Take(hasta - desde + 1).ToList();
                    double m = (Mejorado(t) ? t.Ancho * Franja / 2 : t.Ancho / 2) + Math.Abs(correr) + 0.5;
                    var trozo = new Trozo
                    {
                        Trazo = t, Puntos = tramo, Desde = andado != null ? andado[desde] : 0,
                        Caja = (tramo.Min(p => p.x) - m, tramo.Min(p => p.y) - m, tramo.Max(p => p.x) + m, tramo.Max(p => p.y) + m),
                    };
                    trozos.Add(trozo);
                    HacerTrozo(trozo);
                }
            }
            if (Estilo == EstiloDeTrazos.Mejorado) Parches();
        }

        /// <summary>La malla de un trozo (la rehace si ya tenía), con la firma del suelo de debajo de ahora.</summary>
        void HacerTrozo(Trozo z)
        {
            if (Mejorado(z.Trazo)) { HacerTrozoMejorado(z); return; }
            if (z.Trazo.Tipo != TipoDeTrazo.Camino && TerritorioDelMapa.Estilo != TerritorioDelMapa.EstiloDeTerritorio.Prototipo) { HacerTrozoRaya(z); return; }
            var t = z.Trazo;
            var e = EscalaDelMapa.MetrosPorUnidad;
            var (dy, desplazar) = Capa(t.Tipo);
            // un contorno va corrido medio ancho hacia dentro: donde se tocan dos zonas, cada raya queda de su lado
            double correr = t.HaciaDentro != 0 ? t.HaciaDentro * (t.Ancho / 2 + 0.02) : 0;
            // el camino, la textura tal cual (el material del prototipo es blanco); las rayas, su color plano
            var color = t.Tipo == TipoDeTrazo.Camino ? Color.white : (Color)CosasEnElMapa.Lineal(t.Color);
            color.a = t.Tenue ? 0.45f : t.Tipo == TipoDeTrazo.Anillo ? 0.7f : 1;
            z.Firma = terreno.Firma(z.Caja.x0, z.Caja.y0, z.Caja.x1, z.Caja.y1);
            var v = new List<Vector3>();
            var uv = new List<Vector2>();
            var rel = new List<Vector2>();
            var c = new List<Color>();
            var tri = new List<int>();
            double? periodo = t.Tipo == TipoDeTrazo.Camino ? TexturaDeCamino.Periodo(t.Ancho) : (double?)null;
            Cinta(z.Puntos, t.Ancho, dy, correr, t.Discontinuo, periodo, z.Desde, t.Tipo == TipoDeTrazo.Camino, color, v, uv, rel, c, tri, e);
            z.Vertices = v.Count;
            if (z.Go == null)
            {
                if (tri.Count == 0) return;
                z.Go = new GameObject("trazo");
                z.Go.transform.SetParent(padre, false);
                z.Go.AddComponent<MeshFilter>().sharedMesh = new Mesh { indexFormat = IndexFormat.UInt32 };
                var mr = z.Go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = Material(t, desplazar);
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = true;
            }
            var malla = z.Go.GetComponent<MeshFilter>().sharedMesh;
            malla.Clear();
            malla.name = "trazo " + t.Tipo;
            malla.SetVertices(v);
            malla.SetUVs(0, uv);
            malla.SetUVs(1, rel);
            malla.SetColors(c);
            malla.SetTriangles(tri, 0);
            malla.RecalculateBounds();
        }

        /// <summary>Lo que lleva cada vértice del camino «Mejorado».</summary>
        sealed class Vertices4
        {
            public readonly List<Vector3> V = new List<Vector3>(), N = new List<Vector3>();
            public readonly List<Vector4> T = new List<Vector4>(), Extra = new List<Vector4>();
            public readonly List<Vector2> Uv = new List<Vector2>(), Medio = new List<Vector2>();
            public readonly List<Color> C = new List<Color>();
            public readonly List<int> Tri = new List<int>();

            public void Agregar(Vector3 v, Vector3 n, Vector4 t, Vector2 uv, Vector4 extra, float medio, Color c, float medioMalla = 0)
            {
                V.Add(v); N.Add(n); T.Add(t); Uv.Add(uv); Extra.Add(extra); Medio.Add(new Vector2(medio, medioMalla)); C.Add(c);
            }

            public void Poner(Mesh malla)
            {
                malla.Clear();
                malla.SetVertices(V);
                malla.SetNormals(N);
                malla.SetTangents(T);
                malla.SetUVs(0, Uv);
                malla.SetUVs(1, Extra);
                malla.SetUVs(2, Medio);
                malla.SetColors(C);
                malla.SetTriangles(Tri, 0);
                malla.RecalculateBounds();
            }
        }

        /// <summary>La normal del suelo pintado en (x, y), en la escena.</summary>
        Vector3 NormalDelSuelo(double x, double y)
        {
            var e = EscalaDelMapa.MetrosPorUnidad;
            float gx = (terreno.AlturaPintada(x + 0.5, y) - terreno.AlturaPintada(x - 0.5, y)) / e, gy = (terreno.AlturaPintada(x, y + 0.5) - terreno.AlturaPintada(x, y - 0.5)) / e;
            return new Vector3(-gx, 1, gy).normalized; // z = −y del mapa
        }

        /// <summary>Cuánto hay de seco en (x, y): 0 sobre el agua (mar, lago o cauce de río), subiendo a 1 a 0,6 u de ella; en un trecho de vado, lo
        /// somero (el camino sigue por el vado).</summary>
        float Seco(double x, double y, out float vado)
        {
            // en un vado el camino sigue: lo somero del trecho o, con la franja de antes, a menos de 1,6 u del punto del vado
            vado = PinturaDelMapa.VadoComoTrecho ? terreno.Cauces?.SomeroEn(x, y, 1.2) ?? 0
                : 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(1.6f, 2.2f, (float)(terreno.Cauces?.AUnVado(x, y) ?? double.PositiveInfinity)));
            if (relieve.AguaEn(x, y)) return vado;
            double c = terreno.Cauces?.ACauce(x, y, 2) ?? double.PositiveInfinity;
            float s = double.IsInfinity(c) ? 1 : Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-0.1f, 0.6f, (float)c));
            for (int k = 0; k < 8; k++)
            {
                double a = k * Math.PI / 4;
                foreach (var r in new[] { 0.3, 0.6 })
                    if (relieve.AguaEn(x + r * Math.Cos(a), y + r * Math.Sin(a))) s = Mathf.Min(s, (float)(r / 0.6) - 0.5f);
            }
            return Mathf.Max(Mathf.Clamp01(s), vado);
        }

        Color SueloDelCamino(double x, double y)
        {
            var (arena, roca, verde) = terreno.SueloEn(x, y);
            return new Color(arena, roca, verde, 1);
        }

        /// <summary>El trozo de camino «Mejorado»: la cinta de `Cinta` pero 1,8 veces más ancha, con lo que necesita `MapaCaminoMejorado`.</summary>
        void HacerTrozoMejorado(Trozo z)
        {
            var t = z.Trazo;
            var e = EscalaDelMapa.MetrosPorUnidad;
            var (dy, desplazar) = Capa(t.Tipo);
            z.Firma = terreno.Firma(z.Caja.x0, z.Caja.y0, z.Caja.x1, z.Caja.y1);
            var m = new Vertices4();
            double medio = t.Ancho / 2, malla = medio * Franja, periodo = t.Ancho * Periodos;
            var xs = new List<double>();
            var ys = new List<double>();
            var s = new List<double>();
            double recorrido = 0;
            for (int k = 0; k < z.Puntos.Count; k++)
            {
                var b = z.Puntos[k];
                if (k == 0) { xs.Add(b.x); ys.Add(b.y); s.Add(0); continue; }
                var a = z.Puntos[k - 1];
                double l = Math.Sqrt((b.x - a.x) * (b.x - a.x) + (b.y - a.y) * (b.y - a.y));
                if (l == 0) continue;
                int n = Math.Max(1, (int)Math.Ceiling(l / Paso));
                for (int i = 1; i <= n; i++) { xs.Add(a.x + (b.x - a.x) * i / n); ys.Add(a.y + (b.y - a.y) * i / n); s.Add(recorrido + l * i / n); }
                recorrido += l;
            }
            int cuantas = xs.Count;
            for (int k = 0; k < cuantas && cuantas >= 2; k++)
            {
                int ia = Math.Max(0, k - 1), ib = Math.Min(cuantas - 1, k + 1);
                double tx = xs[ib] - xs[ia], ty = ys[ib] - ys[ia], l = Math.Sqrt(tx * tx + ty * ty);
                if (l == 0) l = 1;
                double ux = -ty / l, uy = tx / l, x = xs[k], y = ys[k];
                double hc = terreno.AlturaPintada(x, y), hi = terreno.AlturaPintada(x + ux * malla, y + uy * malla), hd = terreno.AlturaPintada(x - ux * malla, y - uy * malla);
                double hmi = terreno.AlturaPintada(x + ux * medio, y + uy * medio), hmd = terreno.AlturaPintada(x - ux * medio, y - uy * medio);
                double subir = Math.Max(0, Math.Max(hc - (hi + hd) / 2, Math.Max(hmi - (hi * 0.45 + hc * 0.55), hmd - (hd * 0.45 + hc * 0.55))));
                float v = (float)((z.Desde + s[k]) / periodo);
                var normal = NormalDelSuelo(x, y);
                var hacia = new Vector3((float)-ux, 0, (float)uy); // hacia donde crece UV0.x (del vértice de la izquierda al de la derecha), en la escena
                float tapa = 1 - RelieveSol(x, y);
                var suelo = SueloDelCamino(x, y);
                float seco = Seco(x, y, out float vado);
                var extra = new Vector4(tapa, seco, vado, 0);
                m.Agregar(EscalaDelMapa.AEscena(x + ux * malla, y + uy * malla, (float)(hi + subir + dy * e)), normal, new Vector4(hacia.x, hacia.y, hacia.z, (float)(-malla * e)),
                    new Vector2(0, v), extra, (float)(medio * e), suelo);
                m.Agregar(EscalaDelMapa.AEscena(x - ux * malla, y - uy * malla, (float)(hd + subir + dy * e)), normal, new Vector4(hacia.x, hacia.y, hacia.z, (float)(malla * e)),
                    new Vector2(1, v), extra, (float)(medio * e), suelo);
                if (k > 0)
                {
                    int a0 = 2 * (k - 1);
                    m.Tri.AddRange(new[] { a0, a0 + 1, a0 + 2, a0 + 1, a0 + 3, a0 + 2 });
                }
            }
            z.Vertices = m.V.Count;
            if (z.Go == null)
            {
                if (m.Tri.Count == 0) return;
                z.Go = Objeto("trazo", MaterialMejorado(t.Escalon, false, desplazar));
            }
            m.Poner(z.Go.GetComponent<MeshFilter>().sharedMesh);
            // Sobre el agua de un vado: los cuadros con algo de vado, otra vez, después del agua (la traza mojada se ve por encima de ella).
            var sobreAgua = new List<int>();
            for (int q = 0; q + 5 < m.Tri.Count; q += 6)
            {
                int a0 = m.Tri[q];
                if (m.Extra[a0].z > 0.02f || m.Extra[a0 + 2].z > 0.02f) sobreAgua.AddRange(m.Tri.GetRange(q, 6));
            }
            if (sobreAgua.Count == 0) { Soltar(z.GoVado); z.GoVado = null; return; }
            if (z.GoVado == null) z.GoVado = Objeto("trazo vado", MaterialMejorado(t.Escalon, false, desplazar, true));
            m.Tri.Clear();
            m.Tri.AddRange(sobreAgua);
            m.Poner(z.GoVado.GetComponent<MeshFilter>().sharedMesh);
        }

        /// <summary>Una línea del territorio, estilo «Mejorado» (`MapaRayaMejorada`): la cinta continua (los trazos y sus extremos redondos, el
        /// perfil oscuro y el ancho mínimo los hace el sombreado), 1,5 veces más ancha que la línea.</summary>
        void HacerTrozoRaya(Trozo z)
        {
            var t = z.Trazo;
            var e = EscalaDelMapa.MetrosPorUnidad;
            var (dy, desplazar) = Capa(t.Tipo);
            z.Firma = terreno.Firma(z.Caja.x0, z.Caja.y0, z.Caja.x1, z.Caja.y1);
            double correr = t.HaciaDentro != 0 ? t.HaciaDentro * (t.Ancho / 2 + 0.02) : 0;
            var color = (Color)CosasEnElMapa.Lineal(t.Color);
            color.a = t.Tenue ? 0.45f : t.Tipo == TipoDeTrazo.Anillo ? 0.7f : 1;
            double medio = t.Ancho / 2, malla = medio * 1.5;
            var m = new Vertices4();
            var xs = new List<double>();
            var ys = new List<double>();
            var s = new List<double>();
            double recorrido = 0;
            for (int k = 0; k < z.Puntos.Count; k++)
            {
                var b = z.Puntos[k];
                if (k == 0) { xs.Add(b.x); ys.Add(b.y); s.Add(0); continue; }
                var a = z.Puntos[k - 1];
                double l = Math.Sqrt((b.x - a.x) * (b.x - a.x) + (b.y - a.y) * (b.y - a.y));
                if (l == 0) continue;
                int n = Math.Max(1, (int)Math.Ceiling(l / Paso));
                for (int i = 1; i <= n; i++) { xs.Add(a.x + (b.x - a.x) * i / n); ys.Add(a.y + (b.y - a.y) * i / n); s.Add(recorrido + l * i / n); }
                recorrido += l;
            }
            int cuantas = xs.Count;
            for (int k = 0; k < cuantas && cuantas >= 2; k++)
            {
                int ia = Math.Max(0, k - 1), ib = Math.Min(cuantas - 1, k + 1);
                double tx = xs[ib] - xs[ia], ty = ys[ib] - ys[ia], l = Math.Sqrt(tx * tx + ty * ty);
                if (l == 0) l = 1;
                double ux = -ty / l, uy = tx / l, x = xs[k] + ux * correr, y = ys[k] + uy * correr;
                double hc = terreno.AlturaPintada(x, y), hi = terreno.AlturaPintada(x + ux * malla, y + uy * malla), hd = terreno.AlturaPintada(x - ux * malla, y - uy * malla);
                double subir = Math.Max(0, hc - (hi + hd) / 2);
                var hacia = new Vector3((float)-ux, 0, (float)uy);
                var extra = new Vector4(t.Discontinuo ? 1 : 0, 0, 0, 0);
                m.Agregar(EscalaDelMapa.AEscena(x + ux * malla, y + uy * malla, (float)(hi + subir + dy * e)), Vector3.up, new Vector4(hacia.x, hacia.y, hacia.z, (float)(-malla * e)),
                    new Vector2(-1, (float)s[k]), extra, (float)medio, color, (float)malla);
                m.Agregar(EscalaDelMapa.AEscena(x - ux * malla, y - uy * malla, (float)(hd + subir + dy * e)), Vector3.up, new Vector4(hacia.x, hacia.y, hacia.z, (float)(malla * e)),
                    new Vector2(1, (float)s[k]), extra, (float)medio, color, (float)malla);
                if (k > 0)
                {
                    int a0 = 2 * (k - 1);
                    m.Tri.AddRange(new[] { a0, a0 + 1, a0 + 2, a0 + 1, a0 + 3, a0 + 2 });
                }
            }
            z.Vertices = m.V.Count;
            if (z.Go == null)
            {
                if (m.Tri.Count == 0) return;
                z.Go = Objeto("trazo", MaterialRaya(t, desplazar));
            }
            m.Poner(z.Go.GetComponent<MeshFilter>().sharedMesh);
        }

        Material MaterialRaya(Trazo t, float desplazar)
        {
            var nombre = $"raya mejorada{(t.BajoNiebla ? "" : " encima")} {desplazar}";
            if (materiales.TryGetValue(nombre, out var m)) return m;
            m = new Material(Resources.Load<Shader>("MapaMundo/MapaRayaMejorada")) { name = nombre };
            m.SetFloat("_ConNiebla", t.BajoNiebla ? 1 : 0);
            m.SetFloat("_Desplazar", desplazar);
            m.SetFloat("_MinPx", 2);
            materiales[nombre] = m;
            return m;
        }

        GameObject Objeto(string nombre, Material mat)
        {
            var go = new GameObject(nombre);
            go.transform.SetParent(padre, false);
            go.AddComponent<MeshFilter>().sharedMesh = new Mesh { indexFormat = IndexFormat.UInt32 };
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = true;
            return go;
        }

        /// <summary>Los parches de tierra apisonada (estilo «Mejorado»): en cada extremo de un camino, un disco del ancho del camino (que no se vea el
        /// corte a escuadra); donde el extremo toca otro camino (un cruce de tres ramas o más, o una T), uno más grande que funde las cintas.</summary>
        void Parches()
        {
            var caminos = trazos.Where(t => t.Tipo == TipoDeTrazo.Camino && t.Puntos.Count >= 2).ToList();
            var lista = new List<(double x, double y, double r, int escalon)>();
            double Distancia((double x, double y) p, List<(double x, double y)> l)
            {
                double d = double.PositiveInfinity;
                for (int i = 1; i < l.Count; i++)
                {
                    double dx = l[i].x - l[i - 1].x, dy = l[i].y - l[i - 1].y, ll = dx * dx + dy * dy;
                    double t = ll > 0 ? Math.Max(0, Math.Min(1, ((p.x - l[i - 1].x) * dx + (p.y - l[i - 1].y) * dy) / ll)) : 0;
                    d = Math.Min(d, Math.Sqrt(Math.Pow(p.x - l[i - 1].x - t * dx, 2) + Math.Pow(p.y - l[i - 1].y - t * dy, 2)));
                }
                return d;
            }
            for (int i = 0; i < caminos.Count; i++)
                foreach (var p in new[] { caminos[i].Puntos[0], caminos[i].Puntos[caminos[i].Puntos.Count - 1] })
                {
                    if (Math.Abs(p.x - centroX) > radio || Math.Abs(p.y - centroY) > radio) continue;
                    int otros = 0;
                    double ancho = caminos[i].Ancho;
                    int escalon = caminos[i].Escalon;
                    for (int j = 0; j < caminos.Count; j++)
                        if (j != i && Distancia(p, caminos[j].Puntos) < 0.35) { otros++; ancho = Math.Max(ancho, caminos[j].Ancho); escalon = Math.Max(escalon, caminos[j].Escalon); }
                    double r = otros > 0 ? 0.65 * ancho + 0.1 : caminos[i].Ancho / 2;
                    int ya = lista.FindIndex(q => (q.x - p.x) * (q.x - p.x) + (q.y - p.y) * (q.y - p.y) < 0.25);
                    if (ya >= 0) { if (r > lista[ya].r) lista[ya] = (lista[ya].x, lista[ya].y, r, Math.Max(escalon, lista[ya].escalon)); continue; }
                    lista.Add((p.x, p.y, r, escalon));
                }
            var e = EscalaDelMapa.MetrosPorUnidad;
            var (dy, desplazar) = Capa(TipoDeTrazo.Camino);
            const int Lados = 20;
            foreach (var (cx, cy, r, escalon) in lista)
            {
                var m = new Vertices4();
                double malla = r * Franja;
                foreach (var (anillo, i) in new[] { (0.0, 0) }.Concat(Enumerable.Range(0, Lados).SelectMany(k => new[] { (0.5, k), (1.0, k) })))
                {
                    double ang = i * 2 * Math.PI / Lados, rr = anillo * malla;
                    double x = cx + rr * Math.Cos(ang), y = cy + rr * Math.Sin(ang);
                    var hacia = new Vector3((float)Math.Cos(ang), 0, (float)-Math.Sin(ang));
                    float seco = Seco(x, y, out float vado);
                    m.Agregar(EscalaDelMapa.AEscena(x, y, (float)(terreno.AlturaPintada(x, y) + (dy + 0.002) * e)), NormalDelSuelo(x, y),
                        new Vector4(hacia.x, hacia.y, hacia.z, (float)(rr * e)), new Vector2((float)(0.5 + 0.5 * anillo), 0),
                        new Vector4(1 - RelieveSol(x, y), seco, vado, 1), (float)(r * e), SueloDelCamino(x, y));
                }
                // índices: 0 el centro; 1 + 2k el anillo de en medio y 2 + 2k el de fuera, del lado k
                for (int k = 0; k < Lados; k++)
                {
                    int k1 = (k + 1) % Lados, a0 = 1 + 2 * k, a1 = 1 + 2 * k1, b0 = 2 + 2 * k, b1 = 2 + 2 * k1;
                    m.Tri.AddRange(new[] { 0, a1, a0, a0, a1, b0, b0, a1, b1 });
                }
                var go = Objeto("parche", MaterialMejorado(escalon, true, desplazar));
                m.Poner(go.GetComponent<MeshFilter>().sharedMesh);
                parches.Add(go);
            }
        }

        Material MaterialMejorado(int escalon, bool parche, float desplazar, bool sobreAgua = false)
        {
            var clase = escalon == 0 ? "sendero" : "camino";
            var nombre = clase + " mejorado" + (parche ? " parche" : "") + (sobreAgua ? " sobre el agua" : "");
            if (materiales.TryGetValue(nombre, out var m)) return m;
            m = new Material(Resources.Load<Shader>("MapaMundo/MapaCaminoMejorado")) { name = nombre };
            foreach (var (prop, archivo) in new[] { ("_DetalleA", "_a_detalle"), ("_DetalleB", "_b_detalle"), ("_NormalA", "_a_normal"), ("_NormalB", "_b_normal") })
                m.SetTexture(prop, Resources.Load<Texture2D>("MapaMundo/Caminos/" + clase + archivo));
            var suelo = terreno.Material;
            m.SetTexture("_SueloColor", suelo.GetTexture("_SueloColor"));
            m.SetTexture("_SueloRuido", suelo.GetTexture("_SueloRuido"));
            m.SetVector("_Escalas", suelo.GetVector("_Escalas"));
            m.SetFloat("_Desplazar", desplazar);
            m.SetFloat("_MinPx", escalon == 0 ? 2.2f : 3f);
            m.SetFloat("_Aplastada", escalon == 0 ? 1 : 0);
            m.SetFloat("_SobreAgua", sobreAgua ? 1 : 0);
            // los parches, después de las cintas (tapan los solapes de los cruces y los cortes)
            m.renderQueue = sobreAgua ? 3005 : 2460 + (parche ? 1 : 0);
            materiales[nombre] = m;
            return m;
        }

        /// <summary>`cinta` del prototipo: una muestra cada 0,5 u con un vértice a cada lado; cada borde a su suelo y toda ella subida si el centro asoma.</summary>
        void Cinta(List<(double x, double y)> puntos, double ancho, double dy, double correr, bool discontinua, double? periodo, double desde, bool camino, Color color,
            List<Vector3> v, List<Vector2> uv, List<Vector2> rel, List<Color> c, List<int> tri, float e)
        {
            double medio = ancho / 2;
            var xs = new List<double>();
            var ys = new List<double>();
            var s = new List<double>();
            double recorrido = 0;
            for (int k = 0; k < puntos.Count; k++)
            {
                var b = puntos[k];
                if (k == 0) { xs.Add(b.x); ys.Add(b.y); s.Add(0); continue; }
                var a = puntos[k - 1];
                double l = Math.Sqrt((b.x - a.x) * (b.x - a.x) + (b.y - a.y) * (b.y - a.y));
                if (l == 0) continue;
                int n = Math.Max(1, (int)Math.Ceiling(l / Paso));
                for (int i = 1; i <= n; i++) { xs.Add(a.x + (b.x - a.x) * i / n); ys.Add(a.y + (b.y - a.y) * i / n); s.Add(recorrido + l * i / n); }
                recorrido += l;
            }
            int m = xs.Count;
            if (m < 2) return;
            int inicio = v.Count;
            for (int k = 0; k < m; k++)
            {
                int ia = Math.Max(0, k - 1), ib = Math.Min(m - 1, k + 1);
                double tx = xs[ib] - xs[ia], ty = ys[ib] - ys[ia], l = Math.Sqrt(tx * tx + ty * ty);
                if (l == 0) l = 1;
                double ux = -ty / l, uy = tx / l, nx = ux * medio, ny = uy * medio, x = xs[k] + ux * correr, y = ys[k] + uy * correr;
                double hc = terreno.AlturaPintada(x, y), hi = terreno.AlturaPintada(x + nx, y + ny), hd = terreno.AlturaPintada(x - nx, y - ny);
                double subir = Math.Max(0, hc - (hi + hd) / 2);
                v.Add(EscalaDelMapa.AEscena(x + nx, y + ny, (float)(hi + subir + dy * e)));
                v.Add(EscalaDelMapa.AEscena(x - nx, y - ny, (float)(hd + subir + dy * e)));
                float vv = periodo.HasValue ? (float)((desde + s[k]) / periodo.Value) : 0;
                uv.Add(new Vector2(0, vv));
                uv.Add(new Vector2(1, vv));
                float tapa = camino ? 1 - RelieveSol(x, y) : 0;
                rel.Add(new Vector2(tapa, 0));
                rel.Add(new Vector2(tapa, 0));
                c.Add(color);
                c.Add(color);
            }
            for (int k = 1; k < m; k++)
            {
                if (discontinua && ((s[k - 1] + s[k]) / 2) % (Lleno + Hueco) > Lleno) continue;
                int a = inicio + 2 * (k - 1);
                tri.AddRange(new[] { a, a + 1, a + 2, a + 1, a + 3, a + 2 }); // hacia arriba
            }
        }

        /// <summary>La luz del sol que deja el relieve en (x, y): la de la rejilla de 2 u del suelo, interpolada (1 sin sombras del relieve).</summary>
        float RelieveSol(double x, double y)
        {
            if (!PinturaDelMapa.SombrasReales || !PinturaDelMapa.SombraDelRelieve || relieve == null) return 1;
            double p = SombraDelRelieve.PasoRejilla;
            long i = (long)Math.Floor(x / p), j = (long)Math.Floor(y / p);
            double fx = x / p - i, fy = y / p - j;
            float En(long a, long b)
            {
                if (relieveSol.TryGetValue((a, b), out var r)) return r;
                var (dx, dy, tan) = SombraDelRelieve.DelSol(PinturaDelMapa.Sol.x, PinturaDelMapa.Sol.y, PinturaDelMapa.Sol.z);
                r = SombraDelRelieve.Visibilidad((xx, yy) => relieve.AlturaEn(xx, yy), a * p, b * p, dx, dy, tan, EscalaDelMapa.MetrosPorUnidad);
                relieveSol[(a, b)] = r;
                return r;
            }
            return (float)((En(i, j) * (1 - fx) + En(i + 1, j) * fx) * (1 - fy) + (En(i, j + 1) * (1 - fx) + En(i + 1, j + 1) * fx) * fy);
        }

        Material Material(Trazo t, float desplazar)
        {
            bool camino = t.Tipo == TipoDeTrazo.Camino;
            var nombre = camino ? (t.Escalon == 0 ? "sendero" : "camino") : $"raya{(t.BajoNiebla ? "" : " encima")} {desplazar}";
            if (materiales.TryGetValue(nombre, out var m)) return m;
            m = new Material(Resources.Load<Shader>("MapaMundo/MapaCamino")) { name = nombre };
            if (camino) m.SetTexture("_MainTex", Resources.Load<Texture2D>("MapaMundo/Caminos/" + nombre));
            m.SetFloat("_Luz", camino ? 1 : 0);
            m.SetFloat("_ConNiebla", t.BajoNiebla ? 1 : 0);
            m.SetFloat("_Desplazar", desplazar);
            materiales[nombre] = m;
            return m;
        }

        static void Soltar(GameObject go)
        {
            if (go == null) return;
            UnityEngine.Object.Destroy(go.GetComponent<MeshFilter>().sharedMesh);
            UnityEngine.Object.Destroy(go);
        }

        public void Destruir()
        {
            foreach (var t in trozos) { Soltar(t.Go); Soltar(t.GoVado); }
            trozos.Clear();
            pendientes.Clear();
            foreach (var g in parches) Soltar(g);
            parches.Clear();
            foreach (var m in materiales.Values) UnityEngine.Object.Destroy(m);
            materiales.Clear();
        }
    }
}
