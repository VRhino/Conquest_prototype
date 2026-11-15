// La Diplomacia de tu Facción (paso 8 del bloque 4, tanda 4), calcada de BronzeAgeClient (`ui/pestanaFaccion.ts`: `htmlListaDiplomacia`,
// `htmlFichaFaccion`; `ui/panelDiplomacia.ts`; `ui/panelAnexion.ts`; `ui/panelFusion.ts`; la Liga de `sigilo/imperio.ts`):
//   - A la derecha, la lista: la tuya, «Conocidas» y «Sin contacto» (plegables), con «Ocultar las de bots».
//   - En el centro, la elegida. La tuya: sus relaciones y propuestas, la Anexión, la Fusión y la Liga. Otra: su cabecera y sus datos, lo vuestro
//     (relaciones y propuestas activas entre las dos), «Nueva propuesta» (una cláusula: alianza, vasallaje con tributo, anexión, fusión o la
//     guerra) y las plazas suyas que conoces.
//   - Las órdenes: proponer, responder y retirar relación, anexión y fusión; romper, rebelarse, declarar la guerra y ofrecer la paz. Proponer y
//     retirar, el Rey o el Embajador; responder, el Rey; la fusión, solo el Rey. Lo que no se puede no sale.
using System;
using System.Collections.Generic;
using System.Linq;
using BronzeAge.Contract.V1;
using ConquestTactics.Mundo.Mapa;
using UnityEngine;
using UnityEngine.UIElements;

namespace ConquestTactics.Mundo.UI
{
    public sealed partial class PantallasDeMundo
    {
        /// <summary>La Facción elegida en la lista (null, la tuya), «Ocultar las de bots» y las secciones plegadas.</summary>
        string faccionElegida;
        bool diplomaciaSinBots;
        readonly HashSet<string> seccionesPlegadas = new HashSet<string>();

        /// <summary>El borrador de «Nueva propuesta» con cada Facción (`borradores` del prototipo): una cláusula y sus términos.</summary>
        sealed class Borrador { public string Tipo; public string Recurso = "trigo"; public int Cantidad = 10; public string Nombre = ""; public bool ReyTu = true; }
        readonly Dictionary<string, Borrador> borradores = new Dictionary<string, Borrador>();

        static readonly string[] RecursosDeTributo = { "trigo", "madera", "piedra", "cobre", "estano", "oro" };
        const string AyudaDiplomacia = "Tributo por minuto, solo en vasallaje. Alianza y vasallaje son propuestas: solo nacen si el Rey de la otra Facción las acepta antes de que caduquen (3 días de mundo); mientras tanto las puedes retirar. Una Facción con reputación por debajo de −40 no puede proponer alianzas, y cada una tiene las alianzas a la vez que le da su prestigio (una más cada 100). La guerra es libre y arrastra a señor y vasallos del rival; solo se acaba con la paz de las dos. Romper una alianza sin más cuesta 12 puntos de reputación; liberar a un vasallo da 6.";

        // ─────────────── los datos ───────────────

        List<RelacionPolitica> Relaciones() => c.Sesion.Almacen.Todas(Entidad.CuerpoOneofCase.Relacion).Select(e => e.Relacion)
            .GroupBy(r => r.Id).Select(g => g.First()).ToList();
        List<Propuesta> Propuestas() => c.Sesion.Almacen.Todas(Entidad.CuerpoOneofCase.Propuesta).Select(e => e.Propuesta)
            .GroupBy(p => p.Id).Select(g => g.First()).ToList();
        static bool Entre(string a, string b, string x, string y) => a == x && b == y || a == y && b == x;

        /// <summary>La relación activa entre dos Facciones (`relacionCon`), o null.</summary>
        static RelacionPolitica RelacionActiva(List<RelacionPolitica> rs, string yo, string otra) =>
            rs.FirstOrDefault(r => r.Estado == EstadoRelacion.RelacionActiva && Entre(r.FaccionAId, r.FaccionBId, yo, otra));

        static string TextoDeRelacion(RelacionPolitica r, string yo) => r == null ? null : r.Tipo switch
        {
            TipoRelacion.RelacionVasallaje => r.FaccionAId == yo ? "Tu vasalla" : "Tu señora",
            TipoRelacion.RelacionAlianza => "Aliada",
            _ => "En guerra",
        };

        /// <summary>Las plazas de una Facción que conoces (las del almacén: las tuyas, las vistas y las avistadas).</summary>
        List<Asentamiento> PlazasConocidasDe(string faccionId) => c.Sesion.Almacen.Todas(Entidad.CuerpoOneofCase.Asentamiento).Select(e => e.Asentamiento)
            .Where(x => x.FaccionId == faccionId).ToList();

        /// <summary>Con contacto (`hayContacto`): la tuya, las de tu Liga, con las que tienes relación y de las que conoces alguna plaza.</summary>
        bool HayContacto(Faccion propia, List<RelacionPolitica> rs, string id) =>
            id == propia.Id || propia.Liga != null && propia.Liga.MiembrosIds.Contains(id) || RelacionActiva(rs, propia.Id, id) != null || PlazasConocidasDe(id).Count > 0;

        string NombreDeFaccionCorto(string id) => id == null ? "" : NombreDeFaccionConocida(id);

        string Tributo(Tributo t) => t == null || t.CantidadPorMinuto == 0 ? "sin tributo" : $"{t.CantidadPorMinuto / 1000} {CosasDelMapa.NombreDeRecurso(t.Recurso)}/min";

        /// <summary>«caduca en N h» o «caduca en N días» (`caduca`), con el tiempo de mundo que queda.</summary>
        string Plazo(long expiraMs)
        {
            var h = (expiraMs - AhoraMs) / 3_600_000.0;
            if (h < 1) return $"caduca en {Math.Max(1, Math.Ceiling(h * 60))} min";
            return h >= 48 ? $"caduca en {Math.Floor(h / 24)} días" : $"caduca en {Math.Ceiling(h)} h";
        }

        string FirmaDeDiplomacia() =>
            $"{string.Join(",", Relaciones().Select(r => $"{r.Id}:{r.Version}"))}|{string.Join(",", Propuestas().Select(p => $"{p.Id}:{p.Version}"))}|{faccionElegida}|{diplomaciaSinBots}|{string.Join(",", seccionesPlegadas)}";

        // ─────────────── la pestaña ───────────────

