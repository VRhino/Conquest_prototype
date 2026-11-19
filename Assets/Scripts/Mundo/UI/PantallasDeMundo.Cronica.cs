// La crónica del mundo (paso 13 del bloque 4, tanda 2), calcada de BronzeAgeClient (`ui/cronica.ts`): lo que cantan los Aedas para todos. El botón
// «Crónica» de la barra (el libro), tras la campana de Avisos, abre su panel lateral como el de Avisos (Víctor, 1a): lo más nuevo arriba, cada entrada
// con su icono, su frase y la fecha; lo de tu Facción, marcado; «Más antiguas» pide la página anterior (`PedirCronica`) y lo nuevo llega en vivo (el
// hecho `cronica.entrada` de las `Delta`). El contrato no trae texto: la frase la compone el cliente, con una de dos variantes según un hash de la
// entrada (siempre la misma para la misma).
using System;
using System.Collections.Generic;
using System.Linq;
using BronzeAge.Contract.V1;
using ConquestTactics.Mundo.Mapa;
using UnityEngine.UIElements;

namespace ConquestTactics.Mundo.UI
{
    public partial class PantallasDeMundo
    {
        VisualElement panelCronica;
        readonly List<EventoDominio> cronica = new List<EventoDominio>();
        readonly HashSet<string> clavesDeCronica = new HashSet<string>();
        bool cronicaMasAntiguas;
        /// <summary>sin_pedir, cargando, lista o error.</summary>
        string estadoCronica = "sin_pedir", errorCronica;
        Sesion sesionDeCronica;

        public bool CronicaAbierta => panelCronica?.panel != null;

        /// <summary>El botón «Crónica» de la barra (`barraJugador.ts`), tras la campana de Avisos. Abrir uno de los dos paneles cierra el otro.</summary>
        void BotonDeCronica(VisualElement barra, Button avisos)
        {
            var b = new Button(AlternarCronica) { name = "cronica", tooltip = "Crónica" };
            IconoDeBarra(b, "cronica");
            barra.Insert(barra.IndexOf(avisos) + 1, b);
            avisos.clicked += () => { if (CronicaAbierta) CerrarCronica(); };
        }

        void AlternarCronica()
        {
            if (CronicaAbierta) { CerrarCronica(); return; }
            if (panelAvisos.Abierto) panelAvisos.Alternar();
            panelCronica = Dialogo.Caja("placa panel-avisos panel-cronica");
            panelCronica.name = "panel-cronica";
            capaEncima.Add(panelCronica);
            (pantalla.childCount > 0 ? pantalla[0] : null)?.Q(className: "barra-arriba")?.Q<Button>("cronica")?.AddToClassList("activo");
            OirCronica();
            if (estadoCronica == "sin_pedir" || estadoCronica == "error") PedirCronica(null);
            PintarCronica();
        }

        void CerrarCronica()
        {
            panelCronica?.RemoveFromHierarchy();
            panelCronica = null;
            (pantalla.childCount > 0 ? pantalla[0] : null)?.Q(className: "barra-arriba")?.Q<Button>("cronica")?.RemoveFromClassList("activo");
        }

        /// <summary>Lo nuevo llega en vivo: entra si la crónica ya se pidió (si no, ya vendrá en la primera página). Una sesión nueva empieza de cero.</summary>
        void OirCronica()
        {
            if (sesionDeCronica == c.Sesion) return;
            sesionDeCronica = c.Sesion;
            cronica.Clear();
            clavesDeCronica.Clear();
            cronicaMasAntiguas = false;
            estadoCronica = "sin_pedir";
            if (sesionDeCronica == null) return;
            sesionDeCronica.AlEvento += (_, e) =>
            {
                if (e.Codigo != "cronica.entrada" || estadoCronica != "lista") return;
                Sumar(new[] { e });
                if (CronicaAbierta) PintarCronica();
            };
        }

