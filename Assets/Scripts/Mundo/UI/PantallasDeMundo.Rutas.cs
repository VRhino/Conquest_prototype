// El comercio a distancia del Mercado (paso 10, tanda 3), calcado de `ui/encargoCaravana.ts`, `ui/panelTrueques.ts` y `ui/tabernaRegion.ts`:
// - el Encargo de una caravana de la flota (`encargarCaravana`, `cambiarEncargo`, `cancelarEncargo`, `detenerEncargo`): una ruta permanente entre
//   plazas de la Facción o un envío de ida y vuelta (también a una ajena), con sus dos paradas (primero deja, luego recoge; en una ajena, vende y
//   compra a sus órdenes con precio). En la caravana, «Viaje» o «Encargo»; con encargo, la ruta dibujada con la carreta y sus paradas en fichas;
// - la pestaña Rutas: las caravanas de la plaza con encargo;
// - la pestaña Trueques: los pactos en curso y cerrados, aceptar y rechazar los recibidos, y proponer uno (`proponerTrueque`);
// - la pestaña Región (con un informe de órdenes vigente): las órdenes de las plazas ajenas de la región, para tomarlas a distancia
//   (`tomarOrdenADistancia`);
// - la Taberna de la plaza, por ahora solo con el informe de la región (`comprarInformeRegion`).
// Los encargos los dan el Gobernador, el Tesorero o el Maestro de Caravanas, residentes; los trueques, el Gobernador o el Tesorero; el informe,
// el Rey, el Embajador o el Gobernador. Lo que no se puede hacer no sale.
using System;
using System.Collections.Generic;
using System.Linq;
using BronzeAge.Contract.V1;
using ConquestTactics.Mundo.Mapa;
using Newtonsoft.Json.Linq;
using UnityEngine.UIElements;

namespace ConquestTactics.Mundo.UI
{
    public partial class PantallasDeMundo
    {
        /// <summary>Viaje o Encargo, por caravana; los formularios de encargo abiertos y sus borradores (sobreviven a los repintados).</summary>
        readonly Dictionary<string, string> modoDeCaravana = new Dictionary<string, string>();
        readonly HashSet<string> encargoAbierto = new HashSet<string>();
        readonly Dictionary<string, (string modo, string destino, ParadaEncargo origen, ParadaEncargo destinoParada)> borradorDeEncargo =
            new Dictionary<string, (string, string, ParadaEncargo, ParadaEncargo)>();
        int truequeDestino;
        readonly (string recurso, int cantidad)[] truequeDa = new (string, int)[3], truequeRecibe = new (string, int)[3];

        /// <summary>¿Da encargos aquí? El Gobernador, el Tesorero o el Maestro de Caravanas, residente.</summary>
        bool GestionaEncargos(Asentamiento a, Heroe yo) => Reside(a, yo)
            && (TitularDe(a, "gobernador") == yo.Id || TitularDe(a, "tesorero") == yo.Id || TitularDe(a, "maestroCaravanas") == yo.Id);

        /// <summary>Las plazas que conoces: las de tu Facción primero; (id, nombre, propia).</summary>
        List<(string id, string nombre, bool propia)> PlazasConocidas(string sin, Heroe yo)
        {
            var al = c.Sesion.Almacen;
            return al.Todas(Entidad.CuerpoOneofCase.Asentamiento).Select(e => (e.Asentamiento.Id, e.Asentamiento.HasNombre ? e.Asentamiento.Nombre : e.Asentamiento.Id, e.Asentamiento.FaccionId == yo.FaccionId))
                .Concat(al.Todas(Entidad.CuerpoOneofCase.FichaConocida).Select(e => (e.FichaConocida.AsentamientoId, e.FichaConocida.HasNombre ? e.FichaConocida.Nombre : e.FichaConocida.AsentamientoId, e.FichaConocida.FaccionId == yo.FaccionId)))
                .Where(x => x.Item1 != sin).GroupBy(x => x.Item1).Select(g => g.First())
                .OrderByDescending(x => x.Item3).ThenBy(x => x.Item2, StringComparer.CurrentCulture).ToList();
        }

        // ─────────────────────────── el Encargo de una caravana ───────────────────────────

        /// <summary>Abajo en la caravana elegida: con encargo, la ruta; sin él y parada, «Viaje» o «Encargo» (si los das).</summary>
        void ViajeOEncargo(VisualElement caja, Caravana k, Asentamiento a, Heroe yo, int capacidad, Action rehacer)
        {
            if (k.Encargo != null) { CajaDeEncargo(caja, k, a, yo, capacidad, rehacer); return; }
            if (!GestionaEncargos(a, yo)) { Viaje(caja, k, a, yo, capacidad, rehacer); return; }
            var modo = modoDeCaravana.TryGetValue(k.Id, out var m) ? m : "viaje";
            var tabs = Dialogo.Caja("plaza-subtabs caravana-modos");
            foreach (var (id, nombre) in new[] { ("viaje", "Viaje"), ("encargo", "Encargo") })
            {
                var elegido = id;
                var b = Dialogo.Boton(nombre, "plaza-subtab" + (id == modo ? " activo" : ""), () => { modoDeCaravana[k.Id] = elegido; rehacer(); });
                b.name = "caravana-modo-" + id;
                tabs.Add(b);
            }
            caja.Add(tabs);
            if (modo == "viaje") Viaje(caja, k, a, yo, capacidad, rehacer);
            else FormularioDeEncargo(caja, k, a, yo, capacidad, rehacer);
        }

        void CajaDeEncargo(VisualElement caja, Caravana k, Asentamiento a, Heroe yo, int capacidad, Action rehacer)
        {
            var e = k.Encargo;
            var seccion = Dialogo.Caja("caravana-seccion encargo");
            seccion.name = "encargo-" + k.Id;
            var cab = Dialogo.Caja("caravana-seccion-cab");
            cab.Add(Kicker(e.Modo == "ruta" ? "Ruta" : "Envío"));
            if (!e.Cancelado && GestionaEncargos(a, yo))
            {
                var botones = Dialogo.Caja("caravana-botones");
                if (!e.Detener)
                {
                    var abierto = encargoAbierto.Contains(k.Id);
                    var cambiar = Dialogo.Boton(abierto ? "▾ Cerrar" : "✎ Cambiar", "boton-secundario", () => { if (!encargoAbierto.Remove(k.Id)) encargoAbierto.Add(k.Id); else borradorDeEncargo.Remove(k.Id); rehacer(); });
                    cambiar.name = "encargo-cambiar";
                    botones.Add(cambiar);
                }
                if (e.Modo == "ruta" && !e.Detener)
                    botones.Add(BotonConfirmado("⏹ Detener ruta", "encargo-detener", "¿Detener la ruta?", "Si va de camino, acaba esta ida y vuelta comerciando y al volver se queda sin ruta; si está en el origen, se queda ya sin ella.",
                        "Detener la ruta", () => new Orden { DetenerEncargo = new OrdenDetenerEncargo { CaravanaId = k.Id } }, rehacer));
                botones.Add(BotonConfirmado("Cancelar encargo", "encargo-cancelar", "¿Cancelar el encargo?", "La caravana termina lo que esté haciendo y vuelve a su origen.",
                    "Cancelar el encargo", () => new Orden { CancelarEncargo = new OrdenCancelarEncargo { CaravanaId = k.Id } }, rehacer));
                cab.Add(botones);
            }
            seccion.Add(cab);
            seccion.Add(RutaDibujada(k, a, yo));
            if (encargoAbierto.Contains(k.Id) && !e.Cancelado) FormularioDeEncargo(seccion, k, a, yo, capacidad, rehacer);
            caja.Add(seccion);
        }

