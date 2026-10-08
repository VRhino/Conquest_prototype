Shader "BronzeAge/Campaign Coastal Rock" {
 Properties { _BaseColor("Limestone color",Color)=(.53,.48,.38,1) _MineralScale("Mineral patch size",Float)=85 _StrataScale("Bedding height",Float)=12 }
 SubShader {
  Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
  Pass {
   Tags { "LightMode"="UniversalForward" }
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
   #pragma multi_compile_fragment _ _SHADOWS_SOFT
   #pragma multi_compile_fog
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   CBUFFER_START(UnityPerMaterial)
    half4 _BaseColor;
    float _MineralScale,_StrataScale;
   CBUFFER_END
   struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
   struct Varyings {float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; half3 normalWS:TEXCOORD1; half fog:TEXCOORD2;};
   float Noise(float2 p) {
    float2 c=floor(p),f=frac(p);f=f*f*(3-2*f);
    float4 n=frac(sin(float4(dot(c,float2(127.1,311.7)),dot(c+float2(1,0),float2(127.1,311.7)),dot(c+float2(0,1),float2(127.1,311.7)),dot(c+1,float2(127.1,311.7))))*43758.5453);
    return lerp(lerp(n.x,n.y,f.x),lerp(n.z,n.w,f.x),f.y);
   }
   Varyings Vert(Attributes a) {Varyings o;VertexPositionInputs p=GetVertexPositionInputs(a.positionOS.xyz);o.positionCS=p.positionCS;o.positionWS=p.positionWS;o.normalWS=TransformObjectToWorldNormal(a.normalOS);o.fog=ComputeFogFactor(p.positionCS.z);return o;}
   half4 Frag(Varyings i):SV_Target {
    half3 normal=normalize(i.normalWS);
    float mineral=Noise(i.positionWS.xz/max(1,_MineralScale));
    float fracture=Noise(float2(i.positionWS.x+i.positionWS.z*.4,i.positionWS.y*.65)/21);
    half bedding=sin(i.positionWS.y/max(1,_StrataScale)+mineral*3);
    half3 albedo=_BaseColor.rgb*(.92+mineral*.16+(fracture-.5)*.10+bedding*.035);
    Light light=GetMainLight(TransformWorldToShadowCoord(i.positionWS));
    half diffuse=saturate(dot(normal,light.direction));
    half3 color=albedo*(SampleSH(normal)+light.color*diffuse*light.distanceAttenuation*light.shadowAttenuation);
    return half4(MixFog(color,i.fog),1);
   }
   ENDHLSL
  }
 }
}
