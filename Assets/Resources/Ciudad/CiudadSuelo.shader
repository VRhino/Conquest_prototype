// El suelo de la ciudad por dentro: las mismas capas del suelo del mapa de mundo (`Resources/MapaMundo/Suelo`, Texture2DArray: 0 pasto seco,
// 1 pasto verde, 2 tierra, 3 arena, 4 grava...), en espacio de mundo. Lo que pasa en cada sitio lo dice `_Pisado`, una máscara que se pinta
// desde la planta: R la tierra pisada (las calles, el pie de cada casa, las sendas), G lo hundido (las rodadas de los carros, la huella de
// las sendas: más oscuro y húmedo, con su relieve) y B el lomo con hierba entre las rodadas. Entre el pasto y la tierra, mezcla por la
// altura de la textura (el borde sigue las matas); la tierra lleva piedras sueltas de la grava. El pasto, verde o seco a manchas grandes. Luz de URP
// completa (PBR, sombras, SSAO, bruma, decals).
Shader "Conquest/CiudadSuelo"
{
    Properties
    {
        _SueloColor ("Color de las capas", 2DArray) = "" {}
        _SueloNormal ("Normal de las capas", 2DArray) = "" {}
        _SueloMascara ("Máscara de las capas (R altura, G suavidad)", 2DArray) = "" {}
        _Pisado ("Planta (R pisado, G hundido, B lomo con hierba)", 2D) = "black" {}
        _Zona ("Zona de la máscara: centro x, centro z, lado (m)", Vector) = (0, 0, 100, 0)
        _Metros ("Metros por repetición", Float) = 5
        _Seco ("Cuánto pasto seco (0 verde, 1 seco)", Range(0, 1)) = 0.45
        _FuerzaNormal ("Fuerza de la normal", Float) = 0.9
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        TEXTURE2D_ARRAY(_SueloColor); SAMPLER(sampler_SueloColor);
        TEXTURE2D_ARRAY(_SueloNormal);
        TEXTURE2D_ARRAY(_SueloMascara);
        TEXTURE2D(_Pisado); SAMPLER(sampler_Pisado);
        float4 _Pisado_TexelSize;
        CBUFFER_START(UnityPerMaterial)
            float4 _Zona;
            float _Metros;
            float _Seco;
            float _FuerzaNormal;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"

            struct Entrada { float4 pos : POSITION; float3 normal : NORMAL; };
            struct Salida { float4 pos : SV_POSITION; float3 mundo : TEXCOORD0; float3 normal : TEXCOORD1; float niebla : TEXCOORD2; };

            Salida Vert(Entrada e)
            {
                Salida s;
                s.mundo = TransformObjectToWorld(e.pos.xyz);
                s.pos = TransformWorldToHClip(s.mundo);
                s.normal = TransformObjectToWorldNormal(e.normal);
                s.niebla = ComputeFogFactor(s.pos.z);
                return s;
            }

            // Ruido de valor suave, para las manchas grandes.
            float Azar(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float Ruido(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(Azar(i), Azar(i + float2(1, 0)), f.x), lerp(Azar(i + float2(0, 1)), Azar(i + float2(1, 1)), f.x), f.y);
            }

            void Capa(float2 uv, float capa, out float3 color, out float2 desvio, out float2 masc)
            {
                color = SAMPLE_TEXTURE2D_ARRAY(_SueloColor, sampler_SueloColor, uv, capa).rgb;
                desvio = SAMPLE_TEXTURE2D_ARRAY(_SueloNormal, sampler_SueloColor, uv, capa).xy * 2 - 1;
                masc = SAMPLE_TEXTURE2D_ARRAY(_SueloMascara, sampler_SueloColor, uv, capa).rg;
            }

            half4 Frag(Salida s) : SV_Target
            {
                float2 uv = s.mundo.xz / _Metros;
                float3 cv, cs, ct; float2 nv, ns, nt, mv, ms, mt;
                Capa(uv, 1, cv, nv, mv);
                Capa(uv * 0.93 + 0.37, 0, cs, ns, ms);
                Capa(uv * 1.07 + 0.11, 2, ct, nt, mt);

                // el pasto: verde o seco a manchas de unos 15 m, con el borde por la altura de la textura
                float mancha = Ruido(s.mundo.xz / 23) * 0.5 + Ruido(s.mundo.xz / 8.3 + 17) * 0.3 + Ruido(s.mundo.xz / 2.9 + 5) * 0.2;
                float seco = saturate((mancha + _Seco - 1 + (ms.x - mv.x) * 0.3) * 4 + 0.5);
                float3 c = lerp(cv, cs, seco);
                float2 n = lerp(nv, ns, seco);
                float2 m = lerp(mv, ms, seco);

                // la planta: la tierra pisada gana primero donde el pasto es más bajo; el lomo entre las rodadas conserva su hierba
                float2 uz = (s.mundo.xz - _Zona.xy) / _Zona.z + 0.5;
                float dentro = step(0, uz.x) * step(uz.x, 1) * step(0, uz.y) * step(uz.y, 1);
                float3 planta = SAMPLE_TEXTURE2D(_Pisado, sampler_Pisado, uz).rgb * dentro;
                float pisado = saturate(planta.r - planta.b * 0.55);
                pisado = saturate(pisado + (Ruido(s.mundo.xz / 2.3) - 0.5) * 0.25 * pisado * (1 - pisado) * 4);
                float tierra = saturate((pisado * 1.6 - m.x * 0.7 + mt.x * 0.25 - 0.35) * 5);
                // la grava: piedras sueltas en lo más pisado (solo lo alto de su textura: las piedras, no su suelo), teñidas del polvo de la tierra
                float3 cg; float2 ng, mg;
                Capa(uv * 2.1 + 0.53, 4, cg, ng, mg);
                float grava = saturate((Ruido(s.mundo.xz / 6.1 + 40) * 0.7 + Ruido(s.mundo.xz / 1.7) * 0.3 - 0.44) * 4) * saturate(planta.r * 2 - 1);
                grava = saturate((grava * 0.9 + mg.x * 1.6 - 1.6) * 6);
                cg = lerp(cg, ct * 1.3, 0.45);
                ct = lerp(ct, cg, grava);
                nt = lerp(nt, ng, grava);
                mt = lerp(mt, mg, grava);
                c = lerp(c, ct, tierra);
                n = lerp(n, nt, tierra);
                m = lerp(m, mt, tierra);
                // el pasto junto a lo pisado, algo más ralo y seco
                c = lerp(c, c * float3(1.04, 0.98, 0.86), saturate(pisado * 2) * (1 - tierra) * 0.6);
                // lo hundido: más oscuro y algo húmedo, y con la pendiente de sus bordes
                float hondo = planta.g * tierra;
                c *= 1 - 0.3 * hondo;
                float2 tx = float2(_Pisado_TexelSize.x * 1.5, 0), ty = float2(0, _Pisado_TexelSize.y * 1.5);
                float2 cuesta = float2(SAMPLE_TEXTURE2D(_Pisado, sampler_Pisado, uz + tx).g - SAMPLE_TEXTURE2D(_Pisado, sampler_Pisado, uz - tx).g,
                                       SAMPLE_TEXTURE2D(_Pisado, sampler_Pisado, uz + ty).g - SAMPLE_TEXTURE2D(_Pisado, sampler_Pisado, uz - ty).g) * dentro;
                n += cuesta * 0.9 * tierra;
                m.y = lerp(m.y, 0.55, hondo * 0.6);

                float3 N = normalize(s.normal);
                float3 normal = normalize(float3(n.x * _FuerzaNormal, 1, n.y * _FuerzaNormal) + N - float3(0, 1, 0));

                InputData d = (InputData)0;
                d.positionWS = s.mundo;
                d.positionCS = s.pos;
                d.normalWS = normal;
                d.viewDirectionWS = GetWorldSpaceNormalizeViewDir(s.mundo);
                d.shadowCoord = TransformWorldToShadowCoord(s.mundo);
                d.fogCoord = s.niebla;
                d.bakedGI = SampleSH(normal);
                d.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(s.pos);
                d.shadowMask = half4(1, 1, 1, 1);

                SurfaceData sup = (SurfaceData)0;
                sup.albedo = c;
                sup.alpha = 1;
                sup.smoothness = m.y * 0.5;
                sup.normalTS = float3(0, 0, 1);
                sup.occlusion = 1;
                #if defined(_DBUFFER)
                    ApplyDecalToSurfaceData(s.pos, sup, d);
                #endif

                half4 color = UniversalFragmentPBR(d, sup);
                color.rgb = MixFog(color.rgb, s.niebla);
                return half4(color.rgb, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            float4 Vert(float4 pos : POSITION) : SV_POSITION { return TransformObjectToHClip(pos.xyz); }
            half4 Frag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            struct Salida { float4 pos : SV_POSITION; float3 normal : TEXCOORD0; };
            Salida Vert(float4 pos : POSITION, float3 normal : NORMAL)
            {
                Salida s;
                s.pos = TransformObjectToHClip(pos.xyz);
                s.normal = TransformObjectToWorldNormal(normal);
                return s;
            }
            half4 Frag(Salida s) : SV_Target { return half4(normalize(s.normal), 0); }
            ENDHLSL
        }
    }
}
