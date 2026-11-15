// El sigilo de Facción (paso 8 del bloque 4, tanda 1): ocho ids del catálogo (`Sigilo` del contrato) y su dibujo, calcado de BronzeAgeClient
// (`src/sigilo/sigilo.ts`, `emblemas.ts`): el mismo SVG en un lienzo de 100×120 (silueta recortada, campo, orla, contorno, emblema con halo y la
// corona del Gran Rey). El catálogo, los emblemas (game-icons.net, CC BY 3.0: `Resources/MundoUI/Sigilo/CREDITOS.txt`) y las insignias son copia
// de los del prototipo y los carga el lado Unity (`Cargar`). C# puro: el SVG lo convierte en textura `TexturaDeSigilo` (Conquest.Mundo.UI).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using BronzeAge.Contract.V1;
using Newtonsoft.Json.Linq;

namespace ConquestTactics.Mundo
{
    public static class Sigilos
    {
        public sealed class ColorDeSigilo { public string Id, Nombre, Hex; }

        public static IReadOnlyList<string> Formas { get; private set; } = Array.Empty<string>();
        public static IReadOnlyList<string> Campos { get; private set; } = Array.Empty<string>();
        public static IReadOnlyList<string> Orlas { get; private set; } = Array.Empty<string>();
        public static IReadOnlyList<string> Emblemas { get; private set; } = Array.Empty<string>();
        public static IReadOnlyList<ColorDeSigilo> Colores { get; private set; } = Array.Empty<ColorDeSigilo>();
        public static bool Cargado { get; private set; }

        static readonly Dictionary<string, string> trazos = new Dictionary<string, string>();
        static readonly Dictionary<string, Sigilo> reservados = new Dictionary<string, Sigilo>();
        static string corona;

        /// <summary>El catálogo (`catalogoSigilos.json`), los emblemas (`emblemas.json`) y las insignias (`insignias.json`) del prototipo.</summary>
        public static void Cargar(string catalogo, string emblemas, string insigniasJson)
        {
            if (Cargado) return;
            var c = JObject.Parse(catalogo);
            Formas = c["formas"].Values<string>().ToList();
            Campos = c["campos"].Values<string>().ToList();
            Orlas = c["orlas"].Values<string>().ToList();
            Emblemas = c["emblemas"].Values<string>().ToList();
            Colores = c["colores"].Select(x => new ColorDeSigilo { Id = (string)x["id"], Nombre = (string)x["nombre"], Hex = (string)x["hex"] }).ToList();
            foreach (var r in ((JObject)c["reservados"]).Properties()) reservados[r.Name] = DeJson((JObject)r.Value);
            foreach (var e in JObject.Parse(emblemas).Properties()) trazos[e.Name] = (string)e.Value["d"];
            foreach (var i in JObject.Parse(insigniasJson).Properties()) if ((string)i.Value["d"] is string di) insignias[i.Name] = di;
            insignias.TryGetValue("granRey", out corona);
            Cargado = true;
        }

        static Sigilo DeJson(JObject o) => new Sigilo
        {
            FormaId = (string)o["formaId"], CampoId = (string)o["campoId"], EmblemaId = (string)o["emblemaId"], ColorPrimarioId = (string)o["colorPrimarioId"],
            ColorSecundarioId = (string)o["colorSecundarioId"], ColorEmblemaId = (string)o["colorEmblemaId"], OrlaId = (string)o["orlaId"], ColorOrlaId = (string)o["colorOrlaId"],
        };

        /// <summary>El de quien no tiene Facción, y el que se dibuja si una Facción viene sin él (`SIGILO_NEUTRO`).</summary>
        public static Sigilo Neutro => reservados.TryGetValue("neutro", out var n) ? n
            : new Sigilo { FormaId = "clasico", CampoId = "liso", EmblemaId = "sin_bandera", ColorPrimarioId = "gris", ColorSecundarioId = "blanco", ColorEmblemaId = "blanco", OrlaId = "ninguna", ColorOrlaId = "gris" };

        /// <summary>Un reservado de `CATALOGO_SIGILO` (`neutro`, `bandidos`, `mercenarios`), o null si el catálogo no lo trae.</summary>
        public static Sigilo Reservado(string id) => reservados.TryGetValue(id, out var s) ? s : null;

        /// <summary>El sigilo de una clave de `Clave` (los 8 ids con `|`), o null si no lo es (la de `neutro`, vacía o rota).</summary>
        public static Sigilo DeClave(string clave)
        {
            var p = (clave ?? "").Split('|');
            if (p.Length != 8) return null;
            return new Sigilo { FormaId = p[0], CampoId = p[1], EmblemaId = p[2], ColorPrimarioId = p[3], ColorSecundarioId = p[4], ColorEmblemaId = p[5], OrlaId = p[6], ColorOrlaId = p[7] };
        }

