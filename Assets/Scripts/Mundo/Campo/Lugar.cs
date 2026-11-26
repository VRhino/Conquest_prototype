// El recorte del lugar que trae el ticket de batalla (Campo_De_Batalla_Del_Lugar.md §3; `RecorteDelLugar` del contrato 0.34.0), pasado al
// marco del campo: todo en milímetros desde el centro del recorte (x al este, y al sur), con el zoom del campo (75 m por u, así 1 milésima
// de u son 75 mm justos). Las alturas siguen siendo las del mundo (sin zoom), en milímetros; el zoom lo pone el generador.
using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using static ConquestTactics.Mundo.Campo.Entero;
using pb = BronzeAge.Contract.V1;

namespace ConquestTactics.Mundo.Campo
{
    public struct Punto
    {
        public long X, Y;
        public Punto(long x, long y) { X = x; Y = y; }
    }

    /// <summary>Una rejilla de muestras en el marco del campo: la (i, j) está en (X0 + i·Paso, Y0 + j·Paso), en mm.</summary>
    public sealed class Rejilla
    {
        public long X0, Y0, Paso;
        public int Columnas, Filas;
        public int[] Datos;

        public Rejilla(long x0, long y0, long paso, int columnas, int filas, int[] datos = null)
        {
            X0 = x0; Y0 = y0; Paso = paso; Columnas = columnas; Filas = filas;
            Datos = datos ?? new int[columnas * filas];
        }

        public Rejilla ConDatos(int[] datos) => new Rejilla(X0, Y0, Paso, Columnas, Filas, datos);

        // La posición (x, y) en muestras, en Q16, dentro de la rejilla.
        void Situar(long x, long y, out int i, out int j, out long a, out long b)
        {
            long fi = Clamp(DivSuelo((x - X0) * Uno, Paso), 0, (long)(Columnas - 1) * Uno), fj = Clamp(DivSuelo((y - Y0) * Uno, Paso), 0, (long)(Filas - 1) * Uno);
            i = Math.Min(Columnas - 2, (int)(fi >> 16));
            j = Math.Min(Filas - 2, (int)(fj >> 16));
            a = fi - ((long)i << 16);
            b = fj - ((long)j << 16);
        }

        /// <summary>El valor en (x, y), interpolado entre las cuatro muestras de alrededor; fuera, el del borde.</summary>
        public long Muestra(long x, long y)
        {
            Situar(x, y, out int i, out int j, out long a, out long b);
            int k = j * Columnas + i;
            long arriba = Datos[k] * (Uno - a) + Datos[k + 1] * a, abajo = Datos[k + Columnas] * (Uno - a) + Datos[k + Columnas + 1] * a;
            return (arriba * (Uno - b) + abajo * b) >> 32;
        }

        /// <summary>El valor en (x, y), en bicúbico (Catmull-Rom) entre las 16 muestras de alrededor: curvas suaves, sin facetas.</summary>
        public long Cubica(long x, long y)
        {
            Situar(x, y, out int i, out int j, out long a, out long b);
            long V(int ii, int jj) => Datos[Clamp(jj, 0, Filas - 1) * Columnas + Clamp(ii, 0, Columnas - 1)];
            long Fila(int jj) => CatmullRom(V(i - 1, jj), V(i, jj), V(i + 1, jj), V(i + 2, jj), a);
            return CatmullRom(Fila(j - 1), Fila(j), Fila(j + 1), Fila(j + 2), b);
        }

        static long CatmullRom(long p0, long p1, long p2, long p3, long t)
        {
            long c3 = 3 * (p1 - p2) + p3 - p0, c2 = 2 * p0 - 5 * p1 + 4 * p2 - p3 + (t * c3 >> 16), c1 = p2 - p0 + (t * c2 >> 16);
            return p1 + (t * c1 >> 17);
        }

        /// <summary>La muestra más cercana a (x, y).</summary>
        public int Cercana(long x, long y)
        {
            int i = (int)Clamp(DivRedondo(x - X0, Paso), 0, Columnas - 1), j = (int)Clamp(DivRedondo(y - Y0, Paso), 0, Filas - 1);
            return Datos[j * Columnas + i];
        }
    }

