// La Facción (paso 8 del bloque 4, tanda 1), calcada de BronzeAgeClient: el sigilo en la interfaz (`svgSigilo`, como textura de
// `TexturaDeSigilo`), el botón «Facción» de la barra del jugador (`ui/barraJugador.ts`) y su ventana a pantalla completa (`main.ts:1835-1900`).
// Sin Facción (`ui/pestanaFaccion.ts:587-655`, `cablearFaccion` en `main.ts:271-358`): «Elige tu facción», crear la tuya con el selector de sigilo
// y su validación (`ui/validarFaccion.ts`) y pedir ingreso con la lista de Facciones (`htmlListaFacciones`). Con Facción, la tanda 2.
using System;
using System.Collections.Generic;
using System.Linq;
using BronzeAge.Contract.V1;
using UnityEngine;
using UnityEngine.UIElements;

namespace ConquestTactics.Mundo.UI
{
    public partial class PantallasDeMundo
    {
        // ─────────────── el sigilo en la interfaz ───────────────

        /// <summary>El sigilo como elemento de `ancho` px (alto ×1,2, o ×1,5 con la corona del Gran Rey); la textura, al doble para que se vea nítido.
        /// Sin sigilo, el neutro.</summary>
        static VisualElement SigiloUI(Sigilo s, float ancho, bool granRey = false, string clase = null)
        {
            var e = new VisualElement { pickingMode = PickingMode.Ignore };
            e.AddToClassList("sigilo");
            if (clase != null) e.AddToClassList(clase);
            PonerSigilo(e, s, ancho, granRey);
            return e;
        }

        static void PonerSigilo(VisualElement e, Sigilo s, float ancho, bool granRey = false)
        {
            e.style.width = ancho;
            e.style.height = ancho * (granRey ? 1.5f : 1.2f);
            e.style.flexShrink = 0;
            e.style.backgroundImage = new StyleBackground(TexturaDeSigilo.De(s, Mathf.CeilToInt(ancho * 2), granRey));
            e.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
        }

        /// <summary>El sigilo de una Facción que conoces (la tuya o del directorio) y si es la del Gran Rey de su Liga (`faccionDuena`).</summary>
        (Sigilo sigilo, bool granRey) SigiloDeFaccion(string faccionId)
        {
            var a = c.Sesion?.Almacen;
            var f = a?.Buscar(TipoEntidad.Faccion, faccionId)?.Faccion;
            return (Sigilos.DeFaccion(a, faccionId), f?.Liga != null && f.Liga.GranReyId == faccionId);
        }

        // ─────────────── la ventana ───────────────

        VisualElement faccionVentana;
        /// <summary>Sin Facción: «inicio», «crear» o «unirse» (`estadoCliente.modoPanelFaccion`).</summary>
        string modoFaccion = "inicio";
        /// <summary>El sigilo con el que arranca el selector: al azar una vez (`sigiloPropuesto`), y lo que se va eligiendo.</summary>
        Sigilo sigiloPropuesto;
        string nombrePropuesto = "", buscarFaccion = "";
        bool ocultarBots = true;
        string firmaFaccion;

        public bool FaccionAbierta => faccionVentana?.panel != null;

        /// <summary>El botón «Facción» (la bandera) junto al yelmo de la Base, como en el prototipo.</summary>
        void BotonDeFaccion(VisualElement barra, Button yelmo)
        {
            var b = new Button(AlternarFaccion) { name = "faccion", tooltip = "Facción" };
            IconoDeBarra(b, "faccion");
            barra.Insert(barra.IndexOf(yelmo) + 1, b);
        }

        void AlternarFaccion()
        {
            if (FaccionAbierta) CerrarFaccion();
            else AbrirFaccion();
        }

