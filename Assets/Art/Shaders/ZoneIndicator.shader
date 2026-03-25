Shader "Custom/ZoneIndicator"
{
    Properties
    {
        _BaseColor       ("Color", Color) = (0.5, 0.5, 0.5, 1)
        _FalloffPower    ("Falloff Power (pupil curve)", Range(0.1, 10)) = 1.0
        _MaxOpacity      ("Max Opacity", Range(0, 1)) = 0.8
        _CaptureProgress ("Capture Progress", Range(0, 1)) = 0.0
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Transparent"
            "Queue"          = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float  fogCoord    : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float  _FalloffPower;
                float  _MaxOpacity;
                float  _CaptureProgress;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv          = IN.uv;
                OUT.fogCoord    = ComputeFogFactor(OUT.positionHCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // dist: 0 = centro, 1 = borde del círculo, >1 = exterior
                float dist = length(IN.uv - 0.5) * 2.0;

                // Descartar píxeles fuera del círculo
                clip(1.0 - dist);

                // Gradiente: borde exterior = opaco, centro = transparente
                // _FalloffPower controla la curva (como una pupila cerrándose):
                //   0.3 = suave, disco casi lleno con borde más brillante
                //   1.0 = lineal
                //   5.0 = anillo fino, casi todo transparente salvo el borde
                float radialMask = pow(saturate(dist), _FalloffPower);

                // Pulso en el borde durante captura
                // Localiza el anillo exterior (donde dist ≈ 1) y lo intensifica
                float edgeRing = saturate((dist - 0.7) / 0.3);  // solo último 30% del radio
                float pulse = 1.0 + edgeRing * _CaptureProgress * 0.6;

                float alpha = radialMask * _MaxOpacity * pulse;

                half4 color = half4(_BaseColor.rgb * pulse, alpha);
                color.rgb = MixFog(color.rgb, IN.fogCoord);
                return color;
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/InternalErrorShader"
}
