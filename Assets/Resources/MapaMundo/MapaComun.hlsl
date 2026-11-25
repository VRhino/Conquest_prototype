// Lo que comparten los sombreados del mapa de mundo (`MapaColor`, `MapaSuelo`): encima del color ya iluminado, las sombras de las nubes, la
// niebla de guerra y la bruma lejana, calcadas de BronzeAgeClient (`src/mundo3d/niebla.ts` y la `THREE.Fog` de `vista.ts`). Allí todo esto se
// mezcla después de pasar el color a sRGB, así que aquí también: se pasa a sRGB, se mezcla y se vuelve a lineal.
//  * Nubes: ruido de valor de tres octavas a 0,065 por u (manchas de unas 15 u), que oscurece un 20 % desde 0,5 hasta 0,75 y deriva a
//    (0,04, 0,015) por segundo en unidades del ruido.
//  * Niebla de guerra (con `PinturaDelMapa.NieblaDibujada` apagado; encendido, el mapa dibujado de más abajo): la textura de la VISTA
//    (R explorado, G a la vista, una celda por texel) leída con filtro bilineal (el borde se funde en
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
// La altura (m) de lo que mira la cámara: lo que queda por debajo, valle, lleva algo más de bruma.
float _BrumaValle;
// El cielo del tema 8 (cúmulos, sombras redondeadas y la bruma del aire): 1; 0, el de antes (`PinturaDelMapa.CieloNuevo`).
float _CieloNuevo;
// El reloj del mapa (s, `Time.timeSinceLevelLoad`, lo pone MapaDeMundo cada fotograma): el de los pasos de la niebla y el polvo, que
// apuntan sus momentos con ese mismo reloj desde C#.
float _RelojMapa;

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
        // Cúmulos de buen tiempo (tema 8): masa de borde neto con volumen de bloque, la cara al sol clara y la base gris azulada (la masa
        // comparada con la de un poco hacia el sol). Derivan con el viento de las sombras de nube (`PintarMapa`: +x, −z del mundo).
        float2 q = d.xz / (h + 0.12) * 1.6 + float2(t * 0.004, -t * 0.0015);
        float masa = FbmCielo(q);
        float n = smoothstep(0.52, 0.565, masa);
        if (_CieloNuevo < 0.5)
        {
            float2 q0 = d.xz / (h + 0.12) * 1.6 + float2(t * 0.004, t * 0.0015);
            col = lerp(col, float3(0.97, 0.97, 0.95), smoothstep(0.52, 0.85, FbmCielo(q0)) * 0.55 * smoothstep(0.02, 0.25, h));
        }
        else if (n > 0)
        {
            float luz = saturate((masa - FbmCielo(q + normalize(haciaElSol.xz + 1e-4) * 0.09)) * 11 + 0.55);
            float3 nube = lerp(float3(0.55, 0.6, 0.68), float3(1.0, 0.97, 0.92), luz);
            col = lerp(col, nube, n * 0.92 * smoothstep(0.02, 0.25, h));
        }
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

// ---- La niebla de guerra dibujada (tema 8; `PinturaDelMapa.NieblaDibujada`, `_NieblaModo.x` 1) ----
// Lo explorado que no se ve: apagado (saturación al 55 % y luminancia × 0,72 en sRGB), sin velo. Lo nunca explorado: un mapa dibujado en
// tinta parda sobre papiro (el papiro de Turín, los frescos de Akrotiri), sin sol ni sombras. La frontera, de pincel: el umbral de lo
// explorado (bilineal, una celda de 5 u) movido con ruido y un reborde de tinta del lado del papiro. Lo explorado pasa de la textura de antes
// a la de ahora en `_NieblaModo.z` s desde `_NieblaModo.y` (B y A de `_NieblaTex`, lo explorado y lo visto de antes): la frontera avanza y lo
// que había debajo crece. Apagado (`_NieblaModo.x` 0), el velo de antes tal cual.
float4 _NieblaModo;
TEXTURE2D(_DibujoPapiro); SAMPLER(sampler_DibujoPapiro);
TEXTURE2D(_DibujoTrazos); SAMPLER(sampler_DibujoTrazos);
TEXTURE2D(_DibujoAgua); SAMPLER(sampler_DibujoAgua);
// Lo explorado que no se ve: x la saturación que le queda, y su luminancia (`PinturaDelMapa.Apagado`).
float4 _NieblaApagado;
static const float3 TintaDibujo = float3(0.24, 0.165, 0.10);
static const float3 PapelDibujo = float3(0.52, 0.455, 0.35);
static const float3 AguadaDibujo = float3(0.525, 0.635, 0.62); // #86a29e, azul verdosa apagada: más mate que el mar vivo
static const float3 TintaAgua = float3(0.12, 0.19, 0.29); // el zigzag y la línea de costa, en tinta azul oscura

