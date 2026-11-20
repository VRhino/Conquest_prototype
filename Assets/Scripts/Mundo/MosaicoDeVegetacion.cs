// El mosaico de la vegetación del mapa de mundo: «wave function collapse» por reglas de vecindad sobre las reglas del prototipo
// (`ReglasDeVegetacion`), para que el bosque tenga estructura: bosque cerrado, abierto, lindes, claros, matorral, pradera, ribera, cañaveral y
// pedregal que se juntan como deben, y rodales de una especie separados por franjas mixtas. Enriquece, no sustituye: el prototipo sigue
// poniendo cada árbol (posición, tamaño, color y especie con sus mismos azares); el mosaico solo decide cuántos caben en cada sitio y qué
// especie manda en cada rodal.
//  * Estructura: celdas de 2 u por tesela (32 × 32 en una de 64 u). Lo que manda el motor y el prototipo se pone antes de resolver y nunca se
//    viola (densidad de bosque, pendiente, agua, ribera, claros de los sitios). El anillo de celdas del borde de cada tesela sale de una
//    función pura de los datos (`EstadoPorDatos`): dos teselas vecinas casan sin esperarse.
//  * Rodales: segunda pasada más gruesa (celdas de 8 u) para la especie que manda, con los pesos de las zonas del prototipo; dos especies no
//    se tocan sin una celda mixta en medio; el ciprés nunca manda; el álamo, solo en la ribera. El borde, el dominante del prototipo.
//  * Determinista: la semilla sale del mundo y de la tesela. Si el resolvedor llega a una contradicción, reintenta con otra semilla unas
//    cuantas veces; si no, la tesela se queda con el reparto del prototipo.
// Pura (sin Unity): la usa `VegetacionDelMapa` en el hilo del reparto y la prueban `MosaicoDeVegetacionTests`.
using System;
using System.Collections.Generic;

namespace ConquestTactics.Mundo
{
    public enum EstadoDeCelda : byte { Vacio, Cerrado, Abierto, Linde, Claro, Matorral, Pradera, Ribera, Canaveral, Pedregal }

    /// <summary>La tabla del mosaico: qué estados pueden ser vecinos y cuánto pesa cada uno. Editable (un JSON en Resources); por defecto, la de
    /// aquí.</summary>
    public sealed class TablaDelMosaico
    {
        public const int Estados = 10;
        /// <summary>Pares permitidos (simétricos), por nombre.</summary>
        public List<string[]> Vecinos = new List<string[]>
        {
            new[] { "Cerrado", "Cerrado" }, new[] { "Cerrado", "Abierto" }, new[] { "Cerrado", "Linde" }, new[] { "Cerrado", "Ribera" },
            new[] { "Abierto", "Abierto" }, new[] { "Abierto", "Linde" }, new[] { "Abierto", "Claro" }, new[] { "Abierto", "Matorral" },
            new[] { "Abierto", "Pradera" }, new[] { "Abierto", "Pedregal" }, new[] { "Abierto", "Ribera" },
            new[] { "Linde", "Linde" }, new[] { "Linde", "Claro" }, new[] { "Linde", "Matorral" }, new[] { "Linde", "Pradera" },
            new[] { "Linde", "Pedregal" }, new[] { "Linde", "Ribera" }, new[] { "Linde", "Canaveral" },
            new[] { "Claro", "Claro" },
            new[] { "Matorral", "Matorral" }, new[] { "Matorral", "Pradera" }, new[] { "Matorral", "Pedregal" }, new[] { "Matorral", "Ribera" },
            new[] { "Matorral", "Canaveral" },
            new[] { "Pradera", "Pradera" }, new[] { "Pradera", "Pedregal" }, new[] { "Pradera", "Ribera" }, new[] { "Pradera", "Canaveral" },
            new[] { "Pedregal", "Pedregal" }, new[] { "Pedregal", "Ribera" },
            new[] { "Ribera", "Ribera" }, new[] { "Ribera", "Canaveral" },
            new[] { "Canaveral", "Canaveral" },
        };
        /// <summary>Cuántos árboles caben en cada estado, como parte de la densidad del prototipo (cerrado: copas tocándose, hacia el 75-80 % de
        /// cobertura; abierto: el prototipo), y cuánto se refuerzan las matas.</summary>
        public Dictionary<string, double> Arboles = new Dictionary<string, double>
        {
            ["Cerrado"] = 3.6, ["Abierto"] = 1, ["Linde"] = 0.35, ["Claro"] = 0, ["Matorral"] = 0, ["Pradera"] = 0, ["Ribera"] = 0.8,
            ["Canaveral"] = 0, ["Pedregal"] = 0, ["Vacio"] = 0,
        };
        public Dictionary<string, double> Matas = new Dictionary<string, double>
        {
            ["Cerrado"] = 0.6, ["Abierto"] = 1, ["Linde"] = 2, ["Claro"] = 1, ["Matorral"] = 1.6, ["Pradera"] = 0.15, ["Ribera"] = 1,
            ["Canaveral"] = 0.3, ["Pedregal"] = 0.8, ["Vacio"] = 0,
        };
        /// <summary>Peso de una celda mixta entre rodales y cuánto se refuerza la especie que ya manda al lado (rodales más grandes).</summary>
        public double PesoMixta = 0.35, RefuerzoVecina = 6;
        /// <summary>Cuánto se refuerza un estado de estructura que ya tiene al lado (manchas de varias celdas, no sal y pimienta).</summary>
        public double AfinidadPropia = 3.5;
        /// <summary>Reintentos con otra semilla antes de caer al reparto del prototipo.</summary>
        public int Reintentos = 6;

