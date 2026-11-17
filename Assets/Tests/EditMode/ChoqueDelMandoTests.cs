// La predicción «c»: el paso del héroe de un jugador se aparta de los cuerpos enemigos, la misma cuenta en el cliente y en el servidor.
using ConquestTactics.Net;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

public class ChoqueDelMandoTests
{
    [Test]
    public void ElPasoSeQuedaAlBordeDelCuerpoYResbalaPorEl()
    {
        using var cuerpos = new NativeArray<float3>(new[] { new float3(0f, 0f, 1f) }, Allocator.Temp);
        // De frente: se queda a la distancia de contacto, sin atravesarlo.
        var p = ChoqueDelMando.Apartar(float3.zero, new float3(0f, 0f, 0.6f), cuerpos, 0.8f);
        Assert.AreEqual(0.8f, math.distance(p.xz, cuerpos[0].xz), 1e-4f);
        Assert.Less(p.z, 0.6f);
        // De lado: resbala por su borde y sigue avanzando de lado.
        var q = ChoqueDelMando.Apartar(new float3(-0.5f, 0f, 0.5f), new float3(-0.3f, 0f, 0.6f), cuerpos, 0.8f);
        Assert.GreaterOrEqual(math.distance(q.xz, cuerpos[0].xz), 0.8f - 1e-4f);
        Assert.Less(q.x, -0.3f + 1e-4f);
        // Lejos, nada cambia; sin choque (distancia 0), tampoco.
        Assert.AreEqual(new float3(5f, 0f, 5f), ChoqueDelMando.Apartar(float3.zero, new float3(5f, 0f, 5f), cuerpos, 0.8f));
        Assert.AreEqual(new float3(0f, 0f, 0.9f), ChoqueDelMando.Apartar(float3.zero, new float3(0f, 0f, 0.9f), cuerpos, 0f));
    }

    [Test]
    public void EntreDosCuerposNoSeMeteEnNinguno()
    {
        using var cuerpos = new NativeArray<float3>(new[] { new float3(-0.5f, 0f, 1f), new float3(0.5f, 0f, 1f) }, Allocator.Temp);
        var p = ChoqueDelMando.Apartar(float3.zero, new float3(0f, 0f, 1f), cuerpos, 0.8f);
        foreach (var c in cuerpos) Assert.GreaterOrEqual(math.distance(p.xz, c.xz), 0.8f - 1e-3f);
        // Justo encima de un cuerpo: lo aparta hacia donde venía.
        using var uno = new NativeArray<float3>(new[] { new float3(0f, 0f, 1f) }, Allocator.Temp);
        var r = ChoqueDelMando.Apartar(float3.zero, new float3(0f, 0f, 1f), uno, 0.8f);
        Assert.AreEqual(0.2f, r.z, 1e-4f);
    }
}