        void AbrirFaccion()
        {
            var interior = SelectorDePersonaje.Interior;
            if (FaccionAbierta || HeroeAhora() == null || (interior != null && interior.PanelAbierto && !BaseAbierta)) return;
            if (BaseAbierta) CerrarBase();
            var pantallaMontada = pantalla.childCount > 0 ? pantalla[0] : null;
            if (pantallaMontada == null) return;
            if (interior != null) interior.PanelAbierto = true;
            if (mapa != null) mapa.EntradaApagada = true;
            var barra = pantallaMontada.Q(className: "barra-arriba");
            barra?.AddToClassList("barra--base");
            barra?.Q<Button>("faccion")?.AddToClassList("activo");
            Ver(pantallaMontada.Q("menu-barra"), false);
            faccionVentana = Dialogo.Caja("base-ventana faccion-ventana");
            faccionVentana.name = "faccion-ventana";
            // Las piezas comunes del prototipo (el letrero `.faction-kicker`) están en la hoja de la plaza.
            if (Resources.Load<StyleSheet>("MundoUI/Plaza") is StyleSheet hoja) faccionVentana.styleSheets.Add(hoja);
            pantallaMontada.Add(faccionVentana);
            modoFaccion = "inicio";
            firmaFaccion = null;
            // Las Facciones del mundo (su forma pública y el directorio) llegan por la `VISTA`; dentro de un interior sin haber pasado por el mapa
            // aún no se ha pedido: se pide la del mundo entero, como el mapa.
            if (c.Sesion != null && !c.Sesion.TieneVista && c.Relieve != null)
                c.Sesion.CambiarVista(new Rectangulo { Min = new Punto(), Max = new Punto { X = c.Relieve.Columnas * (long)c.Relieve.Paso, Y = c.Relieve.Filas * (long)c.Relieve.Paso } });
            PintarFaccion();
            RepintarCada(faccionVentana, 500, () => { if (FirmaDeFaccion() != firmaFaccion) PintarFaccion(); });
        }

        void CerrarFaccion()
        {
            if (!FaccionAbierta) return;
            var pantallaMontada = faccionVentana.parent;
            faccionVentana.RemoveFromHierarchy();
            faccionVentana = null;
            tecFijada = null;
            clicFueraDeTecnologia = null;
            var barra = pantallaMontada?.Q(className: "barra-arriba");
            barra?.RemoveFromClassList("barra--base");
            barra?.Q<Button>("faccion")?.RemoveFromClassList("activo");
            if (SelectorDePersonaje.Interior != null) SelectorDePersonaje.Interior.PanelAbierto = false;
            if (mapa != null) mapa.EntradaApagada = false;
        }

        /// <summary>Esc cierra la ventana de Facción.</summary>
        void TeclaDeFaccion()
        {
            var teclado = UnityEngine.InputSystem.Keyboard.current;
            if (teclado == null || !teclado.escapeKey.wasPressedThisFrame || !FaccionAbierta) return;
            // Con la ficha fija de una tecnología abierta, Esc la cierra a ella (`cerrarFicha` del tablero); si no, la ventana.
            if (pestanaFaccion == "tecnologia" && tecFijada != null && cerrarFichaDeTecnologia != null) cerrarFichaDeTecnologia();
            else CerrarFaccion();
        }

        /// <summary>Lo que repinta la ventana: el modo y, en la lista, las Facciones del directorio y tus solicitudes. Mientras se crea, el
        /// formulario es del jugador: no se repinta solo (el prototipo: «solo se repinta al cambiar de modo»).</summary>
        string FirmaDeFaccion()
        {
            var h = HeroeAhora();
            if (h != null && h.HasFaccionId) return FirmaDeTuFaccion(h);
            if (modoFaccion == "crear") return $"crear|{h?.HasFaccionId}";
            var a = c.Sesion.Almacen;
            var dir = a.Buscar(TipoEntidad.DirectorioFacciones, "facciones")?.DirectorioFacciones;
            var completas = string.Join(",", a.Todas(Entidad.CuerpoOneofCase.Faccion).Select(e => e.Faccion.Version));
            var ligeras = dir == null ? "" : string.Join(",", dir.Facciones.Select(x => $"{x.Id}:{x.Ciudadanos}:{x.Nivel}:{x.Derrotada}"));
            return $"{modoFaccion}|{h?.HasFaccionId}|{ligeras}|{completas}|{buscarFaccion}|{ocultarBots}";
        }

