// El color plano del mapa de mundo (URP): color de vértice por el color del material, luz del sol con un poco de cielo, y la niebla de guerra
// encima con sus tres estados (nunca visto, recordado, a la vista), leída de la rejilla de la sesión. Lo propio (tu columna, la ruta y el
// destino) va con `_ConNiebla` = 0. Con instanciado: el color de cada caja va por instancia.
Shader "Conquest/MapaColor"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _ConNiebla ("Con niebla", Float) = 1
        _Luz ("Con luz", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // La niebla, global para todo el mapa (la pone NieblaDelMapa): R = explorado, G = a la vista. `_NieblaRejilla`: x, z del origen
            // en el mundo, metros por celda, y si hay niebla (0: todo a la vista).
            TEXTURE2D(_NieblaTex); SAMPLER(sampler_NieblaTex);
            float4 _NieblaRejilla;
            float4 _NieblaTamano; // columnas, filas
            float4 _NieblaColor;  // rgb y, en a, nada
            float4 _NieblaAlfas;  // nunca visto, recordado, a la vista

            CBUFFER_START(UnityPerMaterial)
                float _ConNiebla;
                float _Luz;
            CBUFFER_END
            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Color)
            UNITY_INSTANCING_BUFFER_END(Props)

            struct Entrada { float4 pos : POSITION; float3 normal : NORMAL; float4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Salida { float4 pos : SV_POSITION; float3 normal : TEXCOORD0; float3 mundo : TEXCOORD1; float4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };

            Salida Vert(Entrada e)
            {
                Salida s;
                UNITY_SETUP_INSTANCE_ID(e);
                UNITY_TRANSFER_INSTANCE_ID(e, s);
                s.mundo = TransformObjectToWorld(e.pos.xyz);
                s.pos = TransformWorldToHClip(s.mundo);
                s.normal = TransformObjectToWorldNormal(e.normal);
                s.color = e.color * UNITY_ACCESS_INSTANCED_PROP(Props, _Color);
                return s;
            }

            half4 Frag(Salida s) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(s);
                float3 c = s.color.rgb;
                if (_Luz > 0.5)
                {
                    Light sol = GetMainLight();
                    float l = saturate(dot(normalize(s.normal), sol.direction));
                    c *= 0.6 + 0.45 * l;
                }
                if (_ConNiebla > 0.5 && _NieblaRejilla.w > 0.5)
                {
                    // El mapa va con z = −y: la fila crece hacia el sur, que es −z.
                    float2 celda = float2(s.mundo.x - _NieblaRejilla.x, _NieblaRejilla.y - s.mundo.z) / _NieblaRejilla.z;
                    float2 uv = (floor(celda) + 0.5) / _NieblaTamano.xy;
                    float2 n = SAMPLE_TEXTURE2D(_NieblaTex, sampler_NieblaTex, uv).rg;
                    float alfa = n.g > 0.5 ? _NieblaAlfas.z : (n.r > 0.5 ? _NieblaAlfas.y : _NieblaAlfas.x);
                    if (any(uv < 0) || any(uv > 1)) alfa = _NieblaAlfas.x;
                    c = lerp(c, _NieblaColor.rgb, alfa);
                }
                return half4(c, 1);
            }
            ENDHLSL
        }
    }
}
