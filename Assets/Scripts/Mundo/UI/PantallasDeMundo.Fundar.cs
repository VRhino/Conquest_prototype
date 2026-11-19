// Fundar asentamiento desde el mundo (Víctor, 2026-11-18, duda 38: tanda 1), calcado de BronzeAgeClient: el panel «fundar» del riel del mapa
// (`renderPanelRiel` de `main.ts`), el resumen de lo que hay a tu alcance (`resumenRecursosFundacion` de `ui/pestanaAsentamientos.ts`), «Fundar aquí»
// (`fundarAqui`) y «Enganchar caravana» (`adjuntarCaravana`).
// - El botón de la casa, en el riel del mapa junto a ⚑ Ejército (con tu columna fuera), abre y cierra el panel.
// - Sin Caravana de Fundación: se funda con una. Tu ejército la lleva y no eres su Líder: funda él. La llevas tú: se funda donde está la columna, con
//   los recursos a tu alcance y «Fundar aquí». La tuya espera en su campamento o su plaza: «Enganchar caravana».
// - El motor valida todo (agua, pendiente, bosque cerrado, un campamento o una plaza cerca); su motivo sale tal cual. Fundar te deja dentro.
// La vista previa en el mapa (la zona, los sitios de aldea y las parcelas) son las tandas 2 (3D) y 3 (2D).
using System;
using System.Collections.Generic;
using System.Linq;
using BronzeAge.Contract.V1;
using ConquestTactics.Mundo.Mapa;
using UnityEngine.UIElements;

namespace ConquestTactics.Mundo.UI
{
    public partial class PantallasDeMundo
    {
        bool ventanaDeFundar;
        string firmaDeFundar;
        Relieve relieveDeFundar;

        /// <summary>El botón de la casa en el riel del mapa (`.mapa-riel`), que abre y cierra el panel de fundar.</summary>
        void RielDeFundar(VisualElement v)
        {
            var b = new Button(() => { ventanaDeFundar = !ventanaDeFundar; firmaDeFundar = null; PintarVentanaDeFundar(v); })
                { name = "riel-fundar", tooltip = "Fundar asentamiento: donde está tu columna, con una Caravana de Fundación" };
            b.AddToClassList("mapa-riel-boton");
            b.Add(IconosDelMundo.Icono("simbolo", "casa", 22, IconosDelMundo.OroClaro));
            v.Q("mapa-riel").Add(b);
        }

        /// <summary>La Caravana de Fundación de la que eres titular (`caravanaDeFundacion`): la llevas tú y fundas tú.</summary>
        Caravana CaravanaDeFundacionPropia(Heroe h) => c.Sesion.Almacen.Todas(Entidad.CuerpoOneofCase.Caravana).Select(e => e.Caravana)
            .FirstOrDefault(k => k.Tipo == TipoCaravana.CaravanaDeFundacion && k.HasTitularId && k.TitularId == h.Id);

