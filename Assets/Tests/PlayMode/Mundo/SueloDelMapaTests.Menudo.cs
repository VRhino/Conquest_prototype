// La parte 5 y lo menudo en el mapa («Mejorado»): avanzadas por etapa y estado (y su mejora, su reparación y su obra), las obras genéricas,
// los campamentos de mercenarios y de bandidos, los alijos y la batalla con su humo; las capturas para la página y el coste.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BronzeAge.Contract.V1;
using ConquestTactics.Mundo;
using ConquestTactics.Mundo.Mapa;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using AmbitoPb = BronzeAge.Contract.V1.Ambito;

namespace ConquestTactics.Mundo.UI.Tests
{
    public partial class SueloDelMapaTests
    {
        /// <summary>Lo de la parte 5 y lo menudo en un banco, cada cosa en tierra firme de pradera, con su plaza madre al norte. Cada cosa: su
        /// entidad (y su obra) y un nombre para las capturas.</summary>
        sealed class BancoDeMenudo
        {
            public readonly Foto Vista = new Foto { Ambito = new AmbitoPb { Tipo = AmbitoTipo.AmbitoVista, Id = "f-1" }, Version = 2 };
            public readonly List<(string nombre, (double x, double y) donde, bool vueltas)> Sitios = new List<(string, (double, double), bool)>();
            int n;

            public string Plaza(double x, double y, uint nivel = 2)
            {
                string id = $"lm-as{n++}";
                Vista.Entidades.Add(new Entidad { Asentamiento = new Asentamiento { Id = id, Nombre = id, Nivel = nivel, FaccionId = "f-1", Posicion = Pu(x, y) } });
                return id;
            }

            public void Avanzada(string nombre, (double x, double y) p, uint etapa, bool ruinas, TipoObra obra = TipoObra.NoEspecificado, bool enMarcha = false, bool vueltas = false)
            {
                string madre = Plaza(p.x, p.y + 6, 2);
                string id = $"lm-av{n++}";
                Vista.Entidades.Add(new Entidad { Avanzada = new Avanzada { Id = id, AsentamientoMadreId = madre, Etapa = etapa, Posicion = Pu(p.x, p.y),
                    Estado = ruinas ? EstadoAvanzada.AvanzadaEnRuinas : EstadoAvanzada.AvanzadaActiva } });
                if (obra != TipoObra.NoEspecificado)
                {
                    var o = new ObraDeMapa { Id = $"lm-ob{n++}", AsentamientoMadreId = madre, Tipo = obra, Posicion = Pu(p.x, p.y), DestinoId = id };
                    if (enMarcha) { o.IniciaEnMs = 1; o.TerminaEnMs = 9_999_999_999_999; }
                    Vista.Entidades.Add(new Entidad { Obra = o });
                }
                Sitios.Add((nombre, p, vueltas));
            }

            public void Obra(string nombre, (double x, double y) p, TipoObra tipo, bool enMarcha)
            {
                string madre = Plaza(p.x, p.y + 5, 1);
                var o = new ObraDeMapa { Id = $"lm-ob{n++}", AsentamientoMadreId = madre, Tipo = tipo, Posicion = Pu(p.x, p.y) };
                if (enMarcha) { o.IniciaEnMs = 1; o.TerminaEnMs = 9_999_999_999_999; }
                Vista.Entidades.Add(new Entidad { Obra = o });
                Sitios.Add((nombre, p, false));
            }

            public void Mercenarios(string nombre, (double x, double y) p, uint origen, string militar, bool vueltas)
            {
                var k = new CampamentoMercenarios { Id = $"lm-me{n++}", Posicion = Pu(p.x, p.y), Origen = origen };
                k.Edificios.AddRange(new[] { "taberna", "vivienda", "mercado", militar });
                Vista.Entidades.Add(new Entidad { CampamentoMercenarios = k });
                Sitios.Add((nombre, p, vueltas));
            }

            public void Bandidos(string nombre, (double x, double y) p, bool veta, uint nivel, bool vueltas)
            {
                var b = new CampamentoBandidos { Id = $"lm-ba{n++}", Posicion = Pu(p.x, p.y), Nivel = nivel, BosqueId = veta ? "" : "bosque-banco" };
                if (veta) b.VetaId = "veta-banco";
                Vista.Entidades.Add(new Entidad { CampamentoBandidos = b });
                Sitios.Add((nombre, p, vueltas));
            }

