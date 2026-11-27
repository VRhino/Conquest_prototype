// La vegetación del campo de batalla del lugar: la de `Conquest/MapaVegetacion` (textura horneada × oclusión del color de vértice, variación de
// tono por planta y por rodal, translucidez a contraluz, viento con el alfa del color de vértice, tarjetas con alpha to coverage; la hierba del
// color del suelo de debajo y la flor del de su mancha), sin lo que es solo del mapa (la niebla de guerra, las nubes, el aclarado) y con la
// luz y la niebla de URP. El suelo de debajo sale de `_CampoDatos` (global, lo pone `VegetacionDelCampo`): R fertilidad, G humedad, B bosque,
// A arena, en celdas de 4 m del campo (`_CampoDatosCaja`: x0 y z0 de la escena, lado en m).
Shader "Conquest/CampoVegetacion"
{
    Properties
    {
        _MainTex ("Color (sRGB; con recorte, alfa)", 2D) = "white" {}
        _Normal ("Normal", 2D) = "bump" {}
        _Mascara ("Máscara (R oclusión, G grosor)", 2D) = "white" {}
        _Recorte ("Recorte por alfa (tarjetas)", Float) = 0
        _Translucidez ("Translucidez a contraluz", Float) = 0.3
        _FuerzaNormal ("Fuerza del mapa de normales", Float) = 0.6
        _Viento ("Viento (m en lo alto de la copa)", Float) = 0.12
        _Variacion ("Variación de tono (follaje 1, corteza poco)", Float) = 1
        _ConHondura ("Hondura del dosel", Float) = 0
        _Modo ("0 planta; 1 hierba; 2 flor", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Caras", Float) = 2
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry+10" }
        HLSLINCLUDE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_Normal); SAMPLER(sampler_Normal);
            TEXTURE2D(_Mascara); SAMPLER(sampler_Mascara);
            TEXTURE2D(_CampoDatos); SAMPLER(sampler_CampoDatos);
            float4 _CampoDatosCaja;
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _Recorte, _Translucidez, _FuerzaNormal, _Viento, _Cull, _Variacion, _ConHondura, _Modo;
            CBUFFER_END

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
            float3 ConViento(float3 w, float azar, float peso)
            {
                float t = fmod(_Time.y, 3600.0), fase = azar * 6.2832;
                float empuje = (sin(t * 1.1 + fase) * 0.6 + sin(t * 2.3 + fase * 1.7 + w.y * 0.25) * 0.4) * _Viento * peso;
                return w + float3(0.93, 0, 0.37) * empuje;
            }
            // El suelo bajo la planta: x verde (0 seco, 1 verde), y fertilidad, z bosque, w arena.
            float4 SueloBajo(float3 origen)
            {
                float4 d = SAMPLE_TEXTURE2D_LOD(_CampoDatos, sampler_CampoDatos, (origen.xz - _CampoDatosCaja.xy) / max(_CampoDatosCaja.z, 1), 0);
                float humedo = 0.5 * d.g + 0.5 * d.r;
                return float4(smoothstep(0.28, 0.55, humedo), d.r, d.b, d.a);
            }
        ENDHLSL
        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]
            AlphaToMask On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog

            struct Entrada
            {
                float4 pos : POSITION; float3 normal : NORMAL; float4 tangente : TANGENT; float2 uv : TEXCOORD0; float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Salida
            {
                float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 normal : TEXCOORD1; float4 tangente : TEXCOORD2; float3 mundo : TEXCOORD3;
                float4 color : COLOR; float2 azar : TEXCOORD4; float4 suelo : TEXCOORD5; float flor : TEXCOORD6; float niebla : TEXCOORD7;
            };

            Salida Vert(Entrada e)
            {
                Salida s;
                UNITY_SETUP_INSTANCE_ID(e);
                float3 origen = TransformObjectToWorld(float3(0, 0, 0));
                float azar = frac(sin(dot(origen.xz, float2(12.9898, 78.233))) * 43758.5453);
                float3 w = ConViento(TransformObjectToWorld(e.pos.xyz), azar, e.color.a);
                s.pos = TransformWorldToHClip(w);
                s.mundo = w;
                s.uv = TRANSFORM_TEX(e.uv, _MainTex);
                s.normal = TransformObjectToWorldNormal(e.normal);
                s.tangente = float4(TransformObjectToWorldDir(e.tangente.xyz), e.tangente.w * GetOddNegativeScale());
                s.color = e.color;
                s.azar = float2(azar, RuidoNube(origen.xz / 60.0) * 0.65 + RuidoNube(origen.xz / 25.0 + 13.1) * 0.35);
                s.suelo = SueloBajo(origen);
                // la flor: un color por mancha de unos 30 m
                float2 celda = floor(origen.xz / 30);
                s.flor = floor(frac(sin(dot(celda, float2(12.9898, 78.233)) + 401) * 43758.5453) * 4);
                s.niebla = ComputeFogFactor(s.pos.z);
                return s;
            }

            half4 Frag(Salida s) : SV_Target
            {
                float4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, s.uv);
                float alfa = 1;
                if (_Recorte > 0.5)
                {
                    clip(c.a - 0.35);
                    alfa = saturate((c.a - 0.5) / max(fwidth(c.a), 1e-4) + 0.5);
                }
                float3 n = normalize(s.normal);
                float3 tg = normalize(s.tangente.xyz - n * dot(s.tangente.xyz, n));
                float3 bt = cross(n, tg) * s.tangente.w;
                float3 nt = UnpackNormalScale(SAMPLE_TEXTURE2D(_Normal, sampler_Normal, s.uv), _FuerzaNormal);
                float3 N = normalize(nt.x * tg + nt.y * bt + nt.z * n);
                float4 m = SAMPLE_TEXTURE2D(_Mascara, sampler_Mascara, s.uv);
                float3 albedo = c.rgb * s.color.rgb * lerp(0.75, 1.0, m.r);
                float hondura = _ConHondura * 0.25 * smoothstep(0.3, 0.85, s.suelo.z);
                albedo *= lerp(1 - hondura, 1, saturate(pow(max(s.color.a, 0), 0.67) * 1.6 - 0.25));
                if (_Modo > 0.5 && _Modo < 1.5)
                {
                    float3 t = lerp(float3(0.352, 0.254, 0.073), float3(0.122, 0.178, 0.028), s.suelo.x);
                    t = lerp(t, float3(0.102, 0.178, 0.030), smoothstep(0.5, 0.9, s.suelo.y) * s.suelo.x);
                    t = lerp(t, float3(0.392, 0.305, 0.102), s.suelo.w);
                    albedo *= t / 0.216;
                }
                else if (_Modo > 1.5)
                {
                    float3 cf = s.flor < 1 ? float3(0.604, 0.027, 0.015) : s.flor < 2 ? float3(0.888, 0.658, 0.073)
                        : s.flor < 3 ? float3(0.888, 0.871, 0.745) : float3(0.319, 0.171, 0.521);
                    albedo = lerp(albedo, albedo * cf / 0.8, saturate(m.b));
                }
                float v = (RuidoNube(s.mundo.xz / 1.5) - 0.5) + (RuidoNube(s.mundo.xz / 5.0 + 31.7) - 0.5);
                albedo *= 1 + 0.3 * v * _Variacion;
                albedo *= lerp(float3(1, 1, 1), float3(1.03, 1.01, 0.94), saturate(v + 0.5) * 0.3 * _Variacion);
                float h1 = s.azar.x, h2 = frac(h1 * 7.13 + 0.31), h3 = frac(h1 * 13.7 + 0.71), rodal = s.azar.y;
                albedo *= (1 + 0.3 * (h1 - 0.5)) * lerp(0.88, 1.1, rodal);
                albedo *= lerp(float3(1, 1, 1), lerp(float3(0.97, 1.0, 1.05), float3(1.05, 1.02, 0.88), h2), 0.8 * _Variacion);
                float lum = dot(albedo, float3(0.2126, 0.7152, 0.0722));
                albedo = lerp(lum.xxx, albedo, 1 + 0.2 * (h3 - 0.5) * _Variacion);
                Light sol = GetMainLight(TransformWorldToShadowCoord(s.mundo));
                float envuelve = saturate((dot(N, sol.direction) + 0.3) / 1.3);
                float3 V = GetWorldSpaceNormalizeViewDir(s.mundo);
                float contraluz = pow(saturate(dot(-V, sol.direction)), 3) * m.g * _Translucidez;
                float3 col = albedo * (SampleSH(N) * 0.9 + sol.color * (0.8 * envuelve + 0.5 * contraluz) * lerp(0.45, 1, sol.shadowAttenuation));
                if (_Variacion < 0.5)
                {
                    float lc = dot(col, float3(0.2126, 0.7152, 0.0722));
                    col *= max(1, 0.035 / max(lc, 1e-4));
                }
                return half4(MixFog(col, s.niebla), alfa);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]
            HLSLPROGRAM
            #pragma vertex VertSombra
            #pragma fragment FragSombra
            #pragma multi_compile_instancing
            float3 _LightDirection;
            struct EntradaSombra { float4 pos : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; float4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct SalidaSombra { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            SalidaSombra VertSombra(EntradaSombra e)
            {
                SalidaSombra s;
                UNITY_SETUP_INSTANCE_ID(e);
                float3 origen = TransformObjectToWorld(float3(0, 0, 0));
                float azar = frac(sin(dot(origen.xz, float2(12.9898, 78.233))) * 43758.5453);
                float3 w = ConViento(TransformObjectToWorld(e.pos.xyz), azar, e.color.a);
                s.pos = TransformWorldToHClip(ApplyShadowBias(w, TransformObjectToWorldNormal(e.normal), _LightDirection));
            #if UNITY_REVERSED_Z
                s.pos.z = min(s.pos.z, UNITY_NEAR_CLIP_VALUE);
            #else
                s.pos.z = max(s.pos.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                s.uv = TRANSFORM_TEX(e.uv, _MainTex);
                return s;
            }
            half4 FragSombra(SalidaSombra s) : SV_Target
            {
                if (_Recorte > 0.5) clip(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, s.uv).a - 0.5);
                return 0;
            }
            ENDHLSL
        }
    }
}