        /// <summary>La clave de una entrada, como la del prototipo (`version|tipo|JSON de sus datos`): da la variante de la frase, la misma que allí.</summary>
        static string ClaveDeCronica(EventoDominio e)
        {
            var datos = e.Payload == null ? "{}" : Newtonsoft.Json.Linq.JToken.Parse(Google.Protobuf.JsonFormatter.Default.Format(e.Payload)).ToString(Newtonsoft.Json.Formatting.None);
            var tipo = e.Payload != null && e.Payload.Fields.TryGetValue("tipo", out var t) ? t.StringValue : "";
            return $"{e.Version}|{tipo}|{datos}";
        }

        void Sumar(IEnumerable<EventoDominio> entradas)
        {
            foreach (var e in entradas)
                if (clavesDeCronica.Add(ClaveDeCronica(e))) cronica.Add(e);
            // lo más nuevo arriba; dentro de una versión, en el orden en que llegaron
            var ordenada = cronica.Select((e, i) => (e, i)).OrderByDescending(x => x.e.Version).ThenBy(x => x.i).Select(x => x.e).ToList();
            cronica.Clear();
            cronica.AddRange(ordenada);
        }

        /// <summary>Una página hacia atrás: las últimas o, con `hasta`, las de esa versión y anteriores.</summary>
        async void PedirCronica(ulong? hasta)
        {
            estadoCronica = "cargando";
            try
            {
                var r = await c.Sesion.PedirCronica(hasta: hasta, limite: 200);
                Sumar(r.Entradas);
                cronicaMasAntiguas = r.MasAntiguas;
                estadoCronica = "lista";
            }
            catch (Exception e)
            {
                estadoCronica = "error";
                errorCronica = e.Message;
            }
            if (CronicaAbierta) PintarCronica();
        }

        // ─────────────── las frases ───────────────

        /// <summary>El hash de las variantes (Tecnologia_Aedas §8): `h = (h·31 + código) mod 2³²`.</summary>
        static uint HashDeCronica(string s)
        {
            uint h = 0;
            foreach (var ch in s) h = unchecked(h * 31 + ch);
            return h;
        }

