// Las maquetas de asentamientos «Mejorado» del mapa de mundo (`MaquetasDelMapa`): la miniatura de Blender (`BronzeAgeArte/mapa/asentamientos`),
// hermana de `MapaNodo`: de caras planas, con su color horneado y la oclusión horneada a medias (canal R de la máscara); las banderas (canal G)
// toman el color de la Facción, por instancia (`_Faccion`). La luz del sol con algo de cielo, la sombra real, la bruma y, con `_ConNiebla`, la
// niebla de guerra y las nubes (las plazas que solo se recuerdan). Mate.
Shader "Conquest/MapaAsentamiento"
{
    Properties
    {
        _ColorTex ("Color horneado", 2D) = "white" {}
        _Mascara ("Máscara (R: oclusión)", 2D) = "white" {}
        _Color ("Tono", Color) = (1, 1, 1, 1)
        _Oclusion ("Cuánta oclusión horneada", Float) = 0.6
        [Toggle] _Rodilla ("Comprimir las luces (que el encalado al sol no se queme)", Float) = 0
        _ConNiebla ("Con niebla de guerra", Float) = 0
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
                float _Rodilla;
                float _ConNiebla;
            CBUFFER_END
            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Color)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Faccion)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Recurso)
            UNITY_INSTANCING_BUFFER_END(Props)

            struct Entrada { float4 pos : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Salida { float4 pos : SV_POSITION; float3 normal : TEXCOORD0; float3 mundo : TEXCOORD1; float2 uv : TEXCOORD2; float4 tono : COLOR; float4 faccion : TEXCOORD3; float4 recurso : TEXCOORD4; };

            Salida Vert(Entrada e)
            {
                Salida s;
                UNITY_SETUP_INSTANCE_ID(e);
                s.mundo = TransformObjectToWorld(e.pos.xyz);
                s.pos = TransformWorldToHClip(s.mundo);
                s.normal = TransformObjectToWorldNormal(e.normal);
                s.uv = e.uv;
                s.tono = UNITY_ACCESS_INSTANCED_PROP(Props, _Color);
                s.faccion = UNITY_ACCESS_INSTANCED_PROP(Props, _Faccion);
                s.recurso = UNITY_ACCESS_INSTANCED_PROP(Props, _Recurso);
                return s;
            }

            half4 Frag(Salida s) : SV_Target
            {
                float3 c = SAMPLE_TEXTURE2D(_ColorTex, sampler_ColorTex, s.uv).rgb * s.tono.rgb;
                float3 masc = SAMPLE_TEXTURE2D(_Mascara, sampler_Mascara, s.uv).rgb;
                float ao = masc.r;
                // las banderas: el color de la Facción (su paño es casi blanco en la textura: conserva su sombreado horneado)
                c = lerp(c, s.faccion.rgb * saturate(c * 1.1), saturate(masc.g * 1.5));
                // el mineral (B, los montones de las minas): el color del recurso del nodo, con el claroscuro horneado
                c = lerp(c, s.recurso.rgb * saturate(dot(c, float3(0.3, 0.59, 0.11)) * 1.5), saturate(masc.b * 1.5) * s.recurso.a);
                c *= lerp(1, ao, _Oclusion);
                Light sol = GetMainLight();
                float3 n = normalize(s.normal);
                float l = saturate(dot(n, sol.direction));
                // (más luz de ambiente y sombra menos honda que el suelo: lo construido tiene que encontrarse solo, más claro que el terreno)
                c *= LuzDelMapa(n.y, 0.9, 0.45 * l, SombraDelSol(s.mundo), 0.6);
                if (_Rodilla > 0.5)
                {
                    // las luces, comprimidas por encima de 0,6 (lineal): lo más claro al sol se queda hacia 230-235 (sRGB), sin cambiar el tono y
                    // sin dejar de ser lo más claro
                    float l = max(c.r, max(c.g, c.b));
                    if (l > 0.6) c *= (0.6 + 0.5 * (l - 0.6)) / l;
                }
                return half4(PintarMapa(c, s.mundo, _ConNiebla > 0.5), 1);
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
                float _Rodilla;
                float _ConNiebla;
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
