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
            #include "MapaComun.hlsl"

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

            half4 Frag(Salida s) : SV_Target
            {
                return half4(ColorDelCielo(s.dir, _Cenit.rgb, _Horizonte.rgb, _Suelo.rgb, _Sol.xyz), 1);
            }
            ENDHLSL
        }
    }
}