float2 NieblaUv(float3 mundo)
{
    // El mapa va con z = −y: la fila crece hacia el sur, que es −z.
    return float2(mundo.x - _NieblaRejilla.x, _NieblaRejilla.y - mundo.z) / _NieblaRejilla.z / _NieblaTamano.xy;
}

// Lo explorado (0..1, antes del umbral) y lo visto en `mundo`, con el paso de la textura de antes a la de ahora.
float2 NieblaEstado(float4 nb, float2 uv)
{
    float t = saturate((_RelojMapa - _NieblaModo.y) / max(_NieblaModo.z, 1e-3));
    float2 ev = lerp(nb.ba, nb.rg, t);
    if (any(uv < 0) || any(uv > 1)) ev = 0;
    return ev;
}

// El valor que decide la frontera de pincel: lo explorado movido por un ruido de unas 3 u y otro fino.
float NieblaUmbral(float explorado, float2 u)
{
    return explorado + (RuidoNube(u * 0.33) - 0.5) * 0.42 + (RuidoNube(u * 1.7 + 5.3) - 0.5) * 0.14;
}

/// Cuánto ha crecido lo que está en `origen` (0 en lo nunca explorado, 1 en lo explorado): para la vegetación y los nodos, desde el vértice.
float NieblaCrecer(float3 origen)
{
    if (_NieblaModo.x < 0.5 || _NieblaRejilla.w < 0.5) return 1;
    float2 uv = NieblaUv(origen);
    float2 ev = NieblaEstado(SAMPLE_TEXTURE2D_LOD(_NieblaTex, sampler_NieblaTex, uv, 0), uv);
    float2 u = float2(origen.x, -origen.z) / max(_Bruma.w, 1);
    return smoothstep(0.47, 0.6, NieblaUmbral(ev.x, u));
}

/// El papiro (sRGB) en `u` (unidades del mapa): fibra irregular en espacio de mundo y manchas amplias y suaves de envejecido.
float3 Papiro(float2 u)
{
    float f = SAMPLE_TEXTURE2D(_DibujoPapiro, sampler_DibujoPapiro, u / 9.0).r;
    float mancha = Nubes(u * 0.03 + 3.1);
    return PapelDibujo * (0.92 + 0.16 * f) * (0.88 + 0.2 * mancha);
}

// Una marca en planta (atlas 2 × 2 del canal `canal`; `variantes` celdas desde `base`) sembrada por celdas de `lado` u: si pasa el azar
// (`quiere`), desplazada, de tamaño y giro al azar. Sin arriba ni abajo: se lee igual con la cámara girada.
float Marca(float2 u, float lado, int canal, int base, int variantes, float quiere, float semilla)
{
    float2 q = u / lado;
    float2 i = floor(q);
    if (AzarNube(i + semilla) > quiere) return 0;
    float2 f = frac(q) - 0.5;
    float2 desvio = (float2(AzarNube(i + semilla + 17.0), AzarNube(i + semilla + 31.0)) - 0.5) * 0.45;
    float esc = 0.55 + 0.4 * AzarNube(i + semilla + 7.0);
    float s, co;
    sincos(AzarNube(i + semilla + 3.0) * 6.2832, s, co);
    float2 g = mul(float2x2(co, -s, s, co), (f - desvio) / esc);
    if (any(abs(g) > 0.5)) return 0;
    int v = base + min((int)(AzarNube(i + semilla + 5.0) * variantes), variantes - 1);
    float2 celda = float2(v % 2, v / 2);
    float2 uv = float2((g.x + 0.5 + celda.x) * 0.5, 1 - (g.y + 0.5 + celda.y) * 0.5);
    float dd = 0.5 / (lado * esc);
    return SAMPLE_TEXTURE2D_GRAD(_DibujoTrazos, sampler_DibujoTrazos, uv, ddx(u) * dd, ddy(u) * dd)[canal];
}

