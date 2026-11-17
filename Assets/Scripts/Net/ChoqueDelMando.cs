using Unity.Collections;
using Unity.Mathematics;

namespace ConquestTactics.Net
{
    /// <summary>
    /// El choque del héroe de un jugador con los cuerpos enemigos (soldados y héroes) dentro de la predicción (la predicción «c»): la misma
    /// cuenta en el cliente, con los cuerpos como los ve (interpolados), y en el servidor, con los de ahora. Como el contacto entre enemigos del
    /// juego (<c>UnitBodyblockSystem</c> con contacto único): dos cuerpos no se acercan a menos de la suma de sus radios; el paso se aparta lo justo
    /// para deshacer el solape. Así el servidor no tiene que corregir al meterse en una escuadra.
    /// ponytail: todos los cuerpos con el radio de cuerpo común (la caballería, con el suyo, solapa un poco) y sin rejilla: se recorren todos
    /// los cuerpos enemigos (unos 400 en la batalla más grande).
    /// </summary>
    public static class ChoqueDelMando
    {
        /// <summary>Pasadas para deshacer los solapes con varios cuerpos a la vez (uno empuja contra otro).</summary>
        const int Pasadas = 3;

        /// <summary>
        /// El punto <paramref name="destino"/> apartado de los <paramref name="cuerpos"/> hasta que ninguno quede a menos de
        /// <paramref name="distancia"/> en el plano. Un cuerpo justo encima (sin dirección) aparta hacia atrás del paso, hacia <paramref name="desde"/>.
        /// Si no se puede (no cabe entre ellos), vale <paramref name="desde"/>.
        /// </summary>
        public static float3 Apartar(float3 desde, float3 destino, NativeArray<float3> cuerpos, float distancia)
        {
            if (distancia <= 0f || cuerpos.Length == 0) return destino;
            float d2 = distancia * distancia;
            for (int pasada = 0; pasada < Pasadas; pasada++)
            {
                bool solape = false;
                for (int i = 0; i < cuerpos.Length; i++)
                {
                    float2 dif = destino.xz - cuerpos[i].xz;
                    float q = math.lengthsq(dif);
                    if (q >= d2) continue;
                    solape = true;
                    float2 dir = q > 1e-8f ? dif / math.sqrt(q) : math.normalizesafe(desde.xz - cuerpos[i].xz, new float2(0f, -1f));
                    float2 xz = cuerpos[i].xz + dir * distancia;
                    destino = new float3(xz.x, destino.y, xz.y);
                }
                if (!solape) return destino;
            }
            // Entre dos cuerpos más juntos que el hueco que hace falta (dos soldados de una línea): no cabe y no pasa. Se queda donde estaba,
            // salvo que el paso solape menos que quedarse (está saliendo de un solape).
            return Solape(desde, cuerpos, distancia) <= Solape(destino, cuerpos, distancia) ? desde : destino;
        }

        /// <summary>Cuánto se mete <paramref name="punto"/> en los cuerpos: la suma de lo que le falta a cada uno para la distancia de contacto.</summary>
        static float Solape(float3 punto, NativeArray<float3> cuerpos, float distancia)
        {
            float total = 0f;
            for (int i = 0; i < cuerpos.Length; i++)
                total += math.max(0f, distancia - math.distance(punto.xz, cuerpos[i].xz));
            return total;
        }
    }
}
