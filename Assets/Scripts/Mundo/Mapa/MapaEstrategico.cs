// El mapa estratégico 2D (paso 9 del bloque 4): el MODO MAPA de BronzeAgeClient (`main.ts: alternarVistaMapa`, `render.ts: pintarTerreno`,
// `ui/pantallaMapa.ts`), a pantalla completa encima de cualquier pantalla. El mundo entero con zoom libre entre el mundo y la escala de visión del
// héroe, arrastre, capas y la niebla de la sesión. Es interfaz (UI Toolkit): el suelo y la niebla son texturas estiradas sobre el mundo, las
// rayas se pintan con `Painter2D` y lo que hay en el mapa son iconos (los del prototipo, `IconosDelMundo`).
// El clic lo decide quien lo abre: en el mundo, marchar o elegir (`AlClicar`); eligiendo un sitio (`Eleccion`), mandar la orden del sitio.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BronzeAge.Contract.V1;
using UnityEngine;
using UnityEngine.UIElements;

namespace ConquestTactics.Mundo.Mapa
{
    /// <summary>Elegir un punto en el mapa (`eleccionEnMapa` del prototipo): el texto de la franja, la zona de la plaza, los sitios de aldea y lo
    /// que hace el clic (devuelve el mensaje del rechazo, o null si se aceptó y el mapa se cierra).</summary>
    public sealed class EleccionEnMapa
    {
        public string Texto;
        public List<(double x, double y)> Zona;
        public List<(double x, double y)> SitiosValidos, SitiosCerca;
        public double PasoSitios = 0.5;
        public Func<double, double, Task<string>> AlElegir;
    }

    public sealed class MapaEstrategico : VisualElement
    {
        // Del prototipo: ESCALA_GLIFO (26), ANCHO_VISTA_GENERAL (600), ANCHO_SIGILO (26), el ocre de los marcadores y la niebla (paletas.ts).
        const double EscalaGlifo = 26, AnchoVistaGeneral = 600, AnchoSigilo = 26, RadioVisionPorDefecto = 15;
        const float ArrastreMinimoPx = 5;
        static readonly Color Marcador = new Color32(0xf1, 0xd3, 0x8b, 255), NieblaColor = new Color32(10, 14, 23, 255);
        const byte AlfaSinVer = 255, AlfaRecordado = 158, AlfaVisible = 0;

        readonly Sesion sesion;
        readonly ArtefactoDeMundo artefacto;
        readonly DatosDeJuego datos;
        readonly Func<string> heroeId;
        readonly SueloDelMapa2D suelo;
        readonly double anchoMundo, altoMundo;

        readonly VisualElement lienzo, imagenSuelo, imagenFertilidad, imagenNiebla, iconosBajo, iconosEncima, franja;
        readonly VectorDelMapa arboles, bajo, encima;
        readonly Label franjaError;
        readonly VisualElement listaCapas;

        CamaraDeVista? camara;
        (double x, double y)? foco;
        double radioVision = RadioVisionPorDefecto;
        double factorInicial = 0.25;
        Vector2? pulsado;
        Vector2 ultimo;
        bool arrastrando;
        int punteroId = -1;
        float ultimoCerca = -1;
        (double x0, double y0, double x1, double y1) rectCerca;
        Texture2D niebla;
        NieblaProyectada nieblaPuesta;
        Func<double, double, bool> explorado = (x, y) => true;
        readonly List<SueloDelMapa2D.Arbol> listaArboles = new List<SueloDelMapa2D.Arbol>();
        readonly Dictionary<string, VisualElement> iconos = new Dictionary<string, VisualElement>();
        readonly HashSet<string> vivos = new HashSet<string>();

        /// <summary>Lo que hay en el mapa en el último repintado (sin tu columna).</summary>
        public List<CosaDelMapa> Cosas { get; private set; } = new List<CosaDelMapa>();
        List<RayaDelMapa> rayas = new List<RayaDelMapa>();
        Ejercito columna;
        (double x, double y)? posColumna;
        List<(double x, double y)> rutaColumna = new List<(double, double)>();

        /// <summary>Un clic que no es arrastre, sin elegir sitio: el punto del mundo y los px por unidad de la cámara. Null: solo para mirar.</summary>
        public Func<double, double, double, Task> AlClicar;
        public EleccionEnMapa Eleccion { get; private set; }
        public event Action AlCerrar;
        /// <summary>Las capas: los nombres de los nodos y la fertilidad de un cultivo (null: sin capa).</summary>
        public bool NombresDeNodos = true;
        public string CultivoDeFertilidad;
        /// <summary>Los cultivos con casilla de fertilidad (los que tu Facción ya desbloqueó) y la fertilidad mínima de la aldea.</summary>
        public Func<IReadOnlyList<string>> Cultivos = () => new[] { "trigo" };
        public double FertilidadMinima = 0.3;
        /// <summary>La capa de parcelas de bosque y el estado de cada una («libre», «ocupada» o «ajena»), que da quien abre el mapa.</summary>
        public bool Parcelas;
        /// <summary>La vista previa de fundar (la misma que la del mapa 3D), mientras su panel está abierto; null, nada.</summary>
        public MapaDeMundo.FundacionEnVista Fundacion { get => fundacion; set { fundacion = value; encima?.MarkDirtyRepaint(); } }
        MapaDeMundo.FundacionEnVista fundacion;
        public Func<ParcelaBosque, string> EstadoDeParcela = _ => "libre";

        /// <summary>Puntos de la ruta de tu columna que pinta el mapa (0 sin ruta).</summary>
        public int PuntosDeRuta => rutaColumna.Count;

        public CamaraDeVista Camara => camara ?? new CamaraDeVista(1, anchoMundo / 2, altoMundo / 2);

