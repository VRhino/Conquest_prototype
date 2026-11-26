using NUnit.Framework;

/// <summary>El relleno de las casillas libres (<see cref="RellenoDeCiudad"/>): sale siempre igual, respeta las vedas y es local.</summary>
public class RellenoDeCiudadTests
{
    const int Lado = 36;

    static Vecindad[,] Plano(int ocupadaX = -1, int ocupadaZ = -1)
    {
        var v = new Vecindad[Lado, Lado];
        for (int i = 0; i < Lado; i++)
            for (int j = 0; j < Lado; j++)
                v[i, j] = (Vecindad)((i / 5 + j / 7) % 5);
        for (int i = 10; i < 14; i++) for (int j = 10; j < 14; j++) v[i, j] = Vecindad.Ocupada;
        if (ocupadaX >= 0) v[ocupadaX, ocupadaZ] = Vecindad.Ocupada;
        return v;
    }

    [Test]
    public void ElMismoPlanoDaElMismoRelleno()
    {
        CollectionAssert.AreEqual(RellenoDeCiudad.Generar(Plano(), -7, 3, 5), RellenoDeCiudad.Generar(Plano(), -7, 3, 5));
    }

    [Test]
    public void NiArbolesNiLenaJuntoAlHuerto()
    {
        var r = RellenoDeCiudad.Generar(Plano(), -7, 3, 5);
        int huertos = 0;
        for (int i = 0; i < Lado; i++)
            for (int j = 0; j < Lado; j++)
            {
                Assert.AreEqual(Plano()[i, j] == Vecindad.Ocupada, r[i, j] == Casilla.Ocupada);
                if (r[i, j] != Casilla.Huerto) continue;
                huertos++;
                foreach (var (a, b) in new[] { (i + 1, j), (i - 1, j), (i, j + 1), (i, j - 1) })
                    if (a >= 0 && b >= 0 && a < Lado && b < Lado)
                        Assert.That(r[a, b], Is.Not.EqualTo(Casilla.Arboleda).And.Not.EqualTo(Casilla.Labor), $"junto al huerto de ({i}, {j})");
            }
        Assert.Greater(huertos, 20);
    }

    [Test]
    public void UnEdificioNuevoSoloCambiaSuTrozoYLosDeAlLado()
    {
        var antes = RellenoDeCiudad.Generar(Plano(), 0, 0, 5);
        var despues = RellenoDeCiudad.Generar(Plano(27, 27), 0, 0, 5);
        int T = RellenoDeCiudad.Trozo;
        for (int i = 0; i < Lado; i++)
            for (int j = 0; j < Lado; j++)
                if (System.Math.Abs(i / T - 27 / T) > 1 || System.Math.Abs(j / T - 27 / T) > 1)
                    Assert.AreEqual(antes[i, j], despues[i, j], $"({i}, {j})");
    }
}
