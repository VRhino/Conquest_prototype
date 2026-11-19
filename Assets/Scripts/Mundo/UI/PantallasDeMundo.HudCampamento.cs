// El HUD del interior del campamento (Víctor, 2026-11-19), calcado de BronzeAgeClient (`hudInterior.ts` y la pantalla del campamento de `main.ts`):
// el riel de abajo, con una parte por botón en el orden del prototipo (`seccionesDeCampamento`), y el minimapa con la planta del campamento
// (`svgPlanoCampamento`). Las partes son las ventanas que Conquest ya tiene en el campamento: Tropa (con su pestaña Mercenarios), Mercado y, con
// Facción, Taberna (con el Fondo); más Resumen y la Armería de solo tienda del prototipo (duda 40a).
using System;
using System.Collections.Generic;
using System.Linq;
using BronzeAge.Contract.V1;
using ConquestTactics.Mundo.Mapa;
using UnityEngine;
using UnityEngine.UIElements;

namespace ConquestTactics.Mundo.UI
{
    public partial class PantallasDeMundo
    {
        /// <summary>El riel del campamento (`htmlRielInterior`): un botón cuadrado por parte, con su icono y su nombre al pasar el ratón.</summary>
        void RielDelCampamento(VisualElement riel, CampamentoMercenarios camp)
        {
            if (riel == null) return;
            riel.Clear();
            var conFaccion = HeroeAhora()?.HasFaccionId == true;
            var partes = new List<(string id, string titulo, string edificio, Action abrir)>
            {
                ("resumen", "Resumen", "resumen", () => PanelResumen(camp.Id)),
                ("tropa", "Tropa", "barracon", () => PanelTropa(camp.Id)),
                ("mercado", "Mercado", "mercado", () => PanelMercado(camp.Id)),
                ("armeria", "Armería", "armeria", () => PanelArmeriaDelCampamento(camp.Id)),
            };
            if (conFaccion) partes.Add(("taberna", "Taberna", "taberna", () => PanelTaberna(camp.Id)));
            foreach (var (id, titulo, edificio, abrir) in partes)
            {
                var b = new Button(abrir) { name = "riel-" + id };
                b.AddToClassList("plaza-riel-boton");
                var icono = IconosDelMundo.Icono(FamiliaDeIcono(edificio), edificio, 30, IconosDelMundo.OroClaro);
                if (icono != null) b.Add(icono);
                var etiqueta = Dialogo.Texto(titulo.ToUpperInvariant(), "plaza-riel-etiqueta");
                etiqueta.pickingMode = PickingMode.Ignore;
                b.Add(etiqueta);
                riel.Add(b);
            }
        }

        /// <summary>El Resumen no es un edificio: su icono es el de la parte (`hudInterior.ts`).</summary>
        static string FamiliaDeIcono(string id) => id == "resumen" ? "parte" : "edificio";

        /// <summary>El Resumen como parte propia del riel (en el prototipo va primero).</summary>
        void PanelResumen(string campId) => PanelDelCampamento("panel-resumen", "RESUMEN", "resumen", (v, error, repintar) =>
        {
            var camp = CampamentoActual(campId);
            var h = HeroeDelCampamento();
            if (camp != null && h != null) PintarResumen(v, camp, h, campId, error, repintar);
        });

        /// <summary>La Armería del campamento (`htmlArmeriaDeCampamento`): solo compra y vende lo que no pide tecnología; es la de la plaza con una
        /// plaza sin metales abiertos, a pantalla completa.</summary>
        void PanelArmeriaDelCampamento(string campId) => PanelDelCampamento("panel-armeria", "ARMERÍA", "armeria", (v, _, repintar) =>
        {
            var h = HeroeDelCampamento();
            if (h == null) return;
            if (Objetos == null) { v.Add(Dialogo.Texto("Cargando el catálogo de objetos…", "plaza-nota")); return; }
            v.Add(Dialogo.Texto("La Armería del campamento solo compra y vende: armaduras de recluta, armas y monturas que no piden tecnología. Lo mejor está en las plazas.", "plaza-nota"));
            TiendaCompleta(v, new Asentamiento { Id = campId }, h, repintar);
        }, completa: true);

        static readonly Color ColorEmpalizada = new Color32(0x6b, 0x4a, 0x29, 0xff), ColorCalle = new Color32(0xb3, 0x9b, 0x72, 0xff),
            ColorSuelo = new Color32(0x8f, 0xb0, 0x63, 0xff), ColorEdificio = new Color32(0xd8, 0xb8, 0x78, 0xff), ColorPuerta = new Color32(0xf1, 0xdc, 0xae, 0xff);

