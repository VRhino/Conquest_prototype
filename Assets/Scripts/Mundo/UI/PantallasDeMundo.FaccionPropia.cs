// Tu Facción (paso 8 del bloque 4, tanda 2), calcada de BronzeAgeClient: la ventana a pantalla completa con Facción (`main.ts:1835-1900`), el
// panel fijo de la izquierda (`htmlLateralFaccion`: el sigilo con su nivel, la experiencia, la franja, el prestigio, el Rey y el Embajador, la Liga,
// los cupos, los títulos y «Dejar la Facción») y las pestañas Miembros (solicitudes, cargos y la lista, `renderPestanaFaccion`, `panelCargos.ts`,
// `participacion.ts`), Plazas (`htmlPlazasFaccion`), Tecnología (tanda 3, `PantallasDeMundo.Tecnologia`) y Ajustes del Rey (`panelAdmision.ts`). Diplomacia, con su tanda; Ejércitos,
// Tablón, Misiones y Mercenarios, con sus pasos: sus pestañas no salen hasta entonces.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BronzeAge.Contract.V1;
using ConquestTactics.Mundo.Mapa;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace ConquestTactics.Mundo.UI
{
    public partial class PantallasDeMundo
    {
        /// <summary>Las pestañas de tu Facción que ya hay (las de `PESTANAS_FACCION`, en su orden).</summary>
        static readonly (string id, string nombre)[] PestanasDeFaccion = { ("miembros", "Miembros"), ("plazas", "Plazas"), ("tecnologia", "Tecnología"), ("tablon", "Tablón"), ("misiones", "Misiones"), ("mercenarios", "Mercenarios"), ("diplomacia", "Diplomacia"), ("ajustes", "Ajustes del Rey") };
        string pestanaFaccion = "miembros";
        /// <summary>Las plazas desplegadas en la pestaña Plazas (todas plegadas al abrir).</summary>
        readonly HashSet<string> plazasAbiertas = new HashSet<string>();

        /// <summary>Un bloque de parámetros de `/v1/datos` por su nombre, esté en el documento que esté (`bloqueDeDatos`); null si no llega.</summary>
        JToken BloqueDeDatos(string nombre) =>
            (c.Datos?.Raiz?["documentos"] as JObject)?.Properties().Select(p => (p.Value as JObject)?[nombre]).FirstOrDefault(x => x != null);

        /// <summary>Los iconos del lateral, los emoji del prototipo de Twemoji: 👥 👑 📜 🏆 ⚖️ 🤝 🗿 🏛.</summary>
        static VisualElement GlifoDeFaccion(string id, float lado = 16) => id switch
        {
            "miembros" => IconosDelMundo.Icono("glifo", "gente", lado),
            "rey" => IconosDelMundo.Icono("glifo", "nobleza", lado),
            "embajador" => IconosDelMundo.Icono("glifo", "aeda", lado),
            "prestigio" => IconosDelMundo.Icono("glifo", "prestigio", lado),
            "reputacion" => IconosDelMundo.Icono("glifo", "reputacion", lado),
            "alianzas" => IconosDelMundo.Icono("glifo", "alianza", lado),
            "estatuas" => IconosDelMundo.Icono("glifo", "estatua", lado),
            "plazas" => IconosDelMundo.Icono("glifo", "templo", lado),
            _ => null,
        };

        static VisualElement ConGlifo(string glifo, VisualElement texto, string clase)
        {
            var fila = Dialogo.Caja(clase);
            if (GlifoDeFaccion(glifo) is VisualElement g) fila.Add(g);
            fila.Add(texto);
            return fila;
        }

        // ─────────────── la ventana ───────────────

        void PintarTuFaccion(Heroe h)
        {
            var a = c.Sesion.Almacen;
            var faccion = a.Buscar(TipoEntidad.Faccion, h.FaccionId)?.Faccion;
            faccionVentana.Clear();
            var cab = Dialogo.Caja("faccion-cabecera");
            cab.Add(Dialogo.Texto("FACCIÓN", "faccion-titulo"));
            var cerrar = Dialogo.Boton("Cerrar", "boton-secundario base-cerrar", CerrarFaccion);
            cerrar.name = "faccion-cerrar";
            cab.Add(cerrar);
            faccionVentana.Add(cab);
            if (faccion == null) { faccionVentana.Add(Dialogo.Texto("Cargando tu Facción…", "faccion-vacia")); return; }
            var marco = Dialogo.Caja("faccion-marco");
            marco.Add(LateralDeFaccion(h, faccion));
            var principal = Dialogo.Caja("faccion-principal");
            var nav = Dialogo.Caja("base-pestanas faccion-pestanas");
            nav.name = "faccion-pestanas";
            var esRey = faccion.HasReyId && faccion.ReyId == h.Id;
            foreach (var (id, nombre) in PestanasDeFaccion)
            {
                var b = new Button(() => { pestanaFaccion = id; firmaFaccion = null; PintarFaccion(); }) { name = "pestana-faccion-" + id };
                b.AddToClassList("base-pestana");
                b.EnableInClassList("activo", id == pestanaFaccion);
                b.Add(Dialogo.Texto(nombre, "base-pestana-texto"));
                // Las solicitudes pendientes, en Miembros (solo las ve el Rey).
                if (id == "miembros" && esRey && faccion.SolicitudesIds.Count > 0) b.Add(Dialogo.Texto(faccion.SolicitudesIds.Count.ToString(), "faccion-pestana-aviso"));
                // Lo que pide atención en Mercenarios: contratos a punto de vencer y valoraciones pendientes.
                if (id == "misiones" && faccion.Misiones.Count(m => m.Estado == EstadoMision.MisionCumplida) is int cumplidas && cumplidas > 0) b.Add(Dialogo.Texto(cumplidas.ToString(), "faccion-pestana-aviso"));
                if (id == "mercenarios" && MercenariosQuePidenAtencion(h, faccion) is int atencion && atencion > 0) b.Add(Dialogo.Texto(atencion.ToString(), "faccion-pestana-aviso"));
                nav.Add(b);
            }
            principal.Add(nav);
            // La Tecnología es un tablero que se arrastra y se desplaza solo: su cuerpo no lleva barra.
            var cuerpo = pestanaFaccion == "tecnologia" ? Dialogo.Caja("faccion-pestana-cuerpo tec-cuerpo")
                : pestanaFaccion == "diplomacia" ? Dialogo.Caja("faccion-pestana-cuerpo diplo-cuerpo") : new ScrollView(ScrollViewMode.Vertical);
            cuerpo.name = "faccion-pestana-cuerpo";
            cuerpo.AddToClassList("faccion-pestana-cuerpo");
            if (pestanaFaccion != "tecnologia") cerrarFichaDeTecnologia = null;
            if (pestanaFaccion == "plazas") PestanaPlazas(cuerpo, h, faccion);
            else if (pestanaFaccion == "tecnologia") PestanaTecnologia(cuerpo, h, faccion);
            else if (pestanaFaccion == "diplomacia") PestanaDiplomacia(cuerpo, h, faccion);
            else if (pestanaFaccion == "ajustes") PestanaAjustes(cuerpo, h, faccion);
            else if (pestanaFaccion == "mercenarios") PestanaMercenarios(cuerpo, h, faccion);
            else if (pestanaFaccion == "misiones") PestanaMisionesDeFaccion(cuerpo, h, faccion);
            else if (pestanaFaccion == "tablon")
            {
                var errorTablon = Dialogo.Texto("", "error oculto");
                errorTablon.name = "tablon-error";
                cuerpo.Add(errorTablon);
                if (faccion.Tablon != null) TablonDeMisiones(cuerpo, h, "faccion", faccion.Id, faccion.Tablon, t => Poner(errorTablon, t), () => { firmaFaccion = null; PintarFaccion(); });
                else cuerpo.Add(Dialogo.Texto("Tu Facción aún no tiene tablón de misiones.", "campo-ayuda"));
            }
            else PestanaMiembros(cuerpo, h, faccion);
            principal.Add(cuerpo);
            marco.Add(principal);
            faccionVentana.Add(marco);
            // La ficha de las tecnologías, encima de todo.
            faccionVentana.Q("tec-tip")?.BringToFront();
        }

        string FirmaDeTuFaccion(Heroe h)
        {
            var a = c.Sesion.Almacen;
            var f = a.Buscar(TipoEntidad.Faccion, h.FaccionId)?.Faccion;
            var plazas = string.Join(",", a.Todas(Entidad.CuerpoOneofCase.Asentamiento).Where(e => e.Asentamiento.FaccionId == h.FaccionId).Select(e => e.Asentamiento.Version));
            var titulos = string.Join(",", a.Todas(Entidad.CuerpoOneofCase.Titulo).Select(e => $"{e.Titulo.TituloId}:{e.Titulo.PoseedorId}"));
            var tecnologia = pestanaFaccion == "tecnologia" && f != null ? FirmaDeTecnologia(h, f) : pestanaFaccion == "diplomacia" ? FirmaDeDiplomacia() : "";
            if (f != null) tecnologia += $"|{MercenariosQuePidenAtencion(h, f)}" + (pestanaFaccion == "mercenarios" ? FirmaDeMercenarios(h, f) : "") + (pestanaFaccion == "misiones" || pestanaFaccion == "tablon" ? FirmaDeMisiones() : "");
            return $"propia|{f?.Version}|{pestanaFaccion}|{plazas}|{titulos}|{string.Join(",", plazasAbiertas)}|{tecnologia}";
        }

        // ─────────────── el lateral ───────────────

        VisualElement LateralDeFaccion(Heroe h, Faccion f)
        {
            var a = c.Sesion.Almacen;
            var lateral = new ScrollView(ScrollViewMode.Vertical) { name = "faccion-lateral" };
            lateral.AddToClassList("faccion-lateral");
            var granRey = f.Liga != null && f.Liga.GranReyId == f.Id;
            var escudo = Dialogo.Caja("faccion-lateral-escudo");
            var conNivel = Dialogo.Caja("faccion-escudo-con-nivel");
            conNivel.Add(SigiloUI(f.Sigilo, 112, granRey, "faccion-sigilo-grande"));
            var medallon = Medallon((int)f.Nivel, "", 34, 40);
            medallon.AddToClassList("faccion-nivel");
            medallon.tooltip = $"Facción de nivel {f.Nivel}";
            conNivel.Add(medallon);
            escudo.Add(conNivel);
            lateral.Add(escudo);
            lateral.Add(Dialogo.Texto(f.Nombre.ToUpperInvariant(), "faccion-lateral-nombre"));
            var kicker = Kicker("Tu facción" + (granRey ? " · Gran Rey" : ""));
            kicker.AddToClassList("faccion-lateral-kicker");
            lateral.Add(kicker);

            // La experiencia hacia el nivel siguiente (`NIVEL_FACCION.xpParaNivel`, umbrales acumulados).
            var reglas = BloqueDeDatos("NIVEL_FACCION");
            var umbrales = reglas?["xpParaNivel"]?.Values<double>().ToList();
            if (umbrales != null && umbrales.Count > 0)
            {
                var maximo = (int?)reglas["nivelMaximo"] ?? umbrales.Count + 1;
                var xp = f.Experiencia / 1000.0; // en milésimas, como en el prototipo (`mil(f.experiencia)`)
                var bloque = Dialogo.Caja("faccion-xp");
                var cabXp = Dialogo.Caja("faccion-xp-cab");
                cabXp.Add(Dialogo.Texto("Experiencia", "faccion-xp-etq"));
                var barra = Dialogo.Caja("faccion-xp-barra");
                var lleno = Dialogo.Caja("faccion-xp-lleno");
                barra.Add(lleno);
                if (f.Nivel >= maximo)
                {
                    cabXp.Add(Dialogo.Texto($"{Miles(xp)} · nivel máximo", "faccion-xp-cifra"));
                    lleno.style.width = Length.Percent(100);
                    bloque.Add(cabXp);
                    bloque.Add(barra);
                }
                else
                {
                    var desde = f.Nivel >= 2 ? umbrales.ElementAtOrDefault((int)f.Nivel - 2) : 0;
                    var hasta = umbrales.Count >= f.Nivel ? umbrales[(int)f.Nivel - 1] : desde;
                    var pct = hasta > desde ? Math.Max(0, Math.Min(100, (xp - desde) / (hasta - desde) * 100)) : 100;
                    cabXp.Add(Dialogo.Texto($"{Miles(xp)} / {Miles(hasta)}", "faccion-xp-cifra"));
                    lleno.style.width = Length.Percent((float)pct);
                    bloque.Add(cabXp);
                    bloque.Add(barra);
                    bloque.Add(Dialogo.Texto($"Faltan {Miles(hasta - xp)} para el nivel {f.Nivel + 1}", "faccion-xp-falta"));
                }
                bloque.tooltip = "";
                lateral.Add(bloque);
            }

            // La franja: miembros, prestigio y reputación.
            var franja = Dialogo.Caja("faccion-franja");
            VisualElement Dato(string glifo, string nombre, string valor, string ayuda = null)
            {
                var d = ConGlifo(glifo, Dialogo.Texto(valor, "faccion-franja-cifra"), "faccion-franja-dato");
                d.tooltip = nombre;
                d.name = "franja-" + glifo;
                return ayuda == null ? d : ConAyuda(d, ayuda);
            }
            var prestigio = PrestigioDe(f);
            franja.Add(Dato("miembros", "Miembros", Miles(f.CiudadanosIds.Count)));
            franja.Add(Dato("prestigio", "Prestigio", Miles(f.Prestigio), prestigio?.texto));
            franja.Add(Dato("reputacion", "Reputación", Miles((f.Reputacion?.ValorEnT0 ?? 0) / 1000.0)));
            lateral.Add(franja);
            if (prestigio != null && (prestigio.Value.usos.Count > 0 || prestigio.Value.siguiente != null))
            {
                var p = Dialogo.Caja("faccion-prestigio");
                if (prestigio.Value.usos.Count > 0)
                {
                    var usos = Dialogo.Caja("faccion-prestigio-usos");
                    foreach (var (glifo, texto, tip) in prestigio.Value.usos) { var u = ConGlifo(glifo, Dialogo.Texto(texto, "faccion-prestigio-uso-texto"), "faccion-prestigio-uso"); u.tooltip = tip; usos.Add(u); }
                    p.Add(usos);
                }
                if (prestigio.Value.siguiente != null) p.Add(Dialogo.Texto(prestigio.Value.siguiente, "faccion-prestigio-siguiente"));
                lateral.Add(p);
            }

            // El resumen: el Rey y el Embajador, si piden entrar, y la Liga.
            var resumen = Dialogo.Caja("faccion-lateral-resumen");
            var cargos = Dialogo.Caja("faccion-cargos-fila");
            VisualElement Cargo(string glifo, string nombre, string id)
            {
                var caja = Dialogo.Caja("faccion-cargo" + (string.IsNullOrEmpty(id) ? " vacante" : ""));
                caja.Add(ConGlifo(glifo, Dialogo.Texto(nombre, "faccion-cargo-etq"), "faccion-cargo-cab"));
                caja.Add(Dialogo.Texto(string.IsNullOrEmpty(id) ? "Sin designar" : Textos.NombreDeHeroe(a, id), "faccion-cargo-nombre"));
                return caja;
            }
            cargos.Add(Cargo("rey", "Rey", f.HasReyId ? f.ReyId : null));
            cargos.Add(Cargo("embajador", "Embajador", f.HasEmbajadorId ? f.EmbajadorId : null));
            resumen.Add(cargos);
            if (f.HasReyId && f.ReyId == h.Id && f.SolicitudesIds.Count > 0) resumen.Add(Dialogo.Texto("Hay héroes que piden entrar: contéstales en Miembros.", "faccion-nota"));
            resumen.Add(Kicker("Liga"));
            resumen.Add(LineaDeLiga(f));
            lateral.Add(resumen);

            // El pie: los cupos, los títulos y dejar la Facción.
            var pie = Dialogo.Caja("faccion-lateral-pie");
            CuposDeFaccion(pie, f);
            pie.Add(Kicker("Títulos del servidor"));
            TitulosDelServidor(pie, f.Id);
            DejarLaFaccion(pie, h, f);
            lateral.Add(pie);
            return lateral;
        }

        VisualElement LineaDeLiga(Faccion f)
        {
            if (f.Liga == null) return Dialogo.Texto("No perteneces a ninguna Liga.", "faccion-nota");
            var fila = Dialogo.Caja("liga-fila");
            if (f.Liga.TieneVasallaje && f.Liga.HasGranReyId) fila.Add(SigiloUI(SigiloDeFaccion(f.Liga.GranReyId).sigilo, 40, true));
            else
            {
                var orla = Dialogo.Caja("liga-orla");
                foreach (var id in f.Liga.MiembrosIds.Take(6)) orla.Add(SigiloUI(SigiloDeFaccion(id).sigilo, 22));
                fila.Add(orla);
            }
            var texto = Dialogo.Caja("liga-texto");
            texto.Add(Dialogo.Texto(f.Liga.TieneVasallaje ? "Por vasallaje" : "Por alianza", "liga-tipo"));
            texto.Add(Dialogo.Texto(string.Join(", ", f.Liga.MiembrosIds.Select(NombreDeFaccionConocida)), "faccion-nota"));
            fila.Add(texto);
            return fila;
        }

        string NombreDeFaccionConocida(string id) => FaccionesConocidas().FirstOrDefault(x => x.id == id).nombre ?? id;

        /// <summary>Lo que ya da el prestigio (alianzas, estatuas, Maravilla), lo próximo que abre y su explicación (`prestigioDe`).</summary>
        (List<(string glifo, string texto, string tip)> usos, string siguiente, string texto)? PrestigioDe(Faccion f)
        {
            var r = BloqueDeDatos("PRESTIGIO");
            var prestigio = (double)f.Prestigio;
            var estatuas = r?["estatuas"]?.Values<double>().ToList() ?? new List<double>();
            var caben = estatuas.Count(u => prestigio >= u);
            var maravilla = (double?)r?["maravilla"];
            var usos = new List<(string, string, string)>();
            var alianzas = c.Sesion.Almacen.Todas(Entidad.CuerpoOneofCase.Relacion).Select(e => e.Relacion)
                .Count(x => x.Estado == EstadoRelacion.RelacionActiva && x.Tipo == TipoRelacion.RelacionAlianza && (x.FaccionAId == f.Id || x.FaccionBId == f.Id));
            usos.Add(("alianzas", $"Alianzas {alianzas} de {f.TopeAlianzas}", "Alianzas activas de las que caben a la vez"));
            if (caben > 0) usos.Add(("estatuas", $"Estatuas {caben}", "Las estatuas que caben en la capital"));
            if (maravilla != null && prestigio >= maravilla) usos.Add(("plazas", "Maravilla", "Ya podéis levantar la Maravilla en una plaza de nivel 5"));
            var proximos = new List<(double umbral, string que)>();
            var cada = (double?)r?["alianzas"]?["unaMasCada"];
            if (cada > 0) proximos.Add(((Math.Floor(prestigio / cada.Value) + 1) * cada.Value, "una alianza más"));
            if (caben < estatuas.Count) proximos.Add((estatuas[caben], caben > 0 ? $"la {caben + 1}.ª estatua" : "una estatua en la capital"));
            if (maravilla != null && prestigio < maravilla) proximos.Add((maravilla.Value, "la Maravilla"));
            string siguiente = null;
            if (proximos.Count > 0)
            {
                var umbral = proximos.Min(x => x.umbral);
                var abre = proximos.Where(x => x.umbral == umbral).Select(x => x.que).ToList();
                siguiente = $"A {Miles(umbral)}: " + (abre.Count > 1 ? string.Join(", ", abre.Take(abre.Count - 1)) + " y " + abre.Last() : abre[0]);
            }
            var hechos = r == null ? new List<string>() : new[]
            {
                r["victoria"] != null ? $"las victorias contra otras Facciones ({r["victoria"]})" : null, r["conquista"] != null ? $"las conquistas ({r["conquista"]})" : null,
                r["fundacion"] != null ? $"las fundaciones ({r["fundacion"]})" : null, r["ascensoPorNivel"] != null ? $"las subidas de nivel de las plazas ({r["ascensoPorNivel"]} × el nivel nuevo)" : null,
                r["comercio"] != null ? $"el comercio con otras Facciones ({r["comercio"]})" : null,
            }.Where(x => x != null).ToList();
            var texto = (hechos.Count > 0 ? $"Sube con {string.Join(", ", hechos.Take(hechos.Count - 1))} y {hechos.Last()}. " : "")
                + $"No se gasta ni se pierde. Da una alianza a la vez más cada {cada ?? 100} de prestigio"
                + (estatuas.Count > 0 ? $", las estatuas de la capital (a {string.Join(", ", estatuas.Select(Miles))})" : "")
                + (maravilla != null ? $" y la Maravilla (a {Miles(maravilla.Value)})" : "") + ".";
            return (usos, siguiente, texto);
        }

        /// <summary>Los cupos (`htmlCuposFaccion`): 🏛 las plazas (y caravanas de fundación) contra su tope, y ② a ⑤ las de cada nivel; debajo, lo que
        /// abre el nivel siguiente de la Facción.</summary>
        void CuposDeFaccion(VisualElement v, Faccion f)
        {
            if (f.CapFundacion == null && f.CuposDeNivel.Count == 0) return;
            v.Add(ConAyuda(Kicker($"Plazas · nivel {f.Nivel} de Facción"),
                "Cuántas plazas puede tener tu Facción (🏛; las Caravanas de Fundación vivas cuentan: no se lanza otra si plazas más caravanas llegan al tope) y cuántas pueden estar a la vez en cada nivel (② a ⑤): ocupan el cupo las de ese nivel y las que suben a él. Sin cupo libre no se pide la subida ni recupera ese nivel una plaza degradada. Todo crece con el nivel de la Facción."));
            var fila = Dialogo.Caja("faccion-cupos-fila");
            fila.name = "faccion-cupos";
            void Casilla(VisualElement icono, string nombre, uint usado, uint tope, string nota)
            {
                var casilla = Dialogo.Caja("faccion-cupo" + (usado >= tope ? " lleno" : "") + (tope == 0 ? " cerrado" : ""));
                casilla.tooltip = $"{nombre}: {nota}";
                casilla.Add(icono);
                var cifra = Dialogo.Caja("faccion-cupo-cifra");
                cifra.Add(Dialogo.Texto(usado.ToString(), "faccion-cupo-usado"));
                cifra.Add(Dialogo.Texto("/" + tope, "faccion-cupo-tope"));
                casilla.Add(cifra);
                var barra = Dialogo.Caja("faccion-cupo-barra");
                var i = Dialogo.Caja("faccion-cupo-barra-i");
                i.style.width = Length.Percent(tope > 0 ? Math.Min(100f, 100f * usado / tope) : 0);
                barra.Add(i);
                casilla.Add(barra);
                fila.Add(casilla);
            }
            if (f.CapFundacion is CapFundacion cap)
                Casilla(GlifoDeFaccion("plazas", 14), "Plazas", cap.Plazas + cap.Caravanas, cap.Cap,
                    $"{cap.Plazas} plaza(s){(cap.Caravanas > 0 ? $" y {cap.Caravanas} Caravana(s) de Fundación" : "")} de {cap.Cap} que permite el nivel {f.Nivel}");
            foreach (var cupo in f.CuposDeNivel)
                Casilla(Dialogo.Texto(new[] { "", "", "②", "③", "④", "⑤" }.ElementAtOrDefault((int)cupo.Nivel) ?? "·", "faccion-cupo-nivel"), $"Nivel {cupo.Nivel}", cupo.Ocupadas, cupo.Cupo,
                    cupo.Cupo == 0 ? $"tu Facción aún no puede tener plazas de nivel {cupo.Nivel}" : $"plazas de nivel {cupo.Nivel} o subiendo a él");
            v.Add(fila);
            // Lo que abre el nivel siguiente (`CAP_FUNDACION_POR_NIVEL`, `CUPO_NIVEL_ASENTAMIENTO`).
            var n = (int)f.Nivel + 1;
            double? De(JToken tabla, int nivel)
            {
                var t = tabla?.Values<double>().ToList();
                return t == null || t.Count == 0 ? (double?)null : t[Math.Min(t.Count, Math.Max(1, nivel)) - 1];
            }
            var capTabla = BloqueDeDatos("CAP_FUNDACION_POR_NIVEL");
            var curvas = BloqueDeDatos("CUPO_NIVEL_ASENTAMIENTO");
            var abre = new List<string>();
            if (f.CapFundacion != null && capTabla != null && n <= capTabla.Count() && De(capTabla, n) != f.CapFundacion.Cap) abre.Add($"{De(capTabla, n)} plazas");
            foreach (var cupo in f.CuposDeNivel)
            {
                var t = curvas?[$"maxNivel{cupo.Nivel}"];
                if (t != null && n <= t.Count() && De(t, n) is double x && x != cupo.Cupo) abre.Add($"{x} de nivel {cupo.Nivel}");
            }
            if (abre.Count > 0) v.Add(Dialogo.Texto($"Con la Facción en nivel {n}: {string.Join(", ", abre)}.", "faccion-nota"));
        }

        /// <summary>Los títulos del servidor (`htmlTitulos`): la insignia, el nombre y quién lo tiene con su sigilo pequeño.</summary>
        void TitulosDelServidor(VisualElement v, string propiaId)
        {
            var a = c.Sesion.Almacen;
            var titulos = a.Todas(Entidad.CuerpoOneofCase.Titulo).Select(e => e.Titulo).ToList();
            if (titulos.Count == 0) { v.Add(Dialogo.Texto("Aún no hay títulos en el servidor.", "faccion-nota")); return; }
            foreach (var t in titulos)
            {
                var suyo = t.PoseedorId == propiaId;
                var fila = Dialogo.Caja("titulo-item" + (suyo ? " titulo-propio" : ""));
                fila.name = "titulo-" + t.TituloId;
                fila.Add(InsigniaUI(t.TituloId, 34));
                var texto = Dialogo.Caja("titulo-texto");
                texto.Add(Dialogo.Texto(t.Nombre, "titulo-nombre"));
                var quien = Dialogo.Caja("titulo-quien");
                quien.Add(SigiloUI(SigiloDeFaccion(t.PoseedorId).sigilo, 14));
                quien.Add(Dialogo.Texto(NombreDeFaccionConocida(t.PoseedorId) + (suyo ? " · la tuya" : ""), "titulo-quien-texto"));
                texto.Add(quien);
                fila.Add(texto);
                v.Add(fila);
            }
        }

        /// <summary>La insignia de un título (`svgInsignia`): un medallón de bronce con su icono de `insignias.json`.</summary>
        static VisualElement InsigniaUI(string tituloId, float lado)
        {
            var e = new VisualElement { pickingMode = PickingMode.Ignore };
            e.AddToClassList("insignia");
            e.style.width = lado;
            e.style.height = lado;
            e.style.flexShrink = 0;
            e.style.backgroundImage = new StyleBackground(TexturaDeSigilo.Insignia(tituloId, Mathf.CeilToInt(lado * 2)));
            e.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            return e;
        }

        /// <summary>«Dejar la Facción» (`htmlDejarFaccion`), con lo que implica según tu cargo y la confirmación.</summary>
        void DejarLaFaccion(VisualElement v, Heroe h, Faccion f)
        {
            var alguienMas = f.CiudadanosIds.Any(id => id != h.Id);
            var cargo = f.HasReyId && f.ReyId == h.Id
                ? alguienMas ? " Eres el Rey: el trono pasa al siguiente ciudadano por orden de ingreso." : " Eres el último ciudadano: la Facción se queda sin Rey."
                : f.HasEmbajadorId && f.EmbajadorId == h.Id ? " Eres el Embajador: la embajada queda libre." : "";
            var dias = (double?)BloqueDeDatos("CIUDADANIA")?["cooldownCreacionFaccionDias"];
            v.Add(ConAyuda(Kicker("Abandonar"), $"Pierdes la ciudadanía, tu casa y tus cargos locales; pasas al campamento de mercenarios más cercano y conservas lo que llevas.{cargo}"
                + (dias != null ? $" No podrás crear otra Facción hasta pasados {dias} días." : "")));
            var error = Dialogo.Texto("", "error oculto");
            Button dejar = null;
            dejar = Dialogo.Boton("Dejar la Facción", "boton-secundario faccion-dejar", async () =>
            {
                if (!await dialogo.Abrir("Pierdes ciudadanía, casa y cargos, y tendrás que esperar para crear otra. Si eres Rey, el trono pasa al siguiente ciudadano.",
                        "¿Dejar la Facción?", "Dejar la Facción")) return;
                await OrdenDelPanel(new Orden { DejarFaccion = new OrdenDejarFaccion() }, m => Poner(error, m), () => { firmaFaccion = null; }, dejar);
            });
            dejar.name = "dejar-faccion";
            v.Add(dejar);
            v.Add(error);
        }

        /// <summary>En la Base, bajo el nombre del héroe: el sigilo pequeño de su Facción y sus cargos («Rey», «Gobernador de Tirinto»…). Null sin
        /// Facción o sin cargos.</summary>
        VisualElement CargosDeFaccionEnLaBase(Heroe h)
        {
            if (!h.HasFaccionId) return null;
            var a = c.Sesion.Almacen;
            var f = a.Buscar(TipoEntidad.Faccion, h.FaccionId)?.Faccion;
            var m = f?.Miembros.FirstOrDefault(x => x.HeroeId == h.Id);
            if (m == null || m.Cargos.Count == 0) return null;
            string Lugar(string id) => a.Buscar(TipoEntidad.Asentamiento, id)?.Asentamiento is Asentamiento p && p.HasNombre ? p.Nombre : id;
            var fila = Dialogo.Caja("base-cargos");
            fila.name = "base-cargos";
            fila.Add(SigiloUI(f.Sigilo, 18));
            fila.Add(Dialogo.Texto(string.Join(", ", m.Cargos.Select(x => { var partes = x.Split('@'); return NombreDeCargoDeFaccion(partes[0]) + (partes.Length > 1 ? " de " + Lugar(partes[1]) : ""); })),
                "base-cargos-texto"));
            fila.tooltip = f.Nombre;
            return fila;
        }

        // ─────────────── Miembros ───────────────

        void PestanaMiembros(VisualElement v, Heroe h, Faccion f)
        {
            var a = c.Sesion.Almacen;
            var esRey = f.HasReyId && f.ReyId == h.Id;
            var error = Dialogo.Texto("", "error oculto");
            error.name = "error-faccion-miembros";
            // Piden entrar (solo el Rey): Aceptar o Denegar.
            if (esRey && f.SolicitudesIds.Count > 0)
            {
                v.Add(Kicker("Piden entrar"));
                foreach (var id in f.SolicitudesIds)
                {
                    var fila = Dialogo.Caja("faccion-fila");
                    fila.name = "solicitud-" + id;
                    fila.Add(Dialogo.Texto(Textos.NombreDeHeroe(a, id), "faccion-fila-nombre"));
                    foreach (var (texto, si) in new[] { ("Aceptar", true), ("Denegar", false) })
                    {
                        Button b = null;
                        b = Dialogo.Boton(texto, "boton-secundario", async () => await OrdenDelPanel(
                            new Orden { ResponderSolicitud = new OrdenResponderSolicitud { FaccionId = f.Id, HeroeId = id, Aceptar = si } }, m => Poner(error, m), () => { firmaFaccion = null; }, b));
                        b.name = (si ? "aceptar-" : "denegar-") + id;
                        fila.Add(b);
                    }
                    v.Add(fila);
                }
            }
            // Los cargos (solo el Rey): designar Embajador y traspasar el trono; los mercenarios con marca, fuera.
            if (esRey)
            {
                var marcados = new HashSet<string>(f.Miembros.Where(m => !string.IsNullOrEmpty(m.Marca)).Select(m => m.HeroeId));
                var ciudadanos = f.CiudadanosIds.Where(id => !marcados.Contains(id)).ToList();
                var otros = ciudadanos.Where(id => id != h.Id).ToList();
                var embajadores = ciudadanos.Where(id => !f.HasEmbajadorId || id != f.EmbajadorId).ToList();
                v.Add(ConAyuda(Kicker("Cargos"), (embajadores.Count > 0 ? "El Embajador propone, con el Rey, alianzas y vasallajes, declara guerra y ofrece la paz. " : "")
                    + (otros.Count > 0 ? "Al traspasar el trono dejas de ser Rey en el acto; solo el nuevo Rey podría devolvértelo." : "")));
                var caja = Dialogo.Caja("faccion-cargos");
                void Selector(string nombre, List<string> ids, string boton, Func<string, Orden> orden, string confirmar = null)
                {
                    var fila = Dialogo.Caja("faccion-fila");
                    string Nombre(string id) => id == null ? "" : Textos.NombreDeHeroe(a, id);
                    var combo = new DropdownField(ids, 0, Nombre, Nombre) { name = "sel-" + nombre };
                    combo.AddToClassList("sigilo-campo");
                    combo.AddToClassList("faccion-combo");
                    fila.Add(combo);
                    Button b = null;
                    b = Dialogo.Boton(boton, "boton-secundario", async () =>
                    {
                        if (confirmar != null && !await dialogo.Abrir(string.Format(confirmar, Textos.NombreDeHeroe(a, combo.value)), "¿Traspasar el trono?", "Traspasar")) return;
                        await OrdenDelPanel(orden(combo.value), m => Poner(error, m), () => { firmaFaccion = null; }, b);
                    });
                    b.name = "btn-" + nombre;
                    fila.Add(b);
                    caja.Add(fila);
                }
                if (embajadores.Count > 0)
                    Selector("embajador", embajadores, "Designar Embajador", id => new Orden { AsignarEmbajador = new OrdenAsignarEmbajador { FaccionId = f.Id, HeroeId = id } });
                if (otros.Count > 0)
                    Selector("rey", otros, "Traspasar el trono", id => new Orden { AsignarRey = new OrdenAsignarRey { FaccionId = f.Id, HeroeId = id } },
                        "El trono pasa a {0}: dejarás de ser Rey en el acto.");
                else caja.Add(Dialogo.Texto("Eres el único ciudadano: nadie más puede recibir el trono ni la embajada.", "faccion-nota"));
                v.Add(caja);
            }
            v.Add(error);
            // Los miembros, por participación, con sus cargos y dónde residen.
            if (f.Miembros.Count == 0) return;
            v.Add(ConAyuda(Dialogo.Texto("Miembros", "faccion-h3"), "Cada ciudadano con sus cargos, dónde reside y la suma de su participación en las plazas que son hoy de la Facción."));
            string Residencia(string id)
            {
                if (string.IsNullOrEmpty(id)) return "sin residencia";
                if (id.StartsWith("camp-")) return "campamento de mercenarios";
                var p = a.Buscar(TipoEntidad.Asentamiento, id)?.Asentamiento;
                return p != null && p.HasNombre ? p.Nombre : id;
            }
            string Cargo(string clave)
            {
                var partes = clave.Split('@');
                var nombre = NombreDeCargoDeFaccion(partes[0]);
                return partes.Length > 1 ? $"{nombre} de {Residencia(partes[1])}" : nombre;
            }
            var lista = Dialogo.Caja("participacion-lista");
            lista.name = "miembros";
            foreach (var m in f.Miembros.OrderByDescending(x => x.Participacion))
            {
                var fila = Dialogo.Caja("participacion-fila" + (m.HeroeId == h.Id ? " participacion-yo" : ""));
                fila.name = "miembro-" + m.HeroeId;
                var izq = Dialogo.Caja("participacion-izq");
                var linea = Dialogo.Caja("participacion-linea");
                linea.Add(Dialogo.Texto(Textos.NombreDeHeroe(a, m.HeroeId), "participacion-nombre"));
                if (m.Cargos.Count > 0) linea.Add(Dialogo.Texto("· " + string.Join(", ", m.Cargos.Select(Cargo)), "participacion-cargos"));
                izq.Add(linea);
                izq.Add(Dialogo.Texto(Residencia(m.HasResidenciaId ? m.ResidenciaId : null), "participacion-donde"));
                fila.Add(izq);
                fila.Add(Dialogo.Texto(m.Participacion.ToString(), "participacion-puntos"));
                lista.Add(fila);
            }
            v.Add(lista);
        }

        /// <summary>El nombre de un cargo (`nombreCargo`): rey → Rey, gobernador → Gobernador, maestroObras → Maestro de Obras…</summary>
        static string NombreDeCargoDeFaccion(string clave) => clave switch
        {
            "rey" => "Rey", "embajador" => "Embajador", "gobernador" => "Gobernador", "tesorero" => "Tesorero", "general" => "General",
            "maestroObras" => "Maestro de Obras", "sacerdote" => "Sacerdote", "maestroCaravanas" => "Maestro de Caravanas", _ => clave,
        };

        // ─────────────── Plazas ───────────────

        void PestanaPlazas(VisualElement v, Heroe h, Faccion f)
        {
            var a = c.Sesion.Almacen;
            var plazas = a.Todas(Entidad.CuerpoOneofCase.Asentamiento).Select(e => e.Asentamiento).Where(x => x.FaccionId == f.Id).ToList();
            if (plazas.Count == 0) { v.Add(Dialogo.Texto("Tu Facción aún no tiene plazas.", "faccion-vacia")); return; }
            var capitalId = f.HasCapitalId ? f.CapitalId : null;
            int Residentes(string id) => f.Miembros.Count(m => m.HasResidenciaId && m.ResidenciaId == id);
            v.Add(Kicker($"Plazas de tu Facción ({plazas.Count})"));
            foreach (var p in plazas.OrderByDescending(x => x.Id == capitalId).ThenByDescending(x => x.Nivel).ThenBy(x => x.HasNombre ? x.Nombre : x.Id))
            {
                var conInterior = p.Edificios.Count > 0 || p.Poblacion != null;
                var abierta = conInterior && plazasAbiertas.Contains(p.Id);
                var caja = Dialogo.Caja("faccion-plaza" + (p.Id == capitalId ? " capital" : "") + (abierta ? " abierta" : ""));
                caja.name = "plaza-" + p.Id;
                var fila = Dialogo.Caja("faccion-plaza-fila");
                var info = Dialogo.Caja("faccion-plaza-info");
                var titulo = Dialogo.Caja("faccion-publica-nombre");
                if (conInterior) titulo.Add(Dialogo.Texto(abierta ? "▾" : "▸", "faccion-plaza-flecha"));
                titulo.Add(Dialogo.Texto(p.HasNombre ? p.Nombre : p.Id, "faccion-publica-titulo"));
                if (p.Id == capitalId) titulo.Add(Dialogo.Texto("Capital", "faccion-publica-relacion"));
                info.Add(titulo);
                var rinde = p.NivelActual < p.Nivel ? $" (rinde como {p.NivelActual})" : "";
                var pos = p.Posicion;
                info.Add(Dialogo.Texto($"Nivel {p.Nivel}{rinde} · ({Math.Round((pos?.X ?? 0) / 1000.0)}, {Math.Round((pos?.Y ?? 0) / 1000.0)})", "faccion-publica-datos"));
                fila.Add(info);
                var cifras = Dialogo.Caja("faccion-publica-cifras");
                var cc = Dialogo.Caja("faccion-cifra");
                cc.Add(Dialogo.Texto(Residentes(p.Id).ToString(), "faccion-cifra-n"));
                cc.Add(Dialogo.Texto("RESIDENTES", "faccion-cifra-q"));
                cifras.Add(cc);
                fila.Add(cifras);
                // «Ver en el mapa» no sale: la cámara del mapa de Conquest sigue siempre al héroe (no hay cómo llevarla a otro sitio).
                if (conInterior)
                {
                    fila.AddToClassList("faccion-plaza-pulsable");
                    fila.tooltip = abierta ? "Plegar" : "Desplegar";
                    var id = p.Id;
                    fila.RegisterCallback<ClickEvent>(e =>
                    {
                        if (e.target is Button) return;
                        if (!plazasAbiertas.Remove(id)) plazasAbiertas.Add(id);
                        firmaFaccion = null;
                        PintarFaccion();
                    });
                }
                caja.Add(fila);
                if (abierta) caja.Add(DetalleDePlaza(p));
                v.Add(caja);
            }
        }

        /// <summary>Lo de dentro de una plaza desplegada (`htmlInteriorPlaza`): el almacén por recurso, la población, las obras y la guarnición.</summary>
        VisualElement DetalleDePlaza(Asentamiento p)
        {
            var detalle = Dialogo.Caja("fplaza-detalle");
            var almacen = Dialogo.Caja("fplaza-bloque fplaza-almacen");
            almacen.Add(Kicker("Almacén"));
            var fichas = Dialogo.Caja("fplaza-fichas");
            foreach (var (r, x) in p.Almacen.OrderBy(kv => kv.Key))
            {
                var cant = x.Cantidad?.ValorEnT0 ?? 0;
                if (cant <= 0) continue;
                var ficha = Dialogo.Caja("fplaza-ficha");
                if (IconosDelMundo.Icono("recurso", r, 16) is VisualElement ico) ficha.Add(ico);
                ficha.Add(Dialogo.Texto(Miles(cant / 1000.0), "fplaza-ficha-cifra"));
                ficha.tooltip = CosasDelMapa.NombreDeRecurso(r);
                fichas.Add(ficha);
            }
            if (fichas.childCount == 0) almacen.Add(Dialogo.Texto("El almacén está vacío.", "faccion-nota"));
            else almacen.Add(fichas);
            if (p.AutoConstruccionPausada) almacen.Add(Dialogo.Texto("La auto-construcción está apagada: no se levantan edificios nuevos solos.", "faccion-nota"));
            detalle.Add(almacen);
            var columna = Dialogo.Caja("fplaza-columna");
            if (p.Poblacion != null)
            {
                var pob = Dialogo.Caja("fplaza-bloque");
                pob.Add(Kicker("Población"));
                var cifras = Dialogo.Caja("fplaza-poblacion");
                foreach (var (glifo, nombre, valor) in new[] { ("pesants", "Pesants", p.Poblacion.Pesants), ("artesanos", "Artesanos", p.Poblacion.Artesanos), ("nobleza", "Nobleza", p.Poblacion.Nobleza) })
                {
                    var d = Dialogo.Caja("fplaza-clase");
                    if (Glifo(glifo, 18) is VisualElement g) d.Add(g);
                    d.Add(Dialogo.Texto(Miles((valor?.ValorEnT0 ?? 0) / 1000.0), "fplaza-clase-cifra"));
                    d.Add(Dialogo.Texto(nombre, "fplaza-clase-nombre"));
                    cifras.Add(d);
                }
                pob.Add(cifras);
                columna.Add(pob);
            }
            var obras = Dialogo.Caja("fplaza-bloque");
            obras.Add(Kicker("Obras y mejoras"));
            var enObra = p.Edificios.Where(e => e.Estado == EstadoEdificio.EdificioEnConstruccion || e.Mejora != null).ToList();
            if (p.Ascenso != null) obras.Add(Dialogo.Texto($"⬆ Subida a nivel {p.Ascenso.NivelObjetivo}", "fplaza-obra"));
            foreach (var e in enObra) obras.Add(Dialogo.Texto($"{Edificios.Nombre(e.Tipo)} {(e.Mejora != null ? $"a nivel {e.Mejora.NivelObjetivo}" : "en obra")}", "fplaza-obra"));
            if (enObra.Count == 0 && p.Ascenso == null) obras.Add(Dialogo.Texto("Sin obras ni mejoras en curso.", "faccion-nota"));
            columna.Add(obras);
            var guarnicion = Dialogo.Caja("fplaza-bloque");
            guarnicion.Add(Kicker("Guarnición"));
            if (p.Guarnicion.Count == 0) guarnicion.Add(Dialogo.Texto("Nadie ha dejado tropa en la guarnición.", "faccion-nota"));
            foreach (var g in p.Guarnicion.GroupBy(x => x.HeroeId))
                guarnicion.Add(Dialogo.Texto($"{(g.Key == c.HeroeId ? "Tuya" : Textos.NombreDeHeroe(c.Sesion.Almacen, g.Key))}: {string.Join(", ", g.Select(x => $"{NombreDeTropa(x.TropaId)} {x.Cantidad}"))}", "fplaza-obra"));
            columna.Add(guarnicion);
            detalle.Add(columna);
            return detalle;
        }

        // ─────────────── Ajustes del Rey ───────────────

        void PestanaAjustes(VisualElement v, Heroe h, Faccion f)
        {
            var esRey = f.HasReyId && f.ReyId == h.Id;
            if (!esRey) v.Add(Dialogo.Texto("Estos ajustes los cambia el Rey; aquí ves cómo están.", "faccion-nota"));
            var admite = f.AdmiteOtrasEnAtaques;
            v.Add(ConAyuda(Kicker("Ataques abiertos a otras Facciones"), admite
                ? "En vuestros asedios pueden unirse al ataque héroes de Facciones neutrales o enemigas del defensor."
                : "En vuestros asedios solo combaten ciudadanos de tu Facción."));
            var fila = Dialogo.Caja("faccion-fila");
            fila.Add(Dialogo.Texto(admite ? "Admitidas" : "Solo la tuya", "faccion-fila-nombre"));
            var error = Dialogo.Texto("", "error oculto");
            if (esRey)
            {
                Button b = null;
                b = Dialogo.Boton(admite ? "Dejar de admitirlas" : "Admitirlas", "boton-secundario", async () => await OrdenDelPanel(
                    new Orden { AdmitirOtrasFacciones = new OrdenAdmitirOtrasFacciones { FaccionId = f.Id, Admitir = !admite } }, m => Poner(error, m), () => { firmaFaccion = null; }, b));
                b.name = "btn-admision";
                fila.Add(b);
            }
            v.Add(fila);
            v.Add(error);
        }
    }
}