        public MapaEstrategico(Sesion sesion, ArtefactoDeMundo artefacto, DatosDeJuego datos, Func<string> heroeId, SueloDelMapa2D suelo)
        {
            this.sesion = sesion;
            this.artefacto = artefacto;
            this.datos = datos;
            this.heroeId = heroeId;
            this.suelo = suelo;
            anchoMundo = artefacto.Manifiesto.Tamano.X / 1000.0;
            altoMundo = artefacto.Manifiesto.Tamano.Y / 1000.0;
            name = "mapa-interior";
            AddToClassList("mapa-interior");
            focusable = true;

            lienzo = Caja("mapa-lienzo");
            lienzo.name = "mapa-lienzo";
            lienzo.pickingMode = PickingMode.Position;
            Add(lienzo);
            imagenSuelo = Capa("mapa-suelo");
            arboles = new VectorDelMapa(PintarArboles) { name = "mapa-arboles" };
            bajo = new VectorDelMapa(PintarBajo) { name = "mapa-bajo-niebla" };
            iconosBajo = Capa("mapa-iconos-bajo");
            imagenNiebla = Capa("mapa-niebla");
            imagenFertilidad = Capa("mapa-fertilidad");
            encima = new VectorDelMapa(PintarEncima) { name = "mapa-encima" };
            iconosEncima = Capa("mapa-iconos");
            foreach (var c in new[] { imagenSuelo, arboles, bajo, iconosBajo, imagenNiebla, imagenFertilidad, encima, iconosEncima })
            {
                c.pickingMode = PickingMode.Ignore;
                c.style.position = Position.Absolute;
                lienzo.Add(c);
            }
            foreach (var c in new VisualElement[] { arboles, bajo, encima, iconosBajo, iconosEncima })
            {
                c.style.left = 0; c.style.top = 0; c.style.right = 0; c.style.bottom = 0;
            }

            // Las capas (abajo a la izquierda), el zoom (abajo a la derecha), cerrar (arriba a la derecha) y la franja de elegir sitio.
            var capas = Caja("mapa-capas");
            var botonCapas = new Button { name = "mapa-capas-boton", tooltip = "Capas del mapa" };
            botonCapas.AddToClassList("mapa-capas-boton");
            var dibujo = IconosDelMundo.Icono("simbolo", "capas", 20, IconosDelMundo.OroClaro);
            if (dibujo != null) botonCapas.Add(dibujo);
            listaCapas = Caja("mapa-capas-lista oculto");
            listaCapas.name = "mapa-capas-lista";
            botonCapas.clicked += () => { if (listaCapas.ClassListContains("oculto")) AbrirCapas(); else listaCapas.AddToClassList("oculto"); botonCapas.EnableInClassList("activo", !listaCapas.ClassListContains("oculto")); };
            capas.Add(listaCapas);
            capas.Add(botonCapas);
            Add(capas);
            var zoom = Caja("mapa-zoom");
            var mas = new Button(() => ZoomBoton(1)) { name = "zoom-mas", text = "+", tooltip = "Acercar" };
            var menos = new Button(() => ZoomBoton(-1)) { name = "zoom-menos", text = "−", tooltip = "Alejar" };
            zoom.Add(mas);
            zoom.Add(menos);
            Add(zoom);
            var cerrar = new Button(Cerrar) { name = "mapa-cerrar", text = "✕", tooltip = "Cerrar el mapa (Esc)" };
            cerrar.AddToClassList("mapa-interior-cerrar");
            Add(cerrar);
            franja = Caja("mapa-eleccion oculto");
            franja.name = "mapa-eleccion";
            franja.pickingMode = PickingMode.Ignore;
            franja.Add(new Label { name = "mapa-eleccion-texto" });
            franjaError = new Label { name = "mapa-eleccion-error" };
            franjaError.AddToClassList("mapa-eleccion-error");
            franja.Add(franjaError);
            Add(franja);

            lienzo.RegisterCallback<WheelEvent>(AlRodar);
            lienzo.RegisterCallback<PointerDownEvent>(AlPulsar);
            lienzo.RegisterCallback<PointerMoveEvent>(AlMover);
            lienzo.RegisterCallback<PointerUpEvent>(AlSoltar);
            RegisterCallback<GeometryChangedEvent>(_ => Aplicar());
            RegisterCallback<KeyDownEvent>(e => { if (e.keyCode == KeyCode.Escape) { Cerrar(); e.StopPropagation(); } });
            schedule.Execute(Repintar).Every(250);
        }

        static VisualElement Caja(string clases)
        {
            var v = new VisualElement();
            foreach (var c in clases.Split(' ')) v.AddToClassList(c);
            return v;
        }

        static VisualElement Capa(string nombre) => new VisualElement { name = nombre };

        // ─────────────────── abrir, cerrar, elegir ───────────────────

        /// <summary>Lo abre centrado en `centro` (o en el héroe); eligiendo sitio, a la escala de visión, si no a un cuarto de ella.</summary>
        public void Abrir((double x, double y)? centro, EleccionEnMapa eleccion = null)
        {
            Eleccion = eleccion;
            factorInicial = eleccion != null ? 1 : 0.25;
            franja.EnableInClassList("oculto", eleccion == null);
            lienzo.EnableInClassList("eligiendo", eleccion != null);
            if (eleccion != null) { franja.Q<Label>("mapa-eleccion-texto").text = eleccion.Texto; Error(null); }
            camara = null;
            foco = centro ?? PosicionDelHeroe();
            // La VISTA del mundo entero (la niebla, las zonas, lo que se ve): la pide el mapa 3D; desde dentro de una plaza, este.
            if (!sesion.Almacen.Ambitos.Keys.Any(k => k.StartsWith("VISTA:", StringComparison.Ordinal)))
                sesion.CambiarVista(new Rectangulo { Min = new Punto(), Max = new Punto { X = artefacto.Manifiesto.Tamano.X, Y = artefacto.Manifiesto.Tamano.Y } });
            radioVision = RadioDeVision();
            Aplicar();
            Repintar();
        }

        void Error(string texto)
        {
            franjaError.text = texto ?? "";
            franjaError.EnableInClassList("oculto", string.IsNullOrEmpty(texto));
        }

        public void Cerrar()
        {
            Eleccion = null;
            AlCerrar?.Invoke();
        }

        public void Destruir()
        {
            if (niebla != null) UnityEngine.Object.Destroy(niebla);
        }

        // ─────────────────── la cámara (`instalarZoomPan`) ───────────────────

        double AnchoPx => lienzo.resolvedStyle.width;
        double AltoPx => lienzo.resolvedStyle.height;

        (double min, double max) Topes() => (GeometriaVista.EscalaMinima(AnchoPx, AltoPx, anchoMundo, altoMundo), GeometriaVista.EscalaDeVision(AnchoPx, AltoPx, radioVision));

        void Aplicar()
        {
            if (!(AnchoPx > 0) || !(AltoPx > 0)) return;
            var (min, max) = Topes();
            var b = camara ?? new CamaraDeVista(max * factorInicial, anchoMundo / 2, altoMundo / 2);
            if (foco != null) b = new CamaraDeVista(b.Escala, foco.Value.x, foco.Value.y);
            camara = GeometriaVista.Acotar(b, AnchoPx, AltoPx, anchoMundo, altoMundo, min, max);
            foco = null;
            Colocar();
        }

        void ZoomA(double factor, double focoX, double focoY)
        {
            if (camara == null) return;
            camara = GeometriaVista.ZoomHacia(camara.Value, factor, focoX, focoY);
            Aplicar();
        }

        /// <summary>+1 acerca, −1 aleja (botones +/−, ×1,4 hacia el centro).</summary>
        public void ZoomBoton(int direccion) => ZoomA(direccion > 0 ? 1.4 : 1 / 1.4, 0, 0);

        void AlRodar(WheelEvent e)
        {
            var p = (Vector2)e.localMousePosition;
            ZoomA(e.delta.y < 0 ? 1.15 : 1 / 1.15, p.x - AnchoPx / 2, p.y - AltoPx / 2);
            e.StopPropagation();
        }

        void AlPulsar(PointerDownEvent e)
        {
            if (e.button != 0) return;
            pulsado = ultimo = e.localPosition;
            arrastrando = false;
            punteroId = e.pointerId;
            lienzo.CapturePointer(e.pointerId);
            Focus();
        }

        void AlMover(PointerMoveEvent e)
        {
            if (pulsado == null || camara == null) return;
            var p = (Vector2)e.localPosition;
            if (!arrastrando && Vector2.Distance(p, pulsado.Value) < ArrastreMinimoPx) return;
            arrastrando = true;
            lienzo.AddToClassList("arrastrando");
            camara = GeometriaVista.Arrastrar(camara.Value, p.x - ultimo.x, p.y - ultimo.y);
            ultimo = p;
            Aplicar();
        }

        void AlSoltar(PointerUpEvent e)
        {
            if (pulsado == null) return;
            var p = (Vector2)e.localPosition;
            var clic = !arrastrando && Vector2.Distance(p, pulsado.Value) < ArrastreMinimoPx;
            pulsado = null;
            arrastrando = false;
            lienzo.RemoveFromClassList("arrastrando");
            if (lienzo.HasPointerCapture(punteroId)) lienzo.ReleasePointer(punteroId);
            if (clic) _ = Clic(p);
        }

        /// <summary>El punto del mundo bajo un punto del lienzo (px locales).</summary>
        public (double x, double y) AMundo(Vector2 local) => GeometriaVista.AMundo(Camara, local.x - AnchoPx / 2, local.y - AltoPx / 2);