        void PintarFaccion()
        {
            if (!FaccionAbierta) return;
            firmaFaccion = FirmaDeFaccion();
            var h = HeroeAhora();
            if (h == null) return;
            if (h.HasFaccionId) { PintarTuFaccion(h); return; }
            faccionVentana.Clear();
            var cab = Dialogo.Caja("faccion-cabecera");
            cab.Add(Dialogo.Texto(h.DisplayName.ToUpperInvariant(), "faccion-titulo"));
            var cerrar = Dialogo.Boton("Cerrar", "boton-secundario base-cerrar", CerrarFaccion);
            cerrar.name = "faccion-cerrar";
            cab.Add(cerrar);
            faccionVentana.Add(cab);
            var cuerpo = Dialogo.Caja("faccion-cuerpo");
            cuerpo.name = "faccion-cuerpo";
            faccionVentana.Add(cuerpo);
            if (modoFaccion == "crear") PintarCrearFaccion(cuerpo);
            else if (modoFaccion == "unirse") PintarPedirIngreso(cuerpo);
            else PintarEligeFaccion(cuerpo);
        }

        void CambiarModoFaccion(string modo)
        {
            modoFaccion = modo;
            firmaFaccion = null;
            PintarFaccion();
        }

        Button BotonVolver()
        {
            var b = Dialogo.Boton("←", "faccion-volver", () => CambiarModoFaccion("inicio"));
            b.name = "faccion-volver";
            b.tooltip = "Volver a elegir facción";
            return b;
        }

        // ─────────────── «Elige tu facción» ───────────────

        void PintarEligeFaccion(VisualElement v)
        {
            var vista = Dialogo.Caja("faccion-vista");
            vista.Add(Kicker("Organización política"));
            vista.Add(ConAyuda(Dialogo.Texto("Elige tu facción", "faccion-h2"), "Crea una nueva o pide entrar en una existente: su Rey decide."));
            var rejilla = Dialogo.Caja("faccion-eleccion");
            Button Opcion(string nombre, string icono, string titulo, string texto, string modo)
            {
                var b = new Button(() => CambiarModoFaccion(modo)) { name = nombre };
                b.AddToClassList("faccion-opcion");
                b.Add(Dialogo.Texto(icono, "faccion-opcion-icono"));
                b.Add(Dialogo.Texto(titulo, "faccion-opcion-titulo"));
                b.Add(Dialogo.Texto(texto, "faccion-opcion-texto"));
                return b;
            }
            rejilla.Add(Opcion("btn-unirse-faccion", "↗", "Pedir ingreso", "Explora las facciones del mundo.", "unirse"));
            rejilla.Add(Opcion("btn-crear-faccion", "✦", "Crear facción", "Funda una nueva casa política.", "crear"));
            vista.Add(rejilla);
            v.Add(vista);
        }

        // ─────────────── «Crear facción» ───────────────