        void PestanaDiplomacia(VisualElement v, Heroe h, Faccion f)
        {
            var rs = Relaciones();
            var conocidas = FaccionesConocidas();
            if (faccionElegida != null && conocidas.All(x => x.id != faccionElegida)) faccionElegida = null;
            var marco = Dialogo.Caja("diplo-marco");
            marco.name = "diplo-marco";
            var centro = new ScrollView(ScrollViewMode.Vertical) { name = "diplo-centro" };
            centro.AddToClassList("diplo-centro");
            var lista = new ScrollView(ScrollViewMode.Vertical) { name = "diplo-lista" };
            lista.AddToClassList("diplo-lista");
            marco.Add(centro);
            marco.Add(lista);
            v.Add(marco);
            ListaDeDiplomacia(lista, f, rs, conocidas);
            if (faccionElegida == null || faccionElegida == f.Id) CentroPropio(centro, h, f, rs);
            else FichaDeOtraFaccion(centro, h, f, rs, conocidas.First(x => x.id == faccionElegida));
        }

        void ElegirFaccion(string id)
        {
            faccionElegida = id;
            firmaFaccion = null;
            PintarFaccion();
        }

        // ─────────────── la lista (`htmlListaDiplomacia`) ───────────────

        void ListaDeDiplomacia(VisualElement v, Faccion f, List<RelacionPolitica> rs, List<(string id, string nombre, Sigilo sigilo, Faccion completa, FaccionDelDirectorio ligera)> todas)
        {
            bool EsBot((string id, string nombre, Sigilo sigilo, Faccion completa, FaccionDelDirectorio ligera) x) => x.completa?.EsBot ?? x.ligera?.EsBot ?? false;
            bool Derrotada((string id, string nombre, Sigilo sigilo, Faccion completa, FaccionDelDirectorio ligera) x) => x.ligera?.Derrotada ?? x.completa?.HasDerrotadaPor ?? false;
            int Miembros((string id, string nombre, Sigilo sigilo, Faccion completa, FaccionDelDirectorio ligera) x) => x.completa?.CiudadanosIds.Count ?? (int)(x.ligera?.Ciudadanos ?? 0);
            var otras = todas.Where(x => x.id != f.Id).OrderBy(x => Derrotada(x)).ThenByDescending(Miembros).ThenBy(x => x.nombre, StringComparer.Create(Es, false)).ToList();
            var totalBots = otras.Count(EsBot);
            bool Visible((string id, string nombre, Sigilo sigilo, Faccion completa, FaccionDelDirectorio ligera) x) =>
                !diplomaciaSinBots || !EsBot(x) || x.id == faccionElegida || RelacionActiva(rs, f.Id, x.id) != null;
            if (totalBots > 0)
            {
                var bots = IconosDelMundo.BotonConGlifo(diplomaciaSinBots ? $"Mostrar las de bots ({totalBots})" : "Ocultar las de bots", ("glifo", "bots"), "boton-secundario diplo-bots", () =>
                {
                    diplomaciaSinBots = !diplomaciaSinBots;
                    firmaFaccion = null;
                    PintarFaccion();
                });
                bots.name = "diplo-bots";
                bots.tooltip = "Las Facciones que lleva un Rey bot";
                bots.EnableInClassList("activo", diplomaciaSinBots);
                v.Add(bots);
            }
            VisualElement Fila(string id, string nombre, Sigilo sigilo, string sub, bool guerra)
            {
                var b = new Button(() => ElegirFaccion(id == f.Id ? null : id)) { name = "diplo-faccion-" + id };
                b.AddToClassList("diplo-fila");
                b.EnableInClassList("activo", (faccionElegida ?? f.Id) == id);
                b.EnableInClassList("guerra", guerra);
                b.Add(SigiloUI(sigilo, 26));
                var texto = Dialogo.Caja("diplo-fila-texto");
                texto.Add(Dialogo.Texto(nombre, "diplo-fila-nombre"));
                texto.Add(Dialogo.Texto(sub, "diplo-fila-sub"));
                b.Add(texto);
                return b;
            }
            string Sub((string id, string nombre, Sigilo sigilo, Faccion completa, FaccionDelDirectorio ligera) x)
            {
                var rel = TextoDeRelacion(RelacionActiva(rs, f.Id, x.id), f.Id);
                var nivel = x.completa?.Nivel ?? x.ligera?.Nivel ?? 1;
                var n = Miembros(x);
                return (rel ?? $"Nivel {nivel} · {n} miembro{(n == 1 ? "" : "s")}") + (EsBot(x) ? " · bots" : "");
            }
            v.Add(Fila(f.Id, f.Nombre, f.Sigilo, "La tuya", false));
            void Seccion(string id, string titulo, List<(string id, string nombre, Sigilo sigilo, Faccion completa, FaccionDelDirectorio ligera)> xs, string vacia, string ayuda = null)
            {
                var visibles = xs.Where(Visible).ToList();
                var ocultas = xs.Count - visibles.Count;
                var plegada = seccionesPlegadas.Contains(id);
                var cab = Dialogo.Caja("diplo-seccion-fila");
                var b = new Button(() => { if (!seccionesPlegadas.Remove(id)) seccionesPlegadas.Add(id); firmaFaccion = null; PintarFaccion(); }) { name = "diplo-seccion-" + id };
                b.AddToClassList("diplo-seccion");
                b.tooltip = plegada ? "Desplegar" : "Plegar";
                b.Add(Dialogo.Texto(plegada ? "▸" : "▾", "diplo-seccion-flecha"));
                b.Add(Kicker($"{titulo} ({xs.Count})"));
                if (ocultas > 0) b.Add(Dialogo.Texto($"+{ocultas} bots", "diplo-seccion-ocultas"));
                cab.Add(ayuda != null ? ConAyuda(b, ayuda) : b);
                v.Add(cab);
                if (plegada) return;
                if (visibles.Count == 0) v.Add(Dialogo.Texto(xs.Count > 0 ? "Solo de bots." : vacia, "faccion-nota"));
                foreach (var x in visibles)
                    v.Add(Fila(x.id, x.nombre, x.sigilo, Sub(x), RelacionActiva(rs, f.Id, x.id)?.Tipo == TipoRelacion.RelacionGuerra));
            }
            var conContacto = otras.Where(x => HayContacto(f, rs, x.id)).ToList();
            var sinContacto = otras.Where(x => !conContacto.Contains(x) && !Derrotada(x)).ToList();
            Seccion("conocidas", "Conocidas", conContacto, "Aún no has tenido contacto con ninguna.",
                "Con contacto: las de tu Liga, con las que tienes relación y aquellas de las que conoces alguna plaza en el mapa.");
            if (sinContacto.Count > 0) Seccion("sin-contacto", "Sin contacto", sinContacto, "Solo de bots.");
        }