        /// <summary>La clave del sigilo de la batalla (el del ticket); sin él, el reservado `neutro`.</summary>
        public static string ClaveDeBatalla(Sigilo s) => s != null && !string.IsNullOrEmpty(s.FormaId) ? Clave(s) : Clave(Reservado("neutro") ?? Neutro);

        public static string ColorHex(string colorId) => Colores.FirstOrDefault(c => c.Id == colorId)?.Hex ?? "#888888";

        /// <summary>El color primario como 0xRRGGBB; null sin catálogo o sin sigilo.</summary>
        public static uint? Primario(Sigilo s)
        {
            if (!Cargado || s == null || string.IsNullOrEmpty(s.ColorPrimarioId)) return null;
            var hex = ColorHex(s.ColorPrimarioId);
            return uint.TryParse(hex.TrimStart('#'), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v) ? v : (uint?)null;
        }

        /// <summary>El sigilo de una Facción: el de su entidad o, si no está, el del directorio (`VISTA`). Null si no se conoce.</summary>
        public static Sigilo DeFaccion(Almacen a, string faccionId)
        {
            if (a == null || string.IsNullOrEmpty(faccionId)) return null;
            var f = a.Buscar(TipoEntidad.Faccion, faccionId)?.Faccion;
            if (f?.Sigilo != null && !string.IsNullOrEmpty(f.Sigilo.FormaId)) return f.Sigilo;
            var d = a.Buscar(TipoEntidad.DirectorioFacciones, "facciones")?.DirectorioFacciones?.Facciones.FirstOrDefault(x => x.Id == faccionId);
            return d?.Sigilo != null && !string.IsNullOrEmpty(d.Sigilo.FormaId) ? d.Sigilo : null;
        }

        /// <summary>Clave estable de un sigilo (la caché de texturas).</summary>
        public static string Clave(Sigilo s) => s == null ? "neutro"
            : string.Join("|", s.FormaId, s.CampoId, s.EmblemaId, s.ColorPrimarioId, s.ColorSecundarioId, s.ColorEmblemaId, s.OrlaId, s.ColorOrlaId);

        /// <summary>`doble_hacha` → `doble hacha`.</summary>
        public static string NombreDeId(string id) => (id ?? "").Replace('_', ' ');

        /// <summary>Uno al azar del catálogo, para ofrecerlo al crear la Facción (`sigiloAleatorio`; el secundario, distinto del primario).</summary>
        public static Sigilo Aleatorio(Random r)
        {
            T Al<T>(IReadOnlyList<T> l) => l[r.Next(l.Count)];
            var primario = Al(Colores).Id;
            return new Sigilo
            {
                FormaId = Al(Formas), CampoId = Al(Campos), EmblemaId = Al(Emblemas), ColorPrimarioId = primario,
                ColorSecundarioId = Al(Colores.Where(c => c.Id != primario).ToList()).Id, ColorEmblemaId = Al(Colores).Id, OrlaId = Al(Orlas), ColorOrlaId = Al(Colores).Id,
            };
        }

        // ── El dibujo (`svgSigilo`) ──

        struct Forma { public string D; public double Cx, Cy, S; }

        static readonly Dictionary<string, Forma> FormasSvg = new Dictionary<string, Forma>
        {
            ["clasico"] = new Forma { D = "M4 4H96V70Q96 105 50 116Q4 105 4 70Z", Cx = 50, Cy = 52, S = 0.62 },
            ["aspis"] = new Forma { D = "M2 60a48 48 0 1 0 96 0a48 48 0 1 0 -96 0Z", Cx = 50, Cy = 60, S = 0.62 },
            ["ocho"] = new Forma { D = "M50 4C28 4 18 18 22 34C25 45 32 50 32 60C32 70 25 75 22 86C18 102 28 116 50 116C72 116 82 102 78 86C75 75 68 70 68 60C68 50 75 45 78 34C82 18 72 4 50 4Z", Cx = 50, Cy = 60, S = 0.38 },
            ["rombo"] = new Forma { D = "M50 2L96 60L50 118L4 60Z", Cx = 50, Cy = 60, S = 0.42 },
            ["ovalo"] = new Forma { D = "M50 2C82 2 96 34 96 62C96 92 76 118 50 118C24 118 4 92 4 62C4 34 18 2 50 2Z", Cx = 50, Cy = 60, S = 0.6 },
            ["torre"] = new Forma { D = "M14 4H86L96 18V112H4V18Z", Cx = 50, Cy = 62, S = 0.62 },
        };