        /// <summary>El panel de fundar, si está abierto (`renderPanelRiel`, rama «fundar»): se rehace cuando cambian tu columna, tu héroe o tu caravana.</summary>
        void PintarVentanaDeFundar(VisualElement v)
        {
            var col = MiColumna();
            var h = HeroeAhora();
            v.Q<Button>("riel-fundar")?.EnableInClassList("activo", ventanaDeFundar && col != null);
            var placa = v.Q("ventana-fundar");
            if (!ventanaDeFundar || col == null || h == null)
            {
                placa?.RemoveFromHierarchy();
                firmaDeFundar = null;
                if (mapa != null) mapa.Fundacion = null;
                if (mapa2D?.Fundacion != null) mapa2D.Fundacion = null;
                return;
            }
            var a = c.Sesion.Almacen;
            // La de fundación va en `CaravanaFundacionId` de la columna; si no, la tuya, esté donde esté.
            var llevada = col.HasCaravanaFundacionId ? col.CaravanaFundacionId : null;
            var caravana = (llevada != null ? a.Buscar(TipoEntidad.Caravana, llevada)?.Caravana : null) ?? CaravanaDeFundacionPropia(h);
            var enganchada = llevada != null || caravana != null && col.CaravanasAdjuntasIds.Contains(caravana.Id);
            var esLider = string.IsNullOrEmpty(col.LiderId) || col.LiderId == h.Id;
            var pos = Instante.Posicion(col.Plan, col.Posicion, AhoraMs);
            var donde = (x: Math.Round(pos.x / 1000.0, 1), y: Math.Round(pos.y / 1000.0, 1));
            var firma = $"{col.Version}|{h.Version}|{caravana?.Id}:{caravana?.Version}|{donde.x},{donde.y}";
            if (placa != null && firma == firmaDeFundar) return;
            firmaDeFundar = firma;
            var errorAntes = placa?.Q<Label>("fundar-error")?.text;
            placa?.RemoveFromHierarchy();
            // Lo de alrededor de la columna, para el resumen y para la vista previa del mapa (tanda 2): con el panel abierto, centrada en ella.
            var alrededor = AlrededorAlFundar(donde);
            if (mapa != null) mapa.Fundacion = new ConquestTactics.Mundo.Mapa.MapaDeMundo.FundacionEnVista
            {
                X = donde.x, Y = donde.y, Radio = alrededor.radio, Paso = SitiosDeAldea.Paso,
                Validos = alrededor.sitios?.Validos ?? new List<(double, double)>(), Cerca = alrededor.sitios?.Cerca ?? new List<(double, double)>(),
                Parcelas = alrededor.parcelas.Select(q => (q.x, q.y, q.libre)).ToList(),
            };
            if (mapa2D != null) mapa2D.Fundacion = mapa?.Fundacion; // la misma en el mapa 2D (tanda 3)

            placa = Dialogo.Caja("placa ventana-fundar");
            placa.name = "ventana-fundar";
            var filete = Dialogo.Caja("placa-filete");
            placa.Add(filete);
            var cab = Dialogo.Caja("ventana-puerta-cab");
            var ayuda = caravana == null && !enganchada
                ? "Una Facción sin plaza la compra en un campamento de mercenarios con el fondo de sus héroes; con plaza, se lanza desde Centro urbano › Fundación. Quien la compra o la lanza la lleva."
                : enganchada ? "Se funda donde está ahora tu columna, no en agua ni a menos de 100 de un campamento." : null;
            cab.Add(TituloDeSeccion("Fundar asentamiento", ayuda));
            var cerrar = Dialogo.Boton("×", "aviso-borrar", () => { ventanaDeFundar = false; PintarVentanaDeFundar(v); });
            cerrar.name = "fundar-cerrar";
            cab.Add(cerrar);
            filete.Add(cab);
            var error = Dialogo.Texto("", "error oculto");
            error.name = "fundar-error";
            void Error(string m) => Poner(error, m);

            if (caravana == null && !enganchada)
            {
                var t = Dialogo.Texto("Se funda con una Caravana de Fundación.", "dialogo-texto");
                t.name = "fundar-sin-caravana";
                filete.Add(t);
            }
            else if (enganchada && !esLider)
                filete.Add(Dialogo.Texto($"Tu ejército lleva la Caravana de Fundación. Funda su Líder, {Textos.NombreDeHeroe(a, col.LiderId)}, donde esté la columna.", "dialogo-texto"));
            else if (enganchada)
            {
                filete.Add(Dialogo.Texto("Se funda donde está ahora tu columna. Estos son los recursos a tu alcance:", "dialogo-texto"));
                filete.Add(RecursosAlFundar(donde, alrededor));
                Button fundar = null;
                fundar = Dialogo.Boton("Fundar aquí", "boton-primario fundar-aqui", async () =>
                {
                    fundar.SetEnabled(false);
                    // El motor dice el porqué (agua, campamento o plaza demasiado cerca…). Fundar te deja dentro: el mapa abre la plaza nueva.
                    if (await OrdenConError(new Orden { Fundar = new OrdenFundar() }, Error)) { ventanaDeFundar = false; PintarVentanaDeFundar(v); }
                    else fundar.SetEnabled(true);
                });
                fundar.name = "fundar-aqui";
                filete.Add(fundar);
            }
            else
            {
                filete.Add(Dialogo.Texto($"Tu Caravana de Fundación espera en {(caravana.HasOrigenCampamentoId ? "su campamento" : "su plaza")}. Para llevártela, sal de allí como ejército y, al partir, elige llevarla; o lleva tu ejército a la puerta y engánchala.", "dialogo-texto"));
                Button enganchar = null;
                enganchar = Dialogo.Boton("Enganchar caravana", "boton-primario", async () =>
                {
                    enganchar.SetEnabled(false);
                    if (await OrdenConError(new Orden { AdjuntarCaravana = new OrdenAdjuntarCaravana { EjercitoId = col.Id, CaravanaId = caravana.Id, HeroeId = h.Id } }, Error)) firmaDeFundar = null;
                    else enganchar.SetEnabled(true);
                });
                enganchar.name = "fundar-enganchar";
                filete.Add(enganchar);
            }
            filete.Add(error);
            if (!string.IsNullOrEmpty(errorAntes)) Error(errorAntes);
            v.Add(placa);
        }

