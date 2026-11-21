// El parche de suelo pedregoso alrededor de cada nodo de recursos «Mejorado» (`NodosDelMapa`): un disco pegado a la malla de suelo pintada,
// con la grava y la roca de las capas del propio suelo (`_SueloColor` de `MapaSuelo`, a su escala) y su mismo valor (nunca más claro), un tinte
// del mineral del 15 % como mucho y hacia oscuro (color de vértice; 0,5 = ninguno) y el contorno en lóbulos (las fases, en uv1), roto en manchas.
// La malla ya va alargada ladera abajo. El alfa del vértice es 0 sobre el agua (ríos, lagos, mar): ahí no se pinta. Encima del
// suelo y debajo de los caminos (cola Geometry+455), sin escribir profundidad.
Shader "Conquest/MapaParcheNodo"
{
    Properties
    {
        _SueloColor ("Color de las capas del suelo", 2DArray) = "" {}
        _SueloRuido ("Ruido suave (4 canales)", 2D) = "gray" {}
        _Escalas ("Metros por repetición: cerca, lejos; distancia del cambio (m)", Vector) = (48, 200, 220, 900)
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry+455" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            Offset -1, -2
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include "MapaComun.hlsl"
            #define GRAVA 4
            #define ROCA 5

            TEXTURE2D_ARRAY(_SueloColor); SAMPLER(sampler_SueloColor);
            TEXTURE2D(_SueloRuido); SAMPLER(sampler_SueloRuido);
            CBUFFER_START(UnityPerMaterial)
                float4 _Escalas;
            CBUFFER_END

            // uv: lo que se aleja del centro del nodo, en huellas del parche (0 en el centro, 1 en el borde); color: tinte del mineral (rgb) y
            // agua (a: 0 sobre el agua)
            struct Entrada { float4 pos : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; float2 fases : TEXCOORD1; float4 color : COLOR; };
            struct Salida { float4 pos : SV_POSITION; float3 normal : TEXCOORD0; float3 mundo : TEXCOORD1; float2 uv : TEXCOORD2; float2 fases : TEXCOORD3; float4 color : COLOR; };

            Salida Vert(Entrada e)
            {
                Salida s;
                s.mundo = TransformObjectToWorld(e.pos.xyz);
                s.pos = TransformWorldToHClip(s.mundo);
                s.normal = TransformObjectToWorldNormal(e.normal);
                s.uv = e.uv;
                s.fases = e.fases;
                s.color = e.color;
                return s;
            }

            float3 Capa(int capa, float3 p, float lejos)
            {
                float3 cerca = SAMPLE_TEXTURE2D_ARRAY(_SueloColor, sampler_SueloColor, p.xz / _Escalas.x, capa).rgb;
                float3 lejana = SAMPLE_TEXTURE2D_ARRAY(_SueloColor, sampler_SueloColor, (p.xz + float2(311, 173)) / _Escalas.y, capa).rgb;
                return lerp(cerca, lejana, lejos);
            }

            half4 Frag(Salida s) : SV_Target
            {
                float3 p = s.mundo;
                float2 pm = p.xz / max(_Bruma.w, 1); // en u
                float lejos = smoothstep(_Escalas.z, _Escalas.w, distance(p, _WorldSpaceCameraPos));
                float4 r1 = SAMPLE_TEXTURE2D(_SueloRuido, sampler_SueloRuido, pm / 1.7);
                float4 r2 = SAMPLE_TEXTURE2D(_SueloRuido, sampler_SueloRuido, pm / 0.45 + 0.37);
                // el contorno: dos o tres lóbulos (el radio según el rumbo, con las fases del nodo) y roto a pequeña escala; borde neto
                float d = length(s.uv);
                float rumbo = atan2(s.uv.y, s.uv.x);
                float borde = 0.8 + 0.13 * sin(2 * rumbo + s.fases.x) + 0.09 * sin(3 * rumbo + s.fases.y) + (r1.x - 0.5) * 0.2 + (r2.y - 0.5) * 0.12;
                float alfa = 1 - smoothstep(borde - 0.05, borde + 0.02, d);
                // hacia fuera se deshace en manchas sueltas (el suelo de debajo asoma entre ellas)
                alfa *= lerp(1, smoothstep(0.42, 0.5, r2.z * 0.7 + r1.w * 0.3 + (1 - d) * 0.35), smoothstep(0.45, 0.8, d / max(borde, 0.3)));
                // el pedregal del propio suelo: grava y roca a manchas, con su valor
                float roca = smoothstep(0.45, 0.62, r1.z * 0.6 + r2.x * 0.4 + (1 - d) * 0.25);
                float3 c = lerp(Capa(GRAVA, p, lejos), Capa(ROCA, p, lejos), roca);
                c = lerp(c, c * s.color.rgb * 2, 0.15) * 0.85; // algo más oscuro que la capa: nunca más claro que el suelo de alrededor
                alfa *= s.color.a;
                Light sol = GetMainLight();
                float3 n = normalize(s.normal);
                c *= LuzDelMapa(n.y, 0.6, 0.45 * saturate(dot(n, sol.direction)), SombraDelSol(p), 0.36);
                return half4(PintarMapa(c, p, true), saturate(alfa));
            }
            ENDHLSL
        }
    }
}
