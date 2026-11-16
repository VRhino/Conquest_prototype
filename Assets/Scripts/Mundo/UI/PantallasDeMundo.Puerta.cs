// Lo que se hace a la puerta de un pueblo, una avanzada o una obra desde la ficha del mapa (paso 9 del bloque 4, tanda 2), calcado de BronzeAgeClient
// (`ui/pueblos.ts`: `FICHA_OBRA`, `FICHA_PUEBLO`, `puertaDeObra`, `htmlEntrega`, `htmlGuarnicion`; `ui/avanzadas.ts`: `puertaDeAvanzada`,
// `htmlGuarnicionTuya`, `htmlCargaAlAlmacen`). La obra lleva su entrega en la propia ficha; el pueblo y la avanzada, un botón por ventana
// («Obra», «Guarnición», «Carga») que abre una placa encima del mapa con lo mismo que por dentro. Solo sale lo que se puede hacer desde donde está
// tu columna.
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
        /// <summary>La ventana abierta desde la ficha: «obra», «guarnicion» o «carga» de la cosa elegida (clase:id), o null.</summary>
        string ventanaDePuerta, cosaDePuerta, firmaDePuerta, firmaExtra;

        Ejercito MiColumna() => HeroeAhora() is Heroe h ? ColumnaDelHeroe(h) : null;

        /// <summary>¿Está tu columna quieta a la puerta de (x, y)?</summary>
        bool ALaPuerta(double x, double y)
        {
            if (mapa?.Columna == null || mapa.Centro == null) return false;
            var (cx, cy) = mapa.Centro.Value;
            var quieta = mapa.Columna.Estado == EstadoColumna.ColumnaEstacionada;
            return quieta && Math.Sqrt((cx - x) * (cx - x) + (cy - y) * (cy - y)) <= c.Datos.RadioPuerta;
        }

        /// <summary>La obra de mejora o reparación de una avanzada, o de subida de una aldea, sobre `destinoId`.</summary>
        ObraDeMapa ObraSobre(string destinoId) => c.Sesion.Almacen.Todas(Entidad.CuerpoOneofCase.Obra).Select(e => e.Obra)
            .FirstOrDefault(o => o.DestinoId == destinoId && (o.Tipo == TipoObra.ObraMejoraAvanzada || o.Tipo == TipoObra.ObraReparacion || o.Tipo == TipoObra.ObraMejoraAldea));

        bool ObraPropia(ObraDeMapa o) => HeroeAhora() is Heroe h && h.HasFaccionId
            && c.Sesion.Almacen.Buscar(TipoEntidad.Asentamiento, o.AsentamientoMadreId)?.Asentamiento?.FaccionId == h.FaccionId;

        List<Escuadron> EscuadrasDeLaColumna()
        {
            var col = MiColumna();
            var h = HeroeAhora();
            return col == null || h == null ? new List<Escuadron>()
                : EscuadrasDelHeroe(h).Where(s => s.Contenedor?.DondeCase == ContenedorEscuadron.DondeOneofCase.EjercitoId && s.Contenedor.EjercitoId == col.Id && s.Cantidad > 0).ToList();
        }

        /// <summary>Las ventanas que se pueden abrir desde la ficha de esa cosa: (id, texto, icono).</summary>
        List<(string id, string texto, (string familia, string id) icono)> VentanasDePuerta(CosaDelMapa cosa)
        {
            var r = new List<(string, string, (string, string))>();
            var h = HeroeAhora();
            if (h == null || mapa?.Columna == null || mapa.Columna.EnBatalla) return r;
            var propia = cosa.FaccionId != null && cosa.FaccionId == h.FaccionId;
            switch (cosa.Clase)
            {
                case ClaseDeCosa.Plaza:
                {
                    // El Mercado, con la columna a la puerta: en tu plaza, sus Órdenes al Gobernador o al Tesorero; si no, sus órdenes abiertas.
                    var a = c.Sesion.Almacen.Buscar(TipoEntidad.Asentamiento, cosa.Id)?.Asentamiento ?? cosa.Entidad?.Asentamiento;
                    if (a == null || !MercadoActivo(a) || !ALaPuerta(cosa.X, cosa.Y)) break;
                    if ((propia && GestionaOrdenes(a, h)) || OrdenesDe(a.Id).Count > 0) r.Add(("mercado", "Mercado", ("edificio", "mercado")));
                    break;
                }
                case ClaseDeCosa.Pueblo:
                case ClaseDeCosa.Aldea:
                {
                    var o = ObraSobre(cosa.Id);
                    if (o != null && ObraPropia(o) && !o.HasTerminaEnMs) r.Add(("obra", "Obra", ("glifo", "obra")));
                    if (propia && ALaPuerta(cosa.X, cosa.Y)) r.Add(("guarnicion", "Guarnición", ("glifo", "escudo")));
                    break;
                }
                case ClaseDeCosa.Mercenarios:
                    // Su taberna, con Facción y la columna quieta a su puerta (paso 12, Víctor 3a).
                    if (h.HasFaccionId && ALaPuerta(cosa.X, cosa.Y)) r.Add(("taberna", "Taberna", ("edificio", "taberna")));
                    // Y su tablón de mercenarios, si alguien se ofrece (paso 12, tanda 2): el motor lo manda con la columna parada a la puerta.
                    if (h.HasFaccionId && ALaPuerta(cosa.X, cosa.Y) && CampamentoActual(cosa.Id)?.Tablon.Count > 0) r.Add(("mercenarios", "Mercenarios", ("parte", "mercenarios")));
                    break;
                case ClaseDeCosa.Avanzada:
                {
                    var x = cosa.Entidad?.Avanzada;
                    if (x == null) break;
                    var o = ObraSobre(cosa.Id);
                    if (o != null && ObraPropia(o) && !o.HasTerminaEnMs) r.Add(("obra", "Obra", ("glifo", "obra")));
                    if (!propia || x.Estado == EstadoAvanzada.AvanzadaEnRuinas || !ALaPuerta(cosa.X, cosa.Y)) break;
                    var reside = h.HasResidenciaId && h.ResidenciaId == x.AsentamientoMadreId;
                    if (reside && (TuyasEnAvanzada(x).Count > 0 || EscuadrasDeLaColumna().Count > 0)) r.Add(("guarnicion", "Guarnición", ("glifo", "escudo")));
                    if (LoQueCabeEnAvanzada(x).Count > 0) r.Add(("carga", "Carga", ("recurso", "_otro")));
                    break;
                }
            }
            return r;
        }

        List<Escuadron> TuyasEnAvanzada(Avanzada x) => HeroeAhora() is Heroe h
            ? EscuadrasDelHeroe(h).Where(s => s.Contenedor?.DondeCase == ContenedorEscuadron.DondeOneofCase.AvanzadaId && s.Contenedor.AvanzadaId == x.Id).ToList()
            : new List<Escuadron>();

        List<Escuadron> TuyasEnPueblo(Pueblo x) => HeroeAhora() is Heroe h
            ? EscuadrasDelHeroe(h).Where(s => s.Contenedor?.DondeCase == ContenedorEscuadron.DondeOneofCase.PuebloId && s.Contenedor.PuebloId == x.Id).ToList()
            : new List<Escuadron>();

        /// <summary>Lo que lleva el carro de tu columna que cabe en el almacén de la avanzada: (recurso, lleva, cabe).</summary>
        List<(string r, double lleva, double cabe)> LoQueCabeEnAvanzada(Avanzada x)
        {
            var col = MiColumna();
            var r = new List<(string, double, double)>();
            if (col?.Suministro == null) return r;
            foreach (var kv in col.Suministro.PorId)
            {
                var lleva = kv.Value / 1000.0;
                if (lleva < 1 || !x.AlmacenPorRecurso.TryGetValue(kv.Key, out var ex) || ex.Capacidad <= 0) continue;
                var sitio = Math.Max(0, ex.Capacidad / 1000.0 - Valor(ex.Cantidad));
                r.Add((kv.Key, lleva, Math.Floor(Math.Min(lleva, sitio))));
            }
            return r.OrderBy(t => t.Item1).ToList();
        }

        /// <summary>Lo que va en la ficha además de las acciones (cada repintado de la ficha): la entrega de una obra y la ventana abierta.</summary>
        void PintarPuerta(VisualElement v, CosaDelMapa cosa)
        {
            var ficha = v.Q("ficha");
            var extra = ficha.Q("ficha-extra");
            if (extra == null)
            {
                // Las piezas de la plaza (barras, notas, botones de anillo) vienen de su hoja.
                if (Resources.Load<StyleSheet>("MundoUI/Plaza") is StyleSheet hoja) ficha.styleSheets.Add(hoja);
                extra = Dialogo.Caja("ficha-extra");
                extra.name = "ficha-extra";
                var acciones = ficha.Q("ficha-acciones");
                acciones.parent.Insert(acciones.parent.IndexOf(acciones), extra);
            }
            var o = cosa?.Clase == ClaseDeCosa.Obra ? cosa.Entidad?.Obra : null;
            var obra = o == null ? null : c.Sesion.Almacen.Buscar(TipoEntidad.ObraDeMapa, o.Id)?.Obra ?? o;
            var firma = obra == null ? "" : $"{obra.Id}:{obra.Version}:{MiColumna()?.Version}:{ALaPuerta(cosa.X, cosa.Y)}";
            if (firma != firmaExtra)
            {
                firmaExtra = firma;
                extra.Clear();
                if (obra != null)
                {
                    if (!ObraPropia(obra)) extra.Add(Dialogo.Texto("Pendiente: se ve, pero no se puede atacar.", "plaza-nota"));
                    else extra.Add(Entrega(obra, cosa));
                }
            }
            // La ventana abierta: se cierra si cambia la cosa elegida o deja de poderse abrir.
            if (ventanaDePuerta != null && (cosa == null || cosaDePuerta != cosa.Clase + ":" + cosa.Id || !VentanasDePuerta(cosa).Any(x => x.id == ventanaDePuerta))) ventanaDePuerta = null;
            var placa = v.Q("ventana-puerta");
            if (ventanaDePuerta == null) { placa?.RemoveFromHierarchy(); firmaDePuerta = null; return; }
            var firmaVentana = $"{ventanaDePuerta}|{cosaDePuerta}|{HeroeAhora()?.Version}|{MiColumna()?.Version}|{string.Join(",", EscuadrasDelHeroe(HeroeAhora()).Select(s => s.Version))}"
                + $"|{(ventanaDePuerta == "mercado" && c.Sesion.Almacen.Buscar(TipoEntidad.Asentamiento, cosa.Id)?.Asentamiento is Asentamiento am ? FirmaDeMercado(am) + am.Version : "")}"
                + $"|{(ventanaDePuerta == "taberna" ? FirmaDeIntel(HeroeAhora()) : "")}|{(ventanaDePuerta == "mercenarios" ? CampamentoActual(cosa.Id)?.Version : 0)}|{eleccionMerc.Count}|{c.Sesion.Almacen.Buscar(TipoEntidad.Avanzada, cosa.Id)?.Avanzada?.Version}|{c.Sesion.Almacen.Buscar(TipoEntidad.Pueblo, cosa.Id)?.Pueblo?.Version}|{ObraSobre(cosa.Id)?.Version}";
            if (placa != null && firmaVentana == firmaDePuerta) return;
            firmaDePuerta = firmaVentana;
            placa?.RemoveFromHierarchy();
            placa = Dialogo.Caja("placa ventana-puerta");
            placa.name = "ventana-puerta";
            placa.EnableInClassList("ancha", ventanaDePuerta == "mercado" || ventanaDePuerta == "taberna" || ventanaDePuerta == "mercenarios");
            if (Resources.Load<StyleSheet>("MundoUI/Plaza") is StyleSheet hojaPlaza) placa.styleSheets.Add(hojaPlaza);
            var filete = Dialogo.Caja("placa-filete ventana-puerta-filete");
            var cab = Dialogo.Caja("ventana-puerta-cab");
            var titulo = ventanaDePuerta == "obra" ? NombreDeObra(ObraSobre(cosa.Id)) : ventanaDePuerta == "carga" ? "Al almacén" : ventanaDePuerta == "mercado" ? "Mercado" : ventanaDePuerta == "taberna" ? "Taberna" : ventanaDePuerta == "mercenarios" ? "Mercenarios"
                : cosa.Clase == ClaseDeCosa.Avanzada ? "Tu guarnición" : "Guarnición";
            cab.Add(Dialogo.Texto(titulo, "dialogo-titulo"));
            var cerrar = Dialogo.Boton("×", "aviso-borrar", () => { ventanaDePuerta = null; PintarPuerta(v, mapa?.Seleccionada); });
            cerrar.name = "ventana-puerta-cerrar";
            cab.Add(cerrar);
            filete.Add(cab);
            var cuerpo = new ScrollView(ScrollViewMode.Vertical) { horizontalScrollerVisibility = ScrollerVisibility.Hidden };
            cuerpo.AddToClassList("ventana-puerta-cuerpo");
            switch (ventanaDePuerta)
            {
                case "obra": cuerpo.Add(Entrega(ObraSobre(cosa.Id), cosa)); break;
                case "guarnicion":
                    if (cosa.Clase == ClaseDeCosa.Avanzada) GuarnicionDeAvanzada(cuerpo, cosa.Entidad.Avanzada);
                    else GuarnicionDePueblo(cuerpo, c.Sesion.Almacen.Buscar(TipoEntidad.Pueblo, cosa.Id)?.Pueblo ?? cosa.Entidad.Pueblo);
                    break;
                case "mercado":
                {
                    var a = c.Sesion.Almacen.Buscar(TipoEntidad.Asentamiento, cosa.Id)?.Asentamiento ?? cosa.Entidad.Asentamiento;
                    void Otra() { firmaDePuerta = null; PintarPuerta(v, mapa?.Seleccionada); }
                    if (cosa.FaccionId != null && cosa.FaccionId == HeroeAhora()?.FaccionId && GestionaOrdenes(a, HeroeAhora())) OrdenesDeMercado(cuerpo, a, HeroeAhora(), Otra);
                    else MercadoDePlaza(cuerpo, a.Id, Otra);
                    break;
                }
                case "taberna":
                {
                    var camp = c.Sesion.Almacen.Buscar(TipoEntidad.CampamentoMercenarios, cosa.Id)?.CampamentoMercenarios ?? cosa.Entidad?.CampamentoMercenarios;
                    if (camp != null) PanelDeIntel(cuerpo, TabernaDeCampamento(camp, HeroeAhora()), HeroeAhora(), null, () => { firmaDePuerta = null; PintarPuerta(v, mapa?.Seleccionada); });
                    break;
                }
                case "mercenarios":
                {
                    var camp = CampamentoActual(cosa.Id) ?? cosa.Entidad?.CampamentoMercenarios;
                    var error = Dialogo.Texto("", "error oculto");
                    error.name = "merc-error";
                    if (camp != null) TablonDeMercenarios(cuerpo, camp.Tablon, HeroeAhora(), FuentesDeContrato(HeroeAhora(), camp), null, t => Poner(error, t), () => { firmaDePuerta = null; PintarPuerta(v, mapa?.Seleccionada); });
                    cuerpo.Add(error);
                    break;
                }
                case "carga": CargaAlAlmacen(cuerpo, c.Sesion.Almacen.Buscar(TipoEntidad.Avanzada, cosa.Id)?.Avanzada ?? cosa.Entidad.Avanzada); break;
            }
            filete.Add(cuerpo);
            placa.Add(filete);
            v.Add(placa);
        }

        /// <summary>Un botón de la ficha que abre (o cierra) una ventana de la puerta.</summary>
        Button BotonDeVentana(VisualElement v, CosaDelMapa cosa, (string id, string texto, (string familia, string id) icono) x)
        {
            var b = IconosDelMundo.BotonConGlifo(x.texto, x.icono, "boton-secundario", () =>
            {
                var clave = cosa.Clase + ":" + cosa.Id;
                ventanaDePuerta = ventanaDePuerta == x.id && cosaDePuerta == clave ? null : x.id;
                cosaDePuerta = clave;
                firmaDePuerta = null;
                PintarPuerta(v, mapa?.Seleccionada);
            });
            b.name = "puerta-" + x.id;
            return b;
        }

        /// <summary>La entrega de una obra (`htmlEntrega`): lo entregado de cada material con su barra, lo que falta y lo que lleva tu columna, y
        /// «Entregar materiales» (se entrega lo que falta de lo que lleve: primero el carro, luego las caravanas enganchadas).</summary>
        VisualElement Entrega(ObraDeMapa o, CosaDelMapa cosa)
        {
            var caja = Dialogo.Caja("obra-entrega");
            caja.name = "entrega-" + o?.Id;
            if (o == null) return caja;
            if (o.HasTerminaEnMs)
            {
                var total = Dialogo.Caja("obra-entrega-total");
                total.Add(IconosDelMundo.ConIcono("glifo", "obra", Dialogo.Texto("Levantándose", "obra-entrega-titulo"), 14));
                total.Add(IconosDelMundo.ConIcono("glifo", "espera", Dialogo.Texto(CuentaAtras(o.TerminaEnMs), "obra-entrega-pct"), 14));
                caja.Add(total);
                caja.Add(BarraAvance(null));
                return caja;
            }
            var col = MiColumna();
            double pedido = o.Requerido.PorId.Values.Sum() / 1000.0;
            double hecho = o.Requerido.PorId.Sum(kv => Math.Min(kv.Value, o.Entregado != null && o.Entregado.PorId.TryGetValue(kv.Key, out var e) ? e : 0)) / 1000.0;
            var parte = pedido > 0 ? hecho / pedido : 1;
            var cab = Dialogo.Caja("obra-entrega-total");
            cab.Add(Dialogo.Texto(parte >= 1 ? "✓ Todo entregado: se levanta" : "Materiales entregados", "obra-entrega-titulo"));
            cab.Add(Dialogo.Texto($"{Math.Floor(parte * 100)} %", "obra-entrega-pct"));
            caja.Add(cab);
            caja.Add(BarraAvance((float)parte));
            foreach (var kv in o.Requerido.PorId.OrderBy(kv => kv.Key))
            {
                var n = kv.Value / 1000.0;
                var hay = Math.Min(n, (o.Entregado != null && o.Entregado.PorId.TryGetValue(kv.Key, out var e) ? e : 0) / 1000.0);
                var queda = Math.Max(0, n - hay);
                var fila = Dialogo.Caja("obra-entrega-fila" + (queda <= 0 ? " lista" : ""));
                fila.name = "entrega-" + kv.Key;
                fila.Add(IconosDelMundo.Icono("recurso", kv.Key, 18));
                fila.Add(Dialogo.Texto(CosasDelMapa.NombreDeRecurso(kv.Key), "obra-entrega-nombre"));
                fila.Add(BarraAvance(n > 0 ? (float)(hay / n) : 1));
                fila.Add(Dialogo.Texto($"{Math.Floor(hay)}/{Math.Floor(n)}", "obra-entrega-cifra"));
                fila.Add(Dialogo.Texto(queda <= 0 ? "✓" : $"faltan {Math.Floor(queda)}", "obra-entrega-falta"));
                if (queda > 0 && col != null)
                {
                    var lleva = Math.Floor((col.Suministro != null && col.Suministro.PorId.TryGetValue(kv.Key, out var l) ? l : 0) / 1000.0);
                    var tuyo = IconosDelMundo.ConIcono("simbolo", "carro", Dialogo.Texto(lleva.ToString(), "obra-entrega-lleva-n"), 12);
                    tuyo.AddToClassList("obra-entrega-lleva");
                    tuyo.AddToClassList(lleva >= queda ? "alcanza" : lleva > 0 ? "parte" : "nada");
                    tuyo.tooltip = $"Lo que lleva tu columna de {CosasDelMapa.NombreDeRecurso(kv.Key)}";
                    fila.Add(tuyo);
                }
                caja.Add(fila);
            }
            if (!ObraPropia(o)) return caja;
            var d = col == null || mapa?.Centro == null ? (double?)null : CosasDelMapa.Distancia(mapa.Centro.Value.x, mapa.Centro.Value.y, o.Posicion.X / 1000.0, o.Posicion.Y / 1000.0);
            var puerta = c.Datos.RadioPuerta;
            var motivo = col == null ? "Hace falta una columna tuya con los materiales en su carro o en sus caravanas."
                : d > puerta ? $"Acerca tu columna a la obra: está a {Math.Floor(d.Value)} y se entrega a {puerta} o menos (un clic en la obra la lleva)." : "";
            var acciones = Dialogo.Caja("obra-entrega-acciones");
            Button b = null;
            b = Dialogo.Boton("Entregar materiales", "boton-primario", async () =>
            {
                b.SetEnabled(false);
                if (await OrdenDePlaza(new Orden { EntregarEnObra = new OrdenEntregarEnObra { HeroeId = c.HeroeId, ObraId = o.Id, Materiales = new Cantidades() } })) { firmaExtra = null; firmaDePuerta = null; }
                b.SetEnabled(true);
            });
            b.name = "entregar-" + o.Id;
            b.SetEnabled(motivo == "");
            if (motivo != "") b.tooltip = motivo;
            acciones.Add(b);
            caja.Add(acciones);
            caja.Add(Dialogo.Texto(motivo != "" ? motivo : "Se entrega lo que falta de lo que lleve tu columna: primero el carro, luego las caravanas enganchadas.", "plaza-nota"));
            return caja;
        }

        /// <summary>Una escuadra en fila con su miniatura, su nombre, su Liderazgo y sus hombres, y un botón.</summary>
        VisualElement FilaDeEscuadra(Escuadron s, Button boton)
        {
            var fila = Dialogo.Caja("puerta-escuadra");
            fila.name = "escuadra-" + s.Id;
            if (PiezasDeTropa.Miniatura(s.TropaId, 30) is VisualElement mini) fila.Add(mini);
            var textos = Dialogo.Caja("puerta-escuadra-texto");
            textos.Add(Dialogo.Texto(string.IsNullOrEmpty(s.Nombre) ? NombreDeTropa(s.TropaId) : s.Nombre, "aviso-titulo"));
            textos.Add(Dialogo.Texto($"{s.Cantidad} hombres · {LiderazgoDe(s.TropaId)} de Liderazgo", "campo-ayuda"));
            fila.Add(textos);
            if (boton != null) fila.Add(boton);
            return fila;
        }

        /// <summary>El cupo en barra: «tu cupo usado / cupo».</summary>
        VisualElement CupoEnBarra(string texto, int usado, int cupo)
        {
            var caja = Dialogo.Caja("puerta-cupo");
            var cab = Dialogo.Caja("puerta-cupo-cab");
            cab.Add(Dialogo.Texto(texto, "plaza-sub"));
            cab.Add(Dialogo.Texto($"{usado} / {cupo}", "puerta-cupo-n"));
            caja.Add(cab);
            caja.Add(BarraAvance(cupo > 0 ? Mathf.Clamp01((float)usado / cupo) : 0));
            return caja;
        }

        Button BotonDeOrden(string texto, string nombre, Func<Orden> orden, bool activo = true, string titulo = null)
        {
            Button b = null;
            b = Dialogo.Boton(texto, "boton-secundario", async () =>
            {
                if (await OrdenDePlaza(orden(), b)) { firmaDePuerta = null; firmasDeSitio.Clear(); }
            });
            b.name = nombre;
            b.SetEnabled(activo);
            if (titulo != null) b.tooltip = titulo;
            return b;
        }

        /// <summary>La guarnición de un pueblo a su puerta (`htmlGuarnicion`): los de serie, tu cupo, las tuyas con «Retirar» y las de tu columna con «Dejar».</summary>
        void GuarnicionDePueblo(VisualElement cuerpo, Pueblo x)
        {
            if (x == null) return;
            var mias = TuyasEnPueblo(x);
            var usado = mias.Sum(s => LiderazgoDe(s.TropaId) ?? 0);
            var cupo = (int)x.CupoGuarnicionPorHeroe;
            var libre = cupo - usado;
            cuerpo.Add(CupoEnBarra("Guarnición · tu cupo", usado, cupo));
            var deOtros = Math.Max(0, x.GuarnicionJugadoresIds.Count - mias.Count);
            var repone = x.HasGuarnicionReponeEnMs && x.GuarnicionReponeEnMs > AhoraMs ? $" (mermada: el siguiente vuelve en {CuentaAtras(x.GuarnicionReponeEnMs)})" : "";
            cuerpo.Add(Dialogo.Texto($"{(EsAldea(x) ? "Granjeros" : "Mineros")} de serie{repone}{(deOtros > 0 ? $" y {deOtros} escuadra(s) de otros héroes" : "")}.", "plaza-nota"));
            foreach (var s in mias)
            {
                var id = s.Id;
                cuerpo.Add(FilaDeEscuadra(s, BotonDeOrden("Retirar", "retirar-" + id, () => new Orden { RetirarDePueblo = new OrdenRetirarDePueblo { PuebloId = x.Id, EscuadronIds = { id } } })));
            }
            cuerpo.Add(Dialogo.Texto("DE TU COLUMNA", "plaza-sub puerta-sub"));
            var columna = EscuadrasDeLaColumna();
            if (columna.Count == 0) cuerpo.Add(Dialogo.Texto("Tu columna no trae escuadras que dejar.", "plaza-nota"));
            foreach (var s in columna)
            {
                var id = s.Id;
                var cabe = (LiderazgoDe(s.TropaId) ?? 0) <= libre;
                cuerpo.Add(FilaDeEscuadra(s, BotonDeOrden("Dejar", "dejar-" + id, () => new Orden { GuarnecerPueblo = new OrdenGuarnecerPueblo { PuebloId = x.Id, EscuadronIds = { id } } },
                    cabe, cabe ? null : $"No cabe: te quedan {Math.Max(0, libre)} de Liderazgo")));
            }
        }

        /// <summary>Tu guarnición en una avanzada a su puerta (`htmlGuarnicionTuya`): tu cupo de su etapa, las tuyas con «Retirar» y las de tu columna con «Dejar».</summary>
        void GuarnicionDeAvanzada(VisualElement cuerpo, Avanzada x)
        {
            if (x == null) return;
            var clave = ClaveDeEtapa((int)x.Etapa);
            var cupo = (int?)c.Datos.Bloque("Combate", "AVANZADA")?["cupoGuarnicion"]?[clave] ?? 0;
            var tuyas = TuyasEnAvanzada(x);
            var usado = tuyas.Sum(s => LiderazgoDe(s.TropaId) ?? 0);
            var libre = cupo - usado;
            cuerpo.Add(CupoEnBarra("Tu guarnición", usado, cupo));
            foreach (var s in tuyas)
            {
                var id = s.Id;
                cuerpo.Add(FilaDeEscuadra(s, BotonDeOrden("Retirar", "retirar-" + id, () => new Orden { RetirarDeAvanzada = new OrdenRetirarDeAvanzada { HeroeId = c.HeroeId, AvanzadaId = x.Id, EscuadronIds = { id } } })));
            }
            var columna = EscuadrasDeLaColumna();
            if (columna.Count > 0) cuerpo.Add(Dialogo.Texto("DE TU COLUMNA", "plaza-sub puerta-sub"));
            foreach (var s in columna)
            {
                var id = s.Id;
                var cabe = (LiderazgoDe(s.TropaId) ?? 0) <= libre;
                cuerpo.Add(FilaDeEscuadra(s, BotonDeOrden("Dejar", "dejar-" + id, () => new Orden { GuarnecerAvanzada = new OrdenGuarnecerAvanzada { HeroeId = c.HeroeId, AvanzadaId = x.Id, EscuadronIds = { id } } },
                    cabe, cabe ? null : $"No cabe: te quedan {Math.Max(0, libre)} de Liderazgo")));
            }
            cuerpo.Add(Dialogo.Texto("Defienden la avanzada aunque no estés y se quedan al salir; si cae en ruinas, vuelven a tu campamento.", "plaza-nota"));
        }

        /// <summary>Dejar lo del carro de tu columna en el almacén de la avanzada (`htmlCargaAlAlmacen`): una fila por recurso con la cantidad.</summary>
        void CargaAlAlmacen(VisualElement cuerpo, Avanzada x)
        {
            var col = MiColumna();
            if (x == null || col == null) return;
            cuerpo.Add(Dialogo.Texto("El fuerte y la ciudadela guardan recursos para la Facción: se deja lo que lleva el carro de tu columna.", "plaza-nota"));
            foreach (var (r, lleva, cabe) in LoQueCabeEnAvanzada(x))
            {
                var fila = Dialogo.Caja("puerta-carga");
                fila.name = "carga-" + r;
                fila.Add(IconosDelMundo.ConIcono("recurso", r, Dialogo.Texto(CosasDelMapa.NombreDeRecurso(r), "puerta-carga-nombre"), 18));
                fila.Add(Dialogo.Texto(Math.Floor(lleva).ToString(), "puerta-carga-lleva"));
                var cantidad = new IntegerField { name = "cantidad-" + r, value = (int)cabe };
                cantidad.AddToClassList("puerta-carga-cantidad");
                cantidad.SetEnabled(cabe > 0);
                fila.Add(cantidad);
                var recurso = r;
                fila.Add(BotonDeOrden("Dejar", "depositar-" + r, () => new Orden { DepositarEnAvanzada = new OrdenDepositarEnAvanzada { EjercitoId = col.Id, AvanzadaId = x.Id, Recurso = recurso, Cantidad = Math.Max(1, Math.Min((long)cabe, cantidad.value)) * 1000L } },
                    cabe > 0, cabe > 0 ? null : "El almacén no tiene sitio para más."));
                cuerpo.Add(fila);
            }
        }

        static string ClaveDeEtapa(int etapa) => etapa == 2 ? "fuerte" : etapa == 3 ? "ciudadela" : "atalaya";
        static string NombreDeEtapa(int etapa) => etapa == 2 ? "Fuerte" : etapa == 3 ? "Ciudadela" : "Atalaya";
    }
}