        /// <summary>El punto del lienzo (px locales) de un punto del mundo.</summary>
        public Vector2 ALienzo(double x, double y)
        {
            var c = Camara;
            var (ox, oy) = GeometriaVista.Origen(c, AnchoPx, AltoPx);
            return new Vector2((float)((x - ox) * c.Escala), (float)((y - oy) * c.Escala));
        }

        /// <summary>Un clic sobre el mapa en (px locales del lienzo): eligiendo sitio, la orden del sitio; si no, lo que diga quien lo abrió.</summary>
        public async Task Clic(Vector2 local)
        {
            var (x, y) = AMundo(local);
            var eleccion = Eleccion;
            if (eleccion == null)
            {
                if (AlClicar != null) await AlClicar(x, y, Camara.Escala);
                return;
            }
            Error("Enviando…");
            var mensaje = await eleccion.AlElegir(x, y);
            if (Eleccion != eleccion) return;
            if (mensaje != null) { Error(mensaje); return; }
            Cerrar();
        }

        // ─────────────────── el héroe ───────────────────

        Heroe Heroe() => heroeId() is string id ? sesion.Almacen.Buscar(TipoEntidad.Heroe, id)?.Heroe : null;

        /// <summary>Dónde está el héroe: su columna, o el sitio donde está dentro (plaza, pueblo, avanzada, campamento).</summary>
        public (double x, double y)? PosicionDelHeroe()
        {
            var u = Heroe()?.Ubicacion;
            if (u == null) return null;
            Punto p = null;
            switch (u.DondeCase)
            {
                case UbicacionHeroe.DondeOneofCase.EjercitoId:
                    var e = sesion.Almacen.Buscar(TipoEntidad.Ejercito, u.EjercitoId)?.Ejercito;
                    if (e == null) return null;
                    var q = Instante.Posicion(e.Plan, e.Posicion, sesion.AhoraMundo);
                    return (q.x / 1000, q.y / 1000);
                case UbicacionHeroe.DondeOneofCase.AsentamientoId: p = sesion.Almacen.Buscar(TipoEntidad.Asentamiento, u.AsentamientoId)?.Asentamiento?.Posicion; break;
                case UbicacionHeroe.DondeOneofCase.PuebloId: p = sesion.Almacen.Buscar(TipoEntidad.Pueblo, u.PuebloId)?.Pueblo?.Posicion; break;
                case UbicacionHeroe.DondeOneofCase.AvanzadaId: p = sesion.Almacen.Buscar(TipoEntidad.Avanzada, u.AvanzadaId)?.Avanzada?.Posicion; break;
                case UbicacionHeroe.DondeOneofCase.CampamentoId: p = sesion.Almacen.Buscar(TipoEntidad.CampamentoMercenarios, u.CampamentoId)?.CampamentoMercenarios?.Posicion; break;
            }
            return p == null ? ((double, double)?)null : (p.X / 1000.0, p.Y / 1000.0);
        }

        /// <summary>La visión del héroe (`Movimiento_Ejercitos.VISION`): la de ejército con soldados en su columna; si no, la del héroe solo.</summary>
        double RadioDeVision()
        {
            var solo = datos.Numero("documentos.Movimiento_Ejercitos.VISION.jugadorSolo");
            var r = solo > 0 ? solo : RadioVisionPorDefecto;
            var col = Heroe()?.Ubicacion?.EjercitoId;
            if (string.IsNullOrEmpty(col)) return r;
            foreach (var e in sesion.Almacen.Todas(Entidad.CuerpoOneofCase.Escuadron))
                if (e.Escuadron.Cantidad > 0 && e.Escuadron.Contenedor?.EjercitoId == col)
                {
                    var ej = datos.Numero("documentos.Movimiento_Ejercitos.VISION.ejercito");
                    return ej > 0 ? ej : r;
                }
            return r;
        }

        // ─────────────────── pintar ───────────────────

        double K => Math.Max(0.5, Math.Min(1.4, Camara.Escala / EscalaGlifo));

        /// <summary>Recoge lo que hay (4 veces por segundo, como el prototipo con algo moviéndose) y lo coloca.</summary>
        public void Repintar()
        {
            if (panel == null || camara == null) return;
            var ahora = sesion.AhoraMundo;
            var id = heroeId();
            var radioInicial = datos.Numero("reglas.territorio.radio_inicial") / 1000;
            Cosas = CosasDelMapa.Reunir(sesion.Almacen, id, ahora, artefacto.Nodos, radioInicial);
            rayas = CosasDelMapa.Rayas(sesion.Almacen, id, datos.Numero("documentos.Campamentos_Mercenarios.MERCENARIOS.radioProteccion"), ahora);
            var u = Heroe()?.Ubicacion;
            columna = u?.DondeCase == UbicacionHeroe.DondeOneofCase.EjercitoId ? sesion.Almacen.Buscar(TipoEntidad.Ejercito, u.EjercitoId)?.Ejercito : null;
            posColumna = null;
            rutaColumna.Clear();
            if (columna != null)
            {
                var p = Instante.Posicion(columna.Plan, columna.Posicion, ahora);
                posColumna = (p.x / 1000, p.y / 1000);
                if (p.enMarcha || columna.Estado != EstadoColumna.ColumnaEstacionada)
                    foreach (var q in Instante.RutaRestante(columna.Plan, columna.Posicion, ahora)) rutaColumna.Add((q.x / 1000, q.y / 1000));
            }
            PonerNiebla();
            Colocar();
        }