        void PintarCrearFaccion(VisualElement v)
        {
            sigiloPropuesto ??= Sigilos.Aleatorio(new System.Random());
            var desplazable = new ScrollView(ScrollViewMode.Vertical) { name = "faccion-crear" };
            desplazable.AddToClassList("faccion-desplazable");
            var vista = Dialogo.Caja("faccion-vista faccion-formulario");
            var cab = Dialogo.Caja("faccion-vista-cab");
            cab.Add(BotonVolver());
            cab.Add(Kicker("Nueva identidad"));
            vista.Add(cab);
            vista.Add(ConAyuda(Dialogo.Texto("Crear facción", "faccion-h2"), "El nombre será visible para todos los jugadores."));

            vista.Add(Dialogo.Texto("Nombre de la facción", "faccion-etiqueta"));
            var nombre = new TextField { name = "input-nombre-faccion", maxLength = 32, value = nombrePropuesto };
            nombre.AddToClassList("entrada");
            nombre.textEdition.placeholder = "Ej. Casa de Micenas";
            nombre.textEdition.hidePlaceholderOnFocus = true;
            vista.Add(nombre);

            var marco = Dialogo.Caja("sigilo-selector");
            marco.name = "sigilo-selector";
            marco.Add(Dialogo.Texto("Sigilo (no se podrá cambiar)", "sigilo-leyenda"));
            var previa = SigiloUI(sigiloPropuesto, 88, false, "sigilo-previa");
            previa.name = "sigilo-previa";
            marco.Add(previa);
            var motivos = Dialogo.Caja("faccion-error");
            motivos.name = "error-faccion";
            Button crear = null;
            var tocado = false;
            void Validar()
            {
                var lista = MotivosParaCrearFaccion(nombre.value, sigiloPropuesto);
                motivos.Clear();
                if (tocado) foreach (var m in lista) motivos.Add(Dialogo.Texto("• " + m, "faccion-motivo"));
                crear?.SetEnabled(lista.Count == 0);
            }
            var colores = Sigilos.Colores.Select(x => x.Id).ToList();
            string NombreDeColor(string id) => Sigilos.Colores.FirstOrDefault(x => x.Id == id)?.Nombre ?? id;
            void Lista(string id, string etiqueta, IReadOnlyList<string> ids, Func<Sigilo, string> leer, Action<Sigilo, string> poner, Func<string, string> nombreDe = null)
            {
                nombreDe ??= Sigilos.NombreDeId;
                var campo = new DropdownField(etiqueta, ids.ToList(), Math.Max(0, ids.ToList().IndexOf(leer(sigiloPropuesto))), nombreDe, nombreDe) { name = id };
                campo.AddToClassList("sigilo-campo");
                campo.RegisterValueChangedCallback(e =>
                {
                    poner(sigiloPropuesto, e.newValue);
                    PonerSigilo(previa, sigiloPropuesto, 88);
                    tocado = true;
                    Validar();
                });
                marco.Add(campo);
            }
            Lista("sigilo-forma", "Forma del escudo", Sigilos.Formas, s => s.FormaId, (s, x) => s.FormaId = x);
            Lista("sigilo-campo", "Fondo", Sigilos.Campos, s => s.CampoId, (s, x) => s.CampoId = x);
            Lista("sigilo-color1", "Color principal", colores, s => s.ColorPrimarioId, (s, x) => s.ColorPrimarioId = x, NombreDeColor);
            Lista("sigilo-color2", "Color secundario", colores, s => s.ColorSecundarioId, (s, x) => s.ColorSecundarioId = x, NombreDeColor);
            Lista("sigilo-emblema", "Emblema", Sigilos.Emblemas, s => s.EmblemaId, (s, x) => s.EmblemaId = x);
            Lista("sigilo-colorEmblema", "Color del emblema", colores, s => s.ColorEmblemaId, (s, x) => s.ColorEmblemaId = x, NombreDeColor);
            Lista("sigilo-orla", "Orla", Sigilos.Orlas, s => s.OrlaId, (s, x) => s.OrlaId = x);
            Lista("sigilo-colorOrla", "Color de la orla", colores, s => s.ColorOrlaId, (s, x) => s.ColorOrlaId = x, NombreDeColor);
            marco.Add(ConAyuda(Dialogo.Texto("Créditos", "sigilo-creditos"),
                "Emblemas: game-icons.net (CC BY 3.0) — Lorc, Delapouite, Caro Asercion, Cathelineau, Skoll y Willdabeast."));
            vista.Add(marco);
            vista.Add(motivos);
            var error = Dialogo.Texto("", "error oculto");
            error.name = "error-crear-faccion";
            crear = Dialogo.Boton("Crear facción", "boton-primario faccion-crear-boton", async () =>
            {
                tocado = true;
                if (MotivosParaCrearFaccion(nombre.value, sigiloPropuesto).Count > 0) { Validar(); return; }
                crear.text = "Creando...";
                var s = sigiloPropuesto;
                var orden = new Orden { CrearFaccion = new OrdenCrearFaccion { Nombre = nombre.value.Trim(), Sigilo = new OrdenCrearFaccion.Types.Sigilo
                {
                    FormaId = s.FormaId, CampoId = s.CampoId, EmblemaId = s.EmblemaId, ColorPrimarioId = s.ColorPrimarioId, ColorSecundarioId = s.ColorSecundarioId,
                    ColorEmblemaId = s.ColorEmblemaId, OrlaId = s.OrlaId, ColorOrlaId = s.ColorOrlaId,
                } } };
                var r = await OrdenDelPanel(orden, m => { error.text = m ?? ""; Ver(error, m != null); }, () => { }, crear);
                if (r?.Resultado == Resultado.OrdenAceptada)
                {
                    sigiloPropuesto = null;
                    nombrePropuesto = "";
                    modoFaccion = "inicio";
                    pestanaFaccion = "miembros";
                    firmaFaccion = null;
                }
                else if (crear != null) crear.text = "Crear facción";
            });
            crear.name = "btn-submit-crear-faccion";
            nombre.RegisterValueChangedCallback(e => { nombrePropuesto = e.newValue; tocado = true; Validar(); });
            vista.Add(error);
            vista.Add(crear);
            Validar();
            desplazable.Add(vista);
            v.Add(desplazable);
        }