        /// <summary>Un botón que pide confirmación antes de mandar su orden.</summary>
        Button BotonConfirmado(string texto, string nombre, string titulo, string cuerpoTexto, string aceptar, Func<Orden> orden, Action rehacer)
        {
            Button b = null;
            b = Dialogo.Boton(texto, "boton-secundario", async () =>
            {
                if (!await dialogo.Abrir(cuerpoTexto, titulo, aceptar, "Seguir", true)) return;
                if (await OrdenDePlaza(orden(), b)) rehacer();
            });
            b.name = nombre;
            return b;
        }

        /// <summary>La ruta dibujada (`htmlRutaVisual`): origen y destino con la carreta en el camino, la fase, las vueltas y las paradas en fichas.</summary>
        VisualElement RutaDibujada(Caravana k, Asentamiento a, Heroe yo)
        {
            var e = k.Encargo;
            var destino = PlazasConocidas(a.Id, yo).FirstOrDefault(x => x.id == e.DestinoAsentamientoId);
            var ajena = destino.id != null && !destino.propia;
            var vis = Dialogo.Caja("ruta-vis");
            var enCamino = k.Estado is EstadoCaravana.CaravanaEnTransito or EstadoCaravana.CaravanaRetornando;
            var linea = Dialogo.Caja("ruta-linea");
            VisualElement Punta(string nombre, string rol, bool aqui, bool deOtro)
            {
                var p = Dialogo.Caja("ruta-punta" + (aqui ? " aqui" : ""));
                p.Add(Dialogo.Texto(deOtro ? "🏴" : "🏛", "ruta-punta-icono"));
                p.Add(Dialogo.Texto(nombre, "ruta-punta-nombre"));
                p.Add(Dialogo.Texto(rol + (deOtro ? " · ajena" : ""), "plaza-sub"));
                return p;
            }
            linea.Add(Punta(a.HasNombre ? a.Nombre : a.Id, "origen", !enCamino && e.ParadaActual == "origen", false));
            var pista = Dialogo.Caja("ruta-pista");
            pista.Add(Dialogo.Caja("ruta-camino"));
            var t = e.ParadaActual == "destino" ? 0.97 : 0.03;
            if (enCamino && c.Sesion.Almacen.Buscar(TipoEntidad.Asentamiento, e.DestinoAsentamientoId)?.Asentamiento?.Posicion is Punto d)
            {
                var o = a.Posicion;
                double dx = d.X - o.X, dy = d.Y - o.Y, l2 = dx * dx + dy * dy;
                var p = Instante.Posicion(k.Plan, k.Posicion, AhoraMs);
                t = l2 > 0 ? Math.Min(0.92, Math.Max(0.08, ((p.x - o.X) * dx + (p.y - o.Y) * dy) / l2)) : 0.5;
            }
            var carreta = Dialogo.Texto("🛞", "ruta-carreta");
            carreta.style.left = Length.Percent((float)(t * 100));
            pista.Add(carreta);
            pista.Add(Dialogo.Texto(e.Modo == "ruta" ? "⇄" : "→ ←", "ruta-sentido"));
            linea.Add(pista);
            linea.Add(Punta(destino.nombre ?? NombreDePlaza(e.DestinoAsentamientoId), "destino", !enCamino && e.ParadaActual == "destino", ajena));
            vis.Add(linea);
            var estado = Dialogo.Caja("ruta-estado");
            estado.Add(Dialogo.Texto(e.Cancelado ? "✕ Cancelado: termina al volver" : Situacion(k), "ruta-fase"));
            estado.Add(Chip(e.Modo == "ruta" ? "⇄ Ruta permanente" : "→ Envío de ida y vuelta"));
            if (e.Modo == "ruta") estado.Add(Chip($"↻ {e.Vueltas} {(e.Vueltas == 1 ? "vuelta" : "vueltas")}"));
            if (e.Detener && !e.Cancelado) estado.Add(Dialogo.Texto("⏹ Última vuelta: vuelve y se queda sin ruta", "ruta-fase detenida"));
            vis.Add(estado);
            if (k.Estado == EstadoCaravana.CaravanaEsperando && (e.Fase == "carga" || e.Fase == "descarga") && e.HasFinPasoMs)
            {
                var inicio = e.HasInicioPasoMs ? e.InicioPasoMs : (long)AhoraMs;
                vis.Add(BarraAvance((float)Math.Clamp((AhoraMs - inicio) / Math.Max(1.0, e.FinPasoMs - inicio), 0, 1)));
            }
            var paradas = Dialogo.Caja("ruta-paradas");
            paradas.Add(ParadaEnFichas($"En {(a.HasNombre ? a.Nombre : "el origen")}", e.ParadaOrigen, false));
            paradas.Add(ParadaEnFichas($"En {destino.nombre ?? NombreDePlaza(e.DestinoAsentamientoId)}", e.ParadaDestino, ajena));
            vis.Add(paradas);
            return vis;
        }

        string Situacion(Caravana k)
        {
            var e = k.Encargo;
            var donde = e.ParadaActual == "origen" ? "el origen" : "el destino";
            if (k.Estado == EstadoCaravana.CaravanaEnTransito) return "De camino al destino";
            if (k.Estado == EstadoCaravana.CaravanaRetornando) return "De vuelta al origen";
            if (k.Estado != EstadoCaravana.CaravanaEsperando) return "Parada";
            if (e.Fase == "descarga") return $"Descargando en {donde}";
            if (e.Fase == "carga") return $"Cargando en {donde}";
            if (e.HasSalirEnMs && e.SalirEnMs > AhoraMs && e.Vueltas == 0 && e.ParadaActual == "origen") return $"Espera en el origen: sale en {FaltaPara(e.SalirEnMs)}";
            return $"Esperando en {donde}{(e.Fase == "espera" ? " a que haya existencias" : "")}";
        }

