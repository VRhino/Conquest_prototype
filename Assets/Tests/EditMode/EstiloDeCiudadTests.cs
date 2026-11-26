using NUnit.Framework;

/// <summary>El estilo y la época de cada edificio (<see cref="EstiloDeCiudad"/>), como en el prototipo.</summary>
public class EstiloDeCiudadTests
{
    [Test]
    public void ElHashEsElDelPrototipo()
    {
        // (los mismos valores que da `hashTexto` en `ciudad3d/estado.ts`)
        Assert.AreEqual(0.23213645676150918, EstiloDeCiudad.HashTexto("ed-1", 7), 1e-12);
        Assert.AreEqual(0.9881110768765211, EstiloDeCiudad.HashTexto("ed-42", 23), 1e-12);
    }

    [Test]
    public void SinAscensoVaConElNivelYLasViejasUnoAtras()
    {
        int atras = 0, total = 400;
        for (int i = 0; i < total; i++)
        {
            int e = EstiloDeCiudad.EstiloDe("ed-" + i, "vivienda", 3);
            Assert.That(e, Is.EqualTo(3).Or.EqualTo(2));
            if (e == 2) atras++;
        }
        Assert.That(atras, Is.InRange(total * 0.1, total * 0.26), "cerca de un 18 % se queda un nivel atrás");
        Assert.AreEqual(1, EstiloDeCiudad.EstiloDe("ed-7", "vivienda", 1), "en el nivel 1 nadie baja de 1");
        Assert.AreEqual(3, EstiloDeCiudad.EstiloDe("ed-7", "palacio", 3), "los monumentales van con el nivel");
    }

    [Test]
    public void EnUnAscensoCambiaCasaACasaYLoMonumentalAlFinal()
    {
        int Nuevas(double ahora) { int n = 0; for (int i = 0; i < 200; i++) if (EstiloDeCiudad.EstiloDe("ed-" + i, "vivienda", 2, 3, 0, 1000, ahora) == 3) n++; return n; }
        Assert.AreEqual(0, Nuevas(0));
        Assert.That(Nuevas(500), Is.InRange(70, 130));
        Assert.AreEqual(200, Nuevas(1000));
        Assert.AreEqual(2, EstiloDeCiudad.EstiloDe("ed-1", "centroUrbano", 2, 3, 0, 1000, 999));
        Assert.AreEqual(3, EstiloDeCiudad.EstiloDe("ed-1", "centroUrbano", 2, 3, 0, 1000, 1000));
        Assert.AreEqual(2, EstiloDeCiudad.EstiloDe("ed-9", "puestoMercado", 2), "de nivel 2 en adelante, el puesto es tienda");
    }

    [Test]
    public void LaEpocaLlegaPorGradosYPrimeroALoPublico()
    {
        int Nuevas(int grado) { int n = 0; for (int i = 0; i < 200; i++) if (EstiloDeCiudad.EpocaDe("ed-" + i, "vivienda", 3, grado) == 3) n++; return n; }
        Assert.That(Nuevas(1), Is.InRange(20, 60));
        Assert.That(Nuevas(2), Is.InRange(95, 145));
        Assert.AreEqual(200, Nuevas(3));
        Assert.AreEqual(3, EstiloDeCiudad.EpocaDe("ed-5", "salaConsejo", 3, 1), "lo público, lo primero");
        Assert.AreEqual(1, EstiloDeCiudad.EpocaDe("ed-5", "vivienda", 1, 3), "la Era I va siempre entera");
    }
}
