// Los nodos de recursos «Mejorado» del mapa de mundo (`NodosDelMapa`): el afloramiento de Blender (`BronzeAgeArte/mapa/nodos`), de caras
// planas, con su color horneado (roca, vetas, cristales) y la oclusión horneada a medias (la otra mitad, la luz); la luz del sol con algo de
// cielo, la sombra real, la niebla de guerra, las nubes y la bruma de `MapaComun.hlsl`, como el resto. Mate. Con instanciado: un tono por
// nodo (`_Color`, casi blanco) para que no salgan todos iguales.
Shader "Conquest/MapaNodo"
{
    Properties
    {
        _ColorTex ("Color horneado", 2D) = "white" {}
        _Mascara ("Máscara (R: oclusión)", 2D) = "white" {}
        _Color ("Tono", Color) = (1, 1, 1, 1)
        _Oclusion ("Cuánta oclusión horneada", Float) = 0.7
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include "MapaComun.hlsl"

            TEXTURE2D(_ColorTex); SAMPLER(sampler_ColorTex);
            TEXTURE2D(_Mascara); SAMPLER(sampler_Mascara);
            CBUFFER_START(UnityPerMaterial)
                float _Oclusion;
            CBUFFER_END
            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Color)
            UNITY_INSTANCING_BUFFER_END(Props)

            struct Entrada { float4 pos : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Salida { float4 pos : SV_POSITION; float3 normal : TEXCOORD0; float3 mundo : TEXCOORD1; float2 uv : TEXCOORD2; float4 tono : COLOR; };

            Salida Vert(Entrada e)
            {
                Salida s;
                UNITY_SETUP_INSTANCE_ID(e);
                s.mundo = TransformObjectToWorld(e.pos.xyz);
                s.pos = TransformWorldToHClip(s.mundo);
                s.normal = TransformObjectToWorldNormal(e.normal);
                s.uv = e.uv;
                s.tono = UNITY_ACCESS_INSTANCED_PROP(Props, _Color);
                return s;
            }

            half4 Frag(Salida s) : SV_Target
            {
                float3 c = SAMPLE_TEXTURE2D(_ColorTex, sampler_ColorTex, s.uv).rgb * s.tono.rgb;
                float ao = SAMPLE_TEXTURE2D(_Mascara, sampler_Mascara, s.uv).r;
                c *= lerp(1, ao, _Oclusion);
                Light sol = GetMainLight();
                float3 n = normalize(s.normal);
                float l = saturate(dot(n, sol.direction));
                c *= LuzDelMapa(n.y, 0.6, 0.45 * l, SombraDelSol(s.mundo), 0.36);
                return half4(PintarMapa(c, s.mundo, true), 1);
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
            HLSLPROGRAM
            #pragma vertex VertSombra
            #pragma fragment FragSombra
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _Oclusion;
            CBUFFER_END
            float3 _LightDirection;
            struct EntradaSombra { float4 pos : POSITION; float3 normal : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            float4 VertSombra(EntradaSombra e) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(e);
                float4 pos = TransformWorldToHClip(ApplyShadowBias(TransformObjectToWorld(e.pos.xyz), TransformObjectToWorldNormal(e.normal), _LightDirection));
            #if UNITY_REVERSED_Z
                pos.z = min(pos.z, UNITY_NEAR_CLIP_VALUE);
            #else
                pos.z = max(pos.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                return pos;
            }
            half4 FragSombra() : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
