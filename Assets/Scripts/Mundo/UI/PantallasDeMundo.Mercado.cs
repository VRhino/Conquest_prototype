// El Mercado de la plaza (paso 10, tanda 1), calcado de `ui/panelMercado.ts` (Órdenes) y `ui/mercadoDePlaza.ts` (tomar una orden en persona):
// - en tu plaza, la pestaña Órdenes: la boleta (Vender / Comprar, el almacén como casillas, cantidad y precio) a la izquierda y las órdenes en pie
//   a la derecha, cada una con «✕ Cancelar» (`cancelarOrdenMercado`, que el prototipo no tiene);
// - en una plaza ajena, sus órdenes abiertas, que el Líder de la columna toma con su carro (`comerciarEnPlaza`).
// Colocar y cancelar los deja el motor al Gobernador o al Tesorero (`comando.rs`): solo a ellos sale la boleta y el ✕.
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
        /// <summary>Lo elegido en la boleta (sobrevive a los repintados), como `boleta` del prototipo.</summary>
        bool boletaVenta = true;
        string boletaRecurso = "madera";

        static bool MercadoActivo(Asentamiento a) => a.MercadoActivo || Internos(a).Any(e => e.Tipo == "mercado" && Activo(e));

        /// <summary>¿Coloca y cancela órdenes? El Gobernador o el Tesorero, residente.</summary>
        bool GestionaOrdenes(Asentamiento a, Heroe yo) => Reside(a, yo) && (TitularDe(a, "gobernador") == yo.Id || TitularDe(a, "tesorero") == yo.Id);

        /// <summary>Las órdenes activas de esa plaza, las más nuevas primero.</summary>
        List<OrdenMercado> OrdenesDe(string plazaId) => c.Sesion.Almacen.Todas(Entidad.CuerpoOneofCase.OrdenMercado).Select(e => e.OrdenMercado)
            .Where(o => o.AsentamientoId == plazaId && o.Estado == EstadoOrden.OrdenActiva).OrderByDescending(o => o.CreadoEnMs).ToList();

        /// <summary>Los recursos que se pueden poner en una orden (todos menos el oro), en el orden del catálogo de iconos.</summary>
        static List<string> RecursosDeOrden() => (IconosDelMundo.Lista["recurso"] as Newtonsoft.Json.Linq.JObject)?.Properties().Select(p => p.Name)
            .Where(r => r != "oro" && !r.StartsWith("_")).ToList() ?? new List<string>();

        /// <summary>Lo que falta para un instante del mundo, como lo dice el prototipo («3 h», «12 min», «40 s»).</summary>
        string FaltaPara(long hastaMs)
        {
            var ms = hastaMs - (long)AhoraMs;
            if (ms <= 0) return "ya";
            var min = ms / 60_000.0;
            return min >= 60 ? $"{Math.Round(min / 60)} h" : min >= 1 ? $"{Math.Round(min)} min" : $"{Math.Round(ms / 1000.0)} s";
        }

        static string Oro(long milesimas) => (milesimas / 1000.0).ToString("0.00");

        /// <summary>La pestaña Órdenes de tu plaza (`htmlOrdenes`): la boleta y las órdenes en pie.</summary>
        void OrdenesDeMercado(VisualElement cuerpo, Asentamiento a, Heroe yo, Action rehacer)
        {
            var gestiona = GestionaOrdenes(a, yo);
            var mias = OrdenesDe(a.Id);
            var cols = Dialogo.Caja("territorio-cols mercado-cols");
            var izq = Dialogo.Caja("territorio-col");
            izq.Add(TituloDeSeccion("Órdenes de mercado",
                "Una orden de venta ofrece recursos de tu almacén por oro; una de compra pide recursos pagando con el oro de la plaza. Cualquiera de tu Facción (o de fuera) con una columna a la puerta puede tomarla en persona. Sin precio, se usa el de mercado.",
                ChipConIcono(IconosDelMundo.Icono("recurso", "oro", 14), Hay(a, "oro").ToString(), "", "Oro de la plaza")));
            if (gestiona) izq.Add(Boleta(a, rehacer));
            cols.Add(izq);
            var der = Dialogo.Caja("territorio-col");
            der.name = "ordenes-en-pie";
            der.Add(TituloDeSeccion("En pie", null, Chip(mias.Count.ToString())));
            if (mias.Count == 0) der.Add(Dialogo.Texto("No hay órdenes en pie.", "plaza-vacio"));
            else
            {
                var fichas = Dialogo.Caja("orden-fichas");
                foreach (var o in mias) fichas.Add(FichaDeOrden(o, gestiona));
                der.Add(fichas);
            }
            cols.Add(der);
            cuerpo.Add(cols);
        }

        /// <summary>La boleta: Vender / Comprar, el almacén como inventario de casillas (lo que hay primero), cantidad, precio y el total.</summary>
        VisualElement Boleta(Asentamiento a, Action rehacer)
        {
            var boleta = Dialogo.Caja("orden-boleta " + (boletaVenta ? "venta" : "compra"));
            boleta.name = "orden-boleta";
            var tipos = Dialogo.Caja("orden-tipos");
            foreach (var venta in new[] { true, false })
            {
                var v = venta;
                var b = Dialogo.Boton(venta ? "▲ Vender" : "▼ Comprar", "orden-tipo " + (venta ? "venta" : "compra") + (boletaVenta == venta ? " elegido" : ""), () => { boletaVenta = v; rehacer(); });
                b.name = venta ? "orden-vender" : "orden-comprar";
                tipos.Add(b);
            }
            boleta.Add(tipos);
            var todos = RecursosDeOrden();
            if (!todos.Contains(boletaRecurso)) boletaRecurso = todos.FirstOrDefault() ?? "madera";
            var inventario = Dialogo.Caja("orden-inventario");
            foreach (var r in todos.Where(r => Hay(a, r) > 0).Concat(todos.Where(r => Hay(a, r) <= 0)))
            {
                var recurso = r;
                var casilla = new Button(() => { boletaRecurso = recurso; rehacer(); }) { name = "orden-recurso-" + r, tooltip = $"{CosasDelMapa.NombreDeRecurso(r)}: hay {Hay(a, r)}" };
                casilla.AddToClassList("orden-recurso");
                casilla.EnableInClassList("elegido", r == boletaRecurso);
                casilla.EnableInClassList("vacio", Hay(a, r) <= 0);
                casilla.Add(IconosDelMundo.Icono("recurso", r, 24));
                casilla.Add(Dialogo.Texto(Hay(a, r).ToString(), "orden-recurso-cifra"));
                inventario.Add(casilla);
            }
            boleta.Add(inventario);
            var elegido = Dialogo.Caja("orden-elegido");
            elegido.Add(IconosDelMundo.Icono("recurso", boletaRecurso, 26));
            elegido.Add(Dialogo.Texto(CosasDelMapa.NombreDeRecurso(boletaRecurso), "orden-elegido-nombre"));
            elegido.Add(Dialogo.Texto($"hay {Hay(a, boletaRecurso)}", "orden-elegido-hay"));
            boleta.Add(elegido);
            var campos = Dialogo.Caja("orden-campos");
            var cantidad = new IntegerField("Cantidad") { name = "orden-cantidad", value = 0 };
            var precio = new FloatField("🪙 c/u") { name = "orden-precio", value = 0 };
            campos.Add(cantidad);
            campos.Add(precio);
            boleta.Add(campos);
            var pie = Dialogo.Caja("orden-pie");
            var total = Dialogo.Texto("a precio de mercado", "orden-total");
            total.name = "orden-total";
            void Total() => total.text = cantidad.value >= 1 && precio.value > 0
                ? $"{(boletaVenta ? "cobras" : "pagas")} 🪙 {Math.Round(cantidad.value * precio.value * 100) / 100}"
                : "a precio de mercado";
            cantidad.RegisterValueChangedCallback(_ => Total());
            precio.RegisterValueChangedCallback(_ => Total());
            pie.Add(total);
            Button colocar = null;
            colocar = Dialogo.Boton(boletaVenta ? "Poner a la venta" : "Pedir", "orden-colocar", async () =>
            {
                if (cantidad.value < 1) { panelAvisos.Mostrar("Pon una cantidad de 1 o más.", true); return; }
                var o = new OrdenColocarOrdenMercado { AsentamientoId = a.Id, Tipo = boletaVenta ? "venta" : "compra", Recurso = boletaRecurso, Cantidad = cantidad.value * 1000L };
                if (precio.value > 0) o.Precio = (long)Math.Round(precio.value * 1000);
                if (await OrdenDePlaza(new Orden { ColocarOrdenMercado = o }, colocar)) rehacer();
            });
            colocar.name = "orden-colocar";
            pie.Add(colocar);
            boleta.Add(pie);
            return boleta;
        }

        /// <summary>Una orden en pie (`.orden-ficha`): el recurso con su flecha, «Vende/Compra N», la barra de lo cumplido, el precio, lo que le queda y,
        /// a quien gestiona, «✕» para cancelarla.</summary>
        VisualElement FichaDeOrden(OrdenMercado o, bool gestiona)
        {
            var venta = !o.EsCompra;
            var hecho = o.Cantidad > 0 ? Math.Min(1.0, (double)o.CantidadCumplida / o.Cantidad) : 0;
            var nombre = CosasDelMapa.NombreDeRecurso(o.Recurso);
            var ficha = Dialogo.Caja("orden-ficha " + (venta ? "venta" : "compra"));
            ficha.name = "orden-" + o.Id;
            ficha.tooltip = $"{(venta ? "Vende" : "Compra")} {nombre}: {o.CantidadCumplida / 1000} de {o.Cantidad / 1000} · {Oro(o.PrecioUnitario)} de oro c/u · caduca en {FaltaPara(o.ExpiraEnMs)}";
            var icono = Dialogo.Caja("orden-ficha-icono");
            icono.Add(IconosDelMundo.Icono("recurso", o.Recurso, 20));
            icono.Add(Dialogo.Texto(venta ? "▲" : "▼", "orden-ficha-flecha"));
            ficha.Add(icono);
            var cuerpo = Dialogo.Caja("orden-ficha-cuerpo");
            cuerpo.Add(Dialogo.Texto($"{(venta ? "Vende" : "Compra")} {o.Cantidad / 1000}", "orden-ficha-titulo"));
            var barra = Dialogo.Caja("orden-ficha-barra");
            var relleno = Dialogo.Caja("orden-ficha-barra-i");
            relleno.style.width = Length.Percent((float)(hecho * 100));
            barra.Add(relleno);
            cuerpo.Add(barra);
            cuerpo.Add(Dialogo.Texto($"{o.CantidadCumplida / 1000}/{o.Cantidad / 1000} · 🪙 {Oro(o.PrecioUnitario)} · ⏳ {FaltaPara(o.ExpiraEnMs)}", "orden-ficha-pie"));
            ficha.Add(cuerpo);
            if (gestiona)
            {
                Button cancelar = null;
                cancelar = Dialogo.Boton("✕", "orden-ficha-cancelar", async () => await OrdenDePlaza(new Orden { CancelarOrdenMercado = new OrdenCancelarOrdenMercado { OrdenId = o.Id } }, cancelar));
                cancelar.name = "cancelar-" + o.Id;
                cancelar.tooltip = "Cancelar la orden";
                ficha.Add(cancelar);
            }
            return ficha;
        }

        /// <summary>Las órdenes abiertas de una plaza ajena (`htmlMercadoDePlaza`): cada una con su cantidad y «Comprar» o «Vender», que toma el Líder de
        /// la columna con su carro. Vacío si no hay ninguna.</summary>
        void MercadoDePlaza(VisualElement cuerpo, string plazaId, Action rehacer)
        {
            var ordenes = OrdenesDe(plazaId);
            if (ordenes.Count == 0) { cuerpo.Add(Dialogo.Texto("No hay órdenes abiertas de compra ni de venta a la vista.", "plaza-vacio")); return; }
            cuerpo.Add(TituloDeSeccion("Mercado de la plaza", "Tomas la orden en persona con tu carro: pagas o cobras en oro del carro, más la comisión de la plaza. Se sirve lo que se pueda."));
            var lista = Dialogo.Caja("mercado-lista");
            foreach (var o in ordenes)
            {
                var pendiente = Math.Max(0, (o.Cantidad - o.CantidadCumplida) / 1000);
                var fila = Dialogo.Caja("mercado-orden");
                fila.name = "tomar-fila-" + o.Id;
                var texto = Dialogo.Caja("mercado-orden-texto");
                texto.Add(IconosDelMundo.ConIcono("recurso", o.Recurso, Dialogo.Texto($"{(o.EsCompra ? "Compra" : "Vende")} {CosasDelMapa.NombreDeRecurso(o.Recurso)}", "mercado-orden-nombre"), 18));
                texto.Add(Dialogo.Texto($"{pendiente} pendientes · {Oro(o.PrecioUnitario)} de oro c/u · caduca en {FaltaPara(o.ExpiraEnMs)}", "plaza-sub"));
                fila.Add(texto);
                var cantidad = new IntegerField { name = "tomar-cantidad-" + o.Id, value = (int)Math.Max(1, pendiente) };
                cantidad.AddToClassList("puerta-carga-cantidad");
                fila.Add(cantidad);
                var orden = o;
                Button tomar = null;
                tomar = Dialogo.Boton(o.EsCompra ? "Vender" : "Comprar", "boton-primario", async () =>
                {
                    if (cantidad.value < 1) { panelAvisos.Mostrar("Pon una cantidad positiva.", true); return; }
                    if (await OrdenDePlaza(new Orden { ComerciarEnPlaza = new OrdenComerciarEnPlaza { HeroeId = c.HeroeId, AsentamientoId = plazaId, OrdenId = orden.Id, Cantidad = cantidad.value * 1000L } }, tomar))
                    {
                        panelAvisos.Mostrar("Orden tomada.", false);
                        rehacer();
                    }
                });
                tomar.name = "tomar-" + o.Id;
                fila.Add(tomar);
                lista.Add(fila);
            }
            cuerpo.Add(lista);
        }

        /// <summary>Lo que cambia en la ventana del Mercado: sus órdenes (de cualquier lado).</summary>
        string FirmaDeMercado(Asentamiento a) => string.Join(",", OrdenesDe(a.Id).Select(o => o.Id + ":" + o.Version)) + $"|{boletaVenta}|{boletaRecurso}";
    }
}
