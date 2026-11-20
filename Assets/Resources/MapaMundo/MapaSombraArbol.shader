// La mancha de sombra blanda bajo cada árbol del mapa de mundo (`VegetacionDelMapa`, por instancias): el mapa no tiene sombras proyectadas y sin
// ella los árboles flotan sobre el suelo. Un cuadro tumbado sobre la pendiente, alargado hacia el lado contrario del sol, con una elipse que
// oscurece hasta un 45 % en el centro y se apaga hacia el borde; se multiplica sobre lo ya pintado (el suelo), antes que los árboles. En el
// bosque denso se solapan y el suelo entre árboles queda más oscuro.
Shader "Conquest/MapaSombraArbol"
{
    Properties
    {
        _Oscuro ("Oscurecimiento en el centro, fuera del bosque (en el cerrado, hasta 0,58: del bosque del motor)", Float) = 0.45
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry+5" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Blend DstColor Zero
            ZWrite Off
            Cull Off
            Offset -1, -1
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "MapaComun.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Oscuro;
            CBUFFER_END

            struct Entrada { float4 pos : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Salida { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float oscuro : TEXCOORD1; };

            Salida Vert(Entrada e)
            {
                Salida s;
                UNITY_SETUP_INSTANCE_ID(e);
                s.pos = TransformObjectToHClip(e.pos.xyz);
                s.uv = e.uv * 2 - 1;
                float3 origen = TransformObjectToWorld(float3(0, 0, 0));
                s.oscuro = lerp(_Oscuro, 0.58, smoothstep(0.3, 0.85, SueloBajo(origen).z));
                // con sombras reales, la mancha solo donde no llega el mapa de sombras
                if (_SombrasReales > 0.5) s.oscuro *= smoothstep(_SombraHasta * 0.75, _SombraHasta, distance(origen, _WorldSpaceCameraPos));
                return s;
            }

            half4 Frag(Salida s) : SV_Target
            {
                float r = length(s.uv);
                float k = 1 - s.oscuro * pow(1 - smoothstep(0.0, 1.0, r), 1.5);
                return half4(k, k, k, 1);
            }
            ENDHLSL
        }
    }
}