        /// <summary>El icono y la frase de una entrada (`fraseDeCronica`).</summary>
        ((string familia, string id) icono, string texto) FraseDeCronica(EventoDominio x, Heroe yo)
        {
            var a = c.Sesion.Almacen;
            string Campo(string k) => x.Payload != null && x.Payload.Fields.TryGetValue(k, out var v) ? (v.HasStringValue ? v.StringValue : v.HasNumberValue ? v.NumberValue.ToString() : null) : null;
            var facciones = x.Payload != null && x.Payload.Fields.TryGetValue("faccionIds", out var lista) && lista.ListValue != null
                ? lista.ListValue.Values.Select(v => v.StringValue).ToList() : new List<string>();
            string Faccion(string id)
            {
                if (id == null) return null;
                var n = a.Buscar(TipoEntidad.Faccion, id)?.Faccion?.Nombre;
                return yo != null && yo.HasFaccionId && id == yo.FaccionId ? $"{n ?? "tu Facción"} (la tuya)" : n ?? "una Facción lejana";
            }
            var fa = Faccion(facciones.ElementAtOrDefault(0));
            var fb = Faccion(facciones.ElementAtOrDefault(1));
            var fc = Faccion(facciones.ElementAtOrDefault(2));
            var plazaId = Campo("asentamientoId");
            var plaza = plazaId == null ? "una plaza lejana" : a.Buscar(TipoEntidad.Asentamiento, plazaId)?.Asentamiento is Asentamiento p && p.HasNombre ? p.Nombre : "una plaza lejana";
            string Una(params string[] opciones) => opciones[(int)(HashDeCronica(ClaveDeCronica(x)) % (uint)opciones.Length)];
            var tecId = Campo("tecnologia");
            var tec = tecId != null ? NombreDeTecnologia(tecId) : "un saber nuevo";
            var tituloId = Campo("titulo");
            var titulo = a.Todas(Entidad.CuerpoOneofCase.Titulo).Select(e => e.Titulo).FirstOrDefault(t => t.TituloId == tituloId)?.Nombre ?? (tituloId ?? "un título").Replace('.', ' ').Replace('_', ' ');
            switch (Campo("tipo"))
            {
                case "asentamiento.conquistada":
                    return (("glifo", "atacar"), Una($"{fa ?? "Una Facción"} arranca {plaza} de manos de {fb ?? "sus dueños"}.", $"{plaza} cae: sus puertas se abren a {fa ?? "los vencedores"} y {fb ?? "sus dueños"} la pierde."));
                case "asentamiento.ruinas":
                    return (("glifo", "fuego"), Una($"{plaza} queda en ruinas; los Aedas lloran sus muros caídos.", $"Del fuego de {plaza} solo quedan piedras: nadie la habita ya."));
                case "fundacion.asentamiento_fundado":
                    return (("simbolo", "casa"), Una($"{fa ?? "Una Facción"} funda {plaza}.", $"Se alzan las primeras casas de {plaza}, de {fa ?? "una Facción"}."));
                case "diplomacia.guerra_declarada":
                    return (("simbolo", "bandera"), Una($"{fa ?? "Una Facción"} declara la guerra a {fb ?? "otra"}.", $"Suenan los cuernos: {fa ?? "una Facción"} va a la guerra contra {fb ?? "otra"}."));
                case "diplomacia.rebelion_vasallo":
                    return (("simbolo", "bandera"), $"Se rompe el vasallaje entre {fa ?? "dos Facciones"} y {fb ?? "su señora"}: el vasallo se rebela.");
                case "diplomacia.vasallo_liberado":
                    return (("simbolo", "bandera"), $"Termina el vasallaje entre {fa ?? "dos Facciones"} y {fb ?? "su vasallo"}: el vasallo queda libre.");
                case "diplomacia.vasallo_conquistado":
                    return (("simbolo", "bandera"), $"El vasallaje entre {fa ?? "dos Facciones"} y {fb ?? "su vasallo"} se impone por las armas.");
                case "diplomacia.anexion":
                    return (("simbolo", "bandera"), Una($"{fa ?? "Una Facción"} se anexiona a {fb ?? "otra"}.", $"{fb ?? "Una Facción"} deja de existir: {fa ?? "otra"} la absorbe."));
                case "diplomacia.fusion":
                    return (("simbolo", "bandera"), $"{fa ?? "Dos Facciones"} y {fb ?? "otra"} se funden en una sola: {fc ?? "una Facción nueva"}.");
                case "titulo.nace":
                    return (("simbolo", "estrella"), $"Nace el título «{titulo}»: lo gana {fa ?? "una Facción"}.");
                case "titulo.cambia_manos":
                    return (("simbolo", "estrella"), $"El título «{titulo}» pasa {(fb != null ? $"de {fb} " : "")}a {fa ?? "otra Facción"}.");
                case "tecnologia.logro":
                    return (("glifo", "obra"), $"Entre todas las Facciones se llega a {Miles(double.TryParse(Campo("umbral"), out var u) ? u : 0)} {TextoContador(Campo("contador") ?? "")}: algo nuevo puede descubrirse.");
                case "era.comienza":
                {
                    var eraId = Campo("era");
                    var era = (string)BloqueDeDatos("ERAS")?[eraId ?? ""]?["nombre"] ?? (eraId ?? "").Replace('.', ' ').Replace('_', ' ');
                    var orden = int.TryParse(Campo("orden"), out var o) ? o : 0;
                    var romano = new[] { "", "I", "II", "III", "IV", "V" }.ElementAtOrDefault(orden) ?? orden.ToString();
                    return (("glifo", "diaria"), $"Comienza la Era {romano}{(era != "" ? $", {era}" : "")}{(Campo("porPlazo") is string pp && pp != "0" ? ": se cumplió su plazo" : "")}.");
                }
                case "aedas.canta_descubrimiento":
                    return (("glifo", "aeda"), Una($"Los Aedas cantan por los caminos que {fa ?? "una Facción"} ha descubierto {tec}.", $"De plaza en plaza se canta el hallazgo de {fa ?? "una Facción"}: {tec}."));
                case "aedas.epica_cumplida":
                    return (("glifo", "aeda"), Una($"Un Aeda de {fa ?? "una Facción"} termina su épica: un saber nuevo entra en sus plazas.", $"Se canta entera la épica de un Aeda de {fa ?? "una Facción"}."));
                case "asentamiento.nivel_subio":
                    return (("simbolo", "corona"), $"{plaza}{(fa != null ? $", de {fa}," : "")} es la primera plaza del mundo en llegar al nivel {Campo("nivel") ?? "?"}.");
                default:
                    return ((null, null), (Campo("tipo") ?? "").Replace('.', ' ').Replace('_', ' '));
            }
        }

