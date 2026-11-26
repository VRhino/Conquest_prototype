// Los mercenarios por contrato (paso 12 del bloque 4, tanda 2), calcados de BronzeAgeClient (`ui/mercenarios.ts`):
// - El tablón: cada oferta con su insignia de rango, su ficha (renombre, medallas e historial) y su tropa; el plazo y su precio como fichas que se
//   eligen (1, 3 o 7 días, o Permanente), los trabajos que alcanzan tus cargos, el objetivo por cercanía, «Firmarás como …» y «Contratar · precio».
//   Sale en la pieza Militar del campamento y a su puerta (paga tu almacén personal si resides, el carro que llevas o tus monedas) y, como
//   «Mercenarios de la región», en la Taberna de una plaza tuya: quien tiene cargo de firma paga con el fondo de monedas de la plaza o con las
//   suyas; el resto, con sus monedas (Víctor, 1a).
// - Facción › Mercenarios: lo que pide atención (los contratos acabados por valorar, guardados en el equipo hasta que caducan: Víctor, 5b), el
//   resumen, la barra de órdenes de un RTS (hasta 16 marcados; solo se encienden las órdenes que admiten todos) y cada contrato con su plazo, su
//   orden, «Prorrogar», «Mudar» y «Despedir».
// El cliente adelanta quién firma y qué orden cabe para no dejar pulsar lo que se va a rechazar; decide el motor, y su motivo sale tal cual.
using System;
using System.Collections.Generic;
using System.Linq;
using BronzeAge.Contract.V1;
using ConquestTactics.Mundo.Mapa;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.UIElements;

namespace ConquestTactics.Mundo.UI
{
    public partial class PantallasDeMundo
    {
        // ─────────────── los catálogos ───────────────

        static readonly Dictionary<string, string> RangoMercenario = new Dictionary<string, string>
        {
            ["a_sueldo"] = "A sueldo", ["lanza"] = "Lanza", ["capitan"] = "Capitán", ["veterano"] = "Veterano", ["senor_de_la_guerra"] = "Señor de la guerra",
        };

        static readonly Dictionary<string, (string nombre, string como)> MedallaMercenario = new Dictionary<string, (string, string)>
        {
            ["palabra_cumplida"] = ("Palabra cumplida", "contratos acabados por plazo"), ["cazador_de_bandidos"] = ("Cazador de bandidos", "campamentos de bandidos vencidos"),
            ["muralla"] = ("Muralla", "defensas ganadas de lo que guardaba"), ["buen_camino"] = ("Buen camino", "caravanas escoltadas hasta su destino"),
            ["asaltante"] = ("Asaltante", "asaltos ganados a pueblos, avanzadas o plazas"), ["campana"] = ("Campaña", "campañas acabadas dentro del ejército"),
            ["bien_valorado"] = ("Bien valorado", "valoraciones buenas"), ["leal"] = ("Leal", "días seguidos como permanente"),
        };

        /// <summary>El icono de cada medalla (`MEDALLA.icono`): ✋ 🏹 🧱 🐂 🔥 ⚑ 👍 ⚓.</summary>
        static readonly Dictionary<string, (string familia, string id)> IconoDeMedalla = new Dictionary<string, (string, string)>
        {
            ["palabra_cumplida"] = ("glifo", "palabra"), ["cazador_de_bandidos"] = ("glifo", "arco"), ["muralla"] = ("glifo", "ladrillos"), ["buen_camino"] = ("glifo", "buey"),
            ["asaltante"] = ("glifo", "fuego"), ["campana"] = ("simbolo", "bandera"), ["bien_valorado"] = ("glifo", "buena"), ["leal"] = ("glifo", "ancla"),
        };

        static readonly string[] GradoMedalla = { "", "bronce", "plata", "oro" };

        /// <summary>Los trabajos (`TRABAJO`): nombre, icono (familia e id) y qué hace.</summary>
        static readonly Dictionary<string, (string nombre, string familia, string icono, string texto)> TrabajoMercenario = new Dictionary<string, (string, string, string, string)>
        {
            ["defender"] = ("Defender", "glifo", "escudo", "Guarda una plaza, un pueblo o una avanzada tuya y la defiende si la atacan."),
            ["escoltar"] = ("Escoltar", "glifo", "buey", "Protege una caravana tuya (también un carro de acarreo, un encargo o una ruta)."),
            ["atacar"] = ("Atacar", "glifo", "atacar", "Un solo ataque contra bandidos o contra algo de una Facción en guerra contigo."),
            ["acompanar"] = ("Acompañar", "simbolo", "bandera", "Se une a una convocatoria o a un ejército tuyo y va con él."),
            ["permanente"] = ("Permanente", "glifo", "templo", "Hasta que lo despidas. Reside en la plaza que elijas y admite todas las órdenes. Solo lo firman el Rey o el Embajador."),
        };

        static readonly Dictionary<string, (string nombre, string familia, string icono, string texto)> OrdenMercenarioDe = new Dictionary<string, (string, string, string, string)>
        {
            ["guardar"] = ("Guardar", "glifo", "escudo", "Guarnece, escolta y defiende lo que guarda. Sin plazo."),
            ["ir_a"] = ("Ir a", "simbolo", "flecha", "Va a un punto del mapa. Caduca a las 24 h."),
            ["atacar"] = ("Atacar", "glifo", "atacar", "Un solo ataque contra ese objetivo. Caduca a las 24 h si no ataca, o con la paz."),
            ["unirse"] = ("Unirse", "simbolo", "bandera", "Se une a ese ejército o convocatoria, y a ningún otro."),
        };

        static readonly Dictionary<string, string> CargoDeFirma = new Dictionary<string, string>
        {
            ["rey"] = "Rey", ["embajador"] = "Embajador", ["general"] = "General", ["gobernador"] = "Gobernador", ["maestroCaravanas"] = "Maestro de Caravanas",
        };

        internal static readonly Dictionary<string, string> FinDeContrato = new Dictionary<string, string>
        {
            ["plazo"] = "se cumplió el plazo", ["despido"] = "despedido", ["plaza_perdida"] = "se perdió la plaza que lo firmó",
            ["sin_ciudadanos"] = "no quedan ciudadanos sin marca", ["faccion_perdida"] = "dejó de ser ciudadano",
        };

        static readonly string[] Trabajos = { "defender", "escoltar", "atacar", "acompanar", "permanente" };
        static readonly string[] TiposDeOrden = { "guardar", "ir_a", "atacar", "unirse" };

        /// <summary>Qué órdenes admite cada trabajo (`TRABAJOS_MERCENARIOS.admite`).</summary>
        static readonly Dictionary<string, string[]> AdmiteTrabajo = new Dictionary<string, string[]>
        {
            ["defender"] = new[] { "guardar", "ir_a", "atacar" }, ["escoltar"] = new[] { "guardar", "ir_a" }, ["atacar"] = new[] { "atacar", "ir_a" },
            ["acompanar"] = new[] { "unirse", "atacar", "ir_a", "guardar" }, ["permanente"] = new[] { "guardar", "ir_a", "atacar", "unirse" },
        };

        static readonly uint[] PlazosDeContrato = { 1, 3, 7 };

        static string Dias(uint d) => $"{d} día{(d == 1 ? "" : "s")}";

        // ─────────────── la insignia ───────────────

        /// <summary>La insignia del rango (`insigniaDeRango`): un escudo con tantos galones como su rango (1 a 5), del bronce al oro.</summary>
        static VisualElement InsigniaDeRango(uint rango, float lado)
        {
            var color = rango >= 5 ? new Color32(0xf1, 0xdc, 0xae, 0xff) : rango >= 4 ? new Color32(0xd8, 0xb8, 0x78, 0xff) : rango >= 3 ? new Color32(0xc9, 0xa1, 0x5e, 0xff)
                : rango >= 2 ? new Color32(0xa9, 0x79, 0x3f, 0xff) : new Color32(0x8a, 0x6a, 0x45, 0xff);
            var v = new VisualElement { pickingMode = PickingMode.Ignore };
            v.AddToClassList("merc-insignia");
            v.style.width = v.style.height = lado;
            var galones = (int)Math.Max(1, Math.Min(5, rango));
            v.generateVisualContent += ctx =>
            {
                var k = lado / 24f;
                Vector2 P(float x, float y) => new Vector2(x * k, y * k);
                var p = ctx.painter2D;
                p.fillColor = new Color32(0x14, 0x10, 0x0c, 0xff);
                p.strokeColor = color;
                p.lineWidth = 1.4f * k;
                p.BeginPath();
                p.MoveTo(P(12, 2)); p.LineTo(P(21, 5)); p.LineTo(P(21, 12));
                p.BezierCurveTo(P(21, 17), P(17, 20.5f), P(12, 22));
                p.BezierCurveTo(P(7, 20.5f), P(3, 17), P(3, 12));
                p.LineTo(P(3, 5)); p.ClosePath();
                p.Fill();
                p.Stroke();
                p.lineWidth = 1.5f * k;
                p.lineCap = LineCap.Round;
                for (var i = 0; i < galones; i++)
                {
                    var y = 18.5f - i * 2.8f;
                    p.BeginPath();
                    p.MoveTo(P(7, y)); p.LineTo(P(12, y - 2.4f)); p.LineTo(P(17, y));
                    p.Stroke();
                }
            };
            return v;
        }