        bool[,] permitidos;
        double[] arboles, matas;

        public bool Permitido(EstadoDeCelda a, EstadoDeCelda b)
        {
            Preparar();
            return permitidos[(int)a, (int)b];
        }

        public double ParteDeArboles(EstadoDeCelda e) { Preparar(); return arboles[(int)e]; }
        public double RefuerzoDeMatas(EstadoDeCelda e) { Preparar(); return matas[(int)e]; }

        void Preparar()
        {
            if (permitidos != null) return;
            var p = new bool[Estados, Estados];
            for (int i = 0; i < Estados; i++) { p[0, i] = true; p[i, 0] = true; } // el vacío va con todo
            foreach (var par in Vecinos)
            {
                var a = Convert.ToInt32(Enum.Parse(typeof(EstadoDeCelda), par[0]));
                var b = Convert.ToInt32(Enum.Parse(typeof(EstadoDeCelda), par[1]));
                p[a, b] = p[b, a] = true;
            }
            var ar = new double[Estados];
            var ma = new double[Estados];
            foreach (EstadoDeCelda e in Enum.GetValues(typeof(EstadoDeCelda)))
            {
                ar[(int)e] = Arboles.TryGetValue(e.ToString(), out var v) ? v : 0;
                ma[(int)e] = Matas.TryGetValue(e.ToString(), out var w) ? w : 1;
            }
            arboles = ar;
            matas = ma;
            permitidos = p;
        }
    }

    /// <summary>Lo que se sabe de una celda antes de resolver.</summary>
    public struct DatosDeCelda
    {
        /// <summary>Densidad de bosque (0..1, la del prototipo), fertilidad, pendiente (m/m), distancia al agua (u) y altura (m).</summary>
        public double Densidad, Fertilidad, Pendiente, DAgua, Altura;
        /// <summary>Agua (mar, lago o cauce) o el claro de un sitio: la celda queda vacía. `AguaAlLado`: una celda vecina es agua.</summary>
        public bool Vacia, AguaAlLado;
        public double X, Y;
    }

    public static class MosaicoDeVegetacion
    {
        public const double Celda = 2, CeldaRodal = 8;