/// El suelo dibujado (sRGB), todo en planta: papiro; el relieve a pluma (trazos cortos que bajan por la ladera, de una textura de trazos
/// verticales proyectada de lado, triplanar en X y Z a lo largo de la Y del mundo: nada en llano, más y más gruesos cuanto más empinado);
/// copas en planta donde hay bosque; y en el llano, pocas marcas menudas (hierba en lo fértil, punteado en lo seco). `N` la normal; `bosque` y
/// `fertil` 0..1.
float3 DibujoSuelo(float3 mundo, float3 N, float bosque, float fertil)
{
    float mpu = max(_Bruma.w, 1);
    float2 u = float2(mundo.x, -mundo.z) / mpu;
    float3 c = Papiro(u);
    float tinta = 0;
    float fuerza = smoothstep(0.06, 0.45, 1 - N.y);
    if (fuerza > 0.01)
    {
        float3 w3 = pow(abs(N), 4);
        float2 wl = w3.xz / max(w3.x + w3.z, 1e-4);
        float esc = 1.0 / (4.5 * mpu); // la textura repite cada 4,5 u
        float4 tx = SAMPLE_TEXTURE2D(_DibujoTrazos, sampler_DibujoTrazos, float2(mundo.z, mundo.y) * esc);
        float4 tz = SAMPLE_TEXTURE2D(_DibujoTrazos, sampler_DibujoTrazos, float2(mundo.x, mundo.y) * esc + 0.37);
        float r = tx.r * wl.x + tz.r * wl.y, rango = tx.g * wl.x + tz.g * wl.y;
        // con poca pendiente, solo los trazos de rango bajo (pocos); con mucha, todos y más gruesos (la tinta se corta más abajo)
        float grueso = lerp(0.5, 0.18, fuerza);
        tinta = step(rango, fuerza * 0.9) * smoothstep(grueso, grueso + 0.2, r) * lerp(0.55, 0.95, fuerza);
    }
    if (bosque > 0.3)
    {
        float d = smoothstep(0.3, 0.85, bosque);
        tinta = max(tinta, Marca(u, 1.3, 2, 0, 3, d * 0.8, 3.0) * 0.9);
        tinta = max(tinta, Marca(u + float2(0.61, 0.37), 1.1, 2, 0, 3, d * 0.55, 9.0) * 0.85);
    }
    else if (fuerza < 0.3)
        tinta = max(tinta, Marca(u, 2.3, 3, fertil > 0.45 ? 0 : 2, 2, 0.2, 21.0) * 0.6);
    return lerp(c, TintaDibujo, saturate(tinta));
}

/// El agua dibujada (sRGB): aguada azul verdosa apagada con filas de zigzag en desorden y la línea de costa u orilla (`linea` 0..1).
float3 DibujoAgua(float3 mundo, float linea)
{
    float2 u = float2(mundo.x, -mundo.z) / max(_Bruma.w, 1);
    float3 c = lerp(Papiro(u), AguadaDibujo, 0.85);
    float2 q = u / 5.0 + (float2(RuidoNube(u * 0.21), RuidoNube(u * 0.21 + 4.7)) - 0.5) * 0.5;
    c = lerp(c, TintaAgua, SAMPLE_TEXTURE2D(_DibujoAgua, sampler_DibujoAgua, q).r * 0.55);
    return lerp(c, TintaAgua, saturate(linea));
}

/// Lo nunca explorado en `mundo` (1) frente a lo explorado (0), con la frontera de pincel; 0 sin niebla o con el velo de antes.
float NuncaExplorado(float3 mundo)
{
    if (_NieblaModo.x < 0.5 || _NieblaRejilla.w < 0.5) return 0;
    float2 uv = NieblaUv(mundo);
    float2 ev = NieblaEstado(SAMPLE_TEXTURE2D(_NieblaTex, sampler_NieblaTex, uv), uv);
    float2 u = float2(mundo.x, -mundo.z) / max(_Bruma.w, 1);
    return 1 - smoothstep(0.48, 0.52, NieblaUmbral(ev.x, u));
}

