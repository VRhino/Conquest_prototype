// Tema 8, ronda 1: la niebla de guerra dibujada. Lo nunca explorado como mapa dibujado en planta (tinta sobre papiro), lo explorado que no se
// ve apagado y sin velo, y la frontera de pincel seco. La prueba fija la máscara en franjas de oeste a este (a la vista, explorado sin ver y
// nunca explorado) y en un círculo para la frontera. Capturas («nd_…» en `MUNDO_CAPTURAS`): la frontera a tres zooms, lo que crece al
// explorar, la montaña y el bosque denso dentro de lo nunca explorado, la costa con el mar nunca explorado, la hierba y el bosque explorados
// sin ver con el apagado de antes y el de ahora, una comparación con el velo de antes y el coste.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BronzeAge.Contract.V1;
using ConquestTactics.Mundo.Mapa;
using Google.Protobuf;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using AmbitoPb = BronzeAge.Contract.V1.Ambito;

namespace ConquestTactics.Mundo.UI.Tests
{
    public partial class SueloDelMapaTests
    {
        [Category("Mapa"), Explicit("tema 8: la niebla de guerra dibujada")]
        [UnityTest, Timeout(3_000_000)]
        public IEnumerator LaNieblaDibujada()
        {
            var url = Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_URL");
            if (string.IsNullOrEmpty(url)) Assert.Ignore("sin BRONZEAGE_PRUEBA_URL: no hay servidor de prueba");
            yield return Preparar(url, "nd-");
            var (relieve, artefacto, datos) = (pRelieve, pArtefacto, pDatos);
            var largo = artefacto.Rios.Where(r => !r.TerminaEnLago).OrderByDescending(r => r.Puntos.Count).First();
            largo.Navegable = true;
            var sitios = BuscarSitios(relieve, artefacto, datos);
            var veg = SitiosDeVegetacion(relieve, artefacto, datos);
            var costa = sitios["costa"];
            var montana = sitios["montana_roca"];
            var bosque = veg.TryGetValue("denso", out var denso) ? denso : sitios["borde_bosque"];
            var hierba = sitios["llano_fertil"];
            // el mar de la costa, al este o al oeste: las franjas se dan la vuelta para que quede en lo nunca explorado
            bool marAlEste = relieve.AguaEn(costa.x + 10, costa.y);
            rt = new RenderTexture(1600, 900, 24) { antiAliasing = 4 };
            var antes = (PinturaDelMapa.NieblaDibujada, PinturaDelMapa.Apagado);
            var apagadoDeAntes = new Vector4(0.55f, 0.72f, 0, 0);
            int version = 1;
            try
            {
                var centro = sitios["borde_bosque"];
                var (almacen, sesion) = Almacen(url, centro, Niebla(relieve, centro, 25, 12));
                var mapa = Montar(almacen, sesion, relieve, artefacto, datos);
                MapaDeMundo.PintarMiColumna = false;
                IEnumerator Poner(NieblaProyectada n)
                {
                    almacen.Aplicar(new MensajeDeServidor { Foto = new Foto { Ambito = new AmbitoPb { Tipo = AmbitoTipo.AmbitoVista, Id = "f-1" }, Version = (ulong)++version, Niebla = n } });
                    Avisar(mapa, almacen);
                    yield return new WaitForSecondsRealtime(NieblaDelMapa.SegundosDeCambio + 0.3f);
                }
                // La frontera: el círculo explorado (25 u) con lo que se ve (12 u), con la cámara en el borde del este.
                yield return Llegar(mapa, almacen, (centro.x + 22, centro.y));
                yield return Vista(mapa, ZoomDelMapa.Cerca, 25, "nd_frontera_1_cerca");
                yield return Vista(mapa, ZoomDelMapa.Normal, 35, "nd_frontera_2_habitual");
                yield return Vista(mapa, ZoomDelMapa.Alejado, 50, "nd_frontera_3_alejado");
                // Lo que crece al explorar: el círculo pasa a 32 u y se ve el paso (cuatro fotogramas, 0,4 s entre uno y otro).
                yield return Vista(mapa, ZoomDelMapa.Normal, 35, "nd_crece_0");
                almacen.Aplicar(new MensajeDeServidor { Foto = new Foto { Ambito = new AmbitoPb { Tipo = AmbitoTipo.AmbitoVista, Id = "f-1" }, Version = (ulong)++version, Niebla = Niebla(relieve, centro, 32, 12) } });
                Avisar(mapa, almacen);
                for (int f = 1; f < 4; f++) yield return Fotograma("nd_crece_" + f, 0.4f);
                // La montaña, dentro de lo nunca explorado: a la vista al oeste, explorado sin ver y, desde 9 u al oeste, nunca explorado.
                yield return Poner(Franjas(relieve, montana, -22, -9));
                yield return Llegar(mapa, almacen, montana);
                yield return Vista(mapa, ZoomDelMapa.Normal, 35, "nd_montana_habitual");
                yield return Vista(mapa, ZoomDelMapa.Alejado, 60, "nd_montana_alejado");
                foreach (var (giro, n) in new[] { (0.0, "1_norte"), (Math.PI / 2, "2_este"), (Math.PI, "3_sur"), (3 * Math.PI / 2, "4_oeste") })
                {
                    mapa.Camara.Giro = giro;
                    yield return Vista(mapa, ZoomDelMapa.Alejado, 45, $"nd_vuelta_{n}");
                }
                mapa.Camara.Giro = 0;
                // El bosque denso, dentro de lo nunca explorado.
                yield return Poner(Franjas(relieve, bosque, -22, -9));
                yield return Llegar(mapa, almacen, bosque);
                yield return Vista(mapa, ZoomDelMapa.Normal, 35, "nd_bosque_habitual");
                yield return Vista(mapa, ZoomDelMapa.Alejado, 60, "nd_bosque_alejado");
                // La costa con el mar nunca explorado; y una sola comparación con el velo de antes.
                yield return Poner(Franjas(relieve, costa, -7, 7, !marAlEste));
                yield return Llegar(mapa, almacen, costa);
                yield return Vista(mapa, ZoomDelMapa.Normal, 35, "nd_costa_habitual");
                yield return Vista(mapa, ZoomDelMapa.Alejado, 60, "nd_costa_alejado");
                PinturaDelMapa.NieblaDibujada = false;
                yield return Poner(Franjas(relieve, costa, -7, 7, !marAlEste));
                yield return Vista(mapa, ZoomDelMapa.Normal, 35, "nd_costa_velo");
                PinturaDelMapa.NieblaDibujada = true;
                // Explorado sin ver (todo menos una franja a la vista al oeste): la hierba y el bosque, con el apagado de antes y el de ahora.
                foreach (var (nombre, donde) in new[] { ("hierba", hierba), ("bosque", bosque) })
                {
                    yield return Llegar(mapa, almacen, donde);
                    PinturaDelMapa.Apagado = apagadoDeAntes;
                    yield return Poner(Franjas(relieve, donde, -6, 1000));
                    yield return Vista(mapa, ZoomDelMapa.Normal, 35, $"nd_apagado_{nombre}_antes");
                    PinturaDelMapa.Apagado = antes.Apagado;
                    yield return Poner(Franjas(relieve, donde, -6, 1000));
                    yield return Vista(mapa, ZoomDelMapa.Normal, 35, $"nd_apagado_{nombre}_ahora");
                }
                // El coste, a zoom habitual en el bosque con los tres estados: con el dibujo y con el velo de antes.
                yield return Poner(Franjas(relieve, bosque, -7, 7));
                yield return Llegar(mapa, almacen, bosque);
                yield return Vista(mapa, ZoomDelMapa.Normal, 35, "nd_coste");
                yield return Medir(mapa, "[Niebla] dibujada");
                PinturaDelMapa.NieblaDibujada = false;
                yield return Poner(Franjas(relieve, bosque, -7, 7));
                yield return Medir(mapa, "[Niebla] velo");
            }
            finally
            {
                (PinturaDelMapa.NieblaDibujada, PinturaDelMapa.Apagado) = antes;
                MapaDeMundo.PintarMiColumna = true;
            }
        }