        // ─────────────── el centro: la tuya ───────────────

        void CentroPropio(VisualElement v, Heroe h, Faccion f, List<RelacionPolitica> rs)
        {
            var cab = Dialogo.Caja("diplo-ficha-cab");
            cab.Add(SigiloUI(f.Sigilo, 56, f.Liga != null && f.Liga.HasGranReyId && f.Liga.GranReyId == f.Id));
            var textos = Dialogo.Caja("diplo-ficha-textos");
            textos.Add(Kicker("Tu facción"));
            textos.Add(Dialogo.Texto(f.Nombre, "diplo-ficha-nombre"));
            cab.Add(textos);
            v.Add(cab);
            DiplomaciaPropia(v, h, f, rs);
            Anexion(v, h, f);
            Fusion(v, h, f);
            v.Add(Kicker("Liga"));
            if (f.Liga == null) v.Add(Dialogo.Texto("No perteneces a ninguna Liga.", "faccion-nota"));
            else
            {
                var liga = LineaDeLiga(f);
                v.Add(liga);
                if (f.Liga.HasGranReyId)
                {
                    var gran = c.Sesion.Almacen.Buscar(TipoEntidad.Faccion, f.Liga.GranReyId)?.Faccion;
                    var rey = gran != null && gran.HasReyId ? Textos.NombreDeHeroe(c.Sesion.Almacen, gran.ReyId) : null;
                    v.Add(Dialogo.Texto($"Gran Rey: {(rey != null ? $"{rey} ({NombreDeFaccionCorto(f.Liga.GranReyId)})" : NombreDeFaccionCorto(f.Liga.GranReyId))}", "faccion-nota"));
                }
            }
        }

        (bool rey, bool autoridad) Autoridad(Heroe h, Faccion f)
        {
            var rey = f.HasReyId && f.ReyId == h.Id;
            return (rey, rey || f.HasEmbajadorId && f.EmbajadorId == h.Id);
        }

        /// <summary>Un botón que manda una orden, con su confirmación si la lleva (`crearEnvio`).</summary>
        Button BotonDeOrden(string texto, string nombre, Func<Orden> orden, Label error, string clases = "boton-secundario",
            (string titulo, string texto, string aceptar, bool peligro)? confirmar = null, string titulo = null)
        {
            Button b = null;
            b = Dialogo.Boton(texto, clases, async () =>
            {
                if (confirmar is var (t, x, ac, pel) && !await dialogo.Abrir(x, t, ac, "Cancelar", pel)) return;
                await OrdenDelPanel(orden(), m => Poner(error, m), () => { firmaFaccion = null; }, b);
            });
            b.name = nombre;
            if (titulo != null) b.tooltip = titulo;
            return b;
        }

        VisualElement FilaDeDiplomacia(string titulo, string sub, params VisualElement[] botones)
        {
            var fila = Dialogo.Caja("faccion-fila diplo-relacion");
            var textos = Dialogo.Caja("diplo-relacion-textos");
            textos.Add(Dialogo.Texto(titulo, "diplo-relacion-titulo"));
            if (!string.IsNullOrEmpty(sub)) textos.Add(Dialogo.Texto(sub, "diplo-relacion-sub"));
            fila.Add(textos);
            foreach (var b in botones) if (b != null) fila.Add(b);
            return fila;
        }

        /// <summary>Una relación activa con sus botones (`fila`).</summary>
        VisualElement FilaDeRelacion(RelacionPolitica r, Faccion f, bool autoridad, Label error)
        {
            var yoSoyA = r.FaccionAId == f.Id;
            var otra = NombreDeFaccionCorto(yoSoyA ? r.FaccionBId : r.FaccionAId);
            var terminar = ("¿Terminar esta relación?", "Es inmediato.", "Terminarla", true);
            Orden Romper() => new Orden { RomperRelacion = new OrdenRomperRelacion { RelacionId = r.Id, IniciadorFaccionId = f.Id } };
            VisualElement fila;
            if (r.Tipo == TipoRelacion.RelacionAlianza)
                fila = FilaDeDiplomacia($"Alianza con {otra}", null, autoridad ? BotonDeOrden("Romper alianza", "romper-" + r.Id, Romper, error, confirmar: terminar, titulo: "Romperla sin más cuesta 12 puntos de reputación") : null);
            else if (r.Tipo == TipoRelacion.RelacionVasallaje)
                fila = yoSoyA
                    ? FilaDeDiplomacia($"{otra} es tu vasalla", $"Tributo: {Tributo(r.Tributo)}", autoridad ? BotonDeOrden("Liberar vasallo", "romper-" + r.Id, Romper, error, confirmar: terminar, titulo: "Liberarla da 6 puntos de reputación") : null)
                    : FilaDeDiplomacia($"Eres vasalla de {otra}", $"Tributo: {Tributo(r.Tributo)}", autoridad ? BotonDeOrden("Rebelarse", "rebelarse-" + r.Id,
                        () => new Orden { RebelionVasallo = new OrdenRebelionVasallo { RelacionId = r.Id } }, error,
                        confirmar: ("¿Rebelarse?", "Rompes el vasallaje, declaras la guerra a tu señor y al resto de sus vasallos, y se cancelan al instante los acuerdos de trueque entre vuestras plazas. Tu señor pierde 10 puntos de reputación. No hay vuelta atrás.", "Rebelarse", true)) : null);
            else
            {
                var mia = r.HasPazPropuestaPor && r.PazPropuestaPor == f.Id;
                var suya = r.HasPazPropuestaPor && !mia;
                var estado = mia ? "Has ofrecido la paz: falta que la ofrezca la otra" : suya ? $"{otra} ofrece la paz" : "La paz exige que la ofrezcan las dos";
                fila = FilaDeDiplomacia($"En guerra con {otra}", estado, autoridad && !mia ? BotonDeOrden(suya ? "Aceptar la paz" : "Ofrecer la paz", "paz-" + r.Id,
                    () => new Orden { ProponerPaz = new OrdenProponerPaz { RelacionId = r.Id, FaccionId = f.Id } }, error) : null);
                fila.AddToClassList("guerra");
            }
            fila.name = "relacion-" + r.Id;
            return fila;
        }

        /// <summary>¿Le cabe otra alianza a esta Facción? (`cabeAlianza`: las activas contra su tope por prestigio). Sin saberlo, sí.</summary>
        bool CabeAlianza(List<RelacionPolitica> rs, string id)
        {
            var x = c.Sesion.Almacen.Buscar(TipoEntidad.Faccion, id)?.Faccion;
            if (x == null) return true;
            return rs.Count(r => r.Estado == EstadoRelacion.RelacionActiva && r.Tipo == TipoRelacion.RelacionAlianza && (r.FaccionAId == id || r.FaccionBId == id)) < x.TopeAlianzas;
        }