        /// <summary>Lo que hay a tu alcance si fundas en `donde` (`resumenRecursosFundacion`): pendiente y bosque cerrado (donde el motor no deja), la
        /// tierra para la aldea agrícola, las parcelas de bosque de la zona inicial, los nodos de recurso dentro de ella y el acceso a bosque.</summary>
        /// <summary>Lo que rodea a `donde` si se funda allí: el radio de la zona inicial, los sitios de aldea y las parcelas de bosque dentro (libres o
        /// no: ocupadas por una Leñera o en la zona de cualquier plaza, también tuya, que la plaza nueva no puede tomar).</summary>
        (double radio, SitiosDeAldea sitios, List<(double x, double y, bool libre)> parcelas) AlrededorAlFundar((double x, double y) donde)
        {
            var radio = c.Datos.Numero("reglas.territorio.radio_inicial") / 1000;
            var zona = Enumerable.Range(0, 48).Select(i => (donde.x + radio * Math.Cos(i * Math.PI / 24), donde.y + radio * Math.Sin(i * Math.PI / 24))).ToList();
            var ocupadas = FuentesOcupadas();
            var zonas = c.Sesion.Almacen.Todas(Entidad.CuerpoOneofCase.Asentamiento).Select(e => e.Asentamiento).Where(x => x.Zona.Count > 2).Select(ZonaDe).ToList();
            var parcelas = c.Artefacto.Parcelas.Where(q => CosasDelMapa.Distancia(q.Centro.X / 1000.0, q.Centro.Y / 1000.0, donde.x, donde.y) <= radio)
                .Select(q => (q.Centro.X / 1000.0, q.Centro.Y / 1000.0, !ocupadas.Contains(q.Id) && !zonas.Any(z => SitiosDeAldea.Dentro(q.Centro.X / 1000.0, q.Centro.Y / 1000.0, z))))
                .ToList();
            return (radio, SitiosAlFundar(donde, zona), parcelas);
        }

