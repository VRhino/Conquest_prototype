// La vegetación del mapa de mundo (URP), dibujada por instancias (`VegetacionDelMapa`): textura horneada (corteza, ramilletes, follaje de los
// grumos o copa de lejos) × color de vértice (la oclusión hacia dentro de la copa, horneada en Blender) × una variación de tono por instancia
// (sale de la posición del árbol: no hace falta dato por instancia). Mate, sin especular: luz envolvente con el sol del mapa (la misma dirección
// que el suelo) y algo de translucidez a contraluz con la G de la máscara (el grosor de la hoja). Las tarjetas de ramillete (`_Recorte` 1) van
// por las dos caras y recortan por alfa con alpha to coverage (con el MSAA 4×; sin MSAA queda el recorte). El viento mueve los vértices con el
// alfa del color de vértice (0 en el tronco, 1 en lo alto de la copa). Encima, las nubes, la niebla de guerra y la bruma de `MapaComun.hlsl`,
// las mismas que el suelo.
Shader "Conquest/MapaVegetacion"
{
    Properties
    {
        _MainTex ("Color (sRGB; con recorte, alfa)", 2D) = "white" {}
        _Normal ("Normal", 2D) = "bump" {}
        _Mascara ("Máscara (R oclusión, G grosor)", 2D) = "white" {}
        _Recorte ("Recorte por alfa (tarjetas)", Float) = 0
        _Translucidez ("Translucidez a contraluz", Float) = 0.3
        _FuerzaNormal ("Fuerza del mapa de normales", Float) = 0.6
        _Viento ("Viento (m en lo alto de la copa)", Float) = 0.12
        _Variacion ("Variación de tono (follaje 1, corteza poco)", Float) = 1
        _ConHondura ("Hondura del dosel (árboles: la base de la copa, más oscura en el bosque cerrado)", Float) = 0
        _Modo ("0 planta; 1 hierba (el tinte, del suelo de debajo); 2 flor (la cabeza, del color de su mancha)", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Caras", Float) = 2
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry+10" }
        HLSLINCLUDE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "MapaComun.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_Normal); SAMPLER(sampler_Normal);
            TEXTURE2D(_Mascara); SAMPLER(sampler_Mascara);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _Recorte, _Translucidez, _FuerzaNormal, _Viento, _Cull, _Variacion, _ConHondura, _Modo;
            CBUFFER_END

            // El viento: dos ondas lentas desfasadas por árbol y por altura, empujando hacia el este-sudeste (lo mismo en la sombra).
            float3 ConViento(float3 w, float azar, float peso)
            {
                float t = fmod(_Time.y, 3600.0), fase = azar * 6.2832;
                float empuje = (sin(t * 1.1 + fase) * 0.6 + sin(t * 2.3 + fase * 1.7 + w.y * 0.25) * 0.4) * _Viento * peso;
                return w + float3(0.93, 0, 0.37) * empuje;
            }
        ENDHLSL
        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]
            AlphaToMask On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            // La sombra del sol (URP, con cascadas y suave): `SombraDelSol` en MapaComun.hlsl.
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            // El aclarado (lo pone VegetacionDelMapa): el héroe y las columnas que se ven, en la escena, con el radio (m) en w.
            float4 _Aclarar[8];
            float _AclararCuantos;

            struct Entrada
            {
                float4 pos : POSITION; float3 normal : NORMAL; float4 tangente : TANGENT; float2 uv : TEXCOORD0; float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Salida
            {
                float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 normal : TEXCOORD1; float4 tangente : TEXCOORD2; float3 mundo : TEXCOORD3;
                float4 color : COLOR; float2 azar : TEXCOORD4; float4 suelo : TEXCOORD5; float flor : TEXCOORD6;
            };

            Salida Vert(Entrada e)
            {
                Salida s;
                UNITY_SETUP_INSTANCE_ID(e);
                float3 origen = TransformObjectToWorld(float3(0, 0, 0));
                float azar = frac(sin(dot(origen.xz, float2(12.9898, 78.233))) * 43758.5453);
                float3 w = ConViento(TransformObjectToWorld(e.pos.xyz), azar, e.color.a);
                w = origen + (w - origen) * NieblaCrecer(origen); // recogida en lo nunca explorado
                s.pos = TransformWorldToHClip(w);
                s.mundo = w;
                s.uv = TRANSFORM_TEX(e.uv, _MainTex);
                s.normal = TransformObjectToWorldNormal(e.normal);
                s.tangente = float4(TransformObjectToWorldDir(e.tangente.xyz), e.tangente.w * GetOddNegativeScale());
                s.color = e.color;
                // El rodal: manchas de 40 a 80 m que comparten tono (por la posición del árbol, no de la hoja: el árbol entero igual).
                s.azar = float2(azar, RuidoNube(origen.xz / 60.0) * 0.65 + RuidoNube(origen.xz / 25.0 + 13.1) * 0.35);
                s.suelo = SueloBajo(origen);
                // la flor: un color por mancha de 9 u (como `colorDeMata` del prototipo; el azar, otro)
                float2 celda = floor(float2(origen.x, -origen.z) / max(_VegDatosTamano.w, 1) / 9);
                s.flor = floor(frac(sin(dot(celda, float2(12.9898, 78.233)) + 401) * 43758.5453) * 4);
                return s;
            }

            half4 Frag(Salida s) : SV_Target
            {
                float4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, s.uv);
                float alfa = 1;
                // Las copas (no los troncos: su material lleva _Variacion 0,25) se aclaran cerca del héroe y de las columnas y entre ellas y la cámara (una
                // cápsula hacia la cámara), con un tramado por píxel: así se ve lo que hay debajo del bosque cerrado.
                float aclarado = 1;
                if (_Variacion > 0.5)
                {
                    float queda = 1;
                    for (int q = 0; q < (int)_AclararCuantos; q++)
                    {
                        float3 a = _Aclarar[q].xyz, b = _WorldSpaceCameraPos;
                        float3 ab = b - a;
                        float t = saturate(dot(s.mundo - a, ab) / max(dot(ab, ab), 1e-3));
                        float d = length(s.mundo - (a + ab * t * 0.8));
                        // núcleo limpio hasta el 60 % del radio; el tramado, solo en el borde
                        queda = min(queda, smoothstep(_Aclarar[q].w * 0.6, _Aclarar[q].w, d));
                    }
                    if (queda < 0.999)
                    {
                        // Tramado fino: la cobertura del MSAA (alpha to coverage, 4 muestras por píxel) sigue a `queda`, con algo de ruido por
                        // píxel (gradiente entrelazado) para que no se vea el patrón fijo del hardware.
                        float tramado = frac(52.9829189 * frac(dot(s.pos.xy, float2(0.06711056, 0.00583715))));
                        aclarado = saturate(queda + (tramado - 0.5) * 0.3);
                        clip(aclarado - 0.01);
                    }
                }
                if (_Recorte > 0.5)
                {
                    clip(c.a - 0.35);
                    // Alpha to coverage con el borde afilado: la cobertura del MSAA sigue el contorno de la hoja.
                    alfa = saturate((c.a - 0.5) / max(fwidth(c.a), 1e-4) + 0.5);
                }
                alfa *= aclarado;
                float3 n = normalize(s.normal);
                float3 tg = normalize(s.tangente.xyz - n * dot(s.tangente.xyz, n));
                float3 bt = cross(n, tg) * s.tangente.w;
                float3 nt = UnpackNormalScale(SAMPLE_TEXTURE2D(_Normal, sampler_Normal, s.uv), _FuerzaNormal);
                float3 N = normalize(nt.x * tg + nt.y * bt + nt.z * n);
                float4 m = SAMPLE_TEXTURE2D(_Mascara, sampler_Mascara, s.uv);
                float3 albedo = c.rgb * s.color.rgb * lerp(0.75, 1.0, m.r);
                // Hondura del dosel: en el bosque cerrado la mitad baja de las copas más oscura (la altura relativa sale del peso del viento,
                // que va con la altura: 0 en la base, 1 arriba).
                // (la hondura, del bosque del motor: hasta 0,25 en el cerrado)
                float hondura = _ConHondura * 0.25 * smoothstep(0.3, 0.85, s.suelo.z);
                albedo *= lerp(1 - hondura, 1, saturate(pow(max(s.color.a, 0), 0.67) * 1.6 - 0.25));
                if (_Modo > 0.5 && _Modo < 1.5)
                {
                    // Hierba: la textura es gris (base #808080, la punta más clara); el color, el del suelo de debajo: pajiza sobre el seco,
                    // verde sobre el verde, rica en lo más fértil y de duna en la arena (sRGB #a08a4c, #62742f, #5a7430, #a8965a).
                    float3 t = lerp(float3(0.352, 0.254, 0.073), float3(0.122, 0.178, 0.028), s.suelo.x);
                    t = lerp(t, float3(0.102, 0.178, 0.030), smoothstep(0.5, 0.9, s.suelo.y) * s.suelo.x);
                    t = lerp(t, float3(0.392, 0.305, 0.102), s.suelo.w);
                    albedo *= t / 0.216;
                }
                else if (_Modo > 1.5)
                {
                    // Flor: la cabeza (azul de la máscara), del color de su mancha: amapola, amarilla, margarita o malva (sRGB #cc2e21, #f2d44c,
                    // #f2f0e0, #9973bf, como `COLOR.flores` del prototipo); horneada casi blanca.
                    float3 cf = s.flor < 1 ? float3(0.604, 0.027, 0.015) : s.flor < 2 ? float3(0.888, 0.658, 0.073)
                        : s.flor < 3 ? float3(0.888, 0.871, 0.745) : float3(0.319, 0.171, 0.521);
                    albedo = lerp(albedo, albedo * cf / 0.8, saturate(m.b));
                }
                // La variación grande del follaje, de un ruido que no se repite (por posición de mundo, hacia 1,5 y 5 m): valor y algo de matiz.
                float v = (RuidoNube(s.mundo.xz / 1.5) - 0.5) + (RuidoNube(s.mundo.xz / 5.0 + 31.7) - 0.5);
                albedo *= 1 + 0.3 * v * _Variacion;
                albedo *= lerp(float3(1, 1, 1), float3(1.03, 1.01, 0.94), saturate(v + 0.5) * 0.3 * _Variacion);
                // Por planta: valor ±15 %, matiz hacia amarillo u oliva y saturación ±10 %; y por rodal, un tono compartido.
                float h1 = s.azar.x, h2 = frac(h1 * 7.13 + 0.31), h3 = frac(h1 * 13.7 + 0.71), rodal = s.azar.y;
                albedo *= (1 + 0.3 * (h1 - 0.5)) * lerp(0.88, 1.1, rodal);
                // matiz: unas algo más amarillas u oliva, otras algo más frías (centrado: el tono medio es el de Blender)
                albedo *= lerp(float3(1, 1, 1), lerp(float3(0.97, 1.0, 1.05), float3(1.05, 1.02, 0.88), h2), 0.8 * _Variacion);
                float lum = dot(albedo, float3(0.2126, 0.7152, 0.0722));
                albedo = lerp(lum.xxx, albedo, 1 + 0.2 * (h3 - 0.5) * _Variacion);
                Light sol = GetMainLight();
                // Luz envolvente con más sol: la cara al sol clara, la contraria claramente más oscura.
                float envuelve = saturate((dot(N, sol.direction) + 0.3) / 1.3);
                float3 V = GetWorldSpaceNormalizeViewDir(s.mundo);
                float contraluz = pow(saturate(dot(-V, sol.direction)), 3) * m.g * _Translucidez;
                // (la translucidez, también a la sombra: la lleva la directa)
                // (sin la compensación del sol bajo, que es para el llano: las copas, como con el sol a 40° sin compensar)
                float sinCompensar = _CompensaSol > 0 ? 1 / _CompensaSol : 1;
                float3 col = albedo * LuzDelMapa(N.y, 0.3, (0.8 * envuelve + 0.5 * contraluz) * sinCompensar, SombraDelSol(s.mundo), 0.45);
                // La corteza: a la sombra baja como la copa y nunca a negro (un suelo de luminancia, con su tono: se lee marrón).
                if (_Variacion < 0.5)
                {
                    float lc = dot(col, float3(0.2126, 0.7152, 0.0722));
                    col *= max(1, 0.035 / max(lc, 1e-4));
                }
                return half4(PintarMapa(col, s.mundo, true), alfa);
            }
            ENDHLSL
        }
        // La sombra que arroja: el mismo viento, el recorte por alfa de las tarjetas (sin alpha to coverage) y sin el aclarado (la copa
        // aclarada sobre el héroe sigue dando sombra: se lee «bajo el bosque»). Sesgo por normal contra el acné en las copas.
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]
            HLSLPROGRAM
            #pragma vertex VertSombra
            #pragma fragment FragSombra
            #pragma multi_compile_instancing
            float3 _LightDirection;
            struct EntradaSombra { float4 pos : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; float4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct SalidaSombra { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            SalidaSombra VertSombra(EntradaSombra e)
            {
                SalidaSombra s;
                UNITY_SETUP_INSTANCE_ID(e);
                float3 origen = TransformObjectToWorld(float3(0, 0, 0));
                float azar = frac(sin(dot(origen.xz, float2(12.9898, 78.233))) * 43758.5453);
                float3 w = ConViento(TransformObjectToWorld(e.pos.xyz), azar, e.color.a);
                w = origen + (w - origen) * NieblaCrecer(origen); // recogida en lo nunca explorado
                s.pos = TransformWorldToHClip(ApplyShadowBias(w, TransformObjectToWorldNormal(e.normal), _LightDirection));
            #if UNITY_REVERSED_Z
                s.pos.z = min(s.pos.z, UNITY_NEAR_CLIP_VALUE);
            #else
                s.pos.z = max(s.pos.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                s.uv = TRANSFORM_TEX(e.uv, _MainTex);
                return s;
            }
            half4 FragSombra(SalidaSombra s) : SV_Target
            {
                if (_Recorte > 0.5) clip(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, s.uv).a - 0.5);
                return 0;
            }
            ENDHLSL
        }
    }
}