        /// <summary>Una propuesta de relación (`filaRecibida` / `filaEnviada`).</summary>
        VisualElement FilaDePropuestaDeRelacion(Propuesta p, Faccion f, bool rey, bool autoridad, List<RelacionPolitica> rs, Label error)
        {
            var pr = p.Relacion;
            var vasallaje = pr.Tipo == TipoRelacion.RelacionVasallaje;
            if (pr.FaccionBId == f.Id)
            {
                var a = NombreDeFaccionCorto(pr.FaccionAId);
                var porQue = vasallaje ? null : !CabeAlianza(rs, f.Id) ? "Tu Facción ya tiene todas las alianzas que le da su prestigio"
                    : !CabeAlianza(rs, pr.FaccionAId) ? $"{a} ya tiene todas las alianzas que le da su prestigio" : null;
                var sub = Plazo(p.ExpiraEnMs) + (rey ? "" : " · solo tu Rey responde") + (porQue != null ? " · " + porQue : "");
                Orden Responder(bool si) => new Orden { ResponderRelacion = new OrdenResponderRelacion { PropuestaId = p.Id, Aceptar = si } };
                return FilaDeDiplomacia(vasallaje ? $"{a} te propone un vasallaje: serías su vasalla y pagarías {Tributo(pr.Tributo)}" : $"{a} te propone una alianza", sub,
                    rey && porQue == null ? BotonDeOrden("Aceptar", "aceptar-" + p.Id, () => Responder(true), error,
                        confirmar: vasallaje ? ("¿Aceptar el vasallaje?", "Pasas a ser su vasalla: les pagarás el tributo cada minuto y no podrás dejarlo salvo rebelándote (guerra contra tu señora y sus vasallos). Una guerra contra ti será también contra ella.", "Aceptar el vasallaje", true) : ((string, string, string, bool)?)null) : null,
                    rey ? BotonDeOrden("Rechazar", "rechazar-" + p.Id, () => Responder(false), error) : null);
            }
            var b = NombreDeFaccionCorto(pr.FaccionBId);
            return FilaDeDiplomacia(vasallaje ? $"Propones un vasallaje ({Tributo(pr.Tributo)}) a {b}" : $"Propones una alianza a {b}", Plazo(p.ExpiraEnMs),
                autoridad ? BotonDeOrden("Retirar", "retirar-" + p.Id, () => new Orden { RetirarRelacion = new OrdenRetirarRelacion { PropuestaId = p.Id } }, error) : null);
        }

        /// <summary>Lo tuyo con todas (`htmlDiplomacia` sin `con`).</summary>
        void DiplomaciaPropia(VisualElement v, Heroe h, Faccion f, List<RelacionPolitica> rs)
        {
            var (rey, autoridad) = Autoridad(h, f);
            var activas = rs.Where(r => r.Estado == EstadoRelacion.RelacionActiva && !string.IsNullOrEmpty(r.Id) && (r.FaccionAId == f.Id || r.FaccionBId == f.Id)).ToList();
            var propuestas = Propuestas().Where(p => p.ClaseCase == Propuesta.ClaseOneofCase.Relacion && (p.Relacion.FaccionAId == f.Id || p.Relacion.FaccionBId == f.Id)).ToList();
            if (activas.Count == 0 && propuestas.Count == 0 && !autoridad) return;
            var error = Dialogo.Texto("", "error oculto");
            error.name = "error-diplomacia";
            var seccion = Dialogo.Caja("diplo-seccion-caja");
            seccion.name = "diplomacia-propia";
            seccion.Add(ConAyuda(Kicker("Diplomacia"), AyudaDiplomacia));
            foreach (var r in activas) seccion.Add(FilaDeRelacion(r, f, autoridad, error));
            var recibidas = propuestas.Where(p => p.Relacion.FaccionBId == f.Id).ToList();
            var enviadas = propuestas.Where(p => p.Relacion.FaccionAId == f.Id).ToList();
            if (recibidas.Count > 0) { seccion.Add(Dialogo.Texto("Propuestas recibidas", "diplo-subtitulo")); foreach (var p in recibidas) seccion.Add(FilaDePropuestaDeRelacion(p, f, rey, autoridad, rs, error)); }
            if (enviadas.Count > 0) { seccion.Add(Dialogo.Texto("Propuestas enviadas", "diplo-subtitulo")); foreach (var p in enviadas) seccion.Add(FilaDePropuestaDeRelacion(p, f, rey, autoridad, rs, error)); }
            if (autoridad) seccion.Add(Dialogo.Texto("Para proponer algo a otra Facción, elígela en la lista de la derecha.", "faccion-nota"));
            seccion.Add(error);
            v.Add(seccion);
        }

        // ─────────────── la Anexión y la Fusión (`panelAnexion.ts`, `panelFusion.ts`) ───────────────

