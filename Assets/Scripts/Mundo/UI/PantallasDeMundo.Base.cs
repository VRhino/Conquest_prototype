// La BASE PERSONAL (paso 6 del bloque 4, tanda 1): una ventana a pantalla completa por encima del mapa o del interior, con todo lo del jugador.
// Se abre con el yelmo de la barra o con la tecla B y se cierra con «Cerrar» o Esc. Tres pestañas arriba del todo (Víctor, 2026-11-14):
//  - Héroe: calcada del detalle del héroe de Conquest (`UIHero_Detail`): atributos a la izquierda, el héroe en 3D en el centro sobre el fondo de
//    la ventana (girable) y el equipo puesto a la derecha; lo del lateral de la Base del prototipo (`ui/basePersonal.ts`) va bajo los atributos.
//  - Tropas: filtros y rejilla de tarjetas a la izquierda, la tropa en 3D en el centro y a la derecha sus datos en dos subpestañas,
//    «Características» y «Atributos» (los del panel del barracón de Conquest, `UISquadDetail`).
//  - Bienes: el carro y el almacén personal del prototipo (`ui/panelCarro.ts`).
// Mientras está abierta, la barra de arriba solo enseña la Base y los Avisos. Una acción solo sale donde se puede hacer; los rechazos del motor
// se enseñan con su frase. En la Base no se ordenan las escuadras: el orden se elige al salir al mundo o al entrar en batalla.
using System;
using System.Collections.Generic;
using System.Linq;
using BronzeAge.Contract.V1;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace ConquestTactics.Mundo.UI
{
    public sealed partial class PantallasDeMundo
    {
        /// <summary>Los prefabs visuales de Conquest (`ScriptableObjects/VisualPrefabConfig`): el héroe modular y los soldados de cada tropa, para los
        /// modelos 3D de la Base. Sin él, los recuadros quedan vacíos.</summary>
        public VisualPrefabConfiguration PrefabsVisuales;

        static readonly (string id, string nombre)[] PestanasDeBase = { ("heroe", "Héroe"), ("tropas", "Tropas"), ("bienes", "Bienes") };

        VisualElement baseVentana;
        string pestanaBase = "heroe";
        string escuadraElegida;
        string pagarConBase;
        string firmaBase;
        string subpestanaAlmacen = "objetos", subpestanaLleva = "mochila", objetoElegido = "";
        string filtroTipo = "todas", filtroRareza = "todas", busquedaTropa, ordenTropas = "Orden de combate", subpestanaTropa = "caracteristicas";
        ModeloDeLaBase modeloHeroe, modeloTropa;
        string tropaDelModelo;

        public bool BaseAbierta => baseVentana?.panel != null;

        // ─────────────── la barra del jugador ───────────────

        /// <summary>La barra de arriba como la del prototipo (`ui/barraJugador.ts`): el yelmo de la Base y la campana de Avisos, en iconos redondos,
        /// y lo demás (capas, marcha forzada, cambiar de partida, cerrar sesión) en un menú. Los botones del menú son los mismos de antes.</summary>
        void BarraDelJugador(VisualElement v)
        {
            var barra = v.Q(className: "barra-arriba");
            if (barra == null) return;
            var avisos = barra.Q<Button>("avisos");
            var yelmo = new Button(AlternarBase) { name = "base", tooltip = "Base personal (B)" };
            IconoDeBarra(yelmo, "base");
            barra.Insert(barra.IndexOf(avisos), yelmo);
            avisos.text = "";
            avisos.tooltip = "Avisos";
            IconoDeBarra(avisos, "avisos");
            var cuenta = Dialogo.Texto("", "barra-cuenta oculto");
            cuenta.name = "avisos-cuenta";
            avisos.Add(cuenta);

            var lista = Dialogo.Caja("menu-barra oculto");
            lista.name = "menu-barra";
            foreach (var n in new[] { "capa-nodos", "capa-fertilidad", "marcha-forzada", "cambiar-partida", "cerrar-sesion" })
            {
                var b = barra.Q<Button>(n);
                if (b == null) continue;
                b.RemoveFromClassList("boton-secundario");
                b.AddToClassList("menu-barra-opcion");
                b.clicked += () => Ver(lista, false);
                lista.Add(b);
            }
            if (lista.childCount == 0) return;
            var menu = new Button(() => lista.ToggleInClassList("oculto")) { name = "menu", tooltip = "Menú" };
            IconoDeBarra(menu, "menu");
            barra.Add(menu);
            v.Add(lista);
        }

        static void IconoDeBarra(Button b, string icono)
        {
            b.RemoveFromClassList("boton-secundario");
            b.AddToClassList("barra-icono");
            var dibujo = IconosDelMundo.Icono("barra", icono, 20, IconosDelMundo.OroClaro);
            if (dibujo != null) b.Add(dibujo);
        }

        /// <summary>Los avisos sin leer, en la pastilla de la campana.</summary>
        void PintarAvisos(Button b)
        {
            var n = c.Avisos.NoLeidos;
            var cuenta = b.Q<Label>("avisos-cuenta");
            if (cuenta == null) { b.text = n > 0 ? $"Avisos ({n})" : "Avisos"; return; }
            cuenta.text = n > 99 ? "99+" : n.ToString();
            Ver(cuenta, n > 0);
        }

        /// <summary>La tecla B abre o cierra la Base donde está la barra (mapa o interior), salvo escribiendo en un campo o con otro panel abierto.</summary>
        void TeclaDeLaBase()
        {
            var teclado = UnityEngine.InputSystem.Keyboard.current;
            if (teclado == null) return;
            if (teclado.escapeKey.wasPressedThisFrame && BaseAbierta) { CerrarBase(); return; }
            if (!teclado.bKey.wasPressedThisFrame || pantalla.Q<Button>("base") == null) return;
            if (pantalla.panel?.focusController?.focusedElement is TextField || pantalla.panel?.focusController?.focusedElement is IntegerField) return;
            AlternarBase();
        }

        void AlternarBase()
        {
            if (BaseAbierta) CerrarBase();
            else AbrirBase();
        }

        // ─────────────── la ventana ───────────────

        void AbrirBase()
        {
            var interior = SelectorDePersonaje.Interior;
            if (BaseAbierta || HeroeAhora() == null || (interior != null && interior.PanelAbierto)) return;
            var pantallaMontada = pantalla.childCount > 0 ? pantalla[0] : null;
            if (pantallaMontada == null) return;
            if (interior != null) interior.PanelAbierto = true;
            if (mapa != null) mapa.EntradaApagada = true;
            var barra = pantallaMontada.Q(className: "barra-arriba");
            barra?.AddToClassList("barra--base");
            barra?.Q<Button>("base")?.AddToClassList("activo");
            Ver(pantallaMontada.Q("menu-barra"), false);

            baseVentana = Dialogo.Caja("base-ventana");
            baseVentana.name = "base-personal";
            // Arriba del todo, las pestañas centradas (la activa subrayada en oro) y «Cerrar» a la derecha.
            var cab = Dialogo.Caja("base-cabecera");
            var nav = Dialogo.Caja("base-pestanas");
            nav.name = "base-pestanas";
            cab.Add(nav);
            var cerrar = Dialogo.Boton("Cerrar", "boton-secundario base-cerrar", CerrarBase);
            cerrar.name = "base-cerrar";
            cab.Add(cerrar);
            baseVentana.Add(cab);
            var cuerpo = Dialogo.Caja("base-cuerpo");
            cuerpo.name = "base-cuerpo";
            baseVentana.Add(cuerpo);
            // Debajo de la barra (que sigue con la Base y los Avisos) y encima de todo lo demás de la pantalla.
            pantallaMontada.Add(baseVentana);

            firmaBase = null;
            PintarBase();
            baseVentana.schedule.Execute(() => { if (!RatonPulsado() && FirmaDeLaBase() != firmaBase) PintarBase(); }).Every(250);
        }

        void CerrarBase()
        {
            if (!BaseAbierta) return;
            var pantallaMontada = baseVentana.parent;
            baseVentana.RemoveFromHierarchy();
            baseVentana = null;
            QuitarModelos();
            var barra = pantallaMontada?.Q(className: "barra-arriba");
            barra?.RemoveFromClassList("barra--base");
            barra?.Q<Button>("base")?.RemoveFromClassList("activo");
            if (SelectorDePersonaje.Interior != null) SelectorDePersonaje.Interior.PanelAbierto = false;
            if (mapa != null) mapa.EntradaApagada = false;
        }

        void QuitarModelos()
        {
            modeloHeroe?.Quitar();
            modeloHeroe = null;
            modeloTropa?.Quitar();
            modeloTropa = null;
            tropaDelModelo = null;
        }

        /// <summary>Lo que, si cambia, repinta la Base: el héroe, sus escuadras, su columna y su campamento (sus versiones), y lo elegido en ella.</summary>
        string FirmaDeLaBase()
        {
            var h = HeroeAhora();
            if (h == null) return "";
            var a = c.Sesion.Almacen;
            var escuadras = string.Join(",", h.EscuadronesIds.Select(id => a.Buscar(TipoEntidad.Escuadron, id)?.Escuadron?.Version ?? 0));
            var col = ColumnaDelHeroe(h);
            var camp = CampamentoDeResidencia(h);
            return $"{h.Version}|{escuadras}|{col?.Version}|{camp?.Version}|{pestanaBase}|{escuadraElegida}|{pagarConBase}|{filtroTipo}|{filtroRareza}|{busquedaTropa}|{ordenTropas}|{subpestanaTropa}|{subpestanaAlmacen}|{subpestanaLleva}|{objetoElegido}";
        }

        /// <summary>Tras una orden: solo si algo cambió (repintar sin motivo se llevaría el clic que esté a medias).</summary>
        void RepintarSiCambia()
        {
            if (BaseAbierta && FirmaDeLaBase() != firmaBase) PintarBase();
        }

        /// <summary>Con el botón del ratón pulsado no se repinta solo: el clic a medias caería en un botón que ya no está.</summary>
        static bool RatonPulsado() => UnityEngine.InputSystem.Mouse.current?.leftButton.isPressed == true;

        void PintarBase()
        {
            if (!BaseAbierta) return;
            // Con un número o una búsqueda a medio escribir no se repinta sola: se llevaría lo escrito.
            var foco = baseVentana.panel?.focusController?.focusedElement;
            if (firmaBase != null && (foco is IntegerField || foco is TextField)) return;
            firmaBase = FirmaDeLaBase();
            var h = HeroeAhora();
            if (h == null) return;
            var nav = baseVentana.Q("base-pestanas");
            nav.Clear();
            var mermadas = EscuadrasDelHeroe(h).Count(s => s.Cantidad < TopeDe(s));
            foreach (var (id, nombre) in PestanasDeBase)
            {
                var p = id;
                var b = Dialogo.Boton("", "base-pestana", () => { pestanaBase = p; PintarBase(); });
                b.Add(Dialogo.Texto(nombre, "base-pestana-texto"));
                b.name = "pestana-" + id;
                b.EnableInClassList("activo", id == pestanaBase);
                if (id == "tropas" && mermadas > 0) { var aviso = Dialogo.Texto(mermadas.ToString(), "base-pestana-aviso"); aviso.tooltip = "Escuadras por debajo de su tope"; b.Add(aviso); }
                if (id == "heroe" && h.PuntosDeAtributoSinGastar > 0) { var aviso = Dialogo.Texto(h.PuntosDeAtributoSinGastar.ToString(), "base-pestana-aviso"); aviso.tooltip = "Puntos sin gastar"; b.Add(aviso); }
                nav.Add(b);
            }
            var cuerpo = baseVentana.Q("base-cuerpo");
            // Los modelos 3D se quedan entre repintados de su pestaña (se están girando); al cambiar de pestaña se quitan.
            if (pestanaBase != "heroe") { modeloHeroe?.Quitar(); modeloHeroe = null; }
            if (pestanaBase != "tropas") { modeloTropa?.Quitar(); modeloTropa = null; tropaDelModelo = null; }
            var foto = cuerpo.Q(pestanaBase == "heroe" ? "heroe-modelo" : "tropa-modelo");
            foto?.RemoveFromHierarchy();
            cuerpo.Clear();
            var error = Dialogo.Texto("", "error oculto");
            error.name = "error-base";
            void Error(string t) => Poner(error, t);
            if (pestanaBase == "bienes") PintarBienes(cuerpo, h, Error, error);
            else if (pestanaBase == "tropas") PintarTropas(cuerpo, h, Error, foto, error);
            else PintarHeroe(cuerpo, h, foto);
        }

        // ─────────────── Héroe ───────────────

        /// <summary>Héroe, calcado del detalle del héroe de Conquest (`UIHero_Detail`): a la izquierda el nombre, el nivel con su barra y los
        /// atributos con sus iconos (Liderazgo, Fuerza, Destreza, Armadura y Dureza, que es la vitalidad del motor) y los puntos; en el centro el
        /// héroe en 3D sobre el fondo de la ventana, girable; a la derecha las casillas del equipo puesto. Lo del lateral del prototipo (clase, dónde
        /// está, herido, monedas, víveres y residencia) va bajo los atributos. Sin «Guardar», «Cancelar», «More», «Free Reset» ni «Repair»: repartir
        /// puntos y el detalle del equipo son de la tanda 2.</summary>
        void PintarHeroe(VisualElement v, Heroe h, VisualElement foto)
        {
            var izquierda = Dialogo.Caja("heroe-col heroe-col--izquierda");
            izquierda.name = "heroe-izquierda";
            v.Add(izquierda);
            foto ??= ModeloEn("heroe-modelo", () => PrefabsVisuales?.GetDefaultHeroPrefab(), "hero_preview",
                instancia => VestidoDelMotor.Vestir(instancia, HeroeParaVer(c.Datos)), m => modeloHeroe = m, () => modeloHeroe);
            v.Add(foto);
            var derecha = Dialogo.Caja("heroe-col heroe-col--derecha");
            derecha.name = "heroe-derecha";
            v.Add(derecha);

            // El estado: nombre y nivel con su barra (`Hero_Status_Panel` y `Experience_Bar`).
            var estado = Dialogo.Caja("heroe-estado");
            estado.Add(Dialogo.Texto(Textos.NombreDeHeroe(c.Sesion.Almacen, h.Id), "base-nombre"));
            var clase = ClaseDeHeroe.Leer(c.Datos).FirstOrDefault(k => k.Id == h.ClassDefinitionId);
            var textoClase = Dialogo.Texto(clase?.Nombre ?? h.ClassDefinitionId.Replace('_', ' '), "base-donde base-clase");
            textoClase.tooltip = "La clase es la del arma que lleva puesta";
            estado.Add(textoClase);
            var pide = ExperienciaParaSubir(h.Nivel);
            var exp = Dialogo.Caja("base-experiencia");
            exp.name = "heroe-experiencia";
            var lineaNivel = Dialogo.Caja("base-dato");
            lineaNivel.Add(Dialogo.Texto($"Nv. {h.Nivel}", "base-dato-valor"));
            var lleva = pide == null ? 0 : Math.Min(pide.Value, Math.Max(0, pide.Value - h.ExperienciaHaciaSiguienteNivel / 1000.0));
            lineaNivel.Add(Dialogo.Texto(pide != null ? $"{Math.Floor(lleva)} / {pide} hacia el {h.Nivel + 1}" : $"faltan {Math.Ceiling(h.ExperienciaHaciaSiguienteNivel / 1000.0)}", "base-dato-etiqueta"));
            exp.Add(lineaNivel);
            if (pide != null) exp.Add(Barra(lleva / pide.Value, "base-barra"));
            estado.Add(exp);
            izquierda.Add(estado);

            // Los atributos (`Basic_Stats_Panel`): icono, nombre y valor, con los del motor (la base de su clase más los puntos repartidos).
            var atributos = Dialogo.Caja("heroe-atributos");
            atributos.name = "heroe-atributos";
            var a = h.AtributosBase ?? new Atributos();
            foreach (var (icono, nombre, valor, ayuda) in new[]
            {
                ("liderazgo", "Liderazgo", h.LiderazgoBase, "Lo que suman las escuadras que puede llevar"),
                ("fuerza", "Fuerza", a.Fuerza, null),
                ("destreza", "Destreza", a.Destreza, null),
                ("armadura", "Armadura", a.Armadura, null),
                ("dureza", "Dureza", a.Vitalidad, "La vitalidad del motor"),
            })
            {
                var fila = Dialogo.Caja("heroe-atributo");
                fila.name = "atributo-" + icono;
                if (ayuda != null) fila.tooltip = ayuda;
                fila.Add(Dialogo.Caja("heroe-atributo-icono heroe-icono-" + icono));
                fila.Add(Dialogo.Texto(nombre, "heroe-atributo-nombre"));
                fila.Add(Dialogo.Texto(valor.ToString(), "heroe-atributo-valor"));
                atributos.Add(fila);
            }
            if (h.PuntosDeAtributoSinGastar > 0)
                atributos.Add(Dialogo.Texto($"Puntos disponibles: {h.PuntosDeAtributoSinGastar}", "heroe-puntos"));
            izquierda.Add(atributos);

            // Lo del lateral del prototipo, debajo.
            var mas = Dialogo.Caja("heroe-mas");
            mas.Add(Dialogo.Texto(DondeEstaElHeroe(h) + (h.HasFaccionId && NombreDeFaccion(c.Sesion.Almacen, h.FaccionId) is string f ? " · " + f : ""), "base-donde heroe-donde"));
            var herido = MinutosHerido(h);
            if (herido != null) mas.Add(Dialogo.Texto($"Herido: {herido} min", "base-herido"));
            var monedas = Dialogo.Caja("base-monedas");
            monedas.tooltip = "Tus monedas: las ganas combatiendo (también contra bandidos) y pagan contratos de mercenario. Aparte del oro de las plazas: 100 monedas valen 1 de oro.";
            monedas.Add(IconosDelMundo.ConIcono("glifo", "monedas", Dialogo.Texto("Monedas", "base-monedas-etiqueta"), 16));
            monedas.Add(Dialogo.Texto(h.Monedas.ToString("#,0", new System.Globalization.CultureInfo("es-ES")), "base-monedas-cifra"));
            mas.Add(monedas);
            PintarViveresDelLateral(mas, h);
            var reside = Dialogo.Caja("base-residencia");
            reside.Add(ConAyuda(Dialogo.Texto("RESIDES EN", "base-lateral-titulo"),
                "La residencia se cambia o se deja desde dentro de la plaza o del campamento: en la plaza, en el blasón; en el campamento, en su Resumen."));
            if (h.HasResidenciaId)
            {
                var esCamp = h.ResidenciaId.StartsWith("camp-");
                var nombre = esCamp ? "Campamento " + NombreDeCampamento(h.ResidenciaId) : NombreDePlaza(h.ResidenciaId);
                reside.Add(IconosDelMundo.ConIcono("glifo", esCamp ? "campamento" : "casa", Dialogo.Texto(nombre, "base-residencia-nombre"), 18));
                // «Ver en el mapa»: solo en el mapa (desde un interior no se ve). El mapa sigue a la columna: se elige la residencia y sale su ficha.
                var cosa = mapa?.Cosas.FirstOrDefault(x => x.Id == h.ResidenciaId);
                if (cosa != null)
                {
                    var ver = Dialogo.Boton("Ver en el mapa", "boton-secundario base-boton-chico", () => { CerrarBase(); mapa?.Seleccionar(cosa); });
                    ver.name = "ver-residencia";
                    reside.Add(ver);
                }
            }
            else reside.Add(Dialogo.Texto("En ningún sitio", "base-residencia-nombre"));
            mas.Add(reside);
            izquierda.Add(mas);

            // El equipo puesto (`EquipmentPanel`): las seis casillas de Conquest en dos columnas y la montura del motor debajo.
            derecha.Add(Dialogo.Texto("EQUIPO", "base-kicker heroe-equipo-titulo"));
            var equipo = Dialogo.Caja("heroe-equipo");
            equipo.name = "heroe-equipo";
            var e = h.Equipamiento ?? new Equipamiento();
            foreach (var (hueco, nombre, pieza) in new[]
            {
                ("arma", "Arma", e.Arma), ("casco", "Casco", e.Casco), ("torso", "Torso", e.Torso), ("guantes", "Guantes", e.Guantes),
                ("pantalones", "Pantalones", e.Pantalones), ("botas", "Botas", e.Botas), ("montura", "Montura", e.Montura),
            })
                equipo.Add(CasillaDeEquipo(hueco, nombre, pieza));
            derecha.Add(equipo);
        }

        /// <summary>Una casilla del equipo (`Inventory_Item_Cell`): el fondo de su rareza y la línea de su color al pie, con una imagen provisional
        /// del objeto (la de su tipo en el prototipo) y su nombre; vacía, el nombre del hueco. La rareza sale del `tier` del objeto en el motor
        /// (1 común … 5 legendario): el motor no tiene rareza propia.</summary>
        VisualElement CasillaDeEquipo(string hueco, string nombreHueco, ItemInstancia pieza)
        {
            var caja = Dialogo.Caja("equipo-casilla");
            caja.name = "equipo-" + hueco;
            var cuadro = Dialogo.Caja("equipo-cuadro");
            caja.Add(cuadro);
            if (pieza == null)
            {
                cuadro.AddToClassList("equipo-cuadro--vacio");
                cuadro.Add(Dialogo.Texto(nombreHueco, "equipo-vacio"));
                caja.tooltip = $"{nombreHueco}: nada puesto";
                return caja;
            }
            var ficha = (c.Datos.Bloque("Objetos_Del_Heroe", "OBJETOS")?["objetos"] as JArray)?.OfType<JObject>().FirstOrDefault(x => (string)x["id"] == pieza.ItemDefinitionId);
            var tier = Mathf.Clamp((int?)ficha?["tier"] ?? 1, 1, 5);
            var rareza = new[] { ItemRarity.Common, ItemRarity.Uncommon, ItemRarity.Rare, ItemRarity.Epic, ItemRarity.Legendary }[tier - 1];
            cuadro.AddToClassList("equipo-rareza-" + tier);
            var recurso = hueco == "arma" ? "armaBronce" : hueco == "montura" ? "livestock" : "armaduraBronce";
            var imagen = IconosDelMundo.Icono("recurso", recurso, 44);
            if (imagen != null) cuadro.Add(imagen);
            var linea = Dialogo.Caja("equipo-linea");
            linea.style.backgroundColor = InventoryUtils.GetRarityColor(rareza);
            cuadro.Add(linea);
            var nombre = (string)ficha?["nombre"] ?? pieza.ItemDefinitionId;
            caja.Add(Dialogo.Texto(nombre, "equipo-nombre"));
            var stats = string.Join(", ", pieza.Estadisticas.Select(x => $"{x.Nombre} {x.Valor}"));
            caja.tooltip = $"{nombreHueco}: {nombre} · {rareza} (tier {tier}){(stats.Length > 0 ? " · " + stats : "")}";
            return caja;
        }

        static VisualElement Dato(string etiqueta, string valor)
        {
            var f = Dialogo.Caja("base-dato");
            f.Add(Dialogo.Texto(etiqueta, "base-dato-etiqueta"));
            f.Add(Dialogo.Texto(valor, "base-dato-valor"));
            return f;
        }

        /// <summary>Una barra de progreso (`.obra-barra`): `parte` de 0 a 1.</summary>
        static VisualElement Barra(double parte, string clases)
        {
            var b = Dialogo.Caja("base-barra-fondo " + clases);
            var relleno = Dialogo.Caja("base-barra-relleno");
            relleno.style.width = Length.Percent((float)(Math.Max(0, Math.Min(1, parte)) * 100));
            b.Add(relleno);
            return b;
        }

        /// <summary>Los víveres: lo que llevas, lo que cabe (con columna) y, en columna personal, cuánto aguantan al paso de ahora (`htmlViveres`).</summary>
        void PintarViveresDelLateral(VisualElement pie, Heroe h)
        {
            var ahora = c.Sesion.AhoraMundo;
            var col = ColumnaDelHeroe(h);
            var viveres = Math.Floor(Instante.Valor(h.Viveres, ahora) / 1000);
            var etiqueta = IconosDelMundo.ConIcono("glifo", "viveres", Dialogo.Texto("Víveres", "base-dato-etiqueta"), 16);
            var fila = Dialogo.Caja("base-dato");
            fila.Add(etiqueta);
            pie.Add(fila);
            if (col == null) { fila.Add(Dialogo.Texto(viveres.ToString(), "base-dato-valor")); return; }
            var capacidad = c.Datos.Numero("documentos.Movimiento_Ejercitos.LOGISTICA.capacidadViveresPorHeroe") * Math.Max(1, col.Participantes.Count);
            fila.Add(Dialogo.Texto($"{viveres} / {capacidad:0}", "base-dato-valor"));
            // En un ejército los víveres son de todos: ahí no se estima. El ritmo lo da el motor (`por_minuto` de los víveres, en milésimas).
            if (col.Participantes.Count != 1 || h.Viveres == null || h.Viveres.EnEscalones) return;
            var porMinuto = -h.Viveres.PorMinuto / 1000.0;
            if (porMinuto <= 0) return;
            var min = viveres / porMinuto;
            var dura = Dialogo.Texto($"aguantan {(min >= 120 ? $"{Math.Floor(min / 60)} h" : $"{Math.Floor(min)} min")}", "base-dura");
            dura.tooltip = $"Al paso de ahora ({(col.Estado == EstadoColumna.ColumnaEstacionada ? "acampada" : col.MarchaForzada ? "en marcha forzada" : "en marcha")}): {porMinuto:0.00} de trigo por minuto";
            pie.Add(dura);
        }

        /// <summary>Lo que pide pasar del nivel `nivel` al siguiente (`militar::heroe::experiencia_para_subir`, bloque `PROGRESION_HEROE`); null sin datos.</summary>
        double? ExperienciaParaSubir(uint nivel)
        {
            var p = c.Datos.Bloque("Heroe_Y_Tropa", "PROGRESION_HEROE");
            var bas = (double?)p?["experienciaBase"];
            if (bas == null) return null;
            var tope = (uint?)p["nivelDeTope"] ?? uint.MaxValue;
            var tramos = (p["incrementos"] as JArray)?.Select(t => ((uint)t["desdeNivel"], (double)t["suma"])).ToList() ?? new List<(uint, double)>();
            var pide = bas.Value;
            for (uint n = 2; n <= Math.Min(nivel, tope); n++)
                pide += tramos.Where(t => t.Item1 <= n).Select(t => t.Item2).LastOrDefault();
            return pide;
        }

        int? MinutosHerido(Heroe h)
        {
            if (!h.HasHeridoHastaMs) return null;
            var falta = h.HeridoHastaMs - c.Sesion.AhoraMundo;
            return falta > 0 ? (int)Math.Ceiling(falta / 60_000.0) : (int?)null;
        }

        /// <summary>Dónde está el héroe, en palabras (`dondeEsta` del prototipo).</summary>
        string DondeEstaElHeroe(Heroe h)
        {
            var u = h.Ubicacion;
            switch (u?.DondeCase)
            {
                case UbicacionHeroe.DondeOneofCase.EjercitoId: return "en su columna";
                case UbicacionHeroe.DondeOneofCase.CampamentoId: return "en el campamento " + NombreDeCampamento(u.CampamentoId);
                case UbicacionHeroe.DondeOneofCase.PuebloId: return "dentro de un pueblo";
                case UbicacionHeroe.DondeOneofCase.AvanzadaId: return "dentro de una avanzada";
                case UbicacionHeroe.DondeOneofCase.AsentamientoId: return "en " + NombreDePlaza(u.AsentamientoId);
                default: return "desconectado";
            }
        }

        string NombreDePlaza(string id)
        {
            var p = c.Sesion.Almacen.Buscar(TipoEntidad.Asentamiento, id)?.Asentamiento;
            return p != null && p.HasNombre ? p.Nombre : id;
        }

        /// <summary>Un modelo 3D en un recuadro, sobre el fondo de la ventana (textura con transparencia), de frente y girable arrastrando:
        /// el héroe (vestido) o un soldado de la tropa. Sin prefab, el recuadro vacío.</summary>
        VisualElement ModeloEn(string nombre, Func<GameObject> prefab, string capa, Action<Transform> preparar,
            Action<ModeloDeLaBase> guardar, Func<ModeloDeLaBase> actual, float distancia = 4.2f)
        {
            var foto = Dialogo.Caja("base-modelo");
            foto.name = nombre;
            var p = prefab();
            var modelo = p != null ? ModeloDeLaBase.Crear(p, capa, preparar, distancia) : null;
            guardar(modelo);
            if (modelo == null) return foto;
            foto.AddToClassList("base-modelo--3d");
            foto.style.backgroundImage = Background.FromRenderTexture(modelo.Imagen);
            foto.tooltip = "Arrastra para girarlo";
            float? desde = null;
            foto.RegisterCallback<PointerDownEvent>(e => { desde = e.position.x; foto.CapturePointer(e.pointerId); });
            foto.RegisterCallback<PointerMoveEvent>(e => { if (desde == null) return; actual()?.Girar(e.position.x - desde.Value); desde = e.position.x; });
            foto.RegisterCallback<PointerUpEvent>(e => { desde = null; foto.ReleasePointer(e.pointerId); });
            return foto;
        }

        // ─────────────── Tropas ───────────────

        /// <summary>El tope de hombres de la tropa de una escuadra (`unidadesPorDefecto`).</summary>
        uint TopeDe(Escuadron s) =>
            (uint?)c.Datos.TropasReclutables?.FirstOrDefault(x => (string)x["id"] == s.TropaId)?["unidadesPorDefecto"] ?? s.Cantidad;

        /// <summary>El campamento de mercenarios donde reside el héroe, si reside en uno.</summary>
        CampamentoMercenarios CampamentoDeResidencia(Heroe h) =>
            h.HasResidenciaId && h.ResidenciaId.StartsWith("camp-") ? CampamentoActual(h.ResidenciaId) : null;

        /// <summary>¿Está esta columna a la puerta de ese punto (`radioPuerta`)?</summary>
        bool ALaPuerta(Ejercito col, Punto puerta)
        {
            if (col == null || puerta == null) return false;
            var p = Instante.Posicion(col.Plan, col.Posicion, c.Sesion.AhoraMundo);
            var dx = p.x / 1000 - puerta.X / 1000.0;
            var dy = p.y / 1000 - puerta.Y / 1000.0;
            return Math.Sqrt(dx * dx + dy * dy) <= c.Datos.RadioPuerta;
        }

        /// <summary>El campamento donde resides si estás en él (dentro o con tu columna a su puerta), como `esta_en_el_campamento` del motor.</summary>
        CampamentoMercenarios CampamentoAMano(Heroe h)
        {
            var camp = CampamentoDeResidencia(h);
            if (camp == null || h.Ubicacion == null) return null;
            if (h.Ubicacion.DondeCase == UbicacionHeroe.DondeOneofCase.CampamentoId) return h.Ubicacion.CampamentoId == camp.Id ? camp : null;
            if (h.Ubicacion.DondeCase == UbicacionHeroe.DondeOneofCase.EjercitoId) return ALaPuerta(ColumnaDelHeroe(h), camp.Posicion) ? camp : null;
            return null;
        }

        /// <summary>¿Está la escuadra aquí, en el campamento a mano (en él, o en tu columna a su puerta)?</summary>
        bool EscuadraAMano(Escuadron s, Heroe h, CampamentoMercenarios camp) =>
            s.Contenedor?.DondeCase == ContenedorEscuadron.DondeOneofCase.Campamento
            || (s.Contenedor?.DondeCase == ContenedorEscuadron.DondeOneofCase.EjercitoId && ALaPuerta(ColumnaDelHeroe(h), camp.Posicion));

        /// <summary>Cómo se repone una escuadra desde donde está el héroe (`comoReponer`): prestada (gratis, en su campamento) o la tuya en tu campamento,
        /// con oro. En una plaza se repone en el paso 7. null si aquí no se puede.</summary>
        (bool prestada, uint faltan, int oro, string motivo)? ComoReponer(Escuadron s, Heroe h)
        {
            var faltan = TopeDe(s) > s.Cantidad ? TopeDe(s) - s.Cantidad : 0;
            if (faltan == 0) return null;
            var camp = CampamentoAMano(h);
            if (camp == null || !EscuadraAMano(s, h, camp)) return null;
            if (s.HasPrestadaPorCampamentoId) return s.PrestadaPorCampamentoId == camp.Id ? (true, faltan, 0, null) : ((bool, uint, int, string)?)null;
            var o = camp.Reclutables.FirstOrDefault(x => x.TropaId == s.TropaId && x.Desbloqueada);
            if (o == null) return null;
            var oro = (int)Math.Ceiling(o.PrecioPorSoldado / 1000.0 * faltan);
            var fuente = FuenteDePago(h, camp);
            var poblacion = Math.Floor(Instante.Valor(camp.Poblacion, c.Sesion.AhoraMundo) / 1000);
            var motivo = fuente == null ? "No tienes con qué pagar aquí."
                : poblacion < faltan ? $"Faltan reclutas: hay {poblacion} y hacen falta {faltan}."
                : oro > fuente.Value.oro ? $"No te llega el oro: {oro} y tienes {Math.Floor(fuente.Value.oro)} en {fuente.Value.nombre}." : null;
            return (false, faltan, oro, motivo);
        }

        /// <summary>Con qué se puede pagar en tu campamento (`fuentesDePago`): con columna, su carro (y tu almacén); dentro, tu almacén y el carro que trajiste.</summary>
        List<(string pagarCon, string nombre, double oro)> FuentesDePago(Heroe h, CampamentoMercenarios camp)
        {
            var almacen = ("almacenPersonal", "tu almacén personal", Milesimas(h.AlmacenPersonal, "oro"));
            if (h.Ubicacion?.DondeCase == UbicacionHeroe.DondeOneofCase.EjercitoId && ColumnaDelHeroe(h) is Ejercito col)
                return new List<(string, string, double)> { ("carro", "el carro de tu columna", Milesimas(col.Suministro, "oro")), almacen };
            if (h.Ubicacion?.DondeCase == UbicacionHeroe.DondeOneofCase.CampamentoId && h.Ubicacion.CampamentoId == camp.Id)
                return new List<(string, string, double)> { almacen, ("carro", "tu carro", Milesimas(h.FueraCarro, "oro")) };
            return new List<(string, string, double)>();
        }

        (string pagarCon, string nombre, double oro)? FuenteDePago(Heroe h, CampamentoMercenarios camp)
        {
            var f = FuentesDePago(h, camp);
            if (f.Count == 0) return null;
            return f.FirstOrDefault(x => x.pagarCon == pagarConBase) is var elegida && elegida.pagarCon != null ? elegida : f[0];
        }

        /// <summary>Dónde está una escuadra, en palabras (`dondeEstaLaEscuadra`).</summary>
        string DondeEstaLaEscuadra(Escuadron s, Heroe h)
        {
            var co = s.Contenedor;
            switch (co?.DondeCase)
            {
                case ContenedorEscuadron.DondeOneofCase.Campamento:
                    if (!h.HasResidenciaId) return "En tu campamento";
                    return h.ResidenciaId.StartsWith("camp-") ? "En tu campamento de mercenarios " + NombreDeCampamento(h.ResidenciaId) : "En tu campamento de " + NombreDePlaza(h.ResidenciaId);
                case ContenedorEscuadron.DondeOneofCase.EjercitoId: return ColumnaDelHeroe(h)?.Id == co.EjercitoId ? "En tu columna" : "En una columna";
                case ContenedorEscuadron.DondeOneofCase.CaravanaId: return "Escoltando una caravana";
                case ContenedorEscuadron.DondeOneofCase.PuebloId: return "De guarnición en un pueblo";
                case ContenedorEscuadron.DondeOneofCase.AsentamientoId: return "De guarnición en " + NombreDePlaza(co.AsentamientoId);
                case ContenedorEscuadron.DondeOneofCase.AvanzadaId: return "De guarnición en una avanzada";
                default: return "Fuera del mundo";
            }
        }

        /// <summary>Las familias de formación que puede elegir (`formacionesPermitidas`): las que no piden tecnología y, con Facción, las de sus
        /// tecnologías adoptadas. La de reposo, la primera.</summary>
        List<(string id, string nombre)> FormacionesPermitidas(Heroe h)
        {
            var familias = c.Datos.Bloque("Heroe_Y_Tropa", "FORMACIONES")?["familias"] as JArray;
            if (familias == null) return new List<(string, string)>();
            var adoptadas = h.HasFaccionId ? c.Sesion.Almacen.Todas(Entidad.CuerpoOneofCase.Tecnologias).Select(e => e.Tecnologias)
                .FirstOrDefault(t => t.FaccionId == h.FaccionId)?.Adoptadas.ToList() : null;
            return familias.Where(f => f["tecnologia"] == null || f["tecnologia"].Type == JTokenType.Null || (adoptadas?.Contains((string)f["tecnologia"]) ?? false))
                .Select(f => ((string)f["id"], (string)f["nombre"])).ToList();
        }

        string NombreDeFormacion(string id)
        {
            var familias = c.Datos.Bloque("Heroe_Y_Tropa", "FORMACIONES")?["familias"] as JArray;
            var f = familias?.FirstOrDefault(x => (string)x["id"] == (string.IsNullOrEmpty(id) ? (string)familias.FirstOrDefault()?["id"] : id));
            return (string)f?["nombre"] ?? (string.IsNullOrEmpty(id) ? "En reposo" : id);
        }

        /// <summary>Tropas: a la izquierda los filtros (tipo y rareza) y la rejilla de tarjetas de tropa con el buscador, el orden y «Reponer las
        /// prestadas»; en el centro la tropa elegida en 3D sobre el fondo de la ventana; a la derecha sus datos en dos subpestañas, «Características»
        /// (cifras, formaciones, órdenes, dónde está, «Reponer» y «Pagar con») y «Atributos» (los del panel del barracón de Conquest).
        /// Sin ordenar: el orden se elige al salir al mundo o al entrar en batalla.</summary>
        void PintarTropas(VisualElement v, Heroe h, Action<string> error, VisualElement foto, Label lineaError)
        {
            var todas = EscuadrasDelHeroe(h);
            var reponer = todas.ToDictionary(s => s.Id, s => ComoReponer(s, h));

            // Los filtros (`Barracks`: tipo arriba, rareza abajo), solo con datos de Conquest (`SquadData.unitType` y su rareza).
            var filtros = Dialogo.Caja("tropas-filtros");
            filtros.name = "tropas-filtros";
            foreach (var (id, icono, titulo) in new[] { ("todas", "_otro", "Todas"), ("Infantry", "lanceros_mimbre", "Infantería"), ("Distance", "arqueros", "A distancia"), ("Cavalry", "jinetes_asirios", "Caballería") })
            {
                var t = id;
                var b = new Button(() => { filtroTipo = t; PintarBase(); }) { name = "filtro-" + id, tooltip = titulo };
                b.AddToClassList("tropas-filtro");
                b.EnableInClassList("activo", filtroTipo == id);
                var dibujo = IconosDelMundo.Icono("tropa", icono, 26, IconosDelMundo.OroClaro);
                if (dibujo != null) b.Add(dibujo);
                filtros.Add(b);
            }
            filtros.Add(Dialogo.Caja("tropas-filtros-hueco"));
            foreach (var (color, titulo) in new[] { (Color.clear, "Todas las rarezas") }.Concat(ColoresDeRareza().Select(c0 => (c0, "Rareza"))))
            {
                var clave = color == Color.clear ? "todas" : ColorUtility.ToHtmlStringRGB(color);
                var b = new Button(() => { filtroRareza = clave; PintarBase(); }) { name = "rareza-" + clave, tooltip = titulo };
                b.AddToClassList("tropas-rareza");
                b.EnableInClassList("activo", filtroRareza == clave);
                if (color != Color.clear) b.style.backgroundColor = color;
                filtros.Add(b);
            }
            v.Add(filtros);

            // La rejilla.
            var panel = Dialogo.Caja("tropas-panel");
            panel.name = "tropas-panel";
            var buscar = new TextField { name = "buscar-tropa", value = busquedaTropa ?? "" };
            buscar.AddToClassList("entrada");
            buscar.AddToClassList("tropas-buscar");
            buscar.textEdition.placeholder = "Buscar";
            buscar.RegisterCallback<KeyDownEvent>(e => { if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) { busquedaTropa = buscar.value; baseVentana.Focus(); PintarBase(); } });
            buscar.RegisterCallback<FocusOutEvent>(_ => { if (buscar.value != (busquedaTropa ?? "")) { busquedaTropa = buscar.value; PintarBase(); } });
            panel.Add(buscar);
            var vistas = todas.Where(s =>
            {
                var d = PiezasDeTropa.Datos(s.TropaId);
                if (filtroTipo != "todas" && d?.unitType.ToString() != filtroTipo) return false;
                if (filtroRareza != "todas" && (d == null || ColorUtility.ToHtmlStringRGB(PiezasDeTropa.ColorDeRareza(d)) != filtroRareza)) return false;
                return string.IsNullOrWhiteSpace(busquedaTropa) || NombreDeEscuadra(s).IndexOf(busquedaTropa.Trim(), StringComparison.CurrentCultureIgnoreCase) >= 0;
            }).ToList();
            vistas = ordenTropas switch
            {
                "Liderazgo" => vistas.OrderByDescending(s => LiderazgoDe(s.TropaId) ?? 0).ToList(),
                "Rareza" => vistas.OrderByDescending(s => (int)(PiezasDeTropa.Datos(s.TropaId)?.rarity ?? 0)).ToList(),
                "Nombre" => vistas.OrderBy(s => NombreDeEscuadra(s)).ToList(),
                _ => vistas,
            };
            if (escuadraElegida == null || todas.All(s => s.Id != escuadraElegida)) escuadraElegida = todas.FirstOrDefault()?.Id;
            var rejilla = new ScrollView(ScrollViewMode.Vertical) { name = "tropas-rejilla", horizontalScrollerVisibility = ScrollerVisibility.Hidden, verticalScrollerVisibility = ScrollerVisibility.Auto };
            rejilla.AddToClassList("tropas-rejilla");
            rejilla.contentContainer.style.flexDirection = FlexDirection.Row;
            rejilla.contentContainer.style.flexWrap = Wrap.Wrap;
            var huecos = Math.Max(8, (vistas.Count + 3) / 4 * 4);
            for (int i = 0; i < huecos; i++)
            {
                if (i >= vistas.Count) { rejilla.Add(Dialogo.Caja("tropas-hueco tropas-hueco--vacio")); continue; }
                var s = vistas[i];
                var id = s.Id;
                var hueco = new Button(() => { escuadraElegida = id; PintarBase(); }) { name = "escuadra-" + s.Id, tooltip = $"{NombreDeEscuadra(s)} · {s.Cantidad} hombres" };
                hueco.AddToClassList("tropas-hueco");
                var tarjeta = PiezasDeTropa.Tarjeta(s.TropaId, LiderazgoDe(s.TropaId), (int)s.Cantidad, (int)TopeDe(s), s.Id == escuadraElegida, AnchoDeTarjeta);
                if (tarjeta != null) hueco.Add(tarjeta);
                else hueco.Add(Dialogo.Texto(NombreDeEscuadra(s), "aviso-titulo"));
                rejilla.Add(hueco);
            }
            panel.Add(rejilla);
            if (todas.Count == 0) panel.Add(Dialogo.Texto("No tienes escuadras: se reclutan en la plaza o en el campamento donde resides.", "base-vacio"));
            var pie = Dialogo.Caja("tropas-pie");
            var opciones = new List<string> { "Orden de combate", "Liderazgo", "Rareza", "Nombre" };
            var orden = new DropdownField(opciones, Math.Max(0, opciones.IndexOf(ordenTropas ?? "Orden de combate"))) { name = "orden-tropas", tooltip = "Cómo se ven en la rejilla (el orden de combate se cambia al salir al mundo)" };
            orden.AddToClassList("tropas-orden");
            orden.RegisterValueChangedCallback(ev => { ordenTropas = ev.newValue; PintarBase(); });
            pie.Add(orden);
            var prestadas = todas.Where(s => reponer[s.Id]?.prestada == true).ToList();
            Button todasPrestadas = null;
            todasPrestadas = Dialogo.Boton(prestadas.Count > 0 ? $"Reponer las prestadas · {prestadas.Sum(s => reponer[s.Id].Value.faltan)}, gratis" : "Reponer las prestadas",
                "boton-secundario tropas-reponer-prestadas", async () => await OrdenDelPanel(new Orden { ReponerPrestamo = new OrdenReponerPrestamo() }, error, RepintarSiCambia, todasPrestadas));
            todasPrestadas.name = "reponer-prestadas";
            todasPrestadas.SetEnabled(prestadas.Count > 0);
            if (prestadas.Count == 0) todasPrestadas.tooltip = "Se reponen gratis en su campamento cuando les faltan hombres.";
            pie.Add(todasPrestadas);
            panel.Add(pie);
            v.Add(panel);

            // El centro: la tropa elegida en 3D (un soldado de su tropa de Conquest).
            var elegida = todas.FirstOrDefault(s => s.Id == escuadraElegida);
            var datosTropa = elegida == null ? null : PiezasDeTropa.Datos(elegida.TropaId);
            if (foto != null && tropaDelModelo != elegida?.TropaId) { foto = null; modeloTropa?.Quitar(); modeloTropa = null; }
            foto ??= ModeloEn("tropa-modelo", () => PrefabsVisuales?.UnitPrefabs?.FirstOrDefault(x => x.unitId == datosTropa?.visualPrefabName)?.visualPrefab,
                "squad_preview", null, m => modeloTropa = m, () => modeloTropa, 3.5f);
            tropaDelModelo = elegida?.TropaId;
            v.Add(foto);

            // La derecha: sus datos.
            var datos = Dialogo.Caja("tropas-datos");
            datos.name = "tropas-datos";
            v.Add(datos);
            if (elegida == null) { datos.Add(lineaError); return; }
            var sub = Dialogo.Caja("tropas-subpestanas");
            foreach (var (id, nombre) in new[] { ("caracteristicas", "Características"), ("atributos", "Atributos") })
            {
                var p = id;
                var b = Dialogo.Boton(nombre, "tropas-subpestana", () => { subpestanaTropa = p; PintarBase(); });
                b.name = "subpestana-" + id;
                b.EnableInClassList("activo", subpestanaTropa == id);
                if (sub.childCount > 0) sub.Add(Dialogo.Texto("|", "tropas-subpestana-raya"));
                sub.Add(b);
            }
            datos.Add(sub);
            var contenido = new ScrollView(ScrollViewMode.Vertical) { name = "tropas-contenido", horizontalScrollerVisibility = ScrollerVisibility.Hidden };
            contenido.AddToClassList("tropas-contenido");
            datos.Add(contenido);
            if (subpestanaTropa == "atributos") AtributosDeTropa(contenido, elegida, datosTropa);
            else CaracteristicasDeTropa(contenido, elegida, datosTropa, h, reponer[elegida.Id], CampamentoAMano(h), error);
            contenido.Add(lineaError);
        }

        /// <summary>Los colores de rareza de Conquest (`SquadUtils.RarityInfo`), de la más alta a la más baja, sin repetir.</summary>
        static List<Color> ColoresDeRareza() =>
            Enum.GetValues(typeof(SquadRarity)).Cast<SquadRarity>().Reverse().Select(r => SquadUtils.GetRarityInfo(r).color).Distinct().ToList();

        const float AnchoDeTarjeta = 96;

        static string TipoDeTropa(SquadData d) => d?.unitType switch
        {
            UnitType.Infantry => "Infantería",
            UnitType.Distance => "A distancia",
            UnitType.Cavalry => "Caballería",
            _ => "Tropa",
        };

        /// <summary>Un título de sección centrado con su línea fina (`Stats_panel` de Conquest).</summary>
        static VisualElement Seccion(string titulo)
        {
            var s = Dialogo.Caja("tropas-seccion");
            s.Add(Dialogo.Texto(titulo, "tropas-seccion-titulo"));
            s.Add(Dialogo.Caja("tropas-seccion-raya"));
            return s;
        }

        /// <summary>Una cifra con su icono encima (`Status_Panel` de Conquest).</summary>
        static VisualElement Cifra(string icono, string valor, string titulo, VisualElement debajo = null)
        {
            var c0 = Dialogo.Caja("tropas-cifra");
            c0.tooltip = titulo;
            c0.Add(Dialogo.Caja("tropas-cifra-icono heroe-icono-" + icono));
            c0.Add(Dialogo.Texto(valor, "tropas-cifra-valor"));
            if (debajo != null) c0.Add(debajo);
            return c0;
        }

        /// <summary>Características: nombre, tipo y estrellas con sus marcas; las cifras (Liderazgo, hombres, moral, nivel con su barra); las
        /// formaciones que puede usar (las de su tropa en Conquest que permite el motor; la elegida, marcada; se guarda con `elegirFormacion`); sus
        /// órdenes (habilidades de Conquest, solo para ver: se usan en la batalla); dónde está; y «Reponer» con «Pagar con».</summary>
        void CaracteristicasDeTropa(VisualElement v, Escuadron s, SquadData d, Heroe h, (bool prestada, uint faltan, int oro, string motivo)? r,
            CampamentoMercenarios camp, Action<string> error)
        {
            var tope = TopeDe(s);
            var cab = Dialogo.Caja("tropas-cabeza");
            cab.Add(Dialogo.Texto(NombreDeEscuadra(s), "tropas-nombre"));
            cab.Add(Dialogo.Texto(TipoDeTropa(d), "tropas-tipo"));
            var fila = Dialogo.Caja("tropas-estrellas-fila");
            if (PiezasDeTropa.Estrellas(s.TropaId, 18) is VisualElement estrellas) fila.Add(estrellas);
            if (s.HasPrestadaPorCampamentoId) fila.Add(Dialogo.Texto("prestada", "base-marca"));
            if (s.EnGuarnicion) fila.Add(Dialogo.Texto("en guarnición", "base-marca"));
            cab.Add(fila);
            if (PiezasDeTropa.Miniatura(s.TropaId, 64) is VisualElement mini) { var arriba = Dialogo.Caja("tropas-cabeza-fila"); arriba.Add(cab); arriba.Add(mini); v.Add(arriba); }
            else v.Add(cab);

            var cifras = Dialogo.Caja("tropas-cifras");
            cifras.name = "tropas-cifras";
            cifras.Add(Cifra("liderazgo", (LiderazgoDe(s.TropaId) ?? 0).ToString(), "Liderazgo"));
            cifras.Add(Cifra("hombres", $"{s.Cantidad}/{tope}", "Hombres"));
            cifras.Add(Cifra("moral", Math.Round(Instante.Valor(s.Moral, c.Sesion.AhoraMundo) / 1000).ToString(), "Moral"));
            cifras.Add(Cifra("nivel", s.HasPrestadaPorCampamentoId ? $"Nv. {s.Nivel}" : "", s.HasPrestadaPorCampamentoId ? "Prestada: no gana experiencia" : "Nivel y experiencia", s.HasPrestadaPorCampamentoId ? null : ExperienciaDeEscuadra(s)));
            v.Add(cifras);

            // Formaciones.
            v.Add(Seccion("Formaciones"));
            var permitidas = FormacionesPermitidas(h);
            var actual = string.IsNullOrEmpty(s.Formacion) ? "reposo" : s.Formacion;
            var forma = Dialogo.Caja("tropas-botones");
            forma.name = "formaciones";
            var deLaTropa = d == null ? new List<FormationDefinition>() : new[] { d.restFormation }.Concat(d.formations ?? new FormationDefinition[0]).Where(x => x != null).ToList();
            foreach (var (fid, fnombre) in permitidas)
            {
                var def = deLaTropa.FirstOrDefault(x => x.formationType == FamiliaDeFormacion(fid));
                if (def == null && fid != "reposo") continue; // su tropa no la tiene: en la batalla iría la de reposo
                var familia = fid;
                var b = new Button { name = "formacion-" + fid, tooltip = fnombre + (fid == actual ? " (la elegida)" : "") };
                b.AddToClassList("tropas-boton");
                b.EnableInClassList("activo", fid == actual);
                if (def?.formationIcon != null) { var ic = Dialogo.Caja("tropas-boton-icono"); ic.style.backgroundImage = new StyleBackground(def.formationIcon); b.Add(ic); }
                else b.Add(Dialogo.Texto(fnombre.Substring(0, 1), "tropas-boton-letra"));
                if (s.ReservaBatalla != null) { b.SetEnabled(false); b.tooltip = "La formación se congeló al empezar la batalla"; }
                else if (fid != actual) b.clicked += async () => await OrdenDelPanel(new Orden { ElegirFormacion = new OrdenElegirFormacion { EscuadronId = s.Id, Formacion = familia } }, error, RepintarSiCambia, b);
                forma.Add(b);
            }
            v.Add(forma);
            v.Add(Dialogo.Texto(NombreDeFormacion(actual), "tropas-elegida"));

            // Órdenes (habilidades de Conquest).
            var habilidades = d?.abilitiesByLevel?.Where(x => x != null).ToList() ?? new List<AbilityData>();
            if (habilidades.Count > 0)
            {
                v.Add(Seccion("Órdenes"));
                var ordenes = Dialogo.Caja("tropas-botones");
                ordenes.name = "ordenes";
                foreach (var hab in habilidades)
                {
                    var b = Dialogo.Caja("tropas-boton tropas-boton--quieto");
                    b.tooltip = string.IsNullOrEmpty(hab.description) ? hab.abilityName : $"{hab.abilityName}: {hab.description}";
                    if (hab.icon != null) { var ic = Dialogo.Caja("tropas-boton-icono"); ic.style.backgroundImage = new StyleBackground(hab.icon); b.Add(ic); }
                    else b.Add(Dialogo.Texto(hab.abilityName.Substring(0, 1), "tropas-boton-letra"));
                    ordenes.Add(b);
                }
                v.Add(ordenes);
            }

            v.Add(Seccion("Dónde está"));
            v.Add(IconosDelMundo.ConIcono("glifo", "sitio", Dialogo.Texto(DondeEstaLaEscuadra(s, h), "base-sitio"), 14));

            // «Reponer» solo donde se puede: la tuya en tu campamento, con oro; las prestadas, con su botón de la rejilla.
            var fuentes = camp != null && r != null && !r.Value.prestada ? FuentesDePago(h, camp) : new List<(string pagarCon, string nombre, double oro)>();
            if (fuentes.Count > 1)
            {
                var opciones = fuentes.Select(f => $"{f.nombre} ({Math.Floor(f.oro)} de oro)").ToList();
                var actualPago = Math.Max(0, fuentes.FindIndex(f => f.pagarCon == FuenteDePago(h, camp)?.pagarCon));
                var pagar = new DropdownField("Pagar con", opciones, actualPago, null, null) { name = "pagar-con" };
                pagar.AddToClassList("base-pagar-con");
                pagar.RegisterValueChangedCallback(_ => { pagarConBase = fuentes[pagar.index].pagarCon; PintarBase(); });
                v.Add(pagar);
            }
            if (r != null && !r.Value.prestada)
            {
                var acciones = Dialogo.Caja("tropas-acciones");
                Button boton = null;
                var tropa = s.TropaId;
                boton = Dialogo.Boton("", "boton-primario base-reponer", async () =>
                    await OrdenDelPanel(new Orden { ReclutarEnCampamento = new OrdenReclutarEnCampamento { TropaId = tropa, PagarCon = FuenteDePago(h, CampamentoAMano(h))?.pagarCon ?? "almacenPersonal" } },
                        error, RepintarSiCambia, boton));
                boton.name = "reponer";
                boton.Add(Dialogo.Texto($"Reponer {r.Value.faltan}", "boton-texto"));
                var coste = IconosDelMundo.ChipDeRecurso("oro", r.Value.oro.ToString(), $"{r.Value.oro} de oro");
                coste.EnableInClassList("chip-recurso--falta", r.Value.motivo?.StartsWith("No te llega") == true);
                boton.Add(coste);
                boton.SetEnabled(r.Value.motivo == null);
                if (r.Value.motivo != null) boton.tooltip = r.Value.motivo;
                acciones.Add(boton);
                if (r.Value.motivo != null) { var m = Dialogo.Texto(r.Value.motivo, "campo-ayuda"); m.name = "motivo-reponer"; acciones.Add(m); }
                v.Add(acciones);
            }
        }

        /// <summary>La familia del motor como formación de Conquest (la misma tabla que `BatallaRealDeTicket`).</summary>
        static FormationType? FamiliaDeFormacion(string id) => id switch
        {
            "reposo" => FormationType.Rest,
            "linea" => FormationType.Line,
            "columna" => FormationType.Column,
            "dispersa" => FormationType.Dispersed,
            "muro_escudos" => FormationType.ShieldWall,
            "testudo" => FormationType.Testudo,
            "cuadro" => FormationType.Square,
            "cuna" => FormationType.Wedge,
            _ => null,
        };

        /// <summary>Atributos (`SquadDetailPanel`, «Stats_panel»): básicos, ataque y defensa, con las cifras de su tropa en Conquest (las que usa la
        /// batalla) y los hombres del motor. Lo que no aplica, «-».</summary>
        void AtributosDeTropa(VisualElement v, Escuadron s, SquadData d)
        {
            if (d == null) { v.Add(Dialogo.Texto("Esta tropa no tiene ficha en Conquest.", "base-vacio")); return; }
            VisualElement Fila(string icono, string nombre, string valor)
            {
                var f = Dialogo.Caja("tropas-atributo");
                f.Add(Dialogo.Caja("heroe-atributo-icono tropas-atributo-icono heroe-icono-" + icono));
                f.Add(Dialogo.Texto(nombre, "tropas-atributo-nombre"));
                f.Add(Dialogo.Texto(valor, "tropas-atributo-valor"));
                return f;
            }
            string N(float x) => x.ToString("0.#", System.Globalization.CultureInfo.GetCultureInfo("es-ES"));
            var r = d.rangedData;
            float Valor(Func<SquadMeleeData, float> m, Func<SquadRangedData, float> a) => d.meleeData != null ? m(d.meleeData) : r != null ? a(r) : 0;
            v.Add(Seccion("Básicos"));
            v.Add(Fila("vida", "Vida", d.baseHealth.ToString()));
            v.Add(Fila("hombres", "Hombres", $"{s.Cantidad}/{TopeDe(s)}"));
            v.Add(Fila("velocidad", "Velocidad", N(d.baseSpeed)));
            v.Add(Fila("liderazgo", "Liderazgo", (LiderazgoDe(s.TropaId) ?? d.leadershipCost).ToString()));
            v.Add(Fila("alcance", "Alcance", r != null ? N(r.range) : "-"));
            v.Add(Fila("municion", "Munición", r != null ? r.ammo.ToString() : "-"));
            v.Add(Seccion("Ataque"));
            v.Add(Fila("perforante", "Penetración perforante", N(Valor(m => m.piercingPenetration, a => a.piercingPenetration))));
            v.Add(Fila("cortante", "Penetración cortante", N(Valor(m => m.slashingPenetration, a => a.slashingPenetration))));
            v.Add(Fila("contundente", "Penetración contundente", N(Valor(m => m.bluntPenetration, a => a.bluntPenetration))));
            v.Add(Fila("perforante", "Daño perforante", N(Valor(m => m.piercingDamage, a => a.piercingDamage))));
            v.Add(Fila("cortante", "Daño cortante", N(Valor(m => m.slashingDamage, a => a.slashingDamage))));
            v.Add(Fila("contundente", "Daño contundente", N(Valor(m => m.bluntDamage, a => a.bluntDamage))));
            v.Add(Seccion("Defensa"));
            v.Add(Fila("perforante", "Defensa perforante", N(d.piercingDefense)));
            v.Add(Fila("cortante", "Defensa cortante", N(d.slashingDefense)));
            v.Add(Fila("contundente", "Defensa contundente", N(d.bluntDefense)));
            v.Add(Fila("bloqueo", "Bloqueo", d.block > 0 ? d.block.ToString() : "-"));
            v.Add(Fila("regeneracion", "Recuperación del bloqueo", d.blockRegenRate > 0 ? d.blockRegenRate.ToString() : "-"));
        }

        string NombreDeEscuadra(Escuadron s) => string.IsNullOrEmpty(s.Nombre) ? NombreDeTropa(s.TropaId) : s.Nombre;

        /// <summary>«nivel N ▬ lleva/pide · habilidad en el M» (`progresoDeEscuadra`, `siguienteHabilidad`; bloque `MILITAR`).</summary>
        VisualElement ExperienciaDeEscuadra(Escuadron s)
        {
            var m = c.Datos.Bloque("Combate", "MILITAR");
            var curva = (m?["experienciaParaSubirEscuadra"] as JArray)?.Select(x => (double)x).ToList() ?? new List<double> { 10, 25, 50, 100, 150, 200, 250, 275, 315 };
            var tope = (int?)m?["nivelMaximoEscuadra"] ?? curva.Count + 1;
            var habilidades = (m?["nivelesHabilidadEscuadra"] as JArray)?.Select(x => (int)x).ToList() ?? new List<int> { 4, 7, 10 };
            var nivel = (int)s.Nivel;
            var hab = habilidades.Where(n => n > nivel).Select(n => (int?)n).FirstOrDefault();
            var fila = Dialogo.Caja("base-esc-nivel");
            fila.Add(Dialogo.Texto($"nivel {nivel}", "base-esc-texto"));
            if (nivel >= tope || nivel < 1 || nivel > curva.Count)
            {
                fila.Add(Dialogo.Texto(" (el tope)", "base-esc-texto"));
                fila.tooltip = "En el nivel de tope";
            }
            else
            {
                var pasado = curva.Take(nivel - 1).Sum();
                var pide = curva[nivel - 1];
                var lleva = Math.Max(0, Math.Min(pide, Math.Floor(s.Experiencia / 1000.0 - pasado)));
                fila.Add(Barra(lleva / pide, "base-esc-xp"));
                fila.Add(Dialogo.Texto($"{lleva}/{pide}", "base-esc-texto"));
                fila.tooltip = $"Hacia el nivel {nivel + 1}: {lleva} de {pide}{(hab != null ? $". En el nivel {hab} gana una habilidad nueva" : "")}";
            }
            if (hab != null) fila.Add(Dialogo.Texto($"· habilidad en el {hab}", "base-esc-texto"));
            return fila;
        }

        // ─────────────── Bienes ───────────────

        /// <summary>Bienes (Víctor, 2026-11-14): dos paneles. A la izquierda, el más ancho, el almacén personal con «Objetos» (su almacén de objetos,
        /// `almacen_objetos`, en casillas) y «Recursos» (el almacén personal de recursos del prototipo, `htmlCarro`). A la derecha, «Mochila» (los
        /// objetos que lleva, `inventario`) y «Carro» (el carro del prototipo, con «Pasar a víveres»). Lo de la plaza es del paso 7.</summary>
        void PintarBienes(VisualElement v, Heroe h, Action<string> error, Label lineaError)
        {
            var objetos = c.Datos.Bloque("Objetos_Del_Heroe", "OBJETOS");
            VisualElement Panel(string nombre, string clase, (string id, string texto)[] subpestanas, string activa, Action<string> elegir)
            {
                var panel = Dialogo.Caja("bienes-panel " + clase);
                panel.name = nombre;
                var sub = Dialogo.Caja("tropas-subpestanas");
                foreach (var (id, texto) in subpestanas)
                {
                    var p = id;
                    var b = Dialogo.Boton(texto, "tropas-subpestana", () => { elegir(p); PintarBase(); });
                    b.name = "subpestana-" + id;
                    b.EnableInClassList("activo", activa == id);
                    if (sub.childCount > 0) sub.Add(Dialogo.Texto("|", "tropas-subpestana-raya"));
                    sub.Add(b);
                }
                panel.Add(sub);
                var contenido = new ScrollView(ScrollViewMode.Vertical) { horizontalScrollerVisibility = ScrollerVisibility.Hidden };
                contenido.AddToClassList("tropas-contenido");
                contenido.name = nombre + "-contenido";
                panel.Add(contenido);
                v.Add(panel);
                return contenido;
            }
            var izquierda = Panel("bienes-almacen", "bienes-panel--ancho", new[] { ("objetos", "Objetos"), ("recursos", "Recursos") }, subpestanaAlmacen, p => subpestanaAlmacen = p);
            var derecha = Panel("bienes-lleva", "bienes-panel--estrecho", new[] { ("mochila", "Mochila"), ("carro", "Carro") }, subpestanaLleva, p => subpestanaLleva = p);

            if (subpestanaAlmacen == "recursos") RecursosDelAlmacen(izquierda, h, error);
            else Casillas(izquierda, "almacen", h.AlmacenObjetos, (int?)objetos?["casillasAlmacen"] ?? 144, 16, "A la mochila",
                (casilla, n) => new Orden { SacarObjeto = new OrdenSacarObjeto { Casilla = casilla, Cantidad = n } }, h, error);
            if (subpestanaLleva == "carro") CarroDeLaBase(derecha, h, error);
            else Casillas(derecha, "mochila", h.Inventario, (int?)objetos?["casillasMochila"] ?? 72, 9, "Al almacén",
                (casilla, n) => new Orden { GuardarObjeto = new OrdenGuardarObjeto { Casilla = casilla, Cantidad = n } }, h, error);
            (subpestanaLleva == "carro" ? derecha : izquierda).Add(lineaError);
        }

        /// <summary>¿Está dentro de su residencia? Solo ahí se pasan objetos entre la mochila y el almacén (`exigir_dentro_de_su_residencia`).</summary>
        static bool DentroDeSuResidencia(Heroe h)
        {
            var u = h.Ubicacion;
            if (u == null || !h.HasResidenciaId) return false;
            return (u.DondeCase == UbicacionHeroe.DondeOneofCase.CampamentoId && u.CampamentoId == h.ResidenciaId)
                || (u.DondeCase == UbicacionHeroe.DondeOneofCase.AsentamientoId && u.AsentamientoId == h.ResidenciaId);
        }

        /// <summary>Una rejilla de objetos en casillas (`Inventory_Item_Cell` de Conquest): todas las casillas, las vacías a la vista; cada objeto con el
        /// fondo y la línea de su rareza (su `tier`), su imagen provisional y su cantidad, y su ficha al pasar el ratón. Al pulsar uno, debajo su
        /// nombre y, dentro de tu residencia, el botón para pasarlo al otro lado (con la orden del motor); fuera, el botón no sale, sin más explicación (duda 20).</summary>
        void Casillas(VisualElement v, string clave, IEnumerable<ItemInstancia> piezas, int casillas, int columnas, string accion, Func<uint, uint, Orden> orden, Heroe h, Action<string> error)
        {
            var porCasilla = piezas.GroupBy(x => (int)x.CasillaInventario).ToDictionary(g => g.Key, g => g.First());
            v.Add(Dialogo.Texto($"{porCasilla.Count} / {casillas} casillas", "bienes-cuenta"));
            var rejilla = Dialogo.Caja("bienes-rejilla");
            rejilla.name = "rejilla-" + clave;
            v.Add(rejilla);
            // Sin desplazamiento: las casillas, del tamaño que quepa entero (en `columnas` columnas y en el alto del panel, dejando sitio al nombre
            // del elegido), de 30 a 64 px.
            var filas = (casillas + columnas - 1) / columnas;
            var visor = (v as ScrollView)?.contentViewport ?? v;
            void Medir()
            {
                var ancho = visor.resolvedStyle.width;
                var alto = visor.resolvedStyle.height - 24 - 52;
                if (float.IsNaN(ancho) || ancho <= 0) return;
                var lado = Mathf.Clamp(Mathf.Floor(Mathf.Min(ancho / columnas, float.IsNaN(alto) || alto <= 0 ? ancho : alto / filas)) - 4, 30, 64);
                rejilla.style.width = (lado + 4) * columnas;
                foreach (var hijo in rejilla.Children())
                {
                    hijo.style.width = lado;
                    hijo.style.height = lado;
                    var icono = hijo.Q(className: "icono");
                    if (icono != null) icono.style.width = icono.style.height = Mathf.Round(lado * 0.6f);
                }
            }
            visor.RegisterCallback<GeometryChangedEvent>(_ => Medir());
            rejilla.schedule.Execute(Medir);
            var elegida = objetoElegido.StartsWith(clave + ":") && int.TryParse(objetoElegido.Substring(clave.Length + 1), out var e) ? e : -1;
            for (int i = 0; i < casillas; i++)
            {
                if (!porCasilla.TryGetValue(i, out var pieza)) { rejilla.Add(Dialogo.Caja("bienes-casilla bienes-casilla--vacia")); continue; }
                var casilla = i;
                var b = new Button(() => { objetoElegido = elegida == casilla ? "" : $"{clave}:{casilla}"; PintarBase(); }) { name = $"{clave}-{i}" };
                b.AddToClassList("bienes-casilla");
                b.EnableInClassList("activo", elegida == i);
                var (nombre, tier, rareza) = FichaDeObjeto(pieza);
                b.AddToClassList("equipo-rareza-" + tier);
                var imagen = IconosDelMundo.Icono("recurso", ImagenDeObjeto(pieza), 34);
                if (imagen != null) b.Add(imagen);
                var linea = Dialogo.Caja("equipo-linea");
                linea.style.backgroundColor = InventoryUtils.GetRarityColor(rareza);
                b.Add(linea);
                if (pieza.Cantidad > 1) b.Add(Dialogo.Texto(pieza.Cantidad.ToString(), "bienes-casilla-cantidad"));
                var stats = string.Join(", ", pieza.Estadisticas.Select(x => $"{x.Nombre} {x.Valor}"));
                b.tooltip = $"{nombre}{(pieza.Cantidad > 1 ? $" ×{pieza.Cantidad}" : "")} · {rareza} (tier {tier}){(stats.Length > 0 ? " · " + stats : "")}";
                rejilla.Add(b);
            }
            if (elegida >= 0 && porCasilla.TryGetValue(elegida, out var sel))
            {
                var barra = Dialogo.Caja("bienes-elegido");
                barra.name = "elegido-" + clave;
                barra.Add(Dialogo.Texto($"{FichaDeObjeto(sel).nombre}{(sel.Cantidad > 1 ? $" ×{sel.Cantidad}" : "")}", "bienes-elegido-nombre"));
                if (DentroDeSuResidencia(h))
                {
                    Button mover = null;
                    mover = Dialogo.Boton(accion, "boton-secundario base-boton-fila", async () =>
                    {
                        objetoElegido = "";
                        await OrdenDelPanel(orden((uint)elegida, Math.Max(1, sel.Cantidad)), error, RepintarSiCambia, mover);
                    });
                    mover.name = "mover-" + clave;
                    barra.Add(mover);
                }
                v.Add(barra);
            }
        }

        /// <summary>Nombre, tier (1 a 5; el motor no tiene rareza) y la rareza de Conquest que le toca, de la ficha del objeto en `OBJETOS`.</summary>
        (string nombre, int tier, ItemRarity rareza) FichaDeObjeto(ItemInstancia pieza)
        {
            var ficha = (c.Datos.Bloque("Objetos_Del_Heroe", "OBJETOS")?["objetos"] as JArray)?.OfType<JObject>().FirstOrDefault(x => (string)x["id"] == pieza.ItemDefinitionId);
            var tier = Mathf.Clamp((int?)ficha?["tier"] ?? 1, 1, 5);
            return ((string)ficha?["nombre"] ?? pieza.ItemDefinitionId, tier, new[] { ItemRarity.Common, ItemRarity.Uncommon, ItemRarity.Rare, ItemRarity.Epic, ItemRarity.Legendary }[tier - 1]);
        }

        /// <summary>La imagen provisional de un objeto: la del prototipo para su tipo (arma, armadura, montura; los demás, la genérica).</summary>
        static string ImagenDeObjeto(ItemInstancia pieza) => pieza.Tipo switch
        {
            "arma" => "armaBronce",
            "armadura" => "armaduraBronce",
            "montura" => "livestock",
            _ => "_otro",
        };

        /// <summary>«Recursos»: el almacén personal de recursos con su barra y «Al carro» (`htmlCarro`), o la nota si no está a mano.</summary>
        void RecursosDelAlmacen(VisualElement v, Heroe h, Action<string> error)
        {
            var columna = h.Ubicacion?.DondeCase == UbicacionHeroe.DondeOneofCase.EjercitoId ? ColumnaDelHeroe(h) : null;
            var esLider = columna?.LiderId == h.Id;
            var motivoLider = columna != null && !esLider ? "Solo el Líder de la columna mueve lo del carro." : null;
            var capacidad = c.Datos.Numero("documentos.Movimiento_Ejercitos.LOGISTICA.capacidadCarroPorJugador") * Math.Max(1, columna?.Participantes.Count ?? 1);
            var libre = Math.Max(0, Math.Floor(capacidad - Unidades(columna != null ? columna.Suministro : h.FueraCarro).Sum(x => x.Value)));
            var almacen = Unidades(h.AlmacenPersonal);
            var caja = new VisualElement { name = "bienes-recursos" };
            v.Add(caja);
            caja.Add(Ocupacion("Almacén personal", almacen.Sum(x => x.Value), h.CapacidadAlmacenPersonal / 1000.0, "ocupacion-almacen"));
            if (!AlmacenAMano(h, columna))
            {
                Filas(caja, almacen, "personal", new List<(string, string, double?, Func<string, long, Orden>)>(), error);
                caja.Add(Dialogo.Texto("El almacén personal está en tu residencia: se usa dentro o con tu columna a su puerta.", "base-nota"));
                return;
            }
            Filas(caja, almacen, "personal", new List<(string, string, double?, Func<string, long, Orden>)>
            {
                ("Al carro", motivoLider ?? (libre == 0 ? "El carro está lleno." : null), libre,
                    (r, n) => new Orden { SacarDelAlmacenPersonal = new OrdenSacarDelAlmacenPersonal { Recurso = r, Cantidad = n * 1000 } }),
            }, error);
        }

        /// <summary>«Carro»: pasar trigo a víveres (solo el Líder), el carro con su barra y sus filas, «Todo al almacén personal» y lo aparcado.</summary>
        void CarroDeLaBase(VisualElement v, Heroe h, Action<string> error)
        {
            var carga = new VisualElement { name = "bienes-carga" };
            v.Add(carga);
            var columna = h.Ubicacion?.DondeCase == UbicacionHeroe.DondeOneofCase.EjercitoId ? ColumnaDelHeroe(h) : null;
            var esLider = columna?.LiderId == h.Id;
            var aMano = AlmacenAMano(h, columna);
            var carro = columna != null ? columna.Suministro : h.FueraCarro;
            var capacidad = c.Datos.Numero("documentos.Movimiento_Ejercitos.LOGISTICA.capacidadCarroPorJugador") * Math.Max(1, columna?.Participantes.Count ?? 1);
            var enCarro = Unidades(carro);
            var hayCarro = enCarro.Count > 0;
            var motivoLider = columna != null && !esLider ? "Solo el Líder de la columna mueve lo del carro." : null;

            if (columna != null && esLider) PasarAViveres(carga, h, columna, error);
            var nota = columna == null
                ? "Dentro de tu residencia el carro sigue contigo: entrar no lo vacía. Pásalo a tu almacén personal recurso a recurso, o todo de una vez con «Todo al almacén personal». Al salir, lo que haya en el carro sale contigo y en la ventana de salida eliges qué más cargas."
                : !esLider ? "Solo el Líder de la columna mueve lo del carro: el carro es común a todos los que van en ella."
                : "Guarda en tu almacén personal lo que quieras conservar. Al entrar en tu campamento se guarda solo. El carro es solo carga: la columna come de tus víveres, y de aquí se les pasa trigo.";
            carga.Add(ConAyuda(Dialogo.Texto("Carga", "base-sub"), nota));
            carga.Add(Ocupacion(columna != null ? "Carro de tu columna" : "Tu carro", enCarro.Sum(x => x.Value), capacidad, "ocupacion-carro"));
            var accionesCarro = new List<(string etiqueta, string motivo, double? tope, Func<string, long, Orden> orden)>();
            if (aMano) accionesCarro.Add(("Al almacén personal", motivoLider, null, (r, n) => new Orden { GuardarEnAlmacenPersonal = new OrdenGuardarEnAlmacenPersonal { Recurso = r, Cantidad = n * 1000 } }));
            Filas(carga, enCarro, "carro", hayCarro ? accionesCarro : new List<(string, string, double?, Func<string, long, Orden>)>(), error);
            if (columna == null && hayCarro && aMano)
            {
                Button todo = null;
                todo = Dialogo.Boton("Todo al almacén personal", "boton-secundario base-boton-alineado", async () =>
                    await OrdenDelPanel(new Orden { VaciarCarro = new OrdenVaciarCarro { HeroeId = h.Id } }, error, RepintarSiCambia, todo));
                todo.name = "vaciar-carro";
                carga.Add(todo);
            }
            // El resto del carro que se quedó aparcado en la puerta de un campamento al entrar sin poder guardarlo todo.
            foreach (var aparcada in c.Sesion.Almacen.Todas(Entidad.CuerpoOneofCase.Ejercito).Select(e => e.Ejercito)
                         .Where(e => e != columna && e.LiderId == h.Id && Unidades(e.Suministro).Count > 0))
            {
                carga.Add(Dialogo.Texto("Resto del carro, aparcado en la puerta", "base-sub"));
                Filas(carga, Unidades(aparcada.Suministro), "aparcado", new List<(string, string, double?, Func<string, long, Orden>)>(), error);
            }
        }

        /// <summary>Lo que hay en un montón, en unidades enteras (lo de menos de una no sale).</summary>
        static List<KeyValuePair<string, double>> Unidades(Cantidades q) =>
            q?.PorId.Where(kv => kv.Value >= 1000).OrderBy(kv => kv.Key).Select(kv => new KeyValuePair<string, double>(kv.Key, Math.Floor(kv.Value / 1000.0))).ToList()
            ?? new List<KeyValuePair<string, double>>();

        /// <summary>¿Se puede tocar el almacén personal? Como el motor (`a_mano.rs`): dentro de tu residencia, o con tu columna a su puerta.</summary>
        bool AlmacenAMano(Heroe h, Ejercito columna)
        {
            var u = h.Ubicacion?.DondeCase;
            if (columna == null) return u == UbicacionHeroe.DondeOneofCase.AsentamientoId || u == UbicacionHeroe.DondeOneofCase.CampamentoId;
            if (!h.HasResidenciaId) return false;
            Punto puerta = h.ResidenciaId.StartsWith("camp-")
                ? CampamentoActual(h.ResidenciaId)?.Posicion
                : c.Sesion.Almacen.Buscar(TipoEntidad.Asentamiento, h.ResidenciaId)?.Asentamiento?.Posicion;
            return ALaPuerta(columna, puerta);
        }

        /// <summary>Cuánto lleva y cuánto cabe, con su barra: verde, ámbar desde el 80 % y roja llena (`ocupacion`).</summary>
        static VisualElement Ocupacion(string titulo, double total, double capacidad, string nombre)
        {
            var caja = new VisualElement { name = nombre };
            var fila = Dialogo.Caja("base-ocupacion");
            fila.Add(Dialogo.Texto(titulo, "base-sub"));
            if (capacidad <= 0) { caja.Add(fila); return caja; }
            var pct = Math.Min(100, Math.Round(total / capacidad * 100));
            fila.Add(Dialogo.Texto($"{Math.Floor(total)} / {Math.Floor(capacidad)} · {pct} %", "base-ocupacion-cifra"));
            caja.Add(fila);
            var barra = Barra(pct / 100, "base-carro-barra " + (pct >= 100 ? "base-carro-barra--llena" : pct >= 80 ? "base-carro-barra--casi" : "base-carro-barra--ok"));
            caja.Add(barra);
            return caja;
        }

        /// <summary>Las filas de un montón con su cantidad y, si hay acciones, el número y sus botones (`filas` del prototipo).</summary>
        void Filas(VisualElement v, List<KeyValuePair<string, double>> recursos, string clave,
            List<(string etiqueta, string motivo, double? tope, Func<string, long, Orden> orden)> acciones, Action<string> error)
        {
            if (recursos.Count == 0) { v.Add(Dialogo.Texto("Vacío.", "base-vacio")); return; }
            foreach (var kv in recursos)
            {
                var recurso = kv.Key;
                var util = (long)kv.Value;
                var fila = Dialogo.Caja("base-carro-fila");
                fila.name = $"{clave}-{recurso}";
                var nombre = IconosDelMundo.ConIcono("recurso", recurso, Dialogo.Texto(CosasDelMapa.NombreDeRecurso(recurso), "base-carro-nombre"), 18);
                nombre.AddToClassList("base-carro-recurso");
                fila.Add(nombre);
                fila.Add(Dialogo.Texto(util.ToString(), "base-carro-cifra"));
                if (acciones.Count > 0)
                {
                    var valor = Math.Max(1, (long)Math.Min(util, acciones.Min(a => a.tope ?? util)));
                    var campo = new IntegerField { value = (int)valor, name = $"cantidad-{clave}-{recurso}" };
                    campo.AddToClassList("base-cantidad");
                    fila.Add(campo);
                    foreach (var a in acciones)
                    {
                        var accion = a;
                        Button b = null;
                        b = Dialogo.Boton(accion.etiqueta, "boton-secundario base-boton-fila", async () =>
                        {
                            if (campo.value < 1) { error("Pon una cantidad de 1 o más."); return; }
                            await OrdenDelPanel(accion.orden(recurso, campo.value), error, RepintarSiCambia, b);
                        });
                        b.name = $"{clave}-{recurso}-{(accion.etiqueta == "Al carro" ? "al-carro" : "al-almacen")}";
                        if (accion.motivo != null) { b.SetEnabled(false); b.tooltip = accion.motivo; }
                        fila.Add(b);
                    }
                }
                v.Add(fila);
            }
        }

        /// <summary>Pasar trigo del carro a los víveres (`pasarAViveres`): solo el Líder de la columna y solo lo que cabe.</summary>
        void PasarAViveres(VisualElement v, Heroe h, Ejercito col, Action<string> error)
        {
            var capacidad = c.Datos.Numero("documentos.Movimiento_Ejercitos.LOGISTICA.capacidadViveresPorHeroe") * Math.Max(1, col.Participantes.Count);
            var viveres = Math.Floor(Instante.Valor(h.Viveres, c.Sesion.AhoraMundo) / 1000);
            var hueco = Math.Max(0, Math.Floor(capacidad - viveres));
            var trigo = Math.Floor(Milesimas(col.Suministro, "trigo"));
            var maximo = (int)Math.Min(hueco, trigo);
            if (maximo < 1) return;
            var fila = Dialogo.Caja("base-viveres");
            fila.name = "pasar-a-viveres";
            fila.Add(IconosDelMundo.ConIcono("glifo", "viveres", Dialogo.Texto($"Víveres {viveres} / {capacidad:0} · trigo en el carro {trigo}", "base-carro-nombre"), 16));
            var campo = new IntegerField { value = maximo, name = "cantidad-viveres" };
            campo.AddToClassList("base-cantidad");
            fila.Add(campo);
            Button b = null;
            b = Dialogo.Boton("Pasar a víveres", "boton-secundario base-boton-fila", async () =>
                await OrdenDelPanel(new Orden { PasarAViveres = new OrdenPasarAViveres { Cantidad = Math.Max(1, Math.Min(maximo, campo.value)) * 1000L } }, error, RepintarSiCambia, b));
            b.name = "pasar-viveres";
            fila.Add(b);
            v.Add(fila);
        }
    }

    /// <summary>Un modelo 3D de la Base (el héroe modular o un soldado de una tropa de Conquest), lejos de todo y en su capa (`hero_preview` o
    /// `squad_preview`), con su cámara (solo esa capa, fondo transparente) pintando en una textura y su luz. Se le quitan los componentes del juego
    /// que trae (EntityVisualSync lo llevaría a la entidad y se tomaría por uno de la escena; la animación por ECS lo seguiría); se le dejan el
    /// Animator, en reposo, y el `AvatarPartSelector` que usa el vestido. De frente; se gira arrastrando.</summary>
    sealed class ModeloDeLaBase
    {
        static readonly Vector3 Lejos = new Vector3(50000, 50000, 50000);
        GameObject raiz;
        Transform modelo;
        Animator animador;
        public RenderTexture Imagen { get; private set; }

        public static ModeloDeLaBase Crear(GameObject prefab, string nombreDeCapa, Action<Transform> preparar, float distancia)
        {
            var capa = LayerMask.NameToLayer(nombreDeCapa);
            if (capa < 0) return null;
            var r = new ModeloDeLaBase { raiz = new GameObject("Modelo de la Base " + nombreDeCapa) };
            r.raiz.SetActive(false);
            r.raiz.transform.position = Lejos + (nombreDeCapa == "squad_preview" ? new Vector3(100, 0, 0) : Vector3.zero);
            var instancia = UnityEngine.Object.Instantiate(prefab, r.raiz.transform, false);
            instancia.transform.localPosition = Vector3.zero;
            instancia.transform.localRotation = Quaternion.identity; // de cara a la cámara (el prefab del héroe viene girado de espaldas)
            r.modelo = instancia.transform;
            foreach (var m in instancia.GetComponentsInChildren<MonoBehaviour>(true))
                if (!(m is AvatarPartSelector)) UnityEngine.Object.DestroyImmediate(m);
            foreach (var cc in instancia.GetComponentsInChildren<CharacterController>(true)) UnityEngine.Object.DestroyImmediate(cc);
            // El cubo que llevan los soldados encima (el marcador de la escuadra en la batalla) no es del soldado.
            foreach (var marca in instancia.GetComponentsInChildren<MeshRenderer>(true).Where(x => x.name == "Cube")) marca.enabled = false;
            preparar?.Invoke(instancia.transform);
            foreach (var t in r.raiz.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = capa;
            r.animador = instancia.GetComponentInChildren<Animator>();

            // Del alto y ancho del recuadro central (unos 2 de ancho por 3 de alto), con transparencia.
            r.Imagen = new RenderTexture(640, 960, 24, RenderTextureFormat.ARGB32) { name = "Modelo de la Base" };
            r.Imagen.Create();
            var camara = new GameObject("Cámara del modelo").AddComponent<Camera>();
            camara.transform.SetParent(r.raiz.transform, false);
            camara.transform.localPosition = new Vector3(0, 1.0f, distancia);
            camara.transform.LookAt(r.raiz.transform.position + new Vector3(0, 0.92f, 0));
            camara.fieldOfView = 30;
            camara.clearFlags = CameraClearFlags.SolidColor;
            camara.backgroundColor = new Color(0, 0, 0, 0);
            camara.cullingMask = 1 << capa;
            camara.nearClipPlane = 0.1f;
            camara.farClipPlane = 20;
            camara.depth = -50;
            camara.allowHDR = false;
            camara.targetTexture = r.Imagen;
            // Su luz: un foco puntual al lado, que con su alcance corto no llega a nada más (está lejos de todo).
            var luz = new GameObject("Luz del modelo").AddComponent<Light>();
            luz.transform.SetParent(r.raiz.transform, false);
            luz.transform.localPosition = new Vector3(1.2f, 2.4f, 2.6f);
            luz.type = LightType.Point;
            luz.range = 12;
            luz.intensity = 6;
            luz.cullingMask = 1 << capa;
            r.raiz.SetActive(true);
            // De pie y en reposo (los parámetros, ya encendido: apagado no los guarda). Sin esto, el controlador del héroe arranca en su salto.
            if (r.animador != null && r.animador.runtimeAnimatorController != null)
            {
                foreach (var (p, v) in new[] { ("IsGrounded", true), ("IsStopped", true) })
                    if (r.animador.parameters.Any(x => x.name == p && x.type == AnimatorControllerParameterType.Bool)) r.animador.SetBool(p, v);
                if (r.animador.HasState(0, Animator.StringToHash("Idle_Standing"))) r.animador.Play("Idle_Standing", 0, 0);
            }
            return r;
        }

        public void Girar(float pixeles) { if (modelo != null) modelo.Rotate(0, -pixeles * 0.5f, 0, Space.World); }

        public void Quitar()
        {
            if (raiz != null) UnityEngine.Object.Destroy(raiz);
            if (Imagen != null) { Imagen.Release(); UnityEngine.Object.Destroy(Imagen); }
        }
    }
}