        /// <summary>La niebla de la VISTA (`pintarNiebla`): la máscara a la resolución de su rejilla, estirada con interpolación (bordes suaves).</summary>
        void PonerNiebla()
        {
            NieblaProyectada n = null;
            foreach (var kv in sesion.Almacen.Ambitos)
                if (kv.Key.StartsWith("VISTA:", StringComparison.Ordinal) && kv.Value.Niebla != null) { n = kv.Value.Niebla; break; }
            if (n == nieblaPuesta) return;
            nieblaPuesta = n;
            if (n == null || n.Columnas == 0) { explorado = (x, y) => true; imagenNiebla.style.backgroundImage = StyleKeyword.None; return; }
            int cols = (int)n.Columnas, filas = (int)n.Filas;
            var celda = n.TamanoCelda / 1000.0;
            var celdas = n.Celdas.ToByteArray();
            var visibles = n.Visibles.ToByteArray();
            bool Bit(byte[] m, int i) => (i >> 3) < m.Length && ((m[i >> 3] >> (i & 7)) & 1) == 1;
            if (niebla == null || niebla.width != cols || niebla.height != filas)
            {
                if (niebla != null) UnityEngine.Object.Destroy(niebla);
                niebla = new Texture2D(cols, filas, TextureFormat.RGBA32, false) { name = "niebla 2D", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            }
            var px = new Color32[cols * filas];
            for (int i = 0; i < cols * filas; i++)
            {
                var a = Bit(visibles, i) ? AlfaVisible : Bit(celdas, i) ? AlfaRecordado : AlfaSinVer;
                int f = i / cols, c = i % cols;
                px[(filas - 1 - f) * cols + c] = new Color32(10, 14, 23, a);
            }
            niebla.SetPixels32(px);
            niebla.Apply(false);
            imagenNiebla.style.backgroundImage = new StyleBackground(niebla);
            imagenNiebla.userData = (cols * celda, filas * celda);
            explorado = (x, y) =>
            {
                int c = (int)Math.Floor(x / celda), f = (int)Math.Floor(y / celda);
                return c >= 0 && f >= 0 && c < cols && f < filas && Bit(celdas, f * cols + c);
            };
            imagenFertilidad.userData = null; // la capa de fertilidad solo va sobre lo explorado: se rehace
        }

        /// <summary>Pone cada capa donde cae con la cámara de ahora.</summary>
        void Colocar()
        {
            if (camara == null || !(AnchoPx > 0)) return;
            var c = camara.Value;
            var (ox, oy) = GeometriaVista.Origen(c, AnchoPx, AltoPx);
            double x1 = ox + AnchoPx / c.Escala, y1 = oy + AltoPx / c.Escala;
            // El suelo: de lejos, la imagen del mundo; de cerca, la vista a media resolución (como mucho cada 0,12 s al arrastrar).
            if (!suelo.DeCerca(c.Escala))
            {
                suelo.FiltroDeLejos(c.Escala);
                Rect(imagenSuelo, 0, 0, anchoMundo, altoMundo, ox, oy, c.Escala);
                imagenSuelo.style.backgroundImage = new StyleBackground(suelo.Lejos());
                listaArboles.Clear();
            }
            else
            {
                if (Time.unscaledTime - ultimoCerca > 0.12f || rectCerca.x1 <= rectCerca.x0 || !arrastrando)
                {
                    ultimoCerca = Time.unscaledTime;
                    rectCerca = (ox, oy, x1, y1);
                    imagenSuelo.style.backgroundImage = new StyleBackground(suelo.Cerca(ox, oy, x1, y1, (int)Math.Round(AnchoPx / 2), (int)Math.Round(AltoPx / 2)));
                    suelo.Arboles(ox, oy, x1, y1, c.Escala, listaArboles);
                }
                Rect(imagenSuelo, rectCerca.x0, rectCerca.y0, rectCerca.x1, rectCerca.y1, ox, oy, c.Escala);
            }
            if (imagenNiebla.userData is ValueTuple<double, double> t) Rect(imagenNiebla, 0, 0, t.Item1, t.Item2, ox, oy, c.Escala);
            PonerFertilidad(ox, oy, c.Escala);
            arboles.MarkDirtyRepaint();
            bajo.MarkDirtyRepaint();
            encima.MarkDirtyRepaint();
            ColocarIconos();
        }

        static void Rect(VisualElement v, double x0, double y0, double x1, double y1, double ox, double oy, double escala)
        {
            v.style.left = (float)((x0 - ox) * escala);
            v.style.top = (float)((y0 - oy) * escala);
            v.style.width = (float)((x1 - x0) * escala);
            v.style.height = (float)((y1 - y0) * escala);
        }

        RasterDeMundo RasterDeCultivo(string cultivo)
        {
            Raster r = cultivo switch
            {
                "vid" => artefacto.FertilidadVid, "oliva" => artefacto.FertilidadOliva, "cebada" => artefacto.FertilidadCebada,
                "sesamo" => artefacto.FertilidadSesamo, _ => artefacto.Fertilidad,
            };
            r ??= artefacto.Fertilidad;
            return r != null && r.Encoding == EncodingRaster.EncodingU8 ? new RasterDeMundo(r) : null;
        }

        readonly Dictionary<string, RasterDeMundo> rastersDeCultivo = new Dictionary<string, RasterDeMundo>();

        /// <summary>La capa de fertilidad del cultivo puesto, sobre lo explorado (`pintarFertilidad`).</summary>
        void PonerFertilidad(double ox, double oy, double escala)
        {
            var cultivo = CultivoDeFertilidad;
            imagenFertilidad.style.display = cultivo == null ? DisplayStyle.None : DisplayStyle.Flex;
            if (cultivo == null) return;
            if (!rastersDeCultivo.TryGetValue(cultivo, out var f)) rastersDeCultivo[cultivo] = f = RasterDeCultivo(cultivo);
            if (f == null) return;
            var clave = $"{cultivo}|{FertilidadMinima}|{nieblaPuesta?.GetHashCode()}";
            if (!Equals(imagenFertilidad.userData, clave))
            {
                imagenFertilidad.userData = clave;
                imagenFertilidad.style.backgroundImage = new StyleBackground(suelo.Fertilidad(f, clave, explorado, FertilidadMinima));
            }
            Rect(imagenFertilidad, 0, 0, f.Columnas * f.Celda, f.Filas * f.Celda, ox, oy, escala);
        }

        // ── los iconos (encima y bajo la niebla) ──

        void ColocarIconos()
        {
            vivos.Clear();
            var k = K;
            var c = Camara;
            var (ox, oy) = GeometriaVista.Origen(c, AnchoPx, AltoPx);
            double x1 = ox + AnchoPx / c.Escala, y1 = oy + AltoPx / c.Escala;
            bool cerca = suelo.DeCerca(c.Escala), general = x1 - ox > AnchoVistaGeneral;
            bool ALaVista(double x, double y, double m) => x >= ox - m && x <= x1 + m && y >= oy - m && y <= y1 + m;
            var almacen = sesion.Almacen;
            foreach (var cosa in Cosas)
            {
                if (!ALaVista(cosa.X, cosa.Y, 30 / c.Escala)) continue;
                if (cosa.Clase == ClaseDeCosa.Nodo && general) continue;
                var p = ALienzo(cosa.X, cosa.Y);
                switch (cosa.Clase)
                {
                    case ClaseDeCosa.Plaza: Sigilo(cosa, p); break;
                    case ClaseDeCosa.Batalla: break; // va pintada en `encima`
                    case ClaseDeCosa.Nodo:
                        var lado = cerca ? Math.Max(14, 16 * k) : 12;
                        var nodo = Icono(cosa, p, lado, true);
                        if (nodo != null) Nombre(nodo, cerca && NombresDeNodos ? cosa.Nombre : null, lado);
                        break;
                    default:
                        var t = Lado(cosa) * k;
                        var ic = Icono(cosa, p, t, cosa.BajoNiebla);
                        if (ic == null) break;
                        ic.style.opacity = cosa.Clase == ClaseDeCosa.Avanzada && cosa.Detalle?.EndsWith("en ruinas") == true ? 0.5f
                            : cosa.Clase == ClaseDeCosa.Obra ? (cosa.Entidad?.Obra?.HasTerminaEnMs == true ? 0.9f : 0.6f) : 1f;
                        var n = (cosa.Clase == ClaseDeCosa.Ejercito || cosa.Clase == ClaseDeCosa.EjercitoAjeno || cosa.Clase == ClaseDeCosa.Formacion)
                            ? cosa.Entidad?.Ejercito?.Participantes.Count ?? 1 : 1;
                        Cuenta(ic, n > 1 ? n.ToString() : null, t);
                        break;
                }
            }
            // Tu columna: el casco en el color de tu Facción.
            if (posColumna != null)
            {
                var mia = new CosaDelMapa { Clase = ClaseDeCosa.Ejercito, Id = "mi-columna", X = posColumna.Value.x, Y = posColumna.Value.y, Color = CosasDelMapa.ColorDeFaccion(almacen, Heroe()?.FaccionId) };
                var ic = Icono(mia, ALienzo(mia.X, mia.Y), 20 * k, false);
                if (ic != null) { ic.name = "mapa-mi-columna"; Cuenta(ic, columna.Participantes.Count > 1 ? columna.Participantes.Count.ToString() : null, 20 * k); }
            }
            foreach (var kv in iconos.Where(kv => !vivos.Contains(kv.Key)).ToList())
            {
                kv.Value.RemoveFromHierarchy();
                iconos.Remove(kv.Key);
            }
        }

        /// <summary>El lado (px, con k = 1) del icono de cada clase, como el prototipo.</summary>
        static double Lado(CosaDelMapa c) => c.Clase switch
        {
            ClaseDeCosa.Ejercito or ClaseDeCosa.EjercitoAjeno or ClaseDeCosa.Formacion => 20,
            ClaseDeCosa.Pueblo or ClaseDeCosa.Aldea => 14,
            ClaseDeCosa.Avanzada => 11 + 2 * (int)(c.Entidad?.Avanzada?.Etapa ?? 1),
            ClaseDeCosa.Obra => 13,
            ClaseDeCosa.Alijo => 16,
            ClaseDeCosa.Mercenarios => 24,
            ClaseDeCosa.Bandidos => 22,
            ClaseDeCosa.Caravana => 16,
            _ => 14,
        };

        VisualElement Icono(CosaDelMapa cosa, Vector2 p, double lado, bool bajoNiebla)
        {
            var de = IconosDelMundo.DeCosa(cosa);
            if (de == null) return null;
            var clave = cosa.Clase + ":" + cosa.Id;
            vivos.Add(clave);
            if (!iconos.TryGetValue(clave, out var v))
            {
                v = new VisualElement { name = "mapa-cosa-" + cosa.Clase.ToString().ToLowerInvariant() + "-" + cosa.Id, pickingMode = PickingMode.Ignore };
                v.AddToClassList("mapa-cosa");
                var img = IconosDelMundo.Imagen("mapa", de.Value.id);
                if (img != null) v.style.backgroundImage = new StyleBackground(img);
                iconos[clave] = v;
            }
            v.style.unityBackgroundImageTintColor = de.Value.color;
            var padre = bajoNiebla ? iconosBajo : iconosEncima;
            if (v.parent != padre) padre.Add(v);
            v.style.width = v.style.height = (float)lado;
            v.style.left = p.x - (float)lado / 2;
            v.style.top = p.y - (float)lado / 2;
            return v;
        }

        static void Nombre(VisualElement icono, string texto, double lado)
        {
            var l = icono.Q<Label>(className: "mapa-cosa-nombre");
            if (texto == null) { l?.RemoveFromHierarchy(); return; }
            if (l == null) { l = new Label(); l.AddToClassList("mapa-cosa-nombre"); l.pickingMode = PickingMode.Ignore; icono.Add(l); }
            l.text = texto;
            l.style.top = (float)lado + 2;
        }

        static void Cuenta(VisualElement icono, string texto, double lado)
        {
            var l = icono.Q<Label>(className: "mapa-cosa-cuenta");
            if (texto == null) { l?.RemoveFromHierarchy(); return; }
            if (l == null) { l = new Label(); l.AddToClassList("mapa-cosa-cuenta"); l.pickingMode = PickingMode.Ignore; icono.Add(l); }
            l.text = texto;
            l.style.left = (float)(lado * 1.0) - 4;
            l.style.top = (float)(lado * 0.05) - 8;
        }

        /// <summary>Una plaza: el sigilo completo de su Facción (`dibujarAsentamiento`); sin sigilo, el punto de su color. La recordada, tenue y bajo la niebla.</summary>
        void Sigilo(CosaDelMapa cosa, Vector2 p)
        {
            var clave = "Plaza:" + cosa.Id;
            vivos.Add(clave);
            if (!iconos.TryGetValue(clave, out var v))
            {
                v = new VisualElement { name = "mapa-plaza-" + cosa.Id, pickingMode = PickingMode.Ignore };
                v.AddToClassList("mapa-plaza");
                iconos[clave] = v;
            }
            var tex = CosasEnElMapa.SigiloDe?.Invoke(cosa.FaccionId, (int)(AnchoSigilo * 3));
            var color = new Color32((byte)(cosa.Color >> 16), (byte)(cosa.Color >> 8), (byte)cosa.Color, 255);
            float ancho, alto;
            if (tex != null)
            {
                ancho = (float)AnchoSigilo; alto = (float)(AnchoSigilo * 1.2);
                v.style.backgroundImage = new StyleBackground(tex);
                v.style.backgroundColor = StyleKeyword.Null;
                v.RemoveFromClassList("mapa-plaza-punto");
            }
            else
            {
                ancho = alto = 12;
                v.style.backgroundImage = StyleKeyword.None;
                v.AddToClassList("mapa-plaza-punto");
                v.style.backgroundColor = cosa.BajoNiebla ? Color.clear : (Color)color;
                v.style.borderTopColor = v.style.borderBottomColor = v.style.borderLeftColor = v.style.borderRightColor = cosa.BajoNiebla ? (Color)color : (Color)new Color32(0x1b, 0x1a, 0x17, 255);
            }
            v.style.opacity = cosa.BajoNiebla ? 0.55f : 1;
            var padre = cosa.BajoNiebla ? iconosBajo : iconosEncima;
            if (v.parent != padre) padre.Add(v);
            v.style.width = ancho;
            v.style.height = alto;
            v.style.left = p.x - ancho / 2;
            v.style.top = p.y - alto / 2;
        }

        // ── lo pintado con Painter2D ──

        void PintarArboles(MeshGenerationContext mgc)
        {
            if (listaArboles.Count == 0 || camara == null) return;
            var p2 = mgc.painter2D;
            var e = (float)Camara.Escala;
            var sombra = new Color(10 / 255f, 30 / 255f, 12 / 255f, 0.35f);
            var brillo = new Color(160 / 255f, 210 / 255f, 120 / 255f, 0.25f);
            foreach (var a in listaArboles)
            {
                var c = ALienzo(a.X, a.Y);
                var r = a.R * e;
                Circulo(p2, c + new Vector2(r * 0.35f, r * 0.35f), r, sombra);
                Circulo(p2, c, r, a.Color);
                Circulo(p2, c - new Vector2(r * 0.3f, r * 0.3f), r * 0.45f, brillo);
            }
        }

        static void Circulo(Painter2D p2, Vector2 c, float r, Color color)
        {
            p2.fillColor = color;
            p2.BeginPath();
            p2.Arc(c, r, 0, 360);
            p2.Fill();
        }

        /// <summary>Bajo la niebla (geografía y lo que solo se recuerda): los ríos y vados de cerca, los caminos, las zonas y fronteras ajenas.</summary>
        void PintarBajo(MeshGenerationContext mgc)
        {
            if (camara == null) return;
            var p2 = mgc.painter2D;
            var c = Camara;
            var (ox, oy) = GeometriaVista.Origen(c, AnchoPx, AltoPx);
            double x1 = ox + AnchoPx / c.Escala, y1 = oy + AltoPx / c.Escala;
            bool cerca = suelo.DeCerca(c.Escala), general = x1 - ox > AnchoVistaGeneral;
            var e = (float)c.Escala;
            p2.lineCap = LineCap.Round;
            p2.lineJoin = LineJoin.Round;
            // 3. Los ríos: de cerca a su anchura real y con orilla; en la vista general ya van en la imagen del suelo.
            if (!general)
                foreach (var rio in artefacto.Rios)
                {
                    if (rio.Puntos.Count < 2 || !rio.Puntos.Any(q => q.X / 1000.0 >= ox - 10 && q.X / 1000.0 <= x1 + 10 && q.Y / 1000.0 >= oy - 10 && q.Y / 1000.0 <= y1 + 10)) continue;
                    var ancho = Math.Max(rio.Navegable ? 5 : 2.5f, (rio.Navegable ? 3 : 1.5f) * e);
                    var puntos = rio.Puntos.Select(q => ALienzo(q.X / 1000.0, q.Y / 1000.0)).ToList();
                    if (cerca) Linea(p2, puntos, new Color(92 / 255f, 84 / 255f, 52 / 255f, 0.55f), ancho + Math.Max(2, 0.6f * e));
                    Linea(p2, puntos, new Color(38 / 255f, 90 / 255f, 145 / 255f, 0.9f), ancho);
                    for (int i = 0; i < rio.Vados.Count; i++)
                    {
                        var v = rio.Vados[i];
                        double vx = v.X / 1000.0, vy = v.Y / 1000.0;
                        if (vx < ox - 4 || vx > x1 + 4 || vy < oy - 4 || vy > y1 + 4) continue;
                        var d = Rumbo(rio, vx, vy);
                        var largo = ancho * 1.4f + 4;
                        var centro = ALienzo(vx, vy);
                        var a = centro + new Vector2(-d.y * largo, d.x * largo);
                        var b = centro + new Vector2(d.y * largo, -d.x * largo);
                        p2.lineCap = LineCap.Butt;
                        Linea(p2, new List<Vector2> { a, b }, new Color(30 / 255f, 24 / 255f, 14 / 255f, 0.85f), Math.Max(4, 0.9f * e) + 2);
                        Linea(p2, new List<Vector2> { a, b }, new Color32(0xd9, 0xc7, 0x9a, 255), Math.Max(4, 0.9f * e));
                        p2.lineCap = LineCap.Round;
                    }
                }
            // 5. Los caminos (bajo la niebla): rayados, con el grosor de su escalón.
            var camino = new Color(139 / 255f, 90 / 255f, 43 / 255f, 0.9f);
            foreach (var r in rayas)
                if (r.Clase == ClaseDeRaya.Camino) Rayado(p2, r.Puntos.Select(q => ALienzo(q.x, q.y)).ToList(), camino, r.Escalon == 0 ? 1.2f : r.Escalon == 2 ? 4 : 2.5f, 6, 4, false);
            // 6. Las zonas ajenas: relleno tenue de su color y su frontera rayada (las recordadas, más corta).
            var miFaccion = Heroe()?.FaccionId;
            foreach (var r in rayas)
            {
                if (r.Clase != ClaseDeRaya.ZonaDeFaccion && r.Clase != ClaseDeRaya.ZonaDePlaza) continue;
                if (!string.IsNullOrEmpty(miFaccion) && r.FaccionId == miFaccion) continue;
                var col = ColorDe(r.Color);
                var puntos = r.Puntos.Select(q => ALienzo(q.x, q.y)).ToList();
                Relleno(p2, puntos, new Color(col.r, col.g, col.b, 0x22 / 255f));
                Rayado(p2, puntos, col, r.Clase == ClaseDeRaya.ZonaDeFaccion ? 4 : 2, 10, 6, true);
            }
        }

        /// <summary>Encima de la niebla: tu zona (relleno y raya por dentro), los anillos, las rutas de caravanas y de tu columna, las batallas, el
        /// sitio que se elige, tu marca y tu destino.</summary>
        void PintarEncima(MeshGenerationContext mgc)
        {
            if (camara == null) return;
            var p2 = mgc.painter2D;
            var k = (float)K;
            var miFaccion = Heroe()?.FaccionId;
            // 8b. Las parcelas de bosque exploradas a la vista (`pintarParcelas`): su círculo de 4 u, más lleno cuanto más denso; verdes las libres,
            // ámbar las que ya tienen Leñera, rojas las de la zona de otra plaza.
            if (Parcelas) PintarParcelas(p2);
            // 9. Tu zona de influencia fusionada.
            foreach (var r in rayas)
            {
                if (r.Clase != ClaseDeRaya.ZonaDeFaccion || string.IsNullOrEmpty(miFaccion) || r.FaccionId != miFaccion) continue;
                var col = ColorDe(r.Color);
                var puntos = r.Puntos.Select(q => ALienzo(q.x, q.y)).ToList();
                Relleno(p2, puntos, new Color(col.r, col.g, col.b, 0x33 / 255f));
                Rayado(p2, puntos, col, 4, 10, 6, true);
            }
            // Los anillos de los mercenarios y las Miradas.
            foreach (var r in rayas)
                if (r.Clase == ClaseDeRaya.Anillo) Linea(p2, r.Puntos.Select(q => ALienzo(q.x, q.y)).ToList(), new Color(ColorDe(r.Color).r, ColorDe(r.Color).g, ColorDe(r.Color).b, 0.7f), 1, true);
            // 13. Las rutas de las caravanas, tenues.
            var anchoRuta = Math.Max(1.2f, 2.2f * k);
            foreach (var cosa in Cosas)
            {
                var cv = cosa.Entidad?.Caravana;
                if (cv?.Plan == null || (cosa.Clase != ClaseDeCosa.Caravana && cosa.Clase != ClaseDeCosa.CaravanaAjena)) continue;
                var ruta = Instante.RutaRestante(cv.Plan, cv.Posicion, sesion.AhoraMundo);
                if (ruta.Count > 1) Rayado(p2, ruta.Select(q => ALienzo(q.x / 1000, q.y / 1000)).ToList(), new Color(241 / 255f, 230 / 255f, 200 / 255f, 0.35f), anchoRuta, 7 * k, 5 * k, false);
            }
            // 15. La ruta de tu columna: por donde vas a ir.
            if (rutaColumna.Count > 1) Rayado(p2, rutaColumna.Select(q => ALienzo(q.x, q.y)).ToList(), new Color(241 / 255f, 230 / 255f, 200 / 255f, 0.6f), anchoRuta, 7 * k, 5 * k, false);
            // 17c. Las batallas: espadas cruzadas en un círculo.
            foreach (var cosa in Cosas)
            {
                if (cosa.Clase != ClaseDeCosa.Batalla) continue;
                var c = ALienzo(cosa.X, cosa.Y);
                Circulo(p2, c, 9, new Color32(0x8a, 0x3b, 0x12, 255));
                var crema = new Color32(0xf4, 0xea, 0xd2, 255);
                Linea(p2, new List<Vector2> { c + new Vector2(-5, -5), c + new Vector2(5, 5) }, crema, 1.5f);
                Linea(p2, new List<Vector2> { c + new Vector2(5, -5), c + new Vector2(-5, 5) }, crema, 1.5f);
            }
            // Eligiendo sitio: la zona de la plaza, rayada, y los sitios de la aldea (verdes los válidos, grises los pegados a algo).
            var el = Eleccion;
            if (el?.Zona != null && el.Zona.Count > 2)
            {
                var z = el.Zona.Select(q => ALienzo(q.x, q.y)).ToList();
                Relleno(p2, z, new Color(216 / 255f, 184 / 255f, 120 / 255f, 0.12f));
                Rayado(p2, z, new Color32(0xd8, 0xb8, 0x78, 255), 2, 6, 4, true);
            }
            if (el != null)
            {
                var lado = (float)Math.Max(1.5, el.PasoSitios * Camara.Escala);
                var borde = Math.Max(1, Math.Min(2.5f, lado * 0.25f));
                Sitios(p2, el.SitiosCerca, lado, borde, new Color(176 / 255f, 172 / 255f, 160 / 255f, 0.7f), new Color(40 / 255f, 36 / 255f, 30 / 255f, 0.75f));
                Sitios(p2, el.SitiosValidos, lado, borde, new Color(96 / 255f, 226 / 255f, 92 / 255f, 0.78f), new Color(10 / 255f, 46 / 255f, 16 / 255f, 0.9f));
            }
            // Fundando (duda 38, tanda 3; `pintarPrevisualizacionFundacion` de `render.ts`): la zona con que nacería la plaza, rayada como la de elegir
            // sitio, los sitios de aldea y las parcelas de bosque de la zona (verdes las libres, rojas las ocupadas o de otra plaza).
            var fu = Fundacion;
            if (fu != null)
            {
                var z = Enumerable.Range(0, 64).Select(i => ALienzo(fu.X + fu.Radio * Math.Cos(i * Math.PI / 32), fu.Y + fu.Radio * Math.Sin(i * Math.PI / 32))).ToList();
                Relleno(p2, z, new Color(241 / 255f, 211 / 255f, 139 / 255f, 0.10f));
                Rayado(p2, z, new Color32(0xf1, 0xd3, 0x8b, 255), 2, 6, 4, true);
                var lado = (float)Math.Max(1.5, fu.Paso * Camara.Escala);
                var borde = Math.Max(1, Math.Min(2.5f, lado * 0.25f));
                Sitios(p2, fu.Cerca, lado, borde, new Color(176 / 255f, 172 / 255f, 160 / 255f, 0.7f), new Color(40 / 255f, 36 / 255f, 30 / 255f, 0.75f));
                Sitios(p2, fu.Validos, lado, borde, new Color(96 / 255f, 226 / 255f, 92 / 255f, 0.78f), new Color(10 / 255f, 46 / 255f, 16 / 255f, 0.9f));
                var rp = (float)Math.Max(2, 2 * Camara.Escala);
                foreach (var (x, y, libre) in fu.Parcelas)
                {
                    p2.strokeColor = libre ? new Color32(0x78, 0xdc, 0x6e, 0xff) : new Color32(0xc8, 0x5a, 0x46, 0xff);
                    p2.lineWidth = 1.5f;
                    p2.BeginPath();
                    p2.Arc(ALienzo(x, y), rp, 0, 360);
                    p2.Stroke();
                }
            }
            // 19. Tú y tu destino.
            if (posColumna != null)
            {
                var c = ALienzo(posColumna.Value.x, posColumna.Value.y);
                p2.strokeColor = Marcador;
                p2.lineWidth = Math.Max(1.2f, 2 * k);
                p2.BeginPath();
                p2.Arc(c, 15 * k, 0, 360);
                p2.Stroke();
                foreach (var d in new[] { new Vector2(0, -1), new Vector2(0, 1), new Vector2(-1, 0), new Vector2(1, 0) })
                    Linea(p2, new List<Vector2> { c + d * 13 * k, c + d * 19 * k }, Marcador, Math.Max(1, 1.6f * k));
                if (rutaColumna.Count > 1)
                {
                    var (dx, dy) = rutaColumna[rutaColumna.Count - 1];
                    var q = ALienzo(dx, dy);
                    var circulo = new List<Vector2>();
                    for (int i = 0; i <= 32; i++) circulo.Add(q + new Vector2(Mathf.Cos(i * Mathf.PI / 16), Mathf.Sin(i * Mathf.PI / 16)) * 8 * k);
                    Rayado(p2, circulo, Marcador, Math.Max(1.2f, 1.8f * k), 4 * k, 3 * k, false);
                    var a = 3.5f * k;
                    Linea(p2, new List<Vector2> { q + new Vector2(-a, -a), q + new Vector2(a, a) }, Marcador, Math.Max(1.2f, 1.8f * k));
                    Linea(p2, new List<Vector2> { q + new Vector2(a, -a), q + new Vector2(-a, a) }, Marcador, Math.Max(1.2f, 1.8f * k));
                }
            }
        }

        const double RadioParcela = 4;
        static readonly Dictionary<string, Color> ColorDeParcela = new Dictionary<string, Color>
        {
            ["libre"] = new Color32(96, 200, 84, 255), ["ocupada"] = new Color32(222, 170, 82, 255), ["ajena"] = new Color32(196, 92, 74, 255),
        };

        void PintarParcelas(Painter2D p2)
        {
            var c = Camara;
            var (ox, oy) = GeometriaVista.Origen(c, AnchoPx, AltoPx);
            double x1 = ox + AnchoPx / c.Escala, y1 = oy + AltoPx / c.Escala;
            var r = (float)Math.Max(1.6, RadioParcela * c.Escala);
            foreach (var q in artefacto.Parcelas)
            {
                double x = q.Centro.X / 1000.0, y = q.Centro.Y / 1000.0;
                if (x < ox - RadioParcela || x > x1 + RadioParcela || y < oy - RadioParcela || y > y1 + RadioParcela || !explorado(x, y)) continue;
                if (!ColorDeParcela.TryGetValue(EstadoDeParcela(q), out var color)) continue;
                var grado = Math.Min(3, (int)Math.Floor(q.DensidadMilesimas / 1000.0 * 4));
                var centro = ALienzo(x, y);
                Circulo(p2, centro, r, new Color(color.r, color.g, color.b, 0.1f + 0.1f * grado));
                if (r >= 3)
                {
                    p2.strokeColor = new Color(color.r, color.g, color.b, 0.8f);
                    p2.lineWidth = r >= 10 ? 1.5f : 1;
                    p2.BeginPath();
                    p2.Arc(centro, r, 0, 360);
                    p2.Stroke();
                }
            }
        }

        void Sitios(Painter2D p2, List<(double x, double y)> puntos, float lado, float borde, Color color, Color oscuro)
        {
            if (puntos == null) return;
            var (ox, oy) = GeometriaVista.Origen(Camara, AnchoPx, AltoPx);
            double x1 = ox + AnchoPx / Camara.Escala, y1 = oy + AltoPx / Camara.Escala;
            // El verde un pelo más grande que su celda: sin rayas entre celdas vecinas.
            foreach (var (relleno, extra) in new[] { (oscuro, borde), (color, 0.5f) })
            {
                p2.fillColor = relleno;
                p2.BeginPath();
                foreach (var (x, y) in puntos)
                {
                    if (x < ox || x > x1 || y < oy || y > y1) continue;
                    var c = ALienzo(x, y);
                    var m = lado / 2 + extra;
                    p2.MoveTo(c + new Vector2(-m, -m)); p2.LineTo(c + new Vector2(m, -m)); p2.LineTo(c + new Vector2(m, m)); p2.LineTo(c + new Vector2(-m, m)); p2.ClosePath();
                }
                p2.Fill();
            }
        }

        static Color ColorDe(uint rgb) => new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);

