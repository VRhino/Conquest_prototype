// El suelo del campo de batalla del lugar (Campo_De_Batalla_Del_Lugar.md §6.10): el de `Conquest/MapaSuelo` (las ocho capas de
// `Resources/MapaMundo/Suelo`, la mezcla por altura de textura, las dos escalas contra la repetición y la roca triplanar), sin lo que es solo
// del mapa (el territorio, la niebla de guerra, el papiro y las nubes), a la escala del soldado y con la luz y la niebla de URP.
// Lo que decide la capa llega por vértice (lo pone `MontajeDelCampo`):
// - color: R la fertilidad, G la humedad, B el bosque (0..1, del recorte del ticket, con el ruido del bosque del campo);
// - TEXCOORD0: la normal ancha (el relieve suavizado a unos 24 m), que decide la roca de pendiente;
// - TEXCOORD1: x la altura del mundo (m, sin el zoom: la de los umbrales del generador), y lo hondo bajo la lámina de agua (m), z lo mojado
//   junto al agua (0..1) y w lo pisado de la bajada al vado (0..1).
Shader "Conquest/CampoSuelo"
{
    Properties
    {
        _SueloColor ("Color de las capas", 2DArray) = "" {}
        _SueloNormal ("Normal de las capas", 2DArray) = "" {}
        _SueloMascara ("Máscara de las capas (R altura, G suavidad)", 2DArray) = "" {}
        _SueloRuido ("Ruido suave (4 canales)", 2D) = "gray" {}
        _Umbrales ("Altura del mundo (m): costa, colina, montaña, cima", Vector) = (420, 545, 600, 685)
        _Roca ("Roca por pendiente (1 − normal.y): desde, hasta", Vector) = (0.14, 0.4, 0, 0)
        _Verdor ("Verde por humedad y fertilidad: desde, hasta; tierra por debajo de", Vector) = (0.28, 0.55, 0.12, 0)
        _Escalas ("Metros por repetición: cerca, lejos; distancia del cambio (m): desde, hasta", Vector) = (6, 30, 60, 250)
        _FuerzaNormal ("Fuerza de la normal", Float) = 0.8
        _Mezcla ("Profundidad de la mezcla por altura", Float) = 0.3
        _Oscuro ("Oscurecido (el anillo, fuera del área jugable)", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
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

            TEXTURE2D_ARRAY(_SueloColor); SAMPLER(sampler_SueloColor);
            TEXTURE2D_ARRAY(_SueloNormal);
            TEXTURE2D_ARRAY(_SueloMascara);
            TEXTURE2D(_SueloRuido); SAMPLER(sampler_SueloRuido);

            CBUFFER_START(UnityPerMaterial)
                float4 _Umbrales;
                float4 _Roca;
                float4 _Verdor;
                float4 _Escalas;
                float _FuerzaNormal;
                float _Mezcla;
                float _Oscuro;
            CBUFFER_END

            struct Entrada { float4 pos : POSITION; float3 normal : NORMAL; float4 color : COLOR; float3 ancha : TEXCOORD0; float4 suelo : TEXCOORD1; };
            struct Salida { float4 pos : SV_POSITION; float3 normal : TEXCOORD0; float3 mundo : TEXCOORD1; float4 color : COLOR; float3 ancha : TEXCOORD2; float4 suelo : TEXCOORD3; float niebla : TEXCOORD4; };

            Salida Vert(Entrada e)
            {
                Salida s;
                s.mundo = TransformObjectToWorld(e.pos.xyz);
                s.pos = TransformWorldToHClip(s.mundo);
                s.normal = TransformObjectToWorldNormal(e.normal);
                s.color = e.color;
                s.ancha = TransformObjectToWorldNormal(e.ancha);
                s.suelo = e.suelo;
                s.niebla = ComputeFogFactor(s.pos.z);
                return s;
            }

            float3 PesosTriplanar(float3 N, int capa)
            {
                if (capa != ROCA && abs(N.y) > 0.57) return float3(0, 1, 0);
                float3 w = pow(abs(N), 8);
                w /= w.x + w.y + w.z;
                if (w.y > 0.98) w = float3(0, 1, 0);
                return w;
            }

            void Muestra(int capa, float3 p, float3 dpx, float3 dpy, float3 N, float escala, out float3 color, out float3 desvio)
            {
                float k = (capa == ROCA ? 1.5 : 1.0) / escala;
                float3 w = PesosTriplanar(N, capa);
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

            float AltoCapa(int capa, float3 p, float3 dpx, float3 dpy, float3 N, float escala)
            {
                float k = (capa == ROCA ? 1.5 : 1.0) / escala;
                float3 w = PesosTriplanar(N, capa);
                float2 uvs[3] = { p.zy * k, p.xz * k, p.xy * k };
                float2 dxs[3] = { dpx.zy * k, dpx.xz * k, dpx.xy * k };
                float2 dys[3] = { dpy.zy * k, dpy.xz * k, dpy.xy * k };
                float a = 0;
                [unroll] for (int i = 0; i < 3; i++)
                    [branch] if (w[i] > 0) a += SAMPLE_TEXTURE2D_ARRAY_GRAD(_SueloMascara, sampler_SueloColor, uvs[i], capa, dxs[i], dys[i]).r * w[i];
                return a;
            }

            float3 GiroY(float3 v, float c, float s) { return float3(c * v.x - s * v.z, v.y, s * v.x + c * v.z); }

            half4 Frag(Salida s) : SV_Target
            {
                float3 p = s.mundo;
                float3 N = normalize(s.normal);
                float3 dpx = ddx(p), dpy = ddy(p);
                float distancia = distance(GetCameraPositionWS(), p);
                // el ruido suave a tres escalas (m)
                float4 r1 = SAMPLE_TEXTURE2D(_SueloRuido, sampler_SueloRuido, p.xz / 400.0);
                float4 r2 = SAMPLE_TEXTURE2D(_SueloRuido, sampler_SueloRuido, p.xz / 90.0 + 0.37);
                float4 r3 = SAMPLE_TEXTURE2D(_SueloRuido, sampler_SueloRuido, p.xz / 14.0 + 0.71);
                float pendienteAncha = 1 - normalize(s.ancha).y;
                float3 datos = s.color.rgb;
                float pendiente = 1 - N.y;
                float h = s.suelo.x + (r2.z - 0.5) * 4 + (r1.w - 0.5) * 3;
                float hondo = s.suelo.y, mojado = s.suelo.z, pisado = s.suelo.w;

                float humedo = 0.5 * datos.y + 0.5 * datos.x + (r2.w - 0.5) * 0.3 + (r1.x - 0.5) * 0.15;
                float verde = smoothstep(_Verdor.x, _Verdor.y, humedo);
                float tierra = smoothstep(_Verdor.z + 0.15, _Verdor.z - 0.08, humedo) * smoothstep(0.42, 0.72, r2.z * 0.5 + r1.w * 0.5) * 0.8;
                float w[CAPAS];
                w[PASTO_SECO] = (1 - verde) * (1 - tierra);
                w[PASTO_VERDE] = verde * (1 - tierra);
                w[TIERRA] = tierra;
                w[ARENA] = 0; w[GRAVA] = 0; w[ROCA] = 0; w[NIEVE] = 0; w[SOTOBOSQUE] = 0;
                float bosque = smoothstep(0.08, 0.85, datos.z + (r2.y - 0.5) * 0.3) * 0.8;
                float grava = smoothstep(_Umbrales.y + 25, _Umbrales.z, h) * (1 - smoothstep(_Umbrales.z + 30, _Umbrales.w, h)) * smoothstep(0.35, 0.7, r2.x * 0.7 + r1.z * 0.3);
                float arena = 1 - smoothstep(_Umbrales.x - 12, _Umbrales.x + 8, h);
                float rocaPendiente = smoothstep(_Roca.x, _Roca.y, pendienteAncha + (r2.y - 0.5) * 0.14 + (r3.x - 0.5) * 0.12);
                float aristaEstrecha = smoothstep(_Roca.x, _Roca.y, pendiente + (r3.y - 0.5) * 0.1) * (1 - rocaPendiente);
                grava = max(grava, saturate(rocaPendiente * (1 - rocaPendiente) * 3 + aristaEstrecha * 0.6));
                float roca = max(smoothstep(_Umbrales.z + 20, _Umbrales.w + 10, h), rocaPendiente);
                // bajo la lámina: el fondo, arena con manchas de guijarros que se pierden con lo hondo
                float fondo = smoothstep(0.0, 0.1, hondo);
                float fondoGrava = fondo * smoothstep(0.62, 0.78, r3.y * 0.45 + r2.y * 0.55) * (1 - smoothstep(0.4, 2.0, hondo)) * 0.7;
                // la orilla mojada (arena y guijarros, a manchas) y la bajada al vado (tierra pisada)
                float ribera = smoothstep(0.25, 0.6, mojado + (r3.z - 0.5) * 0.55 + (r2.x - 0.5) * 0.45) * (1 - fondo);
                float riberaGrava = ribera * smoothstep(0.66, 0.8, r3.x * 0.5 + r2.z * 0.5) * 0.7;
                float camino = smoothstep(0.2, 0.7, pisado + (r3.w - 0.5) * 0.4) * (1 - fondo);
                float encima[8] = { bosque, grava, arena, roca, ribera, riberaGrava, camino, fondo };
                int capaDe[8] = { SOTOBOSQUE, GRAVA, ARENA, ROCA, ARENA, GRAVA, TIERRA, ARENA };
                [unroll] for (int e = 0; e < 8; e++)
                {
                    [unroll] for (int i = 0; i < CAPAS; i++) w[i] *= 1 - encima[e];
                    w[capaDe[e]] += encima[e];
                }
                [unroll] for (int i = 0; i < CAPAS; i++) w[i] *= 1 - fondoGrava;
                w[GRAVA] += fondoGrava;

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

                float lejos = smoothstep(_Escalas.z, _Escalas.w, distancia);
                float otra = smoothstep(0.35, 0.65, r2.w * 0.6 + r1.x * 0.4);
                const float cg = 0.7986, sg = 0.6018;
                float3 pg = GiroY(p + float3(517, 0, 251), cg, sg), dpxg = GiroY(dpx, cg, sg), dpyg = GiroY(dpy, cg, sg), Ng = GiroY(N, cg, sg);
                float3 c = 0, desvio = 0;
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
                            d2 = GiroY(d2, cg, -sg);
                            cc += lerp(c1, c2, otra) * (1 - lejos);
                            dd += lerp(d1, d2, otra) * (1 - lejos);
                        }
                        [branch] if (lejos > 0.001) { Muestra(i, p + float3(311, 0, 173), dpx, dpy, N, _Escalas.y, c1, d1); cc += c1 * lejos; dd += d1 * lejos; }
                        c += cc * peso;
                        desvio += dd * peso;
                    }
                }
                c *= lerp(0.86, 1.1, r1.z) * lerp(0.94, 1.05, r2.z);
                c = lerp(c, c * float3(1.04, 1.0, 0.9), r1.y * 0.6);
                // lo hondo oscurece y enfría el fondo; la orilla mojada, más oscura
                c *= lerp(float3(1, 1, 1), float3(0.41, 0.48, 0.58), smoothstep(0.0, 2.5, hondo));
                c *= lerp(float3(1, 1, 1), float3(0.7, 0.72, 0.74), saturate(mojado * 0.8) * (1 - fondo));
                c *= _Oscuro;

                float3 Nluz = normalize(N + desvio * _FuerzaNormal);
                Light sol = GetMainLight(TransformWorldToShadowCoord(p));
                float directa = saturate(dot(Nluz, sol.direction)) * sol.shadowAttenuation;
                float3 luz = SampleSH(Nluz) + directa * sol.color;
                c *= luz;
                c = MixFog(c, s.niebla);
                return half4(c, 1);
            }
            ENDHLSL
        }
        // La sombra que echa el relieve sobre sí mismo y sobre las tropas.
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
