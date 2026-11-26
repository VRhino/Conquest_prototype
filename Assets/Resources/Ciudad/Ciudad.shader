// Lo construido de la ciudad por dentro (plaza, pueblos, campamentos): los modelos de Blender (`BronzeAgeArte/interior`) con sus texturas
// propias que se repiten (color, normal y máscara: R altura, G suavidad, B oclusión) y las UV en metros. Luz de URP completa (PBR, sombras,
// luces adicionales en Forward+, SSAO, bruma), y encima lo que quita lo plano:
//   - la oclusión horneada por vértice (R del color de vértice), por `_Oclusion`;
//   - la suciedad que sube del suelo: hasta `_SuciedadAlto` metros sobre el pie del objeto, hacia `_Suciedad`, rota por la altura de la textura;
//   - con `_Recorte`, la pieza se recorta por el alfa del color (las mieses, las hojas en plano): se ven sus dos caras (`_Cull` a 0) y la
//     luz les llega casi como al suelo, sin caras a oscuras;
//   - un tono propio por casa: sale de la posición del objeto (cada choza, algo más clara, más gris o más cálida), por `_Variacion`.
Shader "Conquest/Ciudad"
{
    Properties
    {
        _ColorTex ("Color", 2D) = "white" {}
        [NoScaleOffset] [Normal] _NormalTex ("Normal", 2D) = "bump" {}
        [NoScaleOffset] _Mascara ("Máscara (R altura, G suavidad, B oclusión)", 2D) = "white" {}
        _Tono ("Tono", Color) = (1, 1, 1, 1)
        _Relieve ("Fuerza del relieve", Range(0, 2)) = 1
        _Oclusion ("Cuánta oclusión de vértice", Range(0, 1)) = 0.8
        _Suciedad ("Color de la suciedad del pie", Color) = (0.33, 0.26, 0.19, 1)
        _SuciedadAlto ("Hasta dónde sube (m)", Float) = 0.9
        _SuciedadFuerza ("Cuánta suciedad", Range(0, 1)) = 0.6
        _Variacion ("Variación de tono por casa", Range(0, 1)) = 0.5
        _Metal ("Metal (el bronce)", Range(0, 1)) = 0
        [Toggle(_RECORTE)] _Recorte ("Recortar por el alfa del color (mieses, hojas)", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Caras que no se dibujan", Float) = 2
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        TEXTURE2D(_ColorTex); SAMPLER(sampler_ColorTex);
        TEXTURE2D(_NormalTex); SAMPLER(sampler_NormalTex);
        TEXTURE2D(_Mascara); SAMPLER(sampler_Mascara);
        CBUFFER_START(UnityPerMaterial)
            float4 _ColorTex_ST;
            float4 _Tono;
            float4 _Suciedad;
            float _Relieve;
            float _Oclusion;
            float _SuciedadAlto;
            float _SuciedadFuerza;
            float _Variacion;
            float _Metal;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]
            HLSLPROGRAM
            #pragma shader_feature_local _RECORTE
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"

            struct Entrada
            {
                float4 pos : POSITION; float3 normal : NORMAL; float4 tangente : TANGENT; float2 uv : TEXCOORD0; float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Salida
            {
                float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 mundo : TEXCOORD1; float3 normal : TEXCOORD2; float4 tangente : TEXCOORD3;
                float4 dato : TEXCOORD4;      // x: oclusión de vértice; y: metros sobre el pie del objeto; z, w: el azar de la casa
                float niebla : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Salida Vert(Entrada e)
            {
                Salida s;
                UNITY_SETUP_INSTANCE_ID(e);
                UNITY_TRANSFER_INSTANCE_ID(e, s);
                s.mundo = TransformObjectToWorld(e.pos.xyz);
                s.pos = TransformWorldToHClip(s.mundo);
                s.normal = TransformObjectToWorldNormal(e.normal);
                s.tangente = float4(TransformObjectToWorldDir(e.tangente.xyz), e.tangente.w * GetOddNegativeScale());
                s.uv = TRANSFORM_TEX(e.uv, _ColorTex);
                float3 pie = TransformObjectToWorld(float3(0, 0, 0));
                float2 azar = frac(sin(float2(dot(pie.xz, float2(12.9898, 78.233)), dot(pie.xz, float2(39.346, 11.135)))) * 43758.5453);
                s.dato = float4(e.color.r, s.mundo.y - pie.y, azar);
                s.niebla = ComputeFogFactor(s.pos.z);
                return s;
            }

            half4 Frag(Salida s) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(s);
                float4 tex = SAMPLE_TEXTURE2D(_ColorTex, sampler_ColorTex, s.uv);
                #ifdef _RECORTE
                    clip(tex.a - 0.5);
                #endif
                float3 c = tex.rgb * _Tono.rgb;
                float3 masc = SAMPLE_TEXTURE2D(_Mascara, sampler_Mascara, s.uv).rgb;
                float3 nt = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalTex, sampler_NormalTex, s.uv), _Relieve);

                // el tono de la casa: algo más clara u oscura, y más cálida o más gris
                float gris = dot(c, float3(0.3, 0.59, 0.11));
                c = lerp(c, gris.xxx, (s.dato.z - 0.5) * 0.5 * _Variacion);
                c *= 1 + (s.dato.w - 0.5) * 0.3 * _Variacion;
                c *= lerp(float3(1, 1, 1), float3(1.05, 1.0, 0.93), (s.dato.z * s.dato.w) * _Variacion);

                // la suciedad del pie: sube más por lo hundido de la textura
                float pie = saturate(1 - s.dato.y / max(_SuciedadAlto, 0.01));
                float sucio = saturate(pie * pie * 1.4 - masc.r * 0.5 * pie) * _SuciedadFuerza;
                c = lerp(c, _Suciedad.rgb * (0.7 + 0.6 * gris), sucio);

                float ao = masc.b * lerp(1, s.dato.x, _Oclusion);

                float3 n = normalize(s.normal);
                float3 t = normalize(s.tangente.xyz);
                float3 b = cross(n, t) * s.tangente.w;
                float3 normal = normalize(nt.x * t + nt.y * b + nt.z * n);
                #ifdef _RECORTE
                    normal = normalize(lerp(normal, float3(0, 1, 0), 0.75));
                #endif

                InputData d = (InputData)0;
                d.positionWS = s.mundo;
                d.positionCS = s.pos;
                d.normalWS = normal;
                d.viewDirectionWS = GetWorldSpaceNormalizeViewDir(s.mundo);
                d.shadowCoord = TransformWorldToShadowCoord(s.mundo);
                d.fogCoord = s.niebla;
                d.bakedGI = SampleSH(normal);
                d.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(s.pos);
                d.shadowMask = half4(1, 1, 1, 1);

                SurfaceData sup = (SurfaceData)0;
                sup.albedo = c;
                sup.alpha = 1;
                sup.metallic = _Metal;
                sup.specular = half3(0, 0, 0);
                sup.smoothness = masc.g * (1 - sucio * 0.5);
                sup.normalTS = nt;
                sup.occlusion = ao;
                #if defined(_DBUFFER)
                    ApplyDecalToSurfaceData(s.pos, sup, d);
                #endif

                half4 color = UniversalFragmentPBR(d, sup);
                color.rgb = MixFog(color.rgb, s.niebla);
                return half4(color.rgb, 1);
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
            #pragma shader_feature_local _RECORTE
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection;
            float3 _LightPosition;
            struct Entrada { float4 pos : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Salida { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            Salida Vert(Entrada e)
            {
                UNITY_SETUP_INSTANCE_ID(e);
                float3 mundo = TransformObjectToWorld(e.pos.xyz);
                float3 normal = TransformObjectToWorldNormal(e.normal);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 hacia = normalize(_LightPosition - mundo);
                #else
                    float3 hacia = _LightDirection;
                #endif
                float4 pos = TransformWorldToHClip(ApplyShadowBias(mundo, normal, hacia));
                #if UNITY_REVERSED_Z
                    pos.z = min(pos.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    pos.z = max(pos.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                Salida s;
                s.pos = pos;
                s.uv = TRANSFORM_TEX(e.uv, _ColorTex);
                return s;
            }
            half4 Frag(Salida s) : SV_Target
            {
                #ifdef _RECORTE
                    clip(SAMPLE_TEXTURE2D(_ColorTex, sampler_ColorTex, s.uv).a - 0.5);
                #endif
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull [_Cull]
            HLSLPROGRAM
            #pragma shader_feature_local _RECORTE
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            struct Entrada { float4 pos : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Salida { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            Salida Vert(Entrada e)
            {
                Salida s;
                UNITY_SETUP_INSTANCE_ID(e);
                s.pos = TransformObjectToHClip(e.pos.xyz);
                s.uv = TRANSFORM_TEX(e.uv, _ColorTex);
                return s;
            }
            half4 Frag(Salida s) : SV_Target
            {
                #ifdef _RECORTE
                    clip(SAMPLE_TEXTURE2D(_ColorTex, sampler_ColorTex, s.uv).a - 0.5);
                #endif
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            // Para el SSAO: la profundidad y la normal (la de la malla; el relieve fino no hace falta aquí).
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull [_Cull]
            HLSLPROGRAM
            #pragma shader_feature_local _RECORTE
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            struct Entrada { float4 pos : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Salida { float4 pos : SV_POSITION; float3 normal : TEXCOORD0; float2 uv : TEXCOORD1; };
            Salida Vert(Entrada e)
            {
                Salida s;
                UNITY_SETUP_INSTANCE_ID(e);
                s.pos = TransformObjectToHClip(e.pos.xyz);
                s.normal = TransformObjectToWorldNormal(e.normal);
                s.uv = TRANSFORM_TEX(e.uv, _ColorTex);
                return s;
            }
            half4 Frag(Salida s) : SV_Target
            {
                #ifdef _RECORTE
                    clip(SAMPLE_TEXTURE2D(_ColorTex, sampler_ColorTex, s.uv).a - 0.5);
                #endif
                return half4(normalize(s.normal), 0);
            }
            ENDHLSL
        }
    }
}