        /// <summary>La sesión solo avisa al mapa de lo que llega por la red: lo aplicado a mano al almacén, se avisa aquí (la niebla).</summary>
        static void Avisar(MapaDeMundo mapa, Almacen almacen)
        {
            var avisar = typeof(MapaDeMundo).GetMethod("AlCambiar", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            foreach (var k in almacen.Ambitos.Keys.ToList()) avisar.Invoke(mapa, new object[] { k });
        }

        /// <summary>Una niebla de la VISTA (celdas de 5 u) en franjas: a la vista al oeste de `centro.x + visible`, explorada hasta
        /// `centro.x + explorado` y nunca explorada al este; con `volteada`, al revés (lo nunca explorado al oeste).</summary>
        static NieblaProyectada Franjas(Relieve relieve, (double x, double y) centro, double visible, double explorado, bool volteada = false)
        {
            const double celda = 5;
            int columnas = (int)Math.Ceiling(relieve.Columnas * relieve.Paso / celda), filas = (int)Math.Ceiling(relieve.Filas * relieve.Paso / celda);
            var celdas = new byte[(columnas * filas + 7) / 8];
            var visibles = new byte[celdas.Length];
            for (int j = 0; j < filas; j++)
                for (int i = 0; i < columnas; i++)
                {
                    double dx = ((i + 0.5) * celda - centro.x) * (volteada ? -1 : 1);
                    int k = j * columnas + i;
                    if (dx <= explorado) celdas[k >> 3] |= (byte)(1 << (k & 7));
                    if (dx <= visible) visibles[k >> 3] |= (byte)(1 << (k & 7));
                }
            return new NieblaProyectada { TamanoCelda = (uint)(celda * 1000), Columnas = (uint)columnas, Filas = (uint)filas, Celdas = ByteString.CopyFrom(celdas), Visibles = ByteString.CopyFrom(visibles) };
        }
    }
}