            public void Alijo(string nombre, (double x, double y) p, string recurso)
            {
                Vista.Entidades.Add(new Entidad { Alijo = new Alijo { Id = $"lm-al{n++}", Posicion = Pu(p.x, p.y), Recurso = recurso, Cantidad = 1000 } });
                Sitios.Add((nombre, p, false));
            }

            public void Batalla(string nombre, (double x, double y) p, bool vueltas)
            {
                Vista.Entidades.Add(new Entidad { Batalla = new Batalla { Id = $"lm-bt{n++}", Punto = Pu(p.x, p.y), Estado = EstadoBatalla.BatallaEnCurso } });
                Sitios.Add((nombre, p, vueltas));
            }
        }

        /// <summary>Un sitio libre cerca de `p` (tierra firme, sin agua, bosque espeso, ríos ni nodos a menos de `r`), movido lo poco que haga falta
        /// para que su semilla dé lo que pide `vale` (la variante de la ciudadela o del alijo).</summary>
        Func<(double x, double y), double, Func<double, bool>, (double x, double y)> Buscador(Relieve relieve, ArtefactoDeMundo artefacto)
        {
            var bosque = new RasterDeMundo(artefacto.DensidadBosque);
            var cauces = new CaucesDelMapa(artefacto, relieve);
            var puestos = new List<(double x, double y, double r)>();
            bool Libre(double x, double y, double r)
            {
                for (double dy = -r; dy <= r; dy += 0.5)
                    for (double dx = -r; dx <= r; dx += 0.5)
                        if (dx * dx + dy * dy <= r * r && (relieve.AguaEn(x + dx, y + dy) || bosque.En(x + dx, y + dy) > 25)) return false;
                if (cauces.ACauce(x, y, r + 4) < r + 2) return false;
                if (artefacto.Nodos.Any(k => k.Posicion != null && Math.Abs(k.Posicion.X / 1000.0 - x) < r && Math.Abs(k.Posicion.Y / 1000.0 - y) < r)) return false;
                return puestos.All(q => Math.Sqrt((q.x - x) * (q.x - x) + (q.y - y) * (q.y - y)) > q.r + r);
            }
            return (p, r, vale) =>
            {
                for (double d = 0; d <= 260; d += 1.5)
                    for (int k = 0; k < Math.Max(1, (int)(d * 2)); k++)
                    {
                        double a = k * Math.PI * 2 / Math.Max(1, (int)(d * 2)), x = p.x + d * Math.Cos(a), y = p.y + d * Math.Sin(a);
                        if (!Libre(x, y, r)) continue;
                        for (int t = 0; t < 40; t++)
                        {
                            double xx = x + t * 0.071;
                            if (vale == null || vale(MaquetasDelPrototipo.Semilla(xx, y))) { puestos.Add((xx, y, r)); return (xx, y); }
                        }
                    }
                puestos.Add((p.x, p.y, r));
                return p;
            };
        }

