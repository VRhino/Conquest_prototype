// La cámara del mapa de mundo (BronzeAgeClient: `src/mundo3d/camara.ts`, `config.ts` y `docs/Mundo_3D.md`, tabla «Valores»): gira libre
// alrededor del héroe, a la distancia a la que un círculo de «en pantalla» u de diámetro cabe justo en la pantalla con cualquier inclinación,
// y con tres zooms por pasos según la visión de la columna. Y el rayo del clic contra el relieve. Matemática pura, en unidades del mapa.
using System;

namespace ConquestTactics.Mundo
{
    public enum ZoomDelMapa { Cerca, Normal, Alejado }

    public sealed class CamaraDelMapa
    {
        /// <summary>Apertura vertical, en grados.</summary>
        public const double Apertura = 35;
        public const double InclinacionInicial = 30, InclinacionMin = 10, InclinacionMax = 90;
        /// <summary>El zoom de cerca enseña 0,6 veces lo del normal.</summary>
        public const double Acercado = 0.6;
        /// <summary>Suavizado (1/s): seguir a la columna, cambiar de zoom, subir y bajar sobre las laderas.</summary>
        public const double SuaveZoom = 7, SuaveSubir = 14, SuaveBajar = 3;

        /// <summary>Grados sobre el horizonte.</summary>
        public double Inclinacion = InclinacionInicial;
        /// <summary>Radianes alrededor del héroe.</summary>
        public double Giro;
        public ZoomDelMapa Zoom = ZoomDelMapa.Normal;
        /// <summary>Radio de visión de la columna (`Movimiento_Ejercitos.VISION` del servidor): decide cuánto cabe en pantalla.</summary>
        public double RadioVision = 15;
        /// <summary>Distancia de la cámara al héroe (se acerca a la del encuadre poco a poco al cambiar de zoom).</summary>
        public double Distancia;
        double alzado;

        /// <summary>Lo que cabe en pantalla, normal y alejado, según el radio de visión: el radio y 1,5 veces (15 → 15 y 22; 30 → 30 y 45).</summary>
        public static (double normal, double alejado) Encuadres(double radioVision) => (radioVision, Math.Floor(radioVision * 1.5));

        /// <summary>El diámetro (u) del círculo alrededor del héroe que cabe en pantalla ahora.</summary>
        public double EnPantalla
        {
            get
            {
                var (normal, alejado) = Encuadres(RadioVision);
                return Zoom == ZoomDelMapa.Alejado ? alejado : Zoom == ZoomDelMapa.Cerca ? normal * Acercado : normal;
            }
        }

        /// <summary>Un paso de zoom: −1 acerca, 1 aleja. Devuelve si cambió.</summary>
        public bool PasoDeZoom(int sentido)
        {
            var z = (ZoomDelMapa)Math.Max(0, Math.Min(2, (int)Zoom + Math.Sign(sentido)));
            if (z == Zoom) return false;
            Zoom = z;
            return true;
        }

        public void Inclinar(double grados) => Inclinacion = Math.Min(InclinacionMax, Math.Max(InclinacionMin, Inclinacion + grados));

        /// <summary>La distancia a la que un círculo de radio `r` alrededor del objetivo cabe justo en la pantalla, con la inclinación `th`
        /// (radianes), la tangente de media apertura vertical `tg` y el aspecto `asp` (ancho / alto). Se mide sobre el plano del objetivo.</summary>
        public static double DistanciaDeEncuadre(double r, double th, double tg, double asp)
        {
            double s = Math.Sin(th), c = Math.Cos(th);
            bool Cabe(double d)
            {
                for (int k = 0; k < 48; k++)
                {
                    double a = k / 48.0 * Math.PI * 2, x = r * Math.Cos(a), z = r * Math.Sin(a), p = d - z * c;
                    if (p <= 0 || Math.Abs(z * s / p) > tg || Math.Abs(x / p) > tg * asp) return false;
                }
                return true;
            }
            double lo = r * c + 1e-3, hi = r * 60;
            for (int i = 0; i < 40; i++)
            {
                var m = (lo + hi) / 2;
                if (Cabe(m)) hi = m; else lo = m;
            }
            return hi;
        }

        /// <summary>Dónde va la cámara (unidades del mapa: x, altura, y) para mirar a `objetivo` con el aspecto de pantalla dado. `suelo(x, y)`
        /// da la altura del terreno en unidades: en la montaña la cámara se levanta para no quedar dentro de una ladera. `dt` = 0 coloca de golpe.</summary>
        public (double x, double h, double y) Colocar((double x, double h, double y) objetivo, double aspecto, Func<double, double, double> suelo, double dt)
        {
            double th = Math.Min(89.5, Inclinacion) * Math.PI / 180, tg = Math.Tan(Apertura * Math.PI / 360);
            var encuadre = DistanciaDeEncuadre(EnPantalla / 2, th, tg, aspecto);
            Distancia = Distancia > 0 && dt > 0 ? Distancia + (encuadre - Distancia) * (1 - Math.Exp(-dt * SuaveZoom)) : encuadre;
            double d = Distancia;
            double px = objetivo.x + d * Math.Cos(th) * Math.Sin(Giro), py = objetivo.y + d * Math.Cos(th) * Math.Cos(Giro);
            double suelto = objetivo.h + d * Math.Sin(th), minimo = suelo(px, py) + 0.5, falta = Math.Max(0, minimo - suelto);
            // Sube deprisa y baja despacio; nunca por debajo del suelo.
            alzado = dt > 0 ? alzado + (falta - alzado) * (1 - Math.Exp(-dt * (falta > alzado ? SuaveSubir : SuaveBajar))) : falta;
            return (px, Math.Max(suelto + alzado, suelo(px, py) + 0.15), py);
        }

        /// <summary>Píxeles de pantalla por unidad del mapa a la distancia del héroe (para el radio de agarre de los clics).</summary>
        public double PxPorUnidad(double altoPx) => altoPx / (2 * (Distancia > 0 ? Distancia : 30) * Math.Tan(Apertura * Math.PI / 360));

        /// <summary>Dónde toca el relieve un rayo (unidades del mapa: x, altura, y; `dir` no hace falta que sea unitario). Avanza a pasos que crecen
        /// con la distancia y afina por bisección. Null si no toca en `alcance` u.</summary>
        public static (double x, double y)? TocarRelieve((double x, double h, double y) origen, (double x, double h, double y) dir,
            Func<double, double, double> suelo, double alcance = 2000)
        {
            double largo = Math.Sqrt(dir.x * dir.x + dir.h * dir.h + dir.y * dir.y);
            if (largo == 0) return null;
            double dx = dir.x / largo, dh = dir.h / largo, dy = dir.y / largo;
            double Sobre(double t) => origen.h + dh * t - suelo(origen.x + dx * t, origen.y + dy * t);
            if (Sobre(0) < 0) return (origen.x, origen.y);
            double antes = 0;
            for (double t = 0.1; t <= alcance; t += Math.Max(0.1, t * 0.02))
            {
                if (Sobre(t) <= 0)
                {
                    double lo = antes, hi = t;
                    for (int i = 0; i < 30; i++) { var m = (lo + hi) / 2; if (Sobre(m) <= 0) hi = m; else lo = m; }
                    return (origen.x + dx * hi, origen.y + dy * hi);
                }
                antes = t;
            }
            return null;
        }
    }
}
