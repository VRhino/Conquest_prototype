// El humo y la llama de una batalla en el mapa de mundo (`MaquetasDelMapa`, lo menudo de la parte 5): lo que hace visible la marca desde
// cualquier lado. La malla son bocanadas con sus cuatro esquinas en el mismo punto (uv0, la esquina; uv1, su fase y su tamaño): aquí se abren de
// frente a la cámara y se animan con el tiempo.
//  * El humo (`_Llama` 0), al estilo del mapa (no realista): cada bocanada, una coliflor de 4 a 6 lóbulos por ruido, distinta por su semilla,
//    de borde recortado y nítido; con volumen en tres bandas (cara clara hacia el sol del mapa, en espacio de mundo, y panza oscura); se
//    deshace por erosión (se agujerea y se rompe al envejecer: arriba, jirones sueltos). Tamaños distintos (±40 %), desvío lateral que crece
//    con la altura, una inclinación fija en el mundo (viento) con vaivén, y ritmos de subida distintos, a golpes. Carbón abajo (las primeras,
//    teñidas de naranja por la llama), gris medio a media altura y más claro arriba, sin blancos. Recortada (con profundidad): las bocanadas se
//    tapan bien entre sí.
//  * La llama (`_Llama` 1): pequeña, naranja y amarilla, parpadeando; el resplandor (`_Llama` 2): un disco naranja suave en su base que late.
// Sin sombra.
Shader "Conquest/MapaHumo"
{
    Properties
    {
        _Llama ("0 humo, 1 llama, 2 resplandor", Float) = 0
        _Fase ("Fase (por batalla)", Float) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _Origen ("Mezcla: origen", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _Destino ("Mezcla: destino", Float) = 10
        _Profundidad ("Escribe profundidad", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Blend [_Origen] [_Destino]
            ZWrite [_Profundidad]
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Llama;
                float _Fase;
                float _Origen;
                float _Destino;
                float _Profundidad;
            CBUFFER_END

            struct Entrada { float4 pos : POSITION; float2 esquina : TEXCOORD0; float2 bocanada : TEXCOORD1; };
            struct Salida { float4 pos : SV_POSITION; float2 esquina : TEXCOORD0; float t : TEXCOORD1; float semilla : TEXCOORD2; };

            // (hashes sin seno: con semillas grandes, el seno pierde precisión en la GPU y el ruido salía en rayas)
            float Hash(float n) { n = frac(n * 0.1031); n *= n + 33.33; n *= n + n; return frac(n); }
            float Hash2(float2 p) { float3 q = frac(float3(p.xyx) * 0.1031); q += dot(q, q.yzx + 33.33); return frac((q.x + q.y) * q.z); }
            float Ruido(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3 - 2 * f);
                return lerp(lerp(Hash2(i), Hash2(i + float2(1, 0)), u.x), lerp(Hash2(i + float2(0, 1)), Hash2(i + float2(1, 1)), u.x), u.y);
            }

            Salida Vert(Entrada e)
            {
                Salida s;
                // la escala del objeto (lo que mide en metros una unidad del modelo); el objeto va sin girar: su x, y, z son las del mundo
                float escala = length(float3(UNITY_MATRIX_M._m00, UNITY_MATRIX_M._m10, UNITY_MATRIX_M._m20));
                float3 base = TransformObjectToWorld(e.pos.xyz);
                float semilla = e.bocanada.x * 17.31 + _Fase * 3.7;
                float3 centro;
                float tam, t;
                if (_Llama > 1.5)
                {
                    // el resplandor: un disco naranja suave en la base de la llama, que late
                    centro = float3(0, 0.015, 0);
                    tam = 0.16 * (0.92 + 0.08 * sin(_Time.y * 7.3));
                    t = 0.5;
                }
                else if (_Llama > 0.5)
                {
                    // la llama: lenguas cortas que suben y se encogen, con el parpadeo
                    t = frac(_Time.y * 0.9 + e.bocanada.x + _Fase);
                    float a = e.bocanada.x * 6.2832;
                    centro = float3(cos(a) * 0.03, 0.015 + t * 0.105, sin(a) * 0.03);
                    tam = (0.052 * (1 - t) + 0.018) * e.bocanada.y * (0.85 + 0.15 * sin(_Time.y * 23 + a * 3));
                }
                else
                {
                    // el humo: cada bocanada con su ritmo (de ×0,8 a ×1,25), subiendo a golpes; la columna, 1,5 unidades del modelo
                    float ritmo = 0.8 + 0.45 * Hash(semilla);
                    t = frac(_Time.y * 0.07 * ritmo + e.bocanada.x + _Fase);
                    float subida = t + 0.035 * sin(t * 18 + semilla * 5) * (1 - t);
                    float lado = (Hash(semilla + 2) - 0.5) * 0.3 * t;           // el desvío lateral, que crece con la altura
                    float fondo = (Hash(semilla + 5) - 0.5) * 0.3 * t;
                    // el viento: una inclinación fija en el mundo (hacia +x y algo de −z) con un vaivén suave
                    float viento = t * t * 0.42 + sin(_Time.y * 0.6 + semilla) * 0.03 * t;
                    centro = float3(viento + lado, subida * 1.5, -viento * 0.35 + fondo);
                    // el tamaño, el de la versión 49 (0,07 + 0,25 t + 0,15 t²) por ±40 %
                    tam = (0.07 + 0.25 * t + 0.15 * t * t) * e.bocanada.y * (0.6 + 0.8 * Hash(semilla + 9));
                }
                float3 derecha = UNITY_MATRIX_V[0].xyz, arriba = UNITY_MATRIX_V[1].xyz;
                float3 mundo = base + centro * escala + (derecha * e.esquina.x + arriba * e.esquina.y) * tam * escala;
                // (el humo escribe profundidad: cada bocanada, a su propia distancia de la cámara, separadas, para que las caras casi
                // paralelas no se peleen por la profundidad a la distancia del mapa, que salían en rayas)
                if (_Llama < 0.5) mundo += UNITY_MATRIX_V[2].xyz * (Hash(semilla + 13) - 0.5) * 0.3 * escala;
                s.pos = TransformWorldToHClip(mundo);
                s.esquina = e.esquina;
                s.t = t;
                s.semilla = semilla;
                return s;
            }

            half4 Frag(Salida s) : SV_Target
            {
                float d = length(s.esquina);
                if (_Llama > 1.5)
                {
                    float g = saturate(1 - d);
                    return half4(1.0, 0.42, 0.08, g * g * 0.45);
                }
                if (_Llama > 0.5)
                {
                    if (d > 1) discard;
                    float borde = smoothstep(1.0, 0.35, d);
                    float3 cl = lerp(float3(1.0, 0.85, 0.35), float3(0.95, 0.35, 0.08), saturate(d * 1.3 + s.t * 0.5));
                    return half4(cl, borde * (1 - s.t * 0.7));
                }
                // --- el humo ---
                // la bocanada: una unión de 4 a 6 bolas por su semilla (la primera, el cuerpo en el centro; las demás alrededor), cada una una
                // elipse suave (ejes de 0,8 a 1, girada al azar) con el radio ondulado muy poco y en baja frecuencia (hasta un 7 %, tres o cuatro
                // ondas por vuelta) que se mueve despacio con la vida de la bocanada: dibujada a mano, no de compás. En cada punto manda la bola
                // más alta (su casquete). La erosión encoge las bolas a ritmos distintos; la del centro se va antes y ninguna pasa de un tercio
                // del ancho al final: lo que se desprende son bolitas en grupos medio fundidos, nunca un globo grande suelto.
                float erosion = saturate((s.t - 0.35) / 0.6);
                float n = 4 + floor(Hash(s.semilla + 1) * 3);
                float tope = lerp(0.55, 0.17, erosion);
                float mejor = 0, fMejor = 1, queda = 1;
                float2 dirMejor = float2(0, 1);
                [unroll] for (int k = 0; k < 6; k++)
                {
                    float fk = k + 1;
                    // (al erosionarse, las bolas se recogen hacia el centro: lo que se suelta no se aleja más de un diámetro)
                    float2 c0 = (k == 0 ? float2(0, -0.05) : float2(Hash(s.semilla + fk * 3.1) - 0.5, Hash(s.semilla + fk * 5.7) - 0.5) * 0.8) * (1 - 0.5 * erosion);
                    float r0 = k == 0 ? 0.5 : 0.24 + 0.26 * Hash(s.semilla + fk * 7.3);
                    // (la del centro encoge como las demás: si se iba antes, las de alrededor quedaban sueltas; las pequeñas, antes)
                    float encoge = k == 0 ? 0.95 : 0.85 + 0.6 * Hash(s.semilla + fk * 2.3) + 0.4 * (1 - saturate((r0 - 0.24) / 0.26));
                    float rk = min(r0 * (1 - erosion * encoge * 0.85), tope);
                    float giro = Hash(s.semilla + fk * 4.9) * 6.2832, ejes = 0.8 + 0.2 * Hash(s.semilla + fk * 8.1);
                    float2 dq = s.esquina - c0;
                    float2 q = float2(dq.x * cos(giro) + dq.y * sin(giro), (-dq.x * sin(giro) + dq.y * cos(giro)) / ejes);
                    float ondas = 3 + floor(Hash(s.semilla + fk * 6.7) * 2);
                    float rr = rk * (1 + 0.07 * sin(ondas * atan2(q.y, q.x) + fk * 2.1 + s.t * 3));
                    float f = length(q) / max(rr, 1e-4);                       // (0 en el centro de la bola, 1 en su borde)
                    float hk = f < 1 ? rr * sqrt(1 - f * f) : 0;
                    if (k < n && rk > 0.01 && hk > mejor) { mejor = hk; fMejor = f; dirMejor = length(dq) > 1e-4 ? normalize(dq) : float2(0, 1); queda = rk / r0; }
                }
                clip(mejor - 0.0001);
                // el volumen: el claro, un casquete descentrado hacia el sol (en pantalla, la dirección del sol del mundo) hasta tocar el borde de
                // ese lado; la panza oscura, al lado contrario; los dos siguen la elipse ondulada de la bola (van en su radio normalizado). Así
                // nunca sale una diana (aro dentro de aro) ni un corte recto, y el lado claro sigue al sol aunque gire la cámara.
                float3 L = GetMainLight().direction;
                float2 sol = float2(dot(L, UNITY_MATRIX_V[0].xyz), dot(L, UNITY_MATRIX_V[1].xyz));
                sol = length(sol) > 0.05 ? normalize(sol) : float2(0, 1);
                float2 u = dirMejor * fMejor;                                     // (el punto, en el radio de su bola)
                // (el claro: fuera de un círculo corrido hacia la sombra, media luna al sol; la panza, al revés. Dentro, blando: el paso entre
                // tonos, en un 12 % del radio; fuera, la silueta sigue recortada)
                float claro = smoothstep(0.80, 0.92, length(u + sol * 0.5));
                float panza = smoothstep(0.84, 0.96, length(u - sol * 0.5)) * (1 - claro);
                float banda = lerp(lerp(1.08, 1.45, claro), 0.88, panza);         // tres tonos (el claro, un punto más: de frente se leían planas)
                // (lo que se va deshaciendo pierde el contraste y se aclara: lo pequeño y suelto queda plano y pálido, sin panza)
                float firme = smoothstep(0.2, 0.65, queda);
                banda = lerp(1.18, banda, firme);
                // el tono: carbón abajo, gris medio a media altura, más claro arriba (sin blancos), cada bocanada algo distinta
                float3 gris = lerp(float3(0.028, 0.027, 0.025), float3(0.085, 0.083, 0.08), saturate(s.t * 2));
                gris = lerp(gris, float3(0.165, 0.161, 0.155), saturate((s.t - 0.45) * 2));
                gris *= 0.9 + 0.2 * Hash(s.semilla + 4);
                gris = lerp(float3(0.18, 0.176, 0.17), gris, 0.35 + 0.65 * firme);
                float3 c = gris * banda;
                // la luz de la llama: naranja solo en lo que mira hacia abajo (hacia ella) y en el primer 15 % de la altura, apagándose rápido
                float3 normal = normalize(UNITY_MATRIX_V[0].xyz * u.x + UNITY_MATRIX_V[1].xyz * u.y + UNITY_MATRIX_V[2].xyz * sqrt(saturate(1 - fMejor * fMejor)));
                float abajo = saturate(-normal.y);
                float cerca = saturate(1 - s.t / 0.15);
                c += float3(0.35, 0.11, 0.02) * abajo * cerca * cerca;
                // (un filo corto de borde, más oscuro: el recorte de estilo)
                c *= fMejor > 0.95 ? 0.82 : 1;
                return half4(c, 1);
            }
            ENDHLSL
        }
    }
}
