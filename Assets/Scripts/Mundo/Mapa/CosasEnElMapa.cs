// Lo que hay en el mapa, pintado con cuadros (tanda 2B): cada cosa es una caja de su color y su tamaño (instanciada: una llamada por cada mil),
// las rayas (caminos, fronteras, zonas, anillos y Miradas) son cintas pegadas al relieve, lo seleccionado lleva un aro, y encima van los nombres
// (UI Toolkit, colocados cada fotograma). Lo que solo se recuerda va bajo la niebla; lo que se ve ahora, encima.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace ConquestTactics.Mundo.Mapa
{
    public sealed class CosasEnElMapa
    {
        const int PorLlamada = 1023;
        const float SobreElSuelo = 0.15f, AnchoAro = 0.07f, HolguraAro = 0.12f;

        readonly Func<double, double, float> alturaVisible;
        readonly Material bajoNiebla, encima;
        readonly Transform padre;
        readonly Mesh caja;
        readonly List<Matrix4x4>[] matrices = { new List<Matrix4x4>(), new List<Matrix4x4>() };
        readonly List<Vector4>[] colores = { new List<Vector4>(), new List<Vector4>() };
        readonly MaterialPropertyBlock propiedades = new MaterialPropertyBlock();
        readonly MaterialPropertyBlock propiedadesFiguras = new MaterialPropertyBlock();
        readonly Matrix4x4[] tanda = new Matrix4x4[PorLlamada];
        readonly Vector4[] tandaColores = new Vector4[PorLlamada];
        GameObject rayasBajo, rayasEncima, aro;
        VisualElement capaNombres;

        /// <summary>(Las escenas de banco: la previa, la mesa) la capa de interfaz en que van las etiquetas, si se monta después del mapa.</summary>
        public void PonerCapaDeNombres(VisualElement capa)
        {
            if (capa == capaNombres) return;
            foreach (var l in nombres) l.RemoveFromHierarchy();
            nombres.Clear();
            capaNombres = capa;
        }
        /// <summary>Cada nombre: una caja (la etiqueta oscura con el borde del color) con el icono y el texto; un Label con hijos no se mide por su texto.</summary>
        readonly List<VisualElement> nombres = new List<VisualElement>();
        /// <summary>Las caravanas, con su carro 3D (`CarroDeCaravana`) en vez de caja, por id; y las que se han visto en este fotograma.</summary>
        readonly Dictionary<string, CarroDeCaravana> carros = new Dictionary<string, CarroDeCaravana>();
        readonly HashSet<string> carrosVistos = new HashSet<string>();

        public CosasEnElMapa(Func<double, double, float> alturaVisible, Material bajoNiebla, Material encima, Transform padre, VisualElement capaNombres)
        {
            this.alturaVisible = alturaVisible;
            this.bajoNiebla = bajoNiebla;
            this.encima = encima;
            this.padre = padre;
            this.capaNombres = capaNombres;
            bajoNiebla.enableInstancing = true;
            encima.enableInstancing = true;
            var cubo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            caja = cubo.GetComponent<MeshFilter>().sharedMesh;
            FigurasDelMapa.Cubo = caja;
            UnityEngine.Object.Destroy(cubo);
        }

        public static Vector4 Lineal(uint rgb) => (Vector4)new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255).ToColor().linear;

        /// <summary>Pinta las cajas de este fotograma: las que quedan a menos de `alcance` u de (cx, cy).</summary>
        /// <summary>Lo que tardaron en el último fotograma las figuras (ponerlas) y los modelos (mandarlos a pintar), en ms.</summary>
        public static double MsFiguras, MsModelos;
        static readonly System.Diagnostics.Stopwatch reloj = new System.Diagnostics.Stopwatch();

        public void Pintar(List<CosaDelMapa> cosas, double cx, double cy, double alcance)
        {
            MsFiguras = 0;
            var e = EscalaDelMapa.MetrosPorUnidad;
            for (int k = 0; k < 2; k++) { matrices[k].Clear(); colores[k].Clear(); }
            foreach (var c in cosas)
            {
                if (CosasDelMapa.Distancia(c.X, c.Y, cx, cy) > alcance) continue;
                // los nodos de recursos los pinta `NodosDelMapa` salvo en el estilo de antes (las cajas)
                if (c.Clase == ClaseDeCosa.Nodo && NodosDelMapa.Estilo != EstiloDeNodos.Cajas) continue;
                // los asentamientos, campamentos, alijos y batallas los pinta `MaquetasDelMapa` salvo con las cajas de antes
                if (MaquetasDelMapa.Pinta(c.Clase)) continue;
                int k = c.BajoNiebla ? 0 : 1;
                // con figuras (tema 7): columnas, caravanas sueltas y Aedas, por figuras; las caravanas enganchadas van en su columna
                if (c.EnColumna) continue;
                if (c.Figuras != null)
                {
                    reloj.Restart();
                    FigurasDelMapa.Anadir(c, alturaVisible, matrices[k], colores[k]);
                    MsFiguras += reloj.Elapsed.TotalMilliseconds;
                    continue;
                }
                var suelo = alturaVisible(c.X, c.Y);
                if ((c.Clase == ClaseDeCosa.Caravana || c.Clase == ClaseDeCosa.CaravanaAjena) && CarroDeCaravana.Disponible)
                {
                    if (!carros.TryGetValue(c.Id, out var carro)) carros[c.Id] = carro = new CarroDeCaravana(padre, "Caravana " + c.Id);
                    carro.Situar(EscalaDelMapa.AEscena(c.X, c.Y, suelo), Time.deltaTime);
                    carrosVistos.Add(c.Id);
                    continue;
                }
                var tamano = new Vector3((float)(c.Radio * 2), (float)c.Alto, (float)(c.Radio * 2)) * e;
                matrices[k].Add(Matrix4x4.TRS(EscalaDelMapa.AEscena(c.X, c.Y, suelo + tamano.y / 2), Quaternion.identity, tamano));
                colores[k].Add(Lineal(c.Color));
            }
            reloj.Restart();
            FigurasDelMapa.Pintar(encima, propiedadesFiguras, PinturaDelMapa.SombrasReales);
            MsFiguras += reloj.Elapsed.TotalMilliseconds;
            reloj.Restart();
            ModelosDeFiguras.Pintar();
            MsModelos = reloj.Elapsed.TotalMilliseconds;
            // Las que ya no están (o quedan lejos), fuera.
            foreach (var id in new List<string>(carros.Keys))
                if (!carrosVistos.Contains(id)) { carros[id].Soltar(); carros.Remove(id); }
            carrosVistos.Clear();
            for (int k = 0; k < 2; k++)
            {
                var rp = new RenderParams(k == 0 ? bajoNiebla : encima)
                {
                    shadowCastingMode = PinturaDelMapa.SombrasReales ? ShadowCastingMode.On : ShadowCastingMode.Off, matProps = propiedades,
                };
                for (int i = 0; i < matrices[k].Count; i += PorLlamada)
                {
                    int n = Mathf.Min(PorLlamada, matrices[k].Count - i);
                    matrices[k].CopyTo(i, tanda, 0, n);
                    colores[k].CopyTo(i, tandaColores, 0, n);
                    propiedades.SetVectorArray("_Color", tandaColores);
                    Graphics.RenderMeshInstanced(rp, caja, 0, tanda, n);
                }
            }
        }

        /// <summary>Rehace las rayas (cuando cambia lo que llega de la sesión).</summary>
        public void Rayas(List<RayaDelMapa> rayas)
        {
            Soltar(ref rayasBajo);
            Soltar(ref rayasEncima);
            rayasBajo = Cintas("Rayas bajo la niebla", rayas.FindAll(r => r.BajoNiebla), bajoNiebla);
            rayasEncima = Cintas("Rayas encima", rayas.FindAll(r => !r.BajoNiebla), encima);
        }

        /// <summary>El aro de lo seleccionado (null lo quita): lo que ocupa más un poco, como en BronzeAgeClient.</summary>
        public void Seleccion(CosaDelMapa c)
        {
            seleccionada = c?.Id;
            Soltar(ref aro);
            if (c == null) return;
            var radio = Math.Max(c.Radio, c.RadioClic - HolguraAro) + HolguraAro;
            aro = Cintas("Lo seleccionado", new List<RayaDelMapa> { CosasDelMapa.Circulo((c.X, c.Y), radio, AnchoAro, 0xf4ead2) }, encima);
        }

        GameObject Cintas(string nombre, List<RayaDelMapa> rayas, Material material)
        {
            if (rayas.Count == 0) return null;
            var e = EscalaDelMapa.MetrosPorUnidad;
            var v = new List<Vector3>();
            var c = new List<Color>();
            var t = new List<int>();
            foreach (var r in rayas)
            {
                var puntos = Densificar(r.Puntos, r.Cerrada, 1.5);
                var color = ((Color)Lineal(r.Color));
                int inicio = v.Count;
                for (int i = 0; i < puntos.Count; i++)
                {
                    var a = puntos[Math.Max(0, i - 1)];
                    var b = puntos[Math.Min(puntos.Count - 1, i + 1)];
                    double dx = b.x - a.x, dy = b.y - a.y, l = Math.Sqrt(dx * dx + dy * dy);
                    if (l < 1e-9) { dx = 1; dy = 0; l = 1; }
                    double nx = -dy / l * r.Ancho / 2, ny = dx / l * r.Ancho / 2;
                    var (px, py) = puntos[i];
                    var h = alturaVisible(px, py) + SobreElSuelo * e;
                    v.Add(EscalaDelMapa.AEscena(px + nx, py + ny, h));
                    v.Add(EscalaDelMapa.AEscena(px - nx, py - ny, h));
                    c.Add(color);
                    c.Add(color);
                    if (i == 0) continue;
                    int q = inicio + (i - 1) * 2;
                    // Con z = −y el mapa se da la vuelta: este orden es el que mira hacia arriba (hacia la cámara).
                    t.AddRange(new[] { q, q + 1, q + 2, q + 1, q + 3, q + 2 });
                }
            }
            var malla = new Mesh { name = nombre, indexFormat = IndexFormat.UInt32 };
            malla.SetVertices(v);
            malla.SetColors(c);
            malla.SetTriangles(t, 0);
            malla.RecalculateNormals();
            var go = new GameObject(nombre);
            go.transform.SetParent(padre, false);
            go.AddComponent<MeshFilter>().sharedMesh = malla;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        /// <summary>Los puntos con uno cada `paso` u como mucho, para que la cinta siga el relieve.</summary>
        static List<(double x, double y)> Densificar(List<(double x, double y)> p, bool cerrada, double paso)
        {
            var r = new List<(double, double)>();
            int n = p.Count + (cerrada ? 1 : 0);
            for (int i = 0; i + 1 < n; i++)
            {
                var a = p[i];
                var b = p[(i + 1) % p.Count];
                int pasos = Math.Max(1, (int)Math.Ceiling(CosasDelMapa.Distancia(a.x, a.y, b.x, b.y) / paso));
                for (int k = 0; k < pasos; k++) r.Add((a.x + (b.x - a.x) * k / pasos, a.y + (b.y - a.y) * k / pasos));
            }
            r.Add(cerrada ? p[0] : p[p.Count - 1]);
            return r;
        }

        /// <summary>Los nombres sobre el mapa, colocados en pantalla este fotograma: los de las cosas con nombre que quedan a menos de `lejos` u
        /// de (cx, cy) y delante de la cámara; los de los nodos, solo si `nodos` y a menos de `cercaNodos`.</summary>
        /// <summary>El sigilo de una Facción (`faccionId`, ancho en px) como textura; lo pone la interfaz del mundo, que tiene la sesión. Null: sin sigilo.</summary>
        public static Func<string, int, Texture2D> SigiloDe;

        /// <summary>Lo que sube cada etiqueta sobre lo más alto de su modelo (u): un margen corto y constante.</summary>
        public const double MargenSobreElModelo = 0.12;

        /// <summary>Las etiquetas pulsables de este fotograma, con su caja en la capa: un clic en una es un clic en su cosa.</summary>
        readonly List<(VisualElement caja, CosaDelMapa cosa)> pulsables = new List<(VisualElement, CosaDelMapa)>();
        /// <summary>(Para las pruebas) la capa de las etiquetas.</summary>
        public VisualElement CapaDeNombres => capaNombres;
        /// <summary>(Para las pruebas) las etiquetas puestas este fotograma y dónde (en la escena).</summary>
        public readonly List<(EtiquetaDelMapa etiqueta, Vector3 mundo)> Puestas = new List<(EtiquetaDelMapa, Vector3)>();
        /// <summary>(Para las pruebas) las etiquetas que se ven este fotograma (las que no escondió otra de más prioridad), por su id.</summary>
        public readonly HashSet<string> Vistas = new HashSet<string>();
        /// <summary>El id de lo seleccionado (su etiqueta siempre se ve).</summary>
        string seleccionada;
        /// <summary>Lo que midió cada etiqueta (por su id) la última vez que se pintó, en px de la capa; y qué id lleva cada caja ahora.</summary>
        readonly Dictionary<string, Vector2> tamanos = new Dictionary<string, Vector2>();
        readonly List<string> idsDeCajas = new List<string>();
        /// <summary>La placa de las etiquetas (tema 10, punto 8): la 1, «bronce batido», la elegida (25-11); 0, la de antes; 2 y 3, las otras
        /// de bronce (se comparan en la mesa y la previa).</summary>
        public static int Placa = 1;
        /// <summary>Dónde está el ratón (px de la pantalla, el origen abajo); null, fuera. La pulsable que tiene debajo se marca («--encima»).</summary>
        public Vector2? Raton;
        /// <summary>Histéresis (px): la que se veía solo se esconde si la pisan más de esto; la escondida solo vuelve con esto de hueco.</summary>
        public const float HolguraDeSolape = 4;

        /// <summary>¿Lleva el escudo que se sale del cartel? Con la placa «bronce batido» (1) y blasón.</summary>
        static bool Desborda(EtiquetaDelMapa et) => Placa == 1 && et.FaccionSigilo != null && SigiloDe != null;

        /// <summary>El alto (px) del escudo de columnas, ejércitos y caravanas: uno solo, como el prototipo (`CARTEL`, 26 px), sea el cartel de
        /// una línea o de dos; el de la columna de dos líneas (31 px sobre 28 de cartel, aquí 59 sobre 53).</summary>
        public const float AltoDelEscudoDeCartel = 59;

        /// <summary>El escudo que se sale del cartel, como el prototipo (`mundo3d.css`): su ancho y su alto (100:120) y lo que se corre el cartel
        /// para que el conjunto quede centrado. La plaza, por el alto `h` (px) de su cartel: 48 px de escudo frente a 33 (vez y media); lo
        /// demás, `AltoDelEscudoDeCartel`. El cartel se corre 14 de 40 (plaza) o 10 de 26.</summary>
        public static (float ancho, float alto, float corre) Escudo(EtiquetaDelMapa et, float h)
        {
            bool plaza = et.AnchoSigilo >= 40;
            float alto = plaza ? h * 48f / 33 : AltoDelEscudoDeCartel, ancho = alto / 1.2f;
            return (ancho, alto, ancho * (plaza ? 14f / 40 : 10f / 26));
        }

        /// <summary>El punto (en la capa) cae en la etiqueta o en su escudo, que se sale de ella.</summary>
        static bool Contiene(VisualElement l, Vector2 p) =>
            l.worldBound.Contains(p) || l.ClassListContains("nombre-mapa--blason") && l.Q("icono") is VisualElement i && i.worldBound.Contains(p);

        /// <summary>Cuáles se ven: por orden (la prioridad, la más cerca de la cámara), la que pisa a una ya puesta se esconde; las de siempre
        /// no se esconden nunca (y esconden a las de menos prioridad que pisan, no a las de más). Con histéresis: la que se veía (`antes`) tiene `HolguraDeSolape` px de gracia y la escondida
        /// necesita ese hueco de más para volver, para que no parpadeen al girar la cámara.</summary>
        public static HashSet<string> QueSeVen(IEnumerable<(string id, Rect caja, int prioridad, bool siempre, float lejania)> cajas, ICollection<string> antes)
        {
            var r = new HashSet<string>();
            var puestas = new List<Rect>();
            foreach (var k in cajas.OrderBy(k => k.prioridad).ThenBy(k => k.lejania))
            {
                float h = antes.Contains(k.id) ? -HolguraDeSolape : HolguraDeSolape;
                var prueba = new Rect(k.caja.x - h, k.caja.y - h, k.caja.width + 2 * h, k.caja.height + 2 * h);
                if (!k.siempre && puestas.Any(p => p.Overlaps(prueba))) continue;
                puestas.Add(k.caja);
                r.Add(k.id);
            }
            return r;
        }

        /// <summary>El orden en que se quedan cuando se pisan (tema 10, punto 9): plaza, campamento (y Mirada), columna o ejército, caravana,
        /// Aeda, nodo. Los nodos de la zona que se funda, por delante.</summary>
        public static int Prioridad(EtiquetaDelMapa et, bool enFundar) => enFundar ? -1 : et.Tipo switch
        {
            "plaza" => 0, "mercenarios" or "bandidos" or "mirada" => 1, "columna" or "ejercito" => 2, "caravana" => 3, "aeda" => 4, _ => 5,
        };

        /// <summary>Coloca las etiquetas de `etiquetas` (`CosasDelMapa.Etiquetas`) que quedan a menos de `lejos` u de (cx, cy) y delante de la
        /// cámara; las de los nodos, solo si `nodos`, a menos de `cercaNodos` y donde ya se exploró (`explorado`), o todas las de dentro de la
        /// zona de una plaza que se está fundando (`fundar`: centro y radio, u), resaltadas. Cada una va sobre lo más alto de su modelo (`cima`: la
        /// altura en la escena, o null si no tiene modelo), sobre el estandarte o las cabezas de sus figuras, o a su alto fijo.</summary>
        public void Nombres(List<EtiquetaDelMapa> etiquetas, Camera camara, double cx, double cy, double lejos, bool nodos, double cercaNodos,
            Func<double, double, bool> explorado, (double x, double y, double r)? fundar, Func<CosaDelMapa, float?> cima)
        {
            pulsables.Clear();
            Puestas.Clear();
            if (capaNombres?.panel == null) return;
            var e = EscalaDelMapa.MetrosPorUnidad;
            // lo que midió cada una el fotograma anterior (su caja ya tiene el tamaño de su texto)
            for (int i = 0; i < idsDeCajas.Count && i < nombres.Count; i++)
                if (idsDeCajas[i] != null && nombres[i].layout.width > 0) tamanos[idsDeCajas[i]] = nombres[i].layout.size;
            idsDeCajas.Clear();
            var candidatas = new List<(EtiquetaDelMapa et, Vector2 enPanel, bool enFundar, int prioridad, bool siempre, float lejania)>();
            foreach (var et in etiquetas)
            {
                var c = et.Cosa;
                bool nodo = et.Tipo == "nodo";
                var d = CosasDelMapa.Distancia(et.X, et.Y, cx, cy);
                bool enFundar = nodo && fundar is (double fx, double fy, double fr) && CosasDelMapa.Distancia(et.X, et.Y, fx, fy) <= fr;
                if (nodo && !enFundar && (!nodos || d > cercaNodos || explorado != null && !explorado(et.X, et.Y))) continue;
                if (!nodo && d > lejos) continue;
                // dónde: sobre lo más alto de su modelo; las columnas, sobre el estandarte o las cabezas de sus figuras; lo demás, a su alto
                float y;
                if (!double.IsNaN(et.Alto)) y = alturaVisible(et.X, et.Y) + (float)(et.Alto * e);
                else if (c != null && c.Figuras != null && c.Figuras.Count > 0)
                    y = alturaVisible(et.X, et.Y) + (float)((Math.Max(c.Alto, c.Figuras.Exists(f => f.Estandarte) ? FigurasDelMapa.AltoDelEstandarte : 0.27 * ColumnasDelMapa.FactorDeFiguras * 2)
                        + MargenSobreElModelo) * e);
                else if (c != null && cima?.Invoke(c) is float alto) y = alto + (float)(MargenSobreElModelo * e);
                else y = alturaVisible(et.X, et.Y) + (float)(((c?.Alto ?? 0) + 0.25) * e);
                var mundo = EscalaDelMapa.AEscena(et.X, et.Y, 0);
                mundo.y = y;
                Puestas.Add((et, mundo));
                var pantalla = camara.WorldToScreenPoint(mundo);
                if (pantalla.z <= 0 || pantalla.x < 0 || pantalla.y < 0 || pantalla.x > camara.pixelWidth || pantalla.y > camara.pixelHeight) continue;
                // En fracción de la vista de la cámara sobre la capa (que cubre la pantalla): vale también si el panel pinta en una textura de otro tamaño.
                var enPanel = new Vector2(pantalla.x / camara.pixelWidth * capaNombres.layout.width, (1 - pantalla.y / camara.pixelHeight) * capaNombres.layout.height);
                // siempre: la de lo seleccionado, la de tu columna y lo que pisa tu columna
                bool siempre = c != null && c.Id == seleccionada || c != null && c.Propia && (et.Tipo == "columna" || et.Tipo == "ejercito") || et.Tipo == "territorio";
                candidatas.Add((et, enPanel, enFundar, Prioridad(et, enFundar), siempre, pantalla.z));
            }
            // que no se amontonen (su caja, con lo que midió; sin medir aún, una corriente)
            var cajas = candidatas.Select(k =>
            {
                var tam = tamanos.TryGetValue(k.et.Id, out var t) ? t : new Vector2(90, 26);
                if (!Desborda(k.et)) return (k.et.Id, new Rect(k.enPanel.x - tam.x / 2, k.enPanel.y - tam.y, tam.x, tam.y), k.prioridad, k.siempre, k.lejania);
                // (con el escudo que se sale: el cartel, corrido y subido, y el escudo a su izquierda, más alto)
                var (ea, eh, corre) = Escudo(k.et, tam.y);
                float sube = Mathf.Max(0, (eh - tam.y) / 2), x0 = k.enPanel.x + corre - tam.x / 2;
                return (k.et.Id, new Rect(x0 - ea / 2, k.enPanel.y - tam.y - 2 * sube, tam.x + ea / 2, tam.y + 2 * sube), k.prioridad, k.siempre, k.lejania);
            }).ToList();
            var vistas = QueSeVen(cajas, Vistas);
            Vistas.Clear();
            Vistas.UnionWith(vistas);
            // (el ratón, en la capa: con la caja de cada etiqueta del fotograma anterior)
            Vector2? raton = Raton is Vector2 rp && camara.pixelWidth > 0
                ? capaNombres.LocalToWorld(new Vector2(rp.x / camara.pixelWidth * capaNombres.layout.width, (1 - rp.y / camara.pixelHeight) * capaNombres.layout.height))
                : (Vector2?)null;
            int usados = 0;
            foreach (var (et, enPanel, enFundar, _, _, _) in candidatas)
            {
                if (!vistas.Contains(et.Id)) continue;
                var c = et.Cosa;
                bool nodo = et.Tipo == "nodo";
                if (usados == nombres.Count)
                {
                    // la etiqueta: el blasón o el icono a la izquierda y, a su derecha, el nombre con su segunda línea (el subtexto y los avisos)
                    var l = new VisualElement { pickingMode = PickingMode.Ignore };
                    l.AddToClassList("nombre-mapa");
                    var sombra = new VisualElement { pickingMode = PickingMode.Ignore, name = "sombra" };
                    sombra.AddToClassList("nombre-mapa-sombra");
                    l.Add(sombra);
                    var icono = new VisualElement { pickingMode = PickingMode.Ignore, name = "icono" };
                    icono.AddToClassList("nombre-mapa-icono");
                    l.Add(icono);
                    var textos = new VisualElement { pickingMode = PickingMode.Ignore, name = "textos" };
                    textos.AddToClassList("nombre-mapa-textos");
                    textos.Add(new Label { pickingMode = PickingMode.Ignore, name = "texto" });
                    var segunda = new VisualElement { pickingMode = PickingMode.Ignore, name = "segunda" };
                    segunda.AddToClassList("nombre-mapa-segunda");
                    segunda.Add(new Label { pickingMode = PickingMode.Ignore, name = "sub" });
                    textos.Add(segunda);
                    l.Add(textos);
                    capaNombres.Add(l);
                    nombres.Add(l);
                }
                idsDeCajas.Add(et.Id);
                var label = nombres[usados++];
                label.Q<Label>("texto").text = et.Texto;
                var segundaLinea = label.Q("segunda");
                var sub = segundaLinea.Q<Label>("sub");
                sub.text = et.Subtexto ?? "";
                sub.style.display = string.IsNullOrEmpty(et.Subtexto) ? DisplayStyle.None : DisplayStyle.Flex;
                // los avisos (icono y palabra), detrás del subtexto: se rehacen solo si cambian
                var claveAvisos = string.Join("|", et.Avisos.Select(a => a.id));
                if ((segundaLinea.userData as string) != claveAvisos)
                {
                    segundaLinea.userData = claveAvisos;
                    while (segundaLinea.childCount > 1) segundaLinea.RemoveAt(1);
                    foreach (var (familia, id, palabra) in et.Avisos)
                    {
                        var aviso = IconosDelMundo.ConIcono(familia, id, new Label(palabra) { pickingMode = PickingMode.Ignore }, 12, IconosDelMundo.OroClaro);
                        aviso.pickingMode = PickingMode.Ignore;
                        aviso.AddToClassList("nombre-mapa-aviso");
                        segundaLinea.Add(aviso);
                    }
                }
                segundaLinea.style.display = string.IsNullOrEmpty(et.Subtexto) && et.Avisos.Count == 0 ? DisplayStyle.None : DisplayStyle.Flex;
                var dibujo = label.Q("icono");
                // Las plazas, las columnas y las caravanas de una Facción llevan su blasón (`mundo3d/estado.ts`: 40 px en las plazas, 26 en lo demás);
                // lo demás, su icono (el suyo o el del mapa 2D de su cosa).
                int ancho = et.AnchoSigilo;
                bool desborda = Desborda(et);
                // (el que se sale, mayor: la textura, a uno de dos tamaños para no hacer una por etiqueta)
                var sigilo = et.FaccionSigilo != null ? SigiloDe?.Invoke(et.FaccionSigilo, desborda ? (ancho >= 40 ? 64 : 48) : ancho) : null;
                desborda &= sigilo != null;
                var sombraDibujo = label.Q("sombra");
                float subeEscudo = 0;
                if (desborda)
                {
                    var tam = tamanos.TryGetValue(et.Id, out var tm) ? tm : new Vector2(90, ancho >= 40 ? 36 : 30);
                    var (ea, eh, corre) = Escudo(et, tam.y);
                    subeEscudo = Mathf.Max(0, (eh - tam.y) / 2);
                    foreach (var v in new[] { dibujo, sombraDibujo })
                    {
                        v.style.width = ea;
                        v.style.height = eh;
                        v.style.left = -ea / 2;
                        v.style.backgroundImage = new StyleBackground(sigilo);
                    }
                    dibujo.style.display = DisplayStyle.Flex;
                    dibujo.style.unityBackgroundImageTintColor = Color.white;
                    label.style.marginLeft = corre;
                    label.style.paddingLeft = ea * 0.76f;
                }
                else if (sigilo != null)
                {
                    dibujo.style.display = DisplayStyle.Flex;
                    dibujo.style.width = ancho;
                    dibujo.style.height = ancho * 1.2f;
                    dibujo.style.backgroundImage = new StyleBackground(sigilo);
                    dibujo.style.unityBackgroundImageTintColor = Color.white;
                }
                else
                {
                    dibujo.style.width = StyleKeyword.Null;
                    dibujo.style.height = StyleKeyword.Null;
                    (string familia, string id, Color color)? cual = et.Icono is (string fam, string ic) ? (fam, ic, IconosDelMundo.OroClaro)
                        : c != null && IconosDelMundo.DeCosa(c) is (string id2, Color col2) ? ("mapa", id2, col2) : ((string, string, Color)?)null;
                    var imagen = cual == null ? null : IconosDelMundo.Imagen(cual.Value.familia, cual.Value.id);
                    dibujo.style.display = imagen != null ? DisplayStyle.Flex : DisplayStyle.None;
                    if (imagen != null)
                    {
                        dibujo.style.backgroundImage = new StyleBackground(imagen);
                        dibujo.style.unityBackgroundImageTintColor = cual.Value.color;
                    }
                }
                label.EnableInClassList("nombre-mapa--nodo", nodo);
                // (con la placa «bronce batido» y blasón, el escudo se sale del cartel por la izquierda, como en el prototipo)
                label.EnableInClassList("nombre-mapa--blason", desborda);
                if (!desborda)
                {
                    dibujo.style.left = StyleKeyword.Null;
                    label.style.marginLeft = StyleKeyword.Null;
                    label.style.paddingLeft = StyleKeyword.Null;
                }
                for (int k = 1; k <= 3; k++) label.EnableInClassList("nombre-mapa--placa" + k, Placa == k);
                label.EnableInClassList("nombre-mapa--encima", et.Pulsable && c != null && raton is Vector2 rr && Contiene(label, rr));
                label.EnableInClassList("nombre-mapa--resaltada", enFundar);
                label.EnableInClassList("nombre-mapa--tenue", et.Tenue || c != null && c.BajoNiebla && !nodo);
                var borde = enFundar ? 0xf1d38bu : et.Color;
                label.style.borderLeftColor = new StyleColor(new Color32((byte)(borde >> 16), (byte)(borde >> 8), (byte)borde, 255));
                label.style.left = enPanel.x;
                label.style.top = enPanel.y - subeEscudo;
                label.style.display = DisplayStyle.Flex;
                if (et.Pulsable && c != null) pulsables.Add((label, c));
            }
            for (int i = usados; i < nombres.Count; i++) nombres[i].style.display = DisplayStyle.None;
        }

        /// <summary>La cosa de la etiqueta pulsable que hay en ese punto de la pantalla (px, con el origen abajo, como `Mouse.position`), o null.
        /// Las etiquetas no recogen el ratón (arrastrar encima sigue moviendo la cámara): el mapa pregunta aquí con cada clic.</summary>
        public CosaDelMapa CosaBajoEtiqueta(Vector2 pantalla, Camera camara)
        {
            if (capaNombres?.panel == null || pulsables.Count == 0) return null;
            var local = new Vector2(pantalla.x / camara.pixelWidth * capaNombres.layout.width, (1 - pantalla.y / camara.pixelHeight) * capaNombres.layout.height);
            var enMundo = capaNombres.LocalToWorld(local);
            // (la de encima: la última que se pintó)
            for (int i = pulsables.Count - 1; i >= 0; i--)
                if (Contiene(pulsables[i].caja, enMundo)) return pulsables[i].cosa;
            return null;
        }

        static void Soltar(ref GameObject go)
        {
            if (go == null) return;
            UnityEngine.Object.Destroy(go.GetComponent<MeshFilter>().sharedMesh);
            UnityEngine.Object.Destroy(go);
            go = null;
        }

        public void Destruir()
        {
            Soltar(ref rayasBajo);
            Soltar(ref rayasEncima);
            Soltar(ref aro);
            foreach (var l in nombres) l.RemoveFromHierarchy();
        }
    }

    static class Color32Extensiones
    {
        public static Color ToColor(this Color32 c) => c;
    }
}