        void Anexion(VisualElement v, Heroe h, Faccion f)
        {
            var (rey, autoridad) = Autoridad(h, f);
            var ps = Propuestas().Where(p => p.ClaseCase == Propuesta.ClaseOneofCase.Anexion).ToList();
            var recibidas = ps.Where(p => p.Anexion.AbsorbidaId == f.Id).ToList();
            var hechas = ps.Where(p => p.Anexion.AbsorbenteId == f.Id).ToList();
            var ocupadas = new HashSet<string>(ps.SelectMany(p => new[] { p.Anexion.AbsorbenteId, p.Anexion.AbsorbidaId }));
            var candidatas = c.Sesion.Almacen.Todas(Entidad.CuerpoOneofCase.Faccion).Select(e => e.Faccion).Where(x => x.Id != f.Id && !ocupadas.Contains(x.Id)).ToList();
            var puedeProponer = autoridad && !ocupadas.Contains(f.Id) && candidatas.Count > 0;
            if (recibidas.Count == 0 && hechas.Count == 0 && !puedeProponer) return;
            var error = Dialogo.Texto("", "error oculto");
            error.name = "error-anexion";
            var s = Dialogo.Caja("diplo-seccion-caja");
            s.name = "anexion";
            s.Add(ConAyuda(Kicker("Anexión"), "Si su Rey acepta, su facción desaparece y todo lo suyo pasa a la tuya."));
            foreach (var p in recibidas)
            {
                Orden Responder(bool si) => new Orden { ResponderAnexion = new OrdenResponderAnexion { PropuestaId = p.Id, Aceptar = si } };
                s.Add(FilaDeDiplomacia($"{NombreDeFaccionCorto(p.Anexion.AbsorbenteId)} te ofrece anexionarte", Plazo(p.ExpiraEnMs) + (rey ? "" : " · solo tu Rey responde"),
                    rey ? BotonDeOrden("Aceptar", "aceptar-" + p.Id, () => Responder(true), error) : null, rey ? BotonDeOrden("Rechazar", "rechazar-" + p.Id, () => Responder(false), error) : null));
            }
            foreach (var p in hechas)
                s.Add(FilaDeDiplomacia($"Propones anexionar a {NombreDeFaccionCorto(p.Anexion.AbsorbidaId)}", Plazo(p.ExpiraEnMs),
                    autoridad ? BotonDeOrden("Retirar", "retirar-" + p.Id, () => new Orden { RetirarAnexion = new OrdenRetirarAnexion { PropuestaId = p.Id } }, error) : null));
            if (puedeProponer)
            {
                var fila = Dialogo.Caja("faccion-fila");
                var combo = new DropdownField(candidatas.Select(x => x.Id).ToList(), 0, NombreDeFaccionCorto, NombreDeFaccionCorto) { name = "sel-anexion" };
                combo.AddToClassList("sigilo-campo");
                combo.AddToClassList("faccion-combo");
                fila.Add(combo);
                fila.Add(BotonDeOrden("Proponer anexión", "btn-anexion", () => new Orden { ProponerAnexion = new OrdenProponerAnexion { FaccionAId = f.Id, FaccionBId = combo.value } }, error));
                s.Add(fila);
            }
            s.Add(error);
            v.Add(s);
        }

        void Fusion(VisualElement v, Heroe h, Faccion f)
        {
            var (rey, _) = Autoridad(h, f);
            var ps = Propuestas().Where(p => p.ClaseCase == Propuesta.ClaseOneofCase.Fusion).ToList();
            var recibidas = ps.Where(p => p.Fusion.FaccionBId == f.Id).ToList();
            var hechas = ps.Where(p => p.Fusion.FaccionAId == f.Id).ToList();
            var ocupadas = new HashSet<string>(ps.SelectMany(p => new[] { p.Fusion.FaccionAId, p.Fusion.FaccionBId }));
            var candidatas = c.Sesion.Almacen.Todas(Entidad.CuerpoOneofCase.Faccion).Select(e => e.Faccion).Where(x => x.Id != f.Id && x.HasReyId && !ocupadas.Contains(x.Id)).ToList();
            var puedeProponer = rey && candidatas.Count > 0;
            if (recibidas.Count == 0 && hechas.Count == 0 && !puedeProponer) return;
            var error = Dialogo.Texto("", "error oculto");
            error.name = "error-fusion";
            var s = Dialogo.Caja("diplo-seccion-caja");
            s.name = "fusion";
            s.Add(ConAyuda(Kicker("Fusión"), "Si su Rey acepta, las dos Facciones desaparecen y nace una nueva con ese nombre y ese Rey (el otro Rey pasa a ser ciudadano). Hereda tu sigilo, el nivel más alto de las dos y la tecnología de ambas."));
            foreach (var p in recibidas)
            {
                Orden Responder(bool si) => new Orden { ResponderFusion = new OrdenResponderFusion { PropuestaId = p.Id, Aceptar = si } };
                var nuevoRey = p.Fusion.NuevoReyId == h.Id ? "tú" : $"el Rey de {NombreDeFaccionCorto(p.Fusion.FaccionAId)}";
                s.Add(FilaDeDiplomacia($"{NombreDeFaccionCorto(p.Fusion.FaccionAId)} propone fusionaros en «{p.Fusion.NuevoNombre}»",
                    $"Rey de la nueva: {nuevoRey} · {Plazo(p.ExpiraEnMs)}{(rey ? "" : " · solo tu Rey responde")}",
                    rey ? BotonDeOrden("Aceptar", "aceptar-" + p.Id, () => Responder(true), error) : null, rey ? BotonDeOrden("Rechazar", "rechazar-" + p.Id, () => Responder(false), error) : null));
            }
            foreach (var p in hechas)
                s.Add(FilaDeDiplomacia($"Propones fusionarte con {NombreDeFaccionCorto(p.Fusion.FaccionBId)} en «{p.Fusion.NuevoNombre}»", Plazo(p.ExpiraEnMs),
                    rey ? BotonDeOrden("Retirar", "retirar-" + p.Id, () => new Orden { RetirarFusion = new OrdenRetirarFusion { PropuestaId = p.Id } }, error) : null));
            if (puedeProponer)
            {
                var fila = Dialogo.Caja("faccion-fila diplo-fusion-form");
                var combo = new DropdownField(candidatas.Select(x => x.Id).ToList(), 0, NombreDeFaccionCorto, NombreDeFaccionCorto) { name = "sel-fusion" };
                combo.AddToClassList("sigilo-campo");
                combo.AddToClassList("faccion-combo");
                fila.Add(combo);
                var nombre = new TextField { name = "nombre-fusion", maxLength = 32 };
                nombre.textEdition.placeholder = "Nombre de la Facción nueva";
                nombre.AddToClassList("campo");
                nombre.AddToClassList("diplo-campo-texto");
                fila.Add(nombre);
                var reySel = new DropdownField(new List<string> { "Rey: tú", "Rey: el de la otra Facción" }, 0) { name = "rey-fusion" };
                reySel.AddToClassList("sigilo-campo");
                reySel.AddToClassList("faccion-combo");
                fila.Add(reySel);
                fila.Add(BotonDeOrden("Proponer fusión", "btn-fusion", () =>
                {
                    var otra = candidatas.First(x => x.Id == combo.value);
                    return new Orden { ProponerFusion = new OrdenProponerFusion { FaccionAId = f.Id, FaccionBId = otra.Id, NuevoNombre = nombre.value.Trim(), NuevoReyId = reySel.index == 0 ? h.Id : otra.ReyId } };
                }, error));
                s.Add(fila);
            }
            s.Add(error);
            v.Add(s);
        }

        // ─────────────── el centro: otra Facción (`htmlFichaFaccion`) ───────────────

