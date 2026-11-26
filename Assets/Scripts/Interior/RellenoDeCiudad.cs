using System;

/// <summary>Lo que hay en una casilla libre de la ciudad por dentro (una casilla = 1 u.l. = 3 m).</summary>
public enum Casilla : byte { Ocupada, Campo, Huerto, Patio, Arboleda, Labor, Monte }

/// <summary>De qué está cerca una casilla libre: de ello depende qué es más fácil que salga.</summary>
public enum Vecindad : byte { Nada, Vivienda, Granja, Lenera, Afueras, Ocupada = 254, Campo = 255 }

/// <summary>
/// El relleno de las casillas libres de un asentamiento: en vez de sortear cada celda por su cuenta (lo que hacía el prototipo, y quedaba a
/// retales), se colapsa casilla a casilla al estilo WFC: cada una empieza pudiendo ser cualquier cosa, se resuelve primero la que menos
/// opciones tiene, lo que sale pesa según de qué está cerca (<see cref="Vecindad"/>) y de lo que ya tienen sus vecinas (los huertos, los
/// patios y las arboledas se juntan en manchas), y al fijarse prohíbe a las de al lado lo que no puede tener pegado (ni árboles ni leña junto
/// al huerto).
///
/// Todo sale de la posición (no hay azar con estado), y va por trozos de <see cref="Trozo"/> casillas en damero: primero los de un color, cada
/// uno por su cuenta, y luego los del otro, que casan con los cuatro que ya tienen al lado. Así el mismo plano da siempre el mismo relleno y
/// un edificio nuevo solo cambia los trozos que toca y sus vecinos, no la ciudad entera.
/// </summary>
public static class RellenoDeCiudad
{
    public const int Trozo = 6;
    const int Tipos = 5;                                  // Huerto, Patio, Arboleda, Labor, Monte
    const int Todo = (1 << Tipos) - 1;

    // lo que no puede haber al lado de cada cosa (bits, en el orden de arriba): junto al huerto, ni árboles ni leña
    static readonly int[] Veda = { 0b01100, 0, 0b00001, 0b00001, 0 };
    // cuánto tira cada cosa de su igual (una vez, tenga una vecina igual o cuatro: multiplicado por cada vecina, una mancha se lo come todo)
    static readonly float[] Apego = { 3.2f, 3f, 2.2f, 1.3f, 2.6f };
    // lo fácil que es cada cosa según la vecindad: Huerto, Patio, Arboleda, Labor, Monte
    static readonly float[][] Peso =
    {
        new[] { 0.14f, 0.12f, 0.14f, 0.08f, 0.52f },      // Nada
        new[] { 0.34f, 0.26f, 0.08f, 0.12f, 0.20f },      // Vivienda
        new[] { 0.28f, 0.08f, 0.08f, 0.22f, 0.34f },      // Granja
        new[] { 0.00f, 0.12f, 0.14f, 0.40f, 0.34f },      // Leñera
        new[] { 0.08f, 0.00f, 0.12f, 0.01f, 0.79f },      // Afueras
    };

    /// <summary>Un número de 0 a 1 que solo depende de la posición y de la semilla.</summary>
    public static float Azar(int x, int z, int k)
    {
        unchecked
        {
            uint h = (uint)x * 374761393u + (uint)z * 668265263u + (uint)k * 2246822519u;
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / 16777216f;
        }
    }

    static int Suelo(int a, int b) => a >= 0 ? a / b : -((-a + b - 1) / b);

