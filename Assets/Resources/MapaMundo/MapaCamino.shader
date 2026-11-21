// Las rayas sobre el suelo del mapa de mundo (URP): los caminos y las líneas del territorio (`TrazosEnElMapa`), como las del prototipo
// (BronzeAgeClient: `mundo3d/trazos.ts`). El camino es suelo: su textura (`Resources/MapaMundo/Caminos`, con los bordes fundidos por el alfa) por
// la luz del mapa (`LuzDelMapa`), con la sombra del sol y la del relieve (esta, por vértice en UV1.x, como el suelo). Las líneas del territorio
// van de color plano (`_Luz` 0), con su opacidad en el alfa del color de vértice. Encima, la niebla de guerra (si `_ConNiebla`), las nubes y la
// bruma de `MapaComun.hlsl`. Mezcla por alfa, sin escribir profundidad y sin arrojar sombra; `_Desplazar` las adelanta en el z-buffer (lo de
// encima tapa a lo de debajo: camino −2, frontera −3, zona y anillo −4).
Shader "Conquest/MapaCamino"
{
    Properties
    {
        _MainTex ("Textura (sRGB, con alfa; blanca en las líneas)", 2D) = "white" {}
        _Luz ("Con la luz del mapa", Float) = 1
        _ConNiebla ("Bajo la niebla de guerra", Float) = 1
        _Desplazar ("Adelanto en el z-buffer", Float) = -2
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry+460" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            Offset [_Desplazar], [_Desplazar]
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            // La sombra del sol (URP, con cascadas y suave): `SombraDelSol` en MapaComun.hlsl.
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include "MapaComun.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _Luz, _ConNiebla, _Desplazar;
            CBUFFER_END

            struct Entrada { float4 pos : POSITION; float2 uv : TEXCOORD0; float2 relieve : TEXCOORD1; float4 color : COLOR; };
            struct Salida { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 mundo : TEXCOORD1; float relieve : TEXCOORD2; float4 color : COLOR; };

            Salida Vert(Entrada e)
            {
                Salida s;
                s.mundo = TransformObjectToWorld(e.pos.xyz);
                s.pos = TransformWorldToHClip(s.mundo);
                s.uv = e.uv;
                s.relieve = e.relieve.x;
                s.color = e.color;
                return s;
            }

            half4 Frag(Salida s) : SV_Target
            {
                float4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, s.uv) * s.color;
                float3 col = c.rgb;
                if (_Luz > 0.5)
                {
                    // normales arriba (como el prototipo: el camino se alumbra como suelo llano)
                    Light sol = GetMainLight();
                    float l = saturate(sol.direction.y);
                    col *= LuzDelMapa(1, 0.6, 0.45 * l, ConRelieve(SombraDelSol(s.mundo), s.relieve), 0.36);
                }
                return half4(PintarMapa(col, s.mundo, _ConNiebla > 0.5), c.a);
            }
            ENDHLSL
        }
    }
}