        /// <summary>La ficha (`htmlFichaMercenario`): el renombre y su barra, las medallas con su grado y, entera, el historial.</summary>
        VisualElement FichaDeMercenario(FichaMercenario f, bool entera)
        {
            var v = Dialogo.Caja("merc-ficha");
            var siguiente = f.HasRenombreSiguienteRango ? f.RenombreSiguienteRango : (uint?)null;
            var proximo = RangoMercenario.Values.ElementAtOrDefault((int)f.Rango);
            v.Add(Dialogo.Texto($"Renombre {f.Renombre}{(siguiente != null ? $" / {siguiente} para {proximo ?? "el siguiente rango"}" : " · rango máximo")}", "merc-renombre-texto"));
            if (siguiente is uint s && s > 0)
            {
                var barra = Dialogo.Caja("merc-barra");
                barra.tooltip = $"{f.Renombre} de {s} para el rango siguiente";
                var i = Dialogo.Caja("merc-barra-i");
                i.style.width = Length.Percent(Math.Min(100f, 100f * f.Renombre / s));
                barra.Add(i);
                v.Add(barra);
            }
            var medallas = f.Medallas.Where(m => m.Grado > 0 || (entera && m.Cuenta > 0)).ToList();
            if (medallas.Count > 0)
            {
                var fila = Dialogo.Caja("merc-medallas");
                foreach (var m in medallas)
                {
                    var (nombre, como) = MedallaMercenario.TryGetValue(m.Id, out var x) ? x : (m.Id, "");
                    // Como el prototipo: el icono y, en la entera, la cuenta hacia el grado siguiente; el nombre, al pasar el ratón.
                    var chip = Dialogo.Caja($"merc-medalla merc-grado-{m.Grado}");
                    if (IconoDeMedalla.TryGetValue(m.Id, out var ic)) chip.Add(IconosDelMundo.Icono(ic.familia, ic.id, 15));
                    else chip.Add(Dialogo.Texto(nombre, "merc-medalla-texto"));
                    if (entera && m.HasSiguienteUmbral) chip.Add(Dialogo.Texto($"{m.Cuenta}/{m.SiguienteUmbral}", "merc-medalla-texto"));
                    chip.tooltip = $"{nombre}{(m.Grado > 0 ? $" ({GradoMedalla[Math.Min(3, (int)m.Grado)]})" : "")}{(entera ? $": {m.Cuenta} {como}{(m.HasSiguienteUmbral ? $"; el grado siguiente, a {m.SiguienteUmbral}" : "")}" : "")}";
                    fila.Add(chip);
                }
                v.Add(fila);
            }
            if (entera && f.Historial is HistorialMercenario h)
            {
                var fila = Dialogo.Caja("merc-historial");
                fila.tooltip = "Contratos firmados, cumplidos, despidos y valoraciones buenas y malas";
                fila.Add(IconosDelMundo.ConIcono("glifo", "aeda", Dialogo.Texto(h.Firmados.ToString(), "merc-historial-texto"), 13));
                fila.Add(Dialogo.Texto($"Cumplidos {h.Terminados}", "merc-historial-texto"));
                fila.Add(Dialogo.Texto($"Despidos {h.Despidos}", "merc-historial-texto"));
                fila.Add(IconosDelMundo.ConIcono("glifo", "buena", Dialogo.Texto(h.Buenas.ToString(), "merc-historial-texto"), 13));
                fila.Add(IconosDelMundo.ConIcono("glifo", "mala", Dialogo.Texto(h.Malas.ToString(), "merc-historial-texto"), 13));
                if (h.Firmados > 0) fila.Add(Dialogo.Texto($"{Math.Round(100.0 * h.Terminados / h.Firmados)} % cumplidos", "merc-historial-texto merc-fiable"));
                v.Add(fila);
            }
            return v;
        }

        // ─────────────── el mundo visto para elegir ───────────────

        sealed class OpcionMerc
        {
            public string Etiqueta, Plaza, LiderId;
            public ObjetivoMercenario Objetivo;
            public double? X, Y; // en unidades
            public bool Bandidos;
            public string Clave => ClaveDeObjetivo(Objetivo);
        }

        static string ClaveDeObjetivo(ObjetivoMercenario o) => o == null ? "" : $"{o.TipoCase}:{o.TipoCase switch { ObjetivoMercenario.TipoOneofCase.Punto => $"{o.Punto.X},{o.Punto.Y}", _ => o.ToString() }}";

        (double x, double y)? EnUnidades(Plan plan, Punto punto)
        {
            if (punto == null && plan == null) return null;
            var p = Instante.Posicion(plan, punto, AhoraMs);
            return (p.x / 1000, p.y / 1000);
        }

        /// <summary>Dónde está el jugador (`miPosicion`): su columna, la plaza o el campamento donde está.</summary>
        (double x, double y)? MiPosicionMerc(Heroe h)
        {
            var a = c.Sesion.Almacen;
            if (ColumnaDelHeroe(h) is Ejercito col) return EnUnidades(col.Plan, col.Posicion);
            var u = h.Ubicacion;
            if (u?.DondeCase == UbicacionHeroe.DondeOneofCase.AsentamientoId && a.Buscar(TipoEntidad.Asentamiento, u.AsentamientoId)?.Asentamiento?.Posicion is Punto pa) return (pa.X / 1000.0, pa.Y / 1000.0);
            if (u?.DondeCase == UbicacionHeroe.DondeOneofCase.CampamentoId && CampamentoActual(u.CampamentoId)?.Posicion is Punto pc) return (pc.X / 1000.0, pc.Y / 1000.0);
            return null;
        }

        static OpcionMerc Opcion(ObjetivoMercenario o, string etiqueta, Punto p, string plaza = null) =>
            new OpcionMerc { Objetivo = o, Etiqueta = etiqueta, Plaza = plaza, X = p != null ? p.X / 1000.0 : (double?)null, Y = p != null ? p.Y / 1000.0 : (double?)null };

        /// <summary>Lo propio que un contrato puede guardar, escoltar o acompañar (`objetivosPropios`).</summary>
        (List<OpcionMerc> plazas, List<OpcionMerc> pueblos, List<OpcionMerc> avanzadas, List<OpcionMerc> caravanas, List<OpcionMerc> ejercitos, List<OpcionMerc> convocatorias) ObjetivosPropios(Heroe h)
        {
            var a = c.Sesion.Almacen;
            var mias = a.Todas(Entidad.CuerpoOneofCase.Asentamiento).Select(e => e.Asentamiento).Where(x => h.HasFaccionId && x.FaccionId == h.FaccionId).ToList();
            var ids = new HashSet<string>(mias.Select(x => x.Id));
            var plazas = mias.Select(x => Opcion(new ObjetivoMercenario { AsentamientoId = x.Id }, x.HasNombre ? x.Nombre : x.Id, x.Posicion, x.Id)).ToList();
            var pueblos = a.Todas(Entidad.CuerpoOneofCase.Pueblo).Select(e => e.Pueblo).Where(x => ids.Contains(x.AsentamientoMadreId))
                .Select(x => Opcion(new ObjetivoMercenario { PuebloId = x.Id }, $"{(x.HasNivelAldea ? "Aldea" : "Pueblo")} de {NombreDePlaza(x.AsentamientoMadreId)}", x.Posicion, x.AsentamientoMadreId)).ToList();
            var avanzadas = a.Todas(Entidad.CuerpoOneofCase.Avanzada).Select(e => e.Avanzada).Where(x => ids.Contains(x.AsentamientoMadreId) && x.Estado == EstadoAvanzada.AvanzadaActiva)
                .Select(x => Opcion(new ObjetivoMercenario { AvanzadaId = x.Id }, $"Avanzada de {NombreDePlaza(x.AsentamientoMadreId)}", x.Posicion, x.AsentamientoMadreId)).ToList();
            var caravanas = a.Todas(Entidad.CuerpoOneofCase.Caravana).Select(e => e.Caravana).Where(x => !x.HasFaccionId || x.FaccionId == h.FaccionId).Where(x => ids.Contains(x.OrigenAsentamientoId))
                .Select(x =>
                {
                    var o = Opcion(new ObjetivoMercenario { CaravanaId = x.Id }, TextoDeCaravana(x), null, x.OrigenAsentamientoId);
                    if (EnUnidades(x.Plan, x.Posicion) is var (cx, cy)) { o.X = cx; o.Y = cy; }
                    return o;
                }).ToList();
            var ejercitos = a.Todas(Entidad.CuerpoOneofCase.Ejercito).Select(e => e.Ejercito).Where(x => x.FaccionId == h.FaccionId && x.Tipo == TipoColumna.ColumnaEjercito)
                .Select(x =>
                {
                    var o = Opcion(new ObjetivoMercenario { EjercitoId = x.Id }, $"Ejército de {Textos.NombreDeHeroe(a, x.LiderId)}", null);
                    o.LiderId = x.LiderId;
                    if (EnUnidades(x.Plan, x.Posicion) is var (ex, ey)) { o.X = ex; o.Y = ey; }
                    return o;
                }).ToList();
            var convocatorias = a.Todas(Entidad.CuerpoOneofCase.Convocatoria).Select(e => e.Convocatoria).Where(x => x.FaccionId == h.FaccionId)
                .Select(x =>
                {
                    var lugar = x.LugarEsCampamento ? CampamentoActual(x.LugarId)?.Posicion : a.Buscar(TipoEntidad.Asentamiento, x.LugarId)?.Asentamiento?.Posicion;
                    var o = Opcion(new ObjetivoMercenario { ConvocatoriaId = x.Id }, $"Convocatoria de {Textos.NombreDeHeroe(a, x.LiderId)}", lugar);
                    o.LiderId = x.LiderId;
                    return o;
                }).ToList();
            return (plazas, pueblos, avanzadas, caravanas, ejercitos, convocatorias);
        }

        string TextoDeCaravana(Caravana x) =>
            x.HasNombre ? x.Nombre + (x.HasDestinoAsentamientoId ? $" (a {NombreDePlaza(x.DestinoAsentamientoId)})" : "")
                : $"Caravana de {NombreDePlaza(x.OrigenAsentamientoId)}{(x.HasDestinoAsentamientoId ? $" a {NombreDePlaza(x.DestinoAsentamientoId)}" : "")}";

        /// <summary>Las Facciones en guerra con la tuya.</summary>
        HashSet<string> EnGuerraCon(Heroe h)
        {
            var r = new HashSet<string>();
            foreach (var x in c.Sesion.Almacen.Todas(Entidad.CuerpoOneofCase.Relacion).Select(e => e.Relacion))
            {
                if (x.Tipo != TipoRelacion.RelacionGuerra || x.Estado != EstadoRelacion.RelacionActiva) continue;
                if (x.FaccionAId == h.FaccionId) r.Add(x.FaccionBId);
                if (x.FaccionBId == h.FaccionId) r.Add(x.FaccionAId);
            }
            return r;
        }

