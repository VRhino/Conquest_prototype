Shader "BronzeAge/Campaign Vegetation"
{
 SubShader {
 Tags { "RenderType"="Opaque" }
 Pass {
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 struct input { float4 vertex:POSITION; float3 normal:NORMAL; float4 color:COLOR; };
 struct v2f { float4 pos:SV_POSITION; float3 color:COLOR; };
 v2f vert(input v) { v2f o; o.pos=UnityObjectToClipPos(v.vertex); float light=.45+.55*max(0,dot(UnityObjectToWorldNormal(v.normal),normalize(float3(-.5,.8,-.4)))); o.color=v.color.rgb*light; return o; }
 float4 frag(v2f i):SV_Target { return float4(i.color,1); }
 ENDHLSL
 }
 }
}
