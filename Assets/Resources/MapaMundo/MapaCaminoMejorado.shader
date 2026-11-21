// Los caminos del mapa de mundo, estilo «Mejorado» (`TrazosEnElMapa.Estilo`): el camino es suelo. Su tierra es la de las texturas del suelo
// (`MapaSuelo`: tierra, arena, grava y pasto, en espacio de mundo y a las mismas escalas), según lo que hay debajo (por vértice, de la CPU:
// arena, roca, pradera); encima, el detalle horneado en Blender desde geometría (`BronzeAgeArte/mapa/caminos`: rodadas, lomo con hierba, huellas
// y guijarros; dos variantes que alternan por ruido) con su normal, que coge la luz del sol, y la sombra del sol y la del relieve. La malla es
// 1,8 veces el camino: el borde visible lo dibuja una máscara con ruido a dos escalas (±20 % y algún ensanche) y fuera queda un halo de terreno
// pisado. Se desvanece junto al agua salvo en un trecho de vado, donde sigue como una traza mojada entre los guijarros. Lejos, la cinta se ensancha
// en el vértice para no bajar de `_MinPx` píxeles y gana algo de contraste. Los parches (UV1.w = 1) son discos de tierra apisonada en los cruces
// y los extremos. Encima, la niebla de guerra, las nubes y la bruma de `MapaComun.hlsl`.
//   UV0: x de un borde de la malla al otro, y lo andado / periodo. UV1: x sombra del relieve, y seco (1) o agua (0), z vado, w parche.
//   UV2.x: medio ancho lógico (m). COLOR: r arena, g roca, b pradera. NORMAL: la del suelo. TANGENT: hacia donde crece UV0.x (mundo), w lo que
//   se aparta el vértice del eje por ahí (m).
Shader "Conquest/MapaCaminoMejorado"
{
    Properties
    {
        _DetalleA ("Detalle A (R alto, G hierba, B guijarros, A oclusión)", 2D) = "gray" {}
        _DetalleB ("Detalle B", 2D) = "gray" {}
        _NormalA ("Normal A", 2D) = "bump" {}
        _NormalB ("Normal B", 2D) = "bump" {}
        _SueloColor ("Color de las capas del suelo", 2DArray) = "" {}
        _SueloRuido ("Ruido suave (4 canales)", 2D) = "gray" {}
        _Escalas ("Metros por repetición: cerca, lejos; distancia del cambio (m)", Vector) = (48, 200, 220, 900)
        _MinPx ("Ancho mínimo (píxeles)", Float) = 2.5
        _Aplastada ("Hierba aplastada a los lados (1 el sendero, 0 el camino)", Float) = 1
        _SobreAgua ("La copia de encima del agua de un vado (solo lo del vado)", Float) = 0
        _Desplazar ("Adelanto en el z-buffer", Float) = -2
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry+460" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            Offset [_Desplazar], [_Desplazar]
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include "MapaComun.hlsl"

            #define PASTO_SECO 0
            #define PASTO_VERDE 1
            #define TIERRA 2
            #define ARENA 3
            #define GRAVA 4

            TEXTURE2D(_DetalleA); SAMPLER(sampler_DetalleA);
            TEXTURE2D(_DetalleB);
            TEXTURE2D(_NormalA);
            TEXTURE2D(_NormalB);
            TEXTURE2D_ARRAY(_SueloColor); SAMPLER(sampler_SueloColor);
            TEXTURE2D(_SueloRuido); SAMPLER(sampler_SueloRuido);
            CBUFFER_START(UnityPerMaterial)
                float4 _Escalas;
                float _MinPx, _Desplazar, _Aplastada, _SobreAgua;
            CBUFFER_END

            struct Entrada
            {
                float4 pos : POSITION; float3 normal : NORMAL; float4 tangente : TANGENT; float4 color : COLOR;
                float2 uv : TEXCOORD0; float4 extra : TEXCOORD1; float2 medio : TEXCOORD2;
            };
            struct Salida
            {
                float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 mundo : TEXCOORD1; float4 extra : TEXCOORD2; float3 normal : TEXCOORD3;
                float3 tangente : TEXCOORD4; float4 color : COLOR;
            };

            Salida Vert(Entrada e)
            {
                Salida s;
                float3 mundo = TransformObjectToWorld(e.pos.xyz);
                float3 T = normalize(TransformObjectToWorldDir(e.tangente.xyz));
                // Lejos, que no baje de `_MinPx` píxeles de ancho: la cinta (y el parche) se ensancha desde su eje.
                float3 eje = mundo - T * e.tangente.w;
                float pixel = 2 * distance(GetCameraPositionWS(), eje) / (_ScreenParams.y * UNITY_MATRIX_P._m11);
                float f = max(1, _MinPx * pixel / max(2 * e.medio.x, 0.01));
                s.mundo = eje + T * e.tangente.w * f;
                s.pos = TransformWorldToHClip(s.mundo);
                s.uv = e.uv;
                s.extra = e.extra;
                s.normal = TransformObjectToWorldNormal(e.normal);
                s.tangente = T;
                s.color = e.color;
                return s;
            }

            // El color medio de una capa (su mip más pequeño).
            float3 Media(int capa) { return SAMPLE_TEXTURE2D_ARRAY_LOD(_SueloColor, sampler_SueloColor, float2(0.5, 0.5), capa, 12).rgb; }

            // Una capa del suelo, desde arriba, a la escala de cerca y a la de lejos (como `MapaSuelo`).
            float3 Capa(int capa, float3 p, float lejos)
            {
                float3 cerca = SAMPLE_TEXTURE2D_ARRAY(_SueloColor, sampler_SueloColor, p.xz / _Escalas.x, capa).rgb;
                float3 lejana = SAMPLE_TEXTURE2D_ARRAY(_SueloColor, sampler_SueloColor, (p.xz + float2(311, 173)) / _Escalas.y, capa).rgb;
                return lerp(cerca, lejana, lejos);
            }

            half4 Frag(Salida s) : SV_Target
            {
                float3 p = s.mundo;
                float2 pm = float2(p.x, -p.z);
                float distancia = distance(GetCameraPositionWS(), p);
                float lejos = smoothstep(_Escalas.z, _Escalas.w, distancia);
                bool parche = s.extra.w > 0.5;
                float a = abs(s.uv.x - 0.5) * 2; // 0 en el eje, 1 en el borde de la malla
                // El borde visible: el ancho lógico (1 / 1,8 de la malla) ±20 % con ruido a dos escalas y, de vez en cuando, un ensanche.
                float4 r1 = SAMPLE_TEXTURE2D(_SueloRuido, sampler_SueloRuido, pm / 37.0);
                float4 r2 = SAMPLE_TEXTURE2D(_SueloRuido, sampler_SueloRuido, pm / 9.0 + 0.31);
                float4 r3 = SAMPLE_TEXTURE2D(_SueloRuido, sampler_SueloRuido, pm / 70.0 + 0.63);
                const float e0 = 1.0 / 1.8;
                float borde = e0 * (1 + 0.10 * (r1.x - 0.5) * 2 + 0.06 * (r2.y - 0.5) * 2) + e0 * 0.12 * smoothstep(0.6, 0.75, r3.z);
                float nucleo = 1 - smoothstep(borde - 0.08, borde + 0.05, a);
                float halo = 1 - smoothstep(borde, 1.0, a);

                // El detalle: dos variantes que alternan por ruido a lo largo del mundo. El sendero, además, serpentea con ruido en espacio de mundo
                // (no periódico): lo horneado lleva poco serpenteo.
                float4 d;
                float3 nt;
                float2 uvd = s.uv;
                if (_Aplastada > 0.5)
                    uvd.x += (SAMPLE_TEXTURE2D(_SueloRuido, sampler_SueloRuido, pm / 55.0 + 0.77).z - 0.5) * 0.22
                           + (SAMPLE_TEXTURE2D(_SueloRuido, sampler_SueloRuido, pm / 19.0 + 0.29).w - 0.5) * 0.08;
                if (parche)
                {
                    d = float4(0.6, 0, smoothstep(0.62, 0.8, r2.z) * 0.6, 1);
                    nt = float3(0, 0, 1);
                }
                else
                {
                    float var = smoothstep(0.42, 0.58, SAMPLE_TEXTURE2D(_SueloRuido, sampler_SueloRuido, pm / 140.0 + 0.17).w);
                    d = lerp(SAMPLE_TEXTURE2D(_DetalleA, sampler_DetalleA, uvd), SAMPLE_TEXTURE2D(_DetalleB, sampler_DetalleA, uvd), var);
                    nt = lerp(SAMPLE_TEXTURE2D(_NormalA, sampler_DetalleA, uvd).xyz, SAMPLE_TEXTURE2D(_NormalB, sampler_DetalleA, uvd).xyz, var) * 2 - 1;
                }

                // El color: tierra pisada, más clara y seca que la recién removida, respecto al suelo de alrededor (el color medio de sus capas;
                // en lineal, ×1,23 es un 10 % más claro a la vista, ×0,70 un 15 % más oscuro y ×0,83 un 8 % más oscuro):
                //   pradera, marrón cálido un 15 % más oscuro que la hierba; tierra seca, ocre polvoriento un 10 % más claro; arena, arena compactada un
                //   8 % más oscura; roca, una traza gris clara. (Los tonos de partida, 24° y 30°, salen en pantalla hacia 35° y 38° con la luz.)
                // De la textura de tierra, solo su luminancia (el detalle), no su tono, que tiraba a morado.
                const float3 L = float3(0.2126, 0.7152, 0.0722);
                float arena = s.color.r, roca = s.color.g, verde = s.color.b;
                float3 cTierra = Capa(TIERRA, p, lejos), cGrava = Capa(GRAVA, p, lejos), cSeco = Capa(PASTO_SECO, p, lejos);
                float3 mTierra = Media(TIERRA), mSeco = Media(PASTO_SECO), mVerde = Media(PASTO_VERDE), mArena = Media(ARENA), mGrava = Media(GRAVA);
                // (ronda 3) El color sale del suelo de alrededor (su color medio, en sRGB): el tono desplazado hasta 6° hacia el rojo (en pradera,
                // 35°), la saturación al 85 % de la suya (en pradera 0,44; en roca, el 60 %) y el brillo de la luminancia un 8 % por encima (pradera
                // +5 %, arena −8 %, roca +5 %).
                float3 entorno = lerp(lerp(mSeco, mVerde, verde), mArena, arena);
                entorno = lerp(entorno, mGrava, roca);
                float3 hsvE = RgbAHsv(pow(max(entorno, 1e-5), 1 / 2.2));
                float tonoSeco = clamp(hsvE.x * 360 - 6, 30, 45);
                float tono = lerp(lerp(tonoSeco, 35, verde), hsvE.x * 360 - 3, arena);
                tono = lerp(tono, hsvE.x * 360, roca);
                float sat = lerp(lerp(max(0.85 * hsvE.y, 0.3), 0.44, verde), 0.9 * hsvE.y, arena);
                sat = lerp(sat, 0.6 * hsvE.y, roca);
                float lumE = dot(pow(max(entorno, 1e-5), 1 / 2.2), L);
                float lumObj = lumE * lerp(lerp(lerp(1.08, 0.97, verde), 1.02, arena), 1.05, roca);
                float3 tonoRgb = HsvARgb(float3(tono / 360, saturate(sat), 1));
                float3 objetivo = pow(tonoRgb * (lumObj / max(dot(tonoRgb, L), 1e-3)), 2.2);
                float detalleT = clamp(dot(cTierra, L) / max(dot(mTierra, L), 0.01), 0.6, 1.4);
                float3 suelo = objetivo * lerp(1, detalleT, 0.6);
                // Las rodadas, algo más oscuras (más hondas y húmedas); los guijarros, grava agrisada.
                float hondura = saturate((0.556 - d.r) / 0.25);
                float3 col = suelo * lerp(1.0, 0.89, hondura);
                col = lerp(col, lerp(cGrava, dot(cGrava, L).xxx, 0.5) * 1.15, saturate(d.b * 1.1) * 0.8);
                // A media distancia el detalle se emborrona en el mip: las rodadas (a 0,22 de la malla del eje, rotas a trechos) y los guijarros
                // sueltos, también calculados aquí, para que a zoom habitual se siga leyendo un camino de tierra.
                float media = smoothstep(30.0, 140.0, distancia) * (1 - _Aplastada);
                float rodadaA = exp(-pow((a - 0.22) / 0.055, 2)) * smoothstep(0.35, 0.55, SAMPLE_TEXTURE2D(_SueloRuido, sampler_SueloRuido, pm / 6.0 + 0.21).z);
                col *= 1 - 0.11 * rodadaA * media;
                float guij = smoothstep(0.72, 0.8, SAMPLE_TEXTURE2D(_SueloRuido, sampler_SueloRuido, pm / 0.9 + 0.57).y);
                col = lerp(col, lerp(cGrava, dot(cGrava, L).xxx, 0.4) * 1.1, guij * 0.35 * media * (1 - smoothstep(0.1, 0.18, abs(a - 0.11))));
                // La hierba aplastada del sendero (G 0,5): pasto seco tumbado, amarillento; la entera (G 1) deja ver el suelo de verdad.
                // (la hierba aplastada del sendero, del color del camino hacia el pasto seco: sin filo granate)
                float3 aplastada = lerp(objetivo, cSeco * float3(1.08, 1.04, 0.86), 0.55);
                float aplast = saturate(1 - abs(d.g - 0.5) * 3.5) * _Aplastada;
                col = lerp(col, aplastada, aplast * (1 - roca));
                col *= lerp(1, d.a, lerp(0.5, 0.25, _Aplastada));
                // La hierba entera (G 1) deja ver el suelo de verdad, salvo en el lomo del camino: ahí nunca se transparenta (salía una línea clara
                // recta, el suelo asomando); son matas irregulares y a trechos (ruido fino en espacio de mundo) que pinta el camino, verdes en la
                // pradera y secas en la tierra seca, y tierra entre ellas; en la arena y la roca, ninguna.
                float hierbaG = smoothstep(0.62, 0.95, d.g);
                float lomo = (1 - _Aplastada) * (1 - smoothstep(0.14, 0.22, a));
                float matas = smoothstep(0.52, 0.66, SAMPLE_TEXTURE2D(_SueloRuido, sampler_SueloRuido, pm / 1.7 + 0.13).x * 0.6
                                                   + SAMPLE_TEXTURE2D(_SueloRuido, sampler_SueloRuido, pm / 0.6 + 0.41).y * 0.4) * (1 - roca) * (1 - arena);
                float3 colMata = lerp(cSeco, Capa(PASTO_VERDE, p, lejos), verde) * 0.82;
                col = lerp(col, colMata, hierbaG * lomo * matas);
                hierbaG *= 1 - lomo;
                float cubre = nucleo * (1 - hierbaG);
                // En la roca, apenas una traza gastada; lejos, algo más de contraste y que no se pierda entre la hierba.
                cubre *= lerp(1, 0.5, roca);
                col *= lerp(1, 0.86, lejos);
                cubre = max(cubre, nucleo * lejos * 0.85 * lerp(1, 0.5, roca));
                // El halo: terreno pisado alrededor (menos en la arena y la roca).
                float3 colHalo = aplastada * 0.97;
                float pisado = halo * (1 - nucleo) * 0.3 * (1 - arena * 0.7) * (1 - roca * 0.7) * lerp(0.6, 1, verde);
                // El agua: nada sobre ella salvo en el trecho de vado, donde sigue como traza mojada entre los guijarros.
                float vado = s.extra.z, seco = s.extra.y;
                // Sobre el vado, una rodada mojada (nada de calzada): los guijarros de la barra algo más oscuros y aplastados a lo largo del paso
                // (sin el relieve del detalle, que leía como adoquines), el agua por encima en torno a la mitad (su tinte y sus rizos) y los
                // bordes deshechos; no más ancha que el camino.
                float3 mojada = lerp(dot(cGrava, L).xxx, cGrava, 0.6) * 0.62;
                col = lerp(col, mojada * lerp(0.9, 1.1, r2.x), vado);
                col = lerp(col, float3(0.30, 0.48, 0.47) * dot(mojada, L) * 1.6, vado * 0.35);
                float rizos = smoothstep(0.35, 0.65, SAMPLE_TEXTURE2D(_SueloRuido, sampler_SueloRuido, pm / 1.3 + _Time.y * float2(0.02, 0.05)).y);
                float deshecho = smoothstep(0.3, 0.55, r2.w * 0.6 + SAMPLE_TEXTURE2D(_SueloRuido, sampler_SueloRuido, pm / 0.8 + 0.5).x * 0.4);
                cubre *= seco * lerp(1, 0.55 * lerp(0.7, 1, rizos) * deshecho, vado);
                pisado *= seco * (1 - vado);
                nt = lerp(nt, float3(0, 0, 1), vado);
                if (_SobreAgua > 0.5) { cubre *= vado; pisado = 0; }

                // La luz: la normal del detalle (x a lo ancho, y a lo largo) sobre la del suelo; la sombra del sol y la del relieve.
                float3 N = normalize(s.normal), T = normalize(s.tangente), B = normalize(cross(N, T));
                // (la normal, suave: con más fuerza, las paredes de las rodadas que miran al sol salían en dos rayas claras)
                nt.xy *= 0.55;
                float3 Nl = normalize(lerp(N, normalize(T * nt.x + B * nt.y + N * nt.z), cubre > 0.01 ? 1 : 0));
                Light sol = GetMainLight();
                float l = saturate(dot(Nl, sol.direction));
                float sombra = ConRelieve(SombraDelSol(p), s.extra.x);
                float3 luz = LuzDelMapa(Nl.y, 0.6, 0.45 * l, sombra, 0.36);
                float3 luzSuelo = LuzDelMapa(N.y, 0.6, 0.45 * saturate(dot(N, sol.direction)), sombra, 0.36);
                // (lo que cubre el camino y el halo, cada uno con su luz)
                float alfa = cubre + pisado * (1 - cubre);
                float3 c = (col * luz * cubre + colHalo * luzSuelo * pisado * (1 - cubre)) / max(alfa, 1e-4);
                return half4(PintarMapa(c, p, true), saturate(alfa));
            }
            ENDHLSL
        }
    }
}