        /// <summary>Lo que se puede atacar (`objetivosAtacables`): los bandidos a la vista y las plazas de una Facción en guerra contigo.</summary>
        List<OpcionMerc> ObjetivosAtacables(Heroe h)
        {
            var a = c.Sesion.Almacen;
            var guerra = EnGuerraCon(h);
            var r = a.Todas(Entidad.CuerpoOneofCase.CampamentoBandidos).Select(e => e.CampamentoBandidos)
                .Select(x => { var o = Opcion(new ObjetivoMercenario { CampamentoId = x.Id }, $"Bandidos (nivel {x.Nivel})", x.Posicion); o.Bandidos = true; return o; }).ToList();
            r.AddRange(a.Todas(Entidad.CuerpoOneofCase.Asentamiento).Select(e => e.Asentamiento).Where(x => guerra.Contains(x.FaccionId))
                .Select(x => Opcion(new ObjetivoMercenario { AsentamientoId = x.Id }, $"{(x.HasNombre ? x.Nombre : x.Id)} (en guerra)", x.Posicion)));
            return r;
        }

        /// <summary>Por cercanía, con la distancia en la etiqueta (`porCercania`): en un mapa grande, lo primero que se mira es qué queda a mano.</summary>
        List<OpcionMerc> PorCercania(Heroe h, IEnumerable<OpcionMerc> lista, (double x, double y)? desde = null)
        {
            var yo = desde ?? MiPosicionMerc(h);
            var l = lista.ToList();
            if (yo == null) return l;
            double D(OpcionMerc o) => o.X is double x && o.Y is double y ? Math.Sqrt((x - yo.Value.x) * (x - yo.Value.x) + (y - yo.Value.y) * (y - yo.Value.y)) : double.PositiveInfinity;
            return l.OrderBy(D).Select(o => { var d = D(o); if (!double.IsInfinity(d)) o.Etiqueta += $" · a {Math.Round(d)} u"; return o; }).ToList();
        }

        string TextoDeObjetivo(ObjetivoMercenario o)
        {
            if (o == null) return "—";
            var a = c.Sesion.Almacen;
            switch (o.TipoCase)
            {
                case ObjetivoMercenario.TipoOneofCase.AsentamientoId: return NombreDePlaza(o.AsentamientoId);
                case ObjetivoMercenario.TipoOneofCase.PuebloId:
                    var pu = a.Buscar(TipoEntidad.Pueblo, o.PuebloId)?.Pueblo;
                    return pu != null ? $"{(pu.HasNivelAldea ? "Aldea" : "Pueblo")} de {NombreDePlaza(pu.AsentamientoMadreId)}" : "Un pueblo";
                case ObjetivoMercenario.TipoOneofCase.AvanzadaId:
                    var av = a.Buscar(TipoEntidad.Avanzada, o.AvanzadaId)?.Avanzada;
                    return av != null ? $"Avanzada de {NombreDePlaza(av.AsentamientoMadreId)}" : "Una avanzada";
                case ObjetivoMercenario.TipoOneofCase.CaravanaId:
                    var k = a.Buscar(TipoEntidad.Caravana, o.CaravanaId)?.Caravana;
                    return k != null ? TextoDeCaravana(k) : "Una caravana";
                case ObjetivoMercenario.TipoOneofCase.CampamentoId: return "Bandidos";
                case ObjetivoMercenario.TipoOneofCase.EjercitoId:
                    var e = a.Buscar(TipoEntidad.Ejercito, o.EjercitoId)?.Ejercito;
                    return e != null ? $"Ejército de {Textos.NombreDeHeroe(a, e.LiderId)}" : "Un ejército";
                case ObjetivoMercenario.TipoOneofCase.ConvocatoriaId:
                    var cv = a.Buscar(TipoEntidad.Convocatoria, o.ConvocatoriaId)?.Convocatoria;
                    return cv != null ? $"Convocatoria de {Textos.NombreDeHeroe(a, cv.LiderId)}" : "Una convocatoria";
                case ObjetivoMercenario.TipoOneofCase.FaccionId: return a.Buscar(TipoEntidad.Faccion, o.FaccionId)?.Faccion?.Nombre ?? o.FaccionId;
                case ObjetivoMercenario.TipoOneofCase.Punto: return $"Punto ({Math.Round(o.Punto.X / 1000.0)}, {Math.Round(o.Punto.Y / 1000.0)})";
                default: return "—";
            }
        }

        // ─────────────── quién puede (§3.2 y §6.5) ───────────────

        sealed class PuestosMerc
        {
            public bool Rey, Embajador;
            public List<string> Cargos = new List<string>();
            public bool Local(string cargo, string plaza) => plaza != null && Cargos.Contains($"{cargo}@{plaza}");
            public bool AlgunLocal(string cargo) => Cargos.Any(x => x.StartsWith(cargo + "@"));
            public bool Alguno => Rey || Embajador || Cargos.Any(x => x.Contains("@") && (x.StartsWith("general@") || x.StartsWith("gobernador@") || x.StartsWith("maestroCaravanas@")));
        }

        PuestosMerc MisPuestos(Heroe h)
        {
            var f = MiFaccion(h);
            var p = new PuestosMerc();
            if (f == null) return p;
            p.Cargos = f.Miembros.FirstOrDefault(m => m.HeroeId == h.Id)?.Cargos.ToList() ?? new List<string>();
            p.Rey = (f.HasReyId && f.ReyId == h.Id) || p.Cargos.Contains("rey");
            p.Embajador = (f.HasEmbajadorId && f.EmbajadorId == h.Id) || p.Cargos.Contains("embajador");
            return p;
        }

        /// <summary>Con qué puesto firmaría el jugador ese trabajo sobre ese objetivo (el de más rango que alcance), o null (`puestoParaFirmar`).</summary>
        string PuestoParaFirmar(PuestosMerc yo, string trabajo, OpcionMerc o, Heroe h)
        {
            if (yo.Rey) return "Rey";
            if (yo.Embajador) return "Embajador";
            string L(string cargo) => yo.Local(cargo, o.Plaza) ? $"{CargoDeFirma[cargo]} de {NombreDePlaza(o.Plaza)}" : null;
            switch (trabajo)
            {
                case "defender": return L("general") ?? L("gobernador");
                case "escoltar": return L("gobernador") ?? L("maestroCaravanas");
                case "atacar": return yo.AlgunLocal("general") ? "General" : o.Bandidos && yo.AlgunLocal("gobernador") ? "Gobernador" : null;
                case "acompanar": return yo.AlgunLocal("general") && o.LiderId == h.Id ? "General" : null;
                default: return null;
            }
        }

        /// <summary>¿Podría el jugador firmar hoy este contrato? (prorrogar y despedir los hace «quien podría firmarlo»).</summary>
        static bool PuedeGestionar(PuestosMerc yo, Contrato k)
        {
            if (yo.Rey || yo.Embajador) return true;
            if (!k.HasAsentamientoId) return false;
            bool En(string cargo) => yo.Local(cargo, k.AsentamientoId);
            switch (k.Trabajo)
            {
                case "defender": case "atacar": return En("general") || En("gobernador");
                case "escoltar": return En("gobernador") || En("maestroCaravanas");
                case "acompanar": return En("general");
                default: return false;
            }
        }

        /// <summary>Las órdenes que el jugador puede dar a ese contrato: las que admite su trabajo (§6.4) y le deja dar su cargo (§6.5).</summary>
        static HashSet<string> OrdenesPosibles(PuestosMerc yo, Contrato k, string residencia)
        {
            var admite = AdmiteTrabajo.TryGetValue(k.Trabajo, out var x) ? x : new string[0];
            if (k.HasFinAplazado) return new HashSet<string>();
            if (yo.Rey || yo.Embajador) return new HashSet<string>(admite);
            var plaza = k.Trabajo == "permanente" ? residencia : k.HasAsentamientoId ? k.AsentamientoId : null;
            var mias = new HashSet<string>();
            if (yo.Local("general", plaza)) mias.UnionWith(new[] { "guardar", "atacar", "unirse", "ir_a" });
            if (yo.Local("gobernador", plaza)) mias.UnionWith(new[] { "guardar", "atacar", "ir_a" });
            if (k.Trabajo == "escoltar" && k.HasAsentamientoId && yo.Local("maestroCaravanas", k.AsentamientoId)) mias.UnionWith(new[] { "guardar", "ir_a" });
            return new HashSet<string>(admite.Where(mias.Contains));
        }

        string TextoDePuesto(string puesto)
        {
            if (string.IsNullOrEmpty(puesto)) return "orden inicial";
            var partes = puesto.Split('@');
            var cargo = CargoDeFirma.TryGetValue(partes[0], out var n) ? n : partes[0];
            return partes.Length > 1 ? $"{cargo} de {NombreDePlaza(partes[1])}" : cargo;
        }

        /// <summary>Un rato del mundo en tiempo real (`textoEnTiempoReal`): el reloj del mundo puede ir más deprisa.</summary>
        string EnTiempoReal(double msDeMundo) => Duracion(Math.Round(Math.Max(0, msDeMundo) / Math.Max(1e-6, c.Sesion.FactorReloj) / 60_000));

        // ─────────────── el tablón ───────────────

        /// <summary>De dónde sale el pago de un contrato (`pagar_con`): el nombre, lo que hay y si es en monedas.</summary>
        struct FuenteMerc { public string PagarCon, Nombre; public double Saldo; public bool EnMonedas; }

        /// <summary>Lo que el jugador eligió en cada oferta (trabajo, plazo y objetivo): vive entre repintados.</summary>
        readonly Dictionary<string, (string trabajo, uint plazo)> eleccionMerc = new Dictionary<string, (string, uint)>();
        readonly Dictionary<string, int> objetivoMerc = new Dictionary<string, int>();
        string ordenTablon = "renombre", pagarConMerc;

