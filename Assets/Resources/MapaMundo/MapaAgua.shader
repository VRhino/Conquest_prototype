// El agua del mapa de mundo (URP). La base es la de BronzeAgeClient (`src/mundo3d/agua.ts`, `paisaje.ts`): transparente, sin escribir
// profundidad, con lo hondo por vértice sacado de las alturas (sin depth ni opaque texture). Encima, más detalle en estilo Synty:
//  * Lámina (mar y lagos, `_Modo` 0): UV0.x lo hondo (u), UV0.y 1 si es lago. Olas con el mapa de normales propio (`Agua/agua_olas`,
//    BronzeAgeArte/mapa/agua) a tres escalas y direcciones que se desplazan, más una variación grande para que no se repitan; destellos de sol
//    en las crestas; reflejo del cielo de verdad (`ColorDelCielo`); color de turquesa en lo somero a azul hondo, con variación; en la orilla,
//    una banda de espuma dibujada que entra y sale y, en el mar, la rompiente: líneas de espuma que avanzan hacia la orilla, rotas con ruido.
//    Los lagos, más calmados (olas más pequeñas y lentas), un punto más verdes y sin rompiente.
//  * Río (1): UV0 x de orilla a orilla, y los u andados, z lo rápido que baja (aguas blancas), w lo cerca de la desembocadura (abanico que se
//    funde con el mar). Ondas alargadas en el sentido de la corriente, más deprisa en el centro, unas pocas vetas finas de espuma, alguna roca
//    con espuma; el lecho se ve a través en los bordes y el agua es más oscura en el centro; el borde, irregular.
//  * Orilla mojada (2), vado (3): una franja que se desvanece hacia fuera; agua somera sobre guijarros claros con piedras que asoman y espumilla.
// Luz como el suelo (sol del cielo y un poco de cielo) y, encima, nubes, niebla de guerra y bruma (`MapaComun.hlsl`).
Shader "Conquest/MapaAgua"
{
    Properties
    {
        _Modo ("0 lámina, 1 río, 2 orilla mojada, 3 vado", Float) = 0
        _Desfase ("Desfase de profundidad (para pintar encima sin parpadeo)", Float) = 0
        _Olas ("Olas (normal en RGB, altura en A)", 2D) = "bump" {}
        _Somero ("Somero", Color) = (0.353, 0.682, 0.651, 1)
        _Turquesa ("Turquesa de lo muy somero", Color) = (0.42, 0.80, 0.74, 1)
        _Hondo ("Hondo", Color) = (0.114, 0.302, 0.447, 1)
        _Espuma ("Espuma", Color) = (0.910, 0.941, 0.918, 1)
        _OrillaMojada ("Orilla mojada", Color) = (0.420, 0.365, 0.251, 1)
        _Vado ("Guijarros del vado", Color) = (0.851, 0.780, 0.604, 1)
        _Detalle ("Detalle (0 barato: sin destellos ni tercera capa de olas)", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            Offset [_Desfase], [_Desfase]
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            // La sombra del sol (URP, con cascadas y suave): `SombraDelSol` en MapaComun.hlsl.
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include "MapaComun.hlsl"

            TEXTURE2D(_Olas); SAMPLER(sampler_Olas);
            CBUFFER_START(UnityPerMaterial)
                float _Modo, _Desfase, _Detalle;
                float4 _Somero, _Turquesa, _Hondo, _Espuma, _OrillaMojada, _Vado;
            CBUFFER_END

            struct Entrada { float4 pos : POSITION; float4 uv : TEXCOORD0; };
            struct Salida { float4 pos : SV_POSITION; float4 uv : TEXCOORD0; float3 mundo : TEXCOORD1; };

            Salida Vert(Entrada e)
            {
                Salida s;
                s.mundo = TransformObjectToWorld(e.pos.xyz);
                s.pos = TransformWorldToHClip(s.mundo);
                s.uv = e.uv;
                return s;
            }

            // Una capa de olas: la normal del mapa (en el plano: x, y) en el punto q (u), girada `ang` y desplazada con el tiempo.
            float3 Capa(float2 q, float escala, float ang, float2 velocidad, float t)
            {
                float c = cos(ang), s = sin(ang);
                float2 r = float2(c * q.x - s * q.y, s * q.x + c * q.y) / escala + velocidad * t;
                float4 m = SAMPLE_TEXTURE2D(_Olas, sampler_Olas, r);
                float2 n = m.xy * 2 - 1;
                return float3(c * n.x + s * n.y, -s * n.x + c * n.y, m.a); // de vuelta al giro del mundo
            }

            half4 Frag(Salida s) : SV_Target
            {
                float3 p = s.mundo;
                float mpu = max(_Bruma.w, 1);
                float2 pu = float2(p.x, -p.z) / mpu; // en u, con y = y del mapa
                float t = fmod(_Time.y, 3600.0);
                int modo = (int)round(_Modo);
                Light sol = GetMainLight();
                float3 V = normalize(GetCameraPositionWS() - p);
                float lejos = distance(GetCameraPositionWS(), p) / mpu;
                float cerca = 1 - smoothstep(12.0, 60.0, lejos);
                if (modo == 2)
                {
                    // La orilla mojada: tierra oscura que se desvanece hacia fuera, rota con ruido.
                    float3 c = _OrillaMojada.rgb * (0.6 + 0.45 * saturate(sol.direction.y));
                    float a = 0.8 * (1 - smoothstep(0.1, 1.0, s.uv.x)) * lerp(0.6, 1.0, Nubes(pu * 1.7));
                    return half4(PintarMapa(c, p, true), a);
                }

                bool lago = modo == 0 && s.uv.y > 0.5;
                float prof, rapido = 0, boca = 0, centro = 0;
                float3 o2 = float3(0, 0, 0.5), o3 = float3(0, 0, 0.5);
                float2 g;
                if (modo == 0)
                {
                    prof = s.uv.x;
                    // Las olas: tres capas del mapa propio, giradas y desplazadas, y una variación grande que las calma o las aviva por manchas.
                    float calma = lago ? 0.45 : 1.0, vel = lago ? 0.5 : 1.0;
                    float variacion = lerp(0.55, 1.15, Nubes(pu * 0.05 + 3.1));
                    float3 o1 = Capa(pu, 9.0, 0.3, float2(0.012, 0.007) * vel, t);
                    o2 = Capa(pu, 3.7, 2.1, float2(-0.02, 0.016) * vel, t);
                    if (_Detalle > 0.5) o3 = Capa(pu, 1.3, 4.0, float2(0.035, -0.03) * vel, t);
                    g = (o1.xy * 0.55 + o2.xy * 0.35 + o3.xy * 0.25 * cerca) * calma * variacion;
                }
                else
                {
                    centro = 1 - pow(abs(s.uv.x * 2 - 1), 2);
                    prof = modo == 3 ? 0.25 : 1.5 * centro;
                    rapido = s.uv.z;
                    boca = s.uv.w;
                    // El río: ondas alargadas en el sentido de la corriente (el mapa estirado a lo largo), río abajo, más deprisa en el centro.
                    float vel = lerp(0.5, 1.2, centro) * (1 + rapido);
                    float4 m1 = SAMPLE_TEXTURE2D(_Olas, sampler_Olas, float2(s.uv.x * 0.9, s.uv.y * 0.18 - t * 0.35 * vel));
                    float4 m2 = SAMPLE_TEXTURE2D(_Olas, sampler_Olas, float2(s.uv.x * 1.7 + 0.37, s.uv.y * 0.33 - t * 0.6 * vel));
                    float2 n1 = m1.xy * 2 - 1, n2 = m2.xy * 2 - 1;
                    g = (n1 * float2(0.35, 1) * 0.6 + n2 * float2(0.35, 1) * 0.4) * 0.55 * (1 + rapido * 1.5);
                    o2 = float3(n2, m2.a);
                }
                g *= lerp(0.45, 1.0, smoothstep(0.0, 1.2, prof)) * lerp(1.0, 0.35, smoothstep(15.0, 90.0, lejos));
                float3 N = normalize(float3(-g.x, 1, g.y)); // z = −y del mapa (en el río, aproximado: sus ondas no dependen del rumbo)

                // El color: turquesa en lo muy somero, el somero y el hondo mar adentro, con variación suave; los lagos, un punto más verdes.
                float3 base;
                float alfa;
                if (modo == 0)
                {
                    base = lerp(_Turquesa.rgb, _Somero.rgb, smoothstep(0.0, 0.6, prof));
                    base = lerp(base, _Hondo.rgb, smoothstep(0.5, 2.4, prof));
                    base *= lerp(0.92, 1.06, Nubes(pu * 0.08 + 7.7));
                    if (lago) base = lerp(base, base * float3(0.86, 1.04, 0.9), 0.7);
                    alfa = lerp(0.42, 0.9, smoothstep(0, 1.4, prof));
                }
                else
                {
                    // El río: verde azulado de la familia de lo somero del mar, más oscuro en el centro; en los bordes se ve el lecho.
                    base = lerp(_Somero.rgb * float3(0.86, 0.95, 0.88), _Hondo.rgb * float3(0.9, 1.05, 0.95), smoothstep(0.3, 1.6, prof) * 0.75);
                    alfa = lerp(0.28, 0.82, smoothstep(0.0, 1.3, prof));
                    // El borde, irregular a escala pequeña y fundido con la tierra; los extremos, desvanecidos (la fuente y la desembocadura).
                    float r1 = Nubes(pu * 2.3), r2 = Nubes(pu * 2.3 + 3.3);
                    float borde = smoothstep(0.0, 0.1 + 0.16 * r1, s.uv.x) * smoothstep(1.0, 0.9 - 0.16 * r2, s.uv.x);
                    alfa *= borde * smoothstep(0.0, 1.5, s.uv.y) * (1 - smoothstep(0.55, 1.0, boca) * 0.95);
                    base = lerp(base, _Turquesa.rgb, boca * 0.5);
                }
                if (modo == 3)
                {
                    // El vado: fondo de guijarros claros bien visible, piedras que asoman con espumilla alrededor.
                    float g1 = Nubes(pu * 3.0), g2 = Nubes(pu * 7.0 + 2.3);
                    float piedra = smoothstep(0.66, 0.7, g1);
                    float aro = smoothstep(0.58, 0.64, g1) * (1 - piedra);
                    base = lerp(_Vado.rgb * lerp(0.85, 1.12, g2), _Somero.rgb, 0.25);
                    base = lerp(base, _Vado.rgb * 1.2, piedra);
                    base = lerp(base, _Espuma.rgb, aro * 0.8);
                    float ancho = smoothstep(0.0, 0.18, s.uv.y) * smoothstep(1.0, 0.82, s.uv.y);
                    float largo = smoothstep(0.0, 0.25, s.uv.x) * smoothstep(1.0, 0.75, s.uv.x);
                    alfa = 0.92 * ancho * largo;
                }

                float l = saturate(dot(N, sol.direction));
                // la sombra en el agua, a media fuerza (y también en el destello del sol)
                // (la sombra del relieve, en UV0.z)
                float sombra = lerp(1, ConRelieve(SombraDelSol(p), s.uv.z), 0.5);
                // (el tinte de la luz, solo un 40 % en el agua: su color ya es el de su diseño; la luminancia, la misma)
                float3 luz = LuzDelMapa(N.y, 0.6, 0.45 * l, sombra, 0.36);
                float3 c = base * lerp(dot(luz, float3(0.2126, 0.7152, 0.0722)).xxx, luz, 0.4);
                // Reflejo del cielo de verdad (el degradado y las nubes del cielo) con fresnel.
                float3 R = reflect(-V, N);
                R.y = abs(R.y);
                float3 cielo = ColorDelCielo(R, _CieloCenit.rgb, _CieloHorizonte.rgb, _CieloSuelo.rgb, _CieloSol.xyz);
                float fres = pow(1 - saturate(dot(N, V)), 4);
                c = lerp(c, cielo, fres * (modo == 0 ? 0.6 : 0.35));
                if (modo == 0) alfa = lerp(alfa, 1, fres * 0.5);
                // Destellos del sol en las crestas de las olas pequeñas (chispean sin rejilla).
                float3 H = normalize(sol.direction + V);
                float especular = pow(saturate(dot(N, H)), 260);
                float chispa = _Detalle > 0.5 ? smoothstep(0.72, 0.8, o2.z * 0.5 + o3.z * 0.5) : 0.35;
                c += float3(1.0, 0.97, 0.9) * especular * chispa * 1.8 * cerca * sombra;

                float espuma = 0;
                if (modo == 0)
                {
                    // La banda de espuma de la orilla: un borde dibujado que entra y sale (ruido y un vaivén lento).
                    float limite = (lago ? 0.05 : 0.09) + 0.1 * Nubes(pu * 0.9 + float2(t * 0.02, 0)) + 0.03 * sin(t * 0.8 + dot(pu, float2(0.7, 0.4)));
                    espuma = 1 - smoothstep(limite - 0.025, limite, prof);
                    if (!lago)
                    {
                        // La rompiente: líneas que avanzan hacia la orilla siguiendo lo hondo, rotas con ruido.
                        float fase = prof * 7.0 + t * 0.35;
                        float linea = smoothstep(0.82, 0.96, sin(fase * 6.2832) * 0.5 + 0.5);
                        float rota = smoothstep(0.38, 0.62, Nubes(pu * 1.4 + float2(t * 0.02, 0)));
                        espuma = max(espuma, linea * rota * (1 - smoothstep(0.12, 0.6, prof)) * 0.85);
                    }
                }
                if (modo == 1)
                {
                    // Unas pocas vetas finas de espuma que siguen la corriente, aguas blancas donde baja deprisa y alguna roca con espuma.
                    float veta = smoothstep(0.8, 0.86, Nubes(float2(s.uv.x * 5.0, s.uv.y * 0.22 - t * 0.5)));
                    espuma = veta * 0.4 * centro + rapido * smoothstep(0.55, 0.72, Nubes(float2(s.uv.x * 9.0, s.uv.y * 1.6 - t * 1.6))) * 0.8;
                    float r = Nubes(pu * 1.1 + 9.1) * smoothstep(0.35, 0.0, abs(s.uv.x - 0.5));
                    float roca = smoothstep(0.8, 0.83, r), aro = smoothstep(0.73, 0.8, r) * (1 - roca);
                    espuma = max(espuma, aro * 0.9);
                    c = lerp(c, float3(0.55, 0.52, 0.47) * (0.6 + 0.45 * l), roca);
                    alfa = max(alfa, roca);
                }
                c = lerp(c, _Espuma.rgb, saturate(espuma) * 0.85);
                if (modo != 3) alfa = max(alfa, saturate(espuma) * 0.85 * (modo == 1 ? smoothstep(0.0, 0.2, s.uv.x) * smoothstep(1.0, 0.8, s.uv.x) : 1));
                return half4(PintarMapa(c, p, true), alfa);
            }
            ENDHLSL
        }
    }
}