        static string Cifra(long milesimas) => (milesimas / 1000.0).ToString(milesimas % 1000 == 0 ? "0" : "0.0");

        VisualElement ParadaEnFichas(string titulo, ParadaEncargo x, bool ajena)
        {
            var caja = Dialogo.Caja("ruta-parada");
            caja.Add(Dialogo.Texto(titulo, "ruta-parada-cab"));
            VisualElement Fila(string verbo, IEnumerable<(string r, string cifra, string precio)> fichas, string marca)
            {
                var f = Dialogo.Caja("ruta-accion");
                f.Add(Dialogo.Texto(verbo, "ruta-verbo"));
                var n = 0;
                foreach (var (r, cifra, precio) in fichas)
                {
                    n++;
                    var ficha = IconosDelMundo.ChipDeRecurso(r, cifra + precio, CosasDelMapa.NombreDeRecurso(r));
                    f.Add(ficha);
                }
                if (n == 0) f.Add(Dialogo.Texto("nada", "ruta-nada"));
                if (marca != null) f.Add(Dialogo.Texto(marca, "ruta-marca"));
                return f;
            }
            caja.Add(Fila(ajena ? "⬇ Vende" : "⬇ Deja", (x?.Dejar ?? new Google.Protobuf.Collections.RepeatedField<LineaDejar>()).Select(l => (l.Recurso, l.HasTope ? "≤ " + Cifra(l.Tope) : "todo", l.HasPrecioMin ? " · a ≥ " + Cifra(l.PrecioMin) : "")),
                x?.DesecharSobrante == true ? "tira lo que sobre" : null));
            caja.Add(Fila(ajena ? "⬆ Compra" : "⬆ Recoge", (x?.Recoger ?? new Google.Protobuf.Collections.RepeatedField<LineaRecoger>()).Select(l => (l.Recurso, Cifra(l.Cantidad), l.HasPrecioMax ? " · a ≤ " + Cifra(l.PrecioMax) : "")),
                x?.EsperarExistencias == true ? "espera a tenerlo" : null));
            return caja;
        }

        /// <summary>El formulario del encargo (`htmlFormulario`): el modo (sin encargo), el destino, salir dentro de N min (el envío) y las dos paradas.</summary>
        void FormularioDeEncargo(VisualElement caja, Caravana k, Asentamiento a, Heroe yo, int capacidad, Action rehacer)
        {
            var lista = PlazasConocidas(a.Id, yo);
            if (lista.Count == 0) return;
            if (!borradorDeEncargo.TryGetValue(k.Id, out var b))
                b = (k.Encargo?.Modo ?? "ruta", k.Encargo?.DestinoAsentamientoId ?? lista.First().id, k.Encargo?.ParadaOrigen?.Clone() ?? new ParadaEncargo(), k.Encargo?.ParadaDestino?.Clone() ?? new ParadaEncargo());
            // Una ruta solo va a plazas tuyas.
            var validas = b.modo == "ruta" ? lista.Where(x => x.propia).ToList() : lista;
            if (validas.Count == 0) { b.modo = "envio"; validas = lista; }
            if (!validas.Any(x => x.id == b.destino)) b.destino = validas[0].id;
            borradorDeEncargo[k.Id] = b;
            var ajena = !validas.First(x => x.id == b.destino).propia;
            var form = Dialogo.Caja("encargo-form");
            form.name = "encargo-form";
            var fila = Dialogo.Caja("encargo-cab");
            if (k.Encargo == null)
            {
                var modo = new DropdownField(new List<string> { "Ruta permanente (entre plazas tuyas)", "Envío (una ida y vuelta)" }, b.modo == "ruta" ? 0 : 1) { name = "encargo-modo" };
                modo.AddToClassList("viaje-destino");
                modo.RegisterValueChangedCallback(_ => { var x = borradorDeEncargo[k.Id]; x.modo = modo.index == 0 ? "ruta" : "envio"; borradorDeEncargo[k.Id] = x; rehacer(); });
                fila.Add(modo);
            }
            else fila.Add(Dialogo.Texto(k.Encargo.Modo == "ruta" ? "Ruta permanente" : "Envío", "plaza-sub"));
            var destinoFijo = k.Encargo != null && !(k.Estado == EstadoCaravana.CaravanaEsperando && k.Encargo.ParadaActual == "origen");
            var destino = new DropdownField(validas.Select(x => x.nombre + (x.propia ? " (tuya)" : "")).ToList(), validas.FindIndex(x => x.id == b.destino)) { name = "encargo-destino" };
            destino.AddToClassList("viaje-destino");
            destino.SetEnabled(!destinoFijo);
            destino.RegisterValueChangedCallback(_ => { var x = borradorDeEncargo[k.Id]; x.destino = validas[Math.Max(0, destino.index)].id; borradorDeEncargo[k.Id] = x; rehacer(); });
            fila.Add(destino);
            form.Add(fila);
            IntegerField salida = null;
            if (b.modo == "envio")
            {
                salida = new IntegerField("Salir dentro de (min)") { name = "encargo-salida", value = 0 };
                salida.AddToClassList("encargo-salida");
                form.Add(salida);
            }
            form.Add(Dialogo.Texto($"Primero deja y luego recoge. Cabe {capacidad} por parada. Pulsa un recurso para añadirlo; deja solo lo que recoge en la otra parada."
                + (ajena ? " En una plaza ajena deja es vender a sus órdenes de compra y recoge es comprar a sus órdenes de venta, cada línea con su precio y sin oro." : ""), "plaza-nota"));
            var paradas = Dialogo.Caja("encargo-paradas");
            paradas.Add(ParadaDelFormulario(k, a, "origen", b.origen, b.destinoParada, false, rehacer));
            paradas.Add(ParadaDelFormulario(k, a, "destino", b.destinoParada, b.origen, ajena, rehacer));
            form.Add(paradas);
            Button enviar = null;
            enviar = Dialogo.Boton(k.Encargo != null ? "Guardar cambios" : "Dar encargo", "boton-primario", async () =>
            {
                var x = borradorDeEncargo[k.Id];
                var po = ParaEnviar(x.origen, false);
                var pd = ParaEnviar(x.destinoParada, ajena);
                long? salir = salida != null && salida.value > 0 ? (long)(AhoraMs + salida.value * 60_000.0 * c.Sesion.FactorReloj) : null;
                Orden orden;
                if (k.Encargo != null)
                {
                    var o = new OrdenCambiarEncargo { CaravanaId = k.Id, DestinoAsentamientoId = x.destino, ParadaOrigen = po, ParadaDestino = pd };
                    if (salir != null) o.SalirEnMs = salir.Value;
                    orden = new Orden { CambiarEncargo = o };
                }
                else
                {
                    var o = new OrdenEncargarCaravana { CaravanaId = k.Id, DestinoAsentamientoId = x.destino, Modo = x.modo, ParadaOrigen = po, ParadaDestino = pd };
                    if (salir != null) o.SalirEnMs = salir.Value;
                    orden = new Orden { EncargarCaravana = o };
                }
                if (await OrdenDePlaza(orden, enviar)) { encargoAbierto.Remove(k.Id); borradorDeEncargo.Remove(k.Id); modoDeCaravana.Remove(k.Id); rehacer(); }
            });
            enviar.name = "encargo-enviar";
            form.Add(enviar);
            caja.Add(form);
        }