    public sealed class RioDelLugar
    {
        public Punto[] Puntos;
        public Punto[] Vados;
        public bool Navegable;
        /// <summary>El ancho del río en cada punto, en mm del campo (vacío si el artefacto no lo trae).</summary>
        public long[] Anchos;
    }

    public sealed class Lugar
    {
        /// <summary>Metros del campo por u del mundo (el zoom, el mismo en todas las batallas): 1 milésima de u son 75 mm.</summary>
        public const int EscalaCampo = 75;

        /// <summary>El centro del recorte, en milésimas de u del mundo: el origen del marco del campo y de su ruido.</summary>
        public Punto Centro;
        /// <summary>La batalla grande (lado del recorte de 1000 m jugables) o la pequeña.</summary>
        public bool Grande;
        /// <summary>La posición del defensor, en el campo.</summary>
        public Punto PuntoDefensor;
        /// <summary>De dónde llega cada bando, en el campo; null si estaba quieto.</summary>
        public Punto? ProcedenciaAtacante, ProcedenciaDefensor;
        /// <summary>Alturas del mundo en mm (sin zoom), cada 0,5 u.</summary>
        public Rejilla Alturas;
        /// <summary>El agua de cada muestra de las alturas: 0 tierra, 1 lago, 2 mar; y en Q16 (0 o 1), para interpolarla.</summary>
        public Rejilla Agua, AguaQ16;
        /// <summary>Por celda de 4 u, con la muestra en su centro: la clase de terreno (1 agua … 6 cima), el bosque y la fertilidad (Q16).</summary>
        public Rejilla Terreno, Bosque, Fertilidad;
        public List<RioDelLugar> Rios = new List<RioDelLugar>();

        /// <summary>La x absoluta del ruido (mm): la del centro del recorte por la escala del campo.</summary>
        public long AbsolutoX => Centro.X * EscalaCampo;
        public long AbsolutoY => Centro.Y * EscalaCampo;

        /// <summary>Una posición del mundo (milésimas de u), en el campo (mm).</summary>
        public Punto AlCampo(Punto mundo) => new Punto((mundo.X - Centro.X) * EscalaCampo, (mundo.Y - Centro.Y) * EscalaCampo);

        /// <summary>Lo que el lado del recorte dice del tamaño: el área jugable más dos anillos de 700 m, por la escala.</summary>
        public static bool EsGrande(long ladoMilesimas) => ladoMilesimas * EscalaCampo / 1000 - 2 * 700_000 / 1000 > (600 + 1000) / 2;

        // Los umbrales de la clase de terreno del mundo (`generador/src/config.ts`), en milésimas de la altura máxima: costa, colina, montaña y cima.
        static readonly int[] Umbrales = { 420, 545, 600, 685 };

        /// <summary>La clase de terreno de una celda, como la saca el motor (`ticket_del_lugar.rs`) y el mapa de mundo: agua, o por la altura de la
        /// muestra más cercana a su centro.</summary>
        public static int ClaseDeTerreno(ushort altura, bool agua)
        {
            if (agua) return 1;
            int clase = 2;
            foreach (int u in Umbrales) if ((long)altura * 1000 >= (long)u * 65535) clase++;
            return clase;
        }

