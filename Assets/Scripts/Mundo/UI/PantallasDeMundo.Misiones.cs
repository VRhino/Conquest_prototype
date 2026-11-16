// Las misiones (paso 12 del bloque 4, tanda 3), calcadas de BronzeAgeClient (`ui/misiones.ts`, `ui/tablon.ts` y `ui/recompensasMision.ts`):
// - El botón «Misiones» de la barra, con la insignia de las que puedes reclamar, abre el diario: los nombres a la izquierda (Mías · Mi plaza ·
//   Mi Facción) y la misión abierta a la derecha (objetivos, detalles y recompensas), con «Seguir» y «Reclamar» abajo. Dentro de una plaza con
//   tablón, arriba, «Misiones» y «Tablón de …».
// - El rastreador de las seguidas (3 como mucho), arriba a la derecha en el mapa, en la plaza (encima de las tarjetas del ejército) y en el
//   campamento (Víctor, 6). Lo seguido se guarda en el equipo por héroe; lo nuevo se avisa.
// - Los tablones de misiones recurrentes de la plaza (en el diario) y de la Facción (Facción › Tablón): aceptar, reclamar y, el Gobernador o el
//   Rey, elegir las de la semana que viene. Facción › Misiones, la lista de las de la Facción.
// Las misiones de cadena no se aceptan: se activan solas al cumplirse su previa. El motor valida todo; su motivo sale tal cual.
using System;
using System.Collections.Generic;
using System.Linq;
using BronzeAge.Contract.V1;
using ConquestTactics.Mundo.Mapa;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace ConquestTactics.Mundo.UI
{
    public partial class PantallasDeMundo
    {
        const int MaxSeguidas = 3, MaxElegidas = 7;
        static readonly (string dueno, string nombre)[] DuenosDeMision = { ("heroe", "Mías"), ("plaza", "Mi plaza"), ("faccion", "Mi Facción") };

        static readonly Dictionary<string, string> CargoDeMision = new Dictionary<string, string>
        {
            ["rey"] = "Rey", ["embajador"] = "Embajador", ["gobernador"] = "Gobernador", ["maestroObras"] = "Maestro de Obras", ["maestroCaravanas"] = "Maestro de Caravanas",
            ["tesorero"] = "Tesorero", ["general"] = "General", ["sacerdote"] = "Sacerdote",
        };

        // ─────────────── los textos ───────────────

        JToken TextosDeMisiones { get { try { return c.Datos.Bloque("Misiones_Textos", "TEXTOS_MISIONES"); } catch (FaltaDato) { return null; } } }

        (string titulo, string texto, string explicacion) TextoDeMision(string id)
        {
            var t = TextosDeMisiones?["misiones"]?[id];
            return ((string)t?["titulo"] ?? id, (string)t?["texto"] ?? "", (string)t?["explicacion"]);
        }

        string NombreDeContador(string contador) => (string)TextosDeMisiones?["contadores"]?[contador] ?? contador;

        // ─────────────── de quién es cada misión ───────────────

        Asentamiento PlazaDondeEstas(Heroe h) =>
            h?.Ubicacion?.DondeCase == UbicacionHeroe.DondeOneofCase.AsentamientoId ? c.Sesion.Almacen.Buscar(TipoEntidad.Asentamiento, h.Ubicacion.AsentamientoId)?.Asentamiento : null;

        /// <summary>Las misiones de un dueño y su id para reclamar (null si ahora no se ve: la plaza, solo estando dentro).</summary>
        (List<Mision> misiones, string duenoId) DelDueno(Heroe h, string d)
        {
            if (h == null) return (new List<Mision>(), null);
            if (d == "heroe") return (h.Misiones.ToList(), h.Id);
            if (d == "plaza") { var a = PlazaDondeEstas(h); return a == null ? (new List<Mision>(), null) : (a.Misiones.ToList(), a.Id); }
            var f = MiFaccion(h);
            return f == null ? (new List<Mision>(), null) : (f.Misiones.ToList(), f.Id);
        }

        /// <summary>Quién reclama (Misiones §4): las tuyas, tú; las de una plaza, su Gobernador o, sin él, un residente; las de la Facción, su Rey.
        /// null si puedes tú.</summary>
        string QuienReclama(Heroe h, string d)
        {
            if (d == "heroe") return null;
            if (d == "faccion") return MiFaccion(h) is Faccion f && f.HasReyId && f.ReyId == h.Id ? null : "La reclama el Rey de tu Facción.";
            var a = PlazaDondeEstas(h);
            if (a == null) return "La reclama el Gobernador de la plaza.";
            var gobernador = TitularDe(a, "gobernador");
            if (gobernador != null) return gobernador == h.Id ? null : "La reclama el Gobernador de la plaza.";
            return Reside(a, h) ? null : "Sin Gobernador, la reclama un residente de la plaza.";
        }

        /// <summary>Las cumplidas que puedes reclamar ya: la insignia del botón Misiones.</summary>
        int MisionesPorReclamar(Heroe h) =>
            h == null ? 0 : DuenosDeMision.Sum(x => QuienReclama(h, x.dueno) != null ? 0 : DelDueno(h, x.dueno).misiones.Count(m => m.Estado == EstadoMision.MisionCumplida));

        // ─────────────── seguir (en el equipo, por héroe) ───────────────

        sealed class Seguimiento
        {
            public List<string> Seguidas = new List<string>(), Conocidas = new List<string>(), Nuevas = new List<string>();
            public bool Plegado;
        }

        Seguimiento seguimiento;
        string claveSeguimiento;
        static string ClaveMision(string d, string id) => d + ":" + id;

        Seguimiento SeguimientoDe(Heroe h)
        {
            var clave = "mundo.misiones." + h.Id;
            if (clave == claveSeguimiento) return seguimiento;
            claveSeguimiento = clave;
            try { seguimiento = JsonConvert.DeserializeObject<Seguimiento>(PlayerPrefs.GetString(clave, "")); }
            catch (JsonException) { seguimiento = null; }
            return seguimiento;
        }

        void GuardarSeguimiento()
        {
            if (claveSeguimiento == null || seguimiento == null) return;
            PlayerPrefs.SetString(claveSeguimiento, JsonConvert.SerializeObject(seguimiento));
            PlayerPrefs.Save();
        }

        /// <summary>Tras cada cambio (`actualizarSeguimiento`): lo nuevo se avisa y se marca «nueva»; las tuyas entran solas en el rastreador mientras haya
        /// hueco. La primera vez no avisa: sigue las tuyas que ya están en marcha. Las reclamadas salen del rastreador.</summary>
        void ActualizarSeguimiento(Heroe h)
        {
            var previo = SeguimientoDe(h);
            var vivas = DuenosDeMision.SelectMany(x => DelDueno(h, x.dueno).misiones.Where(m => m.Estado != EstadoMision.MisionReclamada).Select(m => (d: x.dueno, m))).ToList();
            if (previo == null)
            {
                seguimiento = new Seguimiento
                {
                    Seguidas = vivas.Where(x => x.d == "heroe").Take(MaxSeguidas).Select(x => ClaveMision(x.d, x.m.Id)).ToList(),
                    Conocidas = vivas.Select(x => ClaveMision(x.d, x.m.Id)).ToList(),
                };
                GuardarSeguimiento();
                return;
            }
            var cambio = false;
            foreach (var (d, m) in vivas)
            {
                var k = ClaveMision(d, m.Id);
                if (previo.Conocidas.Contains(k)) continue;
                previo.Conocidas.Add(k);
                previo.Nuevas.Add(k);
                if (d == "heroe" && previo.Seguidas.Count < MaxSeguidas) previo.Seguidas.Add(k);
                c.Avisos.Anotar(new EntradaAviso { Titulo = "Misiones", Texto = "Nueva misión: " + TextoDeMision(m.Id).titulo, Clase = ClaseAviso.Mirada, Categoria = CategoriaAviso.Economia });
                cambio = true;
            }
            var reclamadas = new HashSet<string>(DuenosDeMision.SelectMany(x => DelDueno(h, x.dueno).misiones.Where(m => m.Estado == EstadoMision.MisionReclamada).Select(m => ClaveMision(x.dueno, m.Id))));
            var antes = previo.Seguidas.Count;
            previo.Seguidas.RemoveAll(reclamadas.Contains);
            if (cambio || previo.Seguidas.Count != antes) GuardarSeguimiento();
        }

        void AlternarSeguir(Heroe h, string k)
        {
            var s = SeguimientoDe(h);
            if (s == null) return;
            if (s.Seguidas.Contains(k)) s.Seguidas.Remove(k);
            else if (s.Seguidas.Count < MaxSeguidas) s.Seguidas.Add(k);
            GuardarSeguimiento();
        }

        // ─────────────── las piezas ───────────────

        /// <summary>Las barras de los objetivos (`barras`): la barra y, debajo, «12 / 50 infantería»; finas en el rastreador.</summary>
        VisualElement BarrasDeMision(IEnumerable<CondicionMision> condiciones, bool fino = false)
        {
            var v = Dialogo.Caja("mision-conds");
            foreach (var cd in condiciones)
            {
                var k = cd.Umbral > 0 ? Math.Min(1.0, (double)cd.Avance / cd.Umbral) : 1;
                var fila = Dialogo.Caja("mision-cond" + (fino ? " fino" : ""));
                var barra = Dialogo.Caja("mision-barra");
                var i = Dialogo.Caja("mision-barra-i");
                i.style.width = Length.Percent((float)(k * 100));
                barra.Add(i);
                fila.Add(barra);
                if (!fino) fila.Add(Dialogo.Texto($"{Math.Min(cd.Avance, cd.Umbral)} / {cd.Umbral} {NombreDeContador(cd.Contador)}", "mision-cond-texto"));
                v.Add(fila);
            }
            return v;
        }

        /// <summary>Las recompensas en etiquetas con su icono (`premiosDeMision`): la fija de su tipo (plaza → participación, Facción → XP) y la lista
        /// libre; adónde va cada una, al pasar el ratón. `cobro`: personal, plaza o faccion.</summary>
        VisualElement Recompensas(ulong participacion, long xpMilesimas, IEnumerable<RecompensaMision> lista, string cobro, bool grupal = false, bool diariaIndividual = false)
        {
            var almacen = cobro == "personal" ? "a tu almacén personal" : cobro == "plaza" ? "al almacén de la plaza" : "al almacén de la capital de la Facción";
            var fila = Dialogo.Caja("diario-premios");
            void Premio(VisualElement icono, string cifra, string nombre, string destino, string tipo, bool fija)
            {
                var p = Dialogo.Caja($"diario-premio diario-premio--{tipo}" + (fija ? " fija" : ""));
                p.tooltip = destino + (fija ? " · recompensa fija" : "");
                if (icono != null) p.Add(icono);
                p.Add(Dialogo.Texto(cifra, "diario-premio-cifra"));
                p.Add(Dialogo.Texto(nombre, "diario-premio-nombre"));
                fila.Add(p);
            }
            string N(double n) => n == Math.Floor(n) ? n.ToString("0", Es) : n.ToString("0.##", Es);
            if (participacion > 0)
                Premio(IconosDelMundo.Icono("glifo", "templo", 15), N(participacion), "de participación",
                    grupal ? "Participación en la plaza, a partes iguales entre los que aportaron (sin mercenarios contratados)"
                        : cobro == "personal" ? "Participación en la plaza del tablón, para ti" : "Participación en la plaza, para quien la reclama", "participacion", true);
            if (xpMilesimas > 0) Premio(IconosDelMundo.Icono("glifo", "xp", 15), N(xpMilesimas / 1000.0), "XP de Facción", "Experiencia para la Facción", "xp", true);
            foreach (var r in lista)
            {
                if (r.Cantidad <= 0) continue;
                switch (r.Tipo)
                {
                    // recurso, oro y xp vienen en milésimas; una tropa, en soldados (`recompensasMision`); un objeto de la lista no se enseña, como allí.
                    case "xp": Premio(IconosDelMundo.Icono("glifo", "xp", 15), N(r.Cantidad / 1000.0), "XP de Facción", cobro == "personal" ? "Experiencia para tu Facción (sin Facción no suma)" : "Experiencia para la Facción de la plaza", "xp", false); break;
                    case "oro": Premio(IconosDelMundo.Icono("recurso", "oro", 15), N(r.Cantidad / 1000.0), "oro", "Oro " + almacen, "oro", false); break;
                    case "recurso": Premio(IconosDelMundo.Icono("recurso", r.Clave, 15), N(r.Cantidad / 1000.0), CosasDelMapa.NombreDeRecurso(r.Clave).ToLowerInvariant(), $"{CosasDelMapa.NombreDeRecurso(r.Clave)} {almacen}", "recurso", false); break;
                    case "tropa": Premio(PiezasDeTropa.Miniatura(r.Clave, 18), N(r.Cantidad), NombreDeTropa(r.Clave), "Soldados que nacen en el campamento donde resides: completan tu escuadra de esa tropa hasta su tope y lo que sobra se pierde", "tropa", false); break;
                }
            }
            // Una diaria individual da además los objetos de `BOTIN_OBJETOS.misionDiaria` (`premiosDeDiaria`): los seguros, con su cifra; los de suerte, con su %.
            if (diariaIndividual)
            {
                JToken tabla = null;
                try { tabla = c.Datos.Bloque("Objetos_Del_Heroe", "BOTIN_OBJETOS")?["misionDiaria"]; } catch (FaltaDato) { }
                foreach (var o in tabla?.OfType<JObject>() ?? Enumerable.Empty<JObject>())
                {
                    var prob = (double?)o["probabilidad"] ?? 1;
                    var suerte = prob < 1 ? $" ({Math.Round(prob * 100)} %)" : "";
                    var nombre = (string)FichaDelMotor((string)o["objeto"])?["nombre"] ?? (string)o["objeto"];
                    Premio(IconosDelMundo.Icono("glifo", "abrir", 15), N((double?)o["cantidad"] ?? 1), nombre + suerte,
                        "A tu mochila; si no cabe, a tu almacén de objetos, y si tampoco, se pierde" + (suerte != "" ? ". Sale con esa probabilidad" : ""), "objeto", false);
                }
            }
            if (fila.childCount == 0) fila.Add(Dialogo.Texto("Sin recompensa", "diario-premio-nombre"));
            return fila;
        }

        static string CobroDe(string d) => d == "heroe" ? "personal" : d;

        VisualElement RecompensasDe(string d, Mision m) => Recompensas(m.Participacion, m.RecompensaExperiencia, m.Recompensas, CobroDe(d));

        // ─────────────── el botón y el diario ───────────────

        VisualElement misionesVentana;
        string firmaMisiones, misionAbierta;
        /// <summary>«misiones» o «tablon» (el de la plaza donde estás).</summary>
        string vistaDiario = "misiones";
        readonly HashSet<string> gruposPlegados = new HashSet<string>();
        public bool MisionesAbiertas => misionesVentana?.panel != null;

        /// <summary>El botón «Misiones» (`barraJugador.ts`) tras la bandera de la Facción, con la insignia de las que puedes reclamar.</summary>
        void BotonDeMisiones(VisualElement barra)
        {
            var junto = barra.Q<Button>("faccion");
            var b = new Button(AlternarMisiones) { name = "misiones", tooltip = "Misiones (M)" };
            IconoDeBarra(b, "misiones");
            var cuenta = Dialogo.Texto("", "barra-cuenta oculto");
            cuenta.name = "misiones-cuenta";
            b.Add(cuenta);
            barra.Insert(junto != null ? barra.IndexOf(junto) + 1 : 0, b);
        }

        void AlternarMisiones()
        {
            if (MisionesAbiertas) CerrarMisiones();
            else AbrirMisiones();
        }

        /// <summary>Abre el diario; con `dueno` e `id`, en esa misión (desde el rastreador).</summary>
        void AbrirMisiones(string dueno = null, string id = null)
        {
            var interior = SelectorDePersonaje.Interior;
            if (dueno != null && id != null) { misionAbierta = ClaveMision(dueno, id); vistaDiario = "misiones"; }
            if (MisionesAbiertas) { PintarMisiones(); return; }
            if (HeroeAhora() == null || (interior != null && interior.PanelAbierto && !BaseAbierta && !FaccionAbierta)) return;
            if (BaseAbierta) CerrarBase();
            if (FaccionAbierta) CerrarFaccion();
            var montada = pantalla.childCount > 0 ? pantalla[0] : null;
            if (montada == null) return;
            if (interior != null) interior.PanelAbierto = true;
            if (mapa != null) mapa.EntradaApagada = true;
            montada.Q(className: "barra-arriba")?.Q<Button>("misiones")?.AddToClassList("activo");
            Ver(montada.Q("menu-barra"), false);
            var velo = Dialogo.Caja("diario-velo");
            velo.name = "misiones-ventana";
            if (Resources.Load<StyleSheet>("MundoUI/Plaza") is StyleSheet hoja) velo.styleSheets.Add(hoja);
            velo.RegisterCallback<PointerDownEvent>(e => { if (e.target == velo) CerrarMisiones(); });
            misionesVentana = velo;
            montada.Add(velo);
            firmaMisiones = null;
            PintarMisiones();
            RepintarCada(velo, 500, () => { if (FirmaDeMisiones() != firmaMisiones) PintarMisiones(); });
        }

        void CerrarMisiones()
        {
            if (!MisionesAbiertas) return;
            var montada = misionesVentana.parent;
            misionesVentana.RemoveFromHierarchy();
            misionesVentana = null;
            montada?.Q(className: "barra-arriba")?.Q<Button>("misiones")?.RemoveFromClassList("activo");
            if (SelectorDePersonaje.Interior != null) SelectorDePersonaje.Interior.PanelAbierto = false;
            if (mapa != null) mapa.EntradaApagada = false;
        }

        /// <summary>M abre o cierra el diario; Esc lo cierra (salvo escribiendo en un campo).</summary>
        void TeclaDeMisiones()
        {
            var teclado = UnityEngine.InputSystem.Keyboard.current;
            if (teclado == null) return;
            if (MisionesAbiertas && teclado.escapeKey.wasPressedThisFrame) { CerrarMisiones(); return; }
            var foco = pantalla.panel?.focusController?.focusedElement;
            if (teclado.mKey.wasPressedThisFrame && !(foco is TextField) && !(foco is IntegerField) && (MisionesAbiertas || pantalla.Q<Button>("misiones") != null)) AlternarMisiones();
        }

        string FirmaDeMisiones()
        {
            var h = HeroeAhora();
            var plaza = PlazaDondeEstas(h);
            var s = h != null ? SeguimientoDe(h) : null;
            return $"{h?.Version}|{plaza?.Version}|{plaza?.VersionInterior}|{MiFaccion(h)?.Version}|{misionAbierta}|{vistaDiario}|{string.Join(",", s?.Seguidas ?? new List<string>())}|{s?.Nuevas.Count}|{string.Join(",", gruposPlegados)}|{string.Join(",", borradoresTablon.Keys)}|{(long)(AhoraMs / 60_000)}";
        }

        /// <summary>La ventana (`htmlPanelMisiones`): centrada, con la lista a la izquierda y la misión abierta a la derecha; dentro de una plaza con
        /// tablón, arriba, «Misiones» y «Tablón de …».</summary>
        void PintarMisiones()
        {
            if (!MisionesAbiertas) return;
            var h = HeroeAhora();
            firmaMisiones = FirmaDeMisiones();
            var scroll = misionesVentana.Q<ScrollView>("diario-indice")?.scrollOffset ?? Vector2.zero;
            misionesVentana.Clear();
            if (h == null) return;
            ActualizarSeguimiento(h);
            var s = SeguimientoDe(h);
            var placa = Dialogo.Caja("placa diario");
            placa.name = "diario";
            misionesVentana.Add(placa);
            var cab = Dialogo.Caja("diario-cab");
            cab.Add(ConAyuda(Dialogo.Texto("MISIONES", "dialogo-titulo"),
                $"Se activan solas: cuando cumples una, aparecen las que venían detrás. Sigue hasta {MaxSeguidas} para verlas en pantalla. La recompensa no llega sola: pulsa Reclamar cuando esté cumplida."));
            var plaza = PlazaDondeEstas(h);
            var conTablon = plaza?.Tablon != null;
            if (!conTablon) vistaDiario = "misiones";
            if (conTablon)
            {
                var vistas = Dialogo.Caja("diario-vistas");
                foreach (var (id, nombre) in new[] { ("misiones", "Misiones"), ("tablon", "Tablón de " + (plaza.HasNombre ? plaza.Nombre : plaza.Id)) })
                {
                    var b = Dialogo.Boton(nombre, "plaza-subtab" + (vistaDiario == id ? " activo" : ""), () => { vistaDiario = id; PintarMisiones(); });
                    b.name = "diario-vista-" + id;
                    vistas.Add(b);
                }
                cab.Add(vistas);
            }
            cab.Add(Dialogo.Caja("recl-hueco"));
            cab.Add(Dialogo.Texto($"Sigues {s?.Seguidas.Count ?? 0}/{MaxSeguidas}", "diario-sigues"));
            var cerrar = Dialogo.Boton("Cerrar", "boton-secundario", CerrarMisiones);
            cerrar.name = "misiones-cerrar";
            cab.Add(cerrar);
            placa.Add(cab);
            var error = Dialogo.Texto("", "error oculto");
            error.name = "misiones-error";
            void Error(string t) => Poner(error, t);
            if (vistaDiario == "tablon" && conTablon)
            {
                var cuerpoTablon = new ScrollView(ScrollViewMode.Vertical) { horizontalScrollerVisibility = ScrollerVisibility.Hidden, name = "diario-tablon" };
                cuerpoTablon.AddToClassList("diario-tablon");
                TablonDeMisiones(cuerpoTablon, h, "plaza", plaza.Id, plaza.Tablon, Error, PintarMisiones);
                placa.Add(cuerpoTablon);
                placa.Add(error);
                return;
            }

            // La lista: por dueño, primero las cumplidas, luego las activas; las hechas, plegadas al final.
            var todas = DuenosDeMision.SelectMany(x =>
            {
                var ms = DelDueno(h, x.dueno).misiones;
                return ms.Where(m => m.Estado == EstadoMision.MisionCumplida).Concat(ms.Where(m => m.Estado == EstadoMision.MisionActiva)).Concat(ms.Where(m => m.Estado == EstadoMision.MisionReclamada)).Select(m => (d: x.dueno, m));
            }).ToList();
            var elegida = todas.FirstOrDefault(x => ClaveMision(x.d, x.m.Id) == misionAbierta);
            if (elegida.m == null) elegida = todas.FirstOrDefault(x => x.m.Estado != EstadoMision.MisionReclamada);
            if (elegida.m == null && todas.Count > 0) elegida = todas[0];
            var abierta = elegida.m != null ? ClaveMision(elegida.d, elegida.m.Id) : null;
            if (abierta != null && s != null && s.Nuevas.Remove(abierta)) GuardarSeguimiento();

            var cuerpo = Dialogo.Caja("diario-cuerpo");
            var indice = new ScrollView(ScrollViewMode.Vertical) { horizontalScrollerVisibility = ScrollerVisibility.Hidden, name = "diario-indice" };
            indice.AddToClassList("diario-indice");
            foreach (var (d, nombre) in DuenosDeMision)
            {
                var (misiones, duenoId) = DelDueno(h, d);
                var vivas = misiones.Where(m => m.Estado == EstadoMision.MisionCumplida).Concat(misiones.Where(m => m.Estado == EstadoMision.MisionActiva)).ToList();
                var hechas = misiones.Where(m => m.Estado == EstadoMision.MisionReclamada).ToList();
                var grupo = Dialogo.Caja("diario-grupo");
                var plegado = gruposPlegados.Contains(d);
                var nuevas = s?.Nuevas.Count(k => k.StartsWith(d + ":") && k != abierta) ?? 0;
                var cabGrupo = new Button(() => { if (!gruposPlegados.Remove(d)) gruposPlegados.Add(d); PintarMisiones(); }) { name = "diario-grupo-" + d };
                cabGrupo.AddToClassList("diario-grupo-cab");
                cabGrupo.Add(Dialogo.Texto(plegado ? "▸" : "▾", "diario-grupo-flecha"));
                cabGrupo.Add(Dialogo.Texto(nombre, "diario-grupo-nombre"));
                if (nuevas > 0) cabGrupo.Add(Dialogo.Texto(nuevas.ToString(), "diario-nueva"));
                cabGrupo.Add(Dialogo.Texto(vivas.Count.ToString(), "diario-grupo-cuenta"));
                grupo.Add(cabGrupo);
                if (!plegado)
                {
                    foreach (var m in vivas) grupo.Add(ItemDelDiario(h, d, m, abierta, s));
                    var vacio = duenoId == null ? (d == "plaza" ? "Solo dentro de una plaza." : "Sin Facción.") : vivas.Count == 0 ? "Ninguna en marcha." : null;
                    if (vacio != null) grupo.Add(Dialogo.Texto(vacio, "diario-vacio"));
                    if (hechas.Count > 0)
                    {
                        var plegable = new Foldout { text = $"Hechas ({hechas.Count})", value = hechas.Any(m => ClaveMision(d, m.Id) == abierta), name = "diario-hechas-" + d };
                        plegable.AddToClassList("diario-hechas");
                        foreach (var m in hechas) plegable.Add(ItemDelDiario(h, d, m, abierta, s));
                        grupo.Add(plegable);
                    }
                }
                indice.Add(grupo);
            }
            cuerpo.Add(indice);
            indice.RegisterCallback<GeometryChangedEvent>(_ => indice.scrollOffset = scroll);

            // La misión abierta: título y estado, de quién, texto, objetivos, detalles y recompensas; los botones, abajo.
            var detalle = Dialogo.Caja("diario-detalle");
            detalle.name = "diario-detalle";
            var pagina = new ScrollView(ScrollViewMode.Vertical) { horizontalScrollerVisibility = ScrollerVisibility.Hidden };
            pagina.AddToClassList("diario-pagina");
            var pie = Dialogo.Caja("diario-pie");
            if (elegida.m == null) pagina.Add(Dialogo.Texto("No tienes misiones todavía. Se activan solas a medida que juegas.", "diario-vacio"));
            else
            {
                var (d, m) = elegida;
                var t = TextoDeMision(m.Id);
                var otro = QuienReclama(h, d);
                var titulo = Dialogo.Caja("diario-titulo");
                titulo.Add(Dialogo.Texto(t.titulo, "diario-titulo-texto"));
                titulo.Add(Dialogo.Texto(m.Estado == EstadoMision.MisionCumplida ? "¡Cumplida!" : m.Estado == EstadoMision.MisionReclamada ? "Hecha" : "En curso",
                    "diario-estado diario-estado--" + (m.Estado == EstadoMision.MisionCumplida ? "cumplida" : m.Estado == EstadoMision.MisionReclamada ? "reclamada" : "activa")));
                pagina.Add(titulo);
                pagina.Add(Dialogo.Texto(DuenosDeMision.First(x => x.dueno == d).nombre, "diario-de"));
                if (t.texto != "") pagina.Add(Dialogo.Texto(t.texto, "diario-texto"));
                if (m.Estado == EstadoMision.MisionActiva && m.Condiciones.Count > 0) { pagina.Add(Dialogo.Texto("OBJETIVOS", "diario-seccion")); pagina.Add(BarrasDeMision(m.Condiciones)); }
                if (!string.IsNullOrEmpty(t.explicacion)) { pagina.Add(Dialogo.Texto("DETALLES", "diario-seccion")); pagina.Add(Dialogo.Texto(t.explicacion, "diario-texto")); }
                pagina.Add(Dialogo.Texto("RECOMPENSAS", "diario-seccion"));
                pagina.Add(RecompensasDe(d, m));
                var k = ClaveMision(d, m.Id);
                // Solo lo que se puede hacer (regla de Víctor): «Seguir» con hueco en el rastreador, «Dejar de seguir» si la sigues.
                var seguidaAqui = s?.Seguidas.Contains(k) == true;
                if (m.Estado != EstadoMision.MisionReclamada && (seguidaAqui || (s?.Seguidas.Count ?? 0) < MaxSeguidas))
                {
                    var seguir = Dialogo.Boton(seguidaAqui ? "Dejar de seguir" : "Seguir", "boton-secundario", () => { AlternarSeguir(h, k); PintarMisiones(); });
                    seguir.name = "mision-seguir";
                    pie.Add(seguir);
                }
                pie.Add(Dialogo.Caja("recl-hueco"));
                var (_, duenoId) = DelDueno(h, d);
                if (m.Estado == EstadoMision.MisionCumplida && otro == null && duenoId != null)
                {
                    Button reclamar = null;
                    reclamar = Dialogo.Boton("", "boton-primario diario-reclamar", async () =>
                        await OrdenDelPanel(new Orden { ReclamarRecompensa = new OrdenReclamarRecompensa { DuenoId = duenoId, MisionId = m.Id } }, Error, PintarMisiones, reclamar));
                    reclamar.Add(IconosDelMundo.Icono("glifo", "abrir", 16));
                    reclamar.Add(Dialogo.Texto("Reclamar", "diario-reclamar-texto"));
                    reclamar.name = "mision-reclamar";
                    pie.Add(reclamar);
                }
            }
            detalle.Add(pagina);
            detalle.Add(error);
            detalle.Add(pie);
            cuerpo.Add(detalle);
            placa.Add(cuerpo);
        }

        VisualElement ItemDelDiario(Heroe h, string d, Mision m, string abierta, Seguimiento s)
        {
            var k = ClaveMision(d, m.Id);
            var b = new Button(() => { misionAbierta = k; PintarMisiones(); }) { name = "diario-item-" + d + "-" + m.Id };
            b.AddToClassList("diario-item");
            b.AddToClassList("diario-item--" + (m.Estado == EstadoMision.MisionCumplida ? "cumplida" : m.Estado == EstadoMision.MisionReclamada ? "reclamada" : "activa"));
            b.EnableInClassList("activo", k == abierta);
            b.EnableInClassList("diario-item-nueva", s?.Nuevas.Contains(k) == true && k != abierta);
            b.Add(Dialogo.Texto(TextoDeMision(m.Id).titulo, "diario-item-texto"));
            if (s?.Seguidas.Contains(k) == true) { var marca = Dialogo.Texto("◆", "diario-seguida"); marca.tooltip = "La sigues"; b.Add(marca); }
            if (s?.Nuevas.Contains(k) == true) b.Add(Dialogo.Texto("nueva", "diario-nueva"));
            if (m.Estado == EstadoMision.MisionCumplida) b.Add(Dialogo.Texto("✓", "diario-lista"));
            return b;
        }

        // ─────────────── el rastreador ───────────────

        string firmaRastreador;
        float proximoRastreador;

        /// <summary>Las seguidas que se ven ahora, con su barra fina; las cumplidas, en dorado (`pintarRastreador`). Arriba a la derecha en todas
        /// partes (Víctor, 6): en la plaza, al principio de su columna derecha, encima de las tarjetas del ejército. Plegado, una línea.</summary>
        void PintarRastreador()
        {
            if (Time.unscaledTime < proximoRastreador) return;
            proximoRastreador = Time.unscaledTime + 0.5f;
            var montada = pantalla != null && pantalla.childCount > 0 ? pantalla[0] : null;
            var h = HeroeAhora();
            if (montada == null || h == null || montada.Q(className: "barra-arriba") == null) return;
            ActualizarSeguimiento(h);
            PintarInsigniaDeMisiones();
            var s = SeguimientoDe(h);
            var seguidas = (s?.Seguidas ?? new List<string>()).Select(k =>
            {
                var partes = k.Split(new[] { ':' }, 2);
                var m = DelDueno(h, partes[0]).misiones.FirstOrDefault(x => x.Id == partes[1]);
                return (d: partes[0], m);
            }).Where(x => x.m != null && x.m.Estado != EstadoMision.MisionReclamada).ToList();
            var sitio = montada.Q("plaza-der") ?? montada;
            var firma = $"{sitio.name}|{s?.Plegado}|" + string.Join(";", seguidas.Select(x => $"{x.d}:{x.m.Id}:{x.m.Estado}:{string.Join(",", x.m.Condiciones.Select(cd => cd.Avance))}"));
            var el = montada.Q("rastreador");
            if (seguidas.Count == 0) { el?.RemoveFromHierarchy(); firmaRastreador = null; return; }
            if (el != null && el.parent == sitio && firma == firmaRastreador) return;
            firmaRastreador = firma;
            el?.RemoveFromHierarchy();
            el = Dialogo.Caja("rastreador" + (sitio == montada ? " rastreador--flota" : ""));
            el.name = "rastreador";
            if (Resources.Load<StyleSheet>("MundoUI/Plaza") is StyleSheet hoja) el.styleSheets.Add(hoja);
            var cab = new Button(() => { if (s == null) return; s.Plegado = !s.Plegado; GuardarSeguimiento(); proximoRastreador = 0; }) { name = "rastreador-plegar" };
            cab.AddToClassList("rastreador-cab");
            cab.Add(Dialogo.Texto($"Misiones {seguidas.Count}/{MaxSeguidas}", "rastreador-cab-texto"));
            cab.Add(Dialogo.Texto(s?.Plegado == true ? "▸" : "▾", "rastreador-cab-flecha"));
            el.Add(cab);
            if (s?.Plegado != true)
                foreach (var (d, m) in seguidas)
                {
                    var b = new Button(() => AbrirMisiones(d, m.Id)) { name = "rastreador-" + d + "-" + m.Id };
                    b.AddToClassList("rastreador-item");
                    b.EnableInClassList("cumplida", m.Estado == EstadoMision.MisionCumplida);
                    b.Add(Dialogo.Texto(TextoDeMision(m.Id).titulo, "rastreador-item-titulo"));
                    if (m.Estado == EstadoMision.MisionCumplida) b.Add(Dialogo.Texto("¡Cumplida! Reclámala", "rastreador-item-nota"));
                    else b.Add(BarrasDeMision(m.Condiciones, true));
                    el.Add(b);
                }
            sitio.Insert(0, el);
        }

        /// <summary>La insignia del botón Misiones, aunque no haya rastreador.</summary>
        void PintarInsigniaDeMisiones()
        {
            var montada = pantalla != null && pantalla.childCount > 0 ? pantalla[0] : null;
            if (montada?.Q<Label>("misiones-cuenta") is Label cuenta && HeroeAhora() is Heroe h) { var n = MisionesPorReclamar(h); cuenta.text = n.ToString(); Ver(cuenta, n > 0); }
        }

        // ─────────────── Facción › Misiones ───────────────

        /// <summary>La lista de las misiones de la Facción (`htmlListaMisiones`): primero las cumplidas por reclamar, luego las activas y, plegadas, las
        /// hechas; cada una con su sello, sus objetivos, su recompensa, «Seguir» y «Reclamar».</summary>
        void PestanaMisionesDeFaccion(VisualElement v, Heroe h, Faccion f)
        {
            if (Resources.Load<StyleSheet>("MundoUI/Plaza") is StyleSheet hoja && !v.styleSheets.Contains(hoja)) v.styleSheets.Add(hoja);
            ActualizarSeguimiento(h);
            var s = SeguimientoDe(h);
            var error = Dialogo.Texto("", "error oculto");
            error.name = "misiones-error";
            void Error(string t) => Poner(error, t);
            void Repintar() { firmaFaccion = null; PintarFaccion(); }
            var cumplidas = f.Misiones.Where(m => m.Estado == EstadoMision.MisionCumplida).ToList();
            var vivas = cumplidas.Concat(f.Misiones.Where(m => m.Estado == EstadoMision.MisionActiva)).ToList();
            var hechas = f.Misiones.Where(m => m.Estado == EstadoMision.MisionReclamada).ToList();
            var cab = Dialogo.Caja("misiones-cab");
            cab.Add(Dialogo.Texto("Misiones de la Facción", "pacto-plaza merc-titulo"));
            var enMarcha = IconosDelMundo.ConIcono("glifo", "aeda", Dialogo.Texto((vivas.Count - cumplidas.Count).ToString(), "plaza-obra-chip-texto"), 14);
            enMarcha.AddToClassList("plaza-obra-chip");
            enMarcha.tooltip = "En marcha";
            cab.Add(enMarcha);
            if (cumplidas.Count > 0)
            {
                var por = IconosDelMundo.ConIcono("glifo", "prestigio", Dialogo.Texto(cumplidas.Count.ToString(), "plaza-obra-chip-texto"), 14);
                por.AddToClassList("plaza-obra-chip");
                por.AddToClassList("activa");
                por.tooltip = "Cumplidas, por reclamar";
                cab.Add(por);
            }
            v.Add(ConAyuda(cab, $"Se activan solas: cuando se cumple una, aparecen las que venían detrás. Sigue hasta {MaxSeguidas} para verlas en pantalla. La recompensa no llega sola: el Rey pulsa Reclamar cuando esté cumplida."));
            v.Add(error);
            if (vivas.Count == 0) v.Add(Dialogo.Texto("Ninguna en marcha ahora.", "campo-ayuda"));
            var cartas = Dialogo.Caja("misiones-cartas");
            foreach (var m in vivas)
            {
                var k = ClaveMision("faccion", m.Id);
                var t = TextoDeMision(m.Id);
                var carta = Dialogo.Caja("mision mision--" + (m.Estado == EstadoMision.MisionCumplida ? "cumplida" : "activa"));
                carta.name = "mision-" + m.Id;
                var mc = Dialogo.Caja("mision-cab");
                mc.Add(IconosDelMundo.Icono("glifo", m.Estado == EstadoMision.MisionCumplida ? "prestigio" : "aeda", 18));
                mc.Add(Dialogo.Texto(t.titulo, "mision-titulo"));
                if (s?.Nuevas.Contains(k) == true) mc.Add(Dialogo.Texto("nueva", "diario-nueva"));
                var seguida = s?.Seguidas.Contains(k) == true;
                if (seguida || (s?.Seguidas.Count ?? 0) < MaxSeguidas)
                {
                    var seguir = Dialogo.Boton(seguida ? "◆ Siguiendo" : "◇ Seguir", "mision-seguir" + (seguida ? " activa" : ""), () => { AlternarSeguir(h, k); Repintar(); });
                    seguir.name = "mision-seguir-" + m.Id;
                    mc.Add(seguir);
                }
                carta.Add(mc);
                if (t.texto != "") carta.Add(Dialogo.Texto(t.texto, "mision-texto"));
                if (m.Estado == EstadoMision.MisionActiva) carta.Add(BarrasDeMision(m.Condiciones));
                var mp = Dialogo.Caja("mision-pie");
                mp.Add(RecompensasDe("faccion", m));
                if (m.Estado == EstadoMision.MisionCumplida)
                {
                    if (QuienReclama(h, "faccion") == null)
                    {
                        Button reclamar = null;
                        reclamar = Dialogo.Boton("", "boton-primario diario-reclamar", async () =>
                            await OrdenDelPanel(new Orden { ReclamarRecompensa = new OrdenReclamarRecompensa { DuenoId = f.Id, MisionId = m.Id } }, Error, Repintar, reclamar));
                        reclamar.Add(IconosDelMundo.Icono("glifo", "abrir", 16));
                        reclamar.Add(Dialogo.Texto("Reclamar", "diario-reclamar-texto"));
                        reclamar.name = "mision-reclamar-" + m.Id;
                        mp.Add(reclamar);
                    }
                }
                carta.Add(mp);
                cartas.Add(carta);
            }
            v.Add(cartas);
            if (hechas.Count > 0)
            {
                var plegable = new Foldout { text = $"Hechas: {hechas.Count}", value = false, name = "misiones-hechas" };
                plegable.AddToClassList("diario-hechas");
                foreach (var m in hechas) plegable.Add(Dialogo.Texto("✓ " + TextoDeMision(m.Id).titulo, "mision-hecha"));
                v.Add(plegable);
            }
            if (s?.Nuevas.RemoveAll(k => k.StartsWith("faccion:")) > 0) GuardarSeguimiento();
        }

        // ─────────────── el tablón de recurrentes ───────────────

        /// <summary>Lo que el Gobernador o el Rey van marcando para la semana que viene, por tablón, hasta que lo guardan.</summary>
        readonly Dictionary<string, (HashSet<string> diaria, HashSet<string> semanal)> borradoresTablon = new Dictionary<string, (HashSet<string>, HashSet<string>)>();

        HashSet<string> MisCargosDeTablon(Heroe h, string tipo, string id)
        {
            var r = new HashSet<string>();
            var f = MiFaccion(h);
            if (f != null && f.HasReyId && f.ReyId == h.Id) r.Add("rey");
            if (f != null && f.HasEmbajadorId && f.EmbajadorId == h.Id) r.Add("embajador");
            if (tipo == "plaza" && c.Sesion.Almacen.Buscar(TipoEntidad.Asentamiento, id)?.Asentamiento is Asentamiento a)
                foreach (var cargo in new[] { "gobernador", "tesorero", "general", "maestroObras", "sacerdote", "maestroCaravanas" })
                    if (TitularDe(a, cargo) == h.Id) r.Add(cargo);
            return r;
        }

        /// <summary>¿Eres quien elige (Gobernador de la plaza, Rey de la Facción)?</summary>
        bool EligesElTablon(Heroe h, string tipo, string id) =>
            tipo == "faccion" ? MiFaccion(h) is Faccion f && f.HasReyId && f.ReyId == h.Id
                : c.Sesion.Almacen.Buscar(TipoEntidad.Asentamiento, id)?.Asentamiento is Asentamiento a && TitularDe(a, "gobernador") == h.Id;

        /// <summary>¿Reclamas tú las grupales? El Rey en la Facción; en la plaza, el Gobernador o, sin él, un residente.</summary>
        bool ReclamasGrupal(Heroe h, string tipo, string id)
        {
            if (tipo == "faccion") return EligesElTablon(h, tipo, id);
            var a = c.Sesion.Almacen.Buscar(TipoEntidad.Asentamiento, id)?.Asentamiento;
            if (a == null) return false;
            var g = TitularDe(a, "gobernador");
            return g != null ? g == h.Id : Reside(a, h);
        }

        VisualElement RecompensasDeRecurrente(DefinicionMisionRecurrente x, string tipo) =>
            Recompensas(x.Participacion, x.Experiencia, x.Recompensas, x.Tipo == TipoMision.MisionIndividual ? "personal" : tipo, x.Tipo == TipoMision.MisionGrupal,
                x.Tipo == TipoMision.MisionIndividual && x.Duracion == DuracionMision.MisionDiaria);

        static string TextoDeEstado(EstadoMisionRecurrente e) => e switch
        {
            EstadoMisionRecurrente.RecurrenteEnCurso => "en curso", EstadoMisionRecurrente.RecurrenteCumplida => "¡Cumplida!",
            EstadoMisionRecurrente.RecurrenteReclamada => "reclamada", EstadoMisionRecurrente.RecurrenteCaducada => "caducada", _ => "",
        };

        /// <summary>El tablón entero (`htmlTablon`): las dos columnas, Diarias y Semanales, y, para quien elige, la elección de la semana que viene.</summary>
        void TablonDeMisiones(VisualElement v, Heroe h, string tipo, string duenoId, Tablon t, Action<string> error, Action repintar)
        {
            if (Resources.Load<StyleSheet>("MundoUI/Plaza") is StyleSheet hoja && !v.styleSheets.Contains(hoja)) v.styleSheets.Add(hoja);
            v.Add(ConAyuda(Dialogo.Texto("Misiones que vuelven cada día y cada semana.", "campo-ayuda"), tipo == "plaza"
                ? "Las individuales las acepta cada residente y cuentan desde que la aceptas; las grupales cuentan lo de todos los residentes. Dan participación en la plaza y, algunas, experiencia de Facción, recursos, oro o tropa: en una individual, a tu almacén personal (la tropa nace en tu campamento); en una grupal, al almacén de la plaza. Pasa el ratón por cada premio para ver adónde va. Lo cumplido se reclama aunque haya pasado el día."
                : "Las individuales las acepta cada ciudadano y cuentan desde que la aceptas; las grupales cuentan lo de toda la Facción. Lo cumplido se reclama aunque haya pasado el periodo; las grupales las reclama el Rey."));
            var columnas = Dialogo.Caja("tablon-columnas");
            foreach (var duracion in new[] { DuracionMision.MisionDiaria, DuracionMision.MisionSemanal })
                columnas.Add(ColumnaDelTablon(h, tipo, duenoId, t, duracion, error, repintar));
            v.Add(columnas);
            if (EligesElTablon(h, tipo, duenoId) && t.Catalogo.Count > 0) v.Add(ElegirTablon(h, duenoId, t, error, repintar));
        }

        VisualElement ColumnaDelTablon(Heroe h, string tipo, string duenoId, Tablon t, DuracionMision duracion, Action<string> error, Action repintar)
        {
            var diaria = duracion == DuracionMision.MisionDiaria;
            var misiones = t.Misiones.Where(m => m.Definicion.Duracion == duracion).OrderBy(m => m.HastaMs <= AhoraMs ? 1 : 0).ToList();
            var renueva = diaria ? t.RenovacionDiariaMs : t.RenovacionSemanalMs;
            var periodo = (diaria ? 1 : 7) * 86_400_000.0;
            EstadoMisionRecurrente? EstadoDe(MisionRecurrente m) => m.Definicion.Tipo == TipoMision.MisionGrupal ? m.Estado : m.Aceptaciones.FirstOrDefault(a => a.HeroeId == h.Id)?.Estado;
            var porReclamar = misiones.Count(m => EstadoDe(m) == EstadoMisionRecurrente.RecurrenteCumplida);
            var col = Dialogo.Caja("tablon-columna");
            col.name = "tablon-" + (diaria ? "diarias" : "semanales");
            var cab = Dialogo.Caja("tablon-cab");
            cab.Add(IconosDelMundo.Icono("glifo", diaria ? "diaria" : "semanal", 18));
            cab.Add(Dialogo.Texto(diaria ? "Diarias" : "Semanales", "tablon-cab-titulo"));
            if (porReclamar > 0)
            {
                var chip = IconosDelMundo.ConIcono("glifo", "prestigio", Dialogo.Texto(porReclamar.ToString(), "plaza-obra-chip-texto"), 14);
                chip.AddToClassList("plaza-obra-chip");
                chip.AddToClassList("activa");
                chip.tooltip = "Cumplidas, por reclamar";
                cab.Add(chip);
            }
            cab.Add(Dialogo.Caja("recl-hueco"));
            var renovacion = IconosDelMundo.ConIcono("glifo", "espera", Dialogo.Texto(EnTiempoReal(renueva - AhoraMs), "tablon-renueva"), 13);
            renovacion.tooltip = "Se renuevan";
            cab.Add(renovacion);
            col.Add(cab);
            var barra = Dialogo.Caja("merc-barra");
            var i = Dialogo.Caja("merc-barra-i");
            i.style.width = Length.Percent((float)(100 * Math.Max(0, Math.Min(1, 1 - (renueva - AhoraMs) / periodo))));
            barra.Add(i);
            col.Add(barra);
            if (misiones.Count == 0) col.Add(Dialogo.Texto("Ninguna ahora.", "campo-ayuda"));
            foreach (var m in misiones) col.Add(TarjetaDelTablon(h, tipo, duenoId, m, EstadoDe(m), error, repintar));
            return col;
        }

        /// <summary>Una misión del tablón (`tarjeta`): sello, título y estado; texto; etiquetas (grupal o individual con su tope, el cargo, el periodo
        /// acabado); avance; «Más» (la explicación y quién aporta); la recompensa y «Aceptar» o «Reclamar».</summary>
        VisualElement TarjetaDelTablon(Heroe h, string tipo, string duenoId, MisionRecurrente m, EstadoMisionRecurrente? estado, Action<string> error, Action repintar)
        {
            var x = m.Definicion;
            var grupal = x.Tipo == TipoMision.MisionGrupal;
            var vencida = m.HastaMs <= AhoraMs;
            var mia = m.Aceptaciones.FirstOrDefault(a => a.HeroeId == h.Id);
            var carta = Dialogo.Caja("mision tablon-tarjeta" + (estado == EstadoMisionRecurrente.RecurrenteCumplida ? " mision--cumplida"
                : estado == EstadoMisionRecurrente.RecurrenteReclamada || estado == EstadoMisionRecurrente.RecurrenteCaducada ? " mision--reclamada"
                : estado == EstadoMisionRecurrente.RecurrenteEnCurso ? " mision--activa" : ""));
            carta.name = "tablon-mision-" + x.Id;
            var cab = Dialogo.Caja("mision-cab");
            var sello = IconosDelMundo.Icono("glifo", estado == EstadoMisionRecurrente.RecurrenteCumplida ? "prestigio" : grupal ? "gente" : "individual", 18);
            if (sello != null) { sello.tooltip = grupal ? "Grupal" : "Individual"; cab.Add(sello); }
            cab.Add(Dialogo.Texto(string.IsNullOrEmpty(x.Titulo) ? x.Id : x.Titulo, "mision-titulo"));
            if (estado != null) cab.Add(Dialogo.Texto(TextoDeEstado(estado.Value), "tablon-estado tablon-estado--" + estado.Value.ToString().ToLowerInvariant()));
            carta.Add(cab);
            if (!string.IsNullOrEmpty(x.Texto)) carta.Add(Dialogo.Texto(x.Texto, "mision-texto"));
            var etiquetas = Dialogo.Caja("tablon-etiquetas");
            etiquetas.Add(Dialogo.Texto(grupal ? "Grupal" : $"Individual · {m.AceptadasSemana}/{x.LimiteSemanal} esta semana", "tablon-chip"));
            if (x.HasCargo) etiquetas.Add(Dialogo.Texto("Solo " + (CargoDeMision.TryGetValue(x.Cargo, out var cn) ? cn : x.Cargo), "tablon-chip"));
            if (vencida) etiquetas.Add(Dialogo.Texto("Periodo acabado", "tablon-chip"));
            carta.Add(etiquetas);
            if (estado == EstadoMisionRecurrente.RecurrenteEnCurso) carta.Add(BarrasDeMision(grupal ? m.Condiciones : mia?.Condiciones ?? Enumerable.Empty<CondicionMision>()));
            var aportes = m.Aportes.Where(kv => kv.Value > 0).OrderByDescending(kv => kv.Value).ToList();
            if (!string.IsNullOrEmpty(x.Explicacion) || (grupal && aportes.Count > 0))
            {
                var mas = new Foldout { text = "Más", value = false, name = "tablon-mas-" + x.Id };
                mas.AddToClassList("mision-mas");
                if (!string.IsNullOrEmpty(x.Explicacion)) mas.Add(Dialogo.Texto(x.Explicacion, "mision-texto"));
                if (grupal && aportes.Count > 0) mas.Add(Dialogo.Texto("Aportan: " + string.Join(", ", aportes.Select(kv => $"{Textos.NombreDeHeroe(c.Sesion.Almacen, kv.Key)} ({kv.Value})")), "mision-texto"));
                carta.Add(mas);
            }
            var pie = Dialogo.Caja("mision-pie");
            pie.Add(RecompensasDeRecurrente(x, tipo));
            if (estado == EstadoMisionRecurrente.RecurrenteCumplida)
            {
                if (!grupal || ReclamasGrupal(h, tipo, duenoId))
                {
                    Button reclamar = null;
                    reclamar = Dialogo.Boton("", "boton-primario diario-reclamar", async () =>
                        await OrdenDelPanel(new Orden { ReclamarRecompensa = new OrdenReclamarRecompensa { DuenoId = duenoId, MisionId = x.Id } }, error, repintar, reclamar));
                    reclamar.Add(IconosDelMundo.Icono("glifo", "abrir", 16));
                    reclamar.Add(Dialogo.Texto("Reclamar", "diario-reclamar-texto"));
                    reclamar.name = "tablon-reclamar-" + x.Id;
                    pie.Add(reclamar);
                }
            }
            // «Aceptar» solo si se puede: individual, sin aceptar, en su periodo, con hueco esta semana y con el cargo que pida (regla de Víctor).
            else if (!grupal && mia == null && !vencida && !(x.LimiteSemanal > 0 && m.AceptadasSemana >= x.LimiteSemanal)
                && !(x.HasCargo && !MisCargosDeTablon(h, tipo, duenoId).Contains(x.Cargo)))
            {
                Button aceptar = null;
                aceptar = Dialogo.Boton("", "boton-secundario tablon-aceptar", async () =>
                    await OrdenDelPanel(new Orden { AceptarMision = new OrdenAceptarMision { DuenoId = duenoId, MisionId = x.Id } }, error, repintar, aceptar));
                aceptar.Add(IconosDelMundo.Icono("glifo", "palabra", 16));
                aceptar.Add(Dialogo.Texto("Aceptar", "tablon-aceptar-texto"));
                aceptar.name = "tablon-aceptar-" + x.Id;
                pie.Add(aceptar);
            }
            carta.Add(pie);
            return carta;
        }

        /// <summary>«Elegir las de la semana que viene» (`elegir`): las del catálogo de cada duración, hasta 7, y «Guardar la elección».</summary>
        VisualElement ElegirTablon(Heroe h, string duenoId, Tablon t, Action<string> error, Action repintar)
        {
            if (!borradoresTablon.TryGetValue(duenoId, out var b))
                b = (new HashSet<string>(t.ConEleccion ? t.ElegidasDiariasIds : t.DiariasIds), new HashSet<string>(t.ConEleccion ? t.ElegidasSemanalesIds : t.SemanalesIds));
            var plegable = new Foldout { text = "Elegir las de la semana que viene", value = borradoresTablon.ContainsKey(duenoId), name = "tablon-elegir" };
            plegable.AddToClassList("tablon-elegir");
            // 📌 delante del título, como el prototipo.
            if (plegable.Q(className: "unity-foldout__input") is VisualElement entradaPlegable) entradaPlegable.Insert(1, IconosDelMundo.Icono("glifo", "chincheta", 16));
            plegable.Add(Dialogo.Texto($"Lo que elijas sale desde la próxima renovación semanal (en {EnTiempoReal(t.RenovacionSemanalMs - AhoraMs)}); hasta {MaxElegidas} de cada. {(t.ConEleccion ? "" : "Sin elección, salen las primeras del catálogo.")}", "campo-ayuda"));
            foreach (var (duracion, marcadas, nombre) in new[] { (DuracionMision.MisionDiaria, b.Item1, "Diarias"), (DuracionMision.MisionSemanal, b.Item2, "Semanales") })
            {
                var grupo = Dialogo.Caja("tablon-elegir-grupo");
                var cab = Dialogo.Caja("tablon-elegir-cab");
                cab.Add(IconosDelMundo.Icono("glifo", duracion == DuracionMision.MisionDiaria ? "diaria" : "semanal", 16));
                cab.Add(Dialogo.Texto(nombre, "tablon-cab-titulo"));
                cab.Add(Dialogo.Texto($"{marcadas.Count}/{MaxElegidas}", "tablon-cuenta" + (marcadas.Count >= MaxElegidas ? " llena" : "")));
                grupo.Add(cab);
                var opciones = Dialogo.Caja("tablon-opciones");
                foreach (var x in t.Catalogo.Where(d => d.Duracion == duracion))
                {
                    var on = marcadas.Contains(x.Id);
                    var id = x.Id;
                    var marca = new Toggle { value = on, name = "tablon-elegir-" + x.Id, tooltip = x.Texto };
                    marca.AddToClassList("tablon-opcion");
                    marca.EnableInClassList("elegida", on);
                    marca.SetEnabled(on || marcadas.Count < MaxElegidas);
                    marca.RegisterValueChangedCallback(e =>
                    {
                        if (e.newValue) marcadas.Add(id); else marcadas.Remove(id);
                        borradoresTablon[duenoId] = b;
                        repintar();
                    });
                    var texto = Dialogo.Caja("tablon-opcion-cuerpo");
                    var tituloOpcion = IconosDelMundo.ConIcono("glifo", x.Tipo == TipoMision.MisionGrupal ? "gente" : "individual", Dialogo.Texto(string.IsNullOrEmpty(x.Titulo) ? x.Id : x.Titulo, "tablon-opcion-titulo"), 14);
                    texto.Add(tituloOpcion);
                    texto.Add(Dialogo.Texto((x.HasCargo ? (CargoDeMision.TryGetValue(x.Cargo, out var cn) ? cn : x.Cargo) + " · " : "") + x.Texto, "tablon-opcion-sub"));
                    marca.Add(texto);
                    opciones.Add(marca);
                }
                if (opciones.childCount == 0) opciones.Add(Dialogo.Texto("El catálogo no tiene ninguna.", "campo-ayuda"));
                grupo.Add(opciones);
                plegable.Add(grupo);
            }
            Button guardar = null;
            guardar = Dialogo.Boton("", "boton-primario tablon-guardar diario-reclamar", async () =>
            {
                var orden = new OrdenElegirTablon { DuenoId = duenoId };
                orden.DiariasIds.AddRange(b.Item1);
                orden.SemanalesIds.AddRange(b.Item2);
                var r = await OrdenDelPanel(new Orden { ElegirTablon = orden }, error, repintar, guardar);
                if (r?.Resultado == Resultado.OrdenAceptada) { borradoresTablon.Remove(duenoId); repintar(); }
            });
            guardar.name = "tablon-guardar";
            guardar.Add(IconosDelMundo.Icono("glifo", "chincheta", 16));
            guardar.Add(Dialogo.Texto("Guardar la elección", "diario-reclamar-texto"));
            plegable.Add(guardar);
            return plegable;
        }
    }
}