        /// <summary>La parada tal como va al motor: sin líneas vacías ni precios fuera de una plaza ajena.</summary>
        static ParadaEncargo ParaEnviar(ParadaEncargo x, bool conPrecio)
        {
            var p = new ParadaEncargo { EsperarExistencias = x.EsperarExistencias, DesecharSobrante = x.DesecharSobrante };
            foreach (var l in x.Recoger.Where(l => l.Cantidad > 0))
            {
                var n = new LineaRecoger { Recurso = l.Recurso, Cantidad = l.Cantidad };
                if (conPrecio && l.HasPrecioMax) n.PrecioMax = l.PrecioMax;
                p.Recoger.Add(n);
            }
            foreach (var l in x.Dejar)
            {
                var n = new LineaDejar { Recurso = l.Recurso };
                if (l.HasTope) n.Tope = l.Tope;
                if (conPrecio && l.HasPrecioMin) n.PrecioMin = l.PrecioMin;
                p.Dejar.Add(n);
            }
            return p;
        }

        /// <summary>Una parada del formulario (`htmlParada`): «Deja» (solo lo que se recoge en la otra) y «Recoge» (lo que hay en el almacén del origen;
        /// en el destino, todos los recursos), con botones de recurso y una fila por cada uno puesto.</summary>
        VisualElement ParadaDelFormulario(Caravana k, Asentamiento a, string punta, ParadaEncargo x, ParadaEncargo otra, bool conPrecio, Action rehacer)
        {
            var caja = Dialogo.Caja("encargo-parada");
            caja.name = "encargo-parada-" + punta;
            caja.Add(Kicker(punta == "origen" ? "En el origen" : conPrecio ? "En el destino (plaza ajena)" : "En el destino"));
            // Deja
            caja.Add(Dialogo.Texto(conPrecio ? "Vende" : "Deja", "plaza-sub"));
            var dejables = otra.Recoger.Select(l => l.Recurso).ToList();
            if (dejables.Count == 0) caja.Add(Dialogo.Texto($"Deja lo que recoge en {(punta == "origen" ? "el destino" : "el origen")}: elige allí qué recoger.", "plaza-nota"));
            else
            {
                var botones = Dialogo.Caja("encargo-recursos");
                foreach (var r in dejables)
                {
                    var recurso = r;
                    var puesto = x.Dejar.Any(l => l.Recurso == r);
                    var b = new Button(() => { if (puesto) x.Dejar.Remove(x.Dejar.First(l => l.Recurso == recurso)); else x.Dejar.Add(new LineaDejar { Recurso = recurso }); rehacer(); }) { name = $"enc-{punta}-dejar-{r}" };
                    b.AddToClassList("encargo-recurso");
                    b.EnableInClassList("puesto", puesto);
                    b.Add(IconosDelMundo.Icono("recurso", r, 20));
                    botones.Add(b);
                }
                caja.Add(botones);
            }
            foreach (var l in x.Dejar.ToList())
            {
                var linea = l;
                var f = Dialogo.Caja("encargo-fila");
                f.Add(IconosDelMundo.ConIcono("recurso", l.Recurso, Dialogo.Texto(CosasDelMapa.NombreDeRecurso(l.Recurso), "encargo-fila-nombre"), 16));
                var tope = new IntegerField { name = $"enc-{punta}-tope-{l.Recurso}", value = l.HasTope ? (int)(l.Tope / 1000) : 0, tooltip = "Tope: 0 = todo lo que lleve" };
                tope.AddToClassList("carga-celda-campo");
                tope.RegisterValueChangedCallback(ev => { if (ev.newValue > 0) linea.Tope = ev.newValue * 1000L; else linea.ClearTope(); });
                f.Add(tope);
                if (conPrecio)
                {
                    var precio = new FloatField { name = $"enc-{punta}-preciomin-{l.Recurso}", value = l.HasPrecioMin ? l.PrecioMin / 1000f : 0, tooltip = "Precio mínimo" };
                    precio.AddToClassList("carga-celda-campo");
                    precio.RegisterValueChangedCallback(ev => linea.PrecioMin = (long)Math.Round(ev.newValue * 1000));
                    f.Add(precio);
                }
                f.Add(Dialogo.Boton("✕", "encargo-quitar", () => { x.Dejar.Remove(linea); rehacer(); }));
                caja.Add(f);
            }
            if (x.Dejar.Count > 0)
            {
                var tirar = new Toggle("Tirar lo que no quepa en el almacén") { value = x.DesecharSobrante, name = $"enc-{punta}-desechar" };
                tirar.AddToClassList("caravana-reservar");
                tirar.RegisterValueChangedCallback(ev => x.DesecharSobrante = ev.newValue);
                caja.Add(tirar);
            }
            // Recoge
            caja.Add(Dialogo.Texto(conPrecio ? "Compra" : "Recoge", "plaza-sub"));
            var recogibles = punta == "origen"
                ? a.Almacen.Keys.Where(r => Hay(a, r) >= 1).OrderBy(r => r).ToList()
                : RecursosDeOrden().Concat(conPrecio ? new string[0] : new[] { "oro" }).ToList();
            var rec = Dialogo.Caja("encargo-recursos");
            foreach (var r in recogibles)
            {
                var recurso = r;
                var puesto = x.Recoger.Any(l => l.Recurso == r);
                var b = new Button(() =>
                {
                    if (puesto)
                    {
                        x.Recoger.Remove(x.Recoger.First(l => l.Recurso == recurso));
                        if (otra.Dejar.FirstOrDefault(l => l.Recurso == recurso) is LineaDejar d) otra.Dejar.Remove(d);
                    }
                    else x.Recoger.Add(new LineaRecoger { Recurso = recurso });
                    rehacer();
                }) { name = $"enc-{punta}-recoger-{r}", tooltip = CosasDelMapa.NombreDeRecurso(r) + (punta == "origen" ? $" · hay {Hay(a, r)}" : "") };
                b.AddToClassList("encargo-recurso");
                b.EnableInClassList("puesto", puesto);
                b.Add(IconosDelMundo.Icono("recurso", r, 20));
                if (punta == "origen") b.Add(Dialogo.Texto(Hay(a, r).ToString(), "orden-recurso-cifra"));
                rec.Add(b);
            }
            caja.Add(rec);
            foreach (var l in x.Recoger.ToList())
            {
                var linea = l;
                var f = Dialogo.Caja("encargo-fila");
                f.Add(IconosDelMundo.ConIcono("recurso", l.Recurso, Dialogo.Texto(CosasDelMapa.NombreDeRecurso(l.Recurso), "encargo-fila-nombre"), 16));
                var cantidad = new IntegerField { name = $"enc-{punta}-cantidad-{l.Recurso}", value = (int)(l.Cantidad / 1000) };
                cantidad.AddToClassList("carga-celda-campo");
                cantidad.RegisterValueChangedCallback(ev => linea.Cantidad = Math.Max(0, ev.newValue) * 1000L);
                f.Add(cantidad);
                if (conPrecio)
                {
                    var precio = new FloatField { name = $"enc-{punta}-preciomax-{l.Recurso}", value = l.HasPrecioMax ? l.PrecioMax / 1000f : 0, tooltip = "Precio máximo" };
                    precio.AddToClassList("carga-celda-campo");
                    precio.RegisterValueChangedCallback(ev => linea.PrecioMax = (long)Math.Round(ev.newValue * 1000));
                    f.Add(precio);
                }
                f.Add(Dialogo.Boton("✕", "encargo-quitar", () => { x.Recoger.Remove(linea); if (otra.Dejar.FirstOrDefault(d => d.Recurso == linea.Recurso) is LineaDejar d0) otra.Dejar.Remove(d0); rehacer(); }));
                caja.Add(f);
            }
            if (x.Recoger.Count > 0)
            {
                var esperar = new Toggle("Esperar a que haya todo") { value = x.EsperarExistencias, name = $"enc-{punta}-esperar" };
                esperar.AddToClassList("caravana-reservar");
                esperar.RegisterValueChangedCallback(ev => x.EsperarExistencias = ev.newValue);
                caja.Add(esperar);
            }
            return caja;
        }

