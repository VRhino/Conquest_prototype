// El fondo de refundación del campamento (paso 8 del bloque 4, tanda 2; pendiente del paso 4), calcado de BronzeAgeClient (`ui/pantallaCampamento.ts`:
// `fondo()` y su cableado): con Facción y sin ninguna plaza suya a la vista, la Caravana de Fundación se compra en el campamento entre todos los
// ciudadanos. El precio (`derivados.costo_refundacion`), lo reunido por la Facción (`fondos[f-N]`), aportar y retirar del almacén personal o del carro
// (`aportarARefundacion`, `retirarDeRefundacion`, en milésimas) y «Comprar caravana» con el precio completo (`comprarCaravanaDeRefundacion`).
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
        /// <summary>La subpestaña del panel de la taberna: «resumen» o «fondo».</summary>
        string seccionCampamento = "resumen";
        string recursoDelFondo;
        int cantidadDelFondo = 10;

        /// <summary>¿Sale el Fondo? Con Facción y sin ninguna plaza suya a la vista o en memoria (`faccionConPlaza`).</summary>
        bool HayFondo(Heroe h) => h != null && h.HasFaccionId
            && !c.Sesion.Almacen.Todas(Entidad.CuerpoOneofCase.Asentamiento).Any(e => e.Asentamiento.FaccionId == h.FaccionId);

        /// <summary>Las subpestañas del panel (`seccionesDeCampamento`): Resumen y, si toca, Fondo. Devuelve la elegida.</summary>
        string SeccionesDelCampamento(VisualElement v, Heroe h, Action repintar)
        {
            if (!HayFondo(h)) { seccionCampamento = "resumen"; return "resumen"; }
            var fila = Dialogo.Caja("pestanas campamento-secciones");
            foreach (var (id, nombre) in new[] { ("resumen", "Resumen"), ("fondo", "Fondo") })
            {
                var b = Dialogo.Boton(nombre, "pestana" + (id == seccionCampamento ? " pestana--activa" : ""), () => { seccionCampamento = id; repintar(); });
                b.name = "seccion-" + id;
                fila.Add(b);
            }
            v.Add(fila);
            return seccionCampamento;
        }

        void PintarFondo(VisualElement v, CampamentoMercenarios camp, Heroe h, Action<string> error, Action repintar)
        {
            var a = c.Sesion.Almacen;
            Dictionary<string, long> precio = null;
            try { precio = c.Datos.CosteRefundacion; } catch (FaltaDato) { }
            var fondo = camp.Fondos.TryGetValue(h.FaccionId, out var f) ? f.PorId.ToDictionary(kv => kv.Key, kv => kv.Value) : new Dictionary<string, long>();
            long Hay(string r) => fondo.TryGetValue(r, out var n) ? n / 1000 : 0;
            long Falta(string r) => precio == null || !precio.TryGetValue(r, out var n) ? 0 : Math.Max(0, n / 1000 - Hay(r));
            var caravana = a.Todas(Entidad.CuerpoOneofCase.Caravana).Select(e => e.Caravana)
                .FirstOrDefault(k => k.Tipo == TipoCaravana.CaravanaDeFundacion && k.HasOrigenCampamentoId && k.OrigenCampamentoId == camp.Id && k.FaccionId == h.FaccionId);
            var resides = camp.ResidentesIds.Contains(h.Id);
            var columna = ColumnaDelHeroe(h);
            // De dónde se aporta: el almacén personal si resides aquí; el carro de tu columna o, dentro de tu residencia y sin columna, tu carro.
            var origenes = new List<(string lado, string de, Cantidades recursos)>();
            if (resides && h.AlmacenPersonal != null) origenes.Add(("almacen", "almacén", h.AlmacenPersonal));
            if (columna?.Suministro != null) origenes.Add(("carro", "carro", columna.Suministro));
            else if (resides && h.FueraCarro != null) origenes.Add(("carro", "carro", h.FueraCarro));
            var opciones = origenes.SelectMany(o => o.recursos.PorId.Where(kv => kv.Value >= 1000).Select(kv => (o.lado, o.de, r: kv.Key, tienes: kv.Value / 1000))).ToList();

            var total = precio?.Values.Sum(x => x / 1000) ?? 0;
            var reunido = precio?.Sum(kv => Math.Min(kv.Value / 1000, Hay(kv.Key))) ?? 0;
            var completo = precio != null && precio.Keys.All(r => Falta(r) == 0);
            v.Add(ConAyuda(Dialogo.Texto($"Precio de la caravana{(precio != null ? $" · reunido {reunido} / {total}" : "")}", "heroe-sub fondo-titulo"),
                "La Caravana de Fundación se compra aquí entre todos los ciudadanos de tu Facción: cada uno aporta lo que quiere a un fondo común. Lo tuyo se puede retirar mientras no se gaste. El fondo se llena con lo de tu almacén personal o del carro que llevas aquí (el de tu columna o, si resides aquí, tu propio carro). Cuando el fondo está completo, la caravana espera aquí dentro: sal como ejército y, al partir, el Líder elige llevársela; luego funda donde quieras."));
            if (precio != null)
            {
                var barra = Dialogo.Caja("fondo-barra fondo-barra-total");
                var i = Dialogo.Caja("fondo-barra-i");
                i.style.width = Length.Percent(total > 0 ? 100f * reunido / total : 0);
                barra.Add(i);
                v.Add(barra);
                foreach (var (r, milesimas) in precio)
                {
                    var n = milesimas / 1000;
                    var hay = Hay(r);
                    var listo = hay >= n;
                    var fila = Dialogo.Caja("fondo-fila" + (listo ? " listo" : ""));
                    fila.name = "fondo-" + r;
                    fila.Add(IconosDelMundo.ConIcono("recurso", r, Dialogo.Texto(CosasDelMapa.NombreDeRecurso(r), "fondo-recurso"), 16));
                    var b = Dialogo.Caja("fondo-barra");
                    var bi = Dialogo.Caja("fondo-barra-i");
                    bi.style.width = Length.Percent(n > 0 ? Math.Min(100f, 100f * hay / n) : 0);
                    b.Add(bi);
                    fila.Add(b);
                    fila.Add(Dialogo.Texto($"{Math.Min(hay, n)} / {n}", "fondo-cifra"));
                    fila.Add(Dialogo.Texto(listo ? "✓ completo" : $"faltan {n - hay}", "fondo-falta"));
                    v.Add(fila);
                }
                var sobrante = fondo.Where(kv => kv.Value > 0 && !precio.ContainsKey(kv.Key)).ToList();
                if (sobrante.Count > 0) v.Add(Dialogo.Texto($"Además en el fondo: {string.Join(", ", sobrante.Select(kv => $"{kv.Value / 1000} de {CosasDelMapa.NombreDeRecurso(kv.Key)}"))}.", "dialogo-texto"));
            }

            v.Add(Dialogo.Texto("Aportar", "heroe-sub fondo-titulo"));
            DropdownField recurso = null;
            IntegerField cantidad = null;
            if (opciones.Count == 0) v.Add(Dialogo.Texto("No tienes nada que aportar.", "dialogo-texto"));
            else
            {
                var etiquetas = opciones.Select(o => $"{CosasDelMapa.NombreDeRecurso(o.r)}, del {o.de} (tienes {o.tienes}{(Falta(o.r) > 0 ? $", faltan {Falta(o.r)}" : "")})").ToList();
                var elegida = Math.Max(0, opciones.FindIndex(o => o.lado + ":" + o.r == recursoDelFondo));
                var fila = Dialogo.Caja("campamento-fila fondo-aportar");
                recurso = new DropdownField(etiquetas, elegida) { name = "fondo-recurso" };
                recurso.AddToClassList("fondo-combo");
                recurso.RegisterValueChangedCallback(_ => recursoDelFondo = opciones[recurso.index].lado + ":" + opciones[recurso.index].r);
                recursoDelFondo = opciones[elegida].lado + ":" + opciones[elegida].r;
                fila.Add(recurso);
                cantidad = new IntegerField { name = "fondo-cantidad", value = cantidadDelFondo };
                cantidad.AddToClassList("fondo-cantidad");
                cantidad.RegisterValueChangedCallback(e => cantidadDelFondo = e.newValue);
                fila.Add(cantidad);
                var loQueFalta = Dialogo.Boton("Lo que falta", "boton-secundario", () =>
                {
                    var o = opciones[recurso.index];
                    var falta = Falta(o.r);
                    cantidad.value = (int)Math.Max(1, falta > 0 ? Math.Min(falta, o.tienes) : o.tienes);
                });
                loQueFalta.name = "fondo-lo-que-falta";
                loQueFalta.tooltip = "Rellena la cantidad con lo que falta de ese material (o con lo que tienes, si es menos)";
                fila.Add(loQueFalta);
                v.Add(fila);
            }
            var acciones = Dialogo.Caja("dialogo-botones fondo-acciones");
            Orden Movimiento(bool aportar)
            {
                var o = opciones.Count > 0 && recurso != null ? opciones[recurso.index] : default;
                var r = o.r ?? precio?.Keys.FirstOrDefault() ?? "madera";
                var lado = o.lado ?? (resides ? "almacen" : "carro");
                var n = (long)(cantidad?.value ?? 0) * 1000;
                return aportar ? new Orden { AportarARefundacion = new OrdenAportarARefundacion { Recurso = r, Cantidad = n, Lado = lado } }
                    : new Orden { RetirarDeRefundacion = new OrdenRetirarDeRefundacion { Recurso = r, Cantidad = n, Lado = lado } };
            }
            Button aportarB = null, retirarB = null, comprarB = null;
            aportarB = Dialogo.Boton("Aportar", "boton-secundario", async () => await OrdenDelPanel(Movimiento(true), error, repintar, aportarB));
            aportarB.name = "fondo-aportar";
            aportarB.SetEnabled(opciones.Count > 0);
            retirarB = Dialogo.Boton("Retirar", "boton-secundario", async () => await OrdenDelPanel(Movimiento(false), error, repintar, retirarB));
            retirarB.name = "fondo-retirar";
            retirarB.SetEnabled(fondo.Values.Any(x => x > 0));
            comprarB = Dialogo.Boton("Comprar caravana", "boton-primario", async () =>
                await OrdenDelPanel(new Orden { ComprarCaravanaDeRefundacion = new OrdenComprarCaravanaDeRefundacion() }, error, repintar, comprarB));
            comprarB.name = "fondo-comprar";
            comprarB.SetEnabled(caravana == null && completo);
            acciones.Add(aportarB);
            acciones.Add(retirarB);
            acciones.Add(comprarB);
            v.Add(acciones);
            if (caravana == null && precio != null && !completo)
                v.Add(Dialogo.Texto($"Aún no se puede comprar: faltan {string.Join(", ", precio.Keys.Where(r => Falta(r) > 0).Select(r => $"{Falta(r)} de {CosasDelMapa.NombreDeRecurso(r)}"))}.", "dialogo-texto"));
            if (caravana != null)
                v.Add(Dialogo.Texto("Caravana de Fundación lista" + (caravana.Estado == EstadoCaravana.CaravanaDisponible
                    ? ": espera aquí dentro hasta que un ejército de tu Facción se la lleve al salir"
                    : caravana.HasTitularId && caravana.TitularId == h.Id ? ", y la llevas tú" : "") + ".", "dialogo-texto fondo-lista"));
        }
    }
}
