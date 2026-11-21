// El banco de los asentamientos (tema 6): una muestra sintética y determinista de todo lo que se ve desde el mundo, puesta en los sitios del
// banco de caminos (`BancoDeCaminos`):
//  * en la pradera, las plazas de nivel 1 a 5 con muralla y sin ella, en dos filas, y una recordada (nivel 3);
//  * en la tierra seca, el pueblo extractor y las seis aldeas (trigo, vid, oliva, cebada, sésamo y ganado), en fila;
//  * en la ladera, las tres avanzadas (atalaya, fuerte y ciudadela) y un fuerte en ruinas;
//  * al borde del bosque, la obra pendiente y la arrancada, el campamento de mercenarios, los de bandidos de nivel 1 a 3, un alijo y una batalla
//    propia y otra ajena.
// Todo de la Facción f-1 (la tuya) o f-2; las madres de pueblos, avanzadas y obras son plazas del banco. `Sitios` da dónde está cada cosa y
// `Filas`, el centro de cada fila (para las láminas). Cada cosa, en tierra firme y despejada: si su sitio de la fila cae en agua o en bosque
// (a menos de su radio), se busca el más cercano que no (en espiral, hasta 40 u), sin pisar las demás.
using System;
using System.Collections.Generic;
using System.Linq;
using BronzeAge.Contract.V1;
using AmbitoPb = BronzeAge.Contract.V1.Ambito;

namespace ConquestTactics.Mundo.UI.Tests
{
    sealed class BancoDeAsentamientos
    {
        public readonly Dictionary<string, (double x, double y)> Sitios = new Dictionary<string, (double x, double y)>();
        public readonly Dictionary<string, (double x, double y)> Filas = new Dictionary<string, (double x, double y)>();
        readonly List<Entidad> cosas = new List<Entidad>();

        static Punto Pu(double x, double y) => new Punto { X = (long)(x * 1000), Y = (long)(y * 1000) };

        public Foto Vista()
        {
            var vista = new Foto { Ambito = new AmbitoPb { Tipo = AmbitoTipo.AmbitoVista, Id = "f-1" }, Version = 2 };
            foreach (var c in cosas) vista.Entidades.Add(c.Clone());
            return vista;
        }

