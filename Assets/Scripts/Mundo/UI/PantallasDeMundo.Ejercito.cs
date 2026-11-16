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
            var firma = $"{col.Version}|{h.Version}|{string.Join(",", otros)}|{string.Join(",", EscuadrasDeLaColumna().Select(s => s.Version))}|{Math.Floor(AhoraMs / 1000)}";
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
            derecha.Add(error);
            v.Add(placa);
        }

        /// <summary>Lo que lleva cada integrante: lo tuyo, tus escuadras en la columna; lo de los demás, lo que el motor deja ver (`escuadras_que_lleva`).</summary>
        List<(string tropaId, uint cantidad, uint nivel)> TropaDe(Ejercito col, string heroeId, Heroe yo)
        {
            if (heroeId == yo.Id) return EscuadrasDeLaColumna().Select(s => (s.TropaId, s.Cantidad, s.Nivel)).ToList();
            var otro = c.Sesion.Almacen.Buscar(TipoEntidad.Heroe, heroeId)?.Heroe;
            return otro?.EscuadrasQueLleva.Select(t => (IdDeTropa(t.TropaId), t.Cantidad, t.Nivel)).ToList() ?? new List<(string, uint, uint)>();
        }

        /// <summary>El id del catálogo de una tropa. `EscuadraQueLleva.tropa_id` llega del motor con el nombre («Leñadores», `vista.rs`
        /// `escuadra_que_lleva`), no con el id: se busca por nombre en `TROPAS_RECLUTABLES` si no es ya un id.</summary>
        string IdDeTropa(string idONombre)
        {
            var t = c.Datos.TropasReclutables;
            if (t == null || t.Any(x => (string)x["id"] == idONombre)) return idONombre;
            return (string)t.FirstOrDefault(x => (string)x["nombre"] == idONombre)?["id"] ?? idONombre;
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
                    cv.Integrantes.OrderByDescending(x => x.HeroeId == cv.LiderId).Select(x => (x.HeroeId, x.HeroeId == cv.LiderId ? "Líder" : null, x.Seleccion?.TropaIds.Select(IdDeTropa).ToList() ?? new List<string>())),
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