        /// <summary>En el campamento: tu almacén personal si resides aquí, el carro que llevas o tus monedas (`fuentesDeContrato`).</summary>
        List<FuenteMerc> FuentesDeContrato(Heroe h, CampamentoMercenarios camp)
        {
            var r = FuentesDePago(h, camp).Where(f => f.pagarCon != "almacenPersonal" || camp.ResidentesIds.Contains(h.Id))
                .Select(f => new FuenteMerc { PagarCon = f.pagarCon, Nombre = f.nombre, Saldo = f.oro }).ToList();
            if (h.Monedas > 0) r.Add(new FuenteMerc { PagarCon = "monedas", Nombre = "tus monedas", Saldo = h.Monedas, EnMonedas = true });
            return r;
        }

        /// <summary>Desde la Taberna de la plaza (Víctor, 1a): quien firma con la plaza (Rey, Embajador o Gobernador) paga con el fondo de monedas de la
        /// plaza o con las suyas; los demás cargos, con sus monedas.</summary>
        List<FuenteMerc> FuentesDeRegion(Heroe h, Asentamiento a)
        {
            var yo = MisPuestos(h);
            var r = new List<FuenteMerc>();
            if (yo.Rey || yo.Embajador || TitularDe(a, "gobernador") == h.Id)
                r.Add(new FuenteMerc { PagarCon = "fondoPlaza", Nombre = "el fondo de la plaza", Saldo = a.Forja?.Fondo ?? 0, EnMonedas = true });
            if (h.Monedas > 0) r.Add(new FuenteMerc { PagarCon = "monedas", Nombre = "tus monedas", Saldo = h.Monedas, EnMonedas = true });
            return r;
        }

        /// <summary>¿Sale «Mercenarios de la región» en esta plaza? Tuya, con la Taberna activa y con un cargo que firme algún trabajo.</summary>
        bool ContrataDesdePlaza(Asentamiento a, Heroe yo) =>
            Propia(a, yo) && Internos(a).Any(e => e.Tipo == "taberna" && Activo(e)) && MisPuestos(yo).Alguno;

        static VisualElement CosteMerc(double n, bool enMonedas, float lado = 15)
        {
            var fila = Dialogo.Caja("merc-cifra");
            fila.Add(enMonedas ? IconosDelMundo.Icono("glifo", "monedas", lado) : IconosDelMundo.Icono("recurso", "oro", lado));
            fila.Add(Dialogo.Texto(enMonedas ? n.ToString("N0", Es) : Cifra(n), "merc-cifra-texto"));
            return fila;
        }

        static string Cuanto(double n, bool enMonedas) => enMonedas ? $"{n.ToString("N0", Es)} monedas" : $"{Cifra(n)} de oro";

        /// <summary>El tablón (`htmlTablonMercenarios`): la cabecera con lo que paga, «Ordenar por» y las ofertas. Con `plaza`, el de la región desde la
        /// Taberna de esa plaza.</summary>
        void TablonDeMercenarios(VisualElement v, IList<OfertaMercenario> tablon, Heroe h, List<FuenteMerc> fuentes, Asentamiento plaza, Action<string> error, Action repintar)
        {
            if (HojaDePlaza is StyleSheet hoja && !v.styleSheets.Contains(hoja)) v.styleSheets.Add(hoja);
            var fuente = fuentes.FirstOrDefault(f => f.PagarCon == pagarConMerc);
            if (fuente.PagarCon == null && fuentes.Count > 0) fuente = fuentes[0];
            var hay = fuente.PagarCon != null;
            var cab = Dialogo.Caja("merc-tablon-cab");
            cab.Add(ConAyuda(Dialogo.Texto(plaza != null ? "Mercenarios de la región" : "Tablón de mercenarios", "pacto-plaza merc-titulo"), plaza != null
                ? "Los mercenarios que se ofrecen en los campamentos de la región de esta plaza: se contratan desde aquí, sin ir. El Rey, el Embajador o el Gobernador pagan con el fondo de monedas de la plaza (o con las suyas); los demás cargos, con sus monedas. Lo pagado no se devuelve nunca. Prorrogar sigue siendo en persona."
                : "Héroes bot que se ofrecen a sueldo. Firma el Rey, el Embajador o el cargo local que alcance (el de más rango), aquí en persona (dentro o con tu columna a la puerta). Se paga con tu almacén personal, si resides aquí, con el carro que llevas o con tus monedas, a la tarifa en monedas. Lo pagado no se devuelve nunca. Mientras dura, el mercenario es ciudadano de tu Facción con marca, y sin orden solo se defiende: se le manda desde Facción › Mercenarios."));
            if (!hay)
            {
                var sin = Dialogo.Texto(plaza != null ? "Sin monedas a mano" : "Sin oro a mano", "merc-bolsa merc-bolsa-sin");
                sin.name = "merc-bolsa";
                cab.Add(sin);
            }
            else if (fuentes.Count > 1)
            {
                var pagar = Dialogo.Caja("merc-bolsa merc-pagar");
                pagar.Add(Dialogo.Texto("Pagar con", "merc-pagar-texto"));
                var lista = new DropdownField(fuentes.Select(f => $"{char.ToUpper(f.Nombre[0])}{f.Nombre.Substring(1)} ({Cuanto(f.Saldo, f.EnMonedas)})").ToList(), fuentes.FindIndex(f => f.PagarCon == fuente.PagarCon)) { name = "merc-pagar" };
                lista.AddToClassList("merc-desplegable");
                lista.RegisterValueChangedCallback(_ => { pagarConMerc = fuentes[lista.index].PagarCon; repintar(); });
                pagar.Add(lista);
                cab.Add(pagar);
            }
            else
            {
                var bolsa = Dialogo.Caja("merc-bolsa");
                bolsa.name = "merc-bolsa";
                bolsa.tooltip = $"Se paga con {fuente.Nombre}";
                bolsa.Add(CosteMerc(fuente.Saldo, fuente.EnMonedas));
                bolsa.Add(Dialogo.Texto(fuente.EnMonedas ? "monedas" : "en " + fuente.Nombre, "merc-bolsa-texto"));
                cab.Add(bolsa);
            }
            v.Add(cab);
            double Renombre(OfertaMercenario o) => o.Ficha?.Renombre ?? 0;
            double Precio(OfertaMercenario o) => o.Tarifas.FirstOrDefault(t => t.HasPlazoDias && t.PlazoDias == 1)?.Oro ?? double.MaxValue;
            var ofertas = (ordenTablon == "renombre" ? tablon.OrderByDescending(Renombre) : tablon.OrderBy(Precio)).ToList();
            if (ofertas.Count > 1)
            {
                var orden = Dialogo.Caja("merc-orden-tablon");
                orden.Add(Dialogo.Texto("Ordenar por", "merc-orden-texto"));
                foreach (var (id, texto) in new[] { ("renombre", "renombre"), ("precio", "precio") })
                {
                    var b = Dialogo.Boton(texto, "merc-chip" + (ordenTablon == id ? " activo" : ""), () => { ordenTablon = id; repintar(); });
                    b.name = "merc-ordenar-" + id;
                    orden.Add(b);
                }
                v.Add(orden);
            }
            if (ofertas.Count == 0) { v.Add(Dialogo.Texto(plaza != null ? "Nadie se ofrece ahora en los campamentos de la región." : "Nadie se ofrece ahora en este campamento.", "campo-ayuda")); return; }
            var rejilla = Dialogo.Caja("merc-ofertas");
            rejilla.name = "merc-ofertas";
            var yo = MisPuestos(h);
            var propios = ObjetivosPropios(h);
            var atacables = ObjetivosAtacables(h);
            foreach (var o in ofertas) rejilla.Add(Oferta(o, h, yo, propios, atacables, hay ? fuente : (FuenteMerc?)null, plaza, error, repintar));
            v.Add(rejilla);
        }

