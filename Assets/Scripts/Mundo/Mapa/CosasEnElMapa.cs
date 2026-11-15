// Lo que hay en el mapa, pintado con cuadros (tanda 2B): cada cosa es una caja de su color y su tamaño (instanciada: una llamada por cada mil),
// las rayas (caminos, fronteras, zonas, anillos y Miradas) son cintas pegadas al relieve, lo seleccionado lleva un aro, y encima van los nombres
// (UI Toolkit, colocados cada fotograma). Lo que solo se recuerda va bajo la niebla; lo que se ve ahora, encima.
using System;
using System.Collections.Generic;
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
        readonly Matrix4x4[] tanda = new Matrix4x4[PorLlamada];
        readonly Vector4[] tandaColores = new Vector4[PorLlamada];
        GameObject rayasBajo, rayasEncima, aro;
        readonly VisualElement capaNombres;
        /// <summary>Cada nombre: una caja (la etiqueta oscura con el borde del color) con el icono y el texto; un Label con hijos no se mide por su texto.</summary>
        readonly List<VisualElement> nombres = new List<VisualElement>();

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
            UnityEngine.Object.Destroy(cubo);
        }

        public static Vector4 Lineal(uint rgb) => (Vector4)new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255).ToColor().linear;

        /// <summary>Pinta las cajas de este fotograma: las que quedan a menos de `alcance` u de (cx, cy).</summary>
        public void Pintar(List<CosaDelMapa> cosas, double cx, double cy, double alcance)
        {
            var e = EscalaDelMapa.MetrosPorUnidad;
            for (int k = 0; k < 2; k++) { matrices[k].Clear(); colores[k].Clear(); }
            foreach (var c in cosas)
            {
                if (CosasDelMapa.Distancia(c.X, c.Y, cx, cy) > alcance) continue;
                int k = c.BajoNiebla ? 0 : 1;
                var suelo = alturaVisible(c.X, c.Y);
                var tamano = new Vector3((float)(c.Radio * 2), (float)c.Alto, (float)(c.Radio * 2)) * e;
                matrices[k].Add(Matrix4x4.TRS(EscalaDelMapa.AEscena(c.X, c.Y, suelo + tamano.y / 2), Quaternion.identity, tamano));
                colores[k].Add(Lineal(c.Color));
            }
            for (int k = 0; k < 2; k++)
            {
                var rp = new RenderParams(k == 0 ? bajoNiebla : encima) { shadowCastingMode = ShadowCastingMode.Off, matProps = propiedades };
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

        public void Nombres(List<CosaDelMapa> cosas, Camera camara, double cx, double cy, double lejos, bool nodos, double cercaNodos)
        {
            if (capaNombres?.panel == null) return;
            var e = EscalaDelMapa.MetrosPorUnidad;
            int usados = 0;
            foreach (var c in cosas)
            {
                bool nodo = c.Clase == ClaseDeCosa.Nodo;
                if (nodo && !nodos) continue;
                if (!nodo && !(c.Clase == ClaseDeCosa.Plaza || c.Clase == ClaseDeCosa.Mercenarios || c.Clase == ClaseDeCosa.Bandidos
                    || c.Clase == ClaseDeCosa.Ejercito || c.Clase == ClaseDeCosa.EjercitoAjeno || c.Clase == ClaseDeCosa.Formacion)) continue;
                var d = CosasDelMapa.Distancia(c.X, c.Y, cx, cy);
                if (d > (nodo ? cercaNodos : lejos)) continue;
                var mundo = EscalaDelMapa.AEscena(c.X, c.Y, alturaVisible(c.X, c.Y) + (float)(c.Alto + 0.25) * e);
                var pantalla = camara.WorldToScreenPoint(mundo);
                if (pantalla.z <= 0 || pantalla.x < 0 || pantalla.y < 0 || pantalla.x > camara.pixelWidth || pantalla.y > camara.pixelHeight) continue;
                // En fracción de la vista de la cámara sobre la capa (que cubre la pantalla): vale también si el panel pinta en una textura de otro tamaño.
                var enPanel = new Vector2(pantalla.x / camara.pixelWidth * capaNombres.layout.width, (1 - pantalla.y / camara.pixelHeight) * capaNombres.layout.height);
                if (usados == nombres.Count)
                {
                    var l = new VisualElement { pickingMode = PickingMode.Ignore };
                    l.AddToClassList("nombre-mapa");
                    // El icono de la cosa (el del mapa 2D del prototipo), delante del nombre.
                    var icono = new VisualElement { pickingMode = PickingMode.Ignore, name = "icono" };
                    icono.AddToClassList("nombre-mapa-icono");
                    l.Add(icono);
                    l.Add(new Label { pickingMode = PickingMode.Ignore, name = "texto" });
                    capaNombres.Add(l);
                    nombres.Add(l);
                }
                var label = nombres[usados++];
                label.Q<Label>("texto").text = c.Clase == ClaseDeCosa.Plaza ? $"{c.Nombre} · {c.Detalle}" : c.Clase == ClaseDeCosa.Bandidos ? $"Bandidos · {c.Detalle.ToLowerInvariant()}" : nodo ? c.Nombre
                    : c.Clase == ClaseDeCosa.Mercenarios ? c.Nombre : c.Detalle;
                var dibujo = label.Q("icono");
                // Las plazas y las columnas de una Facción llevan su sigilo, como los carteles del mapa 3D del prototipo (`mundo3d/estado.ts`: 40 px
                // en las plazas, 26 en las columnas); lo demás, su icono del mapa 2D.
                var ancho = c.Clase == ClaseDeCosa.Plaza ? 40 : 26;
                var sigilo = !string.IsNullOrEmpty(c.FaccionId) && (c.Clase == ClaseDeCosa.Plaza || c.Clase == ClaseDeCosa.Ejercito || c.Clase == ClaseDeCosa.EjercitoAjeno
                    || c.Clase == ClaseDeCosa.Formacion) ? SigiloDe?.Invoke(c.FaccionId, ancho) : null;
                if (sigilo != null)
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
                    var deCosa = IconosDelMundo.DeCosa(c);
                    var imagen = deCosa == null ? null : IconosDelMundo.Imagen("mapa", deCosa.Value.id);
                    dibujo.style.display = imagen != null ? DisplayStyle.Flex : DisplayStyle.None;
                    if (imagen != null)
                    {
                        dibujo.style.backgroundImage = new StyleBackground(imagen);
                        dibujo.style.unityBackgroundImageTintColor = deCosa.Value.color;
                    }
                }
                label.EnableInClassList("nombre-mapa--nodo", nodo);
                label.EnableInClassList("nombre-mapa--tenue", c.BajoNiebla && !nodo);
                label.style.borderLeftColor = new StyleColor(new Color32((byte)(c.Color >> 16), (byte)(c.Color >> 8), (byte)c.Color, 255));
                label.style.left = enPanel.x;
                label.style.top = enPanel.y;
                label.style.display = DisplayStyle.Flex;
            }
            for (int i = usados; i < nombres.Count; i++) nombres[i].style.display = DisplayStyle.None;
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