        VisualElement RecursosAlFundar((double x, double y) donde, (double radio, SitiosDeAldea sitios, List<(double x, double y, bool libre)> parcelas) alrededor)
        {
            var caja = Dialogo.Caja("fundar-recursos");
            caja.name = "fundar-recursos";
            void Puerta(bool ok, string texto) => caja.Add(Dialogo.Texto(texto, "fundacion-gate " + (ok ? "ok" : "falta")));
            var art = c.Artefacto;
            var radio = alrededor.radio;
            var fundacion = c.Datos.Bloque("Economia_Construccion", "FUNDACION");
            // Ni en pendiente fuerte (`fundacion.pendiente`): más de `pendienteMaximaParaFundar` (0,3 m/m, unos 17°), con 12 m por unidad.
            relieveDeFundar ??= new Relieve(c.Relieve);
            var d = relieveDeFundar.Paso;
            double H(double x, double y) => relieveDeFundar.AlturaEn(x, y);
            var pendiente = Math.Sqrt(Math.Pow(H(donde.x + d, donde.y) - H(donde.x - d, donde.y), 2) + Math.Pow(H(donde.x, donde.y + d) - H(donde.x, donde.y - d), 2)) / (2 * d * 12);
            if (pendiente > ((double?)fundacion?["pendienteMaximaParaFundar"] ?? 0.3))
                Puerta(false, $"Demasiada pendiente ({Math.Round(Math.Atan(pendiente) * 180 / Math.PI)}°): aquí no se puede fundar. Busca un sitio más llano.");
            // Ni en bosque cerrado (`fundacion.bosque_denso`): la celda con densidad ≥ `densidadBosqueMaximaParaFundar`.
            if (art.DensidadBosque != null && art.DensidadBosque.Encoding == EncodingRaster.EncodingU8
                && new RasterDeMundo(art.DensidadBosque).En(donde.x, donde.y) / 255.0 >= ((double?)fundacion?["densidadBosqueMaximaParaFundar"] ?? 0.7))
                Puerta(false, "Bosque cerrado: aquí no se puede fundar. Busca la orilla o un claro.");

            // La tierra para la aldea agrícola (la del trigo), dentro de la zona con que nace la plaza y apartada de ella.
            var sitios = alrededor.sitios;
            var fertilidadMinima = (double?)ParamsPueblos?["aldea"]?["fertilidadMinima"] ?? 0.3;
            if (sitios != null)
                Puerta(sitios.Validos.Count > 0, sitios.Validos.Count > 0
                    ? $"Tierra fértil para la aldea agrícola en la zona (mejor {sitios.Mejor.ToString("0.00", Es)}): en verde en el mapa."
                    : $"Sin tierra de fertilidad {fertilidadMinima.ToString("0.0", Es)} o más en la zona inicial: no cabrá la aldea agrícola, que pide el nivel 2, hasta que la zona crezca.");

            // Las parcelas de bosque de la zona inicial: libres (cabría una Leñera) u ocupadas o en la zona de otra plaza (cualquiera, también tuya).
            var parcelas = alrededor.parcelas;
            var libres = parcelas.Count(q => q.libre);
            var otras = parcelas.Count - libres;
            const string sinMadera = "Sin madera no se pueden construir edificios nuevos.";
            if (parcelas.Count == 0) Puerta(false, $"Ninguna parcela de bosque en la zona inicial: no habrá Leñeras hasta que la zona crezca. {sinMadera}");
            else Puerta(libres > 0, $"{libres} parcela{(libres == 1 ? "" : "s")} de bosque libre{(libres == 1 ? "" : "s")} en la zona inicial: {(libres == 1 ? "cabe 1 Leñera" : $"caben {libres} Leñeras")}"
                + (otras > 0 ? $" ({otras} más ocupada{(otras == 1 ? "" : "s")} o en la zona de otra plaza)" : "") + "." + (libres > 0 ? "" : " " + sinMadera) + " En verde en el mapa.");

            // Los nodos de recurso dentro del alcance y el acceso a bosque.
            caja.Add(Dialogo.Texto("Recursos en la zona inicial", "fundar-titulo"));
            var nodos = art.Nodos.Where(n => n.Posicion != null && CosasDelMapa.Distancia(n.Posicion.X / 1000.0, n.Posicion.Y / 1000.0, donde.x, donde.y) <= radio).ToList();
            caja.Add(Dialogo.Texto(nodos.Count > 0 ? $"{nodos.Count} nodo{(nodos.Count == 1 ? "" : "s")} dentro del alcance" : "Ningún nodo de recurso dentro del alcance", "plaza-sub"));
            if (nodos.Count > 0)
            {
                var lista = Dialogo.Caja("fundar-nodos");
                lista.name = "fundar-nodos";
                foreach (var n in nodos) lista.Add(Dialogo.Texto($"<b>{CosasDelMapa.NombreDeRecurso(n.Tipo)}</b> · {n.Rareza} · {n.CantidadInicial / 1000} uds.", "fundar-nodo"));
                caja.Add(lista);
            }
            var bosques = art.Bosques.Count(b => CosasDelMapa.Distancia(b.Centro.X / 1000.0, b.Centro.Y / 1000.0, donde.x, donde.y) <= radio + b.Radio / 1000.0);
            caja.Add(Dialogo.Texto(bosques > 0 ? $"Acceso a {bosques} bosque{(bosques == 1 ? "" : "s")} · madera disponible" : "Sin acceso a bosque · no habrá madera inicial", "fundar-bosque"));
            return caja;
        }

        /// <summary>Dónde cabría la aldea agrícola si se funda en `donde` (`sitiosDeAldeaAlFundar`): dentro de la zona con que nace la plaza, apartada de
        /// ella y de todo lo que ya se conoce.</summary>
        SitiosDeAldea SitiosAlFundar((double x, double y) donde, List<(double x, double y)> zona)
        {
            var f = RasterDeFertilidad("trigo");
            if (f == null) return null;
            rasterTerreno ??= new RasterDeMundo(c.Artefacto.Terreno);
            transitable ??= SitiosDeAldea.Transitable(c.Artefacto.Transitable);
            var al = c.Sesion.Almacen;
            var ocupados = al.Todas(Entidad.CuerpoOneofCase.Asentamiento).Select(e => Pt(e.Asentamiento.Posicion))
                .Concat(al.Todas(Entidad.CuerpoOneofCase.FichaConocida).Select(e => Pt(e.FichaConocida.Posicion)))
                .Concat(al.Todas(Entidad.CuerpoOneofCase.Pueblo).Select(e => Pt(e.Pueblo.Posicion)))
                .Concat(al.Todas(Entidad.CuerpoOneofCase.Obra).Where(e => e.Obra.Tipo != TipoObra.ObraMejoraAldea).Select(e => Pt(e.Obra.Posicion)))
                .Append(donde);
            return SitiosDeAldea.Buscar(f, rasterTerreno, zona, donde, null, ocupados,
                (double?)ParamsPueblos?["aldea"]?["fertilidadMinima"] ?? 0.3, (double?)ParamsPueblos?["aldea"]?["separacionMinima"] ?? 3, transitable);
        }
    }
}