        // ─────────────────────────── la pestaña Rutas ───────────────────────────

        /// <summary>Las caravanas de la plaza con encargo (`htmlRutas`), cada una con su ruta dibujada y, a quien los da, cambiar, detener y cancelar.</summary>
        void Rutas(VisualElement cuerpo, Asentamiento a, Heroe yo, Action rehacer)
        {
            var conEncargo = FlotaDe(a).Where(k => k.Encargo != null).ToList();
            var rutas = conEncargo.Count(k => k.Encargo.Modo == "ruta");
            cuerpo.Add(TituloDeSeccion("Rutas de caravanas",
                "Las caravanas de esta plaza que van y vienen solas. Una ruta une dos plazas tuyas y se repite sin fin; un envío hace una ida y vuelta, también a una plaza ajena (allí vende y compra a sus órdenes). En cada parada primero deja y luego recoge. Una caravana nueva se encarga desde la pestaña Caravanas.",
                Chip($"⇄ {rutas} {(rutas == 1 ? "ruta" : "rutas")}"), Chip($"→ {conEncargo.Count - rutas} {(conEncargo.Count - rutas == 1 ? "envío" : "envíos")}")));
            if (conEncargo.Count == 0) { cuerpo.Add(Dialogo.Texto("Ninguna caravana de esta plaza tiene ruta ni envío. Dale uno desde Caravanas → Encargo.", "plaza-vacio")); return; }
            foreach (var k in conEncargo)
            {
                var tarjeta = Dialogo.Caja("flota-detalle ruta-tarjeta");
                var cab = Dialogo.Caja("caravana-cab");
                cab.Add(IconosDelMundo.Icono("simbolo", "carro", 22, IconosDelMundo.Bronce));
                cab.Add(Dialogo.Texto(NombreDeCaravana(k), "caravana-nombre"));
                tarjeta.Add(cab);
                CajaDeEncargo(tarjeta, k, a, yo, CapacidadDe(k, a), rehacer);
                cuerpo.Add(tarjeta);
            }
        }

        // ─────────────────────────── la pestaña Trueques ───────────────────────────

        static readonly Dictionary<EstadoTrueque, (string icono, string texto)> EstadoDeTrueque = new Dictionary<EstadoTrueque, (string, string)>
        {
            [EstadoTrueque.TruequePropuesto] = ("📜", "sin contestar"), [EstadoTrueque.TruequeActivo] = ("🤝", "en vigor"), [EstadoTrueque.TruequeRechazado] = ("✕", "rechazado"),
            [EstadoTrueque.TruequeCumplido] = ("✔", "cumplido"), [EstadoTrueque.TruequeExpirado] = ("⌛", "expirado"),
        };

        void Trueques(VisualElement cuerpo, Asentamiento a, Heroe yo, Action rehacer)
        {
            var gestiona = GestionaOrdenes(a, yo);
            var mios = c.Sesion.Almacen.Todas(Entidad.CuerpoOneofCase.AcuerdoTrueque).Select(e => e.AcuerdoTrueque)
                .Where(x => x.AsentamientoAId == a.Id || x.AsentamientoBId == a.Id).OrderByDescending(x => x.CreadoEnMs).ToList();
            var vivos = mios.Where(x => x.Estado is EstadoTrueque.TruequePropuesto or EstadoTrueque.TruequeActivo).ToList();
            var cerrados = mios.Except(vivos).ToList();
            var cols = Dialogo.Caja("territorio-cols mercado-cols");
            var izq = Dialogo.Caja("territorio-col");
            var porContestar = vivos.Count(x => x.Estado == EstadoTrueque.TruequePropuesto && x.AsentamientoBId == a.Id);
            izq.Add(TituloDeSeccion("Trueques en curso",
                "Un trueque es un pacto entre dos plazas: cada una se compromete a entregar sus líneas, que van en caravanas. No obliga a nadie hasta que la otra plaza dice que sí; una propuesta sin contestar caduca sin castigo. Aceptado, el plazo corre desde el sí, y vencer sin cumplir resta reputación a cada lado en proporción a lo que dejó sin entregar.",
                Chip(vivos.Count.ToString()), porContestar > 0 ? Chip($"📜 {porContestar} por contestar", "activa") : null));
            if (vivos.Count == 0) izq.Add(Dialogo.Texto("No hay trueques propuestos ni en vigor.", "plaza-vacio"));
            foreach (var x in vivos) izq.Add(Pacto(x, a, gestiona, rehacer));
            if (cerrados.Count > 0)
            {
                izq.Add(TituloDeSeccion("Cerrados", null, Chip(cerrados.Count.ToString())));
                foreach (var x in cerrados) izq.Add(Pacto(x, a, false, rehacer));
            }
            cols.Add(izq);
            if (gestiona)
            {
                var destinos = PlazasConocidas(a.Id, yo);
                if (destinos.Count > 0) cols.Add(ProponerTrueque(a, destinos, rehacer));
            }
            cuerpo.Add(cols);
        }