        /// <summary>Los estados que admite la celda (bits), por lo que mandan el motor y el prototipo (nunca se violan).</summary>
        public static int Admitidos(in DatosDeCelda d, double colina)
        {
            if (d.Vacia) return Bit(EstadoDeCelda.Vacio);
            if (d.Pendiente > ReglasDeVegetacion.PendienteMaxima) return Bit(EstadoDeCelda.Pedregal);
            int r;
            if (d.Densidad < ReglasDeVegetacion.DensidadMinima)
                r = Bit(EstadoDeCelda.Pradera) | Bit(EstadoDeCelda.Matorral) | Bit(EstadoDeCelda.Pedregal);
            else if (d.Densidad >= 0.7)
                r = Bit(EstadoDeCelda.Cerrado) | Bit(EstadoDeCelda.Abierto) | Bit(EstadoDeCelda.Claro);
            else
                r = Bit(EstadoDeCelda.Cerrado) | Bit(EstadoDeCelda.Abierto) | Bit(EstadoDeCelda.Linde) | Bit(EstadoDeCelda.Claro);
            if (d.DAgua < ReglasDeVegetacion.Ribera && d.Altura < colina) r |= Bit(EstadoDeCelda.Ribera);
            if (d.AguaAlLado) r |= Bit(EstadoDeCelda.Canaveral);
            return r;
        }

        /// <summary>El peso de cada estado en la celda (mueve el sorteo; las reglas ya se cumplen con `Admitidos`).</summary>
        public static double Peso(EstadoDeCelda e, in DatosDeCelda d)
        {
            double b = d.Densidad;
            switch (e)
            {
                case EstadoDeCelda.Cerrado: return b >= 0.7 ? 12 : 3 * Suave(0.4, 0.7, b) + 0.05;
                case EstadoDeCelda.Abierto: return b >= 0.7 ? 0.5 : 1.5 * Campana(b, 0.45, 0.2) + 0.1;
                case EstadoDeCelda.Linde: return 1.2 * Campana(b, 0.27, 0.12) + 0.05;
                // El campo abierto, por manchas de unas 12 u (ruido de baja frecuencia propio de cada estado).
                case EstadoDeCelda.Claro: return (b >= 0.7 ? 0.08 : 0.2) * (0.3 + 1.6 * ReglasDeVegetacion.Manchas(d.X, d.Y, 12, 823));
                // la mancha decide casi sola (0,02 frente a 2): así salen manchas de varias celdas y no sal y pimienta
                case EstadoDeCelda.Matorral: return (0.3 + 1.2 * (1 - d.Fertilidad) + 0.6 * Suave(0.1, 0.2, b)) * (0.02 + 2 * Suave(0.42, 0.58, ReglasDeVegetacion.Manchas(d.X, d.Y, 12, 811) + 0.25 * (0.5 - d.Fertilidad)));
                case EstadoDeCelda.Pradera: return (0.3 + 1.5 * d.Fertilidad) * (0.02 + 2 * (1 - Suave(0.42, 0.58, ReglasDeVegetacion.Manchas(d.X, d.Y, 12, 811) + 0.25 * (0.5 - d.Fertilidad))));
                case EstadoDeCelda.Pedregal: return 0.05 + 2 * Suave(0.5, 1.1, d.Pendiente);
                case EstadoDeCelda.Ribera: return 3;
                case EstadoDeCelda.Canaveral: return 4;
                default: return 1;
            }
        }

        /// <summary>El estado del anillo del borde (función pura de los datos de la celda: la misma vista desde las dos teselas vecinas). Clases
        /// ordenadas por la densidad, con un ruido suave que mueve los cortes, y las reglas fijas del agua y la pendiente.</summary>
        public static EstadoDeCelda EstadoPorDatos(in DatosDeCelda d, double colina)
        {
            int a = Admitidos(d, colina);
            if (a == Bit(EstadoDeCelda.Vacio)) return EstadoDeCelda.Vacio;
            if (a == Bit(EstadoDeCelda.Pedregal)) return EstadoDeCelda.Pedregal;
            double b = d.Densidad + (ReglasDeVegetacion.Manchas(d.X, d.Y, 6, 503) - 0.5) * 0.1;
            if ((a & Bit(EstadoDeCelda.Canaveral)) != 0 && b < 0.2) return EstadoDeCelda.Canaveral;
            if ((a & Bit(EstadoDeCelda.Ribera)) != 0 && b < 0.45) return EstadoDeCelda.Ribera;
            if (d.Densidad >= 0.7) return EstadoDeCelda.Cerrado;
            if (d.Densidad >= ReglasDeVegetacion.DensidadMinima) return b >= 0.55 ? EstadoDeCelda.Cerrado : b >= 0.35 ? EstadoDeCelda.Abierto : EstadoDeCelda.Linde;
            return d.Fertilidad < 0.35 ? EstadoDeCelda.Matorral : EstadoDeCelda.Pradera;
        }

