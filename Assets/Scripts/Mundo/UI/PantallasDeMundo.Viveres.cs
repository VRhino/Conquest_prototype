// La barra de víveres del mundo (Víctor, 2026-11-18), calcada de BronzeAgeClient: `pintarComida` de `main.ts` y el indicador de velocidad de
// `ui/velocidadColumna.ts`. Abajo en el centro, debajo del riel, solo con tu columna fuera:
// - «» Marcha forzada»: puesta (roja) o quitada; solo el Líder la cambia (`marchaForzada`).
// - La velocidad de la columna en cuatro flechas del color de su ritmo frente a la infantería ligera; la cifra y quién marca el paso, en su tooltip.
// - 🌾 Víveres: su barra (roja con menos de una cuarta parte) y «víveres / lo que cabe» (`capacidadViveresPorHeroe` por héroe de la columna).
// - «+ desde el carro»: solo el Líder, con trigo en el carro y sitio en los víveres; abre el selector y «Pasar a víveres» (`pasarAViveres`).
using System;
using System.Collections.Generic;
using System.Linq;
using BronzeAge.Contract.V1;
using Newtonsoft.Json.Linq;
using UnityEngine.UIElements;

namespace ConquestTactics.Mundo.UI
{
    public partial class PantallasDeMundo
    {
        /// <summary>Los víveres de verdad: con la columna fuera, los de la columna (`Ejercito.viveres` y `capacidad_viveres`, la suma de sus héroes); sin
        /// ella, los del héroe. Fuera, `Heroe.viveres` no los lleva (llegaba 0 y la barra salía vacía; Víctor, 2026-11-18). En unidades, redondeados
        /// hacia arriba como pide el contrato (la unidad que se está comiendo sigue hasta acabarse). También el ritmo, para estimar cuánto duran.</summary>
        (double viveres, double capacidad, Ritmo ritmo) ViveresDe(Heroe h, Ejercito col)
        {
            var ritmo = col?.Viveres ?? h?.Viveres;
            var viveres = Math.Ceiling(Instante.Valor(ritmo, c.Sesion.AhoraMundo) / 1000);
            var capacidad = col != null && col.CapacidadViveres > 0 ? col.CapacidadViveres / 1000.0
                : c.Datos.Numero("documentos.Movimiento_Ejercitos.LOGISTICA.capacidadViveresPorHeroe") * Math.Max(1, col?.Participantes.Count ?? 1);
            return (viveres, capacidad, ritmo);
        }

        string firmaDeViveres;
        bool selectorDeViveres;
        int cantidadDeViveres = 1;