        /// <summary>Todos los motivos por los que el motor rechazaría el nombre y el sigilo (`motivosParaCrearFaccion`); vacío: nada que objetar.</summary>
        List<string> MotivosParaCrearFaccion(string nombre, Sigilo s)
        {
            var motivos = new List<string>();
            var conocidas = FaccionesConocidas();
            var limpio = (nombre ?? "").Trim();
            if (limpio == "") motivos.Add("Escribe un nombre para la Facción.");
            else if (conocidas.FirstOrDefault(f => string.Equals(f.nombre.Trim(), limpio, StringComparison.OrdinalIgnoreCase)) is var igual && igual.id != null)
                motivos.Add($"Ya existe una Facción llamada «{igual.nombre}»: elige otro nombre (no distingue mayúsculas).");
            if (s == null || string.IsNullOrEmpty(s.CampoId)) motivos.Add("Elige el sigilo completo: forma, fondo y colores.");
            else
            {
                var colores = Sigilos.Colores.Select(x => x.Id).ToList();
                if (!Sigilos.Formas.Contains(s.FormaId) || !Sigilos.Campos.Contains(s.CampoId) || !Sigilos.Emblemas.Contains(s.EmblemaId) || !Sigilos.Orlas.Contains(s.OrlaId)
                    || !new[] { s.ColorPrimarioId, s.ColorSecundarioId, s.ColorEmblemaId, s.ColorOrlaId }.All(colores.Contains))
                    motivos.Add("Alguna pieza del sigilo no es del catálogo del servidor: recarga la página.");
                if (s.ColorPrimarioId == s.ColorSecundarioId) motivos.Add("El color principal y el secundario del fondo son el mismo: cambia uno de los dos.");
                var repetido = conocidas.FirstOrDefault(f => f.sigilo != null && MismoSigilo(f.sigilo, s));
                if (repetido.id != null) motivos.Add($"La Facción «{repetido.nombre}» ya lleva exactamente este sigilo: cambia alguna pieza o color (los parecidos sí valen).");
            }
            return motivos;
        }

