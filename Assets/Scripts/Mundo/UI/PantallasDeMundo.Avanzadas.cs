// Las avanzadas en Territorio (paso 9 del bloque 4, tanda 2), calcadas de BronzeAgeClient (`ui/pueblos.ts`: `htmlAvanzadasDeLaPlaza`,
// `tarjetaAvanzada`; `ui/avanzadas.ts`: `htmlEleccionDeTropa`). La columna de la derecha de «Pueblos y aldeas», solo en una plaza de tu Facción: el
// cupo, una tarjeta por avanzada (guarnición, almacén, tropa de serie para el Gobernador, «⬆ Fuerte/Ciudadela», «Reparar» en ruinas, «✕ Destruir»)
// con su obra en marcha debajo, las obras de atalaya nueva, lo que pide la atalaya y «Elegir el sitio de la atalaya» en el mapa 2D.
using System;
using System.Collections.Generic;
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
        JToken ReglasAvanzadas => c.Datos.Bloque("Economia_Construccion", "AVANZADAS");
        JToken CombateAvanzada => c.Datos.Bloque("Combate", "AVANZADA");

        (List<Avanzada> avanzadas, List<ObraDeMapa> obras) AvanzadasDe(Asentamiento a)
        {
            var al = c.Sesion.Almacen;
            return (al.Todas(Entidad.CuerpoOneofCase.Avanzada).Select(e => e.Avanzada).Where(x => x.AsentamientoMadreId == a.Id).OrderBy(x => x.Id).ToList(),
                al.Todas(Entidad.CuerpoOneofCase.Obra).Select(e => e.Obra).Where(o => o.AsentamientoMadreId == a.Id && o.Tipo == TipoObra.ObraAvanzada).OrderBy(o => o.Id).ToList());
        }

        string FirmaDeAvanzadas(Asentamiento a)
        {
            var (avanzadas, obras) = AvanzadasDe(a);
            var al = c.Sesion.Almacen;
            var sobre = al.Todas(Entidad.CuerpoOneofCase.Obra).Select(e => e.Obra).Where(o => o.AsentamientoMadreId == a.Id && (o.Tipo == TipoObra.ObraMejoraAvanzada || o.Tipo == TipoObra.ObraReparacion));
            return string.Join(",", avanzadas.Select(x => x.Id + ":" + x.Version)) + "|" + string.Join(",", obras.Concat(sobre).Select(o => o.Id + ":" + o.Version));
        }

        /// <summary>La columna de las avanzadas (`htmlAvanzadasDeLaPlaza`).</summary>
        VisualElement AvanzadasDeLaPlaza(Asentamiento a, Heroe yo, bool gestiona, Action rehacer)
        {
            var col = Dialogo.Caja("territorio-col");
            col.name = "avanzadas";
            var (avanzadas, obras) = AvanzadasDe(a);
            var nivel = a.NivelActual > 0 ? a.NivelActual : a.Nivel;
            var porNivel = (int?)ReglasAvanzadas?["cupoPorNivelDeLaMadre"];
            int? total = porNivel == null ? (int?)null : (int)nivel * porNivel.Value;
            var usados = avanzadas.Count + obras.Count;
            VisualElement cupo = null;
            if (total != null)
            {
                cupo = ChipConIcono(IconosDelEtapa(1, 13), $"{usados} / {total}", usados >= total ? "lleno" : "", "Cupo de avanzadas por el nivel de la plaza");
                cupo.name = "cupo-avanzadas";
                if (total > 0) cupo.Add(Casillas(usados, total.Value, "territorio-cupo-casillas", "usada"));
            }
            col.Add(TituloDeSeccion("Avanzadas", "Un puesto fortificado de la plaza en su zona: atalaya, fuerte y ciudadela. Ve a lo lejos, guarda un almacén y tiene guarnición propia.", cupo));
            if (avanzadas.Count + obras.Count == 0) col.Add(Dialogo.Texto("Esta plaza aún no tiene avanzadas.", "plaza-nota"));
            var gobernador = a.Cargos != null && a.Cargos.HasGobernadorId && a.Cargos.GobernadorId == yo.Id && yo.HasResidenciaId && yo.ResidenciaId == a.Id;
            foreach (var x in avanzadas) col.Add(TarjetaAvanzada(a, x, gestiona, gobernador, yo));
            foreach (var o in obras) col.Add(TarjetaObra(a, o, gestiona, yo));

            // Lo que pide la atalaya y el botón para elegir su sitio en el mapa.
            var atalaya = ReglasAvanzadas?["obras"]?["atalaya"];
            var nivelAtalaya = (int?)ReglasAvanzadas?["nivelMadre"]?["atalaya"];
            if (atalaya?["costo"] is JObject costo)
            {
                var fila = Dialogo.Caja("territorio-avanzada-coste");
                fila.Add(IconosDelMundo.ConIcono("simbolo", "torre", Dialogo.Texto("Atalaya", "territorio-boton-texto"), 14));
                fila.Add(CosteLlano(costo));
                var minutos = (double?)atalaya["minutos"];
                if (minutos != null) fila.Add(IconosDelMundo.ConIcono("glifo", "espera", Dialogo.Texto(minutos >= 60 ? $"{Math.Floor(minutos.Value / 60)} h de obra" : $"{minutos} min de obra", "plaza-obra-titulo-small"), 12));
                if (nivelAtalaya != null) fila.Add(Dialogo.Texto($"· plaza a nivel {nivelAtalaya}", "plaza-obra-titulo-small"));
                col.Add(fila);
            }
            if (gestiona)
            {
                var motivo = nivelAtalaya != null && nivel < nivelAtalaya ? $"La plaza tiene que estar a nivel {nivelAtalaya}."
                    : total != null && usados >= total ? $"Cupo lleno ({usados} / {total}): sube la plaza de nivel o destruye una." : "";
                var elegir = Dialogo.Boton("", "territorio-boton principal territorio-elegir-atalaya", () => ElegirSitioDeAtalaya(a, yo));
                elegir.name = "elegir-avanzada";
                elegir.Add(IconosDelMundo.Icono("glifo", "mapa", 16));
                elegir.Add(Dialogo.Texto("Elegir el sitio de la atalaya", "territorio-boton-texto"));
                elegir.SetEnabled(motivo == "");
                if (motivo != "") elegir.tooltip = motivo;
                col.Add(elegir);
                col.Add(Dialogo.Texto(motivo != "" ? motivo : "Dentro de la zona, fuera de la ciudad y sin pisar otro pueblo, avanzada o campamento. Lo que pide se lleva a su puerta con una columna.", "plaza-nota"));
            }
            else col.Add(Dialogo.Texto("Las coloca el Gobernador o el Maestro de Obras, residente y dentro de la plaza.", "plaza-nota"));
            return col;
        }

        /// <summary>El icono de una avanzada por su etapa: la torre (♜) del prototipo.</summary>
        static VisualElement IconosDelEtapa(int etapa, float lado) => IconosDelMundo.Icono("simbolo", "torre", lado, IconosDelMundo.OroClaro);

        /// <summary>«Elegir el sitio de la atalaya»: el mapa 2D eligiendo un punto dentro de la zona; el clic manda `colocarAvanzada`.</summary>
        void ElegirSitioDeAtalaya(Asentamiento a, Heroe yo)
        {
            var plazaId = a.Id;
            AbrirMapa2D(Pt(a.Posicion), new EleccionEnMapa
            {
                Texto = "Pulsa en el mapa donde colocar la atalaya: dentro de la zona de la plaza, fuera de la ciudad y sin pisar otro pueblo, avanzada o campamento.",
                Zona = ZonaDe(a),
                AlElegir = async (x, y) =>
                {
                    try
                    {
                        var r = await c.Sesion.Ordenar(new Orden { ColocarAvanzada = new OrdenColocarAvanzada { AsentamientoId = plazaId, HeroeId = yo.Id, Posicion = new Punto { X = (long)Math.Round(x * 1000), Y = (long)Math.Round(y * 1000) } } });
                        return r.Resultado == Resultado.OrdenAceptada ? null : Textos.Explicar(r.Rechazo);
                    }
                    catch (ErrorDeSesion e) { return e.Message; }
                },
            });
        }

        /// <summary>Una avanzada de la plaza (`tarjetaAvanzada`): qué es y cómo está, su guarnición y su almacén; para quien gestiona, «⬆», «Reparar» y
        /// «✕ Destruir»; el Gobernador elige además su tropa de serie. Su obra en marcha (mejora o reparación) va debajo.</summary>
        VisualElement TarjetaAvanzada(Asentamiento a, Avanzada x, bool gestiona, bool gobernador, Heroe yo)
        {
            var caja = Dialogo.Caja("");
            var etapa = (int)x.Etapa;
            var ruinas = x.Estado == EstadoAvanzada.AvanzadaEnRuinas;
            var obra = ObraSobre(x.Id);
            var t = Dialogo.Caja("plaza-anillo territorio-tarjeta" + (ruinas ? " ruinas" : "") + (x.HasBatallaId ? " atacado" : ""));
            t.name = "tarjeta-" + x.Id;
            var cab = Dialogo.Caja("plaza-anillo-cab");
            var emblema = Dialogo.Caja("territorio-emblema");
            emblema.Add(IconosDelEtapa(etapa, 22));
            cab.Add(emblema);
            var titulo = Dialogo.Caja("plaza-anillo-titulo");
            titulo.Add(Dialogo.Texto(NombreDeEtapa(etapa), "plaza-anillo-nombre"));
            titulo.Add(Dialogo.Texto($"a {Math.Floor(Dist(Pt(x.Posicion), Pt(a.Posicion)))} u de la plaza", "plaza-obra-titulo-small"));
            cab.Add(titulo);
            if (x.HasBatallaId) cab.Add(ChipConIcono(IconosDelMundo.Icono("glifo", "atacar", 12), "¡Atacada!", "baja"));
            else if (ruinas) cab.Add(Dialogo.Texto("En ruinas", "plaza-anillo-estado"));
            else if (obra != null) cab.Add(Dialogo.Texto(obra.Tipo == TipoObra.ObraReparacion ? "En reparación" : "⬆ Mejorando", "plaza-anillo-estado"));
            t.Add(cab);

            var chips = Dialogo.Caja("plaza-anillo-chips");
            var jugadores = x.GuarnicionJugadoresIds.Count;
            chips.Add(ChipConIcono(IconosDelMundo.Icono("glifo", "atacar", 12), $"{x.GuarnicionSerieIds.Count} de serie" + (jugadores > 0 ? $" · {jugadores} de jugadores" : ""), "", "Escuadras de serie y de jugadores en su guarnición"));
            if (x.AlmacenPorRecurso.Values.Any(e => e.Capacidad > 0))
                chips.Add(ChipConIcono(IconosDelMundo.Icono("recurso", "_otro", 12), Math.Floor(x.AlmacenPorRecurso.Values.Sum(e => Valor(e.Cantidad))).ToString(), "", "Lo que guarda su almacén"));
            t.Add(chips);
            if (gobernador && !ruinas) { var tropa = EleccionDeTropa(a, x); if (tropa != null) t.Add(tropa); }

            if (gestiona)
            {
                var acciones = Dialogo.Caja("plaza-anillo-acciones");
                var siguiente = etapa < 3 ? ClaveDeEtapa(etapa + 1) : null;
                if (siguiente != null && !ruinas && obra == null)
                {
                    var nivelPide = (int?)ReglasAvanzadas?["nivelMadre"]?[siguiente];
                    var nivel = a.NivelActual > 0 ? a.NivelActual : a.Nivel;
                    var motivo = nivelPide != null && nivel < nivelPide ? $"La plaza tiene que estar a nivel {nivelPide}." : "";
                    Button b = null;
                    b = Dialogo.Boton("", "plaza-anillo-mejorar", async () => await OrdenDePlaza(new Orden { MejorarAvanzada = new OrdenMejorarAvanzada { HeroeId = yo.Id, AvanzadaId = x.Id } }, b));
                    b.name = "mejorar-" + x.Id;
                    b.Add(Dialogo.Texto("⬆ " + NombreDeEtapa(etapa + 1), "territorio-boton-texto"));
                    b.Add(CosteLlano(ReglasAvanzadas?["obras"]?[siguiente]?["costo"] as JObject));
                    b.SetEnabled(motivo == "");
                    b.tooltip = motivo != "" ? motivo : $"A {NombreDeEtapa(etapa + 1).ToLowerInvariant()}: lo que pide se lleva a su puerta como una obra.";
                    acciones.Add(b);
                }
                if (ruinas && obra == null)
                {
                    var fraccion = (double?)ReglasAvanzadas?["fraccionReparacion"] ?? 0.5;
                    var coste = new JObject();
                    if (ReglasAvanzadas?["obras"]?[ClaveDeEtapa(etapa)]?["costo"] is JObject co) foreach (var p in co.Properties()) coste[p.Name] = Math.Ceiling((double)p.Value * fraccion);
                    Button b = null;
                    b = Dialogo.Boton("", "plaza-anillo-mejorar", async () => await OrdenDePlaza(new Orden { RepararAvanzada = new OrdenRepararAvanzada { HeroeId = yo.Id, AvanzadaId = x.Id } }, b));
                    b.name = "reparar-" + x.Id;
                    b.Add(IconosDelMundo.Icono("glifo", "mantenimiento", 14));
                    b.Add(Dialogo.Texto("Reparar", "territorio-boton-texto"));
                    b.Add(CosteLlano(coste));
                    b.tooltip = "La deja operativa: lo que pide se lleva a su puerta como una obra.";
                    acciones.Add(b);
                }
                Button destruir = null;
                destruir = Dialogo.Boton("✕ Destruir", "plaza-anillo-abandonar", async () =>
                {
                    if (await dialogo.Abrir("Se pierde lo que guarda su almacén, y las escuadras de su guarnición vuelven a los campamentos de sus héroes.", "¿Destruir la avanzada?", "Destruir", "Volver", true))
                        await OrdenDePlaza(new Orden { DestruirAvanzada = new OrdenDestruirAvanzada { HeroeId = yo.Id, AvanzadaId = x.Id } }, destruir);
                });
                destruir.name = "destruir-" + x.Id;
                acciones.Add(destruir);
                t.Add(acciones);
            }
            caja.Add(t);
            if (obra != null) caja.Add(TarjetaObraDeAvanzada(a, obra, gestiona, yo));
            return caja;
        }

        /// <summary>La obra de una avanzada (nueva, mejora o reparación) con «✕ Cancelar» por `cancelarObra`.</summary>
        VisualElement TarjetaObraDeAvanzada(Asentamiento a, ObraDeMapa o, bool gestiona, Heroe yo) => TarjetaObra(a, o, gestiona, yo);

        /// <summary>La tropa de serie de cada escalón (`htmlEleccionDeTropa`): el Gobernador la cambia entre las que la madre recluta.</summary>
        VisualElement EleccionDeTropa(Asentamiento a, Avanzada x)
        {
            var porEscalon = CombateAvanzada?["guarnicionDeSerie"]?[ClaveDeEtapa((int)x.Etapa)] as JObject;
            if (porEscalon == null || !porEscalon.HasValues) return null;
            var porDefecto = (string)CombateAvanzada?["tropaPorDefecto"] ?? "";
            var catalogo = c.Datos.TropasReclutables as JArray ?? new JArray();
            var adoptadas = TecnologiasDeMiFaccion() ?? new HashSet<string>();
            var niveles = new Dictionary<string, int>();
            foreach (var b in Internos(a).Where(Activo)) niveles[b.Tipo] = Math.Max(niveles.TryGetValue(b.Tipo, out var n) ? n : 0, NivelInterno(b));
            bool Recluta(JToken t) => adoptadas.Contains((string)t["tecnologia"] ?? "") && ((string)t["edificio"] == "centroUrbano" || (niveles.TryGetValue((string)t["edificio"] ?? "", out var n) ? n : 0) >= ((int?)t["nivelRequerido"] ?? 1));
            var caja = Dialogo.Caja("territorio-tropa");
            caja.Add(Dialogo.Texto("Tropa de serie", "plaza-obra-titulo-small"));
            foreach (var p in porEscalon.Properties().OrderBy(p => int.Parse(p.Name)))
            {
                var escalon = uint.Parse(p.Name);
                var actual = x.TropaElegidaPorEscalon.TryGetValue(escalon, out var tr) ? tr : porDefecto;
                var opciones = catalogo.Where(t => (int?)t["escalon"] == (int)escalon && (Recluta(t) || (string)t["id"] == actual)).ToList();
                var fila = Dialogo.Caja("territorio-tropa-fila");
                if (PiezasDeTropa.Miniatura(actual, 26) is VisualElement mini) fila.Add(mini);
                fila.Add(Dialogo.Texto($"Escalón {escalon} · {(int)p.Value} escuadra{((int)p.Value == 1 ? "" : "s")}", "plaza-obra-titulo-small"));
                if (opciones.Count > 1)
                {
                    var ids = opciones.Select(t => (string)t["id"]).ToList();
                    var lista = new DropdownField(ids.Select(NombreDeTropa).ToList(), Math.Max(0, ids.IndexOf(actual))) { name = $"tropa-{x.Id}-{escalon}" };
                    lista.AddToClassList("plaza-arm-campo");
                    lista.RegisterValueChangedCallback(async e =>
                    {
                        var i = lista.index;
                        if (i < 0 || ids[i] == actual) return;
                        await OrdenDePlaza(new Orden { ElegirTropaDeAvanzada = new OrdenElegirTropaDeAvanzada { HeroeId = c.HeroeId, AvanzadaId = x.Id, TropaId = ids[i] } }, lista);
                    });
                    fila.Add(lista);
                }
                else fila.Add(Dialogo.Texto(NombreDeTropa(actual), "territorio-nodo-nombre"));
                caja.Add(fila);
            }
            return caja;
        }
    }
}