        /// <summary>Una oferta del tablón (`htmlOferta`).</summary>
        VisualElement Oferta(OfertaMercenario o, Heroe h, PuestosMerc yo,
            (List<OpcionMerc> plazas, List<OpcionMerc> pueblos, List<OpcionMerc> avanzadas, List<OpcionMerc> caravanas, List<OpcionMerc> ejercitos, List<OpcionMerc> convocatorias) propios,
            List<OpcionMerc> atacables, FuenteMerc? fuente, Asentamiento plaza, Action<string> error, Action repintar)
        {
            var a = c.Sesion.Almacen;
            var id = o.HeroeId;
            var saldo = fuente?.Saldo ?? 0;
            var enMonedas = fuente?.EnMonedas ?? false;
            var el = eleccionMerc.TryGetValue(id, out var e0) ? e0 : ("defender", 3u);
            var herido = o.HasHeridoHastaMs && o.HeridoHastaMs > AhoraMs;
            var publico = a.Buscar(TipoEntidad.Heroe, id)?.Heroe;
            double? PrecioDe(uint? plazo) { var t = o.Tarifas.FirstOrDefault(x => plazo == null ? !x.HasPlazoDias : x.HasPlazoDias && x.PlazoDias == plazo); return t == null ? (double?)null : enMonedas ? t.Monedas : t.Oro / 1000.0; }
            var desde = plaza?.Posicion != null ? (plaza.Posicion.X / 1000.0, plaza.Posicion.Y / 1000.0) : ((double, double)?)null;
            List<OpcionMerc> Candidatos(string t) => t switch
            {
                "defender" => propios.plazas.Concat(propios.pueblos).Concat(propios.avanzadas).ToList(),
                "escoltar" => propios.caravanas,
                "atacar" => atacables,
                "acompanar" => propios.convocatorias.Concat(propios.ejercitos).ToList(),
                _ => propios.plazas,
            };
            // Solo los objetivos que alguno de tus cargos alcanza; el permanente, solo con Rey o Embajador.
            var alcanzables = Trabajos.ToDictionary(t => t, t => PorCercania(h, Candidatos(t).Where(x => t == "permanente" ? yo.Rey || yo.Embajador : PuestoParaFirmar(yo, t, x, h) != null)
                .Select(x => new OpcionMerc { Etiqueta = x.Etiqueta, Plaza = x.Plaza, LiderId = x.LiderId, Objetivo = x.Objetivo, X = x.X, Y = x.Y, Bandidos = x.Bandidos }), desde));
            var disponibles = Trabajos.Where(t => alcanzables[t].Count > 0).ToList();
            var trabajo = disponibles.Contains(el.Item1) ? el.Item1 : disponibles.FirstOrDefault() ?? el.Item1;
            var puede = h.HasFaccionId && !herido && disponibles.Count > 0;

            var carta = Dialogo.Caja("merc-oferta" + (herido ? " herido" : ""));
            carta.name = "merc-oferta-" + id;
            var cab = Dialogo.Caja("merc-oferta-cab");
            if (o.Ficha != null) cab.Add(InsigniaDeRango(o.Ficha.Rango, 36));
            var nombre = Dialogo.Caja("merc-nombre");
            nombre.Add(Dialogo.Texto(Textos.NombreDeHeroe(a, id), "merc-nombre-texto"));
            var sub = new List<string>();
            if (o.Ficha != null && RangoMercenario.TryGetValue(o.Ficha.RangoId, out var rango)) sub.Add(rango);
            if (publico != null) sub.Add($"nivel {publico.Nivel}");
            if (herido) sub.Add($"herido {EnTiempoReal(o.HeridoHastaMs - AhoraMs)}");
            nombre.Add(Dialogo.Texto(string.Join(" · ", sub), "merc-sub" + (herido ? " merc-herido" : "")));
            cab.Add(nombre);
            carta.Add(cab);
            if (plaza != null)
            {
                var pos = CampamentoActual(o.CampamentoId)?.Posicion;
                var lejos = pos != null && plaza.Posicion != null ? $" · a {Math.Round(Math.Sqrt(Math.Pow((pos.X - plaza.Posicion.X) / 1000.0, 2) + Math.Pow((pos.Y - plaza.Posicion.Y) / 1000.0, 2)))} u" : "";
                carta.Add(IconosDelMundo.ConIcono("glifo", "campamento", Dialogo.Texto("De un campamento de la región" + lejos, "merc-sub"), 14));
            }
            if (o.Ficha != null) carta.Add(FichaDeMercenario(o.Ficha, true));
            if (o.Tropa.Count > 0)
            {
                var tropa = Dialogo.Caja("merc-tropa");
                foreach (var s in o.Tropa)
                {
                    var tid = s.TropaId;
                    var pieza = Dialogo.Caja("merc-tropa-pieza");
                    pieza.tooltip = $"{NombreDeTropa(tid)}, nivel {s.Nivel}";
                    if (PiezasDeTropa.Miniatura(tid, 26) is VisualElement mini) pieza.Add(mini);
                    pieza.Add(Dialogo.Texto(s.Cantidad.ToString(), "merc-tropa-texto"));
                    tropa.Add(pieza);
                }
                carta.Add(tropa);
            }
            else carta.Add(Dialogo.Texto("Sin tropa propia", "merc-sub"));

            // Plazo y precio: el precio se ve siempre; se elige solo si ese plazo se puede firmar.
            carta.Add(Dialogo.Texto("PLAZO Y PRECIO", "merc-seccion"));
            var tarifas = Dialogo.Caja("merc-tarifas");
            foreach (var (valor, texto, plazo) in PlazosDeContrato.Select(d => (d.ToString(), Dias(d), (uint?)d)).Append(("perm", "Permanente", (uint?)null)))
            {
                var precio = PrecioDe(plazo);
                var elegida = puede && (trabajo == "permanente" ? valor == "perm" : valor == el.Item2.ToString());
                var caro = precio != null && precio > saldo;
                var elegible = puede && (valor != "perm" ? disponibles.Any(t => t != "permanente") : disponibles.Contains("permanente"));
                VisualElement ficha;
                if (elegible)
                {
                    var p0 = plazo;
                    var b = new Button(() =>
                    {
                        // «Permanente» es a la vez plazo y trabajo; un plazo devuelve a uno temporal si se venía del permanente.
                        eleccionMerc[id] = p0 == null ? ("permanente", el.Item2) : (trabajo == "permanente" ? disponibles.FirstOrDefault(t => t != "permanente") ?? "defender" : trabajo, p0.Value);
                        repintar();
                    });
                    if (caro && fuente != null) b.tooltip = $"Te faltan {Cuanto(precio.Value - saldo, enMonedas)}";
                    ficha = b;
                }
                else ficha = new VisualElement();
                ficha.name = $"merc-tarifa-{id}-{valor}";
                ficha.AddToClassList("merc-tarifa");
                ficha.EnableInClassList("fija", !elegible);
                ficha.EnableInClassList("activo", elegida);
                ficha.EnableInClassList("caro", caro);
                ficha.Add(Dialogo.Texto(texto, "merc-tarifa-plazo"));
                if (precio != null) ficha.Add(CosteMerc(precio.Value, enMonedas, 13)); else ficha.Add(Dialogo.Texto("—", "merc-tarifa-plazo"));
                tarifas.Add(ficha);
            }
            carta.Add(tarifas);

            var elegidoPrecio = trabajo == "permanente" ? PrecioDe(null) : PrecioDe(el.Item2);
            var motivo = !h.HasFaccionId ? "Necesitas una Facción para contratar."
                : herido ? "Está herido: espera a que sane."
                : !puede ? "Ninguno de tus cargos puede firmarle un trabajo sobre algo que tengas a la vista."
                : fuente == null ? (plaza != null ? "Desde la taberna se paga con el fondo de monedas de la plaza (firma el Rey, el Embajador o el Gobernador) o con tus monedas, y no tienes ninguna de las dos cosas." : "Se paga con tu almacén personal (si resides aquí), con el carro que llevas aquí o con tus monedas, y no tienes ninguno: llega con tu columna para contratar.")
                : elegidoPrecio != null && elegidoPrecio > saldo ? $"Te faltan {Cuanto(elegidoPrecio.Value - saldo, enMonedas)}." : null;
            if (puede)
            {
                carta.Add(Dialogo.Texto("TRABAJO", "merc-seccion"));
                var trabajos = Dialogo.Caja("merc-trabajos");
                foreach (var t in disponibles)
                {
                    var t0 = t;
                    var (tn, fam, ico, tt) = TrabajoMercenario[t];
                    var b = new Button(() => { eleccionMerc[id] = (t0, el.Item2); repintar(); }) { name = $"merc-trabajo-{id}-{t}", tooltip = tt };
                    b.AddToClassList("merc-trabajo");
                    b.EnableInClassList("activo", t == trabajo);
                    b.Add(IconosDelMundo.Icono(fam, ico, 18));
                    b.Add(Dialogo.Texto(tn, "merc-trabajo-texto"));
                    trabajos.Add(b);
                }
                carta.Add(trabajos);
                var opciones = alcanzables[trabajo];
                var clave = id + "|" + trabajo;
                var indice = objetivoMerc.TryGetValue(clave, out var i0) && i0 < opciones.Count ? i0 : 0;
                carta.Add(Dialogo.Texto(trabajo == "permanente" ? "Residirá en" : "Objetivo", "merc-etiqueta"));
                var objetivo = new DropdownField(opciones.Select(x => x.Etiqueta).ToList(), indice) { name = "merc-objetivo-" + id };
                objetivo.AddToClassList("merc-desplegable");
                objetivo.RegisterValueChangedCallback(_ => { objetivoMerc[clave] = objetivo.index; repintar(); });
                carta.Add(objetivo);
                var pie = Dialogo.Caja("merc-oferta-pie");
                var firma = PuestoParaFirmar(yo, trabajo == "permanente" ? "defender" : trabajo, opciones[indice], h);
                if (yo.Rey) firma = "Rey"; else if (yo.Embajador) firma = "Embajador";
                pie.Add(Dialogo.Texto(firma != null ? $"Firmarás como {firma}" : "", "merc-firma"));
                Button contratar = null;
                contratar = new Button(async () =>
                {
                    var elegida = alcanzables[trabajo][indice];
                    var orden = new OrdenContratarMercenario { MercenarioId = id, Trabajo = trabajo, PagarCon = fuente?.PagarCon ?? "" };
                    if (trabajo == "permanente") { orden.Objetivo = new ObjetivoMercenario { FaccionId = h.FaccionId }; orden.ResidenciaAsentamientoId = elegida.Objetivo.AsentamientoId; }
                    else { orden.Objetivo = elegida.Objetivo; orden.PlazoDias = el.Item2; }
                    var r = await OrdenDelPanel(new Orden { ContratarMercenario = orden }, error, repintar, contratar);
                    if (r?.Resultado == Resultado.OrdenAceptada) eleccionMerc.Remove(id);
                }) { name = "merc-contratar-" + id };
                contratar.AddToClassList("boton-primario");
                contratar.AddToClassList("merc-contratar");
                contratar.Add(Dialogo.Texto("Contratar", "merc-contratar-texto"));
                if (elegidoPrecio != null) contratar.Add(CosteMerc(elegidoPrecio.Value, enMonedas));
                contratar.SetEnabled(motivo == null);
                if (motivo != null) contratar.tooltip = motivo;
                pie.Add(contratar);
                carta.Add(pie);
            }
            if (motivo != null) carta.Add(Dialogo.Texto(motivo, "merc-motivo"));
            return carta;
        }

        // ─────────────── Facción › Mercenarios ───────────────

        /// <summary>Los mercenarios marcados para la barra de órdenes y la orden elegida: viven entre repintados.</summary>
        readonly HashSet<string> marcadosMerc = new HashSet<string>();
        string tipoDeOrdenMerc = "guardar";
        int lugarMerc, objetivoOrdenMerc;
        int? xMerc, yMerc;

        bool VencePronto(Contrato k) => k.HasVenceEnMs && k.HasPlazoDias && k.VenceEnMs - AhoraMs < k.PlazoDias * 86_400_000.0 * 0.2;