        /// <summary>Un pacto (`tarjeta`): las dos plazas frente a frente con lo que da cada una (con lo entregado, en vigor), el estado, el plazo y, si te
        /// toca contestar, «✓ Aceptar» y «✕ Rechazar».</summary>
        VisualElement Pacto(AcuerdoTrueque x, Asentamiento a, bool gestiona, Action rehacer)
        {
            var pacto = Dialogo.Caja("pacto");
            pacto.name = "pacto-" + x.Id;
            var recibido = x.AsentamientoBId == a.Id;
            var pendiente = x.Estado == EstadoTrueque.TruequePropuesto;
            var progreso = x.Estado is EstadoTrueque.TruequeActivo or EstadoTrueque.TruequeCumplido;
            var cab = Dialogo.Caja("pacto-cab");
            var (icono, texto) = EstadoDeTrueque.TryGetValue(x.Estado, out var et) ? et : ("", "");
            cab.Add(Dialogo.Texto($"{icono} {texto}", "pacto-estado"));
            if (pendiente || x.Estado == EstadoTrueque.TruequeActivo) cab.Add(Dialogo.Texto($"⏳ {(pendiente ? "contestar" : "cumplir")} en {FaltaPara(x.ExpiraEnMs)}", "pacto-plazo"));
            pacto.Add(cab);
            var lados = Dialogo.Caja("pacto-lados");
            VisualElement Lado(string plaza, IEnumerable<LineaTrueque> ls)
            {
                var lado = Dialogo.Caja("pacto-lado");
                lado.Add(Dialogo.Texto(NombreDePlaza(plaza), "pacto-plaza"));
                var fichas = Dialogo.Caja("pacto-lineas");
                foreach (var l in ls) fichas.Add(IconosDelMundo.ChipDeRecurso(l.Recurso, progreso ? $"{l.CantidadEntregada / 1000}/{l.CantidadTotal / 1000}" : (l.CantidadTotal / 1000).ToString(), CosasDelMapa.NombreDeRecurso(l.Recurso)));
                lado.Add(fichas);
                return lado;
            }
            lados.Add(Lado(x.AsentamientoAId, x.LineasA));
            lados.Add(Dialogo.Texto("⇄", "pacto-sello"));
            lados.Add(Lado(x.AsentamientoBId, x.LineasB));
            pacto.Add(lados);
            if (pendiente) pacto.Add(Dialogo.Texto(recibido ? "Te toca contestar a ti." : "Espera la respuesta de la otra plaza.", "plaza-sub"));
            if (pendiente && recibido && gestiona)
            {
                var acciones = Dialogo.Caja("pacto-acciones");
                Button si = null, no = null;
                si = Dialogo.Boton("✓ Aceptar", "boton-primario", async () => { if (await OrdenDePlaza(new Orden { AceptarTrueque = new OrdenAceptarTrueque { AcuerdoId = x.Id } }, si)) rehacer(); });
                si.name = "trueque-aceptar-" + x.Id;
                no = Dialogo.Boton("✕ Rechazar", "boton-secundario", async () => { if (await OrdenDePlaza(new Orden { RechazarTrueque = new OrdenRechazarTrueque { AcuerdoId = x.Id } }, no)) rehacer(); });
                no.name = "trueque-rechazar-" + x.Id;
                acciones.Add(si);
                acciones.Add(no);
                pacto.Add(acciones);
            }
            return pacto;
        }

