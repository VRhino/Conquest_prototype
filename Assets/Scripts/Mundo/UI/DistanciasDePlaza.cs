// Distancias de plaza (escena `Assets/Scenes/BronzeAge/DistanciasDePlaza.unity`, menú `Conquest/Mundo/Distancias de plaza`): una plaza en el
// centro de un llano amplio del mundo de disco, sin servidor, con el mismo `MapaDeMundo` («Mejorado», sin niebla), y en el suelo lo que el motor
// mide a su alrededor: la huella de su maqueta, su zona, los anillos de sus aldeas, la separación mínima, el mínimo de avanzada, el radio de
// puerta (con una columna parada en él), la orilla que exige a la plaza y la separación entre plazas (con una plaza vecina). Un panel cambia el
// nivel, la zona, las aldeas, los pueblos y las avanzadas, y los factores de lo fijo; y lee las holguras (distancia entre centros menos la suma
// de las huellas) de cada par. Las reglas son las del motor: las de los datos de la última partida (`DatosDeJuego`, en la caché del cliente) y,
// si falta alguna, la constante del documento del motor (anotada).
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using BronzeAge.Contract.V1;
using ConquestTactics.Mundo.Mapa;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using AmbitoPb = BronzeAge.Contract.V1.Ambito;

namespace ConquestTactics.Mundo.UI
{
    public sealed class DistanciasDePlaza : MonoBehaviour
    {
        public VisualPrefabConfiguration Prefabs;

        public bool Lista { get; private set; }
        public MapaDeMundo Mapa { get; private set; }
        public BancoDelMapa Banco { get; private set; }

        // ------------------------------------------------------------------ las reglas del motor

        /// <summary>Las cifras del motor que miden lo que rodea a una plaza, con de dónde ha salido cada una.</summary>
        public sealed class Reglas
        {
            public double RadioInicial, Crecimiento, SeparacionPlazas, ExclusionCampamento, SeparacionAldea, FraccionCiudad, TamanoAvanzada, RadioPuerta;
            public double OrillaPlaza, OrillaPueblo, OrillaAldea, OrillaAvanzada, CupoAvanzadasPorNivel, NivelAtalaya;
            public double[] Techos = new double[5], Cupo = new double[5];
            /// <summary>Regla nueva (a prueba): el radio de la maqueta de plaza por nivel (u) y la vivienda con que se calcula (u).</summary>
            public double[] RadioPlaza = (double[])RadiosFijos.Clone();
            public double CasaU = CasaPorDefecto;
            public readonly List<string> Origen = new List<string>();
            public int DelMotor, Constantes;
            /// <summary>Reglas nuevas, a prueba (no están en el motor): la separación de un nodo al centro de la plaza, y a cuánto del borde de la
            /// maqueta se para lo que llega (todo el contorno).</summary>
            public double SeparacionNodo = 12, PuertaDesdeElBorde = 1;
            /// <summary>Regla nueva, a prueba: lo cerca que puede ir una aldea de otro pueblo (extractor o aldea). En el motor de hoy es la misma
            /// separación de aldea que a las plazas (`SeparacionAldea`).</summary>
            public double SeparacionAldeaPueblo = 3;
        }

        public Reglas R { get; private set; }