        static int Bit(EstadoDeCelda e) => 1 << (int)e;
        static double Suave(double a, double b, double x) { double t = Math.Min(1, Math.Max(0, (x - a) / (b - a))); return t * t * (3 - 2 * t); }
        static double Campana(double x, double c, double ancho) { double t = (x - c) / ancho; return Math.Exp(-t * t); }

        /// <summary>Resuelve la estructura de una rejilla de `n × n` celdas: las del borde, fijas por `EstadoPorDatos`; el resto, por WFC. Devuelve
        /// null si no hay solución tras los reintentos (entonces, el reparto del prototipo).</summary>
        public static EstadoDeCelda[] Estructura(DatosDeCelda[] datos, int n, int semilla, double colina, TablaDelMosaico tabla, out int intentos)
        {
            var admitidos = new int[n * n];
            for (int k = 0; k < n * n; k++)
            {
                int i = k % n, j = k / n;
                admitidos[k] = i == 0 || j == 0 || i == n - 1 || j == n - 1 ? Bit(EstadoPorDatos(datos[k], colina)) : Admitidos(datos[k], colina);
            }
            for (intentos = 1; intentos <= tabla.Reintentos; intentos++)
            {
                var r = Resolver(admitidos, n, TablaDelMosaico.Estados, semilla + intentos * 7919, (a, b) => tabla.Permitido((EstadoDeCelda)a, (EstadoDeCelda)b),
                    (k, e) => Peso((EstadoDeCelda)e, datos[k]), (e, vecinos) => vecinos[e] > 0 ? Math.Pow(tabla.AfinidadPropia, vecinos[e] / 2.0) : 1);
                if (r == null) continue;
                var estados = new EstadoDeCelda[n * n];
                for (int k = 0; k < n * n; k++) estados[k] = (EstadoDeCelda)r[k];
                return estados;
            }
            return null;
        }

        /// <summary>Los rodales: qué especie manda en cada celda de 8 u (o −1, mixta). El borde, el dominante del prototipo en el centro de la
        /// celda; el resto, por WFC con los pesos de las zonas, sin dos especies distintas tocándose, sin ciprés y con el álamo solo en la
        /// ribera. `zonas[k]`: la zona del centro de cada celda; `bordes[k]`: el dominante del prototipo.</summary>
        public static int[] Rodales(ZonaDeVegetacion[] zonas, EspecieDeArbol[] bordes, int n, int semilla, TablaDelMosaico tabla, out int intentos)
        {
            const int Mixta = 7, Estados = 8;
            var admitidos = new int[n * n];
            for (int k = 0; k < n * n; k++)
            {
                int i = k % n, j = k / n;
                if (i == 0 || j == 0 || i == n - 1 || j == n - 1) { admitidos[k] = 1 << (int)bordes[k]; continue; }
                int a = 1 << Mixta;
                foreach (var (e, _) in ReglasDeVegetacion.MandanEn(zonas[k]))
                    if (e != EspecieDeArbol.Alamo || zonas[k] == ZonaDeVegetacion.Ribera || zonas[k] == ZonaDeVegetacion.RiberaAlta) a |= 1 << (int)e;
                admitidos[k] = a;
            }
            bool Permitido(int a, int b) => a == b || a == Mixta || b == Mixta;
            for (intentos = 1; intentos <= tabla.Reintentos; intentos++)
            {
                var r = Resolver(admitidos, n, Estados, semilla + intentos * 104729, Permitido,
                    (k, e) => e == Mixta ? tabla.PesoMixta : ReglasDeVegetacion.PesoEn(zonas[k], (EspecieDeArbol)e),
                    (e, vecinos) => vecinos[e] > 0 && e != Mixta ? tabla.RefuerzoVecina : 1);
                if (r == null) continue;
                for (int k = 0; k < n * n; k++) if (r[k] == Mixta) r[k] = -1;
                return r;
            }
            return null;
        }

