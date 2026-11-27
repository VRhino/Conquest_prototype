// El agua del campo de batalla del lugar (Campo_De_Batalla_Del_Lugar.md §6.10, los colores de la mesa): una lámina transparente por vértice de
// 2 m (`MontajeDelCampo.Agua`), de #a8d8d0 a #3f8f9c según cubra de 0,4 a 1,4 m, con opacidad 0,3 + 0,55 por lo mismo; espuma (#f2f6f4) donde la
// lámina baja deprisa (los rápidos); un brillo del sol y unas ondas que se mueven. Por vértice, TEXCOORD0: x lo que cubre (m), y la espuma (0..1).
Shader "Conquest/CampoAgua"
{
    Properties
    {
        _Oscuro ("Oscurecido (el anillo)", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-10" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _Oscuro;
            CBUFFER_END
            struct Entrada { float4 pos : POSITION; float3 normal : NORMAL; float2 agua : TEXCOORD0; };
            struct Salida { float4 pos : SV_POSITION; float3 mundo : TEXCOORD0; float3 normal : TEXCOORD1; float2 agua : TEXCOORD2; float niebla : TEXCOORD3; };

            float Azar(float2 p) { p = p - 289.0 * floor(p / 289.0); return frac(sin(dot(p, float2(269.5, 183.3))) * 43758.5453); }
            float Ruido(float2 p)
            {
                float2 i = floor(p), f = frac(p), u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Azar(i), Azar(i + float2(1, 0)), u.x), lerp(Azar(i + float2(0, 1)), Azar(i + float2(1, 1)), u.x), u.y);
            }

            Salida Vert(Entrada e)
            {
                Salida s;
                s.mundo = TransformObjectToWorld(e.pos.xyz);
                s.pos = TransformWorldToHClip(s.mundo);
                s.normal = TransformObjectToWorldNormal(e.normal);
                s.agua = e.agua;
                s.niebla = ComputeFogFactor(s.pos.z);
                return s;
            }

            half4 Frag(Salida s) : SV_Target
            {
                float t = fmod(_Time.y, 3600.0);
                float cubre = smoothstep(0.4, 1.4, s.agua.x);
                // (los colores de la mesa, en lineal)
                float3 somero = float3(0.392, 0.686, 0.630), hondo = float3(0.050, 0.275, 0.332);
                float3 c = lerp(somero, hondo, cubre);
                float alfa = 0.3 + 0.55 * cubre;
                // ondas: dos ruidos que se mueven, para el brillo y la espuma
                float2 p = s.mundo.xz;
                float o1 = Ruido(p / 3.0 + float2(t * 0.35, t * 0.12)), o2 = Ruido(p / 1.3 - float2(t * 0.2, -t * 0.27));
                float3 N = normalize(normalize(s.normal) + float3(o1 - 0.5, 0, o2 - 0.5) * 0.25);
                Light sol = GetMainLight();
                float3 V = GetWorldSpaceNormalizeViewDir(s.mundo);
                float brillo = pow(saturate(dot(reflect(-sol.direction, N), V)), 60) * 0.8;
                float fresnel = pow(1 - saturate(dot(N, V)), 4);
                c = c * (SampleSH(float3(0, 1, 0)) * 0.6 + sol.color * 0.55) + brillo * sol.color + fresnel * float3(0.55, 0.65, 0.72) * 0.35;
                alfa = saturate(alfa + fresnel * 0.3 + brillo);
                float espuma = saturate(s.agua.y) * smoothstep(0.45, 0.75, o1 * 0.6 + o2 * 0.4 + s.agua.y * 0.3);
                c = lerp(c, float3(0.89, 0.92, 0.90), espuma);
                alfa = max(alfa, espuma * 0.9);
                c *= _Oscuro;
                return half4(MixFog(c, s.niebla), alfa);
            }
            ENDHLSL
        }
    }
}
