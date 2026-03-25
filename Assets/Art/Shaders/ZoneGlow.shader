Shader "Custom/ZoneGlow"
{
    // Columna de luz vertical para zonas de captura.
    // Aplicar a un cilindro hijo del ZoneIndicator GO.
    // ZTest Off: visible a través de irregularidades del terreno.
    // Blend One One (additive): se suma al color de la escena como luz.
    //
    // Usa object-space Y para el falloff: NO depende de MaterialPropertyBlock
    // con valores world-space, lo que garantiza compatibilidad con SRP Batcher.

    Properties
    {
        _BaseColor    ("Color", Color) = (0.5, 0.5, 0.5, 1)
        _FalloffPower ("Falloff Power (base=2 suave, 5=concentrado en base)", Range(0.5, 8)) = 2.0
        _MaxOpacity   ("Max Opacity / Glow Intensity", Range(0, 1)) = 0.4
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Transparent"
            "Queue"          = "Transparent+1"
            "RenderPipeline" = "UniversalPipeline"
        }

        Blend One One       // Additive: el glow se suma al color de la escena
        ZWrite Off
        ZTest Off           // Siempre visible, incluso a través del terreno
        Cull Front          // Solo cara interior del cilindro

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float  posOSy      : TEXCOORD0;   // Y en object-space: -1 (base) a +1 (tope)
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float  _FalloffPower;
                float  _MaxOpacity;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.posOSy      = IN.positionOS.y;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // El cilindro de Unity va de Y=-1 (base) a Y=+1 (tope) en object-space.
                // t = 0 en la base → glow máximo
                // t = 1 en el tope → glow = 0
                float t = saturate((IN.posOSy + 1.0) * 0.5);
                float heightMask = pow(1.0 - t, _FalloffPower);

                float intensity = heightMask * _MaxOpacity;

                // Con Blend One One el alpha no se usa — solo RGB importa
                return half4(_BaseColor.rgb * intensity, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/InternalErrorShader"
}