        /// <summary>El recorte a partir de lo que trae el ticket; posiciones en milésimas de u, alturas en el u16 del relieve (0..65535 = 0..1000 m).</summary>
        public static Lugar Crear(Punto centro, long ladoMilesimas, Punto punto, Punto? procedenciaAtacante, Punto? procedenciaDefensor,
            Punto origenRelieve, long pasoRelieve, int columnas, int filas, ushort[] alturas, byte[] agua,
            Punto origenCeldas, long pasoCeldas, int columnasCeldas, int filasCeldas, byte[] bosque, byte[] fertilidad, IEnumerable<(Punto[] puntos, Punto[] vados, bool navegable, long[] anchos)> rios)
        {
            if (alturas.Length != columnas * filas || agua.Length != alturas.Length) throw new ArgumentException("el relieve del recorte no casa con sus medidas");
            if (bosque.Length != columnasCeldas * filasCeldas || fertilidad.Length != bosque.Length) throw new ArgumentException("las celdas del recorte no casan con sus medidas");
            var l = new Lugar { Centro = centro, Grande = EsGrande(ladoMilesimas) };
            l.PuntoDefensor = l.AlCampo(punto);
            if (procedenciaAtacante.HasValue) l.ProcedenciaAtacante = l.AlCampo(procedenciaAtacante.Value);
            if (procedenciaDefensor.HasValue) l.ProcedenciaDefensor = l.AlCampo(procedenciaDefensor.Value);
            var o = l.AlCampo(origenRelieve);
            l.Alturas = new Rejilla(o.X, o.Y, pasoRelieve * EscalaCampo, columnas, filas);
            l.Agua = new Rejilla(o.X, o.Y, pasoRelieve * EscalaCampo, columnas, filas);
            l.AguaQ16 = new Rejilla(o.X, o.Y, pasoRelieve * EscalaCampo, columnas, filas);
            for (int k = 0; k < alturas.Length; k++)
            {
                l.Alturas.Datos[k] = (int)DivRedondo(alturas[k] * 1_000_000L, 65535);
                l.Agua.Datos[k] = agua[k];
                l.AguaQ16.Datos[k] = agua[k] != 0 ? Uno : 0;
            }
            // las celdas, con la muestra en su centro
            var oc = l.AlCampo(new Punto(origenCeldas.X + pasoCeldas / 2, origenCeldas.Y + pasoCeldas / 2));
            l.Terreno = new Rejilla(oc.X, oc.Y, pasoCeldas * EscalaCampo, columnasCeldas, filasCeldas);
            l.Bosque = new Rejilla(oc.X, oc.Y, pasoCeldas * EscalaCampo, columnasCeldas, filasCeldas);
            l.Fertilidad = new Rejilla(oc.X, oc.Y, pasoCeldas * EscalaCampo, columnasCeldas, filasCeldas);
            for (int j = 0, k = 0; j < filasCeldas; j++)
                for (int i = 0; i < columnasCeldas; i++, k++)
                {
                    long cx = origenCeldas.X + i * pasoCeldas + pasoCeldas / 2, cy = origenCeldas.Y + j * pasoCeldas + pasoCeldas / 2;
                    int mi = (int)Clamp(DivSuelo(cx - origenRelieve.X, pasoRelieve), 0, columnas - 1), mj = (int)Clamp(DivSuelo(cy - origenRelieve.Y, pasoRelieve), 0, filas - 1);
                    int m = mj * columnas + mi;
                    l.Terreno.Datos[k] = ClaseDeTerreno(alturas[m], agua[m] != 0);
                    l.Bosque.Datos[k] = (int)DivRedondo(bosque[k] * (long)Uno, 255);
                    l.Fertilidad.Datos[k] = (int)DivRedondo(fertilidad[k] * (long)Uno, 255);
                }
            foreach (var (puntos, vados, navegable, anchos) in rios)
            {
                l.Rios.Add(new RioDelLugar
                {
                    Puntos = Array.ConvertAll(puntos, l.AlCampo),
                    Vados = Array.ConvertAll(vados, l.AlCampo),
                    Navegable = navegable,
                    Anchos = Array.ConvertAll(anchos ?? Array.Empty<long>(), a => a * EscalaCampo),
                });
            }
            return l;
        }

