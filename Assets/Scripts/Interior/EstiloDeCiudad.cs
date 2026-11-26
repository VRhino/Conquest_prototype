using System.Collections.Generic;

/// <summary>
/// Con qué estilo y de qué época se viste cada edificio de una plaza, como en el prototipo (`ciudad3d/estado.ts`, `estiloDe` y `epocaDe`).
/// El motor solo manda el nivel de la plaza, su ascenso en curso y el estilo de época de la Facción (Era y grado); el reparto edificio a
/// edificio lo hace el cliente, y sale fijo del id de cada uno: así la ciudad cambia casa a casa y no toda de golpe.
///   - El ESTILO (1 a 5) es el nivel de la plaza: tamaño, forma y riqueza. Durante un ascenso, cada casa pasa al nivel nuevo cuando el
///     avance alcanza su umbral; las más viejas se quedan siempre un nivel atrás; los monumentales van con el nivel y cambian al final.
///   - La ÉPOCA (1 a 5) es la Era de la Facción: techos, muros, columnas y colores, sin tocar el tamaño. Con cada grado luce la Era nueva una
///     parte mayor de la ciudad, empezando por lo público; el resto sigue en la anterior.
/// </summary>
public static class EstiloDeCiudad
{
    /// <summary>Los que son la cara de la plaza: van con su nivel sin quedarse atrás, y son los primeros en vestirse de la Era nueva.</summary>
    static readonly HashSet<string> Monumentales = new HashSet<string> { "centroUrbano", "palacio", "salaConsejo", "maravilla", "plaza" };
    /// <summary>Por encima de este umbral, una casa se queda un nivel atrás: la ciudad no es uniforme.</summary>
    const double Viejas = 0.82;
    /// <summary>La parte de la ciudad que ya luce la Era nueva con cada grado (1 primeros atisbos, 2 más cambiada, 3 del todo).</summary>
    static readonly double[] PartePorGrado = { 0, 0.2, 0.6, 1 };

    /// <summary>Un número fijo entre 0 y 1 por texto (el mismo `hashTexto` del prototipo, para que una plaza se vea igual en los dos).</summary>
    public static double HashTexto(string texto, uint sal = 0)
    {
        unchecked
        {
            uint h = 2166136261u ^ sal;
            foreach (char c in texto) h = (h ^ c) * 16777619u;
            h = (h ^ (h >> 15)) * 2246822507u;
            h = (h ^ (h >> 13)) * 3266489909u;
            return (h ^ (h >> 16)) / 4294967296.0;
        }
    }

    /// <summary>El estilo de un edificio. `nivelObjetivo` 0 = sin ascenso en curso; los tiempos, en milisegundos de mundo.</summary>
    public static int EstiloDe(string id, string tipo, int nivel, int nivelObjetivo = 0, double desdeMs = 0, double hastaMs = 0, double ahoraMs = 0)
    {
        double u = HashTexto(id ?? "", 7);
        bool sube = nivelObjetivo > 0, monumental = Monumentales.Contains(tipo);
        double p = sube ? System.Math.Max(0, System.Math.Min(1, (ahoraMs - desdeMs) / System.Math.Max(1, hastaMs - desdeMs))) : 0;
        int n = nivel;
        if (sube && (monumental ? p >= 1 : u < p)) n = nivelObjetivo;
        else if (!monumental && u > Viejas && nivel > 1) n = nivel - 1;
        // un puesto de mercado de una plaza de nivel 2 o más es siempre una tienda, nunca el tenderete del 1
        if (tipo == "puestoMercado" && nivel >= 2) n = System.Math.Max(2, n);
        return System.Math.Max(1, System.Math.Min(5, n));
    }

    /// <summary>La época de un edificio: la Era de la plaza si ya le tocó con este grado, y si no, la anterior.</summary>
    public static int EpocaDe(string id, string tipo, int era, int grado)
    {
        if (era <= 1) return 1;
        era = System.Math.Min(5, era);
        double parte = PartePorGrado[System.Math.Max(1, System.Math.Min(3, grado))];
        double turno = Monumentales.Contains(tipo) ? 0 : HashTexto(id ?? "", 23);
        return turno < parte ? era : era - 1;
    }
}