        public static BancoDeAsentamientos Hacer(BancoDeCaminos caminos, Relieve relieve, ArtefactoDeMundo artefacto)
        {
            var b = new BancoDeAsentamientos();
            var bosque = new RasterDeMundo(artefacto.DensidadBosque);
            var puestas = new List<(double x, double y, double r)>();
            bool Libre(double x, double y, double r)
            {
                for (double dy = -r; dy <= r; dy += 1)
                    for (double dx = -r; dx <= r; dx += 1)
                    {
                        if (dx * dx + dy * dy > r * r) continue;
                        if (relieve.AguaEn(x + dx, y + dy) || bosque.En(x + dx, y + dy) > 30) return false;
                    }
                return !puestas.Any(q => Math.Sqrt((q.x - x) * (q.x - x) + (q.y - y) * (q.y - y)) < q.r + r);
            }
            // el sitio despejado más cercano al de la fila
            (double, double) Colocar((double x, double y) p, double r)
            {
                for (double d = 0; d <= 40; d += 1.5)
                    for (int k = 0; k < Math.Max(1, (int)(d * 2)); k++)
                    {
                        double a = k * Math.PI * 2 / Math.Max(1, (int)(d * 2));
                        double x = p.x + d * Math.Cos(a), y = p.y + d * Math.Sin(a);
                        if (Libre(x, y, r)) { puestas.Add((x, y, r)); return (x, y); }
                    }
                puestas.Add((p.x, p.y, r));
                return p;
            }
            var pradera = caminos.SitiosDeSuelo["suelo_pradera"];
            var seco = caminos.SitiosDeSuelo["suelo_seco"];
            var ladera = caminos.Sitios["ladera"];
            var bordeBosque = caminos.Sitios["camino_bosque"];

            // las plazas: con muralla (fila 1) y sin ella (fila 2), de nivel 1 a 5, a 9 u una de otra; y una recordada
            for (int fila = 0; fila < 2; fila++)
                for (int n = 1; n <= 5; n++)
                {
                    var (x, y) = Colocar((pradera.x + (n - 3) * 9, pradera.y + 20 + fila * 12), 3.5);
                    string id = $"as-{(fila == 0 ? "m" : "s")}{n}";
                    b.cosas.Add(new Entidad { Asentamiento = new Asentamiento { Id = id, Nombre = id, Nivel = (uint)n, FaccionId = n % 2 == 0 ? "f-2" : "f-1", Posicion = Pu(x, y), TieneMuralla = fila == 0 } });
                    b.Sitios[$"plaza{n}_{(fila == 0 ? "muralla" : "sin")}"] = (x, y);
                }
            b.Filas["plazas_muralla"] = (pradera.x, pradera.y + 20);
            b.Filas["plazas_sin"] = (pradera.x, pradera.y + 32);
            {
                var (x, y) = Colocar((pradera.x + 27, pradera.y + 20), 3.5);
                b.cosas.Add(new Entidad { FichaConocida = new FichaConocida { AsentamientoId = "as-r", Nombre = "Recordada", FaccionId = "f-2", Nivel = 3, Tipo = TipoEntidad.Asentamiento, Posicion = Pu(x, y) } });
                b.Sitios["plaza3_recordada"] = (x, y);
            }

            // el extractor y las seis aldeas, a 6 u
            var cultivos = new[] { (string)null, "trigo", "vid", "oliva", "cebada", "sesamo", "ganado" };
            for (int i = 0; i < cultivos.Length; i++)
            {
                var (x, y) = Colocar((seco.x + (i - 3) * 6, seco.y + 20), 2.4);
                var q = new Pueblo { Id = $"pu-{i}", AsentamientoMadreId = "as-m1", TipoExtractor = cultivos[i] == null ? "mina" : "aldeaAgricola", Posicion = Pu(x, y) };
                if (cultivos[i] != null) q.Cultivo = cultivos[i];
                b.cosas.Add(new Entidad { Pueblo = q });
                b.Sitios[cultivos[i] == null ? "extractor" : "aldea_" + cultivos[i]] = (x, y);
            }
            b.Filas["aldeas"] = (seco.x, seco.y + 20);

            // las avanzadas en la ladera, a 5 u
            var avanzadas = new[] { (1u, false, "atalaya"), (2u, false, "fuerte"), (3u, false, "ciudadela"), (2u, true, "ruinas") };
            for (int i = 0; i < avanzadas.Length; i++)
            {
                var (etapa, ruinas, nombre) = avanzadas[i];
                var (x, y) = Colocar((ladera.x + (i - 1.5) * 5, ladera.y + 6), 1.6);
                b.cosas.Add(new Entidad { Avanzada = new Avanzada { Id = $"av-{i}", AsentamientoMadreId = "as-m2", Etapa = etapa, Posicion = Pu(x, y),
                    Estado = ruinas ? EstadoAvanzada.AvanzadaEnRuinas : EstadoAvanzada.AvanzadaActiva } });
                b.Sitios["avanzada_" + nombre] = (x, y);
            }
            b.Filas["avanzadas"] = (ladera.x, ladera.y + 6);

            // al borde del bosque: obras, campamentos, alijo y batallas, a 4,5 u
            var (bx, by) = (bordeBosque.x, bordeBosque.y + 8);
            int k = 0;
            (double, double) Sig() { var r = Colocar((bx + (k - 4) * 4.5, by), 1.6); k++; return r; }
            {
                var (x, y) = Sig();
                b.cosas.Add(new Entidad { Obra = new ObraDeMapa { Id = "ob-p", AsentamientoMadreId = "as-m1", Tipo = TipoObra.ObraPueblo, Posicion = Pu(x, y) } });
                b.Sitios["obra_pendiente"] = (x, y);
            }
            {
                var (x, y) = Sig();
                b.cosas.Add(new Entidad { Obra = new ObraDeMapa { Id = "ob-a", AsentamientoMadreId = "as-m1", Tipo = TipoObra.ObraAvanzada, Posicion = Pu(x, y), TerminaEnMs = 1 } });
                b.Sitios["obra_arrancada"] = (x, y);
            }
            {
                var (x, y) = Sig();
                b.cosas.Add(new Entidad { CampamentoMercenarios = new CampamentoMercenarios { Id = "cm-1", Posicion = Pu(x, y) } });
                b.Sitios["mercenarios"] = (x, y);
            }
            for (uint n = 1; n <= 3; n++)
            {
                var (x, y) = Sig();
                b.cosas.Add(new Entidad { CampamentoBandidos = new CampamentoBandidos { Id = $"cb-{n}", Posicion = Pu(x, y), Nivel = n } });
                b.Sitios[$"bandidos{n}"] = (x, y);
            }
            {
                var (x, y) = Sig();
                b.cosas.Add(new Entidad { Alijo = new Alijo { Id = "al-1", Posicion = Pu(x, y), Recurso = "oro", Cantidad = 1000 } });
                b.Sitios["alijo"] = (x, y);
            }
            foreach (var propia in new[] { true, false })
            {
                var (x, y) = Sig();
                var ticket = new BattleTicket { Atacante = new BattleSide(), Defensor = new BattleSide() };
                ticket.Atacante.Participantes.Add(new BattleParticipantSnapshot { HeroeId = propia ? "heroe-1" : "heroe-9" });
                b.cosas.Add(new Entidad { Batalla = new Batalla { Id = propia ? "bt-p" : "bt-a", Estado = EstadoBatalla.BatallaEnCurso, Punto = Pu(x, y), Ticket = ticket } });
                b.Sitios[propia ? "batalla_propia" : "batalla_ajena"] = (x, y);
            }
            b.Filas["campamentos"] = (bx, by);
            return b;
        }
    }
}