        static Vector2 Rumbo(Rio rio, double vx, double vy)
        {
            int mejor = 0;
            double d = double.MaxValue;
            for (int i = 0; i < rio.Puntos.Count; i++)
            {
                var dd = Math.Pow(rio.Puntos[i].X / 1000.0 - vx, 2) + Math.Pow(rio.Puntos[i].Y / 1000.0 - vy, 2);
                if (dd < d) { d = dd; mejor = i; }
            }
            int a = Math.Max(0, mejor - 1), b = Math.Min(rio.Puntos.Count - 1, mejor + 1);
            var v = new Vector2((rio.Puntos[b].X - rio.Puntos[a].X) / 1000f, (rio.Puntos[b].Y - rio.Puntos[a].Y) / 1000f);
            return v.sqrMagnitude > 0 ? v.normalized : Vector2.right;
        }

        static void Linea(Painter2D p2, List<Vector2> puntos, Color color, float ancho, bool cerrada = false)
        {
            if (puntos.Count < 2) return;
            p2.strokeColor = color;
            p2.lineWidth = ancho;
            p2.BeginPath();
            p2.MoveTo(puntos[0]);
            for (int i = 1; i < puntos.Count; i++) p2.LineTo(puntos[i]);
            if (cerrada) p2.ClosePath();
            p2.Stroke();
        }