        /// <summary>El resolvedor (WFC por reglas de vecindad en 4 direcciones): la celda con menos estados posibles (desempate al azar) se
        /// decide por peso; luego se propaga hasta que todo cuadra. Devuelve null si alguna celda se queda sin estados.</summary>
        static int[] Resolver(int[] admitidos, int n, int estados, int semilla, Func<int, int, bool> permitido, Func<int, int, double> peso,
            Func<int, int[], double> refuerzo)
        {
            var posibles = (int[])admitidos.Clone();
            var fijas = new bool[n * n];
            for (int k = 0; k < n * n; k++) { int i = k % n, j = k / n; fijas[k] = i == 0 || j == 0 || i == n - 1 || j == n - 1; }
            // tabla de compatibilidad: para cada estado, la máscara de los que pueden ir al lado
            var compatibles = new int[estados];
            for (int a = 0; a < estados; a++)
                for (int b = 0; b < estados; b++)
                    if (permitido(a, b)) compatibles[a] |= 1 << b;
            var rng = new Random(semilla);
            var desempate = new double[n * n];
            for (int k = 0; k < n * n; k++) desempate[k] = rng.NextDouble();
            var pila = new Stack<int>();
            for (int k = 0; k < n * n; k++) pila.Push(k);
            if (!Propagar(posibles, n, estados, compatibles, pila, fijas)) return null;
            var cuentaVecinos = new int[estados];
            while (true)
            {
                // la celda sin decidir con menos posibles
                int mejor = -1, menos = int.MaxValue;
                for (int k = 0; k < n * n; k++)
                {
                    int c = Cuenta(posibles[k]);
                    if (c <= 1) continue;
                    if (c < menos || (c == menos && desempate[k] > desempate[mejor])) { mejor = k; menos = c; }
                }
                if (mejor < 0) break;
                // sorteo por peso (con el refuerzo de lo que ya hay al lado)
                Array.Clear(cuentaVecinos, 0, estados);
                if (refuerzo != null)
                    foreach (var v in Vecinas(mejor, n))
                        if (Cuenta(posibles[v]) == 1) cuentaVecinos[Primero(posibles[v])]++;
                double total = 0;
                var pesos = new double[estados];
                for (int e = 0; e < estados; e++)
                    if ((posibles[mejor] & (1 << e)) != 0)
                    {
                        pesos[e] = Math.Max(1e-6, peso(mejor, e)) * (refuerzo?.Invoke(e, cuentaVecinos) ?? 1);
                        total += pesos[e];
                    }
                double s = rng.NextDouble() * total;
                int elegido = -1;
                for (int e = 0; e < estados; e++) if (pesos[e] > 0 && (s -= pesos[e]) < 0) { elegido = e; break; }
                if (elegido < 0) elegido = Primero(posibles[mejor]);
                posibles[mejor] = 1 << elegido;
                pila.Push(mejor);
                if (!Propagar(posibles, n, estados, compatibles, pila, fijas)) return null;
            }
            var r = new int[n * n];
            for (int k = 0; k < n * n; k++) r[k] = Primero(posibles[k]);
            return r;
        }

        static bool Propagar(int[] posibles, int n, int estados, int[] compatibles, Stack<int> pila, bool[] fijas)
        {
            while (pila.Count > 0)
            {
                int k = pila.Pop();
                // lo que pueden ser las vecinas de k
                int alLado = 0;
                for (int e = 0; e < estados; e++) if ((posibles[k] & (1 << e)) != 0) alLado |= compatibles[e];
                foreach (var v in Vecinas(k, n))
                {
                    if (fijas[v]) continue;
                    int antes = posibles[v], ahora = antes & alLado;
                    if (ahora == antes) continue;
                    if (ahora == 0) return false;
                    posibles[v] = ahora;
                    pila.Push(v);
                }
            }
            return true;
        }

        static IEnumerable<int> Vecinas(int k, int n)
        {
            int i = k % n, j = k / n;
            if (i > 0) yield return k - 1;
            if (i < n - 1) yield return k + 1;
            if (j > 0) yield return k - n;
            if (j < n - 1) yield return k + n;
        }

        static int Cuenta(int m) { int c = 0; while (m != 0) { m &= m - 1; c++; } return c; }
        static int Primero(int m) { for (int e = 0; e < 32; e++) if ((m & (1 << e)) != 0) return e; return -1; }
    }
}
