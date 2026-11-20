// El cielo del mapa de mundo (cielo de fondo, `RenderSettings.skybox`), calcado de BronzeAgeClient (`src/mundo3d/cielo.ts`): degradado del
// horizonte al cénit (1 − e^(−6·altura)), por debajo del horizonte hacia el color del suelo, nubes de ruido proyectadas en un plano que derivan
// despacio y el sol con su halo. Sin ciclo de día y noche. Los colores van como propiedades (sRGB; Unity los pasa a lineal) y la cuenta, en
// lineal, como allí.
Shader "Conquest/CieloDelMapa"
{
    Properties
    {
        _Cenit ("Cénit", Color) = (0.239, 0.447, 0.690, 1)
        _Horizonte ("Horizonte", Color) = (0.718, 0.776, 0.812, 1)
        _Suelo ("Bajo el horizonte", Color) = (0.557, 0.604, 0.596, 1)
        _Sol ("Dirección del sol (hacia el sol)", Vector) = (-0.43, 0.79, -0.32, 0)
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Cenit, _Horizonte, _Suelo, _Sol;
            CBUFFER_END

            struct Entrada { float4 pos : POSITION; };
            struct Salida { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };

            Salida Vert(Entrada e)
            {
                Salida s;
                s.pos = TransformObjectToHClip(e.pos.xyz);
                s.dir = e.pos.xyz;
                return s;
            }

            float Azar(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float Ruido(float2 p)
            {
                float2 i = floor(p), f = frac(p), u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Azar(i), Azar(i + float2(1, 0)), u.x), lerp(Azar(i + float2(0, 1)), Azar(i + float2(1, 1)), u.x), u.y);
            }
            float Fbm(float2 p)
            {
                float s = 0, a = 0.5;
                [unroll] for (int k = 0; k < 5; k++) { s += a * Ruido(p); p *= 2.03; a *= 0.5; }
                return s;
            }

            half4 Frag(Salida s) : SV_Target
            {
                float3 d = normalize(s.dir);
                float h = d.y;
                float3 col = h > 0 ? lerp(_Horizonte.rgb, _Cenit.rgb, 1 - exp(-h * 6)) : lerp(_Horizonte.rgb, _Suelo.rgb, saturate(-h * 4));
                if (h > 0.02)
                {
                    float t = fmod(_Time.y, 100000.0);
                    float2 q = d.xz / (h + 0.12) * 1.6 + float2(t * 0.004, t * 0.0015);
                    float n = smoothstep(0.52, 0.85, Fbm(q));
                    col = lerp(col, float3(0.97, 0.97, 0.95), n * 0.55 * smoothstep(0.02, 0.25, h));
                }
                float sol = max(dot(d, normalize(_Sol.xyz)), 0);
                col += float3(1.0, 0.86, 0.62) * (pow(sol, 600) * 1.2 + pow(sol, 24) * 0.22);
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
