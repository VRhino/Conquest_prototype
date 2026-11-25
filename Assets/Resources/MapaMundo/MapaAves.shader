// Las bandadas del mapa de mundo (tema 9, como `vida.ts` de BronzeAgeClient): cada pájaro da vueltas alrededor del centro de su bandada, a
// ratos aletea y a ratos planea, todo en el sombreado (una llamada para todos, `AvesDelMapa`). `_Aves`, dos float4 por pájaro: el centro (m)
// y el radio de la vuelta (m); el giro (rad/s, con su signo), la fase en la vuelta, la del aleteo y la envergadura (m). El modelo (`Vida/Ave`,
// envergadura 1, la cabeza hacia +z) lleva en el color R lo que mueve el aleteo y en G la sombra de debajo. Sobre lo nunca explorado se
// recoge (`NieblaCrecer`); lo explorado sin ver lo apaga `PintarMapa`.
Shader "Conquest/MapaAves"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "MapaComun.hlsl"

            StructuredBuffer<float4> _Aves;

            struct Entrada { float4 pos : POSITION; float4 color : COLOR; };
            struct Salida { float4 pos : SV_POSITION; float3 mundo : TEXCOORD0; float sombra : TEXCOORD1; float punta : TEXCOORD2; };

            Salida Vert(Entrada e, uint id : SV_InstanceID)
            {
                float4 a0 = _Aves[2 * id], a1 = _Aves[2 * id + 1];
                float t = _RelojMapa; // el reloj del mapa: las pruebas saben dónde está cada pájaro
                float mpu = max(_Bruma.w, 1);
                float ang = a1.y + t * a1.x;
                float3 sitio = a0.xyz + float3(cos(ang) * a0.w, sin(t * 0.7 + a1.z) * 0.3 * mpu, sin(ang) * a0.w);
                float2 rumbo = normalize(float2(-sin(ang), cos(ang)) * sign(a1.x));
                // a ratos aletea, a ratos planea
                float aleteo = sin(t * 11.0 + a1.z * 7.0) * smoothstep(-0.2, 0.3, sin(t * 0.5 + a1.z));
                float3 p = e.pos.xyz;
                p.y += aleteo * 0.35 * e.color.r;
                p *= a1.w * NieblaCrecer(sitio);
                float3 adelante = float3(rumbo.x, 0, rumbo.y), derecha = float3(rumbo.y, 0, -rumbo.x);
                Salida s;
                s.mundo = sitio + adelante * p.z + derecha * p.x + float3(0, p.y, 0);
                s.pos = TransformWorldToHClip(s.mundo);
                s.sombra = e.color.g;
                s.punta = e.color.r;
                return s;
            }

            half4 Frag(Salida s) : SV_Target
            {
                // plumaje claro, blanco roto, con las puntas de las alas oscuras (como cigüeñas o garcetas)
                float3 pluma = lerp(float3(0.92, 0.9, 0.85), float3(0.16, 0.15, 0.15), smoothstep(0.7, 0.95, s.punta));
                float3 c = SRGBToLinear(pluma * lerp(0.75, 1.0, s.sombra));
                return half4(PintarMapa(c, s.mundo, true), 1);
            }
            ENDHLSL
        }
    }
}
