// Los Aedas (paso 13 del bloque 4, tanda 1), calcados de BronzeAgeClient (`ui/aedas.ts`, `ui/ventanaAedas.ts` y `mundo/aedas.ts`):
// - La ficha del itinerante en el mapa: quién es, dónde está, qué hace y, parado en una plaza de tu Facción, lo que os vende. Solo para mirar: se
//   compra dentro de la plaza (Víctor, 2a: un cuadro hasta tener los modelos; el parado, junto a su plaza).
// - La ventana Aedas del riel de la plaza, que solo sale en una plaza tuya con algún Aeda dentro: el itinerante parado, con lo que os vende y
//   «Comprar» (el Rey o el Gobernador, presentes; paga el almacén de la plaza); los residentes, con lo que dan, su épica en curso y «Abandonar», o
//   las que pueden cantar y «Que la cante» (el Rey, el Gobernador o el Sacerdote, presentes).
// Solo sale lo que el jugador puede hacer; el motor valida todo y su motivo sale tal cual.
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
        // ─────────────── lo común ───────────────

        List<AedaItinerante> ItinerantesEn(string plazaId) =>
            c.Sesion.Almacen.Todas(Entidad.CuerpoOneofCase.AedaItinerante).Select(e => e.AedaItinerante).Where(x => x.HasEnAsentamientoId && x.EnAsentamientoId == plazaId).ToList();

        List<AedaResidente> ResidentesEn(string plazaId) =>
            c.Sesion.Almacen.Todas(Entidad.CuerpoOneofCase.AedaResidente).Select(e => e.AedaResidente).Where(x => x.AsentamientoId == plazaId).ToList();

        /// <summary>¿Sale la lira en el riel? Plaza de tu Facción con algún Aeda dentro (`hayAedasEnPlaza`).</summary>
        bool HayAedasEnPlaza(Asentamiento a, Heroe yo) => Propia(a, yo) && (ItinerantesEn(a.Id).Count + ResidentesEn(a.Id).Count) > 0;

        /// <summary>Las tecnologías reveladas que un Aeda os vende, con su precio en oro (`precioDeAeda`): solo hasta la Era que vende.</summary>
        List<(string id, long precio)> LoQueVende(Heroe yo)
        {
            var T = ArbolDeTecnologiasDelBalance();
            var t = yo.HasFaccionId ? c.Sesion.Almacen.Buscar(TipoEntidad.TecnologiaFaccion, yo.FaccionId)?.Tecnologias : null;
            if (T == null || t == null) return new List<(string, long)>();
            return t.Reveladas.Where(T.Nodos.ContainsKey).Select(id => (id, PrecioDeAeda(T, id))).Where(x => x.Item2 != null).Select(x => (x.id, x.Item2.Value)).ToList();
        }

        string FirmaDeAedas(Asentamiento a, Heroe yo) =>
            string.Join(",", ItinerantesEn(a.Id).Select(x => x.Version)) + "|" + string.Join(",", ResidentesEn(a.Id).Select(x => x.Version))
            + "|" + (yo.HasFaccionId ? c.Sesion.Almacen.Buscar(TipoEntidad.TecnologiaFaccion, yo.FaccionId)?.Tecnologias?.Version : 0) + "|" + (long)(AhoraMs / 60_000);

        static VisualElement ConLira(VisualElement texto, float lado = 18) => IconosDelMundo.ConIcono("riel", "aedas", texto, lado, IconosDelMundo.Bronce);

        // ─────────────── la ficha del mapa ───────────────

        /// <summary>Lo de la ficha de un Aeda itinerante en el mapa (`htmlFichaAeda`), debajo de sus datos: qué hace y, parado en una plaza de tu
        /// Facción, lo que os vende, con su precio. Para mirar: se le compra dentro de la plaza, con la ventana Aedas.</summary>
        void FichaDeAeda(VisualElement datos, CosaDelMapa cosa)
        {
            var aeda = c.Sesion.Almacen.Buscar(TipoEntidad.Aeda, cosa.Id)?.AedaItinerante;
            var yo = HeroeAhora();
            if (aeda == null || yo == null) return;
            var estancia = (double?)BloqueDeDatos("AEDAS")?["itinerantes"]?["estanciaMinutos"];
            var texto = Dialogo.Texto($"Va de plaza en plaza cantando lo que se descubre en el mundo{(estancia != null ? $" y se queda {Duracion(Math.Round(estancia.Value / Math.Max(1e-6, c.Sesion.FactorReloj)))} en cada una" : "")}. Al pararse en una plaza le revela a su Facción las tecnologías que conoce y le faltan, y se las vende. No se le puede atacar ni capturar.", "campo-ayuda ficha-aeda-texto");
            datos.Add(texto);
            if (!aeda.HasEnAsentamientoId || !(c.Sesion.Almacen.Buscar(TipoEntidad.Asentamiento, aeda.EnAsentamientoId)?.Asentamiento is Asentamiento plaza) || !Propia(plaza, yo)) return;
            var vende = LoQueVende(yo);
            if (vende.Count == 0) { datos.Add(Dialogo.Texto("Ha revelado a tu Facción todo lo que sabe que os falta: no tiene nada más que venderos.", "campo-ayuda ficha-aeda-texto")); return; }
            datos.Add(Dialogo.Texto("LO QUE OS VENDE", "kicker ficha-aeda-kicker"));
            foreach (var (id, precio) in vende)
            {
                var fila = Dialogo.Caja("aedas-fila");
                fila.Add(Dialogo.Texto(NombreDeTecnologia(id), "aedas-fila-nombre"));
                fila.Add(IconosDelMundo.ChipDeRecurso("oro", Miles(precio), "Lo que cobra, del almacén de la plaza"));
                datos.Add(fila);
            }
            datos.Add(Dialogo.Texto($"Se le compra dentro de {NombreDePlaza(plaza.Id)}, en la ventana Aedas.", "campo-ayuda ficha-aeda-texto"));
        }

        // ─────────────── la ventana de la plaza ───────────────

        /// <summary>La ventana Aedas (`htmlVentanaAedas`): el itinerante parado con lo que vende y los residentes con sus épicas.</summary>
        void VentanaAedas(VisualElement cuerpo, Asentamiento a, Heroe yo, Action rehacer)
        {
            var itinerantes = ItinerantesEn(a.Id);
            var residentes = ResidentesEn(a.Id);
            var f = MiFaccion(yo);
            var aqui = Presente(a, yo);
            var rey = f != null && f.HasReyId && f.ReyId == yo.Id;
            var gobernador = TitularDe(a, "gobernador") == yo.Id;
            var compra = aqui && (rey || gobernador);
            var epica = aqui && (rey || gobernador || TitularDe(a, "sacerdote") == yo.Id);
            var aedas = BloqueDeDatos("AEDAS");
            var error = Dialogo.Texto("", "error oculto");
            error.name = "aedas-error";
            void Error(string t) => Poner(error, t);
            string EnReal(double minutosDeMundo) => Duracion(Math.Round(minutosDeMundo / Math.Max(1e-6, c.Sesion.FactorReloj)));

            if (itinerantes.Count > 0)
            {
                var seccion = Dialogo.Caja("aedas-seccion");
                seccion.name = "aedas-itinerante";
                var estancia = (double?)aedas?["itinerantes"]?["estanciaMinutos"];
                var venta = aedas?["venta"];
                var nombres = string.Join(" y ", itinerantes.Select(x => CosasDelMapa.NombreDeAeda(x.Id)));
                var titulo = Dialogo.Caja("aedas-titulo");
                titulo.Add(IconosDelMundo.Icono("glifo", "aeda", 20));
                titulo.Add(Dialogo.Texto(nombres, "aedas-titulo-texto"));
                titulo.Add(Dialogo.Texto(itinerantes.Count > 1 ? "Aedas itinerantes" : "Aeda itinerante", "aedas-titulo-sub"));
                seccion.Add(ConAyuda(titulo, $"Va de plaza en plaza cantando lo que se descubre en el mundo{(estancia != null ? $" y se queda {EnReal(estancia.Value)} en cada una" : "")}. Al pararse revela a tu Facción las tecnologías que conoce y os faltan. Comprarle una os salta el hito de la Facción, no el logro del mundo; luego hay que adoptarla en Facción › Tecnología. "
                    + (venta != null ? $"Cobra {(double?)venta["factorOro"]} veces el oro de la tarifa de la Era de la tecnología (hasta la Era {(int?)venta["ordenEraMaximo"]}), del almacén de la plaza." : "Cobra en oro, del almacén de la plaza.")));
                seccion.Add(Dialogo.Texto($"Parado en la plaza{(estancia != null ? $" (se queda {EnReal(estancia.Value)} en cada plaza)" : "")}. Lo que os vende:", "campo-ayuda"));
                var vende = LoQueVende(yo);
                var oro = Hay(a, "oro");
                if (vende.Count == 0) seccion.Add(Dialogo.Texto("Ha revelado a tu Facción todo lo que sabe que os falta: no tiene nada más que venderos.", "campo-ayuda"));
                foreach (var (id, precio) in vende)
                {
                    var fila = Dialogo.Caja("aedas-fila");
                    fila.name = "aedas-venta-" + id;
                    var nombre = Dialogo.Caja("aedas-fila-cuerpo");
                    nombre.Add(Dialogo.Texto(NombreDeTecnologia(id), "aedas-fila-nombre"));
                    nombre.Add(Dialogo.Texto("Revelada: la descubrió otra Facción", "aedas-fila-sub"));
                    fila.Add(nombre);
                    var chip = IconosDelMundo.ChipDeRecurso("oro", Miles(precio), "Lo que cobra, del almacén de la plaza");
                    chip.EnableInClassList("aedas-falta", oro < precio);
                    fila.Add(chip);
                    // Solo lo que se puede hacer: el Rey o el Gobernador, aquí, con el oro en el almacén.
                    if (compra && oro >= precio)
                    {
                        var tec = id;
                        Button comprar = null;
                        comprar = Dialogo.Boton("Comprar", "boton-primario aedas-boton", async () =>
                        {
                            var r = await OrdenDelPanel(new Orden { ComprarTecnologiaAeda = new OrdenComprarTecnologiaAeda { AsentamientoId = a.Id, TecnologiaId = tec } }, Error, rehacer, comprar);
                            if (r?.Resultado == Resultado.OrdenAceptada)
                                c.Avisos.Anotar(new EntradaAviso { Titulo = "Aedas", Texto = "Compra hecha: la tecnología ya es de tu Facción. Adóptala en Facción › Tecnología.", Clase = ClaseAviso.Mirada, Categoria = CategoriaAviso.Aedas });
                        });
                        comprar.name = "aedas-comprar-" + id;
                        fila.Add(comprar);
                    }
                    seccion.Add(fila);
                }
                if (compra && vende.Count > 0)
                    seccion.Add(IconosDelMundo.ConIcono("recurso", "oro", Dialogo.Texto($"Almacén de la plaza: {Miles(oro)} de oro", "campo-ayuda"), 14));
                cuerpo.Add(seccion);
            }

            if (residentes.Count > 0)
            {
                var seccion = Dialogo.Caja("aedas-seccion");
                seccion.name = "aedas-residentes";
                var res = aedas?["residentes"];
                string Pct(JToken x) => x == null ? "" : $"{Math.Round((double)x * 100)} %";
                var cupos = res?["cupoPorNivel"] is JObject cpn ? string.Join(", ", cpn.Properties().OrderBy(p => int.TryParse(p.Name, out var n) ? n : 0).Select(p => $"{p.Value} en nivel {p.Name}")) : "";
                var da = res != null ? $"Cada Aeda residente da +{Pct(res["bonoFelicidad"])} de felicidad a la plaza (crece más su población) y +{Pct(res["bonoNobleza"])} al crecimiento de la nobleza."
                    : "Un Aeda residente da felicidad y crecimiento de nobleza a la plaza.";
                var titulo = Dialogo.Caja("aedas-titulo");
                titulo.Add(IconosDelMundo.Icono("riel", "aedas", 20, IconosDelMundo.Bronce));
                titulo.Add(Dialogo.Texto(residentes.Count > 1 ? "Aedas residentes" : "Aeda residente", "aedas-titulo-texto"));
                seccion.Add(ConAyuda(titulo, (res != null ? $"Viven en plazas con Palacio activo y algo de nobleza: caben {cupos}. Con sitio libre llega uno cada {EnReal((double?)res["llegadaMinutos"] ?? 0)}. " : "")
                    + "Cuentan para el título «Mecenas de los Aedas»: épicas cumplidas × 10 más residentes."));
                seccion.Add(Dialogo.Texto(da, "campo-ayuda"));
                var enfriamiento = (double?)aedas?["epica"]?["enfriamientoMinutos"];
                seccion.Add(ConAyuda(Dialogo.Texto("ÉPICAS", "kicker aedas-kicker"),
                    $"Una épica sustituye al hito de la Facción (no al logro del mundo), no cuesta oro y avanza con hechos de la plaza o de la Facción, {(enfriamiento != null ? $"uno cada {EnReal(enfriamiento.Value)} de mundo como mucho" : "con un enfriamiento entre uno y otro")}. Al cumplirla la tecnología aparece y se adopta como siempre. Solo se ofrecen las tecnologías con logro cumplido, Era alcanzada y que ningún Aeda vuestro canta ya."));
                var epicas = BloqueDeDatos("EPICAS");
                var titulosCapitulo = BloqueDeDatos("TITULO_CAPITULO");
                var posibles = yo.HasFaccionId ? c.Sesion.Almacen.Buscar(TipoEntidad.TecnologiaFaccion, yo.FaccionId)?.Tecnologias?.EpicasPosibles.ToList() ?? new List<string>() : new List<string>();
                foreach (var r in residentes)
                {
                    var caja = Dialogo.Caja("aedas-residente");
                    caja.name = "aedas-residente-" + r.Id;
                    var cab = Dialogo.Caja("aedas-fila");
                    var nombre = Dialogo.Caja("aedas-fila-cuerpo");
                    nombre.Add(Dialogo.Texto(r.Nombre, "aedas-fila-nombre"));
                    var ep = r.Epica;
                    if (ep != null)
                    {
                        var nombreEpica = (string)epicas?[ep.TecnologiaId]?["nombre"] ?? NombreDeTecnologia(ep.TecnologiaId);
                        nombre.Add(Dialogo.Texto($"Canta «{nombreEpica}», la épica de {NombreDeTecnologia(ep.TecnologiaId)}", "aedas-fila-sub"));
                        cab.Add(nombre);
                        if (epica)
                        {
                            var aedaId = r.Id;
                            Button abandonar = null;
                            abandonar = Dialogo.Boton("Abandonar", "boton-secundario aedas-boton", async () =>
                            {
                                if (!await dialogo.Abrir("Se pierde todo lo que lleva cantado de la épica. Podrá empezar otra después.", "¿Abandonar la épica?", "Abandonar", "Volver", true)) return;
                                await OrdenDelPanel(new Orden { AbandonarEpica = new OrdenAbandonarEpica { AsentamientoId = a.Id, AedaId = aedaId } }, Error, rehacer, abandonar);
                            });
                            abandonar.name = "aedas-abandonar-" + r.Id;
                            cab.Add(abandonar);
                        }
                        caja.Add(cab);
                        var avance = (ep.Capitulo + (ep.HechosNecesarios > 0 ? (double)ep.Hechos / ep.HechosNecesarios : 0)) / Math.Max(1, ep.Capitulos);
                        var barra = Dialogo.Caja("merc-barra aedas-barra");
                        var i = Dialogo.Caja("merc-barra-i");
                        i.style.width = Length.Percent((float)(100 * Math.Min(1, avance)));
                        barra.Add(i);
                        caja.Add(barra);
                        var hecho = (string)epicas?[ep.TecnologiaId]?["capitulos"]?[(int)ep.Capitulo]?["hecho"];
                        var tituloCap = (hecho != null ? (string)titulosCapitulo?[hecho] : null) ?? hecho ?? "";
                        var espera = ep.HasUltimoHechoEnMs && enfriamiento != null ? enfriamiento.Value * 60_000 - (AhoraMs - ep.UltimoHechoEnMs) : 0;
                        caja.Add(Dialogo.Texto($"Capítulo {ep.Capitulo + 1} de {ep.Capitulos}{(tituloCap != "" ? $": {tituloCap}" : "")} · {ep.Hechos} / {ep.HechosNecesarios} hechos"
                            + (espera > 0 ? $" · otro hecho cuenta en {EnTiempoReal(espera)}" : ep.HasUltimoHechoEnMs ? " · ya puede contar otro hecho" : ""), "aedas-fila-sub"));
                    }
                    else
                    {
                        nombre.Add(Dialogo.Texto("Sin épica en curso", "aedas-fila-sub"));
                        cab.Add(nombre);
                        caja.Add(cab);
                        // «Que la cante», solo a quien dirige los Aedas aquí; sin nada que cantar, la línea de que no hay.
                        if (epica)
                            foreach (var tec in posibles)
                            {
                                var fila = Dialogo.Caja("aedas-fila aedas-opcion");
                                fila.name = $"aedas-epica-{r.Id}-{tec}";
                                var cuerpoFila = Dialogo.Caja("aedas-fila-cuerpo");
                                var capitulos = (epicas?[tec]?["capitulos"] as JArray)?.Count;
                                cuerpoFila.Add(Dialogo.Texto((string)epicas?[tec]?["nombre"] ?? NombreDeTecnologia(tec), "aedas-fila-nombre"));
                                cuerpoFila.Add(Dialogo.Texto($"Épica de {NombreDeTecnologia(tec)}{(capitulos != null ? $" · {capitulos} capítulos" : "")}", "aedas-fila-sub"));
                                fila.Add(cuerpoFila);
                                var aedaId = r.Id;
                                var t0 = tec;
                                Button cantar = null;
                                cantar = Dialogo.Boton("Que la cante", "boton-primario aedas-boton", async () =>
                                    await OrdenDelPanel(new Orden { EmpezarEpica = new OrdenEmpezarEpica { AsentamientoId = a.Id, AedaId = aedaId, TecnologiaId = t0 } }, Error, rehacer, cantar));
                                cantar.name = $"aedas-cantar-{r.Id}-{tec}";
                                fila.Add(cantar);
                                caja.Add(fila);
                            }
                        if (posibles.Count == 0) caja.Add(Dialogo.Texto("Ahora no hay ninguna tecnología con épica posible.", "aedas-fila-sub"));
                    }
                    seccion.Add(caja);
                }
                cuerpo.Add(seccion);
            }
            cuerpo.Add(error);
        }
    }
}