        /// <summary>El recorte del ticket de batalla (`BattleMapReference`, contrato 0.34.0); null si no lo trae (el motor no tenía el relieve:
        /// se juega en el campo de siempre). `puntoDeLaBatalla` es el `punto` del contexto: la posición del defensor.</summary>
        public static Lugar DesdeContrato(pb.BattleMapReference mapa, pb.Punto puntoDeLaBatalla)
        {
            var r = mapa?.Recorte;
            if (r == null || r.Relieve == null) return null;
            Punto P(pb.Punto q) => new Punto(q.X, q.Y);
            Punto? Procedencia(pb.ProcedenciaDelLugar q) => q == null || q.Quieto ? (Punto?)null : P(q.Punto);
            var rel = r.Relieve;
            int n = (int)(rel.Columnas * rel.Filas);
            var crudas = rel.Alturas.Span;
            if (crudas.Length != 2 * n || rel.Agua.Length < (n + 3) / 4) throw new ArgumentException("el relieve del recorte no casa con sus medidas");
            var alturas = new ushort[n];
            var agua = new byte[n];
            for (int k = 0; k < n; k++)
            {
                alturas[k] = (ushort)(crudas[2 * k] | crudas[2 * k + 1] << 8);
                agua[k] = (byte)(rel.Agua[k >> 2] >> (2 * (k & 3)) & 3);
            }
            var rios = new List<(Punto[], Punto[], bool, long[])>();
            foreach (var rio in r.Rios)
                rios.Add((rio.Puntos.ATabla(P), rio.Vados.ATabla(P), rio.Navegable, rio.Anchos.Count == 0 ? null : rio.Anchos.ATabla(a => (long)a)));
            var b = r.Bosque;
            return Crear(P(mapa.Centro), r.Lado, P(puntoDeLaBatalla), Procedencia(r.ProcedenciaAtacante), Procedencia(r.ProcedenciaDefensor),
                P(rel.Origen), rel.Paso, (int)rel.Columnas, (int)rel.Filas, alturas, agua,
                P(b.Origen), b.Paso, (int)b.Columnas, (int)b.Filas, b.Valores.ToByteArray(), r.Fertilidad.Valores.ToByteArray(), rios);
        }

        /// <summary>El ticket en el JSON de la mesa del campo (`TicketDelLugar`, `pruebas/campo3d/generador.ts` de BronzeAgeClient; el motor lo saca
        /// con `cargo run -p nucleo --example ticket_del_lugar`). La clase de terreno que trae se ignora: sale de las alturas, como del contrato.</summary>
        public static Lugar DesdeJsonDeLaMesa(string json)
        {
            var t = JObject.Parse(json);
            if ((int)t["version"] != 1) throw new ArgumentException($"ticket de la mesa de versión {t["version"]}: se lee la 1");
            long Mil(JToken v) => (long)Math.Round((double)v * 1000);
            Punto P(JToken q) => new Punto(Mil(q["x"]), Mil(q["y"]));
            Punto? Procedencia(JToken q) => (bool)q["quieto"] ? (Punto?)null : P(q["punto"]);
            var r = t["recorte"];
            var alt = r["alturas"];
            var agua = (string)r["agua"];
            var bosque = r["bosque"];
            byte[] Bytes(JToken g) => g["valores"].ToObject<byte[]>();
            var rios = new List<(Punto[], Punto[], bool, long[])>();
            foreach (var rio in r["rios"])
            {
                var anchos = rio["anchos"] as JArray;
                rios.Add((rio["puntos"].ATabla(P), rio["vados"].ATabla(P), (bool?)rio["navegable"] ?? false, anchos == null ? null : anchos.ATabla(Mil)));
            }
            return Crear(P(t["centro"]), Mil(r["lado_recorte"]), P(t["punto"]), Procedencia(r["procedencia_atacante"]), Procedencia(r["procedencia_defensor"]),
                P(alt["origen"]), Mil(alt["paso"]), (int)alt["columnas"], (int)alt["filas"], alt["valores"].ToObject<ushort[]>(), Array.ConvertAll(agua.ToCharArray(), c => (byte)(c - '0')),
                P(bosque["origen"]), Mil(bosque["paso"]), (int)bosque["columnas"], (int)bosque["filas"], Bytes(bosque), Bytes(r["fertilidad"]), rios);
        }
    }

    static class Listas
    {
        public static T[] ATabla<S, T>(this IEnumerable<S> lista, Func<S, T> f)
        {
            var r = new List<T>();
            foreach (var x in lista) r.Add(f(x));
            return r.ToArray();
        }
    }
}
