// Lo que comparten los sombreados del mapa de mundo (`MapaColor`, `MapaSuelo`): encima del color ya iluminado, las sombras de las nubes, la
// niebla de guerra y la bruma lejana, calcadas de BronzeAgeClient (`src/mundo3d/niebla.ts` y la `THREE.Fog` de `vista.ts`). Allí todo esto se
// mezcla después de pasar el color a sRGB, así que aquí también: se pasa a sRGB, se mezcla y se vuelve a lineal.
//  * Nubes: ruido de valor de tres octavas a 0,065 por u (manchas de unas 15 u), que oscurece un 20 % desde 0,5 hasta 0,75 y deriva a
//    (0,04, 0,015) por segundo en unidades del ruido.
//  * Niebla de guerra: la textura de la VISTA (R explorado, G a la vista, una celda por texel) leída con filtro bilineal (el borde se funde en
//    una celda, 5 u); alfa = mezcla(nunca visto, mezcla(recordado, a la vista, G), R), sobre el color de la niebla (#0a0e17).
//  * Bruma: niebla lineal de tres (smoothstep entre cerca y lejos, por la profundidad en la vista) hacia el color del horizonte (#b7c6cf).
#ifndef MAPA_COMUN_INCLUIDO
#define MAPA_COMUN_INCLUIDO
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

// La niebla de guerra (la pone NieblaDelMapa): `_NieblaRejilla` x, z del origen en el mundo, metros por celda, y si hay niebla;
// `_NieblaTamano` columnas, filas; `_NieblaColor` en sRGB; `_NieblaAlfas` nunca visto, recordado, a la vista.
TEXTURE2D(_NieblaTex); SAMPLER(sampler_NieblaTex);
float4 _NieblaRejilla;
float4 _NieblaTamano;
float4 _NieblaColor;
float4 _NieblaAlfas;
// La bruma (la pone MapaDeMundo cada fotograma): cerca y lejos (m), si va, y metros por u; `_BrumaColor` en sRGB.
float4 _Bruma;
float4 _BrumaColor;

float AzarNube(float2 p)
{
    p = p - 289.0 * floor(p / 289.0);
    return frac(sin(dot(p, float2(269.5, 183.3))) * 43758.5453);
}

float RuidoNube(float2 p)
{
    float2 i = floor(p), f = frac(p), u = f * f * (3.0 - 2.0 * f);
    return lerp(lerp(AzarNube(i), AzarNube(i + float2(1, 0)), u.x), lerp(AzarNube(i + float2(0, 1)), AzarNube(i + float2(1, 1)), u.x), u.y);
}

float Nubes(float2 p) { return RuidoNube(p) * 0.55 + RuidoNube(p * 2.1 + 7.3) * 0.3 + RuidoNube(p * 4.3 + 1.7) * 0.15; }

// El cielo (calcado de BronzeAgeClient, `cielo.ts`), para el fondo (`CieloDelMapa`) y para el reflejo del agua (`MapaAgua`). Los colores, en
// lineal; los pone MapaDeMundo como globales (`_CieloCenit`, `_CieloHorizonte`, `_CieloSuelo`, `_CieloSol`, hacia el sol).
float4 _CieloCenit, _CieloHorizonte, _CieloSuelo, _CieloSol;

float AzarCielo(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
float RuidoCielo(float2 p)
{
    float2 i = floor(p), f = frac(p), u = f * f * (3.0 - 2.0 * f);
    return lerp(lerp(AzarCielo(i), AzarCielo(i + float2(1, 0)), u.x), lerp(AzarCielo(i + float2(0, 1)), AzarCielo(i + float2(1, 1)), u.x), u.y);
}
float FbmCielo(float2 p)
{
    float s = 0, a = 0.5;
    [unroll] for (int k = 0; k < 5; k++) { s += a * RuidoCielo(p); p *= 2.03; a *= 0.5; }
    return s;
}

/// El color (lineal) del cielo en la dirección `d`: degradado del horizonte al cénit, nubes de ruido proyectadas y el sol con su halo.
float3 ColorDelCielo(float3 d, float3 cenit, float3 horizonte, float3 suelo, float3 haciaElSol)
{
    d = normalize(d);
    float h = d.y;
    float3 col = h > 0 ? lerp(horizonte, cenit, 1 - exp(-h * 6)) : lerp(horizonte, suelo, saturate(-h * 4));
    if (h > 0.02)
    {
        float t = fmod(_Time.y, 100000.0);
        float2 q = d.xz / (h + 0.12) * 1.6 + float2(t * 0.004, t * 0.0015);
        float n = smoothstep(0.52, 0.85, FbmCielo(q));
        col = lerp(col, float3(0.97, 0.97, 0.95), n * 0.55 * smoothstep(0.02, 0.25, h));
    }
    float sol = max(dot(d, normalize(haciaElSol)), 0);
    col += float3(1.0, 0.86, 0.62) * (pow(sol, 600) * 1.2 + pow(sol, 24) * 0.22);
    return col;
}

/// El color lineal ya iluminado de un punto del mapa (`mundo`, en metros) con las nubes y la niebla de guerra (si `conNiebla`) y la bruma.
float3 PintarMapa(float3 colorLineal, float3 mundo, bool conNiebla)
{
    float3 c = LinearToSRGB(saturate(colorLineal));
    if (conNiebla)
    {
        float metrosPorU = max(_Bruma.w, 1);
        float2 u = float2(mundo.x, -mundo.z) / metrosPorU;
        float t = fmod(_Time.y, 3600.0);
        c *= 1.0 - 0.2 * smoothstep(0.5, 0.75, Nubes(u * 0.065 + float2(t * 0.04, t * 0.015)));
        if (_NieblaRejilla.w > 0.5)
        {
            // El mapa va con z = −y: la fila crece hacia el sur, que es −z.
            float2 uv = float2(mundo.x - _NieblaRejilla.x, _NieblaRejilla.y - mundo.z) / _NieblaRejilla.z / _NieblaTamano.xy;
            float2 nb = SAMPLE_TEXTURE2D(_NieblaTex, sampler_NieblaTex, uv).rg;
            float alfa = lerp(_NieblaAlfas.x, lerp(_NieblaAlfas.y, _NieblaAlfas.z, nb.g), nb.r);
            if (any(uv < 0) || any(uv > 1)) alfa = _NieblaAlfas.x;
            c = lerp(c, _NieblaColor.rgb, alfa);
        }
    }
    if (_Bruma.z > 0.5)
    {
        float profundidad = -TransformWorldToView(mundo).z;
        c = lerp(c, _BrumaColor.rgb, smoothstep(_Bruma.x, _Bruma.y, profundidad));
    }
    return SRGBToLinear(c);
}

#endif