        /// <summary>Las capturas de la parte 5 y lo menudo en el mapa («Mejorado», `MUNDO_CAPTURAS`, «lm_…», a 1920×1080 para recortarlas al 100 %):
        /// de cada cosa una de cerca, una a zoom habitual y una alejada, con el nivel de detalle que se ve en cada una; la vuelta en cuatro vistas
        /// (de cerca) de la atalaya, el fuerte, las dos ciudadelas, los mercenarios, los bandidos 7a‑3 y 7b‑3 y la batalla; y, del mundo real (el
        /// artefacto), un campamento de mercenarios, uno de bandidos en bosque y un alijo, con sus árboles alrededor.</summary>
        [Category("Mapa"), Explicit("capturas de la parte 5 y lo menudo")]
        [UnityTest, Timeout(12_000_000)]
        [Category("Visual")]  // (arte del mapa: en la pasada de antes del push, no en la de cada commit)
        public IEnumerator LoMenudoEnElMapa()
        {
            var url = Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_URL");
            if (string.IsNullOrEmpty(url)) Assert.Ignore("sin BRONZEAGE_PRUEBA_URL: no hay servidor de prueba");
            yield return Preparar(url, "lo-menudo-");
            var (relieve, artefacto, datos) = (pRelieve, pArtefacto, pDatos);
            var caminos = BancoDeCaminos.Hacer(relieve, artefacto, datos);
            Sigilos.Cargar(Resources.Load<TextAsset>("MundoUI/Sigilo/catalogoSigilos").text, "{}", "{}");
            rt = new RenderTexture(1920, 1080, 24) { antiAliasing = 4 };
            var sitio = Buscador(relieve, artefacto);
            var pradera = caminos.SitiosDeSuelo["suelo_pradera"];
            var banco = new BancoDeMenudo();
            Func<double, bool> Ciudadela(int v) => s => MaquetasDelMapa.VarianteDeCiudadela(s) == v;
            Func<double, bool> Alijo(string m) => s => MaquetasDelMapa.ModeloDeAlijo(s) == m;
            // las avanzadas: entera, en ruina, en obras (la mejora en marcha), la mejora pendiente y la reparación
            banco.Avanzada("atalaya", sitio(pradera, 4, null), 1, false, vueltas: true);
            banco.Avanzada("fuerte", sitio(pradera, 4, null), 2, false, vueltas: true);
            banco.Avanzada("ciudadela_1", sitio(pradera, 4, Ciudadela(1)), 3, false, vueltas: true);
            banco.Avanzada("ciudadela_2", sitio(pradera, 4, Ciudadela(2)), 3, false, vueltas: true);
            banco.Avanzada("fuerte_obras", sitio(pradera, 4, null), 1, false, TipoObra.ObraMejoraAvanzada, true);
            banco.Avanzada("ciudadela_1_obras", sitio(pradera, 4, Ciudadela(1)), 2, false, TipoObra.ObraMejoraAvanzada, true);
            banco.Avanzada("ciudadela_2_obras", sitio(pradera, 4, Ciudadela(2)), 2, false, TipoObra.ObraMejoraAvanzada, true);
            banco.Avanzada("fuerte_ruina", sitio(pradera, 4, null), 2, true);
            banco.Avanzada("ciudadela_1_ruina", sitio(pradera, 4, Ciudadela(1)), 3, true);
            banco.Avanzada("ciudadela_2_ruina", sitio(pradera, 4, Ciudadela(2)), 3, true);
            banco.Avanzada("fuerte_mejora_pendiente", sitio(pradera, 4, null), 2, false, TipoObra.ObraMejoraAvanzada, false);
            banco.Avanzada("fuerte_ruina_reparacion", sitio(pradera, 4, null), 2, true, TipoObra.ObraReparacion, true);
            banco.Avanzada("ciudadela_2_ruina_reparacion", sitio(pradera, 4, Ciudadela(2)), 3, true, TipoObra.ObraReparacion, false);
            // las obras genéricas
            banco.Obra("obra_estacas", sitio(pradera, 3, null), TipoObra.ObraAvanzada, false);
            banco.Obra("obra_mitad", sitio(pradera, 3, null), TipoObra.ObraPueblo, true);
            // los campamentos (los de mercenarios, lejos de todo lo demás y entre sí: el anillo azul de su protección no cruza otras cosas)
            double rp = datos.Numero("documentos.Campamentos_Mercenarios.MERCENARIOS.radioProteccion") + 8;
            banco.Mercenarios("mercenarios_crudo_barracon", sitio((pradera.x + 3 * rp, pradera.y), 5, null), 0, "barracon", true);
            banco.Mercenarios("mercenarios_rayado_tiro", sitio((pradera.x + 3 * rp, pradera.y + 2.2 * rp), 5, null), 1, "galeriaDeTiro", false);
            banco.Mercenarios("mercenarios_rubia_caballerizas", sitio((pradera.x + 3 * rp, pradera.y - 2.2 * rp), 5, null), 2, "caballerizas", false);
            foreach (var veta in new[] { false, true })
                for (uint nv = 1; nv <= 3; nv++)
                    banco.Bandidos($"bandidos_{(veta ? "7b" : "7a")}_{nv}", sitio(pradera, 4, null), veta, nv, nv == 3);
            foreach (var m in new[] { "alijo_1", "alijo_2" })
                foreach (var rec in new[] { "trigo", "madera", "piedra", "oro" })
                    banco.Alijo($"{m}_{rec}", sitio(pradera, 2, Alijo(m)), rec);
            banco.Batalla("batalla", sitio(pradera, 4, null), true);
            // del mundo real: un campamento de mercenarios, uno de bandidos en bosque y un alijo, con sus árboles
            var reales = new List<(string nombre, (double x, double y) donde)>();
            // (el resto del banco, a más de su radio de protección de los campamentos de mercenarios)
            foreach (var k in banco.Sitios.Where(q => !q.nombre.StartsWith("mercenarios")))
                foreach (var m in banco.Sitios.Where(q => q.nombre.StartsWith("mercenarios")))
                    Assert.That(Math.Sqrt(Math.Pow(k.donde.x - m.donde.x, 2) + Math.Pow(k.donde.y - m.donde.y, 2)), Is.GreaterThan(rp - 8), $"{k.nombre}, en el anillo de {m.nombre}");
            var cm = artefacto.Campamentos.FirstOrDefault();
            if (cm != null) { banco.Vista.Entidades.Add(new Entidad { CampamentoMercenarios = cm.Clone() }); reales.Add(("real_mercenarios", (cm.Posicion.X / 1000.0, cm.Posicion.Y / 1000.0))); }
            var cb = artefacto.BandidosIniciales.FirstOrDefault(b => !string.IsNullOrEmpty(b.BosqueId) && string.IsNullOrEmpty(b.VetaId)) ?? artefacto.BandidosIniciales.FirstOrDefault();
            if (cb != null) { banco.Vista.Entidades.Add(new Entidad { CampamentoBandidos = cb.Clone() }); reales.Add(("real_bandidos", (cb.Posicion.X / 1000.0, cb.Posicion.Y / 1000.0))); }
            var al = artefacto.Alijos.FirstOrDefault();
            if (al != null) { banco.Vista.Entidades.Add(new Entidad { Alijo = al.Clone() }); reales.Add(("real_alijo", (al.Posicion.X / 1000.0, al.Posicion.Y / 1000.0))); }

            var antes = (MapaDeMundo.AclararCopas, MaquetasDelMapa.Estilo, NodosDelMapa.Estilo);
            MapaDeMundo.AclararCopas = false;
            NodosDelMapa.Estilo = EstiloDeNodos.Mejorado;
            MaquetasDelMapa.Estilo = EstiloDeAsentamientos.Mejorado;
            try
            {
                var (almacen, sesion) = Almacen(url, banco.Sitios[0].donde, null);
                almacen.Aplicar(new MensajeDeServidor { Foto = banco.Vista });
                var mapa = Montar(almacen, sesion, relieve, artefacto, datos);
                mapa.RadioVisionFijo = 15;
                Assert.That(mapa.Maquetas.HayMenudos, "faltan los modelos de lo menudo en Resources");
                (double x, double y) aqui = default;
                // (lo de la cosa del centro: el nivel de detalle y el material)
                string Niveles()
                {
                    var c = EscalaDelMapa.AEscena(aqui.x, aqui.y, 0);
                    var e = mapa.Maquetas.Encargos(mapa.Vista).Where(k => !k.EsSuelo && !k.Material.name.StartsWith("plaza") && !k.Material.name.StartsWith("lm-"))
                        .OrderBy(k => Vector2.Distance(new Vector2(k.M.m03, k.M.m23), new Vector2(c.x, c.z))).FirstOrDefault();
                    return e.Malla == null ? "—" : $"{e.Nivel} ({e.Material.name})";
                }
                // (`MUNDO_MENUDO_SOLO`: solo esas cosas, separadas por comas)
                var solo = Environment.GetEnvironmentVariable("MUNDO_MENUDO_SOLO");
                foreach (var (nombre, donde, vueltas) in banco.Sitios.Concat(reales.Select(r => (r.nombre, r.donde, r.nombre == "real_alijo"))))
                {
                    if (!string.IsNullOrEmpty(solo) && !solo.Split(',').Contains(nombre)) continue;
                    aqui = donde;
                    yield return Llegar(mapa, almacen, donde);
                    yield return new WaitForSecondsRealtime(1);
                    EsconderLaColumna();
                    var lod = new List<string>();
                    foreach (var (zoom, z) in new[] { (ZoomDelMapa.Cerca, "1_cerca"), (ZoomDelMapa.Normal, "2_habitual"), (ZoomDelMapa.Alejado, "3_alejado") })
                    {
                        yield return Vista(mapa, zoom, 30, $"lm_{nombre}_{z}");
                        lod.Add($"{z} {Niveles()}");
                    }
                    if (vueltas)
                        foreach (var (giro, g) in new[] { (Math.PI / 2, "4_este"), (Math.PI, "5_detras"), (Math.PI * 1.5, "6_oeste") })
                        {
                            mapa.Camara.Giro = giro;
                            yield return Vista(mapa, ZoomDelMapa.Cerca, 30, $"lm_{nombre}_{g}");
                        }
                    mapa.Camara.Giro = 0;
                    // (la batalla: cuatro instantes seguidos de cerca, para ver que la animación del humo no salta ni se repite a la vista)
                    if (nombre == "batalla")
                        for (int k = 1; k <= 4; k++) yield return Vista(mapa, ZoomDelMapa.Cerca, 30, $"lm_{nombre}_7_instante_{k}");
                    Debug.Log($"[LoMenudo] {nombre} ({donde.x:0.0}, {donde.y:0.0}): {string.Join("; ", lod)}; humaredas {mapa.Maquetas.Humaredas}");
                }
            }
            finally
            {
                (MapaDeMundo.AclararCopas, MaquetasDelMapa.Estilo, NodosDelMapa.Estilo) = antes;
            }
        }