        double HorasDeValoracion
        {
            get { try { return (double?)c.Datos.Bloque("Mercenarios_Por_Contrato", "MERCENARIOS_CONTRATO")?["valoracionHoras"] ?? 72; } catch (FaltaDato) { return 72; } }
        }

        /// <summary>Los contratos acabados que el jugador puede valorar aún (el Rey, o quien lo firmó).</summary>
        List<ContratosPorValorar.Terminado> PorValorar(Heroe h, Faccion f) =>
            ContratosPorValorar.De(c.ClaveDeJugador(h.Id)).Where(t => !t.Valorado && AhoraMs - t.Momento < HorasDeValoracion * 3_600_000
                && ((f.HasReyId && f.ReyId == h.Id) || string.IsNullOrEmpty(t.FirmadoPorId) || t.FirmadoPorId == h.Id)).ToList();

        /// <summary>La cifra de la pestaña: contratos a punto de vencer y valoraciones pendientes (`mercenariosQuePidenAtencion`).</summary>
        int MercenariosQuePidenAtencion(Heroe h, Faccion f) => f.Contratos.Count(VencePronto) + PorValorar(h, f).Count;

        string FirmaDeMercenarios(Heroe h, Faccion f) =>
            $"{string.Join(",", marcadosMerc)}|{tipoDeOrdenMerc}|{PorValorar(h, f).Count}|{h.Version}|{(long)(AhoraMs / 60_000)}"
            + string.Join(",", c.Sesion.Almacen.Todas(Entidad.CuerpoOneofCase.Ejercito).Select(e => e.Ejercito.Version));

        /// <summary>Dónde anda un mercenario: su columna o su residencia.</summary>
        string DondeAnda(string id, string residencia)
        {
            var col = c.Sesion.Almacen.Todas(Entidad.CuerpoOneofCase.Ejercito).Select(e => e.Ejercito).FirstOrDefault(x => x.LiderId == id || x.Participantes.Any(p => p.HeroeId == id));
            if (col != null) return Instante.Posicion(col.Plan, col.Posicion, AhoraMs).enMarcha ? "en marcha" : "en el mapa";
            if (residencia?.StartsWith("as-") == true) return "en " + NombreDePlaza(residencia);
            if (residencia?.StartsWith("camp-") == true) return "en su campamento";
            return "fuera de la vista";
        }

        void PestanaMercenarios(VisualElement v, Heroe h, Faccion f)
        {
            if (HojaDePlaza is StyleSheet hoja && !v.styleSheets.Contains(hoja)) v.styleSheets.Add(hoja);
            var a = c.Sesion.Almacen;
            ContratosPorValorar.Ver(f.Contratos);
            var yo = MisPuestos(h);
            string Residencia(string id) => f.Miembros.FirstOrDefault(m => m.HeroeId == id) is MiembroFaccion m && m.HasResidenciaId ? m.ResidenciaId : null;
            // Fuera de la selección quien ya no está o ya no recibe órdenes tuyas.
            foreach (var id in marcadosMerc.ToList())
                if (!(f.Contratos.FirstOrDefault(k => k.MercenarioId == id) is Contrato k0) || OrdenesPosibles(yo, k0, Residencia(id)).Count == 0) marcadosMerc.Remove(id);
            var error = Dialogo.Texto("", "error oculto");
            error.name = "merc-error";
            void Error(string t) => Poner(error, t);
            void Repintar() { firmaFaccion = null; PintarFaccion(); }

            v.Add(ConAyuda(Dialogo.Texto("Mercenarios", "pacto-plaza merc-titulo"),
                "Se contratan en persona, en el tablón de un campamento de mercenarios (su pieza Militar), o desde la Taberna de una plaza tuya. Sin orden un mercenario solo se defiende. Ordena quien le alcanza: el Rey y el Embajador a todos; el General, el Gobernador y el Maestro de Caravanas a los de su plaza. Una orden solo la cambia un cargo de igual o más rango que quien la dio."));
            var pendientes = PorValorar(h, f);
            if (pendientes.Count > 0)
            {
                var caja = Dialogo.Caja("merc-valorar");
                caja.name = "merc-valorar";
                caja.Add(Dialogo.Texto("Contratos acabados: valóralos", "merc-valorar-titulo"));
                foreach (var t in pendientes)
                {
                    var fila = Dialogo.Caja("merc-valorar-fila");
                    var texto = Dialogo.Caja("merc-nombre");
                    texto.Add(Dialogo.Texto(Textos.NombreDeHeroe(a, t.Id), "merc-nombre-texto"));
                    var tn = TrabajoMercenario.TryGetValue(t.Trabajo ?? "", out var tr) ? tr.nombre : "Contrato";
                    texto.Add(Dialogo.Texto($"{tn} · {(FinDeContrato.TryGetValue(t.Motivo ?? "", out var fin) ? fin : t.Motivo)} · quedan {EnTiempoReal(t.Momento + HorasDeValoracion * 3_600_000 - AhoraMs)}", "merc-sub"));
                    fila.Add(texto);
                    foreach (var (buena, nombre, ayuda) in new[] { (true, "Buena", "Suma la mitad del renombre que ganó con este contrato"), (false, "Mala", "Le quita el renombre que ganó con este contrato") })
                    {
                        var t0 = t;
                        Button b = null;
                        b = Dialogo.Boton("", "boton-secundario merc-mini", async () =>
                        {
                            var r = await OrdenDelPanel(new Orden { ValorarMercenario = new OrdenValorarMercenario { MercenarioId = t0.Id, Buena = buena } }, Error, Repintar, b);
                            if (r?.Resultado == Resultado.OrdenAceptada) { ContratosPorValorar.Valorado(c.ClaveDeJugador(h.Id), t0.Id); Repintar(); }
                        });
                        b.name = $"merc-valorar-{(buena ? "buena" : "mala")}-{t.Id}";
                        b.Add(IconosDelMundo.Icono("glifo", buena ? "buena" : "mala", 15));
                        b.Add(Dialogo.Texto(nombre, "merc-boton-texto"));
                        b.AddToClassList("merc-con-icono");
                        b.tooltip = ayuda;
                        fila.Add(b);
                    }
                    caja.Add(fila);
                }
                v.Add(caja);
            }
            var contratos = f.Contratos.ToList();
            if (contratos.Count == 0)
            {
                v.Add(Dialogo.Texto("Tu Facción no tiene mercenarios a sueldo. Se contratan en persona en el tablón de un campamento de mercenarios (su pieza Militar) o desde la Taberna de una plaza tuya.", "campo-ayuda"));
                v.Add(error);
                return;
            }
            var temporales = contratos.Count(k => k.Trabajo != "permanente");
            var resumen = Dialogo.Caja("merc-resumen");
            resumen.name = "merc-resumen";
            Fichas(resumen, ("A sueldo", contratos.Count.ToString()), ("Temporales", temporales.ToString()), ("Permanentes", (contratos.Count - temporales).ToString()),
                ("Sin orden", contratos.Count(k => k.Orden == null).ToString()));
            v.Add(resumen);
            v.Add(BarraDeOrdenes(h, contratos, yo, Residencia, Error, Repintar));
            v.Add(error);
            var rejilla = Dialogo.Caja("merc-ofertas");
            rejilla.name = "merc-contratos";
            foreach (var k in contratos.OrderBy(k => k.HasVenceEnMs ? k.VenceEnMs : long.MaxValue)) rejilla.Add(TarjetaDeContrato(h, f, k, yo, Residencia(k.MercenarioId), Error, Repintar));
            v.Add(rejilla);
        }

