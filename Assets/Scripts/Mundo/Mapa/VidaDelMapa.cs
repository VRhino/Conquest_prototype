using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace ConquestTactics.Mundo.Mapa
{
    /// <summary>Las bandadas del mapa (tema 9), como `vida.ts` de BronzeAgeClient: una rejilla de 24 u alrededor de lo que se mira (5 × 5
    /// celdas) y una bandada en el 35 % de las celdas, siempre la misma en cada celda: de 3 a 7 pájaros dando vueltas de 4 a 11 u de radio a
    /// 3,5–5 u sobre el suelo. Solo pájaros: ni gente ni rebaños (el mapa es para ver moverse a las tropas). Todo el vuelo va en
    /// `Conquest/MapaAves`, en una llamada; aquí solo se rehace la lista al cambiar de celda.</summary>
    public sealed class AvesDelMapa : IDisposable
    {
        /// <summary>Con las bandadas (las pruebas las apagan para medir sin ellas).</summary>
        public static bool Encendidas = true;
        const double Celda = 24, Parte = 0.35;
        /// <summary>La envergadura de un pájaro (u); la del prototipo, 0,22.</summary>
        public static double Envergadura = 0.22;
        const int Anillo = 2;
        static readonly int Datos = Shader.PropertyToID("_Aves");

        readonly Func<double, double, float> alturaMetros;
        readonly Mesh malla;
        readonly Material material;
        GraphicsBuffer buffer;
        int pajaros;
        (int, int)? clave;
        double envergaduraPuesta;
        /// <summary>Los centros de las bandadas (escena, m) de la última lista (para las pruebas).</summary>
        /// <summary>Lo que va al sombreado, dos float4 por pájaro (para las pruebas: dónde está cada uno a una hora del reloj del mapa).</summary>
        public System.Collections.Generic.List<Vector4> Vuelos = new System.Collections.Generic.List<Vector4>();
        public readonly System.Collections.Generic.List<Vector3> Centros = new System.Collections.Generic.List<Vector3>();

        /// <summary>`alturaMetros`, la del suelo visible en un punto del mapa (u).</summary>
        public AvesDelMapa(Func<double, double, float> alturaMetros)
        {
            this.alturaMetros = alturaMetros;
            malla = Resources.Load<Mesh>("MapaMundo/Vida/Ave");
            material = new Material(Resources.Load<Shader>("MapaMundo/MapaAves")) { name = "aves del mapa" };
        }

        public int Pajaros => Encendidas ? pajaros : 0;

        public void Dibujar(double cx, double cy)
        {
            if (!Encendidas || malla == null) return;
            var k = ((int)Math.Floor(cx / Celda), (int)Math.Floor(cy / Celda));
            if (clave != k || envergaduraPuesta != Envergadura) { clave = k; envergaduraPuesta = Envergadura; Poner(k.Item1, k.Item2); }
            if (pajaros == 0) return;
            var rp = new RenderParams(material) { worldBounds = new Bounds(Vector3.zero, Vector3.one * 1e7f), shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false };
            Graphics.RenderMeshPrimitives(rp, malla, 0, pajaros);
        }

        void Poner(int ci, int cj)
        {
            var e = EscalaDelMapa.MetrosPorUnidad;
            var lista = new System.Collections.Generic.List<Vector4>();
            Centros.Clear();
            for (int j = cj - Anillo; j <= cj + Anillo; j++)
                for (int i = ci - Anillo; i <= ci + Anillo; i++)
                {
                    if (Azar(i, j, 501) >= Parte) continue;
                    double radio = 4 + 7 * Azar(i, j, 503), v = 2 + 1.5 * Azar(i, j, 509);
                    double x = (i + Azar(i, j, 505)) * Celda, y = (j + Azar(i, j, 507)) * Celda;
                    double giro = (Azar(i, j, 511) < 0.5 ? -1 : 1) * v / radio;
                    int n = 3 + (int)Math.Floor(5 * Azar(i, j, 513));
                    int semilla = i * 7919 + j;
                    float suelo = alturaMetros(x, y);
                    Centros.Add(EscalaDelMapa.AEscena(x, y, suelo + 4 * e));
                    for (int p = 0; p < n; p++)
                    {
                        double R(int q) => Azar(semilla, p, q);
                        var centro = EscalaDelMapa.AEscena(x, y, suelo + (3.5 + 1.5 * R(1) + 0.4 * p / n) * e);
                        // en fila por la vuelta, unos detrás de otros, y un poco más dentro o más fuera
                        lista.Add(new Vector4(centro.x, centro.y, centro.z, (float)(radio * (0.9 + 0.2 * R(2)) * e)));
                        lista.Add(new Vector4((float)(giro * (0.97 + 0.06 * R(3))), (float)(-Math.Sign(giro) * p * 0.12 + R(4) * 0.08), (float)(R(5) * 6.28), (float)(Envergadura * e)));
                    }
                }
            pajaros = lista.Count / 2;
            Vuelos = lista;
            buffer?.Release();
            buffer = null;
            if (pajaros == 0) return;
            buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, lista.Count, 16);
            buffer.SetData(lista);
            material.SetBuffer(Datos, buffer);
        }

        /// <summary>Un azar fijo de 0 a 1 por (i, j, k).</summary>
        static double Azar(int i, int j, int k)
        {
            unchecked
            {
                uint h = (uint)i * 374761393u + (uint)j * 668265263u + (uint)k * 2246822519u;
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (double)0x1000000;
            }
        }

        public void Dispose()
        {
            buffer?.Release();
            buffer = null;
            if (material != null) UnityEngine.Object.Destroy(material);
        }
    }

    /// <summary>El polvo ocre a los pies de lo que anda (tema 9): columnas de ejército y caravanas con figuras que se mueven. Más en suelo seco,
    /// casi nada en la hierba, nada en la nieve ni en el agua; bocanadas cortas (1,4 s) y de poca opacidad, bajas para no tapar las figuras. Solo
    /// cerca de lo que se mira y a zoom cercano o habitual. Una llamada: las bocanadas van en un anillo de `Maximo` y el sombreado
    /// (`Conquest/MapaPolvo`) las hace crecer, subir y apagarse.</summary>
    public sealed class PolvoDelMapa : IDisposable
    {
        /// <summary>Con el polvo (las pruebas lo apagan para medir sin él).</summary>
        public static bool Encendido = true;
        public const float Vida = 1.4f;
        const int Maximo = 512;
        /// <summary>Bocanadas por segundo de una columna en suelo seco y cuántas figuras hacen falta para eso.</summary>
        const double PorSegundo = 9, FigurasLlenas = 12;
        static readonly int Datos = Shader.PropertyToID("_Polvo");

        readonly Func<double, double, float> alturaMetros;
        readonly Func<double, double, bool> agua;
        readonly RasterDeMundo fertilidad, humedad;
        readonly float nieveMetros;
        readonly Mesh quad;
        readonly Material material;
        readonly Vector4[] bocanadas = new Vector4[Maximo * 2];
        readonly System.Random azar = new System.Random(7);
        readonly System.Collections.Generic.Dictionary<string, double> pendiente = new System.Collections.Generic.Dictionary<string, double>();
        GraphicsBuffer buffer;
        int siguiente;
        /// <summary>Las bocanadas echadas desde el principio (para las pruebas).</summary>
        public int Echadas;
        bool sucio;

        public PolvoDelMapa(Func<double, double, float> alturaMetros, Func<double, double, bool> agua, RasterDeMundo fertilidad, RasterDeMundo humedad, float nieveMetros)
        {
            this.alturaMetros = alturaMetros;
            this.agua = agua;
            this.fertilidad = fertilidad;
            this.humedad = humedad;
            this.nieveMetros = nieveMetros;
            material = new Material(Resources.Load<Shader>("MapaMundo/MapaPolvo")) { name = "polvo del mapa" };
            quad = new Mesh { name = "bocanada" };
            quad.SetVertices(new[] { new Vector3(-0.5f, 0, 0), new Vector3(0.5f, 0, 0), new Vector3(0.5f, 1, 0), new Vector3(-0.5f, 1, 0) });
            quad.SetUVs(0, new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) });
            quad.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
            quad.bounds = new Bounds(Vector3.zero, Vector3.one * 1e7f);
            for (int i = 0; i < Maximo; i++) bocanadas[2 * i + 1] = new Vector4(-100, 0, 0, 0);
        }

        /// <summary>Cuánto polvo levanta el suelo en (x, y): 1 en lo seco, casi nada en la hierba, 0 en el agua y la nieve.</summary>
        public double Suelo(double x, double y)
        {
            if (agua(x, y) || alturaMetros(x, y) >= nieveMetros) return 0;
            double f = fertilidad != null ? fertilidad.En(x, y) / 255.0 : 0.5, h = humedad != null ? humedad.En(x, y) / 255.0 : 0.5;
            double verde = Math.Clamp((f + h) / 2, 0, 1);
            return 0.06 + 0.94 * (1 - Smooth(0.3, 0.55, verde));
        }

        static double Smooth(double a, double b, double x) { var t = Math.Clamp((x - a) / (b - a), 0, 1); return t * t * (3 - 2 * t); }

        /// <summary>Echa las bocanadas de este fotograma (`dt` s) y dibuja. `cerca`, el radio (u) alrededor de (cx, cy) en el que hay polvo;
        /// `aZoom`, si el zoom lo quiere (cercano o habitual).</summary>
        public void Actualizar(System.Collections.Generic.List<CosaDelMapa> cosas, double cx, double cy, double cerca, bool aZoom, float dt)
        {
            if (!Encendido || !aZoom || cosas == null) return;
            var e = EscalaDelMapa.MetrosPorUnidad;
            float ahora = Time.timeSinceLevelLoad;
            foreach (var c in cosas)
            {
                if (c.Figuras == null || c.Figuras.Count == 0 || c.Velocidad <= 0.01 || c.Camino == null || c.Camino.Count < 2) continue;
                if (c.Clase == ClaseDeCosa.Aeda) continue;
                if ((c.X - cx) * (c.X - cx) + (c.Y - cy) * (c.Y - cy) > cerca * cerca) continue;
                double largo = 0;
                foreach (var f in c.Figuras) largo = Math.Max(largo, f.Atras * ColumnasDelMapa.Tropa); // `Atras`, en unidades de figura
                double tasa = PorSegundo * Math.Min(1, c.Figuras.Count / FigurasLlenas) * dt;
                pendiente.TryGetValue(c.Id ?? "", out var deuda);
                deuda += tasa;
                while (deuda >= 1)
                {
                    deuda -= 1;
                    // dos de cada diez, la estela corta detrás de la cola; las demás, al pie de una figura
                    double atras = azar.NextDouble() < 0.2 ? largo + (0.15 + 0.35 * azar.NextDouble()) * ColumnasDelMapa.FactorDeFiguras
                        : c.Figuras[azar.Next(c.Figuras.Count)].Atras * ColumnasDelMapa.Tropa + 0.15 * ColumnasDelMapa.FactorDeFiguras * (azar.NextDouble() - 0.3);
                    var (x, y) = EnCamino(c.Camino, atras);
                    x += (azar.NextDouble() - 0.5) * 0.35 * ColumnasDelMapa.FactorDeFiguras;
                    y += (azar.NextDouble() - 0.5) * 0.35 * ColumnasDelMapa.FactorDeFiguras;
                    double suelo = Suelo(x, y);
                    if (suelo <= 0 || azar.NextDouble() > suelo) continue;
                    var p = EscalaDelMapa.AEscena(x, y, alturaMetros(x, y));
                    int k = siguiente;
                    siguiente = (siguiente + 1) % Maximo;
                    bocanadas[2 * k] = new Vector4(p.x, p.y, p.z, (float)((0.4 + 0.2 * azar.NextDouble()) * ColumnasDelMapa.FactorDeFiguras * e));
                    bocanadas[2 * k + 1] = new Vector4(ahora, (float)suelo, (float)(azar.NextDouble() * 6.28), 0);
                    sucio = true;
                    Echadas++;
                }
                pendiente[c.Id ?? ""] = deuda;
            }
            if (sucio)
            {
                sucio = false;
                if (buffer == null) { buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Maximo * 2, 16); material.SetBuffer(Datos, buffer); }
                buffer.SetData(bocanadas);
            }
            if (buffer == null) return;
            var rp = new RenderParams(material) { worldBounds = new Bounds(Vector3.zero, Vector3.one * 1e7f), shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false };
            Graphics.RenderMeshPrimitives(rp, quad, 0, Maximo);
        }

        /// <summary>El punto del camino (de la cabeza hacia atrás, u) a `atras` u de la cabeza.</summary>
        static (double x, double y) EnCamino(System.Collections.Generic.List<(double x, double y)> camino, double atras)
        {
            atras = Math.Max(0, atras);
            for (int i = 1; i < camino.Count; i++)
            {
                var (ax, ay) = camino[i - 1];
                var (bx, by) = camino[i];
                double l = Math.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay));
                if (atras <= l && l > 1e-9) return (ax + (bx - ax) * atras / l, ay + (by - ay) * atras / l);
                atras -= l;
            }
            return camino[camino.Count - 1];
        }

        public void Dispose()
        {
            buffer?.Release();
            buffer = null;
            if (material != null) UnityEngine.Object.Destroy(material);
            if (quad != null) UnityEngine.Object.Destroy(quad);
        }
    }
}