        /// <summary>La tira de comparación de tamaños a zoom habitual, en la misma captura («lm_escala_…»): un pueblo de nivel 1 (cantera sobre su
        /// nodo), una plaza de nivel 1, la atalaya y el fuerte a ×1 y a ×1,35 (la escala del mapa que llevan) y una ciudadela, en fila a 2,6 u.</summary>
        [Category("Mapa"), Explicit("tira de tamaños de las avanzadas")]
        [UnityTest, Timeout(3_000_000)]
        [Category("Visual")]  // (arte del mapa: en la pasada de antes del push, no en la de cada commit)
        public IEnumerator LaEscalaDeLasAvanzadas()
        {
            var url = Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_URL");
            if (string.IsNullOrEmpty(url)) Assert.Ignore("sin BRONZEAGE_PRUEBA_URL: no hay servidor de prueba");
            yield return Preparar(url, "lo-menudo-escala-");
            var (relieve, artefacto, datos) = (pRelieve, pArtefacto, pDatos);
            var caminos = BancoDeCaminos.Hacer(relieve, artefacto, datos);
            Sigilos.Cargar(Resources.Load<TextAsset>("MundoUI/Sigilo/catalogoSigilos").text, "{}", "{}");
            rt = new RenderTexture(1920, 1080, 24) { antiAliasing = 4 };
            // (un sitio llano de pradera, sin roca: de 13 u de radio, con menos de 6 m de desnivel; la fila sale recta y en la hierba)
            var p0 = caminos.SitiosDeSuelo["suelo_pradera"];
            var bosqueE = new RasterDeMundo(artefacto.DensidadBosque);
            double costa = datos.Numero("documentos.Generacion_Del_Mundo.ELEVACION.umbralCosta") * Relieve.MetrosAltura;
            bool Llano(double x, double y)
            {
                double hMin = double.MaxValue, hMax = double.MinValue;
                for (double dy = -13; dy <= 13; dy += 1)
                    for (double dx = -13; dx <= 13; dx += 1)
                    {
                        if (relieve.AguaEn(x + dx, y + dy) || bosqueE.En(x + dx, y + dy) > 25) return false;
                        double h = relieve.AlturaEn(x + dx, y + dy);
                        if (h < costa + 12) return false; // (ni la orilla)
                        hMin = Math.Min(hMin, h); hMax = Math.Max(hMax, h);
                        if (hMax - hMin > 6) return false;
                    }
                return true;
            }
            (double, double) llano = p0;
            for (double d = 0, encontrado = 0; d <= 300 && encontrado == 0; d += 4)
                for (int k = 0; k < Math.Max(1, (int)(d / 2)); k++)
                {
                    double ang = k * Math.PI * 2 / Math.Max(1, (int)(d / 2)), x = p0.x + d * Math.Cos(ang), y = p0.y + d * Math.Sin(ang);
                    if (Llano(x, y)) { llano = (x, y); encontrado = 1; break; }
                }
            // (9 u al este: en este mundo, al oeste del sitio llano hay una orilla que `AguaEn` no marca)
            var (cx, cy) = (llano.Item1 + 9, llano.Item2);
            var b = new BancoDeMenudo();
            string madre = b.Plaza(cx, cy + 6, 1);
            double Xi(int i) => cx + (i - 3) * 2.6;
            // el pueblo de nivel 1, sobre su nodo; la plaza de nivel 1
            var nodo = new NodoRecurso { Id = "lm-escala-nodo", Tipo = "piedra", Rareza = "comun", Posicion = Pu(Xi(0), cy) };
            b.Vista.Entidades.Add(new Entidad { Pueblo = new Pueblo { Id = "lm-escala-pu", AsentamientoMadreId = madre, TipoExtractor = "cantera", NivelAldea = 1, NodoId = nodo.Id, Posicion = Pu(Xi(0), cy) } });
            b.Plaza(Xi(1), cy, 1);
            b.Avanzada("atalaya x1", (Xi(2), cy), 1, false);
            b.Avanzada("atalaya x1,35", (Xi(3), cy), 1, false);
            b.Avanzada("fuerte x1", (Xi(4), cy), 2, false);
            b.Avanzada("fuerte x1,35", (Xi(5), cy), 2, false);
            b.Avanzada("ciudadela", (Xi(6), cy), 3, false);
            var avs = b.Vista.Entidades.Where(e => e.Avanzada != null).Select(e => e.Avanzada.Id).ToList();
            MaquetasDelMapa.EscalaForzada[avs[0]] = 1.0;
            MaquetasDelMapa.EscalaForzada[avs[2]] = 1.0;
            artefacto.Nodos.Add(nodo);
            var antes = (MapaDeMundo.AclararCopas, MaquetasDelMapa.Estilo, NodosDelMapa.Estilo);
            MapaDeMundo.AclararCopas = false;
            NodosDelMapa.Estilo = EstiloDeNodos.Mejorado;
            MaquetasDelMapa.Estilo = EstiloDeAsentamientos.Mejorado;
            try
            {
                var (almacen, sesion) = Almacen(url, (cx, cy), null);
                almacen.Aplicar(new MensajeDeServidor { Foto = b.Vista });
                var mapa = Montar(almacen, sesion, relieve, artefacto, datos);
                mapa.RadioVisionFijo = 15;
                yield return Llegar(mapa, almacen, (cx, cy));
                yield return new WaitForSecondsRealtime(1);
                EsconderLaColumna();
                yield return Vista(mapa, ZoomDelMapa.Normal, 30, "lm_escala_habitual");
                yield return Vista(mapa, ZoomDelMapa.Cerca, 30, "lm_escala_cerca");
                foreach (var a in mapa.Maquetas.AnchosEnPantalla(mapa.Vista)) Debug.Log($"[LoMenudo] escala: {a}");
            }
            finally
            {
                MaquetasDelMapa.EscalaForzada.Clear();
                artefacto.Nodos.Remove(nodo);
                (MapaDeMundo.AclararCopas, MaquetasDelMapa.Estilo, NodosDelMapa.Estilo) = antes;
            }
        }

