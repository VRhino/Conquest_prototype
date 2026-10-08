Shader "BronzeAge/Campaign Scale Overlay"
{
    Properties { _BaseColor ("Color", Color) = (0.15,0.8,0.8,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Overlay" "RenderType"="Transparent" }
        Pass
        {
            ZWrite Off ZTest Always Cull Off
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            CBUFFER_END
            struct Input { float4 positionOS:POSITION; };
            struct Output { float4 positionCS:SV_POSITION; };
            Output Vert(Input v) { Output o;o.positionCS=TransformObjectToHClip(v.positionOS.xyz);return o; }
            half4 Frag(Output v):SV_Target { return _BaseColor; }
            ENDHLSL
        }
    }
}
