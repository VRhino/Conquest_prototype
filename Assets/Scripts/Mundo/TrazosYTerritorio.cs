// Los caminos y el territorio del mapa, reunidos de la sesión como en el prototipo (BronzeAgeClient: `mundoDesdeProyeccion` en
// `src/mundo3d/estado.ts`): cada camino suavizado y con sus anclas, las fronteras partidas en exteriores e interiores (`partirFronteras`), la
// zona propia fusionada, el anillo de los mercenarios y los rellenos de territorio por Facción. Lo pintan `TrazosEnElMapa` y `TerritorioDelMapa`.
using System;
using System.Collections.Generic;
using System.Linq;
using BronzeAge.Contract.V1;

namespace ConquestTactics.Mundo
{
    public enum TipoDeTrazo { Camino, Frontera, Zona, Anillo }

    /// <summary>Una raya sobre el terreno (`TrazoEnVista`).</summary>
    public sealed class Trazo
    {
        public TipoDeTrazo Tipo;
        public List<(double x, double y)> Puntos;
        public double Ancho;
        public uint Color;
        public bool BajoNiebla, Discontinuo, Tenue;
        /// <summary>De un camino: 0 sendero, 1 camino de tierra (la calzada va como camino).</summary>
        public int Escalon;
        /// <summary>De un contorno: 1 o -1 según su sentido, para correr la raya medio ancho hacia dentro.</summary>
        public int HaciaDentro;
    }

    /// <summary>El relleno del territorio de una Facción (`RellenoDeTerritorio`).</summary>
    public sealed class RellenoDeTerritorio
    {
        public uint Color;
        public double Alfa;
        public List<List<(double x, double y)>> Contornos = new List<List<(double x, double y)>>();
        /// <summary>Par-impar (con huecos: la zona propia fusionada); si no, distinto de cero (junta las zonas que se solapan).</summary>
        public bool ParImpar;
    }

    public static class TrazosYTerritorio
    {
        /// <summary>Anchos de las rayas (u): caminos por escalón, fronteras, línea entre provincias, zona propia y anillo (`ANCHOS`).</summary>
        public const double AnchoSendero = 0.4, AnchoCamino = 0.65, AnchoFrontera = 0.12, AnchoProvincia = 0.06, AnchoZona = 0.18, AnchoAnillo = 0.1;
        /// <summary>Opacidad del relleno del territorio: el propio y los ajenos (`ALFA_TERRITORIO`).</summary>
        public const double AlfaPropio = 0.18, AlfaAjeno = 0.13;

        static (double x, double y) P(Punto p) => (p.X / 1000.0, p.Y / 1000.0);

        /// <summary>Los caminos suavizados, guardados por tramo (se rehacen solo si cambian sus puntos o sus anclas).</summary>
        static readonly Dictionary<string, (string firma, List<(double x, double y)> suave)> suaves = new Dictionary<string, (string, List<(double x, double y)>)>();