        /// <summary>El coste de lo menudo con una cantidad realista a la vista (la de un frente con mucho: 12 avanzadas de las tres etapas, en
        /// ruina y en obras, 3 campamentos de mercenarios, 8 de bandidos, 16 alijos, 4 obras y 2 batallas, en 30 u), «Prototipo» frente a
        /// «Mejorado», a zoom habitual y alejado: el fotograma, lo que cuesta mandar a dibujar, rehacer y asentar, y los triángulos.</summary>
        [Category("Mapa"), Explicit("coste de la parte 5 y lo menudo")]
        [UnityTest, Timeout(3_000_000)]
        [Category("Visual")]  // (arte del mapa: en la pasada de antes del push, no en la de cada commit)
        public IEnumerator ElCosteDeLoMenudo()
        {
            var url = Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_URL");
            if (string.IsNullOrEmpty(url)) Assert.Ignore("sin BRONZEAGE_PRUEBA_URL: no hay servidor de prueba");
            yield return Preparar(url, "lo-menudo-coste-");
            var (relieve, artefacto, datos) = (pRelieve, pArtefacto, pDatos);
            var caminos = BancoDeCaminos.Hacer(relieve, artefacto, datos);
            var (cx, cy) = caminos.SitiosDeSuelo["suelo_pradera"];
            Sigilos.Cargar(Resources.Load<TextAsset>("MundoUI/Sigilo/catalogoSigilos").text, "{}", "{}");
            rt = new RenderTexture(1600, 900, 24) { antiAliasing = 4 };
            var b = new BancoDeMenudo();
            var rng = new System.Random(5);
            (double, double) Al(double r) { double a = rng.NextDouble() * Math.PI * 2, d = r * Math.Sqrt(0.15 + 0.85 * rng.NextDouble()); return (cx + d * Math.Cos(a), cy + d * Math.Sin(a)); }
            for (int i = 0; i < 12; i++)
                b.Avanzada($"av{i}", Al(28), (uint)(i % 3 + 1), i % 5 == 4 && i % 3 != 0, i % 4 == 1 ? TipoObra.ObraMejoraAvanzada : TipoObra.NoEspecificado, i % 8 == 1);
            for (int i = 0; i < 3; i++) b.Mercenarios($"me{i}", Al(28), (uint)i, new[] { "barracon", "galeriaDeTiro", "caballerizas" }[i], false);
            for (int i = 0; i < 8; i++) b.Bandidos($"ba{i}", Al(28), i % 2 == 0, (uint)(i % 3 + 1), false);
            for (int i = 0; i < 16; i++) b.Alijo($"al{i}", Al(28), new[] { "trigo", "madera", "piedra", "oro" }[i % 4]);
            for (int i = 0; i < 4; i++) b.Obra($"ob{i}", Al(28), i % 2 == 0 ? TipoObra.ObraAvanzada : TipoObra.ObraPueblo, i >= 2);
            for (int i = 0; i < 2; i++) b.Batalla($"bt{i}", Al(28), false);
            var antes = MaquetasDelMapa.Estilo;
            try
            {
                var (almacen, sesion) = Almacen(url, (cx, cy), null);
                almacen.Aplicar(new MensajeDeServidor { Foto = b.Vista });
                var mapa = Montar(almacen, sesion, relieve, artefacto, datos);
                mapa.RadioVisionFijo = 30;
                foreach (var (estilo, zoom) in new[] { (EstiloDeAsentamientos.Prototipo, ZoomDelMapa.Normal), (EstiloDeAsentamientos.Mejorado, ZoomDelMapa.Normal),
                                                       (EstiloDeAsentamientos.Prototipo, ZoomDelMapa.Alejado), (EstiloDeAsentamientos.Mejorado, ZoomDelMapa.Alejado) })
                {
                    MaquetasDelMapa.Estilo = estilo;
                    yield return Llegar(mapa, almacen, (cx, cy));
                    yield return Vista(mapa, zoom, 30, $"coste_menudo_{estilo}_{zoom}");
                    var fot = new List<double>();
                    var dib = new List<double>();
                    for (int i = 0; i < 240; i++)
                    {
                        yield return null;
                        fot.Add(Time.unscaledDeltaTime * 1000);
                        dib.Add(mapa.Maquetas.MsDibujar);
                    }
                    fot.Sort();
                    dib.Sort();
                    mapa.Maquetas.Olvidar();
                    yield return null;
                    var enc = mapa.Maquetas.Encargos(mapa.Vista);
                    int tris = enc.Where(k => !k.EsSuelo).Sum(k => (int)(k.Malla.GetIndexCount(0) / 3));
                    Debug.Log($"[LoMenudo] coste {estilo} {zoom}: fotograma mediana {fot[fot.Count / 2]:0.00} ms, p95 {fot[fot.Count * 95 / 100]:0.00} ms; " +
                              $"mandar a dibujar mediana {dib[dib.Count / 2]:0.000} ms; rehacer {mapa.Maquetas.MsRehacer:0.00} ms, asentar {mapa.Maquetas.MsAsentar:0.00} ms; " +
                              $"miniaturas {enc.Count(k => !k.EsSuelo)} ({tris} triángulos a su nivel de detalle) y cajas {mapa.Maquetas.PiezasPuestas} ({mapa.Maquetas.Triangulos} triángulos)");
                }
            }
            finally { MaquetasDelMapa.Estilo = antes; }
        }
    }
}
