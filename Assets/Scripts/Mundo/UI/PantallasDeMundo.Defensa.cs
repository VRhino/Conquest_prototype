// Defender tu plaza (tanda 3 del motor ecfb23d/c94a960; Víctor, 2026-11-16): las escuadras que entran en un asedio a tu plaza.
//   - En tu base hay dos listas: las **disponibles** (las de tu campamento, con soldados y fuera de la guarnición) y las **elegidas** (lo que cabe en tu
//     Liderazgo, contando lo que ya llevas fuera). Entras siempre con las elegidas y en la batalla solo cambias entre ellas.
//   - Arriba en la plaza, «Asedio a tu plaza · Defender» abre este panel; «Defender» se une con la lista (`unirseABatalla.escuadron_ids`).
//   - Ya dentro de la batalla, en la preparación, «Elegir escuadras» abre el mismo panel; «Guardar» cambia la lista (`elegirEscuadrasDeBatalla`)
//     antes de que empiece.
//   - Fuera de tu base no se elige: entra todo lo que llevas (el motor rechaza otra lista).
// Como el riel de tropa de la salida (`VentanaDeSalida`): las tarjetas de las elegidas con su ✕, el «+» solo si algo cabe, y al pulsarlo solo lo que
// cabe. Sin huecos vacíos ni botones apagados.
using System;
using System.Collections.Generic;
using System.Linq;
using BronzeAge.Contract.V1;
using UnityEngine;
using UnityEngine.UIElements;

namespace ConquestTactics.Mundo.UI
{
    public sealed partial class PantallasDeMundo
    {
        /// <summary>¿Es un asedio a la plaza donde reside el héroe? Solo ahí se eligen las escuadras.</summary>
        static bool EnSuBase(Batalla b, Heroe h) =>
            b != null && h != null && h.HasResidenciaId && b.Ticket?.ContextoEstrategico?.Asedio?.AsentamientoId is string sitio && sitio == h.ResidenciaId;

        /// <summary>La batalla del asedio a la plaza `a` a la que el héroe puede unirse a defender (dentro, sin haberse unido aún), o null.</summary>
        Batalla AsedioParaDefender(Asentamiento a, Heroe yo)
        {
            if (a == null || yo == null || !a.HasBatallaId || !yo.HasResidenciaId || yo.ResidenciaId != a.Id) return null;
            if (yo.HasHeridoHastaMs && yo.HeridoHastaMs > c.Sesion.AhoraMundo) return null;
            var b = c.Sesion.Almacen.Buscar(TipoEntidad.Batalla, a.BatallaId)?.Batalla;
            if (b == null || b.HasLadoPropio || !b.HasLadoParaUnirse || b.LadoParaUnirse != LadoId.Defensor) return null;
            return b.Estado == EstadoBatalla.BatallaConvocando || b.Estado == EstadoBatalla.BatallaAsignada ? b : null;
        }

        /// <summary>Arriba en la plaza: «Asedio a tu plaza · Defender», mientras puedas unirte a defenderla.</summary>
        void AvisoDeAsedio(VisualElement v, Asentamiento a, Heroe yo)
        {
            var b = AsedioParaDefender(a, yo);
            var aviso = v.Q("plaza-asedio");
            if (b == null) { aviso?.RemoveFromHierarchy(); return; }
            if (aviso != null && (string)aviso.userData == b.Id) return;
            aviso?.RemoveFromHierarchy();
            aviso = Dialogo.Caja("plaza-asedio");
            aviso.name = "plaza-asedio";
            aviso.userData = b.Id;
            aviso.Add(IconosDelMundo.ConIcono("glifo", "atacar", Dialogo.Texto("Asedio a tu plaza", "plaza-asedio-texto"), 18));
            var defender = Dialogo.Boton("Defender", "boton-primario plaza-asedio-boton", () => VentanaDeDefensa(b.Id, true));
            defender.name = "defender";
            aviso.Add(defender);
            // Del todo encima de la vista (las capas de la plaza, como los carteles de obra, van detrás); se va con ella.
            v.Add(aviso);
        }

