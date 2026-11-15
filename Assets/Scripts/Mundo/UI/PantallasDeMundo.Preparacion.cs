// La preparación de la batalla (paso 5, tanda 1, del bloque 4): la BattlePrepScene de Conquest (UGUI) portada a UI Toolkit, calcando su
// disposición. Las posiciones, tamaños, colores, fuentes y sprites son los de la original a 1950x1130 (el inventario de
// `PreparacionDeConquestTests`), y el lienzo se escala entero para caber en la ventana. Sustituye a la ficha de espera de la 5A: desde que
// se ataca hasta que el servidor de batalla da la salida, en su propia escena (PreparacionScene), con la sesión viva.
//
// Lo de la original, en su sitio:
//   - cabecera: el nombre del mapa, el temporizador en rojo (aquí los 60 s de la preparación del servidor de batalla) y la raya;
//   - el minimapa, que es el mismo prefab de la original (`MapDataSO.preparationMap`) pintado en una textura, con su punto de aparición
//     marcado (el que reparte el servidor: no se elige);
//   - la tira de héroes de tu bando (clase, nombre, nivel y la miniatura de cada escuadra);
//   - el pie: el Liderazgo y las tarjetas de tropa (PiezasDeTropa, como Squad_option_Prep);
//   - el equipo (las piezas que lleva en la batalla) y el botón amarillo de abajo a la derecha.
// Lo que hacemos distinto, en el sitio de lo más parecido:
//   - el orden y la formación de cada escuadra, sobre su tarjeta: ◀ ▶ y la formación (con la primera entra el héroe; las demás, para los
//     puntos de suministro). Lo aplica solo el servidor de batalla al montar esa batalla: el motor no guarda nada (Víctor, 2026-11-14), y
//     cada preparación arranca con lo que el héroe tiene en el motor;
//   - «Listo» (o «Volver al mapa» si se canceló o se anuló) en el botón amarillo, que en la original es «Start»;
//   - «Cancelar la batalla» en el sitio de «Warband Builds»; el estado y el plazo del motor, a la derecha de la cabecera;
//   - quién está listo, al final de su fila en la tira de héroes; el aviso de dónde se cambia el equipo, encima de él.
// Lo que no tiene dato detrás se oculta sin mover lo demás: «Save Build», las selecciones guardadas, elegir el punto de aparición y el
// inventario. Los huecos «+» del riel se ven, apagados (contra bandidos no se añade tropa; Víctor, 2026-11-14). Docs/Arquitectura/28.
using System;
using System.Collections.Generic;
using System.Linq;
using BronzeAge.Contract.V1;
using ConquestTactics.Net;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace ConquestTactics.Mundo.UI
{
    public sealed partial class PantallasDeMundo
    {
        /// <summary>El tamaño de la BattlePrepScene original, en el que están todas las medidas de aquí.</summary>
        const float AnchoOriginal = 1950, AltoOriginal = 1130;
        /// <summary>Los huecos del riel de tropas de la original (Troops_Container_Placeholder).</summary>
        const int HuecosDelRiel = 5;

        /// <summary>Lo elegido en la preparación de la batalla de ahora: el orden de las escuadras propias (ids del ticket) y la formación de cada una.</summary>
        readonly List<string> ordenElegido = new List<string>();
        readonly Dictionary<string, string> formacionElegida = new Dictionary<string, string>();
        MapaDeLaBatalla mapaDeLaBatalla;

        /// <summary>Las escuadras propias de la batalla, tal como entran en el ticket (el orden y la formación del héroe en el motor).</summary>
        List<BronzeAge.Contract.V1.SquadSnapshot> MisEscuadras(Batalla b) =>
            new[] { b.Ticket?.Atacante, b.Ticket?.Defensor }.Where(l => l != null).SelectMany(l => l.Participantes)
                .Where(p => p.HeroeId == c.HeroeId).SelectMany(p => p.Escuadras).ToList();

        void EmpezarPreparacion(Batalla b)
        {
            ordenElegido.Clear();
            formacionElegida.Clear();
        }

        /// <summary>El orden de ahora: el elegido y, si aún no hay, el del ticket (que puede llegar después de abrir la preparación).</summary>
        List<BronzeAge.Contract.V1.SquadSnapshot> EnOrden(Batalla b)
        {
            var mias = MisEscuadras(b);
            if (ordenElegido.Count == 0) ordenElegido.AddRange(mias.Select(s => s.SquadId));
            foreach (var s in mias) if (!formacionElegida.ContainsKey(s.SquadId)) formacionElegida[s.SquadId] = string.IsNullOrEmpty(s.Formacion) ? "reposo" : s.Formacion;
            return mias.OrderBy(s => { int i = ordenElegido.IndexOf(s.SquadId); return i < 0 ? int.MaxValue : i; }).ToList();
        }

        /// <summary>Lo elegido, a la red: el cliente de batalla lo manda al servidor cada segundo hasta que empieza.</summary>
        void LoElegidoAlServidor()
        {
            PreparacionRed.Orden.Clear();
            PreparacionRed.Orden.AddRange(ordenElegido);
            PreparacionRed.Formaciones.Clear();
            foreach (var kv in formacionElegida) PreparacionRed.Formaciones[kv.Key] = kv.Value;
        }

        /// <summary>Lo que dice el servidor de batalla, para repintar cuando cambia (quién está listo, punto de aparición).</summary>
        static string FirmaDelServidor() =>
            !PreparacionRed.Conectado ? "-" : string.Join(",", PreparacionRed.Heroes.Select(h => $"{h.heroe}:{h.estado}:{h.punto}")) + (PreparacionRed.Listo ? "|listo" : "");

        /// <summary>Las familias de formación del motor (`FORMACIONES`): id → nombre.</summary>
        Dictionary<string, string> NombresDeFormacion()
        {
            try
            {
                return c.Datos.Bloque("Heroe_Y_Tropa", "FORMACIONES")["familias"].OfType<JObject>().ToDictionary(f => (string)f["id"], f => (string)f["nombre"]);
            }
            catch (FaltaDato) { return new Dictionary<string, string>(); }
        }

        /// <summary>Las formaciones que puede tomar una escuadra: las que permite el ticket y tiene su tropa en Conquest (en reposo, siempre).</summary>
        static List<string> FormacionesDe(BronzeAge.Contract.V1.SquadSnapshot s)
        {
            var tropa = PiezasDeTropa.Datos(s.TropaId);
            var tiene = new HashSet<FormationType>((tropa?.formations ?? Array.Empty<FormationDefinition>()).Where(f => f != null).Select(f => f.formationType)) { FormationType.Rest };
            return s.FormacionesPermitidas.Where(f => BatallaRealDeTicket.Familia(f) is FormationType t && tiene.Contains(t)).DefaultIfEmpty("reposo").Distinct().ToList();
        }

        /// <summary>Una caja en la posición y el tamaño de la original (píxeles a 1950x1130).</summary>
        static VisualElement En(VisualElement padre, float x, float y, float ancho, float alto, string clases = null, string nombre = null)
        {
            var e = new VisualElement { name = nombre, pickingMode = PickingMode.Ignore };
            Colocar(e, x, y, ancho, alto);
            if (clases != null) foreach (var cl in clases.Split(' ')) e.AddToClassList(cl);
            padre.Add(e);
            return e;
        }

        static void Colocar(VisualElement e, float x, float y, float ancho, float alto)
        {
            e.style.position = Position.Absolute;
            e.style.left = x;
            e.style.top = y;
            e.style.width = ancho;
            e.style.height = alto;
        }

        static Label Texto(VisualElement padre, string texto, string clases, string nombre = null)
        {
            var l = new Label(texto) { name = nombre, pickingMode = PickingMode.Ignore };
            foreach (var cl in clases.Split(' ')) l.AddToClassList(cl);
            padre.Add(l);
            return l;
        }

        void PintarPreparacion(Batalla b)
        {
            if (panelBatalla == null)
            {
                panelBatalla = Dialogo.Caja("prep-fondo");
                panelBatalla.name = "panel-batalla";
                capaEncima.Add(panelBatalla);
                // En la escena de preparación no hay mapa que la refresque: se vigila sola.
                RepintarCada(panelBatalla, 250, VigilarBatalla);
                // El lienzo de 1950x1130, escalado entero para caber, centrado.
                panelBatalla.RegisterCallback<GeometryChangedEvent>(_ => Encajar());
            }
            panelBatalla.Clear();
            var lienzo = new VisualElement { name = "prep-lienzo", pickingMode = PickingMode.Ignore };
            lienzo.AddToClassList("prep-lienzo");
            panelBatalla.Add(lienzo);
            Encajar();

            var h = HeroeAhora();
            bool activa = Activa(b);
            bool cambiable = activa && !PreparacionRed.Listo;
            var mio = PreparacionRed.Heroes.FirstOrDefault(x => x.heroe == c.HeroeId);
            int punto = PreparacionRed.Conectado ? mio.punto : 0;

            // El minimapa (minimapContainer: 15,99 1920x1006), el mismo prefab de la original pintado en una textura.
            mapaDeLaBatalla ??= MapaDeLaBatalla.Crear(1920, 1006);
            var mapa = En(lienzo, 15, 99, 1920, 1006, null, "prep-mapa");
            if (mapaDeLaBatalla != null)
            {
                mapaDeLaBatalla.Marcar(punto);
                mapa.style.backgroundImage = Background.FromRenderTexture(mapaDeLaBatalla.Textura);
            }

            // La cabecera (Header): el nombre del mapa, el temporizador y la raya; a la derecha, el estado y el plazo del motor.
            Texto(En(lienzo, 95, 37, 243, 30), mapaDeLaBatalla?.Nombre ?? "", "prep-mapa-nombre");
            En(lienzo, 30, 72, 1890, 2, "prep-raya");
            Texto(En(lienzo, 875, 32, 200, 40), "", "prep-temporizador", "prep-queda");
            var estadoCaja = En(lienzo, 1180, 22, 740, 50, "prep-estado-caja");
            var estado = EstadoDeBatalla.TryGetValue(b.Estado, out var e) ? e : b.Estado.ToString();
            var bandidos = BandidosDe(b);
            Texto(estadoCaja, $"{(bandidos != null ? $"Contra bandidos{(bandidos.Nivel > 0 ? $" · nivel {bandidos.Nivel}" : "")}" : "Batalla")} · {estado}", "prep-estado", "batalla-estado");
            Texto(estadoCaja, "", "prep-plazo", "batalla-plazo");

            // La tira de héroes de tu bando (Hero_Slice_Container: 0,130; filas de 580x40 cada 45).
            var propio = b.LadoPropio == LadoId.Defensor ? b.Ticket?.Defensor : b.Ticket?.Atacante;
            int fila = 0;
            foreach (var p in propio?.Participantes ?? Enumerable.Empty<BattleParticipantSnapshot>())
                FilaDeHeroe(lienzo, p, 130 + 45 * fila++);

            // El pie (Footer): el Liderazgo, los botones y las tarjetas.
            var mias = EnOrden(b);
            var usado = mias.Sum(s => LiderazgoDe(s.TropaId) ?? 0);
            // Leadership_Viewer (1081,808 270x40): el icono y la cifra (Text: 1135,808 162x40).
            En(lienzo, 1098, 813, 30, 30, "prep-liderazgo-icono");
            Texto(En(lienzo, 1135, 808, 162, 40), h != null ? $"{usado}/{h.LiderazgoBase}" : $"{usado}", "prep-liderazgo", "liderazgo");

            var error = Texto(En(lienzo, 481, 760, 870, 40), "", "prep-error oculto", "error");
            if (activa && h != null && b.IniciadaPor == h.Id && (b.Estado == EstadoBatalla.BatallaConvocando || b.Estado == EstadoBatalla.BatallaAsignada))
            {
                // Solo quien la inició, y antes de que empiece (`batalla.no_puede_cancelar`). En el sitio de «Warband Builds».
                Button cancelar = null;
                cancelar = new Button(async () =>
                {
                    cancelar.SetEnabled(false);
                    Poner(error, null);
                    try
                    {
                        var r = await c.Sesion.Ordenar(new Orden { CancelarBatalla = new OrdenCancelarBatalla { BattleId = b.Id } });
                        if (r.Resultado != Resultado.OrdenAceptada) Poner(error, Textos.Explicar(r.Rechazo));
                    }
                    catch (ErrorDeSesion x) { Poner(error, x.Message); }
                    if (cancelar.panel != null) cancelar.SetEnabled(true);
                }) { text = "Cancelar la batalla", name = "batalla-cancelar" };
                cancelar.AddToClassList("prep-boton-negro");
                Colocar(cancelar, 582, 806, 179, 40);
                lienzo.Add(cancelar);
            }

            var nombres = NombresDeFormacion();
            for (int i = 0; i < mias.Count; i++) TarjetaDePreparacion(lienzo, b, mias, i, cambiable, nombres);
            // Los huecos que quedan del riel (Troops_Container_Placeholder: 5, cada 160 desde 520,856), apagados: contra bandidos no se añade
            // tropa, pero el riel se ve entero, como en la original.
            for (int i = mias.Count; i < HuecosDelRiel; i++)
            {
                var hueco = En(lienzo, 520 + 160 * i, 856, 150, 237, "prep-hueco", "prep-hueco-" + i);
                En(hueco, 0, 0, 150, 237, "prep-hueco-mas");
                hueco.pickingMode = PickingMode.Position;
                hueco.tooltip = "Contra bandidos combates con lo que lleva tu columna: aquí no se añade tropa";
            }
            if (mias.Count == 0)
                Texto(En(lienzo, 520, 940, 800, 40), b.Ticket == null ? "Aún no ha llegado quién combate." : "Entras sin escuadras: solo tu héroe.", "prep-aviso");

            // El equipo (EquipmentPanel) y el aviso de dónde se cambia, encima.
            PintarEquipo(lienzo, h);

            // Qué pasa ahora (la frase del motor), encima del equipo.
            Texto(En(lienzo, 1360, 560, 560, 60, "prep-ahora-caja"), QuePasa(b), "prep-ahora", "batalla-ahora");

            // El botón amarillo (Start: 1504,1027 331x60): «Listo», o «Volver al mapa» si la batalla se canceló o se anuló.
            Button boton;
            if (activa)
            {
                boton = new Button(() =>
                {
                    PreparacionRed.Listo = true;
                    LoElegidoAlServidor();
                    firmaBatalla = null;
                }) { text = PreparacionRed.Listo ? "Listo ✓" : "Listo", name = "prep-listo" };
                boton.SetEnabled(entrando && PreparacionRed.Conectado && !PreparacionRed.Listo);
                boton.tooltip = entrando && PreparacionRed.Conectado ? "Entras con lo elegido en cuanto estén todos listos" : "Se puede en cuanto haya servidor de batalla";
            }
            else boton = new Button(() => _ = VolverAlMundo()) { text = "Volver al mapa", name = "batalla-cerrar" };
            boton.AddToClassList("prep-start");
            Colocar(boton, 1504, 1027, 331, 60);
            lienzo.Add(boton);
            PonerPlazo(b);
        }

        /// <summary>El lienzo de 1950x1130 escalado para caber entero en la ventana, centrado (la original no escala: se sale en las pequeñas).</summary>
        void Encajar()
        {
            var lienzo = panelBatalla?.Q("prep-lienzo");
            if (lienzo == null) return;
            float w = panelBatalla.resolvedStyle.width, alto = panelBatalla.resolvedStyle.height;
            if (float.IsNaN(w) || w <= 0 || float.IsNaN(alto) || alto <= 0) return;
            float s = Mathf.Min(w / AnchoOriginal, alto / AltoOriginal);
            lienzo.style.scale = new Scale(new Vector2(s, s));
            lienzo.style.left = (w - AnchoOriginal * s) / 2;
            lienzo.style.top = (alto - AltoOriginal * s) / 2;
        }

        /// <summary>Una fila de la tira de héroes (HeroSlice): el fondo en degradado, la clase en su círculo, el nombre, el nivel en su círculo
        /// naranja y la miniatura de cada escuadra (Squad_Icon). Al final, si hay servidor, cómo está en la preparación.</summary>
        void FilaDeHeroe(VisualElement lienzo, BattleParticipantSnapshot p, float y)
        {
            var fila = En(lienzo, 5, y, 580, 40, "prep-heroe", "heroe-" + p.HeroeId);
            En(fila, 45, 0, 40, 40, "prep-circulo prep-circulo--negro");
            En(fila, 49, 3, 33, 33, "prep-circulo prep-circulo--clase");
            En(fila, 45, 0, 40, 40, "prep-anillo");
            var clase = HeroClassManager.GetClassDefinition(BatallaRealDeTicket.Clase(p.Heroe?.ClassDefinitionId));
            if (clase?.icon != null) En(fila, 52, 7, 26, 26).style.backgroundImage = new StyleBackground(clase.icon);
            var nombre = c.Sesion.Almacen.NombresHeroes.TryGetValue(p.HeroeId, out var n) ? n : p.Heroe?.DisplayName ?? p.HeroeId;
            Texto(En(fila, 90, 0, 160, 40), p.HeroeId == c.HeroeId ? $"{nombre} (tú)" : nombre, "prep-heroe-nombre");
            var nivel = En(fila, 250, 0, 40, 40, "prep-circulo prep-circulo--nivel");
            Texto(nivel, ((int)(p.Heroe?.Nivel ?? 1)).ToString(), "prep-heroe-nivel");
            float x = 340;
            foreach (var s in p.Escuadras)
            {
                var mini = PiezasDeTropa.Miniatura(s.TropaId, 40, NombreDeTropa(s.TropaId));
                if (mini == null) continue;
                mini.style.position = Position.Absolute;
                mini.style.left = x;
                mini.style.top = 0;
                fila.Add(mini);
                x += 45;
            }
            var enPrep = PreparacionRed.Heroes.FirstOrDefault(h => h.heroe == p.HeroeId);
            if (PreparacionRed.Conectado && enPrep.heroe != null)
                Texto(En(fila, 470, 0, 105, 40), enPrep.estado == PreparacionRed.ConListo ? "listo" : enPrep.estado == PreparacionRed.ConectadoSinListo ? "preparándose" : "sin conectar",
                    enPrep.estado == PreparacionRed.ConListo ? "prep-heroe-estado prep-heroe-estado--listo" : "prep-heroe-estado");
        }

        /// <summary>
        /// Una tarjeta del pie (Squad_option_Prep, 150x237, cada 160 desde 520,856), con el orden y la formación encima: ◀ ▶ y la formación en
        /// una franja sobre la parte alta de la tarjeta. La primera, marcada como la elegida: con ella entra el héroe.
        /// </summary>
        void TarjetaDePreparacion(VisualElement lienzo, Batalla b, List<BronzeAge.Contract.V1.SquadSnapshot> mias, int puesto, bool cambiable, Dictionary<string, string> nombres)
        {
            var s = mias[puesto];
            var id = s.SquadId;
            var caja = En(lienzo, 520 + 160 * puesto, 856, 150, 237, "prep-escuadra", "prep-escuadra-" + id);
            caja.pickingMode = PickingMode.Position;
            var total = (int?)c.Datos.TropasReclutables?.FirstOrDefault(x => (string)x["id"] == s.TropaId)?["unidadesPorDefecto"];
            var tarjeta = PiezasDeTropa.Tarjeta(s.TropaId, LiderazgoDe(s.TropaId), (int)s.EfectivosAutorizados, total, puesto == 0, 150);
            if (tarjeta != null) { tarjeta.style.marginRight = 0; tarjeta.style.marginBottom = 0; caja.Add(tarjeta); }
            caja.tooltip = $"{NombreDeTropa(s.TropaId)} · {s.EfectivosAutorizados} hombres" + (puesto == 0 ? " · entras con esta" : " · en reserva, para los puntos de suministro");

            var franja = Dialogo.Caja("prep-franja");
            void Mover(int a)
            {
                ordenElegido.Clear();
                ordenElegido.AddRange(mias.Select(x => x.SquadId));
                ordenElegido.RemoveAt(puesto);
                ordenElegido.Insert(a, id);
                LoElegidoAlServidor();
                firmaBatalla = null;
                PintarPreparacion(b);
            }
            var antes = new Button(() => Mover(puesto - 1)) { text = "◀", name = "antes-" + id, tooltip = "Antes" };
            antes.AddToClassList("prep-flecha");
            antes.SetEnabled(cambiable && puesto > 0);
            var opciones = FormacionesDe(s);
            var elegida = formacionElegida.TryGetValue(id, out var f) && opciones.Contains(f) ? f : opciones[0];
            var formacion = new DropdownField(opciones.Select(x => nombres.TryGetValue(x, out var nom) ? nom : x).ToList(), opciones.IndexOf(elegida)) { name = "formacion-" + id, tooltip = "Formación con la que empieza" };
            formacion.AddToClassList("prep-formacion");
            formacion.SetEnabled(cambiable && opciones.Count > 1);
            formacion.RegisterValueChangedCallback(_ =>
            {
                formacionElegida[id] = opciones[formacion.index];
                LoElegidoAlServidor();
            });
            var despues = new Button(() => Mover(puesto + 1)) { text = "▶", name = "despues-" + id, tooltip = "Después" };
            despues.AddToClassList("prep-flecha");
            despues.SetEnabled(cambiable && puesto < mias.Count - 1);
            franja.Add(antes);
            franja.Add(formacion);
            franja.Add(despues);
            caja.Add(franja);
        }

        /// <summary>
        /// El equipo (EquipmentPanel): arma y casco, torso y guantes, pantalones y botas en casillas de 98, y la montura al lado. Las piezas
        /// de armadura, con la miniatura de la pieza de Conquest que lleva en la batalla (la de inicio de su tipo); lo que Conquest aún no tiene
        /// (el arma, el casco y la montura del motor), con su nombre. Aquí no se cambia: se dice dónde.
        /// </summary>
        void PintarEquipo(VisualElement lienzo, Heroe h)
        {
            var eq = h?.Equipamiento;
            Texto(En(lienzo, 1502, 628, 333, 40, "prep-equipo-aviso-caja"), "El equipo se cambia dentro de una plaza, un pueblo o un campamento.", "prep-equipo-aviso");
            List<JObject> objetos;
            try { objetos = c.Datos.Bloque("Objetos_Del_Heroe", "OBJETOS")["objetos"].OfType<JObject>().ToList(); }
            catch (FaltaDato) { objetos = new List<JObject>(); }
            string Nombre(ItemInstancia o) => (string)objetos.FirstOrDefault(x => (string)x["id"] == o.ItemDefinitionId)?["nombre"] ?? o.ItemDefinitionId;
            var tipo = BatallaRealDeTicket.TipoDeArmadura(eq?.Torso?.ItemDefinitionId);
            void Casilla(float x, float y, string nombre, ItemInstancia pieza, string deConquest)
            {
                var casilla = En(lienzo, x, y, 98, 98, "prep-casilla", "equipo-" + nombre);
                casilla.pickingMode = PickingMode.Position;
                if (pieza == null || string.IsNullOrEmpty(pieza.ItemDefinitionId)) return;
                casilla.tooltip = $"{nombre}: {Nombre(pieza)}";
                var item = deConquest == null ? null : ItemService.GetItemById(deConquest);
                if (item?.icon != null)
                {
                    casilla.AddToClassList("prep-casilla--llena");
                    En(casilla, 0, 0, 98, 98).style.backgroundImage = new StyleBackground(item.icon);
                }
                else Texto(casilla, Nombre(pieza), "prep-casilla-texto");
            }
            Casilla(1502, 674, "Arma", eq?.Arma, null);
            Casilla(1620, 674, "Casco", eq?.Casco, null);
            Casilla(1502, 791, "Torso", eq?.Torso, tipo == null ? null : $"Tor{tipo}ADef");
            Casilla(1620, 791, "Guantes", eq?.Guantes, tipo == null ? null : $"Glo{tipo}ADef");
            Casilla(1502, 909, "Pantalones", eq?.Pantalones, tipo == null ? null : $"Pan{tipo}ADef");
            Casilla(1620, 909, "Botas", eq?.Botas, tipo == null ? null : $"Boot{tipo}ADef");
            Casilla(1736, 909, "Montura", eq?.Montura, null);
        }
    }

    /// <summary>
    /// El minimapa de la batalla de la preparación: el mismo prefab de la original (`MapDataSO.preparationMap` del mapa `default`, el que usa
    /// el servidor de batalla), en un lienzo UGUI que una cámara propia pinta en una textura, lejos de la escena. Sin EventSystem: no se
    /// pulsa nada. El punto de aparición propio se marca con la selección de la original.
    /// </summary>
    public sealed class MapaDeLaBatalla
    {
        public RenderTexture Textura;
        public string Nombre;
        GameObject raiz;
        SpawnPointControllerUI[] apariciones = Array.Empty<SpawnPointControllerUI>();

        public static MapaDeLaBatalla Crear(int ancho, int alto)
        {
            var datos = MapService.GetMapById("default");
            if (datos?.preparationMap == null) return null;
            var r = new MapaDeLaBatalla { Nombre = datos.name, Textura = new RenderTexture(ancho, alto, 24) };
            r.raiz = new GameObject("Minimapa de la preparación");
            r.raiz.transform.position = new Vector3(0, -10000, 0);
            var camara = new GameObject("Camara del minimapa").AddComponent<Camera>();
            camara.transform.SetParent(r.raiz.transform, false);
            camara.clearFlags = CameraClearFlags.SolidColor;
            camara.backgroundColor = new Color32(0x38, 0x38, 0x38, 0xff);
            camara.cullingMask = 1 << 5;
            camara.targetTexture = r.Textura;
            var lienzo = new GameObject("Lienzo del minimapa", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler)).GetComponent<Canvas>();
            lienzo.transform.SetParent(r.raiz.transform, false);
            lienzo.gameObject.layer = 5;
            lienzo.renderMode = RenderMode.ScreenSpaceCamera;
            lienzo.worldCamera = camara;
            lienzo.planeDistance = 1;
            var mapa = UnityEngine.Object.Instantiate(datos.preparationMap, lienzo.transform);
            var rt = (RectTransform)mapa.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            foreach (var t in mapa.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 5;
            r.apariciones = mapa.GetComponentsInChildren<SpawnPointControllerUI>(true);
            return r;
        }

        /// <summary>Marca el punto de aparición propio (0: ninguno todavía).</summary>
        public void Marcar(int punto)
        {
            foreach (var a in apariciones) if (a != null) a.SetSelected(a.spawnPointId == punto.ToString());
        }

        public void Quitar()
        {
            if (raiz != null) UnityEngine.Object.Destroy(raiz);
            Textura?.Release();
        }
    }
}