        /// <summary>La barra de órdenes (`htmlBarraOrdenes`): solo se encienden las que admiten todos los marcados, y el objetivo se elige entre los que valen.</summary>
        VisualElement BarraDeOrdenes(Heroe h, List<Contrato> contratos, PuestosMerc yo, Func<string, string> residencia, Action<string> error, Action repintar)
        {
            var elegidos = contratos.Where(k => marcadosMerc.Contains(k.MercenarioId)).ToList();
            var posibles = TiposDeOrden.Where(t => elegidos.Count > 0 && elegidos.All(k => OrdenesPosibles(yo, k, residencia(k.MercenarioId)).Contains(t))).ToList();
            if (!posibles.Contains(tipoDeOrdenMerc) && posibles.Count > 0) tipoDeOrdenMerc = posibles[0];
            var barra = Dialogo.Caja("merc-barra-ordenes" + (elegidos.Count > 0 ? "" : " vacia"));
            barra.name = "merc-barra";
            var cab = Dialogo.Caja("merc-barra-cab");
            cab.Add(Dialogo.Texto(elegidos.Count > 0 ? $"{elegidos.Count} marcado{(elegidos.Count == 1 ? "" : "s")} de 16" : "Marca mercenarios para darles una orden", "merc-barra-titulo"));
            var chips = Dialogo.Caja("merc-barra-chips");
            var todos = Dialogo.Boton("Marcar todos", "merc-chip", () =>
            {
                foreach (var k in contratos) if (marcadosMerc.Count < 16 && OrdenesPosibles(yo, k, residencia(k.MercenarioId)).Count > 0) marcadosMerc.Add(k.MercenarioId);
                repintar();
            });
            todos.name = "merc-todos";
            var ninguno = Dialogo.Boton("Ninguno", "merc-chip", () => { marcadosMerc.Clear(); repintar(); });
            ninguno.name = "merc-ninguno";
            ninguno.SetEnabled(elegidos.Count > 0);
            chips.Add(todos);
            chips.Add(ninguno);
            cab.Add(chips);
            barra.Add(cab);
            var ordenes = Dialogo.Caja("merc-ordenes");
            foreach (var t in TiposDeOrden)
            {
                var t0 = t;
                var vale = posibles.Contains(t);
                var (nombre, fam, ico, texto) = OrdenMercenarioDe[t];
                var b = new Button(() => { tipoDeOrdenMerc = t0; objetivoOrdenMerc = 0; repintar(); }) { name = "merc-orden-" + t };
                b.tooltip = texto + (vale || elegidos.Count == 0 ? "" : " (algún marcado no la admite, o no le alcanzan tus cargos)");
                b.AddToClassList("merc-trabajo");
                b.EnableInClassList("activo", t == tipoDeOrdenMerc && vale);
                b.SetEnabled(vale);
                b.Add(IconosDelMundo.Icono(fam, ico, 18));
                b.Add(Dialogo.Texto(nombre, "merc-trabajo-texto"));
                ordenes.Add(b);
            }
            barra.Add(ordenes);
            if (elegidos.Count == 0 || posibles.Count == 0) return barra;

            var propios = ObjetivosPropios(h);
            // `guardar` de un defensor o un escolta solo vale sobre el objetivo de su contrato: si todos los marcados comparten uno, es ese; si no, ninguno.
            var fijos = elegidos.Where(k => k.Trabajo == "defender" || k.Trabajo == "escoltar").Select(k => ClaveDeObjetivo(k.Objetivo)).Distinct().ToList();
            List<OpcionMerc> ObjetivosDe(string t)
            {
                if (t == "guardar")
                {
                    var todas = propios.plazas.Concat(propios.pueblos).Concat(propios.avanzadas).Concat(propios.caravanas).ToList();
                    return fijos.Count == 0 ? todas : fijos.Count == 1 ? todas.Where(x => x.Clave == fijos[0]).ToList() : new List<OpcionMerc>();
                }
                if (t == "atacar") return ObjetivosAtacables(h);
                if (t == "unirse") return propios.convocatorias.Concat(propios.ejercitos).ToList();
                return new List<OpcionMerc>();
            }
            var fila = Dialogo.Caja("merc-ordenar-fila");
            Func<ObjetivoMercenario> objetivo;
            string nota = null;
            if (tipoDeOrdenMerc == "ir_a")
            {
                var lugares = PorCercania(h, propios.plazas.Concat(propios.pueblos).Concat(propios.avanzadas).Where(x => x.X != null)).ToList();
                var yoPos = MiPosicionMerc(h);
                var x = new IntegerField("x") { value = xMerc ?? (int)Math.Round(yoPos?.x ?? 0), name = "merc-x", isDelayed = true };
                var y = new IntegerField("y") { value = yMerc ?? (int)Math.Round(yoPos?.y ?? 0), name = "merc-y", isDelayed = true };
                x.AddToClassList("merc-num");
                y.AddToClassList("merc-num");
                x.RegisterValueChangedCallback(e => xMerc = e.newValue);
                y.RegisterValueChangedCallback(e => yMerc = e.newValue);
                if (lugares.Count > 0)
                {
                    var lista = new DropdownField(new[] { "Un lugar tuyo…" }.Concat(lugares.Select(l => l.Etiqueta)).ToList(), Math.Min(lugarMerc, lugares.Count)) { name = "merc-lugar" };
                    lista.AddToClassList("merc-desplegable");
                    lista.RegisterValueChangedCallback(_ =>
                    {
                        lugarMerc = lista.index;
                        if (lista.index > 0) { xMerc = (int)Math.Round(lugares[lista.index - 1].X.Value); yMerc = (int)Math.Round(lugares[lista.index - 1].Y.Value); repintar(); }
                    });
                    fila.Add(lista);
                }
                fila.Add(x);
                fila.Add(y);
                objetivo = () => new ObjetivoMercenario { Punto = new Punto { X = x.value * 1000L, Y = y.value * 1000L } };
                nota = "Elige un lugar tuyo o escribe el punto (empieza en tu posición).";
            }
            else
            {
                var opciones = PorCercania(h, ObjetivosDe(tipoDeOrdenMerc));
                if (opciones.Count == 0)
                {
                    fila.Add(Dialogo.Texto(fijos.Count > 1 ? "Los marcados guardan cosas distintas" : "Ninguno a la vista", "campo-ayuda"));
                    barra.Add(fila);
                    return barra;
                }
                var lista = new DropdownField(opciones.Select(o => o.Etiqueta).ToList(), Math.Min(objetivoOrdenMerc, opciones.Count - 1)) { name = "merc-orden-objetivo" };
                lista.AddToClassList("merc-desplegable");
                lista.RegisterValueChangedCallback(_ => objetivoOrdenMerc = lista.index);
                fila.Add(lista);
                objetivo = () => opciones[lista.index].Objetivo;
                if (tipoDeOrdenMerc == "atacar" && elegidos.Any(k => k.Trabajo == "defender")) nota = "Un defensor solo ataca lo que esté cerca de lo que defiende.";
            }
            Button dar = null;
            dar = Dialogo.Boton("Dar la orden", "boton-primario merc-dar", async () =>
            {
                var o = new OrdenOrdenarMercenario { Tipo = tipoDeOrdenMerc, Objetivo = objetivo() };
                o.MercenarioIds.AddRange(marcadosMerc.Take(16));
                var r = await OrdenDelPanel(new Orden { OrdenarMercenario = o }, error, repintar, dar);
                if (r?.Resultado == Resultado.OrdenAceptada) { marcadosMerc.Clear(); repintar(); }
            });
            dar.name = "merc-dar";
            fila.Add(dar);
            barra.Add(fila);
            if (nota != null) barra.Add(Dialogo.Texto(nota, "merc-sub"));
            return barra;
        }

