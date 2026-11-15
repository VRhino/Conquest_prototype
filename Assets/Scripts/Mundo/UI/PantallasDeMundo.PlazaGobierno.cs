// La plaza por dentro, tanda 2 (paso 7 del bloque 4; BronzeAgeClient: `main.ts` «LAS VENTANAS DE LA PLAZA», `ui/panelObras.ts`, `ui/panelCentro.ts`,
// `ui/participacion.ts`, `ui/panelTesoreria.ts`, `ui/hudPlaza.ts`, `ui/vistaCiudad.ts`, `ui/consumoPausado.ts`, `ui/panelCarro.ts`). El riel con lo que
// se puede abrir (Construcción con cargo, Gobierno); Construcción no abre ventana sino el cajón que ocupa el sitio del riel (la auto-construcción,
// «Construidos» y lo que se puede poner en la cola, por tipos); Gobierno, con Cargos (y los residentes) y Políticas; y el Almacén, que se abre con un
// clic en su placa, a pantalla completa: qué va en la barra de arriba, el almacén con su neto, la extracción, la reserva del Tesorero, pausar el
// consumo y los talleres; y «Tu carga», el carro y el almacén personal frente al de la plaza. Lo que no se puede hacer no sale; el motor valida y su
// rechazo sale flotando.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BronzeAge.Contract.V1;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace ConquestTactics.Mundo.UI
{
    public sealed partial class PantallasDeMundo
    {
        /// <summary>La ventana abierta de la plaza: «ascenso», «construccion» (el cajón del riel), «gobierno» o «almacen»; null, ninguna.</summary>
        string ventanaPlaza;
        bool ventanaAscenso
        {
            get => ventanaPlaza == "ascenso";
            set { if (value) ventanaPlaza = "ascenso"; else if (ventanaPlaza == "ascenso") ventanaPlaza = null; }
        }
        /// <summary>La subpestaña elegida en cada ventana que tiene varias.</summary>
        readonly Dictionary<string, string> subDePlaza = new Dictionary<string, string>();
        /// <summary>La pestaña de Construcción elegida (sin «Todos»: siempre un tipo), lo que se destaca (la Vivienda cuando no cabe más gente) y si
        /// está abierta la ventana «Construidos».</summary>
        string grupoConstruir;
        bool construidosAbiertos;
        /// <summary>Con varios del mismo tipo, cada clic en su cuadrito de «Construidos» elige el siguiente.</summary>
        readonly Dictionary<string, int> siguienteConstruido = new Dictionary<string, int>();
        /// <summary>Los cargos que se están cambiando (su combo a la vista) y a quién se ha elegido en cada uno.</summary>
        readonly Dictionary<string, string> cargosEditando = new Dictionary<string, string>();
        /// <summary>Lo que el Tesorero lleva escrito en la reserva sin guardar, y los recursos que añadió a la tabla.</summary>
        readonly Dictionary<string, long> borradorReserva = new Dictionary<string, long>();
        readonly HashSet<string> reservaAnadidos = new HashSet<string>();
        /// <summary>¿Enseña la elección de la barra todos los recursos o solo los que hay?</summary>
        bool eleccionConTodos;
        /// <summary>El letrero de la plaza (el tooltip del prototipo).</summary>
        Label letreroDePlaza;

        const int MaxBarraAlmacen = 8;
        const string ClaveBarra = "mundo.plaza.barra";

        JToken DatoDeJuego(string documento, string nombre)
        {
            try { return c.Datos.Bloque(documento, nombre); }
            catch (FaltaDato) { return null; }
        }

        /// <summary>El producto de un factor en las políticas en vigor de la plaza (`factorDePoliticas`).</summary>
        double FactorDePoliticas(Asentamiento a, string campo)
        {
            var catalogo = DatoDeJuego("Economia_Construccion", "POLITICA_CATALOGO") as JArray;
            double f = 1;
            foreach (var x in a.PoliticasActivas)
                if (catalogo?.FirstOrDefault(d => (string)d["id"] == x.PoliticaId)?[campo] is JValue v && (v.Type == JTokenType.Float || v.Type == JTokenType.Integer)) f *= (double)v;
            return f;
        }

        bool Reside(Asentamiento a, Heroe yo) => yo.HasResidenciaId && yo.ResidenciaId == a.Id;
        static bool Presente(Asentamiento a, Heroe yo) => yo.Ubicacion?.DondeCase == UbicacionHeroe.DondeOneofCase.AsentamientoId && yo.Ubicacion.AsentamientoId == a.Id;
        static string TitularDe(Asentamiento a, string cargo)
        {
            var k = a.Cargos;
            if (k == null) return null;
            return cargo switch
            {
                "gobernador" => k.HasGobernadorId ? k.GobernadorId : null,
                "tesorero" => k.HasTesoreroId ? k.TesoreroId : null,
                "general" => k.HasGeneralId ? k.GeneralId : null,
                "maestroObras" => k.HasMaestroObrasId ? k.MaestroObrasId : null,
                "sacerdote" => k.HasSacerdoteId ? k.SacerdoteId : null,
                "maestroCaravanas" => k.HasMaestroCaravanasId ? k.MaestroCaravanasId : null,
                _ => null,
            };
        }

        /// <summary>¿No cabe más gente? Pesants o artesanos al 95 % del sitio de las Viviendas (`faltaVivienda`): Construcción lleva el «!».</summary>
        bool FaltaVivienda(Asentamiento a)
        {
            if (a.Poblacion == null) return false;
            var cat = Catalogo;
            var viviendas = Internos(a).Count(e => e.Tipo == "vivienda" && Activo(e));
            var sp = (double?)cat?["vivienda"]?["capacidadPesants"] * viviendas;
            var sa = (double?)cat?["vivienda"]?["capacidadArtesanos"] * viviendas;
            return sp != null && Math.Floor(Valor(a.Poblacion.Pesants)) >= sp * 0.95 || sa != null && Math.Floor(Valor(a.Poblacion.Artesanos)) >= sa * 0.95;
        }

        /// <summary>El letrero al pasar el ratón (el tooltip del prototipo): debajo del elemento, o encima si no cabe.</summary>
        void ConLetrero(VisualElement e, string texto)
        {
            e.RegisterCallback<PointerEnterEvent>(_ =>
            {
                var l = letreroDePlaza;
                if (l == null || string.IsNullOrEmpty(texto)) return;
                l.text = texto;
                Ver(l, true);
                var r = e.worldBound;
                var abajo = r.yMax + 90 < (l.panel?.visualTree.layout.height ?? 1080);
                l.style.left = r.center.x;
                l.style.top = abajo ? r.yMax + 7 : r.yMin - 7;
                l.style.translate = new Translate(Length.Percent(-50), abajo ? Length.Percent(0) : Length.Percent(-100));
            });
            e.RegisterCallback<PointerLeaveEvent>(_ => { if (letreroDePlaza != null) Ver(letreroDePlaza, false); });
        }

        /// <summary>Una rejilla como `grid-template-columns: repeat(auto-fill, minmax(min, 1fr))`: tantas columnas como quepan, del mismo ancho.</summary>
        static VisualElement Rejilla(string clases, float minimo, float hueco)
        {
            var cont = Dialogo.Caja("plaza-rejilla " + clases);
            void Colocar()
            {
                var w = cont.contentRect.width;
                if (float.IsNaN(w) || w <= 0) return;
                var n = Mathf.Max(1, Mathf.FloorToInt((w + hueco) / (minimo + hueco)));
                // (un píxel de holgura: con el ancho justo, el redondeo manda la última columna a otra fila)
                var ancho = Mathf.Floor((w - hueco * (n - 1)) / n) - 1;
                var i = 0;
                foreach (var hijo in cont.Children())
                {
                    hijo.style.width = ancho;
                    hijo.style.marginRight = i % n == n - 1 ? 0 : hueco;
                    hijo.style.marginBottom = hueco;
                    i++;
                }
            }
            cont.RegisterCallback<GeometryChangedEvent>(_ => Colocar());
            return cont;
        }

        /// <summary>Dos columnas iguales desde `desde` px de ancho (`@container (min-width: …)`); más estrecho, una debajo de otra.</summary>
        static void DosColumnas(VisualElement cont, VisualElement izq, VisualElement der, float desde, float hueco)
        {
            cont.Add(izq);
            cont.Add(der);
            cont.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                var dos = cont.contentRect.width >= desde;
                cont.style.flexDirection = dos ? FlexDirection.Row : FlexDirection.Column;
                foreach (var col in new[] { izq, der })
                {
                    col.style.flexGrow = dos ? 1 : 0;
                    col.style.flexBasis = dos ? 0 : new StyleLength(StyleKeyword.Auto);
                }
                izq.style.marginRight = dos ? hueco : 0;
            });
        }

        /// <summary>Un interruptor con aspecto de palanca (`.interruptor`): su pista, el texto y, a la derecha, lo que dice cada lado.</summary>
        VisualElement Interruptor(string nombre, string texto, VisualElement icono, bool encendido, string si, string no, string clases, Func<bool, Task<bool>> alCambiar)
        {
            var caja = Dialogo.Caja("plaza-interruptor " + clases);
            caja.name = nombre;
            caja.EnableInClassList("encendido", encendido);
            var pista = Dialogo.Caja("plaza-interruptor-pista");
            pista.Add(Dialogo.Caja("plaza-interruptor-bola"));
            caja.Add(pista);
            if (icono != null) { icono.AddToClassList("plaza-interruptor-icono"); caja.Add(icono); }
            caja.Add(Dialogo.Texto(texto, "plaza-interruptor-texto"));
            var estado = Dialogo.Texto(encendido ? si : no, "plaza-interruptor-estado");
            caja.Add(estado);
            var ocupado = false;
            caja.RegisterCallback<ClickEvent>(async _ =>
            {
                if (ocupado) return;
                ocupado = true;
                var nuevo = !caja.ClassListContains("encendido");
                caja.EnableInClassList("encendido", nuevo);
                estado.text = nuevo ? si : no;
                if (!await alCambiar(nuevo)) { caja.EnableInClassList("encendido", !nuevo); estado.text = !nuevo ? si : no; }
                ocupado = false;
            });
            return caja;
        }

        /// <summary>Una cifra con signo para los netos: verde si suma, rojo si resta.</summary>
        static string ConSigno(double n) => (n > 0 ? "+" : "") + Cifra(n);

        // ─────────────────────────── el riel ───────────────────────────

        /// <summary>Los botones del riel, en el orden del prototipo, con su icono (los de otros pasos, en el suyo).</summary>
        static readonly (string id, string titulo, string familia, string icono)[] RielDePlaza =
        {
            ("construccion", "Construcción", "riel", "construccion"), ("gobierno", "Gobierno", "riel", "gobierno"), ("defensa", "Defensa", "edificio", "muralla"),
            ("reclutamiento", "Reclutamiento", "edificio", "barracon"), ("armeria", "Armería", "edificio", "armeria"),
        };

        /// <summary>¿Se puede abrir ahora esa ventana? Construcción, con cargo de Gobernador o Maestro de Obras; el Almacén, con almacén; la Armería, activa.
        /// De visita en una plaza ajena solo queda la Armería (lo que se usa desde fuera; Territorio y Mercado, en su paso).</summary>
        bool VentanaDisponible(string ventana, Asentamiento a, Heroe yo) => ventana switch
        {
            "armeria" => ArmeriaActiva(a),
            _ when !Propia(a, yo) => false,
            "ascenso" => true,
            "gobierno" => true,
            "defensa" => true,
            "reclutamiento" => true,
            "almacen" => a.Almacen.Count > 0,
            "construccion" => CargoConstructor(a, yo.Id) != null,
            _ => false,
        };

        /// <summary>Lo que enseña el riel (o el cajón): sus botones e insignias, la pestaña, lo que se puede pagar ya de cada edificio, lo construido
        /// y la auto-construcción. No cambia con cada paso de minuto, solo cuando cambia algo de eso.</summary>
        string FirmaDelRiel(Asentamiento a, Heroe yo)
        {
            var cargo = CargoConstructor(a, yo.Id);
            var enMarcha = Internos(a).Count(e => e.Mejora != null || e.Estado == EstadoEdificio.EdificioEnConstruccion);
            var firma = $"{ventanaPlaza}|{cargo}|{FaltaVivienda(a)}|{enMarcha}|{VentanaDisponible("almacen", a, yo)}";
            if (ventanaPlaza != "construccion") return firma;
            var cat = Catalogo;
            var caros = string.Concat((cat?.Properties() ?? Enumerable.Empty<JProperty>())
                .Select(p => (p.Value["costo"] as JObject)?.Properties().Any(r => Hay(a, r.Name) < (double)r.Value) == true ? '1' : '0'));
            return $"{firma}|{grupoConstruir}|{construidosAbiertos}|{a.AutoConstruccionPausada}|{FirmaDePlanta(a)}|{caros}|{TecnologiasDeMiFaccion()?.Count}|{a.NivelActual}";
        }

        /// <summary>El riel: un botón por ventana que se puede abrir, con su nombre encima al pasar; Construcción con el «!» si falta Vivienda o con
        /// las obras en marcha. Con Construcción abierta, el cajón en su sitio.</summary>
        void PintarRiel(VisualElement v, Asentamiento a, Heroe yo, Action rehacer)
        {
            var riel = v.Q("riel");
            riel.Clear();
            if (letreroDePlaza != null) Ver(letreroDePlaza, false);
            var cargo = CargoConstructor(a, yo.Id);
            var construyendo = ventanaPlaza == "construccion";
            v.Q("plaza").EnableInClassList("construyendo", construyendo);
            riel.EnableInClassList("plaza-riel-cajon", construyendo);
            if (construyendo)
            {
                CajonDeConstruccion(riel, a, yo, cargo, rehacer);
                Ver(riel, true);
                return;
            }
            var enMarcha = Internos(a).Count(e => e.Mejora != null || e.Estado == EstadoEdificio.EdificioEnConstruccion);
            foreach (var (id, titulo, familia, icono) in RielDePlaza)
            {
                if (!VentanaDisponible(id, a, yo)) continue;
                var ventana = id;
                var b = new Button(() => AbrirVentanaDePlaza(ventana, a, rehacer)) { name = "riel-" + id };
                b.AddToClassList("plaza-riel-boton");
                b.EnableInClassList("activo", ventanaPlaza == id);
                b.Add(IconosDelMundo.Icono(familia, icono, 28, ventanaPlaza == id ? IconosDelMundo.OroClaro : IconosDelMundo.Bronce));
                if (id == "construccion" && FaltaVivienda(a)) b.Add(Dialogo.Texto("!", "plaza-atencion"));
                else if (id == "construccion" && enMarcha > 0) b.Add(Dialogo.Texto(enMarcha.ToString(), "plaza-riel-insignia"));
                b.Add(Dialogo.Texto(titulo, "plaza-riel-etiqueta"));
                riel.Add(b);
            }
            Ver(riel, riel.childCount > 0);
        }

        // ─────────────────────────── Construcción ───────────────────────────

        /// <summary>Los tipos de las pestañas de Construcción (el catálogo del motor no los agrupa: es solo cómo se enseñan).</summary>
        static readonly (string id, string nombre, string icono, string[] tipos)[] GruposConstruir =
        {
            ("ciudad", "Ciudad", "vivienda", new[] { "vivienda", "almacen", "granero", "pozo", "parque", "plaza" }),
            ("recursos", "Recursos", "granja", new[] { "granja", "lenera" }),
            ("comida", "Comida", "horno", new[] { "molino", "horno", "quesera", "bodega", "prensa" }),
            ("talleres", "Talleres", "fundicion", new[] { "fundicion", "granFundicion", "curtiduria", "armeria", "carpinteria", "patioDeGremios" }),
            ("militar", "Militar", "barracon", new[] { "barracon", "galeriaDeTiro", "caballerizas", "plazaDeArmas" }),
            ("gobierno", "Gobierno", "palacio", new[] { "salaConsejo", "palacio", "taberna", "mercado", "albergueCaravanas", "maravilla", "estatua" }),
            ("otros", "Otros", "centroUrbano", new string[0]),
        };

        static string GrupoDe(string tipo) => GruposConstruir.FirstOrDefault(g => g.tipos.Contains(tipo)).id ?? "otros";

        /// <summary>Por qué no se puede poner ahora un edificio en la cola, como el motor (`anadir_manual`), o "" si se puede. Lo que el cliente no sabe
        /// (sitio, topes de almacenes y viviendas, cola llena, recursos) lo dice el motor al pedirlo.</summary>
        string MotivoParaConstruir(Asentamiento a, string tipo, JObject def)
        {
            var unicos = (DatoDeJuego("Economia_Construccion", "EDIFICIOS_UNICOS") as JArray)?.Select(x => (string)x).ToList() ?? new List<string>();
            if (unicos.Contains(tipo) && a.Edificios.Any(e => e.Tipo == tipo)) return "Solo puede haber uno.";
            var nivel = a.NivelActual > 0 ? a.NivelActual : a.Nivel;
            var pide = (int?)def["requisitoNivelAsentamientoConstruccion"] ?? 0;
            if (nivel < pide) return $"Pide la plaza en nivel {pide}.";
            var tecnologia = (string)def["requiereTecnologia"];
            if (tecnologia != null && !(TecnologiasDeMiFaccion()?.Contains(tecnologia) ?? false)) return $"Pide la tecnología {tecnologia.Replace('_', ' ')}.";
            var n1 = def["niveles"]?["1"];
            var requiere = (string)n1?["requiereEdificio"];
            var pideNivel = (int?)n1?["requiereEdificioNivel"] ?? 1;
            if (requiere != null && !a.Edificios.Any(e => e.Tipo == requiere && Activo(e) && NivelInterno(e) >= pideNivel))
                return $"Pide un {Edificios.Nombre(requiere)} activo{(pideNivel > 1 ? $" de nivel {pideNivel}" : "")}.";
            var faccion = c.Sesion.Almacen.Buscar(TipoEntidad.Faccion, a.FaccionId)?.Faccion;
            if (tipo == "maravilla" || tipo == "estatua")
            {
                var reglas = DatoDeJuego("Diplomacia_Facciones", "PRESTIGIO");
                var prestigio = faccion?.Prestigio ?? 0;
                if (tipo == "maravilla" && (double?)reglas?["maravilla"] is double m && prestigio < m) return $"Pide {m} de prestigio de la Facción.";
                if (tipo == "estatua")
                {
                    if (faccion == null || !faccion.HasCapitalId || faccion.CapitalId != a.Id) return "Solo en la capital.";
                    var umbrales = (reglas?["estatuas"] as JArray)?.Select(x => (double)x).ToList() ?? new List<double>();
                    var caben = umbrales.Count(u => prestigio >= u);
                    if (a.Edificios.Count(e => e.Tipo == "estatua") >= caben)
                        return caben < umbrales.Count ? $"La siguiente estatua pide {umbrales[caben]} de prestigio." : "Ya tiene todas las estatuas que da el prestigio.";
                }
            }
            if ((int?)TalleresDeUnaReceta?[tipo]?["maximoPorPlaza"] is int maximo && a.Edificios.Count(e => e.Tipo == tipo) >= maximo) return $"Ya tiene las {maximo} que caben en una plaza.";
            if (tipo == "granFundicion")
            {
                if ((int?)def["nivelFaccionMinimo"] is int minimo && (faccion?.Nivel ?? 1) < minimo) return $"Pide la Facción en nivel {minimo}.";
                if (faccion != null && faccion.HasCapitalId && faccion.CapitalId != a.Id) return "Solo en la capital.";
            }
            return "";
        }

        /// <summary>El cajón de Construcción, en el sitio del riel (`htmlCajonConstruir`): «Volver», «Construidos», la auto-construcción y lo que se puede
        /// poner en la cola, por tipos.</summary>
        void CajonDeConstruccion(VisualElement riel, Asentamiento a, Heroe yo, string cargo, Action rehacer)
        {
            var cajon = Dialogo.Caja("plaza-cajon");
            cajon.name = "cajon-construir";
            riel.Add(cajon);
            var construidos = Internos(a).Where(Activo).ToList();
            if (construidos.Count > 0)
            {
                var ventana = ConstruidosVentana(construidos, rehacer);
                Ver(ventana, construidosAbiertos);
                cajon.Add(ventana);
            }
            var cab = Dialogo.Caja("plaza-cajon-cab");
            cajon.Add(cab);
            var volver = Dialogo.Boton("← Volver", "plaza-cajon-volver", () => { construidosAbiertos = false; ventanaPlaza = null; rehacer(); });
            volver.name = "cajon-volver";
            cab.Add(volver);
            if (construidos.Count > 0)
            {
                var boton = new Button(() => { construidosAbiertos = !construidosAbiertos; rehacer(); }) { name = "cajon-construidos" };
                boton.AddToClassList("plaza-cajon-volver");
                boton.AddToClassList("plaza-cajon-construidos");
                boton.EnableInClassList("activo", construidosAbiertos);
                boton.Add(IconosDelMundo.Icono("edificio", "vivienda", 16, IconosDelMundo.OroClaro));
                boton.Add(Dialogo.Texto("Construidos", "plaza-cajon-construidos-texto"));
                boton.Add(Dialogo.Texto(construidos.Count.ToString(), "plaza-cajon-construidos-n"));
                cab.Add(boton);
            }
            var titulo = Dialogo.Caja("plaza-cajon-titulo");
            titulo.Add(IconosDelMundo.Icono("riel", "construccion", 22, IconosDelMundo.OroClaro));
            titulo.Add(Dialogo.Texto("Construcción", "plaza-cajon-titulo-texto"));
            cab.Add(titulo);
            if (cargo != null)
                cab.Add(Interruptor("auto-construccion", "Auto-construcción", IconosDelMundo.Icono("glifo", "obra", 14), !a.AutoConstruccionPausada, "en marcha", "en pausa", "plaza-cajon-auto",
                    async encendido => await OrdenDePlaza(new Orden { AlternarAutoConstruccion = new OrdenAlternarAutoConstruccion { AsentamientoId = a.Id, Pausada = !encendido } })));

            var cuerpo = Dialogo.Caja("plaza-cajon-cuerpo");
            cajon.Add(cuerpo);
            if (cargo == null) return;
            var cat = Catalogo;
            var destacar = FaltaVivienda(a) ? "vivienda" : null;
            var hayGranja = a.Edificios.Any(e => e.Tipo == "granja");
            var info = (cat?.Properties() ?? Enumerable.Empty<JProperty>())
                .Where(p => p.Value is JObject d && (d["costo"] as JObject)?.Count > 0 && !p.Name.StartsWith("mina") && p.Name != "cantera" && p.Name != "corral" && !(p.Name == "granja" && hayGranja))
                .Select(p => (tipo: p.Name, def: (JObject)p.Value))
                .Where(x => MotivoParaConstruir(a, x.tipo, x.def) == "")
                .Select(x =>
                {
                    var coste = (JObject)x.def["costo"];
                    var caro = coste.Properties().Any(r => Hay(a, r.Name) < (double)r.Value);
                    return (x.tipo, x.def, coste, caro, grupo: GrupoDe(x.tipo));
                }).ToList();
            var grupos = GruposConstruir.Where(g => info.Any(x => x.grupo == g.id)).ToList();
            if (!grupos.Any(g => g.id == grupoConstruir)) grupoConstruir = grupos.FirstOrDefault().id;

            var pestanas = Dialogo.Caja("plaza-obra-grupos");
            cuerpo.Add(pestanas);
            foreach (var g in grupos)
            {
                var id = g.id;
                var listos = info.Count(x => x.grupo == id && !x.caro);
                var falta = destacar != null && GrupoDe(destacar) == id && info.Any(x => x.tipo == destacar);
                var b = new Button(() => { grupoConstruir = id; rehacer(); }) { name = "grupo-" + id };
                b.AddToClassList("plaza-obra-grupo");
                b.EnableInClassList("activo", grupoConstruir == id);
                b.EnableInClassList("destaca", falta);
                b.Add(IconosDelMundo.Icono("edificio", g.icono, 20, grupoConstruir == id ? IconosDelMundo.OroClaro : new Color(0.945f, 0.863f, 0.682f, 0.6f)));
                b.Add(Dialogo.Texto(g.nombre, "plaza-obra-grupo-nombre"));
                if (listos > 0) b.Add(Dialogo.Texto(listos.ToString(), "plaza-obra-grupo-n"));
                if (falta) b.Add(Dialogo.Texto("!", "plaza-atencion"));
                ConLetrero(b, $"{g.nombre}: {listos} para construir ya");
                pestanas.Add(b);
            }
            var lista = new ScrollView(ScrollViewMode.Vertical) { horizontalScrollerVisibility = ScrollerVisibility.Hidden };
            lista.AddToClassList("plaza-cajon-lista");
            cuerpo.Add(lista);
            var rejilla = Rejilla("plaza-construir-rejilla", 84, 6);
            lista.Add(rejilla);
            var factor = FactorDePoliticas(a, "factorTiempoConstruccion");
            foreach (var x in info.Where(x => x.grupo == grupoConstruir))
            {
                var tipo = x.tipo;
                var minutos = (double?)x.def["tiempoConstruccionMinutos"];
                var costeTexto = string.Join(", ", x.coste.Properties().Select(r => $"{Cifra((double)r.Value)} {CosasDelMapa.NombreDeRecurso(r.Name)}"));
                var texto = $"{Edificios.Nombre(tipo)}{(costeTexto != "" ? $" · {costeTexto}" : "")}{(minutos > 0 ? $" · {Duracion(Math.Round(minutos.Value * factor))}" : "")}{(x.caro ? " · No hay bastante en el almacén" : "")}";
                Button b = null;
                b = new Button(async () => await OrdenDePlaza(new Orden { AnadirEdificioManualmente = new OrdenAnadirEdificioManualmente { AsentamientoId = a.Id, Cargo = cargo, Tipo = tipo } }, b)) { name = "construir-" + tipo };
                b.AddToClassList("plaza-construir");
                b.EnableInClassList("caro", x.caro);
                b.EnableInClassList("destaca", tipo == destacar);
                if (tipo == destacar) b.Add(Dialogo.Texto("!", "plaza-atencion"));
                b.Add(IconosDelMundo.Icono("edificio", tipo, 30, IconosDelMundo.OroClaro));
                b.Add(Dialogo.Texto(Edificios.Nombre(tipo), "plaza-construir-nombre"));
                var chips = ChipsDeCoste(a, x.coste, 2);
                chips.AddToClassList("plaza-construir-coste");
                b.Add(chips);
                ConLetrero(b, texto);
                rejilla.Add(b);
            }
            if (rejilla.childCount == 0) lista.Add(Dialogo.Texto("Ahora no hay nada que se pueda construir aquí.", "plaza-vacio"));
        }

        /// <summary>La ventana «Construidos» (`htmlConstruidosVentana`): sube desde el cajón con su ancho y enseña los edificios en pie agrupados como las
        /// pestañas, un cuadrito por tipo (una plaza grande tiene cientos de viviendas). Un clic elige uno de ese tipo, con su ficha.</summary>
        VisualElement ConstruidosVentana(List<Edificio> construidos, Action rehacer)
        {
            var ventana = Dialogo.Caja("plaza-construidos");
            ventana.name = "construidos";
            var cab = Dialogo.Caja("plaza-construidos-cab");
            var titulo = Dialogo.Caja("plaza-construidos-titulo");
            titulo.Add(Dialogo.Texto("Construidos", "plaza-construidos-nombre"));
            titulo.Add(Dialogo.Texto(construidos.Count.ToString(), "plaza-construidos-n"));
            cab.Add(titulo);
            var cerrar = Dialogo.Boton("✕", "plaza-construidos-cerrar", () => { construidosAbiertos = false; rehacer(); });
            cerrar.name = "construidos-cerrar";
            cab.Add(cerrar);
            ventana.Add(cab);
            var cuerpo = new ScrollView(ScrollViewMode.Vertical) { horizontalScrollerVisibility = ScrollerVisibility.Hidden };
            cuerpo.AddToClassList("plaza-construidos-cuerpo");
            ventana.Add(cuerpo);
            var porTipo = construidos.GroupBy(e => e.Tipo).ToDictionary(g => g.Key, g => g.ToList());
            foreach (var g in GruposConstruir)
            {
                var tipos = porTipo.Keys.Where(t => GrupoDe(t) == g.id)
                    .OrderBy(t => Array.IndexOf(g.tipos, t) is int i && i >= 0 ? i : 99).ThenBy(t => Edificios.Nombre(t), StringComparer.Create(Es, false)).ToList();
                if (tipos.Count == 0) continue;
                var seccion = Dialogo.Caja("plaza-construidos-grupo");
                var h3 = Dialogo.Caja("plaza-construidos-grupo-cab");
                if (g.id != "otros") h3.Add(IconosDelMundo.Icono("edificio", g.icono, 16, IconosDelMundo.Bronce));
                h3.Add(Dialogo.Texto(g.nombre.ToUpperInvariant(), "plaza-construidos-grupo-nombre"));
                h3.Add(Dialogo.Texto(tipos.Sum(t => porTipo[t].Count).ToString(), "plaza-construidos-grupo-n"));
                seccion.Add(h3);
                var fila = Dialogo.Caja("plaza-construidos-fila");
                foreach (var t in tipos)
                {
                    var lista = porTipo[t];
                    var niveles = lista.Select(NivelInterno).ToList();
                    int max = niveles.Max(), min = niveles.Min();
                    int mejorando = lista.Count(e => e.Mejora != null), danados = lista.Count(e => e.Danado);
                    var notas = string.Join(" · ", new[] { mejorando > 0 ? $"{mejorando} mejorándose" : "", danados > 0 ? $"{danados} dañado{(danados > 1 ? "s" : "")}" : "" }.Where(s => s != ""));
                    var ids = lista.Select(e => e.Id).ToList();
                    var b = new Button(() =>
                    {
                        var clave = string.Join(" ", ids);
                        var k = siguienteConstruido.TryGetValue(clave, out var n) ? n : 0;
                        siguienteConstruido[clave] = (k + 1) % ids.Count;
                        fichaId = ids[k % ids.Count];
                        rehacer();
                    }) { name = "construido-" + t };
                    b.AddToClassList("plaza-construido");
                    b.EnableInClassList("danado", danados > 0);
                    b.EnableInClassList("mejorando", mejorando > 0);
                    b.Add(IconosDelMundo.Icono("edificio", t, 24, IconosDelMundo.OroClaro));
                    b.Add(Dialogo.Texto(Edificios.Nombre(t), "plaza-construido-nombre"));
                    if (lista.Count > 1) b.Add(Dialogo.Texto($"×{lista.Count}", "plaza-construido-n"));
                    b.Add(Dialogo.Texto(max.ToString(), "plaza-construido-nivel"));
                    ConLetrero(b, $"{Edificios.Nombre(t)}{(lista.Count > 1 ? $" ×{lista.Count}" : "")} · nivel {(min == max ? max.ToString() : $"{min} a {max}")}{(notas != "" ? $" · {notas}" : "")}");
                    fila.Add(b);
                }
                seccion.Add(fila);
                cuerpo.Add(seccion);
            }
            return ventana;
        }

        // ─────────────────────────── las ventanas ───────────────────────────

        /// <summary>La ventana abierta, junto al riel (`pintarVentanaPlaza`): la subida de nivel, Gobierno (Cargos y Políticas) o el Almacén (a pantalla
        /// completa: Almacén y «Tu carga»); sin ventana, escondida y vacía.</summary>
        void PintarVentanaDePlaza(VisualElement ventana, Asentamiento a, Heroe yo, Action rehacer)
        {
            ventana.RemoveFromClassList("plaza-ventana-completa");
            ventana.RemoveFromClassList("plaza-ventana-estrecha");
            if (letreroDePlaza != null) Ver(letreroDePlaza, false);
            if (ventanaPlaza == "ascenso" || ventanaPlaza == null || ventanaPlaza == "construccion")
            {
                if (ventanaPlaza == "ascenso") ventana.AddToClassList("plaza-ventana-estrecha");
                PintarVentanaAscenso(ventana, a, yo, rehacer);
                return;
            }
            // Al rehacerla con lo mismo abierto (otra cifra del almacén), se queda donde estaba.
            var clave0 = $"{ventanaPlaza}|{(subDePlaza.TryGetValue(ventanaPlaza, out var s0) ? s0 : "")}";
            var antes = ventana.Q<ScrollView>(className: "plaza-ventana-cuerpo");
            var desplazado = antes != null && (string)antes.userData == clave0 ? antes.scrollOffset : Vector2.zero;
            ventana.Clear();
            Ver(ventana, true);
            var completa = ventanaPlaza == "almacen" || ventanaPlaza == "armeria";
            ventana.EnableInClassList("plaza-ventana-completa", completa);
            ventana.name = "plaza-ventana";
            var cab = Dialogo.Caja("plaza-ventana-cab");
            var titulo = Dialogo.Caja("plaza-ventana-titulo");
            var (familia, icono, nombreVentana) = ventanaPlaza switch
            {
                "almacen" => ("edificio", "almacen", "Almacén"), "defensa" => ("edificio", "muralla", "Defensa"), "armeria" => ("edificio", "armeria", "Armería"),
                _ => ("riel", "gobierno", "Gobierno"),
            };
            titulo.Add(IconosDelMundo.Icono(familia, icono, 28, IconosDelMundo.Bronce));
            titulo.Add(Dialogo.Texto(nombreVentana, "plaza-ventana-nombre"));
            cab.Add(titulo);
            var cerrar = Dialogo.Boton("✕", "plaza-ventana-cerrar", () => { ventanaPlaza = null; rehacer(); });
            cerrar.name = "ventana-cerrar";
            cerrar.tooltip = "Cerrar (Esc)";
            cab.Add(cerrar);
            ventana.Add(cab);
            var subs = ventanaPlaza switch
            {
                "almacen" => new[] { ("almacen", "Almacén"), ("carga", "Tu carga") },
                "defensa" => SubsDeDefensa(a).ToArray(),
                "armeria" => PestanasDeArmeria(a, yo).ToArray(),
                _ => new[] { ("cargos", "Cargos"), ("politicas", "Políticas") },
            };
            var clave = ventanaPlaza;
            var sub = subDePlaza.TryGetValue(clave, out var s) && subs.Any(x => x.Item1 == s) ? s : subs[0].Item1;
            var cuerpo = new ScrollView(ScrollViewMode.Vertical) { horizontalScrollerVisibility = ScrollerVisibility.Hidden, userData = $"{clave}|{sub}" };
            cuerpo.AddToClassList("plaza-ventana-cuerpo");
            ventana.Add(cuerpo);
            if (desplazado != Vector2.zero && (string)cuerpo.userData == clave0)
                cuerpo.RegisterCallback<GeometryChangedEvent>(Volver);
            void Volver(GeometryChangedEvent _) { cuerpo.scrollOffset = desplazado; cuerpo.UnregisterCallback<GeometryChangedEvent>(Volver); }
            var tabs = Dialogo.Caja("plaza-subtabs");
            foreach (var (id, nombre) in subs)
            {
                var elegida = id;
                var b = Dialogo.Boton(nombre, "plaza-subtab" + (id == sub ? " activo" : ""), () => { subDePlaza[clave] = elegida; rehacer(); });
                b.name = "sub-" + id;
                tabs.Add(b);
            }
            if (subs.Length > 1) cuerpo.Add(tabs);
            switch (sub)
            {
                case "guarnicion": Guarnicion(cuerpo, a, yo, rehacer); break;
                case "muralla": Muralla(cuerpo, a, yo, rehacer); break;
                case "puerta": Puerta(cuerpo, a, yo, rehacer); break;
                case "tienda": case "forja": case "abrir": Armeria(cuerpo, a, yo, sub, rehacer); break;
                case "cargos": Cargos(cuerpo, a, yo, rehacer); break;
                case "politicas": Politicas(cuerpo, a, yo); break;
                case "almacen": VentanaAlmacen(cuerpo, a, yo, rehacer); break;
                case "carga": TuCarga(cuerpo, a, yo); break;
            }
        }

        /// <summary>Un título de sección (`.obra-titulo`): el letrero y, detrás, sus chips; con su ⓘ si lleva explicación.</summary>
        static VisualElement TituloDeSeccion(string texto, string ayuda, params VisualElement[] extras)
        {
            var fila = Dialogo.Caja("plaza-obra-titulo");
            fila.Add(Kicker(texto));
            foreach (var x in extras) if (x != null) fila.Add(x);
            if (ayuda == null) return fila;
            var caja = ConAyuda(fila, ayuda);
            caja.AddToClassList("plaza-obra-titulo-caja");
            return caja;
        }

        static Label Chip(string texto, string clases = "") => Dialogo.Texto(texto, "plaza-obra-chip " + clases);

        static VisualElement ChipConIcono(VisualElement icono, string texto, string clases = "")
        {
            var chip = Dialogo.Caja("plaza-obra-chip plaza-obra-chip-icono " + clases);
            if (icono != null) chip.Add(icono);
            chip.Add(Dialogo.Texto(texto, "plaza-obra-chip-texto"));
            return chip;
        }

        // ── Gobierno: Cargos ──

        static readonly (string cargo, string nombre, bool loNombraElRey)[] CargosDePlaza =
        {
            ("gobernador", "Gobernador", true), ("maestroObras", "Maestro de Obras", false), ("tesorero", "Tesorero", false),
            ("maestroCaravanas", "Maestro de Caravanas", false), ("general", "General", false), ("sacerdote", "Sacerdote", false),
        };

        static readonly Dictionary<string, string> NombreDeCargo = new Dictionary<string, string>
        {
            ["rey"] = "Rey", ["embajador"] = "Embajador", ["gobernador"] = "Gobernador", ["maestroObras"] = "Maestro de Obras",
            ["maestroCaravanas"] = "Maestro de Caravanas", ["tesorero"] = "Tesorero", ["general"] = "General", ["sacerdote"] = "Sacerdote",
        };

        /// <summary>El emblema de cada cargo (`CARGO_EMBLEMA`): 👑 📜 🏛️ 🔨 🐂 💰 ⚔️ 🔥. Los que no están bajados van con el más parecido de los que hay.</summary>
        static VisualElement EmblemaDeCargo(string cargo, float lado) => cargo switch
        {
            "rey" => IconosDelMundo.Icono("glifo", "nobleza", lado),
            "embajador" => IconosDelMundo.Icono("glifo", "aeda", lado),
            "gobernador" => IconosDelMundo.Icono("edificio", "palacio", lado, IconosDelMundo.OroClaro),
            "maestroObras" => IconosDelMundo.Icono("glifo", "obra", lado),
            "maestroCaravanas" => IconosDelMundo.Icono("recurso", "livestock", lado),
            "tesorero" => IconosDelMundo.Icono("glifo", "monedas", lado),
            "general" => IconosDelMundo.Icono("glifo", "atacar", lado),
            "sacerdote" => IconosDelMundo.Icono("edificio", "estatua", lado, IconosDelMundo.OroClaro),
            _ => null,
        };

        static VisualElement Emblema(string cargo)
        {
            var e = Dialogo.Caja("plaza-cargo-emblema");
            var icono = EmblemaDeCargo(cargo, 18);
            if (icono != null) e.Add(icono);
            return e;
        }

        /// <summary>Cargos (`htmlCargosPlaza`): una silla por cargo con su emblema y quien la ocupa (o «Vacante»). Quien puede nombrarlo (el Rey al
        /// Gobernador, el Gobernador al resto) ve el lápiz: abre un combo con los ciudadanos (sin los mercenarios con marca) y ✓ lo nombra. Debajo, los
        /// residentes por participación.</summary>
        void Cargos(VisualElement cuerpo, Asentamiento a, Heroe yo, Action rehacer)
        {
            var faccion = c.Sesion.Almacen.Buscar(TipoEntidad.Faccion, a.FaccionId)?.Faccion;
            var soyGobernador = TitularDe(a, "gobernador") == yo.Id;
            var soyRey = faccion != null && faccion.HasReyId && faccion.ReyId == yo.Id;
            var ciudadanos = (faccion?.CiudadanosIds ?? Enumerable.Empty<string>()).Where(id => !(faccion?.Miembros.Any(m => m.HeroeId == id && m.HasMarca) ?? false)).ToList();
            var ocupados = CargosDePlaza.Count(x => TitularDe(a, x.cargo) != null);
            cuerpo.Add(TituloDeSeccion($"Cargos de {(a.HasNombre ? a.Nombre : a.Id)}",
                "El Rey nombra al Gobernador y el Gobernador, al resto. Pulsa el lápiz (o ＋ en una vacante) para elegir a un ciudadano y ✓ para nombrarlo.",
                Chip($"{ocupados}/{CargosDePlaza.Length}", ocupados < CargosDePlaza.Length ? "activa" : "")));
            var rejilla = Rejilla("plaza-cargos", 170, 6);
            cuerpo.Add(rejilla);
            foreach (var (cargo, nombre, loNombraElRey) in CargosDePlaza)
            {
                var puede = loNombraElRey ? soyRey : soyGobernador;
                var actual = TitularDe(a, cargo);
                var editando = puede && cargosEditando.ContainsKey(cargo);
                var silla = Dialogo.Caja("plaza-cargo-silla");
                silla.name = "cargo-" + cargo;
                silla.EnableInClassList("vacante", actual == null);
                silla.EnableInClassList("yo", actual == yo.Id);
                silla.EnableInClassList("editando", editando);
                silla.Add(Emblema(cargo));
                var centro = Dialogo.Caja("plaza-cargo-cuerpo");
                centro.Add(Dialogo.Texto(nombre.ToUpperInvariant(), "plaza-cargo-nombre"));
                DropdownField combo = null;
                if (editando)
                {
                    var nombres = ciudadanos.Select(id => Textos.NombreDeHeroe(c.Sesion.Almacen, id)).ToList();
                    var elegido = cargosEditando[cargo] ?? actual;
                    combo = new DropdownField(nombres, Math.Max(0, ciudadanos.IndexOf(elegido))) { name = "cargo-combo" };
                    combo.AddToClassList("plaza-cargo-combo");
                    combo.RegisterValueChangedCallback(_ => { if (combo.index >= 0) cargosEditando[cargo] = ciudadanos[combo.index]; });
                    centro.Add(combo);
                }
                else centro.Add(Dialogo.Texto(actual != null ? Textos.NombreDeHeroe(c.Sesion.Almacen, actual) : "Vacante", "plaza-cargo-titular" + (actual == null ? " vacante" : "")));
                silla.Add(centro);
                if (puede)
                {
                    var botones = Dialogo.Caja("plaza-cargo-botones");
                    if (editando)
                    {
                        Button ok = null;
                        ok = Dialogo.Boton("✓", "plaza-cargo-boton ok", async () =>
                        {
                            if (combo == null || combo.index < 0 || combo.index >= ciudadanos.Count) return;
                            if (await OrdenDePlaza(new Orden { AsignarCargoLocal = new OrdenAsignarCargoLocal { AsentamientoId = a.Id, Cargo = cargo, HeroeId = ciudadanos[combo.index] } }, ok))
                            {
                                cargosEditando.Remove(cargo);
                                rehacer();
                            }
                        });
                        ok.name = "cargo-asignar";
                        botones.Add(ok);
                        var cancelar = Dialogo.Boton("✕", "plaza-cargo-boton", () => { cargosEditando.Remove(cargo); rehacer(); });
                        cancelar.name = "cargo-cancelar";
                        botones.Add(cancelar);
                    }
                    else
                    {
                        var lapiz = Dialogo.Boton(actual != null ? "✎" : "＋", "plaza-cargo-boton", () => { cargosEditando[cargo] = actual; rehacer(); });
                        lapiz.name = "cargo-cambiar";
                        botones.Add(lapiz);
                    }
                    silla.Add(botones);
                }
                rejilla.Add(silla);
            }
            if (!soyGobernador && !soyRey) cuerpo.Add(Dialogo.Texto("Solo el Rey (al Gobernador) y el Gobernador (al resto) pueden cambiar los cargos.", "plaza-nota"));
            Residentes(cuerpo, a, faccion, yo);
        }

        /// <summary>Los cargos de un héroe en la plaza (los locales) y en su Facción (Rey, Embajador).</summary>
        static List<string> CargosEnPlaza(Asentamiento a, Faccion f, string heroeId)
        {
            var lista = new List<string>();
            if (f != null && f.HasReyId && f.ReyId == heroeId) lista.Add("rey");
            if (f != null && f.HasEmbajadorId && f.EmbajadorId == heroeId) lista.Add("embajador");
            foreach (var k in new[] { "gobernador", "maestroObras", "tesorero", "maestroCaravanas", "general", "sacerdote" })
                if (TitularDe(a, k) == heroeId) lista.Add(k);
            return lista;
        }

        /// <summary>Residentes de la plaza como un ranking (`htmlResidentesPlaza`): puesto (medalla a los tres primeros), nombre con los emblemas de sus
        /// cargos, barra de sus puntos contra los del primero y la cifra. Debajo, en otro tono, quien ha sumado puntos aquí sin residir.</summary>
        void Residentes(VisualElement cuerpo, Asentamiento a, Faccion f, Heroe yo)
        {
            var puntos = a.Participacion;
            var residentes = new HashSet<string>(a.FundadoresIds.Concat(a.CasasCompradas)
                .Concat((f?.Miembros ?? Enumerable.Empty<MiembroFaccion>()).Where(m => m.HasResidenciaId && m.ResidenciaId == a.Id).Select(m => m.HeroeId)));
            ulong Pts(string id) => puntos.TryGetValue(id, out var n) ? n : 0;
            string Nombre(string id) => Textos.NombreDeHeroe(c.Sesion.Almacen, id);
            List<string> Orden(IEnumerable<string> ids) => ids.OrderByDescending(Pts).ThenBy(Nombre, StringComparer.Create(Es, false)).ToList();
            var maximo = Math.Max(1UL, puntos.Values.DefaultIfEmpty(0UL).Max());
            var seccion = Dialogo.Caja("plaza-participacion");
            seccion.name = "residentes";
            seccion.Add(TituloDeSeccion("Residentes y participación",
                "Puntos que gana cada héroe en esta plaza: al defenderla en un asedio, al defender sus pueblos, aldeas o caravanas, y al ganar una batalla residiendo aquí. Lo ganado en una plaza se queda en ella. En otro tono, quien ha sumado aquí sin residir.",
                ChipConIcono(Glifo("casa", 12), residentes.Count.ToString())));
            VisualElement Fila(string id, int i, bool visita)
            {
                var fila = Dialogo.Caja("plaza-ranking-fila");
                fila.EnableInClassList("yo", id == yo.Id);
                fila.EnableInClassList("visita", visita);
                var pts = Pts(id);
                var medalla = !visita && pts > 0 && i < 3;
                var puesto = Dialogo.Caja("plaza-ranking-puesto" + (medalla ? " medalla medalla-" + (i + 1) : ""));
                if (visita) puesto.Add(Glifo("campamento", 14));
                else puesto.Add(Dialogo.Texto((i + 1).ToString(), "plaza-ranking-puesto-n"));
                fila.Add(puesto);
                var nombre = Dialogo.Caja("plaza-ranking-nombre");
                nombre.Add(Dialogo.Texto(Nombre(id), "plaza-ranking-nombre-texto"));
                var cargos = CargosEnPlaza(a, f, id);
                foreach (var k in cargos) if (EmblemaDeCargo(k, 13) is VisualElement e) nombre.Add(e);
                fila.Add(nombre);
                var barra = Dialogo.Caja("plaza-ranking-barra");
                var relleno = Dialogo.Caja("plaza-ranking-barra-i");
                relleno.style.width = Length.Percent((float)pts / maximo * 100);
                barra.Add(relleno);
                fila.Add(barra);
                fila.Add(Dialogo.Texto(pts.ToString(), "plaza-ranking-puntos"));
                ConLetrero(fila, $"{Nombre(id)}{(cargos.Count > 0 ? " · " + string.Join(", ", cargos.Select(k => NombreDeCargo[k])) : "")} · {pts} puntos");
                return fila;
            }
            if (residentes.Count > 0)
            {
                var lista = Dialogo.Caja("plaza-ranking");
                var orden = Orden(residentes);
                for (int i = 0; i < orden.Count; i++) lista.Add(Fila(orden[i], i, false));
                seccion.Add(lista);
            }
            else seccion.Add(Dialogo.Texto("Nadie reside aquí.", "plaza-vacio"));
            var visitantes = Orden(puntos.Keys.Where(id => !residentes.Contains(id) && Pts(id) > 0));
            if (visitantes.Count > 0)
            {
                var lista = Dialogo.Caja("plaza-ranking");
                for (int i = 0; i < visitantes.Count; i++) lista.Add(Fila(visitantes[i], i, true));
                seccion.Add(lista);
            }
            cuerpo.Add(seccion);
        }

        // ── Gobierno: Políticas ──

        static readonly (string cargo, string nombre)[] CargosConPoliticas =
        {
            ("gobernador", "Gobernador"), ("tesorero", "Tesorero"), ("maestroObras", "Maestro de Obras"), ("general", "General"), ("sacerdote", "Sacerdote"),
        };

        /// <summary>Cómo se dice cada efecto del catálogo: su icono, su nombre y si bajarlo es bueno (tiempos, consumos, costes y comisiones).</summary>
        static readonly Dictionary<string, (string familia, string icono, string nombre, bool bajarEsBueno)> EfectosDePolitica = new Dictionary<string, (string, string, string, bool)>
        {
            ["factorConsumoComida"] = ("recurso", "pan", "consumo de comida", true),
            ["factorCrecimientoNobleza"] = ("glifo", "nobleza", "crecimiento de la nobleza", false),
            ["factorTiempoConstruccion"] = ("glifo", "obra", "tiempo de obra", true),
            ["factorComisionExterna"] = ("recurso", "oro", "comisión del comercio exterior", true),
            ["factorCostoReclutamiento"] = ("glifo", "atacar", "coste de reclutar", true),
            ["factorProduccionTrigo"] = ("glifo", "viveres", "producción de trigo", false),
            ["factorCapacidadCaravana"] = ("recurso", "_otro", "capacidad de las caravanas", false),
            ["factorVelocidadCaravana"] = ("recurso", "livestock", "velocidad de las caravanas", false),
            ["factorRecaudacion"] = ("glifo", "monedas", "recaudación de oro", false),
            ["factorCrecimientoPoblacion"] = ("glifo", "gente", "crecimiento de la población", false),
            ["factorTiempoMuralla"] = ("edificio", "muralla", "tiempo de muralla", true),
            ["factorProduccionTalleres"] = ("glifo", "obra", "producción de los talleres", false),
        };
        static readonly Dictionary<string, (string familia, string icono, string nombre)> SumasDePolitica = new Dictionary<string, (string, string, string)>
        {
            ["cupoCaravanaExtra"] = ("recurso", "livestock", "caravana de flota"),
            ["cupoGuarnicionExtra"] = ("glifo", "escudo", "de guarnición por héroe"),
        };

        static VisualElement IconoDe(string familia, string id, float lado) =>
            IconosDelMundo.Icono(familia, id, lado, familia == "edificio" || familia == "simbolo" ? IconosDelMundo.OroClaro : (Color?)null);

        /// <summary>Los efectos de una política como chips: icono y porcentaje, en verde lo que favorece a la plaza y en rojo lo que la castiga.</summary>
        static VisualElement EfectosDe(JObject def)
        {
            var fila = Dialogo.Caja("plaza-politica-efectos");
            foreach (var p in def.Properties())
            {
                VisualElement chip = null;
                if (EfectosDePolitica.TryGetValue(p.Name, out var ef) && p.Value.Type is JTokenType.Float or JTokenType.Integer)
                {
                    var v = (double)p.Value;
                    var pct = (int)Math.Round((v - 1) * 100);
                    var bueno = ef.bajarEsBueno ? v < 1 : v > 1;
                    chip = Dialogo.Caja("plaza-efecto " + (bueno ? "bueno" : "malo"));
                    chip.Add(IconoDe(ef.familia, ef.icono, 11));
                    chip.Add(Dialogo.Texto($"{(pct > 0 ? "+" : "−")}{Math.Abs(pct)}%", "plaza-efecto-texto"));
                    chip.tooltip = $"{ef.nombre} ×{v}";
                }
                else if (SumasDePolitica.TryGetValue(p.Name, out var suma) && p.Value.Type is JTokenType.Float or JTokenType.Integer)
                {
                    chip = Dialogo.Caja("plaza-efecto bueno");
                    chip.Add(IconoDe(suma.familia, suma.icono, 11));
                    chip.Add(Dialogo.Texto($"+{p.Value}", "plaza-efecto-texto"));
                    chip.tooltip = $"+{p.Value} {suma.nombre}";
                }
                else if (p.Name == "perfilTrazado")
                {
                    chip = Dialogo.Caja("plaza-efecto neutro");
                    chip.Add(IconoDe("simbolo", "bandera", 11));
                    chip.Add(Dialogo.Texto((string)p.Value, "plaza-efecto-texto"));
                    chip.tooltip = $"El trazado de la ciudad tiende a «{p.Value}»";
                }
                if (chip != null) fila.Add(chip);
            }
            return fila;
        }

        /// <summary>Políticas (`htmlPoliticas`): cada cargo con su emblema, sus slots en casillas, lo que tiene en vigor con la barra de lo que le queda y, si
        /// es el tuyo, las que puede activar como cartas con sus efectos en chips. El Gobernador, las de cualquier pool: las de los demás, plegadas.</summary>
        void Politicas(VisualElement cuerpo, Asentamiento a, Heroe yo)
        {
            var reside = Reside(a, yo);
            var presente = Presente(a, yo);
            var puede = reside && presente;
            var motivo = !reside ? "Solo quien reside en esta plaza" : !presente ? "Tienes que estar en la plaza" : "";
            var catalogo = (DatoDeJuego("Economia_Construccion", "POLITICA_CATALOGO") as JArray)?.OfType<JObject>().ToList();
            var reglas = DatoDeJuego("Economia_Construccion", "POLITICAS");
            if (catalogo == null || reglas == null) { cuerpo.Add(Dialogo.Texto("Cargando el catálogo de políticas…", "plaza-nota")); return; }
            var faccion = c.Sesion.Almacen.Buscar(TipoEntidad.Faccion, a.FaccionId)?.Faccion;
            var nivelFaccion = faccion?.Nivel ?? 1;
            var sala = Internos(a).Any(e => e.Tipo == "salaConsejo" && Activo(e));
            int Slots(string cargo)
            {
                var cfg = reglas["slotsPorCargo"]?[cargo];
                if (cfg == null) return 0;
                var baseSlots = (int?)cfg["base"] ?? 0;
                if (cargo != "gobernador") return baseSlots;
                var porNivel = Math.Max(1, (int?)reglas["nivelFaccionPorSlotExtraGobernador"] ?? 1);
                return Math.Min((int?)cfg["maximo"] ?? baseSlots, baseSlots + (int)(nivelFaccion / porNivel)) + (sala ? (int?)reglas["slotSalaConsejo"] ?? 0 : 0);
            }
            var duracion = (double?)reglas["duracionMinutosPorDefecto"] ?? 0;
            cuerpo.Add(ConAyuda(Dialogo.Texto("Cada cargo, con sus políticas en vigor y las que puede activar.", "plaza-nota"),
                $"Cada cargo activa las políticas de su pool (el Gobernador, cualquiera). Duran {Duracion(duracion)} y no se pueden cancelar antes; ocupan un slot del cargo mientras estén en vigor. No tienen coste."));
            foreach (var (cargo, nombre) in CargosConPoliticas)
            {
                var titular = TitularDe(a, cargo);
                var mio = titular == yo.Id;
                var activas = a.PoliticasActivas.Where(x => x.Cargo == cargo).ToList();
                var limite = Slots(cargo);
                var lleno = activas.Count >= limite;
                var caja = Dialogo.Caja("plaza-consejo-cargo");
                caja.name = "consejo-" + cargo;
                caja.EnableInClassList("mio", mio);
                caja.EnableInClassList("vacante", titular == null);
                var cab = Dialogo.Caja("plaza-consejo-cab");
                cab.Add(Emblema(cargo));
                var tit = Dialogo.Caja("plaza-consejo-titulo");
                tit.Add(Dialogo.Texto(nombre, "plaza-consejo-nombre"));
                tit.Add(Dialogo.Texto(titular != null ? Textos.NombreDeHeroe(c.Sesion.Almacen, titular) : "vacante: sin titular no se activa ninguna", "plaza-consejo-titular"));
                cab.Add(tit);
                var pips = Dialogo.Caja("plaza-consejo-slots");
                for (int i = 0; i < Math.Max(limite, activas.Count); i++) pips.Add(Dialogo.Caja("plaza-consejo-slot" + (i < activas.Count ? " usado" : "")));
                ConLetrero(pips, $"Slots: {activas.Count} de {limite} en uso");
                cab.Add(pips);
                caja.Add(cab);
                if (activas.Count > 0)
                {
                    var cartas = Rejilla("plaza-politica-cartas", 150, 5);
                    foreach (var x in activas)
                    {
                        var def = catalogo.FirstOrDefault(d => (string)d["id"] == x.PoliticaId);
                        var carta = Dialogo.Caja("plaza-politica-carta activa");
                        carta.name = "politica-" + x.PoliticaId;
                        var n = Dialogo.Caja("plaza-politica-nombre");
                        n.Add(Dialogo.Texto((string)def?["nombre"] ?? x.PoliticaId, "plaza-politica-nombre-texto"));
                        var queda = Dialogo.Texto("", "plaza-politica-queda");
                        n.Add(queda);
                        carta.Add(n);
                        if (def != null) carta.Add(EfectosDe(def));
                        var barra = BarraAvance(0);
                        barra.AddToClassList("plaza-politica-barra");
                        carta.Add(barra);
                        var dura = Math.Max(1, x.ExpiraEnMs - x.ActivadaEnMs);
                        var activa = x;
                        void Reloj()
                        {
                            queda.text = CuentaAtras(activa.ExpiraEnMs);
                            Avance(barra, Mathf.Clamp01((float)((activa.ExpiraEnMs - AhoraMs) / dura)));
                        }
                        Reloj();
                        relojesDePlaza.Add(("ventana", Reloj));
                        cartas.Add(carta);
                    }
                    caja.Add(cartas);
                }
                if (titular != null && mio)
                {
                    var pool = catalogo.Where(d => cargo == "gobernador" || (string)d["cargo"] == cargo).Where(d => !activas.Any(x => x.PoliticaId == (string)d["id"])).ToList();
                    VisualElement Carta(JObject d)
                    {
                        var carta = Dialogo.Caja("plaza-politica-carta");
                        carta.name = "politica-" + (string)d["id"];
                        var n = Dialogo.Caja("plaza-politica-nombre");
                        n.Add(Dialogo.Texto((string)d["nombre"], "plaza-politica-nombre-texto"));
                        if (cargo == "gobernador" && (string)d["cargo"] != "gobernador" && EmblemaDeCargo((string)d["cargo"], 12) is VisualElement deOtro) n.Add(deOtro);
                        carta.Add(n);
                        carta.Add(EfectosDe(d));
                        var bloqueo = !puede ? motivo : lleno ? "Sin slots libres" : "";
                        Button activar = null;
                        activar = Dialogo.Boton("▶ Activar", "plaza-politica-activar", async () =>
                            await OrdenDePlaza(new Orden { ActivarPolitica = new OrdenActivarPolitica { AsentamientoId = a.Id, Cargo = cargo, PoliticaId = (string)d["id"] } }, activar));
                        activar.name = "activar";
                        if (bloqueo != "") { activar.SetEnabled(false); ConLetrero(activar, bloqueo); }
                        carta.Add(activar);
                        return carta;
                    }
                    if (!puede) caja.Add(Dialogo.Texto($"{motivo} para activar políticas.", "plaza-nota"));
                    var propias = pool.Where(d => (string)d["cargo"] == cargo).ToList();
                    var ajenas = pool.Where(d => (string)d["cargo"] != cargo).ToList();
                    if (propias.Count > 0)
                    {
                        var cartas = Rejilla("plaza-politica-cartas", 150, 5);
                        foreach (var d in propias) cartas.Add(Carta(d));
                        caja.Add(cartas);
                    }
                    if (ajenas.Count > 0)
                    {
                        var plegable = new Foldout { text = $"De los otros cargos · {ajenas.Count}", value = false, name = "pool-ajeno" };
                        plegable.AddToClassList("plaza-plegable");
                        var cartas = Rejilla("plaza-politica-cartas", 150, 5);
                        foreach (var d in ajenas) cartas.Add(Carta(d));
                        plegable.Add(cartas);
                        caja.Add(plegable);
                    }
                }
                cuerpo.Add(caja);
            }
        }

        // ── el Almacén ──

        /// <summary>El orden de los recursos (el de la paleta del prototipo: los básicos primero).</summary>
        static List<string> OrdenDeRecursos => (IconosDelMundo.Lista["recurso"] as JObject)?.Properties().Select(p => p.Name).ToList() ?? new List<string>();

        static int PosDeRecurso(List<string> orden, string r) { var i = orden.IndexOf(r); return i < 0 ? 99 : i; }

        /// <summary>La elección de la barra de arriba que guardó el jugador (la misma en todas sus plazas), o null si va sola.</summary>
        static List<string> EleccionDeBarra()
        {
            if (!PlayerPrefs.HasKey(ClaveBarra)) return null;
            var conocidos = OrdenDeRecursos;
            return PlayerPrefs.GetString(ClaveBarra).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Where(conocidos.Contains).Take(MaxBarraAlmacen).ToList();
        }

        static void GuardarBarra(List<string> lista)
        {
            if (lista == null) PlayerPrefs.DeleteKey(ClaveBarra);
            else PlayerPrefs.SetString(ClaveBarra, string.Join(",", lista));
            PlayerPrefs.Save();
        }

        /// <summary>Los recursos de la barra de esta plaza: los que eligió el jugador o, si no eligió, los de la barra automática.</summary>
        List<string> RecursosDeLaBarra(Asentamiento a)
        {
            var orden = OrdenDeRecursos;
            return EleccionDeBarra() ?? BarraAutomatica(a, a.Almacen.Keys.Where(r => Hay(a, r) >= 1).OrderBy(r => PosDeRecurso(orden, r)).ThenBy(r => r, StringComparer.Ordinal).ToList());
        }

        /// <summary>Lo que entra y sale por minuto de un recurso (`flujosDe`): con el almacén lleno, el neto es el balance de lo que rinden los edificios
        /// menos lo que se consume.</summary>
        (double produce, double consume, double neto, bool lleno) Flujos(Existencia x)
        {
            var produce = (x.ProduccionPorMinuto != 0 ? x.ProduccionPorMinuto : x.ProducidoPorMinuto) / 1000.0;
            var consume = x.ConsumidoPorMinuto / 1000.0;
            var lleno = x.Capacidad > 0 && Valor(x.Cantidad) >= x.Capacidad / 1000.0;
            return (produce, consume, lleno ? produce - consume : (x.Cantidad?.PorMinuto ?? 0) / 1000.0, lleno);
        }

        /// <summary>La ventana del Almacén, a pantalla completa: a la izquierda qué va en la barra de arriba, el almacén con su neto por minuto, la
        /// extracción, la reserva del Tesorero y pausar el consumo; a la derecha, los talleres.</summary>
        void VentanaAlmacen(VisualElement cuerpo, Asentamiento a, Heroe yo, Action rehacer)
        {
            var cols = Dialogo.Caja("plaza-cols");
            var izq = Dialogo.Caja("plaza-col");
            var der = Dialogo.Caja("plaza-col");
            DosColumnas(cols, izq, der, 1000, 22);
            cuerpo.Add(cols);
            EleccionDeLaBarra(izq, a, rehacer);
            Extraccion(izq, a);
            Reserva(izq, a, yo, rehacer);
            PausarConsumo(izq, a, yo);
            Talleres(der, a, yo);
        }

        /// <summary>En la barra de arriba (`htmlEleccionBarra`): los recursos de la plaza, para elegir cuáles van en la barra. De serie, los que tiene y los
        /// que ya van en ella; «Mostrar todos» enseña el resto. «Que vaya sola» borra la elección.</summary>
        void EleccionDeLaBarra(VisualElement col, Asentamiento a, Action rehacer)
        {
            var lista = RecursosDeLaBarra(a);
            var sola = EleccionDeBarra() == null;
            var orden = OrdenDeRecursos;
            var conAlgo = a.Almacen.Where(kv => Hay(a, kv.Key) >= 1).Select(kv => kv.Key).ToList();
            var todos = a.Almacen.Where(kv => Hay(a, kv.Key) >= 1 || kv.Value.Capacidad > 0).Select(kv => kv.Key).Concat(lista).Distinct().ToList();
            var visibles = new HashSet<string>(conAlgo.Concat(lista));
            var ocultos = todos.Count(r => !visibles.Contains(r));
            var recursos = (eleccionConTodos ? todos : todos.Where(visibles.Contains)).OrderBy(r => PosDeRecurso(orden, r)).ThenBy(r => r, StringComparer.Ordinal).ToList();
            var lleno = lista.Count >= MaxBarraAlmacen;
            var extras = new List<VisualElement> { Chip($"{lista.Count}/{MaxBarraAlmacen}", lleno ? "activa" : "") };
            if (sola) extras.Add(Chip("Automática"));
            else
            {
                var b = Dialogo.Boton("Que vaya sola", "boton-secundario plaza-barra-sola", () => { GuardarBarra(null); rehacer(); });
                b.name = "barra-sola";
                extras.Add(b);
            }
            if (ocultos > 0 || eleccionConTodos)
            {
                var b = Dialogo.Boton(eleccionConTodos ? "Solo los que hay" : "Mostrar todos", "boton-secundario plaza-barra-todos", () => { eleccionConTodos = !eleccionConTodos; rehacer(); });
                b.name = "barra-todos";
                extras.Add(b);
            }
            var seccion = Dialogo.Caja("plaza-eleccion");
            seccion.name = "eleccion-barra";
            seccion.Add(TituloDeSeccion("En la barra de arriba", null, extras.ToArray()));
            seccion.Add(Dialogo.Texto(sola
                ? $"Ahora va sola: enseña lo que tiene la plaza, hasta {MaxBarraAlmacen}, y lo nuevo entra si queda sitio. Si pones o quitas uno, manda tu elección."
                : $"La has elegido tú (hasta {MaxBarraAlmacen}, la misma en todas tus plazas). Se guarda en este equipo.", "plaza-nota"));
            var rejilla = Rejilla("plaza-barra-elecciones", 175, 6);
            foreach (var r in recursos)
            {
                var recurso = r;
                var dentro = lista.Contains(r);
                var titulo = dentro ? "Está en la barra: clic para quitarlo" : lleno ? $"La barra ya lleva {MaxBarraAlmacen}: quita uno antes" : "Clic para ponerlo en la barra";
                var b = new Button(() =>
                {
                    var ahora = RecursosDeLaBarra(a);
                    if (ahora.Contains(recurso)) GuardarBarra(ahora.Where(x => x != recurso).ToList());
                    else if (ahora.Count >= MaxBarraAlmacen) { panelAvisos.Mostrar($"La barra ya lleva {MaxBarraAlmacen} recursos: quita uno antes de poner otro.", true); return; }
                    else GuardarBarra(ahora.Concat(new[] { recurso }).ToList());
                    rehacer();
                }) { name = "barra-" + r };
                b.AddToClassList("plaza-barra-eleccion");
                b.EnableInClassList("elegido", dentro);
                b.EnableInClassList("sin-sitio", !dentro && lleno);
                b.Add(IconosDelMundo.Icono("recurso", r, 16));
                b.Add(Dialogo.Texto(CosasDelMapa.NombreDeRecurso(r), "plaza-barra-eleccion-nombre"));
                b.Add(Dialogo.Texto(Miles(Hay(a, r)), "plaza-barra-eleccion-n"));
                ConLetrero(b, $"{CosasDelMapa.NombreDeRecurso(r)}: {titulo}");
                rejilla.Add(b);
            }
            seccion.Add(rejilla);
            if (recursos.Count == 0) seccion.Add(Dialogo.Texto("El almacén está vacío.", "plaza-vacio"));
            col.Add(seccion);
        }

        /// <summary>Qué produce un edificio sin receta y cuánto de base (`produccionBase`): `produccionBaseTrigo` es trigo; con sus trabajadores.</summary>
        (string recurso, double base_, double trabajadores)? ProduccionBase(string tipo, int nivel)
        {
            var cat = Catalogo?[tipo] as JObject;
            if (cat == null) return null;
            var delNivel = cat["niveles"]?[nivel.ToString()] as JObject ?? new JObject();
            foreach (var fuente in new[] { delNivel, cat })
            {
                var clave = fuente.Properties().FirstOrDefault(p => p.Name.StartsWith("produccionBase") && (p.Value.Type == JTokenType.Float || p.Value.Type == JTokenType.Integer));
                if (clave == null) continue;
                var r = clave.Name.Substring("produccionBase".Length);
                var recurso = r == "Livestock" ? "livestock" : char.ToLowerInvariant(r[0]) + r.Substring(1);
                return (recurso, (double)clave.Value, (double?)delNivel["trabajadoresRequeridos"] ?? (double?)cat["trabajadoresRequeridos"] ?? 0);
            }
            return null;
        }

        /// <summary>El almacén (`htmlExtraccion`): una ficha por recurso con el neto del último minuto, lo que hay frente a lo que cabe y lo que entra
        /// frente a lo que sale; y la extracción: la mano de obra en un medidor y los extractores como cadena edificio ➜ recurso.</summary>
        void Extraccion(VisualElement col, Asentamiento a)
        {
            var orden = OrdenDeRecursos;
            var seccion = Dialogo.Caja("plaza-extraccion");
            seccion.name = "extraccion";
            seccion.Add(TituloDeSeccion("Almacén", "Cada ficha: el neto del último minuto (▲ sube, ▼ baja), lo que hay frente a lo que cabe y, en la tira de abajo, lo que entra (verde) frente a lo que sale (rojo). Entra lo de edificios, pueblos y recaudación; sale la comida de la población y la guarnición, los insumos de los talleres y el mantenimiento. Órdenes, comercio, botín y caravanas no cuentan. LLENO: lo que se produce de más se pierde, y la flecha da el balance de lo que rinden los edificios menos lo que se consume. Pasa el ratón para las cifras.",
                Dialogo.Texto("por minuto", "plaza-obra-titulo-small")));
            var recursos = a.Almacen.Where(kv => Valor(kv.Value.Cantidad) >= 1 || (kv.Value.Cantidad?.PorMinuto ?? 0) != 0 || kv.Value.ProduccionPorMinuto != 0 || kv.Value.ProducidoPorMinuto != 0 || kv.Value.ConsumidoPorMinuto != 0)
                .OrderBy(kv => PosDeRecurso(orden, kv.Key)).ThenBy(kv => kv.Key, StringComparer.Ordinal).ToList();
            if (recursos.Count > 0)
            {
                var fichas = Rejilla("plaza-prod-fichas", 92, 6);
                foreach (var kv in recursos)
                {
                    var r = kv.Key;
                    var x = kv.Value;
                    var (p, cs, n, lleno) = Flujos(x);
                    var cantidad = Valor(x.Cantidad);
                    var capacidad = x.Capacidad / 1000.0;
                    var minutos = n > 0 && capacidad > 0 && !lleno ? (capacidad - cantidad) / n : n < 0 && cantidad > 0 ? cantidad / -n : (double?)null;
                    var ficha = Dialogo.Caja("plaza-prod-ficha" + (lleno ? " lleno" : "") + (n < 0 ? " baja" : n > 0 ? " sube" : ""));
                    ficha.name = "ficha-" + r;
                    var arriba = Dialogo.Caja("plaza-prod-ficha-arriba");
                    arriba.Add(IconosDelMundo.Icono("recurso", r, 22));
                    var datos = Dialogo.Caja("plaza-prod-ficha-datos");
                    datos.Add(Dialogo.Texto($"{(n > 0 ? "▲" : n < 0 ? "▼" : "■")} {ConSigno(n)}", "plaza-prod-ficha-neto " + (n > 0 ? "pos" : n < 0 ? "neg" : "cero")));
                    var cifra = Dialogo.Caja("plaza-prod-ficha-cifra");
                    cifra.Add(Dialogo.Texto(Math.Floor(cantidad).ToString(Es), "plaza-prod-ficha-cantidad"));
                    if (lleno) cifra.Add(Dialogo.Texto("LLENO", "plaza-prod-ficha-aviso lleno"));
                    else if (minutos != null && n < 0 && minutos < 60) cifra.Add(Dialogo.Texto($"⧗{Duracion(Math.Max(1, Math.Round(minutos.Value)))}", "plaza-prod-ficha-aviso baja"));
                    else cifra.Add(Dialogo.Texto($"/{Math.Floor(capacidad).ToString(Es)}", "plaza-prod-ficha-cap"));
                    datos.Add(cifra);
                    arriba.Add(datos);
                    ficha.Add(arriba);
                    var stock = Dialogo.Caja("plaza-prod-stock");
                    var relleno = Dialogo.Caja("plaza-prod-stock-i");
                    relleno.style.width = Length.Percent(capacidad > 0 ? (float)Math.Min(1, cantidad / capacidad) * 100 : 0);
                    stock.Add(relleno);
                    ficha.Add(stock);
                    var flujo = Dialogo.Caja("plaza-prod-flujo" + (p + cs > 0 ? "" : " quieto"));
                    if (p + cs > 0)
                    {
                        var entra = Dialogo.Caja("plaza-prod-flujo-entra");
                        entra.style.width = Length.Percent((float)(p / (p + cs) * 100));
                        flujo.Add(entra);
                        flujo.Add(Dialogo.Caja("plaza-prod-flujo-sale"));
                    }
                    ficha.Add(flujo);
                    var cuando = minutos == null ? "" : $"{(n > 0 ? "lleno" : "se acaba")} en {Duracion(Math.Max(1, Math.Round(minutos.Value)))}";
                    ConLetrero(ficha, $"{CosasDelMapa.NombreDeRecurso(r)}: {Math.Floor(cantidad)} de {Math.Floor(capacidad)}{(p != 0 || cs != 0 ? $" · por minuto produce {Cifra(p)}, consume {Cifra(cs)}" : "")} · {(lleno ? "balance" : "neto")} {ConSigno(n)}/min{(lleno ? " · almacén lleno: lo que se produce de más se pierde" : cuando != "" ? $" · {cuando}" : "")}");
                    fichas.Add(ficha);
                }
                seccion.Add(fichas);
            }
            else seccion.Add(Dialogo.Texto("El almacén está vacío.", "plaza-vacio"));

            // La extracción: la mano de obra en un medidor y los extractores y granjas activos, por tipo.
            var porTipo = new Dictionary<string, (string recurso, double base_, double trabajadores, List<Edificio> edificios)>();
            foreach (var e in Internos(a).Where(Activo))
            {
                var pb = ProduccionBase(e.Tipo, NivelInterno(e));
                if (pb == null) continue;
                var (recurso, b, t) = pb.Value;
                (string recurso, double base_, double trabajadores, List<Edificio> edificios) g = porTipo.TryGetValue(e.Tipo, out var antes) ? antes : (recurso, 0.0, 0.0, new List<Edificio>());
                g.edificios.Add(e);
                porTipo[e.Tipo] = (g.recurso, g.base_ + b, g.trabajadores + t, g.edificios);
            }
            var necesarios = porTipo.Values.Sum(g => g.trabajadores);
            var pesants = Math.Floor(Valor(a.Poblacion?.Pesants));
            var ratio = necesarios <= 0 ? 1 : Math.Min(1, pesants / necesarios);
            var obra = Dialogo.Caja("plaza-prod-obra" + (ratio < 1 ? " falta" : ""));
            obra.name = "mano-de-obra";
            obra.Add(Glifo("gente", 14));
            var barraObra = Dialogo.Caja("plaza-prod-obra-barra");
            var iObra = Dialogo.Caja("plaza-prod-obra-barra-i");
            iObra.style.width = Length.Percent((float)ratio * 100);
            barraObra.Add(iObra);
            obra.Add(barraObra);
            obra.Add(Dialogo.Texto($"{pesants}/{necesarios}", "plaza-prod-obra-cifra"));
            obra.Add(Dialogo.Texto(ratio < 1 ? $"{Math.Round(ratio * 100)} %" : "✔", "plaza-prod-obra-pct"));
            ConLetrero(obra, $"Mano de obra: {pesants} pesants para {necesarios} puestos de extractores y granjas{(ratio < 1 ? $" ({Math.Round(ratio * 100)} %: rinden menos)" : "")}.");
            seccion.Add(TituloDeSeccion("Extracción", null, obra));
            var extractores = Dialogo.Caja("plaza-prod-extractores");
            foreach (var kv in porTipo.OrderBy(kv => Edificios.Nombre(kv.Key), StringComparer.Create(Es, false)))
            {
                var (recurso, b, t, edificios) = kv.Value;
                int parados = edificios.Count(e => e.PausadoPorAlmacenLleno), danados = edificios.Count(e => e.Danado);
                var ex = Dialogo.Caja("plaza-prod-extractor" + (parados > 0 ? " parado" : ""));
                ex.name = "extractor-" + kv.Key;
                ex.Add(IconosDelMundo.Icono("edificio", kv.Key, 24, IconosDelMundo.OroClaro));
                if (edificios.Count > 1) ex.Add(Dialogo.Texto($"×{edificios.Count}", "plaza-prod-taller-n"));
                ex.Add(Dialogo.Texto("➜", "plaza-prod-flecha"));
                var producto = Dialogo.Caja("plaza-prod-producto");
                producto.Add(IconosDelMundo.Icono("recurso", recurso, 16));
                ex.Add(producto);
                ex.Add(Dialogo.Texto($"≤ {Cifra(b)}/min", "plaza-prod-ritmo"));
                if (parados > 0) ex.Add(Dialogo.Texto($"❚❚{(parados > 1 ? parados.ToString() : "")}", "plaza-obra-insignia parado"));
                if (danados > 0) ex.Add(Dialogo.Texto($"⚠{(danados > 1 ? danados.ToString() : "")}", "plaza-obra-insignia danado"));
                var multiplica = kv.Key == "granja" ? " La fertilidad de la zona la multiplica." : kv.Key == "lenera" ? " La densidad del bosque la multiplica." : "";
                ConLetrero(ex, $"{Edificios.Nombre(kv.Key)}{(edificios.Count > 1 ? $" ×{edificios.Count}" : "")} ➜ {CosasDelMapa.NombreDeRecurso(recurso)}: hasta {Cifra(b)}/min con la mano de obra completa ({t} trabajadores).{multiplica}{(parados > 0 ? $" {parados} parado(s): almacén lleno." : "")}{(danados > 0 ? $" {danados} dañado(s)." : "")}");
                extractores.Add(ex);
            }
            if (extractores.childCount == 0) extractores.Add(Dialogo.Texto("Sin extractores ni granjas activos.", "plaza-vacio"));
            seccion.Add(extractores);
            col.Add(seccion);
        }

        /// <summary>La reserva de recursos del Tesorero (`htmlReservaRecursos`): una ficha por recurso con lo que hay frente a lo que cabe y una raya
        /// roja donde empieza la reserva (lo que la auto-construcción no toca). Solo el Tesorero, residente y dentro, la calibra (0 a 999).</summary>
        void Reserva(VisualElement col, Asentamiento a, Heroe yo, Action rehacer)
        {
            var reside = Reside(a, yo);
            var presente = Presente(a, yo);
            var puede = reside && presente;
            var motivo = !reside ? "Solo quien reside en esta plaza" : !presente ? "Tienes que estar en la plaza" : "";
            var tesorero = TitularDe(a, "tesorero");
            var esTesorero = tesorero == yo.Id;
            var calibra = esTesorero && puede;
            long Guardada(string r) => a.ReservaManual != null && a.ReservaManual.PorId.TryGetValue(r, out var n) ? (long)Math.Round(n / 1000.0) : 0;
            double Produce(string r) => a.Almacen.TryGetValue(r, out var x) ? (x.ProduccionPorMinuto != 0 ? x.ProduccionPorMinuto : x.ProducidoPorMinuto != 0 ? x.ProducidoPorMinuto : Math.Max(0, x.Cantidad?.PorMinuto ?? 0)) / 1000.0 : 0;
            var todos = OrdenDeRecursos.Where(r => r != "oro").ToList();
            var visibles = todos.Where(r => Produce(r) > 0 || Hay(a, r) >= 1 || Guardada(r) > 0 || borradorReserva.ContainsKey(r) || calibra && reservaAnadidos.Contains(r)).ToList();
            var resto = todos.Where(r => !visibles.Contains(r)).ToList();
            var seccion = Dialogo.Caja("plaza-almacen-reserva");
            seccion.name = "reserva";
            var conReserva = visibles.Count(r => Guardada(r) > 0);
            seccion.Add(TituloDeSeccion("Reserva de recursos", "Lo que la auto-construcción no gasta: se suma a la reserva que ya guarda sola. De 0 a 999 por recurso; no afecta a lo que añades a mano a la cola. En cada ficha, la raya roja marca dónde empieza la reserva.",
                ChipConIcono(IconosDelMundo.Icono("simbolo", "candado", 11, IconosDelMundo.OroClaro), conReserva.ToString())));
            if (tesorero == null) seccion.Add(Dialogo.Texto("Esta plaza no tiene Tesorero: sin él no se puede calibrar la reserva.", "plaza-nota"));
            else if (!calibra) seccion.Add(Dialogo.Texto(esTesorero ? $"{motivo} para calibrarla." : "Solo el Tesorero de la plaza calibra la reserva.", "plaza-nota"));
            if (visibles.Count > 0)
            {
                var rejilla = Rejilla("plaza-reserva-rejilla", 190, 5);
                foreach (var r in visibles)
                {
                    var recurso = r;
                    var guardada = Guardada(r);
                    var valor = borradorReserva.TryGetValue(r, out var b) ? b : guardada;
                    var hay = Hay(a, r);
                    var cabe = Math.Max(1, a.Almacen.TryGetValue(r, out var x) && x.Capacidad > 0 ? x.Capacidad / 1000.0 : Math.Max(hay, Math.Max(valor, 1)));
                    var bajo = valor > 0 && hay < valor;
                    var ficha = Dialogo.Caja("plaza-reserva-ficha" + (valor > 0 ? " con" : "") + (valor != guardada ? " cambiada" : "") + (bajo ? " bajo" : ""));
                    ficha.name = "reserva-" + r;
                    ficha.Add(IconosDelMundo.Icono("recurso", r, 20));
                    var centro = Dialogo.Caja("plaza-reserva-cuerpo");
                    centro.Add(Dialogo.Texto(CosasDelMapa.NombreDeRecurso(r), "plaza-reserva-nombre"));
                    centro.Add(Dialogo.Texto($"{hay}{(Produce(r) > 0 ? $" · ▲{Cifra(Produce(r))}/min" : "")}", "plaza-reserva-small"));
                    var barra = Dialogo.Caja("plaza-reserva-barra");
                    var i = Dialogo.Caja("plaza-reserva-barra-i");
                    i.style.width = Length.Percent((float)Math.Min(100, hay / cabe * 100));
                    barra.Add(i);
                    if (valor > 0)
                    {
                        var raya = Dialogo.Caja("plaza-reserva-raya");
                        raya.style.left = Length.Percent((float)Math.Min(100, valor / cabe * 100));
                        barra.Add(raya);
                    }
                    centro.Add(barra);
                    ficha.Add(centro);
                    if (calibra)
                    {
                        var campo = new IntegerField { value = (int)valor, isDelayed = true, name = "reserva-valor" };
                        campo.AddToClassList("plaza-reserva-campo");
                        campo.EnableInClassList("cambiada", valor != guardada);
                        campo.RegisterValueChangedCallback(ev => { borradorReserva[recurso] = ev.newValue; rehacer(); });
                        ficha.Add(campo);
                    }
                    else
                    {
                        var v = Dialogo.Caja("plaza-reserva-valor");
                        if (guardada > 0) v.Add(IconosDelMundo.Icono("simbolo", "candado", 11, new Color32(0xff, 0xb3, 0xa8, 0xff)));
                        v.Add(Dialogo.Texto(guardada > 0 ? guardada.ToString() : "—", "plaza-reserva-valor-texto"));
                        ficha.Add(v);
                    }
                    ConLetrero(ficha, $"{CosasDelMapa.NombreDeRecurso(r)}: hay {hay} de {Math.Floor(cabe)}{(valor > 0 ? $" · reserva {valor}{(bajo ? " (por debajo: la auto-construcción no lo gasta)" : "")}" : "")}");
                    rejilla.Add(ficha);
                }
                seccion.Add(rejilla);
            }
            else seccion.Add(Dialogo.Texto("La plaza no produce ni guarda nada todavía.", "plaza-vacio"));
            if (calibra)
            {
                var cambios = borradorReserva.Where(kv => kv.Value != Guardada(kv.Key)).ToList();
                var pie = Dialogo.Caja("plaza-reserva-pie");
                if (resto.Count > 0)
                {
                    var opciones = new List<string> { "＋ Añadir recurso" }.Concat(resto.Select(CosasDelMapa.NombreDeRecurso)).ToList();
                    var anadir = new DropdownField(opciones, 0) { name = "reserva-anadir" };
                    anadir.AddToClassList("plaza-reserva-anadir");
                    anadir.RegisterValueChangedCallback(_ => { if (anadir.index > 0) { reservaAnadidos.Add(resto[anadir.index - 1]); rehacer(); } });
                    pie.Add(anadir);
                }
                if (cambios.Count > 0)
                {
                    var descartar = Dialogo.Boton("Descartar", "plaza-reserva-descartar", () => { borradorReserva.Clear(); rehacer(); });
                    descartar.name = "reserva-descartar";
                    pie.Add(descartar);
                }
                Button guardar = null;
                guardar = new Button(async () =>
                {
                    if (cambios.Any(kv => kv.Value < 0 || kv.Value > 999)) { panelAvisos.Mostrar("La reserva va de 0 a 999.", true); return; }
                    // Una orden por recurso; se para en el primer rechazo y lo que quedó sin guardar sigue en el borrador.
                    foreach (var (recurso, valor) in cambios.Select(kv => (kv.Key, kv.Value)))
                    {
                        if (!await OrdenDePlaza(new Orden { CalibrarReservaManual = new OrdenCalibrarReservaManual { AsentamientoId = a.Id, Recurso = recurso, Valor = valor * 1000 } }, guardar)) return;
                        borradorReserva.Remove(recurso);
                    }
                    rehacer();
                });
                guardar.name = "reserva-guardar";
                guardar.AddToClassList("plaza-reserva-guardar");
                guardar.Add(IconosDelMundo.Icono("simbolo", "candado", 12, new Color32(0xea, 0xff, 0xdf, 0xff)));
                guardar.Add(Dialogo.Texto($"Guardar{(cambios.Count > 0 ? $" ({cambios.Count})" : "")}", "plaza-reserva-guardar-texto"));
                guardar.SetEnabled(cambios.Count > 0);
                pie.Add(guardar);
                seccion.Add(pie);
            }
            col.Add(seccion);
        }

        /// <summary>Pausar el consumo (`htmlPausarConsumo`): al Gobernador o al Tesorero, residentes y dentro, un interruptor por bien (los que la plaza
        /// tiene o produce, y los pausados); a los demás, solo lo pausado. El trigo nunca se pausa.</summary>
        void PausarConsumo(VisualElement col, Asentamiento a, Heroe yo)
        {
            var pausados = new HashSet<string>(a.ConsumoPausado);
            var pausa = Reside(a, yo) && Presente(a, yo) && (TitularDe(a, "gobernador") == yo.Id || TitularDe(a, "tesorero") == yo.Id);
            var seccion = Dialogo.Caja("plaza-almacen-consumo");
            seccion.name = "pausar-consumo";
            if (!pausa)
            {
                if (pausados.Count == 0) return;
                seccion.Add(TituloDeSeccion("Consumo pausado", null));
                // Lo pausado con su icono delante («🥩 Carne y 🍞 Pan», `textoPausados`).
                var l = a.ConsumoPausado.ToList();
                var nota = Renglon("plaza-nota", l.SelectMany((r, i) => new object[]
                {
                    IconosDelMundo.Icono("recurso", r, 14),
                    CosasDelMapa.NombreDeRecurso(r) + (i == l.Count - 1 ? ": la población no lo come y el trigo pone lo que cubría. Lo deciden el Gobernador y el Tesorero." : i == l.Count - 2 ? " y" : ","),
                }).ToArray());
                seccion.Add(nota);
                col.Add(seccion);
                return;
            }
            var catalogo = (DatoDeJuego("Economia_Construccion", "BIENES_DE_CONSUMO")?["bienes"] as JObject)?.Properties().Select(p => p.Name).Where(r => r != "trigo").ToList();
            var bienes = (catalogo != null && catalogo.Count > 0 ? catalogo : new List<string> { "carne", "pan", "queso", "vino", "cerveza", "aceite", "aceiteSesamo" })
                .Where(r => pausados.Contains(r) || Hay(a, r) >= 1 || a.Almacen.TryGetValue(r, out var x) && (x.ProduccionPorMinuto != 0 || x.ProducidoPorMinuto > 0)).ToList();
            seccion.Add(TituloDeSeccion("Pausar consumo", "Pausado, la población no come ese bien y su tipo de población pierde lo que le hacía crecer; el trigo pone lo que cubría. La guarnición tampoco come la carne pausada. Sirve para guardarlo y venderlo. El trigo nunca se pausa.",
                pausados.Count > 0 ? Chip($"❚❚ {pausados.Count}") : null));
            if (bienes.Count > 0)
            {
                var rejilla = Rejilla("plaza-consumo-pausas", 225, 4);
                foreach (var r in bienes)
                {
                    var recurso = r;
                    rejilla.Add(Interruptor("pausar-" + r, CosasDelMapa.NombreDeRecurso(r), IconosDelMundo.Icono("recurso", r, 14), pausados.Contains(r), "pausado", "se come", "plaza-consumo-pausa",
                        async pausado => await OrdenDePlaza(new Orden { PausarConsumo = new OrdenPausarConsumo { AsentamientoId = a.Id, Recurso = recurso, Pausado = pausado } })));
                }
                seccion.Add(rejilla);
            }
            else seccion.Add(Dialogo.Texto("La plaza no tiene ni produce ningún bien de consumo todavía.", "plaza-vacio"));
            col.Add(seccion);
        }

        /// <summary>Los talleres (`htmlRecetas`): una fila por producto con la cadena insumos ➜ producto, los talleres que la hacen y su ritmo con la mano
        /// de obra completa, y el interruptor ▶ / ❚❚ (con cargo de construcción) que la para o reanuda en todos los talleres de la plaza.</summary>
        void Talleres(VisualElement col, Asentamiento a, Heroe yo)
        {
            var cargo = CargoConstructor(a, yo.Id);
            var pausadas = new HashSet<string>(a.RecetasPausadas);
            var adoptadas = TecnologiasDeMiFaccion();
            var porRecurso = new Dictionary<string, (Dictionary<string, int> talleres, double porMinuto, JObject insumos, string tecnologia, string edificio, int nivel)>();
            foreach (var e in Internos(a))
            {
                if (!Activo(e) || e.RecetaParada) continue;
                var activo = InsumoActivo(e);
                var buey = e.Buey ? (double?)Catalogo?[e.Tipo]?["factorBuey"] ?? 1 : 1;
                foreach (var r in (NivelDe(e.Tipo, NivelInterno(e))?["recetas"] as JArray)?.OfType<JObject>() ?? Enumerable.Empty<JObject>())
                {
                    var consume = r["consumePorUnidad"] as JObject ?? new JObject();
                    if (activo != null && consume[activo] == null) continue;
                    var produce = (string)r["produce"];
                    (Dictionary<string, int> talleres, double porMinuto, JObject insumos, string tecnologia, string edificio, int nivel) ag = porRecurso.TryGetValue(produce, out var x) ? x
                        : (new Dictionary<string, int>(), 0.0, consume, (string)r["requiereTecnologia"], (string)r["requiereEdificio"]?["tipo"], (int?)r["requiereEdificio"]?["nivel"] ?? 1);
                    ag.talleres[e.Tipo] = (ag.talleres.TryGetValue(e.Tipo, out var n) ? n : 0) + 1;
                    porRecurso[produce] = (ag.talleres, ag.porMinuto + ((double?)r["produccionBase"] ?? 0) * buey, ag.insumos, ag.tecnologia, ag.edificio, ag.nivel);
                }
            }
            // Una receta parada cuyo taller ya no está sigue en la lista: se deja a la vista para poder reanudarla.
            foreach (var r in a.RecetasPausadas) if (!porRecurso.ContainsKey(r)) porRecurso[r] = (new Dictionary<string, int>(), 0, new JObject(), null, null, 1);
            var seccion = Dialogo.Caja("plaza-talleres");
            seccion.name = "talleres";
            seccion.Add(TituloDeSeccion("Talleres", "Cada fila: lo que gasta por unidad ➜ lo que fabrica, con los talleres que la hacen y su ritmo con la mano de obra completa. ❚❚ la para en todos los talleres de la plaza; ▶ la reanuda. Con candado: falta la tecnología, o el edificio que pide esa receta. Solo el Gobernador o el Maestro de Obras las paran o reanudan."));
            var filas = Dialogo.Caja("plaza-prod-recetas");
            foreach (var kv in porRecurso.OrderBy(kv => CosasDelMapa.NombreDeRecurso(kv.Key), StringComparer.Create(Es, false)))
            {
                var produce = kv.Key;
                var (talleres, porMinuto, insumos, tecnologia, edificio, nivel) = kv.Value;
                var parada = pausadas.Contains(produce);
                var falta = adoptadas != null && tecnologia != null && !adoptadas.Contains(tecnologia) ? tecnologia : "";
                var sinEdificio = edificio != null && !Internos(a).Any(x => x.Tipo == edificio && Activo(x) && NivelInterno(x) >= nivel) ? $"{Edificios.Nombre(edificio)} de nivel {nivel}" : "";
                var nombre = CosasDelMapa.NombreDeRecurso(produce);
                var estado = falta != "" || sinEdificio != "" ? "bloqueada" : parada ? "parada" : "marcha";
                var fila = Dialogo.Caja("plaza-prod-receta " + estado);
                fila.name = "receta-" + produce;
                var cadena = Dialogo.Caja("plaza-prod-cadena");
                foreach (var p in insumos.Properties())
                {
                    var ins = Dialogo.Caja("plaza-prod-insumo");
                    ins.Add(IconosDelMundo.Icono("recurso", p.Name, 15));
                    ins.Add(Dialogo.Texto(Cifra((double)p.Value), "plaza-prod-insumo-n"));
                    cadena.Add(ins);
                }
                if (insumos.Count == 0) cadena.Add(Dialogo.Texto("·", "plaza-prod-insumo-vacio"));
                cadena.Add(Dialogo.Texto("➜", "plaza-prod-flecha"));
                var producto = Dialogo.Caja("plaza-prod-producto");
                producto.Add(IconosDelMundo.Icono("recurso", produce, 16));
                cadena.Add(producto);
                fila.Add(cadena);
                var centro = Dialogo.Caja("plaza-prod-receta-cuerpo");
                centro.Add(Dialogo.Texto(nombre, "plaza-prod-receta-nombre"));
                var pie = Dialogo.Caja("plaza-prod-receta-pie");
                foreach (var t in talleres)
                {
                    pie.Add(IconosDelMundo.Icono("edificio", t.Key, 16, IconosDelMundo.OroClaro));
                    if (t.Value > 1) pie.Add(Dialogo.Texto($"×{t.Value}", "plaza-prod-taller-n"));
                }
                if (porMinuto > 0) pie.Add(Dialogo.Texto($"{Cifra(porMinuto)}/min", "plaza-prod-receta-small"));
                if (falta != "" || sinEdificio != "")
                {
                    pie.Add(IconosDelMundo.Icono("simbolo", "candado", 10, new Color32(0xe2, 0x67, 0x5a, 0xff)));
                    pie.Add(Dialogo.Texto(falta != "" ? "tecnología" : sinEdificio, "plaza-prod-receta-small falta"));
                }
                centro.Add(pie);
                fila.Add(centro);
                if (cargo != null)
                {
                    Button b = null;
                    b = Dialogo.Boton(parada ? "▶" : "❚❚", "plaza-prod-interruptor" + (parada ? "" : " encendido"), async () =>
                        await OrdenDePlaza(new Orden { AlternarReceta = new OrdenAlternarReceta { AsentamientoId = a.Id, Recurso = produce, Pausada = !parada } }, b));
                    b.name = "receta-interruptor";
                    fila.Add(b);
                }
                else if (falta != "" || sinEdificio != "") fila.Add(IconosDelMundo.Icono("simbolo", "candado", 14, IconosDelMundo.Bronce));
                else fila.Add(Dialogo.Texto(parada ? "❚❚" : "▶", "plaza-prod-estado"));
                ConLetrero(fila, $"{nombre}: {(falta != "" ? $"bloqueada, requiere la tecnología «{falta}»" : sinEdificio != "" ? $"bloqueada, le falta una {sinEdificio} en la plaza" : parada ? "parada" : "en marcha")}{(porMinuto > 0 ? $" · hasta {Cifra(porMinuto)}/min con la mano de obra completa" : "")}{(insumos.Count > 0 ? " · por unidad: " + string.Join(", ", insumos.Properties().Select(p => $"{Cifra((double)p.Value)} {CosasDelMapa.NombreDeRecurso(p.Name)}")) : "")}");
                filas.Add(fila);
            }
            if (filas.childCount == 0) filas.Add(Dialogo.Texto("Ningún taller activo con recetas.", "plaza-vacio"));
            seccion.Add(filas);
            col.Add(seccion);
        }

        // ── «Tu carga» ──

        /// <summary>Manda una orden del carro; el rechazo, en la línea de error de la ventana (como `cablearCarro`).</summary>
        async Task<bool> OrdenDeCarga(Orden orden, Label error, VisualElement boton)
        {
            boton?.SetEnabled(false);
            ResultadoOrden r = null;
            try { r = await c.Sesion.Ordenar(orden); }
            catch (ErrorDeSesion e) { error.text = e.Message; }
            boton?.SetEnabled(true);
            if (r?.Resultado == Resultado.OrdenRechazada) { error.text = Textos.Explicar(r.Rechazo); return false; }
            if (r != null) error.text = "";
            return r?.Resultado == Resultado.OrdenAceptada;
        }

        /// <summary>Las filas de un montón con su cantidad y sus botones (`filas` de `panelCarro.ts`).</summary>
        void FilasDeCarga(VisualElement v, List<KeyValuePair<string, double>> recursos, string clave,
            List<(string etiqueta, string id, string motivo, double? tope, Func<string, long, Button, Task> hacer)> acciones, Label error)
        {
            if (recursos.Count == 0) { v.Add(Dialogo.Texto("Vacío.", "plaza-vacio")); return; }
            foreach (var kv in recursos)
            {
                var recurso = kv.Key;
                var util = (long)kv.Value;
                var fila = Dialogo.Caja("plaza-carro-fila");
                fila.name = $"{clave}-{recurso}";
                var nombre = IconosDelMundo.ConIcono("recurso", recurso, Dialogo.Texto(CosasDelMapa.NombreDeRecurso(recurso), "plaza-carro-nombre"), 18);
                nombre.AddToClassList("plaza-carro-recurso");
                fila.Add(nombre);
                fila.Add(Dialogo.Texto(util.ToString(), "plaza-carro-cifra"));
                if (acciones.Count > 0)
                {
                    var valor = Math.Max(1, (long)Math.Min(util, acciones.Min(x => x.tope ?? util)));
                    var campo = new IntegerField { value = (int)valor, name = "cantidad" };
                    campo.AddToClassList("plaza-carro-cantidad");
                    fila.Add(campo);
                    foreach (var x in acciones)
                    {
                        var accion = x;
                        Button b = null;
                        b = Dialogo.Boton(accion.etiqueta, "boton-secundario plaza-carro-boton", async () =>
                        {
                            if (campo.value < 1) { error.text = "Pon una cantidad de 1 o más."; return; }
                            await accion.hacer(recurso, campo.value, b);
                        });
                        b.name = accion.id;
                        if (accion.motivo != null) { b.SetEnabled(false); ConLetrero(b, accion.motivo); }
                        fila.Add(b);
                    }
                }
                v.Add(fila);
            }
        }

        /// <summary>«Tu carga» (`htmlCarro` con la plaza, a dos columnas): a la izquierda lo tuyo, el carro y el almacén personal; a la derecha el almacén de
        /// la plaza, para pasar de uno a otro. Sacar de la plaza al carro, salvo que el Gobernador lo cierre; dejar, siempre. El Gobernador lo cierra.</summary>
        void TuCarga(VisualElement cuerpo, Asentamiento a, Heroe yo)
        {
            var error = Dialogo.Texto("", "plaza-error");
            error.name = "error-carro";
            var columna = yo.Ubicacion?.DondeCase == UbicacionHeroe.DondeOneofCase.EjercitoId ? ColumnaDelHeroe(yo) : null;
            var esLider = columna?.LiderId == yo.Id;
            var aMano = AlmacenAMano(yo, columna);
            var carro = columna != null ? columna.Suministro : yo.FueraCarro;
            var capacidad = c.Datos.Numero("documentos.Movimiento_Ejercitos.LOGISTICA.capacidadCarroPorJugador") * Math.Max(1, columna?.Participantes.Count ?? 1);
            var enCarro = Unidades(carro);
            var libre = Math.Max(0, Math.Floor(capacidad - enCarro.Sum(x => x.Value)));
            var hayCarro = enCarro.Count > 0;
            var gobernador = TitularDe(a, "gobernador") == yo.Id;
            var cerrado = a.AlmacenCerradoACiudadanos;
            var motivoLider = columna != null && !esLider ? "Solo el Líder de la columna mueve lo del carro." : null;
            var nombrePlaza = a.HasNombre ? a.Nombre : a.Id;

            Func<string, long, Button, Task> Orden1(Func<string, long, Orden> orden) => async (r, n, b) => await OrdenDeCarga(orden(r, n * 1000), error, b);
            var cols = Dialogo.Caja("plaza-carga-columnas");
            var mia = Dialogo.Caja("plaza-carga-mia");
            mia.name = "carga-mia";
            var dePlaza = Dialogo.Caja("plaza-carga-plaza");
            dePlaza.name = "carga-plaza";
            DosColumnas(cols, mia, dePlaza, 788, 28);

            var nota = columna == null
                ? "Dentro de tu residencia el carro sigue contigo: entrar no lo vacía. Pásalo a tu almacén personal o al de la plaza recurso a recurso, o todo de una vez con «Todo al almacén personal». Al salir, lo que haya en el carro sale contigo y en la ventana de salida eliges qué más cargas."
                : "Dentro de una plaza de tu Facción sacas de su almacén al carro (salvo que su Gobernador lo cierre) y le dejas lo que lleves; en una aliada o vasalla solo puedes dejar.";
            mia.Add(ConAyuda(Dialogo.Texto("Carga", "plaza-sub"), nota));
            mia.Add(Ocupacion(columna != null ? "Carro de tu columna" : "Tu carro", enCarro.Sum(x => x.Value), capacidad, "ocupacion-carro"));
            var accionesCarro = new List<(string, string, string, double?, Func<string, long, Button, Task>)>();
            if (aMano) accionesCarro.Add(("Al almacén personal", "al-almacen", motivoLider, null, Orden1((r, n) => new Orden { GuardarEnAlmacenPersonal = new OrdenGuardarEnAlmacenPersonal { Recurso = r, Cantidad = n } })));
            accionesCarro.Add(("A la plaza", "a-la-plaza", null, null, Orden1((r, n) => new Orden { DejarEnPlaza = new OrdenDejarEnPlaza { AsentamientoId = a.Id, Recurso = r, Cantidad = n } })));
            FilasDeCarga(mia, enCarro, "carro", hayCarro ? accionesCarro : new List<(string, string, string, double?, Func<string, long, Button, Task>)>(), error);
            if (columna == null && hayCarro && aMano)
            {
                Button todo = null;
                todo = Dialogo.Boton("Todo al almacén personal", "boton-secundario plaza-carro-todo", async () =>
                    await OrdenDeCarga(new Orden { VaciarCarro = new OrdenVaciarCarro { HeroeId = yo.Id } }, error, todo));
                todo.name = "vaciar-carro";
                mia.Add(todo);
            }
            foreach (var aparcada in c.Sesion.Almacen.Todas(Entidad.CuerpoOneofCase.Ejercito).Select(e => e.Ejercito).Where(e => e != columna && e.LiderId == yo.Id && Unidades(e.Suministro).Count > 0))
            {
                mia.Add(Dialogo.Texto("Resto del carro, aparcado en la puerta", "plaza-sub"));
                FilasDeCarga(mia, Unidades(aparcada.Suministro), "aparcado", new List<(string, string, string, double?, Func<string, long, Button, Task>)>(), error);
            }
            if (aMano)
            {
                var almacen = Unidades(yo.AlmacenPersonal);
                mia.Add(Ocupacion("Almacén personal", almacen.Sum(x => x.Value), yo.CapacidadAlmacenPersonal / 1000.0, "ocupacion-almacen"));
                FilasDeCarga(mia, almacen, "personal", new List<(string, string, string, double?, Func<string, long, Button, Task>)>
                {
                    ("Al carro", "al-carro", motivoLider ?? (libre == 0 ? "El carro está lleno." : null), libre,
                        Orden1((r, n) => new Orden { SacarDelAlmacenPersonal = new OrdenSacarDelAlmacenPersonal { Recurso = r, Cantidad = n } })),
                    // Del almacén personal a la plaza: el motor no tiene orden directa, así que pasa por el carro (sacar y dejar), hasta lo que cabe en él.
                    ("A la plaza", "a-la-plaza", motivoLider ?? (libre == 0 ? "El carro está lleno: pasa por él." : null), libre, async (r, n, b) =>
                    {
                        if (!await OrdenDeCarga(new Orden { SacarDelAlmacenPersonal = new OrdenSacarDelAlmacenPersonal { Recurso = r, Cantidad = n * 1000 } }, error, b)) return;
                        if (!await OrdenDeCarga(new Orden { DejarEnPlaza = new OrdenDejarEnPlaza { AsentamientoId = a.Id, Recurso = r, Cantidad = n * 1000 } }, error, b))
                            error.text = $"Pasó al carro, pero no a la plaza: {error.text}";
                    }),
                }, error);
            }
            else mia.Add(Dialogo.Texto("El almacén personal está en tu residencia: se usa dentro o con tu columna a su puerta.", "plaza-nota"));

            dePlaza.Add(Dialogo.Texto($"Almacén de {nombrePlaza}", "plaza-sub"));
            if (cerrado) dePlaza.Add(Dialogo.Texto(gobernador ? "Lo tienes cerrado: los ciudadanos no sacan al carro (dejar sigue abierto)." : "El Gobernador lo ha cerrado: no se saca al carro, pero se puede dejar.", "plaza-nota"));
            var motivoSacar = cerrado && !gobernador ? "El Gobernador ha cerrado el almacén a los ciudadanos." : libre == 0 ? "El carro está lleno." : null;
            var dePlazaLista = a.Almacen.Keys.Where(r => Hay(a, r) >= 1).OrderBy(r => r, StringComparer.Ordinal).Select(r => new KeyValuePair<string, double>(r, Hay(a, r))).ToList();
            FilasDeCarga(dePlaza, dePlazaLista, "plaza", new List<(string, string, string, double?, Func<string, long, Button, Task>)>
            {
                ("Al carro", "al-carro", motivoSacar, libre, Orden1((r, n) => new Orden { CargarDePlaza = new OrdenCargarDePlaza { AsentamientoId = a.Id, Recurso = r, Cantidad = n } })),
            }, error);
            if (gobernador)
            {
                var cierre = new Toggle("Cerrar el almacén a los ciudadanos (no sacan al carro)") { value = cerrado, name = "cerrar-almacen" };
                cierre.AddToClassList("plaza-carga-cierre");
                cierre.RegisterValueChangedCallback(async ev =>
                {
                    if (!await OrdenDeCarga(new Orden { CerrarAlmacenACiudadanos = new OrdenCerrarAlmacenACiudadanos { AsentamientoId = a.Id, Cerrado = ev.newValue } }, error, null))
                        cierre.SetValueWithoutNotify(!ev.newValue);
                });
                dePlaza.Add(cierre);
            }
            var caja = Dialogo.Caja("plaza-almacen-carga");
            caja.Add(cols);
            caja.Add(error);
            cuerpo.Add(caja);
        }
    }
}