        /// <summary>Proponer un trueque: la otra plaza y hasta tres líneas por lado («Das» y «Recibes»), con la balanza en medio.</summary>
        VisualElement ProponerTrueque(Asentamiento a, List<(string id, string nombre, bool propia)> destinos, Action rehacer)
        {
            var der = Dialogo.Caja("territorio-col");
            der.name = "trueque-proponer";
            der.Add(TituloDeSeccion("Proponer un trueque", "Elige la plaza y hasta tres líneas por lado, sin repetir recurso. Deja en «nada» las que no uses."));
            truequeDestino = Math.Min(truequeDestino, destinos.Count - 1);
            var con = new DropdownField("Con", destinos.Select(x => x.nombre + (x.propia ? " (tuya)" : "")).ToList(), truequeDestino) { name = "trueque-destino" };
            con.AddToClassList("viaje-destino");
            con.RegisterValueChangedCallback(_ => truequeDestino = Math.Max(0, con.index));
            der.Add(con);
            var recursos = new List<string> { "— nada —" }.Concat(RecursosDeOrden().Prepend("oro").Select(CosasDelMapa.NombreDeRecurso)).ToList();
            var ids = new List<string> { "" }.Concat(RecursosDeOrden().Prepend("oro")).ToList();
            var balanza = Dialogo.Caja("trueque-balanza");
            VisualElement Platillo(string titulo, string clase, (string recurso, int cantidad)[] lineas, string lado)
            {
                var p = Dialogo.Caja("trueque-platillo " + clase);
                p.Add(Dialogo.Texto(titulo, "trueque-platillo-titulo"));
                for (int i = 0; i < lineas.Length; i++)
                {
                    var j = i;
                    var fila = Dialogo.Caja("trueque-linea");
                    var recurso = new DropdownField(recursos, Math.Max(0, ids.IndexOf(lineas[i].recurso ?? ""))) { name = $"trueque-{lado}-recurso-{i}" };
                    recurso.AddToClassList("viaje-destino");
                    recurso.RegisterValueChangedCallback(_ => lineas[j].recurso = ids[Math.Max(0, recurso.index)]);
                    var cantidad = new IntegerField { name = $"trueque-{lado}-cantidad-{i}", value = lineas[i].cantidad };
                    cantidad.AddToClassList("carga-celda-campo");
                    cantidad.RegisterValueChangedCallback(ev => lineas[j].cantidad = ev.newValue);
                    fila.Add(recurso);
                    fila.Add(cantidad);
                    p.Add(fila);
                }
                return p;
            }
            balanza.Add(Platillo($"▲ Das · {(a.HasNombre ? a.Nombre : a.Id)}", "da", truequeDa, "a"));
            balanza.Add(Dialogo.Texto("⚖", "trueque-fiel"));
            balanza.Add(Platillo("▼ Recibes · la otra plaza", "recibe", truequeRecibe, "b"));
            der.Add(balanza);
            Button proponer = null;
            proponer = Dialogo.Boton("🤝 Proponer trueque", "boton-primario trueque-proponer", async () =>
            {
                var da = truequeDa.Where(l => !string.IsNullOrEmpty(l.recurso) || l.cantidad > 0).ToList();
                var recibe = truequeRecibe.Where(l => !string.IsNullOrEmpty(l.recurso) || l.cantidad > 0).ToList();
                if (da.Concat(recibe).Any(l => string.IsNullOrEmpty(l.recurso) || l.cantidad < 1)) { panelAvisos.Mostrar("Cada línea necesita recurso y una cantidad de 1 o más.", true); return; }
                if (da.Count == 0 || recibe.Count == 0) { panelAvisos.Mostrar("Cada lado tiene que ofrecer al menos una línea.", true); return; }
                var o = new OrdenProponerTrueque { AsentamientoAId = a.Id, AsentamientoBId = destinos[Math.Max(0, con.index)].id };
                foreach (var l in da) o.LineasA.Add(new OrdenProponerTrueque.Types.LineasA { Recurso = l.recurso, Cantidad = l.cantidad * 1000L });
                foreach (var l in recibe) o.LineasB.Add(new OrdenProponerTrueque.Types.LineasB { Recurso = l.recurso, Cantidad = l.cantidad * 1000L });
                if (await OrdenDePlaza(new Orden { ProponerTrueque = o }, proponer))
                {
                    Array.Clear(truequeDa, 0, truequeDa.Length);
                    Array.Clear(truequeRecibe, 0, truequeRecibe.Length);
                    rehacer();
                }
            });
            proponer.name = "trueque-proponer-boton";
            der.Add(proponer);
            return der;
        }

        // ─────────────────────────── la Región: el informe (Taberna) y las órdenes a distancia (Mercado) ───────────────────────────

        Faccion MiFaccion(Heroe yo) => yo.HasFaccionId ? c.Sesion.Almacen.Buscar(TipoEntidad.Faccion, yo.FaccionId)?.Faccion : null;

        JToken TarifaDeRegion => c.Datos.Bloque("Niebla_De_Guerra", "INTEL")?["region"];

        /// <summary>¿Compra el informe en la taberna de esta plaza? Taberna activa, plaza de tu Facción y el Rey, el Embajador o el Gobernador.</summary>
        bool CompraInforme(Asentamiento a, Heroe yo)
        {
            if (!Propia(a, yo) || !Internos(a).Any(e => e.Tipo == "taberna" && Activo(e))) return false;
            var f = MiFaccion(yo);
            return (f != null && ((f.HasReyId && f.ReyId == yo.Id) || (f.HasEmbajadorId && f.EmbajadorId == yo.Id))) || TitularDe(a, "gobernador") == yo.Id;
        }

        List<InformeRegion> InformesDeOrdenesVigentes(Heroe yo) => MiFaccion(yo)?.InformesRegion.Where(x => x.Tipo == "ordenes" && x.HasCaducaEnMs && x.CaducaEnMs > AhoraMs).ToList()
            ?? new List<InformeRegion>();

        /// <summary>La Taberna de la plaza: por ahora, el informe de la región (`htmlServicioRegion`): «Sus plazas» y «Sus órdenes de mercado», cada uno
        /// con su precio en oro del almacén, o cuándo se puede volver a comprar, y cuánto le queda a la vista.</summary>
        void InformeDeRegion(VisualElement cuerpo, Asentamiento a, Heroe yo, Action rehacer)
        {
            var t = TarifaDeRegion;
            var oro = (double?)t?["oro"] ?? 0;
            var espera = (double?)t?["cooldownMinutos"] ?? 0;
            var dura = (double?)t?["caducidadMinutos"] ?? 0;
            cuerpo.Add(TituloDeSeccion("Informe de la región",
                $"Lo que se sabe de toda la región del relieve donde está esta plaza. Entran sus plazas que no son de tu Facción ni de una Facción en guerra con la tuya. Cada informe cuesta {oro} de oro del almacén de la plaza y no se repite hasta dentro de {Math.Round(espera / 60)} h. Nadie se entera."));
            var informes = MiFaccion(yo)?.InformesRegion.ToList() ?? new List<InformeRegion>();
            foreach (var (tipo, icono, titulo, texto) in new[]
            {
                ("plazas", "🏛", "Sus plazas", "Nombre, Facción, nivel y sitio de cada una, en tu memoria."),
                ("ordenes", "⚖", "Sus órdenes de mercado", $"Las ves {Math.Round(dura / 60.0, 1)} h y las tomas desde el Mercado de tus plazas (pestaña Región)."),
            })
            {
                var fila = Dialogo.Caja("intel-region-fila");
                fila.name = "informe-" + tipo;
                fila.Add(Dialogo.Texto(icono, "intel-region-icono"));
                var cuerpoFila = Dialogo.Caja("intel-region-texto");
                cuerpoFila.Add(Dialogo.Texto(titulo, "pacto-plaza"));
                cuerpoFila.Add(Dialogo.Texto(texto, "plaza-sub"));
                // El informe de este tipo de la región de la plaza (`regionEn`, con el raster `region` del artefacto): el último.
                var region = RegionEn(a.Posicion.X / 1000.0, a.Posicion.Y / 1000.0);
                var previo = informes.Where(x => x.Tipo == tipo && (region == null || x.Region == region)).OrderByDescending(x => x.CompradoEnMs).FirstOrDefault();
                if (previo != null && previo.HasCaducaEnMs && previo.CaducaEnMs > AhoraMs) cuerpoFila.Add(Dialogo.Texto($"A la vista {FaltaPara(previo.CaducaEnMs)} más", "plaza-sub intel-vigente"));
                fila.Add(cuerpoFila);
                if (previo != null && previo.LibreEnMs > AhoraMs) fila.Add(Dialogo.Texto($"Otra vez en {FaltaPara(previo.LibreEnMs)}", "flota-espera"));
                else if (Hay(a, "oro") >= oro)
                {
                    var ti = tipo;
                    Button comprar = null;
                    comprar = Dialogo.Boton("", "boton-primario intel-comprar", async () => { if (await OrdenDePlaza(new Orden { ComprarInformeRegion = new OrdenComprarInformeRegion { AsentamientoId = a.Id, Tipo = ti } }, comprar)) rehacer(); });
                    comprar.name = "comprar-informe-" + tipo;
                    comprar.Add(Dialogo.Texto("Comprar", "intel-comprar-texto"));
                    comprar.Add(IconosDelMundo.ChipDeRecurso("oro", oro.ToString(), "Oro del almacén de la plaza"));
                    fila.Add(comprar);
                }
                cuerpo.Add(fila);
            }
        }

