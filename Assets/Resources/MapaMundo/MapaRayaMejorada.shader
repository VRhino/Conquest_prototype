// Las líneas del territorio del mapa de mundo, estilo «Mejorado» (`TerritorioDelMapa.Estilo`): las medidas del prototipo (ancho, trazos de 1,2 u
// y huecos de 0,7 u), pero cada trazo con los extremos redondeados (una cápsula), un perfil oscuro muy fino alrededor para que contraste sobre
// cualquier suelo y, lejos, un ancho mínimo en píxeles (la cinta se ensancha en el vértice). Color plano, con su opacidad en el alfa del color
// de vértice; encima, la niebla de guerra (si `_ConNiebla`), las nubes y la bruma de `MapaComun.hlsl`.
//   UV0: x de un borde de la malla al otro (−1..1), y lo andado (u). UV1.x: 1 a trazos. UV2.x: medio ancho de la línea (u), UV2.y: el medio
//   ancho de la malla (u). TANGENT: hacia donde crece UV0.x (mundo), w lo que se aparta el vértice del eje (m). La ruta de las columnas
//   (`RutasDelMapa`) usa el mismo sombreado con sus trazos (`_Lleno`, `_Periodo`).
Shader "Conquest/MapaRayaMejorada"
{
    Properties
    {
        _ConNiebla ("Bajo la niebla de guerra", Float) = 1
        _Desplazar ("Adelanto en el z-buffer", Float) = -3
        _MinPx ("Ancho mínimo de la línea (píxeles)", Float) = 2
        _Lleno ("A trazos: lo lleno (u)", Float) = 1.2
        _Periodo ("A trazos: lleno más hueco (u)", Float) = 1.9
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
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "MapaComun.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _ConNiebla, _Desplazar, _MinPx, _Lleno, _Periodo;
            CBUFFER_END

            struct Entrada { float4 pos : POSITION; float4 tangente : TANGENT; float4 color : COLOR; float2 uv : TEXCOORD0; float2 extra : TEXCOORD1; float2 medio : TEXCOORD2; };
            struct Salida { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 mundo : TEXCOORD1; float4 datos : TEXCOORD2; float4 color : COLOR; };

            Salida Vert(Entrada e)
            {
                Salida s;
                float3 mundo = TransformObjectToWorld(e.pos.xyz);
                float3 T = normalize(TransformObjectToWorldDir(e.tangente.xyz));
                float3 eje = mundo - T * e.tangente.w;
                float mpu = max(_Bruma.w, 1);
                float pixel = 2 * distance(GetCameraPositionWS(), eje) / (_ScreenParams.y * UNITY_MATRIX_P._m11);
                // lejos, la línea (no el perfil) no baja de `_MinPx` píxeles
                float f = max(1, _MinPx * pixel / max(2 * e.medio.x * mpu, 0.001));
                s.mundo = eje + T * e.tangente.w * f;
                s.pos = TransformWorldToHClip(s.mundo);
                s.uv = e.uv;
                s.datos = float4(e.medio.x * f, e.medio.y * f, e.extra.x, f);
                s.color = e.color;
                return s;
            }

            half4 Frag(Salida s) : SV_Target
            {
                float w = s.datos.x;                 // medio ancho de la línea (u), ya ensanchado
                float a = s.uv.x * s.datos.y;        // lo que se aparta del eje (u)
                float d = abs(a);
                if (s.datos.z > 0.5)
                {
                    // a trazos: la distancia a la cápsula del trazo más cercano (`_Lleno` y `_Periodo`: 1,2 u y 1,9 u el territorio, extremos redondos de radio w)
                    float lleno = _Lleno, periodo = _Periodo;
                    float fase = s.uv.y - floor(s.uv.y / periodo) * periodo;
                    float r = min(w, lleno / 2);
                    float t = clamp(fase, r, lleno - r);
                    // el trazo de este periodo, el siguiente (empieza en periodo + r) y el anterior (acaba en lleno − r − periodo)
                    d = min(length(float2(fase - t, a)), min(length(float2(periodo + r - fase, a)), length(float2(fase - (lleno - r - periodo), a))));
                }
                float aa = max(fwidth(d), 1e-4);
                float perfil = max(w * 0.35, aa * 1.2);
                float linea = 1 - smoothstep(w - aa, w + aa, d);
                float todo = 1 - smoothstep(w + perfil - aa, w + perfil + aa, d);
                float3 col = lerp(s.color.rgb * 0.18, s.color.rgb, linea);
                float alfa = s.color.a * max(linea, todo * 0.75);
                return half4(PintarMapa(col, s.mundo, _ConNiebla > 0.5), alfa);
            }
            ENDHLSL
        }
    }
}