/// Como `PintarMapa`, con el dibujo (sRGB) de lo nunca explorado que pone quien llama si `propio` (el suelo y el agua); si no, papiro.
float3 PintarMapaDibujado(float3 colorLineal, float3 mundo, bool conNiebla, float3 dibujo, bool propio)
{
    float3 c = LinearToSRGB(saturate(colorLineal));
    if (conNiebla)
    {
        float metrosPorU = max(_Bruma.w, 1);
        float2 u = float2(mundo.x, -mundo.z) / metrosPorU;
        float t = fmod(_Time.y, 3600.0);
        float2 qn = u * 0.065 + float2(t * 0.04, t * 0.015);
        float nube = _CieloNuevo > 0.5 ? smoothstep(0.585, 0.725, RuidoNube(qn) * 0.78 + RuidoNube(qn * 2.1 + 7.3) * 0.22) // manchas redondeadas
                                       : smoothstep(0.5, 0.75, Nubes(qn));
        c *= 1.0 - 0.2 * nube;
        if (_NieblaRejilla.w > 0.5)
        {
            float2 uv = NieblaUv(mundo);
            float4 nb = SAMPLE_TEXTURE2D(_NieblaTex, sampler_NieblaTex, uv);
            if (_NieblaModo.x < 0.5)
            {
                float alfa = lerp(_NieblaAlfas.x, lerp(_NieblaAlfas.y, _NieblaAlfas.z, nb.g), nb.r);
                if (any(uv < 0) || any(uv > 1)) alfa = _NieblaAlfas.x;
                c = lerp(c, _NieblaColor.rgb, alfa);
            }
            else
            {
                float2 ev = NieblaEstado(nb, uv);
                // lo explorado que no se ve: apagado
                float apagado = saturate(ev.x * 2) * (1 - ev.y);
                float gris = dot(c, float3(0.2126, 0.7152, 0.0722));
                c = lerp(c, lerp(gris.xxx, c, _NieblaApagado.x) * _NieblaApagado.y, apagado);
                // lo nunca explorado: el dibujo, con la frontera de pincel y su reborde de tinta
                float umbral = NieblaUmbral(ev.x, u);
                if (umbral < 0.53)
                {
                    // el papel acaba neto y el reborde va pegado a su borde, sin franja clara: pincel seco, de tinta desigual y con cortes
                    float papel = 1 - smoothstep(0.49, 0.505, umbral);
                    float seco = smoothstep(0.3, 0.42, RuidoNube(u * 7.0 + 2.9)) * (0.6 + 0.4 * RuidoNube(u * 3.1 + 1.1));
                    float reborde = smoothstep(0.455, 0.47, umbral) * (1 - smoothstep(0.49, 0.505, umbral)) * seco;
                    if (!propio) dibujo = Papiro(u);
                    c = lerp(c, lerp(dibujo, TintaDibujo, reborde * 0.85), papel);
                }
            }
        }
    }
    if (_Bruma.z > 0.5)
    {
        float profundidad = -TransformWorldToView(mundo).z;
        // la del aire (tema 8): desde la mitad de `cerca` (más allá de lo que mira la cámara), un toque de azul y algo más en los valles
        float aire = smoothstep(_Bruma.x * 0.5, _Bruma.y, profundidad);
        float valle = saturate((_BrumaValle - mundo.y) / 80.0);
        c = lerp(c, lerp(_BrumaColor.rgb, float3(0.6, 0.7, 0.84), 0.25), aire * (0.1 + 0.08 * valle) * _CieloNuevo);
        c = lerp(c, _BrumaColor.rgb, smoothstep(_Bruma.x, _Bruma.y, profundidad));
    }
    return SRGBToLinear(c);
}

float3 PintarMapa(float3 colorLineal, float3 mundo, bool conNiebla)
{
    return PintarMapaDibujado(colorLineal, mundo, conNiebla, 0, false);
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