        /// <summary>La pestaña Región del Mercado (`htmlOrdenesRegion`): las órdenes activas de plazas ajenas que deja ver un informe de órdenes vigente,
        /// por plaza, con la cantidad y «Comprarle» o «Venderle», que manda la primera caravana libre que pueda (`tomarOrdenADistancia`).</summary>
        void OrdenesDeLaRegion(VisualElement cuerpo, Asentamiento a, Heroe yo, Action rehacer)
        {
            var vigentes = InformesDeOrdenesVigentes(yo);
            var chips = vigentes.Select(x => Chip($"⚖ {FaltaPara(x.CaducaEnMs)}")).ToArray();
            cuerpo.Add(TituloDeSeccion("Órdenes de la región",
                "Las órdenes de mercado activas de las plazas de las regiones de las que tu Facción tiene un informe de órdenes vigente (se compra en la taberna). Tomar una manda la primera caravana libre de esta plaza que pueda con lo pedido a esa plaza, con el precio de la orden como tope: si vende, le compra; si compra, le lleva lo tuyo y se lo vende. La orden no se reserva: al llegar se comercia con lo que quede. El oro sale y entra en tu capital.", chips));
            var libres = FlotaDe(a).Where(k => k.Estado == EstadoCaravana.CaravanaDisponible).ToList();
            var capacidad = libres.Select(k => CapacidadDe(k, a)).DefaultIfEmpty(0).Max();
            var puede = GestionaEncargos(a, yo) && libres.Count > 0;
            cuerpo.Add(Dialogo.Texto(puede ? $"Sale la primera caravana libre que pueda con lo pedido: caben hasta {capacidad}." : "Hace falta una caravana libre en esta plaza y ser su Gobernador, Tesorero o Maestro de Caravanas.", "plaza-nota"));
            var grupos = c.Sesion.Almacen.Todas(Entidad.CuerpoOneofCase.OrdenMercado).Select(e => e.OrdenMercado)
                .Where(o => o.Estado == EstadoOrden.OrdenActiva && o.Cantidad > o.CantidadCumplida && o.AsentamientoId != a.Id)
                .GroupBy(o => o.AsentamientoId)
                .Where(g => c.Sesion.Almacen.Buscar(TipoEntidad.Asentamiento, g.Key)?.Asentamiento?.FaccionId != yo.FaccionId).ToList();
            if (grupos.Count == 0) { cuerpo.Add(Dialogo.Texto("Ninguna plaza de esas regiones tiene órdenes abiertas ahora.", "plaza-vacio")); return; }
            foreach (var g in grupos)
            {
                var seccion = Dialogo.Caja("region-plaza");
                seccion.Add(Dialogo.Texto(NombreDePlaza(g.Key), "pacto-plaza"));
                foreach (var o in g)
                {
                    var pendiente = (int)((o.Cantidad - o.CantidadCumplida) / 1000);
                    var hay = (int)Hay(a, o.Recurso);
                    var tope = Math.Min(pendiente, Math.Min(capacidad > 0 ? capacidad : pendiente, o.EsCompra ? hay : pendiente));
                    var fila = Dialogo.Caja("mercado-orden");
                    fila.name = "region-orden-" + o.Id;
                    var texto = Dialogo.Caja("mercado-orden-texto");
                    texto.Add(IconosDelMundo.ConIcono("recurso", o.Recurso, Dialogo.Texto($"{(o.EsCompra ? "Compra" : "Vende")} {pendiente} {CosasDelMapa.NombreDeRecurso(o.Recurso)}", "mercado-orden-nombre"), 18));
                    texto.Add(Dialogo.Texto($"🪙 {Oro(o.PrecioUnitario)} c/u · caduca en {FaltaPara(o.ExpiraEnMs)}{(o.EsCompra ? $" · tienes {hay}" : "")}", "plaza-sub"));
                    fila.Add(texto);
                    if (puede && tope >= 1)
                    {
                        var cantidad = new IntegerField { name = "region-cantidad-" + o.Id, value = tope };
                        cantidad.AddToClassList("puerta-carga-cantidad");
                        fila.Add(cantidad);
                        var orden = o;
                        Button tomar = null;
                        tomar = Dialogo.Boton(o.EsCompra ? "Venderle" : "Comprarle", "boton-primario", async () =>
                        {
                            if (cantidad.value < 1) { panelAvisos.Mostrar("Pon cuánto quieres tomar.", true); return; }
                            if (await OrdenDePlaza(new Orden { TomarOrdenADistancia = new OrdenTomarOrdenADistancia { AsentamientoId = a.Id, OrdenId = orden.Id, Cantidad = cantidad.value * 1000L } }, tomar))
                            {
                                panelAvisos.Mostrar("Sale la caravana hacia esa plaza.", false);
                                rehacer();
                            }
                        });
                        tomar.name = "region-tomar-" + o.Id;
                        fila.Add(tomar);
                    }
                    seccion.Add(fila);
                }
                cuerpo.Add(seccion);
            }
        }

        /// <summary>Lo que cambia en las pestañas de la tanda 3: los acuerdos, los informes de la Facción y las órdenes ajenas.</summary>
        string FirmaDeComercio(Asentamiento a, Heroe yo) =>
            string.Join(",", c.Sesion.Almacen.Todas(Entidad.CuerpoOneofCase.AcuerdoTrueque).Select(e => e.AcuerdoTrueque).Where(x => x.AsentamientoAId == a.Id || x.AsentamientoBId == a.Id).Select(x => x.Id + ":" + x.Version))
            + $"|{MiFaccion(yo)?.Version}|{c.Sesion.Almacen.Todas(Entidad.CuerpoOneofCase.OrdenMercado).Count()}|{string.Join(",", encargoAbierto)}|{string.Join(",", modoDeCaravana.Select(kv => kv.Key + kv.Value))}";
    }
}
