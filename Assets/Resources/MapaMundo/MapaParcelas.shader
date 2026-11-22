// Los campos de una aldea agrícola «Mejorado» (tema 6, parte 4, etapa B; `MaquetasDelMapa` y `ParcelasDeAldea`): un disco pegado a la malla de
// suelo pintada, como el suelo pisado de las miniaturas, que lee el mapa de casillas de su aldea (`_Celdas`, una capa por aldea; por casilla:
// R el id de la parcela, G su estado ×64 + 32 si su hilera va de este a oeste, B su tono, A si hay parcela) y pinta:
//   - cada parcela con el color de su cultivo y su estado (`_Paleta`: 6 cultivos × 4 estados), con su tono, mezclado con la tierra del suelo;
//   - los surcos, una textura fina a lo largo de la hilera (3 o 4 por casilla), suavizados con fwidth; en la vid, las cepas; en el sésamo, las
//     hileras verdes sobre la arena, mitad y mitad; en la ganadera, manchas de pasto;
//   - la linde, una franja de hierba de 0,02 u solo entre parcelas;
//   - el borde de fuera fundido con el terreno en un tercio de casilla.
// uv0: el marco local del núcleo (x, −z del modelo) sobre [−_Lado, _Lado]; uv1: (capa, giro de la rejilla); uv2: (origen de la rejilla, cultivo).
// Encima del suelo y debajo del suelo pisado del núcleo y de los caminos (cola Geometry+454), sin escribir profundidad.
Shader "Conquest/MapaParcelas"
{
    Properties
    {
        _SueloColor ("Color de las capas del suelo", 2DArray) = "" {}
        _SueloRuido ("Ruido suave (4 canales)", 2D) = "gray" {}
        _Celdas ("El mapa de casillas de cada aldea", 2DArray) = "" {}
        [Toggle] _ConNiebla ("Bajo la niebla", Float) = 0
        _Escalas ("Metros por repetición: cerca, lejos; distancia del cambio (m)", Vector) = (48, 200, 220, 900)
        _Lado ("Lado del marco (unidades del modelo)", Float) = 2.15
        _Celda ("Lado de la casilla (unidades del modelo)", Float) = 0.12
        _Maximo ("Casillas por lado del mapa", Float) = 36
        _Tierra ("Cuánto de la tierra del suelo entra en el color del campo", Range(0, 1)) = 0.18
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry+454" }
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
            #define TIERRA 2

            TEXTURE2D_ARRAY(_SueloColor); SAMPLER(sampler_SueloColor);
            TEXTURE2D(_SueloRuido); SAMPLER(sampler_SueloRuido);
            TEXTURE2D_ARRAY(_Celdas); SAMPLER(sampler_point_clamp);
            float4 _Paleta[24];
            CBUFFER_START(UnityPerMaterial)
                float4 _Escalas;
                float _ConNiebla, _Lado, _Celda, _Maximo, _Tierra;
            CBUFFER_END

            struct Entrada { float4 pos : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; float2 capa : TEXCOORD1; float2 rejilla : TEXCOORD2; };
            struct Salida { float4 pos : SV_POSITION; float3 normal : TEXCOORD0; float3 mundo : TEXCOORD1; float2 uv : TEXCOORD2; float2 capa : TEXCOORD3; float2 rejilla : TEXCOORD4; };

            Salida Vert(Entrada e)
            {
                Salida s;
                s.mundo = TransformObjectToWorld(e.pos.xyz);
                s.pos = TransformWorldToHClip(s.mundo);
                s.normal = TransformObjectToWorldNormal(e.normal);
                s.uv = e.uv;
                s.capa = e.capa;
                s.rejilla = e.rejilla;
                return s;
            }

            // la casilla (i, j) de la capa: (id, estado ×64 + orientación, tono, hay)
            float4 Casilla(float2 ij, float capa)
            {
                if (ij.x < 0 || ij.y < 0 || ij.x >= _Maximo || ij.y >= _Maximo) return 0;
                return SAMPLE_TEXTURE2D_ARRAY_LOD(_Celdas, sampler_point_clamp, (ij + 0.5) / _Maximo, capa, 0);
            }

            // una raya cada 1/k a lo largo de `t`, de ancho `a` (fracción), suavizada a un píxel
            float Raya(float t, float k, float a)
            {
                float f = frac(t * k), w = max(fwidth(t * k), 1e-4);
                return smoothstep(a + w, a, f) * smoothstep(-w, 0, f);
            }

            half4 Frag(Salida s) : SV_Target
            {
                Light sol = GetMainLight();
                float3 n = normalize(s.normal);
                float3 p = s.mundo;
                float lejos = smoothstep(_Escalas.z, _Escalas.w, distance(p, _WorldSpaceCameraPos));
                float3 tierra = lerp(SAMPLE_TEXTURE2D_ARRAY(_SueloColor, sampler_SueloColor, p.xz / _Escalas.x, TIERRA).rgb,
                                     SAMPLE_TEXTURE2D_ARRAY(_SueloColor, sampler_SueloColor, (p.xz + float2(311, 173)) / _Escalas.y, TIERRA).rgb, lejos);
                // las sendas (uv1.x = −1): tierra desnuda (#a88f69 mezclado con la del suelo), con los bordes suaves
                if (s.capa.x < -0.5)
                {
                    float a = smoothstep(0, 0.3, s.uv.x) * smoothstep(1, 0.7, s.uv.x);
                    float3 cs = lerp(float3(0.39, 0.27, 0.14), tierra * 0.85, 0.35);
                    cs *= LuzDelMapa(n.y, 0.6, 0.45 * saturate(dot(n, sol.direction)), SombraDelSol(p), 0.36);
                    return half4(PintarMapa(cs, p, _ConNiebla > 0.5), a * 0.95);
                }
                float capa = s.capa.x, g = s.capa.y, o = s.rejilla.x;
                int cultivo = (int)round(s.rejilla.y);
                // del marco local a la rejilla girada
                float2 l = (s.uv * 2 - 1) * _Lado;
                float2 q = float2(l.x * cos(g) + l.y * sin(g), -l.x * sin(g) + l.y * cos(g));
                float2 fij = (q - o) / _Celda;
                float2 ij = floor(fij), f = fij - ij;
                float4 c = Casilla(ij, capa);
                if (c.a < 0.5) discard;
                float id = c.r * 255, gg = round(c.g * 255);
                int estado = (int)(gg / 64);
                bool horizontal = fmod(gg, 64) >= 32;
                float tono = 0.92 + c.b * 0.16;
                float3 col = _Paleta[cultivo * 4 + estado].rgb * tono;
                col = lerp(col, col * tierra / max(dot(tierra, 1.0 / 3), 1e-3), _Tierra);
                // los surcos, a lo largo de la hilera (la coordenada de través)
                float t = horizontal ? fij.y : fij.x;
                float3 c2 = col;
                if (cultivo == 5)
                {
                    float2 pm = p.xz / max(_Bruma.w, 1);
                    float mancha = 0.93 + 0.09 * sin(pm.x * 11 + id) * sin(pm.y * 13 - id);
                    c2 = col * mancha;
                }
                else if (cultivo == 2)
                {
                    c2 = lerp(col, col * 0.86, Raya(t, 3.5, 0.3));
                    if (estado != 3) c2 = lerp(c2, float3(0.21, 0.34, 0.085), Raya(t + 0.125 / 2, 2.0, 0.13));
                }
                else if (cultivo == 4 && estado != 3)
                {
                    // el sésamo, solo verde o en barbecho: hileras de #8fae5a sobre la arena #d9c79a (el color de la paleta), mitad y mitad
                    c2 = lerp(col, float3(0.275, 0.423, 0.102) * tono, Raya(t, 3.0, 0.5));
                }
                else c2 = lerp(col, col * (estado == 3 ? 0.85 : 0.88), Raya(t, estado == 3 ? 4.0 : 3.5, estado == 3 ? 0.4 : 0.3));
                // la linde: donde la parcela vecina es otra, una franja de hierba (media a cada lado)
                float medio = 0.02 / 0.9 / 2 / _Celda;
                float linde = 0, borde = 1;
                float2 lados[4] = { float2(1, 0), float2(-1, 0), float2(0, 1), float2(0, -1) };
                float dist[4] = { 1 - f.x, f.x, 1 - f.y, f.y };
                [unroll] for (int k = 0; k < 4; k++)
                {
                    float4 v = Casilla(ij + lados[k], capa);
                    if (v.a > 0.5 && abs(v.r * 255 - id) > 0.5) linde = max(linde, smoothstep(medio + fwidth(f.x), medio, dist[k]));
                    // el borde de fuera, fundido en un tercio de casilla hacia la casilla sin parcela
                    if (v.a < 0.5) borde = min(borde, saturate(dist[k] * 3));
                }
                c2 = lerp(c2, float3(0.215, 0.25, 0.085), linde);
                c2 *= LuzDelMapa(n.y, 0.6, 0.45 * saturate(dot(n, sol.direction)), SombraDelSol(p), 0.36);
                return half4(PintarMapa(c2, p, _ConNiebla > 0.5), borde * 0.95);
            }
            ENDHLSL
        }
    }
}
