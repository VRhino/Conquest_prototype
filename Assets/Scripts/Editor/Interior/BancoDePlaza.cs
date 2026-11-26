// El banco de pruebas de la ciudad por dentro: la escena `PlazaNivel1`, aparte del flujo del juego, para juzgar la escala contra el héroe, la
// luz y los materiales antes de llevarlo a `InteriorDelMotor`. Menú Conquest > Interior > Banco de la plaza (nivel 1), o por lotes:
//   Unity.exe -batchmode -quit -projectPath . -executeMethod ConquestTactics.Interior.Editor.BancoDePlaza.Crear
// Monta, a 3 m por unidad local (una celda del trazado, 9 m): una calle, tres chozas (las tres variantes de `BronzeAgeArte/interior/plaza1`)
// en sus parcelas de una celda con la puerta a la calle, el Centro Urbano del nivel 1 (el sitio de reunión con la casa del jefe), el sitio de
// la granja como volumen gris, y héroes de Synty de referencia. El suelo es el del mapa de mundo (`Resources/MapaMundo/Suelo`), con la tierra pisada pintada desde esa planta.
// La planta es fija, de ejemplo: la de verdad llega del motor cuando esto pase a `InteriorDelMotor`.
// También, cómo se importa lo de `Assets/Resources/Ciudad`: texturas que se repiten (color sRGB, normal, máscara lineal) y modelos con sus
// materiales por nombre.
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ConquestTactics.Interior.Editor
{
    sealed class ImportarCiudad : AssetPostprocessor
    {
        public const string Base = "Assets/Resources/Ciudad/";

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Base)) return;
            var t = (TextureImporter)assetImporter;
            bool pisado = assetPath.Contains("/Banco/");
            t.textureType = assetPath.EndsWith("_normal.png") ? TextureImporterType.NormalMap : TextureImporterType.Default;
            t.sRGBTexture = assetPath.EndsWith("_color.png");
            // las mieses se recortan por su alfa
            bool recorte = new[] { "trigo", "hierba", "flores", "retama" }.Any(x => assetPath.EndsWith(x + "_color.png"));
            t.alphaSource = recorte ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            t.alphaIsTransparency = recorte;
            t.mipmapEnabled = true;
            t.wrapMode = pisado ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            t.filterMode = FilterMode.Trilinear;
            t.anisoLevel = pisado ? 1 : 8;
            t.textureCompression = pisado ? TextureImporterCompression.Uncompressed : TextureImporterCompression.CompressedHQ;
        }

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(Base)) return;
            var m = (ModelImporter)assetImporter;
            m.importAnimation = false;
            m.importCameras = false;
            m.importLights = false;
            m.importNormals = ModelImporterNormals.Import;
            m.importTangents = ModelImporterTangents.CalculateMikk;
            m.addCollider = false;
            // cada material del modelo, por su nombre (barro, paja, madera, piedra, zarzo), es el de `Materiales/` (si ya existe: los crea el banco)
            foreach (var n in BancoDePlaza.Materiales)
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>($"{Base}Materiales/{n}.mat");
                if (mat != null) m.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), n), mat);
            }
        }

    }

    public static class BancoDePlaza
    {
        const string Base = ImportarCiudad.Base;
        const string Escena = "Assets/Scenes/BronzeAge/PlazaNivel1.unity";
        const string Suelo = "Assets/Resources/MapaMundo/Suelo/";
        public static readonly string[] Materiales = { "barro", "paja", "madera", "piedra", "zarzo", "roca", "tela", "piel", "corteza", "hojas", "labrado", "trigo", "pan", "lona", "toldo", "cipres", "olivo", "mata", "hierba", "flores", "retama", "bronce", "carbon", "adobe" };
        static readonly string[] Recortados = { "trigo", "hierba", "flores", "retama" };
        /// <summary>Metros por celda del trazado (3 unidades locales a 3 m).</summary>
        const float Celda = 9f;
        /// <summary>Lado de la zona que cubre la máscara de tierra pisada (m) y su lado en píxeles.</summary>
        const float Zona = 144f;
        /// <summary>El centro de la máscara en z: la plaza crece al sur (el patio de caravanas), así que la zona no se centra en el origen.</summary>
        const float ZonaZ = -8f;
        const int LadoPisado = 2048;

        [MenuItem("Conquest/Interior/Banco de la plaza (nivel 1)", false, 300)]
        static void Abrir()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(Escena)) Crear();
            else EditorSceneManager.OpenScene(Escena);
        }

        [MenuItem("Conquest/Interior/Banco de la plaza: rehacer", false, 301)]
        public static void Crear()
        {
            CrearMateriales();
            foreach (var f in Directory.GetFiles(Base + "Modelos", "*.fbx")) AssetDatabase.ImportAsset(f.Replace('\\', '/'), ImportAssetOptions.ForceUpdate);

            var escena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ── la planta de ejemplo, como la mandaría el motor (en unidades locales, y hacia el sur): la calle a lo largo, las parcelas a sus
            // lados y, al fondo, el Centro Urbano. La monta el mismo código que en el juego (`CiudadPorDentro`). ──
            var edificios = new System.Collections.Generic.List<(string, double, double, double, double)>();
            void Edificio(string tipo, double xm, double zm, double anchoM, double fondoM) => edificios.Add((tipo, xm / 3, -zm / 3, anchoM / 3, fondoM / 3));
            Edificio("centroUrbano", 9, 36, 36, 36);
            Edificio("vivienda", -9, -18, 9, 9); Edificio("vivienda", -9, -4, 9, 9); Edificio("vivienda", 9, -11, 9, 9);
            Edificio("granja", 22.5, 6, 18, 18); Edificio("patioDeGremios", -13.5, 9.3, 18, 18); Edificio("molino", -13.5, -32.5, 18, 18);
            Edificio("horno", 13.5, -24.5, 18, 9); Edificio("lenera", 9, -35.5, 9, 9); Edificio("almacen", -13.5, -47, 18, 9);
            Edificio("mercado", 18, -51, 27, 18); Edificio("granero", 40.5, -33, 36, 18); Edificio("albergueCaravanas", -18, -60.5, 27, 18);
            // los oficios, al oeste, a lo largo de su calle
            Edificio("fundicion", -36, 9, 18, 18); Edificio("curtiduria", -36, -12, 18, 18); Edificio("taberna", -36, -33, 18, 18);
            // y los de gobierno, al norte, a los lados del Centro Urbano
            Edificio("salaConsejo", -27, 42, 27, 27); Edificio("palacio", 50, 45, 36, 36);
            // y tres solares a medio hacer, al sur del mercado: una vivienda replanteada, otra en obra y un almacén en obra
            Edificio("vivienda:cola", 9, -67, 9, 9); Edificio("vivienda:obra", 9, -77.5, 9, 9); Edificio("almacen:obra", 22.5, -68, 18, 9);
            // y, al este, un recinto de empalizada con lo militar dentro (en unidades locales: x de 21 a 45, y de −8 a 22), con su calle, su
            // puerta al norte, torres en las esquinas, una esquina en diagonal y tres celdas aún sin levantar
            edificios.Add(("barracon", 28, -2, 6, 6)); edificios.Add(("caballerizas", 27, 6, 9, 6)); edificios.Add(("armeria", 28.5, 15, 6, 9));
            edificios.Add(("galeriaDeTiro", 38, 1, 6, 12)); edificios.Add(("carpinteria", 38.5, 14, 6, 12));
            // y cuatro hileras de viviendas al sureste, para ver las del nivel que se pida (`-nivel N`: el nivel de la plaza de ejemplo)
            for (int k = 0; k < 12; k++) Edificio("vivienda", 36 + (k % 3) * 9, -55 - (k / 3) * 13, 9, 9);
            var nivelArg = System.Environment.GetCommandLineArgs().SkipWhile(x => x != "-nivel").Skip(1).FirstOrDefault();
            int nivel = nivelArg != null ? int.Parse(nivelArg) : 1;
            var muralla = new System.Collections.Generic.List<(string, double, double, string)>();
            for (int x = 21; x <= 45; x += 3)
                foreach (var y in new[] { -8, 22 })
                {
                    if (x == 45 && y == 22) continue;                                       // la esquina que va en diagonal
                    muralla.Add((x == 33 && y == -8 ? "puerta" : x == 21 || (x == 45 && y == -8) ? "torre" : "muro", x, y, null));
                }
            for (int y = -5; y <= 19; y += 3)
                foreach (var x in new[] { 21, 45 })
                    muralla.Add((y == 7 ? "torre" : "muro", x, y, x == 45 && y >= 1 && y <= 7 ? "plan" : null));
            // la calle (de z = −78 a 18), el callejón del granero y la calle del recinto
            var calles = new[] { ("calle", 0.0, 10.0, 3.0, 32.0), ("camino", 4.5, 10.0, 6.0, 0.67), ("calle", 33.0, 7.0, 3.0, 27.0), ("calle", -8.3, 4.0, 1.0, 20.0), ("calle", 15.0, 20.2, 9.0, 1.0) };
            var montada = CiudadPorDentro.MontarDeTuplas(edificios, calles, (x, y, alto) => new Vector3((float)x * 3, alto, -(float)y * 3), null, alDia: true, muralla: muralla, nivel: nivel);
            var raiz = montada.Raiz;
            VidaDeLaPlaza.Posar(raiz);   // fuera de Play nadie anda: cada uno, a medio camino y con el paso echado
            raiz.name = "Plaza de ejemplo (nivel 1)";
            // la máscara de lo pisado, a un fichero (en el juego vive en memoria), y el suelo con el material del banco
            var rutaPisado = Base + "Banco/pisado.png";
            File.WriteAllBytes(rutaPisado, montada.Pisado.EncodeToPNG());
            AssetDatabase.ImportAsset(rutaPisado, ImportAssetOptions.ForceUpdate);
            var ms = Material("Banco/suelo", "Conquest/CiudadSuelo");
            ms.SetTexture("_SueloColor", AssetDatabase.LoadAssetAtPath<Texture2DArray>(Suelo + "suelo_color.png"));
            ms.SetTexture("_SueloNormal", AssetDatabase.LoadAssetAtPath<Texture2DArray>(Suelo + "suelo_normal.png"));
            ms.SetTexture("_SueloMascara", AssetDatabase.LoadAssetAtPath<Texture2DArray>(Suelo + "suelo_mascara.png"));
            ms.SetTexture("_Pisado", AssetDatabase.LoadAssetAtPath<Texture2D>(rutaPisado));
            ms.SetVector("_Zona", montada.Suelo.sharedMaterial.GetVector("_Zona"));
            EditorUtility.SetDirty(ms);
            montada.Suelo.sharedMaterial = ms;

            // ── el héroe de referencia: en una puerta, en la calle, en el corro, en el porche y en la granja ──
            var heroe = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonFantasyHeroCharacters/Prefabs/Characters_Presets/Chr_FantasyHero_Preset_1.prefab");
            if (heroe != null)
                foreach (var (pos, mira) in new[] { (new Vector3(-3.8f, 0, -18), Vector3.left), (new Vector3(1.5f, 0, -24), Vector3.forward), (new Vector3(5.6f, 0, 31.2f), Vector3.right),
                    (new Vector3(10.4f, 0.1f, 37.9f), Vector3.back), (new Vector3(16, 0, 9.4f), Vector3.right) })
                {
                    var h = (GameObject)PrefabUtility.InstantiatePrefab(heroe, raiz.transform);
                    h.name = "Héroe de referencia";
                    h.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(mira));
                    foreach (var a in h.GetComponentsInChildren<Animator>()) a.enabled = false;
                }

            Ambiente();

            // ── la cámara: a la altura de los ojos, en la calle ──
            var cam = new GameObject("Cámara") { tag = "MainCamera" };
            var camara = cam.AddComponent<Camera>();
            camara.fieldOfView = 55;
            camara.farClipPlane = 1500;
            cam.transform.position = new Vector3(3.2f, 1.75f, -36);
            cam.transform.rotation = Quaternion.LookRotation(new Vector3(-0.22f, 0.02f, 1));
            var datos = cam.AddComponent<UniversalAdditionalCameraData>();
            datos.renderPostProcessing = true;
            datos.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            cam.AddComponent<CamaraLibreDeBanco>();

            Directory.CreateDirectory(Path.GetDirectoryName(Escena));
            EditorSceneManager.SaveScene(escena, Escena);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Banco] escena creada en {Escena}");

            var captura = System.Environment.GetCommandLineArgs().SkipWhile(a => a != "-capturaDeBanco").Skip(1).FirstOrDefault();
            if (captura != null) Capturar(camara, captura);
        }

        /// <summary>Por lotes: abre la escena ya hecha y saca la imagen (`-capturaDeBanco ruta`).</summary>
        public static void Captura()
        {
            EditorSceneManager.OpenScene(Escena);
            var ruta = System.Environment.GetCommandLineArgs().SkipWhile(a => a != "-capturaDeBanco").Skip(1).First();
            PonerLuz();   // (el cielo de la luz vive en memoria: al abrir la escena hay que volver a ponerla)
            // `-vistas "x,y,z,cabeceo,guiñada;..."`: una imagen por vista (ruta_0.png, ruta_1.png...); sin ello, la de la cámara de la escena
            var vistas = System.Environment.GetCommandLineArgs().SkipWhile(a => a != "-vistas").Skip(1).FirstOrDefault();
            if (vistas == null) { Capturar(Camera.main, ruta); return; }
            var k = 0;
            foreach (var v in vistas.Split(';'))
            {
                var n = v.Split(',').Select(x => float.Parse(x, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                Camera.main.transform.SetPositionAndRotation(new Vector3(n[0], n[1], n[2]), Quaternion.Euler(n[3], n[4], 0));
                Capturar(Camera.main, ruta.Replace(".png", $"_{k++}.png"));
            }
        }

        static void CrearMateriales()
        {
            AssetDatabase.Refresh();
            foreach (var n in Materiales)
            {
                var m = Material("Materiales/" + n, "Conquest/Ciudad");
                m.SetTexture("_ColorTex", AssetDatabase.LoadAssetAtPath<Texture2D>($"{Base}Texturas/{n}_color.png"));
                m.SetTexture("_NormalTex", AssetDatabase.LoadAssetAtPath<Texture2D>($"{Base}Texturas/{n}_normal.png"));
                m.SetTexture("_Mascara", AssetDatabase.LoadAssetAtPath<Texture2D>($"{Base}Texturas/{n}_mascara.png"));
                m.enableInstancing = true;
                // el paño de los estandartes: el color de la Facción (en el banco, un rojo de ejemplo); sin suciedad del pie ni variación
                if (n == "tela") { m.SetColor("_Tono", new Color(0.62f, 0.16f, 0.11f)); m.SetFloat("_SuciedadFuerza", 0); m.SetFloat("_Variacion", 0); }
                bool hoja = n == "hojas" || n == "cipres" || n == "olivo" || n == "mata";
                if (hoja || n == "corteza" || n == "piel") m.SetFloat("_SuciedadFuerza", 0);
                // los paños llevan dos caras pegadas, que se tapan la una a la otra al hornear la oclusión: apenas se les aplica
                if (n == "tela" || n == "piel") m.SetFloat("_Oclusion", 0.15f);
                if (hoja) { m.SetColor("_Tono", n == "olivo" ? new Color(1.5f, 1.55f, 1.5f) : new Color(1.45f, 1.45f, 1.25f)); m.SetFloat("_Oclusion", 0.5f); m.SetFloat("_Relieve", 0.5f); }
                if (n == "corteza") { m.SetColor("_Tono", Color.white); m.SetFloat("_Oclusion", 0.5f); }   // (la corteza ya es propia: no hay que aclararla)
                if (n == "carbon") { m.SetFloat("_SuciedadFuerza", 0); m.SetFloat("_Variacion", 0); }
                // las mieses: recortadas por su alfa, con sus dos caras
                if (Recortados.Contains(n)) { m.SetFloat("_Recorte", 1); m.EnableKeyword("_RECORTE"); m.SetFloat("_Cull", 0); m.SetFloat("_SuciedadFuerza", 0); m.SetFloat("_Oclusion", 0.35f); m.SetFloat("_Variacion", 0); }
                if (n == "labrado") { m.SetFloat("_SuciedadFuerza", 0); m.SetFloat("_Variacion", 0); }
                if (n == "bronce") { m.SetFloat("_Metal", 0.7f); m.SetFloat("_SuciedadFuerza", 0); m.SetFloat("_Variacion", 0); m.SetFloat("_Oclusion", 0.4f); }
                if (n == "pan") { m.SetFloat("_SuciedadFuerza", 0); m.SetFloat("_Variacion", 0); }
                EditorUtility.SetDirty(m);
            }
            AssetDatabase.SaveAssets();
        }

        /// <summary>El material de esa ruta (bajo `Resources/Ciudad`), creado si no existe; siempre con ese shader.</summary>
        static Material Material(string nombre, string shader)
        {
            var ruta = $"{Base}{nombre}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
            var s = Shader.Find(shader);
            if (s == null) throw new System.Exception($"[Banco] no está el shader {shader}");
            if (m == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ruta));
                m = new Material(s);
                AssetDatabase.CreateAsset(m, ruta);
            }
            else m.shader = s;
            return m;
        }

        /// <summary>La luz fija de la ciudad (el motor no tiene día y noche): sol de media tarde, cálido; el cielo procedural, que da la luz
        /// de ambiente y los reflejos; bruma; y el postproceso (tonos, balance cálido, algo de contraste y saturación, bloom bajo, viñeta).</summary>
        static void Ambiente()
        {
            var cielo = Material("Banco/cielo", "Skybox/Procedural");
            cielo.SetFloat("_SunSize", 0.035f);
            cielo.SetFloat("_AtmosphereThickness", 0.85f);
            cielo.SetColor("_SkyTint", new Color(0.52f, 0.56f, 0.6f));
            cielo.SetColor("_GroundColor", new Color(0.46f, 0.42f, 0.36f));
            cielo.SetFloat("_Exposure", 1.15f);
            EditorUtility.SetDirty(cielo);

            // los tres perfiles de postproceso que mezcla `LuzDeLaPlaza` según la hora: el de siempre, el frío de la noche y el cálido del ocaso
            VolumeProfile Perfil(string nombre, System.Action<System.Func<System.Type, VolumeComponent>> llenar)
            {
                var ruta = Base + "Banco/" + nombre + ".asset";
                AssetDatabase.DeleteAsset(ruta);
                var perfil = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(perfil, ruta);
                llenar(t =>
                {
                    var c = perfil.Add(t, true);
                    c.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                    AssetDatabase.AddObjectToAsset(c, perfil);
                    return c;
                });
                EditorUtility.SetDirty(perfil);
                return perfil;
            }
            Perfil("Ambiente", poner =>
            {
                ((Tonemapping)poner(typeof(Tonemapping))).mode.Override(TonemappingMode.Neutral);
                var color = (ColorAdjustments)poner(typeof(ColorAdjustments));
                color.postExposure.Override(0.15f);
                color.contrast.Override(10f);
                color.saturation.Override(8f);
                ((WhiteBalance)poner(typeof(WhiteBalance))).temperature.Override(6f);
                var bloom = (Bloom)poner(typeof(Bloom));
                bloom.threshold.Override(1.05f);
                bloom.intensity.Override(0.2f);
                bloom.scatter.Override(0.6f);
                var vineta = (Vignette)poner(typeof(Vignette));
                vineta.intensity.Override(0.18f);
                vineta.smoothness.Override(0.5f);
            });
            Perfil("AmbienteNoche", poner =>
            {
                var color = (ColorAdjustments)poner(typeof(ColorAdjustments));
                color.postExposure.Override(0.4f);
                color.contrast.Override(6f);
                color.saturation.Override(-2f);
                ((WhiteBalance)poner(typeof(WhiteBalance))).temperature.Override(-14f);
                var bloom = (Bloom)poner(typeof(Bloom));
                bloom.threshold.Override(0.85f);
                bloom.intensity.Override(0.55f);          // los fuegos, de noche, con su halo
                bloom.scatter.Override(0.7f);
            });
            Perfil("AmbienteOcaso", poner =>
            {
                var color = (ColorAdjustments)poner(typeof(ColorAdjustments));
                color.postExposure.Override(0.15f);
                color.contrast.Override(10f);
                color.saturation.Override(6f);
                ((WhiteBalance)poner(typeof(WhiteBalance))).temperature.Override(10f);
            });
            AssetDatabase.SaveAssets();
            PonerLuz();
        }

        /// <summary>La luz de la plaza a la hora de `-hora H` (por defecto, las 17: la tarde con la que se compuso todo).</summary>
        static void PonerLuz()
        {
            foreach (var vieja in Object.FindObjectsByType<LuzDeLaPlaza>(FindObjectsSortMode.None)) Object.DestroyImmediate(vieja.gameObject);
            var hora = System.Environment.GetCommandLineArgs().SkipWhile(a => a != "-hora").Skip(1).FirstOrDefault();
            LuzDeLaPlaza.HoraFija = hora != null ? float.Parse(hora, System.Globalization.CultureInfo.InvariantCulture) : 17f;
            LuzDeLaPlaza.Poner(null, null);
        }

        /// <summary>Una imagen de lo que ve la cámara, para comprobar por lotes que la escena sale como debe.</summary>
        static void Capturar(Camera camara, string ruta)
        {
            var rt = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            camara.targetTexture = rt;
            // dos veces: recién cargados, en el primer fotograma los materiales aún no están subidos y todas las submallas de un modelo salen
            // con el mismo (solo pasa en la captura por lotes)
            // (en el editor, sin Play, las partículas no corren: se adelantan para la imagen)
            foreach (var ps in Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None)) ps.Simulate(6f, true, true);
            camara.Render();
            camara.Render();
            RenderTexture.active = rt;
            var t = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            t.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            File.WriteAllBytes(ruta, t.EncodeToPNG());
            RenderTexture.active = null;
            camara.targetTexture = null;
            Debug.Log($"[Banco] captura en {ruta}");
        }
    }
}
