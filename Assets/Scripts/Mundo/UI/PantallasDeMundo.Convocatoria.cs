// Salir como ejército (paso 11, tanda 1), calcado de `ui/salidaComoEjercito.ts` y `ui/convocatoria.ts`: en «Salir al mundo», «Cómo sales» deja elegir
// «Ejército»; se convoca (`convocarEjercito`, con la política de unión) o se une uno a una convocatoria del sitio (`unirseAConvocatoria`). Con una
// convocatoria, «Salir al mundo» abre su preparación: los integrantes con su tropa, las peticiones (el Líder), la Caravana de Fundación que espera aquí
// (el Líder: «Se queda aquí» o «Llevárnosla»), tu carga y tu tropa con «Guardar» (`cambiarSeleccionDeConvocatoria`) y, abajo, «Cancelar la salida» y
// «Salir con el ejército» (el Líder) o «Separarme (sigues dentro)». El motor valida todo; su motivo sale tal cual.
using System;
using System.Collections.Generic;
using System.Linq;
using BronzeAge.Contract.V1;
using ConquestTactics.Mundo.Mapa;
using UnityEngine;
using UnityEngine.UIElements;

namespace ConquestTactics.Mundo.UI
{
    public partial class PantallasDeMundo
    {
        /// <summary>Lo elegido en «Cómo sales» (sobrevive a reabrir la ventana): ejército o no, y su política.</summary>
        bool salirComoEjercito;
        string politicaDeSalida = "aceptar";
        /// <summary>La Caravana de Fundación que el Líder se lleva, por convocatoria ("" = ninguna).</summary>
        readonly Dictionary<string, string> caravanaDeLaConvocatoria = new Dictionary<string, string>();

        static readonly Dictionary<string, string> NombreDePolitica = new Dictionary<string, string>
        {
            ["aceptar"] = "Abierto: se une quien llegue", ["preguntar"] = "Decide el Líder (10 s)", ["rechazar"] = "Cerrado",
        };

        static string Politica(PoliticaUnion p) => p == PoliticaUnion.UnionPreguntar ? "preguntar" : p == PoliticaUnion.UnionRechazar ? "rechazar" : "aceptar";

        /// <summary>La convocatoria en la que estás, si estás en una.</summary>
        Convocatoria MiConvocatoria(Heroe h) => h == null ? null : c.Sesion.Almacen.Todas(Entidad.CuerpoOneofCase.Convocatoria).Select(e => e.Convocatoria)
            .FirstOrDefault(x => x.Integrantes.Any(i => i.HeroeId == h.Id));

        /// <summary>Las convocatorias abiertas de este sitio, menos la tuya.</summary>
        List<Convocatoria> ConvocatoriasDe(string lugarId, Heroe h) => c.Sesion.Almacen.Todas(Entidad.CuerpoOneofCase.Convocatoria).Select(e => e.Convocatoria)
            .Where(x => x.LugarId == lugarId && !x.Integrantes.Any(i => i.HeroeId == h.Id)).ToList();

