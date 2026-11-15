// La matemática de la cámara del mapa estratégico 2D (paso 9 del bloque 4), calcada de BronzeAgeClient (`src/ui/geometriaVista.ts`). C# puro.
//
// La cámara es `{ Escala, Cx, Cy }`: `Escala` en px de pantalla por unidad y (Cx, Cy) el punto del mundo en el centro del lienzo. Plano: x a la
// derecha, y hacia abajo.   pantalla = (mundo − origen) · escala,   origen = centro − (mitad del lienzo) / escala
// Los dos topes del zoom: de lejos, el mundo entero llenando el lienzo (`EscalaMinima`); de cerca, la escala de visión: el diámetro de lo que ve
// el héroe ocupa el lado corto del lienzo (`EscalaDeVision`).
using System;

namespace ConquestTactics.Mundo
{
    public readonly struct CamaraDeVista
    {
        public readonly double Escala, Cx, Cy;
        public CamaraDeVista(double escala, double cx, double cy) { Escala = escala; Cx = cx; Cy = cy; }
    }

    public static class GeometriaVista
    {
        /// <summary>El mundo entero llenando el lienzo (manda el lado que más pide: no queda fuera del mundo a la vista).</summary>
        public static double EscalaMinima(double anchoPx, double altoPx, double anchoMundo, double altoMundo) =>
            Math.Max(anchoPx / anchoMundo, altoPx / altoMundo);

        /// <summary>El diámetro de visión (2 · radio) ocupando el lado corto del lienzo.</summary>
        public static double EscalaDeVision(double anchoPx, double altoPx, double radioVision) => Math.Min(anchoPx, altoPx) / (2 * radioVision);

        /// <summary>La cámara dentro de sus topes: el zoom entre `min` y `max`, y la vista sin salirse del mundo (centrada si lo cubre entero).</summary>
        public static CamaraDeVista Acotar(CamaraDeVista c, double anchoPx, double altoPx, double anchoMundo, double altoMundo, double min, double max)
        {
            var escala = Math.Max(min, Math.Min(Math.Max(min, max), c.Escala));
            double Eje(double centro, double ladoPx, double ladoMundo)
            {
                var mitad = ladoPx / 2 / escala;
                if (mitad * 2 >= ladoMundo) return ladoMundo / 2;
                return Math.Max(mitad, Math.Min(ladoMundo - mitad, centro));
            }
            return new CamaraDeVista(escala, Eje(c.Cx, anchoPx, anchoMundo), Eje(c.Cy, altoPx, altoMundo));
        }

        /// <summary>El punto del mundo bajo un punto del lienzo, en px medidos desde su CENTRO.</summary>
        public static (double x, double y) AMundo(CamaraDeVista c, double px, double py) => (c.Cx + px / c.Escala, c.Cy + py / c.Escala);

        /// <summary>La esquina superior izquierda del lienzo, en el mundo.</summary>
        public static (double x, double y) Origen(CamaraDeVista c, double anchoPx, double altoPx) => AMundo(c, -anchoPx / 2, -altoPx / 2);

        /// <summary>Zoom por `factor` manteniendo FIJO el punto del mundo bajo `foco` (px desde el centro: el cursor, o 0,0 para los botones).</summary>
        public static CamaraDeVista ZoomHacia(CamaraDeVista c, double factor, double focoX, double focoY)
        {
            var (x, y) = AMundo(c, focoX, focoY);
            var escala = c.Escala * factor;
            return new CamaraDeVista(escala, x - focoX / escala, y - focoY / escala);
        }

        /// <summary>Mover la vista `dx, dy` px de pantalla (arrastre: el mundo sigue al puntero).</summary>
        public static CamaraDeVista Arrastrar(CamaraDeVista c, double dx, double dy) => new CamaraDeVista(c.Escala, c.Cx - dx / c.Escala, c.Cy - dy / c.Escala);
    }
}