        void FichaDeOtraFaccion(VisualElement v, Heroe h, Faccion propia, List<RelacionPolitica> rs,
            (string id, string nombre, Sigilo sigilo, Faccion completa, FaccionDelDirectorio ligera) x)
        {
            var f = x.completa;
            var granRey = f?.Liga != null && f.Liga.HasGranReyId && f.Liga.GranReyId == f.Id;
            var cab = Dialogo.Caja("diplo-ficha-cab");
            cab.name = "diplo-ficha-cab";
            cab.Add(SigiloUI(x.sigilo, 72, granRey));
            var textos = Dialogo.Caja("diplo-ficha-textos");
            textos.Add(Kicker(f != null ? "Facción conocida" : "Del directorio: solo lo público"));
            textos.Add(Dialogo.Texto(x.nombre, "diplo-ficha-nombre"));
            var insignias = Dialogo.Caja("diplo-insignias");
            var rel = RelacionActiva(rs, propia.Id, x.id);
            if (rel != null) insignias.Add(Dialogo.Texto(TextoDeRelacion(rel, propia.Id), "faccion-publica-relacion " + (rel.Tipo == TipoRelacion.RelacionGuerra ? "rel-guerra" : "rel-paz")));
            if (f != null && f.HasDerrotadaPor) insignias.Add(Dialogo.Texto($"Derrotada por {NombreDeFaccionCorto(f.DerrotadaPor)}", "faccion-publica-relacion rel-derrota"));
            else if (x.ligera?.Derrotada == true) insignias.Add(Dialogo.Texto("Derrotada", "faccion-publica-relacion rel-derrota"));
            if (f?.AdmiteOtrasEnAtaques == true) insignias.Add(Dialogo.Texto("Admite a otras en sus ataques", "faccion-publica-relacion rel-paz"));
            if (insignias.childCount > 0) textos.Add(insignias);
            cab.Add(textos);
            v.Add(cab);

            var plazas = PlazasConocidasDe(x.id);
            var datos = Dialogo.Caja("diplo-datos");
            datos.name = "diplo-datos";
            void Dato(string etiqueta, string valor)
            {
                var d = Dialogo.Caja("diplo-dato");
                d.Add(Dialogo.Texto(etiqueta, "diplo-dato-etiqueta"));
                d.Add(Dialogo.Texto(valor, "diplo-dato-valor"));
                datos.Add(d);
            }
            Dato("Nivel", (f?.Nivel ?? x.ligera?.Nivel ?? 1).ToString());
            Dato("Miembros", (f?.CiudadanosIds.Count ?? (int)(x.ligera?.Ciudadanos ?? 0)).ToString());
            Dato("Plazas conocidas", plazas.Count.ToString());
            if (f != null)
            {
                Dato("Reputación", Math.Round((f.Reputacion?.ValorEnT0 ?? 0) / 1000.0).ToString(Es));
                Dato("Rey", f.HasReyId ? Textos.NombreDeHeroe(c.Sesion.Almacen, f.ReyId) : "Sin Rey");
                Dato("Embajador", f.HasEmbajadorId ? Textos.NombreDeHeroe(c.Sesion.Almacen, f.EmbajadorId) : "Sin designar");
                if (f.HasCapitalId) Dato("Capital", plazas.FirstOrDefault(p => p.Id == f.CapitalId) is Asentamiento cap && cap.HasNombre ? cap.Nombre : f.CapitalId);
                if (f.Liga != null)
                    Dato("Liga", (f.Liga.TieneVasallaje ? "Por vasallaje" : "Por alianza") + (granRey ? " · Gran Rey" : f.Liga.HasGranReyId ? $" · Gran Rey: {NombreDeFaccionCorto(f.Liga.GranReyId)}" : ""));
            }
            Dato("¿Bot?", (f?.EsBot ?? x.ligera?.EsBot ?? false) ? "Sí: la lleva un Rey bot" : "No");
            v.Add(datos);

            DiplomaciaCon(v, h, propia, rs, x);
            if (plazas.Count > 0)
            {
                v.Add(Kicker("Sus plazas que conoces"));
                v.Add(Dialogo.Texto(string.Join(", ", plazas.Select(p => p.HasNombre ? p.Nombre : p.Id)), "faccion-nota"));
            }
        }

        /// <summary>Lo vuestro y «Nueva propuesta» (`htmlDiplomacia` con `con`, `propuestasEntre`, `htmlPropuesta`).</summary>
        void DiplomaciaCon(VisualElement v, Heroe h, Faccion f, List<RelacionPolitica> rs, (string id, string nombre, Sigilo sigilo, Faccion completa, FaccionDelDirectorio ligera) x)
        {
            var (rey, autoridad) = Autoridad(h, f);
            var error = Dialogo.Texto("", "error oculto");
            error.name = "error-diplomacia";
            var s = Dialogo.Caja("diplo-seccion-caja");
            s.name = "diplomacia-con";
            s.Add(ConAyuda(Kicker("Diplomacia"), AyudaDiplomacia));
            foreach (var r in rs.Where(r => r.Estado == EstadoRelacion.RelacionActiva && !string.IsNullOrEmpty(r.Id) && Entre(r.FaccionAId, r.FaccionBId, f.Id, x.id)))
                s.Add(FilaDeRelacion(r, f, autoridad, error));

            var ps = Propuestas().Where(p => p.ClaseCase switch
            {
                Propuesta.ClaseOneofCase.Relacion => Entre(p.Relacion.FaccionAId, p.Relacion.FaccionBId, f.Id, x.id),
                Propuesta.ClaseOneofCase.Anexion => Entre(p.Anexion.AbsorbenteId, p.Anexion.AbsorbidaId, f.Id, x.id),
                Propuesta.ClaseOneofCase.Fusion => Entre(p.Fusion.FaccionAId, p.Fusion.FaccionBId, f.Id, x.id),
                _ => false,
            }).ToList();
            s.Add(Kicker($"Propuestas activas ({ps.Count})"));
            if (ps.Count == 0) s.Add(Dialogo.Texto("Ninguna propuesta en curso con ella.", "faccion-nota"));
            foreach (var p in ps)
            {
                if (p.ClaseCase == Propuesta.ClaseOneofCase.Relacion) { s.Add(FilaDePropuestaDeRelacion(p, f, rey, autoridad, rs, error)); continue; }
                if (p.ClaseCase == Propuesta.ClaseOneofCase.Anexion)
                {
                    if (p.Anexion.AbsorbidaId == f.Id)
                    {
                        Orden R(bool si) => new Orden { ResponderAnexion = new OrdenResponderAnexion { PropuestaId = p.Id, Aceptar = si } };
                        s.Add(FilaDeDiplomacia("Te propone anexionarte", "Tu Facción desaparecería y todo lo suyo pasaría a la de ella · " + Plazo(p.ExpiraEnMs),
                            rey ? BotonDeOrden("Aceptar", "aceptar-" + p.Id, () => R(true), error) : null, rey ? BotonDeOrden("Rechazar", "rechazar-" + p.Id, () => R(false), error) : null));
                    }
                    else s.Add(FilaDeDiplomacia("Propones anexionarla", Plazo(p.ExpiraEnMs),
                        autoridad ? BotonDeOrden("Retirar", "retirar-" + p.Id, () => new Orden { RetirarAnexion = new OrdenRetirarAnexion { PropuestaId = p.Id } }, error) : null));
                    continue;
                }
                var fu = p.Fusion;
                if (fu.FaccionBId == f.Id)
                {
                    Orden R(bool si) => new Orden { ResponderFusion = new OrdenResponderFusion { PropuestaId = p.Id, Aceptar = si } };
                    s.Add(FilaDeDiplomacia($"Te propone fusionaros en «{fu.NuevoNombre}»", $"Rey de la nueva: {(fu.NuevoReyId == h.Id ? "tú" : Textos.NombreDeHeroe(c.Sesion.Almacen, fu.NuevoReyId))} · {Plazo(p.ExpiraEnMs)}",
                        rey ? BotonDeOrden("Aceptar", "aceptar-" + p.Id, () => R(true), error) : null, rey ? BotonDeOrden("Rechazar", "rechazar-" + p.Id, () => R(false), error) : null));
                }
                else s.Add(FilaDeDiplomacia($"Propones fusionaros en «{fu.NuevoNombre}»", Plazo(p.ExpiraEnMs),
                    rey ? BotonDeOrden("Retirar", "retirar-" + p.Id, () => new Orden { RetirarFusion = new OrdenRetirarFusion { PropuestaId = p.Id } }, error) : null));
            }
            NuevaPropuesta(s, h, f, rs, x, rey, autoridad, error);
            if (!autoridad) s.Add(Dialogo.Texto("Proponer alianzas, vasallajes, anexiones o la guerra es cosa del Rey o del Embajador; la fusión, del Rey.", "faccion-nota"));
            s.Add(error);
            v.Add(s);
        }