        /// <summary>La barra de víveres, si tu columna está fuera: se rehace cuando cambian tu columna, tu héroe o los víveres (al segundo).</summary>
        void PintarViveres(VisualElement v)
        {
            var caja = v.Q("mapa-comida");
            if (caja == null) return;
            var col = MiColumna();
            var h = HeroeAhora();
            if (col == null || h == null) { Ver(caja, false); selectorDeViveres = false; firmaDeViveres = null; return; }
            var (viveres, capacidad, _) = ViveresDe(h, col);
            var hueco = Math.Max(0, Math.Floor(capacidad - viveres));
            var enElCarro = Math.Floor(Milesimas(col.Suministro, "trigo"));
            var maximo = (int)Math.Min(enElCarro, hueco);
            var esLider = col.LiderId == h.Id;
            var motivo = !esLider ? "Solo el Líder de la columna pasa trigo del carro a los víveres." : enElCarro < 1 ? "El carro no lleva trigo." : hueco < 1 ? "Tus víveres están llenos." : "";
            if (motivo != "") selectorDeViveres = false;
            var firma = $"{col.Version}|{h.Version}|{viveres}|{capacidad}|{enElCarro}|{selectorDeViveres}|{string.Join(",", EscuadrasDeLaColumna().Select(s => s.Version))}";
            if (firma == firmaDeViveres) return;
            firmaDeViveres = firma;
            cantidadDeViveres = Math.Max(1, Math.Min(cantidadDeViveres, Math.Max(1, maximo)));
            var fraccion = capacidad > 0 ? Math.Min(1, viveres / capacidad) : 0;
            caja.Clear();
            Ver(caja, true);
            caja.EnableInClassList("poca", fraccion < 0.25);
            caja.tooltip = "Víveres: el trigo que come tu columna: en marcha, media ración; acampada, una décima parte de eso. Sin víveres, la moral cae y la tropa deserta. Siempre van contigo.";

            // Marcha forzada, a la izquierda: puesta o quitada; solo el Líder la cambia.
            var forzada = col.MarchaForzada;
            var botonForzada = Dialogo.Boton("» Marcha forzada", "mapa-forzada" + (forzada ? " activa" : ""), async () =>
            {
                firmaDeViveres = null;
                if (mapa != null) await mapa.AlternarMarchaForzada();
            });
            botonForzada.name = "viveres-forzada";
            botonForzada.SetEnabled(esLider);
            botonForzada.tooltip = $"Marcha forzada {(forzada ? "puesta" : "quitada")}: ×1,5 de velocidad, y la tropa se cansa (la moral baja). {(esLider ? "Pulsa para " + (forzada ? "quitarla" : "ponerla") + "." : "Solo el Líder la cambia.")}";
            caja.Add(botonForzada);
            caja.Add(IndicadorDeVelocidad(col));

            caja.Add(IconosDelMundo.ConIcono("glifo", "viveres", Dialogo.Texto("Víveres", ""), 16));
            var barra = Dialogo.Caja("mapa-comida-barra");
            var lleno = Dialogo.Caja("mapa-comida-barra-i");
            lleno.style.width = Length.Percent((float)(fraccion * 100));
            barra.Add(lleno);
            caja.Add(barra);
            var cifra = Dialogo.Texto($"{viveres:0} / {capacidad:0}", "mapa-comida-cifra");
            cifra.name = "viveres-cifra";
            caja.Add(cifra);
            var abrir = Dialogo.Boton("＋ desde el carro", "boton-secundario mapa-comida-carro", () => { selectorDeViveres = !selectorDeViveres; cantidadDeViveres = Math.Max(1, maximo); firmaDeViveres = null; PintarViveres(v); });
            abrir.name = "viveres-desde-el-carro";
            abrir.SetEnabled(motivo == "");
            abrir.tooltip = motivo != "" ? motivo : "Pasa trigo del carro a tus víveres: lo que cabe.";
            caja.Add(abrir);

            if (!selectorDeViveres) return;
            var selector = Dialogo.Caja("mapa-comida-selector");
            selector.name = "viveres-selector";
            selector.Add(Dialogo.Texto($"Trigo del carro: {enElCarro:0} · cabe {hueco:0}", ""));
            var deslizador = new SliderInt(1, Math.Max(1, maximo)) { value = cantidadDeViveres, name = "viveres-barra" };
            var campo = new IntegerField { value = cantidadDeViveres, name = "viveres-cantidad" };
            campo.AddToClassList("base-cantidad");
            deslizador.RegisterValueChangedCallback(e => { cantidadDeViveres = e.newValue; campo.SetValueWithoutNotify(e.newValue); });
            campo.RegisterValueChangedCallback(e => { cantidadDeViveres = Math.Max(1, Math.Min(maximo, e.newValue)); deslizador.SetValueWithoutNotify(cantidadDeViveres); });
            selector.Add(deslizador);
            selector.Add(campo);
            var error = Dialogo.Texto("", "error oculto");
            error.name = "viveres-error";
            Button pasar = null;
            pasar = Dialogo.Boton("Pasar a víveres", "boton-primario", async () =>
            {
                pasar.SetEnabled(false);
                var cantidad = Math.Max(1, Math.Min(maximo, cantidadDeViveres));
                if (await OrdenConError(new Orden { PasarAViveres = new OrdenPasarAViveres { Cantidad = cantidad * 1000L } }, m => Poner(error, m))) { selectorDeViveres = false; firmaDeViveres = null; }
                else pasar.SetEnabled(true);
            });
            pasar.name = "viveres-pasar";
            selector.Add(pasar);
            selector.Add(error);
            caja.Add(selector);
        }

