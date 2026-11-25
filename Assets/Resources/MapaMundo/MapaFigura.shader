// Las figuras con su modelo en el mapa de mundo (tema 7): la textura del modelo (`_BaseMap` × `_BaseColor`) con la luz y la bruma del mapa, como
// `MapaColor`. Las tropas traen en el alfa de su atlas la máscara del color de bando (1 = se tiñe): con `_Mascara` = 1 esas piezas se tiñen con el
// color de la relación con quien mira (`_ColorEquipo`, por instancia: propio, aliado, enemigo o neutral), con la receta de la batalla,
// lerp(rgb, rgb × color, alfa). Con `_Mece` = 1 (la cinta de la lanza del destino), los vértices se mecen con el viento tanto como diga el rojo de
// su color de vértice (0 lo rígido), en unidades del objeto.
Shader "Conquest/MapaFigura"
{
    Properties
    {
        _BaseMap ("Textura", 2D) = "white" {}
        _BaseColor ("Color", Color) = (1, 1, 1, 1)
        _Mascara ("Tiñe por el alfa (atlas de tropa)", Float) = 0
        _ConNiebla ("Con niebla", Float) = 1
        _Recorte ("Recorte por alfa (0: no)", Float) = 0
        _Cull ("Caras (0 las dos, 2 las de delante)", Float) = 2
        _Mece ("Se mece con el viento (rojo del color de vértice)", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Cull [_Cull]
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

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float _Mascara;
                float _ConNiebla;
                float _Recorte;
                float _Cull;
                float _Mece;
            CBUFFER_END
            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _ColorEquipo)
            UNITY_INSTANCING_BUFFER_END(Props)

            struct Entrada { float4 pos : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; float4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            // El viento de la cinta: dos ondas desfasadas por sitio y por altura, de lado y algo hacia delante (unidades del objeto: la lanza mide 1).
            float3 Mecer(float3 p, float peso)
            {
                if (_Mece < 0.5 || peso <= 0) return p;
                float3 o = TransformObjectToWorld(float3(0, 0, 0));
                float fase = o.x * 0.013 + o.z * 0.017, t = _Time.y;
                p.x += peso * (0.02 + sin(t * 2.1 + fase + p.y * 9.0) * 0.025);
                p.z += peso * sin(t * 3.3 + fase * 1.7 + p.y * 13.0) * 0.015;
                return p;
            }
            struct Salida { float4 pos : SV_POSITION; float3 normal : TEXCOORD0; float3 mundo : TEXCOORD1; float2 uv : TEXCOORD2; UNITY_VERTEX_INPUT_INSTANCE_ID };

            Salida Vert(Entrada e)
            {
                Salida s;
                UNITY_SETUP_INSTANCE_ID(e);
                UNITY_TRANSFER_INSTANCE_ID(e, s);
                s.mundo = TransformObjectToWorld(Mecer(e.pos.xyz, e.color.r));
                s.pos = TransformWorldToHClip(s.mundo);
                s.normal = TransformObjectToWorldNormal(e.normal);
                s.uv = TRANSFORM_TEX(e.uv, _BaseMap);
                return s;
            }

            half4 Frag(Salida s) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(s);
                float4 t = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, s.uv);
                if (_Recorte > 0) clip(t.a - _Recorte);
                float3 c = t.rgb * _BaseColor.rgb;
                float4 equipo = UNITY_ACCESS_INSTANCED_PROP(Props, _ColorEquipo);
                if (_Mascara > 0.5 && equipo.a > 0) c = lerp(c, c * equipo.rgb, t.a);
                Light sol = GetMainLight();
                float3 n = normalize(s.normal);
                if (_Cull < 1 && dot(n, GetWorldSpaceViewDir(s.mundo)) < 0) n = -n;   // (de los dos lados: la luz de la cara que se ve)
                float l = saturate(dot(n, sol.direction));
                c *= LuzDelMapa(n.y, 0.6, 0.45 * l, SombraDelSol(s.mundo), 0.36);
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
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float _Mascara;
                float _ConNiebla;
                float _Recorte;
                float _Cull;
                float _Mece;
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