        /// <summary>«Cómo sales» con «Ejército» (`htmlSalidaComoEjercito`): la lista de convocatorias del sitio, con «Unirme» o «Pedir unirme», y quién
        /// puede unirse a la tuya. `seleccion` lee la tropa y la carga marcadas en la ventana; `alUnirse` reabre la ventana tras unirse.</summary>
        void BloqueDeEjercito(VisualElement bloque, string lugarId, Heroe h, Func<(List<string> ids, Cantidades carga)> seleccion, Action<string> error, Action alUnirse)
        {
            bloque.Clear();
            if (!h.HasFaccionId) bloque.Add(Dialogo.Texto("No tienes Facción: nadie podrá unirse a tu ejército.", "campo-ayuda aviso-fuerte"));
            bloque.Add(Dialogo.Texto("Ejércitos que se están preparando aquí", "salida-recuadro-sub"));
            var abiertas = ConvocatoriasDe(lugarId, h);
            if (abiertas.Count == 0) bloque.Add(Dialogo.Texto("Nadie de tu Facción está preparando un ejército aquí. Puedes convocar uno tú.", "campo-ayuda"));
            foreach (var cv in abiertas)
            {
                var fila = Dialogo.Caja("ejercito-fila");
                fila.name = "convocatoria-" + cv.Id;
                var texto = Dialogo.Caja("ejercito-fila-texto");
                texto.Add(Dialogo.Texto($"Ejército de {Textos.NombreDeHeroe(c.Sesion.Almacen, cv.LiderId)}", "aviso-titulo"));
                texto.Add(Dialogo.Texto($"{cv.Integrantes.Count} héroe(s) · {NombreDePolitica[Politica(cv.PoliticaDeUnion)]}", "campo-ayuda"));
                fila.Add(texto);
                var pedida = cv.Peticiones.Any(x => x.HeroeId == h.Id && x.ExpiraEnMs > AhoraMs);
                if (pedida) fila.Add(Dialogo.Texto("Petición enviada", "campo-ayuda"));
                else
                {
                    var id = cv.Id;
                    Button unirme = null;
                    unirme = Dialogo.Boton(cv.PoliticaDeUnion == PoliticaUnion.UnionPreguntar ? "Pedir unirme" : "Unirme", "boton-primario", async () =>
                    {
                        var (ids, carga) = seleccion();
                        var o = new OrdenUnirseAConvocatoria { HeroeId = h.Id, ConvocatoriaId = id, Carga = carga };
                        o.EscuadronIds.AddRange(ids);
                        unirme.SetEnabled(false);
                        var r = await OrdenConError(new Orden { UnirseAConvocatoria = o }, error);
                        unirme.SetEnabled(true);
                        if (r) alUnirse();
                    });
                    unirme.name = "unirse-" + cv.Id;
                    fila.Add(unirme);
                }
                bloque.Add(fila);
            }
            bloque.Add(Dialogo.Texto("Quién puede unirse (se fija al convocar)", "salida-recuadro-sub"));
            var politicas = new GroupBox();
            politicas.AddToClassList("salida-modos");
            foreach (var p in new[] { "aceptar", "preguntar" })
            {
                var pol = p;
                var radio = new RadioButton(NombreDePolitica[p]) { value = politicaDeSalida == p, name = "politica-" + p };
                radio.RegisterValueChangedCallback(ev => { if (ev.newValue) politicaDeSalida = pol; });
                politicas.Add(radio);
            }
            bloque.Add(politicas);
        }

        /// <summary>Manda una orden y pone su rechazo en `error`; true si se aceptó.</summary>
        async System.Threading.Tasks.Task<bool> OrdenConError(Orden orden, Action<string> error)
        {
            error(null);
            try
            {
                var r = await c.Sesion.Ordenar(orden);
                if (r.Resultado == Resultado.OrdenAceptada) return true;
                error(Textos.Explicar(r.Rechazo));
            }
            catch (ErrorDeSesion e) { error(e.Message); }
            return false;
        }

        // ─────────────────────────── la preparación ───────────────────────────

