// Las distancias de plaza (`DistanciasDePlaza`, su escena), sin ventana: abre la escena, entra en Play y espera a que se monte; comprueba que se
// monta sin errores (el marco de pruebas falla con cualquier error), que al cambiar el nivel cambian la maqueta y los anillos, que las holguras
// casan con lo puesto, y que con los valores de hoy (nivel 3, aldeas lo más cerca que deja el motor, lo fijo × 2) la tabla marca que la aldea a
// 3 u se mete en la plaza. Necesita el mundo en disco de una partida (`persistentDataPath/mundo`).
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ConquestTactics.Mundo;
using ConquestTactics.Mundo.Mapa;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace ConquestTactics.Mundo.Mapa.EditorTests
{
    public sealed class DistanciasDePlazaEditorTests
    {
        const string Escena = "Assets/Scenes/BronzeAge/DistanciasDePlaza.unity";

        static Type Tipo() => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("ConquestTactics.Mundo.UI.DistanciasDePlaza")).First(t => t != null);

        [UnityTest, Category("Mapa")]
        public IEnumerator LasDistanciasDePlazaSeMontanYMiden()
        {
            if (!Directory.Exists(Path.Combine(Application.persistentDataPath, "mundo", "artefacto")))
                Assert.Ignore("no hay mundo en disco: entra antes una vez al mundo con el servidor");
            EditorSceneManager.OpenScene(Escena, OpenSceneMode.Single);
            yield return new EnterPlayMode();
            // (en otro iterador: con la recarga del dominio al entrar en Play, las variables capturadas de este no siguen)
            yield return Comprobar();
            yield return new ExitPlayMode();
            Assert.That(MaquetasDelMapa.Estilo, Is.EqualTo(EstiloDeAsentamientos.Prototipo), "fuera de Play, el estilo vuelve al del código");
        }

        static IEnumerator Comprobar()
        {
            var tipo = Tipo();
            Component d = null;
            var hasta = Time.realtimeSinceStartup + 120;
            while (Time.realtimeSinceStartup < hasta)
            {
                d = d != null ? d : UnityEngine.Object.FindAnyObjectByType(tipo) as Component;
                if (d != null && (bool)tipo.GetProperty("Lista").GetValue(d)) break;
                yield return null;
            }
            Assert.That(d, Is.Not.Null, "la escena, con su componente");
            Assert.That((bool)tipo.GetProperty("Lista").GetValue(d), "se montó");
            var mapa = (MapaDeMundo)tipo.GetProperty("Mapa").GetValue(d);
            void Fijar(string k, double v) => tipo.GetMethod("Fijar").Invoke(d, new object[] { k, v });
            IEnumerator Esperar() { for (int i = 0; i < 12; i++) yield return null; }
            var holguras = (IList)tipo.GetField("Holguras").GetValue(d);
            var puesto = (IList)tipo.GetField("Puesto").GetValue(d);
            yield return Esperar();

            // al cambiar el nivel cambian la maqueta y los anillos
            var anillos = new Dictionary<int, int>();
            foreach (int n in new[] { 1, 3, 5 })
            {
                Fijar("nivel", n);
                yield return Esperar();
                // en ladera, el pie de las maquetas (lo más bajo de su malla) bajo el suelo: nada en el aire más de 0,01 u
                Assert.That(mapa.Maquetas.AlAireMaximo(), Is.LessThan(0.01), $"nivel {n}: el pie de las maquetas, en el suelo");
                var plaza = mapa.Cosas.First(c => c.Id == "dp-plaza");
                Assert.That(plaza.Detalle, Is.EqualTo($"Nivel {n}"), $"la plaza, de nivel {n}");
                var materiales = mapa.Maquetas.Encargos(mapa.Vista).Where(e => !e.EsSuelo && !e.EsMuralla).Select(e => e.Material.name).Where(m => m.StartsWith("plaza")).Distinct().ToList();
                Assert.That(materiales.Any(m => m[5] - '0' == n), $"la maqueta de nivel {n} ({string.Join(", ", materiales)})");
                anillos[n] = mapa.RayasAdicionales.Count;
            }
            Assert.That(anillos[5], Is.GreaterThan(anillos[1]), "más anillos de aldea en el nivel 5 que en el 1");
            // los niveles 1 a 5 rehechos, cada variante: el suelo del modelo, en el suelo pintado (asentadas por vértice)
            foreach (int n in new[] { 1, 2, 3, 4, 5 })
                foreach (int v in new[] { 0, 1 })
                {
                    Fijar("nivel", n);
                    Fijar("variante", v);
                    yield return Esperar();
                    Assert.That(mapa.Maquetas.AlAireMaximo(), Is.LessThan(0.01), $"nivel {n}, variante {v + 1}: el suelo del modelo, en el suelo");
                }
            Fijar("variante", 0);
            Fijar("nivel", 5);
            // la maqueta fija por nivel: no crece con la zona; sin ella, sí
            double RadioPlaza() => mapa.Cosas.First(c => c.Id == "dp-plaza").Radio;
            Fijar("zona", 30);
            yield return Esperar();
            double fija30 = RadioPlaza();
            Fijar("zona", 60);
            yield return Esperar();
            Assert.That(RadioPlaza(), Is.EqualTo(fija30).Within(1e-6), "fija por nivel: la zona no la cambia");
            Fijar("tamanoFijo", 0);
            yield return Esperar();
            // (los cinco niveles, hechos a la medida de la choza: su radio es el de su modelo, sin la tabla ni la zona)
            Assert.That(RadioPlaza(), Is.EqualTo(fija30).Within(1e-6), "con el modelo a la medida de la choza, tampoco la cambia la zona");
            foreach (int n in new[] { 1, 3, 5 })
                Assert.That(CosasDelMapa.HuellaDePlaza(4, n), Is.EqualTo(Math.Max(CosasDelMapa.HuellaMinima, MaquetasDelPrototipo.RadioDePlaza(4, n)) * CosasDelMapa.Fijos(ClaseDeCosa.Plaza)).Within(1e-9),
                    $"apagada, la regla de hoy intacta (nivel {n})");
            Fijar("tamanoFijo", 1);
            yield return Esperar();
            var radios = (double[])tipo.GetField("RadiosFijos").GetValue(null);
            foreach (int n in new[] { 1, 2, 3, 4, 5 })
            {
                Fijar("nivel", n);
                yield return Esperar();
                Assert.That(CosasDelMapa.HuellaDePlaza(1, n), Is.EqualTo(radios[n - 1]).Within(1e-9), $"fija: el nivel {n}, a su radio");
                double casa = (double)tipo.GetProperty("CasaDeAhora").GetValue(d);
                Assert.That(casa, Is.EqualTo(0.32).Within(0.02), $"fija: la casa del nivel {n}, ≈ 0,32 u");
            }
            StringAssert.Contains("regla nueva: radio de plaza por nivel: 1,4 / 1,9 / 4,15 / 5,1 / 5,6 u · casa 0,32 u", (string)tipo.GetMethod("Valores").Invoke(d, null));

            // los valores de hoy: nivel 3, dos aldeas lo más cerca que deja el motor, un pueblo a 3 u, lo fijo × 2
            tipo.GetMethod("Restablecer").Invoke(d, null);
            yield return Esperar();
            // las holguras casan con lo puesto: cada cosa en su sitio, y la holgura es la distancia menos las huellas de las cosas del mapa
            foreach (var p in puesto)
            {
                var (id, x, y) = ((string)p.GetType().GetField("Item1").GetValue(p), (double)p.GetType().GetField("Item4").GetValue(p), (double)p.GetType().GetField("Item5").GetValue(p));
                var c = mapa.Cosas.FirstOrDefault(k => k.Id == id);
                Assert.That(c, Is.Not.Null, $"{id}, en el mapa");
                Assert.That(Math.Abs(c.X - x) + Math.Abs(c.Y - y), Is.LessThan(0.002), $"{id}, en su sitio");
            }
            Assert.That(holguras.Count, Is.GreaterThan(0), "hay tabla");
            foreach (var h in holguras)
            {
                double dd = (double)h.GetType().GetField("Item2").GetValue(h), ra = (double)h.GetType().GetField("Item3").GetValue(h),
                    rb = (double)h.GetType().GetField("Item4").GetValue(h), hol = (double)h.GetType().GetField("Item5").GetValue(h);
                Assert.That(hol, Is.EqualTo(dd - ra - rb).Within(1e-9), (string)h.GetType().GetField("Item1").GetValue(h));
            }
            var aldea = mapa.Cosas.First(c => c.Id == "dp-aldea-1");
            var plaza3 = mapa.Cosas.First(c => c.Id == "dp-plaza");
            double dist = Math.Sqrt((aldea.X - plaza3.X) * (aldea.X - plaza3.X) + (aldea.Y - plaza3.Y) * (aldea.Y - plaza3.Y));
            Assert.That(dist, Is.EqualTo(3).Within(0.01), "la aldea 1, a la separación mínima (3 u)");
            var fila = holguras.Cast<object>().First(h => ((string)h.GetType().GetField("Item1").GetValue(h)).StartsWith("plaza – aldea 1"));
            Assert.That((double)fila.GetType().GetField("Item6").GetValue(fila), Is.LessThan(0), "con lo fijo × 2, el caserío de la aldea a 3 u se mete en la plaza de nivel 3");
            Assert.That(fila.GetType().GetField("Item7").GetValue(fila).ToString(), Is.EqualTo("Maquetas"), "y la tabla lo marca: se pisan las maquetas");
            // la huella de las aldeas cuenta sus campos: la holgura con campos es menor que la de los caseríos
            foreach (var h in holguras.Cast<object>().Where(h => ((string)h.GetType().GetField("Item1").GetValue(h)).Contains("aldea")))
                Assert.That((double)h.GetType().GetField("Item5").GetValue(h), Is.LessThan((double)h.GetType().GetField("Item6").GetValue(h)), (string)h.GetType().GetField("Item1").GetValue(h));
            // la huella de la plaza de nivel 3 cuenta su muralla
            var plazaM = mapa.Cosas.First(c => c.Id == "dp-plaza");
            Assert.That(plazaM.Radio, Is.GreaterThan(CosasDelMapa.HuellaDePlaza(4.0, 3) * 1.01), "la plaza de nivel 3 con muralla, más que sin ella");
            // una etiqueta para cada maqueta, con el número de la huella de la tabla
            var sobre = (IList)tipo.GetField("Sobre").GetValue(d);
            var puestas = puesto.Cast<object>().Where(p => p.GetType().GetField("Item2").GetValue(p).ToString() != "Ejercito").Select(p => (string)p.GetType().GetField("Item1").GetValue(p)).ToList();
            var conEtiqueta = sobre.Cast<object>().Select(t => (string)t.GetType().GetField("Item1").GetValue(t)).ToList();
            Assert.That(conEtiqueta, Is.EquivalentTo(puestas), "una etiqueta por maqueta");
            foreach (var t in sobre)
            {
                var id = (string)t.GetType().GetField("Item1").GetValue(t);
                var tamano = (string)t.GetType().GetField("Item3").GetValue(t);
                var cosa = mapa.Cosas.First(c => c.Id == id);
                StringAssert.Contains(cosa.Radio.ToString("0.00"), tamano, $"{id}: el tamaño de la etiqueta es la huella de la tabla");
                Assert.That((float)t.GetType().GetField("Item6").GetValue(t), Is.GreaterThan(0), $"{id}: la etiqueta flota sobre la maqueta");
            }
            // el panel de valores lista las cifras del motor
            var valores = (string)tipo.GetMethod("ValoresDelMotor").Invoke(d, new object[] { false });
            foreach (var cifra in new[] { "12 u al fundar", "+2 u por edificio", "20 · 30 · [40] · 50 · 60", "2 · 3 · [4] · 5 · 6", "Entre plazas: 20 u", "Radio de puerta: 2 u", "plaza 4,8" })
                StringAssert.Contains(cifra, valores, "el panel de valores del motor");
            // los valores del motor, editables solo en la escena: la separación de aldea a 6 u mueve la aldea 1 y cambia su holgura; el radio de
            // puerta a 4 u mueve la columna; «Volver a los del motor» lo deja como estaba; y los datos de la caché no cambian
            var banco = (BancoDelMapa)tipo.GetProperty("Banco").GetValue(d);
            double sepDatos = banco.Datos.Numero("documentos.Economia_Construccion.PUEBLOS_EXTRACTORES.aldea.separacionMinima");
            double Dist(string id) { var c = mapa.Cosas.First(k => k.Id == id); return Math.Sqrt((c.X - plaza3.X) * (c.X - plaza3.X) + (c.Y - plaza3.Y) * (c.Y - plaza3.Y)); }
            double HolguraDe(string empieza) { var f = holguras.Cast<object>().First(x => ((string)x.GetType().GetField("Item1").GetValue(x)).StartsWith(empieza)); return (double)f.GetType().GetField("Item5").GetValue(f); }
            double antes = HolguraDe("plaza – aldea 1");
            Fijar("puertaBorde", 0);
            tipo.GetMethod("FijarMotor").Invoke(d, new object[] { "aldea a plaza", 6.0, 0 });
            tipo.GetMethod("FijarMotor").Invoke(d, new object[] { "radio de puerta", 4.0, 0 });
            for (int i = 0; i < 12; i++) yield return null;
            Assert.That(Dist("dp-aldea-1"), Is.EqualTo(6).Within(0.01), "con la separación a 6 u, la aldea 1 a 6 u");
            Assert.That(HolguraDe("plaza – aldea 1"), Is.EqualTo(antes + 3).Within(0.01), "y su holgura, 3 u más");
            Assert.That(Dist("dp-columna"), Is.EqualTo(4).Within(0.01), "con el radio de puerta a 4 u, la columna a 4 u");
            var cambiados = (System.Collections.Generic.List<string>)tipo.GetMethod("Cambiados").Invoke(d, null);
            Assert.That(cambiados, Has.Count.EqualTo(2), "dos valores cambiados");
            StringAssert.Contains("PUEBLOS_EXTRACTORES.aldea.separacionMinima 3 → 6", string.Join("; ", cambiados));
            StringAssert.Contains("motor: ", (string)tipo.GetMethod("Valores").Invoke(d, null), "«Copiar valores» lleva lo cambiado");
            Assert.That(banco.Datos.Numero("documentos.Economia_Construccion.PUEBLOS_EXTRACTORES.aldea.separacionMinima"), Is.EqualTo(sepDatos), "los datos de la caché, intactos");
            tipo.GetMethod("VolverAlMotor").Invoke(d, null);
            for (int i = 0; i < 12; i++) yield return null;
            Assert.That(Dist("dp-aldea-1"), Is.EqualTo(3).Within(0.01), "de vuelta a los del motor, la aldea a 3 u");
            Assert.That(Dist("dp-columna"), Is.EqualTo(2).Within(0.01), "y la columna a 2 u");
            // la puerta desde el borde: en los niveles 1 a 5, la columna justo fuera de la maqueta (su borde más el radio de la columna), y la
            // fila plaza–columna, libre
            Fijar("puertaBorde", 1);
            foreach (int n in new[] { 1, 2, 3, 4, 5 })
            {
                Fijar("nivel", n);
                for (int i = 0; i < 12; i++) yield return null;
                double rpN = mapa.Cosas.First(c => c.Id == "dp-plaza").Radio, rc = mapa.Cosas.First(c => c.Id == "dp-columna").Radio;
                Assert.That(Dist("dp-columna"), Is.EqualTo(rpN + rc).Within(0.03), $"nivel {n}: la columna a la puerta, a borde + su radio");
                var filaC = holguras.Cast<object>().First(h => ((string)h.GetType().GetField("Item1").GetValue(h)).StartsWith("plaza – columna"));
                Assert.That(filaC.GetType().GetField("Item7").GetValue(filaC).ToString(), Is.EqualTo("Libre"), $"nivel {n}: la columna, fuera de la plaza");
            }
            // las avanzadas: por defecto, justo fuera de la plaza (su radio con la muralla, el de la avanzada y 0,5 u); con el deslizador, más lejos
            Fijar("nivel", 5);
            Fijar("avanzadas", 1);
            Fijar("etapa", 3);
            Fijar("distanciaAvanzada", 0);
            for (int i = 0; i < 12; i++) yield return null;
            double minAv = (double)tipo.GetProperty("MinimoDeAvanzadaEnMesa").GetValue(d);
            double rPlaza = mapa.Cosas.First(c => c.Id == "dp-plaza").Radio, rAv = mapa.Cosas.First(c => c.Id == "dp-avanzada-1").Radio;
            Assert.That(minAv, Is.EqualTo(rPlaza + rAv + 0.5).Within(0.03), "el mínimo de la mesa: plaza con muralla + avanzada + 0,5 u");
            Assert.That(Dist("dp-avanzada-1"), Is.EqualTo(minAv).Within(0.03), "la avanzada arranca en el mínimo");
            var filaAv = holguras.Cast<object>().First(h => ((string)h.GetType().GetField("Item1").GetValue(h)).StartsWith("plaza – avanzada 1"));
            Assert.That(filaAv.GetType().GetField("Item7").GetValue(filaAv).ToString(), Is.EqualTo("Libre"), "la avanzada no se monta en la plaza");
            Fijar("distanciaAvanzada", minAv + 5);
            for (int i = 0; i < 12; i++) yield return null;
            Assert.That(Dist("dp-avanzada-1"), Is.EqualTo(minAv + 5).Within(0.03), "con el deslizador, más lejos");
            Fijar("distanciaAvanzada", 1);
            for (int i = 0; i < 12; i++) yield return null;
            Assert.That(Dist("dp-avanzada-1"), Is.EqualTo(minAv).Within(0.03), "nunca más cerca que el mínimo");
            Fijar("avanzadas", 0);
            // «Cargar propuesta»: los valores dichos, y el pueblo sobre su nodo a 12 u no lo pisa la plaza de nivel 5 con muralla
            tipo.GetMethod("CargarPropuesta").Invoke(d, null);
            Fijar("nivel", 5);
            Fijar("pueblos", 1);
            Fijar("distanciaPueblo", 0);
            for (int i = 0; i < 12; i++) yield return null;
            var valoresP = (string)tipo.GetMethod("Valores").Invoke(d, null);
            foreach (var cifra in new[] { "radioInicial 12 → 24", "radioMaximoPorNivel.5 60 → 90", "separacionPlazas 20 → 60", "radioExclusionFundar 20 → 60",
                         "aldea.separacionMinima 3 → 15 (aldea a plaza)", "separación de aldea a otro pueblo (extractor o aldea) = 7,5 u", "ORILLA_RIO.radio.plaza 4,8 → 15", "puerta desde el borde = 1 u", "separación de nodo = 12 u", "fijos=2.00; plazas=0.50" })
                StringAssert.Contains(cifra, valoresP, "«Cargar propuesta»");
            Assert.That(Dist("dp-pueblo-1"), Is.EqualTo(12).Within(0.02), "el pueblo sobre su nodo, a la separación de nodo");
            // la separación de aldea, en dos: la aldea más cercana a 15 u de la plaza; una aldea a 7,5 u de un extractor no lo pisa (campos 3 u y
            // pueblo 2,04 u); y dos extractores a 12 u uno de otro no dan fila ni roja ni ámbar
            Assert.That(new[] { "dp-aldea-1", "dp-aldea-2" }.Min(Dist), Is.EqualTo(15).Within(0.06), "la aldea más cercana, a 15 u de la plaza");
            var huellas = tipo.GetMethod("Huellas");
            var (_, camposAldea) = ((double, double))huellas.Invoke(d, new object[] { "dp-aldea-1" });
            var (pueblo1, _) = ((double, double))huellas.Invoke(d, new object[] { "dp-pueblo-1" });
            Assert.That(7.5 - camposAldea - pueblo1, Is.GreaterThanOrEqualTo(0), $"a 7,5 u, una aldea ({camposAldea:0.00} u con campos) y un pueblo ({pueblo1:0.00} u) no se pisan");
            Fijar("aldeas", 3);
            Fijar("pueblos", 2);
            for (int i = 0; i < 12; i++) yield return null;
            var entreExtractores = holguras.Cast<object>().Where(h => ((string)h.GetType().GetField("Item1").GetValue(h)).StartsWith("pueblo 1") && ((string)h.GetType().GetField("Item1").GetValue(h)).Contains("– pueblo 2")).ToList();
            Assert.That(entreExtractores, Has.Count.EqualTo(1), "una fila pueblo 1 – pueblo 2");
            Assert.That((double)entreExtractores[0].GetType().GetField("Item2").GetValue(entreExtractores[0]), Is.EqualTo(12).Within(0.05), "los dos extractores, a 12 u uno de otro");
            Assert.That(entreExtractores[0].GetType().GetField("Item7").GetValue(entreExtractores[0]).ToString(), Is.EqualTo("Libre"), "dos extractores a 12 u: ni rojo ni ámbar");
            foreach (var h in holguras.Cast<object>().Where(h => ((string)h.GetType().GetField("Item1").GetValue(h)).StartsWith("aldea")))
                Assert.That((double)h.GetType().GetField("Item2").GetValue(h), Is.GreaterThanOrEqualTo(7.5 - 1e-6), $"{h.GetType().GetField("Item1").GetValue(h)}: a 7,5 u o más");
            var al5 = holguras.Cast<object>().First(h => ((string)h.GetType().GetField("Item1").GetValue(h)).StartsWith("plaza de nivel 5 al crecer"));
            Assert.That((double)al5.GetType().GetField("Item5").GetValue(al5), Is.GreaterThanOrEqualTo(0), "la plaza de nivel 5 con muralla no pisa el pueblo a 12 u");
            tipo.GetMethod("VolverAlMotor").Invoke(d, null);
            tipo.GetMethod("Restablecer").Invoke(d, null);
            for (int i = 0; i < 12; i++) yield return null;
            Assert.That(((System.Collections.Generic.List<string>)tipo.GetMethod("Cambiados").Invoke(d, null)).Count, Is.EqualTo(0));

            // los caminos de los carros: uno por aldea y por pueblo, del centro de cada uno al de la plaza, en la red como la manda el servidor
            var rutas = (IDictionary)tipo.GetField("RutasDeAcarreo").GetValue(d);
            var tramos = (IList)tipo.GetField("TramosPuestos").GetValue(d);
            var centro = puesto.Cast<object>().Select(q => ((string)q.GetType().GetField("Item1").GetValue(q), (double)q.GetType().GetField("Item4").GetValue(q),
                (double)q.GetType().GetField("Item5").GetValue(q))).First(q => q.Item1 == "dp-plaza");
            int conCamino = puesto.Cast<object>().Count(q => { var id = (string)q.GetType().GetField("Item1").GetValue(q); return id.StartsWith("dp-aldea") || id.StartsWith("dp-pueblo"); });
            Assert.That(rutas.Count, Is.EqualTo(conCamino), "un camino por aldea y por pueblo");
            foreach (DictionaryEntry kv in rutas)
            {
                var puntos = (IList)kv.Value;
                var ultimo = puntos[puntos.Count - 1];
                Assert.That((double)ultimo.GetType().GetField("Item1").GetValue(ultimo), Is.EqualTo(centro.Item2).Within(0.002), $"{kv.Key}: acaba en el centro de la plaza");
                Assert.That((double)ultimo.GetType().GetField("Item2").GetValue(ultimo), Is.EqualTo(centro.Item3).Within(0.002));
            }
            Assert.That(tramos.Count, Is.GreaterThan(0), "la red, en tramos");
            Assert.That(mapa.Cosas.Count(c => c.Clase == ClaseDeCosa.Aldea), Is.EqualTo(2));
            Fijar("caminos", 0);
            yield return Esperar();
            Assert.That(rutas.Count, Is.EqualTo(0), "sin la casilla, sin caminos");
            // el tipo: todas iguales o variado, y sale en «vista:» de «Copiar valores» (no en las líneas del motor)
            Fijar("caminos", 1);
            Fijar("tipoAldea", 5);
            Fijar("tipoPueblo", 4);
            yield return Esperar();
            var vista = (string)tipo.GetMethod("Valores").Invoke(d, null);
            StringAssert.Contains("vista: aldeas ganado vacuno; pueblos mina de oro; caminos según el motor", vista);
            Fijar("tipoAldea", 7);
            Fijar("escalon", 2);
            yield return Esperar();
            vista = (string)tipo.GetMethod("Valores").Invoke(d, null);
            StringAssert.Contains("vista: aldeas variadas; pueblos mina de oro; caminos todo camino", vista);
            Assert.That(tramos.Cast<object>().All(t => (int)t.GetType().GetField("Item1").GetValue(t) == 1), "forzado: todo camino");
            tipo.GetMethod("Restablecer").Invoke(d, null);
            yield return Esperar();
            StringAssert.Contains("vista: aldeas trigo; pueblos cantera de piedra; caminos según el motor", (string)tipo.GetMethod("Valores").Invoke(d, null));
            Debug.Log("[Distancias de plaza] " + tipo.GetMethod("Valores").Invoke(d, null));
        }

        /// <summary>Las capturas de «Caminos y tipos» (en `MUNDO_CAPTURAS`): con la propuesta cargada y nivel 5, seis aldeas de tipos distintos
        /// y luego una ganadera ovina con cinco pueblos de cada recurso, en planta y del juego; cada aldea y cada pueblo de cerca con su camino.</summary>
        [UnityTest, Category("Mapa"), Explicit("capturas para la página"), Timeout(3600000)]
        public IEnumerator LosCaminosEnCapturas()
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MUNDO_CAPTURAS"))) Assert.Ignore("sin MUNDO_CAPTURAS");
            EditorSceneManager.OpenScene(Escena, OpenSceneMode.Single);
            yield return new EnterPlayMode();
            yield return CapturasDeCaminos();
            yield return new ExitPlayMode();
        }

        /// <summary>El panel derecho cabe sin pisarse a 1920 × 1080 y a 1200 × 675 (nada encogido ni encima de nada; la lectura, fuera de los
        /// paneles), y los desplegables se leen cerrados y con la lista abierta, que no se corta. Con `MUNDO_CAPTURAS`, captura las dos cosas.</summary>
        [UnityTest, Category("Mapa"), Explicit("con la escena en juego"), Timeout(600000)]
        public IEnumerator ElPanelCabeYLosDesplegablesSeLeen()
        {
            EditorSceneManager.OpenScene(Escena, OpenSceneMode.Single);
            yield return new EnterPlayMode();
            yield return PanelYDesplegables();
            yield return new ExitPlayMode();
        }

        /// <summary>Cambiar el tipo en vivo rehace la maqueta sin moverla: la aldea de trigo a sésamo (otro cultivo en sus campos) y a ganadera
        /// (otro núcleo), y el pueblo de cantera a mina (otro modelo). Con `MUNDO_CAPTURAS`, la escena con sésamo y mina de estaño elegidos así.</summary>
        [UnityTest, Category("Mapa"), Explicit("con la escena en juego"), Timeout(600000)]
        public IEnumerator ElTipoEnVivoRehaceLasMaquetas()
        {
            EditorSceneManager.OpenScene(Escena, OpenSceneMode.Single);
            yield return new EnterPlayMode();
            yield return TipoEnVivo();
            yield return new ExitPlayMode();
        }

        /// <summary>Las capturas de «Plaza de tamaño fijo por nivel» (en `MUNDO_CAPTURAS`): con la propuesta cargada y el modo fijo, cada nivel
        /// del 1 al 5 en la vista del juego con una aldea de nivel 2 al lado; y un primer plano de las viviendas de cada nivel y del núcleo de la
        /// aldea, todos con la cámara a la misma altura y distancia (la misma apertura), para comparar las chozas a ojo.</summary>
        [UnityTest, Category("Mapa"), Explicit("capturas para la página"), Timeout(3600000)]
        public IEnumerator LaPlazaFijaEnCapturas()
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MUNDO_CAPTURAS"))) Assert.Ignore("sin MUNDO_CAPTURAS");
            EditorSceneManager.OpenScene(Escena, OpenSceneMode.Single);
            yield return new EnterPlayMode();
            yield return CapturasDePlazaFija();
            yield return new ExitPlayMode();
        }

        static IEnumerator CapturasDePlazaFija()
        {
            var carpeta = Environment.GetEnvironmentVariable("MUNDO_CAPTURAS");
            Directory.CreateDirectory(carpeta);
            var tipo = Tipo();
            Component d = null;
            var hasta = Time.realtimeSinceStartup + 120;
            while (Time.realtimeSinceStartup < hasta)
            {
                d = d != null ? d : UnityEngine.Object.FindAnyObjectByType(tipo) as Component;
                if (d != null && (bool)tipo.GetProperty("Lista").GetValue(d)) break;
                yield return null;
            }
            var mapa = (MapaDeMundo)tipo.GetProperty("Mapa").GetValue(d);
            void Fijar(string k, double v) => tipo.GetMethod("Fijar").Invoke(d, new object[] { k, v });
            (double x, double y) Sitio(string id)
            {
                var q = ((IList)tipo.GetField("Puesto").GetValue(d)).Cast<object>().First(o => (string)o.GetType().GetField("Item1").GetValue(o) == id);
                return ((double)q.GetType().GetField("Item4").GetValue(q), (double)q.GetType().GetField("Item5").GetValue(q));
            }
            var rt = new RenderTexture(1920, 1080, 24) { antiAliasing = 4 };
            mapa.Vista.targetTexture = rt;
            var doc = UnityEngine.Object.FindAnyObjectByType<UIDocument>();
            IEnumerator Listo()
            {
                var fin = Time.realtimeSinceStartup + 60;
                yield return null;
                while (!mapa.SueloCompleto && Time.realtimeSinceStartup < fin) yield return null;
            }
            var e = EscalaDelMapa.MetrosPorUnidad;
            // (el primer plano: la cámara a 2,2 u de altura y 3 u al norte del punto, mirando hacia el sur, de espaldas a la puerta y su columna;
            // sin el panel)
            (Vector3, Vector3) Cerca(double x, double y)
            {
                double h = mapa.AlturaVisible(x, y) / e;
                return (EscalaDelMapa.AEscena(x, y + 3, (h + 2.2) * e), EscalaDelMapa.AEscena(x, y, h * e));
            }
            var texto = new System.Text.StringBuilder();
            foreach (int n in new[] { 1, 2, 3, 4, 5 })
            {
                tipo.GetMethod("Restablecer").Invoke(d, null);
                tipo.GetMethod("CargarPropuesta").Invoke(d, null);
                Fijar("nivel", n);
                Fijar("pueblos", 0);
                Fijar("aldeas", 1);
                Fijar("nivelAldea", 2);
                foreach (var k in new[] { "huella", "zona", "aldeas", "separacion", "avanzada", "puerta", "orilla", "vecina", "tamanos", "nodo" })
                    ((Dictionary<string, bool>)tipo.GetField("Anillos").GetValue(d))[k] = false;
                for (int i = 0; i < 12; i++) yield return null;
                tipo.GetMethod("EnPlanta").Invoke(d, new object[] { false });
                if (doc != null) doc.panelSettings.targetTexture = rt;
                yield return new WaitForSecondsRealtime(3.5f);
                yield return Listo();
                if (n == 3) yield return Foto(rt, Path.Combine(carpeta, "pf_panel.png"));
                // (la plaza y su aldea, con la misma cámara en los cinco niveles: mirando a un tercio de la plaza a la aldea, 20 u al sur y 16 u
                // de alto; sin el panel)
                tipo.GetMethod("VerPanel").Invoke(d, new object[] { false });
                {
                    var (px, py) = Sitio("dp-plaza");
                    var (ax, ay) = Sitio("dp-aldea-1");
                    double mx = px + (ax - px) / 3, my = py + (ay - py) / 3, h = mapa.AlturaVisible(mx, my) / e;
                    MapaDeMundo.CamaraFija = (EscalaDelMapa.AEscena(mx, my - 20, (h + 16) * e), EscalaDelMapa.AEscena(mx, my, h * e));
                }
                yield return Listo();
                yield return Foto(rt, Path.Combine(carpeta, $"pf_{n}_juego.png"));
                double r = CosasDelMapa.HuellaDePlaza(1, n), casa = (double)tipo.GetProperty("CasaDeAhora").GetValue(d);
                texto.AppendLine($"nivel {n}: radio {r:0.00} u · casa {casa:0.000} u");
                // los primeros planos, sin el panel
                if (doc != null) doc.panelSettings.targetTexture = null;
                tipo.GetMethod("VerPanel").Invoke(d, new object[] { false });
                var c = Sitio("dp-plaza");
                MapaDeMundo.CamaraFija = Cerca(c.x, c.y + r * 0.35);
                yield return Listo();
                yield return Foto(rt, Path.Combine(carpeta, $"pf_{n}_cerca.png"));
                if (n == 2)
                {
                    var a = Sitio("dp-aldea-1");
                    MapaDeMundo.CamaraFija = Cerca(a.x, a.y);
                    yield return Listo();
                    yield return Foto(rt, Path.Combine(carpeta, "pf_aldea_cerca.png"));
                }
                MapaDeMundo.CamaraFija = null;
                tipo.GetMethod("VerPanel").Invoke(d, new object[] { true });
            }
            File.WriteAllText(Path.Combine(carpeta, "pf_valores.txt"), texto + (string)tipo.GetMethod("Valores").Invoke(d, null));
            mapa.Vista.targetTexture = null;
            if (doc != null) doc.panelSettings.targetTexture = null;
            rt.Release();
        }

        static IEnumerator TipoEnVivo()
        {
            var carpeta = Environment.GetEnvironmentVariable("MUNDO_CAPTURAS");
            var tipo = Tipo();
            Component d = null;
            var hasta = Time.realtimeSinceStartup + 120;
            while (Time.realtimeSinceStartup < hasta)
            {
                d = d != null ? d : UnityEngine.Object.FindAnyObjectByType(tipo) as Component;
                if (d != null && (bool)tipo.GetProperty("Lista").GetValue(d)) break;
                yield return null;
            }
            var mapa = (MapaDeMundo)tipo.GetProperty("Mapa").GetValue(d);
            void Fijar(string k, double v) => tipo.GetMethod("Fijar").Invoke(d, new object[] { k, v });
            IEnumerator Esperar() { for (int i = 0; i < 15; i++) yield return null; }
            (double x, double y) Sitio(string id)
            {
                var q = ((IList)tipo.GetField("Puesto").GetValue(d)).Cast<object>().First(o => (string)o.GetType().GetField("Item1").GetValue(o) == id);
                return ((double)q.GetType().GetField("Item4").GetValue(q), (double)q.GetType().GetField("Item5").GetValue(q));
            }
            (string malla, int cultivo) Maqueta(string id)
            {
                var (x, y) = Sitio(id);
                var m = mapa.Maquetas.Puestas.OrderBy(p => (p.x - x) * (p.x - x) + (p.y - y) * (p.y - y)).First();
                Assert.That(Math.Sqrt((m.x - x) * (m.x - x) + (m.y - y) * (m.y - y)), Is.LessThan(1.0), $"{id}: su maqueta, en su sitio");
                return (m.malla, m.cultivo);
            }
            tipo.GetMethod("Restablecer").Invoke(d, null);
            tipo.GetMethod("CargarPropuesta").Invoke(d, null);
            Fijar("nivel", 5);
            Fijar("aldeas", 1);
            Fijar("pueblos", 1);
            yield return Esperar();
            var trigo = Maqueta("dp-aldea-1");
            var cantera = Maqueta("dp-pueblo-1");
            StringAssert.Contains("cantera", cantera.malla.ToLowerInvariant());
            Fijar("tipoAldea", 2);
            Fijar("tipoPueblo", 2);
            yield return Esperar();
            var sesamo = Maqueta("dp-aldea-1");
            var mina = Maqueta("dp-pueblo-1");
            Assert.That(sesamo.cultivo, Is.Not.EqualTo(trigo.cultivo), "de trigo a sésamo: otro cultivo en los campos");
            Assert.That(sesamo.malla, Is.EqualTo(trigo.malla), "de trigo a sésamo: el mismo núcleo");
            Assert.That(mina.malla, Is.Not.EqualTo(cantera.malla), "de cantera a mina: otro modelo");
            StringAssert.Contains("mina", mina.malla.ToLowerInvariant());
            if (!string.IsNullOrEmpty(carpeta))
            {
                Directory.CreateDirectory(carpeta);
                var rt = new RenderTexture(1920, 1080, 24) { antiAliasing = 4 };
                mapa.Vista.targetTexture = rt;
                var doc = UnityEngine.Object.FindAnyObjectByType<UIDocument>();
                if (doc != null) doc.panelSettings.targetTexture = rt;
                var e = EscalaDelMapa.MetrosPorUnidad;
                var plaza = Sitio("dp-plaza");
                foreach (var id in new[] { "dp-aldea-1", "dp-pueblo-1" })
                {
                    var (x, y) = Sitio(id);
                    double ax = x - plaza.x, ay = y - plaza.y, l = Math.Sqrt(ax * ax + ay * ay);
                    (ax, ay) = (ax / l, ay / l);
                    double h = mapa.AlturaVisible(x, y) / e;
                    MapaDeMundo.CamaraFija = (EscalaDelMapa.AEscena(x - ax * 6.5, y - ay * 6.5, (h + 5.5) * e), EscalaDelMapa.AEscena(x, y, h * e));
                    var fin = Time.realtimeSinceStartup + 60;
                    yield return null;
                    while (!mapa.SueloCompleto && Time.realtimeSinceStartup < fin) yield return null;
                    yield return Foto(rt, Path.Combine(carpeta, $"vivo_{id}.png"));
                }
                MapaDeMundo.CamaraFija = null;
                mapa.Vista.targetTexture = null;
                if (doc != null) doc.panelSettings.targetTexture = null;
                rt.Release();
            }
            Fijar("tipoAldea", 6);
            yield return Esperar();
            Assert.That(Maqueta("dp-aldea-1").malla, Is.Not.EqualTo(trigo.malla), "a ganadera: otro núcleo");
            // la variante de la plaza, elegida en el panel: la 2 y vuelta a la 1
            StringAssert.Contains("_1 ", Maqueta("dp-plaza").malla, "por defecto, la variante 1");
            Fijar("variante", 1);
            yield return Esperar();
            StringAssert.Contains("_2 ", Maqueta("dp-plaza").malla, "la variante 2");
            Fijar("variante", 0);
            yield return Esperar();
            StringAssert.Contains("_1 ", Maqueta("dp-plaza").malla, "otra vez la 1");
            // las avanzadas a la medida de la choza: cada etapa y estado, su modelo, asentado en el suelo, y la huella (aro y clic) la de su etapa
            // operativa en todos sus estados
            Fijar("avanzadas", 1);
            foreach (int etapa in new[] { 1, 2, 3 })
                foreach (int estado in new[] { 0, 1, 2 })
                {
                    if (etapa == 1 && estado == 2) continue; // (la atalaya no tiene ruina)
                    Fijar("etapa", etapa);
                    Fijar("estadoAvanzada", estado);
                    yield return Esperar();
                    Assert.That(mapa.Maquetas.AlAireMaximo(), Is.LessThan(0.01), $"avanzada {etapa}, estado {estado}: el suelo del modelo, en el suelo");
                    var av = mapa.Cosas.First(c => c.Id == "dp-avanzada-1");
                    Assert.That(av.Radio, Is.EqualTo(CosasDelMapa.HuellaDeAvanzadaEn((uint)etapa, estado == 1)).Within(1e-9), $"avanzada {etapa}, estado {estado}: su huella");
                }
            Fijar("estadoAvanzada", 0);
            Fijar("avanzadas", 0);
            // lo menudo (el panel «Campamentos y alijos»), a escala fija: cada campamento de bandidos (claro y peña, nivel 1 a 3), cada alijo y
            // cada tela de mercenarios, su modelo, en el suelo, y sin pisar la plaza
            Fijar("menudo", 1);
            foreach (int tb in new[] { 0, 1 })
                for (int nivelB = 1; nivelB <= 3; nivelB++)
                {
                    Fijar("tipoBandidos", tb);
                    Fijar("nivelBandidos", nivelB - 1);
                    Fijar("tipoAlijo", tb);
                    Fijar("origenMercenarios", nivelB - 1);
                    yield return Esperar();
                    StringAssert.Contains($"bandidos_{tb + 1}_{nivelB} ", Maqueta("dp-bandidos").malla, $"bandidos {tb + 1}, nivel {nivelB}: su modelo");
                    StringAssert.Contains($"alijo_{tb + 1} ", Maqueta("dp-alijo").malla, $"alijo {tb + 1}: su modelo");
                    StringAssert.StartsWith("mercenarios", Maqueta("dp-mercenarios").malla, "los mercenarios: su modelo");
                    Assert.That(mapa.Maquetas.AlAireMaximo(), Is.LessThan(0.01), $"bandidos {tb + 1}, nivel {nivelB}, alijo {tb + 1}: en el suelo");
                    foreach (var k in new[] { "plaza – mercenarios", "plaza – bandidos", "plaza – alijo" })
                    {
                        var fila = ((System.Collections.IList)tipo.GetField("Holguras").GetValue(d)).Cast<object>().First(h => ((string)h.GetType().GetField("Item1").GetValue(h)).StartsWith(k));
                        Assert.That(fila.GetType().GetField("Item7").GetValue(fila).ToString(), Is.EqualTo("Libre"), $"{k}: no pisa la plaza");
                    }
                }
            Fijar("menudo", 0);
            // los pueblos a la medida de la choza (1:1 con su nodo), de cada recurso: su modelo, asentado en el suelo
            for (int t = 0; t <= 5; t++)
            {
                Fijar("tipoPueblo", t);
                yield return Esperar();
                var mp = Maqueta("dp-pueblo-1");
                // (5, «variado»: el primero, de piedra)
                StringAssert.Contains(t == 0 || t == 5 ? "cantera" : "mina", mp.malla.ToLowerInvariant(), $"tipo {t}: su modelo");
                Assert.That(mapa.Maquetas.AlAireMaximo(), Is.LessThan(0.01), $"tipo {t}: el suelo del pueblo, en el suelo");
            }
        }

        static float Luz(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;

        static IEnumerator PanelYDesplegables()
        {
            var carpeta = Environment.GetEnvironmentVariable("MUNDO_CAPTURAS");
            if (!string.IsNullOrEmpty(carpeta)) Directory.CreateDirectory(carpeta);
            var tipo = Tipo();
            Component d = null;
            var hasta = Time.realtimeSinceStartup + 120;
            while (Time.realtimeSinceStartup < hasta)
            {
                d = d != null ? d : UnityEngine.Object.FindAnyObjectByType(tipo) as Component;
                if (d != null && (bool)tipo.GetProperty("Lista").GetValue(d)) break;
                yield return null;
            }
            var mapa = (MapaDeMundo)tipo.GetProperty("Mapa").GetValue(d);
            var doc = UnityEngine.Object.FindAnyObjectByType<UnityEngine.UIElements.UIDocument>();
            var banderas = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var panel = (UnityEngine.UIElements.VisualElement)tipo.GetField("panel", banderas).GetValue(d);
            var pisa = (UnityEngine.UIElements.VisualElement)tipo.GetField("panelPisa", banderas).GetValue(d);
            var cajaLectura = (UnityEngine.UIElements.VisualElement)tipo.GetField("cajaLectura", banderas).GetValue(d);
            tipo.GetMethod("CargarPropuesta").Invoke(d, null);
            tipo.GetMethod("Fijar").Invoke(d, new object[] { "nivel", 5.0 });
            tipo.GetMethod("Fijar").Invoke(d, new object[] { "tipoAldea", 7.0 });
            foreach (var (ancho, alto) in new[] { (1920, 1080), (1200, 675) })
            {
                var rt = new RenderTexture(ancho, alto, 24) { antiAliasing = 4 };
                mapa.Vista.targetTexture = rt;
                doc.panelSettings.targetTexture = rt;
                for (int i = 0; i < 20; i++) yield return null;
                yield return new WaitForSecondsRealtime(1f);
                var raiz = doc.rootVisualElement;
                string donde = $"{ancho}×{alto}";
                // en columna, cada fila empieza donde acaba la anterior, y la última dentro de la caja
                void EnColumna(UnityEngine.UIElements.VisualElement caja, float tope)
                {
                    var hijos = caja.Children().Where(h => h.resolvedStyle.display != UnityEngine.UIElements.DisplayStyle.None).ToList();
                    for (int i = 0; i + 1 < hijos.Count; i++)
                        Assert.That(hijos[i].layout.yMax, Is.LessThanOrEqualTo(hijos[i + 1].layout.y + 0.5f), $"{donde}: «{Nombre(hijos[i])}» pisa a «{Nombre(hijos[i + 1])}»");
                    Assert.That(hijos.Last().layout.yMax, Is.LessThanOrEqualTo(tope + 0.5f), $"{donde}: «{Nombre(hijos.Last())}» se sale de la caja");
                }
                EnColumna(panel, panel.contentRect.height + panel.resolvedStyle.paddingTop);
                Assert.That(panel.worldBound.yMax, Is.LessThanOrEqualTo(raiz.worldBound.yMax), $"{donde}: el panel cabe en la pantalla");
                foreach (var h in panel.Query<UnityEngine.UIElements.VisualElement>().ToList().Where(h => h.parent != null && h.parent.parent == panel && h.parent.style.flexDirection == UnityEngine.UIElements.FlexDirection.Row))
                    Assert.That(h.worldBound.xMax, Is.LessThanOrEqualTo(panel.worldBound.xMax + 0.5f), $"{donde}: «{Nombre(h)}» se sale por la derecha");
                foreach (var col in panel.Children().Where(h => h.style.flexDirection == UnityEngine.UIElements.FlexDirection.Row).SelectMany(f => f.Children()).Where(c => c.childCount > 0))
                    EnColumna(col, col.layout.height);
                // cada casilla entera en su fila, y ningún rótulo cortado
                foreach (var t in panel.Query<Toggle>().ToList())
                {
                    var caja = t.Q(className: "unity-toggle__input");
                    Assert.That(caja.worldBound.yMin >= t.worldBound.yMin - 0.5f && caja.worldBound.yMax <= t.worldBound.yMax + 0.5f, $"{donde}: la casilla de «{t.label}» se sale de su fila");
                    Assert.That(caja.worldBound.xMax, Is.LessThanOrEqualTo(panel.worldBound.xMax - panel.resolvedStyle.paddingRight + 0.5f), $"{donde}: la casilla de «{t.label}» se sale por la derecha");
                }
                foreach (var l in panel.Query<Label>().ToList().Where(l => !string.IsNullOrEmpty(l.text) && l.resolvedStyle.whiteSpace == WhiteSpace.NoWrap))
                    Assert.That(l.MeasureTextSize(l.text, 0, VisualElement.MeasureMode.Undefined, 0, VisualElement.MeasureMode.Undefined).x,
                        Is.LessThanOrEqualTo(l.layout.width + 1f), $"{donde}: «{l.text}» cortado");
                var botones = panel.Children().Last().Query<UnityEngine.UIElements.Button>().ToList();
                Assert.That(botones.Select(b => Mathf.Round(b.worldBound.y)).Distinct().Count(), Is.EqualTo(1), $"{donde}: los tres botones en una fila");
                Assert.That(cajaLectura.worldBound.Overlaps(panel.worldBound), Is.False, $"{donde}: la lectura no pisa el panel");
                foreach (var c in pisa.Children())
                    Assert.That(cajaLectura.worldBound.Overlaps(c.worldBound), Is.False, $"{donde}: la lectura no pisa el panel de la izquierda");
                // los desplegables, cerrados: texto claro sobre fondo oscuro
                var desplegables = panel.Query<UnityEngine.UIElements.DropdownField>().ToList();
                Assert.That(desplegables.Count, Is.EqualTo(7));
                foreach (var dd in desplegables)
                {
                    var texto = dd.Q<UnityEngine.UIElements.TextElement>(className: "unity-base-popup-field__text");
                    var fondo = dd.Q(className: "unity-base-popup-field__input");
                    Assert.That(Luz(texto.resolvedStyle.color) - Luz(fondo.resolvedStyle.backgroundColor), Is.GreaterThan(0.5f), $"{donde}: «{dd.label}» cerrado se lee");
                    Assert.That(texto.MeasureTextSize(texto.text, 0, VisualElement.MeasureMode.Undefined, 0, VisualElement.MeasureMode.Undefined).y,
                        Is.LessThanOrEqualTo(fondo.contentRect.height + 1f), $"{donde}: «{texto.text}» cabe de alto en «{dd.label}»");
                }
                if (!string.IsNullOrEmpty(carpeta)) yield return Foto(rt, Path.Combine(carpeta, $"pd_{ancho}_cerrado.png"));
                // abierto: la lista de «tipo (todas)», con la flecha del teclado (como al darle)
                var abrir = desplegables.First(x => x.label.Contains("tipo (todas)"));
                abrir.Focus();
                using (var ev = UnityEngine.UIElements.NavigationSubmitEvent.GetPooled()) { ev.target = abrir; abrir.SendEvent(ev); }
                for (int i = 0; i < 10; i++) yield return null;
                var lista = raiz.panel.visualTree.Q(className: "unity-base-dropdown");
                Assert.That(lista, Is.Not.Null, $"{donde}: la lista se abre");
                var dentro = lista.Q(className: "unity-base-dropdown__container-outer") ?? lista;
                Assert.That(dentro.worldBound.xMin >= -0.5f && dentro.worldBound.xMax <= raiz.worldBound.xMax + 0.5f && dentro.worldBound.yMax <= raiz.worldBound.yMax + 0.5f,
                    $"{donde}: la lista abierta, entera en la pantalla ({dentro.worldBound})");
                var fondoLista = lista.Q(className: "unity-base-dropdown__container-inner");
                var rotulos = lista.Query<UnityEngine.UIElements.Label>(className: "unity-base-dropdown__label").ToList();
                Assert.That(rotulos.Count, Is.EqualTo(abrir.choices.Count), $"{donde}: todas las opciones a la vista");
                foreach (var l in rotulos)
                    Assert.That(Luz(l.resolvedStyle.color) - Luz(fondoLista.resolvedStyle.backgroundColor), Is.GreaterThan(0.5f), $"{donde}: «{l.text}» se lee en la lista");
                Assert.That(lista.parent.IndexOf(lista), Is.EqualTo(lista.parent.childCount - 1), $"{donde}: la lista, encima de todo");
                if (!string.IsNullOrEmpty(carpeta)) yield return Foto(rt, Path.Combine(carpeta, $"pd_{ancho}_abierto.png"));
                using (var ev = UnityEngine.UIElements.NavigationCancelEvent.GetPooled()) { ev.target = lista; lista.SendEvent(ev); }
                for (int i = 0; i < 5; i++) yield return null;
                if (lista.panel != null) lista.RemoveFromHierarchy();
                // plegadas: las tres cajas a solo su título (no tapan ni comen clics) y sin la lectura; y abiertas otra vez, como estaban
                tipo.GetMethod("Plegar").Invoke(d, new object[] { true });
                for (int i = 0; i < 5; i++) yield return null;
                Assert.That(panel.worldBound.height, Is.LessThan(70), $"{donde}: el panel derecho, plegado al título");
                foreach (var c in pisa.Children())
                    Assert.That(c.worldBound.height, Is.LessThan(70), $"{donde}: las de la izquierda, plegadas al título");
                Assert.That(cajaLectura.resolvedStyle.display, Is.EqualTo(DisplayStyle.None), $"{donde}: la lectura, plegada con ellas");
                tipo.GetMethod("Plegar").Invoke(d, new object[] { false });
                for (int i = 0; i < 5; i++) yield return null;
                EnColumna(panel, panel.contentRect.height + panel.resolvedStyle.paddingTop);
                mapa.Vista.targetTexture = null;
                doc.panelSettings.targetTexture = null;
                rt.Release();
            }
        }

        static string Nombre(UnityEngine.UIElements.VisualElement v) =>
            v is UnityEngine.UIElements.Label l ? l.text : v is UnityEngine.UIElements.BaseField<float> s ? s.label : v is UnityEngine.UIElements.BaseField<bool> t ? t.label
            : v is UnityEngine.UIElements.BaseField<string> f ? f.label : v.GetType().Name;

        static IEnumerator Foto(RenderTexture rt, string ruta)
        {
            yield return new WaitForSecondsRealtime(1.5f);
            var antes = RenderTexture.active;
            RenderTexture.active = rt;
            var t = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            t.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            t.Apply();
            RenderTexture.active = antes;
            File.WriteAllBytes(ruta, t.EncodeToPNG());
            UnityEngine.Object.Destroy(t);
        }

        static IEnumerator CapturasDeCaminos()
        {
            var carpeta = Environment.GetEnvironmentVariable("MUNDO_CAPTURAS");
            Directory.CreateDirectory(carpeta);
            var tipo = Tipo();
            Component d = null;
            var hasta = Time.realtimeSinceStartup + 120;
            while (Time.realtimeSinceStartup < hasta)
            {
                d = d != null ? d : UnityEngine.Object.FindAnyObjectByType(tipo) as Component;
                if (d != null && (bool)tipo.GetProperty("Lista").GetValue(d)) break;
                yield return null;
            }
            var mapa = (MapaDeMundo)tipo.GetProperty("Mapa").GetValue(d);
            var rt = new RenderTexture(1920, 1080, 24) { antiAliasing = 4 };
            mapa.Vista.targetTexture = rt;
            var doc = UnityEngine.Object.FindAnyObjectByType<UnityEngine.UIElements.UIDocument>();
            if (doc != null) doc.panelSettings.targetTexture = rt;
            void Fijar(string k, double v) => tipo.GetMethod("Fijar").Invoke(d, new object[] { k, v });
            IEnumerator Foto(string nombre)
            {
                var fin = Time.realtimeSinceStartup + 60;
                yield return null;
                while (!mapa.SueloCompleto && Time.realtimeSinceStartup < fin) yield return null;
                yield return new WaitForSecondsRealtime(3f);
                var antes = RenderTexture.active;
                RenderTexture.active = rt;
                var t = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                t.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                t.Apply();
                RenderTexture.active = antes;
                File.WriteAllBytes(Path.Combine(carpeta, nombre + ".png"), t.EncodeToPNG());
                UnityEngine.Object.Destroy(t);
            }
            var e = EscalaDelMapa.MetrosPorUnidad;
            var texto = new System.Text.StringBuilder();
            foreach (var (pasada, aldeas, tipoAldea, pueblos) in new[] { ("a", 6, 7, 0), ("b", 1, 6, 5) })
            {
                tipo.GetMethod("Restablecer").Invoke(d, null);
                tipo.GetMethod("CargarPropuesta").Invoke(d, null);
                Fijar("nivel", 5);
                Fijar("pueblos", 0);
                Fijar("aldeas", aldeas);
                Fijar("tipoAldea", tipoAldea);
                Fijar("pueblos", pueblos);
                Fijar("tipoPueblo", 5);
                // (sin los anillos ni los paneles: que se vean los caminos; el panel, en una toma aparte)
                var anillos = (IDictionary<string, bool>)tipo.GetField("Anillos").GetValue(d);
                foreach (var k in anillos.Keys.ToList()) anillos[k] = false;
                Fijar("nivel", 5);
                for (int i = 0; i < 12; i++) yield return null;
                tipo.GetMethod("VerPanel").Invoke(d, new object[] { true });
                tipo.GetMethod("EnPlanta").Invoke(d, new object[] { false });
                yield return new WaitForSecondsRealtime(3.5f);
                yield return Foto($"cm_{pasada}_panel");
                tipo.GetMethod("VerPanel").Invoke(d, new object[] { false });
                tipo.GetMethod("EnPlanta").Invoke(d, new object[] { true });
                yield return Foto($"cm_{pasada}_planta");
                tipo.GetMethod("EnPlanta").Invoke(d, new object[] { false });
                yield return new WaitForSecondsRealtime(3.5f);
                yield return Foto($"cm_{pasada}_juego");
                texto.AppendLine($"== pasada {pasada}").AppendLine((string)tipo.GetMethod("Valores").Invoke(d, null));
                // cada aldea y cada pueblo de cerca, con el camino entrando (la cámara del lado de la plaza, mirando hacia fuera)
                var puesto = (IList)tipo.GetField("Puesto").GetValue(d);
                var lista = puesto.Cast<object>().Select(q => ((string)q.GetType().GetField("Item1").GetValue(q), (double)q.GetType().GetField("Item4").GetValue(q),
                    (double)q.GetType().GetField("Item5").GetValue(q))).ToList();
                var plaza = lista.First(q => q.Item1 == "dp-plaza");
                foreach (var (id, x, y) in lista.Where(q => q.Item1.StartsWith("dp-aldea") || q.Item1.StartsWith("dp-pueblo")))
                {
                    double ax = x - plaza.Item2, ay = y - plaza.Item3, l = Math.Sqrt(ax * ax + ay * ay);
                    (ax, ay) = (ax / l, ay / l);
                    double h = mapa.AlturaVisible(x, y) / e;
                    MapaDeMundo.CamaraFija = (EscalaDelMapa.AEscena(x - ax * 6.5, y - ay * 6.5, (h + 5.5) * e), EscalaDelMapa.AEscena(x, y, h * e));
                    yield return Foto($"cm_{pasada}_{id}");
                }
                MapaDeMundo.CamaraFija = null;
            }
            File.WriteAllText(Path.Combine(carpeta, "cm_valores.txt"), texto.ToString());
            mapa.Vista.targetTexture = null;
        }

        /// <summary>Las capturas de la página (en `MUNDO_CAPTURAS`): niveles 1, 3 y 5 con todas sus aldeas lo más cerca que deja el motor y un
        /// pueblo a 3 u, a × 1 y a × 2 de lo fijo, en planta y con la cámara del juego, con el panel; y la tabla de holguras de cada una.</summary>
        [UnityTest, Category("Mapa"), Explicit("capturas para la página")]
        public IEnumerator LasDistanciasEnCapturas()
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MUNDO_CAPTURAS"))) Assert.Ignore("sin MUNDO_CAPTURAS");
            EditorSceneManager.OpenScene(Escena, OpenSceneMode.Single);
            yield return new EnterPlayMode();
            yield return Capturas();
            yield return new ExitPlayMode();
        }

        static IEnumerator Capturas()
        {
            var carpeta = Environment.GetEnvironmentVariable("MUNDO_CAPTURAS");
            Directory.CreateDirectory(carpeta);
            var tipo = Tipo();
            Component d = null;
            var hasta = Time.realtimeSinceStartup + 120;
            while (Time.realtimeSinceStartup < hasta)
            {
                d = d != null ? d : UnityEngine.Object.FindAnyObjectByType(tipo) as Component;
                if (d != null && (bool)tipo.GetProperty("Lista").GetValue(d)) break;
                yield return null;
            }
            var mapa = (MapaDeMundo)tipo.GetProperty("Mapa").GetValue(d);
            var rt = new RenderTexture(1920, 1080, 24) { antiAliasing = 4 };
            mapa.Vista.targetTexture = rt;
            var doc = UnityEngine.Object.FindAnyObjectByType<UnityEngine.UIElements.UIDocument>();
            if (doc != null) doc.panelSettings.targetTexture = rt;
            void Fijar(string k, double v) => tipo.GetMethod("Fijar").Invoke(d, new object[] { k, v });
            IEnumerator Foto(string nombre)
            {
                var fin = Time.realtimeSinceStartup + 60;
                yield return null;
                while (!mapa.SueloCompleto && Time.realtimeSinceStartup < fin) yield return null;
                yield return new WaitForSecondsRealtime(3f);
                var antes = RenderTexture.active;
                RenderTexture.active = rt;
                var t = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                t.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                t.Apply();
                RenderTexture.active = antes;
                File.WriteAllBytes(Path.Combine(carpeta, nombre + ".png"), t.EncodeToPNG());
                UnityEngine.Object.Destroy(t);
            }
            var texto = new System.Text.StringBuilder();
            foreach (int n in new[] { 1, 3, 5 })
                foreach (var (f, z) in new[] { (1.0, "x1"), (2.0, "x2") })
                {
                    tipo.GetMethod("Restablecer").Invoke(d, null);
                    Fijar("nivel", n);
                    Fijar("fijos", f);
                    // (el cupo es de pueblos: todas las aldeas menos una, y un pueblo extractor a 3 u)
                    Fijar("pueblos", 0);
                    Fijar("aldeas", n);
                    Fijar("pueblos", 1);
                    Fijar("distanciaPueblo", 3);
                    for (int i = 0; i < 12; i++) yield return null;
                    // (las seis que pide la página: nivel 1 a × 2 del juego; 3 a × 1 en planta y del juego; 5 a × 1 del juego; 5 a × 2 en planta y del juego)
                    bool planta = false, juego = false;
                    if (planta)
                    {
                        tipo.GetMethod("EnPlanta").Invoke(d, new object[] { true });
                        yield return Foto($"dp_{n}_{z}_planta");
                    }
                    tipo.GetMethod("EnPlanta").Invoke(d, new object[] { false });
                    yield return new WaitForSecondsRealtime(3.5f); // (el encuadre se comprueba en pantalla: habitual y, si no entra, alejado)
                    if (juego) yield return Foto($"dp_{n}_{z}_juego");
                    texto.AppendLine($"== nivel {n}, lo fijo × {f}").AppendLine((string)tipo.GetMethod("Valores").Invoke(d, null));
                }
            File.WriteAllText(Path.Combine(carpeta, "dp_holguras.txt"), texto.ToString());
            tipo.GetMethod("Restablecer").Invoke(d, null);
            Fijar("nivel", 3);
            Fijar("fijos", 1);
            Fijar("pueblos", 0);
            Fijar("aldeas", 3);
            Fijar("pueblos", 1);
            Fijar("distanciaPueblo", 3);
            for (int i = 0; i < 12; i++) yield return null;
            tipo.GetMethod("EnPlanta").Invoke(d, new object[] { true });
            yield return Foto("dp_motor_1_del_motor");
            // (la separación de aldea y el radio de puerta, hasta que la tabla deja de marcar esos pares)
            tipo.GetMethod("FijarMotor").Invoke(d, new object[] { "aldea a plaza", 6.0, 0 });
            tipo.GetMethod("FijarMotor").Invoke(d, new object[] { "radio de puerta", 4.0, 0 });
            Fijar("distanciaPueblo", 6);
            for (int i = 0; i < 12; i++) yield return null;
            tipo.GetMethod("EnPlanta").Invoke(d, new object[] { true });
            yield return Foto("dp_motor_2_cambiados");
            File.WriteAllText(Path.Combine(carpeta, "dp_motor_cambiados.txt"), (string)tipo.GetMethod("Valores").Invoke(d, null));
            tipo.GetMethod("VolverAlMotor").Invoke(d, null);
            {
                tipo.GetMethod("Restablecer").Invoke(d, null);
                tipo.GetMethod("CargarPropuesta").Invoke(d, null);
                Fijar("nivel", 5);
                Fijar("pueblos", 0);
                Fijar("aldeas", 4);
                Fijar("pueblos", 2);
                Fijar("distanciaPueblo", 0);
                for (int i = 0; i < 12; i++) yield return null;
                tipo.GetMethod("EnPlanta").Invoke(d, new object[] { true });
                yield return Foto("dp_separaciones_5_planta");
                File.WriteAllText(Path.Combine(carpeta, "dp_separaciones_5_planta.txt"), (string)tipo.GetMethod("Valores").Invoke(d, null));
                // (el detalle: entre la aldea 1 y el pueblo 1)
                var puestoD = (IList)tipo.GetField("Puesto").GetValue(d);
                (double x, double y) Donde(string id) { var q = puestoD.Cast<object>().First(o => (string)o.GetType().GetField("Item1").GetValue(o) == id); return ((double)q.GetType().GetField("Item4").GetValue(q), (double)q.GetType().GetField("Item5").GetValue(q)); }
                var (ax, ay) = Donde("dp-aldea-1");
                var (px, py) = Donde("dp-pueblo-1");
                tipo.GetMethod("Detalle").Invoke(d, new object[] { (ax + px) / 2, (ay + py) / 2, 11.0 });
                yield return Foto("dp_separaciones_detalle");
            }
            foreach (var (n, planta) in new[] { (5, true), (5, false), (3, false) })
            {
                tipo.GetMethod("Restablecer").Invoke(d, null);
                tipo.GetMethod("CargarPropuesta").Invoke(d, null);
                Fijar("nivel", n);
                Fijar("pueblos", 0);
                Fijar("aldeas", n);
                Fijar("pueblos", 1);
                Fijar("distanciaPueblo", 0);
                for (int i = 0; i < 12; i++) yield return null;
                tipo.GetMethod("EnPlanta").Invoke(d, new object[] { planta });
                if (!planta) yield return new WaitForSecondsRealtime(3.5f);
                yield return Foto($"dp_propuesta_{n}_{(planta ? "planta" : "juego")}");
                File.WriteAllText(Path.Combine(carpeta, $"dp_propuesta_{n}_{(planta ? "planta" : "juego")}.txt"), (string)tipo.GetMethod("Valores").Invoke(d, null));
            }
            tipo.GetMethod("VolverAlMotor").Invoke(d, null);
            mapa.Vista.targetTexture = null;
        }
    }
}
