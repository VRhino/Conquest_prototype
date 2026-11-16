// El ejército en el mapa (paso 11 del bloque 4, tanda 2), calcado de BronzeAgeClient (`ui/ejercitos.ts` y `ui/unirseDesdePlaza.ts`).
// - La ventana Ejército (⚑, abajo en el mapa, solo con tu columna fuera): quiénes van con su tropa, la carga y lo que toca según tu columna:
//   sola, «Organizar ejército» (`organizarEjercito`); formándose, «Cancelar la formación» o «Separarme»; ejército, el Líder con las peticiones
//   (`responderPeticionDeUnion`), «Acampar aquí» (`estacionarEjercito`) y «Ceder el mando» (`cederLiderazgo`), y los demás con «Separarme del
//   ejército» (`separarseDelEjercito`). «Replegar» no entra: el motor ya no tiene esa orden.
// - «Unirse a un ejército» en el riel de tu plaza, solo si un ejército de tu Facción que admite a alguien está a su puerta y tienes tropa libre
//   en el campamento: marcas lo que aportas y eliges el ejército (`unirseAEjercito`).
// Unirse en campo y a una batalla van en la ficha del mapa (`CosasDelMapa.Acciones`). El motor valida todo; su motivo sale tal cual.
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
        /// <summary>Héroes que hacen falta para que una formación sea un ejército (`FORMACION_EJERCITO.minimo`).</summary>
        int MinimoDeFormacion => (int)c.Datos.Numero("documentos.Movimiento_Ejercitos.FORMACION_EJERCITO.minimo");

        static readonly Dictionary<EstadoColumna, string> EstadoDeColumna = new Dictionary<EstadoColumna, string>
        {
            [EstadoColumna.ColumnaMarchando] = "en marcha", [EstadoColumna.ColumnaEstacionada] = "acampado",
        };

        bool ventanaDeEjercito;
        string firmaDeEjercito;

        /// <summary>El botón ⚑ abajo a la izquierda del mapa (`.mapa-riel`), que abre y cierra la ventana Ejército. Solo con tu columna en el mapa.</summary>
        void RielDelMapa(VisualElement v)
        {
            var riel = v.Q("mapa-riel");
            var b = new Button(() => { ventanaDeEjercito = !ventanaDeEjercito; firmaDeEjercito = null; PintarVentanaDeEjercito(v); }) { name = "riel-ejercito", tooltip = "Ejército: quién va, su tropa y la carga" };
            b.AddToClassList("mapa-riel-boton");
            b.Add(IconosDelMundo.Icono("simbolo", "bandera", 22, IconosDelMundo.OroClaro));
            riel.Add(b);
        }

        /// <summary>La ventana Ejército, si está abierta: se rehace solo cuando cambia tu columna, tu héroe o lo que corre (plazos de la formación y de
        /// las peticiones, al segundo).</summary>
        void PintarVentanaDeEjercito(VisualElement v)
        {
            var col = MiColumna();
            var h = HeroeAhora();
            Ver(v.Q("mapa-riel"), col != null);
            v.Q<Button>("riel-ejercito")?.EnableInClassList("activo", ventanaDeEjercito && col != null);
            var placa = v.Q("ventana-ejercito");
            if (!ventanaDeEjercito || col == null || h == null) { placa?.RemoveFromHierarchy(); firmaDeEjercito = null; return; }
            var otros = col.Participantes.Where(x => x.HeroeId != h.Id).Select(x => c.Sesion.Almacen.Buscar(TipoEntidad.Heroe, x.HeroeId)?.Heroe?.Version.ToString()).ToList();
            var firma = $"{col.Version}|{h.Version}|{string.Join(",", otros)}|{string.Join(",", EscuadrasDeLaColumna().Select(s => s.Version))}|{Math.Floor(AhoraMs / 1000)}|{FirmaDeCaravanasDeColumna(col)}";
            if (placa != null && firma == firmaDeEjercito) return;
            firmaDeEjercito = firma;
            placa?.RemoveFromHierarchy();
            placa = Dialogo.Caja("placa ventana-ejercito");
            placa.name = "ventana-ejercito";
            var filete = Dialogo.Caja("placa-filete ventana-puerta-filete");
            placa.Add(filete);
            var a = c.Sesion.Almacen;
            var lider = col.LiderId == h.Id;
            var formando = col.HasFormacionExpiraEnMs;
            var hombres = col.Participantes.Sum(x => TropaDe(col, x.HeroeId, h).Sum(t => (long)t.cantidad));
            var titulo = formando ? "Formación" : col.Tipo == TipoColumna.ColumnaEjercito ? $"Ejército de {Textos.NombreDeHeroe(a, col.LiderId)}" : "Tu columna";
            var cab = Dialogo.Caja("ventana-puerta-cab");
            var textos = Dialogo.Caja("ejercito-cab-textos");
            textos.Add(Dialogo.Texto(titulo, "dialogo-titulo"));
            textos.Add(Dialogo.Texto($"{col.Participantes.Count} héroe(s) · {hombres} hombres{(formando ? "" : " · " + (EstadoDeColumna.TryGetValue(col.Estado, out var e) ? e : ""))}", "campo-ayuda"));
            cab.Add(textos);
            var cerrar = Dialogo.Boton("×", "aviso-borrar", () => { ventanaDeEjercito = false; PintarVentanaDeEjercito(v); });
            cerrar.name = "ejercito-cerrar";
            cab.Add(cerrar);
            filete.Add(cab);
            var cuerpo = Dialogo.Caja("ejercito-cuerpo");
            filete.Add(cuerpo);
            ScrollView Columna()
            {
                var s = new ScrollView(ScrollViewMode.Vertical) { horizontalScrollerVisibility = ScrollerVisibility.Hidden };
                s.AddToClassList("ejercito-col");
                cuerpo.Add(s);
                return s;
            }
            var izquierda = Columna();
            var derecha = Columna();
            var error = Dialogo.Texto("", "error oculto");
            error.name = "error-ejercito";
            void Error(string m) => Poner(error, m);
            Button Boton(string texto, string nombre, string clase, Func<Orden> orden)
            {
                Button b = null;
                b = Dialogo.Boton(texto, clase, async () =>
                {
                    b.SetEnabled(false);
                    if (await OrdenConError(orden(), Error)) firmaDeEjercito = null;
                    b.SetEnabled(true);
                });
                b.name = nombre;
                return b;
            }

            // Izquierda: los integrantes, el Líder primero, con su tropa.
            izquierda.Add(Dialogo.Texto("Integrantes", "salida-recuadro-sub"));
            var lista = Dialogo.Caja("conv-integrantes");
            foreach (var x in col.Participantes.OrderByDescending(x => x.HeroeId == col.LiderId).ThenBy(x => x.UnidoEnMs))
            {
                var fila = Dialogo.Caja("conv-integrante");
                fila.name = "integrante-" + x.HeroeId;
                var tropa = TropaDe(col, x.HeroeId, h);
                var papel = string.Join(" · ", new[] { x.HeroeId == col.LiderId && col.Participantes.Count > 1 ? "Líder" : null, x.HeroeId == h.Id ? "tú" : null }.Where(t => t != null));
                var fc = Dialogo.Caja("conv-integrante-cab");
                fc.Add(Dialogo.Texto(Textos.NombreDeHeroe(a, x.HeroeId), "aviso-titulo"));
                if (papel != "") fc.Add(Dialogo.Texto(papel, "conv-papel"));
                fc.Add(Dialogo.Texto($"{tropa.Count} escuadra(s) · {tropa.Sum(t => (long)t.cantidad)} hombres", "campo-ayuda"));
                fila.Add(fc);
                var tropas = Dialogo.Caja("conv-integrante-tropas");
                foreach (var t in tropa) if (PiezasDeTropa.Miniatura(t.tropaId, 28, $"{NombreDeTropa(t.tropaId)} · {t.cantidad} hombres · nivel {t.nivel}") is VisualElement mini) tropas.Add(mini);
                if (tropa.Count == 0) tropas.Add(Dialogo.Texto("Sin tropa.", "campo-ayuda"));
                fila.Add(tropas);
                lista.Add(fila);
            }
            izquierda.Add(lista);

            // Derecha: lo que toca según tu columna, y la carga.
            if (formando)
            {
                derecha.Add(ConAyuda(Dialogo.Texto($"Formación: {col.Participantes.Count}/{MinimoDeFormacion} héroes, {NombreDePolitica[Politica(col.PoliticaDeUnion)].ToLowerInvariant()}. Se deshace en {FaltaPara(col.FormacionExpiraEnMs)}.", "dialogo-texto"),
                    $"Se deshace si no llegáis a {MinimoDeFormacion}: con menos de {MinimoDeFormacion} no es un ejército y no se mueve. Te quedas quieto hasta entonces."));
                derecha.Add(lider ? Boton("Cancelar la formación", "ej-cancelar-formacion", "boton-secundario", () => new Orden { CancelarFormacion = new OrdenCancelarFormacion { HeroeId = h.Id } })
                    : Boton("Separarme", "ej-separarme", "boton-secundario", () => new Orden { SepararseDelEjercito = new OrdenSepararseDelEjercito { HeroeId = h.Id } }));
            }
            else if (col.Tipo == TipoColumna.ColumnaEjercito)
            {
                var resto = col.Participantes.Where(x => x.HeroeId != h.Id).ToList();
                derecha.Add(ConAyuda(Dialogo.Texto($"Unión: {NombreDePolitica[Politica(col.PoliticaDeUnion)].ToLowerInvariant()}.", "dialogo-texto"),
                    (lider ? "Tú lo diriges: haz clic en el mapa y marchará hacia allí; cada clic cambia el rumbo." : "Lo dirige su Líder con clics en el mapa.")
                    + (lider && resto.Count > 0 ? " El Líder no puede separarse: para irse tiene que ceder el mando antes." : "")
                    + (lider ? "" : " Si te separas, te llevas lo tuyo y vuelves a ser una columna personal donde estés.")));
                var vivas = lider ? col.PeticionesDeUnion.Where(x => x.ExpiraEnMs > AhoraMs).ToList() : new List<PeticionUnion>();
                if (vivas.Count > 0) derecha.Add(Dialogo.Texto("Piden unirse", "salida-recuadro-sub"));
                foreach (var p in vivas)
                {
                    var fila = Dialogo.Caja("ejercito-fila");
                    fila.name = "ej-peticion-" + p.HeroeId;
                    var t = Dialogo.Caja("ejercito-fila-texto");
                    t.Add(Dialogo.Texto(Textos.NombreDeHeroe(a, p.HeroeId), "aviso-titulo"));
                    t.Add(Dialogo.Texto($"{FaltaPara(p.ExpiraEnMs)} para contestar", "campo-ayuda"));
                    fila.Add(t);
                    var sol = p.HeroeId;
                    fila.Add(Boton("Aceptar", "ej-aceptar-" + sol, "boton-primario", () => new Orden { ResponderPeticionDeUnion = new OrdenResponderPeticionDeUnion { EjercitoId = col.Id, HeroeId = h.Id, SolicitanteId = sol, Aceptar = true } }));
                    fila.Add(Boton("Rechazar", "ej-rechazar-" + sol, "boton-secundario", () => new Orden { ResponderPeticionDeUnion = new OrdenResponderPeticionDeUnion { EjercitoId = col.Id, HeroeId = h.Id, SolicitanteId = sol, Aceptar = false } }));
                    derecha.Add(fila);
                }
                if (lider)
                {
                    var acciones = Dialogo.Caja("ejercito-acciones");
                    if (col.Estado == EstadoColumna.ColumnaMarchando)
                        acciones.Add(Boton("Acampar aquí", "ej-estacionar", "boton-secundario", () => new Orden { EstacionarEjercito = new OrdenEstacionarEjercito { EjercitoId = col.Id } }));
                    if (acciones.childCount > 0) derecha.Add(acciones);
                    if (resto.Count > 0)
                    {
                        var fila = Dialogo.Caja("ejercito-fila");
                        var sucesor = new DropdownField(resto.Select(x => Textos.NombreDeHeroe(a, x.HeroeId)).ToList(), 0) { name = "ej-sucesor" };
                        sucesor.AddToClassList("tropas-orden");
                        fila.Add(sucesor);
                        fila.Add(Boton("Ceder el mando", "ej-ceder", "boton-secundario", () => new Orden { CederLiderazgo = new OrdenCederLiderazgo { EjercitoId = col.Id, HeroeId = h.Id, SucesorId = resto[Math.Max(0, sucesor.index)].HeroeId } }));
                        derecha.Add(fila);
                    }
                }
                else derecha.Add(Boton("Separarme del ejército", "ej-separarme", "boton-secundario", () => new Orden { SepararseDelEjercito = new OrdenSepararseDelEjercito { HeroeId = h.Id } }));
            }
            else
            {
                derecha.Add(ConAyuda(Dialogo.Texto("Vas por tu cuenta.", "dialogo-texto"),
                    $"Con otros {MinimoDeFormacion - 1} héroes de tu Facción puedes formar un ejército aquí, sin pasar por una plaza: te quedas quieto hasta que se unan."));
                if (!h.HasFaccionId) derecha.Add(Dialogo.Texto("Sin Facción nadie podrá unirse a ti: un ejército lo componen ciudadanos de una sola Facción.", "campo-ayuda aviso-fuerte"));
                var acciones = Dialogo.Caja("ejercito-acciones");
                foreach (var (pol, texto) in new[] { ("aceptar", "Organizar ejército (abierto)"), ("preguntar", "Organizar ejército (decido yo)") })
                {
                    var p = pol;
                    acciones.Add(Boton(texto, "ej-organizar-" + p, "boton-secundario", () => new Orden { OrganizarEjercito = new OrdenOrganizarEjercito { HeroeId = h.Id, PoliticaDeUnion = p } }));
                }
                derecha.Add(acciones);
            }
            derecha.Add(Dialogo.Texto("Carga", "salida-recuadro-sub"));
            var carga = col.Suministro?.PorId.Where(kv => kv.Value >= 1000).OrderByDescending(kv => kv.Value).ToList() ?? new List<KeyValuePair<string, long>>();
            var recursos = Dialogo.Caja("ejercito-recursos");
            recursos.name = "ej-carga";
            foreach (var kv in carga)
            {
                var r = IconosDelMundo.ConIcono("recurso", kv.Key, Dialogo.Texto(Math.Floor(kv.Value / 1000.0).ToString(), "aviso-titulo"), 18);
                r.tooltip = CosasDelMapa.NombreDeRecurso(kv.Key);
                recursos.Add(r);
            }
            if (carga.Count == 0) recursos.Add(Dialogo.Texto("Nada.", "campo-ayuda"));
            derecha.Add(recursos);
            CaravanasDeLaColumna(derecha, col, h, Error);
            derecha.Add(error);
            v.Add(placa);
        }

        // ─────────────────────────── las caravanas de la columna ───────────────────────────

        /// <summary>Las caravanas que lleva la columna: la de Fundación y las comerciales enganchadas, las que llegan en la sesión.</summary>
        List<Caravana> CaravanasDe(Ejercito col)
        {
            var ids = col.CaravanasAdjuntasIds.ToList();
            if (!string.IsNullOrEmpty(col.CaravanaFundacionId) && !ids.Contains(col.CaravanaFundacionId)) ids.Insert(0, col.CaravanaFundacionId);
            return ids.Select(id => c.Sesion.Almacen.Buscar(TipoEntidad.Caravana, id)?.Caravana).Where(k => k != null).ToList();
        }

        /// <summary>Las plazas de tu Facción a cuya puerta está la columna (de donde se carga): la más cercana primero.</summary>
        List<Asentamiento> PlazasALaPuerta(Ejercito col, Heroe h)
        {
            if (mapa?.Centro == null || !h.HasFaccionId) return new List<Asentamiento>();
            var (x, y) = mapa.Centro.Value;
            return c.Sesion.Almacen.Todas(Entidad.CuerpoOneofCase.Asentamiento).Select(e => e.Asentamiento)
                .Where(a => a.FaccionId == h.FaccionId && a.Posicion != null && CosasDelMapa.Distancia(a.Posicion.X / 1000.0, a.Posicion.Y / 1000.0, x, y) <= c.Datos.RadioPuerta)
                .OrderBy(a => CosasDelMapa.Distancia(a.Posicion.X / 1000.0, a.Posicion.Y / 1000.0, x, y)).ToList();
        }

        /// <summary>Los trueques en vigor en que tu Facción debe algo (`ladoPendienteParaEjercito`): lo que falta y la plaza que lo recibe.</summary>
        List<(AcuerdoTrueque acuerdo, List<(string recurso, long falta)> faltan, string destinoId)> TruequesPendientes(Heroe h)
        {
            string FaccionDe(string id) => c.Sesion.Almacen.Buscar(TipoEntidad.Asentamiento, id)?.Asentamiento?.FaccionId;
            List<(string recurso, long falta)> Faltan(IEnumerable<LineaTrueque> ls) => ls.Select(l => (l.Recurso, (l.CantidadTotal - l.CantidadEntregada) / 1000)).Where(l => l.Item2 > 0).ToList();
            var r = new List<(AcuerdoTrueque, List<(string, long)>, string)>();
            foreach (var x in c.Sesion.Almacen.Todas(Entidad.CuerpoOneofCase.AcuerdoTrueque).Select(e => e.AcuerdoTrueque).Where(x => x.Estado == EstadoTrueque.TruequeActivo))
            {
                var fa = Faltan(x.LineasA);
                var fb = Faltan(x.LineasB);
                if (FaccionDe(x.AsentamientoAId) == h.FaccionId && fa.Count > 0) r.Add((x, fa, x.AsentamientoBId));
                else if (FaccionDe(x.AsentamientoBId) == h.FaccionId && fb.Count > 0) r.Add((x, fb, x.AsentamientoAId));
            }
            return r;
        }

        string FirmaDeCaravanasDeColumna(Ejercito col)
        {
            var h = HeroeAhora();
            return h == null ? "" : $"{string.Join(",", CaravanasDe(col).Select(k => k.Id + ":" + k.Version))}|{string.Join(",", PlazasALaPuerta(col, h).Select(a => a.Id + ":" + a.Version))}"
                + $"|{string.Join(",", TruequesPendientes(h).Select(t => t.acuerdo.Id + ":" + t.acuerdo.Version))}";
        }

        /// <summary>Las caravanas de la columna en la ventana Ejército (`htmlCaravanasDeLaColumna` y `caravanasAdjuntas.ts`): cada una con sus carros (el
        /// animal de cada uno), su carga y, si se puede, «Cargar» de la plaza de tu Facción a cuya puerta estás (`cargarCaravana`) y «Entregar» a un
        /// trueque en vigor, a la puerta de la plaza que recibe (`entregarDeCaravana`). Aparcar o soltar es dentro de una plaza: allí, en su lateral.</summary>
        void CaravanasDeLaColumna(VisualElement donde, Ejercito col, Heroe h, Action<string> error)
        {
            var caravanas = CaravanasDe(col);
            if (caravanas.Count == 0) return;
            donde.Add(ConAyuda(Dialogo.Texto("Caravanas", "salida-recuadro-sub"),
                "Van con la columna, a su paso. Cualquiera que vaya en ella las carga a la puerta de una plaza de tu Facción y entrega a la puerta de la que recibe un trueque. Se aparcan entrando con el ejército en una plaza de tu Facción."));
            var plazas = PlazasALaPuerta(col, h);
            var trueques = TruequesPendientes(h);
            foreach (var k in caravanas)
            {
                var fundacion = k.Tipo == TipoCaravana.CaravanaDeFundacion;
                var caja = Dialogo.Caja("ejercito-caravana");
                caja.name = "ej-caravana-" + k.Id;
                var cab = Dialogo.Caja("conv-integrante-cab");
                cab.Add(Dialogo.Texto(fundacion ? "Caravana de Fundación" : NombreDeCaravana(k), "aviso-titulo"));
                cab.Add(Dialogo.Texto($"lleva {CargaDe(k)}", "campo-ayuda"));
                caja.Add(cab);
                if (k.Carros.Count > 0)
                    caja.Add(Dialogo.Texto($"{k.Carros.Count} carro(s): {string.Join(" ", k.Carros.Select(r => r.HasAnimal ? IconoDeAnimal(r.Animal) : "∅"))}", "campo-ayuda"));
                var carga = Dialogo.Caja("ejercito-recursos");
                carga.name = "ej-caravana-carga-" + k.Id;
                foreach (var kv in k.Contenido?.PorId.Where(kv => kv.Value >= 1000).OrderByDescending(kv => kv.Value) ?? Enumerable.Empty<KeyValuePair<string, long>>())
                    carga.Add(IconosDelMundo.ConIcono("recurso", kv.Key, Dialogo.Texto(Math.Floor(kv.Value / 1000.0).ToString(), "aviso-titulo"), 16));
                if (carga.childCount > 0) caja.Add(carga);
                if (!fundacion && plazas.Count > 0)
                {
                    // Cargar: de la plaza a cuya puerta está la columna, lo que tenga su almacén.
                    var plaza = plazas[0];
                    var hay = plaza.Almacen.Select(kv => (r: kv.Key, n: (int)(Instante.Valor(kv.Value.Cantidad, AhoraMs) / 1000))).Where(x => x.n >= 1).OrderBy(x => x.r).ToList();
                    if (hay.Count > 0)
                    {
                        var fila = Dialogo.Caja("ejercito-cargar");
                        var recurso = new DropdownField(hay.Select(x => CosasDelMapa.NombreDeRecurso(x.r)).ToList(), 0) { name = "ej-cargar-recurso-" + k.Id };
                        recurso.AddToClassList("tropas-orden");
                        var cantidad = new IntegerField { value = Math.Min(100, hay[0].n), name = "ej-cargar-cantidad-" + k.Id };
                        cantidad.AddToClassList("ejercito-cantidad");
                        recurso.RegisterValueChangedCallback(_ => cantidad.value = Math.Min(Math.Max(1, cantidad.value), hay[Math.Max(0, recurso.index)].n));
                        cantidad.RegisterValueChangedCallback(ev => cantidad.SetValueWithoutNotify(Math.Max(1, Math.Min(ev.newValue, hay[Math.Max(0, recurso.index)].n))));
                        fila.Add(recurso);
                        fila.Add(cantidad);
                        Button cargar = null;
                        cargar = Dialogo.Boton($"Cargar de {(plaza.HasNombre ? plaza.Nombre : plaza.Id)}", "boton-secundario", async () =>
                        {
                            cargar.SetEnabled(false);
                            if (await OrdenConError(new Orden { CargarCaravana = new OrdenCargarCaravana { EjercitoId = col.Id, CaravanaId = k.Id, AsentamientoId = plaza.Id, Recurso = hay[Math.Max(0, recurso.index)].r, Cantidad = cantidad.value * 1000L } }, error))
                                firmaDeEjercito = null;
                            cargar.SetEnabled(true);
                        });
                        cargar.name = "ej-cargar-" + k.Id;
                        fila.Add(cargar);
                        caja.Add(fila);
                    }
                }
                // Entregar: a cada trueque en vigor en que debes algo, a la puerta de la plaza que recibe y con algo de lo que falta en la caravana.
                foreach (var (acuerdo, faltan, destinoId) in trueques)
                {
                    var destino = c.Sesion.Almacen.Buscar(TipoEntidad.Asentamiento, destinoId)?.Asentamiento;
                    var lleva = faltan.Any(f => k.Contenido != null && k.Contenido.PorId.TryGetValue(f.recurso, out var n) && n >= 1000);
                    if (destino?.Posicion == null || mapa?.Centro == null || !lleva) continue;
                    if (CosasDelMapa.Distancia(destino.Posicion.X / 1000.0, destino.Posicion.Y / 1000.0, mapa.Centro.Value.x, mapa.Centro.Value.y) > c.Datos.RadioPuerta) continue;
                    var fila = Dialogo.Caja("ejercito-fila");
                    var t = Dialogo.Caja("ejercito-fila-texto");
                    t.Add(Dialogo.Texto($"Trueque con {(destino.HasNombre ? destino.Nombre : destinoId)}", "aviso-titulo"));
                    var falta = Dialogo.Caja("ejercito-recursos");
                    foreach (var f in faltan) falta.Add(IconosDelMundo.ConIcono("recurso", f.recurso, Dialogo.Texto($"faltan {f.falta}", "campo-ayuda"), 14));
                    t.Add(falta);
                    fila.Add(t);
                    var id = acuerdo.Id;
                    Button entregar = null;
                    entregar = Dialogo.Boton("Entregar", "boton-primario", async () =>
                    {
                        entregar.SetEnabled(false);
                        if (await OrdenConError(new Orden { EntregarDeCaravana = new OrdenEntregarDeCaravana { EjercitoId = col.Id, CaravanaId = k.Id, AcuerdoId = id } }, error)) firmaDeEjercito = null;
                        entregar.SetEnabled(true);
                    });
                    entregar.name = $"ej-entregar-{k.Id}-{id}";
                    fila.Add(entregar);
                    caja.Add(fila);
                }
                donde.Add(caja);
            }
        }

        // ─────────────────────────── aparcar, dentro de una plaza ───────────────────────────

        /// <summary>Dentro de una plaza de tu Facción con tu ejército: las caravanas que lleva, para dejarlas aparcadas aquí (`aparcarCaravana`, el
        /// Líder). Una tarjeta más del lateral derecho, encima de las de los ejércitos.</summary>
        void PintarCaravanasDePlaza(VisualElement der, Asentamiento a, Heroe yo, Action rehacer)
        {
            der.Q("caravanas-plaza")?.RemoveFromHierarchy();
            var col = ColumnaDelHeroe(yo);
            if (col == null || col.LiderId != yo.Id || a.FaccionId != yo.FaccionId) return;
            var caravanas = CaravanasDe(col).Where(k => k.Tipo == TipoCaravana.CaravanaComercial).ToList();
            if (caravanas.Count == 0) return;
            var t = Dialogo.Caja("ejercito-tarjeta");
            t.name = "caravanas-plaza";
            var cab = Dialogo.Caja("ejercito-tarjeta-cab");
            cab.Add(IconosDelMundo.Icono("simbolo", "carro", 18, IconosDelMundo.Bronce));
            var textos = Dialogo.Caja("ejercito-fila-texto");
            textos.Add(Dialogo.Texto("Caravanas de tu ejército", "aviso-titulo"));
            textos.Add(Dialogo.Texto("Se pueden quedar aparcadas aquí", "campo-ayuda"));
            cab.Add(textos);
            t.Add(cab);
            foreach (var k in caravanas)
            {
                var fila = Dialogo.Caja("ejercito-fila");
                var tx = Dialogo.Caja("ejercito-fila-texto");
                tx.Add(Dialogo.Texto(NombreDeCaravana(k), "aviso-titulo"));
                tx.Add(Dialogo.Texto($"{k.Carros.Count} carro(s) · {(CargaDe(k) > 0 ? $"lleva {CargaDe(k)}" : "vacía")}", "campo-ayuda"));
                fila.Add(tx);
                Button b = null;
                b = Dialogo.Boton("Aparcar aquí", "boton-secundario", async () =>
                {
                    if (await OrdenDePlaza(new Orden { AparcarCaravana = new OrdenAparcarCaravana { CaravanaId = k.Id, AsentamientoId = a.Id } }, b)) rehacer();
                });
                b.name = "aparcar-" + k.Id;
                fila.Add(b);
                t.Add(fila);
            }
            der.Insert(0, t);
        }

        string FirmaDeCaravanasDePlaza(Heroe yo) => ColumnaDelHeroe(yo) is Ejercito col ? $"{col.Version}|{col.LiderId}|{FirmaDeCaravanasDeColumna(col)}" : "";

        /// <summary>Lo que lleva cada integrante: lo tuyo, tus escuadras en la columna; lo de los demás, lo que el motor deja ver (`escuadras_que_lleva`).</summary>
        List<(string tropaId, uint cantidad, uint nivel)> TropaDe(Ejercito col, string heroeId, Heroe yo)
        {
            if (heroeId == yo.Id) return EscuadrasDeLaColumna().Select(s => (s.TropaId, s.Cantidad, s.Nivel)).ToList();
            var otro = c.Sesion.Almacen.Buscar(TipoEntidad.Heroe, heroeId)?.Heroe;
            return otro?.EscuadrasQueLleva.Select(t => (t.TropaId, t.Cantidad, t.Nivel)).ToList() ?? new List<(string, uint, uint)>();
        }

        // ─────────────────────────── unirse desde la plaza ───────────────────────────

        /// <summary>Las escuadras que NO has marcado para aportar (por defecto van todas); sobrevive a los repintados.</summary>
        readonly HashSet<string> desmarcadasParaUnirse = new HashSet<string>();

        List<Escuadron> TropaLibre(Heroe yo) => EscuadrasDelHeroe(yo).Where(s => s.Cantidad > 0 && s.Contenedor?.DondeCase == ContenedorEscuadron.DondeOneofCase.Campamento && !s.EnGuarnicion).ToList();

        /// <summary>Los ejércitos de tu Facción a los que te puedes unir ahora desde esta plaza (`ejercitosParaUnirse`): resides, tienes tropa libre y
        /// el ejército, que admite a alguien, está a su puerta. Vacío si no hay ninguno (y entonces no sale ni su botón del riel).</summary>
        List<Ejercito> EjercitosParaUnirse(Asentamiento a, Heroe yo)
        {
            if (yo == null || !yo.HasResidenciaId || yo.ResidenciaId != a.Id || TropaLibre(yo).Count == 0) return new List<Ejercito>();
            var (px, py) = (a.Posicion.X / 1000.0, a.Posicion.Y / 1000.0);
            return c.Sesion.Almacen.Todas(Entidad.CuerpoOneofCase.Ejercito).Select(e => e.Ejercito)
                .Where(e => e.FaccionId == a.FaccionId && e.Tipo == TipoColumna.ColumnaEjercito && !e.HasFormacionExpiraEnMs && !e.HasDentroDe
                    && e.PoliticaDeUnion != PoliticaUnion.UnionRechazar && !e.Participantes.Any(x => x.HeroeId == yo.Id)
                    && (e.Plan != null || e.Posicion != null))
                .Where(e => { var p = Instante.Posicion(e.Plan, e.Posicion, AhoraMs); return CosasDelMapa.Distancia(p.x / 1000, p.y / 1000, px, py) <= c.Datos.RadioPuerta; })
                .ToList();
        }

        string FirmaDeUnirse(Asentamiento a, Heroe yo) => string.Join(",", EjercitosParaUnirse(a, yo).Select(e => e.Id + ":" + e.Version));

        // ─────────────────────────── las tarjetas del lateral de la plaza ───────────────────────────

        /// <summary>Si los integrantes de las tarjetas van desplegados (se recuerda entre plazas y sesiones).</summary>
        const string PrefIntegrantes = "mundo.plaza.integrantes";

        /// <summary>Las convocatorias de tu Facción que se preparan dentro de esta plaza y a las que puedes unirte desde «Salir al mundo» (resides).</summary>
        List<Convocatoria> ConvocatoriasDePlaza(Asentamiento a, Heroe yo) =>
            yo == null || !yo.HasResidenciaId || yo.ResidenciaId != a.Id || MiConvocatoria(yo) != null ? new List<Convocatoria>() : ConvocatoriasDe(a.Id, yo);

        string FirmaDeEjercitosDePlaza(Asentamiento a, Heroe yo)
        {
            var heroes = EjercitosParaUnirse(a, yo).SelectMany(e => e.Participantes.Select(x => x.HeroeId))
                .Select(id => c.Sesion.Almacen.Buscar(TipoEntidad.Heroe, id)?.Heroe?.Version);
            return $"{FirmaDeUnirse(a, yo)}|{string.Join(",", heroes)}|{string.Join(",", ConvocatoriasDePlaza(a, yo).Select(x => x.Id + ":" + x.Version))}";
        }

        /// <summary>Las tarjetas del lateral derecho de la plaza, en el hueco de las misiones (Víctor, 2026-11-16): una por ejército al que te puedes
        /// unir. Los de tu Facción a la puerta («Unirse» abre la ventana «Unirse a un ejército» del riel) y los que se preparan dentro («Unirse» abre
        /// «Salir al mundo» con «Ejército»). Con sus integrantes y su tropa, plegables. Solo salen si hay a qué unirse.</summary>
        void PintarEjercitosDePlaza(VisualElement der, Asentamiento a, Heroe yo, Action rehacer)
        {
            der.Q("ejercitos-plaza")?.RemoveFromHierarchy();
            var puerta = EjercitosParaUnirse(a, yo);
            var dentro = ConvocatoriasDePlaza(a, yo);
            if (puerta.Count + dentro.Count == 0) return;
            var caja = new ScrollView(ScrollViewMode.Vertical) { name = "ejercitos-plaza", horizontalScrollerVisibility = ScrollerVisibility.Hidden };
            caja.AddToClassList("ejercitos-plaza");
            var abiertos = PlayerPrefs.GetInt(PrefIntegrantes, 1) == 1;
            var almacen = c.Sesion.Almacen;
            void Tarjeta(string id, string titulo, string detalle, IEnumerable<(string heroeId, string papel, List<string> tropa)> integrantes, Action unirse)
            {
                var t = Dialogo.Caja("ejercito-tarjeta");
                t.name = "tarjeta-" + id;
                var cab = Dialogo.Caja("ejercito-tarjeta-cab");
                cab.Add(IconosDelMundo.Icono("simbolo", "bandera", 18, IconosDelMundo.Bronce));
                var textos = Dialogo.Caja("ejercito-fila-texto");
                textos.Add(Dialogo.Texto(titulo, "aviso-titulo"));
                textos.Add(Dialogo.Texto(detalle, "campo-ayuda"));
                cab.Add(textos);
                t.Add(cab);
                var plegable = new Foldout { text = "Integrantes", value = abiertos, name = "integrantes-" + id };
                plegable.AddToClassList("ejercito-tarjeta-integrantes");
                plegable.RegisterValueChangedCallback(ev => { if (ev.target == plegable) PlayerPrefs.SetInt(PrefIntegrantes, ev.newValue ? 1 : 0); });
                foreach (var (heroeId, papel, tropa) in integrantes)
                {
                    var fila = Dialogo.Caja("conv-integrante");
                    var fc = Dialogo.Caja("conv-integrante-cab");
                    fc.Add(Dialogo.Texto(Textos.NombreDeHeroe(almacen, heroeId), "aviso-titulo"));
                    if (papel != null) fc.Add(Dialogo.Texto(papel, "conv-papel"));
                    fila.Add(fc);
                    var tropas = Dialogo.Caja("conv-integrante-tropas");
                    foreach (var tid in tropa) if (PiezasDeTropa.Miniatura(tid, 26, NombreDeTropa(tid)) is VisualElement mini) tropas.Add(mini);
                    if (tropa.Count == 0) tropas.Add(Dialogo.Texto("Sin tropa.", "campo-ayuda"));
                    fila.Add(tropas);
                    plegable.Add(fila);
                }
                t.Add(plegable);
                var b = Dialogo.Boton("Unirse", "boton-primario ejercito-tarjeta-unirse", unirse);
                b.name = "tarjeta-unirse-" + id;
                t.Add(b);
                caja.Add(t);
            }
            foreach (var e in puerta)
                Tarjeta(e.Id, $"Ejército de {Textos.NombreDeHeroe(almacen, e.LiderId)} a la puerta", $"{e.Participantes.Count} héroe(s) · {NombreDePolitica[Politica(e.PoliticaDeUnion)].ToLowerInvariant()}",
                    e.Participantes.OrderByDescending(x => x.HeroeId == e.LiderId).Select(x => (x.HeroeId, x.HeroeId == e.LiderId ? "Líder" : null, TropaDe(e, x.HeroeId, yo).Select(t => t.tropaId).ToList())),
                    () => { ventanaPlaza = "unirse"; rehacer(); });
            foreach (var cv in dentro)
                Tarjeta(cv.Id, $"Ejército de {Textos.NombreDeHeroe(almacen, cv.LiderId)} preparándose", $"{cv.Integrantes.Count} héroe(s) · {NombreDePolitica[Politica(cv.PoliticaDeUnion)].ToLowerInvariant()}",
                    cv.Integrantes.OrderByDescending(x => x.HeroeId == cv.LiderId).Select(x => (x.HeroeId, x.HeroeId == cv.LiderId ? "Líder" : null, x.Seleccion?.TropaIds.ToList() ?? new List<string>())),
                    () => { salirComoEjercito = true; VentanaDeSalida(null, a.HasNombre ? a.Nombre : a.Id, a.Id); });
            der.Insert(0, caja);
        }

        /// <summary>«Unirse a un ejército» (`htmlUnirseDesdePlaza`): la tropa que aportas, marcada, y los ejércitos a la puerta, cada uno con «Unirme con
        /// esta tropa».</summary>
        void UnirseDesdePlaza(VisualElement cuerpo, Asentamiento a, Heroe yo, Action rehacer)
        {
            var almacen = c.Sesion.Almacen;
            var tropa = TropaLibre(yo);
            cuerpo.Add(TituloDeSeccion("Tropa que aportas", "Cuando un ejército de tu Facción está a la puerta de tu plaza puedes sumarte a él con tropa fresca de tu campamento. Pasas a ir donde lo dirija su Líder. Unirse en campo, con lo que ya llevas encima, se hace desde el mapa."));
            var lista = Dialogo.Caja("unirse-tropa");
            foreach (var s in tropa)
            {
                var id = s.Id;
                var fila = Dialogo.Caja("salida-fila unirse-fila");
                if (PiezasDeTropa.Miniatura(s.TropaId, 32) is VisualElement mini) fila.Add(mini);
                var t = Dialogo.Caja("ejercito-fila-texto");
                t.Add(Dialogo.Texto(NombreDeEscuadra(s), "aviso-titulo"));
                t.Add(Dialogo.Texto($"{s.Cantidad} hombres · Liderazgo {LiderazgoDe(s.TropaId) ?? 0}", "campo-ayuda"));
                fila.Add(t);
                var marca = new Toggle { value = !desmarcadasParaUnirse.Contains(id), name = "aporta-" + id };
                marca.RegisterValueChangedCallback(ev => { if (ev.newValue) desmarcadasParaUnirse.Remove(id); else desmarcadasParaUnirse.Add(id); });
                fila.Add(marca);
                lista.Add(fila);
            }
            cuerpo.Add(lista);
            cuerpo.Add(TituloDeSeccion("Ejércitos a la puerta", null));
            var error = Dialogo.Texto("", "error oculto");
            foreach (var e in EjercitosParaUnirse(a, yo))
            {
                var fila = Dialogo.Caja("ejercito-fila");
                fila.name = "ejercito-" + e.Id;
                var t = Dialogo.Caja("ejercito-fila-texto");
                t.Add(Dialogo.Texto($"Ejército de {Textos.NombreDeHeroe(almacen, e.LiderId)}", "aviso-titulo"));
                t.Add(Dialogo.Texto($"{e.Participantes.Count} héroe(s) · {NombreDePolitica[Politica(e.PoliticaDeUnion)].ToLowerInvariant()}", "campo-ayuda"));
                fila.Add(t);
                var ejercito = e.Id;
                Button b = null;
                b = Dialogo.Boton("Unirme con esta tropa", "boton-primario", async () =>
                {
                    var ids = TropaLibre(HeroeAhora()).Where(s => !desmarcadasParaUnirse.Contains(s.Id)).Select(s => s.Id).ToList();
                    if (ids.Count == 0) { Poner(error, "Marca al menos una escuadra que aportar."); return; }
                    var o = new OrdenUnirseAEjercito { EjercitoId = ejercito, AsentamientoId = a.Id, HeroeId = yo.Id };
                    o.EscuadronIds.AddRange(ids);
                    b.SetEnabled(false);
                    if (!await OrdenConError(new Orden { UnirseAEjercito = o }, m => Poner(error, m))) { b.SetEnabled(true); return; }
                    // Ya vas en el ejército, a la puerta: como al salir al mundo, se cierra la plaza y vuelve el mapa.
                    ventanaPlaza = null;
                    if (cerrarInterior != null) await cerrarInterior();
                    if (this != null) Mapa();
                });
                b.name = "unirse-" + e.Id;
                fila.Add(b);
                cuerpo.Add(fila);
            }
            cuerpo.Add(error);
        }
    }
}
