Shader "BronzeAge/Campaign Water"
{
 Properties {
 _DepthMap("Geographic height", 2D)="black"{}
 _WorldSize("World size", Vector)=(32000,24000,0,0)
 _WaterLevel("Water elevation",Float)=375
 _Coastal("Coastal depth shading",Float)=1
 _LocalShore("Mesh shore gradient",Float)=0
 _Flow("River flow",Float)=0
 _DeepColor("Deep water",Color)=(.035,.115,.155,1)
 _ShallowColor("Shallow water",Color)=(.12,.36,.34,1)
 _Highlights("Soft wave highlights",Range(0,.25))=.065
 _WaveSize("Wave size in metres",Float)=95
 _ShoreDepth("Shore depth falloff",Float)=28
 }
 SubShader {
 Tags { "RenderType"="Opaque" "Queue"="Geometry+10" "RenderPipeline"="UniversalPipeline" }
 Pass {
 Tags { "LightMode"="UniversalForward" }
 HLSLPROGRAM
 #pragma target 3.0
 #pragma vertex vert
 #pragma fragment frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
 TEXTURE2D(_DepthMap); SAMPLER(sampler_DepthMap);
 CBUFFER_START(UnityPerMaterial)
 float4 _WorldSize,_DeepColor,_ShallowColor;
 float _WaterLevel,_Coastal,_LocalShore,_Flow,_Highlights,_WaveSize,_ShoreDepth;
 CBUFFER_END
 struct Attributes { float4 vertex:POSITION; float3 normal:NORMAL; float2 uv:TEXCOORD0; float4 color:COLOR; };
 struct v2f { float4 pos:SV_POSITION; float3 world:TEXCOORD0; float2 uv:TEXCOORD1; float shore:TEXCOORD2; float3 normal:TEXCOORD3; };
 v2f vert(Attributes v) { v2f o; o.world=TransformObjectToWorld(v.vertex.xyz); o.pos=TransformWorldToHClip(o.world); o.uv=v.uv; o.shore=v.color.r; o.normal=TransformObjectToWorldNormal(v.normal); return o; }
 float4 frag(v2f i):SV_Target {
   float terrain=SAMPLE_TEXTURE2D(_DepthMap,sampler_DepthMap,i.world.xz/_WorldSize.xy).r*1000;
   float depth=lerp(28,max(0,_WaterLevel-terrain),_Coastal);
   depth=lerp(depth,i.shore*28,_LocalShore);
   float shore=exp(-depth*1.55/max(_ShoreDepth,1));
   float3 deep=_DeepColor.rgb, shallow=_ShallowColor.rgb;
   float2 waveUV=i.world.xz/max(_WaveSize,1);
   float wave=sin(waveUV.x*6.28+waveUV.y*1.7+sin(waveUV.y*.4)*2+_Time.y*.3);
   float3 color=lerp(deep,shallow,shore)+wave*.002;
   // UV.x is distance along the canonical river; the phase travels downstream.
   float current=sin(i.uv.x*6-_Time.y*1.2)*sin(i.uv.y*3.14);
   color+=current*.012*_Flow*saturate(i.shore*2);
   float3 normal=normalize(i.normal+float3(wave*.06,0,cos(waveUV.y*5+_Time.y*.22)*.04));
   float3 view=GetWorldSpaceNormalizeViewDir(i.world);
   Light sun=GetMainLight();
   float fresnel=pow(1-saturate(dot(normal,view)),4);
   float glint=pow(saturate(dot(reflect(-sun.direction,normal),view)),48);
   color+=sun.color*_Highlights*(glint+fresnel*.35);
   // A restrained silt edge avoids a luminous outline around every lake.
   color=lerp(color,_ShallowColor.rgb*.92,exp(-depth*.65)*.3);
   return float4(color,1);
 }
 ENDHLSL
 }
 }
}
