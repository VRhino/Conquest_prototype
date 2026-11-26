// La tienda de la Armería a pantalla completa (Víctor, 2026-11-19), calcada de BronzeAgeClient (`htmlTienda` y `cablearArmeria` de `ui/armeria.ts`):
// arriba tus monedas y tu mochila; a la izquierda «Comprar», con sus filtros y una casilla por pieza; a la derecha «Vender», tu mochila entera en
// casillas. Tocar una casilla saca a su lado la ficha de la pieza con su precio y «Comprar» o «Vender»; se cierra con su ✕, tocando fuera o al
// tocar otra. La usan la Armería de la plaza (su pestaña Tienda) y la del campamento.
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
        /// <summary>La pieza que miras en Comprar; la de Vender es <see cref="casillaArmeria"/>. Se recuerdan entre repintados.</summary>
        string elegidaTienda;
        VisualElement fichaDeTienda;

        /// <summary>Lo que vende la tienda (`piezasDeTienda`): armas, armaduras y monturas hasta `tiendaHastaTier`, con su metal abierto y sin las que no
        /// se venden (`enTienda: false`).</summary>
        List<JObject> PiezasDeTienda(Asentamiento a)
        {
            var hasta = (int?)Objetos?["tiendaHastaTier"] ?? 2;
            return CatalogoDeObjetos.Where(f => ((string)f["tipo"] == "arma" || (string)f["tipo"] == "armadura" || (string)f["tipo"] == "montura")
                && (int?)f["tier"] is int t && t <= hasta && (bool?)f["enTienda"] != false && MetalAbierto(a, f)).ToList();
        }

        void TiendaCompleta(VisualElement cuerpo, Asentamiento a, Heroe yo, Action rehacer)
        {
            CerrarFichaDeTienda();
            var casillas = (int?)Objetos?["casillasMochila"] ?? 72;
            var venta = (int?)Objetos?["ventaPct"] ?? 50;
            var libres = casillas - yo.Inventario.Count;
            var tienda = Dialogo.Caja("arm-tienda");
            tienda.name = "tienda";
            var error = Dialogo.Texto("", "plaza-error");
            error.name = "error-armeria";

            // Arriba: monedas y mochila.
            var cab = Dialogo.Caja("arm-cab");
            var saldo = Dialogo.Caja("plaza-arm-saldo");
            saldo.name = "monedas";
            saldo.Add(IconosDelMundo.Icono("glifo", "monedas", 16));
            saldo.Add(Dialogo.Texto($"{Miles2(yo.Monedas)} monedas", "plaza-arm-saldo-texto"));
            cab.Add(saldo);
            cab.Add(Dialogo.Texto($"Mochila {yo.Inventario.Count}/{casillas}", "arm-mochila-cuenta"));
            tienda.Add(cab);
            if (libres < 1) tienda.Add(Dialogo.Texto("La mochila está llena: vende algo o guárdalo en tu almacén de objetos antes de comprar.", "plaza-nota"));

            var cols = Dialogo.Caja("plaza-cols");
            var izq = Dialogo.Caja("plaza-col arm-panel");
            var der = Dialogo.Caja("plaza-col arm-panel");
            DosColumnas(cols, izq, der, 900, 22);
            tienda.Add(cols);
            tienda.Add(error);
            cuerpo.Add(tienda);
            Action reabrir = null;

            // ── Comprar ──
            var todas = PiezasDeTienda(a);
            var tipos = new[] { ("arma", "Armas"), ("armadura", "Armaduras"), ("montura", "Monturas") }.Where(x => todas.Any(f => (string)f["tipo"] == x.Item1)).ToList();
            if (!tipos.Any(x => x.Item1 == tipoTienda)) tipoTienda = tipos.FirstOrDefault().Item1 ?? "armadura";
            izq.Add(TituloDeSeccion("Comprar", $"Al 100 %, con tus monedas, y a tu mochila. Vende armas, armaduras y monturas hasta el tier {(int?)Objetos?["tiendaHastaTier"] ?? 2}, con los metales que abre la tecnología de la Facción de esta plaza. Lo mejor se forja."));
            var filtros = Dialogo.Caja("plaza-arm-filtros");
            foreach (var (t, nombre) in tipos)
            {
                var tipo = t;
                var b = Dialogo.Boton(nombre, "plaza-arm-chip" + (t == tipoTienda ? " activo" : ""), () => { tipoTienda = tipo; elegidaTienda = null; rehacer(); });
                b.name = "tipo-" + t;
                filtros.Add(b);
            }
            if (tipoTienda != "montura")
            {
                var solo = Dialogo.Caja("arm-solo");
                SoloDeMiClase(solo, yo, () => { elegidaTienda = null; rehacer(); });
                filtros.Add(solo);
            }
            izq.Add(filtros);
            var compra = Dialogo.Caja("arm-rejilla");
            compra.name = "rejilla-compra";
            izq.Add(compra);
            var lista = Ordenar(todas.Where(f => (string)f["tipo"] == tipoTienda && (!soloMiClase || DeMiClase(yo, f)))).ToList();
            if (!lista.Any(f => (string)f["id"] == elegidaTienda)) elegidaTienda = null;
            if (lista.Count == 0) compra.Add(Dialogo.Texto("Nada de esto a la venta aquí.", "plaza-vacio"));
            foreach (var f in lista)
            {
                var id = (string)f["id"];
                var precio = (long?)f["precio"] ?? 0;
                var pieza = ComoPieza(f);
                Button b = null;
                void Abrir()
                {
                    elegidaTienda = id;
                    casillaArmeria = -1;
                    b.AddToClassList("activo");
                    var pie = PieDeFicha(precio, precio > (long)yo.Monedas);
                    if (libres < 1) pie.Add(Dialogo.Texto("La mochila está llena.", "plaza-arm-falta"));
                    else if (precio > (long)yo.Monedas) pie.Add(Dialogo.Texto($"Te faltan {Miles2(precio - (long)yo.Monedas)}", "plaza-arm-falta"));
                    else
                    {
                        Button comprar = null;
                        comprar = Dialogo.Boton("Comprar", "boton-primario arm-ficha-boton", async () =>
                        {
                            if (await OrdenDeArmeria(new Orden { ComprarObjeto = new OrdenComprarObjeto { Objeto = id } }, error, comprar)) rehacer();
                        });
                        comprar.name = "comprar";
                        pie.Add(comprar);
                    }
                    AbrirFichaDeTienda(b, pieza, PuestaEnHueco(yo, (string)f["hueco"]), pie);
                }
                b = CasillaDeTienda("ver-" + id, pieza, () => { var ya = elegidaTienda == id; CerrarFichaDeTienda(); if (!ya) Abrir(); });
                b.tooltip = (string)f["nombre"];
                if (precio <= (long)yo.Monedas) b.AddToClassList("comprable");
                if (id == elegidaTienda) reabrir = Abrir;
                compra.Add(b);
            }

            // ── Vender: la mochila entera, cada pieza en su casilla ──
            der.Add(TituloDeSeccion("Vender", $"Cualquier cosa de tu mochila, al {venta} % del precio de su ficha. Una pieza forjada vale lo mismo que la de tienda. Lo que llevas puesto no se vende: quítatelo antes."));
            var rejilla = Dialogo.Caja("arm-rejilla arm-rejilla-venta");
            rejilla.name = "rejilla-armeria";
            der.Add(rejilla);
            var porCasilla = yo.Inventario.GroupBy(x => (int)x.CasillaInventario).ToDictionary(g => g.Key, g => g.First());
            if (!porCasilla.ContainsKey(casillaArmeria)) casillaArmeria = -1;
            for (int i = 0; i < casillas; i++)
            {
                if (!porCasilla.TryGetValue(i, out var pieza)) { rejilla.Add(Dialogo.Caja("bienes-casilla bienes-casilla--vacia arm-casilla-vacia")); continue; }
                var casilla = i;
                var sel = pieza;
                Button b = null;
                void Abrir()
                {
                    casillaArmeria = casilla;
                    elegidaTienda = null;
                    b.AddToClassList("activo");
                    var precio = (((long?)FichaDelMotor(sel.ItemDefinitionId)?["precio"] ?? sel.Precio) * venta) / 100;
                    var pie = PieDeFicha(precio, false, sel.Cantidad > 1 ? " c/u" : "");
                    IntegerField cuantas = null;
                    if (sel.Cantidad > 1)
                    {
                        cuantas = new IntegerField { value = 1, name = "vender-cantidad" };
                        cuantas.AddToClassList("plaza-carro-cantidad");
                        pie.Add(cuantas);
                    }
                    Button vender = null;
                    vender = Dialogo.Boton("Vender", "boton-secundario arm-ficha-boton", async () =>
                    {
                        var n = (uint)Mathf.Clamp(cuantas?.value ?? 1, 1, (int)sel.Cantidad);
                        if (await OrdenDeArmeria(new Orden { VenderObjeto = new OrdenVenderObjeto { Casilla = (uint)casilla, Cantidad = n } }, error, vender)) { casillaArmeria = -1; rehacer(); }
                    });
                    vender.name = "vender";
                    pie.Add(vender);
                    AbrirFichaDeTienda(b, sel, null, pie);
                }
                b = CasillaDeTienda("mochila-" + i, pieza, () => { var ya = casillaArmeria == casilla; CerrarFichaDeTienda(); if (!ya) Abrir(); });
                b.tooltip = FichaDeObjeto(pieza).nombre;
                if (pieza.Cantidad > 1) b.Add(Dialogo.Texto(pieza.Cantidad.ToString(), "bienes-casilla-cantidad"));
                if (casilla == casillaArmeria) reabrir = Abrir;
                rejilla.Add(b);
            }
            // Las casillas de Vender, doce por fila y cuadradas, a lo que dé su columna (`repeat(12, minmax(0, 1fr))`).
            rejilla.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                var ancho = rejilla.resolvedStyle.width;
                if (float.IsNaN(ancho) || ancho < 1) return;
                var lado = Mathf.Floor((ancho - 12 * 4) / 12);
                foreach (var x in rejilla.Children()) { x.style.width = lado; x.style.height = lado; }
            });

            // Tocar fuera de una casilla cierra la ficha; al irse la tienda (repintar o cerrar), también.
            tienda.RegisterCallback<PointerDownEvent>(ev =>
            {
                for (var t = ev.target as VisualElement; t != null && t != tienda; t = t.parent) if (t.ClassListContains("arm-casilla")) return;
                elegidaTienda = null;
                casillaArmeria = -1;
                CerrarFichaDeTienda();
            });
            tienda.RegisterCallback<DetachFromPanelEvent>(_ => CerrarFichaDeTienda());
            // Tras repintar (comprar, vender, el refresco), la que estaba abierta vuelve a su sitio cuando su casilla ya tiene sitio.
            if (reabrir != null)
            {
                void Reabrir(GeometryChangedEvent _) { tienda.UnregisterCallback<GeometryChangedEvent>(Reabrir); reabrir(); }
                tienda.RegisterCallback<GeometryChangedEvent>(Reabrir);
            }
        }

        /// <summary>Una casilla de la tienda (`obj-casilla`): la imagen con el borde de su rareza y su línea; ✦ si es forjada.</summary>
        Button CasillaDeTienda(string nombre, ItemInstancia pieza, Action alTocar)
        {
            var b = new Button(alTocar) { name = nombre };
            b.AddToClassList("bienes-casilla");
            b.AddToClassList("arm-casilla");
            var (_, tier, rareza) = FichaDeObjeto(pieza);
            b.AddToClassList("equipo-rareza-" + tier);
            if (ImagenDe(pieza, 30) is VisualElement img) { img.AddToClassList("arm-casilla-imagen"); b.Add(img); }
            var linea = Dialogo.Caja("equipo-linea");
            linea.style.backgroundColor = InventoryUtils.GetRarityColor(rareza);
            b.Add(linea);
            if (pieza.HasItemInstanceId) b.Add(Dialogo.Texto("✦", "arm-forjada"));
            return b;
        }

        /// <summary>El pie de la ficha (`arm-ficha-pie`): el precio y, detrás, lo que se hace.</summary>
        static VisualElement PieDeFicha(long precio, bool caro, string sufijo = "")
        {
            var pie = Dialogo.Caja("arm-ficha-pie");
            var p = Dialogo.Caja("plaza-arm-precio" + (caro ? " caro" : ""));
            p.Add(IconosDelMundo.Icono("glifo", "monedas", 14));
            p.Add(Dialogo.Texto(Miles2(precio) + sufijo, "plaza-arm-precio-texto"));
            pie.Add(p);
            return pie;
        }

        /// <summary>La ficha de la pieza junto a su casilla (la de pasar el ratón, pero se queda), con su ✕ y su pie.</summary>
        void AbrirFichaDeTienda(VisualElement junto, ItemInstancia pieza, ItemInstancia comparar, VisualElement pie)
        {
            var ficha = TarjetaDeObjeto(pieza, comparar != null && comparar.ItemDefinitionId != pieza.ItemDefinitionId ? comparar : null);
            ficha.name = "ficha-armeria";
            ficha.pickingMode = PickingMode.Position;
            ficha.AddToClassList("arm-detalle");
            if (HojaDePlaza is StyleSheet hoja) ficha.styleSheets.Add(hoja);
            var cerrar = Dialogo.Boton("✕", "arm-cerrar", () => { elegidaTienda = null; casillaArmeria = -1; CerrarFichaDeTienda(); });
            cerrar.name = "ficha-cerrar";
            ficha.Add(pie);
            ficha.Add(cerrar);
            fichaDeTienda = ficha;
            capaEncima.Add(ficha);
            ColocarJunto(ficha, junto);
        }

        void CerrarFichaDeTienda()
        {
            if (fichaDeTienda == null) return;
            foreach (var b in capaEncima.panel?.visualTree.Query<Button>(className: "arm-casilla").ToList() ?? new List<Button>()) b.RemoveFromClassList("activo");
            fichaDeTienda.RemoveFromHierarchy();
            fichaDeTienda = null;
        }
    }
}