        /// <summary>
        /// El panel de las escuadras para defender tu plaza. Con `unirse`, «Defender» se une a la batalla con las elegidas; si no (ya combates en
        /// ella, en la preparación), «Guardar» las cambia.
        /// </summary>
        void VentanaDeDefensa(string batallaId, bool unirse)
        {
            var h = HeroeAhora();
            var a = c.Sesion.Almacen;
            var b = a.Buscar(TipoEntidad.Batalla, batallaId)?.Batalla;
            if (h == null || b == null || capaEncima.Q("ventana-defensa") != null) return;
            var interior = SelectorDePersonaje.Interior;
            if (interior != null) interior.PanelAbierto = true;
            var velo = Dialogo.Caja("dialogo-velo");
            velo.name = "ventana-defensa";
            var placa = Dialogo.Caja("placa salida-ventana defensa-ventana");
            velo.Add(placa);
            void Cerrar() { velo.RemoveFromHierarchy(); if (interior != null) interior.PanelAbierto = false; }
            velo.RegisterCallback<KeyDownEvent>(e => { if (e.keyCode == KeyCode.Escape) Cerrar(); }, TrickleDown.TrickleDown);

            var lugar = a.Buscar(TipoEntidad.Asentamiento, h.ResidenciaId)?.Asentamiento;
            var cabeza = Dialogo.Caja("salida-cab");
            cabeza.Add(Dialogo.Texto($"DEFENDER {(lugar?.HasNombre == true ? lugar.Nombre : "TU PLAZA").ToUpperInvariant()}", "dialogo-titulo salida-titulo"));
            var aspa = Dialogo.Boton("×", "boton-secundario salida-cerrar", Cerrar);
            aspa.name = "cancelar";
            cabeza.Add(aspa);
            placa.Add(cabeza);
            var tropaCaja = Dialogo.Caja("salida-tropa defensa-tropa");
            placa.Add(tropaCaja);
            var error = Dialogo.Texto("", "error oculto");
            error.name = "defensa-error";
            var pie = Dialogo.Caja("salida-acciones defensa-acciones");

            // Las disponibles: las de tu campamento con soldados, fuera de la guarnición y sin candado de otra batalla (las que ya llevan el de esta valen).
            int Coste(Escuadron s) => LiderazgoDe(s.TropaId) ?? 0;
            List<Escuadron> disponibles = null;
            List<string> elegidas = null;
            int tope = 0;
            void Calcular()
            {
                var mias = h.EscuadronesIds.Select(id => a.Buscar(TipoEntidad.Escuadron, id)?.Escuadron).Where(e => e != null).ToList();
                disponibles = mias.Where(e => e.Cantidad > 0 && e.Contenedor?.DondeCase == ContenedorEscuadron.DondeOneofCase.Campamento && !e.EnGuarnicion
                    && (e.ReservaBatalla == null || e.ReservaBatalla.BattleId == b.Id)).ToList();
                // Lo que ya llevas fuera (en una columna) cuenta para el Liderazgo.
                var fuera = mias.Where(e => e.Contenedor?.DondeCase == ContenedorEscuadron.DondeOneofCase.EjercitoId).Sum(Coste);
                tope = (int)h.LiderazgoBase - fuera;
                // Las de partida: al unirte, las de tu loadout activo que estén aquí, mientras quepan; ya dentro, las que llevas en esta batalla.
                var enLaBatalla = MisEscuadras(b).Select(s => s.SquadId).ToList();
                var loadout = h.Loadouts.FirstOrDefault(x => x.Activo);
                elegidas = new List<string>();
                foreach (var s in unirse ? disponibles.Where(s => loadout?.SquadIds.Contains(s.Id) == true) : disponibles.Where(s => enLaBatalla.Contains(s.Id)))
                    if (elegidas.Sum(id => Coste(disponibles.First(x => x.Id == id))) + Coste(s) <= tope) elegidas.Add(s.Id);
            }
            Calcular();
            bool eligiendo = false;
            // Hasta que tocas la lista, sigue a la batalla y al héroe: al abrirla justo al conectar, la batalla aún puede no traer tus escuadras.
            bool tocada = false;

            void Pintar()
            {
                tropaCaja.Clear();
                pie.Clear();
                var lista = elegidas.Select(id => disponibles.First(s => s.Id == id)).ToList();
                var usado = lista.Sum(Coste);
                var caben = disponibles.Where(s => !elegidas.Contains(s.Id) && Coste(s) <= tope - usado).ToList();

                var cab = Dialogo.Caja("salida-tropa-cab");
                cab.Add(ConAyuda(Dialogo.Texto("ELEGIDAS", "salida-recuadro-titulo"),
                    "Entras en la batalla con estas, y en ella solo cambias entre ellas. Las eliges entre las de tu campamento, mientras quepan en tu Liderazgo "
                    + "(cuenta lo que ya llevas fuera). Vale solo para esta batalla: tu loadout no cambia."));
                var lid = Dialogo.Caja("defensa-liderazgo");
                lid.name = "liderazgo";
                lid.Add(Dialogo.Caja("prep-liderazgo-icono defensa-icono"));
                lid.Add(Dialogo.Texto($"{usado} / {tope}", "salida-liderazgo"));
                cab.Add(lid);
                tropaCaja.Add(cab);

                // Las elegidas, con su ✕ (si queda al menos otra), y el «+» solo si algo más cabe.
                var riel = Dialogo.Caja("salida-riel defensa-riel fila-envuelta");
                riel.name = "elegidas";
                foreach (var s in lista)
                {
                    var hueco = Dialogo.Caja("salida-hueco salida-hueco-tropa");
                    hueco.name = "elegida-" + s.Id;
                    hueco.tooltip = $"{(string.IsNullOrEmpty(s.Nombre) ? NombreDeTropa(s.TropaId) : s.Nombre)} · {s.Cantidad} hombres";
                    var total = (int?)c.Datos.TropasReclutables?.FirstOrDefault(x => (string)x["id"] == s.TropaId)?["unidadesPorDefecto"];
                    if (PiezasDeTropa.Tarjeta(s.TropaId, Coste(s), (int)s.Cantidad, total, null, AnchoDeHueco) is VisualElement tarjeta)
                    {
                        tarjeta.style.marginRight = 0;
                        tarjeta.style.marginBottom = 0;
                        hueco.Add(tarjeta);
                    }
                    if (lista.Count > 1)
                    {
                        var id = s.Id;
                        var quitar = Dialogo.Boton("✕", "salida-hueco-quitar", () => { elegidas.Remove(id); tocada = true; Pintar(); });
                        quitar.name = "quitar-" + id;
                        quitar.tooltip = "Dejarla fuera de esta batalla";
                        hueco.Add(quitar);
                    }
                    riel.Add(hueco);
                }
                if (caben.Count > 0)
                {
                    var mas = Dialogo.Boton("+", "salida-hueco salida-hueco-mas", () => { eligiendo = !eligiendo; Pintar(); });
                    mas.name = "defensa-mas";
                    mas.tooltip = "Añadir una escuadra";
                    mas.EnableInClassList("activo", eligiendo);
                    riel.Add(mas);
                }
                tropaCaja.Add(riel);

                // Las disponibles que caben, al pulsar «+» (lo que no cabe no se ofrece).
                if (eligiendo && caben.Count > 0)
                {
                    var elegir = Dialogo.Caja("salida-elegir fila-envuelta");
                    elegir.name = "disponibles";
                    foreach (var s in caben)
                    {
                        var id = s.Id;
                        var candidata = new Button(() => { elegidas.Add(id); eligiendo = false; tocada = true; Pintar(); }) { name = "anadir-" + id };
                        candidata.AddToClassList("salida-candidata");
                        if (PiezasDeTropa.Miniatura(s.TropaId, 32) is VisualElement mini) candidata.Add(mini);
                        var textos = new VisualElement();
                        textos.Add(Dialogo.Texto(string.IsNullOrEmpty(s.Nombre) ? NombreDeTropa(s.TropaId) : s.Nombre, "aviso-titulo"));
                        var linea = Dialogo.Caja("con-icono");
                        linea.Add(Dialogo.Texto($"{s.Cantidad} hombres · ", "campo-ayuda"));
                        linea.Add(Dialogo.Caja("prep-liderazgo-icono defensa-icono-chico"));
                        linea.Add(Dialogo.Texto(Coste(s).ToString(), "campo-ayuda"));
                        textos.Add(linea);
                        candidata.Add(textos);
                        elegir.Add(candidata);
                    }
                    tropaCaja.Add(elegir);
                }
                // Las que se quedan fuera (no caben en lo que queda): se ven, sin nada que hacer con ellas.
                var fueraDeLista = disponibles.Where(s => !elegidas.Contains(s.Id) && !caben.Contains(s)).ToList();
                if (fueraDeLista.Count > 0)
                {
                    var quedan = Dialogo.Caja("defensa-fuera");
                    quedan.name = "se-quedan";
                    quedan.Add(Dialogo.Texto("Se quedan en el campamento: " + string.Join(", ", fueraDeLista.Select(s => string.IsNullOrEmpty(s.Nombre) ? NombreDeTropa(s.TropaId) : s.Nombre)) + ".", "campo-ayuda"));
                    tropaCaja.Add(quedan);
                }
                if (disponibles.Count == 0) tropaCaja.Add(Dialogo.Texto("No tienes tropa libre en el campamento: defiendes solo con tu héroe.", "campo-ayuda"));

                pie.Add(error);
                Button accion = null;
                accion = Dialogo.Boton(unirse ? "DEFENDER" : "GUARDAR", "boton-primario defensa-boton", async () =>
                {
                    accion.SetEnabled(false);
                    Poner(error, null);
                    Orden orden;
                    if (unirse)
                    {
                        var o = new OrdenUnirseABatalla { HeroeId = h.Id, BattleId = b.Id };
                        o.EscuadronIds.AddRange(elegidas);
                        orden = new Orden { UnirseABatalla = o };
                    }
                    else
                    {
                        var o = new OrdenElegirEscuadrasDeBatalla { BattleId = b.Id };
                        o.EscuadronIds.AddRange(elegidas);
                        orden = new Orden { ElegirEscuadrasDeBatalla = o };
                    }
                    try
                    {
                        var r = await c.Sesion.Ordenar(orden);
                        if (r.Resultado == Resultado.OrdenAceptada || r.Resultado == Resultado.OrdenSinCambios) { Cerrar(); return; }
                        Poner(error, Textos.Explicar(r.Rechazo));
                    }
                    catch (ErrorDeSesion x) { Poner(error, x.Message); }
                    if (accion.panel != null) accion.SetEnabled(true);
                });
                accion.name = unirse ? "defensa-defender" : "defensa-guardar";
                // Con tropa en el campamento, al menos una elegida (una lista vacía el motor la toma por «lo de siempre»: tu loadout activo); sin
                // tropa, defiendes solo con tu héroe.
                if (elegidas.Count > 0 || (unirse && disponibles.Count == 0)) pie.Add(accion);
            }
            Pintar();
            placa.Add(pie);
            capaEncima.Add(velo);
            // Lo que cambia en la batalla o en tu héroe se repinta solo mientras no hayas tocado la lista (como la convocatoria).
            string Firma() => $"{a.Buscar(TipoEntidad.Batalla, batallaId)?.Batalla?.Version}|{HeroeAhora()?.Version}";
            var firma = Firma();
            velo.schedule.Execute(() =>
            {
                if (tocada || Firma() == firma) return;
                firma = Firma();
                h = HeroeAhora() ?? h;
                b = a.Buscar(TipoEntidad.Batalla, batallaId)?.Batalla ?? b;
                Calcular();
                Pintar();
            }).Every(RepasoDeVentanaMs);
        }

        /// <summary>Cada cuánto mira una ventana abierta si cambió lo que enseña (el de la convocatoria).</summary>
        const long RepasoDeVentanaMs = 500;
    }
}
