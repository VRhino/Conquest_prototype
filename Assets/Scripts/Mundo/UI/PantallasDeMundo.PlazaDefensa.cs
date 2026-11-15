// La plaza por dentro, tanda 3 (paso 7 del bloque 4; BronzeAgeClient: `panelGuarnicion.ts`, `panelMuralla.ts`, `panelPuerta.ts`, `panelAliados.ts`,
// `reclutamiento.ts` y `lugarPlaza` de `arbolReclutamiento.ts`, `armeria.ts`). En el riel, Defensa (Guarnición, Muralla y Puerta con los aliados),
// Reclutamiento (el árbol del campamento con los estados de la plaza: su tecnología, su edificio a su nivel; sin «Adoptar») y la Armería (la mochila a
// la izquierda con las fichas de objeto de la Base y, a la derecha, la Tienda, la Forja y abrir la forja). Con F junto al Barracón, la Galería de tiro,
// las Caballerizas o la Armería, su ficha con el botón de su ventana. Lo que no se puede hacer no sale; el motor valida y su rechazo sale flotando.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BronzeAge.Contract.V1;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace ConquestTactics.Mundo.UI
{
    public sealed partial class PantallasDeMundo
    {
        /// <summary>La lista de escuadras para dejar en la guarnición, abierta; lo que se marca de la puerta sin fijar; el anillo elegido para trazar.</summary>
        bool eligiendoGuarnicion;
        HashSet<GrupoPuerta> borradorPuerta;
        int nivelDeAnillo = 1;
        /// <summary>La Armería: qué enseña la tienda, si solo lo de tu clase, la casilla elegida de la mochila y el borrador de abrir la forja.</summary>
        string tipoTienda = "armadura";
        bool soloMiClase = true;
        int casillaArmeria = -1;
        readonly Dictionary<string, (bool abierta, uint recargo)> borradorForja = new Dictionary<string, (bool, uint)>();

        /// <summary>Los edificios que abren una ventana desde su ficha: los militares, Reclutamiento; la Armería, la suya.</summary>
        static readonly Dictionary<string, string> VentanaDeEdificio = new Dictionary<string, string>
        {
            ["barracon"] = "reclutamiento", ["galeriaDeTiro"] = "reclutamiento", ["caballerizas"] = "reclutamiento", ["armeria"] = "armeria",
        };

        /// <summary>¿Es la plaza de tu Facción? (de visita en una ajena solo se ve lo de fuera).</summary>
        static bool Propia(Asentamiento a, Heroe yo) => yo.HasFaccionId && a.FaccionId == yo.FaccionId;

        /// <summary>¿Tiene la plaza una Armería activa? Lo dice la plaza (`armeria_activa`, también la ajena) o sus edificios.</summary>
        static bool ArmeriaActiva(Asentamiento a) => a.ArmeriaActiva || Internos(a).Any(e => e.Tipo == "armeria" && Activo(e));

        /// <summary>Abre una ventana de la plaza: Reclutamiento es la ventana del árbol (como la tropa del campamento); las demás, la de la plaza.</summary>
        void AbrirVentanaDePlaza(string ventana, Asentamiento a, Action rehacer)
        {
            if (ventana == "reclutamiento") { PanelTropa(null, a.Id); return; }
            ventanaPlaza = ventanaPlaza == ventana ? null : ventana;
            rehacer();
        }

        /// <summary>En la ficha de un edificio en pie con ventana (Barracón, Armería…), el botón que la abre.</summary>
        void BotonDeVentanaEnFicha(VisualElement cuerpo, Asentamiento a, Heroe yo, Edificio e, Action rehacer)
        {
            if (!Activo(e) || !VentanaDeEdificio.TryGetValue(e.Tipo, out var ventana) || !VentanaDisponible(ventana, a, yo)) return;
            var b = Dialogo.Boton(ventana == "armeria" ? "Abrir la Armería" : "Reclutar", "boton-primario plaza-ficha-ventana", () => { fichaId = null; AbrirVentanaDePlaza(ventana, a, rehacer); });
            b.name = "ficha-abrir-" + ventana;
            cuerpo.Add(b);
        }

        // ─────────────────────────── Defensa ───────────────────────────

        /// <summary>Las subpestañas de Defensa: Guarnición, Muralla (desde el nivel de la empalizada o si ya tiene recintos) y Puerta.</summary>
        List<(string id, string nombre)> SubsDeDefensa(Asentamiento a)
        {
            var subs = new List<(string, string)> { ("guarnicion", "Guarnición") };
            var minimo = (int?)DatoDeJuego("Murallas", "MURALLA")?["nivelAsentamientoPorNivelDeRecinto"]?["1"] ?? 2;
            if ((a.NivelActual > 0 ? a.NivelActual : a.Nivel) >= minimo || a.Recintos.Count > 0) subs.Add(("muralla", "Muralla"));
            subs.Add(("puerta", "Puerta"));
            return subs;
        }

        // ── Guarnición (`panelGuarnicion.ts`) ──

        /// <summary>La guarnición (`html` de `panelGuarnicion.ts`): quién la forma y de quién es cada escuadra; dejar o sacar las tuyas. Residente, las de
        /// tu campamento; de visita en una plaza de tu Facción, las de la columna con la que entraste. El cupo es por héroe y en Liderazgo.</summary>
        void Guarnicion(VisualElement cuerpo, Asentamiento a, Heroe yo, Action rehacer)
        {
            var reside = Reside(a, yo);
            var dentro = Presente(a, yo);
            var escuadras = EscuadrasDelHeroe(yo);
            bool EnCampamento(Escuadron s) => s.Contenedor?.DondeCase == ContenedorEscuadron.DondeOneofCase.Campamento;
            bool AquiDeVisita(Escuadron s) => s.Contenedor?.DondeCase == ContenedorEscuadron.DondeOneofCase.AsentamientoId && s.Contenedor.AsentamientoId == a.Id;
            var mias = escuadras.Where(s => reside && EnCampamento(s) && s.EnGuarnicion || AquiDeVisita(s)).ToList();
            var idsMias = new HashSet<string>(mias.Select(s => s.Id));
            var ajenas = a.GuarnicionIds.Select((id, i) => (id, info: i < a.Guarnicion.Count ? a.Guarnicion[i] : null))
                .Where(x => !idsMias.Contains(x.id) && x.info?.HeroeId != yo.Id).ToList();
            var columna = dentro ? ColumnaDelHeroe(yo) : null;
            var de = reside ? "campamento" : columna != null ? "columna" : null;
            var candidatas = reside ? escuadras.Where(s => EnCampamento(s) && !s.EnGuarnicion && s.Cantidad > 0).ToList()
                : columna != null ? escuadras.Where(s => s.Contenedor?.DondeCase == ContenedorEscuadron.DondeOneofCase.EjercitoId && s.Contenedor.EjercitoId == columna.Id && s.Cantidad > 0).ToList()
                : new List<Escuadron>();
            var cupo = (int)a.CupoGuarnicion;
            var usado = mias.Sum(s => LiderazgoDe(s.TropaId) ?? 0);
            var libre = cupo - usado;
            var abierta = eligiendoGuarnicion && candidatas.Count > 0;
            int? Total(string tropa) => (int?)c.Datos.TropasReclutables?.FirstOrDefault(x => (string)x["id"] == tropa)?["unidadesPorDefecto"];

            var caja = Dialogo.Caja("plaza-guarnicion");
            caja.name = "guarnicion";
            var cab = Dialogo.Caja("plaza-guarnicion-cab");
            var total = mias.Count + ajenas.Count;
            cab.Add(Dialogo.Texto($"Guarnición · {total} {(total == 1 ? "escuadra" : "escuadras")}", "plaza-sub plaza-guarnicion-titulo"));
            if (de != null || mias.Count > 0)
            {
                var cupoTexto = Dialogo.Texto($"Tu cupo {usado} / {cupo}", "plaza-guarnicion-cupo" + (usado > cupo ? " excedido" : ""));
                cupoTexto.name = "cupo-guarnicion";
                ConLetrero(cupoTexto, "Tu cupo de guarnición en esta plaza: cada héroe tiene el suyo");
                cab.Add(cupoTexto);
            }
            caja.Add(cab);
            if (de != null || mias.Count > 0)
            {
                var barra = Dialogo.Caja("plaza-guarnicion-barra" + (usado >= cupo ? " llena" : ""));
                var i = Dialogo.Caja("plaza-guarnicion-barra-i");
                i.style.width = Length.Percent(cupo > 0 ? Mathf.Min(100, 100f * usado / cupo) : 0);
                barra.Add(i);
                caja.Add(barra);
            }
            caja.Add(Dialogo.Texto("Defiende la plaza en un asedio. Lo que dejas aquí no sale contigo, ni escolta, ni ataca, hasta que lo saques.", "plaza-nota"));
            var huecos = new ScrollView(ScrollViewMode.Horizontal) { verticalScrollerVisibility = ScrollerVisibility.Hidden, name = "guarnicion-huecos" };
            huecos.AddToClassList("salida-riel");
            huecos.AddToClassList("plaza-guarnicion-huecos");
            foreach (var s in mias)
            {
                var hueco = Dialogo.Caja("salida-hueco salida-hueco-tropa");
                hueco.name = "guarnicion-" + s.Id;
                var tarjeta = PiezasDeTropa.Tarjeta(s.TropaId, LiderazgoDe(s.TropaId), (int)s.Cantidad, Total(s.TropaId), null, AnchoDeHueco);
                if (tarjeta != null) { tarjeta.style.marginRight = 0; tarjeta.style.marginBottom = 0; hueco.Add(tarjeta); }
                var esta = s;
                Button quitar = null;
                quitar = Dialogo.Boton("✕", "salida-hueco-quitar", async () =>
                {
                    if (await OrdenDePlaza(new Orden { RetirarGuarnicion = new OrdenRetirarGuarnicion { SquadId = esta.Id } }, quitar)) rehacer();
                });
                quitar.name = "retirar-" + s.Id;
                ConLetrero(quitar, AquiDeVisita(s) ? "Recogerla a tu columna" : "Sacarla al campamento");
                hueco.Add(quitar);
                ConLetrero(hueco, $"{NombreDeTropa(s.TropaId)} · {s.Cantidad} hombres · tuya{(AquiDeVisita(s) ? " · de visita" : "")} · {LiderazgoDe(s.TropaId)} de Liderazgo");
                huecos.Add(hueco);
            }
            foreach (var (id, info) in ajenas)
            {
                var hueco = Dialogo.Caja("salida-hueco salida-hueco-tropa plaza-guarnicion-ajena");
                hueco.name = "guarnicion-" + id;
                if (info != null)
                {
                    var tarjeta = PiezasDeTropa.Tarjeta(info.TropaId, LiderazgoDe(info.TropaId), (int)info.Cantidad, Total(info.TropaId), null, AnchoDeHueco);
                    if (tarjeta != null) { tarjeta.style.marginRight = 0; tarjeta.style.marginBottom = 0; hueco.Add(tarjeta); }
                    var dueno = Dialogo.Texto("de " + Textos.NombreDeHeroe(c.Sesion.Almacen, info.HeroeId), "plaza-guarnicion-dueno");
                    hueco.Add(dueno);
                    ConLetrero(hueco, $"{NombreDeTropa(info.TropaId)} · {info.Cantidad} hombres · de {Textos.NombreDeHeroe(c.Sesion.Almacen, info.HeroeId)}");
                }
                else hueco.Add(Dialogo.Texto("Escuadra de otro héroe", "plaza-nota"));
                huecos.Add(hueco);
            }
            if (candidatas.Count > 0)
            {
                var mas = Dialogo.Boton("+", "salida-hueco salida-hueco-mas", () => { eligiendoGuarnicion = !eligiendoGuarnicion; rehacer(); });
                mas.name = "guarnicion-mas";
                mas.EnableInClassList("activo", abierta);
                ConLetrero(mas, $"Dejar una escuadra {(de == "columna" ? "de tu columna" : "del campamento")} en la guarnición");
                huecos.Add(mas);
            }
            if (huecos.childCount > 0) caja.Add(huecos);
            else caja.Add(Dialogo.Texto("Nadie ha dejado tropa en la guarnición.", "plaza-nota"));
            if (abierta)
            {
                var elegir = Dialogo.Caja("salida-elegir fila-envuelta");
                elegir.name = "guarnicion-elegir";
                foreach (var s in candidatas)
                {
                    var cabe = (LiderazgoDe(s.TropaId) ?? 0) <= libre;
                    var esta = s;
                    Button candidata = null;
                    candidata = new Button(async () =>
                    {
                        eligiendoGuarnicion = false;
                        if (await OrdenDePlaza(new Orden { AsignarGuarnicion = new OrdenAsignarGuarnicion { SquadId = esta.Id } }, candidata)) rehacer();
                    }) { name = "asignar-" + s.Id };
                    candidata.AddToClassList("salida-candidata");
                    if (PiezasDeTropa.Miniatura(s.TropaId, 32) is VisualElement mini) candidata.Add(mini);
                    var textos = new VisualElement();
                    textos.Add(Dialogo.Texto(string.IsNullOrEmpty(s.Nombre) ? NombreDeTropa(s.TropaId) : s.Nombre, "aviso-titulo"));
                    textos.Add(Dialogo.Texto($"{s.Cantidad} hombres · {LiderazgoDe(s.TropaId)} de Liderazgo", "campo-ayuda" + (cabe ? "" : " falta")));
                    candidata.Add(textos);
                    candidata.SetEnabled(cabe);
                    if (!cabe) ConLetrero(candidata, $"No cabe: quedan {Math.Max(0, libre)} de Liderazgo");
                    elegir.Add(candidata);
                }
                caja.Add(elegir);
            }
            string nota = de == null ? null
                : cupo == 0 ? "El cupo es 0: la plaza necesita un Barracón o una Galería de tiro para admitir guarnición."
                : candidatas.Count == 0 && mias.Count == 0 ? (de == "columna" ? "Tu columna no trae escuadras con soldados." : "No tienes escuadras en el campamento: se reclutan aquí o vuelven con tu columna.")
                : de == "columna" ? "De visita: dejas tropa de la columna con la que entraste. No sale contigo; la recoges a tu columna estando dentro." : null;
            if (nota != null) caja.Add(Dialogo.Texto(nota, "plaza-nota"));
            cuerpo.Add(caja);
        }

        // ── Muralla (`panelMuralla.ts`) ──

        static readonly string[] Material = { "Empalizada", "Muro de piedra", "Muralla con adarve" };

        /// <summary>Las puertas de un anillo: cada tirada 4-conexa de celdas de puerta es una sola (Murallas §2).</summary>
        static int Aberturas(Recinto r)
        {
            var puertas = r.Celdas.Where(x => x.Clase == ClaseCelda.CeldaPuerta).ToList();
            var vistas = new HashSet<int>();
            var n = 0;
            for (int i = 0; i < puertas.Count; i++)
            {
                if (!vistas.Add(i)) continue;
                n++;
                var pila = new Stack<int>(new[] { i });
                while (pila.Count > 0)
                {
                    var k = puertas[pila.Pop()];
                    for (int j = 0; j < puertas.Count; j++)
                        if (!vistas.Contains(j) && Math.Abs(puertas[j].Col - k.Col) + Math.Abs(puertas[j].Fila - k.Fila) == 1) { vistas.Add(j); pila.Push(j); }
                }
            }
            return n;
        }

        /// <summary>Lo que tarda cada celda en minutos de mundo (`MURALLA.minutosPorCelda` × `factorTiempoMuralla` de las políticas); null sin datos.</summary>
        double? MinutosPorCelda(Asentamiento a, int nivel) =>
            (double?)DatoDeJuego("Murallas", "MURALLA")?["minutosPorCelda"]?[nivel.ToString()] is double m ? m * FactorDePoliticas(a, "factorTiempoMuralla") : (double?)null;

        /// <summary>El plano de los anillos, celda a celda: en pie (del color de su material), la que se levanta, lo que falta, puertas y torres.</summary>
        static VisualElement PlanoDeMuralla(IList<Recinto> recintos)
        {
            var celdas = recintos.SelectMany(r => r.Celdas.Select((k, i) => (k, i, r))).ToList();
            var plano = new VisualElement { name = "muralla-plano", pickingMode = PickingMode.Ignore };
            plano.AddToClassList("plaza-muralla-plano");
            if (celdas.Count == 0) return plano;
            int minC = celdas.Min(x => x.k.Col), minF = celdas.Min(x => x.k.Fila);
            int ancho = celdas.Max(x => x.k.Col) - minC + 1, alto = celdas.Max(x => x.k.Fila) - minF + 1;
            var tam = Mathf.Max(3, Mathf.Min(12, 240 / Mathf.Max(ancho, alto)));
            plano.style.width = ancho * tam + 8;
            plano.style.height = alto * tam + 8;
            var colores = new[] { new Color32(0x9c, 0x6b, 0x3c, 0xff), new Color32(0xa8, 0x9f, 0x8c, 0xff), new Color32(0xd8, 0xcf, 0xb6, 0xff) };
            plano.generateVisualContent += m =>
            {
                var p = m.painter2D;
                p.fillColor = new Color32(0xd8, 0xb8, 0x78, 0x99);
                p.BeginPath();
                p.Arc(new Vector2(4 + ancho * tam / 2f, 4 + alto * tam / 2f), Mathf.Max(3, tam), 0, 360);
                p.Fill();
                foreach (var (k, i, r) in celdas)
                {
                    var mejorada = r.HasMejorandoA && i < r.Mejoradas;
                    var curso = r.HasMejorandoA ? i == r.Mejoradas : i == r.Avance;
                    var enPie = mejorada || (!r.HasMejorandoA || i >= r.Mejoradas) && i < r.Avance;
                    var nivel = (int)(mejorada ? r.MejorandoA : r.Nivel);
                    Color color = colores[Mathf.Clamp(nivel - 1, 0, 2)];
                    if (curso) color = new Color32(0xe0, 0xa5, 0x3a, 0xff);
                    else if (!enPie) color = new Color(0, 0, 0, 0.35f);
                    if (k.Clase == ClaseCelda.CeldaPuerta) color = enPie || curso ? new Color32(0x6f, 0xb6, 0xff, 0xff) : new Color(0.43f, 0.71f, 1f, 0.3f);
                    p.fillColor = color;
                    var x = 4 + (k.Col - minC) * tam;
                    var y = 4 + (k.Fila - minF) * tam;
                    var lado = k.Clase == ClaseCelda.CeldaTorre ? tam : tam - 1;
                    p.BeginPath();
                    p.MoveTo(new Vector2(x, y)); p.LineTo(new Vector2(x + lado, y)); p.LineTo(new Vector2(x + lado, y + lado)); p.LineTo(new Vector2(x, y + lado)); p.ClosePath();
                    p.Fill();
                    // Lo que falta, con su borde de bronce (`mp-falta`); las torres en pie, con el suyo claro.
                    if (!enPie && !curso) { p.strokeColor = new Color(0.66f, 0.47f, 0.25f, 0.6f); p.lineWidth = 1; p.Stroke(); }
                    else if (k.Clase == ClaseCelda.CeldaTorre) { p.strokeColor = new Color32(0xf1, 0xdc, 0xae, 0xff); p.lineWidth = 1.2f; p.Stroke(); }
                }
            };
            return plano;
        }

        /// <summary>La Muralla (`panelMuralla.ts`): el plano, una tarjeta por anillo (material, puertas, torres, bono, obra y coste por celda, mejorar o
        /// abandonar) y los anillos posibles para trazar el siguiente.</summary>
        void Muralla(VisualElement cuerpo, Asentamiento a, Heroe yo, Action rehacer)
        {
            var datos = DatoDeJuego("Murallas", "MURALLA");
            var esGobernador = TitularDe(a, "gobernador") == yo.Id;
            var puedeComprometer = esGobernador || TitularDe(a, "maestroObras") == yo.Id;
            var cargo = esGobernador ? "gobernador" : "maestroObras";
            var nivel = a.NivelActual > 0 ? a.NivelActual : a.Nivel;
            var porNivel = Enumerable.Range(1, 3).Select(n => (int?)datos?["nivelAsentamientoPorNivelDeRecinto"]?[n.ToString()] ?? n + 1).ToArray();
            var nivelMaximo = (int?)datos?["nivelMaximo"] ?? 3;
            var recintos = a.Recintos;
            var enObra = recintos.Any(r => r.Avance < r.Celdas.Count);
            var motivo = !Reside(a, yo) ? "Solo quien reside en esta plaza gestiona su muralla." : null;
            var info = $"Comprometer un anillo es gratis: la obra se paga celda a celda del almacén, al ritmo de la obra, y se para si falta material o tocaría la reserva. Cada anillo pide un nivel de plaza: empalizada {porNivel[0]}, muro de piedra {porNivel[1]}, muralla con adarve {porNivel[2]} (esta plaza rinde como nivel {nivel}). Un anillo cerrado se mejora al siguiente material; uno a medias solo lo abandona el Gobernador.";
            cuerpo.Add(TituloDeSeccion("Muralla", info, Chip($"{recintos.Count} {(recintos.Count == 1 ? "anillo" : "anillos")}")));
            if (motivo != null) cuerpo.Add(Dialogo.Texto(motivo, "plaza-nota"));
            if (recintos.Count == 0) cuerpo.Add(Dialogo.Texto("La plaza aún no tiene ningún recinto amurallado.", "plaza-vacio"));
            else
            {
                var vista = Dialogo.Caja("plaza-muralla-vista");
                vista.Add(PlanoDeMuralla(recintos));
                var leyenda = Dialogo.Caja("plaza-muralla-leyenda");
                foreach (var (clase, texto) in new[] { ("pie", "en pie"), ("curso", "levantándose"), ("falta", "por levantar"), ("puerta", "puerta"), ("torre", "torre") })
                {
                    var f = Dialogo.Caja("plaza-muralla-leyenda-item");
                    f.Add(Dialogo.Caja("plaza-muralla-muestra " + clase));
                    f.Add(Dialogo.Texto(texto, "plaza-nota"));
                    leyenda.Add(f);
                }
                vista.Add(leyenda);
                cuerpo.Add(vista);
                for (int k = 0; k < recintos.Count; k++) cuerpo.Add(TarjetaDeAnillo(a, recintos[k], k == recintos.Count - 1, puedeComprometer, esGobernador, cargo, motivo, porNivel, nivelMaximo, rehacer));
            }
            cuerpo.Add(TituloDeSeccion(recintos.Count > 0 ? "Nuevo anillo" : "Trazar un anillo", null));
            if (!puedeComprometer) { cuerpo.Add(Dialogo.Texto("Trazar o mejorar la muralla es del Gobernador o del Maestro de Obras de la plaza.", "plaza-nota")); return; }
            if (enObra) { cuerpo.Add(Dialogo.Texto("Hay un anillo en obra: termínalo o abandónalo antes de trazar otro.", "plaza-nota")); return; }
            var permitidos = Enumerable.Range(1, 3).Where(n => nivel >= porNivel[n - 1]).ToList();
            if (!permitidos.Contains(nivelDeAnillo)) nivelDeAnillo = permitidos.FirstOrDefault() is int p0 && p0 > 0 ? p0 : 1;
            var opciones = Rejilla("plaza-anillo-opciones", 180, 6);
            for (int n = 1; n <= 3; n++)
            {
                var esteNivel = n;
                var bloqueada = nivel < porNivel[n - 1];
                var tarifa = datos?["tarifaPorCelda"]?[n.ToString()] as JObject;
                var porCelda = MinutosPorCelda(a, n);
                var bono = (double?)datos?["bonoDefensaPorNivel"]?[n.ToString()];
                var op = new Button(() => { nivelDeAnillo = esteNivel; rehacer(); }) { name = "anillo-" + n };
                op.AddToClassList("plaza-anillo-opcion");
                op.EnableInClassList("elegida", n == nivelDeAnillo && !bloqueada);
                op.EnableInClassList("bloqueada", bloqueada);
                op.SetEnabled(!bloqueada);
                op.Add(Dialogo.Texto(Material[n - 1], "plaza-anillo-opcion-nombre"));
                op.Add(Dialogo.Texto($"{(bono != null ? $"defensa ×{Cifra(bono.Value)}" : "")}{(porCelda != null ? $" · {Duracion(Math.Max(1, Math.Round(porCelda.Value)))}/celda" : "")}", "plaza-nota"));
                if (tarifa != null) op.Add(ChipsDeCoste(a, tarifa));
                if (bloqueada) op.Add(Dialogo.Texto($"pide plaza de nivel {porNivel[n - 1]}", "plaza-anillo-opcion-pide"));
                ConLetrero(op, bloqueada ? $"Pide una plaza de nivel {porNivel[n - 1]}" : $"{Material[n - 1]}: se paga celda a celda (puertas ×{(int?)datos?["factorPuerta"] ?? 4}, torres ×{(int?)datos?["factorTorre"] ?? 3})");
                opciones.Add(op);
            }
            cuerpo.Add(opciones);
            var pie = Dialogo.Caja("plaza-reserva-pie");
            Button trazar = null;
            trazar = Dialogo.Boton(recintos.Count > 0 ? "Ampliar con un anillo" : "Trazar la muralla", "plaza-muralla-trazar", async () =>
            {
                if (await OrdenDePlaza(new Orden { ComprometerRecinto = new OrdenComprometerRecinto { AsentamientoId = a.Id, Cargo = cargo, Nivel = nivelDeAnillo.ToString() } }, trazar)) rehacer();
            });
            trazar.name = "muralla-trazar";
            trazar.SetEnabled(motivo == null && permitidos.Count > 0);
            if (motivo != null || permitidos.Count == 0) ConLetrero(trazar, motivo ?? "La plaza no llega a ningún anillo");
            pie.Add(trazar);
            cuerpo.Add(pie);
        }

        VisualElement TarjetaDeAnillo(Asentamiento a, Recinto r, bool exterior, bool puedeComprometer, bool esGobernador, string cargo, string motivo, int[] porNivel, int nivelMaximo, Action rehacer)
        {
            var datos = DatoDeJuego("Murallas", "MURALLA");
            var mejora = r.HasMejorandoA;
            var total = r.Celdas.Count;
            var completo = total > 0 && r.Avance >= total;
            var hechas = mejora ? (int)r.Mejoradas : (int)r.Avance;
            var torres = r.Celdas.Count(x => x.Clase == ClaseCelda.CeldaTorre);
            var puertas = Aberturas(r);
            var bonoNivel = (double?)datos?["bonoDefensaPorNivel"]?[r.Nivel.ToString()];
            var integridad = total > 0 ? Math.Min(1, (double)r.Avance / total) : 0;
            var bono = bonoNivel == null ? (double?)null : Math.Round((1 + (bonoNivel.Value - 1) * integridad / Math.Max(1, puertas)) * 100) / 100;
            var nombre = (int)r.Nivel >= 1 && r.Nivel <= 3 ? Material[r.Nivel - 1] : $"Nivel {r.Nivel}";
            var tarjeta = Dialogo.Caja("plaza-anillo " + (mejora ? "mejora" : completo ? "cerrado" : "obra"));
            tarjeta.name = "recinto-" + r.Id;
            var cab = Dialogo.Caja("plaza-anillo-cab");
            cab.Add(Medallon((int)r.Nivel, completo ? "" : "apagado", 26, 30));
            var tit = Dialogo.Caja("plaza-anillo-titulo");
            tit.Add(Dialogo.Texto(nombre, "plaza-anillo-nombre"));
            tit.Add(Dialogo.Texto($"Recinto {(exterior ? "exterior" : "interior")}", "plaza-nota"));
            cab.Add(tit);
            cab.Add(Dialogo.Texto(mejora ? $"⬆ a nivel {r.MejorandoA}" : completo ? "✓ cerrado" : "en obra", "plaza-anillo-estado"));
            tarjeta.Add(cab);
            var chips = Dialogo.Caja("plaza-anillo-chips");
            chips.Add(ChipConIcono(IconosDelMundo.Icono("glifo", "entrar", 11), puertas.ToString()));
            chips.Add(ChipConIcono(IconosDelMundo.Icono("simbolo", "torre", 11, IconosDelMundo.OroClaro), torres.ToString()));
            chips.Add(Chip($"{total} celdas"));
            if (bono != null) chips.Add(ChipConIcono(IconosDelMundo.Icono("glifo", "escudo", 11), $"×{Cifra(bono.Value)}"));
            ConLetrero(chips, $"Puertas {puertas} (una abertura de dos celdas cuenta como una) · torres {torres} · {total} celdas{(bono != null ? $" · multiplicador de defensa ×{Cifra(bono.Value)}" : "")}");
            tarjeta.Add(chips);
            var avance = Dialogo.Caja("plaza-anillo-avance");
            avance.Add(BarraAvance(total > 0 ? (float)hechas / total : 0));
            avance.Add(Dialogo.Texto($"{hechas}/{total} {(mejora ? "mejoradas" : "en pie")} · {(total > 0 ? Math.Round(100.0 * hechas / total) : 0)}%", "plaza-nota"));
            tarjeta.Add(avance);
            if (!completo || mejora)
            {
                var nivelObra = (int)(mejora ? r.MejorandoA : r.Nivel);
                var porCelda = MinutosPorCelda(a, nivelObra);
                var quedan = total - hechas;
                var siguiente = r.HasSiguienteCeldaEnMs ? r.SiguienteCeldaEnMs : (long?)null;
                if (porCelda != null)
                {
                    if (siguiente != null && siguiente > AhoraMs)
                    {
                        var celda = Dialogo.Caja("plaza-anillo-celda");
                        celda.Add(Dialogo.Texto($"Celda {hechas + 1}", "plaza-nota"));
                        var barra = BarraAvance(0);
                        celda.Add(barra);
                        var queda = Dialogo.Texto("", "plaza-nota");
                        celda.Add(queda);
                        var fin = siguiente.Value;
                        var ms = porCelda.Value * 60_000;
                        void Reloj() { Avance(barra, Mathf.Clamp01((float)(1 - (fin - AhoraMs) / ms))); queda.text = CuentaAtras(fin); }
                        Reloj();
                        relojesDePlaza.Add(("ventana", Reloj));
                        tarjeta.Add(celda);
                    }
                    else tarjeta.Add(Dialogo.Texto("Celda parada: falta material en el almacén o tocaría la reserva. Sigue sola en cuanto lo haya.", "plaza-nota plaza-anillo-parada"));
                    var resto = (siguiente != null && siguiente > AhoraMs ? (siguiente.Value - AhoraMs) / 60_000.0 : porCelda.Value) + Math.Max(0, quedan - 1) * porCelda.Value;
                    tarjeta.Add(Dialogo.Texto($"quedan {Duracion(Math.Max(1, Math.Round(resto)))} · {Duracion(Math.Max(1, Math.Round(porCelda.Value)))} por celda", "plaza-nota"));
                }
                // La tarifa es la acumulada (Murallas §11): levantar paga la del nivel; mejorar, la diferencia con la de antes.
                var tarifa = datos?["tarifaPorCelda"]?[nivelObra.ToString()] as JObject;
                if (mejora && tarifa != null)
                {
                    var antes = datos?["tarifaPorCelda"]?[(nivelObra - 1).ToString()] as JObject;
                    tarifa = new JObject(tarifa.Properties().Select(p => new JProperty(p.Name, Math.Max(0, (double)p.Value - ((double?)antes?[p.Name] ?? 0)))).Where(p => (double)p.Value > 0));
                }
                if (tarifa != null)
                {
                    var fila = Dialogo.Caja("plaza-anillo-tarifa");
                    fila.Add(Dialogo.Texto("Cada celda", "plaza-nota"));
                    fila.Add(ChipsDeCoste(a, tarifa));
                    tarjeta.Add(fila);
                }
            }
            var acciones = Dialogo.Caja("plaza-anillo-acciones");
            if (completo && !mejora && r.Nivel < nivelMaximo && puedeComprometer)
            {
                var pide = porNivel[r.Nivel];
                var bajo = (a.NivelActual > 0 ? a.NivelActual : a.Nivel) < pide ? $"Mejorar a {Material[r.Nivel]} pide una plaza de nivel {pide}." : null;
                Button mejorar = null;
                mejorar = Dialogo.Boton($"⬆ {Material[r.Nivel]}", "plaza-anillo-mejorar", async () =>
                {
                    if (await OrdenDePlaza(new Orden { MejorarRecinto = new OrdenMejorarRecinto { AsentamientoId = a.Id, Cargo = cargo, RecintoId = r.Id } }, mejorar)) rehacer();
                });
                mejorar.name = "mejorar-recinto";
                mejorar.SetEnabled(motivo == null && bajo == null);
                if (motivo != null || bajo != null) ConLetrero(mejorar, motivo ?? bajo);
                acciones.Add(mejorar);
            }
            if (!completo && esGobernador)
            {
                Button abandonar = null;
                abandonar = Dialogo.Boton("✕ Abandonar", "plaza-anillo-abandonar", async () =>
                {
                    if (!await dialogo.Abrir("El anillo se borra entero, celdas levantadas incluidas, y no se devuelve lo gastado.", "¿Abandonar el anillo?", "Abandonar", peligro: true)) return;
                    if (await OrdenDePlaza(new Orden { AbandonarRecinto = new OrdenAbandonarRecinto { AsentamientoId = a.Id, RecintoId = r.Id } }, abandonar)) rehacer();
                });
                abandonar.name = "abandonar-recinto";
                abandonar.SetEnabled(motivo == null);
                acciones.Add(abandonar);
            }
            if (acciones.childCount > 0) tarjeta.Add(acciones);
            return tarjeta;
        }

        // ── Puerta (`panelPuerta.ts` y `panelAliados.ts`) ──

        static readonly (GrupoPuerta grupo, string clave, string familia, string icono, string nombre, string sub)[] GruposDePuerta =
        {
            (GrupoPuerta.GrupoNeutrales, "neutrales", "glifo", "sitio", "Neutrales", "Ni aliados ni en guerra con la Facción"),
            (GrupoPuerta.GrupoAliados, "aliados", "glifo", "gente", "Aliados", "Aliados y vasallos"),
            (GrupoPuerta.GrupoEnemigos, "enemigos", "glifo", "atacar", "Enemigos", "En guerra con la Facción"),
            (GrupoPuerta.GrupoAedas, "aedas", "glifo", "aeda", "Aedas", "Los cronistas viajeros"),
        };

        /// <summary>La Puerta: un portón con su estado, una casilla por grupo que se abre o se cierra (y «Fijar la puerta» con cambios), los vetados y
        /// «Vetar»; al final, reabastecer a los aliados.</summary>
        void Puerta(VisualElement cuerpo, Asentamiento a, Heroe yo, Action rehacer)
        {
            var faccion = c.Sesion.Almacen.Buscar(TipoEntidad.Faccion, a.FaccionId)?.Faccion;
            var esGobernador = TitularDe(a, "gobernador") == yo.Id && Reside(a, yo);
            var esRey = faccion != null && faccion.HasReyId && faccion.ReyId == yo.Id;
            var puede = esGobernador || esRey;
            var porDefecto = (DatoDeJuego("Diplomacia_Facciones", "PUERTA")?["cerradaAPorDefecto"] as JArray)?.Select(x => (string)x).ToList() ?? new List<string> { "neutrales", "enemigos" };
            var fijada = a.PuertaCerradaA.Count > 0 ? new HashSet<GrupoPuerta>(a.PuertaCerradaA) : new HashSet<GrupoPuerta>(GruposDePuerta.Where(g => porDefecto.Contains(g.clave)).Select(g => g.grupo));
            var marcada = puede && borradorPuerta != null ? borradorPuerta : fijada;
            var cambios = GruposDePuerta.Any(g => marcada.Contains(g.grupo) != fijada.Contains(g.grupo));
            var cerradas = fijada.Count;
            var nombre = a.HasNombre ? a.Nombre : a.Id;
            cuerpo.Add(TituloDeSeccion("Puerta", "Tu Facción y quien reside aquí entran siempre. Sin ningún grupo cerrado, la plaza queda abierta a todos. Es el exilio: no caduca, y el Gobernador o el Rey pueden cambiarla cuando quieran. Vetar a jugadores concretos es solo del Gobernador de la plaza (a un residente no se le veta): solo se veta a quien no reside aquí, y solo ves a los ciudadanos de tu Facción y a los héroes que tienes a la vista."));
            var cab = Dialogo.Caja("plaza-puerta-cab " + (cerradas == 0 ? "abierta" : cerradas == GruposDePuerta.Length ? "cerrada" : "entornada"));
            cab.name = "puerta-cab";
            cab.Add(cerradas == 0 ? IconosDelMundo.Icono("glifo", "entrar", 26) : IconosDelMundo.Icono("simbolo", "candado", 24, IconosDelMundo.OroClaro));
            var textos = Dialogo.Caja("plaza-puerta-cab-cuerpo");
            textos.Add(Dialogo.Texto($"Puerta de {nombre}", "plaza-puerta-cab-nombre"));
            textos.Add(Dialogo.Texto($"{(cerradas == 0 ? "Abierta a todos" : $"Cerrada a {cerradas} de {GruposDePuerta.Length}")} · {(a.PuertaCerradaA.Count > 0 ? "fijada" : "por defecto")}", "plaza-nota"));
            cab.Add(textos);
            var siempre = ChipConIcono(Glifo("casa", 11), "los tuyos, siempre");
            ConLetrero(siempre, "Tu Facción y quien reside aquí entran siempre");
            cab.Add(siempre);
            cuerpo.Add(cab);
            var grupos = Rejilla("plaza-puerta-grupos", 120, 6);
            foreach (var g in GruposDePuerta)
            {
                var cerrada = marcada.Contains(g.grupo);
                var grupo = g.grupo;
                var casilla = new Button(() =>
                {
                    borradorPuerta ??= new HashSet<GrupoPuerta>(fijada);
                    if (!borradorPuerta.Remove(grupo)) borradorPuerta.Add(grupo);
                    rehacer();
                }) { name = "puerta-" + g.clave };
                casilla.AddToClassList("plaza-puerta-grupo");
                casilla.EnableInClassList("cerrada", cerrada);
                casilla.EnableInClassList("cambiada", cerrada != fijada.Contains(g.grupo));
                casilla.SetEnabled(puede);
                casilla.Add(IconoDe(g.familia, g.icono, 22));
                casilla.Add(Dialogo.Texto(g.nombre, "plaza-puerta-grupo-nombre"));
                var estado = Dialogo.Caja("plaza-puerta-grupo-estado");
                if (cerrada) estado.Add(IconosDelMundo.Icono("simbolo", "candado", 11, new Color32(0xff, 0xb3, 0xa8, 0xff)));
                estado.Add(Dialogo.Texto(cerrada ? "cerrada" : "abierta", "plaza-nota"));
                casilla.Add(estado);
                ConLetrero(casilla, $"{g.nombre}: {g.sub}");
                grupos.Add(casilla);
            }
            cuerpo.Add(grupos);
            if (puede)
            {
                var pie = Dialogo.Caja("plaza-reserva-pie");
                if (cambios)
                {
                    var deshacer = Dialogo.Boton("Deshacer", "plaza-reserva-descartar", () => { borradorPuerta = null; rehacer(); });
                    deshacer.name = "puerta-deshacer";
                    pie.Add(deshacer);
                }
                Button fijar = null;
                fijar = Dialogo.Boton("Fijar la puerta", "plaza-muralla-trazar", async () =>
                {
                    var o = new OrdenFijarPuerta { AsentamientoId = a.Id, HeroeId = yo.Id };
                    o.CerradaA.AddRange(GruposDePuerta.Where(x => marcada.Contains(x.grupo)).Select(x => x.clave));
                    if (await OrdenDePlaza(new Orden { FijarPuerta = o }, fijar)) { borradorPuerta = null; rehacer(); }
                });
                fijar.name = "puerta-fijar";
                fijar.SetEnabled(cambios);
                if (!cambios) ConLetrero(fijar, "Pulsa un grupo para abrirle o cerrarle la puerta");
                pie.Add(fijar);
                cuerpo.Add(pie);
            }
            else cuerpo.Add(Dialogo.Texto("Solo el Gobernador de la plaza o el Rey de su Facción pueden fijar la puerta.", "plaza-nota"));

            // Los vetados.
            var residentes = new HashSet<string>(a.FundadoresIds.Concat(a.CasasCompradas));
            var vetados = a.VetadosIds.ToList();
            cuerpo.Add(TituloDeSeccion("Vetados", null, Chip(vetados.Count.ToString())));
            if (vetados.Count == 0) cuerpo.Add(Dialogo.Texto("Nadie vetado.", "plaza-vacio"));
            else
            {
                var fichas = Dialogo.Caja("plaza-puerta-vetados");
                foreach (var id in vetados)
                {
                    var f = Dialogo.Caja("plaza-puerta-vetado");
                    f.Add(Dialogo.Texto(Textos.NombreDeHeroe(c.Sesion.Almacen, id), "plaza-puerta-vetado-nombre"));
                    if (esGobernador)
                    {
                        var vetado = id;
                        Button quitar = null;
                        quitar = Dialogo.Boton("✕", "plaza-puerta-vetado-quitar", async () =>
                        {
                            if (await OrdenDePlaza(new Orden { VetarJugador = new OrdenVetarJugador { AsentamientoId = a.Id, HeroeId = yo.Id, VetadoId = vetado, Vetar = false } }, quitar)) rehacer();
                        });
                        quitar.name = "levantar-veto";
                        ConLetrero(quitar, "Levantar el veto");
                        f.Add(quitar);
                    }
                    fichas.Add(f);
                }
                cuerpo.Add(fichas);
            }
            if (!esGobernador) cuerpo.Add(Dialogo.Texto("Solo el Gobernador de la plaza veta a jugadores concretos.", "plaza-nota"));
            else
            {
                // A quién vetar: los ciudadanos de tu Facción y los héroes que tienes a la vista, que no residen aquí ni están vetados.
                var candidatos = (faccion?.CiudadanosIds ?? Enumerable.Empty<string>())
                    .Concat(c.Sesion.Almacen.Todas(Entidad.CuerpoOneofCase.Heroe).Select(e => e.Heroe.Id))
                    .Distinct().Where(id => id != yo.Id && !residentes.Contains(id) && !vetados.Contains(id)).ToList();
                if (candidatos.Count == 0) cuerpo.Add(Dialogo.Texto("No hay a quién vetar.", "plaza-nota"));
                else
                {
                    var fila = Dialogo.Caja("plaza-reserva-pie");
                    var combo = new DropdownField(candidatos.Select(id => Textos.NombreDeHeroe(c.Sesion.Almacen, id)).ToList(), 0) { name = "vetar-a" };
                    combo.AddToClassList("plaza-reserva-anadir");
                    fila.Add(combo);
                    Button vetar = null;
                    vetar = Dialogo.Boton("Vetar", "plaza-reserva-descartar plaza-puerta-vetar", async () =>
                    {
                        if (combo.index < 0) return;
                        if (await OrdenDePlaza(new Orden { VetarJugador = new OrdenVetarJugador { AsentamientoId = a.Id, HeroeId = yo.Id, VetadoId = candidatos[combo.index], Vetar = true } }, vetar)) rehacer();
                    });
                    vetar.name = "vetar";
                    fila.Add(vetar);
                    cuerpo.Add(fila);
                }
            }

            // Reabastecer a los aliados (`panelAliados.ts`): lo cambian el Gobernador o el Tesorero, residentes y dentro.
            var abierto = a.PermiteReabastecerAliados;
            var tieneCargo = Reside(a, yo) && (TitularDe(a, "gobernador") == yo.Id || TitularDe(a, "tesorero") == yo.Id);
            cuerpo.Add(TituloDeSeccion("Reabastecer a los aliados", "Los ejércitos de tu Facción reponen víveres en esta plaza siempre. Los de un aliado solo si abres el almacén, y eso le cuesta stock real a la plaza. Cerrar no es retroactivo: lo ya repuesto está repuesto. Neutrales y hostiles nunca reponen."));
            var tarjeta = Dialogo.Caja("plaza-aliados" + (abierto ? " abierto" : ""));
            tarjeta.name = "aliados";
            tarjeta.Add(Glifo("pesants", 22));
            var cuerpoT = Dialogo.Caja("plaza-aliados-cuerpo");
            cuerpoT.Add(Dialogo.Texto(abierto ? "Almacén abierto a los aliados" : "Almacén cerrado a los aliados", "plaza-puerta-cab-nombre"));
            cuerpoT.Add(Dialogo.Texto(tieneCargo ? "Sus ejércitos reponen víveres aquí a costa de la plaza." : "Lo cambian el Gobernador o el Tesorero, estando dentro.", "plaza-nota"));
            tarjeta.Add(cuerpoT);
            if (tieneCargo)
                tarjeta.Add(Interruptor("alternar-aliados", "", null, abierto, "", "", "plaza-aliados-interruptor",
                    async si => { var ok = await OrdenDePlaza(new Orden { AlternarReabastecerAliados = new OrdenAlternarReabastecerAliados { AsentamientoId = a.Id, Permitido = si } }); if (ok) rehacer(); return ok; }));
            cuerpo.Add(tarjeta);
        }

        // ─────────────────────────── Reclutamiento ───────────────────────────

        /// <summary>El lugar de la plaza para el árbol (`lugarPlaza`): el estado por las tecnologías de tu Facción y el nivel de sus edificios.</summary>
        LugarCampamento LugarDePlaza(Asentamiento plaza, Heroe h, ArbolDeTropas a)
        {
            var tecs = h.HasFaccionId ? c.Sesion.Almacen.Buscar(TipoEntidad.TecnologiaFaccion, h.FaccionId)?.Tecnologias : null;
            var eraMundo = c.Sesion.Almacen.Buscar(TipoEntidad.EstadoTecnologia, "tecnologia")?.EstadoTecnologia?.Era;
            var niveles = new Dictionary<string, int>();
            foreach (var e in Internos(plaza).Where(Activo)) niveles[e.Tipo] = Math.Max(niveles.TryGetValue(e.Tipo, out var n) ? n : 0, NivelInterno(e));
            var mias = EscuadrasDelHeroe(h);
            return new LugarCampamento
            {
                Plaza = plaza, A = a, Niveles = niveles,
                Adoptadas = tecs == null ? null : new HashSet<string>(tecs.Adoptadas),
                Aparecidas = tecs == null ? null : new HashSet<string>(tecs.Aparecidas),
                Prestadas = new Dictionary<string, Escuadron>(),
                Propias = mias.Where(s => !s.HasPrestadaPorCampamentoId).GroupBy(s => s.TropaId).ToDictionary(g => g.Key, g => g.First()),
                Oferta = new Dictionary<string, TropaDeCampamento>(),
                Presta = new HashSet<string>(),
                EraMundo = string.IsNullOrEmpty(eraMundo) ? (int?)null : a.EraDe(eraMundo),
            };
        }

        static readonly Dictionary<string, string> LeyendaDePlaza = new Dictionary<string, string>
        {
            ["lista"] = "lista para reclutar", ["tuya"] = "ya tienes su escuadra", ["edificio"] = "falta el edificio a su nivel",
            ["adoptable"] = "solo falta adoptar su tecnología", ["bloqueada"] = "faltan tecnologías", ["futura"] = "bloqueada: su Era aún no empieza",
        };

        int NivelEnPlaza(LugarCampamento L, string edificio) => edificio == "centroUrbano" ? 99 : L.Niveles.TryGetValue(edificio, out var n) ? n : 0;

        string NombreDeTecnologia(string id)
        {
            var tecs = (c.Datos.Raiz["documentos"] as JObject)?.Properties().Select(p => p.Value["TECNOLOGIAS"]).FirstOrDefault(x => x != null);
            return (string)tecs?[id]?["nombre"] ?? id?.Replace('_', ' ');
        }

        List<string> TecnologiasQueFaltan(LugarCampamento L, string id) =>
            (L.A.Nodos[id].Tecs ?? new List<string>()).Where(t => L.Adoptadas == null || !L.Adoptadas.Contains(t)).ToList();

        /// <summary>La franja de estado de la carta en la plaza (`carta` de `lugarPlaza`).</summary>
        string EstadoEnCartaDePlaza(LugarCampamento L, string id, string est)
        {
            var t = L.A.Nodos[id].T;
            switch (est)
            {
                case "lista": return "Lista para reclutar";
                case "tuya": return $"Tu escuadra {L.Propias[id].Cantidad}/{t.Unidades}";
                case "edificio": return $"Falta {Pieza(t.Edificio)} {t.NivelRequerido} · tu plaza: {NivelEnPlaza(L, t.Edificio)}";
                case "adoptable": return $"Falta adoptar {NombreDeTecnologia(t.Tecnologia)}";
                case "futura": return $"Bloqueada: la Era {Romano[Math.Min(L.A.Nodos[id].Era, Romano.Length - 1)]} aún no empieza";
                default:
                    var f = TecnologiasQueFaltan(L, id);
                    return f.Count == 1 ? $"Falta {NombreDeTecnologia(f[0])}" : $"Faltan {f.Count} tecnologías";
            }
        }

        /// <summary>La caja de estado de la ficha corta y de la ficha en la plaza (`caja` de `lugarPlaza`).</summary>
        string EstadoEnCajaDePlaza(LugarCampamento L, string id, string est)
        {
            var t = L.A.Nodos[id].T;
            var donde = t.Edificio == "centroUrbano" ? "el Centro Urbano" : $"{Pieza(t.Edificio)} {NivelEnPlaza(L, t.Edificio)}";
            var f = TecnologiasQueFaltan(L, id);
            return est switch
            {
                "lista" => $"Lista para reclutar en esta plaza: tienes su tecnología y {donde}.",
                "tuya" => $"Ya tienes su escuadra ({L.Propias[id].Cantidad} de {t.Unidades} hombres). Un héroe tiene una escuadra por tropa.",
                "edificio" => $"Tienes su tecnología, pero esta plaza tiene {Pieza(t.Edificio)} {NivelEnPlaza(L, t.Edificio)} y la tropa pide {Pieza(t.Edificio)} {t.NivelRequerido}.",
                "adoptable" => $"Solo falta adoptar {NombreDeTecnologia(t.Tecnologia)}: ya ha aparecido y el Rey puede adoptarla en la capital.",
                "futura" => $"Bloqueada: la Era {Romano[Math.Min(L.A.Nodos[id].Era, Romano.Length - 1)]} aún no ha empezado en el mundo.",
                _ => $"{(f.Count > 1 ? $"Te faltan {f.Count} tecnologías" : "Te falta 1 tecnología")}: {string.Join(", ", f.Select(NombreDeTecnologia))}.",
            };
        }

        /// <summary>Lo que cuesta reclutar una escuadra en la plaza (`acciones` de `reclutamiento.ts`): el equipo por soldado y el oro de su escalón (y sus
        /// caballos), por los hombres de la escuadra; y de qué clase sale la gente.</summary>
        (JObject coste, string origen, int unidades) CosteEnPlaza(string id)
        {
            var t = c.Datos.TropasReclutables?.FirstOrDefault(x => (string)x["id"] == id) as JObject;
            var unidades = (int?)t?["unidadesPorDefecto"] ?? 0;
            var escalon = (int?)t?["escalon"] ?? 1;
            var origen = (string)t?["poblacion"] ?? (escalon <= 2 ? "pesants" : escalon == 3 ? "artesanos" : "nobleza");
            var porSoldado = new Dictionary<string, double>();
            foreach (var p in (t?["costoEquipo"] as JObject)?.Properties() ?? Enumerable.Empty<JProperty>()) porSoldado[p.Name] = (double)p.Value;
            if ((string)t?["edificio"] != "centroUrbano")
            {
                var oro = (double?)DatoDeJuego("Heroe_Y_Tropa", "RECLUTAMIENTO_ORO_POR_ESCALON")?[escalon.ToString()] ?? 0;
                oro += ((double?)t?["caballos"] ?? 0) * ((double?)DatoDeJuego("Heroe_Y_Tropa", "ORO_POR_CABALLO") ?? 0);
                if (oro > 0) porSoldado["oro"] = (porSoldado.TryGetValue("oro", out var o) ? o : 0) + oro;
            }
            var coste = new JObject(porSoldado.Select(kv => new JProperty(kv.Key, Math.Ceiling(kv.Value * unidades))).Where(p => (double)p.Value > 0));
            return (coste, origen, unidades);
        }

        /// <summary>«Por soldado» en la ficha corta de la plaza: el equipo y el oro de cada soldado y la gente de la que sale.</summary>
        void PorSoldadoEnPlaza(VisualElement tip, LugarCampamento L, string id)
        {
            // Como el prototipo (`tipTropa`): cada pieza del equipo con su icono y su cifra y de dónde sale (`cadena`), el oro y la gente.
            var t = c.Datos.TropasReclutables?.FirstOrDefault(x => (string)x["id"] == id) as JObject;
            var (_, origen, _) = CosteEnPlaza(id);
            tip.Add(Dialogo.Texto("POR SOLDADO", "recl-tip-seccion"));
            var equipo = (t?["costoEquipo"] as JObject)?.Properties().ToList() ?? new List<JProperty>();
            if (equipo.Count == 0) tip.Add(Renglon("", "Sin equipo"));
            foreach (var p in equipo) tip.Add(CadenaDeRecurso(L.A, p.Name, ChipDeTec(p.Name, (double)p.Value)));
            var porEscalon = DatoDeJuego("Heroe_Y_Tropa", "RECLUTAMIENTO_ORO_POR_ESCALON");
            var porCaballo = (double?)DatoDeJuego("Heroe_Y_Tropa", "ORO_POR_CABALLO");
            if (porEscalon != null && porCaballo != null)
            {
                var centro = (string)t?["edificio"] == "centroUrbano";
                var caballos = (int?)t?["caballos"] ?? 0;
                var oro = centro ? 0 : ((double?)porEscalon[((int?)t?["escalon"] ?? 1).ToString()] ?? 0) + caballos * porCaballo.Value;
                tip.Add(Renglon("", ChipDeTec("oro", oro), centro ? "(la leva del Centro Urbano no paga oro)" : caballos > 0 ? $"(con {caballos} caballo{(caballos > 1 ? "s" : "")})" : ""));
            }
            var gente = Dialogo.Caja("tec-rec");
            gente.tooltip = $"{origen} libres de la plaza";
            if (Glifo(origen, 14) is VisualElement g) gente.Add(g);
            gente.Add(Dialogo.Texto("1", "tec-rec-cifra"));
            tip.Add(Renglon("", gente, $"{(origen == "pesants" ? "campesino" : origen == "artesanos" ? "artesano" : "noble")} libre de la plaza"));
        }

        /// <summary>Un recurso del equipo y de dónde sale (`cadena` del prototipo): su edificio y nivel, la tecnología y lo que consume; o, si es
        /// materia prima, de dónde se saca.</summary>
        VisualElement CadenaDeRecurso(ArbolDeTropas a, string r, VisualElement pre)
        {
            var rc = a.RecetaDe(r);
            if (rc == null)
            {
                var b = ArbolDeTropas.Bruto(r);
                if (b == null) return Renglon("", pre);
                // La Leñera y la Granja, con su ficha de edificio (`PI` del prototipo); los pueblos, por su nombre.
                var edificio = r == "madera" ? "lenera" : r == "trigo" ? "granja" : null;
                return Renglon("", pre, "de", edificio != null ? ChipDeEdificio(edificio) : (object)b.Value.donde,
                    b.Value.tec != null ? $"(pide <b>{NombreDeTecnologia(b.Value.tec)}</b>)" : "");
            }
            return Renglon("", pre, ChipDeEdificio(rc.Edificio, rc.Nivel.ToString()), rc.Tec != null ? $"con <b>{NombreDeTecnologia(rc.Tec)}</b>" : "", "· sale de",
                rc.Cantidades.Select(kv => ChipDeTec(kv.Key, kv.Value)));
        }

        /// <summary>Quien recluta en la plaza (`permisoDeReclutar`): «todo» si reside; «ciudadano» si es de su Facción, no reside y no está vetado (su
        /// escuadra nueva va a su columna); «soloReponer» si además la plaza lo cerró; «no» en otro caso.</summary>
        string PermisoDeReclutar(Asentamiento a, Heroe h)
        {
            if (Reside(a, h)) return "todo";
            if (!Propia(a, h) || a.VetadosIds.Contains(h.Id)) return "no";
            return a.ReclutamientoCerradoACiudadanos ? "soloReponer" : "ciudadano";
        }

        /// <summary>«Reclutar N» en la ficha de la tropa elegida, con su coste del almacén de la plaza (en rojo lo que falta) y la gente libre; o por qué no.</summary>
        void ReclutarEnLaPlaza(VisualElement acciones, LugarCampamento L, string id, string est, Heroe h, Action<string> error, Action repintar)
        {
            if (est != "lista") return;
            var a = L.Plaza;
            var permiso = PermisoDeReclutar(a, h);
            string Nota(string texto) { var n = Dialogo.Texto(texto, "campo-ayuda"); n.name = "motivo-" + id; acciones.Add(n); return null; }
            if (permiso == "no") { Nota(a.VetadosIds.Contains(h.Id) ? "Estás vetado en esta plaza: aquí no reclutas." : "Solo quien reside en esta plaza o es ciudadano de su Facción recluta aquí."); return; }
            if (permiso == "soloReponer") { Nota("Esta plaza ha cerrado el reclutamiento a los ciudadanos que no residen en ella: aquí solo repones las escuadras que ya tienes (en tu Base)."); return; }
            var (coste, origen, unidades) = CosteEnPlaza(id);
            string liderazgo = null;
            if (permiso == "ciudadano")
            {
                if (ColumnaDelHeroe(h) == null) { Nota("Sin residir aquí, la escuadra nueva va a la columna con la que entras, y has entrado sin ella."); return; }
                var fuera = EscuadrasDelHeroe(h).Where(s => s.Contenedor?.DondeCase != ContenedorEscuadron.DondeOneofCase.Campamento
                    && !(s.Contenedor?.DondeCase == ContenedorEscuadron.DondeOneofCase.AsentamientoId && s.Contenedor.AsentamientoId == a.Id)).Sum(s => LiderazgoDe(s.TropaId) ?? 0);
                var usado = fuera + (LiderazgoDe(id) ?? 0);
                if (usado > h.LiderazgoBase) liderazgo = $"No cabe en tu Liderazgo ({usado} / {h.LiderazgoBase})";
            }
            var libres = a.Poblacion == null ? 0 : Math.Floor(Valor(origen == "artesanos" ? a.Poblacion.Artesanos : origen == "nobleza" ? a.Poblacion.Nobleza : a.Poblacion.Pesants));
            var faltaRec = coste.Properties().Any(p => Hay(a, p.Name) < (double)p.Value);
            var faltaGente = libres < unidades;
            var motivo = liderazgo ?? (faltaRec ? "El almacén de la plaza no tiene bastante." : faltaGente ? $"No hay {unidades} {origen} libres en la plaza." : null);
            var gente = Dialogo.Texto($"{libres} {origen} libres", "plaza-obra-chip" + (faltaGente ? " baja" : ""));
            gente.name = "gente-libre";
            acciones.Add(gente);
            if (permiso == "ciudadano") Nota(liderazgo != null ? $"{liderazgo}: la escuadra nueva va a tu columna." : "Va a tu columna y come de sus víveres; la paga la plaza.");
            Button reclutar = null;
            reclutar = Dialogo.Boton("", "boton-primario base-reponer", async () =>
                await OrdenDelPanel(new Orden { ReclutarTropa = new OrdenReclutarTropa { AsentamientoId = a.Id, HeroeId = h.Id, TropaId = id } }, error, repintar, reclutar));
            reclutar.name = "reclutar-" + id;
            reclutar.Add(Dialogo.Texto($"Reclutar {unidades}", "boton-texto"));
            if (coste.Count > 0) reclutar.Add(ChipsDeCoste(a, coste));
            reclutar.SetEnabled(motivo == null);
            acciones.Add(reclutar);
            if (motivo != null) Nota(motivo);
        }

        /// <summary>El reclutamiento de la plaza: la misma ventana que la tropa del campamento, con la gente libre de la plaza arriba, el interruptor del
        /// Gobernador y el General y, a quien no reside, cómo recluta aquí.</summary>
        void PintarTropaDeLaPlaza(VisualElement v, VisualElement tip, string plazaId, Action cerrar, Action repintar)
        {
            var plaza = PlazaActual(plazaId);
            var h = HeroeDelCampamento();
            var a = ArbolDelBalance();
            // La ventana del árbol cuelga encima de todo, fuera del HUD de la plaza: se lleva sus estilos (el interruptor y sus colores).
            if (Resources.Load<StyleSheet>("MundoUI/Plaza") is StyleSheet hoja && !v.styleSheets.Contains(hoja)) v.styleSheets.Add(hoja);
            v.AddToClassList("plaza");
            var error = Dialogo.Texto("", "error oculto");
            error.name = "error";
            void Error(string t) => Poner(error, t);
            var cab = Dialogo.Caja("recl-cabecera");
            var titulo = IconosDelMundo.ConIcono("edificio", "barracon", Dialogo.Texto("RECLUTAMIENTO", "dialogo-titulo"), 26);
            titulo.AddToClassList("titulo-con-icono");
            cab.Add(titulo);
            var chips = Dialogo.Caja("fila-envuelta recl-chips");
            chips.name = "chips";
            cab.Add(chips);
            cab.Add(Dialogo.Caja("recl-hueco"));
            var cerrarBoton = Dialogo.Boton("Cerrar", "boton-secundario", cerrar);
            cerrarBoton.name = "cerrar";
            cab.Add(cerrarBoton);
            v.Add(cab);
            if (plaza == null || h == null || a == null) { v.Add(error); return; }
            if (plaza.Poblacion != null)
                foreach (var (k, nombre) in new[] { ("pesants", "pesants"), ("artesanos", "artesanos"), ("nobleza", "nobleza") })
                {
                    var n = Math.Floor(Valor(k == "pesants" ? plaza.Poblacion.Pesants : k == "artesanos" ? plaza.Poblacion.Artesanos : plaza.Poblacion.Nobleza));
                    var chip = Dialogo.Caja("chip");
                    if (Glifo(k, 16) is VisualElement icono) chip.Add(icono);
                    chip.Add(Dialogo.Texto(n.ToString(Es), "chip-texto"));
                    chip.tooltip = $"{nombre} libres";
                    chips.Add(chip);
                }
            // El interruptor del Gobernador y el General residentes: encendido, los ciudadanos que no residen solo reponen.
            if (Reside(plaza, h) && (TitularDe(plaza, "gobernador") == h.Id || TitularDe(plaza, "general") == h.Id))
            {
                var cerrado = plaza.ReclutamientoCerradoACiudadanos;
                v.Add(Interruptor("cerrar-reclutamiento", "Cerrar el reclutamiento a los ciudadanos", null, cerrado, "solo reponen", "reclutan a su columna", "recl-cierre",
                    async si => { var ok = await OrdenDePlaza(new Orden { CerrarReclutamientoACiudadanos = new OrdenCerrarReclutamientoACiudadanos { AsentamientoId = plaza.Id, Cerrado = si } }); if (ok) repintar(); return ok; }));
            }
            var permiso = PermisoDeReclutar(plaza, h);
            var nota = permiso == "todo" ? null
                : permiso == "soloReponer" ? "Esta plaza ha cerrado el reclutamiento a los ciudadanos que no residen en ella: aquí solo repones las escuadras que ya tienes (en tu Base)."
                : permiso == "no" ? (plaza.VetadosIds.Contains(h.Id) ? "Estás vetado en esta plaza: aquí no reclutas." : null)
                : ColumnaDelHeroe(h) != null ? "No resides aquí: la escuadra nueva que reclutes va a tu columna, come de sus víveres y la paga la plaza."
                : "No resides aquí y has entrado sin columna: la escuadra nueva iría a ella, así que para reclutar entra con tu columna.";
            if (nota != null) v.Add(Dialogo.Texto(nota, "campo-ayuda recl-nota"));
            ArbolDelLugar(v, tip, a, LugarDePlaza(plaza, h, a), h, error, Error, repintar);
        }

        // ─────────────────────────── la Armería ───────────────────────────

        JObject Objetos => c.Datos.Bloque("Objetos_Del_Heroe", "OBJETOS") as JObject;
        IEnumerable<JObject> CatalogoDeObjetos => (Objetos?["objetos"] as JArray)?.OfType<JObject>() ?? Enumerable.Empty<JObject>();

        /// <summary>Tu grupo respecto de la plaza (`grupoEnPlaza`): tu Facción, aliados (alianza o vasallaje), enemigos (guerra) o neutrales.</summary>
        string GrupoEnPlaza(Asentamiento a, Heroe yo)
        {
            if (Propia(a, yo)) return "misma";
            if (!yo.HasFaccionId || string.IsNullOrEmpty(a.FaccionId)) return "neutrales";
            var activas = c.Sesion.Almacen.Todas(Entidad.CuerpoOneofCase.Relacion).Select(e => e.Relacion)
                .Where(r => r.Estado == EstadoRelacion.RelacionActiva && (r.FaccionAId == yo.FaccionId && r.FaccionBId == a.FaccionId || r.FaccionAId == a.FaccionId && r.FaccionBId == yo.FaccionId)).ToList();
            if (activas.Any(r => r.Tipo == TipoRelacion.RelacionGuerra)) return "enemigos";
            if (activas.Any(r => r.Tipo == TipoRelacion.RelacionAlianza || r.Tipo == TipoRelacion.RelacionVasallaje)) return "aliados";
            return "neutrales";
        }

        /// <summary>La forja para un grupo: la misma Facción, siempre abierta y sin recargo; los de fuera, lo que puso la plaza (de partida, cerrada).</summary>
        (bool abierta, uint recargo) AperturaPara(Asentamiento a, string grupo)
        {
            if (grupo == "misma") return (true, 0);
            var ap = grupo == "aliados" ? a.Forja?.Aliados : grupo == "enemigos" ? a.Forja?.Enemigos : a.Forja?.Neutrales;
            return ap != null ? (ap.Abierta, ap.RecargoPct) : (false, (uint?)Objetos?["forja"]?["recargoInicialPct"] ?? 100);
        }

        long? ImpuestoDeForja(int tier, uint recargo) =>
            (long?)Objetos?["forja"]?["impuestoPorTier"]?[tier.ToString()] is long b ? b * (100 + recargo) / 100 : (long?)null;

        int OrdenDeEraDelMundo()
        {
            var era = c.Sesion.Almacen.Buscar(TipoEntidad.EstadoTecnologia, "tecnologia")?.EstadoTecnologia?.Era;
            var eras = (c.Datos.Raiz["documentos"] as JObject)?.Properties().Select(p => p.Value["ERAS"]).FirstOrDefault(x => x != null);
            return era != null ? (int?)eras?[era]?["orden"] ?? 1 : 1;
        }

        int EraDelTier(int tier) => (int?)Objetos?["forja"]?["eraPorTier"]?[tier.ToString()] ?? 0;

        static bool MetalAbierto(Asentamiento a, JObject f) =>
            (string)f["tipo"] == "montura" || string.IsNullOrEmpty((string)f["metal"]) || (string)f["metal"] == "inicio" || a.MetalesArmeria.Contains((string)f["metal"]);

        /// <summary>La clase del arma que lleva (o la suya) y los pesos de armadura que admite.</summary>
        (string clase, List<string> pesos) ClaseDeArma(Heroe h)
        {
            var clase = (string)FichaDelMotor(h.Equipamiento?.Arma?.ItemDefinitionId ?? "")?["clase"] ?? h.ClassDefinitionId;
            var pesos = ((DatoDeJuego("Objetos_Del_Heroe", "CLASES_HEROE")?["clases"] as JArray)?.OfType<JObject>().FirstOrDefault(x => (string)x["id"] == clase)?["armaduras"] as JArray)?
                .Select(x => (string)x).ToList() ?? new List<string>();
            return (clase, pesos);
        }

        bool DeMiClase(Heroe h, JObject f)
        {
            var (clase, pesos) = ClaseDeArma(h);
            return (string)f["tipo"] switch { "arma" => (string)f["clase"] == clase, "armadura" => pesos.Contains((string)f["tipoArmadura"]), _ => true };
        }

        static readonly string[] HuecoOrden = { "arma", "casco", "torso", "guantes", "pantalones", "botas", "montura" };
        static readonly string[] PesoOrden = { "ligera", "media", "pesada" };
        static IOrderedEnumerable<JObject> Ordenar(IEnumerable<JObject> l) => l
            .OrderBy(f => (int?)f["tier"] ?? 0).ThenBy(f => Array.IndexOf(HuecoOrden, (string)f["hueco"] ?? (string)f["tipo"]))
            .ThenBy(f => Array.IndexOf(PesoOrden, (string)f["tipoArmadura"] ?? "")).ThenBy(f => (string)f["nombre"], StringComparer.Create(Es, false));

        /// <summary>Las pestañas de la Armería para ti: la tienda siempre; la forja si está abierta a tu grupo; abrirla, el Tesorero o el Gobernador
        /// residentes y dentro.</summary>
        List<(string id, string nombre)> PestanasDeArmeria(Asentamiento a, Heroe yo)
        {
            var l = new List<(string, string)> { ("tienda", "Tienda") };
            if (AperturaPara(a, GrupoEnPlaza(a, yo)).abierta) l.Add(("forja", "Forja"));
            if (Presente(a, yo) && Reside(a, yo) && (TitularDe(a, "tesorero") == yo.Id || TitularDe(a, "gobernador") == yo.Id)) l.Add(("abrir", "Abrir la forja"));
            return l;
        }

        static string Miles2(double n) => Math.Floor(n).ToString("#,0", Es);

        /// <summary>Un objeto del catálogo como pieza (para su imagen y su ficha de Conquest).</summary>
        static ItemInstancia ComoPieza(JObject f) => new ItemInstancia { ItemDefinitionId = (string)f["id"], Tipo = (string)f["tipo"], Cantidad = 1, Precio = (uint?)f["precio"] ?? 0 };

        /// <summary>La pieza de una fila: su imagen con su rareza, su nombre y lo que es; al pasar el ratón, su ficha de Conquest.</summary>
        VisualElement PiezaDeArmeria(JObject f, string extra = null)
        {
            var fila = Dialogo.Caja("plaza-arm-pieza");
            var tier = (int?)f["tier"] ?? 1;
            var marco = Dialogo.Caja("plaza-arm-pieza-icono equipo-rareza-" + Mathf.Clamp(tier, 1, 5));
            if (ImagenDe(ComoPieza(f), 30) is VisualElement img) marco.Add(img);
            fila.Add(marco);
            var textos = Dialogo.Caja("plaza-arm-pieza-texto");
            textos.Add(Dialogo.Texto((string)f["nombre"], "plaza-arm-pieza-nombre"));
            var metal = (string)f["metal"];
            textos.Add(Dialogo.Texto($"{TipoDeObjeto(f, ComoPieza(f))}{(!string.IsNullOrEmpty(metal) && metal != "inicio" ? $" · {metal.Replace('_', ' ')}" : "")} · tier {Romano[Mathf.Clamp(tier, 1, 5)]}{extra}", "plaza-nota"));
            fila.Add(textos);
            var pieza = ComoPieza(f);
            ConFicha(fila, () => pieza);
            return fila;
        }

        /// <summary>La Armería a pantalla completa (`htmlArmeria` con la mezcla de la duda 6): a la izquierda la mochila en casillas (al elegir una,
        /// «Vender»); a la derecha, la Tienda, la Forja o abrir la forja.</summary>
        void Armeria(VisualElement cuerpo, Asentamiento a, Heroe yo, string sub, Action rehacer)
        {
            if (Objetos == null) { cuerpo.Add(Dialogo.Texto("Cargando el catálogo de objetos…", "plaza-nota")); return; }
            var error = Dialogo.Texto("", "plaza-error");
            error.name = "error-armeria";
            var cols = Dialogo.Caja("plaza-cols");
            var izq = Dialogo.Caja("plaza-col plaza-arm-mochila");
            var der = Dialogo.Caja("plaza-col");
            DosColumnas(cols, izq, der, 900, 22);
            cuerpo.Add(cols);
            cuerpo.Add(error);
            MochilaDeArmeria(izq, a, yo, error, rehacer);
            var g = GrupoEnPlaza(a, yo);
            if (sub == "forja") ForjaDeArmeria(der, a, yo, g, error, rehacer);
            else if (sub == "abrir") AbrirLaForja(der, a, error, rehacer);
            else
            {
                if (!AperturaPara(a, g).abierta) der.Add(Dialogo.Texto($"La forja de esta plaza está cerrada a los {g}: aquí solo se compra y se vende.", "plaza-nota"));
                TiendaDeArmeria(der, a, yo, error, rehacer);
            }
        }

        async Task<bool> OrdenDeArmeria(Orden o, Label error, VisualElement boton) => await OrdenDeCarga(o, error, boton);

        /// <summary>La mochila en casillas, con su rareza y su ficha al pasar; al elegir una, su nombre, lo que te pagan por ella y «Vender».</summary>
        void MochilaDeArmeria(VisualElement col, Asentamiento a, Heroe yo, Label error, Action rehacer)
        {
            var casillas = (int?)Objetos?["casillasMochila"] ?? 72;
            var venta = (int?)Objetos?["ventaPct"] ?? 50;
            var saldo = Dialogo.Caja("plaza-arm-saldo");
            saldo.Add(IconosDelMundo.Icono("glifo", "monedas", 16));
            saldo.Add(Dialogo.Texto($"{Miles2(yo.Monedas)} monedas", "plaza-arm-saldo-texto"));
            saldo.name = "monedas";
            col.Add(TituloDeSeccion("Mochila", $"Al elegir un objeto, «Vender» te paga el {venta} % del precio de su ficha. Una pieza forjada vale lo mismo que la de tienda. Lo que llevas puesto no se vende: quítatelo antes.", saldo));
            var porCasilla = yo.Inventario.GroupBy(x => (int)x.CasillaInventario).ToDictionary(g => g.Key, g => g.First());
            col.Add(Dialogo.Texto($"{porCasilla.Count} / {casillas} casillas", "bienes-cuenta"));
            var rejilla = Dialogo.Caja("bienes-rejilla plaza-arm-rejilla");
            rejilla.name = "rejilla-armeria";
            col.Add(rejilla);
            for (int i = 0; i < casillas; i++)
            {
                if (!porCasilla.TryGetValue(i, out var pieza)) { rejilla.Add(Dialogo.Caja("bienes-casilla bienes-casilla--vacia plaza-arm-casilla")); continue; }
                var casilla = i;
                var b = new Button(() => { casillaArmeria = casillaArmeria == casilla ? -1 : casilla; rehacer(); }) { name = "mochila-" + i };
                b.AddToClassList("bienes-casilla");
                b.AddToClassList("plaza-arm-casilla");
                b.EnableInClassList("activo", casillaArmeria == i);
                var (_, tier, rareza) = FichaDeObjeto(pieza);
                b.AddToClassList("equipo-rareza-" + tier);
                if (ImagenDe(pieza, 26) is VisualElement img) b.Add(img);
                var linea = Dialogo.Caja("equipo-linea");
                linea.style.backgroundColor = InventoryUtils.GetRarityColor(rareza);
                b.Add(linea);
                if (pieza.Cantidad > 1) b.Add(Dialogo.Texto(pieza.Cantidad.ToString(), "bienes-casilla-cantidad"));
                var esta = pieza;
                ConFicha(b, () => esta);
                rejilla.Add(b);
            }
            if (casillaArmeria >= 0 && porCasilla.TryGetValue(casillaArmeria, out var sel))
            {
                var f = FichaDelMotor(sel.ItemDefinitionId);
                var precio = (long)((((long?)f?["precio"] ?? sel.Precio) * venta) / 100);
                var barra = Dialogo.Caja("bienes-elegido");
                barra.name = "elegido-armeria";
                barra.Add(Dialogo.Texto($"{FichaDeObjeto(sel).nombre}{(sel.Cantidad > 1 ? $" ×{sel.Cantidad}" : "")}{(sel.HasItemInstanceId ? " (forjada)" : "")}", "bienes-elegido-nombre"));
                barra.Add(Dialogo.Texto($"{Miles2(precio)} monedas{(sel.Cantidad > 1 ? " c/u" : "")}", "plaza-nota"));
                IntegerField cuantas = null;
                if (sel.Cantidad > 1)
                {
                    cuantas = new IntegerField { value = 1, name = "vender-cantidad" };
                    cuantas.AddToClassList("plaza-carro-cantidad");
                    barra.Add(cuantas);
                }
                var casilla = (uint)casillaArmeria;
                Button vender = null;
                vender = Dialogo.Boton("Vender", "boton-secundario base-boton-fila", async () =>
                {
                    var n = (uint)Mathf.Clamp(cuantas?.value ?? 1, 1, (int)sel.Cantidad);
                    if (await OrdenDeArmeria(new Orden { VenderObjeto = new OrdenVenderObjeto { Casilla = casilla, Cantidad = n } }, error, vender)) { casillaArmeria = -1; rehacer(); }
                });
                vender.name = "vender";
                barra.Add(vender);
                col.Add(barra);
            }
        }

        void SoloDeMiClase(VisualElement col, Heroe yo, Action rehacer)
        {
            var (clase, _) = ClaseDeArma(yo);
            var nombreClase = (string)((DatoDeJuego("Objetos_Del_Heroe", "CLASES_HEROE")?["clases"] as JArray)?.OfType<JObject>().FirstOrDefault(x => (string)x["id"] == clase)?["nombre"]) ?? clase;
            var solo = new Toggle($"Solo lo de tu clase ({nombreClase})") { value = soloMiClase, name = "solo-mi-clase" };
            solo.AddToClassList("plaza-carga-cierre");
            solo.RegisterValueChangedCallback(ev => { soloMiClase = ev.newValue; rehacer(); });
            col.Add(solo);
        }

        /// <summary>La Tienda (`htmlTienda`): armas, armaduras y monturas hasta `tiendaHastaTier`, con el metal que abre la Facción de la plaza; filtros por
        /// tipo y «solo lo de tu clase»; cada una con su precio y «Comprar» (o lo que te falta).</summary>
        void TiendaDeArmeria(VisualElement col, Asentamiento a, Heroe yo, Label error, Action rehacer)
        {
            var hasta = (int?)Objetos?["tiendaHastaTier"] ?? 2;
            var casillas = (int?)Objetos?["casillasMochila"] ?? 72;
            var libres = casillas - yo.Inventario.Count;
            var todas = CatalogoDeObjetos.Where(f => ((string)f["tipo"] == "arma" || (string)f["tipo"] == "armadura" || (string)f["tipo"] == "montura")
                && (int?)f["tier"] is int t && t <= hasta && MetalAbierto(a, f)).ToList();
            var tipos = new[] { ("arma", "Armas"), ("armadura", "Armaduras"), ("montura", "Monturas") }.Where(x => todas.Any(f => (string)f["tipo"] == x.Item1)).ToList();
            if (!tipos.Any(x => x.Item1 == tipoTienda)) tipoTienda = tipos.FirstOrDefault().Item1 ?? "armadura";
            col.Add(TituloDeSeccion("Comprar", $"Al 100 %, con tus monedas, y a tu mochila. Vende armas, armaduras y monturas hasta el tier {hasta}, con los metales que abre la tecnología de la Facción de esta plaza. Lo mejor se forja."));
            if (libres < 1) col.Add(Dialogo.Texto("La mochila está llena: vende algo o guárdalo en tu almacén de objetos antes de comprar.", "plaza-nota"));
            var filtros = Dialogo.Caja("plaza-arm-filtros");
            foreach (var (t, nombre) in tipos)
            {
                var tipo = t;
                var b = Dialogo.Boton(nombre, "plaza-arm-chip" + (t == tipoTienda ? " activo" : ""), () => { tipoTienda = tipo; rehacer(); });
                b.name = "tipo-" + t;
                filtros.Add(b);
            }
            col.Add(filtros);
            if (tipoTienda != "montura") SoloDeMiClase(col, yo, rehacer);
            var lista = Dialogo.Caja("plaza-arm-lista");
            lista.name = "tienda";
            foreach (var f in Ordenar(todas.Where(f => (string)f["tipo"] == tipoTienda && (!soloMiClase || DeMiClase(yo, f)))))
            {
                var precio = (long?)f["precio"] ?? 0;
                var falta = precio - (long)yo.Monedas;
                var fila = Dialogo.Caja("plaza-arm-fila");
                fila.name = "tienda-" + (string)f["id"];
                fila.Add(PiezaDeArmeria(f));
                var p = Dialogo.Caja("plaza-arm-precio" + (falta > 0 ? " caro" : ""));
                p.Add(IconosDelMundo.Icono("glifo", "monedas", 14));
                p.Add(Dialogo.Texto(Miles2(precio), "plaza-arm-precio-texto"));
                fila.Add(p);
                if (libres >= 1)
                {
                    if (falta > 0) fila.Add(Dialogo.Texto($"te faltan {Miles2(falta)}", "plaza-arm-falta"));
                    else
                    {
                        var id = (string)f["id"];
                        Button comprar = null;
                        comprar = Dialogo.Boton("Comprar", "boton-primario plaza-arm-boton", async () =>
                        {
                            if (await OrdenDeArmeria(new Orden { ComprarObjeto = new OrdenComprarObjeto { Objeto = id } }, error, comprar)) rehacer();
                        });
                        comprar.name = "comprar";
                        fila.Add(comprar);
                    }
                }
                lista.Add(fila);
            }
            if (lista.childCount == 0) lista.Add(Dialogo.Texto("Nada de esto a la venta aquí.", "plaza-vacio"));
            col.Add(lista);
        }

        /// <summary>Lo que hay de una pieza en la mochila (todas sus casillas).</summary>
        static long EnMochila(Heroe h, string id) => h.Inventario.Where(x => x.ItemDefinitionId == id).Sum(x => (long)x.Cantidad);

        /// <summary>Los materiales a mano para forjar (`materialesAMano`): en tu residencia, tu almacén personal y tu carro; en otra plaza, el carro de la
        /// columna con la que entraste.</summary>
        (Dictionary<string, double> recursos, string donde) MaterialesAMano(Heroe h, bool reside)
        {
            var columna = ColumnaDelHeroe(h);
            var recursos = new Dictionary<string, double>();
            void Sumar(Cantidades q) { if (q != null) foreach (var kv in q.PorId) recursos[kv.Key] = (recursos.TryGetValue(kv.Key, out var x) ? x : 0) + kv.Value / 1000.0; }
            if (reside) Sumar(h.AlmacenPersonal);
            Sumar(columna != null ? columna.Suministro : reside ? h.FueraCarro : null);
            var donde = reside ? $"de tu almacén personal y, lo que falte, de {(columna != null ? "el carro de tu columna" : "tu carro")}" : columna != null ? "del carro de la columna con la que entraste" : "de ningún sitio: no entraste con tu columna";
            return (recursos, donde);
        }

        /// <summary>La Forja (`htmlForja`): las piezas con receta (tiers II a V) de los metales que abre la Armería y del tier que abrió la Era del mundo;
        /// cada una con su plano, sus crisoles, sus materiales y el impuesto (en rojo lo que falta) y «Forjar» o lo que te falta.</summary>
        void ForjaDeArmeria(VisualElement col, Asentamiento a, Heroe yo, string grupo, Label error, Action rehacer)
        {
            var (abierta, recargo) = AperturaPara(a, grupo);
            if (!abierta) return;
            var reside = Reside(a, yo);
            var (mano, donde) = MaterialesAMano(yo, reside);
            var era = OrdenDeEraDelMundo();
            var casillas = (int?)Objetos?["casillasMochila"] ?? 72;
            var libres = casillas - yo.Inventario.Count;
            var nombreGrupo = grupo switch { "misma" => "miembro de la Facción", "aliados" => "aliado", "enemigos" => "enemigo", _ => "neutral" };
            var tiers = new[] { 2, 3, 4, 5 }.Where(t => EraDelTier(t) <= era).ToList();
            var saldo = Dialogo.Caja("plaza-arm-saldo");
            saldo.Add(IconosDelMundo.Icono("glifo", "monedas", 16));
            saldo.Add(Dialogo.Texto($"{Miles2(yo.Monedas)} monedas", "plaza-arm-saldo-texto"));
            col.Add(TituloDeSeccion("Forja", "Gasta un plano de su tier y sus crisoles de tu mochila, los materiales de la receta y el impuesto. La pieza sale con una calidad al azar en su rango, que multiplica todas sus cifras, y va a tu mochila.", saldo));
            col.Add(Dialogo.Texto($"Forjas aquí como {nombreGrupo}: pagas el impuesto base{(recargo > 0 ? $" con un recargo del {recargo} %" : "")}, que va al fondo de la plaza. Los materiales salen {donde}. La Era del mundo abre {(tiers.Count > 1 ? "los tiers " + string.Join(", ", tiers.Select(t => Romano[t])) : "el tier II")}.", "plaza-nota"));
            SoloDeMiClase(col, yo, rehacer);
            var recetas = Ordenar(CatalogoDeObjetos.Where(f => f["forja"] is JObject && (int?)f["tier"] is int t && t >= 2 && MetalAbierto(a, f) && EraDelTier(t) <= era && (!soloMiClase || DeMiClase(yo, f))))
                .Select(f =>
                {
                    var x = (JObject)f["forja"];
                    var tier = (int)f["tier"];
                    var impuesto = ImpuestoDeForja(tier, recargo) ?? 0;
                    var plano = $"plano_{x["plano"]}";
                    var crisolesPide = (int?)x["crisoles"] ?? 0;
                    long planos = EnMochila(yo, plano), crisoles = EnMochila(yo, "crisol");
                    var faltan = new List<string>();
                    if (planos < 1) faltan.Add($"un {(string)FichaDelMotor(plano)?["nombre"] ?? plano}");
                    if (crisoles < crisolesPide) faltan.Add($"{crisolesPide - crisoles} {(crisolesPide - crisoles == 1 ? "crisol" : "crisoles")}");
                    var sueltan = (planos == 1 ? 1 : 0) + (crisolesPide > 0 && crisoles == crisolesPide ? 1 : 0);
                    if (libres + sueltan < 1) faltan.Add("una casilla libre en la mochila");
                    foreach (var m in (x["materiales"] as JObject)?.Properties() ?? Enumerable.Empty<JProperty>())
                    {
                        var hay = Math.Floor(mano.TryGetValue(m.Name, out var n) ? n : 0);
                        if (hay < (double)m.Value) faltan.Add($"{(double)m.Value - hay} de {CosasDelMapa.NombreDeRecurso(m.Name)}");
                    }
                    if ((long)yo.Monedas < impuesto) faltan.Add($"{Miles2(impuesto - (long)yo.Monedas)} monedas");
                    return (f, x, impuesto, plano, crisolesPide, planos, crisoles, faltan);
                }).ToList();
            VisualElement Fila((JObject f, JObject x, long impuesto, string plano, int crisolesPide, long planos, long crisoles, List<string> faltan) r)
            {
                var fila = Dialogo.Caja("plaza-arm-fila plaza-arm-fila-forja");
                fila.name = "forja-" + (string)r.f["id"];
                var calidad = r.x["calidad"] as JArray;
                fila.Add(PiezaDeArmeria(r.f, calidad != null ? $" · calidad {calidad[0]} a {calidad[1]} %" : null));
                var costes = Dialogo.Caja("plaza-arm-costes");
                VisualElement Coste(VisualElement icono, string cifra, string nombre, bool falta)
                {
                    var chip = Dialogo.Caja("plaza-coste" + (falta ? " falta" : ""));
                    if (icono != null) chip.Add(icono);
                    chip.Add(Dialogo.Texto(cifra, "plaza-coste-cifra"));
                    chip.tooltip = nombre;
                    return chip;
                }
                costes.Add(Coste(IconosDelMundo.Icono("glifo", "aeda", 14), "1", (string)FichaDelMotor(r.plano)?["nombre"] ?? r.plano, r.planos < 1));
                costes.Add(Coste(IconosDelMundo.Icono("recurso", "aceite", 14), r.crisolesPide.ToString(), r.crisolesPide == 1 ? "crisol" : "crisoles", r.crisoles < r.crisolesPide));
                foreach (var m in (r.x["materiales"] as JObject)?.Properties() ?? Enumerable.Empty<JProperty>())
                    costes.Add(Coste(IconosDelMundo.Icono("recurso", m.Name, 14), Cifra((double)m.Value), CosasDelMapa.NombreDeRecurso(m.Name), Math.Floor(mano.TryGetValue(m.Name, out var n) ? n : 0) < (double)m.Value));
                costes.Add(Coste(IconosDelMundo.Icono("glifo", "monedas", 14), Miles2(r.impuesto), "monedas de impuesto", (long)yo.Monedas < r.impuesto));
                fila.Add(costes);
                if (r.faltan.Count == 0)
                {
                    var id = (string)r.f["id"];
                    Button forjar = null;
                    forjar = Dialogo.Boton("Forjar", "boton-primario plaza-arm-boton", async () =>
                    {
                        if (await OrdenDeArmeria(new Orden { ForjarObjeto = new OrdenForjarObjeto { Objeto = id } }, error, forjar)) rehacer();
                    });
                    forjar.name = "forjar";
                    fila.Add(forjar);
                }
                else fila.Add(Dialogo.Texto($"Te {(r.faltan.Count == 1 && System.Text.RegularExpressions.Regex.IsMatch(r.faltan[0], "^(un|una|1) ") ? "falta" : "faltan")} {string.Join(", ", r.faltan)}.", "plaza-arm-falta"));
                return fila;
            }
            col.Add(TituloDeSeccion("Lo que puedes forjar ya", null));
            var listas = Dialogo.Caja("plaza-arm-lista");
            listas.name = "forja-listas";
            foreach (var r in recetas.Where(r => r.faltan.Count == 0)) listas.Add(Fila(r));
            if (listas.childCount == 0) listas.Add(Dialogo.Texto("Ninguna todavía: mira abajo lo que te falta para cada una.", "plaza-vacio"));
            col.Add(listas);
            var faltanLista = recetas.Where(r => r.faltan.Count > 0).ToList();
            if (faltanLista.Count > 0)
            {
                var plegable = new Foldout { text = $"Las que aún no puedes forjar ({faltanLista.Count})", value = false, name = "forja-faltan" };
                plegable.AddToClassList("plaza-plegable");
                foreach (var r in faltanLista) plegable.Add(Fila(r));
                col.Add(plegable);
            }
        }

        static string TextoRecargo(uint r) => r == 0 ? "sin recargo" : $"+{r} % (×{(100 + r) / 100.0:0.##})";

        /// <summary>Abrir la forja (`htmlAbrirForja`): el fondo de la plaza y, por grupo de fuera, si está abierta y con qué recargo, y «Guardar».</summary>
        void AbrirLaForja(VisualElement col, Asentamiento a, Label error, Action rehacer)
        {
            var recargos = (Objetos?["forja"]?["recargosPct"] as JArray)?.Select(x => (uint)x).ToList() ?? new List<uint> { 0, 100, 200, 500 };
            var fondo = Dialogo.Caja("plaza-arm-saldo");
            fondo.Add(IconosDelMundo.Icono("glifo", "monedas", 16));
            fondo.Add(Dialogo.Texto($"Fondo de la plaza: {Miles2(a.Forja?.Fondo ?? 0)} monedas", "plaza-arm-saldo-texto"));
            ConLetrero(fondo, "Lo cobrado por la forja: paga contratos de mercenarios desde la Taberna");
            col.Add(TituloDeSeccion("Abrir la forja", null, fondo));
            col.Add(Dialogo.Texto("Tu Facción forja siempre, al impuesto base. A cada grupo de fuera puedes abrirle la forja con un recargo sobre ese impuesto; lo cobrado entra en el fondo, con el que el Rey, el Embajador o el Gobernador pagan contratos de mercenarios. Abrir la forja no abre la puerta.", "plaza-nota"));
            var lista = Dialogo.Caja("plaza-arm-lista");
            foreach (var (grupo, familia, icono, nombre) in new[] { ("aliados", "glifo", "gente", "Aliados"), ("neutrales", "glifo", "sitio", "Neutrales"), ("enemigos", "glifo", "atacar", "Enemigos") })
            {
                var clave = $"{a.Id}|{grupo}";
                var actual = AperturaPara(a, grupo);
                var b = borradorForja.TryGetValue(clave, out var x) ? x : actual;
                var cambia = b.abierta != actual.abierta || b.recargo != actual.recargo;
                var fila = Dialogo.Caja("plaza-arm-fila plaza-arm-grupo");
                fila.name = "abrir-" + grupo;
                fila.Add(IconoDe(familia, icono, 22));
                var textos = Dialogo.Caja("plaza-arm-pieza-texto");
                textos.Add(Dialogo.Texto(nombre, "plaza-arm-pieza-nombre"));
                textos.Add(Dialogo.Texto(actual.abierta ? $"Abierta, {TextoRecargo(actual.recargo)}" : "Cerrada", "plaza-nota"));
                var cerradaA = new Dictionary<string, GrupoPuerta> { ["aliados"] = GrupoPuerta.GrupoAliados, ["neutrales"] = GrupoPuerta.GrupoNeutrales, ["enemigos"] = GrupoPuerta.GrupoEnemigos };
                if (a.PuertaCerradaA.Contains(cerradaA[grupo])) textos.Add(Dialogo.Texto($"Tu puerta está cerrada a los {nombre.ToLowerInvariant()}: no entran a forjar.", "plaza-arm-falta"));
                fila.Add(textos);
                var abiertaCombo = new DropdownField("Forja", new List<string> { "Abierta", "Cerrada" }, b.abierta ? 0 : 1) { name = "abierta" };
                abiertaCombo.AddToClassList("plaza-arm-campo");
                abiertaCombo.RegisterValueChangedCallback(_ => { borradorForja[clave] = (abiertaCombo.index == 0, borradorForja.TryGetValue(clave, out var y) ? y.recargo : actual.recargo); rehacer(); });
                fila.Add(abiertaCombo);
                var recargoCombo = new DropdownField("Recargo", recargos.Select(TextoRecargo).ToList(), Math.Max(0, recargos.IndexOf(b.recargo))) { name = "recargo" };
                recargoCombo.AddToClassList("plaza-arm-campo");
                recargoCombo.RegisterValueChangedCallback(_ => { borradorForja[clave] = (borradorForja.TryGetValue(clave, out var y) ? y.abierta : actual.abierta, recargos[recargoCombo.index]); rehacer(); });
                fila.Add(recargoCombo);
                var ejemplo = ImpuestoDeForja(2, b.recargo);
                fila.Add(Dialogo.Texto($"un tier II paga {(ejemplo != null ? Miles2(ejemplo.Value) + " monedas" : "—")}", "plaza-nota"));
                var g = grupo;
                Button guardar = null;
                guardar = Dialogo.Boton("Guardar", "boton-primario plaza-arm-boton", async () =>
                {
                    var d = borradorForja.TryGetValue(clave, out var y) ? y : actual;
                    if (await OrdenDeArmeria(new Orden { ConfigurarForja = new OrdenConfigurarForja { AsentamientoId = a.Id, Grupo = g, Abierta = d.abierta, RecargoPct = d.recargo } }, error, guardar))
                    { borradorForja.Remove(clave); rehacer(); }
                });
                guardar.name = "guardar";
                guardar.SetEnabled(cambia);
                fila.Add(guardar);
                lista.Add(fila);
            }
            col.Add(lista);
        }
    }
}
