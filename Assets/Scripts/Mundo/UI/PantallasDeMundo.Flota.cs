// La pestaña Caravanas del Mercado (paso 10, tanda 2), calcada de la vista de flota de `ui/panelMercado.ts` (`htmlCaravanas`, `filaFlota`,
// `detalleFlota`, `carrosEnHuecos`, `seccionViaje`, `cuerpoAparcada`):
// - arriba, el Mercado y el Albergue con los cupos de la flota (caravanas y carros) y «＋ Crear»;
// - a la izquierda, la lista: primero las que están en la plaza, luego las aparcadas aquí y al final las que andan fuera;
// - a la derecha, la elegida: el nombre (✎ para ponérselo), «Reservada», lo que puede llevar, su velocidad y lo que tarda en prepararse, lo que
//   lleva (con «Descargar»), los carros en huecos (+ para añadir uno con su animal; uno elegido: comprarle el animal o pasarlo a otra caravana),
//   la escolta (la del carro de acarreo, con el cupo del Mercado) y el viaje (destino, carga y «Lanzar»), o «Cancelar preparación».
// Las aparcadas de otra plaza: su carga con «Cargar» y «Descargar», y «↩ Enviar al origen». La flota la maneja quien reside en la plaza; lo que no
// se puede hacer no sale. El Encargo (rutas y envíos) llega en la tanda 3.
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
        string caravanaElegida;
        string carroElegido;                       // «caravana:índice»
        readonly HashSet<string> carroNuevoAbierto = new HashSet<string>();
        readonly Dictionary<string, (string tipo, string animal)> carroNuevo = new Dictionary<string, (string, string)>();
        string renombrando;

        static readonly Dictionary<string, string> NombreDeCarro = new Dictionary<string, string> { ["basico"] = "Carro básico", ["reforzado"] = "Carro reforzado" };
        static readonly Dictionary<string, string> NombreDeAnimal = new Dictionary<string, string> { ["buey"] = "Buey", ["caballo"] = "Caballo", ["camello"] = "Camello" };

        JObject CatalogoDeCarros => c.Datos.Bloque("Comercio_Caravanas", "CARRO_CATALOGO") as JObject;
        JObject CatalogoDeAnimales => c.Datos.Bloque("Comercio_Caravanas", "ANIMAL_CATALOGO") as JObject;

        static int NivelActivo(Asentamiento a, string tipo) => Internos(a).Where(e => e.Tipo == tipo && Activo(e)).Select(NivelInterno).DefaultIfEmpty(0).Max();

        /// <summary>El producto de un factor de las políticas activas de la plaza (`factorCapacidadCaravana`, `factorVelocidadCaravana`), o su suma
        /// (`cupoCaravanaExtra`).</summary>
        double DePoliticas(Asentamiento a, string campo, bool sumar = false)
        {
            var catalogo = (DatoDeJuego("Economia_Construccion", "POLITICA_CATALOGO") as JArray)?.OfType<JObject>().ToList();
            var r = sumar ? 0.0 : 1.0;
            foreach (var p in a.PoliticasActivas)
                if (catalogo?.FirstOrDefault(x => (string)x["id"] == p.PoliticaId)?[campo] is JToken v && v.Type is JTokenType.Float or JTokenType.Integer)
                    r = sumar ? r + (double)v : r * (double)v;
            return r;
        }

        /// <summary>Los topes de la flota: los del nivel del Mercado, más los del Albergue y la «Ampliación de flota». Null sin Mercado.</summary>
        (int caravanas, int carros)? CuposDeFlota(Asentamiento a)
        {
            var mercado = NivelActivo(a, "mercado");
            if (mercado == 0 || NivelDe("mercado", mercado) is not JObject n) return null;
            var albergue = NivelActivo(a, "albergueCaravanas");
            var al = albergue > 0 ? NivelDe("albergueCaravanas", albergue) : null;
            var caravanas = ((int?)n["cupoCaravanas"] ?? 0) + ((int?)al?["cupoCaravanas"] ?? 0) + (int)DePoliticas(a, "cupoCaravanaExtra", true);
            var carros = ((int?)n["cupoCarros"] ?? 0) + ((int?)al?["cupoCarros"] ?? 0);
            return (caravanas, carros);
        }

        /// <summary>El cupo de escolta de una caravana comercial: el del nivel del Mercado; sin Mercado, 0 (motor `cupos_de`).</summary>
        int CupoDeEscoltaComercial(Asentamiento a)
        {
            var tabla = c.Datos.Bloque("Comercio_Caravanas", "CARAVANA_ESCOLTA")?["liderazgoPorNivelMercado"] as JArray;
            var mercado = NivelActivo(a, "mercado");
            return tabla == null || tabla.Count == 0 || mercado == 0 ? 0 : (int)tabla[Math.Min(tabla.Count, mercado) - 1];
        }

        /// <summary>Lo que puede llevar: solo cuentan los carros con animal; las políticas de la plaza lo multiplican.</summary>
        int CapacidadDe(Caravana k, Asentamiento a) => (int)Math.Floor(k.Carros.Sum(r =>
            r.HasAnimal && CatalogoDeCarros?[r.TipoCarro] is JObject def && CatalogoDeAnimales?[r.Animal] is JObject an
                ? (double)def["capacidadBase"] * (double)an["factorCarga"] : 0) * DePoliticas(a, "factorCapacidadCaravana"));

        static int CargaDe(Caravana k) => (int)Math.Floor(k.Contenido?.PorId.Values.Sum(v => v / 1000.0) ?? 0);

        static string NombreDeCaravana(Caravana k) => k.HasNombre && !string.IsNullOrEmpty(k.Nombre) ? k.Nombre : "Caravana " + k.Id;

        static bool MismoSitio(Punto x, Punto y) => x != null && y != null && Math.Abs(x.X - y.X) < 500 && Math.Abs(x.Y - y.Y) < 500;

        /// <summary>Dónde está (o adónde va) y si está dentro de esta plaza (`ubicacionDe`).</summary>
        (string texto, bool enPlaza) UbicacionDe(Caravana k, Asentamiento a)
        {
            var quieta = k.Estado is EstadoCaravana.CaravanaDisponible or EstadoCaravana.CaravanaPreparando or EstadoCaravana.CaravanaEsperando or EstadoCaravana.CaravanaAparcada;
            if (quieta && MismoSitio(k.Posicion, a.Posicion))
                return (k.Estado == EstadoCaravana.CaravanaPreparando ? $"En la plaza · sale en {(k.HasPreparaHastaMs ? FaltaPara(k.PreparaHastaMs) : "un momento")}" : "En la plaza", true);
            var destino = k.Encargo != null && k.Encargo.ParadaActual == "origen" ? k.OrigenAsentamientoId : k.Encargo?.DestinoAsentamientoId ?? (k.HasDestinoAsentamientoId ? k.DestinoAsentamientoId : null);
            return k.Estado switch
            {
                EstadoCaravana.CaravanaEnTransito => (destino != null ? $"Hacia {NombreDePlaza(destino)}" : "De camino", false),
                EstadoCaravana.CaravanaEsperando when destino != null => ($"Hacia {NombreDePlaza(destino)}", false),
                EstadoCaravana.CaravanaRetornando => ($"Volviendo a {NombreDePlaza(k.OrigenAsentamientoId)}", false),
                EstadoCaravana.CaravanaAdjunta => ("Con un ejército", false),
                _ => ("Fuera de la plaza", false),
            };
        }

        static readonly Dictionary<EstadoCaravana, string> IconoDeEstado = new Dictionary<EstadoCaravana, string>
        {
            [EstadoCaravana.CaravanaDisponible] = "●", [EstadoCaravana.CaravanaPreparando] = "⏳", [EstadoCaravana.CaravanaEnTransito] = "➜",
            [EstadoCaravana.CaravanaRetornando] = "↩", [EstadoCaravana.CaravanaEsperando] = "⏸", [EstadoCaravana.CaravanaAparcada] = "⚓", [EstadoCaravana.CaravanaAdjunta] = "⛓",
        };

        List<Caravana> FlotaDe(Asentamiento a) => c.Sesion.Almacen.Todas(Entidad.CuerpoOneofCase.Caravana).Select(e => e.Caravana)
            .Where(k => k.Tipo == TipoCaravana.CaravanaComercial && k.OrigenAsentamientoId == a.Id).ToList();

        List<Caravana> AparcadasAqui(Asentamiento a) => c.Sesion.Almacen.Todas(Entidad.CuerpoOneofCase.Caravana).Select(e => e.Caravana)
            .Where(k => k.Estado == EstadoCaravana.CaravanaAparcada && k.OrigenAsentamientoId != a.Id && MismoSitio(k.Posicion, a.Posicion)).ToList();

        /// <summary>Lo que cambia en la pestaña: la flota, las aparcadas y las escuadras del héroe (la escolta).</summary>
        string FirmaDeFlota(Asentamiento a) => string.Join(",", FlotaDe(a).Concat(AparcadasAqui(a)).Select(k => k.Id + ":" + k.Version))
            + $"|{caravanaElegida}|{carroElegido}|{string.Join(",", carroNuevoAbierto)}|{renombrando}|{string.Join(",", EscuadrasDelHeroe(HeroeAhora()).Select(s => s.Version))}";

        /// <summary>La pestaña Caravanas (`htmlCaravanas`).</summary>
        void Caravanas(VisualElement cuerpo, Asentamiento a, Heroe yo, Action rehacer)
        {
            var reside = Reside(a, yo);
            var flota = FlotaDe(a);
            var aparcadas = AparcadasAqui(a);
            var cupos = CuposDeFlota(a);
            var carros = flota.Sum(k => k.Carros.Count);
            var mercado = NivelActivo(a, "mercado");
            var albergue = NivelActivo(a, "albergueCaravanas");
            cuerpo.Add(TituloDeSeccion("Caravanas comerciales",
                "Tu flota: una caravana se arma con carros (que pagas con materiales del almacén) y un animal por carro, y sale a mano hacia otra plaza con la carga que elijas. Solo cuentan los carros con animal. Los carros se reparten como quieras entre las caravanas; mover un carro no gasta cupo. La carga sale del almacén al prepararla; si la cancelas antes de salir, vuelve entera. Escolta: cede escuadras a una caravana parada en su origen; viajan con ella y vuelven al acabar. Aparcadas: caravanas de tu Facción que un ejército dejó aquí."));
            var cab = Dialogo.Caja("flota-cab");
            cab.Add(IconosDelMundo.Icono("edificio", "mercado", 32, IconosDelMundo.Bronce));
            var texto = Dialogo.Caja("flota-cuerpo");
            texto.Add(Dialogo.Texto(mercado > 0 ? $"Mercado nivel {mercado}{(albergue > 0 ? $" · Albergue nivel {albergue}" : "")}" : "Sin Mercado activo", "flota-titulo"));
            var topes = Dialogo.Caja("flota-cupos");
            if (cupos is (int topeCaravanas, int topeCarros))
            {
                topes.Add(Pips("Caravanas", flota.Count, topeCaravanas, "flota-cupo-caravanas"));
                topes.Add(Pips("Carros", carros, topeCarros, "flota-cupo-carros"));
                topes.Add(Chip($"♛ {CupoDeEscoltaComercial(a)}"));
            }
            texto.Add(topes);
            cab.Add(texto);
            // «＋ Crear»: con el Mercado, sin la flota llena y pasada la espera entre caravanas (compartida con la de fundación).
            var espera = (double?)c.Datos.Bloque("Comercio_Caravanas", "CARAVANA_COOLDOWN")?["cooldownMinutos"] ?? 10;
            var listaEn = a.HasUltimaCaravanaCreadaEnMs ? a.UltimaCaravanaCreadaEnMs + (long)(espera * 60_000) : 0;
            if (reside && cupos is (int tope, _) && flota.Count < tope)
            {
                if (listaEn > AhoraMs) cab.Add(Dialogo.Texto($"Otra, en {FaltaPara(listaEn)}", "flota-espera"));
                else
                {
                    Button crear = null;
                    crear = Dialogo.Boton("＋ Crear", "flota-crear", async () => { if (await OrdenDePlaza(new Orden { CrearCaravana = new OrdenCrearCaravana { AsentamientoId = a.Id } }, crear)) rehacer(); });
                    crear.name = "caravana-crear";
                    crear.tooltip = "Crear una caravana nueva (un casco vacío)";
                    cab.Add(crear);
                }
            }
            cuerpo.Add(cab);
            int Rango(Caravana k) => k.OrigenAsentamientoId != a.Id ? 1 : UbicacionDe(k, a).enPlaza ? 0 : 2;
            var todas = flota.Concat(aparcadas).Select((k, i) => (k, i)).OrderBy(x => Rango(x.k)).ThenBy(x => x.i).Select(x => x.k).ToList();
            if (todas.Count == 0) { cuerpo.Add(Dialogo.Texto("No hay caravanas en esta plaza.", "plaza-vacio")); return; }
            var elegida = todas.FirstOrDefault(k => k.Id == caravanaElegida) ?? todas[0];
            var marco = Dialogo.Caja("flota-marco");
            var lista = Dialogo.Caja("flota-lista");
            foreach (var k in todas) lista.Add(FilaDeFlota(k, a, k.Id == elegida.Id, rehacer));
            marco.Add(lista);
            marco.Add(DetalleDeFlota(elegida, a, yo, reside, flota, cupos is (_, int tc) && carros >= tc, rehacer));
            cuerpo.Add(marco);
        }

        VisualElement Pips(string nombre, int usadas, int tope, string clase)
        {
            var caja = Dialogo.Caja("flota-cupo " + clase);
            caja.Add(Dialogo.Texto(nombre, "flota-cupo-nombre"));
            var pips = Dialogo.Caja("flota-pips");
            for (int i = 0; i < Math.Max(tope, usadas); i++) pips.Add(Dialogo.Caja(i < usadas ? "flota-pip usada" : "flota-pip"));
            caja.Add(pips);
            caja.Add(Dialogo.Texto($"{usadas}/{tope}", "flota-cupo-n"));
            return caja;
        }

        /// <summary>Una fila de la lista (`filaFlota`): nombre, dónde está, los carros que tiran y lo que lleva.</summary>
        VisualElement FilaDeFlota(Caravana k, Asentamiento a, bool elegida, Action rehacer)
        {
            var ajena = k.Estado == EstadoCaravana.CaravanaAparcada && k.OrigenAsentamientoId != a.Id;
            var id = k.Id;
            var fila = new Button(() => { caravanaElegida = id; carroElegido = null; rehacer(); }) { name = "flota-" + k.Id };
            fila.AddToClassList("flota-fila");
            fila.EnableInClassList("activo", elegida);
            var cab = Dialogo.Caja("flota-fila-cab");
            cab.Add(IconosDelMundo.Icono("simbolo", "carro", 18, IconosDelMundo.Bronce));
            cab.Add(Dialogo.Texto(NombreDeCaravana(k), "flota-fila-nombre"));
            if (k.ReservadaManual) cab.Add(Dialogo.Texto("🔒", "flota-reservada"));
            fila.Add(cab);
            fila.Add(Dialogo.Texto($"{(IconoDeEstado.TryGetValue(k.Estado, out var ic) ? ic + " " : "")}{(ajena ? $"Aparcada aquí · de {NombreDePlaza(k.OrigenAsentamientoId)}" : UbicacionDe(k, a).texto)}", "flota-fila-donde"));
            var cap = CapacidadDe(k, a);
            var carga = CargaDe(k);
            var datos = Dialogo.Caja("flota-fila-datos");
            datos.Add(Dialogo.Texto($"🛞 {k.Carros.Count(r => r.HasAnimal)}/{k.Carros.Count}", "flota-fila-carros"));
            datos.Add(BarraAvance(cap > 0 ? Math.Min(1f, (float)carga / cap) : 0));
            datos.Add(Dialogo.Texto($"📦 {carga}/{cap}", "flota-fila-carga"));
            fila.Add(datos);
            return fila;
        }

        /// <summary>La caravana elegida, a la derecha (`detalleFlota`).</summary>
        VisualElement DetalleDeFlota(Caravana k, Asentamiento a, Heroe yo, bool reside, List<Caravana> flota, bool carrosLleno, Action rehacer)
        {
            var caja = Dialogo.Caja("flota-detalle");
            caja.name = "flota-detalle-" + k.Id;
            var ajena = k.Estado == EstadoCaravana.CaravanaAparcada && k.OrigenAsentamientoId != a.Id;
            var (donde, enPlaza) = UbicacionDe(k, a);
            var parada = k.Estado == EstadoCaravana.CaravanaDisponible;
            // La cabecera: el nombre (✎ para ponérselo, si resides y está en la plaza), dónde está y «Reservada».
            var cab = Dialogo.Caja("caravana-cab");
            cab.Add(IconosDelMundo.Icono("simbolo", "carro", 26, IconosDelMundo.Bronce));
            var titulo = Dialogo.Caja("caravana-titulo");
            var renombrable = reside && !ajena && enPlaza;
            if (renombrable && renombrando == k.Id)
            {
                var fila = Dialogo.Caja("caravana-renombrar");
                var campo = new TextField { name = "caravana-nombre", value = k.HasNombre ? k.Nombre : "" };
                campo.AddToClassList("caravana-nombre-campo");
                fila.Add(campo);
                Button guardar = null;
                guardar = Dialogo.Boton("✓", "caravana-ok", async () =>
                {
                    if (await OrdenDePlaza(new Orden { RenombrarCaravana = new OrdenRenombrarCaravana { CaravanaId = k.Id, Nombre = campo.value.Trim() } }, guardar)) { renombrando = null; rehacer(); }
                });
                guardar.name = "caravana-guardar";
                fila.Add(guardar);
                fila.Add(Dialogo.Boton("✕", "caravana-cancelar", () => { renombrando = null; rehacer(); }));
                titulo.Add(fila);
            }
            else
            {
                var nombre = Dialogo.Caja("caravana-nombre-fila");
                nombre.Add(Dialogo.Texto(NombreDeCaravana(k), "caravana-nombre"));
                if (renombrable)
                {
                    var lapiz = Dialogo.Boton("✎", "caravana-lapiz", () => { renombrando = k.Id; rehacer(); });
                    lapiz.name = "caravana-renombrar";
                    lapiz.tooltip = "Ponerle nombre";
                    nombre.Add(lapiz);
                }
                titulo.Add(nombre);
            }
            titulo.Add(Dialogo.Texto(ajena ? $"aparcada aquí · de {NombreDePlaza(k.OrigenAsentamientoId)}" : donde, "plaza-sub"));
            cab.Add(titulo);
            if (enPlaza && reside && !ajena && parada)
            {
                var reservada = new Toggle("Reservada") { name = "caravana-reservada", value = k.ReservadaManual, tooltip = "Reservada: la plaza no la usa en sus envíos automáticos" };
                reservada.AddToClassList("caravana-reservar");
                reservada.RegisterValueChangedCallback(async ev =>
                {
                    if (!await OrdenDePlaza(new Orden { ReservarCaravana = new OrdenReservarCaravana { CaravanaId = k.Id, Reservada = ev.newValue } })) reservada.SetValueWithoutNotify(ev.previousValue);
                });
                cab.Add(reservada);
            }
            else if (k.ReservadaManual) cab.Add(Chip("🔒 Reservada"));
            caja.Add(cab);
            if (ajena) { CuerpoAparcada(caja, k, a); return caja; }
            if (!enPlaza)
            {
                caja.Add(Dialogo.Texto("Está fuera de la plaza: se maneja cuando vuelva.", "plaza-nota"));
                return caja;
            }
            var editable = parada && reside;
            // Lo que puede llevar, su velocidad (la del animal más lento) y lo que tarda en prepararse.
            var tiran = k.Carros.Where(r => r.HasAnimal && CatalogoDeAnimales?[r.Animal] != null).ToList();
            var capacidad = CapacidadDe(k, a);
            var velocidad = tiran.Count > 0 ? tiran.Min(r => (double)CatalogoDeAnimales[r.Animal]["velocidad"]) * DePoliticas(a, "factorVelocidadCaravana") : 0;
            var kPorCarro = (double?)c.Datos.Bloque("Comercio_Caravanas", "CARAVANA_PREPARACION")?["kPorCarro"] ?? 0;
            var chips = Dialogo.Caja("caravana-chips");
            chips.Add(DatoDeCaravana("📦", capacidad.ToString(), "carga"));
            chips.Add(DatoDeCaravana("🐾", tiran.Count > 0 ? (Math.Round(velocidad * 10) / 10).ToString("0.#") : "—", "velocidad"));
            chips.Add(DatoDeCaravana("⏱", k.Carros.Count > 1 ? $"{Math.Round(kPorCarro * (k.Carros.Count - 1))} min" : "ya", "preparar"));
            caja.Add(chips);
            Lleva(caja, k, a, reside && (parada || (k.Estado == EstadoCaravana.CaravanaEsperando && k.Encargo?.ParadaActual == "origen")), capacidad);
            CarrosEnHuecos(caja, k, a, editable, flota.Where(x => x.Id != k.Id && x.Estado == EstadoCaravana.CaravanaDisponible && MismoSitio(x.Posicion, a.Posicion)).ToList(), carrosLleno, rehacer);
            // La escolta: la del carro de acarreo, con el cupo del Mercado y la caravana parada en su origen.
            Escolta(caja, k, a.Id, false, CupoDeEscoltaComercial(a), "Escolta", " Solo se cambia con la caravana parada en su origen.");
            if (k.Estado == EstadoCaravana.CaravanaPreparando)
            {
                var sale = Dialogo.Caja("caravana-seccion");
                sale.Add(Dialogo.Texto($"Sale en {(k.HasPreparaHastaMs ? FaltaPara(k.PreparaHastaMs) : "un momento")}.", "plaza-nota"));
                if (reside)
                {
                    Button cancelar = null;
                    cancelar = Dialogo.Boton("Cancelar preparación", "boton-secundario", async () => { if (await OrdenDePlaza(new Orden { CancelarCaravana = new OrdenCancelarCaravana { CaravanaId = k.Id } }, cancelar)) rehacer(); });
                    cancelar.name = "caravana-cancelar-preparacion";
                    sale.Add(cancelar);
                }
                caja.Add(sale);
            }
            else if (editable && tiran.Count > 0 && k.Encargo == null) Viaje(caja, k, a, yo, capacidad, rehacer);
            return caja;
        }

        static VisualElement DatoDeCaravana(string icono, string valor, string nombre)
        {
            var d = Dialogo.Caja("caravana-dato");
            d.Add(Dialogo.Texto(icono, "caravana-dato-icono"));
            d.Add(Dialogo.Texto(valor, "caravana-dato-valor"));
            d.Add(Dialogo.Texto(nombre, "caravana-dato-nombre"));
            return d;
        }

        /// <summary>Lo que lleva, recurso a recurso, y, en casa y residiendo, «⬇ Descargar» al almacén (`moverCargaCaravanaAparcada`).</summary>
        void Lleva(VisualElement caja, Caravana k, Asentamiento a, bool descarga, int capacidad)
        {
            var lleva = k.Contenido?.PorId.Where(kv => kv.Value >= 1000).OrderBy(kv => kv.Key).ToList() ?? new List<KeyValuePair<string, long>>();
            if (lleva.Count == 0) return;
            var fila = Dialogo.Caja("flota-lleva");
            fila.name = "flota-lleva";
            fila.Add(Dialogo.Texto("Lleva", "flota-lleva-titulo"));
            foreach (var kv in lleva) fila.Add(IconosDelMundo.ChipDeRecurso(kv.Key, (kv.Value / 1000).ToString(), CosasDelMapa.NombreDeRecurso(kv.Key)));
            fila.Add(Dialogo.Texto($"{CargaDe(k)}{(capacidad > 0 ? $" / {capacidad}" : "")}", "flota-lleva-total"));
            caja.Add(fila);
            if (!descarga) return;
            var acciones = Dialogo.Caja("flota-descargar");
            foreach (var kv in lleva)
            {
                var r = kv.Key;
                var cantidad = kv.Value;
                var b = BotonDeOrden($"⬇ {CosasDelMapa.NombreDeRecurso(r)}", "descargar-" + r, () => new Orden
                {
                    MoverCargaCaravanaAparcada = new OrdenMoverCargaCaravanaAparcada { HeroeId = c.HeroeId, CaravanaId = k.Id, AsentamientoId = a.Id, Recurso = r, Cantidad = cantidad, Sentido = "descargar" },
                }, titulo: "Del carro al almacén de la plaza");
                acciones.Add(b);
            }
            caja.Add(acciones);
        }

        /// <summary>Los carros en huecos (`carrosEnHuecos`): cada uno con su animal y lo que lleva; «+» abre el añadir (carro y animal a la vez); uno
        /// elegido, su ficha: comprarle el animal que le falte o pasarlo a otra caravana parada en la plaza.</summary>
        void CarrosEnHuecos(VisualElement caja, Caravana k, Asentamiento a, bool editable, List<Caravana> otras, bool carrosLleno, Action rehacer)
        {
            var seccion = Dialogo.Caja("caravana-seccion");
            seccion.name = "caravana-carros";
            seccion.Add(Kicker("Carros"));
            if (k.Carros.Count == 0) seccion.Add(Dialogo.Texto("Caravana vacía: no sale hasta que le montes un carro con su animal.", "plaza-nota"));
            var huecos = Dialogo.Caja("carro-huecos");
            var elegido = carroElegido != null && carroElegido.StartsWith(k.Id + ":") ? int.Parse(carroElegido.Substring(k.Id.Length + 1)) : -1;
            for (int i = 0; i < k.Carros.Count; i++)
            {
                var r = k.Carros[i];
                var indice = i;
                var hueco = new Button(() => { carroElegido = elegido == indice ? null : $"{k.Id}:{indice}"; rehacer(); }) { name = $"carro-{k.Id}-{i}" };
                hueco.AddToClassList("carro-hueco");
                hueco.AddToClassList("lleno");
                hueco.EnableInClassList("sin-animal", !r.HasAnimal);
                hueco.EnableInClassList("activo", elegido == i);
                hueco.SetEnabled(editable);
                hueco.Add(Dialogo.Texto(r.HasAnimal ? IconoDeAnimal(r.Animal) : "∅", "carro-hueco-icono"));
                hueco.Add(Dialogo.Texto(r.TipoCarro == "reforzado" ? "Reforzado" : "Básico", "carro-hueco-nombre"));
                var cap = r.HasAnimal && CatalogoDeCarros?[r.TipoCarro] is JObject def && CatalogoDeAnimales?[r.Animal] is JObject an
                    ? (int)Math.Floor((double)def["capacidadBase"] * (double)an["factorCarga"] * DePoliticas(a, "factorCapacidadCaravana")) : -1;
                hueco.Add(Dialogo.Texto(r.HasAnimal ? $"{(NombreDeAnimal.TryGetValue(r.Animal, out var na) ? na : r.Animal)}{(cap >= 0 ? $" · 📦 {cap}" : "")}" : "sin animal: no tira", "carro-hueco-pie"));
                huecos.Add(hueco);
            }
            var abierto = carroNuevoAbierto.Contains(k.Id);
            if (editable && !carrosLleno)
            {
                var mas = Dialogo.Boton("+", "carro-hueco mas" + (abierto ? " activo" : ""), () => { if (!carroNuevoAbierto.Remove(k.Id)) carroNuevoAbierto.Add(k.Id); rehacer(); });
                mas.name = "carro-mas-" + k.Id;
                mas.tooltip = "Añadir un carro";
                huecos.Add(mas);
            }
            seccion.Add(huecos);
            if (editable && elegido >= 0 && elegido < k.Carros.Count) seccion.Add(FichaDeCarro(k, elegido, a, otras, rehacer));
            if (editable && !carrosLleno && abierto) seccion.Add(CarroNuevo(k, a, rehacer));
            caja.Add(seccion);
        }

        static string IconoDeAnimal(string animal) => animal switch { "buey" => "🐂", "caballo" => "🐎", "camello" => "🐫", _ => "🐾" };

        /// <summary>Lo que falta del almacén para pagar un coste, o null si alcanza.</summary>
        string FaltaParaPagar(Asentamiento a, JObject coste)
        {
            var faltan = coste?.Properties().Where(p => Hay(a, p.Name) < (double)p.Value).Select(p => $"{(double)p.Value} {CosasDelMapa.NombreDeRecurso(p.Name)}").ToList();
            return faltan == null || faltan.Count == 0 ? null : $"Faltan {string.Join(" y ", faltan)} en el almacén.";
        }

        VisualElement FichaDeCarro(Caravana k, int i, Asentamiento a, List<Caravana> otras, Action rehacer)
        {
            var r = k.Carros[i];
            var ficha = Dialogo.Caja("carro-ficha");
            ficha.name = "carro-ficha";
            var cab = Dialogo.Caja("carro-ficha-cab");
            cab.Add(Dialogo.Texto(r.HasAnimal ? IconoDeAnimal(r.Animal) : "∅", "carro-ficha-icono"));
            cab.Add(Dialogo.Texto($"{(NombreDeCarro.TryGetValue(r.TipoCarro, out var nc) ? nc : r.TipoCarro)}{(r.HasAnimal ? $" con {(NombreDeAnimal.TryGetValue(r.Animal, out var na) ? na : r.Animal).ToLowerInvariant()}" : ", sin animal")}", "carro-ficha-nombre"));
            ficha.Add(cab);
            if (!r.HasAnimal)
            {
                // Le falta el animal: los que se pueden pagar.
                var fila = Dialogo.Caja("carro-acciones");
                fila.Add(Dialogo.Texto("Le falta el animal:", "plaza-sub"));
                foreach (var p in CatalogoDeAnimales?.Properties() ?? Enumerable.Empty<JProperty>())
                {
                    var animal = p.Name;
                    if (FaltaParaPagar(a, p.Value["costo"] as JObject) != null) continue;
                    fila.Add(BotonDeOrden($"{IconoDeAnimal(animal)} {(NombreDeAnimal.TryGetValue(animal, out var n) ? n : animal)}", "comprar-animal-" + animal,
                        () => new Orden { ComprarAnimalCaravana = new OrdenComprarAnimalCaravana { CaravanaId = k.Id, CarroIndice = (uint)i, TipoAnimal = animal } }));
                }
                ficha.Add(fila);
            }
            if (otras.Count > 0)
            {
                var fila = Dialogo.Caja("carro-acciones");
                fila.Add(Dialogo.Texto("Pasarlo a", "plaza-sub"));
                foreach (var otra in otras)
                {
                    var destino = otra.Id;
                    var b = BotonDeOrden($"⇄ {NombreDeCaravana(otra)}", "mover-carro-" + destino,
                        () => new Orden { MoverCarroCaravana = new OrdenMoverCarroCaravana { DesdeCaravanaId = k.Id, HaciaCaravanaId = destino, CarroIndice = (uint)i } });
                    b.clicked += () => carroElegido = null;
                    fila.Add(b);
                }
                ficha.Add(fila);
            }
            return ficha;
        }

        /// <summary>Añadir un carro (`carro-nuevo`): el tipo (los que se fabrican aquí) y su animal, con el total; lo que no se puede pagar, apagado.</summary>
        VisualElement CarroNuevo(Caravana k, Asentamiento a, Action rehacer)
        {
            var caja = Dialogo.Caja("carro-nuevo");
            caja.name = "carro-nuevo";
            var tipos = (CatalogoDeCarros?.Properties() ?? Enumerable.Empty<JProperty>())
                .Where(p => (string)p.Value["fabrica"] == "mercado" || Internos(a).Any(e => e.Tipo == (string)p.Value["fabrica"] && Activo(e))).ToList();
            var animales = CatalogoDeAnimales?.Properties().ToList() ?? new List<JProperty>();
            carroNuevo.TryGetValue(k.Id, out var ele);
            var tipo = tipos.Any(p => p.Name == ele.tipo) ? ele.tipo : tipos.FirstOrDefault()?.Name;
            var animal = animales.Any(p => p.Name == ele.animal) ? ele.animal : animales.FirstOrDefault()?.Name;
            VisualElement Grupo(string titulo, List<JProperty> opciones, string actual, Func<JProperty, string> icono, Func<JProperty, string> nombre, Func<JProperty, string> detalle, Action<string> elegir)
            {
                var g = Dialogo.Caja("carro-nuevo-grupo");
                g.Add(Dialogo.Texto(titulo, "plaza-sub"));
                foreach (var p in opciones)
                {
                    var id = p.Name;
                    var falta = FaltaParaPagar(a, p.Value["costo"] as JObject);
                    var b = new Button(() => { elegir(id); rehacer(); }) { name = $"carro-nuevo-{id}", tooltip = falta ?? nombre(p) };
                    b.AddToClassList("carro-opcion");
                    b.EnableInClassList("elegido", id == actual);
                    b.EnableInClassList("caro", falta != null);
                    b.Add(Dialogo.Texto(icono(p), "carro-opcion-icono"));
                    var t = Dialogo.Caja("carro-opcion-texto");
                    t.Add(Dialogo.Texto(nombre(p), "carro-opcion-nombre"));
                    t.Add(Dialogo.Texto(detalle(p), "carro-opcion-detalle"));
                    t.Add(CosteLlano(p.Value["costo"] as JObject));
                    b.Add(t);
                    g.Add(b);
                }
                return g;
            }
            caja.Add(Grupo("Carro", tipos, tipo, p => p.Name == "reforzado" ? "R" : "B", p => NombreDeCarro.TryGetValue(p.Name, out var n) ? n : p.Name,
                p => $"capacidad {(double)p.Value["capacidadBase"]}", id => carroNuevo[k.Id] = (id, animal)));
            caja.Add(Grupo("Animal", animales, animal, p => IconoDeAnimal(p.Name), p => NombreDeAnimal.TryGetValue(p.Name, out var n) ? n : p.Name,
                p => $"×{(double)p.Value["factorCarga"]} de carga · vel. {(double)p.Value["velocidad"]}", id => carroNuevo[k.Id] = (tipo, id)));
            var coste = Sumar(CatalogoDeCarros?[tipo ?? ""]?["costo"] as JObject, CatalogoDeAnimales?[animal ?? ""]?["costo"] as JObject);
            var falta = tipo == null || animal == null ? "Elige el carro y su animal." : FaltaParaPagar(a, coste);
            var pie = Dialogo.Caja("carro-nuevo-pie");
            pie.Add(Dialogo.Texto("Total:", "plaza-sub"));
            pie.Add(CosteLlano(coste));
            if (falta == null)
            {
                // Agregar: el carro y, en cuanto el motor lo pone, su animal (el último carro de la caravana).
                Button agregar = null;
                agregar = Dialogo.Boton("Agregar", "boton-primario", async () =>
                {
                    if (!await OrdenDePlaza(new Orden { AgregarCarroCaravana = new OrdenAgregarCarroCaravana { CaravanaId = k.Id, TipoCarro = tipo } }, agregar)) return;
                    await OrdenDePlaza(new Orden { ComprarAnimalCaravana = new OrdenComprarAnimalCaravana { CaravanaId = k.Id, CarroIndice = (uint)k.Carros.Count, TipoAnimal = animal } });
                    carroNuevoAbierto.Remove(k.Id);
                    rehacer();
                });
                agregar.name = "carro-agregar";
                pie.Add(agregar);
            }
            caja.Add(pie);
            return caja;
        }

        /// <summary>El viaje a mano (`seccionViaje`): el destino (las plazas que conoces), la carga del almacén contra la capacidad y «Lanzar».</summary>
        void Viaje(VisualElement caja, Caravana k, Asentamiento a, Heroe yo, int capacidad, Action rehacer)
        {
            var al = c.Sesion.Almacen;
            var destinos = al.Todas(Entidad.CuerpoOneofCase.Asentamiento).Select(e => (id: e.Asentamiento.Id, nombre: e.Asentamiento.HasNombre ? e.Asentamiento.Nombre : e.Asentamiento.Id, faccion: e.Asentamiento.FaccionId))
                .Concat(al.Todas(Entidad.CuerpoOneofCase.FichaConocida).Select(e => (id: e.FichaConocida.AsentamientoId, nombre: e.FichaConocida.HasNombre ? e.FichaConocida.Nombre : e.FichaConocida.AsentamientoId, faccion: e.FichaConocida.FaccionId)))
                .Where(x => x.id != a.Id).GroupBy(x => x.id).Select(g => g.First()).ToList();
            if (destinos.Count == 0) return;
            var seccion = Dialogo.Caja("caravana-seccion");
            seccion.name = "caravana-viaje";
            var cab = Dialogo.Caja("caravana-seccion-cab");
            cab.Add(Kicker("Viaje"));
            var nombres = destinos.Select(x => x.nombre + (x.faccion == yo.FaccionId ? " (tuya)" : "")).ToList();
            var destino = new DropdownField("Destino", nombres, 0) { name = "viaje-destino" };
            destino.AddToClassList("viaje-destino");
            cab.Add(destino);
            seccion.Add(cab);
            var cargas = new Dictionary<string, IntegerField>();
            var rejilla = Dialogo.Caja("carga-grid");
            foreach (var r in a.Almacen.Keys.Where(r => r != "oro" && Hay(a, r) >= 1).OrderBy(r => r))
            {
                var celda = Dialogo.Caja("carga-celda");
                celda.Add(IconosDelMundo.ConIcono("recurso", r, Dialogo.Texto(CosasDelMapa.NombreDeRecurso(r), "carga-celda-nombre"), 16));
                var campo = new IntegerField { name = "viaje-carga-" + r, value = 0 };
                campo.AddToClassList("carga-celda-campo");
                celda.Add(campo);
                celda.Add(Dialogo.Texto("/" + Hay(a, r), "carga-celda-hay"));
                cargas[r] = campo;
                rejilla.Add(celda);
            }
            if (cargas.Count == 0) seccion.Add(Dialogo.Texto("No hay nada que cargar en el almacén.", "plaza-nota"));
            else seccion.Add(rejilla);
            var total = CupoEnBarra("de carga", 0, capacidad);
            total.name = "viaje-total";
            seccion.Add(total);
            var kPorCarro = (double?)c.Datos.Bloque("Comercio_Caravanas", "CARAVANA_PREPARACION")?["kPorCarro"] ?? 0;
            Button lanzar = null;
            lanzar = Dialogo.Boton(k.Carros.Count > 1 ? $"Preparar ({Math.Round(kPorCarro * (k.Carros.Count - 1))} min) y lanzar" : "Lanzar ya", "boton-primario", async () =>
            {
                var o = new OrdenPrepararCaravana { CaravanaId = k.Id, HeroeId = yo.Id, DestinoAsentamientoId = destinos[Math.Max(0, destino.index)].id, Carga = new Cantidades() };
                foreach (var kv in cargas.Where(kv => kv.Value.value > 0)) o.Carga.PorId[kv.Key] = Math.Min(kv.Value.value, (long)Hay(a, kv.Key)) * 1000L;
                if (await OrdenDePlaza(new Orden { PrepararCaravana = o }, lanzar)) rehacer();
            });
            lanzar.name = "viaje-lanzar";
            void Total()
            {
                var suma = cargas.Values.Sum(x => Math.Max(0, x.value));
                total.Q<Label>(className: "puerta-cupo-n").text = $"{suma} / {capacidad}";
                if (total.Q(className: "plaza-barra-avance") is VisualElement barra) Avance(barra, capacidad > 0 ? Math.Min(1f, (float)suma / capacidad) : 0);
                lanzar.SetEnabled(suma <= capacidad);
            }
            foreach (var x in cargas.Values) x.RegisterValueChangedCallback(_ => Total());
            seccion.Add(lanzar);
            caja.Add(seccion);
        }

        /// <summary>Una caravana de otra plaza aparcada aquí (`cuerpoAparcada`): su carga, «Cargar» y «Descargar» por recurso y «↩ Enviar al origen».</summary>
        void CuerpoAparcada(VisualElement caja, Caravana k, Asentamiento a)
        {
            var lleva = k.Contenido?.PorId.Where(kv => kv.Value >= 1000).ToList() ?? new List<KeyValuePair<string, long>>();
            var fila = Dialogo.Caja("flota-lleva");
            fila.Add(Dialogo.Texto("Lleva", "flota-lleva-titulo"));
            if (lleva.Count == 0) fila.Add(Dialogo.Texto("Va vacía", "plaza-sub"));
            foreach (var kv in lleva) fila.Add(IconosDelMundo.ChipDeRecurso(kv.Key, (kv.Value / 1000).ToString(), CosasDelMapa.NombreDeRecurso(kv.Key)));
            caja.Add(fila);
            foreach (var r in lleva.Select(kv => kv.Key).Concat(a.Almacen.Keys.Where(x => x != "oro" && Hay(a, x) >= 1)).Distinct().OrderBy(x => x))
            {
                var recurso = r;
                var mover = Dialogo.Caja("puerta-carga");
                mover.name = "aparcada-" + r;
                mover.Add(IconosDelMundo.ConIcono("recurso", r, Dialogo.Texto(CosasDelMapa.NombreDeRecurso(r), "puerta-carga-nombre"), 18));
                var cantidad = new IntegerField { name = "aparcada-cantidad-" + r, value = 0 };
                cantidad.AddToClassList("puerta-carga-cantidad");
                mover.Add(cantidad);
                Orden Mover(string sentido) => new Orden { MoverCargaCaravanaAparcada = new OrdenMoverCargaCaravanaAparcada { HeroeId = c.HeroeId, CaravanaId = k.Id, AsentamientoId = a.Id, Recurso = recurso, Cantidad = Math.Max(1, cantidad.value) * 1000L, Sentido = sentido } };
                if (Hay(a, r) >= 1) mover.Add(BotonDeOrden("⬆ Cargar", "aparcada-cargar-" + r, () => Mover("cargar"), titulo: "Del almacén al carro"));
                if (k.Contenido?.PorId.TryGetValue(r, out var n) == true && n >= 1000) mover.Add(BotonDeOrden("⬇ Descargar", "aparcada-descargar-" + r, () => Mover("descargar"), titulo: "Del carro al almacén"));
                caja.Add(mover);
            }
            caja.Add(BotonDeOrden("↩ Enviar al origen", "aparcada-origen", () => new Orden { EnviarCaravanaAlOrigen = new OrdenEnviarCaravanaAlOrigen { HeroeId = c.HeroeId, CaravanaId = k.Id, AsentamientoId = a.Id } }));
        }
    }
}
