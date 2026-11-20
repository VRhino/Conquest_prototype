// El suelo del mapa de mundo (URP), pintado con las ocho capas de `BronzeAgeArte/mapa/suelo` (Texture2DArray: 0 pasto seco, 1 pasto verde,
// 2 tierra, 3 arena, 4 grava, 5 roca, 6 nieve, 7 sotobosque). Qué capa va dónde lo deciden los datos del motor: la altura (umbrales del generador),
// la pendiente, y la fertilidad, la humedad y el bosque del artefacto (`_SueloDatos`, una celda por texel, leídos con filtro bilineal y con el
// borde roto por ruido para que no se vean los escalones de 48 m). Entre capas, mezcla por altura de textura (el borde sigue las matas y las
// piedras). UV en espacio de mundo a dos escalas, detalle cerca y otra más grande lejos, con variación grande por ruido; la roca, triplanar.
// Luz como `MapaColor` (sol más un poco de cielo) y, encima, las nubes, la niebla de guerra y la bruma de `MapaComun.hlsl`.
// Lo que no lleva textura (el agua y la capa de fertilidad) llega con el alfa del color de vértice a 0 y se pinta con ese color, como antes.
Shader "Conquest/MapaSuelo"
{
    Properties
    {
        _SueloColor ("Color de las capas", 2DArray) = "" {}
        _SueloNormal ("Normal de las capas", 2DArray) = "" {}
        _SueloMascara ("Máscara de las capas (R altura, G suavidad)", 2DArray) = "" {}
        _SueloRuido ("Ruido suave (4 canales)", 2D) = "gray" {}
        _SueloDatos ("Datos del motor (R fertilidad, G humedad, B bosque)", 2D) = "black" {}
        _DatosTamano ("Datos: ancho y alto del mapa (m), lado de la celda (m)", Vector) = (64008, 48000, 48, 0)
        _Umbrales ("Altura (m): costa, colina, montaña, cima", Vector) = (420, 545, 600, 685)
        _Nieve ("Nieve: altura (m), ancho del borde (m), pendiente máxima (1 − normal.y)", Vector) = (850, 70, 0.4, 0)
        _Roca ("Roca por pendiente (1 − normal.y): desde, hasta", Vector) = (0.14, 0.4, 0, 0)
        _Verdor ("Verde por humedad y fertilidad: desde, hasta; tierra por debajo de", Vector) = (0.28, 0.55, 0.12, 0)
        _Escalas ("Metros por repetición: cerca, lejos; distancia del cambio (m): desde, hasta", Vector) = (48, 200, 220, 900)
        _FuerzaNormal ("Fuerza de la normal", Float) = 0.8
        _Mezcla ("Profundidad de la mezcla por altura", Float) = 0.3
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            // Sin descartar caras: los faldones de las teselas (que tapan las grietas entre teselas de distinto paso) se ven por cualquier lado.
            Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            #define CAPAS 8
            #define PASTO_SECO 0
            #define PASTO_VERDE 1
            #define TIERRA 2
            #define ARENA 3
            #define GRAVA 4
            #define ROCA 5
            #define NIEVE 6
            #define SOTOBOSQUE 7

            // Las nubes, la niebla de guerra y la bruma, las mismas que en MapaColor.
            #include "MapaComun.hlsl"

            TEXTURE2D_ARRAY(_SueloColor); SAMPLER(sampler_SueloColor);
            TEXTURE2D_ARRAY(_SueloNormal);
            TEXTURE2D_ARRAY(_SueloMascara);
            TEXTURE2D(_SueloRuido); SAMPLER(sampler_SueloRuido);
            TEXTURE2D(_SueloDatos); SAMPLER(sampler_SueloDatos);

            CBUFFER_START(UnityPerMaterial)
                float4 _DatosTamano;
                float4 _Umbrales;
                float4 _Nieve;
                float4 _Roca;
                float4 _Verdor;
                float4 _Escalas;
                float _FuerzaNormal;
                float _Mezcla;
            CBUFFER_END

            struct Entrada { float4 pos : POSITION; float3 normal : NORMAL; float4 color : COLOR; float3 ancha : TEXCOORD0; };
            // `ancha`: la normal del relieve suavizado a unos 24 m (la calcula TerrenoDelMapa); decide la roca de pendiente y la nieve.
            struct Salida { float4 pos : SV_POSITION; float3 normal : TEXCOORD0; float3 mundo : TEXCOORD1; float4 color : COLOR; float3 ancha : TEXCOORD2; };

            Salida Vert(Entrada e)
            {
                Salida s;
                s.mundo = TransformObjectToWorld(e.pos.xyz);
                s.pos = TransformWorldToHClip(s.mundo);
                s.normal = TransformObjectToWorldNormal(e.normal);
                s.color = e.color;
                s.ancha = TransformObjectToWorldNormal(e.ancha);
                return s;
            }

            // Pesos del triplanar por la normal geométrica. En lo casi llano solo cuenta la proyección de arriba (y se ahorran dos muestras).
            float3 PesosTriplanar(float3 N)
            {
                float3 w = pow(abs(N), 8);
                w /= w.x + w.y + w.z;
                if (w.y > 0.98) w = float3(0, 1, 0);
                return w;
            }

            // Color y perturbación de la normal (en mundo) de una capa a una escala, triplanar en las laderas para todas las capas (antes solo
            // la roca: la nieve, la grava y el pasto, proyectados solo desde arriba, se estiraban en rayas por la pendiente). Con gradientes
            // explícitos: se llama dentro de ramas.
            void Muestra(int capa, float3 p, float3 dpx, float3 dpy, float3 N, float escala, out float3 color, out float3 desvio)
            {
                // La roca, a 2/3 de la escala: en las laderas se veía gigante.
                float k = (capa == ROCA ? 1.5 : 1.0) / escala;
                float3 w = PesosTriplanar(N);
                color = 0; desvio = 0;
                float2 uvs[3] = { p.zy * k, p.xz * k, p.xy * k };
                float2 dxs[3] = { dpx.zy * k, dpx.xz * k, dpx.xy * k };
                float2 dys[3] = { dpy.zy * k, dpy.xz * k, dpy.xy * k };
                [unroll] for (int i = 0; i < 3; i++)
                {
                    [branch] if (w[i] > 0)
                    {
                        float3 c = SAMPLE_TEXTURE2D_ARRAY_GRAD(_SueloColor, sampler_SueloColor, uvs[i], capa, dxs[i], dys[i]).rgb;
                        float2 n = SAMPLE_TEXTURE2D_ARRAY_GRAD(_SueloNormal, sampler_SueloColor, uvs[i], capa, dxs[i], dys[i]).xy * 2 - 1;
                        color += c * w[i];
                        float3 d = i == 0 ? float3(0, n.y, n.x) : i == 1 ? float3(n.x, 0, n.y) : float3(n.x, n.y, 0);
                        desvio += d * w[i];
                    }
                }
            }

            // La altura de la máscara de una capa para la mezcla por altura, con la misma proyección que su color (si no, el borde entre capas
            // salía en rayas en las laderas: la causa del estirado de la ronda 4).
            float AltoCapa(int capa, float3 p, float3 dpx, float3 dpy, float3 N, float escala)
            {
                float k = (capa == ROCA ? 1.5 : 1.0) / escala;
                float3 w = PesosTriplanar(N);
                float2 uvs[3] = { p.zy * k, p.xz * k, p.xy * k };
                float2 dxs[3] = { dpx.zy * k, dpx.xz * k, dpx.xy * k };
                float2 dys[3] = { dpy.zy * k, dpy.xz * k, dpy.xy * k };
                float a = 0;
                [unroll] for (int i = 0; i < 3; i++)
                    [branch] if (w[i] > 0) a += SAMPLE_TEXTURE2D_ARRAY_GRAD(_SueloMascara, sampler_SueloColor, uvs[i], capa, dxs[i], dys[i]).r * w[i];
                return a;
            }

            // Giro alrededor de la vertical (para la segunda escala: que no coincidan los mismos bloques).
            float3 GiroY(float3 v, float c, float s) { return float3(c * v.x - s * v.z, v.y, s * v.x + c * v.z); }

            half4 Frag(Salida s, bool delante : SV_IsFrontFace) : SV_Target
            {
                float3 p = s.mundo;
                float3 N = normalize(s.normal);
                if (!delante) N = -N;
                float3 c;
                float3 Nluz = N;
                float pesoNieve = 0;
                if (s.color.a < 0.5)
                {
                    // Sin textura: el agua y la capa de fertilidad, con su color de vértice.
                    c = s.color.rgb;
                }
                else
                {
                    float3 dpx = ddx(p), dpy = ddy(p);
                    float distancia = distance(GetCameraPositionWS(), p);
                    // El ruido suave a dos escalas (unos 900 y 170 m por repetición).
                    float4 r1 = SAMPLE_TEXTURE2D(_SueloRuido, sampler_SueloRuido, p.xz / 900.0);
                    float4 r2 = SAMPLE_TEXTURE2D(_SueloRuido, sampler_SueloRuido, p.xz / 170.0 + 0.37);
                    // Y uno fino (unos 25 m), para que los bordes de la roca y de la nieve sean irregulares de verdad.
                    float4 r3 = SAMPLE_TEXTURE2D(_SueloRuido, sampler_SueloRuido, p.xz / 25.0 + 0.71);
                    float pendienteAncha = 1 - normalize(s.ancha).y;
                    // Los datos del motor, con el borde roto: la posición se mueve hasta celda y media con el ruido.
                    float2 uvDatos = float2(p.x, -p.z) / _DatosTamano.xy + (r2.xy - 0.5) * 3 * _DatosTamano.z / _DatosTamano.xy;
                    float3 datos = SAMPLE_TEXTURE2D(_SueloDatos, sampler_SueloDatos, uvDatos).rgb;
                    float pendiente = 1 - N.y;
                    float h = p.y + (r2.z - 0.5) * 24 + (r1.w - 0.5) * 16;

                    // Los pesos de partida, por capas de pintura: el llano (seco, verde o tierra), el bosque, la grava, la arena, la roca, la nieve.
                    float humedo = 0.5 * datos.y + 0.5 * datos.x + (r2.w - 0.5) * 0.3 + (r1.x - 0.5) * 0.15;
                    float verde = smoothstep(_Verdor.x, _Verdor.y, humedo);
                    // La tierra desnuda, a manchas dentro de lo más seco (no todo el secano es tierra).
                    float tierra = smoothstep(_Verdor.z + 0.15, _Verdor.z - 0.08, humedo) * smoothstep(0.42, 0.72, r2.z * 0.5 + r1.w * 0.5) * 0.8;
                    float w[CAPAS];
                    w[PASTO_SECO] = (1 - verde) * (1 - tierra);
                    w[PASTO_VERDE] = verde * (1 - tierra);
                    w[TIERRA] = tierra;
                    w[ARENA] = 0; w[GRAVA] = 0; w[ROCA] = 0; w[NIEVE] = 0; w[SOTOBOSQUE] = 0;
                    // El sotobosque entra poco a poco con la densidad y nunca tapa del todo el pasto.
                    float bosque = smoothstep(0.08, 0.85, datos.z + (r2.y - 0.5) * 0.3) * 0.8;
                    float grava = smoothstep(_Umbrales.y + 25, _Umbrales.z, h) * (1 - smoothstep(_Umbrales.z + 30, _Umbrales.w, h))
                        * smoothstep(0.35, 0.7, r2.x * 0.7 + r1.z * 0.3);
                    float arena = 1 - smoothstep(_Umbrales.x - 12, _Umbrales.x + 8, h);
                    // La roca de pendiente mira la pendiente ancha: una arista estrecha no llega a roca. Donde la pendiente de la malla es fuerte pero
                    // la ancha no, grava (que no queden cintas de roca sobre el pasto); y al pie de la roca, también.
                    float rocaPendiente = smoothstep(_Roca.x, _Roca.y, pendienteAncha + (r2.y - 0.5) * 0.14 + (r3.x - 0.5) * 0.12);
                    float aristaEstrecha = smoothstep(_Roca.x, _Roca.y, pendiente + (r3.y - 0.5) * 0.1) * (1 - rocaPendiente);
                    grava = max(grava, saturate(rocaPendiente * (1 - rocaPendiente) * 3 + aristaEstrecha * 0.6));
                    float roca = max(smoothstep(_Umbrales.z + 20, _Umbrales.w + 10, h), rocaPendiente);
                    float nieve = smoothstep(_Nieve.x - _Nieve.y * 0.5, _Nieve.x + _Nieve.y * 0.5, h + (r2.x - 0.5) * _Nieve.y + (r1.y - 0.5) * _Nieve.y * 1.5 + (r3.z - 0.5) * _Nieve.y * 0.8)
                        * (1 - smoothstep(_Nieve.z - 0.15, _Nieve.z + 0.15, pendienteAncha + (r3.w - 0.5) * 0.2));
                    float encima[5] = { bosque, grava, arena, roca, nieve };
                    int capaDe[5] = { SOTOBOSQUE, GRAVA, ARENA, ROCA, NIEVE };
                    [unroll] for (int e = 0; e < 5; e++)
                    {
                        [unroll] for (int i = 0; i < CAPAS; i++) w[i] *= 1 - encima[e];
                        w[capaDe[e]] += encima[e];
                    }

                    // Mezcla por altura: cada capa sube con la altura de su textura; solo quedan las que asoman por encima de la más alta.
                    float a[CAPAS];
                    float tope = 0;
                    [unroll] for (int i = 0; i < CAPAS; i++)
                    {
                        a[i] = -10;
                        [branch] if (w[i] > 0.002)
                        {
                            float alto = AltoCapa(i, p, dpx, dpy, N, _Escalas.x);
                            a[i] = w[i] + alto * 0.5;
                            tope = max(tope, a[i]);
                        }
                    }
                    float suma = 0;
                    [unroll] for (int i = 0; i < CAPAS; i++) { w[i] = max(a[i] - tope + _Mezcla, 0); suma += w[i]; }

                    // Cerca, la escala de detalle; lejos, la grande; en medio, las dos.
                    float lejos = smoothstep(_Escalas.z, _Escalas.w, distancia);
                    // Contra la repetición: cerca, cada capa son dos muestras a escalas que no encajan (× 1,73, desplazada y girada 37°), mezcladas
                    // por ruido.
                    float otra = smoothstep(0.35, 0.65, r2.w * 0.6 + r1.x * 0.4);
                    const float cg = 0.7986, sg = 0.6018; // cos y sen de 37°
                    float3 pg = GiroY(p + float3(517, 0, 251), cg, sg), dpxg = GiroY(dpx, cg, sg), dpyg = GiroY(dpy, cg, sg), Ng = GiroY(N, cg, sg);
                    c = 0;
                    float3 desvio = 0;
                    [unroll] for (int i = 0; i < CAPAS; i++)
                    {
                        [branch] if (w[i] > 0)
                        {
                            float peso = w[i] / suma;
                            float3 cc = 0, dd = 0, c1, d1;
                            [branch] if (lejos < 0.999)
                            {
                                float3 c2, d2;
                                Muestra(i, p, dpx, dpy, N, _Escalas.x, c1, d1);
                                Muestra(i, pg, dpxg, dpyg, Ng, _Escalas.x * 1.73, c2, d2);
                                d2 = GiroY(d2, cg, -sg); // la normal, de vuelta al mundo
                                cc += lerp(c1, c2, otra) * (1 - lejos);
                                dd += lerp(d1, d2, otra) * (1 - lejos);
                            }
                            [branch] if (lejos > 0.001) { Muestra(i, p + float3(311, 0, 173), dpx, dpy, N, _Escalas.y, c1, d1); cc += c1 * lejos; dd += d1 * lejos; }
                            c += cc * peso;
                            desvio += dd * peso;
                        }
                    }
                    // La variación grande: manchas de luz y de tono a cientos de metros, para que desde lejos no se vea la repetición.
                    c *= lerp(0.86, 1.1, r1.z) * lerp(0.94, 1.05, r2.z);
                    c = lerp(c, c * float3(1.04, 1.0, 0.9), r1.y * 0.6);
                    pesoNieve = w[NIEVE] / suma;
                    // En la nieve, la normal marca algo más (los ventisqueros).
                    Nluz = normalize(N + desvio * _FuerzaNormal * (1 + pesoNieve));
                }
                Light sol = GetMainLight();
                float l = saturate(dot(Nluz, sol.direction));
                // La nieve, con su propia sombra: luz envolvente y, en lo que no mira al sol, un azul claro (se lee el volumen sin manchas pintadas).
                float envolvente = saturate((dot(Nluz, sol.direction) + 0.5) / 1.5);
                float3 luzNieve = lerp(float3(0.72, 0.8, 0.93), float3(1.02, 1.01, 1.0), envolvente);
                c *= lerp((0.6 + 0.45 * l).xxx, luzNieve, pesoNieve);
                return half4(PintarMapa(c, p, true), 1);
            }
            ENDHLSL
        }
    }
}
