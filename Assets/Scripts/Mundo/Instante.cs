// Lo que cambia con el reloj del mundo sin que llegue nada: dónde va quien sigue un plan y cuánto vale un ritmo (puerto de `evaluarPlan` y
// `valorDeRitmo` de `BronzeAgeClient/src/motor/proyeccion.ts`). El servidor no manda nada mientras una columna marcha: la vista lo pide en
// cada fotograma con `Sesion.AhoraMundo`. Unidades del contrato: milésimas de unidad y ms desde 2026-01-01.
using System;
using System.Collections.Generic;
using BronzeAge.Contract.V1;

namespace ConquestTactics.Mundo
{
    public static class Instante
    {
        /// <summary>2026-01-01T00:00:00Z: el cero de los instantes del contrato (common.proto).</summary>
        public static readonly DateTime Epoca = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public static DateTime AFecha(double msContrato) => Epoca.AddMilliseconds(msContrato);

        /// <summary>Dónde está en `ahora` (ms del contrato) quien sigue `plan` desde `posicion`, en milésimas; y si aún anda.</summary>
        public static (double x, double y, bool enMarcha) Posicion(Plan plan, Punto posicion, double ahora)
        {
            var v = plan?.Vertices;
            if (v == null || v.Count == 0) return (posicion?.X ?? 0, posicion?.Y ?? 0, false);
            if (ahora <= v[0].EnMs) return (v[0].P.X, v[0].P.Y, v.Count > 1);
            for (int i = 1; i < v.Count; i++)
            {
                if (ahora >= v[i].EnMs) continue;
                double f = (ahora - v[i - 1].EnMs) / Math.Max(1, v[i].EnMs - v[i - 1].EnMs);
                Punto a = v[i - 1].P, b = v[i].P;
                return (a.X + (b.X - a.X) * f, a.Y + (b.Y - a.Y) * f, true);
            }
            var fin = v[v.Count - 1].P;
            return (fin.X, fin.Y, false);
        }

        /// <summary>Lo que le queda de ruta en `ahora`, empezando por donde está (en marcha, dos puntos o más).</summary>
        public static List<(double x, double y)> RutaRestante(Plan plan, Punto posicion, double ahora)
        {
            var r = new List<(double, double)>();
            var p = Posicion(plan, posicion, ahora);
            if (!p.enMarcha) return r;
            r.Add((p.x, p.y));
            foreach (var x in plan.Vertices) if (x.EnMs > ahora) r.Add((x.P.X, x.P.Y));
            return r;
        }

        /// <summary>El valor de un ritmo en `ahora` (ms del contrato), en milésimas. En escalones no se interpola.</summary>
        public static double Valor(Ritmo r, double ahora)
        {
            if (r == null) return 0;
            double v = r.ValorEnT0;
            if (!r.EnEscalones) v += r.PorMinuto * (ahora - r.T0Ms) / 60_000.0;
            if (r.HasMaximo) v = Math.Min(v, r.Maximo);
            if (r.HasMinimo) v = Math.Max(v, r.Minimo);
            return v;
        }
    }
}
