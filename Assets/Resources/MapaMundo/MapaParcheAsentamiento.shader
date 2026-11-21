// El suelo pisado y las sendas de una miniatura de asentamiento «Mejorado» (`MaquetasDelMapa`): un disco pegado a la malla de suelo pintada,
// pintado con la tierra desnuda de ese mismo suelo (la capa de tierra de `MapaSuelo`, la de las calvas pardas, a su escala), un 15-20 % más
// oscura que ella (en lo seco, `_Seco`: más, porque la tierra se parece al pasto seco; cuánto es pradera lo trae el color de vértice) y con
// grava a manchas donde más se pisa (los patios): en la hierba, tierra parda con sendas; en lo seco, tierra más oscura y pedregosa. Dónde hay suelo pisado lo dice la máscara de la miniatura (`_Suelo`, R; uv: las coordenadas locales del modelo, ya
// giradas y a su escala), con el borde roto por ruido. Encima del suelo y debajo de los caminos (cola Geometry+455), sin escribir profundidad.
Shader "Conquest/MapaParcheAsentamiento"
{
    Properties
    {
        _SueloColor ("Color de las capas del suelo", 2DArray) = "" {}
        _SueloRuido ("Ruido suave (4 canales)", 2D) = "gray" {}
        _Suelo ("Máscara del suelo pisado (R)", 2D) = "black" {}
        [Toggle] _ConNiebla ("Bajo la niebla", Float) = 0
        _Seco ("Lo que queda de la tierra en lo seco (en la hierba, 0,82)", Range(0.3, 1)) = 0.62
        [Toggle] _Pintados ("La máscara trae huertos (G) y eras (B)", Float) = 0
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
            #define TIERRA 2
            #define GRAVA 4

            TEXTURE2D_ARRAY(_SueloColor); SAMPLER(sampler_SueloColor);
            TEXTURE2D(_SueloRuido); SAMPLER(sampler_SueloRuido);
            TEXTURE2D(_Suelo); SAMPLER(sampler_Suelo);
            CBUFFER_START(UnityPerMaterial)
                float4 _Escalas;
                float _ConNiebla;
                float _Seco;
                float _Pintados;
            CBUFFER_END

            // uv: en la máscara de la miniatura; color: nada (blanco)
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
                // la máscara, con el borde roto a pequeña escala; neto, sin halo
                float4 mk = SAMPLE_TEXTURE2D(_Suelo, sampler_Suelo, s.uv);
                float m = mk.r;
                // (los modelos que la traen) los huertos y las eras del borde, pintados en el suelo en vez de en la malla
                float huerto = _Pintados > 0.5 ? smoothstep(0.35, 0.6, mk.g) : 0;
                float era = _Pintados > 0.5 ? smoothstep(0.35, 0.6, mk.b) : 0;
                float alfa = smoothstep(0.3, 0.55, m + (r1.x - 0.5) * 0.35 + (r2.y - 0.5) * 0.2);
                alfa *= step(0.001, s.uv.x) * step(s.uv.x, 0.999) * step(0.001, s.uv.y) * step(s.uv.y, 0.999);
                // la tierra, un 15-20 % más oscura; en los patios, grava a manchas
                float grava = smoothstep(0.7, 0.95, m) * smoothstep(0.4, 0.62, r2.z * 0.6 + r1.w * 0.4);
                // (el color de vértice, r: cuánto es pradera verde; en lo seco la tierra se parece al pasto seco y se oscurece más)
                float oscuro = lerp(_Seco, 0.82, smoothstep(0.15, 0.5, s.color.r));
                float3 c = lerp(Capa(TIERRA, p, lejos), Capa(GRAVA, p, lejos) * 0.95, grava * 0.8) * oscuro;
                // el huerto: surcos de tierra oscura y verde; la era: un círculo claro de tierra batida
                float surco = step(0.5, frac((p.x + p.z) * 0.9));
                c = lerp(c, lerp(Capa(TIERRA, p, lejos) * 0.5, float3(0.1, 0.17, 0.05), surco), huerto);
                c = lerp(c, Capa(GRAVA, p, lejos) * 1.1 + 0.04, era);
                alfa = max(alfa, max(huerto, era));
                Light sol = GetMainLight();
                float3 n = normalize(s.normal);
                c *= LuzDelMapa(n.y, 0.6, 0.45 * saturate(dot(n, sol.direction)), SombraDelSol(p), 0.36);
                return half4(PintarMapa(c, p, _ConNiebla > 0.5), saturate(alfa) * 0.95);
            }
            ENDHLSL
        }
    }
}