        /// <summary>`vados`: los puntos de `Rio.vados` (u): son anclas del suavizado y el camino cruza recto por ellos (`TrazadoDelMapa.PorLosVados`).</summary>
        public static (List<Trazo> trazos, List<RellenoDeTerritorio> rellenos) Reunir(Almacen a, string heroeId, double radioProteccionMercenarios,
            IReadOnlyList<(double x, double y)> vados = null)
        {
            var trazos = new List<Trazo>();
            var heroe = heroeId == null ? null : a.Buscar(TipoEntidad.Heroe, heroeId)?.Heroe;
            var miFaccion = heroe != null && heroe.HasFaccionId ? heroe.FaccionId : null;
            uint Color(string faccionId) => CosasDelMapa.ColorDeFaccion(a, faccionId);

            // Caminos: con sus anclas (plazas vistas y recordadas, pueblos, campamentos de mercenarios) fijas.
            var anclas = TrazadoDelMapa.Anclas(
                a.Todas(Entidad.CuerpoOneofCase.Asentamiento).Select(e => P(e.Asentamiento.Posicion))
                .Concat(a.Todas(Entidad.CuerpoOneofCase.FichaConocida).Select(e => P(e.FichaConocida.Posicion)))
                .Concat(a.Todas(Entidad.CuerpoOneofCase.Pueblo).Select(e => P(e.Pueblo.Posicion)))
                .Concat(a.Todas(Entidad.CuerpoOneofCase.CampamentoMercenarios).Select(e => P(e.CampamentoMercenarios.Posicion)))
                .Concat(vados ?? (IEnumerable<(double x, double y)>)Array.Empty<(double, double)>()));
            var firmaAnclas = string.Join(";", anclas.OrderBy(x => x).Select(x => $"{x.Item1},{x.Item2}"));
            foreach (var e in a.Todas(Entidad.CuerpoOneofCase.RedCaminos))
                foreach (var t in e.RedCaminos.Tramos)
                {
                    if (t.Puntos.Count < 2) continue;
                    var puntos = t.Puntos.Select(P).ToList();
                    var firma = firmaAnclas.GetHashCode() + ":" + string.Join(";", t.Puntos.Select(q => $"{q.X},{q.Y}"));
                    if (!suaves.TryGetValue(t.Id, out var s) || s.firma != firma)
                    {
                        // los vados por los que pasa (vértices suyos a menos de 0,01 u de un punto de `Rio.vados`): cruza recto por ellos
                        var porVados = vados?.Where(v => puntos.Any(q => Math.Abs(q.x - v.x) < 0.01 && Math.Abs(q.y - v.y) < 0.01)).ToList();
                        suaves[t.Id] = s = (firma, TrazadoDelMapa.PorLosVados(TrazadoDelMapa.CaminoSuave(puntos, anclas), puntos, porVados));
                    }
                    int escalon = t.Escalon == 0 ? 0 : 1; // en la Edad del Bronce no hay calzadas
                    trazos.Add(new Trazo { Tipo = TipoDeTrazo.Camino, Puntos = s.suave, Ancho = escalon == 0 ? AnchoSendero : AnchoCamino, Color = CosasDelMapa.Camino, BajoNiebla = true, Escalon = escalon });
                }

            // Las zonas: las que se ven (su silueta real) y las que se recuerdan (su disco recortado por los vecinos, como el motor).
            var fusionadas = a.Todas(Entidad.CuerpoOneofCase.ZonaFaccion).Select(e => e.ZonaFaccion).ToList();
            bool conZonaPropia = miFaccion != null && fusionadas.Any(z => z.FaccionId == miFaccion && z.Contornos.Count > 0);
            var zonas = new List<(string faccion, string faccionReal, List<(double x, double y)> contorno)>();
            var avistados = new List<(string, (double, double), string, IReadOnlyList<(double x, double y)>)>();
            foreach (var e in a.Todas(Entidad.CuerpoOneofCase.Asentamiento))
            {
                var p = e.Asentamiento;
                if (p.Zona.Count < 3) continue;
                var contorno = p.Zona.Select(P).ToList();
                zonas.Add((string.IsNullOrEmpty(p.FaccionId) ? "sin-" + p.Id : p.FaccionId, p.FaccionId, contorno));
                avistados.Add((p.Id, P(p.Posicion), p.FaccionId, contorno));
            }
            var conocidos = a.Todas(Entidad.CuerpoOneofCase.FichaConocida).Select(e => e.FichaConocida)
                .Select(c => (c.AsentamientoId, P(c.Posicion), c.FaccionId, c.HasRadioPotencial ? c.RadioPotencial / 1000.0 : (double?)null)).ToList();
            var recordadas = TrazadoDelMapa.ZonasDeLoRecordado(avistados, conocidos);
            foreach (var c in conocidos)
                if (recordadas.TryGetValue(c.Item1, out var zona)) zonas.Add((string.IsNullOrEmpty(c.Item3) ? "sin-" + c.Item1 : c.Item3, c.Item3, zona));

            // Fronteras: exteriores a trazos; entre provincias de la misma Facción, una línea tenue (las tuyas, encima de la niebla). La exterior
            // de lo tuyo la traza tu zona fusionada.
            foreach (var t in TrazadoDelMapa.PartirFronteras(zonas.Select(z => (z.faccion, (IReadOnlyList<(double x, double y)>)z.contorno)).ToList()))
            {
                var z = zonas[t.Zona];
                bool mia = miFaccion != null && z.faccion == miFaccion;
                if (t.Tipo == TrazadoDelMapa.TipoDeFrontera.Interior)
                    trazos.Add(new Trazo { Tipo = TipoDeTrazo.Frontera, Puntos = t.Puntos, Ancho = AnchoProvincia, Color = Color(z.faccionReal), BajoNiebla = !mia, Tenue = true });
                else if (!(mia && conZonaPropia))
                    trazos.Add(new Trazo { Tipo = TipoDeTrazo.Frontera, Puntos = t.Puntos, Ancho = AnchoFrontera, Color = Color(z.faccionReal), BajoNiebla = true, Discontinuo = true, HaciaDentro = TrazadoDelMapa.Sentido(z.contorno) });
            }
            // Encima: la zona fusionada (a trazos, cerrada) y el anillo de protección de los campamentos de mercenarios.
            foreach (var z in fusionadas)
                foreach (var c in z.Contornos)
                {
                    if (c.Puntos.Count < 3) continue;
                    var puntos = c.Puntos.Select(P).ToList();
                    int sentido = TrazadoDelMapa.Sentido(puntos);
                    puntos.Add(puntos[0]);
                    trazos.Add(new Trazo { Tipo = TipoDeTrazo.Zona, Puntos = puntos, Ancho = AnchoZona, Color = Color(z.FaccionId), BajoNiebla = z.FaccionId != miFaccion, Discontinuo = true, HaciaDentro = sentido });
                }
            foreach (var e in a.Todas(Entidad.CuerpoOneofCase.CampamentoMercenarios))
                trazos.Add(new Trazo { Tipo = TipoDeTrazo.Anillo, Puntos = TrazadoDelMapa.Circulo(P(e.CampamentoMercenarios.Posicion), radioProteccionMercenarios), Ancho = AnchoAnillo, Color = CosasDelMapa.AnilloMercenarios });

            // El relleno: uno por Facción con sus zonas juntas; el de tu Facción, con su zona fusionada (con huecos).
            var rellenos = new List<RellenoDeTerritorio>();
            var porFaccion = new Dictionary<string, RellenoDeTerritorio>();
            foreach (var z in zonas)
            {
                if (conZonaPropia && z.faccion == miFaccion) continue;
                if (!porFaccion.TryGetValue(z.faccion, out var r))
                {
                    porFaccion[z.faccion] = r = new RellenoDeTerritorio { Color = Color(z.faccionReal), Alfa = z.faccion == miFaccion ? AlfaPropio : AlfaAjeno };
                    rellenos.Add(r);
                }
                r.Contornos.Add(z.contorno);
            }
            foreach (var z in fusionadas)
                if (z.Contornos.Count > 0)
                    rellenos.Add(new RellenoDeTerritorio { Color = Color(z.FaccionId), Alfa = AlfaPropio, ParImpar = true, Contornos = z.Contornos.Select(c => c.Puntos.Select(P).ToList()).ToList() });
            return (trazos, rellenos);
        }
    }
}