        static void Relleno(Painter2D p2, List<Vector2> puntos, Color color)
        {
            if (puntos.Count < 3) return;
            p2.fillColor = color;
            p2.BeginPath();
            p2.MoveTo(puntos[0]);
            for (int i = 1; i < puntos.Count; i++) p2.LineTo(puntos[i]);
            p2.ClosePath();
            p2.Fill(FillRule.NonZero);
        }

        /// <summary>Una línea a trazos (`setLineDash([lleno, hueco])`), en px.</summary>
        static void Rayado(Painter2D p2, List<Vector2> puntos, Color color, float ancho, float lleno, float hueco, bool cerrada)
        {
            if (puntos.Count < 2) return;
            if (cerrada) puntos = new List<Vector2>(puntos) { puntos[0] };
            p2.strokeColor = color;
            p2.lineWidth = ancho;
            p2.BeginPath();
            float resto = lleno;
            bool pintando = true;
            for (int i = 1; i < puntos.Count; i++)
            {
                Vector2 a = puntos[i - 1], b = puntos[i];
                var largo = Vector2.Distance(a, b);
                float hecho = 0;
                while (largo - hecho > 0.0001f)
                {
                    var paso = Math.Min(resto, largo - hecho);
                    var desde = Vector2.Lerp(a, b, hecho / largo);
                    var hasta = Vector2.Lerp(a, b, (hecho + paso) / largo);
                    if (pintando) { p2.MoveTo(desde); p2.LineTo(hasta); }
                    hecho += paso;
                    resto -= paso;
                    if (resto <= 0.0001f) { pintando = !pintando; resto = pintando ? lleno : hueco; }
                }
            }
            p2.Stroke();
        }

