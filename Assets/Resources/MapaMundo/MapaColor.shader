// El color plano del mapa de mundo (URP): color de vértice por el color del material, luz del sol con un poco de cielo, y la niebla de guerra
// encima con sus tres estados (nunca visto, recordado, a la vista), leída de la rejilla de la sesión, con las nubes y la bruma de `MapaComun.hlsl`. Lo propio (tu columna, la ruta y el
// destino) va con `_ConNiebla` = 0. Con instanciado: el color de cada caja va por instancia. Con `_Mece` = 1 (el paño del estandarte de las
// columnas), los vértices se mecen con el viento hacia +z del objeto, tanto como diga el alfa de su color (0 lo rígido).
Shader "Conquest/MapaColor"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _ConNiebla ("Con niebla", Float) = 1
        _Luz ("Con luz", Float) = 1
        _Mece ("Se mece con el viento", Float) = 0
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
            // La sombra del sol (URP, con cascadas y suave): `SombraDelSol` en MapaComun.hlsl.
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH

            // Las nubes, la niebla de guerra (global, la pone NieblaDelMapa) y la bruma, las mismas que en MapaSuelo.
            #include "MapaComun.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _ConNiebla;
                float _Luz;
                float _Mece;
            CBUFFER_END
            // El viento del paño: dos ondas desfasadas por sitio y por altura, que empujan hacia +z (la cara del paño) y algo de lado.
            float3 Mecer(float3 p, float peso)
            {
                if (_Mece < 0.5 || peso <= 0) return p;
                float3 o = TransformObjectToWorld(float3(0, 0, 0));
                float fase = o.x * 0.013 + o.z * 0.017;
                float t = _Time.y;
                p.z += peso * (0.004 + sin(t * 2.3 + fase + p.y * 60.0) * 0.004 + sin(t * 3.7 + fase * 1.7 + p.x * 90.0) * 0.002);
                p.x += peso * sin(t * 1.6 + fase) * 0.002;
                return p;
            }
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
                s.mundo = TransformObjectToWorld(Mecer(e.pos.xyz, e.color.a));
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
                    float3 n = normalize(s.normal);
                    float l = saturate(dot(n, sol.direction));
                    c *= LuzDelMapa(n.y, 0.6, 0.45 * l, SombraDelSol(s.mundo), 0.36);
                }
                // Lo propio (`_ConNiebla` 0) va sin nubes ni niebla de guerra, pero con la bruma lejana, como en BronzeAgeClient.
                return half4(PintarMapa(c, s.mundo, _ConNiebla > 0.5), 1);
            }
            ENDHLSL
        }
        // La sombra que arrojan las cosas y la columna (solo los que la tienen encendida en su renderer).
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
                float _ConNiebla;
                float _Luz;
                float _Mece;
            CBUFFER_END
            // El viento del paño: dos ondas desfasadas por sitio y por altura, que empujan hacia +z (la cara del paño) y algo de lado.
            float3 Mecer(float3 p, float peso)
            {
                if (_Mece < 0.5 || peso <= 0) return p;
                float3 o = TransformObjectToWorld(float3(0, 0, 0));
                float fase = o.x * 0.013 + o.z * 0.017;
                float t = _Time.y;
                p.z += peso * (0.004 + sin(t * 2.3 + fase + p.y * 60.0) * 0.004 + sin(t * 3.7 + fase * 1.7 + p.x * 90.0) * 0.002);
                p.x += peso * sin(t * 1.6 + fase) * 0.002;
                return p;
            }
            float3 _LightDirection;
            struct EntradaSombra { float4 pos : POSITION; float3 normal : NORMAL; float4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            float4 VertSombra(EntradaSombra e) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(e);
                float4 pos = TransformWorldToHClip(ApplyShadowBias(TransformObjectToWorld(Mecer(e.pos.xyz, e.color.a)), TransformObjectToWorldNormal(e.normal), _LightDirection));
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
