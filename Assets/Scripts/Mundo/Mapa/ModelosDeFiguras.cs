// Las figuras de las columnas con su modelo de verdad (tema 7, prueba técnica B): el héroe a caballo vestido con lo que lleva y cada soldado con
// el modelo de batalla de su tropa, andando o quietos, instanciados (sin GameObject ni Animator por figura). Cada modelo llega horneado como una
// «tira» de mallas, una por fotograma del andar y del reposo (lo hornea quien puede ver los prefabs de batalla: `Conquest.Mundo.UI`), y aquí se
// pinta cada fotograma con `RenderMeshInstanced`: una llamada por malla, fotograma y material. Cada figura lleva su fase (no van a la par) y el
// paso, al ritmo de lo que anda de verdad su columna. De lejos (menos de `PixelesMinimos` de alto en pantalla) o mientras su modelo no está
// horneado, la figura de bloques de `FigurasDelMapa`.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ConquestTactics.Mundo.Mapa
{
    /// <summary>Un modelo horneado: sus mallas del andar y del reposo (una por fotograma, con un submesh por material), sus materiales, cuánto
    /// dura cada ciclo, a qué velocidad anda en su clip (m/s) y su alto (m).</summary>
    public sealed class TiraDeFiguras
    {
        public Mesh[] Andar, Reposo;
        public Material[] Materiales;
        public float DuracionAndar = 1, DuracionReposo = 2, PasoDelClip = 1.15f, Alto = 1.8f;
        public int Triangulos;
    }

    public static class ModelosDeFiguras
    {
        /// <summary>Por debajo de esto (m/s, en el modelo), una figura va en reposo: las de columnas y caravanas.</summary>
        public const float UmbralAndar = 0.05f;
        /// <summary>El del Aeda y su burro (estilo «Mejorado»): justo por encima de cero, reposo solo cuando de verdad no avanza.</summary>
        public const float UmbralAndarAeda = 0.002f;
        /// <summary>En el último fotograma: el Aeda y su burro andando o parados, y lo que anda el Aeda (m/s del modelo), para las pruebas.</summary>
        public static int AedaAndando, AedaParado;
        /// <summary>Los del último fotograma pintado.</summary>
        public static int ultimosAedaAndando, ultimosAedaParado;
        public static float VelocidadAeda;

        /// <summary>Lo que sale en pantalla con menos alto que esto (px), con su figura de bloques.</summary>
        public static float PixelesMinimos = 21;
        /// <summary>Solo proyectan sombra las que miden al menos esto en pantalla (px).</summary>
        public static float PixelesSombra = 60;
        /// <summary>Los metros de un modelo, en la escena del mapa: los de la figura de bloques (`ColumnasDelMapa.Tropa`); el soldado (1,75 m)
        /// mide 0,405 u.</summary>
        public static float MetrosAEscena => (float)(ColumnasDelMapa.MetrosAFigura * ColumnasDelMapa.Tropa * EscalaDelMapa.MetrosPorUnidad);
        /// <summary>Con modelos (lo pone quien hornea; sin él, todo de bloques).</summary>
        public static bool Activos;
        /// <summary>Quien hornea: le llega la clave de lo que falta (una vez por clave) y cuando lo tenga lo pone en `Listos`.</summary>
        public static Action<string> Pedir;
        public static readonly Dictionary<string, TiraDeFiguras> Listos = new Dictionary<string, TiraDeFiguras>();
        static readonly HashSet<string> pedidos = new HashSet<string>();

        /// <summary>La cámara del mapa, para el tamaño en pantalla (la pone el mapa en cada fotograma).</summary>
        public static Camera Camara;

        // lo de este fotograma: por malla, las matrices
        sealed class Lote
        {
            public TiraDeFiguras Tira;
            public readonly List<Matrix4x4> M = new List<Matrix4x4>(), SinSombra = new List<Matrix4x4>();
            public readonly List<Vector4> C = new List<Vector4>(), CSinSombra = new List<Vector4>();
        }
        static readonly Dictionary<Mesh, Lote> tanda = new Dictionary<Mesh, Lote>();
        static readonly Matrix4x4[] bloque = new Matrix4x4[1023];
        static readonly Vector4[] bloqueC = new Vector4[1023];
        static readonly MaterialPropertyBlock props = new MaterialPropertyBlock();

        /// <summary>El color de bando por la relación con quien mira (el de la batalla): propio, aliado (misma Facción), enemigo y neutral.</summary>
        public static readonly Color Propio = new Color32(0x2F, 0x5F, 0xB0, 255), Aliado = new Color32(0x6B, 0xBF, 0x59, 255),
            Enemigo = new Color32(0xA3, 0x26, 0x2A, 255), Neutral = new Color32(0xE8, 0xE2, 0xD2, 255);
        public static int Llamadas, Instancias, TriangulosPintados;
        /// <summary>El alto en pantalla (px) del último soldado mirado: para dar el tamaño a cada zoom.</summary>
        public static float PixelesSoldado;

        static readonly Dictionary<string, string[]> clavesDeTropa = new Dictionary<string, string[]>();

        /// <summary>La clave de una tropa y su variante (sin construir la cadena en cada fotograma).</summary>
        public static string ClaveDeTropa(string tropaId, int variante)
        {
            tropaId ??= "";
            if (!clavesDeTropa.TryGetValue(tropaId, out var a)) clavesDeTropa[tropaId] = a = new string[8];
            if (variante < 0 || variante >= a.Length) return "tropa|" + tropaId + "|" + variante;
            return a[variante] ??= "tropa|" + tropaId + "|" + variante;
        }
        static readonly Dictionary<string, string> clavesDeHeroe = new Dictionary<string, string>();
        public static string ClaveDeHeroe(string heroeId)
        {
            if (!clavesDeHeroe.TryGetValue(heroeId, out var c)) clavesDeHeroe[heroeId] = c = "heroe|" + heroeId;
            return c;
        }

        static readonly Dictionary<int, string> clavesDeCaravana = new Dictionary<int, string>();
        /// <summary>El carrito del héroe: con qué animal va (null, a mano).</summary>
        public static string AnimalDelCarrito = "burro";

        /// <summary>La clave del modelo de un animal o carro de caravana (tema 7): el burro del Aeda, «animal|burro»; un carro, «carro|tipo|animal»
        /// (tipo básico o reforzado; el de fundación, «fundacion»; el carrito del héroe, «carrito» con el animal de `AnimalDelCarrito`).</summary>
        public static string ClaveDeCaravana(in SitioDeFigura f)
        {
            int k = (int)f.Figura * 64 + (int)f.Animal * 2 + (f.Reforzado ? 1 : 0);
            if (clavesDeCaravana.TryGetValue(k, out var c)) return c;
            string animal = f.Animal == AnimalDeTiro.Caballo ? "caballo" : f.Animal == AnimalDeTiro.Camello ? "camello" : "buey";
            c = f.Figura == FiguraDeColumna.Burro ? "animal|burro"
                : f.Figura == FiguraDeColumna.Carrito ? (AnimalDelCarrito == null ? "carro|carrito" : "carro|carrito|" + AnimalDelCarrito)
                : f.Figura == FiguraDeColumna.CarroDeFundacion ? "carro|fundacion|" + animal
                : "carro|" + (f.Reforzado ? "reforzado" : "basico") + "|" + animal;
            return clavesDeCaravana[k] = c;
        }

        /// <summary>De cerca: por encima de este alto en pantalla (px), y como mucho los `TopeCompletos` más grandes, el modelo con todos sus
        /// fotogramas y variantes y con sombra (la de más de `PixelesSombra`). El resto, hasta los bloques, el mismo modelo reducido: menos
        /// fotogramas y variantes con la distancia y sin sombra. (No hay nivel simplificado: costaba lo mismo en llamadas y no era el mismo
        /// soldado; ver el doc del tema 7.)</summary>
        public static float PixelesCompleto = 60;
        public static int TopeCompletos = 300;
        /// <summary>Por debajo de esto (px), el reducido usa 4 fotogramas de andar y 1 de reposo y una sola variante; por encima, 6 y 3 y dos
        /// variantes.</summary>
        public static float PixelesLejos = 30;
        /// <summary>Cuántas fases de andar hay en una columna (por grupos de filas).</summary>
        public static int GruposDePaso = 3;

        /// <summary>El desfase de una columna: uno de dos (0 o media fase de grupo), por su id; en todo el mapa hay 2 × `GruposDePaso` fases.</summary>
        public static float FaseDeColumna(string id) => (id == null ? 0 : (id.GetHashCode() & 1)) * 0.5f / GruposDePaso;

        /// <summary>Olvida todo lo horneado y lo pedido (para volver a empezar, en las pruebas).</summary>
        public static void Reiniciar()
        {
            Listos.Clear();
            pedidos.Clear();
        }

        struct Entrada
        {
            public TiraDeFiguras Tira, TiraLejos;
            public bool Anda;
            public float Fase, Px;
            public Matrix4x4 M;
            public Vector4 C;
        }
        static readonly List<Entrada> entradas = new List<Entrada>();
        static readonly List<int> orden = new List<int>();
        static bool[] completa = new bool[256];
        /// <summary>En el último fotograma: cuántas figuras con todo y cuántas reducidas.</summary>
        public static int Completos, Reducidos;

        static TiraDeFiguras Tira(string clave)
        {
            if (Listos.TryGetValue(clave, out var t)) return t;
            if (Pedir != null && pedidos.Add(clave)) Pedir(clave);
            return null;
        }

        /// <summary>Olvida lo pedido (para volver a hornear un héroe que cambió de equipo).</summary>
        public static void Olvidar(string clave)
        {
            pedidos.Remove(clave);
            Listos.Remove(clave);
        }

        static int fotogramaCamara = -1;
        static Vector3 posCamara;
        static float pixelesPorDistancia;

        /// <summary>El alto en pantalla (px) de una figura de 1,75 m en `pie` (la cámara se lee una vez por fotograma).</summary>
        public static float Pixeles(Vector3 pie)
        {
            if (Camara == null) return 0;
            if (fotogramaCamara != Time.frameCount)
            {
                fotogramaCamara = Time.frameCount;
                posCamara = Camara.transform.position;
                pixelesPorDistancia = 1.75f * MetrosAEscena / (2 * Mathf.Tan(Camara.fieldOfView * 0.5f * Mathf.Deg2Rad)) * Camara.pixelHeight;
            }
            return pixelesPorDistancia / Mathf.Max(0.01f, Vector3.Distance(posCamara, pie));
        }

        /// <summary>Las tiras de una figura: la suya y las de menos variantes (la 0 y la 1 a media distancia, solo la 0 lejos), si ya están
        /// horneadas; si no, la suya (o null, y se pide). Se buscan al recolocar la columna, no en cada fotograma.</summary>
        public static (TiraDeFiguras propia, TiraDeFiguras media, TiraDeFiguras lejana) TirasDe(in SitioDeFigura f)
        {
            if (!Activos) return default;
            if (f.Figura == FiguraDeColumna.Heroe)
            {
                var h = f.HeroeId != null ? Tira(ClaveDeHeroe(f.HeroeId)) : null;
                return (h, h, h);
            }
            if (f.Figura == FiguraDeColumna.Aeda)
            {
                var ae = Tira("aeda");
                return (ae, ae, ae);
            }
            if (f.Figura == FiguraDeColumna.Burro || f.Figura == FiguraDeColumna.Carro || f.Figura == FiguraDeColumna.CarroDeFundacion
                || f.Figura == FiguraDeColumna.Carrito)
            {
                var a = Tira(ClaveDeCaravana(f));
                return (a, a, a);
            }
            if (f.Figura != FiguraDeColumna.Soldado) return default;
            var t = Tira(ClaveDeTropa(f.TropaId, f.Variante));
            if (t == null) return default;
            string tropa = f.TropaId;
            int propia = f.Variante;
            TiraDeFiguras de(int v) => v == propia ? t : Listos.TryGetValue(ClaveDeTropa(tropa, v), out var o) ? o : t;
            return (t, de(propia % 2), de(0));
        }

        /// <summary>¿Pinta esta figura con su modelo? Si sí, la apunta para `Pintar` y devuelve true; si no (lejos o aún sin hornear), false y va de
        /// bloques. `px`: su alto en pantalla (`Pixeles`).</summary>
        public static bool Apuntar(in SitioDeFigura f, TiraDeFiguras t, TiraDeFiguras media, TiraDeFiguras lejana, float px, in Matrix4x4 matriz,
                                   double velocidad, float faseColumna, Color equipo)
        {
            if (t == null || !Activos || Camara == null) return false;
            if (f.Figura == FiguraDeColumna.Soldado) PixelesSoldado = px;
            if (px < PixelesMinimos) return false;
            var lejos = f.Figura != FiguraDeColumna.Soldado || px >= PixelesCompleto ? t : px < PixelesLejos ? lejana : media;
            // el paso: al ritmo de lo que anda su columna (u/s → m/s del modelo), con su fase; parada, el reposo
            float vModelo = (float)(velocidad * EscalaDelMapa.MetrosPorUnidad / MetrosAEscena);
            // el umbral de andar: por debajo de `UmbralAndar` m/s, el reposo (que el ruido de la velocidad medida de una columna casi parada no la
            // ponga a dar pasos); el Aeda y su burro, que van solos y despacio, andan en cuanto avanzan (`UmbralAndarAeda`)
            bool delAeda = f.Figura == FiguraDeColumna.Aeda || f.Figura == FiguraDeColumna.Burro;
            bool anda = vModelo > (delAeda ? UmbralAndarAeda : UmbralAndar) && t.Andar.Length > 0;
            if (delAeda) { if (anda) AedaAndando++; else AedaParado++; VelocidadAeda = vModelo; }
            float ritmo = anda ? Mathf.Clamp(vModelo / t.PasoDelClip, 0.6f, 1.8f) : 1;
            float dura = anda ? t.DuracionAndar : t.DuracionReposo;
            // la fase: por grupos de filas (fila r, grupo r mod `GruposDePaso`) más el desfase de su columna, no por figura: así, en cada
            // instante, solo hay unas pocas poses distintas por variante y las instancias se agrupan (y una columna marcha casi al paso)
            int fila = (int)Math.Floor(f.Atras + 0.5);
            float fase = (faseColumna + (((fila % GruposDePaso) + GruposDePaso) % GruposDePaso) / (float)GruposDePaso) % 1f;
            entradas.Add(new Entrada { Tira = t, TiraLejos = lejos, Anda = anda, Fase = ((Time.time * ritmo / dura) + fase) % 1f, Px = px,
                                       M = matriz, C = (Vector4)equipo.linear });
            return true;
        }

        static void Apilar(Mesh malla, TiraDeFiguras t, bool sombra, in Matrix4x4 m, in Vector4 c)
        {
            if (!tanda.TryGetValue(malla, out var lote)) tanda[malla] = lote = new Lote { Tira = t };
            if (sombra) { lote.M.Add(m); lote.C.Add(c); }
            else { lote.SinSombra.Add(m); lote.CSinSombra.Add(c); }
        }

        /// <summary>Pinta lo apuntado en este fotograma y lo vacía: el modelo con todo para las más grandes en pantalla (hasta `TopeCompletos`,
        /// con sombra las de más de `PixelesSombra`) y el reducido para las demás.</summary>
        public static void Pintar()
        {
            Llamadas = Instancias = TriangulosPintados = Completos = Reducidos = 0;
            ultimosAedaAndando = AedaAndando; ultimosAedaParado = AedaParado; AedaAndando = AedaParado = 0;
            // las candidatas a ir con todo, de mayor a menor en pantalla
            orden.Clear();
            for (int i = 0; i < entradas.Count; i++)
                if (entradas[i].Px >= PixelesCompleto) orden.Add(i);
            if (orden.Count > TopeCompletos) orden.Sort((a, b) => entradas[b].Px.CompareTo(entradas[a].Px));
            int tope = Mathf.Min(TopeCompletos, orden.Count);
            if (completa.Length < entradas.Count) completa = new bool[entradas.Count * 2];
            Array.Clear(completa, 0, entradas.Count);
            for (int i = 0; i < tope; i++) completa[orden[i]] = true;
            for (int i = 0; i < entradas.Count; i++)
            {
                var e = entradas[i];
                if (completa[i])
                {
                    var mallas = e.Anda ? e.Tira.Andar : e.Tira.Reposo;
                    int k = (int)(e.Fase * mallas.Length) % mallas.Length;
                    Apilar(mallas[k], e.Tira, e.Px >= PixelesSombra, e.M, e.C);
                    Completos++;
                    TriangulosPintados += e.Tira.Triangulos;
                    continue;
                }
                // reducido: la tira de menos variantes y menos fotogramas con la distancia (de 12 y 6: 6 y 3 a media distancia, 4 y 1 lejos), sin
                // sombra
                var reducidas = e.Anda ? e.TiraLejos.Andar : e.TiraLejos.Reposo;
                int n = reducidas.Length;
                int paso = e.Px < PixelesLejos ? (e.Anda ? 3 : n) : 2;
                int usados = Mathf.Max(1, n / paso);
                int j = ((int)(e.Fase * usados) % usados) * paso;
                Apilar(reducidas[Mathf.Min(j, n - 1)], e.TiraLejos, false, e.M, e.C);
                Reducidos++;
                TriangulosPintados += e.TiraLejos.Triangulos;
            }
            entradas.Clear();
            foreach (var kv in tanda)
            {
                var lote = kv.Value;
                var t = lote.Tira;
                foreach (var (m, cs, sombra) in new[] { (lote.M, lote.C, true), (lote.SinSombra, lote.CSinSombra, false) })
                {
                    if (m.Count == 0) continue;
                    for (int s = 0; s < kv.Key.subMeshCount && s < t.Materiales.Length; s++)
                    {
                        var rp = new RenderParams(t.Materiales[s]) { shadowCastingMode = sombra ? ShadowCastingMode.On : ShadowCastingMode.Off, receiveShadows = true, matProps = props };
                        for (int i = 0; i < m.Count; i += bloque.Length)
                        {
                            int n = Mathf.Min(bloque.Length, m.Count - i);
                            m.CopyTo(i, bloque, 0, n);
                            cs.CopyTo(i, bloqueC, 0, n);
                            props.SetVectorArray("_ColorEquipo", bloqueC);
                            Graphics.RenderMeshInstanced(rp, kv.Key, s, bloque, n);
                            Llamadas++;
                        }
                    }
                    Instancias += m.Count;
                    m.Clear();
                    cs.Clear();
                }
            }
        }
    }
}
