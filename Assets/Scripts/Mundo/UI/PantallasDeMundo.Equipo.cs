// El equipo del héroe en la Base (paso 6 del bloque 4, tanda 2): los objetos del motor (`OBJETOS` de /v1/datos) con su imagen y su rareza, su ficha
// al pasar el ratón (el aspecto de BronzeAge con los valores de Conquest y la comparación con lo puesto), equipar y quitar desde las casillas del
// héroe, usar consumibles y medallas, y repartir los puntos de atributo. Todo con las órdenes del motor y solo donde el motor lo deja; donde no,
// el botón no sale, sin explicación (Víctor, duda 20).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BronzeAge.Contract.V1;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace ConquestTactics.Mundo.UI
{
    public sealed partial class PantallasDeMundo
    {
        static readonly CultureInfo Es = CultureInfo.GetCultureInfo("es-ES");

        /// <summary>Los puntos de atributo repartidos a medias (sin guardar): atributo del motor → puntos.</summary>
        readonly Dictionary<string, int> puntosARepartir = new Dictionary<string, int>();

        /// <summary>La ficha de un objeto en `OBJETOS`, o null si el motor no la trae.</summary>
        JObject FichaDelMotor(string id) =>
            (c.Datos.Bloque("Objetos_Del_Heroe", "OBJETOS")?["objetos"] as JArray)?.OfType<JObject>().FirstOrDefault(x => (string)x["id"] == id);

        /// <summary>¿Se puede cambiar el equipo aquí? Como el motor (`equipo.rs`): dentro de una plaza, un pueblo o un campamento.</summary>
        static bool EquipoAMano(Heroe h) => h.Ubicacion?.DondeCase is UbicacionHeroe.DondeOneofCase.AsentamientoId
            or UbicacionHeroe.DondeOneofCase.PuebloId or UbicacionHeroe.DondeOneofCase.CampamentoId or UbicacionHeroe.DondeOneofCase.AvanzadaId;

        /// <summary>La imagen de un objeto: el icono de Conquest de su pieza (la armadura por hueco y peso, el arma por clase) o, si Conquest no la
        /// tiene, la del prototipo para su tipo. Para un elemento de UI Toolkit.</summary>
        VisualElement ImagenDe(ItemInstancia pieza, float lado)
        {
            var ficha = FichaDelMotor(pieza.ItemDefinitionId);
            var idConquest = VestidoDelMotor.PiezaDeConquest(pieza.ItemDefinitionId)
                ?? (pieza.Tipo == "arma" ? VestidoDelMotor.ArmaDelMotor((string)ficha?["clase"]) : null);
            var icono = idConquest == null ? null : ItemService.GetItemById(idConquest)?.icon;
            if (icono != null)
            {
                var v = new VisualElement { pickingMode = PickingMode.Ignore };
                v.AddToClassList("icono");
                v.style.width = v.style.height = lado;
                v.style.backgroundImage = new StyleBackground(icono);
                // El metal, como tinte (Víctor: «metales como tinte por ahora»).
                if (pieza.Tipo == "armadura") v.style.unityBackgroundImageTintColor = VestidoDelMotor.TinteDeMetal(VestidoDelMotor.Metal(pieza.ItemDefinitionId));
                return v;
            }
            return IconosDelMundo.Icono("recurso", ImagenDeObjeto(pieza), lado);
        }

        /// <summary>Al pasar el ratón por `e`, la ficha del objeto que da `pieza` (comparada con lo puesto en su hueco, si no es lo mismo).</summary>
        void ConFicha(VisualElement e, Func<ItemInstancia> pieza)
        {
            VisualElement abierta = null;
            e.RegisterCallback<PointerEnterEvent>(ev =>
            {
                abierta?.RemoveFromHierarchy();
                var p = pieza();
                if (p == null) return;
                var h = HeroeAhora();
                var puesta = h == null ? null : PuestaEnHueco(h, (string)FichaDelMotor(p.ItemDefinitionId)?["hueco"]);
                abierta = TarjetaDeObjeto(p, puesta != null && !(puesta.ItemDefinitionId == p.ItemDefinitionId && puesta.CasillaInventario == p.CasillaInventario && EsPuesta(h, p)) ? puesta : null);
                abierta.name = "ficha-objeto";
                // La ficha no se queda con el ratón (ni ella ni lo de dentro).
                foreach (var x in abierta.Query<VisualElement>().ToList()) x.pickingMode = PickingMode.Ignore;
                capaEncima.Add(abierta);
                var sitio = e.worldBound;
                abierta.schedule.Execute(() =>
                {
                    if (abierta.panel == null) return;
                    var raiz = capaEncima.worldBound;
                    var ancho = abierta.resolvedStyle.width;
                    var alto = abierta.resolvedStyle.height;
                    var x = sitio.xMax + 8 + ancho > raiz.xMax ? sitio.xMin - ancho - 8 : sitio.xMax + 8;
                    var y = Mathf.Clamp(sitio.yMin, raiz.yMin + 8, raiz.yMax - alto - 8);
                    abierta.style.left = x - raiz.xMin;
                    abierta.style.top = y - raiz.yMin;
                    abierta.style.visibility = Visibility.Visible;
                });
            });
            // Se va al salir de verdad (la salida también llega al pasar por encima de un hijo: entonces el puntero sigue dentro).
            e.RegisterCallback<PointerLeaveEvent>(ev => { if (e.worldBound.Contains(ev.position)) return; abierta?.RemoveFromHierarchy(); abierta = null; });
            e.RegisterCallback<DetachFromPanelEvent>(_ => { abierta?.RemoveFromHierarchy(); abierta = null; });
        }

        static bool EsPuesta(Heroe h, ItemInstancia p)
        {
            var e = h.Equipamiento;
            return e != null && new[] { e.Arma, e.Casco, e.Torso, e.Guantes, e.Pantalones, e.Botas, e.Montura }.Any(x => ReferenceEquals(x, p));
        }

        static ItemInstancia PuestaEnHueco(Heroe h, string hueco)
        {
            var e = h.Equipamiento;
            if (e == null || hueco == null) return null;
            return hueco switch
            {
                "arma" => e.Arma, "casco" => e.Casco, "torso" => e.Torso, "guantes" => e.Guantes,
                "pantalones" => e.Pantalones, "botas" => e.Botas, "montura" => e.Montura, _ => null,
            };
        }

        /// <summary>La ficha de un objeto: con el aspecto de las fichas de BronzeAge (imagen con su rareza, nombre, tipo) y todos los valores que
        /// enseña Conquest (daño y penetración por tipo, defensa por tipo, vida, atributos; de la montura, su vida, aguante y pasos; del consumible,
        /// su efecto), y a cada cifra su diferencia con lo puesto en ese hueco (verde si mejora, rojo si empeora).</summary>
        VisualElement TarjetaDeObjeto(ItemInstancia p, ItemInstancia comparar)
        {
            var ficha = FichaDelMotor(p.ItemDefinitionId);
            var otra = comparar == null ? null : FichaDelMotor(comparar.ItemDefinitionId);
            var (nombre, tier, rareza) = FichaDeObjeto(p);
            var t = Dialogo.Caja("objeto-ficha");
            t.pickingMode = PickingMode.Ignore;
            t.style.visibility = Visibility.Hidden;
            var cab = Dialogo.Caja("objeto-ficha-cab");
            var cuadro = Dialogo.Caja("equipo-cuadro objeto-ficha-cuadro equipo-rareza-" + tier);
            if (ImagenDe(p, 40) is VisualElement img) cuadro.Add(img);
            var linea = Dialogo.Caja("equipo-linea");
            linea.style.backgroundColor = InventoryUtils.GetRarityColor(rareza);
            cuadro.Add(linea);
            cab.Add(cuadro);
            var textos = Dialogo.Caja("objeto-ficha-textos");
            textos.Add(Dialogo.Texto(nombre, "objeto-ficha-nombre"));
            var r = Dialogo.Texto($"{NombreDeRareza(rareza)} · tier {tier}", "objeto-ficha-rareza");
            r.style.color = InventoryUtils.GetRarityColor(rareza);
            textos.Add(r);
            textos.Add(Dialogo.Texto(TipoDeObjeto(ficha, p), "objeto-ficha-tipo"));
            cab.Add(textos);
            t.Add(cab);

            JToken Valor(JObject f, string ruta) => f?.SelectToken("combate." + ruta);
            void Fila(VisualElement donde, string icono, string etiqueta, string ruta, bool menosEsMejor = false)
            {
                var v = Valor(ficha, ruta);
                if (v == null || v.Type == JTokenType.Null) return;
                var n = (double)v;
                var f = Dialogo.Caja("objeto-ficha-fila");
                if (icono != null) f.Add(Dialogo.Caja("heroe-atributo-icono objeto-ficha-icono heroe-icono-" + icono));
                f.Add(Dialogo.Texto(etiqueta, "objeto-ficha-etiqueta"));
                f.Add(Dialogo.Texto(n.ToString("0.#", Es), "objeto-ficha-valor"));
                if (comparar != null)
                {
                    var o = Valor(otra, ruta);
                    var d = n - (o == null || o.Type == JTokenType.Null ? 0 : (double)o);
                    if (Math.Abs(d) > 0.001)
                    {
                        var dif = Dialogo.Texto((d > 0 ? "+" : "") + d.ToString("0.#", Es), "objeto-ficha-dif");
                        dif.EnableInClassList("objeto-ficha-dif--mejor", menosEsMejor ? d < 0 : d > 0);
                        dif.EnableInClassList("objeto-ficha-dif--peor", menosEsMejor ? d > 0 : d < 0);
                        f.Add(dif);
                    }
                }
                donde.Add(f);
            }
            VisualElement Seccion(string titulo)
            {
                var s = Dialogo.Caja("objeto-ficha-seccion");
                s.Add(Dialogo.Texto(titulo, "objeto-ficha-seccion-titulo"));
                t.Add(s);
                return s;
            }
            if (ficha?["combate"] is JObject combate)
            {
                if (combate["dano"] != null)
                {
                    var s = Seccion("Ataque");
                    Fila(s, "perforante", "Daño perforante", "dano.perforante");
                    Fila(s, "cortante", "Daño cortante", "dano.cortante");
                    Fila(s, "contundente", "Daño contundente", "dano.contundente");
                    Fila(s, "perforante", "Penetración perforante", "penetracion.perforante");
                    Fila(s, "cortante", "Penetración cortante", "penetracion.cortante");
                    Fila(s, "contundente", "Penetración contundente", "penetracion.contundente");
                }
                if (combate["defensa"] != null || combate["salud"] != null)
                {
                    var s = Seccion("Defensa");
                    Fila(s, "vida", "Vida", "salud");
                    Fila(s, "perforante", "Defensa perforante", "defensa.perforante");
                    Fila(s, "cortante", "Defensa cortante", "defensa.cortante");
                    Fila(s, "contundente", "Defensa contundente", "defensa.contundente");
                }
                if (combate["atributos"] != null)
                {
                    var s = Seccion("Atributos");
                    Fila(s, "fuerza", "Fuerza", "atributos.fuerza");
                    Fila(s, "destreza", "Destreza", "atributos.destreza");
                    Fila(s, "armadura", "Armadura", "atributos.armadura");
                    Fila(s, "dureza", "Dureza", "atributos.vitalidad");
                }
                if (combate["galope"] != null)
                {
                    var s = Seccion("Montura");
                    Fila(s, "vida", "Vida", "vida");
                    Fila(s, "moral", "Aguante", "aguante");
                    Fila(s, "velocidad", "Trote", "trote");
                    Fila(s, "velocidad", "Galope", "galope");
                }
            }
            if (ficha?["efecto"] is JObject efecto)
            {
                var s = Seccion("Al usarlo");
                foreach (var kv in efecto)
                    s.Add(Dialogo.Texto(kv.Key switch
                    {
                        "experienciaTropa" => $"+{kv.Value} de experiencia a una escuadra tuya",
                        "experienciaHeroePct" => $"+{kv.Value} % de la experiencia que pide tu nivel",
                        "monturaDe" => "Una montura: " + string.Join(", ", (kv.Value as JArray)?.Select(x => $"{(string)FichaDelMotor((string)x["objeto"])?["nombre"] ?? (string)x["objeto"]} ({(double)x["probabilidad"] * 100:0} %)") ?? new string[0]),
                        _ => $"{kv.Key}: {kv.Value}",
                    }, "objeto-ficha-texto"));
            }
            foreach (var est in p.Estadisticas)
                t.Add(Dialogo.Texto($"{est.Nombre}: {est.Valor}", "objeto-ficha-texto"));
            var pie = Dialogo.Caja("objeto-ficha-pie");
            if (p.Cantidad > 1) pie.Add(Dialogo.Texto($"×{p.Cantidad}", "objeto-ficha-texto"));
            if ((int?)ficha?["precio"] is int precio && precio > 0) pie.Add(IconosDelMundo.ConIcono("glifo", "monedas", Dialogo.Texto(precio.ToString("#,0", Es), "objeto-ficha-texto"), 14));
            t.Add(pie);
            if (comparar != null) t.Add(Dialogo.Texto($"Comparado con lo puesto: {FichaDeObjeto(comparar).nombre}", "objeto-ficha-compara"));
            return t;
        }

        static string NombreDeRareza(ItemRarity r) => r switch
        {
            ItemRarity.Uncommon => "Poco común", ItemRarity.Rare => "Rara", ItemRarity.Epic => "Épica", ItemRarity.Legendary => "Legendaria", _ => "Común",
        };

        string TipoDeObjeto(JObject f, ItemInstancia p)
        {
            var hueco = (string)f?["hueco"];
            var metal = (string)f?["metal"];
            var deMetal = string.IsNullOrEmpty(metal) || metal == "inicio" ? "" : " · " + metal.Replace('_', ' ');
            return p.Tipo switch
            {
                "armadura" => $"Armadura {(string)f?["tipoArmadura"]} · {hueco}{deMetal}",
                "arma" => $"Arma · {ClaseDeHeroe.Leer(c.Datos).FirstOrDefault(k => k.Id == (string)f?["clase"])?.Nombre ?? (string)f?["clase"]}{deMetal}",
                "montura" => "Montura",
                "consumible" => "Consumible",
                "material" => "Material",
                _ => p.Tipo,
            };
        }

        // ─────────────── equipar y quitar ───────────────

        /// <summary>Al pulsar una casilla del equipo (como en Conquest): la lista de lo de la mochila que va en ese hueco para ponérselo y, si lleva algo
        /// que se quita (todo menos el arma), «Quitar». Solo donde el motor deja cambiar el equipo; fuera, la casilla no hace nada.</summary>
        void ElegirParaHueco(string hueco, string nombreHueco, VisualElement casilla, Heroe h)
        {
            capaEncima.Q("elegir-equipo")?.RemoveFromHierarchy();
            capaEncima.Q("ficha-objeto")?.RemoveFromHierarchy(); // la de la casilla pulsada no tapa la lista
            var velo = Dialogo.Caja("elegir-equipo-velo");
            velo.name = "elegir-equipo";
            velo.RegisterCallback<PointerDownEvent>(ev => { if (ev.target == velo) velo.RemoveFromHierarchy(); });
            var lista = Dialogo.Caja("elegir-equipo");
            velo.Add(lista);
            lista.Add(Dialogo.Texto(nombreHueco.ToUpperInvariant(), "base-kicker elegir-equipo-titulo"));
            var error = Dialogo.Texto("", "error oculto");
            var candidatas = h.Inventario.Where(p => (string)FichaDelMotor(p.ItemDefinitionId)?["hueco"] == hueco).OrderBy(p => p.CasillaInventario).ToList();
            var rejilla = Dialogo.Caja("elegir-equipo-rejilla");
            foreach (var p in candidatas)
            {
                var pieza = p;
                var (_, tier, rareza) = FichaDeObjeto(p);
                Button b = null;
                b = new Button(async () =>
                {
                    var r = await OrdenDelPanel(new Orden { EquiparObjeto = new OrdenEquiparObjeto { Casilla = pieza.CasillaInventario } }, t => Poner(error, t), RepintarSiCambia, b);
                    if (r?.Resultado == Resultado.OrdenAceptada) capaEncima.Q("elegir-equipo")?.RemoveFromHierarchy();
                }) { name = "equipar-" + p.CasillaInventario };
                b.AddToClassList("bienes-casilla");
                b.AddToClassList("equipo-rareza-" + tier);
                if (ImagenDe(p, 34) is VisualElement img) b.Add(img);
                var linea = Dialogo.Caja("equipo-linea");
                linea.style.backgroundColor = InventoryUtils.GetRarityColor(rareza);
                b.Add(linea);
                ConFicha(b, () => pieza);
                rejilla.Add(b);
            }
            if (candidatas.Count == 0) rejilla.Add(Dialogo.Texto("Nada en la mochila para este hueco.", "base-vacio"));
            lista.Add(rejilla);
            if (hueco != "arma" && PuestaEnHueco(h, hueco) != null)
            {
                Button quitar = null;
                quitar = Dialogo.Boton("Quitar", "boton-secundario base-boton-fila elegir-equipo-quitar", async () =>
                {
                    var r = await OrdenDelPanel(new Orden { DesequiparObjeto = new OrdenDesequiparObjeto { Hueco = hueco } }, t => Poner(error, t), RepintarSiCambia, quitar);
                    if (r?.Resultado == Resultado.OrdenAceptada) capaEncima.Q("elegir-equipo")?.RemoveFromHierarchy();
                });
                quitar.name = "quitar-" + hueco;
                lista.Add(quitar);
            }
            lista.Add(error);
            capaEncima.Add(velo);
            // A la izquierda de la casilla, dentro de la pantalla (cuando ya tiene su tamaño).
            lista.style.visibility = Visibility.Hidden;
            lista.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                var raiz = capaEncima.worldBound;
                var sitio = casilla.worldBound;
                var ancho = lista.resolvedStyle.width;
                var alto = lista.resolvedStyle.height;
                if (float.IsNaN(ancho) || float.IsNaN(alto)) return;
                lista.style.left = Mathf.Clamp(sitio.xMin - ancho - 10, raiz.xMin + 8, Mathf.Max(raiz.xMin + 8, raiz.xMax - ancho - 8)) - raiz.xMin;
                lista.style.top = Mathf.Clamp(sitio.yMin, raiz.yMin + 8, Mathf.Max(raiz.yMin + 8, raiz.yMax - alto - 8)) - raiz.yMin;
                lista.style.visibility = Visibility.Visible;
            });
        }

        // ─────────────── usar consumibles ───────────────

        /// <summary>«Usar» un consumible de la mochila (`usarObjeto`): la medalla de tropa, sobre la escuadra que se elija; las demás, sin más.</summary>
        void BotonesDeUso(VisualElement barra, ItemInstancia pieza, Heroe h, Action<string> error)
        {
            var efecto = FichaDelMotor(pieza.ItemDefinitionId)?["efecto"] as JObject;
            if (pieza.Tipo != "consumible" || efecto == null) return;
            DropdownField escuadra = null;
            List<Escuadron> propias = null;
            if (efecto["experienciaTropa"] != null)
            {
                propias = EscuadrasDelHeroe(h).Where(s => !s.HasPrestadaPorCampamentoId).ToList();
                if (propias.Count == 0) return;
                escuadra = new DropdownField(propias.Select(NombreDeEscuadra).ToList(), 0) { name = "usar-en" };
                escuadra.AddToClassList("tropas-orden");
                barra.Add(escuadra);
            }
            Button usar = null;
            usar = Dialogo.Boton("Usar", "boton-secundario base-boton-fila", async () =>
            {
                objetoElegido = "";
                var o = new OrdenUsarObjeto { Casilla = pieza.CasillaInventario };
                if (escuadra != null) o.EscuadronId = propias[escuadra.index].Id;
                await OrdenDelPanel(new Orden { UsarObjeto = o }, error, RepintarSiCambia, usar);
            });
            usar.name = "usar-objeto";
            barra.Add(usar);
        }

        // ─────────────── repartir puntos ───────────────

        /// <summary>«+» y «−» de un atributo mientras haya puntos sin gastar (como `Basic_Stats_Panel` de Conquest): se reparten a medias y se guardan
        /// con «Guardar» (`repartirPuntos`) o se deshacen con «Cancelar».</summary>
        void BotonesDeReparto(VisualElement fila, string atributo, Heroe h)
        {
            var sinGastar = (int)h.PuntosDeAtributoSinGastar - puntosARepartir.Values.Sum();
            puntosARepartir.TryGetValue(atributo, out var puestos);
            var menos = Dialogo.Boton("−", "heroe-reparto", () => { puntosARepartir[atributo] = puestos - 1; PintarBase(); });
            menos.name = "menos-" + atributo;
            menos.SetEnabled(puestos > 0);
            var mas = Dialogo.Boton("+", "heroe-reparto", () => { puntosARepartir[atributo] = puestos + 1; PintarBase(); });
            mas.name = "mas-" + atributo;
            mas.SetEnabled(sinGastar > 0);
            fila.Insert(fila.childCount - 1, menos);
            fila.Add(mas);
        }

        void BotonesDeGuardarReparto(VisualElement v, Heroe h, Action<string> error)
        {
            if (puntosARepartir.Values.Sum() == 0) return;
            var fila = Dialogo.Caja("heroe-reparto-botones");
            Button guardar = null;
            guardar = Dialogo.Boton("Guardar", "boton-primario base-reponer", async () =>
            {
                var a = new OrdenRepartirPuntos.Types.Atributos();
                if (puntosARepartir.TryGetValue("fuerza", out var f) && f > 0) a.Fuerza = f;
                if (puntosARepartir.TryGetValue("destreza", out var d) && d > 0) a.Destreza = d;
                if (puntosARepartir.TryGetValue("armadura", out var ar) && ar > 0) a.Armadura = ar;
                if (puntosARepartir.TryGetValue("vitalidad", out var vi) && vi > 0) a.Vitalidad = vi;
                var r = await OrdenDelPanel(new Orden { RepartirPuntos = new OrdenRepartirPuntos { Atributos = a } }, error, () => { }, guardar);
                if (r?.Resultado == Resultado.OrdenAceptada) { puntosARepartir.Clear(); PintarBase(); }
            });
            guardar.name = "guardar-reparto";
            fila.Add(guardar);
            var cancelar = Dialogo.Boton("Cancelar", "boton-secundario base-boton-fila", () => { puntosARepartir.Clear(); PintarBase(); });
            cancelar.name = "cancelar-reparto";
            fila.Add(cancelar);
            v.Add(fila);
        }
    }
}