        /// <summary>Un contrato (`htmlContrato`): marcar, insignia y dónde anda; el trabajo y su objetivo; el plazo con su barra; la orden; quién firmó y,
        /// a quien podría firmarlo, «Prorrogar», «Mudar» (permanente; el Rey o el Embajador) y «Despedir».</summary>
        VisualElement TarjetaDeContrato(Heroe h, Faccion f, Contrato k, PuestosMerc yo, string residencia, Action<string> error, Action repintar)
        {
            var a = c.Sesion.Almacen;
            var id = k.MercenarioId;
            var publico = a.Buscar(TipoEntidad.Heroe, id)?.Heroe;
            var permanente = k.Trabajo == "permanente";
            var pronto = VencePronto(k);
            var gestiona = PuedeGestionar(yo, k) && !k.HasFinAplazado;
            var sinGestion = k.HasFinAplazado ? "Su contrato ya acabó" : "Solo quien podría firmar hoy este contrato";
            var marcable = OrdenesPosibles(yo, k, residencia).Count > 0;
            var carta = Dialogo.Caja("merc-oferta merc-contrato");
            carta.name = "merc-contrato-" + id;
            carta.EnableInClassList("marcado", marcadosMerc.Contains(id));
            carta.EnableInClassList("pronto", pronto);
            carta.EnableInClassList("acabado", k.HasFinAplazado);

            var cab = Dialogo.Caja("merc-oferta-cab");
            var marca = new Toggle { value = marcadosMerc.Contains(id), name = "merc-marcar-" + id, tooltip = marcable ? "Marcar para darle una orden" : "Ninguno de tus cargos le da órdenes" };
            marca.AddToClassList("merc-marcar");
            marca.SetEnabled(marcable);
            marca.RegisterValueChangedCallback(e =>
            {
                if (e.newValue && marcadosMerc.Count >= 16) { marca.SetValueWithoutNotify(false); error("Una orden va a 16 mercenarios como mucho."); return; }
                if (e.newValue) marcadosMerc.Add(id); else marcadosMerc.Remove(id);
                repintar();
            });
            cab.Add(marca);
            cab.Add(InsigniaDeRango(publico?.FichaMercenario?.Rango ?? 1, 30));
            var nombre = Dialogo.Caja("merc-nombre");
            nombre.Add(Dialogo.Texto(Textos.NombreDeHeroe(a, id), "merc-nombre-texto"));
            var sub = new List<string>();
            if (publico?.FichaMercenario != null && RangoMercenario.TryGetValue(publico.FichaMercenario.RangoId, out var rango)) sub.Add(rango);
            if (publico != null && publico.Nivel > 0) sub.Add($"nivel {publico.Nivel}");
            sub.Add(DondeAnda(id, residencia));
            nombre.Add(Dialogo.Texto(string.Join(" · ", sub), "merc-sub"));
            cab.Add(nombre);
            carta.Add(cab);

            var (tn, fam, ico, tt) = TrabajoMercenario.TryGetValue(k.Trabajo, out var tr) ? tr : (k.Trabajo, "glifo", "escudo", "");
            carta.Add(LineaDeMercenario(IconosDelMundo.Icono(fam, ico, 16), tn, permanente ? (residencia != null ? "reside en " + NombreDePlaza(residencia) : "") : TextoDeObjetivo(k.Objetivo), null, tt));
            if (!permanente && k.HasVenceEnMs)
            {
                var queda = Math.Max(0, k.VenceEnMs - AhoraMs);
                var tiempo = Dialogo.Caja("merc-tiempo");
                tiempo.Add(IconosDelMundo.ConIcono("glifo", "espera", Dialogo.Texto($"Quedan {EnTiempoReal(queda)}{(pronto ? " · prorrógalo a tiempo" : "")}", "merc-sub" + (pronto ? " merc-aviso" : "")), 13));
                var barra = Dialogo.Caja("merc-barra" + (pronto ? " pronto" : ""));
                var total = k.PlazoDias * 86_400_000.0;
                var i = Dialogo.Caja("merc-barra-i");
                i.style.width = Length.Percent((float)Math.Min(100, total > 0 ? 100 * queda / total : 0));
                barra.Add(i);
                tiempo.Add(barra);
                carta.Add(tiempo);
            }
            var o = k.Orden;
            VisualElement lineaOrden;
            if (o != null)
            {
                var (on, ofam, oico, _) = OrdenMercenarioDe.TryGetValue(o.Tipo, out var od) ? od : (o.Tipo, "glifo", "escudo", "");
                var mas = (o.HasPuesto ? "de " + TextoDePuesto(o.Puesto) : "orden inicial") + (o.HasVenceEnMs ? $" · caduca en {EnTiempoReal(o.VenceEnMs - AhoraMs)}" : "") + (o.Tipo == "unirse" && o.Unido ? " · ya dentro" : "");
                lineaOrden = LineaDeMercenario(IconosDelMundo.Icono(ofam, oico, 16), on, TextoDeObjetivo(o.Objetivo), mas, null);
            }
            else lineaOrden = LineaDeMercenario(Dialogo.Texto("…", "merc-sub"), null, "Sin orden: solo se defiende", null, null);
            lineaOrden.AddToClassList("merc-orden-actual");
            lineaOrden.name = "merc-orden-actual-" + id;
            carta.Add(lineaOrden);
            if (k.HasFinAplazado) carta.Add(Dialogo.Texto($"Contrato acabado ({(FinDeContrato.TryGetValue(k.FinAplazado, out var fin) ? fin : k.FinAplazado)}): sale al terminar su batalla.", "merc-motivo"));

            var acciones = Dialogo.Caja("merc-acciones");
            acciones.Add(IconosDelMundo.ConIcono("glifo", "firma", Dialogo.Texto($"{Textos.NombreDeHeroe(a, k.FirmadoPorId)}, {TextoDePuesto(k.PuestoFirma)}{(k.Renombre > 0 ? $" · +{k.Renombre} de renombre al acabar" : "")}", "merc-firma"), 13));
            var botones = Dialogo.Caja("merc-acciones-botones");
            if (!permanente && gestiona)
            {
                var plazo = new DropdownField(PlazosDeContrato.Select(d => "+" + Dias(d)).ToList(), 0) { name = "merc-prorroga-" + id };
                plazo.AddToClassList("merc-desplegable");
                plazo.AddToClassList("merc-mini");
                botones.Add(plazo);
                DropdownField pago = null;
                if (h.Monedas > 0)
                {
                    pago = new DropdownField(new List<string> { "Con oro", "Con monedas" }, 0) { name = "merc-prorroga-pago-" + id, tooltip = $"Con qué pagas la prórroga: oro, o tus monedas (tienes {h.Monedas.ToString("N0", Es)}) a la tarifa en monedas" };
                    pago.AddToClassList("merc-desplegable");
                    pago.AddToClassList("merc-mini");
                    botones.Add(pago);
                }
                Button prorrogar = null;
                prorrogar = Dialogo.Boton("Prorrogar", "boton-secundario merc-mini", async () =>
                {
                    var p = new OrdenProrrogarContrato { MercenarioId = id, PlazoDias = PlazosDeContrato[Math.Max(0, plazo.index)] };
                    if (pago?.index == 1) p.PagarCon = "monedas";
                    await OrdenDelPanel(new Orden { ProrrogarContrato = p }, error, repintar, prorrogar);
                });
                prorrogar.name = "merc-prorrogar-" + id;
                prorrogar.tooltip = "En persona: en su campamento base, con el carro de tu columna o, si resides allí y estás dentro, con tu almacén personal; o con tu columna junto a la suya, con el carro de la columna. Con monedas, en cualquiera de esos sitios. Pagas la tarifa de hoy.";
                botones.Add(prorrogar);
            }
            if (permanente && (yo.Rey || yo.Embajador) && !k.HasFinAplazado)
            {
                var otras = ObjetivosPropios(h).plazas.Where(x => x.Plaza != residencia).ToList();
                if (otras.Count > 0)
                {
                    var donde = new DropdownField(otras.Select(x => x.Etiqueta).ToList(), 0) { name = "merc-mudar-a-" + id };
                    donde.AddToClassList("merc-desplegable");
                    donde.AddToClassList("merc-mini");
                    botones.Add(donde);
                    Button mudar = null;
                    mudar = Dialogo.Boton("Mudar", "boton-secundario merc-mini", async () =>
                        await OrdenDelPanel(new Orden { MudarMercenario = new OrdenMudarMercenario { MercenarioId = id, AsentamientoId = otras[Math.Max(0, donde.index)].Plaza } }, error, repintar, mudar));
                    mudar.name = "merc-mudar-" + id;
                    mudar.tooltip = "A distancia y sin espera";
                    botones.Add(mudar);
                }
            }
            if (gestiona)
            {
                Button despedir = null;
                despedir = Dialogo.Boton("Despedir", "boton-secundario merc-mini merc-peligro", async () =>
                {
                    if (!await dialogo.Abrir("El oro pagado no se devuelve, pierde el renombre de este contrato y tu Facción tardará en poder recontratarlo.", "¿Despedirlo?", "Despedir", "Volver", true)) return;
                    await OrdenDelPanel(new Orden { DespedirMercenario = new OrdenDespedirMercenario { MercenarioId = id } }, error, repintar, despedir);
                });
                despedir.name = "merc-despedir-" + id;
                despedir.tooltip = "A distancia. No se devuelve nada.";
                botones.Add(despedir);
            }
            else if (!k.HasFinAplazado) acciones.tooltip = sinGestion;
            if (botones.childCount > 0) acciones.Add(botones);
            carta.Add(acciones);
            return carta;
        }

        static VisualElement LineaDeMercenario(VisualElement icono, string fuerte, string texto, string pequeno, string ayuda)
        {
            var fila = Dialogo.Caja("merc-linea");
            if (ayuda != null) fila.tooltip = ayuda;
            var circulo = Dialogo.Caja("merc-linea-icono");
            circulo.Add(icono);
            fila.Add(circulo);
            var cuerpo = Dialogo.Caja("merc-linea-cuerpo");
            var arriba = Dialogo.Caja("merc-linea-arriba");
            if (fuerte != null) arriba.Add(Dialogo.Texto(fuerte, "merc-linea-fuerte"));
            if (!string.IsNullOrEmpty(texto)) arriba.Add(Dialogo.Texto(texto, "merc-linea-texto"));
            cuerpo.Add(arriba);
            if (pequeno != null) cuerpo.Add(Dialogo.Texto(pequeno, "merc-sub"));
            fila.Add(cuerpo);
            return fila;
        }
    }

    /// <summary>Los contratos acabados por valorar (Víctor, 5b): el motor no manda la lista, así que se sacan del hecho `mercenarios.terminado` y se
    /// guardan en el equipo (PlayerPrefs, por héroe) hasta que caduca el plazo de valoración. De cada contrato vigente se recuerda quién lo firmó y su
    /// trabajo, que el hecho no trae.</summary>
    public static class ContratosPorValorar
    {
        public sealed class Terminado
        {
            public string Id, Motivo, FirmadoPorId, Trabajo;
            public double Momento;
            public bool Valorado;
        }

        const string Prefijo = "mundo.mercenarios.porvalorar.";
        /// <summary>Más que el plazo de valoración (72 h de mundo): lo que pasa de aquí se poda al guardar.</summary>
        const double Caduca = 14 * 86_400_000.0;
        static readonly Dictionary<string, Contrato> vistos = new Dictionary<string, Contrato>();
        static readonly string[] TrabajoPorNumero = { "defender", "escoltar", "atacar", "acompanar", "permanente" };

        /// <summary>Los de un jugador: `clave` es `ConexionDeMundo.ClaveDeJugador` (servidor, cuenta y héroe).</summary>
        public static List<Terminado> De(string clave)
        {
            try { return JsonConvert.DeserializeObject<List<Terminado>>(PlayerPrefs.GetString(Prefijo + clave, "")) ?? new List<Terminado>(); }
            catch (JsonException) { return new List<Terminado>(); }
        }

        static void Guardar(string clave, List<Terminado> l, double ahora)
        {
            l.RemoveAll(t => ahora - t.Momento > Caduca);
            PlayerPrefs.SetString(Prefijo + clave, JsonConvert.SerializeObject(l));
            PlayerPrefs.Save();
        }

        public static void Ver(IEnumerable<Contrato> contratos)
        {
            foreach (var k in contratos) vistos[k.MercenarioId] = k;
        }

        public static void Valorado(string clave, string mercenarioId)
        {
            var l = De(clave);
            foreach (var t in l.Where(x => x.Id == mercenarioId)) t.Valorado = true;
            Guardar(clave, l, l.Count > 0 ? l.Max(x => x.Momento) : 0);
        }

        /// <summary>Lo llama la conexión con cada hecho: apunta los `mercenarios.terminado`, marca los `valorado` y olvida al recontratado.</summary>
        public static void Anotar(EventoDominio e, string heroeId, string clave, Almacen almacen)
        {
            if (e.Codigo == null || !e.Codigo.StartsWith("mercenarios.")) return;
            var h = almacen.Buscar(TipoEntidad.Heroe, heroeId)?.Heroe;
            if (h != null && h.HasFaccionId && almacen.Buscar(TipoEntidad.Faccion, h.FaccionId)?.Faccion is Faccion f) Ver(f.Contratos);
            if (e.Payload == null || !e.Payload.Fields.TryGetValue("mercenario", out var m)) return;
            var id = "heroe-" + (m.HasNumberValue ? ((long)m.NumberValue).ToString() : m.StringValue);
            var l = De(clave);
            switch (e.Codigo)
            {
                case "mercenarios.terminado":
                    vistos.TryGetValue(id, out var k);
                    var trabajo = k?.Trabajo;
                    if (trabajo == null && e.Payload.Fields.TryGetValue("trabajo", out var t) && t.HasNumberValue && t.NumberValue >= 0 && t.NumberValue < TrabajoPorNumero.Length) trabajo = TrabajoPorNumero[(int)t.NumberValue];
                    l.RemoveAll(x => x.Id == id);
                    l.Add(new Terminado
                    {
                        Id = id, Momento = e.EnMs, FirmadoPorId = k?.FirmadoPorId, Trabajo = trabajo,
                        Motivo = e.Payload.Fields.TryGetValue("motivo", out var mo) ? mo.StringValue : "",
                    });
                    break;
                case "mercenarios.valorado": foreach (var x in l.Where(x => x.Id == id)) x.Valorado = true; break;
                case "mercenarios.contratado": l.RemoveAll(x => x.Id == id); break;
                default: return;
            }
            Guardar(clave, l, e.EnMs);
        }
    }
}