        static readonly (string tipo, string etiqueta, string icono, string familia, string ayuda)[] OpcionesDePropuesta =
        {
            ("alianza", "Alianza", "alianza", "glifo", "Os defendéis y comerciáis como aliadas. Nace si su Rey acepta."),
            ("vasallaje", "Vasallaje", "nobleza", "glifo", "Tú serías la señora: te pagan un tributo por minuto y tú las defiendes. Nace si su Rey acepta."),
            ("anexion", "Anexión", "templo", "glifo", "Su Facción desaparece y todo lo suyo pasa a la tuya. Nace si su Rey acepta."),
            ("fusion", "Fusión", "fusion", "glifo", "Las dos desaparecen y nace una nueva con el nombre y el Rey que elijas. Nace si su Rey acepta."),
            ("guerra", "Guerra", "atacar", "glifo", "No se propone: se declara al momento y arrastra a señor y vasallos del rival."),
        };

        /// <summary>«Nueva propuesta» (`htmlPropuesta`): una cláusula por propuesta, con sus términos, y «Proponer» o «Declarar la guerra».</summary>
        void NuevaPropuesta(VisualElement v, Heroe h, Faccion f, List<RelacionPolitica> rs, (string id, string nombre, Sigilo sigilo, Faccion completa, FaccionDelDirectorio ligera) x,
            bool rey, bool autoridad, Label error)
        {
            if (!autoridad) return;
            var ps = Propuestas();
            var activa = RelacionActiva(rs, f.Id, x.id);
            var relacionPendiente = ps.Any(p => p.ClaseCase == Propuesta.ClaseOneofCase.Relacion && Entre(p.Relacion.FaccionAId, p.Relacion.FaccionBId, f.Id, x.id));
            bool EnAnexion(string id) => ps.Any(p => p.ClaseCase == Propuesta.ClaseOneofCase.Anexion && (p.Anexion.AbsorbenteId == id || p.Anexion.AbsorbidaId == id));
            bool EnFusion(string id) => ps.Any(p => p.ClaseCase == Propuesta.ClaseOneofCase.Fusion && (p.Fusion.FaccionAId == id || p.Fusion.FaccionBId == id));
            var posibles = new HashSet<string>();
            if (activa == null && !relacionPendiente && CabeAlianza(rs, f.Id) && CabeAlianza(rs, x.id)) posibles.Add("alianza");
            if (activa == null && !relacionPendiente) posibles.Add("vasallaje");
            if (activa == null) posibles.Add("guerra");
            if (x.completa != null && !EnAnexion(f.Id) && !EnAnexion(x.id)) posibles.Add("anexion");
            if (rey && x.completa != null && x.completa.HasReyId && !EnFusion(x.id)) posibles.Add("fusion");
            if (posibles.Count == 0) return;
            if (!borradores.TryGetValue(x.id, out var b)) borradores[x.id] = b = new Borrador();
            if (b.Tipo != null && !posibles.Contains(b.Tipo)) b.Tipo = null;
            void Repintar() { firmaFaccion = null; PintarFaccion(); }

            var caja = Dialogo.Caja("propuesta");
            caja.name = "nueva-propuesta";
            caja.Add(Kicker("Nueva propuesta"));
            var barra = Dialogo.Caja("propuesta-barra");
            barra.tooltip = "Qué proponer";
            foreach (var (tipo, etiqueta, icono, familia, ayuda) in OpcionesDePropuesta.Where(o => posibles.Contains(o.tipo)))
            {
                var t = tipo;
                var op = new Button(() => { b.Tipo = b.Tipo == t ? null : t; Repintar(); }) { name = "opcion-" + tipo };
                op.AddToClassList("propuesta-opcion");
                op.EnableInClassList("guerra", tipo == "guerra");
                op.EnableInClassList("puesta", b.Tipo == tipo);
                op.tooltip = ayuda + (b.Tipo != null && b.Tipo != tipo ? " Sustituye a lo que lleva la propuesta: el motor admite una cosa por propuesta." : "");
                if (IconosDelMundo.Icono(familia, icono, 16) is VisualElement i) op.Add(i);
                op.Add(Dialogo.Texto(etiqueta, "propuesta-opcion-texto"));
                barra.Add(op);
            }
            caja.Add(barra);
            var cuerpo = Dialogo.Caja("propuesta-cuerpo");
            if (b.Tipo == null) cuerpo.Add(Dialogo.Texto("Pulsa arriba lo que quieres proponer: se añade aquí, ajustas sus términos y la mandas entera con «Proponer».", "faccion-nota"));
            else
            {
                var o = OpcionesDePropuesta.First(z => z.tipo == b.Tipo);
                var clausula = Dialogo.Caja("propuesta-clausula" + (b.Tipo == "guerra" ? " guerra" : ""));
                clausula.name = "clausula-" + b.Tipo;
                var ccab = Dialogo.Caja("propuesta-clausula-cab");
                if (IconosDelMundo.Icono(o.familia, o.icono, 18) is VisualElement ic) ccab.Add(ic);
                ccab.Add(Dialogo.Texto(o.etiqueta, "propuesta-clausula-nombre"));
                ccab.Add(Dialogo.Texto(o.ayuda, "propuesta-clausula-ayuda"));
                var quitar = Dialogo.Boton("✕", "propuesta-quitar", () => { b.Tipo = null; Repintar(); });
                quitar.name = "quitar-clausula";
                quitar.tooltip = "Quitarla de la propuesta";
                ccab.Add(quitar);
                clausula.Add(ccab);
                if (b.Tipo == "vasallaje")
                {
                    var terminos = Dialogo.Caja("propuesta-terminos");
                    var recurso = new DropdownField("Tributo", RecursosDeTributo.ToList(), Math.Max(0, Array.IndexOf(RecursosDeTributo, b.Recurso)), CosasDelMapa.NombreDeRecurso, CosasDelMapa.NombreDeRecurso) { name = "tributo-recurso" };
                    recurso.AddToClassList("sigilo-campo");
                    recurso.AddToClassList("diplo-campo");
                    recurso.RegisterValueChangedCallback(e => b.Recurso = e.newValue);
                    terminos.Add(recurso);
                    var cantidad = new IntegerField("Por minuto") { name = "tributo-cantidad", value = b.Cantidad };
                    cantidad.AddToClassList("diplo-campo");
                    cantidad.RegisterValueChangedCallback(e => b.Cantidad = Math.Max(0, e.newValue));
                    terminos.Add(cantidad);
                    clausula.Add(terminos);
                }
                if (b.Tipo == "fusion")
                {
                    var terminos = Dialogo.Caja("propuesta-terminos");
                    var nombre = new TextField("Nombre de la nueva") { name = "fusion-nombre", maxLength = 32, value = b.Nombre };
                    nombre.AddToClassList("diplo-campo");
                    nombre.RegisterValueChangedCallback(e => b.Nombre = e.newValue);
                    terminos.Add(nombre);
                    var reyNueva = new DropdownField("Rey de la nueva", new List<string> { "Tú", "El de la otra Facción" }, b.ReyTu ? 0 : 1) { name = "fusion-rey" };
                    reyNueva.AddToClassList("sigilo-campo");
                    reyNueva.AddToClassList("diplo-campo");
                    reyNueva.RegisterValueChangedCallback(_ => b.ReyTu = reyNueva.index == 0);
                    terminos.Add(reyNueva);
                    clausula.Add(terminos);
                }
                cuerpo.Add(clausula);
            }
            caja.Add(cuerpo);
            var pie = Dialogo.Caja("propuesta-pie");
            var guerra = b.Tipo == "guerra";
            Button mandar = null;
            mandar = Dialogo.Boton(guerra ? "Declarar la guerra" : "Proponer", guerra ? "boton-peligro" : "boton-primario", async () =>
            {
                Orden orden = null;
                (string, string, string, bool)? confirmar = null;
                switch (b.Tipo)
                {
                    case "guerra":
                        confirmar = ("¿Declarar la guerra?", "También la declaras al señor y a los vasallos del rival, y solo termina con la paz de las dos Facciones.", "Declarar la guerra", true);
                        orden = new Orden { DeclararGuerra = new OrdenDeclararGuerra { FaccionAId = f.Id, FaccionBId = x.id } };
                        break;
                    case "vasallaje":
                        confirmar = ("¿Proponer el vasallaje?", $"Solo nace si su Rey acepta. Entonces te pagarán {b.Cantidad} de {CosasDelMapa.NombreDeRecurso(b.Recurso)} por minuto y tú los defenderás (una guerra contra ellos es una guerra contra ti).", "Proponer", false);
                        orden = new Orden { ProponerRelacion = new OrdenProponerRelacion { Tipo = "vasallaje", FaccionAId = f.Id, FaccionBId = x.id, TributoRecurso = b.Recurso, TributoCantidad = b.Cantidad * 1000L } };
                        break;
                    case "alianza":
                        orden = new Orden { ProponerRelacion = new OrdenProponerRelacion { Tipo = "alianza", FaccionAId = f.Id, FaccionBId = x.id } };
                        break;
                    case "anexion":
                        confirmar = ("¿Proponer la anexión?", "Si su Rey acepta, su Facción desaparece y todo lo suyo pasa a la tuya.", "Proponer", false);
                        orden = new Orden { ProponerAnexion = new OrdenProponerAnexion { FaccionAId = f.Id, FaccionBId = x.id } };
                        break;
                    case "fusion":
                        confirmar = ("¿Proponer la fusión?", $"Si su Rey acepta, las dos Facciones desaparecen y nace «{b.Nombre.Trim()}».", "Proponer", false);
                        orden = new Orden { ProponerFusion = new OrdenProponerFusion { FaccionAId = f.Id, FaccionBId = x.id, NuevoNombre = b.Nombre.Trim(), NuevoReyId = b.ReyTu ? h.Id : x.completa.ReyId } };
                        break;
                }
                if (orden == null) return;
                if (confirmar is var (t, tx, ac, pel) && !await dialogo.Abrir(tx, t, ac, "Cancelar", pel)) return;
                var r = await OrdenDelPanel(orden, m => Poner(error, m), () => { firmaFaccion = null; }, mandar);
                if (r?.Resultado == Resultado.OrdenAceptada) borradores.Remove(x.id);
            });
            mandar.name = "btn-proponer";
            var faltaNombre = b.Tipo == "fusion" && string.IsNullOrWhiteSpace(b.Nombre);
            var falta = Dialogo.Texto("Ponle nombre a la Facción nueva", "propuesta-falta" + (faltaNombre ? "" : " oculto"));
            pie.Add(falta);
            mandar.SetEnabled(b.Tipo != null && !faltaNombre);
            // El nombre de la nueva enciende «Proponer» al escribirlo, sin repintar.
            cuerpo.Q<TextField>("fusion-nombre")?.RegisterValueChangedCallback(e =>
            {
                var vacio = string.IsNullOrWhiteSpace(e.newValue);
                mandar.SetEnabled(!vacio);
                falta.EnableInClassList("oculto", !vacio);
            });
            pie.Add(mandar);
            caja.Add(pie);
            v.Add(caja);
        }
    }
}