        // ─────────────── el panel ───────────────

        void PintarCronica()
        {
            if (!CronicaAbierta) return;
            var yo = HeroeAhora();
            var desplazado = panelCronica.Q<ScrollView>("cronica-lista")?.scrollOffset ?? UnityEngine.Vector2.zero;
            panelCronica.Clear();
            var filete = Dialogo.Caja("placa-filete");
            panelCronica.Add(filete);
            var cab = Dialogo.Caja("avisos-cab");
            cab.Add(ConAyuda(Dialogo.Texto("CRÓNICA DEL MUNDO", "kicker"), "Lo que cantan los Aedas para todos: la misma crónica para todos los jugadores, desde el principio de la partida. Lo de tu Facción va marcado."));
            var cerrar = Dialogo.Equis(CerrarCronica);
            cerrar.name = "cronica-cerrar";
            cab.Add(cerrar);
            filete.Add(cab);
            if (estadoCronica == "error")
            {
                filete.Add(Dialogo.Texto("No se pudo leer la crónica: " + errorCronica, "error"));
                var otra = Dialogo.Boton("Reintentar", "boton-secundario", () => { PedirCronica(null); PintarCronica(); });
                otra.name = "cronica-reintentar";
                filete.Add(otra);
                return;
            }
            if (cronica.Count == 0)
            {
                filete.Add(Dialogo.Texto(estadoCronica == "lista" ? "La crónica aún no canta nada: el mundo acaba de empezar." : "Leyendo la crónica…", "vacio"));
                return;
            }
            var lista = new ScrollView(ScrollViewMode.Vertical) { name = "cronica-lista", horizontalScrollerVisibility = ScrollerVisibility.Hidden };
            lista.style.flexGrow = 1;
            foreach (var x in cronica)
            {
                var (icono, texto) = FraseDeCronica(x, yo);
                var mia = yo != null && yo.HasFaccionId && x.Payload != null && x.Payload.Fields.TryGetValue("faccionIds", out var f) && f.ListValue != null
                    && f.ListValue.Values.Any(v => v.StringValue == yo.FaccionId);
                var fila = Dialogo.Caja("cronica-entrada" + (mia ? " es-propia" : ""));
                var circulo = Dialogo.Caja("cronica-icono");
                if (icono.familia != null && IconosDelMundo.Icono(icono.familia, icono.id, 16, icono.familia == "simbolo" ? IconosDelMundo.Bronce : (UnityEngine.Color?)null) is VisualElement ic) circulo.Add(ic);
                else circulo.Add(Dialogo.Texto("•", "cronica-punto"));
                fila.Add(circulo);
                var cuerpo = Dialogo.Caja("cronica-texto");
                cuerpo.Add(Dialogo.Texto(texto, "cronica-frase"));
                var cuando = Instante.Epoca.AddMilliseconds(x.EnMs).ToLocalTime();
                cuerpo.Add(Dialogo.Texto(cuando.ToString("d MMM HH:mm", Es), "cronica-fecha"));
                fila.Add(cuerpo);
                lista.Add(fila);
            }
            filete.Add(lista);
            if (desplazado != UnityEngine.Vector2.zero) lista.RegisterCallback<GeometryChangedEvent>(_ => lista.scrollOffset = desplazado);
            if (cronicaMasAntiguas)
            {
                var mas = Dialogo.Boton(estadoCronica == "cargando" ? "Leyendo…" : "Más antiguas", "boton-secundario cronica-mas", () =>
                {
                    var vieja = cronica.LastOrDefault();
                    if (vieja != null && vieja.Version > 0) { PedirCronica(vieja.Version - 1); PintarCronica(); }
                });
                mas.name = "cronica-mas";
                mas.SetEnabled(estadoCronica != "cargando");
                filete.Add(mas);
            }
        }
    }
}
