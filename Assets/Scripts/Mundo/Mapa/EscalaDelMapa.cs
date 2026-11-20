// La escala del mapa de mundo y los valores con que se pinta, en un solo sitio. Una unidad de Unity es un metro: una unidad del mapa (u) son
// `MetrosPorUnidad` metros, el `factor_unity` del manifiesto del artefacto (12), y la altura del relieve, que ya viene en metros, va tal cual
// (sin exagerar: así las pendientes que se ven son las que usa el servidor). Ejes: x = x del mapa, y = altura, z = −y del mapa (la fila 0 es el
// norte y queda arriba en pantalla; con z = y el mapa saldría en espejo).
//
// Los colores y tamaños son los de BronzeAgeClient (`src/mundo3d/config.ts`, `terreno.ts`, `agua.ts`, `paletas.ts`); son del cliente, no
// reglas. Las cifras de reglas (visión, umbral del agua) salen de `/v1/datos`.
using UnityEngine;

namespace ConquestTactics.Mundo.Mapa
{
    public static class EscalaDelMapa
    {
        /// <summary>Metros (unidades de Unity) por unidad del mapa. Lo fija el manifiesto del artefacto al cargar el mapa.</summary>
        public static float MetrosPorUnidad { get; private set; } = 12;

        public static void Fijar(uint factorUnity)
        {
            if (factorUnity > 0) MetrosPorUnidad = factorUnity;
        }

        /// <summary>Un punto del mapa (u) con su altura (metros) a la escena.</summary>
        public static Vector3 AEscena(double x, double y, double alturaMetros) => new Vector3((float)(x * MetrosPorUnidad), (float)alturaMetros, (float)(-y * MetrosPorUnidad));

        /// <summary>De la escena al mapa: x, altura en u, y.</summary>
        public static (double x, double h, double y) AlMapa(Vector3 p) => (p.x / MetrosPorUnidad, p.y / MetrosPorUnidad, -p.z / MetrosPorUnidad);

        public static (double x, double h, double y) DireccionAlMapa(Vector3 d) => (d.x, d.y, -d.z);
    }

    /// <summary>Lo que pinta el mapa con cuadros y colores planos, hasta que haya modelos propios.</summary>
    public static class PinturaDelMapa
    {
        /// <summary>La nieve del suelo (`Conquest/MapaSuelo`): solo en los picos más altos, desde esta fracción del tramo entre el umbral de la cima
        /// y la altura máxima del relieve. Con la cima a 685 m y el máximo a 1.000, unos 840 m: en el mundo de prueba, el 12 % más alto de la
        /// clase cima (mirado en el histograma de alturas del artefacto, 2026-11-20).</summary>
        public const float NieveSobreCima = 0.5f;
        /// <summary>Radio (m) de la pendiente ancha del suelo: la roca de pendiente y la nieve la miran, así una arista más estrecha que esto no
        /// sale en roca y los bordes no siguen los triángulos de la malla.</summary>
        public const float RadioPendienteAncha = 24;
        // Agua: somera (lagos y orillas) y honda (mar adentro); ríos y vados.
        public static readonly Color AguaSomera = new Color32(0x5a, 0xae, 0xa6, 255), AguaHonda = new Color32(0x1d, 0x4d, 0x72, 255),
            Vado = new Color32(0xd9, 0xc7, 0x9a, 255);
        /// <summary>Lo que cubre el mar para pintarlo hondo (metros bajo la lámina).</summary>
        public const float MarHondo = 30;
        /// <summary>Ancho de los ríos (u): navegables y los demás.</summary>
        public const float RioNavegable = 3, Rio = 1.5f;

        // El cielo de BronzeAgeClient (`config.ts`, `cielo.ts`): cénit, horizonte (también el color de la bruma lejana) y bajo el horizonte, en
        // sRGB; y hacia dónde está el sol (allí, (−60, 110, 45) con z = y del mapa; aquí z = −y).
        public static readonly Color Cenit = new Color32(0x3d, 0x72, 0xb0, 255), Horizonte = new Color32(0xb7, 0xc6, 0xcf, 255),
            BajoElHorizonte = new Color32(0x8e, 0x9a, 0x98, 255);
        public static readonly Vector3 Sol = new Vector3(-60, 110, -45).normalized;

        // La niebla de guerra: el color (el fondo del tema de BronzeAgeClient) y la opacidad de lo nunca visto, lo recordado y lo que se ve.
        public static readonly Color Niebla = new Color32(10, 14, 23, 255);
        public const float AlfaSinVer = 206 / 255f, AlfaRecordado = 158 / 255f, AlfaVisible = 0;

        // Lo propio, encima de la niebla.
        public static readonly Color Columna = new Color32(0xd8, 0xb8, 0x78, 255), Ruta = new Color32(0xf1, 0xdc, 0xae, 255),
            Destino = new Color32(0xbf, 0x52, 0x33, 255);
        /// <summary>La caja de tu columna: lado y alto (u).</summary>
        public const float LadoColumna = 1.2f, AltoColumna = 0.8f;

        /// <summary>Teselas de suelo: hasta dónde se cargan (u) y con qué paso (u) según la distancia al héroe.</summary>
        public const float DistanciaCarga = 320;
        public static float PasoSegunDistancia(float d) => d < 80 ? 0.5f : d < 160 ? 1 : 2;
    }
}