        static string N(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

        static string Radiante(string s)
        {
            string Punto(int i) { var ang = Math.PI + i * Math.PI / 8; return $"{(50 + 160 * Math.Cos(ang)).ToString("0.0", CultureInfo.InvariantCulture)} {(120 + 160 * Math.Sin(ang)).ToString("0.0", CultureInfo.InvariantCulture)}"; }
            var sb = new StringBuilder();
            for (var k = 1; k < 8; k += 2) sb.Append($"<polygon points=\"50 120 {Punto(k)} {Punto(k + 1)}\" fill=\"{s}\"/>");
            return sb.ToString();
        }

        static string Campo(string campoId, string p, string s)
        {
            var fondo = $"<rect width=\"100\" height=\"120\" fill=\"{p}\"/>";
            string Trazo(string d, double w) => $"<path d=\"{d}\" fill=\"none\" stroke=\"{s}\" stroke-width=\"{N(w)}\" stroke-linejoin=\"round\"/>";
            switch (campoId)
            {
                case "partido": return fondo + $"<rect x=\"50\" width=\"50\" height=\"120\" fill=\"{s}\"/>";
                case "cortado": return fondo + $"<rect y=\"60\" width=\"100\" height=\"60\" fill=\"{s}\"/>";
                case "cuartelado": return fondo + $"<rect x=\"50\" width=\"50\" height=\"60\" fill=\"{s}\"/><rect y=\"60\" width=\"50\" height=\"60\" fill=\"{s}\"/>";
                case "bandado": return fondo + $"<polygon points=\"0,20 0,50 100,100 100,70\" fill=\"{s}\"/>";
                case "palado": return fondo + $"<rect x=\"33\" width=\"34\" height=\"120\" fill=\"{s}\"/>";
                case "jefe": return fondo + $"<rect width=\"100\" height=\"40\" fill=\"{s}\"/>";
                case "cruz": return fondo + $"<rect x=\"40\" width=\"20\" height=\"120\" fill=\"{s}\"/><rect y=\"45\" width=\"100\" height=\"20\" fill=\"{s}\"/>";
                case "aspa": return fondo + $"<polygon points=\"0,0 20,0 100,100 100,120 80,120 0,20\" fill=\"{s}\"/><polygon points=\"80,0 100,0 100,20 20,120 0,120 0,100\" fill=\"{s}\"/>";
                case "chevron": return fondo + $"<polygon points=\"0,70 50,30 100,70 100,95 50,55 0,95\" fill=\"{s}\"/>";
                case "bordura": return fondo + $"<rect x=\"9\" y=\"9\" width=\"82\" height=\"102\" fill=\"none\" stroke=\"{s}\" stroke-width=\"12\"/>";
                case "losanjado": return fondo + $"<polygon points=\"50,5 95,60 50,115 5,60\" fill=\"{s}\"/>";
                case "bandas": return fondo + $"<rect y=\"24\" width=\"100\" height=\"24\" fill=\"{s}\"/><rect y=\"72\" width=\"100\" height=\"24\" fill=\"{s}\"/>";
                case "zigzag": return fondo + Trazo("M0 40L12.5 28L25 40L37.5 28L50 40L62.5 28L75 40L87.5 28L100 40", 9) + Trazo("M0 86L12.5 74L25 86L37.5 74L50 86L62.5 74L75 86L87.5 74L100 86", 9);
                case "ondas": return fondo + Trazo("M0 40Q12.5 24 25 40T50 40T75 40T100 40", 9) + Trazo("M0 80Q12.5 64 25 80T50 80T75 80T100 80", 9);
                case "greca":
                {
                    var sb = new StringBuilder(fondo);
                    for (var i = 0; i < 5; i++) foreach (var y in new[] { 30, 76 }) sb.Append($"<g transform=\"translate({i * 20} {y})\">{Trazo("M0 24V0H18V20H6V8H12", 4)}</g>");
                    return sb.ToString();
                }
                case "rombos":
                {
                    var sb = new StringBuilder(fondo);
                    for (var fila = 0; fila < 5; fila++)
                        for (var col = 0; col < 5; col++)
                        {
                            double cx = col * 25 + (fila % 2 == 1 ? 0 : 12.5), cy = fila * 26 + 6;
                            sb.Append($"<polygon points=\"{N(cx)},{N(cy - 11)} {N(cx + 11)},{N(cy)} {N(cx)},{N(cy + 11)} {N(cx - 11)},{N(cy)}\" fill=\"{s}\"/>");
                        }
                    return sb.ToString();
                }
                case "radiante": return fondo + Radiante(s);
                default: return fondo;
            }
        }

        static string Orla(string orlaId, string forma, string color)
        {
            var b = $"d=\"{forma}\" fill=\"none\" stroke=\"{color}\"";
            switch (orlaId)
            {
                case "lisa": return $"<path {b} stroke-width=\"14\"/>";
                case "greca": return $"<path {b} stroke-width=\"14\" stroke-dasharray=\"11 5\"/>";
                case "cuerda": return $"<path {b} stroke-width=\"14\" stroke-dasharray=\"5 3\" stroke-linecap=\"round\"/>";
                case "puntos": return $"<path {b} stroke-width=\"12\" stroke-dasharray=\"0 9\" stroke-linecap=\"round\"/>";
                case "dentada": return $"<path {b} stroke-width=\"16\" stroke-dasharray=\"7 7\"/>";
                default: return "";
            }
        }

        static string HaloPara(string hex)
        {
            int C(int i) => int.Parse(hex.Substring(i, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return 0.299 * C(1) + 0.587 * C(3) + 0.114 * C(5) > 140 ? "#1c1b1a" : "#f4efe4";
        }

        static string Emblema(string emblemaId, string relleno, string halo)
        {
            if (!trazos.TryGetValue(emblemaId ?? "", out var d)) return "";
            return $"<g transform=\"scale({N(100.0 / 512)})\"><path d=\"{d}\" fill=\"{halo}\" stroke=\"{halo}\" stroke-width=\"36\" stroke-linejoin=\"round\"/><path d=\"{d}\" fill=\"{relleno}\"/></g>";
        }

        static readonly Dictionary<string, string> insignias = new Dictionary<string, string>();

        /// <summary>La insignia de un título (`svgInsignia`): un medallón de bronce con su icono, en un lienzo de 100×100.</summary>
        public static string SvgInsignia(string tituloId, int lado)
        {
            insignias.TryGetValue(tituloId ?? "", out var d);
            return $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{lado}\" height=\"{lado}\" viewBox=\"0 0 100 100\"><circle cx=\"50\" cy=\"50\" r=\"46\" fill=\"#3a2d12\" stroke=\"#c9a227\" stroke-width=\"5\"/>"
                + (d != null ? $"<g transform=\"translate(19 19) scale(.118)\"><path d=\"{d}\" fill=\"#f4efe4\"/></g>" : "") + "</svg>";
        }

        /// <summary>El sigilo como SVG (`svgSigilo`) de `ancho` píxeles. Sin sigilo, el neutro; `granRey`, con la corona y el contorno dorado.</summary>
        public static string Svg(Sigilo sigilo, int ancho, bool granRey = false)
        {
            var n = Neutro;
            string O(string v, string defecto) => string.IsNullOrEmpty(v) ? defecto : v;
            var s = sigilo ?? n;
            var formaId = O(s.FormaId, n.FormaId);
            var forma = FormasSvg.TryGetValue(formaId, out var f) ? f : FormasSvg["clasico"];
            var relleno = ColorHex(O(s.ColorEmblemaId, n.ColorEmblemaId));
            var emblema = Emblema(O(s.EmblemaId, n.EmblemaId), relleno, HaloPara(relleno));
            var alto = granRey ? 150 : 120;
            var sb = new StringBuilder();
            sb.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{ancho}\" height=\"{(int)Math.Round(ancho * alto / 100.0)}\" viewBox=\"0 0 100 {alto}\">");
            // La corona va encima (y de −30 a 0 en el prototipo); todo baja 30 para que el lienzo empiece en 0 (Unity no admite el origen negativo).
            if (granRey) sb.Append("<g transform=\"translate(0 30)\">");
            sb.Append($"<defs><clipPath id=\"sg-{formaId}\"><path d=\"{forma.D}\"/></clipPath></defs>");
            sb.Append($"<g clip-path=\"url(#sg-{formaId})\">{Campo(O(s.CampoId, n.CampoId), ColorHex(O(s.ColorPrimarioId, n.ColorPrimarioId)), ColorHex(O(s.ColorSecundarioId, n.ColorSecundarioId)))}{Orla(O(s.OrlaId, n.OrlaId), forma.D, ColorHex(O(s.ColorOrlaId, n.ColorOrlaId)))}</g>");
            // `#0008` del prototipo: negro al 53 %.
            sb.Append(granRey ? $"<path d=\"{forma.D}\" fill=\"none\" stroke=\"#e0b83a\" stroke-width=\"5\"/>" : $"<path d=\"{forma.D}\" fill=\"none\" stroke=\"#000000\" stroke-opacity=\"0.533\" stroke-width=\"3\"/>");
            if (emblema != "") sb.Append($"<g transform=\"translate({N(forma.Cx - 50 * forma.S)} {N(forma.Cy - 50 * forma.S)}) scale({N(forma.S)})\">{emblema}</g>");
            if (granRey && corona != null)
                sb.Append($"<g transform=\"translate(21 -30) scale(.1172)\"><path d=\"{corona}\" fill=\"#1c1b1a\" stroke=\"#1c1b1a\" stroke-width=\"30\" stroke-linejoin=\"round\"/><path d=\"{corona}\" fill=\"#e0b83a\"/></g>");
            if (granRey) sb.Append("</g>");
            sb.Append("</svg>");
            return sb.ToString();
        }
    }
}