        // ── las capas (`ui/capasMapa.ts`) ──

        static readonly Dictionary<string, (string nombre, string de, string da)> NombreDeCultivo = new Dictionary<string, (string, string, string)>
        {
            ["trigo"] = ("Trigo", "del trigo", "trigo"), ["vid"] = ("Vid", "de la vid", "uva"), ["oliva"] = ("Olivo", "del olivo", "aceituna"),
            ["cebada"] = ("Cebada", "de la cebada", "cebada"), ["sesamo"] = ("Sésamo", "del sésamo", "sesamo"),
        };

        public event Action AlCambiarCapas;

        void AbrirCapas()
        {
            listaCapas.Clear();
            listaCapas.RemoveFromClassList("oculto");
            var titulo = new Label("CAPAS");
            titulo.AddToClassList("mapa-capas-titulo");
            listaCapas.Add(titulo);
            var nombres = new Toggle { name = "capa-nombres", text = "Nombres de los nodos", value = NombresDeNodos };
            nombres.AddToClassList("mapa-capas-fila");
            nombres.RegisterValueChangedCallback(e => { NombresDeNodos = e.newValue; AlCambiarCapas?.Invoke(); Colocar(); });
            listaCapas.Add(nombres);
            var casillas = new List<Toggle>();
            foreach (var cultivo in Cultivos())
            {
                if (!NombreDeCultivo.TryGetValue(cultivo, out var n)) continue;
                var cv = cultivo;
                var t = new Toggle { name = "capa-fertilidad-" + cv, value = CultivoDeFertilidad == cv };
                t.AddToClassList("mapa-capas-fila");
                var etiqueta = IconosDelMundo.ConIcono("recurso", n.da, new Label("Fertilidad " + n.de), 16);
                t.Add(etiqueta);
                t.RegisterValueChangedCallback(e =>
                {
                    if (e.newValue) { CultivoDeFertilidad = cv; foreach (var o in casillas) if (o != t) o.SetValueWithoutNotify(false); }
                    else if (CultivoDeFertilidad == cv) CultivoDeFertilidad = null;
                    listaCapas.Q("leyenda-fertilidad")?.EnableInClassList("oculto", CultivoDeFertilidad == null);
                    AlCambiarCapas?.Invoke();
                    Colocar();
                });
                casillas.Add(t);
                listaCapas.Add(t);
            }
            var leyenda = Caja("mapa-capas-leyenda");
            leyenda.name = "leyenda-fertilidad";
            leyenda.EnableInClassList("oculto", CultivoDeFertilidad == null);
            var rampa = new VectorDelMapa(mgc =>
            {
                var r = mgc.visualElement.contentRect;
                for (int i = 0; i < 64; i++)
                {
                    mgc.painter2D.fillColor = (Color)SueloDelMapa2D.ColorDeFertilidad(i / 63.0, FertilidadMinima) * new Color(1, 1, 1, 255f / 160f);
                    mgc.painter2D.BeginPath();
                    mgc.painter2D.MoveTo(new Vector2(r.width * i / 64f, 0)); mgc.painter2D.LineTo(new Vector2(r.width * (i + 1) / 64f + 0.5f, 0));
                    mgc.painter2D.LineTo(new Vector2(r.width * (i + 1) / 64f + 0.5f, r.height)); mgc.painter2D.LineTo(new Vector2(r.width * i / 64f, r.height));
                    mgc.painter2D.ClosePath();
                    mgc.painter2D.Fill();
                }
            });
            rampa.AddToClassList("mapa-capas-rampa");
            var corte = Caja("mapa-capas-corte");
            corte.style.left = Length.Percent((float)(FertilidadMinima * 100));
            rampa.Add(corte);
            leyenda.Add(rampa);
            var extremos = Caja("mapa-capas-extremos");
            extremos.Add(new Label("Yerma"));
            extremos.Add(new Label("Aldea desde " + FertilidadMinima.ToString("0.0").Replace('.', ',')));
            extremos.Add(new Label("Fértil"));
            leyenda.Add(extremos);
            listaCapas.Add(leyenda);
            var parcelas = new Toggle { name = "capa-parcelas", text = "Parcelas de bosque", value = Parcelas };
            parcelas.AddToClassList("mapa-capas-fila");
            listaCapas.Add(parcelas);
            var leyendaParcelas = Caja("mapa-capas-leyenda");
            leyendaParcelas.name = "leyenda-parcelas";
            leyendaParcelas.EnableInClassList("oculto", !Parcelas);
            foreach (var (estado, texto) in new[] { ("libre", "Libre: cabe una Leñera"), ("ocupada", "Con Leñera"), ("ajena", "En zona de otra Facción") })
            {
                var m = Caja("mapa-capas-muestra");
                var punto = Caja("mapa-capas-muestra-punto");
                var col = ColorDeParcela[estado];
                punto.style.borderTopColor = punto.style.borderBottomColor = punto.style.borderLeftColor = punto.style.borderRightColor = col;
                punto.style.backgroundColor = new Color(col.r, col.g, col.b, 0.45f);
                m.Add(punto);
                m.Add(new Label(texto));
                leyendaParcelas.Add(m);
            }
            var notaParcelas = new Label("Más llena cuanto más densa: más madera.");
            notaParcelas.AddToClassList("mapa-capas-nota");
            leyendaParcelas.Add(notaParcelas);
            listaCapas.Add(leyendaParcelas);
            parcelas.RegisterValueChangedCallback(e => { Parcelas = e.newValue; leyendaParcelas.EnableInClassList("oculto", !Parcelas); AlCambiarCapas?.Invoke(); Colocar(); });
            var nota = new Label("Solo sobre lo que has explorado.");
            nota.AddToClassList("mapa-capas-nota");
            listaCapas.Add(nota);
        }
    }

    /// <summary>Un elemento que se pinta con `Painter2D` (el lienzo del prototipo para rayas y figuras).</summary>
    public sealed class VectorDelMapa : VisualElement
    {
        public VectorDelMapa(Action<MeshGenerationContext> pintar)
        {
            pickingMode = PickingMode.Ignore;
            generateVisualContent += pintar;
        }
    }
}
