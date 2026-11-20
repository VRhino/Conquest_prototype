// El color plano del mapa de mundo (URP): color de vértice por el color del material, luz del sol con un poco de cielo, y la niebla de guerra
// encima con sus tres estados (nunca visto, recordado, a la vista), leída de la rejilla de la sesión, con las nubes y la bruma de `MapaComun.hlsl`. Lo propio (tu columna, la ruta y el
// destino) va con `_ConNiebla` = 0. Con instanciado: el color de cada caja va por instancia.
Shader "Conquest/MapaColor"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _ConNiebla ("Con niebla", Float) = 1
        _Luz ("Con luz", Float) = 1
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

            // Las nubes, la niebla de guerra (global, la pone NieblaDelMapa) y la bruma, las mismas que en MapaSuelo.
            #include "MapaComun.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _ConNiebla;
                float _Luz;
            CBUFFER_END
            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Color)
            UNITY_INSTANCING_BUFFER_END(Props)

            struct Entrada { float4 pos : POSITION; float3 normal : NORMAL; float4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Salida { float4 pos : SV_POSITION; float3 normal : TEXCOORD0; float3 mundo : TEXCOORD1; float4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };

            Salida Vert(Entrada e)
            {
                Salida s;
                UNITY_SETUP_INSTANCE_ID(e);
                UNITY_TRANSFER_INSTANCE_ID(e, s);
                s.mundo = TransformObjectToWorld(e.pos.xyz);
                s.pos = TransformWorldToHClip(s.mundo);
                s.normal = TransformObjectToWorldNormal(e.normal);
                s.color = e.color * UNITY_ACCESS_INSTANCED_PROP(Props, _Color);
                return s;
            }

            half4 Frag(Salida s) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(s);
                float3 c = s.color.rgb;
                if (_Luz > 0.5)
                {
                    Light sol = GetMainLight();
                    float l = saturate(dot(normalize(s.normal), sol.direction));
                    c *= 0.6 + 0.45 * l;
                }
                // Lo propio (`_ConNiebla` 0) va sin nubes ni niebla de guerra, pero con la bruma lejana, como en BronzeAgeClient.
                return half4(PintarMapa(c, s.mundo, _ConNiebla > 0.5), 1);
            }
            ENDHLSL
        }
    }
}