        /// <summary>Idénticos a ojos del motor: el color de la orla no cuenta si no hay orla.</summary>
        static bool MismoSigilo(Sigilo a, Sigilo b) =>
            a.FormaId == b.FormaId && a.CampoId == b.CampoId && a.EmblemaId == b.EmblemaId && a.ColorPrimarioId == b.ColorPrimarioId && a.ColorSecundarioId == b.ColorSecundarioId
            && a.ColorEmblemaId == b.ColorEmblemaId && a.OrlaId == b.OrlaId && (a.OrlaId == "ninguna" || a.ColorOrlaId == b.ColorOrlaId);

        /// <summary>Todas las Facciones que conoces: las completas del almacén y las del directorio (`VISTA`), sin repetir.</summary>
        List<(string id, string nombre, Sigilo sigilo, Faccion completa, FaccionDelDirectorio ligera)> FaccionesConocidas()
        {
            var a = c.Sesion.Almacen;
            var completas = a.Todas(Entidad.CuerpoOneofCase.Faccion).Select(e => e.Faccion).ToList();
            var dir = a.Buscar(TipoEntidad.DirectorioFacciones, "facciones")?.DirectorioFacciones?.Facciones.ToList() ?? new List<FaccionDelDirectorio>();
            var r = completas.Select(f => (f.Id, f.Nombre, f.Sigilo, f, dir.FirstOrDefault(d => d.Id == f.Id))).ToList();
            r.AddRange(dir.Where(d => completas.All(f => f.Id != d.Id)).Select(d => (d.Id, d.Nombre, d.Sigilo, (Faccion)null, d)));
            return r;
        }

        // ─────────────── «Pedir ingreso» ───────────────

        void PintarPedirIngreso(VisualElement v)
        {
            var vista = Dialogo.Caja("faccion-vista faccion-unirse");
            var cab = Dialogo.Caja("faccion-vista-cab");
            cab.Add(BotonVolver());
            cab.Add(Kicker("Facciones del mundo"));
            vista.Add(cab);
            vista.Add(Dialogo.Texto("Pedir ingreso", "faccion-h2"));
            var buscar = Dialogo.Caja("faccion-buscar");
            var campo = new TextField { name = "input-buscar-faccion", value = buscarFaccion };
            campo.AddToClassList("entrada");
            campo.AddToClassList("faccion-buscar-campo");
            campo.textEdition.placeholder = "Buscar por nombre...";
            campo.textEdition.hidePlaceholderOnFocus = true;
            campo.RegisterValueChangedCallback(e => { buscarFaccion = e.newValue; PintarListaDeFacciones(vista.Q("lista-facciones")); firmaFaccion = FirmaDeFaccion(); });
            buscar.Add(campo);
            var bots = new Toggle("Ocultar las de bots") { name = "check-sin-bots", value = ocultarBots, tooltip = "Un jugador no puede entrar en una Facción de bots" };
            bots.AddToClassList("faccion-sin-bots");
            bots.RegisterValueChangedCallback(e => { ocultarBots = e.newValue; PintarListaDeFacciones(vista.Q("lista-facciones")); firmaFaccion = FirmaDeFaccion(); });
            buscar.Add(bots);
            vista.Add(buscar);
            var lista = new ScrollView(ScrollViewMode.Vertical) { name = "lista-facciones" };
            lista.AddToClassList("faccion-lista");
            vista.Add(lista);
            PintarListaDeFacciones(lista);
            v.Add(vista);
        }