        static Reglas LeerReglas(DatosDeJuego d)
        {
            var r = new Reglas();
            double N(string ruta, double siNo, string doc)
            {
                try
                {
                    double v = d.Numero(ruta);
                    r.DelMotor++;
                    r.Origen.Add($"{ruta.Replace("documentos.", "")} = {v.ToString("0.###", CultureInfo.InvariantCulture)} (datos del motor)");
                    return v;
                }
                catch
                {
                    r.Constantes++;
                    r.Origen.Add($"{ruta.Replace("documentos.", "")} = {siNo.ToString("0.###", CultureInfo.InvariantCulture)} (constante: {doc})");
                    return siNo;
                }
            }
            const string EC = "documentos.Economia_Construccion.";
            r.RadioInicial = N(EC + "ZONA_INFLUENCIA.radioInicial", 12, "Economia_Construccion.md §14, ZONA_INFLUENCIA");
            r.Crecimiento = N(EC + "ZONA_INFLUENCIA.crecimientoPorEdificioCompletado", 2, "ZONA_INFLUENCIA");
            for (int n = 1; n <= 5; n++)
            {
                r.Techos[n - 1] = N(EC + $"ZONA_INFLUENCIA.radioMaximoPorNivel.{n}", 10 * (n + 1), "ZONA_INFLUENCIA.radioMaximoPorNivel");
                r.Cupo[n - 1] = N(EC + $"PUEBLOS_EXTRACTORES.cupoPorNivel.{n}", n + 1, "PUEBLOS_EXTRACTORES.cupoPorNivel");
            }
            r.SeparacionPlazas = N(EC + "FUNDACION.separacionPlazas", 20, "FUNDACION.separacionPlazas");
            r.ExclusionCampamento = N("documentos.Campamentos_Mercenarios.MERCENARIOS.radioExclusionFundar", 20, "Campamentos_Mercenarios.md, MERCENARIOS");
            r.SeparacionAldea = N(EC + "PUEBLOS_EXTRACTORES.aldea.separacionMinima", 3, "PUEBLOS_EXTRACTORES.aldea.separacionMinima");
            r.SeparacionAldeaPueblo = r.SeparacionAldea;
            r.FraccionCiudad = N(EC + "AVANZADAS.fraccionCiudadDeLaZona", 0.05, "AVANZADAS.fraccionCiudadDeLaZona");
            r.CupoAvanzadasPorNivel = N(EC + "AVANZADAS.cupoPorNivelDeLaMadre", 1, "AVANZADAS.cupoPorNivelDeLaMadre");
            r.NivelAtalaya = N(EC + "AVANZADAS.nivelMadre.atalaya", 2, "AVANZADAS.nivelMadre");
            r.TamanoAvanzada = N(EC + "TAMANOS_MAPA.avanzada", 0.2, "TAMANOS_MAPA.avanzada");
            r.RadioPuerta = N("documentos.Movimiento_Ejercitos.MOVIMIENTO.radioPuerta", 2, "Movimiento_Ejercitos.md, MOVIMIENTO");
            r.OrillaPlaza = N(EC + "ORILLA_RIO.radio.plaza", 4.8, "ORILLA_RIO.radio");
            r.OrillaPueblo = N(EC + "ORILLA_RIO.radio.pueblo", 0.75, "ORILLA_RIO.radio");
            r.OrillaAldea = N(EC + "ORILLA_RIO.radio.aldea", 1.8, "ORILLA_RIO.radio");
            r.OrillaAvanzada = N(EC + "ORILLA_RIO.radio.avanzada", 0.65, "ORILLA_RIO.radio");
            return r;
        }

        /// <summary>El anillo (desde, hasta] de la aldea número `k` (1, 2...): el que añade a la zona el primer nivel cuyo cupo llega a k
        /// (`Nucleo::anillo_de_aldea`), sin recortar por la zona.</summary>
        public (double desde, double hasta) AnilloDeAldea(int k)
        {
            int nivel = 5;
            for (int n = 1; n <= 5; n++) if (R.Cupo[n - 1] >= k) { nivel = n; break; }
            return (nivel == 1 ? 0 : R.Techos[nivel - 2], R.Techos[nivel - 1]);
        }

        public int CupoDeAvanzadas(int nivel) => nivel >= R.NivelAtalaya ? (int)(nivel * R.CupoAvanzadasPorNivel) : 0;
        public double MinimoDeAvanzada => Zona * R.FraccionCiudad + R.TamanoAvanzada / 2;
        /// <summary>(De la mesa) lo más cerca que se pone una avanzada (u, del centro de la plaza al suyo): el radio de la plaza con su muralla, el
        /// de la avanzada y la holgura; así no se montan nunca.</summary>
        public double MinimoDeAvanzadaEnMesa => RadioPlaza(Nivel, Zona) + CosasDelMapa.HuellaDeAvanzadaEn((uint)EtapaAvanzada, EstadoAvanzada == 1) + BancoDelMapa.HolguraDeSitio;
        /// <summary>(De la mesa) a qué distancia se ponen las avanzadas (u): la del deslizador, nunca menos del mínimo (0: el mínimo).</summary>
        public double DistanciaAvanzada;
        public double DistanciaDeAvanzadas => Math.Max(MinimoDeAvanzadaEnMesa, DistanciaAvanzada);

        /// <summary>Regla nueva a prueba: lo que llega a la plaza se para en su borde (a `PuertaDesdeElBorde` de su maqueta, por todo el
        /// contorno), no a 2 u de su centro.</summary>
        public bool PuertaDesdeElBorde = true;

        /// <summary>El radio de la maqueta de la plaza de ese nivel y esa zona (u), con su muralla si la lleva (o si `conMuralla`): la huella de
        /// `CosasDelMapa`.</summary>
        public double RadioPlaza(int nivel, double zona, bool? conMuralla = null) =>
            CosasDelMapa.HuellaConMuralla(CosasDelMapa.HuellaDePlaza(zona * 0.1, nivel, centro.x, centro.y), nivel, centro.x, centro.y, (conMuralla ?? Muralla) && nivel >= 2);
        /// <summary>El radio de la columna (u): su aro, el de `CosasDelMapa`.</summary>
        static double RadioDeColumna => 0.3 * ColumnasDelMapa.FactorDeFiguras;
        /// <summary>Dónde se para la columna que llega (u, del centro): con la puerta desde el borde, justo fuera de la maqueta (su borde más el
        /// radio de la columna); si no, al radio de puerta del motor.</summary>
        public double DistanciaDeColumna => PuertaDesdeElBorde ? RadioPlaza(Nivel, Zona) + RadioDeColumna + 0.01 : R.RadioPuerta;
        /// <summary>El anillo de la puerta (u, del centro).</summary>
        public double AnilloDePuerta => PuertaDesdeElBorde ? RadioPlaza(Nivel, Zona) + R.PuertaDesdeElBorde : R.RadioPuerta;
        /// <summary>Los nodos de los pueblos extractores (uno por pueblo; los que no se usan, lejos).</summary>
        readonly List<NodoRecurso> nodosDePueblo = new List<NodoRecurso>();

        // ------------------------------------------------------------------ lo que se ve (lo cambia el panel)

        public int Nivel = 3, Aldeas = 2, NivelAldea = 2, Pueblos = 1, Avanzadas = 0, EtapaAvanzada = 1;
        public bool Muralla = true, AldeasEnMedio, Vecina, Zonas;
        /// <summary>(De vista, no del motor) el tipo de las aldeas y de los pueblos: uno para todas, o «variado» (uno distinto en cada una, en
        /// orden y repitiendo). Las aldeas: su cultivo, o la ganadera con su rebaño; los pueblos, el recurso de su nodo (la piedra, cantera;
        /// los demás, mina).</summary>
        public static readonly (string nombre, string cultivo, string rebano)[] TiposDeAldea =
        {
            ("trigo", "trigo", null), ("cebada", "cebada", null), ("sésamo", "sesamo", null), ("vid", "vid", null), ("olivo", "oliva", null),
            ("ganado vacuno", "ganado", "vacuno"), ("ganado ovino", "ganado", "ovejas"),
        };
        public static readonly (string nombre, string recurso)[] TiposDePueblo =
            { ("cantera de piedra", "piedra"), ("mina de cobre", "cobre"), ("mina de estaño", "estano"), ("mina de hierro", "hierro"), ("mina de oro", "oro") };
        /// <summary>El índice del tipo (en `TiposDeAldea` / `TiposDePueblo`); su número es «variado».</summary>
        public int TipoAldea, TipoPueblo;
        /// <summary>(De vista) los caminos de los carros de acarreo de cada aldea y cada pueblo a la plaza (`RedDeAcarreo`), y su escalón: 0 el del
        /// motor (por el peso de cada arista), 1 todo sendero, 2 todo camino.</summary>
        public bool Caminos = true;
        /// <summary>(De vista) la maqueta de plaza a un radio fijo por nivel (`CosasDelMapa.HuellaPorNivel`), para que la vivienda mida lo
        /// mismo en todos (la de la choza de la aldea, ≈ 0,32 u), en lugar de crecer con la zona.</summary>
        public bool TamanoFijo = true;
        /// <summary>(De vista) la variante del modelo de la plaza de la mesa (1 o 2; `MaquetasDelMapa.VarianteForzada` mientras dura la
        /// escena): el juego la sigue eligiendo por el sitio.</summary>
        public int Variante = 1;
        /// <summary>(De vista) el tamaño de los pueblos extractores: 1, 2 o 3 (3, 5 y 7 chozas).</summary>
        public int NivelPueblo = 2;
        /// <summary>(De vista) las avanzadas: su estado (0 operativa, 1 en obras: la mejora en marcha, que pone el modelo de la etapa siguiente en
        /// obras; 2 en ruina) y la ciudadela que se pone (1 o 2; `MaquetasDelMapa.VarianteCiudadelaForzada` mientras dura la escena).</summary>
        public int EstadoAvanzada, VarianteCiudadela = 1;
        /// <summary>El lado corto de la vivienda corriente de cada modelo de plaza, niveles 1 a 5 (media de sus dos variantes), en el espacio en
        /// que se escala (radio 1): medido el 23-11 corriendo los generadores (`medir_casas.py`: chozas, casas de adobe y casas de manzana, sin
        /// la del jefe, la principal, las nobles ni los cobertizos).</summary>
        public static readonly double[] LadoDeCasa = { 0.2286, 0.1668, 0.0770, 0.0625, 0.0571 };
        /// <summary>La vivienda de base (u): la choza del núcleo de aldea (Ø 0,18 de su modelo × 1,8 u).</summary>
        public const double CasaPorDefecto = 0.32;
        /// <summary>Los radios que dejan la vivienda en `CasaPorDefecto`, redondeados a 0,05 (los de Víctor, 1,4 / 2,1 / 4,3 / 4,9 / 5,3, eran de
        /// los lados estimados; el 2 y el 5 se van más de un 5 %).</summary>
        public static readonly double[] RadiosFijos = RadiosDeCasa(CasaPorDefecto);
        public static double[] RadiosDeCasa(double casa) => LadoDeCasa.Select(l => Math.Round(casa / l / 0.05) * 0.05).ToArray();
        void PonerTamanoFijo() => CosasDelMapa.HuellaPorNivel = TamanoFijo && R != null ? R.RadioPlaza : null;
        /// <summary>Lo que mide la vivienda corriente en la plaza de ahora (u): su lado en el modelo por el radio al que se escala.</summary>
        public double CasaDeAhora => LadoDeCasa[Nivel - 1] * CosasDelMapa.HuellaDePlaza(Zona * 0.1, Nivel);
        public int EscalonCamino;
        /// <summary>Los tramos de la red puesta (escalón y puntos en u) y los puntos de la ruta de cada aldea o pueblo, para las pruebas.</summary>
        public readonly List<(int escalon, List<(double x, double y)> puntos)> TramosPuestos = new List<(int, List<(double, double)>)>();
        public readonly Dictionary<string, List<(double x, double y)>> RutasDeAcarreo = new Dictionary<string, List<(double, double)>>();
        int IndiceDeTipo(int elegido, int k, int cuantos) => elegido < cuantos ? elegido : (k - 1) % cuantos;
        /// <summary>Un número de los datos del motor, o `siNo` si no está.</summary>
        double Dato(string ruta, double siNo)
        {
            try { var v = Banco.Datos.Numero(ruta); return double.IsNaN(v) || v == 0 ? siNo : v; }
            catch (Exception) { return siNo; }
        }
        public double Zona = 40, DistanciaPueblo = 3;
        /// <summary>Lo que se pinta en el suelo, por anillo.</summary>
        public readonly Dictionary<string, bool> Anillos = new Dictionary<string, bool>
        {
            ["huella"] = true, ["zona"] = true, ["aldeas"] = true, ["separacion"] = true, ["avanzada"] = true, ["puerta"] = true, ["orilla"] = true, ["vecina"] = true,
            ["tamanos"] = true, ["nodo"] = true,
        };

        (double x, double y) centro, mira;
        Almacen almacen;
        ulong version = 10;
        bool cambiado;
        /// <summary>Lo puesto alrededor: id, clase, nombre y dónde.</summary>
        public readonly List<(string id, ClaseDeCosa clase, string nombre, double x, double y)> Puesto = new List<(string, ClaseDeCosa, string, double, double)>();
        /// <summary>La tabla de holguras: el par, la distancia entre centros, las huellas (con los campos de las aldeas), la holgura con ellas
        /// (negativa: se pisan), la de las maquetas solas (los caseríos) y cómo queda.</summary>
        public readonly List<(string par, double d, double ra, double rb, double holgura, double maquetas, Pisa pisa)> Holguras =
            new List<(string, double, double, double, double, double, Pisa)>();

        IEnumerator Start()
        {
            yield return null;
            var estiloAntes = MaquetasDelMapa.Estilo;
            var antes = (CosasDelMapa.FactorDeFijos, CosasDelMapa.FactorPlazas, CosasDelMapa.FactorPueblos, CosasDelMapa.FactorAvanzadas, MapaDeMundo.PintarMiColumna);
            var huellaAntes = CosasDelMapa.HuellaPorNivel;
            var varianteAntes = MaquetasDelMapa.VarianteForzada;
            var ciudadelaAntes = MaquetasDelMapa.VarianteCiudadelaForzada;
            MaquetasDelMapa.VarianteCiudadelaForzada = VarianteCiudadela;
            MaquetasDelMapa.VarianteForzada = Variante;
            alSalir = () =>
            {
                MaquetasDelMapa.VarianteForzada = varianteAntes;
                MaquetasDelMapa.VarianteCiudadelaForzada = ciudadelaAntes;
                CosasDelMapa.HuellaPorNivel = huellaAntes;
                MaquetasDelMapa.Estilo = estiloAntes;
                (CosasDelMapa.FactorDeFijos, CosasDelMapa.FactorPlazas, CosasDelMapa.FactorPueblos, CosasDelMapa.FactorAvanzadas, MapaDeMundo.PintarMiColumna) = antes;
            };
            MaquetasDelMapa.Estilo = EstiloDeAsentamientos.Mejorado;
            MapaDeMundo.PintarMiColumna = false;
            try { Montar(); }
            catch (Exception e) { Debug.LogError($"[Distancias de plaza] no se pudo montar: {e.Message}"); yield break; }
            Panel();
            Lista = true;
        }

        Action alSalir;
        void OnDestroy() => alSalir?.Invoke();

        void Montar()
        {
            Banco = BancoDelMapa.DelDisco("distancias");
            R = LeerReglas(Banco.Datos);
            PonerTamanoFijo();
            centro = Banco.SitioDeHierba(75, out var nota);
            mira = centro;
            Zona = R.Techos[Nivel - 1];
            Debug.Log($"[Distancias de plaza] mundo {Banco.Hash} ({Banco.OrigenDelMundo}); sitio: {nota}; reglas: {R.DelMotor} de los datos del motor, {R.Constantes} constantes");
            for (int j = 1; j <= 6; j++)
            {
                var n = new NodoRecurso { Id = $"dp-nodo-{j}", Tipo = "piedra", Rareza = "comun", Posicion = BancoDelMapa.Pu(centro.x, centro.y) };
                nodosDePueblo.Add(n);
                Banco.Artefacto.Nodos.Add(n);
            }
            (almacen, _, Mapa) = Banco.Montar("Distancias de plaza", centro, Foto());
            Debug.Log("[Distancias de plaza] murallas medidas en su malla (huellas): " + string.Join(", ", MaquetasDelMapa.RadiosMedidos.Select(k => $"{k.Key} {k.Value:0.000}")));
            Mapa.Zonas = false;
            Mapa.TecladoDeCamara = false;
            if (Prefabs != null)
            {
                var clase = ClaseDeHeroe.Leer(Banco.Datos).FirstOrDefault();
                Mapa.gameObject.AddComponent<HornoDeFiguras>().Iniciar(Prefabs, id => new SelectorDePersonaje.HeroeParaVer { Nombre = id, Clase = clase, Armadura = "media" });
            }
            Mapa.RehacerRayas();
        }

        static (double x, double y) Rumbo((double x, double y) c, double grados, double d) =>
            (c.x + d * Math.Cos(grados * Math.PI / 180), c.y + d * Math.Sin(grados * Math.PI / 180));

        /// <summary>Lo puesto, según los valores de ahora: la plaza, sus aldeas (cada una en su anillo, en el peor caso o en medio), sus
        /// pueblos extractores, sus avanzadas, la columna a la puerta y, si toca, la plaza vecina.</summary>
        Foto Foto()
        {
            var f = new Foto { Ambito = new AmbitoPb { Tipo = AmbitoTipo.AmbitoVista, Id = "f-1" }, Version = ++version };
            Puesto.Clear();
            Asentamiento Plaza(string id, int nivel, (double x, double y) p)
            {
                var a = new Asentamiento { Id = id, Nombre = $"plaza de nivel {nivel}", Nivel = (uint)nivel, FaccionId = "f-1", Posicion = BancoDelMapa.Pu(p.x, p.y),
                    TieneMuralla = Muralla && nivel >= 2 };
                for (int i = 0; i < 48; i++) a.Zona.Add(BancoDelMapa.Pu(p.x + Zona * Math.Cos(i * Math.PI / 24), p.y + Zona * Math.Sin(i * Math.PI / 24)));
                f.Entidades.Add(new Entidad { Asentamiento = a });
                Puesto.Add((id, ClaseDeCosa.Plaza, a.Nombre, p.x, p.y));
                return a;
            }
            Plaza("dp-plaza", Nivel, centro);
            // las aldeas: la k-ésima en su anillo, recortado por la zona; lo más cerca que deja el motor (más allá del borde interior y a la
            // separación mínima de la plaza) o en medio
            double distPueblo = Math.Min(Math.Max(DistanciaPueblo, R.SeparacionNodo), Zona);
            var posPueblos = Enumerable.Range(1, Pueblos).Select(j => Rumbo(centro, 50 + 60 * (j - 1), distPueblo)).ToList();
            var colocadas = new List<(double x, double y)>();
            double Lejos((double x, double y) a, (double x, double y) b) => Math.Sqrt((a.x - b.x) * (a.x - b.x) + (a.y - b.y) * (a.y - b.y));
            for (int k = 1; k <= Aldeas; k++)
            {
                var (desde, hasta) = AnilloDeAldea(k);
                hasta = Math.Min(hasta, Zona);
                if (hasta <= Math.Max(desde, R.SeparacionAldea)) continue;
                double d = AldeasEnMedio ? (Math.Max(desde, R.SeparacionAldea) + hasta) / 2 : Math.Max(desde + 0.05, R.SeparacionAldea);
                (double x, double y) p = Rumbo(centro, 20 + 72 * (k - 1), d);
                for (int t = 1; t < 72 && !posPueblos.Concat(colocadas).All(o => Lejos(o, p) >= R.SeparacionAldeaPueblo - 1e-6); t++)
                    p = Rumbo(centro, 20 + 72 * (k - 1) + 5 * t, d);
                colocadas.Add(p);
                var tipo = TiposDeAldea[IndiceDeTipo(TipoAldea, k, TiposDeAldea.Length)];
                var q = new Pueblo { Id = $"dp-aldea-{k}", AsentamientoMadreId = "dp-plaza", TipoExtractor = "aldeaAgricola", NivelAldea = (uint)NivelAldea, Cultivo = tipo.cultivo,
                    Posicion = BancoDelMapa.Pu(p.x, p.y) };
                if (tipo.rebano != null) { q.RebanoEspecie = tipo.rebano; q.RebanoCabezas = 60_000; }
                f.Entidades.Add(new Entidad { Pueblo = q });
                Puesto.Add((q.Id, ClaseDeCosa.Aldea, $"aldea {k} (a {d:0.0} u)", p.x, p.y));
            }
            // los pueblos extractores: van donde está su nodo, a la distancia del panel (sin nodo dibujado: solo el pueblo)
            for (int j = 1; j <= Pueblos; j++)
            {
                var p = posPueblos[j - 1];
                var recurso = TiposDePueblo[IndiceDeTipo(TipoPueblo, j, TiposDePueblo.Length)].recurso;
                if (j <= nodosDePueblo.Count) { nodosDePueblo[j - 1].Posicion = BancoDelMapa.Pu(p.x, p.y); nodosDePueblo[j - 1].Tipo = recurso; }
                var q = new Pueblo { Id = $"dp-pueblo-{j}", AsentamientoMadreId = "dp-plaza", TipoExtractor = recurso == "piedra" ? "cantera" : "mina", NivelAldea = (uint)NivelPueblo, NodoId = j <= nodosDePueblo.Count ? nodosDePueblo[j - 1].Id : "",
                    Posicion = BancoDelMapa.Pu(p.x, p.y) };
                f.Entidades.Add(new Entidad { Pueblo = q });
                Puesto.Add((q.Id, ClaseDeCosa.Pueblo, $"pueblo {j} sobre su nodo (a {Math.Min(Math.Max(DistanciaPueblo, R.SeparacionNodo), Zona):0.0} u)", p.x, p.y));
            }
            // los caminos: la ruta del carro de acarreo de cada aldea y cada pueblo (de su centro al de la plaza), registrada en la red en el orden
            // en que están, con la regla del motor (`RedDeAcarreo`); la red, en tramos, como la manda el servidor (`RedCaminos`)
            TramosPuestos.Clear();
            RutasDeAcarreo.Clear();
            if (Caminos && Banco.Artefacto.Navegacion != null && Banco.Artefacto.Navegacion.Nodos.Count > 0)
            {
                var red = new RedDeAcarreo(Banco.Artefacto.Navegacion, Dato("documentos.Comercio_Caravanas.NAVEGACION.atraccionPorPeso", 0.1),
                    Dato("documentos.Comercio_Caravanas.NAVEGACION.atraccionMin", 0.5), (int)Dato("documentos.Comercio_Caravanas.RED_CAMINOS.escalonCamino", 2),
                    (int)Dato("documentos.Comercio_Caravanas.RED_CAMINOS.escalonCalzada", 6));
                long M(double u) => (long)Math.Round(u * 1000);
                foreach (var (id, clase, _, x, y) in Puesto)
                {
                    if (clase != ClaseDeCosa.Aldea && clase != ClaseDeCosa.Pueblo) continue;
                    var ruta = red.Ruta(M(x), M(y), M(centro.x), M(centro.y));
                    if (ruta == null) continue;
                    red.Registrar(ruta);
                    RutasDeAcarreo[id] = ruta.Select(p => (p.x / 1000.0, p.y / 1000.0)).ToList();
                }
                var rc = new RedCaminos();
                int t = 0;
                foreach (var (escalon, puntos) in red.Tramos(EscalonCamino == 0 ? -1 : EscalonCamino - 1))
                {
                    var tramo = new TramoDeRed { Id = $"dp-camino-{++t}", Escalon = (uint)escalon };
                    foreach (var (px, py) in puntos) tramo.Puntos.Add(new Punto { X = px * 1000, Y = py * 1000 });
                    rc.Tramos.Add(tramo);
                    TramosPuestos.Add((escalon, puntos.Select(p => ((double)p.x, (double)p.y)).ToList()));
                }
                if (rc.Tramos.Count > 0) f.Entidades.Add(new Entidad { RedCaminos = rc });
            }
            // las avanzadas, a la distancia del deslizador (nunca menos de lo que hace falta para que no se monten en la plaza)
            double dAv = DistanciaDeAvanzadas;
            for (int i = 1; i <= Avanzadas; i++)
            {
                var p = Rumbo(centro, 300 + 25 * (i - 1), dAv);
                var v = new Avanzada { Id = $"dp-avanzada-{i}", AsentamientoMadreId = "dp-plaza", Etapa = (uint)EtapaAvanzada, Posicion = BancoDelMapa.Pu(p.x, p.y),
                    Estado = this.EstadoAvanzada == 2 ? BronzeAge.Contract.V1.EstadoAvanzada.AvanzadaEnRuinas : BronzeAge.Contract.V1.EstadoAvanzada.AvanzadaActiva };
                f.Entidades.Add(new Entidad { Avanzada = v });
                if (this.EstadoAvanzada == 1)
                    f.Entidades.Add(new Entidad { Obra = new ObraDeMapa { Id = $"dp-obra-{i}", AsentamientoMadreId = "dp-plaza", Tipo = TipoObra.ObraMejoraAvanzada, Posicion = v.Posicion,
                        DestinoId = v.Id, IniciaEnMs = 1, TerminaEnMs = 9_999_999_999_999 } });
                Puesto.Add((v.Id, ClaseDeCosa.Avanzada, $"avanzada {i} (a {dAv:0.00} u)", p.x, p.y));
            }
            // la columna parada a la puerta (al radio de puerta)
            {
                var p = Rumbo(centro, 270, DistanciaDeColumna);
                var c = new Ejercito { Id = "dp-columna", FaccionId = "f-1", Tipo = TipoColumna.ColumnaPersonal, LiderId = "dp-heroe", Estado = EstadoColumna.ColumnaEstacionada,
                    Posicion = BancoDelMapa.Pu(p.x, p.y) };
                c.Participantes.Add(new Participante { HeroeId = "dp-heroe" });
                f.Entidades.Add(new Entidad { Heroe = new Heroe { Id = "dp-heroe", DisplayName = "dp-heroe", FaccionId = "f-1", ClassDefinitionId = "" } });
                foreach (var (t, i) in new[] { ("milicia_lanceros", 0), ("lenadores", 1) })
                {
                    c.EscuadronIds.Add($"dp-columna-s{i}");
                    f.Entidades.Add(new Entidad { Escuadron = new Escuadron { Id = $"dp-columna-s{i}", HeroeId = "dp-heroe", TropaId = t, Cantidad = 10, Contenedor = new ContenedorEscuadron { EjercitoId = c.Id } } });
                }
                f.Entidades.Add(new Entidad { Ejercito = c });
                Puesto.Add((c.Id, ClaseDeCosa.Ejercito, $"columna a la puerta ({DistanciaDeColumna:0.0} u)", p.x, p.y));
            }
            // los nodos que no llevan pueblo, lejos (y las teselas de los nodos, de nuevo)
            for (int j = Pueblos + 1; j <= nodosDePueblo.Count; j++) nodosDePueblo[j - 1].Posicion = BancoDelMapa.Pu(centro.x + 400 + 3 * j, centro.y);
            Mapa?.Nodos?.Soltar();
            {
            }
            if (Vecina) Plaza("dp-vecina", Nivel, Rumbo(centro, 135, R.SeparacionPlazas));
            return f;
        }

        // ------------------------------------------------------------------ las huellas, las holguras y los anillos

        /// <summary>Cómo queda un par: libre, se pisan los campos de una aldea, o se pisan las maquetas.</summary>
        public enum Pisa { Libre, Campos, Maquetas }

        /// <summary>La huella (u) de la maqueta de lo puesto con ese id, la de `CosasDelMapa` (la del aro y el clic: el modelo con su factor y,
        /// en las plazas, la muralla); y la de sus campos si es una aldea (`ParcelasDeAldea.Radios` de su nivel en las unidades de su modelo,
        /// `MaquetasDelMapa.UnidadDe`), o la misma si no tiene.</summary>
        public (double maqueta, double campos) Huellas(string id)
        {
            var c = Mapa.Cosas.FirstOrDefault(k => k.Id == id);
            double m = c?.Radio ?? 0;
            if (c?.Clase != ClaseDeCosa.Aldea) return (m, m);
            int n = Math.Max(1, Math.Min(3, (int)(c.Entidad?.Pueblo?.NivelAldea ?? 1)));
            return (m, Math.Max(m, ParcelasDeAldea.Radios[n] * MaquetasDelMapa.UnidadDe(ClaseDeCosa.Aldea)));
        }

        double Huella(string id) => Huellas(id).maqueta;

        /// <summary>Lo que hay que encuadrar con la cámara del juego: la plaza y el anillo de orilla (lo mayor de la orilla y la huella × 1,3).</summary>
        public double EncuadreNecesario => Math.Max(R.OrillaPlaza, Huella("dp-plaza") * 1.3);
        /// <summary>Si con el zoom más alejado del juego no entra lo que hay que encuadrar.</summary>
        public bool NoCabeEnElJuego { get; private set; }

        void Medir()
        {
            Holguras.Clear();
            var plaza = Puesto[0];
            void Par(string nombre, (string id, ClaseDeCosa clase, string nombre, double x, double y) a, (string id, ClaseDeCosa clase, string nombre, double x, double y) b, bool cabeza = false, double minimo = 0)
            {
                double d = Math.Sqrt((a.x - b.x) * (a.x - b.x) + (a.y - b.y) * (a.y - b.y));
                var (ma, ca) = Huellas(a.id);
                var (mb, cb) = cabeza ? (0.0, 0.0) : Huellas(b.id);
                double conCampos = d - ca - cb, maquetas = d - ma - mb;
                var pisa = maquetas < 0 ? Pisa.Maquetas : conCampos < 0 ? Pisa.Campos : Pisa.Libre;
                // (la regla de separación de ese par: si no se cumple, al menos en ámbar)
                if (minimo > 0 && d < minimo - 1e-6)
                {
                    nombre += $" · incumple: ≥ {minimo:0.##} u";
                    if (pisa == Pisa.Libre) pisa = Pisa.Campos;
                }
                Holguras.Add((nombre, d, ca, cb, conCampos, maquetas, pisa));
            }
            foreach (var p in Puesto.Skip(1))
            {
                if (p.clase == ClaseDeCosa.Aldea) Par($"plaza – {p.nombre}", plaza, p, minimo: R.SeparacionAldea);
                if (p.clase == ClaseDeCosa.Avanzada) Par($"plaza – {p.nombre}", plaza, p, minimo: MinimoDeAvanzadaEnMesa);
                if (p.clase == ClaseDeCosa.Pueblo)
                {
                    Par($"plaza – {p.nombre}", plaza, p);
                    // (al crecer: la plaza de nivel 5 con su zona al techo y su muralla)
                    double d = Math.Sqrt((plaza.x - p.x) * (plaza.x - p.x) + (plaza.y - p.y) * (plaza.y - p.y)), r5 = RadioPlaza(5, R.Techos[4], true), rq = Huella(p.id);
                    double h5 = d - r5 - rq;
                    Holguras.Add(($"plaza de nivel 5 al crecer – {p.nombre}", d, r5, rq, h5, h5, h5 < 0 ? Pisa.Maquetas : Pisa.Libre));
                }
                // (la columna: su cabeza no cuenta huella; lo que mide es si la puerta queda fuera de la maqueta)
                if (p.clase == ClaseDeCosa.Ejercito) Par($"plaza – {p.nombre}", plaza, p, cabeza: true);
                if (p.clase == ClaseDeCosa.Plaza) Par("plaza – plaza vecina", plaza, p);
            }
            foreach (var a in Puesto.Where(p => p.clase == ClaseDeCosa.Aldea))
                foreach (var b in Puesto.Where(p => p.clase == ClaseDeCosa.Pueblo || (p.clase == ClaseDeCosa.Aldea && string.CompareOrdinal(p.id, a.id) > 0)))
                    Par($"{a.nombre} – {b.nombre}", a, b, minimo: R.SeparacionAldeaPueblo);
            foreach (var a in Puesto.Where(p => p.clase == ClaseDeCosa.Pueblo))
                foreach (var b in Puesto.Where(p => p.clase == ClaseDeCosa.Pueblo && string.CompareOrdinal(p.id, a.id) > 0))
                    Par($"{a.nombre} – {b.nombre}", a, b);
        }

        /// <summary>Lo peor de lo que toca a lo puesto con ese id.</summary>
        Pisa PeorDe(string id)
        {
            var nombre = Puesto.FirstOrDefault(p => p.id == id).nombre;
            if (nombre == null) return Pisa.Libre;
            var peor = Pisa.Libre;
            foreach (var h in Holguras)
                if ((h.par.EndsWith(nombre) || h.par.StartsWith(nombre + " –") || (id == "dp-plaza" && h.par.StartsWith("plaza – ") && !h.par.Contains("columna"))) && h.pisa > peor) peor = h.pisa;
            return peor;
        }

        static readonly Dictionary<string, uint> Colores = new Dictionary<string, uint>
        {
            ["huella"] = 0xf4e9c8, ["zona"] = 0x3a6fb0, ["aldeas"] = 0x5aa83c, ["separacion"] = 0xc9c1a6, ["avanzada"] = 0x9a5cc6, ["puerta"] = 0x2fb0b0,
            ["orilla"] = 0x7fb3e0, ["vecina"] = 0x6b6b6b, ["maquetas"] = 0xd8352a, ["campos"] = 0xf0a020, ["nodo"] = 0xa88f74, ["sinAldeas"] = 0xd98ac0,
        };
        static uint ColorDe(Pisa p, uint libre) => p == Pisa.Maquetas ? Colores["maquetas"] : p == Pisa.Campos ? Colores["campos"] : libre;

        readonly List<(string texto, double x, double y, uint color)> rotulos = new List<(string, double, double, uint)>();
        /// <summary>Lo que flota sobre cada maqueta: su nombre, su tamaño, dónde y a qué altura (m sobre el suelo: su tejado más alto).</summary>
        public readonly List<(string id, string nombre, string tamano, double x, double y, float alto)> Sobre = new List<(string, string, string, double, double, float)>();
        readonly List<Label> etiquetasSobre = new List<Label>();

        /// <summary>El nombre de lo puesto para su etiqueta.</summary>
        string NombreDe((string id, ClaseDeCosa clase, string nombre, double x, double y) q)
        {
            var c = Mapa.Cosas.FirstOrDefault(x => x.Id == q.id);
            switch (q.clase)
            {
                case ClaseDeCosa.Plaza: return q.id == "dp-plaza" ? $"Plaza nivel {Nivel} · variante {Variante}" : $"Plaza vecina (nivel {Nivel})";
                case ClaseDeCosa.Aldea: return $"Aldea {q.id.Split('-').Last()} (nivel {c?.Entidad?.Pueblo?.NivelAldea ?? (uint)NivelAldea})";
                case ClaseDeCosa.Pueblo: return $"Pueblo {q.id.Split('-').Last()}: {c?.Entidad?.Pueblo?.TipoExtractor ?? "extractor"}";
                case ClaseDeCosa.Avanzada:
                    var etapa = c?.Entidad?.Avanzada?.Etapa ?? (uint)EtapaAvanzada;
                    return $"Avanzada {q.id.Split('-').Last()}: {(etapa <= 1 ? "atalaya" : etapa == 2 ? "fuerte" : "ciudadela")}";
                default: return q.nombre;
            }
        }

        /// <summary>El tamaño para la etiqueta, el de la tabla: radio y ancho; en las aldeas, el caserío y con los campos.</summary>
        string TamanoDe(string id, ClaseDeCosa clase)
        {
            var (m, cc) = Huellas(id);
            if (clase == ClaseDeCosa.Plaza)
                return $"radio {CosasDelMapa.HuellaDePlaza(Zona * 0.1, Nivel, centro.x, centro.y):0.00} u{(m > CosasDelMapa.HuellaDePlaza(Zona * 0.1, Nivel, centro.x, centro.y) + 1e-6 ? $" (con muralla {m:0.00})" : "")} · casa ≈ {CasaDeAhora:0.00} u";
            return clase == ClaseDeCosa.Aldea ? $"caserío {m:0.00} u · con campos {cc:0.00} u de radio" : $"radio {m:0.00} u · ancho {2 * m:0.0} u";
        }

        /// <summary>Lo más alto de cada maqueta (m sobre el suelo), de lo que se manda a dibujar: cada pieza, a la maqueta más cercana.</summary>
        Dictionary<string, float> Tejados()
        {
            var r = new Dictionary<string, float>();
            var cosas = Puesto.Where(q => q.clase != ClaseDeCosa.Ejercito).ToList();
            if (cosas.Count == 0 || Mapa.Maquetas == null || Mapa.Vista == null) return r;
            foreach (var e in Mapa.Maquetas.Encargos(Mapa.Vista))
            {
                if (e.EsSuelo || e.Malla == null) continue;
                var c = (Vector3)e.M.GetColumn(3);
                var (x, _, y) = EscalaDelMapa.AlMapa(c);
                var mas = cosas.OrderBy(q => (q.x - x) * (q.x - x) + (q.y - y) * (q.y - y)).First();
                var b = e.Malla.bounds;
                float top = float.MinValue;
                for (int i = 0; i < 8; i++)
                {
                    var esq = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
                    top = Mathf.Max(top, e.M.MultiplyPoint3x4(esq).y);
                }
                float suelo = Mapa.AlturaVisible(mas.x, mas.y);
                r[mas.id] = Mathf.Max(r.TryGetValue(mas.id, out var t) ? t : 0, top - suelo);
            }
            return r;
        }
        /// <summary>Los anillos que caen bajo la maqueta, dibujados también por encima, tenues (sin prueba de profundidad).</summary>
        readonly List<LineRenderer> encima = new List<LineRenderer>();
        Material materialEncima;

        void Rayas()
        {
            Mapa.RayasAdicionales.Clear();
            rotulos.Clear();
            // sobre cada maqueta, su nombre y su tamaño, por encima de su tejado más alto
            Sobre.Clear();
            var tejados = Tejados();
            foreach (var q in Puesto.Where(q => q.clase != ClaseDeCosa.Ejercito))
            {
                float alto = tejados.TryGetValue(q.id, out var t) ? t : (float)((Mapa.Cosas.FirstOrDefault(x => x.Id == q.id)?.Alto ?? 0.5) * EscalaDelMapa.MetrosPorUnidad);
                Sobre.Add((q.id, NombreDe(q), TamanoDe(q.id, q.clase), q.x, q.y, alto + 0.25f * EscalaDelMapa.MetrosPorUnidad));
            }
            var c = centro;
            double rp = Huella("dp-plaza");
            var porEncima = new List<((double x, double y) p, double r, uint color)>();
            // los rótulos, repartidos por rumbo y fuera de la maqueta
            double[] rumbos = { 75, 25, 125, 165, 210, 255, 300, 340, 50, 100, 145, 190, 235, 280, 320 };
            int siguiente = 0;
            void Aro((double x, double y) p, double r, string tipo, string texto, uint color, double ancho = 0.12)
            {
                if (!Anillos.TryGetValue(tipo, out var on) || !on || r <= 0) return;
                Mapa.RayasAdicionales.Add(CosasDelMapa.Circulo(p, r, ancho, color, 96));
                if (p == c && r < rp * 1.15) porEncima.Add((p, r, color));
                if (texto == null) return;
                double rumbo = rumbos[siguiente++ % rumbos.Length];
                var (x, y) = Rumbo(p, rumbo, p == c ? Math.Max(r, rp + 0.8) : r);
                rotulos.Add((texto, x, y, color));
            }
            // la huella de la plaza (roja o ámbar si algo se le mete)
            var pPlaza = PeorDe("dp-plaza");
            Aro(c, rp, "huella", $"maqueta {rp:0.00} u", ColorDe(pPlaza, Colores["huella"]), pPlaza == Pisa.Libre ? 0.12 : 0.2);
            // las fronteras de colocación, un círculo por radio: la zona y los bordes de los anillos de aldea de este nivel, numerados
            Aro(c, Zona, "zona", $"zona {Zona:0} u", Colores["zona"]);
            var bordes = new SortedDictionary<double, List<string>>();
            int k = 1, cupo = (int)R.Cupo[Nivel - 1];
            while (k <= cupo)
            {
                var (desde, hasta) = AnilloDeAldea(k);
                int hastaK = k;
                while (hastaK + 1 <= cupo && AnilloDeAldea(hastaK + 1) == (desde, hasta)) hastaK++;
                string nombre = hastaK == k ? $"aldea {k}" : hastaK == k + 1 ? $"aldeas {k} y {hastaK}" : $"aldeas {k} a {hastaK}";
                double h = Math.Min(hasta, Zona);
                if (h > desde)
                {
                    if (!bordes.TryGetValue(h, out var l)) bordes[h] = l = new List<string>();
                    l.Add($"{nombre}: hasta {h:0} u");
                }
                k = hastaK + 1;
            }
            foreach (var (r, textos) in bordes)
                Aro(c, r, "aldeas", Math.Abs(r - Zona) < 1e-6 ? string.Join(" · ", textos) + " (la zona)" : string.Join(" · ", textos), Colores["aldeas"]);
            Aro(c, MinimoDeAvanzada, "avanzada", $"avanzada ≥ {MinimoDeAvanzada:0.00} u", Colores["avanzada"]);
            bool puertaDentro = Holguras.Any(x => x.par.StartsWith("plaza – columna") && x.pisa != Pisa.Libre);
            Aro(c, AnilloDePuerta, "puerta", PuertaDesdeElBorde ? $"puerta: borde + {R.PuertaDesdeElBorde:0.##} u (regla nueva)" : $"puerta {R.RadioPuerta:0} u{(puertaDentro ? ": dentro de la maqueta" : "")}", Colores["puerta"]);
            Aro(c, R.SeparacionNodo, "nodo", $"nodo ≥ {R.SeparacionNodo:0.#} u (regla nueva)", Colores["nodo"]);
            Aro(c, R.OrillaPlaza, "orilla", $"orilla del río ≥ {R.OrillaPlaza:0.0} u{(rp > R.OrillaPlaza ? ": la maqueta pasa" : "")}", Colores["orilla"]);
            if (Vecina) Aro(c, R.SeparacionPlazas, "vecina", $"plazas a ≥ {R.SeparacionPlazas:0} u", Colores["vecina"]);
            Aro(c, R.SeparacionAldea, "separacion", $"aldeas a {R.SeparacionAldea:0.##} u o más de la plaza", Colores["separacion"]);
            // lo puesto alrededor: su disco de separación y sus huellas (maqueta y campos), del color de lo peor que le toca
            foreach (var p in Puesto.Where(p => p.clase == ClaseDeCosa.Aldea || p.clase == ClaseDeCosa.Pueblo || p.clase == ClaseDeCosa.Avanzada))
            {
                var peor = PeorDe(p.id);
                var (m, cc) = Huellas(p.id);
                if (p.clase != ClaseDeCosa.Avanzada) Aro((p.x, p.y), R.SeparacionAldeaPueblo, "separacion", $"sin aldeas a menos de {R.SeparacionAldeaPueblo:0.##} u", Colores["sinAldeas"]);
                Aro((p.x, p.y), m, "huella", null, ColorDe(peor == Pisa.Maquetas ? Pisa.Maquetas : Pisa.Libre, Colores["huella"]));
                if (cc > m + 1e-6) Aro((p.x, p.y), cc, "huella", null, ColorDe(peor, Colores["huella"]), 0.08);
            }
            // por encima de la maqueta, tenues, los anillos que caen bajo ella
            if (materialEncima == null)
            {
                materialEncima = new Material(Shader.Find("Hidden/Internal-Colored")) { name = "anillos por encima" };
                materialEncima.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
                materialEncima.SetInt("_ZWrite", 0);
                materialEncima.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                materialEncima.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                materialEncima.renderQueue = 4000;
            }
            while (encima.Count < porEncima.Count)
            {
                var go = new GameObject("Anillo por encima");
                go.transform.SetParent(transform, false);
                var lr = go.AddComponent<LineRenderer>();
                lr.sharedMaterial = materialEncima;
                lr.loop = true;
                lr.useWorldSpace = true;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                encima.Add(lr);
            }
            var e = EscalaDelMapa.MetrosPorUnidad;
            for (int i = 0; i < encima.Count; i++)
            {
                var lr = encima[i];
                lr.enabled = i < porEncima.Count;
                if (!lr.enabled) continue;
                var (p, r, color) = porEncima[i];
                var col = (Color)new Color32((byte)(color >> 16), (byte)(color >> 8), (byte)color, 255);
                col.a = 0.35f;
                lr.startColor = lr.endColor = col;
                lr.widthMultiplier = 0.08f * e;
                lr.positionCount = 64;
                for (int j = 0; j < 64; j++)
                {
                    double t = j * Math.PI * 2 / 64, x = p.x + r * Math.Cos(t), y = p.y + r * Math.Sin(t);
                    lr.SetPosition(j, EscalaDelMapa.AEscena(x, y, Mapa.AlturaVisible(x, y) + 0.05f * e));
                }
            }
            Mapa.RehacerRayas();
        }

        /// <summary>Pone el valor `clave` (nivel, muralla, zona, aldeas, nivelAldea, enMedio, pueblos, distanciaPueblo, avanzadas, etapa,
        /// vecina, fijos, plazas, pueblosF, avanzadasF) y lo rehace todo.</summary>
        public void Fijar(string clave, double v)
        {
            switch (clave)
            {
                case "nivel":
                    Nivel = (int)Math.Max(1, Math.Min(5, v));
                    Zona = R.Techos[Nivel - 1];
                    Aldeas = Math.Min(Aldeas, (int)R.Cupo[Nivel - 1]);
                    Pueblos = Math.Min(Pueblos, (int)R.Cupo[Nivel - 1] - Aldeas);
                    Avanzadas = Math.Min(Avanzadas, CupoDeAvanzadas(Nivel));
                    break;
                case "muralla": Muralla = v > 0.5; break;
                case "zona": Zona = Math.Max(R.RadioInicial, Math.Min(R.Techos[Nivel - 1], R.RadioInicial + Math.Round((v - R.RadioInicial) / R.Crecimiento) * R.Crecimiento)); break;
                case "aldeas": Aldeas = (int)Math.Max(0, Math.Min(R.Cupo[Nivel - 1] - Pueblos, v)); break;
                case "nivelAldea": NivelAldea = (int)Math.Max(1, Math.Min(3, v)); break;
                case "enMedio": AldeasEnMedio = v > 0.5; break;
                case "pueblos": Pueblos = (int)Math.Max(0, Math.Min(R.Cupo[Nivel - 1] - Aldeas, v)); break;
                case "distanciaPueblo": DistanciaPueblo = Math.Max(R.SeparacionNodo, Math.Min(Zona, v)); break;
                case "avanzadas": Avanzadas = (int)Math.Max(0, Math.Min(CupoDeAvanzadas(Nivel), v)); break;
                case "etapa": EtapaAvanzada = (int)Math.Max(1, Math.Min(3, v)); break;
                case "distanciaAvanzada": DistanciaAvanzada = v <= MinimoDeAvanzadaEnMesa + 0.01 ? 0 : v; break;
                case "vecina": Vecina = v > 0.5; break;
                case "tipoAldea": TipoAldea = (int)Math.Max(0, Math.Min(TiposDeAldea.Length, v)); break;
                case "tipoPueblo": TipoPueblo = (int)Math.Max(0, Math.Min(TiposDePueblo.Length, v)); break;
                case "caminos": Caminos = v > 0.5; break;
                case "tamanoFijo": TamanoFijo = v > 0.5; PonerTamanoFijo(); break;
                // (el índice del desplegable: 0 es la variante 1)
                // (el índice del desplegable: 0 es el tamaño 1)
                case "estadoAvanzada": EstadoAvanzada = (int)Math.Max(0, Math.Min(2, Math.Round(v))); break;
                case "varianteCiudadela": VarianteCiudadela = (int)Math.Max(1, Math.Min(2, Math.Round(v) + 1)); MaquetasDelMapa.VarianteCiudadelaForzada = VarianteCiudadela; break;
                case "nivelPueblo": NivelPueblo = (int)Math.Max(1, Math.Min(3, Math.Round(v) + 1)); break;
                case "variante": Variante = (int)Math.Max(1, Math.Min(2, Math.Round(v) + 1)); MaquetasDelMapa.VarianteForzada = Variante; break;
                case "escalon": EscalonCamino = (int)Math.Max(0, Math.Min(2, v)); break;
                case "puertaBorde": PuertaDesdeElBorde = v > 0.5; break;
                case "fijos": CosasDelMapa.FactorDeFijos = v; break;
                case "plazas": CosasDelMapa.FactorPlazas = v; break;
                case "pueblosF": CosasDelMapa.FactorPueblos = v; break;
                case "avanzadasF": CosasDelMapa.FactorAvanzadas = v; break;
                default: throw new ArgumentException(clave);
            }
            cambiado = true;
            RefrescarPanel?.Invoke();
        }

        void Aplicar()
        {
            almacen.Aplicar(new MensajeDeServidor { Foto = Foto() });
            Mapa.RehacerRayas();
            esperaMedir = 3;
        }

        int esperaMedir = 3;

        // ------------------------------------------------------------------ la cámara

        bool enPlanta;

        /// <summary>La cámara casi cenital que encuadra la zona (para leer distancias), o la del juego encuadrando la plaza y su orilla: el
        /// zoom habitual si entran, el alejado si no; si ni el alejado llega, `NoCabeEnElJuego` (se dice en la lectura).</summary>
        /// <summary>La cámara casi cenital sobre (x, y), encuadrando `radio` u (un detalle).</summary>
        public void Detalle(double x, double y, double radio)
        {
            enPlanta = true;
            mira = (x, y);
            almacen.Buscar(TipoEntidad.Ejercito, "ej-banco").Ejercito.Posicion = BancoDelMapa.Pu(x, y);
            Mapa.Camara.Inclinacion = 85;
            Mapa.Camara.Zoom = ZoomDelMapa.Normal;
            Mapa.EncuadreFijo = radio;
        }

        public void EnPlanta(bool planta)
        {
            enPlanta = planta;
            mira = centro;
            almacen.Buscar(TipoEntidad.Ejercito, "ej-banco").Ejercito.Posicion = BancoDelMapa.Pu(mira.x, mira.y);
            if (planta)
            {
                Mapa.Camara.Inclinacion = 85;
                Mapa.Camara.Zoom = ZoomDelMapa.Normal;
                Mapa.EncuadreFijo = Zona + 4;
                NoCabeEnElJuego = false;
                return;
            }
            Mapa.EncuadreFijo = null;
            Mapa.Camara.Inclinacion = CamaraDelMapa.InclinacionInicial;
            Encuadrar();
        }

        float comprobarEncuadre;

        /// <summary>La vista del juego: el zoom habitual y, si la plaza y su orilla no entran en pantalla, el alejado; si tampoco, se dice.</summary>
        void Encuadrar()
        {
            Mapa.Camara.Zoom = ZoomDelMapa.Normal;
            NoCabeEnElJuego = false;
            comprobarEncuadre = Time.unscaledTime + 1.5f;
        }

        /// <summary>¿Entra en pantalla el disco de `EncuadreNecesario` alrededor de la plaza?</summary>
        bool Entra()
        {
            var cam = Mapa.Vista;
            double r = EncuadreNecesario;
            for (int i = 0; i < 16; i++)
            {
                var (x, y) = Rumbo(centro, i * 22.5, r);
                var p = cam.WorldToViewportPoint(EscalaDelMapa.AEscena(x, y, Mapa.AlturaVisible(x, y)));
                if (p.z <= 0 || p.x < 0 || p.x > 1 || p.y < 0 || p.y > 1) return false;
            }
            return true;
        }

        // ------------------------------------------------------------------ el panel

        Action RefrescarPanel;
        Label lectura, resumen;
        VisualElement capaRotulos, panel, panelPisa, filasPisa, cajaLectura;
        Foldout libres;
        Label cambiadosMotor;
        /// <summary>Las reglas tal como las da el motor (`R` es la copia que el panel deja cambiar; los `DatosDeJuego` no se tocan).</summary>
        Reglas motorOriginal;
        readonly List<Action> refrescosMotor = new List<Action>();

        /// <summary>Un valor que el panel deja cambiar: grupo, nombre, unidad, el dato del motor, nivel (0 a 4, o −1), paso de − y +, mínimo,
        /// y cómo se lee y se pone en unas reglas.</summary>
        sealed class Editable
        {
            public string Grupo, Nombre, Unidad, Dato;
            public int Nivel = -1;
            public double Paso, Minimo;
            public Func<Reglas, double> Leer;
            public Action<Reglas, double> Poner;
        }

        static readonly Editable[] Editables = Hacer();

        static Editable[] Hacer()
        {
            var l = new List<Editable>();
            const string EC = "Economia_Construccion.";
            void E(string g, string n, string u, string dato, double paso, double min, Func<Reglas, double> leer, Action<Reglas, double> poner, int nivel = -1) =>
                l.Add(new Editable { Grupo = g, Nombre = n, Unidad = u, Dato = dato, Paso = paso, Minimo = min, Leer = leer, Poner = poner, Nivel = nivel });
            E("Zona", "al fundar", "u", EC + "ZONA_INFLUENCIA.radioInicial", 1, 0, r => r.RadioInicial, (r, v) => r.RadioInicial = v);
            E("Zona", "por edificio", "u", EC + "ZONA_INFLUENCIA.crecimientoPorEdificioCompletado", 0.5, 0.5, r => r.Crecimiento, (r, v) => r.Crecimiento = v);
            for (int i = 0; i < 5; i++)
            {
                int n = i;
                E("Zona", "techo por nivel", "u", EC + $"ZONA_INFLUENCIA.radioMaximoPorNivel.{n + 1}", 1, 0, r => r.Techos[n], (r, v) => r.Techos[n] = v, n);
            }
            E("Plazas", "entre plazas", "u", EC + "FUNDACION.separacionPlazas", 1, 0, r => r.SeparacionPlazas, (r, v) => r.SeparacionPlazas = v);
            E("Plazas", "junto a mercenarios", "u", "Campamentos_Mercenarios.MERCENARIOS.radioExclusionFundar", 1, 0, r => r.ExclusionCampamento, (r, v) => r.ExclusionCampamento = v);
            for (int i = 0; i < 5; i++)
            {
                int n = i;
                E("Plazas", "radio de maqueta *", "u", $"REGLA NUEVA: radio de la maqueta de plaza, nivel {n + 1}", 0.05, 0.1, r => r.RadioPlaza[n], (r, v) => r.RadioPlaza[n] = v, n);
            }
            E("Plazas", "casa (u) *", "u", "REGLA NUEVA: vivienda de base (recalcula los radios de maqueta)", 0.01, 0.05, r => r.CasaU,
                (r, v) => { r.CasaU = v; var rr = RadiosDeCasa(v); for (int k = 0; k < 5; k++) r.RadioPlaza[k] = rr[k]; });
            for (int i = 0; i < 5; i++)
            {
                int n = i;
                E("Pueblos y aldeas", "cupo por nivel", "", EC + $"PUEBLOS_EXTRACTORES.cupoPorNivel.{n + 1}", 1, 0, r => r.Cupo[n], (r, v) => r.Cupo[n] = Math.Round(v), n);
            }
            E("Pueblos y aldeas", "aldea a plaza", "u", EC + "PUEBLOS_EXTRACTORES.aldea.separacionMinima", 0.5, 0, r => r.SeparacionAldea, (r, v) => r.SeparacionAldea = v);
            E("Pueblos y aldeas", "aldea a otro pueblo *", "u", "REGLA NUEVA: separación de aldea a otro pueblo (extractor o aldea)", 0.5, 0, r => r.SeparacionAldeaPueblo, (r, v) => r.SeparacionAldeaPueblo = v);
            E("Avanzadas", "fracción de la zona", "", EC + "AVANZADAS.fraccionCiudadDeLaZona", 0.01, 0, r => r.FraccionCiudad, (r, v) => r.FraccionCiudad = v);
            E("Avanzadas", "margen fijo (½ tamaño)", "u", EC + "TAMANOS_MAPA.avanzada (÷ 2)", 0.05, 0, r => r.TamanoAvanzada / 2, (r, v) => r.TamanoAvanzada = 2 * v);
            E("Avanzadas", "cupo por nivel de la madre", "", EC + "AVANZADAS.cupoPorNivelDeLaMadre", 1, 0, r => r.CupoAvanzadasPorNivel, (r, v) => r.CupoAvanzadasPorNivel = Math.Round(v));
            E("Avanzadas", "desde el nivel", "", EC + "AVANZADAS.nivelMadre.atalaya", 1, 1, r => r.NivelAtalaya, (r, v) => r.NivelAtalaya = Math.Round(v));
            E("Pueblos y aldeas", "separación de nodo *", "u", "REGLA NUEVA: separación plaza–nodo (del centro al nodo)", 1, 0, r => r.SeparacionNodo, (r, v) => r.SeparacionNodo = v);
            E("Columnas y río", "radio de puerta", "u", "Movimiento_Ejercitos.MOVIMIENTO.radioPuerta", 0.25, 0, r => r.RadioPuerta, (r, v) => r.RadioPuerta = v);
            E("Columnas y río", "puerta desde el borde *", "u", "REGLA NUEVA: puerta desde el borde de la maqueta (todo el contorno)", 0.25, 0, r => r.PuertaDesdeElBorde, (r, v) => r.PuertaDesdeElBorde = v);
            E("Columnas y río", "orilla: plaza", "u", EC + "ORILLA_RIO.radio.plaza", 0.25, 0, r => r.OrillaPlaza, (r, v) => r.OrillaPlaza = v);
            E("Columnas y río", "orilla: pueblo", "u", EC + "ORILLA_RIO.radio.pueblo", 0.25, 0, r => r.OrillaPueblo, (r, v) => r.OrillaPueblo = v);
            E("Columnas y río", "orilla: aldea", "u", EC + "ORILLA_RIO.radio.aldea", 0.25, 0, r => r.OrillaAldea, (r, v) => r.OrillaAldea = v);
            E("Columnas y río", "orilla: avanzada", "u", EC + "ORILLA_RIO.radio.avanzada", 0.25, 0, r => r.OrillaAvanzada, (r, v) => r.OrillaAvanzada = v);
            return l.ToArray();
        }

        static Reglas Copia(Reglas r)
        {
            var c = new Reglas { DelMotor = r.DelMotor, Constantes = r.Constantes, SeparacionNodo = r.SeparacionNodo, PuertaDesdeElBorde = r.PuertaDesdeElBorde, SeparacionAldeaPueblo = r.SeparacionAldeaPueblo };
            c.Origen.AddRange(r.Origen);
            foreach (var e in Editables) e.Poner(c, e.Leer(r));
            Array.Copy(r.RadioPlaza, c.RadioPlaza, 5);
            return c;
        }

        static bool Distinto(Editable e, Reglas a, Reglas b) => Math.Abs(e.Leer(a) - e.Leer(b)) > 1e-9;
        static string Num(double v) => v.ToString("0.###", CultureInfo.GetCultureInfo("es-ES"));

        /// <summary>Los valores cambiados, con su dato del motor y los dos números («dato 3 → 5,5»).</summary>
        public List<string> Cambiados() => motorOriginal == null ? new List<string>()
            : Editables.Where(e => Distinto(e, R, motorOriginal) && !e.Dato.StartsWith("REGLA NUEVA"))
                .Select(e => $"{e.Dato} {Num(e.Leer(motorOriginal))} → {Num(e.Leer(R))}" + (e.Dato.EndsWith("aldea.separacionMinima") ? " (aldea a plaza)" : "")).ToList();

        /// <summary>Pone un valor (por su nombre, y su nivel de 1 a 5 si va por nivel) con sus límites: nada negativo, los techos no por debajo
        /// del radio al fundar; y lo rehace todo.</summary>
        public void FijarMotor(string nombre, double v, int nivel = 0)
        {
            var e = Editables.First(x => x.Nombre == nombre && x.Nivel == nivel - 1);
            Poner(e, v);
        }

        void Poner(Editable e, double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) v = e.Leer(R);
            v = Math.Max(e.Minimo, v);
            if (e.Grupo == "Zona" && e.Nivel >= 0) v = Math.Max(R.RadioInicial, v);
            e.Poner(R, v);
            if (e.Nombre == "al fundar") for (int i = 0; i < 5; i++) R.Techos[i] = Math.Max(R.Techos[i], R.RadioInicial);
            Fijar("nivel", Nivel);
            foreach (var r in refrescosMotor) r();
        }

        /// <summary>La propuesta de Víctor (2026-11-23): zona al fundar 24 u y techos 30/50/70/80/90; 60 u entre plazas y junto a mercenarios;
        /// separación de aldea 15 u; orilla de plaza 15 u; separación de nodo 12 u; la puerta a 1 u del borde; y lo fijo × 2 con las plazas × 0,5.
        /// Solo en la memoria de esta escena.</summary>
        public void CargarPropuesta()
        {
            R.RadioInicial = 24;
            (R.Techos[0], R.Techos[1], R.Techos[2], R.Techos[3], R.Techos[4]) = (30, 50, 70, 80, 90);
            (R.SeparacionPlazas, R.ExclusionCampamento, R.SeparacionAldea, R.OrillaPlaza, R.SeparacionNodo, R.PuertaDesdeElBorde) = (60, 60, 15, 15, 12, 1);
            R.SeparacionAldeaPueblo = 7.5;
            PuertaDesdeElBorde = true;
            (CosasDelMapa.FactorDeFijos, CosasDelMapa.FactorPlazas) = (2, 0.5);
            Fijar("nivel", Nivel);
            foreach (var r in refrescosMotor) r();
        }

        public void VolverAlMotor()
        {
            foreach (var e in Editables) e.Poner(R, e.Leer(motorOriginal));
            Array.Copy(motorOriginal.RadioPlaza, R.RadioPlaza, 5);
            Fijar("nivel", Nivel);
            foreach (var r in refrescosMotor) r();
        }

        /// <summary>La tabla de valores: por grupos con cabecera, un valor por fila (nombre a la izquierda; campo con − y + a la derecha, con su
        /// unidad); los de cada nivel, en una fila de cinco con el del nivel elegido en dorado; lo cambiado en otro color, con el del motor al
        /// lado y su botón para devolverlo; y los derivados (anillos de aldea, mínimo de avanzada) en texto.</summary>
        void ValoresEditables(VisualElement motor, Color claro)
        {
            motorOriginal ??= Copia(R);
            var oscuro = new Color(0.1f, 0.08f, 0.06f);
            var dorado = new Color(1f, 0.78f, 0.35f);
            var cambiado = new Color(0.55f, 0.85f, 1f);
            Label Rotulo(VisualElement en, string t, float ancho, int tam = 12, bool negrita = false)
            {
                var l = new Label(t);
                if (ancho > 0) l.style.width = ancho;
                l.style.fontSize = tam;
                l.style.color = claro;
                l.style.unityTextAlign = TextAnchor.MiddleLeft;
                if (negrita) l.style.unityFontStyleAndWeight = FontStyle.Bold;
                en.Add(l);
                return l;
            }
            VisualElement Fila(VisualElement en)
            {
                var r = new VisualElement();
                r.style.flexDirection = FlexDirection.Row;
                r.style.alignItems = Align.Center;
                r.style.minHeight = 22;
                en.Add(r);
                return r;
            }
            Button Boton(VisualElement en, string t, Action a)
            {
                var b = new Button(a) { text = t };
                b.style.width = 18;
                b.style.height = 18;
                b.style.paddingLeft = b.style.paddingRight = b.style.paddingTop = b.style.paddingBottom = 0;
                b.style.marginLeft = b.style.marginRight = 1;
                b.style.fontSize = 12;
                b.style.color = oscuro;
                b.style.backgroundColor = new Color(0.85f, 0.8f, 0.7f);
                b.style.unityFontStyleAndWeight = FontStyle.Bold;
                b.style.unityTextAlign = TextAnchor.MiddleCenter;
                en.Add(b);
                return b;
            }
            // un campo con − y + (Intro o perder el foco aplica; coma o punto)
            void Campo(VisualElement en, Editable e, float ancho, bool conBotones)
            {
                if (conBotones) Boton(en, "−", () => Poner(e, Math.Round((e.Leer(R) - e.Paso) / e.Paso) * e.Paso));
                var f = new TextField { isDelayed = true, value = Num(e.Leer(R)) };
                f.style.width = ancho;
                f.style.marginLeft = f.style.marginRight = 1;
                // (el tema deja el texto del campo claro sobre fondo claro: se pinta aquí, en el campo, su entrada y su texto)
                f.style.color = oscuro;
                if (f.Q(className: TextField.inputUssClassName) is VisualElement entrada) { entrada.style.color = oscuro; entrada.style.backgroundColor = new Color(0.96f, 0.93f, 0.86f); entrada.style.paddingTop = entrada.style.paddingBottom = 1; entrada.style.paddingLeft = entrada.style.paddingRight = 4; entrada.style.marginTop = entrada.style.marginBottom = entrada.style.marginLeft = entrada.style.marginRight = 0; }
                f.style.height = 20;
                f.Query<TextElement>().ForEach(t => { t.style.color = oscuro; t.style.fontSize = 12; });
                f.RegisterValueChangedCallback(ev =>
                {
                    if (double.TryParse(ev.newValue.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var v)) Poner(e, v);
                    else f.SetValueWithoutNotify(Num(e.Leer(R)));
                });
                if (conBotones) Boton(en, "+", () => Poner(e, Math.Round((e.Leer(R) + e.Paso) / e.Paso) * e.Paso));
                refrescosMotor.Add(() =>
                {
                    f.SetValueWithoutNotify(Num(e.Leer(R)));
                    bool cam = Distinto(e, R, motorOriginal), delNivel = e.Nivel == Nivel - 1;
                    var borde = cam ? cambiado : dorado;
                    f.style.borderTopWidth = f.style.borderBottomWidth = f.style.borderLeftWidth = f.style.borderRightWidth = cam || delNivel ? 2 : 0;
                    f.style.borderTopColor = f.style.borderBottomColor = f.style.borderLeftColor = f.style.borderRightColor = borde;
                    f.tooltip = $"{e.Dato} · motor: {Num(e.Leer(motorOriginal))}";
                });
                en.Add(f);
            }
            // a la derecha de la fila: lo del motor si se ha cambiado, y su botón para devolverlo
            void Motor(VisualElement fila, IReadOnlyList<Editable> es)
            {
                var l = Rotulo(fila, "", 0, 11);
                l.style.marginLeft = 4;
                l.style.color = cambiado;
                var b = Boton(fila, "↺", () => { foreach (var e in es) e.Poner(R, e.Leer(motorOriginal)); Fijar("nivel", Nivel); foreach (var r in refrescosMotor) r(); });
                b.tooltip = "volver al valor del motor";
                refrescosMotor.Add(() =>
                {
                    bool cam = es.Any(e => Distinto(e, R, motorOriginal));
                    l.text = cam ? "motor: " + string.Join(" · ", es.Select(e => Num(e.Leer(motorOriginal)))) : "";
                    b.style.display = cam ? DisplayStyle.Flex : DisplayStyle.None;
                });
            }
            foreach (var grupo in Editables.Select(e => e.Grupo).Distinct())
            {
                var cab = Rotulo(motor, grupo, 0, 13, true);
                cab.style.marginTop = 6;
                cab.style.color = new Color(0.95f, 0.88f, 0.7f);
                cab.style.borderBottomWidth = 1;
                cab.style.borderBottomColor = new Color(0.95f, 0.88f, 0.7f, 0.3f);
                var deGrupo = Editables.Where(e => e.Grupo == grupo).ToList();
                foreach (var nombre in deGrupo.Select(e => e.Nombre).Distinct())
                {
                    var es = deGrupo.Where(e => e.Nombre == nombre).OrderBy(e => e.Nivel).ToList();
                    if (es[0].Nivel >= 0)
                    {
                        // por nivel: una cabecera N1..N5 y una fila de cinco
                        var encabezado = Fila(motor);
                        encabezado.style.minHeight = 14;
                        Rotulo(encabezado, "", 150, 10);
                        for (int n = 1; n <= 5; n++)
                        {
                            var ln = Rotulo(encabezado, $"N{n}", 46, 10);
                            ln.style.unityTextAlign = TextAnchor.MiddleCenter;
                            int nn = n;
                            refrescosMotor.Add(() => ln.style.color = nn == Nivel ? dorado : claro);
                        }
                        var f = Fila(motor);
                        Rotulo(f, nombre + (es[0].Unidad != "" ? $" ({es[0].Unidad})" : ""), 150);
                        foreach (var e in es) Campo(f, e, 44, false);
                        Motor(f, es);
                    }
                    else
                    {
                        var f = Fila(motor);
                        Rotulo(f, nombre, 150);
                        Campo(f, es[0], 58, true);
                        Rotulo(f, es[0].Unidad, 18, 11).style.marginLeft = 2;
                        Motor(f, es);
                    }
                }
                if (grupo == "Pueblos y aldeas")
                {
                    var anillos = Rotulo(motor, "", 0, 12);
                    anillos.style.whiteSpace = WhiteSpace.Normal;
                    refrescosMotor.Add(() => anillos.text = "anillos de aldea (u): " + AnillosEnTexto());
                }
                if (grupo == "Avanzadas")
                {
                    var minimo = Rotulo(motor, "", 0, 12);
                    refrescosMotor.Add(() => minimo.text = $"mínimo con la zona de ahora ({Zona:0} u): {MinimoDeAvanzada:0.00} u · cupo en el nivel {Nivel}: {CupoDeAvanzadas(Nivel)}");
                }
            }
            foreach (var r in refrescosMotor) r();
        }

        string AnillosEnTexto()
        {
            var anillos = new List<string>();
            int k = 1, cupo = (int)Math.Max(1, R.Cupo[4]);
            while (k <= cupo)
            {
                var (desde, hasta) = AnilloDeAldea(k);
                int hastaK = k;
                while (hastaK + 1 <= cupo && AnilloDeAldea(hastaK + 1) == (desde, hasta)) hastaK++;
                anillos.Add((hastaK > k ? $"{k} y {hastaK}" : $"{k}") + (desde == 0 ? $" hasta {Num(hasta)}" : $" de {Num(desde)} a {Num(hasta)}"));
                k = hastaK + 1;
            }
            return string.Join("; ", anillos);
        }

        /// <summary>Los valores del motor, una línea por valor, con lo que toca al nivel elegido resaltado.</summary>
        public string ValoresDelMotor(bool resaltar = true)
        {
            string U(double v) => v.ToString("0.##", CultureInfo.GetCultureInfo("es-ES"));
            string Res(string t) => resaltar ? $"<color=#ffd27a><b>{t}</b></color>" : $"[{t}]";
            string PorNivel(double[] v) => string.Join(" · ", v.Select((x, i) => i == Nivel - 1 ? Res(U(x)) : U(x)));
            var anillos = new List<string>();
            int k = 1;
            while (k <= (int)R.Cupo[4])
            {
                var (desde, hasta) = AnilloDeAldea(k);
                int hastaK = k;
                while (hastaK + 1 <= (int)R.Cupo[4] && AnilloDeAldea(hastaK + 1) == (desde, hasta)) hastaK++;
                string t = (hastaK > k ? $"{k} y {hastaK}" : $"{k}") + (desde == 0 ? $" hasta {U(hasta)}" : $" de {U(desde)} a {U(hasta)}");
                anillos.Add(hastaK <= R.Cupo[Nivel - 1] ? Res(t) : t);
                k = hastaK + 1;
            }
            return string.Join("\n", new[]
            {
                $"Zona: {U(R.RadioInicial)} u al fundar, +{U(R.Crecimiento)} u por edificio; techo por nivel {PorNivel(R.Techos)} u",
                $"Entre plazas: {U(R.SeparacionPlazas)} u · junto a mercenarios: {U(R.ExclusionCampamento)} u",
                $"Cupo de pueblos por nivel: {PorNivel(R.Cupo)}",
                $"Aldea: a {U(R.SeparacionAldea)} u de la plaza y a {U(R.SeparacionAldeaPueblo)} u de otro pueblo; anillos (u): {string.Join("; ", anillos)}",
                $"Avanzada: zona × {U(R.FraccionCiudad)} + {U(R.TamanoAvanzada / 2)} u = {Res($"{MinimoDeAvanzada:0.00} u")}; cupo {Res(U(CupoDeAvanzadas(Nivel)))} (nivel × {U(R.CupoAvanzadasPorNivel)} desde el {U(R.NivelAtalaya)})",
                $"Radio de puerta: {U(R.RadioPuerta)} u",
                $"Orilla del río: plaza {U(R.OrillaPlaza)} · pueblo {U(R.OrillaPueblo)} · aldea {U(R.OrillaAldea)} · avanzada {U(R.OrillaAvanzada)} u",
            });
        }
        readonly List<Label> etiquetas = new List<Label>();

        public void VerPanel(bool ver)
        {
            if (panel != null) panel.style.display = ver ? DisplayStyle.Flex : DisplayStyle.None;
            if (panelPisa != null) panelPisa.style.display = ver ? DisplayStyle.Flex : DisplayStyle.None;
            if (cajaLectura != null) cajaLectura.style.display = ver ? DisplayStyle.Flex : DisplayStyle.None;
        }

        static string Fila((string par, double d, double ra, double rb, double holgura, double maquetas, Pisa pisa) h) =>
            $"{h.par}: {h.holgura:+0.00;−0.00} u{(Math.Abs(h.holgura - h.maquetas) > 1e-6 ? $" (caserío {h.maquetas:+0.00;−0.00})" : "")}"
            + (h.pisa == Pisa.Maquetas ? " · se pisan las maquetas" : h.pisa == Pisa.Campos ? " · se pisan los campos" : "");

        public string Valores()
        {
            var sb = new StringBuilder();
            sb.Append($"nivel={Nivel}; muralla={(Muralla && Nivel >= 2 ? "sí" : "no")}; zona={Zona:0}; aldeas={Aldeas} (nivel {NivelAldea}, {(AldeasEnMedio ? "en medio" : "lo más cerca")}); ");
            sb.Append($"pueblos={Pueblos} a {DistanciaPueblo:0.0} u; avanzadas={Avanzadas} (etapa {EtapaAvanzada}, a {DistanciaDeAvanzadas:0.00} u, mínimo {MinimoDeAvanzadaEnMesa:0.00}); ");
            sb.Append(string.Format(CultureInfo.InvariantCulture, "fijos={0:0.00}; plazas={1:0.00}; pueblos={2:0.00}; avanzadas={3:0.00}\n", CosasDelMapa.FactorDeFijos, CosasDelMapa.FactorPlazas, CosasDelMapa.FactorPueblos, CosasDelMapa.FactorAvanzadas));
            sb.Append($"regla nueva: puerta desde el borde {(PuertaDesdeElBorde ? $"= {R.PuertaDesdeElBorde:0.##} u (todo el contorno)" : "apagada (2 u desde el centro, como el motor)")}; regla nueva: separación de nodo = {R.SeparacionNodo:0.##} u (del centro de la plaza al nodo); regla nueva: separación de aldea a otro pueblo (extractor o aldea) = {R.SeparacionAldeaPueblo:0.##} u\n");
            sb.Append(TamanoFijo
                ? $"regla nueva: radio de plaza por nivel: {string.Join(" / ", R.RadioPlaza.Select(r => r.ToString("0.##", CultureInfo.GetCultureInfo("es-ES"))))} u · casa {R.CasaU.ToString("0.##", CultureInfo.GetCultureInfo("es-ES"))} u (sin zona ni factores)\n"
                : "radio de plaza: según la zona (como el motor)\n");
            sb.Append($"maqueta de la plaza {Huella("dp-plaza"):0.00} u; encuadre necesario {EncuadreNecesario:0.0} u{(NoCabeEnElJuego ? " (no cabe con el zoom del juego)" : "")}\n");
            sb.Append($"vista: aldeas {(TipoAldea < TiposDeAldea.Length ? TiposDeAldea[TipoAldea].nombre : "variadas")}; pueblos {(TipoPueblo < TiposDePueblo.Length ? TiposDePueblo[TipoPueblo].nombre : "variados")}; caminos {(Caminos ? new[] { "según el motor", "todo sendero", "todo camino" }[EscalonCamino] : "no")}; plaza variante {Variante}; pueblos de tamaño {NivelPueblo}; avanzadas {new[] { "operativas", "en obras", "en ruina" }[EstadoAvanzada]}, ciudadela {VarianteCiudadela}\n");
            var cambiadosM = Cambiados();
            if (cambiadosM.Count > 0) sb.Append("motor: " + string.Join("; ", cambiadosM) + "\n");
            foreach (var t in Sobre) sb.Append($"{t.nombre}: {t.tamano}\n");
            foreach (var h in Holguras.OrderByDescending(h => h.pisa))
                sb.Append($"{h.par}: {h.d:0.00} − ({h.ra:0.00} + {h.rb:0.00}) = {h.holgura:+0.00;−0.00}; caserío {h.maquetas:+0.00;−0.00}"
                          + (h.pisa == Pisa.Maquetas ? " SE PISAN LAS MAQUETAS" : h.pisa == Pisa.Campos ? " se pisan los campos" : "") + "\n");
            return sb.ToString();
        }

        public void Restablecer()
        {
            (CosasDelMapa.FactorDeFijos, CosasDelMapa.FactorPlazas, CosasDelMapa.FactorPueblos, CosasDelMapa.FactorAvanzadas) = (2, 0.5, 1, 1);
            (Nivel, Aldeas, NivelAldea, Pueblos, Avanzadas, EtapaAvanzada, Muralla, AldeasEnMedio, Vecina, DistanciaPueblo) = (3, 2, 2, 1, 0, 1, true, false, false, R.SeparacionAldea);
            (TipoAldea, TipoPueblo, Caminos, EscalonCamino, TamanoFijo, Variante, NivelPueblo, EstadoAvanzada, VarianteCiudadela) = (0, 0, true, 0, true, 1, 2, 0, 1);
            DistanciaAvanzada = 0;
            MaquetasDelMapa.VarianteCiudadelaForzada = VarianteCiudadela;
            MaquetasDelMapa.VarianteForzada = Variante;
            R.CasaU = CasaPorDefecto;
            Array.Copy(RadiosFijos, R.RadioPlaza, 5);
            PonerTamanoFijo();
            foreach (var r in refrescosMotor) r();
            Zona = R.Techos[Nivel - 1];
            cambiado = true;
            RefrescarPanel?.Invoke();
        }

        /// <summary>Un botón que se lee: texto oscuro sobre fondo claro (el del tema lo deja apagado).</summary>
        static Button Claro(Button b)
        {
            b.style.color = new Color(0.12f, 0.1f, 0.08f);
            b.style.backgroundColor = new Color(0.9f, 0.85f, 0.74f);
            b.style.unityFontStyleAndWeight = FontStyle.Bold;
            b.style.fontSize = 12;
            return b;
        }

        const int AnchoDerecha = 430;

        readonly List<Action<bool>> plegadores = new List<Action<bool>>();

        /// <summary>Pliega (o abre) las tres cajas de los lados a solo su título, con la lectura de abajo (las pruebas y la tecla).</summary>
        public void Plegar(bool plegar)
        {
            foreach (var p in plegadores) p(plegar);
            if (cajaLectura != null) cajaLectura.style.display = plegar ? DisplayStyle.None : DisplayStyle.Flex;
        }

        /// <summary>Un botón en la cabecera de la caja (junto a su título) que la pliega a solo el título y la vuelve a abrir.</summary>
        void Plegable(VisualElement caja, Label titulo)
        {
            var fila = new VisualElement();
            fila.style.flexDirection = FlexDirection.Row;
            fila.style.justifyContent = Justify.SpaceBetween;
            fila.style.alignItems = Align.Center;
            fila.style.flexShrink = 0;
            caja.Insert(caja.IndexOf(titulo), fila);
            fila.Add(titulo);
            var b = Claro(new Button { text = "–" });
            b.tooltip = "plegar / abrir (H: todas)";
            b.style.width = 24;
            b.style.height = 20;
            b.style.paddingLeft = b.style.paddingRight = b.style.paddingTop = b.style.paddingBottom = 0;
            fila.Add(b);
            var crece = caja.style.flexGrow;
            var minimo = caja.style.minHeight;
            bool plegada = false;
            void Poner(bool p)
            {
                plegada = p;
                b.text = p ? "+" : "–";
                foreach (var h in caja.Children()) if (h != fila) h.style.display = p ? DisplayStyle.None : DisplayStyle.Flex;
                caja.style.flexGrow = p ? new StyleFloat(0f) : crece;
                caja.style.minHeight = p ? new StyleLength(StyleKeyword.Auto) : minimo;
            }
            b.clicked += () => Poner(!plegada);
            plegadores.Add(Poner);
        }

        bool todasPlegadas;

        static VisualElement Caja(bool derecha)
        {
            var v = new VisualElement();
            v.style.position = Position.Absolute;
            if (derecha) v.style.right = 16; else v.style.left = 16;
            v.style.top = 16;
            v.style.width = derecha ? AnchoDerecha : 440;
            v.style.maxHeight = new Length(96, LengthUnit.Percent);
            v.style.paddingLeft = v.style.paddingRight = v.style.paddingTop = v.style.paddingBottom = 10;
            v.style.backgroundColor = new Color(0.12f, 0.1f, 0.08f, 0.9f);
            v.style.borderTopLeftRadius = v.style.borderTopRightRadius = v.style.borderBottomLeftRadius = v.style.borderBottomRightRadius = 6;
            return v;
        }

        void Panel()
        {
            var ajustes = ScriptableObject.CreateInstance<PanelSettings>();
            ajustes.themeStyleSheet = Resources.Load<ThemeStyleSheet>("MundoUI/TemaMundo");
            ajustes.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            ajustes.referenceResolution = new Vector2Int(1920, 1080);
            ajustes.match = 0.5f;
            var hijo = new GameObject("Panel de las distancias");
            hijo.SetActive(false);
            hijo.transform.SetParent(transform, false);
            var doc = hijo.AddComponent<UIDocument>();
            doc.panelSettings = ajustes;
            hijo.SetActive(true);
            var raiz = doc.rootVisualElement;
            raiz.pickingMode = PickingMode.Ignore;
            var estilo = Resources.Load<StyleSheet>("MundoUI/Mundo");
            if (estilo != null) raiz.styleSheets.Add(estilo);
            // los desplegables y su lista abierta (que el panel pone en su raíz, no bajo el desplegable)
            if (Resources.Load<StyleSheet>("MundoUI/Distancias") is StyleSheet estiloDp) (raiz.panel?.visualTree ?? raiz).styleSheets.Add(estiloDp);
            capaRotulos = new VisualElement { pickingMode = PickingMode.Ignore };
            capaRotulos.style.position = Position.Absolute;
            capaRotulos.style.left = capaRotulos.style.top = capaRotulos.style.right = capaRotulos.style.bottom = 0;
            raiz.Add(capaRotulos);
            var claro = new Color(0.9f, 0.86f, 0.78f);
            Label Texto(VisualElement en, string t, int tam = 12, bool negrita = false)
            {
                var l = new Label(t);
                l.style.fontSize = tam;
                l.style.color = claro;
                l.style.whiteSpace = WhiteSpace.Normal;
                if (negrita) l.style.unityFontStyleAndWeight = FontStyle.Bold;
                en.Add(l);
                return l;
            }

            // a la derecha, los controles
            panel = Caja(true);
            var refrescos = new List<Action>();
            Slider Deslizador(string nombre, string clave, float min, float max, Func<double> leer, bool entero = false)
            {
                string Rotulo() => $"{nombre}: {leer().ToString(entero ? "0" : "0.00", CultureInfo.GetCultureInfo("es-ES"))}";
                var s = new Slider(Rotulo(), min, max) { value = (float)leer() };
                s.labelElement.style.minWidth = 190;
                s.labelElement.style.color = claro;
                s.style.marginTop = s.style.marginBottom = 1;
                s.style.height = 24;
                s.RegisterValueChangedCallback(ev => Fijar(clave, entero ? Math.Round(ev.newValue) : Math.Round(ev.newValue, 2)));
                refrescos.Add(() => { if (Math.Abs(s.value - leer()) > 1e-3) s.SetValueWithoutNotify((float)leer()); s.label = Rotulo(); });
                panel.Add(s);
                return s;
            }
            DropdownField Desplegable(string nombre, string clave, string[] opciones, Func<int> leer)
            {
                var d = new DropdownField(nombre, opciones.ToList(), leer());
                d.labelElement.style.minWidth = 190;
                d.labelElement.style.color = claro;
                d.AddToClassList("dp-desplegable");
                d.style.marginTop = d.style.marginBottom = 1;
                d.style.height = 24;
                d.RegisterValueChangedCallback(ev => Fijar(clave, Array.IndexOf(opciones, ev.newValue)));
                refrescos.Add(() => { if (d.index != leer()) d.SetValueWithoutNotify(opciones[leer()]); });
                panel.Add(d);
                return d;
            }
            Toggle Casilla(VisualElement en, string nombre, Func<bool> leer, Action<bool> poner, bool media = false)
            {
                var t = new Toggle(nombre) { value = leer() };
                t.labelElement.style.color = claro;
                t.labelElement.style.minWidth = media ? 120 : 170;
                if (media) { t.labelElement.style.minWidth = 0; t.labelElement.style.width = 150; t.style.marginRight = 6; }
                t.style.height = 24;
                t.style.marginTop = t.style.marginBottom = media ? 0 : 1;
                t.RegisterValueChangedCallback(ev => { poner(ev.newValue); cambiado = true; });
                refrescos.Add(() => t.SetValueWithoutNotify(leer()));
                en.Add(t);
                return t;
            }
            var titulo = Texto(panel, "Distancias de plaza", 18, true);
            titulo.style.color = new Color(0.95f, 0.88f, 0.7f);
            Plegable(panel, titulo);
            Texto(panel, "La plaza", 13, true);
            Deslizador("Nivel", "nivel", 1, 5, () => Nivel, true);
            Desplegable("  variante", "variante", new[] { "1", "2" }, () => Variante - 1);
            Casilla(panel, "Muralla (nivel 2+)", () => Muralla, v => Fijar("muralla", v ? 1 : 0));
            Deslizador("Zona (u)", "zona", (float)R.RadioInicial, 60, () => Zona, true);
            Casilla(panel, "Plaza vecina a la separación", () => Vecina, v => Fijar("vecina", v ? 1 : 0));
            Casilla(panel, "Puerta desde el borde (regla nueva)", () => PuertaDesdeElBorde, v => Fijar("puertaBorde", v ? 1 : 0));
            // (sin la casilla «Plaza de tamaño fijo por nivel»: con los cinco niveles a la medida de la choza, el radio lo da el modelo)
            Texto(panel, "Aldeas, pueblos y avanzadas", 13, true);
            var dAldeas = Deslizador("Aldeas", "aldeas", 0, 6, () => Aldeas, true);
            refrescos.Insert(0, () => dAldeas.highValue = (float)Math.Max(1, R.Cupo[Nivel - 1]));
            Deslizador("  su nivel", "nivelAldea", 1, 3, () => NivelAldea, true);
            Casilla(panel, "  en medio de su anillo (si no, lo más cerca)", () => AldeasEnMedio, v => Fijar("enMedio", v ? 1 : 0));
            Desplegable("  tipo (todas)", "tipoAldea", TiposDeAldea.Select(t => t.nombre).Append("variado").ToArray(), () => TipoAldea);
            var dPueblos = Deslizador("Pueblos extractores", "pueblos", 0, 6, () => Pueblos, true);
            refrescos.Insert(0, () => dPueblos.highValue = (float)Math.Max(1, R.Cupo[Nivel - 1]));
            Deslizador("  a (u)", "distanciaPueblo", 0, 60, () => DistanciaPueblo);
            Desplegable("  tipo (todos)", "tipoPueblo", TiposDePueblo.Select(t => t.nombre).Append("variado").ToArray(), () => TipoPueblo);
            Desplegable("  tamaño", "nivelPueblo", new[] { "1 (3 chozas)", "2 (5 chozas)", "3 (7 chozas)" }, () => NivelPueblo - 1);
            Casilla(panel, "Caminos de los carros a la plaza", () => Caminos, v => Fijar("caminos", v ? 1 : 0));
            Desplegable("  escalón", "escalon", new[] { "según el motor (peso)", "todo sendero", "todo camino" }, () => EscalonCamino);
            Deslizador("Avanzadas", "avanzadas", 0, 5, () => Avanzadas, true);
            Deslizador("  etapa", "etapa", 1, 3, () => EtapaAvanzada, true);
            Desplegable("  estado", "estadoAvanzada", new[] { "operativa", "en obras (mejora)", "en ruina" }, () => EstadoAvanzada);
            Desplegable("  ciudadela", "varianteCiudadela", new[] { "1", "2" }, () => VarianteCiudadela - 1);
            // (del centro de la plaza al de la avanzada; abajo, lo justo para que no se monten)
            var dDistAv = Deslizador("  distancia (u)", "distanciaAvanzada", 0, 60, () => DistanciaDeAvanzadas);
            refrescos.Insert(0, () => dDistAv.lowValue = (float)MinimoDeAvanzadaEnMesa);
            Texto(panel, "Escala de lo fijo (compartida con la previa)", 13, true);
            Deslizador("Lo fijo", "fijos", 0.5f, 3, () => CosasDelMapa.FactorDeFijos);
            // (sin el de las plazas: van a su tamaño fijo y no se les aplica; en la previa sigue)
            Deslizador("  pueblos y aldeas", "pueblosF", 0.5f, 3, () => CosasDelMapa.FactorPueblos);
            Deslizador("  avanzadas", "avanzadasF", 0.5f, 3, () => CosasDelMapa.FactorAvanzadas);
            Texto(panel, "En el suelo", 13, true);
            // (en dos columnas)
            var rejilla = new VisualElement();
            rejilla.style.flexDirection = FlexDirection.Row;
            var cols = new VisualElement[2];
            for (int c = 0; c < 2; c++)
            {
                cols[c] = new VisualElement();
                cols[c].style.flexGrow = 1;
                cols[c].style.flexBasis = 0;
                rejilla.Add(cols[c]);
            }
            int i0 = 0;
            foreach (var (k, nombre) in new[] { ("huella", "huellas"), ("zona", "zona"), ("aldeas", "anillos de aldea"), ("separacion", "separación"),
                         ("avanzada", "mín. de avanzada"), ("puerta", "puerta"), ("orilla", "orilla del río"), ("vecina", "entre plazas"),
                         ("tamanos", "tamaños"), ("nodo", "nodo") })
                Casilla(cols[i0++ % 2], nombre, () => Anillos[k], v => Anillos[k] = v, true);
            Casilla(cols[i0 % 2], "territorio", () => Zonas, v => { Zonas = v; Mapa.Zonas = v; Mapa.RehacerRayas(); }, true);
            panel.Add(rejilla);
            var fila = new VisualElement();
            fila.style.flexDirection = FlexDirection.Row;
            fila.style.marginTop = 6;
            Button Pequeno(Button b)
            {
                Claro(b);
                b.style.fontSize = 11;
                b.style.paddingLeft = b.style.paddingRight = 6;
                b.style.marginLeft = 0;
                b.style.marginRight = 4;
                fila.Add(b);
                return b;
            }
            Pequeno(new Button(() => EnPlanta(!enPlanta)) { text = "Vista en planta / del juego" });
            Pequeno(new Button(() => { GUIUtility.systemCopyBuffer = Valores(); Debug.Log("[Distancias de plaza] copiado:\n" + Valores()); }) { text = "Copiar valores" });
            Pequeno(new Button(Restablecer) { text = "Restablecer" });
            panel.Add(fila);
            // nada se encoge: si no cupiera, se saldría por abajo (y lo dice la prueba), no se pisaría
            foreach (var h in panel.Children()) h.style.flexShrink = 0;
            RefrescarPanel = () => { foreach (var r in refrescos) r(); };
            raiz.Add(panel);

            // la lectura de la maqueta, abajo en medio, fuera de los paneles
            cajaLectura = Caja(true);
            cajaLectura.pickingMode = PickingMode.Ignore;
            cajaLectura.style.top = StyleKeyword.Auto;
            cajaLectura.style.bottom = 16;
            cajaLectura.style.left = 16 + 440 + 16;
            cajaLectura.style.right = 16 + AnchoDerecha + 16;
            cajaLectura.style.width = StyleKeyword.Auto;
            cajaLectura.style.paddingTop = cajaLectura.style.paddingBottom = 6;
            Texto(cajaLectura, $"Mundo {Banco.Hash} · {Banco.OrigenDelMundo}. Reglas del motor: {R.DelMotor} de sus datos, {R.Constantes} constantes.", 11);
            lectura = Texto(cajaLectura, "", 11);
            raiz.Add(cajaLectura);

            // a la izquierda, una columna: arriba qué se pisa (es lo que se limita si crece) y debajo, siempre entero, los valores del motor
            panelPisa = new VisualElement { pickingMode = PickingMode.Ignore };
            panelPisa.style.position = Position.Absolute;
            panelPisa.style.left = 16;
            panelPisa.style.top = 16;
            panelPisa.style.bottom = 16;
            panelPisa.style.width = 440;
            var pisa = Caja(false);
            pisa.style.position = Position.Relative;
            pisa.style.left = pisa.style.top = StyleKeyword.Auto;
            pisa.style.maxHeight = StyleKeyword.None;
            pisa.style.flexShrink = 1;
            pisa.style.flexGrow = 0;
            pisa.style.minHeight = new Length(34, LengthUnit.Percent);
            pisa.style.overflow = Overflow.Hidden;
            var t2 = Texto(pisa, "Qué se pisa", 18, true);
            t2.style.color = new Color(0.95f, 0.88f, 0.7f);
            Plegable(pisa, t2);
            resumen = Texto(pisa, "", 13, true);
            Texto(pisa, "Holgura: distancia entre centros menos las huellas (con los campos de las aldeas; entre paréntesis, solo los caseríos).", 10);
            var lista = new ScrollView();
            lista.style.flexShrink = 1;
            filasPisa = new VisualElement();
            lista.Add(filasPisa);
            libres = new Foldout { text = "ver todo (lo libre)", value = false };
            if (libres.Q<Label>() is Label ll) ll.style.color = claro;
            lista.Add(libres);
            pisa.Add(lista);
            panelPisa.Add(pisa);
            var motor = Caja(false);
            motor.style.position = Position.Relative;
            motor.style.left = motor.style.top = StyleKeyword.Auto;
            motor.style.maxHeight = StyleKeyword.None;
            motor.style.marginTop = 8;
            motor.style.flexShrink = 1;
            motor.style.flexGrow = 1;
            motor.style.overflow = Overflow.Hidden;
            var t3 = Texto(motor, "Valores del motor", 16, true);
            t3.style.color = new Color(0.95f, 0.88f, 0.7f);
            Plegable(motor, t3);
            cambiadosMotor = Texto(motor, "", 12);
            Texto(motor, "Cambios solo en esta escena: el motor no se toca. * regla nueva (no está en el motor).", 11);
            var desplazable = new ScrollView();
            desplazable.style.flexShrink = 1;
            motor.Add(desplazable);
            ValoresEditables(desplazable.contentContainer, claro);
            var pie = new VisualElement();
            pie.style.flexDirection = FlexDirection.Row;
            pie.style.marginTop = 4;
            pie.Add(Claro(new Button(VolverAlMotor) { text = "Volver a los del motor" }));
            pie.Add(Claro(new Button(CargarPropuesta) { text = "Cargar propuesta" }));
            motor.Add(pie);
            Texto(motor, $"De los datos que el motor manda al cliente (caché de la última partida, mundo {Banco.Hash}): {R.DelMotor} de {R.DelMotor + R.Constantes} cifras"
                         + (R.Constantes > 0 ? $"; {R.Constantes} son constantes del documento." : "."), 10).style.marginTop = 2;
            panelPisa.Add(motor);
            raiz.Add(panelPisa);
        }

        void Tabla()
        {
            int maquetas = Holguras.Count(h => h.pisa == Pisa.Maquetas), campos = Holguras.Count(h => h.pisa == Pisa.Campos);
            resumen.text = maquetas + campos == 0 ? $"Nada se pisa ({Holguras.Count} pares)."
                : $"Se pisan {maquetas + campos} de {Holguras.Count} pares: {maquetas} de maquetas y {campos} de campos.";
            resumen.style.color = maquetas > 0 ? (Color)new Color32(0xff, 0x6b, 0x5b, 255) : campos > 0 ? (Color)new Color32(0xf0, 0xa0, 0x20, 255) : new Color(0.6f, 0.85f, 0.5f);
            filasPisa.Clear();
            libres.contentContainer.Clear();
            foreach (var h in Holguras.Where(h => h.pisa != Pisa.Libre).OrderByDescending(h => h.pisa).ThenBy(h => h.holgura))
            {
                var l = new Label(Fila(h));
                l.style.fontSize = 12;
                l.style.whiteSpace = WhiteSpace.Normal;
                l.style.color = h.pisa == Pisa.Maquetas ? (Color)new Color32(0xff, 0x6b, 0x5b, 255) : (Color)new Color32(0xf0, 0xa0, 0x20, 255);
                filasPisa.Add(l);
            }
            foreach (var h in Holguras.Where(h => h.pisa == Pisa.Libre).OrderBy(h => h.holgura))
            {
                var l = new Label(Fila(h));
                l.style.fontSize = 11;
                l.style.whiteSpace = WhiteSpace.Normal;
                l.style.color = new Color(0.75f, 0.85f, 0.7f);
                libres.contentContainer.Add(l);
            }
            libres.text = $"ver todo: {Holguras.Count(h => h.pisa == Pisa.Libre)} libres";
        }

        void Update()
        {
            if (!Lista || Mapa == null) return;
            // H: pliega o abre las tres cajas a la vez (no mientras se escribe en un campo)
            if (Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame
                && !(panel?.panel?.focusController?.focusedElement is VisualElement fe && (fe is TextField || fe.GetFirstAncestorOfType<TextField>() != null)))
                Plegar(todasPlegadas = !todasPlegadas);
            if (cambiado) { cambiado = false; Aplicar(); }
            if (esperaMedir > 0 && --esperaMedir == 0)
            {
                Medir();
                Rayas();
                Tabla();
                if (!enPlanta) Encuadrar();
            }
            var teclado = Keyboard.current;
            var cam = Mapa.Vista;
            if (!enPlanta && comprobarEncuadre > 0 && Time.unscaledTime >= comprobarEncuadre && cam != null)
            {
                comprobarEncuadre = 0;
                if (!Entra())
                {
                    if (Mapa.Camara.Zoom == ZoomDelMapa.Normal) { Mapa.Camara.Zoom = ZoomDelMapa.Alejado; comprobarEncuadre = Time.unscaledTime + 1.5f; }
                    else NoCabeEnElJuego = true;
                }
            }
            if (teclado != null && cam != null)
            {
                Vector2 v = Vector2.zero;
                if (teclado.wKey.isPressed || teclado.upArrowKey.isPressed) v.y += 1;
                if (teclado.sKey.isPressed || teclado.downArrowKey.isPressed) v.y -= 1;
                if (teclado.dKey.isPressed || teclado.rightArrowKey.isPressed) v.x += 1;
                if (teclado.aKey.isPressed || teclado.leftArrowKey.isPressed) v.x -= 1;
                if (v != Vector2.zero)
                {
                    var adelante = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
                    var derecha = Vector3.ProjectOnPlane(cam.transform.right, Vector3.up).normalized;
                    var d = (adelante * v.y + derecha * v.x).normalized;
                    float alto = Mathf.Max(1, cam.transform.position.y / EscalaDelMapa.MetrosPorUnidad);
                    double paso = Time.unscaledDeltaTime * Mathf.Clamp(alto * 0.8f, 4, 60);
                    mira = (mira.x + d.x * paso, mira.y - d.z * paso);
                    almacen.Buscar(TipoEntidad.Ejercito, "ej-banco").Ejercito.Posicion = BancoDelMapa.Pu(mira.x, mira.y);
                }
            }
            if (lectura == null) return;
            if (cambiadosMotor != null)
            {
                int n = Cambiados().Count;
                cambiadosMotor.text = n == 0 ? "Todos, los del motor." : $"{n} cambiado{(n == 1 ? "" : "s")} respecto al motor.";
                cambiadosMotor.style.color = n == 0 ? new Color(0.6f, 0.85f, 0.5f) : new Color(0.55f, 0.85f, 1f);
            }
            lectura.text = $"Maqueta de la plaza: {Huella("dp-plaza"):0.00} u de radio{(Muralla && Nivel >= 2 ? " (con muralla)" : "")} · zona {Zona:0} u · cupo de pueblos {R.Cupo[Nivel - 1]:0} · avanzadas hasta {CupoDeAvanzadas(Nivel)}"
                         + (enPlanta ? "" : $"\nVista del juego, zoom {(Mapa.Camara.Zoom == ZoomDelMapa.Normal ? "habitual" : Mapa.Camara.Zoom == ZoomDelMapa.Alejado ? "alejado" : "cercano")}"
                            + (NoCabeEnElJuego ? $": ni con el más alejado entran la plaza y su orilla ({EncuadreNecesario:0.0} u)." : "."));
            // las etiquetas sobre las maquetas: proyectadas, centradas y separadas en pantalla (entre sí y de los rótulos de los anillos)
            while (etiquetasSobre.Count < Sobre.Count)
            {
                var l = new Label { pickingMode = PickingMode.Ignore };
                l.style.position = Position.Absolute;
                l.style.fontSize = 12;
                l.style.unityTextAlign = TextAnchor.MiddleCenter;
                l.style.color = new Color(1f, 0.97f, 0.88f);
                l.style.backgroundColor = new Color(0.1f, 0.08f, 0.06f, 0.85f);
                l.style.paddingLeft = l.style.paddingRight = 5;
                l.style.borderTopLeftRadius = l.style.borderTopRightRadius = l.style.borderBottomLeftRadius = l.style.borderBottomRightRadius = 3;
                capaRotulos.Add(l);
                etiquetasSobre.Add(l);
            }
            float W = capaRotulos.layout.width, H = capaRotulos.layout.height;
            var ocupado = new List<Rect>();
            for (int i = 0; i < rotulos.Count && i < etiquetas.Count; i++)
                if (etiquetas[i].resolvedStyle.display == DisplayStyle.Flex)
                    ocupado.Add(new Rect(etiquetas[i].resolvedStyle.left, etiquetas[i].resolvedStyle.top, etiquetas[i].resolvedStyle.width, etiquetas[i].resolvedStyle.height));
            for (int i = 0; i < etiquetasSobre.Count; i++)
            {
                var l = etiquetasSobre[i];
                if (i >= Sobre.Count || cam == null || !Anillos["tamanos"]) { l.style.display = DisplayStyle.None; continue; }
                var (_, nombre, tamano, x, y, alto) = Sobre[i];
                var p = cam.WorldToViewportPoint(EscalaDelMapa.AEscena(x, y, Mapa.AlturaVisible(x, y) + alto));
                bool ve = p.z > 0 && p.x > 0 && p.x < 1 && p.y > 0 && p.y < 1;
                l.style.display = ve ? DisplayStyle.Flex : DisplayStyle.None;
                if (!ve) continue;
                l.text = $"<b>{nombre}</b>\n{tamano}";
                float w = Mathf.Max(l.resolvedStyle.width, 60), h = Mathf.Max(l.resolvedStyle.height, 30);
                var r = new Rect(p.x * W - w / 2, (1 - p.y) * H - h, w, h);
                // (hacia arriba hasta que no pise nada de lo ya puesto)
                for (int intento = 0; intento < 40 && ocupado.Any(o => o.Overlaps(r)); intento++) r.y -= 6;
                ocupado.Add(r);
                l.style.left = r.x;
                l.style.top = r.y;
            }
            // los rótulos de los anillos
            while (etiquetas.Count < rotulos.Count)
            {
                var l = new Label { pickingMode = PickingMode.Ignore };
                l.style.position = Position.Absolute;
                l.style.fontSize = 12;
                l.style.paddingLeft = l.style.paddingRight = 4;
                l.style.borderTopLeftRadius = l.style.borderTopRightRadius = l.style.borderBottomLeftRadius = l.style.borderBottomRightRadius = 3;
                capaRotulos.Add(l);
                etiquetas.Add(l);
            }
            for (int i = 0; i < etiquetas.Count; i++)
            {
                var l = etiquetas[i];
                if (i >= rotulos.Count || cam == null) { l.style.display = DisplayStyle.None; continue; }
                var (texto, x, y, color) = rotulos[i];
                var p = cam.WorldToViewportPoint(EscalaDelMapa.AEscena(x, y, Mapa.AlturaVisible(x, y) + 0.3f * EscalaDelMapa.MetrosPorUnidad));
                bool ve = p.z > 0 && p.x > 0 && p.x < 1 && p.y > 0 && p.y < 1;
                l.style.display = ve ? DisplayStyle.Flex : DisplayStyle.None;
                if (!ve) continue;
                l.text = texto;
                var c = new Color32((byte)(color >> 16), (byte)(color >> 8), (byte)color, 255);
                l.style.color = (Color)c;
                l.style.backgroundColor = new Color(0.08f, 0.07f, 0.05f, 0.8f);
                l.style.left = p.x * capaRotulos.layout.width;
                l.style.top = (1 - p.y) * capaRotulos.layout.height;
            }
        }
    }
}
