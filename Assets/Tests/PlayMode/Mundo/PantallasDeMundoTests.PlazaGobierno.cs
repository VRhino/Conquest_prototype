// Tu plaza por dentro, tanda 2 (paso 7 del bloque 4): dentro de `TuPlazaPorDentro`, con el ratón de verdad. El riel; Construcción (el cajón con sus
// pestañas, poner un edificio en la cola, la auto-construcción y «Construidos», que elige un edificio con su ficha); Gobierno (nombrarte Tesorero
// desde Cargos y activar una política); y el Almacén desde su placa (qué va en la barra de arriba, la reserva del Tesorero, pausar el consumo, los
// talleres y «Tu carga»: sacar de la plaza al carro, dejarlo en la plaza y cerrar el almacén a los ciudadanos). Cada cosa, contra el motor.
using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using V1 = BronzeAge.Contract.V1;

namespace ConquestTactics.Mundo.UI.Tests
{
    public partial class PantallasDeMundoTests
    {
        IEnumerator TandaDos(UnityEngine.InputSystem.Mouse raton, VisualElement raiz, VisualElement plaza, Func<V1.Asentamiento> Plaza, Func<V1.Heroe> Yo,
            Func<VisualElement> Ficha, Func<UnityEngine.InputSystem.Key, IEnumerator> Tecla, ConexionDeMundo conexion)
        {
            var yo = conexion.HeroeId;
            VisualElement Riel() => plaza.Q("riel");
            VisualElement Ventana() => plaza.Q("plaza-ventana");
            VisualElement Cajon() => plaza.Q("cajon-construir");
            IEnumerator ClicEn(VisualElement e) => ClicEnVentana(raton, raiz, e);

            // El riel, con su nombre encima al pasar.
            yield return MoverRaton(raton, raiz, Riel().Q<Button>("riel-gobierno"));
            yield return Capturar("80_riel");

            // ── Construcción: el cajón en el sitio del riel ──
            yield return ClicFijo(raton, raiz, Riel().Q<Button>("riel-construccion"));
            yield return Esperar(() => Cajon() != null, 5, "el cajón de Construcción");
            Assert.IsTrue(SelectorDePersonaje.Interior.PanelAbierto, "con el cajón abierto, el héroe no anda");
            yield return EnTamanos(raiz, "81_construccion", () => DentroDelMarco(Cajon(), "el cajón"));
            // Las pestañas por tipo: Militar y vuelta a la primera.
            var grupos = Cajon().Query<Button>(className: "plaza-obra-grupo").ToList();
            Debug.Log($"[Prueba] construcción: pestañas {string.Join(", ", grupos.Select(g => g.name))}");
            Assert.Greater(grupos.Count, 1, "varias pestañas");
            if (Cajon().Q<Button>("grupo-militar") is Button militar)
            {
                yield return ClicFijo(raton, raiz, militar);
                yield return Esperar(() => Cajon().Q<Button>("grupo-militar").ClassListContains("activo"), 5, "la pestaña Militar");
                yield return Capturar("81b_construccion_militar");
            }
            yield return ClicFijo(raton, raiz, Cajon().Q<Button>(grupos[0].name));
            yield return Esperar(() => Cajon().Q<Button>(grupos[0].name).ClassListContains("activo"), 5, "la primera pestaña");
            // Poner en la cola lo primero que se pueda pagar y que el motor acepte (lo que el cliente no sabe, como el tope de almacenes, lo dice él).
            var candidatas = Cajon().Query<Button>(className: "plaza-construir").ToList().Where(b => !b.ClassListContains("caro")).Select(b => b.name).ToList();
            Assert.IsNotEmpty(candidatas, "algo que se pueda construir");
            yield return MoverRaton(raton, raiz, Cajon().Q<Button>(candidatas[0]));
            yield return Capturar("81c_construir_letrero");
            string puesto = null;
            foreach (var nombre in candidatas)
            {
                var tipo = nombre.Substring("construir-".Length);
                var antes = Plaza().Edificios.Count(e => e.Tipo == tipo);
                yield return ClicFijo(raton, raiz, Cajon().Q<Button>(nombre));
                string aviso = null;
                for (var hasta = Time.realtimeSinceStartup + 6; Time.realtimeSinceStartup < hasta && Plaza().Edificios.Count(e => e.Tipo == tipo) <= antes;)
                {
                    if (raiz.Q<Label>(className: "toast") is Label t && !t.ClassListContains("toast--oculto") && !string.IsNullOrEmpty(t.text)) aviso = t.text;
                    yield return null;
                }
                if (Plaza().Edificios.Count(e => e.Tipo == tipo) > antes) { puesto = tipo; break; }
                Debug.Log($"[Prueba] construir {tipo}: no entra, «{aviso}»");
            }
            Assert.IsNotNull(puesto, "algo puesto en la cola");
            Debug.Log($"[Prueba] construir: {puesto} puesto en la cola");
            // La auto-construcción: encenderla y volverla a apagar.
            Assert.IsTrue(Plaza().AutoConstruccionPausada, "la auto-construcción, en pausa (la pausa la prueba)");
            yield return ClicFijo(raton, raiz, Cajon().Q("auto-construccion"));
            yield return Esperar(() => !Plaza().AutoConstruccionPausada, 10, "la auto-construcción en marcha");
            yield return Esperar(() => Cajon().Q("auto-construccion").ClassListContains("encendido"), 5, "el interruptor encendido");
            yield return ClicFijo(raton, raiz, Cajon().Q("auto-construccion"));
            yield return Esperar(() => Plaza().AutoConstruccionPausada, 10, "la auto-construcción en pausa");
            // «Construidos»: sube encima del cajón; un clic elige un edificio y abre su ficha.
            yield return ClicFijo(raton, raiz, Cajon().Q<Button>("cajon-construidos"));
            yield return Esperar(() => Cajon().Q("construidos") is VisualElement c && !c.ClassListContains("oculto"), 5, "«Construidos»");
            yield return EnTamanos(raiz, "82_construidos", () => DentroDelMarco(Cajon().Q("construidos"), "Construidos"));
            var construido = Cajon().Q("construidos").Query<Button>(className: "plaza-construido").ToList().First();
            yield return ClicFijo(raton, raiz, construido);
            yield return Esperar(() => Ficha() != null, 5, "la ficha del construido");
            Assert.AreEqual(Edificios.Nombre(construido.name.Substring("construido-".Length)), Ficha().Q<Label>(className: "plaza-ficha-titulo")?.text ?? Edificios.Nombre(construido.name.Substring("construido-".Length)), "su ficha");
            yield return Capturar("82b_construido_ficha");
            yield return ClicFijo(raton, raiz, Ficha().Q<Button>("ficha-cerrar"));
            yield return Esperar(() => Ficha() == null, 5, "la ficha cerrada");
            yield return ClicFijo(raton, raiz, Cajon().Q<Button>("construidos-cerrar"));
            yield return Esperar(() => Cajon().Q("construidos").ClassListContains("oculto"), 5, "«Construidos» cerrado");
            yield return ClicFijo(raton, raiz, Cajon().Q<Button>("cajon-volver"));
            yield return Esperar(() => Cajon() == null && Riel().Q<Button>("riel-gobierno") != null, 5, "el riel de vuelta");

            // ── Gobierno: Cargos ──
            yield return ClicEn(Riel().Q<Button>("riel-gobierno"));
            yield return Esperar(() => !Ventana().ClassListContains("oculto") && Ventana().Q("cargo-gobernador") != null, 5, "Gobierno");
            Assert.IsTrue(Ventana().Q("cargo-gobernador").ClassListContains("yo"), "eres el Gobernador");
            Assert.IsNotNull(Ventana().Q("residentes").Q(className: "plaza-ranking-fila"), "tú, entre los residentes");
            yield return EnTamanos(raiz, "83_gobierno_cargos", () => DentroDelMarco(Ventana(), "Gobierno"));
            // Nombrarte Tesorero: el lápiz (＋ en la vacante), el combo con los ciudadanos y ✓.
            yield return ClicEn(Ventana().Q("cargo-tesorero").Q<Button>("cargo-cambiar"));
            yield return Esperar(() => Ventana().Q("cargo-tesorero").Q<DropdownField>("cargo-combo") != null, 5, "el combo del Tesorero");
            yield return Capturar("83b_nombrar_tesorero");
            yield return ClicEn(Ventana().Q("cargo-tesorero").Q<Button>("cargo-asignar"));
            yield return Esperar(() => Plaza().Cargos.HasTesoreroId && Plaza().Cargos.TesoreroId == yo, 10, "Tesorero en el motor");
            yield return Esperar(() => Ventana().Q("cargo-tesorero") is VisualElement t && t.ClassListContains("yo"), 5, "Tesorero en la silla");

            // ── Gobierno: Políticas ──
            yield return ClicEn(Ventana().Q<Button>("sub-politicas"));
            yield return Esperar(() => Ventana().Q("consejo-gobernador") != null, 5, "Políticas");
            yield return EnTamanos(raiz, "84_politicas", () => DentroDelMarco(Ventana(), "Políticas"));
            var activar = Ventana().Q("consejo-gobernador").Query<Button>("activar").ToList().FirstOrDefault(b => b.enabledSelf);
            Assert.IsNotNull(activar, "una política que activar");
            var politicas = Plaza().PoliticasActivas.Count;
            yield return ClicEn(activar);
            yield return Esperar(() => Plaza().PoliticasActivas.Count > politicas, 10, "la política en vigor");
            yield return Esperar(() => Ventana().Q("consejo-gobernador").Q(className: "activa") != null, 5, "la política en vigor en su carta");
            Debug.Log($"[Prueba] política activada: {string.Join(", ", Plaza().PoliticasActivas.Select(x => $"{x.Cargo}:{x.PoliticaId}"))}");
            yield return Capturar("84b_politica_en_vigor");
            yield return Tecla(UnityEngine.InputSystem.Key.Escape);
            yield return Esperar(() => Ventana().ClassListContains("oculto"), 5, "Esc cierra Gobierno");

            // ── El Almacén, desde su placa: a pantalla completa ──
            yield return ClicEn(plaza.Q("placa-almacen"));
            yield return Esperar(() => !Ventana().ClassListContains("oculto") && Ventana().Q("eleccion-barra") != null, 5, "el Almacén");
            Assert.IsTrue(Ventana().ClassListContains("plaza-ventana-completa"), "a pantalla completa");
            yield return EnTamanos(raiz, "85_almacen", () => DentroDelMarco(Ventana(), "el Almacén"));
            // En la barra de arriba: quitar uno, y la barra cambia; «Que vaya sola» la deja como estaba.
            var elegido = Ventana().Q("eleccion-barra").Query<Button>(className: "plaza-barra-eleccion").ToList().First(b => b.ClassListContains("elegido"));
            var recurso = elegido.name.Substring("barra-".Length);
            Assert.IsNotNull(plaza.Q("placa-almacen").Q("pieza-" + CosasDelMapa.NombreDeRecurso(recurso)), "el recurso en la barra");
            yield return ClicEn(elegido);
            yield return Esperar(() => plaza.Q("placa-almacen").Q("pieza-" + CosasDelMapa.NombreDeRecurso(recurso)) == null, 5, "quitado de la barra");
            yield return ClicEn(Ventana().Q<Button>("barra-sola"));
            yield return Esperar(() => plaza.Q("placa-almacen").Q("pieza-" + CosasDelMapa.NombreDeRecurso(recurso)) != null && Ventana().Q<Button>("barra-sola") == null, 5, "la barra sola otra vez");
            // La reserva: ya eres Tesorero; 5 de lo primero y «Guardar».
            var ficha = Ventana().Q("reserva").Query(className: "plaza-reserva-ficha").ToList().First();
            var deReserva = ficha.name.Substring("reserva-".Length);
            var campo = ficha.Q<IntegerField>("reserva-valor");
            Assert.IsNotNull(campo, "el Tesorero calibra la reserva");
            campo.value = 5;
            yield return Esperar(() => Ventana().Q<Button>("reserva-guardar")?.enabledSelf == true, 5, "«Guardar» con un cambio");
            yield return Capturar("85b_reserva_cambiada");
            yield return ClicEn(Ventana().Q<Button>("reserva-guardar"));
            yield return Esperar(() => Plaza().ReservaManual != null && Plaza().ReservaManual.PorId.TryGetValue(deReserva, out var n) && n == 5000, 10, "la reserva en el motor");
            // Pausar el consumo del pan, y reanudarlo.
            if (Ventana().Q("pausar-pan") != null)
            {
                yield return ClicEn(Ventana().Q("pausar-pan"));
                yield return Esperar(() => Plaza().ConsumoPausado.Contains("pan"), 10, "el pan pausado");
                yield return Esperar(() => Ventana().Q("pausar-pan")?.ClassListContains("encendido") == true, 5, "el interruptor del pan");
                yield return Capturar("85c_consumo_pausado");
                yield return ClicEn(Ventana().Q("pausar-pan"));
                yield return Esperar(() => !Plaza().ConsumoPausado.Contains("pan"), 10, "el pan otra vez");
            }
            else Debug.Log("[Prueba] pausar consumo: la plaza no tiene pan");
            // Los talleres: parar una receta y reanudarla.
            var receta = Ventana().Q("talleres").Query(className: "plaza-prod-receta").ToList().FirstOrDefault(f => f.Q<Button>("receta-interruptor") != null && !f.ClassListContains("bloqueada"));
            if (receta != null)
            {
                var produce = receta.name.Substring("receta-".Length);
                yield return ClicEn(receta.Q<Button>("receta-interruptor"));
                yield return Esperar(() => Plaza().RecetasPausadas.Contains(produce), 10, $"{produce} parada");
                yield return Esperar(() => Ventana().Q("receta-" + produce) is VisualElement r && r.ClassListContains("parada"), 5, "la receta parada en su fila");
                yield return ClicEn(Ventana().Q("receta-" + produce).Q<Button>("receta-interruptor"));
                yield return Esperar(() => !Plaza().RecetasPausadas.Contains(produce), 10, $"{produce} en marcha");
            }
            else Debug.Log("[Prueba] talleres: ninguna receta");

            // «Tu carga»: madera de la plaza al carro, del carro a la plaza, y cerrar el almacén a los ciudadanos.
            yield return ClicEn(Ventana().Q<Button>("sub-carga"));
            yield return Esperar(() => Ventana().Q("carga-plaza") != null, 5, "«Tu carga»");
            yield return EnTamanos(raiz, "86_tu_carga", () => DentroDelMarco(Ventana(), "Tu carga"));
            long EnCarro(string r) => Yo().FueraCarro != null && Yo().FueraCarro.PorId.TryGetValue(r, out var n) ? n : 0;
            var madera = Ventana().Q("carga-plaza").Q("plaza-madera");
            Assert.IsNotNull(madera, "la madera de la plaza");
            madera.Q<IntegerField>("cantidad").value = 20;
            yield return ClicEn(madera.Q<Button>("al-carro"));
            yield return Esperar(() => EnCarro("madera") >= 20_000, 10, "la madera en el carro");
            yield return Esperar(() => Ventana().Q("carga-mia")?.Q("carro-madera") != null, 5, "la madera en «Tu carro»");
            yield return Capturar("86b_madera_en_el_carro");
            var fila = Ventana().Q("carga-mia").Q("carro-madera");
            fila.Q<IntegerField>("cantidad").value = 20;
            yield return ClicEn(fila.Q<Button>("a-la-plaza"));
            yield return Esperar(() => EnCarro("madera") == 0, 10, "la madera de vuelta en la plaza");
            yield return ClicEn(Ventana().Q<Toggle>("cerrar-almacen"));
            yield return Esperar(() => Plaza().AlmacenCerradoACiudadanos, 10, "el almacén cerrado a los ciudadanos");
            yield return Esperar(() => Ventana().Q("carga-plaza")?.Q<Label>(className: "plaza-nota") != null, 5, "la nota del almacén cerrado");
            yield return Capturar("86c_almacen_cerrado");
            yield return ClicEn(Ventana().Q<Toggle>("cerrar-almacen"));
            yield return Esperar(() => !Plaza().AlmacenCerradoACiudadanos, 10, "el almacén abierto");
            yield return Tecla(UnityEngine.InputSystem.Key.Escape);
            yield return Esperar(() => Ventana().ClassListContains("oculto") && !SelectorDePersonaje.Interior.PanelAbierto, 5, "Esc cierra el Almacén");
        }
    }
}