        /// <summary>El minimapa con la planta del campamento (`svgPlanoCampamento`): el suelo, la empalizada y su puerta, las calles y cada edificio con
        /// su nombre. Los rectángulos van por su centro en unidades locales (como los monta el interior).</summary>
        void MinimapaDelCampamento(VisualElement caja, CampamentoMercenarios camp)
        {
            if (caja == null) return;
            caja.Clear();
            var piezas = camp.Empalizada.Select(r => (r, "empalizada")).Concat(camp.EmpalizadaPuerta.Select(r => (r, "puerta")))
                .Concat((camp.Trazado?.Calles ?? Enumerable.Empty<RectLocal>()).Select(r => (r, "calle"))).ToList();
            var edificios = camp.Planta.Select(e => (x: e.Posicion.X / 1000.0, y: e.Posicion.Y / 1000.0, ancho: (double)e.Ancho, alto: (double)e.Alto, tipo: e.Tipo)).ToList();
            var todos = piezas.Select(p => (x0: (double)p.r.X, y0: (double)p.r.Y, x1: (double)(p.r.X + p.r.Ancho), y1: (double)(p.r.Y + p.r.Alto)))
                .Concat(edificios.Select(e => (x0: e.x - e.ancho / 2, y0: e.y - e.alto / 2, x1: e.x + e.ancho / 2, y1: e.y + e.alto / 2))).ToList();
            if (todos.Count == 0) { Ver(caja, false); return; }
            double minX = todos.Min(t => t.x0), minY = todos.Min(t => t.y0), maxX = todos.Max(t => t.x1), maxY = todos.Max(t => t.y1);
            const float lado = 206, margen = 10;
            var escala = (float)((lado - 2 * margen) / Math.Max(1e-6, Math.Max(maxX - minX, maxY - minY)));
            var ox = (float)(margen + ((lado - 2 * margen) - (maxX - minX) * escala) / 2);
            var oy = (float)(margen + ((lado - 2 * margen) - (maxY - minY) * escala) / 2);
            Rect A(double x0, double y0, double ancho, double alto) => new Rect(ox + (float)((x0 - minX) * escala), oy + (float)((y0 - minY) * escala), (float)(ancho * escala), (float)(alto * escala));
            caja.generateVisualContent += ctx =>
            {
                var p2 = ctx.painter2D;
                void Rellenar(Rect r, Color color)
                {
                    p2.fillColor = color;
                    p2.BeginPath();
                    p2.MoveTo(new Vector2(r.xMin, r.yMin)); p2.LineTo(new Vector2(r.xMax, r.yMin)); p2.LineTo(new Vector2(r.xMax, r.yMax)); p2.LineTo(new Vector2(r.xMin, r.yMax));
                    p2.ClosePath();
                    p2.Fill();
                }
                Rellenar(A(minX, minY, maxX - minX, maxY - minY), ColorSuelo);
                foreach (var (r, tipo) in piezas.Where(p => p.Item2 == "calle")) Rellenar(A(r.X, r.Y, r.Ancho, r.Alto), ColorCalle);
                foreach (var (r, tipo) in piezas.Where(p => p.Item2 != "calle")) Rellenar(A(r.X, r.Y, r.Ancho, r.Alto), tipo == "puerta" ? ColorPuerta : ColorEmpalizada);
                foreach (var e in edificios)
                {
                    var r = A(e.x - e.ancho / 2, e.y - e.alto / 2, e.ancho, e.alto);
                    Rellenar(r, ColorEdificio);
                    p2.strokeColor = ColorEmpalizada;
                    p2.lineWidth = 1;
                    p2.BeginPath();
                    p2.MoveTo(new Vector2(r.xMin, r.yMin)); p2.LineTo(new Vector2(r.xMax, r.yMin)); p2.LineTo(new Vector2(r.xMax, r.yMax)); p2.LineTo(new Vector2(r.xMin, r.yMax));
                    p2.ClosePath();
                    p2.Stroke();
                }
            };
            // El nombre de los edificios con zona (taberna, mercado, militar…), en su centro.
            foreach (var e in edificios.Where(e => ZonaDePieza(e.tipo) != null))
            {
                var r = A(e.x - e.ancho / 2, e.y - e.alto / 2, e.ancho, e.alto);
                var n = Dialogo.Texto(Pieza(e.tipo), "interior-minimapa-nombre");
                n.pickingMode = PickingMode.Ignore;
                n.style.left = r.center.x;
                n.style.top = r.center.y;
                caja.Add(n);
            }
            caja.MarkDirtyRepaint();
        }
    }
}