        /// <summary>La preparación del ejército (`htmlPreparacion`), en la ventana de salida: se repinta sola cuando cambia la convocatoria.</summary>
        void VentanaDePreparacion(Convocatoria cv0, string campId, string lugar, string plazaId)
        {
            var interior = SelectorDePersonaje.Interior;
            if (interior == null || interior.PanelAbierto) return;
            interior.PanelAbierto = true;
            var velo = Dialogo.Caja("dialogo-velo");
            velo.name = "ventana-salida";
            var placa = Dialogo.Caja("placa salida-ventana");
            placa.name = "preparacion";
            velo.Add(placa);
            IVisualElementScheduledItem vigilar = null;
            void Cerrar() { vigilar?.Pause(); velo.RemoveFromHierarchy(); interior.PanelAbierto = false; }
            velo.RegisterCallback<KeyDownEvent>(e => { if (e.keyCode == KeyCode.Escape) Cerrar(); }, TrickleDown.TrickleDown);
            var error = Dialogo.Texto("", "error oculto");
            void Error(string m) => Poner(error, m);
            List<string> borradorTropa = null;
            Dictionary<string, int> borradorCarga = null;
            var eligiendo = false;
            string firma = null;
            string FirmaExterna(Convocatoria cv, Heroe h) => cv == null || h == null ? "" : $"{cv.Version}|{h.Version}|{c.Sesion.Almacen.Todas(Entidad.CuerpoOneofCase.Caravana).Count()}";

            void Pintar()
            {
                var h = HeroeAhora();
                var cv = MiConvocatoria(h);
                if (h == null || cv == null)
                {
                    // Ya no estás en ella (cancelada, te separaste o salió): la ventana normal.
                    Cerrar();
                    VentanaDeSalida(campId, lugar, plazaId);
                    return;
                }
                firma = FirmaExterna(cv, h);
                placa.Clear();
                var soyLider = cv.LiderId == h.Id;
                var mio = cv.Integrantes.First(x => x.HeroeId == h.Id);
                var resides = cv.LugarEsCampamento ? (CampamentoActual(cv.LugarId)?.ResidentesIds.Contains(h.Id) ?? false) : h.HasResidenciaId && h.ResidenciaId == cv.LugarId;

                var cabeza = Dialogo.Caja("salida-cab");
                cabeza.Add(Dialogo.Texto($"SALIR DE {lugar.ToUpperInvariant()}", "dialogo-titulo salida-titulo"));
                var aspa = Dialogo.Boton("×", "boton-secundario salida-cerrar", Cerrar);
                aspa.name = "cancelar";
                cabeza.Add(aspa);
                placa.Add(cabeza);
                var cuerpo = Dialogo.Caja("salida-cuerpo");
                placa.Add(cuerpo);
                ScrollView Columna(string clases)
                {
                    var col = new ScrollView(ScrollViewMode.Vertical) { horizontalScrollerVisibility = ScrollerVisibility.Hidden };
                    foreach (var cl in clases.Split(' ')) col.AddToClassList(cl);
                    cuerpo.Add(col);
                    return col;
                }
                var izquierda = Columna("salida-col");
                var derecha = Columna("salida-col salida-col--derecha");

                // Izquierda: quién lo dirige, los integrantes, las peticiones y la Caravana de Fundación.
                izquierda.Add(Kicker("Ejército en preparación"));
                izquierda.Add(ConAyuda(Dialogo.Texto($"{(soyLider ? "Tú lo diriges." : $"Lo dirige {Textos.NombreDeHeroe(c.Sesion.Almacen, cv.LiderId)}.")} {NombreDePolitica[Politica(cv.PoliticaDeUnion)]}.", "dialogo-texto"),
                    "Seguís dentro: nada se mueve hasta que el Líder pulse «Salir con el ejército» o cancele. Espera sin límite de tiempo."));
                izquierda.Add(Dialogo.Texto("Integrantes", "salida-recuadro-sub"));
                var lista = Dialogo.Caja("conv-integrantes");
                lista.name = "integrantes";
                foreach (var x in cv.Integrantes)
                {
                    var fila = Dialogo.Caja("conv-integrante");
                    fila.name = "integrante-" + x.HeroeId;
                    var papel = string.Join(" · ", new[] { x.HeroeId == cv.LiderId ? "Líder" : null, x.HeroeId == h.Id ? "tú" : null }.Where(t => t != null));
                    var cab = Dialogo.Caja("conv-integrante-cab");
                    cab.Add(Dialogo.Texto(Textos.NombreDeHeroe(c.Sesion.Almacen, x.HeroeId), "aviso-titulo"));
                    if (papel != "") cab.Add(Dialogo.Texto(papel, "conv-papel"));
                    cab.Add(Dialogo.Texto($"{x.Seleccion?.EscuadronIds.Count ?? 0} escuadra(s)", "campo-ayuda"));
                    fila.Add(cab);
                    var tropas = Dialogo.Caja("conv-integrante-tropas");
                    var ids = x.HeroeId == h.Id ? x.Seleccion.EscuadronIds.Select(id => c.Sesion.Almacen.Buscar(TipoEntidad.Escuadron, id)?.Escuadron?.TropaId).Where(t => t != null).ToList()
                        : x.Seleccion?.TropaIds.ToList() ?? new List<string>();
                    foreach (var t in ids) if (PiezasDeTropa.Miniatura(t, 28) is VisualElement mini) tropas.Add(mini);
                    if (ids.Count == 0) tropas.Add(Dialogo.Texto("Sin tropa", "campo-ayuda"));
                    fila.Add(tropas);
                    lista.Add(fila);
                }
                izquierda.Add(lista);
                if (soyLider)
                {
                    var vivas = cv.Peticiones.Where(x => x.ExpiraEnMs > AhoraMs).ToList();
                    if (vivas.Count > 0)
                    {
                        izquierda.Add(Dialogo.Texto("Piden unirse", "salida-recuadro-sub"));
                        foreach (var p in vivas)
                        {
                            var fila = Dialogo.Caja("ejercito-fila");
                            fila.name = "peticion-" + p.HeroeId;
                            var texto = Dialogo.Caja("ejercito-fila-texto");
                            texto.Add(Dialogo.Texto(Textos.NombreDeHeroe(c.Sesion.Almacen, p.HeroeId), "aviso-titulo"));
                            texto.Add(Dialogo.Texto($"{FaltaPara(p.ExpiraEnMs)} para contestar · {p.Seleccion?.EscuadronIds.Count ?? 0} escuadra(s)", "campo-ayuda"));
                            fila.Add(texto);
                            foreach (var (si, etiqueta, clase) in new[] { (true, "Aceptar", "boton-primario"), (false, "Rechazar", "boton-secundario") })
                            {
                                var acepta = si;
                                var sol = p.HeroeId;
                                Button b = null;
                                b = Dialogo.Boton(etiqueta, clase, async () =>
                                {
                                    b.SetEnabled(false);
                                    await OrdenConError(new Orden { ResponderPeticionDeConvocatoria = new OrdenResponderPeticionDeConvocatoria { HeroeId = h.Id, ConvocatoriaId = cv.Id, SolicitanteId = sol, Aceptar = acepta } }, Error);
                                    b.SetEnabled(true);
                                });
                                b.name = (si ? "aceptar-" : "rechazar-") + p.HeroeId;
                                fila.Add(b);
                            }
                            izquierda.Add(fila);
                        }
                    }
                    // La Caravana de Fundación que espera aquí: «Se queda aquí» o «Llevárnosla».
                    var esperan = c.Sesion.Almacen.Todas(Entidad.CuerpoOneofCase.Caravana).Select(e => e.Caravana)
                        .Where(k => k.Tipo == TipoCaravana.CaravanaDeFundacion && k.Estado == EstadoCaravana.CaravanaDisponible
                            && (cv.LugarEsCampamento ? k.HasOrigenCampamentoId && k.OrigenCampamentoId == cv.LugarId : k.OrigenAsentamientoId == cv.LugarId)).ToList();
                    if (esperan.Count > 0)
                    {
                        izquierda.Add(ConAyuda(Dialogo.Texto("Caravana de Fundación", "salida-recuadro-sub"),
                            "Espera aquí dentro. Si el ejército se la lleva, va enganchada detrás y, si su titular no sale con vosotros, pasas a serlo tú. El ejército marcha entonces a su paso, más lento."));
                        var elegida = caravanaDeLaConvocatoria.TryGetValue(cv.Id, out var e0) && esperan.Any(k => k.Id == e0) ? e0 : "";
                        var cartas = Dialogo.Caja("conv-caravanas");
                        void Carta(string id, string titulo, string detalle)
                        {
                            var b = new Button(() => { caravanaDeLaConvocatoria[cv.Id] = id; Pintar(); }) { name = "conv-caravana-" + (id == "" ? "queda" : id) };
                            b.AddToClassList("conv-caravana");
                            b.EnableInClassList("elegida", id == elegida);
                            b.Add(IconosDelMundo.Icono(id == "" ? "riel" : "mapa", id == "" ? "aedas" : "caravanaFundacion", 26, IconosDelMundo.Bronce));
                            var t = Dialogo.Caja("conv-caravana-cuerpo");
                            t.Add(Dialogo.Texto(titulo, "aviso-titulo"));
                            t.Add(Dialogo.Texto(detalle, "campo-ayuda"));
                            b.Add(t);
                            cartas.Add(b);
                        }
                        Carta("", "Se queda aquí", "El ejército sale sin ella");
                        foreach (var k in esperan) Carta(k.Id, "Llevárnosla" + (esperan.Count > 1 ? " · " + k.Id : ""), CargaDe(k) > 0 ? $"Lleva {CargaDe(k)}" : "Vacía");
                        izquierda.Add(cartas);
                    }
                }

                // Derecha: tu carga, con «Guardar carga» (si resides).
                var camposCarga = new List<(IntegerField campo, string recurso)>();
                if (resides)
                {
                    var plaza = cv.LugarEsCampamento ? null : c.Sesion.Almacen.Buscar(TipoEntidad.Asentamiento, cv.LugarId)?.Asentamiento;
                    var de = plaza != null ? "el almacén de la plaza" : "tu almacén personal";
                    var capacidad = c.Datos.Numero("documentos.Movimiento_Ejercitos.LOGISTICA.capacidadCarroPorJugador");
                    var guardada = mio.Seleccion?.Carga?.PorId.ToDictionary(kv => kv.Key, kv => (int)(kv.Value / 1000)) ?? new Dictionary<string, int>();
                    var valores = borradorCarga ?? guardada;
                    var total = Dialogo.Texto("", "salida-carga-total");
                    derecha.Add(ConAyuda(IconoDelante(Dialogo.Texto("Carga del carro", "salida-recuadro-titulo"), total), $"Lo que metas en el carro sale con el ejército, de {de}."));
                    var almacen = (plaza != null
                        ? plaza.Almacen.Select(kv => new KeyValuePair<string, long>(kv.Key, (long)Instante.Valor(kv.Value.Cantidad, c.Sesion.AhoraMundo)))
                        : h.AlmacenPersonal?.PorId.AsEnumerable() ?? Enumerable.Empty<KeyValuePair<string, long>>())
                        .Where(kv => kv.Value >= 1000).OrderBy(kv => kv.Key).ToList();
                    foreach (var kv in almacen)
                    {
                        var hay = (int)(kv.Value / 1000);
                        var fila = Dialogo.Caja("salida-fila carro-fila");
                        var nombre = IconosDelMundo.ConIcono("recurso", kv.Key, Dialogo.Texto(CosasDelMapa.NombreDeRecurso(kv.Key), "aviso-texto"), 20);
                        nombre.style.flexGrow = 1;
                        fila.Add(nombre);
                        fila.Add(Dialogo.Texto($"hay {hay}", "campo-ayuda carro-hay"));
                        var recurso = kv.Key;
                        var campo = new IntegerField { value = valores.TryGetValue(kv.Key, out var v) ? v : 0, name = "carga-" + kv.Key };
                        campo.RegisterValueChangedCallback(ev =>
                        {
                            campo.SetValueWithoutNotify(Math.Max(0, Math.Min(hay, ev.newValue)));
                            borradorCarga ??= new Dictionary<string, int>(guardada);
                            borradorCarga[recurso] = campo.value;
                            total.text = $"{camposCarga.Sum(x => x.campo.value)} / {capacidad:0}";
                        });
                        fila.Add(campo);
                        derecha.Add(fila);
                        camposCarga.Add((campo, kv.Key));
                    }
                    total.text = $"{camposCarga.Sum(x => x.campo.value)} / {capacidad:0}";
                    var botones = Dialogo.Caja("fila-envuelta");
                    Button guardar = null;
                    guardar = Dialogo.Boton("Guardar carga", "boton-primario", async () =>
                    {
                        var carga = new Cantidades();
                        foreach (var (campo, r) in camposCarga) if (campo.value > 0) carga.PorId[r] = campo.value * 1000L;
                        var o = new OrdenCambiarSeleccionDeConvocatoria { HeroeId = h.Id, Carga = carga };
                        o.EscuadronIds.AddRange(mio.Seleccion.EscuadronIds);
                        guardar.SetEnabled(false);
                        if (await OrdenConError(new Orden { CambiarSeleccionDeConvocatoria = o }, Error)) borradorCarga = null;
                        guardar.SetEnabled(true);
                    });
                    guardar.name = "guardar-carga";
                    botones.Add(guardar);
                    var descartar = Dialogo.Boton("Descartar", "boton-secundario", () => { borradorCarga = null; Pintar(); });
                    descartar.name = "descartar-carga";
                    botones.Add(descartar);
                    derecha.Add(botones);
                }
                else derecha.Add(Dialogo.Texto("No resides aquí: sales con el carro de tu columna, tal cual.", "campo-ayuda"));

                // Abajo: la velocidad, tu tropa con «Guardar cambios» y los botones de tu papel.
                var pie = Dialogo.Caja("salida-pie");
                placa.Add(pie);
                var guardadas = mio.Seleccion?.EscuadronIds.ToList() ?? new List<string>();
                var elegidas = borradorTropa ?? guardadas;
                var disponibles = h.EscuadronesIds.Select(id => c.Sesion.Almacen.Buscar(TipoEntidad.Escuadron, id)?.Escuadron)
                    .Where(e => e != null && e.Cantidad > 0 && (elegidas.Contains(e.Id) || (e.Contenedor?.DondeCase == ContenedorEscuadron.DondeOneofCase.Campamento && !e.EnGuarnicion))).ToList();
                var lista2 = elegidas.Select(id => disponibles.FirstOrDefault(s => s.Id == id)).Where(s => s != null).ToList();
                var otrasTropas = cv.Integrantes.Where(x => x.HeroeId != h.Id).SelectMany(x => x.Seleccion?.TropaIds ?? Enumerable.Empty<string>());
                pie.Add(LineaDeVelocidad(lista2.Select(s => s.TropaId).Concat(otrasTropas).ToList()));
                if (resides)
                {
                    int Coste(Escuadron s) => LiderazgoDe(s.TropaId) ?? 0;
                    var usado = lista2.Sum(Coste);
                    var tope = (int)h.LiderazgoBase;
                    var libre = tope - usado;
                    var cambiado = !elegidas.SequenceEqual(guardadas);
                    var cab = Dialogo.Caja("salida-tropa-cab");
                    cab.Add(ConAyuda(Dialogo.Texto("TU TROPA", "salida-recuadro-titulo"), "La primera entra primero en combate. Quita con ✕ o añade con «+»; los cambios se aplican al pulsar «Guardar cambios»."));
                    var lid = Dialogo.Texto($"Liderazgo {usado} / {tope}", "salida-liderazgo");
                    lid.EnableInClassList("excedido", libre < 0);
                    cab.Add(lid);
                    Button guardarTropa = null;
                    guardarTropa = Dialogo.Boton("Guardar cambios", "boton-secundario salida-guardar", async () =>
                    {
                        var o = new OrdenCambiarSeleccionDeConvocatoria { HeroeId = h.Id, Carga = mio.Seleccion?.Carga ?? new Cantidades() };
                        o.EscuadronIds.AddRange(elegidas);
                        guardarTropa.SetEnabled(false);
                        if (await OrdenConError(new Orden { CambiarSeleccionDeConvocatoria = o }, Error)) borradorTropa = null;
                        guardarTropa.SetEnabled(true);
                    });
                    guardarTropa.name = "guardar-tropa";
                    guardarTropa.SetEnabled(cambiado && lista2.Count > 0 && libre >= 0);
                    cab.Add(guardarTropa);
                    var descartarTropa = Dialogo.Boton("Descartar", "boton-secundario salida-guardar", () => { borradorTropa = null; Pintar(); });
                    descartarTropa.SetEnabled(cambiado);
                    cab.Add(descartarTropa);
                    pie.Add(cab);
                    var riel = new ScrollView(ScrollViewMode.Horizontal) { verticalScrollerVisibility = ScrollerVisibility.Hidden, name = "riel" };
                    riel.AddToClassList("salida-riel");
                    var candidatas = disponibles.Where(s => !elegidas.Contains(s.Id)).ToList();
                    var caben = candidatas.Where(s => Coste(s) <= libre).ToList();
                    for (int i = 0; i < Math.Max(EspaciosDeTropa, lista2.Count + (caben.Count > 0 ? 1 : 0)); i++)
                    {
                        if (i < lista2.Count)
                        {
                            var s = lista2[i];
                            var hueco = Dialogo.Caja("salida-hueco salida-hueco-tropa");
                            hueco.name = "hueco-" + s.Id;
                            var totalTropa = (int?)c.Datos.TropasReclutables?.FirstOrDefault(x => (string)x["id"] == s.TropaId)?["unidadesPorDefecto"];
                            if (PiezasDeTropa.Tarjeta(s.TropaId, Coste(s), (int)s.Cantidad, totalTropa, null, AnchoDeHueco) is VisualElement tarjeta)
                            {
                                tarjeta.style.marginRight = 0;
                                tarjeta.style.marginBottom = 0;
                                hueco.Add(tarjeta);
                            }
                            var id = s.Id;
                            var quitar = Dialogo.Boton("✕", "salida-hueco-quitar", () => { borradorTropa = elegidas.Where(x => x != id).ToList(); Pintar(); });
                            quitar.name = "quitar-" + id;
                            hueco.Add(quitar);
                            riel.Add(hueco);
                        }
                        else if (i == lista2.Count && caben.Count > 0)
                        {
                            var mas = Dialogo.Boton("+", "salida-hueco salida-hueco-mas", () => { eligiendo = !eligiendo; Pintar(); });
                            mas.name = "salida-mas";
                            mas.EnableInClassList("activo", eligiendo);
                            riel.Add(mas);
                        }
                        else riel.Add(Dialogo.Caja("salida-hueco salida-hueco-vacio"));
                    }
                    pie.Add(riel);
                    if (eligiendo && caben.Count > 0)
                    {
                        var elegir = Dialogo.Caja("salida-elegir fila-envuelta");
                        foreach (var s in caben)
                        {
                            var id = s.Id;
                            var b = new Button(() => { borradorTropa = elegidas.Append(id).ToList(); eligiendo = false; Pintar(); }) { name = "anadir-" + id };
                            b.AddToClassList("salida-candidata");
                            if (PiezasDeTropa.Miniatura(s.TropaId, 32) is VisualElement mini) b.Add(mini);
                            var t = new VisualElement();
                            t.Add(Dialogo.Texto(string.IsNullOrEmpty(s.Nombre) ? NombreDeTropa(s.TropaId) : s.Nombre, "aviso-titulo"));
                            t.Add(Dialogo.Texto($"{s.Cantidad} hombres · {Coste(s)} de Liderazgo", "campo-ayuda"));
                            b.Add(t);
                            elegir.Add(b);
                        }
                        pie.Add(elegir);
                    }
                    if (lista2.Count == 0) pie.Add(Dialogo.Texto("Sin ninguna escuadra no sales. Añade al menos una.", "campo-ayuda"));
                }
                else pie.Add(ConAyuda(Dialogo.Texto("No resides aquí.", "campo-ayuda"), "Sales de visita, con tu columna aparcada en la puerta, y tu selección no cuenta."));

                var acciones = Dialogo.Caja("salida-acciones");
                acciones.Add(error);
                if (soyLider)
                {
                    Button cancelar = null, partir = null;
                    cancelar = Dialogo.Boton("Cancelar la salida", "boton-secundario", async () =>
                    {
                        cancelar.SetEnabled(false);
                        if (await OrdenConError(new Orden { CancelarConvocatoria = new OrdenCancelarConvocatoria { HeroeId = h.Id } }, Error)) { Cerrar(); VentanaDeSalida(campId, lugar, plazaId); }
                        else cancelar.SetEnabled(true);
                    });
                    cancelar.name = "conv-cancelar";
                    acciones.Add(cancelar);
                    var excede = (mio.Seleccion?.EscuadronIds ?? new Google.Protobuf.Collections.RepeatedField<string>())
                        .Sum(id => LiderazgoDe(c.Sesion.Almacen.Buscar(TipoEntidad.Escuadron, id)?.Escuadron?.TropaId) ?? 0) > h.LiderazgoBase;
                    partir = Dialogo.Boton("SALIR CON EL EJÉRCITO", "boton-primario salida-salir salida-ejercito-boton", async () =>
                    {
                        partir.SetEnabled(false);
                        var o = new OrdenPartirConvocatoria { HeroeId = h.Id };
                        if (caravanaDeLaConvocatoria.TryGetValue(cv.Id, out var k) && k != "") o.CaravanaFundacionId = k;
                        if (!await OrdenConError(new Orden { PartirConvocatoria = o }, Error)) { partir.SetEnabled(true); return; }
                        Cerrar();
                        if (cerrarInterior != null) await cerrarInterior();
                        if (this != null) Mapa();
                    });
                    partir.name = "conv-partir";
                    partir.SetEnabled(!excede);
                    if (excede) partir.tooltip = "Te pasas de Liderazgo: quita escuadras y guarda";
                    acciones.Add(partir);
                }
                else
                {
                    Button separarme = null;
                    separarme = Dialogo.Boton("Separarme (sigues dentro)", "boton-primario", async () =>
                    {
                        separarme.SetEnabled(false);
                        if (await OrdenConError(new Orden { SepararseDeConvocatoria = new OrdenSepararseDeConvocatoria { HeroeId = h.Id } }, Error)) { Cerrar(); VentanaDeSalida(campId, lugar, plazaId); }
                        else separarme.SetEnabled(true);
                    });
                    separarme.name = "conv-separarme";
                    acciones.Add(separarme);
                }
                pie.Add(acciones);
            }

            Pintar();
            capaEncima.Add(velo);
            // Lo que cambia por otros (peticiones, integrantes, la salida del Líder) se repinta solo; lo que estás escribiendo, no.
            vigilar = velo.schedule.Execute(() =>
            {
                var h = HeroeAhora();
                var cv = MiConvocatoria(h);
                if (cv == null || FirmaExterna(cv, h) != firma) Pintar();
            }).Every(500);
        }
    }
}