    /// <summary>
    /// Rellena la rejilla. <paramref name="vecindad"/>: de qué está cerca cada casilla (o si está ocupada, o si ya es campo);
    /// <paramref name="fijas"/>: las que vienen ya decididas (opcional; <see cref="Casilla.Ocupada"/> = sin decidir);
    /// (<paramref name="origenX"/>, <paramref name="origenZ"/>): la casilla del mundo que es la [0, 0] de la rejilla.
    /// </summary>
    public static Casilla[,] Generar(Vecindad[,] vecindad, int origenX, int origenZ, int semilla, Casilla[,] fijas = null)
    {
        int ancho = vecindad.GetLength(0), alto = vecindad.GetLength(1);
        var sale = new Casilla[ancho, alto];
        var puede = new int[ancho, alto];
        var hecha = new bool[ancho, alto];
        for (int i = 0; i < ancho; i++)
            for (int j = 0; j < alto; j++)
            {
                var v = vecindad[i, j];
                if (v == Vecindad.Ocupada) { sale[i, j] = Casilla.Ocupada; hecha[i, j] = true; }
                else if (v == Vecindad.Campo) { sale[i, j] = Casilla.Campo; hecha[i, j] = true; }
                else puede[i, j] = Todo;
            }
        void Fijar(int i, int j, int t)
        {
            sale[i, j] = (Casilla)(t + (int)Casilla.Huerto);
            hecha[i, j] = true;
            for (int d = 0; d < 4; d++)
            {
                int a = i + (d == 0 ? 1 : d == 1 ? -1 : 0), b = j + (d == 2 ? 1 : d == 3 ? -1 : 0);
                if (a >= 0 && b >= 0 && a < ancho && b < alto && !hecha[a, b]) puede[a, b] &= ~Veda[t];
            }
        }
        if (fijas != null)
            for (int i = 0; i < ancho; i++)
                for (int j = 0; j < alto; j++)
                    if (!hecha[i, j] && fijas[i, j] >= Casilla.Huerto) Fijar(i, j, fijas[i, j] - Casilla.Huerto);

        for (int color = 0; color < 2; color++)
            for (int ti = Suelo(origenX, Trozo); ti <= Suelo(origenX + ancho - 1, Trozo); ti++)
                for (int tj = Suelo(origenZ, Trozo); tj <= Suelo(origenZ + alto - 1, Trozo); tj++)
                {
                    if (((ti + tj) & 1) != color) continue;
                    int i0 = Math.Max(0, ti * Trozo - origenX), i1 = Math.Min(ancho, (ti + 1) * Trozo - origenX);
                    int j0 = Math.Max(0, tj * Trozo - origenZ), j1 = Math.Min(alto, (tj + 1) * Trozo - origenZ);
                    while (true)
                    {
                        // la casilla con menos opciones; a igualdad, la que más vecinas resueltas tiene; y si no, la que diga su posición
                        int mi = -1, mj = -1;
                        float mejor = float.MaxValue;
                        for (int i = i0; i < i1; i++)
                            for (int j = j0; j < j1; j++)
                            {
                                if (hecha[i, j]) continue;
                                int opciones = 0, vecinas = 0;
                                for (int t = 0; t < Tipos; t++) if ((puede[i, j] >> t & 1) != 0) opciones++;
                                for (int d = 0; d < 4; d++)
                                {
                                    int a = i + (d == 0 ? 1 : d == 1 ? -1 : 0), b = j + (d == 2 ? 1 : d == 3 ? -1 : 0);
                                    if (a >= 0 && b >= 0 && a < ancho && b < alto && sale[a, b] >= Casilla.Huerto) vecinas++;
                                }
                                float orden = opciones * 10 - vecinas + Azar(i + origenX, j + origenZ, semilla);
                                if (orden < mejor) { mejor = orden; mi = i; mj = j; }
                            }
                        if (mi < 0) break;
                        // lo que pesa cada opción: la vecindad, y el apego a lo que ya hay al lado
                        var peso = new float[Tipos];
                        float suma = 0;
                        var deQue = Peso[(int)vecindad[mi, mj]];
                        for (int t = 0; t < Tipos; t++)
                        {
                            if ((puede[mi, mj] >> t & 1) == 0) continue;
                            float p = deQue[t];
                            for (int d = 0; d < 4; d++)
                            {
                                int a = mi + (d == 0 ? 1 : d == 1 ? -1 : 0), b = mj + (d == 2 ? 1 : d == 3 ? -1 : 0);
                                if (a >= 0 && b >= 0 && a < ancho && b < alto && sale[a, b] == (Casilla)(t + (int)Casilla.Huerto)) { p *= Apego[t]; break; }
                            }
                            peso[t] = p;
                            suma += p;
                        }
                        int elegido = 4;                       // el monte nunca está vedado: es lo que queda si nada pesa
                        float tiro = Azar(mi + origenX, mj + origenZ, semilla + 1) * suma;
                        for (int t = 0; t < Tipos && suma > 0; t++)
                        {
                            if (peso[t] <= 0) continue;
                            elegido = t;
                            if ((tiro -= peso[t]) < 0) break;
                        }
                        Fijar(mi, mj, elegido);
                    }
                }
        return sale;
    }
}
