// El polvo ocre a los pies de lo que anda (tema 9, `PolvoDelMapa`): cada bocanada, un cuadrado de cara a la cámara que nace en el suelo,
// crece, sube un poco y se apaga en `PolvoDelMapa.Vida` s. `_Polvo`, dos float4 por bocanada: el sitio (m) y el tamaño (m); cuándo nació
// (s, `_RelojMapa`), cuánto polvo da el suelo (0..1) y su giro. Poca opacidad y baja, para no tapar las figuras; lo apaga la niebla
// como al suelo.
Shader "Conquest/MapaPolvo"
{
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
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

            StructuredBuffer<float4> _Polvo;
            static const float Vida = 1.4;

            struct Entrada { float4 pos : POSITION; float2 uv : TEXCOORD0; };
            struct Salida { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 mundo : TEXCOORD1; float alfa : TEXCOORD2; };

            Salida Vert(Entrada e, uint id : SV_InstanceID)
            {
                float4 a0 = _Polvo[2 * id], a1 = _Polvo[2 * id + 1];
                float edad = (_RelojMapa - a1.x) / Vida;
                Salida s;
                s.uv = e.uv;
                bool viva = edad >= 0 && edad < 1;
                float lado = a0.w * (0.6 + 0.9 * edad);
                // de cara a la cámara, sin inclinarse: ancha y baja (no pasa de la rodilla de las figuras), polvo que se arrastra
                float3 derecha = normalize(float3(UNITY_MATRIX_V[0].x, 0, UNITY_MATRIX_V[0].z) + 1e-5);
                float3 centro = a0.xyz + float3(0, a0.w * 0.08 * edad, 0);
                s.mundo = centro + derecha * e.pos.x * lado * 1.6 + float3(0, e.pos.y * lado * 0.3, 0);
                s.pos = viva ? TransformWorldToHClip(s.mundo) : float4(0, 0, -1, 1);
                s.alfa = viva ? 1.0 * a1.y * smoothstep(0, 0.12, edad) * (1 - edad) * (1 - edad) : 0;
                return s;
            }

            half4 Frag(Salida s) : SV_Target
            {
                float2 d = (s.uv - float2(0.5, 0.3)) * float2(1, 1.2);
                float bola = saturate(1 - dot(d, d) * 4);
                float3 c = SRGBToLinear(float3(0.88, 0.8, 0.64));
                return half4(PintarMapa(c, s.mundo, true), s.alfa * bola * bola);
            }
            ENDHLSL
        }
    }
}
