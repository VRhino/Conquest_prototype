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
// HSV (todo de 0 a 1; el tono, una vuelta) y su vuelta.
float3 RgbAHsv(float3 c)
{
    float4 K = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
    float4 p = lerp(float4(c.bg, K.wz), float4(c.gb, K.xy), step(c.b, c.g));
    float4 q = lerp(float4(p.xyw, c.r), float4(c.r, p.yzx), step(p.x, c.r));
    float d = q.x - min(q.w, q.y);
    return float3(abs(q.z + (q.w - q.y) / (6.0 * d + 1e-6)), d / (q.x + 1e-6), q.x);
}
float3 HsvARgb(float3 c)
{
    float3 p = abs(frac(c.xxx + float3(1.0, 2.0 / 3.0, 1.0 / 3.0)) * 6.0 - 3.0);
    return c.z * lerp(float3(1, 1, 1), saturate(p - 1.0), c.y);
}

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


// ---- La luz del mapa con sombras reales (`PinturaDelMapa.SombrasReales`; la global `_SombrasReales` la pone MapaDeMundo) ----
// Apagadas, exactamente la luz de antes (ambiente + directa, gris). Encendidas, la del prototipo (`vista.ts`): el cielo como hemisferio
// (#dfe9f0 arriba y #6b5a40 abajo, por N.y) en el ambiente y el sol #fff0d8 en la directa, normalizados a luminancia 1 para que a pleno sol
// quede la misma luminancia que antes. A la sombra queda `queda` (lineal) de la luz al sol, con un tinte de cielo frío y azulado, no negro:
// en el suelo 0,36 (unos 0,62 en pantalla, sRGB) y en las copas 0,45 (unos 0,70), para no matar la luz horneada en su color de vértice.
float _SombrasReales;
// Hasta dónde (m desde la cámara) llega el mapa de sombras: más allá, la mancha bajo los árboles (MapaSombraArbol) sigue sola.
float _SombraHasta;

#ifdef UNIVERSAL_REALTIME_LIGHTS_INCLUDED
// La sombra del sol en `mundo` (1 al sol, 0 a la sombra): el mapa de sombras de URP con sus cascadas y el desvanecido al final de la distancia.
float SombraDelSol(float3 mundo)
{
#if defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE)
    if (_SombrasReales < 0.5) return 1;
    return GetMainLight(TransformWorldToShadowCoord(mundo), mundo, half4(1, 1, 1, 1)).shadowAttenuation;
#else
    return 1;
#endif
}
#endif

// (`_CompensaSol`: el seno de la elevación del sol del prototipo entre el del sol de ahora; el llano a pleno sol queda igual que siempre)
float _CompensaSol;

float3 LuzDelMapa(float ny, float ambiente, float directa, float sombra, float queda)
{
    directa *= _CompensaSol > 0 ? _CompensaSol : 1;
    float total = ambiente + directa;
    if (_SombrasReales < 0.5) return total.xxx;
    float3 cielo = lerp(float3(1.362, 0.945, 0.475), float3(0.918, 1.016, 1.085), saturate(ny * 0.5 + 0.5));
    float3 alSol = ambiente * cielo + directa * float3(1.13, 0.984, 0.775);
    float3 aLaSombra = queda * total * float3(0.8, 1.0, 1.32); // (luminancia 1: el cielo frío que llena la sombra)
    return lerp(aLaSombra, alSol, sombra);
}

// La sombra del relieve horneada en la tesela (`TerrenoDelMapa`, `SombraDelRelieve`): llega como lo que tapa (0 al sol), así una malla sin el
// dato queda al sol.
float ConRelieve(float sombra, float tapaElRelieve) { return min(sombra, 1 - tapaElRelieve * step(0.5, _SombrasReales)); }

#endif

// ---- El suelo bajo una planta (para la vegetación): lo mismo que mira `MapaSuelo` para pintar seco o verde ----
// Globales que pone `TerrenoDelMapa`: los datos del motor (R fertilidad, G humedad, B bosque), su tamaño en metros (w: metros por unidad),
// el ruido del suelo, los umbrales de altura y el verdor. Sin datos, todo 0: hierba seca, sin hondura.
TEXTURE2D(_VegDatos); SAMPLER(sampler_VegDatos);
TEXTURE2D(_VegRuido); SAMPLER(sampler_VegRuido);
float4 _VegDatosTamano, _VegUmbrales, _VegVerdor;

// (verde, fertilidad, bosque, arena) en `origen` (mundo, metros), en el sombreador de vértices.
float4 SueloBajo(float3 origen)
{
    if (_VegDatosTamano.x <= 0) return 0;
    float4 r1 = SAMPLE_TEXTURE2D_LOD(_VegRuido, sampler_VegRuido, origen.xz / 900.0, 0);
    float4 r2 = SAMPLE_TEXTURE2D_LOD(_VegRuido, sampler_VegRuido, origen.xz / 170.0 + 0.37, 0);
    float2 uv = float2(origen.x, -origen.z) / _VegDatosTamano.xy + (r2.xy - 0.5) * 3 * _VegDatosTamano.z / _VegDatosTamano.xy;
    float3 d = SAMPLE_TEXTURE2D_LOD(_VegDatos, sampler_VegDatos, uv, 0).rgb;
    float humedo = 0.5 * d.y + 0.5 * d.x + (r2.w - 0.5) * 0.3 + (r1.x - 0.5) * 0.15;
    float h = origen.y + (r2.z - 0.5) * 24 + (r1.w - 0.5) * 16;
    return float4(smoothstep(_VegVerdor.x, _VegVerdor.y, humedo), d.x, d.z, 1 - smoothstep(_VegUmbrales.x - 12, _VegUmbrales.x + 8, h));
}