        /// <summary>La velocidad de la columna en flechas (`htmlIndicadorVelocidad`), con las reglas del motor (`velocidad_de_columna`): la pieza más
        /// lenta entre sus escuadras con soldados y sus caravanas (la del animal más lento de sus carros o la de su tipo en `CARAVANA_CATALOGO`; la de
        /// Fundación, `construccion`); sin ninguna, la del héroe solo; con marcha forzada, × `factorVelocidadMarchaForzada`. El color compara con la
        /// infantería ligera (la velocidad más repetida del catálogo): rojo más lenta, blanco igual, azul más rápida.</summary>
        VisualElement IndicadorDeVelocidad(Ejercito col)
        {
            var catalogo = c.Datos.TropasReclutables?.OfType<JObject>().ToList() ?? new List<JObject>();
            var caravanas = c.Datos.Bloque("Comercio_Caravanas", "CARAVANA_CATALOGO") as JObject;
            var animales = c.Datos.Bloque("Comercio_Caravanas", "ANIMAL_CATALOGO") as JObject;
            var piezas = new List<(string nombre, double velocidad)>();
            // Las escuadras con soldados de todos los de la columna (las tuyas y las que llevan los demás héroes).
            foreach (var x in col.Participantes)
                foreach (var t in TropaDe(col, x.HeroeId, HeroeAhora()).Where(t => t.cantidad > 0))
                    if (catalogo.FirstOrDefault(j => (string)j["id"] == t.tropaId) is JObject j && (double?)j["velocidad"] is double vt && vt > 0) piezas.Add(((string)j["nombre"] ?? t.tropaId, vt));
            foreach (var id in col.CaravanasAdjuntasIds)
            {
                var k = c.Sesion.Almacen.Buscar(TipoEntidad.Caravana, id)?.Caravana;
                if (k == null) continue;
                var conAnimal = k.Carros.Where(r => r.HasAnimal).Select(r => (double?)animales?[r.Animal]?["velocidad"] ?? double.PositiveInfinity).ToList();
                var vk = conAnimal.Count > 0 ? conAnimal.Min() : (double?)caravanas?[k.Tipo == TipoCaravana.CaravanaDeFundacion ? "construccion" : "comercial"]?["velocidad"] ?? 0;
                if (vk > 0 && !double.IsInfinity(vk)) piezas.Add((k.Tipo == TipoCaravana.CaravanaDeFundacion ? "la Caravana de Fundación" : "una caravana enganchada", vk));
            }
            if (col.HasCaravanaFundacionId && (double?)caravanas?["construccion"]?["velocidad"] is double vf && vf > 0) piezas.Add(("la Caravana de Fundación", vf));
            var lenta = piezas.Count > 0 ? piezas.OrderBy(p => p.velocidad).First() : ((string)null, 0);
            var valor = (lenta.nombre != null ? lenta.velocidad : c.Datos.Numero("documentos.Movimiento_Ejercitos.MOVIMIENTO.velocidadJugador"))
                * (col.MarchaForzada ? c.Datos.Numero("documentos.Movimiento_Ejercitos.MOVIMIENTO.factorVelocidadMarchaForzada") : 1);
            var velocidades = catalogo.Select(t => (double?)t["velocidad"]).Where(x => x > 0).Select(x => x.Value).ToList();
            double? ligera = velocidades.Count == 0 ? (double?)null : velocidades.GroupBy(x => x).OrderByDescending(g => g.Count()).ThenByDescending(g => g.Key).First().Key;
            var ritmo = ligera == null || Math.Abs(valor - ligera.Value) < 1e-6 ? "ligera" : valor < ligera ? "lenta" : "rapida";
            var flechas = ligera == null ? 0 : ritmo == "ligera" ? 3 : ritmo == "rapida" ? 4
                : velocidades.Where(x => x < ligera).Distinct().OrderByDescending(x => x).FirstOrDefault() is double media && media > 0 && valor >= media ? 2 : 1;
            var textoRitmo = ritmo == "lenta" ? "más lenta que la infantería ligera" : ritmo == "rapida" ? "más rápida que la infantería ligera" : "al paso de la infantería ligera";
            var caja = Dialogo.Caja("mapa-velocidad ritmo-" + ritmo);
            caja.name = "viveres-velocidad";
            caja.tooltip = $"Velocidad: {valor.ToString("0.#", Es)} u/min, {textoRitmo}{(ligera != null ? $" ({ligera.Value.ToString("0.#", Es)})" : "")}. Marca el paso {lenta.nombre ?? "el héroe solo"}{(col.MarchaForzada ? ", con marcha forzada" : "")}.";
            for (int i = 0; i < 4; i++) { var f = Dialogo.Texto("›", ""); f.style.opacity = i < flechas ? 1 : 0.22f; caja.Add(f); }
            return caja;
        }
    }
}