        /// <summary>La lista de «Pedir ingreso» (`htmlListaFacciones` sin Facción): todas las que llegan completas con su detalle, las de bots al
        /// final o fuera; debajo, «Sin contacto», las del directorio con su escudo pequeño. Cada fila, con su «Pedir ingreso».</summary>
        void PintarListaDeFacciones(VisualElement lista)
        {
            if (lista == null) return;
            lista.Clear();
            var a = c.Sesion.Almacen;
            var yo = HeroeAhora();
            var termino = (buscarFaccion ?? "").Trim().ToLowerInvariant();
            var todas = FaccionesConocidas();
            bool EsBot((string id, string nombre, Sigilo sigilo, Faccion completa, FaccionDelDirectorio ligera) f) => (f.completa?.EsBot ?? false) || (f.ligera?.EsBot ?? false);
            int Miembros((string id, string nombre, Sigilo sigilo, Faccion completa, FaccionDelDirectorio ligera) f) => f.completa?.CiudadanosIds.Count ?? (int)(f.ligera?.Ciudadanos ?? 0);
            var filtradas = todas.Where(f => f.nombre.ToLowerInvariant().Contains(termino) && (!ocultarBots || !EsBot(f))).ToList();
            var conocidas = filtradas.Where(f => f.completa != null)
                .OrderBy(f => f.completa.HasDerrotadaPor ? 1 : 0).ThenBy(f => EsBot(f) ? 1 : 0).ThenByDescending(Miembros).ThenBy(f => f.nombre).ToList();
            var lejanas = filtradas.Where(f => f.completa == null && f.ligera != null && !f.ligera.Derrotada)
                .OrderBy(f => EsBot(f) ? 1 : 0).ThenByDescending(Miembros).ThenBy(f => f.nombre).ToList();
            if (conocidas.Count == 0 && lejanas.Count == 0)
            {
                lista.Add(Dialogo.Texto(termino != "" ? "No hay facciones que coincidan." : ocultarBots ? "No hay Facciones de jugadores todavía. Quita «Ocultar las de bots» para ver las demás."
                    : "Aún no hay Facciones en el mundo.", "faccion-vacia"));
                return;
            }
            var plazas = a.Todas(Entidad.CuerpoOneofCase.Asentamiento).Select(e => e.Asentamiento).ToList();
            string NombreDeHeroe(string id) => Textos.NombreDeHeroe(a, id);
            string NombreDe(string id) => todas.FirstOrDefault(x => x.id == id).nombre ?? id;
            VisualElement Accion((string id, string nombre, Sigilo sigilo, Faccion completa, FaccionDelDirectorio ligera) f)
            {
                if (f.completa != null && yo != null && f.completa.SolicitudesIds.Contains(yo.Id)) return Dialogo.Texto("Pedido: decide su Rey", "faccion-pedido");
                if (EsBot(f)) { var t = Dialogo.Texto("🤖 De bots", "faccion-pedido"); t.tooltip = "Un jugador no entra en una Facción de bots"; return t; }
                var caja = Dialogo.Caja("faccion-accion");
                Button b = null;
                b = Dialogo.Boton("Pedir ingreso", "faccion-pedir", async () =>
                {
                    b.text = "Pidiendo...";
                    var r = await OrdenDelPanel(new Orden { SolicitarIngreso = new OrdenSolicitarIngreso { FaccionId = f.id } },
                        m => { if (m != null) caja.Add(Dialogo.Texto(m, "faccion-error-texto")); }, () => { firmaFaccion = null; }, b);
                    b.text = r?.Resultado == Resultado.OrdenAceptada ? "Pedido: decide su Rey" : "Pedir ingreso";
                    if (r?.Resultado == Resultado.OrdenAceptada) b.SetEnabled(false);
                });
                b.name = "pedir-" + f.id;
                caja.Add(b);
                return caja;
            }
            var bloque = Dialogo.Caja("faccion-grupo");
            bloque.Add(ConAyuda(Kicker($"Facciones del mundo ({conocidas.Count})"),
                "Con contacto: la tuya, las de tu Liga, con las que tienes relación y aquellas de las que conoces alguna plaza en el mapa. Las plazas son las que conoces, no todas las que tienen."));
            if (conocidas.Count == 0) bloque.Add(Dialogo.Texto("Aún no has tenido contacto con ninguna.", "faccion-vacia"));
            foreach (var f in conocidas)
            {
                var x = f.completa;
                var fila = Dialogo.Caja("faccion-publica" + (x.HasDerrotadaPor ? " derrotada" : ""));
                fila.name = "faccion-" + f.id;
                var granRey = x.Liga != null && x.Liga.GranReyId == x.Id;
                fila.Add(SigiloUI(x.Sigilo, 56, granRey, "faccion-publica-escudo"));
                var info = Dialogo.Caja("faccion-publica-info");
                var derrota = !x.HasDerrotadaPor ? "" : x.DerrotadaPor != "" ? $"Derrotada por {NombreDe(x.DerrotadaPor)}" : "Desaparecida";
                var titulo = Dialogo.Caja("faccion-publica-nombre");
                titulo.Add(Dialogo.Texto(x.Nombre, "faccion-publica-titulo"));
                if (derrota != "") titulo.Add(Dialogo.Texto(derrota, "faccion-publica-relacion rel-derrota"));
                info.Add(titulo);
                var capital = x.HasCapitalId ? plazas.FirstOrDefault(p => p.Id == x.CapitalId) : null;
                var liga = x.Liga == null ? "" : $"Liga {(x.Liga.TieneVasallaje ? "por vasallaje" : "por alianza")}" + (x.Liga.GranReyId == x.Id ? " · Gran Rey" : x.Liga.HasGranReyId ? $" de {NombreDe(x.Liga.GranReyId)}" : "");
                var datos = new[]
                {
                    $"Nivel {x.Nivel}",
                    $"Prestigio {x.Prestigio.ToString("N0", new System.Globalization.CultureInfo("es-ES"))}",
                    $"Reputación {Math.Round((x.Reputacion?.ValorEnT0 ?? 0) / 1000.0)}",
                    $"Rey: {(x.HasReyId ? NombreDeHeroe(x.ReyId) : "sin designar")}",
                    x.HasEmbajadorId ? $"Embajador: {NombreDeHeroe(x.EmbajadorId)}" : "",
                    capital != null ? $"Capital: {(capital.HasNombre ? capital.Nombre : capital.Id)}" : "",
                    liga,
                    x.AdmiteOtrasEnAtaques ? "Admite a otras Facciones en sus ataques" : "",
                }.Where(t => t != "");
                info.Add(Dialogo.Texto(string.Join(" · ", datos), "faccion-publica-datos"));
                fila.Add(info);
                var cifras = Dialogo.Caja("faccion-publica-cifras");
                void Cifra(string n, string q) { var cc = Dialogo.Caja("faccion-cifra"); cc.Add(Dialogo.Texto(n, "faccion-cifra-n")); cc.Add(Dialogo.Texto(q, "faccion-cifra-q")); cifras.Add(cc); }
                Cifra(Miembros(f).ToString(), "MIEMBROS");
                Cifra(plazas.Count(p => p.FaccionId == f.id).ToString(), "PLAZAS");
                fila.Add(cifras);
                fila.Add(Accion(f));
                bloque.Add(fila);
            }
            lista.Add(bloque);
            if (lejanas.Count > 0)
            {
                var sin = Dialogo.Caja("faccion-grupo");
                sin.Add(Kicker($"Sin contacto ({lejanas.Count})"));
                foreach (var f in lejanas)
                {
                    var fila = Dialogo.Caja("faccion-lejana");
                    fila.name = "faccion-" + f.id;
                    fila.Add(SigiloUI(f.sigilo, 30, false, "faccion-lejana-escudo"));
                    var nombre = Dialogo.Caja("faccion-lejana-nombre");
                    nombre.Add(Dialogo.Texto(f.nombre, "faccion-lejana-titulo"));
                    var m = Miembros(f);
                    nombre.Add(Dialogo.Texto($"Nivel {f.ligera.Nivel} · {m} miembro{(m == 1 ? "" : "s")}", "faccion-lejana-datos"));
                    fila.Add(nombre);
                    fila.Add(Accion(f));
                    sin.Add(fila);
                }
                lista.Add(sin);
            }
        }
    }
}
